using System;
using System.Diagnostics;
using System.IO;
using NAudio.Wave;

class Velocidad2
{
    static double SoloDur(string[] f, int veces)
    {
        Stopwatch sw = Stopwatch.StartNew();
        for (int v = 0; v < veces; v++)
            foreach (string p in f) using (AudioFileReader r = new AudioFileReader(p)) { double d = r.TotalTime.TotalSeconds; }
        sw.Stop(); return sw.Elapsed.TotalMilliseconds / (f.Length * veces);
    }
    static double Decodifica(string[] f, double seg)
    {
        float[] buf = new float[16384];
        Stopwatch sw = Stopwatch.StartNew();
        foreach (string p in f)
            using (AudioFileReader r = new AudioFileReader(p))
            {
                long want = (long)(seg * r.WaveFormat.SampleRate) * r.WaveFormat.Channels, got = 0; int k;
                while (got < want && (k = r.Read(buf, 0, buf.Length)) > 0) got += k;
            }
        sw.Stop(); return sw.Elapsed.TotalMilliseconds / f.Length;
    }
    static int Main()
    {
        string[] f = Directory.GetFiles("wav", "*.wav"); Array.Sort(f);
        SoloDur(f, 2); Decodifica(f, 5);                 // calentar (JIT + cache de disco)
        double a = SoloDur(f, 20);
        double b = Decodifica(f, 60);
        Console.WriteLine();
        Console.WriteLine(string.Format("  Solo leer la DURACION:   {0,8:0.000} ms por cancion", a));
        Console.WriteLine(string.Format("  Decodificar 60 s (hoy):  {0,8:0.000} ms por cancion", b));
        Console.WriteLine(string.Format("  -> sacar solo la duracion es {0:#,0} veces mas barato", b / Math.Max(0.0001, a)));
        Console.WriteLine();
        Console.WriteLine("  Extrapolado a 118.000 canciones (en esta maquina, con WAV):");
        Console.WriteLine(string.Format("     solo duraciones ....  {0,6:0.0} minutos", 118000 * a / 60000.0));
        Console.WriteLine(string.Format("     analisis completo ...  {0,6:0.1} horas", 118000 * b / 3600000.0));
        Console.WriteLine();
        Console.WriteLine("  (Con MP3 la duracion cuesta mas que con WAV: NAudio recorre los cuadros del");
        Console.WriteLine("   archivo para indexarlo. Aun asi es LEER el archivo, no descomprimirlo.)");
        return 0;
    }
}
