using System;
using System.Runtime.InteropServices;

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
        private static string avisoCarpetaDatos;

        internal static void AvisaCarpetaDatos(string carpeta, Exception ex)
        {
            avisoCarpetaDatos = "No se pudo usar la carpeta de datos:\r\n" + carpeta
                + "\r\n\r\nSe usara la carpeta del programa:\r\n" + CarpetaPrograma.Ruta   // EN LINUX: Application.StartupPath -> CarpetaPrograma.Ruta (Nativo/CarpetaPrograma.cs): la carpeta del programa sin la barra del final, como la de WinForms
                + "\r\n\r\nLos datos de la otra carpeta no se han borrado.\r\n" + ex.Message;
        }

        // El nombre de la senal sale de DONDE ESTAN LOS DATOS, no es fijo: asi dos instalaciones distintas
        // en el mismo equipo -por ejemplo una de pruebas- no se estorban. Y son los datos y no el programa
        // porque lo que de verdad se pisaria son los archivos: dos rocolas escribiendo la misma carpeta de
        // datos se pisan aunque el programa este en dos sitios distintos. Se calcula a mano (FNV)
        // y no con GetHashCode porque ese puede cambiar entre versiones de .NET y de un arranque a otro, y
        // aqui hace falta que dos arranques distintos saquen EL MISMO nombre.
        internal static string ClaveInstancia(string carpeta)
        {
            string c = NormalizaCarpetaDatos(carpeta).TrimEnd('\\', '/').ToLowerInvariant();
            uint h = 2166136261u;
            for (int i = 0; i < c.Length; i++) { h ^= c[i]; h *= 16777619u; }
            return "Rocola_" + h.ToString("X8");
        }

        internal static string NormalizaCarpetaDatos(string carpeta)
        {
            // Una ruta relativa y sus variantes con puntos/barras deben designar la misma instalacion.
            string ruta = System.IO.Path.GetFullPath((carpeta ?? "").Trim());
            // Las baterias tambien se pueden compilar con Mono fuera de Windows.
            // No convertir su ruta temporal nativa en un nombre con barras invertidas.
            return System.IO.Path.DirectorySeparatorChar == '\\' ? ruta.Replace('/', '\\') : ruta;
        }

        // EN LINUX: la otra rocola es otro programa, con su propia pantalla: no hay ventana que traer al frente
        private static bool SetForegroundWindow(IntPtr h) { return false; }
        private static bool ShowWindow(IntPtr h, int cmd) { return false; }

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
            NLayerIncluido.Engancha();   // NLayer va dentro de Rocola.exe: no hace falta NLayer.dll al lado (FlotanteNLayer.cs)
            bool primera;
            unica = new System.Threading.Mutex(true, ClaveInstancia(Form1.CarpetaDatos), out primera);
            if (!primera) { AlFrenteLaQueYaCorre(); return; }   // ya hay una rocola corriendo: esta se va

            // Registro de errores: la UI puede continuar tras algunos fallos, sin garantia de estado sano.
            // UnhandledException de otros hilos puede terminar el proceso; registrar no lo recupera.
            // No hay reinicio automatico ni se oculta esa limitacion al operador.
            PantallaRocola.AlFallar = delegate (Exception ex)   // EN LINUX: lo que lance una tecla, un reloj, un encargo o un pintado en la pantalla nativa (en XP, Application.ThreadException) se apunta y la rocola sigue
            { LogCrash("ThreadException", ex); };   // EN LINUX: la excepcion llega sola
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            { LogCrash("UnhandledException", e.ExceptionObject as Exception); };   // hilos de fondo: al menos queda el rastro
            PruebasLinux.DespuesDelInicio();   // EN LINUX: (XP) Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException): la pantalla nativa ya da a AlFallar lo que lanzan sus teclas, relojes, encargos y pintados (CatchException); aqui, las pruebas de Linux que necesitan hilos (no son de XP: ya acabo el inicializador del modulo)
            if (avisoCarpetaDatos != null)
            {
                LogCrash("Carpeta de datos: se utiliza la carpeta del programa", new System.IO.IOException(avisoCarpetaDatos));
                AvisoXp.Muestra(avisoCarpetaDatos, "Rocola - Carpeta de datos", BotonesAviso.OK, IconoAviso.Warning);   // EN LINUX: MessageBox.Show -> AvisoXp.Muestra (Nativo/AvisoXp.cs): el aviso de user32, modal, antes de abrir la rocola
            }

            // (El reloj fino de Windows -timeBeginPeriod(1)- ya lo pide Form1 al construirse, y lo devuelve
            // al cerrarse. Aqui no hace falta repetirlo.)
            PrioridadDeLaRocola();   // la musica y la pantalla, por delante de las tareas de Windows (ver abajo)
            // EN LINUX: (XP) Application.EnableVisualStyles(); el tema de XP en los controles: lo pintan las piezas de la rocola, con los colores de Luna
            // EN LINUX: (XP) Application.SetCompatibleTextRenderingDefault(false); el texto de Label y Button con GDI (TextRenderer): las piezas lo pintan con TextoGdi
            PantallaRocola.Corre(new Form1());   // EN LINUX: la pantalla nativa (en XP, Application.Run): la rocola, sus relojes, encargos y teclas en este hilo hasta que se cierra; el apagado del sistema la cierra como el de Windows
            Bitacora.Vacia(3000);   // lo que quedara por apuntar (la bitacora escribe detras: MedidasBitacora.cs)
            // Se suelta al salir por las buenas. (Ademas hay que TOCAR la variable en algun sitio: si solo
            // se guardara y nunca se usara, el recolector de basura podria llevarsela con la rocola aun
            // corriendo, y entonces otra rocola podria arrancar encima.)
            try { unica.ReleaseMutex(); } catch { }
        }

        // LA ROCOLA POR DELANTE DE LAS TAREAS DE WINDOWS (rendimiento, 28-09-2026). El equipo del bar es de la rocola, pero
        // Windows sigue a lo suyo -actualizaciones, el antivirus repasando el disco, el indexador- y, con la prioridad normal,
        // le quita procesador justo cuando lo necesita: en dos nucleos, eso es la musica o la pantalla esperando. "Por encima
        // de lo normal" la pone delante de esas tareas sin llegar a "alta", que puede estorbar al propio Windows (el raton, el
        // sonido del sistema). Lo de fondo de la rocola (el repaso de la biblioteca, las portadas) sigue en prioridad minima
        // o baja DENTRO de ella: nunca por delante de su pantalla ni de su musica. Los programas que lanza (el motor de la
        // IA) no la heredan: Windows les da la normal. Si Windows no deja cambiarla, sigue como siempre.
        internal static void PrioridadDeLaRocola()
        {
            // EN LINUX: (Process.GetCurrentProcess().PriorityClass = AboveNormal: SetPriorityClass) en Linux .NET solo cambia el hilo principal:
            try { PrioridadHilo.DelProceso(System.Diagnostics.ProcessPriorityClass.AboveNormal); }   // aqui el nice de todos los hilos y la base de los que nazcan, como la clase de Windows (Nativo/PrioridadHilo.cs)
            catch { }
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
                Bitacora.ApuntaConSello(System.IO.Path.Combine(Form1.CarpetaDatos, "rocola_error.txt"), 262144,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [" + context + "]",
                    "\r\n" + (ex != null ? ex.ToString() : "(sin excepcion)") + "\r\n\r\n");
                // Un fallo asi puede terminar el proceso: se escribe ya, en este hilo, con lo que hubiera pendiente.
                Bitacora.Vacia(3000);
            }
            catch { }
        }
    }
}
