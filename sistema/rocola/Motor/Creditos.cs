// LAS MONEDAS, LOS CREDITOS Y EL ESTADO (en XP, Form1.Ajustes.cs: AddCoin y AddItem; Form1.Datos.cs: SaveState, RestoreState y las
// estadisticas). Las reglas son las de XP: una moneda son 4 creditos; cada cancion cuesta uno; no se pone la misma dos veces
// seguidas ni una que ya fallo por archivo danado; los creditos y la cola se guardan en rocola_estado.txt con cada cambio (si se va
// la luz, el dinero del cliente no se pierde) y se restauran al arrancar dentro del mismo dia del negocio (que empieza a las 7).
// Lo que hace la pantalla de XP (la pastilla de los creditos, el aviso, la portada que vuela...) aqui es un aviso (Avisa).
using System;
using System.Collections.Generic;
using System.IO;

namespace Rocola
{
    public partial class Form1
    {
        private int credits;
        public int Creditos { get { return credits; } }
        public event Action<string> Aviso;   // lo que en XP sale en pantalla ("Inserta una moneda", "Reanudando la musica"...)
        private void Avisa(string texto) { Action<string> a = Aviso; if (a != null) try { a(texto); } catch { } }
        private bool pideEmpezar;            // XP llamaria a PlayNext: quien toca lo mira (EmpezarYa)

        // Lo pidio XP (PlayNext) o no suena nada y hay cola: hay que empezar la siguiente.
        public bool EmpezarYa { get { bool p = pideEmpezar; pideEmpezar = false; return p || (nowPlaying == null && queue.Count > 0); } }

        // LA MONEDA (AddCoin de XP, sin la pantalla).
        public void Moneda()
        {
            credits += CreditsPerCoin;
            statsCoins++; statsCreditsAdded += CreditsPerCoin; statsDirty = true;   // estadisticas
            UpdateCredits();
            SaveState();   // estado vivo: si se corta la luz, el dinero del cliente no se pierde
            // Monedas rapidas seguidas: el aviso SUMA (+4, +8, +12...) mientras el anterior siga a la vista (AddCoin de XP)
            coinTotal = unchecked(Environment.TickCount - coinTick) < AvisoVidaMs ? coinTotal + CreditsPerCoin : CreditsPerCoin;
            coinTick = Environment.TickCount;
            Avisa("+" + coinTotal + " creditos");
        }
        private int coinTotal, coinTick = Environment.TickCount - 100000;
        // Lo que dura un aviso en pantalla (VidaAviso de XP): 2,8 s el aviso claro, 1,3 s el de siempre.
        public int AvisoVidaMs { get { return AvisosClaros ? 2800 : 1300; } }

        public enum Pedido { Puesta, SinCredito, Repetida, Danada }
        public string UltimoTurno = "";   // "Tu cancion es la 3a - en ~7 min" (vacio: sin turno, como con turno=0 en XP)
        private double RestanDeLaQueSuena()
        {
            if (nowPlaying == null) return 0;
            if (nowPlaying.IsVideo) return -1;
            CadenaCancion c = cadenaSonando;
            try { if (c != null) { double q = c.Lector.TotalTime.TotalSeconds - c.Lector.CurrentTime.TotalSeconds; return q > 0 ? q : 0; } } catch { }
            return -1;
        }

        // PEDIR UNA CANCION (AddItem de XP, sin la pantalla).
        public Pedido Pide(string ruta)
        {
            PlayItem it = new PlayItem();
            if (EsVideo(ruta)) { it.IsVideo = true; it.VideoPath = ruta; }   // un video (la seccion de videos de XP)
            else
            {
                Track t = null; Album d = null;
                if (pistaDeRuta != null) { pistaDeRuta.TryGetValue(ruta, out t); discoDeRuta.TryGetValue(ruta, out d); }
                if (t == null) { t = new Track(); t.Path = ruta; }
                it.Song = t; it.Disc = d;
                if (EstaDanada(it.Song.Path)) return Pedido.Danada;
            }
            if (credits <= 0) { Avisa("Inserta una moneda"); return Pedido.SinCredito; }
            PlayItem last = (queue.Count > 0) ? queue[queue.Count - 1] : nowPlaying;
            if (last != null && string.Equals(last.PathKey(), it.PathKey(), StringComparison.OrdinalIgnoreCase)) return Pedido.Repetida;
            queue.Add(it);
            // EL TURNO (como XP: se apunta aqui, con la cola como quedo al meterla): el puesto y lo que falta (lo que le queda a la que
            // suena y lo que duran las de delante; si de alguna no se sabe, solo el puesto).
            {
                double[] antes = new double[queue.Count - 1];
                for (int i = 0; i < queue.Count - 1; i++) antes[i] = queue[i].IsVideo ? -1 : SegundosDe(queue[i].PathKey());
                UltimoTurno = TextoTurno(queue.Count, EsperaSegundos(antes, RestanDeLaQueSuena()));
            }
            if (!it.IsVideo) try { it.Song.EnsureTags(); } catch { }   // el genero (la etiqueta ID3), para lo mas sonado por genero (como XP al empezarla)
            RecordPlay(it);   // estadisticas: cuenta lo pedido
            credits--;        // cada cosa cuesta 1 credito
            UpdateCredits();
            if (nowPlaying == null) PlayNext();   // nada sonando -> arranca
            else SaveState();
            return Pedido.Puesta;
        }

