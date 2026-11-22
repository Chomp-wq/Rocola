// R1: RELLENAR LOS HUECOS DE LOS MP3 VIEJOS (decision del dueno del 22-11-2026: «R1: rellenar huecos de los MP3 viejos» en Rocola OS; la
// propuesta, AUDIO_P1_D1_D2_Y_TECNICAS_MODEST_2026-10-30.md; por que no se pudo en XP, AUDIO_R2_Y_SEPARACION_VOZ_MODEST_2026-10-31.md §2;
// como quedo, FASE11_relleno-huecos.md). Dos funciones de Rocola OS, como R2 (el pre-eco: el repaso mide, el interruptor decide):
//   huecos_mide     (ENCENDIDA de fabrica: solo mide, no cambia el sonido) un repaso de fondo (RocolaOs.Hilo, nice 19) lee de cada MP3 de la
//                   biblioteca tres tramos (al 20, 50 y 80 %, unos 380 KB cada uno; el archivo entero si es chico) con el lector de
//                   RocolaOs.HuecosMp3.cs y cuenta, de 3 kHz al corte de cada granulo, las lineas que vinieron a cero y cuanto les pondria el
//                   relleno frente a lo que traen esas bandas (en dB). Una vez por archivo (otra si cambia su tamano o su fecha), en
//                   rocola_huecos.txt de la carpeta de datos («ruta TAB tamano TAB fecha TAB algoritmo TAB cuadros TAB candidatas TAB huecos
//                   TAB relleno en decimas de dB»; solo numeros). Una cancion TRAE HUECOS si su relleno llega a UmbralDb (-20 dB) o mas, con
//                   un minimo de lineas candidatas (medido con musica sintetica: LAME 320, 192 y 128 dan -39, -25 y -21 dB y no; LAME 96 y 64
//                   y shine 128 y 96, de -18 a -14 dB, si). El veredicto se saca de los numeros al leerlos: cambiar el umbral no obliga a
//                   medir otra vez.
//   relleno_huecos  (APAGADA de fabrica: cambia el sonido y hay que oirlo en el bar antes) en las canciones que TRAEN HUECOS, el descompresor
//                   de la cancion (libmpg123, o NLayer en coma flotante) va envuelto: cada cuadro se descomprime como siempre y el mismo
//                   cuadro pasa por el lector de los huecos, que da el relleno ya sintetizado, y se suma (RocolaOsRellenoHuecos). Ganchos
//                   g260 (Codigo/Form1.Sonido.cs, AbreMp3) y g261 (Codigo/CancionEnMemoria.cs, AbreMp3 desde la memoria): solo donde se abre
//                   una cancion PARA QUE SUENE; lo que mide XP (LectorMedida) no se envuelve. «Escuchar sin mejoras» lo deja pasar (desde el
//                   latido, en un segundo como mucho). Encenderla vale desde el siguiente arranque y para las canciones que se abran desde
//                   entonces (como R2, que no se mete a media cancion).
// APAGADA (o sin medida, o una cancion sin huecos, o MPEG-2/2.5): g260 y g261 devuelven el MISMO descompresor de XP: bit a bit como hoy.
// SIN ROCOLA_OS: tambien (RocolaOs.Activo es false), y no se mide ni se escribe nada.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    internal static class RocolaOsHuecos
    {
        internal const string Mide = "huecos_mide", Rellena = "relleno_huecos";
        internal const string Archivo = "rocola_huecos.txt";
        internal const int Algoritmo = 1;
        internal const double UmbralDb = -20.0;         // desde cuanto relleno frente a lo que traen las bandas «trae huecos» (FASE11_relleno-huecos.md)
        internal const long CandidatasMin = 20000;      // y con cuantas lineas candidatas como minimo (una cancion casi muda en los agudos no opina)
        internal const int TramoBytes = 384 * 1024, Tramos = 3;
        internal const int PausaMs = 50;                // entre archivo y archivo (y lo que tarde cada uno)

        private sealed class Entrada { internal long Tam, Fecha; internal int Alg; internal long Cuadros, Candidatas, Huecos; internal int DecimasDb = -990; }
        private static readonly Dictionary<string, Entrada> porRuta = new Dictionary<string, Entrada>(StringComparer.Ordinal);   // (solo su hilo)
        private static Dictionary<string, float> conHuecos = new Dictionary<string, float>(StringComparer.Ordinal);            // publicado entero
        private static List<string> pendiente;
        private static readonly object cerrojo = new object();
        private static readonly AutoResetEvent despierta = new AutoResetEvent(false);
        private static Thread hilo;
        private static volatile bool parar;
        private static bool leido;
        internal static int Medidas, ConHuecosN, Mirados;
        internal static double MsMedida;                 // lo que tardo la ultima medida (para vivo.txt)
        internal static volatile bool Listo;
        internal static volatile bool SinMejoras;        // «Escuchar sin mejoras» (lo pone el latido de relleno_huecos)
        internal static int Sonando;                     // cuantos descompresores envueltos hay abiertos
        internal static long RellenadosSonando;          // bandas rellenadas en las canciones abiertas (para vivo.txt)
        private static readonly bool SinPausa = Environment.GetEnvironmentVariable("ROCOLA_HUECOS_SIN_PAUSA") == "1";   // (pruebas)

        // ¿esta cancion TRAE HUECOS? (desde cualquier hilo: el diccionario publicado no cambia)
        internal static bool TraeHuecos(string ruta)
        {
            float p;
            return ruta != null && Volatile.Read(ref conHuecos).TryGetValue(ruta, out p);
        }
        internal static bool Veredicto(long candidatas, double rellenoDb) { return candidatas >= CandidatasMin && rellenoDb >= UmbralDb; }
        internal static int Decimas(double db) { return (int)Math.Round(Math.Max(-99.0, Math.Min(99.0, db)) * 10); }

        internal static void Repasa(List<string> rutas)
        {
            lock (cerrojo)
            {
                pendiente = rutas;
                if (hilo == null) hilo = RocolaOs.Hilo(Mide, Bucle);
            }
            despierta.Set();
        }
        internal static void Para() { parar = true; despierta.Set(); }

        private static void Bucle()
        {
            while (!parar)
            {
                List<string> rutas;
                lock (cerrojo) { rutas = pendiente; pendiente = null; }
                if (rutas == null) { despierta.WaitOne(60000); continue; }
                if (!leido) { leido = true; Lee(); Publica(); }
                Pasa(rutas);
                Publica();
                Listo = true;
            }
        }

        private static string Ruta { get { return RocolaOs.Datos(Archivo); } }
        private static void Lee()
        {
            int lineas = 0;
            try
            {
                if (!File.Exists(Ruta)) return;
                foreach (string l in File.ReadAllLines(Ruta, Encoding.UTF8))
                {
                    lineas++;
                    string[] c = l.Split('\t');
                    if (c.Length < 8) continue;
                    Entrada x = new Entrada();
                    long.TryParse(c[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.Tam);
                    long.TryParse(c[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.Fecha);
                    int.TryParse(c[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.Alg);
                    long.TryParse(c[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.Cuadros);
                    long.TryParse(c[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.Candidatas);
                    long.TryParse(c[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.Huecos);
                    int.TryParse(c[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.DecimasDb);
                    porRuta[c[0]] = x;
                }
                // (como rocola_letras_mp3.txt) solo se anaden renglones: si sobra mas de un tercio (y al menos 200), se reescribe con el ultimo de cada uno
                if (lineas - porRuta.Count >= 200 && lineas > porRuta.Count * 3 / 2)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (KeyValuePair<string, Entrada> kv in porRuta) Renglon(sb, kv.Key, kv.Value);
                    string t = Ruta + ".nuevo";
                    File.WriteAllText(t, sb.ToString(), new UTF8Encoding(false));
                    File.Move(t, Ruta, true);
                    RocolaOs.Apunta(Mide, Archivo + " compactado: " + lineas.ToString(CultureInfo.InvariantCulture) + " renglones a " + porRuta.Count.ToString(CultureInfo.InvariantCulture));
                }
            }
            catch (Exception ex) { RocolaOs.Falla(Mide, "leer " + Archivo, ex); }
        }
        private static void Renglon(StringBuilder sb, string ruta, Entrada x)
        {
            sb.Append(ruta.Replace('\t', ' ').Replace('\n', ' ')).Append('\t').Append(x.Tam.ToString(CultureInfo.InvariantCulture)).Append('\t')
              .Append(x.Fecha.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(x.Alg.ToString(CultureInfo.InvariantCulture)).Append('\t')
              .Append(x.Cuadros.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(x.Candidatas.ToString(CultureInfo.InvariantCulture)).Append('\t')
              .Append(x.Huecos.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(x.DecimasDb.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        private static void Publica()
        {
            Dictionary<string, float> d = new Dictionary<string, float>(StringComparer.Ordinal);
            int medidas = 0;
            foreach (KeyValuePair<string, Entrada> kv in porRuta)
            {
                if (kv.Value.Alg != Algoritmo) continue;
                medidas++;
                if (Veredicto(kv.Value.Candidatas, kv.Value.DecimasDb / 10.0)) d[kv.Key] = kv.Value.DecimasDb / 10f;
            }
            Medidas = medidas; ConHuecosN = d.Count;
            Volatile.Write(ref conHuecos, d);
        }
        private static void Pasa(List<string> rutas)
        {
            StringBuilder nuevas = new StringBuilder();
            int vistas = 0, con = 0;
            foreach (string r in rutas)
            {
                if (parar) break;
                long tam, fecha;
                try { FileInfo fi = new FileInfo(r); if (!fi.Exists) continue; tam = fi.Length; fecha = fi.LastWriteTimeUtc.Ticks; }
                catch { continue; }
                Entrada ya;
                if (porRuta.TryGetValue(r, out ya) && ya.Tam == tam && ya.Fecha == fecha && ya.Alg == Algoritmo) continue;
                RocolaOsHuecosMp3 m = null;
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                try { m = MideArchivo(r); } catch { }
                MsMedida = RocolaOs.Ms(t0);
                Entrada x = new Entrada { Tam = tam, Fecha = fecha, Alg = Algoritmo };
                if (m != null) { x.Cuadros = m.Cuadros - m.CuadrosRaros; x.Candidatas = m.Candidatas; x.Huecos = m.Huecos; x.DecimasDb = Decimas(m.RellenoDb); }
                porRuta[r] = x;
                Renglon(nuevas, r, x);
                vistas++; Interlocked.Increment(ref Mirados);
                if (Veredicto(x.Candidatas, x.DecimasDb / 10.0)) con++;
                if (nuevas.Length > 64 * 1024) { Escribe(nuevas); Publica(); }
                if (!SinPausa) Thread.Sleep(PausaMs);
            }
            Escribe(nuevas);
            if (vistas > 0) RocolaOs.Apunta(Mide, vistas.ToString(CultureInfo.InvariantCulture) + " MP3 medidos, " + con.ToString(CultureInfo.InvariantCulture) + " con huecos");
        }
        private static void Escribe(StringBuilder sb)
        {
            if (sb.Length == 0) return;
            try { File.AppendAllText(Ruta, sb.ToString(), new UTF8Encoding(false)); }
            catch (Exception ex) { RocolaOs.Falla(Mide, "escribir " + Archivo, ex); }
            sb.Clear();
        }

        // LA MEDIDA DE UN MP3: tres tramos (o el archivo entero si es chico), cuadro a cuadro desde la primera cabecera que se confirma con la
        // siguiente. Solo lee: el archivo se abre para leer y se comparte.
        internal static RocolaOsHuecosMp3 MideArchivo(string ruta)
        {
            RocolaOsHuecosMp3 m = new RocolaOsHuecosMp3(1);
            using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536))
            {
                long largo = fs.Length, inicio = 0;
                byte[] cab = new byte[10];
                if (fs.Read(cab, 0, 10) == 10 && cab[0] == 'I' && cab[1] == 'D' && cab[2] == '3')
                    inicio = 10 + ((cab[6] & 0x7F) << 21 | (cab[7] & 0x7F) << 14 | (cab[8] & 0x7F) << 7 | (cab[9] & 0x7F)) + ((cab[5] & 0x10) != 0 ? 10 : 0);
                long audio = largo - inicio;
                if (audio <= 0) return m;
                byte[] buf = new byte[(int)Math.Min(audio, (long)TramoBytes * Tramos)];
                if (audio <= (long)TramoBytes * Tramos) { fs.Position = inicio; int n = LeeTodo(fs, buf, buf.Length); Recorre(m, buf, n); return m; }
                for (int k = 0; k < Tramos; k++)
                {
                    long desde = inicio + (long)((audio - TramoBytes) * (0.2 + 0.3 * k));
                    fs.Position = desde;
                    int n = LeeTodo(fs, buf, TramoBytes);
                    m.Reinicia();
                    Recorre(m, buf, n);
                }
            }
            return m;
        }
        private static int LeeTodo(Stream s, byte[] b, int n)
        {
            int hecho = 0;
            while (hecho < n) { int r = s.Read(b, hecho, n - hecho); if (r <= 0) break; hecho += r; }
            return hecho;
        }
        // el largo de un cuadro de MPEG-1 capa III desde su cabecera (0 si no es uno)
        internal static int LargoCuadro(byte[] b, int i)
        {
            if (b[i] != 0xFF || (b[i + 1] & 0xFE) != 0xFA) return 0;   // sincronia, MPEG-1, capa III
            int kb = RocolaOsHuecosTablas.KbpsMpeg1[b[i + 2] >> 4], tasa = RocolaOsHuecosTablas.TasaMpeg1[(b[i + 2] >> 2) & 3];
            if (kb == 0 || tasa == 0) return 0;
            return 144000 * kb / tasa + ((b[i + 2] >> 1) & 1);
        }
        private static readonly byte[] cuadroVacio = new byte[0];
        private static void Recorre(RocolaOsHuecosMp3 m, byte[] b, int n)
        {
            int i = 0;
            byte[] cuadro = new byte[2048];
            // la primera cabecera que se confirma con la siguiente (misma tasa)
            while (i + 4 <= n)
            {
                int l = LargoCuadro(b, i);
                if (l > 0 && i + l + 4 <= n && LargoCuadro(b, i + l) > 0 && ((b[i + 2] ^ b[i + l + 2]) & 0x0C) == 0) break;
                i++;
            }
            while (i + 4 <= n)
            {
                int l = LargoCuadro(b, i);
                if (l <= 0 || i + l > n) break;
                Buffer.BlockCopy(b, i, cuadro, 0, l);
                m.Cuadro(cuadro, l, null, false);
                i += l;
            }
        }
    }

    // EL DESCOMPRESOR ENVUELTO (relleno_huecos, en una cancion que TRAE HUECOS): el de XP descomprime; el lector de los huecos lee el MISMO
    // cuadro y da su relleno ya sintetizado, que se suma. libmpg123 da cada cuadro al recibirlo, salvo el primero (y el primero tras un
    // salto), del que no da nada (lo comprueba la prueba: la sintesis propia de la cancion entera cae con desfase 0 sobre lo de libmpg123).
    // El relleno va por una cola para que, aunque el de XP diera a destiempo, cada muestra de relleno caiga en su muestra. Con «Escuchar sin mejoras» el lector sigue (para no perder el hilo) pero no se suma nada. Si el lector falla, se queda
    // callado el resto de la cancion: el sonido de XP nunca deja de salir.
    internal sealed class RocolaOsRellenoHuecos : IMp3FrameDecompressor
    {
        private readonly IMp3FrameDecompressor dentro;
        private readonly RocolaOsHuecosMp3 lector;
        private readonly bool flotante;
        private readonly int canales;
        private readonly float[] cuadro = new float[1152 * 2];
        private readonly float[] cola = new float[1152 * 2 * 8];
        private int colaIni, colaN;
        private bool roto, cerrado;
        internal bool Pasa;                 // (pruebas) no sumar nada, como «Escuchar sin mejoras»
        internal long Desfases;             // veces que la cola se paso de 4 cuadros (no deberia pasar)
        internal long Tirados;              // veces que se tiro lo que el de XP ya no iba a dar (el primer cuadro, al abrir y tras un salto)
        internal long Sumadas;              // muestras a las que se les sumo algo
        internal readonly int[] PrimerosN = new int[4]; internal int Llamadas;   // (pruebas) lo que dio el de XP en los primeros cuadros
        internal RocolaOsHuecosMp3 Lector { get { return lector; } }
        public WaveFormat OutputFormat { get { return dentro.OutputFormat; } }

        internal RocolaOsRellenoHuecos(IMp3FrameDecompressor d, uint semilla)
        {
            dentro = d;
            lector = new RocolaOsHuecosMp3(semilla);
            WaveFormat f = d.OutputFormat;
            flotante = f.Encoding == WaveFormatEncoding.IeeeFloat;
            canales = Math.Max(1, Math.Min(2, f.Channels));
            Interlocked.Increment(ref RocolaOsHuecos.Sonando);
        }

        public int DecompressFrame(Mp3Frame frame, byte[] dest, int destOffset)
        {
            int n = dentro.DecompressFrame(frame, dest, destOffset);
            if (Llamadas < PrimerosN.Length) PrimerosN[Llamadas] = n; Llamadas++;
            if (roto || frame == null) return n;
            try
            {
                long antes = lector.Rellenos;
                int m = lector.Cuadro(frame.RawData, frame.FrameLength, cuadro, true);
                if (m == 0) { m = frame.SampleCount; Array.Clear(cuadro, 0, Math.Min(cuadro.Length, m * canales)); }
                Mete(Math.Min(cuadro.Length, m * canales));
                Interlocked.Add(ref RocolaOsHuecos.RellenadosSonando, lector.Rellenos - antes);
                int muestras = n / (flotante ? 4 : 2);
                // EL PRIMER CUADRO (al abrir y tras cada salto) libmpg123 no lo da en la primera llamada: medido, con uno de LAME lo da en la
                // segunda junto con el siguiente (9216 bytes), y con uno de shine no lo da nunca (lo descomprime, su solape pasa al siguiente,
                // pero se lo calla). Las dos cosas se ven igual desde aqui: lo que da es lo MAS NUEVO, y lo que quede en la cola mas viejo que
                // eso ya no va a salir: se tira. Comprobado con el doble que anula (RocolaOs.HuecosPrueba.cs) en los dos: -103 dB.
                if (muestras > 0 && colaN > muestras) { int tira = colaN - muestras; colaIni = (colaIni + tira) % cola.Length; colaN = muestras; Tirados++; }
                Suma(dest, destOffset, muestras, Pasa || RocolaOsHuecos.SinMejoras);
            }
            catch (Exception ex) { roto = true; RocolaOs.Falla(RocolaOsHuecos.Rellena, "el relleno de los huecos (se calla en esta cancion)", ex); }
            return n;
        }
        private void Mete(int n)
        {
            if (colaN + n > cola.Length / 2)   // (mas de 4 cuadros esperando: el de XP no esta dando lo de cada cuadro; se tira lo viejo)
            {
                Desfases++;
                int tira = colaN + n - cola.Length / 2; colaIni = (colaIni + tira) % cola.Length; colaN -= tira;
            }
            int p = (colaIni + colaN) % cola.Length;
            for (int i = 0; i < n; i++) { cola[p] = cuadro[i]; if (++p == cola.Length) p = 0; }
            colaN += n;
        }
        private void Suma(byte[] dest, int off, int muestras, bool pasa)
        {
            int k = Math.Min(muestras, colaN);
            for (int i = 0; i < k; i++)
            {
                float v = cola[colaIni]; if (++colaIni == cola.Length) colaIni = 0;
                if (pasa || v == 0f) continue;
                Sumadas++;
                if (flotante)
                {
                    int o = off + 4 * i;
                    float s = BitConverter.ToSingle(dest, o) + v;
                    BitConverter.TryWriteBytes(new Span<byte>(dest, o, 4), s);
                }
                else
                {
                    int o = off + 2 * i;
                    int s = (short)(dest[o] | dest[o + 1] << 8);
                    int a = s + (int)Math.Round(v * 32768f);
                    if (a > 32767) a = 32767; else if (a < -32768) a = -32768;
                    dest[o] = (byte)a; dest[o + 1] = (byte)(a >> 8);
                }
            }
            colaN -= k;
        }
        public void Reset()
        {
            dentro.Reset();
            lector.Reinicia(); colaIni = 0; colaN = 0;
        }
        public void Dispose()
        {
            if (!cerrado) { cerrado = true; Interlocked.Decrement(ref RocolaOsHuecos.Sonando); }
            dentro.Dispose();
        }
    }

    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraHuecos()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsHuecos.Mide,
                Que = "medir los huecos de los MP3 (R1; solo mide: rocola_huecos.txt)",
                DeFabrica = true,
                Latido = f => f.RocolaOsHuecosLatido(),
                Vivo = (f, sb) => sb.Append("huecos_mide=").Append(RocolaOsHuecos.Listo ? "listo" : "repasando").Append(' ')
                    .Append(RocolaOsHuecos.Medidas.ToString(CultureInfo.InvariantCulture)).Append(" medidas, ")
                    .Append(RocolaOsHuecos.ConHuecosN.ToString(CultureInfo.InvariantCulture)).Append(" con huecos, ultima ")
                    .Append(RocolaOsHuecos.MsMedida.ToString("0", CultureInfo.InvariantCulture)).Append(" ms\n"),
                ParaAlCerrar = r => RocolaOsHuecos.Para(),
            });
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsHuecos.Rellena,
                Que = "rellenar los huecos de los MP3 viejos (R1; solo en las canciones medidas con huecos)",
                DeFabrica = false,   // (cambia el sonido: hay que oirlo en el bar antes)
                Latido = f => { RocolaOsHuecos.SinMejoras = f.sinMejoras; },
                Vivo = (f, sb) => sb.Append("relleno_huecos=encendida abiertas=").Append(Volatile.Read(ref RocolaOsHuecos.Sonando).ToString(CultureInfo.InvariantCulture))
                    .Append(" rellenadas=").Append(Interlocked.Read(ref RocolaOsHuecos.RellenadosSonando).ToString(CultureInfo.InvariantCulture))
                    .Append(RocolaOsHuecos.SinMejoras ? " (sin mejoras)" : "").Append('\n'),
            });
            RocolaOsOrdenes.Si("--prueba-huecos", RocolaOsHuecosPrueba.Corre);
        }

        // g260 y g261 (AbreMp3, desde el archivo y desde la memoria): el descompresor de la cancion que va a sonar. Con relleno_huecos y una
        // cancion medida con huecos, envuelto; si no, EL MISMO (bit a bit como XP).
        internal static IMp3FrameDecompressor RocolaOsRellenoHuecos(IMp3FrameDecompressor d, string ruta)
        {
            if (d == null || !RocolaOs.Activo || !RocolaOs.Encendida(RocolaOsHuecos.Rellena) || !RocolaOsHuecos.TraeHuecos(ruta)) return d;
            try { return new RocolaOsRellenoHuecos(d, (uint)StringComparer.Ordinal.GetHashCode(ruta) | 1u); }
            catch (Exception ex) { RocolaOs.Falla(RocolaOsHuecos.Rellena, "envolver el descompresor", ex); return d; }
        }

        // EL LATIDO de huecos_mide (como el de letras_mp3): la foto de las rutas de los MP3 al repaso cuando la biblioteca termina de armarse o
        // cambia, y una vez por hora
        private bool rocolaOsHuecosArmando;
        private int rocolaOsHuecosCuenta = -1;
        private long rocolaOsHuecosFotoMs;
        private void RocolaOsHuecosLatido()
        {
            if (libBuilding) { rocolaOsHuecosArmando = true; return; }
            long ahora = Environment.TickCount64;
            if (!rocolaOsHuecosArmando && searchIndex.Count == rocolaOsHuecosCuenta && ahora - rocolaOsHuecosFotoMs < 3600000) return;
            rocolaOsHuecosArmando = false; rocolaOsHuecosCuenta = searchIndex.Count; rocolaOsHuecosFotoMs = ahora;
            List<string> rutas = new List<string>();
            HashSet<string> vistas = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < searchIndex.Count; i++)
            {
                SearchItem si = searchIndex[i];
                if (si == null || si.Kind != 2 || si.TrackRef == null || si.TrackRef.Path == null) continue;
                string p = si.TrackRef.Path;
                if (p.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) && vistas.Add(p)) rutas.Add(p);
            }
            RocolaOsHuecos.Repasa(rutas);
        }
    }
}
