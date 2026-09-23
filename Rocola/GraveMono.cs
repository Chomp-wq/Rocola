// EL GRAVE EN MONO: por debajo de ~120 Hz, lo mismo por todas las bocinas
//
// En el billar hay cuatro bocinas en el gabinete y tres repartidas por el local, y no se sabe cual lleva el canal
// izquierdo y cual el derecho. En estereo cada bocina da SU canal. Si una grabacion trae el bajo cargado a un lado
// -pasa en algunas grabaciones viejas- o el grave de un canal al reves que el del otro (en contrafase), quien esta
// junto a la bocina que no lo lleva se queda sin bajo, y donde se juntan las dos el grave se cancela. Por debajo de
// ~120 Hz el oido apenas distingue de donde viene el sonido: en un bar, ahi el estereo no aporta y si estorba. Asi
// que ahi se pone el mismo grave en los dos canales. Por encima, todo sigue tal cual.
//
// COMO: el cruce de dos vias de siempre (Linkwitz-Riley de cuarto orden en el corte), aplicado al estereo.
//   medio = (L + R) / 2 y lado = (L - R) / 2.
//   El LADO pasa por el paso-alto del cruce: lo que tiene por debajo del corte se va, a 24 dB por octava.
//   El MEDIO pasa por lo que suman las dos vias del cruce, que es un paso-todo: no cambia el nivel de NINGUNA
//   frecuencia; solo retrasa un poco el grave (unos 4 ms por debajo del corte, lo que ya hace el cruce de cualquier
//   bocina de dos vias). Sin el, lo que queda del lado iria desfasado respecto del medio y un instrumento abierto a
//   un lado se colaria al otro tambien por encima del corte. Con el, medio y lado siguen en fase.
//   L = medio + lado y R = medio - lado.
//
// UN CANAL AL REVES. Si una grabacion trae un canal con la polaridad invertida (un defecto de copia), su grave va
// entero en el lado: sumarlo a mono lo borraria del todo. Por eso se vigila, en la banda del grave, cuanto hay en el
// medio y cuanto en el lado (energia de los ultimos ~0,3 s). Si el lado tiene mas del doble que el medio (el grave de
// los dos canales va claramente al reves), ese grave del lado no se tira: se devuelve, en fase, a los dos canales
// (el grave que sale es el del canal izquierdo). Entra en medio segundo y se quita cuando el medio vuelve a tener
// mas que el lado. En una grabacion normal (el bajo en el centro, abierto a un lado o distinto en cada canal) esto
// nunca se activa.
//
// Lo que no se toca: un archivo de un canal (ya es mono) y uno de mas de dos. Y lo mono de un estereo sale igual de
// fuerte en todas las frecuencias (solo con el retraso del grave del paso-todo).
//
// CUADROS ENTEROS. Para saber medio y lado hacen falta los dos canales del mismo instante, asi que siempre se
// entregan cuadros enteros: si el origen entrega uno partido, se le sigue pidiendo hasta completarlo (como hace el
// limitador). Si alguien pide una sola muestra, se hace el cuadro entero aparte y la otra mitad sale en la lectura
// siguiente.
//
// DISCO CONTINUO. Entre una pista y la siguiente que sigue sin fundido (la del mismo disco), la etapa de la que entra
// sigue con la memoria de los filtros de la que acabo (Sigue): el grave sale igual que si fuera una sola cancion.
// Sin eso, el paso-todo empezaria de cero en el cambio y se oiria un golpe en el grave. Con fundido no se hereda
// nada: la anterior acaba en silencio y la siguiente empieza de cero, como siempre.
//
// "grave_mono=" en la configuracion: el corte en Hz (de fabrica 120; 0 lo quita). Medido en pruebas/gravemono.cs.
using System;

namespace Rocola
{
    public partial class Form1
    {
        // El corte del grave en mono, en Hz ("grave_mono=" en config; 0 = apagado). Se lee al armar cada cancion.
        private static float graveMonoHz = GraveMono.HzDeFabrica;
        private static float GraveMonoHz(string texto)
        {
            float v;
            if (!float.TryParse(texto.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)
                || float.IsNaN(v) || float.IsInfinity(v)) return graveMonoHz;
            return v <= 0f ? 0f : Clampf(v, GraveMono.HzMinimo, GraveMono.HzMaximo);
        }

