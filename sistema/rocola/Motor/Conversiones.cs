// LOS «MP3» QUE NO LO SON, CONVERTIDOS (4-11-2026, decision del dueno: «que se convierta automaticamente para la proxima, o si no tarda
// mucho que lo convierta en ese mismo instante y reproduzca la cancion»). Un M4A/AAC, WMA, OGG o FLAC con el nombre cambiado a .mp3
// (LectorCancion.OtroFormato) se pasa con ffmpeg (el del sistema, el de los videos) a un WAV de 16 bits con su frecuencia y sus canales:
// sin perder nada de lo que el archivo trae. El original NO se toca: la copia va aparte (ROCOLA_CONVERTIDAS, en el disco de la musica:
// /rocola/disco/rocola-os/convertidas; sin ese disco, en la carpeta de datos), con un nombre sacado de la ruta, el tamano y la fecha del
// original (si el archivo cambia, se convierte otra vez). Desde ahi la cancion suena y se mide como cualquier otra; las etiquetas, la
// portada y lo aprendido siguen siendo del original.
//  - Se convierte en cuanto alguien la pide o el repaso la encuentra, en un hilo aparte con la prioridad mas baja, de una en una.
//  - Pedida: espera hasta EsperaMaxMs al principio de la cola (aviso «Preparando...») y suena; si tarda mas, se devuelve el credito y la
//    conversion sigue para la proxima vez (Cola.cs: EmpiezaSiguiente).
//  - Sin ffmpeg, sin sitio (menos de 1 GB libre) o si ffmpeg no puede: queda como danada, como antes.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Rocola
{
    internal static class Conversiones
    {
        internal const int EsperaMaxMs = 20000;
        internal const long ReservaBytes = 1024L * 1024 * 1024;
        private static readonly object llave = new object();
        private static readonly Queue<string> pendientes = new Queue<string>();
        private static readonly HashSet<string> enCurso = new HashSet<string>(StringComparer.Ordinal);   // en la cola o convirtiendose
        private static readonly Dictionary<string, string> fallidas = new Dictionary<string, string>(StringComparer.Ordinal);   // clave -> por que
        private static bool trabajando;

        internal static string Carpeta
        {
            get
            {
                string d = Environment.GetEnvironmentVariable("ROCOLA_CONVERTIDAS");
                if (string.IsNullOrEmpty(d)) d = Path.Combine(Form1.CarpetaDatos, "convertidas");
                return d;
            }
        }

        // La copia que le toca a este original (exista o no); null si el original no se puede leer.
        private static string Clave(string ruta)
        {
            try
            {
                FileInfo f = new FileInfo(ruta);
                string id = Path.GetFullPath(ruta) + "|" + f.Length + "|" + f.LastWriteTimeUtc.Ticks;
                byte[] h = SHA1.HashData(Encoding.UTF8.GetBytes(id));
                return Convert.ToHexString(h, 0, 12).ToLowerInvariant();
            }
            catch { return null; }
        }
        private static string RutaDe(string clave) { return Path.Combine(Carpeta, clave + ".wav"); }

        // La copia ya hecha, o null.
        internal static string Lista(string ruta)
        {
            string c = Clave(ruta);
            if (c == null) return null;
            string r = RutaDe(c);
            try { return File.Exists(r) ? r : null; } catch { return null; }
        }
        // Se esta convirtiendo (o espera turno).
        internal static bool Convirtiendo(string ruta) { string c = Clave(ruta); if (c == null) return false; lock (llave) return enCurso.Contains(c); }
        // Ya se intento y no se pudo (por que), o null.
        internal static string Fallo(string ruta) { string c = Clave(ruta); if (c == null) return null; lock (llave) { string p; return fallidas.TryGetValue(c, out p) ? p : null; } }

        // Que se convierta (si no esta ya hecha, en marcha o fallida).
        internal static void Pide(string ruta)
        {
            string c = Clave(ruta);
            if (c == null || Lista(ruta) != null) return;
            lock (llave)
            {
                if (enCurso.Contains(c) || fallidas.ContainsKey(c)) return;
                enCurso.Add(c); pendientes.Enqueue(ruta);
                if (trabajando) return;
                trabajando = true;
            }
            new Thread(Trabaja) { IsBackground = true, Name = "convertir a WAV" }.Start();
        }

        private static void Trabaja()
        {
            Prioridad.Fondo();
            while (true)
            {
                string ruta;
                lock (llave) { if (pendientes.Count == 0) { trabajando = false; return; } ruta = pendientes.Dequeue(); }
                string c = Clave(ruta), porque = null;
                try { if (c != null) porque = Convierte(ruta, RutaDe(c)); else porque = "no se pudo leer"; }
                catch (Exception ex) { porque = ex.Message; }
                lock (llave)
                {
                    if (c != null) { enCurso.Remove(c); if (porque != null) fallidas[c] = porque; }
                }
                try { Console.WriteLine("Convertir: " + Path.GetFileName(ruta) + (porque == null ? " -> WAV listo" : ": no se pudo (" + porque + ")")); } catch { }
            }
        }

        // null si salio bien; si no, por que.
        private static string Convierte(string ruta, string destino)
        {
            string carpeta = Path.GetDirectoryName(destino);
            Directory.CreateDirectory(carpeta);
            try { if (new DriveInfo(carpeta).AvailableFreeSpace < ReservaBytes + new FileInfo(ruta).Length * 16) return "sin sitio en el disco"; } catch { }
            string tmp = destino + ".haciendo.wav";
            ProcessStartInfo pi = new ProcessStartInfo(Environment.GetEnvironmentVariable("ROCOLA_FFMPEG") ?? "ffmpeg");
            foreach (string s in new[] { "-nostdin", "-hide_banner", "-v", "error", "-y", "-i", ruta, "-map", "0:a:0", "-vn", "-sn", "-dn",
                "-map_metadata", "-1", "-c:a", "pcm_s16le", "-f", "wav", tmp })
                pi.ArgumentList.Add(s);
            pi.UseShellExecute = false; pi.RedirectStandardError = true; pi.RedirectStandardOutput = true;
            Process p;
            try { p = Process.Start(pi); } catch (Exception ex) { return "sin ffmpeg (" + ex.Message + ")"; }
            using (p)
            {
                string err = p.StandardError.ReadToEnd();
                p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(600000)) { try { p.Kill(); } catch { } try { File.Delete(tmp); } catch { } return "ffmpeg no acabo"; }
                if (p.ExitCode != 0 || !File.Exists(tmp) || new FileInfo(tmp).Length < 1000)
                {
                    try { File.Delete(tmp); } catch { }
                    err = err.Trim(); if (err.Length > 150) err = err.Substring(err.Length - 150);
                    return "ffmpeg: " + (err.Length > 0 ? err : "codigo " + p.ExitCode);
                }
            }
            File.Move(tmp, destino, true);
            return null;
        }
    }
}