        // Lo que en XP hace la pantalla al cambiar los creditos, al empezar una cancion o al mostrar un aviso.
        // En XP solo cambia la capsula de creditos (sin aviso): la pantalla la lee de Creditos.
        private void UpdateCredits() { }
        private void PlayNext() { pideEmpezar = true; }
        private void ShowToast(string text) { Avisa(text); }
        private void RefrescaCola() { }
        private void UpdateNowPlayingUI() { }
        private void PrecalientaSiguiente() { }

        // Restaurar los creditos y la cola de antes del apagon (RestoreState de XP), con la biblioteca ya leida.
        public void Reanuda() { RestoreState(); }
        // EL CIERRE DELIBERADO (el boton de encendido; en XP, OnFormClosing con WindowsShutDown): se borra el estado del dia, asi manana
        // arranca limpia. Un apagon no pasa por aqui: el archivo sobrevive y al volver la luz se reanuda.
        public void CierreDelDia() { BorraEstado(); }
        // Guardar el estado (al empezar o acabar una cancion: lo que suena y la cola cambiaron).
        public void GuardaEstado() { SaveState(); }
        // Al cerrar sin el boton de encendido: lo guardado ya no cambia (lo que se cierra despues no es «se acabo la cancion»).
        public void CongelaEstado() { estadoCongelado = true; }
        private volatile bool estadoCongelado;

        // ---- LO QUE EN XP HACEN LOS RELOJES DE LA VENTANA ----
        private DateTime lastInput = DateTime.Now;   // el ultimo boton (el reinicio de las 7 espera a que la rocola este ociosa)
        private int ultimoPeriodico;
        // Un boton (cualquiera): la rocola esta en uso.
        public void HuboBoton() { lastInput = DateTime.Now; }
        // Llamarlo a menudo desde el hilo de siempre: una vez por minuto, el reinicio de las 7 (CheckBusinessDayReset de XP: creditos a
        // cero y respaldo de lo aprendido, solo si esta ociosa) y lo aprendido al disco (como StatsTimer_Tick y el repaso de XP).
        public void Periodico()
        {
            int ahora = Environment.TickCount;
            if (unchecked(ahora - ultimoPeriodico) < 60000) return;
            ultimoPeriodico = ahora;
            try { MiraSaltoDeHora(); } catch { }
            try { CheckBusinessDayReset(); } catch { }
            try { if (statsDirty) { statsDirty = false; SaveStats(true); } } catch { }
            try { SaveLoudCache(); FlushEqIfDirty(); } catch { }
            try { GuardaNoche(true); } catch { }   // lo mas pedido esta noche, si cambio
            try { MiraEntreno(); } catch { }        // IA1: entrenar la IA en reposo (IaNueva.cs)
        }

        // LA HORA QUE SALTA (4-11-2026). Al prender, la hora viene del reloj de la placa; poco despues la pone la red (NTP, rocola-actualiza).
        // Si el reloj de la placa iba mal (la pila gastada: puede arrancar en otro ano), la hora salta de golpe y la rocola creia que habia
        // cambiado el dia del negocio: a los 15 min sin botones borraba los creditos y el estado que quedaron de un apagon. Un salto se
        // reconoce porque la hora de pared avanza distinto que el reloj del equipo (que no salta); entonces el dia del negocio se rehace
        // con la hora nueva (un salto no es «llegaron las 7») y lo de «hace cuanto que nadie toca» se corre lo mismo.
        private DateTime paredAntes; private long relojAntes;
        private void MiraSaltoDeHora()
        {
            DateTime pared = DateTime.Now; long reloj = Environment.TickCount64;
            if (relojAntes != 0)
            {
                double salto = (pared - paredAntes).TotalMilliseconds - (reloj - relojAntes);
                if (Math.Abs(salto) > SaltoDeHoraMs)
                {
                    stateDay = BusinessDay(pared);
                    try { lastInput = lastInput.AddMilliseconds(salto); } catch { }
                    LogError("La hora salto " + Math.Round(salto / 60000.0) + " min (" + paredAntes.ToString("yyyy-MM-dd HH:mm") + " -> " + pared.ToString("yyyy-MM-dd HH:mm") + "): no cuenta como dia nuevo", null);
                }
            }
            paredAntes = pared; relojAntes = reloj;
        }
        internal const int SaltoDeHoraMs = 120000;

        // SALTAR con todo parado y la cola vacia (SkipSong de XP): borra los creditos que quedaron. Devuelve si habia.
        public bool BorraCreditos()
        {
            if (nowPlaying != null || queue.Count > 0 || credits <= 0) return false;
            credits = 0; UpdateCredits(); SaveState();
            return true;
        }

        // LOS CREDITOS DE SERVICIO (CreditosServicio.cs de XP): el codigo del dueno escrito en el buscador y mantenido 5 s da 4 creditos; no
        // cuentan como monedas ni ingresos en las estadisticas.
        public static bool EsCodigoDeServicio(string escrito) { return SecretCreditGate.Matches(escrito); }
        public void CreditosDeServicio()
        {
            if (credits > int.MaxValue - 4) return;
            credits += 4;
            UpdateCredits();
            SaveState();   // misma persistencia inmediata que los creditos normales
        }
    }
    // de CreditosServicio.cs (tal cual)
    // Acceso local oculto, no autenticacion: quien conozca el texto puede utilizarlo.
    // El codigo de servicio (los 5 s mantenido los cuenta el Controlador: ActualizaCodigo)
    internal static class SecretCreditGate
    {
        internal static bool Matches(string text) { return string.Equals(text, "CHOMPS21", StringComparison.OrdinalIgnoreCase); }
    }
}
