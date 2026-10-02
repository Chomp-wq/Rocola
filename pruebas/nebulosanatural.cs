using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

class NebulosaNatural
{
    const BindingFlags All = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type panel, form, star;
    static int checks, failures;
    static void Check(bool ok, string message) { checks++; if (!ok) { failures++; Console.WriteLine("FALLO: " + message); } }
    static Bitmap Bake(string name, params object[] args)
    {
        Type[] types = new Type[args.Length];
        for (int i = 0; i < args.Length; i++) types[i] = args[i].GetType();
        return (Bitmap)panel.GetMethod(name, All, null, types, null).Invoke(null, args);
    }
    static int[] Pixels(Bitmap b)
    {
        int[] pixels = new int[b.Width * b.Height];
        BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try { for (int y = 0; y < b.Height; y++) Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + y * (long)d.Stride), pixels, y * b.Width, b.Width); }
        finally { b.UnlockBits(d); }
        return pixels;
    }
    static ulong Hash(Bitmap b)
    {
        ulong hash = 14695981039346656037UL;
        foreach (int color in Pixels(b)) { hash ^= unchecked((uint)color); hash = unchecked(hash * 1099511628211UL); }
        return hash;
    }
    static double Mean(Bitmap b) { long sum = 0; foreach (int p in Pixels(b)) sum += (uint)p >> 24; return sum / (double)(b.Width * b.Height); }
    static void Map(Bitmap b, int w, int h, bool black, int max)
    {
        Check(b.Width == w && b.Height == h, "tamano de mapa");
        Check(b.PixelFormat == PixelFormat.Format32bppArgb, "ARGB32 sin una superficie nueva de mayor formato");
        int[] p = Pixels(b);
        foreach (int color in p)
        {
            Check((unchecked((uint)color) & 0x00FFFFFFu) == (black ? 0u : 0x00FFFFFFu), "neutro tintable; negro solo en la vineta");
            Check((uint)color >> 24 <= max, "alfa contenido");
        }
    }
    static bool Rejected(string name, params object[] args)
    {
        try { using (Bitmap b = Bake(name, args)) { } return false; }
        catch (TargetInvocationException e) { return e.InnerException is ArgumentOutOfRangeException || e.InnerException is OverflowException; }
    }
    static void Legacy()
    {
        FieldInfo detail = form.GetField("nebNaturalFuerza", All);
        float saved = (float)detail.GetValue(null);
        try
        {
            detail.SetValue(null, .55f);
            using (Bitmap b = Bake("BakeNatural", 64, 40))
            {
                ulong hash = Hash(b); Console.WriteLine("LEGACY_NEB=" + hash);
                Check(hash == 6923773870616977701UL, "fingerprint del detalle natural legacy");
            }
            using (Bitmap b = Bake("BakeVineta", 128))
            {
                ulong hash = Hash(b); Console.WriteLine("LEGACY_VIN=" + hash);
                Check(hash == 16774875469761159973UL, "fingerprint de vineta legacy");
            }
            using (Bitmap b = (Bitmap)star.GetMethod("BakeViaLactea", All).Invoke(null, new object[] { 64, 40 }))
            {
                ulong hash = Hash(b); Console.WriteLine("LEGACY_VIA=" + hash);
                Check(hash == 4696848735578331673UL, "fingerprint de via lactea legacy");
            }
        }
        finally { detail.SetValue(null, saved); }
    }
    static void Natural(string output)
    {
        FieldInfo detail = form.GetField("nebNaturalFuerza", All);
        float saved = (float)detail.GetValue(null);
        try
        {
            int[,] sizes = { { 1, 1 }, { 8, 8 }, { 64, 40 }, { 256, 192 }, { 480, 270 }, { 860, 360 } };
            detail.SetValue(null, .55f);
            for (int i = 0; i < sizes.GetLength(0); i++)
            {
                int w = sizes[i, 0], h = sizes[i, 1];
                foreach (string name in new string[] { "BakeNebulosaNatural", "BakeViaNatural" })
                    using (Bitmap a = Bake(name, w, h))
                    using (Bitmap b = Bake(name, w, h))
                    {
                        Map(a, w, h, false, name == "BakeViaNatural" ? 48 : 70);
                        Check(Hash(a) == Hash(b), name + " determinista");
                    }
            }
            Check(Rejected("BakeNebulosaNatural", 0, 10), "neb ancho0 rechazado");
            Check(Rejected("BakeViaNatural", 10, -1), "via alto negativo rechazado");
            Check(Rejected("BakeNebulosaNatural", int.MaxValue, 2), "multiplicacion desbordada rechazada");
            Check(Rejected("BakeVinetaNatural", 1), "vineta insuficiente rechazada");
            using (Bitmap neb = Bake("BakeNebulosaNatural", 256, 192))
            using (Bitmap via = Bake("BakeViaNatural", 256, 192))
            using (Bitmap vin = Bake("BakeVinetaNatural", 128))
            {
                Map(vin, 128, 128, true, 76);
                Check(vin.GetPixel(64, 64).A == 0, "centro de vineta transparente");
                Check(vin.GetPixel(0, 0).A == 76, "esquinas suaves de vineta");
                int[] p = Pixels(neb); int transparent = 0, bright = 0, variation = 0;
                foreach (int c in p) { int a = (int)((uint)c >> 24); if (a == 0) transparent++; if (a > 40) bright++; }
                for (int y = 1; y < neb.Height; y++) for (int x = 1; x < neb.Width; x++)
                    if (((uint)p[y * neb.Width + x] >> 24) != ((uint)p[y * neb.Width + x - 1] >> 24)) variation++;
                double avg = Mean(neb), avgVia = Mean(via);
                Console.WriteLine("Natural: alfa medio neb=" + avg.ToString("0.00") + ", via=" + avgVia.ToString("0.00") + "; vacios=" + transparent + ", luces=" + bright);
                Check(avg > 6 && avg < 15, "neb con cuerpo visible pero sin velo opaco que cubra todo");
                Check(transparent > p.Length / 3, "mas de un tercio del cielo conserva vacios reales");
                Check(bright < p.Length / 20, "picos luminosos selectivos");
                Check(variation > p.Length / 15, "detalle espacial en los filamentos");
                Check(avgVia > .8 && avgVia < 5, "via con relieve visible sin borrar los canales de polvo");
                if (output != null) Preview(output, neb, via, vin);
            }
            detail.SetValue(null, 0f);
            using (Bitmap zero = Bake("BakeNebulosaNatural", 200, 120))
            {
                double baseMean = Mean(zero);
                Check(baseMean > 0, "detalle0 conserva una nebulosa base ligera");
                detail.SetValue(null, 1f);
                using (Bitmap disabled = Bake("BakeNebulosaNatural", 200, 120, false)) Check(Hash(disabled) == Hash(zero), "detalle desactivado usa la base sin mutar el mando");
                Check((float)detail.GetValue(null) == 1f, "el horneado sin detalle no cambia el mando global");
                using (Bitmap full = Bake("BakeNebulosaNatural", 200, 120)) Check(Mean(full) > baseMean * 1.5, "el mando agrega detalle sin eliminar base");
            }
            detail.SetValue(null, .55f);
            ulong fallback;
            using (Bitmap normal = Bake("BakeNebulosaNatural", 64, 40)) fallback = Hash(normal);
            foreach (float value in new float[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                detail.SetValue(null, value);
                using (Bitmap bad = Bake("BakeNebulosaNatural", 64, 40)) Check(Hash(bad) == fallback, "detalle corrupto vuelve al valor de fabrica");
            }
            detail.SetValue(null, -10f);
            using (Bitmap negative = Bake("BakeNebulosaNatural", 64, 40))
            {
                detail.SetValue(null, 0f);
                using (Bitmap zero = Bake("BakeNebulosaNatural", 64, 40)) Check(Hash(negative) == Hash(zero), "detalle negativo acotado");
            }
            detail.SetValue(null, 10f);
            using (Bitmap huge = Bake("BakeNebulosaNatural", 64, 40))
            {
                detail.SetValue(null, 1f);
                using (Bitmap one = Bake("BakeNebulosaNatural", 64, 40)) Check(Hash(huge) == Hash(one), "detalle alto acotado");
            }
            Stopwatch clock = Stopwatch.StartNew();
            using (Bitmap performance = Bake("BakeNebulosaNatural", 860, 360)) { }
            clock.Stop();
            Console.WriteLine("Horneado puntual a cuarto de ultrapanoramico 3440x1440: " + clock.Elapsed.TotalMilliseconds.ToString("0.0") + "ms (no por cuadro).");
        }
        finally { detail.SetValue(null, saved); }
    }
    static void Preview(string directory, Bitmap neb, Bitmap via, Bitmap vin)
    {
        Directory.CreateDirectory(directory);
        neb.Save(Path.Combine(directory, "nebulosa-natural-alfa.png"), ImageFormat.Png);
        via.Save(Path.Combine(directory, "via-natural-alfa.png"), ImageFormat.Png);
        using (Bitmap scene = new Bitmap(1024, 768, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(scene))
        {
            using (System.Drawing.Drawing2D.LinearGradientBrush bg = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, scene.Width, scene.Height), Color.FromArgb(4, 7, 19), Color.FromArgb(12, 25, 67), 90f)) g.FillRectangle(bg, 0, 0, scene.Width, scene.Height);
            using (ImageAttributes tint = new ImageAttributes())
            {
                ColorMatrix cm = new ColorMatrix(); cm.Matrix00 = .32f; cm.Matrix11 = .42f; cm.Matrix22 = .70f;
                tint.SetColorMatrix(cm);
                g.DrawImage(neb, new Rectangle(0, 0, scene.Width, scene.Height), 0, 0, neb.Width, neb.Height, GraphicsUnit.Pixel, tint);
            }
            using (ImageAttributes tint = new ImageAttributes())
            {
                ColorMatrix cm = new ColorMatrix(); cm.Matrix00 = .73f; cm.Matrix11 = .78f; cm.Matrix22 = .92f;
                tint.SetColorMatrix(cm);
                g.DrawImage(via, new Rectangle(0, 0, scene.Width, scene.Height), 0, 0, via.Width, via.Height, GraphicsUnit.Pixel, tint);
            }
            g.DrawImage(vin, 0, 0, scene.Width, scene.Height);
            scene.Save(Path.Combine(directory, "nebulosa-natural-cielo-fixture.png"), ImageFormat.Png);
        }
    }
    static int Main(string[] args)
    {
        try
        {
            Assembly a = Assembly.LoadFrom(args[0]); panel = a.GetType("Rocola.GpuPanel"); form = a.GetType("Rocola.Form1"); star = a.GetType("Rocola.GpuStarfield");
            if (panel == null) { Console.WriteLine("OK: CPU congelado, sin mapas del estilo natural GPU."); return 0; }
            Legacy();
            if (args.Length < 2 || args[1] != "--legacy-baseline") Natural(args.Length > 1 ? args[1] : null);
            Console.WriteLine((failures == 0 ? "PASS: " : "FALLO: ") + checks + " comprobaciones, " + failures + " fallos.");
            return failures == 0 ? 0 : 1;
        }
        catch (Exception e) { Console.WriteLine(e); return 1; }
    }
}
