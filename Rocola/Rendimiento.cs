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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

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
            msFinal = msPeor;
            int n = (aMedio == null) ? 0 : aMedio.Length;
            int[] niv = new int[n];
            for (int i = 0; i < n; i++) niv[i] = NivAlto;
            if (n == 0 || aBajo == null || aBajo.Length < n || meta <= 0) return niv;

            double objetivo = 1000.0 / meta;   // los ms por cuadro que hacen falta para llegar a la meta
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
            // Este va ANTES del corte de abajo: la pared de discos existe aunque el panel de la tarjeta no,
            // y es la pared la que decide como hornea sus casillas.
            if (albumView != null) albumView.HojeoLigero = hojeoLigero;   // aligerar mientras se pasa de pagina
            if (gpuPanel == null) return;
            gpuPanel.Nieblas = nieblasNiv;
            gpuPanel.NebulosasCine = nebulosasCine;
            gpuPanel.VinetaSuave = vinetaSuave;
            gpuPanel.HaloSeleccion = haloSeleccion;
            gpuPanel.EstrellasFinas = estrellasFinas;
            gpuPanel.OndasSuaves = ondasSuaves;
            gpuPanel.ProgresoFluido = progresoFluido;
            gpuPanel.NeonIntenso = neonIntenso;
            gpuPanel.CieloProfundo = cieloProfundo;
            gpuPanel.AuroraBandas = auroraBandas;
            if (searchOverlay != null) searchOverlay.SeparateGpu = buscadorCache;
            if (!estadoExpandido) gpuPanel.IdleExpandTarget = false;
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
            public double MsTrabajo;   // lo que cuesta ARMAR un cuadro (sin la espera del monitor)
            public int Fps;            // los cuadros por segundo ENTREGADOS de verdad, con todo compitiendo
        }
        private List<EscenaMedida> bancoEscenas;   // null = aun sin medir
        private string bancoPeor = "";             // el escenario que peor salio
        private double bancoMsPeor;
        private int bancoFpsPeor;
        private int bancoTope;                     // el techo de la pantalla en este equipo
        private int[] bancoPropuesta;              // el nivel propuesto para cada efecto (null = sin medir)
        private int bancoFpsPropuesta;             // donde quedaria
        private bool bancoConGpu;                  // si al medir la rocola dibujaba con tarjeta grafica
        private string bancoError;                 // una medicion fallida no produce recomendaciones
        private bool bancoConMusica;               // si se pudo medir con una cancion puesta

        // Las cinco pantallas donde la rocola pasa su vida. Ya no son fotos fijas: cada una lleva dentro lo
        // que de verdad hace la gente en ella.
        private const int EscPagina = 0, EscLetra = 1, EscBuscador = 2, EscDisco = 3, EscAtraccion = 4;
        private static readonly string[] NombreEscena = {
            "Pasando de pagina", "Cambiando de letra", "Escribiendo en el buscador",
            "Entrando a discos", "Pantalla de atraccion" };
        private const int MsPorEscena = 2000;    // lo que dura la medida de cada pantalla
        private const int MsPorEscalon = 1200;   // ... y la de cada escalon de efecto (la pantalla ya esta puesta)
        private const int MsCalentando = 350;    // los primeros no cuentan: una pantalla nueva sube texturas

#if ROCOLA_GPU
        private int pasoEsc;                       // por donde va la pantalla que se mide (que letra, que pagina...)
        private int bancoPeorId;                   // cual de las cinco salio peor (se marca en la tabla)
        private bool bancoPuseYoLaCancion;         // si la cancion la puso el medidor: entonces es suya para pararla
        // Cada cuanto se da un paso de la pantalla. El de pagina va a 180 ms A PROPOSITO: la animacion de
        // pasar dura 230, asi que se la interrumpe a si misma, que es exactamente lo que pasa cuando alguien
        // machaca el boton. Lo demas va al ritmo al que lo haria una persona con prisa.
        private static readonly int[] PasoEscenaMs = { 180, 420, 150, 380, 0 };

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
                    case EscPagina: if ((pasoEsc & 3) < 2) NextPage(); else PrevPage(); break;
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
            bool contando = false;
            int tCuenta = t0;
            while (true)
            {
                int ahora = Environment.TickCount;
                int va = ahora - t0;
                if (va >= MsCalentando + ms) break;
                if (!contando && va >= MsCalentando)
                {
                    contando = true; tCuenta = ahora; cuadros = 0;
                    gpuPanel.ReiniciaMedida();   // lo de calentar no cuenta
                }
                if (ahora - ultTick >= 42) { ultTick = ahora; try { AnimTimer_Tick(null, EventArgs.Empty); } catch { } }
                if (paso > 0 && ahora - ultPaso >= paso) { ultPaso = ahora; PasoDeEscena(escena); }
                if (!bancoConMusica) PulsoDeMentira();   // sin cancion, un latido de mentira; con ella, el suyo
                bool presentado = gpuPanel.DibujaUnCuadro();
                if (contando && presentado) cuadros++;
                Application.DoEvents();   // de aqui come el audio: sin esto la musica no entraria en la cuenta
            }
            msReales = Environment.TickCount - tCuenta;
            if (msReales < 1) msReales = 1;
        }

        private EscenaMedida MideEscena(int escena)
        {
            EscenaMedida m = new EscenaMedida();
            m.Nombre = NombreEscena[escena]; m.MsTrabajo = 0.0; m.Fps = 0;
            return MideEscena(escena, MsPorEscena, m);
        }
        private EscenaMedida MideEscena(int escena, int ms, EscenaMedida m)
        {
            if (gpuPanel == null || !gpuPanel.Visible)
                throw new InvalidOperationException("El panel GPU no esta disponible; medicion no valida");
            int cuadros, msReales;
            CorreEscena(escena, ms, out cuadros, out msReales);
            if (gpuPanel.FallosMedidos != 0 || cuadros == 0)
                throw new InvalidOperationException("La GPU no presento todos los cuadros; medicion no valida");
            m.MsTrabajo = gpuPanel.MsTrabajo;
            // Cuadros cuyo Present acepto el controlador, contados con el reloj de pared: con la musica
            // peleando por el mismo hilo, este numero es lo que se siente. El de arriba (ms de trabajo) dice
            // otra cosa distinta: margen de trabajo. No es una medicion de fotones ni latencia del monitor.
            m.Fps = (int)(cuadros * 1000L / msReales);
            return m;
        }
