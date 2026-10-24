using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

// EL MODO NOCHE, RETIRADO (decision del dueno del 24-10-2026).
//
// De las 11 de la noche a las 7 de la manana la rocola bajaba 4 dB. Se hacia porque el que hace el corte de la venta suele
// bajar el volumen para concentrarse; el dueno prefiere que lo siga haciendo el, a mano, y que la rocola no baje nada sola.
// (Hasta el 23-10-2026 esta bateria vigilaba ese modo: el cruce de la medianoche, la rampa y los videos.)
//
// LO QUE ESTA BATERIA VIGILA AHORA:
//
//  1) QUE NO QUEDE NADA. Ni la cuenta de la hora (EsDeNoche, FactorNoche, la rampa), ni la etapa de la cadena que devolvia graves
//     y agudos de madrugada (CompensaNoche), ni su interruptor de Ctrl+F3.
//  2) QUE EL VOLUMEN NO MIRE EL RELOJ. Ni la cadena de cada cancion, ni la siguiente preparada, ni el ajuste en vivo, ni los videos
//     leen la hora: a las tres de la manana suena lo mismo que a las tres de la tarde.
//  3) QUE UNA CONFIGURACION VIEJA NO ROMPA NADA. noche=, noche_desde=, noche_hasta=, noche_db= y noche_compensa= se leen sin error,
//     no cambian nada y al guardar ya no se escriben.
class Noche
{
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        Type TF = asm.GetType("Rocola.Form1", true);

        Console.WriteLine("\n  1) NO QUEDA NADA DEL MODO NOCHE\n");
        string[] metodos = { "EsDeNoche", "MetaNoche", "FactorNoche", "AvanzaRampa", "AvanzaModoNoche", "NocheCompensaDb", "NocheLin" };
        List<string> quedan = new List<string>();
        foreach (string m in metodos) if (TF.GetMethod(m, T) != null) quedan.Add(m);
        foreach (string c in new string[] { "nocheOn", "nocheDesde", "nocheHasta", "nocheDb", "nocheFactor", "nocheCompensa", "curNoche" })
            if (TF.GetField(c, T) != null) quedan.Add(c);
        Check(quedan.Count == 0, "ni la cuenta de la hora, ni la rampa, ni sus campos" + (quedan.Count > 0 ? " (quedan: " + string.Join(", ", quedan.ToArray()) + ")" : ""));
        Check(TF.GetNestedType("CompensaNoche", T) == null && TF.GetNestedType("CadenaCancion", T).GetField("Noche", T) == null,
              "ni la etapa que devolvia graves y agudos de madrugada (CompensaNoche) en la cadena");
        bool interruptor = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            foreach (string t in Codigo.Textos(m))
                if (t.StartsWith("Bajar el volumen de ") || t.StartsWith("Modo noche:") || t.StartsWith("Se acabo el modo noche")) interruptor = true;
        Check(!interruptor, "ni el interruptor de Ctrl+F3 ni sus avisos");

        Console.WriteLine("\n  2) EL VOLUMEN NO MIRA EL RELOJ\n");
        string[] caminos = { "ArmaCadena", "AplicaAPreparada", "ApplyNormNow", "MasterEfectivo", "VideoVolumeFor", "GananciaDe", "NormGainFor" };
        List<string> conReloj = new List<string>();
        foreach (string n in caminos)
        {
            MethodInfo m = TF.GetMethod(n, T);
            if (m == null) { conReloj.Add(n + " (no esta)"); continue; }
            List<string> ll = Codigo.Llamadas(m);
            if (Codigo.Llama(ll, "get_Now") || Codigo.Llama(ll, "get_Hour") || Codigo.Llama(ll, "get_UtcNow")) conReloj.Add(n);
        }
        Check(conReloj.Count == 0, "la cadena, la siguiente preparada, el ajuste en vivo, Subir TODO, el nivelado y los videos no leen la hora"
              + (conReloj.Count > 0 ? " (" + string.Join(", ", conReloj.ToArray()) + ")" : ""));
        List<string> arma = Codigo.Llamadas(TF.GetMethod("ArmaCadena", T));
        Check(Codigo.Llama(arma, "MasterEfectivo") && Codigo.Llama(arma, "GraveMono..ctor"), "la cadena sigue poniendo Subir TODO y el grave en mono justo detras del nivelado");

        Console.WriteLine("\n  3) UNA CONFIGURACION VIEJA NO ROMPE NADA\n");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_noche_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            object f = Codigo.FormSinEncender(TF);
            foreach (FieldInfo fi in TF.GetFields(T))
                if (!fi.IsStatic && fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
            File.WriteAllText(Path.Combine(dir, "rocola_config.txt"),
                "noche=1\r\nnoche_desde=21\r\nnoche_hasta=9\r\nnoche_db=-12\r\nnoche_compensa=80\r\nvideo_vol=70\r\nsubir_db=3\r\n");
            bool leyo = true;
            try { TF.GetMethod("LoadConfigPrefs", T).Invoke(f, null); } catch { leyo = false; }
            Check(leyo, "las claves noche=, noche_desde=, noche_hasta=, noche_db= y noche_compensa= se leen sin error");
            FieldInfo fm = TF.GetField("masterDb", T);
            Check(fm != null && Math.Abs((float)fm.GetValue(f) - 3f) < 1e-4, "y lo de alrededor se lee igual (subir_db=3)");
            TF.GetMethod("SaveConfigPrefs", T).Invoke(f, null);
            Codigo.EsperaGuardado(TF, f);
            string nuevo = File.ReadAllText(Path.Combine(dir, "rocola_config.txt"));
            Check(!nuevo.Contains("\nnoche=") && !nuevo.StartsWith("noche=") && !nuevo.Contains("noche_desde=") && !nuevo.Contains("noche_hasta=")
                  && !nuevo.Contains("noche_db=") && !nuevo.Contains("noche_compensa="),
                  "al guardar ya no se escriben");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN (" + oks + " comprobaciones)" : "  FALLOS: " + fallos);
        return fallos;
    }
}
