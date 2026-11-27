// LOS BOTONES DE VOLUMEN EN EL ASISTENTE DE BOTONES (27-11-2026, el dueno: «Sobre los botones de volumen, no funciona, mejor haz que al
// pedir las teclas [...] pida los inputs de subir y bajar volumen»). El asistente de XP (F8, o la primera vez) pide los 8 botones de la
// rocola; con Rocola OS pide ademas SUBIR VOLUMEN y BAJAR VOLUMEN (pasos 9 y 10). Si la rocola no tiene ese boton, REGRESAR (el que se
// acaba de configurar) lo salta. Se guardan aparte, en rocola_teclas_volumen.txt (subir=, bajar=, con el codigo de tecla de Windows como
// rocola_teclas.txt; el de XP no cambia y la rocola de XP lo lee igual). Al apretar uno, el volumen general de la tarjeta de la rocola
// sube o baja un 2 % (Plataforma/VolumenTeclas.cs, por amixer) y apretado se repite unas 10 veces por segundo (con la repeticion de
// siempre de las teclas). Las teclas multimedia de volumen (VK_VOLUME_UP/DOWN/MUTE, KEY_VOLUMEUP/DOWN/MUTE de Linux) valen siempre,
// aunque no se hayan configurado; si un boton de volumen guardado es uno de los 8 de XP, manda el de XP. Ganchos g420-g424 (Codigo/Form1.Ajustes.cs, el asistente; Codigo/Form1.cs, la tecla). Sin Rocola OS,
// XP tal cual (8 pasos).
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
        private static readonly string[] RocolaOsPasosVolumen = new string[] {
            "SUBIR VOLUMEN   (si la rocola no tiene ese boton, presiona REGRESAR)",
            "BAJAR VOLUMEN   (si la rocola no tiene ese boton, presiona REGRESAR)",
        };
        private string[] rocolaOsPasos;
        private Tecla rocolaOsVolSube = Tecla.None, rocolaOsVolBaja = Tecla.None;
        private Tecla rocolaOsCapSube = Tecla.None, rocolaOsCapBaja = Tecla.None;
        private bool rocolaOsVolLeido;
        private int rocolaOsVolUltimoMs;
        private const int VkVolumenMudo = 0xAD, VkVolumenBaja = 0xAE, VkVolumenSube = 0xAF;

        // g420/g421/g422: los pasos del asistente (los 8 de XP y, con Rocola OS, los dos del volumen)
        private string[] RocolaOsPasos()
        {
            if (!RocolaOs.Activo) return ConfigSteps;
            if (rocolaOsPasos == null)
            {
                string[] p = new string[ConfigSteps.Length + RocolaOsPasosVolumen.Length];
                ConfigSteps.CopyTo(p, 0); RocolaOsPasosVolumen.CopyTo(p, ConfigSteps.Length);
                rocolaOsPasos = p;
            }
            return rocolaOsPasos;
        }

        // g423 (CaptureConfigKey, antes de mirar si el boton ya esta usado): los pasos del volumen (9 y 10). true = lo atendio.
        private bool RocolaOsCapturaVolumen(Tecla k)
        {
            if (!RocolaOs.Activo || configStep < ConfigSteps.Length) return false;
            int paso = configStep - ConfigSteps.Length;   // 0 subir, 1 bajar
            if (k == configCap[5]) k = Tecla.None;   // REGRESAR: «no tengo ese boton»
            else
            {
                for (int j = 0; j < configCap.Length; j++)
                    if (configCap[j] == k) { UpdateConfigPrompt("Ese boton ya esta usado. Presiona OTRO (o REGRESAR si no hay)."); return true; }
                if (paso == 1 && k == rocolaOsCapSube) { UpdateConfigPrompt("Ese boton ya esta usado. Presiona OTRO (o REGRESAR si no hay)."); return true; }
            }
            if (paso == 0) rocolaOsCapSube = k; else rocolaOsCapBaja = k;
            configStep++;
            if (configStep >= RocolaOsPasos().Length)
            {
                rocolaOsVolSube = rocolaOsCapSube; rocolaOsVolBaja = rocolaOsCapBaja; rocolaOsVolLeido = true;
                RocolaOsGuardaVolumen();
                FinishConfig();
            }
            else UpdateConfigPrompt(null);
            return true;
        }

        private string RocolaOsRutaVolumen() { return RutaDatos("rocola_teclas_volumen.txt"); }

        private void RocolaOsGuardaVolumen()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("subir=").Append(((int)rocolaOsVolSube).ToString(CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("bajar=").Append(((int)rocolaOsVolBaja).ToString(CultureInfo.InvariantCulture)).Append("\r\n");
                EscribeSeguro(RocolaOsRutaVolumen(), sb.ToString());
                RocolaOs.Apunta("volumen", "botones de volumen configurados: subir=" + (int)rocolaOsVolSube + " bajar=" + (int)rocolaOsVolBaja);
            }
            catch { }
        }

        private void RocolaOsLeeVolumen()
        {
            rocolaOsVolLeido = true;
            try
            {
                string f = RocolaOsRutaVolumen();
                if (!File.Exists(f)) return;
                foreach (string l in File.ReadAllLines(f))
                {
                    int eq = l.IndexOf('='); int v;
                    if (eq <= 0 || !int.TryParse(l.Substring(eq + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) || v < 0 || v > 0xFF) continue;
                    string n = l.Substring(0, eq).Trim().ToLowerInvariant();
                    if (n == "subir") rocolaOsVolSube = (Tecla)v; else if (n == "bajar") rocolaOsVolBaja = (Tecla)v;
                }
            }
            catch { }
        }

        // g424 (la tecla, ANTES de reiniciar la inactividad: despues del arranque en espera y de Ctrl+Alt+Q, y antes de lastInput): un boton de volumen
        // sube o baja el volumen y no hace nada mas (no despierta la atraccion, no reinicia la inactividad ni cuenta como otro boton). true = lo atendio.
        private bool RocolaOsTeclaVolumen(Tecla keyData, PulsoTecla msg)
        {
            if (!RocolaOs.Activo || configMode) return false;
            if (!rocolaOsVolLeido) RocolaOsLeeVolumen();
            Tecla k = keyData & Tecla.KeyCode;
            if (k == Tecla.None) return false;
            // (revision 27-11-2026) si un boton de volumen guardado coincide con uno de los 8 de XP (pasa si se vuelve a traer rocola_teclas.txt de XP:
            // el de volumen se guardo antes), gana el de XP; las teclas multimedia (0xAD-0xAF) valen siempre
            bool deXp = k == keyUp || k == keyDown || k == keyLeft || k == keyRight || k == keyOK || k == keyBack || k == keyCoin || k == keySkip;
            int dir = 0; bool mudo = false;
            if ((!deXp && k == rocolaOsVolSube) || (int)k == VkVolumenSube) dir = 1;
            else if ((!deXp && k == rocolaOsVolBaja) || (int)k == VkVolumenBaja) dir = -1;
            else if ((int)k == VkVolumenMudo) mudo = true;
            else return false;
            int ahora = Environment.TickCount;
            if (msg.Repetida && unchecked(ahora - rocolaOsVolUltimoMs) < 100) return true;   // apretado: unas 10 veces por segundo
            rocolaOsVolUltimoMs = ahora;
            if (mudo) { if (!msg.Repetida) VolumenTeclas.Silencio(); }
            else VolumenTeclas.Paso(dir);
            return true;
        }
    }
}
