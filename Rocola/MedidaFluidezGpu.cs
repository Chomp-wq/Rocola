// Diagnostico de fluidez GPU: memoria fija, sin ordenar ni escribir archivos por cuadro.
#if ROCOLA_GPU
using System;
using System.Globalization;
using System.Text;
namespace Rocola
{
    internal sealed class FluidezGpu
    {
        internal const int Capacidad = 512, CapacidadEpisodios = 32;
        private readonly double[] intervalos = new double[Capacidad], ordenados = new double[Capacidad];
        private readonly double[] preparar = new double[Capacidad], dibujar = new double[Capacidad], presentar = new double[Capacidad];
        private readonly double[] fueraRender = new double[Capacidad], agenda = new double[Capacidad];
        private readonly int[] gc0 = new int[Capacidad], gc1 = new int[Capacidad], gc2 = new int[Capacidad];
        internal struct Episodio
        {
            internal double Inicio, Intervalo, Preparar, Dibujar, Presentar, FueraRender, Agenda, Periodo;
            internal int Gc0, Gc1, Gc2;
        }
        private readonly Episodio[] episodios = new Episodio[CapacidadEpisodios];
        private int pos, n, episodioPos, episodioN;
        private double anterior = -1, finAnterior, prepAnterior, dibujoAnterior, presentAnterior, periodoAnterior;
        private bool finAnteriorMedido;
        private int gc0Anterior = -1, gc1Anterior = -1, gc2Anterior = -1;
        private double informe = -1e9, informeEpisodios = -1e9;
        internal double P95, P99, Maximo, PrepararMax, DibujarMax, PresentarMax, FueraRenderMax, AgendaMax;
        internal int Gc0Max, Gc1Max, Gc2Max, EpisodiosDescartados;
        // La ventana y el texto drenado no son toda la sesion. Solo escalares y una
        // muestra correlacionada fija: no perder pausas al girar los dos anillos.
        internal long IntervalosSesion, EpisodiosSesion, EpisodiosGcSesion, GeneracionSesion;
        internal double MaximoSesion;
        internal Episodio PeorIntervaloSesion;
        internal int Version;
        internal int EpisodiosPendientes { get { return episodioN; } }
        // Un intento no presentado no tiene las fases del ultimo cuadro exitoso. Conservar
        // su historial, pero no atribuirle el trabajo del intento fallido al hueco del anterior.
        internal void DescartaContinuidad() { CortaIntervalo(); }
        internal void Reinicia()
        {
            pos = n = episodioPos = episodioN = EpisodiosDescartados = 0;
            anterior = -1; gc0Anterior = gc1Anterior = gc2Anterior = -1;
            informe = informeEpisodios = -1e9;
            P95 = P99 = Maximo = PrepararMax = DibujarMax = PresentarMax = FueraRenderMax = AgendaMax = 0;
            Gc0Max = Gc1Max = Gc2Max = 0; Version++;
            IntervalosSesion = EpisodiosSesion = EpisodiosGcSesion = 0;
            GeneracionSesion++;
            MaximoSesion = 0; PeorIntervaloSesion = new Episodio();
        }
        // Compatibilidad con fixtures anteriores. Sin un End medido, el fin es una aproximacion
        // por suma de fases; no atribuir esa aproximacion a bloqueos del scheduler o del driver.
        internal void Toma(double inicio, double prep, double dibujo, double present)
        {
            if (!Finito(prep) || !Finito(dibujo) || !Finito(present)) { CortaIntervalo(); return; }
            prep = Math.Max(0, prep); dibujo = Math.Max(0, dibujo); present = Math.Max(0, present);
            TomaInterna(inicio, inicio + prep + dibujo + present, prep, dibujo, present, 1000d / 60d, 0, -1, -1, -1, false);
        }
        // GC se captura AL INICIO del cuadro actual. Agenda representa trabajo medido desde
        // el End anterior hasta este Start, no las tareas que todavia faltan del cuadro actual.
        // El intervalo Start anterior -> Start actual solo se conoce al llegar este cuadro:
        // sus fases son las ANTERIORES; asignarle las actuales produciria una falsa correlacion.
        internal void TomaCuadro(double inicio, double fin, double prep, double dibujo, double present,
            double periodoObjetivoMs, double agendaDesdeFinAnteriorMs, int gc0AlInicio, int gc1AlInicio, int gc2AlInicio)
        { TomaInterna(inicio, fin, prep, dibujo, present, periodoObjetivoMs, agendaDesdeFinAnteriorMs, gc0AlInicio, gc1AlInicio, gc2AlInicio, true); }
        private void TomaInterna(double inicio, double fin, double prep, double dibujo, double present,
            double periodoObjetivoMs, double agendaDesdeFinAnteriorMs, int gc0AlInicio, int gc1AlInicio, int gc2AlInicio, bool finMedido)
        {
            if (!Finito(inicio) || inicio < 0 || !Finito(fin) || fin < inicio ||
                !Finito(prep) || !Finito(dibujo) || !Finito(present) ||
                !Finito(periodoObjetivoMs) || periodoObjetivoMs <= 0 || !Finito(agendaDesdeFinAnteriorMs))
            { CortaIntervalo(); return; }
            prep = Math.Max(0, prep); dibujo = Math.Max(0, dibujo); present = Math.Max(0, present);
            if (anterior >= 0 && inicio >= anterior && (!finAnteriorMedido || inicio >= finAnterior))
            {
                double intervalo = inicio - anterior, fuera = Math.Max(0, inicio - finAnterior);
                double trabajoAgenda = Math.Min(fuera, Math.Max(0, agendaDesdeFinAnteriorMs));
                int d0 = DiferenciaGc(gc0Anterior, gc0AlInicio), d1 = DiferenciaGc(gc1Anterior, gc1AlInicio), d2 = DiferenciaGc(gc2Anterior, gc2AlInicio);
                intervalos[pos] = intervalo;
                preparar[pos] = prepAnterior; dibujar[pos] = dibujoAnterior; presentar[pos] = presentAnterior;
                fueraRender[pos] = fuera; agenda[pos] = trabajoAgenda;
                gc0[pos] = d0; gc1[pos] = d1; gc2[pos] = d2;
                pos = (pos + 1) % Capacidad; n = Math.Min(Capacidad, n + 1);
                IntervalosSesion++;
                bool peor = intervalo > MaximoSesion;
                bool tiron = intervalo >= Math.Max(20, 2 * periodoAnterior);
                if (peor || tiron)
                {
                    Episodio episodio = new Episodio();
                    episodio.Inicio = anterior; episodio.Intervalo = intervalo; episodio.Preparar = prepAnterior;
                    episodio.Dibujar = dibujoAnterior; episodio.Presentar = presentAnterior; episodio.FueraRender = fuera;
                    episodio.Agenda = trabajoAgenda; episodio.Periodo = periodoAnterior;
                    episodio.Gc0 = d0; episodio.Gc1 = d1; episodio.Gc2 = d2;
                    if (peor) { MaximoSesion = intervalo; PeorIntervaloSesion = episodio; }
                    if (tiron)
                    {
                        EpisodiosSesion++;
                        if (d0 > 0 || d1 > 0 || d2 > 0) EpisodiosGcSesion++; // coincidencia, NO duracion/causa GC
                        episodios[episodioPos] = episodio; episodioPos = (episodioPos + 1) % CapacidadEpisodios;
                        if (episodioN < CapacidadEpisodios) episodioN++;
                        else if (EpisodiosDescartados < int.MaxValue) EpisodiosDescartados++;
                    }
                }
            }
            anterior = inicio; finAnterior = fin; prepAnterior = prep; dibujoAnterior = dibujo;
            finAnteriorMedido = finMedido;
            presentAnterior = present; periodoAnterior = periodoObjetivoMs;
            gc0Anterior = gc0AlInicio; gc1Anterior = gc1AlInicio; gc2Anterior = gc2AlInicio;
        }
        private void CortaIntervalo()
        { anterior = -1; gc0Anterior = gc1Anterior = gc2Anterior = -1; }
        private static bool Finito(double valor) { return !double.IsNaN(valor) && !double.IsInfinity(valor); }
        private static int DiferenciaGc(int anteriorGc, int actualGc)
        { return anteriorGc < 0 || actualGc < anteriorGc ? 0 : actualGc - anteriorGc; }
        internal bool Resume(double ahora)
        {
            if (!Finito(ahora) || ahora < 0 || n == 0 || ahora - informe < 1000) return false;
            informe = ahora; Array.Copy(intervalos, ordenados, n); Array.Sort(ordenados, 0, n);
            P95 = ordenados[Math.Min(n - 1, (int)Math.Ceiling(n * .95) - 1)];
            P99 = ordenados[Math.Min(n - 1, (int)Math.Ceiling(n * .99) - 1)]; Maximo = ordenados[n - 1];
            PrepararMax = DibujarMax = PresentarMax = FueraRenderMax = AgendaMax = 0;
            Gc0Max = Gc1Max = Gc2Max = 0;
            for (int i = 0; i < n; i++)
            {
                PrepararMax = Math.Max(PrepararMax, preparar[i]); DibujarMax = Math.Max(DibujarMax, dibujar[i]);
                PresentarMax = Math.Max(PresentarMax, presentar[i]); FueraRenderMax = Math.Max(FueraRenderMax, fueraRender[i]);
                AgendaMax = Math.Max(AgendaMax, agenda[i]);
                Gc0Max = Math.Max(Gc0Max, gc0[i]); Gc1Max = Math.Max(Gc1Max, gc1[i]); Gc2Max = Math.Max(Gc2Max, gc2[i]);
            }
            Version++; return true;
        }
        // Lo tipico (la mediana) de cada parte de los cuadros guardados, y el p95 del intervalo. Para el medidor de Ctrl+F3:
        // la media se la lleva un solo cuadro raro; la mediana dice lo que cuesta un cuadro normal.
        internal void Medianas(out double intervalo, out double prep, out double dibujo, out double present, out double fuera, out double p95)
        {
            intervalo = prep = dibujo = present = fuera = p95 = 0;
            if (n == 0) return;
            intervalo = Percentil(intervalos, .5); p95 = Percentil(intervalos, .95);
            prep = Percentil(preparar, .5); dibujo = Percentil(dibujar, .5);
            present = Percentil(presentar, .5); fuera = Percentil(fueraRender, .5);
        }
        internal void Colas(out double p99, out double max)
        { p99 = n > 0 ? Percentil(intervalos, .99) : 0; max = n > 0 ? Percentil(intervalos, 1) : 0; }
        private double Percentil(double[] datos, double f)
        {
            Array.Copy(datos, ordenados, n); Array.Sort(ordenados, 0, n);
            return ordenados[Math.Max(0, Math.Min(n - 1, (int)Math.Ceiling(n * f) - 1))];
        }
        internal int Muestras { get { return n; } }
        internal string Resumen()
        {
            return "fluidez GPU: p95=" + Numero(P95) + " p99=" + Numero(P99) + " max=" + Numero(Maximo)
                + " ms; preparar/dibujar/presentar max=" + Numero(PrepararMax) + "/" + Numero(DibujarMax) + "/" + Numero(PresentarMax)
                + " ms; cuadros=" + n + "; fueraRender/agenda max=" + Numero(FueraRenderMax) + "/" + Numero(AgendaMax)
                + " ms; gc max=" + Gc0Max + "/" + Gc1Max + "/" + Gc2Max
                + "; sesion intervalos=" + IntervalosSesion + " episodios=" + EpisodiosSesion
                + " gc_coincidente=" + EpisodiosGcSesion + " max=" + Numero(MaximoSesion) + " ms";
        }
        // Solo esta salida y Resumen crean texto. Nunca se llama al escritor de diagnostico
        // desde TomaCuadro: el consumidor drena como maximo una vez por segundo.
        internal string DrenaEpisodios(double ahora)
        {
            if (!Finito(ahora) || ahora < 0 || episodioN == 0 || ahora - informeEpisodios < 1000) return string.Empty;
            informeEpisodios = ahora;
            StringBuilder texto = new StringBuilder(128 + episodioN * 200);
            texto.Append("episodios GPU: cantidad=").Append(episodioN).Append(" descartados=").Append(EpisodiosDescartados);
            int primero = (episodioPos - episodioN + CapacidadEpisodios) % CapacidadEpisodios;
            for (int i = 0; i < episodioN; i++)
            {
                Episodio episodio = episodios[(primero + i) % CapacidadEpisodios];
                texto.Append('\n').Append("tiron GPU: inicio=").Append(Numero(episodio.Inicio))
                    .Append(" intervalo=").Append(Numero(episodio.Intervalo))
                    .Append(" preparar=").Append(Numero(episodio.Preparar))
                    .Append(" dibujar=").Append(Numero(episodio.Dibujar))
                    .Append(" presentar=").Append(Numero(episodio.Presentar))
                    .Append(" fueraRender=").Append(Numero(episodio.FueraRender))
                    .Append(" agenda=").Append(Numero(episodio.Agenda))
                    .Append(" periodo=").Append(Numero(episodio.Periodo))
                    .Append(" gc=").Append(episodio.Gc0).Append('/').Append(episodio.Gc1).Append('/').Append(episodio.Gc2);
            }
            episodioN = EpisodiosDescartados = 0;
            return texto.ToString();
        }
        internal static string Numero(double n) { return n.ToString("0.0", CultureInfo.InvariantCulture); }
    }
}
#endif
