// EL LOCAL EN AUTOMATICO (23 de octubre de 2026). Un solo interruptor que enciende y ajusta por ti las mejoras del local
// (el subgrave, el estereo estrecho, la claridad, las partes suaves, los agudos y la presencia en sala ruidosa) para que
// suene lo mejor posible, sin tocar los mandos uno por uno.
//
// SIN MICROFONO la rocola no oye el local, asi que decide con dos cosas que si sabe:
//  1. EL TIPO DE LOCAL, que se elige una vez (Ctrl+F3 -> Sonido -> El local y sus bocinas -> Tono y presencia):
//       - Bocinas repartidas del billar: sin subgrave, estereo estrecho solo donde hace falta (S3), claridad.
//       - Sala con bocinas buenas: no se toca el tono; solo se ayuda a las canciones de mucho rango.
//       - Bocinas chicas: sin subgrave, claridad y un poco mas de agudos (+1 dB).
//  2. CADA CANCION, con lo que el repaso ya midio de ella: su rango dinamico (lo fuerte frente al promedio). Las de mucho
//     rango (baladas, clasica, grabaciones sin comprimir) son las que pierden sus partes bajitas en el ruido del local; las
//     ya comprimidas (el pop actual) no lo necesitan y no se tocan. Una cancion sin medir no activa nada de esto.
//
// Con el automatico encendido los mandos que decide quedan en gris diciendolo; lo que habia guardado en ellos se conserva y
// vuelve al apagarlo. El balance, la prueba de bocinas, los arreglos de grabacion y el resto de Sonido siguen a mano.
// Apagado (de fabrica) la cadena sale BIT A BIT como antes: LocalDe devuelve los mandos tal cual.
using System;
using System.Globalization;

namespace Rocola
{
    public partial class Form1
    {
        internal static bool localAuto;   // "local_auto=": el modo automatico (apagado de fabrica)
        internal static int tipoLocal;    // "tipo_local=": 0 bocinas repartidas del billar, 1 sala con bocinas buenas, 2 bocinas chicas
        internal const int TiposLocal = 3;
        internal static readonly string[] NombresTipoLocal = { "Bocinas repartidas del billar", "Sala con bocinas buenas", "Bocinas chicas" };
        // El rango (dB entre el P99 de los picos y la sonoridad integrada) desde el que una cancion cuenta como de mucho rango, y desde el
        // que lo cuenta la presencia en sala ruidosa. Un disco de pop actual ronda 6 a 10 dB; una balada o clasica, 14 o mas.
        internal const float RangoAltoDb = 12f, RangoMuyAltoDb = 16f;

        internal struct AjusteLocal
        {
            public bool Subgrave, Estrecho, EstrechoSoloSiHaceFalta, Claridad, Suaves, Presencia;
            public int Graves, Agudos;
        }

        internal static int TipoLocalDe(string texto, int actual)
        {
            int v;
            if (!int.TryParse((texto ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return actual;
            return Math.Max(0, Math.Min(TiposLocal - 1, v));
        }

        // Lo que el modo automatico pone para un tipo de local y una cancion de ese rango (rangoDb < 0 = sin medir). Pura: sin estado.
        internal static AjusteLocal LocalAutomatico(int tipo, float rangoDb)
        {
            AjusteLocal a = new AjusteLocal();
            bool medida = rangoDb >= 0f && !float.IsNaN(rangoDb);
            bool alto = medida && rangoDb >= RangoAltoDb, muyAlto = medida && rangoDb >= RangoMuyAltoDb;
            switch (Math.Max(0, Math.Min(TiposLocal - 1, tipo)))
            {
                case 0:   // las bocinas repartidas del billar: ruido y rebote
                    a.Subgrave = true; a.Estrecho = true; a.EstrechoSoloSiHaceFalta = true; a.Claridad = true;
                    a.Suaves = alto; a.Presencia = muyAlto;
                    break;
                case 1:   // bocinas buenas: el tono se deja como viene
                    a.Suaves = muyAlto;
                    break;
                default:  // bocinas chicas: no dan el grave, no hay que gastarlo
                    a.Subgrave = true; a.Claridad = true; a.Agudos = 1; a.Suaves = alto;
                    break;
            }
            return a;
        }

        // El rango de la cancion (lo fuerte frente al promedio, en dB) con lo ya medido, o -1 si no hay medida.
        // Es el P99 de los picos menos la sonoridad integrada (las dos de la medida del repaso, en dB): lo que se llama
        // rango de nivel entre lo fuerte y el promedio. Sin medida integrada con picos (algoritmo 2 o mas), no se sabe.
        internal float RangoDeCancion(string path)
        {
            System.Collections.Generic.Dictionary<string, SonoridadIntegrada> t = integCache;
            SonoridadIntegrada s;
            if (path == null || t == null || !t.TryGetValue(path, out s) || !s.Valida || s.Muda || !s.ConPicos) return -1f;
            float r = s.P99Db - s.Lkfs;
            return float.IsNaN(r) || float.IsInfinity(r) ? -1f : Math.Max(0f, r);
        }

        // Lo que de verdad suena para esta cancion: los mandos tal cual (automatico apagado) o lo que decide el automatico.
        private AjusteLocal LocalDe(Track t)
        {
            if (localAuto)
            {
                return LocalAutomatico(tipoLocal, t == null ? -1f : RangoDeCancion(t.Path));
            }
            AjusteLocal m = new AjusteLocal();
            m.Subgrave = cortaSubgrave; m.Estrecho = estereoEstrecho; m.EstrechoSoloSiHaceFalta = estereoAuto;
            m.Claridad = claridadLocal; m.Suaves = partesSuaves; m.Presencia = presenciaSala;
            m.Graves = gravesLocalDb; m.Agudos = agudosLocalDb;
            return m;
        }
        private AjusteLocal LocalDe(CadenaCancion c) { return LocalDe(c == null ? null : c.Cancion); }
    }
}
