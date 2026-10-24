using System;
using System.Collections.Generic;
using System.Reflection;

// EL VOLUMEN DE WINDOWS AL MAXIMO AL ARRANCAR (VolumenWindows.cs; decision del dueno del 24-10-2026).
//
// Aqui no hay Windows: ni el mezclador de XP ni el volumen de Vista existen en Linux. Lo que se vigila es lo que se puede ver en
// el programa compilado, que es donde esto se rompe sin que nadie se entere:
//   1) que se hace UNA vez por arranque, al mostrarse la ventana, en un hilo de fondo y con todo atrapado;
//   2) que en XP sube el control MAESTRO (la linea de los altavoces) y le quita el silencio, y a la linea Onda solo el silencio;
//   3) que en Vista o posterior sube el aparato predeterminado y la sesion de la propia rocola, y les quita el silencio;
//   4) que NINGUN otro sitio toca el volumen de Windows: ni la cadena, ni Ctrl+F3, ni el medidor, ni el diagnostico (y nunca el
//      volumen de la salida WaveOut, que desde Vista es el de la rocola en el mezclador y se queda guardado);
//   5) y que fuera de Windows no hace nada ni revienta.
class VolumenWindows
{
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        Type TF = asm.GetType("Rocola.Form1", true);
        MethodInfo alArrancar = TF.GetMethod("SubeVolumenWindowsAlArrancar", T), sube = TF.GetMethod("SubeVolumenWindows", T);
        if (alArrancar == null || sube == null) { Check(false, "no esta VolumenWindows.cs"); return 1; }

        Console.WriteLine("\n  1) UNA VEZ POR ARRANQUE, DETRAS\n");
        List<string> shown = Codigo.Llamadas(TF.GetMethod("OnShown", T));
        int iCfg = shown.IndexOf("Form1.LoadConfigPrefs"), iVol = shown.IndexOf("Form1.SubeVolumenWindowsAlArrancar");
        if (iCfg < 0) iCfg = shown.FindIndex(delegate (string s) { return s.EndsWith("LoadConfigPrefs"); });
        if (iVol < 0) iVol = shown.FindIndex(delegate (string s) { return s.EndsWith("SubeVolumenWindowsAlArrancar"); });
        Check(iVol >= 0 && iVol > iCfg, "al mostrarse la ventana (OnShown), despues de leer la configuracion");
        int veces = 0;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF)) if (Codigo.Llama(m, "SubeVolumenWindowsAlArrancar")) veces++;
        Check(veces == 1, "y en ningun otro sitio (" + veces + " llamada)");
        List<string> detras = Codigo.ConAyudantes(TF, "SubeVolumenWindowsAlArrancar");
        Check(Codigo.Llama(alArrancar, "QueueUserWorkItem") && Codigo.Llama(detras, "SubeVolumenWindows") && Codigo.Llama(detras, "Diag"),
              "en un hilo de fondo (no frena la pantalla ni la musica) y lo apunta en la bitacora");
        bool atrapa = false;
        foreach (MethodBase m in Codigo.MetodosConAyudantes(TF, "SubeVolumenWindowsAlArrancar"))
            if (Codigo.Llama(m, "SubeVolumenWindows") && m.GetMethodBody() != null && m.GetMethodBody().ExceptionHandlingClauses.Count > 0) atrapa = true;
        Check(atrapa, "con todo atrapado: si una API no esta, no pasa nada");

        Console.WriteLine("\n  2) Y 3) QUE VOLUMEN\n");
        List<string> xp = Codigo.ConAyudantes(TF, "SubeVolumenXp"), vista = Codigo.ConAyudantes(TF, "SubeVolumenVista");
        MethodInfo linea = TF.GetMethod("SubeLinea", T);
        if (linea != null && Codigo.Llama(xp, "SubeLinea")) xp.AddRange(Codigo.Llamadas(linea));   // lo de cada linea (volumen y silencio) va aparte
        Check(Codigo.Llama(xp, "get_NumberOfDevices") && Codigo.Llama(xp, "get_Destinations") && Codigo.Llama(xp, "UnsignedMixerControl.set_Value")
              && Codigo.Llama(xp, "BooleanMixerControl.set_Value") && Codigo.Llama(xp, "get_Sources"),
              "XP: el control maestro de cada tarjeta al maximo y sin silencio (y a la linea Onda solo el silencio)");
        Check(Codigo.Llama(vista, "set_MasterVolumeLevelScalar") && Codigo.Llama(vista, "AudioEndpointVolume.set_Mute")
              && Codigo.Llama(vista, "SimpleAudioVolume.set_Volume") && Codigo.Llama(vista, "SimpleAudioVolume.set_Mute"),
              "Vista o posterior: el aparato predeterminado y la sesion de la rocola, al maximo y sin silencio");

        Console.WriteLine("\n  4) NADIE MAS TOCA EL VOLUMEN DE WINDOWS\n");
        string[] tocan = { "set_MasterVolumeLevelScalar", "set_MasterVolumeLevel", "UnsignedMixerControl.set_Value", "UnsignedMixerControl.set_Percent",
                           "SimpleAudioVolume.set_Volume", "waveOutSetVolume", "WaveOut.set_Volume", "WaveOutEvent.set_Volume", "WasapiOut.set_Volume" };
        List<string> otros = new List<string>();
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
        {
            string quien = (m.DeclaringType != null ? m.DeclaringType.Name + "." : "") + m.Name;
            if (m.Name == "SubeVolumenVista" || m.Name == "SubeVolumenXp" || m.Name == "SubeLinea") continue;
            List<string> ll = Codigo.Llamadas(m);
            foreach (string t in tocan) if (Codigo.Llama(ll, t)) { otros.Add(quien + " (" + t + ")"); break; }
        }
        foreach (string o in otros) Console.WriteLine("     TOCA EL VOLUMEN:  " + o);
        Check(otros.Count == 0, "solo VolumenWindows.cs: ni la cadena, ni Ctrl+F3, ni el medidor, ni el diagnostico, ni la salida WaveOut");

        Console.WriteLine("\n  5) FUERA DE WINDOWS\n");
        string r = null; bool revento = false;
        try { r = (string)sube.Invoke(null, null); } catch { revento = true; }
        bool esWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        Check(!revento && r != null && (esWindows || r.StartsWith("no es Windows")), "no revienta y lo dice (" + (r ?? "nada") + ")");

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN (" + oks + " comprobaciones)" : "  FALLOS: " + fallos);
        return fallos;
    }
}
