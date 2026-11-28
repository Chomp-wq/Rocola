// ROCOLA OS (cadena de audio nueva, 27-11-2026; CADENA_NUEVA.md §4 y decisiones D10-D13; CADENA_NUEVA_salida.md): EL MOTOR DE LA SALIDA NUEVA.
// Un hilo propio (en la prioridad mas alta de la rocola, como la salida de XP) que tiene la tarjeta SIEMPRE ABIERTA y le da un periodo cada
// vez que tiene sitio: la suma de las fuentes enchufadas (RocolaOs.SalidaNuevaCliente.cs: la cancion que suena y su relevo, la de Spotify,
// el sonido del video, el tono de la medida) o ceros si no hay nada. Asi entre canciones, al saltar, con Spotify, con videos y con la rocola
// callada la tarjeta sigue recibiendo su flujo y el convertidor S/PDIF no pierde el enganche (el CS8416 tarda 4-11 ms en engancharse y el
// CS4344 unos 45 ms; sin ceros delante, el arranque da un golpe). Al abrir, 0,5 s de ceros antes de la musica.
//
// LA CUENTA DE CADA PERIODO, en coma flotante y en este orden: la suma de las fuentes (cada una por su ganancia, con una rampa de 20 ms si
// cambia) -> EL VOLUMEN DEL DUENO (los botones de atras: en dB, 1 dB por toque, con una rampa de 10 ms; el silencio, igual) -> LA PROTECCION
// en 0 dBFS (un recorte duro que no deberia actuar nunca: si actua, se cuenta -muestras y veces- y se apunta) -> LA UNICA CUANTIZACION, a 24
// bits con tramado TPDF de un escalon (S32_LE con los 8 de abajo en 0, o S24_3LE; si la tarjeta solo da 16, a 16 con su TPDF, como XP).
// 44 100 Hz FIJOS: lo que viene a otra frecuencia ya lo convirtio la fuente con el sinc de XP.
//
// LA TARJETA (RocolaOs.SalidaNuevaTarjetas.cs): por su NOMBRE de ALSA (hw:CARD=id,DEV=n: sin «plug»), la guardada en rocola_tarjeta.txt o,
// en automatico, la USB si hay una (el SMSL PO100 del dueno) y si no la primera que no sea HDMI. Su control de volumen, si tiene, se pone UNA
// vez al 100 % (0 dB) y no se toca mas. Si la guardada no esta, se avisa (bitacora y hoja Sonido) y se usa la mejor que haya, y cada 2 s se
// mira si volvio. Si al arrancar no abre ninguna por hw: (no acepta 44,1 kHz ni ninguno de los tres formatos), se rinde: esta sesion la
// rocola suena por el camino de XP (plughw y 16 bits) y lo apunta. Si se cae a media sesion (se desenchufo), las fuentes lanzan ENODEV (lo
// que XP trata como «fallo el aparato»: espera y retoma la misma cancion) y el motor la vuelve a buscar cada segundo.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    // LO QUE SE LE PUEDE ENCHUFAR AL MOTOR EN EXCLUSIVA (el tono de la medida del convertidor): mientras esta, la musica sigue su curso en
    // silencio y no hay volumen del dueno ni proteccion
    internal interface ISalidaNuevaGenerador { int Genera(float[] mezcla, int cuadros); }

    internal static partial class SalidaNueva
    {
        internal const string NombreFuncion = "salida_nueva";
        internal const int Tasa = 44100, Canales = 2;
        internal const int VolumenMinDb = -60, VolumenMaxDb = 0;

        // ---------- el estado (desde cualquier hilo) ----------
        internal static volatile bool Arrancada;   // se pidio la salida nueva en esta sesion (salida_nueva encendida)
        internal static volatile bool Abierta;     // hay tarjeta abierta ahora
        internal static volatile bool Caida;       // la tarjeta se fue: las fuentes lanzan ENODEV
        internal static volatile bool Rendida;     // no abrio por hw: al arrancar: esta sesion, el camino de XP
        private static volatile bool parar;
        private static Thread hilo;
        private static readonly ManualResetEventSlim primeraVez = new ManualResetEventSlim(false);

        // ---------- las fuentes ----------
        private static readonly object cerrojoFuentes = new object();
        private static volatile SalidaNuevaCliente[] fuentes = new SalidaNuevaCliente[0];
        internal static volatile ISalidaNuevaGenerador Exclusiva;

        // ---------- el volumen del dueno ----------
        private static volatile int volumenDb;
        private static volatile bool mudo;
        private static float volActual = 1f;

        // ---------- lo que se ve (la hoja, la bitacora, vivo.txt) ----------
        internal static string PcmNombre = "", TarjetaId = "", TarjetaNombre = "", ControlHw = "", Aviso = "", Motivo = "";
        internal static int TarjetaNumero = -1, Dispositivo = -1, FormatoAbierto, TasaAbierta, PeriodoAbierto, BuferAbierto;
        internal static bool TarjetaUsb;
        internal static long Aperturas, Xruns, Caidas;
        internal static long ProteccionMuestras, ProteccionVeces;
        private static long proteccionUltima = long.MinValue / 2, proteccionApuntada = long.MinValue / 2, proteccionVecesApuntadas;
        internal static double PicoMax;            // el mayor valor absoluto antes de la proteccion (desde que abrio)
        internal static long EscritosTarjeta { get { lock (cerrojoSonado) return escritosTarjeta; } }

        // ---------- lo sonado en la tarjeta ----------
        private static readonly object cerrojoSonado = new object();
        private static long escritosTarjeta, sonadoBase, sonadoTs;
        private static bool sonando;
        // Lo que ya sono en la tarjeta (cuadros desde que se abrio): lo escrito menos lo que tenia por delante al escribir, mas lo que paso desde
        // entonces (sin pasar de lo escrito). Las fuentes lo usan para contestar Sonado sin llamar a ALSA desde otro hilo.
        internal static long SonadoTarjeta()
        {
            lock (cerrojoSonado)
            {
                if (!sonando) return sonadoBase;
                long est = sonadoBase + (Stopwatch.GetTimestamp() - sonadoTs) * Tasa / Stopwatch.Frequency;
                return Math.Min(est, escritosTarjeta);
            }
        }
        // Lo que tarda en oirse lo que se le da ahora a la tarjeta (su bufer), en ms
        internal static int RetrasoTarjetaMs { get { return BuferAbierto > 0 ? BuferAbierto * 1000 / Tasa : BuferMs; } }

        // ---------- los ajustes del motor (los pone Arranca) ----------
        internal static int BuferMs = 200, PeriodoMs = 20;
        internal static string PcmFijo;            // (pruebas: ROCOLA_SALIDA_NUEVA_PCM) este dispositivo, tal cual
        internal static int TarjetaFija = -1;      // (pruebas) su numero para amixer
        internal static float GananciaPrueba = 1f; // (pruebas: ROCOLA_SALIDA_NUEVA_GANANCIA_DB) para hacer actuar la proteccion

        // ================================================================== ARRANCAR Y PARAR
        // EL MOTOR ARRANCA (en su hilo) y se espera, como mucho «esperaMs», a la primera vez que intenta abrir: true si abrio. Lo llama el
        // arranque de la funcion (antes de ensenarse la ventana: la espera es antes de que se vea nada) y la orden de consola.
        internal static bool Arranca(int esperaMs)
        {
            if (hilo != null) return Abierta;
            Arrancada = true; parar = false;
            hilo = RocolaOs.Hilo("salida-nueva", ThreadPriority.Highest, Bucle);
            primeraVez.Wait(Math.Max(0, esperaMs));
            return Abierta;
        }
        internal static void Para()
        {
            parar = true;
            Thread h = hilo;
            if (h != null && h != Thread.CurrentThread) { try { h.Join(1500); } catch { } }
        }

        // ================================================================== LAS FUENTES
        // UNA FUENTE NUEVA (lo que en XP era abrir la tarjeta): null si la salida nueva no va (sin arrancar o rendida: el camino de XP). Si la
        // tarjeta esta caida, ErrorAlsa con ENODEV (XP espera y lo vuelve a intentar).
        internal static SalidaNuevaCliente Abre(string nombre, WaveFormat formato, int cuadrosBloque, int bloques)
        {
            if (!Arrancada || Rendida) return null;
            if (!Abierta || Caida) throw new ErrorAlsa(-ErrorAlsa.ENODEV, "salida_nueva (" + (Motivo.Length > 0 ? Motivo : "la tarjeta aun no abre") + ")");
            SalidaNuevaCliente c = new SalidaNuevaCliente(nombre, formato, cuadrosBloque, bloques);
            Enchufa(c);
            return c;
        }
        internal static void Enchufa(SalidaNuevaCliente c)
        {
            lock (cerrojoFuentes)
            {
                SalidaNuevaCliente[] n = new SalidaNuevaCliente[fuentes.Length + 1];
                Array.Copy(fuentes, n, fuentes.Length); n[fuentes.Length] = c;
                fuentes = n;
            }
        }
        private static void QuitaCerradas()
        {
            lock (cerrojoFuentes)
            {
                List<SalidaNuevaCliente> l = null;
                foreach (SalidaNuevaCliente c in fuentes) if (!(c.Cerrada && c.Vacio)) (l ?? (l = new List<SalidaNuevaCliente>())).Add(c);
                if (l == null) { if (fuentes.Length > 0) fuentes = new SalidaNuevaCliente[0]; return; }
                if (l.Count != fuentes.Length) fuentes = l.ToArray();
            }
        }
        internal static int FuentesVivas { get { int n = 0; foreach (SalidaNuevaCliente c in fuentes) if (!c.Cerrada) n++; return n; } }
        internal static SalidaNuevaCliente[] Fuentes { get { return fuentes; } }

        // ================================================================== EL VOLUMEN DEL DUENO
        internal static int VolumenDb { get { return volumenDb; } }
        internal static bool Mudo { get { return mudo; } }
        internal static void PonVolumen(int db, bool silencio) { volumenDb = Math.Max(VolumenMinDb, Math.Min(VolumenMaxDb, db)); mudo = silencio; }

        // ================================================================== EL HILO
        private static SalidaNuevaPcm pcm;
        private static int cerosQuedan;
        private static float[] mezcla = new float[0];
        private static byte[] bytes = new byte[0];
        private static uint azar = 0x9E3779B9u;

        private static void Bucle()
        {
            HilosRocola.Nombra("audio (salida nueva)");
            int intentos = 0;
            long ultimaMirada = 0;
            int sinSitio = 0;
            while (!parar)
            {
                try
                {
                    if (SoltadaParaVideo)
                    {   // (sin videos por la rocola: mpv tiene la tarjeta mientras dura el video)
                        if (pcm != null) { Cierra("se suelta para un video", false); RocolaOs.Apunta(NombreFuncion, "tarjeta suelta para el video (mpv la usa directo)"); }
                        Thread.Sleep(20);
                        continue;
                    }
                    if (pcm == null)
                    {
                        bool primera = !primeraVez.IsSet;
                        if (AbreLaTarjeta(primera)) { intentos = 0; sinSitio = 0; primeraVez.Set(); continue; }
                        if (primera && Caida) primeraVez.Set();   // (la tarjeta aun no esta, o esta ocupada: no es rendirse; se espera y se reintenta)
                        else if (primera)
                        {
                            // al arrancar la tarjeta esta pero no abre por hw: (ni 44,1 kHz ni ninguno de los tres formatos): esta sesion, lo de XP
                            // (plughw y 16 bits)
                            Rendida = true;
                            RocolaOs.Apunta(NombreFuncion, "NO abrio la salida nueva (" + Motivo + "): esta sesion suena por el camino de siempre (plughw y 16 bits)");
                            primeraVez.Set();
                            return;
                        }
                        intentos++;
                        DrenaSinTarjeta(intentos < 10 ? 1000 : 3000);
                        continue;
                    }
                    // cada 2 s: si se esta usando otra porque la guardada no estaba, ¿ya volvio?
                    long ahora = Environment.TickCount64;
                    if (ahora - ultimaMirada >= 2000)
                    {
                        ultimaMirada = ahora;
                        QuitaCerradas();
                        if (Aviso.Length > 0 && PcmFijo == null && VolvioLaDeseada()) { RocolaOs.Apunta(NombreFuncion, "volvio la tarjeta guardada: se cambia a ella"); Cierra("volvio la guardada", false); continue; }
                    }
                    long libres = pcm.Libres();
                    if (libres < 0) { Pierde((int)libres, "snd_pcm_avail"); continue; }
                    if (libres < PeriodoAbierto)
                    {
                        int w = pcm.Espera(100);
                        if (w < 0) { Pierde(w, "snd_pcm_wait"); continue; }
                        if (w == 0 && pcm.Corriendo) { if (++sinSitio >= 30) { Pierde(-SalidaNuevaPcm.EIO, "la tarjeta dejo de pedir sonido (3 s)"); sinSitio = 0; } }
                        else sinSitio = 0;
                        continue;
                    }
                    sinSitio = 0;
                    int n = PeriodoAbierto;
                    long tarjeta;
                    lock (cerrojoSonado) tarjeta = escritosTarjeta;
                    Mezcla(n, tarjeta);
                    long r;
                    unsafe { fixed (byte* p = bytes) r = pcm.Escribe(p, n); }
                    if (r < 0) { Pierde((int)r, "snd_pcm_writei"); continue; }
                    long d = pcm.Retraso();
                    lock (cerrojoSonado)
                    {
                        escritosTarjeta += r;
                        sonadoBase = d >= 0 ? Math.Max(sonadoBase, escritosTarjeta - d) : sonadoBase;
                        sonadoTs = Stopwatch.GetTimestamp();
                        sonando = pcm.Corriendo;
                    }
                    Xruns = pcm.Xruns + xrunsAntes;
                }
                catch (Exception ex)
                {
                    RocolaOs.Falla(NombreFuncion, "el hilo de la salida", ex);
                    try { Cierra("fallo del hilo: " + ex.Message, true); } catch { }
                    Thread.Sleep(200);
                }
            }
            try { Cierra("se cierra la rocola", false); } catch { }
        }
        private static long xrunsAntes;

        // LA MEZCLA DE UN PERIODO: las fuentes, el volumen, la proteccion y la cuantizacion (en «bytes»)
        private static void Mezcla(int n, long tarjeta)
        {
            if (mezcla.Length < n * 2) mezcla = new float[n * 2];
            int bpm = pcm.BytesPorMuestra;
            if (bytes.Length < n * 2 * bpm) bytes = new byte[n * 2 * bpm];
            Array.Clear(mezcla, 0, n * 2);
            ISalidaNuevaGenerador ex = Exclusiva;
            bool tal = ex != null;
            EscritosTarjetaSinCerrojo = tarjeta;
            if (cerosQuedan > 0) cerosQuedan -= n;   // (al abrir: 0,5 s de ceros; las fuentes esperan)
            else
            {
                SalidaNuevaCliente[] fs = fuentes;
                for (int i = 0; i < fs.Length; i++) fs[i].Toma(mezcla, n, tarjeta, tal);
                if (tal) { try { ex.Genera(mezcla, n); } catch (Exception e) { RocolaOs.Falla(NombreFuncion, "la prueba de sonido (o el tono de la medida)", e); SueltaPrueba(ex); } }
            }
            // el volumen del dueno (rampa de 10 ms) y la prueba
            float objetivo = mudo ? 0f : (float)Math.Pow(10.0, volumenDb / 20.0);
            if (GananciaPrueba != 1f) objetivo *= GananciaPrueba;
            if (tal && !ExclusivaConVolumen) objetivo = 1f;   // (la medida: tal cual; una prueba de sonido puede pedir el volumen del dueno)
            float v0 = volActual;
            int rampa = v0 == objetivo ? 0 : Math.Min(n, Tasa / 100);
            int recortes = 0; double pico = PicoMax;
            for (int c = 0; c < n; c++)
            {
                float g = c < rampa ? v0 + (objetivo - v0) * (c + 1) / rampa : objetivo;
                for (int k = 0; k < 2; k++)
                {
                    float x = mezcla[c * 2 + k] * g;
                    float a = Math.Abs(x);
                    if (a > pico) pico = a;
                    if (a > 1f) { x = x > 0 ? 1f : -1f; recortes++; }   // LA PROTECCION (no deberia actuar nunca)
                    mezcla[c * 2 + k] = x;
                }
            }
            volActual = objetivo;
            PicoMax = pico;
            if (recortes > 0 && !tal) Protegio(recortes);
            Cuantiza(n);
        }
        // la proteccion actuo: se cuenta (una «vez» nueva si la anterior fue hace mas de 1 s) y se apunta (como mucho cada 10 s)
        private static void Protegio(int muestras)
        {
            long ahora = Environment.TickCount64;
            ProteccionMuestras += muestras;
            if (ahora - proteccionUltima > 1000) ProteccionVeces++;
            proteccionUltima = ahora;
            if (ahora - proteccionApuntada >= 10000)
            {
                proteccionApuntada = ahora;
                RocolaOs.Apunta(NombreFuncion, "ACTUO LA PROTECCION de 0 dBFS (no deberia): " + ProteccionVeces + " veces, " + ProteccionMuestras + " muestras recortadas en total; pico "
                    + (20 * Math.Log10(Math.Max(1e-9, PicoMax))).ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + " dBFS; volumen del dueno " + volumenDb + " dB");
                proteccionVecesApuntadas = ProteccionVeces;
            }
        }
        // LA UNICA CUANTIZACION: a 24 bits (o 16) con tramado TPDF de un escalon (el silencio exacto, entre canciones y al abrir, sale en 0 de verdad)
        private static void Cuantiza(int n)
        {
            int f = pcm.Formato, total = n * 2;
            uint s = azar;
            if (f == SalidaNuevaPcm.S16_LE)
            {
                for (int i = 0; i < total; i++)
                {
                    if (mezcla[i] == 0f) { bytes[i * 2] = 0; bytes[i * 2 + 1] = 0; continue; }   // (el silencio exacto, sin tramar: ceros de verdad)
                    double t = Tpdf(ref s);
                    int v = (int)Math.Round(mezcla[i] * 32767.0 + t);
                    if (v > 32767) v = 32767; else if (v < -32768) v = -32768;
                    bytes[i * 2] = (byte)v; bytes[i * 2 + 1] = (byte)(v >> 8);
                }
            }
            else
            {
                bool tres = f == SalidaNuevaPcm.S24_3LE;
                for (int i = 0; i < total; i++)
                {
                    double t = mezcla[i] == 0f ? 0.0 : Tpdf(ref s);   // (el silencio exacto, sin tramar: ceros de verdad)
                    int v = (int)Math.Round(mezcla[i] * 8388607.0 + t);
                    if (v > 8388607) v = 8388607; else if (v < -8388608) v = -8388608;
                    if (tres) { int o = i * 3; bytes[o] = (byte)v; bytes[o + 1] = (byte)(v >> 8); bytes[o + 2] = (byte)(v >> 16); }
                    else { int o = i * 4, w = v << 8; bytes[o] = (byte)w; bytes[o + 1] = (byte)(w >> 8); bytes[o + 2] = (byte)(w >> 16); bytes[o + 3] = (byte)(w >> 24); }
                }
            }
            azar = s;
        }
        // tramado triangular de un escalon (dos uniformes de xorshift: entre -1 y 1, la suma en el centro)
        private static double Tpdf(ref uint s)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5; uint a = s;
            s ^= s << 13; s ^= s >> 17; s ^= s << 5; uint b = s;
            return (a * (1.0 / 4294967296.0)) - (b * (1.0 / 4294967296.0));
        }

        // SIN TARJETA (se cayo y aun no vuelve): las fuentes lanzan ENODEV y XP espera; el sonido del video y el tono se tiran a su ritmo
        // (para que mpv no se pare esperando)
        private static void DrenaSinTarjeta(int ms)
        {
            long hasta = Environment.TickCount64 + ms;
            float[] tira = new float[Tasa / 50 * 2];
            while (!parar && Environment.TickCount64 < hasta)
            {
                Thread.Sleep(20);
                SalidaNuevaCliente[] fs = fuentes;
                for (int i = 0; i < fs.Length; i++) fs[i].Toma(tira, Tasa / 50, 0, true);
                QuitaCerradas();
            }
        }

        // LA TARJETA SE FUE (o dejo de pedir): se cierra, las fuentes lanzan ENODEV hasta que vuelva y se vuelve a buscar
        private static void Pierde(int error, string donde)
        {
            Caidas++;
            RocolaOs.Apunta(NombreFuncion, "se perdio la tarjeta " + PcmNombre + " (" + SalidaNuevaPcm.Texto(error) + " en " + donde + "): se vuelve a buscar cada segundo");
            Cierra(donde + ": " + SalidaNuevaPcm.Texto(error), true);
        }
        private static void Cierra(string porque, bool caida)
        {
            SalidaNuevaPcm p = pcm; pcm = null;
            if (p != null) { xrunsAntes += p.Xruns; try { p.Dispose(); } catch { } }
            Abierta = false;
            if (caida) { Caida = true; Motivo = porque; }
            lock (cerrojoSonado) { sonando = false; }
        }
    }
}
