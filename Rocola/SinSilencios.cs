// SALTAR LOS SILENCIOS LARGOS (las dos ramas; interruptor en Ctrl+F3 -> Sonido -> Tu biblioteca).
//
// Hay canciones con varios segundos mudos al principio, con un final que se queda callado diez o veinte segundos, o
// con una "pista oculta" que llega despues de un minuto de nada. En un bar eso es la rocola callada sin motivo.
//
// DE DONDE SALE. La medida entera de cada cancion (Sonoridad.cs) ya mira el pico de cada bloque de 100 ms; ahora apunta
// ademas donde empieza y donde acaba el sonido y cual es el silencio mas largo de en medio (PicosBloques.Mapa). Se
// guarda en rocola_sonoridad.txt (algoritmo 3). "Silencio" es un bloque cuyo pico no pasa de -60 dBFS: lo mismo que la
// medida ya daba por mudo para el P99. Mientras una cancion no este medida con el algoritmo 3, suena como siempre.
//
// QUE SE SALTA (PlanSilencios):
//   - al principio, si hay 2 s o mas de silencio: se empieza 0,3 s antes del primer sonido;
//   - al final, si hay 2 s o mas: se acaba 0,5 s despues del ultimo sonido (o lo que dure el fundido, si es mas);
//   - en medio, el silencio mas largo si pasa de 5 s (el de antes de una pista oculta): se deja la misma cola detras
//     del sonido y se retoma 0,3 s antes del siguiente. La pista oculta SI suena; lo que se quita es la espera.
// Las pausas cortas son musica y no se tocan.
//
// COMO. La cancion se envuelve (FuenteSinSilencios) al abrirla, en segundo plano: el principio mudo se descomprime y
// se tira ahi mismo (o, si pasa de 20 s, se salta con un salto del lector), asi que al hilo de audio le llega la
// cancion ya empezando. El final se corta dejando de entregar muestras; el hueco de en medio, con un salto del lector
// (cuesta lo que cuesta buscar en el archivo, no descomprimir un minuto de nada). El fundido de salida se calcula con
// lo que de verdad va a sonar, asi que cae en el margen mudo del final y no en la musica.
//
// El mapa es de una ruta: si el archivo que hay en ella ya no dura lo que se midio (lo cambiaron por otro con el mismo
// nombre), no se usa y la cancion suena entera (MapaSilencios.DeEsteArchivo).
//
// El interruptor se mira al abrir cada cancion: apagarlo o encenderlo se nota desde la siguiente. Decision del usuario
// del 28 de septiembre de 2026: esta opcion lleva interruptor en Ctrl+F3.
using System;
using System.Globalization;

namespace Rocola
{
    // Donde suena algo en una cancion, en segundos. Desde < 0: no suena nada (se deja como esta). HuecoDesde < 0: no
    // hay silencio en medio.
    internal struct MapaSilencios
    {
        internal float Desde, Hasta, HuecoDesde, HuecoHasta, Dura;
        internal static MapaSilencios Mudo(float dura)
        {
            MapaSilencios m; m.Desde = -1f; m.Hasta = -1f; m.HuecoDesde = -1f; m.HuecoHasta = -1f; m.Dura = dura; return m;
        }
        internal bool Suena { get { return Desde >= 0f; } }
        internal bool ConHueco { get { return HuecoDesde >= 0f; } }
        // Coherente: lo que se lee de la tabla tiene que cuadrar o no se usa.
        internal bool Valido
        {
            get
            {
                if (!(Dura > 0f) || Dura > 36000f) return false;
                if (!Suena) return Hasta < 0f && !ConHueco && HuecoHasta < 0f;
                if (!(Hasta > Desde) || Hasta > Dura + 0.2f) return false;
                if (!ConHueco) return HuecoHasta < 0f;
                return HuecoDesde > Desde && HuecoHasta > HuecoDesde && HuecoHasta < Hasta;
            }
        }
        // EL MAPA TIENE QUE SER DE ESTE ARCHIVO. Las medidas se guardan por ruta, y en esa ruta puede haber hoy OTRO
        // archivo (una version mejor con el mismo nombre, un disco rehecho). Con el mapa del de antes se saltaba musica
        // de verdad al principio o en medio, o se cortaba el final de la cancion pagada. Solo se usa si el archivo dura lo
        // que se midio (con un segundo de margen); si no, suena entera, como antes de medirla, hasta que se vuelva a medir.
        internal const double MargenDura = 1.0;
        internal bool DeEsteArchivo(double segundos) { return Valido && Math.Abs(segundos - Dura) <= MargenDura; }
        internal static string Texto(float v) { return v < 0f ? "-" : v.ToString("0.##", CultureInfo.InvariantCulture); }
        internal static bool Lee(string t, out float v)
        {
            v = -1f;
            if (t == "-") return true;
            return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && !float.IsNaN(v) && v >= 0f && v <= 36000f;
        }
    }

