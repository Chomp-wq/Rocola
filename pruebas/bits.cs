using System;
using System.IO;
using System.Reflection;
using System.Threading;
using NAudio.Wave;

// CUANTOS BITS SE LE MANDAN A LA TARJETA, Y QUE SEAN LOS CORRECTOS.
//
// Hasta ahora la rocola le mandaba SIEMPRE 16 bits, que es lo que habia que hacer en Windows XP. Para
// pasar de la cuenta fina que lleva por dentro a un entero de 16 bits hay que redondear, y redondear
// siempre igual deja un error que SIGUE a la musica en vez de ser ruido: se oye como una aspereza en
// los pasajes callados. El remedio de toda la vida es sumar un ruido diminuto antes de redondear -el
// "dither"-, y entonces el error se vuelve un siseo parejo que el oido no persigue.
//
// Casi cualquier tarjeta de los ultimos veinte anos acepta 24 o 32 bits. Ahi el escalon del redondeo
// queda tan abajo que se pierde bajo el ruido de la propia tarjeta y del bar entero: anadir ruido
// seria empeorar el sonido a cambio de nada. Asi que ahora la salida NEGOCIA con la tarjeta el mejor
// formato que acepte y hace ella la conversion, con dither SOLO si acaba en 16 bits.
//
// Esta bateria comprueba lo que de verdad importa de ese cambio: que las cuentas salen bien en cada
// formato. Un error aqui no da un aviso ni un error: da ruido, o el doble de volumen, o los canales
// cambiados, y solo se descubre en el bar.
class Bits
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const int RATE = 44100, CH = 2;

    // Una fuente en coma flotante con valores ESCOGIDOS: los extremos, el silencio y unos cuantos
    // valores intermedios. Asi se puede comprobar cada muestra contra la cuenta hecha a mano.
    sealed class Rampa : ISampleProvider
    {
        private readonly float[] datos; private int pos;
        private readonly WaveFormat wf = WaveFormat.CreateIeeeFloatWaveFormat(RATE, CH);
        public Rampa(float[] d) { datos = d; }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] b, int o, int c)
        {
            int n = 0;
            while (n < c && pos < datos.Length) { b[o + n] = datos[pos]; pos++; n++; }
            return n;
        }
    }

    static float[] Valores()
    {
        // Medio segundo, para que la salida tenga que dar varias vueltas de verdad.
        int n = RATE * CH / 2;
        float[] d = new float[n];
        float[] clave = { 0f, 1f, -1f, 0.5f, -0.5f, 0.25f, 1f / 32767f, -1f / 32767f, 0.999f, -0.999f };
        for (int i = 0; i < n; i++) d[i] = clave[i % clave.Length];
        return d;
    }

    static byte[] Suena(Type TSal, ISampleProvider fuente, out int bits)
    {
        string crudo = Path.Combine(Path.GetTempPath(), "rocola_bits.raw");
        try { File.Delete(crudo); } catch { }
        string disp = "plug:\"file:FILE='" + crudo + "',FORMAT=raw\"";
        object salida = Activator.CreateInstance(TSal, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                                                 null, new object[] { 200, disp }, null);
        bool aviso = false;
        TSal.GetEvent("PlaybackStopped").AddEventHandler(salida,
            new EventHandler<StoppedEventArgs>(delegate(object s, StoppedEventArgs e) { aviso = true; }));
        bits = 0;
        try
        {
            TSal.GetMethod("Init", new Type[] { typeof(ISampleProvider) }).Invoke(salida, new object[] { fuente });
            bits = (int)TSal.GetProperty("BitsALaTarjeta").GetValue(salida, null);
            TSal.GetMethod("Play").Invoke(salida, null);
            for (int i = 0; i < 200 && !aviso; i++) Thread.Sleep(25);
        }
        catch (TargetInvocationException tie) { Console.WriteLine("     se quejo: " + tie.InnerException.Message); }
        finally { try { ((IDisposable)salida).Dispose(); } catch { } }
        return File.Exists(crudo) ? File.ReadAllBytes(crudo) : new byte[0];
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TSal = asm.GetType("Rocola.SalidaAlsa", true);

        Console.WriteLine("\n  1) LA SALIDA SABE RECIBIR LA CADENA TAL COMO VIAJA POR DENTRO\n");
        MethodInfo mFlo = TSal.GetMethod("Init", new Type[] { typeof(ISampleProvider) });
        MethodInfo m16 = TSal.GetMethod("Init", new Type[] { typeof(IWaveProvider) });
        Check(mFlo != null, "se le puede dar la cadena en coma flotante y que ella decida el formato");
        Check(m16 != null, "y sigue aceptando muestras ya hechas de 16 bits (nada de lo de antes se rompe)");
        if (mFlo == null) { Console.WriteLine("\n  " + Math.Max(1, fallos) + " FALLO(S)\n"); return Math.Max(1, fallos); }

        float[] v = Valores();
        int bits;
        byte[] salio = Suena(TSal, new Rampa(v), out bits);
        Console.WriteLine("     la salida negocio " + bits + " bits con la tarjeta");
        Check(bits == 16 || bits == 24 || bits == 32, "eligio un formato de los que sabe mandar");
        Check(salio.Length > 0, "y sono de verdad (si no, lo de abajo no probaria nada)");
        if (salio.Length == 0) { Console.WriteLine("\n  " + Math.Max(1, fallos) + " FALLO(S)\n"); return Math.Max(1, fallos); }

        int bytesPorMuestra = (bits == 16) ? 2 : 4;
        int muestras = Math.Min(v.Length, salio.Length / bytesPorMuestra);
        Console.WriteLine("     " + muestras + " muestras de " + bytesPorMuestra + " bytes");
        Check(muestras > 20000, "llegaron todas las muestras");

        Console.WriteLine("\n  2) Y LAS CUENTAS SALEN BIEN  (un error aqui no avisa: da ruido)\n");
        // Se comprueba muestra a muestra contra la cuenta hecha a mano. Con 16 bits se admite el ruido
        // del dither -que es de un solo escalon, a proposito-; con mas bits se exige exacto.
        long peor = 0; int fuera = 0;
        long tope = (bits == 16) ? 32767L : (bits == 24 ? 8388607L : 2147483647L);
        for (int i = 0; i < muestras; i++)
        {
            long q = Lee(salio, i * bytesPorMuestra, bytesPorMuestra);
            long esperado = (long)Math.Floor((double)v[i] * tope + 0.5);
            if (esperado > tope) esperado = tope; else if (esperado < -tope - 1) esperado = -tope - 1;
            long d = Math.Abs(q - esperado);
            if (d > peor) peor = d;
            if (d > (bits == 16 ? 1 : 0)) fuera++;
        }
        Console.WriteLine("     la muestra que mas se aparto de la cuenta: " + peor + " escalon(es) de " + tope);
        Console.WriteLine("     muestras fuera de lo admitido: " + fuera);
        Check(fuera == 0, bits == 16
              ? "cada muestra sale donde tiene que salir (con el dither, un escalon de margen)"
              : "cada muestra sale EXACTA: sin dither, porque con " + bits + " bits no hace falta");

        Console.WriteLine("\n  3) LOS TRES FORMATOS, UNO POR UNO\n");
        // Arriba se probo el que la tarjeta de esta maquina acepto. Pero la del bar puede aceptar otro, y
        // ese camino tambien tiene que estar bien. Se le pide a la salida que convierta con cada formato
        // a proposito, sin depender de lo que acepte ninguna tarjeta.
        object conv = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(TSal);
        FieldInfo fFmt = TSal.GetField("formato", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo mConv = TSal.GetMethod("Convierte", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(fFmt != null && mConv != null, "la salida tiene un solo sitio donde convierte las muestras");
        if (fFmt != null && mConv != null)
        {
            int[] formatos = { 2, 6, 10 };      // S16_LE, S24_LE, S32_LE
            int[] cuantos = { 16, 24, 32 };
            long[] topes = { 32767L, 8388607L, 2147483647L };
            for (int f = 0; f < formatos.Length; f++)
            {
                fFmt.SetValue(conv, formatos[f]);
                int bpm = (cuantos[f] == 16) ? 2 : 4;
                byte[] dst = new byte[v.Length * bpm];
                mConv.Invoke(conv, new object[] { v, v.Length, dst });
                long peorF = 0;
                for (int i = 0; i < v.Length; i++)
                {
                    long q = Lee(dst, i * bpm, bpm);
                    long esp = (long)Math.Floor((double)v[i] * topes[f] + 0.5);
                    if (esp > topes[f]) esp = topes[f]; else if (esp < -topes[f] - 1) esp = -topes[f] - 1;
                    long d = Math.Abs(q - esp);
                    if (d > peorF) peorF = d;
                }
                // Y el silencio: con 16 bits tiene que llevar el siseo del dither; con mas, no.
                float[] nada = new float[2048];
                byte[] dsil = new byte[nada.Length * bpm];
                mConv.Invoke(conv, new object[] { nada, nada.Length, dsil });
                int noCero = 0;
                for (int i = 0; i < nada.Length; i++) if (Lee(dsil, i * bpm, bpm) != 0) noCero++;

                Console.WriteLine("     " + cuantos[f] + " bits: la peor muestra se aparto " + peorF
                                  + " escalon(es); en silencio, " + noCero + " de " + nada.Length + " no son cero");
                Check(peorF <= (cuantos[f] == 16 ? 1 : 0),
                      "con " + cuantos[f] + " bits las cuentas salen" + (cuantos[f] == 16 ? " (con el escalon del dither)" : " EXACTAS"));
                if (cuantos[f] == 16)
                    Check(noCero > nada.Length / 10, "y con 16 bits el silencio lleva su dither, que es para lo que esta");
                else
                    Check(noCero == 0, "y con " + cuantos[f] + " bits el silencio es silencio: ni un bit de ruido anadido");
            }
        }

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }

    static long Lee(byte[] b, int o, int n)
    {
        if (n == 2) return (short)(b[o] | (b[o + 1] << 8));
        return (int)((uint)b[o] | ((uint)b[o + 1] << 8) | ((uint)b[o + 2] << 16) | ((uint)b[o + 3] << 24));
    }
}
