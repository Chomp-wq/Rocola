// ROCOLA OS (fase 11): LA HOJITA «PANTALLA» DE LA VENTANA DE CTRL+F4 (peticion del dueno del 15-11-2026; FASE11_pantalla.md). Dos tarjetas
// como las de Ctrl+F3, con sus filas (nombre, valor a la derecha, explicacion y la barra debajo: la FilaAjuste de VentanaAjustes):
//   RESOLUCION Y HERCIOS: los modos del monitor (por KMS, con su EDID; de 1024x768 para arriba: las ventanas de la rocola miden 966x764),
//     «Probar este modo» (15 s con «¿Se ve bien?»; si nadie contesta, vuelve solo al anterior; solo lo confirmado se guarda) y «Volver a lo
//     automatico».
//   COLORES DEL MONITOR: brillo, contraste, gamma, temperatura y saturacion (esta solo si la tarjeta tiene matriz de color, CTM: si no, en
//     gris diciendo por que, regla del 16-10-2026), por la tabla de gamma del controlador: se ve al instante y no cuesta nada por cuadro.
//     «Restablecer los colores» vuelve a los de fabrica.
// Todo se guarda para siempre en rocola_pantalla.txt (RocolaOs/RocolaOs.Pantalla.cs) y se pone al arrancar.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using Rocola.Nativo;
using Rocola.Pantalla;

namespace Rocola
{
    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraHojitaPantalla()
        {
            RocolaOsHojitas.Registra(new RocolaOsHojita { Nombre = "Pantalla", Orden = 10, Arma = (f, x) => f.RocolaOsPaginaPantalla(x) });
        }

        // los modos que se ofrecen: los del monitor de 1024x768 para arriba (y siempre el que esta puesto), sin repetir; por resolucion, de
        // la mas grande a la mas chica, y en cada una los hercios de mas a menos
        private static List<List<ModoPantalla>> RocolaOsModosAgrupados(IModosPantalla m)
        {
            ModoPantalla act = m.Actual;
            List<ModoPantalla> l = new List<ModoPantalla>();
            HashSet<string> ya = new HashSet<string>(StringComparer.Ordinal);
            foreach (ModoPantalla p in m.Modos)
                if ((p.Ancho >= 1024 && p.Alto >= 768) || p.Clave == act.Clave)
                    if (ya.Add(p.Clave)) l.Add(p);
            l.Sort((a, b) =>
            {
                int c = ((long)b.Ancho * b.Alto).CompareTo((long)a.Ancho * a.Alto); if (c != 0) return c;
                c = b.Ancho.CompareTo(a.Ancho); if (c != 0) return c;
                c = a.Entrelazado.CompareTo(b.Entrelazado); if (c != 0) return c;
                return b.Hz.CompareTo(a.Hz);
            });
            List<List<ModoPantalla>> g = new List<List<ModoPantalla>>();
            foreach (ModoPantalla p in l)
            {
                if (g.Count == 0 || g[g.Count - 1][0].Ancho != p.Ancho || g[g.Count - 1][0].Alto != p.Alto) g.Add(new List<ModoPantalla>());
                g[g.Count - 1].Add(p);
            }
            return g;
        }
        private static string RocolaOsDiceHz(ModoPantalla p)
        {
            return p.Hz.ToString(CultureInfo.InvariantCulture) + " Hz" + (p.Entrelazado ? " (entrelazado)" : "");
        }

