#pragma warning disable 0649, 0414
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// TIRONES AL CAMBIAR (20-10-2026): las pruebas de lo que se hizo para los peores cuadros (cambios de pagina, de cancion, de modo).
//
//   mono tironescambio.exe <rocola.exe> [<rocola_anterior.exe>]
//
// Con una tarjeta imitada que solo CUENTA (no pinta): lo que le llega al controlador (texturas creadas, bloqueos, PreLoad, memoria
// libre, Evict...) y lo que se puede probar sin un controlador de verdad. Con el ejecutable anterior como segundo argumento compara
// byte a byte lo que cada camino nuevo produce contra el de antes (se carga con Assembly.Load(File.ReadAllBytes(...)): LoadFrom
// devolveria el ensamblado ya cargado). Todo es Linux/Mono con libgdiplus: NO mide la rocola real, ni Windows XP, ni un A4-6300.
class TironesCambio
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos, oks;
    static void Check(bool ok, string q) { if (ok) { oks++; Console.WriteLine("  OK    " + q); } else { fallos++; Console.WriteLine("  FALLO " + q); } }
    static Type TF, TPanel, TStar, TDev, TD3D, TLibro;
    static Assembly AsmNuevo;
    static readonly List<object> vivos = new List<object>();

    // ======================= REFLEXION =======================
    static FieldInfo Campo(Type t, string n)
    { for (; t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, TODO | BindingFlags.DeclaredOnly); if (f != null) return f; } return null; }
    static void Pon(object o, string n, object v) { FieldInfo f = Campo(o.GetType(), n); if (f == null) throw new Exception("falta el campo " + n); f.SetValue(o, v); }
    static void PonSiHay(object o, string n, object v) { FieldInfo f = Campo(o.GetType(), n); if (f != null) f.SetValue(o, v); }
    static object Toma(object o, string n) { FieldInfo f = Campo(o.GetType(), n); if (f == null) throw new Exception("falta el campo " + n); return f.GetValue(o); }
    static MethodInfo Metodo(Type t, string n)
    { for (; t != null; t = t.BaseType) { MethodInfo m = t.GetMethod(n, TODO | BindingFlags.DeclaredOnly); if (m != null) return m; } return null; }
    static MethodInfo Metodo(Type t, string n, params Type[] tipos)
    { for (; t != null; t = t.BaseType) { MethodInfo m = t.GetMethod(n, TODO | BindingFlags.DeclaredOnly, null, tipos, null); if (m != null) return m; } return null; }
    static object Llama(object o, string n, params object[] a)
    {
        MethodInfo m = Metodo(o.GetType(), n);
        if (m == null) throw new Exception("falta " + o.GetType().Name + "." + n);
        try { return m.Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static object LlamaE(Type t, string n, params object[] a)
    {
        MethodInfo m = Metodo(t, n);
        if (m == null) throw new Exception("falta " + t.Name + "." + n);
        try { return m.Invoke(null, a); } catch (TargetInvocationException e) { throw e.InnerException; }
    }

    // ======================= LA TARJETA IMITADA (solo cuenta) =======================
    sealed class Tx { public int W, H; public IntPtr Mem; public bool Suelta; }
    static readonly Dictionary<long, Tx> texturas = new Dictionary<long, Tx>();
    static IntPtr vtTex, vtDisp;
    static int offPitch, offBits;
    static long cCrea, cBloq, cPreLoad, cEvict, cMem, cDraw, cSueltas, cPresent;
    static bool creaFalla; static long creaFallos;   // pedir al controlador que falle CreateTexture
    static uint memoriaLibre = 256u * 1024 * 1024;
    static uint Nada(IntPtr self) { return 0; }
    static uint Memoria(IntPtr self) { cMem++; return memoriaLibre; }
    static int Evicta(IntPtr self) { cEvict++; return 0; }
    static int PreCarga(IntPtr self) { cPreLoad++; return 0; }
    static IntPtr Puntero(string tipo, string metodo)
    {
        Delegate del = Delegate.CreateDelegate(TD3D.GetNestedType(tipo, TODO), typeof(TironesCambio).GetMethod(metodo, TODO));
        vivos.Add(del); return Marshal.GetFunctionPointerForDelegate(del);
    }
    static IntPtr PunteroPorDireccion(string tipo, string metodo, int argDireccion)
    {
        Type t = TD3D.GetNestedType(tipo, TODO);
        ParameterInfo[] ps = t.GetMethod("Invoke").GetParameters();
        Type[] pt = new Type[ps.Length]; for (int i = 0; i < ps.Length; i++) pt[i] = ps[i].ParameterType;
        DynamicMethod dm = new DynamicMethod(metodo + "Imitado", typeof(int), pt, typeof(TironesCambio).Module, true);
        ILGenerator il = dm.GetILGenerator();
        for (int i = 0; i < ps.Length; i++) { il.Emit(OpCodes.Ldarg, i); if (i == argDireccion) il.Emit(OpCodes.Conv_U); }
        il.Emit(OpCodes.Call, typeof(TironesCambio).GetMethod(metodo, TODO));
        il.Emit(OpCodes.Ret);
        Delegate del = dm.CreateDelegate(t); vivos.Add(del);
        return Marshal.GetFunctionPointerForDelegate(del);
    }
    static int CreaTex(IntPtr self, uint w, uint h, uint levels, uint usage, uint format, uint pool, out IntPtr tex, IntPtr shared)
    {
        PreparaTexturas();
        if (creaFalla) { creaFallos++; tex = IntPtr.Zero; return unchecked((int)0x8876017C); }   // D3DERR_OUTOFVIDEOMEMORY
        cCrea++;
        IntPtr o = Marshal.AllocHGlobal(16); Marshal.WriteIntPtr(o, vtTex);
        Tx t = new Tx(); t.W = (int)w; t.H = (int)h; t.Mem = Marshal.AllocHGlobal((int)(w * h * 4)); Marshal.Copy(new byte[w * h * 4], 0, t.Mem, (int)(w * h * 4));
        texturas[o.ToInt64()] = t; tex = o;
        return 0;
    }
    static int Bloquea(IntPtr self, IntPtr lr, IntPtr rect)
    {
        Tx t = texturas[self.ToInt64()];
        cBloq++;
        int x0 = 0, y0 = 0;
        if (rect != IntPtr.Zero) { x0 = Marshal.ReadInt32(rect, 0); y0 = Marshal.ReadInt32(rect, 4); }
        Marshal.WriteInt32(lr, offPitch, t.W * 4);
        Marshal.WriteIntPtr(lr, offBits, new IntPtr(t.Mem.ToInt64() + (long)y0 * t.W * 4 + x0 * 4));
        return 0;
    }
    static int Desbloquea(IntPtr self, uint level) { return 0; }
    static uint Suelta(IntPtr self) { Tx t; if (texturas.TryGetValue(self.ToInt64(), out t)) { t.Suelta = true; cSueltas++; } return 0; }
    static void PreparaTexturas()
    {
        if (vtTex != IntPtr.Zero) return;
        Type tlr = TD3D.GetNestedType("LockedRect", TODO);
        offPitch = (int)Marshal.OffsetOf(tlr, "Pitch"); offBits = (int)Marshal.OffsetOf(tlr, "Bits");
        Type tLock = TD3D.GetNestedType("LockRectDel", TODO);
        ParameterInfo[] ps = tLock.GetMethod("Invoke").GetParameters();
        Type[] pt = new Type[ps.Length]; for (int i = 0; i < ps.Length; i++) pt[i] = ps[i].ParameterType;
        DynamicMethod dm = new DynamicMethod("BloqueaImitada", typeof(int), pt, typeof(TironesCambio).Module, true);
        ILGenerator il = dm.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Conv_U); il.Emit(OpCodes.Ldarg_3);
        il.Emit(OpCodes.Call, typeof(TironesCambio).GetMethod("Bloquea", TODO));
        il.Emit(OpCodes.Ret);
        Delegate lk = dm.CreateDelegate(tLock); vivos.Add(lk);
        Type tReg = TD3D.GetNestedType("LockRegionDel", TODO);
        ps = tReg.GetMethod("Invoke").GetParameters();
        pt = new Type[ps.Length]; for (int i = 0; i < ps.Length; i++) pt[i] = ps[i].ParameterType;
        DynamicMethod dr = new DynamicMethod("BloqueaRegionImitada", typeof(int), pt, typeof(TironesCambio).Module, true);
        il = dr.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Conv_U); il.Emit(OpCodes.Ldarg_3); il.Emit(OpCodes.Conv_U);
        il.Emit(OpCodes.Call, typeof(TironesCambio).GetMethod("Bloquea", TODO));
        il.Emit(OpCodes.Ret);
        Delegate lr2 = dr.CreateDelegate(tReg); vivos.Add(lr2);
        vtTex = Marshal.AllocHGlobal(64 * IntPtr.Size);
        for (int i = 0; i < 64; i++) Marshal.WriteIntPtr(vtTex, i * IntPtr.Size, Puntero("UintSelf", "Nada"));
        Marshal.WriteIntPtr(vtTex, 2 * IntPtr.Size, Puntero("UintSelf", "Suelta"));
        Marshal.WriteIntPtr(vtTex, 9 * IntPtr.Size, Puntero("HrSelf", "PreCarga"));   // IDirect3DResource9::PreLoad
        Marshal.WriteIntPtr(vtTex, 19 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(lk));
        Marshal.WriteIntPtr(vtTex, 20 * IntPtr.Size, Puntero("UnlockRectDel", "Desbloquea"));
    }
    static int Hr(IntPtr self) { return 0; }
    static int SetRS(IntPtr self, uint a, uint b) { return 0; }
    static int SetFvf(IntPtr self, uint a) { return 0; }
    static int SetTSS(IntPtr self, uint e, uint t, uint v) { return 0; }
    static int SetTex(IntPtr self, uint e, IntPtr t) { return 0; }
    static int DrawUp(IntPtr self, uint prim, uint count, IntPtr data, uint stride) { cDraw++; return 0; }
    static int Present(IntPtr self, IntPtr a, IntPtr b, IntPtr c, IntPtr d) { cPresent++; return 0; }
    static int SetRt(IntPtr self, uint i, IntPtr sup) { return 0; }
    static int GetRt(IntPtr self, uint i, IntPtr salida) { Marshal.WriteIntPtr(salida, IntPtr.Zero); return 0; }
    static int Valida(IntPtr self, IntPtr pasadas) { Marshal.WriteInt32(pasadas, 1); return 0; }
    static int Reset(IntPtr self, IntPtr pp) { return 0; }
    static void Enchufa(object d, string campo, string metodo)
    {
        FieldInfo f = Campo(TDev, campo);
        if (f == null) throw new Exception("el dispositivo no tiene " + campo);
        Delegate del = Delegate.CreateDelegate(f.FieldType, typeof(TironesCambio).GetMethod(metodo, TODO));
        vivos.Add(del); f.SetValue(d, del);
    }
    static void EnchufaPorDireccion(object d, string campo, string tipo, string metodo, int arg)
    {
        FieldInfo f = Campo(TDev, campo);
        Type t = TD3D.GetNestedType(tipo, TODO);
        Delegate del = Marshal.GetDelegateForFunctionPointer(PunteroPorDireccion(tipo, metodo, arg), t);
        vivos.Add(del); f.SetValue(d, del);
    }
    static object Tarjeta(int w, int h)
    {
        PreparaTexturas();
        IntPtr vt = Marshal.AllocHGlobal(128 * IntPtr.Size);
        for (int i = 0; i < 128; i++) Marshal.WriteIntPtr(vt, i * IntPtr.Size, Puntero("UintSelf", "Nada"));
        Marshal.WriteIntPtr(vt, 4 * IntPtr.Size, Puntero("UintSelf", "Memoria"));      // GetAvailableTextureMem
        Marshal.WriteIntPtr(vt, 5 * IntPtr.Size, Puntero("HrSelf", "Evicta"));         // EvictManagedResources
        Marshal.WriteIntPtr(vt, 37 * IntPtr.Size, Puntero("SetTexDel", "SetRt"));
        Marshal.WriteIntPtr(vt, 38 * IntPtr.Size, PunteroPorDireccion("GetPtrDel", "GetRt", 2));
        Marshal.WriteIntPtr(vt, 70 * IntPtr.Size, PunteroPorDireccion("ValidaDel", "Valida", 1));
        vtDisp = vt;
        IntPtr obj = Marshal.AllocHGlobal(16); Marshal.WriteIntPtr(obj, vt);
        object d = Activator.CreateInstance(TDev, new object[] { IntPtr.Zero, w, h });
        Pon(d, "dev", obj); Pon(d, "stTex", new IntPtr(-1));
        Pon(d, "lost", false); Pon(d, "resetPending", false);
        object pp = Activator.CreateInstance(TD3D.GetNestedType("PresentParams", TODO));
        pp.GetType().GetField("BackBufferWidth").SetValue(pp, (uint)w); pp.GetType().GetField("BackBufferHeight").SetValue(pp, (uint)h);
        Pon(d, "pp", pp);
        Enchufa(d, "createTex", "CreaTex");
        Enchufa(d, "setRS", "SetRS"); Enchufa(d, "setFVF", "SetFvf"); Enchufa(d, "setTSS", "SetTSS"); Enchufa(d, "setSamp", "SetTSS");
        Enchufa(d, "setTex", "SetTex"); Enchufa(d, "drawUp", "DrawUp"); Enchufa(d, "present", "Present");
        Enchufa(d, "begin", "Hr"); Enchufa(d, "end", "Hr"); Enchufa(d, "testCoop", "Hr");
        EnchufaPorDireccion(d, "reset", "ResetDel", "Reset", 1);
        Pon(d, "verticesSinSoporte", true);
        Pon(d, "juntar", true); Pon(d, "CapsDosTexturas", true); Pon(d, "CapsSinEspera", false);
        if (!(bool)Llama(d, "InitStates")) throw new Exception("la tarjeta imitada no arranca");
        return d;
    }
    static void PonCeroCuentas() { cCrea = cBloq = cPreLoad = cEvict = cMem = cDraw = cSueltas = cPresent = 0; }

    // ======================= MONTAR LA ROCOLA (rejilla de 18 discos) =======================
    static int ancho, alto, montajeId;
    static Bitmap[] portadas; static Bitmap etiqueta;
    static class GridData<T> { internal static T[] Rows; internal static int Id; }
    static Bitmap NuevaPortada(int k)
    {
        Bitmap b = new Bitmap(300, 300, PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            using (System.Drawing.Drawing2D.LinearGradientBrush br = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, 300, 300),
                Color.FromArgb(40 + k * 30, 60, 200 - k * 20), Color.FromArgb(220, 180 - k * 25, 40 + k * 10), 45f + k * 20)) g.FillRectangle(br, 0, 0, 300, 300);
        }
        return b;
    }
    static void PreparaPortadas()
    {
        portadas = new Bitmap[6];
        for (int k = 0; k < 6; k++) portadas[k] = NuevaPortada(k);
        etiqueta = new Bitmap(256, 64, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(etiqueta))
        {
            g.Clear(Color.Transparent);
            using (Font f = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Bold)) using (SolidBrush w = new SolidBrush(Color.White))
            { g.DrawString("Cancion de ejemplo", f, w, 66, 4); g.DrawString("Artista de ejemplo", f, w, 160, 36); }
        }
    }
    static int Grid<T>(T[] data)
    {
        if (GridData<T>.Rows != null && GridData<T>.Id == montajeId) { Array.Copy(GridData<T>.Rows, data, 18); return 18; }
        int cw = ancho / 6, ch = (alto - 200) / 3;
        int lado = Math.Max(32, Math.Min(cw - 24, ch - 40));
        Type tl = typeof(T).GetField("Layout").FieldType;
        for (int i = 0; i < 18; i++)
        {
            object c = Activator.CreateInstance(typeof(T));
            float x = (i % 6) * cw, y = 100 + (i / 6) * ch;
            Pon(c, "Key", "disco" + montajeId + "_" + i); Pon(c, "Bmp", etiqueta); Pon(c, "Slot", i);
            Pon(c, "Direct", true); Pon(c, "FullCover", portadas[i % 6]); Pon(c, "HasCover", true);
            object lay = Activator.CreateInstance(tl);
            Pon(lay, "Width", 256); Pon(lay, "Height", 64);
            Pon(lay, "Face", new Rectangle(0, 0, 64, 64)); Pon(lay, "FaceAt", new Rectangle(0, 0, 64, 64));
            Pon(lay, "Text1", new Rectangle(64, 0, 96, 32)); Pon(lay, "Text1At", new Rectangle(64, 0, 96, 32));
            Pon(lay, "Text2", new Rectangle(160, 32, 96, 32)); Pon(lay, "Text2At", new Rectangle(160, 32, 96, 32));
            Pon(c, "Layout", lay);
            Pon(c, "CoverRect", new RectangleF(x + (cw - lado) / 2, y, lado, lado));
            Pon(c, "X", x); Pon(c, "Y", y + lado - 64 + 8); Pon(c, "W", (float)cw); Pon(c, "H", 64f); Pon(c, "A", 1f);
            data[i] = (T)c;
        }
        GridData<T>.Rows = (T[])data.Clone(); GridData<T>.Id = montajeId; return 18;
    }
    static Delegate Generic(FieldInfo field, string method)
    {
        Type type = field.FieldType.GetMethod("Invoke").GetParameters()[0].ParameterType.GetElementType();
        return Delegate.CreateDelegate(field.FieldType, typeof(TironesCambio).GetMethod(method, TODO).MakeGenericMethod(type));
    }
    static Control Monta(int w, int h)
    {
        ancho = w; alto = h; montajeId++;
        object tarjeta = Tarjeta(w, h);
        object sf = Activator.CreateInstance(TStar, TODO, null, new object[] { IntPtr.Zero, w, h, 4321 }, null);
        Pon(sf, "gpu", tarjeta); Pon(sf, "seed0", 4321);
        Llama(sf, "BuildStars", w, h, 4321);
        Control p = (Control)Activator.CreateInstance(TPanel, true);
        p.SetBounds(0, 0, w, h);
        Pon(p, "stars", sf);
        PreparaPortadas();
        Pon(p, "Estrella3D", false); Pon(p, "ConLuna", false); Pon(p, "Dim", 1f); Pon(p, "ShowAlbum", true);
        FieldInfo grid = TPanel.GetField("GetGrid", TODO); grid.SetValue(p, Generic(grid, "Grid"));
        return p;
    }
    static bool Cuadro(Control p) { return (bool)Llama(p, "RenderFrame"); }

    // ======================= 0. EL LIBRO DE LOS PEORES CUADROS =======================
    static object NuevoLibro() { return Activator.CreateInstance(TLibro, true); }
    static void Frame(object libro, double t0, double[] fases)
    {
        // fases: milisegundos de cada una de las once, en orden; se marcan en ese orden a partir de t0.
        Llama(libro, "Empieza", t0);
        double t = t0;
        for (int i = 0; i < fases.Length; i++) { t += fases[i]; Llama(libro, "Marca", i, t); }
        Llama(libro, "Termina", t, 16.7);
    }
    static double[] Trabajo(double ms) { double[] f = new double[11]; f[9] = ms; return f; }
    static double Num(string sub) { return double.Parse(sub, System.Globalization.CultureInfo.InvariantCulture); }
    static void Libro()
    {
        Console.WriteLine("# 0. el libro de los peores cuadros");
        TLibro = AsmNuevo.GetType("Rocola.LibroDeCuadros");
        if (TLibro == null) { Check(false, "existe Rocola.LibroDeCuadros"); return; }
        FieldInfo umbral = Campo(TLibro, "UmbralMs"), ventana = Campo(TLibro, "VentanaMs");
        double umbral0 = (double)umbral.GetValue(null), ventana0 = (double)ventana.GetValue(null);
        Check(umbral0 == 12.0 && ventana0 == 60000.0, "de fabrica: entra desde 12 ms de trabajo y la hoja sale cada 60 s");
        object libro = NuevoLibro();
        Check((int)Toma(libro, "Banderas") == 0, "el libro nace vacio");
        // ocho puestos: de 100 cuadros con 13..112 ms se quedan los ocho mayores, de mayor a menor
        for (int i = 0; i < 100; i++) Frame(libro, 1000.0 + i * 20, Trabajo(13 + i));
        Check((int)Llama(libro, "get_Ocupadas") == 8, "se guardan ocho cuadros, no mas");
        Check(Llama(libro, "Vuelca", 1000.0 + 59000, false) == null, "antes de que acabe el minuto no sale hoja");
        string hoja = (string)Llama(libro, "Vuelca", 1000.0 + 60000, false);
        Check(hoja != null && hoja.Contains(": 100 de 100 pasaron de 12.0 ms"), "al acabar el minuto sale la hoja, con cuantos cuadros hubo y cuantos pasaron");
        int p112 = hoja == null ? -1 : hoja.IndexOf("112.0 ms"), p105 = hoja == null ? -1 : hoja.IndexOf("105.0 ms"), p104 = hoja == null ? -1 : hoja.IndexOf("104.0 ms");
        Check(p112 > 0 && p105 > p112 && p104 < 0, "estan los ocho peores (112 a 105) y de mayor a menor; el noveno (104) no");
        Check(hoja != null && hoja.Contains("dibujo 112.0") && hoja.Contains("pasaron de 12.0 ms"), "cada entrada reparte el tiempo por fases");
        Check(Llama(libro, "Vuelca", 1000.0 + 130000, false) == null && (int)Llama(libro, "get_Ocupadas") == 0, "tras la hoja el libro queda vacio y no repite");
        // por debajo del umbral no hay nada que contar
        for (int i = 0; i < 50; i++) Frame(libro, 200000.0 + i * 20, Trabajo(11.9));
        Check(Llama(libro, "Vuelca", 400000.0, true) == null, "ningun cuadro llego al umbral: no sale hoja (ni con forzar)");
        // la espera de Present cuenta solo lo que pase de un periodo
        object l2 = NuevoLibro();
        double[] f = new double[11]; f[9] = 2; f[10] = 16.0; Frame(l2, 10, f);
        Check((int)Llama(l2, "get_Ocupadas") == 0, "esperar al monitor un periodo entero (16 ms) no es un tiron");
        f = new double[11]; f[9] = 2; f[10] = 40.0; Frame(l2, 100, f);
        string h2 = (string)Llama(l2, "Vuelca", 100.0 + 90000, false);
        Check(h2 != null && h2.Contains("25.3 ms (trabajo 2.0, presentar 40.0)") && h2.Contains("presentar 40.0"), "una espera de 40 ms en Present con periodo de 16.7 pesa 2 + 23.3 = 25.3 ms");
        // las fases caen en su sitio
        object l3 = NuevoLibro(); Llama(l3, "Empieza", 500.0);
        Llama(l3, "Marca", 1, 503.0); Llama(l3, "Marca", 2, 510.0); Llama(l3, "Marca", 2, 520.0); Llama(l3, "Marca", 10, 521.0);
        Llama(l3, "Termina", 521.0, 16.7);
        string h3 = (string)Llama(l3, "Vuelca", 600000.0, true);
        Check(h3 != null && h3.Contains("rejilla 3.0") && h3.Contains("portadas 17.0") && h3.Contains("presentar 1.0") && !h3.Contains("cubos"), "cada marca suma a su fase (rejilla 3, portadas 7+10, presentar 1) y las fases sin tiempo no salen");
        // memoria fija: ni una reserva por cuadro (ni un gen0 en 300 mil cuadros, con y sin entradas nuevas)
        object l4 = NuevoLibro();
        int g0 = GC.CollectionCount(0);
        double tt = 0;
        for (int i = 0; i < 300000; i++)
        {
            Llama(l4, "Empieza", tt);
            for (int k = 0; k < 11; k++) { tt += (i % 7 == 0 && k == 9) ? 30.0 + (i % 40) : 0.2; Llama(l4, "Marca", k, tt); }
            Llama(l4, "Termina", tt, 16.7);
        }
        Check(true, "300 mil cuadros pasados por el libro (por reflexion, que reserva lo suyo): recolecciones de generacion 0 = " + (GC.CollectionCount(0) - g0));
        // la prueba de verdad de memoria: la misma cuenta SIN reflexion, con delegados ya creados
        MethodInfo mEmp = Metodo(TLibro, "Empieza"), mMar = Metodo(TLibro, "Marca"), mTer = Metodo(TLibro, "Termina");
        Action<double> emp = (Action<double>)Delegate.CreateDelegate(typeof(Action<double>), l4, mEmp);
        Action<int, double> mar = (Action<int, double>)Delegate.CreateDelegate(typeof(Action<int, double>), l4, mMar);
        Action<double, double> ter = (Action<double, double>)Delegate.CreateDelegate(typeof(Action<double, double>), l4, mTer);
        GC.Collect(); g0 = GC.CollectionCount(0);
        for (int i = 0; i < 2000000; i++)
        {
            emp(tt);
            for (int k = 0; k < 11; k++) { tt += (i % 7 == 0 && k == 9) ? 30.0 + (i % 40) : 0.2; mar(k, tt); }
            ter(tt, 16.7);
        }
        Check(GC.CollectionCount(0) == g0, "2 millones de cuadros (con y sin entradas nuevas) no reservan ni provocan una recoleccion: " + (GC.CollectionCount(0) - g0));
        Check((int)Llama(l4, "get_Ocupadas") == 8, "y el libro sigue con sus ocho");
    }

    // ======================= EL LIBRO DENTRO DEL PANEL =======================
    static void LibroEnElPanel()
    {
        Console.WriteLine("# 0b. el libro dentro de RenderFrame, con la tarjeta imitada");
        FieldInfo umbral = Campo(TLibro, "UmbralMs"), ventana = Campo(TLibro, "VentanaMs");
        List<string> hojas = new List<string>();
        Control p = Monta(1024, 768);
        try
        {
            Pon(p, "ReportPeores", new Action<string>(delegate (string h) { hojas.Add(h); }));
            // apagado (lo de fabrica): ni marcas ni hojas
            for (int i = 0; i < 6; i++) Check(Cuadro(p), "el cuadro " + i + " se presenta con el libro apagado");
            object libro = Llama(p, "get_Libro");
            Check((int)Llama(libro, "get_Cuadros") == 0 && hojas.Count == 0, "con el libro apagado no se cuenta ni un cuadro ni sale una hoja");
            // encendido y con el umbral en cero: todos los cuadros entran
            umbral.SetValue(null, 0.0); ventana.SetValue(null, 1.0);
            Llama(p, "set_LibroActivo", true);
            PonCeroCuentas();
            for (int i = 0; i < 40; i++) { Cuadro(p); System.Threading.Thread.Sleep(1); }
            Check(hojas.Count > 0, "encendido, salen hojas (" + hojas.Count + " en 40 cuadros con ventana de 1 ms)");
            string h0 = hojas.Count > 0 ? hojas[0] : "";
            Check(h0.StartsWith("peores cuadros de los ultimos ") && h0.Contains("ms") && h0.Contains("recolecciones en el cuadro") && h0.Contains("modo "), "la hoja dice de que va y lleva el modo y las recolecciones");
            Check(h0.Contains("pre ") || h0.Contains("dibujo ") || h0.Contains("rejilla "), "con las fases del cuadro");
            Check(h0.Contains("pagina"), "marca la bandera de pagina (la rejilla estaba a la vista)");
            Check(h0.Contains("celdas 18"), "dice cuantas celdas habia (18)");
            Check(h0.Contains("texturas subidas"), "cuenta las texturas subidas");
            Console.WriteLine("        ejemplo de hoja: " + h0.Replace("\r\n", "\n        "));
            // lo que cuesta tenerlo encendido (alternando apagado/encendido; Linux/Mono con tarjeta imitada: NO es el A4)
            umbral.SetValue(null, 1e9);   // encendido pero sin guardar nada
            double[] apagado = new double[7], encendido = new double[7];
            for (int ronda = 0; ronda < 7; ronda++)
                foreach (bool on in (ronda % 2 == 0 ? new bool[] { false, true } : new bool[] { true, false }))
                {
                    Llama(p, "set_LibroActivo", on);
                    Stopwatch sw = Stopwatch.StartNew();
                    for (int i = 0; i < 150; i++) Cuadro(p);
                    (on ? encendido : apagado)[ronda] = sw.Elapsed.TotalMilliseconds / 150;
                }
            Array.Sort(apagado); Array.Sort(encendido);
            Console.WriteLine("        un cuadro con la tarjeta imitada (mediana de 7 rondas alternadas): libro apagado " + (apagado[3] * 1000).ToString("0") + " us, encendido " + (encendido[3] * 1000).ToString("0") + " us (Linux/Mono, sintetico)");
            Check(encendido[3] - apagado[3] < 0.25, "tener el libro encendido cuesta menos de 0,25 ms por cuadro aqui (es diagnostico, no el modo normal)");
        }
        finally { umbral.SetValue(null, 12.0); ventana.SetValue(null, 60000.0); Llama(p, "set_LibroActivo", false); p.Dispose(); texturas.Clear(); }
        // el cierre vuelca lo que quedaba
        hojas.Clear();
        p = Monta(1024, 768);
        try
        {
            Pon(p, "ReportPeores", new Action<string>(delegate (string h) { hojas.Add(h); }));
            umbral.SetValue(null, 0.0);
            Llama(p, "set_LibroActivo", true);
            for (int i = 0; i < 5; i++) Cuadro(p);
            Check(hojas.Count == 0, "con la ventana de un minuto no sale nada durante el minuto");
            Llama(p, "VuelcaLibro");
            Check(hojas.Count == 1 && hojas[0].Contains("peores cuadros"), "al cerrar, VuelcaLibro saca lo que quedaba del minuto");
        }
        finally { umbral.SetValue(null, 12.0); Llama(p, "set_LibroActivo", false); p.Dispose(); texturas.Clear(); }
        // el cableado de Form1: solo con diagnostico=1 y al cerrar
        List<MethodBase> todos = Codigo.TodosLosMetodos(TF).ConvertAll<MethodBase>(delegate (MethodBase m) { return m; });
        bool enciende = false, vuelca = false, ligadoADiag = false;
        foreach (MethodBase m in todos)
        {
            List<string> ll = Codigo.Llamadas(m);
            if (Codigo.Llama(ll, "set_LibroActivo")) { enciende = true; if (Codigo.LeeCampo(new List<MethodBase>(new MethodBase[] { m }), "diagOn")) ligadoADiag = true; }
            if (Codigo.Llama(ll, "VuelcaLibro")) vuelca = true;
        }
        Check(enciende && ligadoADiag, "Form1 enciende el libro leyendo diagOn (diagnostico=1)");
        Check(vuelca, "Form1 vuelca el libro al cerrar");
    }


    // ======================= 1. EL PRECALENTADO DE LA TARJETA =======================
    static object Fn(string n, params object[] a) { return LlamaE(TF, n, a); }
    static Type Nes(string n) { return TF.GetNestedType(n, BindingFlags.NonPublic | BindingFlags.Public); }
    static Font Fuente(float pt) { return new Font(FontFamily.GenericSansSerif, pt, FontStyle.Bold); }
    static Bitmap portadaPrueba;
    static string Gen<T>(T a) { return "rock"; }
    static Image Tapa<T>(T a) { return portadaPrueba; }
    static Control MontaBuscador(int w, int h)
    {
        Type T = Nes("SearchOverlay"), TItem = Nes("SearchItem"), TAlb = Nes("Album");
        Control c = (Control)Activator.CreateInstance(T, true);
        c.SetBounds(0, 0, w, h); c.Font = Fuente(12f);
        System.Collections.IList res = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TItem));
        string[] nom = { "Depeche Mode - Enjoy The Silence", "Jose Jose - El Triste", "Metallica - Enter Sandman", "Juan Gabriel - Amor Eterno", "Bronco - Que No Quede Huella" };
        object alb = Activator.CreateInstance(TAlb, true);
        TAlb.GetField("Folder").SetValue(alb, @"C:\Musica\Disco"); TAlb.GetField("Name").SetValue(alb, "Un disco"); TAlb.GetField("Artist").SetValue(alb, "Un artista");
        TAlb.GetField("CoverPath").SetValue(alb, @"C:\Musica\Disco\cover.jpg");
        for (int i = 0; i < 40; i++)
        {
            object it = Activator.CreateInstance(TItem, true);
            TItem.GetField("Kind").SetValue(it, i % 3); TItem.GetField("Display").SetValue(it, nom[i % nom.Length] + " " + i);
            TItem.GetField("Key").SetValue(it, (nom[i % nom.Length] + " " + i).ToLowerInvariant()); TItem.GetField("Score").SetValue(it, 5000 - i);
            TItem.GetField("AlbumRef").SetValue(it, alb); res.Add(it);
        }
        Type tf = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
        T.GetField("GetCover").SetValue(c, Delegate.CreateDelegate(tf, typeof(TironesCambio).GetMethod("Tapa", TODO).MakeGenericMethod(TAlb)));
        FieldInfo fg = T.GetField("GetGenero");
        if (fg != null) fg.SetValue(c, Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(TItem, typeof(string)), typeof(TironesCambio).GetMethod("Gen", TODO).MakeGenericMethod(TItem)));
        T.GetField("Results").SetValue(c, res); T.GetField("Query").SetValue(c, "de");
        T.GetField("Accent").SetValue(c, Color.FromArgb(178, 152, 255)); T.GetField("FocusResults").SetValue(c, false); T.GetField("SelResult").SetValue(c, 0);
        return c;
    }
    static Control MontaLista(int w, int h)
    {
        Type T = Nes("SongListView");
        Control c = (Control)Activator.CreateInstance(T, true);
        c.SetBounds(0, 0, w, h); c.Font = Fuente(11f);
        Pon(c, "RowH", 38); Pon(c, "Accent", Color.FromArgb(150, 110, 220));
        Pon(c, "GetCount", (Func<int>)delegate { return 60; });
        Pon(c, "GetText", (Func<int, string>)delegate (int i) { return "Cancion numero " + (i + 1) + " con un titulo largo"; });
        Pon(c, "GetRight", (Func<int, string>)delegate (int i) { return "3:4" + (i % 10); });
        Pon(c, "GetQueuePos", (Func<int, int>)delegate (int i) { return (i % 7 == 0) ? 1 : (i % 11 == 0) ? -1 : 0; });
        Pon(c, "GetGenero", (Func<int, string>)delegate (int i) { return (i % 3 == 0) ? "Regional" : "Pop"; });
        return c;
    }

    // La pieza "de verdad" que pinta la tarjeta la primera vez (con datos distintos a los del precalentado). Solo se cronometra.
    static void PiezaReal(string pieza)
    {
        int w = 1024, h = 768;
        switch (pieza)
        {
            case "estado":
            {
                int ph = (int)LlamaE(TPanel, "IdlePanelHeight", h);
                string[] qt = { "Una cancion", "Otra cancion", "Y otra mas" }, qa = { "Alguien", "Alguien mas", "Un tercero" };
                object[] a = { null, w, ph, "Otro titulo mas largo para que no sea el mismo", "Otro artista", "", qt, qa, new Image[3], 3, Color.Teal, false, 0, false, null, false, 0 };
                using (Bitmap panel = (Bitmap)Fn("RenderIdlePanelEn", a))
                using (Bitmap debajo = new Bitmap(w, 34, PixelFormat.Format32bppPArgb))
                {
                    Fn("CopiaRegionReposo", panel, new Rectangle(0, ph - 34, w, 34), debajo, Point.Empty);
                    Fn("UpdateIdleClock", panel, debajo, "1:23 / 3:45");
                    object[] b = { panel, debajo, "1:24 / 3:45", 200, Rectangle.Empty };
                    Fn("UpdateIdleClockRegion", b);
                }
                break;
            }
            case "ficha":
            {
                object fam = Fn("FamiliaUi");
                using (Bitmap foco = new Bitmap(162, 25, PixelFormat.Format32bppPArgb)) Fn("PintaFoco", foco, "7 canciones - 20 min", "7 - 20 min", fam);
                Size t = (Size)Fn("TamanoFicha", 170, 189);
                using (Bitmap ficha = new Bitmap(t.Width, t.Height, PixelFormat.Format32bppPArgb))
                    Fn("PintaFicha", ficha, null, "Otro artista", "Otro disco", "7 canciones", "Rock", Color.OrangeRed, 0, false,
                        new string[] { "Una", "Dos" }, new string[] { "3:00", "2:00" }, new int[] { 1, 0 }, 7, fam);
                break;
            }
            case "asistente":
            {
                Size g = ((Rectangle)LlamaE(TPanel, "AsistentePanelRect", w, h)).Size;
                using (Bitmap p = (Bitmap)Fn("PintaPanelAsistente", g.Width, g.Height, 0, 0, 1)) { }
                using (Bitmap a = (Bitmap)Fn("PintaAdiosAsistente", g.Width, g.Height)) { }
                break;
            }
            case "avisos":
            {
                FontFamily ff = (FontFamily)Fn("FamiliaUi");
                Type ta = Nes("TipoAviso");
                using (Font tit = new Font(ff, 14f, FontStyle.Bold)) using (Font det = new Font(ff, 11f, FontStyle.Bold))
                {
                    Size sz = (Size)Fn("TamanoAviso", w, "Pusiste otra", "va la 2.a", tit, det);
                    using (Bitmap b = new Bitmap(sz.Width, sz.Height, PixelFormat.Format32bppPArgb)) Fn("PintaAviso", b, Enum.ToObject(ta, 1), "Pusiste otra", "va la 2.a", tit, det);
                }
                using (Font tit = new Font(ff, 17f, FontStyle.Bold)) using (Font txt = new Font(ff, 12.5f, FontStyle.Bold)) using (Font pie = new Font(ff, 10.5f, FontStyle.Bold))
                {
                    Size t = (Size)Fn("TamanoInvita", w);
                    using (Bitmap b = new Bitmap(t.Width, t.Height, PixelFormat.Format32bppPArgb))
                    { object[] a = { b, "Tu cancion", false, 4, tit, txt, pie, new PointF(), 0f }; Fn("PintaInvita", a); }
                }
                using (Bitmap m = (Bitmap)LlamaE(TPanel, "BakeMoneda", 96)) { }
                using (Font mas = new Font(ff, 14f, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    Size t = (Size)Fn("TamanoMas", "+2 creditos", mas);
                    using (Bitmap b = new Bitmap(t.Width, t.Height, PixelFormat.Format32bppPArgb)) Fn("PintaMas", b, "+2 creditos", mas);
                }
                break;
            }
        }
    }
    static void GdiCaliente()
    {
        using (Bitmap b = new Bitmap(64, 64, PixelFormat.Format32bppPArgb))
        using (Graphics g = Graphics.FromImage(b))
        using (Font f = new Font(FontFamily.GenericSansSerif, 12f, FontStyle.Bold, GraphicsUnit.Pixel))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.DrawString("hola", f, Brushes.White, 2, 2); g.FillEllipse(Brushes.Red, 4, 4, 20, 20);
            using (System.Drawing.Drawing2D.GraphicsPath p = new System.Drawing.Drawing2D.GraphicsPath()) { p.AddEllipse(1, 1, 30, 30); g.FillPath(Brushes.Blue, p); }
        }
    }
    // Modo hijo: una pieza, fria o precalentada, en un proceso nuevo (la primera vez de verdad: compilar el codigo y crear lo que haga falta).
    static int Hijo(string pieza, string modo)
    {
        TF = AsmNuevo.GetType("Rocola.Form1"); TPanel = AsmNuevo.GetType("Rocola.GpuPanel");
        TD3D = AsmNuevo.GetType("Rocola.D3D9");
        GdiCaliente();
        if (modo == "caliente")
        {
            int paso = pieza == "estado" ? 3 : pieza == "ficha" ? 4 : pieza == "asistente" ? 5 : 6;
            LlamaE(TF, "PrecalientaPiezaGpuEn", paso, 1024, 768, false, false, false, 0);
        }
        Stopwatch sw = Stopwatch.StartNew();
        PiezaReal(pieza);
        Console.WriteLine("MS " + sw.Elapsed.TotalMilliseconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
        return 0;
    }
    static double CorreHijo(string rocola, string pieza, string modo)
    {
        ProcessStartInfo psi = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName);
        string yo = Environment.GetCommandLineArgs()[0];
        psi.Arguments = "\"" + yo + "\" \"" + rocola + "\" --hijo " + pieza + " " + modo;
        psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
        using (Process p = Process.Start(psi))
        {
            string o = p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit();
            foreach (string l in o.Split('\n')) if (l.StartsWith("MS ")) return double.Parse(l.Substring(3), System.Globalization.CultureInfo.InvariantCulture);
        }
        return -1;
    }
    static double Mediana(List<double> v) { v.Sort(); return v[v.Count / 2]; }
    static double MedianaA(double[] v) { double[] c = (double[])v.Clone(); Array.Sort(c); return c[c.Length / 2]; }
    static void Precalentado(string rocola)
    {
        Console.WriteLine("# 1. el precalentado de lo que pinta la tarjeta");
        MethodInfo pre = Metodo(TF, "PrecalientaUnPoco");
        List<string> ll = Codigo.ConAyudantes(TF, "PrecalientaUnPoco");
        Check(Codigo.Llama(ll, "TryEnter") && Codigo.Llama(ll, "get_CuadrosMedidos") && Codigo.Llama(ll, "get_MsTrabajo"), "conserva sus guardas: el turno del DSP (TryEnter), cuadros medidos y el margen del ultimo cuadro (MsTrabajo)");
        int lee, esc; Codigo.UsosDeCampo(pre, "precalUltimoCoste", out lee, out esc);
        Check(lee > 0 && esc > 0, "sigue midiendo lo que cuesta cada paso (precalUltimoCoste) para el margen de 28 ms");
        Codigo.UsosDeCampo(pre, "lastInputTick", out lee, out esc);
        Check(lee > 0, "sigue parandose si alguien esta usando la rocola (PrecalQuieta)");
        Check(Codigo.Llama(ll, "PrecalientaBuscadorGpu") && Codigo.Llama(ll, "PrecalientaListaGpu") && Codigo.Llama(ll, "PrecalientaPiezaGpu"), "con tarjeta calienta el buscador y la lista por su camino de la tarjeta y las piezas que solo pinta ella");
        Check(Codigo.LeeCampo(new List<MethodBase>(new MethodBase[] { pre }), "attractView") && Codigo.LeeCampo(new List<MethodBase>(new MethodBase[] { pre }), "searchOverlay"), "y el camino de siempre sigue ahi (sin tarjeta)");
        // lo que NO toca: ni el panel del estado extendido, ni el del asistente, ni los avisos que haya a la vista
        List<MethodBase> todos = Codigo.MetodosConAyudantes(TF, "PrecalientaPiezaGpu");
        string[] visibles = { "idlePanelBmp", "idlePanelSig", "idleClockUnderlay", "asistentePanel", "avisoBmp", "invitaBmp", "monedaBmp", "monedaMas", "fichaBmp", "focoBmp" };
        bool toca = false;
        foreach (string v in visibles) if (Codigo.EscribeCampo(todos, v) || Codigo.LeeCampo(todos, v)) { toca = true; Console.WriteLine("        toca " + v); }
        Check(!toca, "el precalentado de las piezas no lee ni escribe el panel del estado, el del asistente, los avisos ni la ficha/pastilla que haya a la vista");
        todos = Codigo.MetodosConAyudantes(TF, "PrecalientaListaGpu");
        Check(!Codigo.EscribeCampo(todos, "Sel") && !Codigo.EscribeCampo(todos, "Accent"), "la lista: solo se mueve la marca pintandoGpu (y vuelve como estaba)");
        // Corre sin romper en varias pantallas y no deja memoria colgada
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); long antes = GC.GetTotalMemory(true);
        foreach (int[] wh in new int[][] { new int[] { 1024, 768 }, new int[] { 1280, 1024 }, new int[] { 1920, 1080 }, new int[] { 640, 480 } })
            foreach (bool doce in new bool[] { false, true })
                for (int paso = 3; paso <= 6; paso++) LlamaE(TF, "PrecalientaPiezaGpuEn", paso, wh[0], wh[1], doce, doce, !doce, doce ? 1 : 0);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); long despues = GC.GetTotalMemory(true);
        Check(despues - antes < 4L * 1024 * 1024, "32 pasadas por 4 pantallas dejan menos de 4 MB (los mapas de bits se sueltan): " + ((despues - antes) / 1024) + " KB");
        // La prueba que manda: la primera vez de verdad, fria contra precalentada, en procesos nuevos y alternados (mediana de 5)
        Console.WriteLine("        primera vez de cada pieza en un proceso nuevo (mediana de 5, alternando; Linux/Mono, sintetico; ms)");
        foreach (string pz in new string[] { "estado", "ficha", "asistente", "avisos" })
        {
            List<double> fr = new List<double>(), ca = new List<double>();
            for (int r = 0; r < 5; r++)
            {
                if (r % 2 == 0) { fr.Add(CorreHijo(rocola, pz, "frio")); ca.Add(CorreHijo(rocola, pz, "caliente")); }
                else { ca.Add(CorreHijo(rocola, pz, "caliente")); fr.Add(CorreHijo(rocola, pz, "frio")); }
            }
            double mf = Mediana(fr), mc = Mediana(ca);
            Console.WriteLine("        " + pz.PadRight(10) + " fria " + mf.ToString("0.0") + "   precalentada " + mc.ToString("0.0") + "   ahorro " + (mf - mc).ToString("0.0"));
            Check(mf > 0 && mc > 0 && mc < mf * 0.8, "la primera vez de '" + pz + "' cuesta menos precalentada (" + mc.ToString("0.0") + " ms contra " + mf.ToString("0.0") + " ms)");
        }
        // El buscador y la lista por el camino de la tarjeta (por control: lo horneado en ese control; mediana de 7)
        portadaPrueba = new Bitmap(500, 500, PixelFormat.Format32bppPArgb);
        using (Graphics pg = Graphics.FromImage(portadaPrueba)) pg.Clear(Color.FromArgb(255, 30, 40, 90));
        Type TSO = Nes("SearchOverlay"), TSL = Nes("SongListView");
        MethodInfo rcB = TSO.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) }), rcL = TSL.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
        MethodInfo olvida = TSL.GetMethod("OlvidaPagina", BindingFlags.Instance | BindingFlags.Public);
        FieldInfo fpg = TSL.GetField("pintandoGpu", TODO);
        double[] frioB = new double[7], tibioB = new double[7], frioL = new double[7], tibioL = new double[7];
        for (int i = 0; i < 7; i++)
        {
            Control a = MontaBuscador(1024, 768), b = MontaBuscador(1024, 768);
            using (Bitmap bm = new Bitmap(1024, 768, PixelFormat.Format32bppPArgb)) using (Graphics g = Graphics.FromImage(bm))
            { Stopwatch sw = Stopwatch.StartNew(); rcB.Invoke(a, new object[] { g, new Rectangle(0, 0, 1024, 768), true }); frioB[i] = sw.Elapsed.TotalMilliseconds; }
            using (Bitmap sec = new Bitmap(1024, 768, PixelFormat.Format32bppPArgb)) using (Graphics sg = Graphics.FromImage(sec)) rcB.Invoke(b, new object[] { sg, new Rectangle(0, 0, 1024, 768), true });
            using (Bitmap bm = new Bitmap(1024, 768, PixelFormat.Format32bppPArgb)) using (Graphics g = Graphics.FromImage(bm))
            { Stopwatch sw = Stopwatch.StartNew(); rcB.Invoke(b, new object[] { g, new Rectangle(0, 0, 1024, 768), true }); tibioB[i] = sw.Elapsed.TotalMilliseconds; }
            Control c = MontaLista(980, 532), d = MontaLista(980, 532);
            fpg.SetValue(c, true); fpg.SetValue(d, true);
            using (Bitmap bm = new Bitmap(980, 532, PixelFormat.Format32bppPArgb)) using (Graphics g = Graphics.FromImage(bm))
            { Stopwatch sw = Stopwatch.StartNew(); rcL.Invoke(c, new object[] { g }); frioL[i] = sw.Elapsed.TotalMilliseconds; }
            using (Bitmap sec = new Bitmap(980, 532, PixelFormat.Format32bppPArgb)) using (Graphics sg = Graphics.FromImage(sec)) rcL.Invoke(d, new object[] { sg });
            if (olvida != null) olvida.Invoke(d, null);   // abrir un disco tira la pagina horneada
            using (Bitmap bm = new Bitmap(980, 532, PixelFormat.Format32bppPArgb)) using (Graphics g = Graphics.FromImage(bm))
            { Stopwatch sw = Stopwatch.StartNew(); rcL.Invoke(d, new object[] { g }); tibioL[i] = sw.Elapsed.TotalMilliseconds; }
        }
        Console.WriteLine("        buscador por la tarjeta: fria " + MedianaA(frioB).ToString("0.00") + " ms, precalentada " + MedianaA(tibioB).ToString("0.00") + " ms (mediana de 7, un proceso ya con el codigo compilado)");
        Console.WriteLine("        lista del disco con la marca de la tarjeta, al abrir un disco: fria " + MedianaA(frioL).ToString("0.00") + " ms, precalentada " + MedianaA(tibioL).ToString("0.00") + " ms");
        Check(MedianaA(tibioB) < MedianaA(frioB), "el buscador por el camino de la tarjeta cuesta menos la primera vez ya precalentado");
        Check(MedianaA(tibioL) < MedianaA(frioL), "la lista con la marca de la tarjeta cuesta menos al abrir un disco ya precalentada");
        // y precalentar no cambia ni un pixel
        MethodInfo sacaB = rcB;
        Bitmap f1 = new Bitmap(1024, 768, PixelFormat.Format32bppPArgb), f2 = new Bitmap(1024, 768, PixelFormat.Format32bppPArgb);
        Control u = MontaBuscador(1024, 768), v2 = MontaBuscador(1024, 768);
        using (Graphics g = Graphics.FromImage(f1)) sacaB.Invoke(u, new object[] { g, new Rectangle(0, 0, 1024, 768), true });
        using (Bitmap sec = new Bitmap(1024, 768, PixelFormat.Format32bppPArgb)) using (Graphics sg = Graphics.FromImage(sec)) sacaB.Invoke(v2, new object[] { sg, new Rectangle(0, 0, 1024, 768), true });
        using (Graphics g = Graphics.FromImage(f2)) sacaB.Invoke(v2, new object[] { g, new Rectangle(0, 0, 1024, 768), true });
        Check(IgualesPx(f1, f2), "el buscador sale pixel por pixel igual recien abierto que precalentado");
        f1.Dispose(); f2.Dispose();
        Bitmap l1 = new Bitmap(980, 532, PixelFormat.Format32bppPArgb), l2 = new Bitmap(980, 532, PixelFormat.Format32bppPArgb);
        Control lu = MontaLista(980, 532), lv = MontaLista(980, 532); fpg.SetValue(lu, true); fpg.SetValue(lv, true);
        using (Graphics g = Graphics.FromImage(l1)) rcL.Invoke(lu, new object[] { g });
        using (Bitmap sec = new Bitmap(980, 532, PixelFormat.Format32bppPArgb)) using (Graphics sg = Graphics.FromImage(sec)) rcL.Invoke(lv, new object[] { sg });
        using (Graphics g = Graphics.FromImage(l2)) rcL.Invoke(lv, new object[] { g });
        Check(IgualesPx(l1, l2), "la lista sale pixel por pixel igual recien abierta que precalentada");
        l1.Dispose(); l2.Dispose();
    }
    static bool IgualesPx(Bitmap a, Bitmap b)
    {
        BitmapData da = a.LockBits(new Rectangle(0, 0, a.Width, a.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        BitmapData db = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            byte[] x = new byte[Math.Abs(da.Stride)], y = new byte[Math.Abs(db.Stride)];
            for (int r = 0; r < a.Height; r++)
            {
                Marshal.Copy(new IntPtr(da.Scan0.ToInt64() + (long)r * da.Stride), x, 0, x.Length);
                Marshal.Copy(new IntPtr(db.Scan0.ToInt64() + (long)r * db.Stride), y, 0, y.Length);
                for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            }
            return true;
        }
        finally { a.UnlockBits(da); b.UnlockBits(db); }
    }


    // ======================= 2. LOS BORDES DE LA CELDA SEPARADA, DIBUJADOS UNA VEZ =======================
    static bool filaDeCola;
    static bool Cola<T>(T a) { return filaDeCola; }
    static string Plano(object lay)
    {
        StringBuilder sb = new StringBuilder();
        foreach (FieldInfo f in lay.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) sb.Append(f.Name).Append('=').Append(f.GetValue(lay)).Append(';');
        return sb.ToString();
    }
    static byte[] Bytes(Bitmap b)
    {
        BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            byte[] r = new byte[b.Width * 4 * b.Height], fila = new byte[b.Width * 4];
            for (int y = 0; y < b.Height; y++) { Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), fila, 0, fila.Length); Buffer.BlockCopy(fila, 0, r, y * fila.Length, fila.Length); }
            return r;
        }
        finally { b.UnlockBits(d); }
    }
    static bool Iguales(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    // Una celda separada con el ensamblado dado. 'vista' se reutiliza para tener la cache caliente, o se pasa null para una vista nueva (fria).
    static Bitmap Hornea(Assembly asm, ref object vista, int cw, int ch, bool cola, bool lod, out string plano, out int side)
    {
        Type V = asm.GetType("Rocola.Form1").GetNestedType("AlbumView", BindingFlags.NonPublic | BindingFlags.Public), A = asm.GetType("Rocola.Form1").GetNestedType("Album", BindingFlags.NonPublic | BindingFlags.Public);
        if (vista == null)
        {
            vista = Activator.CreateInstance(V, true);
            Pon(vista, "EnCola", Delegate.CreateDelegate(Campo(V, "EnCola").FieldType, typeof(TironesCambio).GetMethod("Cola", TODO).MakeGenericMethod(A)));
        }
        Pon(vista, "PortadasLod", lod);
        filaDeCola = cola;
        object a = Activator.CreateInstance(A, true);
        Pon(a, "Folder", "test"); Pon(a, "Name", "Nombre largo de un disco"); Pon(a, "Artist", "Artista");
        object[] m = { cw, ch, 0, 0, 0 };
        V.GetMethod("CellMetrics", TODO).Invoke(null, m);
        side = (int)m[2]; int lh = (int)m[3], gap = (int)m[4];
        object[] args = { a, cw, side, lh, gap, null };
        Bitmap r;
        try { r = (Bitmap)V.GetMethod("BakeSeparatedCell", TODO).Invoke(vista, args); } catch (TargetInvocationException e) { throw e.InnerException; }
        plano = Plano(args[5]);
        return r;
    }
    static void Bordes(string rutaNuevo, string rutaViejo)
    {
        Console.WriteLine("# 2. los bordes de la celda separada, dibujados una vez por (cw, side)");
        Type V = Nes("AlbumView");
        Check(Metodo(V, "PegaBordes") != null, "existe PegaBordes");
        Size[] pantallas = { new Size(1024, 768), new Size(1280, 1024), new Size(1920, 1080), new Size(2560, 1440), new Size(3440, 1440), new Size(640, 480) };
        int casos = 0, diferentes = 0, copiados = 0;
        foreach (Size sz in pantallas)
            foreach (bool cola in new bool[] { false, true })
                foreach (bool lod in new bool[] { false, true })
                {
                    int cw = sz.Width / 6, ch = (sz.Height - 160) / 3;
                    object caliente = null; string pl0, pl1, pl2; int sd;
                    using (Bitmap fria = Hornea(AsmNuevo, ref caliente, cw, ch, cola, lod, out pl0, out sd))        // la primera: dibuja los bordes
                    using (Bitmap tibia = Hornea(AsmNuevo, ref caliente, cw, ch, cola, lod, out pl1, out sd))      // la segunda: los copia
                    {
                        object nueva = null;
                        using (Bitmap otra = Hornea(AsmNuevo, ref nueva, cw, ch, cola, lod, out pl2, out sd))
                        {
                            casos++;
                            bool ok = Iguales(Bytes(fria), Bytes(tibia)) && Iguales(Bytes(fria), Bytes(otra)) && pl0 == pl1 && pl0 == pl2;
                            if (!ok) diferentes++;
                            copiados += (int)Toma(caliente, "BordesCopiados");
                        }
                    }
                }
        Check(diferentes == 0, casos + " celdas (6 pantallas x en cola o no x LOD): fria, tibia y de una vista nueva salen byte a byte iguales");
        // contra la rama antes del cambio: la huella de bytes y el plano de las 24 celdas, sacadas con el ejecutable de c29b73e (celdas_separadas_2026-10-20.txt;
        // son los bytes de ESTE equipo -Linux/Mono con libgdiplus-, no los de GDI+ de Windows). Y, si se da un ejecutable anterior como segundo argumento,
        // contra el: cada uno en su proceso, porque dos ensamblados "rocola" en uno solo se confunden.
        string dn = CorreVolcado(rutaNuevo);
        string basef = Path.Combine(Directory.GetCurrentDirectory(), "celdas_separadas_2026-10-20.txt");
        if (Type.GetType("Mono.Runtime") != null && File.Exists(basef))
            Check(dn.Length > 0 && dn.Replace("\r", "") == File.ReadAllText(basef).Replace("\r", ""), "las " + casos + " celdas, dos veces cada una (bordes dibujados y copiados), son byte a byte las de antes del cambio (celdas_separadas_2026-10-20.txt)");
        if (rutaViejo != null)
        {
            string dv = CorreVolcado(rutaViejo);
            Check(dn.Length > 0 && dn == dv, "y byte a byte las del ejecutable anterior dado (cada uno en su proceso)");
            List<double> tn = new List<double>(), tv = new List<double>();
            for (int r = 0; r < 5; r++)
            {
                if (r % 2 == 0) { tv.Add(CorreTiempo(rutaViejo)); tn.Add(CorreTiempo(rutaNuevo)); } else { tn.Add(CorreTiempo(rutaNuevo)); tv.Add(CorreTiempo(rutaViejo)); }
            }
            Console.WriteLine("        una celda separada a 1024x768 (misma vista, mediana de 5 procesos alternados; Linux/Mono con libgdiplus, sintetico): antes " + Mediana(tv).ToString("0.000") + " ms, ahora " + Mediana(tn).ToString("0.000") + " ms");
            Check(Mediana(tn) < Mediana(tv), "hornear una celda separada cuesta menos que con el ejecutable anterior");
        }
        Check(copiados > 0, "la segunda celda de cada vista copia los bordes en vez de dibujarlos (" + copiados + " copias)");
        // cambiar de resolucion rehace los bordes
        object v2 = null; string p; int s1, s2;
        using (Bitmap a = Hornea(AsmNuevo, ref v2, 170, 202, false, false, out p, out s1)) { }
        int dib0 = (int)Toma(v2, "BordesDibujados");
        using (Bitmap a = Hornea(AsmNuevo, ref v2, 213, 202, false, false, out p, out s2)) { }
        Check((int)Toma(v2, "BordesDibujados") == dib0 + 1, "al cambiar el ancho de la celda los bordes se rehacen");
        using (Bitmap a = Hornea(AsmNuevo, ref v2, 213, 202, false, false, out p, out s2)) { }
        Check((int)Toma(v2, "BordesDibujados") == dib0 + 1, "y con la misma medida no");
        // el tiempo: fria (una vista nueva por celda = dibuja los cuatro bordes cada vez, como antes) contra tibia (los copia)
        int N = 36; double[] cfria = new double[7], ctib = new double[7], cvieja = new double[7]; cvieja[0] = 0;
        string pp; int ss; object cal = null;
        Hornea(AsmNuevo, ref cal, 170, 202, false, false, out pp, out ss).Dispose();
        for (int ronda = 0; ronda < 7; ronda++)
        {
            for (int orden = 0; orden < 3; orden++)
            {
                int que = (orden + ronda) % 3;
                if (que == 2) continue;
                Stopwatch sw = Stopwatch.StartNew();
                for (int i = 0; i < N; i++)
                {
                    if (que == 0) { object nv = null; Hornea(AsmNuevo, ref nv, 170, 202, false, false, out pp, out ss).Dispose(); }
                    else if (que == 1) Hornea(AsmNuevo, ref cal, 170, 202, false, false, out pp, out ss).Dispose();
                    else { }
                }
                double ms = sw.Elapsed.TotalMilliseconds / N;
                if (que == 0) cfria[ronda] = ms; else if (que == 1) ctib[ronda] = ms; else cvieja[ronda] = ms;
            }
        }
        Console.WriteLine("        hornear una celda separada a 1024x768 (mediana de 7 rondas alternadas de " + N + "; Linux/Mono con libgdiplus, sintetico): bordes dibujados cada vez (vista nueva) " + MedianaA(cfria).ToString("0.000") + " ms, bordes copiados " + MedianaA(ctib).ToString("0.000") + " ms"
);
        Check(MedianaA(ctib) < MedianaA(cfria), "copiar los bordes cuesta menos que dibujarlos en cada celda");
    }

    // ======================= 5. EL ALFA Y LA SUBIDA SIN COPIAR LOS PArgb =======================
    // Un mapa de bits PArgb de prueba con valores premultiplicados validos (color <= alfa). tipo: 0 opaco, 1 alfas al azar, 2 opaco menos el ultimo pixel (254),
    // 3 opaco menos el primero (0), 4 opaco con un pixel a 128 en la ultima fila, 5 todo transparente, 6 centro opaco y marco transparente, 7 opaco con un pixel a 1 en el centro.
    static Bitmap Pa(int w, int h, int tipo, int semilla)
    {
        Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        Random r = new Random(semilla * 7919 + tipo);
        BitmapData d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try
        {
            int[] fila = new int[w];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int a = 255;
                    switch (tipo)
                    {
                        case 1: a = r.Next(256); break;
                        case 2: if (x == w - 1 && y == h - 1) a = 254; break;
                        case 3: if (x == 0 && y == 0) a = 0; break;
                        case 4: if (x == w / 2 && y == h - 1) a = 128; break;
                        case 5: a = 0; break;
                        case 6: if (x < 3 || y < 3 || x >= w - 3 || y >= h - 3) a = 0; break;
                        case 7: if (x == w / 2 && y == h / 2) a = 1; break;
                    }
                    int rr = a == 0 ? 0 : r.Next(a + 1), gg = a == 0 ? 0 : r.Next(a + 1), bb = a == 0 ? 0 : r.Next(a + 1);
                    fila[x] = (a << 24) | (rr << 16) | (gg << 8) | bb;
                }
                Marshal.Copy(fila, 0, new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), w);
            }
        }
        finally { b.UnlockBits(d); }
        return b;
    }
    static string Md5(byte[] b) { return BitConverter.ToString(System.Security.Cryptography.MD5.Create().ComputeHash(b)).Replace("-", ""); }
    static int[][] tamanosTex = { new int[] { 1, 1 }, new int[] { 7, 5 }, new int[] { 64, 64 }, new int[] { 130, 70 }, new int[] { 300, 200 }, new int[] { 515, 33 } };
    static byte[] BytesTextura(IntPtr tex)
    {
        Tx t = texturas[tex.ToInt64()];
        byte[] r = new byte[t.W * t.H * 4]; Marshal.Copy(t.Mem, r, 0, r.Length); return r;
    }
    static int Volcado5(bool texturas5)
    {
        Type TTe = AsmNuevo.GetType("Rocola.TeselasVisibles"), TTr = AsmNuevo.GetType("Rocola.TrabajoTeselas");
        object tarjeta = Tarjeta(640, 480);
        foreach (int[] wh in tamanosTex)
            for (int tipo = 0; tipo < 8; tipo++)
                using (Bitmap b = Pa(wh[0], wh[1], tipo, 3))
                {
                    string id = wh[0] + "x" + wh[1] + " tipo" + tipo;
                    foreach (bool borde in new bool[] { false, true })
                    {
                        object te = TTe.GetMethod("De", TODO).Invoke(null, new object[] { b, 16, borde });
                        Console.WriteLine("VOLCADO TESELAS " + id + " borde=" + borde + " " + (te == null ? "nulo" : Toma(te, "N") + " " + Md5(IntsABytes((int[])Toma(te, "Rect"))) + " " + ((double)Toma(te, "Fraccion")).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                    object tr = Activator.CreateInstance(TTr, TODO, null, new object[] { b, 16 }, null), res = null; int pasos = 0;
                    while (res == null && pasos++ < 100000) res = TTr.GetMethod("Avanza", TODO).Invoke(tr, new object[] { 1000000L });
                    Console.WriteLine("VOLCADO TRABAJO " + id + " " + (res == null ? "nulo" : Toma(res, "N") + " " + Md5(IntsABytes((int[])Toma(res, "Rect")))));
                    Console.WriteLine("VOLCADO OPACO " + id + " " + LlamaE(TPanel, "InteriorOpaco", b));
                    if (texturas5)
                    {
                        object[] a = { b, 0f, 0f };
                        IntPtr tex = (IntPtr)Llama(tarjeta, "CreateTexturePow2", a);
                        Console.WriteLine("VOLCADO TEXTURA " + id + " " + (tex == IntPtr.Zero ? "nula" : Md5(BytesTextura(tex)) + " " + a[1] + " " + a[2]));
                    }
                }
        return 0;
    }
    static byte[] IntsABytes(int[] v) { byte[] r = new byte[v.Length * 4]; Buffer.BlockCopy(v, 0, r, 0, r.Length); return r; }
    static int Tiempo5()
    {
        Type TTe = AsmNuevo.GetType("Rocola.TeselasVisibles"), TTr = AsmNuevo.GetType("Rocola.TrabajoTeselas");
        // una interfaz de pantalla completa (alfa al azar en una franja) y una pieza opaca
        Bitmap ui = Pa(1024, 300, 6, 1), cubierta = Pa(512, 512, 0, 2);
        double[] de = new double[9], tr = new double[9], op = new double[9];
        MethodInfo mDe = TTe.GetMethod("De", TODO), mOp = TPanel.GetMethod("InteriorOpaco", TODO);
        for (int k = 0; k < 9; k++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 6; i++) mDe.Invoke(null, new object[] { ui, 16, false });
            de[k] = sw.Elapsed.TotalMilliseconds / 6;
            sw.Reset(); sw.Start();
            for (int i = 0; i < 6; i++) { object t = Activator.CreateInstance(TTr, TODO, null, new object[] { ui, 16 }, null); while (TTr.GetMethod("Avanza", TODO).Invoke(t, new object[] { 1000000L }) == null) { } }
            tr[k] = sw.Elapsed.TotalMilliseconds / 6;
            sw.Reset(); sw.Start();
            for (int i = 0; i < 20; i++) mOp.Invoke(null, new object[] { cubierta });
            op[k] = sw.Elapsed.TotalMilliseconds / 20;
        }
        Console.WriteLine("MS " + MedianaA(de).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " " + MedianaA(tr).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " "
            + MedianaA(op).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
        return 0;
    }
    static string CorreModo(string rocola, string modo)
    {
        ProcessStartInfo psi = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName);
        psi.Arguments = "\"" + Environment.GetCommandLineArgs()[0] + "\" \"" + rocola + "\" " + modo;
        psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
        using (Process p = Process.Start(psi)) { string o = p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit(); return o.Replace("\r", ""); }
    }
    static string Lineas(string salida, string prefijo)
    {
        StringBuilder sb = new StringBuilder();
        foreach (string l in salida.Split('\n')) if (l.StartsWith(prefijo)) sb.Append(l).Append('\n');
        return sb.ToString();
    }
    static void Alfa(string rutaNuevo, string rutaViejo)
    {
        Console.WriteLine("# 5. el alfa y la subida de los PArgb, sin la copia de GDI+");
        string nuevo = CorreModo(rutaNuevo, "--volcado5");
        string vol = Lineas(nuevo, "VOLCADO ");
        Check(vol.Length > 0, "se sacaron las huellas de teselas, trabajo por tandas, interior opaco y bytes de textura (" + vol.Split('\n').Length + " lineas)");
        string basef = Path.Combine(Directory.GetCurrentDirectory(), "texturas_2026-10-20.txt");
        if (Type.GetType("Mono.Runtime") != null && File.Exists(basef))
            Check(vol == File.ReadAllText(basef).Replace("\r", ""), "teselas, interior opaco y los bytes de cada textura subida (el camino de subida no cambia) son los de antes del cambio, byte a byte (texturas_2026-10-20.txt: 6 tamanos x 8 clases de alfa)");
        if (rutaViejo != null)
            Check(vol == Lineas(CorreModo(rutaViejo, "--volcado5"), "VOLCADO "), "y los del ejecutable anterior dado (cada uno en su proceso)");
        // el tiempo, en procesos alternados
        if (rutaViejo != null)
        {
            List<double>[] a = new List<double>[3], b = new List<double>[3];
            for (int i = 0; i < 3; i++) { a[i] = new List<double>(); b[i] = new List<double>(); }
            for (int r = 0; r < 5; r++)
                foreach (bool nuevoPrimero in new bool[] { r % 2 == 0, r % 2 != 0 })
                {
                    string[] m = Lineas(CorreModo(nuevoPrimero ? rutaNuevo : rutaViejo, "--tiempo5"), "MS ").Trim().Split(' ');
                    for (int i = 0; i < 3; i++) (nuevoPrimero ? a : b)[i].Add(double.Parse(m[i + 1], System.Globalization.CultureInfo.InvariantCulture));
                }
            string[] que = { "teselas visibles de una interfaz 1024x300", "teselas por tandas de la misma", "interior opaco de una portada 512x512" };
            Console.WriteLine("        (mediana de 5 procesos alternados; Linux/Mono con libgdiplus, sintetico; ms)  antes -> ahora");
            for (int i = 0; i < 3; i++) Console.WriteLine("        " + que[i].PadRight(44) + Mediana(b[i]).ToString("0.000") + " -> " + Mediana(a[i]).ToString("0.000"));
        }
    }

    // ======================= 8. LA MEMORIA DE TEXTURAS SE PREGUNTA MENOS =======================
    static int SondeosSimulados(Type panel)
    {
        // El bucle de RefreshCellBudget con relojes inventados: 180 s a un cuadro cada 100 ms, la pagina quieta.
        MethodInfo puede = panel.GetMethod("CanProbeCoverMemory", TODO);
        double comprobado = -2000, cambio = 0; int n = 0;
        for (double t = 0; t <= 180000; t += 100)
            if ((bool)puede.Invoke(null, new object[] { t, cambio, comprobado, comprobado < 0, false })) { comprobado = t; n++; }
        return n;
    }
    static int Sondeos()
    {
        TF = AsmNuevo.GetType("Rocola.Form1"); TPanel = AsmNuevo.GetType("Rocola.GpuPanel");
        Console.WriteLine("SONDEOS " + SondeosSimulados(TPanel));
        return 0;
    }
    static void MemoriaDeTexturas(string rutaViejo)
    {
        Console.WriteLine("# 8. GetAvailableTextureMem, menos veces");
        int nuevo = SondeosSimulados(TPanel);
        Console.WriteLine("        180 s con la pagina quieta (relojes inventados): " + nuevo + " sondeos");
        if (rutaViejo != null)
        {
            string o = CorreModo(rutaViejo, "--sondeos");
            int viejo = int.Parse(Lineas(o, "SONDEOS").Trim().Substring(8));
            Console.WriteLine("        con el ejecutable anterior: " + viejo + " sondeos");
            Check(nuevo < viejo, "el ejecutable nuevo pregunta menos veces a la tarjeta que el anterior (" + nuevo + " contra " + viejo + " en 180 s)");
        }
        Check(nuevo == 3, "en 180 s quieto: al empezar, al minuto y a los dos minutos (3 sondeos)");
        // Con la tarjeta imitada que cuenta las llamadas (ranura 4 del dispositivo)
        Control p = Monta(1024, 768);
        try
        {
            MethodInfo refresca = Metodo(TPanel, "RefreshCellBudget", typeof(double));
            Action<double> paso = delegate (double t) { refresca.Invoke(p, new object[] { t }); };
            PonCeroCuentas();
            for (double t = 1000; t <= 181000; t += 100) paso(t);
            Check(cMem == 3, "el panel con la tarjeta imitada llama a GetAvailableTextureMem 3 veces en 180 s (" + cMem + ")");
            // al cambiar la resolucion se vuelve a preguntar (despues de asentarse), una vez
            PonCeroCuentas();
            p.SetBounds(0, 0, 1280, 1024);
            for (double t = 181100; t <= 186000; t += 100) paso(t);
            Check(cMem == 1, "al cambiar la resolucion se pregunta una vez, ya asentada (" + cMem + ")");
            // una textura que no se pudo crear lo pide, sin pasar de uno cada 2 s
            PonCeroCuentas();
            Pon(p, "sondeoMemoriaPedido", true);
            for (double t = 190000; t <= 200000; t += 100) { Pon(p, "sondeoMemoriaPedido", true); paso(t); }
            Check(cMem >= 4 && cMem <= 6, "una textura que no se pudo crear pide preguntar, y no mas de uno cada 2 s (" + cMem + " en 10 s de peticiones en cada cuadro)");
            // el estado extendido: nada
            PonCeroCuentas();
            Pon(p, "IdleExpandTarget", true);
            for (double t = 300000; t <= 480000; t += 100) { Pon(p, "sondeoMemoriaPedido", true); paso(t); }
            Check(cMem == 0, "con el estado extendido abierto no se pregunta nunca (" + cMem + ")");
            Pon(p, "IdleExpandTarget", false);
            // y una textura que de verdad falla al crearse deja la peticion
            Pon(p, "sondeoMemoriaPedido", false);
            creaFalla = true; creaFallos = 0;
            Pon(p, "ShowAlbum", true);
            for (int i = 0; i < 5; i++) Cuadro(p);
            creaFalla = false;
            Check(creaFallos > 0 && (bool)Toma(p, "sondeoMemoriaPedido"), "una textura que el controlador no crea deja pedido el sondeo (" + creaFallos + " intentos fallidos)");
        }
        finally { p.Dispose(); texturas.Clear(); creaFalla = false; }
    }
    static string CorreVolcado(string rocola)
    {
        ProcessStartInfo psi = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName);
        psi.Arguments = "\"" + Environment.GetCommandLineArgs()[0] + "\" \"" + rocola + "\" --volcado";
        psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
        using (Process p = Process.Start(psi))
        {
            string o = p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit();
            StringBuilder sb = new StringBuilder();
            foreach (string l in o.Split('\n')) if (l.StartsWith("VOLCADO ")) sb.AppendLine(l);
            return sb.ToString();
        }
    }
    // Modo --tiempo: milisegundos por celda separada (misma vista, 36 celdas por ronda, mediana de 9 rondas) de ESTE ejecutable.
    static int Tiempo()
    {
        object vista = null; string pl; int sd; double[] r = new double[9];
        Hornea(AsmNuevo, ref vista, 170, 202, false, false, out pl, out sd).Dispose();
        for (int k = 0; k < r.Length; k++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 36; i++) Hornea(AsmNuevo, ref vista, 170, 202, false, false, out pl, out sd).Dispose();
            r[k] = sw.Elapsed.TotalMilliseconds / 36;
        }
        Console.WriteLine("MS " + MedianaA(r).ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture));
        return 0;
    }
    static double CorreTiempo(string rocola)
    {
        ProcessStartInfo psi = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName);
        psi.Arguments = "\"" + Environment.GetCommandLineArgs()[0] + "\" \"" + rocola + "\" --tiempo";
        psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
        using (Process p = Process.Start(psi))
        {
            string o = p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit();
            foreach (string l in o.Split('\n')) if (l.StartsWith("MS ")) return double.Parse(l.Substring(3), System.Globalization.CultureInfo.InvariantCulture);
        }
        return -1;
    }
    // Modo --volcado: una huella de cada celda separada que hornea ESTE ejecutable, para compararla con la de otro.
    static int Volcado()
    {
        System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
        foreach (Size sz in new Size[] { new Size(1024, 768), new Size(1280, 1024), new Size(1920, 1080), new Size(2560, 1440), new Size(3440, 1440), new Size(640, 480) })
            foreach (bool cola in new bool[] { false, true })
                foreach (bool lod in new bool[] { false, true })
                {
                    object vista = null; string plano; int sd;
                    using (Bitmap b = Hornea(AsmNuevo, ref vista, sz.Width / 6, (sz.Height - 160) / 3, cola, lod, out plano, out sd))
                    using (Bitmap b2 = Hornea(AsmNuevo, ref vista, sz.Width / 6, (sz.Height - 160) / 3, cola, lod, out plano, out sd))
                        Console.WriteLine("VOLCADO " + sz.Width + "x" + sz.Height + " cola=" + cola + " lod=" + lod + " " + BitConverter.ToString(md5.ComputeHash(Bytes(b))) + " " + BitConverter.ToString(md5.ComputeHash(Bytes(b2))) + " " + plano);
                }
        return 0;
    }
    // En Mono/Linux hace falta la tabla de la biblioteca de copia de memoria; se copia el ejecutable a una carpeta propia con su .config.
    static string carpetaCopia;
    static string Cargable(string ruta)
    {
        if (Type.GetType("Mono.Runtime") == null || Environment.OSVersion.Platform == PlatformID.Win32NT) return ruta;
        string dir = Path.Combine(Path.GetTempPath(), "rocola_tirones_" + Process.GetCurrentProcess().Id + "_" + (carpetaCopia == null ? "a" : "b"));
        if (carpetaCopia == null) carpetaCopia = dir;
        Directory.CreateDirectory(dir);
        string destino = Path.Combine(dir, Path.GetFileName(ruta));
        File.Copy(ruta, destino, true);
        string naudio = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(ruta)), "NAudio.dll");
        if (File.Exists(naudio)) File.Copy(naudio, Path.Combine(dir, "NAudio.dll"), true);
        File.WriteAllText(destino + ".config", "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<configuration>\n  <dllmap dll=\"kernel32.dll\">\n" +
                          "    <dllentry dll=\"libc.so.6\" name=\"RtlMoveMemory\" target=\"memmove\"/>\n  </dllmap>\n</configuration>\n");
        return destino;
    }
    static int Main(string[] argv)
    {
        try { return Prueba(argv); }
        catch (Exception e) { Console.WriteLine("  FALLO excepcion: " + e); return 1; }
        finally { try { foreach (string d in Directory.GetDirectories(Path.GetTempPath(), "rocola_tirones_" + Process.GetCurrentProcess().Id + "_*")) Directory.Delete(d, true); } catch { } }
    }
    static int Prueba(string[] argv)
    {
        if (argv.Length < 1) { Console.WriteLine("uso: tironescambio <rocola.exe> [<rocola_anterior.exe>]"); return 2; }
        AsmNuevo = Assembly.LoadFrom(Cargable(argv[0]));
                TF = AsmNuevo.GetType("Rocola.Form1"); TPanel = AsmNuevo.GetType("Rocola.GpuPanel");
        if (TPanel == null) { Console.WriteLine("  sin tarjeta: nada que medir\n  OK tironescambio (perfil sin GPU)"); return 0; }
        TStar = AsmNuevo.GetType("Rocola.GpuStarfield"); TDev = AsmNuevo.GetType("Rocola.D3D9Device"); TD3D = AsmNuevo.GetType("Rocola.D3D9");
        if (argv.Length > 3 && argv[1] == "--hijo") return Hijo(argv[2], argv[3]);
        if (argv.Length > 1 && argv[1] == "--volcado") return Volcado();
        if (argv.Length > 1 && argv[1] == "--tiempo") return Tiempo();
        if (argv.Length > 1 && argv[1] == "--volcado5") { TF = AsmNuevo.GetType("Rocola.Form1"); return Volcado5(true); }
        if (argv.Length > 1 && argv[1] == "--tiempo5") return Tiempo5();
        if (argv.Length > 1 && argv[1] == "--sondeos") return Sondeos();
        Libro();
        LibroEnElPanel();
        Precalentado(argv[0]);
        Bordes(argv[0], argv.Length > 1 ? argv[1] : null);
        Alfa(argv[0], argv.Length > 1 ? argv[1] : null);
        MemoriaDeTexturas(argv.Length > 1 ? argv[1] : null);
        Console.WriteLine(fallos == 0 ? "  OK tironescambio (" + oks + " comprobaciones)" : "  FALLO tironescambio: " + fallos);
        return fallos;
    }
}
