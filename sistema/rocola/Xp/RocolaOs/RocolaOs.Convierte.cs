// LOS «MP3» QUE NO LO SON, CONVERTIDOS (Rocola OS; no es de XP). Funcion «convierte» (encendida de fabrica; «convierte=0» en rocola_os.txt
// la apaga). Decision del dueno del 4-11-2026 (AGENTS.md: «que se convierta automaticamente para la proxima, o si no tarda mucho que lo
// convierta en ese mismo instante y reproduzca la cancion»), del programa anterior (Motor/Conversiones.cs, la referencia de la logica),
// puesta encima de lo que la rocola de XP hace desde el 17-11-2026 con esos archivos (Codigo/NoEsMp3.cs: los rechaza ANTES de abrir nada,
// el credito se devuelve, la cancion queda danada y se anota en rocola_incompatibles.txt).
//
// QUE HACE. Un M4A/AAC, OGG, FLAC, WMA o WAV con el nombre cambiado a .mp3 (lo que NoEsMp3.OtroFormato reconoce por la cabecera) se pasa
// con ffmpeg (el del sistema, el mismo de los videos: Nativo/MedidorVideo.cs) a un WAV de 16 bits con su frecuencia y sus canales. EL
// ORIGINAL NO SE TOCA: ffmpeg solo lo lee, y la copia va aparte, en ROCOLA_CONVERTIDAS (la pone el lanzador, /usr/bin/rocola: el disco de
// la rocola, /rocola/disco/rocola-os/convertidas; sin ella, «convertidas» en la carpeta de datos, donde casi nunca hay 1 GB libre y entonces
// queda lo de XP), con un nombre sacado de la ruta, el tamano y la fecha de escritura del original (si el archivo cambia, se convierte otra
// vez; la copia vieja se queda: ver PORT_xp-1718.md, lo pendiente).
//
// LA CONVERTIDA SUENA COMO LA CANCION DE LA ROCOLA. Para la cola, el estado, lo mas sonado, las estadisticas, lo aprendido (nivelado, EQ,
// volumen parejo, silencios) y lo que se ve, la cancion sigue siendo la ruta del .mp3 original: solo cambia QUE ARCHIVO SE LEE, en los dos
// sitios donde XP abre el sonido:
//   - g110 (OpenAudio, Form1.Sonido.cs): antes del rechazo de XP, RocolaOsConvertidaAbre arma el lector igual que XP (SinSilencios,
//     SellaFuenteAudio con el sello del ORIGINAL, ComoEstereo, FileAudioSource) pero sobre el WAV, y sin SinRelleno (el hueco del codificador
//     es de los MP3: el WAV no lo lleva). Si aun no esta, se pide y se espera hasta 20 s (en el hilo de la apertura; el medidor de Ctrl+F3,
//     que abre en la pantalla, no espera); si tarda mas, se lanza un IOException «se esta convirtiendo»: por el camino de siempre
//     (AudioStartFailed) el credito se devuelve, y como es un IOException (EsArchivoDanado) NO queda danada: la conversion sigue y la
//     proxima vez suena. Sin ffmpeg, sin sitio o si ffmpeg no pudo: null, y sigue lo de XP (rechazo, danada, rocola_incompatibles.txt).
//   - g111 (LectorMedida, Form1.Sonido.cs: el repaso, la duracion y la tanda larga): RocolaOsConvertidaMide da la ruta del WAV en lugar de
//     la del original (lo que se mide es el mismo sonido; la medida se guarda bajo la ruta del original, que es la que usa quien mide). Si
//     aun no esta, se pide y se espera hasta 60 s en ese hilo de fondo; si no llega, la ruta original y XP la rechaza como siempre.
// El repaso es lo que la encuentra primero (al medirla), asi que normalmente ya esta convertida cuando alguien la pide.
//
// Sin ROCOLA_OS, o con la funcion apagada: los dos ganchos devuelven lo de XP sin leer nada (ni la cabecera). Con ella, un MP3 de verdad
// (y el MP3 dentro de un WAV, formato 0x55) tampoco cambia: NoEsMp3.OtroFormato da null.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Rocola
{
    public partial class Form1
    {
        internal const string RocolaOsConvierteNombre = "convierte";

#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraConvierte()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsConvierteNombre,
                Que = "los «MP3» que son otro formato, convertidos a WAV con ffmpeg (el original no se toca)",
                DeFabrica = true,
                Arranca = f => RocolaOs.Hilo("convierte:limpia", RocolaOsConvertidas.LimpiaRestos),
                Vivo = (f, sb) => sb.Append("convertidas=").Append(RocolaOsConvertidas.Hechas).Append(" fallidas=").Append(RocolaOsConvertidas.Fallidas).Append('\n'),
                ParaAlCerrar = razon => RocolaOsConvertidas.Para(),
            });
        }

        // g110: el lector de la convertida (null: lo de XP)
        private IAudioSource RocolaOsConvertidaAbre(string path)
        {
            if (!RocolaOs.Activo || !RocolaOs.Encendida(RocolaOsConvierteNombre)) return null;
            if (path == null || !path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return null;
            string otro = NoEsMp3.OtroFormato(path);
            if (otro == null) return null;
            string wav = RocolaOsConvertidas.Espera(path, audioOpeningForMeasurement ? 0 : RocolaOsConvertidas.EsperaPedidaMs);
            if (wav == null)
            {
                if (RocolaOsConvertidas.Convirtiendo(path))
                    throw new IOException("se esta convirtiendo (era " + otro + " con otro nombre): sonara la proxima vez");   // credito devuelto, sin marcarla danada
                return null;   // sin ffmpeg, sin sitio o no se pudo: lo de XP
            }
            FuenteMedida inicio = CapturaFuente(path);   // el sello es el del original: sus medidas son las de la cancion
            return SinSilencios(path, SellaFuenteAudio(path, inicio, ComoEstereo(new FileAudioSource(wav))));
        }

        // g111: lo que lee el medidor (la ruta del WAV, o la misma)
        internal static string RocolaOsConvertidaMide(string ruta)
        {
            if (!RocolaOs.Activo || !RocolaOs.Encendida(RocolaOsConvierteNombre)) return ruta;
            if (ruta == null || !ruta.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || NoEsMp3.OtroFormato(ruta) == null) return ruta;
            return RocolaOsConvertidas.Espera(ruta, RocolaOsConvertidas.EsperaMedidaMs) ?? ruta;
        }
    }

    // Las conversiones: de una en una, en un hilo de fondo (nice 19), como Motor/Conversiones.cs.
    internal static class RocolaOsConvertidas
    {
        internal const int EsperaPedidaMs = 20000;   // pedida: lo que se espera al principio de la cola (decision del 4-11-2026)
        internal const int EsperaMedidaMs = 60000;   // el repaso: lo que espera su hilo antes de dejarla como XP
        internal const long ReservaBytes = 1024L * 1024 * 1024;   // 1 GB libre (mas lo que ocupara la copia)
        private static readonly object llave = new object();
        private static readonly Queue<string> pendientes = new Queue<string>();
        private static readonly HashSet<string> enCurso = new HashSet<string>(StringComparer.Ordinal);   // en la cola o convirtiendose
        private static readonly Dictionary<string, string> fallidas = new Dictionary<string, string>(StringComparer.Ordinal);   // clave -> por que
        private static bool trabajando, parado;
        private static Process actual;   // el ffmpeg que corre (para cortarlo al cerrar)
        private static int hechas;
        internal static int Hechas { get { return Volatile.Read(ref hechas); } }
        internal static int Fallidas { get { lock (llave) return fallidas.Count; } }

        private static int MaxConvertirMs
        {
            get { int m; return int.TryParse(Environment.GetEnvironmentVariable("ROCOLA_CONVERTIR_MAX_MS"), out m) && m >= 1000 ? m : 10 * 60 * 1000; }
        }
        internal static string Carpeta
        {
            get
            {
                string d = Environment.GetEnvironmentVariable("ROCOLA_CONVERTIDAS");
                return string.IsNullOrEmpty(d) || d.Trim().Length == 0 ? RocolaOs.Datos("convertidas") : d.Trim();
            }
        }

        // La copia que le toca a este original (exista o no); null si el original no se puede leer.
        private static string Clave(string ruta)
        {
            try
            {
                FileInfo f = new FileInfo(ruta);
                if (!f.Exists) return null;
                string id = Path.GetFullPath(ruta) + "|" + f.Length + "|" + f.LastWriteTimeUtc.Ticks;
                byte[] h = SHA1.HashData(Encoding.UTF8.GetBytes(id));
                return Convert.ToHexString(h, 0, 12).ToLowerInvariant();
            }
            catch { return null; }
        }
        private static string RutaDe(string clave) { return Path.Combine(Carpeta, clave + ".wav"); }

        internal static string Lista(string ruta)
        {
            string c = Clave(ruta);
            if (c == null) return null;
            string r = RutaDe(c);
            try { return File.Exists(r) ? r : null; } catch { return null; }
        }
        internal static bool Convirtiendo(string ruta) { string c = Clave(ruta); if (c == null) return false; lock (llave) return enCurso.Contains(c); }

        // La convertida, pidiendola si hace falta y esperando hasta ms (0: sin esperar). null si no esta (aun no, o no se pudo).
        internal static string Espera(string ruta, int ms)
        {
            string w = Lista(ruta);
            if (w != null) return w;
            if (!Pide(ruta)) return null;   // ya fallo (o no se puede leer el original)
            int desde = Environment.TickCount;
            while (true)
            {
                w = Lista(ruta);
                if (w != null || !Convirtiendo(ruta)) return w;
                if (unchecked(Environment.TickCount - desde) >= ms) return null;
                Thread.Sleep(100);
            }
        }

        // Que se convierta (si no esta hecha, en marcha o fallida). false si ya se intento y no se pudo.
        private static bool Pide(string ruta)
        {
            string c = Clave(ruta);
            if (c == null) return false;
            lock (llave)
            {
                if (fallidas.ContainsKey(c) || parado) return false;
                if (enCurso.Contains(c)) return true;
                enCurso.Add(c); pendientes.Enqueue(ruta);
                if (trabajando) return true;
                trabajando = true;
            }
            RocolaOs.Hilo("convierte", Trabaja);
            return true;
        }

        private static void Trabaja()
        {
            while (true)
            {
                string ruta;
                lock (llave) { if (pendientes.Count == 0 || parado) { trabajando = false; pendientes.Clear(); enCurso.Clear(); return; } ruta = pendientes.Peek(); }
                string c = Clave(ruta), porque;
                int t0 = Environment.TickCount;
                try { porque = c != null ? Convierte(ruta, RutaDe(c)) : "no se pudo leer"; }
                catch (Exception ex) { porque = ex.GetType().Name + ": " + ex.Message; }
                lock (llave)
                {
                    pendientes.Dequeue();
                    if (c != null) { if (porque != null) fallidas[c] = porque; enCurso.Remove(c); }
                }
                if (porque == null) Interlocked.Increment(ref hechas);
                RocolaOs.Apunta("convierte", Path.GetFileName(ruta) + (porque == null
                    ? ": WAV listo en " + (unchecked(Environment.TickCount - t0) / 1000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s"
                    : ": no se pudo (" + porque + "): queda como en XP"));
            }
        }

        // null si salio bien; si no, por que.
        private static string Convierte(string ruta, string destino)
        {
            string carpeta = Path.GetDirectoryName(destino);
            Directory.CreateDirectory(carpeta);
            try { if (new DriveInfo(carpeta).AvailableFreeSpace < ReservaBytes + new FileInfo(ruta).Length * 16) return "sin sitio en el disco (menos de 1 GB libre en " + carpeta + ")"; }
            catch { }
            string tmp = destino + ".haciendo.wav";
            string ffmpeg = Environment.GetEnvironmentVariable("ROCOLA_FFMPEG");
            ProcessStartInfo pi = new ProcessStartInfo(string.IsNullOrEmpty(ffmpeg) ? "ffmpeg" : ffmpeg);
            foreach (string s in new[] { "-nostdin", "-hide_banner", "-v", "error", "-y", "-i", ruta, "-map", "0:a:0", "-vn", "-sn", "-dn",
                "-map_metadata", "-1", "-c:a", "pcm_s16le", "-f", "wav", tmp })
                pi.ArgumentList.Add(s);
            pi.UseShellExecute = false; pi.RedirectStandardError = true; pi.RedirectStandardOutput = true; pi.RedirectStandardInput = false; pi.CreateNoWindow = true;
            Process p;
            try { p = Process.Start(pi); } catch (Exception ex) { return "sin ffmpeg (" + ex.Message + ")"; }
            if (p == null) return "sin ffmpeg";
            using (p)
            {
                lock (llave) { if (parado) { try { p.Kill(true); } catch { } return "la rocola se cierra"; } actual = p; }
                // lo que dice se lee aparte (leerlo de corrido esperaria a que acabe: un ffmpeg colgado dejaria la conversion parada)
                StringBuilder dijo = new StringBuilder();
                p.ErrorDataReceived += delegate (object o, DataReceivedEventArgs e) { if (e.Data != null) lock (dijo) if (dijo.Length < 4000) dijo.AppendLine(e.Data); };
                p.OutputDataReceived += delegate { };
                p.BeginErrorReadLine(); p.BeginOutputReadLine();
                bool acabo = p.WaitForExit(MaxConvertirMs);
                lock (llave) actual = null;
                if (!acabo)
                {
                    try { p.Kill(true); } catch { }
                    try { p.WaitForExit(2000); } catch { }
                    try { File.Delete(tmp); } catch { }
                    return "ffmpeg no acabo en " + MaxConvertirMs / 1000 + " s";
                }
                p.WaitForExit();
                string err; lock (dijo) err = dijo.ToString();
                if (p.ExitCode != 0 || !File.Exists(tmp) || new FileInfo(tmp).Length < 1000)
                {
                    try { File.Delete(tmp); } catch { }
                    err = err.Trim(); if (err.Length > 150) err = err.Substring(err.Length - 150);
                    return "ffmpeg: " + (err.Length > 0 ? err : "codigo " + p.ExitCode);
                }
            }
            File.Move(tmp, destino, true);   // de un tiron: una convertida a medias nunca se ve como hecha
            return null;
        }

        // Al arrancar: las copias a medias de un apagon (.haciendo.wav) se borran.
        internal static void LimpiaRestos()
        {
            try
            {
                string d = Carpeta;
                if (!Directory.Exists(d)) return;
                foreach (string f in Directory.GetFiles(d, "*.haciendo.wav")) { try { File.Delete(f); } catch { } }
            }
            catch { }
        }

        // Al cerrar: nada nuevo, y el ffmpeg que corra se corta (su copia a medias se borra al arrancar).
        internal static void Para()
        {
            Process p;
            lock (llave) { parado = true; p = actual; }
            if (p != null) { try { p.Kill(true); } catch { } }
        }
    }
}
