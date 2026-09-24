// LOS DATOS: donde viven, como se guardan sin perderse, respaldo y estadisticas
//
// Parte de Form1. La clase esta repartida en varios archivos por temas -son todos "partial class Form1"-
// para poder venir a tocar UNA cosa sin leerse el programa entero. Aqui no hay nada nuevo: es el mismo
// codigo que estaba en Form1.cs, movido tal cual.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rocola
{
    public partial class Form1 : Form
    {
        private string backupDir = null;                                                     // 2o destino del respaldo (idealmente OTRO disco); "respaldo_dir=" en config
        // DURACION de cada cancion (segundos). La calcula el PRE-ANALISIS junto con la sonoridad y se guarda en
        // el mismo archivo. Ademas de mostrarse en la lista, sirve de marca de "ya pre-analizada": esas canciones
        // ya tienen una medida COMPLETA y fiable, asi que sonar la cancion no vuelve a tocar su volumen medido.
        private Dictionary<string, float> durCache = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);     // ruta -> duracion en segundos
        // DURACION RAPIDA. Sacar SOLO la duracion de un archivo cuesta ~600 veces menos que el analisis
        // completo: es leer la cabecera, no descomprimir el audio. Antes la duracion venia "de regalo" con ese
        // analisis pesado, asi que con cien mil canciones practicamente ninguna la mostraba (el analisis tarda
        // dias). Ahora va en su PROPIA pasada, que barre la biblioteca entera en minutos. Cuando el analisis
        // completo mide una cancion, SU duracion (del archivo entero) manda sobre esta.
        private Dictionary<string, float> durFast = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> durFastPend = new List<string>();   // lineas apuntadas y aun sin escribir
        private readonly object durFastLock = new object();
        private System.Threading.Thread durScanTh;
        private volatile int durScanGen = 0;
        private volatile int durScanLeft = 0;   // cuantas duraciones faltan (para el panel)
        // Canciones que YA recibieron el repaso A FONDO (cancion completa). Se guarda en disco para que el
        // entrenamiento a fondo CONTINUE donde iba al detenerlo y volverlo a encender, al cerrar la rocola o
        // si se va la luz. Sin esto, cada arranque volvia a empezar por la primera cancion de la biblioteca y
        // con una biblioteca grande el repaso no terminaba nunca.
        private HashSet<string> deepDone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private long statsTotal = 0, statsSongTotal = 0, statsVideoTotal = 0, statsCoins = 0, statsCreditsAdded = 0;
        private volatile bool statsDirty = false;
        private System.Windows.Forms.Timer statsTimer;
        private long statsSeq = 0;

        // Datos preparados sin tocar el formulario. Se publican juntos en OnShown, antes
        // de abrir la biblioteca; mientras BuildUi/modelos/config trabajan, este hilo lee y parsea.
        private sealed class StartupData
        {
            public Dictionary<string, float> Loud = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, float> Dur = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, float> Fast = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, float[]> Eq = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, int> Ia = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> Deep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> Incompatible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public int EqVersion, Healthy;
            public float[] Average;
            public bool OnlyHealthy;
            public StartupStats Stats;
            // La sonoridad integrada (ver Sonoridad.cs). Estado 0 = este trabajador no la leyo.
            public Dictionary<string, SonoridadIntegrada> Integ = new Dictionary<string, SonoridadIntegrada>(StringComparer.OrdinalIgnoreCase);
            public List<string> IntegAjenas = new List<string>();
            public int IntegEstado;
        }

        private sealed class StartupLoad
        {
            public string[] Paths;
            public volatile bool Cancelled;
            public bool Done;
            public StartupData Data;
        }

        private static StartupData ReadStartupData(StartupLoad load)
        {
            if (load.Cancelled) return null;
            StartupData data = new StartupData();
            for (int table = 0; table < Math.Min(8, load.Paths.Length); table++)
            {
                if (load.Cancelled) return null;
                try
                {
                    if (table == 7)
                    {
                        // La integrada tiene sus reglas: si no existe se empieza de cero, pero si existe y no se
                        // puede leer -o la escribio otra version- no se usa ni se pisa (ver Sonoridad.cs).
                        Dictionary<string, SonoridadIntegrada> integ = new Dictionary<string, SonoridadIntegrada>(StringComparer.OrdinalIgnoreCase);
                        List<string> ajenas = new List<string>();
                        int estado = LeeIntegArchivo(load.Paths[7], delegate(TextReader r) { return new StartupTextReader(r, load); }, integ, ajenas);
                        data.Integ = integ; data.IntegAjenas = ajenas; data.IntegEstado = estado;
                        continue;
                    }
                    // Un solo lector secuencial: no multiplicar accesos al HDD ni trabajadores en 2C.
                    using (FileStream stream = new FileStream(load.Paths[table], FileMode.Open, FileAccess.Read,
                        FileShare.Read, 65536, FileOptions.SequentialScan))
                    using (StartupTextReader reader = new StartupTextReader(new StreamReader(stream), load))
                        ReadStartupTableRecords(reader, table, data);
                }
                catch (OperationCanceledException) { return null; }
                catch { }   // archivo opcional ausente/ilegible: conserva esa tabla vacia, nunca parcial
            }
            if (load.Cancelled) return null;
            ComputeEqAverage(data.Eq, ref data.Average, ref data.Healthy, ref data.OnlyHealthy);
            return load.Cancelled ? null : data;
        }

        // Cada parser llena una tabla privada. Solo al llegar a EOF se adopta su referencia:
        // evita duplicar todos los buckets/entries y recorrerlos de nuevo durante el arranque.
        // Los lectores usados sobre tablas vivas conservan su publicacion transaccional original.
        private static void ReadStartupTableRecords(TextReader reader, int table, StartupData data)
        {
            switch (table)
            {
                case 0:
                    Dictionary<string, float> loud = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                    Dictionary<string, float> dur = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                    FillLoudCacheRecords(reader, loud, dur);
                    data.Loud = loud; data.Dur = dur;
                    break;
                case 1:
                    Dictionary<string, float[]> eq = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
                    int version = FillEqCacheRecords(reader, eq);
                    data.Eq = eq; data.EqVersion = version;
                    break;
                case 2:
                    Dictionary<string, int> ia = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    FillIaDoneRecords(reader, ia, null);
                    data.Ia = ia;
                    break;
                case 3:
                    HashSet<string> deep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    // Solo aceptar marcas contra las medidas de ESTA instantanea.
                    FillDeepDoneRecords(reader, deep, data.Loud, data.Dur, data.Eq);
                    data.Deep = deep;
                    break;
                case 4:
                    Dictionary<string, float> fast = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                    FillDurFastRecords(reader, fast);
                    data.Fast = fast;
                    break;
                case 5:
                    HashSet<string> incompatible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    FillIncompatibleRecords(reader, incompatible);
                    data.Incompatible = incompatible;
                    break;
                case 6:
                    data.Stats = ReadStatsRecords(reader);
                    break;
                default: throw new ArgumentOutOfRangeException("table");
            }
        }

        private sealed class StartupTextReader : TextReader
        {
            private readonly TextReader inner;
            private readonly StartupLoad load;
            public StartupTextReader(TextReader inner, StartupLoad load) { this.inner = inner; this.load = load; }
            public override string ReadLine()
            {
                // Cancelar entre registros, no solo despues de un archivo potencialmente enorme.
                if (load.Cancelled) throw new OperationCanceledException();
                string line = inner.ReadLine();
                if (load.Cancelled) throw new OperationCanceledException();
                return line;
            }
            protected override void Dispose(bool disposing)
            {
                try { if (disposing) inner.Dispose(); }
                finally { base.Dispose(disposing); }
            }
        }

        private static StartupLoad CreateStartupLoad(string[] paths)
        {
            StartupLoad load = new StartupLoad(); load.Paths = (string[])paths.Clone();
            System.Threading.Thread worker = new System.Threading.Thread(delegate()
            {
                StartupData data = null;
                try { data = ReadStartupData(load); } catch { }   // fallback de lectura en el hilo original
                finally
                {
                    lock (load)
                    {
                        load.Data = load.Cancelled ? null : data;
                        load.Done = true;
                        System.Threading.Monitor.PulseAll(load);
                    }
                }
            });
            worker.IsBackground = true;
            worker.Priority = System.Threading.ThreadPriority.BelowNormal;
            worker.CurrentCulture = CultureInfo.CurrentCulture;   // los contadores antiguos usan la cultura del llamador
            worker.Start();
            return load;
        }

        private void BeginStartupDataLoad()
        {
            startupReadPending = true;
            try
            {
                startupLoad = CreateStartupLoad(new string[] { LoudFile(), EqFile(), IaDoneFile(),
                    DeepDoneFile(), DurFastFile(), RutaDatos("rocola_incompatibles.txt"), LibStatsFile(), IntegFile() });
            }
            catch { startupLoad = null; }   // no cambia el arranque si no se pudo crear el trabajador
        }

        private bool CompleteStartupDataLoad()
        {
            StartupLoad load = startupLoad;
            if (load == null || startupClosing) return false;
            StartupData data;
            lock (load)
            {
                // Barrera deliberada: OnShown no devuelve el control antes de tener las tablas.
                // Solapa la preparacion, pero no adelanta entrada de monedas ni restauracion.
                while (!load.Done && !load.Cancelled) System.Threading.Monitor.Wait(load);
                data = load.Cancelled ? null : load.Data;
                load.Data = null;
            }
            startupLoad = null;
            if (data == null || startupClosing) return false;
            // Solo el hilo UI publica referencias. No hay Merge ni copia de cientos de miles de filas.
            InvalidateVideoMeasurements();
            loudCache = data.Loud; durCache = data.Dur; durFast = data.Fast; eqCache = data.Eq;
            iaDone = data.Ia; deepDone = data.Deep; incompatibleLogged = data.Incompatible;
            if (data.IntegEstado == 0) LoadIntegCache();   // el trabajador no la leyo: se lee aqui
            else { integCache = data.Integ; integAjenas = data.IntegAjenas; integEstado = data.IntegEstado; }
            eqCacheVer = data.EqVersion; eqAvg = data.Average;
            eqAvgSanas = data.Healthy; eqAvgSoloSanas = data.OnlyHealthy;
            RememberEqAverage();
            PublishStartupStats(data.Stats);
            startupReadPending = false;
            return true;
        }

        private void CancelStartupDataLoad()
        {
            startupClosing = true;
            StartupLoad load = startupLoad; startupLoad = null;
            if (load == null) return;
            lock (load)
            {
                load.Cancelled = true; load.Data = null;
                System.Threading.Monitor.PulseAll(load);
            }
        }

        // ================== DONDE VIVE LO QUE LA ROCOLA GUARDA ==================
        //
        // La rocola escribe una veintena de archivos: los creditos y la cola -para sobrevivir a un apagon-,
        // el volumen medido de cada cancion, la huella del EQ, lo mas sonado, el indice de la biblioteca,
        // los ajustes y el mapa de botones. Hasta ahora iban todos a la carpeta del PROGRAMA.
        //
        // Aqui eso funciona -el programa vive en una carpeta normal donde se puede escribir-, pero deja al
        // operador sin poder decir "los datos y la musica van en el otro disco". Y en la rocola de Linux es
        // peor: alli el programa va dentro de un archivo de SOLO LECTURA y lo que se escriba "por encima" se
        // queda en la memoria hasta que el sistema guarde, o sea que un apagon se llevaria los creditos de
        // la noche -justo lo que el guardado seguro de aqui abajo existe para evitar-.
        //
        // Asi que hay UN solo sitio donde se decide: la variable de entorno ROCOLA_DATOS si esta puesta y
        // apunta a algo donde se pueda escribir, y si no la carpeta del programa, como siempre. Mientras
        // nadie la ponga -que es lo normal- no cambia absolutamente nada.
        //
        // Se calcula UNA vez: si cambiara a media sesion, la mitad de los archivos acabarian en un sitio y
        // la otra mitad en otro.
        private static string carpetaDatos;
        internal static string CarpetaDatos
        {
            get
            {
                if (carpetaDatos != null) return carpetaDatos;
                string d = null;
                try { d = Environment.GetEnvironmentVariable("ROCOLA_DATOS"); } catch { }
                if (!string.IsNullOrEmpty(d))
                {
                    d = d.Trim();
                    try
                    {
                        if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                        // Se comprueba que de verdad se puede escribir ANTES de fiarse: si el disco de la
                        // musica no estuviera montado todavia, la ruta existiria vacia y la rocola escribiria
                        // en un sitio que va a desaparecer. Mejor volver a la carpeta del programa.
                        string prueba = Path.Combine(d, "rocola_escribible.tmp");
                        File.WriteAllText(prueba, "ok");
                        File.Delete(prueba);
                        carpetaDatos = d;
                        return carpetaDatos;
                    }
                    catch { }
                }
                carpetaDatos = Application.StartupPath;
                return carpetaDatos;
            }
        }
        private static string RutaDatos(string nombre) { return Path.Combine(CarpetaDatos, nombre); }

        // Un archivo que la rocola LEE pero no escribe -los modelos de IA entrenados- se busca primero en la
        // carpeta de datos y luego junto al programa. Asi el modelo que viene de fabrica funciona sin tocar
        // nada, y si se entrena uno nuevo basta con dejarlo en la carpeta de datos: dentro del .sfs, que es
        // de solo lectura, no se podria.
        private static string RutaLectura(string nombre)
        {
            string a = RutaDatos(nombre);
            try { if (File.Exists(a)) return a; } catch { }
            return Path.Combine(Application.StartupPath, nombre);
        }

        private string ConfigFile { get { return RutaDatos("rocola_config.txt"); } }

        // ===================== PREFERENCIAS (rocola_config.txt) =====================
        // gpu=1/0 (render por GPU, Ctrl+F12) y dir=<ruta> (carpetas de musica ADICIONALES, Ctrl+F2; una por
        // linea, pueden estar en otros discos). Ambos savers escriben el archivo COMPLETO: nada se pisa.
        private bool gpuPrefOn = true;                                  // preferencia de render (GPU por defecto)
        // ANIMACION al entrar/salir de un disco: la portada hace zoom y se desvanece. Es SOLO un dibujo ENCIMA:
        // el cambio de pantalla sigue siendo instantaneo como siempre, asi que si la animacion se corta a media
        // (alguien pulsa algo, entra el modo atraccion, arranca un video...) no queda ningun estado a medias.
        // Necesita render por GPU: por CPU, escalar y mezclar una imagen grande por cuadro no da el ancho.
        // El AVANCE lo calcula el render (60 fps por Application.Idle), no el temporizador de 42 ms: asi el
        // zoom se ve fluido de verdad. El temporizador solo APAGA la animacion al cumplirse el tiempo.
#if ROCOLA_GPU
        // CUANTO DURA el vuelo de la portada al entrar/salir de un disco. Deja de ser fijo: en MEDIO dura
        // 160 ms -sigue siendo un vuelo, pero se ve menos rato y por tanto se dibuja menos- y en BAJO no hay
        // animacion (transAnimOn en falso, ver mas abajo). Los 260 de siempre son el ALTO.
        private const int TransMsAlto = 260, TransMsMedio = 160;
        private int TransMs { get { return (animNiv >= NivAlto) ? TransMsAlto : TransMsMedio; } }
        private const int WinMs = 210;    // ventana del buscador abriendo/cerrando (mas corta: tipo macOS)
        private const float TransHold = 0.58f;   // hasta aqui la portada va opaca; despues se funde con el cubo de destino
        private int transMode = 0;        // 0 = nada, 1 = animacion de portada viva
        private int transTick = 0;        // TickCount del arranque
        private RectangleF transFrom;     // celda del cubo de ORIGEN (rejilla, disco, atraccion o buscador)
        private RectangleF transTo;       // celda del cubo de DESTINO
        private double transAng0;         // giro del cubo de origen (para arrancar calcado sobre el)
        private double transSpin;         // cuanto gira en total (camino corto al destino + media vuelta de gracia)
        private Image transImg;           // portada que se anima
        private RectangleF transNextFrom; // origen del PROXIMO EnterAlbum (se fija antes de cerrar el buscador)
        private double transNextAngle;    // y su giro
        private int winMode = 0;          // 0 = nada, 1 = abriendo el buscador, 2 = cerrandolo
        private int winTick = 0;
        private Bitmap winSnap;           // instantanea del buscador para animarlo AL CERRAR (al abrir se anima la textura de UI)
        private bool winRobada;           // ...o, sin copia, la textura que ya tenia la UI (BuscadorLigeroGpu.cs)
#endif
        // El zoom al entrar/salir de un disco: hay o no hay. Ya no es un campo aparte que haya que
        // mantener a mano en dos sitios: SALE del nivel, y asi no se pueden contradecir nunca.
        private bool transAnimOn { get { return animNiv > NivBajo; } }
        private bool diagOn = false;      // "diagnostico=" en config: apuntar eventos clave en rocola_diagnostico.txt
        private readonly List<string> extraMusicDirs = new List<string>();   // carpetas de musica extra (ademas de "musica")

        private void LoadConfigPrefs()
        {
            try
            {
                if (!File.Exists(ConfigFile)) return;
                foreach (string line in File.ReadAllLines(ConfigFile))
                {
                    string s = line.Trim();
                    if (s.StartsWith("gpu=")) gpuPrefOn = s.Substring(4).Trim() != "0";
                    else if (s.StartsWith("animacion=")) animNiv = NivelDe(s.Substring(10));   // zoom al entrar/salir de un disco
                    else if (s.StartsWith("espectro=")) { float v; if (float.TryParse(s.Substring(9).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) auroraAmount = Clampf(v / 100f, 0f, 1f); }   // aurora del horizonte (0 = apagada)
                    else if (s.StartsWith("tope_fps=")) { int v; if (int.TryParse(s.Substring(9).Trim(), out v)) fpsTope = (v < 0) ? 0 : v; }   // cuadros/s del fondo (0 = automatico, segun la pantalla)
                    else if (s.StartsWith("estrellas=")) { int v; if (int.TryParse(s.Substring(10).Trim(), out v)) estrellasDens = Clampf(v / 100f, 0.1f, 1f); }   // cuantas estrellas (%)
                    else if (s.StartsWith("fugaces=")) fugacesNiv = NivelDe(s.Substring(8));     // estrellas fugaces
                    else if (s.StartsWith("nieblas=")) nieblasNiv = NivelDe(s.Substring(8));     // nebulosas y neblina de color
                    else if (s.StartsWith("patron=")) relevoNiv = NivelDe(s.Substring(7));       // relevo de patron cada minuto
                    else if (s.StartsWith("hojeo_ligero=")) hojeoLigero = s.Substring(13).Trim() != "0";   // aligerar mientras se pasa de pagina
                    else if (s.StartsWith("progreso_neon=")) progresoNeon = s.Substring(14).Trim() != "0";
                    else if (s.StartsWith("muro_reposo=")) muroReposo = s.Substring(12).Trim() != "0";
                    else if (s.StartsWith("nebulosas_cine=")) nebulosasCine = s.Substring(15).Trim() != "0";
                    else if (s.StartsWith("vineta_suave=")) vinetaSuave = s.Substring(13).Trim() != "0";
                    else if (s.StartsWith("halo_seleccion=")) haloSeleccion = s.Substring(15).Trim() != "0";
                    else if (s.StartsWith("estrellas_finas=")) estrellasFinas = s.Substring(16).Trim() != "0";
                    else if (s.StartsWith("ondas_suaves=")) ondasSuaves = s.Substring(13).Trim() != "0";
                    else if (s.StartsWith("progreso_fluido=")) progresoFluido = s.Substring(16).Trim() != "0";
                    else if (s.StartsWith("estado_expandido=")) estadoExpandido = s.Substring(17).Trim() != "0";
                    else if (s.StartsWith("neon_intenso=")) neonIntenso = s.Substring(13).Trim() != "0";
                    else if (s.StartsWith("estado_transparente=")) estadoTransparente = s.Substring(20).Trim() != "0";
                    else if (s.StartsWith("canciones_transparentes=")) cancionesTransparentes = s.Substring(24).Trim() != "0";
                    else if (s.StartsWith("canciones_cristal=")) cancionesCristal = s.Substring(18).Trim() != "0";
                    else if (s.StartsWith("cola_doce=")) colaDoce = s.Substring(10).Trim() != "0";
                    else if (s.StartsWith("glass_capas=")) glassCapas = s.Substring(12).Trim() != "0";
                    else if (s.StartsWith("colores_cache=")) coloresCache = s.Substring(14).Trim() != "0";
                    else if (s.StartsWith("luna_activa=")) lunaActiva = s.Substring(12).Trim() != "0";
                    else if (s.StartsWith("paleta_completa=")) paletaCompleta = s.Substring(16).Trim() != "0";
                    else if (s.StartsWith("controles_reposo=")) controlesReposo = s.Substring("controles_reposo=".Length).Trim() != "0";
                    else if (s.StartsWith("ondas_unidas=")) ondasUnidas = s.Substring("ondas_unidas=".Length).Trim() != "0";
                    else if (s.StartsWith("luna_realista=")) lunaRealista = s.Substring("luna_realista=".Length).Trim() != "0";
                    else if (s.StartsWith("halo_lunar=")) haloLunar = s.Substring("halo_lunar=".Length).Trim() != "0";
                    else if (s.StartsWith("polvo_lunar=")) polvoLunar = s.Substring("polvo_lunar=".Length).Trim() != "0";
                    else if (s.StartsWith("luna_protagonista=")) lunaProtagonista = s.Substring("luna_protagonista=".Length).Trim() != "0";
                    else if (s.StartsWith("estela_estelar=")) estelaEstelar = s.Substring("estela_estelar=".Length).Trim() != "0";
                    else if (s.StartsWith("estrella_3d=")) estrella3d = s.Substring("estrella_3d=".Length).Trim() != "0";
                    else if (s.StartsWith("colores_fluidos=")) coloresFluidos = s.Substring("colores_fluidos=".Length).Trim() != "0";
                    else if (s.StartsWith("respuesta_inmediata=")) respuestaInmediata = s.Substring(20).Trim() != "0";
                    else if (s.StartsWith("transiciones_tacto=")) transicionesTacto = s.Substring(19).Trim() != "0";
                    else if (s.StartsWith("cielo_profundo=")) cieloProfundo = s.Substring(15).Trim() != "0";
                    else if (s.StartsWith("cielo_profundo_fuerza=")) nebNaturalFuerza = FuerzaNatural(s.Substring(22));
                    else if (s.StartsWith("aurora_bandas=")) auroraBandas = s.Substring(14).Trim() != "0";
                    // Ctrl+F3 -> Pantalla (PantallaVivaGpu.cs): solo cambian el dibujo por tarjeta grafica.
                    else if (s.StartsWith("portada_enfocada=")) portadaEnfocada = s.Substring("portada_enfocada=".Length).Trim() != "0";
                    else if (s.StartsWith("ficha_disco=")) fichaDisco = s.Substring("ficha_disco=".Length).Trim() != "0";
                    else if (s.StartsWith("pagina_desliza=")) paginaDesliza = s.Substring("pagina_desliza=".Length).Trim() != "0";
                    else if (s.StartsWith("letras_lupa=")) letrasLupa = s.Substring("letras_lupa=".Length).Trim() != "0";
                    // Ctrl+F3 -> Pantalla -> Dentro del disco (DiscoVivoGpu.cs): tambien solo por tarjeta grafica.
                    else if (s.StartsWith("vinilo_disco=")) viniloDisco = s.Substring("vinilo_disco=".Length).Trim() != "0";
                    else if (s.StartsWith("lista_grande=")) listaGrande = s.Substring("lista_grande=".Length).Trim() != "0";
                    else if (s.StartsWith("eq_sonando=")) eqSonando = s.Substring("eq_sonando=".Length).Trim() != "0";
                    else if (s.StartsWith("cola_minutos=")) colaMinutos = s.Substring("cola_minutos=".Length).Trim() != "0";
                    else if (s.StartsWith("vuelo_cola=")) vueloCola = s.Substring("vuelo_cola=".Length).Trim() != "0";
                    // Ctrl+F3 -> Pantalla -> Lo que suena y los creditos (SuenaVivoGpu.cs): tambien solo por tarjeta grafica.
                    else if (s.StartsWith("suena_grande=")) suenaGrande = s.Substring("suena_grande=".Length).Trim() != "0";
                    else if (s.StartsWith("moneda_cae=")) monedaCae = s.Substring("moneda_cae=".Length).Trim() != "0";
                    else if (s.StartsWith("sin_creditos_invita=")) sinCreditosInvita = s.Substring("sin_creditos_invita=".Length).Trim() != "0";
                    else if (s.StartsWith("avisos_claros=")) avisosClaros = s.Substring("avisos_claros=".Length).Trim() != "0";
                    // Ctrl+F3 -> Pantalla -> La atraccion (AtraccionGpu.cs): tambien solo por tarjeta grafica.
                    else if (s.StartsWith("escaparate=")) escaparate = s.Substring("escaparate=".Length).Trim() != "0";
                    else if (s.StartsWith("top_noche=")) topNoche = s.Substring("top_noche=".Length).Trim() != "0";
                    // Ctrl+F3 -> Pantalla -> El estilo (EstiloGpu.cs): tambien solo por tarjeta grafica.
                    else if (s.StartsWith("letra_lejos=")) letraLejos = s.Substring("letra_lejos=".Length).Trim() != "0";
                    else if (s.StartsWith("color_genero=")) colorGenero = s.Substring("color_genero=".Length).Trim() != "0";
                    else if (s.StartsWith("disco="))   // tipo de disco: auto (medir), ssd o hdd (Ctrl+F3)
                    {
                        string d = s.Substring(6).Trim().ToLowerInvariant();
                        perfilDisco = (d == "ssd") ? 1 : (d == "hdd" || d == "duro") ? 2 : 0;
                    }
                    else if (s.StartsWith("video_gpu=")) videoGpuOn = s.Substring(10).Trim() != "0";   // interfaz por GPU aunque haya video
                    else if (s.StartsWith("diagnostico=")) diagOn = s.Substring(12).Trim() != "0";   // bitacora en rocola_diagnostico.txt
                    else if (s.StartsWith("normalizar=")) normalizeOn = s.Substring(11).Trim() != "0";   // nivelar volumen de las canciones (por defecto si)
                    else if (s.StartsWith("respaldo_dir=")) { string d = s.Substring(13).Trim(); if (d.Length > 0) backupDir = d; }   // 2o destino del respaldo de datos aprendidos
                    else if (s.StartsWith("nivel_objetivo=")) { float v; if (float.TryParse(s.Substring(15).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) normTarget = Clampf(v, NormTargetMin, NormTargetMax); }
                    else if (s.StartsWith("nivel_fuerza=")) { float v; if (float.TryParse(s.Substring(13).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) normStrength = Clampf(v, 0f, 1f); }
                    else if (s.StartsWith("realce=")) { float v; if (float.TryParse(s.Substring(7).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) excAmount = Clampf(v, 0f, 1f); }   // realce de agudos (exciter, tipo DSEE)
                    else if (s.StartsWith("bajo=")) { float v; if (float.TryParse(s.Substring(5).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) bassAmount = Clampf(v, 0f, 1f); }   // bajo psicoacustico (armonicos del grave)
                    else if (s.StartsWith("noche=")) nocheOn = s.Substring(6).Trim() != "0";   // modo noche (baja el volumen de madrugada)
                    else if (s.StartsWith("turno=")) turnoOn = s.Substring(6).Trim() != "0";   // el letrero de cuando suena tu cancion
                    else if (s.StartsWith("marca_cola=")) marcaColaOn = s.Substring(11).Trim() != "0";   // el palomito de "ya la pusiste"
                    // Las tres perillas del fondo. Todas pasan por su funcion, que acota: un disparate en el
                    // archivo no puede dejar la pantalla inservible.
                    else if (s.StartsWith("portada_manda=")) portadaManda = PesoPortada(s.Substring(14));
                    else if (s.StartsWith("cielo_azul=")) cieloAzul = AzulCielo(s.Substring(11));
                    else if (s.StartsWith("cielo_negro=")) cieloCorte = CorteCielo(s.Substring(12));
                    else if (s.StartsWith("cielo_aire=")) airePaso = PasoDelAire(s.Substring(11));
                    else if (s.StartsWith("fondo_portada=")) fondoPortadaOn = s.Substring(14).Trim() != "0";   // el fondo toma el tono de la portada
                    else if (s.StartsWith("cola_portadas=")) colaPortadasOn = s.Substring(14).Trim() != "0";   // las portaditas en la capsula de la cola
                    else if (s.StartsWith("relevo=")) relevoOn = s.Substring(7).Trim() != "0";   // la media vuelta del cubito al cambiar de cancion
                    else if (s.StartsWith("respira=")) respiraOn = s.Substring(8).Trim() != "0";   // la pared que respira cuando nadie la usa
                    else if (s.StartsWith("luna=")) lunaOn = s.Substring(5).Trim() != "0";
                    else if (s.StartsWith("respira_fuerza=")) respiraFuerza = FuerzaRespira(s.Substring(15));
                    else if (s.StartsWith("noche_desde=")) { int v; if (int.TryParse(s.Substring(12).Trim(), out v) && v >= 0 && v <= 23) nocheDesde = v; }
                    else if (s.StartsWith("noche_hasta=")) { int v; if (int.TryParse(s.Substring(12).Trim(), out v) && v >= 0 && v <= 23) nocheHasta = v; }
                    else if (s.StartsWith("noche_db=")) { float v; if (float.TryParse(s.Substring(9).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) nocheDb = Clampf(v, -24f, 0f); }   // cuanto baja (negativo)
                    else if (s.StartsWith("noche_compensa=")) nocheCompensa = NocheCompensa(s.Substring(15));   // graves y agudos que devuelve de noche (%)
                    else if (s.StartsWith("grave_mono=")) graveMonoHz = GraveMonoHz(s.Substring(11));   // corte del grave en mono (Hz; 0 = apagado)
                    else if (s.StartsWith("subir_db=")) { float v; if (float.TryParse(s.Substring(9).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) masterDb = Clampf(v, 0f, 12f); }   // SUBIR TODO (dB extra general)
                    else if (s.StartsWith("fundido=")) { float v; if (float.TryParse(s.Substring(8).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) crossfadeSec = Clampf(v, 0f, 3f); }   // FUNDIDO entre canciones (segundos)
                    else if (s.StartsWith("declip=")) declipOn = s.Substring(7).Trim() != "0";   // des-recorte de picos aplastados (por defecto si)
                    else if (s.StartsWith("ia_datos_mb=")) { int v; if (int.TryParse(s.Substring(12).Trim(), out v)) iaMaxBytes = (v <= 0) ? 0L : (long)v * 1024 * 1024; }   // tope del archivo de material
                    else if (s.StartsWith("ia_datos=")) iaHarvestOn = s.Substring(9).Trim() != "0";   // juntar material para entrenar la IA
                    else if (s.StartsWith("video_vol=")) { int v; if (int.TryParse(s.Substring(10).Trim(), out v)) videoVol = v < 0 ? 0 : (v > 100 ? 100 : v); }   // volumen base de los videos
                    else if (s.StartsWith("video_automatico=")) videoAuto = s.Substring(17).Trim() != "0";   // ese volumen base lo elige el programa
                    else if (s.StartsWith("eq_auto=")) { float v; if (float.TryParse(s.Substring(8).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) eqAmount = Clampf(v / 100f, 0f, 1f); }   // EQ automatico (fuerza % a mano)
                    else if (s.StartsWith("eq_automatico=")) eqAuto = s.Substring(14).Trim() != "0";     // la fuerza del EQ la decide el programa
                    else if (s.StartsWith("bajo_automatico=")) bassAuto = s.Substring(16).Trim() != "0"; // y la cantidad de bajo tambien
                    else if (s.StartsWith("dsee=")) sbrOn = s.Substring(5).Trim() == "1";   // reconstruccion espectral (NIVEL 2)
                    else if (s.StartsWith("salida=")) { string v = s.Substring(7).Trim().ToLowerInvariant(); if (v == "auto" || v == "exclusivo" || v == "wasapi" || v == "waveout") outPref = v; }   // salida de audio (rama HD)
                    else if (s.StartsWith("ia_vivo=")) iaVivo = s.Substring(8).Trim() == "1";           // Apollo en vivo (Ctrl+F4)
                    else if (s.StartsWith("ia_mezcla="))
                    {
                        string m = s.Substring(10).Trim().ToLowerInvariant(); int mn;
                        iaMezcla = (m == "auto" || (int.TryParse(m, out mn) && mn >= 0 && mn <= 100)) ? m : "auto";
                    }
                    else if (s.StartsWith("ia_gpu="))
                    {
                        string g = s.Substring(7).Trim().ToLowerInvariant(); int gn;
                        iaGpu = (g == "auto" || (int.TryParse(g, out gn) && gn >= 0)) ? g : "auto";
                    }   // GPU de la IA (saneado)
                    else if (s.StartsWith("ia_cache=")) { string d = s.Substring(9).Trim(); if (d.Length > 0) iaCacheDir = d; }
                    else if (s.StartsWith("dsee_forzar=")) { if (s.Substring(12).Trim() == "1") { sbrForce = true; sbrAllowed = true; sbrReason = ""; } }   // salta la puerta de requisitos (pruebas)
                    else if (s.StartsWith("dsee_ventana=")) { int v; dseeVentana = (int.TryParse(s.Substring(13).Trim(), out v) && (v == 2048 || v == 4096)) ? v : 0; }   // tamano de ventana del DSEE a mano (0 = automatico)
                    else if (s.StartsWith("dir="))
                    {
                        string d = s.Substring(4).Trim();
                        if (d.Length > 0 && !extraMusicDirs.Contains(d)) extraMusicDirs.Add(d);
                    }
                }
            }
            catch { }
            if (!sbrAllowed) sbrOn = false;   // la maquina no cumple los requisitos del NIVEL 2: apagado si o si
        }

        private void SaveConfigPrefs()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("gpu=").Append(gpuPrefOn ? "1" : "0").Append("\r\n");
                sb.Append("animacion=").Append(TextoNivel(animNiv)).Append("\r\n");
                sb.Append("espectro=").Append(Math.Round(auroraAmount * 100f)).Append("\r\n");
                sb.Append("tope_fps=").Append(fpsTope).Append("\r\n");
                sb.Append("estrellas=").Append((int)Math.Round(estrellasDens * 100f)).Append("\r\n");
                sb.Append("fugaces=").Append(TextoNivel(fugacesNiv)).Append("\r\n");
                sb.Append("nieblas=").Append(TextoNivel(nieblasNiv)).Append("\r\n");
                sb.Append("patron=").Append(TextoNivel(relevoNiv)).Append("\r\n");
                sb.Append("hojeo_ligero=").Append(hojeoLigero ? "1" : "0").Append("\r\n");
                sb.Append("progreso_neon=").Append(progresoNeon ? "1" : "0").Append("\r\n");
                sb.Append("muro_reposo=").Append(muroReposo ? "1" : "0").Append("\r\n");
                sb.Append("nebulosas_cine=").Append(nebulosasCine ? "1" : "0").Append("\r\n");
                sb.Append("vineta_suave=").Append(vinetaSuave ? "1" : "0").Append("\r\n");
                sb.Append("halo_seleccion=").Append(haloSeleccion ? "1" : "0").Append("\r\n");
                sb.Append("estrellas_finas=").Append(estrellasFinas ? "1" : "0").Append("\r\n");
                sb.Append("ondas_suaves=").Append(ondasSuaves ? "1" : "0").Append("\r\n");
                sb.Append("progreso_fluido=").Append(progresoFluido ? "1" : "0").Append("\r\n");
                sb.Append("estado_expandido=").Append(estadoExpandido ? "1" : "0").Append("\r\n");
                sb.Append("neon_intenso=").Append(neonIntenso ? "1" : "0").Append("\r\n");
                sb.Append("estado_transparente=").Append(estadoTransparente ? "1" : "0").Append("\r\n");
                sb.Append("canciones_transparentes=").Append(cancionesTransparentes ? "1" : "0").Append("\r\n");
                sb.Append("canciones_cristal=").Append(cancionesCristal ? "1" : "0").Append("\r\n");
                sb.Append("cola_doce=").Append(colaDoce ? "1" : "0").Append("\r\n");
                sb.Append("glass_capas=").Append(glassCapas ? "1" : "0").Append("\r\n");
                sb.Append("colores_cache=").Append(coloresCache ? "1" : "0").Append("\r\n");
                sb.Append("luna_activa=").Append(lunaActiva ? "1" : "0").Append("\r\n");
                sb.Append("paleta_completa=").Append(paletaCompleta ? "1" : "0").Append("\r\n");
                sb.Append("controles_reposo=").Append(controlesReposo ? "1" : "0").Append("\r\n");
                sb.Append("ondas_unidas=").Append(ondasUnidas ? "1" : "0").Append("\r\n");
                sb.Append("luna_realista=").Append(lunaRealista ? "1" : "0").Append("\r\n");
                sb.Append("halo_lunar=").Append(haloLunar ? "1" : "0").Append("\r\n");
                sb.Append("polvo_lunar=").Append(polvoLunar ? "1" : "0").Append("\r\n");
                sb.Append("luna_protagonista=").Append(lunaProtagonista ? "1" : "0").Append("\r\n");
                sb.Append("estela_estelar=").Append(estelaEstelar ? "1" : "0").Append("\r\n");
                sb.Append("estrella_3d=").Append(estrella3d ? "1" : "0").Append("\r\n");
                sb.Append("colores_fluidos=").Append(coloresFluidos ? "1" : "0").Append("\r\n");
                sb.Append("respuesta_inmediata=").Append(respuestaInmediata ? "1" : "0").Append("\r\n");
                sb.Append("transiciones_tacto=").Append(transicionesTacto ? "1" : "0").Append("\r\n");
                sb.Append("cielo_profundo=").Append(cieloProfundo ? "1" : "0").Append("\r\n");
                sb.Append("cielo_profundo_fuerza=").Append((int)Math.Round(nebNaturalFuerza * 100f)).Append("\r\n");
                sb.Append("aurora_bandas=").Append(auroraBandas ? "1" : "0").Append("\r\n");
                sb.Append("portada_enfocada=").Append(portadaEnfocada ? "1" : "0").Append("\r\n");
                sb.Append("ficha_disco=").Append(fichaDisco ? "1" : "0").Append("\r\n");
                sb.Append("pagina_desliza=").Append(paginaDesliza ? "1" : "0").Append("\r\n");
                sb.Append("letras_lupa=").Append(letrasLupa ? "1" : "0").Append("\r\n");
                sb.Append("vinilo_disco=").Append(viniloDisco ? "1" : "0").Append("\r\n");
                sb.Append("lista_grande=").Append(listaGrande ? "1" : "0").Append("\r\n");
                sb.Append("eq_sonando=").Append(eqSonando ? "1" : "0").Append("\r\n");
                sb.Append("cola_minutos=").Append(colaMinutos ? "1" : "0").Append("\r\n");
                sb.Append("vuelo_cola=").Append(vueloCola ? "1" : "0").Append("\r\n");
                sb.Append("suena_grande=").Append(suenaGrande ? "1" : "0").Append("\r\n");
                sb.Append("moneda_cae=").Append(monedaCae ? "1" : "0").Append("\r\n");
                sb.Append("sin_creditos_invita=").Append(sinCreditosInvita ? "1" : "0").Append("\r\n");
                sb.Append("avisos_claros=").Append(avisosClaros ? "1" : "0").Append("\r\n");
                sb.Append("escaparate=").Append(escaparate ? "1" : "0").Append("\r\n");
                sb.Append("top_noche=").Append(topNoche ? "1" : "0").Append("\r\n");
                sb.Append("letra_lejos=").Append(letraLejos ? "1" : "0").Append("\r\n");
                sb.Append("color_genero=").Append(colorGenero ? "1" : "0").Append("\r\n");
                sb.Append("disco=").Append(perfilDisco == 1 ? "ssd" : perfilDisco == 2 ? "hdd" : "auto").Append("\r\n");
                sb.Append("video_gpu=").Append(videoGpuOn ? "1" : "0").Append("\r\n");
                sb.Append("diagnostico=").Append(diagOn ? "1" : "0").Append("\r\n");
                sb.Append("normalizar=").Append(normalizeOn ? "1" : "0").Append("\r\n");
                if (!string.IsNullOrEmpty(backupDir)) sb.Append("respaldo_dir=").Append(backupDir).Append("\r\n");
                sb.Append("nivel_objetivo=").Append(normTarget.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("nivel_fuerza=").Append(normStrength.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("realce=").Append(excAmount.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("bajo=").Append(bassAmount.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("subir_db=").Append(masterDb.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("noche=").Append(nocheOn ? "1" : "0").Append("\r\n");
                sb.Append("turno=").Append(turnoOn ? "1" : "0").Append("\r\n");
                sb.Append("marca_cola=").Append(marcaColaOn ? "1" : "0").Append("\r\n");
                sb.Append("portada_manda=").Append(Math.Round(portadaManda * 100f)).Append("\r\n");
                sb.Append("cielo_azul=").Append(Math.Round(cieloAzul * 100f)).Append("\r\n");
                sb.Append("cielo_negro=").Append(Math.Round(cieloCorte * 100f)).Append("\r\n");
                sb.Append("cielo_aire=").Append(airePaso).Append("\r\n");
                sb.Append("fondo_portada=").Append(fondoPortadaOn ? "1" : "0").Append("\r\n");
                sb.Append("cola_portadas=").Append(colaPortadasOn ? "1" : "0").Append("\r\n");
                sb.Append("relevo=").Append(relevoOn ? "1" : "0").Append("\r\n");
                sb.Append("respira=").Append(respiraOn ? "1" : "0").Append("\r\n");
                sb.Append("luna=").Append(lunaOn ? "1" : "0").Append("\r\n");
                sb.Append("respira_fuerza=").Append((respiraFuerza * 100f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("noche_desde=").Append(nocheDesde).Append("\r\n");
                sb.Append("noche_hasta=").Append(nocheHasta).Append("\r\n");
                sb.Append("noche_db=").Append(nocheDb.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("noche_compensa=").Append((nocheCompensa * 100f).ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("grave_mono=").Append(graveMonoHz.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("fundido=").Append(crossfadeSec.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("declip=").Append(declipOn ? "1" : "0").Append("\r\n");
                sb.Append("ia_datos=").Append(iaHarvestOn ? "1" : "0").Append("\r\n");
                sb.Append("ia_datos_mb=").Append(iaMaxBytes <= 0 ? 0L : iaMaxBytes / (1024 * 1024)).Append("\r\n");
                sb.Append("video_vol=").Append(videoVol).Append("\r\n");
                sb.Append("video_automatico=").Append(videoAuto ? "1" : "0").Append("\r\n");
                sb.Append("eq_auto=").Append(((int)Math.Round(eqAmount * 100f)).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("eq_automatico=").Append(eqAuto ? "1" : "0").Append("\r\n");
                sb.Append("bajo_automatico=").Append(bassAuto ? "1" : "0").Append("\r\n");
                sb.Append("dsee=").Append(sbrOn ? "1" : "0").Append("\r\n");
                sb.Append("salida=").Append(outPref).Append("\r\n");
                sb.Append("ia_vivo=").Append(iaVivo ? "1" : "0").Append("\r\n");
                sb.Append("ia_mezcla=").Append(iaMezcla).Append("\r\n");
                sb.Append("ia_gpu=").Append(iaGpu).Append("\r\n");
                if (!string.IsNullOrEmpty(iaCacheDir)) sb.Append("ia_cache=").Append(iaCacheDir).Append("\r\n");
                if (sbrForce) sb.Append("dsee_forzar=1\r\n");
                if (dseeVentana > 0) sb.Append("dsee_ventana=").Append(dseeVentana).Append("\r\n");   // y el tamano de ventana puesto a mano, por lo mismo   // conservar la bandera manual de pruebas (este saver reescribe el archivo completo)
                for (int i = 0; i < extraMusicDirs.Count; i++) sb.Append("dir=").Append(extraMusicDirs[i]).Append("\r\n");
                EscribeSeguro(ConfigFile, sb.ToString());
            }
            catch { }
        }

        // ===================== NIVELADO DE VOLUMEN (rocola_volumen.txt) =====================
        // Una linea por cancion: "<rms>|<ruta>". El RMS integrado se mide al oirse la cancion y se guarda para que
        // la proxima vez suene al mismo nivel que las demas. Archivo chico (una linea por cancion oida).
        // ================== GUARDAR SIN PERDER NADA SI SE VA LA LUZ ==================
        //
        // Todo lo que la rocola guarda -los creditos y la cola, el volumen medido de cada cancion, lo mas
        // sonado, los ajustes, las teclas, la biblioteca- se escribia con "abre el archivo, VACIALO entero y
        // vuelve a llenarlo". Entre el vaciado y el llenado el archivo esta vacio; y ademas Windows no baja
        // lo escrito al disco al momento, se lo guarda en memoria y lo escribe cuando le viene bien, que
        // pueden ser segundos. Si en ese rato se va la luz -y en un bar se va-, el archivo se queda vacio o
        // a medias, y lo que habia dentro ya no vuelve.
        //
        // Lo mas feo es que el archivo de CREDITOS Y COLA existe justamente para sobrevivir a un corte de
        // luz. Y al arrancar con ese archivo roto la rocola no se queja: se queda tan tranquila con cero
        // creditos y sin cola. O sea que el dinero del cliente desaparece sin que nadie sepa por que.
        // Detras van tambien horas de trabajo: el volumen medido de toda la biblioteca se mide una vez y
        // tarda horas; perder ese archivo es volver a empezar.
        //
        // Ahora se escribe en un archivo APARTE, se obliga a Windows a bajarlo al disco de verdad, y solo
        // entonces se pone en el sitio del bueno de un tiron. Pase lo que pase, en el disco queda siempre un
        // archivo COMPLETO: el nuevo si dio tiempo, y el de antes si no. Y el de antes se conserva como
        // copia (.bak) por si el bueno se estropeara por cualquier otro motivo.
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushFileBuffers(IntPtr hFile);

        // UN CANDADO POR ARCHIVO. Dos hilos guardando EL MISMO archivo a la vez se pisarian el archivo de
        // paso y dejarian el bueno hecho un lio. Pasaba de verdad con las estadisticas, que tienen dos
        // escritores: uno de fondo cada tanto y otro al cerrar la rocola. El candado va AQUI DENTRO y no en
        // cada sitio que guarda, para que no dependa de que alguien se acuerde de ponerlo. Es POR ARCHIVO,
        // asi que guardar el volumen de la biblioteca -que es grande- no hace esperar a los creditos.
        private static readonly Dictionary<string, object> candados = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        // Como maximo un escritor activo y una instantanea pendiente por tabla.
        // El trabajador sigue registrado en el drenaje de cierre mientras queden solicitudes.
        private sealed class GuardadoReciente { public Action Pending; public bool Running; }
        private void GuardaUltimo(GuardadoReciente slot, Action work)
        {
            lock (slot)
            {
                slot.Pending = work;
                if (slot.Running) return;
                slot.Running = true;
            }
            GuardaEnFondo(delegate
            {
                while (true)
                {
                    Action next;
                    lock (slot)
                    {
                        next = slot.Pending; slot.Pending = null;
                        if (next == null) { slot.Running = false; return; }
                    }
                    try { next(); } catch { }
                }
            });
        }

        private static object CandadoDe(string ruta)
        {
            lock (candados)
            {
                object c;
                if (!candados.TryGetValue(ruta, out c)) { c = new object(); candados[ruta] = c; }
                return c;
            }
        }

        private static void EscribeSeguro(string ruta, string texto)
        {
            lock (CandadoDe(ruta)) EscribeSeguroYa(ruta, texto);
        }

        // Todos los volcados grandes pasan por aqui. Ademas de permitir esperar al cierre, este contador
        // evita que Windows termine el proceso mientras aun queda una instantanea en el ThreadPool.
        private int guardadosPendientes;
        private readonly object guardadosLock = new object();
        private readonly System.Threading.ManualResetEvent guardadosEnReposo = new System.Threading.ManualResetEvent(true);
        private void GuardaEnFondo(System.Action trabajo)
        {
            if (trabajo == null) return;
            lock (guardadosLock) { guardadosPendientes++; guardadosEnReposo.Reset(); }
            try
            {
                bool encolado = System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    try { trabajo(); }
                    finally
                    {
                        lock (guardadosLock)
                        {
                            guardadosPendientes--;
                            if (guardadosPendientes <= 0) { guardadosPendientes = 0; guardadosEnReposo.Set(); }
                        }
                    }
                });
                if (!encolado) throw new InvalidOperationException("ThreadPool no acepto el guardado");
            }
            catch
            {
                lock (guardadosLock)
                {
                    guardadosPendientes--;
                    if (guardadosPendientes <= 0) { guardadosPendientes = 0; guardadosEnReposo.Set(); }
                }
                // Durante el cierre el ThreadPool puede rechazar un trabajo nuevo. En ese caso se guarda
                // sincronicamente: es preferible esperar un instante a perder la ultima medicion.
                try { trabajo(); } catch { }
            }
        }
        private void EsperaGuardados()
        {
            try { guardadosEnReposo.WaitOne(15000); }
            catch { }
        }

        private static void AnexaSeguro(string ruta, string texto)
        {
            if (string.IsNullOrEmpty(texto)) return;
            byte[] datos = new UTF8Encoding(false).GetBytes(texto);
            lock (CandadoDe(ruta))
            using (FileStream fs = new FileStream(ruta, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            {
                // Descarta una ultima linea partida por un apagon antes de pegarle registros nuevos.
                if (fs.Length > 0)
                {
                    long pos = fs.Length - 1; fs.Position = pos;
                    if (fs.ReadByte() != (byte)'\n')
                    {
                        while (pos >= 0)
                        {
                            fs.Position = pos;
                            if (fs.ReadByte() == (byte)'\n') break;
                            pos--;
                        }
                        fs.SetLength(pos + 1);
                    }
                }
                fs.Position = fs.Length;
                fs.Write(datos, 0, datos.Length);
                fs.Flush();
                try { FlushFileBuffers(fs.SafeFileHandle.DangerousGetHandle()); } catch { }
            }
        }

        private static void EscribeSeguroYa(string ruta, string texto)
        {
            string tmp = ruta + ".tmp", bak = ruta + ".bak";
            // MISMOS BYTES que antes: UTF-8 sin marca al principio, que es lo que escribia File.WriteAllText.
            // Si cambiara, todo lo guardado hasta hoy dejaria de leerse igual.
            byte[] datos = new UTF8Encoding(false).GetBytes(texto);
            using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(datos, 0, datos.Length);
                fs.Flush();
                // Flush solo deja lo escrito en la memoria de Windows. Esto lo baja al disco de verdad, que
                // es lo unico que sobrevive a un corte de luz. Si el sistema no tiene esta llamada (o no es
                // Windows), se sigue adelante: se pierde la garantia dura, no el resto.
                try { FlushFileBuffers(fs.SafeFileHandle.DangerousGetHandle()); } catch { }
            }
            PublicaGuardadoSeguro(ruta, tmp, bak);
        }

        private static void PublicaGuardadoSeguro(string ruta, string tmp, string bak)
        {
            if (File.Exists(ruta))
            {
                // Replace cambia el archivo de un tiron y deja el anterior como copia. En memorias USB
                // (FAT32) no existe, asi que ahi se hace a mano.
                try { File.Replace(tmp, ruta, bak, true); return; }
                catch { }
                try { if (File.Exists(bak)) File.Delete(bak); } catch { }
                // Si no se pudo conservar el original, ABORTAR: nunca borrarlo para forzar el reemplazo.
                File.Move(ruta, bak);
            }
            File.Move(tmp, ruta);
        }

        // Escritura acotada por registros; no publica si falla o queda obsoleta.
        private static bool EscribeFlujoSeguro(string ruta, Func<TextWriter, bool> escribe, Func<bool> vigente)
        {
            lock (CandadoDe(ruta))
            {
                if (vigente != null && !vigente()) return false;
                string tmp = ruta + ".tmp";
                bool creado = false, listo = false;
                try
                {
                    using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        creado = true;
                        using (StreamWriter writer = new StreamWriter(fs, new UTF8Encoding(false), 4096))
                        {
                            if (!escribe(writer) || (vigente != null && !vigente())) return false;
                            writer.Flush(); fs.Flush();
                            try { FlushFileBuffers(fs.SafeFileHandle.DangerousGetHandle()); } catch { }
                        }
                    }
                    if (vigente != null && !vigente()) return false;
                    // Fallo al reemplazar: conservar el temporal COMPLETO para recuperacion, como antes.
                    listo = true;
                    PublicaGuardadoSeguro(ruta, tmp, ruta + ".bak");
                    return true;
                }
                finally { if (creado && !listo) { try { File.Delete(tmp); } catch { } } }
            }
        }

        // Guarda en segundo plano, con la cuenta de pendientes (el cierre espera a que acabe). Lo usa lo mas pedido esta
        // noche (AtraccionGpu.cs).
        private void GuardaDetras(System.Action trabajo) { GuardaEnFondo(trabajo); }

        // Lee un archivo guardado con EscribeSeguro. Si el bueno falta o quedo vacio -un corte de luz de los
        // de antes, o un disco con problemas- se lee la copia. Devuelve null si no hay nada que leer.
        private static string[] LeeLineasSeguro(string ruta)
        {
            try { if (File.Exists(ruta)) { string[] L = File.ReadAllLines(ruta); if (L.Length > 0) return L; } } catch { }
            try { if (File.Exists(ruta + ".bak")) { string[] L = File.ReadAllLines(ruta + ".bak"); if (L.Length > 0) return L; } } catch { }
            return null;
        }

        // BORRAR UN GUARDADO ES BORRARLO ENTERO. Como cada guardado deja una copia al lado, borrar solo el
        // archivo bueno no borra nada: la lectura de arriba encuentra la copia y lo resucita. Paso de verdad
        // con el archivo de estado, donde "que no exista" es la senal de que la rocola se cerro a proposito:
        // se borraba el bueno, sobrevivia la copia, y al dia siguiente reaparecian los creditos y la cola.
        //
        // El ORDEN importa. Primero la copia y el de paso, y el bueno al final: si se fuera la luz a mitad de
        // esto, lo que queda es el archivo bueno -o sea, se reanuda, que es lo que hace la rocola ante un
        // apagon de toda la vida-, nunca una copia suelta capaz de resucitar sola.
        private static void BorraSeguro(string ruta)
        {
            lock (CandadoDe(ruta))
            {
                try { File.Delete(ruta + ".bak"); } catch { }
                try { File.Delete(ruta + ".tmp"); } catch { }
                try { File.Delete(ruta); } catch { }
            }
        }

        // Tabla de huellas tonales (d0;d1;...;d7|ruta). Perdida o rota no rompe nada: se re-aprende oyendo.
        private void LoadEqCache()
        {
            try { if (!ReadEqCache(EqFile(), eqCache, ref eqCacheVer)) return; } catch { }
            EnsureEqAverage();
        }

        private static bool ReadEqCache(string f, Dictionary<string, float[]> eqCache, ref int eqCacheVer)
        {
            try
            {
                if (!File.Exists(f)) return false;
                using (StreamReader reader = new StreamReader(f))
                    ReadEqCacheRecords(reader, eqCache, ref eqCacheVer);
            }
            catch { }
            return true;
        }

        // Preparar por registros sin conservar todas las lineas. Un fallo de lectura
        // antes de EOF descarta pending: ninguna tabla de destino recibe una carga parcial.
        private static void ReadEqCacheRecords(TextReader reader, Dictionary<string, float[]> eqCache, ref int eqCacheVer)
        {
            Dictionary<string, float[]> pending = new Dictionary<string, float[]>(eqCache.Comparer);
            int updates = FillEqCacheRecords(reader, pending);
            foreach (KeyValuePair<string, float[]> item in pending) eqCache[item.Key] = item.Value;
            eqCacheVer += updates;
        }

        // Solo para una tabla privada que todavia no se ha publicado.
        private static int FillEqCacheRecords(TextReader reader, Dictionary<string, float[]> pending)
        {
            int updates = 0;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                int bar = line.IndexOf('|');
                if (bar <= 0 || bar >= line.Length - 1) continue;
                float[] c = ReadEqValues(line, bar);
                if (c != null) { pending[Pool(line.Substring(bar + 1))] = c; updates++; }
            }
            return updates;
        }

        private static float[] ReadEqValues(string line, int bar)
        {
            int parts = 1;
            for (int i = 0; i < bar; i++) if (line[i] == ';') parts++;
            if (parts < 8 || parts > EqLen) return null;   // misma admision de formatos antiguos
            float[] values = new float[EqLen];   // los campos ausentes conservan +0
            int start = 0;
            for (int b = 0; b < parts; b++)
            {
                int end = line.IndexOf(';', start, bar - start);
                if (end < 0) end = bar;
                if (!float.TryParse(line.Substring(start, end - start), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out values[b])) return null;
                start = end + 1;
            }
            return values;   // sin copiar toda la cabecera ni crear un array de subcadenas
        }
        private void SaveEqCache()
        {
            if (startupReadPending) return;
            List<KeyValuePair<string, float[]>> snap;
            try { snap = new List<KeyValuePair<string, float[]>>(eqCache); }   // copia rapida en el hilo de UI
            catch { return; }
            long version = System.Threading.Interlocked.Increment(ref eqSaveVersion);
            GuardaUltimo(eqSaveQueue, delegate
            {
                try
                {
                    Func<bool> vigente = delegate { return version == System.Threading.Interlocked.Read(ref eqSaveVersion); };
                    if (!vigente()) return;
                    lock (eqSaveLock)
                    {
                        EscribeFlujoSeguro(EqFile(), delegate(TextWriter writer)
                        {
                            for (int i = 0; i < snap.Count; i++)
                            {
                                if ((i & 127) == 0 && !vigente()) return false;
                                float[] c = snap[i].Value;
                                for (int b = 0; b < EqLen; b++)
                                { if (b > 0) writer.Write(';'); writer.Write(c[b].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)); }
                                writer.Write('|'); writer.Write(snap[i].Key); writer.Write("\r\n");
                            }
                            return true;
                        }, vigente);
                    }
                }
                catch { }
            });
        }

        // El OBJETIVO del EQ automatico. Antes era el promedio de TODA la biblioteca, y ahi estaba el problema:
        // si la mitad de tus canciones son MP3 flojos sin agudos, ese promedio viene apagado y el EQ estaba
        // empujando a TODAS hacia esa mediocridad, incluso a las que llegaron completas. Ahora el objetivo se
        // saca SOLO de las canciones sanas -las que conservan sus agudos, que es justo lo que el DSEE ya sabe
        // detectar-, asi que el objetivo pasa de ser "el promedio de lo que tengo" a "como suena lo que esta
        // bien grabado". Si no hay suficientes sanas (biblioteca entera de baja calidad) se usan todas, como antes.
        private const float EqSanaHz = 19300f;   // mismo umbral con el que el DSEE da una cancion por completa
        private void RememberEqAverage()
        {
            eqAverageSource = eqCache; eqAverageVersion = eqCacheVer;
            eqAverageCount = eqCache.Count; eqAverageReady = true;
        }
        private void EnsureEqAverage()
        {
            if (eqAverageReady && object.ReferenceEquals(eqAverageSource, eqCache)
                && eqAverageVersion == eqCacheVer && eqAverageCount == eqCache.Count) return;
            RecomputeEqAvg();
        }
        private void RecomputeEqAvg()
        {
            ComputeEqAverage(eqCache, ref eqAvg, ref eqAvgSanas, ref eqAvgSoloSanas);
            RememberEqAverage();   // solo despues de completar el calculo, nunca si lanza una excepcion
        }

        private static void ComputeEqAverage(Dictionary<string, float[]> eqCache, ref float[] eqAvg, ref int eqAvgSanas, ref bool eqAvgSoloSanas)
        {
            if (eqCache.Count < 10) { eqAvg = null; eqAvgSanas = 0; return; }
            float[] avg = new float[8]; int n = 0;
            foreach (KeyValuePair<string, float[]> kv in eqCache)
                if (kv.Value[EqCut] >= EqSanaHz) { for (int b = 0; b < 8; b++) avg[b] += kv.Value[b]; n++; }
            eqAvgSoloSanas = (n >= 10);
            if (!eqAvgSoloSanas)   // aun no hay bastantes sanas medidas: se usa todo, como se hacia antes
            {
                for (int b = 0; b < 8; b++) avg[b] = 0f;
                n = 0;
                foreach (KeyValuePair<string, float[]> kv in eqCache) { for (int b = 0; b < 8; b++) avg[b] += kv.Value[b]; n++; }
            }
            if (n < 10) { eqAvg = null; eqAvgSanas = 0; return; }
            for (int b = 0; b < 8; b++) avg[b] /= n;
            eqAvg = avg; eqAvgSanas = n;
        }

        // FUERZA del EQ cuando se deja en AUTOMATICO (de fabrica). No hay perilla que ajustar: la fuerza sale
        // de cuanto se puede uno FIAR del objetivo, que es lo unico que de verdad deberia decidirla.
        //   - Con pocas canciones medidas, el "sonido de la casa" todavia no significa nada: se corrige poco.
        //   - Segun el pre-analisis va midiendo la biblioteca, el objetivo es cada vez mas fiable y se corrige
        //     mas, hasta un tope que nunca llega al 100% (dejar un resto sin corregir conserva el caracter de
        //     cada disco: la idea es emparejar, no aplanar).
        //   - Con el modelo de IA entrenado el objetivo es POR CANCION en vez de uno para todas, asi que se
        //     puede afinar un poco mas sin miedo.
        // Menos de 10 canciones sanas medidas: cero. Ahi el EQ no toca nada, como si estuviera apagado.
        private float EqFuerzaAuto()
        {
            if (eqAvg == null || eqAvgSanas < 10) return 0f;
            float t = (eqAvgSanas - 10) / 50f;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            float f = 0.35f + 0.50f * t;      // 35% con 10 canciones medidas -> 85% con 60 o mas
            if (!EqNet.Loaded) f *= 0.88f;    // sin modelo el objetivo es el promedio de TODAS: mas prudente
            return f;
        }
        // La huella tonal se mide siempre que haga falta para algo: en automatico hace falta SIEMPRE (de ella
        // salen tambien el bajo, el realce y el informe de calidad), y a mano solo si el EQ esta subido.
        private bool EqMideHuella { get { return eqAuto || eqAmount > 0.001f; } }

        // Correccion en dB por banda para una cancion (o null si apagado / aun sin huella / sin objetivo).
        // Correccion = (objetivo - huella) con tope +-4 dB, escalada por la fuerza. Una banda casi MUDA
        // (>25 dB bajo la referencia de su cancion) no se sube: seria inflar piso de ruido, no musica.
        private float[] EqCorrFor(string path)
        {
            float fuerza = eqAuto ? EqFuerzaAuto() : eqAmount;
            if (fuerza <= 0.001f || path == null || eqAvg == null) return null;
            float[] c;
            if (!eqCache.TryGetValue(path, out c)) return null;
            // Banda por ENCIMA del acantilado de esta cancion: NUNCA se corrige, y por dos razones distintas
            // segun el caso. Con el DSEE apagado, ahi el codec no dejo musica: subirla seria subir ruido. Y con
            // el DSEE encendido tampoco, porque la huella guardada describe el ARCHIVO (donde esa banda esta
            // vacia) mientras que lo que suena YA lleva los agudos re-creados: corregir con un numero viejo
            // seria empujar dos veces lo mismo.
            float corte = c[EqCut];
            // OBJETIVO POR CANCION. Para el cuerpo (158 Hz - 5.6 kHz) el objetivo sigue siendo el promedio de las
            // canciones sanas: es el "sonido de la casa". Pero para el grave y los agudos, si hay modelo entrenado,
            // el objetivo lo dice la IA mirando el cuerpo de ESTA cancion: cuanto grave y cuanto aire tendria una
            // cancion bien grabada que sonara como esta. Asi una balada de piano y un corrido dejan de ser
            // empujados hacia la misma curva. Sin modelo, todo sigue como antes (el promedio para las 8 bandas).
            float[] meta = eqAvg;
            if (EqNet.Loaded)
            {
                float[] ent = eqNetIn ?? (eqNetIn = new float[EqNetIn]);
                float[] sal = eqNetOut ?? (eqNetOut = new float[EqNetOut]);
                for (int i = 0; i < EqNetIn; i++) ent[i] = c[i + 1];   // el cuerpo: bandas 1..5
                if (EqNet.Predict(ent, sal))
                {
                    float[] m = eqMeta ?? (eqMeta = new float[8]);
                    for (int b = 0; b < 8; b++) m[b] = eqAvg[b];
                    for (int i = 0; i < EqNetOut; i++)
                    {
                        float v = sal[i];
                        // Cinturon de seguridad: por muy convencida que este la red, el objetivo no se aleja mas
                        // de 6 dB del promedio de la biblioteca. Un modelo mal entrenado no puede desmadrar el EQ.
                        float lim = eqAvg[EqNetSalida[i]];
                        if (v > lim + 6f) v = lim + 6f; else if (v < lim - 6f) v = lim - 6f;
                        m[EqNetSalida[i]] = v;
                    }
                    meta = m;
                }
            }
            // CANCION RARA. Si se aleja muchisimo del objetivo, casi seguro es que QUERIA sonar asi: un disco
            // muy oscuro a proposito, una grabacion en vivo, una voz sola. Empujarla a la fuerza hacia el resto
            // la estropea, asi que cuanto mas lejos esta de la biblioteca, con mas cuidado se le toca. Solo se
            // hace en automatico: si alguien puso la perilla a mano, manda la perilla.
            if (eqAuto)
            {
                float dif = 0f; int nb = 0;
                for (int b = 0; b < 8; b++)
                {
                    if (corte > 100f && EqCentros[b] > corte) continue;
                    if (c[b] <= -25f) continue;
                    float d = meta[b] - c[b]; if (d < 0f) d = -d;
                    dif += d; nb++;
                }
                if (nb > 0) fuerza *= Clampf(1.15f - (dif / nb) / 14f, 0.45f, 1f);
            }
            float[] dbs = new float[8]; bool any = false;
            for (int b = 0; b < 8; b++)
            {
                float corr = 0f;
                if (corte > 100f && EqCentros[b] > corte) { dbs[b] = 0f; continue; }
                if (c[b] > -25f)
                {
                    corr = meta[b] - c[b];
                    if (corr > 4f) corr = 4f; else if (corr < -4f) corr = -4f;
                    corr *= fuerza;
                }
                dbs[b] = corr;
                if (corr > 0.1f || corr < -0.1f) any = true;
            }
            return any ? dbs : null;
        }

        // CUANTO realce de brillo le toca a ESTA cancion. El EQ no puede devolver una banda que el codec borro
        // -ahi no hay musica que subir-, pero el realce SI: fabrica armonicos a partir de lo que aun queda, asi
        // que cae justo donde el EQ no llega. Por eso una cancion dañada recibe mas realce y una completa menos:
        // esa ya trae su aire de verdad y no hace falta inventarselo.
        // Lo que recibe una cancion que ya trae todo su aire (el suelo de la escala de aqui abajo).
        private const float ExciteCompleta = 0.75f;
        private float ExciteFactorFor(string path) { return ExciteFactorPara(path, DseeActivo()); }
        // Lo mismo para una cancion que aun no suena, con SU reconstructor (relevo sin pausa, Form1.SinPausa.cs).
        private float ExciteFactorPara(string path, bool dsee)
        {
            float[] c;
            if (path == null || !eqCache.TryGetValue(path, out c) || c == null || c.Length < EqLen) return 1f;
            float corte = c[EqCut];
            if (corte <= 100f) return 1f;   // aun sin medir: como siempre
            float f = Clampf(1.45f - (corte - 15000f) / 6000f, ExciteCompleta, 1.45f);
            // ...PERO SI EL RECONSTRUCTOR ESPECTRAL ESTA ENCENDIDO, esos agudos ya se los devolvio EL.
            //
            // Estas dos cosas estaban decidiendo por separado y las dos miraban el MISMO dato: el realce le
            // daba mas a la cancion dañada justamente por estar dañada, y el reconstructor le re-creaba la
            // banda justamente por lo mismo. En la cadena el realce va DESPUES, asi que acababa echandole
            // brillo encima a unos agudos que ya no faltaban: empujar dos veces lo mismo, y el exceso de
            // realce suena aspero (por eso su mezcla esta topada). El EQ automatico ya evita este choque
            // -esta explicado ahi mismo, unas lineas mas arriba-; el realce era el unico que no se enteraba.
            //
            // Se le quita la MITAD del extra, no todo: el reconstructor devuelve la ENVOLVENTE de los agudos
            // transponiendo lo que sobrevivio una octava mas abajo, que no es el detalle original, asi que
            // algo de realce encima sigue teniendo sentido. Y con el reconstructor apagado no cambia nada.
            if (dsee && corte < EqSanaHz)
                f = ExciteCompleta + (f - ExciteCompleta) * 0.5f;
            return f;
        }
        private float ExciteAmountFor(string path) { return Clampf(excAmount * ExciteFactorFor(path), 0f, 1f); }
        private float ExciteAmountPara(string path, bool dsee) { return Clampf(excAmount * ExciteFactorPara(path, dsee), 0f, 1f); }

        // ¿Vale la pena poner el des-recorte en esta cancion? Solo si de verdad viene con picos aplastados. Una
        // cancion aun sin medir se trata como sospechosa (mas vale ponerlo). El umbral es bajo a proposito: con
        // una de cada diez mil muestras recortadas ya se oye la aspereza en un pasaje fuerte.
        private bool DeclipVale(string path)
        {
            float[] c;
            if (path == null || !eqCache.TryGetValue(path, out c) || c == null || c.Length < EqLen) return true;
            if (c[EqCut] <= 100f) return true;   // ficha vieja o sin analizar: no se sabe, se pone
            return c[EqClip] > 0.0001f;
        }

        // ---- CALIDAD DE LA BIBLIOTECA (lo que el analisis ya sabe y antes no se veia) ----
        // Reparte las canciones medidas en tres cajones segun donde el codec les corto los agudos, y cuenta
        // aparte las que vienen recortadas de masterizacion. Sirve para saber que vale la pena volver a
        // conseguir en buena calidad.
        private const float CalBuenaHz = 19300f, CalMediaHz = 16500f;
        // Se pide DOS VECES POR SEGUNDO mientras la ventana de ajustes esta abierta, y recorrer la biblioteca
        // entera (decenas de miles de canciones) para volver a contar lo mismo es puro desperdicio: justo en ese
        // rato la maquina suele estar ademas con el entrenamiento a fondo. Se guarda el recuento y solo se
        // rehace cuando la huella de alguna cancion ha cambiado de verdad (ver eqCacheVer).
        // Ademas de la version se guarda CUANTAS habia: asi el recuento tampoco se queda viejo si alguien
        // cambiara la coleccion entera de golpe en vez de ir anadiendo huellas una a una.
        private int calVer = -1, calCuantas = -1, calBuenas, calMedias, calPobres, calRecort, calSinMedir;
        private void CuentaCalidad(out int buenas, out int medias, out int pobres, out int recortadas, out int sinMedir)
        {
            buenas = medias = pobres = recortadas = sinMedir = 0;
            foreach (KeyValuePair<string, float[]> kv in eqCache)
            {
                float[] c = kv.Value;
                if (c == null || c.Length < EqLen || c[EqCut] <= 100f) { sinMedir++; continue; }
                if (c[EqCut] >= CalBuenaHz) buenas++;
                else if (c[EqCut] >= CalMediaHz) medias++;
                else pobres++;
                if (c[EqClip] > 0.001f) recortadas++;
            }
        }
        // Abrir y cerrar la ventana varias veces seguidas encargaba varios informes a la vez y podian
        // chocar escribiendo el MISMO archivo (y el informe se quedaba sin escribir). Uno por vez.
        private static readonly object calSaveLock = new object();
        // El informe se escribe al abrir Ctrl+F3. Con una biblioteca grande, ordenar decenas de miles de
        // canciones y armar un archivo de varios megas es medio segundo de trabajo: hacerlo en el hilo de la
        // interfaz CONGELA la pantalla justo al abrir la ventana. Aqui solo se saca la lista (una pasada barata)
        // y el resto -ordenar, armar el texto y escribir el archivo- se hace en otro hilo.
        private void EscribeInformeCalidad()
        {
            try
            {
                List<KeyValuePair<string, float[]>> lista = new List<KeyValuePair<string, float[]>>();
                foreach (KeyValuePair<string, float[]> kv in eqCache)
                    if (kv.Value != null && kv.Value.Length >= EqLen && kv.Value[EqCut] > 100f) lista.Add(kv);
                if (lista.Count == 0) return;
                int buenas, medias, pobres, recortadas, sinMedir;
                CalidadResumen(out buenas, out medias, out pobres, out recortadas, out sinMedir);
                string destino = CalidadFile();
                System.Threading.ThreadPool.QueueUserWorkItem(delegate { InformeCalidadFondo(lista, destino, buenas, medias, pobres, recortadas, sinMedir); });
            }
            catch { }
        }
        private static void InformeCalidadFondo(List<KeyValuePair<string, float[]>> lista, string destino,
                                                int buenas, int medias, int pobres, int recortadas, int sinMedir)
        {
            try
            {
                lista.Sort(delegate (KeyValuePair<string, float[]> x, KeyValuePair<string, float[]> y)
                { return x.Value[EqCut].CompareTo(y.Value[EqCut]); });
                StringBuilder sb = new StringBuilder(lista.Count * 80 + 512);
                sb.Append("CALIDAD DE LA BIBLIOTECA  -  ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("\r\n");
                sb.Append("Las peores van primero: son las que mas conviene volver a conseguir en buena calidad.\r\n");
                sb.Append("\r\n  COMPLETAS (WAV o 320k): ").Append(buenas.ToString("#,0"));
                sb.Append("\r\n  ACEPTABLES (~192-256k): ").Append(medias.ToString("#,0"));
                sb.Append("\r\n  POBRES (128k o menos):  ").Append(pobres.ToString("#,0"));
                sb.Append("\r\n  Con picos recortados de origen: ").Append(recortadas.ToString("#,0"));
                sb.Append("\r\n  Aun sin medir: ").Append(sinMedir.ToString("#,0"));
                sb.Append("\r\n\r\ncorte   recorte  archivo\r\n");
                for (int i = 0; i < lista.Count; i++)
                {
                    float[] c = lista[i].Value;
                    sb.Append((c[EqCut] / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(" kHz  ");
                    sb.Append((c[EqClip] * 100f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append("%   ");
                    sb.Append(lista[i].Key).Append("\r\n");
                }
                lock (calSaveLock) EscribeSeguro(destino, sb.ToString());
            }
            catch { }
        }

        // Cierra la medicion de huella tonal de la cancion que acaba de sonar (audio ya detenido).
        private void FinalizeEqMeasure()
        {
            string path = eqMeasPath; eqMeasPath = null;
            double[] sums = eqMeasSum; long n = eqMeasN; int tasa = eqMeasRate;
            eqMeasSum = new double[8]; eqMeasN = 0;
            CierraHuella(path, sums, n, tasa, eqMeasCut, eqMeasCutLo);
        }
        // Lo mismo con lo que se guardo de una cancion que acabo por relevo sin pausa (Form1.SinPausa.cs).
        private void CierraHuella(string path, double[] sums, long n, int tasa, float corte, float corteBajo)
        {
            if (path == null || sums == null || tasa <= 0 || n < (long)(EqMinSec * tasa)) return;   // 20 s a la frecuencia que sea
            float[] curve = new float[EqLen];
            for (int b = 0; b < 8; b++) curve[b] = (float)(10.0 * Math.Log10(sums[b] / n + 1e-12));
            // referencia = MEDIANA de las bandas del CUERPO (158 Hz - 5.6 kHz): invariante al volumen y
            // ademas inmune a una banda desbocada (un lodo de +8 dB no arrastra la referencia consigo).
            float[] cuerpo = { curve[1], curve[2], curve[3], curve[4], curve[5] };
            Array.Sort(cuerpo);
            float refDb = cuerpo[2];
            for (int b = 0; b < 8; b++) curve[b] -= refDb;
            float[] prev; bool had = eqCache.TryGetValue(path, out prev);
            if (had && HasStereoEq(path))
            {
                float maxDelta = 0f;
                for (int b = 0; b < 8; b++) { curve[b] = prev[b] * 0.5f + curve[b] * 0.5f; float d = Math.Abs(curve[b] - prev[b]); if (d > maxDelta) maxDelta = d; }
                if (maxDelta < 0.2f && prev[EqCut] > 0f) return;   // ya aprendida y estable: no reescribir el archivo (cero churn)
            }
            // CORTE: lo mide el propio DSEE mientras la cancion suena, este encendido o no (el acantilado es del
            // ARCHIVO, no del efecto). Si no lo supimos esta vez, se conserva el que ya estuviera guardado.
            curve[EqCut] = (corte > 0f) ? corte : (had ? prev[EqCut] : 0f);
            curve[EqCutLo] = (corteBajo > 0f) ? corteBajo : (had ? prev[EqCutLo] : 0f);
            curve[EqClip] = had ? prev[EqClip] : 0f;   // el recorte solo lo mide el pre-analisis (aqui ya viene reparado)
            curve[EqFingerprint] = 1f;   // no promediar con la antigua medicion mono
            eqCache[path] = curve; eqCacheVer++;
            EnsureEqAverage();
            SaveEqCache();
        }

        private void LoadLoudCache()
        {
            InvalidateVideoMeasurements();
            try { ReadLoudCache(LoudFile(), loudCache, durCache); } catch { }
        }

        private static void ReadLoudCache(string f, Dictionary<string, float> loudCache, Dictionary<string, float> durCache)
        {
            try
            {
                if (!File.Exists(f)) return;
                using (StreamReader reader = new StreamReader(f))
                    ReadLoudCacheRecords(reader, loudCache, durCache);
            }
            catch { }
        }
        private void SaveLoudCache()
        {
            if (startupReadPending) return;
            List<KeyValuePair<string, float>> snap;
            Dictionary<string, float> durSnap;
            try { snap = new List<KeyValuePair<string, float>>(loudCache); durSnap = new Dictionary<string, float>(durCache, StringComparer.OrdinalIgnoreCase); }   // copia (rapida): las tablas solo se tocan aqui
            catch { return; }
            long version = System.Threading.Interlocked.Increment(ref loudSaveVersion);
            GuardaUltimo(loudSaveQueue, delegate
            {
                try
                {
                    Func<bool> vigente = delegate { return version == System.Threading.Interlocked.Read(ref loudSaveVersion); };
                    if (!vigente()) return;
                    lock (loudSaveLock)
                    {
                        EscribeFlujoSeguro(LoudFile(), delegate(TextWriter writer)
                        {
                            for (int i = 0; i < snap.Count; i++)
                            {
                                if ((i & 127) == 0 && !vigente()) return false;
                                float du; if (!durSnap.TryGetValue(snap[i].Key, out du)) du = 0f;
                                writer.Write(snap[i].Value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture));
                                writer.Write('|'); writer.Write(du.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                                writer.Write('|'); writer.Write(snap[i].Key); writer.Write("\r\n");
                            }
                            return true;
                        }, vigente);
                    }
                }
                catch { }
            });
        }

        // ESCALERA DE NIVELADO (etapas por que tan bajita viene la cancion). Antes habia UN solo tope de subida
        // (+15 dB) para todas; ahora cada etapa tiene el SUYO: una cancion ULTRA BAJA puede subir mucho mas (+20 dB)
        // para alcanzar al resto, mientras que una que ya viene FUERTE apenas se toca (no necesita ayuda y subirla
        // solo gastaria margen). Umbrales en dBFS de la sonoridad medida (K-weighted), de mas bajita a mas fuerte.
        private static readonly float[] TierDb = { -34f, -28f, -23f, -19f, -15f, -11f };        // puntos de la escalera (dBFS)
        // Tope de subida (dB) en cada punto. Las canciones BAJITAS se suben mas que antes (una grabacion vieja
        // y floja se quedaba corta y se oia menos que el resto); de la mitad de la escalera para arriba casi no
        // cambia nada, porque esas ya llegaban de sobra. Antes: 20/16/13/10/7/4.
        private static readonly float[] TierMaxDb = { 24f, 19f, 14.5f, 10.8f, 7.4f, 4.3f };
        private static readonly string[] TierName = { "ULTRA BAJA", "BAJA", "MEDIA-BAJA", "MEDIA", "ALTA", "MUY ALTA" };
        // Tope de subida (dB) para una sonoridad dada. Entre punto y punto de la escalera se INTERPOLA en vez
        // de saltar de golpe: con topes en escalon, una cancion apenas mas fuerte que una frontera terminaba
        // MAS BAJITA que una mas callada (un salto audible justo en la frontera). Interpolando, el resultado
        // sube siempre de forma ordenada: mas fuerte de origen nunca acaba sonando mas bajo.
        private static float TierCapDb(float rms)
        {
            if (rms <= 0.0001f) return TierMaxDb[0];
            float db = (float)(20.0 * Math.Log10(rms));
            if (db <= TierDb[0]) return TierMaxDb[0];
            for (int i = 0; i < TierDb.Length - 1; i++)
            {
                if (db <= TierDb[i + 1])
                {
                    float f = (db - TierDb[i]) / (TierDb[i + 1] - TierDb[i]);
                    return TierMaxDb[i] + f * (TierMaxDb[i + 1] - TierMaxDb[i]);
                }
            }
            return TierMaxDb[TierMaxDb.Length - 1];
        }

        // Convierte entre el volumen objetivo (RMS lineal) y el porcentaje 0..100 que muestra la interfaz.
        private int TargetToPct(float t) { int p = (int)Math.Round((t - NormTargetMin) / (NormTargetMax - NormTargetMin) * 100f); return p < 0 ? 0 : (p > 100 ? 100 : p); }
        private float TargetFromPct(int p) { return NormTargetMin + (p / 100f) * (NormTargetMax - NormTargetMin); }

        // Vuelve a decidir si la cancion que suena necesita el respaldo espectral. Se llama al empezar la
        // cancion Y cada vez que se toca algo en la ventana de sonido, para que encender o apagar ULTRA se
        // OIGA en la que esta sonando y no haya que esperar a la siguiente.
        private void RevisaRespaldoDsee() { iaSpectralFallback = iaVivo && sbrAllowed && !iaNowEnhanced; }

        // Aplica el nivelado a la cancion que suena AHORA (para oir el cambio al instante mientras se ajusta en Ctrl+F3).
        private void ApplyNormNow()
        {
            RevisaRespaldoDsee();   // ULTRA puede acabar de encenderse o apagarse: la cancion que suena se entera YA
            LevelTap t = curTap; if (t != null && nowPlaying != null && nowPlaying.Song != null) { t.Gain = NormGainFor(nowPlaying.Song.Path); t.Excite = ExciteAmountFor(nowPlaying.Song.Path); t.Master = MasterLin() * FactorNoche(); t.EqDb = EqCorrFor(nowPlaying.Song.Path); }
            DeepBassProvider bp = curBass; if (bp != null) bp.Amount = BassAmountNow();   // bajo psicoacustico: al instante, ya ajustado a la cancion
            CompensaNoche cn = curNoche; if (cn != null) cn.Db = NocheCompensaDb();   // el modo noche, si se acaba de encender o apagar
            SpectralTap sp = curSbr; if (sp != null) sp.Active = DseeActivo();   // reconstruccion espectral: alterna EN VIVO SIN matar el respaldo de ULTRA
            AplicaAPreparada();   // y a la siguiente, si ya esta preparada para entrar sin pausa
        }

        // Todas las raices de musica que EXISTEN ahora: la carpeta "musica" principal + las agregadas con Ctrl+F2.
        // Una carpeta esta DENTRO de otra (o es la misma). Compara por tramos completos, asi "musica2" NO
        // cuenta como dentro de "musica".
        private static bool PathInside(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            string x = a.TrimEnd('\\', '/'), y = b.TrimEnd('\\', '/');
            if (string.Equals(x, y, StringComparison.OrdinalIgnoreCase)) return true;
            return x.Length > y.Length + 1
                && x.StartsWith(y, StringComparison.OrdinalIgnoreCase)
                && (x[y.Length] == '\\' || x[y.Length] == '/');
        }

        private List<string> AllMusicRoots()
        {
            // Primero, todas las que EXISTEN ahora mismo. Las que no existen -un disco desconectado- no se
            // tiran de la configuracion: siguen guardadas y vuelven solas cuando el disco reaparezca.
            List<string> cand = new List<string>();
            try { if (Directory.Exists(MusicFolder)) cand.Add(MusicFolder); } catch { }
            for (int i = 0; i < extraMusicDirs.Count; i++)
            {
                try { if (Directory.Exists(extraMusicDirs[i])) cand.Add(extraMusicDirs[i]); } catch { }
            }
            // Y ahora se sueltan las que estan DENTRO de otra. Mirar las dos escanearia lo mismo dos veces y
            // cada cancion saldria duplicada en las listas, el buscador y las estadisticas.
            //
            // LA QUE SE SUELTA ES LA DE DENTRO, no la de fuera. Antes era al reves -se quedaba la primera de
            // la lista, que es siempre "musica"-, y eso hacia inutil lo mas normal de todo: decirle a la
            // rocola "la musica esta en ESTE disco". El disco contiene a "musica", asi que el disco entero se
            // soltaba y se seguia mirando solo la carpetita de siempre. Mirar la de fuera ya cubre a la de
            // dentro; al reves no.
            List<string> roots = new List<string>();
            for (int i = 0; i < cand.Count; i++)
            {
                bool dentroDeOtra = false;
                for (int j = 0; j < cand.Count && !dentroDeOtra; j++)
                {
                    if (j == i) continue;
                    // Si son la MISMA carpeta, cada una esta "dentro" de la otra: se queda la primera.
                    if (PathInside(cand[i], cand[j]) && (!PathInside(cand[j], cand[i]) || j < i)) dentroDeOtra = true;
                }
                if (!dentroDeOtra) roots.Add(cand[i]);
            }
            return roots;
        }

        // POR QUE UNA CARPETA NO SE PUEDE AGREGAR, en palabras -o nada, si se puede-.
        //
        // Esto vivia dentro del boton "Agregar carpeta..." de la ventana, donde ninguna prueba lo alcanzaba,
        // y tenia dos fallos que juntos impedian lo mas normal: elegir el disco entero.
        //
        //   - Se comparaba contra la carpeta "musica" del programa EXISTIERA O NO. Y el disco donde esta el
        //     programa siempre la contiene, asi que elegir el disco entero contestaba siempre "esa carpeta ya
        //     esta incluida"... hasta cuando esa carpeta no existia y no se estaba escaneando nada.
        //   - Y se rechazaba tambien cuando la carpeta nueva CONTENIA a una que ya estaba, que es justo el
        //     caso bueno: ahi lo que toca es quedarse con la de fuera (ver AllMusicRoots).
        //
        // Lo que si se sigue rechazando es lo que de verdad duplicaria musica: la misma carpeta otra vez, o
        // una metida DENTRO de otra que ya esta.
        private string PorQueNoSePuedeAgregar(string p)
        {
            if (string.IsNullOrEmpty(p)) return "Esa carpeta no vale.";
            try { if (!Directory.Exists(p)) return "Esa carpeta no existe (o el disco no esta conectado)."; }
            catch { return "No se puede leer esa carpeta."; }
            List<string> yaEstan = AllMusicRoots();
            for (int i = 0; i < yaEstan.Count; i++)
            {
                if (!PathInside(p, yaEstan[i])) continue;
                return PathInside(yaEstan[i], p)
                     ? "Esa carpeta ya esta incluida."
                     : "Esa carpeta ya esta dentro de \"" + yaEstan[i] + "\", que ya esta incluida.";
            }
            return null;
        }


        // Ctrl+F2: GESTOR VISUAL de carpetas de musica. Lista la principal ("musica", fija) y las ADICIONALES
        // (pueden estar en otros discos, SSD/HDD); permite AGREGAR y QUITAR. Los cambios se guardan al cerrar
        // y disparan UN solo re-escaneo. Es una herramienta de mantenimiento (requiere teclado/mouse): el
        // cursor se muestra solo mientras el gestor esta abierto y el vigia de foco queda en pausa.
        // EL RENGLON DE CADA CARPETA EN LA LISTA.
        //
        // El aviso de que una carpeta NO ESTA va DELANTE, no detras. Las rutas son largas -en la maquina del
        // bar, muy largas- y la lista las corta por la derecha, asi que un aviso puesto al final se queda
        // fuera de la pantalla justo cuando mas falta hace. Se vio en una foto de la ventana abriendose sola
        // por falta de musica: se leia la ruta entera y el "(NO EXISTE)" no aparecia por ningun lado.
        private static string RenglonDeCarpeta(string ruta, bool principal, bool existe)
        {
            string aviso = existe ? "" : (principal ? "(NO EXISTE)  " : "(no disponible)  ");
            return aviso + ruta + (principal ? "   [principal - fija]" : "");
        }

        private void ShowMusicDirManager() { ShowMusicDirManager(false); }
        private void ShowMusicDirManager(bool porFaltaDeMusica)
        {
            if (searchOpen) CloseSearch();
            bool wasMaint = maintMode;
            maintMode = true;   // pausa el vigia de foco: el dialogo modal necesita quedarse al frente
            gestorAbierto = true;   // mientras este abierto, que no se pida abrirlo otra vez (ver PideElegirMusica)
            bool changed = false;
            try
            {
                Cursor.Show();
                using (Font fUi = new Font(FontFamily.GenericSansSerif, 10f))
                using (Form dlg = new Form())
                {
                    dlg.Text = "Rocola - Carpetas de musica";
                    dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dlg.MaximizeBox = false; dlg.MinimizeBox = false; dlg.ShowInTaskbar = false; dlg.ShowIcon = false;
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.ClientSize = new Size(640, 372);
                    dlg.BackColor = Color.FromArgb(14, 18, 42);
                    dlg.Font = fUi;

                    Label info = new Label();
                    info.SetBounds(14, 10, 612, 42);
                    info.BackColor = Color.Transparent;
                    info.ForeColor = Color.FromArgb(205, 214, 238);
                    // Cuando se abre SOLA es porque no hay ni una cancion, y quien esta delante tiene que
                    // saber por que le salio esta ventana y que se espera de el. Abierta a mano (Ctrl+F2) el
                    // texto de siempre.
                    info.Text = porFaltaDeMusica
                        ? "NO ENCONTRE MUSICA. Pulsa \"Agregar carpeta...\" y elige donde esta: puede ser una carpeta, o el disco entero. Si la musica esta en un disco aparte, conectalo antes."
                        : "La carpeta 'musica' (junto al programa) siempre se incluye. Aqui puedes agregar o quitar carpetas ADICIONALES, incluso de otros discos (SSD/HDD).";

                    ListBox list = new ListBox();
                    list.SetBounds(14, 58, 612, 230);
                    list.BackColor = Color.FromArgb(24, 30, 62);
                    list.ForeColor = Color.White;
                    list.BorderStyle = BorderStyle.FixedSingle;
                    list.IntegralHeight = false;
                    list.HorizontalScrollbar = true;   // las rutas son largas: que se puedan leer enteras

                    Button bAdd = new Button(), bDel = new Button(), bOk = new Button();
                    bAdd.Text = "Agregar carpeta..."; bAdd.SetBounds(14, 300, 180, 36);
                    bDel.Text = "Quitar seleccionada"; bDel.SetBounds(202, 300, 180, 36);
                    bOk.Text = "Listo"; bOk.SetBounds(530, 300, 96, 36);
                    Button[] bs = { bAdd, bDel, bOk };
                    for (int i = 0; i < bs.Length; i++)
                    {
                        bs[i].FlatStyle = FlatStyle.Flat;
                        bs[i].FlatAppearance.BorderColor = Color.FromArgb(90, 110, 170);
                        bs[i].BackColor = Color.FromArgb(34, 44, 86);
                        bs[i].ForeColor = Color.White;
                    }

                    // Rellena la lista: [0] = principal (fija); debajo las adicionales, marcando las no disponibles
                    // (disco desconectado): esas siguen guardadas y vuelven solas cuando el disco reaparezca.
                    Action refresh = delegate
                    {
                        list.Items.Clear();
                        string main = MusicFolder;
                        bool mainOk = false; try { mainOk = Directory.Exists(main); } catch { }
                        list.Items.Add(RenglonDeCarpeta(main, true, mainOk));
                        for (int i = 0; i < extraMusicDirs.Count; i++)
                        {
                            bool ok = false; try { ok = Directory.Exists(extraMusicDirs[i]); } catch { }
                            list.Items.Add(RenglonDeCarpeta(extraMusicDirs[i], false, ok));
                        }
                        bDel.Enabled = extraMusicDirs.Count > 0;
                    };

                    bAdd.Click += delegate
                    {
                        using (FolderBrowserDialog fb = new FolderBrowserDialog())
                        {
                            fb.Description = "Elige una carpeta ADICIONAL con musica (puede estar en otro disco).";
                            fb.ShowNewFolderButton = false;
                            if (fb.ShowDialog(dlg) != DialogResult.OK || string.IsNullOrEmpty(fb.SelectedPath)) return;
                            string p = fb.SelectedPath;
                            string no = PorQueNoSePuedeAgregar(p);
                            if (no != null) { MessageBox.Show(dlg, no, "Rocola"); return; }
                            extraMusicDirs.Add(p); changed = true; refresh();
                        }
                    };
                    bDel.Click += delegate
                    {
                        int i = list.SelectedIndex;
                        if (i < 0) { MessageBox.Show(dlg, "Selecciona una carpeta de la lista.", "Rocola"); return; }
                        if (i == 0) { MessageBox.Show(dlg, "La carpeta principal 'musica' no se puede quitar.", "Rocola"); return; }
                        extraMusicDirs.RemoveAt(i - 1); changed = true; refresh();
                    };
                    bOk.Click += delegate { dlg.Close(); };
                    dlg.CancelButton = bOk;   // Esc tambien cierra (los cambios ya hechos se aplican igual)

                    dlg.Controls.Add(info); dlg.Controls.Add(list);
                    dlg.Controls.Add(bAdd); dlg.Controls.Add(bDel); dlg.Controls.Add(bOk);
                    refresh();
                    dlg.ShowDialog(this);
                }
                if (changed)
                {
                    SaveConfigPrefs();
                    ShowToast("Carpetas actualizadas: escaneando musica...");
                    try { File.Delete(LibCacheFile()); } catch { }   // el cache de biblioteca ya no corresponde
                    LoadMusicFolder();
                }
            }
            catch { }
            // OJO CON EL ORDEN: el "changed" de arriba vuelve a cargar la musica, y eso puede volver a pedir
            // esta misma ventana. Se suelta la marca DESPUES, cuando ya no hay nada en curso.
            finally { gestorAbierto = false; try { Cursor.Hide(); } catch { } maintMode = wasMaint; ForceFocus(); }
        }

        // Busca automaticamente una carpeta llamada "musica" junto a los datos o hasta 3 niveles arriba
        // (ej.: nueva\Release\Rocola.exe  +  nueva\musica). No requiere elegir carpeta.
        private string MusicFolder
        {
            get
            {
                try
                {
                    string dir = CarpetaDatos;
                    for (int i = 0; i < 4 && dir != null; i++)
                    {
                        string cand = Path.Combine(dir, "musica");
                        if (Directory.Exists(cand)) return cand;
                        DirectoryInfo p = Directory.GetParent(dir);
                        dir = (p != null) ? p.FullName : null;
                    }
                }
                catch { }
                return RutaDatos("musica");   // por defecto, junto a los datos
            }
        }
        // ---- Canciones ya repasadas A FONDO (mismo formato que las de la IA: una ruta por linea) ----
        private string DeepDoneFile() { return RutaDatos("rocola_afondo.txt"); }
        private readonly object deepDoneLock = new object();
        private volatile bool deepDoneDirty = false;   // hay canciones repasadas sin apuntar en disco
        private readonly List<string> deepPend = new List<string>();   // rutas apuntadas y aun sin escribir

        // ---- Duraciones rapidas: "segundos|ruta" por linea, escritas POR ANADIDO ----
        private string DurFastFile() { return RutaDatos("rocola_duracion.txt"); }
        private void GuardaTintes(int firma)
        {
            Dictionary<char, string> copia = new Dictionary<char, string>(letterTag);
            Dictionary<string, string> copiaD = new Dictionary<string, string>(discTag, StringComparer.OrdinalIgnoreCase);
            string ruta = LetterTintFile();
            long version = System.Threading.Interlocked.Increment(ref tintesSaveVersion);
            // Incluido en el drenaje de cierre; la ultima instantanea manda.
            GuardaUltimo(tintesSaveQueue, delegate
            {
                try
                {
                    Func<bool> vigente = delegate { return version == System.Threading.Interlocked.Read(ref tintesSaveVersion); };
                    EscribeFlujoSeguro(ruta, delegate(TextWriter writer)
                    {
                        writer.Write("ROCOLA-LETRAS v2\r\nn="); writer.Write(firma); writer.Write("\r\n");
                        foreach (KeyValuePair<char, string> kv in copia)
                        {
                            if (!vigente()) return false;
                            writer.Write("L|"); writer.Write(kv.Key); writer.Write('|'); writer.Write(kv.Value); writer.Write("\r\n");
                        }
                        int n = 0;
                        foreach (KeyValuePair<string, string> kv in copiaD)
                        {
                            if ((n++ & 127) == 0 && !vigente()) return false;
                            writer.Write("D|"); writer.Write(kv.Key); writer.Write('|'); writer.Write(kv.Value); writer.Write("\r\n");
                        }
                        return true;
                    }, vigente);
                }
                catch { }
            });
        }

        // Lo que va encontrando el paseo de fondo: por cada letra, la etiqueta de genero que mas se repite.
        private void PublicaTintes(int gen, char[] letras, string[] etiquetas, string[] discos, string[] etDiscos, bool ultimo, int firma)
        {
            if (gen != durScanGen) return;
            for (int i = 0; i < letras.Length && i < etiquetas.Length; i++) letterTag[letras[i]] = etiquetas[i];
            for (int i = 0; i < discos.Length && i < etDiscos.Length; i++) discTag[discos[i]] = etDiscos[i];
            RecalculaGenerosArtista();   // con los generos de disco nuevos, los del artista cambian
            if (ultimo) { tintesPend = false; GuardaTintes(firma); }
            RecalculaLetras();
        }

        // Corre en el hilo de la interfaz: las tablas son suyas. "faltan" es el avance ABSOLUTO que reporta el
        // hilo (cuenta tambien los archivos rotos, que no traen duracion pero ya quedaron atras).
        private void PublicaDuraciones(int gen, string[] rutas, float[] segs, int faltan)
        {
            if (gen != durScanGen) return;   // generacion caducada (hubo re-escaneo): descartar
            for (int i = 0; i < rutas.Length; i++)
            {
                if (durFast.ContainsKey(rutas[i])) continue;
                durFast[rutas[i]] = segs[i];
                lock (durFastLock) durFastPend.Add(segs[i].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "|" + rutas[i]);
            }
            durScanLeft = faltan < 0 ? 0 : faltan;
            SaveDurFast();
            if (rutas.Length > 0 && panelSongs != null && panelSongs.Visible && songList != null)
            {
                RefrescaLista();
#if ROCOLA_GPU
                uiOverlaySig = SigNada;   // la lista va horneada en la textura de UI: hay que rehacerla para que se vean
#endif
            }
        }

        // APUNTAR LO QUE SE ACABA DE MEDIR. UN SOLO SITIO, y por eso vive aqui suelto: lo llaman el
        // pre-analisis y el calentador de la siguiente cancion. Con dos sitios que escriban las mismas tres
        // tablas, el dia que alguien arregle uno se olvidara del otro.
        //
        // SOBRESCRIBE lo que hubiera: estas medidas recorren TODA la cancion -repartida en tramos, o entera si es
        // corta o va a fondo-, asi que son mejores que lo aprendido a medias oyendola (que promediaba entre
        // pasadas). Siguen siendo la ESTIMACION: la medida integrada de la cancion entera va aparte, en
        // PublicaIntegrada (ver Sonoridad.cs).
        //
        // Las tablas son del hilo de la pantalla y no llevan candado -a proposito, para no pagarlo en cada
        // consulta-, asi que quien llame a esto tiene que estar EN ese hilo.
        private void PublicaMedida(string ruta, float rms, float dur, float[] eqCurve)
        {
            if (ruta == null) return;
            bool puesto = false;
            if (dur > 0.01f) { durCache[ruta] = dur; puesto = true; }
            if (rms > 0f) { loudCache[ruta] = rms; InvalidateVideoMeasurements(); puesto = true; }   // incluido el silencio (ver LoudMudo)
            if (eqCurve != null) { eqCache[ruta] = eqCurve; eqCacheVer++; eqDirty = true; puesto = true; }   // huella tonal del EQ automatico
            // Y se apunta en la libreta compartida, para que nadie la vuelva a medir en esta sesion. Solo
            // si de verdad quedo algo escrito: un archivo roto no da nada, y darlo por medido seria no
            // volver a intentarlo nunca.
            if (puesto && rms > 0f && dur > 0.01f) MedidaPuesta(ruta);
            if (panelSongs != null && panelSongs.Visible) RefrescaLista();   // que aparezca la duracion recien sabida
        }

        // ============================== TU TURNO EN LA COLA ==============================
        //
        // La pastilla dice cuantas canciones van pedidas. Eso es un dato del LOCAL: al que acaba de
        // meter su moneda no le sirve saber que hay cuatro, le sirve saber CUAL ES LA SUYA y cuanto
        // falta. Y no es un detalle bonito: es la causa de que la gente vuelva a picarle al boton -a
        // veces gastando otro credito- creyendo que la maquina no le hizo caso.
        //
        // La cuenta va APARTE, en dos funciones puras que reciben numeros y devuelven un numero. Asi se
        // les pueden dar casos a mano y comprobar que contestan bien (pruebas/turno.cs), que con esto
        // metido dentro de la pantalla no se podria. Es la misma idea que ProponeEscalones en el medidor.

        // Los segundos que faltan para que empiece TU cancion, o -1 si NO SE PUEDE SABER.
        //
        // Basta con que de UNA de las que van por delante no se sepa la duracion -recien copiada al
        // disco, aun sin analizar- para que la cuenta entera deje de valer. Y entonces hay que decirlo,
        // no redondear: un "en ~11 min" que luego son veinte es peor que no decir nada, porque la
        // proxima vez ya nadie se lo cree.
        internal static double EsperaSegundos(double[] antesQueLaTuya, double restanDeLaQueSuena)
        {
            if (restanDeLaQueSuena < 0) return -1;
            double s = restanDeLaQueSuena;
            if (antesQueLaTuya != null)
                for (int i = 0; i < antesQueLaTuya.Length; i++)
                {
                    if (!(antesQueLaTuya[i] > 0)) return -1;
                    s += antesQueLaTuya[i];
                }
            return s;
        }

        private bool TryLoadLibraryCache(List<string> roots, out List<Track> outTracks, out List<string> outVideos, out Dictionary<string, string> outCovers, out string observedSig)
        {
            outTracks = null; outVideos = null; outCovers = null; observedSig = null;
            try
            {
                string f = LibCacheFile();
                if (!File.Exists(f)) return false;
                using (StreamReader reader = new StreamReader(f))
                {
                    // Rechazar cabeceras antiguas sin cargar todas las rutas en memoria.
                    if (reader.ReadLine() != "ROCOLA-LIB v2") return false;
                    string line = reader.ReadLine();
                    if (line == null || !line.StartsWith("roots=") || !string.Equals(line.Substring(6), JoinRoots(roots), StringComparison.OrdinalIgnoreCase)) return false;
                    string sig = RootsSigForScan(roots);
                    observedSig = sig;
                    line = reader.ReadLine();
                    if (line == null || !line.StartsWith("sig=") || line.Substring(4) != sig) return false;
                    line = reader.ReadLine();
                    if (line == null || !line.StartsWith("dirs=")) return false;
                    return ReadLibraryRecords(reader, out outTracks, out outVideos, out outCovers);
                }
            }
            catch { outTracks = null; outVideos = null; outCovers = null; return false; }
        }

        private static bool ReadLibraryCount(TextReader reader, char section, out int count)
        {
            count = 0;
            string line = reader.ReadLine();
            return line != null && line.Length > 0 && line[0] == section
                && int.TryParse(line.Substring(1), out count) && count >= 0;
        }

        private static bool ReadLibraryRecords(TextReader reader, out List<Track> outTracks, out List<string> outVideos, out Dictionary<string, string> outCovers)
        {
            outTracks = null; outVideos = null; outCovers = null;
            // No reservar segun un contador del archivo: puede estar truncado o corrupto.
            // Publicar solo tras alcanzar EOF, tambien si hay lineas sobrantes.
            int count;
            if (!ReadLibraryCount(reader, 'A', out count)) return false;
            List<Track> tk = new List<Track>();
            for (int i = 0; i < count; i++)
            {
                string path = reader.ReadLine(); if (path == null) return false;
                if (path.Length > 0) tk.Add(Track.FromFile(path));
            }
            if (!ReadLibraryCount(reader, 'V', out count)) return false;
            List<string> vd = new List<string>();
            for (int i = 0; i < count; i++)
            {
                string path = reader.ReadLine(); if (path == null) return false;
                if (path.Length > 0) vd.Add(path);
            }
            if (!ReadLibraryCount(reader, 'C', out count)) return false;
            Dictionary<string, string> cv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < count; i++)
            {
                string line = reader.ReadLine(); if (line == null) return false;
                int b = line.IndexOf('|'); if (b <= 0) continue;
                cv[line.Substring(0, b)] = line.Substring(b + 1);
            }
            while (reader.ReadLine() != null) { }
            if (tk.Count == 0) return false;
            outTracks = tk; outVideos = vd; outCovers = cv;
            return true;
        }

        // ===================== ESTADO VIVO (reanudar tras un corte de luz) =====================
        // Guarda creditos + lo que suena + la cola en rocola_estado.txt (solo cambia en eventos: moneda,
        // encolar, cambio de cancion). Si se corta la luz, al volver la rocola RETOMA: la cancion que estaba
        // sonando arranca DESDE EL PRINCIPIO (a peticion: media cancion confunde mas de lo que ayuda), con su
        // cola y los creditos del cliente intactos. Para el CIERRE del negocio: SALTAR repetido vacia la cola
        // y, con todo vacio, una pulsacion de SALTAR borra los creditos -> estado vacio -> arranca limpia.
        // El archivo se DESCARTA por tres vias que distinguen "cierre" de "apagon": (1) apagado deliberado
        // (boton de poder -> Windows apaga, u Alt+F4) borra el archivo en OnFormClosing; (2) al cruzar las 7 AM
        // estando ociosa (CheckBusinessDayReset); (3) al restaurar, si el estado ya cruzo las 7 AM y no es reciente.
        // Un apagon no dispara nada de esto -> el archivo sobrevive y la rocola reanuda.
        private string StateFile() { return RutaDatos("rocola_estado.txt"); }
        private bool stateRestored = false;   // la restauracion corre UNA vez (al primer armado de biblioteca)
        private List<string> stateMissingRoots;
        private int stateRootRetryTick;

        private string RaizConfiguradaAusente(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            List<string> cfg = new List<string>(); cfg.Add(MusicFolder);
            for (int i = 0; i < extraMusicDirs.Count; i++) cfg.Add(extraMusicDirs[i]);
            string mejor = null;
            for (int i = 0; i < cfg.Count; i++)
            {
                string r = cfg[i]; if (string.IsNullOrEmpty(r)) continue;
                try { r = Path.GetFullPath(r); } catch { continue; }
                if (!PathInside(path, r) || (mejor != null && mejor.Length >= r.Length)) continue;
                mejor = r;
            }
            if (mejor == null) return null;
            try { return Directory.Exists(mejor) ? null : mejor; } catch { return mejor; }
        }

        // El estado de recuperacion pertenece a UN "dia del negocio", que ARRANCA a las 7 AM (hora del sistema): a
        // esa hora ya cerro, y nada de la noche anterior debe cruzar las 7 AM. Todo lo de antes de las 7 AM cuenta
        // como el dia ANTERIOR. Al pasar esa hora: el estado guardado se DESCARTA al restaurar (arranque limpio en
        // la manana) y se LIMPIA si la rocola sigue encendida (creditos/cola a cero) -> cada dia arranca en cero.
        private const int BusinessDayStartHour = 7;
        private const int DayResetIdleMins = 10;   // minutos OCIOSA (sin musica/cola/botones) para aplicar el reinicio de 7 AM: si te desvelas, NO te interrumpe
        private const int StateRecentMins = 30;    // si el estado se guardo hace menos de esto, se reanuda aunque cruce las 7 AM (fue un corte breve en plena sesion)
        private static DateTime BusinessDay(DateTime dt) { return dt.AddHours(-BusinessDayStartHour).Date; }
        private DateTime stateDay = BusinessDay(DateTime.Now);   // dia del negocio del estado vivo actual

        private void SaveState()
        {
            try
            {
                StringBuilder sb = new StringBuilder(256);
                sb.Append("ROCOLA-ESTADO v1\r\n");
                sb.Append("ts=").Append(DateTime.Now.Ticks).Append("\r\n");   // cuando se guardo (para descartarlo si cruza las 7 AM)
                sb.Append("creditos=").Append(credits).Append("\r\n");
                if (nowPlaying != null)
                    sb.Append("sonando=").Append(nowPlaying.IsVideo ? 'V' : 'S').Append('|').Append(nowPlaying.PathKey()).Append("\r\n");
                for (int i = 0; i < queue.Count; i++)
                    sb.Append("cola=").Append(queue[i].IsVideo ? 'V' : 'S').Append('|').Append(queue[i].PathKey()).Append("\r\n");
                EscribeSeguro(StateFile(), sb.ToString());   // archivo diminuto (<1KB): escritura sincrona sin costo notable
            }
            catch { }
        }

        // Al cruzar las 7 AM (nuevo dia del negocio) reinicia el estado guardado. PERO nunca interrumpe una sesion
        // en uso: si te DESVELAS, tu musica NO deja de sonar y tus creditos NO se tocan. El reinicio ESPERA a que la
        // rocola quede OCIOSA (nada sonando, cola vacia y sin botones un buen rato); recien entonces borra los
        // creditos que hayan quedado (de alguien que se fue) y el archivo guardado -> el dia arranca limpio.
        private void CheckBusinessDayReset()
        {
            if (BusinessDay(DateTime.Now) == stateDay) return;                          // mismo dia del negocio: comun, nada que hacer
            if (nowPlaying != null || queue.Count > 0) return;                          // hay musica o cola: EN USO -> se aplaza
            if ((DateTime.Now - lastInput).TotalMinutes < DayResetIdleMins) return;     // botones recientes: EN USO -> se aplaza
            stateDay = BusinessDay(DateTime.Now);                                       // ociosa y dia nuevo -> reinicia UNA vez
            if (credits > 0) { credits = 0; UpdateCredits(); }                          // creditos que quedaron de anoche -> a cero
            BorraSeguro(StateFile());                                                   // borra el estado guardado (y su copia) -> arranque limpio
            BackupLearnedData();                                                        // cierre del dia: respalda lo aprendido (una vez al dia)
        }

        // ===================== RESPALDO DE DATOS APRENDIDOS =====================
        // El disco puede morir (ya paso). La musica no se recupera, pero SI todo lo que la rocola aprendio:
        // volumenes nivelados, huellas del EQ, estadisticas de lo mas sonado, indice de biblioteca,
        // config, mapa de botones y el modelo de IA. Se copia a respaldo\<fecha>\ (y a respaldo_dir= si se
        // configuro, idealmente OTRO disco), guardando los 3 dias mas recientes. Son unos KB: barato y rapido.
        private string BackupBaseDir() { return RutaDatos("respaldo"); }
        private void BackupLearnedData()
        {
            string sp = CarpetaDatos, second = backupDir, baseDir = BackupBaseDir();
            string[] files = {
                LoudFile(), EqFile(), LibStatsFile(), LibCacheFile(), ConfigFile, KeysFile(),
                RutaLectura("rocola_ia.bin"), RutaLectura("rocola_eq_ia.bin"),
                Path.Combine(sp, "rocola_incompatibles.txt"),
                IaDoneFile(), DeepDoneFile(),  // sin estas dos, un repaso a fondo empezaria de cero y la IA repetiria ejemplos
                DurFastFile(), LetterTintFile(),   // sin este, la barra tendria que releer el ID3 de toda la biblioteca
                IntegFile()   // sin esta, habria que volver a descomprimir la biblioteca entera para medirla
            };
            System.Threading.ThreadPool.QueueUserWorkItem(delegate   // fuera del hilo de la interfaz (no frena nada)
            {
                try
                {
                    string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmssfff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N");
                    BackupDestino(baseDir, stamp, files);
                    if (!string.IsNullOrEmpty(second)) BackupDestino(Path.Combine(second, "respaldo_rocola"), stamp, files);
                }
                catch { }
            });
        }
        private void BackupDestino(string root, string stamp, string[] files)
        {
            try { BackupTo(Path.Combine(root, stamp), files); PruneBackups(root, 3); }
            catch (Exception ex)
            {
                LogError("Respaldo incompleto en " + root + " (se conservan los anteriores)", ex);
                try { BeginInvoke((MethodInvoker)delegate { ShowToast("Respaldo incompleto; anteriores conservados. Ver rocola_error.txt", Color.FromArgb(200, 80, 70)); }); } catch { }
            }
        }

        private static readonly object backupLock = new object();
        private static void BackupTo(string dir, string[] files)
        {
            lock (backupLock)
            {
                dir = Path.GetFullPath(dir);
                string parent = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent) || Directory.Exists(dir) || File.Exists(dir))
                    throw new IOException("El destino de respaldo debe ser una carpeta nueva");
                Directory.CreateDirectory(parent);
                string pending = Path.Combine(parent, ".rocola_backup_pending_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(pending);
                try
                {
                    int copied = 0;
                    byte[] buffer = new byte[65536];
                    for (int i = 0; i < files.Length; i++)
                    {
                        if (string.IsNullOrEmpty(files[i])) continue;
                        FileStream input;
                        try { input = new FileStream(files[i], FileMode.Open, FileAccess.Read, FileShare.Read); }
                        catch (FileNotFoundException) { continue; }       // datos opcionales aun no creados
                        catch (DirectoryNotFoundException) { continue; }
                        using (input)
                        using (System.Security.Cryptography.SHA256 copyHash = System.Security.Cryptography.SHA256.Create())
                        {
                            string dst = Path.Combine(pending, Path.GetFileName(files[i]));
                            using (FileStream outputFile = new FileStream(dst, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, FileOptions.WriteThrough))
                            {
                                int n;
                                while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    outputFile.Write(buffer, 0, n);
                                    copyHash.TransformBlock(buffer, 0, n, buffer, 0);
                                }
                                copyHash.TransformFinalBlock(buffer, 0, 0);
                                outputFile.Flush();
                                if (!FlushFileBuffers(outputFile.SafeFileHandle.DangerousGetHandle())) throw new IOException("No se pudo confirmar el respaldo en disco");
                            }
                            using (System.Security.Cryptography.SHA256 hash = System.Security.Cryptography.SHA256.Create())
                            using (FileStream check = File.OpenRead(dst))
                            {
                                byte[] a = copyHash.Hash, b = hash.ComputeHash(check);
                                for (int k = 0; k < a.Length; k++) if (a[k] != b[k]) throw new IOException("La copia del respaldo no coincide");
                            }
                            copied++;
                        }
                    }
                    if (copied == 0) throw new IOException("No habia archivos legibles para respaldar");
                    // Rename en el mismo volumen: la limpieza nunca ve un respaldo a medio copiar.
                    Directory.Move(pending, dir);
                }
                finally
                {
                    // Solo la carpeta unica creada por ESTA operacion; nunca un respaldo publicado.
                    if (Directory.Exists(pending)) try { Directory.Delete(pending, true); } catch { }
                }
            }
        }
        // Deja solo las 'keep' carpetas de respaldo mas recientes (por fecha de modificacion); borra el resto.
        private static void PruneBackups(string baseDir, int keep)
        {
            try
            {
                DirectoryInfo bd = new DirectoryInfo(baseDir);
                if (!bd.Exists) return;
                List<DirectoryInfo> validas = new List<DirectoryInfo>();
                foreach (DirectoryInfo d in bd.GetDirectories())
                {
                    DateTime fecha;
                    if ((d.Attributes & FileAttributes.ReparsePoint) != 0 || d.Name.Length < 10 ||
                        !DateTime.TryParseExact(d.Name.Substring(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha) ||
                        (d.Name.Length != 10 && d.Name[10] != '_')) continue;
                    if (d.GetFiles().Length > 0) validas.Add(d);
                }
                DirectoryInfo[] dirs = validas.ToArray();
                if (dirs.Length <= keep) return;
                Array.Sort(dirs, delegate (DirectoryInfo a, DirectoryInfo b) { return b.LastWriteTime.CompareTo(a.LastWriteTime); });
                for (int i = keep; i < dirs.Length; i++) { try { dirs[i].Delete(true); } catch { } }
            }
            catch { }
        }

        // Cuando se guardo el estado: la marca "ts=" (ticks); si falta (formato viejo), la fecha del archivo.
        private static DateTime SavedStateTime(string[] lines, string file)
        {
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].StartsWith("ts="))
                { long t; if (long.TryParse(lines[i].Substring(3), out t) && t > 0) { try { return new DateTime(t); } catch { } } break; }
            try { return File.GetLastWriteTime(file); } catch { return DateTime.Now; }
        }

        // Reconstruye creditos/cola/cancion desde el archivo de estado (si hay algo que reanudar).
        private void RestoreState()
        {
            if (stateRestored) return;
            stateRestored = true;
            stateMissingRoots = null;
            try
            {
                string f = StateFile();
                // Si el archivo bueno falta o quedo vacio -un corte de luz de los de antes- se lee la copia:
                // se pierde como mucho lo ultimo que se hizo, no el dinero entero del cliente.
                string[] L = LeeLineasSeguro(f);
                if (L == null || L.Length < 1) return;
                if (L[0] != "ROCOLA-ESTADO v1")
                {
                    try { if (File.Exists(f + ".bak")) L = File.ReadAllLines(f + ".bak"); } catch { }
                    if (L == null || L.Length < 1 || L[0] != "ROCOLA-ESTADO v1") return;
                }
                // El estado solo vale DENTRO del mismo dia del negocio (corte a las 7 AM): lo de anoche, visto ya en
                // la manana, se descarta (arranque limpio). PERO si se guardo hace poquito (corte breve en plena
                // sesion, p.ej. un parpadeo de luz justo al dar las 7 mientras te desvelas), se reanuda igual.
                DateTime saved = SavedStateTime(L, f);
                if (BusinessDay(DateTime.Now) != BusinessDay(saved) && (DateTime.Now - saved).TotalMinutes > StateRecentMins)
                { BorraSeguro(f); return; }
                // mapas ruta -> cancion/disco para reconstruir los elementos (solo lo que SIGA existiendo)
                Dictionary<string, Track> byPath = new Dictionary<string, Track>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, Album> discByPath = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < albums.Count; i++)
                    for (int j = 0; j < albums[i].Tracks.Count; j++)
                    {
                        Track t = albums[i].Tracks[j];
                        if (t != null && t.Path != null && !byPath.ContainsKey(t.Path)) { byPath[t.Path] = t; discByPath[t.Path] = albums[i]; }
                    }
                Dictionary<string, Album> vidDisc = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
                if (videoAlbums != null)
                    for (int i = 0; i < videoAlbums.Count; i++)
                        for (int j = 0; j < videoAlbums[i].VideoFiles.Count; j++)
                        { string vp = videoAlbums[i].VideoFiles[j]; if (vp != null && !vidDisc.ContainsKey(vp)) vidDisc[vp] = videoAlbums[i]; }

                int cr = 0; PlayItem playing = null; List<PlayItem> q = new List<PlayItem>();
                List<string> faltan = new List<string>();
                for (int i = 1; i < L.Length; i++)
                {
                    string s = L[i];
                    if (s.Length == 0) continue;
                    if (s.StartsWith("creditos=")) { int.TryParse(s.Substring(9), out cr); continue; }
                    if (s.StartsWith("pos=")) continue;   // formato viejo: la posicion ya no se usa (siempre desde el principio)
                    bool isNow = s.StartsWith("sonando=");
                    if (!isNow && !s.StartsWith("cola=")) continue;
                    string v = s.Substring(s.IndexOf('=') + 1);
                    if (v.Length < 3 || v[1] != '|') continue;
                    bool isVid = (v[0] == 'V'); string path = v.Substring(2);
                    PlayItem it = null;
                    if (isVid)
                    {
                        bool exists = false; try { exists = File.Exists(path); } catch { }
                        if (exists) { it = new PlayItem(); it.IsVideo = true; it.VideoPath = path; Album d; vidDisc.TryGetValue(path, out d); it.Disc = d; }
                    }
                    else
                    {
                        Track t;
                        if (byPath.TryGetValue(path, out t)) { it = new PlayItem(); it.Song = t; Album d; discByPath.TryGetValue(path, out d); it.Disc = d; }
                    }
                    if (it == null)
                    {
                        // Si la ruta pertenece a un disco configurado que hoy no esta, NO se consume el
                        // estado ni los creditos. Se espera a que vuelva y se reescanea automaticamente.
                        // Con la raiz presente, la seleccion fue borrada: devuelve el credito ya pagado.
                        string raiz = RaizConfiguradaAusente(path);
                        if (raiz != null)
                        {
                            bool repetida = false;
                            for (int k = 0; k < faltan.Count; k++)
                                if (string.Equals(faltan[k], raiz, StringComparison.OrdinalIgnoreCase)) { repetida = true; break; }
                            if (!repetida) faltan.Add(raiz);
                        }
                        else cr++;
                        continue;
                    }
                    if (isNow) playing = it; else q.Add(it);
                }
                if (faltan.Count > 0)
                {
                    stateRestored = false;
                    stateMissingRoots = faltan;
                    stateRootRetryTick = Environment.TickCount;
                    lblStatus.Text = "Esperando el disco de musica para restaurar creditos y cola...";
                    return;
                }
                if (cr <= 0 && playing == null && q.Count == 0) return;   // estado vacio: arranque limpio
                credits = Math.Max(0, cr);
                UpdateCredits();
                queue.AddRange(q); RefrescaCola(); PrecalientaSiguiente();
                if (playing != null)
                {
                    queue.Insert(0, playing);   // lo que estaba sonando vuelve a entrar y arranca DESDE EL PRINCIPIO
                    RefrescaCola();
                    PlayNext();
                    ShowToast("Reanudando la musica");
                }
                else UpdateNowPlayingUI();
            }
            catch { stateRestored = false; }
        }

        // ===================== MOTOR DE ESTADISTICAS =====================
        private string LibStatsFile() { return RutaDatos("rocola_stats.txt"); }

        // Registra una reproduccion pedida (algo que entro a la cola con exito).
        private void RecordPlay(PlayItem it)
        {
            if (it == null) return;
            statsTotal++;
            if (it.IsVideo)
            {
                statsVideoTotal++;
                string vp = it.VideoPath;
                if (!string.IsNullOrEmpty(vp)) { int n; videoCounts.TryGetValue(vp, out n); videoCounts[vp] = n + 1; videoSeq[vp] = ++statsSeq; }
            }
            else if (it.Song != null && !string.IsNullOrEmpty(it.Song.Path))
            {
                statsSongTotal++;
                int n; songCounts.TryGetValue(it.Song.Path, out n); songCounts[it.Song.Path] = n + 1;
                songSeq[it.Song.Path] = ++statsSeq;
                if (!string.IsNullOrEmpty(it.Song.Genre) && !songGenre.ContainsKey(it.Song.Path)) songGenre[it.Song.Path] = GenreLabel(it.Song.Genre);
                string ar = it.Song.Artist;
                if (!string.IsNullOrEmpty(ar)) { int a; artistCounts.TryGetValue(ar, out a); artistCounts[ar] = a + 1; }
                // primera cancion contada -> habilitar la seccion "Lo mas sonado" en la barra de letras
                if (letterAvailable != null && !letterAvailable.Contains(TopMark)) { letterAvailable.Add(TopMark); RecalculaLetras(); }
            }
            statsDirty = true;
            topDirty = true;   // OPTIMIZACION (TOP): cambiaron counts/genero/artista -> reconstruir al entrar a TOP
            CuentaNoche(it);   // y lo mas pedido esta noche, para la atraccion (AtraccionGpu.cs; sin tarjeta, nada)
        }

        private void StatsTimer_Tick(object sender, EventArgs e)
        {
            if (statsDirty) { statsDirty = false; SaveStats(true); }   // guardado periodico en segundo plano
            GuardaNoche(true);   // lo mas pedido esta noche, si cambio (AtraccionGpu.cs)
        }

        // Mantiene el archivo acotado: descarta lo menos sonado (y mas viejo) para dejar entrar lo nuevo.
        private void PruneStats()
        {
            PruneCountDict(songCounts, songSeq, songGenre, SongStatCap, SongStatTarget);
            PruneCountDict(videoCounts, videoSeq, null, VideoStatCap, VideoStatTarget);
            PruneCountDict(artistCounts, null, null, ArtistStatCap, ArtistStatTarget);
            if (songGenre.Count > songCounts.Count)   // limpiar generos huerfanos
            {
                List<string> orphans = new List<string>();
                foreach (string k in songGenre.Keys) if (!songCounts.ContainsKey(k)) orphans.Add(k);
                for (int i = 0; i < orphans.Count; i++) songGenre.Remove(orphans[i]);
            }
        }

        private void PruneCountDict(Dictionary<string, int> counts, Dictionary<string, long> seq, Dictionary<string, string> extra, int cap, int target)
        {
            if (counts.Count <= cap) return;
            List<string> keys = new List<string>(counts.Keys);
            keys.Sort(delegate (string a, string b)
            {
                int ca = counts[a], cb = counts[b];
                if (ca != cb) return ca - cb;              // menos sonado primero (candidato a borrar)
                long sa = 0, sb = 0; if (seq != null) { seq.TryGetValue(a, out sa); seq.TryGetValue(b, out sb); }
                if (sa != sb) return (sa < sb) ? -1 : 1;   // mas viejo primero
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });
            int remove = counts.Count - target;
            for (int i = 0; i < remove && i < keys.Count; i++)
            {
                string k = keys[i];
                counts.Remove(k);
                if (seq != null) seq.Remove(k);
                if (extra != null) extra.Remove(k);
            }
        }

        // Claves ordenadas por recencia (mas viejo primero): al guardarse asi, al recargar se reconstruye
        // el orden de recencia leyendo el archivo de arriba hacia abajo.
        private List<string> KeysBySeq(Dictionary<string, int> counts, Dictionary<string, long> seq)
        {
            List<string> ks = new List<string>(counts.Keys);
            ks.Sort(delegate (string a, string b)
            {
                long sa = 0, sb = 0; seq.TryGetValue(a, out sa); seq.TryGetValue(b, out sb);
                if (sa != sb) return (sa < sb) ? -1 : 1;
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });
            return ks;
        }

        private readonly object statsSaveLock = new object();
        private long statsSaveVersion;
        private void SaveStats(bool async)
        {
            PruneStats();   // acotar antes de escribir
            string text;
            try { text = BuildStatsText(); } catch { return; }   // se arma en el hilo de la interfaz (sin acceso cruzado a los diccionarios)
            // Este archivo tiene DOS escritores -uno de fondo cada tanto y otro sincrono al cerrar- y si se
            // pisaban se llevaban por delante "LO MAS SONADO" entero. El candado por archivo esta dentro de
            // EscribeSeguro, asi que aqui ya no hace falta acordarse de nada.
            long version = System.Threading.Interlocked.Increment(ref statsSaveVersion);
            System.Action guardar = delegate
            {
                try
                {
                    lock (statsSaveLock)
                    {
                        if (version != System.Threading.Interlocked.Read(ref statsSaveVersion)) return;
                        EscribeSeguro(LibStatsFile(), text);
                    }
                }
                catch { }
            };
            if (async) GuardaEnFondo(guardar); else guardar();   // al cerrar: sincrono y descarta instantaneas viejas
        }

        private string BuildStatsText()
        {
            StringBuilder sb = new StringBuilder(songCounts.Count * 48 + 256);
            sb.Append("ROCOLA-STATS v1\r\n");
            sb.Append("total-reproducciones=").Append(statsTotal).Append("\r\n");
            sb.Append("total-canciones=").Append(statsSongTotal).Append("\r\n");
            sb.Append("total-videos=").Append(statsVideoTotal).Append("\r\n");
            sb.Append("monedas=").Append(statsCoins).Append("\r\n");
            sb.Append("creditos-cargados=").Append(statsCreditsAdded).Append("\r\n");
            sb.Append("[CANCIONES] count|ruta\r\n");
            List<string> sk = KeysBySeq(songCounts, songSeq);
            for (int i = 0; i < sk.Count; i++) sb.Append(songCounts[sk[i]]).Append('|').Append(sk[i]).Append("\r\n");
            sb.Append("[ARTISTAS] count|artista\r\n");
            foreach (KeyValuePair<string, int> kv in artistCounts) sb.Append(kv.Value).Append('|').Append(kv.Key).Append("\r\n");
            sb.Append("[VIDEOS] count|ruta\r\n");
            List<string> vk = KeysBySeq(videoCounts, videoSeq);
            for (int i = 0; i < vk.Count; i++) sb.Append(videoCounts[vk[i]]).Append('|').Append(vk[i]).Append("\r\n");
            sb.Append("[GENEROS] genero|ruta\r\n");
            foreach (KeyValuePair<string, string> kv in songGenre) sb.Append(kv.Value).Append('|').Append(kv.Key).Append("\r\n");
            return sb.ToString();
        }

        // El historial se prepara en privado, sin array de todas las lineas ni acceso al formulario.
        private sealed class StartupStats
        {
            public readonly Dictionary<string, int> songCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, long> songSeq = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, int> artistCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, int> videoCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, long> videoSeq = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, string> songGenre = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public long statsTotal, statsSongTotal, statsVideoTotal, statsCoins, statsCreditsAdded, statsSeq;
        }

        private static StartupStats ReadStatsRecords(TextReader reader)
        {
            if (reader.ReadLine() != "ROCOLA-STATS v1") return null;
            StartupStats data = new StartupStats();
            int section = 0;   // 0=totales 1=canciones 2=artistas 3=videos
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0) continue;
                if (line.StartsWith("[CANCIONES]")) { section = 1; continue; }
                if (line.StartsWith("[ARTISTAS]")) { section = 2; continue; }
                if (line.StartsWith("[VIDEOS]")) { section = 3; continue; }
                if (line.StartsWith("[GENEROS]")) { section = 4; continue; }
                if (section == 0)
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq); long v;
                    if (!long.TryParse(line.Substring(eq + 1), out v)) continue;
                    if (k == "total-reproducciones") data.statsTotal = v;
                    else if (k == "total-canciones") data.statsSongTotal = v;
                    else if (k == "total-videos") data.statsVideoTotal = v;
                    else if (k == "monedas") data.statsCoins = v;
                    else if (k == "creditos-cargados") data.statsCreditsAdded = v;
                }
                else
                {
                    int b = line.IndexOf('|');
                    if (b <= 0) continue;
                    string key = line.Substring(b + 1);
                    if (key.Length == 0) continue;
                    if (section == 4) { data.songGenre[key] = line.Substring(0, b); continue; }   // genero|ruta (lo de antes del | es la etiqueta, no un numero)
                    int cnt; if (!int.TryParse(line.Substring(0, b), out cnt)) continue;
                    if (section == 1) { data.songCounts[key] = cnt; data.songSeq[key] = ++data.statsSeq; }
                    else if (section == 2) data.artistCounts[key] = cnt;
                    else if (section == 3) { data.videoCounts[key] = cnt; data.videoSeq[key] = ++data.statsSeq; }
                }
            }
            return data;
        }

        // Solo se llama en UI, antes de abrir la biblioteca y de restaurar la cola.
        private void PublishStartupStats(StartupStats data)
        {
            if (data == null) return;
            songCounts = data.songCounts;
            songSeq = data.songSeq;
            artistCounts = data.artistCounts;
            videoCounts = data.videoCounts;
            videoSeq = data.videoSeq;
            songGenre = data.songGenre;
            statsTotal = data.statsTotal;
            statsSongTotal = data.statsSongTotal;
            statsVideoTotal = data.statsVideoTotal;
            statsCoins = data.statsCoins;
            statsCreditsAdded = data.statsCreditsAdded;
            statsSeq = data.statsSeq;
        }

        private void LoadStats()
        {
            // Fallback del arranque si no se pudo crear/completar el lector.
            try
            {
                using (StreamReader reader = new StreamReader(LibStatsFile()))
                    PublishStartupStats(ReadStatsRecords(reader));
            }
            catch { }   // nada se publica si la lectura se corta antes de EOF
        }

        private class TopEntry { public Track T; public int C; public Album A; public string V; }

        // Construye el disco virtual "Lo mas sonado" con las canciones MAS PEDIDAS (de las estadisticas),
        // ordenadas de mas a menos. Se arma al entrar a la seccion, asi siempre refleja lo actual.
        // Construye los discos virtuales "TOP - <genero>" con las canciones mas pedidas de cada genero.
        private void BuildTopDiscs()
        {
            if (!topDirty && topDiscs != null) return;   // OPTIMIZACION (TOP): nada cambio -> reusar lo ya armado
            if (libBuilding) return;                     // el worker esta mutando albums/videoAlbums: iterarlos seria una carrera (crash). topDirty queda puesto -> se reconstruye al terminar
            topDirty = false;                            // limpio AQUI: cubre todas las salidas (incluidas las tempranas)
            topDiscs = new List<Album>();
            if (songCounts.Count == 0) return;

            // 1) recolectar todas las canciones pedidas que EXISTEN en la biblioteca, con su disco de origen
            List<TopEntry> all = new List<TopEntry>();
            for (int i = 0; i < albums.Count; i++)
            {
                Album al = albums[i];
                for (int j = 0; j < al.Tracks.Count; j++)
                {
                    Track t = al.Tracks[j];
                    int c;
                    if (t != null && t.Path != null && songCounts.TryGetValue(t.Path, out c))
                    { TopEntry te = new TopEntry(); te.T = t; te.C = c; te.A = al; all.Add(te); }
                }
            }
            if (all.Count == 0) return;

            // 2) agrupar por etiqueta de genero (las que no tengan genero capturado caen en "Otros")
            Dictionary<string, List<TopEntry>> byGenre = new Dictionary<string, List<TopEntry>>();
            List<string> order = new List<string>();
            for (int i = 0; i < all.Count; i++)
            {
                string gl;
                if (!songGenre.TryGetValue(all[i].T.Path, out gl) || string.IsNullOrEmpty(gl)) gl = "Otros";
                List<TopEntry> lst;
                if (!byGenre.TryGetValue(gl, out lst)) { lst = new List<TopEntry>(); byGenre[gl] = lst; order.Add(gl); }
                lst.Add(all[i]);
            }

            // 3) un disco virtual por genero
            List<Album> discs = new List<Album>();
            Dictionary<Album, long> totals = new Dictionary<Album, long>();
            for (int gi = 0; gi < order.Count; gi++)
            {
                string gl = order[gi];
                List<TopEntry> lst = byGenre[gl];
                lst.Sort(delegate (TopEntry x, TopEntry y)
                {
                    if (y.C != x.C) return y.C - x.C;
                    return string.Compare(x.T.Path, y.T.Path, StringComparison.OrdinalIgnoreCase);
                });
                int take = (lst.Count < TopMax) ? lst.Count : TopMax;
                Album d = new Album();
                d.IsTop = true; d.Letter = TopMark; d.Folder = "::TOP::" + gl;
                d.Artist = "TOP 100"; d.Name = gl;             // AlbumLabel -> "TOP 100 - Rock"
                d.Tracks = new List<Track>();
                d.TrackCovers = new List<string>();
                d.TrackInfo = new List<string>();
                d.TrackAlbums = new List<Album>();
                List<string> cycle = new List<string>();
                long total = 0;
                for (int i = 0; i < take; i++)
                {
                    TopEntry te = lst[i];
                    d.Tracks.Add(te.T);
                    d.TrackCovers.Add(te.A.CoverPath);
                    d.TrackInfo.Add(AlbumLabel(te.A));
                    d.TrackAlbums.Add(te.A);
                    if (te.A.CoverPath != null && !cycle.Contains(te.A.CoverPath)) cycle.Add(te.A.CoverPath);
                    total += te.C;
                }
                d.CycleCovers = cycle;
                d.CoverPath = (cycle.Count > 0) ? cycle[0] : null;
                totals[d] = total;
                discs.Add(d);
            }

            // 3a) discos TOP por ARTISTA muy puesto (hasta 10), RANKEADOS: "TOP #1 - <artista>" ... "TOP #10 - ..."
            {
                Dictionary<string, List<TopEntry>> byArtist = new Dictionary<string, List<TopEntry>>(StringComparer.OrdinalIgnoreCase);
                List<string> aOrder = new List<string>();
                for (int i = 0; i < all.Count; i++)
                {
                    string ar = all[i].T.Artist;
                    if (string.IsNullOrEmpty(ar)) continue;
                    List<TopEntry> lst;
                    if (!byArtist.TryGetValue(ar, out lst)) { lst = new List<TopEntry>(); byArtist[ar] = lst; aOrder.Add(ar); }
                    lst.Add(all[i]);
                }
                // candidatos: artistas con >=2 canciones distintas puestas (un solo tema no amerita disco)
                List<string> cand = new List<string>();
                Dictionary<string, long> aTot = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < aOrder.Count; i++)
                {
                    string ar = aOrder[i];
                    List<TopEntry> lst = byArtist[ar];
                    if (lst.Count < 2) continue;
                    long t = 0; for (int j = 0; j < lst.Count; j++) t += lst[j].C;
                    aTot[ar] = t; cand.Add(ar);
                }
                cand.Sort(delegate (string a, string b)
                {
                    long ta = aTot[a], tb = aTot[b];
                    if (tb != ta) return (tb > ta) ? 1 : -1;
                    return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                });
                int amax = (cand.Count < 10) ? cand.Count : 10;   // hasta 10 artistas mas puestos
                for (int ai = 0; ai < amax; ai++)
                {
                    string ar = cand[ai];
                    List<TopEntry> lst = byArtist[ar];
                    lst.Sort(delegate (TopEntry x, TopEntry y)
                    {
                        if (y.C != x.C) return y.C - x.C;
                        return string.Compare(x.T.Path, y.T.Path, StringComparison.OrdinalIgnoreCase);
                    });
                    int take = (lst.Count < TopMax) ? lst.Count : TopMax;
                    Album d = new Album();
                    // RANKING VISIBLE: el puesto (#1..#10, por total de reproducciones) va en la etiqueta. Tambien
                    // va en Folder (la CLAVE de la celda horneada): si un artista cambia de puesto, su etiqueta se
                    // re-hornea; con la clave vieja se habria quedado el numero anterior pegado en el cache.
                    d.IsTop = true; d.Letter = TopMark; d.Folder = "::TOPART::" + (ai + 1) + "|" + ar;
                    d.Artist = "TOP #" + (ai + 1); d.Name = ar;             // AlbumLabel -> "TOP #1 - Depeche Mode"
                    d.Tracks = new List<Track>();
                    d.TrackCovers = new List<string>();
                    d.TrackInfo = new List<string>();
                    d.TrackAlbums = new List<Album>();
                    List<string> cyc = new List<string>();
                    for (int i = 0; i < take; i++)
                    {
                        TopEntry te = lst[i];
                        d.Tracks.Add(te.T);
                        d.TrackCovers.Add(te.A.CoverPath);
                        d.TrackInfo.Add(AlbumLabel(te.A));
                        d.TrackAlbums.Add(te.A);
                        if (te.A.CoverPath != null && !cyc.Contains(te.A.CoverPath)) cyc.Add(te.A.CoverPath);
                    }
                    d.CycleCovers = cyc;
                    d.CoverPath = (cyc.Count > 0) ? cyc[0] : null;
                    totals[d] = aTot[ar];
                    discs.Add(d);
                }
            }

            // 3b) disco TOP de VIDEOS (de videoCounts, recorriendo los discos con video)
            if (videoCounts.Count > 0 && videoAlbums != null)
            {
                List<TopEntry> vids = new List<TopEntry>();
                for (int i = 0; i < videoAlbums.Count; i++)
                {
                    Album va = videoAlbums[i];
                    for (int j = 0; j < va.VideoFiles.Count; j++)
                    {
                        string vf = va.VideoFiles[j];
                        int c;
                        if (vf != null && videoCounts.TryGetValue(vf, out c))
                        { TopEntry te = new TopEntry(); te.V = vf; te.C = c; te.A = va; vids.Add(te); }
                    }
                }
                if (vids.Count > 0)
                {
                    vids.Sort(delegate (TopEntry x, TopEntry y)
                    {
                        if (y.C != x.C) return y.C - x.C;
                        return string.Compare(x.V, y.V, StringComparison.OrdinalIgnoreCase);
                    });
                    int vtake = (vids.Count < TopMax) ? vids.Count : TopMax;
                    Album vd = new Album();
                    vd.IsTop = true; vd.TopIsVideo = true; vd.Letter = TopMark; vd.Folder = "::TOPVID::";
                    vd.Artist = "TOP 100"; vd.Name = "Videos";
                    vd.VideoFiles = new List<string>();
                    vd.TrackCovers = new List<string>();
                    vd.TrackInfo = new List<string>();
                    vd.TrackAlbums = new List<Album>();
                    List<string> vcyc = new List<string>();
                    long vtotal = 0;
                    for (int i = 0; i < vtake; i++)
                    {
                        TopEntry te = vids[i];
                        vd.VideoFiles.Add(te.V);
                        vd.TrackCovers.Add(te.A.CoverPath);
                        vd.TrackInfo.Add(AlbumLabel(te.A));
                        vd.TrackAlbums.Add(te.A);
                        if (te.A.CoverPath != null && !vcyc.Contains(te.A.CoverPath)) vcyc.Add(te.A.CoverPath);
                        vtotal += te.C;
                    }
                    vd.CycleCovers = vcyc;
                    vd.CoverPath = (vcyc.Count > 0) ? vcyc[0] : null;
                    totals[vd] = vtotal;
                    discs.Add(vd);
                }
            }

            // 4) ordenar por CLASE (genero primero, luego videos, luego artistas) y dentro de
            //    cada clase por total de reproducciones. Asi quedan agrupados y comodos de ver.
            discs.Sort(delegate (Album x, Album y)
            {
                int cx = x.TopIsVideo ? 1 : ((x.Folder != null && x.Folder.StartsWith("::TOPART::", StringComparison.Ordinal)) ? 2 : 0);
                int cy = y.TopIsVideo ? 1 : ((y.Folder != null && y.Folder.StartsWith("::TOPART::", StringComparison.Ordinal)) ? 2 : 0);
                if (cx != cy) return cx - cy;
                long tx = totals[x], ty = totals[y];
                if (ty != tx) return (ty > tx) ? 1 : -1;
                return string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
            });
            topDiscs = discs;
        }

        private int pastCredAncho;   // el ancho con el que se acomodo la capsula de creditos la ultima vez

        private void UpdateCredits()
        {
            if (pastCred == null) return;
            if (credits <= 0) { pastCred.Etiqueta = "Inserta una moneda"; pastCred.Valor = ""; pastCred.Acento = Color.FromArgb(255, 205, 90); }
            else { pastCred.Etiqueta = "Creditos"; pastCred.Valor = credits.ToString(); pastCred.Acento = Color.FromArgb(120, 240, 140); }
            // Y SI AHORA PIDE OTRO ANCHO, HAY QUE VOLVER A ACOMODAR.
            //
            // Los dos letreros miden cosas muy distintas -"Creditos 1" y "Inserta una moneda" se llevan mas
            // de cien pixeles- y el ancho de la capsula se calcula al acomodar la pantalla, no aqui. Al
            // gastar el ultimo credito el texto pasaba al largo DENTRO de la capsula medida para el corto, y
            // se cortaba a media palabra. Se veia en la rocola del bar: "Inserta una mon".
            //
            // Solo si CAMBIO: de "Creditos 1" a "Creditos 2" mide lo mismo, y rehacer el acomodo de la
            // pantalla en cada moneda seria trabajo tirado veinte veces en una noche movida.
            if (pastCred.Height >= 8)
            {
                int quiere = PastillaView.Ancho(pastCred.Etiqueta, pastCred.Valor, pastCred.Height, this.Font.FontFamily, pastCred.ConPortadas);
                if (quiere != pastCredAncho) { pastCredAncho = quiere; LayoutControls(); }
            }
            pastCred.Invalidate();
        }

        // Reproduce el siguiente elemento (cancion o video). Tambien sirve para SALTAR el actual.
        private void PlayNext()
        {
            DisposePlaybackNow();      // detiene cualquier cancion (se desuscribe antes; no re-dispara)
            StopVideoPlayback();       // detiene cualquier video

            if (queue.Count == 0)
            {
                nowPlaying = null;
                RefrescaCola();   // que se borre la marca de "SONANDO" de la lista del disco
                progress.Value = 0; lblTime.Text = "0:00 / 0:00";
                ApplyGenreBackground(null, null);
                LayoutControls(); this.Invalidate();
                UpdateNowPlayingUI();
                SaveState();   // estado vivo: ya no suena nada (los creditos que queden se conservan)
                return;
            }

            nowPlaying = queue[0];
            queue.RemoveAt(0); RefrescaCola();   // (nowPlaying ya esta puesto: la marca de "SONANDO" sale en la misma pasada)
            SaveState();   // estado vivo: cambio lo que suena / la cola (para reanudar tras un corte de luz)

            if (nowPlaying.IsVideo)
            {
                bgAccentTarget = DefaultAccent;       // el video no tiene genero
                bgGeneroTarget = DefaultAccent; bgPort1 = SinColor; bgPort2 = SinColor; bgSinGenero = false; bgArtistaFijo = false;   // ni caratula que aporte
                StartVideoItem(nowPlaying.VideoPath);
            }
            else
            {
                nowPlaying.Song.EnsureTags();         // lee ID3 ahora (genero -> color de fondo)
                // Se apunta SOLO si el archivo trae etiqueta de verdad. Antes se apuntaba siempre, y en una
                // cancion sin etiqueta eso guardaba "Otros" y PISABA lo que la cascada habia deducido: la
                // pastilla decia "Pop" en la lista y al ponerla cambiaba a "Otros". Ese era el fallo.
                if (nowPlaying.Song.Path != null && !string.IsNullOrEmpty(nowPlaying.Song.Genre))
                { songGenre[nowPlaying.Song.Path] = GenreLabel(nowPlaying.Song.Genre); statsDirty = true; topDirty = true; }   // captura genero para los discos TOP
                ApplyGenreBackground(nowPlaying.Song, nowPlaying.Disc);
                PlayTrack(nowPlaying.Song);
            }
            LayoutControls(); this.Invalidate();
            UpdateNowPlayingUI();
        }

        // SALTAR: salta el elemento actual (cancion o video). Los CREDITOS son LO ULTIMO de la fila: solo se
        // borran cuando ya no queda NADA sonando NI en cola (asi SALTAR nunca borra dinero mientras haya musica
        // pendiente). Con todo vacio, UNA pulsacion de SALTAR borra los creditos (para cerrar el negocio).
        private void SkipSong()
        {
            if (nowPlaying != null || queue.Count > 0) { PlayNext(); return; }   // salta (o reanuda la cola si quedo detenida); PlayNext guarda el estado
            if (credits > 0)
            {
                credits = 0; UpdateCredits(); SaveState();
                ShowToast("Creditos borrados", Color.FromArgb(200, 130, 35));
            }
        }

        private void GuardaTodoAlCerrar()
        {
            if (startupReadPending) return;   // no sobrescribir datos anteriores con tablas aun vacias
            try
            {
                System.Threading.Thread[] th = preScanTh;
                if (th != null) for (int i = 0; i < th.Length; i++)
                    if (th[i] != null && th[i].IsAlive) th[i].Join(1200);
            }
            catch { }
            try { if (durScanTh != null && durScanTh.IsAlive) durScanTh.Join(1200); } catch { }

            try { SaveLoudCache(); } catch { }
            try { SaveIntegCache(); } catch { }
            try { FlushEqIfDirty(); } catch { }
            try { SaveIaDone(); } catch { }
            try { SaveDeepDone(); } catch { }
            try { SaveDurFast(); } catch { }
            try { if (statsDirty) { statsDirty = false; SaveStats(false); } } catch { }
            try { GuardaNoche(false); } catch { }   // y lo mas pedido esta noche (AtraccionGpu.cs), antes de esperar a los guardados
            EsperaGuardados();
            try { SaveIaDone(); SaveDeepDone(); SaveDurFast(); } catch { }
            EsperaGuardados();
        }
    }
}
