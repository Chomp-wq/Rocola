#if ROCOLA_GPU
// LA AURORA A LA CADENCIA DE LA PANTALLA (solo tarjeta grafica).
//
// POR QUE. La forma de la aurora -la altura de cada una de sus 8 bandas- se recalculaba en el latido de la pantalla
// (animTimer, 24 por segundo), y las medidas del sonido llegaban todavia mas espaciadas: con WaveOut, trozos de ~42 ms
// (un cuarto de cada bufer). La tarjeta dibuja 60 cuadros por segundo, pero la silueta solo cambiaba en uno de cada dos
// o tres: el ondeo y el titileo iban lisos y la forma avanzaba a saltos por debajo.
//
// LO QUE SE HACE.
//  1) El hilo de audio mide ademas el espectro cada 1/60 s con una ventana DESLIZANTE de 50 ms (los tres ultimos
//     trozos). Se mira tres veces mas a menudo sin acortar la ventana: cada valor tiene el mismo ruido que los de
//     siempre y el golpe de cada banda salta igual. Cada medida lleva la hora (reloj fino) en que empieza a sonar su
//     ventana, igual que los trozos de siempre llevan la de su principio.
//  2) En CADA cuadro, antes de pedir la aurora, la tarjeta avanza las bandas hasta la hora de ese cuadro: entra cada
//     medida que ya suena en su momento y entre medias deja correr la caida. Las constantes son las de siempre pasadas
//     a tiempo: lo que un latido de 42 ms multiplicaba por 0,78 ahora es 0,78^(dt/42 ms) para cualquier dt. Con dt de
//     un latido justo la cuenta es la de antes, bit a bit (AvanzaAurora sigue siendo exactamente eso).
//  3) Si la tarjeta deja de pedir cuadros (ventana oculta, pantallas sin panel), a los 250 ms el latido vuelve a
//     llevar la aurora como antes, para que la presencia y la respiracion sigan su curso.
//
// Es una optimizacion que conserva el aspecto: sin interruptor (AGENTS.md). El dibujo por procesador no se toca: sin
// tarjeta no hay aurora y nada de esto se compila. Coste medido en pruebas/aurorafina.cs.
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Rocola
{
    public partial class Form1
    {
        private const double AurLatidoMs = 42.0;      // el latido para el que se ajustaron las constantes (animTimer)
        private const int AurFinTrozos = 3;           // trozos de 1/60 s en la ventana: 50 ms, como los de siempre
        private const int AurFinTope = 240;           // medidas en espera como mucho (~4 s; con 850 ms de colchon hay ~65)
        private const double AurFinSueltaMs = 250.0;  // sin cuadros de la tarjeta este rato, vuelve a mandar el latido
        private struct LvlFino { public double At; public float[] Esp; public LvlFino(double at, float[] esp) { At = at; Esp = esp; } }
        private Queue<LvlFino> aurFinQ;               // bajo levelLock
        private Stack<float[]> aurFinLibres;          // bajo levelLock: espectros para reusar (nada de basura por medida)
        // Hilo de audio: lo que va del trozo en curso y el anillo con los tres ultimos.
        private double[] aurFinAcc, aurFinAnillo; private int[] aurFinCuenta;
        private int aurFinN, aurFinLen, aurFinRate, aurFinSlot;
        [ThreadStatic] private static double[] tmpFinAt; [ThreadStatic] private static float[] tmpFinEsp; [ThreadStatic] private static int tmpFinCnt;
        // Pantalla: hasta donde van las bandas y cuando pidio la tarjeta su ultimo cuadro.
        private double aurFinT, aurFinUlt; private bool aurFinOk;

        // EL RELOJ FINO. Environment.TickCount salta de 15,6 en 15,6 ms: con medidas cada 16,7 ms, unos cuadros se
        // llevarian dos y otros ninguna. Solo las pruebas lo cambian.
        internal static Func<double> RelojAuroraPrueba = null;
        private static readonly long aurReloj0 = Stopwatch.GetTimestamp();
        private static double AuroraMs()
        {
            if (RelojAuroraPrueba != null) return RelojAuroraPrueba();
            return (Stopwatch.GetTimestamp() - aurReloj0) * 1000.0 / Stopwatch.Frequency;
        }

        // ---------- HILO DE AUDIO ----------

        // Al empezar un bloque con el espectro encendido: el acumulador del trozo en curso y su largo (1/60 s).
        private double[] AuroraFinaEmpieza(int rate, out int finN, out int finLen)
        {
            if (aurFinAcc == null || aurFinRate != rate)
            {
                aurFinRate = rate; aurFinLen = Math.Max(1, (rate + 30) / 60);
                aurFinAcc = new double[SpecBands]; aurFinAnillo = new double[AurFinTrozos * SpecBands];
                aurFinCuenta = new int[AurFinTrozos]; aurFinN = 0; aurFinSlot = 0;
            }
            tmpFinCnt = 0;
            finN = aurFinN; finLen = aurFinLen;
            return aurFinAcc;
        }

        // Cierra un trozo de 'n' cuadros que acaba en la muestra 'finMuestra' del bloque: lo guarda en el anillo y
        // apunta la ventana de los tres ultimos, con la hora (desde el principio del bloque) en que empieza a sonar. La
        // hora va con el largo nominal de la ventana, tambien en los dos primeros trozos de la cancion: asi las medidas
        // salen siempre separadas 1/60 s.
        private void AuroraFinaCierra(int n, int finMuestra, int samplesPerSec)
        {
            int s = aurFinSlot * SpecBands;
            for (int b = 0; b < SpecBands; b++) { aurFinAnillo[s + b] = aurFinAcc[b]; aurFinAcc[b] = 0.0; }
            aurFinCuenta[aurFinSlot] = n;
            aurFinSlot = (aurFinSlot + 1) % AurFinTrozos;
            int tot = 0;
            for (int k = 0; k < AurFinTrozos; k++) tot += aurFinCuenta[k];
            if (tot <= 0) return;
            int i = tmpFinCnt;
            if (tmpFinAt == null || tmpFinAt.Length <= i)
            {
                int cap = Math.Max(16, i * 2);
                double[] na = new double[cap]; float[] ne = new float[cap * SpecBands];
                if (tmpFinAt != null) { Array.Copy(tmpFinAt, na, i); Array.Copy(tmpFinEsp, ne, i * SpecBands); }
                tmpFinAt = na; tmpFinEsp = ne;
            }
            tmpFinAt[i] = finMuestra * 1000.0 / Math.Max(1, samplesPerSec) - AurFinTrozos * aurFinLen * 1000.0 / Math.Max(1, aurFinRate);
            for (int b = 0; b < SpecBands; b++)
            {
                double e = 0.0;
                for (int k = 0; k < AurFinTrozos; k++) e += aurFinAnillo[k * SpecBands + b];
                tmpFinEsp[i * SpecBands + b] = (float)Math.Sqrt(e / tot);
            }
            tmpFinCnt = i + 1;
        }

        // Al acabar el bloque, bajo levelLock: las medidas del bloque a la cola, con la hora del reloj fino.
        private void AuroraFinaEncola(double ahora, int finN)
        {
            aurFinN = finN;
            int n = tmpFinCnt; tmpFinCnt = 0;
            if (n == 0) return;
            if (aurFinQ == null) { aurFinQ = new Queue<LvlFino>(); aurFinLibres = new Stack<float[]>(); }
            for (int i = 0; i < n; i++)
            {
                while (aurFinQ.Count >= AurFinTope) AuroraFinaDevuelve(aurFinQ.Dequeue().Esp);
                float[] e = (aurFinLibres.Count > 0) ? aurFinLibres.Pop() : new float[SpecBands];
                Array.Copy(tmpFinEsp, i * SpecBands, e, 0, SpecBands);
                aurFinQ.Enqueue(new LvlFino(ahora + tmpFinAt[i], e));
            }
        }
        private void AuroraFinaDevuelve(float[] e) { if (e != null && aurFinLibres.Count < AurFinTope) aurFinLibres.Push(e); }
        // Bajo levelLock (ClearLevels): sin cancion no quedan medidas pendientes.
        private void AuroraFinaVacia() { if (aurFinQ != null) while (aurFinQ.Count > 0) AuroraFinaDevuelve(aurFinQ.Dequeue().Esp); }

        // ---------- PANTALLA ----------

        // La tarjeta esta llevando la aurora? Si no pide cuadros, la lleva el latido (AnimTimer_Tick), como antes.
        private bool AuroraFinaViva() { return aurFinOk && AuroraMs() - aurFinUlt < AurFinSueltaMs; }

        // La llama la tarjeta en CADA cuadro, justo antes de pedir la aurora (GetAurora): avanza las bandas y la
        // presencia hasta 'ahora', entrando cada medida en el momento en que empieza a sonar.
        internal void AvanzaAuroraFina(double ahora)
        {
            if (!aurFinOk || ahora - aurFinUlt >= AurFinSueltaMs || ahora < aurFinT) aurFinT = ahora;   // (re)empieza aqui
            aurFinOk = true; aurFinUlt = ahora;
            double dt = ahora - aurFinT;
            bool mudo = nowPlaying == null || videoOn || !EspectroActivo;
            int retraso = RetrasoAudible();
            if (mudo) Array.Clear(specNow, 0, SpecBands);
            lock (levelLock)
            {
                if (aurFinQ != null)
                    while (aurFinQ.Count > 0 && aurFinQ.Peek().At + retraso <= ahora)
                    {
                        LvlFino e = aurFinQ.Dequeue();
                        double te = e.At + retraso;
                        if (te > aurFinT) { AvanzaBandas((float)((te - aurFinT) / AurLatidoMs)); aurFinT = te; }
                        if (!mudo)
                        {
                            Array.Copy(e.Esp, specNow, SpecBands);
                            AvanzaBandas(0f);   // el golpe y la subida, en el instante en que llega (no en el cuadro siguiente)
                        }
                        AuroraFinaDevuelve(e.Esp);
                    }
            }
            if (ahora > aurFinT) AvanzaBandas((float)((ahora - aurFinT) / AurLatidoMs));
            aurFinT = ahora;
            AvanzaPresencia((float)(dt / AurLatidoMs));
        }

        // Lo que pide la tarjeta en cada cuadro: la aurora ya avanzada hasta ese cuadro.
        private GpuAurora AuroraDelCuadro()
        {
            AvanzaAuroraFina(AuroraMs());
            return BuildAuroraState();
        }

        // Un coeficiente "por latido" pasado a 'pasos' latidos (fracciones incluidas). Con un latido justo, el de
        // siempre sin tocar un bit.
        private static float AurPorPasos(float k, float pasos) { return (pasos == 1f) ? k : (float)(1.0 - Math.Pow(1.0 - k, pasos)); }
        private static float AurQueda(float k, float pasos) { return (pasos == 1f) ? k : (float)Math.Pow(k, pasos); }
    }
}
#endif
