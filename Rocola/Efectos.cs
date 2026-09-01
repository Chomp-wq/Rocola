// ================================================================================================
//  LOS SONIDOS DE LA PROPIA ROCOLA
// ------------------------------------------------------------------------------------------------
//  La rocola sabia dibujar un cielo que late con la musica y girar los discos en tres dimensiones,
//  y no emitia NI UN SOLO SONIDO PROPIO: se metia una moneda y no se oia nada. En un bar eso importa,
//  porque una rocola es un mueble que HACE RUIDO cuando la usas -por eso se sabe desde la barra, y de
//  espaldas, que alguien acaba de pagar-.
//
//  TRES DECISIONES QUE MANDAN SOBRE TODO LO DEMAS:
//
//  1) SE FABRICAN CON CUENTAS, NO SON ARCHIVOS. Un .wav al lado del programa se pierde, se borra, hay
//     que acordarse de meterlo en el .sfs y suena distinto si alguien lo cambia. Una cuenta pesa cero,
//     viaja dentro del ejecutable y suena IGUAL en las tres maquinas. Ademas se puede probar sin
//     bocinas: pruebas/efectos.cs le mira la forma a cada uno (ver alli).
//
//  2) NUNCA TAPAN LA MUSICA. El efecto no se suma y ya: mientras suena, la cancion se AGACHA un poco
//     y luego vuelve. Y se agacha con RAMPA, no de golpe: bajarle el volumen a la cancion de un bloque
//     al siguiente es un escalon en mitad de la onda, o sea otro chasquido encima del efecto.
//
//  3) CUANDO NO SUENA NINGUNO, LA MUSICA PASA INTACTA. Es el 99% del tiempo. La mezcla no toca ni una
//     muestra si no hay nada que mezclar: se sale por arriba antes de entrar al bucle. Estropear lo
//     que la gente vino a oir, a cambio de un efecto que casi nunca suena, seria un mal negocio.
// ================================================================================================
using System;

namespace Rocola
{
    // Los seis. El orden no importa; los nombres si, porque con ellos los pide todo lo demas.
    internal enum Efecto
    {
        Moneda,       // entra una moneda: metal cayendo + el ding del credito
        Brazo,        // arranca una cancion: el brazo, el tope, el disco y la aguja
        Cola,         // la cancion entro a la cola: un golpe seco y corto
        SinCredito,   // no hay creditos: dos tonos que bajan, el "no" de toda la vida
        Aguja,        // la aguja sobre el vinilo (va sola o pegada al brazo)
        Tic           // moverse entre discos: casi nada, a proposito
    }

    // ============================================================================================
    //  EL FABRICANTE
    // --------------------------------------------------------------------------------------------
    //  Cada efecto se fabrica UNA vez, en mono y a la frecuencia de la tarjeta, y se guarda. El mas
    //  largo -el brazo con su aguja- son unos 230 KB; los seis juntos no llegan a medio mega.
    //
    //  Mono a proposito: un efecto de rocola no viene de la izquierda ni de la derecha, viene de la
    //  maquina. Ademas cuesta la mitad de memoria y la mezcla lo reparte igual a los dos canales.
    // ============================================================================================
    internal static class Efectos
    {
        private static readonly object candado = new object();
        private static readonly float[][] hechos = new float[6][];
        private static int frecHecha = 0;

        // Lo que dura cada uno, en segundos. Esta aqui y no dentro de la cuenta para que se pueda
        // preguntar SIN fabricarlo (lo usa el que decide cuanto tiempo dejar la salida abierta).
        private static readonly double[] DURA = { 1.05, 2.70, 0.25, 0.50, 1.45, 0.012 };

        // El pico al que se deja cada uno. No son todos iguales a proposito: la moneda tiene que
        // OIRSE en un bar lleno y el tic no tiene que oirse casi nada.
        private static readonly float[] PICO = { 0.85f, 0.75f, 0.80f, 0.70f, 0.35f, 0.30f };

        public static double Segundos(Efecto cual)
        {
            int i = (int)cual;
            return (i >= 0 && i < DURA.Length) ? DURA[i] : 0.0;
        }

