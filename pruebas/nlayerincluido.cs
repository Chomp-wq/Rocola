using System;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// Ejecutar en una carpeta aislada SIN NLayer.dll junto al programa/prueba.
class NLayerIncluidoPrueba
{
    static int Main(string[] args)
    {
        if (File.Exists(Path.Combine(Path.GetDirectoryName(args[0]), "NLayer.dll")))
            throw new Exception("La prueba debe verificar el recurso, no la DLL externa");
        Assembly a = Assembly.LoadFrom(args[0]);
        const BindingFlags b = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        Type incluido = a.GetType("Rocola.NLayerIncluido", true);
        incluido.GetMethod("Engancha", b).Invoke(null, null);
        using (Stream recurso = a.GetManifestResourceStream("Rocola.NLayer.dll"))
            if (recurso == null || recurso.Length < 10000) throw new Exception("Falta NLayer integrado");
        int cuadros = 0, bytes = 0;
        using (FileStream fs = File.OpenRead(args[1]))
        {
            Mp3Frame frame = Mp3Frame.LoadFromStream(fs);
            if (frame == null) throw new Exception("Fixture MP3 vacio");
            WaveFormat formato = new Mp3WaveFormat(frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2, frame.FrameLength, frame.BitRate);
            using (IMp3FrameDecompressor d = (IMp3FrameDecompressor)Activator.CreateInstance(a.GetType("Rocola.MotorNLayer", true), b, null, new object[] { formato }, null))
            {
                if (d.OutputFormat.Encoding != WaveFormatEncoding.IeeeFloat) throw new Exception("No es flotante");
                byte[] pcm = new byte[1152 * formato.Channels * 8];
                do { bytes += d.DecompressFrame(frame, pcm, 0); cuadros++; }
                while (cuadros < 150 && (frame = Mp3Frame.LoadFromStream(fs)) != null);
            }
        }
        if (cuadros < 100 || bytes <= 0) throw new Exception("No descodifico la prueba");
        Console.WriteLine("PASS NLayer integrado: " + cuadros + " cuadros sin DLL externa; no demuestra fidelidad ni activa el codec");
        return 0;
    }
}
