using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

// CUANTO TARDA EL BUSCADOR EN CONTESTAR A UNA TECLA.  ANTES Y AHORA.
//
// El cliente teclea con el dedo en la pantalla tactil. Todo lo que pase de ~50 ms entre la tecla y los
// resultados se siente como que la maquina "se traba". Aqui se mide con una biblioteca del tamano de la del
// bar y con las busquedas que de verdad hace la gente: una letra suelta, un artista, dos palabras.
//
// Se cargan los DOS programas a la vez (rocola_ref.exe = como estaba, rocola.exe = como esta ahora) y se
// mide cada mitad del trabajo por separado:
//   FILTRAR  = pasar por las veinte mil entradas del indice puntuando cada una.
//   ELEGIR 40 = de todo lo que casa, quedarse con los 40 que caben en pantalla.
//
// Nota sobre la maquina: esto corre en el servidor, que es MUCHO mas rapido que el ordenador del bar.
// Los milisegundos de abajo hay que multiplicarlos por unas 4 o 5 veces para el Pentium con Windows XP.
class BuscaPerf
{
    // Score se llama una vez por entrada del indice: por REFLEXION cuesta ~0.9 us, que es cien veces mas que
    // la propia funcion, y la medida saldria toda reflexion. Se saca un delegado UNA vez y a partir de ahi
    // las llamadas van a velocidad normal.
    delegate int Viejo(string cand, string q, string spaceQ, string[] toks);
    delegate int Nuevo(string cand, string q, string[] toks);
    delegate int Marca(string s);

    // La maquina esta compartida: una tanda cualquiera puede salir el triple de lenta por culpa de otro
    // proceso. Se toma el MINIMO de varias tandas, que es lo mas cerca del coste real de la funcion.
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

    // Una biblioteca inventada pero con la forma de una de verdad: artistas con varios discos y sus canciones.
    static string[] Indice(int canciones)
    {
        string[] art = { "Depeche Mode", "Alejandro Fernandez", "Los Angeles Azules", "Metallica", "Jose Jose",
                         "Vicente Fernandez", "Grupo Firme", "Marco Antonio Solis", "Bronco", "Los Tigres del Norte",
                         "Juan Gabriel", "Cafe Tacvba", "Mana", "Soda Stereo", "Los Bukis", "Banda MS",
                         "Christian Nodal", "Camila", "Reik", "La Arrolladora" };
        string[] pal = { "Amor", "Noche", "Corazon", "Vida", "Cielo", "Fuego", "Luna", "Alma", "Sombra", "Camino",
                         "Silencio", "Tiempo", "Lluvia", "Estrella", "Viento", "Fiesta", "Rosa", "Mar", "Sol", "Beso" };
        List<string> ix = new List<string>(canciones + canciones / 8);
        Random r = new Random(4);
        for (int i = 0; i < art.Length; i++) ix.Add(art[i].ToLowerInvariant());
        int porDisco = 12, hechas = 0, disco = 0;
        while (hechas < canciones)
        {
            string a = art[disco % art.Length];
            ix.Add((a + " - " + pal[r.Next(pal.Length)] + " " + pal[r.Next(pal.Length)]).ToLowerInvariant());
            for (int j = 0; j < porDisco && hechas < canciones; j++, hechas++)
                ix.Add((a + " - " + pal[r.Next(pal.Length)] + " " + pal[r.Next(pal.Length)] + " " + hechas).ToLowerInvariant());
            disco++;
        }
        return ix.ToArray();
    }

    // LO DE ANTES: ordenar TODAS las coincidencias para luego tirar todas menos 40.
    static void OrdenaTodo(int[] arr, Comparison<int> cmp)
    {
        int[] copia = (int[])arr.Clone();
        Array.Sort(copia, cmp);
    }

    // LO DE AHORA: una sola pasada guardando los 40 mejores. Es el mismo metodo que TomaMejores del
    // programa (que ya se ha probado aparte que da exactamente la misma lista).
    static void SoloLos40(int[] arr, Comparison<int> cmp)
    {
        const int N = 40;
        int[] top = new int[N]; int n = 0;
        for (int i = 0; i < arr.Length; i++)
        {
            int v = arr[i];
            if (n < N)
            {
                int j = n++;
                while (j > 0 && cmp(top[j - 1], v) > 0) { top[j] = top[j - 1]; j--; }
                top[j] = v;
            }
            else if (cmp(v, top[N - 1]) < 0)
            {
                int j = N - 1;
                while (j > 0 && cmp(top[j - 1], v) > 0) { top[j] = top[j - 1]; j--; }
                top[j] = v;
            }
        }
    }

