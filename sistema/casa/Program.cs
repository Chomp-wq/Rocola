// ROCOLA EN CASA (peticion del dueno del 7-11-2026): la herramienta para trabajar en la rocola desde la laptop (Pop!_OS) o la computadora
// (Windows). La rocola esta detras del router del bar: todo pasa por el «buzon» (un repositorio privado de GitHub que la rocola lee y
// escribe con rocola-buzon, en el sistema) y por el repositorio de la rocola (sus versiones y sus compilaciones).
//
//   rocola-casa                  abre el tablero en el navegador (un servidor solo para esta computadora: 127.0.0.1)
//   rocola-casa --estado         lo esencial, en la terminal
//   rocola-casa --orden ORDEN [ARG]   deja una orden a la rocola (ping, informe, registros, archivo NOMBRE, buscar_actualizacion,
//                                diagnostico, reiniciar_programa [ya], reiniciar_equipo [ya])
//   rocola-casa --sin-navegador  el tablero sin abrir el navegador (para abrirlo a mano: lo dice la terminal)
// La configuracion (la llave de GitHub, los repositorios, el encendido remoto) va en ~/.config/rocola-casa/config.json (Linux) o
// %APPDATA%\RocolaCasa\config.json (Windows); lo que se baja, en ~/RocolaCasa.
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace RocolaCasa
{
    internal static class Program
    {
        internal static string Version
        {
            get
            {
                foreach (AssemblyMetadataAttribute a in typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
                    if (a.Key == "CasaVersion") return a.Value;
                return "0";
            }
        }

        private static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Config cfg = Config.Lee();
            bool navegador = true;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--version") { Console.WriteLine("Rocola en casa " + Version); return 0; }
                if (a == "--sin-navegador") navegador = false;
                else if (a == "--estado") return await EstadoEnTerminal(cfg);
                else if (a == "--orden" && i + 1 < args.Length)
                {
                    if (!cfg.Lista) { Console.WriteLine("Falta la llave de GitHub: abre el tablero (rocola-casa) y ponla en Ajustes."); return 2; }
                    Buzon b = new Buzon(cfg);
                    string id = await b.Ordena(args[i + 1], i + 2 < args.Length ? args[i + 2] : "");
                    Console.WriteLine(id != null ? "Orden dejada (" + id + "). La rocola la lee en menos de un minuto." : "No se pudo dejar la orden: " + b.UltimoError);
                    return id != null ? 0 : 1;
                }
                else if (a == "--ayuda" || a == "-h" || a == "--help")
                {
                    Console.WriteLine("rocola-casa [--estado | --orden ORDEN [ARG] | --sin-navegador | --version]");
                    return 0;
                }
            }
            Servidor s = new Servidor(cfg);
            int puerto = s.Arranca();
            string url = "http://127.0.0.1:" + puerto + "/";
            Console.WriteLine("Rocola en casa " + Version + ": el tablero esta en " + url + "  (Ctrl+C para cerrar)");
            if (navegador) AbreNavegador(url);
            await s.Atiende();
            return 0;
        }

        internal static void AbreNavegador(string url)
        {
            try
            {
                if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                else if (OperatingSystem.IsMacOS()) Process.Start("open", url);
                else Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true });
            }
            catch { Console.WriteLine("Abre en tu navegador: " + url); }
        }

        // --estado: lo esencial en la terminal (sin el tablero).
        private static async Task<int> EstadoEnTerminal(Config cfg)
        {
            if (!cfg.Lista) { Console.WriteLine("Falta la llave de GitHub: abre el tablero (rocola-casa) y ponla en Ajustes."); return 2; }
            Buzon b = new Buzon(cfg);
            JsonObject t = await b.Todo();
            Console.WriteLine(Resumen.Texto(t));
            return 0;
        }
    }
}
