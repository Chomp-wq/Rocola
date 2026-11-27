// ROCOLA OS (fase 11): LA HOJITA «SONIDO» DE LA VENTANA DE CTRL+F4 (peticion del dueno del 21-11-2026; FASE11_salida-audio.md): el selector
// de la salida de sonido. Dos tarjetas como las de Ctrl+F3 y la hoja Pantalla:
//   SALIDAS DE SONIDO: las del equipo, una por renglon, con su numero (el de «rocola-sonido usar N»), si es de la PLACA (analogica: la que
//     va al amplificador por la salida verde) o del HDMI de la pantalla, su nombre y cual suena ahora. Un clic (o Intro) la elige.
//   LA QUE SE USA: la de ahora, «Usar esta salida» (se guarda en rocola_tarjeta.txt como «rocola-sonido usar N», la rocola se reabre
//     conservando creditos, cola y lo que suena, y al volver pregunta «¿Se oye bien?» 15 s: sin respuesta vuelve sola a la de antes),
//     «Probar un pitido» (3 s por otra tarjeta, sin cambiar nada: solo si no es la tarjeta que esta sonando) y «Automática».
// Lo que no es dibujo: RocolaOs/RocolaOs.Salida.cs.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using Rocola.Nativo;

namespace Rocola
{
    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraHojitaSalida()
        {
            // (Orden 15: entre Pantalla y Archivos. Las pestanas van pegadas a la derecha; el navegador se quito el 27-11-2026)
            RocolaOsHojitas.Registra(new RocolaOsHojita { Nombre = "Sonido", Orden = 15, Arma = (f, x) => f.RocolaOsPaginaSalida(x) });
        }

        private const int SalidaFilasMax = 8;

