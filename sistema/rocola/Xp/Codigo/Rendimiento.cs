// ================================================================================================
//  MEDIR ESTE EQUIPO, Y AJUSTAR LOS GRAFICOS CON LO MEDIDO
//  (Ctrl+F3 -> pestana "Imagen y rendimiento")
// ------------------------------------------------------------------------------------------------
//  La rocola sabia lo que cuesta cada trozo de su dibujo -eso lo miden las baterias de pruebas/-,
//  pero no sabia decirle a nadie como le va al equipo donde esta puesta. Aqui se enciende, se pulsa
//  un boton y contesta con numeros de ESE equipo.
//
//  CUATRO COSAS QUE MANDAN SOBRE TODO LO DEMAS:
//
//  1) SE MIDE, NO SE ADIVINA. Nada de una tabla de "equipos buenos y malos" escrita a mano. Se pone
//     cada pantalla de verdad, se le piden cuadros de verdad por el camino de verdad, y se cronometra.
//
//  2) SE MIDE EN MOVIMIENTO Y CON MUSICA. Una pantalla quieta y en silencio no es la rocola: es otra
//     maquina. Aqui se pasa de pagina a lo bestia, se cambia de letra -que ademas lee del disco-, se
//     escribe en el buscador y se entra y sale de discos, todo con una cancion puesta.
//
//     Y LA MUSICA NO ES UN EXTRA. Esta escrito en PlayTrack: "El audio se rellena DESDE EL HILO DE LA
//     PANTALLA (...) cada vez que se vacia un bloque la interfaz se para en seco a descifrar MP3 y a
//     pasarlo por toda la cadena de sonido". O sea que pelea por el MISMO hilo que el dibujo, veinte
//     veces por segundo. Medir sin ella es medir una maquina que no existe.
//
//  3) SE PROPONE, NO SE APLICA. Medir no toca ni un ajuste. La propuesta sale escrita, con sus
//     numeros, y no se mueve nada hasta que se pulsa "Aplicar lo recomendado".
//
//  4) CUANDO NO LLEGA, SE DICE. Hay equipos que no dan 30 cuadros por segundo ni con todo en bajo.
//     Ahi lo suyo es decirlo, no fingir que se arreglo.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Drawing; using Rocola.Dibujo2D;   // EN LINUX: GDI+ de la transicion (Plataforma/Dibujo2D.cs) hasta que este archivo se traduzca a SkiaSharp directo

namespace Rocola
{
    public partial class Form1
    {
        // LA META. Se pidio que el peor caso pasara de 30 cuadros por segundo. Se apunta a 33 y no a 30
        // porque una medida oscila: dejarlo clavado en la raya haria que la misma maquina saliera "bien"
        // una vez y "mal" la siguiente sin haber cambiado nada.
        private const int MetaFps = 33;
        // POR DEBAJO DE ESTO, UN ESCALON NO AHORRA NADA. Dos medidas del MISMO dibujo en la misma maquina
        // no dan el mismo numero: entre pasadas se mueven unas decimas. Un escalon cuyo "ahorro" cabe en
        // ese temblor no ha demostrado nada, y bajarlo seria afear la rocola a cambio de ruido.
        // (Es la regla de siempre; en pruebas/buscacosto.cs esta escrita como "Se midio, y NO PAGA".)
        private const double RuidoMs = 0.20;

        // ---------------------------------------------------------------------------------------------
        //  QUE BAJAR DE NIVEL, Y CUANDO PARAR
        // ---------------------------------------------------------------------------------------------
        //  Esta funcion es la que decide, y esta aparte del resto A PROPOSITO: no toca pantalla, no toca
        //  ajustes y no sabe que es una estrella. Recibe numeros -lo que ahorra cada escalon de cada
        //  efecto y lo que cuesta un cuadro del peor escenario- y devuelve en que NIVEL queda cada uno.
        //  Asi se puede comprobar de verdad (pruebas/banco.cs le da costes de mentira y mira si decide
        //  bien), que con algo que solo se pudiera probar delante de una tarjeta grafica no habria manera.
        //
        //  DOS REGLAS, Y LAS DOS IMPORTAN:
        //   - El orden lo manda el NUMERO: en cada vuelta se da el escalon que mas ahorra de los que
        //     quedan disponibles.
        //   - NO SE SALTA EL ESCALON DE EN MEDIO. Aunque el segundo escalon de una cosa ahorrase muchisimo,
        //     para llegar a el hay que dar antes el primero. Ir de alto a bajo de una es afear la rocola el
        //     doble de lo que hacia falta.
        //
        //  Y se para en cuanto la cuenta pasa de la meta: bajar de mas es tan malo como no bajar.
        // ---------------------------------------------------------------------------------------------
        private static int[] ProponeEscalones(double[] aMedio, double[] aBajo, double msPeor, int meta, out double msFinal)
        {
            if (meta <= 0)
            {
                msFinal = msPeor;
                int n0 = (aMedio == null) ? 0 : aMedio.Length;
                int[] todos = new int[n0];
                for (int i = 0; i < n0; i++) todos[i] = NivAlto;
                return todos;
            }
            return ProponeEscalonesMs(aMedio, aBajo, msPeor, 1000.0 / meta, out msFinal);   // los ms por cuadro de la meta
        }
        // Lo mismo con la meta ya en ms por cuadro (el medidor la pide con margen: ver ObjetivoMs).
        private static int[] ProponeEscalonesMs(double[] aMedio, double[] aBajo, double msPeor, double objetivo, out double msFinal)
        {
            msFinal = msPeor;
            int n = (aMedio == null) ? 0 : aMedio.Length;
            int[] niv = new int[n];
            for (int i = 0; i < n; i++) niv[i] = NivAlto;
            if (n == 0 || aBajo == null || aBajo.Length < n || !(objetivo > 0)) return niv;

            while (msFinal > objetivo)
            {
                // El escalon DISPONIBLE que mas ahorra. Disponible = el siguiente de esa cosa, nunca el de
                // mas abajo saltandose el de en medio.
                int mejor = -1; double mejorAhorro = RuidoMs;
                for (int i = 0; i < n; i++)
                {
                    double a = (niv[i] == NivAlto) ? aMedio[i] : (niv[i] == NivMedio) ? aBajo[i] : 0.0;
                    if (a > mejorAhorro) { mejor = i; mejorAhorro = a; }
                }
                if (mejor < 0) break;   // no queda ni un escalon que de verdad ahorre algo
                niv[mejor]--;
                msFinal -= mejorAhorro;
            }
            if (msFinal < 0.05) msFinal = 0.05;   // un cuadro no cuesta cero: no dividir por nada
            return niv;
        }

        // Los cuadros por segundo que dan esos ms de trabajo, sin pasarse del techo de la pantalla.
        // Sin el techo el numero mentiria al reves: un cuadro de 2 ms no son 500 por segundo si el
        // monitor solo ensena 60.
        private static int FpsDe(double ms, int tope)
        {
            if (ms <= 0.05) ms = 0.05;
            int f = (int)(1000.0 / ms);
            if (tope > 0 && f > tope) f = tope;
            return f;
        }

        // ---------------------------------------------------------------------------------------------
        //  EL CATALOGO DE EFECTOS
        // ---------------------------------------------------------------------------------------------
        //  Seis cosas, cada una con sus tres niveles y con lo que se ve en cada uno dicho en cristiano.
        //  Cuando ya se midio, cada escalon lleva ademas lo que ahorra EN ESTE EQUIPO; antes de medir
        //  sale "sin medir", nunca un numero inventado.
        // ---------------------------------------------------------------------------------------------
        private sealed class EfectoGrafico
        {
            public string Clave;        // como se llama en el archivo de configuracion
            public string Nombre;       // como se llama en la pantalla
            public string[] Pierde;     // que se ve en cada nivel: [0] bajo, [1] medio, [2] alto
            public double AhorroMedio;  // lo que ahorra bajar de alto a medio, MEDIDO
            public double AhorroBajo;   // y lo que ahorra bajar de medio a bajo
            public bool Medido;
        }

        private EfectoGrafico[] efectos;
        private EfectoGrafico[] Efectos()
        {
            if (efectos != null) return efectos;
            efectos = new EfectoGrafico[6];
            efectos[0] = NuevoEfecto("nieblas", "Nieblas y nebulosas",
                "Cielo liso: sin las manchas de color del genero.",
                "Se queda la nebulosa de color y se va el velo de encima.",
                "Nebulosa de color y velo, las dos capas.");
            efectos[1] = NuevoEfecto("estrellas", "Cuantas estrellas",
                "Un tercio de las estrellas: el cielo se ve bastante mas vacio.",
                "Seis de cada diez. De lejos casi no se distingue.",
                "Todas.");
            efectos[2] = NuevoEfecto("espectro", "Aurora del horizonte",
                "Apagada: el suelo deja de teñirse con la musica.",
                "A media altura: se sigue viendo latir, mas discreta.",
                "Sube hasta media pantalla en los golpes fuertes.");
            efectos[3] = NuevoEfecto("patron", "Patron nuevo de estrellas",
                "Nunca: el cielo se queda con las estrellas que tiene.",
                "Cada tres minutos en vez de cada uno.",
                "Cada minuto, con las dos constelaciones cruzandose.");
            efectos[4] = NuevoEfecto("fugaces", "Estrellas fugaces",
                "Ninguna.",
                "Una cada 15-30 segundos, en vez de dos cada 7-16.",
                "Hasta dos a la vez, cada 7-16 segundos.");
            efectos[5] = NuevoEfecto("animacion", "Zoom al entrar a un disco",
                "Sin animacion: el disco se abre de golpe.",
                "El vuelo dura 160 ms en vez de 260.",
                "El vuelo completo, 260 ms.");
            return efectos;
        }
        private static EfectoGrafico NuevoEfecto(string clave, string nombre, string bajo, string medio, string alto)
        {
            EfectoGrafico e = new EfectoGrafico();
            e.Clave = clave; e.Nombre = nombre; e.Medido = false;
            e.AhorroMedio = 0.0; e.AhorroBajo = 0.0;
            e.Pierde = new string[] { bajo, medio, alto };
            return e;
        }

        // Pone un efecto en un nivel (lo usan el medidor, los botones y el mando general).
        private void PonNivel(string clave, int niv)
        {
            if (niv < NivBajo) niv = NivBajo; else if (niv > NivAlto) niv = NivAlto;
            switch (clave)
            {
                case "nieblas": nieblasNiv = niv; break;
                case "patron": relevoNiv = niv; break;
                case "fugaces": fugacesNiv = niv; break;
                case "estrellas": estrellasDens = (niv == NivAlto) ? 1f : (niv == NivMedio) ? 0.6f : 0.3f; break;
                case "espectro": auroraAmount = (niv == NivAlto) ? 0.55f : (niv == NivMedio) ? 0.30f : 0f; break;
                case "animacion": animNiv = niv; break;   // transAnimOn sale solo del nivel
            }
            AplicaEfectosGpu();
        }
        private int LeeNivel(string clave)
        {
            switch (clave)
            {
                case "nieblas": return nieblasNiv;
                case "patron": return relevoNiv;
                case "fugaces": return fugacesNiv;
                case "animacion": return animNiv;
                // Estos dos se guardan como porcentaje -ya lo eran antes de que existieran los niveles- y el
                // nivel sale de ahi. El corte va a media escala, para que un valor puesto a mano en el archivo
                // de configuracion caiga en el escalon al que de verdad se parece.
                case "estrellas": return (estrellasDens > 0.8f) ? NivAlto : (estrellasDens > 0.45f) ? NivMedio : NivBajo;
                case "espectro": return (auroraAmount > 0.43f) ? NivAlto : (auroraAmount > 0.004f) ? NivMedio : NivBajo;
            }
            return NivAlto;
        }

        // Lleva las perillas al panel de la tarjeta grafica. Se llama al montarlo y cada vez que cambian.
        private void AplicaEfectosGpu()
        {
            if (progress != null) progress.Neon = progresoNeon;
#if ROCOLA_GPU
            AplicaPerfilCargaCaratulas();
            if (albumView != null) albumView.PortadasLod = caratulasLod;
            // Este va ANTES del corte de abajo: la pared de discos existe aunque el panel de la tarjeta no,
            // y es la pared la que decide como hornea sus casillas.
            if (albumView != null) albumView.HojeoLigero = hojeoLigero;   // aligerar mientras se pasa de pagina
            if (albumView != null) albumView.PaginaDesliza = paginaDesliza;   // la vieja sale mientras entra la nueva (Ctrl+F3 -> Pantalla)
            if (letterBar != null) letterBar.LupaGpu = letrasLupa;            // la barra de letras con lupa
            AjustaDiscoVivo(ListaPorGpu());                                   // la lista del disco (Pantalla -> Dentro del disco)
            if (pastCred != null) pastCred.MonedaGrande = monedaCae;           // la moneda que cae (Pantalla -> Lo que suena y los creditos)
            AjustaBuscadorPortadas();                                         // el buscador con portadas (Pantalla -> El buscador)
            if (gpuPanel == null) return;
            gpuPanel.OndasFps = NormalizaFluidezAnimacion(animOndasFps);
            gpuPanel.CubosFps = NormalizaFluidezAnimacion(animCubosFps);
            gpuPanel.PortadaEnfocada = portadaEnfocada;
            gpuPanel.GeneroTodo = colorGenero;   // el color del genero en todo (Pantalla -> El estilo)
            gpuPanel.Nieblas = nieblasNiv;
            gpuPanel.NebulosasCine = nebulosasCine;
            gpuPanel.VinetaSuave = vinetaSuave;
            gpuPanel.CieloMedia = cieloMedia; gpuPanel.GranoVineta = granoVineta;   // Performance (PerformanceGpu.cs)
            gpuPanel.FondoUnaCapa = fondoCapa; gpuPanel.CieloCuarto = cieloCuarto;
            gpuPanel.EstadoOpaco = EstadoOpacoActivo; gpuPanel.AuroraLigera = auroraLigeraPerf;
            gpuPanel.ReposoSinReflejo = reposoSinReflejo; gpuPanel.ReposoSinEstela = reposoSinEstela;
            gpuPanel.CoronaCompacta = coronaCompacta; gpuPanel.SinGrano = sinGranoPerf; gpuPanel.AuroraSinSuelo = auroraSinSuelo; gpuPanel.AuroraHaloCompacto = auroraHaloCompacto;
            gpuPanel.HaloSeleccion = haloSeleccion;
            gpuPanel.EstrellasFinas = estrellasFinas;
            gpuPanel.OndasSuaves = ondasSuaves;
            gpuPanel.ProgresoFluido = progresoFluido;
            gpuPanel.NeonIntenso = neonIntenso;
            gpuPanel.CieloProfundo = cieloProfundo;
            gpuPanel.AuroraBandas = auroraBandas;
            gpuPanel.LunaActiva = lunaActiva;
            gpuPanel.ControlesReposo = controlesReposo;
            gpuPanel.LunaRealista = lunaRealista;
            gpuPanel.HaloLunar = haloLunar;
            gpuPanel.PolvoLunar = polvoLunar;
            gpuPanel.LunaProtagonista = lunaProtagonista;
            gpuPanel.EstelaEstelar = estelaEstelar;
            gpuPanel.Estrella3D = estrella3d;
            // El pulido (PulidoGpu.cs)
            gpuPanel.CieloSuave = cieloSuave; gpuPanel.ColoresProfundos = coloresProfundos; gpuPanel.AuroraLimpia = auroraLimpia;
            gpuPanel.EstrellaDegradado = estrellaDegradado; gpuPanel.EstrellaRefleja = estrellaRefleja; gpuPanel.EstrellaDestello = estrellaDestello;
            gpuPanel.EstrellaBorde = estrellaBorde; gpuPanel.EstrellaEspejo = estrellaEspejo; gpuPanel.PortadaBrillo = portadaBrillo;
            if (InterfazTramada != interfazTramada) { InterfazTramada = interfazTramada; CambioElPanelDeReposo(); }   // C4: se rehornea
            gpuPanel.LunaDelCielo = lunaDelCielo; gpuPanel.LluviasEstrellas = lluviasEstrellas;   // el cielo vivo (CieloVivoGpu.cs)
            gpuPanel.FugazFestejo = fugazFestejo;
            gpuPanel.ProfundidadPagina = profundidadPagina; gpuPanel.CieloPorHora = cieloPorHora; gpuPanel.ReflejoAurora = reflejoAurora;
            gpuPanel.ColoresFluidos = coloresFluidos;
            gpuPanel.TransicionesTacto = transicionesTacto;
            gpuPanel.CachePortadas = true;
            if (searchOverlay != null) searchOverlay.SeparateGpu = true;
            CambioElPanelDeReposo();   // 'estado ampliado transparente' y 'cola de doce' cambian como se dibuja
            if (!EstadoExtendidoEfectivo) gpuPanel.IdleExpandTarget = false;
            gpuPanel.UiUrgent = true;
            gpuPanel.Fugaces = fugacesNiv;
            gpuPanel.RelevoPatron = relevoNiv;
            gpuPanel.Estrellas = estrellasDens;
            gpuPanel.TopeFps = fpsTope;
            gpuPanel.OlvidaTope();   // el tope se resuelve una vez y se guarda: al cambiarlo hay que re-resolverlo
#endif
        }

        // ---------------------------------------------------------------------------------------------
        //  LO MEDIDO
        // ---------------------------------------------------------------------------------------------
        private sealed class EscenaMedida
        {
            public string Nombre;
            public double MsTrabajo;   // lo que cuesta ARMAR un cuadro (sin la espera del monitor), de media
            public int Fps;            // los cuadros por segundo ENTREGADOS de verdad, con todo compitiendo
            public double P95 = 0, P99 = 0, PortadasMs = 0;
            public int Lentos = 0, PaginasListas = 0, PaginasSolicitadas = 0;
            public int PerfilCarga = 0, Ancho = 0, Alto = 0, PreviasGpu = 0, VistasGpu = 0;
            public string Inicio = "";
            public DateTime Hora = DateTime.MinValue;
            public bool MusicaReal = false;
            // EL MEDIDOR RENOVADO (13-10-2026). Lo tipico (medianas) de los cuadros, presentados SIN esperar al monitor:
            public double MsCuadro;      // de un cuadro al siguiente: lo que de verdad tarda (1000 / esto = cuadros posibles)
            public double P95Cuadro;     // el 5 % peor
            public double P99Cuadro = 0, MaxCuadro = 0;
            public double MsProcesador;  // preparar y mandar el dibujo
            public double MsEspera;      // presentar: con el monitor suelto, es esperar a la TARJETA
            public double MsFuera;       // entre cuadros: la musica, los mensajes y lo que hace la pantalla
            public double Pedidos, Envios, Pantallas;   // ordenes pedidas y mandadas, y pantallas pintadas, por cuadro
            public bool SinEspera;       // si se pudo medir con el monitor suelto
        }
        private List<EscenaMedida> bancoEscenas;   // null = aun sin medir
        private EscenaMedida bancoPortadas = null;
        private string bancoPeor = "";             // el escenario que peor salio
        private double bancoMsPeor;                // lo que cuesta un cuadro ahi (ver CosteDe)
        private int bancoFpsPeor;
        private int bancoTope;                     // el techo de la pantalla en este equipo
        private int[] bancoPropuesta;              // el nivel propuesto para cada efecto (null = sin medir)
        private int bancoFpsPropuesta;             // donde quedaria
        private bool bancoConGpu;                  // si al medir la rocola dibujaba con tarjeta grafica
        private string bancoError;                 // una medicion fallida no produce recomendaciones
        private bool bancoConMusica;               // si se pudo medir con una cancion puesta
        // EL MEDIDOR RENOVADO (13-10-2026; peticion: «el benchmark renuevalo para que sea mas inteligente y mejor»):
        private bool bancoSinEspera;               // se midio con el monitor suelto (si no, los ms son solo del procesador)
        private int bancoHz = 60;                  // el refresco de la pantalla
        private double bancoObjetivoAlto, bancoObjetivoBajo;   // ms por cuadro para dar el refresco o la mitad, con margen
        private double[] bancoAhorros;             // lo que ahorra cada ahorro de Performance; NaN = no hizo falta medirlo, -1 = la tarjeta no puede
        private bool[] bancoUsaAhorro;             // la propuesta: que ahorros encender
        private int bancoMetaLograda;              // 2 = la del refresco (60), 1 = la mitad (30), 0 = ni bajandolo todo
        private double bancoMsComprobado = -1;     // y lo que dio al probarla de verdad (-1 = sin comprobar)
        private string bancoFrena = "";            // lo que frena, dicho en palabras
        private bool bancoEfectosMedidos;          // si hizo falta medir los escalones de efectos
        private int bancoPeorId = -1;              // cual de las seis salio peor (se marca en la tabla)

