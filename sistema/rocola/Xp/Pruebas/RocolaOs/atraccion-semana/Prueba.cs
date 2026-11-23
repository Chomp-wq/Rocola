// La prueba de la logica de la cuenta de la semana (RocolaOs/RocolaOs.AtraccionSemanaLogica.cs): ventana de 7 dias con el corte de las 8 de la
// manana, descarte de dias viejos, suma, desempate y el formato del archivo. Nada de Form1 ni de reloj.
using System; using System.Collections.Generic; using System.Globalization; using Rocola;
static class P
{
    static int bien, mal;
    static void Ok(string n, bool c) { if (c) bien++; else mal++; Console.WriteLine((c ? "BIEN  " : "MAL   ") + n); }
    static DateTime T(string s) { return DateTime.ParseExact(s, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture); }
    static int Main()
    {
        // el corte de las 8
        Ok("07:59 es el dia de ayer", AtraccionSemanaDatos.ClaveDia(T("2026-11-23 07:59")) == "2026-11-22");
        Ok("08:00 ya es de hoy", AtraccionSemanaDatos.ClaveDia(T("2026-11-23 08:00")) == "2026-11-23");
        Ok("01:00 de madrugada es de ayer", AtraccionSemanaDatos.ClaveDia(T("2026-01-01 01:00")) == "2025-12-31");
        List<string> v = AtraccionSemanaDatos.Ventana("2026-11-23");
        Ok("la ventana tiene 7 dias, de hoy a hoy-6", v.Count == 7 && v[0] == "2026-11-23" && v[6] == "2026-11-17");
        Ok("la ventana cruza el fin de mes", AtraccionSemanaDatos.Ventana("2026-12-02")[6] == "2026-11-26");
        Ok("hoy raro: ventana vacia", AtraccionSemanaDatos.Ventana("xx").Count == 0);

        AtraccionSemanaDatos s = new AtraccionSemanaDatos();
        string hoy = "2026-11-23";
        s.Cuenta("/m/a.mp3", "2026-11-17"); s.Cuenta("/m/a.mp3", "2026-11-19"); s.Cuenta("/m/b.mp3", "2026-11-19"); s.Cuenta("/m/b.mp3", "2026-11-23");
        s.Cuenta("/m/c.mp3", "2026-11-23"); s.Cuenta("/m/Viejo.mp3", "2026-11-16"); s.Cuenta("/m/viejo.mp3", "2026-11-10");
        Ok("no cuenta ruta vacia", !s.Cuenta("", hoy) && !s.Cuenta(null, hoy));
        Dictionary<string, int> suma = s.Suma(hoy);
        Ok("suma de los 7 dias (a=2, b=2, c=1; el de hace 7 dias no cuenta)", suma["/m/a.mp3"] == 2 && suma["/m/b.mp3"] == 2 && suma["/m/c.mp3"] == 1 && !suma.ContainsKey("/m/Viejo.mp3"));
        // el desempate: a y b empatan en 2; gana la pedida mas tarde (b: 23-11)
        s.Cuenta("/m/c.mp3", hoy);   // c=2 y la ultima pedida
        suma = s.Suma(hoy);
        List<string> top = Mas(suma, s.Orden, 3);
        Ok("orden: empatadas, la pedida mas tarde primero (c, b, a)", top[0] == "/m/c.mp3" && top[1] == "/m/b.mp3" && top[2] == "/m/a.mp3");
        s.Cuenta("/m/a.mp3", hoy); suma = s.Suma(hoy);
        Ok("a con 3 va primero", Mas(suma, s.Orden, 1)[0] == "/m/a.mp3" && suma["/m/a.mp3"] == 3);
        // el tope por dia
        AtraccionSemanaDatos t = new AtraccionSemanaDatos();
        for (int i = 0; i < 450; i++) t.Cuenta("/r/" + i, hoy);
        Ok("tope de 400 rutas distintas por dia", t.PorDia[hoy].Count == 400 && !t.Cuenta("/r/449", hoy) && t.Cuenta("/r/3", hoy));
        // poda de dias viejos
        Ok("poda quita el dia viejo", s.Poda("2026-11-24") && !s.PorDia.ContainsKey("2026-11-17") && s.Poda("2026-11-24") == false);
        Ok("la ruta que solo estaba en el dia podado sale del orden", !s.Orden.ContainsKey("/m/Viejo.mp3") && s.Orden.ContainsKey("/m/a.mp3"));
        // el archivo
        string txt = s.Texto("2026-11-24");
        string[] ls = txt.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        Ok("el archivo empieza con semana=1 y lleva AAAA-MM-DD|veces|ruta", ls[0] == "semana=1" && ls[1].IndexOf('|') == 10 && ls.Length > 3);
        Ok("los dias van de el mas viejo al mas nuevo", string.CompareOrdinal(ls[1].Substring(0, 10), ls[ls.Length - 1].Substring(0, 10)) <= 0);
        AtraccionSemanaDatos r = new AtraccionSemanaDatos();
        Ok("leer lo escrito", r.Carga(ls, "2026-11-24"));
        Dictionary<string, int> s1 = s.Suma("2026-11-24"), s2 = r.Suma("2026-11-24");
        bool igual = s1.Count == s2.Count; foreach (KeyValuePair<string, int> kv in s1) { int n; igual &= s2.TryGetValue(kv.Key, out n) && n == kv.Value; }
        Ok("lo leido suma lo mismo", igual);
        Ok("lo leido conserva el desempate", Mas(s1, s.Orden, 5).ToArray().Length == Mas(s2, r.Orden, 5).ToArray().Length && string.Join(",", Mas(s1, s.Orden, 5)) == string.Join(",", Mas(s2, r.Orden, 5)));
        Ok("escribir otra vez da el mismo texto", r.Texto("2026-11-24") == txt);
        // al leer, los dias viejos se descartan y lo roto se salta
        AtraccionSemanaDatos q = new AtraccionSemanaDatos();
        q.Carga(new[] { "semana=1", "2026-11-01|5|/m/z.mp3", "2026-11-23|2|/m/y.mp3", "basura", "2026-11-23|0|/m/x.mp3", "2026-11-23|dos|/m/w.mp3", "2026-11-22|1|/m/con|barra.mp3", "2026-11-22|3|" }, hoy);
        Dictionary<string, int> sq = q.Suma(hoy);
        Ok("al leer: el dia viejo y las lineas rotas se descartan", sq.Count == 2 && sq["/m/y.mp3"] == 2 && sq["/m/con|barra.mp3"] == 1);
        Ok("sin la cabecera no es el formato", !new AtraccionSemanaDatos().Carga(new[] { "noche=2026-11-23", "2026-11-23|1|/a" }, hoy) && !new AtraccionSemanaDatos().Carga(null, hoy));
        // la siembra con lo de hoy
        AtraccionSemanaDatos g = new AtraccionSemanaDatos();
        g.Siembra(hoy, new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("/n/uno.mp3", 4), new KeyValuePair<string, int>("/n/dos.mp3", 1) });
        g.Cuenta("/n/dos.mp3", hoy);
        Dictionary<string, int> sg = g.Suma(hoy);
        Ok("siembra y suma encima", sg["/n/uno.mp3"] == 4 && sg["/n/dos.mp3"] == 2 && Mas(sg, g.Orden, 1)[0] == "/n/uno.mp3");
        Console.WriteLine(mal == 0 ? "TODO BIEN (" + bien + " comprobaciones)" : mal + " MAL de " + (bien + mal));
        return mal == 0 ? 0 : 1;
    }
    // el mismo orden que MasPedidasNoche de XP: mas veces primero y, a igualdad, la pedida mas tarde
    static List<string> Mas(Dictionary<string, int> c, Dictionary<string, long> o, int max)
    {
        List<string> ks = new List<string>(c.Keys);
        ks.Sort(delegate (string a, string b) { if (c[a] != c[b]) return c[b].CompareTo(c[a]); long x, y; o.TryGetValue(a, out x); o.TryGetValue(b, out y); return y.CompareTo(x); });
        if (ks.Count > max) ks.RemoveRange(max, ks.Count - max);
        return ks;
    }
}
