// LA ROCOLA SIN PANTALLA (Rocola OS, etapa 2). Toca una cola de canciones con el motor, como la rocola de XP:
//   0) al arrancar lee rocola_config.txt (los mismos ajustes del sonido que XP) y las tablas de lo aprendido, de la carpeta de los datos;
//   1) mide cada cancion de la cola que aun no lo este (sonoridad, picos y P90: el repaso de XP), para nivelarla "al limite";
//   2) cada una con la cadena ENTERA de XP (ArmaCadena) y, cuando se puede, seguidas en la misma salida sin hueco (el relevo sin
//      pausa de XP: la siguiente se prepara 12 s antes; las pistas seguidas de un disco, sin fundido); 16 bits con dither;
//   3) por ALSA (o a un WAV con --wav, para probar sin tarjeta); al acabar cada una se guarda lo aprendido, como en XP.
// Uso: rocola-consola [--wav salida.wav] [--dispositivo default] [--biblioteca] [cancion1.mp3 cancion2.wav ...]
//      rocola-consola --rocola [--dispositivo default]   la rocola en modo texto, con sus botones (RocolaTexto.cs)
//   --biblioteca: la cola es la biblioteca entera (la carpeta "musica" junto a los datos y las de "dir="), disco por disco.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    internal static class Programa
    {
        internal static int Main(string[] args)
        {
            string wav = null, dispositivo = "default"; bool biblioteca = false, rocola = false; List<string> lista = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--wav" && i + 1 < args.Length) wav = args[++i];
                else if (args[i] == "--dispositivo" && i + 1 < args.Length) dispositivo = args[++i];
                else if (args[i] == "--biblioteca") biblioteca = true;
                else if (args[i] == "--rocola") rocola = true;
                else lista.Add(Path.GetFullPath(args[i]));
            }
            if (rocola)
            {
                Form1 m = new Form1(); m.Arranca();
                int r = new RocolaTexto(m, dispositivo).Corre();
                m.Cierra();
                return r;
            }
            if (lista.Count == 0 && !biblioteca) { Console.Error.WriteLine("Uso: rocola-consola [--wav salida.wav] [--dispositivo default] [--biblioteca] canciones..."); return 2; }
            Form1 motor = new Form1();   // los ajustes de fabrica de XP...
            motor.Arranca();             // ...y encima los de rocola_config.txt y lo aprendido (las tablas), como al arrancar la de XP
            Console.WriteLine(motor.Resumen());
            List<Form1.Disco> discos = motor.LeeBiblioteca();
            Console.WriteLine("Biblioteca (" + motor.CarpetaMusica + "): " + discos.Count + " discos");
            if (biblioteca) foreach (Form1.Disco d in discos) lista.AddRange(d.Rutas);
            foreach (string ruta in lista)
            {
                if (!motor.MideSiFalta(ruta)) Console.WriteLine("  (no se pudo medir " + Path.GetFileName(ruta) + ": suena con la regla de las canciones sin medir)");
                motor.Encola(ruta);
            }
            WaveFileWriter escritor = null;
            ISampleProvider s;
            while ((s = motor.EmpiezaSiguiente()) != null)
            {
                string dicha = null;
                Action cuenta = delegate
                {
                    string ahora = motor.Sonando;
                    if (ahora != null && ahora != dicha) { dicha = ahora; Console.WriteLine("Suena: " + Path.GetFileName(ahora) + "  (nivelado " + motor.SonandoGananciaDb.ToString("+0.0;-0.0") + " dB)"); }
                };
                cuenta();
                try
                {
                    if (wav != null)
                    {
                        if (escritor == null) escritor = new WaveFileWriter(wav, new WaveFormat(s.WaveFormat.SampleRate, 16, 2));
                        IWaveProvider d = Form1.A16Bits(s); byte[] b = new byte[16384]; int n; long cuadros = 0;
                        while ((n = d.Read(b.AsSpan())) > 0)
                        {
                            escritor.Write(b, 0, n);
                            cuadros += n / 4;
                            motor.Atiende(cuadros);   // lo escrito ya "sono"
                            cuenta();
                        }
                    }
                    else
                    {
                        using (SalidaAlsa salida = new SalidaAlsa(s, dispositivo))
                        {
                            salida.Empieza();
                            while (salida.Sonando) { Thread.Sleep(100); motor.Atiende(salida.CuadrosOidos); cuenta(); }
                            if (salida.Recuperaciones > 0) Console.WriteLine("  (cortes recuperados: " + salida.Recuperaciones + ")");
                        }
                    }
                }
                catch (Exception e) { Console.Error.WriteLine("Fallo la salida: " + e.Message); }
                motor.AcaboLaSalida();   // lo aprendido de lo que sonaba
            }
            if (escritor != null) escritor.Dispose();
            motor.Cierra();   // lo aprendido, al disco
            return 0;
        }
    }
}