        private RocolaOsPagina RocolaOsPaginaPantalla(RocolaOsVentanaCtx x)
        {
            Hoja dlg = x.Dlg;
            Color cRes = Color.FromArgb(150, 190, 255), cCol = Color.FromArgb(255, 196, 120);
            Grupo pg = new Grupo(); pg.SetBounds(0, 96, 960, 580); pg.BackColor = x.CFondo;
            Tarjeta tRes = new Tarjeta(); tRes.SetBounds(16, 4, 456, 568); tRes.Titulo = "RESOLUCIÓN Y HERCIOS"; tRes.Acento = cRes;
            Tarjeta tCol = new Tarjeta(); tCol.SetBounds(488, 4, 456, 568); tCol.Titulo = "COLORES DEL MONITOR"; tCol.Acento = cCol;
            pg.Controls.Add(tRes); pg.Controls.Add(tCol);
            const int anchoFila = 424;
            IModosPantalla m = RocolaOs.ModosDeLaPantalla;
            RocolaOsAjustesPantalla aj = RocolaOs.AjustesDePantalla ?? new RocolaOsAjustesPantalla();
            bool va = m != null && RocolaOs.Encendida(RocolaOs.NombrePantalla);

            // --- una fila: nombre arriba, valor a la derecha, explicacion debajo y la barra abajo (FilaAjuste de VentanaAjustes) ---
            Func<Tarjeta, int, string, Color, int, int, int, Tuple<Rotulo, Rotulo, Deslizador>> fila = delegate (Tarjeta card, int y, string titulo, Color ac, int min, int max, int alto)
            {
                Rotulo nom = new Rotulo(); nom.SetBounds(16, y, anchoFila - 120, 18); nom.BackColor = Color.Transparent;
                nom.ForeColor = x.CTexto; nom.Font = x.FNom; nom.Text = titulo;
                Rotulo val = new Rotulo(); val.SetBounds(16 + anchoFila - 160, y - 2, 160, 20); val.BackColor = Color.Transparent;
                val.ForeColor = ac; val.Font = x.FVal; val.TextAlign = AlineaTexto.TopRight;
                Rotulo des = new Rotulo(); des.SetBounds(16, y + 21, anchoFila, alto); des.BackColor = Color.Transparent;
                des.ForeColor = x.CTenue; des.Font = x.FDes;
                Deslizador bar = new Deslizador(); bar.SetBounds(16, y + 21 + alto + 2, anchoFila, 22);
                bar.Minimum = min; bar.Maximum = max; bar.Acento = ac;
                card.Controls.Add(nom); card.Controls.Add(val); card.Controls.Add(des); card.Controls.Add(bar);
                return Tuple.Create(val, des, bar);
            };

            // ================= RESOLUCION Y HERCIOS =================
            Rotulo lAhora = new Rotulo(); lAhora.SetBounds(16, 40, anchoFila, 20); lAhora.BackColor = Color.Transparent;
            lAhora.ForeColor = x.CTexto; lAhora.Font = x.FNom;
            Rotulo lAhora2 = new Rotulo(); lAhora2.SetBounds(16, 62, anchoFila, 18); lAhora2.BackColor = Color.Transparent;
            lAhora2.ForeColor = x.CTenue; lAhora2.Font = x.FDes;
            tRes.Controls.Add(lAhora); tRes.Controls.Add(lAhora2);
            var fRes = fila(tRes, 96, "Resolución", cRes, 0, 0, 32);
            var fHz = fila(tRes, 186, "Hercios", cRes, 0, 0, 32);
            fRes.Item2.Text = "Las que da el monitor, de 1024 x 768 para arriba\n(las ventanas de la rocola miden 966 x 764).";
            fHz.Item2.Text = "Las veces por segundo que se pinta la pantalla,\nlas que da el monitor a esa resolución.";
            BotonPlano bProbar = new BotonPlano(); bProbar.SetBounds(16, 284, anchoFila, 38); bProbar.Font = x.FBot;
            BotonPlano bAuto = new BotonPlano(); bAuto.SetBounds(16, 330, anchoFila, 38); bAuto.Font = x.FBot; bAuto.Text = "Volver a lo automático";
            Rotulo lResInfo = new Rotulo(); lResInfo.SetBounds(16, 380, anchoFila, 66); lResInfo.BackColor = Color.Transparent;
            lResInfo.ForeColor = x.CTenue; lResInfo.Font = x.FDes;
            lResInfo.Text = "Al probar, la pantalla cambia y pregunta «¿Se ve bien?»: si nadie\ncontesta en " + RocolaOs.PantallaPruebaSegundos
                + " s, vuelve sola a la de antes. Lo confirmado se guarda\npara siempre; «rocola-pantalla auto» en la consola de\nmantenimiento lo deshace.";
            Rotulo lResEstado = new Rotulo(); lResEstado.SetBounds(16, 454, anchoFila, 62); lResEstado.BackColor = Color.Transparent;
            lResEstado.ForeColor = x.CAviso; lResEstado.Font = x.FDes;
            Rotulo lTarjeta = new Rotulo(); lTarjeta.SetBounds(16, 522, anchoFila, 34); lTarjeta.BackColor = Color.Transparent;
            lTarjeta.ForeColor = x.CTenue; lTarjeta.Font = x.FDes;
            tRes.Controls.Add(bProbar); tRes.Controls.Add(bAuto); tRes.Controls.Add(lResInfo); tRes.Controls.Add(lResEstado); tRes.Controls.Add(lTarjeta);

            List<List<ModoPantalla>> grupos = m != null ? RocolaOsModosAgrupados(m) : new List<List<ModoPantalla>>();
            int gSel = 0, hSel = 0;
            string estadoRes = null;
            Action eligeActual = delegate
            {
                if (m == null) return;
                string c = m.Actual.Clave;
                grupos = RocolaOsModosAgrupados(m);
                for (int i = 0; i < grupos.Count; i++) for (int j = 0; j < grupos[i].Count; j++) if (grupos[i][j].Clave == c) { gSel = i; hSel = j; }
            };
            Action pintaRes = delegate
            {
                if (m == null || !va || grupos.Count == 0)
                {
                    lAhora.Text = "Ahora: " + PantallaRocola.Ancho + " x " + PantallaRocola.Alto + " a " + PantallaRocola.RefrescoHz + " Hz";
                    lAhora2.Text = "";
                    fRes.Item1.Text = "-"; fHz.Item1.Text = "-";
                    fRes.Item3.Enabled = false; fHz.Item3.Enabled = false; bProbar.Enabled = false; bAuto.Enabled = false;
                    bProbar.Text = "Probar este modo (" + RocolaOs.PantallaPruebaSegundos + " s)";
                    lResEstado.Text = m == null ? "No se puede: la pantalla no se abrió por KMS (fuera de pantalla)\no la hoja Pantalla está apagada (pantalla=0 en rocola_os.txt)."
                                                : "No se puede: la hoja Pantalla está apagada (pantalla=0 en rocola_os.txt).";
                    return;
                }
                ModoPantalla a = m.Actual;
                bool esAuto = a.Clave == RocolaOs.ClaveAutomatica;
                lAhora.Text = "Ahora: " + a.Ancho + " x " + a.Alto + " a " + RocolaOsDiceHz(a);
                RocolaOsAjustesPantalla p = RocolaOs.AjustesDePantalla;
                lAhora2.Text = esAuto ? "Lo automático (lo que da el monitor para la rocola de XP)" : (p != null && p.Modo == a.Clave ? "La que guardaste" : "Sin guardar");
                if (gSel >= grupos.Count) gSel = grupos.Count - 1;
                if (hSel >= grupos[gSel].Count) hSel = grupos[gSel].Count - 1;
                fRes.Item3.Maximum = Math.Max(0, grupos.Count - 1); fRes.Item3.Value = grupos.Count - 1 - gSel; fRes.Item3.Enabled = grupos.Count > 1;
                fHz.Item3.Maximum = Math.Max(0, grupos[gSel].Count - 1); fHz.Item3.Value = grupos[gSel].Count - 1 - hSel; fHz.Item3.Enabled = grupos[gSel].Count > 1;
                ModoPantalla s = grupos[gSel][hSel];
                fRes.Item1.Text = s.Ancho + " x " + s.Alto;
                fHz.Item1.Text = RocolaOsDiceHz(s) + (s.Clave == RocolaOs.ClaveAutomatica ? " *" : "");
                bool otro = s.Clave != a.Clave;
                bProbar.Enabled = otro;
                bProbar.Text = otro ? "Probar " + s.Ancho + " x " + s.Alto + " a " + s.Hz + " Hz (" + RocolaOs.PantallaPruebaSegundos + " s)" : "Probar este modo (" + RocolaOs.PantallaPruebaSegundos + " s)";
                bAuto.Enabled = !esAuto || (p != null && p.Modo != null);
                string t = estadoRes;
                if (t == null && p != null && p.ModoMalo != null) t = "El modo " + p.ModoMalo + "\nno funcionó: se volvió a lo automático.";
                lResEstado.ForeColor = estadoRes != null && estadoRes.StartsWith("Guardad") ? Color.FromArgb(120, 230, 165) : x.CAviso;
                lResEstado.Text = t ?? "";
                lTarjeta.Text = "* = lo automático. Tarjeta: " + (string.IsNullOrEmpty(m.Controlador) ? "?" : m.Controlador)
                    + (m.TamanoGamma > 0 ? ", tabla de gamma por " + m.ViaGamma : ", sin tabla de gamma") + (m.ConCtm ? ", con CTM." : ", sin CTM.");
            };
            // las barras: a la izquierda la resolucion mas chica (y los hercios mas bajos), a la derecha la mas grande, como subir un volumen
            fRes.Item3.Scroll += delegate
            {
                if (grupos.Count == 0) return;
                gSel = Math.Max(0, grupos.Count - 1 - fRes.Item3.Value);
                hSel = 0; estadoRes = null; pintaRes();
            };
            fHz.Item3.Scroll += delegate
            {
                if (grupos.Count == 0) return;
                hSel = grupos[gSel].Count - 1 - fHz.Item3.Value; estadoRes = null; pintaRes();
            };
            bProbar.Click += delegate
            {
                if (m == null || grupos.Count == 0) return;
                ModoPantalla nuevo = grupos[gSel][hSel], antes = m.Actual;
                if (nuevo.Clave == antes.Clave) return;
                string err;
                if (!RocolaOs.CambiaModoPantalla(nuevo.Indice, out err)) { estadoRes = "No se pudo poner " + nuevo.Clave + ":\n" + err; eligeActual(); pintaRes(); return; }
                eligeActual(); pintaRes();
                bool bien = RocolaOsPreguntaSeVeBien(x, nuevo);
                if (bien && !RocolaOs.Cerrandose)
                {
                    RocolaOs.GuardaModoPantalla(nuevo.Clave);
                    estadoRes = "Guardado: " + nuevo.Ancho + " x " + nuevo.Alto + " a " + nuevo.Hz + " Hz, para siempre.";
                }
                else
                {
                    string e2;
                    if (RocolaOs.CambiaModoPantalla(antes.Indice, out e2)) estadoRes = "Nadie confirmó: se volvió a " + antes.Ancho + " x " + antes.Alto + " a " + antes.Hz + " Hz.";
                    else estadoRes = "No se pudo volver a " + antes.Clave + ": " + e2;
                    RocolaOs.Apunta(RocolaOs.NombrePantalla, "prueba de " + nuevo.Clave + " sin confirmar: vuelve a " + antes.Clave);
                }
                eligeActual(); pintaRes();
                // (con el teclado: «Probar» queda en gris -ya es el modo puesto- y el foco se iria solo a la otra tarjeta; se queda en la barra)
                if (fRes.Item3.Enabled) fRes.Item3.Focus();
            };
            bAuto.Click += delegate
            {
                if (m == null) return;
                int ia = m.Automatico;
                string err;
                if (m.Actual.Clave != RocolaOs.ClaveAutomatica && !RocolaOs.CambiaModoPantalla(ia, out err)) { estadoRes = "No se pudo poner lo automático: " + err; pintaRes(); return; }
                RocolaOs.GuardaModoPantalla(null);
                estadoRes = "Guardado: lo automático.";
                eligeActual(); pintaRes();
                if (fRes.Item3.Enabled) fRes.Item3.Focus();
            };
            eligeActual();
            pintaRes();

            // ================= COLORES DEL MONITOR =================
            bool hayGamma = va && m != null && m.TamanoGamma > 0, hayCtm = va && m != null && m.ConCtm;
            var fBri = fila(tCol, 40, "Brillo", cCol, RocolaOsAjustesPantalla.BrilloMin, RocolaOsAjustesPantalla.BrilloMax, 16);
            var fCon = fila(tCol, 118, "Contraste", cCol, RocolaOsAjustesPantalla.ContrasteMin, RocolaOsAjustesPantalla.ContrasteMax, 16);
            var fGam = fila(tCol, 196, "Gamma", cCol, RocolaOsAjustesPantalla.GammaMin, RocolaOsAjustesPantalla.GammaMax, 16);
            var fTem = fila(tCol, 274, "Temperatura de color", cCol, RocolaOsAjustesPantalla.TemperaturaMin, RocolaOsAjustesPantalla.TemperaturaMax, 16);
            var fSat = fila(tCol, 352, "Saturación", cCol, RocolaOsAjustesPantalla.SaturacionMin, RocolaOsAjustesPantalla.SaturacionMax, 16);
            fBri.Item2.Text = "Más oscuro a la izquierda, más claro a la derecha.";
            fCon.Item2.Text = "La diferencia entre lo claro y lo oscuro.";
            fGam.Item2.Text = "Más de 1,00 aclara los tonos medios; menos, los oscurece.";
            fTem.Item2.Text = "Más cálido (rojizo) a la izquierda, más frío (azulado) a la derecha.";
            fSat.Item2.Text = "Lo vivo de los colores (0 % = blanco y negro).";
            BotonPlano bFab = new BotonPlano(); bFab.SetBounds(16, 440, anchoFila, 38); bFab.Font = x.FBot; bFab.Text = "Restablecer los colores (de fábrica)";
            Rotulo lColInfo = new Rotulo(); lColInfo.SetBounds(16, 488, anchoFila, 70); lColInfo.BackColor = Color.Transparent;
            lColInfo.ForeColor = x.CTenue; lColInfo.Font = x.FDes;
            tCol.Controls.Add(bFab); tCol.Controls.Add(lColInfo);
            Deslizador[] barras = { fBri.Item3, fCon.Item3, fGam.Item3, fTem.Item3, fSat.Item3 };
            Rotulo[] valores = { fBri.Item1, fCon.Item1, fGam.Item1, fTem.Item1, fSat.Item1 };
            Action pintaValores = delegate
            {
                fBri.Item1.Text = aj.Brillo > 0 ? "+" + aj.Brillo : aj.Brillo.ToString(CultureInfo.InvariantCulture);
                fCon.Item1.Text = aj.Contraste + " %";
                fGam.Item1.Text = (aj.Gamma / 100.0).ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
                fTem.Item1.Text = aj.Temperatura == 0 ? "Neutra" : aj.Temperatura < 0 ? "Cálida " + (-aj.Temperatura) : "Fría " + aj.Temperatura;
                fSat.Item1.Text = aj.Saturacion + " %";
            };
            Action ponBarras = delegate
            {
                fBri.Item3.Value = aj.Brillo; fCon.Item3.Value = aj.Contraste; fGam.Item3.Value = aj.Gamma; fTem.Item3.Value = aj.Temperatura; fSat.Item3.Value = aj.Saturacion;
                pintaValores();
            };
            for (int i = 0; i < 4; i++) barras[i].Enabled = hayGamma;
            fSat.Item3.Enabled = hayCtm;
            string ctl = m == null || string.IsNullOrEmpty(m.Controlador) ? "esta tarjeta" : "la de esta rocola (" + m.Controlador + ")";
            if (!hayGamma)
            {
                Color cN = Color.FromArgb(232, 186, 112);   // el color de «Necesita» de Ctrl+F3 (Interruptor.ColorNecesita)
                string por = !va ? "Necesita la hoja Pantalla (pantalla=1 y la pantalla por KMS)." : "Necesita una tarjeta con tabla de gamma; " + ctl + " no la tiene.";
                foreach (var f in new[] { fBri, fCon, fGam, fTem }) { f.Item2.Text = por; f.Item2.ForeColor = cN; }
            }
            if (!hayCtm)
            {
                fSat.Item2.Text = !va ? "Necesita la hoja Pantalla (pantalla=1 y la pantalla por KMS)." : "Necesita una tarjeta con matriz de color (CTM);\n" + ctl + " no la tiene.";
                fSat.Item2.ForeColor = Color.FromArgb(232, 186, 112);
                fSat.Item2.SetBounds(16, 352 + 21, anchoFila, 30); fSat.Item3.SetBounds(16, 352 + 21 + 32, anchoFila, 22);
            }
            bFab.Enabled = hayGamma || hayCtm;
            lColInfo.Text = hayGamma
                ? "Lo hace la tarjeta con su tabla de gamma (" + m.TamanoGamma + " valores): se ve\nal instante y no le cuesta nada a la rocola en cada cuadro.\nSe guarda para siempre y se pone al arrancar."
                : "";
            // EL CAMBIO, AL INSTANTE: como mucho uno cada 50 ms (en la radeon cada tabla nueva espera 10 ms en el nucleo); lo que queda, con el
            // reloj de los colores. Se guarda cuando se deja quieto medio segundo (como lo de Ctrl+F3) y al cerrar.
            long ultimoPuesto = 0, ultimoCambio = 0; bool pendiente = false, sucio = false;
            Action ponYa = delegate
            {
                pendiente = false; ultimoPuesto = Environment.TickCount64;
                string e;
                if (!RocolaOs.AplicaColores(m, aj, out e)) lColInfo.Text = "No se pudo: " + e;
            };
            Reloj tCol50 = new Reloj("RocolaOs:colores", null); tCol50.Interval = 50;
            tCol50.Tick += delegate { if (pendiente && Environment.TickCount64 - ultimoPuesto >= 50) ponYa(); };
            Action cambio = delegate
            {
                aj.Brillo = fBri.Item3.Value; aj.Contraste = fCon.Item3.Value; aj.Gamma = fGam.Item3.Value; aj.Temperatura = fTem.Item3.Value; aj.Saturacion = fSat.Item3.Value;
                pintaValores();
                sucio = true; ultimoCambio = Environment.TickCount64;
                if (Environment.TickCount64 - ultimoPuesto >= 50) ponYa(); else pendiente = true;
            };
            foreach (Deslizador b in barras) b.Scroll += delegate { cambio(); };
            bFab.Click += delegate
            {
                aj.ColoresAFabrica(); ponBarras(); ponYa();
                RocolaOs.GuardaPantalla(); sucio = false;
                RocolaOs.Apunta(RocolaOs.NombrePantalla, "colores de fabrica");
            };
            ponBarras();
            if (hayGamma || hayCtm) tCol50.Start();
            Action guarda = delegate
            {
                if (!sucio) return;
                sucio = false;
                if (pendiente) ponYa();
                RocolaOs.GuardaPantalla();
                RocolaOs.Apunta(RocolaOs.NombrePantalla, "colores guardados: " + RocolaOs.DiceColores(aj));
            };

            RocolaOsPagina pag = new RocolaOsPagina();
            pag.Panel = pg; pag.Titulo = "Pantalla";
            pag.CadaMedioSegundo = delegate { if (sucio && Environment.TickCount64 - ultimoCambio >= 500) guarda(); };
            pag.AlCerrar = delegate { tCol50.Stop(); tCol50.Dispose(); guarda(); };
            return pag;
        }