        // UN CUADRO TIENE QUE CABER CON HOLGURA: un 8 % de margen, porque una medida oscila y un cuadro justo en la raya
        // se pasaria la mitad de las veces (y con la espera al monitor, pasarse un poco es caer a la mitad).
        internal const double MargenMeta = 0.92;
        internal static double ObjetivoMs(int fps) { return 1000.0 / Math.Max(1, fps) * MargenMeta; }

        // LOS AHORROS DE PERFORMANCE (PerformanceGpu.cs), en el orden de su lista. Casi no se notan: van ANTES que bajar
        // efectos, que si se notan.
        // El tercero, «Fondo en una sola capa» (G2, 14-10-2026), va DENTRO de la imagen del cielo a media resolucion: sin el
        // primero no hace nada (RequiereAhorro), y lo que ahorra se mide encima de los otros dos (AhorroSobre). El cuarto, «Cielo
        // a un cuarto de resolucion» (J2, 14-10-2026), hace esa imagen mas chica: tambien necesita el primero.
        private static readonly string[] NombreAhorro = { "Cielo a media resolución", "Grano y viñeta en una pasada", "Fondo en una sola capa",
                                                          "Cielo a un cuarto de resolución", "Estado extendido opaco", "Aurora con menos columnas",
                                                          "Sin reflejo de aurora en reposo", "Sin estela de estrella en reposo", "Corona de estrella compacta", "Color directo del estado extendido", "Fondo sin grano", "Aurora sin velo inferior", "Halo de aurora compacto" };
        internal static readonly int[] RequiereAhorro = { -1, -1, 0, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1 };
#if ROCOLA_GPU
        private static readonly int[][] AhorroSobre = { null, null, new int[] { 0, 1 }, new int[] { 0 } };
#endif
        // Ctrl+F3 -> Imagen -> Performance. Son de aspecto (casi identico): interruptor propio, apagados de fabrica (AGENTS.md).
        private bool cieloMedia, granoVineta, fondoCapa, cieloCuarto;
        private bool estadoOpacoPerf, auroraLigeraPerf, reposoSinReflejo, reposoSinEstela, coronaCompacta, estadoColorDirecto, sinGranoPerf, auroraSinSuelo, auroraHaloCompacto;
        private bool EstadoOpacoActivo { get { return estadoOpacoPerf && EstadoExtendidoEfectivo; } }
        private bool EstadoExtendidoEfectivo
        {
            get { return estadoExpandido
#if ROCOLA_GPU
                || bancoReposoActivo
#endif
                ; }
        }
        private bool LeeAhorro(int i)
        {
            switch (i) {
                case 0: return cieloMedia; case 1: return granoVineta; case 2: return fondoCapa; case 3: return cieloCuarto;
                case 4: return estadoOpacoPerf; case 5: return auroraLigeraPerf; case 6: return reposoSinReflejo;
                case 7: return reposoSinEstela; case 8: return coronaCompacta; case 9: return estadoColorDirecto; case 10: return sinGranoPerf; case 11: return auroraSinSuelo; case 12: return auroraHaloCompacto; default: return false;
            }
        }
        private void PonAhorro(int i, bool encendido)
        {
            if (i == 0) cieloMedia = encendido; else if (i == 1) granoVineta = encendido; else if (i == 2) fondoCapa = encendido;
            else if (i == 3) cieloCuarto = encendido;
            else if (i == 4) estadoOpacoPerf = encendido; else if (i == 5) auroraLigeraPerf = encendido;
            else if (i == 6) reposoSinReflejo = encendido; else if (i == 7) reposoSinEstela = encendido;
            else if (i == 8) coronaCompacta = encendido; else if (i == 9) estadoColorDirecto = encendido;
            else if (i == 10) sinGranoPerf = encendido; else if (i == 11) auroraSinSuelo = encendido; else if (i == 12) auroraHaloCompacto = encendido;
            AplicaEfectosGpu();
        }
        // Todos los ahorros a la vez (los presets y «Graficos como de fabrica»).
        private void PonAhorros(bool encendidos)
        {
            cieloMedia = granoVineta = fondoCapa = cieloCuarto = estadoOpacoPerf = auroraLigeraPerf = reposoSinReflejo = reposoSinEstela = coronaCompacta = estadoColorDirecto = sinGranoPerf = auroraSinSuelo = auroraHaloCompacto = encendidos;
            CambioElPanelDeReposo();
        }
        private bool TodosLosAhorros(bool encendidos)
        {
            for (int i = 0; i < NombreAhorro.Length; i++) if (LeeAhorro(i) != encendidos) return false;
            return true;
        }
        // POR QUE UN AHORRO NO HACE NADA AHORA (14-10-2026: «pon en gris los settings que el performance anule»). null = hace
        // lo suyo. El cielo a media resolucion, y lo que va dentro de el, solo se usan con las nebulosas puestas; la pasada
        // de grano y vineta necesita la vineta, y con el fondo en una sola capa no se usa: el grano y la vineta ya van dentro
        // del cielo.
        private string MotivoSinEstadoExtendido()
        {
            if (!estadoExpandido) return "Necesita: Estado ampliado en reposo (Imagen)";
            if (!muroReposo) return "Necesita: Ocultar discos en reposo (Imagen)";
            return null;
        }
        private string MotivoSinAurora()
        { return auroraAmount > .004f ? null : "Necesita: Aurora del horizonte en Medio o Alto"; }
        private string MotivoSinAhorro(int i)
        {
#if ROCOLA_GPU
            if (gpuEnabled && gpuPanel != null && !PuedeAhorro(i))
                return "No disponible: esta tarjeta no admite este ahorro";
#endif
            bool nebulosas = nieblasNiv != NivBajo;
            bool capa = cieloMedia && fondoCapa && nebulosas;
            switch (i)
            {
                case 0: return nebulosas ? null : "Necesita: Nieblas y nebulosas en Medio o Alto (Efectos)";
                case 10: return null;
                case 11: case 12: return MotivoSinAurora();
                case 1:
                    if (sinGranoPerf) return "Sin efecto: Fondo sin grano (Performance)";
                    if (!vinetaSuave) return "Necesita: Viñeta suave (Fondo)";
                    if (capa) return "Sin efecto: ya va dentro del cielo (Fondo en una sola capa)";
                    return null;
                case 9: case 4: return MotivoSinEstadoExtendido();
                case 5: return MotivoSinAurora();
                case 6:
                    if (MotivoSinEstadoExtendido() != null) return MotivoSinEstadoExtendido();
                    if (estadoOpacoPerf) return "Sin efecto: el estado opaco ya tapa ese reflejo";
                    if (MotivoSinAurora() != null) return MotivoSinAurora();
                    return reflejoAurora ? null : "Necesita: Reflejo de la aurora (Fondo / Cielo vivo)";
                case 7:
                    if (MotivoSinEstadoExtendido() != null) return MotivoSinEstadoExtendido();
                    if (!lunaOn) return "Necesita: Astro principal en el cielo (Detalles)";
                    if (!lunaActiva) return "Necesita: Estrella viajera con orbitas (Fondo)";
                    return estelaEstelar ? null : "Necesita: Ecos de color de la estrella (Fondo)";
                case 8:
                    if (!lunaOn) return "Necesita: Astro principal en el cielo (Detalles)";
                    return haloLunar ? null : "Necesita: Corona estelar (Fondo / Estrella)";
                default:
                    if (!cieloMedia) return "Necesita: Cielo a media resolución (arriba)";
                    if (!nebulosas) return "Necesita: Nieblas y nebulosas en Medio o Alto (Efectos)";
                    return null;
            }
        }
        private bool PuedeAhorro(int i)
        {
#if ROCOLA_GPU
            if (gpuPanel == null) return false;
            if (i >= 4) return true;
            return i == 0 ? gpuPanel.PuedeCieloMedia : (i == 1 ? gpuPanel.PuedeGranoVineta : gpuPanel.PuedeFondoUnaCapa);   // (J2, como G2: va en la imagen intermedia)
#else
            return false;
#endif
        }

        // LA PROPUESTA CON LOS AHORROS. Pura, como ProponeEscalones (pruebas/banco.cs le da numeros de mentira):
        //  - si ya cabe en la meta alta (el refresco de la pantalla), no se toca nada;
        //  - si no, primero lo que casi no se nota (los ahorros de Performance, el que mas ahorra primero) y solo si no
        //    basta, los escalones de efectos con la regla de siempre;
        //  - si ni con todo llega a la alta, lo mismo para la baja (la mitad: 30 con una pantalla de 60);
        //  - y si tampoco, todo lo que ayuda, y 'meta' lo dice (0).
        // Devuelve el nivel de cada efecto; 'usa' dice que ahorros encender y 'meta' a cual se llega: 2 alta, 1 baja, 0 ninguna.
        internal static int[] PlanDeAhorros(double msBase, double[] ahorros, double[] aMedio, double[] aBajo,
            double objetivoAlto, double objetivoBajo, out bool[] usa, out double msFinal, out int meta)
        {
            int na = (ahorros == null) ? 0 : ahorros.Length, ne = (aMedio == null) ? 0 : aMedio.Length;
            int[] niv = new int[ne];
            for (int i = 0; i < ne; i++) niv[i] = NivAlto;
            usa = new bool[na];
            msFinal = msBase; meta = 2;
            if (msBase <= objetivoAlto) return niv;
            // Los ahorros que de verdad ahorran, del que mas al que menos.
            List<int> orden = new List<int>();
            // Quitar el grano y unirlo a la vineta son alternativas: no sumar dos veces
            // el mismo ahorro ni proponer las dos como ganancias independientes.
            bool sinGranoMejor = na > 10 && ahorros[10] >= ahorros[1] && ahorros[10] > RuidoMs;
            for (int i = 0; i < na; i++)
                if (ahorros[i] > RuidoMs && !(i == 1 && sinGranoMejor)
                    && !(i == 10 && !sinGranoMejor && ahorros[1] > RuidoMs)) orden.Add(i);
            orden.Sort(delegate (int a, int b) { return ahorros[b].CompareTo(ahorros[a]); });
            int[] ultimo = niv; bool[] ultimoUsa = usa; double ultimoMs = msBase;
            for (int tramo = 2; tramo >= 1; tramo--)
            {
                double objetivo = (tramo == 2) ? objetivoAlto : objetivoBajo;
                if (tramo == 1 && msBase <= objetivo) { usa = new bool[na]; msFinal = msBase; meta = 1; return niv; }
                // Solo con los ahorros, los justos.
                bool[] u = new bool[na]; double ms = msBase;
                foreach (int k in orden)
                {
                    if (ms <= objetivo) break;
                    if (u[k]) continue;
                    u[k] = true; ms -= ahorros[k];
                    // El que necesita a otro (el fondo en una capa va dentro del cielo a media resolucion) lo lleva consigo.
                    int r = k < RequiereAhorro.Length ? RequiereAhorro[k] : -1;
                    if (r >= 0 && r < na && !u[r]) { u[r] = true; ms -= Math.Max(0.0, ahorros[r]); }
                }
                if (ms <= objetivo) { usa = u; msFinal = ms; meta = tramo; return niv; }
                // Con todos los ahorros, y los escalones que hagan falta.
                bool[] todos = new bool[na]; double conTodos = msBase;
                foreach (int k in orden)
                {
                    if (!todos[k]) { todos[k] = true; conTodos -= ahorros[k]; }
                    int r = k < RequiereAhorro.Length ? RequiereAhorro[k] : -1;
                    if (r >= 0 && r < na && !todos[r]) { todos[r] = true; conTodos -= Math.Max(0.0, ahorros[r]); }
                }
                double fin;
                int[] escalones = ProponeEscalonesMs(aMedio, aBajo, conTodos, objetivo, out fin);
                if (fin <= objetivo) { usa = todos; msFinal = fin; meta = tramo; return escalones; }
                ultimo = escalones; ultimoUsa = todos; ultimoMs = fin;
            }
            usa = ultimoUsa; msFinal = ultimoMs; meta = 0;
            return ultimo;
        }

        // Las seis pantallas donde la rocola pasa su vida. Ya no son fotos fijas: cada una lleva dentro lo
        // que de verdad hace la gente en ella.
        private const int EscPagina = 0, EscLetra = 1, EscBuscador = 2, EscDisco = 3, EscAtraccion = 4, EscReposo = 5;
        private static readonly string[] NombreEscena = {
            "Pasando de pagina", "Cambiando de letra", "Escribiendo en el buscador",
            "Entrando a discos", "Pantalla de atraccion", "Estado extendido" };
        private const int MsPorEscena = 2000;    // lo que dura la medida de cada pantalla
        private const int MsPorEscalon = 1200;   // ... y la de cada escalon de efecto (la pantalla ya esta puesta)
        private const int MsCalentando = 350;    // los primeros no cuentan: una pantalla nueva sube texturas

        // LO QUE CUESTA UN CUADRO, para comparar. Con el monitor suelto, lo que tarda de verdad de un cuadro al siguiente
        // (procesador, tarjeta y musica). Si no se pudo soltar, lo de antes: lo que gasta el procesador en armarlo.
        private double CosteDe(EscenaMedida m)
        {
            if (m == null) return 0;
            return (bancoSinEspera && m.SinEspera && m.MsCuadro > 0) ? m.MsCuadro : m.MsTrabajo;
        }
        // LO QUE FRENA, en palabras: donde se va el cuadro tipico de la pantalla que peor va.
        private static string QueFrena(EscenaMedida m)
        {
            if (m == null) return "";
            if (!m.SinEspera || m.MsCuadro <= 0)
                return "Sin soltar el monitor: solo se ve el procesador (" + m.MsTrabajo.ToString("0.0") + " ms).";
            double c = Math.Max(0.01, m.MsCuadro);
            string de = " ms de cada " + c.ToString("0.0") + ".";
            if (m.MsEspera >= 0.35 * c) return "Frena la tarjeta gráfica: espera " + m.MsEspera.ToString("0.0") + de;
            if (m.MsFuera >= 0.35 * c) return "Frenan la música y los mensajes: " + m.MsFuera.ToString("0.0") + de;
            return "Frena el procesador al armar el cuadro: " + m.MsProcesador.ToString("0.0") + de;
        }

#if ROCOLA_GPU
        private int pasoEsc;                       // por donde va la pantalla que se mide (que letra, que pagina...)
        private bool bancoPuseYoLaCancion;         // si la cancion la puso el medidor: entonces es suya para pararla
        // Cada cuanto se da un paso de la pantalla. El de pagina va a 180 ms A PROPOSITO: la animacion de
        // pasar dura 230, asi que se la interrumpe a si misma, que es exactamente lo que pasa cuando alguien
        // machaca el boton. Lo demas va al ritmo al que lo haria una persona con prisa.
        private static readonly int[] PasoEscenaMs = { 180, 420, 150, 380, 0, 0 };

        // UN PULSO DE MUSICA DE MENTIRA, para cuando NO se pudo poner ninguna cancion. Con musica de verdad
        // esto no se llama: los niveles salen de la cancion, que es lo suyo.
        private void PulsoDeMentira()
        {
            audioLevel = 0.85f;
            levLo = 0.90f; levMid = 0.72f; levHi = 0.80f;
            for (int b = 0; b < SpecBands; b++)
                specVis[b] = 0.55f + 0.40f * (float)Math.Sin(b * 0.7 + phase);
            auroraPres = 1f;
        }

        // UN PASO de la pantalla que se esta midiendo: lo que haria la persona que la esta usando.
        private void PasoDeEscena(int escena)
        {
            try
            {
                pasoEsc++;
                switch (escena)
                {
                    case EscPagina: NextPage(); break; // paginas distintas, no alternar dos ya calientes
                    case EscLetra: ChangeLetter(((pasoEsc & 7) < 4) ? 1 : -1); break;
                    case EscBuscador:
                        // Escribe una palabra y la borra, sin parar. Cada letra rehace la lista de resultados
                        // Y vuelve a subir la textura de la pantalla ENTERA a la tarjeta: es lo mas caro que
                        // hace la rocola, y por eso esta aqui.
                        if (searchOverlay == null) break;
                        int k = pasoEsc % 16;
                        if (k < 8) searchOverlay.TypeChar("SABADO"[k % 6]);
                        else searchOverlay.Backspace();
                        break;
                    case EscDisco:
                        if (panelSongs != null && panelSongs.Visible) BackToAlbums();
                        else
                        {
                            List<Album> lista = CurrentList();
                            if (lista != null && lista.Count > 0)
                            {
                                int gi = selAlbumIndex; if (gi < 0 || gi >= lista.Count) gi = 0;
                                EnterAlbum(lista[gi]);
                            }
                        }
                        break;
                }
            }
            catch { }
        }

