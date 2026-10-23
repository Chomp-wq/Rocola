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
            if (path == null || t == null || !t.TryGetValue(path, out s)) return -1f;
            return RangoDe(s);
        }
        internal static float RangoDe(SonoridadIntegrada s)
        {
            if (!s.Valida || s.Muda || !s.ConPicos) return -1f;
            float r = s.P99Db - s.Lkfs;
            return float.IsNaN(r) || float.IsInfinity(r) ? -1f : Math.Max(0f, r);
        }

        // EL REPARTO DE LOS RANGOS de la biblioteca (23-10-2026), para rocola_calidad.txt: los umbrales de 12 y 16 dB se eligieron de oido,
        // y sin ver cuantas canciones de ESTA biblioteca caen de cada lado no hay como saber si le sirven. Solo informa: no cambia el sonido.
        // En el hilo de la pantalla (la tabla es suya); devuelve los rangos de las medidas con picos, de menor a mayor, y cuantas medidas
        // enteras de una version vieja no los tienen (el repaso las vuelve a medir).
        private float[] RangosMedidos(out int sinPicos)
        {
            sinPicos = 0;
            System.Collections.Generic.Dictionary<string, SonoridadIntegrada> t = integCache;
            if (t == null) return new float[0];
            System.Collections.Generic.List<float> r = new System.Collections.Generic.List<float>(t.Count);
            foreach (System.Collections.Generic.KeyValuePair<string, SonoridadIntegrada> kv in t)
            {
                SonoridadIntegrada s = kv.Value;
                if (!s.Valida || s.Muda) continue;
                float x = RangoDe(s);
                if (x < 0f) { if (!s.ConPicos) sinPicos++; continue; }
                r.Add(x);
            }
            r.Sort();
            return r.ToArray();
        }

        internal static string TextoRangos(float[] ordenados, int sinPicos)
        {
            CultureInfo ic = CultureInfo.InvariantCulture;
            int n = ordenados == null ? 0 : ordenados.Length;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(900);
            sb.Append("RANGO DE LAS CANCIONES (modo automatico del local: Ctrl+F3, Sonido, El local y sus bocinas, Tono y presencia)\r\n");
            sb.Append("  Rango = lo fuerte de la cancion frente a su promedio (P99 de los picos menos la sonoridad). Un disco de pop actual ronda 6 a 10 dB;\r\n");
            sb.Append("  una balada, clasica o grabacion sin comprimir, 14 o mas. El modo automatico usa ").Append(RangoAltoDb.ToString("0", ic)).Append(" y ").Append(RangoMuyAltoDb.ToString("0", ic)).Append(" dB como cortes.\r\n");
            sb.Append("  Medidas con picos: ").Append(n.ToString("#,0", ic));
            if (sinPicos > 0) sb.Append("   (otras ").Append(sinPicos.ToString("#,0", ic)).Append(" son de una version vieja, sin picos: el repaso las vuelve a medir)");
            sb.Append("\r\n");
            if (n == 0) { sb.Append("  Aun no hay ninguna: deja que el repaso vaya midiendo.\r\n"); return sb.ToString(); }
            float[] bordes = { 6f, 8f, 10f, 12f, 14f, 16f };
            int[] cuenta = new int[bordes.Length + 1];
            for (int i = 0; i < n; i++) { int b = 0; while (b < bordes.Length && ordenados[i] >= bordes[b]) b++; cuenta[b]++; }
            string[] nombres = { "menos de 6 dB", "de 6 a 8 dB", "de 8 a 10 dB", "de 10 a 12 dB", "de 12 a 14 dB", "de 14 a 16 dB", "16 dB o mas" };
            for (int b = 0; b < cuenta.Length; b++)
                sb.Append("  ").Append(nombres[b].PadRight(15)).Append(cuenta[b].ToString("#,0", ic).PadLeft(8)).Append("   ").Append((100.0 * cuenta[b] / n).ToString("0.0", ic).PadLeft(5)).Append(" %\r\n");
            int altas = 0, muyAltas = 0;
            for (int i = 0; i < n; i++) { if (ordenados[i] >= RangoAltoDb) altas++; if (ordenados[i] >= RangoMuyAltoDb) muyAltas++; }
            sb.Append("  Mediana ").Append(ordenados[n / 2].ToString("0.0", ic)).Append(" dB, la de 9 de cada 10 canciones ").Append(ordenados[Math.Min(n - 1, (int)(n * 0.9))].ToString("0.0", ic)).Append(" dB.\r\n");
            sb.Append("  Con partes suaves (billar y bocinas chicas): ").Append(altas.ToString("#,0", ic)).Append(" canciones (").Append((100.0 * altas / n).ToString("0.0", ic)).Append(" %); con presencia (billar): ").Append(muyAltas.ToString("#,0", ic)).Append(" (").Append((100.0 * muyAltas / n).ToString("0.0", ic)).Append(" %).\r\n");
            return sb.ToString();
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
