// Contratos del buscador real por reflexion. Sin musica, HAL, UI abierta ni datos del usuario.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

class BuscaInteligente
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Type form, motor, item, album, track, view;
    static int checks;
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static object Get(object x, string n) { return x.GetType().GetField(n, B).GetValue(x); }
    static void Set(object x, string n, object v) { x.GetType().GetField(n, B).SetValue(x, v); }
    static object Call(Type t, object x, string n, params object[] args) { return t.GetMethod(n, B).Invoke(x, args); }
    static object ListOf(Type t) { return Activator.CreateInstance(typeof(List<>).MakeGenericType(t)); }
    static object Owner()
    {
        object x = FormatterServices.GetUninitializedObject(form);
        Set(x, "searchIndex", ListOf(item)); Set(x, "albums", ListOf(album));
        Set(x, "bolsaLetra", Activator.CreateInstance(form.GetField("bolsaLetra", B).FieldType));
        Set(x, "searchOverlay", Activator.CreateInstance(view, true));
        return x;
    }
    static void Disc(object x, string artist, string name, params string[] songs)
    {
        object a = Activator.CreateInstance(album, true);
        Set(a, "Artist", artist); Set(a, "Name", name); Set(a, "Folder", name);
        foreach (string title in songs)
        {
            object t = Activator.CreateInstance(track, true);
            Set(t, "Artist", artist); Set(t, "Title", title); Set(t, "Path", title + ".mp3");
            ((IList)Get(a, "Tracks")).Add(t);
        }
        ((IList)Get(x, "albums")).Add(a);
    }
    static string Search(object x, string q, bool fresh)
    {
        if (fresh) { Call(form, x, "ClearRecentQueries"); Set(x, "lastQuery", null); Set(x, "lastMatches", null); }
        Call(form, x, "RecomputeResults", q);
        System.Text.StringBuilder s = new System.Text.StringBuilder();
        foreach (object hit in (IList)Get(Get(x, "searchOverlay"), "Results"))
            s.Append(Get(hit, "Display")).Append('|').Append(Get(hit, "Score")).Append(';');
        return s.ToString();
    }
    static string Norm(string s) { return (string)Call(motor, null, "Normaliza", s); }
    static string Naive(object x, string raw)
    {
        string q = Norm(raw);
        if (q.Length == 0) return "";
        string[] tokens = q.IndexOf(' ') >= 0 ? q.Split(' ') : null;
        List<object> matches = new List<object>();
        IList index = (IList)Get(x, "searchIndex");
        foreach (object hit in index)
        {
            string key = (string)Get(hit, "Key");
            int score = (int)Call(form, null, "Score", key, q, tokens);
            score = (int)Call(motor, null, "Relevancia", key, Get(hit, "TitleAt"), Get(hit, "TitleLength"), q, score);
            if (score > 0) { Set(hit, "Score", score); matches.Add(hit); }
        }
        if (matches.Count == 0 && (bool)Call(motor, null, "PermiteErrata", q, tokens))
            foreach (object hit in index)
            {
                int score = (int)Call(motor, null, "Aproximado", Get(hit, "Key"), q, tokens);
                score = (int)Call(motor, null, "RelevanciaAproximada", Get(hit, "Key"), Get(hit, "TitleAt"), Get(hit, "TitleLength"), q, tokens, score);
                if (score > 0) { Set(hit, "Score", score); matches.Add(hit); }
            }
        matches.Sort(delegate(object a, object b) {
            int order = (int)Get(b, "Score") - (int)Get(a, "Score");
            if (order == 0) order = (int)Get(a, "Kind") - (int)Get(b, "Kind");
            return order == 0 ? String.Compare((string)Get(a, "Display"), (string)Get(b, "Display"), StringComparison.OrdinalIgnoreCase) : order;
        });
        System.Text.StringBuilder s = new System.Text.StringBuilder();
        for (int i = 0; i < Math.Min(40, matches.Count); i++) s.Append(Get(matches[i], "Display")).Append('|').Append(Get(matches[i], "Score")).Append(';');
        return s.ToString();
    }
    static int Distance(string a, string b)
    {
        int[,] d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++) for (int j = 1; j <= b.Length; j++)
        {
            d[i, j] = Math.Min(d[i - 1, j] + 1, Math.Min(d[i, j - 1] + 1, d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1)));
            if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
        }
        return d[a.Length, b.Length];
    }
    static void Words(List<string> words, string prefix, int remaining)
    {
        words.Add(prefix);
        if (remaining == 0) return;
        for (char c = 'a'; c <= 'c'; c++) Words(words, prefix + c, remaining - 1);
    }
    static void Pure()
    {
        string[,] cases = { { "  CAFÉ\tTacvba -- CORAZÓN  ", "cafe tacvba corazon" },
            { "Don't Stop", "dont stop" }, { "Don’t   Stop", "dont stop" },
            { "O'Connor", "oconnor" }, { "AC/DC", "ac dc" }, { "  ", "" },
            { "José—José", "jose jose" }, { "Precious (Deluxe)", "precious deluxe" },
            { "ИСТОРИЯ", "история" }, { "音楽", "音楽" }, { "2000", "2000" },
            { "cafe\u0301", "cafe" }, { "a\u00a0b", "a b" } };
        for (int i = 0; i < cases.GetLength(0); i++) Check(Norm(cases[i, 0]) == cases[i, 1], "Normalizacion " + cases[i, 0]);
        Check(Object.ReferenceEquals("amor noche", Norm("amor noche")), "La clave sencilla se reutiliza");
        Check(Norm(null) == "", "Consulta nula");
        Check((int)Call(form, null, "Score", new string('x', 5000) + " precious", "precious", null) > 0,
            "Una coincidencia lejana en metadatos largos nunca tiene puntuacion negativa");
        Check(Norm("ab\ud800cd") == "ab cd", "Surrogate roto no bloquea el buscador");
        List<string> words = new List<string>(); Words(words, "", 4);
        foreach (string a in words) foreach (string b in words)
            Check((bool)Call(motor, null, "UnaErrata", "_" + a + "_", 1, a.Length, b) == (Distance(a, b) <= 1), "Errata " + a + "/" + b);
        Check(!(bool)Call(motor, null, "PermiteErrata", "ab", null), "No aproximar consultas cortas");
        Check(!(bool)Call(motor, null, "PermiteErrata", new string('a', 129), null), "Consulta enorme no activa aproximacion");
    }
    static void Actual()
    {
        object x = Owner();
        try
        {
            Disc(x, "Depeche Mode", "Ultra", "03 - Dreamer", "Enjoy the Silence", "Precious", "Don't Say You Love Me", "Love In Itself");
            Disc(x, "Dreamer Orchestra", "Dreamer", "Dreamer live");
            Disc(x, "Precious Voices", "Best Of", "Something Else");
            Disc(x, new string('x', 5000), "Long tags", "Distant Song");
            Disc(x, "José José", "Secretos", "El amor acaba", "Almohada");
            Disc(x, "AC/DC", "Back In Black", "You Shook Me All Night Long");
            Disc(x, "O’Connor", "Historia", "Nothing Compares 2 U");
            Disc(x, "love", "Phrase test", "i love", "love", "love-italy", "love in itself");
            Call(form, x, "BuildSearchIndex");
            string[] queries = { "dreamer", "enjoy the silence", "depeche precious", "precious depeche", "  JOSE  JOSE ", "AC/DC", "dont say", "o'connor", "precios", "preciuos", "preccious", "depehce precious", "zzzz", "lov", "love ", "love i", "love in", "love in i", "", "---", "2000", "chomps20" };
            Dictionary<string, string> expected = new Dictionary<string, string>();
            foreach (string q in queries) expected[q] = Search(x, q, true);
            Check(expected["dreamer"].IndexOf("Depeche Mode - 03 - Dreamer", StringComparison.Ordinal) < expected["dreamer"].IndexOf("Dreamer Orchestra|", StringComparison.Ordinal), "Titulo exacto antes que prefijo del artista");
            Check(expected["dreamer"].Contains("Dreamer Orchestra - Dreamer"), "Album exacto tambien aparece");
            Check(Search(x, "Distant Song", true).Contains("Distant Song|12000"), "Titulo exacto no depende del largo del artista");
            string shortQuery = Search(x, "dr", true);
            Check(shortQuery.IndexOf("Dreamer Orchestra|", StringComparison.Ordinal) < shortQuery.IndexOf("Depeche Mode - 03 - Dreamer", StringComparison.Ordinal),
                "Una/dos letras no entierran al artista entre titulos genericos");
            Check(expected["precious depeche"].Contains("Depeche Mode - Precious"), "Palabras en cualquier orden");
            Check(expected["  JOSE  JOSE "].Contains("José José"), "Acentos y espacios");
            Check(expected["AC/DC"].Contains("AC/DC"), "Signos del artista");
            Check(Search(x, "ac dc", true) == expected["AC/DC"], "Separadores equivalentes");
            Check(expected["dont say"].Contains("Don't Say You Love Me"), "Apostrofe no obligatorio");
            foreach (string q in new string[] { "precios", "preciuos", "preccious", "depehce precious" })
                Check(expected[q].Contains("Depeche Mode - Precious"), "Errata recuperada " + q);
            Check(expected["preccious"].IndexOf("Depeche Mode - Precious|", StringComparison.Ordinal) < expected["preccious"].IndexOf("Precious Voices|", StringComparison.Ordinal),
                "Titulo completo corregido antes que una palabra del artista");
            Search(x, "precious", true);
            Check(!(bool)Get(x, "lastMatchesApproximate"), "Con coincidencias literales no se agregan aproximaciones");
            Check(expected["zzzz"] == "", "No inventar coincidencias lejanas");
            Check(expected["love i"].Contains("love - i love"), "Caso que se perdia al pasar de love+espacio a love i");
            for (int pass = 0; pass < 4; pass++) foreach (string q in queries)
                Check(Search(x, q, false) == expected[q], "Cache/escribir/borrar " + q);
            string[] incremental = { "l", "lo", "lov", "love ", "love i", "love in", "love in i", "love in itself", "love in", "love i", "love ", "lov", "lo", "l", "" };
            foreach (string q in incremental)
            {
                string cached = Search(x, q, false);
                Check(cached == Search(x, q, true), "Incremental vs indice completo " + q);
            }
            foreach (string word in new string[] { "precious", "depeche", "almohada", "dreamer", "connor", "silence", "itself", "jose", "zzzz", "love" })
                for (int p = 0; p < word.Length; p++)
                    foreach (string variant in new string[] { word.Remove(p, 1), word.Insert(p, "z"), word.Substring(0, p) + "z" + word.Substring(p + 1),
                        p + 1 < word.Length ? word.Substring(0, p) + word[p + 1] + word[p] + word.Substring(p + 2) : word })
                    {
                        string actual = Search(x, variant, false);
                        Check(actual == Naive(x, variant), "Motor vs referencia sin cache, mascaras ni postings: " + variant);
                    }
            Check(Search(x, "precouis", true) == "", "Dos erratas no se aceptan como una");
            Search(x, "precios", true); // sus aproximaciones no se pueden usar para filtrar una nueva consulta literal
            Check(Search(x, "preciosa", false) == Search(x, "preciosa", true), "Erratas no contaminan el subconjunto");
            Search(x, "precious", true);
            Set(x, "libBuilding", true); Search(x, "amor", false);
            Check(Get(x, "lastMatches") == null && Get(x, "lastQuery") == null, "Durante reescaneo no queda resultado anterior");
            FieldInfo total = view.GetField("TotalResultados", B);
            if (total != null) Check((int)total.GetValue(Get(x, "searchOverlay")) == 0, "Contador tambien se vacia durante reescaneo");
            Set(x, "libBuilding", false);
            ((IList)Get(x, "albums")).Clear(); Call(form, x, "BuildSearchIndex");
            Check(Search(x, "precious", false) == "", "Indice reconstruido sin cache vieja");
        }
        finally { ((Control)Get(x, "searchOverlay")).Dispose(); }
    }
    static void Large()
    {
        object x = Owner();
        try
        {
            for (int i = 0; i < 6000; i++) Disc(x, "Depeche Mode", "Album " + i,
                "Enjoy the Silence " + i, "Precious " + i, "Love In Itself " + i,
                "Nothing Compares " + i, "Almohada " + i, "Dreamer " + i,
                "Amor Noche " + i, "Corazon " + i, "Historia " + i, "Never Let Me Down Again " + i);
            Call(form, x, "BuildSearchIndex");
            string[] queries = { "preccious", "depehce precious", "mode precious", "cafe", "love ", "love i", "enjoy silence", "999", "zzzz" };
            foreach (string q in queries)
            {
                string a = Search(x, q, true); Check(Search(x, q, false) == a, "Grande repetida " + q);
            }
            // Una lista recortada por letra/postings nunca debe cambiar el resultado completo.
            string exact = Search(x, "mode precious", true);
            Set(x, "searchPostings", null);
            Check(Search(x, "mode precious", true) == exact, "Postings no pierden resultados");
            foreach (char c in "abcdefghijklmnopqrstuvwxyz0123456789") Search(x, c.ToString(), false);
            IDictionary cache = (IDictionary)Get(x, "bolsaLetra");
            int retained = 0;
            foreach (object bag in cache.Values) retained += ((IList)Get(bag, "Lista")).Count;
            Check(cache.Count <= 16 && retained <= 262144, "Letras con presupuesto agregado");
            foreach (object bag in cache.Values)
                Check(((int[])Get(bag, "Puntos")).Length == ((IList)Get(bag, "Mejores")).Count && ((int[])Get(bag, "Puntos")).Length <= 40,
                    "Se guardan puntos de solo 40, no decenas de miles");
            for (int i = 0; i < 40; i++) Search(x, "album " + i, false);
            Check(((IList)Get(x, "recentQueries")).Count <= 8 && (int)Get(x, "recentQueryItems") <= 65536, "Consultas recientes con limite");
            Search(x, "precious", true);
            IList hits = (IList)Get(Get(x, "searchOverlay"), "Results");
            string before = Search(x, "precious", false);
            foreach (object hit in (IList)Get(x, "searchIndex")) Set(hit, "Score", -98765);
            hits.Clear();
            Check(Search(x, "precious", false) == before, "Top y sus puntos sobreviven a cambios ajenos");
            // Medicion sintetica: no equivale a FPS ni certifica CPUs antiguas.
            foreach (string q in new string[] { "p", "precious", "depehce precious", "zzzz" })
            {
                double cold = 0, warm = 0;
                for (int r = 0; r < 7; r++)
                {
                    Stopwatch w = Stopwatch.StartNew(); Search(x, q, true); w.Stop(); cold += w.Elapsed.TotalMilliseconds;
                    w.Reset(); w.Start(); for (int j = 0; j < 100; j++) Search(x, q, false); w.Stop(); warm += w.Elapsed.TotalMilliseconds / 100;
                }
                Console.WriteLine("SINTETICO " + ((IList)Get(x, "searchIndex")).Count + " entradas / " + q + ": fria " + (cold / 7).ToString("F3") + " ms; cache " + (warm / 7).ToString("F3") + " ms (incluye reflexion y snapshot de texto de la prueba)");
            }
        }
        finally { ((Control)Get(x, "searchOverlay")).Dispose(); }
    }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            Assembly a = Assembly.LoadFrom(args[0]);
            if (args.Length > 1 && args[1] == "--dos-hilos")
            {
                long mask = Process.GetCurrentProcess().ProcessorAffinity.ToInt64();
                int logical = 0;
                while (mask != 0) { logical++; mask &= mask - 1; }
                Check(logical > 0 && logical <= 2, "El proceso de prueba no heredo la afinidad de dos hilos");
                Console.WriteLine("Afinidad comprobada: " + logical + " procesadores logicos (no simula un CPU antiguo).");
            }
            form = a.GetType("Rocola.Form1", true); motor = a.GetType("Rocola.MotorBusqueda", true);
            item = form.GetNestedType("SearchItem", B); album = form.GetNestedType("Album", B);
            track = form.GetNestedType("Track", B); view = form.GetNestedType("SearchOverlay", B);
            Pure(); Actual(); Large();
            Console.WriteLine("PASS buscainteligente: " + checks + " comprobaciones. Sin FPS/HAL/XP real."); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
