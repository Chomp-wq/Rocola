// EL ZUMBIDO DE LA CORRIENTE (T6, 25 de octubre de 2026).
//
// Hay grabaciones viejas, o pasadas de vinilo y de casete con un equipo mal aterrizado, que traen el zumbido de la red: 60 Hz (50 si
// vienen de Europa) y sus armonicos. Con los graves del local subidos y el bajo psicoacustico, ese "mmm" de fondo se refuerza, y en las
// partes calladas se oye solo.
//
// LA MEDIDA (MedidorZumbido). Va dentro del medidor de sonoridad (MedidorBs1770), asi que la hace el repaso de la biblioteca y tambien
// la medida de una cancion que suena entera, y se guarda con ella (algoritmo 6, rocola_sonoridad.txt). Se baja la suma de los canales a
// unos 1400 Hz (tres medias moviles seguidas, un CIC de orden 3: lo que podria caer sobre las lineas al bajar la tasa queda 55 dB o mas
// por debajo, y cuesta unas sumas por muestra) y, en ventanas de 2 s con ventana de Hann (medio hercio de paso), se mide con
// Goertzel la linea exacta de 50 y 60 Hz, sus armonicos 2.o y 3.o, y a 4 Hz de cada lado. Lo que cuenta es cuanto sobresale la linea
// sobre sus vecinas, y solo en el 20 % mas callado de la cancion (lo que suena por encima de -70 dBFS): ahi es donde el zumbido se oye y
// donde la musica no lo tapa. Hay zumbido si en esas ventanas la fundamental o el 2.o armonico sobresalen de media 10 dB o mas, en al
// menos el 70 % de ellas, y con un nivel de mas de -80 dBFS. Una nota de bajo larga no es una linea exacta en 60 Hz que dure toda la
// cancion en lo callado: no cuenta. La memoria es fija (casillas de 1 dB de la sonoridad de cada ventana).
//
// LA ETAPA (QuitaZumbido). Muescas estrechas (2 Hz de ancho) en la fundamental y sus tres armonicos siguientes, solo en las canciones
// con zumbido medido y con el interruptor encendido ("quita_zumbido=", Ctrl+F3 -> Sonido -> Lo que suena ahora; encendido de fabrica,
// como S1 y S2, porque solo actua con el defecto). Fuera de esos 2 Hz no toca nada; las demas canciones no la llevan (bit a bit igual).
using System;
using NAudio.Wave;

namespace Rocola
{
    internal sealed class MedidorZumbido
    {
        internal const double VentanaS = 2.0, VecinoHz = 4.0, TasaBaja = 1400.0;
        internal static readonly int[] Fundamentales = { 50, 60 };
        internal const int Armonicos = 3;                    // se miden la fundamental, el 2.o y el 3.o
        internal const int Casillas = 110;                   // sonoridad de la ventana en dBFS, de -110 a 0, de 1 en 1
        internal const double PisoDb = -70.0;                // ventanas mas calladas: silencio, no cuentan
        internal const double CalladoFraccion = 0.20;        // el 20 % mas callado
        internal const double SobresaleDb = 10.0, SobresaleMinDb = 6.0, Constancia = 0.70, NivelMinDb = -80.0;
        internal const int VentanasMin = 3;
        private const int Lineas = 2 * Armonicos;            // fundamental x armonico
        private const int Filtros = 3 * Lineas;              // cada linea: ella y sus dos vecinas

        private readonly int canales, diezmo, largoVentana;
        private readonly double invDiezmo;
        private readonly double[] r1, r2, r3;                // las tres medias moviles (CIC de orden 3) del diezmo
        private double s1, s2, s3;
        private int rPos, faseDiezmo, fase;
        private double sumaCuadro;                           // el cuadro a medio sumar (lecturas partidas)
        private readonly double[] hann;
        private readonly double sumaHann;
        private readonly double[] coef = new double[Filtros];
        private readonly double[] g1 = new double[Filtros], g2 = new double[Filtros];
        private int nVentana;
        private double energia; private long muestras;       // la potencia de la suma en la ventana (a la tasa de la cancion)
        private readonly int[] cuenta = new int[Casillas];
        private readonly double[] sumaSobresale = new double[Casillas * Lineas];
        private readonly int[] constantes = new int[Casillas * Lineas];
        private readonly double[] sumaNivel = new double[Casillas * Lineas];

