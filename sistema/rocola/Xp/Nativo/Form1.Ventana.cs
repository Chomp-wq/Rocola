// LA ROCOLA Y SU VENTANA (port nativo, oleada 8, grupo form1-sin-form: W7). En XP Form1 heredaba de Form (WinForms); aqui no hereda de
// nada: es la duena de la ventana que le abre la pantalla (PantallaRocola.AbreVentana, Nativo/PantallaRocola.Rocola.cs) y de las hojas
// (Ctrl+F2, Ctrl+F3, los avisos: MuestraModal(this)). Este archivo declara, con los nombres con que el codigo de XP los usa (asi sus
// lineas no cambian), SOLO lo que la rocola de XP le usaba a Form, y cada cosa se la pide a su ventana:
//   ClientSize, Size, MinimumSize, Width y Height, BackColor, Text      la medida, el color de fondo y el titulo de la ventana
//   IsHandleCreated, IsDisposed, Disposing                              si la ventana existe, si ya se desecho o se esta desechando
//   Enabled, ContainsFocus, Focus()                                     el foco del teclado (ForceFocus y su vigia, el asistente)
//   Invalidate() e Invalidate(true)                                     repintar el fondo de la ventana (y, con true, sus vistas)
//   Close(), Dispose()                                                  cerrar (con la razon UserClosing) y desechar
//   FormClosed                                                          el aviso al cerrarse (lo lanzaba base.OnFormClosed)
//   KeyPress                                                            el aviso de cada caracter (Form_KeyPress, con KeyPreview)
//   Font                                                                la letra de la ventana, que XP nunca cambia: la de fabrica de
//                                                                       WinForms (Control.DefaultFont: Microsoft Sans Serif de 8,25 puntos)
// y le da a la ventana lo que en XP eran los On* que Form1 sobrescribe (OnShown, OnResize, OnPaintBackground, OnFormClosing, OnFormClosed,
// Dispose(bool)) y lo que la pantalla ya le daba directo (ProcessCmdKey y OnKeyUp). Los encargos de otros hilos (BeginInvoke a la ventana
// de la rocola) viajan con la vida de su ventana (IDuenoDeEncargos).
using System;
using System.Drawing;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1 : IDuenoDeEncargos, IDisposable
    {
        private IVentanaDeLaRocola ventana;
        internal IVentanaDeLaRocola Ventana { get { return ventana; } }
        // Donde en XP ya corrio el constructor de Form (antes del cuerpo del de Form1): la ventana existe como objeto, sin crear ni ensenar
        private void AbreVentana()
        {
            AvisosDeLaVentana a = new AvisosDeLaVentana();
            a.Rocola = this;
            a.Ensenada = OnShown;
            a.CambioDeMedida = OnResize;
            a.PintaFondo = OnPaintBackground;
            a.Cerrandose = OnFormClosing;
            a.Cerrada = OnFormClosed;
            a.ProcesaMando = ProcessCmdKey;
            a.Caracter = delegate (PulsoCaracter e) { Action<object, PulsoCaracter> k = KeyPress; if (k != null) k(this, e); };
            a.TeclaArriba = OnKeyUp;
            a.Desecha = Dispose;
            ventana = PantallaRocola.AbreVentana(a);
        }

        long IDuenoDeEncargos.Vida { get { return ventana.Vida; } }

        internal string Text { get { return ventana.Titulo; } set { ventana.Titulo = value; } }
        internal Size Size { get { return ventana.Medida; } set { ventana.Medida = value; } }
        internal Size MinimumSize { set { ventana.MedidaMinima = value; } }
        internal Size ClientSize { get { return ventana.Cliente; } set { ventana.Cliente = value; } }
        internal int Width { get { return ventana.Medida.Width; } }
        internal int Height { get { return ventana.Medida.Height; } }
        internal Color BackColor { get { return ventana.Fondo; } set { ventana.Fondo = value; } }
        internal bool IsHandleCreated { get { return ventana.Creada; } }
        internal bool IsDisposed { get { return ventana.Desechada; } }
        internal bool Disposing { get { return ventana.Desechandose; } }
        internal bool Enabled { get { return ventana.Habilitada; } }
        internal bool ContainsFocus { get { return ventana.ContieneElFoco; } }
        internal bool Focus() { return ventana.Enfoca(); }
        internal void Invalidate() { ventana.Invalida(false); }
        internal void Invalidate(bool invalidateChildren) { ventana.Invalida(invalidateChildren); }
        internal void Close() { ventana.Cierra(); }
        public void Dispose() { ventana.Desecha(); }
        internal Rocola.Nativo.Letra Font { get { return CajaPieza.LetraDeFabrica; } }

        internal event Action FormClosed;
        private void AvisaFormClosed() { Action a = FormClosed; if (a != null) a(); }
        private event Action<object, PulsoCaracter> KeyPress;
    }
}
