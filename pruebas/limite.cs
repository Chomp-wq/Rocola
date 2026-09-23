using System;
using System.Collections.Generic;
using System.Reflection;
using NAudio.Wave;

// EL LIMITADOR: ¿DE VERDAD NO RECORTA?
//
// Es lo ultimo de la cadena antes de salir a la tarjeta, y es lo unico que puede meter distorsion AUDIBLE:
// baja la ganancia suave ANTES del pico gracias a una pre-escucha... pero si un transitorio se le cuela, hay
// una red de seguridad que corta en seco, y cortar en seco SI se oye.
//
// Aqui se le mete material duro -golpes con arranque instantaneo, que es lo peor para un limitador- y se
// cuenta cuantas muestras acaban tocando esa red de seguridad. Cuantas menos, mas limpio suena.
class Fuente3 : ISampleProvider
{
    private readonly float[] d; private readonly WaveFormat wf; private int pos;
    public Fuente3(float[] datos, int fs, int ch) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c)
    { int n = 0; while (n < c && pos < d.Length) { b[o + n] = d[pos]; pos++; n++; } return n; }
}

class Limite
{
    const int FS = 44100, CH = 2;
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static object Crea(Type t, params object[] args)
    {
        foreach (ConstructorInfo ci in t.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            if (ci.GetParameters().Length == args.Length)
                try { return ci.Invoke(args); } catch { }
        return null;
    }

    // Golpes con ARRANQUE INSTANTANEO: de silencio a casi el tope en una muestra. Es lo peor que se le puede
    // dar a un limitador con pre-escucha, porque no hay aviso ninguno.
    static float[] Golpes(int segundos, float pico)
    {
        int n = FS * segundos * CH;
        float[] d = new float[n];
        Random r = new Random(9);
        for (int i = 0; i < n; i += CH)
        {
            int m = i / CH;
            double t = (double)m / FS;
            double v = 0.18 * Math.Sin(2 * Math.PI * 110.0 * t);              // fondo
            int desde = m % (FS / 3);                                          // un golpe cada tercio de segundo
            if (desde < FS / 40)
            {
                double env = Math.Exp(-desde / (FS * 0.006));                  // ataque instantaneo, caida rapida
                v += pico * env * Math.Sin(2 * Math.PI * 62.0 * desde / FS);
            }
            v += (r.NextDouble() - 0.5) * 0.01;
            float s = (float)v;
            for (int c = 0; c < CH; c++) d[i + c] = s;
        }
        return d;
    }

    // Pasa el audio por un limitador recien montado y devuelve la salida.
    static float[] Pasa(Type TLim, float[] datos)
    {
        ISampleProvider fu = new Fuente3(datos, FS, CH);
        object lim = Crea(TLim, fu, 0.98f, 3.0);
        ISampleProvider p = (ISampleProvider)lim;
        float[] sal = new float[datos.Length];
        int hecho = 0; float[] buf = new float[4096];
        int got;
        while (hecho < sal.Length && (got = p.Read(buf, 0, Math.Min(buf.Length, sal.Length - hecho))) > 0)
        { Array.Copy(buf, 0, sal, hecho, got); hecho += got; }
        return sal;
    }

    static float[] LeeTodo(ISampleProvider p, int tope)
    {
        List<float> todo = new List<float>();
        float[] buf = new float[514];   // numero pequeno y par: obliga a vaciar la cola en varias lecturas
        int n;
        while ((n = p.Read(buf, 0, buf.Length)) > 0)
        {
            for (int i = 0; i < n; i++) todo.Add(buf[i]);
            if (todo.Count > tope) break;
        }
        return todo.ToArray();
    }

    static void PruebaCola(Type tipo, bool esLimitador)
    {
        const int cuadros = 257;
        float[] d = new float[cuadros * CH];
        for (int f = 0; f < cuadros; f++)
        {
            d[f * CH] = (f + 1) / 4000f;
            d[f * CH + 1] = -(f + 1) / 5000f;
        }
        object o = esLimitador
            ? Crea(tipo, new Fuente3(d, FS, CH), 0.98f, 3.0)
            : Crea(tipo, new Fuente3(d, FS, CH));
        ISampleProvider p = o as ISampleProvider;
        PropertyInfo pd = (tipo != null) ? tipo.GetProperty("DelayFrames") : null;
        Check(p != null && pd != null, tipo != null ? tipo.Name + " declara cuanto retarda" : "existe el proveedor con retardo");
        if (p == null || pd == null) return;
        int retardo = (int)pd.GetValue(o, null);
        float[] sal = LeeTodo(p, d.Length + retardo * CH + 1024);
        Check(sal.Length == d.Length + retardo * CH,
              tipo.Name + " devuelve tambien las " + retardo + " muestras que quedaban en su colchon");
        bool ceros = sal.Length >= retardo * CH;
        for (int i = 0; ceros && i < retardo * CH; i++) if (sal[i] != 0f) ceros = false;
        Check(ceros, tipo.Name + " conserva su retardo inicial");
        bool entero = sal.Length == d.Length + retardo * CH;
        for (int i = 0; entero && i < d.Length; i++) if (sal[retardo * CH + i] != d[i]) entero = false;
        Check(entero, tipo.Name + " no corta ni cambia el final de una senal que no necesita reparacion");

        object vacio = esLimitador
            ? Crea(tipo, new Fuente3(new float[0], FS, CH), 0.98f, 3.0)
            : Crea(tipo, new Fuente3(new float[0], FS, CH));
        float[] nada = LeeTodo((ISampleProvider)vacio, retardo * CH + 16);
        Check(nada.Length == 0, tipo.Name + " no inventa una cola para una fuente vacia");
    }

    // Cuanta energia hay FUERA del tono que se metio. Se compara la energia del tono (por correlacion con
    // un seno y un coseno de esa frecuencia) contra la energia total: lo que sobra es lo que el limitador
    // ha anadido, o sea distorsion.
    static double Thd(float[] x, int ini, double hz)
    {
        int n = 0; double sr = 0, si = 0, tot = 0;
        for (int i = ini; i < x.Length; i += CH)
        {
            double t = (double)n / FS, v = x[i];
            sr += v * Math.Cos(2 * Math.PI * hz * t);
            si += v * Math.Sin(2 * Math.PI * hz * t);
            tot += v * v;
            n++;
        }
        if (n == 0 || tot <= 0) return 0;
        double eFund = 2.0 * (sr * sr + si * si) / n;   // energia del tono
        double resto = tot - eFund;
        if (resto < 0) resto = 0;
        return Math.Sqrt(resto / tot);
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        // Los dos limitadores: el de pico real, que es el que va en la cadena, y el de muestras de siempre, que
        // se queda como referencia. A los dos se les exige lo mismo.
        Type TReal = TF.GetNestedType("LimitadorPicoReal", BindingFlags.NonPublic | BindingFlags.Public);
        Type TViejo = TF.GetNestedType("LookaheadLimiter", BindingFlags.NonPublic | BindingFlags.Public);
        Type TDec = TF.GetNestedType("DeclipProvider", BindingFlags.NonPublic | BindingFlags.Public);
        Check(TReal != null || TViejo != null, "el limitador esta donde tiene que estar");
        Check(TDec != null, "el reparador de recortes esta donde tiene que estar");
        if ((TReal == null && TViejo == null) || TDec == null) return 1;

        Console.WriteLine("\n  0) AL TERMINAR, LOS COLCHONES SE VACIAN COMPLETOS\n");
        PruebaCola(TDec, false);
        foreach (Type TLim in new Type[] { TReal, TViejo })
        {
        if (TLim == null) continue;
        Console.WriteLine("\n  ===== " + TLim.Name + " =====");
        PruebaCola(TLim, true);

        // 1) TONO SOSTENIDO POR ENCIMA DEL TECHO. Con un tono fijo el limitador se asienta en una ganancia
        //    constante y la salida deberia ser el MISMO tono, mas bajito y limpio. Si en vez de bajar la
        //    ganancia recorta, aparecen armonicos que no estaban: eso es lo que se oye como aspereza.
        Console.WriteLine("\n  1) UN TONO FUERTE SOSTENIDO: ¿lo baja o lo recorta?\n");
        foreach (float amp in new float[] { 1.2f, 2.0f, 3.5f })
        {
            float[] d = new float[FS * 2 * CH];
            for (int i = 0; i < d.Length; i += CH)
            { float v = (float)(amp * Math.Sin(2 * Math.PI * 200.0 * (i / CH) / FS)); for (int c = 0; c < CH; c++) d[i + c] = v; }
            float[] sal = Pasa(TLim, d);
            // se mide en la segunda mitad, ya asentado
            int ini = sal.Length / 2; ini -= ini % CH;
            double thd = Thd(sal, ini, 200.0);
            Console.WriteLine("     tono a " + amp.ToString("0.0") + " (techo 0.98):  distorsion "
                              + (thd * 100).ToString("0.000") + "%");
            Check(thd < 0.005, "un tono a " + amp.ToString("0.0") + " sale limpio (menos del 0.5% de distorsion)");
        }

        // 2) GOLPES SECOS. Aqui el limitador no puede asentarse: llega un pico de la nada.
        //
        //    OJO CON LEER ESTO: que haya muestras EN el techo no es un fallo, es el limitador haciendo su
        //    trabajo -para eso esta el techo-. Lo que delata el recorte es que la punta de la onda se quede
        //    PLANA un rato largo: ahi ya no se esta bajando la ganancia, se esta cortando. Un milisegundo de
        //    aplanado en la punta de un golpe no se oye; dos y pico, si, y suena a aspereza.
        Console.WriteLine("\n  2) GOLPES SECOS: ¿se aplana la punta de la onda?\n");
        foreach (float pico in new float[] { 1.4f, 2.2f, 3.5f })
        {
            float[] sal = Pasa(TLim, Golpes(4, pico));
            int corrida = 0, peorCorrida = 0, aplanadas = 0;
            for (int i = 0; i < sal.Length; i += CH)
            {
                if (Math.Abs(sal[i]) >= 0.98f - 1e-5f) { corrida++; aplanadas++; if (corrida > peorCorrida) peorCorrida = corrida; }
                else corrida = 0;
            }
            Console.WriteLine("     golpes a " + pico.ToString("0.0") + ":  la punta plana mas larga son "
                              + peorCorrida + " muestras (" + (peorCorrida * 1000.0 / FS).ToString("0.00") + " ms)"
                              + "   y en total " + aplanadas + " muestras aplanadas");
            Check(peorCorrida <= 44, "con golpes a " + pico.ToString("0.0") + " la punta no se aplana ni un milisegundo");
        }
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