        public static float[] Muestra(Efecto cual, int frecuencia)
        {
            if (frecuencia < 8000) frecuencia = 44100;
            int i = (int)cual;
            if (i < 0 || i >= hechos.Length) return new float[0];
            lock (candado)
            {
                // Si la tarjeta cambio de frecuencia (una cancion a 48 kHz detras de una a 44.1) se
                // rehacen: un efecto fabricado para otra frecuencia sonaria mas agudo o mas grave.
                if (frecuencia != frecHecha) { for (int k = 0; k < hechos.Length; k++) hechos[k] = null; frecHecha = frecuencia; }
                if (hechos[i] == null) hechos[i] = Fabrica(cual, frecuencia);
                return hechos[i];
            }
        }

        // ---------------------------------------------------------------------------------- piezas
        // Un ruido barato y REPETIBLE. Repetible importa: si el ruido saliera distinto cada arranque,
        // la bateria no podria comprobar nada y el mismo efecto sonaria un poco distinto cada noche.
        private struct Az
        {
            private uint s;
            public Az(uint semilla) { s = semilla | 1u; }
            public float Sig() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s >> 8) * (2f / 16777216f) - 1f; }
        }

        // Un filtro de dos polos (RBJ). Se usa para las campanas y para dejar el ruido en la altura
        // que toca; con uno de un polo el ruido sale demasiado ancho y todo suena a "shhh".
        private struct Biq
        {
            private float b0, b1, b2, a1, a2, x1, x2, y1, y2;
            public static Biq PasaBanda(float hz, float q, int fs)
            {
                Biq f = new Biq();
                double w = 2.0 * Math.PI * hz / fs, al = Math.Sin(w) / (2.0 * q), cs = Math.Cos(w);
                double a0 = 1 + al;
                f.b0 = (float)(al / a0); f.b1 = 0f; f.b2 = (float)(-al / a0);
                f.a1 = (float)(-2 * cs / a0); f.a2 = (float)((1 - al) / a0);
                return f;
            }
            public static Biq PasaBajo(float hz, float q, int fs)
            {
                Biq f = new Biq();
                double w = 2.0 * Math.PI * hz / fs, al = Math.Sin(w) / (2.0 * q), cs = Math.Cos(w);
                double a0 = 1 + al;
                f.b0 = (float)((1 - cs) / 2 / a0); f.b1 = (float)((1 - cs) / a0); f.b2 = f.b0;
                f.a1 = (float)(-2 * cs / a0); f.a2 = (float)((1 - al) / a0);
                return f;
            }
            public static Biq PasaAlto(float hz, float q, int fs)
            {
                Biq f = new Biq();
                double w = 2.0 * Math.PI * hz / fs, al = Math.Sin(w) / (2.0 * q), cs = Math.Cos(w);
                double a0 = 1 + al;
                f.b0 = (float)((1 + cs) / 2 / a0); f.b1 = (float)(-(1 + cs) / a0); f.b2 = f.b0;
                f.a1 = (float)(-2 * cs / a0); f.a2 = (float)((1 - al) / a0);
                return f;
            }
            public float Pasa(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        // Suma una campana: una sola altura que se apaga sola.
        private static void Campana(float[] b, int fs, double t0, double hz, double dur, double vol)
        {
            int ini = (int)(t0 * fs), n = (int)(dur * fs);
            double w = 2.0 * Math.PI * hz / fs, k = 5.0 / Math.Max(1, n);
            for (int i = 0; i < n; i++)
            {
                int p = ini + i; if (p < 0) continue; if (p >= b.Length) break;
                b[p] += (float)(Math.Sin(w * i) * vol * Math.Exp(-k * i));
            }
        }

        // Suma un golpe de ruido ya puesto a su altura.
        private static void Golpe(float[] b, int fs, double t0, double dur, double vol, Biq filtro, ref Az az)
        {
            int ini = (int)(t0 * fs), n = (int)(dur * fs);
            double k = 6.0 / Math.Max(1, n);
            for (int i = 0; i < n; i++)
            {
                int p = ini + i; if (p < 0) continue; if (p >= b.Length) break;
                b[p] += filtro.Pasa(az.Sig()) * (float)(vol * Math.Exp(-k * i));
            }
        }

        // ------------------------------------------------------------------------------ los seis
        private static float[] Fabrica(Efecto cual, int fs)
        {
            int n = (int)(Segundos(cual) * fs) + 1;
            float[] b = new float[n];
            Az az = new Az(0x9E3779B9u + (uint)cual * 2654435761u);   // semilla propia y fija por efecto

            switch (cual)
            {
                // ---- LA MONEDA: metal que rebota por la ranura, y el ding del credito al entrar ----
                // El metal no suena "afinado": sus alturas no son multiplos exactos de una nota, y por
                // eso suena a metal y no a flauta. Esos numeros raros son justo eso.
                case Efecto.Moneda:
                    {
                        double[] parcial = { 1.0, 2.76, 5.40, 8.93 };
                        for (int r = 0; r < 3; r++)   // tres rebotes, cada uno mas flojo y mas corto
                        {
                            double t0 = r * 0.115, v = 0.30 / (r + 1.2);
                            for (int k = 0; k < parcial.Length; k++)
                                Campana(b, fs, t0, 1750.0 * parcial[k] * (1 - r * 0.04), 0.24 - r * 0.05, v / (k + 1.4));
                            Biq tk = Biq.PasaBanda(4200f, 2.5f, fs);
                            Golpe(b, fs, t0, 0.05, 0.30 / (r + 1), tk, ref az);
                        }
                        // Y el credito entra: campanita clara, que es lo que hace levantar la vista.
                        Campana(b, fs, 0.46, 1568, 0.55, 0.34);
                        Campana(b, fs, 0.46, 2350, 0.40, 0.16);
                        Campana(b, fs, 0.465, 3136, 0.28, 0.09);
                        break;
                    }

                // ---- EL BRAZO: motor, tope, el disco que se posa, y la aguja ----
                case Efecto.Brazo:
                    {
                        // El motor: una sierra grave que sube de vueltas y frena, con el filtro abriendo.
                        int fin = (int)(1.0 * fs);
                        Biq lp = Biq.PasaBajo(420f, 0.8f, fs);
                        double fase = 0;
                        for (int i = 0; i < fin && i < n; i++)
                        {
                            double u = (double)i / fin;
                            double hz = (u < 0.55) ? 66 + (88 - 66) * (u / 0.55) : 88 - (88 - 58) * ((u - 0.55) / 0.45);
                            fase += hz / fs;
                            float sierra = (float)(2.0 * (fase - Math.Floor(fase)) - 1.0);
                            double env = (u < 0.09) ? u / 0.09 : (u > 0.80 ? (1 - (u - 0.80) / 0.20) : 1.0);
                            b[i] += lp.Pasa(sierra) * (float)(0.30 * env);
                        }
                        Campana(b, fs, 1.02, 78, 0.28, 0.55);            // el tope: golpe sordo
                        Biq lpG = Biq.PasaBajo(520f, 0.7f, fs);
                        Golpe(b, fs, 1.02, 0.12, 0.55, lpG, ref az);
                        Campana(b, fs, 1.38, 120, 0.20, 0.28);           // el disco que se posa
                        Biq bpD = Biq.PasaBanda(900f, 1.1f, fs);
                        Golpe(b, fs, 1.38, 0.09, 0.26, bpD, ref az);
                        Vinilo(b, fs, 1.72, 0.90, 0.30, ref az);         // y la aguja aterriza
                        break;
                    }

                // ---- A LA COLA: un golpe seco y CLARO, para no confundirlo con la moneda ----
                // Se le carga la mano a los medios y se le quita cuerpo: la moneda vive arriba del todo
                // y el aviso de sin creditos abajo, asi que este se queda en medio y los tres se
                // distinguen con los ojos cerrados (lo comprueba la bateria midiendo su huella).
                case Efecto.Cola:
                    {
                        Biq bp = Biq.PasaBanda(1050f, 1.0f, fs);
                        Golpe(b, fs, 0.0, 0.09, 1.00, bp, ref az);
                        Biq bp2 = Biq.PasaBanda(2200f, 1.8f, fs);
                        Golpe(b, fs, 0.0, 0.04, 0.30, bp2, ref az);      // una punta arriba: la madera que suena
                        Campana(b, fs, 0.0, 300, 0.13, 0.55);            // y el cuerpo, que es lo que lo separa de la moneda
                        break;
                    }

                // ---- SIN CREDITOS: dos tonos que BAJAN, el "no" de toda la vida, y grave ----
                case Efecto.SinCredito:
                    {
                        Biq lp = Biq.PasaBajo(760f, 0.9f, fs);
                        double[] hz = { 233, 175 };
                        double[] t0 = { 0.0, 0.175 };
                        double[] dur = { 0.17, 0.30 };
                        for (int k = 0; k < 2; k++)
                        {
                            int ini = (int)(t0[k] * fs), largo = (int)(dur[k] * fs);
                            double w = 2.0 * Math.PI * hz[k] / fs;
                            for (int i = 0; i < largo; i++)
                            {
                                int p = ini + i; if (p >= n) break;
                                // Onda cuadrada suavizada: zumba como un timbre viejo, no como un pito.
                                float cuad = Math.Sin(w * i) >= 0 ? 1f : -1f;
                                double env = Math.Min(1.0, i / (0.012 * fs)) * Math.Exp(-3.2 * i / largo);
                                b[p] += lp.Pasa(cuad) * (float)(0.42 * env);
                            }
                        }
                        break;
                    }

                // ---- LA AGUJA: el crepitar del vinilo antes de la primera nota ----
                case Efecto.Aguja:
                    Vinilo(b, fs, 0.0, 1.40, 0.55, ref az);
                    break;

                // ---- EL TIC: doce milesimas de segundo. Tiene que NO oirse casi ----
                case Efecto.Tic:
                    {
                        Biq hp = Biq.PasaAlto(3800f, 0.8f, fs);
                        Golpe(b, fs, 0.0, 0.010, 1.00, hp, ref az);
                        break;
                    }
            }

            Remata(b, fs, PICO[(int)cual]);
            return b;
        }

        // El crepitar: un siseo con cuerpo mas los chasquidos sueltos del polvo.
        private static void Vinilo(float[] b, int fs, double t0, double dur, double vol, ref Az az)
        {
            int ini = (int)(t0 * fs), n = (int)(dur * fs);
            Biq bp = Biq.PasaBanda(2600f, 0.7f, fs);
            for (int i = 0; i < n; i++)
            {
                int p = ini + i; if (p < 0) continue; if (p >= b.Length) break;
                double u = (double)i / n;
                double env = Math.Min(1.0, i / (0.05 * fs)) * (1.0 - 0.6 * u);
                b[p] += bp.Pasa(az.Sig()) * (float)(vol * 0.35 * env);
            }
            Biq hp = Biq.PasaAlto(1600f, 0.8f, fs);
            for (int k = 0; k < 11; k++)   // los chasquidos: repartidos, no en fila
            {
                double tp = t0 + 0.04 + (0.5 + 0.5 * az.Sig()) * (dur - 0.10);
                Golpe(b, fs, tp, 0.008, vol * (0.35 + 0.25 * (0.5 + 0.5 * az.Sig())), hp, ref az);
            }
        }

        // EL REMATE, y es lo que evita el peor fallo de un efecto hecho a mano: que ARRANQUE A MITAD DE
        // ONDA. Un sonido que empieza en un valor distinto de cero da un golpe seco en la bocina -y otro
        // al cortarse-, y eso se oye como si algo se rompiera. Aqui se le pone una entrada y una salida
        // suaves, y de paso se deja en el pico que le toca para que no reviente encima de la musica.
        private static void Remata(float[] b, int fs, float pico)
        {
            // LA ENTRADA VA PRIMERO Y ES CORTA. Un milesimo de segundo basta para que no haya golpe en
            // la bocina, y es lo bastante corto para no comerse el ATAQUE -que en un golpe seco es
            // justo lo que lo hace sonar a golpe-. Con dos milesimas, el tic salia a un tercio de su
            // fuerza y el golpe de la cola perdia 1.7 dB: la entrada le estaba mordiendo la punta.
            int ent = Math.Max(2, (int)(0.001 * fs));
            int sal = Math.Max(4, (int)(0.012 * fs));
            if (ent + sal > b.Length) { ent = b.Length / 5; sal = b.Length / 3; }
            for (int i = 0; i < b.Length; i++)
            {
                float e = 1f;
                if (i < ent) e = (float)(0.5 - 0.5 * Math.Cos(Math.PI * i / ent));
                else if (i >= b.Length - sal) e = (float)(0.5 - 0.5 * Math.Cos(Math.PI * (b.Length - 1 - i) / sal));
                b[i] *= e;
            }
            // Y EL PICO SE AJUSTA DESPUES, no antes: si se ajustara antes, la envolvente volveria a
            // bajarlo y el efecto saldria mas flojo de lo pedido sin que nadie se enterara.
            float m = 0f;
            for (int i = 0; i < b.Length; i++) { float a = Math.Abs(b[i]); if (a > m) m = a; }
            if (m > 1e-6f) { float g = pico / m; for (int i = 0; i < b.Length; i++) b[i] *= g; }
        }
    }

    // ============================================================================================
    //  LA MEZCLA
    // --------------------------------------------------------------------------------------------
    //  Va en la cadena de sonido ENTRE el bajo psicoacustico y el limitador. Ahi el limitador -que ya
    //  estaba- se vuelve la red que impide que la suma de musica y efecto se pase de la raya, que es
    //  exactamente su trabajo. Poner los efectos DESPUES del limitador dejaria la suma sin vigilancia.
    // ============================================================================================
    internal sealed class MezclaEfectos : NAudio.Wave.ISampleProvider
    {
        private readonly NAudio.Wave.ISampleProvider src;
        private readonly int ch, fs;
        private readonly object candado = new object();

        public volatile float Volumen = 0.7f;   // 0 = apagado del todo (y entonces ni se toca la musica)

        private float[] voz;      // el efecto que suena ahora (mono)
        private int vozPos;
        private float duck = 1f;  // cuanto se esta agachando la musica AHORA MISMO
        private readonly float pasoDuck;

        // CUANTO SE AGACHA LA MUSICA. Un decibel y pico: lo justo para que el efecto se abra paso sin
        // que nadie diga "se bajo la cancion". Y la rampa dura 15 ms, que a 44.1 kHz son unas 660
        // muestras: el cambio por muestra queda en dos diezmilesimas, o sea imposible de oir como un
        // escalon (que es lo que se oiria si se bajara de un bloque al siguiente).
        private const float DuckMeta = 0.87f;
        private const double RampaSeg = 0.015;

        public MezclaEfectos(NAudio.Wave.ISampleProvider fuente)
        {
            src = fuente;
            ch = Math.Max(1, fuente.WaveFormat.Channels);
            fs = fuente.WaveFormat.SampleRate; if (fs < 8000) fs = 44100;
            pasoDuck = (float)((1.0 - DuckMeta) / (RampaSeg * fs));
        }

        public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }

        // ¿Hay algo sonando ahora? Lo pregunta quien decide cuanto tiempo dejar abierta la tarjeta
        // cuando no hay musica.
        public bool Sonando { get { lock (candado) return voz != null; } }

        // Dispara uno. Si ya habia otro sonando, este manda: en una rocola los momentos van seguidos
        // (moneda, moneda, cancion) y encadenar seis a la vez seria un ruido.
        public void Dispara(Efecto cual)
        {
            if (Volumen <= 0f) return;
            float[] m = Efectos.Muestra(cual, fs);
            if (m == null || m.Length == 0) return;
            lock (candado) { voz = m; vozPos = 0; }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int n = src.Read(buffer, offset, count);
            if (n <= 0) return n;

            float[] m; int pos; float vol = Volumen;
            lock (candado) { m = voz; pos = vozPos; }

            // LA SALIDA RAPIDA, y es la que mas importa: sin efecto sonando y con la musica a su
            // volumen entero, la cancion se devuelve TAL CUAL, sin recorrerla siquiera. Es el 99% del
            // tiempo, y aqui no se le toca ni una muestra.
            if (vol <= 0f || (m == null && duck >= 1f)) return n;

            int cuadros = n / ch;
            for (int f = 0; f < cuadros; f++)
            {
                float meta = (m != null) ? DuckMeta : 1f;
                if (duck < meta) { duck += pasoDuck; if (duck > meta) duck = meta; }
                else if (duck > meta) { duck -= pasoDuck; if (duck < meta) duck = meta; }
                if (duck > 0.9999f) duck = 1f;   // que aterrice EXACTO en uno: si no, nunca se sale por arriba

                float ef = 0f;
                if (m != null)
                {
                    if (pos < m.Length) ef = m[pos++] * vol;
                    else { m = null; lock (candado) { if (voz != null && pos >= voz.Length) voz = null; } }
                }
                int b0 = offset + f * ch;
                for (int c = 0; c < ch; c++) buffer[b0 + c] = buffer[b0 + c] * duck + ef;
            }
            lock (candado) { if (voz != null) vozPos = pos; }
            return n;
        }
    }
}
