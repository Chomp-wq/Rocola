// BANCO DE VISTAS: los hornos de XP (los mapas que se hacen una vez y se suben a la tarjeta) con datos fijos, y la huella de los puntos de
// cada uno. Se compila con el programa de una carpeta Xp (BancoVistas.csproj); la salida de la referencia congelada y la de la copia de
// trabajo tienen que ser iguales linea por linea. "vuelca CARPETA" deja ademas cada mapa en PNG.
// Los hornos traducidos (D4) devuelven un SKBitmap o, si los sube D3D9Star.cs, el Bitmap del puente; los de la referencia, un Bitmap de
// la capa: se miran igual (los puntos tal cual y como los sube la tarjeta, LockBits Format32bppArgb). Ademas, el signo de la ayuda
// (PintaInterrogacion) pintado sobre un fondo con distintos estados del Graphics: en la referencia con GDI+, en la traduccion como lo
// llama el dibujo por procesador (RenderContent), con el lienzo y su estado por el puente.
//   ROCOLA_PANTALLA=fuera:64x64 dotnet bin/Release/net10.0/BancoVistas.dll [vuelca CARPETA]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Text;
using Rocola;
using SkiaSharp;

namespace Banco
{
    internal static class Hornos
    {
        private static string carpeta;
        private static int n;
        private static unsafe void Apunta(string nombre, object o)
        {
            n++;
            if (o == null) { Console.WriteLine(nombre + "\tnull"); return; }
            // un SKBitmap de un horno traducido: lo que sube la tarjeta es lo mismo que con un Bitmap Format32bppArgb de esos puntos
            object b = Pru.Aprecia(o, "Format32bppArgb");
            // los puntos tal cual (premultiplicados) y como los sube la tarjeta (LockBits Format32bppArgb: sin premultiplicar)
            SKBitmap s = Pru.Sk(b); byte[] px = new byte[s.Width * s.Height * 4];
            byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            byte[] sub = Pru.SinPremultiplicar(b);
            Console.WriteLine(nombre + "\t" + s.Width + "x" + s.Height + "\t" + Pru.Formato(b) + "\t" + s.ColorType + "/" + s.AlphaType + "\t" + Huella(px) + "\t" + Huella(sub));
            if (carpeta != null) using (SKData png = s.Encode(SKEncodedImageFormat.Png, 100)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre + ".png"), png.ToArray());
            Pru.Suelta(b);
        }
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static object Privado(Type t, string metodo, params object[] a)
        {
            MethodInfo m = t.GetMethod(metodo, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            return m.Invoke(null, a);
        }
        // el signo de la ayuda sobre un fondo, con ese estado del Graphics (como el dibujo por procesador de la casilla, RenderContent)
        private static void Interrogacion(string nombre, ModoSuave s, ModoPixel po, ModoCopia cm, int trans, RectangleF r, float alfa)
        {
            object b = Pru.NuevoMapa(64, 64, "Format32bppPArgb");
            using (Lz g = Pru.Lienzo(b))
            {
                g.Limpia(Color.FromArgb(120, 30, 60, 90));
                g.Rellena(Color.FromArgb(200, 250, 20, 60), 0, 20, 64, 9);
                g.Suavizado = s; g.Pixel = po; g.Copia = cm;
                if (trans == 1) g.Traslada(3.5f, 2.25f); else if (trans == 2) g.Escala(1.3f, 1.3f); else if (trans == 3) g.Gira(7f);
                BindingFlags bf = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
                MethodInfo m = typeof(Form1).GetMethod("PintaInterrogacion", bf);
                if (Pru.Capa) m.Invoke(null, new object[] { g.Objeto, r, alfa });
                else
                {
                    // como lo llama RenderContent (AyudaCasillaGpu.cs) traducido: el lienzo y su estado
                    object[] e = g.Estado();
                    m.Invoke(null, new object[] { e[0], e[1], e[5], e[3], r, alfa });
                }
            }
            Apunta(nombre, b);
        }
        public static int Main(string[] a)
        {
            for (int i = 0; i + 1 < a.Length; i += 2) if (a[i] == "vuelca") { carpeta = a[i + 1]; System.IO.Directory.CreateDirectory(carpeta); }
            // la estrella 2D (EstrellaGpu.cs) y la luna con detalle (LunaGpu.cs)
            Apunta("BakeStar-sencilla", GpuPanel.BakeStar(false));
            Apunta("BakeStar-detallada", GpuPanel.BakeStar(true));
            Apunta("BakeDetailedMoon", GpuPanel.BakeDetailedMoon());
            // el cielo profundo (CieloNaturalGpu.cs)
            foreach (int[] m in new[] { new[] { 256, 192 }, new[] { 100, 37 }, new[] { 1, 1 }, new[] { 333, 2 } }) Apunta("BakeNatural-" + m[0] + "x" + m[1], GpuPanel.BakeNatural(m[0], m[1]));
            // la luna del cielo vivo (CieloVivoGpu.cs)
            foreach (int lado in new[] { 4, 8, 31, 64, 128 }) foreach (double fase in new[] { 0.0, 0.25, 0.5, 0.73, 0.999 }) Apunta("HorneaLuna-" + lado + "-" + fase.ToString(System.Globalization.CultureInfo.InvariantCulture), Form1.HorneaLuna(lado, fase));
            // el degradado tramado de la interfaz (PulidoGpu.cs)
            Random r = new Random(5);
            for (int k = 0; k < 12; k++)
            {
                int alto = k == 0 ? 1 : k == 1 ? 2 : r.Next(3, 700);
                Color c0 = Color.FromArgb(r.Next(256), r.Next(256), r.Next(256), r.Next(256)), c1 = Color.FromArgb(k % 3 == 0 ? 255 : r.Next(256), r.Next(256), r.Next(256), r.Next(256));
                Apunta("HorneaMapaDegradado-" + k + "-" + alto, Privado(typeof(Form1), "HorneaMapaDegradado", alto, c0, c1));
            }
            // el grano (Form1.Fondo.cs)
            Apunta("BakeRuido", Form1.BakeRuido());
            foreach (int lado in new[] { 1024, 512, 100, 64, 1 }) Apunta("BakeRuidoGrande-" + lado, Form1.BakeRuidoGrande(lado));
            // la interrogacion de la ayuda (AyudaCasillaGpu.cs)
            foreach (int lado in new[] { 8, 16, 37, 64, 129, 256 }) Apunta("BakeInterrogacion-" + lado, GpuPanel.BakeInterrogacion(lado));
            // el signo de la ayuda con el estado del que lo llama
            int k2 = 0;
            foreach (object[] e in new[] {
                new object[] { ModoSuave.AntiAlias, ModoPixel.Default, ModoCopia.SourceOver, 0 },
                new object[] { ModoSuave.None, ModoPixel.Default, ModoCopia.SourceOver, 0 },
                new object[] { ModoSuave.AntiAlias, ModoPixel.Half, ModoCopia.SourceOver, 1 },
                new object[] { ModoSuave.HighQuality, ModoPixel.HighQuality, ModoCopia.SourceCopy, 2 },
                new object[] { ModoSuave.AntiAlias, ModoPixel.Default, ModoCopia.SourceCopy, 3 },
                new object[] { ModoSuave.None, ModoPixel.Half, ModoCopia.SourceOver, 1 } })
                foreach (RectangleF r2 in new[] { new RectangleF(11, 5, 30, 30), new RectangleF(2.5f, 3.25f, 17.3f, 17.3f), new RectangleF(0, 0, 3, 3), new RectangleF(20.7f, 30.1f, 40.2f, 25f) })
                    foreach (float alfa in new[] { 1f, 0.5f, 0f })
                        Interrogacion("PintaInterrogacion-" + (k2++), (ModoSuave)e[0], (ModoPixel)e[1], (ModoCopia)e[2], (int)e[3], r2, alfa);
            Console.Error.WriteLine("hornos: " + n + " mapas");
            return 0;
        }
    }
}
