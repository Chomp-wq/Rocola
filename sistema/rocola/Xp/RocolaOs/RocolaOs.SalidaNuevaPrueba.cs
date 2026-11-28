// LA SALIDA NUEVA: UN DOBLE PARA LAS PRUEBAS (Pruebas/RocolaOs/salida-nueva/prueba.sh), como «calidad-prueba». No hace nada en la rocola: la
// funcion «salida_nueva-prueba» solo va con ROCOLA_OS=lista que la nombre (SoloEnLaLista). En su latido (hilo de la pantalla) lee
// $ROCOLA_RUN/rocola-xp-salida-nueva-prueba, un renglon por orden, y apunta lo que paso en el mismo archivo con «.log» detras:
//   pon FINAL     pide la cancion (AddSong) o el video (AddVideoToQueue) cuya ruta acaba en FINAL, como PONER (cobro y turno)
//   salta         lo que suena, como SALTAR (SkipSong)
//   vol N / mudo  N dB al volumen del dueno / el silencio (como los botones de atras)
//   mide          la medida del convertidor (la de la hoja); «medida» apunta lo que dio
//   mira          el resumen de la salida nueva y sus fuentes
// Y la orden «rocola-xp --prueba-salida-nueva elige» (sin la rocola): que tarjeta toca con ROCOLA_ASOUND y ROCOLA_TARJETA_TXT.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraSalidaNuevaPrueba()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = "salida_nueva-prueba",
                Que = "el doble de la salida nueva, solo para las pruebas (ROCOLA_OS=...,salida_nueva-prueba)",
                DeFabrica = false,
                SoloEnLaLista = true,
                Latido = f => f.RocolaOsLatidoSalidaNuevaPrueba(),
            });
            RocolaOsOrdenes.Si("--prueba-salida-nueva", RocolaOsPruebaSalidaNuevaOrden);
        }
        private static int RocolaOsPruebaSalidaNuevaOrden(string[] a)
        {
            if (a.Length > 0 && a[0] == "elige")
            {
                SalidaNueva.Eleccion e = SalidaNueva.Elige();
                if (e == null) { Console.WriteLine("ELIGE ninguna"); return 0; }
                Console.WriteLine("ELIGE pcm=" + e.Pcm + " tarjeta=" + e.Tarjeta + " usb=" + (e.Usb ? 1 : 0) + " | " + e.Porque + " | aviso=" + e.Aviso);
                return 0;
            }
            Console.Error.WriteLine("uso: rocola-xp --prueba-salida-nueva elige");
            return 2;
        }
        private SalidaNuevaPruebaSonido rocolaOsPruebaSonido;
        private void RocolaOsLatidoSalidaNuevaPrueba()
        {
            string ruta = RocolaOs.Run("rocola-xp-salida-nueva-prueba");
            if (!File.Exists(ruta)) return;
            string[] ls;
            try { ls = File.ReadAllLines(ruta); File.Delete(ruta); } catch { return; }
            StringBuilder log = new StringBuilder();
            foreach (string l0 in ls)
            {
                string l = l0.Trim();
                if (l.Length == 0) continue;
                log.Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append(' ');
                if (l == "mira")
                {
                    log.Append("mira ").Append(SalidaNueva.Resumen()).Append(" | pcm=").Append(SalidaNueva.PcmNombre).Append(" formato=").Append(SalidaNueva.FormatoAbierto)
                       .Append(" hz=").Append(SalidaNueva.TasaAbierta).Append(" control=").Append(SalidaNueva.ControlHw).Append(" videos=").Append(SalidaNueva.VideosPorLaRocola ? 1 : 0)
                       .Append(" ganancia_video=").Append(SalidaNueva.GananciaVideo.ToString("0.0000", CultureInfo.InvariantCulture)).Append(" aviso=").Append(SalidaNueva.Aviso);
                    foreach (SalidaNuevaCliente c in SalidaNueva.Fuentes)
                        log.Append(" | fuente ").Append(c.Nombre).Append(' ').Append(c.TasaIn).Append(" Hz tramos=").Append(c.Tramos).Append(c.Cerrada ? " cerrada" : "");
                    log.Append('\n');
                    continue;
                }
                if (l == "salta") { SkipSong(); log.Append("salta\n"); continue; }
                if (l == "mudo") { SalidaNueva.CambiaVolumen(0, true); log.Append("mudo ").Append(SalidaNueva.Mudo ? 1 : 0).Append('\n'); continue; }
                if (l.StartsWith("vol ", StringComparison.Ordinal))
                {
                    int n; int.TryParse(l.Substring(4), NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
                    SalidaNueva.CambiaVolumen(n, false); log.Append("vol ").Append(SalidaNueva.VolumenDb).Append('\n'); continue;
                }
                if (l == "prueba")
                {
                    // una prueba de sonido sola por el mismo flujo: 500 Hz a 48 kHz, un canal, 3 s, a la mitad (-6 dB), con el volumen del dueno
                    SalidaNuevaPruebaSonido p = SalidaNueva.EmpiezaPrueba(new RocolaOsSenoDePrueba(48000, 500, 3.0), "prueba del doble", 0.5f, true);
                    log.Append("prueba ").Append(p != null ? "empieza" : "no se pudo").Append('\n'); rocolaOsPruebaSonido = p; continue;
                }
                if (l == "prueba-oida")
                {
                    SalidaNuevaPruebaSonido p = rocolaOsPruebaSonido;
                    log.Append("prueba-oida ").Append(p == null ? "-" : p.SegundosOidos.ToString("0.00", CultureInfo.InvariantCulture) + " s" + (p.Sonando ? " sonando" : " acabo")).Append('\n'); continue;
                }
                if (l == "mide") { log.Append("mide ").Append(SalidaNuevaMide.EmpiezaEnLaRocola() ? "empieza" : "no se pudo").Append('\n'); continue; }
                if (l == "medida")
                {
                    SalidaNuevaMide.Informe inf = SalidaNuevaMide.Ultimo;
                    log.Append("medida ").Append(SalidaNuevaMide.Midiendo ? "midiendo " : "").Append(inf == null ? "ninguna (" + SalidaNuevaMide.Estado + ")" : inf.Texto().Replace("\n", " || ")).Append('\n');
                    continue;
                }
                if (!l.StartsWith("pon ", StringComparison.Ordinal)) { log.Append("no entiendo: ").Append(l).Append('\n'); continue; }
                string fin = l.Substring(4);
                Track t = null; Album d = null;
                foreach (Album a in albums)
                {
                    if (a == null || a.Tracks == null) continue;
                    foreach (Track x in a.Tracks) if (x != null && x.Path != null && x.Path.EndsWith(fin, StringComparison.Ordinal)) { t = x; d = a; break; }
                    if (t != null) break;
                }
                int antes = credits;
                if (t != null) AddSong(t, d);
                else
                {
                    string v = null;
                    foreach (string x in RocolaOsTodosLosVideos()) if (x.EndsWith(fin, StringComparison.Ordinal)) { v = x; break; }
                    if (v == null) { log.Append("pon ").Append(fin).Append(" no esta en la biblioteca\n"); continue; }
                    AddVideoToQueue(v, null);
                }
                log.Append("pon ").Append(fin).Append(" creditos ").Append(antes).Append("->").Append(credits).Append(" cola=").Append(queue.Count).Append('\n');
            }
            try { File.AppendAllText(ruta + ".log", log.ToString()); } catch { }
        }
    }

    // (el doble) un seno de prueba: frecuencia, duracion y un canal
    internal sealed class RocolaOsSenoDePrueba : NAudio.Wave.ISampleProvider
    {
        private readonly NAudio.Wave.WaveFormat f; private readonly double hz; private readonly long total; private long pos;
        internal RocolaOsSenoDePrueba(int tasa, double hz, double seg) { f = NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(tasa, 1); this.hz = hz; total = (long)(tasa * seg); }
        public NAudio.Wave.WaveFormat WaveFormat { get { return f; } }
        public int Read(Span<float> d)
        {
            int n = (int)Math.Min(d.Length, total - pos);
            for (int i = 0; i < n; i++, pos++) d[i] = (float)Math.Sin(2 * Math.PI * hz * pos / f.SampleRate);
            return Math.Max(0, n);
        }
    }
}
