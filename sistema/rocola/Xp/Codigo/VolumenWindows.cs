// EL VOLUMEN DE WINDOWS AL MAXIMO AL ARRANCAR (decision del dueno del 24-10-2026).
//
// El que hace el corte de la venta baja el volumen de Windows para concentrarse, y muchas veces se le olvida subirlo como estaba:
// la rocola amanecia sonando bajito toda la noche siguiente. Ahora, cada vez que la rocola arranca (una sesion nueva del programa),
// pone el volumen general de Windows al maximo y le quita el silencio. Mientras la rocola esta abierta no lo vuelve a tocar: si
// alguien lo baja a mano, se queda bajo hasta el siguiente arranque. (Por eso se retiro tambien el modo noche: bajar lo decide el.)
//
// QUE VOLUMEN. El general, el del altavoz de la barra de tareas, no el de la salida de la rocola:
//   - Windows XP: el control maestro del mezclador (la linea de los altavoces, con su "Silencio"), en cada tarjeta de sonido. Y si
//     la linea "Onda" (lo que sale de los programas) esta en silencio, se le quita; su nivel no se toca.
//   - Vista y posteriores: el volumen del aparato de salida predeterminado (AudioEndpointVolume) y, por si alguien lo bajo en el
//     mezclador por aplicacion, el de la propia rocola (su sesion de sonido).
// Nunca el volumen de la salida WaveOut: desde Vista es el de la rocola en el mezclador y se queda guardado (SalidaSonido.cs).
//
// COSTE Y SEGURIDAD. Una vez por arranque, en un hilo de fondo (no frena la pantalla ni la musica), todo con try/catch: si una API
// no esta o la tarjeta no tiene ese control, no pasa nada y se dice en la bitacora. Sin interruptor: es lo que el dueno pidio.
//
// LAS ENTRADAS QUE SE OYEN POR LA SALIDA (25-10-2026, la revision del ruido del amplificador). En XP, el mezclador de los altavoces
// tambien deja pasar las entradas analogicas de la tarjeta (microfono, linea, CD, auxiliar, el altavoz del PC): si estan abiertas, su
// ruido de fondo (el del microfono con su refuerzo, sobre todo) sale por el amplificador aunque no haya nada enchufado. De paso, al subir el
// volumen se MIRA cuales estan abiertas (sin silencio y con volumen) y, si hay alguna, se apunta «[Ruido en la salida] ...» en
// rocola_error.txt con como silenciarlas. No se tocan: AGENTS.md solo permite subir el volumen general (decision del 24-10).
using System;
using System.Collections.Generic;

namespace Rocola
{
    public partial class Form1
    {
        // Lo que hizo la ultima vez (para la bitacora, Ctrl+F3 y las pruebas). null = aun no se intento.
        internal static string volumenWindowsHecho;
        // Las entradas que se oyen por los altavoces sin ser de la rocola ("" = ninguna; null = no se miro: Vista o sin mezclador).
        internal static string entradasAbiertas;

