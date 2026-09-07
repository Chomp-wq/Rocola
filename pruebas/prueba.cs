using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

class Prueba
{
    static Type TF; static object F;
    static object Fld(string n) { return TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).GetValue(F); }
    static void SetFld(string n, object v) { TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(F, v); }
    static void Call(string n) { TF.GetMethod(n, BindingFlags.Instance|BindingFlags.NonPublic).Invoke(F, null); }
    static void Marca(string p) { TF.GetMethod("MarkDeepDone", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(F, new object[]{p}); }

    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        F = FormatterServices.GetUninitializedObject(TF);
        // el constructor no corrio: armamos a mano solo lo que toca esta parte
        SetFld("deepDone", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        SetFld("deepDoneLock", new object());
        SetFld("deepPend", new List<string>());

        string archivo = (string)TF.GetMethod("DeepDoneFile", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(F, null);
        Console.WriteLine("archivo: " + archivo);
        if (File.Exists(archivo)) File.Delete(archivo);

        // --- 1) arranca de cero
        Call("LoadDeepDone");
        HashSet<string> d = (HashSet<string>)Fld("deepDone");
        Check(d.Count == 0, "sin archivo previo, empieza vacio");

        // --- 2) "mide" 3 canciones y guarda (como hace el hilo cada 10)
        string[] tanda1 = { @"D:\musica\Banda\01 Uno.mp3", @"D:\musica\Banda\02 Dos.mp3", @"D:\musica\Otro\Tres.flac" };
        foreach (string r in tanda1) Marca(r);
        Call("SaveDeepDone");
        System.Threading.Thread.Sleep(600);   // el guardado va en segundo plano
        Check(File.Exists(archivo), "el archivo se escribe");
        Check(File.ReadAllLines(archivo).Length >= 3, "trae las 3 rutas");

        // --- 3) SE DETIENE Y SE VUELVE A ENCENDER (rocola reiniciada): otra instancia limpia
        object F2 = FormatterServices.GetUninitializedObject(TF);
        F = F2;
        SetFld("deepDone", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        SetFld("deepDoneLock", new object());
        SetFld("deepPend", new List<string>());
        Call("LoadDeepDone");
        HashSet<string> d2 = (HashSet<string>)Fld("deepDone");
        Check(d2.Count == 3, "al reanudar recuerda las 3 (leyo " + d2.Count + ")");
        foreach (string r in tanda1) Check(d2.Contains(r), "recuerda " + Path.GetFileName(r));
        Check(d2.Contains(@"d:\MUSICA\banda\01 UNO.mp3"), "no distingue mayusculas/minusculas (como Windows)");

        // --- 4) la lista de pendientes SALTA lo ya repasado (misma condicion que StartPreScan)
        string[] biblioteca = { tanda1[0], tanda1[1], tanda1[2], @"D:\musica\Nuevo\Cuatro.mp3", @"D:\musica\Nuevo\Cinco.mp3" };
        List<string> pend = new List<string>();
        int elegibles = 0;
        foreach (string r in biblioteca) { elegibles++; if (d2.Contains(r)) continue; pend.Add(r); }
        Check(elegibles == 5 && pend.Count == 2, "de 5 canciones quedan 2 pendientes (no 5): CONTINUA donde iba");
        Check(pend[0].EndsWith("Cuatro.mp3") && pend[1].EndsWith("Cinco.mp3"), "las pendientes son las que faltaban");
        int hechas = elegibles - pend.Count;
        Check(hechas == 3, "la barra arranca en 3 de 5 (" + (hechas*100/elegibles) + "%), no en 0");

        // --- 5) sigue midiendo y vuelve a guardar: se acumula, no se pisa
        foreach (string r in pend) Marca(r);
        Call("SaveDeepDone");
        System.Threading.Thread.Sleep(600);
        object F3 = FormatterServices.GetUninitializedObject(TF);
        F = F3;
        SetFld("deepDone", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        SetFld("deepDoneLock", new object());
        SetFld("deepPend", new List<string>());
        Call("LoadDeepDone");
        HashSet<string> d3 = (HashSet<string>)Fld("deepDone");
        Check(d3.Count == 5, "tras la 2a tanda recuerda las 5 (leyo " + d3.Count + ")");
        int pend2 = 0; foreach (string r in biblioteca) if (!d3.Contains(r)) pend2++;
        Check(pend2 == 0, "biblioteca completa: 0 pendientes -> el panel dira 'ya esta repasada entera'");

        // --- 6) rutas con acentos y espacios (los nombres reales de la rocola)
        File.Delete(archivo);   // el guardado ahora ANADE: se parte de limpio para contar solo estas dos
        object F4 = FormatterServices.GetUninitializedObject(TF); F = F4;
        SetFld("deepDone", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        SetFld("deepDoneLock", new object());
        SetFld("deepPend", new List<string>());
        Marca(@"D:\musica\Los Ángeles Azules\03 Cómo Te Voy A Olvidar.mp3");
        Marca(@"D:\musica\Niño\canción ñ & símbolos.mp3");
        Call("SaveDeepDone");
        System.Threading.Thread.Sleep(600);
        object F5 = FormatterServices.GetUninitializedObject(TF); F = F5;
        SetFld("deepDone", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        SetFld("deepDoneLock", new object());
        SetFld("deepPend", new List<string>());
        Call("LoadDeepDone");
        HashSet<string> d5 = (HashSet<string>)Fld("deepDone");
        Check(d5.Count == 2 && d5.Contains(@"D:\musica\Niño\canción ñ & símbolos.mp3"),
              "acentos y eñes sobreviven al guardado");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
