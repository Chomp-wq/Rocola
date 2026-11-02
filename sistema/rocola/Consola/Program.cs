// LA ROCOLA SIN PANTALLA (Rocola OS, etapa 2). Toca una cola de canciones con el motor, como la rocola de XP:
//   0) al arrancar lee rocola_config.txt (los mismos ajustes del sonido que XP) y las tablas de lo aprendido, de la carpeta de los datos;
//   1) mide cada cancion de la cola que aun no lo este (sonoridad, picos y P90: el repaso de XP), para nivelarla "al limite";
//   2) cada una con la cadena ENTERA de XP (ArmaCadena) y, cuando se puede, seguidas en la misma salida sin hueco (el relevo sin
//      pausa de XP: la siguiente se prepara 12 s antes; las pistas seguidas de un disco, sin fundido); 16 bits con dither;
//   3) por ALSA (o a un WAV con --wav, para probar sin tarjeta); al acabar cada una se guarda lo aprendido, como en XP.
// Uso: rocola-consola [--wav salida.wav] [--dispositivo default] [--biblioteca] [cancion1.mp3 cancion2.wav ...]
//      rocola-consola --rocola [--texto] [--dispositivo default]   la rocola con sus botones: en la pantalla (KMS y OpenGL ES,
//                     VistaGrafica.cs) o, con --texto o si no hay pantalla grafica, en la consola (VistaTexto.cs)
//      rocola-consola --captura salida.ppm [--tam 1024x768] [--boton poner ...]   un cuadro de la pantalla, fuera de pantalla (pruebas)
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
            string wav = null, dispositivo = "default", captura = null, tam = "1024x768"; bool biblioteca = false, rocola = false, texto = false; string guion = null; List<string> lista = new List<string>(), botones = new List<string>();
            // --version: la version del programa ("rocola-consola programa N (commit)"); la comprueba rocola-actualiza antes de aceptarla.
            if (args.Length > 0 && args[0] == "--version") { Console.WriteLine("rocola-consola programa " + Version.Numero + " (" + Version.Commit + ")"); return 0; }
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--mide-repaso")   // prueba: cuanto tarda medir cada archivo (no publica nada)
                {
                    Form1 mr = new Form1(); mr.Arranca();
                    for (int j = i + 1; j < args.Length; j++) Console.WriteLine(mr.PruebaRepaso(args[j]));
                    return 0;
                }
                else if (args[i] == "--wav" && i + 1 < args.Length) wav = args[++i];
                else if (args[i] == "--dispositivo" && i + 1 < args.Length) dispositivo = args[++i];
                else if (args[i] == "--biblioteca") biblioteca = true;
                else if (args[i] == "--rocola") rocola = true;
                else if (args[i] == "--texto") texto = true;
                else if (args[i] == "--guion" && i + 1 < args.Length) guion = args[++i];
                else if (args[i] == "--captura" && i + 1 < args.Length) captura = args[++i];
                else if (args[i] == "--tam" && i + 1 < args.Length) tam = args[++i];
                else if (args[i] == "--cuadros" && i + 1 < args.Length) botones.Add("cuadros=" + args[++i]);   // cuantos cuadros de 50 ms antes de la captura (26)
                else if (args[i] == "--boton" && i + 1 < args.Length) botones.Add(args[++i]);
                else if (args[i] == "--tecla" && i + 1 < args.Length) botones.Add("#" + args[++i]);
                else lista.Add(Path.GetFullPath(args[i]));
            }
            if (captura != null) return Captura(captura, tam, botones);
            if (rocola)
            {
                // V29: la pantalla primero, con el cielo y el nombre de la rocola, y despues lo que tarda (los datos y la biblioteca)
                IVista vista = null;
                if (!texto)
                {
                    try { VistaGrafica g = new VistaGrafica(new Rocola.Pantalla.Kms(), VistaGrafica.BuscaLetra()); Console.WriteLine("Pantalla: " + g.Descripcion); vista = g; g.Presentacion("Despertando la rocola..."); }
                    catch (Exception e) { Console.Error.WriteLine("Sin pantalla grafica (" + e.Message + "): en modo texto"); }
                }
                Form1 m = new Form1(); m.Arranca();
                if (vista == null) vista = new VistaTexto();
                int r;
                Controlador ctl = new Controlador(m, dispositivo); ctl.Guion = guion;
                using System.Runtime.InteropServices.PosixSignalRegistration apagar = System.Runtime.InteropServices.PosixSignalRegistration.Create(
                    System.Runtime.InteropServices.PosixSignal.SIGTERM, delegate (System.Runtime.InteropServices.PosixSignalContext ctx) { ctx.Cancel = true; ctl.Apaga(); });
                using (vista) r = ctl.Corre(vista);
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

        // UN CUADRO DE LA PANTALLA SIN PANTALLA: la rocola con su biblioteca, los botones dados (moneda, derecha, poner...) y la imagen.
        private static int Cuadros(List<string> botones)
        {
            foreach (string b in botones) if (b.StartsWith("cuadros=")) return int.Parse(b.Substring(8));
            return 26;
        }
        private static int Captura(string ruta, string tam, List<string> botones)
        {
            string[] t = tam.Split('x');
            Form1 m = new Form1(); m.Arranca();
            Rocola.Pantalla.FueraDePantalla fp = new Rocola.Pantalla.FueraDePantalla(int.Parse(t[0]), int.Parse(t[1]));
            VistaGrafica v = new VistaGrafica(fp, VistaGrafica.BuscaLetra());
            Console.WriteLine("Pantalla: " + v.Descripcion);
            if (botones.Contains("presentacion")) { v.Presentacion("Leyendo tu musica..."); fp.Captura(ruta); Console.WriteLine("Captura: " + ruta); return 0; }   // V29
            Controlador c = new Controlador(m, "null");
            c.Prepara();
            foreach (string b in botones)
                if (b.StartsWith("cuadros=")) continue;
                else if (b == "atraccion") c.FuerzaAtraccion();
                else if (b == "rojo") c.RojoDePrueba = true;
                else if (b == "diagnostico") { c.DiagnosticoDePrueba(v); Console.Write(c.UltimoDiagnostico); }   // el ROJO mantenido (las pistas del rojo)
                else if (b == "noche") { c.AtraccionDesfase = 15; c.FuerzaAtraccion(); }   // la atraccion, ya en lo mas pedido esta noche
                else if (b == "espera") { v.Dibuja(c); System.Threading.Thread.Sleep(500); }   // como alguien que mira antes de apretar
                else if (b == "reposo")
                {
                    // algo sonando y cinco en la cola (sin salida: solo para ver la pantalla), y 20 s sin botones
                    m.Moneda(); m.Moneda();
                    foreach (Form1.Disco dd in c.discos) foreach (string r in dd.Rutas) if (!dd.Videos && m.EnCola < 6) m.Pide(r);
                    m.EmpiezaSiguiente();
                    c.FuerzaReposo();
                }
                else if (b == "sonando")
                {
                    // algo sonando y cinco en la cola, en la pared (sin salida: solo para ver la pantalla)
                    m.Moneda(); m.Moneda();
                    foreach (Form1.Disco dd in c.discos) foreach (string r in dd.Rutas) if (!dd.Videos && m.EnCola < 6) m.Pide(r);
                    m.EmpiezaSiguiente();
                }
                else if (b.StartsWith("#")) c.PulsaTecla(int.Parse(b.Substring(1)));   // --tecla 119: F8 (el asistente), o cualquier codigo de Windows
                else if (b.StartsWith("regresar+")) c.Pulsa((Boton)Enum.Parse(typeof(Boton), b.Substring(9), true), true);   // con REGRESAR mantenido
                else c.Pulsa((Boton)Enum.Parse(typeof(Boton), b, true));
            // que termine de aparecer lo que se anima (la atraccion, el reposo, la estrella que vuela): unos cuadros durante 1,3 s
                for (int i = 0; i < Cuadros(botones); i++) { m.PublicaMedidas(); v.Dibuja(c); System.Threading.Thread.Sleep(50); }   // lo medido en fondo, a las tablas (como el bucle de la rocola)   // que termine de aparecer
            v.Dibuja(c);
            fp.Captura(ruta);
            m.GuardaEstado(); m.Cierra();   // lo aprendido y las estadisticas, como al cerrar la rocola
            Console.WriteLine("Captura: " + ruta);
            return 0;
        }
    }
}