    // Que se salta de una cancion, en cuadros. Null (De) si no hay nada que saltar.
    internal sealed class PlanSilencios
    {
        internal const double MinExtremo = 2.0;       // silencio al principio o al final que ya se salta
        internal const double MinHueco = 5.0;         // silencio en medio que ya se salta (el de una pista oculta)
        internal const double AntesDelSonido = 0.3;   // lo que se deja antes de que vuelva a sonar
        internal const double TrasElSonido = 0.5;     // lo que se deja despues del ultimo sonido (o el fundido, si es mas)
        internal long Inicio;                         // cuadro por el que se empieza (0: desde el principio)
        internal long Fin = -1;                       // cuadro en el que se acaba (-1: donde acabe el archivo)
        internal long HuecoDesde = -1, HuecoHasta = -1;   // de donde a donde se salta en medio (-1: nada)
        internal static PlanSilencios De(MapaSilencios m, int tasa, double fundidoSeg)
        {
            if (!m.Valido || !m.Suena || tasa < 1) return null;
            double tras = Math.Max(TrasElSonido, fundidoSeg);
            PlanSilencios p = new PlanSilencios();
            bool algo = false;
            if (m.Desde >= MinExtremo) { p.Inicio = (long)((m.Desde - AntesDelSonido) * tasa); algo = true; }
            if (m.Dura - m.Hasta >= MinExtremo && m.Hasta + tras < m.Dura) { p.Fin = (long)((m.Hasta + tras) * tasa); algo = true; }
            if (m.ConHueco && m.HuecoHasta - m.HuecoDesde >= MinHueco)
            {
                long a = (long)((m.HuecoDesde + tras) * tasa), b = (long)((m.HuecoHasta - AntesDelSonido) * tasa);
                if (b > a) { p.HuecoDesde = a; p.HuecoHasta = b; algo = true; }
            }
            return algo ? p : null;
        }
        // Cuantos segundos se saltan de una cancion con este mapa (la misma cuenta que al sonar).
        internal static double SegundosSaltados(MapaSilencios m, double fundidoSeg)
        {
            PlanSilencios p = De(m, 1000, fundidoSeg);
            if (p == null) return 0.0;
            double s = p.Inicio / 1000.0;
            if (p.Fin >= 0) s += Math.Max(0.0, m.Dura - p.Fin / 1000.0);
            if (p.HuecoDesde >= 0) s += (p.HuecoHasta - p.HuecoDesde) / 1000.0;
            return s;
        }
    }

