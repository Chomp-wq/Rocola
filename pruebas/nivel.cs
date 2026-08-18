using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;

// Nivelado de volumen: las canciones BAJITAS se suben hacia el objetivo, las que ya suenan fuerte no se
// bajan nunca, y cada etapa tiene su propio tope. Se acaba de subir el tope de las bajitas.
class Nivel
{
    static Type TF; static object F; static MethodInfo GAIN, TIERCAP;
    static Dictionary<string,float> loud;
    static void SetFld(string n, object v) { TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(F, v); }
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static float Rms(double db) { return (float)Math.Pow(10.0, db / 20.0); }
    static double Db(float g) { return 20.0 * Math.Log10(g); }
    static double GananciaDb(double rmsDb)
    {
        string p = "c" + rmsDb + ".mp3";
        loud[p] = Rms(rmsDb);
        return Db((float)GAIN.Invoke(F, new object[] { p }));
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        F = FormatterServices.GetUninitializedObject(TF);
        loud = new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
        SetFld("loudCache", loud);
        SetFld("normalizeOn", true);
        SetFld("normStrength", 1f);
        SetFld("normTarget", 0.26f);
        GAIN = TF.GetMethod("NormGainFor", BindingFlags.Instance|BindingFlags.NonPublic);
        TIERCAP = TF.GetMethod("TierCapDb", BindingFlags.Static|BindingFlags.NonPublic);
        if (GAIN == null || TIERCAP == null) { Console.WriteLine("faltan metodos"); return 1; }

        double[] etapas = { -34, -28, -23, -19, -15, -11 };
        string[] nom = { "ULTRA BAJA", "BAJA", "MEDIA-BAJA", "MEDIA", "ALTA", "MUY ALTA" };
        double[] antes = { 20, 16, 13, 10, 7, 4 };   // los topes que habia antes de este cambio
        Console.WriteLine("\n  TOPE DE SUBIDA POR ETAPA\n");
        Console.WriteLine("    etapa          nivel     antes    ahora    diferencia");
        bool subenBajas = true, casiIgualAltas = true, ordenado = true;
        double prevCap = 1e9;
        for (int i = 0; i < etapas.Length; i++)
        {
            double cap = (float)TIERCAP.Invoke(null, new object[] { Rms(etapas[i]) });
            double dif = cap - antes[i];
            Console.WriteLine(string.Format("    {0,-12} {1,6:0} dB   {2,5:0.0}   {3,6:0.0}     {4,6:+0.0;-0.0} dB", nom[i], etapas[i], antes[i], cap, dif));
            if (i <= 1 && dif < 2.0) subenBajas = false;          // las dos mas bajitas: subida de verdad
            if (i >= 4 && dif > 1.0) casiIgualAltas = false;      // las mas altas: casi nada
            if (cap > prevCap) ordenado = false;                  // nunca puede subirse mas a una fuerte que a una debil
            prevCap = cap;
        }
        Check(subenBajas, "las canciones bajitas se suben mas que antes");
        Check(casiIgualAltas, "las que ya suenan fuerte casi no cambian");
        Check(ordenado, "cuanto mas fuerte viene una cancion, menos se le sube (la escalera nunca se invierte)");

        Console.WriteLine("\n  GANANCIA REAL QUE RECIBE CADA CANCION (objetivo 26%, fuerza 100%)\n");
        double[] pruebas = { -40, -34, -30, -26, -22, -18, -14, -11, -8, -4 };
        double prev = 1e9; bool mono = true, nuncaBaja = true;
        foreach (double d in pruebas)
        {
            double g = GananciaDb(d);
            Console.WriteLine(string.Format("    cancion a {0,4:0} dB  ->  se le suben {1,5:0.0} dB   (queda en {2,5:0.0} dB)", d, g, d + g));
            if (g < -0.001) nuncaBaja = false;
            if (g > prev + 0.001) mono = false;
            prev = g;
        }
        Check(nuncaBaja, "a ninguna cancion se le BAJA el volumen (solo se sube el piso)");
        Check(mono, "cuanto mas bajita viene, mas se le sube");
        Check(GananciaDb(-40) > 20.0, "una grabacion muy floja ya recibe mas de 20 dB (antes se quedaba clavada en 20)");
        Check(GananciaDb(-4) < 0.001, "una que ya suena mas fuerte que el objetivo se queda como esta");

        // fuerza 0 y apagado: no se toca nada
        SetFld("normStrength", 0f);
        Check(Math.Abs(GananciaDb(-30)) < 0.001, "con la fuerza en 0 no se nivela nada");
        SetFld("normStrength", 1f); SetFld("normalizeOn", false);
        Check(Math.Abs(GananciaDb(-30)) < 0.001, "con el nivelado apagado no se toca ninguna cancion");
        SetFld("normalizeOn", true);
        Check(Math.Abs(Db((float)GAIN.Invoke(F, new object[] { "nunca_medida.mp3" }))) < 0.001, "una cancion aun sin medir suena a su volumen original");
        Check(Math.Abs(Db((float)GAIN.Invoke(F, new object[] { null }))) < 0.001, "ruta nula: no truena");

        // techo absoluto: por floja que sea, no se amplifica sin limite
        Check(GananciaDb(-70) <= 24.001, "por muda que venga una cancion, nunca se pasa del techo de seguridad");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
