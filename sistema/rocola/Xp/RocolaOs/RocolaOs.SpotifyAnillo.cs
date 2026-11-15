// SPOTIFY EN LA ROCOLA (fase 11, grupo spotify-xp): EL ANILLO, el lado del LECTOR (la interfaz I1 de FASE11_spotify-xp.md).
//
// Soloist (el cliente oficial de Spotify para Linux) toca por PulseAudio; en Rocola OS no hay PulseAudio: carga NUESTRO libpulse.so.0
// (rocola-pulso, sistema/rocola/pulso, de otro grupo), que deja el sonido en este anillo de memoria compartida. rocola-xp lo lee aqui, en el
// hilo del sonido, como una fuente mas de XP (RocolaOs.SpotifyFuente.cs). EL RITMO LO MARCA EL LECTOR: el escritor solo deja escribir lo que
// falta para llegar a su objetivo (el tlength que pidio Soloist, unos 250 ms), asi que la tarjeta de la rocola es el reloj de Spotify.
//
// El archivo (I1): ROCOLA_SPOTIFY_ANILLO o /run/rocola-xp/spotify/sonido.anillo (ROCOLA_RUN en las pruebas); carpetas 0755, archivo 0644;
// 4096 bytes de cabecera y 65536 cuadros de float32 L,R. Lo crea quien llegue primero y no lo encuentre valido, escribiendolo entero en
// <ruta>.t.<pid> y cambiandole el nombre encima (atomico); nadie lo trunca ni lo borra: si alguien lo rehace, el otro lo ve porque cambio el
// inodo (aqui se mira cada segundo desde el hilo de Spotify: Mira) y lo vuelve a abrir. El mapa viejo no se suelta enseguida: el hilo del
// sonido puede estar leyendo de el (se suelta a los 10 s).
//
// El orden de memoria (I1): el escritor copia los datos y publica «escrito» con release; aqui se lee «escrito» con acquire, se copia y se
// publica «leido» con release. Con un «vaciado» nuevo (gen_vaciado, que el escritor sube DESPUES de poner vaciado_hasta) lo pendiente de
// antes se tira: leido = max(leido, vaciado_hasta).
//
// EL HILO DEL SONIDO NUNCA ESPERA: Toma copia lo que haya (puede ser nada) y vuelve; ni cerrojos ni llamadas al sistema que esperen
// (clock_gettime no espera).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

namespace Rocola
{
    internal sealed unsafe class RocolaOsSpotifyAnillo
    {
        // ---------- el formato (I1) ----------
        internal const uint Magia = 0x4F4C5550;   // «PULO» en little endian
        internal const uint Version = 1;
        internal const int Capacidad = 65536;     // cuadros
        internal const int Canales = 2;
        internal const int Cabecera = 4096;
        internal const long Tamano = Cabecera + (long)Capacidad * Canales * 4;
        // desplazamientos de la cabecera
        internal const int OMagia = 0, OVersion = 4, OCapacidad = 8, OCanales = 12, OFrecuencia = 16, OGenFlujo = 20, OTapado = 24,
            OGenVaciado = 28, OEscrito = 32, OLeido = 40, OVaciadoHasta = 48, OLatidoEscritor = 56, OLatidoLector = 64, OPidEscritor = 72,
            OLectorPresente = 76, OVolumenPedido = 80, OFormatoOriginal = 84, OSubdesbordes = 88, OLatenciaLector = 96;

        // LA RUTA (I1)
        internal static string Ruta
        {
            get
            {
                string v = null;
                try { v = Environment.GetEnvironmentVariable("ROCOLA_SPOTIFY_ANILLO"); } catch { }
                return string.IsNullOrWhiteSpace(v) ? RocolaOs.Run("rocola-xp/spotify/sonido.anillo") : v.Trim();
            }
        }

        // ---------- el mapa abierto ----------
        private sealed class Mapa
        {
            internal FileStream Archivo;
            internal MemoryMappedFile Mm;
            internal MemoryMappedViewAccessor Vista;
            internal byte* Base;
            internal ulong Dev, Ino;
            internal long SueltoTick;   // cuando se retiro (para soltarlo a los 10 s)
            internal void Suelta()
            {
                try { if (Base != null) Vista.SafeMemoryMappedViewHandle.ReleasePointer(); } catch { }
                Base = null;
                try { Vista?.Dispose(); } catch { }
                try { Mm?.Dispose(); } catch { }
                try { Archivo?.Dispose(); } catch { }
            }
        }
        private volatile Mapa actual;
        private readonly List<Mapa> retirados = new List<Mapa>();   // (solo el hilo de Spotify)
        private readonly string ruta;
        internal string RutaAbierta { get { return ruta; } }
        internal RocolaOsSpotifyAnillo(string ruta) { this.ruta = ruta; }
        internal bool Abierto { get { return actual != null; } }
        internal string UltimoError;
        internal int Reaperturas;

