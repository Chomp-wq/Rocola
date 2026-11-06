// SPOTIFY EN LA ROCOLA (decision del dueno del 6-11-2026: «solo lo usare para reuniones familiares»). Interruptor «spotify=», APAGADO de
// fabrica (Ctrl+F3 -> Spotify). Encendido, la rocola abre librespot (el cliente libre de Spotify Connect; no oficial, necesita Premium):
// en el celular, en la misma red, Spotify -> dispositivos -> «Rocola». Lo que manda suena POR LA CADENA DE LA ROCOLA, como una cancion
// «en vivo» (FuenteSpotify): nivelado (al nivel de las no medidas), el local, el modo billar, el limitador... Apagado, librespot ni se
// abre y la rocola es la de siempre, bit a bit.
//
// Reglas:
//  - Spotify manda: si empieza a sonar con una cancion de la rocola, esa baja en 60 ms (como al saltar) y vuelve ENTERA al principio de
//    la cola (como cuando falla la tarjeta: AcaboPorFallo); lo que se pida mientras suena Spotify espera. En pausa mas de 8 s, la rocola
//    sigue con su cola.
//  - SALTAR con Spotify sonando: calla Spotify hasta su siguiente cancion (o hasta que se pause y se reanude) y sigue la cola.
//  - De Spotify NO SE APRENDE NADA: ni medidas, ni EQ, ni volumen parejo, ni estadisticas (no se paga), y no se guarda en el estado (tras un
//    apagon no se «reanuda»). librespot va sin cache de audio (--disable-audio-cache): solo guarda la llave de la cuenta en
//    /rocola/datos/spotify para volver a entrar. No se graba ni se guarda musica.
//  - El volumen: librespot normaliza cada cancion con los datos de Spotify (cerca de -14 LUFS) y la rocola la sube al nivel de las no
//    medidas («Videos y canciones aun sin medir», como los videos). El DSEE no se le pone (Spotify llega completo, hasta ~20 kHz).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    // El programa librespot y su sonido: un hilo lo lee de la tuberia (float de 32 bits, 44100 Hz, estereo) a un anillo de 1,5 s.
    internal sealed class SpotifyEnVivo : IDisposable
    {
        internal const int Hz = 44100, Canales = 2;
        internal static string Binario { get { return Env("ROCOLA_LIBRESPOT", "/usr/bin/librespot"); } }
        internal static string Evento { get { return Env("ROCOLA_SPOTIFY_EVENTO", "/usr/bin/rocola-spotify-evento"); } }
        internal static string CarpetaRun { get { return Path.Combine(Env("ROCOLA_RUN", "/run"), "rocola-spotify"); } }
        private static string Env(string n, string porOmision) { string v = null; try { v = Environment.GetEnvironmentVariable(n); } catch { } return string.IsNullOrEmpty(v) ? porOmision : v; }

        private Process proc;
        private Thread lee, errores;
        private readonly float[] anillo = new float[Hz * Canales * 3 / 2];
        private int inicio, cuenta;
        private readonly object cerrojo = new object();
        private volatile bool cerrando;
        private volatile bool callado;
        private int ultimoDato;   // TickCount del ultimo sonido que llego (0 = nunca)
        private string bitacora;

        internal bool Vivo { get { Process p = proc; try { return p != null && !p.HasExited; } catch { return false; } } }
        internal bool Callado { get { return callado; } }
        internal int Disponible { get { lock (cerrojo) return cuenta / Canales; } }   // cuadros en el anillo
        internal int MsSinDatos { get { int u = Volatile.Read(ref ultimoDato); return u == 0 ? int.MaxValue : unchecked(Environment.TickCount - u); } }

        internal void Arranca(string datos, string nombre)
        {
            string cache = Path.Combine(datos, "spotify");
            Directory.CreateDirectory(cache);
            try { Directory.CreateDirectory(CarpetaRun); } catch { }
            bitacora = Path.Combine(datos, "rocola_spotify.txt");
            ProcessStartInfo si = new ProcessStartInfo(Binario)
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = false,
            };
            foreach (string a in new[] {
                "--name", nombre, "--device-type", "speaker",
                "--backend", "pipe", "--format", "F32",            // el sonido, a la rocola (stdout), en float: sin dither ni redondeo
                "--bitrate", "320",
                "--cache", cache, "--disable-audio-cache",         // solo la llave de la cuenta; nada de musica en el disco
                "--enable-volume-normalisation", "--normalisation-method", "basic",   // solo la ganancia de Spotify: el limitador es el de la rocola
                "--initial-volume", "100",
                "--onevent", Evento })
                si.ArgumentList.Add(a);
            proc = Process.Start(si);
            Apunta("librespot abierto (" + nombre + ")");
            lee = new Thread(LeeBucle) { IsBackground = true, Name = "spotify (sonido)" };
            errores = new Thread(ErroresBucle) { IsBackground = true, Name = "spotify (avisos)" };
            lee.Start(); errores.Start();
        }

        // Lo que llega por la tuberia. Callado (tras SALTAR): se tira, al ritmo del reloj (si se tirara de golpe, librespot correria y
        // se acabaria la cancion en segundos). Si no, al anillo; lleno, se espera (librespot espera con el: no se pierde nada).
        private void LeeBucle()
        {
            try
            {
                Stream s = proc.StandardOutput.BaseStream;
                byte[] b = new byte[8192];
                int resto = 0;
                float[] f = new float[b.Length / 4];
                while (!cerrando)
                {
                    int n = s.Read(b, resto, b.Length - resto);
                    if (n <= 0) break;
                    n += resto;
                    int util = n - n % (4 * Canales);   // cuadros enteros
                    int muestras = util / 4;
                    MemoryMarshal.Cast<byte, float>(b.AsSpan(0, util)).CopyTo(f);
                    resto = n - util;
                    if (resto > 0) Buffer.BlockCopy(b, util, b, 0, resto);
                    int ahora = Environment.TickCount;
                    int antes = Volatile.Read(ref ultimoDato);
                    if (callado && antes != 0 && unchecked(ahora - antes) > 2000) callado = false;   // se pauso y se reanudo: vuelve a sonar
                    Volatile.Write(ref ultimoDato, ahora | 1);
                    if (muestras == 0) continue;
                    if (callado) { Thread.Sleep(Math.Max(1, muestras / Canales * 1000 / Hz)); continue; }
                    int hecho = 0;
                    lock (cerrojo)
                    {
                        while (hecho < muestras && !cerrando && !callado)
                        {
                            int libre = anillo.Length - cuenta;
                            if (libre == 0) { Monitor.Wait(cerrojo, 100); continue; }
                            int k = Math.Min(libre, muestras - hecho);
                            int fin = (inicio + cuenta) % anillo.Length;
                            int k1 = Math.Min(k, anillo.Length - fin);
                            Array.Copy(f, hecho, anillo, fin, k1);
                            if (k > k1) Array.Copy(f, hecho + k1, anillo, 0, k - k1);
                            cuenta += k; hecho += k;
                        }
                    }
                }
            }
            catch (Exception ex) { if (!cerrando) Apunta("se corto el sonido de librespot: " + ex.Message); }
        }

        // Lo que dice librespot (avisos y errores), a rocola_spotify.txt, recortado.
        private void ErroresBucle()
        {
            try
            {
                string l;
                while (!cerrando && (l = proc.StandardError.ReadLine()) != null) Apunta(l);
            }
            catch { }
        }
        private readonly object cerrojoBitacora = new object();
        internal void Apunta(string linea)
        {
            if (bitacora == null) return;
            lock (cerrojoBitacora)
            {
                try
                {
                    FileInfo fi = new FileInfo(bitacora);
                    if (fi.Exists && fi.Length > 256 * 1024) File.Copy(bitacora, bitacora + ".1", true);
                    if (fi.Exists && fi.Length > 256 * 1024) File.WriteAllText(bitacora, "");
                    File.AppendAllText(bitacora, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + linea + "\n");
                }
                catch { }
            }
        }

        // Para la salida de sonido (su hilo): lo que haya, sin esperar nunca.
        internal int Toma(Span<float> destino)
        {
            lock (cerrojo)
            {
                int k = Math.Min(destino.Length, cuenta);
                k -= k % Canales;
                int k1 = Math.Min(k, anillo.Length - inicio);
                anillo.AsSpan(inicio, k1).CopyTo(destino);
                if (k > k1) anillo.AsSpan(0, k - k1).CopyTo(destino.Slice(k1));
                inicio = (inicio + k) % anillo.Length; cuenta -= k;
                if (k > 0) Monitor.PulseAll(cerrojo);
                return k;
            }
        }

        // SALTAR: callado hasta la siguiente cancion de Spotify (Form1.LeePistaSpotify) o hasta que se pause y se reanude.
        internal void Calla() { lock (cerrojo) { callado = true; inicio = 0; cuenta = 0; Monitor.PulseAll(cerrojo); } }
        internal void Habla() { callado = false; }

        public void Dispose()
        {
            cerrando = true;
            lock (cerrojo) Monitor.PulseAll(cerrojo);
            try { if (proc != null && !proc.HasExited) proc.Kill(true); } catch { }
            try { if (proc != null) proc.WaitForExit(2000); } catch { }
            try { lee?.Join(1000); errores?.Join(1000); } catch { }
            try { proc?.Dispose(); } catch { }
            proc = null;
            Apunta("librespot cerrado");
        }
    }

    public partial class Form1
    {
        public bool SpotifyOn = false;   // «spotify=»: Spotify en la rocola (reuniones familiares). APAGADO de fabrica.
        internal const string RaizSpotify = "/spotify/";
        internal const float RmsSpotify = 0.2f;   // lo que deja la normalizacion de librespot (cerca de -14 LUFS), en la escala de normTarget
        private const int SpotifyFinSinDatosMs = 8000;   // en pausa mas de esto, la rocola sigue con su cola
        private SpotifyEnVivo spotify;
        private int spotifyReintentoDesde, spotifyFallos;
        private PlayItem spotifyItem;
        private FuenteSpotify spotifyFuente;
        private DateTime spotifyPistaFecha;
        private string spotifyPistaId;
        private int spotifyMirado;

        private static bool EsRutaSpotify(string ruta) { return ruta != null && ruta.StartsWith(RaizSpotify, StringComparison.Ordinal); }
        private bool EsItemSpotify(PlayItem it) { return it != null && object.ReferenceEquals(it, spotifyItem); }

        // Llamarlo a menudo (el bucle de la rocola): abre o cierra librespot segun el interruptor y pone el nombre de lo que suena.
        public void AtiendeSpotify()
        {
            int ahora = Environment.TickCount;
            if (unchecked(ahora - spotifyMirado) < 250 && spotifyMirado != 0) return;
            spotifyMirado = ahora | 1;
            bool quiere = SpotifyOn && File.Exists(SpotifyEnVivo.Binario);
            if (!quiere)
            {
                if (SonandoSpotify) return;   // primero baja lo que suena (el bucle de la rocola: SalidaAlsa.Apaga), luego se cierra
                if (spotify != null) { SpotifyEnVivo s = spotify; spotify = null; s.Calla(); s.Dispose(); }
                return;
            }
            if (spotify == null || !spotify.Vivo)
            {
                // Se cayo (o es la primera vez): se vuelve a abrir, cada vez esperando mas (hasta 5 min) si se sigue cayendo.
                int espera = spotify == null && spotifyFallos == 0 ? 0 : Math.Min(300000, 5000 << Math.Min(6, spotifyFallos));
                if (spotifyReintentoDesde != 0 && unchecked(ahora - spotifyReintentoDesde) < espera) return;
                if (spotify != null) { spotify.Apunta("librespot se cerro solo; se vuelve a abrir"); spotify.Dispose(); spotify = null; spotifyFallos++; }
                spotifyReintentoDesde = ahora | 1;
                try { SpotifyEnVivo s = new SpotifyEnVivo(); s.Arranca(CarpetaDatos, "Rocola"); spotify = s; }
                catch (Exception ex) { spotifyFallos++; LogError("Spotify: no se pudo abrir librespot", ex); }
                return;
            }
            LeePistaSpotify();
        }

        // Spotify esta mandando sonido (y no esta callado): la rocola le deja la salida.
        public bool SpotifyQuiereSonar { get { SpotifyEnVivo s = spotify; return SpotifyOn && s != null && !s.Callado && s.Disponible >= SpotifyEnVivo.Hz / 4; } }
        public bool SonandoSpotify { get { PlayItem p = nowPlaying; return p != null && EsItemSpotify(p); } }
        public void CallaSpotify() { SpotifyEnVivo s = spotify; if (s != null) s.Calla(); }
        // La cancion de la rocola que sonaba cede a Spotify: vuelve entera al principio de la cola (AcaboPorFallo).
        public void DejaPasoASpotify() { AcaboPorFallo(); }

        // EMPIEZA SPOTIFY por la cadena de siempre (como EmpiezaItem), sin aprender nada.
        public ISampleProvider EmpiezaSpotify()
        {
            SpotifyEnVivo s = spotify;
            if (s == null || cadenaSonando != null) return null;
            PlayItem it = new PlayItem();
            Track t = new Track { Path = RaizSpotify + "Spotify.spotify", Title = "Spotify", Artist = "", Album = "", Genre = "" };
            it.Song = t;
            it.Disc = new Album { Folder = RaizSpotify, Name = "", Artist = "Spotify" };
            spotifyItem = it; spotifyPistaFecha = DateTime.MinValue; spotifyPistaId = null;
            FuenteSpotify f = new FuenteSpotify(s, this);
            spotifyFuente = f;
            LeePistaSpotify();
            try
            {
                sonoAntes = nowPlaying; nowPlaying = it;
                CadenaCancion cad = ArmaCadena(t, f, false, 0.0);
                // NADA SE APRENDE DE SPOTIFY: ni la sonoridad, ni la huella del EQ, ni el volumen parejo del billar
                cad.MedidaRuta = null; cad.EqRuta = null; cad.Margen = null;
                if (cad.Tap != null) cad.Tap.Medida = new MedidaCancion(null, null);
                if (cad.Sbr != null) cad.Sbr.Active = false;
                f.Armada = true;   // desde aqui dice lo que dura la cancion (para la barra); antes no, o el fundido de salida caeria ahi
                SellaInicioReloj(cad);
                cad.Item = it;
                lock (audioDatosLock)
                {
                    curMeasurePath = null; curSumSq = 0; curSamples = 0;
                    curFuenteMedida = cad.Fuente;
                    eqMeasPath = null;
                    eqMeasCut = 0f;
                    eqMeasSum = new double[8]; eqMeasN = 0; eqMeasRate = 0;
                    kwRate = 0;
                    tapMedido = cad.Tap;
                }
                curSbr = cad.Sbr; curTap = cad.Tap; curBass = cad.Bajo;
                bassSongFactor = cad.BajoFactor; bassAutoBase = cad.BajoBase;
                cadenaSonando = cad; cadenaPreparada = null;
                secuencia = new Encadenado(cad.Salida, cad);
                sinEncadenar = null;
                pulso = new MedidorPulso(secuencia);
                return pulso;
            }
            catch (Exception ex)
            {
                LogError("Spotify: no se pudo armar la cadena", ex);
                if (EsItemSpotify(nowPlaying)) nowPlaying = null;
                spotifyItem = null; spotifyFuente = null;
                return null;
            }
        }

        // La cancion que dice librespot (rocola-spotify-evento: /run/rocola-spotify/pista.txt): su nombre y su portada, a lo que suena.
        private void LeePistaSpotify()
        {
            PlayItem it = spotifyItem;
            string dir = SpotifyEnVivo.CarpetaRun, ruta = Path.Combine(dir, "pista.txt");
            try
            {
                DateTime f = File.Exists(ruta) ? File.GetLastWriteTimeUtc(ruta) : DateTime.MinValue;
                if (f != DateTime.MinValue && f != spotifyPistaFecha)
                {
                    spotifyPistaFecha = f;
                    Dictionary<string, string> v = new Dictionary<string, string>();
                    foreach (string l in File.ReadAllLines(ruta))
                    {
                        int i = l.IndexOf('='); if (i > 0) v[l.Substring(0, i)] = l.Substring(i + 1).Trim();
                    }
                    string id, nombre, artistas, disco, dura;
                    v.TryGetValue("id", out id); v.TryGetValue("nombre", out nombre); v.TryGetValue("artistas", out artistas);
                    v.TryGetValue("disco", out disco); v.TryGetValue("dura_ms", out dura);
                    bool nueva = id != spotifyPistaId;
                    spotifyPistaId = id;
                    if (nueva) spotifyFallos = 0;   // librespot funciona: si se vuelve a caer, se reabre sin esperar de mas
                    if (nueva && spotify != null && spotify.Callado) spotify.Habla();   // SALTAR callaba hasta la siguiente: ya llego
                    if (it != null && it.Song != null)
                    {
                        it.Song.Title = string.IsNullOrEmpty(nombre) ? "Spotify" : nombre;
                        it.Song.Artist = artistas ?? "";
                        it.Song.Album = disco ?? "";
                        string titulo = string.IsNullOrEmpty(artistas) ? it.Song.Title : artistas + " - " + it.Song.Title;
                        it.Song.Path = RaizSpotify + Limpia(titulo) + ".spotify";   // (lo que se ve: el nombre del «archivo», como en la biblioteca)
                        if (it.Disc != null) { it.Disc.Artist = string.IsNullOrEmpty(artistas) ? "Spotify" : artistas; it.Disc.Name = disco ?? ""; it.Disc.CoverPath = null; }
                    }
                    long ms; FuenteSpotify fu = spotifyFuente;
                    if (nueva && fu != null) fu.NuevaPista(long.TryParse(dura, out ms) ? ms : 0);
                }
                if (it != null && it.Disc != null && it.Disc.CoverPath == null && !string.IsNullOrEmpty(spotifyPistaId))
                {
                    string p = Path.Combine(dir, "portada-" + spotifyPistaId + ".jpg");
                    if (File.Exists(p)) it.Disc.CoverPath = p;
                }
            }
            catch { }
        }
        private static string Limpia(string s)
        {
            StringBuilder b = new StringBuilder(s.Length);
            foreach (char c in s) b.Append(c == '/' || c == '\\' || char.IsControl(c) ? '-' : c);
            return b.Length > 0 ? b.ToString() : "Spotify";
        }

        // El nivel de Spotify: el de las no medidas (como los videos), con el margen del tono del local.
        private float GananciaSpotify(Track t)
        {
            float g = 1f;
            if (normalizeOn) g = Math.Max(0.5f, Math.Min(4f, normTarget / RmsSpotify));
            float m = MargenDelTono(t);
            return m == 1f ? g : g * m;
        }

        // Lo que dice Ctrl+F3 -> Spotify.
        public string TextoSpotify()
        {
            StringBuilder sb = new StringBuilder();
            if (!SpotifyOn)
            {
                sb.Append("Apagado. Es para las reuniones familiares: Spotify no permite ponerlo para el publico de un negocio.\n");
                sb.Append("Encendido, la rocola aparece como «Rocola» en tu app de Spotify (hace falta Premium) y lo que pongas suena por su cadena.");
                return sb.ToString();
            }
            if (!File.Exists(SpotifyEnVivo.Binario)) { sb.Append("Este sistema aun no trae Spotify: llega con la siguiente actualizacion del sistema."); return sb.ToString(); }
            SpotifyEnVivo s = spotify;
            if (s == null || !s.Vivo) sb.Append("Abriendo Spotify...\n");
            else if (SonandoSpotify && nowPlaying.Song != null)
                sb.Append("Suena: ").Append(nowPlaying.Song.Title).Append(string.IsNullOrEmpty(nowPlaying.Song.Artist) ? "" : " - " + nowPlaying.Song.Artist).Append('\n');
            else if (s.Callado) sb.Append("Callado (SALTAR): vuelve con la siguiente cancion de Spotify.\n");
            else sb.Append("Listo. En el celular, en la misma red que la rocola: Spotify -> dispositivos -> «Rocola» (Premium).\n");
            sb.Append("Spotify manda: lo que suena de la rocola vuelve entero a la cola. SALTAR lo calla hasta su siguiente cancion.\n");
            sb.Append("De Spotify no se aprende ni se guarda nada (solo la cuenta, para volver a entrar). Programa no oficial: librespot.");
            return sb.ToString();
        }

        // LA CANCION «EN VIVO»: lo que haya llegado de Spotify, sin esperar nunca (la salida no puede esperar); si no hay, silencio. Se acaba
        // (0) si se cerro librespot, si SALTAR la callo o tras SpotifyFinSinDatosMs sin sonido (pausa larga). Al apagar el interruptor o al
        // saltar, el bucle de la rocola la baja antes en 60 ms (SalidaAlsa.Apaga), sin golpe.
        private sealed class FuenteSpotify : IAudioSource
        {
            private readonly SpotifyEnVivo s;
            private readonly Form1 f;
            private readonly WaveFormat formato = WaveFormat.CreateIeeeFloatWaveFormat(SpotifyEnVivo.Hz, SpotifyEnVivo.Canales);
            private volatile float volumen = 1f;
            private long cuadros, cuadroPista, duraMs;
            private int sinDatosDesde;
            private bool acabada;
            internal volatile bool Armada;
            internal FuenteSpotify(SpotifyEnVivo s, Form1 f) { this.s = s; this.f = f; }
            internal void NuevaPista(long ms) { Interlocked.Exchange(ref cuadroPista, Interlocked.Read(ref cuadros)); Interlocked.Exchange(ref duraMs, ms); }
            public WaveFormat WaveFormat { get { return formato; } }
            public int Read(Span<float> d)
            {
                if (acabada) return 0;
                if (s.Callado || !object.ReferenceEquals(f.spotify, s)) { acabada = true; return 0; }
                int n = s.Toma(d);
                if (n < d.Length)
                {
                    d.Slice(n).Clear();   // lo que no llego, en silencio (una pausa, o la red que tarda)
                    if (n == 0)
                    {
                        int ahora = Environment.TickCount;
                        if (sinDatosDesde == 0) sinDatosDesde = ahora | 1;
                        else if (unchecked(ahora - sinDatosDesde) > SpotifyFinSinDatosMs) { acabada = true; return 0; }
                    }
                    else sinDatosDesde = 0;
                }
                else sinDatosDesde = 0;
                float v = volumen;
                if (v != 1f) for (int i = 0; i < d.Length; i++) d[i] *= v;
                Interlocked.Add(ref cuadros, d.Length / SpotifyEnVivo.Canales);
                return d.Length;
            }
            public int Read(float[] buffer, int offset, int count) { return Read(buffer.AsSpan(offset, count)); }
            public float Volume { set { volumen = value; } }
            public TimeSpan CurrentTime { get { return TimeSpan.FromSeconds((Interlocked.Read(ref cuadros) - Interlocked.Read(ref cuadroPista)) / (double)SpotifyEnVivo.Hz); } }
            public TimeSpan TotalTime { get { return Armada ? TimeSpan.FromMilliseconds(Interlocked.Read(ref duraMs)) : TimeSpan.Zero; } }
            public void Salta(TimeSpan donde) { }
            public void Dispose() { }
        }
    }
}
