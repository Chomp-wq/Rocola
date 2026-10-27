// AFINAR EL LOCAL CON EL CELULAR (27 de octubre de 2026, segunda tanda del modo billar). El EQ del billar (CadenaBillar.cs) esta hecho
// con lo que se sabe de las bocinas y de la nave, pero no con una medida: la inclinacion de agudos (InclinacionBillar) es una estimacion.
// Esta hoja deja medir el local de verdad con lo que hay a mano, un celular con una app de decibeles:
//
//  1) La rocola, callada, toca ruido rosa por bandas de una octava (63 Hz a 8 kHz), el mismo nivel en cada una (-20 dB eficaces por
//     canal, cada canal con su propio ruido para que dos bocinas no se sumen y se resten segun donde este uno parado).
//  2) El dueno, parado donde esta la gente, anota lo que marca la app en cada banda, y dice en que ponderacion mide la app (A, C o Z):
//     la A le quita mucho al grave (26 dB a 63 Hz) y se le devuelve aqui.
//  3) Con eso se calcula cuanto se aleja el local de una curva de casa para un bar (un poco mas de grave y un poco menos de agudo que
//     plano, CurvaCasa), contando desde la zona de la voz (250 Hz a 2 kHz). Se corrige el 70 % (un celular y un solo punto no son una
//     medida de laboratorio), lo de menos de 1 dB se deja, se BAJA hasta 6 dB y se SUBE como mucho 3 (2 en 8 kHz y nada en 63 Hz:
//     subir lo que la bocina no da solo gasta amplificador, y el microfono de un celular casi siempre lee de menos ahi).
//  4) Con el modo billar y el interruptor «Usar la afinacion» encendidos, esa correccion va dentro del EQ de cada cancion (las mismas
//     ocho bandas, sin filtros nuevos ni procesador de mas) y SUSTITUYE la inclinacion estimada. Va tambien en las canciones que el EQ
//     no toca (las de menos de 45 s): es del local, no de la cancion.
//
// Con el modo billar apagado o el interruptor apagado (de fabrica) nada cambia: la cadena sale bit a bit como antes.
using System;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        internal static readonly float[] AfinaCentros = { 63f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f };
        // Lo que la ponderacion de la app le quita a cada banda (dB, IEC 61672): A, C y Z (plana).
        internal static readonly float[][] AfinaPonderacion = {
            new float[] { -26.2f, -16.1f, -8.6f, -3.2f, 0f, 1.2f, 1.0f, -1.1f },
            new float[] { -0.8f, -0.2f, 0f, 0f, 0f, -0.2f, -0.8f, -3.0f },
            new float[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f } };
        internal static readonly string[] AfinaPonderacionNombre = { "A", "C", "Z" };
        // La curva de casa para un bar (dB frente a la zona de la voz): un poco mas de grave y menos de agudo que plano.
        internal static readonly float[] CurvaCasa = { 2f, 1.5f, 0.5f, 0f, 0f, -0.5f, -1.5f, -3f };
        internal static readonly float[] AfinaSubeMax = { 0f, 3f, 3f, 3f, 3f, 3f, 3f, 2f };
        internal const float AfinaBajaMax = -6f, AfinaFuerza = 0.7f, AfinaZonaMuerta = 1f;
        internal const int AfinaSinMedir = 39, AfinaMinDb = 40, AfinaMaxDb = 120;   // el deslizador: 39 = sin medir

        private static readonly float[] afinadoDb = { float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN };
        private static int afinadoPonderacion = 1;   // C de fabrica (la que se recomienda)
        private static bool afinadoUsar;              // "afinado_usar=": apagado de fabrica

        // LA CORRECCION por banda de afinado (dB), o null si faltan las bandas de la voz (250 a 2 kHz). Pura.
        internal static float[] CorreccionAfinada(float[] lecturas, int ponderacion)
        {
            if (lecturas == null || lecturas.Length < 8 || ponderacion < 0 || ponderacion > 2) return null;
            float[] m = new float[8]; double refSum = 0; int refN = 0;
            for (int b = 0; b < 8; b++)
            {
                float v = lecturas[b];
                m[b] = float.IsNaN(v) ? float.NaN : v - AfinaPonderacion[ponderacion][b];
                if (b >= 2 && b <= 5) { if (float.IsNaN(m[b])) return null; refSum += m[b] - CurvaCasa[b]; refN++; }
            }
            float referencia = (float)(refSum / refN);
            float[] c = new float[8];
            for (int b = 0; b < 8; b++)
            {
                if (float.IsNaN(m[b])) continue;
                float dif = (CurvaCasa[b] + referencia - m[b]) * AfinaFuerza;
                if (dif > AfinaZonaMuerta) dif -= AfinaZonaMuerta; else if (dif < -AfinaZonaMuerta) dif += AfinaZonaMuerta; else dif = 0f;
                if (dif > AfinaSubeMax[b]) dif = AfinaSubeMax[b]; else if (dif < AfinaBajaMax) dif = AfinaBajaMax;
                c[b] = dif;
            }
            return c;
        }

        // La misma correccion en las bandas del EQ de cada cancion (EqCentros), interpolada en octavas. 10 kHz se lleva la mitad de la
        // de 8 kHz (ya no se midio) y 15,8 kHz nada. Pura.
        internal static float[] AfinadaEnBandasEq(float[] c)
        {
            if (c == null) return null;
            float[] e = new float[8];
            for (int b = 0; b < 8; b++)
            {
                float f = EqCentros[b];
                if (f <= AfinaCentros[0]) e[b] = c[0];
                else if (f >= 15000f) e[b] = 0f;
                else if (f >= AfinaCentros[7]) e[b] = 0.5f * c[7];
                else
                {
                    int i = 0; while (i < 6 && f > AfinaCentros[i + 1]) i++;
                    double t = Math.Log(f / AfinaCentros[i]) / Math.Log(AfinaCentros[i + 1] / AfinaCentros[i]);
                    e[b] = (float)(c[i] + (c[i + 1] - c[i]) * t);
                }
            }
            return e;
        }

        // ¿Se usa la afinacion ahora? Solo con el modo billar, el interruptor y las medidas de la voz.
        private static bool AfinadaActiva { get { return localAuto && afinadoUsar && CorreccionAfinada(afinadoDb, afinadoPonderacion) != null; } }

        // El EQ que suena: el de la cancion y, en el billar con la afinacion, la del local encima (tope total -8 / +4 dB).
        internal const float AfinaTotalMin = -8f, AfinaTotalMax = 4f;
        private float[] ConLocalAfinado(float[] cancion)
        {
            if (!AfinadaActiva) return cancion;
            float[] sala = AfinadaEnBandasEq(CorreccionAfinada(afinadoDb, afinadoPonderacion));
            if (sala == null) return cancion;
            float[] t = new float[8]; bool any = false;
            for (int b = 0; b < 8; b++)
            {
                float v = sala[b] + (cancion != null ? cancion[b] : 0f);
                if (v > AfinaTotalMax) v = AfinaTotalMax; else if (v < AfinaTotalMin) v = AfinaTotalMin;
                t[b] = v; if (v > 0.1f || v < -0.1f) any = true;
            }
            return any ? t : cancion;
        }

        // La configuracion: "afinado_local=" con las ocho lecturas ("x" = sin medir) y "afinado_ponderacion=" A, C o Z.
        internal static string TextoAfinado()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int b = 0; b < 8; b++)
            {
                if (b > 0) sb.Append(';');
                float v = afinadoDb[b];
                sb.Append(float.IsNaN(v) ? "x" : v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
        internal static void LeeAfinado(string texto)
        {
            if (texto == null) return;
            string[] p = texto.Split(';');
            for (int b = 0; b < 8; b++)
            {
                float v;
                afinadoDb[b] = (b < p.Length && float.TryParse(p[b].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)
                                && v >= AfinaMinDb && v <= AfinaMaxDb) ? v : float.NaN;
            }
        }
        internal static int PonderacionDe(string texto)
        {
            string t = (texto ?? "").Trim().ToUpperInvariant();
            return t == "A" ? 0 : t == "Z" ? 2 : 1;
        }

        // Lo que dice la hoja: la correccion de cada banda, o lo que falta medir.
        internal static string TextoCorreccionAfinada()
        {
            float[] c = CorreccionAfinada(afinadoDb, afinadoPonderacion);
            if (c == null)
            {
                string falta = "";
                for (int b = 2; b <= 5; b++) if (float.IsNaN(afinadoDb[b])) falta += (falta.Length > 0 ? ", " : "") + NombreBanda(b);
                return "Faltan las bandas de la voz: " + falta + ".\nSin ellas no hay con que comparar el resto.";
            }
            string s = "Correccion para tu local (dB):\n";
            for (int b = 0; b < 8; b++)
            {
                s += NombreBanda(b) + " " + (float.IsNaN(afinadoDb[b]) ? "sin medir" : c[b].ToString("+0.0;-0.0;0", System.Globalization.CultureInfo.InvariantCulture));
                s += b == 3 ? "\n" : (b < 7 ? "   " : "");
            }
            return s;
        }
        internal static string NombreBanda(int b) { float f = AfinaCentros[b]; return f >= 1000f ? (f / 1000f).ToString("0") + " kHz" : f.ToString("0") + " Hz"; }

        // ---------------------------------------------------------------- EL RUIDO POR BANDAS
        private PruebaBanda bandaSonando;
        internal string ProbarBanda(int b)
        {
            if (nowPlaying != null || videoOn) return "Hay musica sonando: la medida es con la rocola callada.";
            if (b < 0 || b > 7) return "";
            ParaPruebaBocinas();
            PruebaBanda p = new PruebaBanda(b);
            IWavePlayer w = null;
            try
            {
                w = CreaWaveOut();
                w.Init(new NAudio.Wave.SampleProviders.SampleToWaveProvider16(p));
                pruebaSalida = w; bandaSonando = p;
                w.PlaybackStopped += PruebaAcabo;
                w.Play();
                return "Sonando: " + NombreBanda(b) + ".";
            }
            catch (Exception ex)
            {
                if (w != null) { try { w.PlaybackStopped -= PruebaAcabo; } catch { } try { w.Dispose(); } catch { } }
                pruebaSalida = null; bandaSonando = null;
                LogError("Afinar el local", ex);
                return "No se pudo abrir la salida de sonido: " + MotivoFallo(ex) + ".";
            }
        }
        internal string EstadoBanda()
        {
            PruebaBanda p = bandaSonando;
            if (p == null || pruebaSalida == null) return "";
            return "Sonando: " + NombreBanda(p.Banda) + ". Anota lo que marca la app.";
        }

        // Ruido rosa en una octava (dos paso-banda de Robert Bristow-Johnson en cascada), cada canal con su ruido, a -20 dB eficaces por
        // canal: el nivel se mide al armarlo (un segundo de ese mismo ruido) y se ajusta, asi todas las bandas salen igual.
        internal sealed class PruebaBanda : ISampleProvider
        {
            internal const int Tasa = 44100;
            internal const double NivelDb = -20.0, Segundos = 8.0, FundidoSeg = 0.05;
            private readonly WaveFormat formato = WaveFormat.CreateIeeeFloatWaveFormat(Tasa, 2);
            private readonly int banda;
            private readonly long total;
            private long pos;
            private readonly Generador gl, gr;
            private readonly double nivel;
            internal PruebaBanda(int b)
            {
                banda = b; total = (long)(Tasa * Segundos);
                gl = new Generador(AfinaCentros[b], 31337u); gr = new Generador(AfinaCentros[b], 4242u);
                Generador cal = new Generador(AfinaCentros[b], 777u);
                for (int i = 0; i < Tasa / 2; i++) cal.Siguiente();   // que se asiente
                double s = 0; for (int i = 0; i < Tasa; i++) { double v = cal.Siguiente(); s += v * v; }
                double ef = Math.Sqrt(s / Tasa);
                nivel = ef > 1e-12 ? Math.Pow(10.0, NivelDb / 20.0) / ef : 0.0;
                for (int i = 0; i < Tasa / 2; i++) { gl.Siguiente(); gr.Siguiente(); }
            }
            internal int Banda { get { return banda; } }
            internal double Nivel { get { return nivel; } }
            public WaveFormat WaveFormat { get { return formato; } }
            public int Read(float[] buffer, int offset, int count)
            {
                int cuadros = count / 2, hechos = 0;
                long fundido = (long)(Tasa * FundidoSeg);
                for (; hechos < cuadros && pos < total; hechos++, pos++)
                {
                    double env = 1.0;
                    if (pos < fundido) env = pos / (double)fundido;
                    else if (total - pos < fundido) env = (total - pos) / (double)fundido;
                    buffer[offset + 2 * hechos] = (float)(gl.Siguiente() * nivel * env);
                    buffer[offset + 2 * hechos + 1] = (float)(gr.Siguiente() * nivel * env);
                }
                return hechos * 2;
            }

            internal sealed class Generador
            {
                private uint semilla;
                private double b0, b1, b2, b3, b4, b5, b6;
                private readonly double c0, c1, c2, d1, d2;
                private double x1, x2, y1, y2, z1, z2, w1, w2;
                internal Generador(double fc, uint s)
                {
                    semilla = s;
                    double w0 = 2.0 * Math.PI * fc / Tasa, bw = 1.0;   // una octava
                    double alfa = Math.Sin(w0) * Math.Sinh(Math.Log(2.0) / 2.0 * bw * w0 / Math.Sin(w0));
                    double a0 = 1.0 + alfa;
                    c0 = alfa / a0; c1 = 0.0; c2 = -alfa / a0; d1 = -2.0 * Math.Cos(w0) / a0; d2 = (1.0 - alfa) / a0;
                }
                internal double Siguiente()
                {
                    semilla = semilla * 1664525u + 1013904223u;
                    double w = (semilla >> 8) * (2.0 / 16777216.0) - 1.0;
                    b0 = 0.99886 * b0 + w * 0.0555179; b1 = 0.99332 * b1 + w * 0.0750759; b2 = 0.96900 * b2 + w * 0.1538520;
                    b3 = 0.86650 * b3 + w * 0.3104856; b4 = 0.55000 * b4 + w * 0.5329522; b5 = -0.7616 * b5 - w * 0.0168980;
                    double p = (b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362) * 0.11; b6 = w * 0.115926;
                    double y = c0 * p + c1 * x1 + c2 * x2 - d1 * y1 - d2 * y2; x2 = x1; x1 = p; y2 = y1; y1 = y;
                    double z = c0 * y + c1 * z1 + c2 * z2 - d1 * w1 - d2 * w2; z2 = z1; z1 = y; w2 = w1; w1 = z;
                    return z;
                }
            }
        }
    }
}
