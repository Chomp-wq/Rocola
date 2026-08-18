using System;
using System.Diagnostics;
using System.Reflection;
using NAudio.Wave;

// EL ATORON DE LA MUSICA: CUANTO PARA LA INTERFAZ CADA VEZ QUE HAY QUE DAR DE COMER AL SONIDO.
//
// La tarjeta de sonido de esta rocola se rellena DESDE EL MISMO HILO QUE DIBUJA la pantalla (en Windows XP
// sin Service Pack 1 no se puede hacer de otra forma sin que el programa truene). Cada vez que se vacia un
// bloque, la interfaz se para en seco a descifrar el MP3 y a pasarlo por toda la cadena de sonido: nivelado,
// ecualizador, reconstructor de agudos, bajo, limitador y dither.
//
// Con bloques de 200 ms, eso pasa cinco veces por segundo. Si cada vez cuesta mas de lo que dura un cuadro,
// se ve como un tiron. Aqui se mide cuanto cuesta de verdad UN bloque, con la cadena entera montada.
class Fuente : ISampleProvider
{
    private readonly float[] d; private readonly WaveFormat wf; private int pos;
    public Fuente(float[] datos, int fs, int ch) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c)
    { for (int i = 0; i < c; i++) { b[o + i] = d[pos]; pos++; if (pos >= d.Length) pos = 0; } return c; }
}

class Atoron
{
    const int FS = 44100, CH = 2;
    static Type TF;
    static Assembly asm;

    static object Crea(string nombre, params object[] args)
    {
        Type t = TF.GetNestedType(nombre, BindingFlags.NonPublic | BindingFlags.Public);
        if (t == null) return null;
        // Se busca el constructor por numero de argumentos y se prueba: asi da igual si la clase tiene
        // varios (SpectralTap, por ejemplo, tiene uno para reproducir y otro solo para analizar).
        // Las dos ramas no tienen la misma firma: la de tarjeta grafica pide ademas el tamano de ventana
        // (usa 4096 en su nivel alto). Se rellenan los enteros que sobren con 2048, el de siempre. Sin esto,
        // en esa rama no se montaba el reconstructor y la cadena se medía SIN el (numeros falsamente buenos).
        foreach (ConstructorInfo ci in t.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            ParameterInfo[] ps = ci.GetParameters();
            if (ps.Length < args.Length) continue;
            bool encaja = true;
            for (int i = 0; i < args.Length; i++)
                if (args[i] != null && !ps[i].ParameterType.IsInstanceOfType(args[i])) { encaja = false; break; }
            if (!encaja) continue;
            object[] full = new object[ps.Length];
            Array.Copy(args, full, args.Length);
            for (int i = args.Length; i < ps.Length; i++)
            {
                if (ps[i].ParameterType != typeof(int)) { encaja = false; break; }
                full[i] = 2048;
            }
            if (!encaja) continue;
            try { return ci.Invoke(full); } catch { }
        }
        Console.WriteLine("     (no se pudo montar " + nombre + ")");
        return null;
    }

    static double Mide(int veces, Action a)
    {
        a(); a();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        double mejor = double.MaxValue;
        for (int t = 0; t < 5; t++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < veces; i++) a();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / veces;
            if (ms < mejor) mejor = ms;
        }
        return mejor;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        Console.WriteLine("\n  midiendo: " + exe);

        // Un segundo de musica de mentira, pero con la forma de una de verdad (graves, medios y ruido).
        Random r = new Random(7);
        float[] src = new float[FS * CH];
        for (int i = 0; i < FS; i++)
        {
            double t = (double)i / FS;
            float v = (float)(0.35 * Math.Sin(2 * Math.PI * 55 * t) + 0.22 * Math.Sin(2 * Math.PI * 440 * t)
                            + 0.12 * Math.Sin(2 * Math.PI * 3200 * t) + 0.04 * (r.NextDouble() * 2 - 1));
            src[i * 2] = v; src[i * 2 + 1] = v * 0.92f;
        }

        // La cadena, montada como en el programa: fuente -> reconstructor -> nivelador -> bajo -> limitador -> dither
        object form = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(TF);
        // El nivelador guarda medidas en campos del formulario: se le dan los minimos para que no reviente.
        PonCampo(form, "levelLock", new object());
        PonCampo(form, "specVis", new float[8]); PonCampo(form, "specNow", new float[8]);
        PonCampo(form, "specPiso", new float[8]); PonCampo(form, "specGolpe", new float[8]);
        PonCampo(form, "auroraAmount", 0.55f);
        Type tq = TF.GetField("levelQ", BindingFlags.Instance | BindingFlags.NonPublic).FieldType;
        PonCampo(form, "levelQ", Activator.CreateInstance(tq));

