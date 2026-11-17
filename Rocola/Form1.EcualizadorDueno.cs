// EL ECUALIZADOR DEL DUENO, LA PARTE DE LA PANTALLA (17 de noviembre de 2026; la etapa esta en EcualizadorDueno.cs).
//
// DOS MEMORIAS: una curva para el sonido normal (eq_dueno=) y otra para el modo billar (eq_dueno_billar=), cada una con diez numeros en dB
// separados por ';' (punto decimal, un decimal; de -12 a 12 en pasos de 0,5; si faltan o vienen mal, 0). La cadena arma la del modo que este
// puesto (el guardado: durante «Comparar a ciegas» el modo cambia en vivo pero el ecualizador NO, LocalAutoAGuardar) y, si el modo cambia,
// AplicaBocinas la lleva a la que suena y a la preparada con la rampa de la etapa. Se guardan siempre (SaveConfigPrefs).
//
// LO QUE USA LA VENTANA (todo en el hilo de la pantalla):
//   EqDuenoMedios(billar) / EqDuenoPuesto()      las 10 bandas en medios de dB (copia)
//   EqDuenoDb(billar, banda)                     una banda en dB
//   EqDuenoCambiaBanda(banda, db)                cambia una banda del modo puesto y se oye al momento (la que suena y la preparada)
//   EqDuenoCero(billar)                          pone en 0 todas las bandas de un modo
//   EqDuenoPreganDb(billar)                      la preganancia que resulta (dB, 0 o negativa)
//   EqDuenoRespuesta(billar, hz)                 la curva del dueno (dB, sin preganancia) en esas frecuencias
//   EqDuenoFijoRespuesta(hz)                     LO QUE YA HACE LA ROCOLA en el modo puesto, aproximado (para dibujar de fondo)
//   EqDuenoModoBillar                            ¿el modo puesto es el billar?
using System;

namespace Rocola
{
    public partial class Form1
    {
        // Medios de dB de cada banda, por modo (de fabrica todo 0: la cadena sale bit a bit como antes).
        private static readonly int[] eqDuenoNormal = new int[EcualizadorDueno.Bandas];
        private static readonly int[] eqDuenoBillar = new int[EcualizadorDueno.Bandas];

        // El modo del ecualizador: el billar guardado (no el que «Comparar a ciegas» cambia en vivo).
        internal bool EqDuenoModoBillar { get { return LocalAutoAGuardar; } }
        private int[] EqDuenoArreglo(bool billar) { return billar ? eqDuenoBillar : eqDuenoNormal; }

        internal int[] EqDuenoMedios(bool billar) { return (int[])EqDuenoArreglo(billar).Clone(); }
        internal int[] EqDuenoPuesto() { return EqDuenoMedios(EqDuenoModoBillar); }
        internal double EqDuenoDb(bool billar, int banda) { return banda < 0 || banda >= EcualizadorDueno.Bandas ? 0.0 : EqDuenoArreglo(billar)[banda] * 0.5; }

        // Cambia una banda del modo puesto (dB, a medio dB) y la lleva ya a la cancion que suena y a la preparada.
        internal void EqDuenoCambiaBanda(int banda, double db)
        {
            if (banda < 0 || banda >= EcualizadorDueno.Bandas || double.IsNaN(db) || double.IsInfinity(db)) return;
            EqDuenoArreglo(EqDuenoModoBillar)[banda] = EcualizadorDueno.Acota((int)Math.Round(db * 2.0, MidpointRounding.AwayFromZero));
            AplicaEqDueno();
        }

        // Todas las bandas de un modo en 0 (si es el puesto, se oye al momento).
        internal void EqDuenoCero(bool billar)
        {
            int[] a = EqDuenoArreglo(billar);
            for (int i = 0; i < a.Length; i++) a[i] = 0;
            AplicaEqDueno();
        }

        // La frecuencia de muestreo con la que se calculan las curvas: la de la salida, o 44,1 kHz si aun no suena nada.
        private int EqDuenoTasa()
        {
            try { CadenaCancion c = cadenaSonando; if (c != null && c.EqDueno != null) return c.EqDueno.WaveFormat.SampleRate; } catch { }
            return frecuenciaSalida > 0 ? frecuenciaSalida : 44100;
        }

        internal double EqDuenoPreganDb(bool billar) { return EcualizadorDueno.PreganDb(EqDuenoArreglo(billar), EqDuenoTasa()); }
        internal double[] EqDuenoRespuesta(bool billar, double[] hz) { return EcualizadorDueno.Respuesta(EqDuenoArreglo(billar), EqDuenoTasa(), hz); }

        // Lleva el ecualizador a lo que suena y a la siguiente ya preparada (la etapa lo toma con su rampa de 50 ms por 12 dB).
        private void AplicaEqDueno()
        {
            CadenaCancion c = cadenaSonando, p = cadenaPreparada;
            int[] m = EqDuenoPuesto();
            if (c != null && c.EqDueno != null) c.EqDueno.Ajusta(m);
            if (p != null && p.EqDueno != null) p.EqDueno.Ajusta(m);
        }

