// EL MODO NOCHE SIN PERDER GRAVES NI AGUDOS
//
// De madrugada la rocola baja 4 dB (Form1.Sonido.cs, "MODO NOCHE"). Pero el oido no pierde lo mismo en todas las
// frecuencias: al bajar el volumen, los graves y los agudos mas altos se apagan antes que la voz. Lo dicen las curvas
// de igual sonoridad de la norma ISO 226. Por cada dB que se baja, para que se oigan bajar lo mismo que un tono de
// 1 kHz, hay que bajar MENOS: 0,42 dB menos en 50 Hz, 0,31 en 100 Hz, 0,20 en 200 Hz y 0,16 en 12,5 kHz; entre 1 y
// 6 kHz, lo mismo. Y eso casi no depende de lo fuerte que suene el local: calculado a 60, 75 y 90 fonios cambia menos
// de una decima de dB (tablas de la edicion 2003; sus autores dicen que la de 2023 se aparta 0,6 dB como mucho).
//
// Aqui se devuelve eso mientras el modo noche baja el volumen, con dos estantes suaves:
//   graves: primer orden a 160 Hz, 0,44 dB por cada dB que baja la noche;
//   agudos: segundo orden (Butterworth) a 7,5 kHz, 0,24 dB por cada dB.
// Con los 4 dB de fabrica: +1,6 dB en 50 Hz, +1,3 en 100, +0,8 en 200, +0,6 en 12,5 kHz y +0,9 arriba del todo. De
// 40 Hz a 12,5 kHz se aparta de la norma 0,07 dB de media y 0,2 como mucho (pruebas/noche.cs). Los graves siguen
// bajando, pero menos: en 50 Hz, 2,4 dB en vez de 4.
//
// Van EN PARALELO sobre filtros fijos: sale = entra + kg * pasobajo(entra) + ka * pasoalto(entra). Solo cambian las
// dos mezclas, en rampa de 20 ms, y siguen la rampa de 20 s del modo noche. De dia las dos son cero: si la cancion
// empieza de dia ni se calcula (sale bit a bit lo que entra); si la noche llega a media cancion, los filtros se
// ponen en marcha y ya siguen la musica hasta el final (al amanecer vuelven a cero y sale otra vez tal cual).
//
// Va despues del nivelado y antes del grave en mono, del grave sintetico y del limitador: los armonicos del grave
// sintetico siguen al grave ya compensado, y el limitador sigue siendo lo ultimo. Los videos no pasan por aqui (los
// baja el reproductor de Windows): de noche bajan igual, sin compensar.
//
// Disco continuo: como el grave en mono (GraveMono.cs), la pista siguiente sigue con los filtros de la anterior.
//
// "noche_compensa=" en la configuracion: cuanto de lo que dice la norma se devuelve, en % (de fabrica 100; 0 lo quita
// y deja solo la bajada de volumen).
using System;

namespace Rocola
{
    public partial class Form1
    {
        private static float nocheCompensa = 1f;   // "noche_compensa=" en config, en %: fraccion de lo que dice la norma
        private CompensaNoche curNoche;            // la de la cancion que suena (para seguir la rampa en vivo)

        private static float NocheCompensa(string texto)
        {
            float v;
            if (!float.TryParse(texto.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)
                || float.IsNaN(v) || float.IsInfinity(v)) return nocheCompensa;
            return Clampf(v / 100f, 0f, 1f);
        }

        // Lo que el modo noche esta bajando AHORA, en dB, por la parte que se compensa. 0 de dia, con el modo noche
        // apagado o con noche_compensa=0. Sigue la rampa: mientras baja, va dando los valores de en medio.
        private float NocheCompensaDb()
        {
            float f = FactorNoche();
            if (!(f > 0f) || f >= 1f || !(nocheCompensa > 0f)) return 0f;
            return (float)(-20.0 * Math.Log10(f)) * Math.Min(1f, nocheCompensa);
        }

        private sealed class CompensaNoche : NAudio.Wave.ISampleProvider
        {
            internal const float GraveHz = 160f, GraveDbPorDb = 0.44f;    // estante de graves (primer orden)
            internal const float AgudoHz = 7500f, AgudoDbPorDb = 0.24f;   // estante de agudos (segundo orden)
            internal const float TopeDb = 24f;                             // lo mas que puede bajar la noche (noche_db)
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch, tasa, rampa;
            private readonly bool conAgudo;                   // con 16 kHz o menos no queda banda de agudos que compensar
            private readonly double gb, ga;                   // paso-bajo: y = gb (x + x1) - ga y1 (bilineal, un polo)
            private readonly double hb, ha1, ha2;             // paso-alto: y = hb (x - 2 x1 + x2) - ha1 y1 - ha2 y2
            private readonly double[] gx, gy, hx1, hx2, hy1, hy2;   // estado de cada canal
            public volatile float Db;                         // dB que baja la noche AHORA, ya por noche_compensa (0 = nada)
            private float visto = -1f;                        // el ultimo Db que se paso a mezclas
            private float kg, ka, og, oa, pg, pa;             // mezclas actuales, pedidas y paso de su rampa
            private int quedan, canal;                        // cuadros de rampa que faltan; canal de la muestra siguiente
            private bool empezo, vivo, acabo;
            private volatile CompensaNoche anterior;          // la pista que acaba justo antes, sin fundido

