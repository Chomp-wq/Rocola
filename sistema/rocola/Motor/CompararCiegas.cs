// (Rocola OS: copiado de XP tal cual; la hoja de ajustes lo ensena con sus botones.)
// COMPARAR A CIEGAS (27 de octubre de 2026, quinta tanda del modo billar). Para decidir con el oido, en el billar, si el modo billar suena
// mejor que la cadena de fabrica: Ctrl+F3 -> Sonido -> Ajustes del sonido -> Lo que suena ahora -> «Comparar a ciegas». Durante 2 minutos la
// cancion que suena cambia cada 15 s entre el modo billar y la cadena de fabrica, en un orden al azar (cuatro tramos de cada uno), sin
// decir cual es. El dueno toca «Me gusta esta» cuando le suena mejor (un voto por tramo) y al final se dice cual eligio mas veces.
//
// Se cambia de verdad el modo (localAuto) y se aplica al instante como el interruptor (las bocinas, el EQ, el bajo y lo demas); lo que se
// fija al armar la cancion (el corte del subgrave a 40 Hz, la compensacion de la presencia) sigue como estaba en esa cancion. Cada modo suena
// a su volumen de verdad: el del billar lleva su volumen parejo. No se guarda nada: la configuracion sigue diciendo lo de antes, y al acabar,
// al cambiar de cancion, al tocar un ajuste o al cerrar la ventana vuelve el modo como estaba. Solo con el modo billar encendido.
//
// CON O SIN AFINAR (septima tanda, 28-10-2026): lo mismo, pero lo que cambia es la afinacion del celular (afinado_usar=), con el modo
// billar encendido todo el rato: para saber si lo que se anoto con el celular de verdad suena mejor. Necesita las bandas de la voz anotadas.
// Lo que se fija al armar la cancion (el bajo segun lo medido) sigue como estaba en esa cancion; el EQ cambia al instante.
//
// PRESENCIA: NUEVA O DE ANTES (30-10-2026, P1 de AUDIO_PROPUESTAS_MODEST_2026-10-29.md): lo mismo con el detector de la presencia del
// billar: la nueva mide sin el grave (de 150 Hz hacia arriba) y la de antes mide la senal entera. Solo cambia la presencia de la cancion que
// suena (PresenciaSala.SinGrave); nada se guarda. Necesita el modo billar y la presencia puesta en esa cancion.
//
// Revision de la septima tanda: (1) mientras se compara, el volumen parejo no aprende de esa cancion (sonaron dos cadenas distintas);
// (2) si la cancion se acaba en mitad de la comparacion, esta se termina ANTES de armar la siguiente (ArmaCadena): si no, la siguiente
// podia armarse en un tramo «de fabrica» y quedarse toda la cancion sin el corte de 40 Hz, la presencia y el volumen parejo del billar.
using System;

namespace Rocola
{
    public partial class Form1
    {
        internal const int CiegoTramos = 8, CiegoTramoSeg = 15;
        public const int CiegoBillar = 0, CiegoAfinacion = 1, CiegoPresencia = 2, CiegoIaNueva = 3;   // que se compara (CiegoIaNueva: IaNueva.cs)
        private bool presenciaSinGrave = true;   // la presencia del billar mide sin el grave (P1); false solo en un tramo "de antes"
        private bool ciegoActivo, ciegoGuardado;
        public bool CiegoActivo { get { return ciegoActivo; } }
        public int CiegoQue { get { return ciegoQue; } }
        private int ciegoQue;
        private bool[] ciegoOrden, ciegoVotado;   // true: el tramo es del modo billar (o con la afinacion) / ya votado (se arman al empezar)
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
        // Lo que se dice al acabar ('a' votos de lo primero: el billar o con la afinacion). Pura.
        internal static string ResultadoCiego(int a, int b, int que)
        {
            if (a + b == 0) return "No votaste en ningun tramo: no hay ganador.";
            if (a == b) return "Empate: " + a + " votos para cada uno.";
            string nomA = que == CiegoAfinacion ? "con tu afinacion" : que == CiegoPresencia ? "la presencia nueva" : que == CiegoIaNueva ? "la IA nueva" : "el modo billar";
            string nomB = que == CiegoAfinacion ? "sin tu afinacion" : que == CiegoPresencia ? "la presencia de antes" : que == CiegoIaNueva ? "la IA de ahora" : "la cadena de fabrica";
            return "Te gusto mas " + (a > b ? nomA : nomB) + ": " + Math.Max(a, b) + " votos contra " + Math.Min(a, b) + ".";
        }

