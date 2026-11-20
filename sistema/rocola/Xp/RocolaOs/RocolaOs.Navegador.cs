// ROCOLA OS (fase 11): EL NAVEGADOR WEB DE LA VENTANA DE CTRL+F4 (peticion del dueno del 20-11-2026: «en la ventana de ctrl f4, debes poner un
// navegador por si quiero desarrollar en la misma rocola entrando a Claude»; FASE11_navegador-archivos.md). Solo con ROCOLA_OS.
//
// QUE ES: WPE WebKit con cog (paquetes de Buildroot; cog por su plataforma DRM: pinta directo en la tarjeta por KMS y lee el teclado y el
// raton por libinput, que necesita udev: Rocola OS ya trae eudev desde la etapa 1 -BR2_ROOTFS_DEVICE_CREATION_DYNAMIC_EUDEV, S10udevd-, asi
// que no hace falta ni Wayland ni un compositor ni cambiar el arranque). Lo abre el guion /usr/bin/rocola-navegador («abre»), que pone sus
// datos en /rocola/datos/xp/navegador (con tope) y lo cierra con su arranque.
//
// EL TRASPASO DE LA PANTALLA (rocola-xp sigue abierta y SONANDO todo el tiempo):
//   1. La ventana de la rocola cuenta como minimizada (gancho g100: XP deja de pintar en la tarjeta y de preparar portadas, como en Windows con
//      la ventana minimizada), la composicion de la pantalla no hace nada (PantallaRocola.Componer, el delegado que pone el anfitrion: se
//      guarda y se devuelve) y la pantalla de KMS se suelta (ISuperficie.Suelta: drmDropMaster; la de las pruebas no hace nada). La guarda
//      el anfitrion (gancho g101).
//   2. Se abre rocola-navegador («abre»): cog se queda con la tarjeta (el primero que la abre sin amo es su amo) y con el teclado y el raton.
//   3. Mientras tanto, la rocola sigue leyendo las teclas (evdev, sin quitarselas a nadie) en una hoja suya que no se ve («Navegador
//      abierto»), que solo atiende dos combinaciones: CTRL+ALT+F1 cierra el navegador y vuelve a la rocola; CTRL+ALT+INICIO vuelve a abrirlo
//      en su pagina de inicio (para escribir otra direccion: cog no tiene barra). Ningun boton de la rocola puede dar esas combinaciones (son
//      tres teclas a la vez, con F1 o Inicio: las placas de botones dan flechas, Intro, Retroceso, numeros, letras, y a lo mas Ctrl o Alt
//      sueltos). Las monedas NO cuentan con el navegador abierto: el monedero y el teclado son la misma tecla para la rocola y cada «5» que se
//      escribiera daria un credito.
//   4. Al cerrarse cog (la combinacion, el propio navegador o un fallo): la pantalla se retoma (ISuperficie.Recupera: drmSetMaster, y el
//      siguiente cuadro vuelve a poner el modo de la rocola), los colores de la hoja Pantalla se vuelven a poner, la composicion vuelve y la
//      ventana deja de estar minimizada.
// Si la rocola se cierra con el navegador abierto (la consola de mantenimiento, el apagado), lo cierra antes (ParaAlCerrar); si
// se cae en seco, el guion lo ve (vigila al dueno) y cierra cog solo.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Rocola.Nativo;
using Rocola.Pantalla;

namespace Rocola
{
    internal static partial class RocolaOs
    {
        // (gancho g101) la pantalla que abrio el anfitrion; solo la usa el navegador
        internal static ISuperficie LaSuperficie;
        // (gancho g100) con el navegador delante, la ventana de la rocola cuenta como minimizada. Sin ROCOLA_OS nunca se pone: false.
        internal static bool NavegadorDelante { get { return RocolaOsNavegador.Delante; } }
    }

    internal static class RocolaOsNavegador
    {
        internal const string Nombre = "navegador";
        internal const string ClaveInicio = "navegador_inicio", ClaveTope = "navegador_tope_mb", ClaveTeclado = "navegador_teclado";
        internal const string Carpeta = "navegador";
        private static volatile bool delante;
        internal static bool Delante { get { return delante; } }

