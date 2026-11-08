// BANCO DE VISTAS: los hornos de XP (los mapas que se hacen una vez y se suben a la tarjeta) con datos fijos, y la huella de los puntos de
// cada uno. Se compila con el programa de una carpeta Xp (BancoVistas.csproj); la salida de la referencia congelada y la de la copia de
// trabajo tienen que ser iguales linea por linea. "vuelca CARPETA" deja ademas cada mapa en PNG.
//   ROCOLA_PANTALLA=fuera:64x64 dotnet bin/Release/net10.0/BancoVistas.dll [vuelca CARPETA]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Text;
using Rocola;
using Rocola.Dibujo2D;
using SkiaSharp;

namespace Banco
{
    internal static class Hornos
    {
        private static string carpeta;
        private static int n;
        private static unsafe void Apunta(string nombre, Bitmap b)
        {
            n++;
            if (b == null) { Console.WriteLine(nombre + "\tnull"); return; }
            // los puntos tal cual (premultiplicados) y como los sube la tarjeta (LockBits Format32bppArgb: sin premultiplicar)
            SKBitmap s = b.Sk; byte[] px = new byte[s.Width * s.Height * 4];
            byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            BitmapData d = b.LockBits(new Rectangle(0, 0, s.Width, s.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] sub = new byte[s.Width * s.Height * 4];
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) sub[y * s.Width * 4 + x] = ((byte*)d.Scan0)[y * d.Stride + x];
            b.UnlockBits(d);
            Console.WriteLine(nombre + "\t" + s.Width + "x" + s.Height + "\t" + b.PixelFormat + "\t" + Huella(px) + "\t" + Huella(sub));
            if (carpeta != null) using (SKData png = s.Encode(SKEncodedImageFormat.Png, 100)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre + ".png"), png.ToArray());
            b.Dispose();
        }
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static Bitmap Privado(Type t, string metodo, params object[] a)
        {
            MethodInfo m = t.GetMethod(metodo, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            return (Bitmap)m.Invoke(null, a);
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
            Console.Error.WriteLine("hornos: " + n + " mapas");
            return 0;
        }
    }
}
