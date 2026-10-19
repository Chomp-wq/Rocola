using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Forms;


// INVENTARIO DE COSTES DEL CUADRO DE GPU (19-10-2026): con la tarjeta imitada, cuanto cuesta en procesador y en memoria un cuadro
// completo del fondo, capa por capa, y cuanta pantalla pinta. No mide velocidad de tarjeta; es la base de la ronda de optimizacion.
class Inventario
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos, oks;
    static bool soloContar; static long llamadas, trisTotal;
    static void Exige(bool c, string q) { if (c) { oks++; Console.WriteLine("  OK    " + q); } else { fallos++; Console.WriteLine("  FALLO " + q); } }

    static Type TPanel, TStar, TDev, TD3D, TVtx, TVtx2, TForm;
    static FieldInfo Campo(Type t, string n)
    {
        for (; t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, TODO | BindingFlags.DeclaredOnly); if (f != null) return f; }
        return null;
    }
    static void Pon(object o, string n, object v) { FieldInfo f = Campo(o.GetType(), n); if (f == null) throw new Exception("falta el campo " + n); f.SetValue(o, v); }
    static object Toma(object o, string n) { FieldInfo f = Campo(o.GetType(), n); if (f == null) throw new Exception("falta el campo " + n); return f.GetValue(o); }
    static MethodInfo Metodo(Type t, string n)
    {
        for (; t != null; t = t.BaseType) { MethodInfo m = t.GetMethod(n, TODO | BindingFlags.DeclaredOnly); if (m != null) return m; }
        return null;
    }
    static object Llama(object o, string n, params object[] a) { return LlamaEn(o.GetType(), o, n, a); }
    static object Estatico(Type t, string n, params object[] a) { return LlamaEn(t, null, n, a); }
    static object LlamaEn(Type t, object o, string n, object[] a)
    {
        MethodInfo m = Metodo(t, n);
        if (m == null) throw new Exception("falta " + t.Name + "." + n);
        try { return m.Invoke(o, a); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static object Prop(object o, string n) { PropertyInfo p = o.GetType().GetProperty(n, TODO); if (p == null) throw new Exception("falta " + n); return p.GetValue(o, null); }

    // ======================= LA TARJETA IMITADA =======================
    static readonly List<object> vivos = new List<object>();
    static uint Nada(IntPtr self) { return 0; }
    static uint Memoria(IntPtr self) { return 256u * 1024 * 1024; }
    static IntPtr Puntero(string tipo, string metodo)
    {
        Delegate del = Delegate.CreateDelegate(TD3D.GetNestedType(tipo, TODO), typeof(Inventario).GetMethod(metodo, TODO));
        vivos.Add(del); return Marshal.GetFunctionPointerForDelegate(del);
    }
    sealed class Textura { public int W, H; public int[] Px; public IntPtr Mem; public bool Destino; public bool Sucia = true; public IntPtr Sup; }
    static readonly Dictionary<long, Textura> texturas = new Dictionary<long, Textura>();
    static readonly Dictionary<long, Textura> superficies = new Dictionary<long, Textura>();   // superficie -> su textura
    static IntPtr vtTex, vtSup, fondo;
    static int offPitch, offBits, soltadas, soltadasDestino, reinicios;
    static void PreparaTexturas()
    {
        if (vtTex != IntPtr.Zero) return;
        Type tlr = TD3D.GetNestedType("LockedRect", TODO);
        offPitch = (int)Marshal.OffsetOf(tlr, "Pitch"); offBits = (int)Marshal.OffsetOf(tlr, "Bits");
        Type tLock = TD3D.GetNestedType("LockRectDel", TODO);
        ParameterInfo[] ps = tLock.GetMethod("Invoke").GetParameters();
        Type[] pt = new Type[ps.Length]; for (int i = 0; i < ps.Length; i++) pt[i] = ps[i].ParameterType;
        DynamicMethod dm = new DynamicMethod("BloqueaImitada", typeof(int), pt, typeof(Inventario).Module, true);
        ILGenerator il = dm.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Conv_U);
        il.Emit(OpCodes.Call, typeof(Inventario).GetMethod("Bloquea", TODO));
        il.Emit(OpCodes.Ret);
        Delegate lk = dm.CreateDelegate(tLock); vivos.Add(lk);
        // GetSurfaceLevel(self, level, out superficie): con el tipo de delegado de la rocola (GetPtrDel), igual que arriba.
        Type tGet = TD3D.GetNestedType("GetPtrDel", TODO);
        ps = tGet.GetMethod("Invoke").GetParameters();
        pt = new Type[ps.Length]; for (int i = 0; i < ps.Length; i++) pt[i] = ps[i].ParameterType;
        DynamicMethod dg = new DynamicMethod("SuperficieImitada", typeof(int), pt, typeof(Inventario).Module, true);
        il = dg.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Conv_U);
        il.Emit(OpCodes.Call, typeof(Inventario).GetMethod("Superficie", TODO));
        il.Emit(OpCodes.Ret);
        Delegate sl = dg.CreateDelegate(tGet); vivos.Add(sl);
        vtTex = Marshal.AllocHGlobal(64 * IntPtr.Size);
        for (int i = 0; i < 64; i++) Marshal.WriteIntPtr(vtTex, i * IntPtr.Size, Puntero("UintSelf", "Nada"));
        Marshal.WriteIntPtr(vtTex, 2 * IntPtr.Size, Puntero("UintSelf", "Suelta"));
        Marshal.WriteIntPtr(vtTex, 18 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(sl));
        Marshal.WriteIntPtr(vtTex, 19 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(lk));
        Marshal.WriteIntPtr(vtTex, 20 * IntPtr.Size, Puntero("UnlockRectDel", "Desbloquea"));
        vtSup = Marshal.AllocHGlobal(64 * IntPtr.Size);
        for (int i = 0; i < 64; i++) Marshal.WriteIntPtr(vtSup, i * IntPtr.Size, Puntero("UintSelf", "Nada"));
        fondo = Marshal.AllocHGlobal(16); Marshal.WriteIntPtr(fondo, vtSup);
    }
    static int CreaTex(IntPtr self, uint w, uint h, uint levels, uint usage, uint format, uint pool, out IntPtr tex, IntPtr shared)
    {
        PreparaTexturas();
        IntPtr o = Marshal.AllocHGlobal(16); Marshal.WriteIntPtr(o, vtTex);
        Textura t = new Textura(); t.W = (int)w; t.H = (int)h; t.Destino = (usage & 1) != 0;
        t.Px = new int[w * h];
        Random r = new Random((int)(w * 31 + h));   // basura: lo que no se escribe no puede parecer bueno
        for (int i = 0; i < t.Px.Length; i++) t.Px[i] = r.Next() ^ (r.Next() << 16);
        t.Mem = Marshal.AllocHGlobal((int)(w * h * 4));
        Marshal.Copy(t.Px, 0, t.Mem, t.Px.Length);
        texturas[o.ToInt64()] = t; tex = o;
        return 0;
    }
    static int Bloquea(IntPtr self, IntPtr lr) { Textura t = texturas[self.ToInt64()]; Marshal.WriteInt32(lr, offPitch, t.W * 4); Marshal.WriteIntPtr(lr, offBits, t.Mem); t.Sucia = true; return 0; }
    static int Desbloquea(IntPtr self, uint level) { return 0; }
    static uint Suelta(IntPtr self)
    {
        Textura t;
        if (texturas.TryGetValue(self.ToInt64(), out t)) { soltadas++; if (t.Destino) soltadasDestino++; }
        return 0;
    }
    static int Superficie(IntPtr self, IntPtr salida)
    {
        Textura t = texturas[self.ToInt64()];
        if (t.Sup == IntPtr.Zero) { t.Sup = Marshal.AllocHGlobal(16); Marshal.WriteIntPtr(t.Sup, vtSup); superficies[t.Sup.ToInt64()] = t; }
        Marshal.WriteIntPtr(salida, t.Sup);
        return 0;
    }
    static Textura Lee(IntPtr tex)
    {
        Textura t; if (!texturas.TryGetValue(tex.ToInt64(), out t)) return null;
        if (t.Sucia && !t.Destino) { Marshal.Copy(t.Mem, t.Px, 0, t.Px.Length); t.Sucia = false; }
        return t;
    }

    // El estado de la tarjeta imitada, y cada orden con el suyo.
    sealed class Etapa { public uint Op = 1, A1 = 2, A2 = 1, AOp = 1, AA1 = 2, AA2 = 1, Indice; public IntPtr Tex; public uint Dir = 1; }
    sealed class Estado
    {
        public bool Mezcla; public uint Origen = 2, Destino = 1, Fvf;
        public Etapa[] E = { new Etapa(), new Etapa() };
        public IntPtr Blanco;
        public Estado() { E[0].Op = 4; E[0].AOp = 2; E[1].Indice = 1; }
        public Estado Copia()
        {
            Estado c = new Estado(); c.Mezcla = Mezcla; c.Origen = Origen; c.Destino = Destino; c.Fvf = Fvf; c.Blanco = Blanco;
            for (int i = 0; i < 2; i++)
            {
                Etapa a = E[i], b = c.E[i];
                b.Op = a.Op; b.A1 = a.A1; b.A2 = a.A2; b.AOp = a.AOp; b.AA1 = a.AA1; b.AA2 = a.AA2; b.Indice = a.Indice; b.Tex = a.Tex; b.Dir = a.Dir;
            }
            return c;
        }
    }
    sealed class Envio { public int Tris; public byte[] Datos; public int Paso; public Estado S; }
    static readonly List<Envio> envios = new List<Envio>();
    static readonly Dictionary<long, Estado> estados = new Dictionary<long, Estado>();
    static Estado EstadoDe(IntPtr self) { Estado e; if (!estados.TryGetValue(self.ToInt64(), out e)) { e = new Estado(); e.Blanco = fondo; estados[self.ToInt64()] = e; } return e; }
    static int DrawUp(IntPtr self, uint prim, uint count, IntPtr data, uint stride)
    {
        if (soloContar) { llamadas++; trisTotal += count; return 0; }
        Envio e = new Envio(); e.Tris = (int)count; e.Paso = (int)stride; e.Datos = new byte[count * 3 * stride];
        Marshal.Copy(data, e.Datos, 0, e.Datos.Length);
        e.S = EstadoDe(self).Copia();
        envios.Add(e);
        return 0;
    }
    static int SetRS(IntPtr self, uint a, uint b)
    {
        Estado st = EstadoDe(self);
        if (a == 27) st.Mezcla = b != 0; else if (a == 19) st.Origen = b; else if (a == 20) st.Destino = b;
        return 0;
    }
    static int SetFvf(IntPtr self, uint a) { EstadoDe(self).Fvf = a; return 0; }
    static int SetTSS(IntPtr self, uint etapa, uint tipo, uint v)
    {
        if (etapa > 1) return 0;
        Etapa e = EstadoDe(self).E[etapa];
        switch (tipo) { case 1: e.Op = v; break; case 2: e.A1 = v; break; case 3: e.A2 = v; break; case 4: e.AOp = v; break; case 5: e.AA1 = v; break; case 6: e.AA2 = v; break; case 11: e.Indice = v; break; }
        return 0;
    }
    static int SetSamp(IntPtr self, uint etapa, uint tipo, uint v) { if (etapa <= 1 && tipo == 1) EstadoDe(self).E[etapa].Dir = v; return 0; }
    static int SetTex(IntPtr self, uint etapa, IntPtr t) { if (etapa <= 1) EstadoDe(self).E[etapa].Tex = t; return 0; }
    static int Hr(IntPtr self) { return 0; }
    static int Present(IntPtr self, IntPtr a, IntPtr b, IntPtr c, IntPtr d) { return 0; }
    static int SetRt(IntPtr self, uint i, IntPtr sup) { if (i == 0) EstadoDe(self).Blanco = sup; return 0; }
    static int GetRt(IntPtr self, uint i, IntPtr salida) { Marshal.WriteIntPtr(salida, EstadoDe(self).Blanco); return 0; }
    static int Valida(IntPtr self, IntPtr pasadas) { Marshal.WriteInt32(pasadas, 1); return 0; }
    static int Reset(IntPtr self, IntPtr pp) { reinicios++; return 0; }

    static void Enchufa(object d, string campo, string metodo)
    {
        FieldInfo f = Campo(TDev, campo);
        if (f == null) throw new Exception("el dispositivo no tiene " + campo);
        Delegate del = Delegate.CreateDelegate(f.FieldType, typeof(Inventario).GetMethod(metodo, TODO));
        vivos.Add(del); f.SetValue(d, del);
    }
    // Un delegado con firma "out IntPtr" o "ref PresentParams" no se puede atar a un metodo de aqui sin conocer el tipo: se
    // arma al vuelo, como Bloquea, pasando la direccion.
    static IntPtr PunteroPorDireccion(string tipo, string metodo, int argDireccion)
    {
        Type t = TD3D.GetNestedType(tipo, TODO);
        ParameterInfo[] ps = t.GetMethod("Invoke").GetParameters();
        Type[] pt = new Type[ps.Length]; for (int i = 0; i < ps.Length; i++) pt[i] = ps[i].ParameterType;
        DynamicMethod dm = new DynamicMethod(metodo + "Imitado", typeof(int), pt, typeof(Inventario).Module, true);
        ILGenerator il = dm.GetILGenerator();
        for (int i = 0; i < ps.Length; i++)
        {
            il.Emit(OpCodes.Ldarg, i);
            if (i == argDireccion) il.Emit(OpCodes.Conv_U);
        }
        il.Emit(OpCodes.Call, typeof(Inventario).GetMethod(metodo, TODO));
        il.Emit(OpCodes.Ret);
        Delegate del = dm.CreateDelegate(t); vivos.Add(del);
        return Marshal.GetFunctionPointerForDelegate(del);
    }
    static void EnchufaPorDireccion(object d, string campo, string tipo, string metodo, int arg)
    {
        FieldInfo f = Campo(TDev, campo);
        Type t = TD3D.GetNestedType(tipo, TODO);
        Delegate del = Marshal.GetDelegateForFunctionPointer(PunteroPorDireccion(tipo, metodo, arg), t);
        vivos.Add(del); f.SetValue(d, del);
    }
    static object Tarjeta(int w, int h, bool juntar, bool dosTexturas, bool sinEspera)
    {
        PreparaTexturas();
        IntPtr vt = Marshal.AllocHGlobal(128 * IntPtr.Size);
        for (int i = 0; i < 128; i++) Marshal.WriteIntPtr(vt, i * IntPtr.Size, Puntero("UintSelf", "Nada"));
        Marshal.WriteIntPtr(vt, 4 * IntPtr.Size, Puntero("UintSelf", "Memoria"));
        Marshal.WriteIntPtr(vt, 37 * IntPtr.Size, Puntero("SetTexDel", "SetRt"));
        Marshal.WriteIntPtr(vt, 38 * IntPtr.Size, PunteroPorDireccion("GetPtrDel", "GetRt", 2));
        Marshal.WriteIntPtr(vt, 70 * IntPtr.Size, PunteroPorDireccion("ValidaDel", "Valida", 1));
        IntPtr obj = Marshal.AllocHGlobal(16); Marshal.WriteIntPtr(obj, vt);
        object d = FormatterServices.GetUninitializedObject(TDev);
        Pon(d, "dev", obj);
        Pon(d, "stTex", new IntPtr(-1));
        object pp = Activator.CreateInstance(TD3D.GetNestedType("PresentParams", TODO));
        pp.GetType().GetField("BackBufferWidth").SetValue(pp, (uint)w); pp.GetType().GetField("BackBufferHeight").SetValue(pp, (uint)h);
        Pon(d, "pp", pp);
        Enchufa(d, "createTex", "CreaTex");
        Enchufa(d, "setRS", "SetRS"); Enchufa(d, "setFVF", "SetFvf"); Enchufa(d, "setTSS", "SetTSS"); Enchufa(d, "setSamp", "SetSamp");
        Enchufa(d, "setTex", "SetTex"); Enchufa(d, "drawUp", "DrawUp"); Enchufa(d, "present", "Present");
        Enchufa(d, "begin", "Hr"); Enchufa(d, "end", "Hr"); Enchufa(d, "testCoop", "Hr");
        EnchufaPorDireccion(d, "reset", "ResetDel", "Reset", 1);
        Pon(d, "verticesSinSoporte", true);
        string[] arr = { "cubeFaceV", "cubeBatchV", "cbx", "cbz", "cbo" }; int[] lar = { 6, 24, 4, 4, 4 };
        for (int i = 0; i < arr.Length; i++)
        {
            FieldInfo f = Campo(TDev, arr[i]);
            if (f != null && f.GetValue(d) == null) f.SetValue(d, Array.CreateInstance(f.FieldType.GetElementType(), lar[i]));
        }
        Pon(d, "juntar", juntar); Pon(d, "CapsDosTexturas", dosTexturas); Pon(d, "CapsSinEspera", sinEspera);
        if (!(bool)Llama(d, "InitStates")) throw new Exception("la tarjeta imitada no arranca");
        return d;
    }
    static object Cielo(object tarjeta, int w, int h)
    {
        object sf = Activator.CreateInstance(TStar, TODO, null, new object[] { IntPtr.Zero, w, h, 4321 }, null);
        Pon(sf, "gpu", tarjeta); Pon(sf, "seed0", 4321);
        Llama(sf, "BuildStars", w, h, 4321);
        return sf;
    }
    static Control Panel(int w, int h, object sf)
    {
        Control p = (Control)Activator.CreateInstance(TPanel, true);
        p.SetBounds(0, 0, w, h);
        Pon(p, "stars", sf);
        return p;
    }
    static List<Envio> Captura() { List<Envio> r = new List<Envio>(envios); envios.Clear(); return r; }
    static double Ef(double ax, double ay, double bx, double by, double px, double py) { return (bx - ax) * (py - ay) - (by - ay) * (px - ax); }
    static bool ArribaIzq(double ax, double ay, double bx, double by) { double dx = bx - ax, dy = by - ay; return dy < 0 || (dy == 0 && dx > 0); }
    static float Fx(byte[] d, int paso, int v, int campo) { return BitConverter.ToSingle(d, v * paso + campo); }
    static uint Col(byte[] d, int paso, int v) { return BitConverter.ToUInt32(d, v * paso + 16); }
    // Pixeles pintados (con la regla de centros), en su destino.
    static long Area(List<Envio> es)
    {
        long n = 0;
        foreach (Envio e in es)
        {
            for (int t = 0; t < e.Tris; t++)
            {
                int i0 = t * 3;
                double x0 = Fx(e.Datos, e.Paso, i0, 0), y0 = Fx(e.Datos, e.Paso, i0, 4), x1 = Fx(e.Datos, e.Paso, i0 + 1, 0), y1 = Fx(e.Datos, e.Paso, i0 + 1, 4), x2 = Fx(e.Datos, e.Paso, i0 + 2, 0), y2 = Fx(e.Datos, e.Paso, i0 + 2, 4);
                n += (long)Math.Round(Math.Abs(Ef(x0, y0, x1, y1, x2, y2)) / 2);
            }
        }
        return n;
    }

    static string carpetaCopia;
    static string Cargable(string ruta)
    {
        if (Type.GetType("Mono.Runtime") == null || Environment.OSVersion.Platform == PlatformID.Win32NT) return ruta;
        string dir = Path.Combine(Path.GetTempPath(), "rocola_performance_" + System.Diagnostics.Process.GetCurrentProcess().Id);
        Directory.CreateDirectory(dir);
        string destino = Path.Combine(dir, Path.GetFileName(ruta));
        File.Copy(ruta, destino, true);
        string naudio = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(ruta)), "NAudio.dll");
        if (File.Exists(naudio)) File.Copy(naudio, Path.Combine(dir, "NAudio.dll"), true);
        File.WriteAllText(destino + ".config", "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<configuration>\n  <dllmap dll=\"kernel32.dll\">\n" +
                          "    <dllentry dll=\"libc.so.6\" name=\"RtlMoveMemory\" target=\"memmove\"/>\n  </dllmap>\n</configuration>\n");
        carpetaCopia = dir;
        return destino;
    }

    static object Aurora(Type tAur)
    {
        object au = Activator.CreateInstance(tAur);
        float[] b = new float[48]; for (int i = 0; i < b.Length; i++) b[i] = 0.35f + 0.3f * (float)Math.Sin(i * 0.4);
        tAur.GetField("Has").SetValue(au, true); tAur.GetField("B").SetValue(au, b);
        tAur.GetField("BaseY").SetValue(au, 560f); tAur.GetField("Alto").SetValue(au, 260f);
        tAur.GetField("Rgb").SetValue(au, 0xFF30E080u); tAur.GetField("Rgb2").SetValue(au, 0xFF8040FFu);
        tAur.GetField("RgbA").SetValue(au, 0xFF30A0FFu); tAur.GetField("Rgb2A").SetValue(au, 0xFFFF60C0u);
        tAur.GetField("Dim").SetValue(au, 0.9f); tAur.GetField("DimFilo").SetValue(au, 0.9f);
        return au;
    }
    static void Escena(string nombre, int W, int H, bool perf, bool fugaces, bool luna, bool aurora, Type tAur)
    {
        object dev = Tarjeta(W, H, true, true, false);
        Control p = Panel(W, H, Cielo(dev, W, H));
        Pon(p, "VinetaSuave", true);
        Pon(p, "CieloMedia", perf); Pon(p, "GranoVineta", perf); Pon(p, "FondoUnaCapa", perf); Pon(p, "CieloCuarto", perf);
        Pon(p, "Fugaces", fugaces ? 3 : 0); Pon(p, "ConLuna", luna);
        if (aurora)
        {
            Type tf = typeof(Func<>).MakeGenericType(tAur);
            object au = Aurora(tAur);
            System.Linq.Expressions.Expression body = System.Linq.Expressions.Expression.Constant(au, tAur);
            Delegate d = System.Linq.Expressions.Expression.Lambda(tf, body).Compile();
            Pon(p, "GetAurora", d);
        }
        soloContar = true;
        for (int i = 0; i < 40; i++) { Pon(p, "NebFase", 1f + (i * 24 / 60) * 0.00654f); Llama(p, "RenderFrame"); }
        llamadas = 0; trisTotal = 0;
        GC.Collect(); int g0 = GC.CollectionCount(0); long m0 = GC.GetTotalMemory(false);
        const int N = 400; double[] ms = new double[N];
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        for (int i = 0; i < N; i++)
        {
            Pon(p, "NebFase", 1f + (i * 24 / 60) * 0.00654f);
            sw.Reset(); sw.Start(); Llama(p, "RenderFrame"); sw.Stop(); ms[i] = sw.Elapsed.TotalMilliseconds;
        }
        long m1 = GC.GetTotalMemory(false); int g1 = GC.CollectionCount(0);
        soloContar = false;
        Array.Sort(ms); double suma = 0; foreach (double x in ms) suma += x;
        Console.WriteLine("     {0,-34} {1,6:0.000} ms media  p95 {2,6:0.000}  max {3,6:0.000} | {4,5:0.0} ordenes {5,6:0.0} triang. por cuadro | GC0 {6}  memoria {7:+0;-0;0} B/cuadro",
            nombre, suma / N, ms[(int)(N * 0.95)], ms[N - 1], llamadas / (double)N, trisTotal / (double)N, g1 - g0, (m1 - m0) / N);
    }

    static void Detalle(string nombre, int W, int H, bool perf, bool aurora, Type tAur)
    {
        object dev = Tarjeta(W, H, true, true, false);
        Control p = Panel(W, H, Cielo(dev, W, H));
        Pon(p, "VinetaSuave", true); Pon(p, "CuentaArea", true);
        Pon(p, "CieloMedia", perf); Pon(p, "GranoVineta", perf); Pon(p, "FondoUnaCapa", perf); Pon(p, "CieloCuarto", perf);
        Pon(p, "Fugaces", 0); Pon(p, "ConLuna", true);
        if (aurora)
        {
            Type tf = typeof(Func<>).MakeGenericType(tAur);
            System.Linq.Expressions.Expression body = System.Linq.Expressions.Expression.Constant(Aurora(tAur), tAur);
            Pon(p, "GetAurora", System.Linq.Expressions.Expression.Lambda(tf, body).Compile());
        }
        for (int i = 0; i < 6; i++) { Pon(p, "NebFase", 1f + i * 0.0005f); envios.Clear(); Llama(p, "RenderFrame"); }
        List<Envio> c = Captura();
        double pant = (double)W * H;
        Console.WriteLine("   " + nombre + ": " + c.Count + " ordenes, " + (Area(c) / pant).ToString("0.00") + " pantallas");
        foreach (Envio e in c)
            Console.WriteLine("      " + e.Tris.ToString().PadLeft(5) + " triang.  " + (Area(new List<Envio> { e }) / pant).ToString("0.000").PadLeft(6) + " pantallas  "
                              + (e.S.Mezcla ? "mezcla " : "opaco  ") + (e.S.Blanco != fondo ? "a la imagen intermedia" : ""));
    }
    static int Main(string[] argv)
    {
        try { return Prueba(argv); }
        finally { if (carpetaCopia != null) try { Directory.Delete(carpetaCopia, true); } catch { } }
    }
    static int Prueba(string[] argv)
    {
        Assembly a = Assembly.LoadFrom(Cargable((argv.Length > 0) ? argv[0] : "rocola.exe"));
        TForm = a.GetType("Rocola.Form1"); TPanel = a.GetType("Rocola.GpuPanel");
        if (TPanel == null) { Console.WriteLine("  sin tarjeta: nada que medir\n  OK inventario (perfil sin GPU)"); return 0; }
        TStar = a.GetType("Rocola.GpuStarfield"); TDev = a.GetType("Rocola.D3D9Device");
        TD3D = a.GetType("Rocola.D3D9"); TVtx = TD3D.GetNestedType("Vtx", TODO); TVtx2 = TD3D.GetNestedType("Vtx2", TODO);
        Type tAur = a.GetType("Rocola.GpuAurora");
        Console.WriteLine("  (tarjeta imitada de " + TForm.Name + ": vertice de " + Marshal.SizeOf(TVtx) + " bytes, y doble de " + Marshal.SizeOf(TVtx2) + ")");
        Console.WriteLine("\n  INVENTARIO DEL CUADRO DE GPU, 1024x768, tarjeta imitada (cuenta ordenes, no pinta; el tiempo es del procesador de ESTE equipo)\n");
        foreach (bool perf in new bool[] { false, true })
        {
            Console.WriteLine(perf ? "   -- Performance (G1, G2, J2) --" : "   -- de fabrica --");
            Escena("cielo y estrellas", 1024, 768, perf, false, false, false, tAur);
            Escena("+ fugaces", 1024, 768, perf, true, false, false, tAur);
            Escena("+ fugaces + luna", 1024, 768, perf, true, true, false, tAur);
            Escena("+ fugaces + luna + aurora", 1024, 768, perf, true, true, true, tAur);
        }
        Console.WriteLine("\n  POR ORDEN, un cuadro con luna y aurora (1024x768)");
        Detalle("de fabrica", 1024, 768, false, true, tAur);
        Detalle("Performance", 1024, 768, true, true, tAur);
        Console.WriteLine("\n  OK inventario");
        return 0;
    }
}