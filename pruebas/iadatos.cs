using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

// EL INTERRUPTOR DE "JUNTAR MATERIAL PARA LA IA".
//
// Mientras la rocola repasa la biblioteca, a las canciones que TODAVIA conservan sus agudos (320k, FLAC,
// WAV) les corta el agudo a proposito y guarda el par "lo que queda / lo que habia". Ese archivo se lleva en
// una USB a una PC potente, se entrena alli y se regresa el modelo nuevo. Es material de entrenamiento, y
// llega a pesar 24 GB en el disco de la rocola.
//
// Eso no siempre interesa: hay locales con el disco justo, y hay duenos que sencillamente no quieren juntar
// nada. Se podia apagar, pero solo escribiendo "ia_datos=0" a mano en el archivo de configuracion: o sea,
// que en la practica no se podia. Ahora hay un interruptor en la ventana de sonido (Ctrl+F3).
//
// LO IMPORTANTE ES QUE APAGUE ESO Y NADA MAS. La rocola aprende muchas cosas mientras repasa -cuanto suena
// cada cancion, la huella tonal de la biblioteca entera para el EQ automatico, cuanto grave le falta a cada
// una, donde el MP3 le corto los agudos, cuanto dura- y todo eso tiene que seguir funcionando exactamente
// igual con el interruptor apagado. Aqui se mide la MISMA cancion con el interruptor encendido y apagado y
// se comprueba que lo medido sale identico; que con el apagado no se guarda ni un ejemplo; y se lista el
// juego COMPLETO de sitios del programa que consultan ese interruptor, para que no se cuele uno nuevo.
class IaDatos
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF; static object F; static MethodInfo MF;
    static FieldInfo Campo(string n) { return TF.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic); }
    static void Set(string n, object v) { Campo(n).SetValue(F, v); }
    static object Get(string n) { return Campo(n).GetValue(F); }
    static object Llama(string n) { return TF.GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(F, null); }

    // Quien LEE y quien ESCRIBE un campo, en todo el programa (tambien dentro de las funciones sueltas que
    // el compilador saca a clases escondidas).
    static void Quien(Type t, string campo, Dictionary<string, int[]> salida)
    {
        BindingFlags bf = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        List<MethodBase> todos = new List<MethodBase>();
        todos.AddRange(t.GetMethods(bf)); todos.AddRange(t.GetConstructors(bf));
        foreach (MethodBase m in todos)
        {
            int lee, esc;
            Codigo.UsosDeCampo(m, campo, out lee, out esc);
            if (lee > 0 || esc > 0) salida[t.Name + "." + m.Name] = new int[] { lee, esc };
        }
        foreach (Type n in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)) Quien(n, campo, salida);
    }
    // Las funciones sueltas de una ventana las saca el compilador a una clase escondida con nombre raro
    // (<VentanaAjustes>c__AnonStorey...). Para saber de quien son basta con mirar el nombre.
    // OJO CON EL NOMBRE: el cuerpo de la ventana de Ctrl+F3 se llama VentanaAjustes desde que la ventana
    // tiene dos pestanas -ShowVolumeManager quedo fuera, solo para poder reabrirla despues de medir-. Esta
    // bateria se puso roja al renombrarla, que es justo lo que tenia que pasar.
    static bool EsDe(string quien, string metodo)
    {
        return quien.EndsWith("." + metodo, StringComparison.Ordinal)
            || quien.IndexOf("<" + metodo + ">", StringComparison.Ordinal) >= 0;
    }

    // La rocola se arma aqui a mano, sin arrancarla entera: solo lo que hace falta para medir una cancion.
    // Los candados van uno por uno porque un candado sin poner revienta al guardar y el fallo se disfraza de
    // "esta cancion no dio material".
    static void Prepara(bool juntar)
    {
        Set("iaDone", new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        Set("iaDoneLock", new object());
        Set("iaFileLock", new object());
        Set("iaPend", new List<string>());
        Set("preScanStop", false);
        Set("iaPairsTotal", 0);
        Set("iaMaxBytes", 0L);              // sin tope: aqui no se prueba el tope
        Set("iaHarvestOn", juntar);
    }

    // MeasureFile(ruta, aFondo, out rms, out durSec, out eqCurve)
    static bool Medir(string ruta, out float rms, out float dur, out float[] eq)
    {
        object[] a = new object[] { ruta, false, null, null, null };
        bool ok = (bool)MF.Invoke(F, a);
        rms = (float)a[2]; dur = (float)a[3]; eq = (float[])a[4];
        return ok;
    }

    static long Tam(string f) { try { return new FileInfo(f).Length; } catch { return 0; } }

    // El indice que dejo la prueba (y lo que la rocola aparto al ver el material empezar de cero). Los
    // ".anterior" que ya estaban antes de empezar la prueba NO se tocan: son del dueno.
    static HashSet<string> anterioresPrevios;
    static string[] Anteriores(string indice)
    {
        try { return Directory.GetFiles(Path.GetDirectoryName(Path.GetFullPath(indice)), Path.GetFileName(indice) + ".anterior*"); }
        catch { return new string[0]; }
    }
    static void BorraIndice(string indice)
    {
        try { File.Delete(indice); } catch { }
        foreach (string f in Anteriores(indice))
            if (!anterioresPrevios.Contains(f)) { try { File.Delete(f); } catch { } }
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);

        // =========================================================================================
        Console.WriteLine("\n  1) EL INTERRUPTOR ESTA EN LA VENTANA DE SONIDO (Ctrl+F3)\n");
        Dictionary<string, int[]> quien = new Dictionary<string, int[]>();
        Quien(TF, "iaHarvestOn", quien);
        int escribeVentana = 0, leeVentana = 0;
        foreach (KeyValuePair<string, int[]> kv in quien)
            if (EsDe(kv.Key, "VentanaAjustes")) { leeVentana += kv.Value[0]; escribeVentana += kv.Value[1]; }
        Check(escribeVentana > 0,
              "la ventana de sonido puede APAGARLO (antes solo se podia escribiendo a mano en un archivo)");
        Check(leeVentana > 0, "y lo lee para dejar el interruptor puesto como esta ahora mismo");

        // =========================================================================================
        Console.WriteLine("\n  2) Y SE RECUERDA AL APAGAR LA ROCOLA\n");
        F = FormatterServices.GetUninitializedObject(TF);
        Set("extraMusicDirs", new List<string>());
        string cfg = (string)TF.GetProperty("ConfigFile", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(F, null);
        string cfgBak = cfg + ".guardado_por_la_prueba";
        bool habia = File.Exists(cfg);
        try { if (habia) File.Copy(cfg, cfgBak, true); } catch { }
        try
        {
            Set("iaHarvestOn", false);
            Llama("SaveConfigPrefs");
            string guardado = File.ReadAllText(cfg);
            Check(guardado.Contains("ia_datos=0"), "apagado, queda apuntado en la configuracion");
            Set("iaHarvestOn", true);
            Llama("LoadConfigPrefs");
            Check(!(bool)Get("iaHarvestOn"), "y al volver a encender la rocola sigue apagado");
            Set("iaHarvestOn", true);
            Llama("SaveConfigPrefs");
            Set("iaHarvestOn", false);
            Llama("LoadConfigPrefs");
            Check((bool)Get("iaHarvestOn"), "y encendido, igual: se recuerda como se dejo");
        }
        finally
        {
            try { if (habia) File.Copy(cfgBak, cfg, true); else File.Delete(cfg); } catch { }
            try { File.Delete(cfgBak); } catch { }
            // La rocola guarda dejando una copia del anterior (asi un corte de luz no borra los ajustes):
            // aqui esa copia es basura de la prueba y no tiene por que quedarse tirada.
            if (!habia) { try { File.Delete(cfg + ".bak"); } catch { } try { File.Delete(cfg + ".tmp"); } catch { } }
        }

        // =========================================================================================
        // Se mide la MISMA cancion dos veces -con el interruptor encendido y apagado- y se comparan las dos
        // cosas: cuanto material se guardo, y todo lo demas que la rocola aprende de esa cancion.
        Console.WriteLine("\n  3) APAGADO NO SE GUARDA NI UN EJEMPLO, Y ENCENDIDO SI\n");
        MF = TF.GetMethod("MeasureFile", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(MF != null, "se puede pedir a la rocola que mida una cancion");
        if (MF == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        string[] wavs = Directory.Exists("wav") ? Directory.GetFiles("wav", "*.wav") : new string[0];
        Array.Sort(wavs);
        Check(wavs.Length > 0, "hay canciones de prueba con las que medir");
        if (wavs.Length == 0) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        MethodInfo mDatos = TF.GetMethod("IaDataFile", BindingFlags.Instance | BindingFlags.NonPublic);
        string datos = (string)mDatos.Invoke(F, null);
        string datosBak = datos + ".guardado_por_la_prueba";
        bool habiaDatos = File.Exists(datos);
        try { if (habiaDatos) File.Move(datos, datosBak); } catch { }
        // Desde que la cosecha apunta de que cancion viene cada ejemplo, al lado vive rocola_ia_datos.idx.
        // Se aparta y se devuelve igual que el material: una prueba no toca los datos de nadie.
        MethodInfo mIndice = TF.GetMethod("IaIndexFile", BindingFlags.Instance | BindingFlags.NonPublic);
        string indice = mIndice != null ? (string)mIndice.Invoke(F, null) : null;
        string indiceBak = indice + ".guardado_por_la_prueba";
        bool habiaIndice = indice != null && File.Exists(indice);
        anterioresPrevios = new HashSet<string>(indice != null ? Anteriores(indice) : new string[0]);
        try { if (habiaIndice) File.Move(indice, indiceBak); } catch { }
        long indiceOn = 0, indiceOff = 0;

        float rOn = 0, dOn = 0, rOff = 0, dOff = 0; float[] eOn = null, eOff = null;
        long creceOn = 0, creceOff = 0;
        string usada = null;
        try
        {
            foreach (string w in wavs)
            {
                // ----- con el interruptor ENCENDIDO -----
                Prepara(true);
                try { File.Delete(datos); } catch { }
                long antes = Tam(datos);
                if (!Medir(w, out rOn, out dOn, out eOn)) continue;
                creceOn = Tam(datos) - antes;
                if (indice != null) indiceOn = Tam(indice);
                if (creceOn <= 0) continue;         // esta cancion no da material (agudos ya cortados): la siguiente

                // ----- la MISMA, con el interruptor APAGADO -----
                Prepara(false);
                try { File.Delete(datos); } catch { }
                if (indice != null) { BorraIndice(indice); }
                antes = Tam(datos);
                Medir(w, out rOff, out dOff, out eOff);
                creceOff = Tam(datos) - antes;
                if (indice != null) indiceOff = Tam(indice);
                usada = Path.GetFileNameWithoutExtension(w);
                break;
            }
        }
        finally
        {
            try { File.Delete(datos); } catch { }
            try { if (habiaDatos) File.Move(datosBak, datos); } catch { }
            if (indice != null) BorraIndice(indice);
            try { if (habiaIndice) File.Move(indiceBak, indice); } catch { }
        }

        Check(usada != null, "hay al menos una cancion completa de la que se puede sacar material");
        if (usada == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }
        Console.WriteLine("     midiendo \"" + usada + "\":");
        Console.WriteLine("        con el interruptor ENCENDIDO el archivo de material crecio " + creceOn.ToString("#,0") + " bytes");
        Console.WriteLine("        con el interruptor APAGADO   crecio " + creceOff.ToString("#,0") + " bytes");
        Check(creceOn > 0, "encendido SI se junta material");
        Check(creceOff == 0, "y apagado no se guarda ni un byte");
        if (indice != null)
        {
            Console.WriteLine("        indice de origen: " + indiceOn + " bytes encendido, " + indiceOff + " apagado");
            Check(indiceOn == 16 + 64 && indiceOff == 0, "encendido se apunta de que cancion viene (una entrada); apagado, nada");
        }

        // =========================================================================================
        Console.WriteLine("\n  4) PERO TODO LO DEMAS SE SIGUE MIDIENDO EXACTAMENTE IGUAL\n");
        Console.WriteLine("        volumen:   " + rOn.ToString("0.000000") + "   apagado: " + rOff.ToString("0.000000"));
        Console.WriteLine("        duracion:  " + dOn.ToString("0.000") + "s  apagado: " + dOff.ToString("0.000") + "s");
        Check(rOn == rOff, "el volumen medido sale identico: el nivelado sigue aprendiendo igual");
        Check(dOn == dOff, "y la duracion tambien");
        Check(eOn != null && eOff != null, "y la huella tonal de la cancion se mide en los dos casos");
        if (eOn != null && eOff != null)
        {
            Check(eOn.Length == eOff.Length, "con las mismas bandas");
            double peor = 0; int nb = Math.Min(8, Math.Min(eOn.Length, eOff.Length));
            for (int b = 0; b < nb; b++) { double d = Math.Abs(eOn[b] - eOff[b]); if (d > peor) peor = d; }
            Console.WriteLine("        huella tonal: la banda que mas se mueve, " + peor.ToString("0.000000") + " dB");
            Check(peor == 0, "las 8 bandas del EQ automatico salen identicas");
            int iCut = (int)TF.GetField("EqCut", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
            int iCutLo = (int)TF.GetField("EqCutLo", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
            int iClip = (int)TF.GetField("EqClip", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
            Console.WriteLine("        donde el archivo se queda sin agudos: " + eOn[iCut].ToString("#,0") + " Hz"
                              + "   apagado: " + eOff[iCut].ToString("#,0") + " Hz");
            Check(eOn[iCut] == eOff[iCut], "sigue midiendo donde el MP3 corto los agudos (de ahi salen el EQ y los graves automaticos)");
            Check(eOn[iCutLo] == eOff[iCutLo], "y donde se queda sin graves");
            Check(eOn[iClip] == eOff[iClip], "y cuanto trae los picos recortados");
        }

        // =========================================================================================
        // Si alguien mas adelante engancha OTRA cosa a este interruptor -el repaso a fondo, el EQ, el
        // nivelado- aparecera aqui y esta bateria se pondra en rojo. Es justo lo que se quiere: el
        // interruptor apaga el material de entrenamiento y nada mas.
        Console.WriteLine("\n  5) Y NADIE MAS EN TODO EL PROGRAMA MIRA ESE INTERRUPTOR\n");
        List<string> sobra = new List<string>();
        foreach (KeyValuePair<string, int[]> kv in quien)
        {
            string q = kv.Key;
            Console.WriteLine("        " + q.PadRight(52) + " lee " + kv.Value[0] + ", escribe " + kv.Value[1]);
            bool permitido = EsDe(q, "VentanaAjustes")               // el interruptor y su letrero
                          || q == "Form1.LoadConfigPrefs"            // leerlo de la configuracion al arrancar
                          || q == "Form1.SaveConfigPrefs"            // y guardarlo al cambiarlo
                          || q == "Form1.MeasureFile"                // EL UNICO sitio que decide si se guarda material
                          || q == "Form1..ctor";                     // el valor de fabrica: encendido
            if (!permitido) sobra.Add(q);
        }
        Check(sobra.Count == 0, "solo lo miran la ventana, la configuracion y el sitio que guarda el material"
              + (sobra.Count > 0 ? ("   [de mas: " + string.Join(", ", sobra.ToArray()) + "]") : ""));
        int[] med; quien.TryGetValue("Form1.MeasureFile", out med);
        Check(med != null && med[0] == 1,
              "y al medir una cancion se consulta UNA sola vez: la decision de guardar, nada mas");

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}
