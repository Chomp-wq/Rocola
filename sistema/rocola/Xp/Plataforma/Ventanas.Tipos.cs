// LAS VENTANAS DE LA ROCOLA, NATIVAS EN LINUX (traduccion del programa de XP, fase 1). En XP los controles son de WinForms; aqui son
// clases propias de la rocola con los mismos nombres y las mismas medidas (para que cada archivo traducido quede como el de XP), sin
// Windows debajo: no hay mensajes de Windows ni ventanas del sistema. El dibujo de cada control es el codigo de XP traducido
// (Dibujo2D, SkiaSharp), la pantalla la compone OpenGL ES 2 por KMS (Escritorio.cs) y las teclas llegan por evdev.
//
// Este archivo: los tipos sencillos, con los mismos valores que en Windows. (Port nativo, ventanas2 T: las teclas ya no son de la capa:
// Keys es Tecla, el Message de ProcessCmdKey es PulsoTecla y KeyPressEventArgs es PulsoCaracter, en Nativo/Tecla.cs. KeyEventArgs y
// KeyPressEventArgs quedan aqui como nombres de lo nativo para las piezas que aun heredan de la capa -las de Ctrl+F3, las vistas- y se van
// con ellas.)
using System;
using System.Drawing;
using Rocola.Dibujo2D;   // GDI+ de la transicion (Dibujo2D.cs); ya no es global (port nativo, D1)

namespace Rocola.Ventanas
{
    [Flags] public enum MouseButtons { None = 0, Left = 0x100000, Right = 0x200000, Middle = 0x400000, XButton1 = 0x800000, XButton2 = 0x1000000 }
    [Flags]
    public enum ControlStyles
    {
        ContainerControl = 1, UserPaint = 2, Opaque = 4, ResizeRedraw = 16, FixedWidth = 32, FixedHeight = 64, StandardClick = 256,
        Selectable = 512, UserMouse = 1024, SupportsTransparentBackColor = 2048, StandardDoubleClick = 4096, AllPaintingInWmPaint = 8192,
        CacheText = 16384, EnableNotifyMessage = 32768, DoubleBuffer = 65536, OptimizedDoubleBuffer = 131072, UseTextForAccessibility = 262144,
    }
    public enum FormBorderStyle { None, FixedSingle, Fixed3D, FixedDialog, Sizable, FixedToolWindow, SizableToolWindow }
    public enum FormWindowState { Normal, Minimized, Maximized }
    public enum FormStartPosition { Manual, CenterScreen, WindowsDefaultLocation, WindowsDefaultBounds, CenterParent }
    public enum DockStyle { None, Top, Bottom, Left, Right, Fill }
    [Flags] public enum AnchorStyles { None = 0, Top = 1, Bottom = 2, Left = 4, Right = 8 }
    public enum BorderStyle { None, FixedSingle, Fixed3D }
    public enum FlatStyle { Flat, Popup, Standard, System }
    public enum AutoScaleMode { None, Font, Dpi, Inherit }
    public enum CloseReason { None, WindowsShutDown, MdiFormClosing, UserClosing, TaskManagerClosing, FormOwnerClosing, ApplicationExitCall }
    public enum DialogResult { None, OK, Cancel, Abort, Retry, Ignore, Yes, No }
    public enum MessageBoxButtons { OK, OKCancel, AbortRetryIgnore, YesNoCancel, YesNo, RetryCancel }
    public enum MessageBoxIcon { None = 0, Hand = 16, Error = 16, Stop = 16, Question = 32, Exclamation = 48, Warning = 48, Asterisk = 64, Information = 64 }
    public enum MessageBoxDefaultButton { Button1 = 0, Button2 = 256, Button3 = 512 }
    public enum ImageLayout { None, Tile, Center, Stretch, Zoom }
    public enum RightToLeft { No, Yes, Inherit }
    public enum SelectionMode { None, One, MultiSimple, MultiExtended }
    public enum AccessibleRole { Default = -1, None = 0, Grouping = 20, StaticText = 41, Text = 42, PushButton = 43, CheckButton = 44, RadioButton = 45, Slider = 51, PageTab = 37, PageTabList = 60, List = 33, ListItem = 34, Client = 10, Window = 9, Pane = 16 }
    // la alineacion del texto de una etiqueta (en .NET de Linux ya no esta: era de System.Drawing de Windows), con sus mismos valores
    public enum ContentAlignment { TopLeft = 1, TopCenter = 2, TopRight = 4, MiddleLeft = 16, MiddleCenter = 32, MiddleRight = 64, BottomLeft = 256, BottomCenter = 512, BottomRight = 1024 }
    [Flags] public enum BoundsSpecified { None = 0, X = 1, Y = 2, Location = 3, Width = 4, Height = 8, Size = 12, All = 15 }

