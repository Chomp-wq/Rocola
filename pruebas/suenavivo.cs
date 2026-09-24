using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// LA PANTALLA VIVA, TANDA 3: LO QUE SUENA Y LOS CREDITOS (Rocola/SuenaVivoGpu.cs; solo tarjeta grafica)
//
// Lo que vigila:
//  1) EL DIBUJO POR PROCESADOR NO CAMBIA. La capsula de creditos con una moneda entrando, pintada por el camino del
//     procesador, sale pixel a pixel igual que la del perfil sin tarjeta grafica. El panel de reposo con la idea apagada
//     sale igual que el de siempre. Y sin tarjeta grafica los enganches no hacen nada: el letrero naranja, el "+4
//     creditos" de arriba y el aviso de 1,3 s siguen como siempre.
//  2) LOS AVISOS QUE SE ENTIENDEN: el tipo segun el color con que los pide cada sitio, los dos renglones, el de la
//     cancion pedida ("Pusiste «El Rey»" y cuanto falta), el dibujo (color, icono, texto), la entrada por la derecha
//     y la linea de lo que les queda; que sustituyen al de siempre y que, apagados, vuelve el de siempre.
//  3) SIN CREDITOS, INVITAR: la tarjeta sale en vez del letrero naranja (y no con la franja tapada), insistir la alarga
//     sin volver a entrar, al llegar creditos se va, la moneda de la ranura va y viene, y NO encola nada sola.
//  4) LA MONEDA QUE CAE: se entera sola de cada moneda (y de los creditos de servicio), cae acelerando hasta el numero,
//     la capsula da su golpe solo en la imagen de la tarjeta, la monedita de dentro no se pinta, sube "+4 creditos" y
//     el letrero de arriba no sale.
//  5) LO QUE SUENA, EN GRANDE: el texto se corre para el vinilo (y la insignia con el), el titulo en una linea que se
//     desliza si no cabe (quieto, va, se para, vuelve, sin saltos), cuanto falta, la pastilla del genero y los minutos
//     de cada una de la cola (los mismos de la cola del disco), y que el panel se entera cuando cambian.
//  6) Ctrl+F3 -> Pantalla: las dos hojas, los cuatro interruptores, 16 combinaciones guardadas y aplicadas.
//  7) Lo que cuesta cada cosa (Mono: para comparar entre ellas, no son los milisegundos del bar).
class SuenaVivo
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type form, gpu, pvT, itemT, trackT, avisoT, invitaT, monedaT, reposoT, grandeT, tipoT;
    static Assembly asm, asmCpu;
    static int fallos, oks;
    static string dirPng;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string Fm(double v, string f) { return v.ToString(f, System.Globalization.CultureInfo.InvariantCulture); }
    static FieldInfo Campo(Type t, string n) { for (Type x = t; x != null; x = x.BaseType) { FieldInfo fi = x.GetField(n, F | BindingFlags.DeclaredOnly); if (fi != null) return fi; } return null; }
    static object Get(object o, string n) { return Campo(o.GetType(), n).GetValue(o); }
    static void Set(object o, string n, object v) { Campo(o.GetType(), n).SetValue(o, v); }
    static object St(Type t, string n, params object[] a) { return t.GetMethod(n, F).Invoke(null, a); }
    static object Run(object o, string n, params object[] a) { return o.GetType().GetMethod(n, F).Invoke(o, a); }
    static object Prop(object o, string n) { return o.GetType().GetProperty(n, F).GetValue(o, null); }
    static T Cte<T>(Type t, string n) { return (T)t.GetField(n, F).GetValue(null); }
    static Control Find(Control p, string name) { foreach (Control c in p.Controls) { if (c.Text == name) return c; Control r = Find(c, name); if (r != null) return r; } return null; }
    static int Distintos(Bitmap a, Bitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height) return -1;
        int n = 0;
        for (int y = 0; y < a.Height; y++) for (int x = 0; x < a.Width; x++) if (a.GetPixel(x, y).ToArgb() != b.GetPixel(x, y).ToArgb()) n++;
        return n;
    }
    // Cuantos pixeles de un color parecido (del mismo tono y opacos) hay en una caja.
    static int Parecidos(Bitmap b, Rectangle r, Color c, int tol)
    {
        int n = 0;
        for (int y = Math.Max(0, r.Top); y < Math.Min(b.Height, r.Bottom); y++)
            for (int x = Math.Max(0, r.Left); x < Math.Min(b.Width, r.Right); x++)
            {
                Color p = b.GetPixel(x, y);
                if (p.A > 200 && Math.Abs(p.R - c.R) <= tol && Math.Abs(p.G - c.G) <= tol && Math.Abs(p.B - c.B) <= tol) n++;
            }
        return n;
    }
    // Pixeles del azul de "en ~6 min": claramente mas azules que rojos y claros.
    static int Azules(Bitmap b, Rectangle r)
    {
        int n = 0;
        for (int y = Math.Max(0, r.Top); y < Math.Min(b.Height, r.Bottom); y++)
            for (int x = Math.Max(0, r.Left); x < Math.Min(b.Width, r.Right); x++)
            {
                Color p = b.GetPixel(x, y);
                if (p.A > 150 && p.B > 170 && p.B - p.R > 60 && p.G > 140) n++;
            }
        return n;
    }
    static Bitmap MasDeReferencia(object f, string txt)
    {
        Font fm = (Font)Get(f, "fMonedaMas");
        Size tm = (Size)St(form, "TamanoMas", txt, fm);
        Bitmap b = new Bitmap(tm.Width, tm.Height, PixelFormat.Format32bppPArgb);
        St(form, "PintaMas", b, txt, fm);
        return b;
    }
    static object Enum(string nombre) { return System.Enum.Parse(tipoT, nombre); }
    static Bitmap Liso(Color c, int lado)
    {
        Bitmap bm = new Bitmap(lado, lado);
        using (Graphics gg = Graphics.FromImage(bm)) gg.Clear(c);
        return bm;
    }
    static object Form() { object f = Codigo.FormSinEncender(form); Codigo.PonCerrojos(f); Codigo.PonColecciones(f); return f; }
    static object ConTarjeta()
    {
        object f = Form();
        Control pg = (Control)Activator.CreateInstance(gpu, true);
        Set(f, "gpuPanel", pg); Set(f, "gpuEnabled", true);
        Label t = new Label(); t.Font = new Font(FontFamily.GenericSansSerif, 14f, FontStyle.Bold);
        Set(f, "lblToast", t);
        Tamano(f, 1024, 768);
        return f;
    }
    // La ventana armada sin constructor no se puede redimensionar (se pondria a acomodar controles que no existen): se
    // le pone el tamaño por dentro, que es lo unico que leen los avisos y la invitacion.
    static void Tamano(object f, int w, int h)
    {
        FieldInfo cs = typeof(Control).GetField("client_size", F), cr = typeof(Control).GetField("client_rect", F), bo = typeof(Control).GetField("bounds", F);
        if (cs != null) cs.SetValue(f, new Size(w, h));
        if (cr != null) cr.SetValue(f, new Rectangle(0, 0, w, h));
        if (bo != null) bo.SetValue(f, new Rectangle(0, 0, w, h));
    }
    static Control Capsula(Type t, string etq, string valor)
    {
        Control pv = (Control)Activator.CreateInstance(t, true);
        pv.Size = new Size(170, 34); pv.Font = new Font(FontFamily.GenericSansSerif, 9f);
        Set(pv, "Etiqueta", etq); Set(pv, "Valor", valor); Set(pv, "Acento", Color.FromArgb(120, 240, 140));
        return pv;
    }
    // La moneda entro hace 'd' ms (de 3 a 7 creditos).
    static void Momento(Control c, int d)
    {
        int t0 = Environment.TickCount - d;
        Set(c, "monTick", t0); Set(c, "rodTick", t0 + 420); Set(c, "rodDe", "3"); Set(c, "Valor", "7");
    }
    static Bitmap PintaCapsula(Control pv, bool paraGpu)
    {
        Bitmap b = new Bitmap(230, 70, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(Color.FromArgb(14, 16, 30)); g.TranslateTransform(30, 18);
            pv.GetType().GetMethod(paraGpu ? "RenderContentGpu" : "RenderContent", F).Invoke(pv, new object[] { g });
        }
        return b;
    }
    static object Item(string titulo, bool video)
    {
        object it = Activator.CreateInstance(itemT, true);
        Set(it, "IsVideo", video);
        if (video) Set(it, "VideoPath", "c:/v/" + titulo + ".mp4");
        else
        {
            object t = Activator.CreateInstance(trackT, true);
            foreach (string n in new string[] { "Path", "path" }) { FieldInfo fi = Campo(trackT, n); if (fi != null) { fi.SetValue(t, "c:/m/Vicente/" + titulo + ".mp3"); break; } }
            FieldInfo ti = Campo(trackT, "Title"); if (ti != null) ti.SetValue(t, titulo);
            Set(it, "Song", t);
        }
        return it;
    }

    // ================================================================================================
    //  1) EL DIBUJO POR PROCESADOR NO CAMBIA
    // ================================================================================================
    static void Procesador()
    {
        Console.WriteLine("\n  1) EL DIBUJO POR PROCESADOR NO CAMBIA\n");
        Type cpuPv = asmCpu.GetType("Rocola.Form1").GetNestedType("PastillaView", F);
        Check(cpuPv != null && Campo(cpuPv, "MonedaGrande") == null && Campo(pvT, "MonedaGrande") != null, "se compara contra el perfil SIN tarjeta grafica, que ni tiene la moneda grande");
        // LA CAPSULA DE CREDITOS con una moneda entrando: por procesador, igual; por la tarjeta, sin la monedita y con golpe.
        Control a = Capsula(pvT, "Creditos", "3"), b = Capsula(cpuPv, "Creditos", "3");
        Set(a, "MonedaGrande", true);
        int peor = 0, gpuDistinta = 0, casos = 0;
        foreach (int d in new int[] { 0, 60, 150, 300, 410, 430, 520, 650, 900 })
        {
            // La capsula mira el reloj al pintarse: cada una se pinta con SU momento puesto justo antes, y como puede
            // caer en el milisegundo siguiente, se repite hasta que las dos caen igual (se queda la menor diferencia).
            int mejor = int.MaxValue;
            for (int intento = 0; intento < 25 && mejor > 0; intento++)
            {
                Bitmap pa, pb;
                Momento(a, d); pa = PintaCapsula(a, false);
                Momento(b, d); pb = PintaCapsula(b, false);
                mejor = Math.Min(mejor, Distintos(pa, pb)); pa.Dispose(); pb.Dispose();
            }
            peor = Math.Max(peor, mejor); casos++;
            int dif = int.MaxValue;
            for (int intento = 0; intento < 5; intento++)
            {
                Momento(a, d); Bitmap ga = PintaCapsula(a, true);
                Momento(b, d); Bitmap gb = PintaCapsula(b, false);
                dif = Math.Min(dif, Distintos(ga, gb)); ga.Dispose(); gb.Dispose();
            }
            if (dif > 30) gpuDistinta++;
        }
        Check(peor == 0, "la capsula de creditos, pintada por procesador, sale IDENTICA con una moneda entrando (" + casos + " momentos, con la moneda grande encendida)");
        Check(gpuDistinta >= 3, "(y en la imagen de la tarjeta si cambia: sin la monedita y con el golpe; " + gpuDistinta + " de " + casos + " momentos)");
        Set(a, "MonedaGrande", false);
        int igualSinIdea = 0;
        foreach (int d in new int[] { 60, 300, 520 })
        {
            int mejor = int.MaxValue;
            for (int intento = 0; intento < 25 && mejor > 0; intento++)
            {
                Momento(a, d); Bitmap pa = PintaCapsula(a, true);
                Momento(b, d); Bitmap pb = PintaCapsula(b, false);
                mejor = Math.Min(mejor, Distintos(pa, pb)); pa.Dispose(); pb.Dispose();
            }
            igualSinIdea = Math.Max(igualSinIdea, mejor);
        }
        Check(igualSinIdea == 0, "con la moneda que cae apagada, la imagen de la tarjeta es la de siempre (la monedita de dentro)");

        // EL PANEL DE REPOSO con la idea apagada: el de siempre.
        string[] qt = { "Cielito Lindo", "Contrabando y Traicion", "Evidencias" }, qa = { "Los Cadetes", "Los Tigres del Norte", "Ana Gabriel" };
        Image[] cov = { Liso(Color.SteelBlue, 60), null, Liso(Color.Crimson, 60) };
        object[] args = { 1024, 245, "Mi Gusto Es (En Vivo desde Guadalajara)", "Mariachi Vargas", "1:47 / 4:12", qt, qa, cov, 3, Color.MediumPurple, false, 0, false, 0 };
        MethodInfo lay = form.GetMethod("RenderIdlePanelLayout", F), gra = form.GetMethod("RenderIdlePanelGrande", F);
        using (Bitmap p1 = (Bitmap)lay.Invoke(null, args))
        {
            object[] args2 = { 1024, 245, "Mi Gusto Es (En Vivo desde Guadalajara)", "Mariachi Vargas", "1:47 / 4:12", qt, qa, cov, 3, Color.MediumPurple, false, 0, false, null, 0 };
            using (Bitmap p2 = (Bitmap)gra.Invoke(null, args2)) Check(Distintos(p1, p2) == 0, "el panel de reposo con la idea apagada sale identico al de siempre");
        }

        // SIN TARJETA, LOS ENGANCHES NO HACEN NADA.
        Type cf = asmCpu.GetType("Rocola.Form1");
        object fc = Codigo.FormSinEncender(cf);
        bool quietos = !(bool)cf.GetMethod("InvitaSinCredito", F).Invoke(fc, new object[] { null })
                    && !(bool)cf.GetMethod("MonedaSinAviso", F).Invoke(fc, null)
                    && (int)cf.GetMethod("VidaAviso", F).Invoke(fc, null) == 1300
                    && !(bool)cf.GetMethod("AvisoEnBuscador", F).Invoke(fc, new object[] { "x", true });
        Check(quietos, "sin tarjeta grafica: el letrero naranja sale, el \"+4 creditos\" de arriba tambien, y los avisos duran 1,3 s como siempre");
        Check(cf.GetNestedType("PastillaView", F).GetMethod("MoneditaTapada", F) == null && cf.GetMethod("HorneaAviso", F) == null,
              "y ni existe nada de dibujo nuevo en ese perfil");
    }

    // ================================================================================================
    //  2) LOS AVISOS QUE SE ENTIENDEN
    // ================================================================================================
    static void Avisos()
    {
        Console.WriteLine("\n  2) LOS AVISOS QUE SE ENTIENDEN\n");
        MethodInfo tdc = form.GetMethod("TipoDeColor", F);
        object[][] casos = {
            new object[] { Color.FromArgb(40, 165, 90), "Cancion agregada", "Hecho" },
            new object[] { Color.FromArgb(40, 130, 90), "Imagen actualizada", "Hecho" },
            new object[] { Color.FromArgb(200, 130, 35), "Inserta una moneda", "Cuidado" },
            new object[] { Color.FromArgb(150, 60, 45), "Repaso a fondo: ENCENDIDO", "Cuidado" },
            new object[] { Color.FromArgb(200, 80, 70), "No se pudo reproducir: credito devuelto", "Error" },
            new object[] { Color.FromArgb(40, 165, 90), "Carpetas actualizadas: escaneando musica...", "Novedad" },
            new object[] { Color.FromArgb(40, 165, 90), "Reanudando la musica", "Novedad" },
            new object[] { Color.FromArgb(60, 110, 220), "algo nuevo", "Novedad" },
        };
        bool tipos = true;
        foreach (object[] c in casos) if (tdc.Invoke(null, new object[] { c[0], c[1] }).ToString() != (string)c[2]) { tipos = false; Info("mal: " + c[1]); }
        Check(tipos, "cada aviso sale con su tipo por el color con que lo pide su sitio: hecho, cuidado, error o novedad (" + casos.Length + " casos)");
        // Los colores de TODOS los sitios que avisan, leidos del programa: ninguno se queda sin tipo razonable.
        MethodInfo part = form.GetMethod("PartirAviso", F);
        string[][] partes = {
            new string[] { "No se pudo reproducir: credito devuelto", "No se pudo reproducir", "credito devuelto" },
            new string[] { "Tu cancion es la 3a - en ~9 min", "Tu cancion es la 3a", "en ~9 min" },
            new string[] { "Esa ya va seguida", "Esa ya va seguida", "" },
            new string[] { "Modo noche: el volumen baja 6 dB", "Modo noche", "el volumen baja 6 dB" },
            new string[] { "raro:", "raro:", "" },
        };
        bool dos = true;
        foreach (string[] p in partes)
        {
            object[] a = { p[0], null, null }; part.Invoke(null, a);
            if ((string)a[1] != p[1] || (string)a[2] != p[2]) { dos = false; Info("mal: " + p[0] + " -> " + a[1] + " | " + a[2]); }
        }
        Check(dos, "en dos renglones: lo que paso arriba y el detalle debajo (se parte por \": \" o por \" - \")");
        MethodInfo ped = form.GetMethod("TextoPedido", F);
        object[] q1 = { "El Rey", 3, 540.0, false, false, null, null }; ped.Invoke(null, q1);
        object[] q2 = { "El Rey", 1, 20.0, false, false, null, null }; ped.Invoke(null, q2);
        object[] q3 = { "El Rey", 1, -1.0, true, false, null, null }; ped.Invoke(null, q3);
        object[] q4 = { "", 2, -1.0, false, true, null, null }; ped.Invoke(null, q4);
        Info((string)q1[5] + " / " + (string)q1[6] + "   ·   " + (string)q2[6] + "   ·   " + (string)q3[6] + "   ·   " + (string)q4[5] + " / " + (string)q4[6]);
        Check((string)q1[5] == "Pusiste «El Rey»" && (string)q1[6] == "va la 3.ª en la cola · en ~9 min" && (string)q2[6] == "es la siguiente · en menos de 1 min"
              && (string)q3[6] == "ya está sonando" && (string)q4[5] == "Pusiste tu video" && (string)q4[6] == "va la 2.ª en la cola",
              "la cancion pedida: \"Pusiste «El Rey»\" y debajo cual es y cuanto falta, sin inventar minutos que no se saben");

        // El dibujo: su color, su icono y su texto.
        Font ft = new Font(FontFamily.GenericSansSerif, 14f, FontStyle.Bold), fd = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Bold);
        bool dibujo = true;
        foreach (string tipo in new string[] { "Hecho", "Cuidado", "Error", "Novedad" })
        {
            Size s = (Size)St(form, "TamanoAviso", 1024, "Pusiste «El Rey»", "va la 3.ª en la cola · en ~9 min", ft, fd);
            using (Bitmap bm = new Bitmap(s.Width, s.Height, PixelFormat.Format32bppPArgb))
            {
                St(form, "PintaAviso", bm, Enum(tipo), "Pusiste «El Rey»", "va la 3.ª en la cola · en ~9 min", ft, fd);
                Color col = (Color)St(form, "ColorAviso", Enum(tipo));
                int borde = Parecidos(bm, new Rectangle(0, 0, bm.Width, 4), col, 40) + Parecidos(bm, new Rectangle(0, bm.Height - 4, bm.Width, 4), col, 40);
                int icono = Parecidos(bm, new Rectangle(10, 6, 60, bm.Height - 12), col, 40);
                int blanco = Parecidos(bm, new Rectangle(70, 6, bm.Width - 80, bm.Height / 2), Color.White, 20);
                bool esquina = bm.GetPixel(0, 0).A == 0 && bm.GetPixel(bm.Width - 1, bm.Height - 1).A == 0;
                if (borde < 100 || icono < 40 || blanco < 60 || !esquina) { dibujo = false; Info(tipo + ": borde " + borde + ", icono " + icono + ", texto " + blanco); }
                bm.Save(Path.Combine(dirPng, "aviso_" + tipo + ".png"), ImageFormat.Png);
            }
        }
        Check(dibujo, "cada tipo con su color en el borde y en el icono, el texto en blanco y las esquinas redondas con aire transparente");
        Size corto = (Size)St(form, "TamanoAviso", 1920, "Hola", "", ft, fd), largo = (Size)St(form, "TamanoAviso", 1024, new string('x', 200), "", ft, fd);
        Check(corto.Width <= 310 && corto.Height < ((Size)St(form, "TamanoAviso", 1024, "a", "b", ft, fd)).Height && largo.Width <= (int)(1024 * .40f) + 6,
              "mide lo que pide el texto: un renglon es mas bajo que dos, y nunca pasa del 40 % de la pantalla");

        // La curva: entra por la derecha frenando, se queda, y se va hacia la derecha apagandose.
        MethodInfo cur = gpu.GetMethod("CurvaAviso", F);
        int ms = Cte<int>(form, "AvisoMs"), entra = Cte<int>(form, "AvisoEntraMs");
        float corre = Cte<float>(form, "AvisoCorre");
        object[] c0 = { 0.0, 0f, 0f }, c1 = { (double)entra, 0f, 0f }, c2 = { ms * .5, 0f, 0f }, c3 = { (double)(ms - 1), 0f, 0f }, cm = { entra * .5, 0f, 0f };
        cur.Invoke(null, c0); cur.Invoke(null, c1); cur.Invoke(null, c2); cur.Invoke(null, c3); cur.Invoke(null, cm);
        bool liso = true; float antA = -1, antX = -1;
        for (double t = 0; t < ms; t += 16.7)
        {
            object[] ci = { t, 0f, 0f }; cur.Invoke(null, ci);
            float al = (float)ci[1], dx = (float)ci[2];
            if (antA >= 0 && (Math.Abs(al - antA) > .25f || Math.Abs(dx - antX) > 15f)) liso = false;
            antA = al; antX = dx;
        }
        Check((float)c0[1] == 0f && (float)c0[2] == corre && (float)c1[1] == 1f && (float)c1[2] == 0f && (float)c2[1] == 1f && (float)c3[1] < .02f && (float)cm[2] < corre * .5f,
              "entra desde " + corre + " px a la derecha frenando (a mitad de su entrada ya lleva mas de la mitad), se queda, y se va apagandose");
        Check(liso, "y a 60 cuadros por segundo no da ningun salto (como mucho un cuarto de su opacidad y 15 px de un cuadro al siguiente, al empezar a entrar)");
        Check(ms >= 2400 && ms <= 3600, "dura " + Fm(ms / 1000.0, "0.0") + " s: da para leer dos renglones (el de siempre, 1,3 s)");

        // Sustituye al de siempre... y apagado vuelve el de siempre.
        object f = ConTarjeta();
        Set(f, "avisosClaros", true);
        Run(f, "HorneaToast", "No se pudo reproducir: credito devuelto", Color.FromArgb(200, 80, 70));
        object av = Run(f, "BuildAvisoState");
        Color rojo = (Color)St(form, "ColorAviso", Enum("Error"));
        uint colAv = (uint)Get(av, "Color");
        Check((bool)Get(av, "Has") && (int)Get(f, "toastTick") < 0 && colAv == (0xFF000000u | ((uint)rojo.R << 16) | ((uint)rojo.G << 8) | rojo.B),
              "con avisos claros, el aviso rojo de \"no se pudo\" sale como error y el de siempre no se hornea");
        float x = (float)Get(av, "X");
        Bitmap bav = (Bitmap)Get(av, "Bmp");
        Check(x + bav.Width <= 1024 - 10 && x + bav.Width >= 1024 - 30 && (float)Get(av, "Y") == Cte<float>(form, "AvisoArriba"), "arriba a la derecha, a la altura del de siempre");
        Check((int)Run(f, "VidaAviso") == ms, "y mientras se ve, las monedas y canciones seguidas se siguen sumando (la cuenta dura lo que el aviso)");
        Set(f, "avisosClaros", false);
        Run(f, "HorneaToast", "Imagen actualizada", Color.FromArgb(40, 130, 90));
        Check((int)Get(f, "toastTick") >= 0 && (int)Run(f, "VidaAviso") == 1300, "apagado, vuelve el de siempre (y su cuenta de 1,3 s)");
        // El titulo a medida: se usa una sola vez.
        Set(f, "avisosClaros", true);
        Run(f, "PreparaAvisoPropio", Enum("Hecho"), "Pusiste «El Rey»", "va la 3.ª en la cola");
        Run(f, "HorneaToast", "Tu cancion es la 3a - en ~9 min", Color.FromArgb(40, 165, 90));
        bool usado = !(bool)Get(f, "avisoPropio");
        Run(f, "HorneaToast", "Imagen actualizada", Color.FromArgb(40, 130, 90));
        Check(usado && !(bool)Get(f, "avisoPropio"), "el aviso de la cancion pedida (\"Pusiste...\") se usa una sola vez: el siguiente vuelve a salir de su texto");
        // En el buscador tambien: y el buscador no lo pinta en su imagen.
        Type soT = form.GetNestedType("SearchOverlay", F);
        object so = Activator.CreateInstance(soT, true); Set(f, "searchOverlay", so);
        System.Windows.Forms.Timer tt = new System.Windows.Forms.Timer(); Set(f, "toastTimer", tt);
        int verAntes = (int)Get(f, "avisoVer");
        form.GetMethod("ShowSearchToast", F, null, new Type[] { typeof(string), typeof(bool) }, null).Invoke(f, new object[] { "Esa cancion ya va seguida", false });
        Check((int)Get(f, "avisoVer") == verAntes + 1 && !(bool)Get(so, "ToastOn"), "en el buscador, el aviso claro va encima de todo y el buscador no pinta el suyo");
        // El aviso de la cancion pedida sale en los tres sitios donde se pide algo.
        bool enganchado = Codigo.Llama(form.GetMethod("ShowSongAddedToast", F), "AvisoPedido") && Codigo.Llama(form.GetMethod("AddVideoToQueue", F), "AvisoPedido")
                       && Codigo.LeeCampo(Codigo.MetodosConAyudantes(form, "AvisoPedido"), "ultimoPedido") && Codigo.EscribeCampo(Codigo.MetodosConAyudantes(form, "AddItem"), "ultimoPedido");
        Check(enganchado, "y el de la cancion pedida sale al pedir una cancion (disco o buscador) o un video, con lo que se acaba de pedir");
        MethodInfo hta = form.GetMethod("HorneaToast", F);
        List<string> pasos = Codigo.Repasa(hta);
        Check(pasos.IndexOf("AvisoClaro") >= 0 && pasos.IndexOf("HorneaAviso") > pasos.IndexOf("AvisoClaro") && pasos.IndexOf("HorneaAviso") < pasos.IndexOf("PintaToast"),
              "el aviso claro se decide antes de hornear el de siempre (un solo sitio: todos los avisos pasan por aqui)");
    }

    // ================================================================================================
    //  3) SIN CREDITOS, INVITAR
    // ================================================================================================
    static void Invita()
    {
        Console.WriteLine("\n  3) SIN CREDITOS, INVITAR\n");
        object f = ConTarjeta();
        Set(f, "sinCreditosInvita", true); Set(f, "avisosClaros", true);
        object it = Item("Hermoso Carino", false);
        bool sale = (bool)Run(f, "InvitaSinCredito", it);
        object iv = Run(f, "BuildInvitaState");
        Bitmap bi = (Bitmap)Get(iv, "Bmp");
        Check(sale && (bool)Get(iv, "Has") && bi != null, "sin creditos sale la tarjeta que invita (y el que llama ya no pone el letrero naranja)");
        float xi = (float)Get(iv, "X");
        Check(Math.Abs(xi + bi.Width * .5f - 512f) < 1f && bi.Width >= 420 && bi.Width <= 620, "centrada, de " + bi.Width + " x " + bi.Height + " px en 1024x768");
        using (Bitmap copia = new Bitmap(bi)) copia.Save(Path.Combine(dirPng, "invita.png"), ImageFormat.Png);
        int oro = Parecidos(bi, new Rectangle(0, 0, bi.Width, bi.Height), Color.FromArgb(255, 232, 154), 30);
        Check(oro > 150, "con su titulo dorado (\"Te falta un credito\")");
        double d0 = (double)Get(iv, "Desde"), h0 = (double)Get(iv, "Hasta");
        System.Threading.Thread.Sleep(30);
        Run(f, "InvitaSinCredito", it);
        object iv2 = Run(f, "BuildInvitaState");
        Check((double)Get(iv2, "Desde") == d0 && (double)Get(iv2, "Hasta") > h0, "si insiste, la tarjeta no vuelve a entrar: solo se alarga");
        Set(f, "credits", 4);
        object iv3 = Run(f, "BuildInvitaState");
        double ahora = (double)St(gpu, "AhoraMs");
        Check((double)Get(iv3, "Hasta") <= ahora + Cte<int>(form, "InvitaSaleMs") + 1, "en cuanto hay creditos se va, sin esperar a su tiempo");
        Check(((IList)Get(f, "queue")).Count == 0, "y no pone nada en la cola sola: el credito lo gasta quien lo mete, en lo que elija");
        // Donde NO sale.
        Set(f, "credits", 0);
        Set(f, "sinCreditosInvita", false);
        bool apagada = (bool)Run(f, "InvitaSinCredito", it);
        bool avisoPropio = (bool)Get(f, "avisoPropio") && (string)Get(f, "avisoTitulo") == "Te falta un crédito";
        Set(f, "avisoPropio", false);
        Set(f, "sinCreditosInvita", true); Set(f, "attractOn", true); bool atrae = (bool)Run(f, "InvitaAPoner", it); Set(f, "attractOn", false);
        Set(f, "videoFull", true); bool video = (bool)Run(f, "InvitaAPoner", it); Set(f, "videoFull", false);
        Set(f, "gpuEnabled", false); bool sin = (bool)Run(f, "InvitaAPoner", it); Set(f, "gpuEnabled", true);
        Check(!apagada && avisoPropio, "apagada, vuelve el letrero de siempre (con avisos claros, \"Te falta un crédito\" en amarillo)");
        Check(!atrae && !video && !sin, "y no sale en modo atraccion, con un video a pantalla completa ni sin tarjeta grafica");
        // Los tres sitios donde se pide algo la usan.
        Check(Codigo.Llama(form.GetMethod("AddSong", F), "InvitaSinCredito") && Codigo.Llama(form.GetMethod("AddVideoToQueue", F), "InvitaSinCredito")
              && Codigo.Llama(form.GetMethod("ChooseSearchResult", F), "InvitaSinCredito"), "sale al pedir una cancion, un video o desde el buscador");
        // La moneda de la ranura: va y viene, un ciclo de 2,6 s.
        MethodInfo fase = gpu.GetMethod("FaseInvita", F);
        float antX = 1f; bool entra = true, seVe = false, seMete = false;
        for (double t = 400; t < 1560; t += 20)
        {
            object[] a = { t, 0f, 0f, 0f }; fase.Invoke(null, a);
            float mx = (float)a[2], ma = (float)a[3];
            if (mx > antX + 1e-4f) entra = false;
            antX = mx; if (ma > .9f) seVe = true;
        }
        object[] fin = { 2000.0, 0f, 0f, 0f }; fase.Invoke(null, fin); seMete = (float)fin[3] < .05f && (float)fin[2] == 0f;
        object[] otra = { 2600.0 + 400, 0f, 0f, 0f }, una = { 400.0, 0f, 0f, 0f }; fase.Invoke(null, otra); fase.Invoke(null, una);
        Check(entra && seVe && seMete && (float)otra[2] == (float)una[2], "la moneda llega desde la derecha, se deja ver, se mete en la ranura y vuelve a empezar (cada 2,6 s)");
    }

    // ================================================================================================
    //  4) LA MONEDA QUE CAE
    // ================================================================================================
    static void Moneda()
    {
        Console.WriteLine("\n  4) LA MONEDA QUE CAE\n");
        object f = ConTarjeta();
        Set(f, "monedaCae", true);
        Control pc = Capsula(pvT, "Creditos", "3"); pc.SetBounds(820, 690, 170, 34);
        Set(f, "pastCred", pc);
        object m0 = Run(f, "BuildMonedaState");
        Check(!(bool)Get(m0, "Has"), "sin moneda, nada");
        // La capsula arranca su ceremonia (asi la llaman la moneda y los creditos de servicio) y la tarjeta se entera sola.
        Run(pc, "Moneda"); Set(pc, "Valor", "7");
        object m1 = Run(f, "BuildMonedaState");
        float xn = 820 + (float)Run(pc, "XNumero");
        Check((bool)Get(m1, "Has"), "al entrar una moneda, la tarjeta se entera sola (por el reloj de la capsula, sin tocar a quien la mete)");
        Check(Math.Abs((float)Get(m1, "X1") - xn) < .5f && Math.Abs((float)Get(m1, "Y1") - 707f) < .5f && (float)Get(m1, "Y0") < 690f - 34f * 3f,
              "cae desde mas de tres capsulas de alto y aterriza en el numero (x " + Fm(xn, "0") + ")");
        object m2 = Run(f, "BuildMonedaState");
        Check((double)Get(m2, "Desde") == (double)Get(m1, "Desde"), "y una sola vez por moneda");
        Bitmap mas = (Bitmap)Get(m1, "Mas");
        using (Bitmap r4 = MasDeReferencia(f, "+4 créditos"))
            Check(mas != null && mas.Height > 10 && Distintos(mas, r4) == 0, "con su \"+4 créditos\" (de 3 a 7: lo que entro de verdad)");
        using (Bitmap c = new Bitmap(mas)) c.Save(Path.Combine(dirPng, "mas_creditos.png"), ImageFormat.Png);
        // Los creditos de servicio pasan por la misma ceremonia: dice lo que entro, no lo que da una moneda.
        int vista = (int)Get(pc, "monTick");
        Run(pc, "Moneda"); Set(pc, "Valor", "17");
        int otro = vista + 1; if (otro == 0) otro = 1; Set(pc, "monTick", otro);   // otra ceremonia aunque caiga en el mismo milisegundo
        object m3 = Run(f, "BuildMonedaState");
        using (Bitmap r10 = MasDeReferencia(f, "+10 créditos"))
            Check((bool)Get(m3, "Has") && (int)Get(m3, "MasVer") == (int)Get(m1, "MasVer") + 1 && Distintos((Bitmap)Get(m3, "Mas"), r10) == 0,
                  "y con 10 creditos de servicio (de 7 a 17), \"+10 créditos\"");
        // La caida: acelerando, girando, y aterriza.
        MethodInfo paso = gpu.GetMethod("PasoMoneda", F);
        object[] p0 = { 0f, 100f, 0f, 120f, 300f, 0f, 0f, 0f, 0f }, pm = { .5f, 100f, 0f, 120f, 300f, 0f, 0f, 0f, 0f }, p1 = { 1f, 100f, 0f, 120f, 300f, 0f, 0f, 0f, 0f };
        paso.Invoke(null, p0); paso.Invoke(null, pm); paso.Invoke(null, p1);
        bool gira = false; for (float u = 0; u <= 1f; u += .05f) { object[] pu = { u, 100f, 0f, 120f, 300f, 0f, 0f, 0f, 0f }; paso.Invoke(null, pu); if ((float)pu[7] < .3f) gira = true; }
        Check((float)p0[6] == 0f && (float)pm[6] < 150f && (float)p1[5] == 120f && (float)p1[6] == 300f && gira,
              "cae acelerando (a mitad de tiempo lleva menos de medio camino), gira (se aplasta a lo ancho) y aterriza justo en su sitio");
        // Aterriza cuando rueda el numero.
        Check((int)form.GetProperty("MonedaCaeMs", F).GetValue(null, null) == Cte<int>(pvT, "MonMs"),
              "cae en lo mismo que tarda la capsula en rodar su numero: aterriza justo cuando el numero cambia");
        // El letrero de arriba ya no sale mientras la moneda se ve.
        Check((bool)Run(f, "MonedaSinAviso"), "con la moneda a la vista, el \"+4 creditos\" de arriba no sale (lo dice la capsula)");
        Set(f, "attractOn", true); bool atr = (bool)Run(f, "MonedaSinAviso"); Set(f, "attractOn", false);
        Set(f, "monedaCae", false); bool apag = (bool)Run(f, "MonedaSinAviso"); Set(f, "monedaCae", true);
        Check(!atr && !apag, "pero sale como siempre en modo atraccion (la franja va limpia) o con la idea apagada");
        MethodInfo add = form.GetMethod("AddCoin", F);
        List<string> pasos = Codigo.Repasa(add);
        int iSin = pasos.IndexOf("MonedaSinAviso"), iShow = pasos.IndexOf("ShowToast"), iMark = pasos.IndexOf("MarkToast");
        Check(iSin >= 0 && iSin < iShow && iMark > iShow, "AddCoin pregunta antes de poner el letrero, y la cuenta de monedas seguidas se sigue llevando");
        // El golpe de la capsula y la monedita tapada: solo en la imagen de la tarjeta.
        Set(pc, "MonedaGrande", true);
        int t0 = Environment.TickCount - 420 - 150;   // aterrizo hace 150 ms: en pleno golpe
        Set(pc, "monTick", t0); Set(pc, "rodTick", t0 + 420);
        float salto = (float)Run(pc, "SaltoMoneda", Environment.TickCount);
        Check(salto == 1f, "fuera de la imagen de la tarjeta no hay golpe");
        MethodInfo aplica = form.GetMethod("AplicaEfectosGpu", F);
        Set(pc, "MonedaGrande", false); aplica.Invoke(f, null);
        Check((bool)Get(pc, "MonedaGrande"), "y el interruptor llega a la capsula al aplicarse (AplicaEfectosGpu)");
    }

    // ================================================================================================
    //  5) LO QUE SUENA, EN GRANDE
    // ================================================================================================
    static void Grande()
    {
        Console.WriteLine("\n  5) LO QUE SUENA, EN GRANDE\n");
        string[] qt = { "Cielito Lindo", "Contrabando y Traicion", "Evidencias" }, qa = { "Los Cadetes del Norte", "Los Tigres del Norte", "Ana Gabriel" };
        Image[] cov = { Liso(Color.SteelBlue, 60), Liso(Color.SeaGreen, 60), Liso(Color.Crimson, 60) };
        MethodInfo gra = form.GetMethod("RenderIdlePanelGrande", F);
        object x = Activator.CreateInstance(grandeT, true);
        Set(x, "Corre", 60); Set(x, "Genero", "Regional"); Set(x, "GeneroColor", Color.FromArgb(215, 120, 25)); Set(x, "Minutos", new int[] { 2, 6, -1 });
        string largo = "Mi Gusto Es (En Vivo desde Guadalajara, Edicion Aniversario)";
        object[] args = { 1024, 245, largo, "Mariachi Vargas de Tecalitlan", "1:47  ·  faltan 2:25", qt, qa, cov, 3, Color.MediumPurple, false, 2, false, x, 0 };
        Bitmap conX = (Bitmap)gra.Invoke(null, args);
        conX.Save(Path.Combine(dirPng, "reposo_grande.png"), ImageFormat.Png);
        object[] args0 = { 1024, 245, largo, "Mariachi Vargas de Tecalitlan", "1:47  ·  faltan 2:25", qt, qa, cov, 3, Color.MediumPurple, false, 2, false, null, 0 };
        Bitmap sinX = (Bitmap)gra.Invoke(null, args0);
        sinX.Save(Path.Combine(dirPng, "reposo_siempre.png"), ImageFormat.Png);
        RectangleF caja = (RectangleF)Get(x, "TituloCaja"), gcaja = (RectangleF)Get(x, "GeneroCaja");
        Rectangle cover = (Rectangle)St(gpu, "IdleCoverRect", 1024, 245);
        Check((bool)Get(x, "TituloFuera") && Math.Abs(caja.X - (cover.Right + 20 + 60)) < .5f && caja.Width > 50,
              "el titulo que no cabe lo pinta la tarjeta deslizandose: el panel solo apunta su caja, corrida para el vinilo (" + Fm(caja.X, "0") + " px)");
        int blancoCaja = Parecidos(conX, new Rectangle((int)caja.X, (int)caja.Y, (int)caja.Width, (int)caja.Height), Color.FromArgb(240, 245, 255), 12);
        Check(blancoCaja == 0, "y el panel deja esa caja vacia (no hay dos titulos)");
        Check(gcaja.Width > 20 && gcaja.Right <= caja.Right + 1 && gcaja.X >= caja.X - .5f && gcaja.Y > caja.Bottom, "la pastilla del genero va debajo del titulo, junto al artista o debajo si no le cabe, sin salirse");
        // Donde van los minutos: a la derecha del renglon del artista de cada tarjeta (sin el numero, que va arriba).
        Rectangle donde = new Rectangle(860, 40, 110, 205);
        int azul = Azules(conX, donde), azul0 = Azules(sinX, donde);
        Info("pixeles azules de los minutos: " + azul + " (sin la idea, " + azul0 + ")");
        Check(azul > 40 && azul0 < 10, "las de la cola dicen en cuanto suenan (\"en ~6 min\", en azul), y sin la idea no");
        MethodInfo tmc = form.GetMethod("TextoMinutosCola", F);
        Check((string)tmc.Invoke(null, new object[] { x, 0 }) == "en ~2 min" && (string)tmc.Invoke(null, new object[] { x, 2 }) == ""
              && (string)tmc.Invoke(null, new object[] { null, 0 }) == "", "y si no se sabe lo que dura alguna de delante, no se inventa");
        // Con un titulo corto, el titulo va en el panel, en una linea.
        object xc = Activator.CreateInstance(grandeT, true); Set(xc, "Corre", 60); Set(xc, "Minutos", new int[0]);
        object[] args2 = { 1024, 245, "El Rey", "Vicente Fernandez", "0:10", new string[0], new string[0], new Image[0], 0, Color.MediumPurple, false, 0, false, xc, 0 };
        using (Bitmap cortoB = (Bitmap)gra.Invoke(null, args2))
        {
            RectangleF cc = (RectangleF)Get(xc, "TituloCaja");
            int blanco = Parecidos(cortoB, new Rectangle((int)cc.X, (int)cc.Y, (int)cc.Width, (int)cc.Height), Color.FromArgb(240, 245, 255), 20);
            Check(!(bool)Get(xc, "TituloFuera") && blanco > 50 && ((RectangleF)Get(xc, "GeneroCaja")).Width == 0f,
                  "un titulo que cabe va en el panel, quieto, en una linea; y sin genero conocido no hay pastilla");
        }
        // Al dibujar, la tarjeta recibe el titulo horneado en una tira: mas ancha que su caja y una sola vez por titulo.
        object fr = ConTarjeta();
        Set(fr, "suenaGrande", true); Set(fr, "estadoExpandido", true);
        Set(fr, "nowPlaying", Item("Mi Gusto Es", false)); Set(fr, "reposoUltimo", x);
        Set(x, "Titulo", largo); Set(x, "W", 1024); Set(x, "H", 245);   // lo que apunta BuildIdlePanel al rehacer el panel
        object rr = Run(fr, "BuildReposoState"), rr2 = Run(fr, "BuildReposoState");
        Bitmap tira = (Bitmap)Get(rr, "Titulo");
        float exceso = (float)Get(rr, "TituloExceso");
        Check((bool)Get(rr, "Has") && tira != null && exceso > 20f && Math.Abs(exceso - (tira.Width - caja.Width)) < .5f
              && Math.Abs((float)Get(rr, "TituloX") - caja.X) < .5f && Parecidos(tira, new Rectangle(0, 0, tira.Width, tira.Height), Color.FromArgb(240, 245, 255), 20) > 200,
              "la tarjeta recibe el titulo horneado en una tira mas ancha que su caja (le sobran " + Fm(exceso, "0") + " px para deslizarse)");
        Check((int)Get(rr2, "TituloVer") == (int)Get(rr, "TituloVer") && (Bitmap)Get(rr2, "Titulo") == tira, "y se hornea una sola vez por titulo, no en cada cuadro");
        Set(xc, "Titulo", "El Rey"); Set(xc, "W", 1024); Set(xc, "H", 245); Set(fr, "reposoUltimo", xc);
        Check((bool)Get(Run(fr, "BuildReposoState"), "Has") && Get(Run(fr, "BuildReposoState"), "Titulo") == null, "si cabe, no hay tira: va quieto en el panel");
        // La insignia MEJORADA se corre con el texto (y su brillo con ella).
        Rectangle b0 = (Rectangle)St(gpu, "IdleBadgeRect", 1024, 245), b1 = (Rectangle)St(gpu, "IdleBadgeRectCorrido", 1024, 245, 60), b2 = (Rectangle)St(gpu, "IdleBadgeRectCorrido", 1024, 245, 0);
        Check(b1.X == b0.X + 60 && b1.Right <= (int)(1024 * .52f) - 24 + 1 && b2 == b0, "la insignia MEJORADA se corre con el texto sin pasarse de su mitad, y sin vinilo queda donde siempre");
        Check(Codigo.LeeCampo(Codigo.MetodosConAyudantes(gpu, "IdleBadgeGlow"), "IdleCorre") && Codigo.EscribeCampo(Codigo.MetodosConAyudantes(form, "BuildIdlePanel"), "IdleCorre"),
              "y su brillo va donde ella (el panel le dice a la tarjeta cuanto se corrio)");
        // Cuanto falta.
        MethodInfo tf = form.GetMethod("TextoFaltan", F);
        string s1 = (string)tf.Invoke(null, new object[] { TimeSpan.FromSeconds(107), TimeSpan.FromSeconds(252), "x" });
        string s2 = (string)tf.Invoke(null, new object[] { TimeSpan.FromSeconds(10), TimeSpan.Zero, "0:10 / 0:00" });
        Info(s1);
        Check(s1 == "1:47  ·  faltan 2:25" && s2 == "0:10 / 0:00", "el reloj dice por donde va y cuanto falta; si no se sabe lo que dura, el de siempre");
        Check(Codigo.Llama(form.GetMethod("ParcheaElReloj", F), "TextoRelojReposo") && Codigo.Llama(form.GetMethod("BuildIdlePanel", F), "TextoRelojReposo"),
              "y el reloj del panel lo usa en sus dos caminos (el parche de cada segundo tambien)");
        // El titulo que se desliza: quieto, va, se para, vuelve; sin saltos y a su ritmo.
        MethodInfo ct = gpu.GetMethod("CorreTitulo", F);
        float ex = 180f, antes = 0f, maxPaso = 0f, maximo = 0f; bool quietoIni = true, vuelve = false;
        for (double t = 0; t < 20000; t += 16.7)
        {
            float o = (float)ct.Invoke(null, new object[] { t, ex });
            if (t < 2000 && o != 0f) quietoIni = false;
            maxPaso = Math.Max(maxPaso, Math.Abs(o - antes)); maximo = Math.Max(maximo, o);
            if (maximo >= ex - .01f && o < 1f) vuelve = true;
            antes = o;
        }
        Check(quietoIni && Math.Abs(maximo - ex) < .01f && vuelve && maxPaso < 5f,
              "quieto 2,2 s para leerse, se desliza hasta el final, se para y vuelve; nunca salta (como mucho " + Fm(maxPaso, "0.0") + " px por cuadro)");
        Check((float)ct.Invoke(null, new object[] { 5000.0, 0f }) == 0f, "y si cabe no se mueve");
        // Los minutos del panel: la misma cuenta que la cola del disco, y el panel se entera cuando cambian.
        object f = ConTarjeta();
        Set(f, "suenaGrande", true); Set(f, "estadoExpandido", true);
        Check((bool)Run(f, "QuiereEsperas"), "con la idea encendida, la rocola lleva la cuenta de los minutos de la cola aunque no haya un disco abierto");
        Set(f, "suenaGrande", false);
        Check(!(bool)Run(f, "QuiereEsperas"), "y apagada, no (salvo que la pida la lista del disco)");
        Check(Codigo.Llama(form.GetMethod("ActualizaEsperas", F), "CambioElPanelDeReposo"), "cuando cambia un minuto, el panel se entera (se rehace una vez por minuto, no mas)");
        Check(Codigo.Llama(form.GetMethod("ActualizaGenero", F), "CambioElPanelDeReposo"), "y cuando se aprende el genero de la que suena, tambien");
        MethodInfo er = form.GetMethod("ExtraReposo", F);
        Set(f, "suenaGrande", false);
        Check(er.Invoke(f, new object[] { 1024, 245, 3 }) == null, "apagada, el panel es el de siempre");
        Set(f, "suenaGrande", true);
        Set(f, "esperaMin", new int[] { 3, 7, -1 });
        object xr = er.Invoke(f, new object[] { 1024, 245, 3 });
        int[] mins = (int[])Get(xr, "Minutos");
        Check(mins.Length == 3 && mins[0] == 3 && mins[1] == 7 && mins[2] == -1, "cada una de la cola lleva los minutos de la cola del disco (los mismos, no otra cuenta)");
        int corre = (int)Get(xr, "Corre");
        float r = Cte<float>(form, "ViniloReposoRadio"), sa = Cte<float>(form, "ViniloReposoSale");
        Check(corre >= (int)(cover.Width * (r + sa - .5f)) && corre <= (int)(cover.Width * (r + sa - .5f)) + 12,
              "el texto se corre lo que asoma el vinilo (" + corre + " px con una portada de " + cover.Width + ")");
        // El vinilo: el mismo dibujo que el de dentro del disco, una vez por cancion.
        Check(Codigo.Llama(form.GetMethod("PreparaViniloReposo", F), "PintaEtiqueta") && Codigo.Llama(form.GetMethod("PreparaViniloReposo", F), "PintaSurcos"),
              "el vinilo del reposo es el mismo dibujo que el de dentro del disco (surcos una vez por tamaño, la etiqueta por cancion)");
        Check(Codigo.Llama(form.GetMethod("SuenaVivoTic", F), "PreparaViniloReposo") && !Codigo.Llama(form.GetMethod("BuildReposoState", F), "PreparaViniloReposo"),
              "se hornea en el reloj de la pantalla, no al dibujar");
    }

    // ================================================================================================
    //  6) Ctrl+F3 -> PANTALLA
    // ================================================================================================
    static void Ajustes()
    {
        Console.WriteLine("\n  6) CTRL+F3 -> PANTALLA -> LO QUE SUENA Y LOS CREDITOS\n");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_suenavivo_" + Environment.TickCount);
        Directory.CreateDirectory(dir);
        object owner = Codigo.FormSinEncender(form);
        Set(owner, "carpetaDatos", dir);
        Control pc = (Control)Activator.CreateInstance(pvT, true); Set(owner, "pastCred", pc);
        if (gpu != null) { Set(owner, "gpuPanel", Activator.CreateInstance(gpu, true)); Set(owner, "gpuEnabled", true); }
        string[] nom = { "Lo que suena, en grande (GPU)", "La moneda que cae (GPU)", "Sin creditos, invitar (GPU)", "Avisos que se entienden (GPU)" };
        string[] campo = { "suenaGrande", "monedaCae", "sinCreditosInvita", "avisosClaros" };
        using (Font f = new Font("Segoe UI", 9))
        using (Panel page = (Panel)form.GetMethod("PaginaPantalla", F).Invoke(owner, new object[] { f, f, f, Color.White, Color.Gray, Color.Gray }))
        {
            Control[] sw = new Control[4]; bool estan = true;
            for (int i = 0; i < 4; i++) { sw[i] = Find(page, nom[i]); if (sw[i] == null) estan = false; }
            Check(estan, "la pestaña Pantalla tiene los cuatro nuevos");
            if (!estan) return;
            List<Control> hojas = new List<Control>(); List<Control> botones = new List<Control>();
            foreach (Control c in page.Controls) { if (c.GetType() == typeof(Panel)) hojas.Add(c); else if (c.GetType().Name == "BotonPlano" && c.Top < 40) botones.Add(c); }
            Control hojaSuena = sw[0].Parent != null ? sw[0].Parent.Parent : null;
            Control hojaPared = Find(page, "Portada enfocada (GPU)").Parent.Parent;
            bool unaVisible = hojaPared.Visible && !hojaSuena.Visible;
            Check(hojas.Count == 2 && botones.Count == 2 && hojaSuena != hojaPared && unaVisible,
                  "en dos hojas con su boton arriba (\"" + (botones.Count > 1 ? botones[1].Text : "") + "\"); al abrir se ve la de la pared y el disco");
            typeof(Control).GetMethod("OnClick", F).Invoke(botones[1], new object[] { EventArgs.Empty });
            bool cambia = !hojaPared.Visible && hojaSuena.Visible;
            typeof(Control).GetMethod("OnClick", F).Invoke(botones[0], new object[] { EventArgs.Empty });
            Check(cambia && hojaPared.Visible && !hojaSuena.Visible, "cada boton enseña su hoja (y solo la suya)");
            bool dentro = true;
            foreach (Control h in hojas)
            {
                if (h.Bottom > 508 || h.Right > page.Width) dentro = false;
                foreach (Control card in h.Controls)
                {
                    if (card.Right > h.Width || card.Bottom > h.Height) dentro = false;
                    foreach (Control d in h.Controls) if (d != card && d.Bounds.IntersectsWith(card.Bounds)) dentro = false;
                    foreach (Control x in card.Controls) { if (x.Right > card.Width || x.Bottom > card.Height) dentro = false; foreach (Control y in card.Controls) if (x != y && x.Bounds.IntersectsWith(y.Bounds)) dentro = false; }
                }
            }
            Check(dentro, "todo cabe en su hoja sin taparse y encima del pie de la pagina");
            PropertyInfo marcado = sw[0].GetType().GetProperty("Checked", F);
            bool guarda = true, aplica = true;
            for (int mask = 15; mask >= 0; mask--)
            {
                for (int i = 0; i < 4; i++) marcado.SetValue(sw[i], (mask & (1 << i)) != 0, null);
                form.GetMethod("SaveConfigPrefs", F).Invoke(owner, null);
                Codigo.EsperaGuardado(form, owner);
                object leido = Codigo.FormSinEncender(form); form.GetMethod("LoadConfigPrefs", F).Invoke(leido, null);
                for (int i = 0; i < 4; i++) if ((bool)Get(leido, campo[i]) != ((mask & (1 << i)) != 0) || (bool)Get(owner, campo[i]) != ((mask & (1 << i)) != 0)) guarda = false;
                if (gpu != null && (bool)Get(pc, "MonedaGrande") != ((mask & 2) != 0)) aplica = false;
            }
            Check(guarda, "las 16 combinaciones se guardan en rocola_config.txt y se leen igual");
            if (gpu != null) Check(aplica, "y se aplican al momento (la moneda grande llega a la capsula)");
            Control reset = Find(page, "Pantalla como de fabrica");
            try { typeof(Control).GetMethod("OnClick", F).Invoke(reset, new object[] { EventArgs.Empty }); } catch { }
            bool todos = true; for (int i = 0; i < 4; i++) if (!(bool)Get(owner, campo[i]) || !(bool)marcado.GetValue(sw[i], null)) todos = false;
            Check(reset != null && todos, "\"Pantalla como de fabrica\" vuelve a encender las cuatro");
            // Pone las preferencias, no solo los interruptores: aunque un interruptor encendido y su preferencia no coincidan.
            for (int i = 0; i < 4; i++) Set(owner, campo[i], false);
            try { typeof(Control).GetMethod("OnClick", F).Invoke(reset, new object[] { EventArgs.Empty }); } catch { }
            bool puestas = true; for (int i = 0; i < 4; i++) if (!(bool)Get(owner, campo[i])) puestas = false;
            Check(puestas, "y las pone de verdad, no solo los interruptores (aunque no coincidieran)");
        }
        try { Directory.Delete(dir, true); } catch { }
        // De fabrica: la rocola armada sin constructor no las tiene puestas, asi que se lee su declaracion.
        string fuente = Path.Combine(Path.Combine("..", "Rocola"), "SuenaVivoGpu.cs");
        string txt = File.Exists(fuente) ? File.ReadAllText(fuente) : "";
        bool fabrica = txt.Length > 0; foreach (string c in campo) if (txt.IndexOf("private bool " + c + " = true;") < 0) fabrica = false;
        Check(fabrica, "de fabrica vienen encendidas");
    }

    // ================================================================================================
    //  7) LO QUE CUESTA
    // ================================================================================================
    static double Mide(int veces, Action a)
    {
        a();
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < veces; i++) a();
        return sw.Elapsed.TotalMilliseconds / veces;
    }
    static void Coste()
    {
        Console.WriteLine("\n  7) LO QUE CUESTA\n");
        Font ft = new Font(FontFamily.GenericSansSerif, 14f, FontStyle.Bold), fd = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Bold);
        Size s = (Size)St(form, "TamanoAviso", 1024, "Pusiste «El Rey»", "va la 3.ª en la cola · en ~9 min", ft, fd);
        Bitmap bm = new Bitmap(s.Width, s.Height, PixelFormat.Format32bppPArgb);
        MethodInfo pa = form.GetMethod("PintaAviso", F); object hecho = Enum("Hecho");
        double tAviso = Mide(40, delegate { pa.Invoke(null, new object[] { bm, hecho, "Pusiste «El Rey»", "va la 3.ª en la cola · en ~9 min", ft, fd }); });
        Size si = (Size)St(form, "TamanoInvita", 1024);
        Bitmap bi = new Bitmap(si.Width, si.Height, PixelFormat.Format32bppPArgb);
        MethodInfo pi = form.GetMethod("PintaInvita", F);
        double tInv = Mide(20, delegate { object[] a = { bi, "Hermoso Carino", false, 4, ft, fd, fd, PointF.Empty, 0f }; pi.Invoke(null, a); });
        MethodInfo bake = gpu.GetMethod("BakeMoneda", F);
        double tMon = Mide(10, delegate { ((Bitmap)bake.Invoke(null, new object[] { 96 })).Dispose(); });
        string[] qt = { "Cielito Lindo", "Contrabando y Traicion", "Evidencias" }, qa = { "a", "b", "c" };
        Image[] cov = { null, null, null };
        MethodInfo gra = form.GetMethod("RenderIdlePanelGrande", F);
        object x = Activator.CreateInstance(grandeT, true); Set(x, "Corre", 60); Set(x, "Genero", "Regional"); Set(x, "GeneroColor", Color.Orange); Set(x, "Minutos", new int[] { 2, 6, 9 });
        double tG = Mide(10, delegate { ((Bitmap)gra.Invoke(null, new object[] { 1024, 245, "Mi Gusto Es (En Vivo)", "Mariachi", "1:47", qt, qa, cov, 3, Color.Purple, false, 0, false, x, 0 })).Dispose(); });
        double tS = Mide(10, delegate { ((Bitmap)gra.Invoke(null, new object[] { 1024, 245, "Mi Gusto Es (En Vivo)", "Mariachi", "1:47", qt, qa, cov, 3, Color.Purple, false, 0, false, null, 0 })).Dispose(); });
        MethodInfo cur = gpu.GetMethod("CurvaAviso", F), pm = gpu.GetMethod("PasoMoneda", F), ct = gpu.GetMethod("CorreTitulo", F);
        double tCuadro = Mide(20000, delegate {
            object[] a = { 1000.0, 0f, 0f }; cur.Invoke(null, a);
            object[] b = { .5f, 0f, 0f, 10f, 10f, 0f, 0f, 0f, 0f }; pm.Invoke(null, b);
            ct.Invoke(null, new object[] { 3000.0, 120f });
        });
        Info("hornear un aviso: " + Fm(tAviso, "0.00") + " ms (una vez por aviso); la tarjeta de la invitacion: " + Fm(tInv, "0.00") + " ms; la moneda: " + Fm(tMon, "0.00") + " ms (una vez)");
        Info("el panel de reposo en grande: " + Fm(tG, "0.00") + " ms (el de siempre: " + Fm(tS, "0.00") + " ms), solo cuando cambia algo de lo que ensena");
        Info("por cuadro: la curva del aviso, la caida de la moneda y el deslizar del titulo, juntas: " + Fm(tCuadro * 1000, "0.0") + " us (con la llamada por reflexion)");
        Check(tAviso < 15 && tInv < 25 && tG < tS * 2 + 5, "hornear es cosa de milisegundos y solo pasa cuando cambia algo");
        Check(tCuadro < .05, "y por cuadro no llega a 50 microsegundos");
    }

    [STAThread]
    static int Main(string[] args)
    {
        asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        form = asm.GetType("Rocola.Form1"); gpu = asm.GetType("Rocola.GpuPanel");
        pvT = form.GetNestedType("PastillaView", F); itemT = form.GetNestedType("PlayItem", F); trackT = form.GetNestedType("Track", F);
        dirPng = Path.Combine(Path.GetTempPath(), "rocola_suenavivo_png"); Directory.CreateDirectory(dirPng);
        Console.WriteLine("  perfil: " + (gpu != null ? "con tarjeta grafica" : "sin tarjeta grafica (solo la pestaña de ajustes y los enganches quietos)"));
        if (gpu != null)
        {
            avisoT = asm.GetType("Rocola.GpuAviso"); invitaT = asm.GetType("Rocola.GpuInvita"); monedaT = asm.GetType("Rocola.GpuMoneda");
            reposoT = asm.GetType("Rocola.GpuReposo"); grandeT = asm.GetType("Rocola.ReposoGrande"); tipoT = form.GetNestedType("TipoAviso", F);
            Check(avisoT != null && invitaT != null && monedaT != null && reposoT != null && grandeT != null && tipoT != null
                  && gpu.GetField("GetAviso", F) != null && gpu.GetField("GetInvita", F) != null && gpu.GetField("GetMoneda", F) != null && gpu.GetField("GetReposo", F) != null,
                  "la tarjeta pide a la rocola el aviso, la invitacion, la moneda y lo del reposo, cada uno con lo suyo");
            string cpu = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args.Length > 0 ? args[0] : "rocola.exe")), "rocola_cpu.exe");
            if (File.Exists(cpu)) asmCpu = Assembly.Load(File.ReadAllBytes(cpu));
            Check(asmCpu != null && asmCpu.GetType("Rocola.GpuPanel") == null, "esta el ejecutable sin tarjeta grafica para comparar (" + Path.GetFileName(cpu) + ")");
            if (asmCpu != null) Procesador();
            Avisos(); Invita(); Moneda(); Grande();
        }
        else
        {
            // Sin tarjeta grafica: los enganches existen (se llaman desde el codigo comun) y no hacen nada.
            object fc = Codigo.FormSinEncender(form);
            bool quietos = !(bool)Run(fc, "InvitaSinCredito", new object[] { null }) && !(bool)Run(fc, "MonedaSinAviso") && (int)Run(fc, "VidaAviso") == 1300
                        && !(bool)Run(fc, "AvisoEnBuscador", "x", true);
            Check(quietos, "los enganches no hacen nada: letrero naranja, \"+4 creditos\" arriba y avisos de 1,3 s, como siempre");
        }
        Ajustes();
        if (gpu != null) Coste();
        Console.WriteLine("\n  imagenes para mirar: " + dirPng);
        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN (" + oks + " comprobaciones)" : fallos + " FALLO(S)"));
        return fallos;
    }
}
