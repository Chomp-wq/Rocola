// LA ROCOLA (Rocola OS, etapa 2): lo que hace con cada boton y el sonido, sin el dibujo (la vista: VistaTexto en la consola, o
// VistaGrafica por KMS y OpenGL ES). Los discos por letra; ARRIBA/ABAJO mueven, IZQUIERDA/DERECHA cambian de letra
// (o de pagina dentro de un disco), PONER entra al disco o pide la cancion (cuesta un credito), REGRESAR sale del disco, MONEDA
// suma 4 creditos y SALTAR salta la que suena. Las reglas (creditos, cola, estado, relevo sin pausa, lo aprendido) son las del motor,
// que son las de XP. Los botones llegan por evdev (Botones.cs, rocola_teclas.txt de XP) y, para probar, tambien por el teclado de la
// terminal. Todo el motor se usa desde un solo hilo (como la pantalla de XP); el sonido va en el suyo (SalidaAlsa).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    internal interface IVista : IDisposable
    {
        void Dibuja(Controlador c);
        bool LeeTerminal { get; }   // la vista de texto lee tambien el teclado de la terminal
        bool Anima { get; }         // dibuja cosas que se mueven (el cielo): el bucle la llama mas a menudo
        void Suelta();              // ceder la pantalla a un video
        void Recupera();            // y recuperarla
    }

    internal sealed partial class Controlador
    {
        internal readonly Form1 motor;
        private readonly string dispositivo;
        private readonly BlockingCollection<int> pulsos = new BlockingCollection<int>();   // codigos de tecla de Windows (0: salir)
        private Botones botones;
        // EL ASISTENTE DE BOTONES (el de XP: StartConfig, CaptureConfigKey y FinishConfig): sin rocola_teclas.txt, o con F8 (si F8 no es un
        // boton de la rocola), se pide cada boton por turno; cada uno distinto; al acabar se guarda rocola_teclas.txt en el formato de XP.
        internal static readonly string[] PasosAsistente = {
            "ARRIBA   (subir / pagina anterior)", "ABAJO   (bajar / pagina siguiente)", "IZQUIERDA   (disco anterior)",
            "DERECHA   (disco siguiente)", "PONER / ACEPTAR   (elegir, poner cancion)", "REGRESAR   (atras / salir)",
            "MONEDA   (credito)", "SALTAR   (saltar la cancion actual)" };
        private static readonly string[] ClavesAsistente = { "arriba", "abajo", "izquierda", "derecha", "poner", "regresar", "moneda", "saltar" };
        internal int AsistentePaso = -1;              // -1: no hay asistente
        internal string AsistenteAviso = "";
        private readonly int[] capturas = new int[8];
        internal List<Form1.Disco> discos;
        internal readonly List<char> letras = new List<char>();
        internal readonly Dictionary<char, List<Form1.Disco>> porLetra = new Dictionary<char, List<Form1.Disco>>();
        internal int letra, cursorDisco, cursorCancion;
        internal Form1.Disco dentro;            // el disco abierto (null: la lista de discos)
        private string aviso = ""; private int avisoHasta;
        // LO QUE LA PANTALLA VIVA ANIMA (la vista mira la hora de cada suceso): la moneda que cae, la tarjeta que invita, el boleto que vuela
        // y el aviso que entra.
        internal int MonedaTick, MonedaSuma, InvitaTick, VueloTick, AvisoDesde;
        internal string InvitaTitulo = "", VueloTitulo = "";
        internal bool VueloASuena, VueloDesdeBuscador;
        private SalidaAlsa salida;
        private bool saltar, salir;
        internal int PorPagina = 20;           // canciones que caben en una pagina de la vista (IZQ/DER dentro de un disco)
        internal string AvisoVigente { get { return unchecked(Environment.TickCount - avisoHasta) < 0 ? aviso : ""; } }
        internal float Progreso { get { return motor.SonandoProgreso; } }
        internal long CuadrosOidos { get { SalidaAlsa sa = salida; return sa != null ? sa.CuadrosOidos : -1; } }
        internal long PorDelante { get { SalidaAlsa sa = salida; return sa != null ? sa.PorDelante : 0; } }   // (las letras: lo que aun no se oye)   // para el pulso de la pantalla

        internal Controlador(Form1 motor, string dispositivo) { this.motor = motor; this.dispositivo = dispositivo; }

        // La biblioteca por letras y lo de antes del apagon.
        internal void Prepara()
        {
            motor.Aviso += delegate (string t) { aviso = t; avisoHasta = Environment.TickCount + motor.AvisoVidaMs; AvisoDesde = Environment.TickCount | 1; };
            discos = motor.LeeBiblioteca();
            foreach (Form1.Disco d in discos)
            {
                List<Form1.Disco> l;
                if (!porLetra.TryGetValue(d.Letra, out l)) { l = new List<Form1.Disco>(); porLetra[d.Letra] = l; letras.Add(d.Letra); }
                l.Add(d);
            }
            foreach (KeyValuePair<char, List<Form1.Disco>> kv in porLetra) localDeLetra[kv.Key] = new List<Form1.Disco>(kv.Value);   // (P1: lo tuyo, sin Spotify)
            motor.Reanuda();   // los creditos y la cola de antes del apagon (mismo dia del negocio)
            RefrescaTop();
            foreach (Form1.Disco d in discos) if (!d.Videos) TotalCanciones += d.Rutas.Count;
            int repaso = motor.RepasaBiblioteca();   // lo que falta medir, poco a poco y con prioridad baja (el repaso de XP)
            if (repaso > 0) Console.WriteLine("Repaso: " + repaso + " canciones por medir");
        }
        // Un boton, como si se hubiera pulsado (las pruebas).
        internal void Pulsa(Boton b) { Maneja(b); TrasBoton(); }
        // Una tecla (codigo de Windows), como si llegara de los botones (las pruebas).
        internal void PulsaTecla(int vk) { Tecla(vk); }
        internal void EscribeDePrueba(string t) { foreach (char ch in t.ToUpperInvariant()) Tecla(DeTexto - ch); }

        internal void EmpiezaAsistente() { AsistentePaso = 0; AsistenteAviso = ""; }

        // UN GUION DE BOTONES (pruebas: --guion archivo): una linea por paso: un boton (moneda, poner...), "regresar+derecha" (con REGRESAR
        // mantenido), "espera 2000" (ms) o "salir". Se mete en la misma fila que los botones de verdad.
        internal string Guion;
        private const int DeGuion = -1000;
        private const int DeTexto = -100000;   // una letra para el buscador (pruebas)
        private const int Atasca = -1000000;   // el bucle parado (pruebas del vigilante: solo desde un guion)
        private const int ConRegresar = 0x10000;   // la tecla llego con REGRESAR apretado
        private const int ConCtrl = 0x20000;       // ... o con Ctrl (Ctrl+F3: los ajustes del dueno, como en XP)
        private void CorreGuion()
        {
            foreach (string l0 in File.ReadAllLines(Guion))
            {
                string l = l0.Trim().ToLowerInvariant(); if (l.Length == 0 || l.StartsWith("#")) continue;
                if (l.StartsWith("espera ")) { Thread.Sleep(int.Parse(l.Substring(7))); continue; }
                if (l == "salir") { pulsos.Add(0); return; }
                if (l.StartsWith("atasca ")) { pulsos.Add(Atasca - int.Parse(l.Substring(7))); continue; }   // el bucle se queda parado N ms (el vigilante)
                if (l == "ctrl+f3") { pulsos.Add(0x72 | ConCtrl); Thread.Sleep(150); continue; }
                if (l.StartsWith("escribe ")) { foreach (char ch in l.Substring(8).ToUpperInvariant()) pulsos.Add(DeTexto - ch); Thread.Sleep(150); continue; }   // (en el buscador abierto)
                bool con = l.StartsWith("regresar+"); if (con) l = l.Substring(9);
                Boton b = (Boton)Enum.Parse(typeof(Boton), l, true);
                pulsos.Add(DeGuion - (int)b * 2 - (con ? 1 : 0));
                Thread.Sleep(150);
            }
        }

        private void Tecla(int vk)
        {
            if (vk <= Atasca) { Thread.Sleep(Atasca - vk); return; }
            if (DiagEscena >= 0) { AcabaDiagnostico("se toco un boton"); ultimoToque = Environment.TickCount; return; }   // cualquier boton corta el diagnostico
            bool enReposo = Reposo;
            ultimoToque = Environment.TickCount;
            if (Atraccion || enReposo)
            {
                Atraccion = false;   // la despierta (y al reposo: vuelve la pared); la moneda ademas cuenta (en XP la moneda cuenta siempre)
                Boton f = botones != null ? botones.FuncionDe(vk & 0xFFFF) : Boton.Ninguno;
                if (f != Boton.Moneda) return;
            }
            if (vk <= DeTexto) { if (Buscando) Escribe((char)(DeTexto - vk)); return; }
            if (vk <= DeGuion) { int x = DeGuion - vk; Pulsa((Boton)(x / 2), (x & 1) != 0); return; }
            if ((vk & ConCtrl) != 0)
            {
                vk &= ~ConCtrl;
                if ((vk & 0xFFFF) == 0x72 && AsistentePaso < 0) { AbreAjustes(); return; }   // Ctrl+F3
            }
            if ((vk & ConRegresar) != 0)
            {
                vk &= ~ConRegresar;
                if (AsistentePaso < 0 && botones != null && botones.FuncionDe(vk) != Boton.Ninguno) { Pulsa(botones.FuncionDe(vk), true); return; }
            }
            if (AsistentePaso >= 0) { CapturaAsistente(vk); return; }
            if (vk == 0x77 && (botones == null || botones.FuncionDe(vk) == Boton.Ninguno)) { EmpiezaAsistente(); return; }   // F8, como en XP
            Boton b = botones != null ? botones.FuncionDe(vk) : Boton.Ninguno;
            if (b != Boton.Ninguno) Maneja(b);
            TrasBoton();
        }
        // Tras cada boton, la guia de la ayuda mira ya si se avanzo (ademas de cada vuelta del bucle).
        private void TrasBoton() { if (AyudaAbierta && AyudaMision != 0) EvaluaGuia(); }
        private void CapturaAsistente(int vk)
        {
            if (vk == 0 || vk == 0x10 || vk == 0x11 || vk == 0x12) return;   // nada, Mayus, Ctrl o Alt sueltas (como XP)
            for (int j = 0; j < AsistentePaso; j++)
                if (capturas[j] == vk) { AsistenteAviso = "Ese boton ya esta usado. Presiona OTRO."; return; }
            capturas[AsistentePaso++] = vk; AsistenteAviso = "";
            if (AsistentePaso < PasosAsistente.Length) return;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 8; i++) sb.Append(ClavesAsistente[i]).Append('=').Append(capturas[i]).Append("\r\n");
            // de un tiron y con copia (.bak): un apagon al guardar no la deja a medias
            try { Form1.GuardaSeguro(motor.ArchivoBotones, sb.ToString()); } catch (Exception ex) { Aviso("No se pudo guardar: " + ex.Message); }
            try { string c = motor.CopiaBotones; if (c != null) Form1.GuardaSeguro(c, sb.ToString()); } catch { }   // y fuera de lo aprendido
            if (botones != null) botones.Configura(motor.ArchivoBotones);
            AsistentePaso = -1;
            Aviso("¡Botones configurados!");
        }

        // El aviso de apagar (SIGTERM: el boton de encendido, por acpid): se sale del bucle y es un cierre deliberado.
        internal volatile bool Apagando;
        internal void Apaga() { Apagando = true; salir = true; pulsos.Add(0); }

        // EL MODO ATRACCION (el de XP: AtraccionGpu.cs, en lo esencial): nada sonando, cola vacia y 40 s sin tocarla -> un escaparate de
        // discos (uno cada 8 s, al azar) y los mensajes de XP. Cualquier boton la quita (y la MONEDA, ademas, cuenta).
        internal bool Atraccion;
        internal int AtraccionDisco; internal Form1.Disco AtraccionActual;
        private int ultimoToque = Environment.TickCount, atraccionCambio;
        private readonly Random azar = new Random();
        internal int TotalCanciones;
        private void RevisaAtraccion()
        {
            bool sola = motor.Sonando == null && motor.EnCola == 0 && video == null && AsistentePaso < 0 && AjusteSel < 0 && !Buscando;
            if (!Atraccion && sola && unchecked(Environment.TickCount - ultimoToque) > 40000 && discos != null && discos.Count > 0)
            { Atraccion = true; atraccionCambio = Environment.TickCount - 8000; }
            if (Atraccion && !sola) Atraccion = false;
            if (Atraccion && unchecked(Environment.TickCount - atraccionCambio) >= 8000)
            {
                atraccionCambio = Environment.TickCount;
                List<Form1.Disco> l = discos.FindAll(x => !x.Videos);
                if (l.Count > 0) { AtraccionActual = l[azar.Next(l.Count)]; AtraccionDisco++; }
            }
        }

        // EL REPOSO (muro_reposo= y estado_expandido= de XP): algo sonando, 15 s sin botones, en la pared y sin hojas abiertas -> la pared se
        // aparta y lo que suena crece (el estado extendido). El primer boton solo la despierta (la moneda, ademas, cuenta).
        internal bool Reposo
        {
            get
            {
                return motor.MuroReposo && motor.Sonando != null && video == null && AsistentePaso < 0 && AjusteSel < 0 && !Buscando
                    && dentro == null && !Atraccion && unchecked(Environment.TickCount - ultimoToque) >= 15000;
            }
        }
        internal void FuerzaReposo() { ultimoToque = Environment.TickCount - 20000; }   // (pruebas)

        internal double AtraccionDesfase;   // (pruebas) segundos ya corridos del ciclo de 30 s de la atraccion
        internal void FuerzaAtraccion() { ultimoToque = Environment.TickCount - 60000; RevisaAtraccion(); }   // (pruebas)

        private int fallos;
        internal int Corre(IVista vista)
        {
            vistaActual = vista;
            VistaGrafica vg = vista as VistaGrafica; if (vg != null) vg.Presentacion("Leyendo tu musica...");   // V29
            Prepara();
            using (Botones botones = new Botones())
            {
                botones.Configura(motor.ArchivoBotones, motor.CopiaBotones);
                this.botones = botones;
                botones.PulsadoConRegresar += delegate (Boton b, int vk, bool conRegresar)
                { if (vk != 0) pulsos.Add(vk | (conRegresar ? ConRegresar : 0) | (botones.TeclaApretada(0x11) ? ConCtrl : 0)); };
                if (!botones.DeArchivo) EmpiezaAsistente();   // sin rocola_teclas.txt: el asistente (como XP la primera vez)
                int n = botones.Escucha();
                Aviso((botones.DeArchivo ? "Botones de rocola_teclas.txt" : "Botones de teclado (sin rocola_teclas.txt)") + "; " + n + " aparatos de entrada");
                // El teclado de la terminal: solo si no hay aparatos de entrada (si los hay, el teclado ya llega por ahi y contaria dos veces).
                if (vista.LeeTerminal && !Console.IsInputRedirected)
                {
                    bool despacha = n == 0;
                    Thread teclado = new Thread(delegate () { LeeTeclado(botones, despacha); }) { IsBackground = true, Name = "teclado de la terminal" };
                    teclado.Start();
                }
                if (Guion != null) { Thread g = new Thread(CorreGuion) { IsBackground = true, Name = "guion" }; g.Start(); }
                latido = Environment.TickCount;
                new Thread(Vigila) { IsBackground = true, Name = "vigila el bucle" }.Start();
                try
                {
                    while (!salir)
                    {
                        latido = Environment.TickCount;
                        // UN FALLO NO TUMBA LA ROCOLA (como XP): cada vuelta va protegida; el error se apunta en rocola_error.txt y se sigue.
                        try
                        {
                            // Todas las pulsaciones pendientes antes de dibujar (si un cuadro tarda, los botones no se quedan esperando en fila).
                            int vk; bool fin = false;
                            if (pulsos.TryTake(out vk, vista.Anima && video == null ? 0 : 50))   // con la pantalla, el ritmo lo pone el barrido (Presenta espera)
                                do { if (vk == 0) { fin = true; break; } motor.HuboBoton(); Tecla(vk); } while (pulsos.TryTake(out vk, 0));
                            if (fin) break;
                            motor.PublicaMedidas();
                            ActualizaCodigo();
                            RevisaAtraccion();
                            RevisaLoQueSuena();
                            RevisaAyuda();
                            RevisaDiagnostico();
                            RevisaActualizacion();
                            motor.AvanzaModoNoche();   // de 11 a 7, el volumen al % del modo noche (con su rampa: ModoNoche.cs)
                            NocheAlVideo();
                            motor.Periodico();   // el reinicio de las 7 (si esta ociosa) y lo aprendido al disco, una vez por minuto
                            Suena();
                            if (video == null) vista.Dibuja(this);   // mientras suena un video, la pantalla es suya
                        }
                        catch (Exception ex)
                        {
                            fallos++;
                            motor.ApuntaError("La rocola (vuelta del bucle)", ex);
                            if (fallos > 200) throw;   // algo roto de verdad: que el vigilante la vuelva a abrir
                            Thread.Sleep(100);
                        }
                    }
                }
                finally
                {
                    // LA COLA SE GUARDA ANTES DE CERRAR LA SALIDA (4-11-2026): AcaboLaSalida da por acabada la que suena (y recoge el relevo
                    // de la siguiente), asi que guardar despues perdia lo que sonaba y lo que esperaba, ya pagado, al cerrar sin el boton
                    // de encendido (la actualizacion que reinicia, el vigilante). Lo que sonaba vuelve a empezar desde el principio, como
                    // tras un apagon.
                    if (!Apagando) { motor.GuardaEstado(); motor.CongelaEstado(); }
                    if (salida != null) { CierraSuave(salida); salida = null; motor.AcaboLaSalida(); }   // al apagar, sin golpe (SalidaAlsa.Apaga)
                    if (video != null) { try { video.Kill(); } catch { } TerminaVideo(); }
                    if (Apagando) motor.CierreDelDia();   // el boton de encendido: como el cierre de XP, manana arranca limpia
                }
            }
            return CodigoSalida;
        }

        // EL VIGILANTE DEL BUCLE (4-11-2026). Dibujar, poner la siguiente cancion y atender los botones van en el mismo bucle; si algo lo
        // deja parado (la tarjeta grafica colgada dentro de su controlador, un disco que no responde), la cancion que suena acaba (va en
        // su propio hilo) pero ya no entra otra ni responden los botones: la rocola muerta hasta que alguien la reinicie. Si el bucle no
        // da una vuelta en BucleMaxMs, se apunta y el programa se cierra a proposito: /usr/bin/rocola lo vuelve a abrir a los 2 s, con
        // los creditos y la cola guardados (lo que sonaba vuelve a empezar). Nada de esto toca un bucle que funciona.
        private volatile int latido;
        internal static int BucleMaxMs = 60000;
        private void Vigila()
        {
            int m; if (int.TryParse(Environment.GetEnvironmentVariable("ROCOLA_BUCLE_MAX_MS"), out m) && m >= 1000) BucleMaxMs = m;   // (para probarlo)
            while (!salir)
            {
                Thread.Sleep(1000);
                int parado = unchecked(Environment.TickCount - latido);
                if (parado < BucleMaxMs || salir) continue;
                string t = "El bucle de la rocola lleva " + parado / 1000 + " s parado: se cierra para que vuelva a abrirse";
                try { Console.Error.WriteLine(t); } catch { }
                try { File.AppendAllText(Path.Combine(Path.GetDirectoryName(motor.ArchivoBotones), "rocola_error.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [" + t + "]\r\n\r\n"); } catch { }
                try { System.Diagnostics.Process.GetCurrentProcess().Kill(); } catch { Environment.Exit(70); }
            }
        }

        private void Aviso(string t) { aviso = t; avisoHasta = Environment.TickCount + motor.AvisoVidaMs; AvisoDesde = Environment.TickCount | 1; }
        internal int AvisoDuraMs { get { return motor.AvisoVidaMs; } }

        // El teclado de la terminal: sus codigos de tecla son los de Windows (ConsoleKey), asi que valen las mismas funciones. Ctrl+Q: salir.
        private void LeeTeclado(Botones botones, bool despacha)
        {
            while (!salir)
            {
                ConsoleKeyInfo k;
                try { k = Console.ReadKey(true); } catch { return; }
                if (k.Key == ConsoleKey.Q && (k.Modifiers & ConsoleModifiers.Control) != 0) { salir = true; pulsos.Add(0); return; }
                if (!despacha) continue;   // (solo para que la terminal no escriba lo que se teclea)
                if (k.Key == ConsoleKey.F3 && (k.Modifiers & ConsoleModifiers.Control) != 0) { pulsos.Add(0x72 | ConCtrl); continue; }
                pulsos.Add((int)k.Key);   // el codigo de ConsoleKey es el de tecla de Windows
            }
        }

        // LO MAS SONADO: la "letra" TOP (la estrella de XP), la primera, con un disco por genero; aparece cuando hay estadisticas.
        internal static string NombreLetra(char l) { return l == Form1.LetraTop ? "TOP" : l == Form1.LetraVideos ? "VIDEOS" : l == Form1.LetraSpotify ? "SPOTIFY" : l.ToString(); }
        // LA LETRA SPOTIFY (Motor/Spotify.cs): despues de TOP, con tus favoritas, listas y discos; se pone al dia cuando cambian (y se quita al
        // apagar el interruptor). Dentro de ella no se mueve lo que se mira: solo cambia lo que hay (las portadas llegan solas).
        private int versionSpotify = -1, repasoLocalMs;
        private void RevisaSpotify()
        {
            int v = motor.VersionSpotify;
            int ahoraR = Environment.TickCount;
            if (v == versionSpotify)
            {   // nada nuevo; solo el repaso de tu biblioteca que quedo pendiente, cuando le toque
                if (repasoPendiente && unchecked(ahoraR - repasoLocalMs) >= 5000) RepasoLocal(ahoraR);
                return;
            }
            versionSpotify = v;
            // SP5 y SP6 (Motor/SpotifyMas.cs): las portadas que llegaron para tus discos sin caratula, y lo que le falta a cada disco. Recorre
            // TODA la biblioteca: como mucho cada 5 s (mientras se emparejan, cambia cada 2,5 s), y siempre al final (repasoPendiente).
            if (repasoLocalMs == 0 || unchecked(ahoraR - repasoLocalMs) >= 5000) RepasoLocal(ahoraR); else repasoPendiente = true;
            List<Form1.Disco> l = motor.DiscosSpotify();
            char actual = letras.Count > 0 ? letras[letra] : '\0';
            if (l.Count == 0) { letras.Remove(Form1.LetraSpotify); porLetra.Remove(Form1.LetraSpotify); if (dentro != null && Form1.EsRutaSpotify(dentro.Carpeta) && !motor.SpotifyOn) dentro = null; }
            else
            {
                if (!letras.Contains(Form1.LetraSpotify)) letras.Insert(letras.Count > 0 && letras[0] == Form1.LetraTop ? 1 : 0, Form1.LetraSpotify);
                porLetra[Form1.LetraSpotify] = l;
            }
            int i = letras.IndexOf(actual);
            letra = i >= 0 ? i : Math.Min(letra, Math.Max(0, letras.Count - 1));
            if (letras.Count > 0 && cursorDisco >= porLetra[letras[letra]].Count) cursorDisco = 0;
            if (Buscando && Resultados != null) resultadosSpotifyDe = null;   // las portadas de los resultados llegaron: se vuelven a mirar
        }
        private bool repasoPendiente;
        private void RepasoLocal(int ahora)
        {
            repasoLocalMs = ahora | 1; repasoPendiente = false;
            if (discos == null) return;
            foreach (Form1.Disco dd in discos)
            {
                if (dd.Videos) continue;
                if (string.IsNullOrEmpty(dd.Portada)) { string p = motor.PortadaSpotifyDe(dd.Carpeta); if (p != null) dd.Portada = p; }
                PonFaltantes(dd, motor.FaltanSabidas(dd));
            }
        }
        // (Pruebas: --boton spotify en una captura) unos segundos atendiendo Spotify como el bucle de la rocola.
        internal void EsperaSpotify(IVista v, int ms)
        {
            int t0 = Environment.TickCount;
            while (unchecked(Environment.TickCount - t0) < ms) { motor.AtiendeSpotify(); RevisaSpotify(); RevisaLetrasSpotify(); RevisaMas(); RevisaBusquedaSpotify(); v.Dibuja(this); System.Threading.Thread.Sleep(100); }
        }
        // P1 (Motor/SpotifyPortadas.cs): los discos de Spotify en las letras, junto a los del mismo artista (o en su sitio por el nombre). Se
        // rehace cuando el motor trae discos nuevos; lo que se mira se queda elegido (el mismo disco, aunque cambie de sitio).
        private readonly Dictionary<char, List<Form1.Disco>> localDeLetra = new Dictionary<char, List<Form1.Disco>>();
        private readonly HashSet<char> letrasConSpotify = new HashSet<char>();
        private int versionLetrasSp, letrasRehechas;
        // OPTIMIZACION (7-11-2026): lo que va en las letras se calcula en otro hilo (compara toda la biblioteca con lo de Spotify) y aqui solo
        // se mezcla, en una pasada por letra (antes se insertaba uno por uno normalizando la letra entera cada vez: cuadratico, en el hilo
        // que dibuja; en una letra con cientos de discos, un tiron).
        private System.Threading.Tasks.Task<List<Form1.DiscoEnLetra>> letrasCalculando;
        private int letrasCalculandoV;
        private void RevisaLetrasSpotify()
        {
            if (letrasCalculando != null)
            {
                if (!letrasCalculando.IsCompleted) return;
                List<Form1.DiscoEnLetra> listo = letrasCalculando.Status == System.Threading.Tasks.TaskStatus.RanToCompletion ? letrasCalculando.Result : null;
                int vl = letrasCalculandoV; letrasCalculando = null;
                if (listo != null && motor.VersionLetrasSpotify != 0) { MezclaLetras(listo); versionLetrasSp = vl; }   // (si mientras tanto se apago, no se pone)
                return;
            }
            int v = motor.VersionLetrasSpotify;
            if (v == versionLetrasSp) return;
            // mientras llegan discos (uno cada pocos segundos), la pared se reacomoda como mucho cada 15 s; apagarlo se ve al momento
            int ahora = Environment.TickCount;
            if (v != 0 && versionLetrasSp != 0 && letrasRehechas != 0 && unchecked(ahora - letrasRehechas) < 15000) return;
            letrasRehechas = ahora | 1;
            if (v == 0) { MezclaLetras(new List<Form1.DiscoEnLetra>()); versionLetrasSp = 0; return; }
            letrasCalculandoV = v;
            letrasCalculando = System.Threading.Tasks.Task.Run(() => motor.DiscosSpotifyPorLetra());
        }
        private void MezclaLetras(List<Form1.DiscoEnLetra> nuevos)
        {
            char actual = letras.Count > 0 ? letras[letra] : '\0';
            Form1.Disco foco = letras.Count > 0 && cursorDisco < porLetra[actual].Count ? porLetra[actual][cursorDisco] : null;
            // primero, cada letra como era (solo lo tuyo)
            foreach (char l in letrasConSpotify)
            {
                List<Form1.Disco> base_;
                if (localDeLetra.TryGetValue(l, out base_)) porLetra[l] = new List<Form1.Disco>(base_);
                else { porLetra.Remove(l); letras.Remove(l); }
            }
            letrasConSpotify.Clear();
            // por letra, lo de Spotify agrupado por artista (en el orden en que llega: los mas conocidos primero)
            Dictionary<char, Dictionary<string, List<Form1.Disco>>> porArtista = new Dictionary<char, Dictionary<string, List<Form1.Disco>>>();
            foreach (Form1.DiscoEnLetra x in nuevos)
            {
                if (!porArtista.TryGetValue(x.Letra, out Dictionary<string, List<Form1.Disco>> g)) { g = new Dictionary<string, List<Form1.Disco>>(StringComparer.Ordinal); porArtista[x.Letra] = g; }
                string k = Form1.ClaveArtista(x.JuntoA);
                if (!g.TryGetValue(k, out List<Form1.Disco> l)) { l = new List<Form1.Disco>(); g[k] = l; }
                l.Add(x.D);
            }
            foreach (KeyValuePair<char, Dictionary<string, List<Form1.Disco>>> kv in porArtista)
            {
                List<Form1.Disco> locales;
                if (!porLetra.TryGetValue(kv.Key, out locales))
                {   // una letra que no tenias: en su sitio (antes de la primera mayor, o de VIDEOS)
                    locales = new List<Form1.Disco>(); porLetra[kv.Key] = locales;
                    int i = 0;
                    while (i < letras.Count && (letras[i] == Form1.LetraTop || letras[i] == Form1.LetraSpotify || (letras[i] != Form1.LetraVideos && letras[i] < kv.Key))) i++;
                    letras.Insert(i, kv.Key);
                }
                letrasConSpotify.Add(kv.Key);
                // los artistas de Spotify sin discos tuyos en esta letra, en orden de su clave
                List<string> solos = new List<string>();
                HashSet<string> conLocal = new HashSet<string>(StringComparer.Ordinal);
                foreach (Form1.Disco d in locales) conLocal.Add(Form1.ClaveDe(d));
                foreach (string k in kv.Value.Keys) if (!conLocal.Contains(k)) solos.Add(k);
                solos.Sort(StringComparer.Ordinal);
                List<Form1.Disco> r = new List<Form1.Disco>(locales.Count + nuevos.Count);
                int si = 0;
                for (int i = 0; i < locales.Count; i++)
                {
                    string k = Form1.ClaveDe(locales[i]);
                    while (si < solos.Count && string.CompareOrdinal(solos[si], k) < 0) { r.AddRange(kv.Value[solos[si]]); si++; }   // los que van antes por nombre
                    r.Add(locales[i]);
                    bool ultimoDelArtista = i + 1 == locales.Count || Form1.ClaveDe(locales[i + 1]) != k;
                    if (ultimoDelArtista && kv.Value.TryGetValue(k, out List<Form1.Disco> suyos)) r.AddRange(suyos);   // detras de los tuyos
                }
                while (si < solos.Count) { r.AddRange(kv.Value[solos[si]]); si++; }
                porLetra[kv.Key] = r;
            }
            int li = letras.IndexOf(actual);
            letra = li >= 0 ? li : Math.Min(letra, Math.Max(0, letras.Count - 1));
            if (letras.Count > 0)
            {
                int j = foco != null ? porLetra[letras[letra]].IndexOf(foco) : -1;
                if (j >= 0) cursorDisco = j; else if (cursorDisco >= porLetra[letras[letra]].Count) cursorDisco = 0;
            }
        }

        // P2: LA TIRA «MAS DE ESTE ARTISTA» (dentro de un disco). cursorMas: -1 en la lista de canciones; si no, el disco elegido de la tira.
        // Los discos abiertos desde la tira se apilan: REGRESAR vuelve al de antes.
        internal List<Form1.Disco> MasDisco;
        internal string MasArtista;
        internal int cursorMas = -1;
        private readonly Stack<Form1.Disco> deDonde = new Stack<Form1.Disco>();
        private Form1.Disco masDe;
        private int masMirado;
        private void RevisaMas()
        {
            if (dentro == null) { MasDisco = null; masDe = null; cursorMas = -1; deDonde.Clear(); if (!Buscando) volverAlBuscador = false; return; }
            int ahora = Environment.TickCount;
            if (object.ReferenceEquals(masDe, dentro) && masMirado != 0 && unchecked(ahora - masMirado) < 1500) return;
            if (!object.ReferenceEquals(masDe, dentro)) cursorMas = -1;
            masDe = dentro; masMirado = ahora | 1;
            string artista;
            List<Form1.Disco> l = motor.MasDelArtista(dentro, out artista);
            MasDisco = l; MasArtista = artista;
            if (l == null) cursorMas = -1; else if (cursorMas >= l.Count) cursorMas = l.Count - 1;
        }
        private void AbreDeLaTira()
        {
            List<Form1.Disco> l = MasDisco;
            if (l == null || cursorMas < 0 || cursorMas >= l.Count) return;
            deDonde.Push(dentro);
            dentro = l[cursorMas]; cursorCancion = 0; cursorMas = -1; masDe = null;
            motor.CargaSpotify(dentro);
        }
        private void SaleDelDisco()
        {
            cursorMas = -1; masDe = null;
            if (deDonde.Count > 0) { dentro = deDonde.Pop(); cursorCancion = 0; }
            else
            {
                dentro = null;
                if (volverAlBuscador) { volverAlBuscador = false; Buscando = true; }   // (abierto desde el buscador y sin sitio en la pared: de vuelta al buscador)
            }
        }
        private bool volverAlBuscador;   // el disco abierto vino del buscador y no esta en la pared

        // SP6: al final del disco, las canciones de Spotify que le faltan (o ninguna: se quitan las que habia, si se apago).
        private void PonFaltantes(Form1.Disco dd, List<string> faltan)
        {
            int propias = dd.Rutas.Count;
            while (propias > 0 && Form1.EsRutaSpotify(dd.Rutas[propias - 1])) propias--;
            int ya = dd.Rutas.Count - propias, nuevas = faltan != null ? faltan.Count : 0;
            if (ya == nuevas && (nuevas == 0 || dd.Rutas[propias] == faltan[0])) return;
            List<string> rutas = dd.Rutas.GetRange(0, propias), titulos = dd.Titulos.GetRange(0, Math.Min(propias, dd.Titulos.Count));
            if (faltan != null) foreach (string r in faltan) { rutas.Add(r); titulos.Add(motor.TituloSpotify(r)); }
            dd.Titulos = titulos; dd.Rutas = rutas;
            if (object.ReferenceEquals(dentro, dd) && cursorCancion >= rutas.Count) cursorCancion = Math.Max(0, rutas.Count - 1);
        }
        // Los resultados de Spotify, detras de los de la biblioteca, en cuanto llegan (el motor busca cuando se deja de escribir).
        private List<Form1.Resultado> resultadosLocales, resultadosMezclados;
        private string resultadosSpotifyDe;
        private void RevisaBusquedaSpotify()
        {
            if (!Buscando || resultadosLocales == null || Form1.EsCodigoDeServicio(Escrito)) return;
            // solo sobre lo que se busco escribiendo (no sobre «Lo mas pedido hoy» ni otro atajo)
            if (!object.ReferenceEquals(Resultados, resultadosLocales) && !object.ReferenceEquals(Resultados, resultadosMezclados)) return;
            if (resultadosSpotifyDe == Escrito) return;
            List<Form1.Resultado> sp = motor.ResultadosSpotify(Escrito);
            if (sp == null) return;
            List<Form1.Resultado> todo = new List<Form1.Resultado>(resultadosLocales);
            todo.AddRange(sp);
            Resultados = resultadosMezclados = todo; resultadosSpotifyDe = Escrito;
        }
        private void RefrescaTop()
        {
            List<Form1.Disco> top = motor.LoMasSonado();
            char actual = letras.Count > 0 ? letras[letra] : '\0';
            letras.Remove(Form1.LetraTop); porLetra.Remove(Form1.LetraTop);
            if (top.Count > 0) { letras.Insert(0, Form1.LetraTop); porLetra[Form1.LetraTop] = top; }
            int i = letras.IndexOf(actual);
            if (i >= 0) letra = i; else { letra = Math.Min(letra, Math.Max(0, letras.Count - 1)); }
            if (letras.Count > 0 && cursorDisco >= porLetra[letras[letra]].Count) cursorDisco = 0;
        }

        // EL ROJO MANTENIDO (para las pistas del rojo de XP): lo dicen los botones (evdev); en las pruebas, --boton rojo.
        internal bool RojoDePrueba;
        internal bool RojoMantenido { get { return RojoDePrueba || (botones != null && botones.Apretado(Boton.Regresar)); } }
        internal int SentidoPagina = 1;   // la ultima flecha de pagina: +1 ARRIBA (la siguiente), -1 ABAJO (la anterior); para deslizar
        internal int DiscosEn(int i) { return i >= 0 && i < letras.Count ? porLetra[letras[i]].Count : 0; }
        // Las canciones (o videos) de una letra: el numero que XP pone bajo cada letra (letterBar.Counts).
        internal int CancionesEn(int i)
        {
            if (i < 0 || i >= letras.Count) return 0;
            int n = 0; foreach (Form1.Disco x in porLetra[letras[i]]) n += x.Rutas.Count;
            return n;
        }
        internal List<Form1.Disco> DiscosDeLetra() { return letras.Count == 0 ? new List<Form1.Disco>() : porLetra[letras[letra]]; }

        internal const int DiscosPorPagina = 18;   // seis columnas y tres filas, como la pared de XP
        // REGRESAR mantenido (para los combos de XP); en las pruebas se da por boton.
        private bool regresarDePrueba;
        private bool RegresarApretado { get { return regresarDePrueba; } }   // lo que estaba apretado al pulsar (PulsadoConRegresar)
        internal void Pulsa(Boton b, bool conRegresar) { regresarDePrueba = conRegresar; try { Maneja(b); } finally { regresarDePrueba = false; } TrasBoton(); }

        // LOS BOTONES, como en XP (Form1.cs: ProcessCmdKey). MONEDA y SALTAR siempre. REGRESAR mantenido + ARRIBA abre o cierra el buscador.
        // En la pared: IZQ/DER, de disco; ARRIBA, pagina siguiente; ABAJO, la anterior; REGRESAR mantenido + IZQ/DER, de letra; PONER, entrar.
        // Dentro de un disco: ARRIBA/ABAJO, de cancion; IZQ/DER, de pagina; PONER, pedirla; REGRESAR, a la pared.
        private void Maneja(Boton b)
        {
            switch (b)
            {
                case Boton.Moneda:
                    {
                        int antes = motor.Creditos; motor.Moneda(); MonedaAyuda();
                        if (motor.Creditos > antes) { MonedaSuma = motor.Creditos - antes; MonedaTick = Environment.TickCount | 1; InvitaTick = 0; }   // LA MONEDA QUE CAE (XP)
                        return;
                    }
                case Boton.Saltar:   // como SkipSong de XP: salta la que suena; con todo parado y sin cola, borra los creditos que quedaron
                    if (salida != null || video != null) saltar = true;
                    else if (motor.BorraCreditos()) Aviso("Creditos borrados");
                    return;
            }
            if (video != null) return;   // durante un video los demas botones no hacen nada (la pantalla es del video)
            if (AjusteSel >= 0) { ManejaAjustes(b); return; }
            if (b == Boton.Abajo && RegresarApretado) { if (AyudaAbierta) CierraAyuda(); else AbreAyuda(); return; }   // ROJO + ABAJO: la ayuda (Ayuda.cs)
            if (BotonAyuda(b)) return;   // el menu de la ayuda se queda con el boton; guiando, hace lo suyo de siempre
            if (b == Boton.Arriba && RegresarApretado) { if (Buscando) CierraBuscador(); else AbreBuscador(); return; }
            if (Buscando) { ManejaBuscador(b); return; }
            if (dentro == null)
            {
                List<Form1.Disco> l = DiscosDeLetra();
                if (l.Count == 0) return;
                if (b == Boton.Izquierda && RegresarApretado) { letra = (letra + letras.Count - 1) % letras.Count; cursorDisco = 0; }
                else if (b == Boton.Derecha && RegresarApretado) { letra = (letra + 1) % letras.Count; cursorDisco = 0; }
                else if (b == Boton.Izquierda) cursorDisco = (cursorDisco + l.Count - 1) % l.Count;
                else if (b == Boton.Derecha) cursorDisco = (cursorDisco + 1) % l.Count;
                else if (b == Boton.Arriba && !RegresarApretado) { SentidoPagina = 1; int p = cursorDisco / DiscosPorPagina + 1; if (p * DiscosPorPagina >= l.Count) p = 0; cursorDisco = p * DiscosPorPagina; }
                else if (b == Boton.Abajo && !RegresarApretado) { SentidoPagina = -1; int paginas = (l.Count + DiscosPorPagina - 1) / DiscosPorPagina, p = cursorDisco / DiscosPorPagina - 1; if (p < 0) p = paginas - 1; cursorDisco = p * DiscosPorPagina; }
                else if (b == Boton.Poner) { dentro = l[cursorDisco]; cursorCancion = 0; cursorMas = -1; deDonde.Clear(); volverAlBuscador = false; motor.CargaSpotify(dentro); motor.FaltanEnDisco(dentro); }   // (SP6: lo que le falta llega en un momento)   // (uno de Spotify: sus canciones llegan en un momento)
            }
            else
            {
                int n = dentro.Rutas.Count, pagina = Math.Max(1, PorPagina);
                List<Form1.Disco> mas = MasDisco; int nm = mas != null ? mas.Count : 0;
                if (b == Boton.Regresar) { SaleDelDisco(); return; }
                if (cursorMas >= 0 && nm > 0)
                {   // EN LA TIRA (P2): IZQ/DER y ABAJO pasan de disco; ARRIBA vuelve a las canciones; PONER lo abre
                    if (b == Boton.Izquierda) cursorMas = (cursorMas + nm - 1) % nm;
                    else if (b == Boton.Derecha) cursorMas = (cursorMas + 1) % nm;
                    else if (b == Boton.Abajo && !RegresarApretado) { if (cursorMas + 1 < nm) cursorMas++; else { cursorMas = -1; cursorCancion = 0; } }
                    else if (b == Boton.Arriba && !RegresarApretado) { cursorMas = -1; cursorCancion = Math.Max(0, n - 1); }
                    else if (b == Boton.Poner) AbreDeLaTira();
                    return;
                }
                cursorMas = -1;
                if (n == 0) { if (nm > 0 && (b == Boton.Abajo || b == Boton.Arriba) && !RegresarApretado) cursorMas = 0; return; }   // (un disco de Spotify que aun carga)
                if (b == Boton.Arriba && !RegresarApretado) { if (cursorCancion == 0 && nm > 0) cursorMas = nm - 1; else cursorCancion = (cursorCancion + n - 1) % n; }
                else if (b == Boton.Abajo && !RegresarApretado) { if (cursorCancion == n - 1 && nm > 0) cursorMas = 0; else cursorCancion = (cursorCancion + 1) % n; }
                else if (b == Boton.Izquierda) cursorCancion = Math.Max(0, cursorCancion - pagina);
                else if (b == Boton.Derecha) cursorCancion = Math.Min(n - 1, cursorCancion + pagina);
                else if (b == Boton.Poner) PideCancion(dentro.Rutas[cursorCancion], dentro.Titulos[cursorCancion]);
            }
        }

        private void PideCancion(string ruta, string titulo)
        {
            if (!Form1.EsRutaSpotify(ruta)) motor.MideEnFondo(ruta);   // como el repaso de XP, en segundo plano: hasta tenerla, suena con la regla de las no medidas
            bool sonaba = motor.Sonando != null || motor.SonandoVideo || motor.EnCola > 0;
            Form1.Pedido p = Form1.EsRutaSpotify(ruta) ? motor.PideSpotify(ruta) : motor.Pide(ruta);   // las de Spotify, con su credito como todo
            if (p == Form1.Pedido.SinCredito && motor.SinCreditosInvita)
            {   // SIN CREDITOS, INVITAR (XP): una tarjeta amable en vez del letrero
                aviso = ""; InvitaTitulo = titulo; InvitaTick = Environment.TickCount | 1;
            }
            if (p == Form1.Pedido.Puesta)
            {
                NotaPedidoAyuda();
                if (motor.VueloCola) { VueloTick = Environment.TickCount | 1; VueloASuena = !sonaba; VueloDesdeBuscador = Buscando; VueloTitulo = titulo; }   // VER ADONDE VA (XP)
                Aviso(motor.UltimoTurno.Length > 0 ? motor.UltimoTurno : "Pusiste «" + titulo + "»");   // el turno de XP
                if (letras.Count == 0 || letras[letra] != Form1.LetraTop) RefrescaTop();   // (dentro del TOP no: se moveria lo que se mira)
            }
            else if (p == Form1.Pedido.Repetida) Aviso("Esa cancion ya va seguida");
            else if (p == Form1.Pedido.Danada) Aviso("Esa cancion esta danada: no se cobra");
        }

        // ---- LOS AJUSTES DEL DUENO (Ctrl+F3, como en XP): ARRIBA/ABAJO eligen, IZQ/DER o PONER cambian, REGRESAR guarda y sale.
        internal int AjusteSel = -1;   // -1: cerrados
        internal IList<Form1.Ajuste> ListaAjustes { get { return motor.Ajustes(); } }
        internal int AjustesTotal { get { return ListaAjustes.Count + 1; } }   // y al final: configurar los botones
        internal void AbreAjustes() { AjusteSel = 0; Buscando = false; ultimoLoQueSuena = 0; RevisaLoQueSuena(); }
        // S5 de XP (LoQueSuena.cs): lo que la cadena le hace a la que suena, cada medio segundo con la hoja abierta (sus lecturas se ponen a
        // cero al leerlas: este es su unico lector).
        internal string LoQueSuena = "", RecuadroTitulo = "LO QUE SUENA AHORA";
        private bool ciegoVisto;   // tras una comparacion, su resultado se queda a la vista mientras la hoja siga abierta
        private int ultimoLoQueSuena;
        private void RevisaLoQueSuena()
        {
            if (AjusteSel < 0) return;
            int ahora = Environment.TickCount;
            if (ultimoLoQueSuena != 0 && unchecked(ahora - ultimoLoQueSuena) < 500) return;
            ultimoLoQueSuena = ahora == 0 ? 1 : ahora;
            motor.CiegoTick();   // la comparacion a ciegas cambia de tramo (como el reloj de la ventana de XP)
            string grupo = AjusteSel >= 0 && AjusteSel < ListaAjustes.Count ? ListaAjustes[AjusteSel].Grupo : "";
            if (grupo == "Spotify")
            {
                RecuadroTitulo = "SPOTIFY (REUNIONES FAMILIARES)";
                LoQueSuena = motor.TextoSpotify();
                return;
            }
            if (grupo == "Internet")
            {
                RecuadroTitulo = "INTERNET Y ACTUALIZACIONES";
                LoQueSuena = TextoInternet();
                return;
            }
            if (grupo == "Afinar con el celular")
            {
                RecuadroTitulo = "AFINAR EL LOCAL CON EL CELULAR";
                string av = motor.AvisoAfinar;
                try { LoQueSuena = (av.Length > 0 ? av + "\n" : "") + motor.TextoAfinar(); } catch (Exception ex) { LoQueSuena = ""; motor.ApuntaError("Afinar el local", ex); }
                return;
            }
            if (grupo == "El local" && (motor.ProbandoBocinas || motor.AvisoAfinar.Length > 0))
            {
                RecuadroTitulo = "PRUEBA DE BOCINAS";
                LoQueSuena = motor.ProbandoBocinas ? motor.EstadoPrueba() + "\nPolaridad: si «al reves» suena con MAS grave que «igual», una bocina esta al reves." : motor.AvisoAfinar;
                return;
            }
            RecuadroTitulo = "LO QUE SUENA AHORA";
            try { LoQueSuena = motor.TextoLoQueSuena(); string ciego = motor.TextoCiego(); if (motor.CiegoActivo || ciegoVisto) { LoQueSuena += "\n" + ciego; ciegoVisto = true; } } catch (Exception ex) { LoQueSuena = ""; motor.ApuntaError("Lo que suena ahora", ex); }
        }
        private void CierraAjustes() { AjusteSel = -1; ciegoVisto = false; motor.ParaPrueba(); motor.CortaCiego("Ventana cerrada: comparacion cortada. "); if (motor.SinMejorasQuedan() > 0) motor.PonSinMejoras(false); }   // como XP al cerrar la ventana
        private void ManejaAjustes(Boton b)
        {
            int n = AjustesTotal;
            if (b == Boton.Arriba) { AjusteSel = (AjusteSel + n - 1) % n; ultimoLoQueSuena = 0; RevisaLoQueSuena(); }
            else if (b == Boton.Abajo) { AjusteSel = (AjusteSel + 1) % n; ultimoLoQueSuena = 0; RevisaLoQueSuena(); }
            else if (b == Boton.Regresar)
            {
                motor.CortaCiego("Ventana cerrada: comparacion cortada. ");   // antes de guardar: vuelve el modo de antes
                try { motor.GuardaAjustes(); Aviso("Ajustes guardados: se oyen desde la siguiente cancion"); } catch (Exception ex) { Aviso("No se pudo guardar: " + ex.Message); }
                CierraAjustes();
            }
            else if (AjusteSel == n - 1) { if (b == Boton.Poner) { CierraAjustes(); EmpiezaAsistente(); } }
            else if (b == Boton.Izquierda || b == Boton.Derecha || b == Boton.Poner)
            {
                Form1.Ajuste a = ListaAjustes[AjusteSel];
                if (a.Motivo != null) Aviso("No se puede: " + a.Motivo);
                else
                {
                    if (!a.Herramienta) motor.CortaCiego("Cambiaste un ajuste: comparacion cortada. ");   // primero vuelve el modo como estaba (XP)
                    a.Mueve(b == Boton.Izquierda ? -1 : 1);
                    if (a.Herramienta) ciegoVisto = true;
                    ultimoLoQueSuena = 0; RevisaLoQueSuena();
                }
            }
        }

        // ---- EL BUSCADOR (el de XP: teclado en pantalla, resultados a la derecha; REGRESAR borra una letra) ----
        internal static readonly string[] FilasTeclado = { "1234567890", "QWERTYUIOP", "ASDFGHJKL\u00D1", "ZXCVBNM", "\u0001\u0002" };   // la ultima: ESPACIO y BORRAR
        internal bool Buscando;
        internal string Escrito = "";
        internal int FilaTecla = 1, ColTecla, SelResultado;
        internal bool EnResultados;
        internal List<Form1.Resultado> Resultados = new List<Form1.Resultado>();
        internal static string Etiqueta(char t) { return t == '\u0001' ? "ESPACIO" : t == '\u0002' ? "BORRAR" : t < 256 ? (letrasSueltas[t] ??= t.ToString()) : t.ToString(); }
        private static readonly string[] letrasSueltas = new string[256];   // cada tecla, hecha texto una vez (no en cada cuadro)

        internal void AbreBuscador() { Buscando = true; Escrito = ""; FilaTecla = 1; ColTecla = 0; EnResultados = false; SelResultado = 0; Resultados = new List<Form1.Resultado>(); ArmaAtajos(); }
        // LOS ATAJOS DEL BUSCADOR CON PORTADAS (buscador_portadas= de XP): solo los que tienen algo detras, en este orden: lo mas pedido hoy,
        // los videos y las dos ultimas busquedas con las que alguien eligio algo (solo en memoria, y nunca la clave de servicio). Van en
        // una fila encima del teclado (FilaTecla = -1). (El de Karaoke de XP no: aqui el genero de cada disco no se sabe sin leerlo.)
        internal sealed class Atajo { public string Texto; public int Tipo; public string Busqueda; }   // Tipo: 0 lo mas pedido hoy, 1 videos, 2 buscaste, 3 y 4 de Spotify (SP9)
        internal List<Atajo> Atajos = new List<Atajo>();
        private readonly List<string> buscadas = new List<string>();
        internal bool ConPortadas { get { return motor.BuscadorPortadas; } }
        private void ArmaAtajos()
        {
            Atajos.Clear();
            if (!motor.BuscadorPortadas) return;
            int total; if (motor.TopNoche(out total).Count > 0 || total > 0) Atajos.Add(new Atajo { Texto = "Lo más pedido hoy", Tipo = 0 });
            if (letras.Contains(Form1.LetraVideos)) Atajos.Add(new Atajo { Texto = "Videos", Tipo = 1 });
            if (motor.SpotifyListo && motor.AtajoSpotify(0) != null) Atajos.Add(new Atajo { Texto = "Tus más escuchadas", Tipo = 3 });   // SP9
            if (motor.SpotifyListo && motor.AtajoSpotify(1) != null) Atajos.Add(new Atajo { Texto = "Escuchado hace poco", Tipo = 4 });
            foreach (string b in buscadas) Atajos.Add(new Atajo { Texto = "Buscaste: " + b, Tipo = 2, Busqueda = b });
        }
        private void ApuntaBusqueda()
        {
            string b = Escrito.Trim();
            if (b.Length == 0 || Form1.EsCodigoDeServicio(Escrito)) return;
            buscadas.RemoveAll(x => string.Equals(x, b, StringComparison.OrdinalIgnoreCase));
            buscadas.Insert(0, b); if (buscadas.Count > 2) buscadas.RemoveAt(2);
        }
        private void UsaAtajo(Atajo a)
        {
            if (a.Tipo == 0) { Resultados = motor.MasPedidoHoy(); EnResultados = Resultados.Count > 0; SelResultado = 0; Escrito = ""; }
            else if (a.Tipo == 1) { int i = letras.IndexOf(Form1.LetraVideos); if (i >= 0) { letra = i; cursorDisco = 0; dentro = null; Buscando = false; } }
            else if (a.Tipo == 3 || a.Tipo == 4) { Resultados = motor.AtajoSpotify(a.Tipo - 3) ?? new List<Form1.Resultado>(); EnResultados = Resultados.Count > 0; SelResultado = 0; Escrito = ""; }
            else { Escrito = a.Busqueda; Resultados = motor.Busca(Escrito); SelResultado = 0; EnResultados = Resultados.Count > 0; }
        }
        private void CierraBuscador() { Buscando = false; }
        // El codigo de servicio de XP: escrito y mantenido 5 s, aparece "+4 creditos" como resultado (Tipo 3).
        private int codigoDesde = -1;
        private void ActualizaCodigo()
        {
            if (!Buscando || !Form1.EsCodigoDeServicio(Escrito)) { codigoDesde = -1; return; }
            if (codigoDesde < 0) { codigoDesde = Environment.TickCount; Resultados = new List<Form1.Resultado>(); return; }
            if (unchecked(Environment.TickCount - codigoDesde) >= 5000 && (Resultados.Count == 0 || Resultados[0].Tipo != 3))
            {
                Resultados = new List<Form1.Resultado> { new Form1.Resultado { Tipo = 3, Texto = "+4 créditos" } };
                EnResultados = true; SelResultado = 0;
            }
        }
        private void Escribe(char ch)
        {
            if (ch == '\u0002') { if (Escrito.Length > 0) Escrito = Escrito.Substring(0, Escrito.Length - 1); }
            else Escrito += ch == '\u0001' ? ' ' : ch;
            Resultados = Form1.EsCodigoDeServicio(Escrito) ? new List<Form1.Resultado>() : motor.Busca(Escrito); SelResultado = 0;   // el codigo no busca
            resultadosLocales = Resultados; resultadosSpotifyDe = null;
            if (!Form1.EsCodigoDeServicio(Escrito)) motor.PideBusquedaSpotify(Escrito);   // y en Spotify, si esta encendido (llegan despues)
        }
        private void ManejaBuscador(Boton b)
        {
            if (b == Boton.Regresar) { Escribe('\u0002'); return; }
            if (EnResultados)
            {
                int n = Resultados.Count;
                if (n == 0) { EnResultados = false; return; }
                if (ConPortadas)
                {   // las tarjetas van en dos columnas
                    if (b == Boton.Arriba) SelResultado = SelResultado >= 2 ? SelResultado - 2 : SelResultado;
                    else if (b == Boton.Abajo) SelResultado = Math.Min(n - 1, SelResultado + 2);
                    else if (b == Boton.Derecha) { if (SelResultado % 2 == 0 && SelResultado + 1 < n) SelResultado++; }
                    else if (b == Boton.Izquierda) { if (SelResultado % 2 == 1) SelResultado--; else EnResultados = false; }
                    else if (b == Boton.Poner) { ApuntaBusqueda(); Elige(Resultados[Math.Min(SelResultado, n - 1)]); }
                    return;
                }
                if (b == Boton.Arriba) SelResultado = Math.Max(0, SelResultado - 1);
                else if (b == Boton.Abajo) SelResultado = Math.Min(n - 1, SelResultado + 1);
                else if (b == Boton.Izquierda) EnResultados = false;
                else if (b == Boton.Poner) Elige(Resultados[Math.Min(SelResultado, n - 1)]);
                return;
            }
            if (FilaTecla < 0)
            {   // la fila de los atajos
                if (Atajos.Count == 0) { FilaTecla = 0; return; }
                if (b == Boton.Abajo) { FilaTecla = 0; ColTecla = 0; }
                else if (b == Boton.Izquierda) ColTecla = Math.Max(0, ColTecla - 1);
                else if (b == Boton.Derecha) ColTecla = Math.Min(Atajos.Count - 1, ColTecla + 1);
                else if (b == Boton.Poner) UsaAtajo(Atajos[Math.Min(ColTecla, Atajos.Count - 1)]);
                return;
            }
            string fila = FilasTeclado[FilaTecla];
            if (b == Boton.Arriba && FilaTecla == 0 && Atajos.Count > 0) { FilaTecla = -1; ColTecla = 0; }
            else if (b == Boton.Arriba) { FilaTecla = Math.Max(0, FilaTecla - 1); ColTecla = Math.Min(ColTecla, FilasTeclado[FilaTecla].Length - 1); }
            else if (b == Boton.Abajo) { FilaTecla = Math.Min(FilasTeclado.Length - 1, FilaTecla + 1); ColTecla = Math.Min(ColTecla, FilasTeclado[FilaTecla].Length - 1); }
            else if (b == Boton.Izquierda) ColTecla = Math.Max(0, ColTecla - 1);
            else if (b == Boton.Derecha)
            {
                if (ColTecla >= fila.Length - 1 && Resultados.Count > 0) { EnResultados = true; SelResultado = 0; }   // del borde del teclado a los resultados
                else ColTecla = Math.Min(fila.Length - 1, ColTecla + 1);
            }
            else if (b == Boton.Poner) Escribe(fila[ColTecla]);
        }
        // Elegir un resultado (ChooseSearchResult de XP): una cancion se pide y el buscador sigue abierto; un disco se abre; un artista,
        // se va a su primer disco.
        private void Elige(Form1.Resultado r)
        {
            if (r.Tipo == 3) { if (Form1.EsCodigoDeServicio(Escrito)) { motor.CreditosDeServicio(); Aviso("+4 créditos"); } return; }
            if (r.Tipo == 2 && r.Ruta != null) { PideCancion(r.Ruta, r.Texto); return; }
            if (r.Carpeta == null) return;
            if (Form1.EsRutaSpotify(r.Carpeta))
            {   // un disco o una lista de Spotify del buscador: se abre directo
                Form1.Disco d = motor.DiscoSpotifyDe(r.Carpeta);
                if (d == null) return;
                if (string.IsNullOrEmpty(d.Nombre)) d.Nombre = r.Texto;
                // SI ESTA EN LA PARED (la letra SPOTIFY, o su letra con «Discos de Spotify en las letras»), la pared se queda en el, como
                // con los tuyos, y REGRESAR vuelve a la pared. SI SOLO ESTA EN EL BUSCADOR (pedido del dueno del 7-11-2026), la pared no se
                // mueve y REGRESAR vuelve al buscador, con lo que se habia escrito y la misma eleccion (alli no hay un sitio al que volver).
                bool enPared = false;
                for (int i = 0; i < letras.Count && !enPared; i++)
                {
                    if (letras[i] == Form1.LetraTop) continue;
                    int j = porLetra[letras[i]].IndexOf(d);
                    if (j >= 0) { letra = i; cursorDisco = j; enPared = true; }
                }
                volverAlBuscador = !enPared;
                Buscando = false; dentro = d; cursorCancion = 0; cursorMas = -1; deDonde.Clear();
                return;
            }
            for (int i = 0; i < letras.Count; i++)
            {
                if (letras[i] == Form1.LetraTop) continue;
                List<Form1.Disco> l = porLetra[letras[i]];
                for (int j = 0; j < l.Count; j++)
                    if (l[j].Carpeta == r.Carpeta)
                    {
                        letra = i; cursorDisco = j; Buscando = false;
                        if (r.Tipo == 1) { dentro = l[j]; cursorCancion = 0; } else dentro = null;
                        cursorMas = -1; deDonde.Clear(); volverAlBuscador = false;
                        return;
                    }
            }
        }

        // LOS VIDEOS: con mpv (en Rocola OS, por KMS y ALSA), a pantalla completa y con el volumen de cada video (medido, al de las canciones; si no, video_vol= de XP). La
        // rocola cede la pantalla mientras tanto; MONEDA sigue contando y SALTAR lo corta (como en XP).
        private System.Diagnostics.Process video;
        private StringBuilder errorVideo;   // lo que dijo mpv (si falla, va al aviso y a la salida de la rocola: /root/rocola.txt)
        private IVista vistaActual;
        internal string Mpv = Environment.GetEnvironmentVariable("ROCOLA_MPV") ?? "mpv";
        private void EmpiezaVideo(string ruta)
        {
            try
            {
                if (vistaActual != null) vistaActual.Suelta();
                motor.PausaRepaso = true;   // mpv necesita el procesador
                System.Diagnostics.ProcessStartInfo pi = new System.Diagnostics.ProcessStartInfo(Mpv);
                foreach (string a in new[] { "--fs", "--no-input-default-bindings", "--no-terminal", "--log-file=/tmp/rocola-mpv.txt", "--msg-level=all=warn", "--vo=gpu,drm", "--gpu-context=drm",
                    "--ao=alsa", "--volume=100" })
                    pi.ArgumentList.Add(a);
                // EL VOLUMEN DEL VIDEO (3-11-2026): antes «--volume=85», pero el volumen de mpv es cubico (85 = 0,85^3: -4,2 dB, no los
                // -1,4 dB de XP). Ahora la ganancia va exacta en el limitador de ffmpeg (alimiter: level_in), que ademas para los picos en
                // el techo de la cadena de la rocola (-1,1 dB) si un video flojo se sube; con su retraso compensado (latency), la imagen
                // y el sonido siguen juntos. Medido o no, lo decide el motor (VolumenVideo).
                double g = Math.Max(0.015625, Math.Min(64.0, motor.VolumenVideo(ruta)));
                pi.ArgumentList.Add("--af=lavfi=[alimiter=level_in=" + g.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)
                    + ":limit=" + motor.TechoVideos.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + ":attack=5:release=80:level=0:latency=1],@noche:lavfi=[volume=volume=" + motor.FactorNoche().ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)
                    + "],@fin:lavfi=[volume=volume=1:eval=frame]");
                videoNoche = motor.FactorNoche();
                // EL MODO NOCHE (7-11-2026, ModoNoche.cs): @noche lleva el mismo factor que la cadena de las canciones; si la rampa se mueve con
                // el video sonando (llegan las 11, o «Probar»), NocheAlVideo se lo manda a mpv por su canal de ordenes.
                // SALTAR UN VIDEO SIN GOLPE (4-11-2026): @fin no hace nada hasta que, al saltar, se le pide por el canal de ordenes de mpv que baje a
                // cero en 0,3 s (ApagaVideo); despues se cierra. Antes se mataba mpv a media onda: un golpe seco en las bocinas.
                mpvOrdenes = Path.Combine(Path.GetTempPath(), "rocola-mpv-" + Environment.ProcessId + ".sock");
                try { File.Delete(mpvOrdenes); } catch { }
                pi.ArgumentList.Add("--input-ipc-server=" + mpvOrdenes);
                // la misma tarjeta que la musica (SalidaAlsa.Elige: la del amplificador, directa), no la 0 por su mezclador
                string tarjeta = dispositivo == "default" ? SalidaAlsa.Elige() : dispositivo;
                if (tarjeta != "default") pi.ArgumentList.Add("--audio-device=alsa/" + tarjeta);
                pi.ArgumentList.Add("--"); pi.ArgumentList.Add(ruta);
                pi.UseShellExecute = false;
                pi.RedirectStandardError = true; pi.RedirectStandardOutput = true;
                errorVideo = new StringBuilder(); videoDesde = Environment.TickCount; videoRuta = ruta; videoAvanzo = 0;
                video = System.Diagnostics.Process.Start(pi);
                video.ErrorDataReceived += delegate (object o, System.Diagnostics.DataReceivedEventArgs e) { if (e.Data != null) lock (errorVideo) errorVideo.AppendLine(e.Data); };
                video.OutputDataReceived += delegate (object o, System.Diagnostics.DataReceivedEventArgs e) { if (e.Data != null) lock (errorVideo) errorVideo.AppendLine(e.Data); };
                video.BeginErrorReadLine(); video.BeginOutputReadLine();
            }
            catch (Exception ex) { try { if (video != null) { video.Kill(); video.Dispose(); } } catch { } video = null; motor.DevuelveCredito("No se pudo poner el video: " + ex.Message); TerminaVideo(); }
        }
        private string mpvOrdenes;
        private float videoNoche = 1f;
        private int videoNocheMs;
        // El modo noche al video que suena: cuando el factor se movio mas de 0,005, como mucho cada 250 ms (el canal es de mpv, en otro hilo).
        private void NocheAlVideo()
        {
            if (video == null || mpvOrdenes == null) return;
            float f = motor.FactorNoche();
            if (Math.Abs(f - videoNoche) < 0.005f && !(f == 1f && videoNoche != 1f) && !(f == Form1.NocheLin() && videoNoche != f)) return;
            int ahora = Environment.TickCount; if (unchecked(ahora - videoNocheMs) < 250) return;
            videoNocheMs = ahora; videoNoche = f;
            string ruta = mpvOrdenes, v = f.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    using (System.Net.Sockets.Socket s = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified))
                    {
                        s.SendTimeout = 200;
                        s.Connect(new System.Net.Sockets.UnixDomainSocketEndPoint(ruta));
                        s.Send(Encoding.UTF8.GetBytes("{\"command\":[\"af-command\",\"noche\",\"volume\",\"" + v + "\"]}\n"));
                    }
                }
                catch { }
            });
        }
        private int videoDesde; internal const int VideoFalloMs = 8000;   // un video que falla antes de 8 s no llego a verse: se devuelve su credito
        private int videoApagaDesde; private bool videoSaltado;
        internal const int VideoApagaMs = 300;
        private void SigueVideo()
        {
            bool acabo; try { acabo = video.HasExited; } catch { acabo = true; }
            if (saltar && !acabo)
            {
                saltar = false;
                if (videoApagaDesde == 0) { videoApagaDesde = Environment.TickCount | 1; videoSaltado = true; ApagaVideo(); }
                return;
            }
            // se le dio tiempo a bajar (o el canal de ordenes no respondio): ahora si se cierra
            if (!acabo && videoApagaDesde != 0 && unchecked(Environment.TickCount - videoApagaDesde) > VideoApagaMs + 100) { try { video.Kill(); } catch { } return; }
            if (!acabo && videoApagaDesde == 0) MiraAvanceVideo();
            if (acabo) TerminaVideo();
        }
        // UN VIDEO QUE SE TRABA (4-11-2026): la rocola espera a que mpv acabe; si mpv se colgaba sin cerrarse, la rocola se quedaba en ese
        // video para siempre (salvo que alguien saltara). Cada 10 s se le pregunta por donde va (time-pos, por su canal de ordenes, en un
        // hilo aparte); si en VideoTrabadoMs no avanzo (o no contesta), se cierra como un salto y sigue la cola.
        internal const int VideoTrabadoMs = 60000;
        private volatile int videoPreguntando; private double videoPos = double.NaN; private int videoAvanzo, videoPregunto;
        private void MiraAvanceVideo()
        {
            int ahora = Environment.TickCount;
            if (videoAvanzo == 0) { videoAvanzo = ahora | 1; videoPregunto = ahora; videoPos = double.NaN; }
            if (unchecked(ahora - videoAvanzo) > VideoTrabadoMs)
            {
                motor.ApuntaError("Video trabado", new Exception("«" + System.IO.Path.GetFileName(videoRuta) + "» no avanzo en " + VideoTrabadoMs / 1000 + " s: se cierra"));
                videoAvanzo = 0; videoSaltado = true; videoApagaDesde = Environment.TickCount | 1;
                try { video.Kill(); } catch { }
                return;
            }
            if (unchecked(ahora - videoPregunto) < 10000 || videoPreguntando != 0) return;
            videoPregunto = ahora; videoPreguntando = 1;
            string ruta = mpvOrdenes;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    double p = PreguntaMpv(ruta, "time-pos");
                    if (!double.IsNaN(p) && (double.IsNaN(videoPos) || Math.Abs(p - videoPos) > 0.5)) { videoPos = p; videoAvanzo = Environment.TickCount | 1; }
                }
                catch { }
                finally { videoPreguntando = 0; }
            });
        }
        private string videoRuta;
        // Una propiedad numerica de mpv por su canal de ordenes; NaN si no contesta.
        private static double PreguntaMpv(string ruta, string propiedad)
        {
            using (System.Net.Sockets.Socket s = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified))
            {
                s.ReceiveTimeout = 2000; s.SendTimeout = 2000;
                s.Connect(new System.Net.Sockets.UnixDomainSocketEndPoint(ruta));
                s.Send(Encoding.UTF8.GetBytes("{\"command\":[\"get_property\",\"" + propiedad + "\"],\"request_id\":9}\n"));
                byte[] b = new byte[4096]; StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 8; i++)
                {
                    int n = s.Receive(b); if (n <= 0) break;
                    sb.Append(Encoding.UTF8.GetString(b, 0, n));
                    foreach (string l in sb.ToString().Split('\n'))
                        if (l.Contains("\"request_id\":9"))
                        {
                            int k = l.IndexOf("\"data\":"); if (k < 0) return double.NaN;
                            string v = l.Substring(k + 7); int fin = v.IndexOfAny(new[] { ',', '}' });
                            double d; return double.TryParse(fin > 0 ? v.Substring(0, fin) : v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d) ? d : double.NaN;
                        }
                }
            }
            return double.NaN;
        }
        // Por el canal de ordenes de mpv (JSON por un socket de Unix): donde va el sonido y que @fin baje de 1 a 0 en 0,3 s desde un poco
        // despues. En un hilo aparte (la pantalla no espera); si algo falla, el video se cierra igual a los 0,4 s, como antes pero mas tarde.
        private void ApagaVideo()
        {
            string ruta = mpvOrdenes;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    using (System.Net.Sockets.Socket s = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified))
                    {
                        s.ReceiveTimeout = 200; s.SendTimeout = 200;
                        s.Connect(new System.Net.Sockets.UnixDomainSocketEndPoint(ruta));
                        s.Send(Encoding.UTF8.GetBytes("{\"command\":[\"get_property\",\"audio-pts\"],\"request_id\":7}\n"));
                        byte[] b = new byte[4096]; StringBuilder sb = new StringBuilder(); double pts = double.NaN;
                        for (int i = 0; i < 5 && double.IsNaN(pts); i++)
                        {
                            int n = s.Receive(b); if (n <= 0) break;
                            sb.Append(Encoding.UTF8.GetString(b, 0, n));
                            foreach (string l in sb.ToString().Split('\n'))
                                if (l.Contains("\"request_id\":7") && l.Contains("\"data\":"))
                                {
                                    string v = l.Substring(l.IndexOf("\"data\":") + 7); int fin = v.IndexOfAny(new[] { ',', '}' });
                                    double.TryParse(fin > 0 ? v.Substring(0, fin) : v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out pts);
                                }
                        }
                        if (double.IsNaN(pts)) return;
                        string expr = "max(0,1-(t-" + (pts + 0.05).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + ")/" + (VideoApagaMs / 1000.0).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + ")";
                        s.Send(Encoding.UTF8.GetBytes("{\"command\":[\"af-command\",\"fin\",\"volume\",\"" + expr + "\"]}\n"));
                        Thread.Sleep(50);
                    }
                }
                catch { }
            });
        }
        private void TerminaVideo()
        {
            if (video != null)
            {
                int codigo = 0; try { video.WaitForExit(1000); codigo = video.ExitCode; } catch { }
                string dijo; lock (errorVideo) dijo = errorVideo.ToString().Trim();
                if (codigo != 0 && !saltar && !videoSaltado)   // saltado: cerrado a proposito, no es un fallo (antes salia «No se pudo poner el video»)
                {
                    Console.Error.WriteLine("mpv salio con " + codigo + ": " + dijo);
                    if (dijo.Length == 0) try { dijo = File.ReadAllText("/tmp/rocola-mpv.txt").Trim(); } catch { }
                    string[] lineas = dijo.Split('\n');
                    string ultima = "codigo " + codigo;
                    List<string> malas = new List<string>();
                    foreach (string l in lineas)
                        if ((l.Contains("][e][") || l.Contains("][w][") || l.Contains("][f][")) && !l.Contains("Exiting")) malas.Add(l.Substring(l.IndexOf("][") + 4).Trim());
                    if (malas.Count > 0) { ultima = string.Join(" | ", malas.GetRange(Math.Max(0, malas.Count - 2), Math.Min(2, malas.Count))); if (ultima.Length > 150) ultima = ultima.Substring(0, 150); }
                    if (unchecked(Environment.TickCount - videoDesde) < VideoFalloMs) motor.DevuelveCredito("No se pudo poner el video (" + ultima + ")");   // no llego a verse
                    else Aviso("No se pudo poner el video (" + ultima + ")");
                }
                try { video.Dispose(); } catch { } video = null;
            }
            saltar = false; videoSaltado = false; videoApagaDesde = 0;
            if (mpvOrdenes != null) { try { File.Delete(mpvOrdenes); } catch { } }
            motor.PausaRepaso = false;
            motor.AcaboElVideo();
            if (vistaActual != null) vistaActual.Recupera();
        }

        // LAS PRUEBAS DEL LOCAL (la de bocinas y las bandas de afinar): el motor dice cual debe sonar y aqui se toca por su propia salida
        // ALSA, sin la cadena, solo con nada sonando (como en XP, que les abre su propia salida).
        private SalidaAlsa salidaPrueba;
        private ISampleProvider pruebaTocando;
        private void CierraPrueba()
        {
            if (salidaPrueba != null) { try { CierraSuave(salidaPrueba); } catch { } }
            salidaPrueba = null; pruebaTocando = null;
        }
        // CERRAR SIN GOLPE (4-11-2026): como al saltar, la salida baja a cero en 60 ms y acaba sola antes de cerrarla (al apagar la rocola
        // y al cortar una prueba de bocinas o de bandas); si en medio segundo no acabo, se cierra como antes.
        private static void CierraSuave(SalidaAlsa s)
        {
            try
            {
                if (s.Sonando) { s.Apaga(); int t0 = Environment.TickCount; while (s.Sonando && unchecked(Environment.TickCount - t0) < 500) Thread.Sleep(5); }
            }
            catch { }
            s.Dispose();
        }
        private void RevisaPrueba()
        {
            ISampleProvider pq = motor.PruebaActual;
            if (salidaPrueba != null && !salidaPrueba.Sonando) { ISampleProvider t = pruebaTocando; CierraPrueba(); motor.PruebaAcabo(t); pq = motor.PruebaActual; }
            if (salidaPrueba != null && !object.ReferenceEquals(pq, pruebaTocando)) CierraPrueba();
            if (salidaPrueba == null && pq != null && salida == null && video == null)
            {
                try { salidaPrueba = new SalidaAlsa(pq, dispositivo); pruebaTocando = pq; salidaPrueba.Empieza(); }
                catch (Exception ex) { CierraPrueba(); motor.ParaPrueba(); Aviso("No se pudo abrir el sonido: " + ex.Message); }
            }
        }

        // Los cortes de la salida, por cancion (en un disco continuo la salida sigue abierta de una a otra): ApuntaCortes.
        private long cortesVistos; private string cortesCancion;
        private void CajaNegra(bool fin)
        {
            string t = motor.SonandoTitulo;
            if (!fin && t == cortesCancion) return;
            long r = Interlocked.Read(ref salida.Recuperaciones), n = r - cortesVistos;
            cortesVistos = r;
            if (n > 0) motor.ApuntaCortes(cortesCancion ?? t, n, salida.Dispositivo, salida.PrioridadSonido);
            cortesCancion = t;
        }

        // SIN TARJETA, LA COLA ESPERA (4-11-2026; Form1.AcaboPorFallo): un fallo de la salida no se come la cancion; se vuelve a probar a
        // los 2, 4, 8... s (hasta cada 30 s), diciendolo en pantalla. Al primer intento bueno, todo como siempre.
        private int sinSonidoHasta, fallosSonido;
        private void FalloSonido(string porque)
        {
            fallosSonido++;
            int espera = Math.Min(30000, 2000 << Math.Min(4, fallosSonido - 1));
            sinSonidoHasta = Environment.TickCount + espera;
            Aviso("Sin sonido (" + porque + "). La cola se conserva: vuelvo a probar en " + espera / 1000 + " s");
            if (fallosSonido == 1 || fallosSonido % 20 == 0) motor.ApuntaError("Sin sonido (intento " + fallosSonido + ")", new Exception(porque));
        }

        // El sonido: empieza la siguiente si toca, atiende el relevo sin pausa y cierra al acabar o al saltar.
        private void Suena()
        {
            // SPOTIFY (Motor/Spotify.cs): abre o cierra su programa segun el interruptor y trae la biblioteca; la letra SPOTIFY se pone al dia.
            motor.AtiendeSpotify();
            RevisaSpotify();
            RevisaLetrasSpotify();
            RevisaMas();
            RevisaBusquedaSpotify();
            if (salida == null && video == null)
            {
                string v = motor.TomaVideo();
                if (v != null) { EmpiezaVideo(v); return; }
            }
            if (video != null) { SigueVideo(); return; }
            if (salida == null && (fallosSonido == 0 || unchecked(Environment.TickCount - sinSonidoHasta) >= 0) && motor.EmpezarYa)
            {
                if (salidaPrueba != null || motor.PruebaActual != null) { motor.ParaPrueba(); CierraPrueba(); }   // la musica manda: la prueba se calla
                ISampleProvider s = motor.EmpiezaSiguiente();
                if (s != null)
                {
                    try { salida = new SalidaAlsa(s, dispositivo); salida.Empieza(); cortesVistos = 0; cortesCancion = motor.SonandoTitulo; fallosSonido = 0; }
                    catch (Exception ex) { salida = null; FalloSonido(ex.Message); motor.AcaboPorFallo(); }
                    motor.GuardaEstado();
                }
            }
            RevisaPrueba();
            if (salida != null)
            {
                if (!saltar && motor.SonandoSpotify && !motor.SpotifyOn && salida.Sonando && !salida.Apagando)
                {
                    salida.Apaga();   // se apago el interruptor: baja en 60 ms y despues se cierra Spotify (Form1.AtiendeSpotify)
                    return;
                }
                if (saltar && salida.Sonando)
                {
                    // SALTAR SIN CHASQUIDO (SalidaAlsa.Apaga): la musica baja a cero en 60 ms y la salida acaba sola; mientras, no se
                    // prepara ni releva nada (lo que entraria ya no se oiria)
                    salida.Apaga();
                    return;
                }
                if (!salida.Apagando) motor.Atiende(salida.CuadrosOidos);
                CajaNegra(false);
                if (!salida.Sonando || saltar)
                {
                    CajaNegra(true);
                    bool fallo = salida.Fallo && !saltar;
                    if (salida.FalloLectura != null) motor.ApuntaError("Fallo al leer «" + motor.SonandoTitulo + "» (se acabo la cancion)", salida.FalloLectura);
                    salida.Dispose(); salida = null; saltar = false;
                    if (fallo) { FalloSonido("la tarjeta dejo de responder"); motor.AcaboPorFallo(); }
                    else motor.AcaboLaSalida();
                    motor.GuardaEstado();
                }
            }
        }

    }
}
