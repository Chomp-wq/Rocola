// IA2 «CORTAR DONDE CORTA TU MUSICA» (Rocola OS, 3-11-2026; decision del dueno sobre IA_DATOS_EN_CRUDO_MODEST_2026-11-03.md).
// La cosecha para la IA del DSEE simulaba el corte de un MP3 en cinco sitios fijos (11; 13; 15; 16,5 y 18 kHz). Pero la rocola sabe
// donde corta de verdad cada MP3 danado de la biblioteca (la medida de calidad: EqCut de la tabla del EQ), y es ahi donde la red tiene
// que acertar. Ahora a los cinco de siempre se suman hasta CortesExtra cortes: los mas comunes de la biblioteca (redondeados a 250 Hz,
// entre 8 y 19 kHz, separados al menos 400 Hz de los fijos y entre si), en el orden de cuantas canciones los tienen. Mas ejemplos justo
// donde se usan, sin cambiar la cuenta de cada par (MakePair: el extractor sigue en la version 2) ni lo que suena.
// La lista se rehace como mucho cada 10 minutos en el hilo que lleva las tablas (PublicaMedidas) y la cosecha (el hilo del repaso)
// solo lee la copia ya hecha. Con menos de CortesMinCanciones canciones danadas medidas en un corte, ese corte no entra.
using System;
using System.Collections.Generic;

namespace Rocola
{
    public partial class Form1
    {
        internal const int CortesExtra = 4, CortesMinCanciones = 3;
        private static readonly float[] IaCutsFijos = { 11000f, 13000f, 15000f, 16500f, 18000f };
        private volatile float[] iaCortes = IaCutsFijos;
        private int iaCortesHecho = int.MinValue / 2, iaCortesVer = -1;
        private float[] IaCutsHz { get { return iaCortes; } }

        // En el hilo de las tablas.
        private void RehaceCortesIa()
        {
            if (iaCortesVer == eqCacheVer) return;   // nada nuevo
            if (iaCortesVer >= 0 && unchecked(Environment.TickCount - iaCortesHecho) < 10 * 60 * 1000) return;   // como mucho cada 10 min
            iaCortesHecho = Environment.TickCount; iaCortesVer = eqCacheVer;
            List<float> cortes = new List<float>();
            foreach (float[] c in eqCache.Values) if (c != null && c.Length > EqCut) cortes.Add(c[EqCut]);
            iaCortes = CortesDe(cortes);
        }

        // Pura (la prueba la llama): de los cortes medidos de la biblioteca, la lista de la cosecha.
        internal static float[] CortesDe(IEnumerable<float> medidos)
        {
            Dictionary<int, int> cuenta = new Dictionary<int, int>();
            foreach (float hz in medidos)
            {
                if (!(hz >= 8000f && hz < EqSanaHz)) continue;   // sin medir (0), o completa: no es un corte de MP3
                int k = (int)Math.Round(hz / 250f);
                if (k * 250 > 19000) continue;
                int n; cuenta.TryGetValue(k, out n); cuenta[k] = n + 1;
            }
            List<KeyValuePair<int, int>> orden = new List<KeyValuePair<int, int>>(cuenta);
            orden.Sort(delegate (KeyValuePair<int, int> a, KeyValuePair<int, int> b) { int d = b.Value.CompareTo(a.Value); return d != 0 ? d : a.Key.CompareTo(b.Key); });
            List<float> r = new List<float>(IaCutsFijos);
            int extra = 0;
            foreach (KeyValuePair<int, int> kv in orden)
            {
                if (extra >= CortesExtra || kv.Value < CortesMinCanciones) break;
                float hz = kv.Key * 250f; bool cerca = false;
                foreach (float f in r) if (Math.Abs(f - hz) < 400f) { cerca = true; break; }
                if (cerca) continue;
                r.Add(hz); extra++;
            }
            return r.ToArray();
        }
    }
}
