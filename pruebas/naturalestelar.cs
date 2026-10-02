using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Acabado estelar GPU. El segundo argumento opcional es un ejecutable Legacy anterior:
// compara bitmap, geometria y material EXACTOS, no solo una tolerancia visual.
// El tercero opcional guarda laminas de atlas/poses de prueba; no son capturas de la app.
class NaturalEstelar
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int checks;
    static Type stars, gpu, mesh, vertex;
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); checks++; }
    static object Read(object instance, string name) { return instance.GetType().GetField(name, B).GetValue(instance); }
    static void Write(object instance, string name, object value) { instance.GetType().GetField(name, B).SetValue(instance, value); }
    static object Call(Type type, object instance, string name, params object[] parameters) { return type.GetMethod(name, B).Invoke(instance, parameters); }
    static Array Make(int count) { return Array.CreateInstance(vertex, count); }
    static uint Col(Array buffer, int i) { return (uint)vertex.GetField("Color", B).GetValue(buffer.GetValue(i)); }
    static float X(Array buffer, int i) { return (float)vertex.GetField("X", B).GetValue(buffer.GetValue(i)); }
    static float Y(Array buffer, int i) { return (float)vertex.GetField("Y", B).GetValue(buffer.GetValue(i)); }
    static bool Same(Array first, Array second, int count)
    {
        for (int i = 0; i < count; i++)
            foreach (string field in new string[] { "X", "Y", "Z", "Rhw", "Color", "U", "V" })
                if (!first.GetValue(i).GetType().GetField(field, B).GetValue(first.GetValue(i)).Equals(second.GetValue(i).GetType().GetField(field, B).GetValue(second.GetValue(i)))) return false;
        return true;
    }
    static bool SameBitmap(Bitmap a, Bitmap b)
    {
        if (a.Size != b.Size) return false;
        for (int y = 0; y < a.Height; y++) for (int x = 0; x < a.Width; x++) if (a.GetPixel(x, y) != b.GetPixel(x, y)) return false;
        return true;
    }
    static object Field(Type type, int w, int h)
    {
        object sf = FormatterServices.GetUninitializedObject(type);
        Write(sf, "densidad", 1f); Write(sf, "EstrellasFinas", true);
        Call(type, sf, "BuildStars", w, h, 7731);
        return sf;
    }
    static Array FieldVertices(Type type, object field, int w, int h, double time, float pulse)
    {
        int count = (int)Read(field, "nStars");
        Type vt = type.Assembly.GetType("Rocola.D3D9+Vtx", true);
        Array result = Array.CreateInstance(vt, count * 6);
        Call(type, field, "BuildStarVerts", result, Read(field, "sx"), Read(field, "sy"), Read(field, "sphase"), Read(field, "sspeed"), Read(field, "ssize"),
            Read(field, "skind"), Read(field, "sforma"), Read(field, "scol"), count, time, .83f, pulse, new float[] { 3, 5, 8 }, new float[] { 2, 3, 4 }, (float)w, (float)h);
        return result;
    }
    static object[] Pose(Type type, object panel, double yaw, bool echo, uint tint)
    {
        Type vt = type.Assembly.GetType("Rocola.D3D9+Vtx", true);
        object[] parameters = { Array.CreateInstance(vt, 60), 0, Array.CreateInstance(vt, 180), 0, 180f, 150f, 95f, yaw, .22, .1, tint, echo ? 130f : 255f, echo, echo ? 1.8f : 1.1f, 0, 0, 0f };
        int count = (int)Call(type, panel, "PoseEstrella", parameters);
        return new object[] { parameters[0], parameters[2], count, parameters[14] };
    }
    static void BudgetAndLegacy(Assembly baseline)
    {
        foreach (Size screen in new Size[] { new Size(640, 480), new Size(1024, 768), new Size(1920, 1080), new Size(3440, 1440), new Size(7680, 4320) })
        {
            object f = Field(stars, screen.Width, screen.Height);
            int count = (int)Read(f, "nStars");
            Check(count <= 1400 && count >= 200, "cap 1400 y mismo suelo por resolucion");
            Array legacy = FieldVertices(stars, f, screen.Width, screen.Height, 53.3, .9f);
            if (baseline != null)
            {
                Type oldStars = baseline.GetType(stars.FullName, true);
                object old = Field(oldStars, screen.Width, screen.Height);
                Check((int)Read(old, "nStars") == count, "Legacy conserva cantidad");
                Check(Same(legacy, FieldVertices(oldStars, old, screen.Width, screen.Height, 53.3, .9f), count * 6), "Legacy conserva los vertices exactos del campo anterior");
            }
            Write(f, "FondoNatural", true);
            Array natural = FieldVertices(stars, f, screen.Width, screen.Height, 53.3, .9f);
            Check(natural.Length == legacy.Length && !Same(legacy, natural, count * 6), "Natural distinto sin aumentar vertices");
            Write(f, "FondoNatural", false);
            Check(Same(legacy, FieldVertices(stars, f, screen.Width, screen.Height, 53.3, .9f), count * 6), "volver a Legacy recupera exactamente el campo");
            int[] kinds = (int[])Read(f, "skind");
            for (int i = 0; i < count; i++) Check(kinds[i] >= 0 && kinds[i] < 3, "solo tres capas de deriva");
        }
        using (Control panel = (Control)Activator.CreateInstance(gpu, true))
        {
            Check(!(bool)Read(panel, "FondoNatural"), "GpuPanel arranca en Legacy");
            Check(((Array)Read(panel, "starEchoes")).Length == 12, "12 ecos, no mas");
            Check((int)mesh.GetField("Caras", B).GetRawConstantValue() == 20, "20 caras, no mas");
            Check(((Array)Read(panel, "e3ecos")).Length == 12 * 20 * 3, "buffer fijo de ecos");
            using (Control old = baseline != null ? (Control)Activator.CreateInstance(baseline.GetType(gpu.FullName, true), true) : null)
                foreach (double yaw in new double[] { 0, .3, 1.2, 2.4, 3.2, 5.9 })
                    foreach (bool echo in new bool[] { false, true })
                    {
                        Write(panel, "FondoNatural", false);
                        object[] legacy = Pose(gpu, panel, yaw, echo, 0xFFFF6ABA);
                        if (old != null)
                        {
                            object[] original = Pose(old.GetType(), old, yaw, echo, 0xFFFF6ABA);
                            Check((int)legacy[2] == (int)original[2] && (int)legacy[3] == (int)original[3], "Legacy conserva conteo de pose");
                            Check(Same((Array)legacy[0], (Array)original[0], (int)legacy[2]) && Same((Array)legacy[1], (Array)original[1], (int)legacy[3]), "Legacy conserva pose y color exactos");
                        }
                        Write(panel, "FondoNatural", true);
                        object[] natural = Pose(gpu, panel, yaw, echo, 0xFFFF6ABA);
                        Check((int)legacy[2] == (int)natural[2] && (int)legacy[3] == (int)natural[3], "Natural misma malla visible y mismo contorno");
                        Check(!Same((Array)legacy[0], (Array)natural[0], (int)legacy[2]), "Natural modifica material");
                        for (int i = 0; i < (int)natural[2]; i++) Check((Col((Array)natural[0], i) >> 24) == (echo ? 130u : 255u), "superficie solida y alfa explicito de ecos");
                    }
        }
    }
    static void Textures(Assembly baseline, string folder)
    {
        using (Bitmap legacy = (Bitmap)Call(stars, null, "BakeStarTexture", 64))
        using (Bitmap natural = (Bitmap)Call(stars, null, "BakeNaturalStarTexture", 64))
        using (Bitmap again = (Bitmap)Call(stars, null, "BakeNaturalStarTexture", 64))
        {
            Check(legacy.Size == natural.Size && natural.Size == new Size(128, 128), "mismo atlas 128x128");
            Check(!SameBitmap(legacy, natural) && SameBitmap(natural, again), "atlas natural diferente y determinista");
            if (baseline != null)
                using (Bitmap old = (Bitmap)Call(baseline.GetType(stars.FullName, true), null, "BakeStarTexture", 64)) Check(SameBitmap(legacy, old), "atlas Legacy pixel-identico al anterior");
            for (int y = 64; y < 128; y++) for (int x = 64; x < 128; x++) Check(natural.GetPixel(x, y).A == 0, "cuarta celda transparente");
            Check(natural.GetPixel(31, 31).A > 230 && natural.GetPixel(0, 0).A == 0, "nucleo claro sin bloque en cantos");
            Check(natural.GetPixel(16, 31).A > 35 && natural.GetPixel(31, 16).A > 35,
                "mediana conserva cuatro puntas visibles, no un punto");
            Check(natural.GetPixel(16, 16).A < natural.GetPixel(16, 31).A / 2,
                "las puntas sobresalen del halo, no una pompa redonda");
            Check(natural.GetPixel(64 + 16, 31).A > natural.GetPixel(16, 31).A,
                "las grandes tienen rayos mas destacados que las medianas");
            if (folder != null) { legacy.Save(Path.Combine(folder, "atlas-legacy.png"), ImageFormat.Png); natural.Save(Path.Combine(folder, "atlas-natural.png"), ImageFormat.Png); }
        }
        bool invalid = false;
        try { Call(stars, null, "BakeNaturalStarTexture", 0); } catch (TargetInvocationException e) { invalid = e.InnerException is ArgumentOutOfRangeException; }
        Check(invalid, "tamano de atlas invalido rechazado");
        Check(Codigo.Llama(stars.GetMethod("Begin", B), "GpuStarfield.ActualizaAtlasNatural"), "atlas conectado al cuadro");
        Check(!Codigo.Llama(stars.GetMethod("BuildStarVerts", B), "GpuStarfield.BakeNaturalStarTexture"), "no hornea por estrella ni por cuadro");
        Check(Codigo.Llama(stars.GetMethod("ActualizaAtlasNatural", B), "D3D9.Release"), "libera atlas anterior al sustituirlo");
    }
    static void DistributionAndSurface(Assembly baseline, string folder)
    {
        foreach (Size screen in new Size[] { new Size(640, 480), new Size(1024, 768), new Size(3440, 1440) })
        foreach (float density in new float[] { .2f, .55f, 1f })
        foreach (bool fine in new bool[] { false, true })
        {
            object legacy = Field(stars, screen.Width, screen.Height), natural = Field(stars, screen.Width, screen.Height);
            Write(legacy, "densidad", density); Write(natural, "densidad", density);
            Write(legacy, "EstrellasFinas", fine); Write(natural, "EstrellasFinas", fine); Write(natural, "FondoNatural", true);
            Call(stars, legacy, "BuildStars", screen.Width, screen.Height, 7731);
            Call(stars, natural, "BuildStars", screen.Width, screen.Height, 7731);
            Check((int)Read(legacy, "nStars") == (int)Read(natural, "nStars"), "Natural comparte densidad Legacy, no oculta ni agrega estrellas");
            foreach (string field in new string[] { "sx", "sy", "ssize", "skind", "sforma", "sphase", "sspeed", "scol" })
            {
                Array before = (Array)Read(legacy, field), after = (Array)Read(natural, field);
                Check(before.Length == after.Length, "distribucion " + field + " conserva cantidad");
                for (int i = 0; i < before.Length; i++) Check(before.GetValue(i).Equals(after.GetValue(i)), "patron, profundidad, tamanos y formas exactamente Legacy");
            }
            Array lv = FieldVertices(stars, legacy, screen.Width, screen.Height, 53.3, 0f);
            Array nv = FieldVertices(stars, natural, screen.Width, screen.Height, 53.3, 0f);
            for (int i = 0; i < lv.Length; i += 6) {
                Check(Math.Abs((X(lv, i) + X(lv, i + 1)) - (X(nv, i) + X(nv, i + 1))) < .002f &&
                    Math.Abs((Y(lv, i) + Y(lv, i + 2)) - (Y(nv, i) + Y(nv, i + 2))) < .002f,
                    "cobertura antialias no desplaza la estrella ni rompe la deriva");
                Check((Col(lv, i) & 0xFFFFFFu) == (Col(nv, i) & 0xFFFFFFu), "Natural conserva temperaturas estelares Legacy");
            }
        }
        foreach (bool detailed in new bool[] { false, true })
        using (Bitmap legacy = (Bitmap)Call(gpu, null, "BakeStar", detailed))
        using (Bitmap natural = (Bitmap)Call(gpu, null, "BakeStarNatural", detailed))
        using (Bitmap repeat = (Bitmap)Call(gpu, null, "BakeStarNatural", detailed))
        {
            Check(legacy.Size == natural.Size && natural.Size == new Size(256, 256), "estrella 2D conserva presupuesto de textura");
            Check(!SameBitmap(legacy, natural) && SameBitmap(natural, repeat), "estrella Natural 2D tiene material propio determinista");
            Check(natural.GetPixel(127, 127).A == 255 && natural.GetPixel(0, 0).A == 0, "superficie Natural solida y cantos transparentes");
            if (baseline != null)
                using (Bitmap old = (Bitmap)Call(baseline.GetType(gpu.FullName, true), null, "BakeStar", detailed))
                    Check(SameBitmap(legacy, old), "estrella 2D Legacy pixel-identica al Release anterior");
            if (folder != null) natural.Save(Path.Combine(folder, detailed ? "protagonista-natural-detallada.png" : "protagonista-natural-simple.png"), ImageFormat.Png);
        }
        Check(Codigo.Llama(gpu.GetMethod("EnsureLuna", B), "BakeStarNatural"), "material 2D Natural conectado, no solo un metodo sin uso");
    }
    static void Meteors()
    {
        Array vertices = Make(12);
        foreach (float life in new float[] { float.NaN, -1, 0, .2f, .7f, 1, 2 })
            foreach (float width in new float[] { .8f, 2, 4 })
            {
                Call(stars, null, "NaturalShootVertices", vertices, 0, 130f, 100f, 400f, 200f, width, .2f, life, .8f, 0xFFC0E0u);
                float tailWidth = (float)Math.Sqrt(Math.Pow(X(vertices, 4) - X(vertices, 5), 2) + Math.Pow(Y(vertices, 4) - Y(vertices, 5), 2));
                Check(tailWidth < width * .1, "cola afilada, no cinta rectangular");
                for (int i = 0; i < 12; i++) Check(!float.IsNaN(X(vertices, i)) && !float.IsInfinity(Y(vertices, i)), "geometria finita");
                Check((Col(vertices, 2) >> 24) == 0 && (Col(vertices, 5) >> 24) == 0, "cola desvanece a transparente");
                if (!(life > 0)) for (int i = 0; i < 12; i++) Check((Col(vertices, i) >> 24) == 0, "fugaz sin vida no deja cabeza visible");
            }
        Check(Codigo.Llama(stars.GetMethod("UpdateAndDrawShoots", B), "GpuStarfield.NaturalShootVertices"), "fugaces activas usan acabado natural");
    }
    static void MetalAndEchoes(Assembly baseline)
    {
        uint[] palette = { 0xFF000000, 0xFFFFFFFF, 0xFFFF4A9C, 0xFF2CDCF4, 0xFF20DD45 };
        foreach (uint color in palette)
            foreach (float light in new float[] { .2f, .4f, .7f, 1.3f })
                foreach (float reflection in new float[] { 0f, .2f, 1f })
                {
                    uint value = (uint)Call(mesh, null, "ColorCaraNatural", color, light, reflection, 255u);
                    Check((value >> 24) == 255, "material conserva opacidad");
                    if (baseline != null) Check(Call(mesh, null, "ColorCara", color, light, reflection, 255u).Equals(Call(baseline.GetType(mesh.FullName, true), null, "ColorCara", color, light, reflection, 255u)), "material Legacy intacto");
                }
        uint dark = (uint)Call(mesh, null, "ColorCaraNatural", 0xFF000000u, .7f, 0f, 255u);
        Check((dark & 255) > ((dark >> 8) & 255) && ((dark >> 8) & 255) > ((dark >> 16) & 255), "caratula negra conserva metal azul acero");
        foreach (bool threeD in new bool[] { false, true })
        {
            float previous = 1;
            for (int i = 0; i <= 200; i++)
            {
                double age = .06 + i * .009;
                float alpha = (float)Call(gpu, null, "StarEchoAlphaNatural", age, 1f, threeD);
                Check(alpha >= 0 && alpha <= .72f && alpha <= previous + 1e-5, "ecos decrecen sin rebotes");
                previous = alpha;
            }
            foreach (double age in new double[] { double.NaN, double.PositiveInfinity, -1, 0, 2 }) Check((float)Call(gpu, null, "StarEchoAlphaNatural", age, 1f, threeD) == 0, "ecos fuera de vida invisibles");
        }
        Check(!Codigo.Llama(gpu.GetMethod("PoseEstrella", B), "Random.NextDouble"), "material no usa azar por cuadro");
        Check(!Codigo.Llama(stars.GetMethod("BuildStarVerts", B), "Math.Sin"), "campo mantiene tabla de titileo, sin senos extra");
    }
    [StructLayout(LayoutKind.Sequential)] struct Locked { public int Pitch; public IntPtr Bits; }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int LockTexture(IntPtr texture, uint level, out Locked data, IntPtr rect, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int UnlockTexture(IntPtr texture, uint level);
    static D Native<D>(IntPtr instance, int slot) where D : class { return (D)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size), typeof(D)); }
    static bool UploadedAtlas(object field, Bitmap expected)
    {
        IntPtr texture = (IntPtr)Read(field, "glowTex"); Locked data = new Locked();
        Check(texture != IntPtr.Zero && Native<LockTexture>(texture, 19)(texture, 0, out data, IntPtr.Zero, 0x10) >= 0, "bloquear atlas real HAL");
        try
        {
            for (int y = 0; y < expected.Height; y++)
                for (int x = 0; x < expected.Width; x++)
                    if (Marshal.ReadInt32(new IntPtr(data.Bits.ToInt64() + y * data.Pitch + x * 4)) != expected.GetPixel(x, y).ToArgb()) return false;
            return true;
        }
        finally { Check(Native<UnlockTexture>(texture, 20)(texture, 0) >= 0, "soltar atlas real HAL"); }
    }
    static void Hardware()
    {
        using (Control control = new Control())
        {
            control.Size = new Size(320, 240);
            object field = Activator.CreateInstance(stars, B, null, new object[] { control.Handle, 320, 240, 7731 }, null);
            try
            {
                Check((bool)stars.GetProperty("Ok", B).GetValue(field, null), "dispositivo real HAL disponible");
                object device = Read(field, "gpu");
                Console.WriteLine("HAL: " + device.GetType().GetProperty("Quien", B).GetValue(device, null));
                int attempts = 0;
                Write(field, "CreateStarAtlasForTest", (Func<Bitmap, IntPtr>)delegate(Bitmap bitmap) { attempts++; return attempts == 1 ? IntPtr.Zero : (IntPtr)Call(device.GetType(), device, "CreateTextureFromBitmap", bitmap); });
                Write(field, "FondoNatural", true);
                IntPtr original = (IntPtr)Read(field, "glowTex");
                Call(stars, field, "ActualizaAtlasNatural");
                Check(attempts == 1 && !(bool)Read(field, "glowNatural") && (IntPtr)Read(field, "glowTex") == original, "fallo transitorio HAL conserva atlas y estado reales");
                Call(stars, field, "ActualizaAtlasNatural");
                Check(attempts == 1, "HAL no reintenta antes de backoff");
                Write(field, "atlasRetryMs", 0.0);
                Call(stars, field, "ActualizaAtlasNatural");
                Check(attempts == 2 && (bool)Read(field, "glowNatural"), "HAL se recupera sin nuevo toggle");
                Write(field, "CreateStarAtlasForTest", null);
                Write(field, "FondoNatural", false);
                Call(stars, field, "ActualizaAtlasNatural");
                using (Bitmap legacy = (Bitmap)Call(stars, null, "BakeStarTexture", 64))
                using (Bitmap natural = (Bitmap)Call(stars, null, "BakeNaturalStarTexture", 64))
                {
                    Check(UploadedAtlas(field, legacy), "atlas inicial HAL es Legacy exacto");
                    foreach (bool style in new bool[] { true, false, true, false })
                    {
                        Write(field, "FondoNatural", style);
                        Check((bool)Call(stars, field, "Begin", 320, 240, .4f, .4f), "Begin HAL al cambiar estilo");
                        Call(stars, field, "End");
                        Check(UploadedAtlas(field, style ? natural : legacy), "atlas HAL corresponde al estilo, incluido retorno Legacy");
                        IntPtr before = (IntPtr)Read(field, "glowTex");
                        for (int i = 0; i < 15; i++)
                        {
                            Check((bool)Call(stars, field, "Begin", 320, 240, .4f, .4f), "Begin HAL estable");
                            Call(stars, field, "End");
                            Check(before == (IntPtr)Read(field, "glowTex"), "no reconstruye/sube atlas en cuadros estables");
                        }
                    }
                }
            }
            finally { Call(stars, field, "Dispose"); }
        }
    }
    static void TransientFailure()
    {
        object field = FormatterServices.GetUninitializedObject(stars);
        int attempts = 0;
        Write(field, "FondoNatural", true);
        Write(field, "CreateStarAtlasForTest", (Func<Bitmap, IntPtr>)delegate(Bitmap bitmap) { attempts++; return IntPtr.Zero; });
        try
        {
            Call(stars, field, "ActualizaAtlasNatural");
            object baked = Read(field, "naturalStarAtlas");
            Check(attempts == 1 && !(bool)Read(field, "glowNatural") && baked != null, "crear textura fallida no marca estilo activo");
            Call(stars, field, "ActualizaAtlasNatural");
            Check(attempts == 1, "backoff evita repeticion por cuadro");
            Write(field, "atlasRetryMs", 0.0);
            Call(stars, field, "ActualizaAtlasNatural");
            Check(attempts == 2 && object.ReferenceEquals(baked, Read(field, "naturalStarAtlas")), "retry reutiliza bitmap sin hornearlo");
            Write(field, "FondoNatural", false); Call(stars, field, "ActualizaAtlasNatural");
            Write(field, "FondoNatural", true); Call(stars, field, "ActualizaAtlasNatural");
            Check(attempts == 3, "cambio de selector cancela la espera anterior");
        }
        finally { Call(stars, field, "Dispose"); }
        Check(Read(field, "naturalStarAtlas") == null && Read(field, "legacyStarAtlas") == null, "Dispose libera caches RAM pequenas");
    }
    static double Timed(MethodInfo method, object instance, object[] parameters, int loops)
    {
        for (int i = 0; i < 30; i++) method.Invoke(instance, parameters);
        double best = double.MaxValue;
        for (int series = 0; series < 3; series++)
        {
            Stopwatch timer = Stopwatch.StartNew();
            for (int i = 0; i < loops; i++) method.Invoke(instance, parameters);
            timer.Stop(); best = Math.Min(best, timer.Elapsed.TotalMilliseconds / loops);
        }
        return best;
    }
    static void IsolatedCost()
    {
        object field = Field(stars, 3440, 1440);
        object[] parameters = { Make(1400 * 6), Read(field, "sx"), Read(field, "sy"), Read(field, "sphase"), Read(field, "sspeed"), Read(field, "ssize"), Read(field, "skind"), Read(field, "sforma"), Read(field, "scol"), 1400, 53.3, .83f, .9f, new float[] { 3, 5, 8 }, new float[] { 2, 3, 4 }, 3440f, 1440f };
        MethodInfo method = stars.GetMethod("BuildStarVerts", B);
        double legacy = Timed(method, field, parameters, 400);
        Write(field, "FondoNatural", true);
        double natural = Timed(method, field, parameters, 400);
        Console.WriteLine("Coste aislado CPU de 1400 estrellas, con reflexion: Legacy " + legacy.ToString("0.000") + " ms; Natural " + natural.ToString("0.000") + " ms. No son FPS de la aplicacion.");
        foreach (string name in new string[] { "BuildStarVerts", "NaturalShootVertices" })
            foreach (Codigo.Paso step in Codigo.Pasos(stars.GetMethod(name, B))) Check(step.Op != 0x8D && step.Op != 0x73, "sin reservas en ruta estelar por cuadro");
        foreach (string name in new string[] { "ColorCaraNatural", "ColorEcoNatural", "StarEchoAlphaNatural" })
        {
            Type owner = name == "ColorCaraNatural" ? mesh : gpu;
            foreach (Codigo.Paso step in Codigo.Pasos(owner.GetMethod(name, B))) Check(step.Op != 0x8D && step.Op != 0x73, "sin reservas en material/eco por cuadro");
        }
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            Assembly program = Assembly.LoadFile(Path.GetFullPath(args[0]));
            Assembly baseline = args.Length > 1 && args[1] != "--sin-base" ? Assembly.LoadFile(Path.GetFullPath(args[1])) : null;
            string folder = args.Length > 2 ? Path.GetFullPath(args[2]) : null;
            if (folder != null) Directory.CreateDirectory(folder);
            stars = program.GetType("Rocola.GpuStarfield", true); gpu = program.GetType("Rocola.GpuPanel", true); mesh = program.GetType("Rocola.Estrella3d", true); vertex = program.GetType("Rocola.D3D9+Vtx", true);
            BudgetAndLegacy(baseline); Textures(baseline, folder); DistributionAndSurface(baseline, folder); Meteors(); MetalAndEchoes(baseline); IsolatedCost(); TransientFailure();
            if (args.Length > 3 && args[3] == "--hal") Hardware();
            Console.WriteLine("OK naturalestelar: " + checks + " comprobaciones; Legacy " + (baseline != null ? "contrastado con ejecutable anterior" : "retorno exacto, sin referencia externa") + "; mismas capas/mallas/ecos. No es medicion FPS de la aplicacion.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
