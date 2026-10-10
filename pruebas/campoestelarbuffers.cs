using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

// Solo generacion numerica y buffers en RAM: no inicia ventanas, musica ni un dispositivo GPU.
// Baseline opcional (segundo argumento): exige patrones y vertices exactos de la compilacion anterior.
// Sin baseline, la receta independiente conserva el orden de Random, tipos, colores y semillas.
class CampoEstelarBuffers
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly string[] Active = { "sx", "sy", "sphase", "sspeed", "ssize", "skind", "sforma", "scol" };
    static readonly string[] Previous = { "px", "py", "pphase", "pspeed", "psize", "pkind", "pforma", "pcol" };
    static Type stars, beforeStars;
    static int checks;
    static void Check(bool yes, string text) { if (!yes) throw new Exception(text); checks++; }
    static object Get(object x, string field) { return x.GetType().GetField(field, B).GetValue(x); }
    static void Set(object x, string field, object value) { x.GetType().GetField(field, B).SetValue(x, value); }
    static object Call(object x, string method, params object[] args)
    { try { return x.GetType().GetMethod(method, B).Invoke(x, args); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static object Field(Type type, float density, bool natural, bool fine)
    {
        object field = FormatterServices.GetUninitializedObject(type);
        Set(field, "densidad", density); Set(field, "FondoNatural", natural); Set(field, "EstrellasFinas", fine);
        return field;
    }
    static int Count(int w, int h, float density)
    {
        int n = Math.Max(200, w * h / 1700); if (n > 1400) n = 1400;
        if (density < .999f) { n = (int)(n * density); if (n < 24) n = 24; }
        return n;
    }
    static float Gauss(Random r)
    {
        double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }
    static Array[] Recipe(int w, int h, float density, int seed)
    {
        int n = Count(w, h, density); Random r = new Random(seed);
        float[] x = new float[n], y = new float[n], phase = new float[n], speed = new float[n], size = new float[n];
        int[] kind = new int[n], form = new int[n]; uint[] col = new uint[n];
        uint[] palette = { 0xFF9BB0FF, 0xFFAABFFF, 0xFFCAD7FF, 0xFFE4EBFF, 0xFFF8F7FF, 0xFFFFFFFA,
            0xFFFFF4EA, 0xFFFFEDCD, 0xFFFFDAA5, 0xFFFFC78E, 0xFFFFB280 };
        int nc = 10 + r.Next(5); float[] cx = new float[nc], cy = new float[nc], cr = new float[nc];
        for (int c = 0; c < nc; c++) {
            cx[c] = w * (.10f + (float)r.NextDouble() * .80f);
            cy[c] = h * (.10f + (float)r.NextDouble() * .80f);
            cr[c] = h * (.10f + (float)r.NextDouble() * .14f);
        }
        for (int i = 0; i < n; i++) {
            float bx, by;
            for (int attempt = 0; ; attempt++) {
                if (attempt < 6 && r.Next(100) < 25) {
                    int c = r.Next(nc); bx = cx[c] + Gauss(r) * cr[c]; by = cy[c] + Gauss(r) * cr[c];
                } else { bx = (float)(r.NextDouble() * w); by = (float)(r.NextDouble() * h); }
                if (bx >= 2f && bx <= w - 2f && by >= 2f && by <= h - 2f) break;
            }
            x[i] = bx; y[i] = by;
            phase[i] = (float)(r.NextDouble() * Math.PI * 2);
            speed[i] = .6f + (float)r.NextDouble() * 1.8f;
            double m = Math.Pow(r.NextDouble(), 4.6);
            kind[i] = m > .80 ? 2 : m > .55 ? 1 : 0;
            size[i] = kind[i] == 2 ? (float)(20 + m * 14) : kind[i] == 1 ? (float)(8 + m * 2) : (float)(1.0 + m * 1.2);
            // Valores historicos de las tres casillas del atlas.
            form[i] = kind[i] == 0 ? 2 : kind[i] == 2 ? 1 : 0;
            int q = r.Next(100);
            int p = q < 14 ? r.Next(2) : q < 34 ? 2 + r.Next(2) : q < 68 ? 4 + r.Next(2) :
                q < 88 ? 6 + r.Next(2) : q < 97 ? 8 + r.Next(2) : 10;
            uint alpha = (uint)(255.0 * (.26 + m * .74)); if (alpha > 255) alpha = 255;
            col[i] = (alpha << 24) | (palette[p] & 0xFFFFFFu);
        }
        return new Array[] { x, y, phase, speed, size, kind, form, col };
    }
    static Array[] Arrays(object field, bool previous, bool clone)
    {
        string[] names = previous ? Previous : Active; Array[] arrays = new Array[names.Length];
        for (int i = 0; i < names.Length; i++) {
            Array a = (Array)Get(field, names[i]); arrays[i] = a == null || !clone ? a : (Array)a.Clone();
        }
        return arrays;
    }
    static bool SameArray(Array a, Array b, int count)
    {
        if (a == null || b == null || a.Length < count || b.Length < count) return false;
        byte[] aa = new byte[count * 4], bb = new byte[count * 4];
        Buffer.BlockCopy(a, 0, aa, 0, aa.Length); Buffer.BlockCopy(b, 0, bb, 0, bb.Length);
        for (int i = 0; i < aa.Length; i++) if (aa[i] != bb[i]) return false; return true;
    }
    static void SamePattern(Array[] actual, Array[] expected, int count, string stage)
    {
        for (int i = 0; i < Active.Length; i++)
            Check(SameArray(actual[i], expected[i], count), stage + ": patron exacto " + Active[i]);
    }
    static void Distinct(object field)
    {
        Array[] a = Arrays(field, false, false), p = Arrays(field, true, false);
        for (int i = 0; i < a.Length; i++) {
            Check(a[i] != null && p[i] != null && !Object.ReferenceEquals(a[i], p[i]), "Activo/anterior independientes " + Active[i]);
            for (int k = 0; k < a.Length; k++) Check(!Object.ReferenceEquals(a[i], p[k]), "Ningun alias entre juegos");
        }
        Check(!Object.ReferenceEquals(Get(field, "starV"), Get(field, "starV2")), "Vertices actual/anterior independientes");
    }
    static Array Vertices(object field, bool previous, int w, int h, double time, float fade, float pulse)
    {
        int n = (int)Get(field, previous ? "pnStars" : "nStars");
        Array v = (Array)Get(field, previous ? "starV2" : "starV"); Array[] s = Arrays(field, previous, false);
        Call(field, "BuildStarVerts", v, s[0], s[1], s[2], s[3], s[4], s[5], s[6], s[7],
            n, time, fade, pulse, new float[] { 3, 5, 8 }, new float[] { 2, 3, 4 }, (float)w, (float)h);
        return v;
    }
    static bool SameVertices(Array a, Array b, int count)
    {
        Check(Marshal.SizeOf(a.GetType().GetElementType()) == 28 && Marshal.SizeOf(b.GetType().GetElementType()) == 28, "Vtx conserva 28 bytes");
        byte[] aa = new byte[count * 28], bb = new byte[count * 28];
        GCHandle ah = GCHandle.Alloc(a, GCHandleType.Pinned), bh = GCHandle.Alloc(b, GCHandleType.Pinned);
        try { Marshal.Copy(ah.AddrOfPinnedObject(), aa, 0, aa.Length); Marshal.Copy(bh.AddrOfPinnedObject(), bb, 0, bb.Length); }
        finally { ah.Free(); bh.Free(); }
        for (int i = 0; i < aa.Length; i++) if (aa[i] != bb[i]) return false; return true;
    }
    static void ReferenceRenew(object old, int w, int h, int seed, int now)
    {
        for (int i = 0; i < Active.Length; i++) Set(old, Previous[i], Get(old, Active[i]));
        Set(old, "pnStars", Get(old, "nStars")); Call(old, "BuildStars", w, h, seed);
        int count = (int)Get(old, "pnStars") * 6;
        Array v = (Array)Get(old, "starV2");
        if (v == null || v.Length < count) Set(old, "starV2", Array.CreateInstance(old.GetType().Assembly.GetType("Rocola.D3D9+Vtx", true), count));
        Set(old, "transing", true); Set(old, "transStart", now);
    }
    static void Exact(object actual, object old, int w, int h, float density, int seed)
    {
        int count = Count(w, h, density);
        Check((int)Get(actual, "nStars") == count, "Misma cantidad por tamaño y densidad");
        SamePattern(Arrays(actual, false, false), Recipe(w, h, density, seed), count, "Receta independiente");
        if (old != null) {
            SamePattern(Arrays(actual, false, false), Arrays(old, false, false), count, "Compilacion anterior");
            foreach (double time in new double[] { 0, 53.3, 60.2, 180.7 }) {
                Check(SameVertices(Vertices(actual, false, w, h, time, .71f, .9f), Vertices(old, false, w, h, time, .71f, .9f), count * 6),
                    "Vertices actuales identicos al baseline");
            }
        }
    }
    static void RecipesAndChanges()
    {
        foreach (bool natural in new bool[] { false, true })
            foreach (bool fine in new bool[] { false, true })
                foreach (float density in new float[] { .1f, .55f, 1f }) {
                    object field = Field(stars, density, natural, fine);
                    object old = beforeStars == null ? null : Field(beforeStars, density, natural, fine);
                    int step = 0;
                    foreach (Size screen in new Size[] { new Size(3440,1440), new Size(640,480), new Size(1024,768), new Size(1920,1080), new Size(7680,4320) }) {
                        int w = screen.Width, h = screen.Height, seed = unchecked(7731 + step * 40503);
                        Call(field, "ReconstruyePatron", w, h, seed); if (old != null) Call(old, "BuildStars", w, h, seed);
                        Check(!(bool)Get(field, "transing"), "Tamaño/densidad cortan el entrecruce");
                        Exact(field, old, w, h, density, seed);
                        Array[] snapshot = Arrays(field, false, true);
                        int n = (int)Get(field, "nStars");
                        for (int round = 0; round < 3; round++) {
                            seed = unchecked(seed + 40503); int now = unchecked(Int32.MaxValue - 1000 + round * 1100);
                            Call(field, "RenuevaPatron", w, h, seed, now);
                            if (old != null) ReferenceRenew(old, w, h, seed, now);
                            Check((bool)Get(field, "transing") && (int)Get(field, "transStart") == now, "Cruce y TickCount conservados");
                            Check((int)Get(field, "pnStars") == n, "Cantidad anterior conservada");
                            SamePattern(Arrays(field, true, false), snapshot, n, "No pisa el patron que se desvanece");
                            Distinct(field); Exact(field, old, w, h, density, seed);
                            if (old != null)
                                Check(SameVertices(Vertices(field, true, w, h, 63.1, .29f, .8f), Vertices(old, true, w, h, 63.1, .29f, .8f), n * 6),
                                    "Vertices anteriores identicos durante el cruce, incluso si otro cambio lo interrumpe");
                            snapshot = Arrays(field, false, true); n = (int)Get(field, "nStars");
                        }
                        step++;
                    }
                }
        Console.WriteLine("  OK recetas, densidades, tamaños y cruces Legacy/Natural");
    }
    static void StableBuffers()
    {
        object field = Field(stars, 1f, false, true);
        Call(field, "BuildStars", 3440, 1440, 100);
        Array[] first = Arrays(field, false, false);
        object verts = Get(field, "starV");
        Call(field, "RenuevaPatron", 3440, 1440, 101, 0);
        Array[] second = Arrays(field, false, false);
        object previousVerts = Get(field, "starV2");
        object centersX = Get(field, "starCenterX"), centersY = Get(field, "starCenterY"), centersR = Get(field, "starCenterRadius");
        for (int round = 0; round < 100; round++) {
            Call(field, "RenuevaPatron", 3440, 1440, round + 102, round * 60000);
            for (int i = 0; i < Active.Length; i++) {
                object a = Get(field, Active[i]), p = Get(field, Previous[i]);
                Check(Object.ReferenceEquals(a, round % 2 == 0 ? first[i] : second[i]) &&
                    Object.ReferenceEquals(p, round % 2 == 0 ? second[i] : first[i]), "Solo los dos buffers originales tras calentar");
            }
            Check(Object.ReferenceEquals(verts, Get(field, "starV")) && Object.ReferenceEquals(previousVerts, Get(field, "starV2")),
                "Ningun nuevo buffer LOH al renovar");
            Check(Object.ReferenceEquals(centersX, Get(field, "starCenterX")) && Object.ReferenceEquals(centersY, Get(field, "starCenterY")) &&
                Object.ReferenceEquals(centersR, Get(field, "starCenterRadius")), "Centros reutilizados aunque cambie su cantidad");
        }
        Array[] previous = Arrays(field, true, false), snapshot = Arrays(field, true, true);
        int previousN = (int)Get(field, "pnStars");
        Set(field, "densidad", .1f); Call(field, "ReconstruyePatron", 640, 480, 311);
        Check(!(bool)Get(field, "transing"), "Cambiar densidad durante el cruce lo cancela");
        for (int i = 0; i < Previous.Length; i++) Check(Object.ReferenceEquals(previous[i], Get(field, Previous[i])), "No pierde capacidad inactiva al cambiar tamaño");
        SamePattern(Arrays(field, true, false), snapshot, previousN, "Reconstruir no pisa el juego anterior");
        Check(Object.ReferenceEquals(verts, Get(field, "starV")), "Bajar tamaño/densidad no crea un buffer menor");
        Exact(field, null, 640, 480, .1f, 311);
        Array[] current = Arrays(field, false, true); int currentN = (int)Get(field, "nStars");
        Call(field, "RenuevaPatron", 640, 480, 312, 0);
        SamePattern(Arrays(field, true, false), current, currentN, "Primera renovacion tras reconstruir conserva el actual");
        Set(field, "densidad", 1f); Call(field, "ReconstruyePatron", 3440, 1440, 313);
        Check(Object.ReferenceEquals(verts, Get(field, "starV")), "Volver a alta resolucion reutiliza la capacidad completa");
        Distinct(field); Exact(field, null, 3440, 1440, 1f, 313);
        Console.WriteLine("  OK cien renovaciones sin buffers nuevos; cambios durante el cruce");
    }
    static int Main(string[] args)
    {
        try {
            Assembly assembly = Assembly.LoadFrom(args.Length > 0 ? args[0] : "Rocola.exe");
            stars = assembly.GetType("Rocola.GpuStarfield");
            if (stars == null) { Console.WriteLine("  OK CPU congelado: no existe el campo GPU"); return 0; }
            if (args.Length > 1) beforeStars = Assembly.Load(File.ReadAllBytes(args[1])).GetType(stars.FullName, true);
            RecipesAndChanges(); StableBuffers();
            Check(Codigo.Llama(stars.GetMethod("Begin", B), "RenuevaPatron"), "Renovacion real usa el doble buffer");
            Check(Codigo.Llama(stars.GetMethod("SetEfectos", B), "ReconstruyePatron") &&
                Codigo.Llama(stars.GetMethod("Redimensiona", B), "ReconstruyePatron"), "Densidad y tamaño conservan buffers sin cruce");
            Console.WriteLine("PASS campoestelarbuffers: " + checks + " comprobaciones" +
                (beforeStars == null ? ", receta independiente" : ", tambien baseline exacto") + ", sin GPU/HAL/audio"); return 0;
        } catch (Exception e) { Console.WriteLine("FALLO: " + e); return 1; }
    }
}
