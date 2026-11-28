// ROCOLA OS (cadena de audio nueva; CADENA_NUEVA.md §4; CADENA_NUEVA_salida.md): CUAL TARJETA USA LA SALIDA NUEVA, POR SU NOMBRE.
// En XP la tarjeta es «la predeterminada de Windows»; en Rocola OS el lanzador le pasa a rocola-xp ROCOLA_ALSA=$(rocola-tarjeta), con el
// NUMERO de la tarjeta (plughw:N,M). Un aparato USB (el SMSL PO100 del dueno) puede salir con otro numero de un arranque a otro, asi que la
// salida nueva la elige por su NOMBRE de ALSA (el «id» de /proc/asound/cardN/id: «PO100», «Generic»...) y la abre como hw:CARD=id,DEV=n:
//   - la guardada en rocola_tarjeta.txt (la de rocola-tarjeta y rocola-sonido: «plughw:CARD=id,DEV=n», o la de antes, «plughw:N,M», que
//     sigue valiendo: se busca su id con el numero de ahora);
//   - si no hay nada guardado, la AUTOMATICA: la primera USB (que no sea HDMI) y, si no hay, la primera que no sea HDMI (lo de siempre);
//   - si la guardada no esta conectada, se avisa (bitacora y hoja Sonido) y se usa la mejor que haya; el motor mira cada 2 s si volvio (o
//     si aparecio una USB, en la automatica) y se cambia a ella.
// Su control de volumen por hardware, si tiene, se pone UNA vez al 100 % (0 dB) y sin silencio, y no se vuelve a tocar (el volumen del dueno
// es digital: RocolaOs.SalidaNuevaMotor.cs).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Rocola
{
    internal static partial class SalidaNueva
    {
        internal sealed class Eleccion
        {
            internal int Tarjeta = -1, Dispositivo, Numero;
            internal string Id = "", Nombre = "", Porque = "", Aviso = "";
            internal bool Usb, Hdmi;
            internal string Pcm { get { return Id.Length > 0 ? "hw:CARD=" + Id + ",DEV=" + Dispositivo.ToString(CultureInfo.InvariantCulture) : "hw:" + Tarjeta.ToString(CultureInfo.InvariantCulture) + "," + Dispositivo.ToString(CultureInfo.InvariantCulture); } }
            internal string ClaveNombre { get { return Id.Length > 0 ? "plughw:CARD=" + Id + ",DEV=" + Dispositivo.ToString(CultureInfo.InvariantCulture) : "plughw:" + Tarjeta.ToString(CultureInfo.InvariantCulture) + "," + Dispositivo.ToString(CultureInfo.InvariantCulture); } }
        }

        private static readonly Regex reNumero = new Regex(@"^(?:plug)?hw:([0-9]+),([0-9]+)$", RegexOptions.CultureInvariant);
        private static readonly Regex reNombre = new Regex(@"^(?:plug)?hw:CARD=([A-Za-z0-9_\-]+),DEV=([0-9]+)$", RegexOptions.CultureInvariant);
        internal static bool EsClaveDeTarjeta(string s) { return s != null && (reNumero.IsMatch(s) || reNombre.IsMatch(s)); }

        // el id de ALSA de la tarjeta N («» si no se sabe)
        internal static string IdDe(int tarjeta)
        {
            try
            {
                string f = Path.Combine(RocolaOsAsound, "card" + tarjeta.ToString(CultureInfo.InvariantCulture), "id");
                return File.Exists(f) ? File.ReadAllText(f).Trim() : "";
            }
            catch { return ""; }
        }
        private static string RocolaOsAsound
        {
            get { string v = null; try { v = Environment.GetEnvironmentVariable("ROCOLA_ASOUND"); } catch { } return string.IsNullOrEmpty(v) ? "/proc/asound" : v; }
        }
        // EL NUMERO DE AHORA de una clave guardada («plughw:N,M» tal cual; «plughw:CARD=id,DEV=M»: el de la tarjeta con ese id). null si no esta.
        internal static string ANumero(string clave)
        {
            if (clave == null) return null;
            Match m = reNumero.Match(clave);
            if (m.Success) return "plughw:" + m.Groups[1].Value + "," + m.Groups[2].Value;
            m = reNombre.Match(clave);
            if (!m.Success) return null;
            foreach (RocolaOsSalida s in RocolaOs.SalidasDeSonido())
                if (IdDe(s.Tarjeta) == m.Groups[1].Value && s.Dispositivo.ToString(CultureInfo.InvariantCulture) == m.Groups[2].Value) return s.Clave;
            return null;
        }
        // LA CLAVE CON NOMBRE de una salida de la lista («plughw:CARD=id,DEV=M»; sin id, la del numero)
        internal static string ANombre(string claveNumero)
        {
            Match m = reNumero.Match(claveNumero ?? "");
            if (!m.Success) return claveNumero;
            int t = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            string id = IdDe(t);
            return id.Length > 0 && Regex.IsMatch(id, "^[A-Za-z0-9_\\-]+$") ? "plughw:CARD=" + id + ",DEV=" + m.Groups[2].Value : claveNumero;
        }

        // LA TARJETA QUE TOCA AHORA (la guardada, o la automatica con la USB primero). null si no hay ninguna salida.
        internal static Eleccion Elige()
        {
            List<RocolaOsSalida> l = RocolaOs.SalidasDeSonido();
            if (l.Count == 0) return null;
            Func<RocolaOsSalida, Eleccion> de = s => new Eleccion { Tarjeta = s.Tarjeta, Dispositivo = s.Dispositivo, Numero = s.Numero, Id = IdDe(s.Tarjeta), Nombre = s.NombreTarjeta, Usb = s.Usb, Hdmi = s.Hdmi };
            string g = RocolaOs.SalidaGuardadaCruda();
            string aviso = "";
            if (g != null)
            {
                Match m = reNombre.Match(g);
                if (m.Success)
                {
                    foreach (RocolaOsSalida s in l)
                        if (IdDe(s.Tarjeta) == m.Groups[1].Value && s.Dispositivo.ToString(CultureInfo.InvariantCulture) == m.Groups[2].Value)
                        { Eleccion e = de(s); e.Porque = "la guardada (" + m.Groups[1].Value + ")"; return e; }
                    aviso = "La tarjeta guardada «" + m.Groups[1].Value + "» no está conectada";
                }
                else
                {
                    m = reNumero.Match(g);
                    if (m.Success)
                    {
                        foreach (RocolaOsSalida s in l)
                            if (s.Tarjeta.ToString(CultureInfo.InvariantCulture) == m.Groups[1].Value && s.Dispositivo.ToString(CultureInfo.InvariantCulture) == m.Groups[2].Value)
                            { Eleccion e = de(s); e.Porque = "la guardada (" + g + ")"; return e; }
                        aviso = "La salida guardada (" + g + ") no está";
                    }
                }
            }
            Eleccion r = null;
            foreach (RocolaOsSalida s in l) if (s.Usb && !s.Hdmi) { r = de(s); r.Porque = "automática: la USB"; break; }
            if (r == null) foreach (RocolaOsSalida s in l) if (!s.Hdmi) { r = de(s); r.Porque = "automática: la primera que no es HDMI"; break; }
            if (r == null) { r = de(l[0]); r.Porque = "la única que hay (HDMI)"; }
            if (aviso.Length > 0) r.Aviso = aviso + ": se usa " + (r.Nombre.Length > 0 ? r.Nombre : r.Pcm) + " (" + r.Pcm + ").";
            return r;
        }
        // ¿Ya esta la que se quiere (la guardada que faltaba, o la USB en la automatica) y no es la que suena?
        private static bool VolvioLaDeseada()
        {
            Eleccion e = Elige();
            return e != null && e.Aviso.Length == 0 && e.Pcm != PcmNombre;
        }

        // ================================================================== ABRIR
        private static readonly int[] Formatos = { SalidaNuevaPcm.S32_LE, SalidaNuevaPcm.S24_3LE, SalidaNuevaPcm.S16_LE };
        private static string avisoApuntado = "";
        private static readonly HashSet<string> mezcladorPuesto = new HashSet<string>();
        // (28-11-2026, «La cadena vieja borrada», CADENA_NUEVA.md) LA SALIDA SE RESPALDA SOLA: ya no se rinde al camino de XP. Si la elegida no abre
        // por hw: (no acepta 44,1 kHz ni ninguno de los tres formatos, o no esta), se prueba en este orden: la misma por plughw: (ALSA convierte),
        // la del lanzador (ROCOLA_ALSA: el plughw de rocola-tarjeta; en las pruebas, la tarjeta con reloj o «null») y «default»; siempre con este
        // motor, su sinc, su volumen y su proteccion. Lo que abra por un respaldo se dice en la bitacora y en la hoja (Respaldo). Si nada abre, se
        // espera y se vuelve a intentar (como XP con el aparato que falla), sin ningun camino viejo.
        internal static string Respaldo = "";
        private static string respaldoApuntado = "";
        private static bool AbreLaTarjeta(bool primera)
        {
            Eleccion e = null;
            List<KeyValuePair<string, string>> candidatas = new List<KeyValuePair<string, string>>();
            Action<string, string> pon = delegate (string pcmN, string porque)
            {
                if (string.IsNullOrEmpty(pcmN)) return;
                foreach (KeyValuePair<string, string> c in candidatas) if (c.Key == pcmN) return;
                candidatas.Add(new KeyValuePair<string, string>(pcmN, porque));
            };
            if (PcmFijo != null) { pon(PcmFijo, ""); pon("plug:" + PcmFijo, "respaldo: por plug (ALSA convierte)"); }
            else
            {
                e = Elige();
                if (e != null)
                {
                    Aviso = e.Aviso;
                    if (Aviso != avisoApuntado) { avisoApuntado = Aviso; if (Aviso.Length > 0) RocolaOs.Apunta(NombreFuncion, "AVISO: " + Aviso); }
                    pon(e.Pcm, "");
                    pon(e.ClaveNombre, "respaldo: la misma por plughw (ALSA convierte)");
                }
                string alsa = null; try { alsa = Environment.GetEnvironmentVariable("ROCOLA_ALSA"); } catch { }
                pon(alsa, "respaldo: la del lanzador (ROCOLA_ALSA)");
                pon("default", "respaldo: la de fabrica de ALSA (default)");
            }
            int periodo = Math.Max(64, Tasa * PeriodoMs / 1000), bufer = Math.Max(periodo * 3, Tasa * BuferMs / 1000);
            SalidaNuevaPcm p = null;
            string nombre = null, porQue = "", primerMotivo = null;
            for (int k = 0; k < candidatas.Count && p == null; k++)
            {
                int err = 0; string paso = "";
                string n = candidatas[k].Key;
                for (int i = 0; i < (k == 0 ? 15 : 1); i++)
                {
                    p = SalidaNuevaPcm.Abre(n, false, Formatos, Tasa, Canales, periodo, bufer, false, out err, out paso);
                    if (p != null || err != -SalidaNuevaPcm.EBUSY) break;
                    System.Threading.Thread.Sleep(100);   // (otro la suelta: mpv de un video que acaba, speaker-test)
                }
                if (p != null) { nombre = n; porQue = candidatas[k].Value; break; }
                string m = n + ": " + (err == int.MinValue ? "no esta ALSA (libasound.so.2)" : SalidaNuevaPcm.Texto(err)) + " en " + paso;
                if (primerMotivo == null) primerMotivo = (e == null && PcmFijo == null ? "Linux no ve ninguna tarjeta de sonido; " : "") + m;
            }
            if (p == null)
            {
                Motivo = primerMotivo ?? "Linux no ve ninguna tarjeta de sonido";
                if (primera) RocolaOs.Apunta(NombreFuncion, "aun no abre ninguna salida (" + Motivo + "): se vuelve a intentar cada segundo");
                Caida = true;
                return false;
            }
            Respaldo = porQue.Length > 0 ? porQue + " (" + nombre + "; la elegida no abrio: " + primerMotivo + ")" : "";
            if (Respaldo != respaldoApuntado) { respaldoApuntado = Respaldo; if (Respaldo.Length > 0) RocolaOs.Apunta(NombreFuncion, "RESPALDO: " + Respaldo); }
            if (porQue.Length > 0 && e != null && nombre != e.ClaveNombre) e = null;   // (lo del lanzador o default: no se sabe de que tarjeta es)
            pcm = p;
            PcmNombre = nombre; FormatoAbierto = p.Formato; TasaAbierta = p.Tasa; PeriodoAbierto = p.Periodo; BuferAbierto = p.Bufer;
            if (e != null) { TarjetaNumero = e.Tarjeta; Dispositivo = e.Dispositivo; TarjetaId = e.Id; TarjetaNombre = e.Nombre; TarjetaUsb = e.Usb; }
            else { TarjetaNumero = TarjetaFija; Dispositivo = 0; TarjetaId = nombre; TarjetaNombre = nombre; TarjetaUsb = false; }
            cerosQuedan = Tasa / 2;   // 0,5 s de ceros antes de la musica
            if (mezcla.Length < periodo * 2) mezcla = new float[periodo * 2];
            Aperturas++;
            Abierta = true; Caida = false; Motivo = "";
            RocolaOs.Apunta(NombreFuncion, "abierta " + nombre + (e != null ? " (" + e.Nombre + "; " + e.Porque + ")" : "") + ": " + SalidaNuevaPcm.NombreFormato(p.Formato)
                + " (" + SalidaNuevaPcm.BitsUtiles(p.Formato) + " bits) a " + p.Tasa + " Hz, bufer " + p.Bufer + " cuadros (" + (p.Bufer * 1000 / Tasa) + " ms), periodo " + p.Periodo
                + "; apertura " + Aperturas);
            int t = TarjetaNumero; string clave = PcmNombre;
            if (t >= 0) lock (mezcladorPuesto) if (mezcladorPuesto.Add(clave)) RocolaOs.Hilo("salida-nueva-mezclador", delegate { PonMezclador(t, TarjetaUsb); });
            return true;
        }

        // EL CONTROL DE VOLUMEN DE LA TARJETA, UNA VEZ AL 100 % (0 dB) Y SIN SILENCIO (en su hilo; por amixer). En una USB, los de volumen de
        // reproduccion que tenga (no los de captura ni microfono); en las demas, los de salida de siempre (los del lanzador).
        private static readonly string[] SalidasDeSiempre = { "Master", "PCM", "Speaker", "Headphone", "Front", "Line Out", "Surround", "Center", "LFE", "Side" };
        private static void PonMezclador(int tarjeta, bool usb)
        {
            string t = tarjeta.ToString(CultureInfo.InvariantCulture);
            string lista = Amixer("-c " + t + " scontents") ?? "";
            List<string> puestos = new List<string>();
            string nombre = null, idx = "0";
            foreach (string l0 in lista.Split('\n'))
            {
                string l = l0.TrimEnd();
                Match m = Regex.Match(l, @"^Simple mixer control '([^']*)',([0-9]+)");
                if (m.Success) { nombre = m.Groups[1].Value; idx = m.Groups[2].Value; continue; }
                if (nombre == null || !l.TrimStart().StartsWith("Capabilities:", StringComparison.Ordinal)) continue;
                bool volumen = Regex.IsMatch(l, @"\bpvolume\b") || Regex.IsMatch(l, @"\bvolume\b");
                string n = nombre; nombre = null;
                if (!volumen) continue;
                bool vale = usb ? !Regex.IsMatch(n, "Capture|Mic|Input|Boost|Line In|Loopback|Monitor", RegexOptions.IgnoreCase) : Array.IndexOf(SalidasDeSiempre, n) >= 0;
                if (!vale) continue;
                Amixer("-c " + t + " -q sset '" + n.Replace("'", "") + "'," + idx + " 100% unmute");
                puestos.Add(n);
            }
            ControlHw = puestos.Count > 0 ? string.Join(", ", puestos) + " (al 100 %, una vez)" : (lista.Length > 0 ? "ninguno (la tarjeta no tiene control de volumen)" : "no se pudo preguntar (amixer)");
            RocolaOs.Apunta(NombreFuncion, "control de volumen de la tarjeta " + t + ": " + ControlHw);
        }
        internal static string Amixer(string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("/bin/sh", "-c \"amixer " + args.Replace("\"", "") + "\"") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                using (Process p = Process.Start(psi))
                {
                    System.Threading.Tasks.Task<string> lee = p.StandardOutput.ReadToEndAsync();
                    p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(3000)) { try { p.Kill(true); } catch { } return null; }
                    return lee.Wait(1000) ? lee.Result : null;
                }
            }
            catch { return null; }
        }
    }
}
