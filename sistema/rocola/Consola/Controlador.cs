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
        }
        // Un boton, como si se hubiera pulsado (las pruebas).
        internal void Pulsa(Boton b) { Maneja(b); }
        // Una tecla (codigo de Windows), como si llegara de los botones (las pruebas).
        internal void PulsaTecla(int vk) { Tecla(vk); }

        internal void EmpiezaAsistente() { AsistentePaso = 0; AsistenteAviso = ""; }

        private void Tecla(int vk)
        {
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

        internal int Corre(IVista vista)
        {
            Prepara();
            using (Botones botones = new Botones())
            {
                botones.Configura(motor.ArchivoBotones);
                this.botones = botones;
                botones.Pulsado += delegate (Boton b, int vk) { if (vk != 0) pulsos.Add(vk); };
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
                try
                {
                    while (!salir)
                    {
                        int vk;
                        if (pulsos.TryTake(out vk, 50)) { if (vk == 0) break; motor.HuboBoton(); Tecla(vk); }
                        motor.PublicaMedidas();
                        motor.Periodico();   // el reinicio de las 7 (si esta ociosa) y lo aprendido al disco, una vez por minuto
                        Suena();
                        vista.Dibuja(this);
                    }
                }
                finally
                {
                    if (salida != null) { salida.Dispose(); salida = null; motor.AcaboLaSalida(); }
                    motor.GuardaEstado();
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
                pulsos.Add((int)k.Key);   // el codigo de ConsoleKey es el de tecla de Windows
            }
        }

        internal List<Form1.Disco> DiscosDeLetra() { return letras.Count == 0 ? new List<Form1.Disco>() : porLetra[letras[letra]]; }

        private void Maneja(Boton b)
        {
            switch (b)
            {
                case Boton.Moneda: motor.Moneda(); return;
                case Boton.Saltar: if (salida != null) saltar = true; return;
            }
            if (dentro == null)
            {
                List<Form1.Disco> l = DiscosDeLetra();
                if (l.Count == 0) return;
                if (b == Boton.Arriba) cursorDisco = (cursorDisco + l.Count - 1) % l.Count;
                else if (b == Boton.Abajo) cursorDisco = (cursorDisco + 1) % l.Count;
                else if (b == Boton.Izquierda) { letra = (letra + letras.Count - 1) % letras.Count; cursorDisco = 0; }
                else if (b == Boton.Derecha) { letra = (letra + 1) % letras.Count; cursorDisco = 0; }
                else if (b == Boton.Poner) { dentro = l[cursorDisco]; cursorCancion = 0; }
            }
            else
            {
                int n = dentro.Rutas.Count, pagina = Math.Max(1, PorPagina);
                if (b == Boton.Arriba) cursorCancion = (cursorCancion + n - 1) % n;
                else if (b == Boton.Abajo) cursorCancion = (cursorCancion + 1) % n;
                else if (b == Boton.Izquierda) cursorCancion = Math.Max(0, cursorCancion - pagina);
                else if (b == Boton.Derecha) cursorCancion = Math.Min(n - 1, cursorCancion + pagina);
                else if (b == Boton.Regresar) dentro = null;
                else if (b == Boton.Poner)
                {
                    string ruta = dentro.Rutas[cursorCancion];
                    motor.MideEnFondo(ruta);   // como el repaso de XP, en segundo plano: hasta tenerla, suena con la regla de las no medidas
                    Form1.Pedido p = motor.Pide(ruta);
                    if (p == Form1.Pedido.Puesta) Aviso("Pusiste «" + dentro.Titulos[cursorCancion] + "» (" + motor.EnCola + " en la cola)");
                    else if (p == Form1.Pedido.Repetida) Aviso("Esa ya va en la cola");
                    else if (p == Form1.Pedido.Danada) Aviso("Esa cancion esta danada: no se cobra");
                }
            }
        }

        // El sonido: empieza la siguiente si toca, atiende el relevo sin pausa y cierra al acabar o al saltar.
        private void Suena()
        {
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
