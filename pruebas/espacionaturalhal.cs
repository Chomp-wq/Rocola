using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Fixture aislada: HAL real, sin Form1, biblioteca, musica ni configuracion del usuario.
// Los tiempos son construccion/envio de cuadros calentados, NO FPS reales ni tiempo GPU.
class EspacioNaturalHal
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type P, S, D, A, F;
    static Control panel;
    static object sky, device, aurora;
    static int checks, stage;
    static int[] calls = new int[6], tris = new int[6];
    static DrawUp nativeDraw;
    static Delegate originalDraw;
    static double now = 100;
    static object Get(object o, string n) { return o.GetType().GetField(n, All).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, All).SetValue(o, v); }
    static object Call(Type t, object o, string n, params object[] args)
    {
        try { return t.GetMethod(n, All).Invoke(o, args); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; Console.WriteLine("OK " + message); }
    static Delegate Hook(string field, string method)
    {
        return Delegate.CreateDelegate(S.GetField(field, All).FieldType, typeof(EspacioNaturalHal).GetMethod(method, All));
    }
    static void Background()
    {
        stage = 1; Call(P, panel, "DibujaNieblas");
        stage = 2; Call(P, panel, "DrawAurora", aurora);
        stage = 3; Call(P, panel, "DibujaRuido");
        stage = 0;
    }
    static void Protagonist() { stage = 4; Call(P, panel, "DibujaLuna"); stage = 0; }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int DrawUp(IntPtr self, uint kind, uint count, IntPtr data, uint stride);
    static int ObserveDraw(IntPtr self, uint kind, uint count, IntPtr data, uint stride)
    {
        calls[stage]++; tris[stage] += (int)count;
        return nativeDraw(self, kind, count, data, stride);
    }
    static void Observe(bool enabled)
    {
        FieldInfo field = D.GetField("drawUp", All);
        if (enabled)
        {
            originalDraw = (Delegate)field.GetValue(device);
            nativeDraw = Fn<DrawUp>((IntPtr)Get(device, "dev"), 83);
            field.SetValue(device, Delegate.CreateDelegate(field.FieldType, typeof(EspacioNaturalHal).GetMethod("ObserveDraw", All)));
            Set(device, "verticesSinSoporte", true); // contador UP; despues se restaura el regulador real VB/UP
        }
        else { field.SetValue(device, originalDraw); Set(device, "verticesSinSoporte", false); nativeDraw = null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Desc { public uint Format, Type, Usage, Pool, Multi, Quality, Width, Height; }
    [StructLayout(LayoutKind.Sequential)]
    struct Locked { public int Pitch; public IntPtr Data; }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Hr(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint Rel(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Back(IntPtr self, uint a, uint b, uint c, out IntPtr surface);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Description(IntPtr self, out Desc desc);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Offscreen(IntPtr self, uint w, uint h, uint format, uint pool, out IntPtr surface, IntPtr shared);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Readback(IntPtr self, IntPtr source, IntPtr dest);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Lock(IntPtr self, out Locked r, IntPtr region, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Present(IntPtr self, IntPtr source, IntPtr destination, IntPtr window, IntPtr dirty);
    static T Fn<T>(IntPtr p, int slot) where T : class
    { return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(p), slot * IntPtr.Size), typeof(T)); }
    static void Release(IntPtr p) { if (p != IntPtr.Zero) Fn<Rel>(p, 2)(p); }
    static void EndScene()
    {
        IntPtr dev = (IntPtr)Get(device, "dev");
        if (Fn<Hr>(dev, 42)(dev) < 0) throw new Exception("EndScene HAL");
        Set(device, "inScene", false); Call(D, device, "SueltaPinesVertices");
    }
    static void PresentScene()
    {
        IntPtr dev = (IntPtr)Get(device, "dev");
        if (Fn<Present>(dev, 17)(dev, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) < 0)
            throw new Exception("Present HAL");
    }
    static int[] ReadPixels(int w, int h)
    {
        IntPtr dev = (IntPtr)Get(device, "dev"), back = IntPtr.Zero, off = IntPtr.Zero;
        bool locked = false;
        try
        {
            Check(Fn<Back>(dev, 18)(dev, 0, 0, 0, out back) >= 0, "backbuffer HAL");
            Desc desc; Check(Fn<Description>(back, 12)(back, out desc) >= 0, "descripcion HAL");
            Check(desc.Width == w && desc.Height == h, "backbuffer a resolucion exacta " + w + "x" + h);
            Check(Fn<Offscreen>(dev, 36)(dev, desc.Width, desc.Height, desc.Format, 2, out off, IntPtr.Zero) >= 0, "superficie de lectura HAL");
            Check(Fn<Readback>(dev, 32)(dev, back, off) >= 0, "readback HAL real");
            Locked r; Check(Fn<Lock>(off, 13)(off, out r, IntPtr.Zero, 0x10) >= 0, "lock HAL"); locked = true;
            int[] pixels = new int[w * h];
            for (int y = 0; y < h; y++) Marshal.Copy(new IntPtr(r.Data.ToInt64() + (long)y * r.Pitch), pixels, y * w, w);
            return pixels;
        }
        finally { if (locked) Fn<Hr>(off, 14)(off); Release(off); Release(back); }
    }
    static void Save(int[] pixels, int w, int h, string file)
    {
        using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            int[] opaque = (int[])pixels.Clone();
            for (int i = 0; i < opaque.Length; i++) opaque[i] |= unchecked((int)0xFF000000u);
            BitmapData data = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { for (int y = 0; y < h; y++) Marshal.Copy(opaque, y * w, new IntPtr(data.Scan0.ToInt64() + y * (long)data.Stride), w); }
            finally { b.UnlockBits(data); }
            b.Save(file, ImageFormat.Png);
        }
    }

    static void Configure(int w, int h, bool natural)
    {
        Set(panel, "FondoNatural", natural); Set(sky, "FondoNatural", natural);
        uint top = (uint)Call(P, null, "CieloEstilo", 0xFF06081Au, 0, natural);
        uint middle = (uint)Call(P, null, "CieloEstilo", 0xFF101C3Au, 1, natural);
        uint bottom = (uint)Call(P, null, "CieloEstilo", 0xFF1A2C5Au, 2, natural);
        Call(S, sky, "SetSky", top, middle, bottom);
        Call(S, sky, "SetEfectos", 1f, 0, 0, w, h);
        Call(S, sky, "SetDim", 1f);
        Set(panel, "Nieblas", 2); Set(panel, "NebColor", 0x005898D8u); Set(panel, "NebColor2", 0x009C70E8u);
        Set(panel, "NebFase", .35f); Set(panel, "HazFase", .18f);
        Set(panel, "VinetaSuave", true); Set(panel, "CieloProfundo", true);
        Set(panel, "ConLuna", true); Set(panel, "Estrella3D", true);
        Set(panel, "EstelaEstelar", true); Set(panel, "LunaActiva", true);
        Set(panel, "idleBlend", 1f); Set(panel, "idleH", (int)Call(P, null, "IdlePanelHeight", h)); Set(panel, "uiBotH", 68);
        Set(panel, "cuadroMs", now * 1000);
        Set(panel, "starTravelX", -.85); Set(panel, "starTravelY", 1.4);
        Set(panel, "starYaw", .45); Set(panel, "moonMotionTime", 3.5);
        Set(panel, "Dim", 1f);
        Set(panel, "OndasSuaves", true); Set(panel, "AuroraBandas", true);
        Set(panel, "aurT", 18d); Set(panel, "aurRelojOk", false);
        Set(panel, "waveNatural", !natural); // fuerza muestreo inicial de esta fixture y del nuevo estilo
        aurora = Activator.CreateInstance(A);
        Set(aurora, "Has", true); Set(aurora, "B", new float[] { .3f, .66f, .91f, .57f, .43f, .72f, .83f, .31f });
        Set(aurora, "BaseY", h - (float)(int)Get(panel, "idleH"));
        Set(aurora, "Alto", h * .39f);
        Set(aurora, "Rgb", 0x00953FC9u); Set(aurora, "Rgb2", 0x004169CEu);
        Set(aurora, "RgbA", 0x00F27BC8u); Set(aurora, "Rgb2A", 0x0088B6EDu);
        Set(aurora, "Dim", 1f); Set(aurora, "DimFilo", 1f); Set(aurora, "Calma", 0f);
        // Paleta de portada sintética, fija e independiente de la biblioteca.
        uint[] palette = (uint[])Get(panel, "flowColors");
        uint[] colors = { 0xEC63BEu, 0x953FC9u, 0x4C80D2u, 0x75C5DFu };
        for (int i = 0; i < palette.Length; i++) palette[i] = colors[(i * colors.Length / palette.Length) % colors.Length];
        uint[] cover = (uint[])Get(panel, "coverBands");
        float[] target = (float[])Get(panel, "coverTarget"), current = (float[])Get(panel, "coverCurrent");
        for (int i = 0; i < cover.Length; i++)
        {
            uint rgb = colors[(i * colors.Length / cover.Length) % colors.Length]; cover[i] = rgb;
            target[i * 3] = current[i * 3] = (rgb >> 16) & 255;
            target[i * 3 + 1] = current[i * 3 + 1] = (rgb >> 8) & 255;
            target[i * 3 + 2] = current[i * 3 + 2] = rgb & 255;
        }
        Set(panel, "coverTargetWeight", 1f); Set(panel, "coverColorsReady", true);
        Set(panel, "flowBlend", .55f); Set(panel, "coverWeight", 1f);
        Call(P, panel, "EnsureNieblas"); Call(P, panel, "EnsureVineta"); Call(P, panel, "EnsureLuna");
        if (natural)
        {
            // Natural puede preparar sus mapas neutros fuera del hilo de dibujo.
            // Tener IntPtr no basta: durante la preparacion se conserva el estilo anterior.
            // Esperar la adopcion correcta, fuera de Begin/EndScene, no el horneado por cuadro.
            Stopwatch ready = Stopwatch.StartNew();
            while (!(bool)Get(panel, "nebEstiloDe") || !(bool)Get(panel, "hazEstiloDe")
                || (IntPtr)Get(panel, "nebTex") == IntPtr.Zero || (IntPtr)Get(panel, "hazTex") == IntPtr.Zero)
            {
                if (ready.Elapsed.TotalSeconds > 5) throw new Exception("Preparacion Natural HAL no finalizo en cinco segundos");
                System.Threading.Thread.Sleep(5); Call(P, panel, "EnsureNieblas");
            }
        }
        Check((IntPtr)Get(panel, "nebTex") != IntPtr.Zero && (IntPtr)Get(panel, "hazTex") != IntPtr.Zero
            && (IntPtr)Get(panel, "vinetaTex") != IntPtr.Zero && (IntPtr)Get(panel, "lunaTex") != IntPtr.Zero, "texturas preparadas antes de Begin");
        Set(sky, "TrasCielo", Hook("TrasCielo", "Background")); Set(sky, "TrasEstrellas", Hook("TrasEstrellas", "Protagonist"));
        FixedScene();
    }
    static void FixedScene()
    {
        Set(sky, "campoRelojOk", false); Set(sky, "campoT", 18d); Set(sky, "driftFinoAnt", 0d);
        Set(panel, "aurRelojOk", false); Set(panel, "aurT", 18d);
        Array echoes = (Array)Get(panel, "starEchoes");
        PointF center = (PointF)Call(P, panel, "StarSceneCenter");
        float diameter = (float)Call(P, null, "DetailedMoonDiameter", panel.Height, 1f)
            * (float)Call(P, null, "MoonSceneScale", 1f, true);
        float radius = diameter * .5f * (float)P.GetField("Estrella3dEscala", All).GetRawConstantValue();
        for (int i = 0; i < 12; i++)
        {
            object echo = Activator.CreateInstance(echoes.GetType().GetElementType());
            Set(echo, "Time", now - (.10 + i * .035));
            Set(echo, "X", center.X - radius * .55f * (12 - i));
            Set(echo, "Y", center.Y + radius * .10f * (float)Math.Sin(i * .55));
            Set(echo, "Radius", radius); Set(echo, "Angle", .17f);
            Set(echo, "Yaw", .45 + .03 * i); Set(echo, "Pitch", .16f);
            Set(echo, "Strength", .8f);
            Set(echo, "Color", new uint[] { 0xFFB862D5u, 0xFF6F88DCu, 0xFF70C2D3u, 0xFFE57DC1u }[i % 4]);
            echoes.SetValue(echo, i);
        }
        Set(panel, "starEchoCount", 12); Set(panel, "starEchoHead", 0);
        Set(panel, "starEchoClock", now); Set(panel, "starEchoVisibility", 1f);
        Set(panel, "starEchoWidth", panel.Width); Set(panel, "starEchoHeight", panel.Height);
        Set(panel, "starEchoHayUlt", true); Set(panel, "starEchoUltX", center.X); Set(panel, "starEchoUltY", center.Y);
        Set(panel, "starEchoCamino", 0f);
    }
    static void Draw()
    {
        stage = 0;
        if (!(bool)Call(S, sky, "Begin", panel.Width, panel.Height, .32f, .32f)) throw new Exception("Begin HAL");
        stage = 5;
        Call(P, panel, "DibujaTeselas", Get(panel, "vinetaTex"), Get(panel, "vinetaTeselas"), 0f, 0f, (float)panel.Width, (float)panel.Height,
            Get(panel, "vinetaU"), Get(panel, "vinetaV"), 0xFFFFFFFFu, Get(panel, "adornoQuad"));
        stage = 0;
    }

    static void Cache()
    {
        bool original = (bool)Get(panel, "FondoNatural");
        FieldInfo force = F.GetField("nebNaturalFuerza", All); float saved = (float)force.GetValue(null);
        try
        {
            Set(panel, "CieloProfundo", true); force.SetValue(null, .55f);
            Bitmap[] neb = new Bitmap[2], via = new Bitmap[2];
            for (int i = 0; i < 2; i++)
            {
                Set(panel, "FondoNatural", i == 1);
                neb[i] = (Bitmap)Call(P, panel, "MapaNebEstilo", 120, 90);
                via[i] = (Bitmap)Call(P, panel, "MapaViaEstilo", 120, 90);
            }
            for (int pass = 0; pass < 8; pass++)
            {
                int i = pass % 2; Set(panel, "FondoNatural", i == 1);
                Check(ReferenceEquals(neb[i], Call(P, panel, "MapaNebEstilo", 120, 90)), "selector reutiliza mapa neb estilo " + i);
                Check(ReferenceEquals(via[i], Call(P, panel, "MapaViaEstilo", 120, 90)), "selector reutiliza mapa via estilo " + i);
            }
            Check(!ReferenceEquals(neb[0], neb[1]) && !ReferenceEquals(via[0], via[1]), "estilos conservan mapas separados sin mezclarlos");
            Set(panel, "FondoNatural", true); Set(panel, "CieloProfundo", false);
            Bitmap simple = (Bitmap)Call(P, panel, "MapaNebEstilo", 120, 90);
            Check(!ReferenceEquals(simple, neb[1]), "cambio de detalle invalida solo nebulosa activa");
            Check(ReferenceEquals(via[1], Call(P, panel, "MapaViaEstilo", 120, 90)), "cambio detalle no rehornea via");
            force.SetValue(null, .2f);
            Check(!ReferenceEquals(simple, Call(P, panel, "MapaNebEstilo", 120, 90)), "fuerza nueva entra en la clave");
            Bitmap resized = (Bitmap)Call(P, panel, "MapaViaEstilo", 140, 85);
            Check(!ReferenceEquals(resized, via[1]) && resized.Size == new Size(140, 85), "tamano nuevo invalida via activa");
            Check(((Array)Get(panel, "mapasNebEstilo")).Length == 2 && ((Array)Get(panel, "mapasViaEstilo")).Length == 2, "cache acotada a dos mapas por familia");
        }
        finally { force.SetValue(null, saved); Set(panel, "CieloProfundo", true); Set(panel, "FondoNatural", original); }
    }
    static double Bench()
    {
        for (int i = 0; i < 48; i++) { Draw(); EndScene(); PresentScene(); }
        double sum = 0;
        Stopwatch clock = new Stopwatch();
        for (int i = 0; i < 72; i++)
        {
            clock.RestartCompat(); Draw(); EndScene(); sum += clock.Elapsed.TotalMilliseconds;
            PresentScene();
        }
        return sum / 72;
    }
    static void ColdPreparation(Size size)
    {
        FieldInfo force = F.GetField("nebNaturalFuerza", All); object saved = force.GetValue(null);
        try
        {
            Set(panel, "FondoNatural", true); Set(panel, "Nieblas", 0);
            Call(P, panel, "EnsureNieblas");
            Check(((Bitmap[])Get(panel, "mapasNebEstilo"))[1] == null
                && ((Bitmap[])Get(panel, "mapasViaEstilo"))[1] == null
                && Get(panel, "preparacionNatural") == null, "Natural apagado no prepara nebulosas");
            Check((IntPtr)Get(panel, "ruidoTex") != IntPtr.Zero, "apagar nebulosas conserva grano independiente");
            Set(panel, "Nieblas", 2); Set(panel, "CieloProfundo", true); force.SetValue(null, .31f);
            Call(P, panel, "PreparaFondoNatural");
            Check(Get(panel, "preparacionNatural") != null, "Natural frio usa preparador fuera de pantalla");
            panel.Size = new Size(size.Width + 64, size.Height + 32);
            Set(panel, "CieloProfundo", false); force.SetValue(null, .67f);
            Call(P, panel, "PreparaFondoNatural");
            Stopwatch ready = Stopwatch.StartNew();
            do
            {
                Call(P, panel, "EnsureNieblas");
                if ((IntPtr)Get(panel, "nebTex") != IntPtr.Zero && (bool)Get(panel, "nebEstiloDe")
                    && !(bool)Get(panel, "nebNaturalDe") && (float)Get(panel, "nebFuerzaDe") == .67f
                    && (int)Get(panel, "nebTW") == panel.Width / 4 && (int)Get(panel, "hazTW") == panel.Width / 4) break;
                if (ready.Elapsed.TotalSeconds > 5) throw new Exception("Natural frio/resize no adopta ultimo pedido");
                System.Threading.Thread.Sleep(5);
            } while (true);
            Check(((Bitmap[])Get(panel, "mapasNebEstilo"))[1].Size == new Size(panel.Width / 4, panel.Height / 4),
                "Natural frio adopta tamano/detalle/fuerza actuales y descarta obsoletos");
            // Simular grano en backoff, no un fallo real del controlador grafico.
            Release((IntPtr)Get(panel, "ruidoTex")); Set(panel, "ruidoTex", IntPtr.Zero);
            Set(panel, "naturalRuidoRetry", true); Set(panel, "naturalRuidoRetryMs", Environment.TickCount);
            Release((IntPtr)Get(panel, "nebTex")); Set(panel, "nebTex", IntPtr.Zero);
            Call(P, panel, "EnsureNieblas");
            Check((IntPtr)Get(panel, "nebTex") != IntPtr.Zero && (IntPtr)Get(panel, "ruidoTex") == IntPtr.Zero,
                "espera del grano no bloquea subida de nebulosa");
            Set(panel, "naturalRuidoRetry", false); Call(P, panel, "EnsureNieblas");
        }
        finally { panel.Size = size; force.SetValue(null, saved); Set(panel, "CieloProfundo", true); }
    }

    static void RestartNatural(Size size)
    {
        Configure(size.Width, size.Height, true);
        Bitmap neb = ((Bitmap[])Get(panel, "mapasNebEstilo"))[1], via = ((Bitmap[])Get(panel, "mapasViaEstilo"))[1];
        object oldDevice = device;
        Call(P, panel, "StopGpu");
        Check((IntPtr)Get(oldDevice, "dev") == IntPtr.Zero, "reinicio libera device anterior");
        Check((bool)Call(P, panel, "StartGpu"), "reinicio HAL disponible");
        sky = Get(panel, "stars"); device = Get(sky, "gpu");
        Configure(size.Width, size.Height, true);
        Check(ReferenceEquals(neb, ((Bitmap[])Get(panel, "mapasNebEstilo"))[1])
            && ReferenceEquals(via, ((Bitmap[])Get(panel, "mapasViaEstilo"))[1]), "reinicio reutiliza mapas CPU sin hornear");
        Draw(); EndScene(); PresentScene();
    }
    static void Run(Size size, string output)
    {
        using (Control control = (Control)Activator.CreateInstance(P, true))
        {
            panel = control; panel.Size = size; panel.Visible = false;
            Check((bool)Call(P, panel, "StartGpu"), "HAL disponible para " + size.Width + "x" + size.Height);
            sky = Get(panel, "stars"); device = Get(sky, "gpu");
            ColdPreparation(size);
            Cache();
            int[][] counts = new int[2][], triangles = new int[2][], frames = new int[2][];
            for (int style = 0; style < 2; style++)
            {
                Configure(size.Width, size.Height, style == 1);
                Draw(); EndScene(); PresentScene(); // calienta atlas, mapas y geometria antes de observar
                Observe(true);
                try
                {
                    Array.Clear(calls, 0, calls.Length); Array.Clear(tris, 0, tris.Length);
                    Draw(); EndScene();
                    counts[style] = (int[])calls.Clone(); triangles[style] = (int[])tris.Clone();
                    frames[style] = ReadPixels(size.Width, size.Height);
                    Save(frames[style], size.Width, size.Height, Path.Combine(output, (style == 1 ? "natural-" : "legacy-") + size.Width + "x" + size.Height + ".png"));
                    PresentScene();
                }
                finally { Observe(false); }
                Console.WriteLine("ENVIO " + (style == 1 ? "Natural" : "Legacy") + " " + size + " draws=[" + string.Join(",", Array.ConvertAll(counts[style], delegate(int n) { return n.ToString(); })) + "] tris=[" + string.Join(",", Array.ConvertAll(triangles[style], delegate(int n) { return n.ToString(); })) + "]");
            }
            foreach (int index in new int[] { 0, 2, 3, 4 })
                Check(counts[0][index] == counts[1][index] && triangles[0][index] == triangles[1][index], "misma geometria/envios fase " + index + " (0estrellas/2aurora/3grano/4protagonista+12ecos)");
            int oldCalls = 0, newCalls = 0;
            foreach (int count in counts[0]) oldCalls += count;
            foreach (int count in counts[1]) newCalls += count;
            Check(oldCalls == newCalls, "mismas llamadas totales: " + oldCalls + "; solo cambian teselas transparentes de niebla/vineta");
            int[] differing = frames[1]; int changed = 0; long red = 0, blue = 0;
            for (int i = 0; i < differing.Length; i++)
            {
                if ((frames[0][i] & 0xFFFFFF) != (differing[i] & 0xFFFFFF)) changed++;
                if (i / size.Width < size.Height / 10) { red += (differing[i] >> 16) & 255; blue += differing[i] & 255; }
            }
            Check(changed > differing.Length / 10, "estilos realmente diferentes en el backbuffer");
            Check(blue > red * 1.2, "parte superior conserva noche azul/negra");
            // Alternar vuelve a usar los mapas CPU, y mantiene una textura activa por familia.
            Bitmap[] nebMaps = (Bitmap[])((Bitmap[])Get(panel, "mapasNebEstilo")).Clone();
            Bitmap[] viaMaps = (Bitmap[])((Bitmap[])Get(panel, "mapasViaEstilo")).Clone();
            for (int pass = 0; pass < 8; pass++)
            {
                int style = pass % 2;
                Configure(size.Width, size.Height, style == 1);
                Check(ReferenceEquals(nebMaps[style], ((Bitmap[])Get(panel, "mapasNebEstilo"))[style]) && ReferenceEquals(viaMaps[style], ((Bitmap[])Get(panel, "mapasViaEstilo"))[style]), "alternancia calentada no rehornea CPU");
                IntPtr texture = (IntPtr)Get(panel, "nebTex");
                Call(P, panel, "EnsureNieblas");
                Check((IntPtr)Get(panel, "nebTex") == texture, "cuadro sin cambios no vuelve a subir nebulosa");
                Draw(); EndScene(); PresentScene();
            }
            for (int pass = 0; pass < 2; pass++)
                for (int order = 0; order < 2; order++)
                {
                    bool natural = (order + pass) % 2 == 1;
                    Configure(size.Width, size.Height, natural);
                    Console.WriteLine("COSTE " + size + " " + (natural ? "Natural" : "Legacy") + ": " + Bench().ToString("0.000") + "ms/cuadro calentado, CPU+envios, sin Present/readback. Fixture sintetica; NO FPS reales.");
                }
            RestartNatural(size);
            Call(P, panel, "StopGpu");
            Check((IntPtr)Get(device, "dev") == IntPtr.Zero, "HAL y texturas liberados al cerrar fixture");
            Call(P, panel, "LiberaMapasEstilo");
            foreach (Bitmap b in (Bitmap[])Get(panel, "mapasNebEstilo")) Check(b == null, "cache CPU neb liberada");
            foreach (Bitmap b in (Bitmap[])Get(panel, "mapasViaEstilo")) Check(b == null, "cache CPU via liberada");
        }
        panel = null; sky = device = aurora = null;
    }
    [STAThread]
    static int Main(string[] args)
    {
        bool skip = Array.IndexOf(args, "--sin-hal") >= 0;
        PlatformID platform = Environment.OSVersion.Platform;
        bool windows = platform == PlatformID.Win32NT || platform == PlatformID.Win32Windows;
        if (skip || !windows)
        {
            Console.WriteLine("OMISION EXPLICITA: fixture HAL no ejecutada (" + (skip ? "--sin-hal" : "plataforma no Windows") + "). No confirma aspecto ni rendimiento GPU.");
            return 0;
        }
        string before = Environment.GetEnvironmentVariable("ROCOLA_DATOS");
        string output = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(Path.GetTempPath(), "rocola-natural-hal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", Path.Combine(output, "datos-fixture"));
        try
        {
            Assembly a = Assembly.LoadFrom(args[0]); P = a.GetType("Rocola.GpuPanel"); S = a.GetType("Rocola.GpuStarfield");
            D = a.GetType("Rocola.D3D9Device"); A = a.GetType("Rocola.GpuAurora"); F = a.GetType("Rocola.Form1");
            if (P == null) { Console.WriteLine("OK CPU congelado: no se genera una fixture GPU."); return 0; }
            foreach (Size size in new Size[] { new Size(1024, 768), new Size(3440, 1440) }) Run(size, output);
            Console.WriteLine("PASS " + checks + " verificaciones HAL. Capturas: " + output);
            return 0;
        }
        catch (Exception e) { Console.WriteLine("FALLO " + e); return 1; }
        finally { Environment.SetEnvironmentVariable("ROCOLA_DATOS", before); }
    }
}
static class StopwatchCompat
{
    internal static void RestartCompat(this Stopwatch clock) { clock.Reset(); clock.Start(); }
}
