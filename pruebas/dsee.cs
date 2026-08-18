using System;
using System.Diagnostics;
using System.Reflection;
using NAudio.Wave;

// EL RECONSTRUCTOR DE AGUDOS (el DSEE): QUE SIGA SONANDO IGUAL, Y QUE CUESTE LA MITAD.
//
// Es lo mas caro de toda la cadena de sonido -el 73% del coste- y corre en el MISMO hilo que dibuja la
// pantalla, asi que cada milisegundo suyo es tiempo robado a los cuadros.
//
// El cambio: la musica es una senal REAL, y el espectro de una senal real es simetrico, o sea que media FFT
// compleja se estaba tirando a la basura. Metiendo el canal izquierdo en la parte real y el derecho en la
// imaginaria, UNA sola FFT trae los dos espectros, que luego se desenredan con esa misma simetria. Cuatro
// FFT por ventana pasan a ser dos.
//
// Eso es exacto sobre el papel, pero un error de un signo en el desenredo METERIA UN CANAL DENTRO DEL OTRO,
// que es justo lo que no se puede permitir. Por eso aqui no se comprueba "que suene parecido": se comprueban
// invariantes que un error de esos rompe a lo bestia.
class FuenteD : ISampleProvider
{
    private readonly float[] d; private readonly WaveFormat wf; private int pos;
    public FuenteD(float[] datos, int fs, int ch) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c)
    {
        int n = 0;
        while (n < c && pos < d.Length) { b[o + n] = d[pos]; pos++; n++; }
        return n;
    }
}

class Dsee
{
    const int FS = 44100;
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TSbr;

    static object Crea(params object[] args)
    {
        foreach (ConstructorInfo ci in TSbr.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            ParameterInfo[] ps = ci.GetParameters();
            if (ps.Length < args.Length) continue;
            bool encaja = true;
            for (int i = 0; i < args.Length; i++)
                if (!ps[i].ParameterType.IsInstanceOfType(args[i])) { encaja = false; break; }
            if (!encaja) continue;
            object[] full = new object[ps.Length];
            Array.Copy(args, full, args.Length);
            for (int i = args.Length; i < ps.Length; i++)
            {
                if (ps[i].ParameterType != typeof(int)) { encaja = false; break; }
                full[i] = 2048;   // la rama de tarjeta grafica pide ademas el tamano de ventana
            }
            if (encaja) { try { return ci.Invoke(full); } catch { } }
        }
        return null;
    }

    // Una tanda de musica de mentira con la forma que importa: mucha energia abajo, poca arriba y un
    // ACANTILADO a 16 kHz (lo que deja un MP3 de 128k). Sin acantilado el DSEE no reconstruye nada.
    static float[] Mono(int muestras, int semilla, float amplitud)
    {
        float[] d = new float[muestras];
        Random r = new Random(semilla);
        double[] fase = new double[40];
        for (int k = 0; k < 40; k++) fase[k] = r.NextDouble() * 6.283;
        for (int i = 0; i < muestras; i++)
        {
            double t = (double)i / FS, v = 0;
            for (int k = 0; k < 40; k++)
            {
                double hz = 55.0 * Math.Pow(1.18, k);
                if (hz > 16000) break;
                v += Math.Sin(2 * Math.PI * hz * t + fase[k]) * (0.9 / (1.0 + hz / 300.0));
            }
            float s = (float)(v * 0.3 * amplitud);
            d[i] = (s > 1f) ? 1f : (s < -1f ? -1f : s);
        }
        return d;
    }

    static float[] Entrelaza(float[][] canales)
    {
        int ch = canales.Length, n = canales[0].Length;
        float[] d = new float[n * ch];
        for (int i = 0; i < n; i++) for (int c = 0; c < ch; c++) d[i * ch + c] = canales[c][i];
        return d;
    }

