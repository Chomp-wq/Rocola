// LAS PRUEBAS DEL LOCAL CON LA ROCOLA CALLADA (en XP, la prueba de bocinas de SalaLocal.cs y el ruido por bandas de AfinarLocal.cs; las
// clases, copiadas tal cual). En XP cada una abre su propia salida; en Rocola OS el motor la deja lista (PruebaPorSonar) y el programa la
// toca por ALSA, sin pasar por la cadena, solo con nada sonando; se para al salir de la hoja o al pedir otra.
using System;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        public enum PruebaModo { Izquierda, Derecha, Ambas, Polaridad }

        private ISampleProvider pruebaActual;   // la que debe sonar (el programa la toca por ALSA); null = ninguna
        private PruebaBocinas pruebaSonando;
        private PruebaBanda bandaSonando;
        private const int PruebaRetrasoMs = 400;   // lo que guarda la salida ALSA (300 ms) y su bloque: para decir lo que se oye
        public ISampleProvider PruebaActual { get { return pruebaActual; } }

        // Suena la prueba de bocinas (y para la que sonara). Devuelve lo que hay que decir.
        public string ProbarBocinas(PruebaModo m)
        {
            if (nowPlaying != null) return "Hay musica sonando: la prueba es con la rocola callada.";
            ParaPrueba();
            pruebaSonando = new PruebaBocinas(m); pruebaActual = pruebaSonando;
            return "Sonando: " + NombrePrueba(m) + ".";
        }
        // Las bandas de afinar el local, de grave a agudo (AfinarLocal.cs de XP: ProbarBandas).
        public string ProbarBandas(int desde, int hasta)
        {
            if (nowPlaying != null) return "Hay musica sonando: la medida es con la rocola callada.";
            if (desde < 0 || hasta > 7 || desde > hasta) return "";
            ParaPrueba();
            bandaSonando = new PruebaBanda(desde, hasta); pruebaActual = bandaSonando;
            return desde == hasta ? "Sonando: " + NombreBanda(desde) + "." : "Sonando las " + (hasta - desde + 1) + " bandas seguidas.";
        }
        public void ParaPrueba() { pruebaActual = null; pruebaSonando = null; bandaSonando = null; }
        // El programa avisa que esa termino sola.
        public void PruebaAcabo(ISampleProvider p) { if (object.ReferenceEquals(p, pruebaActual)) ParaPrueba(); }
        public bool ProbandoBocinas { get { return pruebaSonando != null; } }
        public bool ProbandoBandas { get { return bandaSonando != null; } }

        // Que esta sonando ahora ("" si nada), como EstadoPrueba de XP.
        public string EstadoPrueba()
        {
            PruebaBocinas p = pruebaSonando;
            if (p == null) return "";
            if (p.Modo == PruebaModo.Polaridad) return p.AlReves(PruebaRetrasoMs) ? "Polaridad: AL REVES (una de las dos invertida)." : "Polaridad: IGUAL por las dos.";
            return "Sonando: " + NombrePrueba(p.Modo) + ".";
        }
        // En grande: la banda que suena ("250 Hz   3 de 8"), o "" si nada (BandaGrande de XP).
        public string BandaGrande()
        {
            PruebaBanda p = bandaSonando;
            if (p == null) return "";
            int b = p.BandaOida(PruebaRetrasoMs);
            if (b < 0) return "";
            return NombreBanda(b) + (p.Varias ? "   " + (b - p.Desde + 1) + " de " + p.Cuantas : "");
        }
        // Lo que dice la hoja de afinar: la banda que suena y la correccion (o lo que falta).
        public string TextoAfinar()
        {
            string g = BandaGrande();
            return (g.Length > 0 ? "Suena: " + g + "   (anota lo que marca tu app)\n" : "") + TextoCorreccionAfinada();
        }

        public static string NombrePrueba(PruebaModo m)
        {
            return m == PruebaModo.Izquierda ? "solo la izquierda" : m == PruebaModo.Derecha ? "solo la derecha"
                 : m == PruebaModo.Ambas ? "las dos" : "la polaridad";
        }

        internal sealed class PruebaBocinas : ISampleProvider
        {
            internal const int Tasa = 44100;
            internal const double NivelDb = -20.0;          // ruido rosa a -20 dB (eficaz): como musica bajita
            internal const double SegundosCanal = 3.0, SegundosFase = 2.0, FundidoSeg = 0.04, CruceSeg = 0.03;
            internal const double GraveHz = 160.0;           // la de polaridad: solo grave, que es donde se nota
            internal const double RosaEficaz = 0.2, GraveRepone = 1.56;   // medidos (pruebas/salalocal.cs): -20 dB las dos
            private readonly WaveFormat formato = WaveFormat.CreateIeeeFloatWaveFormat(Tasa, 2);
            private readonly PruebaModo modo;
            private readonly long total;
            private long pos;
            private uint semilla = 22222u;
            private double b0, b1, b2, b3, b4, b5, b6;       // el filtro del ruido rosa (Paul Kellet)
            private double g1, g2;                           // el paso-bajo del grave (dos polos)
            private readonly double aGrave, nivel, nivelGrave;

            internal PruebaBocinas(PruebaModo m)
            {
                modo = m;
                total = (long)(Tasa * (m == PruebaModo.Polaridad ? 4 * SegundosFase : SegundosCanal));
                aGrave = 1.0 - Math.Exp(-2.0 * Math.PI * GraveHz / Tasa);
                nivel = Math.Pow(10.0, NivelDb / 20.0) / RosaEficaz;   // asi sale a -20 dB eficaces (medido)
                nivelGrave = nivel * GraveRepone;                       // el paso-bajo se queda con parte: se repone (medido)
            }
            public WaveFormat WaveFormat { get { return formato; } }
            internal PruebaModo Modo { get { return modo; } }
            internal long Total { get { return total; } }
            // Lo que hay que decir mientras suena: "igual" o "al reves" en la de polaridad. Lo leido va por delante de lo que
            // se oye lo que guarda la salida (hasta un segundo): se cuenta con ese retraso, para que el letrero diga lo que
            // suena y no lo que viene.
            internal bool AlReves(int retrasoMs)
            {
                if (modo != PruebaModo.Polaridad) return false;
                long oido = pos - (long)Math.Max(0, retrasoMs) * Tasa / 1000;
                if (oido < 0) oido = 0;
                return ((oido / (long)(Tasa * SegundosFase)) & 1) == 1;
            }

            private double Blanco()
            {
                semilla = semilla * 1664525u + 1013904223u;   // congruencial: el mismo ruido cada vez, sin reservar nada
                return (semilla >> 8) * (2.0 / 16777216.0) - 1.0;
            }
            private double Rosa()
            {
                double w = Blanco();
                b0 = 0.99886 * b0 + w * 0.0555179; b1 = 0.99332 * b1 + w * 0.0750759; b2 = 0.96900 * b2 + w * 0.1538520;
                b3 = 0.86650 * b3 + w * 0.3104856; b4 = 0.55000 * b4 + w * 0.5329522; b5 = -0.7616 * b5 - w * 0.0168980;
                double p = b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362; b6 = w * 0.115926;
                return p * 0.11;   // la escala de la receta; el nivel final lo fija RosaEficaz
            }

            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                int cuadros = count / 2, hechos = 0;
                long fase = (long)(Tasa * SegundosFase), fundido = (long)(Tasa * FundidoSeg), cruce = (long)(Tasa * CruceSeg);
                for (; hechos < cuadros && pos < total; hechos++, pos++)
                {
                    double env = 1.0;
                    if (pos < fundido) env = pos / (double)fundido;
                    else if (total - pos < fundido) env = (total - pos) / (double)fundido;
                    double l, r;
                    if (modo == PruebaModo.Polaridad)
                    {
                        double x = Rosa();
                        g1 += (x - g1) * aGrave; g2 += (g1 - g2) * aGrave;
                        double v = g2 * nivelGrave * env;
                        // Cada 2 s cambia: igual, al reves, igual, al reves. En cada cambio las dos bajan a cero y vuelven
                        // en 30 ms: dar la vuelta a un canal de golpe sonaria como un golpe.
                        long seg = pos / fase, enFase = pos - seg * fase, ultimo = total / fase - 1;
                        long dIni = seg == 0 ? long.MaxValue : enFase, dFin = seg >= ultimo ? long.MaxValue : fase - enFase;
                        double suave = Math.Min(1.0, Math.Min(dIni, dFin) / (double)cruce);
                        double signo = (seg & 1) == 1 ? -1.0 : 1.0;
                        l = v * suave; r = v * signo * suave;
                    }
                    else
                    {
                        double x = Rosa() * nivel * env;
                        l = modo == PruebaModo.Derecha ? 0.0 : x;
                        r = modo == PruebaModo.Izquierda ? 0.0 : x;
                    }
                    buffer[offset + 2 * hechos] = (float)l; buffer[offset + 2 * hechos + 1] = (float)r;
                }
                return hechos * 2;
            }
        }

        internal sealed class PruebaBanda : ISampleProvider
        {
            internal const int Tasa = 44100;
            internal const double NivelDb = -20.0, Segundos = 8.0, PausaSeg = 1.5, FundidoSeg = 0.05;
            private readonly WaveFormat formato = WaveFormat.CreateIeeeFloatWaveFormat(Tasa, 2);
            private readonly int desde, cuantas;
            private readonly long porBanda, suena, total;
            private long pos;
            private readonly Generador[] gl, gr;
            private readonly double[] nivel;
            internal PruebaBanda(int b) : this(b, b) { }
            internal PruebaBanda(int desdeB, int hastaB)
            {
                desde = desdeB; cuantas = hastaB - desdeB + 1;
                suena = (long)(Tasa * Segundos);
                porBanda = cuantas > 1 ? suena + (long)(Tasa * PausaSeg) : suena;
                total = porBanda * (cuantas - 1) + suena;
                gl = new Generador[cuantas]; gr = new Generador[cuantas]; nivel = new double[cuantas];
                for (int k = 0; k < cuantas; k++)
                {
                    float fc = AfinaCentros[desde + k];
                    gl[k] = new Generador(fc, 31337u + (uint)k); gr[k] = new Generador(fc, 4242u + (uint)k);
                    Generador cal = new Generador(fc, 777u);
                    for (int i = 0; i < Tasa / 2; i++) cal.Siguiente();   // que se asiente
                    double s = 0; for (int i = 0; i < Tasa; i++) { double v = cal.Siguiente(); s += v * v; }
                    double ef = Math.Sqrt(s / Tasa);
                    nivel[k] = ef > 1e-12 ? Math.Pow(10.0, NivelDb / 20.0) / ef : 0.0;
                    for (int i = 0; i < Tasa / 2; i++) { gl[k].Siguiente(); gr[k].Siguiente(); }
                }
            }
            internal int Banda { get { return desde; } }
            internal int Desde { get { return desde; } }
            internal int Cuantas { get { return cuantas; } }
            internal bool Varias { get { return cuantas > 1; } }
            internal double Nivel { get { return nivel[0]; } }
            internal double NivelDe(int k) { return nivel[k]; }
            internal long Total { get { return total; } }
            // La banda que se oye con 'retrasoMs' de salida por delante (-1: en un silencio entre bandas o antes de empezar).
            internal int BandaOida(int retrasoMs)
            {
                long oido = pos - (long)Math.Max(0, retrasoMs) * Tasa / 1000;
                if (oido < 0) return -1;
                long k = oido / porBanda;
                if (k >= cuantas) k = cuantas - 1;
                if (oido - k * porBanda >= suena) return -1;
                return desde + (int)k;
            }
            public WaveFormat WaveFormat { get { return formato; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                int cuadros = count / 2, hechos = 0;
                long fundido = (long)(Tasa * FundidoSeg);
                for (; hechos < cuadros && pos < total; hechos++, pos++)
                {
                    int k = (int)(pos / porBanda); long en = pos - k * porBanda;
                    double l = 0.0, r = 0.0;
                    if (en < suena)
                    {
                        double env = 1.0;
                        if (en < fundido) env = en / (double)fundido;
                        else if (suena - en < fundido) env = (suena - en) / (double)fundido;
                        l = gl[k].Siguiente() * nivel[k] * env; r = gr[k].Siguiente() * nivel[k] * env;
                    }
                    buffer[offset + 2 * hechos] = (float)l;
                    buffer[offset + 2 * hechos + 1] = (float)r;
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
        // de AfinarLocal.cs (XP), tal cual
        internal static string TextoAfinado()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            float[] d = afinadoDb;
            for (int b = 0; b < 8; b++)
            {
                if (b > 0) sb.Append(';');
                float v = d[b];
                sb.Append(float.IsNaN(v) ? "x" : v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
        internal static string TextoCorreccionAfinada()
        {
            float[] prom = AfinadoPromedio();
            float[] c = CorreccionAfinada(prom, afinadoPonderacion);
            if (c == null)
            {
                string falta = "";
                for (int b = 2; b <= 5; b++) if (float.IsNaN(prom[b])) falta += (falta.Length > 0 ? ", " : "") + NombreBanda(b);
                return "Faltan las bandas de la voz: " + falta + ".\nSin ellas no hay con que comparar el resto.";
            }
            string s = "Correccion para tu local (dB):\n";
            for (int b = 0; b < 8; b++)
            {
                s += NombreBanda(b) + " " + (float.IsNaN(prom[b]) ? "sin medir" : c[b].ToString("+0.0;-0.0;0", System.Globalization.CultureInfo.InvariantCulture));
                s += b == 3 ? "\n" : (b < 7 ? "   " : "");
            }
            return s;
        }
        internal static readonly string[] AfinaPonderacionNombre = { "A", "C", "Z" };
        internal static string NombreBanda(int b) { float f = AfinaCentros[b]; return f >= 1000f ? (f / 1000f).ToString("0") + " kHz" : f.ToString("0") + " Hz"; }
    }
}
