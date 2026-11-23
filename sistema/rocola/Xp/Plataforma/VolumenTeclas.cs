// LAS TECLAS DE VOLUMEN (22-11-2026, el dueno: «Mi rocola tiene 2 botones atras que restan o aumentaban el volumen del sistema en Windows
// XP, necesito que funcionen en rocola OS»). En XP esos botones mandan las teclas multimedia de volumen (VK_VOLUME_DOWN / VK_VOLUME_UP) y
// las atiende Windows, no la rocola: cambian el volumen general del sistema (el que la rocola sube al maximo al arrancar, VolumenWindows.cs).
// En Linux llegan como KEY_VOLUMEDOWN (114) y KEY_VOLUMEUP (115) y nadie las atendia. Aqui se hace lo de Windows: el volumen general de la
// tarjeta que usa la rocola (la de ROCOLA_ALSA; el lanzador lo pone al 100 % al arrancar, como XP) baja o sube un 2 % por toque (Windows XP
// mueve 1/50 de su escala por toque) y, dejando el boton apretado, se repite a los 500 ms unas 10 veces por segundo hasta soltarlo (se
// pregunta al nucleo si sigue abajo: un soltar perdido no deja el volumen bajando solo). KEY_MUTE (113) quita o pone el silencio. Por
// amixer, en un hilo aparte que junta los toques (nunca espera la pantalla). La tecla no le llega a la rocola, como en XP.
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace Rocola
{
    internal static class VolumenTeclas
    {
        private const int KEY_MUTE = 113, KEY_VOLUMEDOWN = 114, KEY_VOLUMEUP = 115;
        private const int PasoPct = 2, RetrasoMs = 500, RepiteMs = 100;
        private static readonly object candado = new object();
        private static readonly AutoResetEvent senal = new AutoResetEvent(false);
        private static int pendiente;        // pasos por aplicar (+ sube, - baja)
        private static bool silencio;        // un toque de silencio por aplicar
        private static int apretada;         // el codigo apretado ahora (0: ninguno)
        private static long apretadaDesde;   // cuando (ms)
        private static long ultimoPaso;
        private static Thread hilo;
        private static string control;       // el control de amixer (Master, si no PCM...)
        private static string tarjeta;       // «-c N» o ""

        // true = era una tecla de volumen (ya atendida)
        internal static bool Atiende(int codigo, bool abajo)
        {
            if (codigo != KEY_VOLUMEUP && codigo != KEY_VOLUMEDOWN && codigo != KEY_MUTE) return false;
            lock (candado)
            {
                Arranca();
                if (codigo == KEY_MUTE) { if (abajo) silencio = true; }
                else if (abajo) { pendiente += codigo == KEY_VOLUMEUP ? 1 : -1; apretada = codigo; apretadaDesde = Ahora(); ultimoPaso = apretadaDesde; }
                else if (apretada == codigo) apretada = 0;
            }
            senal.Set();
            return true;
        }

        private static long Ahora() { return Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency; }

        private static void Arranca()
        {
            if (hilo != null) return;
            hilo = new Thread(Bucle) { IsBackground = true, Name = "volumen" };
            hilo.Start();
        }

        private static void Bucle()
        {
            while (true)
            {
                int pasos; bool mudo; int ap;
                lock (candado)
                {
                    ap = apretada;
                    if (ap != 0)
                    {
                        long t = Ahora();
                        if (t - apretadaDesde >= RetrasoMs && t - ultimoPaso >= RepiteMs)
                        {
                            if (TeclasLinux.FisicaLinux(ap) == 0) apretada = ap = 0;   // (se perdio el soltar)
                            else { pendiente += ap == KEY_VOLUMEUP ? 1 : -1; ultimoPaso = t; }
                        }
                    }
                    pasos = pendiente; pendiente = 0;
                    mudo = silencio; silencio = false;
                }
                try
                {
                    if (pasos != 0) Amixer((Math.Abs(pasos) * PasoPct).ToString(CultureInfo.InvariantCulture) + "%" + (pasos > 0 ? "+" : "-") + (pasos > 0 ? " unmute" : ""));
                    if (mudo) Amixer("toggle");
                }
                catch { }
                if (ap != 0) senal.WaitOne(30); else senal.WaitOne();
            }
        }

        private static long eligioMs;
        private static void Amixer(string que)
        {
            // (23-11-2026, revision) sin control encontrado (la tarjeta aun arrancando, amixer que fallo) se vuelve a buscar, como mucho cada
            // 30 s: antes la primera vez valia para siempre y los botones quedaban muertos hasta reiniciar
            if (control == null || (control.Length == 0 && Ahora() - eligioMs >= 30000)) { eligioMs = Ahora(); Elige(); }
            if (control.Length == 0) return;
            Corre(tarjeta + "-q sset '" + control + "' " + que);
        }

        // la tarjeta de la rocola (ROCOLA_ALSA: «plughw:0,0», «hw:CARD=Generic,DEV=0»...) y su primer control de volumen general
        private static void Elige()
        {
            tarjeta = "";
            string e = null; try { e = Environment.GetEnvironmentVariable("ROCOLA_ALSA"); } catch { }
            if (!string.IsNullOrEmpty(e))
            {
                int i = e.IndexOf("hw:", StringComparison.Ordinal);
                if (i >= 0)
                {
                    string c = e.Substring(i + 3);
                    if (c.StartsWith("CARD=", StringComparison.Ordinal)) c = c.Substring(5);
                    int k = c.IndexOfAny(new[] { ',', ' ' }); if (k >= 0) c = c.Substring(0, k);
                    bool limpio = c.Length > 0;
                    foreach (char ch in c) if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '-') limpio = false;
                    if (limpio) tarjeta = "-c " + c + " ";
                }
            }
            control = "";
            string lista = Corre(tarjeta + "scontrols") ?? "";
            foreach (string nombre in new[] { "Master", "PCM", "Speaker", "Front", "Headphone" })
                if (lista.IndexOf("'" + nombre + "'", StringComparison.Ordinal) >= 0) { control = nombre; break; }
        }

        private static string Corre(string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo("/bin/sh", "-c \"amixer " + args.Replace("\"", "") + "\"") { UseShellExecute = false, RedirectStandardOutput = true };
            using (Process p = Process.Start(psi))
            {
                // (23-11-2026, revision) con tope: un amixer colgado ya no deja colgado el hilo del volumen (ReadToEnd esperaba para siempre)
                System.Threading.Tasks.Task<string> lee = p.StandardOutput.ReadToEndAsync();
                if (!p.WaitForExit(3000)) { try { p.Kill(true); } catch { } return null; }
                return lee.Wait(1000) ? lee.Result : null;
            }
        }
    }
}
