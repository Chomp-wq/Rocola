// ROCOLA OS (fase 11): LA VENTANA DE ROCOLA OS CON CTRL+F4 (decision 2 del plan de la fase 11, PLAN_FASE11.md §6: «una ventana propia con
// Ctrl+F4, como Ctrl+F2»; FASE11_pantalla.md). Con EL MISMO aspecto y manejo que las hojas de Ctrl+F3 de XP (Codigo/Form1.Ajustes.cs,
// VentanaAjustes): la hoja nativa (Nativo/Hoja.cs: el marco de XP, el foco, el tabulador, el raton, Intro y Escape), de 960x732 como Ctrl+F3
// (con su barra cabe en 1024x768), el mismo fondo, las mismas letras, el titulo grande a la izquierda y las pestanas a la derecha en su
// renglon, las mismas tarjetas, deslizadores, interruptores y botones planos (las clases de XP: Tarjeta, Deslizador, Interruptor,
// BotonPlano), «Listo» verde abajo a la derecha, Escape cierra, la moneda cuenta con la ventana abierta (MonedaTambienAqui) y, como las
// modales de XP, pone maintMode mientras esta abierta (con ella la rocola no esta «libre»).
//
// SE ABRE CON LAS MISMAS REGLAS QUE CTRL+F2 Y CTRL+F3: el gancho g85 va en ProcessCmdKey (Codigo/Form1.cs) justo delante de Ctrl+F2, asi que
// lo que impide abrir aquellas (las tablas del arranque aun leyendose, la atraccion -la tecla solo despierta-, el asistente de botones,
// el video a pantalla completa, el asistente estrella) impide abrir esta. Sin ROCOLA_OS, Ctrl+F4 no hace nada (como en XP).
//
// LAS HOJITAS (pestanas): por ahora una, «Pantalla» (RocolaOs/RocolaOs.VentanaPantalla.cs). Cada grupo que venga anade la suya en su
// archivo con RocolaOsHojitas.Registra (en su [ModuleInitializer], como las funciones): un nombre, un orden y el metodo que arma su pagina
// (un Grupo de 960x584 bajo el renglon del titulo) y, si quiere, lo que hace cada medio segundo y al cerrarse la ventana.
using System;
using System.Collections.Generic;
using System.Drawing;
using Rocola.Nativo;

namespace Rocola
{
    // UNA PAGINA YA ARMADA de la ventana de Rocola OS
    internal sealed class RocolaOsPagina
    {
        internal Grupo Panel;              // la pagina (960 de ancho; empieza en y = 96 y acaba antes del pie)
        internal string Titulo;            // el titulo grande y el de la barra («Rocola OS - <Titulo>»)
        internal Action CadaMedioSegundo;  // con la ventana abierta (un reloj de la pantalla con nombre fijo: RocolaOs:ventana)
        internal Action AlCerrar;          // la ventana se cerro (con Listo, Escape, la «X» o el cierre de la rocola)
    }
    // LO QUE UNA PAGINA NECESITA PARA PARECERSE A CTRL+F3: las letras y los colores de VentanaAjustes, y la hoja
    internal sealed class RocolaOsVentanaCtx
    {
        internal Hoja Dlg;
        internal Letra FTit, FSub, FNom, FVal, FDes, FBot, FInt;
        internal Color CTexto = Color.FromArgb(222, 230, 250), CTenue = Color.FromArgb(138, 149, 182), CApagado = Color.FromArgb(96, 104, 128),
            CFondo = Color.FromArgb(11, 14, 32), CAviso = Color.FromArgb(255, 196, 96);
    }
    internal sealed class RocolaOsHojita
    {
        internal string Nombre;    // el de la pestana
        internal int Orden;        // de izquierda a derecha
        internal Func<Form1, RocolaOsVentanaCtx, RocolaOsPagina> Arma;
    }
    internal static class RocolaOsHojitas
    {
        private static readonly List<RocolaOsHojita> lista = new List<RocolaOsHojita>();
        internal static void Registra(RocolaOsHojita h) { lock (lista) { if (h != null && h.Arma != null) lista.Add(h); } }
        internal static RocolaOsHojita[] Todas
        {
            get { lock (lista) { List<RocolaOsHojita> l = new List<RocolaOsHojita>(lista); l.Sort((a, b) => a.Orden.CompareTo(b.Orden)); return l.ToArray(); } }
        }
    }

