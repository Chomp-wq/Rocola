// LA ROCOLA EN LA PANTALLA NATIVA (Program.cs: lo que en XP es Application.Run(new Form1())). Va aparte de PantallaRocola.cs porque es
// lo unico de la pantalla que conoce a la rocola (las pruebas de la pantalla compilan PantallaRocola.cs y Reloj.cs sin ella).
// TRANSICION: Form1 aun hereda la ventana de la capa de Plataforma (Ventanas.Form.cs), que la ensena con los pasos de WinForms
// (HandleCreated, Resize, Load, Visible, Activated y Shown en la cola: lo que fijan las 41 pruebas), avisa al cerrarse (FormClosed) y
// la cierra cuando el sistema se apaga (Escritorio.FinDeSesion: FormClosing y FormClosed con CloseReason.WindowsShutDown). Cuando Form1
// deje de heredar (la herencia va en otra tanda), esas tres cosas seran llamadas directas a la rocola, aqui.
using System;
using System.Threading;

namespace Rocola
{
    internal static partial class PantallaRocola
    {
        internal static void Corre(Form1 rocola)
        {
            Hilo = Thread.CurrentThread;
            ContextoVentana.Pon();   // (transicion) SynchronizationContext.Current de este hilo: lo que otros hilos le mandan con Post llega como encargo
            Principal = rocola;
            AlApagar = Escritorio.FinDeSesion;   // (transicion) WM_QUERYENDSESSION y WM_ENDSESSION a las ventanas de la capa
            bool cerrada = false;
            rocola.FormClosed += delegate { cerrada = true; };   // (transicion) el aviso de la capa al cerrarse
            rocola.Visible = true;   // (transicion) la capa la ensena con los pasos de WinForms
            Corre(delegate { return !cerrada && !Saliendo && !rocola.IsDisposed; });
        }
    }
}
