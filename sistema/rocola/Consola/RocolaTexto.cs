// LA ROCOLA EN MODO TEXTO (Rocola OS, etapa 2, mientras llega la pantalla grafica): la rocola de verdad, con sus botones, en la
// consola (tty1 en Rocola OS, o cualquier terminal). Los discos por letra; ARRIBA/ABAJO mueven, IZQUIERDA/DERECHA cambian de letra
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
    internal sealed class RocolaTexto
    {
        private readonly Form1 motor;
        private readonly string dispositivo;
        private readonly BlockingCollection<Boton> pulsos = new BlockingCollection<Boton>();
        private List<Form1.Disco> discos;
        private readonly List<char> letras = new List<char>();
        private readonly Dictionary<char, List<Form1.Disco>> porLetra = new Dictionary<char, List<Form1.Disco>>();
        private int letra, cursorDisco, cursorCancion;
        private Form1.Disco dentro;            // el disco abierto (null: la lista de discos)
        private string aviso = ""; private int avisoHasta;
        private SalidaAlsa salida;
        private bool saltar, salir;
        private string pantallaAntes;

        internal RocolaTexto(Form1 motor, string dispositivo) { this.motor = motor; this.dispositivo = dispositivo; }

        internal int Corre()
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
            using (Botones botones = new Botones())
            {
                botones.Configura(motor.ArchivoBotones);
                botones.Pulsado += delegate (Boton b, int vk) { if (b != Boton.Ninguno) pulsos.Add(b); };
                int n = botones.Escucha();
                Aviso((botones.DeArchivo ? "Botones de rocola_teclas.txt" : "Botones de teclado (sin rocola_teclas.txt)") + "; " + n + " aparatos de entrada");
                Thread teclado = null;
                if (!Console.IsInputRedirected)
                {
                    teclado = new Thread(delegate () { LeeTeclado(botones); }) { IsBackground = true, Name = "teclado de la terminal" };
                    teclado.Start();
                }
                Console.Write("\x1b[?25l");   // sin cursor
                try
                {
                    while (!salir)
                    {
                        Boton b;
                        if (pulsos.TryTake(out b, 100)) Maneja(b);
                        Suena();
                        Dibuja();
                    }
                }
                finally
                {
                    if (salida != null) { salida.Dispose(); salida = null; motor.AcaboLaSalida(); }
                    motor.GuardaEstado();
                    Console.Write("\x1b[?25h\x1b[0m\n");
                }
            }
            return 0;
        }

        private void Aviso(string t) { aviso = t; avisoHasta = Environment.TickCount + 4000; }

        // El teclado de la terminal: sus codigos de tecla son los de Windows (ConsoleKey), asi que valen las mismas funciones. Esc o Q: salir.
        private void LeeTeclado(Botones botones)
        {
            while (!salir)
            {
                ConsoleKeyInfo k;
                try { k = Console.ReadKey(true); } catch { return; }
                if (k.Key == ConsoleKey.Escape || (k.Key == ConsoleKey.Q && (k.Modifiers & ConsoleModifiers.Control) != 0)) { salir = true; pulsos.Add(Boton.Ninguno); return; }
                Boton b = botones.FuncionDe((int)k.Key);
                if (b != Boton.Ninguno) pulsos.Add(b);
            }
        }

        private List<Form1.Disco> DiscosDeLetra() { return letras.Count == 0 ? new List<Form1.Disco>() : porLetra[letras[letra]]; }

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
                int n = dentro.Rutas.Count, pagina = Math.Max(1, FilasLista());
                if (b == Boton.Arriba) cursorCancion = (cursorCancion + n - 1) % n;
                else if (b == Boton.Abajo) cursorCancion = (cursorCancion + 1) % n;
                else if (b == Boton.Izquierda) cursorCancion = Math.Max(0, cursorCancion - pagina);
                else if (b == Boton.Derecha) cursorCancion = Math.Min(n - 1, cursorCancion + pagina);
                else if (b == Boton.Regresar) dentro = null;
                else if (b == Boton.Poner)
                {
                    string ruta = dentro.Rutas[cursorCancion];
                    if (!motor.MideSiFalta(ruta)) { }   // como el repaso de XP; si no se pudo, suena con la regla de las no medidas
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

        private static int Filas() { try { return Math.Max(12, Console.WindowHeight); } catch { return 25; } }
        private static int Columnas() { try { return Math.Max(40, Console.WindowWidth); } catch { return 80; } }
        private static int FilasLista() { return Filas() - 7; }
        private static string Corta(string s, int n) { s = s ?? ""; return s.Length <= n ? s.PadRight(n) : s.Substring(0, n - 1) + "…"; }

        private void Dibuja()
        {
            int ancho = Columnas(), alto = FilasLista();
            StringBuilder sb = new StringBuilder();
            string sonando = motor.Sonando;
            sb.Append("\x1b[1;37;44m").Append(Corta(" ROCOLA OS   Creditos: " + motor.Creditos + "   En la cola: " + motor.EnCola, ancho)).Append("\x1b[0m\n");
            sb.Append(Corta(" Suena: " + (sonando != null ? Path.GetFileNameWithoutExtension(sonando) : "(nada)"), ancho)).Append('\n');
            if (dentro == null)
            {
                StringBuilder l = new StringBuilder(" Letra: ");
                for (int i = 0; i < letras.Count; i++) l.Append(i == letra ? "[" + letras[i] + "]" : " " + letras[i] + " ");
                sb.Append(Corta(l.ToString(), ancho)).Append('\n').Append(new string('-', ancho)).Append('\n');
                List<Form1.Disco> ds = DiscosDeLetra();
                int desde = Math.Max(0, Math.Min(cursorDisco - alto / 2, ds.Count - alto));
                for (int i = 0; i < alto; i++)
                {
                    int k = desde + i;
                    if (k < ds.Count) sb.Append(k == cursorDisco ? "\x1b[7m" : "").Append(Corta(" " + ds[k] + "  (" + ds[k].Rutas.Count + ")", ancho)).Append("\x1b[0m");
                    else sb.Append(new string(' ', ancho));
                    sb.Append('\n');
                }
                sb.Append(Corta(" ARRIBA/ABAJO: disco   IZQ/DER: letra   PONER: entrar   MONEDA   SALTAR", ancho)).Append('\n');
            }
            else
            {
                sb.Append(Corta(" Disco: " + dentro, ancho)).Append('\n').Append(new string('-', ancho)).Append('\n');
                int n = dentro.Rutas.Count, desde = Math.Max(0, Math.Min(cursorCancion - alto / 2, n - alto));
                for (int i = 0; i < alto; i++)
                {
                    int k = desde + i;
                    if (k < n) sb.Append(k == cursorCancion ? "\x1b[7m" : "").Append(Corta(" " + (k + 1).ToString().PadLeft(3) + ". " + dentro.Titulos[k], ancho)).Append("\x1b[0m");
                    else sb.Append(new string(' ', ancho));
                    sb.Append('\n');
                }
                sb.Append(Corta(" ARRIBA/ABAJO: cancion   PONER: pedirla (1 credito)   REGRESAR: a los discos", ancho)).Append('\n');
            }
            sb.Append("\x1b[1;33m").Append(Corta(unchecked(Environment.TickCount - avisoHasta) < 0 ? " " + aviso : "", ancho)).Append("\x1b[0m");
            string p = sb.ToString();
            if (p == pantallaAntes) return;
            pantallaAntes = p;
            Console.Write("\x1b[H" + p);
        }
    }
}
