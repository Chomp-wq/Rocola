using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// Renderer digital del DSEE REAL para comparar candidatos offline. No crea Form1 ni dispositivos,
// no lee preferencias, no modifica modelos instalados. Un proceso nuevo por modelo: evita estado compartido.
class RenderDsee
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    sealed class Fuente : ISampleProvider
    {
        readonly ISampleProvider interior;
        internal Fuente(ISampleProvider s) { interior = s; }
        public WaveFormat WaveFormat { get { return interior.WaveFormat; } }
        public int Read(float[] b, int o, int c)
        {
            int n = interior.Read(b, o, c);
            for (int i = o; i < o + n; i++)
                if (float.IsNaN(b[i]) || float.IsInfinity(b[i]) || Math.Abs(b[i]) > 4f)
                    throw new InvalidDataException("La entrada PCM de evaluacion no es valida");
            return n;
        }
    }
    static int Main(string[] args)
    {
        if (args.Length < 4 || args.Length > 5)
        {
            Console.Error.WriteLine("Uso: renderdsee.exe Rocola.exe modelo.bin entrada.wav salida.wav [2048|4096]");
            return 2;
        }
        string temporal = null;
        try
        {
            int frame = args.Length == 5 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 2048;
            if (frame != 2048 && frame != 4096) throw new ArgumentException("Ventana: 2048 o 4096");
            string exe = Path.GetFullPath(args[0]), modelo = Path.GetFullPath(args[1]);
            string entrada = Path.GetFullPath(args[2]), salida = Path.GetFullPath(args[3]);
            if (File.Exists(salida)) throw new IOException("La salida ya existe; no se sobrescribe");
            Assembly asm = Assembly.LoadFrom(exe);
            Type form = asm.GetType("Rocola.Form1", true);
            object red = form.GetField("NeuralEnv", Flags).GetValue(null);
            red.GetType().GetMethod("TryLoad", Flags).Invoke(red, new object[] { modelo });
            if (!(bool)red.GetType().GetField("Loaded", Flags).GetValue(red))
                throw new InvalidDataException("Modelo invalido o contrato incompatible");
            Type espectro = asm.GetType("Rocola.Form1+SpectralTap", true);
            temporal = salida + ".parcial-" + Guid.NewGuid().ToString("N");
            long total = 0;
            using (WaveFileReader lector = new WaveFileReader(entrada))
            {
                ISampleProvider fuente = new Fuente(lector.ToSampleProvider());
                ConstructorInfo constructor = espectro.GetConstructor(Flags, null, new Type[] { typeof(ISampleProvider), typeof(int) }, null);
                if (constructor == null && frame != 2048) throw new NotSupportedException("Esta rama solo usa ventana 2048");
                object tap = constructor != null ? constructor.Invoke(new object[] { fuente, frame })
                    : Activator.CreateInstance(espectro, Flags, null, new object[] { fuente }, CultureInfo.InvariantCulture);
                espectro.GetField("Active", Flags).SetValue(tap, true);
                ISampleProvider procesamiento = (ISampleProvider)tap;
                int ch = fuente.WaveFormat.Channels;
                float[] b = new float[4096 * ch];
                using (WaveFileWriter escritor = new WaveFileWriter(temporal,
                    WaveFormat.CreateIeeeFloatWaveFormat(fuente.WaveFormat.SampleRate, ch)))
                {
                    int n;
                    while ((n = procesamiento.Read(b, 0, b.Length)) > 0)
                    {
                        if (n % ch != 0) throw new InvalidDataException("Cuadro de salida incompleto");
                        for (int i = 0; i < n; i++)
                            if (float.IsNaN(b[i]) || float.IsInfinity(b[i]))
                                throw new InvalidDataException("El candidato produjo PCM no finito");
                        escritor.WriteSamples(b, 0, n); total += n;
                    }
                }
                Console.WriteLine("PCM: " + (total / ch).ToString(CultureInfo.InvariantCulture) + " cuadros, "
                    + fuente.WaveFormat.SampleRate.ToString(CultureInfo.InvariantCulture) + " Hz, " + ch + " canales");
            }
            File.Move(temporal, salida); temporal = null;
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.ToString()); return 1;
        }
        finally { if (temporal != null) { try { File.Delete(temporal); } catch { } } }
    }
}
