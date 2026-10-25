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
// AL LIMITE (24-10-2026). El dueno decidio lo que aqui se habia descartado: cada cancion medida con el algoritmo 5 sube hasta
// su limite sin rasparse (GananciaAlLimite, Sonoridad.cs). Esas ya no tienen holgura (HolguraAlLimiteDb = 0 con la fuerza al
// 100 %): cuando casi toda la biblioteca este medida asi, "Subir TODO" dira que la musica ya va al limite y no subira nada. Con
// la fuerza por debajo del 100 % les queda margen, y lo de antes (algoritmos 1 a 4) cuenta con su holgura de siempre.
//
// RETIRADO DE CTRL+F3 (25-10-2026). El dueno: «ya no cumple ninguna funcion realmente». Con cada cancion al limite no queda nada
// que subir, asi que la fila «Extra para todas» se quito y la clave subir_db= se ignora: masterDb se queda en 0 y todo lo de aqui
// devuelve 0 dB (MasterEfectivo = 1 exacto) sin calcular el techo. Se deja la cuenta por si algun dia se vuelve a pedir.
//
// COSTE. El techo se calcula recorriendo la tabla de medidas y se guarda: se rehace si cambian los ajustes del nivelado
// (en la pantalla, al momento: es un ajuste de Ctrl+F3) o, como mucho una vez por minuto, si llegaron medidas nuevas.
// Esto ultimo pasa sin parar mientras se mide la biblioteca, y la cuenta se pedia desde el latido de la pantalla (el
// modo noche mira el volumen en cada vuelta): 11 ms con 20.000 canciones en un procesador de servidor, un tiron cada
// minuto. Ahora, con medidas nuevas, la pantalla solo copia los valores (1 ms) y la cuenta va en otro hilo; hasta que
// llega, vale el techo de antes (el que ya se estaba aplicando). Nunca en el hilo de audio.
using System;
using System.Collections.Generic;
using System.Threading;

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
        private bool subirEnFondo;                         // hay una cuenta del techo en marcha fuera de la pantalla

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

        // Lo que le queda a una cancion medida con el algoritmo 5 (con el P90) hasta su limite sin rasparse, encima de su nivelado. Con
        // el nivelado al limite (24-10-2026) las niveladas ya estan en el: 0, y "Subir TODO" lo dice.
        internal static float HolguraAlLimiteDb(float ganancia, float picoLin, float p99Lin, float p90Lin)
        {
            float t = LimiteSinRaspar(picoLin, p99Lin, p90Lin) / Math.Max(ganancia, 1e-6f);
            if (!(t > 1f)) return 0f;
            return (float)(20.0 * Math.Log10(t));
        }
        private static float GananciaDeTabla(SonoridadIntegrada s, bool nivela, float objetivo, float fuerza)
        {
            if (!nivela || fuerza <= 0.001f) return 1f;
            if (s.ConP90) return GananciaAlLimite(fuerza, DbALin(s.PicoDb), DbALin(s.P99Db), DbALin(s.P90Db));
            float rms = s.RmsPorCanal;
            if (!(rms > 0.0001f)) return 1f;
            float cap = (float)Math.Pow(10.0, TierCapDb(rms) / 20.0);
            if (cap > NormMaxBoost) cap = NormMaxBoost;
            return GananciaNivelado(objetivo, rms, fuerza, cap, true, DbALin(s.PicoDb), DbALin(s.P99Db));
        }
        // La cuenta entera, sin tocar nada de la ventana (sirve en la pantalla y fuera): el techo y con cuantas se hizo.
        private static float TechoDeTabla(ICollection<SonoridadIntegrada> valores, bool nivela, float objetivo, float fuerza, out int medidas)
        {
            int n = 0; float[] h = null;
            if (valores != null)
            {
                h = new float[valores.Count];
                foreach (SonoridadIntegrada s in valores)
                {
                    if (!s.Valida || !s.ConPicos || s.Muda || float.IsNaN(s.PicoDb) || s.PicoDb <= PicosBloques.Nada) continue;
                    float g = GananciaDeTabla(s, nivela, objetivo, fuerza);
                    h[n++] = s.ConP90 ? HolguraAlLimiteDb(g, DbALin(s.PicoDb), DbALin(s.P99Db), DbALin(s.P90Db)) : HolguraDb(g, DbALin(s.PicoDb), DbALin(s.P99Db));
                }
            }
            medidas = n;
            return (n >= SubirMinMedidas) ? TechoDeHolguras(h, n) : float.NaN;
        }

        // El techo de la biblioteca (dB), guardado. 'medidas' dice con cuantas canciones se calculo.
        private float TechoSubirDb(out int medidas)
        {
            int ahora = Environment.TickCount;
            bool ajustes = subirObj != normTarget || subirFuerza != normStrength || subirNivela != normalizeOn
                           || !object.ReferenceEquals(subirTabla, integCache);   // tabla nueva (la del arranque): se rehace ya
            bool nuevas = subirCambios != integCambios && unchecked(ahora - subirCuando) >= SubirRehacerMs;
            if (subirHecho && !ajustes && !nuevas) { medidas = subirMedidas; return subirTecho; }
            if (subirHecho && !ajustes && RehaceTechoEnFondo(ahora)) { medidas = subirMedidas; return subirTecho; }   // medidas nuevas: fuera
            subirHecho = true; subirCuando = ahora; subirCambios = integCambios;
            subirObj = normTarget; subirFuerza = normStrength; subirNivela = normalizeOn; subirTabla = integCache;
            Dictionary<string, SonoridadIntegrada> t = integCache;
            int n;
            subirTecho = TechoDeTabla(t != null ? t.Values : null, normalizeOn, normTarget, normStrength, out n);
            subirMedidas = n;
            medidas = n;
            return subirTecho;
        }

        // CON MEDIDAS NUEVAS, LA CUENTA FUERA DE LA PANTALLA: se copian los valores (la tabla es de la pantalla) y los
        // ajustes de ahora, se cuenta en otro hilo y el resultado vuelve a la pantalla, que lo pone si nada cambio
        // mientras. Hasta entonces vale el de antes. Sin ventana a la que volver (y sin prueba que lo pida), false: se
        // cuenta aqui, como antes.
        private bool RehaceTechoEnFondo(int ahora)
        {
            if (subirEnFondo) return true;   // ya hay una en marcha: mientras, el de antes
            Action<System.Windows.Forms.MethodInvoker> vuelve = vuelveALaPantalla;
            Dictionary<string, SonoridadIntegrada> t = integCache;
            if (t == null || (vuelve == null && !IsHandleCreated)) return false;
            SonoridadIntegrada[] valores = new SonoridadIntegrada[t.Count];
            t.Values.CopyTo(valores, 0);
            bool nivela = normalizeOn; float objetivo = normTarget, fuerza = normStrength;
            subirEnFondo = true; subirCuando = ahora; subirCambios = integCambios;
            ThreadPool.QueueUserWorkItem(delegate
            {
                int n;
                float techo = TechoDeTabla(valores, nivela, objetivo, fuerza, out n);
                System.Windows.Forms.MethodInvoker pon = delegate
                {
                    subirEnFondo = false;
                    // Si mientras tanto cambiaron los ajustes o la tabla, ya se rehizo en la pantalla con lo nuevo.
                    if (subirNivela != nivela || subirObj != objetivo || subirFuerza != fuerza || !object.ReferenceEquals(subirTabla, t)) return;
                    subirTecho = techo; subirMedidas = n;
                };
                if (vuelve != null) { vuelve(pon); return; }
                try { BeginInvoke(pon); }
                catch (InvalidOperationException) { }   // la ventana ya se cerro
            });
            return true;
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

        // LOS TEXTOS DEL NIVELADO EN CTRL+F3 (25-10-2026: que se entienda sin saber de sonido).
        // El valor de "Cuanto se sube cada cancion": con palabras en los extremos.
        private static string TextoCuantoSube(int pct)
        {
            if (pct >= 100) return "Al limite";
            if (pct <= 0) return "Tal cual";
            return pct + "%";
        }
        // El segundo renglon de esa fila: lo que de verdad lleva la cancion que suena (el nivelado, con el margen del tono del local).
        private string TextoLaQueSuena()
        {
            PlayItem np = nowPlaying; LevelTap tap = curTap;
            if (np == null || np.Song == null || tap == null) return "menos, se quedan un poco por debajo de su limite.";
            float rms;
            if (!SonoridadParaNivelar(np.Song.Path, out rms)) return "la que suena aun no esta medida: va como viene.";
            double db = 20.0 * Math.Log10(Math.Max(1e-6, (double)tap.Gain));
            string v = (db >= 0 ? "+" : "\u2212") + Math.Abs(db).ToString("0.0", System.Globalization.CultureInfo.CurrentCulture) + " dB";
            float p90;
            return P90ParaNivelar(np.Song.Path, out p90)
                ? ("la que suena ahora va " + v + ".")
                : ("la que suena va " + v + " (con la medida de antes).");
        }
        // La fila del volumen de lo que no se puede subir al limite: los videos y las canciones que el repaso aun no midio.
        private string TextoSinMedir()
        {
            int falta = integLeft;
            return falta > 0
                ? ("Los videos y las " + falta.ToString("#,0") + " canciones que faltan por medir\nsuben hacia aqui; las ya medidas, hasta su limite.")
                : "Los videos suben hacia aqui. Las canciones ya estan\nmedidas: cada una sube hasta su limite.";
        }
    }
}
