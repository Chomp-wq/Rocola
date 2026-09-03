using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

// LOS COLORES DEL FONDO, CON MANDO.
//
// "Puse la cancion HOME en el buscador, dice que es POP, pero no tiene los colores del genero."
//
// Y era verdad, aunque no fuera un fallo. El fondo sale del GENERO y encima se le pega el tono de la
// CARATULA... y se le pegaba ENTERO. La portada de "The Singles 86-98 Vol. 2" es una foto azul, asi que
// una cancion de Pop -que es rosa fuerte- pintaba la pantalla AZUL y del rosa no quedaba nada.
//
// Ahora eso es una perilla, y de fabrica se queda a MEDIAS: se reconoce el genero y se sigue notando que
// cada disco es distinto. Y con ella van otras dos, que son las que de verdad cambian como se ve la
// pared: cuanto azul lleva el medio del cielo y cuanto negro hay arriba.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE LAS TRES PASEN POR UNA FUNCION QUE ACOTA. En el archivo de configuracion se puede escribir
//      cualquier cosa, y un disparate no puede dejar la pantalla inservible en un bar de madrugada.
//   2) QUE UN POP CON CARATULA AZUL VUELVA A PARECERSE A POP. Medido en grados de la rueda de color, que
//      es donde vive la queja.
//   3) QUE LOS DOS EXTREMOS HAGAN LO QUE DICEN: a cero manda el genero; a tope manda la caratula.
//   4) QUE NINGUNA PORTADA PUEDA ESTROPEAR LA PANTALLA, este donde este la perilla. Es la regla de
//      siempre y aqui hay un sitio nuevo por donde se podria colar.
//   5) QUE EL AZUL Y EL NEGRO MUEVAN LO QUE DICEN, y cuanto, en niveles y en parte de pantalla.
//   6) QUE EL CORTE DEL CIELO LO PIDAN LAS DOS PAREDES AL MISMO SITIO. Con dos numeros, la maquina del
//      bar y la de repuesto pintarian cielos distintos y nadie se enteraria hasta verlas juntas.
//   7) QUE SE GUARDEN Y SE VUELVAN A LEER, o al cerrar la ventana se perderia todo.
class Cielo
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF;
    static MethodInfo mTinte, mEtiqueta, mMid, mSky, mSky2;
    static FieldInfo fManda, fAzul, fCorte;

    static MethodBase Met(string n)
    {
        if (TF == null) return null;
        foreach (MethodInfo m in TF.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }
    static Color Etiqueta(string g) { return (Color)mEtiqueta.Invoke(null, new object[] { g }); }
    static Color Tinte(Color gen, Color port) { return (Color)mTinte.Invoke(null, new object[] { gen, port }); }
    static float Tono(Color c)
    {
        float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
        float mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
        float d = mx - mn;
        if (d < 0.0001f) return 0f;
        float h;
        if (mx == r) h = 60f * (((g - b) / d) % 6f);
        else if (mx == g) h = 60f * ((b - r) / d + 2f);
        else h = 60f * ((r - g) / d + 4f);
        while (h < 0f) h += 360f;
        return h;
    }
    static float DifTono(float a, float b)
    {
        float d = Math.Abs(a - b) % 360f;
        return (d > 180f) ? 360f - d : d;
    }
    static float Brillo(Color c) { return Math.Max(c.R, Math.Max(c.G, c.B)) / 255f; }
    static float Viveza(Color c)
    {
        int mx = Math.Max(c.R, Math.Max(c.G, c.B)), mn = Math.Min(c.R, Math.Min(c.G, c.B));
        return (mx <= 0) ? 0f : (mx - mn) / (float)mx;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);

        Console.WriteLine("\n  1) LAS TRES PERILLAS, Y QUE NINGUNA SE PUEDA PONER A UN DISPARATE\n");
        MethodInfo mPeso = (MethodInfo)Met("PesoPortada"), mAzul = (MethodInfo)Met("AzulCielo"), mCorte = (MethodInfo)Met("CorteCielo");
        Check(mPeso != null, "cuanto manda la caratula se lee por una funcion que acota (PesoPortada)");
        Check(mAzul != null, "cuanto azul, tambien (AzulCielo)");
        Check(mCorte != null, "y cuanto negro (CorteCielo)");
        if (mPeso == null || mAzul == null || mCorte == null) { Console.WriteLine("\n  " + Math.Max(fallos, 1) + " FALLO(S)"); return Math.Max(fallos, 1); }
        fManda = TF.GetField("portadaManda", TODO); fAzul = TF.GetField("cieloAzul", TODO); fCorte = TF.GetField("cieloCorte", TODO);
        Check(fManda != null && fAzul != null && fCorte != null, "y las tres se guardan en un solo sitio cada una");
        mTinte = (MethodInfo)Met("TinteConPortada"); mEtiqueta = (MethodInfo)Met("ColorDeEtiqueta");
        mMid = (MethodInfo)Met("SkyColorMid"); mSky = (MethodInfo)Met("SkyColor"); mSky2 = (MethodInfo)Met("SkyColor2");

        // Disparates: 500, -3, "hola", vacio. Ninguno puede pasar.
        string[] locos = { "500", "-3", "hola", "" };
        bool acota = true;
        foreach (string t in locos)
        {
            float a = Convert.ToSingle(mPeso.Invoke(null, new object[] { t }));
            float b = Convert.ToSingle(mAzul.Invoke(null, new object[] { t }));
            float c = Convert.ToSingle(mCorte.Invoke(null, new object[] { t }));
            if (a < 0f || a > 1f || b < 0f || b > 1f || c < 0.24f || c > 0.67f) acota = false;
        }
        Console.WriteLine("     con 500 en el archivo:  caratula " + Convert.ToSingle(mPeso.Invoke(null, new object[] { "500" })).ToString("0.00")
                          + "   azul " + Convert.ToSingle(mAzul.Invoke(null, new object[] { "500" })).ToString("0.00")
                          + "   corte " + Convert.ToSingle(mCorte.Invoke(null, new object[] { "500" })).ToString("0.00"));
        Check(acota, "un 500, un negativo o una linea sin sentido no rompen nada: se quedan dentro");

        Console.WriteLine("\n  2) UN POP CON CARATULA AZUL VUELVE A PARECERSE A POP\n");
        // El caso de la foto, tal cual: la cancion "Home" es Pop -rosa fuerte- y su disco es una foto azul.
        Color pop = Etiqueta("Pop");
        Color portadaAzul = Color.FromArgb(255, 40, 90, 200);
        object antesManda = fManda.GetValue(null);
        try
        {
            fManda.SetValue(null, 1f);
            Color entera = Tinte(pop, portadaAzul);
            fManda.SetValue(null, 0.5f);
            Color medias = Tinte(pop, portadaAzul);
            fManda.SetValue(null, 0f);
            Color nada = Tinte(pop, portadaAzul);
            Console.WriteLine("     Pop es tono " + Tono(pop).ToString("0") + " grados; su caratula, " + Tono(portadaAzul).ToString("0"));
            Console.WriteLine("     con la caratula al mando entero el fondo sale a " + DifTono(Tono(entera), Tono(pop)).ToString("0") + " grados del Pop");
            Console.WriteLine("     a medias, a " + DifTono(Tono(medias), Tono(pop)).ToString("0") + " grados");
            Console.WriteLine("     y sin caratula, a " + DifTono(Tono(nada), Tono(pop)).ToString("0"));
            Check(DifTono(Tono(medias), Tono(pop)) < DifTono(Tono(entera), Tono(pop)) - 20f,
                  "a medias el fondo se queda MUCHO mas cerca del color del genero");
            Check(DifTono(Tono(medias), Tono(pop)) > 15f,
                  "pero no tanto como para que dos discos distintos pinten lo mismo");

            Console.WriteLine("\n  3) Y LOS DOS EXTREMOS HACEN LO QUE DICEN\n");
            fManda.SetValue(null, 0f);
            Check(DifTono(Tono(Tinte(pop, portadaAzul)), Tono(pop)) < 2f, "a cero manda el genero: la caratula no pinta nada");
            fManda.SetValue(null, 1f);
            Check(DifTono(Tono(Tinte(pop, portadaAzul)), Tono(portadaAzul)) < 2f, "a tope manda la caratula: como estaba antes");

            Console.WriteLine("\n  4) Y NINGUNA PORTADA PUEDE ESTROPEAR LA PANTALLA\n");
            // Este por si alguien mete mano a la mezcla: de la caratula se toma SOLO de que color es.
            Color gen = Etiqueta("Regional");
            Color[] malas = { Color.FromArgb(252, 250, 248), Color.FromArgb(6, 4, 10), Color.FromArgb(0, 255, 0), Color.FromArgb(255, 0, 255) };
            string[] nomM = { "casi blanca", "casi negra", "neon", "chillona" };
            float[] pasos = { 0f, 0.2f, 0.5f, 1f };
            bool salvo = true;
            foreach (float p in pasos)
            {
                fManda.SetValue(null, p);
                for (int i = 0; i < malas.Length; i++)
                {
                    Color sale = Tinte(gen, malas[i]);
                    if (Math.Abs(Brillo(sale) - Brillo(gen)) > 0.02f || Math.Abs(Viveza(sale) - Viveza(gen)) > 0.02f)
                    { salvo = false; Console.WriteLine("     se cuela con la perilla al " + (p * 100).ToString("0") + "% y la portada " + nomM[i]); }
                }
            }
            Console.WriteLine("     probadas 4 portadas de las que dan miedo en 4 posiciones de la perilla");
            Check(salvo, "el brillo y la viveza los sigue mandando el genero, este donde este la perilla");
        }
        finally { fManda.SetValue(null, antesManda); }

        Console.WriteLine("\n  5) EL AZUL Y EL NEGRO MUEVEN LO QUE DICEN\n");
        object antesAzul = fAzul.GetValue(null);
        try
        {
            Color rock = Etiqueta("Rock");
            fAzul.SetValue(null, 1f);
            Color todo = (Color)mMid.Invoke(null, new object[] { rock });
            fAzul.SetValue(null, 0.35f);
            Color poco = (Color)mMid.Invoke(null, new object[] { rock });
            int mueve = Math.Max(Math.Abs(todo.R - poco.R), Math.Max(Math.Abs(todo.G - poco.G), Math.Abs(todo.B - poco.B)));
            Console.WriteLine("     el medio del cielo pasa de (" + todo.R + "," + todo.G + "," + todo.B + ") a ("
                              + poco.R + "," + poco.G + "," + poco.B + "): " + mueve + " niveles de 255");
            Check(mueve >= 40, "bajar el azul mueve el medio del cielo de verdad, no un pelin");
        }
        finally { fAzul.SetValue(null, antesAzul); }

        object antesCorte = fCorte.GetValue(null);
        try
        {
            PropertyInfo pCorte = TF.GetProperty("CorteDelCielo", TODO);
            Check(pCorte != null, "hay UN solo sitio que dice donde acaba el negro (CorteDelCielo)");
            if (pCorte != null)
            {
                fCorte.SetValue(null, 0.58f);
                float mas = Convert.ToSingle(pCorte.GetValue(null, null));
                fCorte.SetValue(null, 0.30f);
                float menos = Convert.ToSingle(pCorte.GetValue(null, null));
                Console.WriteLine("     el corte se mueve entre el " + (menos * 100).ToString("0") + "% y el " + (mas * 100).ToString("0") + "% del alto");
                Check(mas - menos > 0.2f, "y mueve una parte grande de la pantalla, no un dedo");
            }
        }
        finally { fCorte.SetValue(null, antesCorte); }

        Console.WriteLine("\n  6) LAS DOS PAREDES PIDEN EL CORTE AL MISMO SITIO\n");
        // Con dos numeros, la maquina del bar y la de repuesto pintarian cielos distintos y nadie se
        // enteraria hasta verlas juntas. Aqui se cuenta cuantos sitios lo dicen: tiene que ser uno.
        int cuantos = 0;
        foreach (Type t in asm.GetTypes())
        {
            foreach (FieldInfo f in t.GetFields(TODO | BindingFlags.DeclaredOnly))
                if (f.Name == "cieloCorte") cuantos++;
            foreach (Type x in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                foreach (FieldInfo f in x.GetFields(TODO | BindingFlags.DeclaredOnly))
                    if (f.Name == "cieloCorte") cuantos++;
        }
        Console.WriteLine("     donde acaba el negro esta escrito en " + cuantos + " sitio(s)");
        Check(cuantos == 1, "un solo numero, y las dos paredes lo piden ahi");
        // Y el horneado sin tarjeta grafica tiene que TIRAR su cielo guardado al tocarlo: el corte no es un
        // color, no entra en la firma, y sin esto el cielo se quedaria como estaba.
        MethodBase repinta = Met("RepintaFondo");
        Check(repinta != null, "al tocar una perilla la pared se rehace (RepintaFondo)");
        Check(repinta != null && Codigo.Llama(repinta, "TiraCieloCache"),
              "y tira el cielo guardado: el corte no es un color y no entra en su firma");

        Console.WriteLine("\n  7) Y SE GUARDAN\n");
        MethodBase lee = Met("LoadConfigPrefs"), escribe = Met("SaveConfigPrefs");
        string[] claves = { "portada_manda=", "cielo_azul=", "cielo_negro=" };
        for (int i = 0; i < claves.Length; i++)
        {
            bool enLee = false, enEsc = false;
            if (lee != null) foreach (string t in Codigo.Textos(lee)) if (t == claves[i]) enLee = true;
            if (escribe != null) foreach (string t in Codigo.Textos(escribe)) if (t == claves[i]) enEsc = true;
            Check(enLee && enEsc, "la rocola se acuerda de \"" + claves[i] + "\" al apagarla y encenderla");
        }

        Console.WriteLine("\n  8) Y HAY DONDE TOCARLAS\n");
        // Una perilla que solo se puede mover escribiendo en un archivo de texto es, para quien no toca
        // archivos de texto, lo mismo que no tener perilla.
        MethodBase pag = Met("PaginaColores");
        Check(pag != null, "los colores del fondo tienen su propia pagina (PaginaColores)");
        bool laMonta = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            if (m != pag && Codigo.Llama(m, "PaginaColores")) laMonta = true;
        Check(laMonta, "y la ventana de Ctrl+F3 la monta: hay una cuarta pestaña");
        List<MethodBase> deLaPagina = Codigo.MetodosConAyudantes(TF, "PaginaColores");
        string[] campo = { "portadaManda", "cieloAzul", "cieloCorte" };
        string[] porDonde = { "PesoPortada", "AzulCielo", "CorteCielo" };
        for (int i = 0; i < campo.Length; i++)
        {
            Check(Codigo.EscribeCampo(deLaPagina, campo[i]), "la pagina puede mover " + campo[i]);
            Check(Codigo.Llama(Codigo.ConAyudantes(TF, "PaginaColores"), porDonde[i]),
                  "   ...y lo hace por la funcion que acota (" + porDonde[i] + "), no poniendo el numero a mano");
        }
        Check(Codigo.EscribeCampo(deLaPagina, "f3CambioGraf"), "y lo que se toca se guarda al cerrar");
        Check(Codigo.Llama(Codigo.ConAyudantes(TF, "PaginaColores"), "RepintaFondo"),
              "y la pared cambia AL INSTANTE: estos se eligen mirando, no a ciegas");

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
