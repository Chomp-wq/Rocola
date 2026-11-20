// EL BANCO DE LAS CARATULAS (rapido-caratulas, 20-11-2026; prueba, no es de XP ni va en Rocola OS). Lo que hace el lector de portadas de XP
// (CoverPump de Form1.Portadas.cs) con cada portada, una por una, en este hilo, cronometrado por partes, y la huella de cada resultado:
//   rocola-xp --banco-caratulas LISTA MODO [SALIDA]
//     LISTA   un archivo con una ruta de portada por renglon
//     MODO    frio     sin miniatura guardada: se borra la suya antes (la original se decodifica, se reduce y se guarda la PNG)
//             guardada con la miniatura ya guardada (la lee); con BANCO_DESALOJA=1 se le quita antes de la cache del sistema (fadvise)
//             partes   lo de DecodeCoverCore por la original, paso a paso con las funciones nativas (leer, Imagenes.Lee, la reduccion,
//                      Mapas.Copia, la PNG): para ver donde se va el tiempo (no se guarda nada)
//     SALIDA  (opcional) un renglon por portada: ruta, huella de la miniatura publicada, de la copia por guardar, del archivo guardado, de la
//             previa y de los tonos y la paleta. Dos versiones del programa tienen que dar el mismo archivo.
// La carpeta de las miniaturas es la de siempre (ROCOLA_IA_DATOS/rocola_miniaturas, o la de datos con ROCOLA_DATOS).
// Corre en el inicializador del modulo (antes que la pantalla), sin la rocola: no necesita OpenGL.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void BancoCaratulasRegistra() { RocolaOsOrdenes.Si("--banco-caratulas", BancoCaratulas); }

        private static readonly CultureInfo bci = CultureInfo.InvariantCulture;
        private static string BHuella(byte[] b, int n) { byte[] h = SHA256.HashData(new ReadOnlySpan<byte>(b, 0, n)); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static unsafe string BPuntos(SKBitmap b)
        {
            if (b == null) return "null";
            int fila = b.Width * 4; byte[] t = new byte[fila * b.Height];
            byte* p = (byte*)b.GetPixels();
            for (int y = 0; y < b.Height; y++) Marshal.Copy((IntPtr)(p + (long)y * b.RowBytes), t, y * fila, fila);
            return b.Width + "x" + b.Height + ":" + b.ColorType + "/" + b.AlphaType + ":" + BHuella(t, t.Length);
        }
        private static string BArchivo(string ruta)
        {
            if (ruta == null || !File.Exists(ruta)) return "no";
            byte[] d = File.ReadAllBytes(ruta); return Path.GetExtension(ruta) + ":" + d.Length + ":" + BHuella(d, d.Length);
        }
        [DllImport("libc", SetLastError = true)] private static extern int posix_fadvise(int fd, long offset, long len, int advice);
        private static void BDesaloja(string ruta)
        {
            try { using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read)) posix_fadvise((int)fs.SafeFileHandle.DangerousGetHandle(), 0, 0, 4); } catch { }
        }
        private static double BMs(long t) { return t * 1000.0 / Stopwatch.Frequency; }

        private sealed class BTiempos
        {
            internal readonly Dictionary<string, List<double>> Por = new Dictionary<string, List<double>>();
            internal void Pon(string clase, string que, double ms)
            {
                foreach (string k in new string[] { clase + "\t" + que, "TODAS\t" + que })
                { List<double> l; if (!Por.TryGetValue(k, out l)) Por[k] = l = new List<double>(); l.Add(ms); }
            }
        }
        private static string BClase(string ruta, byte[] d, int n)
        {
            int w, h; string ext = Path.GetExtension(ruta).ToLowerInvariant();
            string tam = MedidaDeCabecera(d, n, out w, out h) ? (Math.Max(w, h) <= 720 ? "chica" : Math.Max(w, h) <= 1450 ? "media" : "grande") : "?";
            return ext + "-" + tam;
        }

        private static int BancoCaratulas(string[] a)
        {
            if (a.Length < 2) { Console.Error.WriteLine("uso: --banco-caratulas LISTA frio|guardada|partes [SALIDA]"); return 2; }
            List<string> rutas = new List<string>();
            foreach (string l in File.ReadAllLines(a[0])) if (l.Trim().Length > 0) rutas.Add(l.Trim());
            string modo = a[1];
            StreamWriter salida = a.Length >= 3 ? new StreamWriter(a[2]) : null;
            bool desaloja = Environment.GetEnvironmentVariable("BANCO_DESALOJA") == "1";
            BTiempos t = new BTiempos();
            Stopwatch total = Stopwatch.StartNew();
            for (int i = 0; i < rutas.Count; i++)
            {
                string ruta = rutas[i];
                byte[] cab = new byte[64 * 1024]; int ncab;
                using (FileStream fs = File.OpenRead(ruta)) ncab = fs.Read(cab, 0, cab.Length);
                string clase = BClase(ruta, cab, ncab);
                if (modo == "partes") { BPartes(ruta, clase, t, desaloja); continue; }
                string id = ThumbCachePath(ruta);
                if (modo == "frio")
                {
                    foreach (string f in new string[] { id, MiniaturaJpeg(id), MiniaturaAlfa(id) }) try { if (File.Exists(f)) File.Delete(f); } catch { }
                    if (desaloja) BDesaloja(ruta);
                }
                else if (desaloja) { string g = MiniaturaGuardada(id); if (g != null) BDesaloja(g); }
                // lo de CoverPump, en su orden (sin la cache de memoria ni el aviso a la pantalla)
                long t0 = Stopwatch.GetTimestamp();
                SKBitmap pendiente;
                SKBitmap thumb = DecodeCoverForPublication(ruta, id, out pendiente);
                long t1 = Stopwatch.GetTimestamp();
                SKBitmap previa = null; byte[] previaBytes = null;
                if (thumb != null) { previa = GpuPreviewStore.Small(thumb); previaBytes = GpuPreviewStore.ToBytes(previa); }
                long t2 = Stopwatch.GetTimestamp();
                Color uno = Color.Empty, dos = Color.Empty, prom = Color.Empty; Color[] paleta = null;
                if (thumb != null) { prom = ColoresDePortada(thumb, out uno, out dos); paleta = ExtractCoverPalette(thumb, FormatoPortada); }
                long t3 = Stopwatch.GetTimestamp();
                string huellaPendiente = BPuntos(pendiente);
                long t4 = Stopwatch.GetTimestamp();
                if (pendiente != null && thumb != null) SaveThumb(pendiente, id);
                long t5 = Stopwatch.GetTimestamp();
                t.Pon(clase, "1-miniatura", BMs(t1 - t0)); t.Pon(clase, "2-previa", BMs(t2 - t1)); t.Pon(clase, "3-tonos+paleta", BMs(t3 - t2));
                t.Pon(clase, "4-guardar", BMs(t5 - t4)); t.Pon(clase, "9-total", BMs(t1 - t0 + t3 - t1 + t5 - t4));
                if (salida != null)
                {
                    StringBuilder pal = new StringBuilder();
                    if (paleta != null) foreach (Color c in paleta) pal.Append(c.ToArgb().ToString("x8")).Append(',');
                    salida.WriteLine(Path.GetFileName(Path.GetDirectoryName(ruta)) + "\t" + BPuntos(thumb) + "\t" + huellaPendiente + "\t" + BArchivo(MiniaturaGuardada(id))
                        + "\t" + (previaBytes == null ? "null" : BHuella(previaBytes, previaBytes.Length)) + "\t" + prom.ToArgb().ToString("x8") + "," + uno.ToArgb().ToString("x8") + "," + dos.ToArgb().ToString("x8") + "\t" + pal);
                }
                if (previa != null) previa.Dispose(); if (pendiente != null) pendiente.Dispose(); if (thumb != null) thumb.Dispose();
            }
            if (salida != null) salida.Dispose();
            List<string> claves = new List<string>(t.Por.Keys); claves.Sort(StringComparer.Ordinal);
            Console.WriteLine("clase\tparte\tn\tp50\tp95\tmax\tsuma (ms); " + rutas.Count + " portadas en " + total.ElapsedMilliseconds + " ms, " + Environment.ProcessorCount + " procesadores");
            foreach (string k in claves)
            {
                List<double> l = t.Por[k]; l.Sort(); double s = 0; foreach (double v in l) s += v;
                Console.WriteLine(k + "\t" + l.Count + "\t" + l[l.Count / 2].ToString("0.00", bci) + "\t" + l[Math.Min(l.Count - 1, (int)(l.Count * 0.95))].ToString("0.00", bci)
                    + "\t" + l[l.Count - 1].ToString("0.00", bci) + "\t" + s.ToString("0", bci));
            }
            return 0;
        }

        // lo de DecodeCoverCore por la original, paso a paso (la rama de siempre: «Caratulas en JPEG» apagado), con las funciones nativas
        private static void BPartes(string ruta, string clase, BTiempos t, bool desaloja)
        {
            if (desaloja) BDesaloja(ruta);
            long t0 = Stopwatch.GetTimestamp();
            int largo; byte[] data = LeeEntero(ruta, out largo);
            long t1 = Stopwatch.GetTimestamp();
            FormatoImagen formato;
            SKBitmap src;
            using (MemoryStream ms = new MemoryStream(data, 0, largo, false)) src = Imagenes.Lee(ms, out formato);
            long t2 = Stopwatch.GetTimestamp();
            SKBitmap thumb = Mapas.Nuevo(ThumbPx, ThumbPx);
            using (SKCanvas g = new SKCanvas(thumb)) Imagenes.Pinta(g, src, new SKRect(0, 0, src.Width, src.Height), new SKRect(0, 0, ThumbPx, ThumbPx), Filtro.BilinealAlta, true);
            long t3 = Stopwatch.GetTimestamp();
            SKBitmap copia = Mapas.Copia(thumb, new Rectangle(0, 0, ThumbPx, ThumbPx));
            long t4 = Stopwatch.GetTimestamp();
            byte[] png;
            using (MemoryStream o = new MemoryStream()) { Imagenes.GuardaPng(copia, o); png = o.ToArray(); }
            long t5 = Stopwatch.GetTimestamp();
            SKBitmap leida;
            using (MemoryStream ms = new MemoryStream(png)) leida = Imagenes.Lee(ms, out formato);
            long t6 = Stopwatch.GetTimestamp();
            SKBitmap copia2 = CopiaMiniaturaGpu(leida);
            long t7 = Stopwatch.GetTimestamp();
            t.Pon(clase, "a-leer-original", BMs(t1 - t0)); t.Pon(clase, "b-decodificar-original", BMs(t2 - t1)); t.Pon(clase, "c-reducir-256", BMs(t3 - t2));
            t.Pon(clase, "d-copia", BMs(t4 - t3)); t.Pon(clase, "e-png-codificar", BMs(t5 - t4)); t.Pon(clase, "f-png-decodificar", BMs(t6 - t5));
            t.Pon(clase, "g-copia-gpu", BMs(t7 - t6)); t.Pon(clase, "h-kb-png", png.Length / 1024.0);
            src.Dispose(); thumb.Dispose(); copia.Dispose(); leida.Dispose(); copia2.Dispose();
        }
    }
}