        private RocolaOsPagina RocolaOsPaginaSalida(RocolaOsVentanaCtx x)
        {
            Hoja dlg = x.Dlg;
            Color cSal = Color.FromArgb(150, 230, 180), cHdmi = Color.FromArgb(255, 196, 120), cUso = Color.FromArgb(120, 230, 165);
            Grupo pg = new Grupo(); pg.SetBounds(0, 96, 960, 580); pg.BackColor = x.CFondo;
            Tarjeta tLis = new Tarjeta(); tLis.SetBounds(16, 4, 456, 568); tLis.Titulo = "SALIDAS DE SONIDO"; tLis.Acento = cSal;
            Tarjeta tUso = new Tarjeta(); tUso.SetBounds(488, 4, 456, 568); tUso.Titulo = "LA QUE SE USA"; tUso.Acento = Color.FromArgb(150, 190, 255);
            pg.Controls.Add(tLis); pg.Controls.Add(tUso);
            const int anchoFila = 424;
            bool va = RocolaOs.Encendida(RocolaOs.NombreSalida);
            List<RocolaOsSalida> salidas = RocolaOs.SalidasDeSonido();
            string enUso = RocolaOs.SalidaEnUso(salidas);
            string guardada = RocolaOs.SalidaGuardada();
            RocolaOsSalida auto = RocolaOs.SalidaAutomatica(salidas);
            int sel = 0;
            for (int i = 0; i < salidas.Count; i++) if (salidas[i].Clave == enUso) sel = i;
            Func<RocolaOsSalida, string> dice = s => s.Numero + " · " + s.Tipo + " · " + (s.Nombre.Length > 0 ? s.Nombre : s.Clave);

            // ================= SALIDAS DE SONIDO =================
            List<BotonPlano> filas = new List<BotonPlano>();
            List<Rotulo> notas = new List<Rotulo>();
            int n = Math.Min(salidas.Count, SalidaFilasMax);
            for (int i = 0; i < n; i++)
            {
                RocolaOsSalida s = salidas[i];
                int y = 40 + i * 58;
                BotonPlano b = new BotonPlano(); b.SetBounds(16, y, anchoFila, 34); b.Font = x.FInt;
                Rotulo r = new Rotulo(); r.SetBounds(16, y + 36, anchoFila, 16); r.BackColor = Color.Transparent; r.ForeColor = x.CTenue; r.Font = x.FDes;
                r.Text = "tarjeta " + s.Tarjeta + ", salida " + s.Dispositivo + " (" + s.Clave + ")" + (s.NombreTarjeta.Length > 0 ? " · " + s.NombreTarjeta : "")
                    + (s == auto ? " · la automática" : "");
                int k = i;
                b.Click += delegate { sel = k; };
                tLis.Controls.Add(b); tLis.Controls.Add(r);
                filas.Add(b); notas.Add(r);
            }
            Rotulo lVacia = new Rotulo(); lVacia.SetBounds(16, 40 + n * 58 + 4, anchoFila, 60); lVacia.BackColor = Color.Transparent;
            lVacia.ForeColor = x.CAviso; lVacia.Font = x.FDes;
            if (salidas.Count == 0) lVacia.Text = "Linux no ve ninguna salida de sonido (/proc/asound sin tarjetas). En la consola de mantenimiento, «rocola-sonido» dice más.";
            else if (salidas.Count > n) lVacia.Text = "Hay " + salidas.Count + " salidas: aquí se ven las primeras " + n + ". Las demás, con «rocola-sonido» en la consola de mantenimiento.";
            tLis.Controls.Add(lVacia);

            // ================= LA QUE SE USA =================
            Rotulo lAhora = new Rotulo(); lAhora.SetBounds(16, 40, anchoFila, 20); lAhora.BackColor = Color.Transparent; lAhora.ForeColor = x.CTexto; lAhora.Font = x.FNom;
            Rotulo lAhora2 = new Rotulo(); lAhora2.SetBounds(16, 62, anchoFila, 34); lAhora2.BackColor = Color.Transparent; lAhora2.ForeColor = x.CTenue; lAhora2.Font = x.FDes;
            Rotulo lSel = new Rotulo(); lSel.SetBounds(16, 108, anchoFila, 20); lSel.BackColor = Color.Transparent; lSel.ForeColor = x.CTexto; lSel.Font = x.FNom;
            BotonPlano bUsar = new BotonPlano(); bUsar.SetBounds(16, 136, anchoFila, 38); bUsar.Font = x.FBot;
            bUsar.Relleno = Color.FromArgb(30, 96, 70); bUsar.Borde = Color.FromArgb(72, 182, 132);
            BotonPlano bProbar = new BotonPlano(); bProbar.SetBounds(16, 182, anchoFila, 38); bProbar.Font = x.FBot;
            BotonPlano bAuto = new BotonPlano(); bAuto.SetBounds(16, 228, anchoFila, 38); bAuto.Font = x.FBot;
            Rotulo lInfo = new Rotulo(); lInfo.SetBounds(16, 278, anchoFila, 150); lInfo.BackColor = Color.Transparent; lInfo.ForeColor = x.CTenue; lInfo.Font = x.FDes;
            lInfo.Text = "«Usar esta salida» la guarda (como «rocola-sonido usar N») y la rocola se vuelve a abrir en unos segundos sin perder "
                + "los créditos, la cola ni lo que suena. Al volver pregunta «¿Se oye bien?»: si nadie contesta en " + RocolaOs.SalidaPruebaSegundos
                + " s, vuelve sola a la de antes. «Probar un pitido» suena 3 s por otra tarjeta sin cambiar nada (la que está sonando no se puede "
                + "probar aparte). La automática es la primera que no es del HDMI. «rocola-sonido auto» en la consola de mantenimiento lo deshace.";
            Rotulo lEstado = new Rotulo(); lEstado.SetBounds(16, 436, anchoFila, 120); lEstado.BackColor = Color.Transparent; lEstado.ForeColor = x.CAviso; lEstado.Font = x.FDes;
            tUso.Controls.Add(lAhora); tUso.Controls.Add(lAhora2); tUso.Controls.Add(lSel);
            tUso.Controls.Add(bUsar); tUso.Controls.Add(bProbar); tUso.Controls.Add(bAuto); tUso.Controls.Add(lInfo); tUso.Controls.Add(lEstado);

            string estado = null; bool estadoBien = false;
            int tarjetaEnUso = -1;
            foreach (RocolaOsSalida s in salidas) if (s.Clave == enUso) tarjetaEnUso = s.Tarjeta;
            Action pinta = delegate
            {
                RocolaOsSalida u = null;
                foreach (RocolaOsSalida s in salidas) if (s.Clave == enUso) u = s;
                lAhora.Text = "Ahora suena por: " + (u != null ? "la salida " + u.Numero + ", " + u.Tipo : enUso);
                lAhora2.Text = (u != null ? u.Clave + (u.Nombre.Length > 0 ? " · " + u.Nombre : "") + "\n" : "")
                    + (guardada != null ? "Elegida a mano." : "La automática" + (auto != null ? " (la salida " + auto.Numero + ")." : "."));
                for (int i = 0; i < filas.Count; i++)
                {
                    RocolaOsSalida s = salidas[i];
                    bool act = i == sel, usa = s.Clave == enUso;
                    filas[i].Text = dice(s) + (usa ? "  (suena ahora)" : "");
                    filas[i].Relleno = act ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                    filas[i].Borde = usa ? cUso : act ? Color.FromArgb(150, 205, 255) : Color.FromArgb(60, 72, 118);
                    filas[i].ForeColor = s.Hdmi ? cHdmi : cSal;
                    filas[i].Enabled = va;
                    filas[i].Invalidate();
                }
                RocolaOsSalida e = sel < salidas.Count ? salidas[sel] : null;
                lSel.Text = e != null ? "Elegida en la lista: la " + e.Numero + " (" + (e.Hdmi ? "HDMI" : e.Usb ? "USB" : "placa") + ")" : "";
                bool otra = e != null && e.Clave != enUso;
                bUsar.Text = e != null ? "Usar la salida " + e.Numero + " (prueba de " + RocolaOs.SalidaPruebaSegundos + " s)" : "Usar esta salida";
                bUsar.Enabled = va && otra;
                bool probable = va && e != null && e.Tarjeta != tarjetaEnUso && !RocolaOs.Probando;
                bProbar.Text = RocolaOs.Probando ? "Sonando el pitido..." : e != null ? "Probar un pitido por la " + e.Numero + " (3 s)" : "Probar un pitido (3 s)";
                bProbar.Enabled = probable;
                bAuto.Text = "Automática" + (auto != null ? " (la salida " + auto.Numero + ")" : "");
                bAuto.Enabled = va && guardada != null;
                string t = estado;
                if (t == null && !va) t = "No se puede: la hoja Sonido está apagada (" + RocolaOs.NombreSalida + "=0 en rocola_os.txt).";
                if (t == null && e != null && !probable && e.Tarjeta == tarjetaEnUso && otra) t = "Esa salida es de la misma tarjeta que suena: no se puede probar aparte (úsala y contesta «¿Se oye bien?»).";
                lEstado.ForeColor = estadoBien ? cUso : x.CAviso;
                lEstado.Text = t ?? "";
                bUsar.Invalidate(); bProbar.Invalidate(); bAuto.Invalidate();
            };
            for (int i = 0; i < filas.Count; i++) filas[i].Click += delegate { estado = null; estadoBien = false; pinta(); };

            // ELEGIR: lo guardado y, si cambia la que suena, a reabrir (la ventana se cierra primero)
            Action<string> elige = delegate (string clave)
            {
                string destino = clave == RocolaOs.SalidaAuto ? (auto != null ? auto.Clave : "default") : clave;
                string err;
                if (destino == enUso)
                {
                    if (RocolaOs.GuardaSalida(clave, out err)) { guardada = RocolaOs.SalidaGuardada(); estado = "Guardado (ya era la que suena: no hace falta volver a abrir)."; estadoBien = true; }
                    else { estado = "No se pudo guardar: " + err; estadoBien = false; }
                    pinta(); return;
                }
                if (!RocolaOs.EligeSalida(clave, out err)) { estado = "No se pudo guardar: " + err; estadoBien = false; pinta(); return; }
                estado = "Guardado: la rocola se vuelve a abrir con la salida nueva..."; estadoBien = true; pinta();
                dlg.Close();
                RocolaOs.ReabreConservando("otra salida de sonido (Ctrl+F4)");
            };
            bUsar.Click += delegate { if (sel < salidas.Count) elige(salidas[sel].Clave); };
            bAuto.Click += delegate { elige(RocolaOs.SalidaAuto); };
            bProbar.Click += delegate
            {
                if (sel >= salidas.Count) return;
                RocolaOsSalida e = salidas[sel];
                if (RocolaOs.ProbarSalida(e)) { estado = "Sonando 3 s por la salida " + e.Numero + " (" + e.Clave + "): escucha el amplificador."; estadoBien = true; }
                pinta();
            };
            pinta();

            RocolaOsPagina pag = new RocolaOsPagina();
            pag.Panel = pg; pag.Titulo = "Sonido";
            bool probabaAntes = false;
            pag.CadaMedioSegundo = delegate { bool p = RocolaOs.Probando; if (p != probabaAntes) { probabaAntes = p; pinta(); } };
            return pag;
        }

