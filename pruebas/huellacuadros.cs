using System;
using System.Collections;
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

// HUELLAS E INVENTARIO DEL CUADRO DE LA TARJETA (19-10-2026).
//
// Una tarjeta imitada que, ademas de contar, PINTA: rasteriza en software lo que la rocola le manda (triangulos con color de
// vertice, una o dos texturas, mezcla normal o sumada, imagen intermedia) y saca de cada cuadro tres huellas:
//   - los PIXELES de la pantalla (lo que se veria, segun esta imitacion);
//   - el chorro BRUTO de ordenes (estados y vertices tal cual salen del programa);
//   - el chorro CANONICO (lo mismo, sin los triangulos que no pintan nada: sin area o con alfa cero en una mezcla que no cambia
//     la pantalla, y con las ordenes seguidas del mismo estado juntas).
// Un ahorro "automatico" (misma imagen) tiene que dejar los pixeles IGUALES, y el chorro canonico igual o, si cambia, que sea
// solo por triangulos que no pintaban. Esto es una imitacion hecha aqui: no es una tarjeta de verdad ni el GDI+ de Windows XP.
//
// Uso:
//   mono huellacuadros.exe <rocola_instrumentada.exe> huella  [filtro]   -> una linea por escenario (huellas); necesita el reloj de pruebas
//   mono huellacuadros.exe <rocola.exe>               coste   [filtro]   -> milisegundos de procesador, memoria y recolecciones por cuadro
//   mono huellacuadros.exe <rocola.exe>               detalle [filtro]   -> orden por orden de un cuadro: area, pixeles que cambian, llamadas
//   mono huellacuadros.exe <rocola.exe>               lista
// El ejecutable instrumentado sale de pruebas/huella-compila.sh (cambia los relojes de la COPIA de las fuentes; la rocola de verdad no).
class HuellaCuadros
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TPanel, TStar, TDev, TD3D, TReloj;
    static readonly List<object> vivos = new List<object>();

    // ======================= REFLEXION =======================
    static FieldInfo Campo(Type t, string n)
    {
        for (; t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, TODO | BindingFlags.DeclaredOnly); if (f != null) return f; }
        return null;
    }
    static void Pon(object o, string n, object v) { FieldInfo f = Campo(o.GetType(), n); if (f == null) throw new Exception("falta el campo " + n); f.SetValue(o, v); }
    static void PonSiHay(object o, string n, object v) { FieldInfo f = Campo(o.GetType(), n); if (f != null) f.SetValue(o, v); }
    static object Toma(object o, string n) { FieldInfo f = Campo(o.GetType(), n); if (f == null) throw new Exception("falta el campo " + n); return f.GetValue(o); }
    static int Cuenta(object o, string n) { FieldInfo f = Campo(o.GetType(), n); return f == null ? 0 : (int)f.GetValue(o); }
    static MethodInfo Metodo(Type t, string n)
    {
        for (; t != null; t = t.BaseType) { MethodInfo m = t.GetMethod(n, TODO | BindingFlags.DeclaredOnly); if (m != null) return m; }
        return null;
    }
    static object Llama(object o, string n, params object[] a)
    {
        MethodInfo m = Metodo(o.GetType(), n);
        if (m == null) throw new Exception("falta " + o.GetType().Name + "." + n);
        try { return m.Invoke(o, a); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }

    // ======================= EL RELOJ (solo con el ejecutable instrumentado) =======================
    static void PonReloj(double ms)
    {
        if (TReloj != null) TReloj.GetMethod("Pon", TODO).Invoke(null, new object[] { ms });
    }

    // ======================= LA TARJETA IMITADA =======================
    sealed class Tx
    {
        public int W, H; public IntPtr Mem; public int[] Px; public bool Rt, Sucia = true, X8; public IntPtr Sup; public long Id; public bool Suelta;
    }
    static readonly Dictionary<long, Tx> texturas = new Dictionary<long, Tx>();
    static readonly Dictionary<long, Tx> superficies = new Dictionary<long, Tx>();
    static IntPtr vtTex, vtSup, fondo;
    static Tx pantalla;
    static int offPitch, offBits;
    static long idTx;
    static uint Nada(IntPtr self) { return 0; }
    static uint Memoria(IntPtr self) { return 256u * 1024 * 1024; }
    static IntPtr Puntero(string tipo, string metodo)
    {
        Delegate del = Delegate.CreateDelegate(TD3D.GetNestedType(tipo, TODO), typeof(HuellaCuadros).GetMethod(metodo, TODO));
        vivos.Add(del); return Marshal.GetFunctionPointerForDelegate(del);
    }
    static IntPtr PunteroPorDireccion(string tipo, string metodo, int argDireccion)
    {
        Type t = TD3D.GetNestedType(tipo, TODO);
        ParameterInfo[] ps = t.GetMethod("Invoke").GetParameters();
        Type[] pt = new Type[ps.Length]; for (int i = 0; i < ps.Length; i++) pt[i] = ps[i].ParameterType;
        DynamicMethod dm = new DynamicMethod(metodo + "Imitado", typeof(int), pt, typeof(HuellaCuadros).Module, true);
        ILGenerator il = dm.GetILGenerator();
        for (int i = 0; i < ps.Length; i++) { il.Emit(OpCodes.Ldarg, i); if (i == argDireccion) il.Emit(OpCodes.Conv_U); }
        il.Emit(OpCodes.Call, typeof(HuellaCuadros).GetMethod(metodo, TODO));
        il.Emit(OpCodes.Ret);
        Delegate del = dm.CreateDelegate(t); vivos.Add(del);
        return Marshal.GetFunctionPointerForDelegate(del);
    }
    // Cuenta de lo que le llega al "controlador": llamadas crudas (lo que paga el procesador en Direct3D 9).
    static long cSetRS, cSetTSS, cSetSamp, cSetTex, cSetFvf, cDraw, cBloqueos, cBytesBloqueo, cCreaTex, cSetRt, cPresent, cTrisEnviados, cBytesVertices, cSueltas;
    static void PonCeroCuentas() { cSetRS = cSetTSS = cSetSamp = cSetTex = cSetFvf = cDraw = cBloqueos = cBytesBloqueo = cCreaTex = cSetRt = cPresent = cTrisEnviados = cBytesVertices = cSueltas = 0; }

    static int CreaTex(IntPtr self, uint w, uint h, uint levels, uint usage, uint format, uint pool, out IntPtr tex, IntPtr shared)
    {
        PreparaTexturas();
        cCreaTex++;
        IntPtr o = Marshal.AllocHGlobal(16); Marshal.WriteIntPtr(o, vtTex);
        Tx t = new Tx(); t.W = (int)w; t.H = (int)h; t.Rt = (usage & 1) != 0; t.X8 = format == 22; t.Id = ++idTx;
        t.Px = new int[w * h];
        Random r = new Random((int)(w * 31 + h));   // basura repetible: lo que nadie escribe se nota en las huellas
        for (int i = 0; i < t.Px.Length; i++) t.Px[i] = r.Next() ^ (r.Next() << 16);
        t.Mem = Marshal.AllocHGlobal((int)(w * h * 4));
        Marshal.Copy(t.Px, 0, t.Mem, t.Px.Length);
        texturas[o.ToInt64()] = t; tex = o;
        return 0;
    }
    static int Bloquea(IntPtr self, IntPtr lr, IntPtr rect)
    {
        Tx t = texturas[self.ToInt64()];
        cBloqueos++;
        int x0 = 0, y0 = 0, x1 = t.W, y1 = t.H;
        if (rect != IntPtr.Zero) { x0 = Marshal.ReadInt32(rect, 0); y0 = Marshal.ReadInt32(rect, 4); x1 = Marshal.ReadInt32(rect, 8); y1 = Marshal.ReadInt32(rect, 12); }
        cBytesBloqueo += (long)Math.Max(0, x1 - x0) * Math.Max(0, y1 - y0) * 4;
        Marshal.WriteInt32(lr, offPitch, t.W * 4);
        Marshal.WriteIntPtr(lr, offBits, new IntPtr(t.Mem.ToInt64() + (long)y0 * t.W * 4 + x0 * 4));
        t.Sucia = true; return 0;
    }
    static int Desbloquea(IntPtr self, uint level) { return 0; }
    static uint Suelta(IntPtr self)
    {
        Tx t;
        if (texturas.TryGetValue(self.ToInt64(), out t)) { t.Suelta = true; cSueltas++; }
        return 0;
    }
    static int Superficie(IntPtr self, IntPtr salida)
    {
        Tx t = texturas[self.ToInt64()];
        if (t.Sup == IntPtr.Zero) { t.Sup = Marshal.AllocHGlobal(16); Marshal.WriteIntPtr(t.Sup, vtSup); superficies[t.Sup.ToInt64()] = t; }
        Marshal.WriteIntPtr(salida, t.Sup);
        return 0;
    }
    static void PreparaTexturas()
    {
        if (vtTex != IntPtr.Zero) return;
        Type tlr = TD3D.GetNestedType("LockedRect", TODO);
        offPitch = (int)Marshal.OffsetOf(tlr, "Pitch"); offBits = (int)Marshal.OffsetOf(tlr, "Bits");
        // El Lock de la rocola tiene dos formas (con "IntPtr rect" y con "ref RectI"): las dos entran por la misma direccion.
        Type tLock = TD3D.GetNestedType("LockRectDel", TODO);
        ParameterInfo[] ps = tLock.GetMethod("Invoke").GetParameters();
        Type[] pt = new Type[ps.Length]; for (int i = 0; i < ps.Length; i++) pt[i] = ps[i].ParameterType;
        DynamicMethod dm = new DynamicMethod("BloqueaImitada", typeof(int), pt, typeof(HuellaCuadros).Module, true);
        ILGenerator il = dm.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Conv_U); il.Emit(OpCodes.Ldarg_3);
        il.Emit(OpCodes.Call, typeof(HuellaCuadros).GetMethod("Bloquea", TODO));
        il.Emit(OpCodes.Ret);
        Delegate lk = dm.CreateDelegate(tLock); vivos.Add(lk);
        Type tReg = TD3D.GetNestedType("LockRegionDel", TODO);
        ps = tReg.GetMethod("Invoke").GetParameters();
        pt = new Type[ps.Length]; for (int i = 0; i < ps.Length; i++) pt[i] = ps[i].ParameterType;
        DynamicMethod dr = new DynamicMethod("BloqueaRegionImitada", typeof(int), pt, typeof(HuellaCuadros).Module, true);
        il = dr.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Conv_U); il.Emit(OpCodes.Ldarg_3); il.Emit(OpCodes.Conv_U);
        il.Emit(OpCodes.Call, typeof(HuellaCuadros).GetMethod("Bloquea", TODO));
        il.Emit(OpCodes.Ret);
        Delegate lr2 = dr.CreateDelegate(tReg); vivos.Add(lr2);
        // La rocola usa LockRectDel y LockRegionDel sobre la MISMA ranura; el delegado nativo es el mismo (cuatro punteros).
        Type tGet = TD3D.GetNestedType("GetPtrDel", TODO);
        ps = tGet.GetMethod("Invoke").GetParameters();
        pt = new Type[ps.Length]; for (int i = 0; i < ps.Length; i++) pt[i] = ps[i].ParameterType;
        DynamicMethod dg = new DynamicMethod("SuperficieImitada", typeof(int), pt, typeof(HuellaCuadros).Module, true);
        il = dg.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Conv_U);
        il.Emit(OpCodes.Call, typeof(HuellaCuadros).GetMethod("Superficie", TODO));
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

    // ---- el estado de la tarjeta imitada ----
    sealed class Etapa
    {
        public uint ColorOp = 4, ColorA1 = 2, ColorA2 = 1, AlphaOp = 2, AlphaA1 = 2, AlphaA2 = 1, Indice;
        public uint DirU = 1, DirV = 1, Mag = 1, Min = 1;
        public IntPtr Tex;
        public void Pon(Etapa o) { ColorOp = o.ColorOp; ColorA1 = o.ColorA1; ColorA2 = o.ColorA2; AlphaOp = o.AlphaOp; AlphaA1 = o.AlphaA1; AlphaA2 = o.AlphaA2; Indice = o.Indice; DirU = o.DirU; DirV = o.DirV; Mag = o.Mag; Min = o.Min; Tex = o.Tex; }
    }
    static Etapa[] E = { new Etapa(), new Etapa { ColorOp = 1, AlphaOp = 1, Indice = 1 } };
    static bool mezcla; static uint origen = 2, destino = 1, fvf;
    static IntPtr blanco;   // la superficie de destino puesta ahora (fondo = la pantalla)
    static int Hr(IntPtr self) { return 0; }
    static int SetRS(IntPtr self, uint a, uint b)
    {
        cSetRS++;
        if (a == 27) mezcla = b != 0; else if (a == 19) origen = b; else if (a == 20) destino = b;
        return 0;
    }
    static int SetFvf(IntPtr self, uint a) { cSetFvf++; fvf = a; return 0; }
    static int SetTSS(IntPtr self, uint etapa, uint tipo, uint v)
    {
        cSetTSS++;
        if (etapa > 1) return 0;
        Etapa e = E[etapa];
        switch (tipo) { case 1: e.ColorOp = v; break; case 2: e.ColorA1 = v; break; case 3: e.ColorA2 = v; break; case 4: e.AlphaOp = v; break; case 5: e.AlphaA1 = v; break; case 6: e.AlphaA2 = v; break; case 11: e.Indice = v; break; }
        return 0;
    }
    static int SetSamp(IntPtr self, uint etapa, uint tipo, uint v)
    {
        cSetSamp++;
        if (etapa > 1) return 0;
        Etapa e = E[etapa];
        switch (tipo) { case 1: e.DirU = v; break; case 2: e.DirV = v; break; case 5: e.Mag = v; break; case 6: e.Min = v; break; }
        return 0;
    }
    static int SetTex(IntPtr self, uint etapa, IntPtr t) { cSetTex++; if (etapa <= 1) E[etapa].Tex = t; return 0; }
    static int SetRt(IntPtr self, uint i, IntPtr sup) { cSetRt++; if (i == 0) blanco = sup; return 0; }
    static int GetRt(IntPtr self, uint i, IntPtr salida) { Marshal.WriteIntPtr(salida, blanco); return 0; }
    static int Valida(IntPtr self, IntPtr pasadas) { Marshal.WriteInt32(pasadas, 1); return 0; }
    static int Reset(IntPtr self, IntPtr pp) { return 0; }
    static int Present(IntPtr self, IntPtr a, IntPtr b, IntPtr c, IntPtr d) { cPresent++; AlPresentar(); return 0; }
    static int Begin(IntPtr self)
    {
        // Sin borrar (como la rocola): para que se note lo que nadie pinta, la pantalla empieza con una trama fea y repetible.
        if (modo == "huella" && pantalla != null)
        {
            int[] px = pantalla.Px;
            for (int i = 0; i < px.Length; i++) px[i] = unchecked((int)0xFFFF00FFu ^ (i * 2654435761u > 0 ? 0x00101010 : 0));
        }
        return 0;
    }

    // ---- las ordenes de dibujo ----
    sealed class Orden
    {
        public int Tris, Paso; public string Estado, Sitio, Destino; public long Area, Cambian; public long SinArea, Transparentes;
        public ulong HashEstado;
    }
    static readonly List<Orden> ordenes = new List<Orden>();
    static bool anota, hashea, pinta, conSitios;
    static string modo = "";
    static ulong hBruto, hCanon, hPixeles;
    static long trisTotal, trisSinArea, trisTransparentes;
    static long sobreEscritos, escritosTotal;   // pixeles escritos, y de ellos los que una orden opaca posterior tapo del todo (sobreescritura)
    static int[] contador;     // cuantas escrituras desde la ultima opaca, por pixel de la pantalla
    static long[] ordenDe;     // y la orden (indice) que escribio ultimo, para atribuir lo tapado

    [StructLayout(LayoutKind.Explicit)] struct Bits { [FieldOffset(0)] public int I; [FieldOffset(0)] public float F; [FieldOffset(0)] public uint U; }
    static float Fl(int bits) { Bits b = new Bits(); b.I = bits; return b.F; }
    static ulong Fnv(ulong h, ulong v) { h ^= v; h *= 1099511628211UL; return h; }
    const ulong FnvIni = 14695981039346656037UL;

    static Tx DestinoActual()
    {
        Tx t;
        if (blanco == fondo || blanco == IntPtr.Zero) return pantalla;
        return superficies.TryGetValue(blanco.ToInt64(), out t) ? t : pantalla;
    }
    static int[] buf = new int[9 * 3 * 4096];
    static int DrawUp(IntPtr self, uint prim, uint count, IntPtr data, uint stride)
    {
        cDraw++; cTrisEnviados += count; cBytesVertices += (long)count * 3 * stride;
        if (!anota && !hashea && !pinta) return 0;
        int n = (int)count * 3; int palabras = (int)stride / 4;
        if (buf.Length < n * palabras) buf = new int[n * palabras];
        Marshal.Copy(data, buf, 0, n * palabras);
        Orden o = new Orden(); o.Tris = (int)count; o.Paso = (int)stride;
        Tx dst = DestinoActual();
        o.Destino = dst == pantalla ? "pantalla" : "imagen " + dst.W + "x" + dst.H;
        o.Estado = EstadoTexto();
        if (conSitios) o.Sitio = Sitio();
        // chorro bruto
        if (hashea)
        {
            ulong he = HashEstadoDe(dst);
            hBruto = Fnv(hBruto, he);
            for (int i = 0; i < n * palabras; i++) hBruto = Fnv(hBruto, (uint)buf[i]);
            o.HashEstado = he;
        }
        // chorro canonico: sin los triangulos que no pintan nada (sin area, o con alfa cero en los tres vertices en una mezcla que no
        // cambia la pantalla), y con las ordenes seguidas del mismo estado juntas (el estado solo entra al cambiar)
        bool alfaMultiplicaDifuso = AlfaVaConElDifuso();
        bool mezclaSinCambio = mezcla && origen == 5 && (destino == 6 || destino == 2);
        if (hashea && o.HashEstado != ultimoEstadoCanon) { hCanon = Fnv(hCanon, o.HashEstado); ultimoEstadoCanon = o.HashEstado; }
        for (int t = 0; t < (int)count; t++)
        {
            int b = t * 3 * palabras;
            double ar = (double)(Fl(buf[b + palabras]) - Fl(buf[b])) * (Fl(buf[b + palabras * 2 + 1]) - Fl(buf[b + 1]))
                      - (double)(Fl(buf[b + palabras * 2]) - Fl(buf[b])) * (Fl(buf[b + palabras + 1]) - Fl(buf[b + 1]));
            bool sinArea = !(Math.Abs(ar) > 0);
            bool transp = !sinArea && mezclaSinCambio && alfaMultiplicaDifuso && (buf[b + 4] >> 24) == 0 && (buf[b + palabras + 4] >> 24) == 0 && (buf[b + palabras * 2 + 4] >> 24) == 0;
            trisTotal++;
            if (sinArea) { trisSinArea++; o.SinArea++; continue; }
            if (transp) { trisTransparentes++; o.Transparentes++; continue; }
            if (hashea) for (int k = 0; k < 3 * palabras; k++) hCanon = Fnv(hCanon, (uint)buf[b + k]);
        }
        if (pinta) Rasteriza((int)count, palabras, dst, o);
        if (anota) ordenes.Add(o);
        return 0;
    }
    static ulong ultimoEstadoCanon;

    // El alfa de salida es (alfa de vertice) x algo: asi un alfa de vertice cero vuelve nulo el origen en la mezcla.
    static bool AlfaVaConElDifuso()
    {
        Etapa e = E[0];
        if (E[1].AlphaOp != 1 || E[1].ColorOp != 1) return false;   // con segunda etapa no se asume nada
        bool d1 = (e.AlphaA1 & 0x3F) == 0, d2 = (e.AlphaA2 & 0x3F) == 0;   // alfa de vertice puro, sin complemento
        if (e.AlphaOp == 4) return d1 || d2;
        if (e.AlphaOp == 2) return d1;
        return false;
    }
    static ulong HashEstadoDe(Tx dst)
    {
        ulong h = FnvIni;
        h = Fnv(h, (uint)(mezcla ? 1 : 0)); if (mezcla) { h = Fnv(h, origen); h = Fnv(h, destino); }
        h = Fnv(h, fvf);
        h = Fnv(h, dst == pantalla ? 0UL : (ulong)dst.W * 100000 + (ulong)dst.H);
        for (int s = 0; s < (fvf == 0x344 || fvf == 0x244 ? 2 : 1); s++)
        {
            Etapa e = E[s];
            if (s == 1 && e.ColorOp == 1) continue;
            h = Fnv(h, e.ColorOp); h = Fnv(h, e.ColorA1); h = Fnv(h, e.ColorA2); h = Fnv(h, e.AlphaOp); h = Fnv(h, e.AlphaA1); h = Fnv(h, e.AlphaA2); h = Fnv(h, e.Indice);
            bool usaTex = UsaTextura(e);
            if (usaTex) { h = Fnv(h, e.DirU); h = Fnv(h, e.DirV); h = Fnv(h, e.Mag); h = Fnv(h, e.Min); h = Fnv(h, ContenidoTex(e.Tex)); }
        }
        return h;
    }
    static bool UsaTextura(Etapa e)
    {
        if (e.ColorOp == 1) return false;
        return (e.ColorA1 & 15) == 2 || (e.ColorA2 & 15) == 2 || (e.AlphaA1 & 15) == 2 || (e.AlphaA2 & 15) == 2;
    }
    static ulong ContenidoTex(IntPtr tex)
    {
        Tx t;
        if (tex == IntPtr.Zero || !texturas.TryGetValue(tex.ToInt64(), out t)) return 0;
        Lee(t);
        ulong h = FnvIni; h = Fnv(h, (ulong)t.W); h = Fnv(h, (ulong)t.H);
        int[] p = t.Px; for (int i = 0; i < p.Length; i++) h = Fnv(h, (uint)p[i]);
        return h;
    }
    static void Lee(Tx t) { if (t.Sucia && !t.Rt) { Marshal.Copy(t.Mem, t.Px, 0, t.Px.Length); t.Sucia = false; } }
    static readonly Dictionary<long, string> nombres = new Dictionary<long, string>();
    static void Nombra(object o, string prefijo)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
            foreach (FieldInfo f in t.GetFields(TODO | BindingFlags.DeclaredOnly))
            {
                if (f.IsStatic) continue;
                if (f.FieldType == typeof(IntPtr)) { IntPtr v = (IntPtr)f.GetValue(o); if (v != IntPtr.Zero) nombres[v.ToInt64()] = f.Name; }
                else if (f.FieldType == typeof(IntPtr[])) { IntPtr[] a = (IntPtr[])f.GetValue(o); if (a != null) for (int i = 0; i < a.Length; i++) if (a[i] != IntPtr.Zero) nombres[a[i].ToInt64()] = f.Name + i; }
            }
    }
    static string EstadoTexto()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(mezcla ? (origen == 5 && destino == 6 ? "normal" : origen == 5 && destino == 2 ? "suma" : origen == 2 && destino == 6 ? "uno/inv" : "mezcla?") : "opaco");
        for (int s = 0; s < 2; s++)
        {
            Etapa e = E[s];
            if (s == 1 && e.ColorOp == 1) continue;
            if (UsaTextura(e))
            {
                Tx t; string d = "tex?";
                string nom;
                if (e.Tex != IntPtr.Zero && texturas.TryGetValue(e.Tex.ToInt64(), out t)) d = (nombres.TryGetValue(e.Tex.ToInt64(), out nom) ? nom : (t.Rt ? "imagen" : "tex")) + "[" + t.W + "x" + t.H + "]";
                sb.Append(" ").Append(d).Append(e.DirU == 1 ? "(rep)" : "");
            }
            else sb.Append(s == 0 ? " color" : " .");
        }
        return sb.ToString();
    }
    static string Sitio()
    {
        StackTrace st = new StackTrace(2, false);
        List<string> r = new List<string>();
        for (int i = 0; i < st.FrameCount && r.Count < 3; i++)
        {
            MethodBase m = st.GetFrame(i).GetMethod();
            if (m == null || m.DeclaringType == null) continue;
            string tn = m.DeclaringType.Name;
            if (tn == "HuellaCuadros" || tn == "D3D9Device" || tn.StartsWith("<") || tn == "RuntimeMethodHandle") continue;
            if (m.DeclaringType.Namespace != "Rocola") continue;
            if (m.Name.StartsWith("Draw") && tn == "GpuStarfield" && r.Count == 0 && i < 3) { r.Add(m.Name); continue; }
            r.Add(m.Name);
        }
        return string.Join("<", r.ToArray());
    }

    // ======================= LA RASTERIZACION (imitacion) =======================
    // Como una tarjeta: vertices ajustados a 1/256 de pixel, funciones de borde en enteros (sin huecos ni dobles entre triangulos
    // vecinos), regla de arriba-izquierda, centros de pixel en las coordenadas enteras (Direct3D 9). La mezcla y el muestreo son
    // en coma flotante y se redondean al escribir. No es una tarjeta: es una funcion repetible de lo que se manda.
    static long FloorDiv(long a, long b) { long q = a / b; if ((a % b != 0) && ((a < 0) != (b < 0))) q--; return q; }
    static long CeilDiv(long a, long b) { return -FloorDiv(-a, b); }
    static long Snap(float v) { return (long)Math.Floor(v * 256.0 + 0.5); }
    static void Rasteriza(int tris, int palabras, Tx dst, Orden orden)
    {
        int W = dst.W, H = dst.H;
        int[] D = dst.Px;
        Etapa e0 = E[0], e1 = E[1];
        bool dos = palabras >= 9 && e1.ColorOp != 1;
        Tx t0 = null, t1 = null;
        if (UsaTextura(e0) && e0.Tex != IntPtr.Zero) { texturas.TryGetValue(e0.Tex.ToInt64(), out t0); if (t0 != null) Lee(t0); }
        if (dos && UsaTextura(e1) && e1.Tex != IntPtr.Zero) { texturas.TryGetValue(e1.Tex.ToInt64(), out t1); if (t1 != null) Lee(t1); }
        bool dosUv = palabras >= 9 && e1.Indice == 1;
        // camino rapido: solo color de vertice, o textura modulada por el color de vertice, con mezcla ninguna/normal/suma
        bool soloDifuso = !dos && e0.ColorOp == 2 && (e0.ColorA1 & 0x3F) == 0 && e0.AlphaOp == 2 && (e0.AlphaA1 & 0x3F) == 0;
        bool modula = !dos && e0.ColorOp == 4 && e0.AlphaOp == 4 && (e0.ColorA1 & 0x3F) == 2 && (e0.ColorA2 & 0x3F) == 0
                      && (e0.AlphaA1 & 0x3F) == 2 && (e0.AlphaA2 & 0x3F) == 0 && t0 != null;
        int bm = !mezcla ? 0 : (origen == 5 && destino == 6) ? 1 : (origen == 5 && destino == 2) ? 2 : 3;
        bool rapido = (soloDifuso || modula) && bm < 3;
        bool lineal = modula && e0.Mag == 2;
        bool repite = modula && e0.DirU == 1;
        const float k255 = 1f / 255f;
        for (int t = 0; t < tris; t++)
        {
            int b = t * 3 * palabras;
            long ax = Snap(Fl(buf[b])), ay = Snap(Fl(buf[b + 1]));
            long bx = Snap(Fl(buf[b + palabras])), by = Snap(Fl(buf[b + palabras + 1]));
            long cx = Snap(Fl(buf[b + palabras * 2])), cy = Snap(Fl(buf[b + palabras * 2 + 1]));
            long area = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay);
            if (area == 0) continue;
            int ia = 0, ib = 1, ic = 2;
            if (area < 0) { ib = 2; ic = 1; area = -area; long tx = bx, ty = by; bx = cx; by = cy; cx = tx; cy = ty; }
            int baseA = b + ia * palabras, baseB = b + ib * palabras, baseC = b + ic * palabras;
            long minxF = Math.Min(ax, Math.Min(bx, cx)), maxxF = Math.Max(ax, Math.Max(bx, cx));
            long minyF = Math.Min(ay, Math.Min(by, cy)), maxyF = Math.Max(ay, Math.Max(by, cy));
            int miny = (int)CeilDiv(minyF, 256), maxy = (int)FloorDiv(maxyF, 256);
            int minx = (int)CeilDiv(minxF, 256), maxx = (int)FloorDiv(maxxF, 256);
            if (miny < 0) miny = 0; if (maxy > H - 1) maxy = H - 1; if (minx < 0) minx = 0; if (maxx > W - 1) maxx = W - 1;
            if (miny > maxy || minx > maxx) continue;
            long thr0 = EsArribaIzq(ax, ay, bx, by) ? 0 : 1, thr1 = EsArribaIzq(bx, by, cx, cy) ? 0 : 1, thr2 = EsArribaIzq(cx, cy, ax, ay) ? 0 : 1;
            // E_ab(x,y) = (bx-ax)*(y*256-ay) - (by-ay)*(x*256-ax)  =  c0 + c1*x
            long c1_0 = -(by - ay) * 256, c1_1 = -(cy - by) * 256, c1_2 = -(ay - cy) * 256;
            uint ca = (uint)buf[baseA + 4], cb = (uint)buf[baseB + 4], cc = (uint)buf[baseC + 4];
            float aA = ca >> 24, aB = cb >> 24, aC = cc >> 24;
            float rA = (ca >> 16) & 255, rB = (cb >> 16) & 255, rC = (cc >> 16) & 255;
            float gA = (ca >> 8) & 255, gB = (cb >> 8) & 255, gC = (cc >> 8) & 255;
            float bA = ca & 255, bB = cb & 255, bC = cc & 255;
            float ua = Fl(buf[baseA + 5]), va = Fl(buf[baseA + 6]), ub = Fl(buf[baseB + 5]), vb = Fl(buf[baseB + 6]), uc = Fl(buf[baseC + 5]), vc = Fl(buf[baseC + 6]);
            float u2a = 0, v2a = 0, u2b = 0, v2b = 0, u2c = 0, v2c = 0;
            if (palabras >= 9) { u2a = Fl(buf[baseA + 7]); v2a = Fl(buf[baseA + 8]); u2b = Fl(buf[baseB + 7]); v2b = Fl(buf[baseB + 8]); u2c = Fl(buf[baseC + 7]); v2c = Fl(buf[baseC + 8]); }
            double inv = 1.0 / area;
            for (int y = miny; y <= maxy; y++)
            {
                long py = (long)y * 256;
                long c0_0 = (bx - ax) * (py - ay) + (by - ay) * ax;
                long c0_1 = (cx - bx) * (py - by) + (cy - by) * bx;
                long c0_2 = (ax - cx) * (py - cy) + (ay - cy) * cx;
                long xl = minx, xr = maxx;
                if (!Recorta(c0_0, c1_0, thr0, ref xl, ref xr)) continue;
                if (!Recorta(c0_1, c1_1, thr1, ref xl, ref xr)) continue;
                if (!Recorta(c0_2, c1_2, thr2, ref xl, ref xr)) continue;
                for (int x = (int)xl; x <= (int)xr; x++)
                {
                    long e0v = c0_0 + c1_0 * x, e1v = c0_1 + c1_1 * x, e2v = c0_2 + c1_2 * x;
                    float pa = (float)(e1v * inv), pb = (float)(e2v * inv), pc = (float)(e0v * inv);
                    int idx = y * W + x;
                    if (rapido)
                    {
                        float sa = (aA * pa + aB * pb + aC * pc) * k255, sr = (rA * pa + rB * pb + rC * pc) * k255, sg = (gA * pa + gB * pb + gC * pc) * k255, sb = (bA * pa + bB * pb + bC * pc) * k255;
                        if (modula)
                        {
                            float u = ua * pa + ub * pb + uc * pc, v = va * pa + vb * pb + vc * pc;
                            float tr, tg, tb, tA;
                            MuestreaRapido(t0, lineal, repite, u, v, out tr, out tg, out tb, out tA);
                            sr *= tr; sg *= tg; sb *= tb; sa *= tA;
                        }
                        int dv = D[idx];
                        float rr, gg, bb;
                        if (bm == 0) { rr = sr; gg = sg; bb = sb; }
                        else
                        {
                            float dr = ((dv >> 16) & 255) * k255, dg = ((dv >> 8) & 255) * k255, db = (dv & 255) * k255;
                            if (bm == 1) { float ia2 = 1f - sa; rr = sr * sa + dr * ia2; gg = sg * sa + dg * ia2; bb = sb * sa + db * ia2; }
                            else { rr = sr * sa + dr; gg = sg * sa + dg; bb = sb * sa + db; }
                        }
                        int nv = Empaca(rr, gg, bb, dst.X8 ? 1f : sa);
                        orden.Area++; if (nv != dv) orden.Cambian++;
                        D[idx] = nv;
                        Cuenta1(dst, idx);
                        continue;
                    }
                    Pixel(D, W, x, y, pa, pb, pc, ca, cb, cc, ua, va, ub, vb, uc, vc, u2a, v2a, u2b, v2b, u2c, v2c, t0, t1, dos, dosUv, orden, dst);
                }
            }
        }
    }
    // Recorta [xl,xr] a los x con c0 + c1*x >= thr.
    static bool Recorta(long c0, long c1, long thr, ref long xl, ref long xr)
    {
        if (c1 == 0) return c0 >= thr;
        if (c1 > 0) { long m = CeilDiv(thr - c0, c1); if (m > xl) xl = m; }
        else { long m = FloorDiv(c0 - thr, -c1); if (m < xr) xr = m; }
        return xl <= xr;
    }
    static bool EsArribaIzq(long ax, long ay, long bx, long by) { long dx = bx - ax, dy = by - ay; return dy < 0 || (dy == 0 && dx > 0); }
    static void MuestreaRapido(Tx t, bool lineal, bool repite, float u, float v, out float r, out float g, out float b, out float a)
    {
        int W = t.W, H = t.H; int[] p = t.Px;
        const float k255 = 1f / 255f;
        if (!lineal)
        {
            int ix = (int)Math.Floor(u * W), iy = (int)Math.Floor(v * H);
            if (repite) { ix %= W; if (ix < 0) ix += W; iy %= H; if (iy < 0) iy += H; }
            else { ix = ix < 0 ? 0 : (ix >= W ? W - 1 : ix); iy = iy < 0 ? 0 : (iy >= H ? H - 1 : iy); }
            uint q = (uint)p[iy * W + ix];
            r = ((q >> 16) & 255) * k255; g = ((q >> 8) & 255) * k255; b = (q & 255) * k255; a = t.X8 ? 1f : (q >> 24) * k255; return;
        }
        float fx = u * W - 0.5f, fy = v * H - 0.5f;
        int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
        float tx = fx - x0, ty = fy - y0;
        int x1 = x0 + 1, y1 = y0 + 1;
        if (repite) { x0 %= W; if (x0 < 0) x0 += W; x1 %= W; if (x1 < 0) x1 += W; y0 %= H; if (y0 < 0) y0 += H; y1 %= H; if (y1 < 0) y1 += H; }
        else { x0 = x0 < 0 ? 0 : (x0 >= W ? W - 1 : x0); x1 = x1 < 0 ? 0 : (x1 >= W ? W - 1 : x1); y0 = y0 < 0 ? 0 : (y0 >= H ? H - 1 : y0); y1 = y1 < 0 ? 0 : (y1 >= H ? H - 1 : y1); }
        uint c00 = (uint)p[y0 * W + x0], c10 = (uint)p[y0 * W + x1], c01 = (uint)p[y1 * W + x0], c11 = (uint)p[y1 * W + x1];
        float w00 = (1 - tx) * (1 - ty), w10 = tx * (1 - ty), w01 = (1 - tx) * ty, w11 = tx * ty;
        r = (((c00 >> 16) & 255) * w00 + ((c10 >> 16) & 255) * w10 + ((c01 >> 16) & 255) * w01 + ((c11 >> 16) & 255) * w11) * k255;
        g = (((c00 >> 8) & 255) * w00 + ((c10 >> 8) & 255) * w10 + ((c01 >> 8) & 255) * w01 + ((c11 >> 8) & 255) * w11) * k255;
        b = ((c00 & 255) * w00 + (c10 & 255) * w10 + (c01 & 255) * w01 + (c11 & 255) * w11) * k255;
        a = t.X8 ? 1f : ((c00 >> 24) * w00 + (c10 >> 24) * w10 + (c01 >> 24) * w01 + (c11 >> 24) * w11) * k255;
    }
    static void Cuenta1(Tx dst, int ix)
    {
        if (dst != pantalla || contador == null) return;
        escritosTotal++;
        if (!mezcla) { sobreEscritos += contador[ix]; if (contador[ix] > 0) AcumTapado(ordenDe[ix], contador[ix]); contador[ix] = 1; }
        else contador[ix]++;
        ordenDe[ix] = ordenes.Count;
    }
    struct Col { public float R, G, B, A; }
    static float Cl(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
    static Col Arg(uint a, Col dif, Col act, Col tex)
    {
        Col c;
        switch (a & 15) { case 0: c = dif; break; case 1: c = act; break; default: c = tex; break; }
        if ((a & 0x20) != 0) { c.R = c.G = c.B = c.A; }
        if ((a & 0x10) != 0) { c.R = 1f - c.R; c.G = 1f - c.G; c.B = 1f - c.B; c.A = 1f - c.A; }
        return c;
    }
    static Col Muestrea(Tx t, Etapa e, float u, float v)
    {
        Col c; MuestreaRapido(t, e.Mag == 2, e.DirU == 1, u, v, out c.R, out c.G, out c.B, out c.A); return c;
    }
    static Col Unpack(int p, bool x8)
    {
        Col c; uint u = (uint)p;
        c.A = x8 ? 1f : (u >> 24) / 255f; c.R = ((u >> 16) & 255) / 255f; c.G = ((u >> 8) & 255) / 255f; c.B = (u & 255) / 255f; return c;
    }
    static void Pixel(int[] D, int W, int x, int y, float pa, float pb, float pc, uint ca, uint cb, uint cc,
        float ua, float va, float ub, float vb, float uc, float vc, float u2a, float v2a, float u2b, float v2b, float u2c, float v2c,
        Tx t0, Tx t1, bool dos, bool dosUv, Orden orden, Tx dst)
    {
        Col dif;
        dif.A = (((ca >> 24) * pa + (cb >> 24) * pb + (cc >> 24) * pc)) / 255f;
        dif.R = ((((ca >> 16) & 255) * pa + ((cb >> 16) & 255) * pb + ((cc >> 16) & 255) * pc)) / 255f;
        dif.G = ((((ca >> 8) & 255) * pa + ((cb >> 8) & 255) * pb + ((cc >> 8) & 255) * pc)) / 255f;
        dif.B = (((ca & 255) * pa + (cb & 255) * pb + (cc & 255) * pc)) / 255f;
        Etapa e0 = E[0];
        Col tex0 = default(Col);
        if (t0 != null) tex0 = Muestrea(t0, e0, ua * pa + ub * pb + uc * pc, va * pa + vb * pb + vc * pc);
        else if (UsaTextura(e0)) { tex0.R = tex0.G = tex0.B = 0; tex0.A = 1; }
        Col act = dif;   // "actual" de entrada: el color difuso
        act = Etapa1(e0, dif, act, tex0);
        if (dos)
        {
            Etapa e1 = E[1];
            Col tex1 = default(Col);
            if (t1 != null)
            {
                float uu = dosUv ? u2a * pa + u2b * pb + u2c * pc : ua * pa + ub * pb + uc * pc;
                float vv = dosUv ? v2a * pa + v2b * pb + v2c * pc : va * pa + vb * pb + vc * pc;
                tex1 = Muestrea(t1, e1, uu, vv);
            }
            act = Etapa1(e1, dif, act, tex1);
        }
        int idx = y * W + x;
        int v0 = D[idx];
        int nuevo;
        if (!mezcla) nuevo = Empaca(act.R, act.G, act.B, dst.X8 ? 1f : act.A);
        else
        {
            Col d = Unpack(v0, dst.X8);
            float sa = act.A;
            float sf = origen == 5 ? sa : (origen == 2 ? 1f : (origen == 6 ? 1f - sa : 1f));
            float df = destino == 6 ? 1f - sa : (destino == 2 ? 1f : (destino == 5 ? sa : 0f));
            nuevo = Empaca(act.R * sf + d.R * df, act.G * sf + d.G * df, act.B * sf + d.B * df, dst.X8 ? 1f : act.A * sf + d.A * df);
        }
        orden.Area++;
        if (nuevo != v0) orden.Cambian++;
        D[idx] = nuevo;
        Cuenta1(dst, idx);
    }
    static Dictionary<long, long> tapadoPorOrden = new Dictionary<long, long>();
    static void AcumTapado(long orden, int n) { long v; tapadoPorOrden.TryGetValue(orden, out v); tapadoPorOrden[orden] = v + n; }
    static Col Etapa1(Etapa e, Col dif, Col act, Col tex)
    {
        Col r = act;
        if (e.ColorOp != 1)
        {
            Col a1 = Arg(e.ColorA1, dif, act, tex), a2 = Arg(e.ColorA2, dif, act, tex);
            switch (e.ColorOp)
            {
                case 2: r.R = a1.R; r.G = a1.G; r.B = a1.B; break;
                case 3: r.R = a2.R; r.G = a2.G; r.B = a2.B; break;
                case 4: r.R = a1.R * a2.R; r.G = a1.G * a2.G; r.B = a1.B * a2.B; break;
                case 11: r.R = a1.R + a2.R - a1.R * a2.R; r.G = a1.G + a2.G - a1.G * a2.G; r.B = a1.B + a2.B - a1.B * a2.B; break;
                default: break;
            }
        }
        if (e.AlphaOp != 1)
        {
            Col a1 = Arg(e.AlphaA1 & ~0x20u, dif, act, tex), a2 = Arg(e.AlphaA2 & ~0x20u, dif, act, tex);
            switch (e.AlphaOp)
            {
                case 2: r.A = a1.A; break;
                case 3: r.A = a2.A; break;
                case 4: r.A = a1.A * a2.A; break;
                case 11: r.A = a1.A + a2.A - a1.A * a2.A; break;
                default: break;
            }
        }
        return r;
    }
    static int Empaca(float r, float g, float b, float a)
    {
        int R = (int)(Cl(r) * 255f + 0.5f), G = (int)(Cl(g) * 255f + 0.5f), B = (int)(Cl(b) * 255f + 0.5f), A = (int)(Cl(a) * 255f + 0.5f);
        return (A << 24) | (R << 16) | (G << 8) | B;
    }
    static void AlPresentar()
    {
        if (pinta && pantalla != null)
        {
            ulong h = FnvIni; int[] p = pantalla.Px;
            for (int i = 0; i < p.Length; i++) h = Fnv(h, (uint)p[i]);
            hPixeles = h;
        }
    }

    // ======================= MONTAR LA ROCOLA =======================
    static object Tarjeta(int w, int h)
    {
        PreparaTexturas();
        IntPtr vt = Marshal.AllocHGlobal(128 * IntPtr.Size);
        for (int i = 0; i < 128; i++) Marshal.WriteIntPtr(vt, i * IntPtr.Size, Puntero("UintSelf", "Nada"));
        Marshal.WriteIntPtr(vt, 4 * IntPtr.Size, Puntero("UintSelf", "Memoria"));
        Marshal.WriteIntPtr(vt, 37 * IntPtr.Size, Puntero("SetTexDel", "SetRt"));
        Marshal.WriteIntPtr(vt, 38 * IntPtr.Size, PunteroPorDireccion("GetPtrDel", "GetRt", 2));
        Marshal.WriteIntPtr(vt, 70 * IntPtr.Size, PunteroPorDireccion("ValidaDel", "Valida", 1));
        IntPtr obj = Marshal.AllocHGlobal(16); Marshal.WriteIntPtr(obj, vt);
        // Con el constructor de verdad (falla sin d3d9.dll y deja el objeto "muerto" pero con todos los inicializadores hechos).
        object d = Activator.CreateInstance(TDev, new object[] { IntPtr.Zero, w, h });
        Pon(d, "dev", obj); Pon(d, "stTex", new IntPtr(-1));
        Pon(d, "lost", false); Pon(d, "resetPending", false);
        object pp = Activator.CreateInstance(TD3D.GetNestedType("PresentParams", TODO));
        pp.GetType().GetField("BackBufferWidth").SetValue(pp, (uint)w); pp.GetType().GetField("BackBufferHeight").SetValue(pp, (uint)h);
        Pon(d, "pp", pp);
        Enchufa(d, "createTex", "CreaTex");
        Enchufa(d, "setRS", "SetRS"); Enchufa(d, "setFVF", "SetFvf"); Enchufa(d, "setTSS", "SetTSS"); Enchufa(d, "setSamp", "SetSamp");
        Enchufa(d, "setTex", "SetTex"); Enchufa(d, "drawUp", "DrawUp"); Enchufa(d, "present", "Present");
        Enchufa(d, "begin", "Begin"); Enchufa(d, "end", "Hr"); Enchufa(d, "testCoop", "Hr");
        EnchufaPorDireccion(d, "reset", "ResetDel", "Reset", 1);
        Pon(d, "verticesSinSoporte", true);
        Pon(d, "juntar", true); Pon(d, "CapsDosTexturas", true); Pon(d, "CapsSinEspera", false);
        pantalla = new Tx(); pantalla.W = w; pantalla.H = h; pantalla.Px = new int[w * h]; pantalla.Rt = true; pantalla.X8 = true; pantalla.Id = ++idTx;
        blanco = fondo; superficies[fondo.ToInt64()] = pantalla;
        mezcla = false; origen = 2; destino = 1; fvf = 0;
        E[0] = new Etapa(); E[1] = new Etapa(); E[1].ColorOp = 1; E[1].AlphaOp = 1; E[1].Indice = 1;
        if (!(bool)Llama(d, "InitStates")) throw new Exception("la tarjeta imitada no arranca");
        return d;
    }
    static void Enchufa(object d, string campo, string metodo)
    {
        FieldInfo f = Campo(TDev, campo);
        if (f == null) throw new Exception("el dispositivo no tiene " + campo);
        Delegate del = Delegate.CreateDelegate(f.FieldType, typeof(HuellaCuadros).GetMethod(metodo, TODO));
        vivos.Add(del); f.SetValue(d, del);
    }
    static void EnchufaPorDireccion(object d, string campo, string tipo, string metodo, int arg)
    {
        FieldInfo f = Campo(TDev, campo);
        Type t = TD3D.GetNestedType(tipo, TODO);
        Delegate del = Marshal.GetDelegateForFunctionPointer(PunteroPorDireccion(tipo, metodo, arg), t);
        vivos.Add(del); f.SetValue(d, del);
    }
    static Bitmap celda;
    static int ancho, alto;
    static object textHost;
    static Bitmap actual;
    static bool animado = true;
    static readonly float[] bandas = { .7f, .65f, .5f, .4f, .5f, .3f, .2f, .1f };
    static int cuadroN;

    static int montajeId;
    static class GridData<T> { internal static T[] Rows; internal static int Id; }
    static Bitmap[] portadas;
    static Bitmap etiqueta;
    static Bitmap NuevaPortada(int k)
    {
        Bitmap b = new Bitmap(300, 300, PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            using (System.Drawing.Drawing2D.LinearGradientBrush br = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, 300, 300),
                Color.FromArgb(40 + k * 30, 60, 200 - k * 20), Color.FromArgb(220, 180 - k * 25, 40 + k * 10), 45f + k * 20)) g.FillRectangle(br, 0, 0, 300, 300);
            using (SolidBrush w = new SolidBrush(Color.FromArgb(230, 245, 245, 245))) { g.FillEllipse(w, 60 + k * 10, 60, 180 - k * 10, 180); }
            using (SolidBrush d = new SolidBrush(Color.FromArgb(255, 20, 20, 30))) { g.FillEllipse(d, 135, 135, 30, 30); g.FillRectangle(d, 0, 270 - k * 5, 300, 30); }
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
            using (Font f = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Bold))
            using (SolidBrush w = new SolidBrush(Color.White))
            { g.DrawString("Cancion de ejemplo", f, w, 66, 4); g.DrawString("Artista de ejemplo", f, w, 160, 36); }
            using (SolidBrush c = new SolidBrush(Color.FromArgb(255, 70, 90, 140))) g.FillEllipse(c, 4, 4, 56, 56);
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
            Pon(c, "Key", "disco" + i); Pon(c, "Bmp", etiqueta); Pon(c, "Slot", i);
            Pon(c, "Direct", true); Pon(c, "FullCover", portadas[i % 6]); Pon(c, "HasCover", true);
            object lay = Activator.CreateInstance(tl);
            Pon(lay, "Width", 256); Pon(lay, "Height", 64);
            Pon(lay, "Face", new Rectangle(0, 0, 64, 64)); Pon(lay, "FaceAt", new Rectangle(0, 0, 64, 64));
            Pon(lay, "Text1", new Rectangle(64, 0, 96, 32)); Pon(lay, "Text1At", new Rectangle(64, 0, 96, 32));
            Pon(lay, "Text2", new Rectangle(160, 32, 96, 32)); Pon(lay, "Text2At", new Rectangle(160, 32, 96, 32));
            Pon(c, "Layout", lay);
            Pon(c, "CoverRect", new RectangleF(x + (cw - lado) / 2, y, lado, lado));
            Pon(c, "X", x); Pon(c, "Y", y + lado - 64 + 8); Pon(c, "W", (float)cw); Pon(c, "H", 64f); Pon(c, "A", 1f);
            Pon(c, "Sel", i == 7);
            data[i] = (T)c;
        }
        GridData<T>.Rows = (T[])data.Clone(); GridData<T>.Id = montajeId; return 18;
    }
    static bool conAsistente, conGuia; static Bitmap panelAsistente;
    static T Asistente<T>()
    {
        object st = Activator.CreateInstance(typeof(T));
        if (!conAsistente) return (T)st;
        if (panelAsistente == null)
        {
            panelAsistente = new Bitmap(420, 180, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(panelAsistente))
            {
                g.Clear(Color.FromArgb(235, 20, 30, 60));
                using (Font f = new Font(FontFamily.GenericSansSerif, 12f, FontStyle.Bold)) using (SolidBrush w = new SolidBrush(Color.White))
                { g.DrawString("Hola, soy la estrella.", f, w, 12, 12); g.DrawString("Toca un disco para escuchar.", f, w, 12, 60); }
            }
        }
        Pon(st, "Visible", true); Pon(st, "Animada", false); Pon(st, "Panel", panelAsistente); Pon(st, "Version", 1);
        Pon(st, "Guia", conGuia);
        if (conGuia) Pon(st, "Objetivo", new RectangleF(ancho * .5f, alto * .3f, 160f, 80f));
        return (T)st;
    }
    static bool conCubos, reposoMontaje;
    static T Cubo<T>()
    {
        object st = Activator.CreateInstance(typeof(T));
        if (!conCubos) return (T)st;
        Type ti = typeof(T).GetField("Sel").FieldType;
        int cw = ancho / 6, ch = (alto - 200) / 3, lado = Math.Max(32, Math.Min(cw - 24, ch - 40));
        object sel = Activator.CreateInstance(ti);
        if (!reposoMontaje) {   // el cubo que gira de la pared; en el reposo solo queda el chico de la barra
        Pon(sel, "Has", true); Pon(sel, "X", (float)(cw + 12)); Pon(sel, "Y", 100f + ch); Pon(sel, "W", (float)lado); Pon(sel, "H", (float)lado);
        Pon(sel, "Angle", cuadroN * 0.03); Pon(sel, "CoverA", portadas[1]); Pon(sel, "Tilt", 0.05f * (float)Math.Sin(cuadroN * .1));
        Pon(st, "Sel", sel); }
        object mini = Activator.CreateInstance(ti);
        Rectangle gr = (Rectangle)TPanel.GetMethod("IdleCoverRect", TODO).Invoke(null, new object[] { ancho, (int)TPanel.GetMethod("IdlePanelHeight", TODO).Invoke(null, new object[] { alto }) });
        Pon(mini, "Has", true); Pon(mini, "X", (float)gr.X); Pon(mini, "Y", (float)(alto - 90)); Pon(mini, "W", 60f); Pon(mini, "H", 60f);
        Pon(mini, "Angle", cuadroN * 0.02); Pon(mini, "CoverA", portadas[2]);
        Pon(st, "Mini", mini);
        return (T)st;
    }
    static T Aurora<T>()
    {
        object wave = Activator.CreateInstance(typeof(T));
        Pon(wave, "Has", true);
        float[] b = new float[8];
        for (int i = 0; i < 8; i++) b[i] = Math.Max(0f, Math.Min(1f, bandas[i] + .15f * (float)Math.Sin(cuadroN * .05 + i)));
        Pon(wave, "B", b); Pon(wave, "Alto", 140f); Pon(wave, "BaseY", (float)(alto - 96));
        Pon(wave, "Dim", dimAurora); Pon(wave, "DimFilo", 1f); Pon(wave, "Calma", .4f);
        Pon(wave, "Rgb", 0xFF30E080u); Pon(wave, "Rgb2", 0xFF8040FFu); Pon(wave, "RgbA", 0xFF30A0FFu); Pon(wave, "Rgb2A", 0xFFFF60C0u);
        return (T)wave;
    }
    static float dimAurora = .5f;
    static class ProgressData<T> { internal static T State; internal static int Id = -1; }
    static T Progress<T>()
    {
        if (ProgressData<T>.Id != montajeId)
        {
            object p = Activator.CreateInstance(typeof(T));
            Pon(p, "Has", true); Pon(p, "Neon", true); Pon(p, "Value", .37);
            Pon(p, "Rect", new Rectangle(180, alto - 70, ancho - 240, 12));
            Pon(p, "Color", 0xFF48A0D0u); Pon(p, "Key", new object());
            ProgressData<T>.State = (T)p; ProgressData<T>.Id = montajeId;
        }
        return ProgressData<T>.State;
    }
    static T[] Cintas<T>() { return (T[])Toma(textHost, "cintasTexto"); }
    static Delegate Generic(FieldInfo field, string method)
    {
        Type type = field.FieldType.GetMethod("Invoke").ReturnType;
        if (method == "Cintas") type = type.GetElementType();
        if (method == "Asistente") type = field.FieldType.GetMethod("Invoke").ReturnType;
        if (method == "Cubo") type = field.FieldType.GetMethod("Invoke").ReturnType;
        if (method == "Grid") type = field.FieldType.GetMethod("Invoke").GetParameters()[0].ParameterType.GetElementType();
        return Delegate.CreateDelegate(field.FieldType, typeof(HuellaCuadros).GetMethod(method, TODO).MakeGenericMethod(type));
    }
    static Bitmap Idle() { return actual; }
    static Bitmap PintaPanel(bool opaco, bool cambiado, bool textos)
    {
        int n = 12; string[] t = new string[n], a = new string[n]; Image[] covers = new Image[n];
        for (int i = 0; i < n; i++) { t[i] = "Cancion " + i + (textos ? " con nombre largo para leer completo, version extendida en vivo" : ""); a[i] = "Artista"; covers[i] = celda; }
        MethodInfo reuse = TF.GetMethod("RenderIdlePanelEn", TODO);
        object[] args = { ancho, (int)TPanel.GetMethod("IdlePanelHeight", TODO).Invoke(null, new object[] { alto }), cambiado ? "Otra cancion" : "Cancion actual", "Artista", "1:00 / 3:00", t, a, covers, n, Color.Teal, false, 0, true, null, 0 };
        object[] nuevo = new object[args.Length + 2]; nuevo[0] = actual;
        Array.Copy(args, 0, nuevo, 1, args.Length - 1); nuevo[nuevo.Length - 2] = opaco; nuevo[nuevo.Length - 1] = 0;
        return (Bitmap)reuse.Invoke(null, nuevo);
    }

    sealed class Esc
    {
        public string Nombre; public int W = 1024, H = 768; public string Preset = "alto";
        public bool Reposo, Estrella3D = true, Aurora = true, Textos, Progreso, Luna = true, Cubos = true, Asistente, Guia;
        public string[] Apaga = new string[0];   // campos bool del panel que se ponen a false
        public string[] Enciende = new string[0];
        public Esc Con(string nombre) { Esc e = (Esc)MemberwiseClone(); e.Nombre = nombre; return e; }
    }
    sealed class Montaje { public Control Panel; public Esc E; public object Estrellas; public Delegate Cuadro; }

    static readonly string[] Ahorros = { "CieloMedia", "GranoVineta", "FondoUnaCapa", "CieloCuarto", "EstadoOpaco", "AuroraLigera", "ReposoSinReflejo", "ReposoSinEstela", "CoronaCompacta", "SinGrano", "AuroraSinSuelo", "AuroraHaloCompacto" };
    static Montaje Monta(Esc e)
    {
        ancho = e.W; alto = e.H;
        montajeId++;
        object tarjeta = Tarjeta(e.W, e.H);
        object sf = Activator.CreateInstance(TStar, TODO, null, new object[] { IntPtr.Zero, e.W, e.H, 4321 }, null);
        Pon(sf, "gpu", tarjeta); Pon(sf, "seed0", 4321);
        Llama(sf, "BuildStars", e.W, e.H, 4321);
        // El disco de luz (glow) de las estrellas lo crea el constructor cuando hay tarjeta; aqui se suben a mano, como el constructor.
        foreach (string nombre in new string[] { "BakeStarTexture", "BakeRadialGlow" })
        {
            MethodInfo m = Metodo(TStar, nombre);
            using (Bitmap bmp = (Bitmap)m.Invoke(null, new object[] { 64 }))
                Pon(sf, nombre == "BakeStarTexture" ? "glowTex" : "uiGlowTex", Llama(tarjeta, "CreateTextureFromBitmap", bmp));
        }
        Control p = (Control)Activator.CreateInstance(TPanel, true);
        p.SetBounds(0, 0, e.W, e.H);
        Pon(p, "stars", sf);
        PreparaPortadas(); conCubos = e.Cubos; reposoMontaje = e.Reposo; conAsistente = e.Asistente; conGuia = e.Guia;
        FieldInfo fasis = TPanel.GetField("GetAsistenteState", TODO); fasis.SetValue(p, Generic(fasis, "Asistente"));
        FieldInfo fcubo = TPanel.GetField("GetCubeState", TODO); fcubo.SetValue(p, Generic(fcubo, "Cubo"));
        celda = new Bitmap(156, 130, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(celda)) g.Clear(Color.DarkSlateBlue);
        if (e.Textos)
        {
            textHost = Codigo.FormSinEncender(TF); Pon(textHost, "gpuEnabled", true); Pon(textHost, "textosDeslizantes", true);
            FieldInfo tapes = TPanel.GetField("GetCintasTexto", TODO); tapes.SetValue(p, Generic(tapes, "Cintas"));
        }
        else textHost = null;
        if (e.Progreso) { FieldInfo f = TPanel.GetField("GetProgress", TODO); f.SetValue(p, Generic(f, "Progress")); }
        // La pantalla de siempre: todo lo visual de fabrica ya viene encendido en el panel. Aqui solo lo que cambia con el preset.
        int nivel = e.Preset == "medio" ? 1 : e.Preset == "bajo" ? 0 : 2;
        bool ahorros = e.Preset != "alto";
        Pon(p, "Nieblas", nivel); Pon(p, "Fugaces", nivel); Pon(p, "RelevoPatron", nivel);
        Pon(p, "Estrellas", nivel == 2 ? 1f : nivel == 1 ? 0.6f : 0.3f);
        dimAurora = nivel == 2 ? .9f : nivel == 1 ? .5f : 0f;
        foreach (string a in Ahorros) PonSiHay(p, a, ahorros);
        if (ahorros) Pon(p, "EstrellaEspejo", false);
        Pon(p, "ConLuna", e.Luna); Pon(p, "LunaActiva", true); Pon(p, "Estrella3D", e.Estrella3D);
        foreach (string s in e.Apaga) Pon(p, s, false);
        foreach (string s in e.Enciende) Pon(p, s, true);
        Pon(p, "Dim", 1f); Pon(p, "ShowAlbum", !e.Reposo); Pon(p, "IdleExpandTarget", e.Reposo);
        Pon(p, "uiBotH", 96); Pon(p, "idleBlend", e.Reposo ? 1f : 0f); Pon(p, "IdlePanelVersion", 1);
        TPanel.GetField("GetIdlePanel", TODO).SetValue(p, new Func<Bitmap>(Idle));
        FieldInfo grid = TPanel.GetField("GetGrid", TODO); grid.SetValue(p, Generic(grid, "Grid"));
        if (e.Aurora && nivel > 0) { FieldInfo wave = TPanel.GetField("GetAurora", TODO); wave.SetValue(p, Generic(wave, "Aurora")); }
        actual = null; actual = PintaPanel(Toma(p, "EstadoOpaco") is bool && (bool)Toma(p, "EstadoOpaco"), false, e.Textos);
        Montaje m2 = new Montaje(); m2.Panel = p; m2.E = e; m2.Estrellas = sf;
        m2.Cuadro = Delegate.CreateDelegate(typeof(Func<bool>), p, Metodo(TPanel, "RenderFrame"));
        return m2;
    }
    static void Desmonta(Montaje m)
    {
        if (actual != null) { actual.Dispose(); actual = null; }
        if (celda != null) { celda.Dispose(); celda = null; }
        if (portadas != null) { foreach (Bitmap b in portadas) b.Dispose(); portadas = null; }
        if (etiqueta != null) { etiqueta.Dispose(); etiqueta = null; }
        if (panelAsistente != null) { panelAsistente.Dispose(); panelAsistente = null; }
        if (textHost != null) { try { Llama(textHost, "SueltaCintasTexto"); } catch { } textHost = null; }
        try { m.Panel.Dispose(); } catch { }
        // las texturas de esta tarjeta ya no se usan
        texturas.Clear(); superficies.Clear(); GC.Collect();
        // genericos con estado
    }
    // Un cuadro: pone el reloj, mueve las fases como el temporizador de la rocola (24 de cada 60 cuadros) y dibuja.
    static bool Cuadro(Montaje m, int n)
    {
        cuadroN = n;
        PonReloj(n * 1000.0 / 60.0);
        Control p = m.Panel;
        if (animado)
        {
            float fase = (n * 24 / 60) * .005f;
            PonSiHay(p, "NebFase", fase); PonSiHay(p, "HazFase", fase * .7f);
        }
        if (m.E.Reposo && n % 30 == 0)
        {
            Bitmap viejo = actual; actual = PintaPanel(Estado(m, "EstadoOpaco"), (n / 30) % 2 == 0, m.E.Textos);
            if (!ReferenceEquals(viejo, actual) && viejo != null) viejo.Dispose();
            Pon(p, "IdlePanelVersion", 2 + n / 30); Pon(p, "IdlePanelDirty", Rectangle.Empty);
        }
        return ((Func<bool>)m.Cuadro)();
    }
    static bool Estado(Montaje m, string campo) { return (bool)Toma(m.Panel, campo); }

    // ======================= LOS ESCENARIOS =======================
    static List<Esc> Escenarios()
    {
        List<Esc> r = new List<Esc>();
        int[][] pantallas = { new int[] { 1024, 768 }, new int[] { 1280, 1024 }, new int[] { 1920, 1080 } };
        string[] presets = { "alto", "perf", "medio", "bajo" };
        foreach (int[] pw in pantallas)
            foreach (string pr in presets)
                foreach (bool reposo in new bool[] { false, true })
                    foreach (bool tres in new bool[] { true, false })
                    {
                        Esc e = new Esc(); e.W = pw[0]; e.H = pw[1]; e.Preset = pr; e.Reposo = reposo; e.Estrella3D = tres; e.Textos = true; e.Progreso = true;
                        e.Nombre = pw[0] + "x" + pw[1] + " " + pr + (reposo ? " reposo" : " rejilla") + (tres ? " est3D" : " est2D");
                        r.Add(e);
                    }
        // Cada interruptor visual, uno a uno, apagado en la pantalla de fabrica; y cada ahorro de Performance, uno a uno, encendido en la de
        // fabrica y apagado en el preset Performance. Solo a 1024x768, rejilla y reposo.
        string[] visuales = { "VinetaSuave", "HaloSeleccion", "EstrellasFinas", "OndasSuaves", "NeonIntenso", "CieloProfundo", "AuroraBandas", "LunaActiva", "ControlesReposo",
            "LunaRealista", "HaloLunar", "PolvoLunar", "LunaProtagonista", "EstelaEstelar", "CieloSuave", "ColoresProfundos", "AuroraLimpia", "EstrellaDegradado", "EstrellaRefleja",
            "EstrellaDestello", "EstrellaBorde", "EstrellaEspejo", "PortadaBrillo", "LunaDelCielo", "LluviasEstrellas", "FugazFestejo", "ProfundidadPagina", "CieloPorHora",
            "ReflejoAurora", "ColoresFluidos", "NebulosasCine", "ConLuna" };
        foreach (bool reposo in new bool[] { false, true })
        {
            foreach (string v in visuales)
            {
                Esc e = new Esc(); e.W = 1024; e.H = 768; e.Preset = "alto"; e.Reposo = reposo; e.Textos = true; e.Progreso = true; e.Apaga = new string[] { v };
                e.Nombre = "sw 1024x768 alto " + (reposo ? "reposo" : "rejilla") + " sin " + v; r.Add(e);
            }
            foreach (string a in Ahorros)
            {
                Esc e = new Esc(); e.W = 1024; e.H = 768; e.Preset = "alto"; e.Reposo = reposo; e.Textos = true; e.Progreso = true; e.Enciende = new string[] { a };
                e.Nombre = "sw 1024x768 alto " + (reposo ? "reposo" : "rejilla") + " con " + a; r.Add(e);
                Esc q = new Esc(); q.W = 1024; q.H = 768; q.Preset = "perf"; q.Reposo = reposo; q.Textos = true; q.Progreso = true; q.Apaga = new string[] { a };
                q.Nombre = "sw 1024x768 perf " + (reposo ? "reposo" : "rejilla") + " sin " + a; r.Add(q);
            }
        }
        // La estrella asistente (sin rostro) y su guia, encima de todo, en las dos pantallas y con y sin ahorros.
        foreach (string pr in new string[] { "alto", "perf" })
            foreach (bool reposo in new bool[] { false, true })
                foreach (bool guia in new bool[] { false, true })
                {
                    Esc e = new Esc(); e.W = 1024; e.H = 768; e.Preset = pr; e.Reposo = reposo; e.Asistente = true; e.Guia = guia; e.Textos = false; e.Progreso = false;
                    e.Nombre = "1024x768 " + pr + (reposo ? " reposo" : " rejilla") + " asistente" + (guia ? " guia" : "");
                    r.Add(e);
                }
        return r;
    }

    // ======================= MAIN =======================
    static string carpetaCopia;
    static string Cargable(string ruta)
    {
        if (Type.GetType("Mono.Runtime") == null || Environment.OSVersion.Platform == PlatformID.Win32NT) return ruta;
        string dir = Path.Combine(Path.GetTempPath(), "rocola_huella_" + System.Diagnostics.Process.GetCurrentProcess().Id);
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
    [STAThread]
    static int Main(string[] argv)
    {
        try { return Prueba(argv); }
        finally { if (carpetaCopia != null) try { Directory.Delete(carpetaCopia, true); } catch { } }
    }
    static bool Carga(string ruta)
    {
        Assembly a = Assembly.LoadFrom(Cargable(ruta));
        TF = a.GetType("Rocola.Form1"); TPanel = a.GetType("Rocola.GpuPanel");
        if (TPanel == null) return false;
        TStar = a.GetType("Rocola.GpuStarfield"); TDev = a.GetType("Rocola.D3D9Device"); TD3D = a.GetType("Rocola.D3D9"); TReloj = a.GetType("Rocola.HuellaReloj");
        return true;
    }
    static int Prueba(string[] argv)
    {
        if (argv.Length == 1) return Bateria(argv[0]);
        if (argv.Length < 2) { Console.WriteLine("uso: huellacuadros <rocola.exe> [huella|coste|detalle|componentes|largo|panel|lista [filtro]]"); return 2; }
        modo = argv[1];
        string filtro = argv.Length > 2 ? argv[2] : "";
        if (!Carga(argv[0])) { Console.WriteLine("  sin tarjeta: nada que medir\n  OK huellacuadros (perfil sin GPU)"); return 0; }
        List<Esc> todos = Escenarios();
        if (modo == "lista") { foreach (Esc e in todos) Console.WriteLine(e.Nombre); return 0; }
        if (modo == "huella" && TReloj == null) { Console.WriteLine("  FALLO: la huella necesita el ejecutable instrumentado (pruebas/huella-compila.sh)"); return 1; }
        foreach (Esc e in todos)
        {
            if (filtro.Length > 0 && e.Nombre.IndexOf(filtro, StringComparison.Ordinal) < 0) continue;
            if (modo == "huella") Huella(e);
            else if (modo == "coste") Coste(e);
            else if (modo == "detalle") Detalle(e);
            else if (modo == "componentes") Componentes(e);
            else if (modo == "largo") Largo(e);
            else if (modo == "panel") RepintaPanel(e);
            else if (modo == "vinetea") MicroVinetea(e);
            else if (modo == "portadas") MicroPortadas(e);
        }
        return 0;
    }

    // ======================= LA BATERIA =======================
    // Lo que corre pruebas/todo.sh: compila UNA COPIA instrumentada de las fuentes actuales (pruebas/huella-compila.sh), monta unas
    // cuantas pantallas con la tarjeta imitada y exige que los PIXELES (segun la imitacion) sean los mismos que los de la rama
    // antes de los ahorros automaticos del 19-10-2026 (pruebas/huellas_modest_2026-10-19.txt), que el chorro de ordenes sea el mismo
    // donde el ahorro no debia tocarlo, y que los ahorros sigan ahorrando (llamadas al controlador y pantallas pintadas).
    // Para rehacer el archivo tras un cambio de aspecto QUERIDO: mono huellacuadros.exe <instrumentado.exe> huella "<filtro>".
    static int bfallos, boks;
    static void Exige(bool c, string q) { if (c) { boks++; Console.WriteLine("  OK    " + q); } else { bfallos++; Console.WriteLine("  FALLO " + q); } }
    static readonly string[] bateriaPantallas = {
        "1024x768 alto rejilla est3D", "1024x768 alto rejilla est2D", "1024x768 alto reposo est3D", "1024x768 perf rejilla est3D", "1024x768 perf reposo est3D",
        "1024x768 medio reposo est2D", "1024x768 bajo reposo est3D", "1024x768 bajo rejilla est2D", "1280x1024 bajo reposo est2D", "1920x1080 alto reposo est2D",
        "1920x1080 perf rejilla est3D", "1024x768 alto rejilla asistente guia", "1024x768 perf reposo asistente",
        "sw 1024x768 alto reposo con EstadoOpaco", "sw 1024x768 alto reposo sin CieloSuave", "sw 1024x768 alto rejilla sin ReflejoAurora" };
    // Donde el ahorro NO debe tocar ni una orden ni un vertice (el chorro bruto es el de antes): todo lo que no es reposo de fabrica o con el estado opaco.
    static bool BrutoIgual(string nombre)
    { return nombre.IndexOf("alto reposo") < 0 && nombre.IndexOf("bajo reposo") < 0 && nombre.IndexOf("medio reposo") < 0 || nombre.IndexOf("perf reposo") >= 0; }
    static int Bateria(string rocola)
    {
        if (!Carga(rocola)) { Console.WriteLine("  sin tarjeta: nada que medir\n  OK huellacuadros (perfil sin GPU)"); return 0; }
        string aqui = Directory.GetCurrentDirectory();
        string raiz = Path.GetFullPath(Path.Combine(aqui, ".."));
        string guion = Path.Combine(aqui, "huella-compila.sh"), base0 = Path.Combine(aqui, "huellas_modest_2026-10-19.txt");
        if (!File.Exists(guion) || !File.Exists(base0) || !Directory.Exists(Path.Combine(raiz, "Rocola")))
        { Console.WriteLine("  (sin las fuentes o sin el archivo de huellas junto a la prueba: no se puede comparar)\n  OK huellacuadros (omitida)"); return 0; }
        string dir = Path.Combine(Path.GetTempPath(), "rocola_huellab_" + System.Diagnostics.Process.GetCurrentProcess().Id);
        Directory.CreateDirectory(dir);
        string exe = Path.Combine(dir, "rocola_huella.exe");
        try
        {
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("sh", "\"" + guion + "\" \"" + raiz + "\" \"" + exe + "\"");
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            string salida; using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi)) { salida = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd(); p.WaitForExit(); }
            Exige(File.Exists(exe), "se compila la copia instrumentada de las fuentes (relojes y azar de pruebas)" + (File.Exists(exe) ? "" : ": " + salida));
            if (!File.Exists(exe)) return 1;
            if (!Carga(exe)) { Exige(false, "la copia instrumentada trae la tarjeta"); return 1; }
            Exige(TReloj != null, "y trae el reloj de pruebas");
            Dictionary<string, string[]> antes = new Dictionary<string, string[]>();
            foreach (string linea in File.ReadAllLines(base0))
            {
                System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(linea, @"^(.*?)\s+pix (\w+) bruto (\w+)");
                if (m.Success) antes[m.Groups[1].Value.Trim()] = new string[] { m.Groups[2].Value, m.Groups[3].Value };
            }
            Dictionary<string, Esc> todos = new Dictionary<string, Esc>();
            foreach (Esc e in Escenarios()) todos[e.Nombre] = e;
            Dictionary<string, Resultado> res = new Dictionary<string, Resultado>();
            foreach (string n in bateriaPantallas)
            {
                if (!todos.ContainsKey(n) || !antes.ContainsKey(n)) { Exige(false, n + ": falta en la lista o en el archivo de huellas"); continue; }
                Resultado r = HuellaDe(todos[n]); res[n] = r;
                Exige(r.Ok == Medidos, n + ": se presentan los " + Medidos + " cuadros");
                Exige(r.Pix == antes[n][0], n + ": los pixeles son los de antes de los ahorros (" + r.Pix + (r.Pix == antes[n][0] ? "" : " en vez de " + antes[n][0]) + ")");
                if (BrutoIgual(n)) Exige(r.Bruto == antes[n][1], n + ": ni una orden ni un vertice cambian (chorro bruto " + r.Bruto + ")");
            }
            // lo mismo dos veces da lo mismo: si no, las huellas no valdrian nada
            Resultado otra = HuellaDe(todos[bateriaPantallas[0]]);
            Exige(otra.Pix == res[bateriaPantallas[0]].Pix && otra.Bruto == res[bateriaPantallas[0]].Bruto, "una pantalla montada dos veces da las mismas huellas (el reloj y el azar son los de pruebas)");
            // los ahorros ahorran
            Resultado ar = res["1024x768 alto reposo est3D"], br = res["1024x768 bajo reposo est3D"], aj = res["1024x768 alto rejilla est3D"];
            Console.WriteLine("        alto reposo: RS {0:0.0} TSS {1:0.0} ordenes {2:0.0} pantallas {3:0.00} (max {4:0.00}); bajo reposo: pantallas {5:0.00}; alto rejilla: RS {6:0.0} TSS {7:0.0}",
                ar.RS, ar.TSS, ar.Ordenes, ar.Pantallas, ar.PantallasMax, br.Pantallas, aj.RS, aj.TSS);
            Exige(aj.RS <= 18.5 && aj.TSS <= 32.5, "alto rejilla: los estados de mezcla y de etapa solo se mandan cuando cambian (antes 24 y 40 por cuadro)");
            Exige(ar.Pantallas <= 5.9, "alto reposo: el cielo se guarda y se estira: " + ar.Pantallas.ToString("0.00") + " pantallas por cuadro de media (antes 6.45)");
            Exige(br.Pantallas <= 1.80, "bajo reposo: las franjas del cielo bajo el estado opaco no se mandan: " + br.Pantallas.ToString("0.00") + " pantallas (antes 1.98)");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine(bfallos == 0 ? "\n  OK huellacuadros" : "\n  FALLOS: " + bfallos);
        return bfallos;
    }

    const int Calentamiento = 48, Medidos = 6, Ventana = 8;   // se pinta (imitado) solo la ultima parte del calentamiento: lo anterior solo llena cachés
    sealed class Resultado
    {
        public string Pix, Bruto, Canon; public int Ok; public long SinArea, Transp;
        public double RS, TSS, Ordenes, Pantallas, PantallasMax;
    }
    static Resultado HuellaDe(Esc e)
    {
        PonCeroCuentas();
        pinta = false; hashea = false; anota = false; conSitios = false;
        Montaje m = Monta(e);
        contador = null;
        hBruto = FnvIni; hCanon = FnvIni; ultimoEstadoCanon = 0;
        ulong hp = FnvIni; int ok = 0;
        Pon(m.Panel, "CuentaArea", true);
        PropertyInfo pArea = TStar.GetProperty("AreaPx", TODO);
        double areaSuma = 0, areaMax = 0, pant = (double)e.W * e.H;
        // Calentamiento sin huella (llenan las caches); luego 'Medidos' cuadros seguidos con huella.
        for (int i = 0; i < Calentamiento + Medidos; i++)
        {
            pinta = hashea = i >= Calentamiento - Ventana;
            if (i == Calentamiento) { hBruto = FnvIni; hCanon = FnvIni; ultimoEstadoCanon = 0; trisTotal = trisSinArea = trisTransparentes = 0; PonCeroCuentas(); }
            bool listo = Cuadro(m, i);
            if (i >= Calentamiento)
            {
                if (listo) ok++;
                hp = Fnv(hp, hPixeles);
                double ar = (double)pArea.GetValue(m.Estrellas, null) / pant; areaSuma += ar; if (ar > areaMax) areaMax = ar;
            }
        }
        string vuelca = Environment.GetEnvironmentVariable("HUELLA_VUELCA");
        if (vuelca != null && pantalla != null)
        {
            // el ultimo cuadro, en bruto (ancho, alto, y los pixeles ARGB): para compararlo fuera
            using (BinaryWriter bw = new BinaryWriter(File.Create(vuelca + "_" + e.Nombre.Replace(' ', '_') + ".raw")))
            { bw.Write(pantalla.W); bw.Write(pantalla.H); for (int i = 0; i < pantalla.Px.Length; i++) bw.Write(pantalla.Px[i]); }
        }
        Resultado r = new Resultado();
        r.Pix = hp.ToString("x16"); r.Bruto = hBruto.ToString("x16"); r.Canon = hCanon.ToString("x16"); r.Ok = ok; r.SinArea = trisSinArea; r.Transp = trisTransparentes;
        r.RS = cSetRS / (double)Medidos; r.TSS = cSetTSS / (double)Medidos; r.Ordenes = cDraw / (double)Medidos; r.Pantallas = areaSuma / Medidos; r.PantallasMax = areaMax;
        Desmonta(m);
        return r;
    }
    static void Huella(Esc e)
    {
        Resultado r = HuellaDe(e);
        Console.WriteLine("{0,-34} pix {1} bruto {2} canon {3} cuadros {4}/{5} sinArea {6} transp {7} | RS {8:0.0} TSS {9:0.0} ord {10:0.0} pant {11:0.00} max {12:0.00}",
            e.Nombre, r.Pix, r.Bruto, r.Canon, r.Ok, Medidos, r.SinArea, r.Transp, r.RS, r.TSS, r.Ordenes, r.Pantallas, r.PantallasMax);
    }
    static void Coste(Esc e)
    {
        pinta = false; hashea = false; anota = false; conSitios = false;
        PonCeroCuentas();
        Montaje m = Monta(e);
        Func<bool> f = (Func<bool>)m.Cuadro;
        for (int i = 0; i < 150; i++) Cuadro(m, i);
        PonCeroCuentas();
        GC.Collect(); int g0 = GC.CollectionCount(0); long m0 = GC.GetTotalMemory(false);
        const int N = 480; double[] ms = new double[N]; Stopwatch sw = new Stopwatch();
        Pon(m.Panel, "CuentaArea", true);
        PropertyInfo pArea = TStar.GetProperty("AreaPx", TODO);
        double areaSuma = 0, areaMax = 0, pantalla1 = (double)e.W * e.H;
        int rc0 = Cuenta(m.Panel, "RecortesCalculados"), rr0 = Cuenta(m.Panel, "RecortesReusados"), cc0 = Cuenta(m.Panel, "CubiertasCalculadas"), cr0 = Cuenta(m.Panel, "CubiertasReusadas");
        for (int i = 0; i < N; i++)
        {
            cuadroN = 150 + i; PonReloj((150 + i) * 1000.0 / 60.0);
            if (animado) { float fase = ((150 + i) * 24 / 60) * .005f; PonSiHay(m.Panel, "NebFase", fase); PonSiHay(m.Panel, "HazFase", fase * .7f); }
            if (e.Reposo && (150 + i) % 30 == 0)
            {
                Bitmap viejo = actual; actual = PintaPanel(Estado(m, "EstadoOpaco"), ((150 + i) / 30) % 2 == 0, e.Textos);
                if (!ReferenceEquals(viejo, actual) && viejo != null) viejo.Dispose();
                Pon(m.Panel, "IdlePanelVersion", 2 + (150 + i) / 30); Pon(m.Panel, "IdlePanelDirty", Rectangle.Empty);
            }
            sw.Reset(); sw.Start(); f(); sw.Stop(); ms[i] = sw.Elapsed.TotalMilliseconds;
            double ar = (double)pArea.GetValue(m.Estrellas, null) / pantalla1; areaSuma += ar; if (ar > areaMax) areaMax = ar;
        }
        long m1 = GC.GetTotalMemory(false); int g1 = GC.CollectionCount(0);
        Console.WriteLine("        recortes de capas: {0:0.00} calculados y {1:0.00} reusados por cuadro; mallas de portadas: {2:0.00} y {3:0.00}",
            (Cuenta(m.Panel, "RecortesCalculados") - rc0) / (double)N, (Cuenta(m.Panel, "RecortesReusados") - rr0) / (double)N,
            (Cuenta(m.Panel, "CubiertasCalculadas") - cc0) / (double)N, (Cuenta(m.Panel, "CubiertasReusadas") - cr0) / (double)N);
        Array.Sort(ms); double s = 0; foreach (double x in ms) s += x;
        Console.WriteLine("{0,-34} p50 {1,6:0.000} p95 {2,6:0.000} p99 {3,6:0.000} max {4,7:0.000} media {5,6:0.000} ms | GC0 {6} mem {7:+0;-0;0} B/c | ord {8:0.0} tri {9:0} tx {10:0.0} | RS {11:0.0} TSS {12:0.0} samp {13:0.0} tex {14:0.0} fvf {15:0.0} lock {16:0.00} ({17:0} B) vert {18:0} B | pant {19:0.00} max {20:0.00}",
            e.Nombre, ms[N / 2], ms[(int)(N * .95)], ms[(int)(N * .99)], ms[N - 1], s / N, g1 - g0, (m1 - m0) / N,
            cDraw / (double)N, cTrisEnviados / (double)N, 0.0, cSetRS / (double)N, cSetTSS / (double)N, cSetSamp / (double)N, cSetTex / (double)N, cSetFvf / (double)N, cBloqueos / (double)N, cBytesBloqueo / (double)N, cBytesVertices / (double)N, areaSuma / N, areaMax);
        Desmonta(m);
    }
    // CPU del cuadro quitando de uno en uno cada pieza: dice cuanto cuesta cada una en el procesador (de ESTE equipo).
    static double MedianaCuadro(Esc e, int rondas)
    {
        pinta = false; hashea = false; anota = false;
        Montaje m = Monta(e);
        Func<bool> f = (Func<bool>)m.Cuadro;
        for (int i = 0; i < 200; i++) Cuadro(m, i);
        List<double> todos = new List<double>(); Stopwatch sw = new Stopwatch();
        for (int r = 0; r < rondas; r++)
            for (int i = 0; i < 300; i++)
            {
                int n = 200 + r * 300 + i;
                cuadroN = n; PonReloj(n * 1000.0 / 60.0);
                float fase = (n * 24 / 60) * .005f; PonSiHay(m.Panel, "NebFase", fase); PonSiHay(m.Panel, "HazFase", fase * .7f);
                if (e.Reposo && n % 30 == 0)
                {
                    Bitmap viejo = actual; actual = PintaPanel(Estado(m, "EstadoOpaco"), (n / 30) % 2 == 0, e.Textos);
                    if (!ReferenceEquals(viejo, actual) && viejo != null) viejo.Dispose();
                    Pon(m.Panel, "IdlePanelVersion", 2 + n / 30); Pon(m.Panel, "IdlePanelDirty", Rectangle.Empty);
                }
                sw.Reset(); sw.Start(); f(); sw.Stop(); todos.Add(sw.Elapsed.TotalMilliseconds);
            }
        Desmonta(m);
        todos.Sort();
        return todos[todos.Count / 2];
    }
    static void Componentes(Esc e)
    {
        double basePor = MedianaCuadro(e, 3);
        Console.WriteLine("{0}: p50 {1:0.000} ms con todo", e.Nombre, basePor);
        string[][] quita = {
            new string[] { "ConLuna" }, new string[] { "LunaActiva" }, new string[] { "Estrella3D" }, new string[] { "VinetaSuave" }, new string[] { "HaloSeleccion" },
            new string[] { "EstrellasFinas" }, new string[] { "OndasSuaves" }, new string[] { "NeonIntenso" }, new string[] { "CieloProfundo" }, new string[] { "AuroraBandas" },
            new string[] { "ControlesReposo" }, new string[] { "LunaRealista" }, new string[] { "HaloLunar" }, new string[] { "PolvoLunar" }, new string[] { "LunaProtagonista" },
            new string[] { "EstelaEstelar" }, new string[] { "CieloSuave" }, new string[] { "ColoresProfundos" }, new string[] { "AuroraLimpia" }, new string[] { "EstrellaDegradado" },
            new string[] { "EstrellaRefleja" }, new string[] { "EstrellaDestello" }, new string[] { "EstrellaBorde" }, new string[] { "EstrellaEspejo" }, new string[] { "PortadaBrillo" },
            new string[] { "LunaDelCielo" }, new string[] { "LluviasEstrellas" }, new string[] { "FugazFestejo" }, new string[] { "ProfundidadPagina" }, new string[] { "CieloPorHora" },
            new string[] { "ReflejoAurora" }, new string[] { "ColoresFluidos" }, new string[] { "NebulosasCine" }
        };
        foreach (string[] q in quita)
        {
            Esc v = e.Con(e.Nombre + " -" + q[0]); v.Apaga = q;
            double ms = MedianaCuadro(v, 3);
            Console.WriteLine("   sin {0,-20} {1,7:0.000} ms  ({2:+0.000;-0.000})", q[0], ms, ms - basePor);
        }
        Esc sa = e.Con(e.Nombre + " sin aurora"); sa.Aurora = false; double msa = MedianaCuadro(sa, 3);
        Console.WriteLine("   sin aurora (datos)        {0,7:0.000} ms  ({1:+0.000;-0.000})", msa, msa - basePor);
        Esc st = e.Con(e.Nombre + " sin textos"); st.Textos = false; double mst = MedianaCuadro(st, 3);
        Console.WriteLine("   sin textos largos          {0,7:0.000} ms  ({1:+0.000;-0.000})", mst, mst - basePor);
        Esc sp = e.Con(e.Nombre + " sin progreso"); sp.Progreso = false; double msp = MedianaCuadro(sp, 3);
        Console.WriteLine("   sin progreso               {0,7:0.000} ms  ({1:+0.000;-0.000})", msp, msp - basePor);
    }
    // El repintado del panel de estado extendido (GDI+), solo: mismo bitmap reutilizado, color y titulo que cambian como en una cancion nueva.
    static void RepintaPanel(Esc e)
    {
        pinta = false; hashea = false; anota = false;
        Montaje m = Monta(e);
        int total = Environment.GetEnvironmentVariable("HUELLA_CUADROS") != null ? int.Parse(Environment.GetEnvironmentVariable("HUELLA_CUADROS")) : 400;
        Stopwatch sw = new Stopwatch(); double[] ms = new double[total];
        for (int n = 0; n < total; n++)
        {
            sw.Reset(); sw.Start();
            Bitmap viejo = actual; actual = PintaPanel(false, n % 2 == 0, e.Textos);
            sw.Stop(); ms[n] = sw.Elapsed.TotalMilliseconds;
            if (!ReferenceEquals(viejo, actual) && viejo != null) viejo.Dispose();
        }
        Array.Sort(ms);
        Console.WriteLine("{0}: repintar el panel de estado: p50 {1:0.000} p95 {2:0.000} max {3:0.000} ms ({4} repintados)", e.Nombre, ms[total / 2], ms[(int)(total * .95)], ms[total - 1], total);
        Desmonta(m);
    }
    // Lo que cuesta, por vertice, oscurecer los vertices con la vineta punto por punto (G2).
    static void MicroVinetea(Esc e)
    {
        pinta = false; hashea = false; anota = false;
        Montaje m = Monta(e);
        for (int i = 0; i < 60; i++) Cuadro(m, i);
        object gpu = Toma(Toma(m.Panel, "stars"), "gpu");
        Type tv = TD3D.GetNestedType("Vtx", TODO);
        int tris = 2000, n = tris * 3;
        Array verts = Array.CreateInstance(tv, n);
        Random r = new Random(5);
        for (int i = 0; i < n; i++)
        {
            object v = Activator.CreateInstance(tv);
            tv.GetField("X").SetValue(v, (float)(r.NextDouble() * e.W)); tv.GetField("Y").SetValue(v, (float)(r.NextDouble() * e.H));
            tv.GetField("Color").SetValue(v, 0xC0000000u | (uint)r.Next(0xFFFFFF)); tv.GetField("Rhw").SetValue(v, 1f);
            verts.SetValue(v, i);
        }
        MethodInfo mi = Metodo(TDev, "Vinetea");
        Func<Array, int, object> f = delegate(Array a, int t) { return mi.Invoke(gpu, new object[] { a, t }); };
        for (int i = 0; i < 200; i++) f(verts, tris);
        Stopwatch sw = Stopwatch.StartNew(); int N = 3000;
        for (int i = 0; i < N; i++) f(verts, tris);
        Console.WriteLine("Vinetea: {0:0.0} us por llamada de {1} vertices ({2:0.0} ns por vertice)", sw.Elapsed.TotalMilliseconds * 1000 / N, n, sw.Elapsed.TotalMilliseconds * 1e6 / N / n);
        Desmonta(m);
    }
    // Lo que cuesta armar la malla de una portada de la rejilla (54 vertices) y lo que cuesta cuando ya esta guardada.
    static void MicroPortadas(Esc e)
    {
        pinta = false; hashea = false; anota = false;
        Montaje m = Monta(e);
        for (int i = 0; i < 60; i++) Cuadro(m, i);
        MethodInfo dibuja = Metodo(TPanel, "DrawNativeCover");
        Array celdas = (Array)Toma(m.Panel, "gridBuf");
        object celda0 = celdas.GetValue(0);
        FieldInfo guardadas = Campo(TPanel, "cubiertas");
        Stopwatch sw = new Stopwatch(); int N = 20000;
        double[] t = new double[2];
        for (int modoGuardado = 0; modoGuardado < 2; modoGuardado++)
        {
            for (int i = 0; i < 2000; i++) { PonerNoLista(guardadas, m.Panel, modoGuardado == 0); dibuja.Invoke(m.Panel, new object[] { 0, celda0, 0xFFFFFFFFu }); }
            sw.Reset(); sw.Start();
            for (int i = 0; i < N; i++) { PonerNoLista(guardadas, m.Panel, modoGuardado == 0); dibuja.Invoke(m.Panel, new object[] { 0, celda0, 0xFFFFFFFFu }); }
            sw.Stop(); t[modoGuardado] = sw.Elapsed.TotalMilliseconds * 1000 / N;
        }
        Console.WriteLine("portada de la rejilla: armada cada vez {0:0.00} us; guardada {1:0.00} us (incluye la llamada por reflexion y el envio imitado)", t[0], t[1]);
        Desmonta(m);
    }
    static void PonerNoLista(FieldInfo guardadas, object panel, bool invalida)
    {
        if (guardadas == null || !invalida) return;
        Array a = (Array)guardadas.GetValue(panel); object g = a.GetValue(0);
        if (g != null) Campo(g.GetType(), "Lista").SetValue(g, false);
    }
    // Muchos cuadros seguidos (para un perfilador de muestreo externo: mono --profile=log:sample).
    static void Largo(Esc e)
    {
        pinta = false; hashea = false; anota = false;
        Montaje m = Monta(e);
        Func<bool> f = (Func<bool>)m.Cuadro;
        int total = Environment.GetEnvironmentVariable("HUELLA_CUADROS") != null ? int.Parse(Environment.GetEnvironmentVariable("HUELLA_CUADROS")) : 20000;
        for (int n = 0; n < total; n++)
        {
            cuadroN = n; PonReloj(n * 1000.0 / 60.0);
            float fase = (n * 24 / 60) * .005f; PonSiHay(m.Panel, "NebFase", fase); PonSiHay(m.Panel, "HazFase", fase * .7f);
            if (e.Reposo && n % 30 == 0)
            {
                Bitmap viejo = actual; actual = PintaPanel(Estado(m, "EstadoOpaco"), (n / 30) % 2 == 0, e.Textos);
                if (!ReferenceEquals(viejo, actual) && viejo != null) viejo.Dispose();
                Pon(m.Panel, "IdlePanelVersion", 2 + n / 30); Pon(m.Panel, "IdlePanelDirty", Rectangle.Empty);
            }
            f();
        }
        Desmonta(m);
    }
    static void Detalle(Esc e)
    {
        pinta = false; hashea = false; anota = false; conSitios = false;
        PonCeroCuentas();
        Montaje m = Monta(e);
        for (int i = 0; i < Calentamiento; i++) Cuadro(m, i);
        // un cuadro de repaso: orden por orden. Se elige uno donde el cielo se repinta (el peor) y otro donde se reutiliza.
        nombres.Clear(); Nombra(m.Panel, ""); Nombra(m.Estrellas, "");
        Pon(Toma(m.Estrellas, "gpu"), "juntar", false);   // para atribuir cada orden a quien la pidio (en la rocola se juntan)
        foreach (int n in new int[] { Calentamiento, Calentamiento + 1 })
        {
            pinta = true; anota = true; conSitios = true;
            ordenes.Clear(); PonCeroCuentas(); tapadoPorOrden.Clear();
            contador = new int[e.W * e.H]; ordenDe = new long[e.W * e.H]; sobreEscritos = escritosTotal = 0;
            Cuadro(m, n);
            pinta = false; anota = false;
            double pant = (double)e.W * e.H; double suma = 0, sumaCambia = 0;
            try { Console.WriteLine("   (StatusTopY {0}, aurora BaseY {1}, alto {2})", TPanel.GetProperty("StatusTopY", TODO).GetValue(m.Panel, null), Toma(m.Panel, "aurDibujadaBase"), e.H); } catch { }
            Console.WriteLine("\n== {0}, cuadro {1}: {2} ordenes, RS {3} TSS {4} samp {5} tex {6} fvf {7} rt {8} lock {9} ({10} KB) crea {11}", e.Nombre, n, ordenes.Count, cSetRS, cSetTSS, cSetSamp, cSetTex, cSetFvf, cSetRt, cBloqueos, cBytesBloqueo / 1024, cCreaTex);
            int idx = 0;
            foreach (Orden o in ordenes)
            {
                long tapado; tapadoPorOrden.TryGetValue(idx, out tapado);
                suma += o.Area / pant; sumaCambia += o.Cambian / pant;
                Console.WriteLine("  {0,3} {1,5} tri {2,6:0.000} pant {3,6:0.000} cambia {4,6:0.000} tapado | {5,-9} {6,-46} {7} {8}", idx, o.Tris, o.Area / pant, o.Cambian / pant, tapado / pant, o.Destino, o.Estado, o.Sitio, (o.SinArea + o.Transparentes > 0 ? "[sin area " + o.SinArea + ", transp " + o.Transparentes + "]" : ""));
                idx++;
            }
            Console.WriteLine("  total: {0:0.00} pantallas pintadas, {1:0.00} cambian algun pixel, {2:0.00} tapadas despues por una opaca", suma, sumaCambia, sobreEscritos / pant);
        }
        Desmonta(m);
    }
}
