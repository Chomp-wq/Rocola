using System;

namespace Rocola
{
    public partial class Form1
    {
        // Optimizaciones automaticas: no son preferencias visuales de Ctrl+F3.
        [ThreadStatic] private static Func<bool> measureCancellation;

        // LAS RECOLECCIONES GRANDES, FUERA DE CUANDO ALGUIEN ELIGE (24-10-2026). En el diagnostico del A4 los dos peores tirones (194 y
        // 283 ms) cayeron navegando, uno de ellos con una recoleccion de generacion 2 en el mismo cuadro; con un monton de 130 a 220 MB
        // (las 118.000 canciones) una de esas para todos los hilos un buen rato. .NET 3.5 deja pedir que no las haga mientras tanto
        // (GCSettings.LatencyMode = LowLatency): las de generacion 0 y 1 siguen, solo las grandes esperan. Se pide mientras alguien elige
        // (un boton hace menos de 10 s, o una escena del diagnostico completo) y fuera de la atraccion; al dejar de elegir se vuelve al
        // modo de antes y la grande, si toca, llega cuando nadie mira. Con tope: si se sigue eligiendo mas de 5 minutos o el monton crece
        // mas de 128 MB, se vuelve al modo de antes hasta que se deje de elegir. Si el sistema anda corto de memoria, .NET la hace igual.
        // No cambia nada de lo que se ve ni de lo que suena: automatico, sin interruptor. En un .NET sin ese modo no hace nada.
        internal const int EligiendoMs = 10000, GcBajaMaxMs = 300000;
        internal const long GcBajaCrecerBytes = 128L << 20;
        private bool gcBaja, gcAgotada, gcModoPrevioOk; private int gcBajaDesde; private long gcBajaMemoria; private int gcModoPrevio;
        internal static int GcBajasPuestas, GcBajasQuitadas;   // (pruebas y diagnostico)
        private bool AlguienElige()
        {
            if (attractOn) return false;
            if (diagEnMarcha) return true;
            int t = lastInputTick;
            return t != 0 && unchecked(Environment.TickCount - t) < EligiendoMs;
        }
        // Desde el latido (Form1.cs): unas comparaciones por vuelta.
        private void RevisaRecolector()
        {
            if (!AlguienElige()) { gcAgotada = false; if (gcBaja) QuitaGcBaja(); return; }
            if (gcBaja)
            {
                if (unchecked(Environment.TickCount - gcBajaDesde) > GcBajaMaxMs || GC.GetTotalMemory(false) - gcBajaMemoria > GcBajaCrecerBytes)
                { QuitaGcBaja(); gcAgotada = true; }
                return;
            }
            if (gcAgotada) return;
            int previo;
            if (!LeeLatenciaGc(out previo)) return;
            if (!PonLatenciaGc(-1)) return;   // -1: LowLatency
            if (!gcModoPrevioOk) { gcModoPrevio = previo; gcModoPrevioOk = true; }
            gcBaja = true; gcBajaDesde = Environment.TickCount; gcBajaMemoria = GC.GetTotalMemory(false); GcBajasPuestas++;
        }
        private void QuitaGcBaja() { gcBaja = false; if (PonLatenciaGc(gcModoPrevioOk ? gcModoPrevio : -2)) GcBajasQuitadas++; }   // -2: Interactive
        // GCSettings.LatencyMode existe desde .NET 2.0 SP1 (el de la rocola de XP es 2.0.50727.1433). Si no existiera, la llamada truena
        // al compilarse el metodo de dentro: se atrapa fuera y no se hace nada.
        private static bool LeeLatenciaGc(out int modo) { modo = -2; try { modo = LeeLatenciaGcDirecto(); return true; } catch { return false; } }
        private static bool PonLatenciaGc(int modo) { try { PonLatenciaGcDirecto(modo); return true; } catch { return false; } }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static int LeeLatenciaGcDirecto() { return (int)System.Runtime.GCSettings.LatencyMode; }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void PonLatenciaGcDirecto(int modo)   // el de antes tal cual (su numero), o -1 LowLatency / -2 Interactive por nombre
        {
            System.Runtime.GCSettings.LatencyMode = modo == -1 ? System.Runtime.GCLatencyMode.LowLatency
                : modo == -2 ? System.Runtime.GCLatencyMode.Interactive : (System.Runtime.GCLatencyMode)modo;
        }

