// Application, la pantalla, el cursor, los mensajes y TextRenderer.
using System;
using System.Drawing;
using System.Threading;

namespace System.Windows.Forms
{
    public sealed class Application
    {
        private Application() { }
        internal static volatile bool Saliendo;
        public static event EventHandler Idle;
        public static event ThreadExceptionEventHandler ThreadException;
        public static event EventHandler ApplicationExit;
        internal static void LlamaIdle() { EventHandler h = Idle; if (h != null) try { h(null, EventArgs.Empty); } catch (Exception ex) { Avisa(ex); } }
        internal static void Avisa(Exception ex)
        {
            ThreadExceptionEventHandler h = ThreadException;
            if (h != null) { try { h(null, new ThreadExceptionEventArgs(ex)); } catch (Exception otra) { Console.Error.WriteLine("[Formas] " + otra); } }
            else Console.Error.WriteLine("[Formas] " + ex);
        }
        public static string StartupPath { get { return AppContext.BaseDirectory.TrimEnd('/', '\\'); } }
        public static string ExecutablePath { get { return Environment.ProcessPath ?? System.IO.Path.Combine(StartupPath, "rocola"); } }
        public static string ProductName { get { return "Rocola"; } }
        public static string ProductVersion { get { return "1.0"; } }
        public static void EnableVisualStyles() { }
        public static void SetCompatibleTextRenderingDefault(bool defaultValue) { }
        public static void SetUnhandledExceptionMode(UnhandledExceptionMode mode) { }
        public static void SetUnhandledExceptionMode(UnhandledExceptionMode mode, bool threadScope) { }
        public static void DoEvents() { Escritorio.Vuelta(false); }
        public static void Exit() { Saliendo = true; Escritorio.Despierta(); }
        public static void ExitThread() { Exit(); }
        public static FormCollection OpenForms { get { return new FormCollection(Escritorio.Formas); } }
        public static void Run(Form mainForm)
        {
            Escritorio.HiloUi = Thread.CurrentThread; ContextoVentana.Pon();
            bool cerrada = false;
            mainForm.FormClosed += delegate { cerrada = true; };
            mainForm.Visible = true;
            Escritorio.Bucle(delegate { return !cerrada && !Saliendo && !mainForm.IsDisposed; });
            ApplicationExit?.Invoke(null, EventArgs.Empty);
        }
        public static void Run() { Escritorio.HiloUi = Thread.CurrentThread; Escritorio.Bucle(delegate { return !Saliendo; }); }
    }
    public class FormCollection : System.Collections.ReadOnlyCollectionBase
    {
        internal FormCollection(Form[] f) { foreach (Form x in f) InnerList.Add(x); }
        public Form this[int index] { get { return (Form)InnerList[index]; } }
    }

    public class Screen
    {
        private Screen() { }
        public static Screen PrimaryScreen { get { return new Screen(); } }
        public static Screen[] AllScreens { get { return new Screen[] { PrimaryScreen }; } }
        public static Screen FromControl(Control c) { return PrimaryScreen; }
        public static Screen FromHandle(IntPtr h) { return PrimaryScreen; }
        public static Screen FromPoint(Point p) { return PrimaryScreen; }
        public Rectangle Bounds { get { return new Rectangle(Point.Empty, Escritorio.Pantalla); } }
        public Rectangle WorkingArea { get { return Bounds; } }
        public string DeviceName { get { return "\\\\.\\DISPLAY1"; } }
        public bool Primary { get { return true; } }
        public int BitsPerPixel { get { return 32; } }
    }

    public sealed class Cursor : IDisposable
    {
        private readonly string nombre;
        internal Cursor(string n) { nombre = n; }
        public static Point Position { get; set; }
        public static Cursor Current { get; set; }
        public static void Hide() { }
        public static void Show() { }
        public static Rectangle Clip { get; set; }
        public void Dispose() { }
        public override string ToString() { return "[Cursor: " + nombre + "]"; }
    }
    public sealed class Cursors
    {
        private Cursors() { }
        public static Cursor Default { get; } = new Cursor("Default");
        public static Cursor Hand { get; } = new Cursor("Hand");
        public static Cursor Arrow { get; } = new Cursor("Arrow");
        public static Cursor WaitCursor { get; } = new Cursor("WaitCursor");
        public static Cursor No { get; } = new Cursor("No");
    }

