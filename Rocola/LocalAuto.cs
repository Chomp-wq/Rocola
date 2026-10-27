// EL LOCAL EN AUTOMATICO (23 de octubre de 2026). Un solo interruptor que enciende y ajusta por ti las mejoras del local
// (el subgrave, el estereo estrecho, la claridad, las partes suaves y la presencia en sala ruidosa) para que suene lo mejor
// posible, sin tocar los mandos uno por uno. Sin microfono: decide con lo que el repaso midio de cada cancion.
//
// SOLO PARA ESTE LOCAL (25-10-2026, el dueno: «que el modo automatico sea solo para mi configuracion de bocinas, las demas no me
// importan»). Ya no hay tipos de local (la clave tipo_local= se ignora): esta hecho para el billar -bocinas repartidas por zonas,
// sin subwoofer, con gente y bolas-. Lo que pone siempre:
//   - SIN SUBGRAVE: ninguna bocina del billar da menos de 30 Hz; solo gastaria amplificador.
//   - ESTEREO ESTRECHO SOLO DONDE HACE FALTA (S3): con las bocinas por zonas, lo grabado a un solo lado se oye en todas.
//   - CLARIDAD: menos retumbe (300 Hz) y la voz mas presente (3 kHz). Graves y agudos del local en 0.
//   - PARTES SUAVES (P1, 23-10-2026): siempre; la etapa se frena sola por nivel (solo sube lo que va 12 dB bajo el nivel de
//     siempre, hasta 4 dB): en una cancion pareja no hace nada, y la que tiene una intro o una estrofa bajita la recibe.
// Y la PRESENCIA EN SALA RUIDOSA cancion por cancion, por su rango de sonoridad (LRA, EBU Tech 3342: cuanto varia el volumen en el
// tiempo; P2, 23-10-2026). Una cancion pareja y fuerte no la necesita y la presencia la bajaria ~1 dB.
//   - L5 (25-10-2026): el corte ya no es fijo. La llevan las canciones del TERCIO que mas varia de ESTA biblioteca (el percentil
//     66,7 de los LRA medidos, a 0,1 LU), acotado entre 6 y 14 LU. Con menos de 200 canciones con LRA vale el de antes (10 LU). Se
//     rehace como mucho cada 10 minutos mientras el repaso mide (un histograma, en el hilo de la pantalla: UmbralPresenciaAhora).
//   - Una cancion con medida de antes (sin LRA) usa la regla vieja de P99 - sonoridad (16 dB) hasta que el repaso la vuelva a
//     medir; sin medida, sin presencia.
//   - L1 (25-10-2026): en racha con otra de su disco decide el DISCO (el LRA mas alto de sus pistas medidas), lo decidido para una
//     cancion se fija al armarla y no cambia a media cancion (CadenaCancion.AutoFijo), y en un disco continuo la siguiente lo hereda
//     (HeredaLocal). Antes, si una pista llevaba presencia y la siguiente no, el volumen saltaba hasta 3 dB en el cambio de pista.
//
//   - VOZ, GOLPE Y RETUMBE (27-10-2026, VozGolpeYRetumbe.cs): que el bombo no hunda la voz, el retumbe dinamico y el ataque mas claro, siempre
//     (cada una se frena sola por lo que oye; ninguna necesita una regla por cancion). Pendiente de oirlas en el bar para decidir si siguen aqui.
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
        // Para que Ctrl+F3 diga para que esta hecho.
        internal const string NombreLocal = "bocinas repartidas por zonas, sin subwoofer";
        // El rango (dB entre el P99 de los picos y la sonoridad integrada) desde el que una cancion cuenta como de mucho rango (informe),
        // y desde el que lleva la presencia una con medida de antes (sin LRA). Un disco de pop actual ronda 6 a 10 dB; una balada, 14 o mas.
        internal const float RangoAltoDb = 12f, RangoMuyAltoDb = 16f;
        // L5: la presencia para el tercio de la biblioteca que mas varia; el corte, entre LraUmbralMin y LraUmbralMax; con menos de
        // LraMinMedidas, el fijo de antes (LraPresenciaLu). Una balada o una cancion con intro suave ronda 11 a 18 LU; un tema parejo, < 3.
        internal const double PresenciaParte = 1.0 / 3.0;
        internal const float LraPresenciaLu = 10f, LraUmbralMin = 6f, LraUmbralMax = 14f;
        internal const int LraMinMedidas = 200;
        internal const float LraPaso = 0.1f; internal const int LraCasillas = 600;   // el histograma: de 0 a 60 LU, de 0,1 en 0,1
        private const int UmbralRehacerMs = 600000;                                   // con medidas nuevas, como mucho cada 10 minutos

        internal struct AjusteLocal
        {
            public bool Subgrave, Estrecho, EstrechoSoloSiHaceFalta, Claridad, Suaves, Presencia;
            public bool Bombo;   // voz, golpe y retumbe (VozGolpeYRetumbe.cs, 27-10-2026)
            public int Graves, Agudos;
        }

        // Lo que el modo automatico pone para una cancion: 'rangoDb' es el P99 - sonoridad (< 0 = sin medir), 'lraLu' su LRA (< 0 = sin
        // LRA: medida de antes) y 'umbralLu' el corte de la presencia (UmbralPresencia). Pura: sin estado.
        internal static AjusteLocal LocalAutomatico(float rangoDb, float lraLu, float umbralLu)
        {
            AjusteLocal a = new AjusteLocal();
            bool medida = rangoDb >= 0f && !float.IsNaN(rangoDb);
            bool conLra = lraLu >= 0f && !float.IsNaN(lraLu) && !float.IsInfinity(lraLu);
            if (float.IsNaN(umbralLu) || float.IsInfinity(umbralLu)) umbralLu = LraPresenciaLu;
            a.Subgrave = true; a.Estrecho = true; a.EstrechoSoloSiHaceFalta = true; a.Claridad = true; a.Suaves = true;
            a.Presencia = conLra ? lraLu >= umbralLu : (medida && rangoDb >= RangoMuyAltoDb);
            // Voz, golpe y retumbe (27-10-2026): siempre, en el billar. Cada etapa se frena sola por lo que oye (el bombo solo baja el bajo cuando algo pasa del
            // techo; el retumbe solo corta cuando el grave medio se pasa de su propia media; el ataque solo actua con la cola de un golpe, y sube solo con
            // sitio bajo el techo): ninguna necesita una medida de la cancion, asi que no hay regla por cancion.
            a.Bombo = true;
            return a;
        }

        // L5: el corte de LRA con el histograma de la biblioteca (casillas de LraPaso): el valor desde el que queda PresenciaParte de las
        // canciones, acotado. Con menos de LraMinMedidas, el de antes. Pura.
        internal static float UmbralPresencia(int[] cuenta, int n)
        {
            if (cuenta == null || n < LraMinMedidas) return LraPresenciaLu;
            long debajo = (long)Math.Ceiling(n * (1.0 - PresenciaParte) - 1e-9);   // las que quedan por debajo del corte (600 * 2/3 da 400,00000000000006)
            long acum = 0; int b = 0;
            for (; b < cuenta.Length - 1; b++) { acum += cuenta[b]; if (acum >= debajo) break; }
            float u = (float)Math.Round((b + 1) * LraPaso, 1);   // el borde de arriba de la casilla donde se completan
            return Math.Max(LraUmbralMin, Math.Min(LraUmbralMax, u));
        }
        internal static int CasillaLra(float lra)
        {
            int b = (int)(lra / LraPaso);
            return b < 0 ? 0 : (b >= LraCasillas ? LraCasillas - 1 : b);
        }

        private float umbralLra = float.NaN; private long umbralCambios = -1; private int umbralCuando, umbralMedidas; private object umbralTabla;
        // El corte de la presencia para esta biblioteca, guardado. En el hilo de la pantalla (integCambios es suyo). 'medidas': con cuantas.
        internal float UmbralPresenciaAhora(out int medidas)
        {
            System.Collections.Generic.Dictionary<string, SonoridadIntegrada> t = integCache;
            int ahora = Environment.TickCount;
            bool rehacer = !(umbralLra > 0f) || !object.ReferenceEquals(umbralTabla, t)
                           || (umbralCambios != integCambios && unchecked(ahora - umbralCuando) >= UmbralRehacerMs);
            if (rehacer)
            {
                int[] cuenta = new int[LraCasillas]; int n = 0;
                if (t != null)
                    lock (integCerrojo)
                        foreach (SonoridadIntegrada s in t.Values)
                        {
                            if (!s.Valida || s.Muda || !s.ConLra || float.IsNaN(s.Lra) || float.IsInfinity(s.Lra)) continue;
                            cuenta[CasillaLra(s.Lra)]++; n++;
                        }
                umbralLra = UmbralPresencia(cuenta, n); umbralMedidas = n;
                umbralTabla = t; umbralCambios = integCambios; umbralCuando = ahora;
            }
            medidas = umbralMedidas;
            return umbralLra;
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
            // El corte de la presencia con ESTA biblioteca (L5): la misma cuenta que usa el modo automatico.
            int[] hist = new int[LraCasillas];
            for (int i = 0; i < m; i++) if (!float.IsNaN(lras[i]) && !float.IsInfinity(lras[i])) hist[CasillaLra(lras[i])]++;
            float corte = UmbralPresencia(hist, m);
            int conPres = 0;
            for (int i = 0; i < m; i++) if (lras[i] >= corte) conPres++;
            sb.Append("  Mediana ").Append(lras[m / 2].ToString("0.0", ic)).Append(" LU. Presencia en el billar desde ").Append(corte.ToString("0.0", ic))
              .Append(m < LraMinMedidas ? " LU (el de siempre: aun hay menos de " + LraMinMedidas + " medidas)" : " LU (el tercio que mas varia de esta biblioteca)")
              .Append(": ").Append(conPres.ToString("#,0", ic)).Append(" canciones (").Append((100.0 * conPres / m).ToString("0.0", ic)).Append(" %).\r\n");
        }

        // Lo que de verdad suena para esta cancion: los mandos tal cual (automatico apagado) o lo que decide el automatico.
        private AjusteLocal LocalDe(Track t)
        {
            if (localAuto)
            {
                int n;
                return LocalAutomatico(t == null ? -1f : RangoDeCancion(t.Path), t == null ? -1f : LraParaDecidir(t), UmbralPresenciaAhora(out n));
            }
            AjusteLocal m = new AjusteLocal();
            m.Subgrave = cortaSubgrave; m.Estrecho = estereoEstrecho; m.EstrechoSoloSiHaceFalta = estereoAuto;
            m.Claridad = claridadLocal; m.Suaves = partesSuaves; m.Presencia = presenciaSala;
            m.Graves = gravesLocalDb; m.Agudos = agudosLocalDb;
            m.Bombo = bomboSinHundir;
            return m;
        }
        // L1: en racha con otra de su disco, el LRA mas alto de las pistas medidas del disco (todas deciden igual); si no, el suyo.
        private float LraParaDecidir(Track t)
        {
            float propio = LraDeCancion(t.Path);
            if (queue == null) return propio;
            System.Collections.Generic.List<Track> disco = RachaDeDisco(t);
            if (disco == null) return propio;
            float mayor = propio;
            for (int i = 0; i < disco.Count; i++)
            {
                Track tr = disco[i];
                if (tr == null || tr.Path == null) continue;
                float l = LraDeCancion(tr.Path);
                if (l > mayor) mayor = l;
            }
            return mayor;
        }
        // La de una cadena ya armada: lo del automatico se fija la primera vez que se pregunta (al armarla) y no cambia a media cancion.
        private AjusteLocal LocalDe(CadenaCancion c)
        {
            if (c == null) return LocalDe((Track)null);
            AjusteLocal a = LocalDe(c.Cancion);
            if (!localAuto) return a;
            if (!c.AutoFijo) { c.AutoFijo = true; c.AutoPresencia = a.Presencia; c.AutoSuaves = a.Suaves; }
            a.Presencia = c.AutoPresencia; a.Suaves = c.AutoSuaves;
            return a;
        }
        // L1, disco continuo: la que entra sin pausa sigue con lo que el automatico decidio para la que acaba, y se le pone ya.
        private void HeredaLocal(CadenaCancion nueva, CadenaCancion vieja)
        {
            if (nueva == null || vieja == null || !localAuto) return;
            AjusteLocal v = LocalDe(vieja);
            nueva.AutoFijo = true; nueva.AutoPresencia = v.Presencia; nueva.AutoSuaves = v.Suaves;
            if (sinMejoras) return;   // «Escuchar sin mejoras» ya las tiene apagadas (AplicaSinMejoras)
            AjusteLocal n = LocalDe(nueva);
            if (nueva.Sala != null) nueva.Sala.Ajusta(n.Claridad, n.Suaves, balanceDb);
            if (nueva.Presencia != null) nueva.Presencia.Ajusta(n.Presencia);
        }
    }
}
