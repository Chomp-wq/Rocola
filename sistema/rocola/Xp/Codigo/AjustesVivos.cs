// CTRL+F3: DEPENDENCIAS VIVAS Y GUARDADO AL MOMENTO (A1 y A2, 13-10-2026).
//
// Peticion del usuario: «Haz A1 y A2» (AJUSTES_CTRL_F3_REVISION_MODEST_2026-10-13.md).
//
// A1. Algunos interruptores solo hacen algo si otro esta encendido, y ese otro a veces esta en otra pagina. Antes se
//     podian tocar y no pasaba nada. Ahora el que no puede funcionar se pone en gris y dice que le falta
//     ("Necesita: ... (pagina)"), y vuelve solo en cuanto su requisito se enciende, este en la pagina que este. Su
//     valor no se toca: se guarda tal cual y vale en cuanto puede.
//     Sin tarjeta grafica, lo que solo se dibuja con ella va en gris y la pagina lo dice una vez (antes cada nombre
//     llevaba "(GPU)", y se podia tocar sin que pasara nada).
//
// A2. Lo tocado se guardaba al cerrar la ventana; si se iba la luz con ella abierta (en un bar pasa), se perdia.
//     Ahora se guarda en cuanto se deja quieto medio segundo, detras de la pantalla, por el mismo camino seguro
//     de siempre (EscribeSeguro). Al cerrar se guarda otra vez, como antes.
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Rocola
{
    public partial class Form1
    {
        // ------------------------------- A1: LAS DEPENDENCIAS -------------------------------
        private sealed class Requisito
        {
            public Control Mando;        // el que depende
            public Func<bool> Cumple;    // si su requisito esta encendido (lee el ajuste, no el interruptor: vale entre paginas)
            public string Necesita;      // "Necesita: ... (pagina)"
            public Label Aviso;          // si el mando no tiene sitio para decirlo, lo dice esta nota
            public string AvisoTexto;    // y lo que la nota dice cuando no falta nada
            public Color AvisoColor;
            public Func<string> Motivo;  // en vez de Cumple: lo que falta o lo que lo anula, al momento (null = vale)
        }
        // Se crean al primer uso (las paginas tambien se arman sueltas, en las pruebas, sin pasar por el constructor).
        private List<Requisito> f3Requisitos;
        private List<Control> f3SoloTarjeta;
        private List<Label> f3AvisoTarjeta;   // visibles solo sin tarjeta
        private List<Label> f3ConTarjeta;     // y sus contrapartes, solo con ella
        private void ListasDeRequisitos()
        {
            if (f3Requisitos == null) f3Requisitos = new List<Requisito>();
            if (f3SoloTarjeta == null) f3SoloTarjeta = new List<Control>();
            if (f3AvisoTarjeta == null) f3AvisoTarjeta = new List<Label>();
            if (f3ConTarjeta == null) f3ConTarjeta = new List<Label>();
        }

        // Ctrl+F3 se arma entera cada vez que se abre: lo apuntado de la vez anterior ya no existe.
        private void OlvidaRequisitos()
        {
            ListasDeRequisitos();
            f3Requisitos.Clear(); f3SoloTarjeta.Clear(); f3AvisoTarjeta.Clear(); f3ConTarjeta.Clear();
        }

        // 'mando' solo funciona si 'cumple'. Sin sitio debajo de su nombre (DosRenglones), lo dice detras del nombre o en
        // la nota 'aviso'.
        private void Necesita(Control mando, Func<bool> cumple, string que, Label aviso)
        {
            if (mando == null || cumple == null) return;
            ListasDeRequisitos();
            Requisito r = new Requisito();
            r.Mando = mando; r.Cumple = cumple; r.Necesita = "Necesita: " + que; r.Aviso = aviso;
            if (aviso != null) { r.AvisoTexto = aviso.Text; r.AvisoColor = aviso.ForeColor; }
            f3Requisitos.Add(r);
        }
        private void Necesita(Interruptor mando, Func<bool> cumple, string que)
        {
            if (mando == null) return;
            // Debajo del nombre, sin mover el nombre: el mando crece hacia abajo y el nombre queda en su renglon.
            mando.DosRenglones = true;
            if (mando.Height < Interruptor.AltoDosRenglones) mando.Height = Interruptor.AltoDosRenglones;
            Necesita(mando, cumple, que, null);
        }

        // Como Necesita, pero el motivo se dice al momento y puede ser mas de uno: "Necesita: ..." o "Sin efecto: ...", o null
        // si vale (14-10-2026: los ahorros de Performance, que dependen de ajustes de otras paginas y se anulan entre ellos).
        private void NecesitaMotivo(Interruptor mando, Func<string> motivo)
        {
            if (mando == null || motivo == null) return;
            mando.DosRenglones = true;
            if (mando.Height < Interruptor.AltoDosRenglones) mando.Height = Interruptor.AltoDosRenglones;
            ListasDeRequisitos();
            Requisito r = new Requisito();
            r.Mando = mando; r.Motivo = motivo; r.Cumple = delegate { return true; }; r.Necesita = "";
            f3Requisitos.Add(r);
        }

        // Lo mismo, pero sin sitio debajo del nombre: el motivo (si lo hay) lo dice la nota 'aviso' del mando, como en Necesita(...,
        // aviso), y vuelve a su texto cuando ya nada falta (23-10-2026: "a la medida" de la IA, que necesita el modelo del EQ).
        private void NecesitaMotivo(Interruptor mando, Func<string> motivo, Label aviso)
        {
            if (mando == null || motivo == null) return;
            ListasDeRequisitos();
            Requisito r = new Requisito();
            r.Mando = mando; r.Motivo = motivo; r.Cumple = delegate { return true; }; r.Necesita = ""; r.Aviso = aviso;
            if (aviso != null) { r.AvisoTexto = aviso.Text; r.AvisoColor = aviso.ForeColor; }
            f3Requisitos.Add(r);
        }

        // Lo que solo se ve con tarjeta grafica.
        private void SoloConTarjeta(params Control[] mandos)
        {
            ListasDeRequisitos();
            foreach (Control c in mandos) if (c != null && !f3SoloTarjeta.Contains(c)) f3SoloTarjeta.Add(c);
        }

        // Una nota por pagina: sin tarjeta se ve 'sin'; con ella, 'con' (si la hay).
        private void AvisoSinTarjeta(Label sin, Label con)
        {
            ListasDeRequisitos();
            if (sin != null) f3AvisoTarjeta.Add(sin);
            if (con != null) f3ConTarjeta.Add(con);
        }

        internal const string TextoSinTarjeta = "Esta rocola dibuja ahora SIN tarjeta grafica: lo que esta en gris\n"
                                              + "en esta pagina solo se ve con ella.";

        // Si ahora mismo se dibuja con tarjeta grafica.
        private bool HayTarjeta()
        {
#if ROCOLA_GPU
            return gpuEnabled;
#else
            return false;
#endif
        }

        // Pone cada mando como toca. Se llama al abrir y despues de cualquier cambio en la ventana.
        private void RefrescaRequisitos()
        {
            ListasDeRequisitos();
            bool tarjeta = HayTarjeta();
            foreach (Control c in f3SoloTarjeta) c.Enabled = tarjeta;
            foreach (Requisito r in f3Requisitos)
            {
                bool ok = true;
                if (r.Motivo != null)
                {
                    string m = null;
                    try { m = r.Motivo(); } catch { }
                    ok = m == null;
                    r.Necesita = m ?? "";
                }
                else try { ok = r.Cumple(); } catch { }
                bool vale = ok && (tarjeta || !f3SoloTarjeta.Contains(r.Mando));
                r.Mando.Enabled = vale;
                // Sin tarjeta lo dice la pagina, no cada mando: el requisito no es lo que falta.
                string falta = (!ok && (tarjeta || !f3SoloTarjeta.Contains(r.Mando))) ? r.Necesita : null;
                Interruptor sw = r.Mando as Interruptor;
                if (r.Aviso != null)
                {
                    string t = falta ?? r.AvisoTexto;
                    if (r.Aviso.Text != t) r.Aviso.Text = t;
                    r.Aviso.ForeColor = falta != null ? Interruptor.ColorNecesita : r.AvisoColor;
                    if (sw != null) sw.PonNecesita(null);
                }
                else if (sw != null) sw.PonNecesita(falta);
            }
            foreach (Label l in f3AvisoTarjeta) l.Visible = !tarjeta;
            foreach (Label l in f3ConTarjeta) l.Visible = tarjeta;
        }

        // Despues de CUALQUIER cambio en la ventana se repasan las dependencias. Se engancha al final, cuando cada mando ya
        // tiene el suyo: los manejadores van en orden, asi que el ajuste ya esta puesto cuando se repasa.
        private void EnganchaRequisitos(Control raiz)
        {
            foreach (Control c in raiz.Controls)
            {
                Interruptor sw = c as Interruptor;
                if (sw != null) sw.CheckedChanged += delegate { RefrescaRequisitos(); };
                else if (c is BotonPlano) c.Click += delegate { RefrescaRequisitos(); };
                if (c.Controls.Count > 0) EnganchaRequisitos(c);
            }
        }

        // ------------------------------- A2: GUARDAR AL MOMENTO -------------------------------
        // Cada guardado lleva su turno. El que llega tarde (uno de detras que se adelanto otro mas nuevo, como el de
        // cerrar la ventana) no pisa al nuevo: se salta.
        // Estaticos: el candado existe aunque el formulario se arme sin constructor (pruebas), y hay un solo archivo.
        private static readonly object configEscribe = new object();
        private static long configTurno, configEscrito;
        private volatile string f3Guardado;  // confirmado por el escritor, nunca solo por haberlo encolado
        private string f3Visto;
        private int f3Guardando;             // como mucho un trabajo pendiente por ventana
        private volatile bool f3Reintenta;
        private int f3FalloTick;
        internal const int ConfigReintentoMs = 2000;

        private void EscribeConfigEnTurno(string texto, long turno)
        {
            lock (configEscribe)
            {
                if (turno <= configEscrito) return;
                EscribeSeguro(ConfigFile, texto);
                configEscrito = turno;
                f3Guardado = texto;   // tambien si lo guardo el cierre o SaveConfigPrefs, no solo el latido
            }
        }

        // En la primera apertura se parte de las preferencias cargadas. Reabrir no debe olvidar un fallo ni un
        // trabajo pendiente: se conserva lo confirmado por el escritor (incluidos los guardados al cerrar).
        private void EmpiezaGuardadoAlMomento()
        {
            try
            {
                f3Visto = TextoConfig();
                if (f3Guardado == null && !f3Reintenta) f3Guardado = f3Visto;
            }
            catch { f3Visto = null; }
        }

        // Cada medio segundo con la ventana abierta. Si los ajustes cambiaron y llevan medio segundo quietos (una barra que
        // se arrastra no escribe en cada paso), se guardan detras de la pantalla. Devuelve si mando guardar.
        private bool GuardaAlMomento()
        {
            string t;
            try { t = TextoConfig(); } catch { return false; }
            if (t == f3Guardado) { f3Visto = t; return false; }
            if (t != f3Visto) { f3Visto = t; return false; }   // aun se esta moviendo
            // Un fallo transitorio no confirma el texto. Reintentar desde el latido, con pausa y sin llenar la cola
            // ni dormir al escritor compartido con los otros datos de la Rocola.
            if (f3Reintenta && unchecked(Environment.TickCount - f3FalloTick) < ConfigReintentoMs) return false;
            if (System.Threading.Interlocked.CompareExchange(ref f3Guardando, 1, 0) != 0) return false;
            long turno = System.Threading.Interlocked.Increment(ref configTurno);
            GuardaDetras(delegate
            {
                try
                {
                    EscribeConfigEnTurno(t, turno);   // un turno antiguo se salta sin confirmar su texto
                    f3Reintenta = false;
                }
                catch { f3FalloTick = Environment.TickCount; f3Reintenta = true; }
                finally { System.Threading.Interlocked.Exchange(ref f3Guardando, 0); }
            });
            return true;
        }
    }
}
