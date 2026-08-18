using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

class Comparar
{
    static Type TF; static object F; static MethodInfo MF;
    static void SetFld(string n, object v) { TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(F, v); }

    // MeasureFile(path, aFondo, out rms, out durSec, out eqCurve)
    static bool Medir(string ruta, bool aFondo, out float rms, out float dur, out float[] eq)
    {
        object[] a = new object[] { ruta, aFondo, null, null, null };
        bool ok = (bool)MF.Invoke(F, a);
        rms = (float)a[2]; dur = (float)a[3]; eq = (float[])a[4];
        return ok;
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        F = FormatterServices.GetUninitializedObject(TF);
        SetFld("iaDone", new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase));
        SetFld("iaDoneLock", new object());
        SetFld("iaPend", new List<string>());
        SetFld("iaHarvestOn", false);      // aqui solo comparamos la MEDIDA (volumen y EQ)
        SetFld("preScanStop", false);
        MF = TF.GetMethod("MeasureFile", BindingFlags.Instance|BindingFlags.NonPublic);

        string[] files = Directory.GetFiles("wav", "*.wav");
        Array.Sort(files);
        Console.WriteLine();
        Console.WriteLine("                          VOLUMEN medido            EQ automatico (8 bandas)");
        Console.WriteLine("  cancion               pasivo    a fondo   dif      dif maxima   dif media");
        Console.WriteLine("  ---------------------------------------------------------------------------");
        double peorVol = 0, peorEq = 0;
        foreach (string f in files)
        {
            float r1, d1, r2, d2; float[] e1, e2;
            if (!Medir(f, false, out r1, out d1, out e1)) { Console.WriteLine("  " + f + ": no se pudo medir"); continue; }
            if (!Medir(f, true,  out r2, out d2, out e2)) { Console.WriteLine("  " + f + ": no se pudo medir (a fondo)"); continue; }
            double difDb = 20.0 * Math.Log10(Math.Max(1e-9, r2) / Math.Max(1e-9, r1));
            double emax = 0, esum = 0; int en = 0;
            if (e1 != null && e2 != null)
                for (int b = 0; b < 8 && b < e1.Length && b < e2.Length; b++)
                { double d = Math.Abs(e2[b] - e1[b]); if (d > emax) emax = d; esum += d; en++; }
            string nom = Path.GetFileNameWithoutExtension(f);
            Console.WriteLine(string.Format("  {0,-20}  {1,7:0.0000}  {2,7:0.0000}  {3,7:+0.00;-0.00} dB   {4,6:0.00} dB   {5,6:0.00} dB",
                              nom, r1, r2, difDb, emax, en > 0 ? esum/en : 0));
            if (Math.Abs(difDb) > peorVol) peorVol = Math.Abs(difDb);
            if (emax > peorEq) peorEq = emax;
        }
        Console.WriteLine("  ---------------------------------------------------------------------------");
        Console.WriteLine(string.Format("  PEOR CASO:  volumen {0:0.00} dB de diferencia   |   EQ {1:0.00} dB en una banda", peorVol, peorEq));
        Console.WriteLine();
        Console.WriteLine("  Referencia: un escalon del nivelado son 3 dB; el oido apenas nota 1 dB.");
        return 0;
    }
}