        internal MedidorZumbido(int tasa, int canales)
        {
            this.canales = Math.Max(1, canales);
            diezmo = Math.Max(1, (int)Math.Round(tasa / TasaBaja));
            double baja = tasa / (double)diezmo;
            invDiezmo = 1.0 / diezmo;
            r1 = new double[diezmo]; r2 = new double[diezmo]; r3 = new double[diezmo];
            largoVentana = Math.Max(16, (int)Math.Round(VentanaS * baja));
            hann = new double[largoVentana];
            for (int k = 0; k < largoVentana; k++) { hann[k] = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * k / largoVentana); sumaHann += hann[k]; }
            for (int f = 0; f < Fundamentales.Length; f++)
                for (int a = 0; a < Armonicos; a++)
                {
                    double hz = Fundamentales[f] * (a + 1);
                    int l = f * Armonicos + a;
                    coef[3 * l] = 2.0 * Math.Cos(2.0 * Math.PI * hz / baja);
                    coef[3 * l + 1] = 2.0 * Math.Cos(2.0 * Math.PI * (hz - VecinoHz) / baja);
                    coef[3 * l + 2] = 2.0 * Math.Cos(2.0 * Math.PI * (hz + VecinoHz) / baja);
                }
        }
        internal void Reinicia()
        {
            Array.Clear(r1, 0, diezmo); Array.Clear(r2, 0, diezmo); Array.Clear(r3, 0, diezmo); s1 = s2 = s3 = 0; rPos = faseDiezmo = fase = 0; sumaCuadro = 0;
            Array.Clear(g1, 0, Filtros); Array.Clear(g2, 0, Filtros); nVentana = 0; energia = 0; muestras = 0;
            Array.Clear(cuenta, 0, Casillas); Array.Clear(sumaSobresale, 0, sumaSobresale.Length);
            Array.Clear(constantes, 0, constantes.Length); Array.Clear(sumaNivel, 0, sumaNivel.Length);
        }

        // Muestras entrelazadas; 'cuantos' no tiene que ser multiplo de los canales (el canal que toca se recuerda).
        internal void Procesa(float[] datos, int desde, int cuantos)
        {
            int fin = desde + cuantos, c = fase, d = diezmo, p = rPos, fd = faseDiezmo;
            double sc = sumaCuadro, inv = 1.0 / canales, id = invDiezmo, a1 = s1, a2 = s2, a3 = s3, en = energia;
            double[] q1 = r1, q2 = r2, q3 = r3;
            long mu = muestras;
            for (int i = desde; i < fin; i++)
            {
                sc += datos[i];
                if (++c < canales) continue;
                c = 0;
                double x = sc * inv; sc = 0;
                en += x * x; mu++;
                a1 += x - q1[p]; q1[p] = x; double m1 = a1 * id;
                a2 += m1 - q2[p]; q2[p] = m1; double m2 = a2 * id;
                a3 += m2 - q3[p]; q3[p] = m2;
                if (++p == d) p = 0;
                if (++fd < d) continue;
                fd = 0;
                energia = en; muestras = mu;
                Baja(a3 * id);
                en = energia; mu = muestras;
            }
            fase = c; sumaCuadro = sc; rPos = p; faseDiezmo = fd; s1 = a1; s2 = a2; s3 = a3; energia = en; muestras = mu;
        }

