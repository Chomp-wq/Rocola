using System;
using System.Collections;
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

// EL BUSCADOR, MAS LIGERO PARA LA TARJETA (Rocola/BuscadorLigeroGpu.cs): LO MISMO EN PANTALLA, MENOS QUE SUBIR.
//
// La imagen grande del buscador -la de pantalla completa, 7 MB a 1920x1080- se sigue haciendo entera como siempre: no
// cambia como se dibuja nada. Lo que cambia es lo que se le manda a la tarjeta:
//
//  1) SOLO EL TROZO QUE CAMBIO. Al posar una consulta, al pedir una cancion o al entrar o salir un aviso, la tarjeta
//     recibe la columna de resultados o el aviso, no la imagen entera. Lo delicado es que el trozo sea el bueno: si
//     algo cambiara fuera de el, en la pantalla del bar se quedaria viejo. Aqui se hace la imagen ANTES y DESPUES de
//     cada cambio, por el mismo camino que la rocola, y se comprueba que fuera del trozo que calcula el formulario no
//     cambia NI UN PIXEL -con el teclado y con los resultados enfocados, en varias pantallas-. Y que lo que cambia la
//     pantalla entera (enfocar resultados, el color, el tamano) si pide la imagen entera.
//  2) CERRAR SIN COPIAR. La ventana que se encoge al cerrar usa la textura que ya tenia la imagen, en vez de copiarla y
//     volver a subirla. Se compara lo que le llega a la tarjeta por los dos caminos: los mismos vertices y la misma
//     imagen.
//  3) ABRIR SIN CREAR. Al abrir, se reutiliza la textura de la ultima ventana si mide lo mismo y ya no se dibuja.
//
// La tarjeta es IMITADA (como en 'dibujoligero'): apunta cada bloqueo de textura y cada envio. No mide velocidad ni
// promete lo que haga una tarjeta concreta: dice que el programa le pide lo mismo, con menos bytes.
class BuscaLigero
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos;
    static void Bien(string q) { Console.WriteLine("  OK    " + q); }
    static void Mal(string q) { Console.WriteLine("  FALLO " + q); fallos++; }
    static void Exige(bool c, string q) { if (c) Bien(q); else Mal(q); }

    static Type TF, TSO, TItem, TAlb, TPanel, TStar, TDev, TD3D, TWin;

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
    static object Llama(object o, string n, params object[] a)
    {
        MethodInfo m = Metodo(o.GetType(), n);
        if (m == null) throw new Exception("falta " + o.GetType().Name + "." + n);
        try { return m.Invoke(o, a); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }

    // ======================= EL BUSCADOR, MONTADO COMO EN LA ROCOLA =======================
    static Bitmap portada;
    static Image Cov<T>(T a) { return portada; }
    static readonly Dictionary<string, int> colaDe = new Dictionary<string, int>();
    static int Cola<T>(T it) { int p; string d = (string)TItem.GetField("Display").GetValue(it); return colaDe.TryGetValue(d, out p) ? p : 0; }
    static string generoDe = "Balada";
    static string Gen<T>(T it) { return generoDe; }
    static IList Resultados(string semilla, int n)
    {
        FieldInfo fKind = TItem.GetField("Kind"), fDisp = TItem.GetField("Display"), fKey = TItem.GetField("Key"), fSc = TItem.GetField("Score"), fAlb = TItem.GetField("AlbumRef");
        IList res = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TItem));
        string[] nom = { "Depeche Mode - Enjoy The Silence", "Jose Jose - El Triste", "Los Angeles Azules - Nunca Es Suficiente",
                         "Metallica - Enter Sandman", "Juan Gabriel - Amor Eterno", "Bronco - Que No Quede Huella" };
        object alb = Activator.CreateInstance(TAlb, true);
        TAlb.GetField("Folder").SetValue(alb, @"C:\Musica\Disco"); TAlb.GetField("Name").SetValue(alb, "Un disco"); TAlb.GetField("Artist").SetValue(alb, "Un artista");
        for (int i = 0; i < n; i++)
        {
            object it = Activator.CreateInstance(TItem, true);
            string d = nom[(i + semilla.Length) % nom.Length] + " " + semilla + i;
            fKind.SetValue(it, i % 3); fDisp.SetValue(it, d); fKey.SetValue(it, d.ToLowerInvariant()); fSc.SetValue(it, 5000 - i); fAlb.SetValue(it, alb);
            res.Add(it);
        }
        return res;
    }
    static Control Buscador(int W, int H)
    {
        Control so = (Control)Activator.CreateInstance(TSO, true);
        so.SetBounds(0, 0, W, H - 86);   // SearchOverlayRect
        so.Font = new Font(FontFamily.GenericSansSerif, 12f, FontStyle.Bold);
        TSO.GetField("GetCover").SetValue(so, Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(TAlb, typeof(Image)), typeof(BuscaLigero).GetMethod("Cov", TODO).MakeGenericMethod(TAlb)));
        TSO.GetField("GetGenero").SetValue(so, Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(TItem, typeof(string)), typeof(BuscaLigero).GetMethod("Gen", TODO).MakeGenericMethod(TItem)));
        FieldInfo fq = TSO.GetField("GetQueuePos");
        if (fq != null) fq.SetValue(so, Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(TItem, typeof(int)), typeof(BuscaLigero).GetMethod("Cola", TODO).MakeGenericMethod(TItem)));
        TSO.GetField("Results").SetValue(so, Resultados("de", 40));
        TSO.GetField("Query").SetValue(so, "de"); TSO.GetField("Escrito").SetValue(so, "de");
        TSO.GetField("Accent").SetValue(so, Color.FromArgb(178, 152, 255));
        TSO.GetField("SeparateGpu").SetValue(so, true);
        return so;
    }
    // La imagen grande, por el mismo camino que EnsureUiOverlay: limpiar y componer el buscador encima.
    static MethodInfo mRender;
    static Bitmap Compone(Control so, int W, int mainH)
    {
        Bitmap b = new Bitmap(W, mainH, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            GraphicsState gs = g.Save();
            g.TranslateTransform(so.Left, so.Top);
            mRender.Invoke(so, new object[] { g, new Rectangle(0, 0, so.Width, so.Height), true });
            g.Restore(gs);
        }
        return b;
    }
    static int[] Pixeles(Bitmap b)
    {
        int w = b.Width, h = b.Height; int[] px = new int[w * h];
        BitmapData bd = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try { for (int y = 0; y < h; y++) Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), px, y * w, w); }
        finally { b.UnlockBits(bd); }
        return px;
    }

    // El formulario, sin encender: solo lo que usan las firmas y el trozo del buscador.
    static object form;
    static void Firmas(int W, int H, int mainH, out long fija, out long res, out long aviso)
    {
        object[] a = { W, H, mainH, 0L, 0L, 0L };
        Metodo(TF, "FirmasBuscador").Invoke(form, a);
        fija = (long)a[3]; res = (long)a[4]; aviso = (long)a[5];
    }
    static Rectangle Zona(int W, int mainH, long fija, long res, long aviso)
    { return (Rectangle)Metodo(TF, "ZonaBuscador").Invoke(form, new object[] { W, mainH, fija, res, aviso }); }
    // Como si la imagen se acabara de rehacer en este estado (lo que EnsureUiOverlay apunta al final).
    static void Apunta(int W, int H, int mainH)
    {
        long f, r, a; Firmas(W, H, mainH, out f, out r, out a);
        Pon(form, "uiBuscaFija", f); Pon(form, "uiBuscaRes", r); Pon(form, "uiBuscaAviso", a);
    }

    // ======================= 1) EL TROZO QUE CAMBIA =======================
    delegate void Cambio(Control so);
    static void ElTrozo()
    {
        Console.WriteLine("\n  1) SOLO CAMBIA EL TROZO QUE SE SUBE: ANTES Y DESPUES DE CADA CAMBIO, PIXEL A PIXEL\n");
        string[] nombres = { "posar una consulta", "posar otra con los mismos resultados", "consulta sin resultados", "entra un aviso", "sale el aviso",
                             "el aviso cambia de texto", "se pide una cancion (marca EN COLA)", "llega el genero de un disco", "cancion pedida y su aviso a la vez" };
        Cambio[] cambios = {
            delegate(Control s) { TSO.GetField("Query").SetValue(s, "metal"); TSO.GetField("Results").SetValue(s, Resultados("metal", 23)); },
            delegate(Control s) { TSO.GetField("Query").SetValue(s, "da"); TSO.GetField("Results").SetValue(s, Resultados("da", 40)); },
            delegate(Control s) { TSO.GetField("Query").SetValue(s, "zzzz"); TSO.GetField("Results").SetValue(s, Resultados("z", 0)); },
            delegate(Control s) { TSO.GetField("ToastOn").SetValue(s, true); TSO.GetField("ToastMsg").SetValue(s, "Cancion agregada a la cola"); },
            delegate(Control s) { TSO.GetField("ToastOn").SetValue(s, false); },
            delegate(Control s) { TSO.GetField("ToastMsg").SetValue(s, "Esa cancion ya va seguida"); },
            delegate(Control s) { IList r = (IList)TSO.GetField("Results").GetValue(s); colaDe[(string)TItem.GetField("Display").GetValue(r[3])] = 2; SubeCola(s); },
            delegate(Control s) { generoDe = "Rock"; TSO.GetField("DatosVer").SetValue(s, (int)TSO.GetField("DatosVer").GetValue(s) + 1); },
            delegate(Control s) { IList r = (IList)TSO.GetField("Results").GetValue(s); colaDe[(string)TItem.GetField("Display").GetValue(r[7])] = 3; SubeCola(s);
                                  TSO.GetField("ToastOn").SetValue(s, true); TSO.GetField("ToastMsg").SetValue(s, "Cancion agregada a la cola"); }
        };
        // estado de partida de cada cambio (el aviso que sale o cambia necesita estar puesto)
        bool[] conAviso = { false, false, false, false, true, true, false, false, false };
        int[][] pant = { new int[] { 1024, 768 }, new int[] { 1366, 768 }, new int[] { 1920, 1080 } };
        int casos = 0, malos = 0, vacios = 0, enteras = 0; long bytesZona = 0, bytesTodo = 0; string primero = null;
        foreach (int[] p in pant)
            foreach (bool foco in new bool[] { false, true })
                for (int c = 0; c < cambios.Length; c++)
                {
                    int W = p[0], H = p[1], mainH = H - 114;
                    colaDe.Clear(); generoDe = "Balada";
                    Control so = Buscador(W, H);
                    Pon(form, "searchOverlay", so); Pon(form, "searchOpen", true); Pon(form, "colaVer", 5); TSO.GetField("ColaVer").SetValue(so, 5);
                    TSO.GetField("FocusResults").SetValue(so, foco);
                    if (conAviso[c]) { TSO.GetField("ToastOn").SetValue(so, true); TSO.GetField("ToastMsg").SetValue(so, "Cancion agregada a la cola"); }
                    Bitmap antes = Compone(so, W, mainH);
                    Apunta(W, H, mainH);
                    cambios[c](so);
                    long f, r, a; Firmas(W, H, mainH, out f, out r, out a);
                    Rectangle z = Zona(W, mainH, f, r, a);
                    Bitmap despues = Compone(so, W, mainH);
                    int[] pa = Pixeles(antes), pd = Pixeles(despues);
                    int fuera = 0, dentro = 0; Rectangle resto = Rectangle.Empty;
                    for (int y = 0; y < mainH; y++)
                        for (int x = 0; x < W; x++)
                            if (pa[y * W + x] != pd[y * W + x]) { if (z.Contains(x, y)) dentro++; else { fuera++; Rectangle pixel=new Rectangle(x,y,1,1); resto=resto.IsEmpty ? pixel : Rectangle.Union(resto,pixel); } }
                    casos++;
                    string q = null;
                    // Con los resultados enfocados, quedarse sin resultados devuelve el teclado a la mitad izquierda: media
                    // pantalla. Ahi lo bueno es subirla ENTERA (y se comprueba que de verdad cambio fuera de la columna).
                    bool mediaPantalla = foco && c == 2;
                    if (mediaPantalla)
                    {
                        object[] za = { Rectangle.Empty, Rectangle.Empty }; TSO.GetMethod("ZonasAlEscribir").Invoke(so, za); Rectangle der = (Rectangle)za[1];
                        int fueraCol = 0; for (int y = 0; y < mainH; y++) for (int x = 0; x < W; x++) if (pa[y * W + x] != pd[y * W + x] && !der.Contains(x, y)) fueraCol++;
                        if (!z.IsEmpty) q = "media pantalla cambia y solo sube el trozo " + z;
                        else if (fueraCol == 0) q = "sube entera sin hacer falta";
                        else enteras++;
                    }
                    else if (z.IsEmpty) { q = "no sabe que trozo es (se subiria entera)"; vacios++; }
                    else if (fuera > 0) q = fuera + " pixeles cambian FUERA del trozo " + z + "; extension " + resto;
                    else if (dentro == 0) q = "no cambio nada";
                    if (q != null) { malos++; if (primero == null) primero = W + "x" + H + (foco ? ", resultados enfocados" : ", con teclado") + ", " + nombres[c] + ": " + q; }
                    bytesZona += (long)z.Width * z.Height * 4; bytesTodo += (long)W * mainH * 4;
                    antes.Dispose(); despues.Dispose(); so.Dispose();
                }
        Exige(malos == 0, casos + " cambios (" + nombres.Length + " clases x 3 pantallas x teclado/resultados): fuera del trozo que se sube no cambia ni un pixel" +
                          (primero != null ? " -- " + malos + " mal; el primero: " + primero : ""));
        Exige(enteras == 3, "y quedarse sin resultados con los resultados enfocados (vuelve el teclado: media pantalla) sube la imagen entera, en las 3 pantallas");
        Exige(bytesZona < bytesTodo * 0.75, "y el trozo es de media el " + (100.0 * bytesZona / bytesTodo).ToString("0") + " % de la imagen (lo que se deja de subir: el resto)");

        // Lo que cambia la pantalla entera pide la imagen entera.
        string[] enteros = { "enfocar los resultados", "otro color", "otro tamano del buscador", "sin cambios en resultados ni aviso",
                             "con los resultados enfocados, vuelve a haber resultados (vuelve el cubo)" };
        int bienEnteros = 0; string malEntero = null;
        for (int c = 0; c < enteros.Length; c++)
        {
            const int W = 1024, H = 768, mainH = 654;
            Control so = Buscador(W, H);
            Pon(form, "searchOverlay", so); Pon(form, "searchOpen", true);
            if (c == 4) { TSO.GetField("FocusResults").SetValue(so, true); TSO.GetField("Query").SetValue(so, "zzzz"); TSO.GetField("Results").SetValue(so, Resultados("z", 0)); }
            Apunta(W, H, mainH);
            if (c == 4) { TSO.GetField("Query").SetValue(so, "de"); TSO.GetField("Results").SetValue(so, Resultados("de", 40)); }
            if (c == 0) TSO.GetField("FocusResults").SetValue(so, true);
            else if (c == 1) TSO.GetField("Accent").SetValue(so, Color.FromArgb(255, 120, 60));
            else if (c == 2) so.SetBounds(0, 0, W, H - 90);
            long f, r, a; Firmas(W, H, mainH, out f, out r, out a);
            if (c <= 2) { TSO.GetField("Query").SetValue(so, "otra"); Firmas(W, H, mainH, out f, out r, out a); }   // aunque ADEMAS cambien los resultados
            Rectangle z = Zona(W, mainH, f, r, a);
            if (z.IsEmpty) bienEnteros++; else if (malEntero == null) malEntero = enteros[c] + " -> " + z;
            so.Dispose();
        }
        Exige(bienEnteros == enteros.Length, "enfocar los resultados, que vuelva el cubo, otro color, otro tamano o algo fuera de las dos partes: imagen ENTERA, como siempre" + (malEntero != null ? " -- " + malEntero : ""));

        // El trozo solo vale para la version de justo antes.
        Pon(form, "uiOverlayVer", 10); Pon(form, "uiOverlayZonaVer", 10); Pon(form, "uiOverlayZona", new Rectangle(500, 0, 400, 600));
        Func<int, Rectangle> cambio = (Func<int, Rectangle>)Delegate.CreateDelegate(typeof(Func<int, Rectangle>), form, Metodo(TF, "UiCambioDesde"));
        bool ok = cambio(9) == new Rectangle(500, 0, 400, 600) && cambio(8).IsEmpty && cambio(10).IsEmpty && cambio(-1).IsEmpty;
        Pon(form, "uiOverlayZonaVer", 9);
        ok = ok && cambio(9).IsEmpty;
        Exige(ok, "el formulario solo da el trozo a quien tiene la version de justo antes; a cualquier otra, nada (entera)");
    }
    static void SubeCola(Control s)
    {
        int v = (int)Toma(form, "colaVer") + 1;
        Pon(form, "colaVer", v); TSO.GetField("ColaVer").SetValue(s, v);
    }

    // ======================= LA TARJETA IMITADA (como en 'dibujoligero', con bloqueos por trozos) =======================
    static uint Nada(IntPtr self) { return 0; }
    static uint Memoria(IntPtr self) { return 256u * 1024 * 1024; }
    static readonly List<object> vivos = new List<object>();
    static IntPtr Puntero(string tipo, string metodo)
    {
        Delegate del = Delegate.CreateDelegate(TD3D.GetNestedType(tipo, TODO), typeof(BuscaLigero).GetMethod(metodo, TODO));
        vivos.Add(del); return Marshal.GetFunctionPointerForDelegate(del);
    }
    sealed class Envio { public int Tris; public byte[] Datos; public bool Mezcla, ConTextura; public IntPtr Tex; }
    static readonly List<Envio> envios = new List<Envio>();
    sealed class Estado { public bool Mezcla; public uint OpColor = 4; public IntPtr Tex; }
    static readonly Dictionary<long, Estado> estados = new Dictionary<long, Estado>();
    static Estado EstadoDe(IntPtr self) { Estado e; if (!estados.TryGetValue(self.ToInt64(), out e)) { e = new Estado(); estados[self.ToInt64()] = e; } return e; }
    static int DrawUp(IntPtr self, uint prim, uint count, IntPtr data, uint stride)
    {
        Estado st = EstadoDe(self);
        Envio e = new Envio(); e.Tris = (int)count; e.Datos = new byte[count * 3 * stride]; Marshal.Copy(data, e.Datos, 0, e.Datos.Length);
        e.Mezcla = st.Mezcla; e.ConTextura = st.OpColor == 4; e.Tex = st.Tex; envios.Add(e); return 0;
    }
    static int SetRS(IntPtr self, uint a, uint b) { if (a == 27) EstadoDe(self).Mezcla = b != 0; return 0; }
    static int SetU1(IntPtr self, uint a) { return 0; }
    static int SetTSS(IntPtr self, uint stage, uint type, uint val) { if (type == 1) EstadoDe(self).OpColor = val; return 0; }
    static int SetSamp(IntPtr self, uint stage, uint type, uint val) { return 0; }
    static int SetTex(IntPtr self, uint stage, IntPtr t) { EstadoDe(self).Tex = t; return 0; }
    static int Hr(IntPtr self) { return 0; }
    static int Present(IntPtr self, IntPtr a, IntPtr b, IntPtr c, IntPtr d) { return 0; }
    static void Enchufa(object d, string campo, string metodo)
    {
        FieldInfo f = Campo(TDev, campo);
        Delegate del = Delegate.CreateDelegate(f.FieldType, typeof(BuscaLigero).GetMethod(metodo, TODO));
        vivos.Add(del); f.SetValue(d, del);
    }
    // Las texturas: se crean, se bloquean enteras o por trozos (un rectangulo), y cada bloqueo se apunta.
    sealed class TexImitada { public int W, H; public IntPtr Mem; }
    static readonly Dictionary<long, TexImitada> creadas = new Dictionary<long, TexImitada>();
    static int creaciones;
    sealed class Bloqueo { public IntPtr Tex; public Rectangle Zona; }   // Zona vacia = entera
    static readonly List<Bloqueo> bloqueos = new List<Bloqueo>();
    static IntPtr vtTex, vtTrozos; static int offPitch, offBits;
    static Delegate AlVuelo(string tipo, string destino, bool conRect)
    {
        Type td = TD3D.GetNestedType(tipo, TODO);
        ParameterInfo[] ps = td.GetMethod("Invoke").GetParameters();
        Type[] pt = new Type[ps.Length]; for (int i = 0; i < ps.Length; i++) pt[i] = ps[i].ParameterType;
        DynamicMethod dm = new DynamicMethod(destino + "Imitado", typeof(int), pt, typeof(BuscaLigero).Module, true);
        ILGenerator il = dm.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Conv_U); il.Emit(OpCodes.Ldarg_3);
        if (conRect) il.Emit(OpCodes.Conv_U);   // ref RectI -> su direccion
        il.Emit(OpCodes.Call, typeof(BuscaLigero).GetMethod(destino, TODO));
        il.Emit(OpCodes.Ret);
        Delegate d = dm.CreateDelegate(td); vivos.Add(d); return d;
    }
    static void PreparaTexturas()
    {
        if (vtTex != IntPtr.Zero) return;
        Type tlr = TD3D.GetNestedType("LockedRect", TODO);
        offPitch = (int)Marshal.OffsetOf(tlr, "Pitch"); offBits = (int)Marshal.OffsetOf(tlr, "Bits");
        vtTex = Marshal.AllocHGlobal(64 * IntPtr.Size); vtTrozos = Marshal.AllocHGlobal(64 * IntPtr.Size);
        IntPtr nada = Puntero("UintSelf", "Nada"), suelta = Puntero("UintSelf", "Suelta"), desb = Puntero("UnlockRectDel", "Desbloquea");
        for (int i = 0; i < 64; i++) { Marshal.WriteIntPtr(vtTex, i * IntPtr.Size, nada); Marshal.WriteIntPtr(vtTrozos, i * IntPtr.Size, nada); }
        Marshal.WriteIntPtr(vtTex, 2 * IntPtr.Size, suelta); Marshal.WriteIntPtr(vtTrozos, 2 * IntPtr.Size, suelta);
        Marshal.WriteIntPtr(vtTex, 20 * IntPtr.Size, desb); Marshal.WriteIntPtr(vtTrozos, 20 * IntPtr.Size, desb);
        // Una textura normal se bloquea entera; una "por trozos" se bloquea por un rectangulo (UpdateTextureRegion).
        Marshal.WriteIntPtr(vtTex, 19 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(AlVuelo("LockRectDel", "Bloquea", false)));
        Marshal.WriteIntPtr(vtTrozos, 19 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(AlVuelo("LockRegionDel", "BloqueaTrozo", true)));
    }
    static int CreaTex(IntPtr self, uint w, uint h, uint levels, uint usage, uint format, uint pool, out IntPtr tex, IntPtr shared)
    {
        PreparaTexturas();
        IntPtr o = Marshal.AllocHGlobal(16); Marshal.WriteIntPtr(o, vtTex);
        TexImitada t = new TexImitada(); t.W = (int)w; t.H = (int)h; t.Mem = Marshal.AllocHGlobal((int)(w * h * 4));
        byte[] b = new byte[w * h * 4]; new Random((int)(w * 31 + h)).NextBytes(b); Marshal.Copy(b, 0, t.Mem, b.Length);
        creadas[o.ToInt64()] = t; tex = o; creaciones++;
        return 0;
    }
    static int Bloquea(IntPtr self, IntPtr lr, IntPtr rect)
    {
        TexImitada t = creadas[self.ToInt64()];
        Bloqueo b = new Bloqueo(); b.Tex = self; b.Zona = Rectangle.Empty; bloqueos.Add(b);
        Marshal.WriteInt32(lr, offPitch, t.W * 4); Marshal.WriteIntPtr(lr, offBits, t.Mem);
        return 0;
    }
    static int BloqueaTrozo(IntPtr self, IntPtr lr, IntPtr rect)
    {
        TexImitada t = creadas[self.ToInt64()];
        Rectangle z = rect == IntPtr.Zero ? Rectangle.Empty
            : Rectangle.FromLTRB(Marshal.ReadInt32(rect, 0), Marshal.ReadInt32(rect, 4), Marshal.ReadInt32(rect, 8), Marshal.ReadInt32(rect, 12));
        Bloqueo b = new Bloqueo(); b.Tex = self; b.Zona = z; bloqueos.Add(b);
        Marshal.WriteInt32(lr, offPitch, t.W * 4);
        Marshal.WriteIntPtr(lr, offBits, new IntPtr(t.Mem.ToInt64() + (long)z.Top * t.W * 4 + z.Left * 4));   // como Direct3D: apunta al trozo
        return 0;
    }
    static int Desbloquea(IntPtr self, uint level) { return 0; }
    static uint Suelta(IntPtr self) { TexImitada t; if (creadas.TryGetValue(self.ToInt64(), out t)) { creadas.Remove(self.ToInt64()); Marshal.FreeHGlobal(t.Mem); } return 0; }
    static void PorTrozos(IntPtr tex) { Marshal.WriteIntPtr(tex, vtTrozos); }   // a partir de ahora esta textura se bloquea por rectangulos
    // ¿Lo que tiene la textura es exactamente esta imagen? (arriba a la izquierda, como la sube la rocola)
    static int DistintosEnTextura(IntPtr tex, Bitmap b)
    {
        TexImitada t; if (!creadas.TryGetValue(tex.ToInt64(), out t)) return -1;
        int[] px = Pixeles(b); int[] fila = new int[b.Width]; int n = 0;
        for (int y = 0; y < b.Height; y++)
        {
            Marshal.Copy(new IntPtr(t.Mem.ToInt64() + (long)y * t.W * 4), fila, 0, b.Width);
            for (int x = 0; x < b.Width; x++) if (fila[x] != px[y * b.Width + x]) n++;
        }
        return n;
    }
    static object Tarjeta(int w, int h)
    {
        PreparaTexturas();
        IntPtr vt = Marshal.AllocHGlobal(128 * IntPtr.Size);
        for (int i = 0; i < 128; i++) Marshal.WriteIntPtr(vt, i * IntPtr.Size, Puntero("UintSelf", "Nada"));
        Marshal.WriteIntPtr(vt, 4 * IntPtr.Size, Puntero("UintSelf", "Memoria"));
        IntPtr obj = Marshal.AllocHGlobal(16); Marshal.WriteIntPtr(obj, vt);
        object d = FormatterServices.GetUninitializedObject(TDev);
        Pon(d, "dev", obj); Pon(d, "stTex", new IntPtr(-1));
        object pp = Activator.CreateInstance(TD3D.GetNestedType("PresentParams", TODO));
        pp.GetType().GetField("BackBufferWidth").SetValue(pp, (uint)w); pp.GetType().GetField("BackBufferHeight").SetValue(pp, (uint)h);
        Pon(d, "pp", pp);
        Enchufa(d, "createTex", "CreaTex"); Enchufa(d, "setRS", "SetRS"); Enchufa(d, "setFVF", "SetU1"); Enchufa(d, "setTSS", "SetTSS");
        Enchufa(d, "setSamp", "SetSamp"); Enchufa(d, "setTex", "SetTex"); Enchufa(d, "drawUp", "DrawUp"); Enchufa(d, "present", "Present");
        Pon(d, "verticesSinSoporte", true); // doble UP; el anillo real se comprueba en verticesgpu
        Enchufa(d, "begin", "Hr"); Enchufa(d, "end", "Hr"); Enchufa(d, "testCoop", "Hr");
        string[] arr = { "cubeFaceV", "cubeBatchV", "cbx", "cbz", "cbo" }; int[] lar = { 6, 24, 4, 4, 4 };
        for (int i = 0; i < arr.Length; i++) { FieldInfo f = Campo(TDev, arr[i]); if (f != null && f.GetValue(d) == null) f.SetValue(d, Array.CreateInstance(f.FieldType.GetElementType(), lar[i])); }
        if (!(bool)Llama(d, "InitStates")) throw new Exception("la tarjeta imitada no arranca");
        return d;
    }
    static Control Panel(int w, int h)
    {
        object sf = Activator.CreateInstance(TStar, TODO, null, new object[] { IntPtr.Zero, w, h, 4321 }, null);
        Pon(sf, "gpu", Tarjeta(w, h)); Pon(sf, "seed0", 4321); Llama(sf, "BuildStars", w, h, 4321);
        Control p = (Control)Activator.CreateInstance(TPanel, true);
        p.SetBounds(0, 0, w, h); Pon(p, "stars", sf);
        return p;
    }
    static List<Envio> Cuadro(Control p)
    {
        envios.Clear(); bloqueos.Clear();
        if (!(bool)Llama(p, "RenderFrame")) throw new Exception("el cuadro no se presenta");
        List<Envio> r = new List<Envio>(envios); envios.Clear(); return r;
    }
    // Dibuja cuadros hasta que la tarjeta tenga la version 'version' de la imagen de la UI (como en el bar: el trabajo
    // pesado va por franjas que se ajustan a lo que cuesta, asi que no siempre toca en el cuadro siguiente). Devuelve el
    // ultimo cuadro, y en 'bloqueos' quedan los de todos.
    static List<Envio> CuadroHasta(Control p, int version)
    {
        List<Bloqueo> todos = new List<Bloqueo>(); List<Envio> ult = null;
        System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();
        do
        {
            ult = Cuadro(p); todos.AddRange(bloqueos);
            if ((int)Toma(p, "uiVer") == version) break;
            System.Threading.Thread.Sleep(10);
        } while (reloj.ElapsedMilliseconds < 5000);
        bloqueos.Clear(); bloqueos.AddRange(todos);
        return ult;
    }
    static int Pot2(int n) { int p = 1; while (p < n) p <<= 1; return p; }
    static List<Bloqueo> De(IntPtr tex) { List<Bloqueo> r = new List<Bloqueo>(); foreach (Bloqueo b in bloqueos) if (b.Tex == tex) r.Add(b); return r; }
    static List<Envio> ConTextura(List<Envio> es, IntPtr tex) { List<Envio> r = new List<Envio>(); foreach (Envio e in es) if (e.Tex == tex && e.ConTextura) r.Add(e); return r; }

    // ======================= 2) LA TARJETA RECIBE SOLO EL TROZO =======================
    static Bitmap actual; static int version;
    static void SoloElTrozo()
    {
        Console.WriteLine("\n  2) LA TARJETA RECIBE SOLO EL TROZO, Y SU TEXTURA QUEDA IGUAL QUE LA IMAGEN\n");
        const int W = 1024, H = 768, mainH = 654;
        Control p = Panel(W, H);
        Control so = Buscador(W, H);
        colaDe.Clear(); generoDe = "Balada";
        Pon(form, "searchOverlay", so); Pon(form, "searchOpen", true); Pon(form, "colaVer", 1);
        actual = Compone(so, W, mainH); version = 1;
        Pon(form, "uiOverlayVer", version); Pon(form, "uiOverlayZonaVer", version); Pon(form, "uiOverlayZona", Rectangle.Empty); Apunta(W, H, mainH);
        Pon(p, "GetUi", (Func<Bitmap>)delegate { return actual; });
        Pon(p, "GetUiVersion", (Func<int>)delegate { return version; });
        Pon(p, "GetUiCambio", Delegate.CreateDelegate(typeof(Func<int, Rectangle>), form, Metodo(TF, "UiCambioDesde")));
        int c0 = creaciones; Cuadro(p);
        IntPtr ui = (IntPtr)Toma(p, "uiTex");
        bool enteraUi = false; foreach (Bloqueo bq in bloqueos) if (bq.Tex == ui && bq.Zona.IsEmpty) enteraUi = true;
        Exige(ui != IntPtr.Zero && enteraUi && DistintosEnTextura(ui, actual) == 0, "la primera vez se crea la textura y se sube entera");
        PorTrozos(ui);
        // Un cambio que sabe su trozo: como EnsureUiOverlay, rehacer la imagen entera y apuntar que trozo cambio.
        string[] nom = { "posar una consulta", "entra un aviso", "sale el aviso", "se pide una cancion" };
        Cambio[] cambios = {
            delegate(Control s) { TSO.GetField("Query").SetValue(s, "metal"); TSO.GetField("Results").SetValue(s, Resultados("metal", 23)); },
            delegate(Control s) { TSO.GetField("ToastOn").SetValue(s, true); TSO.GetField("ToastMsg").SetValue(s, "Cancion agregada a la cola"); },
            delegate(Control s) { TSO.GetField("ToastOn").SetValue(s, false); },
            delegate(Control s) { IList r = (IList)TSO.GetField("Results").GetValue(s); colaDe[(string)TItem.GetField("Display").GetValue(r[1])] = 1; SubeCola(s); }
        };
        for (int k = 0; k < cambios.Length; k++)
        {
            cambios[k](so);
            long f, r, a; Firmas(W, H, mainH, out f, out r, out a);
            Rectangle z = Zona(W, mainH, f, r, a);
            Bitmap nueva = Compone(so, W, mainH);
            Bitmap vieja = actual; actual = nueva; version++;
            Pon(form, "uiOverlayVer", version); Pon(form, "uiOverlayZonaVer", version); Pon(form, "uiOverlayZona", z); Apunta(W, H, mainH);
            c0 = creaciones;
            CuadroHasta(p, version);
            IntPtr ui2 = (IntPtr)Toma(p, "uiTex");
            List<Bloqueo> bu = De(ui);
            // Si el trozo toca el borde derecho o el de abajo de la imagen, se bloquea hasta el borde de la
            // textura (potencia de 2) para extender el ultimo pixel: el filtro bilineal no lee basura al lado.
            // Eso es relleno que no se ve; lo demas tiene que ser exactamente el trozo (TexturasCompatiblesGpu.cs).
            Rectangle zb = Rectangle.FromLTRB(z.Left, z.Top, z.Right == W ? Pot2(W) : z.Right, z.Bottom == mainH ? Pot2(mainH) : z.Bottom);
            bool trozo = bu.Count == 1 && (bu[0].Zona == z || bu[0].Zona == zb) && !z.IsEmpty;
            int dist = DistintosEnTextura(ui2, actual);
            Exige(trozo && ui2 == ui && creaciones == c0 && dist == 0,
                  nom[k] + ": la tarjeta recibe solo " + z.Width + "x" + z.Height + " de " + W + "x" + mainH + " (" + (100.0 * z.Width * z.Height / (W * mainH)).ToString("0") + " %), " +
                  "la misma textura, y queda igual que la imagen" + (dist != 0 ? " -- " + dist + " pixeles distintos" : "") + (trozo ? "" : " -- bloqueos de la textura: " + bu.Count));
            vieja.Dispose();
        }
        // Un cambio de los de pantalla entera: se sube entera (aunque la textura se deje por trozos).
        TSO.GetField("FocusResults").SetValue(so, true);
        {
            long f, r, a; Firmas(W, H, mainH, out f, out r, out a);
            Rectangle z = Zona(W, mainH, f, r, a);
            actual = Compone(so, W, mainH); version++;
            Pon(form, "uiOverlayVer", version); Pon(form, "uiOverlayZonaVer", version); Pon(form, "uiOverlayZona", z); Apunta(W, H, mainH);
            CuadroHasta(p, version);
            IntPtr ui2 = (IntPtr)Toma(p, "uiTex");
            bool entera = false; foreach (Bloqueo b in bloqueos) if ((b.Tex == ui || b.Tex == ui2) && b.Zona.IsEmpty) entera = true;
            Exige(z.IsEmpty && entera && DistintosEnTextura(ui2, actual) == 0, "al enfocar los resultados (media pantalla cambia) se sube entera y queda igual");
            ui = ui2; PorTrozos(ui);
        }
        // Si la tarjeta se salto una version, tampoco vale el trozo: entera.
        {
            TSO.GetField("Query").SetValue(so, "otra vez"); TSO.GetField("Results").SetValue(so, Resultados("ov", 12));
            long f, r, a; Firmas(W, H, mainH, out f, out r, out a);
            Rectangle z = Zona(W, mainH, f, r, a);
            actual = Compone(so, W, mainH); version += 2;   // una version que la tarjeta no llego a ver
            Pon(form, "uiOverlayVer", version); Pon(form, "uiOverlayZonaVer", version); Pon(form, "uiOverlayZona", z); Apunta(W, H, mainH);
            CuadroHasta(p, version);
            IntPtr ui2 = (IntPtr)Toma(p, "uiTex");
            bool entera = false; foreach (Bloqueo b in bloqueos) if ((b.Tex == ui || b.Tex == ui2) && b.Zona.IsEmpty) entera = true;
            Exige(!z.IsEmpty && entera && DistintosEnTextura(ui2, actual) == 0, "si la tarjeta no tiene la version de justo antes, aunque se sepa el trozo, se sube entera");
        }
        so.Dispose();
    }

    // ======================= 3) CERRAR SIN COPIAR, 4) ABRIR SIN CREAR =======================
    static object Ventana(bool robada, Bitmap bmp, bool abriendo)
    {
        object g = Activator.CreateInstance(TWin);
        TWin.GetField("Has").SetValue(g, true);
        TWin.GetField("Robada").SetValue(g, robada);
        TWin.GetField("Bmp").SetValue(g, bmp);
        TWin.GetField("HideUi").SetValue(g, abriendo);
        TWin.GetField("Scale").SetValue(g, 0.91f); TWin.GetField("ScaleY").SetValue(g, 0.93f); TWin.GetField("Dy").SetValue(g, 6.5f); TWin.GetField("Alpha").SetValue(g, 0.71f);
        return g;
    }
    static void CerrarYAbrir()
    {
        Console.WriteLine("\n  3) CERRAR SIN COPIAR: LA VENTANA USA LA TEXTURA QUE YA TENIA LA IMAGEN\n");
        const int W = 1024, H = 768, mainH = 654;
        Control so = Buscador(W, H);
        Bitmap busca = Compone(so, W, mainH);
        Bitmap nav = new Bitmap(W, 118, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(nav)) using (SolidBrush sb = new SolidBrush(Color.FromArgb(160, 40, 60, 120))) { g.Clear(Color.Transparent); g.FillRectangle(sb, 10, 10, 500, 60); }
        // Dos paneles iguales: el de ahora (sin copia) y el de siempre (copia subida a la textura de la ventana).
        Control[] ps = { Panel(W, H), Panel(W, H) };
        Bitmap[] ui = { busca, busca }; int[] ver = { 1, 1 };
        List<Envio>[] vent = new List<Envio>[2]; IntPtr[] texVent = new IntPtr[2];
        for (int k = 0; k < 2; k++)
        {
            int kk = k;
            Pon(ps[k], "GetUi", (Func<Bitmap>)delegate { return ui[kk]; });
            Pon(ps[k], "GetUiVersion", (Func<int>)delegate { return ver[kk]; });
            Pon(ps[k], "GetWin", Delegate.CreateDelegate(typeof(Func<>).MakeGenericType(TWin), typeof(BuscaLigero).GetMethod("DameVentana" + k, TODO).MakeGenericMethod(TWin)));
            ventanas[k] = null;
            Cuadro(ps[k]);   // el buscador abierto, subido
        }
        IntPtr uiTex0 = (IntPtr)Toma(ps[0], "uiTex");
        Exige(!(bool)Llama(ps[0], "RobaUiParaVentana", 7), "si la textura no tiene la version que se pide, no se toca (el formulario hace la copia de siempre)");
        Exige((IntPtr)Toma(ps[0], "uiTex") == uiTex0, "y la textura de la UI sigue en su sitio");
        bool robo = (bool)Llama(ps[0], "RobaUiParaVentana", 1);
        Exige(robo && (IntPtr)Toma(ps[0], "winTex") == uiTex0 && (IntPtr)Toma(ps[0], "uiTex") == IntPtr.Zero && (int)Toma(ps[0], "winBw") == W && (int)Toma(ps[0], "winBh") == mainH,
              "con la version buena, la textura de la UI pasa a ser la de la ventana, sin copiar ni subir nada");
        // Se cierra: la UI pasa a ser la de navegacion y la ventana se encoge.
        ui[0] = nav; ver[0] = 2; ventanas[0] = Ventana(true, null, false);
        Bitmap copia = busca.Clone(new Rectangle(0, 0, W, mainH), PixelFormat.Format32bppPArgb);
        ui[1] = nav; ver[1] = 2; ventanas[1] = Ventana(false, copia, false);
        for (int k = 0; k < 2; k++)
        {
            List<Envio> c = CuadroHasta(ps[k], 2);
            texVent[k] = (IntPtr)Toma(ps[k], "winTex");
            vent[k] = ConTextura(c, texVent[k]);
        }
        bool mismos = vent[0].Count == 1 && vent[1].Count == 1 && vent[0][0].Tris == vent[1][0].Tris && vent[0][0].Datos.Length == vent[1][0].Datos.Length;
        if (mismos) for (int i = 0; i < vent[0][0].Datos.Length; i++) if (vent[0][0].Datos[i] != vent[1][0].Datos[i]) { mismos = false; break; }
        Exige(mismos, "la ventana que se encoge recibe los mismos vertices por los dos caminos");
        Exige(DistintosEnTextura(texVent[0], busca) == 0 && DistintosEnTextura(texVent[1], busca) == 0, "y las dos texturas tienen la misma imagen: la del buscador");
        Exige(ConTextura(Cuadro(ps[0]), (IntPtr)Toma(ps[0], "uiTex")).Count >= 0 && DistintosEnTextura((IntPtr)Toma(ps[0], "uiTex"), nav) == 0,
              "mientras, la UI de navegacion se sube a su propia textura, como siempre");

        Console.WriteLine("\n  4) ABRIR SIN CREAR: LA TEXTURA DE LA ULTIMA VENTANA SE REUTILIZA\n");
        // Mientras la ventana se esta dibujando no se toca.
        ui[0] = busca; ver[0] = 3; ventanas[0] = Ventana(true, null, false);
        int c0 = creaciones; CuadroHasta(ps[0], 3);
        IntPtr ui3 = (IntPtr)Toma(ps[0], "uiTex");
        Exige(ui3 != texVent[0] && (IntPtr)Toma(ps[0], "winTex") == texVent[0] && creaciones == c0 + 1 && DistintosEnTextura(ui3, busca) == 0,
              "con la ventana todavia encogiendose, abrir crea una textura nueva y no toca la de la ventana");
        // Ya terminada la ventana: se cierra y se vuelve a abrir.
        ventanas[0] = null; ui[0] = nav; ver[0] = 4; CuadroHasta(ps[0], 4);
        if (!(bool)Llama(ps[0], "RobaUiParaVentana", 4)) Mal("no se pudo preparar la ventana para la prueba de abrir");
        IntPtr laVentana = (IntPtr)Toma(ps[0], "winTex");
        // (ahora la ventana es la de navegacion, que no mide lo mismo que el buscador: al abrir se crea)
        ui[0] = busca; ver[0] = 5; ventanas[0] = Ventana(false, null, true);
        c0 = creaciones; CuadroHasta(ps[0], 5);
        Exige(creaciones == c0 + 1 && (IntPtr)Toma(ps[0], "winTex") == laVentana, "si la ventana no mide lo mismo, se crea una textura nueva, como siempre");
        // Cerrar el buscador de verdad (se roba su textura grande) y abrirlo otra vez: se reutiliza.
        IntPtr grande = (IntPtr)Toma(ps[0], "uiTex");
        Llama(ps[0], "RobaUiParaVentana", 5);
        ventanas[0] = null; ui[0] = nav; ver[0] = 6; CuadroHasta(ps[0], 6);
        Bitmap busca2 = Compone(Otro(so), W, mainH);
        ui[0] = busca2; ver[0] = 7; ventanas[0] = Ventana(false, null, true);
        c0 = creaciones; List<Envio> ab = CuadroHasta(ps[0], 7);
        IntPtr reu = (IntPtr)Toma(ps[0], "uiTex");
        Exige(reu == grande && creaciones == c0 && (IntPtr)Toma(ps[0], "winTex") == IntPtr.Zero && DistintosEnTextura(reu, busca2) == 0,
              "al abrir, la textura grande de la ultima ventana se reutiliza (sin crear ninguna) y queda con la imagen nueva");
        Exige(ConTextura(ab, reu).Count == 1, "y la ventana que se abre se anima con ella, como siempre");
    }
    static object[] ventanas = new object[2];
    static object Vacia() { return Activator.CreateInstance(TWin); }
    static object Dame(int k) { return ventanas[k] ?? Vacia(); }
    static T DameVentana0<T>() { return (T)Dame(0); }
    static T DameVentana1<T>() { return (T)Dame(1); }
    static Control Otro(Control so) { TSO.GetField("Query").SetValue(so, "otra"); TSO.GetField("Results").SetValue(so, Resultados("otra", 31)); return so; }

    // ======================= 5) ENCHUFADO =======================
    static void Enchufado()
    {
        Console.WriteLine("\n  5) ENCHUFADO DONDE TOCA\n");
        MethodInfo ens = Metodo(TF, "EnsureUiOverlay");
        Exige(Codigo.Llama(ens, "FirmasBuscador") && Codigo.Llama(ens, "ZonaBuscador"), "al rehacer la imagen, el formulario mira que trozo cambio");
        int lee, esc; Codigo.UsosDeCampo(ens, "uiOverlayZona", out lee, out esc);
        int lee2, esc2; Codigo.UsosDeCampo(ens, "uiOverlayZonaVer", out lee2, out esc2);
        Exige(esc > 0 && esc2 > 0, "y lo apunta junto a la version");
        Exige(Codigo.Llama(ens, "Clear") && Codigo.Llama(ens, "RenderContent"), "la imagen se sigue haciendo entera (limpiar y componer): no cambia como se dibuja");
        bool enganche = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF)) { int l, e; Codigo.UsosDeCampo(m, "GetUiCambio", out l, out e); if (e > 0) enganche = true; }
        Exige(enganche, "el panel sabe a quien preguntar el trozo (GetUiCambio)");
        Exige(Codigo.Llama(Metodo(TPanel, "RenderFrame"), "SubeUi"), "el cuadro sube la imagen por SubeUi (el trozo, o entera)");
        MethodInfo arranca = Metodo(TF, "StartWinTrans");
        Exige(Codigo.Llama(arranca, "RobaUiParaVentana") && Codigo.Llama(arranca, "Clone"), "al cerrar, primero se intenta sin copia; si no se puede, la copia de siempre");
        int lr, er; Codigo.UsosDeCampo(Metodo(TF, "BuildWinState"), "Robada", out lr, out er);
        Exige(er > 0, "y la ventana que se encoge sabe que va con la textura de la UI");
    }

    // En Linux (Mono) la rocola copia memoria con RtlMoveMemory, de Windows: se carga una COPIA con su .config que la lleva
    // a memmove (como en 'dibujoligero'). No se toca el ejecutable de las pruebas y la carpeta se borra al acabar.
    static string carpetaCopia;
    static string Cargable(string ruta)
    {
        if (Type.GetType("Mono.Runtime") == null || Environment.OSVersion.Platform == PlatformID.Win32NT) return ruta;
        string dir = Path.Combine(Path.GetTempPath(), "rocola_buscaligero_" + System.Diagnostics.Process.GetCurrentProcess().Id);
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
        if (TPanel == null) { Console.WriteLine("  OK    sin tarjeta grafica el buscador se pinta por el procesador, como siempre: nada que mirar"); return 0; }
        TF = a.GetType("Rocola.Form1"); TStar = a.GetType("Rocola.GpuStarfield"); TDev = a.GetType("Rocola.D3D9Device"); TD3D = a.GetType("Rocola.D3D9"); TWin = a.GetType("Rocola.GpuWin");
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)) { if (t.Name == "SearchOverlay") TSO = t; if (t.Name == "SearchItem") TItem = t; if (t.Name == "Album") TAlb = t; }
        if (TSO == null || TItem == null || TAlb == null || TWin == null || TWin.GetField("Robada") == null || Metodo(TF, "ZonaBuscador") == null || Metodo(TPanel, "SubeUi") == null)
        { Mal("faltan piezas del buscador ligero (ZonaBuscador, SubeUi, GpuWin.Robada...)"); return 1; }
        mRender = TSO.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) });
        portada = new Bitmap(320, 320, PixelFormat.Format32bppPArgb);
        using (Graphics pg = Graphics.FromImage(portada)) { pg.Clear(Color.FromArgb(255, 30, 40, 90)); for (int i = 0; i < 40; i++) using (SolidBrush sb = new SolidBrush(Color.FromArgb(90, 200, 120 + i, 255 - i))) pg.FillEllipse(sb, i * 7, i * 6, 60, 60); }
        form = Codigo.FormSinEncender(TF);
        try { ElTrozo(); } catch (Exception e) { Mal("el trozo: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { SoloElTrozo(); } catch (Exception e) { Mal("solo el trozo: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { CerrarYAbrir(); } catch (Exception e) { Mal("cerrar y abrir: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { Enchufado(); } catch (Exception e) { Mal("enchufado: " + e.GetType().Name + ": " + e.Message); }
        Console.WriteLine(fallos == 0 ? "\n  todo bien" : ("\n  " + fallos + " fallos"));
        return fallos;
    }
}