    public partial class Form1
    {
        private bool rocolaOsVentanaAbierta;
        private int rocolaOsPagina;   // la pestana que se ve (vuelve a la primera al cerrar, como f3Pagina)

        // GANCHO g85 (Codigo/Form1.cs, ProcessCmdKey): Ctrl+F4 abre la ventana de Rocola OS. Sin ROCOLA_OS, false (y la tecla sigue su camino
        // de XP, que no la usa).
        internal bool RocolaOsCtrlF4(Tecla keyData)
        {
            if (keyData != (Tecla.Control | Tecla.F4) || !RocolaOs.Activo) return false;
            RocolaOsVentana();
            return true;
        }

        private void RocolaOsVentana()
        {
            // (como ShowVolumeManager: un atajo reenviado no anida otra ventana)
            if (rocolaOsVentanaAbierta || IsDisposed || Disposing) return;
            rocolaOsVentanaAbierta = true;
            if (searchOpen) CloseSearch();
            bool wasMaint = maintMode;
            maintMode = true;   // el dialogo modal necesita quedarse al frente; se devuelve en el finally (como VentanaAjustes)
            List<RocolaOsPagina> paginas = new List<RocolaOsPagina>();
            try
            {
                PantallaRocola.EnsenaFlecha();
                string ff = FamiliaUi();
                RocolaOsVentanaCtx x = new RocolaOsVentanaCtx();
                using (Rocola.Nativo.Letra fTit = Rocola.Nativo.Letra.EnPuntos(ff, 15f, true))
                using (Rocola.Nativo.Letra fSub = Rocola.Nativo.Letra.EnPuntos(ff, 9.5f))
                using (Rocola.Nativo.Letra fNom = Rocola.Nativo.Letra.EnPuntos(ff, 10f, true))
                using (Rocola.Nativo.Letra fVal = Rocola.Nativo.Letra.EnPuntos(ff, 11f, true))
                using (Rocola.Nativo.Letra fDes = Rocola.Nativo.Letra.EnPuntos(ff, 8.5f))
                using (Rocola.Nativo.Letra fBot = Rocola.Nativo.Letra.EnPuntos(ff, 10f, true))
                using (Rocola.Nativo.Letra fInt = Rocola.Nativo.Letra.EnPuntos(ff, 9.5f, true))
                using (Hoja dlg = new Hoja(MarcoXp.Dialogo))
                {
                    x.Dlg = dlg; x.FTit = fTit; x.FSub = fSub; x.FNom = fNom; x.FVal = fVal; x.FDes = fDes; x.FBot = fBot; x.FInt = fInt;
                    dlg.Text = "Rocola OS";
                    dlg.ClientSize = new Size(960, 732);   // como Ctrl+F3: con su barra de titulo cabe en 1024x768
                    dlg.BackColor = x.CFondo;
                    dlg.Font = fSub;

                    Rotulo lTit = new Rotulo(); lTit.SetBounds(22, 10, 284, 28); lTit.BackColor = Color.Transparent;
                    lTit.ForeColor = Color.FromArgb(232, 238, 255); lTit.Font = fTit;
                    Rotulo lSub = new Rotulo(); lSub.SetBounds(24, 42, 600, 18); lSub.BackColor = Color.Transparent;
                    lSub.ForeColor = x.CTenue; lSub.Font = fSub;
                    lSub.Text = "Lo propio de Rocola OS. Ctrl+F3 sigue siendo el de siempre.";
                    BotonPlano bOk = new BotonPlano(); bOk.SetBounds(804, 680, 140, 38); bOk.Font = fBot;
                    bOk.Text = "Listo"; bOk.Relleno = Color.FromArgb(30, 96, 70); bOk.Borde = Color.FromArgb(72, 182, 132);
                    bOk.Click += delegate { dlg.Close(); };

                    // las paginas y sus pestanas, a la derecha del renglon del titulo y terminando donde las de Ctrl+F3 (en el 942), de 100
                    RocolaOsHojita[] hs = RocolaOsHojitas.Todas;
                    List<BotonPlano> pes = new List<BotonPlano>();
                    foreach (RocolaOsHojita h in hs)
                    {
                        RocolaOsPagina p = null;
                        try { p = h.Arma(this, x); }
                        catch (Exception ex) { RocolaOs.Falla("ventana", "la hojita " + h.Nombre, ex); }
                        if (p == null || p.Panel == null) continue;
                        if (p.Titulo == null) p.Titulo = h.Nombre;
                        paginas.Add(p);
                        BotonPlano b = new BotonPlano(); b.Font = fBot; b.Text = h.Nombre;
                        pes.Add(b);
                    }
                    // (28-11-2026) con siete pestanas (Cadena y Billar de la cadena de sonido nueva) la primera quedaba bajo el titulo: con mas de seis, mas
                    // angostas para que todas quepan del 312 al 942 (con seis o menos, como siempre: de 100 cada 106)
                    int pasoPes = pes.Count > 6 ? Math.Min(106, 630 / pes.Count) : 106;
                    for (int i = 0; i < pes.Count; i++) pes[i].SetBounds(942 - (pes.Count - i) * pasoPes + 6, 8, pasoPes - 6, 30);
                    if (rocolaOsPagina < 0 || rocolaOsPagina >= paginas.Count) rocolaOsPagina = 0;
                    Action pintaPag = delegate
                    {
                        for (int k = 0; k < paginas.Count; k++)
                        {
                            bool act = k == rocolaOsPagina;
                            paginas[k].Panel.Visible = act;
                            pes[k].Relleno = act ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                            pes[k].Borde = act ? Color.FromArgb(150, 205, 255) : Color.FromArgb(60, 72, 118);
                            pes[k].ForeColor = act ? Color.FromArgb(226, 240, 255) : x.CApagado;
                            pes[k].Invalidate();
                        }
                        string t = paginas.Count > 0 ? paginas[rocolaOsPagina].Titulo : "Rocola OS";
                        lTit.Text = t;
                        dlg.Text = "Rocola OS - " + t;
                    };
                    for (int k = 0; k < pes.Count; k++) { int kk = k; pes[k].Click += delegate { rocolaOsPagina = kk; pintaPag(); }; }

                    // el orden de Ctrl+F3: el titulo, las pestanas, el subtitulo, el pie (Listo: encima de las paginas) y las paginas
                    dlg.Controls.Add(lTit);
                    foreach (BotonPlano b in pes) dlg.Controls.Add(b);
                    dlg.Controls.Add(lSub);
                    dlg.Controls.Add(bOk);
                    foreach (RocolaOsPagina p in paginas) dlg.Controls.Add(p.Panel);
                    pintaPag();
                    // Escape cierra, como en Ctrl+F3 (la hoja mira las teclas antes que las piezas: KeyPreview)
                    dlg.TeclaAntes += delegate (PulsoTecla ke) { if (ke.KeyCode == Tecla.Escape) dlg.Close(); };
                    MonedaTambienAqui(dlg);   // y una moneda con la ventana abierta tambien cuenta
                    Reloj tic = new Reloj("RocolaOs:ventana", null);
                    tic.Interval = 500;
                    tic.Tick += delegate
                    {
                        foreach (RocolaOsPagina p in paginas)
                            if (p.CadaMedioSegundo != null) try { p.CadaMedioSegundo(); } catch (Exception ex) { RocolaOs.Falla("ventana", "cada medio segundo", ex); }
                    };
                    tic.Start();
                    RocolaOs.Apunta("ventana", "Ctrl+F4 abierta");
                    dlg.MuestraModal(this);
                    tic.Stop(); tic.Dispose();
                }
            }
            catch (Exception ex) { RocolaOs.Falla("ventana", "Ctrl+F4", ex); }
            finally
            {
                foreach (RocolaOsPagina p in paginas)
                    if (p.AlCerrar != null) try { p.AlCerrar(); } catch (Exception ex) { RocolaOs.Falla("ventana", "al cerrar", ex); }
                try { PantallaRocola.EscondeFlecha(); } catch { }
                maintMode = wasMaint;
                rocolaOsVentanaAbierta = false;
                rocolaOsPagina = 0;
                ForceFocus();
            }
        }
    }
}
