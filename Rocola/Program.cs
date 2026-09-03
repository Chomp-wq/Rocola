using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Rocola
{
    internal static class Program
    {
        // UNA SOLA ROCOLA A LA VEZ.
        //
        // No habia nada que impidiera abrirla dos veces, y en un local pasa: la rocola tarda en arrancar
        // -tiene que leer la biblioteca-, parece que no hizo nada, y se le da doble clic otra vez. O el
        // atajo del arranque de Windows coincide con uno manual. Con dos rocolas corriendo a la vez:
        //
        //   - suenan DOS canciones encima;
        //   - las dos escriben los mismos archivos y se pisan, o sea que se pierden los creditos, el
        //     volumen medido de la biblioteca y lo mas sonado (el candado de los guardados vale dentro de
        //     un programa, no entre dos programas distintos);
        //   - los creditos van por separado: la moneda entra en una y el cliente esta mirando la otra;
        //   - y el repaso de la biblioteca corre dos veces, con el disco moliendo el doble durante horas.
        //
        // Se marca con una senal del sistema (un "mutex" con nombre). Si ya hay una corriendo, la nueva
        // trae la vieja al frente y se va sin hacer nada.
        private static System.Threading.Mutex unica;

        // El nombre de la senal sale de DONDE ESTAN LOS DATOS, no es fijo: asi dos instalaciones distintas
        // en el mismo equipo -por ejemplo una de pruebas- no se estorban. Y son los datos y no el programa
        // porque lo que de verdad se pisaria son los archivos: dos rocolas escribiendo la misma carpeta de
        // datos se pisan aunque el programa este en dos sitios distintos. Se calcula a mano (FNV)
        // y no con GetHashCode porque ese puede cambiar entre versiones de .NET y de un arranque a otro, y
        // aqui hace falta que dos arranques distintos saquen EL MISMO nombre.
        internal static string ClaveInstancia(string carpeta)
        {
            string c = (carpeta ?? "").TrimEnd('\\', '/').ToLowerInvariant();
            uint h = 2166136261u;
            for (int i = 0; i < c.Length; i++) { h ^= c[i]; h *= 16777619u; }
            return "Rocola_" + h.ToString("X8");
        }

        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);

        private static void AlFrenteLaQueYaCorre()
        {
            try
            {
                System.Diagnostics.Process yo = System.Diagnostics.Process.GetCurrentProcess();
                foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcessesByName(yo.ProcessName))
                {
                    if (p.Id == yo.Id || p.MainWindowHandle == IntPtr.Zero) continue;
                    ShowWindow(p.MainWindowHandle, 9);   // 9 = restaurar si estaba minimizada
                    SetForegroundWindow(p.MainWindowHandle);
                    break;
                }
            }
            catch { }
        }

        /// <summary>
        /// Punto de entrada principal para la aplicación.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool primera;
            unica = new System.Threading.Mutex(true, ClaveInstancia(Form1.CarpetaDatos), out primera);
            if (!primera) { AlFrenteLaQueYaCorre(); return; }   // ya hay una rocola corriendo: esta se va

            // BLINDAJE DE KIOSCO: una excepcion no controlada en el hilo de la interfaz mostraba el dialogo
            // de error de .NET y dejaba la rocola muerta hasta reiniciarla a mano (sin teclado ni mouse en el
            // local). Ahora se anota en rocola_error.txt (junto al .exe) y la rocola SIGUE corriendo.
            Application.ThreadException += delegate (object s, System.Threading.ThreadExceptionEventArgs e)
            { LogCrash("ThreadException", e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            { LogCrash("UnhandledException", e.ExceptionObject as Exception); };   // hilos de fondo: al menos queda el rastro
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            // (El reloj fino de Windows -timeBeginPeriod(1)- ya lo pide Form1 al construirse, y lo devuelve
            // al cerrarse. Aqui no hace falta repetirlo.)
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
            // Se suelta al salir por las buenas. (Ademas hay que TOCAR la variable en algun sitio: si solo
            // se guardara y nunca se usara, el recolector de basura podria llevarsela con la rocola aun
            // corriendo, y entonces otra rocola podria arrancar encima.)
            try { unica.ReleaseMutex(); } catch { }
        }

        // Mismo archivo y tope (~256KB) que Form1.LogError, pero autonomo (aqui Form1 pudo no existir aun).
        //
        // Y CADA APUNTE DICE DE QUE VERSION SALIO. Sin eso, un error que llega por mensaje no se puede
        // diagnosticar: paso de verdad con uno de "acmStreamClose" fechado quince dias DESPUES de haberlo
        // arreglado, y no habia forma de saber si era un fallo nuevo o simplemente una rocola vieja que
        // nadie habia vuelto a instalar. Con la fecha del ejecutable al lado, el apunte se explica solo.
        // El sello sale de Form1.DeCuandoEs(), que es el mismo que se ensena en Ctrl+F3 y en la bitacora.
        private static void LogCrash(string context, Exception ex)
        {
            try
            {
                string f = System.IO.Path.Combine(Form1.CarpetaDatos, "rocola_error.txt");
                try { System.IO.FileInfo fi = new System.IO.FileInfo(f); if (fi.Exists && fi.Length > 262144) fi.Delete(); } catch { }
                string sello = "";
                try { sello = Form1.DeCuandoEs(); } catch { }
                if (sello.Length > 0) sello = "  (rocola del " + sello + ")";
                System.IO.File.AppendAllText(f,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [" + context + "]" + sello + "\r\n"
                    + (ex != null ? ex.ToString() : "(sin excepcion)") + "\r\n\r\n");
            }
            catch { }
        }
    }
}
