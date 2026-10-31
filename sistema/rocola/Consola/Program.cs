// LA ROCOLA SIN PANTALLA (Rocola OS, etapa 2, hito 4). Toca una lista de canciones con la cadena del motor, una tras otra:
//   1) mide la cancion entera (sonoridad, picos y P90: Sonoridad.cs, como el repaso de XP) y la sube "al limite" (GananciaAlLimite);
//   2) la cadena: arreglos de la grabacion -> nivelado -> grave en mono (120 Hz) -> limitador de pico real -> 16 bits con dither;
//   3) la toca por ALSA (o la escribe en un WAV con --wav, para probar sin tarjeta).
// Uso: rocola-consola [--wav salida.wav] [--dispositivo default] cancion1.mp3 cancion2.wav ...
using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace Rocola
{
    internal static class Programa
    {
        internal static int Main(string[] args)
        {
            string wav = null, dispositivo = "default"; List<string> lista = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--wav" && i + 1 < args.Length) wav = args[++i];
                else if (args[i] == "--dispositivo" && i + 1 < args.Length) dispositivo = args[++i];
                else lista.Add(args[i]);
            }
            if (lista.Count == 0) { Console.Error.WriteLine("Uso: rocola-consola [--wav salida.wav] [--dispositivo default] canciones..."); return 2; }
            WaveFileWriter escritor = null;
            foreach (string ruta in lista)
            {
                try
                {
                    float ganancia = Form1.MideAlLimite(ruta);
                    using (LectorCancion lector = new LectorCancion(ruta))
                    {
                        ISampleProvider cadena = Form1.CadenaDeFabrica(lector, ganancia);
                        Console.WriteLine("Suena: " + Path.GetFileName(ruta) + "  (" + lector.Duracion.ToString(@"m\:ss") + ", nivelado " + (20 * Math.Log10(ganancia)).ToString("+0.0;-0.0") + " dB)");
                        if (wav != null)
                        {
                            if (escritor == null) escritor = new WaveFileWriter(wav, new WaveFormat(cadena.WaveFormat.SampleRate, 16, 2));
                            IWaveProvider d = Form1.A16Bits(cadena); byte[] b = new byte[16384]; int n;
                            while ((n = d.Read(b.AsSpan())) > 0) escritor.Write(b, 0, n);
                        }
                        else
                        {
                            using (SalidaAlsa salida = new SalidaAlsa(cadena, dispositivo)) { salida.Empieza(); salida.Espera(); if (salida.Recuperaciones > 0) Console.WriteLine("  (cortes recuperados: " + salida.Recuperaciones + ")"); }
                        }
                    }
                }
                catch (Exception e) { Console.Error.WriteLine("No se pudo tocar " + ruta + ": " + e.Message); }
            }
            if (escritor != null) escritor.Dispose();
            return 0;
        }

    }
}
