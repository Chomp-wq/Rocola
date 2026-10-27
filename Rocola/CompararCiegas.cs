// COMPARAR A CIEGAS (27 de octubre de 2026, quinta tanda del modo billar). Para decidir con el oido, en el billar, si el modo billar suena
// mejor que la cadena de fabrica: Ctrl+F3 -> Sonido -> Ajustes del sonido -> Lo que suena ahora -> «Comparar a ciegas». Durante 2 minutos la
// cancion que suena cambia cada 15 s entre el modo billar y la cadena de fabrica, en un orden al azar (cuatro tramos de cada uno), sin
// decir cual es. El dueno toca «Me gusta esta» cuando le suena mejor (un voto por tramo) y al final se dice cual eligio mas veces.
//
// Se cambia de verdad el modo (localAuto) y se aplica al instante como el interruptor (las bocinas, el EQ, el bajo y lo demas); lo que se
// fija al armar la cancion (el corte del subgrave a 40 Hz, la compensacion de la presencia) sigue como estaba en esa cancion. Cada modo suena
// a su volumen de verdad: el del billar lleva su volumen parejo. No se guarda nada: la configuracion sigue diciendo lo de antes, y al acabar,
// al cambiar de cancion, al tocar un ajuste o al cerrar la ventana vuelve el modo como estaba. Solo con el modo billar encendido.
using System;

namespace Rocola
{
    public partial class Form1
    {
        internal const int CiegoTramos = 8, CiegoTramoSeg = 15;
        private bool ciegoActivo, ciegoGuardado;
        private bool[] ciegoOrden, ciegoVotado;   // true: el tramo es del modo billar / ya votado (se arman al empezar)
        private int ciegoVotosBillar, ciegoVotosFabrica, ciegoTramo = -1;
        private DateTime ciegoInicio;
        private PlayItem ciegoCancion;
        private string ciegoResultado;

        // El orden: la mitad de los tramos de cada uno, barajados. Pura salvo el azar que se le pasa.
        internal static bool[] OrdenCiego(Random r)
        {
            bool[] o = new bool[CiegoTramos];
            for (int i = 0; i < CiegoTramos; i++) o[i] = i < CiegoTramos / 2;
            for (int i = CiegoTramos - 1; i > 0; i--) { int j = r.Next(i + 1); bool t = o[i]; o[i] = o[j]; o[j] = t; }
            return o;
        }
        // Lo que se dice al acabar. Pura.
        internal static string ResultadoCiego(int billar, int fabrica)
        {
            if (billar + fabrica == 0) return "No votaste en ningun tramo: no hay ganador.";
            if (billar == fabrica) return "Empate: " + billar + " votos para cada uno.";
            return "Te gusto mas " + (billar > fabrica ? "el modo billar" : "la cadena de fabrica") + ": " + Math.Max(billar, fabrica) + " votos contra " + Math.Min(billar, fabrica) + ".";
        }

        internal string EmpiezaCiego()
        {
            if (ciegoActivo) return "";
            if (!localAuto) return "Enciende el modo billar para comparar.";
            if (nowPlaying == null || nowPlaying.Song == null || videoOn) return "Pon una cancion para comparar.";
            if (sinMejoras) PonSinMejoras(false);
            ciegoOrden = OrdenCiego(new Random()); ciegoVotado = new bool[CiegoTramos];
            ciegoVotosBillar = ciegoVotosFabrica = 0; ciegoTramo = -1; ciegoResultado = "";
            ciegoGuardado = localAuto; ciegoActivo = true; ciegoInicio = DateTime.Now; ciegoCancion = nowPlaying;
            CiegoTick();
            return "";
        }
        // Cada medio segundo (la ventana): cambia de tramo cuando toca, y acaba a los 2 minutos o si cambio la cancion.
        internal void CiegoTick()
        {
            if (!ciegoActivo) return;
            if (!object.ReferenceEquals(nowPlaying, ciegoCancion)) { TerminaCiego("Cambio la cancion: comparacion cortada. "); return; }
            int t = (int)((DateTime.Now - ciegoInicio).TotalSeconds / CiegoTramoSeg);
            if (t >= CiegoTramos) { TerminaCiego(""); return; }
            if (t != ciegoTramo) { ciegoTramo = t; CiegoPon(ciegoOrden[t]); }
        }
        internal void VotaCiego()
        {
            if (!ciegoActivo || ciegoVotado == null || ciegoTramo < 0 || ciegoTramo >= CiegoTramos || ciegoVotado[ciegoTramo]) return;
            ciegoVotado[ciegoTramo] = true;
            if (ciegoOrden[ciegoTramo]) ciegoVotosBillar++; else ciegoVotosFabrica++;
        }
        internal void TerminaCiego(string porQue)
        {
            if (!ciegoActivo) return;
            ciegoActivo = false; ciegoTramo = -1;
            CiegoPon(ciegoGuardado);
            ciegoResultado = porQue + ResultadoCiego(ciegoVotosBillar, ciegoVotosFabrica);
        }
        private void CiegoPon(bool billar)
        {
            if (localAuto == billar) return;
            localAuto = billar;
            try { AplicaBocinas(); AplicaALaMedida(); ApplyNormNow(); } catch (Exception ex) { LogError("Comparar a ciegas", ex); }
        }
        // Lo que dice la hoja.
        internal string TextoCiego()
        {
            if (ciegoActivo && ciegoVotado != null)
            {
                int votos = 0; for (int i = 0; i < CiegoTramos; i++) if (ciegoVotado[i]) votos++;
                return "Comparando: tramo " + (ciegoTramo + 1) + " de " + CiegoTramos + " (cambia cada " + CiegoTramoSeg + " s). Votos: " + votos + "."
                     + (ciegoTramo >= 0 && ciegoVotado[ciegoTramo] ? "\nEste tramo ya tiene tu voto." : "\nToca «Me gusta esta» si este tramo te suena mejor.");
            }
            return !string.IsNullOrEmpty(ciegoResultado) ? ciegoResultado : "2 min: cambia cada 15 s entre el modo billar y la cadena de\nfabrica sin decir cual. Vota los tramos que te suenen mejor.";
        }
        // Lo que se guarda como modo billar: durante la comparacion, el de antes.
        private bool LocalAutoAGuardar { get { return ciegoActivo ? ciegoGuardado : localAuto; } }
    }
}
