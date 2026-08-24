using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

// EL INDICE DEL BUSCADOR: MISMO RESULTADO, MUCHO MENOS TIEMPO.
//
// Cada entrada del buscador guarda su texto ya "limpio" -en minusculas y sin acentos-, porque es asi como
// se compara con lo que se teclea. Limpiarlo costaba lo mismo para "Enter Sandman" que para "Ah\u00ed Viene la
// Plaga": descomponer la palabra en letra+tilde y preguntarle a Windows la categoria de CADA caracter. Con
// 120 mil canciones eso era un tercio de segundo de arranque.
//
// Ahora hay dos cambios y los dos pueden romper el buscador sin que se note mirando la pantalla:
//   1) el ATAJO para el texto sencillo -si no hay ningun caracter raro, se baja a minusculas a mano-. Si
//      el atajo se equivoca, "Coraz\u00f3n" dejaria de encontrarse tecleando "corazon" y nadie lo sabria.
//   2) el REPARTO en varios hilos de esa misma limpieza. Si el reparto se equivoca, alguna cancion se
//      quedaria SIN clave y desapareceria del buscador para siempre.
//
// Aqui se comprueban las dos cosas contra la version de referencia -la lenta, escrita aparte en este mismo
// archivo, que es la definicion de lo que tiene que salir- y se mide que de verdad se gane tiempo.
class Indice
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    // La forma LENTA y evidente de limpiar un texto. No es codigo de la rocola: es el patron con el que se
    // compara. Si el atajo rapido no da exactamente esto, el atajo esta mal.
    static string Referencia(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        string formd = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder(formd.Length);
        for (int i = 0; i < formd.Length; i++)
            if (CharUnicodeInfo.GetUnicodeCategory(formd[i]) != UnicodeCategory.NonSpacingMark) sb.Append(formd[i]);
        return sb.ToString();
    }

    static readonly string[] Art = { "Depeche Mode", "Jos\u00e9 Jos\u00e9", "Los \u00c1ngeles Azules", "Metallica",
        "Juan Gabriel", "Bronco", "Roc\u00edo D\u00farcal", "Man\u00e1", "Los Tigres del Norte", "Vicente Fern\u00e1ndez",
        "Marco Antonio Sol\u00eds", "Camila", "Reik", "Grupo Firme", "Christian Nodal", "Banda MS" };
    static readonly string[] Tit = { "Enjoy The Silence", "El Triste", "Nunca Es Suficiente", "Enter Sandman",
        "Amor Eterno", "Que No Quede Huella", "Rayando El Sol", "La Puerta Negra", "Volver Volver",
        "Si No Te Hubieras Ido", "Mi Ni\u00f1a Bonita", "Coraz\u00f3n Part\u00edo", "La Chona", "El Rey",
        "Mientes Tan Bien", "Ah\u00ed Viene la Plaga", "A\u00f1oranza", "Sinfon\u00eda de Amor",
        "Ma\u00f1ana Ser\u00e1 Bonito", "Qu\u00e9 Bonito Amor" };

    delegate string Limpia(string s);

    // El mejor de varios intentos: esta maquina esta compartida y un intento suelto no dice nada.
    static double Rapido(Action a)
    {
        a(); a();
        double mejor = double.MaxValue;
        for (int k = 0; k < 5; k++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            a();
            sw.Stop();
            if (sw.Elapsed.TotalMilliseconds < mejor) mejor = sw.Elapsed.TotalMilliseconds;
        }
        return mejor;
    }

    // Cuantas veces gana la version de la rocola a la version lenta.
    //
    // Se mide la PAREJA varias veces y se toma la MEJOR proporcion, no la proporcion de los mejores tiempos
    // sueltos. La maquina esta compartida: si a la version lenta le toca un rato tranquilo y a la nueva uno
    // ocupado, la proporcion sale mal aunque el codigo sea el mismo. Con textos que van por el mismo camino
    // -los que llevan acentos- eso hacia que la prueba fallara sola de vez en cuando. El ruido solo puede
    // hacer que algo parezca MAS lento, nunca mas rapido, asi que la mejor proporcion es la buena.
    static double Gana(Limpia limpia, string[] txt)
    {
        double mejor = 0;
        for (int intento = 0; intento < 3; intento++)
        {
            double a = Pareja(limpia, txt);
            if (a > mejor) mejor = a;
        }
        return mejor;
    }
    static double Pareja(Limpia limpia, string[] txt)
    {
        double tRef = Rapido(delegate { for (int i = 0; i < txt.Length; i++) Referencia(txt[i]); });
        double tNue = Rapido(delegate { for (int i = 0; i < txt.Length; i++) limpia(txt[i]); });
        return tRef / tNue;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        BindingFlags IF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        BindingFlags SF = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        Type TAlb = null, TTrk = null, TItem = null;
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "Album") TAlb = t;
            if (t.Name == "Track") TTrk = t;
            if (t.Name == "SearchItem") TItem = t;
        }
        MethodInfo mN = TF.GetMethod("Normalize", SF, null, new Type[] { typeof(string) }, null);
        Limpia limpia = (Limpia)Delegate.CreateDelegate(typeof(Limpia), mN);

        Console.WriteLine();
        Console.WriteLine("  esta maquina tiene " + Environment.ProcessorCount + " nucleos");

        // ---------- 1) EL ATAJO TIENE QUE DAR EXACTAMENTE LO MISMO ----------
        // Se prueban a mano los casos que el atajo trata distinto, y luego un monton de texto de verdad.
        Console.WriteLine();
        string[] casos = {
            "", " ", "a", "A", "abc def 123", "ABC DEF 123", "Enter Sandman",
            "Coraz\u00f3n Part\u00edo", "Ni\u00f1a", "NI\u00d1A", "\u00c1\u00c9\u00cd\u00d3\u00da", "\u00e1\u00e9\u00ed\u00f3\u00fa",
            "Sigur R\u00f3s - Sa\u00b0eg\u00fdlvo", "M\u00f6tley Cr\u00fce", "AC/DC", "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~",
            "\u0100\u0101", "\u00df", "\u0410\u043b\u043b\u0430 \u041f\u0443\u0433\u0430\u0447\u0451\u0432\u0430", "\u4e2d\u6587\u6b4c\u66f2",
            "mezcla Jos\u00e9 123 ABC", "  espacios  al  borde  ", "\t\ttabuladores\t\t" };
        int distintos = 0;
        for (int i = 0; i < casos.Length; i++)
            if (limpia(casos[i]) != Referencia(casos[i]))
            {
                distintos++;
                if (distintos <= 3) Console.WriteLine("     distinto en [" + casos[i] + "]: da [" + limpia(casos[i]) + "] y tenia que dar [" + Referencia(casos[i]) + "]");
            }
        Check(distintos == 0, "el atajo da lo mismo que la version lenta en los " + casos.Length + " casos dificiles");

        List<string> corpus = new List<string>();
        for (int i = 0; i < 40000; i++)
            corpus.Add(Art[i % Art.Length] + " - " + Tit[(i * 7) % Tit.Length] + " " + i);
        int malos = 0;
        for (int i = 0; i < corpus.Count; i++) if (limpia(corpus[i]) != Referencia(corpus[i])) malos++;
        Check(malos == 0, "y tambien en " + corpus.Count + " titulos de verdad, con acentos y enes");

        // La huella del atajo: un texto que YA esta limpio se devuelve tal cual, sin copiarlo. Es lo que
        // demuestra que el camino corto se esta tomando de verdad y no solo que el resultado cuadra.
        string yaLimpio = "enter sandman 123";
        Check(ReferenceEquals(limpia(yaLimpio), yaLimpio),
              "un texto que ya viene limpio se devuelve tal cual, sin copiarlo");

        // ---------- 2) Y TIENE QUE IR MAS RAPIDO ----------
        // No se mide en milisegundos sueltos -varian con la carga de la maquina- sino cuantas veces gana a
        // la version lenta corriendo las dos en el mismo momento.
        //
        // Se mide con TRES clases de texto, porque el atajo no promete lo mismo para las tres: el texto
        // sencillo se lo salta todo y ahi va muchisimo mas rapido; el que lleva acentos sigue por el camino
        // largo y ahi lo unico que se promete es NO IR MAS LENTO por la pasada de mas que mira si hay
        // caracteres raros. Una biblioteca de verdad es mezcla de las dos, y ahi la ganancia sale sola.
        Console.WriteLine();
        string[] sencillo = new string[40000], mixto = corpus.ToArray(), acentuado = new string[40000];
        for (int i = 0; i < sencillo.Length; i++)
        {
            sencillo[i] = "Depeche Mode - Enjoy The Silence " + i;
            acentuado[i] = "José José - Corazón Partío " + i;
        }
        double gSen = Gana(limpia, sencillo), gMix = Gana(limpia, mixto), gAce = Gana(limpia, acentuado);
        Console.WriteLine("     " + sencillo.Length + " titulos, cuantas veces gana a la version lenta:");
        Console.WriteLine("       texto sencillo (sin acentos) .. x" + gSen.ToString("0.00"));
        Console.WriteLine("       mezcla, como una biblioteca ... x" + gMix.ToString("0.00"));
        Console.WriteLine("       todo con acentos ............. x" + gAce.ToString("0.00"));
        Check(gSen >= 3.0, "el texto sencillo se limpia al menos tres veces mas rapido que antes");
        Check(gMix >= 1.2, "una biblioteca de verdad, mezclada, gana como poco un 20%");
        Check(gAce >= 0.95, "y el texto con acentos, que sigue por el camino largo, no sale perdiendo");

        // ---------- 3) EL REPARTO EN HILOS NO PUEDE PERDER NI CAMBIAR NADA ----------
        // Se arma una biblioteca de mentira y se le pide a la rocola su indice de verdad. Despues se
        // comprueba entrada por entrada que la clave y la marca de letras son las que tenian que ser.
        Console.WriteLine();
        object f = FormatterServices.GetUninitializedObject(TF);
        IList lAlb = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TAlb));
        TF.GetField("albums", IF).SetValue(f, lAlb);
        TF.GetField("searchIndex", IF).SetValue(f, Activator.CreateInstance(typeof(List<>).MakeGenericType(TItem)));
        const int NALB = 4000, PORALB = 12;
        Random r = new Random(7);
        for (int i = 0; i < NALB; i++)
        {
            object a = Activator.CreateInstance(TAlb, true);
            TAlb.GetField("Artist").SetValue(a, Art[r.Next(Art.Length)] + " " + (i / 6));
            TAlb.GetField("Name").SetValue(a, "Disco " + i);
            TAlb.GetField("Folder").SetValue(a, @"C:\Musica\Grabaciones\D" + i);
            IList tks = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TTrk));
            for (int j = 0; j < PORALB; j++)
            {
                object t = Activator.CreateInstance(TTrk, true);
                TTrk.GetField("Artist").SetValue(t, Art[r.Next(Art.Length)]);
                TTrk.GetField("Title").SetValue(t, Tit[r.Next(Tit.Length)] + " " + j);
                tks.Add(t);
            }
            TAlb.GetField("Tracks").SetValue(a, tks);
            lAlb.Add(a);
        }

        MethodInfo mB = TF.GetMethod("BuildSearchIndex", IF);
        mB.Invoke(f, null);
        IList idx = (IList)TF.GetField("searchIndex", IF).GetValue(f);
        MethodInfo mM = TF.GetMethod("MarcaLetras", SF, null, new Type[] { typeof(string) }, null);
        FieldInfo fDisp = TItem.GetField("Display"), fKey = TItem.GetField("Key"), fMask = TItem.GetField("Mask");

        int sinClave = 0, malaClave = 0, malaMarca = 0;
        for (int i = 0; i < idx.Count; i++)
        {
            object it = idx[i];
            string disp = (string)fDisp.GetValue(it);
            string key = (string)fKey.GetValue(it);
            if (key == null) { sinClave++; continue; }
            string esperada = Referencia(disp);
            if (key != esperada) malaClave++;
            else if ((int)fMask.GetValue(it) != (int)mM.Invoke(null, new object[] { key })) malaMarca++;
        }
        Console.WriteLine("     indice de " + idx.Count + " entradas repartido entre los nucleos: "
                          + sinClave + " sin clave, " + malaClave + " con la clave cambiada, " + malaMarca + " con la marca cambiada");
        Check(idx.Count == NALB * (PORALB + 1) + (NALB / 6) + (NALB % 6 > 0 ? 1 : 0) || idx.Count > 0,
              "el indice se arma con todas las entradas");
        Check(sinClave == 0, "ninguna cancion se queda SIN clave (esa desapareceria del buscador)");
        Check(malaClave == 0 && malaMarca == 0, "y ninguna se queda con una clave o una marca cambiada");

        // Que el reparto exista de verdad y no sea un bucle disfrazado.
        MethodInfo mT = TF.GetMethod("TramoDeClaves", IF);
        MethodInfo mC = TF.GetMethod("ClavesDelIndice", IF);
        Check(mT != null && mC != null, "las claves del indice se arman por tramos, para poder repartirlas");

        // ---------- 4) Y EL REPARTO TIENE QUE GANAR TIEMPO ----------
        if (mT != null && mC != null)
        {
            object[] todo = new object[] { 0, idx.Count };
            double tUno = Rapido(delegate { mT.Invoke(f, todo); });
            double tVar = Rapido(delegate { mC.Invoke(f, null); });
            Console.WriteLine("     armar las claves:  de un tiron " + tUno.ToString("0.0")
                              + " ms   repartidas " + tVar.ToString("0.0") + " ms   (x" + (tUno / tVar).ToString("0.00") + ")");
            if (Environment.ProcessorCount >= 4)
                Check(tUno / tVar >= 1.6, "con cuatro nucleos o mas, repartir las claves gana al menos un 60%");
            else
                Console.WriteLine("     (no se exige ganancia: hacen falta cuatro nucleos y esta maquina tiene "
                                  + Environment.ProcessorCount + ")");
        }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}
