// AGUDOS ASPEROS DINAMICOS (27 de octubre de 2026, quinta tanda del modo billar): un ecualizador que solo corta cuando los agudos raspan.
//
// Los tweeters de corneta del gabinete (Mitzu TW-4700) y los de las cajas «gritan» entre 2 y 4 kHz cuando entran de golpe unos platillos,
// unas trompetas o una guitarra saturada: en una nave de lamina y loseta ese rango ademas rebota. El EQ del billar ya no sube esa franja y
// la claridad le da +1,5 dB a 3 kHz para la voz; esta etapa baja 3,2 kHz SOLO en el instante en que se pasa de lo normal en la cancion, y
// lo suelta en cuanto vuelve a su sitio. Es el mismo diseno que el retumbe dinamico (RetumbeDinamico.cs), en los agudos:
//
//  - DETECTOR (sobre la suma mono, que ya lleva el EQ, el local, la presencia y el ataque: va detras de AtaqueClaro): dos paso-banda, uno en
//    3,2 kHz y otro en 1 kHz (el cuerpo de la voz), con su energia suavizada en 10 ms. Se compara la razon entre los dos (dB) con su propia
//    media lenta: lo que importa no es cuanto brillo tiene la cancion sino cuando tiene MAS del que tenia. La voz, que esta en casi toda la
//    cancion, entra en la media y no se toca. La media baja en unos 6 s pero SUBE cuatro veces mas despacio (24 s): unos platillos que
//    gritan cada pocos segundos no se vuelven «lo normal» de la cancion (con la media pareja, a la tercera rafaga ya solo cortaba 1,2 dB).
//  - EL CORTE. Si la razon pasa 4 dB de su media, corta la mitad del exceso, hasta 3 dB. Ataque de 8 ms (un platillo llega de golpe) y
//    suelta de 150 ms. Si el brillo se queda alto, la media lo alcanza en unos 6 s y el corte se va solo (es la cancion, no un golpe).
//  - EL FILTRO. Una campana de 3,2 kHz (Q 1,2), con una sola ganancia para los dos canales (la imagen no se mueve), recalculada cada 32
//    cuadros. Sin corte (0 dB exacto) no se filtra: la muestra sale bit a bit y la memoria sigue a la senal.
//
// Su interruptor («Agudos asperos dinamicos», aspereza_dinamica=) esta APAGADO de fabrica; con el modo billar va encendida. Apagada no toca
// ni una muestra; si se apaga a media bajada, el corte vuelve a 0 en 40 ms. «Escuchar sin mejoras» la quita mientras dura. Lecturas
// partidas, disco continuo (Sigue) y cebado, como las demas (EtapaPorCuadros.cs).
using System;
using NAudio.Wave;

namespace Rocola
{
    internal sealed class AsperezaDinamica : EtapaPorCuadros
    {
        internal const double CentroHz = 3200.0, CentroQ = 1.2, RefHz = 1000.0, DetQ = 1.0;
        internal const double EnvolventeMs = 10.0, MediaSeg = 6.0, MediaSubeVeces = 4.0, UmbralDb = 4.0, Pendiente = 0.5, MaxCorteDb = 3.0;
        internal const double AtaqueMs = 8.0, SueltaMs = 150.0, ApagaMs = 40.0, CalentarSeg = 0.15;
        internal const double PisoPotencia = 1e-8;
        internal const int Trozo = 32;

        private volatile bool encendido;
        private readonly double kEnv, kAta, kSue, kMediaMin, pasoApaga, sinCentro, cosCentro;
        private readonly double d1b0, d1a1, d1a2, d2b0, d2a1, d2a2;
        private readonly int calentar;
        private readonly double[] det = new double[8];
        private readonly double[] fil = new double[8];
        private double e1, e2, media, corte, corteAplicado;
        private double c0, c1, c2, ca1, ca2;
        private long bloques, validos;
        private int cf;
        private bool primado;
        private double corteMax;
        private volatile AsperezaDinamica anterior;

