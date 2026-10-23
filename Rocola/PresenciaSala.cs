// PRESENCIA EN SALA RUIDOSA (22 de octubre de 2026): un compresor suave entre el local (SalaLocal.cs) y el bajo psicoacustico.
//
// En un billar con gente y bolas, lo que mas se pierde de una cancion no es lo fuerte sino lo que va por debajo: la voz que
// baja en la estrofa, la intro, el pasaje callado. "Que las partes suaves se oigan" sube lo bajito de golpe, hasta 4 dB, y
// solo cuando la cancion entera va baja; este compresor hace otra cosa, mas fina y continua: aprieta un poco lo que pasa del
// nivel de siempre y devuelve esa ganancia a todo, asi que la distancia entre lo fuerte y lo bajito se acorta unos 3 dB sin
// que el volumen total cambie.
//
//  - UMBRAL. 6 dB por debajo del volumen objetivo del nivelado (normTarget), medido SIN el volumen general ni el modo noche
//    (se divide por el Master de la cadena, como en SalaLocal): bajar el volumen de madrugada no cambia lo que hace.
//  - FORMA. 2 a 1 con rodilla suave de 6 dB: la reduccion entra poco a poco, sin escalon. Lo que esta por debajo del umbral
//    no se aprieta.
//  - TIEMPOS. Ataque de unos 15 ms y suelta de unos 250 ms, sobre la ganancia (no sobre la senal): ni muerde los golpes ni
//    bombea. El nivel se mide en ventanas de 5 ms.
//  - ENLAZADO. Los dos canales comparten UNA ganancia (la potencia media de los dos): la imagen estereo no se mueve.
//  - GANANCIA DE COMPENSACION AUTOMATICA. Lo que se aprieta se devuelve (hasta 3 dB), pero nunca mas de lo que deja el techo
//    del limitador de pico real: se vigila el pico ya comprimido (sube de golpe, se recuerda 2 s entero y despues baja con
//    2 s de constante) y la compensacion se reduce lo que haga falta para que ese pico, con ella, no pase de TechoPicoReal (con un margen de 0,13 dB).
//    Sube despacio (250 ms) y baja deprisa (15 ms). Cerca del silencio no se sube nada (no se levanta el soplido de la
//    grabacion), como en las partes suaves. Sin mirar hacia adelante no puede saber que viene un pico mas alto que todos los
//    de los ultimos segundos: ese, si llega, lo recorta el limitador de pico real que va despues (que solo baja).
//  - Nunca toca el volumen de Windows ni el de la salida: solo multiplica la senal de esta cancion.
//
// Apagado (de fabrica) la etapa NO toca ni una muestra: sale bit a bit lo que entra. Encenderlo o apagarlo a media cancion
// no da chasquido: la ganancia parte de 1 y llega a su sitio con el ataque, y al apagar vuelve a 1 en unos 40 ms. Las pistas
// seguidas del mismo disco heredan el estado (Sigue). Los mandos se leen en cada lectura: se oyen al instante.
//
// COSTE: dos multiplicaciones y una comparacion por muestra y, cada 32 cuadros (0,7 ms), tres o cuatro funciones
// trascendentes. Nada se reserva al leer (pruebas/presenciasala.cs lo mide).
using System;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        // El interruptor de Ctrl+F3 -> Sonido -> El local y sus bocinas -> Tono y presencia ("presencia_sala=", apagado de fabrica).
        private static bool presenciaSala;

        private sealed class PresenciaSala : ISampleProvider
        {
            internal const double UmbralBajoObjetivoDb = 6.0, RodillaDb = 6.0, Razon = 2.0;
            internal const double CompensaMaxDb = 3.0;
            internal const double VentanaSeg = 0.005, AtaqueSeg = 0.015, SueltaSeg = 0.25, PicoRecuerdaSeg = 2.0, PicoSeg = 2.0, ApagaSeg = 0.04;
            internal const double PuertaDb = -58.0, PuertaAnchoDb = 8.0;   // como SalaLocal: cerca del silencio no se compensa
            internal const int Trozo = 32;                                  // cuadros por cada nueva cuenta de la ganancia

            private readonly ISampleProvider src;
            private readonly LevelTap tap;
            private readonly int tasa, canales;
            private readonly bool vale;
            private readonly double umbralDb, techo, picoSinRecorte;
            private volatile bool encendido;
            private readonly double aVentana, aAtaque, aSuelta, kPico, pasoApaga;
            private readonly int picoRecuerda;   // trozos que se recuerda el pico entero antes de dejarlo bajar
            // Estado que sigue la musica (y que hereda la pista siguiente del mismo disco).
            private double potencia;        // potencia media suavizada (5 ms), con el Master incluido
            private double grDb;            // reduccion suavizada (dB, <= 0)
            private double compDb;          // compensacion que se aplica (dB, >= 0)
            private double picoEnv;         // pico comprimido (lineal), subida instantanea, recordado PicoRecuerdaSeg y despues bajando
            private int picoQueda;          // trozos que le quedan de recuerdo entero
            private double g = 1.0, dg;     // ganancia de este cuadro y su paso por cuadro
            // El trozo que se esta midiendo.
            private double suma, maximo;
            private int cuadroEnTrozo, canal;
            private volatile PresenciaSala anterior;
            private bool empezo;

            public PresenciaSala(ISampleProvider s, LevelTap volumen, float objetivo, bool encender)
            {
                src = s; tap = volumen;
                tasa = s.WaveFormat.SampleRate; canales = s.WaveFormat.Channels;
                vale = (canales == 1 || canales == 2) && tasa >= 8000;
                encendido = encender;
                double obj = (objetivo > 0.001f && objetivo <= 1f) ? objetivo : 0.26;
                umbralDb = 20.0 * Math.Log10(obj) - UmbralBajoObjetivoDb;
                techo = TechoPicoReal * 0.985;   // un pelo por debajo: la cuenta va un trozo (0,7 ms) por detras del pico
                // Si cabe incluso la compensacion maxima, el logaritmo no puede limitarla.
                // Margen conservador para no cambiar decisiones por redondeo en el borde.
                picoSinRecorte = techo / Math.Pow(10.0, CompensaMaxDb / 20.0) * (1.0 - 1e-12);
                aVentana = 1.0 - Math.Exp(-(double)Trozo / (VentanaSeg * tasa));
                aAtaque = 1.0 - Math.Exp(-(double)Trozo / (AtaqueSeg * tasa));
                aSuelta = 1.0 - Math.Exp(-(double)Trozo / (SueltaSeg * tasa));
                kPico = Math.Exp(-(double)Trozo / (PicoSeg * tasa));
                picoRecuerda = (int)(PicoRecuerdaSeg * tasa / Trozo);
                pasoApaga = 0.42 * Trozo / (ApagaSeg * tasa);   // 0,42 = 3 dB en lineal: el tramo mas largo hasta 1 en ApagaSeg
                potencia = Math.Pow(10.0, (20.0 * Math.Log10(obj)) / 10.0);   // empieza "en el nivel de siempre"
            }

            public WaveFormat WaveFormat { get { return src.WaveFormat; } }
            internal bool Encendido { get { return encendido; } }
            internal double GananciaAhora { get { return g; } }
            internal double ReduccionDb { get { return grDb; } }
            internal double CompensaDb { get { return compDb; } }
            internal double UmbralDb { get { return umbralDb; } }

            internal void Ajusta(bool encender) { if (vale) encendido = encender; }

            internal void Sigue(PresenciaSala previa) { if (previa != null && !object.ReferenceEquals(previa, this)) anterior = previa; }
            private void Hereda()
            {
                PresenciaSala a = anterior; anterior = null;
                if (a == null || !a.vale || a.tasa != tasa || a.canales != canales) return;
                potencia = a.potencia; grDb = a.grDb; compDb = a.compDb; picoEnv = a.picoEnv; picoQueda = a.picoQueda; g = a.g; dg = a.dg;
                suma = a.suma; maximo = a.maximo; cuadroEnTrozo = a.cuadroEnTrozo; canal = a.canal;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                if (!vale || n <= 0) return n;
                if (!empezo) { empezo = true; Hereda(); }
                bool on = encendido;
                if (!on && g == 1.0 && dg == 0.0)
                {
                    // El bypass tambien avanza por los canales: al encender despues de
                    // una lectura impar, la derecha sigue compartiendo ganancia con SU izquierda.
                    canal = (canal + n % canales) % canales;
                    return n;   // apagado y ya en 1: ni toca la muestra
                }
                Proceso(buffer, offset, n, on);
                return n;
            }

            private void Proceso(float[] b, int o, int n, bool on)
            {
                double master = 1.0;
                try { float m = (tap != null) ? tap.Master : 1f; if (m > 1e-4f && !float.IsNaN(m) && !float.IsInfinity(m)) master = m; } catch { }
                double master2 = master * master;
                double gg = g, d = dg, sm = suma, mx = maximo;
                int cf = cuadroEnTrozo, ca = canal, ch = canales;
                for (int i = o, fin = o + n; i < fin; i++)
                {
                    double x = b[i];
                    double q = x * x;
                    sm += q; if (q > mx) mx = q;
                    b[i] = (float)(x * gg);
                    if (++ca < ch) continue;
                    ca = 0; gg += d;
                    if (++cf < Trozo) continue;
                    cf = 0;
                    // Un trozo entero (32 cuadros): una cuenta nueva de la ganancia, y un paso por cuadro hacia ella.
                    double siguiente = SiguienteGanancia(sm / (double)(Trozo * ch), mx, gg, master2, on);
                    sm = 0.0; mx = 0.0;
                    d = (siguiente - gg) / Trozo;
                }
                g = gg; dg = d; suma = sm; maximo = mx; cuadroEnTrozo = cf; canal = ca;
                if (!on && gg == 1.0 && d == 0.0) { grDb = 0.0; compDb = 0.0; picoEnv = 0.0; picoQueda = 0; suma = 0.0; maximo = 0.0; }
            }

            // La ganancia que debe tener el trozo que viene, con la potencia media (de los dos canales) y el pico^2 del que acaba.
            private double SiguienteGanancia(double mediaCuadrados, double picoCuadrado, double gActual, double master2, bool on)
            {
                if (!on)
                {
                    // Apagando: vuelve a 1 en ApagaSeg, sin pasar de largo.
                    grDb = 0.0; compDb = 0.0;
                    if (gActual > 1.0) return gActual - 1.0 <= pasoApaga ? 1.0 : gActual - pasoApaga;
                    if (gActual < 1.0) return 1.0 - gActual <= pasoApaga ? 1.0 : gActual + pasoApaga;
                    return 1.0;
                }
                potencia += (mediaCuadrados - potencia) * aVentana;
                if (potencia < 1e-20) potencia = 0.0;
                double nivelDb = potencia > 0.0 ? 10.0 * Math.Log10(potencia / master2) : -200.0;
                // La curva: nada por debajo de la rodilla; 2 a 1 por encima, con la rodilla suave en medio.
                double sobre = nivelDb - umbralDb, gr;
                if (2.0 * sobre <= -RodillaDb) gr = 0.0;
                else if (2.0 * Math.Abs(sobre) < RodillaDb) { double u = sobre + RodillaDb / 2.0; gr = (1.0 / Razon - 1.0) * u * u / (2.0 * RodillaDb); }
                else gr = (1.0 / Razon - 1.0) * sobre;
                grDb += (gr - grDb) * (gr < grDb ? aAtaque : aSuelta);
                if (grDb > -1e-6 && gr == 0.0) grDb = 0.0;
                // La compensacion: lo que se aprieta se devuelve hasta CompensaMaxDb, menos cerca del silencio y sin pasar del techo.
                double objetivoComp = CompensaMaxDb;
                if (nivelDb <= PuertaDb) objetivoComp = 0.0;
                else { double puerta = (nivelDb - PuertaDb) / PuertaAnchoDb; if (puerta < 1.0) objetivoComp *= puerta; }
                double sinComp = Math.Pow(10.0, grDb / 20.0);
                double pico = Math.Sqrt(picoCuadrado) * sinComp;   // el pico ya comprimido (la senal real, con el Master dentro)
                if (pico >= picoEnv) { picoEnv = pico; picoQueda = picoRecuerda; }
                else if (picoQueda > 0) picoQueda--;
                else picoEnv *= kPico;
                if (picoEnv > picoSinRecorte)
                {
                    double espacio = 20.0 * Math.Log10(techo / picoEnv);   // lo que queda hasta el techo (negativo si ya lo pasa)
                    if (espacio < objetivoComp) objetivoComp = Math.Max(0.0, espacio);
                }
                compDb += (objetivoComp - compDb) * (objetivoComp < compDb ? aAtaque : aSuelta);
                if (compDb < 1e-6) compDb = 0.0;
                return Math.Pow(10.0, (grDb + compDb) / 20.0);
            }
        }
    }
}
