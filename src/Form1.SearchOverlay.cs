using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Rocola
{
    public partial class Form1
    {
        private class SearchOverlay : Control
        {
            public string Query = "";
            public List<SearchItem> Results = new List<SearchItem>();
            public int SelRow = 1;
            public int SelCol;
            public bool FocusResults;
            public int SelResult;

            private int resCols = 1;
            private int resRowsPerCol = 8;

            public double CubeAngle;
            public double Phase;
            public Color Accent = DefaultAccent;
            public Func<Album, Image> GetCover;
            public Action<string> OnQueryChanged;
            public Action<SearchItem> OnChoose;
            public Action OnClose;
            public bool ToastOn;
            public string ToastMsg = "";
            public Color ToastBack = Color.FromArgb(238, 40, 165, 90);

            private Rectangle cubeRect = Rectangle.Empty;
            private readonly List<Image> artistCovers = new List<Image>();
            private Bitmap blendBmp;
            private readonly List<List<SearchKey>> rows = new List<List<SearchKey>>();
            private readonly List<KeyValuePair<RectangleF, int>> resultRects = new List<KeyValuePair<RectangleF, int>>();

            // Gradiente de fondo cacheado: solo se recrea al cambiar tamano o acento.
            private LinearGradientBrush bgBrush;
            private int bgBrushW = -1;
            private int bgBrushH = -1;
            private int bgBrushAccent;

            // Fuentes del teclado, dependen del alto de tecla.
            private Font keyFont;
            private Font keyFontSmall;
            private float keyFontHeight = -1f;

            public SearchOverlay()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                TabStop = true;
                BackColor = Color.FromArgb(8, 12, 36);
                BuildKeys();
            }

            private void BuildKeys()
            {
                AddRow("1234567890");
                AddRow("QWERTYUIOP");
                AddRow("ASDFGHJKLÑ");
                AddRow("ZXCVBNM");
                List<SearchKey> bottom = new List<SearchKey>();
                bottom.Add(new SearchKey
                {
                    Kind = KKind.Space,
                    Label = "ESPACIO"
                });
                bottom.Add(new SearchKey
                {
                    Kind = KKind.Back,
                    Label = "BORRAR"
                });
                rows.Add(bottom);
            }

            private void AddRow(string s)
            {
                List<SearchKey> row = new List<SearchKey>();
                for (int i = 0; i < s.Length; i++)
                {
                    row.Add(new SearchKey
                    {
                        Kind = KKind.Char,
                        Ch = s[i],
                        Label = s[i].ToString()
                    });
                }
                rows.Add(row);
            }

            public void SetQuery(string q)
            {
                Query = q;
                FocusResults = false;
                SelResult = 0;
                Invalidate();
            }

            public void TypeChar(char c)
            {
                Query += c;
                Changed();
            }

            public void Backspace()
            {
                if (Query.Length > 0)
                {
                    Query = Query.Substring(0, Query.Length - 1);
                }
                Changed();
            }

            private void Changed()
            {
                FocusResults = false;
                SelResult = 0;
                if (OnQueryChanged != null)
                {
                    OnQueryChanged(Query);
                }
                Invalidate();
            }

            public void InvalidateCube()
            {
                if (cubeRect != Rectangle.Empty)
                {
                    Invalidate(Rectangle.Inflate(cubeRect, 6, 6));
                }
                else
                {
                    Invalidate();
                }
            }

            public void MoveSel(int dx, int dy)
            {
                if (FocusResults)
                {
                    int capacity = Math.Max(1, resCols * resRowsPerCol);
                    int count = Math.Min((Results != null) ? Results.Count : 0, capacity);
                    if (count == 0)
                    {
                        if (dx < 0)
                        {
                            FocusResults = false;
                        }
                        Invalidate();
                        return;
                    }
                    if (SelResult < 0)
                    {
                        SelResult = 0;
                    }
                    if (SelResult >= count)
                    {
                        SelResult = count - 1;
                    }
                    int perCol = (resRowsPerCol <= 0) ? 1 : resRowsPerCol;
                    int col = SelResult / perCol;
                    int row = SelResult % perCol;
                    if (dx < 0 && col == 0)
                    {
                        FocusResults = false;
                        Invalidate();
                        return;
                    }
                    if (dy != 0)
                    {
                        int newRow = row + dy;
                        if (newRow < 0)
                        {
                            newRow = 0;
                        }
                        if (newRow >= perCol)
                        {
                            newRow = perCol - 1;
                        }
                        int idx = col * perCol + newRow;
                        if (idx < count)
                        {
                            SelResult = idx;
                        }
                    }
                    if (dx != 0)
                    {
                        int newCol = col + dx;
                        if (newCol < 0)
                        {
                            newCol = 0;
                        }
                        int idx = newCol * perCol + row;
                        if (idx >= count)
                        {
                            idx = count - 1;
                        }
                        if (idx >= 0)
                        {
                            SelResult = idx;
                        }
                    }
                    Invalidate();
                }
                else
                {
                    if (rows.Count == 0)
                    {
                        return;
                    }
                    if (dy != 0)
                    {
                        SelRow += dy;
                        if (SelRow < 0)
                        {
                            SelRow = 0;
                        }
                        if (SelRow >= rows.Count)
                        {
                            SelRow = rows.Count - 1;
                        }
                        if (SelCol >= rows[SelRow].Count)
                        {
                            SelCol = rows[SelRow].Count - 1;
                        }
                    }
                    if (dx != 0)
                    {
                        if (dx > 0 && SelCol >= rows[SelRow].Count - 1 && Results != null && Results.Count > 0)
                        {
                            FocusResults = true;
                            SelResult = 0;
                            Invalidate();
                            return;
                        }
                        SelCol += dx;
                        if (SelCol < 0)
                        {
                            SelCol = 0;
                        }
                        if (SelCol >= rows[SelRow].Count)
                        {
                            SelCol = rows[SelRow].Count - 1;
                        }
                    }
                    Invalidate();
                }
            }

            public void PressSelected()
            {
                if (FocusResults)
                {
                    if (Results != null && SelResult >= 0 && SelResult < Results.Count && OnChoose != null)
                    {
                        OnChoose(Results[SelResult]);
                    }
                }
                else if (SelRow >= 0 && SelRow < rows.Count && SelCol >= 0 && SelCol < rows[SelRow].Count)
                {
                    Activate(rows[SelRow][SelCol]);
                }
            }

            private void Activate(SearchKey k)
            {
                if (k.Kind == KKind.Char)
                {
                    TypeChar(k.Ch);
                }
                else if (k.Kind == KKind.Space)
                {
                    TypeChar(' ');
                }
                else if (k.Kind == KKind.Back)
                {
                    Backspace();
                }
                else if (k.Kind == KKind.Choose && Results != null && Results.Count > 0 && OnChoose != null)
                {
                    OnChoose(Results[0]);
                }
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                if (!FocusResults)
                {
                    for (int i = 0; i < rows.Count; i++)
                    {
                        for (int j = 0; j < rows[i].Count; j++)
                        {
                            if (rows[i][j].R.Contains(e.X, e.Y))
                            {
                                SelRow = i;
                                SelCol = j;
                                Activate(rows[i][j]);
                                Invalidate();
                                base.OnMouseClick(e);
                                return;
                            }
                        }
                    }
                }
                else if (cubeRect != Rectangle.Empty && cubeRect.Contains(e.X, e.Y))
                {
                    FocusResults = false;
                    Invalidate();
                    base.OnMouseClick(e);
                    return;
                }
                for (int k = 0; k < resultRects.Count; k++)
                {
                    if (resultRects[k].Key.Contains(e.X, e.Y))
                    {
                        int idx = resultRects[k].Value;
                        if (Results != null && idx >= 0 && idx < Results.Count && OnChoose != null)
                        {
                            OnChoose(Results[idx]);
                        }
                        base.OnMouseClick(e);
                        return;
                    }
                }
                base.OnMouseClick(e);
            }

            private void DrawGlows(Graphics g, int w, int h)
            {
                Color glow1 = Lighten(Accent, 0.45);
                Color glow2 = Lighten(Accent, 0.75);
                float t = (float)Phase * 2f;
                float x1 = (0.3f + 0.18f * (float)Math.Sin(t * 0.11)) * w;
                float y1 = (0.35f + 0.14f * (float)Math.Cos(t * 0.09)) * h;
                float r1 = Math.Min(Math.Min(w, h) * 0.55f, 460f);
                g.FillEllipse(Gdi.Brush(Color.FromArgb(34, glow1)), x1 - r1, y1 - r1, r1 * 2f, r1 * 2f);
                float x2 = (0.72f + 0.16f * (float)Math.Cos(t * 0.08 + 1.3)) * w;
                float y2 = (0.62f + 0.15f * (float)Math.Sin(t * 0.1 + 0.6)) * h;
                float r2 = Math.Min(Math.Min(w, h) * 0.45f, 460f);
                g.FillEllipse(Gdi.Brush(Color.FromArgb(28, glow2)), x2 - r2, y2 - r2, r2 * 2f, r2 * 2f);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                int w = Width;
                int h = Height;
                if (w < 60 || h < 60)
                {
                    return;
                }
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle clip = e.ClipRectangle;
                if (bgBrush == null || bgBrushW != w || bgBrushH != h || bgBrushAccent != Accent.ToArgb())
                {
                    if (bgBrush != null)
                    {
                        bgBrush.Dispose();
                    }
                    bgBrush = new LinearGradientBrush(new Rectangle(0, 0, w, h), Shade(Accent, 0.6), Shade(Accent, 0.88), LinearGradientMode.Vertical);
                    bgBrushW = w;
                    bgBrushH = h;
                    bgBrushAccent = Accent.ToArgb();
                }
                g.FillRectangle(bgBrush, clip);
                DrawGlows(g, w, h);
                g.FillRectangle(Gdi.Brush(Color.FromArgb(150, 6, 9, 26)), 0, 0, w, h);
                int margin = 30;
                Rectangle queryRect = new Rectangle(margin, margin, w - 2 * margin, 70);
                Rectangle helpRect = new Rectangle(margin, margin + 72, w - 2 * margin, 20);
                if (queryRect.IntersectsWith(clip) || helpRect.IntersectsWith(clip))
                {
                    using (GraphicsPath path = RoundedPath(queryRect, 16))
                    {
                        g.FillPath(Gdi.Brush(Shade(Accent, 0.42)), path);
                        g.DrawPath(Gdi.Pen(Lighten(Accent, 0.35), 2f), path);
                    }
                    bool caret = Math.Sin(Phase * 3.0) > 0.0;
                    string text = "Buscar:  " + Query + (caret ? "|" : "");
                    TextRenderer.DrawText(g, text, Gdi.SansBold20,
                        new Rectangle(queryRect.X + 18, queryRect.Y, queryRect.Width - 30, queryRect.Height),
                        Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    Font helpFont = Gdi.SansBold95;
                    int cy = helpRect.Y + helpRect.Height / 2;
                    Color cText = Color.FromArgb(214, 222, 242);
                    Color cRed = Color.FromArgb(228, 72, 72);
                    Color cGreen = Color.FromArgb(72, 212, 112);
                    int dot = 14;
                    float gap = 6f;
                    float sep = 18f;
                    float x = helpRect.X;
                    x = CtrlPad(g, x, cy, dot + 2);
                    x = CtrlTxt(g, helpFont, "Mover", x + gap, cy, cText) + sep;
                    x = CtrlDot(g, x, cy, dot, cGreen);
                    x = CtrlTxt(g, helpFont, "Poner / Elegir", x + gap, cy, cText) + sep;
                    x = CtrlDot(g, x, cy, dot, cRed) + 3f;
                    x = CtrlDot(g, x, cy, dot, cGreen);
                    CtrlTxt(g, helpFont, "Cerrar", x + gap, cy, cText);
                }
                int top = margin + 70 + 28;
                int bodyH = h - top - margin;
                if (bodyH < 60)
                {
                    return;
                }
                int leftW = (int)((w - 2 * margin) * 0.52);
                Rectangle leftRegion = new Rectangle(margin, top, leftW, bodyH);
                Rectangle rightRegion = new Rectangle(margin + leftW + 20, top, w - 2 * margin - leftW - 20, bodyH);
                if (FocusResults && Results != null && Results.Count > 0 && GetCover != null)
                {
                    if (leftRegion.IntersectsWith(clip))
                    {
                        DrawPreviewCube(g, leftRegion);
                    }
                }
                else
                {
                    cubeRect = Rectangle.Empty;
                    LayoutKeys(leftRegion);
                    if (leftRegion.IntersectsWith(clip))
                    {
                        DrawKeys(g);
                    }
                }
                if (rightRegion.IntersectsWith(clip))
                {
                    DrawResults(g, rightRegion);
                }
                if (ToastOn)
                {
                    DrawToast(g, w, h);
                }
            }

            private void DrawToast(Graphics g, int W, int H)
            {
                int w = 400;
                int h = 56;
                Rectangle r = new Rectangle((W - w) / 2, 104, w, h);
                using (GraphicsPath path = RoundedPath(r, 16))
                {
                    g.FillPath(Gdi.Brush(ToastBack), path);
                    g.DrawPath(Gdi.Pen(Lighten(ToastBack, 0.45), 2.4f), path);
                }
                TextRenderer.DrawText(g, ToastMsg, Gdi.SansBold155, r, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            private void LayoutKeys(Rectangle region)
            {
                int rowCount = rows.Count;
                if (rowCount == 0)
                {
                    return;
                }
                float gap = 6f;
                float rowH = (region.Height - gap * (rowCount - 1)) / rowCount;
                int maxCols = 10;
                float keyW = (region.Width - gap * (maxCols - 1)) / maxCols;
                for (int i = 0; i < rows.Count; i++)
                {
                    float y = region.Y + i * (rowH + gap);
                    if (i < 4)
                    {
                        int count = rows[i].Count;
                        float rowW = count * keyW + gap * (count - 1);
                        float x0 = region.X + (region.Width - rowW) / 2f;
                        for (int j = 0; j < count; j++)
                        {
                            rows[i][j].R = new RectangleF(x0 + j * (keyW + gap), y, keyW, rowH);
                        }
                        continue;
                    }
                    int bottomCount = rows[i].Count;
                    float[] weights;
                    switch (bottomCount)
                    {
                        case 2:
                            weights = new float[2] { 0.6f, 0.4f };
                            break;
                        case 3:
                            weights = new float[3] { 0.4f, 0.28f, 0.32f };
                            break;
                        default:
                        {
                            weights = new float[bottomCount];
                            for (int k = 0; k < bottomCount; k++)
                            {
                                weights[k] = 1f / bottomCount;
                            }
                            break;
                        }
                    }
                    float available = region.Width - gap * (bottomCount - 1);
                    float x = region.X;
                    for (int j = 0; j < bottomCount; j++)
                    {
                        float kw = available * weights[j];
                        rows[i][j].R = new RectangleF(x, y, kw, rowH);
                        x += kw + gap;
                    }
                }
            }

            private void DrawKeys(Graphics g)
            {
                if (rows.Count == 0 || rows[0].Count == 0)
                {
                    return;
                }
                float keyH = rows[0][0].R.Height;
                if (keyH < 8f)
                {
                    keyH = 8f;
                }
                if (keyFont == null || keyFontHeight != keyH)
                {
                    if (keyFont != null)
                    {
                        keyFont.Dispose();
                        keyFontSmall.Dispose();
                    }
                    keyFont = new Font(FontFamily.GenericSansSerif, Math.Max(12f, keyH * 0.42f), FontStyle.Bold);
                    keyFontSmall = new Font(FontFamily.GenericSansSerif, Math.Max(9f, keyH * 0.24f), FontStyle.Bold);
                    keyFontHeight = keyH;
                }
                for (int i = 0; i < rows.Count; i++)
                {
                    for (int j = 0; j < rows[i].Count; j++)
                    {
                        SearchKey key = rows[i][j];
                        Rectangle rect = Rectangle.Round(key.R);
                        if (rect.Width < 2 || rect.Height < 2)
                        {
                            continue;
                        }
                        bool selected = i == SelRow && j == SelCol;
                        double wave = 0.5 + 0.5 * Math.Sin(Phase * 2.2 - (i * 0.55 + j * 0.32));
                        using (GraphicsPath path = RoundedPath(rect, 10))
                        {
                            Color top = selected ? Lighten(Accent, 0.32) : Lighten(Shade(Accent, 0.34), 0.1 + 0.14 * wave);
                            Color bottom = selected ? Accent : Shade(Accent, 0.55);
                            using (LinearGradientBrush lg = new LinearGradientBrush(rect, top, bottom, LinearGradientMode.Vertical))
                            {
                                g.FillPath(lg, path);
                            }
                            if (selected)
                            {
                                g.DrawPath(Gdi.Pen(Color.FromArgb(130 + (int)((0.5 + 0.5 * Math.Sin(Phase * 2.6)) * 120.0), 170, 215, 255), 2.8f), path);
                            }
                            else
                            {
                                g.DrawPath(Gdi.Pen(Color.FromArgb(85 + (int)(wave * 130.0), 150, 200, 255), 1.7f), path);
                            }
                        }
                        Font font = (key.Kind == KKind.Char) ? keyFont : keyFontSmall;
                        TextRenderer.DrawText(g, key.Label, font, rect, Color.White,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
                }
            }

            private void DrawResults(Graphics g, Rectangle region)
            {
                resultRects.Clear();
                TextRenderer.DrawText(g, "Resultados", Gdi.SansBold13,
                    new Rectangle(region.X, region.Y, region.Width, 24), Color.White, TextFormatFlags.Default);
                int top = region.Y + 30;
                int bodyH = region.Bottom - top;
                if (bodyH < 28)
                {
                    resCols = 1;
                    resRowsPerCol = 1;
                    return;
                }
                int cardH = 38;
                int gapY = 5;
                int gapX = 8;
                int minCardW = 185;
                int cols = (region.Width + gapX) / (minCardW + gapX);
                if (cols < 1)
                {
                    cols = 1;
                }
                if (cols > 2)
                {
                    cols = 2;
                }
                int cardW = (region.Width - (cols - 1) * gapX) / cols;
                int rowsPerCol = (bodyH + gapY) / (cardH + gapY);
                if (rowsPerCol < 1)
                {
                    rowsPerCol = 1;
                }
                resCols = cols;
                resRowsPerCol = rowsPerCol;
                int capacity = cols * rowsPerCol;
                int count = (Results != null) ? Math.Min(Results.Count, capacity) : 0;
                if (FocusResults && count > 0)
                {
                    if (SelResult < 0)
                    {
                        SelResult = 0;
                    }
                    if (SelResult >= count)
                    {
                        SelResult = count - 1;
                    }
                }
                if (count == 0)
                {
                    TextRenderer.DrawText(g, (Query.Length == 0) ? "Escribe para buscar..." : "Sin resultados",
                        Gdi.SansItalic11, new Rectangle(region.X, top, region.Width, 28),
                        Color.FromArgb(190, 200, 225), TextFormatFlags.Default);
                    return;
                }
                for (int i = 0; i < count; i++)
                {
                    int col = i / rowsPerCol;
                    int row = i % rowsPerCol;
                    int x = region.X + col * (cardW + gapX);
                    int y = top + row * (cardH + gapY);
                    Rectangle card = new Rectangle(x, y, cardW, cardH);
                    bool selected = FocusResults ? (i == SelResult) : (i == 0);
                    using (GraphicsPath path = RoundedPath(card, 8))
                    {
                        g.FillPath(Gdi.Brush(selected ? Shade(Accent, 0.26) : Shade(Accent, 0.5)), path);
                        g.DrawPath(Gdi.Pen(Color.FromArgb(selected ? 215 : 100, 160, 200, 245), selected ? 2f : 1.1f), path);
                    }
                    SearchItem item = Results[i];
                    string kind = (item.Kind == 0) ? "ARTISTA" : ((item.Kind == 1) ? "ALBUM" : "CANCION");
                    Color kindColor = (item.Kind == 0) ? Color.FromArgb(150, 215, 255) : ((item.Kind == 1) ? Color.FromArgb(255, 210, 120) : Color.FromArgb(170, 245, 170));
                    TextRenderer.DrawText(g, kind, Gdi.SansBold75, new Rectangle(card.X + 9, card.Y + 4, cardW - 14, 13), kindColor, TextFormatFlags.Default);
                    TextRenderer.DrawText(g, item.Display, Gdi.SansBold105, new Rectangle(card.X + 9, card.Y + 16, cardW - 16, cardH - 18), Color.White, TextFormatFlags.EndEllipsis);
                    resultRects.Add(new KeyValuePair<RectangleF, int>(card, i));
                }
            }

            private List<Image> EnsureArtistCovers(SearchItem it)
            {
                artistCovers.Clear();
                if (it.Albums != null && GetCover != null)
                {
                    for (int i = 0; i < it.Albums.Count; i++)
                    {
                        Image img = GetCover(it.Albums[i]);
                        if (img != null)
                        {
                            artistCovers.Add(img);
                        }
                    }
                }
                return artistCovers;
            }

            private Image BlendInto(Image a, Image b, float f)
            {
                if (blendBmp == null)
                {
                    blendBmp = new Bitmap(320, 320, PixelFormat.Format32bppPArgb);
                }
                using (Graphics g = Graphics.FromImage(blendBmp))
                {
                    g.Clear(Color.Black);
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    Rectangle rect = new Rectangle(0, 0, blendBmp.Width, blendBmp.Height);
                    if (a != null)
                    {
                        g.DrawImage(a, rect);
                    }
                    if (b != null && f > 0f)
                    {
                        ColorMatrix matrix = new ColorMatrix();
                        matrix.Matrix33 = f;
                        using (ImageAttributes attrs = new ImageAttributes())
                        {
                            attrs.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                            g.DrawImage(b, rect, 0, 0, b.Width, b.Height, GraphicsUnit.Pixel, attrs);
                        }
                    }
                }
                return blendBmp;
            }

            private void DrawPreviewCube(Graphics g, Rectangle region)
            {
                int count = (Results != null) ? Results.Count : 0;
                if (count == 0 || SelResult < 0 || SelResult >= count)
                {
                    cubeRect = Rectangle.Empty;
                    return;
                }
                SearchItem item = Results[SelResult];
                Image cover;
                if (item.Kind == 0)
                {
                    List<Image> covers = EnsureArtistCovers(item);
                    if (covers.Count == 0)
                    {
                        cover = (GetCover != null && item.AlbumRef != null) ? GetCover(item.AlbumRef) : null;
                    }
                    else if (covers.Count == 1)
                    {
                        cover = covers[0];
                    }
                    else
                    {
                        double slot = 3.8;
                        double hold = 2.6;
                        int n = covers.Count;
                        double cycle = slot * n;
                        double pos = Phase - Math.Floor(Phase / cycle) * cycle;
                        int idx = (int)(pos / slot) % n;
                        double within = pos - idx * slot;
                        float blend = 0f;
                        if (within > hold)
                        {
                            blend = (float)((within - hold) / (slot - hold));
                        }
                        if (blend < 0f)
                        {
                            blend = 0f;
                        }
                        else if (blend > 1f)
                        {
                            blend = 1f;
                        }
                        cover = (blend <= 0.002f) ? covers[idx] : BlendInto(covers[idx], covers[(idx + 1) % n], blend);
                    }
                }
                else
                {
                    cover = (GetCover != null && item.AlbumRef != null) ? GetCover(item.AlbumRef) : null;
                }
                int side = (int)(Math.Min(region.Width, region.Height) * 0.62);
                if (side < 40)
                {
                    side = 40;
                }
                int cx = region.X + region.Width / 2;
                int top = region.Y + (region.Height - side) / 2 - 24;
                if (top < region.Y + 6)
                {
                    top = region.Y + 6;
                }
                Rectangle bounds = (cubeRect = new Rectangle(cx - side / 2, top, side, side));
                g.InterpolationMode = InterpolationMode.Bilinear;
                Cube.Draw(g, bounds, cover, CubeAngle);
                string title;
                string subtitle;
                if (item.Kind == 1)
                {
                    title = (item.AlbumRef != null) ? item.AlbumRef.Name : item.Display;
                    subtitle = (item.AlbumRef != null) ? item.AlbumRef.Artist : "";
                }
                else if (item.Kind == 2 && item.TrackRef != null)
                {
                    title = item.TrackRef.Title;
                    subtitle = item.TrackRef.Artist + ((item.AlbumRef != null) ? ("   -   " + item.AlbumRef.Name) : "");
                }
                else
                {
                    title = item.Display;
                    int discs = (item.Albums != null) ? item.Albums.Count : 0;
                    subtitle = discs + ((discs == 1) ? " disco" : " discos");
                }
                int textY = bounds.Bottom + 12;
                TextRenderer.DrawText(g, title, Gdi.SansBold15,
                    new Rectangle(region.X, textY, region.Width, 28), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, subtitle, Gdi.SansReg11,
                    new Rectangle(region.X, textY + 30, region.Width, 24), Color.FromArgb(215, 225, 245),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
                Font helpFont = Gdi.SansBold11;
                SmoothingMode oldSmoothing = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                TextRenderingHint oldHint = g.TextRenderingHint;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                Color cText = Color.FromArgb(205, 214, 238);
                Color cArrow = Color.FromArgb(242, 206, 72);
                Color cGreen = Color.FromArgb(72, 212, 112);
                int cy = region.Bottom - 14;
                int dot = 15;
                float gap = 7f;
                float sep = 26f;
                string backText = "volver al teclado";
                string chooseText = "elegir";
                SizeF backSize = g.MeasureString(backText, helpFont);
                SizeF chooseSize = g.MeasureString(chooseText, helpFont);
                float totalW = dot + gap + backSize.Width + sep + dot + gap + chooseSize.Width;
                float x = region.X + (region.Width - totalW) / 2f;
                x = CtrlDirLeft(g, x, cy, dot, cArrow) + gap;
                x = CtrlTxt(g, helpFont, backText, x, cy, cText) + sep;
                x = CtrlDot(g, x, cy, dot, cGreen) + gap;
                CtrlTxt(g, helpFont, chooseText, x, cy, cText);
                g.SmoothingMode = oldSmoothing;
                g.TextRenderingHint = oldHint;
            }
        }
    }
}
