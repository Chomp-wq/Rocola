using System;
using System.Reflection;

// LOS SONIDOS DE LA ROCOLA.
//
// La rocola dibuja un cielo que late con la musica, gira los discos en tres dimensiones y tiñe el fondo
// segun el genero. Y no emitia NI UN SOLO SONIDO PROPIO: se metia una moneda y no se oia nada. En un bar
// eso importa, porque una rocola es un mueble que hace ruido cuando la usas: por eso se sabe desde la
// barra, y de espaldas, que alguien acaba de pagar.
//
// COMO SE PRUEBA UN SONIDO SIN BOCINAS. Los efectos no son archivos: se fabrican con cuentas, y una
// cuenta se puede mirar. Esta bateria le pide a la rocola cada efecto, lo recibe como una tira de
// numeros, y le mide lo que un sonido tiene que cumplir. No dice si suena BONITO -eso lo estrena el
// bar- pero si dice si esta roto, y de las cinco maneras en que un efecto se rompe:
//
//   1) QUE NO SEA SILENCIO Y QUE NO REVIENTE. Un efecto mudo no se nota que falta hasta que alguien
//      mete una moneda en el bar lleno; uno que se pasa de la raya distorsiona sobre la musica.
//
//   2) QUE EMPIECE Y ACABE EN SILENCIO. Un sonido que arranca a mitad de onda hace CHASQUIDO al
//      empezar, y otro al acabar. Es el fallo mas comun de los efectos hechos a mano y el mas feo:
//      se oye como si la bocina se rompiera, no como un efecto.
//
//   3) QUE SE DISTINGAN ENTRE SI. La moneda y el golpe de la cola tienen que sonar DISTINTO: con los
//      ojos cerrados hay que poder decir cual de los dos fue. Sin esta prueba se acaba poniendole el
//      mismo pitido a todo, que es peor que no tener sonidos.
//
//   4) QUE LA MUSICA NO CAMBIE CUANDO NO SUENA NINGUNO. Es el 99% del tiempo. La mezcla tiene que
//      dejar pasar la cancion MUESTRA POR MUESTRA igual que hoy; si no, se estaria estropeando lo que
//      la gente vino a oir a cambio de un efecto que casi nunca suena.
//
//   5) QUE LA MUSICA SE AGACHE CON RAMPA, NO DE GOLPE. Bajarle un decibel a la cancion de un bloque
//      al siguiente es exactamente un escalon en mitad de la onda: otro chasquido. Tiene que bajar
//      poco a poco y volver poco a poco.
class Efectos
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const int FREC = 44100;
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static Type TEfectos, TEfecto, TMezcla;

    static Type Busca(Assembly a, string n)
    {
        foreach (Type t in a.GetTypes())
        {
            if (t.Name == n) return t;
            foreach (Type x in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)) if (x.Name == n) return x;
        }
        return null;
    }

    // Los seis, por su nombre. Si alguno no existe, se dice cual.
    static readonly string[] NOMBRES = { "Moneda", "Brazo", "Cola", "SinCredito", "Aguja", "Tic" };
    static readonly string[] COMO = { "la moneda", "el brazo mecanico", "la cancion a la cola",
                                      "el aviso de sin creditos", "la aguja del vinilo", "el tic de moverse" };

    static float[] Muestra(string nombre)
    {
        object cual = Enum.Parse(TEfecto, nombre);
        MethodInfo m = TEfectos.GetMethod("Muestra", TODO);
        return (float[])m.Invoke(null, new object[] { cual, FREC });
    }
    static double Segundos(string nombre)
    {
        object cual = Enum.Parse(TEfecto, nombre);
        MethodInfo m = TEfectos.GetMethod("Segundos", TODO);
        return Convert.ToDouble(m.Invoke(null, new object[] { cual }));
    }

    static float Pico(float[] x) { float m = 0f; for (int i = 0; i < x.Length; i++) { float a = Math.Abs(x[i]); if (a > m) m = a; } return m; }

    // LA HUELLA DE UN SONIDO: cuanta energia lleva en cada una de cuatro alturas (grave, cuerpo, medio,
    // agudo). Dos efectos que suenan distinto reparten su energia distinto, y eso se puede comparar sin
    // oirlos. Los filtros son de un polo, los mismos que usa la rocola en su cadena.
    static double[] Huella(float[] x, int fs)
    {
        double[] e = new double[4];
        double[] corte = { 180.0, 800.0, 3500.0 };
        double a0 = 1.0 - Math.Exp(-2.0 * Math.PI * corte[0] / fs);
        double a1 = 1.0 - Math.Exp(-2.0 * Math.PI * corte[1] / fs);
        double a2 = 1.0 - Math.Exp(-2.0 * Math.PI * corte[2] / fs);
        double l0 = 0, l1 = 0, l2 = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double v = x[i];
            l0 += (v - l0) * a0;          // por debajo de 180
            l1 += (v - l1) * a1;          // por debajo de 800
            l2 += (v - l2) * a2;          // por debajo de 3500
            e[0] += l0 * l0;
            e[1] += (l1 - l0) * (l1 - l0);
            e[2] += (l2 - l1) * (l2 - l1);
            e[3] += (v - l2) * (v - l2);
        }
        double s = e[0] + e[1] + e[2] + e[3];
        if (s <= 0) s = 1;
        for (int i = 0; i < 4; i++) e[i] /= s;
        return e;
    }
    // GOLPE O TONO: que parte de su fuerza suelta en los primeros 30 ms. Un golpe lo suelta casi todo
    // de una; un tono se sostiene. Dos sonidos pueden vivir en la misma altura -el golpe de la cola y
    // el aviso de sin creditos son los dos graves- y aun asi no confundirse nunca, porque uno GOLPEA y
    // el otro SUENA. La huella de altura sola no ve esa diferencia; esto si.
    static double Arranque(float[] x, int fs)
    {
        double todo = 0, principio = 0;
        int corte = (int)(0.030 * fs);
        for (int i = 0; i < x.Length; i++) { double e = (double)x[i] * x[i]; todo += e; if (i < corte) principio += e; }
        return (todo <= 0) ? 0 : principio / todo;
    }

    static double Distancia(double[] a, double[] b)
    {
        double d = 0; for (int i = 0; i < 4; i++) d += Math.Abs(a[i] - b[i]); return d;
    }

    // Una fuente de valor conocido, para poder comprobar QUE le hizo la mezcla a la musica.
    class Fuente : NAudio.Wave.ISampleProvider
    {
        private readonly float valor; private readonly int ch;
        public Fuente(float v, int canales) { valor = v; ch = canales; }
        public NAudio.Wave.WaveFormat WaveFormat { get { return NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(FREC, ch); } }
        public int Read(float[] b, int off, int n) { for (int i = 0; i < n; i++) b[off + i] = valor; return n; }
    }

    static object NuevaMezcla(NAudio.Wave.ISampleProvider fuente, float volumen)
    {
        object mz = Activator.CreateInstance(TMezcla, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                             null, new object[] { fuente }, null);
        TMezcla.GetField("Volumen", TODO).SetValue(mz, volumen);
        return mz;
    }
    static void Dispara(object mz, string nombre)
    {
        TMezcla.GetMethod("Dispara", TODO).Invoke(mz, new object[] { Enum.Parse(TEfecto, nombre) });
    }
    static float[] Corre(object mz, int bloques, int muestrasPorBloque)
    {
        NAudio.Wave.ISampleProvider p = (NAudio.Wave.ISampleProvider)mz;
        float[] todo = new float[bloques * muestrasPorBloque];
        for (int b = 0; b < bloques; b++) p.Read(todo, b * muestrasPorBloque, muestrasPorBloque);
        return todo;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TEfectos = Busca(asm, "Efectos");
        TEfecto = Busca(asm, "Efecto");
        TMezcla = Busca(asm, "MezclaEfectos");

        Console.WriteLine("\n  1) LAS PIEZAS QUE HACEN FALTA\n");
        Check(TEfectos != null, "la rocola sabe fabricar sus propios sonidos (Efectos)");
        Check(TEfecto != null, "y tiene una lista de cuales son (Efecto)");
        Check(TMezcla != null, "y una pieza que los mete en la musica (MezclaEfectos)");
        if (TEfectos == null || TEfecto == null || TMezcla == null)
        {
            Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
            return fallos;
        }
        for (int i = 0; i < NOMBRES.Length; i++)
        {
            bool hay = false;
            foreach (string n in Enum.GetNames(TEfecto)) if (n == NOMBRES[i]) hay = true;
            Check(hay, "esta " + COMO[i]);
        }
        if (fallos > 0)
        {
            Console.WriteLine("\n  " + fallos + " FALLO(S)");
            return fallos;
        }

        Console.WriteLine("\n  2) CADA UNO ES UN SONIDO DE VERDAD, Y NO REVIENTA\n");
        float[][] ondas = new float[NOMBRES.Length][];
        for (int i = 0; i < NOMBRES.Length; i++)
        {
            float[] x = Muestra(NOMBRES[i]);
            ondas[i] = x;
            double dur = (x == null) ? 0 : (double)x.Length / FREC;
            double dice = Segundos(NOMBRES[i]);
            float pico = (x == null || x.Length == 0) ? 0f : Pico(x);
            Console.WriteLine("     " + COMO[i].PadRight(26) + dur.ToString("0.000") + " s   pico " + pico.ToString("0.000"));
            Check(x != null && x.Length > 64, COMO[i] + ": tiene sonido");
            if (x == null || x.Length == 0) continue;
            Check(pico > 0.05f, COMO[i] + ": no es silencio");
            Check(pico <= 1.0f, COMO[i] + ": no se pasa de la raya (reventaria sobre la musica)");
            Check(Math.Abs(dur - dice) <= dice * 0.10 + 0.01, COMO[i] + ": dura lo que dice que dura");
        }

        Console.WriteLine("\n  3) NINGUNO CHASQUEA AL EMPEZAR NI AL ACABAR\n");
        // Un sonido que arranca a mitad de onda da un golpe seco en la bocina, y otro al cortarse. Se
        // mira que las dos puntas salgan de cero y vuelvan a cero.
        for (int i = 0; i < NOMBRES.Length; i++)
        {
            float[] x = ondas[i]; if (x == null || x.Length < 8) continue;
            Check(Math.Abs(x[0]) < 0.02f, COMO[i] + ": arranca desde el silencio");
            Check(Math.Abs(x[x.Length - 1]) < 0.02f, COMO[i] + ": y termina en el silencio");
        }

        Console.WriteLine("\n  4) SE DISTINGUEN ENTRE SI\n");
        // Con los ojos cerrados hay que poder decir cual sono. Se comparan las huellas: cuanta energia
        // lleva cada uno en graves, cuerpo, medios y agudos.
        double[] hMoneda = Huella(ondas[0], FREC), hCola = Huella(ondas[2], FREC), hNo = Huella(ondas[3], FREC);
        Console.WriteLine("     moneda      " + Pinta(hMoneda));
        Console.WriteLine("     a la cola   " + Pinta(hCola));
        Console.WriteLine("     sin credito " + Pinta(hNo));
        Check(Distancia(hMoneda, hCola) > 0.35, "la moneda y el golpe de la cola no suenan igual");
        Check(Distancia(hCola, hNo) > 0.35, "el golpe de la cola y el aviso de sin creditos tampoco");
        Check(Distancia(hMoneda, hNo) > 0.35, "ni la moneda y el aviso de sin creditos");
        // Y la otra mitad de la diferencia, la que la altura no ve: uno GOLPEA y el otro SUENA.
        double aCola = Arranque(ondas[2], FREC), aNo = Arranque(ondas[3], FREC);
        Console.WriteLine("     de su fuerza en los primeros 30 ms:  a la cola " + (aCola * 100).ToString("00")
                          + "%   sin credito " + (aNo * 100).ToString("00") + "%");
        Check(aCola > 0.55, "el de la cola es un GOLPE: suelta su fuerza de una");
        Check(aNo < 0.35, "y el de sin creditos es un TONO: se sostiene");

        Console.WriteLine("\n  5) SIN NINGUN EFECTO, LA MUSICA SALE IGUAL QUE HOY\n");
        // Es el 99% del tiempo. Si la mezcla tocara la cancion aunque no suene nada, se estaria
        // estropeando lo que la gente vino a oir a cambio de un efecto que casi nunca suena.
        int blo = FREC / 20 * 2;   // un bloque de 50 ms en estereo, el de verdad
        float[] quieto = Corre(NuevaMezcla(new Fuente(0.5f, 2), 0.7f), 8, blo);
        bool igual = true;
        for (int i = 0; i < quieto.Length; i++) if (quieto[i] != 0.5f) { igual = false; break; }
        Check(igual, "sin nada disparado, la musica pasa MUESTRA POR MUESTRA sin tocarla");

        // Y con el volumen a cero, aunque se dispare: apagado es apagado.
        object mzApagada = NuevaMezcla(new Fuente(0.5f, 2), 0f);
        Dispara(mzApagada, "Moneda");
        float[] apagado = Corre(mzApagada, 8, blo);
        bool igual2 = true;
        for (int i = 0; i < apagado.Length; i++) if (apagado[i] != 0.5f) { igual2 = false; break; }
        Check(igual2, "con los efectos apagados, disparar uno no cambia ni una muestra");

        Console.WriteLine("\n  6) LA MUSICA SE AGACHA CON RAMPA, NO DE GOLPE\n");
        // Bajarle un decibel a la cancion de un bloque al siguiente es un escalon en mitad de la onda:
        // otro chasquido. Con la fuente en 1.0 se puede despejar cuanto la agacho la mezcla en cada
        // muestra, restando el efecto (que ya sabemos como suena).
        const float VOL = 0.7f;
        object mz = NuevaMezcla(new Fuente(1f, 1), VOL);
        Dispara(mz, "Moneda");
        int largo = (int)(Segundos("Moneda") * FREC) + FREC / 2;
        float[] mez = Corre(mz, (largo / blo) + 2, blo);
        float[] ef = ondas[0];
        float menor = 1f, salto = 0f, prev = 1f;
        for (int i = 0; i < mez.Length; i++)
        {
            float suEfecto = (i < ef.Length) ? ef[i] * VOL : 0f;
            float agacha = mez[i] - suEfecto;              // lo que quedo de la musica (la fuente vale 1)
            if (i > 0 && Math.Abs(agacha - prev) > salto) salto = Math.Abs(agacha - prev);
            if (agacha < menor) menor = agacha;
            prev = agacha;
        }
        float alFinal = mez[mez.Length - 1];
        Console.WriteLine("     lo mas que se agacho la musica:      " + menor.ToString("0.000"));
        Console.WriteLine("     el mayor salto de una muestra a otra: " + salto.ToString("0.00000"));
        Check(menor < 0.97f, "mientras suena el efecto, la musica SE AGACHA");
        Check(menor > 0.60f, "pero no se hunde: sigue siendo la cancion lo que manda");
        Check(salto < 0.01f, "y baja poco a poco: ni un escalon de golpe (chasquido)");
        Check(Math.Abs(alFinal - 1f) < 0.02f, "cuando el efecto acaba, la musica vuelve a su volumen");

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }

    static string Pinta(double[] h)
    {
        return "graves " + (h[0] * 100).ToString("00") + "%  cuerpo " + (h[1] * 100).ToString("00")
             + "%  medios " + (h[2] * 100).ToString("00") + "%  agudos " + (h[3] * 100).ToString("00") + "%";
    }
}
