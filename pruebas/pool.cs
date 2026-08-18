using System;
using System.Collections.Generic;
using System.Diagnostics;

// EL AGRUPADOR DE RUTAS: string.Intern contra una tabla propia.
//
// La rocola comparte UNA sola copia de cada ruta entre todas sus tablas (sonoridad, duracion, EQ, cosecha...).
// Hoy lo hace con string.Intern, que es la tabla que el propio .NET usa para los textos del programa. Tiene
// dos pegas: toma un candado GLOBAL en cada llamada, y lo que entra ahi NO SE LIBERA JAMAS mientras el
// programa viva. Aqui se compara con una tabla propia, que es lo mismo sin las dos pegas.
class PoolBench
{
    static string Ruta(int i)
    {
        return @"D:\Musica\" + (char)('A' + (i % 26)) + @"\Artista " + (i % 900)
             + @" - Album numero " + (i % 120) + @"\" + (i % 30 + 1).ToString("00")
             + " - Cancion con nombre de largo normal " + i + ".mp3";
    }

    static int Main()
    {
        int N = 50000;
        string[] rutas = new string[N];
        for (int i = 0; i < N; i++) rutas[i] = Ruta(i);
        // segunda tanda de textos IGUALES pero objetos distintos (como salen de leer un archivo)
        string[] otra = new string[N];
        for (int i = 0; i < N; i++) otra[i] = Ruta(i);

        Console.WriteLine("\n  AGRUPAR " + N.ToString("#,0") + " RUTAS (lo que se hace al arrancar, por cada tabla)\n");

        double mejorI = double.MaxValue, mejorD = double.MaxValue;
        for (int t = 0; t < 5; t++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < N; i++) { string s = string.Intern(otra[i]); if (s == null) return 1; }
            sw.Stop();
            if (sw.Elapsed.TotalMilliseconds < mejorI) mejorI = sw.Elapsed.TotalMilliseconds;

            Dictionary<string, string> tabla = new Dictionary<string, string>(N, StringComparer.Ordinal);
            for (int i = 0; i < N; i++) tabla[rutas[i]] = rutas[i];
            sw = Stopwatch.StartNew();
            for (int i = 0; i < N; i++) { string s; if (!tabla.TryGetValue(otra[i], out s)) tabla[otra[i]] = otra[i]; }
            sw.Stop();
            if (sw.Elapsed.TotalMilliseconds < mejorD) mejorD = sw.Elapsed.TotalMilliseconds;
        }
        Console.WriteLine("     con string.Intern (lo de hoy) : " + mejorI.ToString("0.0") + " ms");
        Console.WriteLine("     con una tabla propia          : " + mejorD.ToString("0.0") + " ms   ("
                          + (mejorI / Math.Max(mejorD, 0.001)).ToString("0.0") + " veces mas rapido)");
        Console.WriteLine();
        Console.WriteLine("     (y esto se hace UNA VEZ POR TABLA al arrancar: sonoridad, duracion, EQ, cosecha,");
        Console.WriteLine("      repaso a fondo y la propia lista de canciones)");
        return 0;
    }
}
