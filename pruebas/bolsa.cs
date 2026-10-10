using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;

// LA PRIMERA LETRA DEL BUSCADOR: ¿SALE IGUAL SI SE GUARDA, Y PAGA GUARDARLA?
//
// De todas las teclas de una busqueda, la PRIMERA es la unica cara. El buscador se salta de golpe a casi
// todo el indice mirando que letras tiene cada cancion (ver MarcaLetras), pero con una sola letra tecleada
// ese atajo no descarta a nadie -casi todas las canciones tienen una "a"-, asi que hay que puntuar el
// indice ENTERO. A partir de la segunda letra ya solo se repasa lo que quedaba.
//
// Y para una letra suelta la respuesta es SIEMPRE la misma mientras no cambie la musica. Asi que se puede
// guardar: la primera "a" de la noche cuesta lo de siempre y las demas salen de memoria.
//
// Eso es la teoria. Aqui se comprueban las dos cosas que hacen falta para aceptarlo, en este orden:
//
//   1) QUE SALGA IDENTICO.  Es lo delicado: la lista guardada es EL MISMO OBJETO que se le entrega al
//      buscador, y los puntos de cada cancion los pisa cualquier otra busqueda que se haga en medio. Por
//      eso los puntos se guardan aparte. Si eso estuviera mal, la segunda vez que alguien teclea "a"
//      saldrian los mismos discos en OTRO ORDEN, que es de esos fallos que no se ven hasta que se ven.
//   2) QUE PAGUE.  Si guardarlo no ahorra tiempo de verdad, sobra: aqui una optimizacion que no se nota
//      es codigo de mas que puede romperse, y se quita (como ya paso con el troceado del buscador).
class Bolsa
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags IF = BindingFlags.Instance | BindingFlags.NonPublic;

    static Type TF, TItem, TAlb, TTrk, TSO;
    static FieldInfo fIdx, fBolsa, fUltQ, fUltM, fOver, fRes, fDisp, fScore;
    static MethodInfo mRecomp;

    delegate void Recalcula(string q);

    // Una biblioteca inventada pero con la forma de una de verdad: artistas con varios discos y sus
    // canciones. Es el mismo material que usa 'buscaperf', para que los numeros se puedan comparar.
    static readonly string[] Art = { "Depeche Mode", "Alejandro Fernandez", "Los Angeles Azules", "Metallica", "Jose Jose",
                                     "Vicente Fernandez", "Grupo Firme", "Marco Antonio Solis", "Bronco", "Los Tigres del Norte",
                                     "Juan Gabriel", "Cafe Tacvba", "Mana", "Soda Stereo", "Los Bukis", "Banda MS",
                                     "Christian Nodal", "Camila", "Reik", "La Arrolladora" };
    static readonly string[] Pal = { "Amor", "Noche", "Corazon", "Vida", "Cielo", "Fuego", "Luna", "Alma", "Sombra", "Camino",
                                     "Silencio", "Tiempo", "Lluvia", "Estrella", "Viento", "Fiesta", "Rosa", "Mar", "Sol", "Beso" };

    // Una rocola con su biblioteca, SIN abrir ventana: se fabrica el objeto sin pasar por el constructor
    // (igual que hace 'indice') y se le ponen a mano solo las cosas que el buscador toca.
    static object Monta(int canciones)
    {
        object f = FormatterServices.GetUninitializedObject(TF);
        IList lAlb = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TAlb));
        TF.GetField("albums", IF).SetValue(f, lAlb);
        fIdx.SetValue(f, Activator.CreateInstance(typeof(List<>).MakeGenericType(TItem)));
        fOver.SetValue(f, Activator.CreateInstance(TSO, true));
        if (fBolsa != null) fBolsa.SetValue(f, Activator.CreateInstance(fBolsa.FieldType));

        FieldInfo aArt = TAlb.GetField("Artist"), aNom = TAlb.GetField("Name"), aCar = TAlb.GetField("Folder"), aTks = TAlb.GetField("Tracks");
        FieldInfo tArt = TTrk.GetField("Artist"), tTit = TTrk.GetField("Title"), tRuta = TTrk.GetField("Path");
        Random r = new Random(4);
        int porDisco = 12, hechas = 0, disco = 0;
        while (hechas < canciones)
        {
            object a = Activator.CreateInstance(TAlb, true);
            string art = Art[disco % Art.Length];
            aArt.SetValue(a, art);
            aNom.SetValue(a, Pal[r.Next(Pal.Length)] + " " + Pal[r.Next(Pal.Length)] + " " + disco);
            aCar.SetValue(a, "/musica/d" + disco);
            IList tks = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TTrk));
            for (int j = 0; j < porDisco && hechas < canciones; j++, hechas++)
            {
                object t = Activator.CreateInstance(TTrk, true);
                tArt.SetValue(t, art);
                tTit.SetValue(t, Pal[r.Next(Pal.Length)] + " " + Pal[r.Next(Pal.Length)] + " " + hechas);
                tRuta.SetValue(t, "/musica/d" + disco + "/" + hechas + ".mp3");
                tks.Add(t);
            }
            aTks.SetValue(a, tks);
            lAlb.Add(a);
            disco++;
        }
        TF.GetMethod("BuildSearchIndex", IF).Invoke(f, null);
        return f;
    }

    // Lo que el buscador ENSENA: nombre y puntos de cada resultado, en el orden en que salen.
    static List<string> Ensena(object f)
    {
        IList res = (IList)fRes.GetValue(fOver.GetValue(f));
        List<string> o = new List<string>(res.Count);
        for (int i = 0; i < res.Count; i++) o.Add((string)fDisp.GetValue(res[i]) + " |" + (int)fScore.GetValue(res[i]));
        return o;
    }

    // Dejar la rocola como recien encendida: sin nada guardado de busquedas anteriores.
    static void Olvida(object f)
    {
        fUltQ.SetValue(f, null); fUltM.SetValue(f, null);
        if (fBolsa != null) { IDictionary d = (IDictionary)fBolsa.GetValue(f); if (d != null) d.Clear(); }
    }

    static bool Igual(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }

    // La maquina esta compartida: una tanda cualquiera puede salir el triple de lenta por culpa de otro
    // proceso. Se toma el MINIMO de varias tandas, que es lo mas cerca del coste real. (Igual que 'buscaperf'.)
    static double Mide(int veces, Action a)
    {
        a(); a();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        double mejor = double.MaxValue;
        for (int t = 0; t < 7; t++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < veces; i++) a();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / veces;
            if (ms < mejor) mejor = ms;
        }
        return mejor;
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "SearchItem") TItem = t;
            else if (t.Name == "Album") TAlb = t;
            else if (t.Name == "Track") TTrk = t;
            else if (t.Name == "SearchOverlay") TSO = t;
        }
        fIdx = TF.GetField("searchIndex", IF);
        fBolsa = TF.GetField("bolsaLetra", IF);
        fUltQ = TF.GetField("lastQuery", IF);
        fUltM = TF.GetField("lastMatches", IF);
        fOver = TF.GetField("searchOverlay", IF);
        mRecomp = TF.GetMethod("RecomputeResults", IF);

        Check(TItem != null && TAlb != null && TTrk != null && TSO != null && fIdx != null && fUltQ != null
              && fUltM != null && fOver != null && mRecomp != null, "el buscador y su indice estan donde tienen que estar");
        if (TItem == null || mRecomp == null) return 1;
        fRes = TSO.GetField("Results");
        fDisp = TItem.GetField("Display"); fScore = TItem.GetField("Score");

        // Esta es la que FALLA con el codigo de antes: antes no habia nada guardado, asi que ni existe.
        Check(fBolsa != null, "lo que casa con cada letra suelta se guarda (bolsaLetra)");

        // ---------- 1) QUE SALGA IDENTICO ----------
        Console.WriteLine("\n  1) LA SEGUNDA VEZ QUE SE TECLEA UNA LETRA SALE EXACTAMENTE LO MISMO\n");
        object f = Monta(8000);
        Recalcula rec = (Recalcula)Delegate.CreateDelegate(typeof(Recalcula), f, mRecomp);
        Console.WriteLine("     biblioteca de 8000 canciones  ->  " + ((IList)fIdx.GetValue(f)).Count + " entradas en el indice");

        // Con OTRAS busquedas en medio, que es donde se rompe si los puntos no se guardan aparte: cada
        // busqueda escribe los puntos DENTRO de cada cancion, pisando los de la vez anterior.
        string letras = "aeiounmdlsrtcpbgjz";
        int distintas = 0, comparadas = 0; string primero = null;
        foreach (char c in letras)
        {
            string q = c.ToString();
            Olvida(f); rec(q);
            List<string> bueno = Ensena(f);                       // la respuesta sin nada guardado
            rec("depeche"); rec("amor noche"); rec("e"); rec("fernandez");
            rec(q);
            List<string> guardado = Ensena(f);                    // la misma pregunta, ya de memoria
            comparadas++;
            if (!Igual(bueno, guardado))
            {
                distintas++;
                if (primero == null)
                    primero = "\"" + q + "\": " + bueno.Count + " resultados contra " + guardado.Count
                            + (bueno.Count == guardado.Count && bueno.Count > 0 ? "  (el primero: " + bueno[0] + "  ->  " + guardado[0] + ")" : "");
            }
        }
        Console.WriteLine("     " + comparadas + " letras tecleadas dos veces, con otras busquedas en medio: " + distintas + " dieron otra cosa");
        if (primero != null) Console.WriteLine("     la primera que cambio -> " + primero);
        Check(distintas == 0, "teclear una letra dos veces da la MISMA lista, en el MISMO orden y con los MISMOS puntos");

        // Y el caso de escribir hacia adelante y borrar: "a", "am", "amo" y vuelta a "a". Al escribir hacia
        // adelante el buscador se apoya en las coincidencias de la tecla anterior -que son justo la lista
        // guardada-, asi que si alguna de esas pasadas la tocara, al borrar saldria otra cosa.
        Olvida(f); rec("a"); List<string> aSolo = Ensena(f);
        rec("am"); rec("amo"); rec("amor"); rec("amo"); rec("am"); rec("a");
        Check(Igual(aSolo, Ensena(f)), "escribir \"amor\" letra a letra y borrarlo entero deja la \"a\" igual que estaba");

        // ---------- 2) QUE SE CONSULTE DE VERDAD, Y QUE NO SE QUEDE VIEJA ----------
        Console.WriteLine("\n  2) SE CONSULTA DE VERDAD, Y AL RECARGAR LA MUSICA SE VACIA\n");
        if (fBolsa != null)
        {
            // Antes de medir nada hay que probar que lo guardado se USA. Se le mete una respuesta falsa
            // -vacia- y se comprueba que el buscador la devuelve: si contestara igual que siempre, es que ni
            // la mira, y toda la medida de abajo estaria midiendo aire.
            Olvida(f); rec("a"); int deVerdad = Ensena(f).Count;
            IDictionary d = (IDictionary)fBolsa.GetValue(f);
            object entrada = d['a'];
            Type TBol = entrada.GetType();
            object listaBuena = TBol.GetField("Lista").GetValue(entrada);
            TBol.GetField("Lista").SetValue(entrada, Activator.CreateInstance(typeof(List<>).MakeGenericType(TItem)));
            TBol.GetField("Puntos").SetValue(entrada, new int[0]);
            // La entrada tiene dos niveles coherentes: coincidencias completas y top visible.
            FieldInfo fMejores = TBol.GetField("Mejores");
            if (fMejores != null) fMejores.SetValue(entrada, Activator.CreateInstance(typeof(List<>).MakeGenericType(TItem)));
            rec("a"); int conMentira = Ensena(f).Count;
            Console.WriteLine("     con la respuesta de verdad: " + deVerdad + " resultados;  con una falsa metida a mano: " + conMentira);
            Check(deVerdad > 0 && conMentira == 0, "la respuesta guardada se consulta de verdad (no es un adorno)");
            TBol.GetField("Lista").SetValue(entrada, listaBuena);

            // Y el seguro: al recargar la musica, lo guardado apunta a canciones que ya no existen.
            Olvida(f); rec("a");
            Check(Igual(Ensena(f), aSolo), "y al vaciarla, la respuesta vuelve a salir del indice");

            // Alterar la lista que usa la vista no debe corromper el top cacheado.
            rec("a"); ((IList)fRes.GetValue(fOver.GetValue(f))).Clear(); rec("a");
            Check(Igual(Ensena(f), aSolo), "la vista recibe una copia del top: modificarla no corrompe la cache");
            // Una consulta mas larga debe seguir usando TODAS las coincidencias, no solo las 40 visibles.
            rec("e"); rec("a"); rec("amor noche"); List<string> desdeCache = Ensena(f);
            Olvida(f); rec("amor noche");
            Check(Igual(desdeCache, Ensena(f)), "la busqueda incremental conserva coincidencias fuera del top 40");

            MethodInfo mStart = TF.GetMethod("StartScan", IF);
            Check(mStart != null, "el rearmado de la biblioteca esta donde tiene que estar");
            if (mStart != null)
            {
                int lee, escribe;
                Codigo.UsosDeCampo(mStart, "bolsaLetra", out lee, out escribe);
                Check(lee > 0 || escribe > 0, "al rearmar la biblioteca la bolsa se vacia (si no, apuntaria a canciones que ya no estan)");
            }

            // Referencias logicas de la lista, sin cabeceras/capacidad sobrante. Los puntos
            // ahora corresponden solo al top visible; no asumir cuatro bytes por cada coincidencia.
            Olvida(f);
            int mayor = 0;
            foreach (char c in letras) { rec(c.ToString()); int n = Ensena(f) == null ? 0 : ((IList)TBol.GetField("Lista").GetValue(((IDictionary)fBolsa.GetValue(f))[c])).Count; if (n > mayor) mayor = n; }
            Console.WriteLine("     la letra que mas casa guarda " + mayor + " referencias logicas; los puntos son solo los del top visible.");
            Check(mayor <= 131072, "una letra no supera el presupuesto de coincidencias cacheadas");
        }

        // ---------- 3) Y CUANTO SE AHORRA ----------
        Console.WriteLine("\n  3) Y CUANTO SE AHORRA DE VERDAD\n");
        Console.WriteLine(string.Format("     {0,-12} {1,9} | {2,11} {3,11} {4,8}", "biblioteca", "entradas", "1a vez", "2a vez", "gana"));
        Console.WriteLine("     " + new string('-', 60));
        int[] tam = { 2000, 8000, 20000 };
        double ganaGrande = 0;
        foreach (int n in tam)
        {
            object g = Monta(n);
            Recalcula r2 = (Recalcula)Delegate.CreateDelegate(typeof(Recalcula), g, mRecomp);
            int entradas = ((IList)fIdx.GetValue(g)).Count;
            // "1a vez" = como estaba antes: cada tecla vuelve a puntuar el indice entero. Se descuenta lo que
            // cuesta el propio vaciado, que es del banco de pruebas y no del buscador.
            double vacio = Mide(200, delegate { Olvida(g); });
            double primeraVez = Mide(200, delegate { Olvida(g); r2("a"); }) - vacio;
            double segundaVez = Mide(200, delegate { r2("a"); });
            double gana = primeraVez / Math.Max(segundaVez, 0.000001);
            Console.WriteLine(string.Format("     {0,-12} {1,9} | {2,8:0.000} ms {3,8:0.000} ms {4,7}", n + " canciones", entradas,
                                            primeraVez, segundaVez, "x" + gana.ToString("0.0")));
            if (n == 20000) ganaGrande = gana;
        }
        Console.WriteLine();
        Console.WriteLine("     Medicion sintetica en ESTE equipo; no permite predecir otro CPU con un multiplicador fijo.");
        Check(ganaGrande >= 2.0, "con 20000 canciones, la segunda vez que se teclea una letra cuesta menos de la MITAD");

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }
}