        // ---------- P/Invoke (libc; Rocola OS y la maquina de pruebas son x86-64 con glibc >= 2.33: stat y fstat son funciones) ----------
        [DllImport("libc", SetLastError = true)] private static extern int stat(string ruta, byte* buf);
        [DllImport("libc", SetLastError = true)] private static extern int fstat(int fd, byte* buf);
        [DllImport("libc", SetLastError = true)] private static extern int chmod(string ruta, uint modo);
        [DllImport("libc")] private static extern int clock_gettime(int reloj, long* ts);
        [DllImport("libc")] private static extern int getpid();
        // el dispositivo y el inodo (struct stat de x86-64: st_dev en 0, st_ino en 8; la estructura mide 144 bytes)
        private static bool Inodo(string r, out ulong dev, out ulong ino)
        {
            byte* b = stackalloc byte[256];
            dev = ino = 0;
            if (stat(r, b) != 0) return false;
            dev = *(ulong*)b; ino = *(ulong*)(b + 8);
            return true;
        }
        private static bool InodoDe(FileStream f, out ulong dev, out ulong ino)
        {
            byte* b = stackalloc byte[256];
            dev = ino = 0;
            int fd = (int)f.SafeFileHandle.DangerousGetHandle();
            if (fstat(fd, b) != 0) return false;
            dev = *(ulong*)b; ino = *(ulong*)(b + 8);
            return true;
        }
        // CLOCK_MONOTONIC en nanosegundos (I1: los latidos)
        internal static long Monotonico()
        {
            long* ts = stackalloc long[2];
            clock_gettime(1, ts);
            return ts[0] * 1000000000L + ts[1];
        }

        // ---------- abrir, comprobar, rehacer ----------
        // ABRE (o rehace) el anillo: desde el hilo de Spotify, nunca desde el del sonido. true si queda abierto.
        internal bool Abre()
        {
            try
            {
                string carpeta = Path.GetDirectoryName(ruta);
                if (!string.IsNullOrEmpty(carpeta)) CreaCarpetas(carpeta);
                if (!ValidoEnDisco()) Crea();
                Mapa m = AbreMapa();
                if (m == null) { UltimoError = "no se pudo abrir el anillo"; return false; }
                Mapa viejo = actual;
                actual = m;
                if (viejo != null) { viejo.SueltoTick = Environment.TickCount64; retirados.Add(viejo); Reaperturas++; }
                UltimoError = null;
                return true;
            }
            catch (Exception ex) { UltimoError = ex.GetType().Name + ": " + ex.Message; return false; }
        }
        // CADA SEGUNDO (hilo de Spotify): si el archivo de la ruta ya no es el que esta abierto (otro lo rehizo) o dejo de valer, se reabre;
        // y se sueltan los mapas retirados hace mas de 10 s.
        internal void Mira()
        {
            for (int i = retirados.Count - 1; i >= 0; i--)
                if (Environment.TickCount64 - retirados[i].SueltoTick > 10000) { retirados[i].Suelta(); retirados.RemoveAt(i); }
            Mapa m = actual;
            ulong dev, ino;
            if (m == null || !Inodo(ruta, out dev, out ino) || dev != m.Dev || ino != m.Ino || !CabeceraValida(m.Base)) Abre();
        }
        internal void Cierra()
        {
            Mapa m = actual; actual = null;
            if (m != null) { try { Volatile.Write(ref *(uint*)(m.Base + OLectorPresente), 0u); } catch { } m.SueltoTick = Environment.TickCount64; retirados.Add(m); }
            // (los retirados se sueltan en el siguiente Mira o al soltar todo)
        }
        internal void SueltaTodo()
        {
            Cierra();
            foreach (Mapa r in retirados) r.Suelta();
            retirados.Clear();
        }

