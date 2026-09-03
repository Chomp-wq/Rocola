using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

// DOS NEBULOSAS QUE SE MUEVEN: EL GENERO POR UN LADO, LA CARATULA POR OTRO.
//
// La rocola tiene 33 colores, uno por genero, y como colores planos estan bien separados: el par mas
// parecido -Latino y World- mide 53 en la distancia que se parece a lo que ve el ojo. El problema no era
// que faltaran colores. Era que al cielo llegaban aplastados: de los 33 se distinguen 4 arriba, 10 en el
// medio y 28 abajo, y hay 6 parejas de generos que no se distinguen en NINGUN renglon.
//
// La unica cosa grande de la pantalla donde los 33 llegan enteros es la capa de nebulosas. Y estaba
// quieta, y de un solo color.
//
// AHORA SON DOS, y se dibujan una encima de otra yendo a la deriva en sentidos contrarios: una lleva el
// color del GENERO y la otra el de la CARATULA. Donde se cruzan sale la mezcla, y como las dos se mueven,
// la mezcla se mueve.
//
// Y LA CARATULA PASA A DAR DOS COLORES. Antes se resumia en uno promediando los tonos en circulo, y
// cuando una portada traia dos colores fuertes -media roja y media verde- el promedio no se parecia a
// ninguno de los dos: la rocola se callaba y mandaba el genero solo. O sea que las portadas mas vistosas
// eran justo las que no pintaban nada.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE LOS DOS COLORES SALGAN DE UN SOLO SITIO, y que los dos caminos de dibujo tiren de el.
//   2) QUE LOS 33 GENEROS SE SIGAN DISTINGUIENDO con la mezcla puesta. Si al mezclar se juntaran dos,
//      habriamos cambiado un fondo aburrido por uno que miente.
//   3) QUE DENTRO DE CADA GENERO LOS DOS COLORES SE DISTINGAN. Si salieran iguales no habria mezcla que
//      ver y todo esto no serviria de nada. Incluidos los dos generos casi grises.
//   4) QUE NINGUNA PORTADA PUEDA ESTROPEAR LA PANTALLA. Es la regla de siempre y aqui hay mas sitios por
//      donde se podria colar: de la caratula se toma SOLO de que color es.
//   5) QUE UNA PORTADA DE DOS COLORES DE DOS COLORES, y que una en blanco y negro siga callandose.
//   6) QUE EL CRUCE SEA LENTO. Lo que marea es la velocidad.
//   7) QUE NO CUESTE MAS DE LO DICHO en la maquina sin tarjeta grafica, donde el cielo va horneado.
//   8) QUE EL CIELO NO HAYA CAMBIADO. Se pidio expresamente que se quedara como estaba.
class Nebulosa
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF;
    static MethodInfo mColores, mEtiqueta, mTinte, mPortada, mColoresPortada, mNeb;

    static MethodBase Met(string n)
    {
        if (TF == null) return null;
        foreach (MethodInfo m in TF.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }
    static float Cte(string n)
    {
        FieldInfo f = TF.GetField(n, TODO);
        return (f == null) ? float.NaN : Convert.ToSingle(f.GetValue(null));
    }

    // --- las dos nebulosas de un genero, en un punto del ciclo ---
    static void Dos(Color genero, Color p1, Color p2, float fase, out Color a, out Color b)
    {
        object[] arg = { genero, p1, p2, fase, null, null };
        mColores.Invoke(null, arg);
        a = (Color)arg[4]; b = (Color)arg[5];
    }
    static Color Etiqueta(string g) { return (Color)mEtiqueta.Invoke(null, new object[] { g }); }
    static readonly Color SinColor = Color.FromArgb(0, 0, 0, 0);

    // Distancia entre dos colores como la ve el ojo ("redmean"): la de RGB a secas dice que un azul y un
    // verde oscuros son distintisimos y que dos rojos claros son iguales, y no es asi.
    static double Dif(Color c1, Color c2)
    {
        double rm = (c1.R + c2.R) / 2.0;
        double dr = c1.R - c2.R, dg = c1.G - c2.G, db = c1.B - c2.B;
        return Math.Sqrt((2 + rm / 256) * dr * dr + 4 * dg * dg + (2 + (255 - rm) / 256) * db * db);
    }
    static float Brillo(Color c) { return Math.Max(c.R, Math.Max(c.G, c.B)) / 255f; }
    // Lo que se ve donde las dos nebulosas se enciman: la mezcla de las dos.
    static Color Media(Color a, Color b) { return Color.FromArgb(255, (a.R + b.R) / 2, (a.G + b.G) / 2, (a.B + b.B) / 2); }
    // Deshace la mezcla con el violeta azulado que hace ColorNebulosa: (c + violeta) / 2, o sea que el de
    // antes es 2c - violeta. Sirve para mirar el brillo y la viveza donde de verdad viven.
    static Color Desmezcla(Color c)
    {
        int r = 2 * c.R - 140, g = 2 * c.G - 120, b = 2 * c.B - 255;
        if (r < 0) r = 0; if (g < 0) g = 0; if (b < 0) b = 0;
        if (r > 255) r = 255; if (g > 255) g = 255; if (b > 255) b = 255;
        return Color.FromArgb(255, r, g, b);
    }
    static float Viveza(Color c)
    {
        int mx = Math.Max(c.R, Math.Max(c.G, c.B)), mn = Math.Min(c.R, Math.Min(c.G, c.B));
        return (mx <= 0) ? 0f : (mx - mn) / (float)mx;
    }

    // --- portadas de mentira, hechas aqui ---
    static Bitmap Lisa(Color c, int lado)
    {
        Bitmap b = new Bitmap(lado, lado);
        using (Graphics g = Graphics.FromImage(b)) g.Clear(c);
        return b;
    }
    static Bitmap Mitades(Color a, Color b, int lado)
    {
        Bitmap bm = new Bitmap(lado, lado);
        using (Graphics g = Graphics.FromImage(bm))
        { g.Clear(a); g.FillRectangle(new SolidBrush(b), 0, lado / 2, lado, lado - lado / 2); }
        return bm;
    }
    static Bitmap ByN(int lado)
    {
        Bitmap bm = new Bitmap(lado, lado);
        using (Graphics g = Graphics.FromImage(bm))
        { g.Clear(Color.FromArgb(30, 30, 30)); g.FillRectangle(Brushes.White, 0, lado / 3, lado, lado / 3); }
        return bm;
    }
    static void Picos(Bitmap b, out Color uno, out Color dos)
    {
        object[] arg = { b, null, null };
        mColoresPortada.Invoke(null, arg);
        uno = (Color)arg[1]; dos = (Color)arg[2];
    }

    static readonly string[] Generos = {
        "Karaoke","Navidad","K-Pop","Metal","Punk","Gotico","Industrial","Reggaeton","Hip-Hop","Tropical",
        "Regional","Bolero","Flamenco","Latino","Reggae","DnB","Electronica","Chill","Jazz","Soul",
        "Cristiana","Clasica","Soundtrack","Country","Folk","Disco","Oldies","World","Vocal","Hablado",
        "Infantil","Pop","Rock",
    };

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes()) if (t.Name == "Form1") TF = t;
        if (TF == null) { Console.WriteLine("  FALLO  no se encontro Form1"); return 1; }

        Console.WriteLine("\n  1) UN SOLO SITIO QUE ARMA LOS DOS COLORES\n");
        MethodBase col = Met("ColoresNebulosa"), neb = Met("ColorNebulosa"), cp = Met("ColoresDePortada");
        Check(col != null, "hay una sola funcion que dice los dos colores de las nebulosas (ColoresNebulosa)");
        Check(neb != null, "que se apoya en la de siempre para llevarlos a color de cielo (ColorNebulosa)");
        Check(cp != null, "y una que saca los colores -en plural- de una portada (ColoresDePortada)");
        if (col == null || neb == null || cp == null) { Console.WriteLine("\n  " + Math.Max(fallos, 1) + " FALLO(S)"); return Math.Max(fallos, 1); }
        Check(col.IsStatic, "es pura: se le dan los colores y contesta, sin encender la rocola");
        mColores = (MethodInfo)col; mNeb = (MethodInfo)neb; mColoresPortada = (MethodInfo)cp;
        mEtiqueta = (MethodInfo)Met("ColorDeEtiqueta");
        mTinte = (MethodInfo)Met("TinteConPortada");
        mPortada = (MethodInfo)Met("ColorDePortada");
        Check(mEtiqueta != null && mTinte != null && mPortada != null, "y las piezas de antes siguen donde estaban");
        // Y QUE LOS DOS CAMINOS DE DIBUJO TIREN DE ELLA. Si uno se quedara con la cuenta vieja, la maquina
        // del bar y la de repuesto pintarian fondos distintos y nadie se enteraria hasta verlas juntas.
        Check(Codigo.Llama(Codigo.ConAyudantes(TF, "AnimTimer_Tick"), "ColoresNebulosa"),
              "el reloj de la pantalla la usa (de ahi salen los dos caminos)");
        // El horneado sin tarjeta grafica ya no lleva la cuenta copiada a mano: llama a la de todos.
        MethodBase horno = null;
        foreach (Type t in TF.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly))
                if (m.Name == "EnsureSkyCache") horno = m;
        Check(horno != null, "sigue habiendo un horneado del cielo (EnsureSkyCache)");
        Check(horno != null && Codigo.Llama(horno, "PintaNebulosa"),
              "y pinta las nebulosas por un solo sitio, no con la cuenta copiada a mano");

        Console.WriteLine("\n  2) LOS 33 GENEROS SE SIGUEN DISTINGUIENDO\n");
        // Las dos nebulosas estan en pantalla A LA VEZ, asi que lo que hay que comparar es lo que se ve:
        // donde se encima una con otra, la mezcla de las dos. Comparar una sola capa diria que dos generos
        // se parecen cuando en pantalla se distinguen perfectamente por la otra.
        //
        // Y se mira SIN portada, que es el caso pelado: con portada cada disco separa todavia mas.
        double peor = 1e9; string pa = "", pb = "";
        for (int i = 0; i < Generos.Length; i++)
            for (int j = i + 1; j < Generos.Length; j++)
            {
                Color a1, b1, a2, b2;
                Dos(Etiqueta(Generos[i]), SinColor, SinColor, 0f, out a1, out b1);
                Dos(Etiqueta(Generos[j]), SinColor, SinColor, 0f, out a2, out b2);
                double d = Dif(Media(a1, b1), Media(a2, b2));
                if (d < peor) { peor = d; pa = Generos[i]; pb = Generos[j]; }
            }
        Console.WriteLine("     el par mas parecido es " + pa + " y " + pb + ": " + peor.ToString("0"));
        Console.WriteLine("     (antes, con una sola nebulosa, el mas parecido era Latino y World: 26)");
        Check(peor >= 20, "ninguna pareja de generos se confunde con la mezcla puesta");

        Console.WriteLine("\n  3) Y DENTRO DE CADA GENERO, LOS DOS SE DISTINGUEN\n");
        // Si las dos nebulosas de un genero salieran del mismo color no habria mezcla que ver: dos capas
        // iguales encimadas son una capa. Aqui esta la razon de ser de todo esto.
        double flojo = 1e9; string cual = "";
        foreach (string g in Generos)
        {
            Color a, b;
            Dos(Etiqueta(g), SinColor, SinColor, 0f, out a, out b);
            double d = Dif(a, b);
            if (d < flojo) { flojo = d; cual = g; }
        }
        Console.WriteLine("     el que menos se mueve es " + cual + ": " + flojo.ToString("0"));
        Check(flojo >= 12, "los dos colores de cada genero se distinguen: hay mezcla que ver");
        // LOS DOS CASI GRISES, aparte y con nombre. A un gris girarle el tono no le hace NADA (medido:
        // Hablado se movia dos niveles de 255), asi que a esos se les mueve el brillo y la temperatura.
        foreach (string g in new string[] { "Hablado", "Vocal" })
        {
            Color a, b;
            Dos(Etiqueta(g), SinColor, SinColor, 0f, out a, out b);
            Console.WriteLine("     " + g.PadRight(8) + " (viveza " + Viveza(Etiqueta(g)).ToString("0.00") + ")  ->  "
                              + a.R + "," + a.G + "," + a.B + "   y   " + b.R + "," + b.G + "," + b.B
                              + "   (" + Dif(a, b).ToString("0") + ")");
            Check(Dif(a, b) >= 12, "   ...y " + g + ", que es casi gris, tambien");
        }

        Console.WriteLine("\n  4) NINGUNA PORTADA PUEDE ESTROPEAR LA PANTALLA\n");
        // La regla de siempre, ahora con mas sitios por donde se podria colar: de la caratula se toma SOLO
        // de que color es. El brillo y la viveza los sigue mandando el genero.
        Color gen = Etiqueta("Regional");
        Color[] malas = {
            Color.FromArgb(252, 250, 248),   // casi blanca
            Color.FromArgb(6, 4, 10),        // casi negra
            Color.FromArgb(0, 255, 0),       // neon a tope
            Color.FromArgb(255, 0, 255),     // magenta chillon
        };
        string[] nomM = { "casi blanca", "casi negra", "neon", "chillona" };
        Color refA, refB;
        Dos(gen, SinColor, SinColor, 0f, out refA, out refB);
        Console.WriteLine("     el genero:  brillo " + Brillo(gen).ToString("0.00") + "  viveza " + Viveza(gen).ToString("0.00"));
        for (int i = 0; i < malas.Length; i++)
        {
            Color a, b;
            Dos(gen, malas[i], SinColor, 0f, out a, out b);
            // El brillo hay que mirarlo ANTES de llevar el color a cielo: ColorNebulosa lo lleva a medias
            // con un violeta azulado fijo, y despues de eso el brillo depende del TONO -un azul acaba mas
            // claro que un rojo- y ya no dice nada de la portada. Como esa mezcla es una cuenta que se puede
            // deshacer, se deshace y se mira el color de antes, que es donde vive la regla.
            Color antes = Desmezcla(b);
            Console.WriteLine("     portada " + nomM[i].PadRight(11) + " ->  la del genero " + a.R + "," + a.G + "," + a.B
                              + "   la de la portada, antes de llevarla a cielo: brillo " + Brillo(antes).ToString("0.00")
                              + "  viveza " + Viveza(antes).ToString("0.00"));
            Check(a.ToArgb() == refA.ToArgb(), "portada " + nomM[i] + ": la nebulosa del GENERO no se entera de la portada");
            Check(Math.Abs(Brillo(antes) - Brillo(gen)) < 0.03f, "portada " + nomM[i] + ": el brillo lo sigue mandando el genero");
            Check(Math.Abs(Viveza(antes) - Viveza(gen)) < 0.03f, "portada " + nomM[i] + ": y la viveza tambien");
        }

        Console.WriteLine("\n  5) UNA PORTADA DE DOS COLORES DA DOS COLORES\n");
        // Este es el caso que ANTES no pintaba nada: el promedio de un rojo y un verde no se parece a
        // ninguno de los dos, asi que la rocola se callaba y mandaba el genero solo.
        // Rojo y cian: OPUESTOS en la rueda. Ese es el caso que el promedio no sabe resolver -apuntaria a
        // un sitio que no se parece a ninguno de los dos- y por eso se calla. Con un rojo y un verde no
        // valdria: estan a 132 grados y el promedio si se pone de acuerdo (0.40, por encima del 0.35).
        using (Bitmap b = Mitades(Color.FromArgb(210, 40, 40), Color.FromArgb(40, 200, 210), 120))
        {
            Color uno, dos;
            Picos(b, out uno, out dos);
            Color prom = (Color)mPortada.Invoke(null, new object[] { b });
            Console.WriteLine("     media roja y media cian  ->  " + (uno.A != 0 ? "dos picos" : "ninguno")
                              + ((dos.A != 0) ? " (" + uno.R + "," + uno.G + "," + uno.B + " y " + dos.R + "," + dos.G + "," + dos.B + ")" : "")
                              + "   |  el promedio de siempre: " + (prom.A == 0 ? "se calla" : "dice algo"));
            Check(uno.A != 0 && dos.A != 0, "una portada de dos colores da SUS DOS colores");
            Check(dos.A != 0 && Dif(uno, dos) >= 40, "y son de verdad dos, no el mismo dos veces");
            // Y el promedio, que es el que tine el CIELO, sigue callandose como siempre: el cielo no cambia.
            Check(prom.A == 0, "y el color que tine el cielo sigue callandose ahi, como estaba");
        }
        using (Bitmap b = Lisa(Color.FromArgb(200, 40, 40), 120))
        {
            Color uno, dos;
            Picos(b, out uno, out dos);
            Check(uno.A != 0, "una portada de un solo color da uno");
            Check(dos.A == 0, "y no se inventa un segundo");
        }
        using (Bitmap b = ByN(120))
        {
            Color uno, dos;
            Picos(b, out uno, out dos);
            Check(uno.A == 0 && dos.A == 0, "y una en blanco y negro sigue sin decir nada");
        }
        // SIN PORTADA QUE APORTE -o con el interruptor de Ctrl+F3 apagado- tiene que seguir habiendo mezcla:
        // si no, apagar ese interruptor dejaria el fondo de un solo color y parecería que se rompio algo.
        Color s1, s2;
        Dos(Etiqueta("Rock"), SinColor, SinColor, 0f, out s1, out s2);
        Check(Dif(s1, s2) >= 12, "sin portada que aporte, el genero se acompana solo: sigue habiendo mezcla");

        Console.WriteLine("\n  6) EL CRUCE ES LENTO\n");
        float paso = Cte("NebPaso");
        double segs = (6.2831853 / paso) / 24.0;   // el reloj de la pantalla va a 24 cuadros/s
        Console.WriteLine("     una vuelta entera dura " + segs.ToString("0") + " segundos");
        Check(segs > 25, "el cruce tarda mas de medio minuto: es una marea, no un parpadeo");

        Console.WriteLine("\n  7) Y SIN TARJETA GRAFICA NO HACE TRABAJAR MAS A LA MAQUINA\n");
        // Ahi el cielo entero -degradado, via lactea y nebulosas- va horneado a un mapa de bits que se
        // guarda, porque rehacerlo cuesta de 6 a 27 ms segun la pantalla, y se rehornea cuando cambia alguno
        // de los colores de su firma.
        //
        // Se PROBO pasarle los dos colores en movimiento, y esta medido lo que costaba: los rehorneados
        // pasaban de 73 a 151 en medio minuto, o sea 70 ms de cada mil a 2560x1440. El doble de trabajo en
        // la maquina que se eligio justamente por no tener tarjeta grafica. Asi que alli van QUIETOS.
        //
        // Lo que se vigila aqui es eso: que ninguno de los dos colores que recibe esa pared dependa de la
        // fase. Si alguien los enchufara al movimiento "porque se ve mejor", esto lo canta.
        MethodBase pinta = null;
        foreach (MethodInfo m in TF.GetMethods(TODO | BindingFlags.DeclaredOnly))
            if (Codigo.Llama(m, "DrawCached")) pinta = m;
        Check(pinta != null, "hay un sitio que le entrega el cielo al campo de estrellas");
        if (pinta != null)
        {
            Check(!Codigo.Llama(pinta, "PasoDeColor"), "no hace falta redondear nada: los colores de ahi no se mueven");
            int lee, esc2;
            Codigo.UsosDeCampo(pinta, "nebFase", out lee, out esc2);
            Check(lee == 0, "el cielo horneado NO mira la fase del movimiento (moverlo seria rehornearlo)");
            Codigo.UsosDeCampo(pinta, "nebB", out lee, out esc2);
            Check(lee == 0, "ni el color que va cruzando entre los dos de la caratula");
        }
        // Y que los dos que SI recibe sean de verdad dos colores distintos: si no, esa maquina se quedaria
        // como estaba y todo esto seria solo para la del bar.
        Color qa, qb;
        Dos(Etiqueta("Regional"), Color.FromArgb(40, 200, 210), SinColor, 0f, out qa, out qb);
        Console.WriteLine("     ahi se pintan dos nebulosas quietas: " + qa.R + "," + qa.G + "," + qa.B
                          + "   y   " + qb.R + "," + qb.G + "," + qb.B + "   (" + Dif(qa, qb).ToString("0") + " de diferencia)");
        Check(Dif(qa, qb) >= 12, "y aun asi son DOS colores: antes ahi habia uno solo");

        Console.WriteLine("\n  8) LA FUERZA, LA MISMA EN LAS DOS PAREDES\n");
        // El dibujo de las nebulosas son manchas de alfa 4 y 7 de 255: un velo suavisimo con el que los dos
        // colores estaban pero no se veian. Se dibujaron cuatro fuerzas y se eligio mirando.
        //
        // Lo que se vigila es que el numero sea UNO. La pared por tarjeta grafica y la horneada hornean su
        // propio dibujo cada una; si cada una llevara su fuerza, el dia que alguien tocara una, la maquina
        // del bar y la de repuesto pintarian fondos distintos y nadie se enteraria hasta verlas juntas.
        float fuerza = Cte("NebFuerza");
        Console.WriteLine("     las nebulosas se pintan a " + fuerza.ToString("0.0") + " veces la fuerza de antes");
        Check(fuerza >= 2f && fuerza <= 3f, "a la fuerza que se eligio mirando (cuatro veces la de antes)");
        int cuantos = 0;
        foreach (Type t in asm.GetTypes())
        {
            foreach (FieldInfo f in t.GetFields(TODO | BindingFlags.DeclaredOnly))
                if (f.Name == "NebFuerza") cuantos++;
            foreach (Type x in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                foreach (FieldInfo f in x.GetFields(TODO | BindingFlags.DeclaredOnly))
                    if (f.Name == "NebFuerza") cuantos++;
        }
        Check(cuantos == 1, "y ese numero esta escrito en UN solo sitio, no uno por pared");
        // Y las dos paredes tienen que preguntarlo: si una horneara su dibujo a pelo, se saldria del acuerdo.
        int conAlfa = 0;
        foreach (Type t in asm.GetTypes())
        {
            foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly))
                if (m.Name == "AlfaNeb") conAlfa++;
            foreach (Type x in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                foreach (MethodInfo m in x.GetMethods(TODO | BindingFlags.DeclaredOnly))
                    if (m.Name == "AlfaNeb") conAlfa++;
        }
        Console.WriteLine("     paredes que hornean su dibujo a esa fuerza: " + conAlfa);
        Check(conAlfa >= 1, "y las paredes hornean su dibujo pasando por ahi, no a pelo");

        Console.WriteLine("\n  9) Y EL CIELO NO HA CAMBIADO\n");
        // Se pidio expresamente que el cielo se quedara como estaba: el azul del medio es una decision, no
        // un descuido. Las tres paradas tienen que salir EXACTAMENTE como salian.
        MethodInfo arriba = (MethodInfo)Met("SkyColor"), medio = (MethodInfo)Met("SkyColorMid"), abajo = (MethodInfo)Met("SkyColor2");
        Check(arriba != null && medio != null && abajo != null, "las tres paradas del cielo siguen ahi");
        if (arriba != null && medio != null && abajo != null)
        {
            // Los numeros estan escritos a mano a proposito: si alguien toca el cielo, esto lo canta.
            Color g2 = Etiqueta("Rock");
            Color ar = (Color)arriba.Invoke(null, new object[] { g2 });
            Color me = (Color)medio.Invoke(null, new object[] { g2 });
            Color ab = (Color)abajo.Invoke(null, new object[] { g2 });
            Console.WriteLine("     Rock: arriba (" + ar.R + "," + ar.G + "," + ar.B + ")  medio (" + me.R + "," + me.G + "," + me.B
                              + ")  abajo (" + ab.R + "," + ab.G + "," + ab.B + ")");
            Check(ar.R == 13 && ar.G == 8 && ar.B == 12, "arriba del cielo, igual que antes");
            Check(me.R == 33 && me.G == 45 && me.B == 97, "el azul del medio, igual que antes");
            Check(ab.R == 102 && ab.G == 54 && ab.B == 66, "y el horizonte, igual que antes");
        }

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