        // ---------- al volver: «¿Se oye bien?» ----------
        private bool rocolaOsSalidaPreguntando, rocolaOsSalidaPreguntada;
        internal void RocolaOsLatidoSalida()
        {
            if (RocolaOs.SalidaReabrirYa) { RocolaOs.SalidaReabrirYa = false; RocolaOs.ReabreConservando("la salida de sonido de antes (la nueva no se confirmo)"); return; }
            if (rocolaOsSalidaPreguntando || rocolaOsSalidaPreguntada || RocolaOs.Cerrandose) return;
            if (Environment.TickCount64 - RocolaOs.SalidaArrancoMs < RocolaOs.SalidaPreguntaTrasMs) return;
            RocolaOs.PruebaSalida p = RocolaOs.LeePruebaSalida();
            if (p == null) { rocolaOsSalidaPreguntada = true; return; }
            if (Hoja.Abiertas.Length > 0 || rocolaOsVentanaAbierta) return;   // (otra hoja abierta: en el siguiente latido)
            rocolaOsSalidaPreguntando = true;
            // (fuera del latido: una hoja modal dentro del latido lo dejaria parado 15 s para todas las funciones)
            Reloj r = new Reloj("RocolaOs:salida-pregunta", null); r.Interval = 10;
            r.Tick += delegate
            {
                r.Stop(); r.Dispose();
                try { RocolaOsPreguntaSalida(p); }
                catch (Exception ex) { RocolaOs.Falla(RocolaOs.NombreSalida, "«¿Se oye bien?»", ex); }
                finally { rocolaOsSalidaPreguntando = false; rocolaOsSalidaPreguntada = true; }
            };
            r.Start();
        }

