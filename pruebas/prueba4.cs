using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

class Prueba4
{
    static Type TF; static object F;
    static object Fld(string n) { return TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).GetValue(F); }
    static void SetFld(string n, object v) { TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(F, v); }
    static object Call(string n, params object[] a)
    { MethodInfo mi = TF.GetMethod(n, BindingFlags.Instance|BindingFlags.NonPublic); object r = mi.Invoke(F, a); EsperaGuardado(); return r; }
    // AL GUARDAR HAY QUE ESPERAR A QUE SE HAYA GUARDADO. La rocola escribe en segundo plano: pedir
    // "guarda" no deja el archivo escrito al volver, lo deja ENCARGADO. La barrera esta en Codigo
    // porque cada rama le puso un nombre distinto y aqui no hay por que saberlo.
    static void EsperaGuardado() { Codigo.EsperaGuardado(TF, F); }
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }
    static long Tam(string f) { return File.Exists(f) ? new FileInfo(f).Length : 0; }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        F = Codigo.FormSinEncender(TF);
        SetFld("iaDone", new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase));
        SetFld("iaDoneLock", new object()); SetFld("iaPend", new List<string>());
        SetFld("deepDone", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        SetFld("deepDoneLock", new object()); SetFld("deepPend", new List<string>());
        string fIa = (string)Call("IaDoneFile"), fDeep = (string)Call("DeepDoneFile");
        if (File.Exists(fIa)) File.Delete(fIa);
        if (File.Exists(fDeep)) File.Delete(fDeep);
        string A = @"D:\musica\Banda\01 Uno.mp3";

        // --- 1) re-marcar lo MISMO no crece: ni la lista pendiente ni el archivo
        Call("MarkIaDone", A, 1);
        for (int i = 0; i < 100; i++) Call("MarkIaDone", A, 1);
        List<string> pend = (List<string>)Fld("iaPend");
        Check(pend.Count == 1, "marcar 101 veces la misma cancion deja UNA sola linea pendiente (hay " + pend.Count + ")");
        Call("SaveIaDone");
        long t1 = Tam(fIa);
        Check(pend.Count == 0, "tras guardar, la lista pendiente queda vacia (no se acumula)");
        for (int i = 0; i < 50; i++) Call("SaveIaDone");
        Check(Tam(fIa) == t1, "guardar 50 veces sin nada nuevo NO toca el archivo (" + t1 + " bytes)");
        Call("MarkIaDone", A, 2);   // subir de nivel: exactamente una linea mas
        Call("SaveIaDone");
        Check(Tam(fIa) > t1 && Tam(fIa) <= t1 + 2 + A.Length + 4, "subir de nivel anade UNA linea, no reescribe");

        // --- 2) lo mismo para el repaso a fondo
        for (int i = 0; i < 100; i++) Call("MarkDeepDone", A);
        List<string> dp = (List<string>)Fld("deepPend");
        Check(dp.Count == 1, "repaso a fondo: 100 marcas de la misma cancion = 1 linea pendiente");
        Call("SaveDeepDone");
        long d1 = Tam(fDeep);
        for (int i = 0; i < 50; i++) Call("SaveDeepDone");
        Check(Tam(fDeep) == d1 && ((List<string>)Fld("deepPend")).Count == 0, "sin nada nuevo, ni archivo ni lista crecen");

        // --- 3) tope de 24 GB: corta EXACTO donde debe (sin desbordes de enteros)
        SetFld("iaMaxBytes", 24L*1024*1024*1024);
        SetFld("iaPairsTotal", 161061273);
        Check(!(bool)Call("IaBudgetFull"), "un ejemplo ANTES del tope: sigue cosechando (161.061.273)");
        SetFld("iaPairsTotal", 161061274);
        Check((bool)Call("IaBudgetFull"), "justo EN el tope: se detiene (161.061.274)");
        string info = (string)Call("IaInfoText");
        Check(info.Contains("GB") && info.Contains("tope"), "el panel lo dice en GB y avisa del tope: \"" + info + "\"");
        SetFld("iaMaxBytes", 0L); SetFld("iaPairsTotal", int.MaxValue);
        Check(!(bool)Call("IaBudgetFull"), "ia_datos_mb=0 (sin tope) aguanta hasta int.MaxValue ejemplos sin desbordar");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
