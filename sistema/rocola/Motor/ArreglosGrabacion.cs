// ARREGLOS DE LA GRABACION (S1 y S2, 13-10-2026; peticion: «Haz S1, S2, S3, S4 y S5»).
//
// Dos defectos de algunas copias que en el bar se oyen, y que esta etapa arregla solo en las canciones que los traen. En
// las demas no toca ni un bit: sale exactamente lo que entro.
//
// S1. UN CANAL MUDO, POR LOS DOS. Hay copias viejas (de cassette, de vinilo o mal convertidas) con un canal vacio. Con
//     las bocinas repartidas por zonas, en media sala esa cancion no se oye. Si un canal pasa 1,5 s mas de 30 dB por
//     debajo del otro mientras el otro suena, los dos dan el que suena, cada uno a -3 dB: la suma de potencias es la
//     misma, asi que el nivelado (que midio el original) sigue valiendo. Entra en medio segundo; en cuanto el canal mudo
//     vuelve a sonar (a menos de 20 dB del otro) se quita en medio segundo. En un silencio de los dos no cambia nada.
//     Un instrumento grabado a un lado con su eco en el otro no llega a 30 dB de diferencia: no lo toma por mudo.
//
// S2. QUITAR LA CORRIENTE CONTINUA. Una grabacion con continua (la onda entera corrida hacia arriba o hacia abajo) gasta
//     margen del limitador, que baja la cancion de mas, y empuja los conos sin sonar. Se sigue la media de cada canal
//     (constante de 2 s, unos 0,08 Hz). Si pasa de -50 dBFS durante 3 s, se resta, entrando en un segundo; si baja de
//     -56 dBFS durante 5 s, se deja de restar. Restar la media lenta es un paso-alto por debajo de 0,1 Hz: no se oye.
//
// PASO DIRECTO (S4, «Escuchar sin mejoras»): con Pasa, los dos arreglos salen en su rampa y la etapa queda en pase.
//
// DISCO CONTINUO: la pista siguiente del mismo disco sigue con lo medido por la anterior (Sigue), asi no hay un salto en
// el cambio: si la anterior restaba la continua, la siguiente tambien, desde la primera muestra.
//
// Cuesta unas sumas y productos por muestra. Mono: solo S2. Mas de dos canales: pasa tal cual.
using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        // Los interruptores (Ctrl+F3 -> Sonido -> Lo que suena ahora). Encendidos de fabrica: solo actuan con el defecto.
        private static bool canalMudo = true, quitaContinua = true;   // "canal_mudo=" y "quita_continua="
        private static bool quitaZumbido = true;                    // "quita_zumbido=" (T6, Zumbido.cs): encendido de fabrica, como S1 y S2

        internal sealed class ArreglaGrabacion : ISampleProvider
        {
            internal const double MudoDb = 30.0, VuelveDb = 20.0, MudoSeg = 1.5, RampaMudoSeg = 0.5, EnergiaSeg = 0.5;
            internal const double PisoMusica = 1e-5;          // -50 dBFS de potencia: por debajo, el canal "vivo" esta callado
            internal const double DcSeg = 2.0, DcHay = 0.00316, DcNoHay = 0.0016, DcHaySeg = 3.0, DcNoHaySeg = 5.0, RampaDcSeg = 1.0;
            internal const float MitadPotencia = 0.70710678f;
            private readonly ISampleProvider src;
            private readonly int ch, tasa;
            private readonly bool activo;
            private volatile bool mudoOn, dcOn, pasa;
            // S2: la media de cada canal y lo que se resta.
            private readonly double aDc;
            private readonly double[] dc = new double[2];
            private int dcCuenta, dcFalta;                    // cuadros seguidos con continua / sin ella
            private bool dcHay;
            private double kDc;
            private readonly double pasoDc, cuadrosHay, cuadrosNoHay;
            // S1: la energia de cada canal (sin la continua) y cuanto se reparte.
            private readonly double aE, pasoMudo, cuadrosMudo, razonMudo, razonVuelve;
            private double eL, eR, kL, kR, metaL, metaR;
            private int cuentaL, cuentaR;
            private int limpia;
            private readonly float[] sale = new float[2];
            private int saleN;
            private bool empezo, acabo;
            private volatile ArreglaGrabacion anterior;

            // Lo que la ventana ensena (S5): cuanto se reparte el canal vivo y cuanto se resta de continua.
            internal volatile float RepartoAhora, ContinuaAhora, ContinuaDb = -120f;

            public ArreglaGrabacion(ISampleProvider s, bool canalMudo, bool quitaContinua)
            {
                src = s;
                ch = s.WaveFormat.Channels; tasa = s.WaveFormat.SampleRate;
                activo = (ch == 1 || ch == 2) && tasa >= 8000;
                mudoOn = canalMudo && ch == 2; dcOn = quitaContinua;
                aDc = 1.0 - Math.Exp(-1.0 / (DcSeg * tasa));
                pasoDc = 1.0 / (RampaDcSeg * tasa);
                cuadrosHay = DcHaySeg * tasa; cuadrosNoHay = DcNoHaySeg * tasa;
                aE = 1.0 - Math.Exp(-1.0 / (EnergiaSeg * tasa));
                pasoMudo = 1.0 / (RampaMudoSeg * tasa);
                cuadrosMudo = MudoSeg * tasa;
                razonMudo = Math.Pow(10.0, -MudoDb / 10.0); razonVuelve = Math.Pow(10.0, -VuelveDb / 10.0);
            }
            public WaveFormat WaveFormat { get { return src.WaveFormat; } }
            internal bool Activo { get { return activo; } }
            // Los interruptores, en vivo (los lee el hilo del sonido en cada cuadro).
            internal void Ajusta(bool canalMudo, bool quitaContinua) { mudoOn = canalMudo && ch == 2; dcOn = quitaContinua; }
            // S4: todo en pase, con su rampa.
            internal bool Pasa { get { return pasa; } set { pasa = value; } }
            internal void Sigue(ArreglaGrabacion previa) { if (previa != null && !object.ReferenceEquals(previa, this)) anterior = previa; }

            private void Hereda()
            {
                ArreglaGrabacion a = anterior; anterior = null;
                if (a == null || !a.activo || !a.acabo || a.ch != ch || a.tasa != tasa) return;
                dc[0] = a.dc[0]; dc[1] = a.dc[1]; dcCuenta = a.dcCuenta; dcFalta = a.dcFalta; dcHay = a.dcHay; kDc = a.kDc;
                eL = a.eL; eR = a.eR; kL = a.kL; kR = a.kR; metaL = a.metaL; metaR = a.metaR; cuentaL = a.cuentaL; cuentaR = a.cuentaR;
            }

            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                if (!activo) return src.Read(buffer, offset, count);
                if (!empezo) { empezo = true; Hereda(); }
                if (ch == 1)
                {
                    int n1 = src.Read(buffer, offset, count);
                    if (n1 > 0) Cuadros(buffer, offset, n1);
                    else acabo = true;
                    return n1;
                }
                int hechos = 0;
                if (saleN > 0 && count > 0) { buffer[offset] = sale[1]; saleN = 0; hechos = 1; }
                int pares = (count - hechos) & ~1;
                if (pares > 0 && !acabo)
                {
                    int n = Enteros(buffer, offset + hechos, pares);
                    Cuadros(buffer, offset + hechos, n >> 1);
                    hechos += n;
                }
                if (count - hechos == 1 && !acabo && Enteros(sale, 0, 2) == 2)
                {
                    Cuadros(sale, 0, 1);
                    buffer[offset + hechos++] = sale[0]; saleN = 1;
                }
                return hechos;
            }

            // Cuadros enteros del origen (como GraveMono): si llega uno partido, se sigue leyendo hasta completarlo.
            private int Enteros(float[] b, int o, int quiero)
            {
                int tiene = 0;
                while (tiene < quiero)
                {
                    int r = src.Read(b, o + tiene, quiero - tiene);
                    if (r <= 0) { acabo = true; break; }
                    tiene += r;
                    if ((tiene & 1) == 0) break;
                }
                return tiene & ~1;
            }

            private void Cuadros(float[] b, int i, int cuadros)
            {
                bool directo = pasa;
                bool conDc = dcOn && !directo, conMudo = mudoOn && !directo;
                double d0 = dc[0], d1 = dc[1], k = kDc, a = aDc;
                double el = eL, er = eR, kl = kL, kr = kR, ml = metaL, mr = metaR;
                int cl = cuentaL, cr = cuentaR, hay = dcCuenta, falta = dcFalta;
                bool dHay = dcHay;
                for (int f = 0; f < cuadros; f++)
                {
                    int p = i + f * ch;
                    double x0 = b[p], x1 = ch == 2 ? b[p + 1] : 0.0;
                    // S2: la media lenta de cada canal, siempre (para tenerla lista).
                    d0 += (x0 - d0) * a;
                    if (ch == 2) d1 += (x1 - d1) * a;
                    double mayor = Math.Max(Math.Abs(d0), ch == 2 ? Math.Abs(d1) : 0.0);
                    if (mayor > DcHay) { if (++hay >= cuadrosHay) { dHay = true; hay = (int)cuadrosHay; } falta = 0; }
                    else if (mayor < DcNoHay) { if (++falta >= cuadrosNoHay) { dHay = false; falta = (int)cuadrosNoHay; } hay = 0; }
                    double metaK = (conDc && dHay) ? 1.0 : 0.0;
                    if (k != metaK) k = k < metaK ? Math.Min(metaK, k + pasoDc) : Math.Max(metaK, k - pasoDc);
                    double y0 = x0, y1 = x1;
                    if (k != 0.0) { y0 = x0 - k * d0; y1 = x1 - k * d1; }
                    if (ch == 2)
                    {
                        // S1: la energia de cada canal sin su continua.
                        double l = x0 - d0, r = x1 - d1;
                        el += (l * l - el) * aE; er += (r * r - er) * aE;
                        // Solo se decide con musica: en un silencio de los dos (una pausa, el final) no cambia nada.
                        bool suena = el > PisoMusica || er > PisoMusica;
                        if (er > PisoMusica && el < er * razonMudo) { if (++cl >= cuadrosMudo) { ml = 1.0; cl = (int)cuadrosMudo; } }
                        else { cl = 0; if (suena && el > er * razonVuelve) ml = 0.0; }
                        if (el > PisoMusica && er < el * razonMudo) { if (++cr >= cuadrosMudo) { mr = 1.0; cr = (int)cuadrosMudo; } }
                        else { cr = 0; if (suena && er > el * razonVuelve) mr = 0.0; }
                        double tl = conMudo ? ml : 0.0, tr = conMudo ? mr : 0.0;
                        if (kl != tl) kl = kl < tl ? Math.Min(tl, kl + pasoMudo) : Math.Max(tl, kl - pasoMudo);
                        if (kr != tr) kr = kr < tr ? Math.Min(tr, kr + pasoMudo) : Math.Max(tr, kr - pasoMudo);
                        if (kl != 0.0 || kr != 0.0)
                        {
                            // El canal mudo se llena con el vivo, y los dos a media potencia: la suma de potencias no cambia.
                            double vl = y0, vr = y1;
                            y0 = vl * (1.0 - kl) + vr * kl * MitadPotencia;
                            y1 = vr * (1.0 - kl * (1.0 - MitadPotencia));
                            if (kr != 0.0)
                            {
                                double ql = y0, qr = y1;
                                y1 = qr * (1.0 - kr) + ql * kr * MitadPotencia;
                                y0 = ql * (1.0 - kr * (1.0 - MitadPotencia));
                            }
                        }
                        if (k != 0.0 || kl != 0.0 || kr != 0.0) { b[p] = (float)y0; b[p + 1] = (float)y1; }
                    }
                    else if (k != 0.0) b[p] = (float)y0;
                    if (++limpia == Desnormales.Cada)
                    {
                        limpia = 0;
                        d0 = Desnormales.Limpia(d0); d1 = Desnormales.Limpia(d1);
                        el = Desnormales.Limpia(el); er = Desnormales.Limpia(er);
                    }
                }
                dc[0] = d0; dc[1] = d1; kDc = k; dcCuenta = hay; dcFalta = falta; dcHay = dHay;
                eL = el; eR = er; kL = kl; kR = kr; metaL = ml; metaR = mr; cuentaL = cl; cuentaR = cr;
                RepartoAhora = (float)Math.Max(kl, kr); ContinuaAhora = (float)k;
                double m = Math.Max(Math.Abs(d0), Math.Abs(d1));
                ContinuaDb = m > 1e-6 ? (float)(20.0 * Math.Log10(m)) : -120f;
            }
        }
    }
}
