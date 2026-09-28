// "SUBIR TODO EL VOLUMEN" PAREJO Y CON TECHO AUTOMATICO (las dos ramas). Decision del usuario del 28 de septiembre
// de 2026 (opcion 2 de tres).
//
// EL PROBLEMA. Lo que ya llega al maximo digital solo puede sonar mas fuerte si el limitador del final lo aplasta. Los
// dB de "Subir TODO" se multiplicaban encima del nivelado sin mirar nada: con +6, las canciones con golpes volvian a
// perder 5-6 dB en el limitador, y con +12 hasta 12 (el volumen "respira" y el sonido cansa). Subir cada cancion solo
// lo que aguantan sus picos se probo y se descarto: rompe el volumen parejo (las dinamicas no suben nada, las
// comprimidas unos 3 dB).
//
// LO QUE SE HACE. Todas suben LO MISMO (el volumen sigue parejo), pero no mas de lo que aguanta la biblioteca: el
// techo es lo mas que se puede subir a todas sin que mas del 10 % de las canciones medidas pase del tope del nivelado
// (el limitador quita como mucho 3 dB en el P99 y 6 en el pico; ver GananciaNivelado). Si se pide mas, se aplica el
// techo y Ctrl+F3 lo dice ("pides +6, tu musica aguanta +3: se aplican +3"). Con menos de 20 canciones con los picos
// medidos no se puede saber y se aplica lo pedido, como antes. Para mas volumen que eso, el amplificador.
//
// COSTE. El techo se calcula en la pantalla recorriendo la tabla de medidas (unos milisegundos con decenas de miles de
// canciones, medido en pruebas/nivelpicos.cs) y se guarda: se rehace si cambian los ajustes del nivelado o, como mucho
// una vez por minuto, si llegaron medidas nuevas. Nunca en el hilo de audio.
using System;
using System.Collections.Generic;

namespace Rocola
{
    public partial class Form1
    {
        internal const double SubirFueraDelTope = 0.10;   // canciones medidas que pueden pasar del tope al subir
        internal const int SubirMinMedidas = 20;          // con menos no se calcula techo (se aplica lo pedido)
        private const int SubirRehacerMs = 60000;         // con medidas nuevas, se rehace como mucho una vez por minuto

        private float subirTecho = float.NaN;             // dB; NaN = sin calcular
        private int subirMedidas;                         // canciones con picos que entraron en la cuenta
        private long subirCambios = -1; private int subirCuando;
        private float subirObj = -1f, subirFuerza = -1f; private bool subirNivela; private bool subirHecho;
        private object subirTabla;                         // la tabla con la que se calculo (al arrancar se carga entera de golpe)

        // Cuanto se puede subir UNA cancion, encima de su nivelado, sin que el limitador pase del tope (dB, >= 0).
        internal static float HolguraDb(float ganancia, float picoLin, float p99Lin)
        {
            float t = TopeSubidaPorPicos(picoLin, p99Lin) / Math.Max(ganancia, 1e-6f);
            if (!(t > 1f)) return 0f;
            return (float)(20.0 * Math.Log10(t));
        }
        // El techo: lo que aguantan todas menos el 10 % que menos aguanta. 'h' se ordena aqui mismo.
        internal static float TechoDeHolguras(float[] h, int n)
        {
            if (h == null || n <= 0) return float.NaN;
            Array.Sort(h, 0, n);
            int fuera = (int)Math.Floor(n * SubirFueraDelTope);
            if (fuera >= n) fuera = n - 1;
            return (float)(Math.Floor(h[fuera] * 10.0) / 10.0);   // a la decima, hacia abajo
        }

        // La ganancia de nivelado de una cancion de la tabla (la misma cuenta que NormGainFor con su medida entera).
        private float GananciaDeTabla(SonoridadIntegrada s)
        {
            if (!normalizeOn || normStrength <= 0.001f) return 1f;
            float rms = s.RmsPorCanal;
            if (!(rms > 0.0001f)) return 1f;
            float cap = (float)Math.Pow(10.0, TierCapDb(rms) / 20.0);
            if (cap > NormMaxBoost) cap = NormMaxBoost;
            return GananciaNivelado(normTarget, rms, normStrength, cap, true, DbALin(s.PicoDb), DbALin(s.P99Db));
        }

        // El techo de la biblioteca (dB), guardado. 'medidas' dice con cuantas canciones se calculo.
        private float TechoSubirDb(out int medidas)
        {
            int ahora = Environment.TickCount;
            bool ajustes = subirObj != normTarget || subirFuerza != normStrength || subirNivela != normalizeOn
                           || !object.ReferenceEquals(subirTabla, integCache);   // tabla nueva (la del arranque): se rehace ya
            bool nuevas = subirCambios != integCambios && unchecked(ahora - subirCuando) >= SubirRehacerMs;
            if (subirHecho && !ajustes && !nuevas) { medidas = subirMedidas; return subirTecho; }
            subirHecho = true; subirCuando = ahora; subirCambios = integCambios;
            subirObj = normTarget; subirFuerza = normStrength; subirNivela = normalizeOn; subirTabla = integCache;
            Dictionary<string, SonoridadIntegrada> t = integCache;
            int n = 0; float[] h = null;
            if (t != null)
            {
                h = new float[t.Count];
                foreach (KeyValuePair<string, SonoridadIntegrada> kv in t)
                {
                    SonoridadIntegrada s = kv.Value;
                    if (!s.Valida || !s.ConPicos || s.Muda || float.IsNaN(s.PicoDb) || s.PicoDb <= PicosBloques.Nada) continue;
                    h[n++] = HolguraDb(GananciaDeTabla(s), DbALin(s.PicoDb), DbALin(s.P99Db));
                }
            }
            subirMedidas = n;
            subirTecho = (n >= SubirMinMedidas) ? TechoDeHolguras(h, n) : float.NaN;
            medidas = n;
            return subirTecho;
        }

        // Los dB de "Subir TODO" que de verdad se aplican: lo pedido, sin pasar del techo de la biblioteca.
        private float SubirAplicadoDb()
        {
            if (!(masterDb > 0f)) return 0f;
            int n;
            float techo = TechoSubirDb(out n);
            if (float.IsNaN(techo)) return masterDb;   // aun no hay bastantes medidas: lo pedido, como antes
            return Math.Min(masterDb, Math.Max(0f, techo));
        }
        // Lo mismo para TODAS las canciones: el volumen sigue parejo.
        private float MasterEfectivo() { return (float)Math.Pow(10.0, SubirAplicadoDb() / 20.0); }

        // El texto de Ctrl+F3 debajo de la barra (dos renglones).
        private string TextoSubir(int pedidoDb)
        {
            if (pedidoDb <= 0) return "Lo mismo de mas para TODAS, sin aplastar los golpes.\nPara mas volumen, mejor el amplificador.";
            int n;
            float techo = TechoSubirDb(out n);
            if (float.IsNaN(techo))
                return "Aun no hay bastantes canciones medidas para saber cuanto\naguantan: se aplica lo pedido (se mide sola en reposo).";
            if (techo < 0.05f) return "Tu musica ya va al limite: subirla mas aplastaria los golpes,\nasi que no se sube. Para mas volumen, el amplificador.";
            string t = techo.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            if (pedidoDb <= techo) return "Sube lo mismo a todas. Tu musica aguanta hasta +" + t + " dB\nsin aplastar los golpes: se aplica todo lo pedido.";
            return "Pides +" + pedidoDb + " dB; tu musica aguanta +" + t + " dB sin aplastar los\ngolpes: se aplican +" + t + " dB. Para mas, el amplificador.";
        }
    }
}
