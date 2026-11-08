// LA DURACION DE UN VIDEO SIN TOCARLO (port nativo de XP, 8-11-2026).
//
// En XP, Form1.MideDuracionVideo (Form1.Biblioteca.cs) crea un reproductor de Windows Media suelto (WMPlayer.OCX, sin interfaz, sin
// autoStart y callado) y le pide newMedia(ruta).duration: la duracion del CONTENEDOR (la de la cabecera del archivo), que el WMP rellena
// poco despues de abrirlo sin reproducir nada; XP la mira cada 100 ms hasta 3 s y, si no llega, deja ese video sin duracion.
//
// Aqui la misma cuenta la da ffmpeg (el programa: Rocola OS lo trae, BR2_PACKAGE_FFMPEG_FFMPEG; ffprobe no esta en el sistema). «ffmpeg -i
// RUTA» abre el archivo, lee su cabecera y escribe «Duration: HH:MM:SS.cc» (la duracion del contenedor segun libavformat, redondeada a la
// centesima: lo que guarda XP en rocola_duracion.txt, "0.##"), sin descomprimir nada y sin sonar. Se lanza al pedirla (PideDuracion, lo que
// era newMedia) en un proceso aparte, y Duracion la da en cuanto llega (0 mientras no): XP sigue mirando cada 100 ms con su tope de 3 s, y
// el que no conteste se deja (QuitaArchivo mata el proceso). Sin ffmpeg no se puede medir: Crea da null, como un XP sin el reproductor.
//
// Por que no libmpv (el reproductor, Nativo/ReproductorVideo.cs): su propiedad duration no es la del contenedor sino la de la pista mas larga
// (30,02 s en un video cuyo contenedor dice 30,00 s; medido el 8-11-2026), y XP guarda la del contenedor.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Rocola
{
    internal sealed class MedidorVideo : IDisposable
    {
        private static int hay;   // 0 sin mirar, 1 esta ffmpeg, -1 no
        private readonly object cerrojo = new object();
        private Process proceso;
        private int vuelta;        // cada PideDuracion; lo que conteste un proceso de una vuelta anterior no cuenta
        private double duracion;   // la del archivo pedido (0 mientras no se sabe)

        private MedidorVideo() { }

        // El medidor, si hay con que medir (en XP: el reproductor suelto, si esta el WMP).
        internal static MedidorVideo Crea() { return Hay() ? new MedidorVideo() : null; }
        private static bool Hay()
        {
            if (hay == 0)
            {
                bool esta = false;
                try
                {
                    foreach (string d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':'))
                        if (d.Length > 0 && File.Exists(Path.Combine(d, "ffmpeg"))) { esta = true; break; }
                }
                catch { }
                hay = esta ? 1 : -1;
            }
            return hay > 0;
        }

        // Empieza a medir ese archivo (en XP: newMedia(ruta), que no reproduce nada). false si ni siquiera se pudo pedir.
        internal bool PideDuracion(string ruta)
        {
            QuitaArchivo();
            if (string.IsNullOrEmpty(ruta)) return false;
            int esta;
            lock (cerrojo) { esta = ++vuelta; duracion = 0; }
            Process p;
            try
            {
                ProcessStartInfo pi = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, RedirectStandardInput = false, CreateNoWindow = true };
                foreach (string a in new[] { "-hide_banner", "-nostdin", "-i", ruta }) pi.ArgumentList.Add(a);
                p = new Process { StartInfo = pi };
                p.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e) { Lee(esta, e.Data); };
                p.OutputDataReceived += delegate (object s, DataReceivedEventArgs e) { };
                if (!p.Start()) return false;
                p.BeginErrorReadLine(); p.BeginOutputReadLine();
            }
            catch { return false; }
            lock (cerrojo) proceso = p;
            return true;
        }
        // Una linea de ffmpeg: «  Duration: 00:00:30.00, start: 0.000000, bitrate: 183 kb/s» (o «Duration: N/A» si no la sabe)
        private void Lee(int esta, string linea)
        {
            if (linea == null) return;
            int i = linea.IndexOf("Duration: ", StringComparison.Ordinal);
            if (i < 0) return;
            string t = linea.Substring(i + 10);
            int coma = t.IndexOf(','); if (coma >= 0) t = t.Substring(0, coma);
            string[] p = t.Trim().Split(':');
            if (p.Length != 3) return;
            int h, m; double s;
            if (!int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out h)
                || !int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out m)
                || !double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out s)) return;
            lock (cerrojo) { if (esta == vuelta && duracion <= 0) duracion = h * 3600.0 + m * 60.0 + s; }
        }

        // Lo que dura el archivo pedido, en segundos (0 mientras no se sabe; en XP: media.duration)
        internal double Duracion { get { lock (cerrojo) return duracion; } }

        // Deja el archivo (en XP: Marshal.ReleaseComObject(media)): si ffmpeg aun no acabo, se le corta.
        internal void QuitaArchivo()
        {
            Process p;
            lock (cerrojo) { p = proceso; proceso = null; vuelta++; duracion = 0; }
            if (p == null) return;
            try { if (!p.HasExited) p.Kill(); } catch { }
            try { p.WaitForExit(2000); } catch { }
            try { p.Dispose(); } catch { }
        }

        // (en XP: close y Marshal.ReleaseComObject del reproductor suelto)
        public void Dispose() { QuitaArchivo(); }
    }
}
