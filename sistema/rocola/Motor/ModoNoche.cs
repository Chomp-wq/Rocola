// EL MODO NOCHE (decision del dueno del 7-11-2026: «nos aventaron piedras por tener musica a las 12 A.M, asi que volveremos a poner el modo
// noche, con un slider desde el 70 al 90% del audio, y con un boton para probar como se escucharia cuando se active»). Lo mismo que en XP
// (Rocola/Form1.Sonido.cs, la misma cuenta y las mismas claves): de noche_desde= a noche_hasta= (23 a 7) la rocola suena al noche_pct= %
// (70 a 90, 80 de fabrica) de lo que se oye -10*log2(pct/100) dB: 70 % = -5,1 dB, 80 % = -3,2, 90 % = -1,5-, con 20 s de rampa; noche=
// (encendido de fabrica). Canciones, Spotify (por la misma cadena) y videos (el filtro @noche de mpv). «Probar» (Ctrl+F3 -> Sonido) lo pone
// 15 s al momento, con rampa de 1,5 s, y vuelve solo.
using System;

namespace Rocola
{
    public partial class Form1
    {
        public static bool nocheOn = true;   // "noche=" (publico: la consola lo apaga al escribir a un archivo)
        internal static int nocheDesde = 23, nocheHasta = 7, nochePct = 80;
        internal const int NochePctMin = 70, NochePctMax = 90;
        private const int NocheRampaMs = 20000, NochePruebaRampaMs = 1500, NochePruebaMs = 15000;
        private float nocheFactor = 1f;
        private int nocheUltMs, nochePruebaHasta;
        private bool nocheArrancada, nocheAvisada, nochePruebaRapida;

        internal static float NocheDb() { return (float)(10.0 * Math.Log(Math.Max(1, Math.Min(100, nochePct)) / 100.0, 2.0)); }
        private static int linDe = -1; private static float linVal = 1f;
        public static float NocheLin() { int p = nochePct; if (p != linDe) { linVal = (float)Math.Pow(10.0, NocheDb() / 20.0); linDe = p; } return linVal; }   // (una vez por porcentaje)
        // El rango CRUZA LA MEDIANOCHE: va un O, no un Y («hora >= 23 && hora < 7» es siempre falso).
        internal static bool EsDeNoche(DateTime t)
        {
            int h = t.Hour, a = nocheDesde, b = nocheHasta;
            if (a == b) return false;
            if (a < b) return h >= a && h < b;
            return h >= a || h < b;
        }
        private bool ProbandoNoche { get { return nochePruebaHasta != 0 && unchecked(Environment.TickCount - nochePruebaHasta) < 0; } }
        // (la hora se mira una vez por segundo, no en cada vuelta del bucle: DateTime.Now convierte de zona horaria cada vez)
        private bool nocheAhora; private int nocheMirada;
        private bool EsDeNocheAhora()
        {
            int t = Environment.TickCount;
            if (nocheMirada == 0 || unchecked(t - nocheMirada) >= 1000) { nocheAhora = EsDeNoche(DateTime.Now); nocheMirada = t | 1; }
            return nocheAhora;
        }
        private float MetaNoche() { return ((nocheOn && EsDeNocheAhora()) || ProbandoNoche) ? NocheLin() : 1f; }
        // Lo que baja AHORA (la senal: canciones y videos). Antes de la primera vuelta, el destino.
        public float FactorNoche() { return nocheArrancada ? nocheFactor : MetaNoche(); }
        public void PruebaNoche() { nochePruebaHasta = (Environment.TickCount + NochePruebaMs) | 1; nochePruebaRapida = true; }
        public int SegundosPruebaNoche { get { return ProbandoNoche ? Math.Max(1, unchecked(nochePruebaHasta - Environment.TickCount) / 1000 + 1) : 0; } }

        internal static float AvanzaRampa(float actual, float meta, int msPasados, int rampaMs)
        {
            if (actual == meta) return meta;
            if (msPasados <= 0) return actual;
            float paso = 0.5f * msPasados / rampaMs;   // medio volumen en rampaMs: la noche (70 a 90 %) en menos que eso
            if (actual < meta) { actual += paso; if (actual > meta) actual = meta; }
            else { actual -= paso; if (actual < meta) actual = meta; }
            return actual;
        }

        // Cada vuelta del bucle de la rocola (Controlador). Por milisegundos de reloj, no por vueltas.
        public void AvanzaModoNoche()
        {
            bool esNoche = nocheOn && EsDeNocheAhora();
            float meta = MetaNoche();
            int ahora = Environment.TickCount;
            if (!nocheArrancada) { nocheArrancada = true; nocheUltMs = ahora; nocheFactor = meta; nocheAvisada = esNoche; return; }   // al prender, sin rampa
            int dt = unchecked(ahora - nocheUltMs); nocheUltMs = ahora;
            if (dt < 0 || dt > 5000) dt = 0;
            if (nochePruebaHasta != 0 && !ProbandoNoche) { nochePruebaHasta = 0; nochePruebaRapida = true; }   // acabo la prueba: vuelve rapido
            if (esNoche != nocheAvisada)
            {
                nocheAvisada = esNoche;
                Avisa(esNoche ? "Modo noche: el volumen baja al " + nochePct + " %" : "Se acabó el modo noche: vuelve el volumen de siempre");
            }
            float antes = nocheFactor;
            nocheFactor = AvanzaRampa(nocheFactor, meta, dt, nochePruebaRapida ? NochePruebaRampaMs : NocheRampaMs);
            if (nocheFactor == meta) nochePruebaRapida = false;
            if (nocheFactor != antes)
            {
                float m = MasterEfectivo() * nocheFactor;
                LevelTap t = curTap; if (t != null) t.Master = m;   // la que suena (y Spotify: misma cadena)
                CadenaCancion p = cadenaPreparada; if (p != null && p.Tap != null) p.Tap.Master = m;   // y la siguiente (solo su volumen: lo demas no cambia)
            }
        }
    }
}
