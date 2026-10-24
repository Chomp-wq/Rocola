using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;

// LAS CARATULAS ENORMES, DE UNA EN UNA (24-10-2026).
//
// Una portada original se lee entera y GDI+ la descomprime ENTERA para reducirla a la miniatura: un JPEG de 8000x8000 pesa
// menos de 1 MB en el disco y pide 256 MB al decodificarse (medido aqui con Mono: +258 MB). Los lectores de caratulas y el
// calentador de miniaturas trabajan a la vez, asi que dos o tres portadas asi pedian el doble o el triple de golpe. En la
// rocola de XP (32 bits, 2 GB de direcciones, 1,3 GB libres en el apunte del 23-10 con el calentador en marcha) eso puede
// dejar sin memoria a la pantalla o a la tarjeta en ese momento. Ahora las enormes (mas de 16 millones de pixeles) se
// decodifican de una en una; las normales siguen a la vez y sin esperar. La miniatura sale igual, pixel a pixel.
//
// Se mide la memoria del proceso con un hilo que la mira cada 2 ms (libgdiplus devuelve al sistema lo que suelta): es
// una medida de Mono/libgdiplus en Linux, no del GDI+ de XP, que tambien descomprime la imagen entera.
class CaratulaEnorme
{
    static int fallos, oks;
    static void Exige(bool c, string q) { if (c) { oks++; Console.WriteLine("  OK    " + q); } else { fallos++; Console.WriteLine("  FALLO " + q); } }
    static MethodInfo decodifica;
    static Image Decodifica(string ruta) { return (Image)decodifica.Invoke(null, new object[] { ruta, null }); }

    static string Crea(string dir, string nombre, int lado, Color c)
    {
        string ruta = Path.Combine(dir, nombre);
        using (Bitmap b = new Bitmap(lado, lado, PixelFormat.Format24bppRgb))
        {
            using (Graphics g = Graphics.FromImage(b)) { g.Clear(c); using (SolidBrush br = new SolidBrush(Color.White)) g.FillEllipse(br, lado / 4, lado / 4, lado / 2, lado / 2); }
            b.Save(ruta, ImageFormat.Jpeg);
        }
        return ruta;
    }

