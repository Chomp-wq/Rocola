// PIXELES-IGUAL (rapido-caratulas, 20-11-2026): Pixeles.Lee y Pixeles.Escribe de ahora (Nativo/Pixeles.cs) contra los de la base, con mapas
// de muchas clases, en los tres formatos, con anchos que no son multiplo de cuatro y trozos corridos: los mismos bytes. Despues, lo que tarda
// cada uno con lo que sube la rocola (una portada opaca de 256x256, la interfaz de 1024x768 casi toda transparente, un mapa al azar).
using System; using System.Diagnostics; using System.Drawing; using SkiaSharp;
using Rocola.Nativo; using V = Rocola.NativoViejo;
static unsafe class Prueba
{
    static int casos, mal;
    static SKBitmap Mapa(int w, int h, int clase, Random r)
    {
        SKBitmap b = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
        if (clase == 6)
        {
            // como una capa de la interfaz de verdad: transparente, con texto suavizado, pastillas redondeadas con sombra difuminada y degradados
            b.Erase(SKColors.Transparent);
            using (SKCanvas cv = new SKCanvas(b))
            using (SKFont f = new SKFont(SKTypeface.Default, 22))
            using (SKPaint t = new SKPaint { IsAntialias = true, Color = SKColors.White })
            using (SKPaint sombra = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 140), ImageFilter = SKImageFilter.CreateBlur(6, 6) })
            using (SKPaint caja = new SKPaint { IsAntialias = true })
            {
                for (int i = 0; i < 18; i++)
                {
                    float x = 20 + (i % 6) * 165, y = 40 + (i / 6) * 230;
                    cv.DrawRoundRect(new SKRect(x + 4, y + 6, x + 154, y + 206), 14, 14, sombra);
                    caja.Shader = SKShader.CreateLinearGradient(new SKPoint(x, y), new SKPoint(x, y + 200), new[] { new SKColor(40, 60, 120, 200), new SKColor(10, 10, 30, 230) }, SKShaderTileMode.Clamp);
                    cv.DrawRoundRect(new SKRect(x, y, x + 150, y + 200), 14, 14, caja);
                    cv.DrawText("Artista " + i, x + 10, y + 170, f, t);
                    cv.DrawText("Disco de prueba", x + 10, y + 195, f, t);
                }
                for (int k = 0; k < 12; k++) cv.DrawText("LETRA ABCDEFGHIJKLMNOPQRSTUVWXYZ " + k, 30, 720 + (k % 2) * 24, f, t);
            }
            return b;
        }
        uint* p = (uint*)b.GetPixels(); int paso = b.RowBytes / 4;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                uint a;
                switch (clase)
                {
                    case 0: a = 255; break;                                         // opaca (una portada)
                    case 1: a = 0; break;                                           // transparente
                    case 2: a = (uint)r.Next(256); break;                           // al azar
                    case 3: a = (x / 7 + y / 3) % 5 == 0 ? (uint)r.Next(1, 255) : ((x + y) % 2 == 0 ? 255u : 0u); break;   // casi todo 0/255, algo a medias
                    case 4: a = (x * 13 + y * 7) % 23 == 0 ? 128u : 255u; break;     // opaca con puntos sueltos a medias
                    default: a = r.Next(10) == 0 ? (uint)r.Next(256) : 0; break;    // la interfaz: casi todo transparente
                }
                uint c0 = (uint)r.Next(256), c1 = (uint)r.Next(256), c2 = (uint)r.Next(256);
                if (r.Next(3) != 0) { c0 = c0 * a / 255; c1 = c1 * a / 255; c2 = c2 * a / 255; }   // premultiplicado (y a veces no: que la tabla vea de todo)
                p[(long)y * paso + x] = (a << 24) | (c2 << 16) | (c1 << 8) | c0;
            }
        b.NotifyPixelsChanged();
        return b;
    }
    static void Compara(string que, byte[] a, byte[] b)
    {
        casos++;
        if (a.Length != b.Length) { mal++; Console.WriteLine("DISTINTO " + que + ": largo"); return; }
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) { mal++; Console.WriteLine("DISTINTO " + que + " en el byte " + i); return; }
    }
    static byte[] Lee(bool nuevo, SKBitmap b, Rectangle z, int f, int paso)
    {
        byte[] d = new byte[paso * z.Height + 16];
        fixed (byte* p = d)
            if (nuevo) Pixeles.Lee(b, z, (FormatoPuntos)f, (IntPtr)p, paso); else V.Pixeles.Lee(b, z, (V.FormatoPuntos)f, (IntPtr)p, paso);
        return d;
    }
    static byte[] Escribe(bool nuevo, SKBitmap b0, Rectangle z, int f, byte[] origen, int paso)
    {
        using (SKBitmap b = b0.Copy())
        {
            fixed (byte* p = origen)
                if (nuevo) Pixeles.Escribe(b, z, (FormatoPuntos)f, (IntPtr)p, paso); else V.Pixeles.Escribe(b, z, (V.FormatoPuntos)f, (IntPtr)p, paso);
            byte[] t = new byte[b.RowBytes * b.Height]; System.Runtime.InteropServices.Marshal.Copy(b.GetPixels(), t, 0, t.Length); return t;
        }
    }
    static int Main()
    {
        Random r = new Random(20261120);
        int[] anchos = { 1, 3, 4, 5, 7, 8, 9, 16, 31, 64, 257 };
        foreach (int w in anchos)
            for (int clase = 0; clase < 6; clase++)
            {
                int h = 1 + (w % 5) + clase;
                using (SKBitmap b = Mapa(w, h, clase, r))
                    foreach (Rectangle z in new[] { new Rectangle(0, 0, w, h), new Rectangle(Math.Min(1, w - 1), Math.Min(1, h - 1), Math.Max(1, w - 1), Math.Max(1, h - 1)), new Rectangle(w / 2, 0, w - w / 2, h) })
                        for (int f = 0; f < 3; f++)
                        {
                            int paso = Pixeles.Paso((FormatoPuntos)f, z.Width) + (f == 0 ? 8 : 0);
                            string que = "w=" + w + " clase=" + clase + " zona=" + z + " formato=" + f;
                            Compara("Lee " + que, Lee(true, b, z, f, paso), Lee(false, b, z, f, paso));
                            byte[] origen = Lee(false, b, z, f, paso);
                            for (int i = 0; i < origen.Length; i++) if (r.Next(9) == 0) origen[i] = (byte)r.Next(256);   // y puntos que no salen de un mapa premultiplicado
                            Compara("Escribe " + que, Escribe(true, b, z, f, origen, paso), Escribe(false, b, z, f, origen, paso));
                        }
            }
        using (SKBitmap b = Mapa(1024, 768, 6, r))   // y la interfaz dibujada, entera y en un trozo corrido, en los tres formatos
            foreach (Rectangle z in new[] { new Rectangle(0, 0, 1024, 768), new Rectangle(3, 5, 1001, 700) })
                for (int f = 0; f < 3; f++)
                {
                    int paso = Pixeles.Paso((FormatoPuntos)f, z.Width);
                    Compara("Lee dibujada " + z + " formato=" + f, Lee(true, b, z, f, paso), Lee(false, b, z, f, paso));
                    byte[] origen = Lee(false, b, z, f, paso);
                    Compara("Escribe dibujada " + z + " formato=" + f, Escribe(true, b, z, f, origen, paso), Escribe(false, b, z, f, origen, paso));
                }
        Console.WriteLine("pixeles-igual: " + casos + " comparaciones, " + mal + " distintas");
        // lo que tarda: mediana de 41 vueltas de cada uno, alternando
        foreach (var caso in new[] { (256, 256, 0, "portada opaca 256x256"), (1024, 768, 5, "interfaz 1024x768 casi transparente"), (1024, 768, 3, "1024x768 0/255 con algo a medias"), (256, 256, 2, "256x256 al azar (lo peor)"), (1024, 768, 6, "interfaz 1024x768 dibujada (texto, sombras, degradados)") })
            using (SKBitmap b = Mapa(caso.Item1, caso.Item2, caso.Item3, r))
            {
                int paso = caso.Item1 * 4; IntPtr d = System.Runtime.InteropServices.Marshal.AllocHGlobal(paso * caso.Item2);
                Rectangle z = new Rectangle(0, 0, caso.Item1, caso.Item2);
                double[] tn = new double[41], tv = new double[41];
                for (int k = 0; k < 41; k++)
                {
                    long t0 = Stopwatch.GetTimestamp(); V.Pixeles.CopiaSinPremultiplicar(b, z, d, paso); long t1 = Stopwatch.GetTimestamp();
                    Pixeles.CopiaSinPremultiplicar(b, z, d, paso); long t2 = Stopwatch.GetTimestamp();
                    tv[k] = (t1 - t0) * 1000.0 / Stopwatch.Frequency; tn[k] = (t2 - t1) * 1000.0 / Stopwatch.Frequency;
                }
                Array.Sort(tn); Array.Sort(tv);
                Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "  {0}: antes {1:0.000} ms, ahora {2:0.000} ms (medianas de 41)", caso.Item4, tv[20], tn[20]));
                System.Runtime.InteropServices.Marshal.FreeHGlobal(d);
            }
        return mal == 0 && casos > 0 ? 0 : 1;
    }
}