        private void RocolaOsPreguntaSalida(RocolaOs.PruebaSalida p)
        {
            if (searchOpen) CloseSearch();
            bool wasMaint = maintMode;
            maintMode = true;   // (como la ventana de Ctrl+F4: la hoja modal al frente)
            bool bien = false;
            try
            {
                PantallaRocola.EnsenaFlecha();
                List<RocolaOsSalida> l = RocolaOs.SalidasDeSonido();
                string nueva = p.Nueva == RocolaOs.SalidaAuto ? "la automática" : p.Nueva;
                foreach (RocolaOsSalida s in l) if (s.Clave == p.Nueva) nueva = "la salida " + s.Numero + ", " + s.Tipo + " (" + s.Clave + ")";
                string ff = FamiliaUi();
                using (Rocola.Nativo.Letra fTit = Rocola.Nativo.Letra.EnPuntos(ff, 15f, true))
                using (Rocola.Nativo.Letra fSub = Rocola.Nativo.Letra.EnPuntos(ff, 9.5f))
                using (Rocola.Nativo.Letra fBot = Rocola.Nativo.Letra.EnPuntos(ff, 10f, true))
                using (Hoja q = new Hoja(MarcoXp.Dialogo))
                {
                    Color cFondo = Color.FromArgb(11, 14, 32);
                    q.Text = "Rocola OS - Sonido";
                    q.ClientSize = new Size(500, 220);
                    q.BackColor = cFondo;
                    q.Font = fSub;
                    Rotulo lT = new Rotulo(); lT.SetBounds(22, 14, 456, 28); lT.BackColor = Color.Transparent;
                    lT.ForeColor = Color.FromArgb(232, 238, 255); lT.Font = fTit; lT.Text = "¿Se oye bien?";
                    Rotulo lD = new Rotulo(); lD.SetBounds(24, 52, 456, 74); lD.BackColor = Color.Transparent;
                    lD.ForeColor = Color.FromArgb(138, 149, 182); lD.Font = fSub;
                    lD.Text = "La rocola suena ahora por " + nueva + ". Si no se oye o nadie contesta, vuelve sola a la de antes.";
                    int quedan = RocolaOs.SalidaPruebaSegundos;
                    BotonPlano bNo = new BotonPlano(); bNo.SetBounds(22, 156, 220, 40); bNo.Font = fBot; bNo.TabIndex = 0;
                    bNo.Text = "No (vuelve en " + quedan + " s)";
                    BotonPlano bSi = new BotonPlano(); bSi.SetBounds(258, 156, 220, 40); bSi.Font = fBot; bSi.TabIndex = 1;
                    bSi.Text = "Sí, se oye bien"; bSi.Relleno = Color.FromArgb(30, 96, 70); bSi.Borde = Color.FromArgb(72, 182, 132);
                    bNo.Click += delegate { bien = false; q.Close(); };
                    bSi.Click += delegate { bien = true; q.Close(); };
                    q.Controls.Add(bNo); q.Controls.Add(bSi); q.Controls.Add(lT); q.Controls.Add(lD);
                    q.TeclaAntes += delegate (PulsoTecla ke) { if (ke.KeyCode == Tecla.Escape) { bien = false; q.Close(); } };
                    MonedaTambienAqui(q);
                    long desde = Environment.TickCount64;
                    Reloj r = new Reloj("RocolaOs:salida-prueba", null); r.Interval = 1000;
                    r.Tick += delegate
                    {
                        // (por el reloj, como la prueba de la hoja Pantalla)
                        quedan = RocolaOs.SalidaPruebaSegundos - (int)((Environment.TickCount64 - desde) / 1000);
                        if (quedan <= 0) { r.Stop(); bien = false; RocolaOs.Apunta(RocolaOs.NombreSalida, "prueba de " + p.Nueva + ": nadie contesto en " + RocolaOs.SalidaPruebaSegundos + " s"); q.Close(); return; }
                        bNo.Text = "No (vuelve en " + quedan + " s)"; bNo.Invalidate();
                    };
                    r.Start();
                    RocolaOs.Apunta(RocolaOs.NombreSalida, "prueba de " + p.Nueva + ": «¿Se oye bien?»");
                    q.MuestraModal(this);
                    r.Stop(); r.Dispose();
                }
            }
            finally
            {
                try { PantallaRocola.EscondeFlecha(); } catch { }
                maintMode = wasMaint;
                ForceFocus();
            }
            if (RocolaOs.Cerrandose) return;   // (se cierra por otra cosa: la prueba sigue en el archivo y se pregunta al volver)
            if (bien)
            {
                RocolaOs.BorraPruebaSalida();
                RocolaOs.Apunta(RocolaOs.NombreSalida, "confirmada: " + p.Nueva + ", para siempre");
                return;
            }
            string err;
            if (!RocolaOs.GuardaSalida(p.Antes, out err)) { RocolaOs.Apunta(RocolaOs.NombreSalida, "no se pudo volver a " + p.Antes + ": " + err); return; }
            RocolaOs.BorraPruebaSalida();
            RocolaOs.Apunta(RocolaOs.NombreSalida, "sin confirmar " + p.Nueva + ": vuelve a " + p.Antes);
            RocolaOs.ReabreConservando("la salida de sonido de antes (la nueva no se confirmo)");
        }
    }
}