        // la orden del sistema (las pruebas: ROCOLA_NAVEGADOR_ORDEN, con un cog de mentira)
        internal static string Orden { get { string o = Environment.GetEnvironmentVariable("ROCOLA_NAVEGADOR_ORDEN"); return string.IsNullOrEmpty(o) ? "/usr/bin/rocola-navegador" : o; } }
        internal static string CarpetaDatos { get { return RocolaOs.Datos(Carpeta); } }

        // ---------- lo que se sabe del sistema (en un hilo; la hoja lo lee) ----------
        internal static volatile string Falta;       // null: esta; si no, por que no se puede abrir
        internal static volatile bool Mirado;
        internal static long Ocupa = -1;              // bytes de lo guardado (-1: sin mirar)
        internal static void Mira()
        {
            RocolaOs.Hilo("navegador-mira", delegate
            {
                string f = null;
                string o = Orden;
                if (!File.Exists(o)) f = "No está en este sistema: llega con la compilación\ncompleta del sistema (WPE WebKit y cog).";
                else
                {
                    string salida; int r = Corre(o, "hay", 5000, out salida);
                    if (r == 1) f = "No está en este sistema: llega con la compilación\ncompleta del sistema (WPE WebKit y cog).";
                    else if (r != 0) f = "No se pudo saber si está (rocola-navegador hay: " + r + ").";
                    if (r != 0 && !string.IsNullOrEmpty(salida)) RocolaOs.Apunta(Nombre, "rocola-navegador hay (" + r + "): " + salida.Trim().Replace('\n', ' '));
                }
                Falta = f;
                Interlocked.Exchange(ref Ocupa, Tamano(CarpetaDatos));
                Mirado = true;
            });
        }
        internal static long Tamano(string dir)
        {
            long t = 0;
            try
            {
                if (!Directory.Exists(dir)) return 0;
                foreach (string f in Directory.EnumerateFiles(dir, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 }))
                    try { FileInfo i = new FileInfo(f); if (i.LinkTarget == null) t += i.Length; } catch { }
            }
            catch { }
            return t;
        }
        // una orden corta del guion (hay, olvida): su salida y su codigo; -1 si no acabo a tiempo
        internal static int Corre(string orden, string que, int ms, out string salida)
        {
            salida = "";
            try
            {
                ProcessStartInfo si = new ProcessStartInfo(orden) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                si.ArgumentList.Add(que);
                si.Environment["ROCOLA_XP_DATOS"] = Form1.CarpetaDatos;
                using (Process p = Process.Start(si))
                {
                    string o = p.StandardOutput.ReadToEnd();
                    if (!p.WaitForExit(ms)) { try { p.Kill(true); } catch { } return -1; }
                    salida = o; return p.ExitCode;
                }
            }
            catch (Exception ex) { salida = ex.Message; return -2; }
        }

        // ---------- olvidar lo guardado ----------
        internal static string Olvida()
        {
            if (proceso != null) return "No con el navegador abierto.";
            string d = CarpetaDatos;
            try
            {
                if (Directory.Exists(d))
                {
                    foreach (string f in Directory.GetFileSystemEntries(d))
                    {
                        FileAttributes a = File.GetAttributes(f);
                        if ((a & FileAttributes.Directory) != 0 && (a & FileAttributes.ReparsePoint) == 0) Directory.Delete(f, true); else File.Delete(f);
                    }
                }
                Interlocked.Exchange(ref Ocupa, 0);
                RocolaOs.Apunta(Nombre, "se olvido lo guardado del navegador");
                return null;
            }
            catch (Exception ex) { RocolaOs.Falla(Nombre, "olvidar", ex); Interlocked.Exchange(ref Ocupa, Tamano(d)); return "No se pudo borrar todo: " + ex.Message; }
        }