        private static void CreaCarpetas(string carpeta)
        {
            if (Directory.Exists(carpeta)) return;
            string padre = Path.GetDirectoryName(carpeta);
            if (!string.IsNullOrEmpty(padre)) CreaCarpetas(padre);
            Directory.CreateDirectory(carpeta);
            chmod(carpeta, 0x1ED);   // 0755
        }
        private static bool CabeceraValida(byte* b)
        {
            return b != null && *(uint*)(b + OMagia) == Magia && *(uint*)(b + OVersion) == Version && *(uint*)(b + OCapacidad) == (uint)Capacidad
                && *(uint*)(b + OCanales) == (uint)Canales;
        }
        private bool ValidoEnDisco()
        {
            try
            {
                FileInfo fi = new FileInfo(ruta);
                if (!fi.Exists || fi.Length != Tamano) return false;
                byte[] c = new byte[16];
                using (FileStream f = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    if (f.Read(c, 0, 16) != 16) return false;
                fixed (byte* p = c) return CabeceraValida(p);
            }
            catch { return false; }
        }
        // LO CREA: entero en <ruta>.t.<pid>, con la cabecera y lo demas a cero, y le cambia el nombre encima (atomico)
        private void Crea()
        {
            string t = ruta + ".t." + getpid().ToString(CultureInfo.InvariantCulture);
            using (FileStream f = new FileStream(t, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] cab = new byte[Cabecera];
                fixed (byte* p = cab)
                {
                    *(uint*)(p + OMagia) = Magia; *(uint*)(p + OVersion) = Version; *(uint*)(p + OCapacidad) = (uint)Capacidad;
                    *(uint*)(p + OCanales) = (uint)Canales; *(uint*)(p + OTapado) = 1;
                }
                f.Write(cab, 0, cab.Length);
                f.SetLength(Tamano);   // (lo de los datos, a cero)
            }
            chmod(t, 0x1A4);   // 0644
            File.Move(t, ruta, true);
        }
        private Mapa AbreMapa()
        {
            Mapa m = new Mapa();
            try
            {
                m.Archivo = new FileStream(ruta, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                if (m.Archivo.Length != Tamano) { m.Suelta(); return null; }
                if (!InodoDe(m.Archivo, out m.Dev, out m.Ino)) { m.Suelta(); return null; }
                m.Mm = MemoryMappedFile.CreateFromFile(m.Archivo, null, Tamano, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);
                m.Vista = m.Mm.CreateViewAccessor(0, Tamano, MemoryMappedFileAccess.ReadWrite);
                byte* b = null;
                m.Vista.SafeMemoryMappedViewHandle.AcquirePointer(ref b);
                m.Base = b + m.Vista.PointerOffset;
                if (!CabeceraValida(m.Base)) { m.Suelta(); return null; }
                return m;
            }
            catch { m.Suelta(); throw; }
        }

        // ---------- lo que se lee de la cabecera (desde cualquier hilo) ----------
        private static ref long L(byte* b, int o) { return ref *(long*)(b + o); }
        private static ref uint U(byte* b, int o) { return ref *(uint*)(b + o); }
        internal long Escrito { get { Mapa m = actual; return m == null ? 0 : Volatile.Read(ref L(m.Base, OEscrito)); } }
        internal long Leido { get { Mapa m = actual; return m == null ? 0 : Volatile.Read(ref L(m.Base, OLeido)); } }
        internal int Frecuencia { get { Mapa m = actual; return m == null ? 0 : (int)Volatile.Read(ref U(m.Base, OFrecuencia)); } }
        internal uint GenFlujo { get { Mapa m = actual; return m == null ? 0 : Volatile.Read(ref U(m.Base, OGenFlujo)); } }
        internal bool Tapado { get { Mapa m = actual; return m == null || Volatile.Read(ref U(m.Base, OTapado)) != 0; } }
        internal long LatidoEscritorNs { get { Mapa m = actual; return m == null ? 0 : Volatile.Read(ref L(m.Base, OLatidoEscritor)); } }
        internal int PidEscritor { get { Mapa m = actual; return m == null ? 0 : (int)Volatile.Read(ref U(m.Base, OPidEscritor)); } }
        internal uint VolumenPedido { get { Mapa m = actual; return m == null ? 0 : Volatile.Read(ref U(m.Base, OVolumenPedido)); } }
        internal long Subdesbordes { get { Mapa m = actual; return m == null ? 0 : Volatile.Read(ref L(m.Base, OSubdesbordes)); } }
        // lo pendiente (sin contar lo que un vaciado ya tiro)
        internal long Pendientes
        {
            get
            {
                Mapa m = actual; if (m == null) return 0;
                long l = Math.Max(Volatile.Read(ref L(m.Base, OLeido)), Volatile.Read(ref L(m.Base, OVaciadoHasta)));
                return Math.Max(0, Volatile.Read(ref L(m.Base, OEscrito)) - l);
            }
        }

        // ---------- lo que escribe el lector ----------
        // EL LATIDO DEL LECTOR y si esta (desde el hilo de Spotify, al menos cada 500 ms mientras una cancion de Spotify esta abierta)
        internal void Late(bool presente, int latenciaUs)
        {
            Mapa m = actual; if (m == null) return;
            Volatile.Write(ref U(m.Base, OLatenciaLector), (uint)Math.Max(0, latenciaUs));
            Volatile.Write(ref L(m.Base, OLatidoLector), Monotonico());
            Volatile.Write(ref U(m.Base, OLectorPresente), presente ? 1u : 0u);
        }

        // ---------- el hilo del sonido ----------
        private uint genVaciadoVisto;
        private bool genVaciadoSabido;
        private bool enSubdesborde;
        // TIRA LO PENDIENTE (lo de antes de pedir una cancion): leido = escrito. Desde el hilo del sonido (el unico que mueve «leido»).
        internal void Tira()
        {
            Mapa m = actual; if (m == null) return;
            long e = Volatile.Read(ref L(m.Base, OEscrito));
            if (e > Volatile.Read(ref L(m.Base, OLeido))) Volatile.Write(ref L(m.Base, OLeido), e);
        }
        // SALTA hasta el cuadro «desde» (lo de antes de el no se oye). Hilo del sonido.
        internal void SaltaA(long desde)
        {
            Mapa m = actual; if (m == null) return;
            if (desde > Volatile.Read(ref L(m.Base, OLeido))) Volatile.Write(ref L(m.Base, OLeido), Math.Min(desde, Volatile.Read(ref L(m.Base, OEscrito))));
        }
        // EL SALTO DE UN VACIADO SIN CONSUMIR (revision; el LEEME de rocola-pulso, «Lo que el LECTOR tiene que hacer», paso 4: el escritor mide su
        // sitio con «leido» tal cual, y lo nuevo espera en su cola hasta que el lector salta): si hubo un vaciado nuevo, leido = max(leido,
        // vaciado_hasta). Lo llama la fuente mientras espera a que Soloist toque la suya. Hilo del sonido.
        internal void SaltaVaciado()
        {
            Mapa m = actual; if (m == null) return;
            byte* b = m.Base;
            uint gv = Volatile.Read(ref U(b, OGenVaciado));
            if (genVaciadoSabido && gv == genVaciadoVisto) return;
            long vh = Volatile.Read(ref L(b, OVaciadoHasta));
            if (vh > Volatile.Read(ref L(b, OLeido))) Volatile.Write(ref L(b, OLeido), vh);
            genVaciadoVisto = gv; genVaciadoSabido = true;
        }
        // TOMA hasta destino.Length/2 cuadros (float L,R intercalados), sin pasar de «hasta» (el cuadro absoluto donde acaba la cancion;
        // long.MaxValue: sin limite). Devuelve los CUADROS copiados (puede ser 0). Nunca espera. Hilo del sonido.
        // Con «paraEnVaciado», un vaciado nuevo (Soloist empezo otra cosa a media cancion: un celular, un salto) no se lee: se tira lo
        // pendiente y vuelve con «vaciado» = true y 0 cuadros (la cancion acaba ahi).
        internal int Toma(Span<float> destino, long hasta, bool paraEnVaciado, out long leidoDespues, out bool vaciado)
        {
            leidoDespues = 0; vaciado = false;
            Mapa m = actual;
            if (m == null) return 0;
            byte* b = m.Base;
            uint gv = Volatile.Read(ref U(b, OGenVaciado));
            long leido = Volatile.Read(ref L(b, OLeido));
            if (!genVaciadoSabido || gv != genVaciadoVisto)
            {   // un vaciado nuevo: lo pendiente de antes se tira (el escritor puso vaciado_hasta ANTES de subir gen_vaciado)
                long vh = Volatile.Read(ref L(b, OVaciadoHasta));
                if (vh > leido) leido = vh;
                vaciado = genVaciadoSabido;
                genVaciadoVisto = gv; genVaciadoSabido = true;
                if (vaciado && paraEnVaciado)
                {
                    Volatile.Write(ref L(b, OLeido), leido);
                    leidoDespues = leido;
                    return 0;
                }
            }
            long escrito = Volatile.Read(ref L(b, OEscrito));   // (acquire: lo copiado antes por el escritor ya se ve)
            if (escrito - leido > Capacidad) leido = escrito - Capacidad;   // (no deberia: el escritor nunca deja mas de la capacidad)
            long tope = Math.Min(escrito, hasta);
            long k = Math.Min(tope - leido, destino.Length / Canales);
            if (k < 0) k = 0;
            float* datos = (float*)(b + Cabecera);
            long hecho = 0;
            while (hecho < k)
            {
                long pos = (leido + hecho) % Capacidad;
                long trozo = Math.Min(k - hecho, Capacidad - pos);
                new ReadOnlySpan<float>(datos + pos * Canales, (int)(trozo * Canales)).CopyTo(destino.Slice((int)(hecho * Canales)));
                hecho += trozo;
            }
            leido += k;
            Volatile.Write(ref L(b, OLeido), leido);   // (release)
            Volatile.Write(ref L(b, OLatidoLector), Monotonico());
            // sin datos con el flujo sonando: un subdesborde (se cuenta una vez por cada vez que se queda seco)
            bool seco = k * Canales < destino.Length && leido >= escrito && Volatile.Read(ref U(b, OTapado)) == 0 && hasta == long.MaxValue;
            if (seco && !enSubdesborde) Interlocked.Increment(ref L(b, OSubdesbordes));
            enSubdesborde = seco;
            leidoDespues = leido;
            return (int)k;
        }
    }
}