        // EL BUCLE. Pide los cuadros el mismo -medir ocurre dentro de una pulsacion, o sea con el bucle de
        // mensajes parado, asi que esperar a que dibuje solo seria esperar sentado- y ADEMAS deja correr los
        // mensajes entre uno y otro. Las dos cosas hacen falta, y la segunda es la que hace que esto valga:
        // de ese bucle de mensajes come el audio, y si no corre, la cancion se queda sin colchon y su trabajo
        // -que es el que de verdad compite con el dibujo, en el mismo hilo- NO ENTRARIA EN LA CUENTA. El
        // numero saldria bueno y falso.
        //
        // El reloj de las animaciones se adelanta A SU RITMO (uno cada 42 ms de reloj de pared, los de
        // siempre) y con el temporizador de verdad PARADO: si se dispararan los dos, la rocola creeria que va
        // al doble de velocidad y rehariamos la textura de la interfaz el doble de veces que en la vida real.
        private void CorreEscena(int escena, int ms, out int cuadros, out int msReales)
        {
            cuadros = 0; msReales = 1;
            if (gpuPanel == null || !gpuPanel.Visible) return;
            int t0 = Environment.TickCount, ultTick = t0 - 42, ultPaso = t0;
            int paso = PasoEscenaMs[escena];
            int calentamiento = escena == EscPagina ? 0 : escena == EscReposo && !gpuPanel.EstadoExtendidoAsentado ? 2200 : MsCalentando;
            bool contando = false;
            int tCuenta = t0;
            while (true)
            {
                int ahora = Environment.TickCount;
                int va = ahora - t0;
                if (va >= calentamiento + ms) break;
                if (!contando && va >= calentamiento)
                {
                    contando = true; tCuenta = ahora; cuadros = 0;
                    gpuPanel.ReiniciaMedida();   // lo de calentar no cuenta
                }
                if (ahora - ultTick >= 42) { ultTick = ahora; try { AnimTimer_Tick(null, EventArgs.Empty); } catch { } }
                if (escena == EscPagina) paso = va < 1500 ? 600 : 180; // paso lento y rafaga en la misma medida
                if (paso > 0 && ahora - ultPaso >= paso) { ultPaso = ahora; PasoDeEscena(escena); }
                if (!bancoConMusica) PulsoDeMentira();   // sin cancion, un latido de mentira; con ella, el suyo
                if (escena == EscReposo) gpuPanel.IdleExpandTarget = true;
                bool presentado = gpuPanel.DibujaUnCuadro();
                if (contando && presentado) cuadros++;
                PantallaRocola.Bombea();   // de aqui come el audio: sin esto la musica no entraria en la cuenta; EN LINUX: todo lo pendiente de la pantalla nativa, sin Libre (en XP, Application.DoEvents)
            }
            msReales = Environment.TickCount - tCuenta;
            if (msReales < 1) msReales = 1;
        }
        // Unos cuadros sin contar, tras soltar el monitor: el primero reinicia el dispositivo con la espera nueva.
        private void CalientaMedida(int ms)
        {
            int t0 = Environment.TickCount, ultTick = t0 - 42;
            while (unchecked(Environment.TickCount - t0) < ms)
            {
                int ahora = Environment.TickCount;
                if (ahora - ultTick >= 42) { ultTick = ahora; try { AnimTimer_Tick(null, EventArgs.Empty); } catch { } }
                if (!bancoConMusica) PulsoDeMentira();
                gpuPanel.DibujaUnCuadro();
                PantallaRocola.Bombea();   // EN LINUX: todo lo pendiente de la pantalla nativa, sin Libre (en XP, Application.DoEvents)
            }
        }

        private EscenaMedida MideEscena(int escena)
        {
            EscenaMedida m = new EscenaMedida();
            m.Nombre = NombreEscena[escena]; m.MsTrabajo = 0.0; m.Fps = 0;
            return MideEscena(escena, escena == EscPagina ? 4500 : MsPorEscena, m);
        }
        private EscenaMedida MideEscena(int escena, int ms, EscenaMedida m)
        {
            if (gpuPanel == null || !gpuPanel.Visible)
                throw new InvalidOperationException("El panel GPU no esta disponible; medicion no valida");
            int cuadros, msReales;
            CorreEscena(escena, ms, out cuadros, out msReales);
            if (escena == EscReposo && !gpuPanel.EstadoExtendidoAsentado)
                throw new InvalidOperationException("No se pudo abrir el estado extendido; medicion no valida");
            if (gpuPanel.FallosMedidos != 0 || cuadros == 0)
                throw new InvalidOperationException("La GPU no presento todos los cuadros; medicion no valida");
            m.MsTrabajo = gpuPanel.MsTrabajo;
            m.P95 = coverGovernor.Percentile(.95); m.P99 = coverGovernor.Percentile(.99);
            m.Lentos = coverGovernor.SlowFrames; m.PaginasListas = coverGovernor.CompletedPages;
            m.PaginasSolicitadas = coverGovernor.StartedPages;
            m.PortadasMs = coverGovernor.WorstPageMs;
            // Cuadros cuyo Present acepto el controlador, contados con el reloj de pared: con la musica
            // peleando por el mismo hilo, este numero es lo que se siente. El de arriba (ms de trabajo) dice
            // otra cosa distinta: margen de trabajo. No es una medicion de fotones ni latencia del monitor.
            m.Fps = (int)(cuadros * 1000L / msReales);
            // Y donde se va el cuadro tipico (PerformanceGpu.cs / MedidaFluidezGpu.cs).
            double intervalo, preparar, dibujar, presentar, fuera, p95;
            gpuPanel.Medianas(out intervalo, out preparar, out dibujar, out presentar, out fuera, out p95);
            m.MsCuadro = intervalo; m.P95Cuadro = p95; m.MsProcesador = preparar + dibujar;
            m.MsEspera = presentar; m.MsFuera = fuera;
            gpuPanel.ColasDeCuadro(out m.P99Cuadro, out m.MaxCuadro);
            m.Pedidos = gpuPanel.PedidosPorCuadro; m.Envios = gpuPanel.EnviosPorCuadro; m.Pantallas = gpuPanel.PantallasPorCuadro;
            m.SinEspera = gpuPanel.SinEspera;
            Diag("medida " + m.Nombre + ": p95=" + m.P95Cuadro.ToString("0.0") + " p99=" + m.P99Cuadro.ToString("0.0")
                + " max=" + m.MaxCuadro.ToString("0.0") + " ms; repaso_pendiente=" + System.Threading.Thread.VolatileRead(ref preScanLeft)
                + "; miniaturas_pendientes=" + minisPendientes);
            return m;
        }
        private double MideCoste(int escena, int ms) { return CosteDe(MideEscena(escena, ms, new EscenaMedida())); }
#else
        // Sin tarjeta grafica no hay nada que cronometrar: estas pantallas no se dibujan por ese camino. Se
        // devuelven los campos a cero -y se ASIGNAN, para que el compilador no avise de campos que nadie
        // escribe- y la pagina ya dice, en su sitio, por que aqui no hay numeros.
        private EscenaMedida MideEscena(int escena)
        {
            EscenaMedida m = new EscenaMedida();
            m.Nombre = NombreEscena[escena]; m.MsTrabajo = 0.0; m.Fps = 0;
            m.MsCuadro = m.P95Cuadro = m.MsProcesador = m.MsEspera = m.MsFuera = 0; m.Pedidos = m.Envios = m.Pantallas = 0; m.SinEspera = false;
            return m;
        }
#endif

        // ---------------------------------------------------------------------------------------------
        //  EL MEDIDOR
        // ---------------------------------------------------------------------------------------------
        //  Pone una cancion, mide las portadas como siempre (con el monitor), y despues SUELTA EL MONITOR:
        //  recorre las seis pantallas EN MOVIMIENTO viendo lo que cuesta de verdad cada cuadro, se queda con
        //  la peor y, contra esa:
        //   1. si ya cabe en el refresco de la pantalla (60), no prueba nada mas;
        //   2. prueba los ahorros de Performance uno a uno, cada uno ENTRE DOS medidas sin el (lo que tiembla
        //      la maquina entre una y otra es el ruido: un ahorro mas chico que eso no cuenta);
        //   3. solo si con ellos no basta, mide los escalones de efectos (con los ahorros puestos);
        //   4. arma la propuesta (PlanDeAhorros): primero 60, si no 30;
        //   5. y la COMPRUEBA: la pone, mide otra vez la peor pantalla y lo apunta. Los ahorros no siempre se
        //      suman (si frena el procesador, aliviar la tarjeta no da nada): lo comprobado es lo que vale.
        //  Al acabar deja la rocola donde estaba, monitor incluido.
        //
        //  NO GUARDA NADA Y NO APLICA NADA: solo rellena lo medido y la propuesta. (La bateria lo vigila:
        //  si algun dia esta funcion llamara a SaveConfigPrefs, se pondria roja.)
        // ---------------------------------------------------------------------------------------------
        private void MideRendimiento()
        {
            // Todo a cero primero: volver a medir no puede dejar ni un numero de la vez anterior asomando.
            bancoEscenas = new List<EscenaMedida>();
            bancoPortadas = null;
            bancoPropuesta = null; bancoError = null;
            bancoConGpu = false; bancoConMusica = false;
            bancoPeor = ""; bancoMsPeor = 0.0; bancoFpsPeor = 0; bancoTope = 0; bancoFpsPropuesta = 0;
            bancoSinEspera = false; bancoHz = 60; bancoObjetivoAlto = bancoObjetivoBajo = 0;
            bancoAhorros = null; bancoUsaAhorro = null; bancoMetaLograda = 0; bancoMsComprobado = -1; bancoPeorId = -1;
            bancoFrena = ""; bancoEfectosMedidos = false;
#if ROCOLA_GPU
            bancoPuseYoLaCancion = false;
            if (gpuPanel == null || !gpuEnabled || !gpuPanel.Visible) return;
            bancoConGpu = true;
            bancoTope = gpuPanel.TopeFpsReal;
            bancoHz = gpuPanel.RefrescoHz;
            bancoObjetivoAlto = ObjetivoMs(bancoHz);
            bancoObjetivoBajo = ObjetivoMs(Math.Max(24, bancoHz / 2));

            // COMO ESTABA TODO, para dejarlo igual pase lo que pase.
            bool bkBuscador = searchOpen, bkAtraccion = attractOn;
            bool bkDisco = (panelSongs != null && panelSongs.Visible);
            char bkLetra = currentLetter; int bkPag = page, bkSel = selAlbumIndex;
            EfectoGrafico[] ef = Efectos();
            int[] bkNiv = new int[ef.Length];
            for (int i = 0; i < ef.Length; i++) bkNiv[i] = LeeNivel(ef[i].Clave);
            bool[] bkAhorro = new bool[NombreAhorro.Length];
            for (int i = 0; i < bkAhorro.Length; i++) bkAhorro[i] = LeeAhorro(i);
            for (int i = 0; i < ef.Length; i++) { ef[i].Medido = false; ef[i].AhorroMedio = ef[i].AhorroBajo = 0.0; }
            animTimer.Stop();   // durante la medida el reloj de animaciones lo lleva CorreEscena, a mano
            try
            {
                // ---- LA MUSICA ----
                // Si ya hay algo sonando se usa ESO: es la carga de verdad y ademas es la cancion de un
                // cliente que la pago, asi que ni se para ni se le toca el volumen. Solo si no hay nada se
                // pone una, y MUDA: sale callada del final de la cadena (CallaSiEsMedida, SalidaSonido.cs), asi
                // que hace exactamente el mismo trabajo -descomprimir el MP3, el DSEE, el nivelado, los graves,
                // el limitador- sin tocar ningun volumen de Windows. Se mide igual de duro y no se le suelta
                // medio minuto de ruido al local.
                if (nowPlaying != null) bancoConMusica = output != null; // no reemplazar una cancion aun abriendo
                else if (tracks.Count > 0)
                {
                    try
                    {
                        PlayTrackForMeasurement(tracks[new Random().Next(tracks.Count)]);
                        if (output != null) { bancoConMusica = true; bancoPuseYoLaCancion = true; }
                    }
                    catch { bancoConMusica = false; }
                }
                if (bkBuscador) CloseSearch();
                if (bkAtraccion) ExitAttract();
                if (panelSongs != null && panelSongs.Visible) BackToAlbums();

                // ---- LAS PORTADAS, con el monitor de siempre: su historial compara perfiles medidos asi ----
                try
                {
                    for (int i = 0; i < ef.Length; i++) PonNivel(ef[i].Clave, NivAlto);
                    // Mismo comienzo en cada comparacion; no borrar caches ni interrumpir el audio.
                    currentLetter = bkLetra; page = bkPag; selAlbumIndex = bkSel; RefreshAlbums();
                    int measuredProfile = caratulasCarga, measuredWidth = gpuPanel.Width, measuredHeight = gpuPanel.Height;
                    DateTime measuredTime = DateTime.Now;
                    bancoPortadas = MideEscena(EscPagina);
                    if (caratulasCarga != measuredProfile || gpuPanel.Width != measuredWidth || gpuPanel.Height != measuredHeight)
                        throw new InvalidOperationException("Cambio el perfil o la resolucion durante la medida; vuelve a medir.");
                    bancoPortadas.PerfilCarga = measuredProfile; bancoPortadas.Ancho = measuredWidth; bancoPortadas.Alto = measuredHeight;
                    bancoPortadas.Inicio = bkLetra + "/" + (bkPag + 1); bancoPortadas.Hora = measuredTime;
                    bancoPortadas.MusicaReal = bancoConMusica;
                    bancoPortadas.PreviasGpu = coverGovernor.ReadyAtFirstSeen; bancoPortadas.VistasGpu = coverGovernor.SeenCovers;
                    RegisterCoverMeasurement(bancoPortadas);
                }
                finally { for (int i = 0; i < ef.Length; i++) PonNivel(ef[i].Clave, bkNiv[i]); }

                // ---- EL MONITOR, SUELTO: desde aqui se ve lo que cuesta de verdad cada cuadro ----
                gpuPanel.CuentaArea = true;
                bancoSinEspera = gpuPanel.PideSinEspera(true);
                CalientaMedida(400);
                currentLetter = bkLetra; page = bkPag; selAlbumIndex = bkSel; RefreshAlbums();

                // ---- LAS CINCO PANTALLAS ----
                bancoEscenas.Add(MideEscena(EscPagina));
                bancoEscenas.Add(MideEscena(EscLetra));
                OpenSearch();
                bancoEscenas.Add(MideEscena(EscBuscador));
                CloseSearch();
                bancoEscenas.Add(MideEscena(EscDisco));
                if (panelSongs != null && panelSongs.Visible) BackToAlbums();
                EnterAttract();
                bancoEscenas.Add(MideEscena(EscAtraccion));
                ExitAttract();
                if (nowPlaying != null && !nowPlaying.IsVideo)
                {
                    PonEscena(EscReposo);
                    try { bancoEscenas.Add(MideEscena(EscReposo)); }
                    finally { QuitaEscena(EscReposo); }
                }

                // EL PEOR MANDA: incluir el estado extendido, aunque las demas pantallas vayan sobradas.
                EscenaMedida peor = null; int peorId = 0;
                for (int i = 0; i < bancoEscenas.Count; i++)
                    if (CosteDe(bancoEscenas[i]) > 0.0 && (peor == null || CosteDe(bancoEscenas[i]) > CosteDe(peor)))
                    { peor = bancoEscenas[i]; peorId = i; }
                if (peor == null) throw new InvalidOperationException("No se obtuvieron cuadros validos");
                bancoPeor = peor.Nombre; bancoPeorId = peorId; bancoMsPeor = CosteDe(peor); bancoFpsPeor = peor.Fps;
                bancoFrena = QueFrena(peor);

                // ---- LO QUE AHORRA CADA COSA, CONTRA ESA PANTALLA ----
                PonEscena(peorId);
                double base0 = MideCoste(peorId, MsPorEscalon);
                bancoAhorros = new double[NombreAhorro.Length];
                for (int i = 0; i < bancoAhorros.Length; i++) bancoAhorros[i] = double.NaN;
                if (base0 > bancoObjetivoAlto)
                {
                    // Cada ahorro ENTRE DOS medidas sin el: la diferencia entre esas dos es lo que tiembla la maquina.
                    double antes = base0;
                    for (int i = 0; i < bancoAhorros.Length; i++)
                    {
                        if (bkAhorro[i]) { bancoAhorros[i] = 0.0; continue; }       // ya estaba puesto: esta en la cuenta
                        if (!PuedeAhorro(i)) { bancoAhorros[i] = -1.0; continue; }  // esta tarjeta no puede
                        // El que va encima de otros (G2, sobre los dos primeros) se mide con esos puestos: lo que ahorra DE MAS.
                        int[] sobre = i < AhorroSobre.Length ? AhorroSobre[i] : null;
                        if (sobre != null)
                        {
                            foreach (int j in sobre) if (PuedeAhorro(j)) PonAhorro(j, true);
                            antes = MideCoste(peorId, MsPorEscalon);
                        }
                        PonAhorro(i, true);
                        double con = MideCoste(peorId, MsPorEscalon);
                        bool pudo = PuedeAhorro(i);   // pudo descubrir al usarlo que no le sale
                        PonAhorro(i, false);
                        double despues = MideCoste(peorId, MsPorEscalon);
                        double ruido = Math.Abs(antes - despues), ahorro = (antes + despues) / 2.0 - con;
                        bancoAhorros[i] = !pudo ? -1.0 : (ahorro > Math.Max(RuidoMs, ruido) ? ahorro : 0.0);
                        antes = despues;
                        if (sobre != null)
                        {
                            foreach (int j in sobre) PonAhorro(j, bkAhorro[j]);
                            if (i + 1 < bancoAhorros.Length) antes = MideCoste(peorId, MsPorEscalon);
                        }
                    }
                    double conAhorros = base0;
                    for (int i = 0; i < bancoAhorros.Length; i++) if (bancoAhorros[i] > 0) conAhorros -= bancoAhorros[i];
                    // Los escalones de efectos SOLO si los ahorros no bastan, y medidos con ellos puestos.
                    if (conAhorros > bancoObjetivoAlto)
                    {
                        for (int i = 0; i < bancoAhorros.Length; i++) if (bancoAhorros[i] > 0) PonAhorro(i, true);
                        double baseEf = MideCoste(peorId, MsPorEscalon);
                        for (int i = 0; i < ef.Length; i++)
                        {
                            int nivAntes = LeeNivel(ef[i].Clave);
                            ef[i].AhorroMedio = 0.0; ef[i].AhorroBajo = 0.0; ef[i].Medido = true;
                            if (nivAntes <= NivBajo) continue;   // ya estaba abajo del todo: no hay escalon que dar
                            PonNivel(ef[i].Clave, NivMedio);
                            double conMedio = MideCoste(peorId, MsPorEscalon);
                            PonNivel(ef[i].Clave, NivBajo);
                            double conBajo = MideCoste(peorId, MsPorEscalon);
                            PonNivel(ef[i].Clave, nivAntes);
                            double a1 = baseEf - conMedio, a2 = conMedio - conBajo;
                            ef[i].AhorroMedio = (a1 > 0.0) ? a1 : 0.0;
                            ef[i].AhorroBajo = (a2 > 0.0) ? a2 : 0.0;
                        }
                        bancoEfectosMedidos = true;
                        for (int i = 0; i < bancoAhorros.Length; i++) PonAhorro(i, bkAhorro[i]);
                    }
                }

                // ---- LA PROPUESTA. Sale de lo medido y de nada mas. ----
                double[] am = new double[ef.Length], ab = new double[ef.Length], ah = new double[bancoAhorros.Length];
                for (int i = 0; i < ef.Length; i++) { am[i] = ef[i].AhorroMedio; ab[i] = ef[i].AhorroBajo; }
                for (int i = 0; i < ah.Length; i++) ah[i] = bancoAhorros[i] > 0 ? bancoAhorros[i] : 0.0;
                bool[] usa; double msFinal; int meta;
                bancoPropuesta = PlanDeAhorros(base0, ah, am, ab, bancoObjetivoAlto, bancoObjetivoBajo, out usa, out msFinal, out meta);
                bancoUsaAhorro = usa; bancoMetaLograda = meta;
                bancoFpsPropuesta = FpsDe(msFinal, 0);

                // ---- Y SE COMPRUEBA, puesta de verdad ----
                bool cambia = false;
                for (int i = 0; i < ef.Length; i++) if (bancoPropuesta[i] != LeeNivel(ef[i].Clave)) cambia = true;
                for (int i = 0; i < usa.Length; i++) if (usa[i] && !bkAhorro[i]) cambia = true;
                if (cambia)
                {
                    for (int i = 0; i < ef.Length; i++) PonNivel(ef[i].Clave, bancoPropuesta[i]);
                    for (int i = 0; i < usa.Length; i++) if (usa[i]) PonAhorro(i, true);
                    bancoMsComprobado = MideCoste(peorId, MsPorEscena);
                    for (int i = 0; i < ef.Length; i++) PonNivel(ef[i].Clave, bkNiv[i]);
                    for (int i = 0; i < bkAhorro.Length; i++) PonAhorro(i, bkAhorro[i]);
                }
                else bancoMsComprobado = base0;
                QuitaEscena(peorId);
            }
            catch (Exception ex) { bancoPropuesta = null; bancoError = ex.Message; LogError("Medicion de GPU no valida", ex); }
            finally
            {
                // La rocola vuelve donde estaba, truene lo que truene por el camino: niveles, ahorros y el monitor.
                if (bancoReposoActivo) QuitaEscena(EscReposo);
                for (int i = 0; i < ef.Length; i++) PonNivel(ef[i].Clave, bkNiv[i]);
                for (int i = 0; i < bkAhorro.Length; i++) if (LeeAhorro(i) != bkAhorro[i]) PonAhorro(i, bkAhorro[i]);
                try { gpuPanel.CuentaArea = false; gpuPanel.PideSinEspera(false); } catch { }
                try
                {
                    // SOLO SE PARA LA QUE PUSO EL MEDIDOR. Si ya sonaba algo, es de alguien que lo pago.
                    if (bancoPuseYoLaCancion) DisposePlaybackNow();
                    if (searchOpen && !bkBuscador) CloseSearch();
                    if (panelSongs != null && panelSongs.Visible && !bkDisco) BackToAlbums();
                    if (attractOn && !bkAtraccion) ExitAttract();
                    currentLetter = bkLetra; page = bkPag; selAlbumIndex = bkSel;
                    RefreshAlbums();
                    if (bkAtraccion && !attractOn) EnterAttract();
                    else if (bkBuscador && !searchOpen) OpenSearch();
                }
                catch { }
                animTimer.Start();
            }
#endif
        }

#if ROCOLA_GPU
        // Deja puesta la pantalla que toca medir (y la quita despues). Sale aparte porque el coste de cada
        // escalon se mide varias veces sobre la MISMA pantalla.
        private bool bancoReposoActivo, bancoReposoTargetAnterior;
        private void PonEscena(int escena)
        {
            pasoEsc = 0;
            if (escena == EscBuscador) OpenSearch();
            else if (escena == EscAtraccion) EnterAttract();
            else if (escena == EscReposo && gpuPanel != null)
            {
                if (!bancoReposoActivo) { bancoReposoTargetAnterior = gpuPanel.IdleExpandTarget; }
                bancoReposoActivo = true; AplicaEfectosGpu(); gpuPanel.IdleExpandTarget = true;
            }
        }
        private void QuitaEscena(int escena)
        {
            if (escena == EscBuscador) { if (searchOpen) CloseSearch(); }
            else if (escena == EscAtraccion) { if (attractOn) ExitAttract(); }
            else if (escena == EscReposo && bancoReposoActivo)
            {
                bancoReposoActivo = false; AplicaEfectosGpu();
                if (gpuPanel != null) gpuPanel.IdleExpandTarget = bancoReposoTargetAnterior;
            }
            else if (panelSongs != null && panelSongs.Visible) BackToAlbums();
        }
#endif