    // EL RECORTE, contando en muestras (de todos los canales), como RecorteRelleno: un origen que entrega cuadros
    // partidos no descoloca nada. 'salta' mueve el lector a un cuadro; si falla, se sigue leyendo sin saltar.
    internal sealed class RecorteSilencios
    {
        internal const double MaxTirarSeg = 20.0;     // un principio mudo mas largo se salta con el lector, no descomprimiendo
        private readonly Func<float[], int, int, int> lee;
        private readonly Action<long> salta;
        private readonly int ch;
        private long pos;                             // muestras desde el principio del archivo
        private long fin = -1, hDesde = -1, hHasta = -1;
        internal RecorteSilencios(Func<float[], int, int, int> origen, Action<long> saltaA, int canales, int tasa, PlanSilencios p)
        {
            lee = origen; salta = saltaA; ch = Math.Max(1, canales);
            if (p == null) return;
            if (p.Fin >= 0) fin = p.Fin * ch;
            if (p.HuecoDesde >= 0) { hDesde = p.HuecoDesde * ch; hHasta = p.HuecoHasta * ch; }
            if (p.Inicio > 0) Empieza(p.Inicio, tasa);
        }
        // El principio mudo: se tira descomprimiendo (corto) o se salta con el lector (largo).
        private void Empieza(long cuadro, int tasa)
        {
            long hasta = cuadro * ch;
            if (cuadro > (long)(MaxTirarSeg * tasa) && Salta(cuadro)) return;
            float[] basura = new float[4096 * ch];
            while (pos < hasta)
            {
                int r = lee(basura, 0, (int)Math.Min(basura.Length, hasta - pos));
                if (r <= 0) break;
                pos += r;
            }
        }
        private bool Salta(long cuadro)
        {
            if (salta == null) return false;
            try { salta(cuadro); pos = cuadro * ch; return true; }
            catch { return false; }
        }
        // Donde acaba de verdad (cuadro), o -1 si acaba donde el archivo.
        internal long FinCuadro { get { return fin >= 0 ? fin / ch : -1; } }
        // Retomar desde fuera (volvio el aparato de sonido): se sigue contando desde ahi.
        internal void Movido(long cuadro) { pos = Math.Max(0, cuadro) * ch; }
        internal long Cuadro { get { return pos / ch; } }
        internal int Lee(float[] buffer, int offset, int count)
        {
            while (true)
            {
                if (fin >= 0 && pos >= fin) return 0;
                if (hDesde >= 0 && pos >= hDesde && pos < hHasta)
                {
                    long h = hHasta; hDesde = -1;   // una sola vez: si el salto falla, se sigue leyendo el hueco
                    Salta(h / ch);
                    continue;
                }
                long lim = count;
                if (fin >= 0) lim = Math.Min(lim, fin - pos);
                if (hDesde >= 0 && pos < hDesde) lim = Math.Min(lim, hDesde - pos);
                int n = lee(buffer, offset, (int)lim);
                if (n > 0) pos += n;
                return n;
            }
        }
        // Cuantos cuadros van a salir desde aqui hasta el final, si el archivo tiene 'total' (para el fundido).
        internal long Salen(long total)
        {
            long desde = pos / ch, hasta = total;
            if (fin >= 0) hasta = Math.Min(hasta, fin / ch);
            long n = hasta - desde;
            if (hDesde >= 0)
            {
                long a = Math.Max(desde, hDesde / ch), b = Math.Min(hasta, hHasta / ch);
                if (b > a) n -= b - a;
            }
            return Math.Max(0, n);
        }
    }

    public partial class Form1
    {
        // Ctrl+F3 -> Sonido -> Tu biblioteca: "Saltar silencios largos". Encendido de fabrica ("saltar_silencios=").
        private bool saltaSilencios = true;
        // La tabla de medidas la escribe la pantalla; las canciones se abren en segundo plano. Quien la cambia o la lee
        // desde otro hilo pasa por aqui.
        private static readonly object integCerrojo = new object();

        private bool MapaDe(string path, out MapaSilencios m)
        {
            m = MapaSilencios.Mudo(0f);
            if (path == null) return false;
            SonoridadIntegrada s;
            lock (integCerrojo)
            {
                System.Collections.Generic.Dictionary<string, SonoridadIntegrada> t = integCache;
                // Con DSEE ULTRA suena la version de Apollo (otro archivo, alineado con la original): vale la medida de
                // la original (RutaOriginalDe, en AperturaAudio.cs).
                if (t == null || (!t.TryGetValue(path, out s) && !t.TryGetValue(RutaOriginalDe(path), out s))) return false;
            }
            if (!s.ConSilencios) return false;
            m = s.Silencios;
            return true;
        }