        ISampleProvider fuente = new Fuente(src, FS, CH);
        ISampleProvider cad = fuente;

        object sbr = Crea("SpectralTap", cad);
        if (sbr != null) { sbr.GetType().GetField("Active").SetValue(sbr, true); cad = (ISampleProvider)sbr; }

        object tap = Crea("LevelTap", cad, form);
        Type tTap = tap.GetType();
        tTap.GetField("Gain").SetValue(tap, 1f);
        tTap.GetField("Master").SetValue(tap, 1f);
        tTap.GetField("Excite").SetValue(tap, 0.35f);
        cad = (ISampleProvider)tap;

        object bass = Crea("DeepBassProvider", cad, 0.5f);
        if (bass != null) cad = (ISampleProvider)bass;

        object lim = Crea("LookaheadLimiter", cad, 0.98f, 3.0);
        if (lim != null) cad = (ISampleProvider)lim;

        object dith = Crea("DitherWaveProvider16", cad);
        IWaveProvider salida = dith as IWaveProvider;

        Console.WriteLine("\n  UN BLOQUE DE SONIDO: lo que la pantalla se queda parada\n");
        Console.WriteLine("     un cuadro a 24 por segundo son 41,7 ms; a 60 por segundo, 16,7 ms\n");
        int[] bloquesMs = { 200, 100, 50, 25 };
        Console.WriteLine(string.Format("     {0,-14} {1,12} {2,14} {3,26}", "bloque", "cuesta", "veces/segundo", "y en el ordenador del bar"));
        Console.WriteLine("     " + new string('-', 76));
        foreach (int ms in bloquesMs)
        {
            int bytes = (FS * CH * 2) * ms / 1000;   // 16 bits
            byte[] buf = new byte[bytes];
            double t = Mide(20, delegate { salida.Read(buf, 0, bytes); });
            double bar = t * 4;   // el ordenador del bar es unas 4 veces mas lento
            Console.WriteLine(string.Format("     {0,4} ms       {1,7:0.00} ms {2,10:0.0}    {3,10:0.0} ms  {4}",
                                            ms, t, 1000.0 / ms, bar,
                                            bar > 41.7 ? "<<< SE COME UN CUADRO ENTERO" : (bar > 16.7 ? "<< se nota" : "")));
        }

        // Y cuanto de todo eso es cada pieza.
        Console.WriteLine("\n  DE QUE SE COMPONE (bloque de 200 ms)\n");
        int b200 = (FS * CH * 2) * 200 / 1000;
        int f200 = (FS * CH) * 200 / 1000;
        float[] fbuf = new float[f200];
        byte[] bbuf = new byte[b200];
        Fuente f2 = new Fuente(src, FS, CH);
        Fila("solo leer el audio (sin nada)", Mide(20, delegate { f2.Read(fbuf, 0, f200); }));
        if (sbr != null) { ISampleProvider s2 = (ISampleProvider)sbr; Fila("+ reconstructor de agudos", Mide(20, delegate { s2.Read(fbuf, 0, f200); })); }
        { ISampleProvider s3 = (ISampleProvider)tap; Fila("+ nivelador/ecualizador/realce", Mide(20, delegate { s3.Read(fbuf, 0, f200); })); }
        if (bass != null) { ISampleProvider s4 = (ISampleProvider)bass; Fila("+ bajo profundo", Mide(20, delegate { s4.Read(fbuf, 0, f200); })); }
        if (lim != null) { ISampleProvider s5 = (ISampleProvider)lim; Fila("+ limitador", Mide(20, delegate { s5.Read(fbuf, 0, f200); })); }
        Fila("+ paso a 16 bits con dither (la cadena entera)", Mide(20, delegate { salida.Read(bbuf, 0, b200); }));
        Console.WriteLine("\n     (cada linea lleva dentro todo lo anterior)\n");
        return 0;
    }

    static void Fila(string q, double ms) { Console.WriteLine(string.Format("     {0,-46} {1,8:0.00} ms", q, ms)); }
    static void PonCampo(object o, string n, object v)
    {
        FieldInfo f = TF.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic);
        if (f != null) f.SetValue(o, v);
    }
}
