// ================================================================================================
//  EL DIAGNOSTICO COMPLETO DE LA ROCOLA (23-10-2026)
//  (Ctrl+F3 -> Imagen -> «Diagnostico completo», junto a «Medir este equipo»)
// ------------------------------------------------------------------------------------------------
//  «Medir este equipo» (Rendimiento.cs) contesta UNA pregunta: cuanto cuesta armar un cuadro en las seis pantallas
//  y que ahorros de Performance conviene encender. Esto contesta otra, que hace falta para decidir QUE MAS HAY QUE
//  OPTIMIZAR PARA EL A4: donde se pierde la fluidez de la rocola entera, escena por escena, y a quien le toca.
//
//  QUE HACE
//   - Recorre unas dieciseis escenas (rejilla quieta, hojeo rapido con portadas calientes y frias, letras, buscador, discos,
//     lista de canciones, atraccion, estado extendido con la estrella en 2D y en 3D, entrar y salir del estado extendido,
//     cambio de cancion, asistente y su guia, avisos y ficha, y dos «peor caso» con todo en Alto), cada una con su
//     calentamiento y su ventana de medida.
//   - A diferencia del medidor rapido, NO suelta el monitor: los cuadros se piden con el mismo ritmo que el bucle de verdad
//     (AgendaCuadro + RenderFrame, con la espera de mensajes entre uno y otro). Lo que se mide es lo que se ve y se oye en el
//     bar: huecos entre cuadros presentados, no «cuantos cuadros cabrian».
//   - Por escena: cuadros presentados; intervalos p50/p95/p99/max entre presentaciones; cuantos pasan de 1,5 y 2 periodos del
//     refresco; huecos de mas de 50 y de 100 ms (tirones), el peor y en que FASE del cuadro cayo (el libro de los peores
//     cuadros, LibroDeCuadros, encendido durante el diagnostico); fallos de presentacion y dispositivo perdido; CPU del
//     proceso, por hilo y del sistema; recolecciones de basura; memoria; memoria de texturas; cortes de sonido.
//   - Al final, la atribucion («que cuesta mas», por fase y por hilo) y una lista corta de candidatos a optimizar, con
//     numeros. Todo a un archivo de texto en la carpeta de datos (rocola_diagnostico_AAAA-MM-DD_HHMM.txt) y un resumen en la
//     pagina de Imagen.
//
//  QUE NO HACE (y la bateria pruebas/diagnostico.cs lo vigila)
//   - No guarda ni aplica NADA: ni configuracion, ni propuesta. No lleva interruptor propio (no es una preferencia).
//   - No reserva memoria por cuadro: los arreglos de la toma de muestras se crean una vez; ni una escritura a disco durante
//     las escenas (el informe se escribe al final).
//   - Sin diagnostico en marcha, cuesta cero en el bucle normal: un «if» sobre una referencia nula.
//   - No afirma causas. Un hueco «cae en una fase» porque esa fue la parte mas larga del tiempo entre dos presentaciones; el
//     numero de recolecciones de basura en un hueco es una COINCIDENCIA, no una duracion ni una causa; y los milisegundos de
//     cada fase son tiempo de reloj del hilo de la pantalla (si Windows le quita el procesador en medio, cuenta en la fase).
//   - No promete fluidez en la rocola real a partir de ninguna maquina que no sea la rocola real.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Rocola
{
    // ============================ LOS HILOS DE LA ROCOLA, CON NOMBRE (comun a los dos perfiles) ============================
    //
    // Los hilos de trabajo se anotan aqui al nacer («audio», "caratulas", "repaso"...). El diagnostico los mira por su
    // identificador NATIVO (GetCurrentThreadId, que existe desde Windows NT) y les pide el tiempo de procesador con
    // OpenThread + GetThreadTimes (tambien desde NT, funciona en XP). Anotarse cuesta una llamada al nacer el hilo; no hace nada
    // mas. Si algo falla (Linux de las pruebas, una API que no esta), no se anota y listo.
    internal static class HilosRocola
    {
        internal const int Max = 48;
        private static readonly object cerrojo = new object();
        private static readonly string[] nombres = new string[Max];
        private static readonly long[] ids = new long[Max];
        private static readonly Thread[] hilos = new Thread[Max];
        private static int cuantos;

        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("libc", EntryPoint = "syscall")] private static extern long LlamadaLinux(long numero);

        internal static bool EsUnix { get { int p = (int)Environment.OSVersion.Platform; return p == 4 || p == 6 || p == 128; } }

        // El identificador nativo del hilo que llama (el de GetThreadTimes, o el de /proc en Linux), o -1 si no se pudo.
        internal static long IdNativoActual()
        {
            try { return EsUnix ? LlamadaLinux(186) : (long)GetCurrentThreadId(); }
            catch { return -1; }
        }

        internal static void Nombra(string nombre)
        {
            try
            {
                long id = IdNativoActual();
                Thread yo = Thread.CurrentThread;
                lock (cerrojo)
                {
                    if (id >= 0)
                        for (int i = 0; i < cuantos; i++)
                            if (ids[i] == id && hilos[i] != null && hilos[i].IsAlive) { nombres[i] = nombre; hilos[i] = yo; return; }
                    int libre = -1;
                    for (int i = 0; i < cuantos; i++) if (hilos[i] == null || !hilos[i].IsAlive) { libre = i; break; }
                    if (libre < 0) { if (cuantos >= Max) return; libre = cuantos++; }
                    nombres[libre] = nombre; ids[libre] = id; hilos[libre] = yo;
                }
            }
            catch { }
        }

        // Los hilos anotados que siguen vivos y se pueden mirar (con identificador nativo).
        internal static void Copia(out string[] nom, out long[] id)
        {
            lock (cerrojo)
            {
                int n = 0;
                for (int i = 0; i < cuantos; i++) if (ids[i] >= 0 && hilos[i] != null && hilos[i].IsAlive) n++;
                nom = new string[n]; id = new long[n];
                int k = 0;
                for (int i = 0; i < cuantos; i++)
                    if (ids[i] >= 0 && hilos[i] != null && hilos[i].IsAlive) { nom[k] = nombres[i]; id[k] = ids[i]; k++; }
            }
        }
    }