    static int Main()
    {
        Assembly aV = Assembly.LoadFrom("rocola_ref.exe");
        Assembly aN = Assembly.LoadFrom("rocola.exe");
        Type tV = aV.GetType("Rocola.Form1", true), tN = aN.GetType("Rocola.Form1", true);
        Viejo punV = (Viejo)Delegate.CreateDelegate(typeof(Viejo), tV.GetMethod("Score", BindingFlags.Static | BindingFlags.NonPublic));
        Nuevo punN = (Nuevo)Delegate.CreateDelegate(typeof(Nuevo), tN.GetMethod("Score", BindingFlags.Static | BindingFlags.NonPublic));
        MethodInfo mNorm = tN.GetMethod("Normalize", BindingFlags.Static | BindingFlags.NonPublic);
        Marca marca = (Marca)Delegate.CreateDelegate(typeof(Marca), tN.GetMethod("MarcaLetras", BindingFlags.Static | BindingFlags.NonPublic));

        int[] tamanos = { 2000, 8000, 20000 };
        string[] consultas = { "a", "de", "dep", "depeche", "amor noche" };

        foreach (int n in tamanos)
        {
            string[] ix = Indice(n);
            // La marca de letras de cada entrada se calcula UNA vez al construir el indice, en segundo
            // plano, igual que hace el programa. Por eso no entra en el tiempo de la tecla.
            int[] masc = new int[ix.Length];
            for (int i = 0; i < ix.Length; i++) masc[i] = marca(ix[i]);
            Console.WriteLine("\n\n  BIBLIOTECA DE " + n + " CANCIONES   ->   " + ix.Length + " entradas en el indice");
            Console.WriteLine("  " + new string('-', 100));
            Console.WriteLine(string.Format("  {0,-12} {1,7} | {2,17} | {3,19} | {4,17}", "tecleado", "casan", "FILTRAR (ms)", "ELEGIR LOS 40 (ms)", "UNA TECLA (ms)"));
            Console.WriteLine(string.Format("  {0,-12} {1,7} | {2,7} {3,7} {4,1} | {5,8} {6,8} {7,1} | {8,7} {9,7} {10,1}",
                                            "", "", "antes", "ahora", "", "antes", "ahora", "", "antes", "ahora", ""));
            Console.WriteLine("  " + new string('-', 100));
            foreach (string cons in consultas)
            {
                string q = (string)mNorm.Invoke(null, new object[] { cons });
                string spaceQ = " " + q;
                string[] toks = (q.IndexOf(' ') >= 0) ? q.Split(' ') : null;

                int[] punt = new int[ix.Length];
                int casan = 0;
                for (int i = 0; i < ix.Length; i++) { punt[i] = punN(ix[i], q, toks); if (punt[i] > 0) casan++; }

                double filtroV = Mide(20, delegate { for (int i = 0; i < ix.Length; i++) punV(ix[i], q, spaceQ, toks); });
                int qm = marca(q);
                double filtroN = Mide(20, delegate
                {
                    for (int i = 0; i < ix.Length; i++) { if ((masc[i] & qm) != qm) continue; punN(ix[i], q, toks); }
                });

                List<int> m = new List<int>();
                for (int i = 0; i < ix.Length; i++) if (punt[i] > 0) m.Add(i);
                int[] arr = m.ToArray();
                // El comparador de VERDAD (puntaje, luego tipo, y a igualdad el texto). Comparar textos es lo
                // caro, y con una sola letra tecleada casi todo empata en puntaje. Es el MISMO para los dos
                // lados, asi que lo que se compara aqui es solo "ordenar veinte mil" contra "quedarse con 40".
                Comparison<int> cmpReal = delegate (int a, int b)
                {
                    if (punt[b] != punt[a]) return punt[b] - punt[a];
                    int ka = ix[a].Length & 3, kb = ix[b].Length & 3;
                    if (ka != kb) return ka - kb;
                    return string.Compare(ix[a], ix[b], StringComparison.OrdinalIgnoreCase);
                };
                double ordenV = Mide(20, delegate { OrdenaTodo(arr, cmpReal); });
                double ordenN = Mide(20, delegate { SoloLos40(arr, cmpReal); });

                double totV = filtroV + ordenV, totN = filtroN + ordenN;
                string estrella = (totV / Math.Max(totN, 0.0001) >= 1.5) ? "*" : " ";
                Console.WriteLine(string.Format("  \"{0,-10}\" {1,7} | {2,7:0.00} {3,7:0.00} {4,1} | {5,8:0.00} {6,8:0.00} {7,1} | {8,7:0.00} {9,7:0.00} {10,1}",
                                                cons, casan, filtroV, filtroN, "", ordenV, ordenN, "", totV, totN, estrella));
            }
        }
        Console.WriteLine("\n  (*) donde el tiempo por tecla se ha reducido a menos de la mitad.");
        Console.WriteLine("  Recordatorio: en el ordenador del bar multiplica estos numeros por unas 4 o 5 veces.\n");
        return 0;
    }
}