        private void Baja(double y)
        {
            double v = y * hann[nVentana];
            for (int k = 0; k < Filtros; k++) { double s0 = v + coef[k] * g1[k] - g2[k]; g2[k] = g1[k]; g1[k] = s0; }
            if (++nVentana < largoVentana) return;
            nVentana = 0;
            double rms = muestras > 0 ? energia / muestras : 0.0;
            energia = 0; muestras = 0;
            double db = rms > 0 ? 10.0 * Math.Log10(rms) : -200.0;
            if (db > PisoDb)
            {
                int casilla = (int)Math.Floor(db + Casillas);
                if (casilla < 0) casilla = 0; else if (casilla >= Casillas) casilla = Casillas - 1;
                cuenta[casilla]++;
                for (int l = 0; l < Lineas; l++)
                {
                    double pc = Potencia(3 * l), pv = 0.5 * (Potencia(3 * l + 1) + Potencia(3 * l + 2));
                    double sobre = 10.0 * Math.Log10((pc + 1e-30) / (pv + 1e-30));
                    if (sobre > 60.0) sobre = 60.0; else if (sobre < -60.0) sobre = -60.0;
                    double amp = 2.0 * Math.Sqrt(Math.Max(0.0, pc)) / sumaHann;
                    double nivel = amp > 1e-12 ? 20.0 * Math.Log10(amp) : -240.0;
                    int q = casilla * Lineas + l;
                    sumaSobresale[q] += sobre; sumaNivel[q] += nivel;
                    if (sobre >= SobresaleMinDb) constantes[q]++;
                }
            }
            Array.Clear(g1, 0, Filtros); Array.Clear(g2, 0, Filtros);
            // Las sumas de las medias moviles se rehacen de sus anillos de vez en cuando: asi el redondeo no se acumula en horas de musica.
            double t1 = 0, t2 = 0, t3 = 0;
            for (int k = 0; k < diezmo; k++) { t1 += r1[k]; t2 += r2[k]; t3 += r3[k]; }
            s1 = t1; s2 = t2; s3 = t3;
        }
        private double Potencia(int k) { double a = g1[k], b = g2[k]; return a * a + b * b - coef[k] * a * b; }