#else
        // Sin tarjeta grafica no hay nada que cronometrar: estas pantallas no se dibujan por ese camino. Se
        // devuelven los campos a cero -y se ASIGNAN, para que el compilador no avise de campos que nadie
        // escribe- y la pagina ya dice, en su sitio, por que aqui no hay numeros.
        private EscenaMedida MideEscena(int escena)
        {
            EscenaMedida m = new EscenaMedida();
            m.Nombre = NombreEscena[escena]; m.MsTrabajo = 0.0; m.Fps = 0;
            return m;
        }
#endif

        // ---------------------------------------------------------------------------------------------
        //  EL MEDIDOR
        // ---------------------------------------------------------------------------------------------
        //  Pone una cancion, recorre las cinco pantallas EN MOVIMIENTO, se queda con la peor, y mide lo que
        //  ahorra cada escalon de cada efecto contra esa. Al acabar deja la rocola donde estaba.
        //
        //  NO GUARDA NADA Y NO APLICA NADA: solo rellena lo medido y la propuesta. (La bateria lo vigila:
        //  si algun dia esta funcion llamara a SaveConfigPrefs, se pondria roja.)
        // ---------------------------------------------------------------------------------------------
        private void MideRendimiento()
        {
            // Todo a cero primero: volver a medir no puede dejar ni un numero de la vez anterior asomando.
            bancoEscenas = new List<EscenaMedida>();
            bancoPropuesta = null; bancoError = null;
            bancoConGpu = false; bancoConMusica = false;
            bancoPeor = ""; bancoMsPeor = 0.0; bancoFpsPeor = 0; bancoTope = 0; bancoFpsPropuesta = 0;
#if ROCOLA_GPU
            bancoPeorId = 0; bancoPuseYoLaCancion = false;
            if (gpuPanel == null || !gpuEnabled || !gpuPanel.Visible) return;
            bancoConGpu = true;
            bancoTope = gpuPanel.TopeFpsReal;

            // COMO ESTABA TODO, para dejarlo igual pase lo que pase.
            bool bkBuscador = searchOpen, bkAtraccion = attractOn;
            bool bkDisco = (panelSongs != null && panelSongs.Visible);
            char bkLetra = currentLetter; int bkPag = page, bkSel = selAlbumIndex;
            EfectoGrafico[] ef = Efectos();
            int[] bkNiv = new int[ef.Length];
            for (int i = 0; i < ef.Length; i++) bkNiv[i] = LeeNivel(ef[i].Clave);
            animTimer.Stop();   // durante la medida el reloj de animaciones lo lleva CorreEscena, a mano
            try
            {
                // ---- LA MUSICA ----
                // Si ya hay algo sonando se usa ESO: es la carga de verdad y ademas es la cancion de un
                // cliente que la pago, asi que ni se para ni se le toca el volumen. Solo si no hay nada se
                // pone una, y MUDA: con el volumen a cero hace exactamente el mismo trabajo -descomprimir el
                // MP3, el DSEE, el nivelado, los graves, el limitador- porque todo eso pasa antes de llegar a
                // la bocina. Se mide igual de duro y no se le suelta medio minuto de ruido al local.
                if (nowPlaying != null && output != null) bancoConMusica = true;
                else if (tracks.Count > 0)
                {
                    try
                    {
                        PlayTrack(tracks[new Random().Next(tracks.Count)]);
                        if (output != null) { output.Volume = 0f; bancoConMusica = true; bancoPuseYoLaCancion = true; }
                    }
                    catch { bancoConMusica = false; }
                }

                // ---- LAS CINCO PANTALLAS ----
                if (bkBuscador) CloseSearch();
                if (bkAtraccion) ExitAttract();
                if (panelSongs != null && panelSongs.Visible) BackToAlbums();

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

                // EL PEOR MANDA: de nada sirve que cuatro pantallas vayan sobradas si la quinta se arrastra.
                EscenaMedida peor = null; int peorId = 0;
                for (int i = 0; i < bancoEscenas.Count; i++)
                    if (bancoEscenas[i].MsTrabajo > 0.0 && (peor == null || bancoEscenas[i].MsTrabajo > peor.MsTrabajo))
                    { peor = bancoEscenas[i]; peorId = i; }
                if (peor == null) throw new InvalidOperationException("No se obtuvieron cuadros validos");
                bancoPeor = peor.Nombre; bancoPeorId = peorId; bancoMsPeor = peor.MsTrabajo; bancoFpsPeor = peor.Fps;

                // ---- LO QUE AHORRA CADA ESCALON ----
                // Contra ESA pantalla y no contra una cualquiera: un efecto puede costar mucho donde se ve y
                // nada donde no. De cada efecto se mide su nivel MEDIO y su nivel BAJO, con todo lo demas
                // como estaba, y las restas dan lo que ahorra cada escalon por separado.
                PonEscena(peorId);
                for (int i = 0; i < ef.Length; i++)
                {
                    int antes = LeeNivel(ef[i].Clave);
                    ef[i].AhorroMedio = 0.0; ef[i].AhorroBajo = 0.0; ef[i].Medido = true;
                    if (antes <= NivBajo) continue;   // ya estaba abajo del todo: no hay escalon que dar
                    double conMedio = bancoMsPeor, conBajo = bancoMsPeor;
                    PonNivel(ef[i].Clave, NivMedio);
                    conMedio = MideEscena(peorId, MsPorEscalon, new EscenaMedida()).MsTrabajo;
                    PonNivel(ef[i].Clave, NivBajo);
                    conBajo = MideEscena(peorId, MsPorEscalon, new EscenaMedida()).MsTrabajo;
                    PonNivel(ef[i].Clave, antes);
                    double a1 = bancoMsPeor - conMedio, a2 = conMedio - conBajo;
                    ef[i].AhorroMedio = (a1 > 0.0) ? a1 : 0.0;
                    ef[i].AhorroBajo = (a2 > 0.0) ? a2 : 0.0;
                }
                QuitaEscena(peorId);

                // ---- Y LA PROPUESTA. Sale de lo medido y de nada mas. ----
                double[] am = new double[ef.Length], ab = new double[ef.Length];
                for (int i = 0; i < ef.Length; i++) { am[i] = ef[i].AhorroMedio; ab[i] = ef[i].AhorroBajo; }
                double msFinal;
                bancoPropuesta = ProponeEscalones(am, ab, bancoMsPeor, MetaFps, out msFinal);
                bancoFpsPropuesta = FpsDe(msFinal, bancoTope);
            }
            catch (Exception ex) { bancoPropuesta = null; bancoError = ex.Message; LogError("Medicion de GPU no valida", ex); }
            finally
            {
                // La rocola vuelve donde estaba, truene lo que truene por el camino.
                for (int i = 0; i < ef.Length; i++) PonNivel(ef[i].Clave, bkNiv[i]);
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
        private void PonEscena(int escena)
        {
            pasoEsc = 0;
            if (escena == EscBuscador) OpenSearch();
            else if (escena == EscAtraccion) EnterAttract();
        }
        private void QuitaEscena(int escena)
        {
            if (escena == EscBuscador) { if (searchOpen) CloseSearch(); }
            else if (escena == EscAtraccion) { if (attractOn) ExitAttract(); }
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
        private int f3Pagina;          // 0 = Sonido, 1 = Imagen (al volver de medir se abre en Imagen)
        private bool f3CambioGraf;     // se toco algo de graficos (para guardar al cerrar)

        // Cual de las cinco pantallas salio peor: es la que manda sobre todo lo demas, y en la tabla se
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
        // Los seis adornos que la rocola fue juntando -el turno, el palomito, el fondo con el tono de la
        // portada, las portaditas de la cola, el relevo del cubito y la pared que respira- solo se podian
        // apagar escribiendo a mano en el archivo de configuracion. Para quien no toca archivos de texto,
        // eso es lo mismo que no poder apagarlos.
        //
        // Y no cabian en la pagina de Imagen: la tarjeta EFECTOS mide 568 px y su contenido llega al 562, y
        // las dos columnas acaban en 572 de 636. Estan llenas, medido. Asi que van en pestaña propia, que
        // ademas es su sitio: las de Imagen se BAJAN para ganar rendimiento a cambio de que se vea menos
        // bonito, y por eso el medidor las puede proponer. Estas no cuestan nada que se note; son gusto.
        //
        // Cada una lleva escrito QUE SE VE si se enciende, no como esta hecha por dentro. Quien abre esta
        // ventana quiere saber que va a cambiar en la pantalla del bar.
        private Panel PaginaDetalles(Font fNom, Font fDes, Font fBot,
                                     Color cTexto, Color cTenue, Color cApagado)
        {
            Color cVida = Color.FromArgb(150, 220, 180);
            Color cResp = Color.FromArgb(140, 205, 255);
            Color cOn = Color.FromArgb(28, 62, 104), cOff = Color.FromArgb(28, 34, 62);
            Color cBordeOn = Color.FromArgb(150, 205, 255), cBordeOff = Color.FromArgb(60, 72, 118);

            Panel pg = new Panel();
            pg.SetBounds(0, 96, 960, 636);
            pg.BackColor = Color.FromArgb(11, 14, 32);

            // LAS DOS TARJETAS CRECEN 72 PX para que quepa el sexto interruptor. La pagina son 636 y debajo
            // quedaban el pie y el boton de fabrica con sitio de sobra; ahora bajan y siguen cabiendo. La
            // bateria 'luna' lo comprueba armando la pagina de verdad y midiendola.
            Tarjeta tVida = new Tarjeta(); tVida.SetBounds(16, 4, 456, 496); tVida.Titulo = "LO QUE SE VE"; tVida.Acento = cVida;
            Tarjeta tResp = new Tarjeta(); tResp.SetBounds(488, 4, 456, 496); tResp.Titulo = "LA PARED QUE RESPIRA"; tResp.Acento = cResp;

            // ---------------- LO QUE SE VE: cinco interruptores con su explicacion ----------------
            string[] nombre = {
                "Decir cuando suena tu cancion",
                "Marcar los discos que ya pediste",
                "Que el fondo tome el color de la portada",
                "Ensenar las portadas de la cola",
                "Media vuelta al cambiar de cancion",
                "La luna en el cielo",
            };
            string[] explica = {
                "Al pedir una cancion, el aviso dice que puesto ocupa y cuantos\nminutos faltan, en vez de solo \"Cancion agregada\".",
                "Un palomito verde en la esquina de los discos de los que ya hay\nalgo pedido o sonando, para no volver a pedir lo mismo.",
                "Del disco se toma SOLO de que color es. Lo oscuro y lo vivo lo\nsigue mandando el genero: ninguna portada estropea la pantalla.",
                "En la capsula de \"En cola\", las caratulas de las tres proximas.\nSi hay mas de tres, la ultima lleva un \"+2\".",
                "El cubito de la barra de abajo gira media vuelta y la portada\nnueva aparece en el camino, en vez de cambiar de golpe.",
                "Una luna baja a un lado, con su halo, que se apaga y tira a\nnaranja cerca del horizonte igual que hacen las estrellas.",
            };
            Interruptor[] sw = new Interruptor[nombre.Length];
            for (int i = 0; i < nombre.Length; i++)
            {
                int y = 40 + i * 72;
                sw[i] = new Interruptor();
                sw[i].SetBounds(16, y, 424, 26); sw[i].ForeColor = cTexto; sw[i].Font = fNom;
                sw[i].Text = nombre[i]; sw[i].Acento = cVida;
                Label le = new Label(); le.SetBounds(16, y + 30, 424, 32); le.BackColor = Color.Transparent;
                le.ForeColor = cTenue; le.Font = fDes; le.Text = explica[i];
                tVida.Controls.Add(sw[i]); tVida.Controls.Add(le);
            }
            sw[0].Checked = turnoOn;
            sw[1].Checked = marcaColaOn;
            sw[2].Checked = fondoPortadaOn;
            sw[3].Checked = colaPortadasOn;
            sw[4].Checked = relevoOn;
            sw[5].Checked = lunaOn;

            // ---------------- LA PARED QUE RESPIRA ----------------
            Interruptor swResp = new Interruptor();
            swResp.SetBounds(16, 40, 424, 26); swResp.ForeColor = cTexto; swResp.Font = fNom;
            swResp.Text = "Que la pared respire cuando nadie la usa"; swResp.Acento = cResp;
            swResp.Checked = respiraOn;
            Label lResp = new Label(); lResp.SetBounds(16, 70, 424, 48); lResp.BackColor = Color.Transparent;
            lResp.ForeColor = cTenue; lResp.Font = fDes;
            lResp.Text = "La pared de discos sube y baja de brillo muy despacio, una\n"
                       + "respiracion cada medio minuto. Es lo que hace que una pantalla\n"
                       + "quieta parezca encendida en vez de colgada.";

            Label lCuanto = new Label(); lCuanto.SetBounds(16, 128, 424, 16); lCuanto.BackColor = Color.Transparent;
            lCuanto.ForeColor = cApagado; lCuanto.Font = fDes; lCuanto.Text = "Cuanto respira:";
            // Los tres pasos, medidos sobre el degradado del cielo (cuanto se mueve, en niveles de 255, de
            // un extremo de la respiracion al otro): 2.5% son 4-6 niveles, 4.5% son 8-10 y 6% son 12. El
            // 1.2% que habia aqui son DOS niveles, o sea nada: se quito porque no era una opcion, era un
            // apagado con otro nombre.
            string[] nomFza = { "Apenas", "Normal", "Se nota" };
            string[] valFza = { "2.5", "4.5", "6" };
            BotonPlano[] bFza = new BotonPlano[3];
            for (int k = 0; k < 3; k++)
            {
                bFza[k] = new BotonPlano(); bFza[k].SetBounds(16 + k * 140, 148, 132, 28);
                bFza[k].Font = fBot; bFza[k].Text = nomFza[k];
                tResp.Controls.Add(bFza[k]);
            }

            Label lFreno = new Label(); lFreno.SetBounds(16, 190, 424, 118); lFreno.BackColor = Color.Transparent;
            lFreno.ForeColor = cTenue; lFreno.Font = fDes;
            lFreno.Text = "EL FRENO. En cuanto alguien toca un boton la pared deja de\n"
                        + "respirar, y vuelve sola unos segundos despues de que la dejen\n"
                        + "en paz. Una pantalla grande moviendose delante de quien esta\n"
                        + "eligiendo cancion marea.\n\n"
                        + "Y no se para en seco -asi se quedaria clavada mas oscura o mas\n"
                        + "clara-: se le quita el aire, y acaba en su brillo de siempre.";

            tResp.Controls.Add(swResp); tResp.Controls.Add(lResp); tResp.Controls.Add(lCuanto); tResp.Controls.Add(lFreno);

            Label lPie = new Label(); lPie.SetBounds(16, 508, 928, 48); lPie.BackColor = Color.Transparent;
            lPie.ForeColor = cApagado; lPie.Font = fDes;
            lPie.Text = "Nada de esto le quita ni le pone rendimiento a la rocola: no son las perillas de la pestaña de Imagen,\n"
                      + "que ahi si se cambia trabajo por bonito. Estas son de gusto, y apagarlas no gana nada mas que quitarlas\n"
                      + "de la vista. Todo lo que se toque aqui se guarda al cerrar la ventana.";

            BotonPlano bResetD = new BotonPlano();
            bResetD.SetBounds(16, 572, 300, 38); bResetD.Font = fBot; bResetD.Text = "Detalles como de fabrica";

            // ---------------- LO QUE SE ENSENA ----------------
            Action pinta = delegate
            {
                int cual = 1;
                if (respiraFuerza < 0.035f) cual = 0; else if (respiraFuerza > 0.052f) cual = 2;
                for (int k = 0; k < 3; k++)
                {
                    bool act = (k == cual) && respiraOn;
                    bFza[k].Enabled = respiraOn;
                    bFza[k].Relleno = act ? cOn : cOff;
                    bFza[k].Borde = act ? cBordeOn : cBordeOff;
                    bFza[k].ForeColor = act ? Color.FromArgb(226, 240, 255) : cApagado;
                    bFza[k].Invalidate();
                }
                lCuanto.ForeColor = respiraOn ? cTenue : cApagado;
                lFreno.ForeColor = respiraOn ? cTenue : cApagado;
            };

            sw[0].CheckedChanged += delegate { turnoOn = sw[0].Checked; f3CambioGraf = true; };
            // La marca va DENTRO del mapa de bits horneado de cada casilla y su clave lleva el "#q", asi que
            // al apagarla la clave cambia sola y las casillas se rehacen: basta con pedir un repintado.
            sw[1].CheckedChanged += delegate { marcaColaOn = sw[1].Checked; f3CambioGraf = true; if (albumView != null) albumView.Invalidate(); };
            sw[2].CheckedChanged += delegate
            {
                fondoPortadaOn = sw[2].Checked;
                f3CambioGraf = true;
                // Se ve YA, sin esperar a la siguiente cancion: el fondo viaja hasta el color nuevo con el
                // mismo fundido lento de siempre.
                if (nowPlaying != null && !nowPlaying.IsVideo) ApplyGenreBackground(nowPlaying.Song, nowPlaying.Disc);
            };
            sw[3].CheckedChanged += delegate
            {
                colaPortadasOn = sw[3].Checked;
                f3CambioGraf = true;
                // La capsula cambia de ANCHO al llevar o no portaditas, asi que hay que reacomodar la fila.
                if (pastCola != null) { pastCola.ConPortadas = colaPortadasOn; LlenaPortaditas(); pastCola.Invalidate(); }
                LayoutControls();
            };
            sw[4].CheckedChanged += delegate { relevoOn = sw[4].Checked; f3CambioGraf = true; };
            // La luna NO va horneada en el cielo: se pega encima en cada cuadro, asi que con pedir un
            // repintado basta y no hay que rehacer nada.
            sw[5].CheckedChanged += delegate { lunaOn = sw[5].Checked; f3CambioGraf = true; RepintaFondo(); };
            swResp.CheckedChanged += delegate { respiraOn = swResp.Checked; f3CambioGraf = true; pinta(); };
            for (int k = 0; k < 3; k++)
            {
                int mio = k;
                bFza[k].Click += delegate
                {
                    // SIEMPRE por FuerzaRespira, que es la que acota: es el unico numero de esta pagina, y
                    // uno grande ahi marea a todo el bar.
                    respiraFuerza = FuerzaRespira(valFza[mio]);
                    f3CambioGraf = true;
                    // Y QUE SE VEA. Tal y como esta hecha no se puede elegir mirando -tarda catorce segundos
                    // en arrancar y se mueve tres decimas de nivel por segundo-, asi que al pulsar respira
                    // deprisa unos segundos. En el bar sigue igual de lenta.
                    MuestraRespiracion();
                    pinta();
                };
            }
            bResetD.Click += delegate
            {
                turnoOn = true; marcaColaOn = true; fondoPortadaOn = true; colaPortadasOn = true;
                relevoOn = true; respiraOn = true; lunaOn = true;
                respiraFuerza = FuerzaRespira("4.5");
                MuestraRespiracion();
                for (int k = 0; k < sw.Length; k++) sw[k].Checked = true;
                swResp.Checked = true;
                f3CambioGraf = true;
                RepintaFondo();   // la luna vuelve a encenderse: que se vea ya
                pinta();
                ShowToast("Detalles como de fabrica", Color.FromArgb(40, 130, 90));
            };

            pg.Controls.Add(tVida); pg.Controls.Add(tResp); pg.Controls.Add(lPie); pg.Controls.Add(bResetD);
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
        private Panel PaginaColores(Font fNom, Font fDes, Font fBot,
                                    Color cTexto, Color cTenue, Color cApagado)
        {
            Color cAc = Color.FromArgb(190, 165, 255);
            Color cOn = Color.FromArgb(28, 62, 104), cOff = Color.FromArgb(28, 34, 62);
            Color cBordeOn = Color.FromArgb(150, 205, 255), cBordeOff = Color.FromArgb(60, 72, 118);

            Panel pg = new Panel();
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
            Label l1 = new Label(); l1.SetBounds(16, 36, 896, 62); l1.BackColor = Color.Transparent;
            l1.ForeColor = cTenue; l1.Font = fDes;
            l1.Text = "Los botones dan al tono del DISCO un peso de 20%, 50% o 100% en el acento; el resto viene del GENERO.\n"
                    + "Con genero, una nebulosa conserva su tono. BTS/Bangtan con genero mantiene el morado fijo.\n"
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
            Label l2 = new Label(); l2.SetBounds(16, 36, 896, 48); l2.BackColor = Color.Transparent;
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
            Label l3 = new Label(); l3.SetBounds(16, 36, 896, 48); l3.BackColor = Color.Transparent;
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
            Label l4 = new Label(); l4.SetBounds(16, 36, 896, 62); l4.BackColor = Color.Transparent;
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

            Label lPie = new Label(); lPie.SetBounds(330, 582, 614, 32); lPie.BackColor = Color.Transparent;
            lPie.ForeColor = cApagado; lPie.Font = fDes;
            lPie.Text = "La pared esta detras de esta ventana: toca una y mirala. Todo se guarda al\n"
                      + "cerrar. El coste depende del equipo; compruebalo con el medidor.";

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

        private Panel PaginaImagen(Font fNom, Font fVal, Font fDes, Font fBot,
                                   Color cTexto, Color cTenue, Color cApagado)
        {
            Color cMide = Color.FromArgb(140, 205, 255);
            Color cEfe = Color.FromArgb(178, 152, 255);
            Color cProp = Color.FromArgb(255, 176, 102);
            Color cBien = Color.FromArgb(120, 230, 165);
            Color cMal = Color.FromArgb(255, 170, 110);
            Color cOn = Color.FromArgb(28, 62, 104), cOff = Color.FromArgb(28, 34, 62);
            Color cBordeOn = Color.FromArgb(150, 205, 255), cBordeOff = Color.FromArgb(60, 72, 118);

            Panel pg = new Panel();
            pg.SetBounds(0, 96, 960, 636);
            pg.BackColor = Color.FromArgb(11, 14, 32);

            Tarjeta tMide = new Tarjeta(); tMide.SetBounds(16, 4, 456, 400); tMide.Titulo = "ESTE EQUIPO"; tMide.Acento = cMide;
            Tarjeta tProp = new Tarjeta(); tProp.SetBounds(16, 412, 456, 160); tProp.Titulo = "LA PROPUESTA"; tProp.Acento = cProp;
            pg.AutoScroll = true;
            // Reservar el ancho de la barra vertical: 944 px ya desbordaban
            // el area util de 943 px y hacian aparecer scroll horizontal.
            Tarjeta tEfe = new Tarjeta(); tEfe.SetBounds(480, 4, 456, 1026); tEfe.Titulo = "EFECTOS"; tEfe.Acento = cEfe;

            // ---------------- ESTE EQUIPO ----------------
            BotonPlano bMedir = new BotonPlano();
            bMedir.SetBounds(16, 40, 424, 36); bMedir.Font = fBot; bMedir.Text = "Medir este equipo";
            bMedir.Relleno = cOn; bMedir.Borde = cMide;
            Label lTarda = new Label(); lTarda.SetBounds(16, 82, 424, 32); lTarda.BackColor = Color.Transparent;
            lTarda.ForeColor = cTenue; lTarda.Font = fDes;
            lTarda.Text = "Tarda medio minuto. Pone una cancion muda y usa la rocola sola:\npasa de pagina, cambia de letra, escribe y entra en discos.";
            Label lCab = new Label(); lCab.SetBounds(16, 120, 424, 16); lCab.BackColor = Color.Transparent;
            lCab.ForeColor = cApagado; lCab.Font = fDes; lCab.Text = "PANTALLA                          TRABAJO   CUADROS/S";
            Label[] lFila = new Label[5];
            for (int i = 0; i < 5; i++)
            {
                lFila[i] = new Label(); lFila[i].SetBounds(16, 140 + i * 22, 424, 20);
                lFila[i].BackColor = Color.Transparent; lFila[i].ForeColor = cTexto; lFila[i].Font = fDes;
                tMide.Controls.Add(lFila[i]);
            }
            Label lTecho = new Label(); lTecho.SetBounds(16, 252, 424, 16); lTecho.BackColor = Color.Transparent;
            lTecho.ForeColor = cApagado; lTecho.Font = fDes;
            Label lSinMus = new Label(); lSinMus.SetBounds(16, 272, 424, 32); lSinMus.BackColor = Color.Transparent;
            lSinMus.ForeColor = Color.FromArgb(255, 196, 96); lSinMus.Font = fDes;
            Label lVer = new Label(); lVer.SetBounds(16, 308, 424, 80); lVer.BackColor = Color.Transparent;
            lVer.ForeColor = cTenue; lVer.Font = fDes;
            tMide.Controls.Add(bMedir); tMide.Controls.Add(lTarda); tMide.Controls.Add(lCab);
            tMide.Controls.Add(lTecho); tMide.Controls.Add(lSinMus); tMide.Controls.Add(lVer);

            // ---------------- LA PROPUESTA ----------------
            Label lProp = new Label(); lProp.SetBounds(16, 36, 424, 68); lProp.BackColor = Color.Transparent;
            lProp.ForeColor = cTenue; lProp.Font = fDes;
            BotonPlano bApli = new BotonPlano();
            bApli.SetBounds(16, 112, 424, 34); bApli.Font = fBot; bApli.Text = "Aplicar lo recomendado";
            tProp.Controls.Add(lProp); tProp.Controls.Add(bApli);

            // ---------------- EFECTOS: el mando general y las seis filas ----------------
            EfectoGrafico[] ef = Efectos();
            Label lGen = new Label(); lGen.SetBounds(16, 38, 424, 16); lGen.BackColor = Color.Transparent;
            lGen.ForeColor = cApagado; lGen.Font = fDes;
            BotonPlano[] bGen = new BotonPlano[3];
            string[] nomNivel = { "Bajo", "Medio", "Alto" };
            for (int k = 0; k < 3; k++)
            {
                bGen[k] = new BotonPlano(); bGen[k].SetBounds(16 + k * 144, 58, 136, 28);
                bGen[k].Font = fBot; bGen[k].Text = nomNivel[k];
                tEfe.Controls.Add(bGen[k]);
            }
            Label[] lNom = new Label[ef.Length], lCost = new Label[ef.Length];
            BotonPlano[][] bNiv = new BotonPlano[ef.Length][];
            for (int i = 0; i < ef.Length; i++)
            {
                // Paso de 62 y no de 68: el contenido de una fila mide 48, asi que quedan 14 px entre una y
                // otra en vez de 20 -se sigue leyendo igual de suelto- y con lo que se ahorra cabe abajo el
                // interruptor del hojeo ligero, que es la unica cosa de esta tarjeta que no tenia mando.
                int y = 100 + i * 62;
                lNom[i] = new Label(); lNom[i].SetBounds(16, y, 424, 18); lNom[i].BackColor = Color.Transparent;
                lNom[i].ForeColor = cTexto; lNom[i].Font = fNom; lNom[i].Text = ef[i].Nombre;
                lCost[i] = new Label(); lCost[i].SetBounds(322, y + 24, 118, 18); lCost[i].BackColor = Color.Transparent;
                lCost[i].ForeColor = cApagado; lCost[i].Font = fDes; lCost[i].TextAlign = ContentAlignment.TopRight;
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
            Interruptor swHojeo = new Interruptor();
            swHojeo.SetBounds(16, 466, 424, 26); swHojeo.ForeColor = cTexto; swHojeo.Font = fNom;
            swHojeo.Text = "Ocultar caratulas al pasar de pagina"; swHojeo.Acento = cEfe;
            swHojeo.Checked = hojeoLigero;
            Interruptor swNeon = new Interruptor();
            swNeon.SetBounds(16, 498, 424, 26); swNeon.ForeColor = cTexto; swNeon.Font = fNom;
            swNeon.Text = "Barra de progreso neon"; swNeon.Acento = cEfe; swNeon.Checked = progresoNeon;
            tEfe.Controls.Add(swNeon);
            Interruptor swReposo = new Interruptor();
            swReposo.SetBounds(16, 530, 424, 26); swReposo.ForeColor = cTexto; swReposo.Font = fNom;
            swReposo.Text = "Discos tenues en reposo con musica"; swReposo.Acento = cEfe; swReposo.Checked = muroReposo;
            tEfe.Controls.Add(swReposo);
            Interruptor swCine = new Interruptor();
            swCine.SetBounds(16, 562, 424, 26); swCine.ForeColor = cTexto; swCine.Font = fNom;
            swCine.Text = "Nebulosas azul / violeta (solo GPU)"; swCine.Acento = cEfe; swCine.Checked = nebulosasCine;
            tEfe.Controls.Add(swCine);
            Interruptor swVinetaSuave = new Interruptor();
            swVinetaSuave.SetBounds(16, 594, 424, 26); swVinetaSuave.ForeColor = cTexto; swVinetaSuave.Font = fNom;
            swVinetaSuave.Text = "Vineta suave (solo GPU)"; swVinetaSuave.Acento = cEfe; swVinetaSuave.Checked = vinetaSuave;
            tEfe.Controls.Add(swVinetaSuave);
            swVinetaSuave.CheckedChanged += delegate { vinetaSuave = swVinetaSuave.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            Interruptor swHaloSeleccion = new Interruptor();
            swHaloSeleccion.SetBounds(16, 626, 424, 26); swHaloSeleccion.ForeColor = cTexto; swHaloSeleccion.Font = fNom;
            swHaloSeleccion.Text = "Halo de seleccion (solo GPU)"; swHaloSeleccion.Acento = cEfe; swHaloSeleccion.Checked = haloSeleccion;
            tEfe.Controls.Add(swHaloSeleccion);
            swHaloSeleccion.CheckedChanged += delegate { haloSeleccion = swHaloSeleccion.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            Interruptor swEstrellasFinas = new Interruptor();
            swEstrellasFinas.SetBounds(16, 658, 424, 26); swEstrellasFinas.ForeColor = cTexto; swEstrellasFinas.Font = fNom;
            swEstrellasFinas.Text = "Estrellas finas (solo GPU)"; swEstrellasFinas.Acento = cEfe; swEstrellasFinas.Checked = estrellasFinas;
            tEfe.Controls.Add(swEstrellasFinas);
            swEstrellasFinas.CheckedChanged += delegate { estrellasFinas = swEstrellasFinas.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            Interruptor swOndasSuaves = new Interruptor();
            swOndasSuaves.SetBounds(16, 690, 424, 26); swOndasSuaves.ForeColor = cTexto; swOndasSuaves.Font = fNom;
            swOndasSuaves.Text = "Ondas suaves (solo GPU)"; swOndasSuaves.Acento = cEfe; swOndasSuaves.Checked = ondasSuaves;
            tEfe.Controls.Add(swOndasSuaves);
            swOndasSuaves.CheckedChanged += delegate { ondasSuaves = swOndasSuaves.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            Interruptor swFluido = new Interruptor();
            swFluido.SetBounds(16, 722, 424, 26); swFluido.ForeColor = cTexto; swFluido.Font = fNom;
            swFluido.Text = "Progreso ultrafluido (solo GPU)"; swFluido.Acento = cEfe; swFluido.Checked = progresoFluido;
            tEfe.Controls.Add(swFluido);
            swFluido.CheckedChanged += delegate { progresoFluido = swFluido.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            Interruptor swExpandido = new Interruptor();
            swExpandido.SetBounds(16, 754, 424, 26); swExpandido.ForeColor = cTexto; swExpandido.Font = fNom;
            swExpandido.Text = "Estado y cola ampliados en reposo (GPU)"; swExpandido.Acento = cEfe; swExpandido.Checked = estadoExpandido;
            tEfe.Controls.Add(swExpandido);
            swExpandido.CheckedChanged += delegate { estadoExpandido = swExpandido.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            Interruptor swneonIntenso = new Interruptor();
            swneonIntenso.SetBounds(16, 786, 424, 26); swneonIntenso.ForeColor = cTexto; swneonIntenso.Font = fNom;
            swneonIntenso.Text = "Neon de progreso intenso (GPU)"; swneonIntenso.Acento = cEfe; swneonIntenso.Checked = neonIntenso;
            tEfe.Controls.Add(swneonIntenso);
            swneonIntenso.CheckedChanged += delegate { neonIntenso = swneonIntenso.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            Interruptor swestadoTransparente = new Interruptor();
            swestadoTransparente.SetBounds(16, 818, 424, 26); swestadoTransparente.ForeColor = cTexto; swestadoTransparente.Font = fNom;
            swestadoTransparente.Text = "Estado ampliado transparente (GPU)"; swestadoTransparente.Acento = cEfe; swestadoTransparente.Checked = estadoTransparente;
            tEfe.Controls.Add(swestadoTransparente);
            swestadoTransparente.CheckedChanged += delegate { estadoTransparente = swestadoTransparente.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            Interruptor swcancionesTransparentes = new Interruptor();
            swcancionesTransparentes.SetBounds(16, 850, 424, 26); swcancionesTransparentes.ForeColor = cTexto; swcancionesTransparentes.Font = fNom;
            swcancionesTransparentes.Text = "Lista de canciones transparente (GPU)"; swcancionesTransparentes.Acento = cEfe; swcancionesTransparentes.Checked = cancionesTransparentes;
            tEfe.Controls.Add(swcancionesTransparentes);
            swcancionesTransparentes.CheckedChanged += delegate { cancionesTransparentes = swcancionesTransparentes.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            Interruptor swbuscadorCache = new Interruptor();
            swbuscadorCache.SetBounds(16, 882, 424, 26); swbuscadorCache.ForeColor = cTexto; swbuscadorCache.Font = fNom;
            swbuscadorCache.Text = "Optimizar seleccion del buscador (GPU)"; swbuscadorCache.Acento = cEfe; swbuscadorCache.Checked = buscadorCache;
            tEfe.Controls.Add(swbuscadorCache);
            swbuscadorCache.CheckedChanged += delegate { buscadorCache = swbuscadorCache.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            Interruptor swcieloProfundo = new Interruptor();
            swcieloProfundo.SetBounds(16, 914, 424, 26); swcieloProfundo.ForeColor = cTexto; swcieloProfundo.Font = fNom;
            swcieloProfundo.Text = "Nebulosas con detalle natural (GPU)"; swcieloProfundo.Acento = cEfe; swcieloProfundo.Checked = cieloProfundo;
            tEfe.Controls.Add(swcieloProfundo);
            swcieloProfundo.CheckedChanged += delegate { cieloProfundo = swcieloProfundo.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            Interruptor swauroraBandas = new Interruptor();
            swauroraBandas.SetBounds(16, 946, 424, 26); swauroraBandas.ForeColor = cTexto; swauroraBandas.Font = fNom;
            swauroraBandas.Text = "Aurora con bandas mas definidas (GPU)"; swauroraBandas.Acento = cEfe; swauroraBandas.Checked = auroraBandas;
            tEfe.Controls.Add(swauroraBandas);
            swauroraBandas.CheckedChanged += delegate { auroraBandas = swauroraBandas.Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            Label lPierde = new Label(); lPierde.SetBounds(16, 978, 424, 40); lPierde.BackColor = Color.Transparent;
            lPierde.ForeColor = cTenue; lPierde.Font = fDes;
            tEfe.Controls.Add(lGen); tEfe.Controls.Add(swHojeo); tEfe.Controls.Add(lPierde);

            BotonPlano bResetG = new BotonPlano();
            bResetG.SetBounds(16, 1042, 300, 38); bResetG.Font = fBot; bResetG.Text = "Graficos como de fabrica";

            int ultimoTocado = 0;

            // ---------------- LO QUE SE ENSENA, SEGUN LO QUE HAYA ----------------
            Action pinta = delegate
            {
                // Los tres botones de cada fila, y el mando general si TODAS estan en el mismo nivel.
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
                    lCost[i].Text = coste; lCost[i].ForeColor = cc;
                }
                for (int k = 0; k < 3; k++)
                {
                    bool act = (k == comun);
                    bGen[k].Relleno = act ? cOn : cOff;
                    bGen[k].Borde = act ? cBordeOn : cBordeOff;
                    bGen[k].ForeColor = act ? Color.FromArgb(226, 240, 255) : cApagado;
                    bGen[k].Invalidate();
                }
                lGen.Text = "Todo de golpe:" + ((comun < 0) ? "        (ahora esta a medida)" : "");
                int nv = LeeNivel(ef[ultimoTocado].Clave);
                lPierde.Text = ef[ultimoTocado].Nombre + " en " + nomNivel[nv].ToLower() + ":\n" + ef[ultimoTocado].Pierde[nv];

                // ---- La tabla de lo medido ----
                for (int i = 0; i < lFila.Length; i++) lFila[i].Text = "";
                if (bancoEscenas == null)
                {
                    lCab.Visible = false; lTecho.Text = ""; lSinMus.Text = "";
                    lVer.ForeColor = cTenue;
                    lVer.Text = "Aun sin medir.\n\nAqui no hay ningun numero porque ninguno seria de verdad:"
                              + " los de este equipo salen de medirlo.";
                    lProp.Text = "Sin medir todavia. La propuesta sale de lo que se mida en ESTE equipo, no de una"
                               + " lista de equipos buenos y malos escrita a mano.";
                    bApli.Enabled = false; bApli.Relleno = cOff; bApli.Borde = cBordeOff; bApli.ForeColor = cApagado;
                    return;
                }
                lCab.Visible = true;
                for (int i = 0; i < bancoEscenas.Count && i < lFila.Length; i++)
                {
                    EscenaMedida m = bancoEscenas[i];
                    string nom = m.Nombre;
                    while (nom.Length < 28) nom += " ";
                    lFila[i].Text = nom + m.MsTrabajo.ToString("0.0") + " ms" + (m.MsTrabajo < 10 ? "  " : " ") + "     " + m.Fps
                                  + (EsLaPeor(i) ? "   <-- la que manda" : "");
                    lFila[i].ForeColor = (m.Fps >= MetaFps) ? cBien : cMal;
                }

                lTecho.Text = "El techo de esta pantalla son " + bancoTope + " cuadros por segundo.";
                lSinMus.Text = bancoConMusica ? "" :
                      "OJO: no se pudo poner ninguna cancion y se midio en\n"
                    + "silencio. De verdad saldra peor de lo que dice aqui.";
                if (!string.IsNullOrEmpty(bancoError))
                {
                    lTecho.Text = ""; lVer.ForeColor = cMal;
                    lVer.Text = "Medicion incompleta: " + bancoError;
                    lProp.Text = "No se aplican recomendaciones de una medicion fallida. Revisa el dispositivo y vuelve a medir.";
                    bApli.Enabled = false; bApli.Relleno = cOff; bApli.Borde = cBordeOff; bApli.ForeColor = cApagado;
                    return;
                }
                if (!bancoConGpu)
                {
                    lTecho.Text = "";
                    lVer.ForeColor = cMal;
                    lVer.Text = "Esta rocola NO esta dibujando con la tarjeta grafica.\n\nPor ese camino el techo son"
                              + " 24 cuadros por segundo, puesto asi a proposito para gastar poco. La perilla que"
                              + " importa aqui es encender la tarjeta.";
                    lProp.Text = "Sin tarjeta grafica no hay nada que proponer aqui: bajar efectos no sube un techo que"
                               + " no es de la tarjeta.";
                    bApli.Enabled = false; bApli.Relleno = cOff; bApli.Borde = cBordeOff; bApli.ForeColor = cApagado;
                    return;
                }

                string peor = "La que peor va es \"" + bancoPeor + "\": " + bancoFpsPeor + " cuadros por segundo"
                            + " (" + bancoMsPeor.ToString("0.0") + " ms de trabajo por cuadro).";
                if (bancoFpsPeor >= MetaFps)
                {
                    lVer.ForeColor = cBien;
                    lVer.Text = peor + "\n\nPasa de los 30 de sobra. No hace falta bajar nada.";
                    lProp.Text = "Nada que cambiar: la pantalla que peor va ya da " + bancoFpsPeor + " cuadros por"
                               + " segundo, por encima de los 30 que se pedian.";
                    bApli.Enabled = false; bApli.Relleno = cOff; bApli.Borde = cBordeOff; bApli.ForeColor = cApagado;
                    return;
                }

                lVer.ForeColor = cMal;
                lVer.Text = peor + "\n\nNo llega a los 30. Al lado de cada cosa, a la derecha, esta lo que se gana"
                          + " bajandola un escalon en ESTE equipo.";

                // Que escalones propone, dicho en palabras. Se nombran los DOS primeros y del resto se dice
                // cuantos son: nombrarlos todos son seis nombres largos y el hueco no da para tanto.
                string cuales = ""; int cuantos = 0, nombrados = 0;
                if (bancoPropuesta != null)
                    for (int i = 0; i < ef.Length && i < bancoPropuesta.Length; i++)
                    {
                        if (bancoPropuesta[i] >= NivAlto) continue;
                        cuantos++;
                        if (nombrados < 2)
                        {
                            cuales += (cuales.Length == 0 ? "" : ", ") + ef[i].Nombre.ToLower() + " a " + nomNivel[bancoPropuesta[i]].ToLower();
                            nombrados++;
                        }
                    }
                if (cuantos > nombrados) cuales += " y " + (cuantos - nombrados) + " mas";
                if (cuantos == 0)
                {
                    lProp.Text = "No llega a los 30 y NO hay nada que bajar que lo arregle: ninguno de los efectos"
                               + " cuesta lo suficiente como para notarse. El cuello esta en otro sitio, no en los"
                               + " adornos del cielo.";
                    bApli.Enabled = false; bApli.Relleno = cOff; bApli.Borde = cBordeOff; bApli.ForeColor = cApagado;
                    return;
                }
                bool llega = bancoFpsPropuesta >= MetaFps;
                // Corto a proposito: el hueco da para cuatro renglones y el "como" ya esta dicho arriba.
                lProp.Text = "Bajar " + cuales + " deja \"" + bancoPeor + "\" en " + bancoFpsPropuesta + "."
                           + (llega ? "  Con eso pasa de los 30." : "  AUN ASI NO LLEGA A 30: este equipo no da"
                                    + " para mas ni bajandolo todo.");
                bApli.Enabled = true; bApli.Relleno = Color.FromArgb(30, 96, 70);
                bApli.Borde = Color.FromArgb(72, 182, 132); bApli.ForeColor = Color.White;
            };

            // ---------------- LO QUE HACEN LOS BOTONES ----------------
            bMedir.Click += delegate
            {
                f3Medir = true;
                Form f = pg.FindForm();
                if (f != null) f.Close();   // la ventana se cierra, se mide con la pantalla libre, y vuelve sola
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
                for (int k = 0; k < 3; k++)
                    if (so == bGen[k]) foreach (EfectoGrafico e in ef) PonNivel(e.Clave, k);
                f3CambioGraf = true;
                pinta();
            };
            for (int k = 0; k < 3; k++) bGen[k].Click += alGeneral;
            swHojeo.CheckedChanged += delegate
            {
                hojeoLigero = swHojeo.Checked;
                AplicaEfectosGpu();
                f3CambioGraf = true;
            };
            swNeon.CheckedChanged += delegate
            {
                progresoNeon = swNeon.Checked;
                AplicaEfectosGpu();
                f3CambioGraf = true;
            };
            swReposo.CheckedChanged += delegate
            {
                muroReposo = swReposo.Checked;
                f3CambioGraf = true;
            };
            swCine.CheckedChanged += delegate
            {
                nebulosasCine = swCine.Checked;
                AplicaEfectosGpu();
                f3CambioGraf = true;
            };

            bApli.Click += delegate
            {
                if (bancoPropuesta == null) return;
                for (int i = 0; i < ef.Length && i < bancoPropuesta.Length; i++) PonNivel(ef[i].Clave, bancoPropuesta[i]);
                f3CambioGraf = true;
                pinta();
                ShowToast("Graficos ajustados a este equipo", Color.FromArgb(40, 130, 90));
            };
            bResetG.Click += delegate
            {
                foreach (EfectoGrafico e in ef) PonNivel(e.Clave, NivAlto);
                fpsTope = 0;
                hojeoLigero = true; swHojeo.Checked = true;   // de fabrica viene encendido: no se ve y paga
                progresoNeon = true; swNeon.Checked = true;
                muroReposo = true; swReposo.Checked = true;
                nebulosasCine = true; swCine.Checked = true;
                vinetaSuave = true; swVinetaSuave.Checked = true;
                haloSeleccion = true; swHaloSeleccion.Checked = true;
                estrellasFinas = true; swEstrellasFinas.Checked = true;
                ondasSuaves = true; swOndasSuaves.Checked = true;
                progresoFluido = true; swFluido.Checked = true;
                estadoExpandido = true; swExpandido.Checked = true;
                neonIntenso = true; swneonIntenso.Checked = true;
                estadoTransparente = true; swestadoTransparente.Checked = true;
                cancionesTransparentes = true; swcancionesTransparentes.Checked = true;
                buscadorCache = true; swbuscadorCache.Checked = true;
                cieloProfundo = true; swcieloProfundo.Checked = true;
                auroraBandas = true; swauroraBandas.Checked = true;
                AplicaEfectosGpu();
                f3CambioGraf = true;
                pinta();
            };

            pg.Controls.Add(tMide); pg.Controls.Add(tProp); pg.Controls.Add(tEfe); pg.Controls.Add(bResetG);
            pinta();
            return pg;
        }
    }
}