        // ---- LO QUE YA HACE LA ROCOLA, para dibujarlo de fondo ----
        // La respuesta en magnitud (dB) APROXIMADA de las etapas FIJAS del modo puesto: el corte del subgrave (Butterworth de 4.o orden; en el
        // billar a 40 Hz como poco), el estante de graves y el de agudos del local (con lo que baja el modo noche), la campana de la claridad
        // y la inclinacion fija del billar o la afinacion con el celular (dB por banda del EQ de cada cancion, interpolados en log de
        // frecuencia). NO lleva el EQ automatico de cada cancion ni las etapas dinamicas (retumbe, presencia, ataque, eses, bombo, partes
        // suaves), que cambian solas con la musica; ni el grave en mono, que no cambia el nivel. Por eso es una guia, no una medida.
        internal double[] EqDuenoFijoRespuesta(double[] hz)
        {
            int tasa = EqDuenoTasa();
            double[] r = new double[hz.Length];
            AjusteLocal loc = LocalDe(cadenaSonando);
            if (sinMejoras) { loc.Claridad = false; loc.Graves = 0; loc.Agudos = 0; }
            // el subgrave
            double fc = SubgraveEf;
            if (loc.Subgrave && fc > 0f && fc < 0.1f * tasa)
                for (int i = 0; i < hz.Length; i++) { double q = hz[i] > 0 ? Math.Pow(fc / hz[i], 8.0) : 1e30; r[i] += -10.0 * Math.Log10(1.0 + q); }
            double[] c = new double[5];
            // la claridad (campana)
            if (loc.Claridad && SalaLocal.ClaridadHz < 0.45 * tasa)
            {
                double A = Math.Pow(10.0, SalaLocal.ClaridadDb / 40.0), w0 = 2.0 * Math.PI * SalaLocal.ClaridadHz / tasa, al = Math.Sin(w0) / (2.0 * SalaLocal.ClaridadQ), cs = Math.Cos(w0);
                double a0 = 1.0 + al / A;
                c[0] = (1.0 + al * A) / a0; c[1] = -2.0 * cs / a0; c[2] = (1.0 - al * A) / a0; c[3] = -2.0 * cs / a0; c[4] = (1.0 - al / A) / a0;
                SumaBiquad(r, hz, c, tasa);
            }
            // los estantes del local: graves (120 Hz) y agudos (6 kHz)
            double gr = loc.Graves - (double)SalaLocal.GravesNocheDb;
            if (gr != 0.0) { Estante(false, gr, SalaLocal.GravesHz, tasa, c); SumaBiquad(r, hz, c, tasa); }
            if (loc.Agudos != 0 && SalaLocal.AgudosHz < 0.45 * tasa) { Estante(true, loc.Agudos, SalaLocal.AgudosHz, tasa, c); SumaBiquad(r, hz, c, tasa); }
            // la inclinacion fija del billar, o la afinacion con el celular
            if (localAuto)
            {
                float[] bandas = ConLocalAfinado(null);   // la afinacion en las 8 bandas del EQ (con su tope), o null si no esta en uso
                if (bandas == null) bandas = InclinacionBillar;
                for (int i = 0; i < hz.Length; i++) r[i] += InterpolaEq(bandas, hz[i]);
            }
            return r;
        }

        private static void Estante(bool alto, double db, double hz, int tasa, double[] c)
        {
            double w = 2.0 * Math.PI * hz / tasa;
            SalaLocal.Estante(alto, db, Math.Sin(w), Math.Cos(w), out c[0], out c[1], out c[2], out c[3], out c[4]);
        }
        private static void SumaBiquad(double[] r, double[] hz, double[] c, int tasa)
        {
            for (int i = 0; i < hz.Length; i++) if (hz[i] > 0 && hz[i] < 0.5 * tasa) r[i] += EcualizadorDueno.MagDb(c, 0, 2.0 * Math.PI * hz[i] / tasa);
        }
        // dB por banda del EQ de cada cancion (EqCentros) llevados a una frecuencia: lineal en log de frecuencia, plano mas alla de los extremos.
        private static double InterpolaEq(float[] db, double f)
        {
            float[] cen = EqCentros;
            if (f <= cen[0]) return db[0];
            if (f >= cen[cen.Length - 1]) return db[cen.Length - 1];
            int i = 0; while (i < cen.Length - 2 && f > cen[i + 1]) i++;
            double t = Math.Log(f / cen[i]) / Math.Log(cen[i + 1] / cen[i]);
            return db[i] + (db[i + 1] - db[i]) * t;
        }

        // ---- La configuracion: eq_dueno= y eq_dueno_billar= ----
        // «0.0;0.0;1.5;...» (10 numeros, punto decimal, ';'). Lo que falta o viene mal es 0; todo acotado a -12..12 dB.
        internal static void EqDuenoLee(string texto, int[] destino)
        {
            string[] p = (texto ?? "").Split(';');
            for (int i = 0; i < destino.Length; i++)
            {
                double v;
                if (i < p.Length && double.TryParse(p[i].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)
                    && !double.IsNaN(v) && !double.IsInfinity(v))
                    destino[i] = EcualizadorDueno.Acota((int)Math.Round(Math.Max(-100.0, Math.Min(100.0, v)) * 2.0, MidpointRounding.AwayFromZero));
                else destino[i] = 0;
            }
        }
        internal static string EqDuenoTexto(int[] medios)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < medios.Length; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append((medios[i] * 0.5).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }
}