        private sealed class GraveMono : NAudio.Wave.ISampleProvider
        {
            internal const float HzDeFabrica = 120f, HzMinimo = 40f, HzMaximo = 250f;
            internal const double VigilaSeg = 0.3, VueltaSeg = 0.5;   // ventana de la vigilancia y lo que tarda en dar la vuelta
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly bool activo;                 // estereo, con un corte que tenga sentido a esta frecuencia
            private readonly int tasa;
            private readonly float hz;
            // Denominador comun 1 + a1 z^-1 + a2 z^-2 (Butterworth, Q = 0,7071, bilineal con el corte ajustado).
            // Paso-alto: b0 (1 - 2 z^-1 + z^-2). Paso-todo: a2 + a1 z^-1 + z^-2 (el mismo al reves).
            private readonly double a1, a2, b0;
            private readonly double aVigila, aEnergia, pasoVuelta;
            private double mx1, mx2, my1, my2;            // paso-todo del medio: entradas y salidas anteriores
            private double sx1, sx2, sy1, sy2;            // primer paso-alto del lado
            private double tx1, tx2, ty1, ty2;            // segundo paso-alto del lado
            private double zx1, zx2, zy1, zy2;            // paso-todo del lado (su grave = paso-todo - paso-alto)
            private double vm1, vm2, vs1, vs2, em, es;    // vigilancia: grave del medio y del lado, y su energia
            private double vuelta, metaVuelta;            // cuanto grave del lado se devuelve en fase (0 o 1, en rampa)
            private readonly float[] sale = new float[2]; // un cuadro ya hecho que se entrego a medias
            private int saleN;
            private bool empezo, acabo;
            private volatile GraveMono anterior;          // la pista que acaba justo antes, sin fundido (disco continuo)

