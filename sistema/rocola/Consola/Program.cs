// LA ROCOLA SIN PANTALLA (Rocola OS, etapa 2). Toca una lista de canciones con la cadena del motor, una tras otra:
//   0) al arrancar lee rocola_config.txt (los mismos ajustes del sonido que XP) y las tablas de lo aprendido, de la carpeta de los datos;
//   1) mide la cancion entera si aun no lo esta (sonoridad, picos y P90: el repaso de XP), para nivelarla "al limite";
//   2) le arma la cadena ENTERA de XP con esos ajustes (ArmaCadena: arreglos, des-recorte, nivelado y EQ, grave en mono, el
//      local, presencia, voz/golpe/retumbe, bajo psicoacustico, limitador de pico real y fundido) y la pasa a 16 bits con dither;
//      al acabar cada una se guarda lo que se aprendio oyendola (volumen, huella del EQ), como en XP;
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
            Form1 motor = new Form1();   // los ajustes de fabrica de XP...
            motor.Arranca();             // ...y encima los de rocola_config.txt y lo aprendido (las tablas), como al arrancar la de XP
            Console.WriteLine(motor.Resumen());
            foreach (string ruta in lista)
            {
                try
                {
                    string completa = Path.GetFullPath(ruta);
                    if (!motor.MideSiFalta(completa)) Console.WriteLine("  (no se pudo medir: suena con la regla de las canciones sin medir)");
                    using (Form1.Cancion cancion = motor.Arma(completa))
                    {
                        ISampleProvider cadena = cancion.Salida;
                        Console.WriteLine("Suena: " + Path.GetFileName(ruta) + "  (" + cancion.Duracion.ToString(@"m\:ss") + ", nivelado " + cancion.GananciaDb.ToString("+0.0;-0.0") + " dB)");
                        if (wav != null)
                        {
                            if (escritor == null) escritor = new WaveFileWriter(wav, new WaveFormat(cadena.WaveFormat.SampleRate, 16, 2));
                            IWaveProvider d = Form1.A16Bits(cadena); byte[] b = new byte[16384]; int n; long hechos = 0, cada = cadena.WaveFormat.SampleRate * 2L;   // medio segundo, en bytes de 16 bits estereo
                            while ((n = d.Read(b.AsSpan())) > 0)
                            {
                                escritor.Write(b, 0, n);
                                hechos += n; if (hechos >= cada) { hechos -= cada; motor.MuestreaEtapas(cancion); }   // como el reloj de la cancion en XP
                            }
                        }
                        else
                        {
                            using (System.Threading.Timer reloj = new System.Threading.Timer(delegate { motor.MuestreaEtapas(cancion); }, null, 500, 500))   // el reloj de la cancion
                            using (SalidaAlsa salida = new SalidaAlsa(cadena, dispositivo)) { salida.Empieza(); salida.Espera(); if (salida.Recuperaciones > 0) Console.WriteLine("  (cortes recuperados: " + salida.Recuperaciones + ")"); }
                        }
                    }
                }
                catch (Exception e) { Console.Error.WriteLine("No se pudo tocar " + ruta + ": " + e.Message); }
            }
            if (escritor != null) escritor.Dispose();
            motor.Cierra();   // lo aprendido, al disco
            return 0;
        }

    }
}
