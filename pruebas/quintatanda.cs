using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// QUINTA TANDA (12-10-2026).
//  1) LAS FLECHAS DE BUSCAR Y AYUDA CON EL ROJO MANTENIDO (peticion del usuario: "en vez de que aparezcan las flechitas
//     feas al presionar el boton rojo, que mejor se resalten las que ya tienen en su contenedor"). Ya no se pinta un
//     triangulito suelto junto a cada casilla: se enciende la tecla de la flecha que la casilla ya trae (rojo vivo, la
//     flecha en blanco, un filo claro) y el brillo de la tarjeta va encima de esa tecla. Solo con tarjeta grafica: sin
//     ella la guia del rojo no existe y el dibujo por CPU no cambia.
//  2) EL REPRODUCTOR DE VIDEO QUE NO SE PUEDE CREAR. Si el control de Windows Media Player no da su reproductor, se
//     quitaba de la ventana nunca: cada reacomodo lo volvia a tocar fuera de cualquier 'try' (con mono, donde no
//     existe, la rocola se cerraba al pedir un video). Ahora se suelta ANTES de devolver el credito.
// Formulario y casillas aislados, sin ventana ni musica.
class QuintaTanda
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static Type form;
    static int Main(string[] args)
    {
        form = Assembly.LoadFrom(args[0]).GetType("Rocola.Form1", true);
        if (form.GetNestedType("AyudaView", B) == null)
        {
            // Sin tarjeta grafica (perfil CPU): ni casilla de AYUDA ni guia del rojo; lo de BUSCAR no se toca.
            Check(form.GetMethod("PintaGuiaRojoAyuda", B) == null && form.GetNestedType("BuscaView", B).GetField("Tecla", B) == null,
                  "sin tarjeta, la casilla de BUSCAR queda como estaba");
        }
        else Flechas();
        Video();
        Console.WriteLine(mal == 0 ? "  OK quintatanda: " + ok + " comprobaciones (casillas aisladas; sin ventana ni musica)"
                                   : "  FALLO quintatanda: " + mal + " de " + (ok + mal));
        return mal;
    }

    static Rectangle Tecla(Control c) { return (Rectangle)c.GetType().GetField("Tecla", B).GetValue(c); }
    static void Pinta(Control c, Graphics g)
    { c.GetType().GetMethod("RenderContent", B, null, new Type[] { typeof(Graphics), typeof(bool) }, null).Invoke(c, new object[] { g, true }); }

    static void Flechas()
    {
        Type tb = form.GetNestedType("BuscaView", B), ta = form.GetNestedType("AyudaView", B);
        MethodInfo pinta = form.GetMethod("PintaGuiaRojoAyuda", B), tecla = form.GetMethod("GuiaRojoTecla", B);
        Check(pinta != null && pinta.GetParameters().Length == 6 && tecla != null, "la guia recibe las teclas de las casillas");
        Check(form.GetMethod("GuiaRojoFlechaAyuda", B) == null, "ya no hay sitio para una flecha suelta junto a la ayuda");
        foreach (int alto in new int[] { 34, 40, 48, 56, 64, 76 })
        foreach (bool compacta in new bool[] { false, true })
        {
            Control busca = (Control)Activator.CreateInstance(tb, true), ayuda = (Control)Activator.CreateInstance(ta, true);
            try
            {
                ta.GetField("Compacta", B).SetValue(ayuda, compacta);
                int wb = (int)tb.GetMethod("AnchoJusto", B).Invoke(null, new object[] { alto });
                int wa = (int)ta.GetMethod("Ancho", B).Invoke(null, new object[] { alto, compacta });
                busca.Bounds = new Rectangle(240, 10, wb, alto); ayuda.Bounds = new Rectangle(240 + wb + 10, 10, wa, alto);
                Rectangle barra = new Rectangle(34, 10 + alto + 14, 900, 40);
                string caso = " (alto " + alto + (compacta ? ", compacta)" : ")");
                Check(Tecla(busca).IsEmpty && Tecla(ayuda).IsEmpty, "antes de dibujarse no hay tecla" + caso);
                using (Bitmap cb = new Bitmap(busca.Width, busca.Height)) using (Graphics cg = Graphics.FromImage(cb)) Pinta(busca, cg);
                using (Bitmap ca = new Bitmap(ayuda.Width, ayuda.Height)) using (Graphics cg = Graphics.FromImage(ca)) Pinta(ayuda, cg);
                Rectangle kb = (Rectangle)tecla.Invoke(null, new object[] { busca.Bounds, Tecla(busca) });
                Rectangle ka = (Rectangle)tecla.Invoke(null, new object[] { ayuda.Bounds, Tecla(ayuda) });
                // La tecla es la ULTIMA cosa de la casilla, cuadrada, dentro y a su derecha.
                Check(kb.Width > 8 && kb.Width == kb.Height && busca.Bounds.Contains(kb) && kb.Right > busca.Right - 24, "la tecla de BUSCAR, dentro y al final" + caso + ": " + kb);
                Check(ka.Width > 8 && ka.Width == ka.Height && ayuda.Bounds.Contains(ka) && ka.Right > ayuda.Right - 24, "la tecla de AYUDA, dentro y al final" + caso + ": " + ka);
                // Lo que pinta la guia con las teclas: nada fuera de las casillas y la barra (las flechas de la barra sí).
                using (Bitmap sin = new Bitmap(1000, barra.Bottom + 12)) using (Bitmap con = new Bitmap(1000, barra.Bottom + 12))
                {
                    using (Graphics g = Graphics.FromImage(con)) { g.Clear(Color.Black); g.SmoothingMode = SmoothingMode.AntiAlias; pinta.Invoke(null, new object[] { g, barra, busca.Bounds, ayuda.Bounds, kb, ka }); }
                    using (Graphics g = Graphics.FromImage(sin)) { g.Clear(Color.Black); g.SmoothingMode = SmoothingMode.AntiAlias; pinta.Invoke(null, new object[] { g, barra, busca.Bounds, ayuda.Bounds, Rectangle.Empty, Rectangle.Empty }); }
                    Rectangle hueco = new Rectangle(busca.Left - 14, busca.Top, 13, alto), entre = new Rectangle(busca.Right + 1, busca.Top, ayuda.Left - busca.Right - 2, alto);
                    Check(Vacio(con, hueco) && Vacio(con, entre), "ni un triangulito en el hueco antes de BUSCAR ni entre las casillas" + caso);
                    Check(Rojo(con, kb) && Rojo(con, ka), "las dos teclas se encienden en rojo" + caso);
                    Check(Blanco(con, kb, true) && Blanco(con, ka, false), "con la flecha en blanco, en su sentido (arriba BUSCAR, abajo AYUDA)" + caso);
                    Check(!Rojo(sin, kb) && !Rojo(sin, ka), "sin teclas (aun no dibujadas) no se pinta nada en su sitio" + caso);
                    Rectangle fuera = Rectangle.Union(busca.Bounds, ayuda.Bounds); fuera.Inflate(1, 1);
                    Check(SoloDentro(con, fuera, barra), "todo lo de BUSCAR y AYUDA queda dentro de sus casillas" + caso);
                }
            }
            finally { busca.Dispose(); ayuda.Dispose(); }
        }
        // Una medida vieja (la casilla se achico y la tecla ya no cabe) no pinta nada.
        Check(((Rectangle)tecla.Invoke(null, new object[] { new Rectangle(10, 10, 100, 40), new Rectangle(95, 8, 24, 24) })).IsEmpty, "una tecla que ya no cabe en su casilla no se pinta");
        Check(((Rectangle)tecla.Invoke(null, new object[] { Rectangle.Empty, new Rectangle(5, 5, 20, 20) })).IsEmpty, "una casilla escondida, tampoco");
        // El brillo de la tarjeta va sobre las teclas.
        MethodInfo glows = form.GetMethod("FillGuiaRojoGlows", B);
        Check(Codigo.Llama(glows, "TeclaBusca") && Codigo.Llama(glows, "TeclaAyuda"), "el brillo de la tarjeta se pone sobre las teclas de las casillas");
        Check(!Codigo.Llama(glows, "GuiaRojoFlechaAyuda"), "y no en el hueco de fuera");
        MethodInfo draw = form.GetMethod("DrawGuiaRojo", B);
        Check(Codigo.Llama(draw, "TeclaBusca") && Codigo.Llama(draw, "TeclaAyuda"), "la guia pinta las teclas de las casillas de verdad");
        // Las casillas se dibujan en la textura ANTES que la guia (asi la tecla esta al dia).
        string fuente = Codigo.FuenteDeForm1();
        int casillas = fuente.IndexOf("if (ayudaView != null && ayudaView.Visible) DrawCtrlOverlay(g, ayudaView);"), guia = fuente.IndexOf("DrawGuiaRojo(g);");
        Check(casillas > 0 && guia > casillas, "las casillas se pintan antes que la guia del rojo");
    }

    static void Video()
    {
        MethodInfo start = form.GetMethod("StartVideoItem", B), suelta = form.GetMethod("SueltaVideoRoto", B);
        Check(suelta != null && Codigo.Llama(start, "SueltaVideoRoto"), "si el video no abre, se mira si el reproductor sirve");
        // En orden: soltar el control roto ANTES de devolver el credito (que reacomoda la pantalla).
        List<string> il = Codigo.IlConNombres(start);
        int iSuelta = il.FindIndex(delegate (string s) { return s.Contains("SueltaVideoRoto"); });
        int iDevuelve = il.FindIndex(delegate (string s) { return s.Contains("RefundCredit"); });
        Check(iSuelta >= 0 && iDevuelve > iSuelta, "se suelta antes de devolver el credito");
        // De verdad: un control sin reproductor (aqui no hay Windows Media Player) se quita y se suelta.
        object f = Codigo.FormSinEncender(form);
        Codigo.PonColecciones(f);
        Type th = form.GetNestedType("VideoHost", B);
        object host = null;
        try { host = Activator.CreateInstance(th, true); } catch (Exception e) { Console.WriteLine("  (no se pudo crear el control: " + e.GetType().Name + ")"); }
        if (host != null)
        {
            // (el formulario aislado no tiene lista de controles: quitarlo de ella va en su 'try')
            form.GetField("video", B).SetValue(f, host);
            suelta.Invoke(f, null);
            Check(form.GetField("video", B).GetValue(f) == null && ((Control)host).IsDisposed, "un reproductor que no da su control se quita y se suelta");
            suelta.Invoke(f, null);
            Check(form.GetField("video", B).GetValue(f) == null, "y sin control no hace nada");
        }
    }

    static bool Vacio(Bitmap b, Rectangle r)
    {
        for (int y = Math.Max(0, r.Top); y < Math.Min(b.Height, r.Bottom); y++)
            for (int x = Math.Max(0, r.Left); x < Math.Min(b.Width, r.Right); x++)
            { Color c = b.GetPixel(x, y); if (c.R + c.G + c.B > 60) return false; }
        return true;
    }
    static bool Rojo(Bitmap b, Rectangle k)
    {
        if (k.Width <= 0) return false;
        // Una esquina de dentro de la tecla (lejos de la flecha): rojo vivo.
        Color c = b.GetPixel(k.Left + k.Width / 5 + 1, k.Bottom - k.Height / 5 - 1);
        return c.R > 180 && c.G < 120 && c.B < 120;
    }
    static bool Blanco(Bitmap b, Rectangle k, bool arriba)
    {
        int cx = k.Left + k.Width / 2, cy = k.Top + k.Width / 2;
        int punta = arriba ? cy - (int)(k.Width * 0.18f) : cy + (int)(k.Width * 0.18f);
        int base_ = arriba ? cy + (int)(k.Width * 0.12f) : cy - (int)(k.Width * 0.12f);
        Color p = b.GetPixel(cx, punta), q = b.GetPixel(cx, base_);
        return p.R > 230 && p.G > 200 && q.R > 230 && q.G > 200;
    }
    static bool SoloDentro(Bitmap b, Rectangle casillas, Rectangle barra)
    {
        // Fuera de las casillas y de la franja de la barra (con sus flechas a los lados) no hay nada pintado.
        Rectangle franja = new Rectangle(0, barra.Top - 2, b.Width, barra.Height + 4);
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
            {
                if (casillas.Contains(x, y) || franja.Contains(x, y)) continue;
                Color c = b.GetPixel(x, y);
                if (c.R + c.G + c.B > 60) return false;
            }
        return true;
    }
}