        // El veredicto: la fundamental (50 o 60) y cuanto sobresale (dB), o falso si no hay zumbido (o no hay con que saberlo).
        internal bool Resultado(out int hz, out float sobresaleDb)
        {
            hz = 0; sobresaleDb = float.NaN;
            int total = 0;
            for (int b = 0; b < Casillas; b++) total += cuenta[b];
            if (total < VentanasMin) return false;
            int quiero = Math.Max(VentanasMin, (int)Math.Ceiling(total * CalladoFraccion)), tomadas = 0, hasta = -1;
            for (int b = 0; b < Casillas && tomadas < quiero; b++) { tomadas += cuenta[b]; hasta = b; }
            double mejor = double.NegativeInfinity; int mejorHz = 0;
            for (int f = 0; f < Fundamentales.Length; f++)
                for (int a = 0; a < 2; a++)   // la fundamental o el 2.o armonico (el zumbido de un rectificador va en el doble)
                {
                    int l = f * Armonicos + a; double s = 0, nv = 0; int k = 0;
                    for (int b = 0; b <= hasta; b++) { int q = b * Lineas + l; s += sumaSobresale[q]; nv += sumaNivel[q]; k += constantes[q]; }
                    double media = s / tomadas, nivel = nv / tomadas, fraccion = k / (double)tomadas;
                    if (media >= SobresaleDb && fraccion >= Constancia && nivel > NivelMinDb && media > mejor) { mejor = media; mejorHz = Fundamentales[f]; }
                }
            if (mejorHz == 0) return false;
            hz = mejorHz; sobresaleDb = (float)Math.Round(mejor, 1);
            return true;
        }
    }

    // Las muescas. Una bicuadrada "notch" (Audio EQ Cookbook) por armonico, con 2 Hz de ancho a -3 dB: Q = f / 2. En serie, por canal.
    internal sealed class QuitaZumbido : ISampleProvider
    {
        internal const double AnchoHz = 2.0;
        internal const int Muescas = 4;                       // la fundamental y sus tres armonicos siguientes
        internal const double RampaS = 0.025;                 // encender o apagar en vivo, sin chasquido (como el subgrave)
        private readonly ISampleProvider fuente;
        private readonly int ch, nm;
        private readonly double[] b0, b1, a1, a2;             // b2 = b0 en una muesca
        private readonly double[] est;                        // por canal y muesca: x1, x2, y1, y2
        private int canal;
        private readonly int rampaN;
        private int mezcla;                                   // 0 = pasa tal cual .. rampaN = filtrado entero
        private volatile bool activa;
        internal volatile bool Pasa;                          // "Escuchar sin mejoras" (S4): pasa tal cual
        internal readonly int Hz;
        private volatile QuitaZumbido anterior;               // disco continuo: la de la pista anterior (se copia en la primera lectura)
        private bool empezo;

        // 'encendida': el interruptor al abrir. Apagada, pasa tal cual (bit a bit) hasta que se encienda en vivo.
        internal QuitaZumbido(ISampleProvider s, int hz, bool encendida)
        {
            fuente = s; Hz = hz; ch = Math.Max(1, s.WaveFormat.Channels);
            int tasa = s.WaveFormat.SampleRate;
            int n = 0;
            for (int k = 1; k <= Muescas; k++) if (hz * k < 0.45 * tasa) n++;
            nm = n;
            b0 = new double[nm]; b1 = new double[nm]; a1 = new double[nm]; a2 = new double[nm];
            for (int k = 0; k < nm; k++)
            {
                double f = hz * (k + 1), w = 2.0 * Math.PI * f / tasa, q = f / AnchoHz, alfa = Math.Sin(w) / (2.0 * q), a0 = 1.0 + alfa;
                b0[k] = 1.0 / a0; b1[k] = -2.0 * Math.Cos(w) / a0; a1[k] = b1[k]; a2[k] = (1.0 - alfa) / a0;
            }
            est = new double[ch * nm * 4];
            rampaN = Math.Max(1, (int)(RampaS * tasa));
            activa = encendida;
            mezcla = encendida ? rampaN : 0;
        }
        public WaveFormat WaveFormat { get { return fuente.WaveFormat; } }
        // El interruptor en vivo: entra o sale en 25 ms.
        internal void Ajusta(bool encendida) { activa = encendida; }
        internal bool Activa { get { return activa; } }
        // Disco continuo: si la anterior tambien lo quitaba, a la misma frecuencia, se sigue con sus filtros (sin escalon). Lo toma en
        // su primera lectura, cuando la anterior ya acabo (el relevo no lee la siguiente hasta que la anterior se termina).
        internal void Sigue(QuitaZumbido previa) { if (previa != null && !object.ReferenceEquals(previa, this)) anterior = previa; }
        private void Hereda()
        {
            QuitaZumbido a = anterior; anterior = null;
            if (a == null || a.Hz != Hz || a.ch != ch || a.nm != nm || a.WaveFormat.SampleRate != WaveFormat.SampleRate) return;
            Array.Copy(a.est, est, est.Length); canal = a.canal; mezcla = a.mezcla;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (!empezo) { empezo = true; Hereda(); }
            int n = fuente.Read(buffer, offset, count);
            if (n <= 0) return n;
            bool quiere = activa && !Pasa;
            if (!quiere && mezcla == 0) { canal = (canal + n) % ch; return n; }   // apagada del todo: tal cual (y sigue la cuenta del canal)
            int c = canal;
            for (int i = offset; i < offset + n; i++)
            {
                double x = buffer[i], y = x;
                int e = c * nm * 4;
                for (int k = 0; k < nm; k++, e += 4)
                {
                    double v = b0[k] * (y + est[e + 1]) + b1[k] * est[e] - a1[k] * est[e + 2] - a2[k] * est[e + 3];
                    est[e + 1] = est[e]; est[e] = y; est[e + 3] = est[e + 2]; est[e + 2] = v;
                    y = v;
                }
                if (c == 0)
                {
                    if (quiere) { if (mezcla < rampaN) mezcla++; }
                    else if (mezcla > 0) mezcla--;
                }
                buffer[i] = mezcla >= rampaN ? (float)y : (float)(x + (y - x) * mezcla / (double)rampaN);
                if (++c == ch) c = 0;
            }
            canal = c;
            return n;
        }
    }
}
