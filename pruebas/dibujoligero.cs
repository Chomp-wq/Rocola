using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Forms;

// EL DIBUJO MAS LIGERO (Rocola/DibujoLigeroGpu.cs): LA MISMA PANTALLA CON MENOS TRABAJO PARA LA TARJETA.
//
// Tres ahorros, y de los tres se comprueba lo mismo: que lo que se ve no cambia.
//
//  1) SIN BORRAR LA PANTALLA. Cada cuadro empezaba borrandola entera, y lo primero que se dibuja despues es el
//     cielo, opaco, que la tapa entera. Aqui se arranca un cuadro de verdad con una tarjeta IMITADA y se mira que
//     no se borra, que lo primero que le llega es el cielo, sin mezcla, y que sus cuatro triangulos cubren cada
//     pixel UNA vez (ni huecos ni repetidos), en varios tamanos de pantalla y cortes del cielo.
//
//  2) SIN PINTAR LO TRANSPARENTE. La via lactea, la vineta y la interfaz se dibujan a trozos: solo los que tienen
//     algo. Se comprueba la regla (una tesela se salta solo si ella y el texel de alrededor son transparentes del
//     todo; ninguna se pinta dos veces; las del borde, siempre, si la imagen se estira) con imagenes hechas a
//     proposito y con las de verdad. Y se pinta: lo que el programa manda a la tarjeta, entero y a trozos, pasa por
//     un raster de referencia -las reglas de Direct3D 9 que usa la rocola: centros de pixel, regla de arriba a la
//     izquierda, bilineal con envoltura, textura por color y mezcla normal- sobre un fondo al azar. La interfaz,
//     que va texel a pixel, tiene que salir IDENTICA. Lo estirado puede diferir en el redondeo de algun pixel -un
//     nivel de 255 como mucho, en muy pocos- porque los vertices intermedios caen en decimales.
//
//  3) LA AURORA SIN REPETIR CUENTAS. El color de partida de cada franja se guarda y se rehace solo cuando cambia lo
//     que lo decide. Dos paneles iguales, uno con lo guardado y otro obligado a rehacerlo en cada cuadro, tienen que
//     mandar los mismos vertices byte a byte. Y lo guardado se tiene que rehacer al cambiar CADA cosa de la que
//     depende, y solo entonces.
//
// Y que las cuentas de color hechas por entero (en vez de "sin signo", que es una conversion lenta) den los mismos
// numeros de antes.
//
// La tarjeta es imitada: esto no mide velocidad ni promete lo que haga una tarjeta concreta. Dice que el programa le
// pide lo mismo, con menos pixeles. Solo existe con tarjeta grafica; el dibujo por procesador no se toca.
class DibujoLigero
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos;
    static void Bien(string q) { Console.WriteLine("  OK    " + q); }
    static void Mal(string q) { Console.WriteLine("  FALLO " + q); fallos++; }
    static void Exige(bool c, string q) { if (c) Bien(q); else Mal(q); }

    static Type TPanel, TStar, TDev, TTes, TVtx, TAu, TForm, TFlujo, TD3D;

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

    // ======================= LA TARJETA IMITADA: apunta todo lo que se le manda pintar =======================
    static uint Nada(IntPtr self) { return 0; }
    static uint Memoria(IntPtr self) { return 256u * 1024 * 1024; }
    // Un puntero a funcion "de COM" hecho con el tipo de delegado de la propia rocola (asi lo reconoce al leerlo).
    static IntPtr Puntero(string tipo, string metodo)
    {
        Delegate del = Delegate.CreateDelegate(TD3D.GetNestedType(tipo, TODO), typeof(DibujoLigero).GetMethod(metodo, TODO));
        vivos.Add(del); return Marshal.GetFunctionPointerForDelegate(del);
    }

    // LAS TEXTURAS IMITADAS: se crean, se bloquean para escribirlas (subir una imagen) y se sueltan. El bloqueo devuelve
    // una estructura de la rocola por referencia, que no se puede escribir con un metodo normal de aqui: se arma uno
    // al vuelo con el tipo exacto, que solo pasa la direccion a Bloquea.
    sealed class TexImitada { public int W, H; public IntPtr Mem; }
    static readonly Dictionary<long, TexImitada> creadas = new Dictionary<long, TexImitada>();
    static IntPtr vtTex; static int offPitch, offBits;
    static void PreparaTexturas()
    {
        if (vtTex != IntPtr.Zero) return;
        Type tlr = TD3D.GetNestedType("LockedRect", TODO);
        offPitch = (int)Marshal.OffsetOf(tlr, "Pitch"); offBits = (int)Marshal.OffsetOf(tlr, "Bits");
        Type tLock = TD3D.GetNestedType("LockRectDel", TODO);
        ParameterInfo[] ps = tLock.GetMethod("Invoke").GetParameters();
        Type[] pt = new Type[ps.Length]; for (int i = 0; i < ps.Length; i++) pt[i] = ps[i].ParameterType;
        DynamicMethod dm = new DynamicMethod("BloqueaImitada", typeof(int), pt, typeof(DibujoLigero).Module, true);
        ILGenerator il = dm.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Conv_U);
        il.Emit(OpCodes.Call, typeof(DibujoLigero).GetMethod("Bloquea", TODO));
        il.Emit(OpCodes.Ret);
        Delegate lk = dm.CreateDelegate(tLock); vivos.Add(lk);
        vtTex = Marshal.AllocHGlobal(64 * IntPtr.Size);
        for (int i = 0; i < 64; i++) Marshal.WriteIntPtr(vtTex, i * IntPtr.Size, Puntero("UintSelf", "Nada"));
        Marshal.WriteIntPtr(vtTex, 2 * IntPtr.Size, Puntero("UintSelf", "Suelta"));
        Marshal.WriteIntPtr(vtTex, 19 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(lk));
        Marshal.WriteIntPtr(vtTex, 20 * IntPtr.Size, Puntero("UnlockRectDel", "Desbloquea"));
    }
    static int CreaTex(IntPtr self, uint w, uint h, uint levels, uint usage, uint format, uint pool, out IntPtr tex, IntPtr shared)
    {
        PreparaTexturas();
        IntPtr o = Marshal.AllocHGlobal(16); Marshal.WriteIntPtr(o, vtTex);
        TexImitada t = new TexImitada(); t.W = (int)w; t.H = (int)h; t.Mem = Marshal.AllocHGlobal((int)(w * h * 4));
        byte[] b = new byte[w * h * 4]; new Random((int)(w * 31 + h)).NextBytes(b);   // basura: lo que no se sube no puede parecer transparente
        Marshal.Copy(b, 0, t.Mem, b.Length);
        creadas[o.ToInt64()] = t; tex = o;
        return 0;
    }
    static int Bloquea(IntPtr self, IntPtr lr) { TexImitada t = creadas[self.ToInt64()]; Marshal.WriteInt32(lr, offPitch, t.W * 4); Marshal.WriteIntPtr(lr, offBits, t.Mem); return 0; }
    static int Desbloquea(IntPtr self, uint level) { return 0; }
    static uint Suelta(IntPtr self) { TexImitada t; if (creadas.TryGetValue(self.ToInt64(), out t)) { creadas.Remove(self.ToInt64()); Marshal.FreeHGlobal(t.Mem); } return 0; }
    // Lo que tiene una textura imitada, para el raster de referencia.
    static Textura DeImitada(IntPtr tex)
    {
        TexImitada t; if (!creadas.TryGetValue(tex.ToInt64(), out t)) return null;
        Textura r = new Textura(); r.W = t.W; r.H = t.H; r.Px = new int[t.W * t.H];
        Marshal.Copy(t.Mem, r.Px, 0, r.Px.Length);
        return r;
    }
    static readonly List<object> vivos = new List<object>();   // que el recolector no se lleve los punteros
    sealed class Envio { public int Tris; public byte[] Datos; public bool Mezcla, Suma, ConTextura; public IntPtr Tex; }
    static readonly List<Envio> envios = new List<Envio>();
    // El estado de cada tarjeta imitada (cada una recuerda lo suyo, como una de verdad).
    sealed class Estado { public bool Mezcla; public uint Destino = 6, OpColor = 4; public IntPtr Tex; }
    static readonly Dictionary<long, Estado> estados = new Dictionary<long, Estado>();
    static int borrados;
    static Estado EstadoDe(IntPtr self) { Estado e; if (!estados.TryGetValue(self.ToInt64(), out e)) { e = new Estado(); estados[self.ToInt64()] = e; } return e; }

    static int DrawUp(IntPtr self, uint prim, uint count, IntPtr data, uint stride)
    {
        Estado st = EstadoDe(self);
        Envio e = new Envio(); e.Tris = (int)count; e.Datos = new byte[count * 3 * stride];
        Marshal.Copy(data, e.Datos, 0, e.Datos.Length);
        e.Mezcla = st.Mezcla; e.Suma = st.Destino == 2; e.ConTextura = st.OpColor == 4; e.Tex = st.Tex;
        envios.Add(e);
        return 0;
    }
    static int SetRS(IntPtr self, uint a, uint b) { Estado st = EstadoDe(self); if (a == 27) st.Mezcla = b != 0; else if (a == 20) st.Destino = b; return 0; }
    static int SetU1(IntPtr self, uint a) { return 0; }
    static int SetTSS(IntPtr self, uint stage, uint type, uint val) { if (type == 1) EstadoDe(self).OpColor = val; return 0; }
    static int SetSamp(IntPtr self, uint stage, uint type, uint val) { return 0; }
    static int SetTex(IntPtr self, uint stage, IntPtr t) { EstadoDe(self).Tex = t; return 0; }
    static int Hr(IntPtr self) { return 0; }
    static int Present(IntPtr self, IntPtr a, IntPtr b, IntPtr c, IntPtr d) { return 0; }
    static int Clear(IntPtr self, uint count, IntPtr rects, uint flags, uint color, float z, uint stencil) { borrados++; return 0; }

    static void Enchufa(object d, string campo, string metodo)
    {
        FieldInfo f = Campo(TDev, campo);
        if (f == null) throw new Exception("el dispositivo no tiene " + campo);
        Delegate del = Delegate.CreateDelegate(f.FieldType, typeof(DibujoLigero).GetMethod(metodo, TODO));
        vivos.Add(del); f.SetValue(d, del);
    }
    static object Tarjeta(int w, int h)
    {
        PreparaTexturas();
        IntPtr vt = Marshal.AllocHGlobal(128 * IntPtr.Size);
        for (int i = 0; i < 128; i++) Marshal.WriteIntPtr(vt, i * IntPtr.Size, Puntero("UintSelf", "Nada"));
        Marshal.WriteIntPtr(vt, 4 * IntPtr.Size, Puntero("UintSelf", "Memoria"));   // la memoria de texturas que le queda
        IntPtr obj = Marshal.AllocHGlobal(16); Marshal.WriteIntPtr(obj, vt);
        object d = FormatterServices.GetUninitializedObject(TDev);
        Pon(d, "dev", obj);
        Pon(d, "stTex", new IntPtr(-1));
        object pp = Activator.CreateInstance(TD3D.GetNestedType("PresentParams", TODO));   // el tamano del buffer: asi no se "redimensiona"
        pp.GetType().GetField("BackBufferWidth").SetValue(pp, (uint)w); pp.GetType().GetField("BackBufferHeight").SetValue(pp, (uint)h);
        Pon(d, "pp", pp);
        Enchufa(d, "createTex", "CreaTex");
        Enchufa(d, "setRS", "SetRS"); Enchufa(d, "setFVF", "SetU1"); Enchufa(d, "setTSS", "SetTSS"); Enchufa(d, "setSamp", "SetSamp");
        Enchufa(d, "setTex", "SetTex"); Enchufa(d, "drawUp", "DrawUp"); Enchufa(d, "present", "Present");
        Pon(d, "verticesSinSoporte", true); // doble UP; el anillo real se comprueba en verticesgpu
        Enchufa(d, "begin", "Hr"); Enchufa(d, "end", "Hr"); Enchufa(d, "testCoop", "Hr");
        if (Campo(TDev, "clear") != null) Enchufa(d, "clear", "Clear");   // el de antes todavia borraba: que se vea
        // Lo que en Modest se crea en la declaracion (un objeto hecho sin constructor no lo tiene).
        string[] arr = { "cubeFaceV", "cubeBatchV", "cbx", "cbz", "cbo" }; int[] lar = { 6, 24, 4, 4, 4 };
        for (int i = 0; i < arr.Length; i++)
        {
            FieldInfo f = Campo(TDev, arr[i]);
            if (f != null && f.GetValue(d) == null) f.SetValue(d, Array.CreateInstance(f.FieldType.GetElementType(), lar[i]));
        }
        if (!(bool)Llama(d, "InitStates")) throw new Exception("la tarjeta imitada no arranca");
        return d;
    }
    static object Cielo(object tarjeta, int w, int h)
    {
        object sf = Activator.CreateInstance(TStar, TODO, null, new object[] { IntPtr.Zero, w, h, 4321 }, null);   // sin Direct3D: se queda sin dispositivo
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

    // ======================= LAS TEXTURAS, COMO LAS SUBE LA ROCOLA =======================
    // Potencia de dos, el bitmap arriba a la izquierda, leido como ARGB. Lo que sobra alrededor NO se sube: en la
    // tarjeta es lo que hubiera en esa memoria, asi que aqui se rellena de basura a proposito.
    sealed class Textura { public int W, H; public int[] Px; }
    static readonly Dictionary<long, Textura> texturas = new Dictionary<long, Textura>();
    static IntPtr Registra(Textura t) { IntPtr p = new IntPtr(0x70000 + texturas.Count * 64); texturas[p.ToInt64()] = t; return p; }
    static int Pow2(int n) { int p = 1; while (p < n) p <<= 1; return p; }
    static int[] Pixeles(Bitmap b)
    {
        int w = b.Width, h = b.Height; int[] px = new int[w * h];
        BitmapData bd = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try { for (int y = 0; y < h; y++) Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), px, y * w, w); }
        finally { b.UnlockBits(bd); }
        return px;
    }
    static Textura DeBitmap(Bitmap b, Random basura)
    {
        int w = b.Width, h = b.Height;
        Textura t = new Textura(); t.W = Pow2(w); t.H = Pow2(h); t.Px = new int[t.W * t.H];
        for (int i = 0; i < t.Px.Length; i++) t.Px[i] = basura.Next() ^ (basura.Next() << 16);
        int[] px = Pixeles(b);
        for (int y = 0; y < h; y++) Array.Copy(px, y * w, t.Px, y * t.W, w);
        return t;
    }

    // ======================= EL RASTER DE REFERENCIA =======================
    // Las reglas de Direct3D 9 en lo que usa la rocola: centros de pixel en coordenadas enteras, regla de arriba a la
    // izquierda, color y coordenadas de textura interpolados (rhw = 1), bilineal con envoltura, textura por color y
    // las tres mezclas con 8 bits. No es una tarjeta: sirve para comparar dos formas de pedir lo mismo.
    static double Ef(double ax, double ay, double bx, double by, double px, double py) { return (bx - ax) * (py - ay) - (by - ay) * (px - ax); }
    static bool ArribaIzq(double ax, double ay, double bx, double by) { double dx = bx - ax, dy = by - ay; return dy < 0 || (dy == 0 && dx > 0); }
    static float Fx(byte[] d, int v, int campo) { return BitConverter.ToSingle(d, v * 28 + campo); }
    static uint Col(byte[] d, int v) { return BitConverter.ToUInt32(d, v * 28 + 16); }
    static double Ch(uint c, int s) { return (c >> s) & 255; }
    static uint Q(double v) { int i = (int)Math.Floor(v + 0.5); return (uint)(i < 0 ? 0 : (i > 255 ? 255 : i)); }

    // 'veces' != null: solo cuenta que pixeles tapa (para el cielo). Si no, pinta en fb.
    static void Triangulo(byte[] d, int t0, Textura tex, bool mezcla, bool suma, int[] fb, int[] veces, int W, int H)
    {
        int i0 = t0, i1 = t0 + 1, i2 = t0 + 2;
        double x0 = Fx(d, i0, 0), y0 = Fx(d, i0, 4), x1 = Fx(d, i1, 0), y1 = Fx(d, i1, 4), x2 = Fx(d, i2, 0), y2 = Fx(d, i2, 4);
        double area = Ef(x0, y0, x1, y1, x2, y2);
        if (area == 0) return;
        if (area < 0) { double tx = x1, ty = y1; x1 = x2; y1 = y2; x2 = tx; y2 = ty; int ti = i1; i1 = i2; i2 = ti; area = -area; }
        int minx = Math.Max(0, (int)Math.Ceiling(Math.Min(x0, Math.Min(x1, x2)))), maxx = Math.Min(W - 1, (int)Math.Floor(Math.Max(x0, Math.Max(x1, x2))));
        int miny = Math.Max(0, (int)Math.Ceiling(Math.Min(y0, Math.Min(y1, y2)))), maxy = Math.Min(H - 1, (int)Math.Floor(Math.Max(y0, Math.Max(y1, y2))));
        bool tl0 = ArribaIzq(x1, y1, x2, y2), tl1 = ArribaIzq(x2, y2, x0, y0), tl2 = ArribaIzq(x0, y0, x1, y1);
        uint ca0 = Col(d, i0), ca1 = Col(d, i1), ca2 = Col(d, i2);
        double u0 = Fx(d, i0, 20), v0 = Fx(d, i0, 24), u1 = Fx(d, i1, 20), v1 = Fx(d, i1, 24), u2 = Fx(d, i2, 20), v2 = Fx(d, i2, 24);
        for (int py = miny; py <= maxy; py++)
            for (int px = minx; px <= maxx; px++)
            {
                double w0 = Ef(x1, y1, x2, y2, px, py), w1 = Ef(x2, y2, x0, y0, px, py), w2 = Ef(x0, y0, x1, y1, px, py);
                if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                if ((w0 == 0 && !tl0) || (w1 == 0 && !tl1) || (w2 == 0 && !tl2)) continue;
                int o = py * W + px;
                if (veces != null) { veces[o]++; continue; }
                w0 /= area; w1 /= area; w2 /= area;
                double ca = Ch(ca0, 24) * w0 + Ch(ca1, 24) * w1 + Ch(ca2, 24) * w2;
                double cr = Ch(ca0, 16) * w0 + Ch(ca1, 16) * w1 + Ch(ca2, 16) * w2;
                double cg = Ch(ca0, 8) * w0 + Ch(ca1, 8) * w1 + Ch(ca2, 8) * w2;
                double cb = Ch(ca0, 0) * w0 + Ch(ca1, 0) * w1 + Ch(ca2, 0) * w2;
                if (tex != null)
                {
                    double ta, tr, tg, tb; Muestra(tex, u0 * w0 + u1 * w1 + u2 * w2, v0 * w0 + v1 * w1 + v2 * w2, out ta, out tr, out tg, out tb);
                    ca = ca * ta / 255.0; cr = cr * tr / 255.0; cg = cg * tg / 255.0; cb = cb * tb / 255.0;
                }
                uint dd = (uint)fb[o];
                double dr = (dd >> 16) & 255, dg = (dd >> 8) & 255, db = dd & 255, al = ca / 255.0;
                if (!mezcla) { dr = cr; dg = cg; db = cb; }
                else if (suma) { dr += cr * al; dg += cg * al; db += cb * al; }
                else { dr = dr * (1 - al) + cr * al; dg = dg * (1 - al) + cg * al; db = db * (1 - al) + cb * al; }
                fb[o] = unchecked((int)(0xFF000000u | (Q(dr) << 16) | (Q(dg) << 8) | Q(db)));
            }
    }
    static void Muestra(Textura t, double u, double v, out double a, out double r, out double g, out double b)
    {
        double fu = u * t.W - 0.5, fv = v * t.H - 0.5;
        int xa = (int)Math.Floor(fu), ya = (int)Math.Floor(fv); double fx = fu - xa, fy = fv - ya;
        a = r = g = b = 0;
        for (int k = 0; k < 4; k++)
        {
            double wgt = ((k & 1) != 0 ? fx : 1 - fx) * ((k >> 1) != 0 ? fy : 1 - fy);
            if (wgt == 0) continue;
            int xx = xa + (k & 1), yy = ya + (k >> 1);
            xx = ((xx % t.W) + t.W) % t.W; yy = ((yy % t.H) + t.H) % t.H;
            uint p = (uint)t.Px[yy * t.W + xx];
            a += wgt * ((p >> 24) & 255); r += wgt * ((p >> 16) & 255); g += wgt * ((p >> 8) & 255); b += wgt * (p & 255);
        }
    }
    static void Pinta(List<Envio> es, int[] fb, int W, int H)
    {
        foreach (Envio e in es)
        {
            Textura t = null;
            if (e.ConTextura && !texturas.TryGetValue(e.Tex.ToInt64(), out t) && (t = DeImitada(e.Tex)) == null) throw new Exception("textura desconocida");
            for (int i = 0; i < e.Tris; i++) Triangulo(e.Datos, i * 3, t, e.Mezcla, e.Suma, fb, null, W, H);
        }
    }
    static int[] Fondo(int W, int H, int semilla)
    {
        Random r = new Random(semilla); int[] fb = new int[W * H];
        for (int i = 0; i < fb.Length; i++) fb[i] = unchecked((int)(0xFF000000u | (uint)r.Next(0x1000000)));
        return fb;
    }
    static int Compara(int[] a, int[] b, out int maxd)
    {
        int n = 0; maxd = 0;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i])
            {
                n++;
                for (int s = 0; s < 24; s += 8) { int dd = Math.Abs(((a[i] >> s) & 255) - ((b[i] >> s) & 255)); if (dd > maxd) maxd = dd; }
            }
        return n;
    }

    // ======================= LAS TESELAS: LA REGLA =======================
    static object De(Bitmap b, int lado, bool borde) { return Estatico(TTes, "De", b, lado, borde); }
    // Devuelve null si la respuesta de De cumple la regla; si no, que falla.
    static string Revisa(Bitmap b, int lado, bool borde, object t)
    {
        if (t == null) return "no hay respuesta";
        int w = b.Width, h = b.Height; int[] px = Pixeles(b);
        if ((int)Toma(t, "Ancho") != w || (int)Toma(t, "Alto") != h) return "no dice bien el tamano";
        int n = (int)Toma(t, "N"); int[] r = (int[])Toma(t, "Rect"); Array vtx = (Array)Toma(t, "Vtx");
        if (r.Length < n * 4) return "faltan rectangulos";
        if (vtx.Length < n * 6) return "faltan vertices";
        int tx = (w + lado - 1) / lado, ty = (h + lado - 1) / lado;
        int[] veces = new int[tx * ty]; long area = 0;
        for (int i = 0; i < n; i++)
        {
            int x0 = r[i * 4], y0 = r[i * 4 + 1], x1 = r[i * 4 + 2], y1 = r[i * 4 + 3];
            if (x0 < 0 || y0 < 0 || x1 > w || y1 > h || x0 >= x1 || y0 >= y1) return "un rectangulo se sale o esta vacio";
            if (x0 % lado != 0 || y0 % lado != 0 || (x1 != w && x1 % lado != 0) || (y1 != h && y1 % lado != 0)) return "un rectangulo no va por los bordes de las teselas";
            area += (long)(x1 - x0) * (y1 - y0);
            for (int j = y0 / lado; j < (y1 + lado - 1) / lado; j++) for (int k = x0 / lado; k < (x1 + lado - 1) / lado; k++) veces[j * tx + k]++;
        }
        for (int j = 0; j < ty; j++)
            for (int k = 0; k < tx; k++)
            {
                bool hace = borde && (k == 0 || j == 0 || k == tx - 1 || j == ty - 1);
                int xa = Math.Max(0, k * lado - 1), xb = Math.Min(w, (k + 1) * lado + 1), ya = Math.Max(0, j * lado - 1), yb = Math.Min(h, (j + 1) * lado + 1);
                for (int y = ya; y < yb && !hace; y++) for (int x = xa; x < xb; x++) if (((uint)px[y * w + x] >> 24) != 0) { hace = true; break; }
                int v = veces[j * tx + k];
                if (v > 1) return "la tesela " + k + "," + j + " se pinta " + v + " veces";
                if (hace && v == 0) return "se salta la tesela " + k + "," + j + ", que tiene algo (ella o a un texel)";
                if (!hace && v > 0) return "pinta la tesela " + k + "," + j + ", que esta vacia";
            }
        double frac = (double)Toma(t, "Fraccion");
        if (Math.Abs(frac - (double)area / ((double)w * h)) > 1e-9) return "la fraccion que dice no es la que pinta";
        return null;
    }
    static Bitmap Imagen(int w, int h, int semilla, double densidad, int alfaMin)
    {
        Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        Random r = new Random(semilla);
        int[] px = new int[w * h];
        for (int i = 0; i < px.Length; i++)
            px[i] = r.NextDouble() < densidad ? unchecked((int)(((uint)(alfaMin + r.Next(256 - alfaMin)) << 24) | (uint)r.Next(0x1000000)))
                                               : unchecked((int)(uint)r.Next(0x1000000));   // alfa 0 con color: sigue sin pintar nada
        BitmapData bd = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < h; y++) Marshal.Copy(px, y * w, new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), w);
        b.UnlockBits(bd);
        return b;
    }
    static void Punto(Bitmap b, int x, int y, int alfa) { if (x >= 0 && y >= 0 && x < b.Width && y < b.Height) b.SetPixel(x, y, Color.FromArgb(alfa, 200, 100, 50)); }

    static void Regla()
    {
        Console.WriteLine("\n  2) SIN PINTAR LO TRANSPARENTE: LA REGLA DE LAS TESELAS\n");
        int[][] tam = { new int[] { 1, 1 }, new int[] { 3, 5 }, new int[] { 4, 4 }, new int[] { 5, 4 }, new int[] { 16, 16 }, new int[] { 17, 33 },
                        new int[] { 64, 48 }, new int[] { 100, 37 }, new int[] { 255, 193 }, new int[] { 256, 192 } };
        double[] dens = { 0, 0.0005, 0.004, 0.03, 0.3, 1 };
        int casos = 0, malos = 0; string primero = null;
        for (int a = 0; a < tam.Length; a++)
            for (int d = 0; d < dens.Length; d++)
                for (int lado = 4; lado <= 16; lado += 12)
                    for (int bi = 0; bi < 2; bi++)
                    {
                        using (Bitmap b = Imagen(tam[a][0], tam[a][1], 1000 * a + 10 * d + lado + bi, dens[d], 1))
                        {
                            string q = Revisa(b, lado, bi == 1, De(b, lado, bi == 1));
                            casos++; if (q != null) { malos++; if (primero == null) primero = tam[a][0] + "x" + tam[a][1] + ", lado " + lado + (bi == 1 ? ", con borde" : "") + ": " + q; }
                        }
                    }
        // Un texel suelto en cada sitio delicado: esquinas, y a uno y dos texeles de la frontera entre teselas.
        foreach (int lado in new int[] { 4, 16 })
            foreach (int w in new int[] { 3 * lado, 3 * lado + 1, 3 * lado - 1 })
            {
                int[] sitios = { 0, 1, lado - 2, lado - 1, lado, lado + 1, 2 * lado - 1, 2 * lado, w - 2, w - 1 };
                foreach (int sx in sitios)
                    foreach (int sy in sitios)
                        for (int bi = 0; bi < 2; bi++)
                            using (Bitmap b = new Bitmap(w, w, PixelFormat.Format32bppArgb))
                            {
                                Punto(b, sx, sy, 1);
                                string q = Revisa(b, lado, bi == 1, De(b, lado, bi == 1));
                                casos++; if (q != null) { malos++; if (primero == null) primero = "texel suelto en " + sx + "," + sy + " de " + w + "x" + w + ", lado " + lado + ": " + q; }
                            }
            }
        Exige(malos == 0, casos + " imagenes: ninguna tesela con algo (ni a un texel) se salta, ninguna vacia se pinta, ninguna se pinta dos veces" +
                          (primero != null ? " -- " + malos + " mal; la primera: " + primero : ""));

        // Las de verdad: la via lactea a varios tamanos de pantalla y la vineta.
        int[][] pant = { new int[] { 640, 480 }, new int[] { 1024, 768 }, new int[] { 1366, 768 }, new int[] { 1920, 1080 } };
        int lt = (int)Campo(TTes, "LadoTesela").GetValue(null);
        foreach (int[] p in pant)
        {
            int mw = Math.Max(8, p[0] / 4), mh = Math.Max(8, p[1] / 4);
            using (Bitmap hz = (Bitmap)Estatico(TStar, "BakeViaLactea", mw, mh))
            {
                object t = De(hz, lt, true); string q = Revisa(hz, lt, true, t);
                Exige(q == null, "via lactea de " + p[0] + "x" + p[1] + " (" + mw + "x" + mh + " texeles): cumple la regla" + (q != null ? " -- " + q : "") +
                                 "; pinta el " + Pct((double)Toma(t, "Fraccion")) + " de su imagen en " + Toma(t, "N") + " rectangulos");
            }
        }
        using (Bitmap vm = (Bitmap)Estatico(TPanel, "BakeVineta", 128))
        {
            object t = De(vm, lt, true); string q = Revisa(vm, lt, true, t);
            Exige(q == null, "vineta: cumple la regla" + (q != null ? " -- " + q : "") + "; pinta el " + Pct((double)Toma(t, "Fraccion")) + " de su imagen");
            Exige((double)Toma(t, "Fraccion") < 0.95, "y deja sin pintar su centro transparente");
        }
        // Juntar: una imagen llena es UN rectangulo; una columna, uno; nada, ninguno (o solo el borde si se pide).
        using (Bitmap b = Imagen(64, 48, 7, 1, 1))
            Exige((int)Toma(De(b, 4, false), "N") == 1, "una imagen sin huecos se pinta en un solo rectangulo (las tiras se juntan a lo alto)");
        using (Bitmap b = new Bitmap(64, 64, PixelFormat.Format32bppArgb))
        {
            for (int y = 0; y < 64; y++) Punto(b, 30, y, 255);
            Exige((int)Toma(De(b, 16, false), "N") == 1, "una columna con algo de arriba abajo: un rectangulo");
        }
        using (Bitmap b = new Bitmap(64, 64, PixelFormat.Format32bppArgb))
        {
            object t0 = De(b, 16, false), t1 = De(b, 16, true);
            Exige((int)Toma(t0, "N") == 0, "una imagen transparente del todo no pinta nada");
            Exige((double)Toma(t1, "Fraccion") > 0 && Revisa(b, 16, true, t1) == null, "y si se va a estirar, pinta solo su borde");
        }
    }
    static string Pct(double f) { return (100 * f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " %"; }

    // ======================= LA MISMA IMAGEN: ENTERA Y A TROZOS =======================
    static List<Envio> Captura() { List<Envio> r = new List<Envio>(envios); envios.Clear(); return r; }
    // Pinta lo 'entero' y lo 'a trozos' sobre el mismo fondo al azar y los compara.
    static void Iguales(string que, List<Envio> entero, List<Envio> trozos, int W, int H, int semilla, int maxPixeles, int maxNivel, object tes)
    {
        int n = (int)Toma(tes, "N");
        bool unaLlamada = trozos.Count == (n > 0 ? 1 : 0) && entero.Count == 1;
        bool mismoEstado = unaLlamada && (n == 0 || (trozos[0].Tex == entero[0].Tex && trozos[0].Mezcla == entero[0].Mezcla && trozos[0].Suma == entero[0].Suma
                                                   && trozos[0].ConTextura == entero[0].ConTextura && trozos[0].Tris == n * 2));
        int[] fa = Fondo(W, H, semilla), fb = (int[])fa.Clone();
        Pinta(entero, fa, W, H); Pinta(trozos, fb, W, H);
        int maxd; int dist = Compara(fa, fb, out maxd);
        int[] f0 = Fondo(W, H, semilla); int cambia = 0; for (int i = 0; i < f0.Length; i++) if (f0[i] != fa[i]) cambia++;
        Exige(mismoEstado && dist <= maxPixeles && maxd <= maxNivel,
              que + ": " + (unaLlamada ? "una llamada de " + (n * 2) + " triangulos, misma textura y mezcla" : "LLAMADAS DE MAS O DE MENOS (" + trozos.Count + ")") +
              "; pixeles distintos " + dist + " de " + (W * H) + (dist > 0 ? " (el peor por " + maxd + " de 255)" : "") + "; lo pintado cambia " + cambia + " pixeles");
    }
    static void MismaImagen()
    {
        Console.WriteLine("\n  3) SIN PINTAR LO TRANSPARENTE: LA MISMA IMAGEN, ENTERA Y A TROZOS\n");
        Random basura = new Random(99);
        int lt = (int)Campo(TTes, "LadoTesela").GetValue(null), lui = (int)Campo(TPanel, "LadoTeselaUi").GetValue(null);
        Array quad = Array.CreateInstance(TVtx, 6);
        // --- la via lactea, por el camino de verdad (DibujaNieblas), con la deriva en varias posiciones ---
        foreach (int[] p in new int[][] { new int[] { 1024, 768 }, new int[] { 1366, 768 } })
        {
            int W = p[0], H = p[1];
            Control panel = Panel(W, H, Cielo(Tarjeta(W, H), W, H));
            int mw = Math.Max(8, W / 4), mh = Math.Max(8, H / 4);
            Bitmap hz = (Bitmap)Estatico(TStar, "BakeViaLactea", mw, mh);
            Textura tx = DeBitmap(hz, basura); IntPtr tex = Registra(tx);
            object tes = De(hz, lt, true);
            Pon(panel, "hazTex", tex); Pon(panel, "nebTex", IntPtr.Zero); Pon(panel, "hazU", (float)mw / tx.W); Pon(panel, "hazV", (float)mh / tx.H);
            float[] fases = { 0f, 0.7f, 1.5707964f, 1.9f, 3.3f, 4.4f, 4.712389f, 5.9f };   // con pi/2 y 3pi/2 el borde de la capa toca el de la pantalla
            for (int i = 0; i < fases.Length; i++)
            {
                Pon(panel, "HazFase", fases[i]);
                envios.Clear();
                Pon(panel, "hazTeselas", null); Llama(panel, "DibujaNieblas"); List<Envio> entero = Captura();
                Pon(panel, "hazTeselas", tes); Llama(panel, "DibujaNieblas"); List<Envio> trozos = Captura();
                Iguales("via lactea " + W + "x" + H + ", deriva " + fases[i], entero, trozos, W, H, 10 + i, W * H / 1000, 1, tes);
            }
            hz.Dispose();   // el panel no se suelta: sus texturas son imitadas y no hay nada que devolver a la tarjeta
        }
        // --- la vineta, a pantalla entera ---
        foreach (int[] p in new int[][] { new int[] { 1024, 768 }, new int[] { 800, 600 } })
        {
            int W = p[0], H = p[1];
            Control panel = Panel(W, H, Cielo(Tarjeta(W, H), W, H));
            using (Bitmap vm = (Bitmap)Estatico(TPanel, "BakeVineta", 128))
            {
                Textura tv = DeBitmap(vm, basura); IntPtr tex = Registra(tv); object tes = De(vm, lt, true);
                envios.Clear();
                Llama(panel, "DibujaTeselas", tex, null, 0f, 0f, (float)W, (float)H, 1f, 1f, 0xFFFFFFFFu, quad); List<Envio> entero = Captura();
                Llama(panel, "DibujaTeselas", tex, tes, 0f, 0f, (float)W, (float)H, 1f, 1f, 0xFFFFFFFFu, quad); List<Envio> trozos = Captura();
                Iguales("vineta " + W + "x" + H, entero, trozos, W, H, 20, W * H / 1000, 1, tes);
            }
        }
        // --- una imagen estirada que solo tiene algo en el centro y basura alrededor en la textura: las teselas del
        //     borde tienen que pintarse, porque ahi la tarjeta lee la basura de al lado (envoltura) ---
        {
            int W = 640, H = 480;
            Control panel = Panel(W, H, Cielo(Tarjeta(W, H), W, H));
            using (Bitmap b = new Bitmap(60, 40, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(b)) using (SolidBrush sb = new SolidBrush(Color.FromArgb(160, 90, 200, 255))) g.FillEllipse(sb, 20, 12, 18, 14);
                Textura t = DeBitmap(b, basura); IntPtr tex = Registra(t); object tes = De(b, lt, true);
                float u = 60f / t.W, v = 40f / t.H;
                envios.Clear();
                Llama(panel, "DibujaTeselas", tex, null, 0f, 0f, (float)W, (float)H, u, v, 0xFFFFFFFFu, quad); List<Envio> entero = Captura();
                Llama(panel, "DibujaTeselas", tex, tes, 0f, 0f, (float)W, (float)H, u, v, 0xFFFFFFFFu, quad); List<Envio> trozos = Captura();
                Iguales("imagen estirada con basura alrededor (el borde se lee)", entero, trozos, W, H, 30, W * H / 1000, 1, tes);
            }
        }
        // --- la interfaz: texel a pixel, arriba y la franja de abajo, con varias opacidades. IDENTICA. ---
        {
            int W = 1024, H = 768;
            Control panel = Panel(W, H, Cielo(Tarjeta(W, H), W, H));
            int[][] ui = { new int[] { W, 118, 0 }, new int[] { W, 645, 0 }, new int[] { W, 123, H - 123 } };
            string[] nom = { "franja de arriba", "interfaz grande (buscador, disco, atraccion)", "franja de abajo" };
            uint[] tintes = { 0xFFFFFFFFu, 0x80FFFFFFu, 0x01FFFFFFu, 0xC8FFFFFFu };
            for (int k = 0; k < ui.Length; k++)
                using (Bitmap b = Interfaz(ui[k][0], ui[k][1], 40 + k))
                {
                    Textura t = DeBitmap(b, basura); IntPtr tex = Registra(t);
                    object tes = Estatico(TPanel, "TeselasQuietas", b, b.Width, b.Height, (double)Estatico(TPanel, "AhoraMs") - 1000.0);
                    if (tes == null) { Mal(nom[k] + ": quieta de sobra y sin teselas"); continue; }
                    string q = Revisa(b, lui, false, tes);
                    Exige(q == null, nom[k] + ": sus teselas cumplen la regla" + (q != null ? " -- " + q : "") + "; pinta el " + Pct((double)Toma(tes, "Fraccion")));
                    float u = (float)b.Width / t.W, v = (float)b.Height / t.H, y0 = ui[k][2];
                    for (int j = 0; j < tintes.Length; j++)
                    {
                        envios.Clear();
                        Llama(panel, "DibujaTeselas", tex, null, 0f, y0, (float)b.Width, y0 + b.Height, u, v, tintes[j], quad); List<Envio> entero = Captura();
                        Llama(panel, "DibujaTeselas", tex, tes, 0f, y0, (float)b.Width, y0 + b.Height, u, v, tintes[j], quad); List<Envio> trozos = Captura();
                        Iguales(nom[k] + ", opacidad " + (tintes[j] >> 24), entero, trozos, W, H, 50 + j, 0, 0, tes);
                    }
                }
        }
    }
    // Una interfaz de prueba: textos, pastillas y huecos transparentes; y texeles sueltos de alfa 1 en sitios delicados.
    static Bitmap Interfaz(int w, int h, int semilla)
    {
        Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        Random r = new Random(semilla);
        using (Graphics g = Graphics.FromImage(b))
        using (Font f = new Font(FontFamily.GenericSansSerif, 15f, FontStyle.Bold))
        {
            g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            for (int i = 0; i < 14; i++)
            {
                int x = r.Next(w - 40), y = r.Next(Math.Max(1, h - 30)), ww = 30 + r.Next(w / 5), hh = 12 + r.Next(Math.Max(1, h / 5));
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(30 + r.Next(225), r.Next(256), r.Next(256), r.Next(256))))
                    if (i % 3 == 0) g.FillEllipse(sb, x, y, ww, hh); else g.FillRectangle(sb, x, y, ww, hh);
                g.DrawString("Cancion " + i + " - Artista", f, Brushes.White, r.Next(w - 100), r.Next(Math.Max(1, h - 24)));
            }
        }
        Punto(b, 0, 0, 1); Punto(b, w - 1, h - 1, 1); Punto(b, 31, h / 2, 1); Punto(b, 32, 5, 1); Punto(b, w / 2, 15, 1); Punto(b, w / 2 + 3, 16, 1);
        return b;
    }

    // ======================= LA INTERFAZ QUIETA =======================
    static void Quieta()
    {
        Console.WriteLine("\n  4) LA INTERFAZ SE TROCEA CUANDO LLEVA UN RATO QUIETA\n");
        double quieta = (double)Campo(TPanel, "TeselasQuieta").GetValue(null);
        double ahora = (double)Estatico(TPanel, "AhoraMs");
        using (Bitmap b = Interfaz(300, 80, 3))
        {
            Exige(Estatico(TPanel, "TeselasQuietas", b, 300, 80, ahora) == null, "recien subida no se trocea: escribir en el buscador no espera a nada");
            Exige(Estatico(TPanel, "TeselasQuietas", b, 300, 80, ahora - quieta * 0.5) == null, "a medio camino (" + (quieta * 0.5) + " ms) tampoco");
            Exige(Estatico(TPanel, "TeselasQuietas", b, 300, 80, ahora - quieta - 5) != null, "pasados " + quieta + " ms sin cambios, si");
            Exige(Estatico(TPanel, "TeselasQuietas", b, 299, 80, ahora - quieta - 5) == null && Estatico(TPanel, "TeselasQuietas", b, 300, 81, ahora - quieta - 5) == null,
                  "si la imagen no mide lo que se subio, no se trocea (se pinta entera, como siempre)");
            Exige(Estatico(TPanel, "TeselasQuietas", null, 300, 80, ahora - quieta - 5) == null, "sin imagen, nada");
        }
        Exige(quieta >= 100 && quieta <= 1000, "la espera es corta pero no inmediata (" + quieta + " ms)");
        // Enchufado: al subir una imagen nueva las teselas viejas se tiran; y se piden a trozos las tres capas grandes.
        MethodInfo rf = Metodo(TPanel, "RenderFrame");
        int lee, esc;
        Codigo.UsosDeCampo(rf, "uiTeselas", out lee, out esc);
        Exige(esc >= 2 && lee >= 2, "el cuadro tira las teselas de la interfaz al subirla y las pide cuando esta quieta");
        Codigo.UsosDeCampo(rf, "uiBotTeselas", out lee, out esc);
        Exige(esc >= 2 && lee >= 2, "y lo mismo con la franja de abajo");
        int veces = 0; foreach (string s in Codigo.Nombres(rf)) if (s == "DibujaTeselas") veces++;
        Exige(veces >= 3, "el cuadro dibuja a trozos la vineta, la interfaz y la franja (" + veces + " sitios)");
        int vq = 0; foreach (string s in Codigo.Nombres(rf)) if (s == "TeselasQuietas") vq++;
        Exige(vq == 2, "y pide las teselas quietas de las dos imagenes de la interfaz");
        Exige(Codigo.Llama(Metodo(TPanel, "DibujaNieblas"), "DibujaTeselas"), "la via lactea se dibuja a trozos");
        Exige(Codigo.Llama(Metodo(TPanel, "EnsureNieblas"), "De") && Codigo.Llama(Metodo(TPanel, "EnsureVineta"), "De"),
              "y sus teselas se sacan al hornear la via lactea y la vineta, una vez");
        int lv, ev; Codigo.UsosDeCampo(Metodo(TPanel, "EnsureVineta"), "vinetaTeselas", out lv, out ev);
        Exige(ev >= 2, "al apagar la vineta sus teselas se sueltan con ella");
    }

    // ======================= SIN BORRAR =======================
    static void SinBorrar()
    {
        Console.WriteLine("\n  1) SIN BORRAR LA PANTALLA: EL CIELO LA TAPA ENTERA\n");
        Exige(Campo(TDev, "clear") == null, "el dispositivo ya no guarda la funcion de borrar la pantalla");
        FieldInfo corte = Campo(TForm, "cieloCorte");
        float corteDe = (float)corte.GetValue(null);
        int[][] pant = { new int[] { 640, 480 }, new int[] { 800, 600 }, new int[] { 1024, 768 }, new int[] { 1366, 768 }, new int[] { 1920, 1080 } };
        float[] cortes = { corteDe, 0.25f, 0.5f, 0.61803f, 0.8317f };
        int cuadros = 0, malos = 0; string primero = null;
        try
        {
            foreach (int[] p in pant)
            {
                int W = p[0], H = p[1];
                object tarjeta = Tarjeta(W, H); object sf = Cielo(tarjeta, W, H);
                foreach (float c in cortes)
                {
                    corte.SetValue(null, c);
                    envios.Clear(); borrados = 0;
                    bool ok = (bool)Llama(sf, "Begin", W, H, 0f, 0f);
                    Llama(sf, "End");
                    string q = null;
                    if (!ok) q = "el cuadro no arranca";
                    else if (borrados != 0) q = "borra la pantalla";
                    else if (envios.Count == 0) q = "no pinta nada";
                    else
                    {
                        Envio e = envios[0];
                        if (e.Mezcla || e.ConTextura || e.Tris != 4) q = "lo primero no es el cielo opaco (mezcla " + e.Mezcla + ", textura " + e.ConTextura + ", " + e.Tris + " triangulos)";
                        else
                        {
                            int[] veces = new int[W * H];
                            for (int i = 0; i < 4; i++) Triangulo(e.Datos, i * 3, null, false, false, null, veces, W, H);
                            int huecos = 0, dobles = 0;
                            for (int i = 0; i < veces.Length; i++) { if (veces[i] == 0) huecos++; else if (veces[i] > 1) dobles++; }
                            if (huecos > 0 || dobles > 0) q = huecos + " pixeles sin tapar y " + dobles + " tapados dos veces";
                        }
                    }
                    cuadros++;
                    if (q != null) { malos++; if (primero == null) primero = W + "x" + H + ", corte " + c + ": " + q; }
                }
            }
        }
        finally { corte.SetValue(null, corteDe); }
        Exige(malos == 0, cuadros + " cuadros (5 pantallas x 5 cortes del cielo): nadie borra, lo primero que llega es el cielo sin mezcla y tapa cada pixel una vez" +
                          (primero != null ? " -- " + malos + " mal; el primero: " + primero : ""));
        List<string> orden = Codigo.Repasa(Metodo(TStar, "Begin"));
        int iBegin = orden.IndexOf("BeginFrame"), iSky = orden.IndexOf("DrawSky"), antes = 0;
        for (int i = iBegin + 1; i >= 1 && i < iSky; i++) if (orden[i].StartsWith("Draw")) antes++;
        Exige(iBegin >= 0 && iSky > iBegin && antes == 0, "en el codigo, despues de empezar el cuadro lo primero que se dibuja es el cielo");
    }

    // ======================= LA AURORA =======================
    static Control PanelAurora(int W, int H)
    {
        Control p = Panel(W, H, Cielo(Tarjeta(W, H), W, H));
        Pon(p, "coverClockReady", false); Pon(p, "coverColorsReady", false); Pon(p, "coverWeight", 0f); Pon(p, "coverTargetWeight", 0f);
        Pon(p, "flowPhase", 0.0); Pon(p, "flowBlend", 0f);
        if (Campo(TPanel, "starDrive") != null) Pon(p, "starDrive", 0f);
        return p;
    }
    static object Aurora(int paso, int H)
    {
        object au = Activator.CreateInstance(TAu);
        float[] b = new float[8];
        for (int i = 0; i < 8; i++) b[i] = (float)(0.5 + 0.45 * Math.Sin(paso * 0.21 + i * 0.9));
        uint[] col = { 0x00D77819u, 0x0040C0E0u, 0x00E040A0u, 0x0080FF40u };
        SetF(au, "Has", true); SetF(au, "B", b); SetF(au, "BaseY", H - 130 + 6f); SetF(au, "Alto", H * 1.02f * (0.2f + 0.3f * (paso % 7) / 6f));
        SetF(au, "Rgb", col[(paso / 50) % 4]); SetF(au, "Rgb2", col[(paso / 50 + 1) % 4]); SetF(au, "RgbA", col[(paso / 50 + 2) % 4]); SetF(au, "Rgb2A", col[(paso / 50 + 3) % 4]);
        SetF(au, "Dim", 0.5f + 0.5f * (paso % 5) / 4f); SetF(au, "DimFilo", (paso % 3) / 2f); SetF(au, "Calma", (paso % 11 == 0) ? 0.7f : 0f);
        return au;
    }
    static void SetF(object boxed, string n, object v) { TAu.GetField(n).SetValue(boxed, v); }
    // Un cuadro de aurora con TODO fijado (sin reloj de verdad): lo que se manda a la tarjeta.
    static List<Envio> CuadroAurora(Control p, object au, double reloj, double t, bool sinGuardar)
    {
        Pon(p, "auCuadro", au);
        Llama(p, "AdvanceCoverPalette", reloj);
        Pon(p, "aurRelojOk", false); Pon(p, "aurT", t);
        if (sinGuardar) Pon(p, "aurBaseOk", false);
        envios.Clear();
        Llama(p, "DrawAurora", au);
        return Captura();
    }
    static bool MismosBytes(List<Envio> a, List<Envio> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Tris != b[i].Tris || a[i].Mezcla != b[i].Mezcla || a[i].Suma != b[i].Suma || a[i].Datos.Length != b[i].Datos.Length) return false;
            for (int k = 0; k < a[i].Datos.Length; k++) if (a[i].Datos[k] != b[i].Datos[k]) return false;
        }
        return true;
    }
    static void AuroraIgual()
    {
        Console.WriteLine("\n  5) LA AURORA SIN REPETIR CUENTAS\n");
        const int W = 1024, H = 768;
        Control pa = PanelAurora(W, H), pb = PanelAurora(W, H);
        Color[][] paletas = {
            new Color[] { Color.FromArgb(200, 40, 60), Color.FromArgb(240, 180, 40), Color.FromArgb(30, 90, 200), Color.FromArgb(250, 250, 250), Color.FromArgb(20, 20, 20) },
            new Color[] { Color.FromArgb(90, 200, 120), Color.FromArgb(20, 60, 160) },
            new Color[] { Color.FromArgb(128, 0, 255), Color.FromArgb(255, 0, 128), Color.FromArgb(0, 255, 200), Color.FromArgb(255, 255, 0), Color.FromArgb(10, 10, 60), Color.FromArgb(200, 120, 60), Color.FromArgb(60, 60, 60), Color.FromArgb(240, 240, 255) }
        };
        double reloj = 1000; int distintos = 0, cuadros = 0, conDibujo = 0; string primero = null;
        for (int paso = 0; paso < 360; paso++)
        {
            foreach (Control p in new Control[] { pa, pb })
            {
                if (paso < 240 && paso % 60 == 0) Llama(p, "SetCoverPalette", paletas[(paso / 60) % 3], paso == 120 ? 0f : 0.35f + 0.2f * (paso / 60));
                if (paso == 240) for (int i = 0; i < 200; i++) Llama(p, "AdvanceCoverPalette", reloj + 100.0 * (i + 1));   // la portada ya fundida del todo
                Pon(p, "AuroraBandas", (paso / 30) % 2 == 0); Pon(p, "OndasSuaves", (paso / 45) % 2 == 0); Pon(p, "ColoresFluidos", (paso / 80) % 3 != 2);
                Pon(p, "NebColor", 0xFF8C78FFu); Pon(p, "NebColor2", 0xFF40C0A0u);
            }
            if (paso == 240) reloj += 100.0 * 200;
            object au = Aurora(paso >= 240 ? 240 : paso, H);   // del 240 en adelante: la cancion quieta, solo se mueve la musica
            if (paso >= 240) { float[] b = (float[])TAu.GetField("B").GetValue(au); for (int i = 0; i < 8; i++) b[i] = (float)(0.5 + 0.45 * Math.Sin(paso * 0.33 + i)); }
            reloj += 16.666;
            List<Envio> ea = CuadroAurora(pa, au, reloj, 3.25 + paso * 0.37, false);
            List<Envio> eb = CuadroAurora(pb, au, reloj, 3.25 + paso * 0.37, true);
            cuadros++; if (ea.Count > 0) conDibujo++;
            if (!MismosBytes(ea, eb)) { distintos++; if (primero == null) primero = "cuadro " + paso; }
        }
        Exige(distintos == 0 && conDibujo > cuadros / 2, cuadros + " cuadros (" + conDibujo + " con aurora; paletas que se funden, bandas, ondas, colores que fluyen y 120 cuadros quietos): " +
                                                        "con lo guardado y rehaciendolo siempre se mandan los mismos vertices, byte a byte" + (primero != null ? " -- " + distintos + " distintos, el primero el " + primero : ""));

        // Lo guardado se usa, y se rehace al cambiar cada cosa de la que depende: el testigo en el primer color del pie
        // sobrevive si no se rehace y desaparece si si.
        object au0 = Aurora(250, H);
        CuadroAurora(pa, au0, reloj += 16.666, 1.0, false);
        uint[] pie = (uint[])Toma(pa, "aurBasePie");
        const uint TESTIGO = 0xDEADBEEFu;
        pie[0] = TESTIGO; CuadroAurora(pa, au0, reloj += 16.666, 1.37, false);
        Exige(pie[0] == TESTIGO, "con lo mismo de antes, el color de partida no se vuelve a calcular (se usa lo guardado)");
        string[] nombres = { "el pie grave (Rgb)", "la punta grave (RgbA)", "el pie agudo (Rgb2)", "la punta aguda (Rgb2A)", "las bandas de color", "el peso de la portada" };
        int bien = 0; List<string> mal = new List<string>();
        for (int k = 0; k < nombres.Length + 8; k++)
        {
            pie = (uint[])Toma(pa, "aurBasePie");
            pie[0] = TESTIGO;
            object au1 = Aurora(250, H);
            if (k == 0) SetF(au1, "Rgb", 0x00D77819u ^ 0x00010000u);
            else if (k == 1) SetF(au1, "RgbA", (uint)TAu.GetField("RgbA").GetValue(au1) ^ 0x00000100u);
            else if (k == 2) SetF(au1, "Rgb2", (uint)TAu.GetField("Rgb2").GetValue(au1) ^ 0x00000001u);
            else if (k == 3) SetF(au1, "Rgb2A", (uint)TAu.GetField("Rgb2A").GetValue(au1) ^ 0x00010101u);
            else if (k == 4) Pon(pa, "AuroraBandas", !(bool)Toma(pa, "AuroraBandas"));
            else if (k == 5) Pon(pa, "coverWeight", (float)Toma(pa, "coverWeight") * 0.5f + 0.01f);
            else { uint[] bandas = (uint[])Toma(pa, "coverBands"); bandas[k - 6] ^= 0x00010203u; }
            // sin avanzar la portada (se cambio a mano): solo el dibujo
            Pon(pa, "auCuadro", au1); Pon(pa, "aurRelojOk", false); Pon(pa, "aurT", 2.0 + k);
            envios.Clear(); Llama(pa, "DrawAurora", au1); envios.Clear();
            pie = (uint[])Toma(pa, "aurBasePie");
            string que = k < nombres.Length ? nombres[k] : "el color " + (k - 6) + " de la portada";
            if (pie[0] != TESTIGO) bien++; else mal.Add(que);
            if (k < 4) { CuadroAurora(pa, au0, reloj += 16.666, 1.0, false); }   // vuelve al de siempre
        }
        Exige(mal.Count == 0, "y se rehace al cambiar cualquiera de las " + (nombres.Length + 8) + " cosas de las que depende" + (mal.Count > 0 ? " -- NO al cambiar: " + string.Join(", ", mal.ToArray()) : ""));
        Exige(!Codigo.Llama(Metodo(TPanel, "DrawAurora"), "CoverColor"), "el cuadro de la aurora ya no pide el color de la portada columna a columna");
    }

    // ======================= LAS CUENTAS POR ENTERO =======================
    static uint LerpAntes(uint c0, uint c1, float f)
    {
        uint r = (uint)(((c0 >> 16) & 0xFF) + (((int)((c1 >> 16) & 0xFF) - (int)((c0 >> 16) & 0xFF)) * f));
        uint g = (uint)(((c0 >> 8) & 0xFF) + (((int)((c1 >> 8) & 0xFF) - (int)((c0 >> 8) & 0xFF)) * f));
        uint b = (uint)((c0 & 0xFF) + (((int)(c1 & 0xFF) - (int)(c0 & 0xFF)) * f));
        return (r << 16) | (g << 8) | b;
    }
    static Func<float, float> seguro;
    static uint MixAntes(uint a, uint b, float f)
    {
        f = seguro(f);
        return ((uint)(((a >> 16) & 255) * (1 - f) + ((b >> 16) & 255) * f) << 16) |
            ((uint)(((a >> 8) & 255) * (1 - f) + ((b >> 8) & 255) * f) << 8) | (uint)((a & 255) * (1 - f) + (b & 255) * f);
    }
    static void PorEntero()
    {
        Console.WriteLine("\n  6) LAS CUENTAS DE COLOR POR ENTERO DAN LOS MISMOS NUMEROS\n");
        Func<uint, uint, float, uint> lerp = (Func<uint, uint, float, uint>)Delegate.CreateDelegate(typeof(Func<uint, uint, float, uint>), Metodo(TPanel, "AurLerpRgb"));
        Func<uint, uint, float, uint> mix = (Func<uint, uint, float, uint>)Delegate.CreateDelegate(typeof(Func<uint, uint, float, uint>), Metodo(TFlujo, "Mix"));
        seguro = (Func<float, float>)Delegate.CreateDelegate(typeof(Func<float, float>), Metodo(TPanel, "SafeMoonLevel"));
        List<float> fs = new List<float>(new float[] { 0f, 1f, 0.5f, 0.38f, 1f / 3f, 2f / 3f, 0.25f, 0.75f, 0.999999f, 1e-7f, 0.0625f, 0.5000001f });
        Random r = new Random(5); while (fs.Count < 40) fs.Add((float)r.NextDouble());
        long casos = 0, dl = 0, dm = 0;
        for (int a = 0; a < 256; a++)
            for (int b = 0; b < 256; b++)
            {
                uint c0 = (uint)((a << 16) | (b << 8) | ((a + b) & 255)), c1 = (uint)((b << 16) | (a << 8) | ((a * 7 + b) & 255));
                foreach (float f in fs)
                {
                    casos++;
                    if (lerp(c0, c1, f) != LerpAntes(c0, c1, f)) dl++;
                    if (mix(c0, c1, f) != MixAntes(c0, c1, f)) dm++;
                }
            }
        // Y fuera de 0..1, que Mix recorta y la mezcla de la aurora no recibe nunca.
        foreach (float f in new float[] { -0.5f, 1.5f, float.NaN, float.PositiveInfinity })
            for (int a = 0; a < 256; a += 5) { casos++; if (mix((uint)(a * 0x010101), 0x00FF7F10u, f) != MixAntes((uint)(a * 0x010101), 0x00FF7F10u, f)) dm++; }
        Exige(dl == 0, "la mezcla de la aurora: " + casos + " combinaciones, las mismas que con la cuenta de antes" + (dl > 0 ? " -- " + dl + " distintas" : ""));
        Exige(dm == 0, "y la del color que fluye (PaletteFlow.Mix), tambien" + (dm > 0 ? " -- " + dm + " distintas" : ""));
    }

    // ======================= EL CUADRO DE VERDAD =======================
    static List<Envio> Cuadro(Control p) { envios.Clear(); borrados = 0; bool ok = (bool)Llama(p, "RenderFrame"); if (!ok) throw new Exception("el cuadro no se presenta"); return Captura(); }
    static List<Envio> ConTextura(List<Envio> es, IntPtr tex) { List<Envio> r = new List<Envio>(); foreach (Envio e in es) if (e.Tex == tex && e.ConTextura) r.Add(e); return r; }
    static int Area(List<Envio> es, int W, int H) { int[] v = new int[W * H]; foreach (Envio e in es) for (int i = 0; i < e.Tris; i++) Triangulo(e.Datos, i * 3, null, false, false, null, v, W, H); int n = 0; for (int i = 0; i < v.Length; i++) n += v[i]; return n; }
    static void CuadroDeVerdad()
    {
        Console.WriteLine("\n  7) EL CUADRO DE VERDAD (RenderFrame con la tarjeta imitada)\n");
        const int W = 1024, H = 768;
        Control p = Panel(W, H, Cielo(Tarjeta(W, H), W, H));
        Bitmap arriba = Interfaz(W, 118, 71), abajo = Interfaz(W, 123, 72);
        Pon(p, "GetUi", (Func<Bitmap>)delegate { return arriba; }); Pon(p, "GetUiVersion", (Func<int>)delegate { return 1; });
        Pon(p, "GetUiBot", (Func<Bitmap>)delegate { return abajo; }); Pon(p, "GetUiBotVersion", (Func<int>)delegate { return 1; });
        List<Envio> c1 = Cuadro(p);
        IntPtr ui = (IntPtr)Toma(p, "uiTex"), bot = (IntPtr)Toma(p, "uiBotTex");
        Exige(c1.Count > 0 && !c1[0].Mezcla && !c1[0].ConTextura && c1[0].Tris == 4 && borrados == 0, "el cuadro no borra y empieza por el cielo opaco");
        Exige(Toma(p, "uiTeselas") == null, "recien subida, la interfaz se pinta entera");
        // Quieta: se sigue dibujando (como en el bar) hasta que la interfaz lleva el rato sin cambiar.
        List<Envio> c2; System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();
        // (las dos imagenes: la franja se sube unos milisegundos despues que la interfaz, y llega a los 300 ms despues)
        do { System.Threading.Thread.Sleep(40); c2 = Cuadro(p); }
        while ((Toma(p, "uiTeselas") == null || Toma(p, "uiBotTeselas") == null) && reloj.ElapsedMilliseconds < 5000);
        Exige(Toma(p, "uiTeselas") != null && Toma(p, "uiBotTeselas") != null, "quieta un rato, ya va a trozos");
        IntPtr[] texs = { ui, bot }; string[] nom = { "la interfaz", "la franja de abajo" };
        Rectangle[] donde = { new Rectangle(0, 0, W, arriba.Height), new Rectangle(0, H - abajo.Height, W, abajo.Height) };
        for (int k = 0; k < 2; k++)
        {
            List<Envio> a = ConTextura(c1, texs[k]), b = ConTextura(c2, texs[k]);
            if (a.Count != 1 || b.Count != 1) { Mal(nom[k] + ": se esperaba un envio en cada cuadro (" + a.Count + " y " + b.Count + ")"); continue; }
            // Donde siempre: la entera tapa justo su rectangulo (arriba, o pegada abajo), una vez cada pixel.
            int[] v = new int[W * H]; for (int i = 0; i < a[0].Tris; i++) Triangulo(a[0].Datos, i * 3, null, false, false, null, v, W, H);
            int fuera = 0; for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (v[y * W + x] != (donde[k].Contains(x, y) ? 1 : 0)) fuera++;
            Exige(fuera == 0, nom[k] + " se pinta donde siempre (" + donde[k].Width + "x" + donde[k].Height + " desde y=" + donde[k].Y + ")" + (fuera > 0 ? " -- " + fuera + " pixeles fuera de sitio" : ""));
            int[] fa = Fondo(W, H, 70 + k), fb = (int[])fa.Clone();
            Pinta(a, fa, W, H); Pinta(b, fb, W, H);
            int maxd; int dist = Compara(fa, fb, out maxd);
            int areaA = Area(a, W, H), areaB = Area(b, W, H);
            Exige(dist == 0 && areaB < areaA, nom[k] + ", entera y a trozos dentro del cuadro: " + dist + " pixeles distintos; pinta " + areaB + " pixeles en vez de " + areaA);
        }
        // Con el reposo del todo abierto y los controles ocultos, la interfaz y la franja no se ven: no se mandan.
        List<Envio> c3, c4;
        IntPtr fal; CreaTex(IntPtr.Zero, 64, 64, 1, 0, 0, 0, out fal, IntPtr.Zero);
        Pon(p, "idleTex", fal); Pon(p, "IdleExpandTarget", true); Pon(p, "idleBlend", 1f); Pon(p, "sceneMode", 1); Pon(p, "ControlesReposo", true);
        c3 = Cuadro(p);
        Exige(ConTextura(c3, ui).Count == 0 && ConTextura(c3, bot).Count == 0, "invisibles del todo (reposo abierto, controles ocultos), ni la interfaz ni la franja se mandan");
        Pon(p, "ControlesReposo", false);
        c4 = Cuadro(p);
        Exige(ConTextura(c4, ui).Count == 1 && ConTextura(c4, bot).Count == 0, "con los controles a la vista la interfaz si se pinta, y la franja tapada por el reposo sigue sin mandarse");
    }

    // En Linux (Mono) la rocola copia memoria con una funcion de Windows (RtlMoveMemory), y sin ella no puede subir una
    // imagen a la tarjeta imitada. Se carga una COPIA de la rocola con su .config, que le dice a Mono que use memmove.
    // No se toca el ejecutable de las pruebas y no se deja nada: la carpeta se borra al acabar. En Windows, tal cual.
    static string carpetaCopia;
    static string Cargable(string ruta)
    {
        if (Type.GetType("Mono.Runtime") == null || Environment.OSVersion.Platform == PlatformID.Win32NT) return ruta;
        string dir = Path.Combine(Path.GetTempPath(), "rocola_dibujoligero_" + System.Diagnostics.Process.GetCurrentProcess().Id);
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
        TPanel = a.GetType("Rocola.GpuPanel");
        if (TPanel == null) { Console.WriteLine("  OK    sin tarjeta grafica no hay nada que aligerar: el dibujo por procesador no se toca"); return 0; }
        TStar = a.GetType("Rocola.GpuStarfield"); TDev = a.GetType("Rocola.D3D9Device"); TTes = a.GetType("Rocola.TeselasVisibles");
        TAu = a.GetType("Rocola.GpuAurora"); TForm = a.GetType("Rocola.Form1"); TFlujo = a.GetType("Rocola.PaletteFlow");
        TD3D = a.GetType("Rocola.D3D9"); TVtx = TD3D != null ? TD3D.GetNestedType("Vtx", TODO) : null;
        if (TStar == null || TDev == null || TTes == null || TAu == null || TVtx == null || TForm == null || TFlujo == null)
        { Mal("faltan piezas del dibujo por tarjeta (TeselasVisibles, GpuAurora, D3D9.Vtx...)"); return 1; }
        Exige(Marshal.SizeOf(TVtx) == 28 && (int)Marshal.OffsetOf(TVtx, "Color") == 16 && (int)Marshal.OffsetOf(TVtx, "U") == 20, "el vertice mide lo que espera el raster de referencia");
        try { SinBorrar(); } catch (Exception e) { Mal("sin borrar: " + e.GetType().Name + ": " + e.Message); }
        try { Regla(); } catch (Exception e) { Mal("la regla: " + e.GetType().Name + ": " + e.Message); }
        try { MismaImagen(); } catch (Exception e) { Mal("la misma imagen: " + e.GetType().Name + ": " + e.Message); }
        try { Quieta(); } catch (Exception e) { Mal("la interfaz quieta: " + e.GetType().Name + ": " + e.Message); }
        try { AuroraIgual(); } catch (Exception e) { Mal("la aurora: " + e.GetType().Name + ": " + e.Message); }
        try { PorEntero(); } catch (Exception e) { Mal("por entero: " + e.GetType().Name + ": " + e.Message); }
        try { CuadroDeVerdad(); } catch (Exception e) { Mal("el cuadro de verdad: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        Console.WriteLine(fallos == 0 ? "\n  todo bien" : ("\n  " + fallos + " fallos"));
        return fallos;
    }
}