        // Solo scratch del analisis: ningun lector, formulario, ruta ni resultado publicado.
        // Una instancia por hilo; una llamada reentrante recibe otra y no pisa la medida externa.
        [ThreadStatic] private static TrabajoMedida trabajoMedidaDelHilo;
        private sealed class TrabajoMedida
        {
            internal const int MaxCanalesRetenidos = 8, MaxParesRetenidos = 65536;
            internal const int MaxBloquesRetenidos = 8192;
            internal bool EnUso;
            internal int LecturaMax;
            private int frecuencia, frecuenciaBandas;
            internal float[] C1, C2, Estado, Buffer, EstadoBandas;
            internal int[] Recorte;
            internal readonly float[][] Bandas = new float[8][];
            internal readonly double[] SumaBandas = new double[8];
            internal readonly float[] Cuerpo = new float[5], Features = new float[28], Target = new float[12];
            private readonly SpectralTap[] analisis = new SpectralTap[3];
            private readonly float[][] cuadros = new float[3][];
            private readonly System.Collections.Generic.List<float>[] pares = new System.Collections.Generic.List<float>[2];
            private MedidorBs1770 integrada;

            private static T[] Capacidad<T>(T[] buffer, int n)
            { return buffer != null && buffer.Length >= n ? buffer : new T[n]; }
            internal void Prepara(int rate, int canales, bool tonal)
            {
                if (frecuencia != rate)
                {
                    C1 = EqShelfHi(1681.974f, rate, 3.99984f, 0.70717f);
                    C2 = EqHighPass(38.1355f, rate, 0.50033f);
                    frecuencia = rate; frecuenciaBandas = 0;
                    Array.Clear(Bandas, 0, Bandas.Length);
                    Array.Clear(analisis, 0, analisis.Length); // solo se retiene un formato, no un diccionario
                }
                if (tonal && frecuenciaBandas != rate)
                {
                    for (int b = 0; b < Bandas.Length; b++) Bandas[b] = EqBpf(EqCentros[b], rate);
                    frecuenciaBandas = rate;
                }
                LecturaMax = checked(4096 * canales); // el conteo LOGICO original, no Buffer.Length
                Buffer = Capacidad(Buffer, LecturaMax); Estado = Capacidad(Estado, checked(canales * 8));
                EstadoBandas = Capacidad(EstadoBandas, checked(canales * 32)); Recorte = Capacidad(Recorte, canales);
                Array.Clear(Buffer, 0, Buffer.Length); Array.Clear(Estado, 0, Estado.Length);
                Array.Clear(EstadoBandas, 0, EstadoBandas.Length); Array.Clear(Recorte, 0, Recorte.Length);
                Array.Clear(SumaBandas, 0, SumaBandas.Length);
            }
            internal SpectralTap Analizador(int indice, int rate)
            {
                SpectralTap tap = analisis[indice];
                if (tap == null || !tap.ReiniciaAnalisis(rate)) analisis[indice] = tap = new SpectralTap(rate, 1);
                return tap;
            }
            internal float[] Cuadro(int indice, int largo)
            {
                // No sobredimensionar: el bucle usa Length como tamano de ventana.
                if (cuadros[indice] == null || cuadros[indice].Length != largo) cuadros[indice] = new float[largo];
                return cuadros[indice];
            }
            internal System.Collections.Generic.List<float> Pares(int indice)
            {
                if (pares[indice] == null) pares[indice] = new System.Collections.Generic.List<float>(4096);
                pares[indice].Clear();
                return pares[indice];
            }
            internal MedidorBs1770 Sonoridad(int rate, double[] pesos)
            {
                MedidorBs1770 m = integrada;
                if (m == null || !m.Reinicia(rate, pesos)) integrada = m = new MedidorBs1770(rate, pesos);
                return m;
            }
            internal void Suelta()
            {
                // Retener hasta 64 KiB de bloques; canciones largas se analizan enteras, sin dejarlos en cache.
                if (integrada != null && !integrada.MemoriaRetenible(MaxBloquesRetenidos)) integrada = null;
                for (int i = 0; i < pares.Length; i++)
                    if (pares[i] != null)
                    {
                        // Un entrenamiento a fondo puede crecer mucho: medirlo entero, pero no retenerlo entero.
                        if (pares[i].Capacity > MaxParesRetenidos) pares[i] = null;
                        else pares[i].Clear();
                    }
                EnUso = false;
            }
        }
        private static TrabajoMedida TomaTrabajoMedida(int rate, int canales, bool tonal)
        {
            TrabajoMedida trabajo = trabajoMedidaDelHilo;
            if (canales > TrabajoMedida.MaxCanalesRetenidos || (trabajo != null && trabajo.EnUso))
                trabajo = new TrabajoMedida(); // formatos grandes/reentrada: temporales, sin limitar su analisis
            else if (trabajo == null) trabajoMedidaDelHilo = trabajo = new TrabajoMedida();
            trabajo.EnUso = true;
            try { trabajo.Prepara(rate, canales, tonal); return trabajo; }
            catch { trabajo.Suelta(); throw; }
        }