        // ---------- el proceso ----------
        [DllImport("libc", EntryPoint = "kill", SetLastError = true)] private static extern int Mata(int pid, int senal);
        private static Process proceso;
        internal static bool Abierto { get { return proceso != null; } }
        internal static int Veces, UltimoCodigo;
        internal static string UltimoError;
        // CEDER LA PANTALLA (en el hilo de la pantalla). Lo hace todo o nada.
        private static Action componerGuardado;
        internal static void Cede()
        {
            if (delante) return;
            delante = true;   // (g100: la ventana minimizada; XP deja de pintar en la tarjeta)
            componerGuardado = PantallaRocola.Componer;
            PantallaRocola.Componer = NoCompone;
            try { ISuperficie s = RocolaOs.LaSuperficie; if (s != null) s.Suelta(); } catch (Exception ex) { RocolaOs.Falla(Nombre, "soltar la pantalla", ex); }
            Traza("cede (composiciones " + Composiciones + ")");
        }
        private static void NoCompone() { }
        private static int Composiciones { get { Anfitrion a = Anfitrion.Actual; return a != null ? a.Composiciones : -1; } }
        internal static void Retoma()
        {
            if (!delante) return;
            try { ISuperficie s = RocolaOs.LaSuperficie; if (s != null) s.Recupera(); } catch (Exception ex) { RocolaOs.Falla(Nombre, "retomar la pantalla", ex); }
            // los colores del monitor de la hoja Pantalla (el navegador pudo dejar otra tabla)
            try
            {
                IModosPantalla m = RocolaOs.ModosDeLaPantalla; RocolaOsAjustesPantalla a = RocolaOs.AjustesDePantalla;
                string e; if (m != null && a != null && !a.ColoresDeFabrica) RocolaOs.AplicaColores(m, a, out e);
            }
            catch (Exception ex) { RocolaOs.Falla(Nombre, "los colores al volver", ex); }
            if (PantallaRocola.Componer == (Action)NoCompone) PantallaRocola.Componer = componerGuardado;
            componerGuardado = null;
            delante = false;
            PantallaRocola.HayQueComponer();
            Traza("retoma (composiciones " + Composiciones + ")");
        }
        // abre «rocola-navegador abre [URL]»; devuelve el proceso o null (y UltimoError)
        internal static Process Lanza(string url)
        {
            try
            {
                ProcessStartInfo si = new ProcessStartInfo(Orden) { UseShellExecute = false };
                si.ArgumentList.Add("abre");
                if (!string.IsNullOrEmpty(url)) si.ArgumentList.Add(url);
                si.Environment["ROCOLA_XP_DATOS"] = Form1.CarpetaDatos;
                si.Environment["ROCOLA_NAVEGADOR_TOPE_MB"] = RocolaOs.AjusteEntero(ClaveTope).ToString(CultureInfo.InvariantCulture);
                si.Environment["ROCOLA_NAVEGADOR_TECLADO"] = RocolaOs.Ajuste(ClaveTeclado);
                si.Environment["ROCOLA_NAVEGADOR_DUENO"] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
                Process p = Process.Start(si);
                proceso = p; Veces++;
                Traza("abre " + (string.IsNullOrEmpty(url) ? "(inicio)" : "(otra pagina)") + " pid " + p.Id);
                return p;
            }
            catch (Exception ex) { UltimoError = ex.Message; RocolaOs.Falla(Nombre, "abrir", ex); return null; }
        }
        // cerrarlo (desde cualquier hilo): SIGTERM al guion (cierra cog y deja todo como estaba); a los 5 s, SIGKILL
        internal static void Cierra(string por)
        {
            Process p = proceso;
            if (p == null) return;
            int pid; try { if (p.HasExited) return; pid = p.Id; } catch { return; }
            Traza("cierra (" + por + ")");
            Mata(pid, 15);
            Thread t = new Thread(delegate () { try { if (!p.WaitForExit(5000)) { Mata(pid, 9); Traza("no se cerro en 5 s: SIGKILL"); } } catch { } }) { IsBackground = true, Name = "RocolaOs:navegador-mata" };
            t.Start();
        }
        internal static void EsperaQueAcabe(int ms) { Process p = proceso; if (p == null) return; try { p.WaitForExit(ms); } catch { } }
        internal static void Acabo(Process p)
        {
            if (proceso != p) return;
            try { UltimoCodigo = p.ExitCode; } catch { UltimoCodigo = -1; }
            proceso = null;
            try { p.Dispose(); } catch { }
            Traza("se cerro (codigo " + UltimoCodigo + ")");
        }
        // (pruebas) ROCOLA_NAVEGADOR_TRAZA=ruta: cada paso con su hora
        private static readonly string traza = Environment.GetEnvironmentVariable("ROCOLA_NAVEGADOR_TRAZA");
        internal static void Traza(string que)
        {
            RocolaOs.Apunta(Nombre, que);
            if (string.IsNullOrEmpty(traza)) return;
            try { File.AppendAllText(traza, ((long)PantallaRocola.AhoraMs).ToString(CultureInfo.InvariantCulture) + " " + que + "\n"); } catch { }
        }
    }

    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraNavegador()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsNavegador.Nombre,
                Que = "el navegador web de Ctrl+F4 (WPE WebKit con cog)",
                DeFabrica = true,
                Claves = new RocolaOsClave[] {
                    RocolaOsClave.Texto(RocolaOsNavegador.ClaveInicio, "", RocolaOsUrlVale, true, "la pagina con que abre el navegador (vacio: la de inicio de la rocola, con su campo para escribir la direccion)"),
                    RocolaOsClave.Entero(RocolaOsNavegador.ClaveTope, 128, 32, 256, true, "el tope de lo que guarda el navegador (MB; va en lo aprendido, datos.img, de 512 MB): al abrirlo y al cerrarlo, si pasa, se borra la cache"),
                    RocolaOsClave.Texto(RocolaOsNavegador.ClaveTeclado, "us", RocolaOsTecladoVale, true, "la distribucion del teclado en el navegador (la de XKB: us, latam, es...)"),
                },
                ParaAlCerrar = razon =>
                {
                    RocolaOsNavegador.Cierra("la rocola se cierra");
                    RocolaOsNavegador.EsperaQueAcabe(3000);   // (el guion cierra cog en menos de 3 s; si no, su SIGKILL a los 5 s sigue de fondo)
                },
            });
            RocolaOsHojitas.Registra(new RocolaOsHojita { Nombre = "Navegador", Orden = 20, Arma = (f, x) => f.RocolaOsPaginaNavegador(x) });
        }
        private static bool RocolaOsUrlVale(string v)
        {
            if (v.Length > 2000) return false;
            foreach (char c in v) if (c <= ' ' || c == '"' || c == '\'' || c == '\\' || c == '`') return false;
            return v.Length == 0 || v.StartsWith("https://", StringComparison.Ordinal) || v.StartsWith("http://", StringComparison.Ordinal) || v.StartsWith("file:///", StringComparison.Ordinal);
        }
        private static bool RocolaOsTecladoVale(string v)
        {
            if (v.Length == 0 || v.Length > 32) return false;
            foreach (char c in v) if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_' && c != ',' && c != '(' && c != ')' && c != '-') return false;
            return true;
        }

        private static string RocolaOsMegas(long b)
        {
            if (b < 0) return "?";
            double mb = b / 1048576.0;
            return (mb < 10 ? mb.ToString("0.0", CultureInfo.InvariantCulture) : Math.Round(mb).ToString(CultureInfo.InvariantCulture)).Replace('.', ',') + " MB";
        }

        private RocolaOsPagina RocolaOsPaginaNavegador(RocolaOsVentanaCtx x)
        {
            Color cNav = Color.FromArgb(120, 210, 255), cDat = Color.FromArgb(200, 170, 255), cN = Color.FromArgb(232, 186, 112);
            Grupo pg = new Grupo(); pg.SetBounds(0, 96, 960, 580); pg.BackColor = x.CFondo;
            Tarjeta tNav = new Tarjeta(); tNav.SetBounds(16, 4, 456, 568); tNav.Titulo = "NAVEGADOR WEB"; tNav.Acento = cNav;
            Tarjeta tDat = new Tarjeta(); tDat.SetBounds(488, 4, 456, 568); tDat.Titulo = "LO QUE GUARDA"; tDat.Acento = cDat;
            pg.Controls.Add(tNav); pg.Controls.Add(tDat);
            const int ancho = 424;
            Func<Tarjeta, int, int, Letra, Color, string, Rotulo> rot = delegate (Tarjeta t, int y, int alto, Letra f, Color c, string s)
            {
                Rotulo r = new Rotulo(); r.SetBounds(16, y, ancho, alto); r.BackColor = Color.Transparent; r.ForeColor = c; r.Font = f; r.Text = s; r.UseMnemonic = false;
                t.Controls.Add(r); return r;
            };
            bool va = RocolaOs.Encendida(RocolaOsNavegador.Nombre);

            // ================= NAVEGADOR WEB =================
            rot(tNav, 40, 80, x.FSub, x.CTexto,
                "Un navegador a pantalla completa para entrar a claude.ai\no a otras páginas desde la misma rocola, con teclado y\nratón. La rocola le presta la pantalla y la música sigue\nsonando.");
            Rotulo lEsta = rot(tNav, 138, 36, x.FDes, x.CTenue, "Mirando si el sistema lo trae...");
            BotonPlano bAbre = new BotonPlano(); bAbre.SetBounds(16, 180, ancho, 44); bAbre.Font = x.FBot; bAbre.Text = "Abrir el navegador";
            bAbre.Relleno = Color.FromArgb(30, 96, 70); bAbre.Borde = Color.FromArgb(72, 182, 132);
            tNav.Controls.Add(bAbre);
            rot(tNav, 238, 18, x.FNom, x.CTexto, "Para volver a la rocola");
            rot(tNav, 258, 112, x.FDes, x.CTenue,
                "Ctrl + Alt + F1: cierra el navegador y vuelve aquí.\nCtrl + Alt + Inicio: lo abre otra vez en su página de inicio,\npara escribir otra dirección (el navegador no tiene barra).\nLos botones de la rocola no pueden hacer estas combinaciones.\nMientras está abierto, las monedas NO cuentan (el teclado y\nel monedero son la misma tecla para la rocola).");
            Rotulo lInicio = rot(tNav, 380, 54, x.FDes, x.CTenue, "");
            Rotulo lEstado = rot(tNav, 446, 110, x.FDes, x.CAviso, "");

            // ================= LO QUE GUARDA =================
            rot(tDat, 40, 60, x.FSub, x.CTexto,
                "La sesión, las galletas y la caché del navegador\nquedan en la carpeta de datos de la rocola, para no\ntener que volver a entrar cada vez:");
            string cd = RocolaOsNavegador.CarpetaDatos;
            while (cd.Length > 8 && TextoGdiPlus.Mide(cd, x.FDes, AjusteLetra.ClearTypeAjustada, new SizeF(0, 0), FormatoTexto.SinFormato).Width > ancho) cd = "..." + cd.Substring(Math.Min(cd.Length - 4, 8));
            rot(tDat, 104, 18, x.FDes, x.CTenue, cd);
            Rotulo lOcupa = rot(tDat, 140, 22, x.FVal, cDat, "");
            Rotulo lTope = rot(tDat, 166, 40, x.FDes, x.CTenue, "");
            BotonPlano bOlvida = new BotonPlano(); bOlvida.SetBounds(16, 220, ancho, 40); bOlvida.Font = x.FBot; bOlvida.Text = "Olvidar lo del navegador";
            tDat.Controls.Add(bOlvida);
            rot(tDat, 270, 54, x.FDes, x.CTenue, "Borra la sesión, las galletas y la caché: tendrás que volver\na entrar a tus cuentas. Pide confirmar.");
            rot(tDat, 340, 18, x.FNom, x.CTexto, "Ajustes (orden «ajuste» o rocola_os.txt)");
            Rotulo lAj = rot(tDat, 362, 120, x.FDes, x.CTenue, "");

            string estado = null;
            Action pinta = delegate
            {
                string falta = RocolaOsNavegador.Falta;
                string no = null;
                if (!va) no = "Necesita la función «navegador» (navegador=1 en rocola_os.txt).";
                else if (!RocolaOsNavegador.Mirado) no = "Mirando si el sistema lo trae...";
                else if (falta != null) no = falta;
                else if (videoOn) no = "No con un video sonando: cuando acabe.";
                bAbre.Enabled = no == null;
                lEsta.ForeColor = no == null ? Color.FromArgb(120, 230, 165) : cN;
                lEsta.Text = no ?? "Está en el sistema (WPE WebKit con cog).";
                string ini = RocolaOs.Ajuste(RocolaOsNavegador.ClaveInicio);
                lInicio.Text = "Página al abrir: " + (string.IsNullOrEmpty(ini) ? "la de inicio de la rocola (con su campo\npara escribir la dirección y atajos)." : ini);
                lEstado.Text = estado ?? "";
                long oc = Interlocked.Read(ref RocolaOsNavegador.Ocupa);
                lOcupa.Text = oc < 0 ? "Ocupa: ..." : "Ocupa: " + RocolaOsMegas(oc);
                int tope = RocolaOs.AjusteEntero(RocolaOsNavegador.ClaveTope);
                lTope.Text = "Tope: " + tope + " MB (navegador_tope_mb). Si lo pasa, al abrir\ny al cerrar se borra primero la caché.";
                bOlvida.Enabled = va && oc != 0 && !RocolaOsNavegador.Abierto;
                lAj.Text = "navegador_inicio = " + (string.IsNullOrEmpty(ini) ? "(vacío: la de la rocola)" : ini)
                    + "\nnavegador_teclado = " + RocolaOs.Ajuste(RocolaOsNavegador.ClaveTeclado) + "  (us, latam, es...)"
                    + "\nnavegador_tope_mb = " + tope;
            };
            if (va) RocolaOsNavegador.Mira();
            bAbre.Click += delegate
            {
                if (!bAbre.Enabled) return;
                estado = RocolaOsUsaNavegador(x);
                RocolaOsNavegador.Mira();
                pinta();
                bAbre.Focus();
            };
            bOlvida.Click += delegate
            {
                if (!RocolaOsConfirma(x, "¿Olvidar lo del navegador?", "Se borran la sesión, las galletas y la caché del navegador.\nTendrás que volver a entrar a tus cuentas.", "Sí, olvidar")) { bOlvida.Focus(); return; }
                string e = RocolaOsNavegador.Olvida();
                estado = e ?? "Listo: se olvidó lo del navegador.";
                pinta(); bAbre.Focus();
            };
            pinta();

            RocolaOsPagina pag = new RocolaOsPagina();
            pag.Panel = pg; pag.Titulo = "Navegador";
            pag.CadaMedioSegundo = delegate { if (pg.Visible) pinta(); };
            return pag;
        }

        // USAR EL NAVEGADOR: cede la pantalla, lo abre y espera (una hoja que no se ve, con las dos combinaciones) a que se cierre; retoma la
        // pantalla. Devuelve lo que paso, para la hoja.
        private string RocolaOsUsaNavegador(RocolaOsVentanaCtx x)
        {
            if (RocolaOsNavegador.Abierto) return "Ya está abierto.";
            if (videoOn) return "No con un video sonando.";
            string url = RocolaOs.Ajuste(RocolaOsNavegador.ClaveInicio);
            string res = null;
            RocolaOsNavegador.Traza("antes: " + RocolaOsDiceSuena());
            RocolaOsNavegador.Cede();
            try
            {
                Process p = RocolaOsNavegador.Lanza(url);
                if (p == null) return "No se pudo abrir: " + RocolaOsNavegador.UltimoError;
                bool aInicio = false, cerrar = false;
                long desde = Environment.TickCount64;
                using (Hoja q = new Hoja(MarcoXp.Dialogo))
                {
                    q.Text = "Rocola OS - Navegador";
                    q.ClientSize = new Size(560, 150);
                    q.BackColor = x.CFondo; q.Font = x.FSub;
                    Rotulo lT = new Rotulo(); lT.SetBounds(22, 14, 516, 28); lT.BackColor = Color.Transparent; lT.ForeColor = Color.FromArgb(232, 238, 255); lT.Font = x.FTit; lT.Text = "El navegador está abierto";
                    Rotulo lD = new Rotulo(); lD.SetBounds(24, 54, 516, 80); lD.BackColor = Color.Transparent; lD.ForeColor = x.CTenue; lD.Font = x.FSub;
                    lD.Text = "Ctrl + Alt + F1: volver a la rocola.\nCtrl + Alt + Inicio: a la página de inicio del navegador.\n(La música sigue; las monedas no cuentan.)";
                    q.Controls.Add(lT); q.Controls.Add(lD);
                    // SOLO las dos combinaciones; todo lo demas (lo que se escribe en el navegador) se tira, monedas incluidas
                    q.TeclaAntes += delegate (PulsoTecla ke)
                    {
                        ke.SuppressKeyPress = true;
                        Tecla mods = ke.KeyData & (Tecla.Control | Tecla.Alt | Tecla.Shift);
                        if (mods != (Tecla.Control | Tecla.Alt) || ke.Repetida) return;
                        if (ke.KeyCode == Tecla.F1) { cerrar = true; RocolaOsNavegador.Cierra("Ctrl+Alt+F1"); }
                        else if (ke.KeyCode == Tecla.Home) { aInicio = true; RocolaOsNavegador.Cierra("Ctrl+Alt+Inicio"); }
                    };
                    Process actual = p;
                    Action vigila = null;
                    vigila = delegate
                    {
                        Process pv = actual;
                        RocolaOs.Hilo("navegador-espera", delegate
                        {
                            try { pv.WaitForExit(); } catch { }
                            RocolaOs.Encarga("navegador-cerrado", delegate
                            {
                                RocolaOsNavegador.Acabo(pv);
                                // Ctrl+Alt+Inicio: otra vez, en la pagina de inicio, sin devolver la pantalla
                                if (aInicio && !cerrar && !RocolaOs.Cerrandose && !q.IsDisposed)
                                {
                                    aInicio = false;
                                    Process np = RocolaOsNavegador.Lanza("");
                                    if (np != null) { actual = np; vigila(); return; }
                                }
                                if (!q.IsDisposed) q.Close();
                            });
                        });
                    };
                    vigila();
                    q.MuestraModal(x.Dlg);
                }
                // (la hoja se pudo cerrar por el cierre de la rocola: el navegador, fuera)
                if (RocolaOsNavegador.Abierto) RocolaOsNavegador.Cierra("la hoja se cerro");
                long s = (Environment.TickCount64 - desde) / 1000;
                int c = RocolaOsNavegador.UltimoCodigo;
                if (cerrar) res = "Se cerró con Ctrl+Alt+F1 (" + s + " s abierto).";
                else if (c == 0) res = "El navegador se cerró (" + s + " s abierto).";
                else res = "El navegador se cerró con el código " + c + (s < 5 ? " al abrirse: ver /run/rocola-navegador.txt en la consola." : " (" + s + " s abierto).");
            }
            catch (Exception ex) { RocolaOs.Falla(RocolaOsNavegador.Nombre, "usar el navegador", ex); res = "Falló: " + ex.Message; }
            finally
            {
                RocolaOsNavegador.Retoma();
                RocolaOsNavegador.Traza("despues: " + RocolaOsDiceSuena());
            }
            return res;
        }

        // (la bitacora y las pruebas) lo que suena y por donde va: la musica sigue con el navegador abierto
        private string RocolaOsDiceSuena()
        {
            IAudioSource r = reader;
            return (nowPlaying != null ? "suena «" + nowPlaying.Display() + "»" : "nada sonando") + (r != null ? " a " + r.CurrentTime.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s" : "")
                + ", creditos " + credits + ", cola " + queue.Count;
        }

        // UNA CONFIRMACION como «¿Se ve bien?» de la hoja Pantalla: «No» con el foco (Intro o Escape sin mirar: no) y el boton de si.
        internal bool RocolaOsConfirma(RocolaOsVentanaCtx x, string titulo, string texto, string si)
        {
            bool ok = false;
            using (Hoja q = new Hoja(MarcoXp.Dialogo))
            {
                q.Text = "Rocola OS";
                q.ClientSize = new Size(520, 200);
                q.BackColor = x.CFondo; q.Font = x.FSub;
                Rotulo lT = new Rotulo(); lT.SetBounds(22, 14, 476, 28); lT.BackColor = Color.Transparent; lT.ForeColor = Color.FromArgb(232, 238, 255); lT.Font = x.FTit; lT.Text = titulo; lT.UseMnemonic = false;
                Rotulo lD = new Rotulo(); lD.SetBounds(24, 52, 476, 70); lD.BackColor = Color.Transparent; lD.ForeColor = x.CTenue; lD.Font = x.FSub; lD.Text = texto; lD.UseMnemonic = false;
                BotonPlano bNo = new BotonPlano(); bNo.SetBounds(22, 138, 230, 40); bNo.Font = x.FBot; bNo.TabIndex = 0; bNo.Text = "No";
                BotonPlano bSi = new BotonPlano(); bSi.SetBounds(268, 138, 230, 40); bSi.Font = x.FBot; bSi.TabIndex = 1; bSi.Text = si;
                bSi.Relleno = Color.FromArgb(110, 40, 46); bSi.Borde = Color.FromArgb(220, 96, 96);
                bNo.Click += delegate { ok = false; q.Close(); };
                bSi.Click += delegate { ok = true; q.Close(); };
                q.Controls.Add(bNo); q.Controls.Add(bSi); q.Controls.Add(lT); q.Controls.Add(lD);
                q.TeclaAntes += delegate (PulsoTecla ke) { if (ke.KeyCode == Tecla.Escape) { ok = false; q.Close(); } };
                MonedaTambienAqui(q);
                q.MuestraModal(x.Dlg);
            }
            return ok;
        }
    }
}