    // Pasa la musica por el reconstructor y devuelve la salida por canales.
    static float[][] Pasa(float[] entrelazado, int ch, bool activo, out object tap)
    {
        ISampleProvider fu = new FuenteD(entrelazado, FS, ch);
        tap = Crea(fu);
        ISampleProvider p = (ISampleProvider)tap;
        TSbr.GetField("Active").SetValue(tap, activo);
        int n = entrelazado.Length;
        float[] salida = new float[n];
        int hecho = 0;
        float[] buf = new float[4096];
        while (hecho < n)
        {
            int pide = Math.Min(buf.Length, n - hecho);
            int got = p.Read(buf, 0, pide);
            if (got <= 0) break;
            Array.Copy(buf, 0, salida, hecho, got);
            hecho += got;
        }
        float[][] r = new float[ch][];
        for (int c = 0; c < ch; c++) { r[c] = new float[hecho / ch]; for (int i = 0; i < hecho / ch; i++) r[c][i] = salida[i * ch + c]; }
        return r;
    }

    static double MaxDif(float[] a, float[] b)
    {
        int n = Math.Min(a.Length, b.Length);
        double m = 0;
        for (int i = 0; i < n; i++) { double d = Math.Abs(a[i] - b[i]); if (d > m) m = d; }
        return m;
    }
    static double MaxAbs(float[] a) { double m = 0; for (int i = 0; i < a.Length; i++) { double d = Math.Abs(a[i]); if (d > m) m = d; } return m; }

