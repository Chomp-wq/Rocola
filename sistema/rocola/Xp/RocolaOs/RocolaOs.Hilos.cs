// LOS HILOS, LOS ENCARGOS, LAS RUTAS Y LA BITACORA DE ROCOLA OS (fase 11, grupo andamio; PLAN_FASE11.md §2.4).
//
// LOS HILOS (RocolaOs.Hilo): de fondo (no mantienen vivo el programa), con nombre «RocolaOs:<nombre>», y lo atrapan TODO: una excepcion sin
// atrapar en otro hilo cierra el programa (Codigo/Program.cs solo la apunta) y el lanzador lo cuenta como caida. Su prioridad se pone al
// empezar, en el propio hilo (en Linux cada hilo nace con el nice del que lo crea: uno creado desde la pantalla correria a -6, por delante del
// repaso de XP; Nativo/PrioridadHilo.cs):
//   Hilo(nombre, trabajo)              LO DE FONDO: por debajo del repaso de XP (que va en la minima de la rocola, nice 4): nice 19, el
//                                      «Prioridad.Fondo» del programa anterior. Para emparejar, indexar, convertir, entrenar, mirar la red...
//   Hilo(nombre, prioridad, trabajo)   un nivel de XP (PrioridadHilo.Pon: el nice de ese nivel), para lo poco que no puede ir al fondo
//                                      (lo que alimenta al sonido). Nunca por delante de la salida de sonido de XP (la mas alta).
// Un hilo de Rocola OS no toca el estado de XP (se lee en el hilo de la pantalla: a los hilos, una copia), ni OpenGL, ni la pantalla, y nada
// lo espera desde el hilo del sonido.
//
// LOS ENCARGOS (RocolaOs.Encarga): lo que un hilo le pide al hilo de la pantalla (en XP, BeginInvoke a la ventana de la rocola), con nombre
// fijo «RocolaOs:<nombre>» (las trazas lo nombran asi, sin mover los de XP, que van por archivo:linea; compara_trazas.py --ignora RocolaOs:
// lo quita). Viajan con la vida de la ventana de la rocola: antes de ensenarse o despues de cerrarse no se encargan (devuelve false), y uno
// que llega con la ventana ya cerrada no corre. Lo que lancen se apunta.
//
// LAS RUTAS: Run (lo vivo, en memoria: /run, o ROCOLA_RUN en las pruebas, como en los guiones del sistema), Datos (lo pequeno, en la carpeta
// de datos de rocola-xp, /rocola/datos/xp: nombres rocola_*.txt, asi rocola-instalar les traduce las rutas y el buzon los encuentra) y
// Grande (lo grande, en el disco de la rocola: Nativo/DatosGrandes.cs, ROCOLA_IA_DATOS). Las llaves (rocola_github.txt, rocola_buzon.txt,
// rocola_genius.txt, /rocola/datos/spotify) se leen donde estan y nunca se copian, se apuntan ni se suben; en /rocola/datos (lo de
// rocola-consola) no se escribe.
//
// LA BITACORA (decision del andamio). Dos sitios, porque son dos cosas:
//   - lo que pasa (que arranco, que se encendio, un ajuste de fuera, cuanto tardo el cierre, un latido lento): rocola_os_bitacora.txt, PROPIA,
//     en la carpeta de datos (Apunta). Asi no llena rocola_error.txt de XP, que borra todo al pasar de 256 KB (Codigo/MedidasBitacora.cs,
//     Pega) y se llevaria los errores de XP; y el buzon ya la sube con los registros (rocola*.txt de /rocola/datos/xp, como xp_...).
//   - los fallos (una excepcion atrapada en un hilo, un latido, un encargo o un arranque): ADEMAS, en rocola_error.txt con el formato de
//     LogError de XP (la pila y el sello de la version), porque ahi miran el dueno y el buzon (estado.json lleva sus ultimos 40 renglones), y
//     junto a los de XP se ve que paso antes de que. Para no tapar los de XP, el mismo fallo (quien, que, tipo y mensaje) se apunta como mucho
//     una vez cada 10 minutos, y la siguiente dice cuantas veces se callo.
// Las dos con la Bitacora de XP (un hilo de fondo; quien apunta no espera al disco; tope de 256 KB; el Main de XP la vacia al salir). Sin
// ROCOLA_OS no se apunta nada.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Rocola
{
    internal static partial class RocolaOs
    {
        // ---------- los hilos ----------
        internal const int NiceDeFondo = 19;
        [DllImport("libc", SetLastError = true)] private static extern int setpriority(int cual, uint quien, int nice);

        // LO DE FONDO: por debajo del repaso de XP (nice 19)
        internal static Thread Hilo(string nombre, Action trabajo) { return NuevoHilo(nombre, ThreadPriority.Lowest, true, trabajo); }
        // UN NIVEL DE XP (Nativo/PrioridadHilo.cs)
        internal static Thread Hilo(string nombre, ThreadPriority prioridad, Action trabajo) { return NuevoHilo(nombre, prioridad, false, trabajo); }
        private static Thread NuevoHilo(string nombre, ThreadPriority p, bool deFondo, Action trabajo)
        {
            Thread t = new Thread(delegate ()
            {
                try
                {
                    PrioridadHilo.Pon(Thread.CurrentThread, p);   // (el nivel apuntado, como los de XP, y su nice: en Linux, en el propio hilo)
                    if (deFondo) setpriority(0, PrioridadHilo.Este(), NiceDeFondo);   // (PRIO_PROCESS con el numero del hilo: ese hilo)
                }
                catch { }
                try { trabajo(); }
                catch (ThreadInterruptedException) { }   // (quien lo para con Interrupt)
                catch (Exception ex) { Falla(nombre, "hilo", ex); }
            });
            t.IsBackground = true;
            t.Name = "RocolaOs:" + nombre;
            t.Start();
            return t;
        }

        // ---------- los encargos ----------
        // UN ENCARGO AL HILO DE LA PANTALLA, con nombre fijo «RocolaOs:<nombre>» (desde cualquier hilo). false si la ventana de la rocola aun
        // no existe o ya se cerro (no se encarga).
        internal static bool Encarga(string nombre, Action que)
        {
            Form1 r = LaRocola;
            if (r == null || que == null) return false;
            try
            {
                PantallaRocola.EncolaDesde(r, delegate
                {
                    try { que(); }
                    catch (Exception ex) { Falla(nombre, "encargo", ex); }
                }, "RocolaOs:" + nombre, false);
                return true;
            }
            catch (InvalidOperationException) { return false; }   // (la ventana sin crear: EncolaDesde lanza, como BeginInvoke)
            catch (Exception ex) { Falla(nombre, "encargar", ex); return false; }
        }
        // si el que llama es el hilo de la pantalla
        internal static bool EnLaPantalla { get { return PantallaRocola.EnSuHilo; } }

        // ---------- las rutas ----------
        private static string run;
        // LO VIVO, en memoria: /run (ROCOLA_RUN en las pruebas, como los guiones del sistema)
        internal static string Run(string nombre)
        {
            string d = Volatile.Read(ref run);
            if (d == null)
            {
                string v = null;
                try { v = Environment.GetEnvironmentVariable("ROCOLA_RUN"); } catch { }
                d = string.IsNullOrEmpty(v) || v.Trim().Length == 0 ? "/run" : v.Trim();
                Volatile.Write(ref run, d);
            }
            return Path.Combine(d, nombre);
        }
        // LO PEQUENO: la carpeta de datos de rocola-xp (la de XP: ROCOLA_DATOS)
        internal static string Datos(string nombre) { return Path.Combine(Form1.CarpetaDatos, nombre); }
        // LO GRANDE: el disco de la rocola (ROCOLA_IA_DATOS; sin ella, la carpeta de datos, como XP)
        internal static string Grande(string nombre) { return DatosGrandes.Ruta(nombre); }

        // ---------- la bitacora ----------
        internal const string ArchivoBitacora = "rocola_os_bitacora.txt";
        internal const long TopeBitacora = 262144;
        internal const int SilencioDeFallosMs = 600000;   // el mismo fallo, como mucho una vez cada 10 minutos

        // LO QUE PASA: un renglon en rocola_os_bitacora.txt («fecha  [quien] que»). Desde cualquier hilo; no espera al disco.
        internal static void Apunta(string quien, string que)
        {
            if (!Activo) return;
            try { Bitacora.Apunta(Datos(ArchivoBitacora), TopeBitacora, Fecha() + "  [" + quien + "] " + (que ?? "").Replace('\n', ' ').Replace('\r', ' ') + "\n"); }
            catch { }
        }
        private static string Fecha() { return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); }

        private sealed class Callado { internal long Ultimo; internal int Veces; }
        private static readonly Dictionary<string, Callado> callados = new Dictionary<string, Callado>(StringComparer.Ordinal);
        // UN FALLO: un renglon en la bitacora propia y la pila en rocola_error.txt (como LogError de XP), el mismo como mucho una vez cada
        // 10 minutos. Desde cualquier hilo; nunca lanza.
        internal static void Falla(string quien, string que, Exception ex)
        {
            if (!Activo) return;
            try
            {
                string clave = quien + "|" + que + "|" + (ex != null ? ex.GetType().FullName + "|" + ex.Message : "");
                int veces;
                long ahora = Environment.TickCount64;
                lock (callados)
                {
                    Callado c;
                    if (callados.TryGetValue(clave, out c) && ahora - c.Ultimo < SilencioDeFallosMs) { c.Veces++; return; }
                    if (c == null) { c = new Callado(); callados[clave] = c; }
                    veces = c.Veces; c.Veces = 0; c.Ultimo = ahora;
                }
                string mas = veces > 0 ? " (y " + veces.ToString(CultureInfo.InvariantCulture) + " veces mas, calladas, desde el apunte anterior)" : "";
                Apunta(quien, "FALLO en " + que + ": " + (ex != null ? ex.GetType().Name + ": " + ex.Message : "(sin excepcion)") + mas);
                Bitacora.ApuntaConSello(Datos("rocola_error.txt"), 262144, Fecha() + "  [Rocola OS: " + quien + ": " + que + "]",
                    "\r\n" + (ex != null ? ex.ToString() : "(sin excepcion)") + mas + "\r\n\r\n");
            }
            catch { }
        }
        private static readonly HashSet<string> avisados = new HashSet<string>(StringComparer.Ordinal);
        // un aviso del andamio, una sola vez por sesion
        private static void AvisaUnaVez(string que)
        {
            lock (avisados) { if (!avisados.Add(que)) return; }
            Apunta("andamio", que);
        }
    }
}
