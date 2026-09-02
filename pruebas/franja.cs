using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

// LA FRANJA DE ABAJO: LA PASTILLA DEL GENERO, Y QUE LOS CREDITOS SE LEAN.
//
// Abajo del todo va la tarjeta de lo que suena: la caratula, el titulo, el artista, lo que sigue y la barra
// de duracion; y a la derecha las dos capsulas, "En cola" y "Creditos".
//
// Dos cosas se comprueban aqui:
//
//   1) LA PASTILLA DEL GENERO. Dentro de un disco y en el buscador cada cancion ensena de que es, pero al
//      SONAR no se veia por ningun lado. Ahora sale detras del artista, con el color de su genero, y dice lo
//      mismo que las otras dos (todas salen de GeneroDeCancion). Un video no lleva -no hay de donde sacarla-
//      y si el genero no se sabe, tampoco: mejor sin etiqueta que con una inventada.
//
//   2) QUE LOS CREDITOS SE LEAN DE LEJOS. El alto de la capsula manda el cuerpo de su letra, y estaba clavado
//      en 26 px: los creditos y la cola salian en letra MAS CHICA que todo lo demas de la franja, siendo lo
//      que el cliente tiene que poder leer desde la barra del local.
//
// Lo del tamano no se comprueba leyendo un numero: se DIBUJA la capsula de verdad, la de antes y la de ahora,
// y se miden los pixeles de tinta que ocupa la palabra "Creditos".
class Franja
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const int CORTE = 190;   // alfa a partir del cual es TEXTO y no el vidrio de la capsula

    static Type TF;
    static object rocola;
    static Type Nes(string n) { return TF.GetNestedType(n, BindingFlags.NonPublic | BindingFlags.Public); }
    static FieldInfo Campo(Type t, string n) { return t.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); }
    static bool Pon(Type t, object o, string campo, object v)
    {
        FieldInfo f = Campo(t, campo);
        if (f == null) return false;
        f.SetValue(o, v);
        return true;
    }
    static object Lee(Type t, object o, string campo) { FieldInfo f = Campo(t, campo); return (f == null) ? null : f.GetValue(o); }
    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    // Lo que ocupa la TINTA (el texto) dentro de un dibujo: alto en pixeles y cuantos pixeles son.
    static void Tinta(Bitmap b, out int alto, out int cuantos)
    {
        int arriba = b.Height, abajo = -1; cuantos = 0;
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
                if (b.GetPixel(x, y).A >= CORTE) { cuantos++; if (y < arriba) arriba = y; if (y > abajo) abajo = y; }
        alto = (abajo >= arriba) ? (abajo - arriba + 1) : 0;
    }

    static Bitmap DibujaCapsula(Type TPas, string etiq, string valor, int alto, FontFamily fam)
    {
        MethodInfo mAncho = TPas.GetMethod("Ancho", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        int w = (int)mAncho.Invoke(null, new object[] { etiq, valor, alto, fam, false });
        Control c = (Control)Activator.CreateInstance(TPas, true);
        c.Font = new Font(fam, 9f);
        c.SetBounds(0, 0, w, alto);
        Pon(TPas, c, "Etiqueta", etiq); Pon(TPas, c, "Valor", valor);
        Pon(TPas, c, "Acento", Color.FromArgb(120, 240, 140));
        Bitmap b = new Bitmap(w, alto, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            TPas.GetMethod("RenderContent", new Type[] { typeof(Graphics) }).Invoke(c, new object[] { g });
        }
        return b;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        Type TPas = Nes("PastillaView"), TGen = Nes("GeneroView");
        Type TTrk = Nes("Track"), TAlb = Nes("Album"), TIt = Nes("PlayItem");
        FontFamily fam = Fam();

        Console.WriteLine();

        // ================= 1) LA PASTILLA DEL GENERO =================
        if (TGen == null)
        {
            Check(false, "al sonar una cancion se ve de que genero es (no existe la pastilla)");
        }
        else
        {
            rocola = FormatterServices.GetUninitializedObject(TF);
            Control gv = (Control)Activator.CreateInstance(TGen, true);
            gv.Font = new Font(fam, 10f, FontStyle.Bold);
            Pon(TF, rocola, "pastGenero", gv);

            Label lArt = new Label(); lArt.Font = new Font(fam, 11.5f, FontStyle.Bold); lArt.Text = "";
            Label lTit = new Label(); lTit.Font = new Font(fam, 15f, FontStyle.Bold); lTit.Text = "";
            Label lMej = new Label(); lMej.Font = new Font(fam, 12f, FontStyle.Bold); lMej.Text = "MEJORADA";
            lArt.SetBounds(114, 40, 600, 21);
            Pon(TF, rocola, "lblNow", lArt); Pon(TF, rocola, "lblNowTop", lTit); Pon(TF, rocola, "lblMejorada", lMej);
            Pon(TF, rocola, "mejRowW", 600);
            Pon(TF, rocola, "sbrOn", false);
            Pon(TF, rocola, "songGenre", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            Pon(TF, rocola, "discTag", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            Pon(TF, rocola, "artistTag", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            Pon(TF, rocola, "nombreTag", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

            // Una cancion sonando, de un genero que la rocola ya sabe.
            object tk = Activator.CreateInstance(TTrk, true);
            TTrk.GetField("Path").SetValue(tk, @"C:\Musica\Grabaciones\Uno\01 La Bamba.mp3");
            // OJO con el nombre de la carpeta: si dijera "Disco", "Cumbias" o cualquier otra palabra de
            // genero, la rocola lo adivinaria por ahi (paso 4 de GeneroDeCancion) y esta prueba estaria
            // midiendo otra cosa. Por eso la carpeta es un nombre que no dice nada.
            object alb = Activator.CreateInstance(TAlb, true);
            TAlb.GetField("Folder").SetValue(alb, @"C:\Musica\Grabaciones\Uno");
            TAlb.GetField("Artist").SetValue(alb, "Los Lobos del Norte");
            object it = Activator.CreateInstance(TIt, true);
            TIt.GetField("Song").SetValue(it, tk); TIt.GetField("Disc").SetValue(it, alb);
            Pon(TF, rocola, "nowPlaying", it);
            IDictionary sg = (IDictionary)Lee(TF, rocola, "songGenre");
            sg[@"C:\Musica\Grabaciones\Uno\01 La Bamba.mp3"] = "Cumbia";

            MethodInfo mGen = TF.GetMethod("ActualizaGenero", BindingFlags.Instance | BindingFlags.NonPublic);
            if (mGen == null) { Check(false, "no existe ActualizaGenero"); }
            else
            {
                lArt.Text = "Los Lobos del Norte";
                mGen.Invoke(rocola, null);
                string txt = (string)TGen.GetProperty("Texto").GetValue(gv, null);
                Console.WriteLine("  suena \"La Bamba\" de Los Lobos del Norte (Cumbia)");
                Console.WriteLine("  la pastilla dice: \"" + txt + "\"    " + gv.Width + "x" + gv.Height + " px, en x=" + gv.Left);
                Check(txt == "Cumbia", "la pastilla dice el genero de la cancion que suena");
                Check(gv.Visible, "y se ve");

                int anchoArt = TextRenderer.MeasureText(lArt.Text, lArt.Font).Width;
                Check(gv.Left >= lArt.Left + anchoArt, "va DETRAS del artista, sin pisarlo (artista hasta x="
                      + (lArt.Left + anchoArt) + ", pastilla desde x=" + gv.Left + ")");
                Check(gv.Left + gv.Width <= lArt.Left + 600, "y sin salirse del renglon");
                Check(gv.Top >= lArt.Top && gv.Top + gv.Height <= lArt.Top + lArt.Height + 1, "centrada en el renglon del artista");

                // El color es el del genero, el mismo que usa la barra de letras y la lista del disco.
                MethodInfo mCol = TF.GetMethod("ColorDeEtiqueta", BindingFlags.Static | BindingFlags.NonPublic);
                Color esperado = (Color)mCol.Invoke(null, new object[] { "Cumbia" });
                Color tinte = (Color)Lee(TGen, gv, "Tinte");
                Check(tinte.ToArgb() == esperado.ToArgb(), "y del color de su genero, el mismo de siempre");

                // Y se dibuja de verdad: que haya tinta dentro.
                using (Bitmap b = new Bitmap(gv.Width, gv.Height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        TGen.GetMethod("RenderContent", new Type[] { typeof(Graphics) }).Invoke(gv, new object[] { g });
                    }
                    int alto, n; Tinta(b, out alto, out n);
                    Check(n > 30 && alto >= 7, "se dibuja: " + n + " pixeles de texto, " + alto + " px de alto");
                }

                // Un VIDEO no lleva genero, y una cancion cuyo genero no se sabe tampoco: sin pastilla.
                TIt.GetField("IsVideo").SetValue(it, true);
                mGen.Invoke(rocola, null);
                Check(!gv.Visible, "un video no lleva pastilla de genero");
                TIt.GetField("IsVideo").SetValue(it, false);
                sg.Remove(@"C:\Musica\Grabaciones\Uno\01 La Bamba.mp3");
                mGen.Invoke(rocola, null);
                Check(!gv.Visible, "y si el genero no se sabe, no se inventa: tampoco sale");

                // Al aprenderse el genero en segundo plano, aparece sola.
                sg[@"C:\Musica\Grabaciones\Uno\01 La Bamba.mp3"] = "Rock";
                mGen.Invoke(rocola, null);
                Check(gv.Visible && (string)TGen.GetProperty("Texto").GetValue(gv, null) == "Rock",
                      "y cuando el genero se aprende de fondo, aparece sola");
            }
        }

        // ================= 2) LOS CREDITOS Y LA COLA, MAS GRANDES =================
        Console.WriteLine();
        MethodInfo mAlto = TF.GetMethod("AltoPastilla", BindingFlags.Static | BindingFlags.NonPublic);
        // El alto de la tarjeta de lo que suena en 1024x768, hecho con las mismas cuentas del acomodo.
        int hTit = Math.Max(24, new Font(fam, 15f, FontStyle.Bold).Height + 1);
        int hArt = Math.Max(18, new Font(fam, 11.5f, FontStyle.Bold).Height + 1);
        int hSig = Math.Max(17, new Font(fam, 10.5f, FontStyle.Bold).Height + 1);
        int tarjetaH = hTit + hArt + hSig + 4 + 12 + 2;
        int altoNuevo = (mAlto != null) ? (int)mAlto.Invoke(null, new object[] { tarjetaH }) : 26;

        int altoAntes, nAntes, altoAhora, nAhora;
        using (Bitmap b = DibujaCapsula(TPas, "Creditos", "3", 26, fam)) Tinta(b, out altoAntes, out nAntes);
        using (Bitmap b = DibujaCapsula(TPas, "Creditos", "3", altoNuevo, fam)) Tinta(b, out altoAhora, out nAhora);
        Console.WriteLine("  la capsula \"Creditos 3\", dibujada de verdad:");
        Console.WriteLine("     antes (26 px de alto):     texto de " + altoAntes + " px, " + nAntes + " pixeles de tinta");
        Console.WriteLine("     ahora (" + altoNuevo + " px de alto):     texto de " + altoAhora + " px, " + nAhora + " pixeles de tinta");
        Check(altoNuevo > 26, "la capsula es mas alta que los 26 px de antes (" + altoNuevo + " px)");
        Check(altoAhora >= altoAntes * 5 / 4, "y su letra crece al menos un cuarto: " + altoAntes + " px -> " + altoAhora + " px");
        Check(altoAhora <= hTit, "sin pasarse del renglon del titulo (" + altoAhora + " px contra " + hTit + " px del titulo)");

        // Y que siga cabiendo: las dos capsulas no pueden comerse el sitio del titulo.
        MethodInfo mAncho = TPas.GetMethod("Ancho", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        int wCred = (int)mAncho.Invoke(null, new object[] { "Inserta una moneda", "", altoNuevo, fam, false });
        // La de la cola SI lleva portaditas, y por eso se mide con ellas: es su ancho de verdad,
        // el que le quita sitio al titulo.
        int wCola = (int)mAncho.Invoke(null, new object[] { "En cola", "12", altoNuevo, fam, true });
        int txtX = 16 + tarjetaH + 14;                      // caratula + hueco
        int paraTitulo = 1024 - 16 - wCred - wCola - 24 - txtX;
        Console.WriteLine("  a 1024 px de ancho: \"Inserta una moneda\" " + wCred + " px + \"En cola 12\" " + wCola
                          + " px  ->  al titulo le quedan " + paraTitulo + " px");
        Check(paraTitulo >= 420, "al titulo le sigue quedando sitio de sobra (" + paraTitulo + " px)");
        Check(altoNuevo <= tarjetaH - 12, "y la capsula no se sale de la tarjeta");

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}