    static double Mide(int veces, Action a)
    {
        a(); a();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        double mejor = double.MaxValue;
        for (int t = 0; t < 7; t++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < veces; i++) a();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / veces;
            if (ms < mejor) mejor = ms;
        }
        return mejor;
    }

    // Que metodos llama otro metodo, leyendo su codigo compilado. Sirve para comprobar que una funcion
    // sigue ENCHUFADA donde debe: un ayudante puede existir, estar documentado y no llamarlo nadie.
    static System.Collections.Generic.List<string> Llamadas(MethodBase m)
    {
        System.Collections.Generic.List<string> r = new System.Collections.Generic.List<string>();
        MethodBody b = null;
        try { b = m.GetMethodBody(); } catch { }
        if (b == null) return r;
        byte[] il;
        try { il = b.GetILAsByteArray(); } catch { return r; }
        if (il == null) return r;
        for (int i = 0; i + 4 < il.Length; i++)
            if (il[i] == 0x28 || il[i] == 0x6F)
            {
                try { MethodBase mb = m.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1)); if (mb != null) r.Add(mb.Name); }
                catch { }
            }
        return r;
    }
    static bool Llama(MethodBase m, string q) { foreach (string s in Llamadas(m)) if (s == q) return true; return false; }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        TSbr = TF.GetNestedType("SpectralTap", BindingFlags.NonPublic | BindingFlags.Public);
        Check(TSbr != null, "el reconstructor de agudos esta donde tiene que estar");
        if (TSbr == null) return 1;
        FieldInfo fCut = TSbr.GetField("cutHz", BindingFlags.Instance | BindingFlags.NonPublic);

        const int N = FS * 3;                     // 3 segundos
        float[] L = Mono(N, 11, 1.0f);
        float[] mitad = new float[N]; for (int i = 0; i < N; i++) mitad[i] = L[i] * 0.5f;
        float[] silencio = new float[N];
        object t0;

        Console.WriteLine("\n  1) APAGADO ES UN PASE DIRECTO (bit a bit)\n");
        float[] est = Entrelaza(new float[][] { L, mitad });
        float[][] off = Pasa(est, 2, false, out t0);
        Check(MaxDif(off[0], L) == 0.0 && MaxDif(off[1], mitad) == 0.0,
              "con el DSEE apagado la musica sale IDENTICA, sin tocar un solo bit");

        Console.WriteLine("\n  2) NO SE CUELA UN CANAL EN EL OTRO\n");
        // Con los dos canales dentro de la MISMA FFT, un error de signo al desenredarlos meteria el izquierdo
        // dentro del derecho. Se manda musica por la izquierda y SILENCIO por la derecha: si algo se cuela,
        // la derecha deja de estar callada y se ve al instante.
        float[][] sd = Pasa(Entrelaza(new float[][] { L, silencio }), 2, true, out t0);
        double fuga = MaxAbs(sd[1]), senal = MaxAbs(sd[0]);
        Console.WriteLine("     izquierda con musica: pico " + senal.ToString("0.0000")
                          + "   derecha en silencio: pico " + fuga.ToString("0.00000000"));
        Check(senal > 0.05, "por la izquierda sale musica (si no, esta prueba no probaria nada)");
        Check(fuga < 1e-6, "por la derecha no se cuela NADA de la izquierda");

        // Y al reves, por si el error fuera del otro lado.
        float[][] ds = Pasa(Entrelaza(new float[][] { silencio, L }), 2, true, out t0);
        Console.WriteLine("     al reves: derecha con musica pico " + MaxAbs(ds[1]).ToString("0.0000")
                          + "   izquierda en silencio pico " + MaxAbs(ds[0]).ToString("0.00000000"));
        Check(MaxAbs(ds[0]) < 1e-6, "y por la izquierda tampoco se cuela nada de la derecha");

        Console.WriteLine("\n  3) CADA CANAL SE TRATA POR SU CUENTA\n");
        // La derecha lleva la MISMA musica a la mitad de volumen. El reconstructor mide proporciones, asi que
        // debe devolver exactamente la mitad. Si los canales se mezclaran, esta proporcion se rompe.
        float[][] mix = Pasa(Entrelaza(new float[][] { L, mitad }), 2, true, out t0);
        double peor = 0;
        for (int i = 0; i < mix[0].Length; i++) { double d = Math.Abs(mix[1][i] - mix[0][i] * 0.5); if (d > peor) peor = d; }
        Console.WriteLine("     la derecha deberia ser la mitad exacta de la izquierda; se desvia como mucho " + peor.ToString("0.00000000"));
        Check(peor < 1e-5, "el canal a mitad de volumen sale a la mitad exacta (no hay mezcla entre canales)");

        Console.WriteLine("\n  4) ESTEREO Y MONO DAN LO MISMO\n");
        // El camino de dos canales (una FFT para los dos) y el de un canal suelto son CODIGO DISTINTO.
        // Con la misma musica tienen que dar el mismo resultado: asi cada uno vigila al otro.
        float[][] mo = Pasa(L, 1, true, out t0);
        float[][] es2 = Pasa(Entrelaza(new float[][] { L, L }), 2, true, out t0);
        double dif = MaxDif(mo[0], es2[0]);
        // El limite es la DECIMA PARTE del ultimo bit de 16, que es la resolucion real de la tarjeta de
        // sonido: por debajo de ahi no hay nada que oir. No se pide que salga bit a bit igual porque con los
        // dos canales dentro de una misma FFT el redondeo de los flotantes ya no puede recorrer el mismo
        // camino; lo que se exige es que la diferencia se quede muy por debajo de lo audible.
        double lsb = 1.0 / 32768;
        Console.WriteLine("     mismo audio por mono y por estereo: se desvian como mucho " + dif.ToString("0.00000000")
                          + "   (el ultimo bit de 16 vale " + lsb.ToString("0.00000000") + ")");
        Check(dif < lsb / 10, "el camino de estereo da lo mismo que el de mono, muy por debajo del ultimo bit");
        Check(MaxDif(es2[0], es2[1]) < lsb / 10, "y los dos canales de un estereo identico salen practicamente identicos");

        Console.WriteLine("\n  5) UNA CANCION COMPLETA NO SE TOCA\n");
        // Sin acantilado (WAV o 320k) el DSEE debe pasar la musica INTACTA: no inventar agudos donde ya los hay.
        float[] completa = new float[N];
        Random rr = new Random(7);
        for (int i = 0; i < N; i++)
        {
            double t = (double)i / FS, v = 0;
            for (int k = 0; k < 60; k++) { double hz = 55.0 * Math.Pow(1.105, k); if (hz > 21000) break; v += Math.Sin(2 * Math.PI * hz * t + k) * (0.9 / (1.0 + hz / 2000.0)); }
            v += (rr.NextDouble() - 0.5) * 0.05;
            completa[i] = (float)Math.Max(-1, Math.Min(1, v * 0.2));
        }
        float[][] cs = Pasa(Entrelaza(new float[][] { completa, completa }), 2, true, out t0);
        float cutC = (float)fCut.GetValue(t0);
        // El solape-y-suma NO desplaza la musica: las dos ventanas raiz-de-Hann suman exactamente 1, asi que
        // la muestra n sale en el sitio n. Solo se saltan las primeras, que aun no recibieron sus dos solapes.
        int cola = 4096;
        double difC = 0;
        for (int i = cola; i < cs[0].Length - cola; i++) { double d = Math.Abs(cs[0][i] - completa[i]); if (d > difC) difC = d; }
        Console.WriteLine("     acantilado medido " + cutC.ToString("0") + " Hz (>=19300 = completa)   se desvia como mucho " + difC.ToString("0.0000000"));
        Check(cutC >= 19300f, "una cancion con todos sus agudos se reconoce como completa");
        Check(difC < 1e-4, "y sale tal cual entro: no se le inventa nada");

        Console.WriteLine("\n  6) A UNA CANCION POBRE SI SE LE RECONSTRUYE\n");
        float[][] pobre = Pasa(Entrelaza(new float[][] { L, L }), 2, true, out t0);
        float cutP = (float)fCut.GetValue(t0);
        PropertyInfo pMej = TSbr.GetProperty("Mejorando");
        bool mejorando = (bool)pMej.GetValue(t0, null);
        Console.WriteLine("     acantilado medido " + cutP.ToString("0") + " Hz (la musica de prueba corta a 16000)");
        Check(cutP > 14000f && cutP < 19300f, "se localiza el acantilado del codec donde de verdad esta");
        Check(mejorando, "y la cancion queda marcada como MEJORADA");
        Check(MaxDif(pobre[0], L) > 1e-4, "la salida no es igual a la entrada: de verdad se le anadio algo arriba");

        Console.WriteLine("\n  7) LO QUE CUESTA\n");
        int bloque = FS * 2 / 20;                 // 50 ms de estereo, que es el bloque real de la rocola
        float[] buf = new float[bloque];
        object tOn, tOff;
        ISampleProvider pOn = (ISampleProvider)(tOn = null);
        float[] largo = Entrelaza(new float[][] { Mono(FS * 30, 3, 1f), Mono(FS * 30, 4, 0.9f) });
        ISampleProvider fuOn = new FuenteD(largo, FS, 2);
        tOn = Crea(fuOn); TSbr.GetField("Active").SetValue(tOn, true); pOn = (ISampleProvider)tOn;
        ISampleProvider fuOff = new FuenteD(largo, FS, 2);
        tOff = Crea(fuOff); TSbr.GetField("Active").SetValue(tOff, false);
        ISampleProvider pOff = (ISampleProvider)tOff;
        double msOn = Mide(60, delegate { pOn.Read(buf, 0, bloque); });
        double msOff = Mide(60, delegate { pOff.Read(buf, 0, bloque); });
        Console.WriteLine("     bloque de 50 ms: encendido " + msOn.ToString("0.000") + " ms, apagado " + msOff.ToString("0.000") + " ms");
        Console.WriteLine("     por segundo de musica: " + (msOn * 20).ToString("0.00") + " ms  ("
                          + (msOn * 20 / 10).ToString("0.0") + "% de un nucleo en esta maquina)");
        Check(msOff < msOn * 0.2, "apagado es de verdad un pase directo (cuesta una fraccion de encendido)");

        Console.WriteLine("\n  8) EL REALCE DE AGUDOS NO EMPUJA DOS VECES LO MISMO\n");
        // El realce le da MAS brillo a una cancion dañada justamente por estar dañada. El reconstructor le
        // re-crea la banda por la misma razon. Y en la cadena el realce va DESPUES, asi que si los dos van a
        // su aire acaba echandole brillo encima a unos agudos que ya no faltaban.
        object F = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(TF);
        FieldInfo fEq = TF.GetField("eqCache", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fOn = TF.GetField("sbrOn", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fAll = TF.GetField("sbrAllowed", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo mExc = TF.GetMethod("ExciteFactorFor", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fEq == null || fOn == null || fAll == null || mExc == null)
        {
            Check(false, "se puede consultar cuanto realce le toca a cada cancion");
        }
        else
        {
            int iCut = (int)TF.GetField("EqCut", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            int len = (int)TF.GetField("EqLen", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            System.Collections.Generic.Dictionary<string, float[]> cache = new System.Collections.Generic.Dictionary<string, float[]>();
            fEq.SetValue(F, cache);
            fAll.SetValue(F, true);
            Func<string, float, bool, float> factor = delegate (string ruta, float corte, bool dsee)
            {
                float[] c = new float[len];
                for (int i = 0; i < len; i++) c[i] = -30f;
                c[iCut] = corte;
                cache[ruta] = c;
                fOn.SetValue(F, dsee);
                return (float)mExc.Invoke(F, new object[] { ruta });
            };
            float danadaSin = factor("a.mp3", 15000f, false), danadaCon = factor("a.mp3", 15000f, true);
            float mediaSin = factor("b.mp3", 17000f, false), mediaCon = factor("b.mp3", 17000f, true);
            float sanaSin = factor("c.mp3", 20000f, false), sanaCon = factor("c.mp3", 20000f, true);
            Console.WriteLine("     realce que le toca a cada cancion (reconstructor apagado / encendido):");
            Console.WriteLine("        cortada a 15000 Hz (muy dañada): " + danadaSin.ToString("0.00") + "  /  " + danadaCon.ToString("0.00"));
            Console.WriteLine("        cortada a 17000 Hz:              " + mediaSin.ToString("0.00") + "  /  " + mediaCon.ToString("0.00"));
            Console.WriteLine("        completa (20000 Hz):             " + sanaSin.ToString("0.00") + "  /  " + sanaCon.ToString("0.00"));
            Check(danadaSin > 1.2f, "con el reconstructor apagado, una cancion dañada si recibe realce de sobra");
            Check(danadaCon < danadaSin, "con el reconstructor encendido recibe MENOS: los agudos ya se los devolvio el");
            Check(danadaCon > sanaCon, "pero no se le quita del todo (lo reconstruido no es el detalle original)");
            Check(Math.Abs(sanaSin - sanaCon) < 0.001f, "y a una cancion COMPLETA no le cambia nada: el reconstructor ni la toca");
        }

        Console.WriteLine("\n  9) LA TANGENTE RAPIDA DEL REALCE SIGUE ENCHUFADA\n");
        // El realce de agudos satura la banda alta con una tangente hiperbolica, UNA VEZ POR MUESTRA Y CANAL
        // (88.200 veces por segundo). Hay una version aproximada bastante mas barata hecha justo para eso...
        // que estaba escrita, documentada y SIN QUE LA LLAMARA NADIE: el realce seguia usando la de la
        // libreria matematica. Aqui se comprueba las dos cosas: que se parece lo suficiente, y que de verdad
        // se llama desde donde toca.
        MethodInfo mTanh = TF.GetMethod("TanhRapida", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Check(mTanh != null, "existe la tangente rapida");
        if (mTanh != null)
        {
            double peorT = 0; float xPeor = 0;
            for (float x = -6f; x <= 6f; x += 0.002f)
            {
                float ap = (float)mTanh.Invoke(null, new object[] { x });
                double d = Math.Abs(ap - Math.Tanh(x));
                if (d > peorT) { peorT = d; xPeor = x; }
            }
            Console.WriteLine("     se aparta de la de verdad como mucho " + peorT.ToString("0.0000") + " (en x=" + xPeor.ToString("0.00") + ")");
            Check(peorT < 0.03, "dibuja practicamente la misma curva");
            Check((float)mTanh.Invoke(null, new object[] { float.NaN }) == -1f, "y un NaN no la atraviesa (un archivo roto no deja el sonido pegado)");

            Type TLvl = TF.GetNestedType("LevelTap", BindingFlags.NonPublic | BindingFlags.Public);
            MethodInfo mRead = (TLvl != null) ? TLvl.GetMethod("Read") : null;
            Check(mRead != null && Llama(mRead, "TanhRapida"), "el realce de agudos LA LLAMA de verdad");
            Check(mRead != null && !Llama(mRead, "Tanh"), "y ya no llama a la de la libreria");

            // La red neuronal SI debe seguir con la de verdad: se entreno con ella.
            Type TRed = TF.GetNestedType("RedChica", BindingFlags.NonPublic | BindingFlags.Public);
            MethodInfo mPred = (TRed != null) ? TRed.GetMethod("Predict") : null;
            Check(mPred != null && Llama(mPred, "Tanh"), "la red neuronal, en cambio, sigue con la tangente exacta (se entreno con ella)");
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