        // =============================================================================================
        //  LA PAGINA "IMAGEN Y RENDIMIENTO"
        // =============================================================================================
        //  A la izquierda, el medidor con lo que salio y debajo la propuesta. A la derecha, de arriba
        //  abajo, la sub-ventana de los efectos: primero el mando general -Bajo, Medio, Alto de golpe- y
        //  luego cada cosa con sus tres posiciones y SU coste medido en este equipo.
        //
        //  Va toda dentro de un Panel para que cambiar de pestana sea una linea (ensenarlo o esconderlo) y
        //  la pagina de Sonido no se entere: se queda exactamente donde estaba, hasta el ultimo pixel.
        // =============================================================================================
        private bool f3Medir;          // se pulso "Medir este equipo": la ventana se cierra, mide, y vuelve
        private bool f3Diagnostico;    // se pulso "Diagnostico completo" (DiagnosticoCompleto.cs): la ventana se cierra, recorre las escenas, y vuelve
        private bool diagEnMarcha = false;       // el diagnostico completo esta corriendo (la atraccion no entra sola mientras tanto)
        private bool diagCancelado;      // el ultimo diagnostico se cancelo porque alguien toco la rocola: la ventana de ajustes NO se reabre
        private PlayItem diagMuda = null;       // la cancion MUDA que puso el diagnostico (nunca una de un cliente); null = ninguna
        private bool EsMudaDiag(PlayItem it) { return diagMuda != null && object.ReferenceEquals(it, diagMuda); }
        private string diagResumen = "";   // el resumen de dos renglones del ultimo diagnostico completo de esta sesion (y el informe que dejo)

        // El diagnostico completo. No es una preferencia: ni se guarda ni lleva interruptor. Con tarjeta grafica lo hace
        // Form1.DiagnosticoCompleto.cs; sin ella solo dice por que no hay nada que medir.
        private void DiagnosticoCompletoF3()
        {
#if ROCOLA_GPU
            CorreDiagnosticoGpu();
#else
            diagResumen = "No se hizo el diagnostico: esta rocola dibuja SIN tarjeta grafica.\nSin ella no hay nada que medir por este camino.";
#endif
        }
        private int f3Pagina;          // 0 = Sonido, 1 = Imagen (al volver de medir se abre en Imagen)
        private bool f3CambioGraf;     // se toco algo de graficos (para guardar al cerrar)

        // Cual de las seis pantallas salio peor: es la que manda sobre todo lo demas, y en la tabla se
        // senala para que se vea de un vistazo de donde salen los numeros de abajo.
        private bool EsLaPeor(int i)
        {
#if ROCOLA_GPU
            return bancoEscenas != null && i == bancoPeorId;
#else
            return false;
#endif
        }

        // ============================ LA PAGINA DE DETALLES ============================
        //
        // Los adornos que la rocola fue juntando -el turno, el palomito, el fondo con el tono de la portada,
        // las portaditas de la cola, el relevo del cubito y el astro del cielo- solo se podian apagar
        // escribiendo a mano en el archivo de configuracion. Para quien no toca archivos de texto, eso es lo
        // mismo que no poder apagarlos.
        //
        // Van en pestaña propia: las de Imagen se BAJAN para ganar rendimiento a cambio de que se vea menos
        // bonito, y por eso el medidor las puede proponer. Estas no cuestan nada que se note; son gusto.
        //
        // La pared que respira estuvo aqui hasta el 13-10-2026: se retiro porque con musica la pared se
        // oculta a los pocos segundos de reposo y la respiracion no se llegaba a ver (ver Form1.cs).
        //
        // Cada una lleva escrito QUE SE VE si se enciende, no como esta hecha por dentro. Quien abre esta
        // ventana quiere saber que va a cambiar en la pantalla del bar.
        private Grupo PaginaDetalles(Rocola.Nativo.Letra fNom, Rocola.Nativo.Letra fDes, Rocola.Nativo.Letra fBot,   // EN LINUX: Panel -> Grupo (Nativo/Rotulo.cs); Font -> Letra (Nativo/Letras.cs: la misma familia, tamano y estilo)
                                     Color cTexto, Color cTenue, Color cApagado)
        {
            Color cVida = Color.FromArgb(150, 220, 180);
            Color cFon = Color.FromArgb(140, 205, 255);

            Grupo pg = new Grupo();   // EN LINUX: Panel -> Grupo (Nativo/Rotulo.cs)
            pg.SetBounds(0, 96, 960, 636);
            pg.BackColor = Color.FromArgb(11, 14, 32);

            // Dos tarjetas de tres: lo que ve quien pide, y lo que pasa en el fondo y en la barra de abajo.
            Tarjeta tVida = new Tarjeta(); tVida.SetBounds(16, 4, 456, 280); tVida.Titulo = "AL PEDIR CANCIONES"; tVida.Acento = cVida;
            Tarjeta tFon = new Tarjeta(); tFon.SetBounds(488, 4, 456, 280); tFon.Titulo = "EL FONDO Y LA BARRA"; tFon.Acento = cFon;

            string[] nombre = {
                "Decir cuando suena tu cancion",
                "Marcar los discos que ya pediste",
                "Ensenar las portadas de la cola",
                "Que el fondo tome el color de la portada",
                "Media vuelta al cambiar de cancion",
                "Astro principal en el cielo",
            };
            string[] explica = {
                "Al pedir una cancion, el aviso dice que puesto ocupa y cuantos\nminutos faltan, en vez de solo \"Cancion agregada\".",
                "Un palomito verde en la esquina de los discos de los que ya hay\nalgo pedido o sonando, para no volver a pedir lo mismo.",
                "En la capsula de \"En cola\", las caratulas de las tres proximas.\nSi hay mas de tres, la ultima lleva un \"+2\".",
                "Del disco se toma SOLO de que color es. Lo oscuro y lo vivo lo\nsigue mandando el genero: ninguna portada estropea la pantalla.",
                "El cubito de la barra de abajo gira media vuelta y la portada\nnueva aparece en el camino, en vez de cambiar de golpe.",
                "Estrella de color musical y corona en GPU.\nEl renderizador CPU conserva la luna clasica.",
            };
            Interruptor[] sw = new Interruptor[nombre.Length];
            for (int i = 0; i < nombre.Length; i++)
            {
                Tarjeta t = (i < 3) ? tVida : tFon;
                int y = 40 + (i % 3) * 72;
                sw[i] = new Interruptor();
                sw[i].SetBounds(16, y, 424, 26); sw[i].ForeColor = cTexto; sw[i].Font = fNom;
                sw[i].Text = nombre[i]; sw[i].Acento = (i < 3) ? cVida : cFon;
                Rotulo le = new Rotulo(); le.SetBounds(16, y + 30, 424, 32); le.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
                le.ForeColor = cTenue; le.Font = fDes; le.Text = explica[i];
                t.Controls.Add(sw[i]); t.Controls.Add(le);
            }
            sw[0].Checked = turnoOn;
            sw[1].Checked = marcaColaOn;
            sw[2].Checked = colaPortadasOn;
            sw[3].Checked = fondoPortadaOn;
            sw[4].Checked = relevoOn;
            sw[5].Checked = lunaOn;

            Rotulo lPie = new Rotulo(); lPie.SetBounds(16, 508, 928, 48); lPie.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lPie.ForeColor = cApagado; lPie.Font = fDes;
            lPie.Text = "Nada de esto le quita ni le pone rendimiento a la rocola: no son las perillas de la pestaña de Imagen,\n"
                      + "que ahi si se cambia trabajo por bonito. Estas son de gusto, y apagarlas no gana nada mas que quitarlas\n"
                      + "de la vista. Lo que se toque aqui se guarda al momento.";

            BotonPlano bResetD = new BotonPlano();
            bResetD.SetBounds(16, 572, 300, 38); bResetD.Font = fBot; bResetD.Text = "Detalles como de fabrica";

            sw[0].CheckedChanged += delegate { turnoOn = sw[0].Checked; f3CambioGraf = true; };
            // La marca va DENTRO del mapa de bits horneado de cada casilla y su clave lleva el "#q", asi que
            // al apagarla la clave cambia sola y las casillas se rehacen: basta con pedir un repintado.
            sw[1].CheckedChanged += delegate { marcaColaOn = sw[1].Checked; f3CambioGraf = true; if (albumView != null) albumView.Invalidate(); };
            sw[2].CheckedChanged += delegate
            {
                colaPortadasOn = sw[2].Checked;
                f3CambioGraf = true;
                // La capsula cambia de ANCHO al llevar o no portaditas, asi que hay que reacomodar la fila.
                if (pastCola != null) { pastCola.ConPortadas = colaPortadasOn; LlenaPortaditas(); pastCola.Invalidate(); }
                LayoutControls();
            };
            sw[3].CheckedChanged += delegate
            {
                fondoPortadaOn = sw[3].Checked;
                f3CambioGraf = true;
                // Se ve YA, sin esperar a la siguiente cancion: el fondo viaja hasta el color nuevo con el
                // mismo fundido lento de siempre.
                if (nowPlaying != null && !nowPlaying.IsVideo) ApplyGenreBackground(nowPlaying.Song, nowPlaying.Disc);
            };
            sw[4].CheckedChanged += delegate { relevoOn = sw[4].Checked; f3CambioGraf = true; };
            // La luna NO va horneada en el cielo: se pega encima en cada cuadro, asi que con pedir un
            // repintado basta y no hay que rehacer nada.
            sw[5].CheckedChanged += delegate { lunaOn = sw[5].Checked; f3CambioGraf = true; RepintaFondo(); };
            bResetD.Click += delegate
            {
                turnoOn = true; marcaColaOn = true; fondoPortadaOn = true; colaPortadasOn = true;
                relevoOn = true; lunaOn = true;
                for (int k = 0; k < sw.Length; k++) sw[k].Checked = true;
                f3CambioGraf = true;
                RepintaFondo();   // la luna vuelve a encenderse: que se vea ya
                ShowToast("Detalles como de fabrica", Color.FromArgb(40, 130, 90));
            };

            pg.Controls.Add(tVida); pg.Controls.Add(tFon); pg.Controls.Add(lPie); pg.Controls.Add(bResetD);
            return pg;
        }