    // El pico de memoria del proceso por encima de la de antes, mientras corre 'trabajo'.
    static volatile bool midiendo;
    static long MideMB(ThreadStart trabajo)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Process p = Process.GetCurrentProcess(); p.Refresh(); long base0 = p.WorkingSet64, pico = base0;
        midiendo = true;
        Thread mira = new Thread(delegate () { Process q = Process.GetCurrentProcess(); while (midiendo) { q.Refresh(); long w = q.WorkingSet64; if (w > pico) pico = w; Thread.Sleep(2); } });
        mira.Start();
        trabajo();
        midiendo = false; mira.Join();
        return (pico - base0) / (1024 * 1024);
    }

    static byte[] Pixeles(Image im)
    {
        Bitmap b = (Bitmap)im; Rectangle r = new Rectangle(0, 0, b.Width, b.Height);
        BitmapData d = b.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try { byte[] x = new byte[d.Stride * d.Height]; System.Runtime.InteropServices.Marshal.Copy(d.Scan0, x, 0, x.Length); return x; }
        finally { b.UnlockBits(d); }
    }
    static bool Iguales(byte[] a, byte[] b) { if (a == null || b == null || a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }

    static int Main(string[] args)
    {
        Type f = Assembly.LoadFrom(args[0]).GetType("Rocola.Form1", true);
        decodifica = f.GetMethod("DecodeCoverWithIdentity", BindingFlags.Static | BindingFlags.NonPublic);
        if (decodifica == null) { Console.WriteLine("  FALLO no esta DecodeCoverWithIdentity"); return 1; }
        string dir = Path.Combine(Path.GetTempPath(), "caratulaenorme_" + Process.GetCurrentProcess().Id);
        Directory.CreateDirectory(dir);
        try
        {
            string a = Crea(dir, "a.jpg", 8000, Color.SteelBlue), b = Crea(dir, "b.jpg", 8000, Color.DarkRed), chica = Crea(dir, "chica.jpg", 600, Color.SeaGreen);
            Console.WriteLine("  (dos JPEG de 8000x8000: " + new FileInfo(a).Length / 1024 + " y " + new FileInfo(b).Length / 1024 + " KB en el disco)");

            // 1) Una sola: lo que cuesta y la miniatura de referencia.
            byte[] refA = null, refB = null, refChica = null;
            long una = MideMB(delegate { using (Image im = Decodifica(a)) refA = Pixeles(im); });
            using (Image im = Decodifica(b)) refB = Pixeles(im);
            using (Image im = Decodifica(chica)) refChica = Pixeles(im);
            Console.WriteLine("  (una enorme sola: +" + una + " MB)");
            Exige(una >= 150, "decodificar una portada de 8000x8000 pide la imagen entera (+" + una + " MB; se esperan unos 256)");

            // 2) Dos enormes a la vez, como dos lectores de caratulas: de una en una, no el doble.
            byte[] sa = null, sb = null; Exception ea = null, eb = null;
            long dos = MideMB(delegate
            {
                ManualResetEvent ya = new ManualResetEvent(false);
                Thread t1 = new Thread(delegate () { ya.WaitOne(); try { using (Image im = Decodifica(a)) sa = Pixeles(im); } catch (Exception e) { ea = e; } });
                Thread t2 = new Thread(delegate () { ya.WaitOne(); try { using (Image im = Decodifica(b)) sb = Pixeles(im); } catch (Exception e) { eb = e; } });
                t1.Start(); t2.Start(); ya.Set(); t1.Join(); t2.Join();
            });
            Console.WriteLine("  (dos enormes a la vez: +" + dos + " MB)");
            Exige(ea == null && eb == null, "las dos se decodifican sin error");
            Exige(dos < una * 3 / 2, "dos enormes a la vez no piden el doble: +" + dos + " MB contra +" + una + " MB de una sola");
            Exige(Iguales(sa, refA) && Iguales(sb, refB), "las dos miniaturas salen pixel a pixel como decodificadas solas");

            // 3) Una normal mientras una enorme se decodifica: no espera a la enorme.
            long msChica = -1, msEnorme = -1; byte[] sChica = null;
            Thread grande = new Thread(delegate () { Stopwatch w = Stopwatch.StartNew(); using (Image im = Decodifica(a)) { } msEnorme = w.ElapsedMilliseconds; });
            grande.Start();
            Thread.Sleep(150);   // la enorme ya va por dentro
            Stopwatch sw = Stopwatch.StartNew();
            using (Image im = Decodifica(chica)) sChica = Pixeles(im);
            msChica = sw.ElapsedMilliseconds;
            grande.Join();
            Console.WriteLine("  (la normal tardo " + msChica + " ms con la enorme en marcha, que tardo " + msEnorme + " ms)");
            Exige(msChica < msEnorme, "una portada normal no espera a que termine la enorme");
            Exige(Iguales(sChica, refChica), "y sale igual que sola");

            // 4) La medida de la cabecera, la que decide si espera turno, en los formatos que guarda GDI+.
            MethodInfo medida = f.GetMethod("MedidaDeCabecera", BindingFlags.Static | BindingFlags.NonPublic);
            ImageFormat[] fmts = { ImageFormat.Jpeg, ImageFormat.Png, ImageFormat.Gif, ImageFormat.Bmp };
            string[] nombres = { "JPEG", "PNG", "GIF", "BMP" };
            for (int k = 0; k < fmts.Length; k++)
            {
                byte[] bytes;
                using (Bitmap bm = new Bitmap(1234, 567, PixelFormat.Format24bppRgb))
                using (MemoryStream m = new MemoryStream()) { using (Graphics g = Graphics.FromImage(bm)) g.Clear(Color.Orange); bm.Save(m, fmts[k]); bytes = m.ToArray(); }
                object[] p = { bytes, bytes.Length, 0, 0 };
                bool ok = (bool)medida.Invoke(null, p);
                Exige(ok && (int)p[2] == 1234 && (int)p[3] == 567, nombres[k] + ": la cabecera dice 1234x567 (" + (ok ? p[2] + "x" + p[3] : "no se supo") + ")");
            }
            object[] basura = { new byte[64], 64, 0, 0 };
            Exige(!(bool)medida.Invoke(null, basura), "con bytes que no son una imagen no se sabe la medida (se decodifica como siempre)");
            object[] corta = { new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x7F, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 28, 0, 0 };
            Exige(!(bool)medida.Invoke(null, corta), "un JPEG cortado a media cabecera no se lee fuera del arreglo");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN (" + oks + " comprobaciones; memoria medida con Mono/libgdiplus, no en XP)" : "\n  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}