    // Sin nadie que haga clic en la rocola: el aviso se apunta y se da por aceptado.
    public class MessageBox
    {
        private MessageBox() { }
        public static DialogResult Show(string text) { return Show(text, ""); }
        public static DialogResult Show(string text, string caption) { Console.Error.WriteLine("[Aviso] " + caption + ": " + text); return DialogResult.OK; }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) { Show(text, caption); return buttons == MessageBoxButtons.YesNo || buttons == MessageBoxButtons.YesNoCancel ? DialogResult.Yes : DialogResult.OK; }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) { return Show(text, caption, buttons); }
        public static DialogResult Show(IWin32Window owner, string text) { return Show(text); }
        public static DialogResult Show(IWin32Window owner, string text, string caption) { return Show(text, caption); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons) { return Show(text, caption, buttons); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) { return Show(text, caption, buttons); }
    }

    // TEXTRENDERER: en Windows va por GDI (no GDI+). Aqui, por GDI+ con el margen que GDI deja a los lados (un sexto de la altura de la
    // letra, como TextRenderer sin NoPadding).
    public sealed class TextRenderer
    {
        private TextRenderer() { }
        internal static TextFormatFlags DeAlineacion(ContentAlignment a)
        {
            TextFormatFlags f = TextFormatFlags.Default;
            if ((a & (ContentAlignment.TopCenter | ContentAlignment.MiddleCenter | ContentAlignment.BottomCenter)) != 0) f |= TextFormatFlags.HorizontalCenter;
            if ((a & (ContentAlignment.TopRight | ContentAlignment.MiddleRight | ContentAlignment.BottomRight)) != 0) f |= TextFormatFlags.Right;
            if ((a & (ContentAlignment.MiddleLeft | ContentAlignment.MiddleCenter | ContentAlignment.MiddleRight)) != 0) f |= TextFormatFlags.VerticalCenter;
            if ((a & (ContentAlignment.BottomLeft | ContentAlignment.BottomCenter | ContentAlignment.BottomRight)) != 0) f |= TextFormatFlags.Bottom;
            return f;
        }
        private static StringFormat Formato(TextFormatFlags f)
        {
            StringFormat sf = new StringFormat(StringFormat.GenericDefault);
            sf.Alignment = (f & TextFormatFlags.HorizontalCenter) != 0 ? StringAlignment.Center : (f & TextFormatFlags.Right) != 0 ? StringAlignment.Far : StringAlignment.Near;
            sf.LineAlignment = (f & TextFormatFlags.VerticalCenter) != 0 ? StringAlignment.Center : (f & TextFormatFlags.Bottom) != 0 ? StringAlignment.Far : StringAlignment.Near;
            if ((f & TextFormatFlags.SingleLine) != 0 || (f & TextFormatFlags.WordBreak) == 0) sf.FormatFlags |= StringFormatFlags.NoWrap;
            if ((f & TextFormatFlags.NoClipping) != 0) sf.FormatFlags |= StringFormatFlags.NoClip;
            if ((f & (TextFormatFlags.EndEllipsis | TextFormatFlags.WordEllipsis | TextFormatFlags.PathEllipsis)) != 0) sf.Trimming = StringTrimming.EllipsisCharacter;
            else sf.Trimming = StringTrimming.None;
            sf.HotkeyPrefix = (f & TextFormatFlags.NoPrefix) != 0 ? System.Drawing.Text.HotkeyPrefix.None : System.Drawing.Text.HotkeyPrefix.Show;
            return sf;
        }
        private static int Margen(Font font, TextFormatFlags f) { return (f & TextFormatFlags.NoPadding) != 0 ? 0 : (int)Math.Ceiling(font.Height / 6.0); }
        public static void DrawText(IDeviceContext dc, string text, Font font, Rectangle bounds, Color foreColor, TextFormatFlags flags)
        {
            Graphics g = dc as Graphics; if (g == null || string.IsNullOrEmpty(text) || font == null) return;
            int m = Margen(font, flags);
            RectangleF r = new RectangleF(bounds.X + m, bounds.Y, Math.Max(0, bounds.Width - 2 * m), bounds.Height);
            using (StringFormat sf = Formato(flags)) using (SolidBrush b = new SolidBrush(foreColor)) g.DrawString(text, font, b, r, sf);
        }
        public static void DrawText(IDeviceContext dc, string text, Font font, Rectangle bounds, Color foreColor, Color backColor, TextFormatFlags flags)
        {
            Graphics g = dc as Graphics; if (g != null && backColor.A > 0) using (SolidBrush b = new SolidBrush(backColor)) g.FillRectangle(b, bounds);
            DrawText(dc, text, font, bounds, foreColor, flags);
        }
        public static void DrawText(IDeviceContext dc, string text, Font font, Rectangle bounds, Color foreColor) { DrawText(dc, text, font, bounds, foreColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); }
        public static void DrawText(IDeviceContext dc, string text, Font font, Point pt, Color foreColor) { DrawText(dc, text, font, new Rectangle(pt, new Size(100000, 100000)), foreColor, TextFormatFlags.Default); }
        public static void DrawText(IDeviceContext dc, string text, Font font, Point pt, Color foreColor, TextFormatFlags flags) { DrawText(dc, text, font, new Rectangle(pt, new Size(100000, 100000)), foreColor, flags); }
        public static Size MeasureText(string text, Font font) { return MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.Default); }
        public static Size MeasureText(string text, Font font, Size proposedSize) { return MeasureText(text, font, proposedSize, TextFormatFlags.Default); }
        public static Size MeasureText(IDeviceContext dc, string text, Font font) { return MeasureText(text, font); }
        public static Size MeasureText(IDeviceContext dc, string text, Font font, Size proposedSize, TextFormatFlags flags) { return MeasureText(text, font, proposedSize, flags); }
        private static Bitmap medidor;
        public static Size MeasureText(string text, Font font, Size proposedSize, TextFormatFlags flags)
        {
            if (font == null) return Size.Empty;
            if (string.IsNullOrEmpty(text)) return new Size(0, font.Height);
            Bitmap b = medidor ?? (medidor = new Bitmap(1, 1));
            lock (b)
                using (Graphics g = Graphics.FromImage(b))
                using (StringFormat sf = Formato(flags))
                {
                    int m = Margen(font, flags);
                    int ancho = proposedSize.Width <= 0 || proposedSize.Width == int.MaxValue || (flags & TextFormatFlags.WordBreak) == 0 ? 100000 : Math.Max(1, proposedSize.Width - 2 * m);
                    SizeF s = g.MeasureString(text, font, ancho, sf);
                    return new Size((int)Math.Ceiling(s.Width) + 2 * m, Math.Max(font.Height, (int)Math.Ceiling(s.Height)));
                }
        }
    }
}
