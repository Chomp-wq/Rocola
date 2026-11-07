// RETUMBE DINAMICO (27 de octubre de 2026): un ecualizador que solo corta cuando el local retumba.
//
// En un billar con techo de lamina, ladrillo y loseta, el grave medio (200-400 Hz) se acumula en ciertos momentos -un bajo que se
// pasa, un bombo con mucho cuerpo, un acorde grave sostenido- y tapa la voz. «Claridad para el local» (SalaLocal.cs) baja 300 Hz
// SIEMPRE; esta etapa baja ese rango solo cuando se pasa, y lo suelta cuando vuelve a su sitio.
//
// COMO.
//  - DETECTOR (sobre la suma mono de los dos canales, que ya lleva el EQ automatico y la claridad: va detras de SalaLocal, asi no se
//    cuenta dos veces lo que ya se corto): dos paso-banda, uno en 260 Hz y otro en 1,4 kHz, con su energia suavizada en 25 ms. Se
//    compara la razon entre los dos (dB) con su propia media lenta (6 s): lo que importa no es cuanto grave tiene la cancion sino
//    cuando tiene MAS grave de lo que tenia. Los primeros 6 s la media es la media de lo oido hasta entonces.
//  - EL CORTE. Si la razon pasa 3 dB de su media, corta la mitad del exceso (un salto de +8 dB en 260 Hz da 2,5 dB), hasta 4 dB; con
//    «Claridad para el local» puesta, hasta 3 dB. Ataque de 20 ms y suelta de 200 ms. Si el grave se queda alto, la media lo alcanza
//    en unos 6 s y el corte se va solo (un grave que dura no es un retumbe, es la cancion).
//  - PRESUPUESTO. El EQ automatico de la cancion (que puede cortar hasta ~3,4 dB en 400 Hz) y la claridad (-2,5 dB en 300 Hz) ya cortan
//    ese rango: se mide lo que cortan juntos en 250-500 Hz (CorteEstaticoDb) y el corte dinamico solo puede llegar a lo que falte
//    para 6 dB en total. Si ya cortan 5,3 dB, el dinamico queda en 0,7 dB; si cortan 6 o mas, no actua.
//  - EL FILTRO. Una campana de 260 Hz (Q 1,4) de las notas de Robert Bristow-Johnson, en coma doble, con una sola ganancia para los
//    dos canales (la imagen no se mueve), recalculada cada 32 cuadros (0,7 ms) como las del local (SalaLocal.cs). Sin corte
//    (0 dB exacto) no se filtra: la muestra sale bit a bit y la memoria del filtro sigue a la senal, asi que al empezar a cortar no hay
//    escalon.
//
// Apagado (de fabrica) la etapa no toca ni una muestra. Si se apaga a media bajada, el corte vuelve a 0 en 40 ms, sin chasquido. Lo que
// esta fuera de la banda casi no se entera: con la campana a 2,5 dB y Q 1,4, el 1 kHz baja 0,1 dB, el 3 kHz 0,01 y los 80 Hz 0,15; a 500 Hz
// le toca algo menos de la mitad del corte. «Escuchar sin mejoras» (S4) la quita mientras dura. Lecturas partidas, disco continuo (Sigue) y cebado, como las demas
// (EtapaPorCuadros.cs). Cuesta unos 1,5 us por cuadro.
using System;
using NAudio.Wave;

namespace Rocola
{
    internal sealed class RetumbeDinamico : EtapaPorCuadros
    {
        internal const double CentroHz = 260.0, CentroQ = 1.4, RefHz = 1400.0, DetQ = 1.0;
        internal const double EnvolventeMs = 25.0, MediaSeg = 6.0, UmbralDb = 3.0, Pendiente = 0.5;
        internal const double MaxCorteDb = 4.0, MaxCorteClaridadDb = 3.0, PresupuestoDb = 6.0;
        internal const double AtaqueMs = 20.0, SueltaMs = 200.0, ApagaMs = 40.0, CalentarSeg = 0.15;
        internal const double PisoPotencia = 1e-8;           // por debajo (unos -80 dBFS) no se mide: no hay nada que cortar
        internal const int Trozo = 32;                       // cuadros por cada nueva cuenta (el logaritmo y los coeficientes)

