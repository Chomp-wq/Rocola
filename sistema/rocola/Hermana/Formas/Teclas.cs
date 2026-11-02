// Las teclas y los tipos sencillos de WinForms, con los mismos valores que en Windows (el codigo de XP los compara y los guarda).
using System;
using System.Drawing;

namespace System.Windows.Forms
{
    [Flags]
    public enum Keys
    {
        KeyCode = 0xFFFF, Modifiers = unchecked((int)0xFFFF0000), None = 0,
        LButton = 1, RButton = 2, Cancel = 3, MButton = 4, XButton1 = 5, XButton2 = 6, Back = 8, Tab = 9, LineFeed = 10, Clear = 12,
        Return = 13, Enter = 13, ShiftKey = 16, ControlKey = 17, Menu = 18, Pause = 19, Capital = 20, CapsLock = 20, Escape = 27,
        Space = 32, Prior = 33, PageUp = 33, Next = 34, PageDown = 34, End = 35, Home = 36, Left = 37, Up = 38, Right = 39, Down = 40,
        Select = 41, Print = 42, Execute = 43, Snapshot = 44, PrintScreen = 44, Insert = 45, Delete = 46, Help = 47,
        D0 = 48, D1, D2, D3, D4, D5, D6, D7, D8, D9,
        A = 65, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        LWin = 91, RWin = 92, Apps = 93, Sleep = 95,
        NumPad0 = 96, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
        Multiply = 106, Add = 107, Separator = 108, Subtract = 109, Decimal = 110, Divide = 111,
        F1 = 112, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, F13, F14, F15, F16, F17, F18, F19, F20, F21, F22, F23, F24,
        NumLock = 144, Scroll = 145, LShiftKey = 160, RShiftKey = 161, LControlKey = 162, RControlKey = 163, LMenu = 164, RMenu = 165,
        OemSemicolon = 186, Oemplus = 187, Oemcomma = 188, OemMinus = 189, OemPeriod = 190, OemQuestion = 191, Oemtilde = 192,
        OemOpenBrackets = 219, OemPipe = 220, OemCloseBrackets = 221, OemQuotes = 222, Oem8 = 223, OemBackslash = 226,
        Shift = 0x10000, Control = 0x20000, Alt = 0x40000,
    }
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
    public enum UnhandledExceptionMode { Automatic, ThrowException, CatchException }
    public enum ImageLayout { None, Tile, Center, Stretch, Zoom }
    public enum RightToLeft { No, Yes, Inherit }
    [Flags]
    public enum TextFormatFlags
    {
        Default = 0, Left = 0, Top = 0, GlyphOverhangPadding = 0, HorizontalCenter = 1, Right = 2, VerticalCenter = 4, Bottom = 8,
        WordBreak = 16, SingleLine = 32, ExpandTabs = 64, NoClipping = 256, ExternalLeading = 512, NoPrefix = 2048, Internal = 4096,
        TextBoxControl = 8192, PathEllipsis = 16384, EndOfLine = 32768, EndEllipsis = 32768, ModifyString = 65536, RightToLeft = 131072,
        WordEllipsis = 262144, NoFullWidthCharacterBreak = 524288, HidePrefix = 1048576, PrefixOnly = 2097152, PreserveGraphicsClipping = 16777216,
        PreserveGraphicsTranslateTransform = 33554432, NoPadding = 268435456, LeftAndRightPadding = 536870912,
    }

    public struct Message
    {
        public IntPtr HWnd { get; set; }
        public int Msg { get; set; }
        public IntPtr WParam { get; set; }
        public IntPtr LParam { get; set; }
        public IntPtr Result { get; set; }
        public static Message Create(IntPtr hWnd, int msg, IntPtr wparam, IntPtr lparam)
        { Message m = new Message(); m.HWnd = hWnd; m.Msg = msg; m.WParam = wparam; m.LParam = lparam; return m; }
    }

    public delegate void KeyEventHandler(object sender, KeyEventArgs e);
    public delegate void KeyPressEventHandler(object sender, KeyPressEventArgs e);
    public delegate void MouseEventHandler(object sender, MouseEventArgs e);
    public delegate void PaintEventHandler(object sender, PaintEventArgs e);
    public delegate void FormClosingEventHandler(object sender, FormClosingEventArgs e);
    public delegate void FormClosedEventHandler(object sender, FormClosedEventArgs e);
    public delegate void ControlEventHandler(object sender, ControlEventArgs e);
    public delegate void MethodInvoker();

    public class KeyEventArgs : EventArgs
    {
        private readonly Keys datos;
        public KeyEventArgs(Keys keyData) { datos = keyData; }
        public bool Alt { get { return (datos & Keys.Alt) == Keys.Alt; } }
        public bool Control { get { return (datos & Keys.Control) == Keys.Control; } }
        public bool Shift { get { return (datos & Keys.Shift) == Keys.Shift; } }
        public bool Handled { get; set; }
        public Keys KeyCode { get { return datos & Keys.KeyCode; } }
        public int KeyValue { get { return (int)(datos & Keys.KeyCode); } }
        public Keys KeyData { get { return datos; } }
        public Keys Modifiers { get { return datos & Keys.Modifiers; } }
        public bool SuppressKeyPress { get { return supr; } set { supr = value; Handled = value; } }
        private bool supr;
    }
    public class KeyPressEventArgs : EventArgs
    {
        public KeyPressEventArgs(char keyChar) { KeyChar = keyChar; }
        public char KeyChar { get; set; }
        public bool Handled { get; set; }
    }
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
}
