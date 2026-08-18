using System;
using System.Reflection;
using System.Runtime.Serialization;

class Prueba3
{
    static Type TF; static object F;
    static void SetFld(string n, object v) { TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(F, v); }
    static object Call(string n) { return TF.GetMethod(n, BindingFlags.Instance|BindingFlags.NonPublic).Invoke(F, null); }
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        F = FormatterServices.GetUninitializedObject(TF);

        // --- tope del archivo de material
        SetFld("iaMaxBytes", 600L*1024*1024);
        SetFld("iaPairsTotal", 0);
        Check(!(bool)Call("IaBudgetFull"), "vacio: hay presupuesto");
        SetFld("iaPairsTotal", 1000000);
        Check(!(bool)Call("IaBudgetFull"), "1 millon de ejemplos (152 MB): sigue habiendo");
        Console.WriteLine("        texto del panel: " + Call("IaInfoText"));
        SetFld("iaPairsTotal", 3932160);
        Check((bool)Call("IaBudgetFull"), "al llegar a 600 MB: tope alcanzado -> deja de cosechar");
        Console.WriteLine("        texto del panel: " + Call("IaInfoText"));
        SetFld("iaMaxBytes", 0L);
        Check(!(bool)Call("IaBudgetFull"), "ia_datos_mb=0 quita el tope (nunca se llena)");

        // --- reparto de cuadros por toda la cancion (misma cuenta que MeasureFile)
        const int WARM = 64, DEEP = 1536, FRAME = 1024;
        Console.WriteLine();
        Console.WriteLine("  cancion   cuadros  paso  ultimo cuadro   cubre    ejemplos");
        foreach (double dur in new double[]{ 45, 120, 210, 360, 600 })
        {
            int rate = 44100;
            long framesTot = (long)(dur * rate / FRAME);
            long paso = (framesTot - WARM + DEEP - 1) / DEEP;
            int stride = (paso > 1) ? (int)paso : 1;
            int frameMax = WARM + stride * DEEP;
            double cubre = Math.Min(frameMax, framesTot) * FRAME / (double)rate;
            int emitidos = 0;
            for (int f = 1; f <= Math.Min(frameMax, framesTot); f++)
                if (f > WARM && (stride == 1 || (f % stride) == 0)) emitidos++;
            Console.WriteLine(string.Format("  {0,5:0}s    {1,6}  {2,4}  {3,10}   {4,5:0}s   {5,7}",
                              dur, framesTot, stride, frameMax, cubre, emitidos*5));
            Check(cubre >= dur*0.985, "  cubre la cancion ENTERA de " + dur + "s");
            Check(emitidos*5 <= DEEP*5 + 5, "  no se pasa del tope de ejemplos por cancion");
            Check(emitidos*5 >= 200, "  y saca material suficiente");
        }
        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