#if ROCOLA_GPU
    // ============================ DELEGADOS (C# 3: sin Func/Action de varios argumentos) ============================
    internal delegate bool DiagBool();
    internal delegate void DiagVacio();
    internal delegate double DiagNum();
    internal delegate long DiagLargo();
    internal delegate string DiagTextoFn();
    internal delegate void DiagTextoAccion(string t);
    internal delegate string DiagPrepara();
    internal delegate void DiagPaso(int n);

    // ============================ LO QUE SE LEE DEL PROCESADOR Y DEL SISTEMA ============================
    //
    // Todo con try/catch y devolviendo -1 si la API no esta: GetSystemTimes existe desde XP SP1; OpenThread/GetThreadTimes desde
    // NT. En Linux (las pruebas) se lee /proc, que es lo unico que permite comprobar las cuentas sin Windows.
    internal static class LectorCpu
    {
        [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out long inactivo, out long nucleo, out long usuario);
        [DllImport("kernel32.dll")] private static extern IntPtr OpenThread(uint acceso, bool hereda, uint id);
        [DllImport("kernel32.dll")] private static extern bool GetThreadTimes(IntPtr h, out long creado, out long salido, out long nucleo, out long usuario);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
        private const uint ThreadQueryInformation = 0x0040;   // el unico permiso de hilo que conoce XP

        // El tiempo de procesador acumulado del proceso, en ms (suma todos los hilos, tambien los que ya murieron).
        internal static double ProcesoMs()
        {
            try
            {
                using (Process p = Process.GetCurrentProcess()) return p.TotalProcessorTime.TotalMilliseconds;
            }
            catch { return -1; }
        }

        // El tiempo de procesador ocupado y el total del sistema entero, acumulados, en ms. false si no se puede.
        internal static bool SistemaMs(out double ocupado, out double total)
        {
            ocupado = total = -1;
            try
            {
                if (HilosRocola.EsUnix)
                {
                    string l = File.ReadAllText("/proc/stat");
                    int fin = l.IndexOf('\n'); if (fin > 0) l = l.Substring(0, fin);
                    string[] p = l.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length < 8 || p[0] != "cpu") return false;
                    double[] v = new double[8];
                    for (int i = 0; i < 8; i++) v[i] = double.Parse(p[i + 1], CultureInfo.InvariantCulture);
                    double inactivo = v[3] + v[4];
                    double suma = 0; for (int i = 0; i < 8; i++) suma += v[i];
                    total = suma * 10.0; ocupado = (suma - inactivo) * 10.0;   // USER_HZ = 100: 10 ms por tic
                    return true;
                }
                long idle, kernel, user;
                if (!GetSystemTimes(out idle, out kernel, out user)) return false;
                total = (kernel + user) / 10000.0;                  // el tiempo de nucleo YA incluye el inactivo
                ocupado = (kernel - idle + user) / 10000.0;
                return true;
            }
            catch { ocupado = total = -1; return false; }
        }

        // El tiempo de procesador acumulado de UN hilo (por su identificador nativo), en ms; -1 si no se puede.
        internal static double HiloMs(long id)
        {
            if (id < 0) return -1;
            try
            {
                if (HilosRocola.EsUnix)
                {
                    string t = File.ReadAllText("/proc/self/task/" + id.ToString(CultureInfo.InvariantCulture) + "/stat");
                    int cierre = t.LastIndexOf(')');
                    if (cierre < 0) return -1;
                    string[] p = t.Substring(cierre + 1).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length < 13) return -1;
                    return (double.Parse(p[11], CultureInfo.InvariantCulture) + double.Parse(p[12], CultureInfo.InvariantCulture)) * 10.0;
                }
                IntPtr h = OpenThread(ThreadQueryInformation, false, (uint)id);
                if (h == IntPtr.Zero) return -1;
                try
                {
                    long c, e, k, u;
                    if (!GetThreadTimes(h, out c, out e, out k, out u)) return -1;
                    return (k + u) / 10000.0;
                }
                finally { CloseHandle(h); }
            }
            catch { return -1; }
        }
    }

    // Una fotografia del procesador, el sistema, el recolector y la memoria en un instante (se toma al principio y al final
    // de cada ventana de medida, nunca dentro).
    internal sealed class DiagInstantanea
    {
        internal double RelojMs, ProcesoMs = -1, SisOcupadoMs = -1, SisTotalMs = -1;
        internal string[] Hilos = new string[0]; internal long[] Ids = new long[0]; internal double[] HilosMs = new double[0];
        internal int Gc0, Gc1, Gc2;
        internal long PrivadaB = -1, TrabajoB = -1, MontonB = -1;

        internal static DiagInstantanea Toma(double relojMs)
        {
            DiagInstantanea s = new DiagInstantanea();
            s.RelojMs = relojMs;
            s.ProcesoMs = LectorCpu.ProcesoMs();
            double o, t; if (LectorCpu.SistemaMs(out o, out t)) { s.SisOcupadoMs = o; s.SisTotalMs = t; }
            try
            {
                HilosRocola.Copia(out s.Hilos, out s.Ids);
                s.HilosMs = new double[s.Ids.Length];
                for (int i = 0; i < s.Ids.Length; i++) s.HilosMs[i] = LectorCpu.HiloMs(s.Ids[i]);
            }
            catch { s.Hilos = new string[0]; s.Ids = new long[0]; s.HilosMs = new double[0]; }
            s.Gc0 = GC.CollectionCount(0); s.Gc1 = GC.CollectionCount(1); s.Gc2 = GC.CollectionCount(2);
            try
            {
                using (Process p = Process.GetCurrentProcess()) { s.PrivadaB = p.PrivateMemorySize64; s.TrabajoB = p.WorkingSet64; }
            }
            catch { }
            try { s.MontonB = GC.GetTotalMemory(false); } catch { }
            return s;
        }
    }

    // ============================ LO QUE SE GUARDA DE CADA ESCENA ============================
    internal sealed class DiagTiron
    {
        internal double Cuando, Hueco, Fuera, Espera, Trabajo;
        internal readonly double[] Fases = new double[LibroDeCuadros.NFases];
        internal int FaseDom, Banderas, Celdas, Subidas, Modo, Gc0, Gc1, Gc2, Fallos;
        internal DiagTiron Copia()
        {
            DiagTiron t = new DiagTiron();
            t.Cuando = Cuando; t.Hueco = Hueco; t.Fuera = Fuera; t.Espera = Espera; t.Trabajo = Trabajo; t.FaseDom = FaseDom; t.Banderas = Banderas;
            t.Celdas = Celdas; t.Subidas = Subidas; t.Modo = Modo; t.Gc0 = Gc0; t.Gc1 = Gc1; t.Gc2 = Gc2; t.Fallos = Fallos;
            Array.Copy(Fases, t.Fases, Fases.Length);
            return t;
        }
    }
    internal sealed class DiagHiloCpu { internal string Nombre; internal double PctNucleo; }

    internal sealed class DiagResultado
    {
        internal string Id = "", Nombre = "", Que = "", Estado = "medida", Motivo = "", Notas = "", FondoIni = "", FondoFin = "", Musica = "", Portadas = "";
        internal double DuraMs, PeriodoRefMs; internal int Hz;
        internal int Cuadros, Fallos, PerdidasDispositivo, Desborde, LatidosManuales, PasosDados;
        internal double Fps, P50, P95, P99, Max, Medio, ProcP50, PresP50;
        internal int Sobre15, Sobre2, Huecos50, Huecos100, Huecos50ConGc;
        internal double[] FasesMs = new double[DiagCaptura.NSlots];
        internal int[] Huecos50PorFase = new int[DiagCaptura.NSlots], Huecos100PorFase = new int[DiagCaptura.NSlots];
        internal double[] HuecosMsPorFase = new double[DiagCaptura.NSlots];
        internal int[] Huecos50PorFaseYBandera = new int[DiagCaptura.NSlots * (DiagCaptura.NBanderas + 1)];
        internal DiagTiron[] Peores = new DiagTiron[0];
        internal double CpuProcesoPct = -1, CpuNucleos = -1, CpuSistemaPct = -1, CpuOtrosPct = -1;
        internal List<DiagHiloCpu> Hilos = new List<DiagHiloCpu>();
        internal int Gc0, Gc1, Gc2;
        internal long PrivadaMb = -1, PrivadaDeltaMb = 0, TrabajoMb = -1, MontonMb = -1, TexIniMb = -1, TexFinMb = -1;
        internal long Cortes = -1, Tardios = -1, MaxTardioMs = -1;
        internal double InicioMs;   // en el reloj del diagnostico
    }

    // ============================ LA TOMA DE MUESTRAS (cero reservas por cuadro) ============================
    //
    // GpuPanel.RenderFrame le da cada cuadro presentado (si hay una toma enchufada: un «if» sobre una referencia). Los arreglos
    // se crean UNA vez con la toma y se reusan en cada escena; Cuadro() solo hace cuentas con numeros. Texto y objetos solo al
    // cerrar la escena (Cierra), fuera de la ventana de medida.
    //
    // EL HUECO que se cuenta es el tiempo entre dos cuadros PRESENTADOS (de un Present al siguiente): lo que la persona ve. Se reparte
    // en lo que paso FUERA del dibujo (mensajes, audio, temporizadores, otros hilos, el sistema: entre el Present anterior y el
    // inicio de este cuadro) y las once fases de este cuadro. La «fase dominante» es la mas larga de las doce; es una atribucion
    // de tiempo de reloj, no una causa.
    internal sealed class DiagCaptura
    {
        internal const int Capacidad = 24000, MaxPeores = 12, SlotFuera = LibroDeCuadros.NFases, SlotEspera = LibroDeCuadros.NFases + 1, NSlots = LibroDeCuadros.NFases + 2, NBanderas = 10;
        internal const double UmbralPeorMs = 33.0;
        internal static readonly string[] NombresSlots = {
            "pre", "rejilla", "portadas", "cubos", "estado", "vivos", "interfaz", "adornos", "adelanto", "dibujo", "presentar", "fuera", "espera" };
        internal static readonly string[] Significado = {
            "preparar el cuadro (reloj, tarjeta, regulador de caratulas)",
            "rejilla de discos (celdas que se hornean o suben)",
            "portadas (texturas de celda, expulsiones, recortes)",
            "cubos y contorno del disco elegido",
            "estado extendido (construir y subir su panel)",
            "elementos vivos (ventana del buscador, viaje de portada, aviso, atraccion)",
            "interfaz (subir las barras, botones y la franja de abajo)",
            "adornos (cintas de texto, nieblas, vineta, asistente, estrella, cielo vivo)",
            "adelanto de portadas (precarga de la pagina que viene)",
            "dibujo (las ordenes que se mandan al controlador)",
            "presentar (Present: la espera a la tarjeta o al monitor)",
            "fuera del dibujo, ocupado (mensajes, audio, temporizadores, otros hilos o el sistema)",
            "espera ociosa a que toque el siguiente cuadro (no cuesta nada: el hilo duerme)" };

        private readonly double[] intervalos = new double[Capacidad], proc = new double[Capacidad], pres = new double[Capacidad], orden = new double[Capacidad];
        private readonly DiagTiron[] peores = new DiagTiron[MaxPeores];
        private readonly double[] sumaFases = new double[NSlots], msPorFase = new double[NSlots];
        private readonly int[] h50PorFase = new int[NSlots], h100PorFase = new int[NSlots], h50FaseBandera = new int[NSlots * (NBanderas + 1)];
        private int nIntervalos, nPeores, cuadros, fallos, desborde, sobre15, sobre2, h50, h100, h50ConGc, fallosPendientes;
        private double finPrevio = -1, t0, periodo = 1000.0 / 60.0, sumaHuecos, ociosoAcum;
        private int gcPrev0, gcPrev1, gcPrev2;
        internal volatile bool Activo;
        internal int CuadrosVistos { get { return cuadros; } }

        internal DiagCaptura() { for (int i = 0; i < MaxPeores; i++) peores[i] = new DiagTiron(); }

        // Empieza una ventana de medida. 'periodoRefMs' es el periodo del refresco de la pantalla.
        internal void Empieza(double ahoraMs, double periodoRefMs)
        {
            Activo = false;
            nIntervalos = nPeores = cuadros = fallos = desborde = sobre15 = sobre2 = h50 = h100 = h50ConGc = fallosPendientes = 0;
            finPrevio = -1; t0 = ahoraMs; sumaHuecos = 0; ociosoAcum = 0;
            periodo = periodoRefMs > 1 ? periodoRefMs : 1000.0 / 60.0;
            for (int i = 0; i < NSlots; i++) { sumaFases[i] = 0; msPorFase[i] = 0; h50PorFase[i] = 0; h100PorFase[i] = 0; }
            for (int i = 0; i < h50FaseBandera.Length; i++) h50FaseBandera[i] = 0;
            gcPrev0 = GC.CollectionCount(0); gcPrev1 = GC.CollectionCount(1); gcPrev2 = GC.CollectionCount(2);
            Activo = true;
        }

        // Un cuadro presentado. 'libro' (si hay) trae las once fases de ESTE cuadro; null = sin fases (todo cae en «fuera»).
        // NO reserva nada: solo cuentas con numeros.
        internal void Cuadro(double inicio, double fin, double prep, double dibujo, double present, LibroDeCuadros libro)
        {
            if (!Activo) return;
            cuadros++;
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            if (finPrevio >= 0)
            {
                double hueco = fin - finPrevio; if (hueco < 0) hueco = 0;
                double fuera = inicio - finPrevio; if (fuera < 0) fuera = 0;
                // De ese «fuera», lo que el bucle espero a proposito a que tocara el siguiente cuadro (hilo dormido) no cuesta nada: se aparta.
                double espera = ociosoAcum < fuera ? ociosoAcum : fuera; fuera -= espera;
                int k = nIntervalos;
                if (k < Capacidad) { intervalos[k] = hueco; proc[k] = prep + dibujo; pres[k] = present; nIntervalos = k + 1; }
                else desborde++;
                sumaHuecos += hueco;
                if (hueco > 1.5 * periodo) sobre15++;
                if (hueco > 2.0 * periodo) sobre2++;
                double[] f = libro != null ? libro.Fases : null;
                if (f != null) for (int i = 0; i < LibroDeCuadros.NFases; i++) sumaFases[i] += f[i];
                sumaFases[SlotFuera] += fuera; sumaFases[SlotEspera] += espera;
                if (hueco > 50.0)
                {
                    h50++; bool grande = hueco > 100.0; if (grande) h100++;
                    int dom = SlotFuera; double mejor = fuera;
                    if (f != null) for (int i = 0; i < LibroDeCuadros.NFases; i++) if (f[i] > mejor) { mejor = f[i]; dom = i; }
                    h50PorFase[dom]++; if (grande) h100PorFase[dom]++; msPorFase[dom] += hueco;
                    int b = libro != null ? libro.Banderas : 0;
                    if (b == 0) h50FaseBandera[dom * (NBanderas + 1) + NBanderas]++;
                    else for (int i = 0; i < NBanderas; i++) if ((b & (1 << i)) != 0) h50FaseBandera[dom * (NBanderas + 1) + i]++;
                    if (g0 != gcPrev0 || g1 != gcPrev1 || g2 != gcPrev2) h50ConGc++;
                }
                if (hueco >= UmbralPeorMs)
                {
                    int destino = -1;
                    if (nPeores < MaxPeores) destino = nPeores++;
                    else
                    {
                        double menor = double.MaxValue;
                        for (int i = 0; i < MaxPeores; i++) if (peores[i].Hueco < menor) { menor = peores[i].Hueco; destino = i; }
                        if (!(hueco > menor)) destino = -1;
                    }
                    if (destino >= 0)
                    {
                        DiagTiron t = peores[destino];
                        t.Cuando = fin - t0; t.Hueco = hueco; t.Fuera = fuera; t.Espera = espera; t.Trabajo = fin - inicio;
                        int dom = SlotFuera; double mejor = fuera;
                        if (f != null) for (int i = 0; i < LibroDeCuadros.NFases; i++) { t.Fases[i] = f[i]; if (f[i] > mejor) { mejor = f[i]; dom = i; } }
                        else for (int i = 0; i < LibroDeCuadros.NFases; i++) t.Fases[i] = 0;
                        t.FaseDom = dom;
                        if (libro != null) { t.Banderas = libro.Banderas; t.Celdas = libro.Celdas; t.Subidas = libro.Subidas; t.Modo = libro.Modo; }
                        else { t.Banderas = 0; t.Celdas = 0; t.Subidas = 0; t.Modo = 0; }
                        t.Gc0 = g0 - gcPrev0; t.Gc1 = g1 - gcPrev1; t.Gc2 = g2 - gcPrev2; t.Fallos = fallosPendientes;
                    }
                }
            }
            fallosPendientes = 0; ociosoAcum = 0;
            finPrevio = fin; gcPrev0 = g0; gcPrev1 = g1; gcPrev2 = g2;
        }

        // Un cuadro que NO se presento (dispositivo perdido, escena sin abrir...). Cuenta como fallo y queda apuntado para el
        // proximo hueco; no corta la continuidad (para quien mira, la pantalla se quedo quieta ese rato).
        internal void Fallo() { if (!Activo) return; fallos++; fallosPendientes++; }

        // El bucle espero 'ms' a proposito (hilo dormido, esperando que toque el siguiente cuadro): no es trabajo ni tiron.
        internal void Ocioso(double ms) { if (Activo && ms > 0) ociosoAcum += ms; }

        private static double Pct(double[] ord, int n, double f)
        { return n == 0 ? 0 : ord[Math.Max(0, Math.Min(n - 1, (int)Math.Ceiling(n * f) - 1))]; }

        // Cierra la ventana y rellena el resultado (aqui si se crean objetos: ya no se esta midiendo).
        internal void Cierra(double ahoraMs, DiagResultado r)
        {
            Activo = false;
            r.DuraMs = ahoraMs - t0;
            r.PeriodoRefMs = periodo; r.Cuadros = cuadros; r.Fallos = fallos; r.Desborde = desborde;
            r.Fps = r.DuraMs > 0 ? cuadros * 1000.0 / r.DuraMs : 0;
            int n = nIntervalos;
            Array.Copy(intervalos, orden, n); Array.Sort(orden, 0, n);
            r.P50 = Pct(orden, n, .5); r.P95 = Pct(orden, n, .95); r.P99 = Pct(orden, n, .99); r.Max = n > 0 ? orden[n - 1] : 0;
            r.Medio = n > 0 ? sumaHuecos / n : 0;
            Array.Copy(proc, orden, n); Array.Sort(orden, 0, n); r.ProcP50 = Pct(orden, n, .5);
            Array.Copy(pres, orden, n); Array.Sort(orden, 0, n); r.PresP50 = Pct(orden, n, .5);
            r.Sobre15 = sobre15; r.Sobre2 = sobre2; r.Huecos50 = h50; r.Huecos100 = h100; r.Huecos50ConGc = h50ConGc;
            Array.Copy(sumaFases, r.FasesMs, NSlots);
            Array.Copy(h50PorFase, r.Huecos50PorFase, NSlots); Array.Copy(h100PorFase, r.Huecos100PorFase, NSlots);
            Array.Copy(msPorFase, r.HuecosMsPorFase, NSlots);
            Array.Copy(h50FaseBandera, r.Huecos50PorFaseYBandera, h50FaseBandera.Length);
            DiagTiron[] lista = new DiagTiron[nPeores];
            for (int i = 0; i < nPeores; i++) lista[i] = peores[i].Copia();
            Array.Sort(lista, delegate (DiagTiron a, DiagTiron b) { return b.Hueco.CompareTo(a.Hueco); });
            r.Peores = lista;
        }
    }

    // ============================ LAS ESCENAS Y EL ANFITRION ============================
    internal sealed class DiagEscenaDef
    {
        internal string Id = "", Nombre = "", Que = "";
        internal int CalentaMs = 1000, MedidaMs = 8000, PasoMs = 0, AsentarMaxMs = 0;
        internal DiagPrepara Prepara;       // null = lista; texto = por que se salta
        internal DiagPaso Paso;             // un paso de lo que haria una persona (cada PasoMs)
        internal DiagVacio PorIteracion;    // cada vuelta del bucle (p. ej. mantener abierto el estado extendido)
        internal DiagBool Asentada;         // para escenas que tardan en montarse (estado extendido): cuando ya esta
        internal DiagTextoFn Notas;         // notas de la escena (al acabar)
        internal DiagVacio Limpia;          // deja la escena como estaba
        internal string MotivoNoAsienta = "la escena no llego a montarse";
    }

    // Lo que el diagnostico necesita del mundo. Form1 lo llena con la tarjeta de verdad; las pruebas con un reloj virtual.
    internal sealed class DiagAnfitrion
    {
        internal DiagBool Cuadro;               // pide UN turno del bucle de cuadros (dibuja si toca, espera si no); true si presento
        internal DiagVacio Bombea;              // deja correr los mensajes (de ahi come el audio)
        internal DiagBool Interrumpido;         // alguien toco algo: hay que dejarlo todo
        internal DiagNum Ahora;                 // reloj, en ms
        internal DiagNum PeriodoRefMs;          // periodo del refresco de la pantalla
        internal DiagVacio ReiniciaMedida;      // reinicia las cuentas de la tarjeta y del regulador de caratulas
        internal DiagVacio Pulso;               // sin musica de verdad: un latido de mentira (puede ser null)
        internal DiagLargo Latidos;             // contador de latidos del temporizador de animacion (para saber si se queda sin latir)
        internal DiagVacio Latido;              // un latido a mano
        internal DiagTextoAccion Aviso;         // el aviso de progreso
        internal DiagLargo Cortes, Tardios, MaxTardioMs, TexMb, Perdidas;   // cortes de sonido, avisos tardios, peor aviso, memoria de texturas, dispositivo perdido
        internal DiagTextoFn Fondo;             // trabajo de fondo que hay (repaso, miniaturas...)
        internal DiagTextoFn Musica;            // «real», «muda del diagnostico» o «sin musica»
        internal DiagTextoFn Portadas;          // lo que cuenta el regulador de caratulas en esta escena
    }

    // ============================ EL MOTOR ============================
    internal sealed class DiagnosticoCompleto
    {
        internal readonly DiagCaptura Captura = new DiagCaptura();
        internal readonly DiagAnfitrion Host;
        internal readonly List<DiagEscenaDef> Escenas = new List<DiagEscenaDef>();
        internal readonly List<DiagResultado> Resultados = new List<DiagResultado>();
        internal bool Cancelado; internal string MotivoCancelacion = "";
        internal double PresupuestoMs = 7.0 * 60 * 1000;   // el diagnostico entero no pasa de esto (objetivo: 3 a 6 minutos)
        internal double InicioMs, FinMs;
        internal int Nucleos = Math.Max(1, Environment.ProcessorCount);
        internal int Hz = 60;

        internal DiagnosticoCompleto(DiagAnfitrion host) { Host = host; }

        private double Ahora() { return Host.Ahora != null ? Host.Ahora() : 0; }
        private static long L(DiagLargo f) { if (f == null) return -1; try { return f(); } catch { return -1; } }
        private static string T(DiagTextoFn f) { if (f == null) return ""; try { return f() ?? ""; } catch { return ""; } }

        internal void Corre()
        {
            InicioMs = Ahora();
            HilosRocola.Nombra("pantalla (interfaz)");   // este hilo es el de la pantalla: se anota para el reparto por hilo
            double periodoRef = Host.PeriodoRefMs != null ? Host.PeriodoRefMs() : 1000.0 / 60.0;
            if (periodoRef > 1) Hz = (int)Math.Round(1000.0 / periodoRef);
            for (int i = 0; i < Escenas.Count; i++)
            {
                if (Cancelado) break;
                DiagEscenaDef e = Escenas[i];
                double gastado = Ahora() - InicioMs;
                if (gastado + e.CalentaMs + e.MedidaMs > PresupuestoMs)
                {
                    DiagResultado s = NuevoResultado(e);
                    s.Estado = "saltada"; s.Motivo = "se acabo el tiempo del diagnostico (" + (int)(PresupuestoMs / 1000) + " s)";
                    Resultados.Add(s);
                    continue;
                }
                if (Host.Aviso != null)
                {
                    double quedan = 0;
                    for (int j = i; j < Escenas.Count; j++) quedan += Escenas[j].CalentaMs + Escenas[j].MedidaMs + 600;
                    try { Host.Aviso("Diagnostico " + (i + 1) + "/" + Escenas.Count + ": " + e.Nombre + " (quedan unos " + Math.Max(1, (int)Math.Round(quedan / 60000.0)) + " min; cualquier boton lo cancela)"); } catch { }
                }
                Resultados.Add(CorreEscena(e));
            }
            FinMs = Ahora();
        }

        private DiagResultado NuevoResultado(DiagEscenaDef e)
        {
            DiagResultado r = new DiagResultado();
            r.Id = e.Id; r.Nombre = e.Nombre; r.Que = e.Que; r.Hz = Hz;
            r.PeriodoRefMs = Host.PeriodoRefMs != null ? Host.PeriodoRefMs() : 1000.0 / 60.0;
            return r;
        }

        private void Limpia(DiagEscenaDef e) { if (e.Limpia != null) { try { e.Limpia(); } catch { } } }

        private DiagResultado CorreEscena(DiagEscenaDef e)
        {
            DiagResultado r = NuevoResultado(e);
            r.InicioMs = Ahora() - InicioMs;
            string motivo = null;
            try { motivo = e.Prepara != null ? e.Prepara() : null; }
            catch (Exception ex) { motivo = "no se pudo montar la escena: " + ex.Message; }
            if (motivo != null)
            {
                r.Estado = "saltada"; r.Motivo = motivo;
                Limpia(e);
                return r;
            }
            try
            {
                bool interrumpida;
                Ventana(e, r, out interrumpida);
                if (interrumpida) { Cancelado = true; MotivoCancelacion = "alguien uso la rocola durante \"" + e.Nombre + "\""; if (r.Estado == "medida") r.Estado = "cancelada"; }
            }
            catch (Exception ex)
            {
                r.Estado = "saltada"; r.Motivo = "fallo durante la escena: " + ex.Message;
                Captura.Activo = false;
            }
            finally { Limpia(e); }
            try { if (e.Notas != null) r.Notas = (r.Notas.Length > 0 ? r.Notas + " " : "") + (e.Notas() ?? ""); } catch { }
            return r;
        }

        // El bucle de una escena: calentamiento (sin contar), ventana de medida, y el cierre con las fotografias de antes y despues.
        private void Ventana(DiagEscenaDef e, DiagResultado r, out bool interrumpida)
        {
            interrumpida = false;
            double t0 = Ahora();
            double periodoRef = r.PeriodoRefMs;
            bool asentada = e.Asentada == null; double tAsentada = 0;
            bool contando = false; double tIni = 0;
            DiagInstantanea ini = null;
            long cortes0 = -1, tardios0 = -1, perdidas0 = -1;
            double ultPaso = t0 - e.PasoMs; int paso = 0;
            long latidoVisto = Host.Latidos != null ? L(Host.Latidos) : 0; double tLatido = t0;
            r.Musica = T(Host.Musica);
            while (true)
            {
                double ahora = Ahora(); double va = ahora - t0;
                if (Host.Interrumpido != null && Host.Interrumpido()) { interrumpida = true; break; }
                if (!contando)
                {
                    if (!asentada)
                    {
                        bool ya = false;
                        try { ya = e.Asentada(); } catch { }
                        if (ya) { asentada = true; tAsentada = va; }
                        else if (va >= e.AsentarMaxMs)
                        {
                            r.Estado = "saltada"; r.Motivo = e.MotivoNoAsienta;
                            return;
                        }
                    }
                    if (asentada && va >= tAsentada + e.CalentaMs)
                    {
                        // Aqui empieza la ventana: las fotografias se toman justo antes del primer cuadro que cuenta.
                        r.FondoIni = T(Host.Fondo);
                        r.TexIniMb = L(Host.TexMb);
                        cortes0 = L(Host.Cortes); tardios0 = L(Host.Tardios); perdidas0 = L(Host.Perdidas);
                        if (Host.ReiniciaMedida != null) Host.ReiniciaMedida();
                        ini = DiagInstantanea.Toma(Ahora());
                        Captura.Empieza(Ahora(), periodoRef);
                        contando = true; tIni = Ahora();
                    }
                }
                else if (ahora - tIni >= e.MedidaMs) break;
                if (e.PasoMs > 0 && ahora - ultPaso >= e.PasoMs && e.Paso != null)
                {
                    ultPaso = ahora;
                    try { e.Paso(paso); r.PasosDados++; } catch { }
                    paso++;
                }
                if (e.PorIteracion != null) { try { e.PorIteracion(); } catch { } }
                if (Host.Pulso != null) { try { Host.Pulso(); } catch { } }
                // El temporizador de animacion late solo (WM_TIMER entre cuadro y cuadro). Si se queda sin latir, se le da a mano.
                if (Host.Latidos != null)
                {
                    long lat = L(Host.Latidos);
                    if (lat != latidoVisto) { latidoVisto = lat; tLatido = ahora; }
                    else if (ahora - tLatido >= 120 && Host.Latido != null) { tLatido = ahora; try { Host.Latido(); } catch { } if (contando) r.LatidosManuales++; }
                }
                if (Host.Cuadro != null) Host.Cuadro();
                if (Host.Bombea != null) Host.Bombea();
            }
            if (!contando) { r.Estado = "cancelada"; r.Motivo = "se cancelo antes de empezar a medir"; return; }
            // El cierre: primero la captura (que es lo mas sensible al tiempo), despues el resto.
            double fin = Ahora();
            Captura.Cierra(fin, r);
            DiagInstantanea acaba = DiagInstantanea.Toma(Ahora());
            CalculaCpu(ini, acaba, Nucleos, r);
            r.Gc0 = acaba.Gc0 - ini.Gc0; r.Gc1 = acaba.Gc1 - ini.Gc1; r.Gc2 = acaba.Gc2 - ini.Gc2;
            if (acaba.PrivadaB >= 0) { r.PrivadaMb = acaba.PrivadaB >> 20; if (ini.PrivadaB >= 0) r.PrivadaDeltaMb = (acaba.PrivadaB - ini.PrivadaB) / (1024 * 1024); }
            if (acaba.TrabajoB >= 0) r.TrabajoMb = acaba.TrabajoB >> 20;
            if (acaba.MontonB >= 0) r.MontonMb = acaba.MontonB >> 20;
            r.TexFinMb = L(Host.TexMb);
            long c1 = L(Host.Cortes), t1 = L(Host.Tardios), p1 = L(Host.Perdidas);
            r.Cortes = (cortes0 >= 0 && c1 >= 0) ? Math.Max(0, c1 - cortes0) : -1;
            r.Tardios = (tardios0 >= 0 && t1 >= 0) ? Math.Max(0, t1 - tardios0) : -1;
            r.MaxTardioMs = L(Host.MaxTardioMs);
            r.PerdidasDispositivo = (perdidas0 >= 0 && p1 >= 0) ? (int)Math.Max(0, p1 - perdidas0) : 0;
            r.FondoFin = T(Host.Fondo);
            r.Portadas = T(Host.Portadas);
        }

        // Los porcentajes de procesador entre dos fotografias. Del proceso: sobre el total de nucleos; de cada hilo: sobre UN nucleo
        // (un hilo no puede pasar de 100). El resto del proceso es lo que no se pudo poner a ningun hilo con nombre.
        internal static void CalculaCpu(DiagInstantanea a, DiagInstantanea b, int nucleos, DiagResultado r)
        {
            double dt = b.RelojMs - a.RelojMs;
            if (!(dt > 0)) return;
            if (nucleos < 1) nucleos = 1;
            double dp = -1;
            if (a.ProcesoMs >= 0 && b.ProcesoMs >= a.ProcesoMs)
            {
                dp = b.ProcesoMs - a.ProcesoMs;
                r.CpuNucleos = dp / dt; r.CpuProcesoPct = 100.0 * dp / (dt * nucleos);
            }
            if (a.SisTotalMs >= 0 && b.SisTotalMs > a.SisTotalMs)
            {
                r.CpuSistemaPct = 100.0 * (b.SisOcupadoMs - a.SisOcupadoMs) / (b.SisTotalMs - a.SisTotalMs);
                if (r.CpuSistemaPct < 0) r.CpuSistemaPct = 0; else if (r.CpuSistemaPct > 100) r.CpuSistemaPct = 100;
                if (r.CpuProcesoPct >= 0) r.CpuOtrosPct = Math.Max(0, r.CpuSistemaPct - r.CpuProcesoPct);
            }
            r.Hilos = new List<DiagHiloCpu>();
            double sumaNombrados = 0;
            for (int i = 0; i < b.Ids.Length; i++)
            {
                int j = -1;
                for (int k = 0; k < a.Ids.Length; k++) if (a.Ids[k] == b.Ids[i]) { j = k; break; }
                if (j < 0 || a.HilosMs[j] < 0 || b.HilosMs[i] < a.HilosMs[j]) continue;
                double pct = 100.0 * (b.HilosMs[i] - a.HilosMs[j]) / dt;
                DiagHiloCpu h = null;
                for (int k = 0; k < r.Hilos.Count; k++) if (r.Hilos[k].Nombre == b.Hilos[i]) { h = r.Hilos[k]; break; }
                if (h == null) { h = new DiagHiloCpu(); h.Nombre = b.Hilos[i]; r.Hilos.Add(h); }
                h.PctNucleo += pct; sumaNombrados += pct;
            }
            if (dp >= 0)
            {
                double resto = 100.0 * dp / dt - sumaNombrados;
                if (resto < 0) resto = 0;
                DiagHiloCpu h = new DiagHiloCpu(); h.Nombre = "(resto del proceso: hilos sin nombre, del sistema de .NET y de la tarjeta)"; h.PctNucleo = resto;
                r.Hilos.Add(h);
            }
            r.Hilos.Sort(delegate (DiagHiloCpu x, DiagHiloCpu y) { return y.PctNucleo.CompareTo(x.PctNucleo); });
        }
    }

    // ============================ LO QUE SE SABE DEL EQUIPO ============================
    internal static class DiagEquipo
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MemEx { public uint Length, MemoryLoad; public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual; }
        [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MemEx m);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DispositivoPantalla
        {
            public int Tam;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Nombre;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Cadena;
            public int Estado;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Clave;
        }
        [DllImport("user32.dll", CharSet = CharSet.Ansi)] private static extern bool EnumDisplayDevices(string dispositivo, uint num, ref DispositivoPantalla dd, uint flags);

        internal static string Cpu()
        {
            string nombre = "", mhz = "";
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0"))
                {
                    if (k != null)
                    {
                        object v = k.GetValue("ProcessorNameString"); if (v != null) nombre = v.ToString().Trim();
                        object m = k.GetValue("~MHz"); if (m != null) mhz = m.ToString();
                    }
                }
            }
            catch { }
            if (nombre.Length == 0 && HilosRocola.EsUnix)
            {
                try
                {
                    foreach (string l in File.ReadAllLines("/proc/cpuinfo"))
                        if (l.StartsWith("model name")) { int i = l.IndexOf(':'); if (i > 0) nombre = l.Substring(i + 1).Trim(); break; }
                }
                catch { }
            }
            if (nombre.Length == 0) nombre = "(no se pudo leer el nombre)";
            return nombre + (mhz.Length > 0 ? " (" + mhz + " MHz)" : "") + ", " + Environment.ProcessorCount + " nucleo(s)/hilo(s)";
        }

        internal static string Memoria(out long totalMb, out long libreMb)
        {
            totalMb = libreMb = -1;
            try
            {
                MemEx m = new MemEx(); m.Length = (uint)Marshal.SizeOf(typeof(MemEx));
                if (GlobalMemoryStatusEx(ref m)) { totalMb = (long)(m.TotalPhys >> 20); libreMb = (long)(m.AvailPhys >> 20); return totalMb + " MB en total, " + libreMb + " MB libres (carga " + m.MemoryLoad + " %)"; }
            }
            catch { }
            if (HilosRocola.EsUnix)
            {
                try
                {
                    long tot = -1, lib = -1;
                    foreach (string l in File.ReadAllLines("/proc/meminfo"))
                    {
                        if (l.StartsWith("MemTotal:")) tot = long.Parse(l.Substring(9).Replace("kB", "").Trim(), CultureInfo.InvariantCulture) / 1024;
                        else if (l.StartsWith("MemAvailable:")) lib = long.Parse(l.Substring(13).Replace("kB", "").Trim(), CultureInfo.InvariantCulture) / 1024;
                    }
                    totalMb = tot; libreMb = lib;
                    if (tot > 0) return tot + " MB en total, " + lib + " MB libres";
                }
                catch { }
            }
            return "(no se pudo leer)";
        }

        internal static string Tarjeta()
        {
            try
            {
                DispositivoPantalla d = new DispositivoPantalla(); d.Tam = Marshal.SizeOf(typeof(DispositivoPantalla));
                for (uint i = 0; i < 8; i++)
                {
                    d = new DispositivoPantalla(); d.Tam = Marshal.SizeOf(typeof(DispositivoPantalla));
                    if (!EnumDisplayDevices(null, i, ref d, 0)) break;
                    if ((d.Estado & 4) != 0 && !string.IsNullOrEmpty(d.Cadena)) return d.Cadena.Trim();   // DISPLAY_DEVICE_PRIMARY_DEVICE
                }
            }
            catch { }
            return "(el sistema no dijo el nombre del adaptador)";
        }
    }

    // ============================ EL INFORME ============================
    internal sealed class DiagMeta
    {
        internal DateTime Inicio = DateTime.Now;
        internal double DuracionMs;
        internal string Version = "", Equipo = "", Ajustes = "", Condiciones = "", Observaciones = "";
        internal bool Cancelado; internal string MotivoCancelacion = "";
        internal int Hz = 60, Nucleos = 1;
        internal bool SoloPrueba = false;   // las pruebas: se dice en la cabecera
    }

    internal sealed class DiagTotales
    {
        internal int Escenas, Medidas, Cuadros, Huecos50, Huecos100, Huecos50ConGc, Fallos, Perdidas;
        internal double DuraMs;
        internal readonly int[] H50PorFase = new int[DiagCaptura.NSlots], H100PorFase = new int[DiagCaptura.NSlots];
        internal readonly double[] FasesMs = new double[DiagCaptura.NSlots];
        internal readonly int[] H50FaseBandera = new int[DiagCaptura.NSlots * (DiagCaptura.NBanderas + 1)];
        internal readonly Dictionary<string, double> CpuHiloMs = new Dictionary<string, double>();
        internal readonly Dictionary<string, double> CpuHiloMax = new Dictionary<string, double>();
        internal readonly Dictionary<string, string> CpuHiloMaxEscena = new Dictionary<string, string>();
        internal long Cortes, Tardios;
        internal int EscenasConRepaso;
    }

    internal static class DiagInforme
    {
        internal const int Conservar = 10, AnchoNombre = 40;
        private static readonly string[] Cortos = { "pre", "rejill", "portad", "cubos", "estado", "vivos", "interf", "adorn", "adelan", "dibujo", "presen", "fuera", "espera" };

        internal static string N(double v, string fmt) { return v.ToString(fmt, CultureInfo.InvariantCulture); }
        private static string N1(double v) { return N(v, "0.0"); }
        private static string Pad(string s, int n) { if (s == null) s = ""; return s.Length >= n ? s.Substring(0, n) : s.PadRight(n); }
        private static string PadI(string s, int n) { if (s == null) s = ""; return s.Length >= n ? s : s.PadLeft(n); }
        private static string Pct(double v) { return v < 0 ? "-" : N1(v); }

        internal static string Veredicto(DiagResultado r)
        {
            if (r.Estado != "medida") return r.Estado;
            double hz = r.PeriodoRefMs > 0 ? 1000.0 / r.PeriodoRefMs : 60;
            if (r.Huecos100 > 0) return "TIRONES FUERTES";
            if (r.Huecos50 > 0) return "tirones";
            if (r.Fps < 0.85 * hz) return "lenta";
            if (r.P99 > 2.0 * r.PeriodoRefMs) return "p99 alto";
            return "bien";
        }

        internal static DiagTotales Totales(List<DiagResultado> rs)
        {
            DiagTotales t = new DiagTotales();
            for (int i = 0; i < rs.Count; i++)
            {
                DiagResultado r = rs[i]; t.Escenas++;
                if (r.Estado == "saltada" || r.Cuadros == 0) continue;
                t.Medidas++; t.Cuadros += r.Cuadros; t.DuraMs += r.DuraMs; t.Huecos50 += r.Huecos50; t.Huecos100 += r.Huecos100;
                t.Huecos50ConGc += r.Huecos50ConGc; t.Fallos += r.Fallos; t.Perdidas += r.PerdidasDispositivo;
                if (r.Cortes > 0) t.Cortes += r.Cortes; if (r.Tardios > 0) t.Tardios += r.Tardios;
                if (r.FondoIni.Length > 0 || r.FondoFin.Length > 0) t.EscenasConRepaso++;
                for (int s = 0; s < DiagCaptura.NSlots; s++) { t.H50PorFase[s] += r.Huecos50PorFase[s]; t.H100PorFase[s] += r.Huecos100PorFase[s]; t.FasesMs[s] += r.FasesMs[s]; }
                for (int s = 0; s < t.H50FaseBandera.Length; s++) t.H50FaseBandera[s] += r.Huecos50PorFaseYBandera[s];
                for (int h = 0; h < r.Hilos.Count; h++)
                {
                    string n = r.Hilos[h].Nombre; double ms = r.Hilos[h].PctNucleo * r.DuraMs / 100.0;
                    double antes; t.CpuHiloMs.TryGetValue(n, out antes); t.CpuHiloMs[n] = antes + ms;
                    double mx; if (!t.CpuHiloMax.TryGetValue(n, out mx) || r.Hilos[h].PctNucleo > mx) { t.CpuHiloMax[n] = r.Hilos[h].PctNucleo; t.CpuHiloMaxEscena[n] = r.Nombre; }
                }
            }
            return t;
        }

        // Los candidatos a optimizar, en orden, cada uno con sus numeros. No afirman causas.
        internal static List<string> Candidatos(List<DiagResultado> rs, DiagMeta m)
        {
            List<string> c = new List<string>();
            DiagTotales t = Totales(rs);
            if (t.Medidas == 0) { c.Add("No se midio ninguna escena: no hay nada que atribuir. Mira \"Escenas saltadas\"."); return c; }
            // 1) la fase donde cayeron los tirones de mas de 50 ms
            if (t.Huecos50 >= 3)
            {
                int top = 0; for (int s = 1; s <= DiagCaptura.SlotFuera; s++) if (t.H50PorFase[s] > t.H50PorFase[top]) top = s;
                double pc = 100.0 * t.H50PorFase[top] / t.Huecos50;
                string bandera = ""; int mejorB = -1, mejorN = 0;
                for (int b = 0; b <= DiagCaptura.NBanderas; b++) { int n = t.H50FaseBandera[top * (DiagCaptura.NBanderas + 1) + b]; if (n > mejorN) { mejorN = n; mejorB = b; } }
                if (mejorB >= 0 && mejorN * 2 >= t.H50PorFase[top])
                    bandera = mejorB == DiagCaptura.NBanderas ? ", sobre todo sin banderas del cuadro" : ", sobre todo con la bandera \"" + LibroDeCuadros.NombresBanderas[mejorB] + "\" (" + mejorN + " de " + t.H50PorFase[top] + ")";
                string donde = ""; int mejorE = -1, nE = 0;
                for (int i = 0; i < rs.Count; i++) if (rs[i].Huecos50PorFase.Length > top && rs[i].Huecos50PorFase[top] > nE) { nE = rs[i].Huecos50PorFase[top]; mejorE = i; }
                if (mejorE >= 0) donde = " La escena con mas: \"" + rs[mejorE].Nombre + "\" (" + nE + ").";
                c.Add("El " + N(pc, "0") + " % de los huecos de mas de 50 ms (" + t.H50PorFase[top] + " de " + t.Huecos50 + ") cayo en la fase \"" + DiagCaptura.NombresSlots[top]
                    + "\" = " + DiagCaptura.Significado[top] + bandera + "." + donde + (top == DiagCaptura.SlotFuera ? " Fuera del dibujo pueden ser el audio en el hilo de la pantalla, los temporizadores, otros hilos o el sistema; el reparto por hilo de abajo ayuda a separarlos." : ""));
            }
            else if (t.Huecos50 > 0) c.Add("Hubo " + t.Huecos50 + " hueco(s) de mas de 50 ms en total: pocos para atribuirlos a una fase con seguridad (ver la lista de tirones).");
            else c.Add("Ningun hueco de mas de 50 ms en ninguna escena medida (" + t.Medidas + " escenas, " + t.Cuadros + " cuadros presentados). Es esta maquina y este rato; no es una promesa para la rocola real ni para siempre.");
            // 2) las escenas que no dan el refresco o tienen colas largas
            DiagResultado peor = null;
            for (int i = 0; i < rs.Count; i++) if (rs[i].Estado != "saltada" && rs[i].Cuadros > 0 && (peor == null || rs[i].P99 > peor.P99)) peor = rs[i];
            if (peor != null && (peor.P99 > 2.0 * peor.PeriodoRefMs || peor.Fps < 0.85 * (1000.0 / peor.PeriodoRefMs)))
                c.Add("La escena de cola mas larga: \"" + peor.Nombre + "\": p99 " + N1(peor.P99) + " ms (" + N(peor.P99 / peor.PeriodoRefMs, "0.0") + " periodos de " + N1(peor.PeriodoRefMs) + " ms), max " + N1(peor.Max)
                    + " ms, " + N(peor.Fps, "0") + " cuadros/s presentados de " + N(1000.0 / peor.PeriodoRefMs, "0") + " posibles.");
            // 3) las fases con mas coste medio en la escena mas cargada
            DiagResultado cara = null; double costeCara = 0;
            for (int i = 0; i < rs.Count; i++)
            {
                DiagResultado r = rs[i]; if (r.Estado == "saltada" || r.Cuadros < 10) continue;
                double w = 0; for (int s = 0; s < LibroDeCuadros.FPresentar; s++) w += r.FasesMs[s];   // sin presentar ni fuera
                double porCuadro = w / r.Cuadros;
                if (porCuadro > costeCara) { costeCara = porCuadro; cara = r; }
            }
            if (cara != null && costeCara > 0.5)
            {
                int a = -1, b = -1;
                for (int s = 0; s < LibroDeCuadros.FPresentar; s++) { if (a < 0 || cara.FasesMs[s] > cara.FasesMs[a]) { b = a; a = s; } else if (b < 0 || cara.FasesMs[s] > cara.FasesMs[b]) b = s; }
                string dos = "\"" + DiagCaptura.NombresSlots[a] + "\" " + N(cara.FasesMs[a] / cara.Cuadros, "0.00") + " ms/cuadro" + (b >= 0 && cara.FasesMs[b] > 0 ? " y \"" + DiagCaptura.NombresSlots[b] + "\" " + N(cara.FasesMs[b] / cara.Cuadros, "0.00") + " ms/cuadro" : "");
                c.Add("En la escena que mas trabajo de CPU arma (\"" + cara.Nombre + "\", " + N(costeCara, "0.0") + " ms por cuadro sin contar Present): " + dos + ".");
            }
            // 4) CPU
            DiagResultado cpuMax = null;
            for (int i = 0; i < rs.Count; i++) if (rs[i].Estado != "saltada" && rs[i].CpuNucleos >= 0 && (cpuMax == null || rs[i].CpuNucleos > cpuMax.CpuNucleos)) cpuMax = rs[i];
            if (cpuMax != null)
            {
                string hilo = "";
                if (cpuMax.Hilos.Count > 0) hilo = " El hilo que mas: \"" + cpuMax.Hilos[0].Nombre + "\" con " + N(cpuMax.Hilos[0].PctNucleo, "0") + " % de un nucleo.";
                bool casi = cpuMax.CpuNucleos >= 0.8 * m.Nucleos;
                c.Add("CPU: el pico fue \"" + cpuMax.Nombre + "\" con " + N(cpuMax.CpuNucleos, "0.00") + " nucleos de " + m.Nucleos + " (" + N(cpuMax.CpuProcesoPct, "0") + " % del procesador)."
                    + (casi ? " Queda muy poco margen en esa escena." : "") + hilo);
            }
            // 5) otros procesos
            DiagResultado ajeno = null;
            for (int i = 0; i < rs.Count; i++) if (rs[i].CpuOtrosPct >= 10 && (ajeno == null || rs[i].CpuOtrosPct > ajeno.CpuOtrosPct)) ajeno = rs[i];
            if (ajeno != null)
                c.Add("Otro programa usa procesador: " + N(ajeno.CpuOtrosPct, "0") + " % del total del equipo fuera de la rocola en \"" + ajeno.Nombre + "\" (sistema " + N(ajeno.CpuSistemaPct, "0") + " %, rocola " + N(ajeno.CpuProcesoPct, "0")
                    + " %). Revisar antivirus, indexadores o servicios; eso no es de la rocola.");
            // 6) audio
            if (t.Cortes > 0 || t.Tardios > 0)
            {
                DiagResultado a = null;
                for (int i = 0; i < rs.Count; i++) if ((rs[i].Cortes > 0 || rs[i].Tardios > 0) && (a == null || rs[i].Cortes + rs[i].Tardios > a.Cortes + a.Tardios)) a = rs[i];
                c.Add("Sonido: " + t.Cortes + " corte(s) del hilo de la musica y " + t.Tardios + " aviso(s) tardio(s) de la salida durante el diagnostico" + (a != null ? "; el peor rato: \"" + a.Nombre + "\"" : "") + ". Mirar tambien [Cortes de sonido] en rocola_error.txt.");
            }
            // 7) fallos de presentacion
            if (t.Fallos > 0 || t.Perdidas > 0)
                c.Add("Presentacion: " + t.Fallos + " cuadro(s) sin presentar y " + t.Perdidas + " vez(veces) que se perdio el dispositivo grafico. Ese tiempo cuenta como hueco, no como cuadro.");
            // 8) memoria
            long privMax = 0; DiagResultado pm = null; long texMin = long.MaxValue; DiagResultado tm = null;
            for (int i = 0; i < rs.Count; i++)
            {
                if (rs[i].PrivadaMb > privMax) { privMax = rs[i].PrivadaMb; pm = rs[i]; }
                if (rs[i].TexFinMb > 0 && rs[i].TexFinMb < texMin) { texMin = rs[i].TexFinMb; tm = rs[i]; }
            }
            if (privMax >= 1400) c.Add("Memoria: la privada llego a " + privMax + " MB (\"" + pm.Nombre + "\"): en un proceso de 32 bits el techo es de 2 GB.");
            if (tm != null && texMin < 96) c.Add("Memoria de texturas de la tarjeta: solo " + texMin + " MB disponibles al final de \"" + tm.Nombre + "\".");
            // 9) el repaso/preanalisis
            if (t.EscenasConRepaso > 0)
                c.Add("El repaso o las miniaturas estaban activos en " + t.EscenasConRepaso + " de " + t.Medidas + " escenas: para separar su efecto, repetir el diagnostico con la biblioteca ya repasada (la cifra de tirones puede incluirlo).");
            // 10) recolecciones: SIEMPRE como coincidencia
            if (t.Huecos50ConGc > 0)
                c.Add(t.Huecos50ConGc + " de los " + t.Huecos50 + " huecos de mas de 50 ms coincidieron con alguna recoleccion de basura (.NET). Es una COINCIDENCIA de contadores: no dice cuanto duro la recoleccion ni que fuera la causa.");
            if (c.Count > 8) c.RemoveRange(8, c.Count - 8);
            return c;
        }

        internal static string Construye(DiagMeta m, List<DiagResultado> rs)
        {
            StringBuilder sb = new StringBuilder(16384);
            string nl = "\r\n";
            DiagTotales tot = Totales(rs);
            string raya = new string('=', 118);
            sb.Append(raya).Append(nl);
            sb.Append("ROCOLA - DIAGNOSTICO COMPLETO").Append(nl);
            sb.Append(raya).Append(nl);
            sb.Append("Fecha: ").Append(m.Inicio.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append("   Duracion: ").Append(N(m.DuracionMs / 1000.0, "0")).Append(" s   Programa: ").Append(m.Version).Append(nl);
            sb.Append("Este archivo se copia entero y se pasa tal cual. Los numeros llevan punto decimal; los tiempos estan en milisegundos salvo que se diga otra cosa.").Append(nl);
            if (m.SoloPrueba) sb.Append("OJO: ESTE INFORME ES DE UNA PRUEBA AUTOMATICA (tarjeta imitada, Linux/Mono): las cifras NO son de la rocola ni de Windows XP.").Append(nl);
            if (m.Cancelado) sb.Append("OJO: EL DIAGNOSTICO SE CANCELO (").Append(m.MotivoCancelacion).Append("). Solo estan las escenas que llegaron a medirse.").Append(nl);
            sb.Append(nl);
            sb.Append("== 1. EQUIPO ==").Append(nl).Append(m.Equipo).Append(nl);
            sb.Append("== 2. AJUSTES VIGENTES (los de la rocola al empezar) ==").Append(nl).Append(m.Ajustes).Append(nl);
            sb.Append("== 3. CONDICIONES DE LA MEDIDA ==").Append(nl).Append(m.Condiciones).Append(nl);

            // 4) la tabla
            sb.Append("== 4. RESUMEN POR ESCENA ==").Append(nl);
            sb.Append("Intervalos = tiempo entre dos cuadros PRESENTADOS seguidos. Periodo del refresco: ").Append(N1(rs.Count > 0 ? rs[0].PeriodoRefMs : 1000.0 / Math.Max(1, m.Hz))).Append(" ms (").Append(m.Hz).Append(" Hz). >1.5x y >2x = cuantos intervalos pasan de 1,5 y 2 periodos.").Append(nl);
            sb.Append(Pad("Escena", AnchoNombre)).Append(PadI("cuad", 6)).Append(PadI("fps", 5)).Append(PadI("p50", 6)).Append(PadI("p95", 6)).Append(PadI("p99", 7)).Append(PadI("max", 7))
              .Append(PadI(">1.5x", 6)).Append(PadI(">2x", 5)).Append(PadI(">50", 5)).Append(PadI(">100", 5)).Append(PadI("fall", 5)).Append("  veredicto").Append(nl);
            for (int i = 0; i < rs.Count; i++)
            {
                DiagResultado r = rs[i];
                if (r.Estado == "saltada") { sb.Append(Pad(r.Nombre, AnchoNombre)).Append("  (saltada: ").Append(r.Motivo).Append(")").Append(nl); continue; }
                sb.Append(Pad(r.Nombre, AnchoNombre)).Append(PadI(r.Cuadros.ToString(CultureInfo.InvariantCulture), 6)).Append(PadI(N(r.Fps, "0"), 5)).Append(PadI(N1(r.P50), 6)).Append(PadI(N1(r.P95), 6))
                  .Append(PadI(N1(r.P99), 7)).Append(PadI(N1(r.Max), 7)).Append(PadI(r.Sobre15.ToString(CultureInfo.InvariantCulture), 6)).Append(PadI(r.Sobre2.ToString(CultureInfo.InvariantCulture), 5))
                  .Append(PadI(r.Huecos50.ToString(CultureInfo.InvariantCulture), 5)).Append(PadI(r.Huecos100.ToString(CultureInfo.InvariantCulture), 5)).Append(PadI(r.Fallos.ToString(CultureInfo.InvariantCulture), 5))
                  .Append("  ").Append(Veredicto(r)).Append(nl);
            }
            sb.Append(nl);
            sb.Append("Procesador y fases por cuadro (medianas): prep+dibujo = trabajo del procesador al armar y mandar el cuadro; present = lo que espera Present.").Append(nl);
            sb.Append(Pad("Escena", AnchoNombre)).Append(PadI("prep+dib", 9)).Append(PadI("present", 8)).Append(PadI("medio", 7)).Append(PadI("cpu%", 6)).Append(PadI("nucl", 6)).Append(PadI("sist%", 6)).Append(PadI("otros%", 7)).Append(PadI("GC0/1/2", 10)).Append(PadI("priv MB", 8)).Append(PadI("tex MB", 7)).Append(nl);
            for (int i = 0; i < rs.Count; i++)
            {
                DiagResultado r = rs[i]; if (r.Estado == "saltada" || r.Cuadros == 0) continue;
                sb.Append(Pad(r.Nombre, AnchoNombre)).Append(PadI(N1(r.ProcP50), 9)).Append(PadI(N1(r.PresP50), 8)).Append(PadI(N1(r.Medio), 7)).Append(PadI(Pct(r.CpuProcesoPct < 0 ? -1 : r.CpuProcesoPct), 6))
                  .Append(PadI(r.CpuNucleos < 0 ? "-" : N(r.CpuNucleos, "0.00"), 6)).Append(PadI(Pct(r.CpuSistemaPct), 6)).Append(PadI(Pct(r.CpuOtrosPct), 7))
                  .Append(PadI(r.Gc0 + "/" + r.Gc1 + "/" + r.Gc2, 10)).Append(PadI(r.PrivadaMb < 0 ? "-" : r.PrivadaMb.ToString(CultureInfo.InvariantCulture), 8)).Append(PadI(r.TexFinMb < 0 ? "-" : r.TexFinMb.ToString(CultureInfo.InvariantCulture), 7)).Append(nl);
            }
            sb.Append(nl);
            sb.Append("Detalle de cada escena:").Append(nl);
            for (int i = 0; i < rs.Count; i++)
            {
                DiagResultado r = rs[i];
                sb.Append(" - ").Append(r.Nombre).Append(": ").Append(r.Que).Append(nl);
                if (r.Estado == "saltada") { sb.Append("     SALTADA: ").Append(r.Motivo).Append(nl); continue; }
                sb.Append("     ").Append(N(r.DuraMs / 1000.0, "0.0")).Append(" s medidos").Append(r.Musica.Length > 0 ? "; musica: " + r.Musica : "").Append(r.Estado == "cancelada" ? "; CANCELADA (datos parciales)" : "")
                  .Append(r.PasosDados > 0 ? "; pasos de la escena: " + r.PasosDados : "").Append(r.LatidosManuales > 0 ? "; latidos dados a mano: " + r.LatidosManuales : "")
                  .Append(r.Desborde > 0 ? "; intervalos sin guardar (tope): " + r.Desborde : "").Append(nl);
                if (r.Notas.Length > 0) sb.Append("     ").Append(r.Notas).Append(nl);
                if (r.Portadas.Length > 0) sb.Append("     Portadas: ").Append(r.Portadas).Append(nl);
                if (r.FondoIni.Length > 0 || r.FondoFin.Length > 0)
                    sb.Append("     Trabajo de fondo -> al empezar: ").Append(r.FondoIni.Length > 0 ? r.FondoIni : "nada").Append("; al acabar: ").Append(r.FondoFin.Length > 0 ? r.FondoFin : "nada").Append(nl);
                if (r.Cortes >= 0 || r.Tardios >= 0)
                    sb.Append("     Sonido en la escena: cortes del hilo de la musica ").Append(r.Cortes < 0 ? "?" : r.Cortes.ToString(CultureInfo.InvariantCulture)).Append(", avisos tardios ").Append(r.Tardios < 0 ? "?" : r.Tardios.ToString(CultureInfo.InvariantCulture)).Append(nl);
                if (r.PerdidasDispositivo > 0) sb.Append("     Dispositivo grafico perdido ").Append(r.PerdidasDispositivo).Append(" vez(veces).").Append(nl);
            }
            sb.Append(nl);

            // 5) los tirones
            sb.Append("== 5. TIRONES (huecos de mas de 33 ms entre cuadros presentados), con la fase donde cayeron ==").Append(nl);
            sb.Append("Cada linea: hueco = fuera del dibujo (ocupado; entre el Present anterior y el inicio del cuadro) + las fases del cuadro. La fase dominante es la mas larga; no es una causa.").Append(nl);
            bool hubo = false;
            for (int i = 0; i < rs.Count; i++)
            {
                DiagResultado r = rs[i]; if (r.Peores.Length == 0) continue; hubo = true;
                sb.Append(" ").Append(r.Nombre).Append("  (>50 ms: ").Append(r.Huecos50).Append(", >100 ms: ").Append(r.Huecos100).Append(")").Append(nl);
                for (int k = 0; k < r.Peores.Length && k < 5; k++)
                {
                    DiagTiron t = r.Peores[k];
                    sb.Append("   #").Append(k + 1).Append(" a los ").Append(N(t.Cuando / 1000.0, "0.0")).Append(" s: hueco ").Append(N1(t.Hueco)).Append(" ms = fuera(ocupado) ").Append(N1(t.Fuera)).Append(t.Espera >= 0.05 ? " espera(ociosa) " + N1(t.Espera) : "");
                    for (int f = 0; f < LibroDeCuadros.NFases; f++) if (t.Fases[f] >= 0.05) sb.Append(' ').Append(LibroDeCuadros.NombresFases[f]).Append(' ').Append(N1(t.Fases[f]));
                    sb.Append(" | fase dominante: ").Append(DiagCaptura.NombresSlots[t.FaseDom]).Append(" | banderas: ");
                    bool alguna = false;
                    for (int b = 0; b < LibroDeCuadros.NombresBanderas.Length; b++) if ((t.Banderas & (1 << b)) != 0) { sb.Append(alguna ? "," : "").Append(LibroDeCuadros.NombresBanderas[b]); alguna = true; }
                    if (!alguna) sb.Append("ninguna");
                    sb.Append(" | celdas ").Append(t.Celdas).Append(", texturas subidas ").Append(t.Subidas).Append(" | recolecciones en el hueco ").Append(t.Gc0).Append('/').Append(t.Gc1).Append('/').Append(t.Gc2).Append(" (coincidencia, no causa)");
                    if (t.Fallos > 0) sb.Append(" | cuadros sin presentar antes: ").Append(t.Fallos);
                    sb.Append(nl);
                }
            }
            if (!hubo) sb.Append(" Ningun hueco de mas de 33 ms en las escenas medidas.").Append(nl);
            sb.Append(nl);

            // 6) CPU
            sb.Append("== 6. PROCESADOR: DEL PROCESO, DEL SISTEMA Y POR HILO ==").Append(nl);
            sb.Append("Proceso = % de todos los nucleos (").Append(m.Nucleos).Append("). Hilos = % de UN nucleo (un hilo no pasa de 100). 'Otros' = lo que usa el resto del equipo, ajeno a la rocola.").Append(nl);
            for (int i = 0; i < rs.Count; i++)
            {
                DiagResultado r = rs[i]; if (r.Estado == "saltada" || r.Cuadros == 0) continue;
                sb.Append(" ").Append(Pad(r.Nombre, AnchoNombre)).Append(" proceso ").Append(r.CpuProcesoPct < 0 ? "sin dato" : N(r.CpuProcesoPct, "0.0") + " % (" + N(r.CpuNucleos, "0.00") + " nucleos)")
                  .Append("; sistema ").Append(r.CpuSistemaPct < 0 ? "sin dato" : N(r.CpuSistemaPct, "0.0") + " %").Append("; otros ").Append(r.CpuOtrosPct < 0 ? "sin dato" : N(r.CpuOtrosPct, "0.0") + " %").Append(nl);
                if (r.Hilos.Count > 0)
                {
                    sb.Append("      hilos:");
                    for (int h = 0; h < r.Hilos.Count && h < 8; h++) sb.Append(' ').Append(r.Hilos[h].Nombre.StartsWith("(resto") ? "(resto)" : r.Hilos[h].Nombre).Append(' ').Append(N(r.Hilos[h].PctNucleo, "0.0")).Append(h + 1 < r.Hilos.Count && h < 7 ? ";" : "");
                    sb.Append(nl);
                }
            }
            sb.Append(" Suma de todas las escenas, por hilo (media sobre el tiempo medido y la escena donde mas):").Append(nl);
            List<KeyValuePair<string, double>> orden = new List<KeyValuePair<string, double>>(tot.CpuHiloMs);
            orden.Sort(delegate (KeyValuePair<string, double> a, KeyValuePair<string, double> b) { return b.Value.CompareTo(a.Value); });
            if (orden.Count == 0) sb.Append("   (sin datos por hilo en este equipo)").Append(nl);
            for (int i = 0; i < orden.Count; i++)
            {
                string n = orden[i].Key; string nombre = n.StartsWith("(resto") ? "(resto del proceso)" : n;
                sb.Append("   ").Append(Pad(nombre, 40)).Append(PadI(tot.DuraMs > 0 ? N(100.0 * orden[i].Value / tot.DuraMs, "0.0") : "-", 7)).Append(" % de un nucleo de media; maximo ").Append(N(tot.CpuHiloMax[n], "0.0")).Append(" % en \"").Append(tot.CpuHiloMaxEscena[n]).Append("\"").Append(nl);
            }
            sb.Append(nl);

            // 7) atribucion
            sb.Append("== 7. ATRIBUCION: QUE CUESTA MAS ==").Append(nl);
            sb.Append("Tiempo por cuadro y por fase (ms de reloj del hilo de la pantalla, media de la ventana). 'fuera' = ocupado fuera del dibujo; 'espera' = hilo dormido esperando el siguiente cuadro (no cuesta).").Append(nl);
            sb.Append(Pad("Escena", AnchoNombre));
            for (int s = 0; s < DiagCaptura.NSlots; s++) sb.Append(PadI(Cortos[s], 7));
            sb.Append(nl);
            for (int i = 0; i < rs.Count; i++)
            {
                DiagResultado r = rs[i]; if (r.Estado == "saltada" || r.Cuadros == 0) continue;
                sb.Append(Pad(r.Nombre, AnchoNombre));
                for (int s = 0; s < DiagCaptura.NSlots; s++) sb.Append(PadI(N(r.FasesMs[s] / Math.Max(1, r.Cuadros - 1), "0.00"), 7));
                sb.Append(nl);
            }
            double totalFases = 0; for (int s = 0; s < DiagCaptura.NSlots; s++) totalFases += tot.FasesMs[s];
            sb.Append(Pad("TODAS (% del tiempo entre cuadros)", AnchoNombre));
            for (int s = 0; s < DiagCaptura.NSlots; s++) sb.Append(PadI(totalFases > 0 ? N(100.0 * tot.FasesMs[s] / totalFases, "0.0") : "-", 7));
            sb.Append(nl).Append(nl);
            sb.Append("Huecos de mas de 50 ms por fase (total ").Append(tot.Huecos50).Append(", de mas de 100 ms: ").Append(tot.Huecos100).Append("):").Append(nl);
            if (tot.Huecos50 == 0) sb.Append("   ninguno.").Append(nl);
            for (int s = 0; s < DiagCaptura.NSlots; s++)
                if (tot.H50PorFase[s] > 0)
                    sb.Append("   ").Append(Pad(DiagCaptura.NombresSlots[s], 10)).Append(PadI(tot.H50PorFase[s].ToString(CultureInfo.InvariantCulture), 5)).Append("  (").Append(N(100.0 * tot.H50PorFase[s] / tot.Huecos50, "0")).Append(" %; de mas de 100 ms: ").Append(tot.H100PorFase[s]).Append(")  ").Append(DiagCaptura.Significado[s]).Append(nl);
            if (tot.Huecos50 > 0) sb.Append("   recolecciones de basura coincidentes con esos huecos: ").Append(tot.Huecos50ConGc).Append(" de ").Append(tot.Huecos50).Append(" (coincidencia de contadores, no causa ni duracion)").Append(nl);
            sb.Append(nl);

            // 8) candidatos
            sb.Append("== 8. CANDIDATOS A OPTIMIZAR PARA EL A4 (justificados con los numeros de arriba) ==").Append(nl);
            List<string> cand = Candidatos(rs, m);
            for (int i = 0; i < cand.Count; i++) sb.Append(" ").Append(i + 1).Append(". ").Append(cand[i]).Append(nl);
            sb.Append(nl);

            // 9) saltadas
            sb.Append("== 9. ESCENAS SALTADAS ==").Append(nl);
            bool salto = false;
            for (int i = 0; i < rs.Count; i++) if (rs[i].Estado == "saltada") { salto = true; sb.Append(" - ").Append(rs[i].Nombre).Append(": ").Append(rs[i].Motivo).Append(nl); }
            if (!salto) sb.Append(" Ninguna.").Append(nl);
            sb.Append(nl);

            sb.Append("== 10. LIMITES ==").Append(nl);
            sb.Append(" - Es UNA corrida en UN rato: la rocola real cambia con la biblioteca, el repaso, el disco y lo que hagan los clientes. Repetirla antes de decidir.").Append(nl);
            sb.Append(" - El tiempo de cada fase es de reloj de pared del hilo de la pantalla: si Windows le quita el procesador en medio de una fase, ese tiempo cuenta en ella. No se puede separar.").Append(nl);
            sb.Append(" - 'Fuera del dibujo' mezcla mensajes, audio que se rellena desde el hilo de la pantalla, temporizadores, recolecciones y otros hilos; el reparto por hilo ayuda pero no lo resuelve.").Append(nl);
            sb.Append(" - El procesador por hilo sale de GetThreadTimes: en Windows XP avanza por tics de ~15 ms; sobre ventanas de varios segundos es fiable, no para tirones sueltos.").Append(nl);
            sb.Append(" - El numero de recolecciones de basura en un hueco es una coincidencia de contadores, no su duracion ni su causa.").Append(nl);
            sb.Append(" - Con musica muda del diagnostico la cadena de sonido entera trabaja, pero no se oye; la salida real puede pedir la musica de otra manera. No se promete ausencia de pausas del sistema operativo.").Append(nl);
            sb.Append(" - Las escenas de cambio de cancion mueven la pantalla como un relevo de verdad, pero con musica de un cliente no se cambia su cancion: solo se mide la transicion visual del color del genero.").Append(nl);
            if (m.Observaciones.Length > 0) sb.Append(m.Observaciones).Append(nl);
            return sb.ToString();
        }

        // El resumen corto de la pagina de Ctrl+F3 (dos renglones de unos 58 caracteres: cabe en su etiqueta sin recortarse).
        internal static string Resumen(DiagMeta m, List<DiagResultado> rs, string archivo)
        {
            DiagTotales t = Totales(rs);
            double peor = 0;
            for (int i = 0; i < rs.Count; i++) if (rs[i].Estado != "saltada" && rs[i].Cuadros > 0 && rs[i].Max > peor) peor = rs[i].Max;
            string a = "Diagnóstico " + m.Inicio.ToString("HH:mm", CultureInfo.InvariantCulture) + (m.Cancelado ? " CANCELADO" : "") + ": " + t.Medidas + " escenas, " + t.Huecos50 + " tirones >50 ms"
                + (peor > 0 ? ", peor " + N(peor, "0") + " ms" : "");
            if (a.Length > 62) a = a.Substring(0, 61) + "…";
            string b = string.IsNullOrEmpty(archivo) ? "No se pudo escribir el informe." : "Informe: " + Path.GetFileName(archivo);
            return a + "\n" + b;
        }

        // Escribe el informe sin pisar los anteriores y conserva solo los ultimos 'conservar'. Devuelve la ruta o null.
        internal static string Escribe(string carpeta, string texto, DateTime ahora, int conservar, out string error)
        {
            error = null;
            try
            {
                if (!Directory.Exists(carpeta)) Directory.CreateDirectory(carpeta);
                string baseNombre = "rocola_diagnostico_" + ahora.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture);
                string ruta = Path.Combine(carpeta, baseNombre + ".txt");
                int k = 2;
                while (File.Exists(ruta) && k < 100) { ruta = Path.Combine(carpeta, baseNombre + "_" + k.ToString(CultureInfo.InvariantCulture) + ".txt"); k++; }
                File.WriteAllText(ruta, texto, new UTF8Encoding(true));   // con marca: el bloc de notas de XP muestra bien los acentos
                Poda(carpeta, conservar, ruta);
                return ruta;
            }
            catch (Exception ex) { error = ex.Message; return null; }
        }

        private static readonly Regex Patron = new Regex("^rocola_diagnostico_\\d{4}-\\d{2}-\\d{2}_\\d{4}(_\\d+)?\\.txt$", RegexOptions.CultureInvariant);
        internal static void Poda(string carpeta, int conservar, string nuevo)
        {
            try
            {
                string[] fs = Directory.GetFiles(carpeta, "rocola_diagnostico_*.txt");
                List<string> buenos = new List<string>();
                foreach (string f in fs) if (Patron.IsMatch(Path.GetFileName(f))) buenos.Add(f);
                buenos.Sort(StringComparer.Ordinal);
                int sobran = buenos.Count - Math.Max(1, conservar);
                for (int i = 0; i < buenos.Count && sobran > 0; i++)
                {
                    if (string.Equals(buenos[i], nuevo, StringComparison.OrdinalIgnoreCase)) continue;
                    try { File.Delete(buenos[i]); sobran--; } catch { }
                }
            }
            catch { }
        }
    }

    // ============================ LO QUE LE HACE FALTA AL PANEL DE LA TARJETA ============================
    //
    // El enchufe (un campo y un «if» en RenderFrame y en NoPresentado) y UN turno del bucle de cuadros, igual que el de OnIdle:
    // espera a que toque con la espera de mensajes (sin gastar procesador), agenda como siempre y dibuja. Asi el diagnostico
    // mide los cuadros con el ritmo de la rocola encendida, no cuadros pedidos en un bucle sin freno.
    internal sealed partial class GpuPanel
    {
        private DiagCaptura diagSink;
        internal DiagCaptura DiagSink { get { return diagSink; } set { diagSink = value; } }
        internal int DiagPerdidas { get { return stars != null ? stars.Perdidas : 0; } }
        internal long DiagMemoriaTexturaMb { get { try { return stars != null ? (long)(stars.EstimatedTextureMemory >> 20) : -1; } catch { return -1; } } }

        // Un turno. true si presento un cuadro.
        internal bool DiagCuadroPaceado()
        {
            if (stars == null || !stars.Ok || !Visible) return false;
            int th = TopeMs();
            if (ThrottleMs > th) th = ThrottleMs;
            double ahora = AhoraMs();
            if (ahora + 0.6 < proxCuadroMs)
            {
                int pedido = MilisegundosEspera(proxCuadroMs - ahora);
                double antes = AhoraMs();
                EsperaMensajes(pedido);
                // Lo que se durmio a proposito no es trabajo: la toma lo aparta (si el hilo tarda en despertar, ese retraso SI cuenta).
                if (diagSink != null) diagSink.Ocioso(Math.Min(AhoraMs() - antes, pedido));
                return false;
            }
            AgendaCuadro(ahora, th);
            bool ok = RenderFrame();
            vistoMs = -1;   // igual que OnIdle: un cuadro pedido fuera del bucle no sabe cuando se vera
            return ok;
        }
    }
#endif
}
