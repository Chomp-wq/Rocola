// EL CIERRE DE LO DE ROCOLA OS (fase 11, grupo andamio; PLAN_FASE11.md §2.4, «Los cierres»).
//
// El envoltorio de Nativo/Form1.Ventana.cs (gancho g22: Form1.RocolaOsEnvuelveElCierre, en RocolaOs.Form1.cs) llama a SeCierra con la razon
// del cierre ANTES de OnFormClosing de XP (donde XP para el sonido, guarda lo aprendido y, con WindowsShutDown o UserClosing, borra el
// estado). Sin ROCOLA_OS el envoltorio es el mismo delegado de XP y esto no corre. Las razones (regla del dueno del 8-11-2026; no cambian):
//   SIGTERM (el apagado del sistema)             WindowsShutDown      XP borra el estado
//   Alt+F4, Close                                UserClosing          XP borra el estado
//   SIGUSR1 (los reinicios propios de Rocola OS)  TaskManagerClosing   XP lo conserva
//   un apagon (kill -9, la luz)                  nada corre
// Las salidas propias de Rocola OS (42 a 46: las hace 1a) ponen Environment.ExitCode y van por esas mismas vias (PantallaRocola.CierreDeFuera
// conserva el estado; PantallaRocola.FinDeSesion lo borra).
//
// LO QUE HACE, UNA VEZ (el apagado avisa dos veces, WM_QUERYENDSESSION y WM_ENDSESSION: la segunda ya no hace nada):
//   1. apunta la razon (RazonDelCierre) y para el latido;
//   2. el AlCerrar de cada funcion encendida, en el hilo de la pantalla y una detras de otra: rapido (sin esperar a nada); pueden leer y
//      cambiar el estado de XP, que todavia no se guardo;
//   3. el ParaAlCerrar de cada una, cada uno en su hilo y todos a la vez, para parar procesos e hilos (rocola-spotify, ffmpeg, el
//      entrenamiento...), sin tocar el estado de XP. Se esperan como mucho PlazoAlCerrarMs entre todos; lo que no acabo sigue en su hilo de
//      fondo mientras XP cierra, y muere con el programa;
//   4. apunta en la bitacora cuanto tardo cada una (lo que hay que parar tiene que caber en los 20 s que dan el lanzador y el buzon, contando
//      el cierre de XP: se MIDE, no se supone).
// El plazo: 4 s. El cierre de XP tarda en esta maquina de 30 a 90 ms, pero con lo pendiente por escribir puede esperar hasta 15 s
// (WaitDataWrites en OnFormClosing, «cierre por otra causa») mas 3 s de la bitacora: Rocola OS se queda con lo que sobra de los 20 s.
// (XP nunca impide su cierre: OnFormClosing no toca Cancel y sus hojas tampoco lo impiden. Si algun dia lo impidiera, lo de Rocola OS
// ya estaria parado y sin latido: habria que volver a arrancarlo aqui.)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Rocola
{
    internal static partial class RocolaOs
    {
        internal const int PlazoAlCerrarMs = 4000;
        private static int cerrandose;
        private static int razonDelCierre = -1;
        // si la rocola ya se esta cerrando (desde que llego el primer aviso)
        internal static bool Cerrandose { get { return Volatile.Read(ref cerrandose) != 0; } }
        // la razon del cierre (None hasta que se cierra)
        internal static RazonCierre RazonDelCierre { get { int r = Volatile.Read(ref razonDelCierre); return r < 0 ? RazonCierre.None : (RazonCierre)r; } }
        private static double cierreMs;
        internal static double CierreMs { get { return cierreMs; } }   // (pruebas) cuanto tardo lo de Rocola OS en cerrarse

        // LA ROCOLA SE CIERRA (el envoltorio del cierre, antes de que XP guarde). En el hilo de la pantalla. Nunca lanza.
        internal static void SeCierra(Form1 r, RazonCierre razon)
        {
            if (!Activo) return;
            if (Interlocked.Exchange(ref cerrandose, 1) != 0) return;
            Volatile.Write(ref razonDelCierre, (int)razon);
            try { SeCierraYa(r, razon); }
            catch (Exception ex) { Falla("andamio", "cierre", ex); }
        }

        private static void SeCierraYa(Form1 r, RazonCierre razon)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            Reloj l = latido;
            if (l != null) l.Enabled = false;   // (desde aqui, ningun latido)
            RocolaOsFuncion[] fs = Encendidas;
            StringBuilder det = new StringBuilder();
            // 1. en la pantalla, una detras de otra
            foreach (RocolaOsFuncion f in fs)
            {
                if (f.AlCerrar == null) continue;
                long t = System.Diagnostics.Stopwatch.GetTimestamp();
                try { f.AlCerrar(r, razon); }
                catch (Exception ex) { Falla(f.Nombre, "AlCerrar", ex); }
                det.Append(det.Length > 0 ? "; " : "").Append(f.Nombre).Append(" (pantalla) ").Append(Ms(t).ToString("0", CultureInfo.InvariantCulture)).Append(" ms");
            }
            // 2. cada una en su hilo, todas a la vez, con el plazo comun
            List<RocolaOsFuncion> conHilo = new List<RocolaOsFuncion>();
            foreach (RocolaOsFuncion f in fs) if (f.ParaAlCerrar != null) conHilo.Add(f);
            if (conHilo.Count > 0)
            {
                Thread[] hs = new Thread[conHilo.Count];
                double[] tardo = new double[conHilo.Count];
                for (int i = 0; i < hs.Length; i++)
                {
                    RocolaOsFuncion f = conHilo[i];
                    int k = i;
                    tardo[k] = -1;
                    hs[i] = new Thread(delegate ()
                    {
                        long t = System.Diagnostics.Stopwatch.GetTimestamp();
                        try { PrioridadHilo.Pon(Thread.CurrentThread, ThreadPriority.Normal); } catch { }   // (la del programa: cerrar no espera)
                        try { f.ParaAlCerrar(razon); }
                        catch (Exception ex) { Falla(f.Nombre, "ParaAlCerrar", ex); }
                        Volatile.Write(ref tardo[k], Ms(t));
                    });
                    hs[i].IsBackground = true;
                    hs[i].Name = "RocolaOs:cierre:" + f.Nombre;
                    hs[i].Start();
                }
                for (int i = 0; i < hs.Length; i++)
                {
                    int falta = PlazoAlCerrarMs - (int)Ms(t0);
                    if (falta < 0) falta = 0;
                    try { hs[i].Join(falta); } catch { }
                }
                for (int i = 0; i < hs.Length; i++)
                {
                    double ms = Volatile.Read(ref tardo[i]);
                    det.Append(det.Length > 0 ? "; " : "").Append(conHilo[i].Nombre).Append(" (su hilo) ")
                       .Append(ms >= 0 ? ms.ToString("0", CultureInfo.InvariantCulture) + " ms" : "NO ACABO en " + PlazoAlCerrarMs.ToString(CultureInfo.InvariantCulture) + " ms (sigue de fondo)");
                }
            }
            cierreMs = Ms(t0);
            Apunta("andamio", "cierre (" + razon + "): " + (det.Length > 0 ? det.ToString() : "nada que parar") + "; en total "
                + cierreMs.ToString("0", CultureInfo.InvariantCulture) + " ms; " + Latidos.ToString(CultureInfo.InvariantCulture) + " latidos, el mas lento de "
                + LatidoMaxMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms");
        }
    }
}
