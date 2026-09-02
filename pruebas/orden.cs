using System;
using System.Collections.Generic;
using System.Reflection;

// MEDIR PRIMERO LO QUE LA GENTE PONE.
//
// La rocola mide cada cancion una vez -la descomprime entera para saber su volumen, su duracion y su
// huella tonal- y con eso nivela el volumen, escribe la duracion en la lista y puede decir "tu cancion
// suena en ~11 min". Medir un minuto de cancion cuesta 550 ms, o sea unos 2.2 segundos por cancion de
// cuatro minutos. Una biblioteca de 3000 canciones son casi dos horas de trabajo, y en la maquina del
// bar bastante mas.
//
// LO QUE PASABA: esa lista se recorria EN EL ORDEN EN QUE ESTA LA BIBLIOTECA. Primera cancion, segunda,
// tercera. Asi la rocola pasa noches enteras midiendo discos que nadie ha puesto nunca, mientras las
// cinco que suenan cada viernes siguen sin medir -y por tanto sin nivelar y sin decir los minutos-
// hasta que le toque el turno, que puede ser semanas despues.
//
// Y LA ROCOLA YA SABE CUALES SON. Lleva la cuenta de lo mas sonado para la lista de "Lo mas sonado" y
// sabe que hay en la cola ahora mismo. No hace falta medir mas ni distinto: hace falta medir en otro
// orden. El trabajo total es exactamente el mismo; lo que cambia es CUANDO llega el beneficio.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE EL ORDEN SEA UNA CUENTA APARTE. Recibe listas y devuelve el orden, sin tocar la biblioteca
//      ni la cola, asi se le pueden dar casos a mano.
//   2) QUE LO DE LA COLA VAYA PRIMERO, aunque no se haya puesto nunca: va a sonar en minutos.
//   3) QUE DESPUES MANDE LO MAS SONADO, y que empate por lo mas reciente.
//   4) QUE NO SE PIERDA NI SE REPITA NADA. Una cancion que se cayera de la lista no se mediria jamas.
//   5) QUE EL ORDEN SEA SIEMPRE EL MISMO. En .NET 3.5 List.Sort NO es estable: sin un desempate de
//      verdad, dos canciones igual de sonadas saldrian una vez en un orden y otra vez en otro, y el
//      pre-analisis empezaria por un sitio distinto cada vez que se enciende la rocola.
//   6) QUE LA ROCOLA LO USE de verdad, y que la cola llegue hasta alli.
class Orden
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF;
    static MethodInfo mOrden;

    static MethodBase Met(string n)
    {
        if (TF == null) return null;
        foreach (MethodInfo m in TF.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }

    // Llama a la cuenta con listas de mentira y devuelve como quedo el orden.
    static List<string> Ordena(string[] pend, string[] sonadas, int[] veces, string[] enCola)
    {
        List<string> lista = new List<string>(pend);
        Dictionary<string, int> dv = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, long> dr = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sonadas.Length; i++) { dv[sonadas[i]] = veces[i]; dr[sonadas[i]] = veces.Length - i; }
        HashSet<string> hc = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < enCola.Length; i++) hc.Add(enCola[i]);
        mOrden.Invoke(null, new object[] { lista, dv, dr, hc });
        return lista;
    }
    static string Junta(List<string> l) { return string.Join(" ", l.ToArray()); }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes()) if (t.Name == "Form1") TF = t;
        if (TF == null) { Console.WriteLine("  FALLO  no se encontro Form1"); return 1; }

        Console.WriteLine("\n  1) LA CUENTA, Y QUE SE PUEDA COMPROBAR\n");
        MethodBase o = Met("OrdenaPorUso"), tanda = Met("TandaPorMedir");
        Check(o != null, "el orden en que conviene medir es una cuenta aparte (OrdenaPorUso)");
        Check(tanda != null, "y sigue habiendo un solo sitio que arma la tanda (TandaPorMedir)");
        if (o == null) { Console.WriteLine("\n  " + fallos + " FALLO(S)"); return Math.Max(fallos, 1); }
        Check(o.IsStatic, "es pura: recibe las listas y devuelve el orden, sin tocar la biblioteca");
        mOrden = (MethodInfo)o;

        Console.WriteLine("\n  2) LO QUE ESTA EN LA COLA VA PRIMERO\n");
        // 'nadie' no se ha puesto nunca, pero esta en la cola: suena en minutos y hay que medirla YA.
        List<string> r = Ordena(new string[] { "vieja", "nadie", "reina" },
                                new string[] { "vieja", "reina" }, new int[] { 3, 40 },
                                new string[] { "nadie" });
        Console.WriteLine("     " + Junta(r));
        Check(r[0] == "nadie", "una cancion recien pedida se mide antes que la mas sonada del bar");

        Console.WriteLine("\n  3) Y DESPUES, LO MAS SONADO\n");
        r = Ordena(new string[] { "poca", "muchisima", "ninguna", "media" },
                   new string[] { "poca", "muchisima", "media" }, new int[] { 2, 90, 12 },
                   new string[0]);
        Console.WriteLine("     " + Junta(r));
        Check(r[0] == "muchisima" && r[1] == "media" && r[2] == "poca",
              "de mas sonada a menos sonada");
        Check(r[3] == "ninguna", "y la que nunca se ha puesto, al final");

        Console.WriteLine("\n  4) EMPATE A VECES: MANDA LA MAS RECIENTE\n");
        // Las tres se han puesto 5 veces. 'ayer' es la ultima que sono (la de recencia mas alta).
        List<string> l2 = new List<string>(new string[] { "hace_meses", "ayer", "hace_semanas" });
        Dictionary<string, int> dv = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, long> dr = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        dv["hace_meses"] = 5; dv["ayer"] = 5; dv["hace_semanas"] = 5;
        dr["hace_meses"] = 10; dr["ayer"] = 900; dr["hace_semanas"] = 300;
        mOrden.Invoke(null, new object[] { l2, dv, dr, new HashSet<string>(StringComparer.OrdinalIgnoreCase) });
        Console.WriteLine("     " + Junta(l2));
        Check(l2[0] == "ayer" && l2[1] == "hace_semanas" && l2[2] == "hace_meses",
              "igual de sonadas: primero la que sono hace menos");

        Console.WriteLine("\n  5) NO SE PIERDE NI SE REPITE NADA\n");
        // Una cancion que se cayera de la lista no se mediria JAMAS: ni nivelada, ni con duracion.
        string[] muchas = new string[400];
        for (int i = 0; i < muchas.Length; i++) muchas[i] = "c" + i;
        string[] son = new string[60]; int[] vec = new int[60];
        for (int i = 0; i < 60; i++) { son[i] = "c" + (i * 7 % 400); vec[i] = (i * 13) % 30; }
        List<string> g = Ordena(muchas, son, vec, new string[] { "c399", "c17" });
        Dictionary<string, int> cuenta = new Dictionary<string, int>();
        for (int i = 0; i < g.Count; i++) { int n; cuenta.TryGetValue(g[i], out n); cuenta[g[i]] = n + 1; }
        bool sobra = false, falta = (g.Count != muchas.Length);
        for (int i = 0; i < muchas.Length; i++) { int n; if (!cuenta.TryGetValue(muchas[i], out n) || n != 1) sobra = true; }
        Console.WriteLine("     entraron " + muchas.Length + " y salieron " + g.Count);
        Check(!falta && !sobra, "salen exactamente las mismas que entraron, una vez cada una");

        Console.WriteLine("\n  6) Y EL ORDEN ES SIEMPRE EL MISMO\n");
        // En .NET 3.5 List.Sort NO es estable. Sin un desempate de verdad, dos canciones con los mismos
        // numeros saldrian hoy en un orden y manana en otro, y el pre-analisis empezaria cada vez por un
        // sitio distinto: nunca terminaria de cubrir lo que importa.
        string[] iguales = new string[60];
        for (int i = 0; i < iguales.Length; i++) iguales[i] = "z" + i.ToString("00");
        List<string> a1 = Ordena(iguales, new string[0], new int[0], new string[0]);
        List<string> a2 = Ordena(iguales, new string[0], new int[0], new string[0]);
        bool mismo = (Junta(a1) == Junta(a2));
        bool comoEntro = (Junta(a1) == string.Join(" ", iguales));
        Check(mismo, "dos veces con los mismos datos dan el mismo orden");
        Check(comoEntro, "y sin nada que las distinga se quedan como estaban, sin revolverse");
        // Lo mismo con empates DENTRO de un grupo con numeros.
        List<string> b1 = Ordena(new string[] { "p", "q", "r", "s" },
                                 new string[] { "p", "q", "r", "s" }, new int[] { 7, 7, 7, 7 }, new string[0]);
        List<string> b2 = Ordena(new string[] { "p", "q", "r", "s" },
                                 new string[] { "p", "q", "r", "s" }, new int[] { 7, 7, 7, 7 }, new string[0]);
        Console.WriteLine("     cuatro empatadas a todo: " + Junta(b1));
        Check(Junta(b1) == Junta(b2), "y empatadas en todo, tambien");

        Console.WriteLine("\n  7) LA ROCOLA LO USA\n");
        if (tanda != null)
        {
            Check(Codigo.Llama(tanda, "OrdenaPorUso"), "la tanda por medir sale ordenada, no en el orden de la biblioteca");
            int lee, esc;
            Codigo.UsosDeCampo(tanda, "songCounts", out lee, out esc);
            Check(lee > 0, "y mira lo que la rocola ya sabe de lo mas sonado (songCounts)");
            Codigo.UsosDeCampo(tanda, "queue", out lee, out esc);
            Check(lee > 0, "y lo que hay pedido ahora mismo (queue)");
        }

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
