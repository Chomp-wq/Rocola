using System;
using System.IO;
using System.Reflection;

// Sin Form1, musica ni preferencias del usuario. Ambos perfiles, incluida .NET 3.5.
// Segundo argumento opcional: Release anterior para comparar el camino Legacy.
class OpticaNatural
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static int checks;
    static Type panel, stars;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static object Run(Type type, string name, params object[] args)
    { return type.GetMethod(name, B).Invoke(null, args); }
    static object Get(object value, string field) { return value.GetType().GetField(field, B).GetValue(value); }
    static int R(uint c) { return (int)((c >> 16) & 255); }
    static int G(uint c) { return (int)((c >> 8) & 255); }
    static int Blue(uint c) { return (int)(c & 255); }

    static void Tints(Type baseline)
    {
        foreach (bool second in new bool[] { false, true }) foreach (bool cine in new bool[] { false, true })
        {
            uint previous = 0; bool varies = false;
            for (int i = 0; i < 4096; i++)
            {
                uint input = unchecked((uint)(i * 7559)) | 0xA0000000u;
                uint old = (uint)Run(panel, "TinteNebulosa", input, second, cine);
                uint legacy = (uint)Run(panel, "TinteNebulosaEstilo", input, second, cine, false);
                Check(old == legacy, "El selector altera el tinte Legacy");
                if (baseline != null) Check(old == (uint)Run(baseline, "TinteNebulosa", input, second, cine), "Cambio al tinte del Release Legacy");
                uint natural = (uint)Run(panel, "TinteNebulosaEstilo", input, second, cine, true);
                Check((natural >> 24) == 0, "Tinte natural incluye un alfa no solicitado");
                Check(R(natural) <= Blue(natural) * 3 / 4 && G(natural) <= Blue(natural) * 9 / 10, "Una portada recolorea todo el cielo nocturno");
                Check(Blue(natural) >= 94 && Blue(natural) <= 180, "Nebulosa natural demasiado apagada o blanca");
                if (i != 0 && natural != previous) varies = true;
                previous = natural;
            }
            Check(varies, "El tinte natural borra completamente el acento de portada");
        }
        foreach (uint c in new uint[] { 0xFFFFFFFFu, 0xFF000000u, 0xFFFF00CCu, 0xFFFF8000u })
        {
            uint off = (uint)Run(panel, "TinteNebulosaEstilo", c, false, false, true);
            uint on = (uint)Run(panel, "TinteNebulosaEstilo", c, false, true, true);
            Check(off != on, "Nebulosas cine deja de producir un cambio visual");
        }
    }
    static void Halos(Type baseline)
    {
        MethodInfo ellipse = stars.GetMethod("DrawGlowElipse", B);
        Check(ellipse != null && ellipse.GetParameters().Length == 5, "Falta halo eliptico sin textura adicional");
        Check((int)stars.GetField("HaloVtx", B).GetRawConstantValue() == 18, "Aumenta la geometria de los halos");
        MethodInfo mesh = null;
        foreach (MethodInfo m in stars.GetMethods(B)) if (m.Name == "Halo" && m.GetParameters().Length == 7) mesh = m;
        Check(mesh != null, "Falta la malla eliptica reutilizada");
        foreach (float width in new float[] { 16, 60, 180, 700 }) foreach (float height in new float[] { 16, 90, 270, 900 })
        foreach (uint color in new uint[] { 0u, 0xFF000000u, 0xFFFFFFFFu, 0x7000FF66u, 0xC9EF52C1u })
        {
            object[] halo = { width, height, color, 0f, 0f, 0u };
            Run(panel, "HaloPortadaNatural", halo);
            float rx = (float)halo[3], ry = (float)halo[4]; uint tint = (uint)halo[5];
            Check(rx > 0 && ry > 0 && Math.Abs(rx / ry - width / height) < .001f, "Halo no sigue la forma de la portada");
            Check((tint >> 24) <= (color >> 24), "El acabado natural vuelve opaco el halo");
            float legacy = Math.Max(width, height) * .85f;
            Check(rx * ry < legacy * legacy, "El halo natural pinta mas superficie que el de seleccion Legacy");
            Array vertices = Array.CreateInstance(stars.Assembly.GetType("Rocola.D3D9+Vtx", true), 18);
            mesh.Invoke(null, new object[] { vertices, 0, 400f, 450f, rx, ry, tint });
            foreach (object v in vertices)
            {
                float x = (float)Get(v, "X"), y = (float)Get(v, "Y"), u = (float)Get(v, "U"), w = (float)Get(v, "V");
                Check(!float.IsNaN(x) && !float.IsNaN(y) && !float.IsInfinity(x) && !float.IsInfinity(y), "Halo no finito");
                Check(x >= 400 - rx - 1 && x <= 400 + rx + 1 && y >= 450 - ry - 1 && y <= 450 + ry + 1, "Halo sale de sus radios");
                Check(u >= 0 && u <= 1 && w >= 0 && w <= 1, "Halo muestrea fuera de la textura lisa");
                Check((uint)Get(v, "Color") == tint, "Halo introduce una capa opaca");
            }
        }
        if (baseline == null) return;
        foreach (double ms in new double[] { 0, 16, 1250, 2500, 3750, 5000, 60000 })
        foreach (uint color in new uint[] { 0xFFAA30EFu, 0xFFF0F0F0u, 0xFF123456u })
        {
            object[] before = { 11f, 21f, 180f, 270f, color, ms, 0f, 0f, 0f, 0u };
            object[] after = (object[])before.Clone();
            Run(baseline, "HaloGenero", before); Run(panel, "HaloGenero", after);
            for (int i = 6; i < after.Length; i++) Check(before[i].Equals(after[i]), "Cambio a halo de genero Legacy");
        }
    }
    static int Main(string[] args)
    {
        try
        {
            if (args.Length < 1) throw new Exception("Uso: opticanatural Rocola.exe [Release-anterior.exe]");
            Assembly app = Assembly.LoadFile(Path.GetFullPath(args[0]));
            panel = app.GetType("Rocola.GpuPanel", true); stars = app.GetType("Rocola.GpuStarfield", true);
            Type previous = args.Length < 2 ? null : Assembly.LoadFile(Path.GetFullPath(args[1])).GetType("Rocola.GpuPanel", true);
            Tints(previous); Halos(previous);
            Console.WriteLine("PASS optica natural: " + checks + " comprobaciones; mismas texturas, 18 vertices y Legacy conservado");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("FAIL optica natural: " + e); return 1; }
    }
}
