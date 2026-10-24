// EL LOCAL EN AUTOMATICO (23 de octubre de 2026). Un solo interruptor que enciende y ajusta por ti las mejoras del local
// (el subgrave, el estereo estrecho, la claridad, las partes suaves, los agudos y la presencia en sala ruidosa) para que
// suene lo mejor posible, sin tocar los mandos uno por uno.
//
// SIN MICROFONO la rocola no oye el local, asi que decide con dos cosas que si sabe:
//  1. EL TIPO DE LOCAL, que se elige una vez (Ctrl+F3 -> Sonido -> El local y sus bocinas -> Tono y presencia):
//       - Bocinas repartidas del billar: sin subgrave, estereo estrecho solo donde hace falta (S3), claridad.
//       - Sala con bocinas buenas: no se toca el tono; solo se ayuda a las canciones de mucho rango.
//       - Bocinas chicas: sin subgrave, claridad y un poco mas de agudos (+1 dB).
//  2. CADA CANCION, con lo que el repaso ya midio de ella. Desde el 23-10-2026 (el dueno eligio P1 y P2 de
//     MODOS_AUTOMATICOS_SONIDO_MODEST_2026-10-23.md):
//       - PARTES SUAVES (billar y bocinas chicas): SIEMPRE encendidas. La etapa ya se frena sola por nivel (solo sube lo que va 12 dB
//         bajo el objetivo, hasta 4 dB): en una cancion pareja no hace nada, y la que tiene una intro o una estrofa bajita la recibe.
//         Antes dependian del rango P99 - sonoridad, que mide picos contra promedio, no cuanto se calla la cancion.
//       - PRESENCIA (billar): segun el RANGO DE SONORIDAD (LRA, EBU Tech 3342) de la cancion, desde LraPresenciaLu (10 LU): lo que
//         varia el volumen en el tiempo. Una cancion pareja y fuerte no la necesita y la presencia la bajaria ~1 dB.
//       - Sala con bocinas buenas: partes suaves solo con LRA desde LraSalaLu (14 LU: ayuda mas parca, como antes con 16 dB de rango).
//       - Una cancion con medida de antes (sin LRA) usa la regla vieja de P99 - sonoridad para la presencia del billar (16 dB) y para
//         las partes suaves de la sala (16 dB) hasta que el repaso la vuelva a medir (desde el 24-10-2026 la tanda larga vuelve a medir
//         toda la biblioteca para el nivelado al limite, con el modo encendido o apagado); sin medida, solo lo fijo del tipo.
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
        // El rango de sonoridad (LRA, en LU) desde el que una cancion lleva la presencia en el billar, y desde el que la sala con bocinas
        // buenas le sube las partes suaves. Una balada o una cancion con intro suave ronda 11 a 18 LU; un tema parejo, menos de 3.
        internal const float LraPresenciaLu = 10f, LraSalaLu = 14f;

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
        internal static AjusteLocal LocalAutomatico(int tipo, float rangoDb) { return LocalAutomatico(tipo, rangoDb, -1f); }
        // 'lraLu' < 0 = la cancion no tiene rango de sonoridad (medida de antes): se usa la regla de P99 - sonoridad.
        internal static AjusteLocal LocalAutomatico(int tipo, float rangoDb, float lraLu)
        {
            AjusteLocal a = new AjusteLocal();
            bool medida = rangoDb >= 0f && !float.IsNaN(rangoDb);
            bool muyAlto = medida && rangoDb >= RangoMuyAltoDb;
            bool conLra = lraLu >= 0f && !float.IsNaN(lraLu) && !float.IsInfinity(lraLu);
            switch (Math.Max(0, Math.Min(TiposLocal - 1, tipo)))
            {
                case 0:   // las bocinas repartidas del billar: ruido y rebote
                    a.Subgrave = true; a.Estrecho = true; a.EstrechoSoloSiHaceFalta = true; a.Claridad = true;
                    a.Suaves = true; a.Presencia = conLra ? lraLu >= LraPresenciaLu : muyAlto;
                    break;
                case 1:   // bocinas buenas: el tono se deja como viene
                    a.Suaves = conLra ? lraLu >= LraSalaLu : muyAlto;
                    break;
                default:  // bocinas chicas: no dan el grave, no hay que gastarlo (la presencia, como antes, no)
                    a.Subgrave = true; a.Claridad = true; a.Agudos = 1; a.Suaves = true;
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
        // El rango de sonoridad (LRA, en LU) de la cancion con lo ya medido, o -1 si no hay (medida de antes del 23-10-2026, cancion muy corta).
        internal float LraDeCancion(string path)
        {
            System.Collections.Generic.Dictionary<string, SonoridadIntegrada> t = integCache;
            SonoridadIntegrada s;
            if (path == null || t == null || !t.TryGetValue(path, out s) || !s.Valida || s.Muda || !s.ConLra) return -1f;
            return s.Lra;
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
        private float[] RangosMedidos(out int sinPicos) { float[] lras; int sinLra; return RangosMedidos(out sinPicos, out lras, out sinLra); }
        // Y los rangos de sonoridad (LRA) de las que ya lo tienen, y cuantas medidas validas aun no (el repaso las vuelve a medir).
        private float[] RangosMedidos(out int sinPicos, out float[] lras, out int sinLra) { int sinPosible; return RangosMedidos(out sinPicos, out lras, out sinLra, out sinPosible); }
        // 'sinPosible': medidas de este algoritmo sin LRA porque no pueden tenerlo (menos de 3 s): el repaso no las vuelve a medir.
        // Solo copia los numeros (sin ordenar): el que arma el texto los ordena en el hilo de fondo que escribe el informe.
        private float[] RangosMedidos(out int sinPicos, out float[] lras, out int sinLra, out int sinPosible)
        {
            sinPicos = 0; sinLra = 0; sinPosible = 0; lras = new float[0];
            System.Collections.Generic.Dictionary<string, SonoridadIntegrada> t = integCache;
            if (t == null) return new float[0];
            System.Collections.Generic.List<float> r = new System.Collections.Generic.List<float>(t.Count);
            System.Collections.Generic.List<float> q = new System.Collections.Generic.List<float>(t.Count);
            foreach (System.Collections.Generic.KeyValuePair<string, SonoridadIntegrada> kv in t)
            {
                SonoridadIntegrada s = kv.Value;
                if (!s.Valida || s.Muda) continue;
                if (s.ConLra) q.Add(s.Lra); else if (s.Algoritmo >= MedidorBs1770.Algoritmo) sinPosible++; else sinLra++;
                float x = RangoDe(s);
                if (x < 0f) { if (!s.ConPicos) sinPicos++; continue; }
                r.Add(x);
            }
            lras = q.ToArray();
            return r.ToArray();
        }

        internal static string TextoRangos(float[] ordenados, int sinPicos) { return TextoRangos(ordenados, sinPicos, null, 0); }
        internal static string TextoRangos(float[] ordenados, int sinPicos, float[] lrasOrdenados, int sinLra) { return TextoRangos(ordenados, sinPicos, lrasOrdenados, sinLra, 0); }
        // Los numeros pueden venir sin ordenar: aqui se ordena una copia.
        internal static string TextoRangos(float[] ordenados, int sinPicos, float[] lrasOrdenados, int sinLra, int sinLraPosible)
        {
            if (ordenados != null) { ordenados = (float[])ordenados.Clone(); Array.Sort(ordenados); }
            if (lrasOrdenados != null) { lrasOrdenados = (float[])lrasOrdenados.Clone(); Array.Sort(lrasOrdenados); }
            CultureInfo ic = CultureInfo.InvariantCulture;
            int n = ordenados == null ? 0 : ordenados.Length;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(900);
            sb.Append("RANGO DE LAS CANCIONES (modo automatico del local: Ctrl+F3, Sonido, El local y sus bocinas, Tono y presencia)\r\n");
            sb.Append("  Rango = lo fuerte de la cancion frente a su promedio (P99 de los picos menos la sonoridad). Un disco de pop actual ronda 6 a 10 dB;\r\n");
            sb.Append("  una balada, clasica o grabacion sin comprimir, 14 o mas. El modo automatico usa ").Append(RangoAltoDb.ToString("0", ic)).Append(" y ").Append(RangoMuyAltoDb.ToString("0", ic)).Append(" dB como cortes.\r\n");
            sb.Append("  Medidas con picos: ").Append(n.ToString("#,0", ic));
            if (sinPicos > 0) sb.Append("   (otras ").Append(sinPicos.ToString("#,0", ic)).Append(" son de una version vieja, sin picos: el repaso las vuelve a medir)");
            sb.Append("\r\n");
            if (n == 0) { sb.Append("  Aun no hay ninguna: deja que el repaso vaya midiendo.\r\n"); AnadeLra(sb, lrasOrdenados, sinLra, sinLraPosible, ic); return sb.ToString(); }
            float[] bordes = { 6f, 8f, 10f, 12f, 14f, 16f };
            int[] cuenta = new int[bordes.Length + 1];
            for (int i = 0; i < n; i++) { int b = 0; while (b < bordes.Length && ordenados[i] >= bordes[b]) b++; cuenta[b]++; }
            string[] nombres = { "menos de 6 dB", "de 6 a 8 dB", "de 8 a 10 dB", "de 10 a 12 dB", "de 12 a 14 dB", "de 14 a 16 dB", "16 dB o mas" };
            for (int b = 0; b < cuenta.Length; b++)
                sb.Append("  ").Append(nombres[b].PadRight(15)).Append(cuenta[b].ToString("#,0", ic).PadLeft(8)).Append("   ").Append((100.0 * cuenta[b] / n).ToString("0.0", ic).PadLeft(5)).Append(" %\r\n");
            int altas = 0, muyAltas = 0;
            for (int i = 0; i < n; i++) { if (ordenados[i] >= RangoAltoDb) altas++; if (ordenados[i] >= RangoMuyAltoDb) muyAltas++; }
            sb.Append("  Mediana ").Append(ordenados[n / 2].ToString("0.0", ic)).Append(" dB, la de 9 de cada 10 canciones ").Append(ordenados[Math.Min(n - 1, (int)(n * 0.9))].ToString("0.0", ic)).Append(" dB.\r\n");
            sb.Append("  Con 12 dB o mas: ").Append(altas.ToString("#,0", ic)).Append(" canciones (").Append((100.0 * altas / n).ToString("0.0", ic)).Append(" %); con 16 dB o mas: ").Append(muyAltas.ToString("#,0", ic)).Append(" (").Append((100.0 * muyAltas / n).ToString("0.0", ic)).Append(" %). Es la regla de las medidas de antes, que aun no tienen rango de sonoridad.\r\n");
            AnadeLra(sb, lrasOrdenados, sinLra, sinLraPosible, ic);
            return sb.ToString();
        }
        // El rango de sonoridad (LRA, EBU Tech 3342): lo que decide la presencia del billar (10 LU) y las partes suaves de la sala (14 LU).
        private static void AnadeLra(System.Text.StringBuilder sb, float[] lras, int sinLra, int sinLraPosible, CultureInfo ic)
        {
            int m = lras == null ? 0 : lras.Length;
            sb.Append("  RANGO DE SONORIDAD (LRA, lo que varia el volumen en el tiempo; un tema parejo, menos de 3 LU; una balada, 10 a 20): ").Append(m.ToString("#,0", ic)).Append(" medidas");
            if (sinLra > 0) sb.Append("   (faltan ").Append(sinLra.ToString("#,0", ic)).Append(": la tanda larga las vuelve a medir en segundo plano; mientras, rige la regla de arriba)");
            if (sinLraPosible > 0) sb.Append("   (otras ").Append(sinLraPosible.ToString("#,0", ic)).Append(" no pueden tenerlo: duran menos de 3 s)");
            sb.Append("\r\n");
            if (m == 0) return;
            float[] bordes = { 3f, 6f, 10f, 14f, 20f };
            int[] cuenta = new int[bordes.Length + 1];
            for (int i = 0; i < m; i++) { int b = 0; while (b < bordes.Length && lras[i] >= bordes[b]) b++; cuenta[b]++; }
            string[] nombres = { "menos de 3 LU", "de 3 a 6 LU", "de 6 a 10 LU", "de 10 a 14 LU", "de 14 a 20 LU", "20 LU o mas" };
            for (int b = 0; b < cuenta.Length; b++)
                sb.Append("  ").Append(nombres[b].PadRight(15)).Append(cuenta[b].ToString("#,0", ic).PadLeft(8)).Append("   ").Append((100.0 * cuenta[b] / m).ToString("0.0", ic).PadLeft(5)).Append(" %\r\n");
            int conPres = 0, conSala = 0;
            for (int i = 0; i < m; i++) { if (lras[i] >= LraPresenciaLu) conPres++; if (lras[i] >= LraSalaLu) conSala++; }
            sb.Append("  Mediana ").Append(lras[m / 2].ToString("0.0", ic)).Append(" LU. Con presencia en el billar (").Append(LraPresenciaLu.ToString("0", ic)).Append(" LU o mas): ").Append(conPres.ToString("#,0", ic)).Append(" (").Append((100.0 * conPres / m).ToString("0.0", ic)).Append(" %); con partes suaves en la sala buena (").Append(LraSalaLu.ToString("0", ic)).Append(" LU o mas): ").Append(conSala.ToString("#,0", ic)).Append(" (").Append((100.0 * conSala / m).ToString("0.0", ic)).Append(" %).\r\n");
        }

        // Lo que de verdad suena para esta cancion: los mandos tal cual (automatico apagado) o lo que decide el automatico.
        private AjusteLocal LocalDe(Track t)
        {
            if (localAuto)
            {
                return LocalAutomatico(tipoLocal, t == null ? -1f : RangoDeCancion(t.Path), t == null ? -1f : LraDeCancion(t.Path));
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
