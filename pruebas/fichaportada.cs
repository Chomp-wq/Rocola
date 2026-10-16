using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

// Regresion del DrawImage de la ficha visto en XP. Datos sinteticos aislados;
// no abre la rocola, Direct3D ni audio. Ejecutar tambien en Windows/XP para su GDI+ real.
class FichaPortada
{
    const BindingFlags B = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type form;
    static int checks, failures;
    static void Check(bool ok, string message)
    { checks++; if (!ok) failures++; Console.WriteLine((ok ? "OK " : "FALLO ") + message); }
    static object Call(string method, params object[] args)
    { return form.GetMethod(method, B).Invoke(null, args); }
    static void Paint(Bitmap dest, Image cover)
    {
        Call("PintaFicha", dest, cover, "Artista", "Disco", "12 canciones", "Rock", Color.CornflowerBlue,
            2, true, new string[] { "Primera", "Segunda", "Tercera" }, new string[] { "3:10", "4:20", "2:50" },
            new int[] { 3, 2, 1 }, 12, FontFamily.GenericSansSerif);
    }
    static byte[] Pixels(Bitmap bitmap)
    {
        BitmapData d = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            byte[] result = new byte[bitmap.Width * bitmap.Height * 4];
            for (int y = 0; y < bitmap.Height; y++)
                System.Runtime.InteropServices.Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride),
                    result, y * bitmap.Width * 4, bitmap.Width * 4);
            return result;
        }
        finally { bitmap.UnlockBits(d); }
    }
    static bool Equal(byte[] a, byte[] b)
    { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    static Bitmap Pattern()
    {
        Bitmap b = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
        for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++)
            b.SetPixel(x, y, Color.FromArgb(y, x, 255 - x, (x + y) % 256));
        return b;
    }
    static void Cached(string dir, bool jpeg)
    {
        form.GetField("caratulasJpeg", B).SetValue(null, jpeg);
        string cache = Path.Combine(dir, jpeg ? "cache-jpeg.png" : "cache-alfa.png");
        string stored = jpeg ? (string)Call("MiniaturaJpeg", cache) : cache;
        using (Bitmap b = Pattern()) b.Save(stored, jpeg ? ImageFormat.Jpeg : ImageFormat.Png);
        byte[] before;
        using (Bitmap decoded = new Bitmap(stored)) before = Pixels(decoded);
        // Sin original: solo puede tener exito si efectivamente leyo la miniatura guardada.
        object[] args = { Path.Combine(dir, "original-ausente.jpg"), cache, null };
        using (Bitmap cover = (Bitmap)form.GetMethod("DecodeCoverForPublication", B).Invoke(null, args))
        {
            Check(cover != null && args[2] == null, "lee cache " + (jpeg ? "JPEG" : "PNG transparente") + " sin original");
            if (cover == null) return;
            // El mismo hilo reutiliza el arreglo que alimentaba al stream; destruirlo por completo.
            byte[] reused = (byte[])Call("LeeEntero", stored, 0);
            Array.Clear(reused, 0, reused.Length);
            File.Delete(stored);
            GC.Collect(); GC.WaitForPendingFinalizers();
            Check(Equal(before, Pixels(cover)), "pixeles y alfa intactos despues de cerrar stream y sobrescribir bytes");
            using (Bitmap card = new Bitmap(340, 230, PixelFormat.Format32bppPArgb))
            { Paint(card, cover); Check(card.GetPixel(100, 100).A > 0, "DrawImage posterior completa la ficha"); }
        }
    }
    static void InvalidCover()
    {
        using (Bitmap absent = new Bitmap(340, 230, PixelFormat.Format32bppPArgb))
        using (Bitmap invalid = new Bitmap(340, 230, PixelFormat.Format32bppPArgb))
        {
            Paint(absent, null);
            Bitmap retired = new Bitmap(20, 20); retired.Dispose();
            bool completed = true;
            try { Paint(invalid, retired); }
            catch (TargetInvocationException e) { completed = false; Console.WriteLine(e.InnerException); }
            Check(completed, "portada invalida no propaga la excepcion al bucle de pantalla");
            Check(Equal(Pixels(absent), Pixels(invalid)), "se restaura el clip y se dibuja toda la ficha igual que sin portada");
        }
    }
    static void Dpi()
    {
        using (Bitmap cover = Pattern())
        using (Bitmap a = new Bitmap(340, 230, PixelFormat.Format32bppPArgb))
        using (Bitmap b = new Bitmap(340, 230, PixelFormat.Format32bppPArgb))
        {
            cover.SetResolution(96, 96); Paint(a, cover);
            cover.SetResolution(300, 72); Paint(b, cover);
            Check(Equal(Pixels(a), Pixels(b)), "el dibujo usa todo el bitmap y no depende del DPI");
        }
    }
    static int Main(string[] args)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rocola-fichaportada-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir); Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            form = Assembly.LoadFrom(args[0]).GetType("Rocola.Form1", true);
            if (form.GetMethod("PintaFicha", B) == null)
            {
                Check(form.GetMethod("CopiaMiniaturaGpu", B) == null, "perfil CPU conserva su ruta sin cambios GPU");
                return failures;
            }
            InvalidCover(); Dpi(); Cached(dir, false); Cached(dir, true);
        }
        catch (Exception e) { failures++; Console.WriteLine("FALLO " + e); }
        finally { Directory.Delete(dir, true); }
        Console.WriteLine(checks + " comprobaciones, " + failures + " fallos");
        return failures == 0 ? 0 : 1;
    }
}
