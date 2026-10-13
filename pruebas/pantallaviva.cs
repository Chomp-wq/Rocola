using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// LA PANTALLA VIVA, TANDA 1: LA PARED Y LAS LETRAS (Rocola/PantallaVivaGpu.cs; solo tarjeta grafica)
//
// Lo que vigila:
//  1) PORTADA ENFOCADA: la elegida crece desde el centro de su borde de abajo (el nombre no se tapa), en lo que
//     dura, sin pasarse; las demas se apagan poco a poco y la elegida no; el halo toma el color de la portada y,
//     sin enfocar, sale bit a bit el de siempre; el viaje al entrar al disco sale de donde se ve la portada.
//  2) EL LETRERO: que dice (canciones, minutos solo si se saben todos), que cabe en la portada ya crecida y va
//     por dentro de su borde de abajo.
//  3) LA FICHA RAPIDA: cuando sale y cuando se va, donde se pone, cuales son las mas pedidas, y el recorrido
//     entero con una rocola armada sin encender (espera, se hornea antes de salir, no se rehace porque si, se
//     rehace si cambia la cola, se va al moverse).
//  4) PASAR DE PAGINA DESLIZANDO: cada columna con su curva, todas a tiempo, ninguna alcanza a la de delante
//     (las dos paginas nunca se pisan), la que sale no hornea ni pide portadas, el cielo se corre lo justo, y
//     apagado es el hojeo de antes.
//  5) LA LUPA: crece la elegida y sus vecinas, las lejanas quedan igual, la rayita dice cuanta musica hay, y el
//     dibujo por procesador sale identico con la lupa encendida o apagada.
//  6) Ctrl+F3 -> Pantalla: los cuatro interruptores caben, se guardan, se leen y se aplican; y el cableado.
//  7) Lo que cuesta cada cosa, medido aqui (Mono: para comparar entre ellas, no son los milisegundos del bar).
//
// Corre contra los DOS perfiles: sin tarjeta grafica solo existe la pagina de ajustes (se comprueba que se guarda
// igual y que no hay nada mas).
class PantallaViva
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type form, gpu, star, av, lbT, albumT, trackT, itemT, cellT, cubeT, infoT;
    static int fallos, oks;
    static string dirPng;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string Fm(double v, string f) { return v.ToString(f, System.Globalization.CultureInfo.InvariantCulture); }
    static object Get(object o, string n) { return o.GetType().GetField(n, F).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, F).SetValue(o, v); }
    static object St(Type t, string n, params object[] a) { return t.GetMethod(n, F).Invoke(null, a); }
    static object Run(object o, string n, params object[] a) { return o.GetType().GetMethod(n, F).Invoke(o, a); }
    static T Cte<T>(Type t, string n) { return (T)t.GetField(n, F).GetValue(null); }
    static Control Find(Control p, string name) { foreach (Control c in p.Controls) { if (c.Text == name) return c; Control r = Find(c, name); if (r != null) return r; } return null; }

    // ================================================================================================
    //  1) LA PORTADA ENFOCADA
    // ================================================================================================
    static object Info4(float x, float y, float w, float h, Bitmap cover)
    {
        object ci = Activator.CreateInstance(infoT);
        Set(ci, "Has", true); Set(ci, "X", x); Set(ci, "Y", y); Set(ci, "W", w); Set(ci, "H", h); Set(ci, "CoverA", cover);
        return ci;
    }
    static object Estado(object sel, object relay, bool pared)
    {
        object cs = Activator.CreateInstance(cubeT);
        Set(cs, "Sel", sel); if (relay != null) Set(cs, "Relay", relay); Set(cs, "Pared", pared);
        return cs;
    }
    static float Fl(object o, string n) { return Convert.ToSingle(Get(o, n)); }
    static object Aplica(object panel, object cs) { object[] a = { cs }; gpu.GetMethod("AplicaEnfoque", F).Invoke(panel, a); return a[0]; }
    static Bitmap Liso(int r, int g, int b)
    {
        Bitmap bm = new Bitmap(60, 60);
        using (Graphics gg = Graphics.FromImage(bm)) gg.Clear(Color.FromArgb(r, g, b));
        return bm;
    }

    static void Enfocada()
    {
        Console.WriteLine("\n  1) LA PORTADA ENFOCADA\n");
        float crece = Cte<float>(gpu, "EnfoqueCrece"), atenOtras = Cte<float>(gpu, "AtenuaOtras");
        double ms = Cte<double>(gpu, "EnfoqueMs");
        Info("crece un " + Fm(crece * 100, "0") + " % en " + Fm(ms, "0") + " ms; las demas bajan un " + Fm(atenOtras * 100, "0") + " %");
        // La geometria: crece desde el centro de su borde de abajo.
        object[] g = { 1.12f, 100f, 50f, 150f, 150f };
        gpu.GetMethod("Enfoca", F).Invoke(null, g);
        float x = (float)g[1], y = (float)g[2], w = (float)g[3], h = (float)g[4];
        Check(Math.Abs(y + h - 200f) < 1e-3 && Math.Abs(x + w / 2 - 175f) < 1e-3 && Math.Abs(w - 168f) < 1e-3 && Math.Abs(h - 168f) < 1e-3,
              "crece desde el centro de su borde de abajo: el borde de abajo no se mueve (debajo va el nombre del disco)");
        // La curva.
        MethodInfo curva = gpu.GetMethod("CurvaEnfoque", F);
        float c0 = (float)curva.Invoke(null, new object[] { 0.0 }), cm = (float)curva.Invoke(null, new object[] { ms / 2 });
        float c1 = (float)curva.Invoke(null, new object[] { ms }), cneg = (float)curva.Invoke(null, new object[] { -5.0 });
        float cnan = (float)curva.Invoke(null, new object[] { double.NaN });
        bool mono = true; float ant = -1f;
        for (double t = 0; t <= ms + 20; t += 1) { float v = (float)curva.Invoke(null, new object[] { t }); if (v < ant - 1e-6f || v > 1f) mono = false; ant = v; }
        Check(c0 == 0f && c1 == 1f && cneg == 0f && cnan == 0f && mono, "la curva empieza en 0, llega a 1 en " + Fm(ms, "0") + " ms, nunca se pasa ni vuelve atras (ni con un reloj raro)");
        Check(cm > .8f, "y frena al llegar: a la mitad del tiempo ya lleva " + Fm(cm * 100, "0") + " % (se siente rapida)");
        // Acerca: sin pasarse, 95 % en un cuarto de segundo.
        MethodInfo acerca = gpu.GetMethod("Acerca", F);
        float v1 = 0f; for (int i = 0; i < 15; i++) v1 = (float)acerca.Invoke(null, new object[] { v1, .2f, 1f / 60f });
        float quieto = (float)acerca.Invoke(null, new object[] { .07f, .2f, 0f });
        Check(v1 > .19f && v1 <= .2f && quieto == .07f, "el apagado de las demas llega en un cuarto de segundo sin pasarse (" + Fm(v1, "0.000") + " de .200), y sin tiempo no se mueve");

        // El color de la portada.
        MethodInfo col = gpu.GetMethod("ColorDePortada", F);
        uint siNo = 0xFF123456u;
        Func<Bitmap, uint> Color1 = delegate (Bitmap b) { object[] a = { b, siNo, 0u }; col.Invoke(null, a); return (uint)a[2]; };
        using (Bitmap gris = Liso(128, 128, 128)) using (Bitmap negro = Liso(0, 0, 0)) using (Bitmap rojo = Liso(200, 40, 30))
        using (Bitmap apagado = Liso(60, 110, 100))
        {
            Check(Color1(gris) == siNo && Color1(negro) == siNo && Color1(null) == siNo, "una portada gris, negra o que no esta deja el color de siempre (el del genero)");
            uint r = Color1(rojo);
            int R = (int)((r >> 16) & 255), G = (int)((r >> 8) & 255), B = (int)(r & 255);
            Info("roja (200,40,30) -> (" + R + "," + G + "," + B + ")");
            Check(R == 255 && R > G && G > B && B < 60, "una roja da un rojo vivo del mismo tono (el canal mayor al maximo, el orden de los canales igual)");
            uint t = Color1(apagado);
            int R2 = (int)((t >> 16) & 255), G2 = (int)((t >> 8) & 255), B2 = (int)(t & 255);
            int mx = Math.Max(R2, Math.Max(G2, B2)), mn = Math.Min(R2, Math.Min(G2, B2));
            Info("verde apagado (60,110,100) -> (" + R2 + "," + G2 + "," + B2 + ")");
            Check(mx == 255 && G2 > B2 && B2 > R2 && (mx - mn) / (double)mx >= .54, "una apagada se aviva: toda la luz y al menos un 55 % de color, sin cambiar de tono");
        }
        using (Bitmap mezcla = new Bitmap(60, 60))
        {
            using (Graphics gg = Graphics.FromImage(mezcla))
            {
                gg.Clear(Color.Black);
                gg.FillRectangle(Brushes.RoyalBlue, 20, 20, 30, 30);
            }
            uint c = Color1(mezcla);
            Check((c & 255) == 255 && ((c >> 16) & 255) < 150, "en una portada negra con un parche azul manda el azul (lo negro no pesa)");
        }
        Color1(null);

        // El color de las casillas: sin apagar, el blanco de siempre.
        MethodInfo cc = gpu.GetMethod("ColorCelda", F);
        bool igual = true;
        for (int a = 0; a <= 255; a++) if ((uint)cc.Invoke(null, new object[] { a, 0f }) != (((uint)a << 24) | 0x00FFFFFFu)) igual = false;
        uint ap = (uint)cc.Invoke(null, new object[] { 255, .2f });
        Check(igual, "sin apagar, cada casilla lleva exactamente el color de antes (256 alfas comparados)");
        Check(ap == 0xFFCCCCCCu, "apagada un 20 % lleva 204 de luz en los tres canales y el mismo alfa");
        MethodInfo mez = gpu.GetMethod("MezclaRgb", F);
        Check((uint)mez.Invoke(null, new object[] { 0xFF0000FFu, 0xFFFF0000u, 0f }) == 0x0000FFu
              && (uint)mez.Invoke(null, new object[] { 0xFF0000FFu, 0xFFFF0000u, 1f }) == 0xFF0000u
              && (uint)mez.Invoke(null, new object[] { 0xFF000000u, 0xFFC8C8C8u, .5f }) == 0x646464u, "la mezcla de colores va de uno a otro");

        using (Control p = (Control)Activator.CreateInstance(gpu, true))
        using (Bitmap roja = Liso(200, 40, 30))
        {
            // El halo sin enfocar: bit a bit el de antes.
            bool halo = true;
            foreach (float wo in new float[] { 0f, .3f, .5f, 1f, 1.4f })
                foreach (uint neb in new uint[] { 0xFF8C78FFu, 0xFF102030u })
                {
                    Set(p, "WallOpacity", wo); Set(p, "NebColor", neb);
                    uint antes = ((uint)(90f * Math.Max(0f, Math.Min(1f, wo))) << 24) | (neb & 0x00FFFFFFu);
                    if ((uint)Run(p, "HaloArgb") != antes) halo = false;
                }
            Check(halo, "sin portada enfocada el halo de la elegida sale bit a bit como antes");
            Set(p, "WallOpacity", 1f); Set(p, "NebColor", 0xFF8C78FFu);
            // La rejilla: tres casillas, la del medio es la elegida.
            Array rej = (Array)Get(p, "gridBuf");
            for (int i = 0; i < 3; i++) { object c = Activator.CreateInstance(cellT); Set(c, "Sel", i == 1); rej.SetValue(c, i); }
            Set(p, "gridN", 3); Set(p, "PortadaEnfocada", true); Set(p, "ShowAlbum", true);
            // Primero se deja crecer del todo la de al lado, que sera la que se deja.
            for (int k = 0; k < 5; k++) { Aplica(p, Estado(Info4(300, 100, 150, 150, roja), null, true)); System.Threading.Thread.Sleep(60); }
            Aplica(p, Estado(Info4(300, 100, 150, 150, roja), null, true));
            object cs = Aplica(p, Estado(Info4(100, 100, 150, 150, roja), Info4(300, 100, 150, 150, null), true));
            object sel = Get(cs, "Sel"), rel = Get(cs, "Relay");
            Check(Math.Abs(Fl(sel, "W") - 150f) < .01f && Math.Abs(Fl(rel, "W") - 168f) < .01f,
                  "al llegar a una casilla la elegida aun no crecio y la que se deja (relevo) sigue crecida");
            float[] atenua = (float[])Get(p, "atenua");
            for (int k = 0; k < 6; k++)
            {
                System.Threading.Thread.Sleep(60);
                cs = Aplica(p, Estado(Info4(100, 100, 150, 150, roja), Info4(300, 100, 150, 150, null), true));
            }
            sel = Get(cs, "Sel"); rel = Get(cs, "Relay");
            Check(Math.Abs(Fl(sel, "W") - 168f) < .01f && Math.Abs(Fl(sel, "Y") + Fl(sel, "H") - 250f) < .01f
                  && Math.Abs(Fl(sel, "X") + Fl(sel, "W") / 2 - 175f) < .01f, "pasado su tiempo la elegida mide un 12 % mas, con el borde de abajo en su sitio");
            Check(Math.Abs(Fl(rel, "W") - 150f) < .01f, "y el relevo volvio a su tamaño");
            Info("apagado de las casillas: " + Fm(atenua[0], "0.000") + ", " + Fm(atenua[1], "0.000") + ", " + Fm(atenua[2], "0.000"));
            Check(atenua[0] > .17f && atenua[2] > .17f && atenua[0] <= atenOtras + 1e-4f && atenua[1] == 0f, "las demas se apagaron un 20 % poco a poco; la elegida no");
            uint halo2 = (uint)Run(p, "HaloArgb");
            Check((halo2 >> 24) == 150 && ((halo2 >> 16) & 255) == 255 && (halo2 & 255) < 80, "enfocada, el halo toma el rojo de la portada y sube de 90 a 150");
            // El contorno y el viaje.
            object[] caja = { 96f, 96f, 158f, 158f };
            gpu.GetMethod("EnfocaCaja", F).Invoke(p, caja);
            Check(Math.Abs((float)caja[1] + (float)caja[3] - (250f + 4f * 1.12f)) < .05f && Math.Abs((float)caja[2] - 158f * 1.12f) < .05f,
                  "el contorno crece con ella desde el mismo punto");
            RectangleF viaje = (RectangleF)Run(p, "Enfocado", new RectangleF(100, 100, 150, 150));
            Check(Math.Abs(viaje.Width - 168f) < .01f && Math.Abs(viaje.Bottom - 250f) < .01f, "la portada que vuela al entrar al disco sale de donde se ve, ya crecida");
            // El halo no salta de color: si la portada cambia (los discos de "lo mas sonado" la van cambiando), va
            // hacia el color nuevo en medio segundo.
            using (Bitmap azul = Liso(30, 60, 220))
            {
                System.Threading.Thread.Sleep(30);
                Aplica(p, Estado(Info4(100, 100, 150, 150, azul), null, true));
                uint h1 = (uint)Run(p, "HaloArgb");
                for (int k = 0; k < 16; k++) { System.Threading.Thread.Sleep(60); Aplica(p, Estado(Info4(100, 100, 150, 150, azul), null, true)); }
                uint h2 = (uint)Run(p, "HaloArgb");
                Info("halo al cambiar de portada: " + ((h1 >> 16) & 255) + "," + ((h1 >> 8) & 255) + "," + (h1 & 255) + " -> un segundo despues " + ((h2 >> 16) & 255) + "," + ((h2 >> 8) & 255) + "," + (h2 & 255));
                Check(((h1 >> 16) & 255) > 180 && (h1 & 255) < 150 && ((h2 >> 16) & 255) < 90 && (h2 & 255) > 230, "si la portada cambia, el halo pasa al color nuevo sin saltar");
                Aplica(p, Estado(Info4(100, 100, 150, 150, roja), null, true));
            }
            // Cambiar de casilla vuelve a empezar, y si el cursor corre, la que se deja encoge desde lo que llego a crecer.
            cs = Aplica(p, Estado(Info4(300, 100, 150, 150, roja), null, true));
            Check(Math.Abs(Fl(Get(cs, "Sel"), "W") - 150f) < .01f, "al moverse a otra casilla vuelve a crecer desde cero");
            System.Threading.Thread.Sleep(50);
            Aplica(p, Estado(Info4(300, 100, 150, 150, roja), null, true));
            float medio = (float)Get(p, "enfoque");
            object cs2 = Aplica(p, Estado(Info4(500, 100, 150, 150, roja), Info4(300, 100, 150, 150, null), true));
            float wr = Fl(Get(cs2, "Relay"), "W");
            Info("a medio crecer (" + Fm(medio * 100, "0") + " %) se movio: el relevo sale de " + Fm(wr, "0.0") + " px");
            Check(medio > .05f && medio < .99f && Math.Abs(wr - 150f * (1f + crece * medio)) < .05f, "si el cursor corre, la que se deja encoge desde lo que llego a crecer (sin saltito)");
            // Fuera de la pared, o apagado: nada. Se mira pasado su tiempo (en el primer cuadro aun no habria crecido).
            Aplica(p, Estado(Info4(100, 100, 150, 150, roja), null, false));
            System.Threading.Thread.Sleep(220);
            object dentro = Aplica(p, Estado(Info4(100, 100, 150, 150, roja), null, false));
            Check(Math.Abs(Fl(Get(dentro, "Sel"), "W") - 150f) < .01f && (float)Get(p, "enfoque") == 0f && ((RectangleF)Run(p, "Enfocado", new RectangleF(1, 2, 3, 4))).Width == 3f,
                  "dentro de un disco, en el buscador o en la atraccion (no es la pared) no se toca nada");
            Set(p, "PortadaEnfocada", false);
            Aplica(p, Estado(Info4(100, 100, 150, 150, roja), null, true));
            System.Threading.Thread.Sleep(220);
            object apagada = Aplica(p, Estado(Info4(100, 100, 150, 150, roja), null, true));
            Check(Math.Abs(Fl(Get(apagada, "Sel"), "W") - 150f) < .01f && (uint)Run(p, "HaloArgb") == ((90u << 24) | 0x8C78FFu),
                  "con el interruptor apagado, ni crece ni cambia el halo");
        }

        // El cableado del cuadro.
        MethodInfo rf = gpu.GetMethod("RenderFrame", F);
        List<string> n = Codigo.Nombres(rf);
        int iBegin = n.IndexOf("Begin"), iApl = n.IndexOf("AplicaEnfoque"), iPrep = n.IndexOf("PreparaPantallaViva");
        int iFoco = n.IndexOf("DibujaFoco"), iFicha = n.IndexOf("DibujaFicha"), iProg = n.IndexOf("DrawSmoothProgress"), iTrans = n.LastIndexOf("DrawCube");
        Check(iApl >= 0 && iPrep >= 0 && iApl < iBegin && iPrep < iBegin, "el cuadro prepara el enfoque y sube el letrero y la ficha ANTES de abrir la escena (Direct3D 9 no deja despues)");
        Check(iFoco > iBegin && iFicha > iProg && iFicha < iTrans, "dibuja el letrero sobre el cubo elegido y la ficha encima de la interfaz y debajo del viaje y los avisos");
        Check(Codigo.Llama(rf, "HaloArgb") && Codigo.Llama(rf, "EnfocaCaja") && Codigo.Llama(rf, "ColorCelda"), "el halo, el contorno y el apagado de las casillas salen de sus funciones");
        Check(Codigo.Llama(gpu.GetMethod("StopGpu", F), "SueltaPantallaViva"), "al apagar la tarjeta se sueltan sus texturas");
        int lee, esc; Codigo.UsosDeCampo(form.GetMethod("BuildGpuCubeState", F), "Pared", out lee, out esc);
        Check(esc >= 1, "el estado de los cubos marca cuando el elegido es el de la pared");
        Check(Codigo.Llama(form.GetMethod("SelectedDiscRect", F), "Enfocado"), "el viaje al entrar a un disco pregunta donde se ve la portada");
    }

    // ================================================================================================
    //  2) EL LETRERO
    // ================================================================================================
    static void Letrero()
    {
        Console.WriteLine("\n  2) EL LETRERO DE LA PORTADA\n");
        Func<int, int, double, string> T = delegate (int c, int v, double s) { return (string)St(form, "TextoFoco", c, v, s); };
        Check(T(12, 0, 38 * 60) == "12 canciones · 38 min" && T(1, 0, 200) == "1 canción · 3 min", "\"12 canciones · 38 min\", y en singular \"1 canción\"");
        Check(T(3, 0, -1) == "3 canciones" && T(3, 0, 0) == "3 canciones", "si falta la duracion de alguna, no se inventan minutos");
        Check(T(0, 5, -1) == "5 videos" && T(0, 1, -1) == "1 video", "un disco de videos dice cuantos videos");
        Check(T(10, 0, 20) == "10 canciones · menos de 1 min" && T(20, 0, 7380) == "20 canciones · 2 h 03 min", "menos de un minuto y mas de una hora, bien dichos");
        if (gpu == null) return;
        MethodInfo alto = form.GetMethod("AltoFoco", F), ancho = form.GetMethod("AnchoFoco", F);
        bool cabe = true;
        foreach (int lado in new int[] { 40, 80, 128, 160, 240, 400 })
        {
            int cw = lado + 38;   // la casilla: la portada y su margen (como en Geometry)
            int a = (int)alto.Invoke(null, new object[] { lado }), w = (int)ancho.Invoke(null, new object[] { lado, cw });
            float y = (float)St(gpu, "LetreroY", 100f, lado * 1.12f, (float)a);
            if (w > cw - 8 || a < 18 || a > 34 || y < 100f || y + a > 100f + lado * 1.12f) cabe = false;
        }
        Check(cabe, "la pastilla nunca se sale de su casilla y va por DENTRO del borde de abajo de la portada (el nombre no se tapa)");
        MethodInfo pinta = form.GetMethod("PintaFoco", F);
        FontFamily fam = (FontFamily)St(form, "FamiliaUi");
        using (Bitmap b = new Bitmap((int)ancho.Invoke(null, new object[] { 150, 190 }), (int)alto.Invoke(null, new object[] { 150 }), PixelFormat.Format32bppPArgb))
        using (Bitmap chico = new Bitmap((int)ancho.Invoke(null, new object[] { 60, 80 }), (int)alto.Invoke(null, new object[] { 60 }), PixelFormat.Format32bppPArgb))
        {
            string dijo = (string)pinta.Invoke(null, new object[] { b, "12 canciones · 38 min", "12 canciones", fam });
            string dijo2 = (string)pinta.Invoke(null, new object[] { chico, "12 canciones · 38 min", "12 canciones", fam });
            Info("portada de 150: \"" + dijo + "\"; de 60: \"" + dijo2 + "\"");
            Check(dijo == "12 canciones · 38 min", "en una portada normal cabe entero");
            Check(dijo2 == "12 canciones", "en una muy chica se quitan los minutos antes que cortar el texto");
            int izq = b.Width, der = -1;
            for (int x = 0; x < b.Width; x++) for (int y = 0; y < b.Height; y++) if (b.GetPixel(x, y).A > 40) { if (x < izq) izq = x; if (x > der) der = x; }
            Check(izq >= 0 && der < b.Width && Math.Abs(izq - (b.Width - 1 - der)) <= 2, "la pastilla queda centrada y entera dentro de su imagen");
            b.Save(Path.Combine(dirPng, "letrero.png")); chico.Save(Path.Combine(dirPng, "letrero_chico.png"));
        }
    }

    // ================================================================================================
    //  3) LA FICHA RAPIDA
    // ================================================================================================
    static object Disco(string folder, string nombre, int canciones)
    {
        object a = Activator.CreateInstance(albumT, true);
        Set(a, "Folder", folder); Set(a, "Name", nombre); Set(a, "Artist", "Artista"); Set(a, "CoverPath", folder + ".jpg");
        IList tr = (IList)Get(a, "Tracks");
        for (int i = 0; i < canciones; i++)
        {
            object t = Activator.CreateInstance(trackT, true);
            Set(t, "Path", Path.Combine(folder, (i + 1).ToString("00") + " Cancion numero " + (i + 1) + ".mp3"));
            Set(t, "Title", "Cancion " + (i + 1)); Set(t, "Artist", "Artista");
            tr.Add(t);
        }
        return a;
    }
    static string RutaDe(object a, int i) { return (string)Get(((IList)Get(a, "Tracks"))[i], "Path"); }

    static void Ficha()
    {
        Console.WriteLine("\n  3) LA FICHA RAPIDA\n");
        int espera = Cte<int>(form, "FichaEsperaMs"), entra = Cte<int>(form, "FichaEntraMs"), dura = Cte<int>(form, "FichaDuraMs"), sale = Cte<int>(form, "FichaSaleMs");
        MethodInfo curva = form.GetMethod("CurvaFicha", F);
        Func<int, float> C = delegate (int t) { return (float)curva.Invoke(null, new object[] { t }); };
        Check(C(0) == 0f && C(espera - 1) == 0f && C(espera + entra) == 1f && C(espera + entra + dura - 1) == 1f && C(espera + entra + dura + sale) == 0f && C(-50) == 0f,
              "espera " + espera + " ms quieta, entra en " + entra + ", se queda " + (dura / 1000) + " s y se va en " + sale);
        bool mono = true;
        for (int t = espera; t < espera + entra; t++) if (C(t + 1) < C(t)) mono = false;
        for (int t = espera + entra + dura; t < espera + entra + dura + sale; t++) if (C(t + 1) > C(t)) mono = false;
        Check(mono, "entra y sale sin parpadeos (la opacidad solo sube al entrar y solo baja al irse)");

        MethodInfo lugar = form.GetMethod("LugarFicha", F);
        Rectangle pared = new Rectangle(12, 100, 1000, 540);
        PointF d = (PointF)lugar.Invoke(null, new object[] { new Rectangle(100, 120, 140, 140), pared, 312, 246 });
        PointF i2 = (PointF)lugar.Invoke(null, new object[] { new Rectangle(850, 400, 140, 140), pared, 312, 246 });
        Check(d.X == 256f && d.Y == 116f, "cabe a la derecha de la casilla: ahi va, a 16 px");
        Check(i2.X + 312 <= 850 - 16 + .01f && i2.Y + 246 <= pared.Bottom - 6 + .01f && i2.Y >= pared.Top, "si no cabe va a la izquierda, y nunca se sale de la pared");

        MethodInfo mas = form.GetMethod("MasPedidas", F);
        List<string> rutas = new List<string>(new string[] { "a", "b", "c", "d", "e" });
        Dictionary<string, int> cuentas = new Dictionary<string, int>();
        object[] args = { rutas, cuentas, false };
        List<int> top = (List<int>)mas.Invoke(null, args);
        Check(!(bool)args[2] && top.Count == 3 && top[0] == 0 && top[2] == 2, "si nunca se pidio ninguna, van las tres primeras (y dice \"algunas\", no \"las mas pedidas\")");
        cuentas["d"] = 7; cuentas["b"] = 3; cuentas["e"] = 3;
        args = new object[] { rutas, cuentas, false };
        top = (List<int>)mas.Invoke(null, args);
        Check((bool)args[2] && top.Count == 3 && top[0] == 3 && top[1] == 1 && top[2] == 4, "las mas pedidas de mas a menos, y a igualdad en el orden del disco");
        cuentas.Clear(); cuentas["c"] = 1;
        args = new object[] { rutas, cuentas, false };
        top = (List<int>)mas.Invoke(null, args);
        Check(top.Count == 1 && top[0] == 2, "si solo se pidio una, sale esa sola (no se rellena con las no pedidas)");
        args = new object[] { null, null, false };
        Check(((List<int>)mas.Invoke(null, args)).Count == 0, "y sin canciones no revienta");

        if (gpu == null) return;
        Size t1 = (Size)St(form, "TamanoFicha", 166, 181), t2 = (Size)St(form, "TamanoFicha", 40, 40), t3 = (Size)St(form, "TamanoFicha", 900, 900);
        Check(t1.Width == 312 && t2.Width == 260 && t2.Height == 170 && t3.Width == 480 && t3.Height == 300, "la tarjeta mide dos casillas de ancho, con minimo y maximo");

        // Pintada, para verla.
        FontFamily fam = (FontFamily)St(form, "FamiliaUi");
        using (Bitmap tapa = Liso(180, 60, 200))
        using (Bitmap b = new Bitmap(t1.Width, t1.Height, PixelFormat.Format32bppPArgb))
        using (Bitmap chica = new Bitmap(t2.Width, t2.Height, PixelFormat.Format32bppPArgb))
        using (Bitmap grande = new Bitmap(t3.Width, t3.Height, PixelFormat.Format32bppPArgb))
        {
            string[] filas = { "Volver volver", "El rey", "Mujeres divinas con un titulo muy largo que no cabe" };
            string[] durs = { "3:02", "3:14", "" }; int[] veces = { 14, 9, 1 };
            MethodInfo pf = form.GetMethod("PintaFicha", F);
            bool bien = true;
            foreach (Bitmap cual in new Bitmap[] { b, chica, grande })
                try { pf.Invoke(null, new object[] { cual, tapa, "Vicente Fernandez", "Sus mejores exitos", "12 canciones · 38 min", "Regional", Color.FromArgb(215, 120, 25), 2, true, filas, durs, veces, 12, fam }); }
                catch (Exception e) { bien = false; Info("no se pudo pintar: " + e.InnerException); }
            Check(bien, "se pinta a cualquier tamaño, de la mas chica a la mas grande (" + t2.Width + "x" + t2.Height + " a " + t3.Width + "x" + t3.Height + ")");
            int opacos = 0; for (int x = 0; x < b.Width; x += 3) for (int y = 0; y < b.Height; y += 3) if (b.GetPixel(x, y).A > 200) opacos++;
            Check(opacos > (b.Width / 3) * (b.Height / 3) * 8 / 10 && b.GetPixel(0, 0).A < 60, "la tarjeta se pinta llena, con las esquinas redondas");
            b.Save(Path.Combine(dirPng, "ficha.png")); chica.Save(Path.Combine(dirPng, "ficha_chica.png")); grande.Save(Path.Combine(dirPng, "ficha_grande.png"));
        }

        // EL RECORRIDO ENTERO, con una rocola armada sin encender.
        object f = Codigo.FormSinEncender(form);
        using (Control pared2 = (Control)Activator.CreateInstance(av, true))
        {
            pared2.SetBounds(12, 100, 1000, 540);
            Type lista = typeof(List<>).MakeGenericType(albumT);
            IList pag = (IList)Activator.CreateInstance(lista);
            for (int k = 0; k < 18; k++) pag.Add(Disco("d" + k, "Disco " + k, 12));
            Set(pared2, "Page", pag); Set(pared2, "Selected", 7);
            Set(f, "albumView", pared2); Set(f, "gpuEnabled", true); Set(f, "fichaDisco", true); Set(f, "portadaEnfocada", true);
            object elegido = pag[7];
            IDictionary dur = (IDictionary)Get(f, "durCache");
            for (int k = 0; k < 12; k++) dur[RutaDe(elegido, k)] = 190f;
            IDictionary pedidas = (IDictionary)Get(f, "songCounts");
            pedidas[RutaDe(elegido, 4)] = 9; pedidas[RutaDe(elegido, 1)] = 3;
            IList cola = (IList)Get(f, "queue");
            for (int k = 0; k < 2; k++) { object it = Activator.CreateInstance(itemT, true); Set(it, "Disc", elegido); cola.Add(it); }
            MethodInfo bf = form.GetMethod("BuildFichaState", F), bfo = form.GetMethod("BuildFocoState", F);
            object s0 = bf.Invoke(f, null);
            Check(!(bool)Get(s0, "Has"), "al llegar a un disco no sale nada todavia");
            Set(f, "fichaDesde", Environment.TickCount - (espera - 400));
            Check(!(bool)Get(bf.Invoke(f, null), "Has"), "y sigue sin salir antes de su hora");
            Set(f, "fichaDesde", Environment.TickCount - (espera - 200));
            object s1 = bf.Invoke(f, null);
            Check((bool)Get(s1, "Has") && (float)Get(s1, "Alpha") == 0f && Get(s1, "Bmp") != null, "un poco antes de salir ya esta horneada y se entrega INVISIBLE, para subirla antes");
            int ver = (int)Get(s1, "Ver");
            Set(f, "fichaDesde", Environment.TickCount - (espera + entra + 500));
            object s2 = bf.Invoke(f, null);
            float fx = (float)Get(s2, "X"), fy = (float)Get(s2, "Y");
            Rectangle celda = (Rectangle)Run(pared2, "RectElegida"); celda.Offset(pared2.Left, pared2.Top);
            Check((float)Get(s2, "Alpha") == 1f && (int)Get(s2, "Ver") == ver, "al salir se ve entera y NO se vuelve a hornear en cada cuadro");
            Check(fx >= celda.Right + 16 - .01f && fx + ((Bitmap)Get(s2, "Bmp")).Width <= pared2.Right && fy >= pared2.Top, "va al lado de la casilla y dentro de la pared");
            ((Bitmap)Get(s2, "Bmp")).Save(Path.Combine(dirPng, "ficha_rocola.png"));
            Set(f, "colaVer", 5);
            Check((int)Get(bf.Invoke(f, null), "Ver") == ver + 1, "si cambia la cola mientras se ve, se rehace (el numero de \"en la cola\" tiene que cuadrar)");
            Set(f, "fichaDesde", Environment.TickCount - (espera + entra + dura + sale + 10));
            Check(!(bool)Get(bf.Invoke(f, null), "Has"), "si nadie toca nada, se va sola");
            Set(f, "fichaDesde", Environment.TickCount - (espera + 500));
            Check(!(bool)Get(bf.Invoke(f, null), "Has"), "y no vuelve a salir hasta cambiar de disco");
            Set(pared2, "Selected", 8);
            Check(!(bool)Get(bf.Invoke(f, null), "Has"), "al mover el cursor se va en el acto");
            Set(f, "searchOpen", true); Set(f, "fichaDesde", Environment.TickCount - 5000);
            Check(!(bool)Get(bf.Invoke(f, null), "Has") && !(bool)Get(bfo.Invoke(f, null), "Has"), "con el buscador abierto no sale ni la ficha ni el letrero");
            Set(f, "searchOpen", false);
            Set(pared2, "Selected", 7);
            object fo = bfo.Invoke(f, null);
            int lado = (int)St(av, "LadoPortada", pared2.Width, pared2.Height);
            Bitmap fb = (Bitmap)Get(fo, "Bmp");
            Check((bool)Get(fo, "Has") && fb != null && fb.Width == (int)St(form, "AnchoFoco", lado, pared2.Width / 6) && fb.Height == (int)St(form, "AltoFoco", lado), "el letrero del disco elegido se hornea a la medida de su portada y su casilla");
            int vf = (int)Get(fo, "Ver");
            Check((int)Get(bfo.Invoke(f, null), "Ver") == vf, "y no se rehace mientras el disco sea el mismo");
            fb.Save(Path.Combine(dirPng, "letrero_rocola.png"));
            Set(f, "portadaEnfocada", false);
            Check(!(bool)Get(bfo.Invoke(f, null), "Has"), "con la portada enfocada apagada no hay letrero");
            Set(f, "fichaDisco", false); Set(f, "fichaDesde", Environment.TickCount - 5000);
            Check(!(bool)Get(bf.Invoke(f, null), "Has"), "y con la ficha apagada no hay ficha");
            Set(f, "fichaDisco", true);
            Set(elegido, "IsTop", true); Set(f, "fichaAlbum", null);
            bf.Invoke(f, null); Set(f, "fichaDesde", Environment.TickCount - 5000);
            Check(!(bool)Get(bf.Invoke(f, null), "Has"), "\"lo mas sonado\" no lleva ficha: ya es una lista de las mas pedidas");
        }
        List<MethodBase> hf = Codigo.MetodosConAyudantes(form, "HorneaFicha");
        Check(Codigo.LeeCampo(hf, "PeekCoverGpu") && !Codigo.LeeCampo(hf, "GetCover"), "la portadita de la ficha es la que ya esta en memoria: no encarga nada al disco");
    }

    // ================================================================================================
    //  4) PASAR DE PAGINA DESLIZANDO
    // ================================================================================================
    static int nVisibles, nMemoria;
    static Bitmap tapaComun;
    static Image Visible<T>(T a) { nVisibles++; return tapaComun; }
    static Image Memoria<T>(T a) { nMemoria++; return tapaComun; }

    static void Pagina()
    {
        Console.WriteLine("\n  4) PASAR DE PAGINA DESLIZANDO\n");
        int slide = Cte<int>(av, "SlideMs"), des = Cte<int>(av, "DesfaseCol"), viaje = Cte<int>(av, "ViajeCol");
        int cols = Cte<int>(form, "Cols");
        Info("columnas cada " + des + " ms, cada una viaja " + viaje + " ms; en total " + slide + " ms, como antes");
        MethodInfo curva = av.GetMethod("CurvaColumna", F), orden = av.GetMethod("OrdenColumna", F);
        Func<int, int, bool, float> R = delegate (int t, int o, bool vieja) { object[] a = { t, o, vieja, 0f }; return (float)curva.Invoke(null, a); };
        Func<int, int, bool, float> A = delegate (int t, int o, bool vieja) { object[] a = { t, o, vieja, 0f }; curva.Invoke(null, a); return (float)a[3]; };
        bool bordes = true, monot = true; float salto = 0f;
        for (int o = 0; o < 2 * cols; o++)
        {
            if (R(0, o, false) != 1f || R(slide, o, false) != 0f || R(slide + 100, o, true) != 0f) bordes = false;
            float ant = 2f;
            for (int t = 0; t <= slide; t++) { float v = R(t, o, false); if (v > ant + 1e-6f) monot = false; if (t > 0) salto = Math.Max(salto, ant - v); ant = v; }
        }
        Check(bordes, "las doce columnas (seis que salen y seis que entran) empiezan en su sitio de salida y llegan justo a los " + slide + " ms");
        Check(monot && salto < .03f, "ninguna retrocede ni da tirones (el paso mas grande de un milisegundo es " + Fm(salto * 100, "0.0") + " % del camino)");
        Check(R(slide - 1, 2 * cols - 1, false) > 0f && R(des - 1, 1, false) == 1f, "la ultima llega al final del tiempo, y cada una espera su turno");
        Check(A(0, 0, false) == 0f && A(slide, 11, false) == 1f && A(0, 0, true) == 1f && A(slide, 0, true) == 0f, "la que entra aparece al moverse; la que sale se apaga por el camino");

        // Ninguna alcanza a la de delante: simulado a milisegundos, en los dos sentidos y con dos anchos de pared.
        bool pisa = false; float hueco = 1e9f;
        foreach (int W in new int[] { 996, 1000, 1281 })
            foreach (int dir in new int[] { 1, -1 })
            {
                int cw = W / cols; float desde = dir * (float)W; bool izq = dir > 0;
                for (int t = 0; t <= slide + 5; t++)
                {
                    float[] x = new float[2 * cols];
                    for (int c = 0; c < cols; c++)
                    {
                        int ov = (int)orden.Invoke(null, new object[] { c, izq, true }), on = (int)orden.Invoke(null, new object[] { c, izq, false });
                        x[ov] = c * cw + desde * (R(t, ov, true) - 1f);
                        x[on] = c * cw + desde * R(t, on, false);
                    }
                    // en el orden de salida, cada una va por delante de la siguiente al menos una casilla
                    for (int j = 0; j + 1 < 2 * cols; j++)
                    {
                        float gap = izq ? (x[j + 1] - x[j]) : (x[j] - x[j + 1]);
                        if (gap < cw - .01f) pisa = true;
                        hueco = Math.Min(hueco, gap - cw);
                    }
                }
            }
        Check(!pisa, "la pagina que sale y la que entra nunca se pisan: van en fila y ninguna columna alcanza a la de delante (hueco minimo " + Fm(hueco, "0.0") + " px)");

        // Con una pared de verdad.
        using (Control v = (Control)Activator.CreateInstance(av, true))
        using (Bitmap tapa = Liso(250, 160, 30))
        {
            tapaComun = tapa;
            v.Size = new Size(1280, 720);
            Type lista = typeof(List<>).MakeGenericType(albumT);
            IList A1 = (IList)Activator.CreateInstance(lista), B1 = (IList)Activator.CreateInstance(lista);
            for (int k = 0; k < 18; k++) { A1.Add(Disco("a" + k, "Viejo " + k, 3)); B1.Add(Disco("b" + k, "Nuevo " + k, 3)); }
            Set(v, "GetCover", Delegate.CreateDelegate(av.GetField("GetCover", F).FieldType, typeof(PantallaViva).GetMethod("Visible", F).MakeGenericMethod(albumT)));
            Set(v, "PeekCoverGpu", Delegate.CreateDelegate(av.GetField("PeekCoverGpu", F).FieldType, typeof(PantallaViva).GetMethod("Memoria", F).MakeGenericMethod(albumT)));
            Set(v, "Page", A1); Set(v, "Selected", 3);
            Array buf = Array.CreateInstance(cellT, 36), buf18 = Array.CreateInstance(cellT, 18);
            MethodInfo grid = av.GetMethod("BuildGpuGrid", F);
            // Hornea la pagina vieja entera (el horneado va con presupuesto por cuadro: hacen falta varias vueltas).
            int vueltas = 0, listas = 0;
            for (; vueltas < 80 && listas < 18; vueltas++)
            {
                int m = (int)grid.Invoke(v, new object[] { buf });
                listas = 0; for (int k = 0; k < m; k++) if (Get(buf.GetValue(k), "Bmp") != null) listas++;
            }
            Info("la pagina de antes quedo horneada en " + vueltas + " cuadros");
            int quieta = (int)grid.Invoke(v, new object[] { buf });
            int sel = 0; for (int k = 0; k < quieta; k++) if ((bool)Get(buf.GetValue(k), "Sel")) sel++;
            Check(quieta == 18 && sel == 1 && (bool)Get(buf.GetValue(3), "Sel"), "quieta: 18 casillas y solo la elegida marcada como tal");
            IDictionary cache = (IDictionary)Get(v, "gpuCellCache");
            int enCaja = cache.Count;
            Set(v, "Page", B1);
            Set(v, "gpuLastSlideTick", Environment.TickCount - 5000);
            Run(v, "StartSlide", 1, 1280);
            nVisibles = 0; nMemoria = 0;
            int n = (int)grid.Invoke(v, new object[] { buf });
            bool viejasBien = true, slots = true, ninguna = true;
            for (int k = 0; k < n; k++) if ((bool)Get(buf.GetValue(k), "Sel")) ninguna = false;
            for (int k = 18; k < n; k++)
            {
                object c = buf.GetValue(k);
                if ((string)Get(c, "Key") != (string)Get(A1[k - 18], "Folder") || Get(c, "Bmp") == null)
                { if (viejasBien) Info("la casilla " + k + " sale con clave " + Get(c, "Key") + " e imagen " + (Get(c, "Bmp") != null)); viejasBien = false; }
                if ((int)Get(c, "Slot") != k) slots = false;
            }
            Check(n == 36, "pasando de pagina se entregan las dos: 18 que entran y 18 que salen (" + n + ")");
            Check(viejasBien && slots, "las que salen son las de la pagina de antes, con su imagen ya hecha y en sus propios puestos (18 a 35)");
            Check(nMemoria == 18 && nVisibles == 18 && cache.Count <= enCaja + 6, "la que sale no hornea nada ni pide portadas: solo mira lo que ya hay en memoria");
            Check(ninguna, "y mientras pasa, ninguna casilla esta elegida");
            Check((int)grid.Invoke(v, new object[] { buf18 }) == 18 && (string)Get(buf18.GetValue(0), "Key") == "b0", "si el buffer no da para dos paginas, la que falta es la que sale, nunca la que entra");
            // A medio camino: en la fila de arriba, las doce en fila y sin pisarse.
            Set(v, "slideTick", Environment.TickCount - 110);
            n = (int)grid.Invoke(v, new object[] { buf });
            List<float> fila = new List<float>();
            for (int k = 0; k < n; k++) { object c = buf.GetValue(k); int s = (int)Get(c, "Slot") % 18; if (s < cols) fila.Add((float)Get(c, "X")); }
            fila.Sort();
            bool enFila = fila.Count == 12; int cwv = 1280 / cols;
            for (int k = 0; k + 1 < fila.Count; k++) if (fila[k + 1] - fila[k] < cwv - .5f) enFila = false;
            Check(enFila, "a medio camino las doce columnas de la fila van en orden y sin montarse");
            // Llegada.
            Set(v, "slideTick", Environment.TickCount - slide - 20);
            n = (int)grid.Invoke(v, new object[] { buf });
            Check(n == 18 && Get(v, "slideViejas") == null && Math.Abs((float)Get(buf.GetValue(5), "X") - 5 * cwv) < .01f && (bool)Get(buf.GetValue(3), "Sel"),
                  "al llegar queda solo la nueva, en su sitio, con su elegida, y la vieja se suelta");
            // Dos seguidas: la que iba a medio entrar no sale de su sitio (seria un salto): se va de golpe.
            IList C1 = (IList)Activator.CreateInstance(lista); for (int k = 0; k < 18; k++) C1.Add(Disco("c" + k, "Otro " + k, 1));
            Set(v, "Page", C1); Run(v, "StartSlide", 1, 1280); grid.Invoke(v, new object[] { buf });
            IList D1 = (IList)Activator.CreateInstance(lista); for (int k = 0; k < 18; k++) D1.Add(Disco("e" + k, "Mas " + k, 1));
            Set(v, "Page", D1); Run(v, "StartSlide", 1, 1280);
            Check((int)grid.Invoke(v, new object[] { buf }) == 18, "si se pasa otra antes de que llegue la anterior, la de a medio entrar se va de golpe (como siempre) en vez de saltar");
            Set(v, "slideTick", -1); grid.Invoke(v, new object[] { buf });
            Set(v, "gpuLastSlideTick", Environment.TickCount - 5000);
            Run(v, "StartSlide", 1, 1280);
            Check((int)grid.Invoke(v, new object[] { buf }) == 18, "si la pagina no cambio no hay de donde salir");
            // Apagado: el hojeo de antes.
            Set(v, "slideTick", -1); Set(v, "PaginaDesliza", false); Set(v, "Page", B1); grid.Invoke(v, new object[] { buf });
            Set(v, "Page", A1); Set(v, "gpuLastSlideTick", Environment.TickCount - 5000); Run(v, "StartSlide", 1, 1280);
            Set(v, "slideTick", Environment.TickCount - 60);
            n = (int)grid.Invoke(v, new object[] { buf });
            float dx0 = (float)Get(buf.GetValue(0), "X"), dx5 = (float)Get(buf.GetValue(5), "X") - 5 * cwv;
            Check(n == 18 && Math.Abs(dx0 - dx5) < .01f && dx0 > 0f, "con el interruptor apagado es el deslizamiento de antes: una sola pagina, todas las columnas juntas");
            Check((float)Run(v, "TomaEmpuje") == 0f, "y el cielo no se corre");
            // El empujon del cielo.
            Set(v, "PaginaDesliza", true); Set(v, "slideTick", -1);
            foreach (int dir in new int[] { 1, -1 })
            {
                Run(v, "StartSlide", dir, 1280);
                float suma = 0f; int t0 = Environment.TickCount;
                for (int paso = 0; paso <= 12; paso++) { Set(v, "empujeDesde", t0 - paso * 25); suma += (float)Run(v, "TomaEmpuje"); }
                suma += (float)Run(v, "TomaEmpuje");
                float espera = -dir * Cte<float>(av, "EmpujeFr") * 1280f;
                Check(Math.Abs(suma - espera) < .05f, "el cielo se corre " + Fm(suma, "0.0") + " px al pasar " + (dir > 0 ? "adelante" : "atras") + " (un 4.5 % del ancho, en el sentido del movimiento)");
                Set(v, "slideTick", -1);
            }
            // Si la pared estaba tapada mientras pasaba (buscador), lo pendiente se tira: nada de saltos despues.
            Run(v, "StartSlide", 1, 1280);
            Set(v, "empujeDesde", Environment.TickCount - 2000);
            Check((float)Run(v, "TomaEmpuje") == 0f && (float)Run(v, "TomaEmpuje") == 0f, "si nadie lo pidio a tiempo, el cielo no se corre de golpe despues");
            Set(v, "slideTick", -1);
        }
        // Las estrellas se corren cada una segun su profundidad, sin salirse de la deriva.
        object campo = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(star);
        Set(campo, "driftX", new float[] { 10f, 500f, 990f });
        MethodInfo empuja = star.GetMethod("Empuja", F);
        empuja.Invoke(campo, new object[] { -30f, 1000 });
        float[] dr = (float[])Get(campo, "driftX");
        float[] kind = (float[])star.GetField("DerivaKind", F).GetValue(null);
        Check(Math.Abs(dr[0] - (10f - 30f * kind[0])) < .01f && Math.Abs(dr[1] - (500f - 30f * kind[1])) < .01f && Math.Abs(dr[2] - (990f - 30f * kind[2])) < .01f,
              "cada tamaño de estrella se corre segun lo lejos que esta (el polvo " + Fm(kind[0], "0.00") + ", las grandes " + Fm(kind[2], "0.00") + ")");
        empuja.Invoke(campo, new object[] { 50f, 1000 }); empuja.Invoke(campo, new object[] { -2000f, 1000 }); empuja.Invoke(campo, new object[] { float.NaN, 1000 });
        dr = (float[])Get(campo, "driftX");
        Check(dr[0] >= 0f && dr[0] < 1000f && dr[1] >= 0f && dr[1] < 1000f && dr[2] >= 0f && dr[2] < 1000f, "y la deriva se queda siempre dentro de la pantalla (envuelve), aunque le llegue un disparate");
        bool cablea = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(form)) { List<string> l = Codigo.Nombres(m); if (l.Contains("BuildGpuGrid") && l.Contains("TomaEmpuje")) cablea = true; }
        Check(cablea && Codigo.Llama(gpu.GetMethod("PreparaPantallaViva", F), "Empuja"), "la rejilla le pasa el empujon al panel y el panel se lo pasa a las estrellas");
        bool soloGpu = true;
        foreach (MethodBase m in Codigo.TodosLosMetodos(form))
            if (m.Name != "BuildGpuGrid" && m.Name != "CeldasViejas" && Codigo.Llama(m, "CurvaColumna")) soloGpu = false;
        Check(soloGpu, "solo la rejilla de la tarjeta grafica usa la curva por columnas");
    }

    // ================================================================================================
    //  5) LA LUPA
    // ================================================================================================
    static Bitmap PintaBarra(Control bar, bool paraGpu)
    {
        Bitmap b = new Bitmap(bar.Width, bar.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b)) { g.Clear(Color.FromArgb(10, 14, 36)); bar.GetType().GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(bool) }).Invoke(bar, new object[] { g, paraGpu }); }
        return b;
    }
    static int Cuenta(Bitmap b, int x0, int x1, int y0, int y1, Predicate<Color> p)
    {
        int n = 0;
        for (int x = Math.Max(0, x0); x < Math.Min(b.Width, x1); x++) for (int y = Math.Max(0, y0); y < Math.Min(b.Height, y1); y++) if (p(b.GetPixel(x, y))) n++;
        return n;
    }
    static int Distintos(Bitmap a, Bitmap b, int x0, int x1, int y0, int y1)
    {
        int n = 0;
        for (int x = Math.Max(0, x0); x < Math.Min(a.Width, x1); x++) for (int y = Math.Max(0, y0); y < Math.Min(a.Height, y1); y++) if (a.GetPixel(x, y) != b.GetPixel(x, y)) n++;
        return n;
    }
    static void Lupa()
    {
        Console.WriteLine("\n  5) LA BARRA DE LETRAS CON LUPA\n");
        MethodInfo esc = lbT.GetMethod("EscalaLupa", F);
        Func<float, float> E = delegate (float d) { return (float)esc.Invoke(null, new object[] { d }); };
        float max = Cte<float>(lbT, "LupaMax");
        Check(Math.Abs(E(0) - (1 + max)) < 1e-5 && Math.Abs(E(1) - E(-1)) < 1e-6 && E(1) < E(0) && E(2) < E(1) && E(5) < 1.0001f,
              "la del centro crece un " + Fm(max * 100, "0") + " %, las vecinas " + Fm((E(1) - 1) * 100, "0") + " y " + Fm((E(2) - 1) * 100, "0") + " %, y a cinco casillas nada");
        float tope = (float)St(lbT, "TopeLupa", 1.55f, 30f, 34.5f, 10f, 28f);
        Check(Math.Abs(tope - (31.5f / 30f)) < 1e-4 && (float)St(lbT, "TopeLupa", 1.55f, 60f, 34.5f, 10f, 28f) == 1f, "una letra que ya llena su casilla no crece (no se come a las vecinas)");
        MethodInfo raya = lbT.GetMethod("RayaLupa", F);
        Check((float)raya.Invoke(null, new object[] { 300, 300, 34.5f }) == 24.5f && (float)raya.Invoke(null, new object[] { 1, 300, 34.5f }) == 3f
              && (float)raya.Invoke(null, new object[] { 0, 300, 34.5f }) == 0f, "la rayita va de 3 px (casi nada) a toda la casilla (la letra que mas tiene)");

        using (Control bar = (Control)Activator.CreateInstance(lbT, true))
        using (Control bar2 = (Control)Activator.CreateInstance(lbT, true))
        {
            string letras = (string)Get(bar, "Letters");
            foreach (Control b in new Control[] { bar, bar2 })
            {
                b.SetBounds(0, 0, 1000, 40);
                HashSet<char> disp = new HashSet<char>(); foreach (char ch in letras) disp.Add(ch);
                Dictionary<char, int> cnt = new Dictionary<char, int>();
                for (int i = 2; i < 28; i++) cnt[letras[i]] = 20 + (i * 37) % 90;
                cnt['A'] = 300; cnt['N'] = 10;
                Set(b, "Available", disp); Set(b, "Counts", cnt); Set(b, "cur", 'M');
            }
            int iM = letras.IndexOf('M'), iA = letras.IndexOf('A'), iN = letras.IndexOf('N'), iL = letras.IndexOf('L');
            float slot = 1000f / letras.Length;
            Set(bar, "LupaGpu", false);
            Bitmap cpuSin = PintaBarra(bar, false), gpuSin = PintaBarra(bar, true);
            Set(bar, "LupaGpu", true);
            Bitmap cpuCon = PintaBarra(bar, false), gpuCon = PintaBarra(bar, true);
            Check(Distintos(cpuSin, cpuCon, 0, 1000, 0, 40) == 0, "el dibujo por procesador sale IDENTICO con la lupa encendida o apagada (40 000 pixeles comparados)");
            Predicate<Color> blanco = delegate (Color c) { return c.R > 240 && c.G > 240 && c.B > 240; };
            Predicate<Color> tinta = delegate (Color c) { return c.R > 150; };
            int m0 = Cuenta(gpuSin, (int)(iM * slot), (int)((iM + 1) * slot), 0, 28, blanco), m1 = Cuenta(gpuCon, (int)(iM * slot), (int)((iM + 1) * slot), 0, 28, blanco);
            int l0 = Cuenta(gpuSin, (int)(iL * slot), (int)((iL + 1) * slot), 0, 26, tinta), l1 = Cuenta(gpuCon, (int)(iL * slot), (int)((iL + 1) * slot), 0, 26, tinta);
            Info("la M elegida: " + m0 + " -> " + m1 + " pixeles blancos; la L de al lado: " + l0 + " -> " + l1 + " de tinta");
            Check(m1 > m0 * 1.5, "con la lupa la letra elegida se ve bastante mas grande");
            Check(l1 > l0 * 1.2, "y la de al lado tambien crece");
            Check(Distintos(gpuSin, gpuCon, (int)(iA * slot) + 1, (int)((iA + 1) * slot) - 1, 0, 25) == 0, "las lejanas se quedan exactamente igual");
            int ra = Distintos(gpuSin, gpuCon, (int)(iA * slot), (int)((iA + 1) * slot), 26, 29), rn = Distintos(gpuSin, gpuCon, (int)(iN * slot), (int)((iN + 1) * slot), 26, 29);
            Info("rayita de la A (300 canciones): " + ra + " pixeles; de la N (10): " + rn);
            Check(ra > rn * 3 && rn > 0, "la rayita dice donde hay mas musica: la de la letra que mas tiene es mucho mas larga");
            gpuCon.Save(Path.Combine(dirPng, "lupa.png")); gpuSin.Save(Path.Combine(dirPng, "lupa_sin.png"));
            Set(bar2, "LupaGpu", true); PintaBarra(bar2, true);
            Check(Get(bar2, "lupaCache") != null && Get(bar2, "stripCache") == null, "la lupa usa su PROPIA tira: la del dibujo de siempre ni se crea");
            cpuSin.Dispose(); cpuCon.Dispose(); gpuSin.Dispose(); gpuCon.Dispose();
        }
        int lee, esc2; Codigo.UsosDeCampo(form.GetMethod("UiSig", F), "LupaGpu", out lee, out esc2);
        Check(lee >= 1, "encender o apagar la lupa rehace la textura de la barra (entra en su firma)");
    }

    // ================================================================================================
    //  6) CTRL+F3 -> PANTALLA
    // ================================================================================================
    static void Ajustes()
    {
        Console.WriteLine("\n  6) CTRL+F3 -> PANTALLA\n");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_pantallaviva_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        form.GetField("carpetaDatos", F).SetValue(null, dir);
        object owner = Codigo.FormSinEncender(form);
        string[] nom = { "Portada enfocada", "Ficha rápida del disco", "Pasar de página deslizando", "Barra de letras con lupa" };
        string[] campo = { "portadaEnfocada", "fichaDisco", "paginaDesliza", "letrasLupa" };
        Control panel = gpu != null ? (Control)Activator.CreateInstance(gpu, true) : null;
        Control pared = gpu != null ? (Control)Activator.CreateInstance(av, true) : null;
        Control barra = (Control)Activator.CreateInstance(lbT, true);
        if (gpu != null) { Set(owner, "gpuPanel", panel); Set(owner, "albumView", pared); }
        Set(owner, "letterBar", barra);
        using (Font f = new Font("Segoe UI", 9))
        using (Panel page = (Panel)form.GetMethod("PaginaPantalla", F).Invoke(owner, new object[] { f, f, f, Color.White, Color.Gray, Color.Gray }))
        {
            Control[] sw = new Control[4]; bool estan = true;
            for (int i = 0; i < 4; i++) { sw[i] = Find(page, nom[i]); if (sw[i] == null) estan = false; }
            Check(estan, "la pestaña tiene sus cuatro interruptores: portada enfocada, ficha rapida, pagina deslizando y lupa");
            if (!estan) return;
            bool dentro = true; int fondo = 0;
            foreach (Control card in page.Controls)
            {
                fondo = Math.Max(fondo, card.Bottom);
                if (card.Right > page.Width) dentro = false;
                foreach (Control c in card.Controls)
                {
                    if (card is Panel && (c.Left < 0 || c.Top < 0 || c.Right > card.Width || c.Bottom > card.Height)) dentro = false;
                    foreach (Control d in card.Controls) if (c != d && c.Bounds.IntersectsWith(d.Bounds)) dentro = false;
                }
            }
            Check(dentro && fondo <= page.Height, "todo cabe en su sitio sin taparse (lo mas bajo llega a " + fondo + " de " + page.Height + ")");
            bool guarda = true, aplica = true;
            PropertyInfo marcado = sw[0].GetType().GetProperty("Checked", F);
            for (int mask = 15; mask >= 0; mask--)
            {
                // Primero todo encendido: la rocola armada sin constructor lo tiene apagado y marcar lo que ya esta no avisa.
                for (int i = 0; i < 4; i++) marcado.SetValue(sw[i], (mask & (1 << i)) != 0, null);
                form.GetMethod("SaveConfigPrefs", F).Invoke(owner, null);
                Codigo.EsperaGuardado(form, owner);
                object leido = Codigo.FormSinEncender(form); form.GetMethod("LoadConfigPrefs", F).Invoke(leido, null);
                for (int i = 0; i < 4; i++) if ((bool)Get(leido, campo[i]) != ((mask & (1 << i)) != 0) || (bool)Get(owner, campo[i]) != ((mask & (1 << i)) != 0)) guarda = false;
                if (gpu != null)
                {
                    if ((bool)Get(panel, "PortadaEnfocada") != ((mask & 1) != 0)) aplica = false;
                    if ((bool)Get(pared, "PaginaDesliza") != ((mask & 4) != 0)) aplica = false;
                    if ((bool)Get(barra, "LupaGpu") != ((mask & 8) != 0)) aplica = false;
                }
            }
            Check(guarda, "las 16 combinaciones se guardan en rocola_config.txt y se leen igual");
            if (gpu != null) Check(aplica, "y cada una se aplica al momento: al panel, a la pared y a la barra de letras");
            Control reset = Find(page, "Pantalla como de fabrica");
            try { typeof(Control).GetMethod("OnClick", F).Invoke(reset, new object[] { EventArgs.Empty }); } catch { }
            bool todos = true; for (int i = 0; i < 4; i++) if (!(bool)Get(owner, campo[i]) || !(bool)marcado.GetValue(sw[i], null)) todos = false;
            Check(reset != null && todos, "\"Pantalla como de fabrica\" las vuelve a encender todas");
        }
        try { Directory.Delete(dir, true); } catch { }
        Check(Codigo.Llama(form.GetMethod("VentanaAjustes", F), "PaginaPantalla"), "la ventana de Ctrl+F3 monta la pestaña");
        List<MethodBase> aef = Codigo.MetodosConAyudantes(form, "AplicaEfectosGpu");
        if (gpu != null)
            Check(Codigo.EscribeCampo(aef, "PortadaEnfocada") && Codigo.EscribeCampo(aef, "PaginaDesliza") && Codigo.EscribeCampo(aef, "LupaGpu"), "AplicaEfectosGpu lleva los tres a donde se dibujan");
        else
        {
            Check(lbT.GetField("LupaGpu", F) == null && av.GetField("PaginaDesliza", F) == null && form.GetMethod("BuildFichaState", F) == null,
                  "sin tarjeta grafica no hay nada de esto en el dibujo: solo se guardan los interruptores");
        }
    }


    // ================================================================================================
    //  7) LO QUE CUESTA (medido aqui, con Mono: sirve para comparar, no son los milisegundos del bar)
    // ================================================================================================
    static double Mide(int n, Action a)
    {
        for (int i = 0; i < 3; i++) a();
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < n; i++) a();
        return sw.Elapsed.TotalMilliseconds / n;
    }
    static void Coste()
    {
        Console.WriteLine("\n  7) LO QUE CUESTA (Mono, esta maquina: para comparar)\n");
        // La barra: rehacer la tira de la lupa contra rehacer la de siempre (solo pasa al cambiar de letra).
        using (Control bar = (Control)Activator.CreateInstance(lbT, true))
        using (Bitmap lienzo = new Bitmap(1000, 40, PixelFormat.Format32bppPArgb))
        using (Graphics g = Graphics.FromImage(lienzo))
        {
            bar.SetBounds(0, 0, 1000, 40);
            string letras = (string)Get(bar, "Letters");
            HashSet<char> disp = new HashSet<char>(); foreach (char ch in letras) disp.Add(ch);
            Dictionary<char, int> cnt = new Dictionary<char, int>(); for (int i = 2; i < 28; i++) cnt[letras[i]] = 20 + i * 3;
            Set(bar, "Available", disp); Set(bar, "Counts", cnt); Set(bar, "cur", 'M');
            MethodInfo rc = lbT.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(bool) });
            Set(bar, "LupaGpu", false);
            double normal = Mide(40, delegate { Set(bar, "scCur", '\0'); rc.Invoke(bar, new object[] { g, true }); });
            Set(bar, "LupaGpu", true);
            double lupa = Mide(40, delegate { Set(bar, "luCentro", int.MinValue); rc.Invoke(bar, new object[] { g, true }); });
            double quieta = Mide(80, delegate { rc.Invoke(bar, new object[] { g, true }); });
            Info("barra de letras: rehacer la tira de siempre " + Fm(normal, "0.00") + " ms; la de la lupa " + Fm(lupa, "0.00") + " ms; sin cambios (copiar la tira) " + Fm(quieta, "0.00") + " ms");
            Check(lupa < normal * 2.5 + .5, "la lupa cuesta del orden de la barra de siempre al cambiar de letra, y nada si no cambia");
        }
        // La ficha y el letrero: se hornean UNA vez por parada, nunca por cuadro.
        FontFamily fam = (FontFamily)St(form, "FamiliaUi");
        using (Bitmap tapa = Liso(90, 140, 200))
        using (Bitmap b = new Bitmap(312, 246, PixelFormat.Format32bppPArgb))
        using (Bitmap pill = new Bitmap(158, 30, PixelFormat.Format32bppPArgb))
        {
            MethodInfo pf = form.GetMethod("PintaFicha", F), po = form.GetMethod("PintaFoco", F);
            string[] filas = { "Volver volver", "El rey", "Mujeres divinas" }, durs = { "3:02", "3:14", "3:25" }; int[] veces = { 14, 9, 1 };
            double ficha = Mide(20, delegate { pf.Invoke(null, new object[] { b, tapa, "Vicente Fernandez", "Sus exitos", "12 canciones · 38 min", "Regional", Color.Orange, 2, true, filas, durs, veces, 12, fam }); });
            double foco = Mide(40, delegate { po.Invoke(null, new object[] { pill, "12 canciones · 38 min", "12 canciones", fam }); });
            object[] ca = { tapa, 0xFF000000u, 0u }; MethodInfo cdp = gpu.GetMethod("ColorDePortada", F);
            double color = Mide(200, delegate { cdp.Invoke(null, ca); });
            Info("ficha rapida " + Fm(ficha, "0.00") + " ms y letrero " + Fm(foco, "0.00") + " ms (una vez por disco en el que se para); color de la portada " + Fm(color * 1000, "0") + " us (una vez por portada)");
            Check(ficha < 60 && foco < 20, "hornear la ficha y el letrero es cosa de milisegundos, y solo al pararse en un disco");
        }
        // La rejilla pasando de pagina (dos paginas, todo ya horneado) contra quieta, y el enfoque de cada cuadro.
        using (Control v = (Control)Activator.CreateInstance(av, true))
        using (Bitmap tapa = Liso(250, 160, 30))
        using (Control p = (Control)Activator.CreateInstance(gpu, true))
        {
            tapaComun = tapa; v.Size = new Size(1280, 720);
            Type lista = typeof(List<>).MakeGenericType(albumT);
            IList A1 = (IList)Activator.CreateInstance(lista), B1 = (IList)Activator.CreateInstance(lista);
            for (int k = 0; k < 18; k++) { A1.Add(Disco("ca" + k, "Viejo " + k, 1)); B1.Add(Disco("cb" + k, "Nuevo " + k, 1)); }
            Set(v, "GetCover", Delegate.CreateDelegate(av.GetField("GetCover", F).FieldType, typeof(PantallaViva).GetMethod("Visible", F).MakeGenericMethod(albumT)));
            Set(v, "PeekCoverGpu", Delegate.CreateDelegate(av.GetField("PeekCoverGpu", F).FieldType, typeof(PantallaViva).GetMethod("Memoria", F).MakeGenericMethod(albumT)));
            Array buf = Array.CreateInstance(cellT, 36); MethodInfo grid = av.GetMethod("BuildGpuGrid", F);
            Set(v, "Page", B1); for (int k = 0; k < 60; k++) grid.Invoke(v, new object[] { buf });
            Set(v, "Page", A1); for (int k = 0; k < 60; k++) grid.Invoke(v, new object[] { buf });
            double quieta = Mide(200, delegate { grid.Invoke(v, new object[] { buf }); });
            Set(v, "Page", B1); Set(v, "gpuLastSlideTick", Environment.TickCount - 5000); Run(v, "StartSlide", 1, 1280);
            double hojeando = Mide(200, delegate { Set(v, "slideTick", Environment.TickCount - 100); grid.Invoke(v, new object[] { buf }); });
            int n = (int)grid.Invoke(v, new object[] { buf });
            Info("rejilla por cuadro: quieta " + Fm(quieta * 1000, "0") + " us (18 casillas); pasando de pagina " + Fm(hojeando * 1000, "0") + " us (" + n + " casillas)");
            Check(n == 36 && hojeando < quieta * 3 + .2, "pasando de pagina la rejilla entrega el doble de casillas sin hornear nada: cuesta del orden de dos cuadros quietos");
            Array rej = (Array)Get(p, "gridBuf");
            for (int i = 0; i < 18; i++) { object c = Activator.CreateInstance(cellT); Set(c, "Sel", i == 4); rej.SetValue(c, i); }
            Set(p, "gridN", 18); Set(p, "PortadaEnfocada", true); Set(p, "ShowAlbum", true);
            object cs0 = Estado(Info4(100, 100, 150, 150, tapa), null, true);
            double enf = Mide(500, delegate { Aplica(p, cs0); });
            Info("portada enfocada por cuadro (crecer, apagar 18 casillas, color ya calculado): " + Fm(enf * 1000, "0.0") + " us");
            Check(enf < .2, "el enfoque de cada cuadro cuesta microsegundos");
        }
    }

    [STAThread]
    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        form = asm.GetType("Rocola.Form1"); gpu = asm.GetType("Rocola.GpuPanel"); star = asm.GetType("Rocola.GpuStarfield");
        av = form.GetNestedType("AlbumView", F); lbT = form.GetNestedType("LetterBar", F);
        albumT = form.GetNestedType("Album", F); trackT = form.GetNestedType("Track", F); itemT = form.GetNestedType("PlayItem", F);
        cellT = asm.GetType("Rocola.GpuGridCell"); cubeT = asm.GetType("Rocola.GpuCubeState"); infoT = asm.GetType("Rocola.GpuCubeInfo");
        dirPng = Path.Combine(Path.GetTempPath(), "rocola_pantallaviva_png"); Directory.CreateDirectory(dirPng);
        Console.WriteLine("  perfil: " + (gpu != null ? "con tarjeta grafica" : "sin tarjeta grafica (solo la pestaña de ajustes)"));
        if (gpu != null) { Enfocada(); Letrero(); Ficha(); Pagina(); Lupa(); }
        else Letrero();
        Ajustes();
        if (gpu != null) Coste();
        Console.WriteLine("\n  imagenes para mirar: " + dirPng);
        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN (" + oks + " comprobaciones)" : fallos + " FALLO(S)"));
        return fallos;
    }
}