        internal AsperezaDinamica(ISampleProvider s, bool encender) : base(s)
        {
            encendido = encender;
            if (!vale) return;
            kEnv = 1.0 - Math.Exp(-1.0 / (EnvolventeMs * 0.001 * tasa));
            kAta = 1.0 - Math.Exp(-(double)Trozo / (AtaqueMs * 0.001 * tasa));
            kSue = 1.0 - Math.Exp(-(double)Trozo / (SueltaMs * 0.001 * tasa));
            kMediaMin = (double)Trozo / (MediaSeg * tasa);
            pasoApaga = MaxCorteDb * Trozo / (ApagaMs * 0.001 * tasa);
            calentar = (int)(CalentarSeg * tasa / Trozo);
            double w = 2.0 * Math.PI * Math.Min(CentroHz, 0.4 * tasa) / tasa; sinCentro = Math.Sin(w); cosCentro = Math.Cos(w);
            PasoBanda(CentroHz, out d1b0, out d1a1, out d1a2);
            PasoBanda(RefHz, out d2b0, out d2a1, out d2a2);
        }

        internal bool Encendido { get { return encendido; } }
        internal double CorteAhora { get { return corteAplicado; } }
        internal double MediaRazonDb { get { return media; } }
        internal void Ajusta(bool encender) { if (vale) encendido = encender; }
        // Lo que mas corto desde la ultima vez que se pregunto, en dB (0 = nada). Lo lee la pantalla (S5).
        internal double CortaDb { get { double c = corteMax; corteMax = 0; return c; } }

        private void PasoBanda(double hz, out double b0, out double a1, out double a2)
        {
            double w = 2.0 * Math.PI * Math.Min(hz, 0.4 * tasa) / tasa, al = Math.Sin(w) / (2.0 * DetQ), a0 = 1.0 + al;
            b0 = al / a0; a1 = -2.0 * Math.Cos(w) / a0; a2 = (1.0 - al) / a0;
        }

        internal void Sigue(AsperezaDinamica previa) { if (previa != null && !object.ReferenceEquals(previa, this)) anterior = previa; }
        protected override bool Hereda()
        {
            AsperezaDinamica a = anterior; anterior = null;
            if (a == null || !a.vale || !a.primado || a.tasa != tasa || a.canales != canales) return false;
            Array.Copy(a.det, det, det.Length); Array.Copy(a.fil, fil, fil.Length);
            e1 = a.e1; e2 = a.e2; media = a.media; corte = a.corte; corteAplicado = a.corteAplicado;
            c0 = a.c0; c1 = a.c1; c2 = a.c2; ca1 = a.ca1; ca2 = a.ca2;
            bloques = a.bloques; validos = a.validos; cf = a.cf; primado = true;
            return true;
        }

        protected override bool Quieta()
        {
            if (encendido || corteAplicado != 0.0 || corte != 0.0) return false;
            primado = false;
            return true;
        }

        private void NuevaCampana(double cortaDb)
        {
            double A = Math.Pow(10.0, -cortaDb / 40.0), al = sinCentro / (2.0 * CentroQ), a0 = 1.0 + al / A;
            c0 = (1.0 + al * A) / a0; c1 = -2.0 * cosCentro / a0; c2 = (1.0 - al * A) / a0; ca1 = c1; ca2 = (1.0 - al / A) / a0;
        }