            public CompensaNoche(NAudio.Wave.ISampleProvider s)
            {
                src = s; ch = Math.Max(1, s.WaveFormat.Channels);
                tasa = s.WaveFormat.SampleRate; int fs = tasa < 8000 ? 44100 : tasa;
                rampa = Math.Max(1, (int)(fs * .020));
                double k = Math.Tan(Math.PI * GraveHz / fs);
                gb = k / (1.0 + k); ga = (k - 1.0) / (k + 1.0);
                conAgudo = AgudoHz < 0.45f * fs;
                double w0 = 2.0 * Math.PI * AgudoHz / fs, c = Math.Cos(w0), al = Math.Sin(w0) / Math.Sqrt(2.0), a0 = 1.0 + al;
                hb = (1.0 + c) / (2.0 * a0); ha1 = -2.0 * c / a0; ha2 = (1.0 - al) / a0;
                gx = new double[ch]; gy = new double[ch]; hx1 = new double[ch]; hx2 = new double[ch]; hy1 = new double[ch]; hy2 = new double[ch];
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }

            // La siguiente pista del mismo disco sigue con los filtros de esta (ver GraveMono.Sigue).
            internal void Sigue(CompensaNoche previa) { if (previa != null && !object.ReferenceEquals(previa, this)) anterior = previa; }

            public int Read(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                if (n <= 0) { acabo = true; return n; }
                Pide(Db);
                if (!empezo) { empezo = true; kg = og; ka = oa; quedan = 0; Hereda(); }   // al empezar, la pedida sin rampa
                if (!vivo)
                {
                    if (kg == 0f && ka == 0f && quedan == 0) { canal = (canal + n) % ch; return n; }   // de dia: tal cual, sin coste
                    vivo = true;
                }
                Muestras(buffer, offset, n);
                return n;
            }

            // Los dB pedidos, en mezclas de los dos estantes. Si cambian, se va hacia ellas en 20 ms.
            private void Pide(float db)
            {
                if (!(db > 0f)) db = 0f; else if (db > TopeDb) db = TopeDb;   // NaN, negativo o cero: nada
                if (db == visto) return;
                visto = db;
                og = db > 0f ? (float)(Math.Pow(10.0, db * GraveDbPorDb / 20.0) - 1.0) : 0f;
                oa = (db > 0f && conAgudo) ? (float)(Math.Pow(10.0, db * AgudoDbPorDb / 20.0) - 1.0) : 0f;
                if (empezo) Rampa();
            }
            private void Rampa()
            {
                if (kg == og && ka == oa) { quedan = 0; return; }
                quedan = rampa; pg = (og - kg) / rampa; pa = (oa - ka) / rampa;
            }

            // La anterior ya acabo: sus filtros y sus mezclas siguen aqui, como si fuera la misma cancion.
            private void Hereda()
            {
                CompensaNoche a = anterior; anterior = null;
                if (a == null || !a.acabo || a.ch != ch || a.tasa != tasa) return;
                Array.Copy(a.gx, gx, ch); Array.Copy(a.gy, gy, ch);
                Array.Copy(a.hx1, hx1, ch); Array.Copy(a.hx2, hx2, ch); Array.Copy(a.hy1, hy1, ch); Array.Copy(a.hy2, hy2, ch);
                kg = a.kg; ka = a.ka; vivo = a.vivo;
                Rampa();
            }

            private void Muestras(float[] b, int desde, int n)
            {
                int c = canal, fin = desde + n;
                double gb = this.gb, ga = this.ga, hb = this.hb, ha1 = this.ha1, ha2 = this.ha2;
                for (int i = desde; i < fin; i++)
                {
                    if (c == 0 && quedan > 0) { kg += pg; ka += pa; if (--quedan == 0) { kg = og; ka = oa; } }
                    double x = b[i];
                    double g = gb * (x + gx[c]) - ga * gy[c];
                    gx[c] = x; gy[c] = g;
                    double h = 0.0;
                    if (conAgudo)
                    {
                        h = hb * (x - 2.0 * hx1[c] + hx2[c]) - ha1 * hy1[c] - ha2 * hy2[c];
                        hx2[c] = hx1[c]; hx1[c] = x; hy2[c] = hy1[c]; hy1[c] = h;
                    }
                    if (kg != 0f || ka != 0f) b[i] = (float)(x + kg * g + ka * h);
                    if (++c == ch) c = 0;
                }
                canal = c;
            }
        }
    }
}