        private volatile bool encendido, claridad;
        private volatile float capDinamico;                  // el corte maximo que deja el presupuesto (dB), puesto por Ajusta
        private readonly double kEnv, kAta, kSue, kMediaMin, pasoApaga, sinCentro, cosCentro;
        private readonly double d1b0, d1a1, d1a2, d2b0, d2a1, d2a2;   // los dos paso-banda (b1 = 0, b2 = -b0)
        private readonly int calentar;
        // Memoria: detector (por paso-banda x1 x2 y1 y2), filtro (por canal x1 x2 y1 y2), energias, media, corte.
        private readonly double[] det = new double[8];
        private readonly double[] fil = new double[8];
        private double e1, e2, media, corte, corteAplicado;
        private double c0, c1, c2, ca1, ca2;                 // la campana ahora (a0 = 1)
        private long bloques, validos;                       // bloques de Trozo cuadros desde que arranco, y los que se midieron (con la media)
        private int cf;                                      // cuadros del bloque que se esta midiendo
        private bool primado;
        private double corteMax;                             // lo que mas corto desde la ultima mirada (S5)
        private volatile RetumbeDinamico anterior;

        internal RetumbeDinamico(ISampleProvider s, bool encender, bool conClaridad, double estaticoDb) : base(s)
        {
            encendido = encender; claridad = conClaridad; capDinamico = (float)CapDinamicoDb(estaticoDb, conClaridad);
            if (!vale) return;
            kEnv = 1.0 - Math.Exp(-1.0 / (EnvolventeMs * 0.001 * tasa));
            kAta = 1.0 - Math.Exp(-(double)Trozo / (AtaqueMs * 0.001 * tasa));
            kSue = 1.0 - Math.Exp(-(double)Trozo / (SueltaMs * 0.001 * tasa));
            kMediaMin = (double)Trozo / (MediaSeg * tasa);
            pasoApaga = MaxCorteDb * Trozo / (ApagaMs * 0.001 * tasa);   // de lo mas alto a 0 en ApagaMs
            calentar = (int)(CalentarSeg * tasa / Trozo);
            double w = 2.0 * Math.PI * CentroHz / tasa; sinCentro = Math.Sin(w); cosCentro = Math.Cos(w);
            PasoBanda(CentroHz, out d1b0, out d1a1, out d1a2);
            PasoBanda(RefHz, out d2b0, out d2a1, out d2a2);
        }

        internal bool Encendido { get { return encendido; } }
        internal bool ConClaridad { get { return claridad; } }
        internal double CorteAhora { get { return corteAplicado; } }
        internal double MediaRazonDb { get { return media; } }
        internal double CapDb { get { return capDinamico; } }
        // El presupuesto (dB) que deja el corte estatico: lo que falte para PresupuestoDb en total, sin pasar del tope (con la claridad, 3 dB).
        internal static double CapDinamicoDb(double estaticoDb, bool conClaridad)
        {
            double tope = conClaridad ? MaxCorteClaridadDb : MaxCorteDb;
            double resta = PresupuestoDb - Math.Max(0.0, estaticoDb);
            return resta < 0.0 ? 0.0 : (resta < tope ? resta : tope);
        }
        // En vivo: encender o apagar, y lo que ya corta el resto (la claridad y el EQ automatico de la cancion, dB positivos).
        internal void Ajusta(bool encender, bool conClaridad, double estaticoDb)
        {
            if (!vale) return;
            claridad = conClaridad; capDinamico = (float)CapDinamicoDb(estaticoDb, conClaridad); encendido = encender;
        }
        // Lo que mas corto desde la ultima vez que se pregunto, en dB (0 = nada). Lo lee la pantalla (S5).
        internal double CortaDb { get { double c = corteMax; corteMax = 0; return c; } }