        // LA PRUEBA DE UN MODO: «¿Se ve bien?» con «No (vuelve en N s)» y «Si, se ve bien». Una hoja de dialogo encima de la ventana de Ctrl+F4,
        // con el foco en «No» (Intro o Escape sin mirar: vuelve), la moneda contando y un reloj de 1 s; a los 15 s sin respuesta, No. Devuelve
        // si se confirmo.
        private bool RocolaOsPreguntaSeVeBien(RocolaOsVentanaCtx x, ModoPantalla nuevo)
        {
            bool bien = false;
            using (Hoja q = new Hoja(MarcoXp.Dialogo))
            {
                q.Text = "Rocola OS - Pantalla";
                q.ClientSize = new Size(500, 200);
                q.BackColor = x.CFondo;
                q.Font = x.FSub;
                Rotulo lT = new Rotulo(); lT.SetBounds(22, 14, 456, 28); lT.BackColor = Color.Transparent;
                lT.ForeColor = Color.FromArgb(232, 238, 255); lT.Font = x.FTit; lT.Text = "¿Se ve bien?";
                Rotulo lD = new Rotulo(); lD.SetBounds(24, 52, 456, 54); lD.BackColor = Color.Transparent;
                lD.ForeColor = x.CTenue; lD.Font = x.FSub;
                lD.Text = "La pantalla está en " + nuevo.Ancho + " x " + nuevo.Alto + " a " + nuevo.Hz + " Hz.\nSi no se ve bien o nadie contesta, vuelve sola a la de antes.";
                int quedan = RocolaOs.PantallaPruebaSegundos;
                BotonPlano bNo = new BotonPlano(); bNo.SetBounds(22, 136, 220, 40); bNo.Font = x.FBot; bNo.TabIndex = 0;
                bNo.Text = "No (vuelve en " + quedan + " s)";
                BotonPlano bSi = new BotonPlano(); bSi.SetBounds(258, 136, 220, 40); bSi.Font = x.FBot; bSi.TabIndex = 1;
                bSi.Text = "Sí, se ve bien"; bSi.Relleno = Color.FromArgb(30, 96, 70); bSi.Borde = Color.FromArgb(72, 182, 132);
                bNo.Click += delegate { bien = false; q.Close(); };
                bSi.Click += delegate { bien = true; q.Close(); };
                q.Controls.Add(bNo); q.Controls.Add(bSi); q.Controls.Add(lT); q.Controls.Add(lD);
                q.TeclaAntes += delegate (PulsoTecla ke) { if (ke.KeyCode == Tecla.Escape) { bien = false; q.Close(); } };
                MonedaTambienAqui(q);
                long desde = Environment.TickCount64;
                Reloj r = new Reloj("RocolaOs:pantalla-prueba", null); r.Interval = 1000;
                r.Tick += delegate
                {
                    // POR EL RELOJ, no por la cuenta de avisos: si con el modo nuevo la rocola pinta a tirones (un cuadro de segundos), los avisos de
                    // 1 s llegan tarde y la vuelta sola tardaria mucho mas de 15 s
                    quedan = RocolaOs.PantallaPruebaSegundos - (int)((Environment.TickCount64 - desde) / 1000);
                    if (quedan <= 0) { r.Stop(); bien = false; RocolaOs.Apunta(RocolaOs.NombrePantalla, "prueba de " + nuevo.Clave + ": nadie contesto en " + RocolaOs.PantallaPruebaSegundos + " s"); q.Close(); return; }
                    bNo.Text = "No (vuelve en " + quedan + " s)"; bNo.Invalidate();
                };
                r.Start();
                RocolaOs.Apunta(RocolaOs.NombrePantalla, "prueba de " + nuevo.Clave + ": «¿Se ve bien?»");
                q.MuestraModal(x.Dlg);
                r.Stop(); r.Dispose();
            }
            return bien;
        }
    }
}
