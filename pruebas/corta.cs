using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// LA MUSICA NO SE PUEDE CORTAR SOLA.
//
// Dos fallos distintos apagaban la rocola a media cancion, y los dos quedaron apuntados en el archivo de
// errores del local.
//
//   1) "El tipo de matriz de origen no puede asignarse al tipo de matriz de destino", cinco veces en una
//      tarde, con el DSEE encendido. El motivo es una trampa de NAudio que no se ve mirando el codigo: el
//      buffer donde hay que dejar las muestras llega declarado como float[], pero POR DENTRO es el mismo
//      objeto que NAudio le entrega a la tarjeta de sonido como byte[]. Su WaveBuffer solapa las dos vistas
//      en la misma memoria a proposito. Escribir muestra a muestra funciona -para numeros no se comprueba el
//      tipo-, pero Array.Copy SI lo comprueba, ve un byte[] donde le dicen float[] y lanza. Y lanzar ahi
//      mata el hilo que produce el sonido: la cancion se corta en seco.
//
//   2) "InvalidParameter calling acmStreamClose", desde el hilo del recolector de basura. NAudio arma el
//      descompresor de MP3 y justo despues termina de prepararse; si algo falla en ese hueco -una cabecera
//      rota- cierra el archivo pero NO el descompresor. Queda tirado, el recolector lo cierra por su cuenta
//      un rato despues, Windows dice que ya no vale, y la queja sale por un hilo donde nadie la puede
//      recoger: se cierra el programa entero. Salia de madrugada, que es cuando el repaso de la biblioteca
//      abre miles de archivos.
//
// Aqui se reproduce el primero TAL CUAL -con el mismo WaveBuffer de NAudio- y se comprueba que ya no corta y
// que las muestras salen bien; y del segundo, que los MP3 se abren por el camino que no deja nada tirado.
class Corta
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF;
    static Type Nes(string n) { return TF.GetNestedType(n, BindingFlags.NonPublic | BindingFlags.Public); }

    // ¿Copia en bloque SOBRE EL BUFFER QUE LE DAN? Se mira si justo antes de la llamada se carga el primer
    // argumento (ldarg.1, que es el buffer). Vaciar o copiar arrays PROPIOS dentro de un Read esta bien; lo
    // que no vale es tocar en bloque el que llega de fuera, porque ese miente sobre lo que es.
    static bool CopiaEnBloqueSobreElBuffer(MethodBase m)
    {
        Module mod = m.Module; int desdeArg = 99;
        foreach (Codigo.Paso p in Codigo.Pasos(m))
        {
            if (p.Op2 < 0 && p.Op == 0x03) { desdeArg = 0; continue; }   // ldarg.1 = el buffer que llega de fuera
            if (desdeArg < 99) desdeArg++;
            if (p.Op2 >= 0 || (p.Op != 0x28 && p.Op != 0x6F) || desdeArg > 6) continue;
            try
            {
                MethodBase mb = mod.ResolveMethod(p.Numero);
                string q = (mb.DeclaringType != null ? mb.DeclaringType.Name : "?") + "." + mb.Name;
                if (q == "Array.Copy" || q == "Buffer.BlockCopy" || q == "Array.Clear") return true;
            }
            catch { }
        }
        return false;
    }

    // Una fuente de sonido de mentira: una onda que no se acaba nunca. Sirve para hacer sonar la cadena sin
    // ningun archivo de por medio.
    sealed class Onda : ISampleProvider
    {
        private readonly WaveFormat wf; private double fase;
        public Onda(int rate, int ch) { wf = WaveFormat.CreateIeeeFloatWaveFormat(rate, ch); }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] buffer, int offset, int count)
        {
            int ch = wf.Channels;
            for (int i = 0; i < count; i += ch)
            {
                float v = (float)(0.25 * Math.Sin(fase));
                fase += 2.0 * Math.PI * 440.0 / wf.SampleRate;
                for (int c = 0; c < ch && i + c < count; c++) buffer[offset + i + c] = v;
            }
            return count;
        }
    }

    // El reconstructor se arma distinto en cada rama: la de tarjeta grafica le deja elegir el tamano de
    // ventana (usa 4096 para afinar mas) y la modesta no. Se prueban las dos formas.
    static object CreaTap(Type TSp, ISampleProvider fuente)
    {
        BindingFlags bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        try { return Activator.CreateInstance(TSp, bf, null, new object[] { fuente, 2048 }, null); }
        catch { }
        return Activator.CreateInstance(TSp, bf, null, new object[] { fuente }, null);
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);

        // =========================================================================================
        Console.WriteLine("\n  1) EL DSEE NO CORTA LA CANCION\n");
        Type TSp = Nes("SpectralTap");
        Check(TSp != null, "esta el reconstructor de agudos");
        if (TSp == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }
        FieldInfo fAct = TSp.GetField("Active");
        Check(fAct != null && typeof(ISampleProvider).IsAssignableFrom(TSp), "y se le puede pedir sonido con el DSEE encendido");
        if (fAct == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        const int RATE = 44100, CH = 2, MUESTRAS = 4096;

        // EL BUFFER TAL Y COMO LLEGA DE VERDAD: un byte[] visto como float[]. Esto no es un apano de la
        // prueba, es literalmente lo que NAudio le pasa a la cadena (WaveBuffer solapa las dos vistas).
        WaveBuffer wb = new WaveBuffer(MUESTRAS * 4);
        float[] disfrazado = wb.FloatBuffer;
        Console.WriteLine("     el buffer se declara float[] pero por dentro es un " + ((Array)(object)disfrazado).GetType().Name);
        Check(((Array)(object)disfrazado).GetType() != typeof(float[]),
              "la prueba usa el MISMO buffer disfrazado que usa NAudio (si no, no probaria nada)");

        object tap = CreaTap(TSp, new Onda(RATE, CH));
        fAct.SetValue(tap, true);
        string quePaso = null;
        int leidas = 0;
        try
        {
            // POR LA INTERFAZ, como lo llama NAudio de verdad. Por reflexion no valdria: la reflexion mira
            // el tipo del argumento y rechazaria el buffer disfrazado antes de entrar en el codigo.
            ISampleProvider sp = (ISampleProvider)tap;
            for (int v = 0; v < 12; v++) leidas = sp.Read(disfrazado, 0, MUESTRAS);
        }
        catch (Exception ex) { quePaso = ex.GetType().Name + ": " + ex.Message; }
        if (quePaso != null) Console.WriteLine("     al sonar con el DSEE encendido: " + quePaso);
        Check(quePaso == null, "con el DSEE encendido la cancion NO se corta");
        Check(leidas == MUESTRAS, "y entrega todas las muestras que le piden");

        // Y que ademas salgan bien: no basta con no reventar.
        bool haySonido = false;
        for (int i = 0; i < MUESTRAS; i++) if (disfrazado[i] > 0.001f || disfrazado[i] < -0.001f) { haySonido = true; break; }
        Check(haySonido, "y lo que deja en el buffer es sonido de verdad, no silencio");

        // Lo mismo con un buffer NORMAL: las dos formas tienen que dar exactamente lo mismo.
        object tap2 = CreaTap(TSp, new Onda(RATE, CH));
        fAct.SetValue(tap2, true);
        float[] normal = new float[MUESTRAS];
        for (int v = 0; v < 12; v++) ((ISampleProvider)tap2).Read(normal, 0, MUESTRAS);
        int distintas = 0;
        for (int i = 0; i < MUESTRAS; i++) if (normal[i] != disfrazado[i]) distintas++;
        Console.WriteLine("     muestras distintas entre el buffer normal y el disfrazado: " + distintas + " de " + MUESTRAS);
        Check(distintas == 0, "y suena IGUAL con los dos: el arreglo no cambio ni una muestra");

        // =========================================================================================
        Console.WriteLine("\n  2) Y NINGUN ESLABON DE LA CADENA VUELVE A CAER EN LA TRAMPA\n");
        // La regla, para el que venga despues: dentro de un Read que recibe el buffer de salida NO se usan
        // copias en bloque. Da igual lo tentador que sea: ese buffer miente sobre lo que es.
        int mirados = 0; List<string> culpables = new List<string>();
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            MethodInfo m = t.GetMethod("Read", new Type[] { typeof(float[]), typeof(int), typeof(int) });
            if (m == null || m.DeclaringType != t) continue;
            mirados++;
            if (CopiaEnBloqueSobreElBuffer(m)) culpables.Add(t.Name);
        }
        Console.WriteLine("     eslabones mirados: " + mirados);
        Check(mirados >= 5, "se miraron todos los eslabones de la cadena de sonido");
        Check(culpables.Count == 0, "ninguno copia en bloque sobre el buffer que le dan"
              + (culpables.Count > 0 ? ("   [" + string.Join(", ", culpables.ToArray()) + "]") : ""));

        // =========================================================================================
        Console.WriteLine("\n  3) UN MP3 QUE NO SE PUEDE ABRIR NO DEJA NADA TIRADO\n");
        // POR NOMBRE Y CONTANDO, no con GetMethod a secas: hay DOS caminos seguros -uno para un MP3 de un
        // archivo y otro para uno que ya esta en memoria- y pedir "el" AbreMp3 revienta en cuanto hay dos.
        int cuantosAbre = 0;
        MethodInfo mAbre = null;
        foreach (MethodInfo m in TF.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            if (m.Name == "AbreMp3") { cuantosAbre++; if (mAbre == null) mAbre = m; }
        Check(mAbre != null, "los MP3 se abren por un camino propio, no directamente con NAudio");
        Check(cuantosAbre >= 2, "y ese camino cubre TAMBIEN el MP3 reconstruido en memoria (por ahi se colo una fuga)");
        if (mAbre != null)
        {
            List<string> l = Codigo.ConAyudantes(TF, "AbreMp3");
            Check(Codigo.Llama(l, "Mp3FileReader.CreateAcmFrameDecompressor"),
                  "se le dice a NAudio que arme el descompresor con nuestro gancho, para saber cual creo");
            Check(Codigo.Llama(l, "IMp3FrameDecompressor.Dispose") || Codigo.Llama(l, "IDisposable.Dispose"),
                  "y si la apertura falla, se cierra AHI MISMO (antes se quedaba para el recolector, y eso cerraba la rocola)");
        }
        Type TLm = Nes("LectorMedida");
        Check(TLm != null, "y el repaso de la biblioteca usa ese camino");
        MethodInfo mMed = TF.GetMethod("MeasureFile", BindingFlags.Instance | BindingFlags.NonPublic);
        if (mMed != null && TLm != null)
        {
            List<string> l = Codigo.Llamadas(mMed);
            Check(Codigo.Llama(l, "LectorMedida..ctor"), "al medir una cancion se abre con el lector que no deja fugas");
            Check(!Codigo.Llama(l, "AudioFileReader..ctor"), "y ya no directamente con el de NAudio, que si las dejaba");
        }

        // =========================================================================================
        Console.WriteLine("\n  4) Y MEDIR SIGUE DANDO EXACTAMENTE LO MISMO\n");
        // El lector nuevo es el MISMO montaje que hace NAudio por dentro. Se comprueba contra el de NAudio,
        // con un archivo de verdad, muestra por muestra.
        string[] wavs = Directory.Exists("wav") ? Directory.GetFiles("wav", "*.wav") : new string[0];
        Array.Sort(wavs);
        Check(wavs.Length > 0, "hay canciones de prueba con las que comparar");
        if (wavs.Length > 0 && TLm != null)
        {
            string w = wavs[0];
            object lm = Activator.CreateInstance(TLm, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                                                 null, new object[] { w }, null);
            float[] mias = new float[16384], suyas = new float[16384];
            int n1, n2; TimeSpan t1, t2; int rate1, rate2;
            try
            {
                rate1 = ((WaveFormat)TLm.GetProperty("WaveFormat").GetValue(lm, null)).SampleRate;
                t1 = (TimeSpan)TLm.GetProperty("TotalTime").GetValue(lm, null);
                n1 = (int)TLm.GetMethod("Read").Invoke(lm, new object[] { mias, 0, mias.Length });
            }
            finally { ((IDisposable)lm).Dispose(); }
            using (AudioFileReader afr = new AudioFileReader(w))
            { rate2 = afr.WaveFormat.SampleRate; t2 = afr.TotalTime; n2 = afr.Read(suyas, 0, suyas.Length); }
            int dif = 0; for (int i = 0; i < mias.Length; i++) if (mias[i] != suyas[i]) dif++;
            Console.WriteLine("     " + Path.GetFileNameWithoutExtension(w) + ": " + n1 + " muestras, "
                              + rate1 + " Hz, " + t1.TotalSeconds.ToString("0.00") + "s   |   diferencias: " + dif);
            Check(rate1 == rate2 && t1 == t2, "el lector nuevo da la misma frecuencia y la misma duracion");
            Check(n1 == n2 && dif == 0, "y exactamente las mismas muestras: no cambia lo que se mide");
        }

        // =========================================================================================
        Console.WriteLine("\n  5) Y LA CANCION QUE SUENA SE ABRE POR EL MISMO CAMINO\n");
        // El repaso de la biblioteca ya no dejaba descompresores tirados, pero la cancion que SUENA seguia
        // abriendose con el lector de NAudio de siempre, que tiene la misma trampa por partida doble: NAudio
        // arma el descompresor de MP3 y despues sigue preparandose; si algo falla en ese hueco -una cabecera
        // rota- no cierra ni el descompresor ni el archivo. Se abren menos archivos por aqui que en el
        // repaso, pero se abren TODOS los que alguien pide en el bar, y cuando esto revienta se lleva la
        // rocola entera por delante desde un hilo donde nadie puede recogerlo.
        Type TFas = Nes("FileAudioSource");
        Check(TFas != null, "esta el lector con el que suena la musica");
        if (TFas != null)
        {
            List<string> l = new List<string>();
            foreach (ConstructorInfo c in TFas.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                l.AddRange(Codigo.Llamadas(c));
            Check(Codigo.Llama(l, "Form1.AbreMp3"),
                  "los MP3 que suenan se abren por el camino que no deja descompresores tirados");
            Check(Codigo.Llama(l, "Mp3FileReader.Dispose") || Codigo.Llama(l, "IDisposable.Dispose") || Codigo.Llama(l, "Stream.Dispose"),
                  "y si lo de despues falla, el lector se cierra ahi mismo en vez de esperar al recolector");
        }

        // Y lo que se oye no cambia: para todo lo que no es MP3 el camino sigue siendo el de siempre, y se
        // comprueba muestra a muestra contra NAudio.
        if (wavs.Length > 0 && TFas != null)
        {
            string w = wavs[0];
            object fs = Activator.CreateInstance(TFas, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                                                 null, new object[] { w }, null);
            float[] mias = new float[16384], suyas = new float[16384];
            int n1, n2; TimeSpan t1, t2; int rate1, rate2;
            try
            {
                rate1 = ((WaveFormat)TFas.GetProperty("WaveFormat").GetValue(fs, null)).SampleRate;
                t1 = (TimeSpan)TFas.GetProperty("TotalTime").GetValue(fs, null);
                TFas.GetProperty("Volume").SetValue(fs, 1f, null);
                n1 = (int)TFas.GetMethod("Read").Invoke(fs, new object[] { mias, 0, mias.Length });
            }
            finally { ((IDisposable)fs).Dispose(); }
            using (AudioFileReader afr = new AudioFileReader(w))
            { rate2 = afr.WaveFormat.SampleRate; t2 = afr.TotalTime; afr.Volume = 1f; n2 = afr.Read(suyas, 0, suyas.Length); }
            int dif = 0; for (int i = 0; i < mias.Length; i++) if (mias[i] != suyas[i]) dif++;
            Console.WriteLine("     " + Path.GetFileNameWithoutExtension(w) + ": " + n1 + " muestras, "
                              + rate1 + " Hz, " + t1.TotalSeconds.ToString("0.00") + "s   |   diferencias: " + dif);
            Check(rate1 == rate2 && t1 == t2 && n1 == n2 && dif == 0,
                  "y lo que suena es exactamente lo mismo que antes: ni una muestra de diferencia");
        }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}
