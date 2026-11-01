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

    internal sealed class Controlador
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
        private SalidaAlsa salida;
        private bool saltar, salir;
        internal int PorPagina = 20;           // canciones que caben en una pagina de la vista (IZQ/DER dentro de un disco)
        internal string AvisoVigente { get { return unchecked(Environment.TickCount - avisoHasta) < 0 ? aviso : ""; } }
        internal float Progreso { get { return motor.SonandoProgreso; } }
        internal long CuadrosOidos { get { SalidaAlsa sa = salida; return sa != null ? sa.CuadrosOidos : -1; } }   // para el pulso de la pantalla

        internal Controlador(Form1 motor, string dispositivo) { this.motor = motor; this.dispositivo = dispositivo; }

        // La biblioteca por letras y lo de antes del apagon.
        internal void Prepara()
        {
            motor.Aviso += delegate (string t) { aviso = t; avisoHasta = Environment.TickCount + 4000; };
            discos = motor.LeeBiblioteca();
            foreach (Form1.Disco d in discos)
            {
                List<Form1.Disco> l;
                if (!porLetra.TryGetValue(d.Letra, out l)) { l = new List<Form1.Disco>(); porLetra[d.Letra] = l; letras.Add(d.Letra); }
                l.Add(d);
            }
            motor.Reanuda();   // los creditos y la cola de antes del apagon (mismo dia del negocio)
            RefrescaTop();
            int repaso = motor.RepasaBiblioteca();   // lo que falta medir, poco a poco y con prioridad baja (el repaso de XP)
            if (repaso > 0) Console.WriteLine("Repaso: " + repaso + " canciones por medir");
        }
        // Un boton, como si se hubiera pulsado (las pruebas).
        internal void Pulsa(Boton b) { Maneja(b); }
        // Una tecla (codigo de Windows), como si llegara de los botones (las pruebas).
        internal void PulsaTecla(int vk) { Tecla(vk); }

        internal void EmpiezaAsistente() { AsistentePaso = 0; AsistenteAviso = ""; }

        // UN GUION DE BOTONES (pruebas: --guion archivo): una linea por paso: un boton (moneda, poner...), "regresar+derecha" (con REGRESAR
        // mantenido), "espera 2000" (ms) o "salir". Se mete en la misma fila que los botones de verdad.
        internal string Guion;
        private const int DeGuion = -1000;
        private const int ConRegresar = 0x10000;   // la tecla llego con REGRESAR apretado
        private const int ConCtrl = 0x20000;       // ... o con Ctrl (Ctrl+F3: los ajustes del dueno, como en XP)
        private void CorreGuion()
        {
            foreach (string l0 in File.ReadAllLines(Guion))
            {
                string l = l0.Trim().ToLowerInvariant(); if (l.Length == 0 || l.StartsWith("#")) continue;
                if (l.StartsWith("espera ")) { Thread.Sleep(int.Parse(l.Substring(7))); continue; }
                if (l == "salir") { pulsos.Add(0); return; }
                if (l == "ctrl+f3") { pulsos.Add(0x72 | ConCtrl); Thread.Sleep(150); continue; }
                bool con = l.StartsWith("regresar+"); if (con) l = l.Substring(9);
                Boton b = (Boton)Enum.Parse(typeof(Boton), l, true);
                pulsos.Add(DeGuion - (int)b * 2 - (con ? 1 : 0));
                Thread.Sleep(150);
            }
        }

        private void Tecla(int vk)
        {
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
        }
        private void CapturaAsistente(int vk)
        {
            if (vk == 0 || vk == 0x10 || vk == 0x11 || vk == 0x12) return;   // nada, Mayus, Ctrl o Alt sueltas (como XP)
            for (int j = 0; j < AsistentePaso; j++)
                if (capturas[j] == vk) { AsistenteAviso = "Ese boton ya esta usado. Presiona OTRO."; return; }
            capturas[AsistentePaso++] = vk; AsistenteAviso = "";
            if (AsistentePaso < PasosAsistente.Length) return;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 8; i++) sb.Append(ClavesAsistente[i]).Append('=').Append(capturas[i]).Append("\r\n");
            try { File.WriteAllText(motor.ArchivoBotones, sb.ToString()); } catch (Exception ex) { Aviso("No se pudo guardar: " + ex.Message); }
            if (botones != null) botones.Configura(motor.ArchivoBotones);
            AsistentePaso = -1;
            Aviso("¡Botones configurados!");
        }

        // El aviso de apagar (SIGTERM: el boton de encendido, por acpid): se sale del bucle y es un cierre deliberado.
        internal volatile bool Apagando;
        internal void Apaga() { Apagando = true; salir = true; pulsos.Add(0); }

        internal int Corre(IVista vista)
        {
            vistaActual = vista;
            Prepara();
            using (Botones botones = new Botones())
            {
                botones.Configura(motor.ArchivoBotones);
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
                try
                {
                    while (!salir)
                    {
                        int vk;
                        if (pulsos.TryTake(out vk, vista.Anima ? 15 : 50)) { if (vk == 0) break; motor.HuboBoton(); Tecla(vk); }
                        motor.PublicaMedidas();
                        motor.Periodico();   // el reinicio de las 7 (si esta ociosa) y lo aprendido al disco, una vez por minuto
                        Suena();
                        if (video == null) vista.Dibuja(this);   // mientras suena un video, la pantalla es suya
                    }
                }
                finally
                {
                    if (salida != null) { salida.Dispose(); salida = null; motor.AcaboLaSalida(); }
                    if (video != null) { try { video.Kill(); } catch { } TerminaVideo(); }
                    if (Apagando) motor.CierreDelDia();   // el boton de encendido: como el cierre de XP, manana arranca limpia
                    else motor.GuardaEstado();
                }
            }
            return 0;
        }

        private void Aviso(string t) { aviso = t; avisoHasta = Environment.TickCount + 4000; }

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
        internal static string NombreLetra(char l) { return l == Form1.LetraTop ? "TOP" : l == Form1.LetraVideos ? "VIDEOS" : l.ToString(); }
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

        internal List<Form1.Disco> DiscosDeLetra() { return letras.Count == 0 ? new List<Form1.Disco>() : porLetra[letras[letra]]; }

        internal const int DiscosPorPagina = 8;
        // REGRESAR mantenido (para los combos de XP); en las pruebas se da por boton.
        private bool regresarDePrueba;
        private bool RegresarApretado { get { return regresarDePrueba; } }   // lo que estaba apretado al pulsar (PulsadoConRegresar)
        internal void Pulsa(Boton b, bool conRegresar) { regresarDePrueba = conRegresar; try { Maneja(b); } finally { regresarDePrueba = false; } }

        // LOS BOTONES, como en XP (Form1.cs: ProcessCmdKey). MONEDA y SALTAR siempre. REGRESAR mantenido + ARRIBA abre o cierra el buscador.
        // En la pared: IZQ/DER, de disco; ARRIBA, pagina siguiente; ABAJO, la anterior; REGRESAR mantenido + IZQ/DER, de letra; PONER, entrar.
        // Dentro de un disco: ARRIBA/ABAJO, de cancion; IZQ/DER, de pagina; PONER, pedirla; REGRESAR, a la pared.
        private void Maneja(Boton b)
        {
            switch (b)
            {
                case Boton.Moneda: motor.Moneda(); return;
                case Boton.Saltar: if (salida != null || video != null) saltar = true; return;
            }
            if (video != null) return;   // durante un video los demas botones no hacen nada (la pantalla es del video)
            if (AjusteSel >= 0) { ManejaAjustes(b); return; }
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
                else if (b == Boton.Arriba && !RegresarApretado) { int p = cursorDisco / DiscosPorPagina + 1; if (p * DiscosPorPagina >= l.Count) p = 0; cursorDisco = p * DiscosPorPagina; }
                else if (b == Boton.Abajo && !RegresarApretado) { int paginas = (l.Count + DiscosPorPagina - 1) / DiscosPorPagina, p = cursorDisco / DiscosPorPagina - 1; if (p < 0) p = paginas - 1; cursorDisco = p * DiscosPorPagina; }
                else if (b == Boton.Poner) { dentro = l[cursorDisco]; cursorCancion = 0; }
            }
            else
            {
                int n = dentro.Rutas.Count, pagina = Math.Max(1, PorPagina);
                if (b == Boton.Arriba && !RegresarApretado) cursorCancion = (cursorCancion + n - 1) % n;
                else if (b == Boton.Abajo && !RegresarApretado) cursorCancion = (cursorCancion + 1) % n;
                else if (b == Boton.Izquierda) cursorCancion = Math.Max(0, cursorCancion - pagina);
                else if (b == Boton.Derecha) cursorCancion = Math.Min(n - 1, cursorCancion + pagina);
                else if (b == Boton.Regresar) dentro = null;
                else if (b == Boton.Poner) PideCancion(dentro.Rutas[cursorCancion], dentro.Titulos[cursorCancion]);
            }
        }

        private void PideCancion(string ruta, string titulo)
        {
            motor.MideEnFondo(ruta);   // como el repaso de XP, en segundo plano: hasta tenerla, suena con la regla de las no medidas
            Form1.Pedido p = motor.Pide(ruta);
            if (p == Form1.Pedido.Puesta)
            {
                Aviso("Pusiste «" + titulo + "» (" + motor.EnCola + " en la cola)");
                if (letras.Count == 0 || letras[letra] != Form1.LetraTop) RefrescaTop();   // (dentro del TOP no: se moveria lo que se mira)
            }
            else if (p == Form1.Pedido.Repetida) Aviso("Esa cancion ya va seguida");
            else if (p == Form1.Pedido.Danada) Aviso("Esa cancion esta danada: no se cobra");
        }

        // ---- LOS AJUSTES DEL DUENO (Ctrl+F3, como en XP): ARRIBA/ABAJO eligen, IZQ/DER o PONER cambian, REGRESAR guarda y sale.
        internal int AjusteSel = -1;   // -1: cerrados
        internal IList<Form1.Ajuste> ListaAjustes { get { return motor.Ajustes(); } }
        internal int AjustesTotal { get { return ListaAjustes.Count + 1; } }   // y al final: configurar los botones
        internal void AbreAjustes() { AjusteSel = 0; Buscando = false; }
        private void ManejaAjustes(Boton b)
        {
            int n = AjustesTotal;
            if (b == Boton.Arriba) AjusteSel = (AjusteSel + n - 1) % n;
            else if (b == Boton.Abajo) AjusteSel = (AjusteSel + 1) % n;
            else if (b == Boton.Regresar)
            {
                try { motor.GuardaAjustes(); Aviso("Ajustes guardados: se oyen desde la siguiente cancion"); } catch (Exception ex) { Aviso("No se pudo guardar: " + ex.Message); }
                AjusteSel = -1;
            }
            else if (AjusteSel == n - 1) { if (b == Boton.Poner) { AjusteSel = -1; EmpiezaAsistente(); } }
            else if (b == Boton.Izquierda || b == Boton.Derecha || b == Boton.Poner)
            {
                Form1.Ajuste a = ListaAjustes[AjusteSel];
                if (a.Motivo != null) Aviso("No se puede: " + a.Motivo);
                else a.Mueve(b == Boton.Izquierda ? -1 : 1);
            }
        }

        // ---- EL BUSCADOR (el de XP: teclado en pantalla, resultados a la derecha; REGRESAR borra una letra) ----
        internal static readonly string[] FilasTeclado = { "1234567890", "QWERTYUIOP", "ASDFGHJKL\u00D1", "ZXCVBNM", "\u0001\u0002" };   // la ultima: ESPACIO y BORRAR
        internal bool Buscando;
        internal string Escrito = "";
        internal int FilaTecla = 1, ColTecla, SelResultado;
        internal bool EnResultados;
        internal List<Form1.Resultado> Resultados = new List<Form1.Resultado>();
        internal static string Etiqueta(char t) { return t == '\u0001' ? "ESPACIO" : t == '\u0002' ? "BORRAR" : t.ToString(); }

        internal void AbreBuscador() { Buscando = true; Escrito = ""; FilaTecla = 1; ColTecla = 0; EnResultados = false; SelResultado = 0; Resultados = new List<Form1.Resultado>(); }
        private void CierraBuscador() { Buscando = false; }
        private void Escribe(char ch)
        {
            if (ch == '\u0002') { if (Escrito.Length > 0) Escrito = Escrito.Substring(0, Escrito.Length - 1); }
            else Escrito += ch == '\u0001' ? ' ' : ch;
            Resultados = motor.Busca(Escrito); SelResultado = 0;
        }
        private void ManejaBuscador(Boton b)
        {
            if (b == Boton.Regresar) { Escribe('\u0002'); return; }
            if (EnResultados)
            {
                int n = Resultados.Count;
                if (n == 0) { EnResultados = false; return; }
                if (b == Boton.Arriba) SelResultado = Math.Max(0, SelResultado - 1);
                else if (b == Boton.Abajo) SelResultado = Math.Min(n - 1, SelResultado + 1);
                else if (b == Boton.Izquierda) EnResultados = false;
                else if (b == Boton.Poner) Elige(Resultados[Math.Min(SelResultado, n - 1)]);
                return;
            }
            string fila = FilasTeclado[FilaTecla];
            if (b == Boton.Arriba) { FilaTecla = Math.Max(0, FilaTecla - 1); ColTecla = Math.Min(ColTecla, FilasTeclado[FilaTecla].Length - 1); }
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
            if (r.Tipo == 2 && r.Ruta != null) { PideCancion(r.Ruta, r.Texto); return; }
            if (r.Carpeta == null) return;
            for (int i = 0; i < letras.Count; i++)
            {
                if (letras[i] == Form1.LetraTop) continue;
                List<Form1.Disco> l = porLetra[letras[i]];
                for (int j = 0; j < l.Count; j++)
                    if (l[j].Carpeta == r.Carpeta)
                    {
                        letra = i; cursorDisco = j; Buscando = false;
                        if (r.Tipo == 1) { dentro = l[j]; cursorCancion = 0; } else dentro = null;
                        return;
                    }
            }
        }

        // LOS VIDEOS: con mpv (en Rocola OS, por KMS y ALSA), a pantalla completa y con el volumen de los videos de XP (video_vol=). La
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
                    "--ao=alsa", "--volume=" + motor.VolumenVideos, "--", ruta })
                    pi.ArgumentList.Add(a);
                pi.UseShellExecute = false;
                pi.RedirectStandardError = true; pi.RedirectStandardOutput = true;
                errorVideo = new StringBuilder();
                video = System.Diagnostics.Process.Start(pi);
                video.ErrorDataReceived += delegate (object o, System.Diagnostics.DataReceivedEventArgs e) { if (e.Data != null) lock (errorVideo) errorVideo.AppendLine(e.Data); };
                video.OutputDataReceived += delegate (object o, System.Diagnostics.DataReceivedEventArgs e) { if (e.Data != null) lock (errorVideo) errorVideo.AppendLine(e.Data); };
                video.BeginErrorReadLine(); video.BeginOutputReadLine();
            }
            catch (Exception ex) { Aviso("No se pudo poner el video: " + ex.Message); TerminaVideo(); }
        }
        private void SigueVideo()
        {
            bool acabo; try { acabo = video.HasExited; } catch { acabo = true; }
            if (saltar && !acabo) { try { video.Kill(); } catch { } saltar = false; return; }
            if (acabo) TerminaVideo();
        }
        private void TerminaVideo()
        {
            if (video != null)
            {
                int codigo = 0; try { video.WaitForExit(1000); codigo = video.ExitCode; } catch { }
                string dijo; lock (errorVideo) dijo = errorVideo.ToString().Trim();
                if (codigo != 0 && !saltar)
                {
                    Console.Error.WriteLine("mpv salio con " + codigo + ": " + dijo);
                    if (dijo.Length == 0) try { dijo = File.ReadAllText("/tmp/rocola-mpv.txt").Trim(); } catch { }
                    string[] lineas = dijo.Split('\n');
                    string ultima = "codigo " + codigo;
                    List<string> malas = new List<string>();
                    foreach (string l in lineas)
                        if ((l.Contains("][e][") || l.Contains("][w][") || l.Contains("][f][")) && !l.Contains("Exiting")) malas.Add(l.Substring(l.IndexOf("][") + 4).Trim());
                    if (malas.Count > 0) { ultima = string.Join(" | ", malas.GetRange(Math.Max(0, malas.Count - 2), Math.Min(2, malas.Count))); if (ultima.Length > 150) ultima = ultima.Substring(0, 150); }
                    Aviso("No se pudo poner el video (" + ultima + ")");
                }
                try { video.Dispose(); } catch { } video = null;
            }
            saltar = false;
            motor.PausaRepaso = false;
            motor.AcaboElVideo();
            if (vistaActual != null) vistaActual.Recupera();
        }

        // El sonido: empieza la siguiente si toca, atiende el relevo sin pausa y cierra al acabar o al saltar.
        private void Suena()
        {
            if (salida == null && video == null)
            {
                string v = motor.TomaVideo();
                if (v != null) { EmpiezaVideo(v); return; }
            }
            if (video != null) { SigueVideo(); return; }
            if (salida == null && motor.EmpezarYa)
            {
                ISampleProvider s = motor.EmpiezaSiguiente();
                if (s != null)
                {
                    try { salida = new SalidaAlsa(s, dispositivo); salida.Empieza(); }
                    catch (Exception ex) { Aviso("No se pudo abrir el sonido: " + ex.Message); salida = null; motor.AcaboLaSalida(); }
                    motor.GuardaEstado();
                }
            }
            if (salida != null)
            {
                motor.Atiende(salida.CuadrosOidos);
                if (!salida.Sonando || saltar)
                {
                    salida.Dispose(); salida = null; saltar = false;
                    motor.AcaboLaSalida();
                    motor.GuardaEstado();
                }
            }
        }

    }
}