        public string EmpiezaCiego(int que)
        {
            if (ciegoActivo) return "";
            if (que == CiegoIaNueva)
            {
                // LA IA NUEVA (IaNueva.cs): solo se oye donde el DSEE trabaja con el modelo general.
                if (!HayIaNueva) return "No hay IA nueva: la rocola la entrena sola en reposo\n(o «Entrenar la IA ahora» en Mejoras con IA).";
                if (localAuto && !(dseeBillarCortes && DseePara(nowPlaying != null && nowPlaying.Song != null ? nowPlaying.Song.Path : null)))
                    return "Con el modo billar el DSEE solo suena en las muy cortadas\n(«DSEE en el billar, solo en las muy cortadas»): pon una o apaga el modo.";
                if (!sbrOn || !sbrAllowed) return "Enciende «Reconstruir los agudos (DSEE)» en Mejoras con IA.";
                if (nowPlaying == null || nowPlaying.Song == null || SonandoVideo) return "Pon una cancion con los agudos cortados para comparar.";
                float[] c; float corte = eqCache.TryGetValue(nowPlaying.Song.Path, out c) && c != null && c.Length > EqCut ? c[EqCut] : 0f;
                if (corte >= EqSanaHz) return "Esta cancion trae sus agudos completos: el DSEE no la toca.\nPrueba con una de agudos cortados.";
                CadenaCancion ci = cadenaSonando;
                if (ci != null && ci.Sbr != null && ci.Sbr.Red != null) return "Esta cancion usa la IA de su genero (I3):\nprueba con otra o apaga I3.";
            }
            else if (!localAuto) return "Enciende el modo billar para comparar.";
            if (que == CiegoAfinacion && CorreccionAfinada(AfinadoPromedio(), afinadoPonderacion) == null)
                return "Anota antes las bandas de la voz (250 Hz a 2 kHz) en\n\"Afinar el local con el celular\".";
            if (nowPlaying == null || nowPlaying.Song == null || SonandoVideo) return "Pon una cancion para comparar.";
            if (que == CiegoPresencia)
            {
                CadenaCancion cp = cadenaSonando;
                if (cp == null || cp.Presencia == null || !cp.Presencia.Encendido)
                    return "En esta cancion el modo billar no puso la presencia\n(su rango es chico): prueba con otra.";
            }
            if (sinMejoras) PonSinMejoras(false);
            CadenaCancion cs = cadenaSonando; if (cs != null && cs.Margen != null) cs.Margen.Descarta = true;   // (1) esta vuelta no se aprende
            ciegoQue = que == CiegoAfinacion || que == CiegoPresencia || que == CiegoIaNueva ? que : CiegoBillar;
            ciegoOrden = OrdenCiego(new Random()); ciegoVotado = new bool[CiegoTramos];
            ciegoVotosBillar = ciegoVotosFabrica = 0; ciegoTramo = -1; ciegoResultado = "";
            ciegoGuardado = ciegoQue == CiegoAfinacion ? afinadoUsar : ciegoQue == CiegoPresencia ? presenciaSinGrave : ciegoQue == CiegoIaNueva ? false : localAuto; ciegoActivo = true; ciegoInicio = DateTime.Now; ciegoCancion = nowPlaying;
            CiegoTick();
            return "";
        }
        // Cada medio segundo (la ventana): cambia de tramo cuando toca, y acaba a los 2 minutos o si cambio la cancion.
        public void CiegoTick()
        {
            if (!ciegoActivo) return;
            if (!object.ReferenceEquals(nowPlaying, ciegoCancion)) { TerminaCiego("Cambio la cancion: comparacion cortada. "); return; }
            int t = (int)((DateTime.Now - ciegoInicio).TotalSeconds / CiegoTramoSeg);
            if (t >= CiegoTramos) { TerminaCiego(""); return; }
            if (t != ciegoTramo) { ciegoTramo = t; CiegoPon(ciegoOrden[t]); }
        }
        public void VotaCiego()
        {
            if (!ciegoActivo || ciegoVotado == null || ciegoTramo < 0 || ciegoTramo >= CiegoTramos || ciegoVotado[ciegoTramo]) return;
            ciegoVotado[ciegoTramo] = true;
            if (ciegoOrden[ciegoTramo]) ciegoVotosBillar++; else ciegoVotosFabrica++;
        }
        public void TerminaCiego(string porQue)
        {
            if (!ciegoActivo) return;
            ciegoActivo = false; ciegoTramo = -1;
            CiegoPon(ciegoGuardado);
            ciegoResultado = porQue + ResultadoCiego(ciegoVotosBillar, ciegoVotosFabrica, ciegoQue);
            if (ciegoQue == CiegoIaNueva)
            {
                iaNuevaGano = porQue.Length == 0 && ciegoVotosBillar > ciegoVotosFabrica;   // solo una comparacion entera cuenta
                if (iaNuevaGano) ciegoResultado += "\nSi te convence: «Quedarse con la IA nueva» (Mejoras con IA).";
            }
        }
        private void CiegoPon(bool a)
        {
            if (ciegoQue == CiegoIaNueva) { PonIaComparada(a); return; }
            if (ciegoQue == CiegoPresencia)
            {
                presenciaSinGrave = a;
                CadenaCancion c = cadenaSonando; if (c != null && c.Presencia != null) c.Presencia.SinGrave = a;
                return;
            }
            if (ciegoQue == CiegoAfinacion) { if (afinadoUsar == a) return; afinadoUsar = a; }
            else { if (localAuto == a) return; localAuto = a; }
            try { AplicaBocinas(); AplicaALaMedida(); ApplyNormNow(); } catch (Exception ex) { LogError("Comparar a ciegas", ex); }
        }
        // Lo que dice la hoja.
        public string TextoCiego()
        {
            if (ciegoActivo && ciegoVotado != null)
            {
                int votos = 0; for (int i = 0; i < CiegoTramos; i++) if (ciegoVotado[i]) votos++;
                return (ciegoQue == CiegoAfinacion ? "Con o sin afinar: tramo " : ciegoQue == CiegoPresencia ? "Presencia nueva o de antes: tramo " : ciegoQue == CiegoIaNueva ? "IA de ahora o nueva: tramo " : "Billar o fabrica: tramo ") + (ciegoTramo + 1) + " de " + CiegoTramos + " (cambia cada " + CiegoTramoSeg + " s). Votos: " + votos + "."
                     + (ciegoTramo >= 0 && ciegoVotado[ciegoTramo] ? "\nEste tramo ya tiene tu voto." : "\nToca «Me gusta esta» si este tramo te suena mejor.");
            }
            return !string.IsNullOrEmpty(ciegoResultado) ? ciegoResultado : "A ciegas, 2 min: cambia cada 15 s entre dos formas de sonar sin\ndecir cual (el billar o la de fabrica; con o sin tu afinacion; la\npresencia nueva o la de antes). Vota los tramos que te suenen mejor.";
        }
        // Lo que se guarda como modo billar y como «usar la afinacion»: durante la comparacion, lo de antes.
        private bool LocalAutoAGuardar { get { return ciegoActivo && ciegoQue == CiegoBillar ? ciegoGuardado : localAuto; } }
        private bool AfinadoUsarAGuardar { get { return ciegoActivo && ciegoQue == CiegoAfinacion ? ciegoGuardado : afinadoUsar; } }
    }
}
