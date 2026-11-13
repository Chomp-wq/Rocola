// LA ROCOLA EN LA PANTALLA NATIVA (Program.cs: lo que en XP es Application.Run(new Form1())). Va aparte de PantallaRocola.cs porque es
// lo unico de la pantalla que conoce a la rocola (las pruebas de la pantalla compilan PantallaRocola.cs y Reloj.cs sin ella).
// LA VENTANA DE LA ROCOLA (port nativo, oleada 8, grupo form1-sin-form: W7). Form1 ya no hereda de nada: es la duena de su ventana, y lo
// que XP le usaba a Form (Nativo/Form1.Ventana.cs: ClientSize, IsDisposed, Invalidate, Focus, Close...) se lo pide a la ventana que la
// pantalla le abre (IVentanaDeLaRocola), y la ventana le avisa de lo que en XP eran los On* de Form (AvisosDeLaVentana: OnShown,
// OnResize, OnPaintBackground, OnFormClosing, OnFormClosed, ProcessCmdKey, KeyPress, OnKeyUp y Dispose). Ninguna de las dos imita a Form:
// solo lo que la rocola de XP usa, con nombres propios.
// TRANSICION: la ventana que se abre es todavia una de la capa de Plataforma (Anfitrion.cs, VentanaDeLaRocola: una Form de la capa que
// la ensena con los pasos de WinForms -HandleCreated, Resize, Load, Visible, Activated y Shown en la cola: lo que fijan las 41 pruebas-,
// reparte las teclas y el raton, pinta sus vistas en sus huecos y la cierra con su razon, tambien cuando el sistema se apaga:
// Escritorio.FinDeSesion, por PantallaRocola.LaCapa). Lo pone el anfitrion en AbreEnLaCapa. Sin ella, la ventana es la pantalla misma (la
// lista nativa de PantallaRocola.Vistas.cs, con PantallaRocola.Teclas): lo que queda de la oleada 8 (PORT_form1-sin-form.md).
using System;
using System.Drawing;
using System.Threading;
using SkiaSharp;

namespace Rocola
{
    // LO QUE LA ROCOLA LE PIDE A SU VENTANA (lo que XP usaba de Form, Nativo/Form1.Ventana.cs)
    internal interface IVentanaDeLaRocola
    {
        string Titulo { get; set; }          // Text (las trazas nombran la ventana con el)
        Size Medida { get; set; }            // Size (por fuera; sin borde, igual que el cliente)
        Size MedidaMinima { set; }           // MinimumSize
        Size Cliente { get; set; }           // ClientSize
        Color Fondo { get; set; }            // BackColor
        void SinBorde();                     // FormBorderStyle = None
        void Maximiza();                     // WindowState = Maximized (antes de ensenarse: se aplica al crearla)
        bool Minimizada { get; }             // WindowState == Minimized
        bool Creada { get; }                 // IsHandleCreated
        bool Desechada { get; }              // IsDisposed
        bool Desechandose { get; }           // Disposing
        long Vida { get; }                   // la de sus encargos (IDuenoDeEncargos: BeginInvoke a su Handle)
        bool Habilitada { get; }             // Enabled
        bool ContieneElFoco { get; }         // ContainsFocus
        bool Enfoca();                       // Focus()
        void Invalida(bool conHijas);        // Invalidate() e Invalidate(true)
        void PintaFondoDeFabrica(SKCanvas g);   // base.OnPaintBackground: el cliente con el color de fondo
        void Ensena();                       // Visible = true (Application.Run la ensena)
        void Cierra();                       // Close()
        void Desecha();                      // Dispose()
        void DesechaLoSuyo(bool disposing);  // base.Dispose(disposing): lo de la ventana, despues de lo de la rocola
    }
    // LO QUE LA VENTANA LE AVISA A LA ROCOLA (los On* de Form que XP sobrescribe y los avisos que escucha); lo llena Form1.Ventana.cs
    internal sealed class AvisosDeLaVentana
    {
        internal object Rocola;                        // la rocola (Form1): la que el codigo de XP nombra como «this»
        internal Action Ensenada;                      // OnShown
        internal Action CambioDeMedida;                // OnResize (despues de lo de fabrica)
        internal Action<SKCanvas> PintaFondo;          // OnPaintBackground
        internal Action<RazonCierre> Cerrandose;       // OnFormClosing (la rocola nunca lo impide)
        internal Action<RazonCierre> Cerrada;          // OnFormClosed
        internal PantallaRocola.Mando ProcesaMando;    // ProcessCmdKey
        internal Action<PulsoCaracter> Caracter;       // KeyPress (con KeyPreview: antes que nadie)
        internal Action<PulsoTecla> TeclaArriba;       // OnKeyUp
        internal Action<bool> Desecha;                 // Dispose(bool)
    }

    internal static partial class PantallaRocola
    {
        // (transicion) quien abre la ventana de la rocola mientras sea una de la capa (Plataforma/Anfitrion.cs)
        internal static Func<AvisosDeLaVentana, IVentanaDeLaRocola> AbreEnLaCapa;
        internal static IVentanaDeLaRocola AbreVentana(AvisosDeLaVentana avisos)
        {
            Func<AvisosDeLaVentana, IVentanaDeLaRocola> f = AbreEnLaCapa;
            if (f != null) return f(avisos);
            throw new NotSupportedException("La ventana de la rocola sin la capa aun no esta (PORT_form1-sin-form.md).");
        }

        internal static void Corre(Form1 rocola)
        {
            Hilo = Thread.CurrentThread;
            PonContexto();   // SynchronizationContext.Current de este hilo: lo que otros hilos le mandan con Post llega como encargo (PantallaRocola.Teclado.cs)
            Principal = rocola;
            AlApagar = delegate { ILaCapa c = LaCapa; if (c != null) c.FinDeSesion(); };   // (transicion) WM_QUERYENDSESSION y WM_ENDSESSION a las ventanas de la capa (Escritorio.FinDeSesion, por PantallaRocola.Vistas.cs)
            bool cerrada = false;
            rocola.FormClosed += delegate { cerrada = true; };   // el aviso de la rocola al cerrarse (el FormClosed de XP)
            rocola.Ventana.Ensena();   // (Application.Run: Visible = true) la ventana se ensena con los pasos de WinForms
            Corre(delegate { return !cerrada && !Saliendo && !rocola.IsDisposed; });
        }
    }
}