        protected override void Proceso(float[] b, int o, int cuadros)
        {
            bool on = encendido, est2 = canales == 2;
            double[] d = det, s = fil;
            if (!primado)
            {
                Array.Clear(d, 0, d.Length);
                double p0 = b[o], p1 = est2 ? b[o + 1] : 0.0;
                s[0] = s[1] = s[2] = s[3] = p0;
                if (est2) s[4] = s[5] = s[6] = s[7] = p1;
                e1 = e2 = 0.0; media = 0.0; corte = 0.0; corteAplicado = 0.0; bloques = 0; validos = 0; cf = 0;
                primado = true;
            }
            double ee1 = e1, ee2 = e2;
            double k0 = c0, k1 = c1, k2 = c2, q1 = ca1, q2 = ca2, aplicado = corteAplicado;
            int cc = cf;
            for (int f = 0, i = o; f < cuadros; f++, i += canales)
            {
                double x0 = b[i], x1 = est2 ? b[i + 1] : 0.0;
                if (on)
                {
                    double m = est2 ? 0.5 * (x0 + x1) : x0;
                    double y1 = d1b0 * (m - d[1]) - d1a1 * d[2] - d1a2 * d[3];
                    if (y1 < 1e-30 && y1 > -1e-30) y1 = 0.0;
                    d[1] = d[0]; d[0] = m; d[3] = d[2]; d[2] = y1;
                    double y2 = d2b0 * (m - d[5]) - d2a1 * d[6] - d2a2 * d[7];
                    if (y2 < 1e-30 && y2 > -1e-30) y2 = 0.0;
                    d[5] = d[4]; d[4] = m; d[7] = d[6]; d[6] = y2;
                    ee1 += kEnv * (y1 * y1 - ee1); ee2 += kEnv * (y2 * y2 - ee2);
                    if (ee1 < 1e-20) ee1 = 0.0;
                    if (ee2 < 1e-20) ee2 = 0.0;
                }
                if (aplicado != 0.0)
                {
                    double yy = k0 * x0 + k1 * s[0] + k2 * s[1] - q1 * s[2] - q2 * s[3];
                    if (yy < 1e-30 && yy > -1e-30) yy = 0.0;
                    s[1] = s[0]; s[0] = x0; s[3] = s[2]; s[2] = yy;
                    b[i] = (float)yy;
                    if (est2)
                    {
                        yy = k0 * x1 + k1 * s[4] + k2 * s[5] - q1 * s[6] - q2 * s[7];
                        if (yy < 1e-30 && yy > -1e-30) yy = 0.0;
                        s[5] = s[4]; s[4] = x1; s[7] = s[6]; s[6] = yy;
                        b[i + 1] = (float)yy;
                    }
                }
                else
                {
                    s[1] = s[0]; s[0] = x0; s[3] = s[2]; s[2] = x0;
                    if (est2) { s[5] = s[4]; s[4] = x1; s[7] = s[6]; s[6] = x1; }
                }
                if (++cc < Trozo) continue;
                cc = 0;
                double meta = 0.0;
                if (on)
                {
                    bloques++;
                    if (bloques > calentar && (ee1 + ee2) > PisoPotencia)
                    {
                        double r = 10.0 * Math.Log10((ee1 + 1e-10) / (ee2 + 1e-10));
                        if (r > 40.0) r = 40.0; else if (r < -40.0) r = -40.0;
                        validos++;
                        if (validos == 1) media = r;
                        else { double km = 1.0 / validos; double piso = r > media ? kMediaMin / MediaSubeVeces : kMediaMin; if (km < piso) km = piso; media += km * (r - media); }
                        double exceso = r - media - UmbralDb;
                        if (exceso > 0.0) { meta = Pendiente * exceso; if (meta > MaxCorteDb) meta = MaxCorteDb; }
                    }
                    corte += (meta - corte) * (meta > corte ? kAta : kSue);
                    if (corte < 0.002) corte = 0.0;
                    else if (corte > MaxCorteDb) corte = MaxCorteDb;
                    if (corte > corteMax) corteMax = corte;
                }
                else corte = corte <= pasoApaga ? 0.0 : corte - pasoApaga;
                if (corte != aplicado)
                {
                    if (corte == 0.0) aplicado = 0.0;
                    else if (Math.Abs(corte - aplicado) > 0.0005 || aplicado == 0.0)
                    {
                        NuevaCampana(corte); aplicado = corte;
                        k0 = c0; k1 = c1; k2 = c2; q1 = ca1; q2 = ca2;
                    }
                }
            }
            e1 = ee1; e2 = ee2; corteAplicado = aplicado; cf = cc;
        }
    }
}
