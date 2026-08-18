using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

// Dibuja la franja de abajo REAL (con el mismo DrawLabelOverlay que hornea la textura de la GPU)
// para ver como queda "Reproduciendo" en una y en dos lineas, con y sin el letrero MEJORADA.
class Vista
{
    static Type TF; static MethodInfo PARTIR, DIBUJA;
    static Font FNow, FMej;

    static Label L(string t, Font f, Color c, int x, int y, int w, int h, ContentAlignment a)
    {
        Label l = new Label();
        l.Text = t; l.Font = f; l.ForeColor = c; l.UseMnemonic = false;
        l.SetBounds(x, y, w, h); l.TextAlign = a; l.AutoEllipsis = true;
        return l;
    }

    static void Franja(Graphics g, int W, int top, string texto, bool mejorada, int fila)
    {
        // mismos numeros que LayoutControls
        int pad = 14, H = 768, progH = 16;
        int progY = H - pad - progH, nextY = progY - 26, nowY = nextY - 26;
        int nowLineH = Math.Max(24, FNow.Height + 2);
        int nowTopY = nowY - nowLineH;
        int cubeSize = 64, rightTextW = 200;
        int rightTextX = W - pad - rightTextW, cubeX = rightTextX - 10 - cubeSize;
        int leftW = Math.Max(120, (cubeX - 14) - pad);

        int bw = mejorada ? TextRenderer.MeasureText("MEJORADA", FMej).Width + 4 : 0;
        int anchoUlt = mejorada ? Math.Max(60, leftW - bw - 12) : leftW;
        object[] args = { texto, FNow, leftW, anchoUlt, null, null };
        PARTIR.Invoke(null, args);
        string arriba = (string)args[4], abajo = (string)args[5];

        Color verde = Color.FromArgb(125, 235, 165);
        Label lTop = L(arriba, FNow, verde, pad, nowTopY, leftW, nowLineH, ContentAlignment.BottomLeft);
        Label lNow = L(abajo, FNow, verde, pad, nowY, anchoUlt, 24, ContentAlignment.TopLeft);
        Label lNext = L("Siguiente:  Los Bukis - Tu Carcel", new Font(FNow.FontFamily, 11f, FontStyle.Bold),
                        Color.FromArgb(150, 200, 255), pad, nextY, leftW, 22, ContentAlignment.TopLeft);
        g.ResetTransform();
        g.TranslateTransform(0, -top + fila);
        DIBUJA.Invoke(null, new object[] { g, lTop });
        DIBUJA.Invoke(null, new object[] { g, lNow });
        DIBUJA.Invoke(null, new object[] { g, lNext });
        if (mejorada)
        {
            int tw = TextRenderer.MeasureText(lNow.Text, FNow).Width; if (tw > anchoUlt) tw = anchoUlt;
            Label lm = L("MEJORADA", FMej, Color.FromArgb(255, 205, 90), pad + tw + 10, nowY + 2, bw, 20, ContentAlignment.MiddleLeft);
            DIBUJA.Invoke(null, new object[] { g, lm });
        }
        g.ResetTransform();
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        PARTIR = TF.GetMethod("PartirNow", BindingFlags.Static | BindingFlags.NonPublic);
        DIBUJA = TF.GetMethod("DrawLabelOverlay", BindingFlags.Static | BindingFlags.NonPublic);
        if (PARTIR == null || DIBUJA == null) { Console.WriteLine("faltan metodos"); return 1; }
        FNow = new Font("DejaVu Sans", 13f, FontStyle.Bold);
        FMej = new Font("DejaVu Sans", 10f, FontStyle.Bold);

        int W = 1024, H = 768, progH = 16, pad = 14;
        int progY = H - pad - progH, nowY = progY - 52;
        int nowLineH = Math.Max(24, FNow.Height + 2);
        int top = nowY - nowLineH - 4;
        int bh = H - top;

        string[] casos = { "Reproduciendo:  Cielito Lindo",
                           "Reproduciendo:  Banda El Recodo de Cruz Lizarraga - Y Llegaste Tu (Version Ranchera)" };
        bool[] mej = { false, true, false, true };
        Bitmap bmp = new Bitmap(W, bh * 4, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(255, 18, 26, 52));
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            for (int k = 0; k < 4; k++)
            {
                using (Pen p = new Pen(Color.FromArgb(60, 255, 255, 255))) g.DrawLine(p, 0, bh * k, W, bh * k);
                Franja(g, W, top, casos[k / 2], mej[k], bh * k);
            }
        }
        bmp.Save("vista.png", System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine("listo: vista.png  (" + W + "x" + (bh * 4) + ", cuatro franjas: corto/corto+MEJORADA/largo/largo+MEJORADA)");
        return 0;
    }
}
