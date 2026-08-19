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

    // ---- lectura del ejecutable ya compilado ----
    static int Largo1(int op)
    {
        if (op >= 0x2B && op <= 0x37) return 1;
        if (op >= 0x38 && op <= 0x44) return 4;
        switch (op)
        {
            case 0x0E: case 0x10: case 0x11: case 0x12: case 0x13: case 0x1F: case 0xDE: return 1;
            case 0x20: case 0x22: case 0x27: case 0x28: case 0x29: case 0x6F: case 0x70: case 0x71:
            case 0x72: case 0x73: case 0x74: case 0x75: case 0x79: case 0x7B: case 0x7C: case 0x7D:
            case 0x7E: case 0x7F: case 0x80: case 0x81: case 0x8C: case 0x8D: case 0x8F: case 0xA3:
            case 0xA4: case 0xA5: case 0xC2: case 0xC6: case 0xD0: return 4;
            case 0x21: case 0x23: return 8;
            default: return 0;
        }
    }
    static int Largo2(int op2)
    {
        switch (op2)
        {
            case 0x06: case 0x07: case 0x15: case 0x16: case 0x1C: return 4;
            case 0x09: case 0x0A: case 0x0B: case 0x0C: case 0x0D: case 0x0E: return 2;
            case 0x12: case 0x19: return 1;
            default: return 0;
        }
    }
    // Todo lo que llama un metodo, con el nombre del tipo delante ("Array.Copy", "Mp3FileReader..ctor").
    static List<string> Llamadas(MethodBase m)
    {
        List<string> r = new List<string>();
        MethodBody b = null; try { b = m.GetMethodBody(); } catch { }
        if (b == null) return r;
        byte[] il = null; try { il = b.GetILAsByteArray(); } catch { }
        if (il == null) return r;
        Module mod = m.Module; int i = 0;
        while (i < il.Length)
        {
            int op = il[i];
            if (op == 0xFE) { if (i + 1 >= il.Length) break; i += 2 + Largo2(il[i + 1]); continue; }
            if (op == 0x45) { int n = BitConverter.ToInt32(il, i + 1); i += 1 + 4 + 4 * n; continue; }
            int len = Largo1(op);
            if ((op == 0x28 || op == 0x6F || op == 0x73) && i + 4 < il.Length)
            {
                try
                {
                    MethodBase mb = mod.ResolveMethod(BitConverter.ToInt32(il, i + 1));
                    r.Add((mb.DeclaringType != null ? mb.DeclaringType.Name : "?") + "." + mb.Name);
                }
                catch { }
            }
            i += 1 + len;
        }
        return r;
    }
    static bool Llama(List<string> l, string q) { foreach (string s in l) if (s == q) return true; return false; }

    // Lo que llama un metodo MAS lo que llaman las funciones sueltas que el compilador le saca a metodos
    // aparte con el nombre entre picos (<AbreMp3>m__0). Sin esto, un delegado esconde lo que hace dentro.
    static List<string> LlamadasConAyudantes(string metodo)
    {
        List<string> r = new List<string>();
        BindingFlags bf = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        List<Type> tipos = new List<Type>(); tipos.Add(TF);
        tipos.AddRange(TF.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
        foreach (Type t in tipos)
            foreach (MethodInfo m in t.GetMethods(bf))
                if (m.Name == metodo || m.Name.IndexOf("<" + metodo + ">", StringComparison.Ordinal) >= 0
                    || t.Name.IndexOf("<" + metodo + ">", StringComparison.Ordinal) >= 0)
                    r.AddRange(Llamadas(m));
        return r;
    }

    // ¿Copia en bloque SOBRE EL BUFFER QUE LE DAN? Se mira si justo antes de la llamada se carga el primer
    // argumento (ldarg.1, que es el buffer). Vaciar o copiar arrays PROPIOS dentro de un Read esta bien; lo
    // que no vale es tocar en bloque el que llega de fuera, porque ese miente sobre lo que es.
    static bool CopiaEnBloqueSobreElBuffer(MethodBase m)
    {
        MethodBody b = null; try { b = m.GetMethodBody(); } catch { }
        if (b == null) return false;
        byte[] il = null; try { il = b.GetILAsByteArray(); } catch { }
        if (il == null) return false;
        Module mod = m.Module; int i = 0; int desdeArg = 99;
        while (i < il.Length)
        {
            int op = il[i];
            if (op == 0x03) desdeArg = 0;            // ldarg.1 = el buffer
            else if (desdeArg < 99) desdeArg++;
            if (op == 0xFE) { if (i + 1 >= il.Length) break; i += 2 + Largo2(il[i + 1]); continue; }
            if (op == 0x45) { int n = BitConverter.ToInt32(il, i + 1); i += 1 + 4 + 4 * n; continue; }
            int len = Largo1(op);
            if ((op == 0x28 || op == 0x6F) && i + 4 < il.Length && desdeArg <= 6)
            {
                try
                {
                    MethodBase mb = mod.ResolveMethod(BitConverter.ToInt32(il, i + 1));
                    string q = (mb.DeclaringType != null ? mb.DeclaringType.Name : "?") + "." + mb.Name;
                    if (q == "Array.Copy" || q == "Buffer.BlockCopy" || q == "Array.Clear") return true;
                }
                catch { }
            }
            i += 1 + len;
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
        MethodInfo mAbre = TF.GetMethod("AbreMp3", BindingFlags.Static | BindingFlags.NonPublic);
        Check(mAbre != null, "los MP3 se abren por un camino propio, no directamente con NAudio");
        if (mAbre != null)
        {
            List<string> l = LlamadasConAyudantes("AbreMp3");
            Check(Llama(l, "Mp3FileReader.CreateAcmFrameDecompressor"),
                  "se le dice a NAudio que arme el descompresor con nuestro gancho, para saber cual creo");
            Check(Llama(l, "IMp3FrameDecompressor.Dispose") || Llama(l, "IDisposable.Dispose"),
                  "y si la apertura falla, se cierra AHI MISMO (antes se quedaba para el recolector, y eso cerraba la rocola)");
        }
        Type TLm = Nes("LectorMedida");
        Check(TLm != null, "y el repaso de la biblioteca usa ese camino");
        MethodInfo mMed = TF.GetMethod("MeasureFile", BindingFlags.Instance | BindingFlags.NonPublic);
        if (mMed != null && TLm != null)
        {
            List<string> l = Llamadas(mMed);
            Check(Llama(l, "LectorMedida..ctor"), "al medir una cancion se abre con el lector que no deja fugas");
            Check(!Llama(l, "AudioFileReader..ctor"), "y ya no directamente con el de NAudio, que si las dejaba");
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

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}
