// LAS MONEDAS, LOS CREDITOS Y EL ESTADO (en XP, Form1.Ajustes.cs: AddCoin y AddItem; Form1.Datos.cs: SaveState, RestoreState y las
// estadisticas). Las reglas son las de XP: una moneda son 4 creditos; cada cancion cuesta uno; no se pone la misma dos veces
// seguidas ni una que ya fallo por archivo danado; los creditos y la cola se guardan en rocola_estado.txt con cada cambio (si se va
// la luz, el dinero del cliente no se pierde) y se restauran al arrancar dentro del mismo dia del negocio (que empieza a las 7).
// Lo que hace la pantalla de XP (la pastilla de los creditos, el aviso, la portada que vuela...) aqui es un aviso (Avisa).
using System;
using System.Collections.Generic;

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
        }

        public enum Pedido { Puesta, SinCredito, Repetida, Danada }

        // PEDIR UNA CANCION (AddItem de XP, sin la pantalla).
        public Pedido Pide(string ruta)
        {
            PlayItem it = new PlayItem();
            Track t = null; Album d = null;
            if (pistaDeRuta != null) { pistaDeRuta.TryGetValue(ruta, out t); discoDeRuta.TryGetValue(ruta, out d); }
            if (t == null) { t = new Track(); t.Path = ruta; }
            it.Song = t; it.Disc = d;
            if (EstaDanada(it.Song.Path)) return Pedido.Danada;
            if (credits <= 0) { Avisa("Inserta una moneda"); return Pedido.SinCredito; }
            PlayItem last = (queue.Count > 0) ? queue[queue.Count - 1] : nowPlaying;
            if (last != null && string.Equals(last.PathKey(), it.PathKey(), StringComparison.OrdinalIgnoreCase)) return Pedido.Repetida;
            queue.Add(it);
            RecordPlay(it);   // estadisticas: cuenta lo pedido
            credits--;        // cada cosa cuesta 1 credito
            UpdateCredits();
            if (nowPlaying == null) PlayNext();   // nada sonando -> arranca
            else SaveState();
            return Pedido.Puesta;
        }

        // Lo que en XP hace la pantalla al cambiar los creditos, al empezar una cancion o al mostrar un aviso.
        private void UpdateCredits() { Avisa(credits <= 0 ? "Inserta una moneda" : "Creditos: " + credits); }
        private void PlayNext() { pideEmpezar = true; }
        private void ShowToast(string text) { Avisa(text); }
        private void RefrescaCola() { }
        private void UpdateNowPlayingUI() { }
        private void PrecalientaSiguiente() { }

        // Restaurar los creditos y la cola de antes del apagon (RestoreState de XP), con la biblioteca ya leida.
        public void Reanuda() { RestoreState(); }
        // Guardar el estado (al empezar o acabar una cancion: lo que suena y la cola cambiaron).
        public void GuardaEstado() { SaveState(); }
    }
}
