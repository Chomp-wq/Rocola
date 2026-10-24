// EL LOCAL Y SUS BOCINAS (12 de octubre de 2026): tres ajustes para el billar y una prueba de bocinas.
//
// El billar tiene techo de lamina, paredes de ladrillo y piso de loseta: mucho rebote. Y ruido de gente y de bolas.
// Con las fotos del sistema de sonido (SONIDO_BOCINAS_MODEST_2026-10-11.md) se propusieron, y el usuario eligio:
//
//  - CLARIDAD PARA EL LOCAL. En una sala que rebota, el grave medio (200-400 Hz) se acumula y tapa la voz: suena a
//    "retumbe". Se baja 2,5 dB alrededor de 300 Hz y se sube 1,5 dB la presencia de la voz, alrededor de 3 kHz. Dos
//    filtros de campana (los de siempre de un ecualizador), en coma doble. El bajo de verdad (50-120 Hz) no se toca.
//  - QUE LAS PARTES SUAVES SE OIGAN. Con el ruido del local, una intro bajita o un pasaje suave se pierden aunque la
//    cancion este bien nivelada. Se sube lo que va por debajo del nivel de siempre, como mucho 4 dB, despacio, y en
//    cuanto vuelve lo fuerte se suelta deprisa. Lo fuerte no se toca. El nivel se mide SIN el volumen general ni el
//    del modo noche (se divide por el Master de la cadena), asi que bajar el volumen de madrugada no hace que suba
//    mas. El silencio y lo casi mudo no se suben (no se levanta el soplido de la grabacion).
//  - EL BALANCE. Si una zona suena mas fuerte porque las perillas del amplificador (IN A, IN B) no estan iguales, se
//    puede bajar el canal que sobra hasta 6 dB. Solo BAJA: nunca sube un canal, asi no puede recortar. Lo mejor sigue
//    siendo igualarlo en el amplificador, y para eso esta la prueba de bocinas.
//
// Los tres llevan su mando en Ctrl+F3 -> Sonido -> "El local y sus bocinas", apagados de fabrica (en otro sitio no
// hacen falta), se oyen al instante en la que suena y en la siguiente ya preparada, y apagados la etapa sale BIT A BIT
// como antes: ni toca la muestra. Al encender o apagar, la claridad entra y sale en 30 ms, la subida se suelta sola y
// el balance va en 50 ms: sin chasquidos. Las pistas seguidas del mismo disco heredan los filtros y la medida (Sigue),
// igual que el grave en mono. Lo que se queda en casi nada se pone a cero: sin numeros desnormales.
//
// GRAVES DEL LOCAL Y AGUDOS DEL LOCAL (22 de octubre de 2026). Dos mandos de -6 a +6 dB, de 1 en 1, para afinar el tono al
// local sin tocar el EQ automatico: los graves son un escalon de bajos (estante) alrededor de 120 Hz y los agudos uno de
// altos alrededor de 6 kHz (los dos, filtros de biquad de las notas de Robert Bristow-Johnson, en coma doble). Estan en
// 0 dB de fabrica y a 0 la etapa no los toca ni los sigue: la muestra sale bit a bit como antes. Al moverlos el valor
// sube o baja en una rampa de 50 ms para los 12 dB enteros (un escalon de 1 dB tarda 4 ms), con los coeficientes
// recalculados en cada cuadro: sin chasquidos. Van detras de la claridad y delante de las partes suaves, y las pistas
// seguidas del mismo disco heredan su estado (Sigue) como el resto. Estan en Ctrl+F3 -> Sonido -> El local y sus bocinas
// -> Tono y presencia. «Escuchar sin mejoras» los pone a 0 mientras dura.
//
// Va detras del grave en mono y delante del bajo psicoacustico y del limitador, que es quien cuida los picos.
//
// LA PRUEBA DE BOCINAS: ruido rosa suave (el reparto de energia de la musica) solo por la izquierda, solo por la
// derecha o por las dos; y una prueba de polaridad: ruido grave igual por los dos canales y luego con uno al reves.
// Desde un punto entre dos bocinas, si "al reves" suena con MAS grave que "igual", una bocina esta conectada al reves
// (rojo con negro). Sale por su propia salida, sin pasar por la cadena, solo con nada sonando, y se para al cerrar
// la ventana.
using System;
using System.Windows.Forms;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        // Los mandos de Ctrl+F3 -> Sonido -> "El local y sus bocinas" (apagados de fabrica).
        private static bool claridadLocal, partesSuaves;
        private static int balanceDb;   // -6..6: negativo baja la derecha (el sonido se va a la izquierda), positivo la izquierda
        // Graves y agudos del local (22-10-2026): dB de -6 a 6 ("graves_local=", "agudos_local="; 0 de fabrica).
        private static int gravesLocalDb, agudosLocalDb;

        internal static int TonoLocalDb(string texto, int actual)
        {
            int v;
            if (!int.TryParse(texto.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out v)) return actual;
            return Math.Max(-SalaLocal.TonoMax, Math.Min(SalaLocal.TonoMax, v));
        }
        internal static string TextoTono(int db)
        {
            return db == 0 ? "0 dB" : (db > 0 ? "+" + db + " dB" : "−" + (-db) + " dB");
        }

        internal static int BalanceDb(string texto)
        {
            int v;
            if (!int.TryParse(texto.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out v)) return balanceDb;
            return Math.Max(-SalaLocal.BalanceMax, Math.Min(SalaLocal.BalanceMax, v));
        }
        internal static string TextoBalance(int db)
        {
            if (db == 0) return "Centrado";
            return db < 0 ? ("Derecha −" + (-db) + " dB") : ("Izquierda −" + db + " dB");
        }

        private sealed class SalaLocal : ISampleProvider
        {
            internal const int BalanceMax = 6;
            internal const double ClaridadHz = 300.0, ClaridadDb = -2.5, ClaridadQ = 0.9;
            internal const double PresenciaHz = 3000.0, PresenciaDb = 1.5, PresenciaQ = 1.0;
            internal const double SuaveMaxDb = 4.0, SuaveBajoObjetivoDb = 12.0, SuaveRazon = 0.5;
            internal const double SuavePuertaDb = -58.0, SuavePuertaAnchoDb = 8.0;
            internal const double MedidaSeg = 0.4, SubeSeg = 1.5, BajaSeg = 0.12, MezclaSeg = 0.03, BalanceSeg = 0.05;
            internal const int Trozo = 32;   // cuadros por cada nueva cuenta de la subida (el logaritmo, no en cada muestra)
            internal const int TonoMax = 6;                       // graves y agudos del local: de -6 a +6 dB
            internal const double GravesHz = 120.0, AgudosHz = 6000.0, TonoQ = 0.7071, TonoSeg = 0.05;   // TonoSeg: los 12 dB enteros

            private readonly ISampleProvider src;
            private readonly LevelTap tap;           // de donde se lee el volumen general (Master) para no medirlo
            private readonly int tasa, canales;
            private readonly bool vale;              // 1 o 2 canales a 8 kHz o mas: si no, pasa tal cual
            private readonly double umbralDb;        // por debajo de esto se sube (dB de pico completo, sin el Master)
            // Los mandos: los cambia la ventana de ajustes en cualquier momento; se leen al empezar cada lectura.
            private volatile bool claridadOn, suavesOn;
            private volatile int balance;
            // Solo el hilo de audio actualiza esta cache; la ventana publica un entero.
            private int balanceCalculado;
            private double balanceL, balanceR;
            // Claridad: dos campanas por canal. Coeficientes normalizados (a0 = 1) y estados x1 x2 y1 y2 por canal y campana.
            private readonly bool conPresencia;
            private readonly double cb0, cb1, cb2, ca1, ca2, pb0, pb1, pb2, pa1, pa2;
            private readonly double[] eq = new double[16];
            private double mezcla;                   // 0 = sin claridad, 1 = con ella (rampa de 30 ms)
            private readonly double pasoMezcla;
            // Partes suaves: la medida (media de los cuadrados, ~0,4 s) y la subida (lineal, 1 = nada).
            private double medida, sube = 1.0, meta = 1.0;
            private readonly double aMedida, aSube, aBaja;
            private int cuenta;
            // Balance: lo que lleva cada canal ahora (rampa de 50 ms hacia lo que pide el mando).
            private double gl = 1.0, gr = 1.0;
            private readonly double pasoBalance;
            // Graves y agudos del local: lo pedido (dB, volatile), lo que llevan ahora (rampa), sus coeficientes y su memoria
            // (x1 x2 y1 y2 por canal: de 0 a 7 los graves, de 8 a 15 los agudos). 'primado' = su memoria sigue la musica.
            private volatile int gravesObj, agudosObj;
            private double gravesDb, agudosDb;
            private readonly bool conAgudos;
            private readonly double pasoTono, tSenoG, tCosG, tSenoA, tCosA;
            private double gb0, gb1, gb2, ga1, ga2, hb0, hb1, hb2, ha1, ha2;
            private readonly double[] sh = new double[16];
            private bool gravesPrimado, agudosPrimado, recalculaTono;
            private volatile SalaLocal anterior;     // la pista que acaba justo antes, sin fundido (disco continuo)
            private bool empezo;
            // El cebado de delante (lo pone ArmaCadena; GraveMono.cs): tras heredar de la anterior del mismo disco pasa tal cual.
            internal int Cebado;
            private int cebadoPorPasar;

            public SalaLocal(ISampleProvider s, LevelTap volumen, float objetivo, bool conClaridad, bool conSuaves, int balanceDb)
                : this(s, volumen, objetivo, conClaridad, conSuaves, balanceDb, 0, 0) { }

            public SalaLocal(ISampleProvider s, LevelTap volumen, float objetivo, bool conClaridad, bool conSuaves, int balanceDb, int graves, int agudos)
            {
                src = s; tap = volumen;
                tasa = s.WaveFormat.SampleRate; canales = s.WaveFormat.Channels;
                vale = (canales == 1 || canales == 2) && tasa >= 8000;
                claridadOn = conClaridad; suavesOn = conSuaves;
                balance = Math.Max(-BalanceMax, Math.Min(BalanceMax, balanceDb));
                double obj = (objetivo > 0.001f && objetivo <= 1f) ? objetivo : 0.26;
                umbralDb = 20.0 * Math.Log10(obj) - SuaveBajoObjetivoDb;
                if (!vale) return;
                Campana(ClaridadHz, ClaridadDb, ClaridadQ, out cb0, out cb1, out cb2, out ca1, out ca2);
                conPresencia = PresenciaHz < 0.45 * tasa;
                if (conPresencia) Campana(PresenciaHz, PresenciaDb, PresenciaQ, out pb0, out pb1, out pb2, out pa1, out pa2);
                pasoMezcla = 1.0 / (MezclaSeg * tasa);
                pasoBalance = 1.0 / (BalanceSeg * tasa);
                conAgudos = AgudosHz < 0.45 * tasa;
                pasoTono = 2.0 * TonoMax / (TonoSeg * tasa);   // dB por cuadro
                double w = 2.0 * Math.PI * GravesHz / tasa; tSenoG = Math.Sin(w); tCosG = Math.Cos(w);
                w = 2.0 * Math.PI * AgudosHz / tasa; tSenoA = Math.Sin(w); tCosA = Math.Cos(w);
                gravesObj = Math.Max(-TonoMax, Math.Min(TonoMax, graves));
                agudosObj = conAgudos ? Math.Max(-TonoMax, Math.Min(TonoMax, agudos)) : 0;
                gravesDb = gravesObj; agudosDb = agudosObj;   // arrancan donde piden los mandos, sin rampa
                recalculaTono = true;
                aMedida = 1.0 - Math.Exp(-1.0 / (MedidaSeg * tasa));
                aSube = 1.0 - Math.Exp(-1.0 / (SubeSeg * tasa));
                aBaja = 1.0 - Math.Exp(-1.0 / (BajaSeg * tasa));
                // Arrancan donde piden los mandos, sin rampa: lo de la primera muestra ya es lo que se pidio.
                mezcla = conClaridad ? 1.0 : 0.0;
                balanceCalculado = canales == 2 ? balance : 0;
                GananciasBalance(balanceCalculado, out balanceL, out balanceR);
                gl = balanceL; gr = balanceR;
                medida = Math.Pow(10.0, umbralDb / 10.0);   // empieza "en el nivel de siempre": nada que subir al arrancar
            }

            // Una campana de ecualizador (la de siempre, de las notas de Robert Bristow-Johnson), normalizada.
            private void Campana(double hz, double db, double q, out double b0, out double b1, out double b2, out double a1, out double a2)
            {
                double A = Math.Pow(10.0, db / 40.0), w0 = 2.0 * Math.PI * hz / tasa, al = Math.Sin(w0) / (2.0 * q), c = Math.Cos(w0);
                double a0 = 1.0 + al / A;
                b0 = (1.0 + al * A) / a0; b1 = -2.0 * c / a0; b2 = (1.0 - al * A) / a0; a1 = -2.0 * c / a0; a2 = (1.0 - al / A) / a0;
            }
            internal static void GananciasBalance(int db, out double l, out double r)
            {
                l = 1.0; r = 1.0;
                if (db > 0) l = Math.Pow(10.0, -db / 20.0);       // a la derecha: baja la izquierda
                else if (db < 0) r = Math.Pow(10.0, db / 20.0);   // a la izquierda: baja la derecha
            }

            public WaveFormat WaveFormat { get { return src.WaveFormat; } }
            internal bool ConClaridad { get { return claridadOn; } }
            internal int Graves { get { return gravesObj; } }
            internal int Agudos { get { return agudosObj; } }
            internal double GravesAhora { get { return gravesDb; } }
            internal double AgudosAhora { get { return agudosDb; } }
            internal bool ConSuaves { get { return suavesOn; } }
            internal int Balance { get { return balance; } }
            internal double Subida { get { return sube; } }   // lo que sube ahora (1 = nada); para las pruebas

            // Los mandos, en vivo (desde la ventana). Al encender la claridad sus filtros empiezan de cero: la rampa
            // de entrada tapa ese arranque.
            internal void Ajusta(bool conClaridad, bool conSuaves, int balanceDb)
            {
                if (!vale) return;
                claridadOn = conClaridad; suavesOn = conSuaves;
                balance = Math.Max(-BalanceMax, Math.Min(BalanceMax, balanceDb));
            }

            // Graves y agudos del local, en vivo (desde la ventana): de -6 a 6 dB; la rampa de 50 ms hace el resto.
            internal void AjustaTono(int graves, int agudos)
            {
                if (!vale) return;
                gravesObj = Math.Max(-TonoMax, Math.Min(TonoMax, graves));
                agudosObj = conAgudos ? Math.Max(-TonoMax, Math.Min(TonoMax, agudos)) : 0;
            }

            internal void Sigue(SalaLocal previa) { if (previa != null && !object.ReferenceEquals(previa, this)) anterior = previa; }
            private void Hereda()
            {
                SalaLocal a = anterior; anterior = null;
                if (a == null || !a.vale || a.tasa != tasa || a.canales != canales) return;
                Array.Copy(a.eq, eq, eq.Length);
                mezcla = a.mezcla; medida = a.medida; sube = a.sube; meta = a.meta; cuenta = a.cuenta; gl = a.gl; gr = a.gr;
                Array.Copy(a.sh, sh, sh.Length);
                gravesDb = a.gravesDb; agudosDb = a.agudosDb; gravesPrimado = a.gravesPrimado; agudosPrimado = a.agudosPrimado;
                recalculaTono = true;
                cebadoPorPasar = Math.Max(0, Cebado) * canales;
            }

            // Nada que hacer: todo apagado, el balance centrado y las rampas ya llegaron. Entonces la etapa no toca ni una
            // muestra. (Con el balance movido NO esta quieta aunque su rampa haya llegado: hay que seguir bajando el canal.)
            private bool Quieta(double tl, double tr)
            {
                return !claridadOn && mezcla == 0.0 && !suavesOn && sube == 1.0
                    && tl == 1.0 && tr == 1.0 && gl == 1.0 && gr == 1.0
                    && gravesDb == 0.0 && agudosDb == 0.0 && gravesObj == 0 && agudosObj == 0;
            }

            // CUADROS ENTEROS, como el grave en mono: en estereo se pide siempre un numero par. Si piden una sola muestra,
            // se hace el cuadro entero aparte y su otra mitad sale en la lectura siguiente (los canales nunca se cruzan).
            private readonly float[] sale = new float[2];
            private int saleN;

            public int Read(float[] buffer, int offset, int count)
            {
                if (!vale || canales != 2) return Lee(buffer, offset, count);
                int hechos = 0;
                if (saleN > 0 && count > 0) { buffer[offset] = sale[1]; saleN = 0; hechos = 1; }
                int pares = (count - hechos) & ~1;
                if (pares > 0)
                {
                    int n = Lee(buffer, offset + hechos, pares);
                    if (n <= 0) return hechos;
                    hechos += n;
                    if ((n & 1) == 1) return hechos;   // el origen acabo a medio cuadro: lo que queda sale tal cual
                }
                if (count - hechos == 1 && Lee(sale, 0, 2) == 2) { buffer[offset + hechos++] = sale[0]; saleN = 1; }
                return hechos;
            }

            private int Lee(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                if (!vale || n <= 0) return n;
                if (!empezo) { empezo = true; Hereda(); }
                int o = offset, m = n;
                if (cebadoPorPasar > 0)
                {   // el cebado de la siguiente del mismo disco: sin tocar los filtros heredados (el relevo lo tira)
                    int salta = Math.Min(n, cebadoPorPasar); cebadoPorPasar -= salta; o += salta; m -= salta;
                    if (m == 0) return n;
                }
                int pedido = canales == 2 ? balance : 0;
                if (pedido != balanceCalculado)
                {
                    GananciasBalance(pedido, out balanceL, out balanceR);
                    balanceCalculado = pedido;
                }
                double tl = balanceL, tr = balanceR;
                if (Quieta(tl, tr)) return n;
                if (claridadOn && mezcla == 0.0) Array.Clear(eq, 0, eq.Length);   // entra de nuevo: filtros desde cero
                Proceso(buffer, o, m / canales, tl, tr);
                return n;
            }

            private void Proceso(float[] b, int o, int cuadros, double tl, double tr)
            {
                bool cla = claridadOn, sua = suavesOn, estereo = canales == 2;
                double master = 1.0;
                try { float m = (tap != null) ? tap.Master : 1f; if (m > 1e-4f && !float.IsNaN(m) && !float.IsInfinity(m)) master = m; } catch { }
                double master2 = master * master;
                double mz = mezcla, pm = pasoMezcla, s = sube, mt = meta, me = medida, l = gl, r = gr, pb = pasoBalance;
                double cb0 = this.cb0, cb1 = this.cb1, cb2 = this.cb2, ca1 = this.ca1, ca2 = this.ca2;
                double pb0 = this.pb0, pb1 = this.pb1, pb2 = this.pb2, pa1 = this.pa1, pa2 = this.pa2;
                bool pres = conPresencia;
                int cnt = cuenta;
                double tg = gravesObj, ta = agudosObj, gd = gravesDb, ad = agudosDb, ps = pasoTono;
                bool tono = gd != 0.0 || ad != 0.0 || tg != 0.0 || ta != 0.0;
                double gb0 = this.gb0, gb1 = this.gb1, gb2 = this.gb2, ga1 = this.ga1, ga2 = this.ga2;
                double hb0 = this.hb0, hb1 = this.hb1, hb2 = this.hb2, ha1 = this.ha1, ha2 = this.ha2;
                if (recalculaTono)
                {
                    recalculaTono = false;
                    if (gd != 0.0) Estante(false, gd, tSenoG, tCosG, out gb0, out gb1, out gb2, out ga1, out ga2);
                    if (ad != 0.0) Estante(true, ad, tSenoA, tCosA, out hb0, out hb1, out hb2, out ha1, out ha2);
                }
                bool gp = gravesPrimado, ap = agudosPrimado;
                for (int f = 0, i = o; f < cuadros; f++)
                {
                    double x0 = b[i], x1 = estereo ? b[i + 1] : 0.0;
                    // 1) La claridad (con su rampa de entrada y salida).
                    if (cla) { if (mz < 1.0) mz = Math.Min(1.0, mz + pm); }
                    else if (mz > 0.0) mz = Math.Max(0.0, mz - pm);
                    if (mz > 0.0)
                    {
                        double y0 = Filtra(x0, 0, cb0, cb1, cb2, ca1, ca2);
                        if (pres) y0 = Filtra(y0, 4, pb0, pb1, pb2, pa1, pa2);
                        x0 += mz * (y0 - x0);
                        if (estereo)
                        {
                            double y1 = Filtra(x1, 8, cb0, cb1, cb2, ca1, ca2);
                            if (pres) y1 = Filtra(y1, 12, pb0, pb1, pb2, pa1, pa2);
                            x1 += mz * (y1 - x1);
                        }
                    }
                    // 1b) Graves y agudos del local: la rampa de dB y, mientras no sea 0, su estante.
                    if (tono)
                    {
                        if (gd != tg)
                        {
                            gd = gd < tg ? Math.Min(tg, gd + ps) : Math.Max(tg, gd - ps);
                            if (gd != 0.0) Estante(false, gd, tSenoG, tCosG, out gb0, out gb1, out gb2, out ga1, out ga2);
                            else { Array.Clear(sh, 0, 8); gp = false; }
                        }
                        if (ad != ta)
                        {
                            ad = ad < ta ? Math.Min(ta, ad + ps) : Math.Max(ta, ad - ps);
                            if (ad != 0.0) Estante(true, ad, tSenoA, tCosA, out hb0, out hb1, out hb2, out ha1, out ha2);
                            else { Array.Clear(sh, 8, 8); ap = false; }
                        }
                        if (gd != 0.0)
                        {
                            if (!gp) { sh[0] = sh[1] = sh[2] = sh[3] = x0; if (estereo) sh[4] = sh[5] = sh[6] = sh[7] = x1; gp = true; }
                            x0 = FiltraTono(x0, 0, gb0, gb1, gb2, ga1, ga2);
                            if (estereo) x1 = FiltraTono(x1, 4, gb0, gb1, gb2, ga1, ga2);
                        }
                        if (ad != 0.0)
                        {
                            if (!ap) { sh[8] = sh[9] = sh[10] = sh[11] = x0; if (estereo) sh[12] = sh[13] = sh[14] = sh[15] = x1; ap = true; }
                            x0 = FiltraTono(x0, 8, hb0, hb1, hb2, ha1, ha2);
                            if (estereo) x1 = FiltraTono(x1, 12, hb0, hb1, hb2, ha1, ha2);
                        }
                    }
                    // 2) Las partes suaves: se mide lo que llega (sin el Master) y se va hacia la subida que toca.
                    if (sua || s != 1.0)
                    {
                        double cuad = estereo ? 0.5 * (x0 * x0 + x1 * x1) : x0 * x0;
                        me += (cuad / master2 - me) * aMedida;
                        if (me < 1e-20) me = 0.0;
                        if (++cnt >= Trozo) { cnt = 0; mt = sua ? MetaSubida(me) : 1.0; }
                        if (!sua) mt = 1.0;
                        s += (mt - s) * (mt > s ? aSube : aBaja);
                        if (s - 1.0 < 1e-6 && s - 1.0 > -1e-6 && mt == 1.0) s = 1.0;
                        x0 *= s; x1 *= s;
                    }
                    // 3) El balance (solo baja; rampa lineal hacia lo que pide el mando).
                    if (estereo)
                    {
                        if (l != tl) l = l < tl ? Math.Min(tl, l + pb) : Math.Max(tl, l - pb);
                        if (r != tr) r = r < tr ? Math.Min(tr, r + pb) : Math.Max(tr, r - pb);
                        b[i] = (float)(x0 * l); b[i + 1] = (float)(x1 * r);
                        i += 2;
                    }
                    else { b[i] = (float)x0; i += 1; }
                }
                mezcla = mz; sube = s; meta = mt; medida = me; gl = l; gr = r; cuenta = cnt;
                gravesDb = gd; agudosDb = ad; gravesPrimado = gp; agudosPrimado = ap;
                this.gb0 = gb0; this.gb1 = gb1; this.gb2 = gb2; this.ga1 = ga1; this.ga2 = ga2;
                this.hb0 = hb0; this.hb1 = hb1; this.hb2 = hb2; this.ha1 = ha1; this.ha2 = ha2;
                if (mz == 0.0 && !cla) Array.Clear(eq, 0, eq.Length);   // ya salio del todo: sin estados viejos ni desnormales
            }

            // Un estante de ecualizador (RBJ, con Q): graves (alto = false) o agudos (alto = true), normalizado (a0 = 1).
            internal static void Estante(bool alto, double db, double seno, double coseno, out double b0, out double b1, out double b2, out double a1, out double a2)
            {
                double A = Math.Pow(10.0, db / 40.0), k = 2.0 * Math.Sqrt(A) * (seno / (2.0 * TonoQ));
                double Am = A - 1.0, Ap = A + 1.0, a0;
                if (!alto)
                {
                    b0 = A * (Ap - Am * coseno + k); b1 = 2.0 * A * (Am - Ap * coseno); b2 = A * (Ap - Am * coseno - k);
                    a0 = Ap + Am * coseno + k; a1 = -2.0 * (Am + Ap * coseno); a2 = Ap + Am * coseno - k;
                }
                else
                {
                    b0 = A * (Ap + Am * coseno + k); b1 = -2.0 * A * (Am + Ap * coseno); b2 = A * (Ap + Am * coseno - k);
                    a0 = Ap - Am * coseno + k; a1 = 2.0 * (Am - Ap * coseno); a2 = Ap - Am * coseno - k;
                }
                b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
            }
            private double FiltraTono(double x, int e, double b0, double b1, double b2, double a1, double a2)
            {
                double y = b0 * x + b1 * sh[e] + b2 * sh[e + 1] - a1 * sh[e + 2] - a2 * sh[e + 3];
                if (y < 1e-30 && y > -1e-30) y = 0.0;
                sh[e + 1] = sh[e]; sh[e] = x; sh[e + 3] = sh[e + 2]; sh[e + 2] = y;
                return y;
            }

            private double Filtra(double x, int e, double b0, double b1, double b2, double a1, double a2)
            {
                double y = b0 * x + b1 * eq[e] + b2 * eq[e + 1] - a1 * eq[e + 2] - a2 * eq[e + 3];
                if (y < 1e-30 && y > -1e-30) y = 0.0;
                eq[e + 1] = eq[e]; eq[e] = x; eq[e + 3] = eq[e + 2]; eq[e + 2] = y;
                return y;
            }

            // Cuanto subir con esta medida (media de los cuadrados, sin el Master): nada por encima del umbral; por debajo,
            // la mitad de lo que falta, hasta 4 dB. Cerca del silencio se va apagando y en el silencio no sube nada.
            internal double MetaSubida(double mediaCuadrados)
            {
                if (!(mediaCuadrados > 0.0)) return 1.0;
                double db = 10.0 * Math.Log10(mediaCuadrados);
                if (db >= umbralDb || db <= SuavePuertaDb) return 1.0;
                double sub = Math.Min(SuaveMaxDb, (umbralDb - db) * SuaveRazon);
                double puerta = (db - SuavePuertaDb) / SuavePuertaAnchoDb;   // 0 en la puerta, 1 a 8 dB por encima
                if (puerta < 1.0) sub *= puerta;
                return Math.Pow(10.0, sub / 20.0);
            }
            internal double UmbralDb { get { return umbralDb; } }
        }

        // ---------------------------------------------------------------- LA PRUEBA DE BOCINAS
        internal enum PruebaModo { Izquierda, Derecha, Ambas, Polaridad }

        private IWavePlayer pruebaSalida;      // su propia salida: la de la musica ni se toca
        private PruebaBocinas pruebaSonando;

        internal static string NombrePrueba(PruebaModo m)
        {
            return m == PruebaModo.Izquierda ? "solo la izquierda" : m == PruebaModo.Derecha ? "solo la derecha"
                 : m == PruebaModo.Ambas ? "las dos" : "la polaridad";
        }

        // Suena la prueba (y para la que sonara). Devuelve lo que hay que decir en la ventana.
        internal string ProbarBocinas(PruebaModo m)
        {
            if (nowPlaying != null || videoOn) return "Hay musica sonando: la prueba es con la rocola callada.";
            ParaPruebaBocinas();
            PruebaBocinas p = new PruebaBocinas(m);
            IWavePlayer w = null;
            try
            {
                w = CreaWaveOut();
                w.Init(new NAudio.Wave.SampleProviders.SampleToWaveProvider16(p));   // 16 bits: lo admite cualquier equipo (a un ruido no le hace falta dither)
                pruebaSalida = w; pruebaSonando = p;
                w.PlaybackStopped += PruebaAcabo;
                w.Play();
                return "Sonando: " + NombrePrueba(m) + ".";
            }
            catch (Exception ex)
            {
                if (w != null) { try { w.PlaybackStopped -= PruebaAcabo; } catch { } try { w.Dispose(); } catch { } }
                pruebaSalida = null; pruebaSonando = null;
                LogError("Prueba de bocinas", ex);
                return "No se pudo abrir la salida de sonido: " + MotivoFallo(ex) + ".";
            }
        }

        // Termino sola: se suelta la salida despues, fuera del aviso de la propia salida.
        private void PruebaAcabo(object sender, StoppedEventArgs e)
        {
            IWavePlayer w = sender as IWavePlayer;
            if (w == null) return;
            MethodInvoker suelta = delegate
            {
                if (object.ReferenceEquals(pruebaSalida, w)) { pruebaSalida = null; pruebaSonando = null; }
                try { w.PlaybackStopped -= PruebaAcabo; } catch { }
                try { w.Dispose(); } catch { }
            };
            try { if (IsHandleCreated && !IsDisposed) { BeginInvoke(suelta); return; } } catch { }
            suelta();
        }

        // Para la que suene (al cerrar la ventana de ajustes, o al pedir otra).
        internal void ParaPruebaBocinas()
        {
            IWavePlayer w = pruebaSalida;
            pruebaSalida = null; pruebaSonando = null;
            if (w == null) return;
            try { w.PlaybackStopped -= PruebaAcabo; } catch { }
            try { w.Stop(); } catch { }
            try { w.Dispose(); } catch { }
        }

        // Que esta sonando ahora, para la ventana ("" si nada).
        internal string EstadoPrueba()
        {
            PruebaBocinas p = pruebaSonando;
            if (p == null || pruebaSalida == null) return "";
            int retraso = latenciaAbiertaMs > 0 ? latenciaAbiertaMs : 1000;   // lo que guarda la salida (CreaWaveOut)
            if (p.Modo == PruebaModo.Polaridad) return p.AlReves(retraso) ? "Polaridad: AL REVES (una de las dos invertida)." : "Polaridad: IGUAL por las dos.";
            return "Sonando: " + NombrePrueba(p.Modo) + ".";
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
    }
}
