using System;
using System.Collections.Generic;
using System.IO;

// EL ARCHIVO CON EL QUE SE COMPILA DE VERDAD EN VISUAL STUDIO.
//
// La rocola se compila de dos maneras distintas: la tanda de pruebas usa su propia lista de archivos
// (FUENTES= en todo.sh) y Visual Studio usa Rocola/Rocola.csproj. Las dos tienen que decir lo mismo, y
// nadie lo comprobaba: la tanda salia entera en verde mientras Visual Studio devolvia 28 errores porque al
// .csproj le faltaban dos archivos (D3D9Device.cs y WinSistema.cs).
//
// Y los errores no se parecen en nada a la causa: como la clase D3D9 esta PARTIDA en dos archivos, quitar
// uno no daba "falta un archivo" sino veinticuatro veces "D3D9 no contiene una definicion para Release".
// Por eso esto se comprueba aqui y no a ojo.
//
// Se comprueba en las dos direcciones, y ademas que ningun archivo de codigo se quede fuera EN SILENCIO:
// si en esta rama no se compila (el dispositivo de Direct3D en la de Linux, por ejemplo), que al menos
// este dicho por que en todo.sh.
class Proyecto
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    // todo.sh parte las ordenes largas con \ al final. Para poder mirar una orden entera hay que juntarlas.
    static List<string> LineasLogicas(string ruta)
    {
        List<string> o = new List<string>();
        string junto = null;
        foreach (string cruda in File.ReadAllLines(ruta))
        {
            string l = cruda;
            bool sigue = l.EndsWith("\\");
            if (sigue) l = l.Substring(0, l.Length - 1);
            junto = (junto == null) ? l : (junto + " " + l.Trim());
            if (!sigue) { o.Add(junto); junto = null; }
        }
        if (junto != null) o.Add(junto);
        return o;
    }

    // Las asignaciones sueltas del guion (NAUDIO="...", FUENTES="..."), para poder resolver los $NOMBRE.
    static Dictionary<string, string> Variables(List<string> ls)
    {
        Dictionary<string, string> v = new Dictionary<string, string>();
        foreach (string l in ls)
        {
            string t = l.Trim();
            int eq = t.IndexOf('=');
            if (eq <= 0 || eq + 1 >= t.Length || t[eq + 1] != '"') continue;
            string nom = t.Substring(0, eq);
            bool bien = true;
            foreach (char c in nom) if (!char.IsLetterOrDigit(c) && c != '_') { bien = false; break; }
            if (!bien || nom.Length == 0) continue;
            int fin = t.IndexOf('"', eq + 2);
            if (fin < 0) continue;
            string val = t.Substring(eq + 2, fin - eq - 2);
            // FUENTES="$FUENTES mas.cs" AGREGA a la lista: sin esto se perdian los archivos anadidos despues.
            if (v.ContainsKey(nom) && val.IndexOf("$" + nom) >= 0) v[nom] = val.Replace("$" + nom, v[nom]);
            else if (!v.ContainsKey(nom)) v[nom] = val;
        }
        return v;
    }

    static string Expande(string s, Dictionary<string, string> v)
    {
        for (int vuelta = 0; vuelta < 4 && s.IndexOf('$') >= 0; vuelta++)
            foreach (KeyValuePair<string, string> kv in v)
                s = s.Replace("$" + kv.Key, kv.Value);
        return s;
    }

    // De "$RAIZ/Rocola/Form1.cs" a "Form1.cs"; de "Properties\Cosa.cs" a "Properties/Cosa.cs".
    static string Suelto(string p)
    {
        p = p.Replace('\\', '/');
        int i = p.IndexOf("Rocola/");
        if (i >= 0) p = p.Substring(i + 7);
        return p;
    }

    // Los Compile Include="..." del .csproj, tal cual, en orden.
    static List<string> Trozos(string xml, string etiqueta)
    {
        List<string> o = new List<string>();
        string marca = "<" + etiqueta + " Include=\"";
        int i = 0;
        while ((i = xml.IndexOf(marca, i, StringComparison.Ordinal)) >= 0)
        {
            i += marca.Length;
            int fin = xml.IndexOf('"', i);
            if (fin < 0) break;
            o.Add(xml.Substring(i, fin - i));
            i = fin;
        }
        return o;
    }

    static bool Tiene(List<string> l, string q)
    {
        foreach (string s in l) if (string.Equals(s, q, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static string Lista(List<string> l) { return (l.Count == 0) ? "(ninguno)" : string.Join(", ", l.ToArray()); }

    static int Main()
    {
        string raiz = "..";
        string csprojRuta = Path.Combine(raiz, Path.Combine("Rocola", "Rocola.csproj"));
        string todoRuta = "todo.sh";
        Check(File.Exists(csprojRuta) && File.Exists(todoRuta), "estan el proyecto de Visual Studio y el guion de la tanda");
        if (!File.Exists(csprojRuta) || !File.Exists(todoRuta)) return 1;

        List<string> ls = LineasLogicas(todoRuta);
        Dictionary<string, string> vars = Variables(ls);
        vars["RAIZ"] = raiz;   // en el guion es "$(cd .. && pwd)"; aqui la raiz es la carpeta de arriba y punto
        string xml = File.ReadAllText(csprojRuta);

        // ---------- lo que compila la TANDA ----------
        List<string> tanda = new List<string>();
        string fuentes;
        if (vars.TryGetValue("FUENTES", out fuentes))
            foreach (string t in Expande(fuentes, vars).Split(' '))
                if (t.Trim().EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) tanda.Add(Suelto(t.Trim()));
        Check(tanda.Count > 0, "el guion de la tanda dice que archivos compila");

        // ---------- lo que compila VISUAL STUDIO ----------
        List<string> proy = new List<string>();
        foreach (string t in Trozos(xml, "Compile")) if (t.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) proy.Add(Suelto(t));
        Check(proy.Count > 0, "el proyecto de Visual Studio dice que archivos compila");

        Console.WriteLine("\n  1) LAS DOS LISTAS DE ARCHIVOS TIENEN QUE DECIR LO MISMO\n");
        Console.WriteLine("     la tanda compila  " + tanda.Count + ":  " + Lista(tanda));
        Console.WriteLine("     el proyecto lista " + proy.Count + ":  " + Lista(proy));

        // 1a) Todo lo que compila la tanda tiene que estar en el proyecto. Este es el que fallaba.
        List<string> faltan = new List<string>();
        foreach (string t in tanda) if (!Tiene(proy, t)) faltan.Add(t);
        if (faltan.Count > 0) Console.WriteLine("     AL PROYECTO LE FALTAN: " + Lista(faltan));
        Check(faltan.Count == 0, "el proyecto de Visual Studio compila TODO lo que compila la tanda");

        // 1b) Y al reves, menos lo de Properties\, que lo pone Visual Studio y la tanda no necesita.
        List<string> sobran = new List<string>();
        foreach (string p in proy)
            if (!p.StartsWith("Properties/", StringComparison.OrdinalIgnoreCase) && !Tiene(tanda, p)) sobran.Add(p);
        if (sobran.Count > 0) Console.WriteLine("     EL PROYECTO LISTA DE MAS: " + Lista(sobran));
        Check(sobran.Count == 0, "y no lista ninguno que la tanda no compile (Properties aparte)");

        // ---------- 2) NINGUN ARCHIVO SE QUEDA FUERA EN SILENCIO ----------
        // En cada rama hay archivos que existen pero no se compilan aqui (el dispositivo de Direct3D en la
        // rama de Linux). Eso vale, pero tiene que estar DICHO: si no se compila y nadie explica por que en
        // todo.sh, lo mas probable es que se haya olvidado.
        Console.WriteLine("\n  2) Y NINGUN ARCHIVO DE CODIGO SE QUEDA FUERA SIN QUE NADIE LO DIGA\n");
        string todoTxt = File.ReadAllText(todoRuta);
        List<string> mudos = new List<string>(), dichos = new List<string>();
        string[] hay = Directory.GetFiles(Path.Combine(raiz, "Rocola"), "*.cs");
        Array.Sort(hay, StringComparer.OrdinalIgnoreCase);
        foreach (string f in hay)
        {
            string n = Path.GetFileName(f);
            if (Tiene(tanda, n)) continue;
            if (todoTxt.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0) dichos.Add(n); else mudos.Add(n);
        }
        Console.WriteLine("     archivos .cs en Rocola/: " + hay.Length + ";  no se compilan aqui: " + (dichos.Count + mudos.Count));
        if (dichos.Count > 0) Console.WriteLine("       y esta dicho por que:  " + Lista(dichos));
        if (mudos.Count > 0) Console.WriteLine("       SIN EXPLICACION:       " + Lista(mudos));
        Check(mudos.Count == 0, "todo archivo que existe y no se compila aqui esta explicado en todo.sh");

        // ---------- 3) LAS BIBLIOTECAS DE FUERA, TAMBIEN EN LAS DOS ----------
        Console.WriteLine("\n  3) Y LAS BIBLIOTECAS DE FUERA ESTAN EN LOS DOS SITIOS\n");
        List<string> pideTanda = new List<string>();
        foreach (string l in ls)
        {
            if (l.IndexOf("-out:rocola_cpu.exe", StringComparison.Ordinal) < 0) continue;
            foreach (string t in Expande(l, vars).Split(' '))
            {
                string s = t.Trim();
                if (!s.StartsWith("-r:", StringComparison.Ordinal)) continue;
                s = s.Substring(3).Trim('"');
                s = Path.GetFileNameWithoutExtension(s);
                if (s.Length > 0 && !Tiene(pideTanda, s)) pideTanda.Add(s);
            }
        }
        List<string> tieneProy = new List<string>();
        foreach (string r in Trozos(xml, "Reference"))
        {
            int c = r.IndexOf(',');
            tieneProy.Add((c > 0 ? r.Substring(0, c) : r).Trim());
        }
        List<string> sinRef = new List<string>();
        foreach (string r in pideTanda) if (!Tiene(tieneProy, r)) sinRef.Add(r);
        Console.WriteLine("     la tanda pide:   " + Lista(pideTanda));
        if (sinRef.Count > 0) Console.WriteLine("     AL PROYECTO LE FALTAN: " + Lista(sinRef));
        Check(pideTanda.Count > 0, "se pudo leer que bibliotecas pide la tanda");
        Check(sinRef.Count == 0, "el proyecto de Visual Studio referencia todas las que pide la tanda");

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }
}
