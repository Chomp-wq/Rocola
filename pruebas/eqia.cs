using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

// El modelo del EQ automatico: que la rocola lea EXACTAMENTE el mismo archivo que escribe el entrenador
// y saque los MISMOS numeros, y que al usarlo no pueda desmadrar el EQ por muy convencido que este.
class EqIa
{
    static Type TF; static object F; static MethodInfo CORR, RECALC;
    static object RED;   // la instancia EqNet
    static Dictionary<string,float[]> cache;
    static void SetFld(string n, object v) { TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(F, v); }
    static object Fld(string n) { return TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).GetValue(F); }
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }
    static int CUT;

    static void Pon(string nom, float[] ocho, float corte)
    { float[] c = new float[12]; c[11] = 2f; for (int b = 0; b < 8; b++) c[b] = ocho[b]; c[CUT] = corte; cache[nom] = c; }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        F = FormatterServices.GetUninitializedObject(TF);
        CUT = (int)TF.GetField("EqCut", BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        FieldInfo fi = TF.GetField("EqNet", BindingFlags.Static|BindingFlags.NonPublic);
        if (fi == null) { Console.WriteLine("no existe EqNet"); return 1; }
        RED = fi.GetValue(null);
        Type TR = RED.GetType();
        MethodInfo load = TR.GetMethod("TryLoad", BindingFlags.Instance|BindingFlags.Public);
        MethodInfo pred = TR.GetMethod("Predict", BindingFlags.Instance|BindingFlags.Public);
        FieldInfo loaded = TR.GetField("Loaded", BindingFlags.Instance|BindingFlags.Public);

        // --- sin modelo: todo como siempre
        Check(!(bool)loaded.GetValue(RED), "sin archivo de modelo la rocola arranca sin el (usa el promedio)");

        string bin = "datos/rocola_eq_ia.bin";
        load.Invoke(RED, new object[] { bin });
        Check((bool)loaded.GetValue(RED), "carga el modelo que escribio el entrenador");

        // --- mismos numeros que numpy
        Console.WriteLine("\n  entrada                         rocola                        entrenador (numpy)");
        double peor = 0;
        foreach (string linea in File.ReadAllLines("datos/ref.txt"))
        {
            string[] p = linea.Split('|');
            if (p.Length != 2) continue;
            string[] xs = p[0].Trim().Split(' '), ys = p[1].Trim().Split(' ');
            float[] x = new float[5], y = new float[3];
            for (int i = 0; i < 5; i++) x[i] = float.Parse(xs[i], System.Globalization.CultureInfo.InvariantCulture);
            pred.Invoke(RED, new object[] { x, y });
            string a = "", b = "";
            for (int i = 0; i < 3; i++)
            {
                double refv = double.Parse(ys[i], System.Globalization.CultureInfo.InvariantCulture);
                peor = Math.Max(peor, Math.Abs(refv - y[i]));
                a += string.Format("{0,8:0.000}", y[i]); b += string.Format("{0,8:0.000}", refv);
            }
            Console.WriteLine("  " + p[0].Trim().PadRight(24) + a + "     " + b);
        }
        Console.WriteLine(string.Format("\n  peor diferencia: {0:0.0000} dB", peor));
        Check(peor < 0.002, "la rocola saca los MISMOS numeros que el entrenador (mismo modelo, misma cuenta)");

        // --- usarlo de verdad en la correccion
        cache = new Dictionary<string,float[]>(StringComparer.OrdinalIgnoreCase);
        SetFld("eqCache", cache); SetFld("eqAmount", 1f);
        SetFld("sbrOn", false); SetFld("sbrAllowed", false);
        RECALC = TF.GetMethod("RecomputeEqAvg", BindingFlags.Instance|BindingFlags.NonPublic);
        CORR = TF.GetMethod("EqCorrFor", BindingFlags.Instance|BindingFlags.NonPublic);
        float[] plana = { 0,0,0,0,0,0,0,0 };
        for (int i = 0; i < 15; i++) Pon("lib" + i + ".mp3", plana, 22050f);
        // una cancion con el cuerpo MUY brillante: la red deberia pedirle mas aire que el promedio
        float[] brillante = { 0, -1f, 0f, 0f, 3f, 5f, 0f, 0f };
        Pon("brillante.mp3", brillante, 22050f);
        RECALC.Invoke(F, null);
        float[] cb = (float[])CORR.Invoke(F, new object[] { "brillante.mp3" });
        Console.WriteLine(string.Format("\n  cancion de cuerpo brillante -> correccion aire {0:+0.0;-0.0} dB (el promedio pediria {1:+0.0;-0.0})", cb[7], 0f - brillante[7]));
        Check(cb != null, "con modelo cargado la correccion se calcula igual");

        // --- cinturon de seguridad: un modelo disparatado no puede desmadrar el EQ
        float[] loco = { 0, 40f, -40f, 40f, -40f, 40f, 0f, 0f };
        Pon("loco.mp3", loco, 22050f);
        float[] cl = (float[])CORR.Invoke(F, new object[] { "loco.mp3" });
        bool acotado = true;
        for (int b = 0; b < 8; b++) if (cl != null && Math.Abs(cl[b]) > 4.001f) acotado = false;
        Console.WriteLine("  huella disparatada -> correcciones: " + (cl == null ? "(ninguna)" : string.Join(" ", Array.ConvertAll(cl, v => v.ToString("0.0")))));
        Check(acotado, "por disparatada que sea la cancion, ninguna correccion pasa de +-4 dB");

        // --- quitar el modelo: vuelve al promedio sin romper nada
        load.Invoke(RED, new object[] { "no_existe.bin" });
        Check((bool)loaded.GetValue(RED), "un archivo inexistente no borra el modelo ya cargado");
        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