        private void SubeVolumenWindowsAlArrancar()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string r;
                try { r = SubeVolumenWindows(); }
                catch (Exception ex) { r = "no se pudo (" + ex.GetType().Name + ")"; }
                volumenWindowsHecho = r;
                Diag("volumen de Windows al arrancar: " + r);
                if (r.StartsWith("no se pudo")) { try { LogError("Volumen de Windows al arrancar: " + r, null); } catch { } }
                string ruido = TextoRuidoEntradas(entradasAbiertas);
                if (ruido != null) { Diag(ruido); ApuntaCajaNegra(ruido); }
            });
        }

        // Pone el volumen general al maximo y sin silencio. Devuelve lo que hizo, en una linea.
        // EN LINUX (traduccion de XP): el volumen general es el de ALSA, el mezclador de cada tarjeta de sonido (amixer, de alsa-utils,
        // como /usr/bin/rocola al arrancar). El «Master» es el control maestro de los altavoces de XP; el «PCM» es la linea «Onda» (lo
        // que sale de los programas): se le quita el silencio y su nivel no se toca. Las entradas analogicas se miran y no se tocan.
        internal static string SubeVolumenWindows()
        {
            return SubeVolumenAlsa();
        }

        private static string SubeVolumenAlsa()
        {
            List<string> hecho = new List<string>(), abiertas = new List<string>();
            bool mirado = false;
            List<int> tarjetas = TarjetasAlsa();
            foreach (int i in tarjetas)
            {
                try
                {
                    string[] controles = Amixer(i, "scontrols").Split('\n');
                    List<string> nombres = new List<string>();
                    foreach (string l in controles)
                    {
                        // «Simple mixer control 'Master',0»: el nombre entre comillas; solo los de indice 0
                        int a = l.IndexOf('\''), b = l.LastIndexOf('\'');
                        if (a >= 0 && b > a && l.Substring(b).StartsWith("',0")) { string n = l.Substring(a + 1, b - a - 1); if (!nombres.Contains(n)) nombres.Add(n); }
                    }
                    if (!nombres.Contains("Master")) continue;
                    string h = SubeLinea(i, "Master", true);
                    if (nombres.Contains("PCM")) { string w = SubeLinea(i, "PCM", false); if (w.Length > 0) h += "; onda: " + w; }
                    foreach (string n in nombres)
                        if (EsEntradaAnalogica(n)) { try { string a = EntradaAbierta(i, n); if (a != null) abiertas.Add((tarjetas.Count > 1 ? "tarjeta " + i + ": " : "") + a); } catch { } }
                    mirado = true;
                    hecho.Add("tarjeta " + i + ": " + (h.Length > 0 ? h : "sin control de volumen"));
                }
                catch (Exception ex) { hecho.Add("tarjeta " + i + ": no se pudo (" + ex.GetType().Name + ")"); }
            }
            if (mirado) entradasAbiertas = string.Join(", ", abiertas.ToArray());
            return hecho.Count > 0 ? string.Join("; ", hecho.ToArray()) : "no se pudo (no hay mezclador de altavoces)";
        }

        // Las tarjetas de sonido (/proc/asound/cards: « 0 [Generic        ]: ...»)
        private static List<int> TarjetasAlsa()
        {
            List<int> l = new List<int>();
            try
            {
                foreach (string s in System.IO.File.ReadAllLines("/proc/asound/cards"))
                {
                    string t = s.TrimStart(); int e = t.IndexOf(' '); int n;
                    if (e > 0 && t.IndexOf('[') > e && int.TryParse(t.Substring(0, e), out n) && !l.Contains(n)) l.Add(n);
                }
            }
            catch { }
            return l;
        }
        // Una orden de amixer en esa tarjeta; lo que contesta («» si no esta amixer o fallo).
        private static string Amixer(int tarjeta, params string[] orden)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo pi = new System.Diagnostics.ProcessStartInfo("amixer");
                pi.ArgumentList.Add("-c"); pi.ArgumentList.Add(tarjeta.ToString(System.Globalization.CultureInfo.InvariantCulture));
                foreach (string o in orden) pi.ArgumentList.Add(o);
                pi.UseShellExecute = false; pi.RedirectStandardOutput = true; pi.RedirectStandardError = true;
                using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(pi))
                {
                    string r = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(5000);
                    return r;
                }
            }
            catch { return ""; }
        }
        // Del estado de un control (sget): el volumen de reproduccion en % (el mayor de sus canales; -1 sin volumen) y si esta en silencio
        // (algun canal «[off]»; false si no tiene interruptor).
        private static void LeeControl(int tarjeta, string nombre, out double pct, out bool mudo, out bool tieneMudo)
        {
            pct = -1; mudo = false; tieneMudo = false;
            foreach (string l in Amixer(tarjeta, "sget", nombre).Split('\n'))
            {
                string t = l.Trim();
                if (!t.Contains(":") || t.StartsWith("Capabilities") || t.StartsWith("Playback channels") || t.StartsWith("Limits") || t.StartsWith("Capture") || t.StartsWith("Simple mixer")) continue;
                if (!t.Contains("Playback") && !t.StartsWith("Mono:")) continue;
                int a = t.IndexOf('['); 
                while (a >= 0)
                {
                    int b = t.IndexOf(']', a); if (b < 0) break;
                    string v = t.Substring(a + 1, b - a - 1);
                    double x;
                    if (v.EndsWith("%") && double.TryParse(v.TrimEnd('%'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x)) { if (x > pct) pct = x; }
                    else if (v == "off") { mudo = true; tieneMudo = true; }
                    else if (v == "on") tieneMudo = true;
                    a = t.IndexOf('[', b);
                }
            }
        }

        // Las entradas analogicas de la tarjeta (las que meten ruido de fondo si se oyen por la salida). La onda y el sintetizador no.
        internal static bool EsEntradaAnalogica(string control)
        {
            switch (control)
            {
                case "Mic": case "Front Mic": case "Rear Mic": case "Internal Mic": case "Line": case "Line In": case "CD": case "Aux":
                case "Video": case "Phone": case "Beep": case "PC Speaker":
                    return true;
            }
            return false;
        }
        // "Mic (75 %)" si la linea se oye (sin silencio y con volumen), o null. Solo LEE: no cambia nada.
        private static string EntradaAbierta(int tarjeta, string control)
        {
            double pct; bool muda, tieneMudo;
            LeeControl(tarjeta, control, out pct, out muda, out tieneMudo);
            if (muda || pct == 0 || (pct < 0 && !tieneMudo)) return null;
            return control + (pct >= 0 ? " (" + pct.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " %)" : "");
        }

        // Quita el silencio del control y, si 'nivel', lo sube al maximo. Devuelve lo que hizo ("" si no habia nada).
        private static string SubeLinea(int tarjeta, string control, bool nivel)
        {
            List<string> h = new List<string>();
            double antes; bool mudo, tieneMudo;
            LeeControl(tarjeta, control, out antes, out mudo, out tieneMudo);
            if (nivel && antes >= 0)
            {
                Amixer(tarjeta, "-q", "sset", control, "100%");
                h.Add(antes.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " % -> 100 %");
            }
            if (mudo) { Amixer(tarjeta, "-q", "sset", control, "unmute"); h.Add("sin silencio"); }
            return string.Join(", ", h.ToArray());
        }

        // La linea para rocola_error.txt (null si no hay ninguna abierta, o no se miro).
        internal static string TextoRuidoEntradas(string abiertas)
        {
            if (string.IsNullOrEmpty(abiertas)) return null;
            return "[Ruido en la salida] Se oyen por los altavoces entradas que la rocola no usa: " + abiertas + ". Su ruido de fondo (el del "
                 + "microfono, sobre todo) sale por el amplificador aunque no haya nada enchufado. Para quitarlo: en la consola de mantenimiento "
                 + "(Ctrl+Alt+F2), alsamixer, y silencio (tecla M) en esas lineas. La rocola no las toca.";
        }

        private static string Pct(float f) { return (f * 100f).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " %"; }
    }
}
