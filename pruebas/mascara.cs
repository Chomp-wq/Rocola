using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

// PRUEBA DE LABORATORIO: merece la pena un pre-filtro por letras antes de mirar el texto entero?
//
// La idea: de cada entrada del indice se guarda, al construirlo, un numero de 32 bits que dice QUE LETRAS
// contiene ("Metallica" contiene m,e,t,a,l,i,c). Cuando el cliente teclea "amor noche", esa consulta usa las
// letras a,m,o,r,n,c,h,e. Una entrada que no tenga TODAS esas letras no puede contener lo tecleado, ni
// entero ni por palabras, asi que se descarta con una sola comparacion de numeros en vez de recorrerle el
// texto tres veces.
//
// Esto NO se toca en el programa hasta ver aqui que de verdad ahorra tiempo.
class Mascara
{
    delegate int Nuevo(string cand, string q, string[] toks);

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

    // Bit 0..25 = letras a..z ; bit 26 = cualquier cifra ; bit 27 = cualquier otra cosa (guiones, etc).
    // Los espacios no cuentan. Meter todo lo raro en un mismo bit es a proposito: nunca descarta de mas.
    static int Masc(string s)
    {
        int m = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == ' ') continue;
            if (c >= 'a' && c <= 'z') m |= 1 << (c - 'a');
            else if (c >= '0' && c <= '9') m |= 1 << 26;
            else m |= 1 << 27;
        }
        return m;
    }

    static string[] Indice(int canciones)
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

    static int Main()
    {
        Assembly aN = Assembly.LoadFrom("rocola.exe");
        Type tN = aN.GetType("Rocola.Form1", true);
        Nuevo punN = (Nuevo)Delegate.CreateDelegate(typeof(Nuevo), tN.GetMethod("Score", BindingFlags.Static | BindingFlags.NonPublic));
        MethodInfo mNorm = tN.GetMethod("Normalize", BindingFlags.Static | BindingFlags.NonPublic);

        string[] ix = Indice(20000);
        int[] masc = new int[ix.Length];
        double msConstruir = Mide(3, delegate { for (int i = 0; i < ix.Length; i++) masc[i] = Masc(ix[i]); });
        Console.WriteLine("\n  indice de " + ix.Length + " entradas");
        Console.WriteLine("  calcular las marcas de letras al construir el indice: " + msConstruir.ToString("0.0") + " ms UNA vez (va en segundo plano)\n");

        string[] consultas = { "a", "de", "dep", "depeche", "amor", "amor noche", "metallica enter", "silencio", "juan gabriel amor", "zxq" };
        Console.WriteLine(string.Format("  {0,-20} {1,7} | {2,8} {3,8} | {4,9} {5,9}", "tecleado", "casan", "filtra", "c/marca", "descarta%", "gana"));
        Console.WriteLine("  " + new string('-', 74));
        int malos = 0;
        foreach (string cons in consultas)
        {
            string q = (string)mNorm.Invoke(null, new object[] { cons });
            string[] toks = (q.IndexOf(' ') >= 0) ? q.Split(' ') : null;
            int qm = Masc(q);

            int casan = 0, saltados = 0;
            for (int i = 0; i < ix.Length; i++)
            {
                bool salta = (masc[i] & qm) != qm;
                int sc = punN(ix[i], q, toks);
                if (salta && sc > 0) malos++;    // el pre-filtro NUNCA puede tirar algo que si casaba
                if (salta) saltados++;
                if (sc > 0) casan++;
            }

            double sinMarca = Mide(20, delegate { for (int i = 0; i < ix.Length; i++) punN(ix[i], q, toks); });
            double conMarca = Mide(20, delegate
            {
                for (int i = 0; i < ix.Length; i++) { if ((masc[i] & qm) != qm) continue; punN(ix[i], q, toks); }
            });
            Console.WriteLine(string.Format("  \"{0,-18}\" {1,7} | {2,8:0.00} {3,8:0.00} | {4,8:0.0}% {5,8:0.0}x",
                                            cons, casan, sinMarca, conMarca, 100.0 * saltados / ix.Length, sinMarca / Math.Max(conMarca, 0.0001)));
        }
        Console.WriteLine("\n  entradas descartadas por error (tenian que casar y el pre-filtro las tiro): " + malos);
        Console.WriteLine(malos == 0 ? "  el pre-filtro no pierde ni un resultado\n" : "  EL PRE-FILTRO PIERDE RESULTADOS\n");
        return malos;
    }
}