        // ================== LA QUINTA PESTAÑA: EL FONDO Y SUS ADORNOS ==================
        //
        // Once interruptores que estaban en "Imagen" y no eran de ahi. La regla de Imagen es que lo de alli
        // se BAJA para ganar rendimiento a cambio de que se vea menos bonito, y por eso el medidor lo puede
        // proponer. Estos once no quitan trabajo de ningun cuadro: se mira en el codigo y se ve.
        //
        //   Estrellas finas          multiplica un tamaño (D3D9Star, BuildStarVerts)
        //   Ondas suaves             multiplica tres numeros de la aurora
        //   Aurora con bandas        APAGARLO cuesta mas: añade dos pasadas de suavizado
        //   Nebulosas azul/violeta   elige un tinte
        //   Detalle natural          elige que mapa se hornea UNA vez
        //   Viñeta, halo             un dibujo mas, y el halo solo con disco abierto
        //   Los tres del progreso    la barra de abajo, que es del tamaño de un dedo
        //
        // Y no cabian donde estaban: con las dieciseis, la tarjeta EFECTOS media 1026 px en una pagina de
        // 636 y llevaba barra de desplazamiento. Tampoco cabian en Detalles (sus tarjetas llegan a 500 de
        // 636) ni en Colores (cuatro tarjetas apretadas que acaban en 576). Asi que pestaña propia, igual
        // que hicieron Detalles y Colores en su momento.
        private Grupo PaginaFondo(Rocola.Nativo.Letra fNom, Rocola.Nativo.Letra fVal, Rocola.Nativo.Letra fDes, Rocola.Nativo.Letra fBot,   // EN LINUX: Panel -> Grupo (Nativo/Rotulo.cs); Font -> Letra (Nativo/Letras.cs: la misma familia, tamano y estilo)
                                  Color cTexto, Color cTenue, Color cApagado)
        {
            Color cAc = Color.FromArgb(150, 190, 255);
            Grupo pg = new Grupo();   // EN LINUX: Panel -> Grupo (Nativo/Rotulo.cs)
            pg.SetBounds(0, 96, 960, 636);
            pg.BackColor = Color.FromArgb(11, 14, 32);

            Tarjeta tAd = new Tarjeta(); tAd.SetBounds(16, 4, 456, 496); tAd.Titulo = "LOS ADORNOS DEL FONDO"; tAd.Acento = cAc;
            Tarjeta tNat = new Tarjeta(); tNat.SetBounds(488, 4, 456, 496); tNat.Titulo = "CUANTO DETALLE NATURAL"; tNat.Acento = cAc;

            // ---------------- LOS ONCE ADORNOS ----------------
            string[] nom = {
                "Nebulosas azul / violeta",
                "Nebulosas con detalle natural",
                "Aurora con bandas mas definidas",
                "Ondas suaves",
                "Estrellas finas",
                "Viñeta suave",
                "Halo de seleccion",
                "Estado ampliado transparente",
                "Barra de progreso neon",
                "Neon de progreso intenso",
                "Progreso ultrafluido",
            };
            bool[] val = { nebulosasCine, cieloProfundo, auroraBandas, ondasSuaves, estrellasFinas,
                           vinetaSuave, haloSeleccion, estadoTransparente, progresoNeon, neonIntenso, progresoFluido };
            Interruptor[] sw = new Interruptor[nom.Length];
            for (int i = 0; i < sw.Length; i++)
            {
                sw[i] = new Interruptor();
                sw[i].SetBounds(16, 36 + i * 32, 424, 26); sw[i].ForeColor = cTexto; sw[i].Font = fNom;
                sw[i].Text = nom[i]; sw[i].Acento = cAc; sw[i].Checked = val[i];
                tAd.Controls.Add(sw[i]);
            }
            sw[0].CheckedChanged += delegate { nebulosasCine = sw[0].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            sw[1].CheckedChanged += delegate { cieloProfundo = sw[1].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            sw[2].CheckedChanged += delegate { auroraBandas = sw[2].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            sw[3].CheckedChanged += delegate { ondasSuaves = sw[3].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            sw[4].CheckedChanged += delegate { estrellasFinas = sw[4].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            sw[5].CheckedChanged += delegate { vinetaSuave = sw[5].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            sw[6].CheckedChanged += delegate { haloSeleccion = sw[6].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            sw[7].CheckedChanged += delegate { estadoTransparente = sw[7].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            sw[8].CheckedChanged += delegate { progresoNeon = sw[8].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            sw[9].CheckedChanged += delegate { neonIntenso = sw[9].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            sw[10].CheckedChanged += delegate { progresoFluido = sw[10].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };

            // Una nota para toda la pagina: con tarjeta, lo de siempre; sin ella, que lo de aqui no se ve (A1).
            Rotulo lAd = new Rotulo(); lAd.SetBounds(340, 598, 440, 32); lAd.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lAd.ForeColor = cApagado; lAd.Font = fDes;
            lAd.Text = "Todo lo de esta pagina es de la tarjeta grafica: el coste depende\ndel equipo. Pruebalos con musica; se apagan por separado.";
            Rotulo lAdSin = new Rotulo(); lAdSin.Bounds = lAd.Bounds; lAdSin.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lAdSin.ForeColor = Interruptor.ColorNecesita; lAdSin.Font = fDes; lAdSin.Text = TextoSinTarjeta; lAdSin.Visible = false;
            Interruptor swFlujo=new Interruptor();swFlujo.SetBounds(16,392,424,26);
            swFlujo.Font=fNom;swFlujo.ForeColor=cTexto;swFlujo.Acento=cAc;
            swFlujo.Text="Colores del espectro en movimiento";swFlujo.Checked=coloresFluidos;
            swFlujo.CheckedChanged+=delegate { coloresFluidos=swFlujo.Checked;AplicaEfectosGpu();f3CambioGraf=true; };
            tAd.Controls.Add(swFlujo);

            // ---------------- CUANTO DETALLE NATURAL ----------------
            Rotulo lNatN = new Rotulo(); lNatN.SetBounds(16, 40, 320, 20); lNatN.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lNatN.ForeColor = cTexto; lNatN.Font = fNom; lNatN.Text = "Fuerza del detalle natural";
            Rotulo lNatV = new Rotulo(); lNatV.SetBounds(340, 40, 100, 20); lNatV.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lNatV.ForeColor = cAc; lNatV.Font = fVal; lNatV.TextAlign = AlineaTexto.TopRight;   // EN LINUX: ContentAlignment -> AlineaTexto, los mismos numeros
            Rotulo lNatD = new Rotulo(); lNatD.SetBounds(16, 66, 424, 34); lNatD.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lNatD.ForeColor = cTenue; lNatD.Font = fDes;
            lNatD.Text = "Menos bruma deja ver mas el relieve y el degradado.";
            Deslizador barNat = new Deslizador(); barNat.SetBounds(16, 106, 424, 22);
            barNat.Minimum = 0; barNat.Maximum = 100; barNat.Acento = cAc;
            barNat.Value = (int)Math.Round(nebNaturalFuerza * 100f);
            tNat.Controls.Add(lNatN); tNat.Controls.Add(lNatV); tNat.Controls.Add(lNatD); tNat.Controls.Add(barNat);
            Interruptor swCristal = new Interruptor();
            swCristal.SetBounds(16, 140, 424, 26); swCristal.Font = fNom; swCristal.ForeColor = cTexto;
            swCristal.Text = "Interior de discos con cristal"; swCristal.Acento = cAc; swCristal.Checked = cancionesCristal;
            swCristal.CheckedChanged += delegate { cancionesCristal = swCristal.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            Interruptor swDoce = new Interruptor();
            swDoce.SetBounds(16, 176, 424, 26); swDoce.Font = fNom; swDoce.ForeColor = cTexto;
            swDoce.Text = "Cola ampliada de 12 canciones"; swDoce.Acento = cAc; swDoce.Checked = colaDoce;
            swDoce.CheckedChanged += delegate { colaDoce = swDoce.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            tNat.Controls.Add(swCristal); tNat.Controls.Add(swDoce);
            Rotulo lExtras = new Rotulo(); lExtras.SetBounds(16, 208, 424, 48); lExtras.Font = fDes;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lExtras.ForeColor = cTenue; lExtras.BackColor = Color.Transparent;
            lExtras.Text = "Apagados, vuelven el velo plano dentro del disco\ny las tres canciones del estado ampliado.";
            tNat.Controls.Add(lExtras);
            tNat.Height = 586;
            tAd.Height = 580;
            // "Transiciones suaves de color" se retiro el 13-10-2026: apagarla solo dejaba el buscador con el color de antes
            // al cambiar de cancion. Va siempre encendida (sin interruptor, como las demas cosas que no son una opcion).
            string[] extraNames = { "Vidrio por capas y color vivo", "Estrella viajera con orbitas", "Ocultar controles en reposo" };
            bool[] extraValues = { glassCapas, lunaActiva, controlesReposo };
            Interruptor[] extras = new Interruptor[extraNames.Length];
            for (int i = 0; i < extras.Length; i++)
            {
                extras[i] = new Interruptor(); extras[i].SetBounds(16, 434 + i * 36, 424, 26);
                extras[i].Font = fNom; extras[i].ForeColor = cTexto; extras[i].Acento = cAc;
                extras[i].Text = extraNames[i]; extras[i].Checked = extraValues[i]; tAd.Controls.Add(extras[i]);
            }
            extras[0].CheckedChanged += delegate { glassCapas = extras[0].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            extras[1].CheckedChanged += delegate { lunaActiva = extras[1].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            extras[2].CheckedChanged += delegate { controlesReposo = extras[2].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            tNat.Titulo = "DETALLE Y RESPUESTA";
            // "Respuesta inmediata al toque" (es rendimiento: apagarla solo hacia que la pantalla tardara mas en responder)
            // y "Ondas unidas al estado" (apagada, la aurora se despegaba del estado ampliado) se retiraron el 13-10-2026:
            // van siempre encendidas.
            string[] tactileNames = { "Transiciones de vista y seleccion" };
            bool[] tactileValues = { transicionesTacto };
            Interruptor[] tactile = new Interruptor[tactileNames.Length];
            for (int i = 0; i < tactile.Length; i++)
            {
                tactile[i] = new Interruptor(); tactile[i].SetBounds(16, 262 + i * 30, 424, 26);
                tactile[i].Font = fNom; tactile[i].ForeColor = cTexto; tactile[i].Acento = cAc;
                tactile[i].Text = tactileNames[i]; tactile[i].Checked = tactileValues[i]; tNat.Controls.Add(tactile[i]);
            }
            tactile[0].CheckedChanged += delegate { transicionesTacto = tactile[0].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };

            Rotulo lMoon = new Rotulo(); lMoon.SetBounds(16, 386, 424, 20);   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            Interruptor swPaleta = new Interruptor(); swPaleta.SetBounds(16, 354, 424, 26);
            swPaleta.Font = fNom; swPaleta.ForeColor = cTexto; swPaleta.Acento = cAc;
            swPaleta.Text = "Paleta completa de caratula"; swPaleta.Checked = paletaCompleta;
            swPaleta.CheckedChanged += delegate { paletaCompleta = swPaleta.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            tNat.Controls.Add(swPaleta);
            lMoon.Text = "ESTRELLA: superficie, corona y polvo independientes";
            lMoon.Font = fDes; lMoon.ForeColor = cTenue; lMoon.BackColor = Color.Transparent;
            tNat.Controls.Add(lMoon);
            string[] moonNames = { "Superficie detallada de la estrella", "Corona estelar", "Polvo estelar orbital", "Realzar estrella en reposo" };
            bool[] moonValues = { lunaRealista, haloLunar, polvoLunar, lunaProtagonista };
            Interruptor[] moonSwitches = new Interruptor[moonNames.Length];
            for (int i = 0; i < moonSwitches.Length; i++)
            {
                moonSwitches[i] = new Interruptor(); moonSwitches[i].SetBounds(16, 410 + i * 30, 424, 26);
                moonSwitches[i].Font = fNom; moonSwitches[i].ForeColor = cTexto; moonSwitches[i].Acento = cAc;
                moonSwitches[i].Text = moonNames[i]; moonSwitches[i].Checked = moonValues[i]; tNat.Controls.Add(moonSwitches[i]);
            }
            moonSwitches[0].CheckedChanged += delegate { lunaRealista = moonSwitches[0].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            moonSwitches[1].CheckedChanged += delegate { haloLunar = moonSwitches[1].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            moonSwitches[2].CheckedChanged += delegate { polvoLunar = moonSwitches[2].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            moonSwitches[3].CheckedChanged += delegate { lunaProtagonista = moonSwitches[3].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            Interruptor swEstela = new Interruptor(); swEstela.SetBounds(16, 530, 424, 26);
            swEstela.Font = fNom; swEstela.ForeColor = cTexto; swEstela.Acento = cAc;
            swEstela.Text = "Ecos de color de la estrella"; swEstela.Checked = estelaEstelar;
            swEstela.CheckedChanged += delegate { estelaEstelar = swEstela.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            tNat.Controls.Add(swEstela);
            // La estrella en 3D girando (Estrella3dGpu.cs). Apagado vuelve la estrella plana del icono, con sus ecos de antes.
            Interruptor sw3d = new Interruptor(); sw3d.SetBounds(16, 560, 424, 26);
            sw3d.Font = fNom; sw3d.ForeColor = cTexto; sw3d.Acento = cAc;
            sw3d.Text = "Estrella en 3D girando"; sw3d.Checked = estrella3d;
            sw3d.CheckedChanged += delegate { estrella3d = sw3d.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            tNat.Controls.Add(sw3d);

            // EL CIELO VIVO (CieloVivoGpu.cs, 12-10-2026): los adornos de Legacy, cada uno con su interruptor, en su hoja. (El
            // cometa se retiro el 13-10-2026.)
            string[] vivoNames = { "Luna con su fase real", "Lluvias de estrellas en sus fechas",
                "Fugaz al poner una cancion",
                "Mas profundidad al pasar de pagina", "Cielo segun la hora", "Reflejo de la aurora" };
            bool[] vivoValues = { lunaDelCielo, lluviasEstrellas, fugazFestejo, profundidadPagina, cieloPorHora, reflejoAurora };
            Interruptor[] vivo = new Interruptor[vivoNames.Length];
            for (int i = 0; i < vivo.Length; i++)
            {
                vivo[i] = new Interruptor(); vivo[i].SetBounds(16, 40 + (i % 3) * 40, 424, 26);
                vivo[i].Font = fNom; vivo[i].ForeColor = cTexto; vivo[i].Acento = cAc;
                vivo[i].Text = vivoNames[i]; vivo[i].Checked = vivoValues[i];
            }
            vivo[0].CheckedChanged += delegate { lunaDelCielo = vivo[0].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            vivo[1].CheckedChanged += delegate { lluviasEstrellas = vivo[1].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            vivo[2].CheckedChanged += delegate { fugazFestejo = vivo[2].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            vivo[3].CheckedChanged += delegate { profundidadPagina = vivo[3].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            vivo[4].CheckedChanged += delegate { cieloPorHora = vivo[4].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            vivo[5].CheckedChanged += delegate { reflejoAurora = vivo[5].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            Rotulo lVivo1 = new Rotulo(); lVivo1.BackColor = Color.Transparent; lVivo1.ForeColor = cTenue; lVivo1.Font = fDes;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lVivo1.Text = "La luna sigue su fase de verdad: cerca de la luna nueva\nno se ve. Las lluvias, en sus fechas (agosto: Perseidas;\ndiciembre: Geminidas).";
            Rotulo lVivo2 = new Rotulo(); lVivo2.BackColor = Color.Transparent; lVivo2.ForeColor = cTenue; lVivo2.Font = fDes;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lVivo2.Text = "Con la hora cambia solo el cielo de abajo: mas\nhondo de madrugada y calido al anochecer.";

            // EL PULIDO (PulidoGpu.cs, 14-10-2026): diez opciones visuales de la tarjeta, cada una con su interruptor, en su hoja.
            string[] pulNames = { "Cielo de muchas paradas", "Colores con mas profundidad", "Aurora mas limpia", "Interfaz sin escalones",
                "Estrella con facetas en degradado", "La estrella refleja el cielo", "Destello nitido en cruz", "Borde de luz en la estrella",
                "Reflejo de la estrella en el horizonte", "Brillo de cristal en la portada elegida" };
            Interruptor[] pul = new Interruptor[pulNames.Length];
            for (int i = 0; i < pul.Length; i++)
            {
                int k = i;
                pul[i] = new Interruptor(); pul[i].SetBounds(16, 40, 424, 26);
                pul[i].Font = fNom; pul[i].ForeColor = cTexto; pul[i].Acento = cAc;
                pul[i].Text = pulNames[i]; pul[i].Checked = LeePulido(i);
                pul[i].CheckedChanged += delegate { if (LeePulido(k) == pul[k].Checked) return; PonPulido(k, pul[k].Checked); AplicaEfectosGpu(); f3CambioGraf = true; };
            }
            Rotulo lPul1 = new Rotulo(); lPul1.BackColor = Color.Transparent; lPul1.ForeColor = cTenue; lPul1.Font = fDes;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lPul1.Text = "Los colores se mezclan como luz y no como numeros:\nlos cruces salen limpios y vivos. Los degradados,\nsin quiebres ni escalones.";
            Rotulo lPul2 = new Rotulo(); lPul2.BackColor = Color.Transparent; lPul2.ForeColor = cTenue; lPul2.Font = fDes;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lPul2.Text = "La estrella como metal pulido: refleja el cielo y el\ncolor de la cancion. Sin cara, como siempre.";

            BotonPlano bResetF = new BotonPlano();
            bResetF.SetBounds(16, 594, 300, 38); bResetF.Font = fBot; bResetF.Text = "Fondo como de fabrica";
            bResetF.TabIndex = 4;

            Action pinta = delegate
            {
                lNatV.Text = barNat.Value + " %";
                for (int i = 0; i < sw.Length; i++) sw[i].Invalidate();
            };
            // NO llama a RepintaFondo, y eso es a proposito. RepintaFondo tira la cache del cielo de la
            // CPU y rehace el fondo del genero, y este mando NO PUEDE cambiar ninguna de las dos cosas:
            // nebNaturalFuerza solo entra en BakeNatural, que vive dentro de #if ROCOLA_GPU. El cielo por
            // CPU usa NebFuerza, que es const y esta congelado. Era trabajo tirado, cien veces por
            // arrastre. Se hace lo mismo que los demas mandos que son solo de tarjeta: AplicaEfectosGpu.
            barNat.Scroll += delegate
            {
                nebNaturalFuerza = barNat.Value / 100f;
                f3CambioGraf = true; pinta(); AplicaEfectosGpu();
            };
            bResetF.Click += delegate
            {
                nebulosasCine = true; cieloProfundo = true; auroraBandas = true; ondasSuaves = true;
                estrellasFinas = true; vinetaSuave = true; haloSeleccion = true; estadoTransparente = true;
                progresoNeon = true; neonIntenso = true; progresoFluido = true;
                cancionesCristal = true; colaDoce = true; swCristal.Checked = true; swDoce.Checked = true;
                transicionesTacto = true;
                glassCapas = lunaActiva = true;
                controlesReposo = true;
                lunaRealista = haloLunar = polvoLunar = lunaProtagonista = true;
                estelaEstelar = true; swEstela.Checked = true;
                estrella3d = true; sw3d.Checked = true;
                coloresFluidos = true; swFlujo.Checked = true;
                paletaCompleta = true; swPaleta.Checked = true;
                for (int i = 0; i < moonSwitches.Length; i++) moonSwitches[i].Checked = true;
                for (int i = 0; i < extras.Length; i++) extras[i].Checked = true;
                for (int i = 0; i < tactile.Length; i++) tactile[i].Checked = true;
                for (int i = 0; i < vivo.Length; i++) vivo[i].Checked = true;   // y el cielo vivo, encendido
                for (int i = 0; i < pul.Length; i++) { PonPulido(i, true); pul[i].Checked = true; }   // y el pulido
                nebNaturalFuerza = NebNaturalDef;
                for (int i = 0; i < sw.Length; i++) sw[i].Checked = true;
                barNat.Value = (int)Math.Round(NebNaturalDef * 100f);
                f3CambioGraf = true; AplicaEfectosGpu(); pinta(); RepintaFondo();
                ShowToast("Fondo como de fabrica", Color.FromArgb(40, 130, 90));
            };

            pg.Controls.Add(tAd); pg.Controls.Add(tNat); pg.Controls.Add(bResetF);
            pg.Controls.Add(lAd); pg.Controls.Add(lAdSin);
            IPieza[][] groups = {   // EN LINUX: Control -> IPieza (Nativo/Pieza.cs: lo que la hoja le pide a cada pieza)
                new IPieza[] { sw[0], sw[1], sw[2], sw[3], sw[4], sw[5], swFlujo, swPaleta },   // EN LINUX: Control -> IPieza (Nativo/Pieza.cs: lo que la hoja le pide a cada pieza)
                new IPieza[] { lNatN, lNatV, lNatD, barNat },   // EN LINUX: Control -> IPieza (Nativo/Pieza.cs: lo que la hoja le pide a cada pieza)
                new IPieza[] { sw[6], sw[7], sw[8], sw[9], sw[10], swDoce },   // EN LINUX: Control -> IPieza (Nativo/Pieza.cs: lo que la hoja le pide a cada pieza)
                new IPieza[] { swCristal, extras[0], tactile[0], lExtras },   // EN LINUX: Control -> IPieza (Nativo/Pieza.cs: lo que la hoja le pide a cada pieza)
                new IPieza[] { moonSwitches[0], moonSwitches[1], moonSwitches[2], moonSwitches[3], sw3d, swEstela },   // EN LINUX: Control -> IPieza (Nativo/Pieza.cs: lo que la hoja le pide a cada pieza)
                new IPieza[] { extras[1], extras[2], lMoon },   // EN LINUX: Control -> IPieza (Nativo/Pieza.cs: lo que la hoja le pide a cada pieza)
                new IPieza[] { vivo[0], vivo[1], vivo[2], lVivo1 },   // EN LINUX: Control -> IPieza (Nativo/Pieza.cs: lo que la hoja le pide a cada pieza)
                new IPieza[] { vivo[3], vivo[4], vivo[5], lVivo2 },   // EN LINUX: Control -> IPieza (Nativo/Pieza.cs: lo que la hoja le pide a cada pieza)
                new IPieza[] { pul[0], pul[1], pul[2], pul[3], lPul1 },   // EN LINUX: Control -> IPieza (Nativo/Pieza.cs: lo que la hoja le pide a cada pieza)
                new IPieza[] { pul[4], pul[5], pul[6], pul[7], pul[8], pul[9], lPul2 }   // EN LINUX: Control -> IPieza (Nativo/Pieza.cs: lo que la hoja le pide a cada pieza)
            };
            MontaHojasFondo(pg, tAd, tNat, groups, fNom, fDes, fBot, cTexto, cTenue);

            // A1: LO QUE DEPENDE DE OTRO, gris y diciendo que le falta. Cada uno lee el AJUSTE, no el interruptor: asi
            // vale aunque el requisito este en otra pagina. Va despues de MontaHojasFondo, que acomoda las filas.
            NecesitaMotivo(sw[7], delegate
            {
                string falta = MotivoSinEstadoExtendido();
                if (falta != null) return falta;
                return estadoOpacoPerf ? "Sin efecto: estado opaco (Performance)" : null;
            });
            NecesitaMotivo(sw[2], MotivoSinAurora);
            NecesitaMotivo(sw[3], MotivoSinAurora);
            NecesitaMotivo(vivo[5], MotivoSinAurora);
            NecesitaMotivo(pul[2], MotivoSinAurora);
            Necesita(sw[9], delegate { return progresoNeon; }, "Barra de progreso neon (esta hoja)");
            NecesitaMotivo(swDoce, MotivoSinEstadoExtendido);
            Necesita(swCristal, delegate { return cancionesTransparentes; }, "Lista de canciones transparente (Imagen)");
            Necesita(swPaleta, delegate { return fondoPortadaOn; }, "Que el fondo tome el color de la portada (Detalles)");
            Necesita(barNat, delegate { return cieloProfundo; }, "Nebulosas con detalle natural (hoja Cielo y aurora)", lNatD);
            Necesita(swEstela, delegate { return lunaActiva; }, "Estrella viajera con orbitas (esta hoja)");
            NecesitaMotivo(extras[2], MotivoSinEstadoExtendido);
            Necesita(vivo[3], delegate { return paginaDesliza; }, "Pasar de pagina deslizando (Pantalla)");
            // El pulido de la estrella es de la estrella en 3D (la de fabrica).
            for (int i = 4; i <= 8; i++) Necesita(pul[i], delegate { return estrella3d; }, "Estrella en 3D girando (hoja Estrella)");
            // Y todo lo de esta pagina es de la tarjeta grafica.
            SoloConTarjeta(sw); SoloConTarjeta(swFlujo, swPaleta, barNat, swCristal, swDoce, swEstela, sw3d);
            SoloConTarjeta(extras); SoloConTarjeta(tactile); SoloConTarjeta(moonSwitches); SoloConTarjeta(vivo); SoloConTarjeta(pul);
            AvisoSinTarjeta(lAdSin, lAd);
            pinta();
            return pg;
        }

        // ================== LA CUARTA PESTAÑA: LOS COLORES DEL FONDO ==================
        //
        // Los colores del cielo eran numeros fijos dentro del programa, y quien queria otro fondo no tenia
        // como pedirlo. Aqui hay tres perillas, y las tres se eligen MIRANDO la pared, que esta detras de
        // esta ventana: se toca una y el fondo cambia al instante.
        //
        // NO CABIAN EN "DETALLES": sus dos tarjetas ocupan de y=4 a y=428 de 636 y debajo ya estan el pie y
        // el boton de fabrica. Es la misma razon por la que Detalles nacio de su propia pestaña cuando las
        // tarjetas de Imagen se llenaron. Y ademas no son lo mismo: aquellas son adornos que se encienden y
        // se dejan; estas cambian de que color es la rocola.
        private Grupo PaginaColores(Rocola.Nativo.Letra fNom, Rocola.Nativo.Letra fDes, Rocola.Nativo.Letra fBot,   // EN LINUX: Panel -> Grupo (Nativo/Rotulo.cs); Font -> Letra (Nativo/Letras.cs: la misma familia, tamano y estilo)
                                    Color cTexto, Color cTenue, Color cApagado)
        {
            Color cAc = Color.FromArgb(190, 165, 255);
            Color cOn = Color.FromArgb(28, 62, 104), cOff = Color.FromArgb(28, 34, 62);
            Color cBordeOn = Color.FromArgb(150, 205, 255), cBordeOff = Color.FromArgb(60, 72, 118);

            Grupo pg = new Grupo();   // EN LINUX: Panel -> Grupo (Nativo/Rotulo.cs)
            pg.SetBounds(0, 96, 960, 636);
            pg.BackColor = Color.FromArgb(11, 14, 32);

            // LAS CUATRO TARJETAS VAN APRETADAS, y esta medido. La pagina son 636 px y no puede crecer: debajo
            // esta la banda del sello de version. Antes eran tres tarjetas holgadas (168+156+156) que dejaban
            // 26 px de aire cada una; ahora son cuatro (148+134+134+146) que acaban en 576, con el pie y el
            // boton de fabrica repartidos a lo ancho en la ultima banda en vez de uno debajo del otro.
            Tarjeta t1 = new Tarjeta(); t1.SetBounds(16, 2, 928, 148); t1.Titulo = "DE QUE COLOR ES CADA CANCION"; t1.Acento = cAc;
            Tarjeta t2 = new Tarjeta(); t2.SetBounds(16, 154, 928, 134); t2.Titulo = "CUANTO AZUL"; t2.Acento = cAc;
            Tarjeta t3 = new Tarjeta(); t3.SetBounds(16, 292, 928, 134); t3.Titulo = "CUANTO NEGRO"; t3.Acento = cAc;
            Tarjeta t4 = new Tarjeta(); t4.SetBounds(16, 430, 928, 146); t4.Titulo = "CUANTO AIRE HAY EN EL CIELO"; t4.Acento = cAc;

            // ---------------- 1) quien manda en el tono: el genero o la caratula ----------------
            Rotulo l1 = new Rotulo(); l1.SetBounds(16, 36, 896, 62); l1.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            l1.ForeColor = cTenue; l1.Font = fDes;
            l1.Text = "Los botones dan al tono del DISCO un peso de 20%, 50% o 100% en el acento; el resto viene del GENERO.\n"
                    + "Paleta completa (en Fondo): hasta ocho colores del disco. BTS/Bangtan con genero conserva el morado fijo.\n"
                    + "Sin genero reconocido, manda todo el tono disponible del disco, incluso con Mitad y mitad.\n"
                    + "Se conservan brillo, viveza y degradados del cielo; no se copia el RGB de la portada como un fondo plano.";
            string[] nom1 = { "Disco 20%", "Mitad y mitad", "Disco 100%" };
            string[] val1 = { "20", "50", "100" };
            BotonPlano[] b1 = new BotonPlano[3];
            for (int k = 0; k < 3; k++)
            {
                b1[k] = new BotonPlano(); b1[k].SetBounds(16 + k * 200, 110, 192, 30);
                b1[k].Font = fBot; b1[k].Text = nom1[k];
                t1.Controls.Add(b1[k]);
            }

            // ---------------- 2) cuanto azul lleva el medio del cielo ----------------
            Rotulo l2 = new Rotulo(); l2.SetBounds(16, 36, 896, 48); l2.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            l2.ForeColor = cTenue; l2.Font = fDes;
            l2.Text = "El medio del cielo es azul noche y casi no depende de la cancion, a proposito: es lo que hace que parezca cielo.\n"
                    + "Bajando el azul, el color de la cancion sube desde el horizonte y tine mucha mas pantalla.";
            string[] nom2 = { "Poco azul", "Normal", "Todo azul" };
            string[] val2 = { "35", "70", "100" };
            BotonPlano[] b2 = new BotonPlano[3];
            for (int k = 0; k < 3; k++)
            {
                b2[k] = new BotonPlano(); b2[k].SetBounds(16 + k * 200, 94, 192, 30);
                b2[k].Font = fBot; b2[k].Text = nom2[k];
                t2.Controls.Add(b2[k]);
            }

            // ---------------- 3) cuanto negro hay arriba ----------------
            Rotulo l3 = new Rotulo(); l3.SetBounds(16, 36, 896, 48); l3.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            l3.ForeColor = cTenue; l3.Font = fDes;
            l3.Text = "Arriba del todo el cielo es casi negro, y ahi es donde mejor se ven las estrellas. Este es el punto donde acaba\n"
                    + "ese negro y empieza el azul: mas negro deja una noche mas cerrada, menos negro deja una pantalla mas encendida.";
            string[] nom3 = { "Mas negro", "Normal", "Menos negro" };
            string[] val3 = { "58", "42", "30" };
            BotonPlano[] b3 = new BotonPlano[3];
            for (int k = 0; k < 3; k++)
            {
                b3[k] = new BotonPlano(); b3[k].SetBounds(16 + k * 200, 94, 192, 30);
                b3[k].Font = fBot; b3[k].Text = nom3[k];
                t3.Controls.Add(b3[k]);
            }

            // ---------------- 4) cuanto aire hay delante de las estrellas ----------------
            // Son tres numeros -cuanto se apaga, cuanto enrojece y cuanto centellea la estrella de abajo- pero
            // UN SOLO mando, porque son la misma cosa: cuanto aire hay delante. Un cielo con las de abajo
            // apagadas pero igual de blancas no es medio efecto, es un cielo raro.
            // Y SI AHORA MISMO NO HAY TARJETA GRAFICA, QUE LO DIGA, y en la tercera linea en vez de en una
            // cuarta: la tarjeta esta medida al milimetro y no hay sitio para una linea de mas. Esto se hace
            // estrella por estrella al dibujar, y sin tarjeta las estrellas van horneadas en capas que se
            // deslizan: no se les puede atar nada a la altura de la pantalla. El mando sigue vivo -se guarda
            // y valdra en el equipo que si la tenga-, pero un mando que no hace nada y no lo dice es peor
            // que no tenerlo.
            bool conTarjeta = false;
#if ROCOLA_GPU
            conTarjeta = (gpuEnabled && gpuPanel != null);
#endif
            Rotulo l4 = new Rotulo(); l4.SetBounds(16, 36, 896, 62); l4.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            l4.ForeColor = cTenue; l4.Font = fDes;
            l4.Text = "En un cielo de verdad, mirando al horizonte se atraviesa muchisimo mas aire que mirando arriba: las estrellas\n"
                    + "de abajo salen mas apagadas, tiran a naranja y centellean mucho mas. Eso es lo que le da FONDO al cielo.\n"
                    + (conTarjeta
                       ? "Y ademas le deja el sitio al resplandor del genero, que vive justo ahi abajo. En PLANO salen todas iguales."
                       : "AHORA MISMO esta rocola dibuja SIN tarjeta grafica: esto se guarda, pero aqui no se va a ver.");
            string[] nom4 = { "Plano", "Suave", "Normal", "Fuerte" };
            string[] val4 = { "0", "1", "2", "3" };
            BotonPlano[] b4 = new BotonPlano[4];
            for (int k = 0; k < 4; k++)
            {
                b4[k] = new BotonPlano(); b4[k].SetBounds(16 + k * 200, 108, 192, 30);
                b4[k].Font = fBot; b4[k].Text = nom4[k];
                t4.Controls.Add(b4[k]);
            }
            t1.Controls.Add(l1); t2.Controls.Add(l2); t3.Controls.Add(l3); t4.Controls.Add(l4);

            // Termina antes del boton Listo (x 804 de la ventana): antes llegaba al borde y, con una letra mas ancha,
            // el final de la frase caia debajo del boton.
            Rotulo lPie = new Rotulo(); lPie.SetBounds(330, 580, 460, 46); lPie.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lPie.ForeColor = cApagado; lPie.Font = fDes;
            lPie.Text = "La pared esta detras de esta ventana: toca una y mirala.\n"
                      + "Todo se guarda al momento. El coste depende del equipo:\n"
                      + "compruebalo con el medidor (Imagen).";

            BotonPlano bReset = new BotonPlano();
            bReset.SetBounds(16, 580, 300, 38); bReset.Font = fBot; bReset.Text = "Colores como de fabrica";

            // ---------------- lo que se ensena ----------------
            Action pinta = delegate
            {
                Pinta3(b1, cual1(), cOn, cOff, cBordeOn, cBordeOff, cApagado);
                Pinta3(b2, cual2(), cOn, cOff, cBordeOn, cBordeOff, cApagado);
                Pinta3(b3, cual3(), cOn, cOff, cBordeOn, cBordeOff, cApagado);
                Pinta3(b4, AirePaso, cOn, cOff, cBordeOn, cBordeOff, cApagado);   // aqui el paso ES el numero
            };
            for (int k = 0; k < 3; k++)
            {
                int mio = k;
                // SIEMPRE por la funcion que acota, nunca poniendo el numero a mano: es la misma regla que
                // sigue la fuerza de la respiracion, y la que hace que el archivo no pueda romper nada.
                b1[k].Click += delegate { portadaManda = PesoPortada(val1[mio]); f3CambioGraf = true; RepintaFondo(); pinta(); };
                b2[k].Click += delegate { cieloAzul = AzulCielo(val2[mio]); f3CambioGraf = true; RepintaFondo(); pinta(); };
                b3[k].Click += delegate { cieloCorte = CorteCielo(val3[mio]); f3CambioGraf = true; RepintaFondo(); pinta(); };
            }
            for (int k = 0; k < 4; k++)
            {
                int mio = k;
                b4[k].Click += delegate { airePaso = PasoDelAire(val4[mio]); f3CambioGraf = true; RepintaFondo(); pinta(); };
            }
            bReset.Click += delegate
            {
                portadaManda = PesoPortada("50"); cieloAzul = AzulCielo("100"); cieloCorte = CorteCielo("42");
                airePaso = PasoDelAire("2");
                f3CambioGraf = true; RepintaFondo(); pinta();
                ShowToast("Colores como de fabrica", Color.FromArgb(40, 130, 90));
            };

            pg.Controls.Add(t1); pg.Controls.Add(t2); pg.Controls.Add(t3); pg.Controls.Add(t4);
            pg.Controls.Add(lPie); pg.Controls.Add(bReset);
            // A1: el peso del disco solo cuenta si el fondo toma el color de la portada (Detalles). Lo dice al lado.
            Rotulo lNec1 = new Rotulo(); lNec1.SetBounds(620, 108, 292, 34); lNec1.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lNec1.ForeColor = cTenue; lNec1.Font = fDes; lNec1.Text = "";
            t1.Controls.Add(lNec1);
            for (int k = 0; k < b1.Length; k++)
                Necesita(b1[k], delegate { return fondoPortadaOn; }, "Que el fondo tome el color\nde la portada (Detalles)", k == 0 ? lNec1 : null);
            pinta();
            return pg;
        }

        // Cual de los tres pasos esta puesto ahora mismo, para cada perilla.
        private int cual1() { return (portadaManda < 0.35f) ? 0 : (portadaManda > 0.75f ? 2 : 1); }
        private int cual2() { return (cieloAzul < 0.52f) ? 0 : (cieloAzul > 0.85f ? 2 : 1); }
        private int cual3() { return (cieloCorte > 0.50f) ? 0 : (cieloCorte < 0.36f ? 2 : 1); }

        // Enciende el paso elegido de una fila de tres botones.
        private static void Pinta3(BotonPlano[] b, int cual, Color cOn, Color cOff, Color cBordeOn, Color cBordeOff, Color cApagado)
        {
            for (int k = 0; k < b.Length; k++)
            {
                bool act = (k == cual);
                b[k].Relleno = act ? cOn : cOff;
                b[k].Borde = act ? cBordeOn : cBordeOff;
                b[k].ForeColor = act ? Color.FromArgb(226, 240, 255) : cApagado;
                b[k].Invalidate();
            }
        }

        private Grupo PaginaImagen(Rocola.Nativo.Letra fNom, Rocola.Nativo.Letra fVal, Rocola.Nativo.Letra fDes, Rocola.Nativo.Letra fBot,   // EN LINUX: Panel -> Grupo (Nativo/Rotulo.cs); Font -> Letra (Nativo/Letras.cs: la misma familia, tamano y estilo)
                                   Color cTexto, Color cTenue, Color cApagado)
        {
            Color cMide = Color.FromArgb(140, 205, 255);
            Color cEfe = Color.FromArgb(178, 152, 255);
            Color cProp = Color.FromArgb(255, 176, 102);
            Color cBien = Color.FromArgb(120, 230, 165);
            Color cMal = Color.FromArgb(255, 170, 110);
            Color cOn = Color.FromArgb(28, 62, 104), cOff = Color.FromArgb(28, 34, 62);
            Color cBordeOn = Color.FromArgb(150, 205, 255), cBordeOff = Color.FromArgb(60, 72, 118);

            Grupo pg = new Grupo();   // EN LINUX: Panel -> Grupo (Nativo/Rotulo.cs)
            pg.SetBounds(0, 96, 960, 636);
            pg.BackColor = Color.FromArgb(11, 14, 32);

            Tarjeta tMide = new Tarjeta(); tMide.SetBounds(16, 4, 456, 400); tMide.Titulo = "ESTE EQUIPO"; tMide.Acento = cMide;
            Tarjeta tProp = new Tarjeta(); tProp.SetBounds(16, 412, 456, 160); tProp.Titulo = "LA PROPUESTA"; tProp.Acento = cProp;
            // Las filas y los interruptores comparten un cursor vertical: no solapar
            // los cinco mandos con los ultimos niveles. Cabe en los 636 px de la pagina.
            Tarjeta tEfe = new Tarjeta(); tEfe.SetBounds(488, 4, 456, 624); tEfe.Titulo = "EFECTOS"; tEfe.Acento = cEfe;

            // ---------------- ESTE EQUIPO ----------------
            BotonPlano bMedir = new BotonPlano();
            bMedir.SetBounds(16, 40, 208, 36); bMedir.Font = fBot; bMedir.Text = "Medir este equipo";
            bMedir.Relleno = cOn; bMedir.Borde = cMide;
            // El DIAGNOSTICO COMPLETO (23-10-2026): varias escenas duras, tirones, procesador por hilo e informe a un archivo.
            BotonPlano bDiag = new BotonPlano();
            bDiag.SetBounds(232, 40, 208, 36); bDiag.Font = fBot; bDiag.Text = "Diagnóstico completo";
            bDiag.Relleno = cOn; bDiag.Borde = cMide;
            // EN LINUX: (XP) bDiag.AccessibleName = "Diagnóstico completo: varios minutos, escribe un informe"; -- el nombre, el papel o la descripcion para los lectores de pantalla de Windows (MSAA): en Rocola OS no hay, y nada de XP los lee
            Rotulo lTarda = new Rotulo(); lTarda.SetBounds(16, 82, 424, 32); lTarda.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lTarda.ForeColor = cTenue; lTarda.Font = fDes;
            const string textoTarda = "Medir: unos minutos. Diagnóstico: 3 a 6 min e informe.\nIncluyen el estado extendido. Música actual, o una muda.";
            lTarda.Text = textoTarda;
            BotonPlano bHistorial = new BotonPlano(); bHistorial.SetBounds(328, 590, 144, 38);
            bHistorial.Font = fBot; bHistorial.Text = "Comparar perfiles";
            bHistorial.Enabled = historialCaratulas != null && historialCaratulas.Count > 0;
            bHistorial.Click += delegate { ShowCoverHistory(); }; pg.Controls.Add(bHistorial);
            // LA TABLA, EN COLUMNAS (el medidor renovado): lo que tarda un cuadro tipico, lo que es del procesador, lo que
            // espera a la tarjeta y los cuadros por segundo que da eso. Una etiqueta por celda: con letra proporcional, los
            // espacios no alinean.
            int[] colX = { 16, 176, 238, 300, 362 }, colW = { 160, 62, 62, 62, 78 };
            string[] cabeza = { "PANTALLA", "CUADRO", "PROCESO", "TARJETA", "CUADROS/S" };
            Rotulo[] lCab = new Rotulo[colX.Length];   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            for (int c = 0; c < colX.Length; c++)
            {
                lCab[c] = new Rotulo(); lCab[c].SetBounds(colX[c], 120, colW[c], 16); lCab[c].BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
                lCab[c].ForeColor = cApagado; lCab[c].Font = fDes; lCab[c].Text = cabeza[c];
                lCab[c].TextAlign = c == 0 ? AlineaTexto.TopLeft : AlineaTexto.TopRight;   // EN LINUX: ContentAlignment -> AlineaTexto, los mismos numeros
                tMide.Controls.Add(lCab[c]);
            }
            Rotulo[,] lCelda = new Rotulo[NombreEscena.Length, colX.Length];   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            for (int i = 0; i < NombreEscena.Length; i++)
                for (int c = 0; c < colX.Length; c++)
                {
                    Rotulo l = new Rotulo(); l.SetBounds(colX[c], 140 + i * 18, colW[c], 18);   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
                    l.BackColor = Color.Transparent; l.ForeColor = cTexto; l.Font = fDes;
                    l.TextAlign = c == 0 ? AlineaTexto.TopLeft : AlineaTexto.TopRight;   // EN LINUX: ContentAlignment -> AlineaTexto, los mismos numeros
                    lCelda[i, c] = l; tMide.Controls.Add(l);
                }
            Rotulo lTecho = new Rotulo(); lTecho.SetBounds(16, 252, 424, 34); lTecho.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lTecho.ForeColor = cTenue; lTecho.Font = fDes;
            Rotulo lSinMus = new Rotulo(); lSinMus.SetBounds(16, 288, 424, 32); lSinMus.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lSinMus.ForeColor = Color.FromArgb(255, 196, 96); lSinMus.Font = fDes;
            Rotulo lVer = new Rotulo(); lVer.SetBounds(16, 322, 424, 68); lVer.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lVer.ForeColor = cTenue; lVer.Font = fDes;
            tMide.Controls.Add(bMedir); tMide.Controls.Add(bDiag); tMide.Controls.Add(lTarda);
            tMide.Controls.Add(lTecho); tMide.Controls.Add(lSinMus); tMide.Controls.Add(lVer);

            // ---------------- LA PROPUESTA ----------------
            Rotulo lProp = new Rotulo(); lProp.SetBounds(16, 36, 424, 70); lProp.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lProp.ForeColor = cTenue; lProp.Font = fDes;
            BotonPlano bApli = new BotonPlano();
            bApli.SetBounds(16, 112, 208, 34); bApli.Font = fBot; bApli.Text = "Aplicar lo recomendado";
            // Solo lo que casi no se nota: los ahorros de Performance, sin tocar los efectos.
            BotonPlano bSoloPerf = new BotonPlano();
            bSoloPerf.SetBounds(232, 112, 208, 34); bSoloPerf.Font = fBot; bSoloPerf.Text = "Solo Performance";
            // EN LINUX: (XP) bSoloPerf.AccessibleName = "Encender solo los ahorros de Performance"; -- el nombre, el papel o la descripcion para los lectores de pantalla de Windows (MSAA): en Rocola OS no hay, y nada de XP los lee
            tProp.Controls.Add(lProp); tProp.Controls.Add(bApli); tProp.Controls.Add(bSoloPerf);

            // ---------------- EFECTOS: el mando general y las seis filas ----------------
            EfectoGrafico[] ef = Efectos();
            Rotulo lGen = new Rotulo(); lGen.SetBounds(16, 38, 424, 18); lGen.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lGen.ForeColor = cApagado; lGen.Font = fDes;
            // CUATRO DE GOLPE: Bajo, Medio y Alto como siempre, y PERFORMANCE (13-10-2026): los efectos en Alto con los dos
            // ahorros que casi no se notan (hoja Performance). Alto es la pantalla de siempre, sin ahorros; Bajo y Medio
            // son para ganar rendimiento, asi que llevan los ahorros puestos.
            const int Performance = 3;
            BotonPlano[] bGen = new BotonPlano[4];
            string[] nomNivel = { "Bajo", "Medio", "Alto" };
            string[] nomGen = { "Bajo", "Medio", "Alto", "Performance" };
            for (int k = 0; k < bGen.Length; k++)
            {
                // Performance es la palabra larga: con la letra mas ancha (DejaVu Sans) mide 103 px.
                bGen[k] = new BotonPlano(); bGen[k].SetBounds(16 + k * 100, 58, k < 3 ? 92 : 124, 28);
                bGen[k].Font = fBot; bGen[k].Text = nomGen[k];
                // EN LINUX: (XP) bGen[k].AccessibleName = "Todo de golpe: " + nomGen[k]; -- el nombre, el papel o la descripcion para los lectores de pantalla de Windows (MSAA): en Rocola OS no hay, y nada de XP los lee
                tEfe.Controls.Add(bGen[k]);
            }
            Rotulo[] lNom = new Rotulo[ef.Length], lCost = new Rotulo[ef.Length];   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            BotonPlano[][] bNiv = new BotonPlano[ef.Length][];
            for (int i = 0; i < ef.Length; i++)
            {
                // 48 px de contenido y 4 de separacion; fuentes y botones intactos.
                int y = 94 + i * 52;
                lNom[i] = new Rotulo(); lNom[i].SetBounds(16, y, 424, 22); lNom[i].BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
                lNom[i].ForeColor = cTexto; lNom[i].Font = fNom; lNom[i].Text = ef[i].Nombre;
                lCost[i] = new Rotulo(); lCost[i].SetBounds(322, y + 24, 118, 18); lCost[i].BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
                lCost[i].ForeColor = cApagado; lCost[i].Font = fDes; lCost[i].TextAlign = AlineaTexto.TopRight;   // EN LINUX: ContentAlignment -> AlineaTexto, los mismos numeros
                bNiv[i] = new BotonPlano[3];
                for (int k = 0; k < 3; k++)
                {
                    bNiv[i][k] = new BotonPlano(); bNiv[i][k].SetBounds(16 + k * 102, y + 22, 96, 26);
                    bNiv[i][k].Font = fBot; bNiv[i][k].Text = nomNivel[k];
                    tEfe.Controls.Add(bNiv[i][k]);
                }
                tEfe.Controls.Add(lNom[i]); tEfe.Controls.Add(lCost[i]);
            }
            // ALIGERAR EL HOJEO. Va aparte de las seis de arriba, y con interruptor de si/no en vez de tres
            // niveles, porque es otra cosa: las seis se bajan para ganar rendimiento A CAMBIO de que se vea
            // menos bonito, y por eso el medidor las puede proponer. Esta no se ve -solo ocurre mientras la
            // pagina esta en movimiento- y apagarla solo CUESTA rendimiento. Esta aqui para poder deshacerla,
            // no para regatearla.
            // LAS CINCO QUE SE QUEDAN AQUI, y por que estas y no otras. La regla de esta pagina es que lo
            // de aqui se BAJA para ganar rendimiento a cambio de que se vea menos bonito. Estas cinco lo
            // cumplen -cada una quita trabajo de cada cuadro- y las once que eran de puro gusto se fueron
            // a la pestaña Fondo, que es su sitio.
            //
            // El primer interruptor empieza despues de TODAS las filas de niveles,
            // no en una coordenada fija que pueda pisar la ultima.
            int switchesY = 94 + ef.Length * 52 + 8;
            string[] nomSw = {
                "Ocultar caratulas en hojeo rapido",
                "Ocultar discos en reposo con musica",
                "Estado ampliado en reposo",
                "Lista de canciones transparente",
                "Miniaturas provisionales durante la carga",
            };
            bool[] valSw = { hojeoLigero, muroReposo, estadoExpandido, cancionesTransparentes, caratulasLod };
            Interruptor[] sw = new Interruptor[nomSw.Length];
            for (int i = 0; i < sw.Length; i++)
            {
                sw[i] = new Interruptor();
                sw[i].SetBounds(16, switchesY + i * 30, 424, 26); sw[i].ForeColor = cTexto; sw[i].Font = fNom;
                sw[i].Text = nomSw[i]; sw[i].Acento = cEfe; sw[i].Checked = valSw[i];
                tEfe.Controls.Add(sw[i]);
            }
            Interruptor swHojeo = sw[0];
            sw[1].CheckedChanged += delegate { muroReposo = sw[1].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            sw[2].CheckedChanged += delegate { estadoExpandido = sw[2].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            sw[3].CheckedChanged += delegate { cancionesTransparentes = sw[3].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            sw[4].CheckedChanged += delegate { caratulasLod = sw[4].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            // 280 de ancho y no 424: la tarjeta baja hasta el boton Listo, y lo que se pierde con un nivel ("Un tercio de las
            // estrellas: el cielo se ve bastante mas vacio") caia debajo de el. Asi parte la frase antes (barrido del 12-10-2026).
            Rotulo lPierde = new Rotulo(); lPierde.SetBounds(16, switchesY + sw.Length * 30 + 4, 280, 52); lPierde.BackColor = Color.Transparent;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            lPierde.ForeColor = cTenue; lPierde.Font = fDes;
            tEfe.Controls.Add(lGen); tEfe.Controls.Add(swHojeo); tEfe.Controls.Add(lPierde);

            BotonPlano bResetG = new BotonPlano();
            bResetG.SetBounds(16, 590, 300, 38); bResetG.Font = fBot; bResetG.Text = "Graficos como de fabrica";

            // PERFORMANCE: ahorros independientes y lista desplazable bajo una cabecera fija.
            Tarjeta tPerf = new Tarjeta(); tPerf.Size = tEfe.Size;
            Interruptor[] swAhorro; Rotulo[] lAhorro; Rotulo lCuentaPerf;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            LlenaHojaPerformance(tPerf, fNom, fDes, fBot, cTexto, cTenue, out swAhorro, out lAhorro, out lCuentaPerf);
            Action pintaFluidez;
            Grupo hojasGraficas = MontaFluidezGrafica(tEfe, tPerf, fNom, fDes, fBot, cTexto, cTenue, out pintaFluidez);   // EN LINUX: Panel -> Grupo (Nativo/Rotulo.cs)
            int ultimoTocado = 0;
            Action pinta = null;   // (se arma abajo; los interruptores de la hoja Performance la llaman)
            for (int i = 0; i < swAhorro.Length; i++)
            {
                int k = i; Interruptor s = swAhorro[i];
                s.CheckedChanged += delegate
                {
                    if (LeeAhorro(k) == s.Checked) return;   // repintado: nada que cambiar
                    PonAhorro(k, s.Checked); f3CambioGraf = true;
                    if (pinta != null) pinta();
                };
            }

            // ---------------- LO QUE SE ENSENA, SEGUN LO QUE HAYA ----------------
            pinta = delegate
            {
                // Los tres botones de cada fila, y el mando general si TODAS estan en el mismo nivel.
                bHistorial.Enabled = historialCaratulas != null && historialCaratulas.Count > 0;
                int comun = -2;
                for (int i = 0; i < ef.Length; i++)
                {
                    int niv = LeeNivel(ef[i].Clave);
                    if (comun == -2) comun = niv; else if (comun != niv) comun = -1;
                    for (int k = 0; k < 3; k++)
                    {
                        bool act = (k == niv);
                        bNiv[i][k].Relleno = act ? cOn : cOff;
                        bNiv[i][k].Borde = act ? cBordeOn : cBordeOff;
                        bNiv[i][k].ForeColor = act ? Color.FromArgb(226, 240, 255) : cApagado;
                        bNiv[i][k].Invalidate();
                    }
                    // El coste: lo que ahorra bajar UN escalon desde donde esta ahora. Puesto asi y no como
                    // "lo que cuesta el efecto entero" porque es la pregunta que uno se hace mirando el boton:
                    // si bajo esto un punto, cuanto gano.
                    string coste = "sin medir";
                    Color cc = cApagado;
                    if (ef[i].Medido)
                    {
                        double a = (niv == NivAlto) ? ef[i].AhorroMedio : (niv == NivMedio) ? ef[i].AhorroBajo : 0.0;
                        if (niv == NivBajo) { coste = "ya esta abajo"; cc = cApagado; }
                        else if (a > RuidoMs) { coste = "-" + a.ToString("0.0") + " ms"; cc = cEfe; }
                        else { coste = "no se nota"; cc = cApagado; }
                    }
                    else if (bancoEscenas != null && bancoError == null && bancoConGpu && !bancoEfectosMedidos) coste = "no hizo falta";
                    lCost[i].Text = coste; lCost[i].ForeColor = cc;
                }
                bool ahorrosPuestos = TodosLosAhorros(true), ahorrosQuitados = TodosLosAhorros(false);
                int general = (comun == NivAlto && ahorrosPuestos) ? Performance
                            : (comun == NivAlto && ahorrosQuitados) ? NivAlto
                            : (comun == NivMedio || comun == NivBajo) ? comun : -1;
                for (int k = 0; k < bGen.Length; k++)
                {
                    bool act = (k == general);
                    bGen[k].Relleno = act ? cOn : cOff;
                    bGen[k].Borde = act ? cBordeOn : cBordeOff;
                    bGen[k].ForeColor = act ? Color.FromArgb(226, 240, 255) : cApagado;
                    bGen[k].Invalidate();
                }
                lGen.Text = "Todo de golpe:" + ((general < 0) ? "        (ahora esta a medida)" : "");
                int nv = LeeNivel(ef[ultimoTocado].Clave);
                lPierde.Text = ef[ultimoTocado].Nombre + " en " + nomNivel[nv].ToLower() + ":\n" + ef[ultimoTocado].Pierde[nv];
                // La hoja Performance: interruptores al dia y lo medido de cada ahorro.
                for (int i = 0; i < swAhorro.Length; i++)
                {
                    if (swAhorro[i].Checked != LeeAhorro(i)) swAhorro[i].Checked = LeeAhorro(i);
                    lAhorro[i].Text = TextoAhorro(i);
                    lAhorro[i].ForeColor = (bancoAhorros != null && i < bancoAhorros.Length && bancoAhorros[i] > RuidoMs) ? cEfe : cApagado;
                }
                lCuentaPerf.Text = TextoCuentaPerformance();
                bool soloPerfUtil = !ahorrosPuestos;
                bSoloPerf.Enabled = soloPerfUtil;
                bSoloPerf.Relleno = soloPerfUtil ? cOn : cOff; bSoloPerf.Borde = soloPerfUtil ? cBordeOn : cBordeOff;
                bSoloPerf.ForeColor = soloPerfUtil ? Color.FromArgb(226, 240, 255) : cApagado;
                bSoloPerf.Invalidate();

                // ---- La tabla de lo medido ----
                for (int i = 0; i < NombreEscena.Length; i++) for (int c = 0; c < colX.Length; c++) lCelda[i, c].Text = "";
                lTarda.Text = textoTarda; lTarda.ForeColor = cTenue;
                // El resumen del ultimo diagnostico completo ocupa el sitio del recordatorio (el medidor rapido, mas abajo, lo vuelve a
                // quitar si tiene algo propio que decir ahi). El detalle esta en el archivo que dice.
                if (!string.IsNullOrEmpty(diagResumen)) { lTarda.Text = diagResumen; lTarda.ForeColor = cBien; }
                if (bancoEscenas == null)
                {
                    for (int c = 0; c < colX.Length; c++) lCab[c].Visible = false;
                    lTecho.Text = ""; lSinMus.Text = "";
                    lVer.ForeColor = cTenue;
                    lVer.Text = "Aun sin medir.\n\nAqui no hay ningun numero porque ninguno seria de verdad:"
                              + " los de este equipo salen de medirlo.";
                    // Sin tarjeta, lo que va en gris en las hojas de la derecha lo dice aqui, una vez (A1).
                    lProp.Text = HayTarjeta()
                        ? "Sin medir todavia. La propuesta sale de lo que se mida en ESTE equipo, no de una"
                          + " lista de equipos buenos y malos escrita a mano."
                        : "Esta rocola dibuja ahora SIN tarjeta grafica: lo que esta en gris en Efectos, Fluidez y"
                          + " Performance solo se ve con ella.";
                    bApli.Enabled = false; bApli.Relleno = cOff; bApli.Borde = cBordeOff; bApli.ForeColor = cApagado;
                    return;
                }
                for (int c = 0; c < colX.Length; c++) lCab[c].Visible = true;
                for (int i = 0; i < bancoEscenas.Count && i < NombreEscena.Length; i++)
                {
                    EscenaMedida m = bancoEscenas[i];
                    double coste = CosteDe(m);
                    int posibles = bancoSinEspera ? FpsDe(coste, 0) : m.Fps;
                    lCelda[i, 0].Text = m.Nombre + (EsLaPeor(i) ? "  <" : "");
                    lCelda[i, 1].Text = coste.ToString("0.0") + " ms";
                    lCelda[i, 2].Text = m.MsProcesador > 0 ? m.MsProcesador.ToString("0.0") + " ms" : "-";
                    lCelda[i, 3].Text = (m.SinEspera && bancoSinEspera) ? m.MsEspera.ToString("0.0") + " ms" : "-";
                    lCelda[i, 4].Text = posibles.ToString();
                    Color cf = (coste <= bancoObjetivoAlto || bancoObjetivoAlto <= 0) ? cBien : (coste <= bancoObjetivoBajo ? cProp : cMal);
                    for (int c = 0; c < colX.Length; c++) lCelda[i, c].ForeColor = c == 0 ? cTexto : cf;
                }
                lTecho.Text = "";
                if (bancoPeorId >= 0 && bancoPeorId < bancoEscenas.Count)
                {
                    EscenaMedida pe = bancoEscenas[bancoPeorId];
                    lTecho.Text = bancoFrena + "\nCada cuadro: " + pe.Pedidos.ToString("0") + " ordenes de dibujo ("
                                + pe.Envios.ToString("0") + " a la tarjeta), " + pe.Pantallas.ToString("0.0") + " pantallas.";
                }
                lSinMus.Text = !bancoConMusica ? "OJO: no se pudo poner ninguna cancion y se midio en\nsilencio. De verdad saldra peor de lo que dice aqui."
                             : !bancoSinEspera && bancoConGpu ? "OJO: esta tarjeta no deja soltar el monitor. Los numeros\nson solo los del procesador."
                             : "";
                if (!string.IsNullOrEmpty(bancoError))
                {
                    lTecho.Text = ""; lVer.ForeColor = cMal;
                    lVer.Text = "Medicion incompleta: " + bancoError;
                    lProp.Text = "No se aplican recomendaciones de una medicion fallida. Revisa el dispositivo y vuelve a medir.";
                    bApli.Enabled = false; bApli.Relleno = cOff; bApli.Borde = cBordeOff; bApli.ForeColor = cApagado;
                    return;
                }
                string portadas = "";
                if (bancoConGpu && bancoPortadas != null)
                {
                    EscenaMedida pm = bancoPortadas;
                    lTarda.Text = CoverMeasurementContext(pm);
                    lTarda.ForeColor = CoverMeasurementStale(pm) ? cMal : cTenue;
                    portadas = "\nPaginas: P95 " + pm.P95.ToString("0.0") + " / P99 " + pm.P99.ToString("0.0")
                        + " ms; >33 ms: " + pm.Lentos + ". Caratulas listas: " + pm.PaginasListas
                        + "/" + pm.PaginasSolicitadas + ".";
                }
                if (CoverMeasurementStale(bancoPortadas))
                {
                    lProp.Text = "Esta medida corresponde a otro perfil o resolucion. Vuelve a medir antes de aplicar una propuesta.";
                    bApli.Enabled = false; bApli.Relleno = cOff; bApli.Borde = cBordeOff; bApli.ForeColor = cApagado;
                    return;
                }
                if (!bancoConGpu)
                {
                    lTecho.Text = "";
                    lVer.ForeColor = cMal;
                    lVer.Text = "Esta rocola NO esta dibujando con la tarjeta grafica.\nPor ese camino el techo son"
                              + " 24 cuadros por segundo, puesto asi a proposito para gastar poco. La perilla que"
                              + " importa aqui es encender la tarjeta.";
                    lProp.Text = "Sin tarjeta grafica no hay nada que proponer aqui: bajar efectos no sube un techo que"
                               + " no es de la tarjeta.";
                    bApli.Enabled = false; bApli.Relleno = cOff; bApli.Borde = cBordeOff; bApli.ForeColor = cApagado;
                    return;
                }

                int posiblesPeor = bancoSinEspera ? FpsDe(bancoMsPeor, 0) : bancoFpsPeor;
                string peor = "La peor: \"" + bancoPeor + "\", " + bancoMsPeor.ToString("0.0") + " ms: " + posiblesPeor + " cuadros/s"
                            + (bancoSinEspera ? "." : " (techo " + bancoTope + ").");
                bool yaLlega = bancoMsPeor <= bancoObjetivoAlto;
                lVer.ForeColor = yaLlega ? cBien : (bancoMsPeor <= bancoObjetivoBajo ? cTenue : cMal);
                lVer.Text = peor + "\n" + (yaLlega ? "Cabe en los " + bancoHz + " de esta pantalla: no hace falta bajar nada."
                    : "Para " + bancoHz + " (pantalla de " + bancoHz + " Hz) hace falta " + bancoObjetivoAlto.ToString("0.0") + " ms o menos.")
                    + portadas;
                lProp.Text = TextoPropuesta();
                bool cambia = PropuestaCambiaAlgo();
                bApli.Enabled = cambia;
                bApli.Relleno = cambia ? Color.FromArgb(30, 96, 70) : cOff;
                bApli.Borde = cambia ? Color.FromArgb(72, 182, 132) : cBordeOff;
                bApli.ForeColor = cambia ? Color.White : cApagado;
            };

            // ---------------- LO QUE HACEN LOS BOTONES ----------------
            bMedir.Click += delegate
            {
                f3Medir = true;
                Hoja f = ((IPieza)pg).Caja.Hoja;   // EN LINUX: Form f = pg.FindForm(): la hoja donde esta la pagina (Control.FindForm, la ventana de arriba)
                if (f != null) f.Close();   // la ventana se cierra, se mide con la pantalla libre, y vuelve sola
            };
            bDiag.Click += delegate
            {
                f3Diagnostico = true;
                Hoja f = ((IPieza)pg).Caja.Hoja;   // EN LINUX: Form f = pg.FindForm(): la hoja donde esta la pagina
                if (f != null) f.Close();   // igual que medir: la ventana se cierra, se recorre todo con la pantalla libre, y vuelve sola
            };
            EventHandler alNivel = delegate (object so, EventArgs ea)
            {
                for (int i = 0; i < ef.Length; i++)
                    for (int k = 0; k < 3; k++)
                        if (so == bNiv[i][k]) { PonNivel(ef[i].Clave, k); ultimoTocado = i; }
                f3CambioGraf = true;
                pinta();
            };
            for (int i = 0; i < ef.Length; i++)
                for (int k = 0; k < 3; k++) bNiv[i][k].Click += alNivel;
            EventHandler alGeneral = delegate (object so, EventArgs ea)
            {
                for (int k = 0; k < bGen.Length; k++)
                    if (so == bGen[k]) AplicaPresetGrafico(k);
                f3CambioGraf = true;
                pinta();
            };
            for (int k = 0; k < bGen.Length; k++) bGen[k].Click += alGeneral;
            swHojeo.CheckedChanged += delegate
            {
                hojeoLigero = swHojeo.Checked;
                AplicaEfectosGpu();
                f3CambioGraf = true;
            };

            bApli.Click += delegate
            {
                if (bancoPropuesta == null) return;
                if (CoverMeasurementStale(bancoPortadas)) { pinta(); return; }
                for (int i = 0; i < ef.Length && i < bancoPropuesta.Length; i++) PonNivel(ef[i].Clave, bancoPropuesta[i]);
                if (bancoUsaAhorro != null)
                    for (int i = 0; i < bancoUsaAhorro.Length; i++) if (bancoUsaAhorro[i] && !LeeAhorro(i)) PonAhorro(i, true);
                f3CambioGraf = true;
                pinta();
                ShowToast("Graficos ajustados a este equipo", Color.FromArgb(40, 130, 90));
            };
            bSoloPerf.Click += delegate
            {
                for (int i = 0; i < NombreAhorro.Length; i++) if (!LeeAhorro(i)) PonAhorro(i, true);
                f3CambioGraf = true;
                pinta();
                ShowToast("Performance encendido", Color.FromArgb(40, 130, 90));
            };
            bResetG.Click += delegate
            {
                foreach (EfectoGrafico e in ef) PonNivel(e.Clave, NivAlto);
                fpsTope = 0;
                animOndasFps = animCubosFps = 60;
                pintaFluidez();
                // Solo lo de ESTA pagina. Los once adornos del fondo los devuelve el boton de su propia
                // pestaña: un boton que dice "graficos como de fabrica" y ademas te reordena el cielo seria
                // una sorpresa, y en la ventana de al lado no se ve lo que cambio.
                hojeoLigero = true; muroReposo = true; estadoExpandido = true;
                cancionesTransparentes = true; caratulasLod = true;
                PonAhorros(false);   // Performance: de fabrica, apagados
                for (int i = 0; i < sw.Length; i++) sw[i].Checked = true;   // de fabrica vienen encendidos
                AplicaEfectosGpu();
                f3CambioGraf = true;
                pinta();
            };

            pg.Controls.Add(tMide); pg.Controls.Add(tProp); pg.Controls.Add(hojasGraficas); pg.Controls.Add(bResetG);
            // A1: el estado ampliado solo sale con la pared oculta en reposo (Form1.cs: IdleExpandTarget). Esta justo
            // encima, asi que lo dice detras del nombre, en su renglon.
            Necesita(sw[2], delegate { return muroReposo; }, "la de arriba", null);
            // Los cinco interruptores son de la tarjeta grafica; los niveles de arriba valen tambien sin ella.
            // Sin tarjeta, la tarjeta de la propuesta lo dice una vez (ver pinta).
            SoloConTarjeta(sw);
            CoverProfileChanged += pinta;
            pg.Disposed += delegate { CoverProfileChanged -= pinta; };
            pg.VisibleChanged += delegate { if (pg.Visible) pinta(); };
            pinta();
            return pg;
        }

        // LOS CUATRO DE GOLPE (0 Bajo, 1 Medio, 2 Alto, 3 Performance). Alto es la pantalla de siempre; los demas llevan los
        // ahorros que casi no se notan, y Performance los lleva con los efectos en Alto.
        private void AplicaPresetGrafico(int preset)
        {
            EfectoGrafico[] ef = Efectos();
            int nivel = preset >= NivAlto ? NivAlto : preset;
            foreach (EfectoGrafico e in ef) PonNivel(e.Clave, nivel);
            bool ahorros = preset != NivAlto;
            PonAhorros(ahorros);
            if (ahorros) estrellaEspejo = false;   // E5 cuesta algo de tarjeta: Performance, Medio y Bajo lo apagan (PulidoGpu.cs)
            AplicaEfectosGpu();
        }

        // La hoja Performance de la tarjeta EFECTOS (la tercera, con Efectos y Fluidez).
        // 19-10-2026: con trece ahorros la lista ya no cabia (iba en un panel con barra de desplazamiento y las ultimas filas
        // quedaban debajo del boton Listo), asi que van AGRUPADOS POR TEMA en hojitas con su pestana, como las de Fondo. Cada
        // hoja cabe hasta cuatro, sin apretar los textos; lo de siempre-activo y lo medido queda abajo, fijo.
        private static readonly string[] GrupoAhorro = { "Cielo", "Grano", "Aurora", "Estrella", "Estado" };
        private static readonly int[] GrupoDeAhorro = { 0, 1, 0, 0, 4, 2, 2, 3, 3, 4, 1, 2, 2 };
        private void LlenaHojaPerformance(Tarjeta hoja, Rocola.Nativo.Letra fNom, Rocola.Nativo.Letra fDes, Rocola.Nativo.Letra fBot, Color cTexto, Color cTenue,   // EN LINUX: Font -> Letra (Nativo/Letras.cs: la misma familia, tamano y estilo)
                                          out Interruptor[] interruptores, out Rotulo[] medidos, out Rotulo cuenta)   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
        {
            hoja.Name = "Performance"; hoja.Titulo = ""; hoja.Acento = Color.FromArgb(120, 230, 165);
            Rotulo tit = new Rotulo(); tit.SetBounds(16, 42, 424, 22); tit.Font = fNom; tit.ForeColor = cTexto;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            tit.BackColor = Color.Transparent; tit.Text = "Casi igual, más ligero";
            Rotulo des = new Rotulo(); des.SetBounds(16, 64, 424, 34); des.Font = fDes; des.ForeColor = cTenue;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            des.BackColor = Color.Transparent;
            des.Text = "Para equipos modestos: menos trabajo para la tarjeta con\n"
                     + "cambios que casi no se notan. Performance los enciende.";
            hoja.Controls.Add(tit); hoja.Controls.Add(des);
            // Explicaciones de dos renglones. Los ahorros que pueden quedar sin efecto llevan un renglon
            // mas debajo del nombre (NecesitaMotivo).
            string[] explica = {
                "El cielo, las nebulosas y la vía láctea se pintan a la mitad\n"
              + "y se estiran una vez: casi idéntico, un cuarto de píxeles.",
                "Grano y viñeta mezclados a la vez: la pantalla se lee y escribe\n"
              + "una vez y no dos. El grano queda también sobre las estrellas.",
                "El grano y la viñeta van dentro del cielo y solo se pintan\n"
              + "cuando cambia. Casi igual; el grano, algo más grueso.",
                "El cielo a un cuarto: las nebulosas ya vienen de ese tamaño.\n"
              + "Algo más suaves; pasar de página pesa mucho menos.",
                "Fondo sólido en el estado extendido: deja de transparentar\n"
              + "el cielo y evita dibujar sus capas bajo esa zona.",
                "La cortina y su filo usan la mitad de segmentos.\n"
              + "La música sigue guiando la forma, algo más suave.",
                "Con el estado abierto se omite el reflejo bajo el horizonte.\n"
              + "La aurora principal y la reacción a la música siguen igual.",
                "En reposo se dibuja la estrella sin sus copias de estela.\n"
              + "Conserva sus giros, gestos y ayuda; vuelve al navegar.",
                "Los dos halos grandes de la estrella cubren menos espacio.\n"
              + "El cuerpo, el polvo y sus movimientos siguen completos.",
                "El estado toma el nuevo color de una vez, sin transición.\n"
              + "Evita redibujarlo por cada paso del cambio de canción.",
                "Quita la textura de grano del cielo, también en su caché.\n"
              + "Conserva el degradado, las nebulosas y la viñeta.",
                "Omite el velo de luz que ocupa la zona bajo el horizonte.\n"
              + "Conserva la cortina, el filo y las ocho bandas de música.",
                "Estrecha el resplandor de la cresta de la aurora.\n"
              + "Conserva su línea luminosa, la cortina y el horizonte."
            };
            // Las hojitas por tema: una pestana por grupo y un panel con sus filas.
            const int altoFila = 86, yGrupos = 134, altoGrupo = 344;
            Grupo[] paneles = new Grupo[GrupoAhorro.Length];   // EN LINUX: Panel -> Grupo (Nativo/Rotulo.cs)
            BotonPlano[] pestanas = new BotonPlano[GrupoAhorro.Length];
            int elegido = 0;
            Action pintaPestanas = delegate
            {
                for (int g = 0; g < paneles.Length; g++)
                {
                    bool activa = g == elegido;
                    paneles[g].Visible = activa;
                    pestanas[g].Relleno = activa ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                    pestanas[g].Borde = activa ? Color.FromArgb(150, 205, 255) : Color.FromArgb(60, 72, 118);
                    pestanas[g].ForeColor = activa ? cTexto : cTenue;
                    pestanas[g].Invalidate();
                }
            };
            for (int g = 0; g < GrupoAhorro.Length; g++)
            {
                Grupo pn = new Grupo(); pn.SetBounds(0, yGrupos, 456, altoGrupo); pn.BackColor = Color.Transparent;   // EN LINUX: Panel -> Grupo (Nativo/Rotulo.cs)
                paneles[g] = pn; hoja.Controls.Add(pn);
                BotonPlano t = new BotonPlano(); t.SetBounds(16 + g * 84, 102, 78, 28); t.Font = fBot; t.Text = GrupoAhorro[g];
                // EN LINUX: (XP) t.AccessibleName = "Performance: " + GrupoAhorro[g]; -- el nombre, el papel o la descripcion para los lectores de pantalla de Windows (MSAA): en Rocola OS no hay, y nada de XP los lee
                int k0 = g; t.Click += delegate { elegido = k0; pintaPestanas(); };
                pestanas[g] = t; hoja.Controls.Add(t);
            }
            interruptores = new Interruptor[NombreAhorro.Length];
            medidos = new Rotulo[NombreAhorro.Length];   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            int[] filaDe = new int[GrupoAhorro.Length];
            for (int i = 0; i < NombreAhorro.Length; i++)
            {
                Grupo pn = paneles[GrupoDeAhorro[i]];   // EN LINUX: Panel -> Grupo (Nativo/Rotulo.cs)
                int y = filaDe[GrupoDeAhorro[i]]++ * altoFila;
                Interruptor s = new Interruptor(); s.SetBounds(16, y, 424, 26); s.Font = fNom; s.ForeColor = cTexto;
                s.Acento = hoja.Acento; s.Text = NombreAhorro[i]; s.Checked = LeeAhorro(i);
                // EN LINUX: (XP) s.AccessibleName = "Performance: " + NombreAhorro[i]; -- el nombre, el papel o la descripcion para los lectores de pantalla de Windows (MSAA): en Rocola OS no hay, y nada de XP los lee
                Rotulo d = new Rotulo(); d.SetBounds(16, y + 38, 424, 34); d.Font = fDes; d.ForeColor = cTenue;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
                d.BackColor = Color.Transparent; d.Text = explica[i];
                Rotulo m = new Rotulo(); m.SetBounds(16, y + 70, 424, 16); m.Font = fDes; m.ForeColor = cTenue;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
                m.BackColor = Color.Transparent;
                pn.Controls.Add(s); pn.Controls.Add(d); pn.Controls.Add(m);
                interruptores[i] = s; medidos[i] = m;
                int k = i;
                if (k != 10) NecesitaMotivo(s, delegate { return MotivoSinAhorro(k); });   // en gris si ahora no hace nada, y por que
            }
            pintaPestanas();
            // Lo fijo de abajo.
            const int y0 = yGrupos + altoGrupo + 4;   // 482: y el boton Listo empieza en 580, a la derecha de la x 316
            Rotulo autoT = new Rotulo(); autoT.SetBounds(16, y0, 424, 22); autoT.Font = fNom; autoT.ForeColor = cTexto;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            autoT.BackColor = Color.Transparent; autoT.Text = "Automático: mapas y reloj por recuadros";
            Rotulo autoD = new Rotulo(); autoD.SetBounds(16, y0 + 22, 424, 34); autoD.Font = fDes; autoD.ForeColor = cTenue;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            autoD.BackColor = Color.Transparent;
            autoD.Text = "Reutilizar mapas y texturas; no subir textos ocultos.\n"
                       + "Juntar órdenes y no repetir el cielo. Misma imagen.";
            cuenta = new Rotulo(); cuenta.SetBounds(16, y0 + 58, 424, 34); cuenta.Font = fDes; cuenta.ForeColor = cTenue;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            cuenta.BackColor = Color.Transparent;
            Rotulo nota = new Rotulo(); nota.SetBounds(16, y0 + 94, 290, 32); nota.Font = fDes; nota.ForeColor = cTenue;   // 290: no llega al boton Listo; EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            nota.BackColor = Color.Transparent;
            nota.Text = "De fábrica, apagados. Se guardan al momento.";
            hoja.Controls.Add(autoT); hoja.Controls.Add(autoD); hoja.Controls.Add(cuenta); hoja.Controls.Add(nota);
            // Sin tarjeta, en gris y dicho una vez (A1), en el sitio de la cuenta.
            Rotulo sin = new Rotulo(); sin.SetBounds(16, y0 + 58, 424, 34); sin.Font = fDes; sin.ForeColor = Interruptor.ColorNecesita;   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            sin.BackColor = Color.Transparent; sin.Text = TextoSinTarjeta; sin.Visible = false; hoja.Controls.Add(sin);
            SoloConTarjeta(interruptores); AvisoSinTarjeta(sin, cuenta);
        }
        // Lo medido de un ahorro, en una linea.
        private string TextoAhorro(int i)
        {
            if (bancoAhorros == null || i >= bancoAhorros.Length || bancoError != null) return "Sin medir. «Medir este equipo» lo prueba aquí.";
            double a = bancoAhorros[i];
            if (double.IsNaN(a)) return "No hizo falta probarlo: la pantalla ya llegaba.";
            if (a < 0) return "Esta tarjeta no puede: se dibuja como siempre.";
            if (a > RuidoMs) return "Medido aquí: -" + a.ToString("0.0") + " ms por cuadro.";
            return "Medido aquí: no se nota en este equipo.";
        }
        // Las ordenes de la pantalla que peor fue, para la hoja Performance.
        private string TextoCuentaPerformance()
        {
            if (bancoEscenas == null || bancoError != null || bancoPeorId < 0 || bancoPeorId >= bancoEscenas.Count) return "";
            EscenaMedida m = bancoEscenas[bancoPeorId];
            return "En \"" + m.Nombre + "\": " + m.Pedidos.ToString("0") + " órdenes por cuadro,\n"
                 + m.Envios.ToString("0") + " a la tarjeta; " + m.Pantallas.ToString("0.0") + " pantallas pintadas.";
        }
        private bool PropuestaCambiaAlgo()
        {
            if (bancoPropuesta == null) return false;
            EfectoGrafico[] ef = Efectos();
            for (int i = 0; i < ef.Length && i < bancoPropuesta.Length; i++) if (bancoPropuesta[i] != LeeNivel(ef[i].Clave)) return true;
            if (bancoUsaAhorro != null) for (int i = 0; i < bancoUsaAhorro.Length; i++) if (bancoUsaAhorro[i] && !LeeAhorro(i)) return true;
            return false;
        }
        // LA PROPUESTA EN PALABRAS: que se enciende, que se baja, a que llega y lo que dio al comprobarla.
        private string TextoPropuesta()
        {
            if (bancoPropuesta == null) return "";
            EfectoGrafico[] ef = Efectos();
            string[] nomNivel = { "bajo", "medio", "alto" };
            List<string> cosas = new List<string>();
            if (bancoUsaAhorro != null)
            {
                bool a0 = bancoUsaAhorro.Length > 0 && bancoUsaAhorro[0] && !LeeAhorro(0);
                bool a1 = bancoUsaAhorro.Length > 1 && bancoUsaAhorro[1] && !LeeAhorro(1);
                bool a2 = bancoUsaAhorro.Length > 2 && bancoUsaAhorro[2] && !LeeAhorro(2);
                bool a3 = bancoUsaAhorro.Length > 3 && bancoUsaAhorro[3] && !LeeAhorro(3);
                if (a0 && a1 && a2 && a3) cosas.Add("cielo Performance");
                else
                {
                    if (a0) cosas.Add("cielo a media resolución");
                    if (a1) cosas.Add("grano y viñeta juntos");
                    if (a2) cosas.Add("fondo en una sola capa");
                    if (a3) cosas.Add("cielo a un cuarto");
                }
            }
            if (bancoUsaAhorro != null)
                for (int i = 4; i < bancoUsaAhorro.Length && i < NombreAhorro.Length; i++)
                    if (bancoUsaAhorro[i] && !LeeAhorro(i)) cosas.Add(NombreAhorro[i]);
            int efectos = 0, nombrados = 0;
            for (int i = 0; i < ef.Length && i < bancoPropuesta.Length; i++)
            {
                if (bancoPropuesta[i] == LeeNivel(ef[i].Clave)) continue;
                efectos++;
                if (nombrados < 2) { cosas.Add(ef[i].Nombre.ToLower() + " a " + nomNivel[bancoPropuesta[i]]); nombrados++; }
            }
            if (efectos > nombrados) cosas.Add((efectos - nombrados) + " efecto(s) más");
            int bajo = Math.Max(24, bancoHz / 2);
            if (cosas.Count == 0)
            {
                if (bancoMsPeor <= bancoObjetivoAlto)
                    return "Nada que cambiar: la pantalla que peor va da " + FpsDe(bancoMsPeor, 0) + " cuadros por segundo y esta"
                         + " pantalla muestra " + bancoHz + ".";
                return "Nada que cambiar: " + bancoHz + " no se alcanza ni bajándolo todo, y " + bajo
                     + " estables ya los da (" + FpsDe(bancoMsPeor, 0) + ").";
            }
            string que = string.Join(", ", cosas.ToArray());
            string meta = bancoMetaLograda == 2 ? "Con eso llega a " + bancoHz + "."
                        : bancoMetaLograda == 1 ? bancoHz + " no se alcanza ni bajándolo todo; con eso, " + bajo + " estables."
                        : "Ni bajándolo todo llega a " + bajo + ": este equipo no da para más.";
            string comprobado = bancoMsComprobado > 0
                ? " Comprobado: " + FpsDe(bancoMsComprobado, 0) + " cuadros/s (antes " + FpsDe(bancoMsPeor, 0)
                  + "; la cuenta daba " + bancoFpsPropuesta + ")."
                : "";
            return "Poner " + que + ". " + meta + comprobado;
        }
    }
}
