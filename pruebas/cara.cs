using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

// LA CARA DEL DISCO: QUE EL FONDO SE TIÑA DEL COLOR DE LA PORTADA, Y QUE LA COLA LAS ENSEÑE.
//
// Hoy el fondo entero -cielo, aurora, interior del disco, barra de avance- sale del GENERO y de nada
// mas. Eso esta bien y hay que conservarlo, pero deja fuera lo unico que el cliente esta mirando de
// verdad: la portada. Dos cumbias seguidas pintan exactamente la misma pantalla naranja aunque una
// sea un disco rojo y la otra uno verde.
//
// LA REGLA, Y ES LA QUE HACE QUE ESTO NO PUEDA SALIR MAL:
//
//     LA PORTADA ELIGE EL TONO. LA ROCOLA MANDA EN TODO LO DEMAS.
//
// De la portada se toma SOLO de que color es -rojo, azul, verde-. Lo oscuro y lo vivo que sale a la
// pantalla lo sigue decidiendo la rocola, con el mismo brillo y la misma viveza que ya tenia el color
// del genero. Asi NINGUNA portada puede estropear la pantalla: ni una casi blanca lavandola, ni una
// negra apagandola, ni una de neon quemandola. No hace falta recortar nada ni poner topes de
// seguridad; sale de como esta hecho.
//
// Y la cola: la pastilla dice "En cola 3". Tres portaditas dicen lo mismo Y ADEMAS cuales son, que es
// lo que el cliente quiere saber para no volver a pedir la que ya pidio.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE LAS DOS PIEZAS SEAN PURAS. El color de una portada y la mezcla con el genero se pueden
//      probar con imagenes hechas a mano aqui mismo, sin encender la rocola.
//   2) QUE ACIERTE EL COLOR, y que un marco negro o un borde blanco no la vuelvan gris.
//   3) QUE SEPA CALLARSE. Una portada en blanco y negro no tiene color: entonces manda el genero,
//      como hasta hoy.
//   4) QUE NINGUNA PORTADA PUEDA ESTROPEAR LA PANTALLA (el brillo y la viveza no se tocan).
//   5) QUE NO SE MIDA LA PORTADA UNA Y OTRA VEZ. Recorrer un mapa de bits es caro; se hace una vez
//      por disco y se guarda.
//   6) QUE LA PASTILLA DE LA COLA NO DE SALTOS. Mide siempre para tres portaditas, haya 0, 1 o 3;
//      si no, la cápsula de creditos que va a su derecha brincaria en cada moneda.
class Cara
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TPast;

    static MethodBase Met(Type t, string n)
    {
        if (t == null) return null;
        foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }

    // --- portadas de mentira, hechas aqui ---
    static Bitmap Lisa(Color c, int lado)
    {
        Bitmap b = new Bitmap(lado, lado);
        using (Graphics g = Graphics.FromImage(b)) g.Clear(c);
        return b;
    }
    // Una portada como las de verdad: fondo de color, un marco negro grueso y una franja blanca
    // (el titulo). Ni el marco ni la franja deben decidir el color.
    static Bitmap ConMarco(Color c, int lado, Color marco, Color franja)
    {
        Bitmap b = new Bitmap(lado, lado);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(marco);
            using (SolidBrush s = new SolidBrush(c)) g.FillRectangle(s, lado / 8, lado / 8, lado * 3 / 4, lado * 3 / 4);
            using (SolidBrush s = new SolidBrush(franja)) g.FillRectangle(s, 0, lado * 5 / 8, lado, lado / 8);
        }
        return b;
    }

    static float Tono(Color c)
    {
        float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
        float mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
        float d = mx - mn;
        if (d < 0.0001f) return -1f;
        float h;
        if (mx == r) h = 60f * (((g - b) / d) % 6f);
        else if (mx == g) h = 60f * ((b - r) / d + 2f);
        else h = 60f * ((r - g) / d + 4f);
        while (h < 0f) h += 360f;
        return h;
    }
    static float Viveza(Color c)
    {
        float mx = Math.Max(c.R, Math.Max(c.G, c.B)) / 255f, mn = Math.Min(c.R, Math.Min(c.G, c.B)) / 255f;
        return (mx <= 0.0001f) ? 0f : (mx - mn) / mx;
    }
    static float Brillo(Color c) { return Math.Max(c.R, Math.Max(c.G, c.B)) / 255f; }
    // Distancia entre dos tonos en la rueda (0 y 359 estan pegados).
    static float DifTono(float a, float b)
    {
        float d = Math.Abs(a - b); if (d > 180f) d = 360f - d; return d;
    }

    static MethodInfo mPortada, mTinte;
    static Color Portada(Bitmap b) { return (Color)mPortada.Invoke(null, new object[] { b }); }
    static Color Tinte(Color genero, Color portada) { return (Color)mTinte.Invoke(null, new object[] { genero, portada }); }

    // Pinta la capsula de la cola con n portaditas de un verde que no usa ninguna otra parte de ella, y
    // cuenta cuantos pixeles de ese verde quedan en la imagen.
    static int TintaDePortadas(int n)
    {
        Color marca = Color.FromArgb(0, 255, 0);
        MethodInfo anc = (MethodInfo)Met(TPast, "Ancho");
        FontFamily fam = FontFamily.GenericSansSerif;
        int alto = 40;
        int w = Convert.ToInt32(anc.Invoke(null, new object[] { "En cola", n.ToString(), alto, fam, true }));
        Control c = (Control)Activator.CreateInstance(TPast, true);
        c.Font = new Font(fam, 9f);
        c.SetBounds(0, 0, w, alto);
        Pon("Etiqueta", c, "En cola"); Pon("Valor", c, n.ToString());
        Pon("Acento", c, Color.FromArgb(140, 205, 255));
        Pon("ConPortadas", c, true);
        Image[] ims = new Image[3];
        for (int i = 0; i < n && i < 3; i++) ims[i] = Lisa(marca, 64);
        Pon("Portadas", c, ims);
        int cuantos = 0;
        using (Bitmap b = new Bitmap(w, alto))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.FromArgb(255, 10, 14, 34));
                ((MethodInfo)Met(TPast, "RenderContent")).Invoke(c, new object[] { g });
            }
            for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                {
                    Color q = b.GetPixel(x, y);
                    if (q.G > 200 && q.R < 90 && q.B < 90) cuantos++;
                }
        }
        for (int i = 0; i < ims.Length; i++) if (ims[i] != null) ims[i].Dispose();
        c.Dispose();
        return cuantos;
    }
    static void Pon(string campo, object obj, object val)
    {
        FieldInfo f = TPast.GetField(campo, TODO);
        if (f != null) f.SetValue(obj, val);
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes())
        {
            if (t.Name == "Form1") TF = t;
            if (t.Name == "PastillaView") TPast = t;
        }
        if (TF == null) { Console.WriteLine("  FALLO  no se encontro Form1"); return 1; }

        Console.WriteLine("\n  1) LAS DOS PIEZAS, Y QUE SE PUEDAN PROBAR\n");
        MethodBase p = Met(TF, "ColorDePortada"), tt = Met(TF, "TinteConPortada"), suena = Met(TF, "ColorDeLoQueSuena");
        Check(p != null, "hay una funcion que dice DE QUE COLOR es una portada (ColorDePortada)");
        Check(tt != null, "y otra que la mezcla con el color del genero (TinteConPortada)");
        if (p == null || tt == null) { Console.WriteLine("\n  " + (fallos + 1) + " FALLO(S)"); return fallos + 1; }
        Check(p.IsStatic && tt.IsStatic, "las dos son puras: se les puede dar una imagen a mano y ver que contestan");
        mPortada = (MethodInfo)p; mTinte = (MethodInfo)tt;
        Check(suena != null && Codigo.Llama(suena, "TinteConPortada"),
              "y el color de lo que suena las usa (asi el fondo se entera)");

        Console.WriteLine("\n  2) DE QUE COLOR ES CADA PORTADA\n");
        Color[] pruebas = { Color.FromArgb(200, 40, 40), Color.FromArgb(40, 70, 210), Color.FromArgb(50, 190, 80) };
        string[] nom = { "roja", "azul", "verde" };
        for (int i = 0; i < pruebas.Length; i++)
        {
            using (Bitmap b = Lisa(pruebas[i], 120))
            {
                Color sale = Portada(b);
                float d = DifTono(Tono(sale), Tono(pruebas[i]));
                Console.WriteLine("     portada " + nom[i] + "  ->  tono " + Tono(sale).ToString("0") + " grados (el suyo es " + Tono(pruebas[i]).ToString("0") + ")");
                Check(sale.A != 0 && d < 12f, "una portada " + nom[i] + " sale " + nom[i]);
            }
        }

        Console.WriteLine("\n  3) NI EL MARCO NEGRO NI EL TITULO EN BLANCO DECIDEN\n");
        using (Bitmap b = ConMarco(Color.FromArgb(200, 40, 40), 120, Color.Black, Color.White))
        {
            Color sale = Portada(b);
            Console.WriteLine("     roja con marco negro y franja blanca  ->  tono " + Tono(sale).ToString("0") + " grados");
            Check(sale.A != 0 && DifTono(Tono(sale), Tono(Color.FromArgb(200, 40, 40))) < 20f,
                  "sigue siendo roja: lo gris y lo negro no cuentan");
        }
        using (Bitmap b = ConMarco(Color.FromArgb(40, 70, 210), 120, Color.FromArgb(20, 20, 20), Color.FromArgb(240, 240, 240)))
        {
            Color sale = Portada(b);
            Check(sale.A != 0 && DifTono(Tono(sale), Tono(Color.FromArgb(40, 70, 210))) < 20f, "y una azul con marco, azul");
        }

        Console.WriteLine("\n  4) Y SI NO TIENE COLOR, SE CALLA\n");
        using (Bitmap b = Lisa(Color.FromArgb(128, 128, 128), 120))
            Check(Portada(b).A == 0, "una portada gris no sabe de que color es: manda el genero");
        using (Bitmap b = ConMarco(Color.FromArgb(90, 90, 90), 120, Color.Black, Color.White))
            Check(Portada(b).A == 0, "una portada en blanco y negro, igual");
        Check(Portada(null).A == 0, "y sin portada, tampoco se inventa nada");

        Console.WriteLine("\n  5) NINGUNA PORTADA PUEDE ESTROPEAR LA PANTALLA\n");
        // El genero manda el brillo y la viveza. Se prueba con portadas de las que dan miedo.
        Color gen = Color.FromArgb(225, 120, 30);   // el naranja de lo Regional
        Color[] malas = {
            Color.FromArgb(252, 250, 248),   // casi blanca (una portada lavada)
            Color.FromArgb(6, 4, 10),        // casi negra
            Color.FromArgb(0, 255, 0),       // neon a tope
            Color.FromArgb(120, 105, 95),    // apagada
            Color.FromArgb(255, 0, 255),     // magenta chillon
        };
        string[] nomM = { "casi blanca", "casi negra", "neon", "apagada", "chillona" };
        Console.WriteLine("     genero: brillo " + Brillo(gen).ToString("0.00") + "  viveza " + Viveza(gen).ToString("0.00"));
        for (int i = 0; i < malas.Length; i++)
        {
            Color sale = Tinte(gen, malas[i]);
            Console.WriteLine("     portada " + nomM[i].PadRight(11) + " ->  brillo " + Brillo(sale).ToString("0.00")
                              + "  viveza " + Viveza(sale).ToString("0.00")
                              + "  tono " + Tono(sale).ToString("0"));
            Check(Math.Abs(Brillo(sale) - Brillo(gen)) < 0.02f, "portada " + nomM[i] + ": el brillo lo sigue mandando el genero");
            Check(Math.Abs(Viveza(sale) - Viveza(gen)) < 0.02f, "portada " + nomM[i] + ": y la viveza tambien");
        }
        // Y con una portada que no sabe de que color es, el genero sale INTACTO.
        Check(Tinte(gen, Color.FromArgb(0, 0, 0, 0)).ToArgb() == gen.ToArgb(),
              "sin color de portada, el color del genero sale tal cual");
        // Pero el tono SI cambia cuando la portada si sabe.
        Color azulada = Tinte(gen, Color.FromArgb(255, 40, 70, 210));
        Check(DifTono(Tono(azulada), Tono(gen)) > 30f, "y cuando la portada si sabe, el tono cambia de verdad");

        Console.WriteLine("\n  6) LA PORTADA SE MIDE UNA VEZ, NO EN CADA CANCION\n");
        MethodBase cacheado = Met(TF, "ColorDePortadaDe");
        Check(cacheado != null, "el color de un disco se pide por un solo sitio (ColorDePortadaDe)");
        if (cacheado != null)
        {
            int lee, esc;
            Codigo.UsosDeCampo(cacheado, "colorPortada", out lee, out esc);
            Check(lee > 0, "que lleva apuntado lo que ya midio (colorPortada)");
            Check(Codigo.Llama(cacheado, "TryGetValue"), "y lo mira antes de nada");
            Check(Codigo.Llama(cacheado, "set_Item"), "y apunta lo que acaba de medir");
            Check(suena != null && !Codigo.Llama(suena, "ColorDePortada"),
                  "lo que suena pregunta por el guardado, no recorre la imagen cada vez");
        }

        Console.WriteLine("\n  7) LA PASTILLA DE LA COLA, CON PORTADITAS\n");
        Check(TPast != null, "existe la pastilla");
        if (TPast != null)
        {
            FieldInfo fp = TPast.GetField("Portadas", TODO);
            Check(fp != null, "la pastilla de la cola puede llevar portaditas (Portadas)");
            MethodInfo anc = (MethodInfo)Met(TPast, "Ancho");
            Check(anc != null, "y sigue sabiendo lo que mide de ancho");
            if (anc != null)
            {
                // ESTA ES LA IMPORTANTE, Y SE COMPRUEBA POR COMO ESTA HECHA Y NO PROBANDO CASOS.
                //
                // El ancho de esta capsula manda hasta donde puede llegar el TITULO de la cancion
                // (derPast, en el acomodo). Si cambiara al entrar o salir una cancion de la cola, el
                // titulo se re-repartiria en dos lineas cada vez que alguien pide algo: el nombre de
                // la cancion que suena bailando solo, sin que nadie lo haya tocado.
                //
                // La unica forma de que eso NO pueda pasar es que la cuenta de la cola no llegue
                // hasta aqui: se mide siempre el hueco de las tres portaditas, llenas o vacias. Por
                // eso lo que se comprueba es que a esta funcion NO se le pasa cuantas hay.
                // Se le dice SI esta capsula lleva portaditas -que es cosa de la capsula y no cambia
                // nunca-, pero NO cuantas hay ahora. Un numero ahi seria la puerta por donde el ancho
                // acabaria dependiendo de la cola.
                ParameterInfo[] ps = anc.GetParameters();
                Check(ps.Length == 5 && ps[4].ParameterType == typeof(bool),
                      "se le dice SI lleva portaditas, no CUANTAS: asi el ancho no puede depender de la cola");
                if (ps.Length == 5 && ps[4].ParameterType == typeof(bool))
                {
                    int con = Convert.ToInt32(anc.Invoke(null, new object[] { "En cola", "3", 40, FontFamily.GenericSansSerif, true }));
                    int sin = Convert.ToInt32(anc.Invoke(null, new object[] { "En cola", "3", 40, FontFamily.GenericSansSerif, false }));
                    int con9 = Convert.ToInt32(anc.Invoke(null, new object[] { "En cola", "9", 40, FontFamily.GenericSansSerif, true }));
                    Console.WriteLine("     \"En cola 3\" con portaditas: " + con + " px   sin ellas: " + sin + " px");
                    Check(con > sin, "con portaditas la capsula es mas ancha (les guarda su hueco)");
                    Check(con == con9, "y ese hueco es el mismo tenga 3 o 9 canciones: el titulo no se re-reparte");
                }
            }
            MethodBase pinta = Met(TPast, "RenderContent");
            Check(pinta != null, "la pastilla se dibuja en un solo sitio (RenderContent)");
            if (pinta != null)
            {
                int lee, esc;
                Codigo.UsosDeCampo(pinta, "Portadas", out lee, out esc);
                Check(lee > 0, "que salen de la cola de verdad, no de un adorno fijo");
                // Y SE VEN. Se pinta la capsula de verdad con tres portadas de un color que no sale en
                // ninguna otra parte de ella, y se cuentan los pixeles de ese color. Mirar el codigo dice
                // que llama a DrawImage; contar los pixeles dice que ademas cae donde se ve.
                int n0 = TintaDePortadas(0), n3 = TintaDePortadas(3);
                Console.WriteLine("     pintada sin portaditas: " + n0 + " pixeles suyos    con tres: " + n3);
                Check(n3 > 600, "las tres portaditas se pintan y se ven (" + n3 + " pixeles)");
                Check(n0 == 0, "y sin nada en la cola no se pinta ninguna");
            }
        }

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
