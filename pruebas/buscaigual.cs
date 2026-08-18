using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

// PRUEBA DE QUE EL BUSCADOR NUEVO CONTESTA EXACTAMENTE LO MISMO QUE EL VIEJO.
//
// Se ha cambiado como puntua el buscador (una sola pasada por el texto en vez de dos) y como elige los 40
// que se ensenan (quedarse con los 40 mejores en vez de ordenar los veinte mil). Eso es mas rapido, pero de
// nada sirve si sale otra cosa en pantalla. Aqui se cargan los DOS programas a la vez -el de antes y el de
// ahora- se les hace la misma pregunta cientos de miles de veces y se comparan las respuestas una por una.
class BuscaIgual
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    delegate int Viejo(string cand, string q, string spaceQ, string[] toks);
    delegate int Nuevo(string cand, string q, string[] toks);

    static MethodInfo mNormV, mNormN;

    // ---------- material de prueba ----------
    static readonly string[] Palabras = {
        "amor", "noche", "corazon", "vida", "cielo", "fuego", "luna", "alma", "sombra", "camino",
        "de", "la", "el", "los", "un", "mi", "tu", "y", "a", "en",
        "depeche", "mode", "metallica", "jose", "juan", "gabriel", "vicente", "fernandez", "bronco", "banda",
        "silencio", "tiempo", "lluvia", "estrella", "viento", "fiesta", "rosa", "mar", "sol", "beso" };

    static string TextoAlAzar(Random r)
    {
        int n = r.Next(1, 7);
        string s = "";
        for (int i = 0; i < n; i++) { if (i > 0) s += (r.Next(12) == 0 ? "  " : (r.Next(6) == 0 ? " - " : " ")); s += Palabras[r.Next(Palabras.Length)]; }
        if (r.Next(10) == 0) s = " " + s;              // empieza con espacio
        if (r.Next(10) == 0) s = s + " ";              // acaba con espacio
        if (r.Next(20) == 0) s = s + s;                // texto con la misma frase dos veces
        return s;
    }

    static string ConsultaAlAzar(Random r, string fuente)
    {
        int modo = r.Next(10);
        if (modo < 4 && fuente.Length > 1)             // un trozo del propio texto: asi SI coincide
        {
            int a = r.Next(fuente.Length);
            int l = r.Next(1, Math.Min(9, fuente.Length - a + 1));
            return fuente.Substring(a, l);
        }
        if (modo < 6) return Palabras[r.Next(Palabras.Length)];
        if (modo < 8) return Palabras[r.Next(Palabras.Length)] + " " + Palabras[r.Next(Palabras.Length)];
        int n = r.Next(0, 5);
        char[] cs = new char[n];
        for (int i = 0; i < n; i++) cs[i] = " aeioumnstdlrz".ToCharArray()[r.Next(14)];
        return new string(cs);
    }

    static int Main()
    {
        Assembly aV = Assembly.LoadFrom("rocola_ref.exe");     // el de ANTES
        Assembly aN = Assembly.LoadFrom("rocola.exe");         // el de AHORA
        Type tV = aV.GetType("Rocola.Form1", true), tN = aN.GetType("Rocola.Form1", true);

        MethodInfo msV = tV.GetMethod("Score", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo msN = tN.GetMethod("Score", BindingFlags.Static | BindingFlags.NonPublic);
        Check(msV != null && msN != null, "los dos programas tienen su funcion de puntuar");
        if (msV == null || msN == null) return 1;
        Viejo punV = (Viejo)Delegate.CreateDelegate(typeof(Viejo), msV);
        Nuevo punN = (Nuevo)Delegate.CreateDelegate(typeof(Nuevo), msN);
        mNormV = tV.GetMethod("Normalize", BindingFlags.Static | BindingFlags.NonPublic);
        mNormN = tN.GetMethod("Normalize", BindingFlags.Static | BindingFlags.NonPublic);

        // ---------- 1) MISMO PUNTAJE, CASO POR CASO ----------
        Console.WriteLine("\n  1) EL PUNTAJE ES EL MISMO QUE ANTES\n");

        // Primero los casos escogidos a mano, los que de verdad se teclean en el bar y los que mas facil
        // se rompen: la palabra al principio, a la mitad, repetida, pegada a un guion, con espacios dobles.
        string[][] mano = {
            new string[]{ "depeche mode - enjoy the silence", "de" },
            new string[]{ "depeche mode - enjoy the silence", "mode" },
            new string[]{ "depeche mode - enjoy the silence", "silence" },
            new string[]{ "depeche mode - enjoy the silence", "e s" },
            new string[]{ "modelo de moda modesto", "mode" },      // tres veces, la 1a NO empieza palabra
            new string[]{ "amodelo modesto", "mode" },             // la 1a pegada, la 2a empieza palabra
            new string[]{ "amodelo amoderno", "mode" },            // ninguna empieza palabra
            new string[]{ "aaaa", "a" },
            new string[]{ "aaaa aaaa", "aa" },                     // solapadas
            new string[]{ " amor", "amor" },                       // texto que empieza con espacio
            new string[]{ "amor  amor", " amor" },                 // consulta que empieza con espacio
            new string[]{ "los angeles azules", "angeles azules" },
            new string[]{ "juan gabriel - amor eterno", "eterno juan" },  // dos palabras al reves
            new string[]{ "juan gabriel", "juan gabriel" },         // identico
            new string[]{ "juan gabriel", "zzz" },
            new string[]{ "juan gabriel", "" },
        };
        int malos = 0;
        for (int i = 0; i < mano.Length; i++)
        {
            string cand = mano[i][0], cons = mano[i][1];
            int a, b; Compara(punV, punN, cand, cons, out a, out b);
            bool ok = (a == b);
            Console.WriteLine(string.Format("     {0,-36} \"{1,-16}\"  antes {2,6}   ahora {3,6}   {4}",
                                            cand, cons, a, b, ok ? "igual" : "<<< DISTINTO"));
            if (!ok) malos++;
        }
        Check(malos == 0, "los " + mano.Length + " casos escogidos a mano puntuan igual que antes");

        // Y ahora por fuerza bruta: cientos de miles de combinaciones al azar.
        Random r = new Random(20250812);
        int distintos = 0, comparados = 0, casaron = 0; string primerFallo = null;
        string[] textos = new string[3000];
        for (int i = 0; i < textos.Length; i++) textos[i] = TextoAlAzar(r);
        for (int t = 0; t < textos.Length; t++)
        {
            for (int k = 0; k < 60; k++)
            {
                string cons = ConsultaAlAzar(r, textos[r.Next(textos.Length)]);
                int a, b; Compara(punV, punN, textos[t], cons, out a, out b);
                comparados++;
                if (a > 0) casaron++;
                if (a != b) { distintos++; if (primerFallo == null) primerFallo = "\"" + textos[t] + "\" / \"" + cons + "\"  antes " + a + " ahora " + b; }
            }
        }
        Console.WriteLine("\n     combinaciones comparadas: " + comparados + "   (de ellas coinciden con algo: " + casaron + ")");
        Console.WriteLine("     puntajes distintos: " + distintos + (primerFallo != null ? "   primero: " + primerFallo : ""));
        Check(distintos == 0, "con " + comparados + " combinaciones al azar el puntaje NUNCA cambia");

        // ---------- 2) LOS 40 QUE SE ENSENAN SON LOS MISMOS ----------
        // El programa nuevo no ordena las veinte mil coincidencias: se queda con las 40 mejores de una
        // pasada. Aqui se comprueba que esas 40 son exactamente las que salian al ordenarlo todo.
        Console.WriteLine("\n  2) LOS 40 RESULTADOS QUE SE ENSENAN SON LOS MISMOS\n");
        Type tItem = null;
        foreach (Type t in tN.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            if (t.Name == "SearchItem") tItem = t;
        MethodInfo mTop = tN.GetMethod("TomaMejores", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo mCmp = tN.GetMethod("ComparaHit", BindingFlags.Static | BindingFlags.NonPublic);
        Check(tItem != null && mTop != null && mCmp != null, "la nueva seleccion de los mejores esta donde tiene que estar");
        if (tItem == null || mTop == null) { Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos); return fallos; }

        FieldInfo fKind = tItem.GetField("Kind"), fDisp = tItem.GetField("Display"), fSc = tItem.GetField("Score");
        Type tLista = typeof(List<>).MakeGenericType(tItem);
        Comparison<object> cmpRef = delegate (object x, object y)
        {
            int sa = (int)fSc.GetValue(x), sb = (int)fSc.GetValue(y);
            if (sb != sa) return sb - sa;
            int ka = (int)fKind.GetValue(x), kb = (int)fKind.GetValue(y);
            if (ka != kb) return ka - kb;
            return string.Compare((string)fDisp.GetValue(x), (string)fDisp.GetValue(y), StringComparison.OrdinalIgnoreCase);
        };

        // Tamanos y formas distintas: menos de 40, justo 40, muchisimos, todos empatados (el caso de teclear
        // una sola letra: casi todo saca el mismo puntaje y manda el nombre), y todos distintos.
        int[] tam = { 0, 1, 5, 39, 40, 41, 100, 5000, 20000 };
        int casosMal = 0, casosHechos = 0;
        foreach (int n in tam)
        {
            for (int forma = 0; forma < 3; forma++)
            {
                IList src = (IList)Activator.CreateInstance(tLista);
                List<object> copia = new List<object>();
                Random rr = new Random(1000 + n * 7 + forma);
                for (int i = 0; i < n; i++)
                {
                    object it = Activator.CreateInstance(tItem, true);
                    int sc = (forma == 0) ? rr.Next(1, 10001)          // puntajes variados
                          : (forma == 1) ? 2000                        // TODOS empatados: manda tipo y nombre
                          : (i % 3 == 0 ? 4000 : 2000);                // dos grupos grandes empatados
                    fSc.SetValue(it, sc);
                    fKind.SetValue(it, rr.Next(3));
                    fDisp.SetValue(it, Palabras[rr.Next(Palabras.Length)] + " " + rr.Next(50));
                    src.Add(it); copia.Add(it);
                }
                // Lo de ANTES: ordenar todo y coger los 40 primeros.
                copia.Sort(cmpRef);
                int take = Math.Min(40, copia.Count);
                // Lo de AHORA:
                IList dst = (IList)Activator.CreateInstance(tLista);
                mTop.Invoke(null, new object[] { src, dst, 40 });
                casosHechos++;
                bool ok = (dst.Count == take);
                for (int i = 0; ok && i < take; i++)
                    if (cmpRef(dst[i], copia[i]) != 0) ok = false;   // mismo puesto y mismo valor de orden
                if (!ok)
                {
                    casosMal++;
                    Console.WriteLine("     DISTINTO con " + n + " coincidencias, forma " + forma + " (salen " + dst.Count + ", esperaba " + take + ")");
                }
            }
        }
        Console.WriteLine("     listas probadas: " + casosHechos + " (de 0 a 20000 coincidencias, con y sin empates)   distintas: " + casosMal);
        Check(casosMal == 0, "quedarse con los 40 mejores da EXACTAMENTE la misma lista que ordenarlo todo");

        // Y que no se lleve por delante la lista de origen ni la deje a medias.
        IList orig = (IList)Activator.CreateInstance(tLista);
        for (int i = 0; i < 100; i++)
        {
            object it = Activator.CreateInstance(tItem, true);
            fSc.SetValue(it, i); fKind.SetValue(it, 0); fDisp.SetValue(it, "x" + i);
            orig.Add(it);
        }
        object[] antes = new object[orig.Count]; orig.CopyTo(antes, 0);
        IList sal = (IList)Activator.CreateInstance(tLista);
        mTop.Invoke(null, new object[] { orig, sal, 40 });
        bool intacta = (orig.Count == 100);
        for (int i = 0; intacta && i < 100; i++) if (!ReferenceEquals(orig[i], antes[i])) intacta = false;
        Check(intacta, "la lista de coincidencias que se guarda para la tecla siguiente no se toca ni se reordena");

        // Pedir mas de los que hay, o pedir cero: sin reventar.
        int revento = 0;
        int[] pide = { 0, -1, 1, 1000 };
        foreach (int pn in pide)
        {
            try { IList d2 = (IList)Activator.CreateInstance(tLista); mTop.Invoke(null, new object[] { orig, d2, pn }); if (d2.Count > Math.Max(0, Math.Min(pn, 100))) revento++; }
            catch { revento++; }
        }
        try { IList d3 = (IList)Activator.CreateInstance(tLista); mTop.Invoke(null, new object[] { Activator.CreateInstance(tLista), d3, 40 }); if (d3.Count != 0) revento++; }
        catch { revento++; }
        Check(revento == 0, "pedir cero, pedir menos que cero, pedir mas de los que hay o una lista vacia: no revienta");

        // ---------- 3) EL PRE-FILTRO DE LETRAS NUNCA TIRA UN RESULTADO BUENO ----------
        // El buscador descarta una entrada sin mirarle el texto cuando le falta alguna letra de lo tecleado.
        // Eso solo vale si NUNCA descarta algo que si casaba. Es la comprobacion mas importante de todas:
        // un fallo aqui haria que canciones que existen no aparezcan al buscarlas.
        Console.WriteLine("\n  3) EL ATAJO POR LETRAS NUNCA ESCONDE UNA CANCION\n");
        MethodInfo mMar = tN.GetMethod("MarcaLetras", BindingFlags.Static | BindingFlags.NonPublic);
        Check(mMar != null, "el atajo por letras esta donde tiene que estar");
        if (mMar != null)
        {
            Marca marca = (Marca)Delegate.CreateDelegate(typeof(Marca), mMar);
            // Textos con de todo: cifras, guiones, apostrofes, parentesis y hasta letras de otros alfabetos,
            // que es lo que aparece en los titulos de verdad ("AC/DC", "Song 2", "Bad Guy (Remix)", japones...).
            string raro = "abcdefghijklmnopqrstuvwxyz0123456789 -'()/&.,!?+#" + "あ中Жα";
            Random r3 = new Random(777);
            int escondidos = 0, probados = 0, atajados = 0; string primero3 = null;
            for (int t = 0; t < 60000; t++)
            {
                string cand, cons;
                if (t % 2 == 0)
                {
                    cand = TextoAlAzar(r3);
                    cons = ConsultaAlAzar(r3, cand);
                }
                else
                {
                    int n = r3.Next(0, 30);
                    char[] cs = new char[n];
                    for (int i = 0; i < n; i++) cs[i] = raro[r3.Next(raro.Length)];
                    cand = new string(cs);
                    int mq = r3.Next(0, 6);
                    char[] qs = new char[mq];
                    for (int i = 0; i < mq; i++) qs[i] = raro[r3.Next(raro.Length)];
                    cons = new string(qs);
                }
                // El texto del indice va SIEMPRE ya limpio (asi lo guarda el programa), y lo tecleado tambien.
                string ck = (string)mNormN.Invoke(null, new object[] { cand });
                string qn = (string)mNormN.Invoke(null, new object[] { cons });
                if (qn.Length == 0) continue;
                string[] toks = (qn.IndexOf(' ') >= 0) ? qn.Split(' ') : null;
                int mc = marca(ck), mq2 = marca(qn);
                bool atajado = (mc & mq2) != mq2;
                int sc = punN(ck, qn, toks);
                probados++;
                if (atajado) atajados++;
                if (atajado && sc > 0)
                {
                    escondidos++;
                    if (primero3 == null) primero3 = "\"" + ck + "\" / \"" + qn + "\" (puntaje " + sc + ")";
                }
            }
            Console.WriteLine("     parejas probadas: " + probados + "   descartadas por el atajo: " + atajados
                              + " (" + (100.0 * atajados / Math.Max(probados, 1)).ToString("0.0") + "%)");
            Console.WriteLine("     canciones que casaban y el atajo escondio: " + escondidos + (primero3 != null ? "   primera: " + primero3 : ""));
            Check(escondidos == 0, "en " + probados + " parejas el atajo no escondio NI UNA cancion que casara");

            // Y a mano, los casos que mas facil romperian el atajo.
            string[][] finos = {
                new string[]{ "ac dc - back in black", "ac dc" },
                new string[]{ "song 2", "2" },
                new string[]{ "song 2", "song 2" },
                new string[]{ "bad guy (remix)", "(remix)" },
                new string[]{ "jose jose", "jose" },
                new string[]{ "cancion", "cancion" },       // ya normalizado: la ene con tilde se vuelve n
                new string[]{ "a", "a" },
                new string[]{ "  ", " " },                  // solo espacios: la marca es cero por los dos lados
                new string[]{ "abc", "" },
            };
            int finosMal = 0;
            for (int i = 0; i < finos.Length; i++)
            {
                string ck = finos[i][0], qn = finos[i][1];
                string[] toks = (qn.IndexOf(' ') >= 0) ? qn.Split(' ') : null;
                int sc = punN(ck, qn, toks);
                bool atajado = (marca(ck) & marca(qn)) != marca(qn);
                if (atajado && sc > 0) { finosMal++; Console.WriteLine("     ESCONDIDA: \"" + ck + "\" / \"" + qn + "\""); }
            }
            Check(finosMal == 0, "los casos delicados (cifras, parentesis, barras, solo espacios) tampoco se esconden");
        }

        // ---------- 4) GUARDIA DE RENDIMIENTO ----------
        // Que nadie vuelva a meter, sin darse cuenta, lo que hacia lenta la busqueda. No se comprueban
        // milisegundos sueltos (esta maquina esta compartida y varian), sino la COMPARACION contra el
        // programa de antes medida en el mismo momento: eso si es estable.
        Console.WriteLine("\n  4) GUARDIA: LA BUSQUEDA NO PUEDE VOLVER A HACERSE LENTA\n");
        if (mMar != null)
        {
            Marca marca = (Marca)Delegate.CreateDelegate(typeof(Marca), mMar);
            string[] ix = IndiceGrande(20000);
            int[] masc = new int[ix.Length];
            for (int i = 0; i < ix.Length; i++) masc[i] = marca(ix[i]);

            string[] cons = { "a", "de", "depeche", "amor noche" };
            double[] minimo = { 1.5, 2.0, 3.0, 3.0 };   // cuantas veces MAS RAPIDO tiene que ir como poco
            int lentos = 0;
            Console.WriteLine(string.Format("     {0,-14} {1,9} {2,9} {3,8} {4,8}", "tecleado", "antes ms", "ahora ms", "gana", "minimo"));
            for (int c = 0; c < cons.Length; c++)
            {
                string q = (string)mNormN.Invoke(null, new object[] { cons[c] });
                string sq = " " + q;
                string[] toks = (q.IndexOf(' ') >= 0) ? q.Split(' ') : null;
                int qm = marca(q);

                int[] punt = new int[ix.Length];
                for (int i = 0; i < ix.Length; i++) punt[i] = punN(ix[i], q, toks);
                List<int> lm = new List<int>();
                for (int i = 0; i < ix.Length; i++) if (punt[i] > 0) lm.Add(i);
                int[] arr = lm.ToArray();
                Comparison<int> cmp = delegate (int x, int y)
                {
                    if (punt[y] != punt[x]) return punt[y] - punt[x];
                    return string.Compare(ix[x], ix[y], StringComparison.OrdinalIgnoreCase);
                };

                double tViejo = Rapido(delegate
                {
                    for (int i = 0; i < ix.Length; i++) punV(ix[i], q, sq, toks);
                    int[] copia = (int[])arr.Clone(); Array.Sort(copia, cmp);
                });
                double tNuevo = Rapido(delegate
                {
                    for (int i = 0; i < ix.Length; i++) { if ((masc[i] & qm) != qm) continue; punN(ix[i], q, toks); }
                    Mejores40(arr, cmp);
                });
                double gana = tViejo / Math.Max(tNuevo, 0.0001);
                bool ok = gana >= minimo[c];
                if (!ok) lentos++;
                Console.WriteLine(string.Format("     \"{0,-12}\" {1,9:0.00} {2,9:0.00} {3,7:0.0}x {4,7:0.0}x  {5}",
                                                cons[c], tViejo, tNuevo, gana, minimo[c], ok ? "" : "<<< SE HA VUELTO LENTA"));
            }
            Check(lentos == 0, "con 20000 canciones cada tecla sigue costando mucho menos que antes");
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }

    delegate int Marca(string s);

    // El minimo de varias tandas: la maquina esta compartida y una tanda suelta puede salir el triple de lenta.
    static double Rapido(Action a)
    {
        a(); a();
        double mejor = double.MaxValue;
        for (int t = 0; t < 7; t++)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10; i++) a();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / 10.0;
            if (ms < mejor) mejor = ms;
        }
        return mejor;
    }

    static void Mejores40(int[] arr, Comparison<int> cmp)
    {
        const int N = 40;
        int[] top = new int[N]; int n = 0;
        for (int i = 0; i < arr.Length; i++)
        {
            int v = arr[i];
            if (n < N) { int j = n++; while (j > 0 && cmp(top[j - 1], v) > 0) { top[j] = top[j - 1]; j--; } top[j] = v; }
            else if (cmp(v, top[N - 1]) < 0) { int j = N - 1; while (j > 0 && cmp(top[j - 1], v) > 0) { top[j] = top[j - 1]; j--; } top[j] = v; }
        }
    }

    // Una biblioteca inventada con la forma de una de verdad: artistas con varios discos y sus canciones.
    static string[] IndiceGrande(int canciones)
    {
        string[] art = { "Depeche Mode", "Alejandro Fernandez", "Los Angeles Azules", "Metallica", "Jose Jose",
                         "Vicente Fernandez", "Grupo Firme", "Marco Antonio Solis", "Bronco", "Los Tigres del Norte",
                         "Juan Gabriel", "Cafe Tacvba", "Mana", "Soda Stereo", "Los Bukis", "Banda MS",
                         "Christian Nodal", "Camila", "Reik", "La Arrolladora" };
        string[] pal = { "Amor", "Noche", "Corazon", "Vida", "Cielo", "Fuego", "Luna", "Alma", "Sombra", "Camino",
                         "Silencio", "Tiempo", "Lluvia", "Estrella", "Viento", "Fiesta", "Rosa", "Mar", "Sol", "Beso" };
        List<string> ix = new List<string>();
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

    static void Compara(Viejo pv, Nuevo pn, string cand, string cons, out int a, out int b)
    {
        string qv = (string)mNormV.Invoke(null, new object[] { cons });
        string qn = (string)mNormN.Invoke(null, new object[] { cons });
        if (qv != qn) { a = -1; b = -2; return; }   // ni siquiera limpian igual la consulta
        string[] toks = (qn.IndexOf(' ') >= 0) ? qn.Split(' ') : null;
        a = pv(cand, qv, " " + qv, toks);
        b = pn(cand, qn, toks);
    }
}
