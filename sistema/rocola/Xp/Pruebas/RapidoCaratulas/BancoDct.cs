// LA ESCALA DCT, MEDIDA (rapido-caratulas, 21-11-2026; prueba, no es de XP ni va en Rocola OS): para cada JPEG de la lista, la miniatura de
// 256 como la hace XP (Imagenes.Lee entera y la reduccion de DecodeCoverCore: BilinealAlta con medio pixel) contra la misma reduccion desde la
// JPEG decodificada ya reducida (RocolaOsLeeJpegReducida, g222) con los dos lados en al menos VECES x 256, para varias VECES:
//   rocola-xp --banco-dct LISTA [VECES,VECES...] [SALIDA]
// Por VECES: a cuantas se aplica, lo que tarda decodificar y reducir (p50 y p95, y lo mismo entero) y la diferencia de los puntos de la
// miniatura (BGRA premultiplicado, de 0 a 255 por canal): la media, el p99 y el p99,9 de cada portada (la mediana y el peor de todas), el
// mayor en todo y el mayor sin el marco de 3 puntos (donde el borde transparente de GDI+ se mezcla), y cuantos puntos pasan de 16.
// Y lo que cada una se aparta de la REFERENCIA (la entera reducida por el promedio exacto de area: DReferencia), sin el marco.
// Con SALIDA, un renglon por portada y VECES.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void BancoDctRegistra() { RocolaOsOrdenes.Si("--banco-dct", BancoDct); }

        private static SKBitmap DReduce(SKBitmap src)
        {
            SKBitmap t = Mapas.Nuevo(ThumbPx, ThumbPx);
            using (SKCanvas g = new SKCanvas(t)) Imagenes.Pinta(g, src, new SKRect(0, 0, src.Width, src.Height), new SKRect(0, 0, ThumbPx, ThumbPx), Filtro.BilinealAlta, true);
            return t;
        }
        private static unsafe void DDiferencia(SKBitmap a, SKBitmap b, out double media, out int p99, out int p999, out int max, out int maxDentro, out int sobre16)
        {
            int[] hist = new int[256]; long suma = 0; max = maxDentro = sobre16 = 0; int n = 0;
            for (int y = 0; y < ThumbPx; y++)
            {
                byte* pa = (byte*)a.GetPixels() + (long)y * a.RowBytes, pb = (byte*)b.GetPixels() + (long)y * b.RowBytes;
                for (int x = 0; x < ThumbPx * 4; x++)
                {
                    int d = Math.Abs(pa[x] - pb[x]); hist[d]++; suma += d; n++;
                    if (d > max) max = d;
                    int px = x / 4;
                    if (px >= 3 && px < ThumbPx - 3 && y >= 3 && y < ThumbPx - 3 && d > maxDentro) maxDentro = d;
                    if (d > 16) sobre16++;
                }
            }
            media = (double)suma / n;
            p99 = DPercentil(hist, n, 0.99); p999 = DPercentil(hist, n, 0.999);
        }
        // LA REFERENCIA: la imagen entera reducida a 256 por el promedio exacto de lo que cubre cada punto (area, en double): lo que un
        // filtro ideal de achicar daria. Se mide cuanto se aparta de ella cada miniatura (la de XP y las de la DCT), sin el marco de 3 puntos
        // (ahi la de XP se mezcla con lo transparente de fuera a proposito).
        private static unsafe double[] DReferencia(SKBitmap src)
        {
            int W = src.Width, H = src.Height, N = ThumbPx;
            double[] fila = new double[N * W * 4];   // primero en vertical: N filas de W
            double sy = (double)H / N;
            for (int i = 0; i < N; i++)
            {
                double y0 = i * sy, y1 = (i + 1) * sy;
                for (int y = (int)Math.Floor(y0); y < Math.Min(H, (int)Math.Ceiling(y1)); y++)
                {
                    double peso = (Math.Min(y1, y + 1) - Math.Max(y0, y)) / sy; if (peso <= 0) continue;
                    byte* p = (byte*)src.GetPixels() + (long)y * src.RowBytes;
                    for (int x = 0; x < W * 4; x++) fila[i * W * 4 + x] += p[x] * peso;
                }
            }
            double[] r = new double[N * N * 4]; double sx = (double)W / N;
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    double x0 = j * sx, x1 = (j + 1) * sx;
                    for (int x = (int)Math.Floor(x0); x < Math.Min(W, (int)Math.Ceiling(x1)); x++)
                    {
                        double peso = (Math.Min(x1, x + 1) - Math.Max(x0, x)) / sx; if (peso <= 0) continue;
                        for (int c = 0; c < 4; c++) r[(i * N + j) * 4 + c] += fila[i * W * 4 + x * 4 + c] * peso;
                    }
                }
            return r;
        }
        private static unsafe void DContraRef(SKBitmap a, double[] r, out double media, out int p999)
        {
            int[] hist = new int[256]; double suma = 0; int n = 0;
            for (int y = 3; y < ThumbPx - 3; y++)
            {
                byte* pa = (byte*)a.GetPixels() + (long)y * a.RowBytes;
                for (int x = 12; x < (ThumbPx - 3) * 4; x++) { double d = Math.Abs(pa[x] - r[(y * ThumbPx) * 4 + x]); suma += d; n++; hist[Math.Min(255, (int)Math.Round(d))]++; }
            }
            media = suma / n; p999 = DPercentil(hist, n, 0.999);
        }
        private static int DPercentil(int[] h, int n, double f) { long c = 0, k = (long)Math.Ceiling(n * f); for (int i = 0; i < 256; i++) { c += h[i]; if (c >= k) return i; } return 255; }
        private static double DPct(List<double> l, double f) { if (l.Count == 0) return double.NaN; l.Sort(); return l[Math.Min(l.Count - 1, (int)(l.Count * f))]; }

        private static int BancoDct(string[] a)
        {
            if (a.Length < 1) { Console.Error.WriteLine("uso: --banco-dct LISTA [VECES,VECES...] [SALIDA]"); return 2; }
            List<string> rutas = new List<string>();
            foreach (string l in File.ReadAllLines(a[0])) if (l.Trim().Length > 0) rutas.Add(l.Trim());
            List<double> veces = new List<double>();
            foreach (string v in (a.Length >= 2 ? a[1] : "1.5,2,3,4").Split(',')) veces.Add(double.Parse(v, CultureInfo.InvariantCulture));
            StreamWriter salida = a.Length >= 3 ? new StreamWriter(a[2]) : null;
            CultureInfo ci = CultureInfo.InvariantCulture;
            List<double> tEntera = new List<double>(), refE = new List<double>(), refEp = new List<double>();
            Dictionary<double, List<double>> refR = new Dictionary<double, List<double>>(), refRp = new Dictionary<double, List<double>>(), refEv = new Dictionary<double, List<double>>(), refEvp = new Dictionary<double, List<double>>();
            Dictionary<double, List<double>> tRed = new Dictionary<double, List<double>>(), tEnt = new Dictionary<double, List<double>>(), medias = new Dictionary<double, List<double>>(), p999s = new Dictionary<double, List<double>>(), p99s = new Dictionary<double, List<double>>();
            Dictionary<double, int> maxes = new Dictionary<double, int>(), dentro = new Dictionary<double, int>(), aplicadas = new Dictionary<double, int>(); Dictionary<double, long> s16 = new Dictionary<double, long>();
            foreach (double v in veces) { tRed[v] = new List<double>(); tEnt[v] = new List<double>(); medias[v] = new List<double>(); p999s[v] = new List<double>(); p99s[v] = new List<double>(); maxes[v] = dentro[v] = aplicadas[v] = 0; s16[v] = 0; refR[v] = new List<double>(); refRp[v] = new List<double>(); refEv[v] = new List<double>(); refEvp[v] = new List<double>(); }
            int jpeg = 0;
            foreach (string ruta in rutas)
            {
                int largo; byte[] dd = LeeEntero(ruta, out largo); byte[] data = new byte[largo]; Buffer.BlockCopy(dd, 0, data, 0, largo);
                if (largo < 4 || data[0] != 0xFF || data[1] != 0xD8) continue;
                jpeg++;
                string tipo; using (SKData sd0 = SKData.CreateCopy(data)) using (SKCodec c0 = SKCodec.Create(sd0)) tipo = c0 == null ? "?" : c0.Info.ColorType.ToString();
                FormatoImagen f;
                long t0 = Stopwatch.GetTimestamp();
                SKBitmap src; using (MemoryStream ms = new MemoryStream(data, 0, largo, false)) src = Imagenes.Lee(ms, out f);
                SKBitmap entera = DReduce(src);
                double msEntera = BMs(Stopwatch.GetTimestamp() - t0);
                int w = src.Width, h = src.Height;
                double[] refe = DReferencia(src); src.Dispose();
                double rmE; int rpE; DContraRef(entera, refe, out rmE, out rpE);
                tEntera.Add(msEntera); refE.Add(rmE); refEp.Add(rpE);
                foreach (double v in veces)
                {
                    long t1 = Stopwatch.GetTimestamp();
                    FormatoImagen fr; SKBitmap red = RocolaOsLeeJpegReducida(data, largo, (int)Math.Round(v * ThumbPx), out fr);
                    if (red == null) continue;
                    int rw = red.Width, rh = red.Height;
                    SKBitmap mini = DReduce(red); red.Dispose();
                    double ms2 = BMs(Stopwatch.GetTimestamp() - t1);
                    double media; int p99, p999, max, maxD, so;
                    DDiferencia(entera, mini, out media, out p99, out p999, out max, out maxD, out so);
                    double rmR; int rpR; DContraRef(mini, refe, out rmR, out rpR);
                    mini.Dispose();
                    refR[v].Add(rmR); refRp[v].Add(rpR); refEv[v].Add(rmE); refEvp[v].Add(rpE);
                    aplicadas[v]++; tRed[v].Add(ms2); tEnt[v].Add(msEntera); medias[v].Add(media); p999s[v].Add(p999); p99s[v].Add(p99);
                    maxes[v] = Math.Max(maxes[v], max); dentro[v] = Math.Max(dentro[v], maxD); s16[v] += so;
                    if (salida != null) salida.WriteLine(Path.GetFileName(Path.GetDirectoryName(ruta)) + ":" + tipo + "\t" + v.ToString(ci) + "\t" + w + "x" + h + "\t" + rw + "x" + rh + "\t" + msEntera.ToString("0.00", ci) + "\t" + ms2.ToString("0.00", ci)
                        + "\t" + media.ToString("0.000", ci) + "\t" + p99 + "\t" + p999 + "\t" + max + "\t" + maxD + "\t" + so + "\t" + rmE.ToString("0.000", ci) + "\t" + rpE + "\t" + rmR.ToString("0.000", ci) + "\t" + rpR);
                }
                entera.Dispose();
            }
            if (salida != null) salida.Dispose();
            Console.WriteLine("jpeg=" + jpeg + " entera(todas) p50=" + DPct(tEntera, .5).ToString("0.00", ci) + " ms");
            Console.WriteLine("veces\tn\tentera p50/p95 (ms)\treducida p50/p95 (ms)\tmedia: mediana/peor\tp99: mediana/peor\tp99.9: mediana/peor\tmax\tmax sin marco\tpuntos>16 (de " + (ThumbPx * ThumbPx * 4) + " por portada)");
            foreach (double v in veces)
                Console.WriteLine(v.ToString(ci) + "\t" + aplicadas[v] + "\t" + DPct(tEnt[v], .5).ToString("0.0", ci) + "/" + DPct(tEnt[v], .95).ToString("0.0", ci) + "\t" + DPct(tRed[v], .5).ToString("0.0", ci) + "/" + DPct(tRed[v], .95).ToString("0.0", ci)
                    + "\t" + DPct(medias[v], .5).ToString("0.000", ci) + "/" + DPct(medias[v], 1).ToString("0.000", ci) + "\t" + DPct(p99s[v], .5) + "/" + DPct(p99s[v], 1) + "\t" + DPct(p999s[v], .5) + "/" + DPct(p999s[v], 1)
                    + "\t" + maxes[v] + "\t" + dentro[v] + "\t" + s16[v]
                    + "\tcontra la referencia: XP media " + DPct(refEv[v], .5).ToString("0.000", ci) + " p99.9 " + DPct(refEvp[v], .5) + "/" + DPct(refEvp[v], 1) + ", DCT media " + DPct(refR[v], .5).ToString("0.000", ci) + " p99.9 " + DPct(refRp[v], .5) + "/" + DPct(refRp[v], 1));
            return 0;
        }
    }
}