    public struct Padding
    {
        public int Left, Top, Right, Bottom;
        public Padding(int all) { Left = Top = Right = Bottom = all; }
        public Padding(int left, int top, int right, int bottom) { Left = left; Top = top; Right = right; Bottom = bottom; }
        public int All { get { return Left; } set { Left = Top = Right = Bottom = value; } }
        public int Horizontal { get { return Left + Right; } }
        public int Vertical { get { return Top + Bottom; } }
        public static readonly Padding Empty = new Padding(0);
    }

    public delegate void KeyEventHandler(object sender, KeyEventArgs e);
    public delegate void KeyPressEventHandler(object sender, KeyPressEventArgs e);
    public delegate void MouseEventHandler(object sender, MouseEventArgs e);
    public delegate void PaintEventHandler(object sender, PaintEventArgs e);
    public delegate void FormClosingEventHandler(object sender, FormClosingEventArgs e);
    public delegate void FormClosedEventHandler(object sender, FormClosedEventArgs e);
    public delegate void ControlEventHandler(object sender, ControlEventArgs e);
    public delegate void MethodInvoker();

    // (transicion: los avisos de tecla de las piezas que aun heredan de la capa son los pulsos nativos)
    public class KeyEventArgs : PulsoTecla { public KeyEventArgs(Tecla keyData) : base(keyData) { } }
    public class KeyPressEventArgs : PulsoCaracter { public KeyPressEventArgs(char keyChar) : base(keyChar) { } }
    public class MouseEventArgs : EventArgs
    {
        public MouseEventArgs(MouseButtons button, int clicks, int x, int y, int delta) { Button = button; Clicks = clicks; X = x; Y = y; Delta = delta; }
        public MouseButtons Button { get; }
        public int Clicks { get; }
        public int X { get; }
        public int Y { get; }
        public int Delta { get; }
        public Point Location { get { return new Point(X, Y); } }
    }
    // LA RUEDA (WmMouseWheel de WinForms): si nadie la atiende (Handled), sube al padre (DefWindowProc la pasa)
    public class HandledMouseEventArgs : MouseEventArgs
    {
        public HandledMouseEventArgs(MouseButtons button, int clicks, int x, int y, int delta) : base(button, clicks, x, y, delta) { }
        public bool Handled { get; set; }
    }
    public class PaintEventArgs : EventArgs, IDisposable
    {
        public PaintEventArgs(Graphics graphics, Rectangle clipRect) { Graphics = graphics; ClipRectangle = clipRect; }
        public Graphics Graphics { get; }
        public Rectangle ClipRectangle { get; }
        public void Dispose() { }
    }
    public class FormClosingEventArgs : System.ComponentModel.CancelEventArgs
    {
        public FormClosingEventArgs(CloseReason closeReason, bool cancel) : base(cancel) { CloseReason = closeReason; }
        public CloseReason CloseReason { get; }
    }
    public class FormClosedEventArgs : EventArgs
    {
        public FormClosedEventArgs(CloseReason closeReason) { CloseReason = closeReason; }
        public CloseReason CloseReason { get; }
    }
    public class ControlEventArgs : EventArgs
    {
        public ControlEventArgs(Control control) { Control = control; }
        public Control Control { get; }
    }
    public interface IWin32Window { IntPtr Handle { get; } }
    public interface IButtonControl { DialogResult DialogResult { get; set; } void NotifyDefault(bool value); void PerformClick(); }
}
