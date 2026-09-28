using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Label = System.Windows.Forms.Label;

// EL RELOJ DE LA CANCION, SOLO SU RECUADRO (Rocola/FranjaLigeraGpu.cs).
//
// La franja de abajo (lo que suena, lo que sigue, el reloj, los creditos, la cola y la barra) se rehacia entera y se
// subia entera a la tarjeta cada segundo, porque el reloj cambia cada segundo. Ahora, si solo cambio el reloj o la barra,
// se repinta solo su recuadro y la tarjeta recibe solo ese trozo. Aqui se mira:
//   1) que la imagen queda EXACTAMENTE igual que rehaciendola entera, pixel a pixel, en 24 escenas (dos pantallas, letra
//      de lejos o no, textos que se deslizan o no, barra en la franja, en neon o dibujada por la tarjeta) y diez pasos de
//      reloj y barra en cada una (0:59 -> 1:00, 9:59 -> 10:00, 59:59 -> 1:00:00...);
//   2) que los textos que se deslizan (las cintas) quedan igual que rehaciendola entera: un recuadro no los borra;
//   3) que lo demas va entero como siempre: otro titulo, otros creditos, el reloj que se mueve, otra pantalla, la atraccion;
//   4) que el trozo solo vale para la version de justo antes;
//   5) con la tarjeta imitada: la tarjeta recibe solo el recuadro y su textura queda igual que la imagen; si se salto una
//      version, o cambio otra cosa, entera;
//   6) lo que cuesta, entera y por recuadro;
//   7) los enganches.
class FranjaReloj
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos, oks;
    static void Exige(bool c, string q) { Console.WriteLine((c ? "  OK    " : "  FALLO ") + q); if (c) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }

    static Type TF, TPanel, TStar, TDev, TD3D, TPast, TProg;

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

    // ======================= LA FRANJA, MONTADA COMO EN LA ROCOLA =======================
    sealed class Escena { public int W, H; public bool Lejos, Desliza; public int Barra; public string Titulo; public string Nombre; }
    static Bitmap portada;
    static Label Etiqueta(string t, float pt, Color c, Rectangle r, ContentAlignment a)
    {
        Label l = new Label(); l.AutoSize = false; l.Text = t; l.Font = new Font(FontFamily.GenericSansSerif, pt, FontStyle.Bold);
        l.ForeColor = c; l.BackColor = Color.Transparent; l.TextAlign = a; l.Bounds = r; return l;
    }
    static void PonSiHay(object o, string n, object v) { FieldInfo f = Campo(o.GetType(), n); if (f != null) f.SetValue(o, v); }
    static object Monta(Escena e, string reloj, double avance)
    {
        object f = Codigo.FormSinEncender(TF);
        Pon(f, "gpuPanel", Activator.CreateInstance(TPanel, true)); Pon(f, "gpuEnabled", true);
        Pon(f, "letraLejos", e.Lejos); Pon(f, "colorGenero", true); Pon(f, "textosDeslizantes", e.Desliza);
        Pon(f, "progresoFluido", e.Barra == 0); Pon(f, "neonIntenso", false); Pon(f, "progresoNeon", false);
        Pon(f, "attractOn", false);
        Pon(f, "tamanoFranjaPrueba", new Size(e.W, e.H));
        int txtX = 110, tarjetaY = e.H - 100, progW = e.W - txtX - 12 - 130 - 12 - 270;
        Label tit = Etiqueta(e.Titulo, 15f, Color.White, Rectangle.Empty, ContentAlignment.MiddleLeft);
        int hTit = Math.Max(24, tit.Font.Height + 1);
        Label art = Etiqueta("Vicente Fernandez", 11.5f, Color.FromArgb(140, 230, 160), Rectangle.Empty, ContentAlignment.MiddleLeft);
        int hArt = Math.Max(18, art.Font.Height + 1);
        Label sig = Etiqueta("Sigue:  Cielito Lindo", 10.5f, Color.FromArgb(170, 180, 200), Rectangle.Empty, ContentAlignment.MiddleLeft);
        int hSig = Math.Max(17, sig.Font.Height + 1);
        tit.Bounds = new Rectangle(txtX, tarjetaY, e.W / 2, hTit);
        art.Bounds = new Rectangle(txtX, tarjetaY + hTit, 400, hArt);
        sig.Bounds = new Rectangle(txtX, tarjetaY + hTit + hArt, 700, hSig);
        int filaY = Math.Min(e.H - 14, tarjetaY + hTit + hArt + hSig + 4);
        Label tiempo = Etiqueta(reloj, 13f, Color.White, new Rectangle(txtX + progW + 12, filaY - 6, 130, 24), ContentAlignment.TopLeft);
        Control cred = (Control)Activator.CreateInstance(TPast, true), cola = (Control)Activator.CreateInstance(TPast, true);
        cred.Bounds = new Rectangle(e.W - 12 - 120, tarjetaY, 120, 36); cola.Bounds = new Rectangle(e.W - 12 - 120 - 10 - 130, tarjetaY, 130, 36);
        PonSiHay(cred, "Etiqueta", "CREDITOS"); PonSiHay(cred, "Valor", "3");
        PonSiHay(cola, "Etiqueta", "EN COLA"); PonSiHay(cola, "Valor", "5"); PonSiHay(cola, "ConPortadas", true);
        PonSiHay(cola, "Portadas", new Image[] { portada, null, portada }); PonSiHay(cola, "Mas", 2);
        Label mej = Etiqueta("MEJORADA", 12f, Color.Silver, new Rectangle(txtX + 180, art.Top, 90, 22), ContentAlignment.MiddleLeft);
        mej.Visible = false;
        Pon(f, "lblNowTop", tit); Pon(f, "lblNow", art); Pon(f, "lblNext", sig); Pon(f, "lblTime", tiempo);
        Pon(f, "pastCred", cred); Pon(f, "pastCola", cola); Pon(f, "lblMejorada", mej); Pon(f, "pastGenero", null);
        Control progress = (Control)Activator.CreateInstance(TProg, true);
        progress.Bounds = new Rectangle(txtX, filaY, progW, 12);
        TProg.GetProperty("Value").SetValue(progress, avance, null);
        TProg.GetProperty("Neon").SetValue(progress, e.Barra == 2, null);
        TProg.GetProperty("Accent").SetValue(progress, Color.FromArgb(215, 45, 45), null);
        Pon(f, "progress", progress);
        return f;
    }
    static void PonReloj(object f, string reloj, double avance)
    {
        ((Label)Toma(f, "lblTime")).Text = reloj;
        TProg.GetProperty("Value").SetValue(Toma(f, "progress"), avance, null);
    }
    static int Pinta(object f) { return (int)Llama(f, "EnsureUiBotOverlay"); }
    static Bitmap Imagen(object f) { return (Bitmap)Toma(f, "uiBotOverlay"); }
    static int[] Pixeles(Bitmap b)
    {
        int w = b.Width, h = b.Height; int[] px = new int[w * h];
        BitmapData bd = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try { for (int y = 0; y < h; y++) Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), px, y * w, w); }
        finally { b.UnlockBits(bd); }
        return px;
    }
    static int Distintos(Bitmap a, Bitmap b)
    {
        if (a == null || b == null || a.Width != b.Width || a.Height != b.Height) return -1;
        int[] pa = Pixeles(a), pb = Pixeles(b); int n = 0;
        for (int i = 0; i < pa.Length; i++) if (pa[i] != pb[i]) n++;
        return n;
    }
    // Las cintas del grupo de la franja: lo que se desliza y donde.
    static string Cintas(object f)
    {
        Array grupos = (Array)Toma(f, "cintasTexto");
        if (grupos == null || grupos.Length == 0 || grupos.GetValue(0) == null) return "";
        IList items = (IList)Toma(grupos.GetValue(0), "Items");
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (object it in items) sb.Append((string)Toma(it, "Key")).Append('@').Append(((RectangleF)Toma(it, "Rect")).ToString()).Append(';');
        return sb.ToString();
    }

    static readonly string[] Relojes = { "0:58 / 4:12", "0:59 / 4:12", "1:00 / 4:12", "1:01 / 4:12", "9:59 / 12:03", "10:00 / 12:03",
                                         "10:01 / 12:03", "59:59 / 1:02:03", "1:00:00 / 1:02:03", "1:00:01 / 1:02:03" };
    static readonly double[] Avances = { 0, 0.001, 0.0042, 0.2, 0.2003, 0.5, 0.75, 0.999, 1.0, 0.33 };
    const string Largo = "Contrabando y Traicion (En Vivo desde el Auditorio Nacional, version extendida con mariachi)";

    // ======================= 1) Y 2) IGUAL QUE ENTERA =======================
    static void IgualQueEntera()
    {
        Console.WriteLine("\n  1) POR RECUADRO QUEDA IGUAL QUE REHECHA ENTERA, PIXEL A PIXEL\n");
        int[][] pant = { new int[] { 1024, 768 }, new int[] { 1920, 1080 } };
        int escenas = 0, pasos = 0, porRecuadro = 0, malos = 0, cintasMal = 0; long area = 0, entera = 0; string primero = null;
        foreach (int[] p in pant)
            foreach (bool lejos in new bool[] { false, true })
                foreach (bool desliza in new bool[] { false, true })
                    for (int barra = 0; barra < 3; barra++)
                    {
                        Escena e = new Escena(); e.W = p[0]; e.H = p[1]; e.Lejos = lejos; e.Desliza = desliza; e.Barra = barra; e.Titulo = Largo;
                        e.Nombre = p[0] + "x" + p[1] + (lejos ? ", de lejos" : "") + (desliza ? ", deslizando" : "") + (barra == 0 ? ", barra por la tarjeta" : barra == 1 ? ", barra en la franja" : ", barra neon en la franja");
                        escenas++;
                        object vivo = Monta(e, Relojes[0], Avances[0]); Pinta(vivo);
                        for (int k = 1; k < Relojes.Length; k++)
                        {
                            PonReloj(vivo, Relojes[k], Avances[k]);
                            Pinta(vivo);
                            Rectangle z = (Rectangle)Toma(vivo, "uiBotZona");
                            object nuevo = Monta(e, Relojes[k], Avances[k]); Pinta(nuevo);
                            int d = Distintos(Imagen(vivo), Imagen(nuevo));
                            bool cintasIgual = Cintas(vivo) == Cintas(nuevo);
                            pasos++;
                            if (!z.IsEmpty) { porRecuadro++; area += (long)z.Width * z.Height; }
                            entera += (long)Imagen(vivo).Width * Imagen(vivo).Height;
                            string q = null;
                            if (d != 0) q = d + " pixeles distintos de la rehecha entera (recuadro " + z + ")";
                            else if (z.IsEmpty) q = "no se hizo por recuadro";
                            if (!cintasIgual) { cintasMal++; if (q == null) q = "las cintas no quedan como rehaciendola entera"; }
                            if (q != null) { malos++; if (primero == null) primero = e.Nombre + ", " + Relojes[k - 1] + " -> " + Relojes[k] + ": " + q; }
                        }
                    }
        Exige(malos == 0, escenas + " escenas x " + (Relojes.Length - 1) + " pasos de reloj y barra (" + pasos + "): por recuadro, y la imagen queda identica a rehacerla entera" +
                          (primero != null ? " -- " + malos + " mal; el primero: " + primero : ""));
        Exige(cintasMal == 0, "y los textos que se deslizan quedan igual que rehaciendola entera (un recuadro no los borra)");
        Info("el recuadro es de media el " + F(100.0 * area / Math.Max(1, entera), "0.0") + " % de la franja (" + porRecuadro + " de " + pasos + " pasos por recuadro)");
        Exige(area * 5 < entera, "y es menos de la quinta parte de la franja (el reloj y la barra van en la misma fila)");

        // Solo la barra (el reloj igual): con la barra en la franja, por recuadro; si la pinta la tarjeta, ni se rehace.
        bool bien = true; string que = null;
        for (int barra = 0; barra < 3 && bien; barra++)
        {
            Escena e = new Escena(); e.W = 1024; e.H = 768; e.Barra = barra; e.Titulo = "Amor Eterno";
            object vivo = Monta(e, "2:10 / 4:12", 0.50); int v0 = Pinta(vivo);
            PonReloj(vivo, "2:10 / 4:12", 0.51);
            int v1 = Pinta(vivo); Rectangle z = (Rectangle)Toma(vivo, "uiBotZona");
            object nuevo = Monta(e, "2:10 / 4:12", 0.51); Pinta(nuevo);
            if (barra == 0) { if (v1 != v0) { bien = false; que = "con la barra por la tarjeta, mover la barra rehace la franja"; } }
            else if (v1 != v0 + 1 || z.IsEmpty || Distintos(Imagen(vivo), Imagen(nuevo)) != 0) { bien = false; que = "barra " + barra + ": recuadro " + z + ", " + Distintos(Imagen(vivo), Imagen(nuevo)) + " distintos"; }
        }
        Exige(bien, "si solo avanza la barra: en la franja, por su recuadro e identica; si la dibuja la tarjeta, la franja ni se rehace" + (que != null ? " -- " + que : ""));
    }

    // ======================= 3) LO DEMAS, ENTERA =======================
    static void LoDemasEntera()
    {
        Console.WriteLine("\n  3) LO DEMAS SE REHACE ENTERA, COMO SIEMPRE\n");
        string[] nom = { "otro titulo", "otros creditos", "el reloj se mueve de sitio", "otra pantalla", "otra cancion en la cola", "letra de lejos al vuelo" };
        int bien = 0; string mal = null;
        for (int c = 0; c < nom.Length; c++)
        {
            Escena e = new Escena(); e.W = 1024; e.H = 768; e.Barra = 1; e.Desliza = true; e.Titulo = Largo;
            object vivo = Monta(e, "1:00 / 4:12", 0.3); Pinta(vivo);
            PonReloj(vivo, "1:01 / 4:12", 0.3);   // y ADEMAS cambia el reloj: aun asi, entera
            if (c == 0) ((Label)Toma(vivo, "lblNowTop")).Text = "Amor Eterno";
            else if (c == 1) Pon(Toma(vivo, "pastCred"), "Valor", "4");
            else if (c == 2) { Label t = (Label)Toma(vivo, "lblTime"); t.Left += 7; }
            else if (c == 3) { Pon(vivo, "tamanoFranjaPrueba", new Size(1366, 768)); }
            else if (c == 4) Pon(Toma(vivo, "pastCola"), "Valor", "6");
            else if (c == 5) Pon(vivo, "letraLejos", true);
            Pinta(vivo);
            Rectangle z = (Rectangle)Toma(vivo, "uiBotZona");
            if (z.IsEmpty) bien++; else if (mal == null) mal = nom[c] + " -> recuadro " + z;
        }
        Exige(bien == nom.Length, "otro titulo, otros creditos o cola, el reloj que se mueve, otra pantalla, la letra de lejos: entera, aunque ademas cambie el reloj" + (mal != null ? " -- " + mal : ""));

        // La atraccion: la franja va limpia; al volver, entera e identica.
        {
            Escena e = new Escena(); e.W = 1024; e.H = 768; e.Barra = 1; e.Titulo = "Amor Eterno";
            object vivo = Monta(e, "1:00 / 4:12", 0.3); Pinta(vivo);
            Pon(vivo, "attractOn", true); PonReloj(vivo, "1:01 / 4:12", 0.31); Pinta(vivo);
            Rectangle z1 = (Rectangle)Toma(vivo, "uiBotZona");
            int[] px = Pixeles(Imagen(vivo)); bool limpia = true; foreach (int v in px) if (v != 0) { limpia = false; break; }
            PonReloj(vivo, "1:02 / 4:12", 0.32); Pinta(vivo);
            Pon(vivo, "attractOn", false); PonReloj(vivo, "1:03 / 4:12", 0.33); Pinta(vivo);
            Rectangle z2 = (Rectangle)Toma(vivo, "uiBotZona");
            object nuevo = Monta(e, "1:03 / 4:12", 0.33); Pinta(nuevo);
            Exige(z1.IsEmpty && limpia && z2.IsEmpty && Distintos(Imagen(vivo), Imagen(nuevo)) == 0,
                  "en la atraccion la franja queda limpia entera; al salir se rehace entera e identica (nunca por recuadro)");
        }
    }

    // ======================= 4) EL TROZO SOLO PARA LA VERSION DE JUSTO ANTES =======================
    static void SoloJustoAntes()
    {
        Console.WriteLine("\n  4) EL TROZO SOLO VALE PARA LA VERSION DE JUSTO ANTES\n");
        object f = Codigo.FormSinEncender(TF);
        Pon(f, "uiBotVer", 10); Pon(f, "uiBotZonaVer", 10); Pon(f, "uiBotZona", new Rectangle(700, 60, 140, 28));
        Func<int, Rectangle> cambio = (Func<int, Rectangle>)Delegate.CreateDelegate(typeof(Func<int, Rectangle>), f, Metodo(TF, "UiBotCambioDesde"));
        bool ok = cambio(9) == new Rectangle(700, 60, 140, 28) && cambio(8).IsEmpty && cambio(10).IsEmpty && cambio(-1).IsEmpty;
        Pon(f, "uiBotZonaVer", 9);
        ok = ok && cambio(9).IsEmpty;
        Exige(ok, "el formulario solo da el trozo a quien tiene la version de justo antes; a cualquier otra, nada (entera)");
    }

    // ======================= LA TARJETA IMITADA (como en 'buscaligero') =======================
    static uint Nada(IntPtr self) { return 0; }
    static uint Memoria(IntPtr self) { return 256u * 1024 * 1024; }
    static readonly List<object> vivos = new List<object>();
    static IntPtr Puntero(string tipo, string metodo)
    {
        Delegate del = Delegate.CreateDelegate(TD3D.GetNestedType(tipo, TODO), typeof(FranjaReloj).GetMethod(metodo, TODO));
        vivos.Add(del); return Marshal.GetFunctionPointerForDelegate(del);
    }
    static int DrawUp(IntPtr self, uint prim, uint count, IntPtr data, uint stride) { return 0; }
    static int SetRS(IntPtr self, uint a, uint b) { return 0; }
    static int SetU1(IntPtr self, uint a) { return 0; }
    static int SetTSS(IntPtr self, uint stage, uint type, uint val) { return 0; }
    static int SetSamp(IntPtr self, uint stage, uint type, uint val) { return 0; }
    static int SetTex(IntPtr self, uint stage, IntPtr t) { return 0; }
    static int Hr(IntPtr self) { return 0; }
    static int Present(IntPtr self, IntPtr a, IntPtr b, IntPtr c, IntPtr d) { return 0; }
    static void Enchufa(object d, string campo, string metodo)
    {
        FieldInfo f = Campo(TDev, campo);
        Delegate del = Delegate.CreateDelegate(f.FieldType, typeof(FranjaReloj).GetMethod(metodo, TODO));
        vivos.Add(del); f.SetValue(d, del);
    }
    sealed class TexImitada { public int W, H; public IntPtr Mem; }
    static readonly Dictionary<long, TexImitada> creadas = new Dictionary<long, TexImitada>();
    static int creaciones;
    sealed class Bloqueo { public IntPtr Tex; public Rectangle Zona; }
    static readonly List<Bloqueo> bloqueos = new List<Bloqueo>();
    static IntPtr vtTex, vtTrozos; static int offPitch, offBits;
    static Delegate AlVuelo(string tipo, string destino, bool conRect)
    {
        Type td = TD3D.GetNestedType(tipo, TODO);
        ParameterInfo[] ps = td.GetMethod("Invoke").GetParameters();
        Type[] pt = new Type[ps.Length]; for (int i = 0; i < ps.Length; i++) pt[i] = ps[i].ParameterType;
        DynamicMethod dm = new DynamicMethod(destino + "Imitado", typeof(int), pt, typeof(FranjaReloj).Module, true);
        ILGenerator il = dm.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Conv_U); il.Emit(OpCodes.Ldarg_3);
        if (conRect) il.Emit(OpCodes.Conv_U);
        il.Emit(OpCodes.Call, typeof(FranjaReloj).GetMethod(destino, TODO));
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
        Marshal.WriteIntPtr(lr, offBits, new IntPtr(t.Mem.ToInt64() + (long)z.Top * t.W * 4 + z.Left * 4));
        return 0;
    }
    static int Desbloquea(IntPtr self, uint level) { return 0; }
    static uint Suelta(IntPtr self) { TexImitada t; if (creadas.TryGetValue(self.ToInt64(), out t)) { creadas.Remove(self.ToInt64()); Marshal.FreeHGlobal(t.Mem); } return 0; }
    static void PorTrozos(IntPtr tex) { Marshal.WriteIntPtr(tex, vtTrozos); }
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
    // Cuadros hasta que la tarjeta tenga la version 'version' de la franja; en 'bloqueos' quedan los de todos.
    static void CuadroHasta(Control p, int version)
    {
        List<Bloqueo> todos = new List<Bloqueo>();
        Stopwatch reloj = Stopwatch.StartNew();
        do
        {
            bloqueos.Clear();
            if (!(bool)Llama(p, "RenderFrame")) throw new Exception("el cuadro no se presenta");
            todos.AddRange(bloqueos);
            if ((int)Toma(p, "uiBotVer") == version) break;
            System.Threading.Thread.Sleep(10);
        } while (reloj.ElapsedMilliseconds < 5000);
        bloqueos.Clear(); bloqueos.AddRange(todos);
    }
    static List<Bloqueo> De(IntPtr tex) { List<Bloqueo> r = new List<Bloqueo>(); foreach (Bloqueo b in bloqueos) if (b.Tex == tex) r.Add(b); return r; }

    // ======================= 5) LA TARJETA RECIBE SOLO EL RECUADRO =======================
    static void LaTarjeta()
    {
        Console.WriteLine("\n  5) LA TARJETA RECIBE SOLO EL RECUADRO, Y SU TEXTURA QUEDA IGUAL QUE LA IMAGEN\n");
        const int W = 1024, H = 768;
        Escena e = new Escena(); e.W = W; e.H = H; e.Barra = 1; e.Desliza = true; e.Titulo = Largo;
        object f = Monta(e, "0:58 / 4:12", 0.2);
        Control p = Panel(W, H);
        Pon(p, "GetUiBot", (Func<Bitmap>)delegate { return Imagen(f); });
        Pon(p, "GetUiBotVersion", Delegate.CreateDelegate(typeof(Func<int>), f, Metodo(TF, "EnsureUiBotOverlay")));
        Pon(p, "GetUiBotCambio", Delegate.CreateDelegate(typeof(Func<int, Rectangle>), f, Metodo(TF, "UiBotCambioDesde")));
        CuadroHasta(p, 1);
        IntPtr tex = (IntPtr)Toma(p, "uiBotTex");
        bool entera = false; foreach (Bloqueo b in De(tex)) if (b.Zona.IsEmpty) entera = true;
        Exige(tex != IntPtr.Zero && entera && DistintosEnTextura(tex, Imagen(f)) == 0, "la primera vez se crea la textura de la franja y se sube entera");
        PorTrozos(tex);
        int ok = 0, n = 0; string mal = null; long bytes = 0, bytesEntera = 0;
        for (int k = 1; k < Relojes.Length; k++)
        {
            PonReloj(f, Relojes[k], Avances[k]);
            int c0 = creaciones;
            int ver = (int)Toma(f, "uiBotVer") + 1;
            CuadroHasta(p, ver);
            Rectangle z = (Rectangle)Toma(f, "uiBotZona");
            IntPtr tex2 = (IntPtr)Toma(p, "uiBotTex");
            List<Bloqueo> bs = De(tex);
            bool trozo = bs.Count == 1 && !z.IsEmpty && bs[0].Zona == z;
            int dist = DistintosEnTextura(tex2, Imagen(f));
            n++;
            if (trozo && tex2 == tex && creaciones == c0 && dist == 0) ok++;
            else if (mal == null) mal = Relojes[k] + ": " + bs.Count + " bloqueos (" + (bs.Count > 0 ? bs[0].Zona.ToString() : "") + "), recuadro " + z + ", " + dist + " pixeles distintos";
            bytes += (long)z.Width * z.Height * 4; bytesEntera += (long)Imagen(f).Width * Imagen(f).Height * 4;
        }
        Exige(ok == n, "en " + n + " pasos de reloj la tarjeta recibe solo el recuadro, en la misma textura, y queda igual que la imagen" + (mal != null ? " -- " + mal : ""));
        Info("lo que se sube cada segundo: " + F(bytes / 1024.0 / n, "0.0") + " KB en vez de " + F(bytesEntera / 1024.0 / n, "0") + " KB");
        // Otra cosa (el titulo) y el reloj a la vez: entera.
        ((Label)Toma(f, "lblNowTop")).Text = "Amor Eterno"; PonReloj(f, "1:01:02 / 1:02:03", 0.98);
        CuadroHasta(p, (int)Toma(f, "uiBotVer") + 1);
        IntPtr tex3 = (IntPtr)Toma(p, "uiBotTex");
        bool ent = false; foreach (Bloqueo b in bloqueos) if ((b.Tex == tex || b.Tex == tex3) && b.Zona.IsEmpty) ent = true;
        Exige(ent && DistintosEnTextura(tex3, Imagen(f)) == 0, "si cambia otra cosa, se sube entera y queda igual");
        tex = tex3; PorTrozos(tex);
        // La tarjeta se salta una version: entera, aunque se sepa el recuadro.
        PonReloj(f, "1:01:03 / 1:02:03", 0.98); Pinta(f);
        PonReloj(f, "1:01:04 / 1:02:03", 0.98);
        CuadroHasta(p, (int)Toma(f, "uiBotVer") + 1);
        IntPtr tex4 = (IntPtr)Toma(p, "uiBotTex");
        bool ent2 = false; foreach (Bloqueo b in bloqueos) if ((b.Tex == tex || b.Tex == tex4) && b.Zona.IsEmpty) ent2 = true;
        Exige(!((Rectangle)Toma(f, "uiBotZona")).IsEmpty && ent2 && DistintosEnTextura(tex4, Imagen(f)) == 0,
              "si la tarjeta no tiene la version de justo antes, aunque se sepa el recuadro, se sube entera");
    }

    // ======================= 6) LO QUE CUESTA =======================
    static void Coste()
    {
        Console.WriteLine("\n  6) LO QUE CUESTA REHACERLA\n");
        Escena e = new Escena(); e.W = 1920; e.H = 1080; e.Barra = 1; e.Desliza = true; e.Titulo = Largo;
        object f = Monta(e, "0:00 / 4:12", 0); Pinta(f);
        Label tit = (Label)Toma(f, "lblNowTop");
        double entera = double.MaxValue, recuadro = double.MaxValue;
        for (int r = 0; r < 5; r++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 20; i++) { tit.Text = Largo + (i % 2 == 0 ? " " : ""); PonReloj(f, "1:" + (i % 60).ToString("00") + " / 4:12", 0.2 + i * 0.01); Pinta(f); }
            sw.Stop(); entera = Math.Min(entera, sw.Elapsed.TotalMilliseconds / 20);
            sw = Stopwatch.StartNew();
            for (int i = 0; i < 20; i++) { PonReloj(f, "2:" + (i % 60).ToString("00") + " / 4:12", 0.4 + i * 0.01); Pinta(f); }
            sw.Stop(); recuadro = Math.Min(recuadro, sw.Elapsed.TotalMilliseconds / 20);
        }
        Info("rehacer la franja a 1920x1080: entera " + F(entera, "0.00") + " ms, solo el recuadro del reloj " + F(recuadro, "0.00") + " ms (en este procesador; en el del bar, varias veces mas)");
        Exige(recuadro < entera, "por recuadro cuesta menos que entera");
    }

    // ======================= 7) ENCHUFADO =======================
    static void Enchufado()
    {
        Console.WriteLine("\n  7) ENCHUFADO DONDE TOCA\n");
        MethodInfo ens = Metodo(TF, "EnsureUiBotOverlay");
        Exige(Codigo.Llama(ens, "ZonaFranja") && Codigo.Llama(ens, "ApuntaFranja") && Codigo.Llama(ens, "EmpiezaRecuadro") && Codigo.Llama(ens, "CapturaRecuadro"),
              "al rehacer la franja se mira que cambio, se vacia solo el recuadro, las cintas no se tocan y se apunta que trozo cambio");
        int l, esc; Codigo.UsosDeCampo(Metodo(TF, "UiBotSig"), "uiBotSigFija", out l, out esc);
        Exige(esc >= 2, "la firma apunta aparte la de todo menos el reloj y la barra (tambien en la atraccion)");
        bool enganche = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF)) { int a, b; Codigo.UsosDeCampo(m, "GetUiBotCambio", out a, out b); if (b > 0) enganche = true; }
        Exige(enganche, "el panel sabe a quien preguntar el trozo de la franja (GetUiBotCambio)");
        Exige(Codigo.Llama(Metodo(TPanel, "RenderFrame"), "SubeUiBot") && Codigo.Llama(Metodo(TPanel, "SubeUiBot"), "UpdateOverlayRegion"),
              "el cuadro sube la franja por SubeUiBot: el trozo, o entera");
    }

    // En Linux (Mono) la rocola copia memoria con RtlMoveMemory, de Windows: se carga una COPIA con su .config que la lleva
    // a memmove (como en 'buscaligero'). No se toca el ejecutable de las pruebas y la carpeta se borra al acabar.
    static string carpetaCopia;
    static string Cargable(string ruta)
    {
        if (Type.GetType("Mono.Runtime") == null || Environment.OSVersion.Platform == PlatformID.Win32NT) return ruta;
        string dir = Path.Combine(Path.GetTempPath(), "rocola_franjareloj_" + Process.GetCurrentProcess().Id);
        Directory.CreateDirectory(dir);
        string destino = Path.Combine(dir, Path.GetFileName(ruta));
        File.Copy(ruta, destino, true);
        string dirOrigen = Path.GetDirectoryName(Path.GetFullPath(ruta));
        foreach (string dll in new string[] { "NAudio.dll", "NLayer.dll" })
            if (File.Exists(Path.Combine(dirOrigen, dll))) File.Copy(Path.Combine(dirOrigen, dll), Path.Combine(dir, dll), true);
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
        if (TPanel == null) { Console.WriteLine("  OK    sin tarjeta grafica la franja la pinta el procesador, como siempre: nada que mirar"); return 0; }
        TF = a.GetType("Rocola.Form1"); TStar = a.GetType("Rocola.GpuStarfield"); TDev = a.GetType("Rocola.D3D9Device"); TD3D = a.GetType("Rocola.D3D9");
        TPast = TF.GetNestedType("PastillaView", TODO); TProg = TF.GetNestedType("ProgressView", TODO);
        if (Metodo(TF, "ZonaFranja") == null || Metodo(TPanel, "SubeUiBot") == null || TPast == null || TProg == null)
        { Exige(false, "faltan piezas de la franja por recuadro (ZonaFranja, SubeUiBot)"); return 1; }
        portada = new Bitmap(96, 96, PixelFormat.Format32bppPArgb);
        using (Graphics pg = Graphics.FromImage(portada)) { pg.Clear(Color.FromArgb(255, 30, 40, 90)); for (int i = 0; i < 12; i++) using (SolidBrush sb = new SolidBrush(Color.FromArgb(90, 200, 120 + i * 9, 255 - i * 9))) pg.FillEllipse(sb, i * 6, i * 5, 30, 30); }
        try { IgualQueEntera(); } catch (Exception e) { Exige(false, "igual que entera: " + e); }
        try { LoDemasEntera(); } catch (Exception e) { Exige(false, "lo demas entera: " + e); }
        try { SoloJustoAntes(); } catch (Exception e) { Exige(false, "justo antes: " + e); }
        try { LaTarjeta(); } catch (Exception e) { Exige(false, "la tarjeta: " + e); }
        try { Coste(); } catch (Exception e) { Exige(false, "el coste: " + e); }
        try { Enchufado(); } catch (Exception e) { Exige(false, "enchufado: " + e); }
        Console.WriteLine(fallos == 0 ? ("\n  todo bien (" + oks + " comprobaciones)") : ("\n  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}