        // LO QUE DE VERDAD SUENA UNA CANCION (segundos; 0 = no se sabe), para la espera de la cola: su duracion menos
        // los silencios largos que se saltan, si el interruptor esta encendido y la cancion ya esta medida. Sin esto,
        // "en ~7 min" contaba tambien los 20 s mudos del final de cada cancion de la cola.
        private float SegundosQueSuenan(string path)
        {
            float d = SegundosDe(path);
            if (d <= 0f || !saltaSilencios) return d;
            MapaSilencios m;
            if (!MapaDe(path, out m)) return d;
            double r = d - PlanSilencios.SegundosSaltados(m, crossfadeSec);
            return (r > 1.0) ? (float)r : d;
        }
        // Lo que le queda a la que suena si va envuelta: desde donde va, sin lo que aun falte por saltar (-1: no va).
        private static double QuedaSinSilencios(IAudioSource r, double totalSeg)
        {
            FuenteSinSilencios s = r as FuenteSinSilencios;
            if (s == null || !(totalSeg > 0)) return -1;
            int hz = Math.Max(1, s.WaveFormat.SampleRate);
            return s.Salen((long)(totalSeg * hz)) / (double)hz;
        }

        // Envuelve la cancion si el interruptor esta encendido y hay algo que saltar; si no, la deja tal cual.
        private IAudioSource SinSilencios(string path, IAudioSource s)
        {
            if (s == null || !saltaSilencios) return s;
            try
            {
                MapaSilencios m;
                if (!MapaDe(path, out m)) return s;
                if (!m.DeEsteArchivo(s.TotalTime.TotalSeconds)) return s;   // en esa ruta hay ahora otro archivo (ver DeEsteArchivo)
                PlanSilencios p = PlanSilencios.De(m, s.WaveFormat.SampleRate, crossfadeSec);
                if (p == null) return s;
                return new FuenteSinSilencios(s, p);
            }
            catch { return s; }
        }

        // La cancion sin sus silencios largos (la cuenta, en RecorteSilencios).
        private sealed class FuenteSinSilencios : IAudioSource
        {
            internal readonly IAudioSource Interior;
            private readonly RecorteSilencios recorte;
            private readonly int tasa;
            internal FuenteSinSilencios(IAudioSource origen, PlanSilencios p)
            {
                Interior = origen; tasa = Math.Max(1, origen.WaveFormat.SampleRate);
                recorte = new RecorteSilencios(origen.Read, delegate(long cuadro) { origen.Salta(TimeSpan.FromSeconds((double)cuadro / tasa)); },
                                               origen.WaveFormat.Channels, tasa, p);
            }
            internal long Salen(long total) { return recorte.Salen(total); }
            public NAudio.Wave.WaveFormat WaveFormat { get { return Interior.WaveFormat; } }
            public int Read(float[] buffer, int offset, int count) { return recorte.Lee(buffer, offset, count); }
            public float Volume { set { Interior.Volume = value; } }
            public TimeSpan CurrentTime { get { return Interior.CurrentTime; } }
            // EL FINAL DE VERDAD. El paso sin pausa prepara la siguiente cuando a esta le quedan 12 s (TotalTime menos
            // CurrentTime) y la barra de progreso se llena con lo mismo: con el final mudo cortado, contando con el del
            // archivo la cancion acababa cuando aun "quedaban" los segundos del silencio y la siguiente llegaba tarde.
            public TimeSpan TotalTime
            {
                get
                {
                    TimeSpan t = Interior.TotalTime;
                    long f = recorte.FinCuadro;
                    if (f < 0) return t;
                    TimeSpan corte = TimeSpan.FromSeconds((double)f / tasa);
                    return corte < t ? corte : t;
                }
            }
            public void Salta(TimeSpan donde) { Interior.Salta(donde); recorte.Movido((long)(donde.TotalSeconds * tasa)); }
            public void Dispose() { Interior.Dispose(); }
        }
    }
}
