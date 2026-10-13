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

// PERFORMANCE (13-10-2026; Rocola/PerformanceGpu.cs y el medidor de Rendimiento.cs).
//
// Peticion: «Haz las 4 cosas, pon los nuevos settings graficos como un preset llamado performance en la ventana de imagen,
// y el benchmark renuevalo para que sea mas inteligente y mejor». Aqui no hay Direct3D: se usa una tarjeta IMITADA que
// apunta cada orden con su estado (destino, mezcla, las dos etapas de textura, el formato del vertice) y un raster de
// referencia con las reglas de Direct3D 9 que usa la rocola (centros de pixel, regla de arriba a la izquierda, bilineal
// con repeticion o pegado al borde, las operaciones de etapa que se usan y las mezclas). No mide velocidad: compara lo
// que se pinta y cuenta cuanto se pinta.
//
//   1) JUNTAR ORDENES: la misma secuencia de dibujos al azar, con y sin juntar, da la MISMA imagen, pixel a pixel, con
//      menos ordenes a la tarjeta.
//   2) EL CIELO A MEDIA RESOLUCION: el degradado y las nebulosas pintados en la imagen intermedia y estirados, contra los
//      de siempre. Casi identico (se dice cuanto) y un cuarto de pixeles en las capas de nebulosa.
//   3) GRANO Y VINETA EN UNA PASADA: las dos texturas a la vez contra las dos pasadas, con y sin huecos de portadas.
//   4) SIN ESPERAR AL MONITOR: se pide, reinicia el dispositivo soltando antes la imagen intermedia, y se devuelve.
//   5) EL CUADRO ENTERO con los dos ahorros: se presenta, el estado de dos texturas no se queda puesto para lo siguiente.
//   6) LA PROPUESTA DEL MEDIDOR (PlanDeAhorros) con numeros de mentira, y el medidor por dentro (lee su codigo).
//   7) EL PRESET Performance y los dos interruptores: se guardan, se releen y la pagina Imagen los ensena.
class Performance
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos, oks;
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
        Delegate del = Delegate.CreateDelegate(TD3D.GetNestedType(tipo, TODO), typeof(Performance).GetMethod(metodo, TODO));
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
        DynamicMethod dm = new DynamicMethod("BloqueaImitada", typeof(int), pt, typeof(Performance).Module, true);
        ILGenerator il = dm.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Conv_U);
        il.Emit(OpCodes.Call, typeof(Performance).GetMethod("Bloquea", TODO));
        il.Emit(OpCodes.Ret);
        Delegate lk = dm.CreateDelegate(tLock); vivos.Add(lk);
        // GetSurfaceLevel(self, level, out superficie): con el tipo de delegado de la rocola (GetPtrDel), igual que arriba.
        Type tGet = TD3D.GetNestedType("GetPtrDel", TODO);
        ps = tGet.GetMethod("Invoke").GetParameters();
        pt = new Type[ps.Length]; for (int i = 0; i < ps.Length; i++) pt[i] = ps[i].ParameterType;
        DynamicMethod dg = new DynamicMethod("SuperficieImitada", typeof(int), pt, typeof(Performance).Module, true);
        il = dg.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Conv_U);
        il.Emit(OpCodes.Call, typeof(Performance).GetMethod("Superficie", TODO));
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
        Delegate del = Delegate.CreateDelegate(f.FieldType, typeof(Performance).GetMethod(metodo, TODO));
        vivos.Add(del); f.SetValue(d, del);
    }
    // Un delegado con firma "out IntPtr" o "ref PresentParams" no se puede atar a un metodo de aqui sin conocer el tipo: se
    // arma al vuelo, como Bloquea, pasando la direccion.
    static IntPtr PunteroPorDireccion(string tipo, string metodo, int argDireccion)
    {
        Type t = TD3D.GetNestedType(tipo, TODO);
        ParameterInfo[] ps = t.GetMethod("Invoke").GetParameters();
        Type[] pt = new Type[ps.Length]; for (int i = 0; i < ps.Length; i++) pt[i] = ps[i].ParameterType;
        DynamicMethod dm = new DynamicMethod(metodo + "Imitado", typeof(int), pt, typeof(Performance).Module, true);
        ILGenerator il = dm.GetILGenerator();
        for (int i = 0; i < ps.Length; i++)
        {
            il.Emit(OpCodes.Ldarg, i);
            if (i == argDireccion) il.Emit(OpCodes.Conv_U);
        }
        il.Emit(OpCodes.Call, typeof(Performance).GetMethod(metodo, TODO));
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

    // ======================= EL RASTER DE REFERENCIA =======================
    static double Ef(double ax, double ay, double bx, double by, double px, double py) { return (bx - ax) * (py - ay) - (by - ay) * (px - ax); }
    static bool ArribaIzq(double ax, double ay, double bx, double by) { double dx = bx - ax, dy = by - ay; return dy < 0 || (dy == 0 && dx > 0); }
    static float Fx(byte[] d, int paso, int v, int campo) { return BitConverter.ToSingle(d, v * paso + campo); }
    static uint Col(byte[] d, int paso, int v) { return BitConverter.ToUInt32(d, v * paso + 16); }
    static double Ch(uint c, int s) { return ((c >> s) & 255) / 255.0; }
    static uint Q(double v) { int i = (int)Math.Floor(v * 255.0 + 0.5); return (uint)(i < 0 ? 0 : (i > 255 ? 255 : i)); }
    static void Muestra(Textura t, double u, double v, uint dir, double[] o)
    {
        double fu = u * t.W - 0.5, fv = v * t.H - 0.5;
        int xa = (int)Math.Floor(fu), ya = (int)Math.Floor(fv); double fx = fu - xa, fy = fv - ya;
        o[0] = o[1] = o[2] = o[3] = 0;
        for (int k = 0; k < 4; k++)
        {
            double wgt = ((k & 1) != 0 ? fx : 1 - fx) * ((k >> 1) != 0 ? fy : 1 - fy);
            if (wgt == 0) continue;
            int xx = xa + (k & 1), yy = ya + (k >> 1);
            if (dir == 3) { xx = Math.Max(0, Math.Min(t.W - 1, xx)); yy = Math.Max(0, Math.Min(t.H - 1, yy)); }
            else { xx = ((xx % t.W) + t.W) % t.W; yy = ((yy % t.H) + t.H) % t.H; }
            uint p = (uint)t.Px[yy * t.W + xx];
            o[3] += wgt * ((p >> 24) & 255) / 255.0; o[0] += wgt * ((p >> 16) & 255) / 255.0;
            o[1] += wgt * ((p >> 8) & 255) / 255.0; o[2] += wgt * (p & 255) / 255.0;
        }
    }
    static void Arg(uint a, double[] dif, double[] cur, double[] tex, bool alfa, double[] o)
    {
        double[] b = (a & 0xF) == 0 ? dif : (a & 0xF) == 1 ? cur : tex;
        for (int i = 0; i < 4; i++)
        {
            double v = ((a & 0x20) != 0 && !alfa) ? b[3] : b[i];
            if ((a & 0x10) != 0) v = 1 - v;
            o[i] = v;
        }
    }
    static double Op(uint op, double a, double b, double cur)
    {
        switch (op)
        {
            case 1: return cur;
            case 2: return a;
            case 3: return b;
            case 4: return a * b;
            case 11: return Math.Min(1, a + b - a * b);
        }
        throw new Exception("operacion de etapa desconocida: " + op);
    }
    // Pinta una orden en su destino (la pantalla 'fb' o la textura destino).
    static void Pinta(List<Envio> es, int[] fb, int W, int H)
    {
        double[] dif = new double[4], cur = new double[4], tx = new double[4], a1 = new double[4], a2 = new double[4], sal = new double[4];
        foreach (Envio e in es)
        {
            int[] dst = fb; int DW = W, DH = H;
            if (e.S.Blanco != fondo)
            {
                Textura rt; if (!superficies.TryGetValue(e.S.Blanco.ToInt64(), out rt)) throw new Exception("destino desconocido");
                dst = rt.Px; DW = rt.W; DH = rt.H;
            }
            Textura[] tt = new Textura[2];
            for (int s = 0; s < 2; s++)
            {
                bool usa = s == 0 ? (e.S.E[0].Op != 1 && (UsaTextura(e.S.E[0]))) : e.S.E[1].Op != 1;
                if (usa) { tt[s] = Lee(e.S.E[s].Tex); if (tt[s] == null) throw new Exception("textura desconocida en la etapa " + s); }
            }
            int paso = e.Paso;
            for (int t = 0; t < e.Tris; t++)
            {
                int i0 = t * 3, i1 = i0 + 1, i2 = i0 + 2;
                double x0 = Fx(e.Datos, paso, i0, 0), y0 = Fx(e.Datos, paso, i0, 4), x1 = Fx(e.Datos, paso, i1, 0), y1 = Fx(e.Datos, paso, i1, 4), x2 = Fx(e.Datos, paso, i2, 0), y2 = Fx(e.Datos, paso, i2, 4);
                double area = Ef(x0, y0, x1, y1, x2, y2);
                if (area == 0) continue;
                if (area < 0) { double qx = x1, qy = y1; x1 = x2; y1 = y2; x2 = qx; y2 = qy; int ti = i1; i1 = i2; i2 = ti; area = -area; }
                int minx = Math.Max(0, (int)Math.Ceiling(Math.Min(x0, Math.Min(x1, x2)))), maxx = Math.Min(DW - 1, (int)Math.Floor(Math.Max(x0, Math.Max(x1, x2))));
                int miny = Math.Max(0, (int)Math.Ceiling(Math.Min(y0, Math.Min(y1, y2)))), maxy = Math.Min(DH - 1, (int)Math.Floor(Math.Max(y0, Math.Max(y1, y2))));
                bool tl0 = ArribaIzq(x1, y1, x2, y2), tl1 = ArribaIzq(x2, y2, x0, y0), tl2 = ArribaIzq(x0, y0, x1, y1);
                uint c0 = Col(e.Datos, paso, i0), c1 = Col(e.Datos, paso, i1), c2 = Col(e.Datos, paso, i2);
                for (int py = miny; py <= maxy; py++)
                    for (int px = minx; px <= maxx; px++)
                    {
                        double w0 = Ef(x1, y1, x2, y2, px, py), w1 = Ef(x2, y2, x0, y0, px, py), w2 = Ef(x0, y0, x1, y1, px, py);
                        if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                        if ((w0 == 0 && !tl0) || (w1 == 0 && !tl1) || (w2 == 0 && !tl2)) continue;
                        w0 /= area; w1 /= area; w2 /= area;
                        dif[3] = Ch(c0, 24) * w0 + Ch(c1, 24) * w1 + Ch(c2, 24) * w2;
                        dif[0] = Ch(c0, 16) * w0 + Ch(c1, 16) * w1 + Ch(c2, 16) * w2;
                        dif[1] = Ch(c0, 8) * w0 + Ch(c1, 8) * w1 + Ch(c2, 8) * w2;
                        dif[2] = Ch(c0, 0) * w0 + Ch(c1, 0) * w1 + Ch(c2, 0) * w2;
                        for (int k = 0; k < 4; k++) cur[k] = dif[k];
                        for (int s = 0; s < 2; s++)
                        {
                            Etapa st = e.S.E[s];
                            if (st.Op == 1) break;
                            if (tt[s] != null)
                            {
                                int ci = s == 0 ? 0 : (int)st.Indice;
                                int ou = ci == 0 ? 20 : 28;
                                double u = Fx(e.Datos, paso, i0, ou) * w0 + Fx(e.Datos, paso, i1, ou) * w1 + Fx(e.Datos, paso, i2, ou) * w2;
                                double v = Fx(e.Datos, paso, i0, ou + 4) * w0 + Fx(e.Datos, paso, i1, ou + 4) * w1 + Fx(e.Datos, paso, i2, ou + 4) * w2;
                                Muestra(tt[s], u, v, st.Dir, tx);
                            }
                            Arg(st.A1, dif, cur, tx, false, a1); Arg(st.A2, dif, cur, tx, false, a2);
                            for (int k = 0; k < 3; k++) sal[k] = Op(st.Op, a1[k], a2[k], cur[k]);
                            Arg(st.AA1, dif, cur, tx, true, a1); Arg(st.AA2, dif, cur, tx, true, a2);
                            sal[3] = Op(st.AOp == 1 ? 1 : st.AOp, a1[3], a2[3], cur[3]);
                            for (int k = 0; k < 4; k++) cur[k] = sal[k];
                        }
                        int o = py * DW + px;
                        uint d = (uint)dst[o];
                        double dr = ((d >> 16) & 255) / 255.0, dg = ((d >> 8) & 255) / 255.0, db = (d & 255) / 255.0;
                        if (!e.S.Mezcla) { dr = cur[0]; dg = cur[1]; db = cur[2]; }
                        else
                        {
                            double sf = e.S.Origen == 2 ? 1 : cur[3], df = e.S.Destino == 2 ? 1 : 1 - cur[3];
                            dr = cur[0] * sf + dr * df; dg = cur[1] * sf + dg * df; db = cur[2] * sf + db * df;
                        }
                        dst[o] = unchecked((int)(0xFF000000u | (Q(dr) << 16) | (Q(dg) << 8) | Q(db)));
                    }
            }
        }
    }
    static bool UsaTextura(Etapa e) { return (e.A1 & 0xF) == 2 || (e.A2 & 0xF) == 2 || (e.AA1 & 0xF) == 2 || (e.AA2 & 0xF) == 2; }
    static int[] Fondo(int W, int H, int semilla)
    {
        Random r = new Random(semilla); int[] fb = new int[W * H];
        for (int i = 0; i < fb.Length; i++) fb[i] = unchecked((int)(0xFF000000u | (uint)r.Next(0x1000000)));
        return fb;
    }
    // Un fondo con algo de todo: un degradado, y puntos muy claros (como estrellas).
    static int[] FondoCielo(int W, int H, int semilla)
    {
        Random r = new Random(semilla); int[] fb = new int[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int b = 20 + 60 * y / H, g = 10 + 30 * y / H, rr = 8 + (x * 20) / W;
                fb[y * W + x] = unchecked((int)(0xFF000000u | ((uint)rr << 16) | ((uint)g << 8) | (uint)b));
            }
        for (int i = 0; i < W * H / 400; i++) fb[r.Next(fb.Length)] = unchecked((int)0xFFF0F4FFu);
        return fb;
    }
    static void Compara(int[] a, int[] b, out int distintos, out int masDeDos, out int maxd, out double media)
    {
        distintos = masDeDos = maxd = 0; double suma = 0;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] == b[i]) continue;
            distintos++;
            int peor = 0;
            for (int s = 0; s < 24; s += 8) { int dd = Math.Abs(((a[i] >> s) & 255) - ((b[i] >> s) & 255)); if (dd > peor) peor = dd; suma += dd; }
            if (peor > 2) masDeDos++;
            if (peor > maxd) maxd = peor;
        }
        media = suma / (3.0 * a.Length);
    }
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

    // Un arreglo de vertices de la rocola con quads al azar.
    static Array Quads(int n, Random r, int W, int H, uint alfa)
    {
        Array v = Array.CreateInstance(TVtx, n * 6);
        for (int q = 0; q < n; q++)
        {
            float x0 = r.Next(W - 40), y0 = r.Next(H - 40), x1 = x0 + 8 + r.Next(60), y1 = y0 + 8 + r.Next(60);
            float u0 = (float)r.NextDouble() * .5f, v0 = (float)r.NextDouble() * .5f, u1 = u0 + .3f, v1 = v0 + .3f;
            uint c = (alfa << 24) | (uint)r.Next(0x1000000);
            float[][] p = { new float[] { x0, y0, u0, v0 }, new float[] { x1, y0, u1, v0 }, new float[] { x1, y1, u1, v1 },
                            new float[] { x0, y0, u0, v0 }, new float[] { x1, y1, u1, v1 }, new float[] { x0, y1, u0, v1 } };
            for (int k = 0; k < 6; k++)
            {
                object x = Activator.CreateInstance(TVtx);
                TVtx.GetField("X").SetValue(x, p[k][0] - .5f); TVtx.GetField("Y").SetValue(x, p[k][1] - .5f);
                TVtx.GetField("Z").SetValue(x, 0f); TVtx.GetField("Rhw").SetValue(x, 1f); TVtx.GetField("Color").SetValue(x, c);
                TVtx.GetField("U").SetValue(x, p[k][2]); TVtx.GetField("V").SetValue(x, p[k][3]);
                v.SetValue(x, q * 6 + k);
            }
        }
        return v;
    }
    static IntPtr TexturaDe(object dev, int w, int h, int semilla)
    {
        IntPtr t; CreaTex(IntPtr.Zero, (uint)w, (uint)h, 1, 0, 21, 1, out t, IntPtr.Zero);
        Textura x = texturas[t.ToInt64()];
        Random r = new Random(semilla);
        for (int i = 0; i < x.Px.Length; i++) x.Px[i] = unchecked((int)(((uint)(40 + r.Next(216)) << 24) | (uint)r.Next(0x1000000)));
        Marshal.Copy(x.Px, 0, x.Mem, x.Px.Length); x.Sucia = false;
        return t;
    }

    // ======================= 1) JUNTAR ORDENES =======================
    static List<Envio> Secuencia(bool juntar, int semilla, out int pedidos, out int mandados)
    {
        const int W = 320, H = 240;
        object d = Tarjeta(W, H, juntar, false, false);
        IntPtr[] tx = { TexturaDe(d, 64, 64, 1), TexturaDe(d, 32, 32, 2), TexturaDe(d, 128, 64, 3) };
        Random r = new Random(semilla);
        envios.Clear();
        if (!(bool)Llama(d, "BeginFrame")) throw new Exception("no abre el cuadro");
        Llama(d, "EmpiezaCuenta", false);
        for (int i = 0; i < 160; i++)
        {
            int que = r.Next(10);
            int n = (r.Next(12) == 0) ? 40 : 1 + r.Next(4);   // de vez en cuando, uno grande (mas de 64 triangulos)
            Array v = Quads(n, r, W, H, (uint)(60 + r.Next(190)));
            IntPtr t = tx[r.Next(tx.Length)];
            if (que < 4) Llama(d, "DrawQuads", t, v, n * 2);
            else if (que < 6) Llama(d, "DrawTexQuad", t, Quads(1, r, W, H, 200));
            else if (que < 7) Llama(d, "DrawQuadsWrap", t, v, n * 2);
            else if (que < 8) Llama(d, "DrawMesh", v, n * 2);
            else if (que < 9) Llama(d, "DrawMeshAdd", v, n * 2);
            else Llama(d, "DrawQuads", t, v, n * 2);
        }
        Llama(d, "EndFrame");
        pedidos = (int)Toma(d, "Pedidos"); mandados = (int)Toma(d, "Envios");
        return Captura();
    }
    static void Juntar()
    {
        Console.WriteLine("\n  1) JUNTAR ORDENES DE DIBUJO (automatico, sin interruptor)\n");
        int peorDist = 0, menos = 0, total = 0, pedidosIguales = 0;
        for (int semilla = 1; semilla <= 4; semilla++)
        {
            int p0, m0, p1, m1;
            List<Envio> sin = Secuencia(false, semilla, out p0, out m0);
            List<Envio> con = Secuencia(true, semilla, out p1, out m1);
            int[] fa = Fondo(320, 240, 50 + semilla), fb = (int[])fa.Clone();
            Pinta(sin, fa, 320, 240); Pinta(con, fb, 320, 240);
            int dist, mas2, maxd; double media; Compara(fa, fb, out dist, out mas2, out maxd, out media);
            peorDist = Math.Max(peorDist, dist);
            if (m1 < m0 && con.Count == m1 && sin.Count == m0) menos++;
            if (p0 == p1 && p0 == m0) pedidosIguales++;
            total += m0 - m1;
        }
        Exige(peorDist == 0, "cuatro secuencias de 160 dibujos al azar (texturas, mezclas, modos, chicos y grandes): la MISMA imagen con y sin juntar (" + peorDist + " pixeles distintos)");
        Exige(menos == 4 && pedidosIguales == 4, "y con menos ordenes a la tarjeta: " + total + " menos en total; sin juntar, una orden por pedido");
        // Lo pendiente se manda antes de cambiar de estado: una orden juntada nunca mezcla dos texturas o mezclas.
        int p, m; List<Envio> c = Secuencia(true, 9, out p, out m);
        bool mezclados = false;
        foreach (Envio e in c) if (e.Tris > 2 * 4 * 2 && e.S.E[0].Tex == IntPtr.Zero && e.S.E[0].Op == 4) mezclados = true;
        Exige(!mezclados, "ninguna orden juntada queda con un estado que no le toca");
    }

    // ======================= 2) EL CIELO A MEDIA RESOLUCION =======================
    static void CieloMedio()
    {
        Console.WriteLine("\n  2) EL CIELO A MEDIA RESOLUCION (interruptor; apagado de fabrica)\n");
        foreach (int[] p in new int[][] { new int[] { 640, 480 }, new int[] { 801, 601 } })
        {
            int W = p[0], H = p[1];
            object dev = Tarjeta(W, H, true, false, false);
            object sf = Cielo(dev, W, H);
            Control panel = Panel(W, H, sf);
            Llama(panel, "EnsureNieblas");
            Pon(panel, "Nieblas", 2);
            Pon(panel, "NebColor", 0xFF8C78FFu); Pon(panel, "NebColor2", 0xFF40C0A0u);
            float midY = H * 0.42f; uint top = 0xFF02030Au, mid = 0xFF101848u, bot = 0xFF3A1E5Au;
            Array sky = Array.CreateInstance(TVtx, 12);
            Estatico(TStar, "SetQuad", sky, 0, 0f, 0f, (float)W, midY, top, top, mid, mid);
            Estatico(TStar, "SetQuad", sky, 6, 0f, midY, (float)W, (float)H, mid, mid, bot, bot);
            int peorMas2 = 0, peorMax = 0, malas = 0; double peorMedia = 0; long areaA = 0, areaB = 0, nebA = 0, nebB = 0;
            bool estructura = true, opaca = true;
            float[] fases = { 0f, 1.3f, 3.1f, 4.7f };
            foreach (float fase in fases)
            {
                Pon(panel, "NebFase", fase); Pon(panel, "HazFase", fase * .7f);
                envios.Clear();
                Pon(panel, "CieloMedia", false);
                Llama(dev, "BeginFrame"); Llama(dev, "DrawSky", sky); Llama(panel, "DibujaNieblas"); Llama(dev, "EndFrame");
                List<Envio> normal = Captura();
                Pon(panel, "CieloMedia", true);
                Llama(dev, "BeginFrame");
                bool hecho = (bool)Llama(panel, "CieloMedioBase", W, H, midY, top, mid, bot);
                Llama(dev, "EndFrame");
                List<Envio> media = Captura();
                if (!hecho) { estructura = false; continue; }
                // Todo menos la ultima orden va a la imagen intermedia; la ultima la estira, opaca, a la pantalla.
                for (int i = 0; i < media.Count - 1; i++) if (media[i].S.Blanco == fondo) estructura = false;
                Envio ultima = media[media.Count - 1];
                if (ultima.S.Blanco != fondo || ultima.S.Mezcla || ultima.Tris != 8) estructura = false;
                int[] fa = Fondo(W, H, 3), fb = Fondo(W, H, 4);   // fondos DISTINTOS: los dos tienen que taparlo todo
                Pinta(normal, fa, W, H); Pinta(media, fb, W, H);
                int dist, mas2, maxd; double med; Compara(fa, fb, out dist, out mas2, out maxd, out med);
                peorMas2 = Math.Max(peorMas2, mas2); peorMax = Math.Max(peorMax, maxd); peorMedia = Math.Max(peorMedia, med);
                if (mas2 > W * H / 1000 || maxd > 8) malas++;
                // Opaca: lo que se ve no depende de lo que habia antes en la pantalla.
                int[] fc = Fondo(W, H, 5); Pinta(media, fc, W, H);
                int d2, m2, x2; double e2; Compara(fb, fc, out d2, out m2, out x2, out e2);
                if (d2 != 0) opaca = false;
                areaA += Area(normal); areaB += Area(media);
                for (int i = 1; i < normal.Count; i++) nebA += Area(new List<Envio> { normal[i] });
                for (int i = 1; i < media.Count - 1; i++) nebB += Area(new List<Envio> { media[i] });
            }
            Exige(estructura, W + "x" + H + ": el cielo y las nebulosas van a la imagen intermedia y se estiran una vez, opacos, a la pantalla");
            Exige(opaca, W + "x" + H + ": tapa toda la pantalla, tambien la columna y la fila del borde (no sale basura de la imagen)");
            Exige(malas == 0 && peorMedia < 0.4, W + "x" + H + ", " + fases.Length + " posiciones de la deriva: casi identico -- pixeles que cambian mas de 2 niveles de 255: "
                  + Pct((double)peorMas2 / (W * H)) + " como mucho; el que mas, " + peorMax + " niveles; de media " + peorMedia.ToString("0.00") + " niveles");
            Exige(nebB * 3 < nebA, W + "x" + H + ": las nebulosas y la via lactea pintan " + Pct((double)nebB / Math.Max(1, nebA)) + " de los pixeles de antes; con el cielo y el estirado, "
                  + Pct((double)areaB / Math.Max(1, areaA)) + " del total de estas capas");
        }
        // Sin nebulosas no hay nada que ahorrar: el cielo de siempre.
        {
            object dev = Tarjeta(320, 240, true, false, false); Control panel = Panel(320, 240, Cielo(dev, 320, 240));
            Llama(panel, "EnsureNieblas"); Pon(panel, "Nieblas", 0); Pon(panel, "CieloMedia", true);
            Llama(dev, "BeginFrame");
            bool hecho = (bool)Llama(panel, "CieloMedioBase", 320, 240, 100f, 0xFF000000u, 0xFF000010u, 0xFF000020u);
            Llama(dev, "EndFrame"); Captura();
            Exige(!hecho, "con las nebulosas apagadas no se usa la imagen intermedia");
        }
    }

    // ======================= 3) GRANO Y VINETA EN UNA PASADA =======================
    static void GranoVineta()
    {
        Console.WriteLine("\n  3) GRANO Y VINETA EN UNA PASADA (interruptor; apagado de fabrica)\n");
        foreach (bool huecos in new bool[] { false, true })
        {
            const int W = 640, H = 480;
            object dev = Tarjeta(W, H, true, true, false);
            Control panel = Panel(W, H, Cielo(dev, W, H));
            Llama(panel, "EnsureNieblas"); Llama(panel, "EnsureVineta");
            if (huecos)
            {
                int[] h = { 40, 60, 200, 220, 300, 260, 460, 420 };
                int[] dst = (int[])Toma(panel, "huecos"); Array.Clear(dst, 0, dst.Length); Array.Copy(h, dst, h.Length); Pon(panel, "nHuecos", 2);
            }
            IntPtr vineta = (IntPtr)Toma(panel, "vinetaTex");
            envios.Clear();
            Llama(dev, "BeginFrame");
            Llama(panel, "DibujaRuido");
            Llama(panel, "DibujaTeselasFondo", vineta, Toma(panel, "vinetaTeselas"), 0f, 0f, (float)W, (float)H, Toma(panel, "vinetaU"), Toma(panel, "vinetaV"), 0xFFFFFFFFu, Toma(panel, "adornoQuad"));
            Llama(dev, "EndFrame");
            List<Envio> dos = Captura();
            Llama(dev, "BeginFrame");
            bool hecho = (bool)Llama(panel, "DibujaGranoYVineta");
            Llama(dev, "EndFrame");
            List<Envio> una = Captura();
            Exige(hecho && una.Count == 1 && una[0].Paso == 36 && una[0].S.E[1].Op == 4 && una[0].S.E[1].AOp == 11 && una[0].S.Origen == 2,
                  (huecos ? "con huecos de portadas" : "sin huecos") + ": una sola orden con dos texturas (vertice de 36 bytes, suma suave del alfa, origen UNO)");
            int[] fa = FondoCielo(W, H, 8), fb = (int[])fa.Clone();
            Pinta(dos, fa, W, H); Pinta(una, fb, W, H);
            int dist, mas2, maxd; double med; Compara(fa, fb, out dist, out mas2, out maxd, out med);
            Exige(maxd <= 2 && med < 0.2, (huecos ? "con huecos" : "sin huecos") + ": lo mismo que las dos pasadas salvo el redondeo: " + Pct((double)dist / (W * H))
                  + " de los pixeles cambian, como mucho " + maxd + " niveles de 255");
            long a2 = Area(dos), a1 = Area(una);
            Exige(a1 < a2, (huecos ? "con huecos" : "sin huecos") + ": se pinta " + Pct((double)a1 / Math.Max(1, a2)) + " de lo de antes (la vineta ya no tiene su pasada)");
            // Despues, todo como estaba: la segunda etapa apagada y el vertice de siempre.
            Llama(dev, "BeginFrame"); Llama(dev, "DrawQuads", TexturaDe(dev, 16, 16, 4), Quads(1, new Random(1), W, H, 255), 2); Llama(dev, "EndFrame");
            List<Envio> luego = Captura();
            Exige(luego.Count == 1 && luego[0].S.E[1].Op == 1 && luego[0].S.Fvf == 0x144 && luego[0].S.Origen == 5 && luego[0].S.E[0].A2 == 0,
                  "despues de la pasada doble, lo siguiente se dibuja con la segunda etapa apagada, el vertice y la mezcla de siempre");
        }
        // Sin la capacidad, no se intenta.
        {
            object dev = Tarjeta(320, 240, true, false, false); Control panel = Panel(320, 240, Cielo(dev, 320, 240));
            Llama(panel, "EnsureNieblas"); Llama(panel, "EnsureVineta");
            Pon(panel, "GranoVineta", true); Pon(panel, "VinetaSuave", true);
            Llama(panel, "DecideAhorros");
            Exige(!(bool)Toma(panel, "granoUnidoEsteCuadro"), "si la tarjeta no puede dos texturas a la vez, el grano y la vineta van por separado como siempre");
        }
    }

    // ======================= 4) SIN ESPERAR AL MONITOR =======================
    static void SinEspera()
    {
        Console.WriteLine("\n  4) PRESENTAR SIN ESPERAR AL MONITOR (solo el medidor)\n");
        object no = Tarjeta(320, 240, true, false, false);
        Exige(!(bool)Llama(no, "PideSinEspera", true) && !(bool)Prop(no, "SinEspera"), "si la tarjeta no lo permite, no se pide");
        object dev = Tarjeta(320, 240, true, false, true);
        Control panel = Panel(320, 240, Cielo(dev, 320, 240));
        Llama(panel, "EnsureNieblas"); Pon(panel, "Nieblas", 2); Pon(panel, "CieloMedia", true);
        Llama(dev, "BeginFrame"); Llama(panel, "CieloMedioBase", 320, 240, 100f, 0xFF000000u, 0xFF000010u, 0xFF000020u); Llama(dev, "EndFrame"); Captura();
        IntPtr rt = (IntPtr)Toma(dev, "mediaTex");
        Exige(rt != IntPtr.Zero && texturas[rt.ToInt64()].Destino, "la imagen intermedia existe y es un destino de la tarjeta");
        int r0 = reinicios, s0 = soltadasDestino;
        bool pedido = (bool)Llama(dev, "PideSinEspera", true);
        object pp = Toma(dev, "pp");
        Exige(pedido && (uint)pp.GetType().GetField("PresentationInterval").GetValue(pp) == 0x80000000u && (bool)Toma(dev, "resetPending"),
              "se pide: el intervalo pasa a inmediato y el proximo cuadro reinicia el dispositivo");
        Llama(dev, "BeginFrame"); Llama(dev, "EndFrame"); Captura();
        Exige(reinicios == r0 + 1 && soltadasDestino > s0 && (IntPtr)Toma(dev, "mediaTex") == IntPtr.Zero,
              "al reiniciar, la imagen intermedia (memoria de la tarjeta) se suelta ANTES, y se rehara sola");
        Llama(dev, "PideSinEspera", false);
        pp = Toma(dev, "pp");
        Llama(dev, "BeginFrame"); Llama(dev, "EndFrame");
        Exige((uint)pp.GetType().GetField("PresentationInterval").GetValue(pp) == 0u && reinicios == r0 + 2, "y se devuelve: otra vez esperando al monitor");
    }

    // ======================= 5) EL CUADRO ENTERO =======================
    static void CuadroEntero()
    {
        Console.WriteLine("\n  5) EL CUADRO ENTERO CON LOS DOS AHORROS\n");
        const int W = 640, H = 480;
        object dev = Tarjeta(W, H, true, true, false);
        Control p = Panel(W, H, Cielo(dev, W, H));
        Pon(p, "CieloMedia", true); Pon(p, "GranoVineta", true); Pon(p, "CuentaArea", true);
        bool ok = true; List<Envio> c = null;
        for (int i = 0; i < 3; i++) { envios.Clear(); ok &= (bool)Llama(p, "RenderFrame"); c = Captura(); }
        Exige(ok, "el cuadro se presenta con los dos ahorros encendidos");
        int alIntermedio = 0, dobles = 0; bool estadoLimpio = true; bool pasoDoble = false;
        foreach (Envio e in c)
        {
            if (e.S.Blanco != fondo) alIntermedio++;
            if (e.Paso == 36) { dobles++; pasoDoble = true; continue; }
            if (pasoDoble && (e.S.E[1].Op != 1 || e.S.Fvf != 0x144)) estadoLimpio = false;
        }
        Exige(alIntermedio > 0 && dobles == 1, "en el cuadro: " + alIntermedio + " ordenes a la imagen intermedia y una pasada doble (grano y vineta)");
        Exige(estadoLimpio, "nada de lo que viene despues se dibuja con la segunda etapa ni con el vertice doble");
        object pedidos = Prop(p, "PedidosPorCuadro"), mandados = Prop(p, "EnviosPorCuadro"), pantallas = Prop(p, "PantallasPorCuadro");
        Exige((double)pedidos > 0 && (double)mandados > 0 && (double)mandados <= (double)pedidos && (double)pantallas > 0.5,
              "el cuadro cuenta lo suyo: " + ((double)pedidos).ToString("0") + " ordenes pedidas, " + ((double)mandados).ToString("0") + " mandadas, "
              + ((double)pantallas).ToString("0.00") + " pantallas pintadas");
        // Y apagados: como siempre (el cielo opaco es lo primero, y va a la pantalla).
        Pon(p, "CieloMedia", false); Pon(p, "GranoVineta", false);
        envios.Clear(); Llama(p, "RenderFrame"); c = Captura();
        bool siempre = c.Count > 0 && c[0].S.Blanco == fondo && !c[0].S.Mezcla && c[0].Tris == 4;
        foreach (Envio e in c) if (e.S.Blanco != fondo || e.Paso != 28) siempre = false;
        Exige(siempre, "con los dos apagados (de fabrica), el cuadro de siempre: todo a la pantalla, nada doble");
    }

    // ======================= 6) LA PROPUESTA Y EL MEDIDOR =======================
    static int[] Plan(double msBase, double[] ah, double[] am, double[] ab, double alto, double bajo, out bool[] usa, out double fin, out int meta)
    {
        object[] a = { msBase, ah, am, ab, alto, bajo, null, null, null };
        int[] r = (int[])Estatico(TForm, "PlanDeAhorros", a);
        usa = (bool[])a[6]; fin = (double)a[7]; meta = (int)a[8];
        return r;
    }
    static bool Todos(int[] n, int v) { foreach (int x in n) if (x != v) return false; return true; }
    static void Medidor()
    {
        Console.WriteLine("\n  6) LA PROPUESTA DEL MEDIDOR Y EL MEDIDOR POR DENTRO\n");
        double alto = (double)Estatico(TForm, "ObjetivoMs", 60), bajo = (double)Estatico(TForm, "ObjetivoMs", 30);
        Exige(Math.Abs(alto - 1000.0 / 60 * 0.92) < 1e-9 && Math.Abs(bajo - 1000.0 / 30 * 0.92) < 1e-9, "las metas llevan un 8 % de margen: " + alto.ToString("0.00") + " y " + bajo.ToString("0.00") + " ms");
        double[] am = { 1.0, 3.0, 2.0, 0.05, 0.5, 0.5 }, ab = { 1.0, 1.5, 1.0, 0.05, 0.5, 0.5 };
        bool[] usa; double fin; int meta; int[] niv;
        niv = Plan(14.0, new double[] { 3, 2 }, am, ab, alto, bajo, out usa, out fin, out meta);
        Exige(meta == 2 && !usa[0] && !usa[1] && Todos(niv, 2), "si ya cabe en el refresco, no toca nada");
        niv = Plan(18.0, new double[] { 3.0, 1.0 }, am, ab, alto, bajo, out usa, out fin, out meta);
        Exige(meta == 2 && usa[0] && !usa[1] && Todos(niv, 2) && fin <= alto, "17 ms de mas: con el ahorro que mas ahorra basta; ni el otro ni un solo efecto");
        niv = Plan(19.5, new double[] { 2.0, 1.5 }, am, ab, alto, bajo, out usa, out fin, out meta);
        Exige(meta == 2 && usa[0] && usa[1] && niv[1] == 1 && niv[0] == 2 && fin <= alto, "si los ahorros no bastan, los dos y luego el escalon de efecto que mas ahorra");
        double[] amPoco = { 0.5, 1.0, 0.5, 0.05, 0.3, 0.3 }, abPoco = { 0.5, 0.5, 0.3, 0.05, 0.3, 0.3 };
        niv = Plan(28.0, new double[] { 2.0, 1.0 }, amPoco, abPoco, alto, bajo, out usa, out fin, out meta);
        Exige(meta == 1 && Todos(niv, 2) && !usa[0] && !usa[1], "si 60 no se alcanza ni bajandolo todo y ya da 30 estables, no se toca nada");
        niv = Plan(32.0, new double[] { 2.0, 1.0 }, am, ab, alto, bajo, out usa, out fin, out meta);
        Exige(meta == 1 && usa[0] && !usa[1] && Todos(niv, 2) && fin <= bajo, "si ni 30: primero lo que casi no se nota, lo justo (" + fin.ToString("0.0") + " ms)");
        niv = Plan(35.0, new double[] { 2.0, 1.0 }, amPoco, abPoco, alto, bajo, out usa, out fin, out meta);
        Exige(meta == 1 && usa[0] && usa[1] && !Todos(niv, 2) && fin <= bajo, "y si con eso no basta para 30, los escalones de efectos justos (" + fin.ToString("0.0") + " ms)");
        niv = Plan(60.0, new double[] { 2.0, 1.0 }, am, ab, alto, bajo, out usa, out fin, out meta);
        Exige(meta == 0 && usa[0] && usa[1] && niv[3] == 2 && fin > bajo, "y si tampoco, todo lo que ayuda, sin tocar lo que no ahorra nada, y lo dice (meta 0)");
        niv = Plan(18.0, new double[] { 0.1, 0.0 }, am, ab, alto, bajo, out usa, out fin, out meta);
        Exige(!usa[0] && !usa[1], "un ahorro que cabe en el temblor de la medida no cuenta");

        List<string> ll = Codigo.ConAyudantes(TForm, "MideRendimiento");
        int pide = 0; foreach (string s in ll) if (s.EndsWith("PideSinEspera")) pide++;
        Exige(pide >= 2, "el medidor suelta el monitor y lo devuelve (PideSinEspera dos veces)");
        Exige(Codigo.Llama(ll, "PlanDeAhorros") && Codigo.Llama(ll, "MideCoste") && Codigo.Llama(ll, "QueFrena") && Codigo.Llama(ll, "CalientaMedida"),
              "mide sin el monitor, dice que frena, arma la propuesta con los ahorros y la comprueba");
        bool medianas = false;
        foreach (MethodInfo me in TForm.GetMethods(TODO)) if (me.Name == "MideEscena" && Codigo.Llama(me, "Medianas")) medianas = true;
        Exige(medianas,
              "cada pantalla se resume con medianas (lo tipico), no con la media que arrastra un cuadro raro");
        Exige(!Codigo.Llama(ll, "SaveConfigPrefs"), "medir sigue sin guardar nada");
        // QueFrena con numeros de mentira.
        Type esc = TForm.GetNestedType("EscenaMedida", TODO);
        object m = Activator.CreateInstance(esc, true);
        Pon(m, "SinEspera", true); Pon(m, "MsCuadro", 20.0); Pon(m, "MsEspera", 12.0); Pon(m, "MsProcesador", 6.0); Pon(m, "MsFuera", 2.0);
        string q1 = (string)Estatico(TForm, "QueFrena", m);
        Pon(m, "MsEspera", 1.0); Pon(m, "MsProcesador", 17.0);
        string q2 = (string)Estatico(TForm, "QueFrena", m);
        Pon(m, "MsProcesador", 6.0); Pon(m, "MsFuera", 12.0);
        string q3 = (string)Estatico(TForm, "QueFrena", m);
        Exige(q1.Contains("tarjeta") && q2.Contains("procesador") && q3.Contains("música"), "dice que frena: \"" + q1 + "\" / \"" + q2 + "\" / \"" + q3 + "\"");
    }

    // ======================= 7) EL PRESET Y LOS INTERRUPTORES =======================
    static Control Busca(Control p, string texto)
    {
        foreach (Control c in p.Controls) { if (c.Text == texto || c.AccessibleName == texto) return c; Control r = Busca(c, texto); if (r != null) return r; }
        return null;
    }
    static void Preset()
    {
        Console.WriteLine("\n  7) EL PRESET Performance Y SUS INTERRUPTORES\n");
        string raiz = Path.Combine(Path.GetTempPath(), "performance_" + System.Diagnostics.Process.GetCurrentProcess().Id);
        Directory.CreateDirectory(raiz);
        try
        {
            object f = Codigo.FormSinEncender(TForm);
            Pon(f, "carpetaDatos", raiz);
            Exige(!(bool)Toma(f, "cieloMedia") && !(bool)Toma(f, "granoVineta"), "de fabrica, los dos apagados: la pantalla de siempre");
            Llama(f, "AplicaPresetGrafico", 3);
            bool alto = true; foreach (string k in new string[] { "nieblas", "estrellas", "espectro", "patron", "fugaces", "animacion" }) if ((int)Llama(f, "LeeNivel", k) != 2) alto = false;
            Exige((bool)Toma(f, "cieloMedia") && (bool)Toma(f, "granoVineta") && alto, "Performance: los dos ahorros encendidos y los efectos en Alto");
            Llama(f, "AplicaPresetGrafico", 2);
            Exige(!(bool)Toma(f, "cieloMedia") && !(bool)Toma(f, "granoVineta"), "Alto: la pantalla de siempre, sin ahorros");
            Llama(f, "AplicaPresetGrafico", 1);
            Exige((bool)Toma(f, "cieloMedia") && (int)Llama(f, "LeeNivel", "nieblas") == 1, "Medio: efectos a medio y los ahorros puestos (son para ganar rendimiento)");
            // Se guardan y se releen.
            Pon(f, "cieloMedia", true); Pon(f, "granoVineta", false);
            Llama(f, "SaveConfigPrefs"); Codigo.EsperaGuardado(TForm, f);
            string cfg = File.ReadAllText(Path.Combine(raiz, "rocola_config.txt"));
            object g = Codigo.FormSinEncender(TForm); Pon(g, "carpetaDatos", raiz); Llama(g, "LoadConfigPrefs");
            Exige(cfg.Contains("cielo_media=1") && cfg.Contains("grano_vineta=0") && (bool)Toma(g, "cieloMedia") && !(bool)Toma(g, "granoVineta"),
                  "cada interruptor se guarda con su clave (cielo_media=, grano_vineta=) y se relee");
            // La pagina Imagen: el boton de golpe y la hoja con sus dos interruptores.
            using (Font n = new Font("Segoe UI", 10f, FontStyle.Bold)) using (Font s = new Font("Segoe UI", 8.5f))
            using (Panel pg = (Panel)Llama(g, "PaginaImagen", n, n, s, n, Color.White, Color.Gray, Color.DimGray))
            {
                Control boton = Busca(pg, "Todo de golpe: Performance"), hoja = Busca(pg, "Imagen: Performance");
                Control s0 = Busca(pg, "Performance: Cielo a media resolución"), s1 = Busca(pg, "Performance: Grano y viñeta en una pasada");
                Exige(boton != null && hoja != null && s0 != null && s1 != null, "Imagen tiene el boton Performance (con Bajo, Medio y Alto), la hoja Performance y sus dos interruptores");
                if (s0 != null && s1 != null)
                {
                    Exige((bool)s0.GetType().GetProperty("Checked").GetValue(s0, null) && !(bool)s1.GetType().GetProperty("Checked").GetValue(s1, null),
                          "los interruptores ensenan lo guardado");
                    s1.GetType().GetProperty("Checked").SetValue(s1, true, null);
                    Exige((bool)Toma(g, "granoVineta") && (bool)Toma(g, "f3CambioGraf"), "tocar un interruptor lo cambia y se guardara al cerrar");
                }
                if (boton != null)
                {
                    Llama(g, "AplicaPresetGrafico", 2);
                    MethodInfo click = typeof(Control).GetMethod("OnClick", TODO);
                    click.Invoke(boton, new object[] { EventArgs.Empty });
                    Exige((bool)Toma(g, "cieloMedia") && (bool)Toma(g, "granoVineta") && (bool)s0.GetType().GetProperty("Checked").GetValue(s0, null),
                          "pulsar Performance enciende los dos, y su hoja lo ensena");
                }
                Control fab = Busca(pg, "Graficos como de fabrica");
                if (fab != null)
                {
                    typeof(Control).GetMethod("OnClick", TODO).Invoke(fab, new object[] { EventArgs.Empty });
                    Exige(!(bool)Toma(g, "cieloMedia") && !(bool)Toma(g, "granoVineta"), "Graficos como de fabrica los apaga");
                }
            }
        }
        finally { try { Directory.Delete(raiz, true); } catch { } }
    }

    static string Pct(double f) { return (f * 100).ToString("0.0") + " %"; }

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
    static int Main(string[] argv)
    {
        try { return Prueba(argv); }
        finally { if (carpetaCopia != null) try { Directory.Delete(carpetaCopia, true); } catch { } }
    }
    static int Prueba(string[] argv)
    {
        Assembly a = Assembly.LoadFrom(Cargable((argv.Length > 0) ? argv[0] : "rocola.exe"));
        TForm = a.GetType("Rocola.Form1");
        TPanel = a.GetType("Rocola.GpuPanel");
        // El preset y los interruptores existen tambien sin tarjeta (se guardan igual); lo demas es solo de la tarjeta.
        try { Preset(); } catch (Exception e) { Exige(false, "el preset: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        if (TPanel == null)
        {
            Exige(Metodo(TForm, "PlanDeAhorros") != null, "sin tarjeta grafica: el medidor y los ahorros no dibujan nada (el dibujo por procesador no se toca)");
            Console.WriteLine(fallos == 0 ? "\n  PASS performance (" + oks + " comprobaciones; perfil sin tarjeta)" : "\n  " + fallos + " fallos");
            return fallos;
        }
        TStar = a.GetType("Rocola.GpuStarfield"); TDev = a.GetType("Rocola.D3D9Device");
        TD3D = a.GetType("Rocola.D3D9"); TVtx = TD3D.GetNestedType("Vtx", TODO); TVtx2 = TD3D.GetNestedType("Vtx2", TODO);
        Exige(TVtx2 != null && Marshal.SizeOf(TVtx2) == 36 && (int)Marshal.OffsetOf(TVtx2, "U2") == 28, "el vertice doble mide lo que espera el raster (36 bytes, segunda coordenada en el 28)");
        try { Juntar(); } catch (Exception e) { Exige(false, "juntar: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { CieloMedio(); } catch (Exception e) { Exige(false, "cielo a media: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { GranoVineta(); } catch (Exception e) { Exige(false, "grano y vineta: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { SinEspera(); } catch (Exception e) { Exige(false, "sin espera: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { CuadroEntero(); } catch (Exception e) { Exige(false, "el cuadro entero: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { Medidor(); } catch (Exception e) { Exige(false, "el medidor: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        Console.WriteLine(fallos == 0 ? "\n  PASS performance (" + oks + " comprobaciones; tarjeta imitada, no mide velocidad ni es la del bar)" : "\n  " + fallos + " fallos");
        return fallos;
    }
}