        // ==========================================================================================
        //  QUIEN MIDE QUE: una libreta con candado para que dos trabajadores no midan lo mismo.
        //
        //  En la rocola hay dos que miden canciones -sonoridad, duracion y huella tonal- y ninguno
        //  sabia del otro:
        //
        //    - EL REPASO DE LA BIBLIOTECA arma su lista de pendientes UNA SOLA VEZ, al arrancar, y en
        //      su bucle no vuelve a mirar. Esa lista vive HORAS: una biblioteca de unos miles de
        //      canciones tarda varias en repasarse entera.
        //    - EL CALENTADOR DE LA SIGUIENTE mide la cancion que viene, en cuanto alguien la pide.
        //
        //  O sea que CADA CANCION QUE UN CLIENTE PEDIA se media dos veces: una al pedirla, y otra un
        //  rato despues, cuando al repaso le llegaba su turno. Medir una de cuatro minutos cuesta
        //  335 ms aqui -y mas en el equipo del bar, que descomprime MP3 y no WAV-, y eso es trabajo
        //  tirado peleando con la musica justo cuando hay gente eligiendo.
        //
        //  Aqui se apunta QUIEN esta midiendo que, y QUE se midio ya.
        //
        //  EL REPASO A FONDO NO ENTRA EN LA SEGUNDA PARTE, y es a proposito: ese existe justamente
        //  para volver a medir, porque analiza la cancion COMPLETA en vez de por tramos y saca
        //  material para la IA. Lo que si respeta es la primera: no se pone a medir lo que otro esta
        //  midiendo en ese instante.
        //
        //  Las rutas que se guardan son LAS MISMAS de las demas tablas (pasan por Pool), asi que lo
        //  que ocupa esto es el hueco de la lista, no las cadenas: del mismo orden que durCache, que
        //  ya guarda una fila por cancion.
        // ==========================================================================================
        private static readonly object medidasCerrojo = new object();
        private static readonly System.Collections.Generic.HashSet<string> medidasEnCurso =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Collections.Generic.HashSet<string> medidasHechas =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Pide el turno para medir un archivo. Falso = no lo midas: o lo esta midiendo otro ahora
        // mismo, o ya se midio. Quien recibe verdadero TIENE QUE soltarlo despues, pase lo que pase.
        private static bool ReclamaMedida(string ruta, bool aFondo)
        {
            if (ruta == null) return false;
            lock (medidasCerrojo)
            {
                if (medidasEnCurso.Contains(ruta)) return false;
                if (!aFondo && medidasHechas.Contains(ruta)) return false;
                medidasEnCurso.Add(ruta);
                return true;
            }
        }
        private static void SueltaMedida(string ruta)
        {
            if (ruta == null) return;
            lock (medidasCerrojo) medidasEnCurso.Remove(ruta);
        }
        private static bool ReclamaMedidaEsperando(string ruta, bool aFondo, Func<bool> cancel)
        {
            if (ruta == null) return false;
            while (true)
            {
                if (cancel != null && cancel()) return false;
                lock (medidasCerrojo)
                    if (!medidasEnCurso.Contains(ruta)) return ReclamaMedida(ruta, aFondo);
                // Ocupado no significa completado: si el otro se cancela, este conserva su trabajo.
                System.Threading.Thread.Sleep(40);
            }
        }
        private static void ReiniciaMedidasHechas()
        {
            // No borrar turnos vivos: una generacion vieja aun debe soltar su propio archivo.
            lock (medidasCerrojo) medidasHechas.Clear();
        }
        // Se apunta cuando la medida ESTA PUESTA en las tablas, no cuando se pide. Un archivo roto no
        // da ni duracion ni sonoridad: darlo por medido seria no volver a intentarlo nunca.
        private static void MedidaPuesta(string ruta)
        {
            if (ruta == null) return;
            lock (medidasCerrojo) medidasHechas.Add(ruta);
        }
        private static void OlvidaMedidaHecha(string ruta)
        {
            if (ruta == null) return;
            lock (medidasCerrojo) medidasHechas.Remove(ruta);
        }
        private static bool YaSeMidio(string ruta)
        {
            if (ruta == null) return false;
            lock (medidasCerrojo) return medidasHechas.Contains(ruta);
        }
        private bool MeasureFileResponsive(string path, bool aFondo, Func<bool> cancel,
            out float rms, out float durSec, out float[] eqCurve)
        {
            Func<bool> previous = measureCancellation;
            // Solo en los trabajadores de analisis, nunca en el callback de reproduccion.
            // Ceder por bloques a las portadas visibles; comprobar cancelacion tambien durante la espera.
            string unit = UnidadDe(path);
            bool smallCpu = CargaTrabajo.Procesadores <= 2;
            measureCancellation = delegate
            {
                if (cancel != null && cancel()) return true;
                return !EsperaPortadas(unit, smallCpu, cancel);
            };
            try { return MeasureFile(path, aFondo, out rms, out durSec, out eqCurve); }
            finally { measureCancellation = previous; }
        }
        private static bool MeasurementCancelled()
        {
            Func<bool> cancel = measureCancellation;
            return cancel != null && cancel();
        }

