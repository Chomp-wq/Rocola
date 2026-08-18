using System;
using System.Diagnostics;
using System.IO;
using NAudio.Wave;

class Velocidad
{
    static int Main()
    {
        string[] f = Directory.GetFiles("wav", "*.wav"); Array.Sort(f);
        // SOLO abrir y leer la duracion (sin decodificar nada)
        Stopwatch sw = Stopwatch.StartNew();
        double tot = 0; int n = 0;
        foreach (string p in f)
            using (AudioFileReader r = new AudioFileReader(p)) { tot += r.TotalTime.TotalSeconds; n++; }
        sw.Stop();
        double msSolo = sw.Elapsed.TotalMilliseconds / n;
        Console.WriteLine(string.Format("  Solo leer la DURACION:      {0,7:0.00} ms por cancion", msSolo));

        // Decodificar 60 s (lo que hace hoy el pre-analisis para volumen y EQ)
        sw = Stopwatch.StartNew();
        float[] buf = new float[16384];
        foreach (string p in f)
            using (AudioFileReader r = new AudioFileReader(p))
            {
                long want = (long)(60.0 * r.WaveFormat.SampleRate) * r.WaveFormat.Channels, got = 0;
                int k;
                while (got < want && (k = r.Read(buf, 0, buf.Length)) > 0) got += k;
            }
        sw.Stop();
        double msDec = sw.Elapsed.TotalMilliseconds / n;
        Console.WriteLine(string.Format("  Decodificar 60 s (hoy):     {0,7:0.00} ms por cancion   -> {1:0}x mas lento", msDec, msDec / Math.Max(0.01, msSolo)));
        Console.WriteLine();
        foreach (int lib in new int[] { 118000 })
        {
            Console.WriteLine(string.Format("  Con {0:#,0} canciones:", lib));
            Console.WriteLine(string.Format("     solo duraciones .... {0,6:0.0} minutos", lib * msSolo / 60000.0));
            Console.WriteLine(string.Format("     analisis completo ... {0,6:0.0} horas", lib * msDec / 3600000.0));
        }
        return 0;
    }
}
