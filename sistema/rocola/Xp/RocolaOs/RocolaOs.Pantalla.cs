// ROCOLA OS (fase 11): LA PANTALLA DEL MONITOR — resolucion, hercios y colores (peticion del dueno del 15-11-2026, AGENTS.md: «pon para
// configurar elementos mas de GPU, como la resolucion, los hertz del monitor, y los colores del monitor, todas esas cosas quedandose
// guardadas para siempre»; FASE11_pantalla.md). Lo que no es dibujo: lo guardado (rocola_pantalla.txt), ponerlo al arrancar ANTES de pintar
// (gancho g86 en Nativo/Anfitrion.cs), cambiar de modo con la rocola andando (la pantalla se acomoda como en XP a otra resolucion: la
// ventana maximizada toma la medida nueva y XP se reacomoda, LayoutControls), la tabla de gamma y la matriz de color (Consola/Pantalla/Kms.cs,
// IModosPantalla) y la vuelta sola a lo automatico. La hoja de Ctrl+F4 que lo maneja: RocolaOs/RocolaOs.VentanaPantalla.cs.
//
// SEGURIDAD (un modo mal puesto deja la rocola a oscuras):
//   - un cambio de modo desde la hoja se prueba 15 s («¿Se ve bien?») y vuelve solo al anterior si nadie confirma; solo lo confirmado se
//     guarda;
//   - al arrancar con un modo guardado se cuenta el arranque (sin_confirmar=N, escrito ANTES de abrir la pantalla); a los 60 s en pie vuelve
//     a 0. Con 2 arranques seguidos sin llegar a 60 s (el programa se cae con ese modo), el siguiente es lo automatico y el modo queda como
//     modo_malo=;
//   - si el controlador no acepta el modo guardado (drmModeSetCrtc), lo automatico al momento (Kms.FalloAlArrancar);
//   - un modo guardado que este monitor no da: lo automatico (lo guardado se queda, por si vuelve el monitor);
//   - «rocola-pantalla auto» en la consola de mantenimiento borra lo guardado.
// Sin ROCOLA_OS (o con una lista sin «pantalla», o con pantalla=0 en rocola_os.txt) no se lee ni se pone nada: new Kms() como siempre.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Rocola.Pantalla;

namespace Rocola
{
    // LO GUARDADO DE LA PANTALLA (rocola_pantalla.txt: «clave=valor» por renglon, UTF-8 sin marca y LF; lo lee tambien rocola-pantalla)
    internal sealed class RocolaOsAjustesPantalla
    {
        internal const int BrilloMin = -50, BrilloMax = 50, ContrasteMin = 50, ContrasteMax = 150, GammaMin = 50, GammaMax = 200,
            TemperaturaMin = -50, TemperaturaMax = 50, SaturacionMin = 0, SaturacionMax = 200;
        internal string Modo;            // «1920x1080@60» (o «...i»); null: lo automatico
        internal int Brillo, Contraste = 100, Gamma = 100, Temperatura, Saturacion = 100;
        internal int SinConfirmar;       // arranques con el modo guardado que aun no llegaron a 60 s en pie
        internal string ModoMalo;        // el ultimo que no sirvio, y por que (para decirlo en la hoja y en «rocola-pantalla estado»)
        internal readonly List<string> Otras = new List<string>();   // los renglones que no conoce (comentarios, claves de otras versiones), tal cual
        internal bool ColoresDeFabrica { get { return Brillo == 0 && Contraste == 100 && Gamma == 100 && Temperatura == 0 && Saturacion == 100; } }
        internal bool GammaDeFabrica { get { return Brillo == 0 && Contraste == 100 && Gamma == 100 && Temperatura == 0; } }
        internal void ColoresAFabrica() { Brillo = 0; Contraste = 100; Gamma = 100; Temperatura = 0; Saturacion = 100; }
        internal RocolaOsAjustesPantalla Copia()
        {
            RocolaOsAjustesPantalla c = (RocolaOsAjustesPantalla)MemberwiseClone();
            return c;
        }