        private sealed class AudioFftPlan
        {
            internal readonly float[] Window, Cos, Sin, SinNeg;
            internal readonly int[] Reverse;
            internal AudioFftPlan(int size)
            {
                Window = new float[size]; Reverse = new int[size];
                Cos = new float[size / 2]; Sin = new float[size / 2]; SinNeg = new float[size / 2];
                for (int i = 0; i < size; i++) Window[i] = (float)Math.Sqrt(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / size));
                for (int i = 1, j = 0; i < size; i++)
                {
                    int bit = size >> 1;
                    for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                    j |= bit; Reverse[i] = j;
                }
                for (int k = 0; k < size / 2; k++)
                {
                    Cos[k] = (float)Math.Cos(2.0 * Math.PI * k / size);
                    Sin[k] = (float)Math.Sin(2.0 * Math.PI * k / size);
                    SinNeg[k] = -Sin[k];
                }
            }
        }
        private static readonly object fftPlanLock = new object();
        private static AudioFftPlan fft2048, fft4096;
        // Las grandes (8192 a 65536), una de cada: el grave en mono y el subgrave en fase lineal (GraveMono.cs) las piden cancion tras cancion.
        private static readonly System.Collections.Generic.Dictionary<int, AudioFftPlan> fftGrandes = new System.Collections.Generic.Dictionary<int, AudioFftPlan>();
        // Cuatro piezas pequenas, invocadas desde el precalentador despues del primer cuadro.
        // Sin abrir audio ni modelos: no desplaza ni espera trabajo de reproduccion o portadas.
        internal static bool PrecalientaTablasAudio(int paso)
        {
            switch (paso)
            {
                case 0: return GetAudioFftPlan(2048) != null;
                case 1: return GetAudioFftPlan(4096) != null;
                case 2: return DetectorPicoReal.GananciaMaxima > 0;
                case 3: return RealceSobremuestreado.C != null;
                default: return true;
            }
        }
        private static AudioFftPlan GetAudioFftPlan(int size)
        {
            bool grande = size >= 8192 && size <= 65536 && (size & (size - 1)) == 0;
            if (size != 2048 && size != 4096 && !grande) return new AudioFftPlan(size);
            lock (fftPlanLock)
            {
                if (size == 2048) return fft2048 ?? (fft2048 = new AudioFftPlan(size));
                if (size == 4096) return fft4096 ?? (fft4096 = new AudioFftPlan(size));
                AudioFftPlan p;
                if (!fftGrandes.TryGetValue(size, out p)) { p = new AudioFftPlan(size); fftGrandes[size] = p; }
                return p;
            }
        }

    }
}
