// LA ROCOLA EN LA PANTALLA NATIVA (Program.cs: lo que en XP es Application.Run(new Form1())). Va aparte de PantallaRocola.cs porque es
// lo unico de la pantalla que conoce a la rocola (las pruebas de la pantalla compilan PantallaRocola.cs y Reloj.cs sin ella).
// TRANSICION: Form1 aun hereda la ventana de la capa de Plataforma (Ventanas.Form.cs), que la ensena con los pasos de WinForms
// (HandleCreated, Resize, Load, Visible, Activated y Shown en la cola: lo que fijan las 41 pruebas), avisa al cerrarse (FormClosed) y
// la cierra cuando el sistema se apaga (Escritorio.FinDeSesion: FormClosing y FormClosed con CloseReason.WindowsShutDown). Cuando Form1
// deje de heredar (la herencia va en otra tanda), esas tres cosas seran llamadas directas a la rocola, aqui. Lo mismo las teclas: hoy la
// capa decide quien tiene el foco y el orden de WinForms y llama a lo de la rocola (Form1.ProcessCmdKey y OnKeyUp, nativos desde
// ventanas2: Escritorio.TeclaDeMando y TeclaArriba); sin la capa, la pantalla se las dara directo.
using System;
using System.Threading;

namespace Rocola
{
    internal static partial class PantallaRocola
    {
        internal static void Corre(Form1 rocola)
        {
            Hilo = Thread.CurrentThread;
            PonContexto();   // SynchronizationContext.Current de este hilo: lo que otros hilos le mandan con Post llega como encargo (PantallaRocola.Teclado.cs)
            Principal = rocola;
            AlApagar = Escritorio.FinDeSesion;   // (transicion) WM_QUERYENDSESSION y WM_ENDSESSION a las ventanas de la capa
            bool cerrada = false;
            rocola.FormClosed += delegate { cerrada = true; };   // (transicion) el aviso de la capa al cerrarse
            rocola.Visible = true;   // (transicion) la capa la ensena con los pasos de WinForms
            Corre(delegate { return !cerrada && !Saliendo && !rocola.IsDisposed; });
        }
    }
}
