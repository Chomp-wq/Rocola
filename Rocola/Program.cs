using System;
using System.Windows.Forms;

namespace Rocola
{
    internal static class Program
    {
        /// <summary>
        /// Punto de entrada principal para la aplicación.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // BLINDAJE DE KIOSCO: una excepcion no controlada en el hilo de la interfaz mostraba el dialogo
            // de error de .NET y dejaba la rocola muerta hasta reiniciarla a mano (sin teclado ni mouse en el
            // local). Ahora se anota en rocola_error.txt (junto al .exe) y la rocola SIGUE corriendo.
            Application.ThreadException += delegate (object s, System.Threading.ThreadExceptionEventArgs e)
            { LogCrash("ThreadException", e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            { LogCrash("UnhandledException", e.ExceptionObject as Exception); };   // hilos de fondo: al menos queda el rastro
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            // RELOJ FINO DE WINDOWS. Por defecto Windows solo despierta a los programas cada 15.6 ms: un
            // Sleep(1) duerme 15.6, y un temporizador de 42 ms llega a los 46.8. Eso hacia que el limitador de
            // cuadros del panel (un cuadro cada 33 ms con video a la vista) acabara dando uno cada 46.8, o sea
            // 21 cuadros por segundo en vez de 30. Pidiendo 1 ms -que es lo que hace cualquier programa de
            // video o de sonido, el propio Windows Media Player incluido- todo cae donde tiene que caer.
            try { timeBeginPeriod(1); } catch { }
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Form1());
            }
            finally { try { timeEndPeriod(1); } catch { } }
        }

        [System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint timeBeginPeriod(uint ms);
        [System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint timeEndPeriod(uint ms);

        // Mismo archivo y tope (~256KB) que Form1.LogError, pero autonomo (aqui Form1 pudo no existir aun).
        private static void LogCrash(string context, Exception ex)
        {
            try
            {
                string f = System.IO.Path.Combine(Application.StartupPath, "rocola_error.txt");
                try { System.IO.FileInfo fi = new System.IO.FileInfo(f); if (fi.Exists && fi.Length > 262144) fi.Delete(); } catch { }
                System.IO.File.AppendAllText(f,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [" + context + "]\r\n"
                    + (ex != null ? ex.ToString() : "(sin excepcion)") + "\r\n\r\n");
            }
            catch { }
        }
    }
}