            public GraveMono(NAudio.Wave.ISampleProvider s, float corteHz)
            {
                src = s;
                tasa = s.WaveFormat.SampleRate;
                hz = corteHz;
                activo = s.WaveFormat.Channels == 2 && tasa >= 8000 && corteHz > 0f && corteHz < 0.2f * tasa;
                if (!activo) return;
                double w0 = 2.0 * Math.PI * corteHz / tasa, c = Math.Cos(w0), al = Math.Sin(w0) / Math.Sqrt(2.0), a0 = 1.0 + al;
                a1 = -2.0 * c / a0; a2 = (1.0 - al) / a0; b0 = (1.0 + c) / (2.0 * a0);
                aVigila = 1.0 - Math.Exp(-w0);
                aEnergia = 1.0 - Math.Exp(-1.0 / (VigilaSeg * tasa));
                pasoVuelta = 1.0 / (VueltaSeg * tasa);
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            internal bool Activo { get { return activo; } }

            // La siguiente pista del mismo disco sigue con los filtros de esta. Lo llama la pantalla al preparar el
            // relevo; se toma en la primera lectura, en el hilo del sonido, cuando esta ya acabo.
            internal void Sigue(GraveMono previa) { if (previa != null && !object.ReferenceEquals(previa, this)) anterior = previa; }

            private void Hereda()
            {
                GraveMono a = anterior; anterior = null;
                if (a == null || !a.activo || !a.acabo || a.tasa != tasa || a.hz != hz) return;
                mx1 = a.mx1; mx2 = a.mx2; my1 = a.my1; my2 = a.my2;
                sx1 = a.sx1; sx2 = a.sx2; sy1 = a.sy1; sy2 = a.sy2;
                tx1 = a.tx1; tx2 = a.tx2; ty1 = a.ty1; ty2 = a.ty2;
                zx1 = a.zx1; zx2 = a.zx2; zy1 = a.zy1; zy2 = a.zy2;
                vm1 = a.vm1; vm2 = a.vm2; vs1 = a.vs1; vs2 = a.vs2; em = a.em; es = a.es;
                vuelta = a.vuelta; metaVuelta = a.metaVuelta;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                if (!activo) return src.Read(buffer, offset, count);
                if (!empezo) { empezo = true; Hereda(); }
                int hechos = 0;
                // La otra mitad de un cuadro ya hecho, si la lectura anterior pidio un numero impar.
                if (saleN > 0 && count > 0) { buffer[offset] = sale[1]; saleN = 0; hechos = 1; }
                int pares = (count - hechos) & ~1;
                if (pares > 0 && !acabo)
                {
                    int n = Enteros(buffer, offset + hechos, pares);
                    Cuadros(buffer, offset + hechos, n >> 1);
                    hechos += n;
                }
                // Queda sitio para una sola muestra: se hace el cuadro entero aparte y sale su primera mitad.
                if (count - hechos == 1 && !acabo && Enteros(sale, 0, 2) == 2)
                {
                    Cuadros(sale, 0, 1);
                    buffer[offset + hechos++] = sale[0]; saleN = 1;
                }
                return hechos;
            }

            // Lee del origen hasta 'quiero' muestras (par), siempre cuadros enteros: si llega uno partido, se sigue
            // leyendo hasta completarlo. Solo al final puede quedar medio cuadro, que no se puede completar: se descarta.
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
                double a1 = this.a1, a2 = this.a2, b0 = this.b0, av = aVigila, ae = aEnergia, pv = pasoVuelta;
                double mx1 = this.mx1, mx2 = this.mx2, my1 = this.my1, my2 = this.my2;
                double sx1 = this.sx1, sx2 = this.sx2, sy1 = this.sy1, sy2 = this.sy2;
                double tx1 = this.tx1, tx2 = this.tx2, ty1 = this.ty1, ty2 = this.ty2;
                double zx1 = this.zx1, zx2 = this.zx2, zy1 = this.zy1, zy2 = this.zy2;
                double vm1 = this.vm1, vm2 = this.vm2, vs1 = this.vs1, vs2 = this.vs2, em = this.em, es = this.es;
                double vuelta = this.vuelta, meta = this.metaVuelta;
                for (int f = 0; f < cuadros; f++, i += 2)
                {
                    double l = b[i], r = b[i + 1];
                    double m = 0.5 * (l + r), s = 0.5 * (l - r);
                    double pm = a2 * m + a1 * mx1 + mx2 - a1 * my1 - a2 * my2;       // el medio: paso-todo
                    mx2 = mx1; mx1 = m; my2 = my1; my1 = pm;
                    double h = b0 * (s - 2.0 * sx1 + sx2) - a1 * sy1 - a2 * sy2;      // el lado: paso-alto...
                    sx2 = sx1; sx1 = s; sy2 = sy1; sy1 = h;
                    double h2 = b0 * (h - 2.0 * tx1 + tx2) - a1 * ty1 - a2 * ty2;     // ...dos veces (24 dB/octava)
                    tx2 = tx1; tx1 = h; ty2 = ty1; ty1 = h2;
                    double ps = a2 * s + a1 * zx1 + zx2 - a1 * zy1 - a2 * zy2;       // el lado por el paso-todo
                    zx2 = zx1; zx1 = s; zy2 = zy1; zy1 = ps;
                    // La vigilancia: el grave del medio y el del lado (dos polos en el corte) y su energia.
                    vm1 += (m - vm1) * av; vm2 += (vm1 - vm2) * av; em += (vm2 * vm2 - em) * ae;
                    vs1 += (s - vs1) * av; vs2 += (vs1 - vs2) * av; es += (vs2 * vs2 - es) * ae;
                    if (es > 2.0 * em) meta = 1.0; else if (es < em) meta = 0.0;
                    if (vuelta != meta) { vuelta = vuelta < meta ? Math.Min(meta, vuelta + pv) : Math.Max(meta, vuelta - pv); }
                    double lado = h2;
                    if (vuelta != 0.0) pm += vuelta * (ps - h2);                      // el grave del lado, de vuelta y en fase
                    b[i] = (float)(pm + lado); b[i + 1] = (float)(pm - lado);
                }
                this.mx1 = mx1; this.mx2 = mx2; this.my1 = my1; this.my2 = my2;
                this.sx1 = sx1; this.sx2 = sx2; this.sy1 = sy1; this.sy2 = sy2;
                this.tx1 = tx1; this.tx2 = tx2; this.ty1 = ty1; this.ty2 = ty2;
                this.zx1 = zx1; this.zx2 = zx2; this.zy1 = zy1; this.zy2 = zy2;
                this.vm1 = vm1; this.vm2 = vm2; this.vs1 = vs1; this.vs2 = vs2; this.em = em; this.es = es;
                this.vuelta = vuelta; this.metaVuelta = meta;
            }
        }
    }
}
