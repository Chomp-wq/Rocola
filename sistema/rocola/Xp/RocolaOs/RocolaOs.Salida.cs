// ROCOLA OS (fase 11): LA SALIDA DE SONIDO — el selector de la hoja «Sonido» de Ctrl+F4 (peticion del dueno del 21-11-2026: «un selector
// para la salida de audio, ya que como recordaras la ultima vez no se escuchaba»; FASE11_salida-audio.md). Lo que no es dibujo: la lista de
// las salidas del equipo, cual se usa, la eleccion guardada y la prueba de 15 s al volver. La hoja: RocolaOs/RocolaOs.VentanaSalida.cs.
//
// LAS SALIDAS, como las ven rocola-sonido y rocola-tarjeta (rootfs_overlay/usr/bin): las de reproduccion de /proc/asound (ROCOLA_ASOUND en
// las pruebas), cardN/pcmMp, en el orden de «aplay -l» (tarjeta y salida de menor a mayor: el mismo numero N de «rocola-sonido usar N»). Una
// es del HDMI de la pantalla con las MISMAS reglas que rocola-tarjeta (el nombre de la tarjeta en cards, su id, su codec, sus ELD o el
// nombre de la salida); la automatica es la que rocola-tarjeta elige sin nada guardado (la primera que no sea HDMI).
//
// ELEGIR UNA (como «rocola-sonido usar N» / «auto»): se escribe /rocola/datos/rocola_tarjeta.txt («plughw:T,S», o se borra para la
// automatica; ROCOLA_TARJETA_TXT en las pruebas), que rocola-tarjeta obedece, y la rocola se reabre por la via que CONSERVA el estado
// (SIGUSR1 a si misma, como los reinicios del lanzador; antes deja $RUN/rocola-xp-reinicio para que el lanzador no lo cuente como caida):
// el lanzador sube el volumen de la tarjeta nueva y la vuelve a abrir con ROCOLA_ALSA=$(rocola-tarjeta). Creditos, cola y lo que suena
// siguen.
// LA SEGURIDAD (como la prueba de un modo de la hoja Pantalla): antes de reabrir se escribe rocola_salida_prueba.txt en los datos (antes=,
// nueva=, arranques=). Al volver, a los 3 s pregunta «¿Se oye bien?» 15 s; con «Si» se borra (queda la nueva); con «No», Escape, Intro sin
// mirar o nadie, se vuelve a escribir la de antes y se reabre otra vez. El archivo esta en el disco: un apagon durante la prueba la vuelve
// a preguntar al prender (y sin respuesta, vuelve). Si la rocola se cae con la salida nueva (3 arranques sin contestar), vuelve a la de
// antes sin preguntar.
// Sin ROCOLA_OS no corre nada de esto (Ctrl+F4 ya es de Rocola OS).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Rocola
{
    // UNA SALIDA DE SONIDO del equipo
    internal sealed class RocolaOsSalida
    {
        internal int Numero;        // 1, 2...: el de «rocola-sonido usar N»
        internal int Tarjeta, Dispositivo;
        internal string NombreTarjeta = "", Nombre = "";
        internal bool Hdmi, Usb;
        internal string Clave { get { return "plughw:" + Tarjeta.ToString(CultureInfo.InvariantCulture) + "," + Dispositivo.ToString(CultureInfo.InvariantCulture); } }
        internal string Tipo { get { return Hdmi ? "HDMI (pantalla)" : Usb ? "USB" : "Placa"; } }
    }

    internal static partial class RocolaOs
    {
        internal const string NombreSalida = "salida_audio", ArchivoSalidaPrueba = "rocola_salida_prueba.txt", SalidaAuto = "auto";
        internal const int SalidaPruebaSegundos = 15, SalidaPreguntaTrasMs = 3000, SalidaArranquesMax = 3;

        private static string Asound
        {
            get { string v = null; try { v = Environment.GetEnvironmentVariable("ROCOLA_ASOUND"); } catch { } return string.IsNullOrEmpty(v) ? "/proc/asound" : v; }
        }
        // rocola_tarjeta.txt (la de rocola-tarjeta y rocola-sonido: ${ROCOLA_DATOS:-/rocola/datos}/rocola_tarjeta.txt del sistema, que no es la
        // carpeta de datos de rocola-xp)
        internal static string ArchivoTarjetaFija
        {
            get { string v = null; try { v = Environment.GetEnvironmentVariable("ROCOLA_TARJETA_TXT"); } catch { } return string.IsNullOrEmpty(v) ? "/rocola/datos/rocola_tarjeta.txt" : v; }
        }

        private static string Lee(string ruta) { try { return File.Exists(ruta) ? File.ReadAllText(ruta) : null; } catch { return null; } }
        private static readonly Regex reHdmi = new Regex("HDMI|DisplayPort", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex reFija = new Regex(@"^plughw:([0-9]+),([0-9]+)$", RegexOptions.CultureInvariant);
        private static int NumeroDe(string nombre, string prefijo, string sufijo)
        {
            if (!nombre.StartsWith(prefijo, StringComparison.Ordinal) || !nombre.EndsWith(sufijo, StringComparison.Ordinal)) return -1;
            string m = nombre.Substring(prefijo.Length, nombre.Length - prefijo.Length - sufijo.Length);
            int n; return m.Length > 0 && int.TryParse(m, NumberStyles.None, CultureInfo.InvariantCulture, out n) ? n : -1;
        }

        // LAS SALIDAS DEL EQUIPO (de /proc/asound, como aplay -l), con lo que dice cada una. Nunca lanza.
        internal static List<RocolaOsSalida> SalidasDeSonido()
        {
            List<RocolaOsSalida> l = new List<RocolaOsSalida>();
            try
            {
                string R = Asound;
                if (!Directory.Exists(R)) return l;   // (un equipo sin sonido -o fuera de pantalla, sin /proc/asound-: ninguna, sin fallo)
                string[] cards = (Lee(Path.Combine(R, "cards")) ?? "").Split('\n');
                List<int> ts = new List<int>();
                foreach (string d in Directory.GetDirectories(R)) { int n = NumeroDe(Path.GetFileName(d), "card", ""); if (n >= 0) ts.Add(n); }
                ts.Sort();
                foreach (int t in ts)
                {
                    string c = Path.Combine(R, "card" + t.ToString(CultureInfo.InvariantCulture));
                    // el renglon de la tarjeta en cards: « 0 [HDMI           ]: HDA-Intel - HDA ATI HDMI»
                    string linea = "";
                    foreach (string x in cards)
                    {
                        string y = x.TrimStart();
                        if (y.StartsWith(t.ToString(CultureInfo.InvariantCulture) + " [", StringComparison.Ordinal)) { linea = y; break; }
                    }
                    string nombreT = linea; int k = linea.IndexOf("]: ", StringComparison.Ordinal);
                    if (k >= 0) { nombreT = linea.Substring(k + 3); int g = nombreT.IndexOf(" - ", StringComparison.Ordinal); if (g >= 0) nombreT = nombreT.Substring(g + 3); }
                    string id = (Lee(Path.Combine(c, "id")) ?? "").Trim();
                    if (nombreT.Trim().Length == 0) nombreT = id;
                    // LA TARJETA DEL HDMI, ENTERA (las reglas de rocola-tarjeta)
                    bool hdmiT = (linea.Length > 0 && linea.IndexOf('[') >= 0 && reHdmi.IsMatch(linea.Substring(linea.IndexOf('[')))) || reHdmi.IsMatch(id);
                    bool eld = false;
                    try
                    {
                        List<string> codecs = new List<string>(Directory.GetFiles(c, "codec#*")); codecs.Sort(StringComparer.Ordinal);
                        StringBuilder cab = new StringBuilder();
                        foreach (string f in codecs) { string s = Lee(f); if (s != null) cab.Append(s); }
                        string[] dos = cab.ToString().Split('\n');
                        for (int i = 0; i < dos.Length && i < 2; i++) if (reHdmi.IsMatch(dos[i])) hdmiT = true;
                        eld = Directory.GetFiles(c, "eld#*").Length > 0;
                    }
                    catch { }
                    if (eld) hdmiT = true;
                    bool usb = linea.IndexOf("USB", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("USB", StringComparison.OrdinalIgnoreCase) >= 0;
                    List<int> ps = new List<int>();
                    try { foreach (string d in Directory.GetDirectories(c)) { int n = NumeroDe(Path.GetFileName(d), "pcm", "p"); if (n >= 0) ps.Add(n); } } catch { }
                    ps.Sort();
                    foreach (int p in ps)
                    {
                        string info = Lee(Path.Combine(c, "pcm" + p.ToString(CultureInfo.InvariantCulture) + "p", "info")) ?? "";
                        string nombre = "";
                        foreach (string x in info.Split('\n')) if (x.StartsWith("name: ", StringComparison.Ordinal)) { nombre = x.Substring(6).Trim(); break; }
                        if (nombre.Length == 0) foreach (string x in info.Split('\n')) if (x.StartsWith("id: ", StringComparison.Ordinal)) { nombre = x.Substring(4).Trim(); break; }
                        l.Add(new RocolaOsSalida
                        {
                            Numero = l.Count + 1, Tarjeta = t, Dispositivo = p, NombreTarjeta = nombreT.Trim(), Nombre = nombre,
                            Hdmi = hdmiT || reHdmi.IsMatch(info), Usb = usb && !hdmiT,
                        });
                    }
                }
            }
            catch (Exception ex) { Falla(NombreSalida, "leer las salidas de sonido", ex); }
            return l;
        }
        // LA AUTOMATICA (rocola-tarjeta sin nada guardado): la primera que no sea HDMI; null si no hay
        internal static RocolaOsSalida SalidaAutomatica(List<RocolaOsSalida> l)
        {
            foreach (RocolaOsSalida s in l) if (!s.Hdmi) return s;
            return null;
        }
        // LO GUARDADO en rocola_tarjeta.txt: «plughw:T,S» o null (la automatica: no hay archivo, o no dice una salida)
        internal static string SalidaGuardada()
        {
            string t = Lee(ArchivoTarjetaFija);
            if (t == null) return null;
            foreach (string x in t.Split('\n')) { string y = x.TrimEnd('\r'); if (reFija.IsMatch(y)) return y; }
            return null;
        }
        // LA QUE USA LA ROCOLA AHORA: ROCOLA_ALSA si dice una salida (la pone el lanzador con rocola-tarjeta); si no, la que rocola-tarjeta
        // elegiria (lo guardado si esa salida existe, si no la automatica)
        internal static string SalidaEnUso(List<RocolaOsSalida> l)
        {
            string e = null; try { e = Environment.GetEnvironmentVariable("ROCOLA_ALSA"); } catch { }
            if (!string.IsNullOrEmpty(e) && reFija.IsMatch(e)) return e;
            return SalidaQueTocaria(l);
        }
        internal static string SalidaQueTocaria(List<RocolaOsSalida> l)
        {
            string g = SalidaGuardada();
            if (g != null) foreach (RocolaOsSalida s in l) if (s.Clave == g) return g;
            RocolaOsSalida a = SalidaAutomatica(l);
            return a != null ? a.Clave : "default";
        }

        // ESCRIBE rocola_tarjeta.txt (null o «auto»: lo borra) por un temporal, con fsync, como «echo ... > ; sync» de rocola-sonido
        internal static bool GuardaSalida(string clave, out string err)
        {
            err = null;
            string f = ArchivoTarjetaFija;
            try
            {
                if (clave == null || clave == SalidaAuto) { if (File.Exists(f)) File.Delete(f); }
                else EscribeConSync(f, clave + "\n");
                AnotaEnSonido(clave == null || clave == SalidaAuto ? "se eligio la salida automatica en Ctrl+F4" : "se eligio a mano la salida " + clave + " en Ctrl+F4");
                return true;
            }
            catch (Exception ex) { err = ex.Message; Falla(NombreSalida, "guardar " + f, ex); return false; }
        }
        private static void EscribeConSync(string ruta, string texto)
        {
            string d = Path.GetDirectoryName(ruta);
            if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
            string tmp = ruta + ".tmp";
            using (FileStream s = new FileStream(tmp, FileMode.Create, FileAccess.Write))
            {
                byte[] b = new UTF8Encoding(false).GetBytes(texto);
                s.Write(b, 0, b.Length);
                s.Flush(true);
            }
            File.Move(tmp, ruta, true);
        }
        // un renglon en rocola_sonido.txt (junto a rocola_tarjeta.txt), como «rocola-sonido usar»
        private static void AnotaEnSonido(string que)
        {
            try
            {
                string d = Path.GetDirectoryName(ArchivoTarjetaFija);
                if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) return;
                File.AppendAllText(Path.Combine(d, "rocola_sonido.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "  " + que + "\n");
            }
            catch { }
        }

        // ---------- la prueba (rocola_salida_prueba.txt) ----------
        internal sealed class PruebaSalida { internal string Antes, Nueva; internal int Arranques; }
        internal static PruebaSalida LeePruebaSalida()
        {
            string t = Lee(Datos(ArchivoSalidaPrueba));
            if (t == null) return null;
            PruebaSalida p = new PruebaSalida();
            foreach (string x in t.Split('\n'))
            {
                string y = x.TrimEnd('\r'); int k = y.IndexOf('=');
                if (k <= 0) continue;
                string c = y.Substring(0, k), v = y.Substring(k + 1).Trim();
                if (c == "antes") p.Antes = v; else if (c == "nueva") p.Nueva = v;
                else if (c == "arranques") int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out p.Arranques);
            }
            if (p.Antes == null || (p.Antes != SalidaAuto && !reFija.IsMatch(p.Antes))) p.Antes = SalidaAuto;
            if (p.Nueva == null) p.Nueva = SalidaAuto;
            return p;
        }
        internal static void GuardaPruebaSalida(PruebaSalida p)
        {
            EscribeConSync(Datos(ArchivoSalidaPrueba), "# la prueba de la salida de sonido de Ctrl+F4: si nadie confirma, vuelve a «antes»\nantes=" + p.Antes + "\nnueva=" + p.Nueva
                + "\narranques=" + p.Arranques.ToString(CultureInfo.InvariantCulture) + "\n");
        }
        internal static void BorraPruebaSalida() { try { File.Delete(Datos(ArchivoSalidaPrueba)); } catch { } }

        // ELEGIR UNA SALIDA (clave «plughw:T,S» o «auto») DESDE LA HOJA: la prueba, lo guardado y a reabrir. Devuelve false (con el porque) si no
        // pudo escribir; entonces no cambia nada.
        internal static bool EligeSalida(string clave, out string err)
        {
            err = null;
            string antes = SalidaGuardada() ?? SalidaAuto;
            try { GuardaPruebaSalida(new PruebaSalida { Antes = antes, Nueva = clave, Arranques = 0 }); }
            catch (Exception ex) { err = ex.Message; Falla(NombreSalida, "guardar la prueba", ex); return false; }
            if (!GuardaSalida(clave, out err)) { BorraPruebaSalida(); return false; }
            Apunta(NombreSalida, "elegida " + clave + " (antes " + antes + "): se reabre la rocola conservando el estado, y al volver «¿Se oye bien?» " + SalidaPruebaSegundos + " s");
            return true;
        }

        // REABRIR LA ROCOLA CONSERVANDO EL ESTADO: la marca del lanzador y SIGUSR1 a si misma (lo mismo que hace el lanzador: el cierre en
        // orden, TaskManagerClosing, que no borra rocola_estado.txt)
        [DllImport("libc", EntryPoint = "kill", SetLastError = true)] private static extern int MataSalida(int pid, int senal);
        internal static void ReabreConservando(string porque)
        {
            try { File.WriteAllText(Run("rocola-xp-reinicio"), porque + "\n"); } catch (Exception ex) { Falla(NombreSalida, "la marca del reinicio", ex); }
            Apunta(NombreSalida, "se reabre la rocola (" + porque + ")");
            try { MataSalida(Environment.ProcessId, 10); } catch (Exception ex) { Falla(NombreSalida, "SIGUSR1", ex); }
        }

        // «PROBAR»: un pitido de 3 s por una salida SIN cambiarla, solo de OTRA tarjeta que la que usa la rocola (la misma no se puede abrir
        // dos veces sin pelear con lo que suena). Como rocola-sonido: le quita el silencio y suena speaker-test. En su hilo. ROCOLA_TONO
        // (las pruebas) cambia la orden por otra con la salida y la tarjeta.
        private static int probando;
        internal static bool Probando { get { return Volatile.Read(ref probando) != 0; } }
        internal static bool ProbarSalida(RocolaOsSalida s)
        {
            if (s == null || Interlocked.Exchange(ref probando, 1) != 0) return false;
            string t = s.Tarjeta.ToString(CultureInfo.InvariantCulture), clave = s.Clave;
            Thread h = new Thread(delegate ()
            {
                try
                {
                    string otra = null; try { otra = Environment.GetEnvironmentVariable("ROCOLA_TONO"); } catch { }
                    System.Diagnostics.ProcessStartInfo psi;
                    if (!string.IsNullOrEmpty(otra)) psi = new System.Diagnostics.ProcessStartInfo(otra);
                    else
                    {
                        psi = new System.Diagnostics.ProcessStartInfo("/bin/sh");
                        psi.ArgumentList.Add("-c");
                        psi.ArgumentList.Add("for c in Master PCM Speaker Headphone Front 'Line Out'; do amixer -c \"$2\" -q sset \"$c\" 100% unmute; done 2>/dev/null;"
                            + " i=0; while [ $i -lt 8 ]; do amixer -c \"$2\" -q sset \"IEC958,$i\" unmute 2>/dev/null; i=$((i + 1)); done;"
                            + " timeout 3 speaker-test -D \"$1\" -t sine -f 440 -c 2 >/dev/null 2>&1; exit 0");
                        psi.ArgumentList.Add("rocola-tono");
                    }
                    psi.ArgumentList.Add(clave); psi.ArgumentList.Add(t);
                    psi.UseShellExecute = false;
                    using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
                        if (p != null && !p.WaitForExit(8000)) { try { p.Kill(true); } catch { } }
                    Apunta(NombreSalida, "probada " + clave + " (un pitido de 3 s)");
                }
                catch (Exception ex) { Falla(NombreSalida, "probar " + clave, ex); }
                finally { Volatile.Write(ref probando, 0); }
            });
            h.IsBackground = true; h.Name = "RocolaOs:salida-tono";
            h.Start();
            return true;
        }

        // ---------- la funcion ----------
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RegistraSalida()
        {
            Registra(new RocolaOsFuncion
            {
                Nombre = NombreSalida,
                Que = "la hoja Sonido de Ctrl+F4: elegir la salida de sonido (rocola_tarjeta.txt) con la prueba de 15 s al volver",
                DeFabrica = true,
                Arranca = f => ArrancaSalida(),
                Latido = f => f.RocolaOsLatidoSalida(),
            });
        }
        private static long salidaArrancoMs;
        internal static bool SalidaReabrirYa;   // (3 arranques sin contestar: ya se volvio a la de antes; falta reabrir)
        private static void ArrancaSalida()
        {
            salidaArrancoMs = Environment.TickCount64;
            List<RocolaOsSalida> l = SalidasDeSonido();
            StringBuilder sb = new StringBuilder();
            foreach (RocolaOsSalida s in l) sb.Append(sb.Length > 0 ? ", " : "").Append(s.Numero).Append(' ').Append(s.Clave).Append(s.Hdmi ? " HDMI" : "");
            Apunta(NombreSalida, "salidas (" + l.Count + "): " + (sb.Length > 0 ? sb.ToString() : "ninguna") + "; en uso " + SalidaEnUso(l) + (SalidaGuardada() != null ? " (elegida a mano)" : " (automatica)"));
            PruebaSalida p = LeePruebaSalida();
            if (p == null) return;
            p.Arranques++;
            if (p.Arranques >= SalidaArranquesMax)
            {
                string e;
                GuardaSalida(p.Antes, out e);
                BorraPruebaSalida();
                SalidaReabrirYa = true;
                Apunta(NombreSalida, "la prueba de " + p.Nueva + " lleva " + p.Arranques + " arranques sin contestar: vuelve a " + p.Antes);
                return;
            }
            try { GuardaPruebaSalida(p); } catch (Exception ex) { Falla(NombreSalida, "contar el arranque", ex); }
            Apunta(NombreSalida, "prueba de " + p.Nueva + " (arranque " + p.Arranques + "): a los " + (SalidaPreguntaTrasMs / 1000) + " s «¿Se oye bien?»");
        }
        internal static long SalidaArrancoMs { get { return salidaArrancoMs; } }
    }
}
