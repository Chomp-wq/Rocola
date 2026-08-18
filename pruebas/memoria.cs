using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.Serialization;

// CUANTA MEMORIA GASTA LA ROCOLA CON UNA BIBLIOTECA GRANDE, Y EN QUE.
//
// La maquina del bar es un Windows XP de 32 bits: por mucho que tenga 4 GB de RAM, UN programa no puede
// pasar de ~2 GB, y mucho antes de eso el sistema empieza a tirar de disco. Aqui se arma una biblioteca de
// mentira del tamaño de una de verdad y se mide, parte por parte, donde se va la memoria.
class Memoria
{
    static Type TF, TTrack, TAlb;

    static long Mide()
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        return GC.GetTotalMemory(true);
    }
    static string MB(long b) { return (b / 1024.0 / 1024.0).ToString("0.0") + " MB"; }

    static string RutaFalsa(int i)
    {
        // Una ruta como las de verdad: unidad, dos o tres carpetas y el archivo. ~85 caracteres.
        return @"D:\Musica\" + (char)('A' + (i % 26)) + @"\Artista " + (i % 900)
             + @" - Album numero " + (i % 120) + @"\" + (i % 30 + 1).ToString("00")
             + " - Cancion con nombre de largo normal " + i + ".mp3";
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "Track") TTrack = t;
            if (t.Name == "Album") TAlb = t;
        }
        if (TTrack == null || TAlb == null) { Console.WriteLine("no encuentro Track/Album"); return 1; }

        int N = 50000;   // canciones
        int A = 3000;    // discos
        if (argv.Length > 1) N = int.Parse(argv[1]);
        Console.WriteLine("\n  BIBLIOTECA DE PRUEBA: " + N.ToString("#,0") + " canciones en " + A.ToString("#,0") + " discos\n");

        long b0 = Mide();

        // ---------- las rutas, una sola vez ----------
        string[] rutas = new string[N];
        for (int i = 0; i < N; i++) rutas[i] = RutaFalsa(i);
        long bRutas = Mide();
        Console.WriteLine("  {0,-46} {1,10}", "las rutas de los archivos (una copia)", MB(bRutas - b0));

        // ---------- la lista de canciones ----------
        FieldInfo fPath = TTrack.GetField("Path"), fArt = TTrack.GetField("Artist"),
                  fTit = TTrack.GetField("Title"), fAlb = TTrack.GetField("Album"), fGen = TTrack.GetField("Genre");
        IList tracks = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TTrack));
        for (int i = 0; i < N; i++)
        {
            object t = FormatterServices.GetUninitializedObject(TTrack);
            fPath.SetValue(t, rutas[i]);
            fArt.SetValue(t, "Artista " + (i % 900));
            fTit.SetValue(t, "Cancion con nombre de largo normal " + i);
            fAlb.SetValue(t, "Album numero " + (i % 120));
            fGen.SetValue(t, (i % 7 == 0) ? "Pop" : "");
            tracks.Add(t);
        }
        long bTracks = Mide();
        Console.WriteLine("  {0,-46} {1,10}", "la lista de canciones (fichas Track)", MB(bTracks - bRutas));

        // ---------- las tablas que van por RUTA ----------
        // Todas guardan la MISMA ruta como clave. Si cada una se queda con su propia copia del texto, se
        // paga el mismo camino tantas veces como tablas haya.
        Dictionary<string, float> loud = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, float> dur = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, float> durF = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, float[]> eq = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> ia = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> cuentas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, long> secu = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> gen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // (a) COMPARTIENDO la ruta (lo que hace hoy la rocola donde usa Pool)
        for (int i = 0; i < N; i++)
        {
            string r = rutas[i];
            loud[r] = 0.1f; dur[r] = 200f; durF[r] = 200f;
            eq[r] = new float[9]; ia[r] = 2;
        }
        long bComp = Mide();
        Console.WriteLine("  {0,-46} {1,10}", "5 tablas por ruta, COMPARTIENDO el texto", MB(bComp - bTracks));

        // (b) cada tabla con SU PROPIA copia del texto
        Dictionary<string, float> loud2 = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, float> dur2 = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, float> durF2 = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, float[]> eq2 = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> ia2 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < N; i++)
        {
            loud2[string.Copy(rutas[i])] = 0.1f;
            dur2[string.Copy(rutas[i])] = 200f;
            durF2[string.Copy(rutas[i])] = 200f;
            eq2[string.Copy(rutas[i])] = new float[9];
            ia2[string.Copy(rutas[i])] = 2;
        }
        long bCopia = Mide();
        Console.WriteLine("  {0,-46} {1,10}   <- lo que cuesta NO compartir", "las mismas 5, cada una con SU copia", MB(bCopia - bComp));

        // ---------- las tablas de estadisticas (van por ruta y NO estan compartidas) ----------
        for (int i = 0; i < N; i++)
        {
            string r = string.Copy(rutas[i]);   // como salen hoy de leer el archivo de estadisticas
            cuentas[r] = 1; secu[r] = i;
            if (i % 7 == 0) gen[r] = "Pop";
        }
        long bStats = Mide();
        Console.WriteLine("  {0,-46} {1,10}", "estadisticas (veces, recencia, genero)", MB(bStats - bCopia));

        // ---------- las portadas en memoria ----------
        Console.WriteLine();
        int cov = 220, px = 256;
        Console.WriteLine("  {0,-46} {1,10}", "cache de portadas (" + cov + " de " + px + "x" + px + ")",
                          MB((long)cov * px * px * 4));
        int celdas = 6 * 18, cw = 150, chh = 150;
        Console.WriteLine("  {0,-46} {1,10}", "casillas horneadas (" + celdas + " de ~" + cw + "x" + chh + ")",
                          MB((long)celdas * cw * chh * 4));
        Console.WriteLine("  {0,-46} {1,10}", "textura de interfaz a 1024x768 (x2)", MB(2L * 1024 * 768 * 4));

        Console.WriteLine("\n  ------------------------------------------------------------------------");
        long tablasComp = bComp - bTracks;
        long tablasCopia = bCopia - bComp;
        long stats = bStats - bCopia;
        Console.WriteLine("  {0,-46} {1,10}", "TOTAL de datos de biblioteca (como esta hoy)",
                          MB((bRutas - b0) + (bTracks - bRutas) + tablasComp + stats));
        Console.WriteLine("  {0,-46} {1,10}", "lo que se AHORRARIA compartiendo tambien stats",
                          MB(stats - stats * 3 / 10));
        Console.WriteLine();
        Console.WriteLine("  (las tablas de estadisticas guardan hoy su propia copia de cada ruta; compartirlas");
        Console.WriteLine("   con las demas es exactamente lo que ya se hace en las otras cinco)");
        return 0;
    }
}
