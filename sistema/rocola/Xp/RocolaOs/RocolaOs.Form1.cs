// LO QUE LO DE ROCOLA OS HACE DENTRO DE LA ROCOLA (Form1) (fase 11, grupo andamio). Una parte de Form1, como Nativo/Form1.Ventana.cs y
// Plataforma/VolcadoEstado.cs: desde aqui se ve lo privado de XP sin tocar Codigo/. Las reglas de estas partes (PLAN_FASE11.md §2.2): todos
// los nombres empiezan por RocolaOs; sin inicializadores de campo con efectos (correrian dentro del constructor de XP); sin constructores
// estaticos que trabajen.
//   - El envoltorio del cierre (gancho g22 de Nativo/Form1.Ventana.cs): sin ROCOLA_OS, el MISMO delegado de XP; con ella, lo de Rocola OS
//     sabe la razon del cierre y para lo suyo (RocolaOs.SeCierra) antes de que XP guarde.
//   - Leer y guardar rocola_os.txt con lo de XP (LeeLineasSeguro, EscribeSeguro y GuardaDetras: Codigo/Form1.Datos.cs).
//   - Su respaldo diario, con las funciones del de XP (BackupTo y PruneBackups) en su propia carpeta (respaldo/rocola-os).
using System;
using System.Globalization;
using System.IO;

namespace Rocola
{
    public partial class Form1
    {
        // EL ENVOLTORIO DEL CIERRE (gancho g22, Nativo/Form1.Ventana.cs, AbreVentana: corre en el constructor de la rocola, asi que solo mira
        // la variable). Sin ROCOLA_OS devuelve lo de XP tal cual: el mismo delegado y el mismo orden de siempre.
        private Action<RazonCierre> RocolaOsEnvuelveElCierre(Action<RazonCierre> xp)
        {
            if (!RocolaOs.Activo) return xp;
            return delegate (RazonCierre razon) { RocolaOs.SeCierra(this, razon); xp(razon); };
        }

        // rocola_os.txt con lo de XP: leer (si el bueno falta o quedo vacio, el .bak), escribir seguro (de paso, .bak y cambio de nombre) y
        // encargar el guardado detras (cuenta para WaitDataWrites: el cierre espera)
        internal static string[] RocolaOsLeeLineas(string ruta) { return LeeLineasSeguro(ruta); }
        internal static void RocolaOsEscribeSeguro(string ruta, string texto) { EscribeSeguro(ruta, texto); }
        internal void RocolaOsGuardaDetras(Action trabajo) { GuardaDetras(trabajo); }

        // el dia del negocio de XP (empieza a las 7 AM: BusinessDay)
        internal static DateTime RocolaOsDiaDelNegocio() { return BusinessDay(DateTime.Now); }

        // EL RESPALDO DE UN ARCHIVO DE ROCOLA OS (de la carpeta de datos) en respaldo/rocola-os/<fecha>, junto al de lo aprendido de XP (que va
        // en ROCOLA_IA_DATOS si esta: BackupBaseDir), y en el segundo destino de XP (respaldo_dir=) si lo hay; las 3 copias mas nuevas. Como
        // BackupLearnedData de XP: el archivo se captura YA (en este hilo: lo de antes de cualquier guardado que venga despues) y la copia
        // comprobada va detras (QueueDataWrite: el cierre la espera). Sin el archivo no hace nada. En el hilo de la pantalla.
        internal void RocolaOsRespalda(string nombre)
        {
            string ruta = RutaDatos(nombre);
            if (!File.Exists(ruta)) return;
            string raiz = Path.Combine(BackupBaseDir(), "rocola-os"), otra = backupDir;
            RespaldoCapturado captura;
            try { captura = CapturaRespaldo(new string[] { ruta }); }
            catch (Exception ex) { RocolaOs.Falla("andamio", "respaldo de " + nombre + " (al capturarlo)", ex); return; }
            QueueDataWrite(delegate (object nada)
            {
                try
                {
                    string sello = DateTime.Now.ToString("yyyy-MM-dd_HHmmssfff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N");
                    RocolaOsRespaldaEn(raiz, sello, captura, nombre);
                    if (!string.IsNullOrEmpty(otra)) RocolaOsRespaldaEn(Path.Combine(otra, "respaldo_rocola", "rocola-os"), sello, captura, nombre);
                }
                finally { captura.Dispose(); }
            });
        }
        private static void RocolaOsRespaldaEn(string raiz, string sello, RespaldoCapturado captura, string nombre)
        {
            try
            {
                BackupToCapturado(Path.Combine(raiz, sello), captura, null);
                PruneBackups(raiz, 3);
            }
            catch (Exception ex) { RocolaOs.Falla("andamio", "respaldo de " + nombre + " en " + raiz, ex); }
        }
    }
}
