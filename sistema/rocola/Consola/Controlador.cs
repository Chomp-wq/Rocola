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
        private readonly BlockingCollection<Boton> pulsos = new BlockingCollection<Boton>();
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

        internal int Corre(IVista vista)
        {
            Prepara();
            using (Botones botones = new Botones())
            {
                botones.Configura(motor.ArchivoBotones);
                botones.Pulsado += delegate (Boton b, int vk) { if (b != Boton.Ninguno) pulsos.Add(b); };
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
                        Boton b;
                        if (pulsos.TryTake(out b, 50)) Maneja(b);
                        motor.PublicaMedidas();
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

        // El teclado de la terminal: sus codigos de tecla son los de Windows (ConsoleKey), asi que valen las mismas funciones. Esc o Q: salir.
        private void LeeTeclado(Botones botones, bool despacha)
        {
            while (!salir)
            {
                ConsoleKeyInfo k;
                try { k = Console.ReadKey(true); } catch { return; }
                if (k.Key == ConsoleKey.Escape || (k.Key == ConsoleKey.Q && (k.Modifiers & ConsoleModifiers.Control) != 0)) { salir = true; pulsos.Add(Boton.Ninguno); return; }
                if (!despacha) continue;   // (solo para que la terminal no escriba lo que se teclea)
                Boton b = botones.FuncionDe((int)k.Key);
                if (b != Boton.Ninguno) pulsos.Add(b);
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