        internal static RocolaOsAjustesPantalla Lee(string[] lineas)
        {
            RocolaOsAjustesPantalla a = new RocolaOsAjustesPantalla();
            if (lineas == null) return a;
            foreach (string l0 in lineas)
            {
                string l = l0.TrimEnd('\r');
                int k = l.IndexOf('=');
                string c = k > 0 ? l.Substring(0, k).Trim() : null, v = k > 0 ? l.Substring(k + 1).Trim() : null;
                int n;
                bool num = v != null && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
                n = num ? int.Parse(v, NumberStyles.Integer, CultureInfo.InvariantCulture) : 0;
                switch (c)
                {
                    case "modo": a.Modo = v.Length == 0 || v == "auto" ? null : (ClaveValida(v) ? v : null); break;
                    case "brillo": if (num) a.Brillo = Acota(n, BrilloMin, BrilloMax); break;
                    case "contraste": if (num) a.Contraste = Acota(n, ContrasteMin, ContrasteMax); break;
                    case "gamma": if (num) a.Gamma = Acota(n, GammaMin, GammaMax); break;
                    case "temperatura": if (num) a.Temperatura = Acota(n, TemperaturaMin, TemperaturaMax); break;
                    case "saturacion": if (num) a.Saturacion = Acota(n, SaturacionMin, SaturacionMax); break;
                    case "sin_confirmar": if (num) a.SinConfirmar = Math.Max(0, n); break;
                    case "modo_malo": a.ModoMalo = v.Length > 0 ? v : null; break;
                    default: if (l.Trim().Length > 0) a.Otras.Add(l); break;
                }
            }
            return a;
        }
        internal static int Acota(int v, int a, int b) { return v < a ? a : v > b ? b : v; }
        // «1920x1080@60» o «1920x1080@60i»
        internal static bool ClaveValida(string v)
        {
            int x = v.IndexOf('x'), a = v.IndexOf('@');
            if (x <= 0 || a <= x + 1 || a == v.Length - 1) return false;
            string hz = v.Substring(a + 1); if (hz.EndsWith("i")) hz = hz.Substring(0, hz.Length - 1);
            int t;
            return int.TryParse(v.Substring(0, x), NumberStyles.None, CultureInfo.InvariantCulture, out t) && int.TryParse(v.Substring(x + 1, a - x - 1), NumberStyles.None, CultureInfo.InvariantCulture, out t)
                && int.TryParse(hz, NumberStyles.None, CultureInfo.InvariantCulture, out t);
        }
        internal string Texto()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("# La pantalla de Rocola OS (Ctrl+F4 -> Pantalla). «rocola-pantalla auto» en la consola de mantenimiento lo borra.\n");
            foreach (string o in Otras) if (!o.StartsWith("# La pantalla de Rocola OS")) sb.Append(o).Append('\n');
            sb.Append("modo=").Append(Modo ?? "auto").Append('\n');
            if (Brillo != 0) sb.Append("brillo=").Append(Brillo.ToString(CultureInfo.InvariantCulture)).Append('\n');
            if (Contraste != 100) sb.Append("contraste=").Append(Contraste.ToString(CultureInfo.InvariantCulture)).Append('\n');
            if (Gamma != 100) sb.Append("gamma=").Append(Gamma.ToString(CultureInfo.InvariantCulture)).Append('\n');
            if (Temperatura != 0) sb.Append("temperatura=").Append(Temperatura.ToString(CultureInfo.InvariantCulture)).Append('\n');
            if (Saturacion != 100) sb.Append("saturacion=").Append(Saturacion.ToString(CultureInfo.InvariantCulture)).Append('\n');
            if (SinConfirmar > 0) sb.Append("sin_confirmar=").Append(SinConfirmar.ToString(CultureInfo.InvariantCulture)).Append('\n');
            if (ModoMalo != null) sb.Append("modo_malo=").Append(ModoMalo.Replace('\n', ' ')).Append('\n');
            return sb.ToString();
        }
    }

    internal static partial class RocolaOs
    {
        internal const string NombrePantalla = "pantalla", ArchivoPantalla = "rocola_pantalla.txt";
        internal const int PantallaArranquesMax = 2;          // arranques seguidos sin llegar a ConfirmaSegundos con el modo guardado: lo automatico
        internal const int PantallaConfirmaSegundos = 60;
        internal const int PantallaPruebaSegundos = 15;       // la prueba de un modo nuevo («¿Se ve bien?»)

        private static ISuperficie laPantalla;                 // la que abrio g86 (solo si la funcion va)
        private static RocolaOsAjustesPantalla pantalla;       // lo guardado (en el hilo de la pantalla, despues de abrirla)
        private static readonly List<string> pantallaAvisos = new List<string>();   // lo que paso al abrirla (se apunta al arrancar: aun no hay bitacora)
        private static bool pantallaConModoGuardado;           // esta sesion arranco con el modo guardado (y se cuenta)
        private static long pantallaAbiertaMs;
        private static string pantallaAutomatica;              // la clave del modo automatico de este monitor

        // La pantalla que se puede cambiar (null sin la funcion, o fuera de pantalla)
        internal static IModosPantalla ModosDeLaPantalla { get { return laPantalla as IModosPantalla; } }
        internal static RocolaOsAjustesPantalla AjustesDePantalla { get { return pantalla; } }

        // ¿VA LA FUNCION «pantalla»? Antes de pintar (desde el [ModuleInitializer] de Plataforma/PruebasLinux.cs, por g86) aun no esta el
        // registro: se mira lo mismo que decidira Encendida despues (la variable, la lista y «pantalla=» de rocola_os.txt, la ultima; un
        // valor que no sea 0 ni 1 vale el de fabrica, encendida).
        internal static bool PantallaPedidaTemprano()
        {
            int m = Modo;
            if (m == Nada) return false;
            if (m == Lista) return lista.Contains(NombrePantalla);
            string v = null;
            try
            {
                string[] l = Form1.RocolaOsLeeLineas(Datos(ArchivoAjustes));
                if (l != null) foreach (string x in l) { string t = x.Trim(); if (t.StartsWith(NombrePantalla + "=")) v = t.Substring(NombrePantalla.Length + 1).Trim(); }
            }
            catch { }
            return v != "0";
        }

        // GANCHO g86 (Nativo/Anfitrion.cs): la pantalla de KMS con el modo guardado y los colores, o la simulada de las pruebas. Sin la
        // funcion, new Kms() como siempre. Lo que lance new Kms() sale igual que antes (el anfitrion lo atrapa y abre fuera de pantalla).
        internal static ISuperficie AbreLaPantalla()
        {
            if (!PantallaPedidaTemprano()) return new Kms();
            pantallaAbiertaMs = Environment.TickCount64;
            RocolaOsAjustesPantalla a;
            try { a = RocolaOsAjustesPantalla.Lee(Form1.RocolaOsLeeLineas(Datos(ArchivoPantalla))); }
            catch (Exception ex) { a = new RocolaOsAjustesPantalla(); pantallaAvisos.Add("no se pudo leer " + ArchivoPantalla + " (" + ex.Message + "): lo automatico"); }
            pantalla = a;
            bool cambio = false;
            if (a.Modo != null && a.SinConfirmar >= PantallaArranquesMax)
            {
                a.ModoMalo = a.Modo + " (" + a.SinConfirmar + " arranques seguidos sin llegar a " + PantallaConfirmaSegundos + " s en pie)";
                pantallaAvisos.Add("el modo guardado " + a.Modo + " no llego a " + PantallaConfirmaSegundos + " s en pie en " + a.SinConfirmar + " arranques seguidos: vuelve a lo automatico");
                a.Modo = null; a.SinConfirmar = 0; cambio = true;
            }
            string pedido = a.Modo;
            Kms.EligeModo = delegate (ModoPantalla[] modos, int auto)
            {
                pantallaAutomatica = modos[auto].Clave;
                if (pedido == null) return auto;
                int i = BuscaModo(modos, pedido);
                if (i < 0) { pantallaAvisos.Add("el modo guardado " + pedido + " no lo da este monitor: lo automatico (" + modos[auto].Clave + "); lo guardado se queda"); return auto; }
                if (i == auto) return auto;
                // SE CUENTA ANTES DE PONERLO (si el programa se cae con este modo, el arranque queda contado)
                a.SinConfirmar++; pantallaConModoGuardado = true;
                try { GuardaPantallaYa(a); } catch (Exception ex) { pantallaAvisos.Add("no se pudo contar el arranque en " + ArchivoPantalla + ": " + ex.Message); }
                return i;
            };
            ISuperficie s;
            try
            {
                string sim = Environment.GetEnvironmentVariable("ROCOLA_KMS_SIMULADO");
                s = !string.IsNullOrEmpty(sim) ? (ISuperficie)new RocolaOsPantallaSimulada(sim) : new Kms();
            }
            finally { Kms.EligeModo = null; }
            IModosPantalla m = s as IModosPantalla;
            if (m != null && m.FalloAlArrancar != null)
            {
                a.ModoMalo = m.FalloAlArrancar; a.Modo = null; a.SinConfirmar = 0; pantallaConModoGuardado = false; cambio = true;
                pantallaAvisos.Add("el modo guardado no se pudo poner (" + m.FalloAlArrancar + "): lo automatico");
            }
            if (cambio) try { GuardaPantallaYa(a); } catch (Exception ex) { pantallaAvisos.Add("no se pudo guardar " + ArchivoPantalla + ": " + ex.Message); }
            laPantalla = s;
            // (REVISION 20-11-2026: sin monitor al prender -a ciegas- o sin modos del monitor, Kms no pregunta a EligeModo y la clave del
            // automatico quedaba sin saber: la hoja guardaba lo automatico como un modo y el latido, con un fallo al presentar, intentaba
            // «volver a lo automatico» en cada latido. Se toma del modo automatico de la pantalla.)
            if (m != null && pantallaAutomatica == null) { ModoPantalla[] vm0 = m.Modos; int ia = m.Automatico; if (ia >= 0 && ia < vm0.Length) pantallaAutomatica = vm0[ia].Clave; }
            if (m != null)
            {
                pantallaAvisos.Add("pantalla " + m.Actual.Clave + (pantallaConModoGuardado ? " (la guardada; arranque " + a.SinConfirmar + " sin confirmar)" : " (automatica)")
                    + "; controlador " + (m.Controlador ?? "?") + "; gamma " + (m.TamanoGamma > 0 ? m.TamanoGamma + " por " + m.ViaGamma : "no") + "; CTM " + (m.ConCtm ? "si" : "no"));
                // (los modos que da el monitor, para saber desde la bitacora -o desde casa- que se puede elegir; «*» el automatico)
                StringBuilder lm = new StringBuilder();
                ModoPantalla[] vm = m.Modos;
                for (int i = 0; i < vm.Length && i < 64; i++) lm.Append(i > 0 ? " " : "").Append(vm[i].Clave).Append(i == m.Automatico ? "*" : "");
                pantallaAvisos.Add("modos del monitor (" + vm.Length + "): " + lm);
                if (!a.ColoresDeFabrica)
                {
                    string e;
                    if (!AplicaColores(m, a, out e)) pantallaAvisos.Add("los colores guardados no se pudieron poner: " + e);
                    else pantallaAvisos.Add("colores puestos: " + DiceColores(a));
                }
            }
            return s;
        }
        internal static int BuscaModo(ModoPantalla[] modos, string clave)
        {
            for (int i = 0; i < modos.Length; i++) if (modos[i].Clave == clave) return i;
            return -1;
        }
        internal static int IndiceAutomatico { get { IModosPantalla m = ModosDeLaPantalla; return m == null ? -1 : m.Automatico; } }
        internal static string DiceColores(RocolaOsAjustesPantalla a)
        {
            return "brillo " + a.Brillo + ", contraste " + a.Contraste + " %, gamma " + (a.Gamma / 100.0).ToString("0.00", CultureInfo.InvariantCulture)
                + ", temperatura " + a.Temperatura + ", saturacion " + a.Saturacion + " %";
        }

        // ---------- guardar ----------
        // De un tiron, con el EscribeSeguro de XP (el de paso, el .bak y el cambio de nombre). Antes de la rocola (al abrir la pantalla), aqui
        // mismo; con ella, por su GuardaDetras (lo espera el cierre).
        private static void GuardaPantallaYa(RocolaOsAjustesPantalla a) { Form1.RocolaOsEscribeSeguro(Datos(ArchivoPantalla), a.Texto()); }
        internal static void GuardaPantalla()
        {
            RocolaOsAjustesPantalla a = pantalla;
            if (a == null) return;
            string texto = a.Texto(), ruta = Datos(ArchivoPantalla);
            Form1 r = LaRocola;
            if (r != null) r.RocolaOsGuardaDetras(delegate { Form1.RocolaOsEscribeSeguro(ruta, texto); });
            else Form1.RocolaOsEscribeSeguro(ruta, texto);
        }

        // ---------- los colores ----------
        // LA TABLA DE GAMMA (n entradas por canal, de 16 bits; la entrada i es la entrada x = i/(n-1)): primero la gamma (x^(1/g): mas de 1,00
        // aclara los medios), luego el contraste alrededor del gris medio, luego el brillo (+-0,25 en las puntas), y la temperatura por
        // canal (multiplica: nunca pasa del blanco). Lo que se sale, al borde. Aproximada, como el menu de un monitor.
        internal static ushort[][] TablaGamma(int n, int brillo, int contraste, int gamma, int temperatura)
        {
            double g = Math.Max(0.1, gamma / 100.0), c = contraste / 100.0, b = brillo / 200.0;
            double t = temperatura / 50.0;   // -1 (calido) a +1 (frio)
            double mr = t < 0 ? 1.0 : 1.0 - 0.25 * t, mg = 1.0 - 0.10 * Math.Abs(t), mb = t < 0 ? 1.0 + 0.40 * t : 1.0;
            ushort[] r = new ushort[n], gv = new ushort[n], bv = new ushort[n];
            for (int i = 0; i < n; i++)
            {
                double x = n > 1 ? (double)i / (n - 1) : 0.0;
                double y = Math.Pow(x, 1.0 / g);
                y = (y - 0.5) * c + 0.5 + b;
                r[i] = A16(y * mr); gv[i] = A16(y * mg); bv[i] = A16(y * mb);
            }
            return new ushort[][] { r, gv, bv };
        }
        private static ushort A16(double y) { if (y <= 0) return 0; if (y >= 1) return 65535; return (ushort)Math.Round(y * 65535.0); }
        // LA MATRIZ DE LA SATURACION (3x3 por filas, para la CTM): s = 1 la identidad; 0, gris (la luz de cada punto con los pesos de Rec. 709)
        internal static double[] MatrizSaturacion(int saturacion)
        {
            double s = saturacion / 100.0;
            double[] L = { 0.2126, 0.7152, 0.0722 };
            double[] m = new double[9];
            for (int f = 0; f < 3; f++) for (int k = 0; k < 3; k++) m[f * 3 + k] = (1 - s) * L[k] + (f == k ? s : 0);
            return m;
        }
        // PONER LOS COLORES en la tarjeta (en el hilo de la pantalla). De fabrica: los de fabrica del controlador (sin tabla o la del nucleo).
        internal static bool AplicaColores(IModosPantalla m, RocolaOsAjustesPantalla a, out string error)
        {
            error = null;
            if (m == null) { error = "sin pantalla de KMS"; return false; }
            if (a.ColoresDeFabrica) return m.QuitaColores(out error);
            bool ok = true; string e;
            if (a.GammaDeFabrica) { if (!m.QuitaColores(out e)) { ok = false; error = e; } }
            else if (m.TamanoGamma > 0)
            {
                ushort[][] t = TablaGamma(m.TamanoGamma, a.Brillo, a.Contraste, a.Gamma, a.Temperatura);
                if (!m.PonGamma(t[0], t[1], t[2], out e)) { ok = false; error = e; }
            }
            else { ok = false; error = "el controlador no tiene tabla de gamma"; }
            if (m.ConCtm && (a.Saturacion != 100 || !a.GammaDeFabrica))
                if (!m.PonCtm(a.Saturacion == 100 ? null : MatrizSaturacion(a.Saturacion), out e)) { ok = false; error = (error == null ? "" : error + "; ") + e; }
            return ok;
        }

        // ---------- cambiar de modo con la rocola andando (en el hilo de la pantalla) ----------
        // El modo del monitor por KMS y, si se pudo, la rocola a la medida nueva: la ventana maximizada toma la pantalla (como hace Windows con
        // una ventana maximizada al cambiar la resolucion), XP se reacomoda (OnResize: LayoutControls) y las hojas abiertas se centran otra
        // vez. Los colores se vuelven a poner (por si el controlador los pierde al cambiar de modo).
        internal static bool CambiaModoPantalla(int indice, out string error)
        {
            error = null;
            IModosPantalla m = ModosDeLaPantalla;
            if (m == null) { error = "sin pantalla de KMS"; return false; }
            if (!m.CambiaModo(indice, out error)) { Apunta(NombrePantalla, "no se pudo poner el modo " + DiceModo(m, indice) + ": " + error); return false; }
            ModoPantalla a = m.Actual;
            ISuperficie s = laPantalla;
            PantallaRocola.RocolaOsCambioDeMedida(s.Ancho, s.Alto, a.Hz);
            RocolaOsAjustesPantalla p = pantalla;
            if (p != null && !p.ColoresDeFabrica) { string e; if (!AplicaColores(m, p, out e)) Apunta(NombrePantalla, "los colores, al cambiar de modo: " + e); }
            Apunta(NombrePantalla, "modo puesto: " + a.Clave);
            return true;
        }
        private static string DiceModo(IModosPantalla m, int i) { ModoPantalla[] v = m.Modos; return i >= 0 && i < v.Length ? v[i].Clave : "#" + i; }
        // lo confirmado (o lo automatico: null) se guarda para siempre
        internal static void GuardaModoPantalla(string clave)
        {
            RocolaOsAjustesPantalla a = pantalla ?? (pantalla = new RocolaOsAjustesPantalla());
            a.Modo = clave == pantallaAutomatica ? null : clave;
            a.SinConfirmar = 0; pantallaConModoGuardado = false;
            GuardaPantalla();
            Apunta(NombrePantalla, "guardado: " + (a.Modo ?? "lo automatico"));
        }
        internal static string ClaveAutomatica { get { return pantallaAutomatica; } }

        // ---------- la funcion «pantalla» (se registra sola) ----------
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RegistraPantalla()
        {
            Registra(new RocolaOsFuncion
            {
                Nombre = NombrePantalla,
                Que = "la hoja Pantalla de Ctrl+F4: resolucion, hercios y colores del monitor (rocola_pantalla.txt)",
                DeFabrica = true,
                Arranca = f => ArrancaPantalla(),
                Latido = f => LatidoPantalla(),
                Vivo = (f, sb) => VivoPantalla(sb),
                AlCerrar = (f, razon) => CierraPantalla(),
            });
        }
        private static void ArrancaPantalla()
        {
            foreach (string a in pantallaAvisos.ToArray()) Apunta(NombrePantalla, a);
            pantallaAvisos.Clear();
            if (laPantalla == null) Apunta(NombrePantalla, "la pantalla no se abrio por aqui (fuera de pantalla): la hoja no puede cambiar nada");
        }
        private static void LatidoPantalla()
        {
            RocolaOsAjustesPantalla a = pantalla;
            IModosPantalla m = ModosDeLaPantalla;
            if (a == null || m == null) return;
            // 60 S EN PIE CON EL MODO GUARDADO: confirmado (el siguiente arranque empieza a contar de cero)
            if (pantallaConModoGuardado && a.SinConfirmar > 0 && Environment.TickCount64 - pantallaAbiertaMs >= PantallaConfirmaSegundos * 1000L)
            {
                pantallaConModoGuardado = false;
                a.SinConfirmar = 0;
                GuardaPantalla();
                Apunta(NombrePantalla, "el modo guardado " + a.Modo + " lleva " + PantallaConfirmaSegundos + " s en pie: confirmado");
            }
            // EL CONTROLADOR NO ACEPTO EL MODO al presentar (no deberia: CambiaModo y el arranque lo prueban antes): a lo automatico
            string f = m.UltimoFallo;
            if (f != null && m.Actual.Clave != pantallaAutomatica && m.Automatico >= 0)
            {
                string mal = m.Actual.Clave;
                string e;
                if (CambiaModoPantalla(m.Automatico, out e))
                {
                    a.ModoMalo = mal + ": " + f; a.Modo = null; a.SinConfirmar = 0; pantallaConModoGuardado = false;
                    GuardaPantalla();
                    Apunta(NombrePantalla, "el controlador no acepto " + mal + " (" + f + "): lo automatico");
                }
            }
        }
        // AL CERRARSE LA ROCOLA (en el hilo de la pantalla, por el envoltorio del cierre): los colores de fabrica, para que la consola (el
        // lanzador, la consola de mantenimiento) no se quede con los de la rocola; nadie desecha la pantalla al salir (el nucleo devuelve la
        // consola al cerrarse el programa, pero no la tabla de gamma). Un apagon o una caida no corren esto: el siguiente arranque los pone
        // otra vez. El modo se queda (el nucleo pone el de la consola).
        private static void CierraPantalla()
        {
            IModosPantalla m = ModosDeLaPantalla;
            RocolaOsAjustesPantalla a = pantalla;
            if (m == null || a == null || a.ColoresDeFabrica) return;
            string e;
            if (!m.QuitaColores(out e)) Apunta(NombrePantalla, "al cerrar, los colores de fabrica: " + e);
        }
        private static void VivoPantalla(StringBuilder sb)
        {
            IModosPantalla m = ModosDeLaPantalla;
            RocolaOsAjustesPantalla a = pantalla;
            if (m == null || a == null) return;
            sb.Append("pantalla=").Append(m.Actual.Clave).Append(a.Modo == null ? " auto" : " guardada").Append(a.ColoresDeFabrica ? "" : " colores").Append('\n');
        }

        // ---------- la orden de prueba (sin la rocola): las cuentas de los colores y lo guardado ----------
        //   rocola-xp --prueba-pantalla tabla N BRILLO CONTRASTE GAMMA TEMPERATURA   la tabla de gamma (r, g y b, una linea cada una)
        //   rocola-xp --prueba-pantalla ctm SATURACION                                la matriz y su S31.32
        //   rocola-xp --prueba-pantalla lee RUTA                                       lo que entiende de un rocola_pantalla.txt, y como lo escribe
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RegistraOrdenPantalla()
        {
            RocolaOsOrdenes.Si("--prueba-pantalla", delegate (string[] a)
            {
                CultureInfo ci = CultureInfo.InvariantCulture;
                if (a.Length >= 6 && a[0] == "tabla")
                {
                    ushort[][] t = TablaGamma(int.Parse(a[1], ci), int.Parse(a[2], ci), int.Parse(a[3], ci), int.Parse(a[4], ci), int.Parse(a[5], ci));
                    string[] nom = { "r", "g", "b" };
                    for (int c = 0; c < 3; c++)
                    {
                        StringBuilder sb = new StringBuilder(nom[c]);
                        foreach (ushort v in t[c]) sb.Append(' ').Append(v.ToString(ci));
                        Console.WriteLine(sb.ToString());
                    }
                    ushort[] f = Kms.TablaDeFabrica(int.Parse(a[1], ci));
                    StringBuilder sf = new StringBuilder("fabrica");
                    foreach (ushort v in f) sf.Append(' ').Append(v.ToString(ci));
                    Console.WriteLine(sf.ToString());
                    return 0;
                }
                if (a.Length >= 2 && a[0] == "ctm")
                {
                    double[] m = MatrizSaturacion(int.Parse(a[1], ci));
                    StringBuilder sb = new StringBuilder("ctm"), sh = new StringBuilder("s31.32");
                    foreach (double v in m) { sb.Append(' ').Append(v.ToString("0.######", ci)); sh.Append(' ').Append(Kms.S31_32(v).ToString("x16", ci)); }
                    Console.WriteLine(sb.ToString()); Console.WriteLine(sh.ToString());
                    return 0;
                }
                if (a.Length >= 2 && a[0] == "lee")
                {
                    RocolaOsAjustesPantalla p = RocolaOsAjustesPantalla.Lee(Form1.RocolaOsLeeLineas(a[1]));
                    Console.WriteLine("modo=" + (p.Modo ?? "auto") + " " + DiceColores(p) + " sin_confirmar=" + p.SinConfirmar + " modo_malo=" + (p.ModoMalo ?? "-") + " otras=" + p.Otras.Count);
                    Console.Write(p.Texto());
                    return 0;
                }
                Console.Error.WriteLine("uso: --prueba-pantalla tabla N BRILLO CONTRASTE GAMMA TEMPERATURA | ctm SATURACION | lee RUTA");
                return 2;
            });
        }
    }
}