        // LO QUE CORTAN JUNTOS el EQ automatico de la cancion (su curva de 8 bandas, o null) y la claridad (300 Hz, -2,5 dB) en
        // 250-500 Hz: el corte mas hondo de esa zona, en dB positivos (0 si no corta, o si sube). A 'tasa' Hz.
        internal static double CorteEstaticoDb(float[] curvaEq, bool conClaridad, int tasa)
        {
            double peor = 0.0;
            for (int f = 250; f <= 500; f += 25)
            {
                double w = 2.0 * Math.PI * f / tasa, db = 0.0;
                if (curvaEq != null && curvaEq.Length >= 8)
                    for (int b = 0; b < 8; b++)
                    {
                        float[] cf = Form1.EqPeak(Form1.EqCentros[b], tasa, curvaEq[b]);
                        if (cf != null) db += MagnitudDb(cf[0], cf[1], cf[2], cf[3], cf[4], w);
                    }
                if (conClaridad)
                {
                    double b0, b1, b2, a1, a2;
                    Campana(SalaLocalClaridadHz, SalaLocalClaridadDb, SalaLocalClaridadQ, tasa, out b0, out b1, out b2, out a1, out a2);
                    db += MagnitudDb(b0, b1, b2, a1, a2, w);
                }
                if (-db > peor) peor = -db;
            }
            return peor;
        }
        // Los mismos numeros que la claridad de SalaLocal.cs (300 Hz, -2,5 dB, Q 0,9).
        private const double SalaLocalClaridadHz = 300.0, SalaLocalClaridadDb = -2.5, SalaLocalClaridadQ = 0.9;

        private static double MagnitudDb(double b0, double b1, double b2, double a1, double a2, double w)
        {
            double c1 = Math.Cos(w), s1 = Math.Sin(w), c2 = Math.Cos(2.0 * w), s2 = Math.Sin(2.0 * w);
            double nr = b0 + b1 * c1 + b2 * c2, ni = -(b1 * s1 + b2 * s2);
            double dr = 1.0 + a1 * c1 + a2 * c2, di = -(a1 * s1 + a2 * s2);
            return 10.0 * Math.Log10((nr * nr + ni * ni) / (dr * dr + di * di));
        }

        // Una campana de ecualizador (RBJ), normalizada (a0 = 1).
        internal static void Campana(double hz, double db, double q, int tasa, out double b0, out double b1, out double b2, out double a1, out double a2)
        {
            double A = Math.Pow(10.0, db / 40.0), w0 = 2.0 * Math.PI * hz / tasa, al = Math.Sin(w0) / (2.0 * q), c = Math.Cos(w0);
            double a0 = 1.0 + al / A;
            b0 = (1.0 + al * A) / a0; b1 = -2.0 * c / a0; b2 = (1.0 - al * A) / a0; a1 = -2.0 * c / a0; a2 = (1.0 - al / A) / a0;
        }
        // Paso-banda de 0 dB en el centro (Audio EQ Cookbook): b1 = 0 y b2 = -b0.
        private void PasoBanda(double hz, out double b0, out double a1, out double a2)
        {
            double w = 2.0 * Math.PI * Math.Min(hz, 0.4 * tasa) / tasa, al = Math.Sin(w) / (2.0 * DetQ), a0 = 1.0 + al;
            b0 = al / a0; a1 = -2.0 * Math.Cos(w) / a0; a2 = (1.0 - al) / a0;
        }

        internal void Sigue(RetumbeDinamico previa) { if (previa != null && !object.ReferenceEquals(previa, this)) anterior = previa; }
        protected override bool Hereda()
        {
            RetumbeDinamico a = anterior; anterior = null;
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
            primado = false;   // al encender de nuevo se vuelve a medir desde cero
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
            {   // detector a cero (el calentamiento lo deja fuera de las decisiones); el filtro parte del continuo de la primera muestra
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
                {   // sin corte la salida es la entrada, y la memoria la sigue (y = x): al empezar a cortar no hay escalon
                    s[1] = s[0]; s[0] = x0; s[3] = s[2]; s[2] = x0;
                    if (est2) { s[5] = s[4]; s[4] = x1; s[7] = s[6]; s[6] = x1; }
                }
                if (++cc < Trozo) continue;
                cc = 0;
                // Un bloque entero: una cuenta nueva del corte y, si cambio, de los coeficientes.
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
                        else { double km = 1.0 / validos; if (km < kMediaMin) km = kMediaMin; media += km * (r - media); }
                        double exceso = r - media - UmbralDb;
                        if (exceso > 0.0) { meta = Pendiente * exceso; double cap = capDinamico; if (meta > cap) meta = cap; }
                    }
                    corte += (meta - corte) * (meta > corte ? kAta : kSue);
                    if (corte < 0.002) corte = 0.0;
                    else if (corte > MaxCorteDb) corte = MaxCorteDb;
                    if (corte > corteMax) corteMax = corte;
                }
                else
                {   // apagando: vuelve a 0 en ApagaMs, sin pasar de largo
                    corte = corte <= pasoApaga ? 0.0 : corte - pasoApaga;
                }
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
