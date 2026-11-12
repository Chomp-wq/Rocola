// LA PARED DE DISCOS: la rejilla, los cubos y la pantalla de atraccion
//
// Parte de Form1. La clase esta repartida en varios archivos por temas -son todos "partial class Form1"-
// para poder venir a tocar UNA cosa sin leerse el programa entero. Aqui no hay nada nuevo: es el mismo
// codigo que estaba en Form1.cs, movido tal cual.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: la pared (la atraccion, el cubo, las ayudas de los botones, las casillas, el contorno y el cubo de la cancion) dibuja con SkiaSharp directo y las funciones nativas (Xp/Nativo), no con GDI+; las portadas son los SKBitmap de la cache de portadas; las vistas (AttractView, AlbumView y CubeBox) ya no heredan de Control (vistas-pared) y se pintan con Pinta en el lienzo de Skia, con el estado del Graphics de XP en argumentos; del puente (Plataforma/Puente.pared.cs) solo queda ExtractCoverPalette con un Bitmap, para quien aun la llama asi
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rocola
{
    public partial class Form1 : Form
    {

        // Modo atraccion: pantalla negra con estrellas titilantes y un cubo gigante girando que cicla
        // portadas de la coleccion (con fundido). Aparece en reposo para llamar al cliente.
        private class AttractView : IVista   // EN LINUX: : Control -> sin herencia (port nativo, vistas-pared: W3b): una vista de la pantalla nativa (Nativo/IVista.cs) con su CajaVista; en la ventana de la capa la pone su hueco (Plataforma/Anfitrion.cs), que la pinta con Pinta y le da el raton como WinForms al control (Raton); el MouseDown que Form1 le oye (PantallaRocola.OyeRaton) lo avisan el hueco o la pantalla despues de Raton, que aqui es el de fabrica (XP no le sobrescribe ningun On* del raton); no es IDisposable: XP no le sobrescribe Dispose
        {
            public double Phase = 0;
            public double Angle = 0;
            public List<string> CycleCovers = new List<string>();
            public Func<string, SKBitmap> GetCoverByPath;   // EN LINUX: Image: las portadas las da Form1 (LoadCoverByPath, Form1.Biblioteca.cs): SKBitmap de la cache de portadas
            public int SongCount { get; set; }   // propiedad (no campo): un campo de una clase por-referencia (Control) dispara la advertencia CS1690 al usarlo

            private SKPaint sb;   // EN LINUX: SolidBrush: el pincel de un color en un SKPaint
            private SKBitmap blendBmp;   // EN LINUX: Bitmap: un SKBitmap (BGRA premultiplicado, como Format32bppPArgb)
            private Rocola.Nativo.Letra fBig, fSmall; private FormatoTexto sfCen;   // EN LINUX: Font y StringFormat: Letra y FormatoTexto (Nativo/Letras.cs y TextoGdiPlus.cs; Form1 tiene un metodo Letra, por eso va con su espacio)

            private Starfield attractStars = new Starfield();

            public AttractView()
            {
                attractStars.Varied = true;   // estrellas multicolor en el modo atraccion
                // El cubo gigante gira a 24 por segundo, pero las estrellas no tienen por que: titilan al
                // mismo ritmo que en el resto del programa. Rehacer el cielo entero 24 veces por segundo era
                // el grueso del gasto de esta pantalla, y es la que mas horas pasa encendida.
                attractStars.RefrescoMs = 200;
                caja = new CajaVista(this);   // EN LINUX: SetStyle(AllPaintingInWmPaint | OptimizedDoubleBuffer | UserPaint | ResizeRedraw, true): sin ventana propia no hay WM_ERASEBKGND ni doble bufer: la pinta su Pinta entera; ResizeRedraw = RepintaConMedida, que la CajaVista trae de fabrica (al cambiar de medida se pinta entera); Selectable, TabStop, StandardClick y StandardDoubleClick no los toca: los de fabrica de Control, que el hueco de la capa conserva; un Control mide 0x0 de fabrica (DefaultSize), como la caja
            }

            private void EnsureRes()
            {
                if (sb == null) sb = new SKPaint { Color = Color.White.Sk() };   // EN LINUX: new SolidBrush(Color.White)
                if (fBig == null) fBig = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, 24f, true);   // EN LINUX: new Font(FontFamily.GenericSansSerif, 24, Bold): la generica sin remates de XP (Microsoft Sans Serif, Letras.SinRemates), en puntos y en negrita
                if (fSmall == null) fSmall = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, 15f, true);   // EN LINUX: new Font(FontFamily.GenericSansSerif, 15, Bold)
                if (sfCen == null) { sfCen = new FormatoTexto(); sfCen.Horizontal = Alinea.Centro; sfCen.Vertical = Alinea.Centro; }   // EN LINUX: new StringFormat() con Alignment y LineAlignment = Center
            }

            private SKBitmap BlendInto(SKBitmap a, SKBitmap b, float f)   // EN LINUX: los puntos de las dos portadas y el mapa de la mezcla, SKBitmap
            {
                if (blendBmp == null) blendBmp = Mapas.Nuevo(320, 320);   // EN LINUX: new Bitmap(320, 320, Format32bppPArgb) (Nativo/Mapas.cs)
                using (SKCanvas g = new SKCanvas(blendBmp))   // EN LINUX: Graphics.FromImage: el lienzo de Skia sobre el mapa (lo demas del estado, de fabrica: sin copia ni medio pixel)
                {
                    g.Clear(Color.Black.Sk());   // EN LINUX: Graphics.Clear
                    Filtro filtro = Filtro.Bilineal;   // EN LINUX: g.InterpolationMode = Bilinear, en una variable
                    Rectangle dst = new Rectangle(0, 0, blendBmp.Width, blendBmp.Height);
                    if (a != null) Imagenes.Pinta(g, a, new SKRect(0, 0, a.Width, a.Height), new SKRect(dst.Left, dst.Top, dst.Right, dst.Bottom), filtro);   // EN LINUX: g.DrawImage(a, dst): la imagen entera en esa caja (Nativo/Imagenes.cs)
                    if (b != null && f > 0f)
                    {
                        float[][] cm = MatrizColor.Identidad(); cm[3][3] = f;   // EN LINUX: new ColorMatrix() con Matrix33 = f: la matriz de 5x5 (Nativo/Imagenes.cs)
                        // EN LINUX: using (ImageAttributes ia = new ImageAttributes()): la matriz de color va en la llamada (lo demas de ImageAttributes, de fabrica: la envoltura Clamp, que en GDI+ es transparente)
                        {
                            // EN LINUX: ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap): va en la llamada de abajo
                            Imagenes.Pinta(g, b, new SKRect(0, 0, 0 + b.Width, 0 + b.Height), new SKRect(dst.Left, dst.Top, dst.Right, dst.Bottom), filtro, false, false, Envoltura.Transparente, cm);   // EN LINUX: g.DrawImage(b, dst, 0, 0, b.Width, b.Height, Pixel, ia): el trozo (la imagen entera) en la caja, con la matriz de color
                        }
                    }
                }
                return blendBmp;
            }

            // Par de portadas a fundir + factor (0..1) segun la fase. Lo usa el CPU (BlendInto) y tambien la
            // GPU (que hace el fundido como en los discos TOP, sin generar el bitmap mezclado).
            public bool GetCoverPair(out SKBitmap a, out SKBitmap b, out float f)   // EN LINUX: Image: las portadas, SKBitmap de la cache (las da Form1 y las pide AtraccionGpu.cs para la tarjeta); aqui no se pintan
            {
                a = null; b = null; f = 0f;
                if (CycleCovers == null || CycleCovers.Count == 0 || GetCoverByPath == null) return false;
                int m = CycleCovers.Count;
                double cyc = 4.2, hold = 3.0;
                double total = cyc * m;
                double pos = Phase - Math.Floor(Phase / total) * total;
                int idx = (int)(pos / cyc) % m;
                double within = pos - idx * cyc;
                if (within > hold) f = (float)((within - hold) / (cyc - hold));
                if (f < 0f) f = 0f; else if (f > 1f) f = 1f;
                a = GetCoverByPath(CycleCovers[idx]);
                b = (m > 1) ? GetCoverByPath(CycleCovers[(idx + 1) % m]) : null;
                return a != null || b != null;
            }

            private SKBitmap CurrentCover()   // EN LINUX: los puntos de la portada (o de la mezcla) para pintarla: un SKBitmap
            {
                SKBitmap a, b; float f;   // EN LINUX: Image: SKBitmap de la cache (GetCoverPair)
                if (!GetCoverPair(out a, out b, out f)) return null;
                if (f <= 0.002f || b == null) return a;
                if (a == null) return b;
                return BlendInto(a, b, f);
            }

            public void Pinta(SKCanvas g) { Pinta(g, false, false, Filtro.Bilineal, false); } private readonly CajaVista caja; public Rectangle Bounds { get { return caja.Bounds; } set { caja.Bounds = value; } } public int Left { get { return caja.Left; } set { caja.Left = value; } } public int Top { get { return caja.Top; } set { caja.Top = value; } } public int Width { get { return caja.Width; } set { caja.Width = value; } } public int Height { get { return caja.Height; } set { caja.Height = value; } } public void SetBounds(int x, int y, int width, int height) { caja.SetBounds(x, y, width, height); } public bool Visible { get { return caja.Visible; } set { caja.Visible = value; } } public void Invalidate() { caja.Invalidate(); } internal void Pinta(SKCanvas g, bool suave, bool copia, Filtro filtroDe, bool medio)   // EN LINUX: protected override void OnPaint(PaintEventArgs e): Pinta(SKCanvas) es la de la pantalla (IVista.Pinta): el lienzo y el estado del Graphics de un WM_PAINT (el de fabrica: sin suavizado, sin copia, filtro Bilinear, sin medio pixel); la de cinco argumentos es el OnPaint de XP con el estado del Graphics que llega en argumentos, que pide tambien el precalentado (EnSeco de Form1.Portadas.cs: InvokePaint con el Graphics de un mapa con suavizado); lo que XP le dejaba al Graphics (el filtro Bilinear y el modo de letra ClearTypeGridFit) se iba con el. En la misma linea, lo que XP usa de este control por fuera y le daba Control: Bounds, Left, Top, Width, Height, SetBounds, Visible e Invalidate (con su CajaVista)
            {
                // EN LINUX: Graphics g = e.Graphics: el lienzo de Skia (con su matriz y su recorte) llega como g y el estado del Graphics como argumentos (suave, copia, filtroDe, medio)
                int w = Width, h = Height;
                if (w < 8 || h < 8) return;
                EnsureRes();
                attractStars.SkyOffsetY = 0; attractStars.SkyFullH = h;
                attractStars.Draw(g, suave, filtroDe, medio, copia, w, h, Color.FromArgb(255, 6, 8, 26), Color.FromArgb(255, 18, 20, 54), Color.White, Phase, true, 0f);   // cielo nocturno en degradado (atraccion: sin musica)   // EN LINUX: Starfield.Draw traducida (Form1.Fondo.cs), directo, con el lienzo y el estado del Graphics (el que trae: suavizado, filtro, medio pixel y copia; no se lo cambia); ya no pasa por la firma de XP del puente (Plataforma/Puente.fondo.cs)

                // cubo gigante central rotando + ciclando portadas (con fundido)
                int side = (int)(Math.Min(w, h) * 0.40);
                if (side < 80) side = 80;
                Rectangle area = new Rectangle(w / 2 - side / 2, (h - side) / 2 - 30, side, side);
                Filtro filtro = Filtro.Bilineal;   // EN LINUX: g.InterpolationMode = Bilinear, en una variable; lo demas del estado es el que trae el Graphics (suave, copia y medio, en argumentos: el cielo no lo cambia)
                Cube.Draw(g, suave, copia, filtro, medio, area, CurrentCover(), Angle);   // EN LINUX: el lienzo y su estado

                // textos
                AjusteLetra ajuste = AjusteLetra.ClearTypeAjustada;   // EN LINUX: g.TextRenderingHint = ClearTypeGridFit, en una variable
                string line1 = (SongCount > 0) ? (SongCount.ToString("#,0") + " canciones") : "Tu rocola";
                sb.Color = Color.White.Sk();   // EN LINUX: el color del pincel, en el SKPaint
                TextoGdiPlus.Escribe(g, line1, fBig, ajuste, sb, new RectangleF(0, area.Bottom + 18, w, 36), sfCen, copia, medio);   // EN LINUX: g.DrawString en esa caja con ese formato (Nativo/TextoGdiPlus.cs)
                double pz = 0.5 + 0.5 * Math.Sin(Phase * 3.0);
                sb.Color = Color.FromArgb((int)(150 + 105 * pz), 255, 220, 120).Sk();   // EN LINUX: el color del pincel, en el SKPaint
                TextoGdiPlus.Escribe(g, "toca un boton para elegir", fSmall, ajuste, sb, new RectangleF(0, area.Bottom + 58, w, 28), sfCen, copia, medio);   // EN LINUX: g.DrawString
            }
        }

        private static class Cube
        {
            // OPTIMIZACION (render): estos 3 arreglos se asignaban en CADA llamada a Draw, y Draw se
            // llama varias veces por cuadro (cubo seleccionado, relevo, cubeBox, miniCube, buscador,
            // atraccion). Reutilizarlos via [ThreadStatic] elimina cientos de asignaciones/seg.
            [ThreadStatic] private static double[] exBuf;
            [ThreadStatic] private static double[] depthBuf;
            [ThreadStatic] private static int[] orderBuf;
            [ThreadStatic] private static SKPaint faceBrush;   // pincel reusable (cara/oscurecido): antes asignaba 1-2 por cara y cuadro   // EN LINUX: SolidBrush: un SKPaint de un color
            [ThreadStatic] private static SKPaint edgePen;            // lapiz de contorno reusable (antes 1 por cuadro)   // EN LINUX: Pen: un SKPaint con su color y su ancho

            public static void Draw(SKCanvas g, bool suave, bool copia, Filtro filtro, bool medio, Rectangle bounds, SKBitmap cover, double angle)   // EN LINUX: el lienzo de Skia (con su matriz y su recorte) y el estado del que llama (suavizado, copia, filtro y medio pixel), y la portada (el SKBitmap de la cache); la firma de XP con un Graphics y una Image ya no la llama nadie (su parte del puente la quita la limpieza)
            {
                double cx = bounds.X + bounds.Width / 2.0;
                int y0 = bounds.Y;
                int H = bounds.Height;
                double R = bounds.Width / 2.0;
                double DEG = Math.PI / 180.0;

                double[] ex = exBuf ?? (exBuf = new double[4]);
                for (int k = 0; k < 4; k++) ex[k] = cx + R * Math.Cos(angle + 45 * DEG + k * 90 * DEG);
                double[] depth = depthBuf ?? (depthBuf = new double[4]);
                for (int k = 0; k < 4; k++) depth[k] = Math.Sin(angle + 90 * DEG + k * 90 * DEG);

                int[] order = orderBuf ?? (orderBuf = new int[4]);
                order[0] = 0; order[1] = 1; order[2] = 2; order[3] = 3;
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 3 - i; j++)
                        if (depth[order[j]] > depth[order[j + 1]]) { int tmp = order[j]; order[j] = order[j + 1]; order[j + 1] = tmp; }

                for (int oi = 0; oi < 4; oi++)
                {
                    int k = order[oi];
                    if (depth[k] <= 0.0) continue;
                    int left = (int)Math.Round(Math.Min(ex[k], ex[(k + 1) % 4]));
                    int right = (int)Math.Round(Math.Max(ex[k], ex[(k + 1) % 4]));
                    int w = right - left;
                    if (w < 1) continue;
                    Rectangle rect = new Rectangle(left, y0, w, H);
                    SKPaint fb = faceBrush ?? (faceBrush = new SKPaint { Color = Color.Black.Sk() });   // EN LINUX: new SolidBrush(Color.Black)
                    if (cover != null) Imagenes.Pinta(g, cover, new SKRect(0, 0, cover.Width, cover.Height), new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom), filtro, medio, copia);   // EN LINUX: g.DrawImage(cover, rect): la portada entera en la cara (Nativo/Imagenes.cs)
                    else { fb.Color = Color.FromArgb(60, 70, 110).Sk(); Pixel.Rellena(g, (RectangleF)rect, fb, suave, medio, copia); }   // EN LINUX: g.FillRectangle (Nativo/Pixel.cs)
                    int darken = (int)((1.0 - depth[k]) * 130);
                    if (darken > 0) { fb.Color = Color.FromArgb(darken, 0, 0, 0).Sk(); Pixel.Rellena(g, (RectangleF)rect, fb, suave, medio, copia); }   // EN LINUX: g.FillRectangle
                }

                double minx = ex[0], maxx = ex[0];
                for (int k = 1; k < 4; k++) { if (ex[k] < minx) minx = ex[k]; if (ex[k] > maxx) maxx = ex[k]; }
                SKPaint ep = edgePen ?? (edgePen = new SKPaint { Color = Color.FromArgb(120, 0, 0, 0).Sk(), StrokeWidth = 1 });   // EN LINUX: new Pen(color, 1): la pluma en un SKPaint (Pixel le pone el minimo de un pixel y las esquinas de GDI+)
                Pixel.TrazaRect(g, new RectangleF((int)Math.Round(minx), y0, (int)Math.Round(maxx - minx), H - 1), ep, suave, medio, copia);   // EN LINUX: g.DrawRectangle(ep, x, y, w, h) con enteros
            }
        }

        // Estado del "brillo" de los simbolos de la barra de instrucciones al pulsar sus botones (halo que
        // decae). UN BRILLO POR SIMBOLO: en los COMBOS (p.ej. REGRESAR mantenido + PONER) se encienden VARIOS
        // a la vez y cada uno decae por su cuenta. (Antes habia UN solo brillo global: el nuevo pisaba al
        // anterior y en los combos solo se veia uno.) El color se fija al dispararlo (asi Mover puede ser
        // ROJO en arriba/abajo o AMARILLO en izq/der). Lo comparten HelpBar (4 simbolos) y el buscador (3).
        private sealed class InstrFlash
        {
            private const int Ms = 420;
            private readonly int[] tick = new int[4];      // momento del disparo por simbolo (0 = nunca disparado)
            private readonly Color[] cols = new Color[4];
            public void Fire(int w, Color c)
            {
                if (w < 0 || w >= tick.Length) return;
                int t = Environment.TickCount;
                tick[w] = (t == 0) ? 1 : t;   // 0 esta reservado como "nunca"
                cols[w] = c;
            }
            public float Level(int w)
            {
                if (w < 0 || w >= tick.Length || tick[w] == 0) return 0f;
                int dt = Environment.TickCount - tick[w];
                if (dt < 0 || dt > Ms) return 0f;
                return 1f - (float)dt / Ms;
            }
            public Color Col(int w) { return (w >= 0 && w < cols.Length) ? cols[w] : Color.White; }
            public int Sig()   // cambia mientras dure ALGUN brillo (para animarlo por CPU); 0 = todo apagado
            {
                int now = Environment.TickCount, s = 0;
                for (int w = 0; w < tick.Length; w++)
                {
                    if (tick[w] == 0) continue;
                    int dt = now - tick[w];
                    if (dt < 0 || dt > Ms) continue;
                    s = s * 37 + (w + 1) * 1000 + dt / 40;
                }
                return s;
            }
        }

        // Halo suave (radial) centrado en un simbolo de instrucciones; intensidad segun 'level' (0..1).
        private static void GlowAround(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float leftX, float cy, float symW, float level, Color c)   // EN LINUX: el lienzo de Skia y el estado del que llama (suavizado, modo de letra, copia y medio pixel; las siete ayudas de los botones lo reciben igual); las firmas de XP con un Graphics se borraron del puente (limpieza-puentes): todos los que la llaman pasan el lienzo
        {
            if (level <= 0.02f) return;
            float cx = leftX + symW / 2f, r = symW * 0.9f + 6f;
            using (SKPath gp = new SKPath { FillType = SKPathFillType.EvenOdd })   // EN LINUX: GraphicsPath: un SKPath (su relleno de fabrica, Alternate, es par-impar)
            {
                float ox = cx - r, oy = cy - r, od = 2 * r; gp.AddOval(new SKRect(ox, oy, ox + od, oy + od));   // EN LINUX: gp.AddEllipse(cx - r, cy - r, 2 * r, 2 * r): el ovalo de esa caja (x, y, ancho, alto)
                using (SKPaint pb = new SKPaint { Color = SKColors.Black })   // EN LINUX: PathGradientBrush(gp): un SKPaint con el shader del degradado de trazo (abajo), con el color opaco que no lo tine
                {
                    SKRect o = gp.Bounds; RectangleF caja = RectangleF.FromLTRB(o.Left, o.Top, o.Right, o.Bottom); PointF centro = new PointF(cx, cy);   // EN LINUX: pb.CenterPoint; la caja del pincel es la del trazo (GraphicsPath.GetBounds)
                    Color colorCentro = Color.FromArgb((int)(level * 215), c);   // EN LINUX: pb.CenterColor
                    Color colorOrilla = Color.FromArgb(0, c);   // EN LINUX: pb.SurroundColors = { ese color }
                    using (SKShader degradado = Degradado.DeTrazo(caja, centro, colorCentro, colorOrilla)) { pb.Shader = degradado; Pixel.Rellena(g, gp, pb, suave, medio, copia); pb.Shader = null; }   // EN LINUX: g.FillPath(pb, gp) con el degradado de trazo de GDI+ (Nativo/Degradado.cs y Pixel.cs)
                }
            }
        }

        // Ayudantes para dibujar los controles fisicos como FORMAS (circulo rojo/verde, cruceta).
        // GDI reusables para las formas de instrucciones (se llaman muchas veces por cuadro): pincel al que se
        // le muta el color, contorno blanco fijo y un buffer de 3 puntos. Evita asignar objetos GDI por forma.
        [ThreadStatic] private static SKPaint ctrlB;   // EN LINUX: SolidBrush: un SKPaint de un color
        [ThreadStatic] private static SKPaint ctrlOutline;   // EN LINUX: Pen: un SKPaint con su color y su ancho
        [ThreadStatic] private static PointF[] ctrlTri3;
        private static SKPaint CB(Color c) { if (ctrlB == null) ctrlB = new SKPaint { Color = c.Sk() }; else ctrlB.Color = c.Sk(); return ctrlB; }   // EN LINUX: CB(c), el pincel reusable de XP (un SolidBrush al que se le muta el color), en un SKPaint; con su nombre de XP: el CB de la capa (para Form1.Vistas.cs) ya no existe
        private static SKPaint COut() { return ctrlOutline ?? (ctrlOutline = new SKPaint { Color = Color.FromArgb(130, 255, 255, 255).Sk(), StrokeWidth = 1.4f }); }   // EN LINUX: COut(), el contorno blanco fijo (Pen de 1,4), en un SKPaint; con su nombre de XP, como CB

        private static float CtrlDot(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int d, Color c)   // EN LINUX: el lienzo de Skia y el estado del que llama; las firmas de XP con un Graphics se borraron del puente (limpieza-puentes): todos los que la llaman pasan el lienzo
        {
            Pixel.RellenaOvalo(g, new RectangleF(x, cy - d / 2f, d, d), CB(c), suave, medio, copia);   // EN LINUX: g.FillEllipse(CB(c), x, y, d, d) (Nativo/Pixel.cs)
            Pixel.TrazaOvalo(g, new RectangleF(x, cy - d / 2f, d, d), COut(), suave, medio, copia);   // EN LINUX: g.DrawEllipse(COut(), x, y, d, d)
            return x + d;
        }
        private static float CtrlTxt(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, Rocola.Nativo.Letra f, string s, float x, int cy, Color c)   // EN LINUX: el lienzo de Skia y el estado del que llama, y la letra es una Letra; las firmas de XP con un Graphics y una Font se borraron del puente (limpieza-puentes): todos los que la llaman pasan el lienzo y la Letra
        {
            SizeF sz = TextoGdiPlus.Mide(s, f, ajuste, new SizeF(0, 0), null);   // EN LINUX: g.MeasureString(s, f): sin caja (0, 0) y sin formato (null: el de fabrica), con el modo de letra del que llama (Nativo/TextoGdiPlus.cs)
            TextoGdiPlus.Escribe(g, s, f, ajuste, CB(c), new RectangleF(x, cy - sz.Height / 2f, 0, 0), null, copia, medio);   // EN LINUX: g.DrawString(s, f, CB(c), x, y): desde ese punto (una caja sin ancho ni alto) y sin formato
            return x + sz.Width;
        }
        private static void CtrlTri(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, Color c, PointF a, PointF b, PointF d)   // EN LINUX: el lienzo de Skia y el estado del que llama; las firmas de XP con un Graphics se borraron del puente (limpieza-puentes): todos los que la llaman pasan el lienzo
        {
            PointF[] p = ctrlTri3 ?? (ctrlTri3 = new PointF[3]);
            p[0] = a; p[1] = b; p[2] = d;
            Pixel.RellenaPoligono(g, p, CB(c), suave, true, medio, copia);   // EN LINUX: g.FillPolygon(CB(c), p): el relleno de fabrica (Alternate) (Nativo/Pixel.cs)
        }
        private static float CtrlPad(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int sz)   // cruceta: arriba/abajo rojo, izq/der amarillo   // EN LINUX: el lienzo de Skia y el estado del que llama; las firmas de XP con un Graphics se borraron del puente (limpieza-puentes): todos los que la llaman pasan el lienzo
        {
            Color red = Color.FromArgb(228, 72, 72), yellow = Color.FromArgb(242, 206, 72);
            float cx = x + sz / 2f, t = sz * 0.34f, w = t / 1.4f;
            CtrlTri(g, suave, ajuste, copia, medio, red, new PointF(cx, cy - sz / 2f), new PointF(cx - w, cy - sz / 2f + t), new PointF(cx + w, cy - sz / 2f + t));   // EN LINUX: el lienzo y su estado
            CtrlTri(g, suave, ajuste, copia, medio, red, new PointF(cx, cy + sz / 2f), new PointF(cx - w, cy + sz / 2f - t), new PointF(cx + w, cy + sz / 2f - t));   // EN LINUX: el lienzo y su estado
            CtrlTri(g, suave, ajuste, copia, medio, yellow, new PointF(x, cy), new PointF(x + t, cy - w), new PointF(x + t, cy + w));   // EN LINUX: el lienzo y su estado
            CtrlTri(g, suave, ajuste, copia, medio, yellow, new PointF(x + sz, cy), new PointF(x + sz - t, cy - w), new PointF(x + sz - t, cy + w));   // EN LINUX: el lienzo y su estado
            return x + sz;
        }
        // Icono del BOTON IZQUIERDA (flecha amarilla, mismo color que la cruceta horizontal)
        private static float CtrlDirLeft(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int sz, Color c)   // EN LINUX: el lienzo de Skia y el estado del que llama; las firmas de XP con un Graphics se borraron del puente (limpieza-puentes): todos los que la llaman pasan el lienzo
        {
            PointF[] pts = ctrlTri3 ?? (ctrlTri3 = new PointF[3]);
            pts[0] = new PointF(x, cy); pts[1] = new PointF(x + sz, cy - sz * 0.5f); pts[2] = new PointF(x + sz, cy + sz * 0.5f);
            Pixel.RellenaPoligono(g, pts, CB(c), suave, true, medio, copia); Pixel.TrazaPoligono(g, pts, COut(), suave, medio, copia);   // EN LINUX: g.FillPolygon(CB(c), pts) y g.DrawPolygon(COut(), pts) (Nativo/Pixel.cs)
            return x + sz;
        }
        // Icono del BOTON DERECHA (flecha amarilla, espejo de la izquierda)
        private static float CtrlDirRight(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int sz, Color c)   // EN LINUX: el lienzo de Skia y el estado del que llama; las firmas de XP con un Graphics se borraron del puente (limpieza-puentes): todos los que la llaman pasan el lienzo
        {
            PointF[] pts = ctrlTri3 ?? (ctrlTri3 = new PointF[3]);
            pts[0] = new PointF(x + sz, cy); pts[1] = new PointF(x, cy - sz * 0.5f); pts[2] = new PointF(x, cy + sz * 0.5f);
            Pixel.RellenaPoligono(g, pts, CB(c), suave, true, medio, copia); Pixel.TrazaPoligono(g, pts, COut(), suave, medio, copia);   // EN LINUX: g.FillPolygon(CB(c), pts) y g.DrawPolygon(COut(), pts)
            return x + sz;
        }

        // Solo recursos PROPIOS de las vistas, nunca Font/Cover/Sky prestados.
        private static void SueltaDibujo<T>(ref T resource) where T : class, IDisposable
        {
            T old = resource; resource = null;
            if (old != null) old.Dispose();
        }
        private static void SueltaBitmaps(ref SKBitmap[] images)   // EN LINUX: Bitmap[] -> SKBitmap[]: los mapas son SKBitmap (Form1.Vistas.cs, ya traducido, la llama directo)
        {
            SKBitmap[] old = images; images = null;   // EN LINUX: SKBitmap
            if (old != null) for (int i = 0; i < old.Length; i++) if (old[i] != null) old[i].Dispose();
        }

        private partial class AlbumView : IVista, IDisposable   // EN LINUX: : Control -> sin herencia (port nativo, vistas-pared: W3b): una vista de la pantalla nativa (Nativo/IVista.cs) con su CajaVista; en la ventana de la capa la pone su hueco (Plataforma/Anfitrion.cs), que la pinta con Pinta y le da el raton como WinForms al control (Raton); partial como en XP (FluidezGpu.cs, CaratulasSeparadasGpu.cs)
        {
            public List<Album> Page = new List<Album>();
            public int Selected = 0;
            public double SelAngle = 0;
            public double Phase = 0;
            public Color BgAccent = DefaultAccent;
            public Func<Album, SKBitmap> GetCover;   // EN LINUX: Image: las portadas las da Form1 (LoadCoverAsync, Form1.Biblioteca.cs): SKBitmap de la cache de portadas; se comparan por identidad como en XP
            public Func<string, SKBitmap> GetCoverByPath;   // discos TOP: caratula por ruta (ciclado)   // EN LINUX: Image: SKBitmap de la cache (LoadCoverByPath)
            public Action<int> OnActivate;
#if ROCOLA_GPU
            public bool GpuCovered;   // el panel GPU (pantalla completa, opaco) tapa esta vista: no malgastes su OnPaint (rejilla+estrellas) por CPU
#endif
            public float BgDim = 1f;  // atenuacion del starfield mientras se navega con musica (el form la anima)

            private SKBitmap cubeCache;   // EN LINUX: Bitmap: un SKBitmap (BGRA premultiplicado)
            public float WallOpacity = 1f;
            private SKBitmap wallLayer;   // EN LINUX: Bitmap: un SKBitmap
            private int cacheW = -1, cacheH = -1;
            private string cacheSig = null;
            // CACHE DE CELDAS: cada disco (portada estatica + etiqueta) se HORNEA a un bitmap UNA vez y luego se
            // COPIA (blit 1:1) en cubeCache al paginar. Antes cada pagina re-ESCALABA 18 portadas y re-MAQUETABA
            // 18 etiquetas (ajuste de linea) -> carisimo al paginar rapido. Ahora paginar es casi solo copias.
            // Nace: cuando se horneo. Hace falta para saber si una casilla chica es de ESTE deslizamiento o
            // quedo de uno de hace rato (ver PeekOrBakeCell).
            private sealed class CellSprite { public SKBitmap Bmp; public bool HasCover; public int Uso; public int Nace; }   // EN LINUX: el mapa de la casilla, un SKBitmap
            // CUANTO SE ENCOGE la casilla mientras la pagina se desliza. A la mitad de lado son la CUARTA
            // PARTE de pixeles, y por tanto la cuarta parte de bytes subidos a la tarjeta grafica: ahi es
            // donde de verdad se atasca el hojeo rapido. El DIBUJO no era el problema y se midio -las 18
            // casillas enteras cuestan 2.9 ms a 1280x1024, y a la mitad 1.7-; lo caro es la subida.
            private const float CeldaChica = 0.5f;
            // EL INTERRUPTOR DE TODO ESTO (Ctrl+F3 -> Imagen). Manda sobre las DOS cosas a la vez -la media
            // resolucion y el aplazar el re-horneado- porque las dos son lo mismo visto de dos maneras:
            // aligerar el momento en que la pagina se mueve. Apagado, la pared se dibuja pasando paginas
            // exactamente como antes de que esto existiera. Lo pone el formulario, como Page o Sky.
            public bool HojeoLigero = true;
            private readonly Dictionary<string, CellSprite> cellCache = new Dictionary<string, CellSprite>();
            private int cellCacheW = -1, cellCacheH = -1;   // tamano con el que se horneo (si cambia, se limpia todo)
            private int cellUso;                            // reloj de uso: sube cada vez que se toca una casilla
            // CUANTAS CASILLAS CABEN EN EL CAJON: las doce paginas de historial que se midieron como el punto
            // dulce (ver CeldasHistorialPaginas), salvo que a este tamano de pantalla no quepan en el techo de
            // memoria. El suelo son las seis de siempre.
            private int capCeldas = 6 * PageSize;
            private int keepCeldas = 4 * PageSize;   // a cuantas se baja al podar
            private void RecalculaCajon(int w, int h)
            {
                long porCasilla = (long)Math.Max(1, w / Cols) * Math.Max(1, h / Rows) * 4L;
                long techo = ((long)CacheCeldasMb() * 1024L * 1024L) / Math.Max(porCasilla, 1L);
                long cabe = CeldasHistorialPaginas * PageSize;
                if (cabe > techo) cabe = techo;
                if (cabe < 6 * PageSize) cabe = 6 * PageSize;   // nunca menos historial que antes
                capCeldas = (int)cabe;
                // Al podar se baja un octavo (y al menos dos paginas): asi la poda no salta en cada pagina.
                int baja = capCeldas / 8; if (baja < 2 * PageSize) baja = 2 * PageSize;
                keepCeldas = capCeldas - baja;
                if (keepCeldas < 4 * PageSize) keepCeldas = 4 * PageSize;
            }
            private bool cubesPending;                       // (CPU) quedan celdas por hornear: se reparte en varios repintados (como en GPU) para no congelar en equipos lentos
            private const int CpuBakeBudget = 9;             // (CPU) celdas horneadas por repintado (media pagina): una pagina nueva se llena en 2 pasadas, sin congelon
#if ROCOLA_GPU
            // Atlas GPU: conserva medidas y filtro pero no guarda las columnas transparentes
            // del disco ni una superficie grande para sus dos lineas de texto.
            private sealed class CeldaGpu { internal SKBitmap Bmp; internal GpuCellLayout Layout; internal SKBitmap Cover; internal long Uso; internal bool Queued, Lod; }   // EN LINUX: el mapa de la casilla, un SKBitmap (lo sube la tarjeta tal cual: D3D9Star.cs); la portada con que se hizo, el SKBitmap de la cache (solo se compara por identidad)
            private readonly Dictionary<string, CeldaGpu> gpuCellCache = new Dictionary<string, CeldaGpu>();
            private readonly Dictionary<Album, CeldaGpu> gpuAnonymousCache = new Dictionary<Album, CeldaGpu>();
            private long gpuCellUso;
            private bool gpuPruning;
            private void ClearGpuCells()
            {
                foreach (CeldaGpu c in gpuCellCache.Values) c.Bmp.Dispose(); gpuCellCache.Clear();
                foreach (CeldaGpu c in gpuAnonymousCache.Values) c.Bmp.Dispose(); gpuAnonymousCache.Clear();
                gpuCellUso = 0;
                gpuPruning = false;
            }
            private void PodaGpuCells()
            {
                int count = gpuCellCache.Count + gpuAnonymousCache.Count;
                if (count > capCeldas) gpuPruning = true;
                if (!gpuPruning) return;
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                for (int i = 0; i < 4 && count > keepCeldas; i++) {
                    if (i > 0 && System.Diagnostics.Stopwatch.GetTimestamp() - start >= System.Diagnostics.Stopwatch.Frequency * .0005) break;
                    string key = null; Album album = null; CeldaGpu victim = null;
                    foreach (KeyValuePair<string, CeldaGpu> row in gpuCellCache)
                        if (victim == null || row.Value.Uso < victim.Uso) { victim = row.Value; key = row.Key; }
                    foreach (KeyValuePair<Album, CeldaGpu> row in gpuAnonymousCache)
                        if (victim == null || row.Value.Uso < victim.Uso) { victim = row.Value; key = null; album = row.Key; }
                    if (victim == null) break;
                    victim.Bmp.Dispose();
                    if (key != null) gpuCellCache.Remove(key); else gpuAnonymousCache.Remove(album);
                    count--;
                }
                if (count <= keepCeldas) gpuPruning = false;
            }
            internal static GpuCellLayout PlanAtlasGpu(int cw, int side, int labelH, int gap, bool enCola)
            {
                GpuCellLayout p = new GpuCellLayout();
                int x = (cw - side) / 2;
                int l = Math.Max(0, (int)Math.Round(x + side * .5 - side * .5 / Math.Sqrt(2)) - 2);
                int r = Math.Min(cw, (int)Math.Round(x + side * .5 + side * .5 / Math.Sqrt(2)) + 3);
                if (enCola)
                {
                    float d = Math.Max(13f, side * .26f), q = x + side - d * .92f;
                    float margin = Math.Max(1f, d * .07f) * .5f + 2f;
                    l = Math.Min(l, Math.Max(0, (int)Math.Floor(q - margin)));
                    r = Math.Max(r, Math.Min(cw, (int)Math.Ceiling(q + d + margin)));
                }
                int split = (cw + 1) / 2, width = Math.Max(r - l + 2, split + 2);
                int faceH = side + 2, height = faceH + 2 + 2 * (labelH + 2);
                if (cw < 4 || r <= l || GpuAtlasBytes(width, height) >= GpuAtlasBytes(cw, side + gap + labelH)
                    || (long)width * height >= (long)cw * (side + gap + labelH)) return p;
                p.FaceAt = new Rectangle(l, 0, r - l, faceH);
                p.Face = new Rectangle(1, 1, r - l, faceH);
                p.Text1At = new Rectangle(0, side + gap, split, labelH);
                p.Text1 = new Rectangle(1, faceH + 3, split, labelH);
                p.Text2At = new Rectangle(split, side + gap, cw - split, labelH);
                p.Text2 = new Rectangle(1, faceH + 3 + labelH + 2, cw - split, labelH);
                p.Width = width; p.Height = height; return p;
            }
            private static long GpuAtlasBytes(int w, int h)
            { long tw = 1, th = 1; while (tw < w) tw <<= 1; while (th < h) th <<= 1; return tw * th * 4; }
            // Referencia del atlas anterior para comparaciones de regresion. La ruta de produccion
            // usa BakeSeparatedCell y no vuelve a ampliar una portada al rasterizar su etiqueta.
            private SKBitmap BakeGpuCell(Album a, int cw, int side, int labelH, int gap, SKBitmap cover, out GpuCellLayout layout)   // EN LINUX: el mapa y los puntos de la portada, SKBitmap
            {
                bool q = EnCola != null && EnCola(a);
                layout = PlanAtlasGpu(cw, side, labelH, gap, q);
                if (layout.Width == 0)
                {
                    SKBitmap whole = BakeCell(a, cw, side, labelH, gap, cover);   // EN LINUX: SKBitmap
                    if (cover == null && PortadasLod)
                        using (SKCanvas g = new SKCanvas(whole))   // EN LINUX: Graphics.FromImage
                        { using (SKPaint transparente = new SKPaint { Color = Color.Transparent.Sk() }) Pixel.Rellena(g, new RectangleF(0, 0, cw, side), transparente, false, false, true); }   // EN LINUX: g.CompositingMode = SourceCopy y g.FillRectangle(Brushes.Transparent, 0, 0, cw, side): el relleno con copia (sin suavizado ni medio pixel, de fabrica) (Nativo/Pixel.cs)
                    return whole;
                }
                SKBitmap bmp = Mapas.Nuevo(layout.Width, layout.Height);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb) (Nativo/Mapas.cs)
                try
                {
                    using (SKCanvas g = new SKCanvas(bmp))   // EN LINUX: Graphics.FromImage
                    {
                        g.Clear(Color.Transparent.Sk()); Filtro filtro = Filtro.Bilineal;   // EN LINUX: Graphics.Clear y g.InterpolationMode = Bilinear, en una variable
                        bool suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias, en una variable
                        g.Save(); g.ClipRect(new SKRect(layout.Face.Left, layout.Face.Top, layout.Face.Right, layout.Face.Bottom), SKClipOperation.Intersect, false);   // EN LINUX: g.SetClip(layout.Face): el recorte de pixeles enteros, sin suavizar, en un Save del lienzo (no se quita: el lienzo se suelta al acabar)
                        g.Translate(layout.Face.X - layout.FaceAt.X, layout.Face.Y);   // EN LINUX: g.TranslateTransform
                        Rectangle disco = new Rectangle((cw - side) / 2, 0, side, side);
                        if (cover != null || !PortadasLod) Cube.Draw(g, suave, false, filtro, false, disco, cover, 0.0);   // EN LINUX: el lienzo y su estado (copia y medio pixel, de fabrica)
                        if (q) Palomito(g, suave, disco);   // EN LINUX: el lienzo y su suavizado
                    }
                    // Texto una sola vez, con las mismas medidas/fuente. Copias PArgb sin recomponer alfa.
                    using (SKBitmap label = Mapas.Nuevo(cw, labelH + 2))   // EN LINUX: new Bitmap(cw, labelH + 2, Format32bppPArgb)
                    {
                        using (SKCanvas g = new SKCanvas(label))   // EN LINUX: Graphics.FromImage
                        {
                            g.Clear(Color.Transparent.Sk());   // EN LINUX: Graphics.Clear; g.SmoothingMode = AntiAlias no se lleva a ninguna variable: aqui solo se escribe, y DrawString no mira el suavizado (mira el modo de letra, el de fabrica)
                            EnsureLabelRes();
                            TextoGdiPlus.Escribe(g, AlbumLabel(a), lblFont, AjusteLetra.DelSistema, lblBrush, new Rectangle(2, 1, cw - 4, labelH), lblSF);   // EN LINUX: g.DrawString en esa caja con ese formato y el modo de letra de fabrica (SystemDefault) (Nativo/TextoGdiPlus.cs)
                        }
                        CopiaEtiquetaGpu(label, bmp, layout.Text1At, layout.Text1);
                        CopiaEtiquetaGpu(label, bmp, layout.Text2At, layout.Text2);
                    }
                    return bmp;
                }
                catch { bmp.Dispose(); throw; }
            }
            private static void CopiaEtiquetaGpu(SKBitmap source, SKBitmap dest, Rectangle at, Rectangle to)   // EN LINUX: los dos mapas, SKBitmap
            {
                // Un pixel vecino a cada lado evita costuras con filtrado bilineal durante el deslizamiento.
                int from = Math.Max(0, at.X - 1), end = Math.Min(source.Width, at.Right + 1);
                Rectangle src = new Rectangle(from, 0, end - from, source.Height);
                Rectangle dst = new Rectangle(to.X + from - at.X, to.Y - 1, src.Width, src.Height);
                IntPtr s = IntPtr.Zero, d = IntPtr.Zero;   // EN LINUX: BitmapData s = null, d = null: los punteros a los puntos del trozo (los mapas ya estan en BGRA premultiplicado, que es lo que LockBits daba con Format32bppPArgb)
                try
                {
                    s = (IntPtr)(source.GetPixels().ToInt64() + (long)src.Y * source.RowBytes + (long)src.X * 4);   // EN LINUX: source.LockBits(src, ReadOnly, Format32bppPArgb): los puntos mismos desde la esquina del trozo, con el paso del mapa
                    d = (IntPtr)(dest.GetPixels().ToInt64() + (long)dst.Y * dest.RowBytes + (long)dst.X * 4);   // EN LINUX: dest.LockBits(dst, WriteOnly, Format32bppPArgb)
                    for (int y = 0; y < src.Height; y++) unsafe { Buffer.MemoryCopy(   // EN LINUX: RtlMoveMemory de kernel32 = Buffer.MemoryCopy (origen, destino; vale aunque se pisen)
                        (void*)(s.ToInt64() + (long)y * source.RowBytes),   // EN LINUX: s.Scan0 y s.Stride
                        (void*)(d.ToInt64() + (long)y * dest.RowBytes), src.Width * 4, src.Width * 4); }   // EN LINUX: d.Scan0 y d.Stride
                }
                finally { if (d != IntPtr.Zero) dest.NotifyPixelsChanged(); }   // EN LINUX: dest.UnlockBits(d) (lo escrito queda avisado) y source.UnlockBits(s) (de solo leer: nada)
            }
            private SKBitmap PeekGpuCell(Album a, int cw, int side, int labelH, int gap, SKBitmap cover, bool visible, ref int budget, out GpuCellLayout layout)   // EN LINUX: el mapa de la casilla, un SKBitmap; la portada, el SKBitmap de la cache (aqui solo se compara y se apunta)
            {
                layout = new GpuCellLayout(); layout.Separate = true; layout.HideBlank = PortadasLod;
                string key = ClaveCasilla(a); CeldaGpu c = null;
                if (key != null) gpuCellCache.TryGetValue(key, out c);
                else gpuAnonymousCache.TryGetValue(a, out c);
                bool queued = key == null && EnCola != null && EnCola(a);
                if (c != null && ((c.Layout.Separate && c.Queued == queued && c.Lod == PortadasLod)
                    || (ReferenceEquals(c.Cover, cover) && c.Queued == queued && (cover != null || c.Lod == PortadasLod)) || budget <= 0))
                { c.Cover = cover; c.Uso = ++gpuCellUso; layout = c.Layout; return c.Bmp; }
                if (budget <= 0 || (CoverControl != null && (CoverControl.DeferBake || !CoverControl.CanStart(gpuBakeCost, visible, false))))
                { if (c != null) { c.Uso = ++gpuCellUso; layout = c.Layout; return c.Bmp; } return null; }
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                budget--; SKBitmap bmp = BakeSeparatedCell(a, cw, side, labelH, gap, out layout);   // EN LINUX: SKBitmap
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - start;
                gpuBakeCost = CargaTrabajo.AprendeCoste(gpuBakeCost, elapsed);
                if (CoverControl != null) CoverControl.Spend(elapsed);
                if (c != null) c.Bmp.Dispose();
                CeldaGpu next = new CeldaGpu { Bmp = bmp, Layout = layout, Cover = cover, Uso = ++gpuCellUso, Queued = queued, Lod = PortadasLod };
                if (key != null) gpuCellCache[key] = next; else gpuAnonymousCache[a] = next;
                return bmp;
            }
            // La rejilla por GPU ya NO usa una textura a pantalla completa (cubeCache): el panel pide las celdas
            // con BuildGpuGrid y dibuja cada disco como su propio quad. cubeCache queda solo para el render por CPU.
            public SKBitmap ContourSpriteBmp { get { EnsureContourSprite(); return contourSprite; } }   // sprite del resalte (lo hornea si falta: el panel GPU lo lee aunque el OnPaint por CPU este suprimido)   // EN LINUX: un SKBitmap (la tarjeta lo sube tal cual: GetContour, D3D9Star.cs)
            public Rectangle ContourScreenRect()                     // donde va el contorno (celda seleccionada)
            {
                if (contourSprite == null || Page == null || Selected < 0 || Selected >= Page.Count) return Rectangle.Empty;
                if (Sliding) return Rectangle.Empty;   // hojeando: nada seleccionado hasta que la pagina termine de entrar
                int w = Width, h = Height; if (w < 8 || h < 8) return Rectangle.Empty;
                int cw = w / Cols, ch = h / Rows; if (cw < 4 || ch < 4) return Rectangle.Empty;
                Rectangle area, lbl; Geometry(w, h, Selected, out area, out lbl);
                Rectangle fr = area; fr.Inflate(4, 4);
                return new Rectangle(fr.X - ContourMargin, fr.Y - ContourMargin, contourSprite.Width, contourSprite.Height);
            }

            // --- Cubos que GIRAN (seleccionado + relevo) para el modo GPU ---
            // En CPU se dibujan ENCIMA de los cubos estaticos en cada cuadro (Cube.Draw). En GPU le pasamos
            // al panel la celda, el angulo y la(s) portada(s): el panel sube la portada como textura UNA vez
            // (al cambiar) y dibuja el cubo girando como GEOMETRIA. Asi el giro casi no cuesta CPU.


            // Arma el estado de los cubos que giran para que el panel GPU los dibuje (mismo aspecto que CPU).
            public GpuCubeState BuildCubeState()
            {
                GpuCubeState st = new GpuCubeState();
                int w = Width, h = Height;
                if (Page == null || w < 8 || h < 8) return st;
                int cw = w / Cols, ch = h / Rows;
                if (cw < 4 || ch < 4) return st;
                // HOJEANDO: la rejilla entera se desliza, pero el cubo que gira y el resalte se dibujan en su
                // sitio fijo. Si se pintaran, se verian clavados y girando mientras todo lo demas pasa de
                // largo. Durante el deslizamiento no hay nada seleccionado; el cubo aparece -plano y quieto,
                // porque el giro tampoco avanza (ver StepSpin)- justo cuando la pagina nueva termina de entrar.
                if (Sliding) return st;
                if (Selected >= 0 && Selected < Page.Count)
                {
                    Rectangle a, l; Geometry(w, h, Selected, out a, out l);
                    SKBitmap ca, cb; float f; SelectedCovers(out ca, out cb, out f);   // EN LINUX: Image: la portada de la cache de portadas, un SKBitmap
                    st.Sel.Has = true; st.Sel.X = a.X; st.Sel.Y = a.Y; st.Sel.W = a.Width; st.Sel.H = a.Height;
                    st.Sel.Angle = SelAngle; st.Sel.CoverA = ca as SKBitmap; st.Sel.CoverB = cb as SKBitmap; st.Sel.Fade = f;   // EN LINUX: as Bitmap: las portadas del cubo que gira van a la tarjeta (GpuCubeInfo, D3D9Star.cs) como SKBitmap de la cache
                    st.Sel.Key = Page[Selected];
                }
                if (prevIdx >= 0 && prevIdx != Selected && prevIdx < Page.Count)   // RELEVO: el que dejamos, girando y decayendo
                {
                    Rectangle a2, l2; Geometry(w, h, prevIdx, out a2, out l2);
                    st.Relay.Has = true; st.Relay.X = a2.X; st.Relay.Y = a2.Y; st.Relay.W = a2.Width; st.Relay.H = a2.Height;
                    st.Relay.Angle = prevAngle; st.Relay.CoverA = ((GetCover != null) ? GetCover(Page[prevIdx]) : null) as SKBitmap;   // EN LINUX: as Bitmap: la portada de la cache de portadas, un SKBitmap
                    st.Relay.Key = Page[prevIdx];
                }
                return st;
            }

            private const int GridBakeBudget = 6;   // celdas NUEVAS horneadas por cuadro (barrer paginas nuevas sin bajar fps)
            // HOJEO: la pagina nueva ENTRA deslizandose por el lado hacia el que vas. No cuesta un solo dibujo
            // mas -las casillas ya se pintan cada cuadro-, solo cambian de sitio; por eso es practicamente gratis.
            // El avance se calcula EN VIVO con el reloj (no con el temporizador de 24): se ve fluido a los fps
            // que de el equipo, igual que las demas animaciones de la rocola.
            public const int SlideMs = 230;
            private int slideTick = -1; private float slideFrom;
            private int gpuLastSlideTick;
            private bool gpuHasSlideTick, gpuRapidSlide;
            private long gpuBakeCost;
            private static readonly long GpuBakeLimit = CargaTrabajo.LimiteTicks(CargaTrabajo.Procesadores);
            internal static bool RapidPageTurn(int now, int previous, bool hasPrevious)
            { return hasPrevious && unchecked((uint)(now - previous)) < 300u; }
            public void StartSlide(int dir, int ancho)   // dir: +1 la nueva entra por la derecha, -1 por la izquierda
            {
                bool enCurso = Sliding;   // (antes de tocar nada) si la anterior aun no habia llegado
                if (ancho < 8) { slideTick = -1; return; }
                int now = TickVivo();
                gpuRapidSlide = RapidPageTurn(now, gpuLastSlideTick, gpuHasSlideTick);
                gpuLastSlideTick = now; gpuHasSlideTick = true;
                // PASAR DE PAGINA DESLIZANDO: la que se estaba viendo sale mientras entra la nueva. No si la pagina no
                // cambio (no hay de donde salir) ni si la anterior iba a medio entrar: habria que sacarla de donde
                // estaba, no de su sitio, y daria un salto. Ahi se va de golpe, como siempre.
                slideViejas = (PaginaDesliza && !enCurso && paginaVista != null && !ReferenceEquals(paginaVista, Page)) ? paginaVista : null;
                double fino = MsFino();
                if (PaginaDesliza)
                {
                    // Un empujon nuevo NO tira lo que le faltaba al anterior: los dos siguen cada uno su curva y se suman
                    // (TomaEmpuje). Antes el nuevo empezaba de cero: hojeando rapido el cielo frenaba en seco y volvia a
                    // arrancar en cada pagina.
                    int k = empujeSig; empujeSig = (empujeSig + 1) % EmpujesVivos;
                    empujeHechoN[k] = 0f; empujeDirN[k] = dir; empujeDesdeN[k] = fino;
                }
                slideFrom = dir * (float)ancho; slideTick = now; slideFino = fino;
            }
            // ---------------- PASAR DE PAGINA DESLIZANDO (Ctrl+F3 -> Pantalla; PantallaVivaGpu.cs) ----------------
            // Las dos paginas van en fila, una detras de otra, y cada columna sale con un pequeño retraso respecto
            // a la que va delante en el sentido del movimiento: primero la vieja, columna a columna, y detras la
            // nueva. Todas siguen LA MISMA curva, solo que mas tarde, asi que ninguna alcanza a la de delante: se
            // separan un poco por el camino y se vuelven a juntar al llegar. Todo cabe en SlideMs, como antes.
            public bool PaginaDesliza = true;
            private List<Album> paginaVista, slideViejas;   // la ultima pagina dibujada, y la que esta saliendo
            // Los numeros salen de simular la pared a mitad de camino: con 8 ms entre columna y columna, y la vieja
            // apagandose pronto, a los ~100 ms quedaba cubierta menos de un tercio (parecia vacia). Con 4 ms, la nueva
            // apareciendo en su primer cuarto de camino y la vieja apagandose solo al final, nunca baja del 60 %, y el
            // escalonado se sigue viendo (44 ms de punta a punta, casi tres cuadros).
            internal const int DesfaseCol = 4;                                   // ms entre una columna y la siguiente
            internal const int ViajeCol = SlideMs - (2 * Cols - 1) * DesfaseCol; // lo que tarda cada columna (186 ms)
            // En que turno se mueve una columna: primero la de delante segun hacia donde va todo, y la vieja antes
            // que la nueva (van en fila).
            internal static int OrdenColumna(int col, bool haciaIzquierda, bool vieja)
            {
                int k = haciaIzquierda ? col : (Cols - 1 - col);
                return vieja ? k : Cols + k;
            }
            // Cuanto le FALTA a esa columna (1 = aun no salio, 0 = ya llego), y su opacidad: la nueva aparece al
            // empezar a moverse y la vieja se apaga al final de su camino. La curva es mitad "arranca y frena suave" y
            // mitad "arranca ya y frena": sola, la primera tardaba 30 ms en moverse algo que se viera (el boton se
            // sentia lento); sola, la segunda arranca de golpe. Juntas, la primera columna ya se mueve en el primer
            // cuadro, el paso mas grande es de un 1 % del camino por milisegundo, y la pared nunca queda cubierta menos
            // de un 75 % a mitad de camino (simulado a 1000 px).
            internal static float CurvaColumna(int dt, int orden, bool vieja, out float alpha)
            {
                return CurvaColumnaFina(dt, orden, vieja, out alpha);   // un entero se pasa a float exacto: la misma cuenta
            }
            internal static float CurvaColumnaFina(float dt, int orden, bool vieja, out float alpha)
            {
                float u = (dt - orden * DesfaseCol) / (float)ViajeCol;
                if (!(u > 0f)) u = 0f; else if (u > 1f) u = 1f;
                float q = 1f - u;
                float suave = (u < .5f) ? 4f * u * u * u : 1f - 4f * q * q * q;
                float e = .5f * suave + .5f * (1f - q * q * q);
                if (vieja) alpha = 1f - Math.Min(1f, Math.Max(0f, (u - .4f) / .55f));
                else alpha = Math.Min(1f, u / .25f);
                return 1f - e;
            }
            // EL CIELO SE CORRE un poco al pasar de pagina, en el mismo sentido y mucho mas despacio que las portadas
            // (un 4.5 % del ancho de la pared): lo lejano se mueve menos, y eso se lee como profundidad.
            internal const float EmpujeFr = .045f;
            // EL RELOJ FINO (13-10-2026). El deslizamiento de la pared y el empujon del cielo se median con Environment.TickCount,
            // que en Windows avanza a saltos de unos 15,6 ms: a 60 cuadros por segundo, un cuadro veia el tiempo quieto y el
            // siguiente un salto doble, y el cielo -que se mueve despacio y se ve entero- iba a tirones aunque los FPS no
            // bajaran. Ahora la parte de la tarjeta usa el cronometro de alta resolucion. (Sliding sigue con TickCount: lo
            // usa tambien el dibujo por CPU, que no se toca.)
            internal static double MsFino() { return System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency; }
            private double slideFino = -1;
            internal const int EmpujesVivos = 4;   // empujones a la vez (hojeando rapido se solapan)
            private readonly float[] empujeHechoN = { 1f, 1f, 1f, 1f };
            private readonly int[] empujeDirN = new int[EmpujesVivos];
            private readonly double[] empujeDesdeN = new double[EmpujesVivos];
            private int empujeSig;
            internal static float CurvaCielo(int dt)
            {
                if (dt <= 0) return 0f;
                if (dt >= SlideMs) return 1f;
                float u = dt / (float)SlideMs;
                return u * u * (3f - 2f * u);
            }
            // Las tres curvas con el tiempo en fraccion de milisegundo (el reloj fino de la tarjeta): la misma cuenta.
            internal static float CurvaCieloFina(float dt)
            {
                if (!(dt > 0f)) return 0f;
                if (dt >= SlideMs) return 1f;
                float u = dt / (float)SlideMs;
                return u * u * (3f - 2f * u);
            }
            // Lo que hay que correr el cielo DESDE LA ULTIMA VEZ que se pregunto (px; negativo = a la izquierda). Si
            // nadie lo pregunto mientras pasaba la pagina (la pared estaba tapada), lo que faltaba se tira: correr el
            // cielo de golpe mas tarde seria un salto.
            public float TomaEmpuje()
            {
                float total = 0f; double ahora = -1;
                for (int k = 0; k < EmpujesVivos; k++)
                {
                    if (empujeHechoN[k] >= 1f) continue;
                    if (ahora < 0) ahora = MsFino();
                    double dt = ahora - empujeDesdeN[k];
                    if (!PaginaDesliza || dt < 0 || dt > SlideMs + 150) { empujeHechoN[k] = 1f; continue; }
                    float p = CurvaCieloFina((float)dt);
                    float d = p - empujeHechoN[k]; empujeHechoN[k] = p;
                    total += -empujeDirN[k] * EmpujeFr * Width * d;
                }
                return total;
            }
            // El lado de la portada de una casilla (el mismo calculo que Geometry), para el letrero de la elegida.
            internal static int LadoPortada(int w, int h)
            {
                int side, labelH, gap; CellMetrics(Math.Max(1, w / Cols), Math.Max(1, h / Rows), out side, out labelH, out gap);
                return side;
            }
            // Donde esta la portada elegida (coords de la pared); vacio si no hay o si la pagina se esta moviendo.
            public Rectangle RectElegida()
            {
                if (Page == null || Selected < 0 || Selected >= Page.Count || Sliding) return Rectangle.Empty;
                int w = Width, h = Height; if (w / Cols < 4 || h / Rows < 4) return Rectangle.Empty;
                Rectangle area, lbl; Geometry(w, h, Selected, out area, out lbl);
                return area;
            }
            // La pagina que SE VA: sus casillas tal como se vieron -sin hornear nada ni pedir portadas-, un ancho de
            // pared por detras de la nueva. Van en los puestos PageSize.. para no pisar las texturas por puesto de la
            // nueva, y DESPUES de ella en el buffer: si no caben, la que falta es la que sale, nunca la que entra.
            private int CeldasViejas(GpuGridCell[] buf, int n, int w, int h, int cw, int side, int labelH, int gap, float dt, int ox, int oy)
            {
                bool izq = slideFrom > 0f;
                int nada = 0;
                for (int i = 0; i < slideViejas.Count && i < PageSize && n < buf.Length; i++)
                {
                    Album a = slideViejas[i];
                    int col = i % Cols;
                    float al; float falta = CurvaColumnaFina(dt, OrdenColumna(col, izq, true), true, out al);
                    float cdx = slideFrom * (falta - 1f);
                    Rectangle area, lbl; Geometry(w, h, i, out area, out lbl);
                    SKBitmap tapa = (PeekCoverGpu != null) ? PeekCoverGpu(a) : null;   // EN LINUX: Image: la portada de la cache de portadas, un SKBitmap
                    buf[n] = new GpuGridCell();
                    buf[n].Key = ClaveCasilla(a);
                    buf[n].Bmp = PeekGpuCell(a, cw, side, labelH, gap, tapa, true, ref nada, out buf[n].Layout);
                    buf[n].Direct = true; buf[n].FullCover = tapa as SKBitmap;   // EN LINUX: as Bitmap: la portada entera va a la tarjeta (GpuGridCell.FullCover, D3D9Star.cs) como SKBitmap de la cache
                    CeldaGpu cached;
                    buf[n].HasCover = buf[n].Key != null && gpuCellCache.TryGetValue(buf[n].Key, out cached) && cached.Cover != null;
                    buf[n].Preview = PortadasLod && PeekPreviewGpu != null ? PeekPreviewGpu(a) as SKBitmap : null;   // EN LINUX: as Bitmap: la previa es un SKBitmap (gpuPreviewCache, CaratulasAdaptativas.cs)
                    buf[n].Placeholder = HojeoLigero;
                    buf[n].CoverRect = new RectangleF(ox + area.X + cdx, oy + area.Y, area.Width, area.Height);
                    buf[n].Slot = PageSize + i;
                    buf[n].X = ox + col * cw + cdx; buf[n].Y = oy + area.Y; buf[n].W = cw; buf[n].H = side + gap + labelH;
                    buf[n].A = al;
                    n++;
                }
                return n;
            }
            public bool Sliding { get { return slideTick != -1 && Environment.TickCount - slideTick < SlideMs; } }
            // CURVA del hojeo (funcion pura, para poder comprobarla): devuelve cuanto FALTA por recorrer
            // (1 = recien empieza, 0 = ya llego) y la opacidad de entrada.
            internal static float CurvaHojeo(int dt, out float alpha)
            {
                alpha = 1f;
                if (dt < 0 || dt >= SlideMs) return 0f;
                float t = dt / (float)SlideMs;
                float e = 1f - (1f - t) * (1f - t) * (1f - t);   // frenado suave al llegar (cubico)
                alpha = t < 0.45f ? (t / 0.45f) : 1f;
                return 1f - e;
            }
            internal static float CurvaHojeoFina(float dt, out float alpha)
            {
                alpha = 1f;
                if (!(dt >= 0f) || dt >= SlideMs) return 0f;
                float t = dt / (float)SlideMs;
                float e = 1f - (1f - t) * (1f - t) * (1f - t);   // frenado suave al llegar (cubico)
                alpha = t < 0.45f ? (t / 0.45f) : 1f;
                return 1f - e;
            }
            // Devuelve el desplazamiento de este cuadro y, de paso, el desvanecido de entrada.
            private float SlideNow(out float alpha)
            {
                alpha = 1f;
                if (slideTick == -1) return 0f;
                float dt = (float)(MsFino() - slideFino);   // reloj fino (ver MsFino)
                if (dt < 0 || dt >= SlideMs) { slideTick = -1; return 0f; }
                return slideFrom * CurvaHojeoFina(dt, out alpha);
            }

            // Llena 'buf' con las celdas (disco estatico + etiqueta) de la pagina actual, en coords de PANTALLA (el
            // panel GPU es a pantalla completa). Cada celda trae su bitmap ya horneado para que el panel lo suba como
            // su PROPIA textura (cacheada por Folder). Se llama cada cuadro: para celdas cacheadas es casi gratis; las
            // nuevas se hornean con presupuesto (el resto sale null y el panel las salta hasta el proximo cuadro).
            public int BuildGpuGrid(GpuGridCell[] buf)
            {
                if (Page == null || buf == null) return 0;
                int w = Width, h = Height;
                if (w < 8 || h < 8) return 0;
                int cw = w / Cols, ch = h / Rows;
                if (cw < 4 || ch < 4) return 0;
                if (cellCacheW != w || cellCacheH != h) { ClearCellCache(); cellCacheW = w; cellCacheH = h; RecalculaCajon(w, h); }   // tamano nuevo: rehornear (y el cajon cambia de tope: la casilla mide otra cosa)
                else PodaGpuCells();
                int side, labelH, gap; CellMetrics(cw, ch, out side, out labelH, out gap);
                // Una pagina aislada muestra las caratulas desde el primer cuadro disponible.
                // Solo ocultarlas al encadenar paginas rapido; nunca degradar su resolucion.
                bool chica = Sliding && HojeoLigero && gpuRapidSlide;
                float alpha; float dx = SlideNow(out alpha);   // hojeo: desplazamiento y desvanecido de ESTE cuadro
                // Pasar de pagina deslizando: cada columna lleva su propio punto del camino (CurvaColumna).
                float dtHojeo = (slideTick != -1) ? (float)(MsFino() - slideFino) : -1f;   // reloj fino (ver MsFino)
                bool columnas = PaginaDesliza && dtHojeo >= 0;
                if (!columnas) slideViejas = null;
                bool izq = slideFrom > 0f;
                int ox = this.Left, oy = this.Top;   // coords de albumView -> pantalla (mismo criterio que los cubos que giran)
                int budget = CoverControl != null ? 32 : GridBakeBudget;
                long bakeT0 = System.Diagnostics.Stopwatch.GetTimestamp();
                int n = 0;
                for (int i = 0; i < Page.Count && i < PageSize && n < buf.Length; i++)
                {
                    Rectangle area, lbl; Geometry(w, h, i, out area, out lbl);
                    Album a = Page[i];
                    buf[n].Key = ClaveCasilla(a);   // CON la marca: el panel cachea sus texturas por esta clave
                    // En rafaga no hornear; en pasos normales reutilizar la cache y el presupuesto habitual.
                    long cellStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    int beforeBudget = budget;
                    int useBudget = chica ? 0 : budget;
                    buf[n].Layout = new GpuCellLayout();
                    SKBitmap cover = chica ? (PeekCoverGpu != null ? PeekCoverGpu(a) : null) : (GetCover != null ? GetCover(a) : null);   // EN LINUX: Image: la portada de la cache de portadas, un SKBitmap
                    buf[n].Bmp = chica && !PortadasLod ? null : PeekGpuCell(a, cw, side, labelH, gap, cover, true, ref useBudget, out buf[n].Layout);
                    buf[n].Direct = true; buf[n].FullCover = cover as SKBitmap;   // EN LINUX: as Bitmap: la portada de la cache de portadas, un SKBitmap
                    if (!chica) budget = useBudget;
                    CeldaGpu cached = null;
                    if (buf[n].Key != null) gpuCellCache.TryGetValue(buf[n].Key, out cached);
                    else gpuAnonymousCache.TryGetValue(a, out cached);
                    buf[n].HasCover = cached != null && cached.Bmp == buf[n].Bmp && cached.Cover != null;
                    buf[n].NeedsCover = !string.IsNullOrEmpty(a.CoverPath);
                    buf[n].Preview = PortadasLod && PeekPreviewGpu != null ? PeekPreviewGpu(a) as SKBitmap : null;   // EN LINUX: as Bitmap: la previa, un SKBitmap (gpuPreviewCache)
                    if (budget < beforeBudget)
                        gpuBakeCost = CargaTrabajo.AprendeCoste(gpuBakeCost, System.Diagnostics.Stopwatch.GetTimestamp() - cellStart);
                    buf[n].Placeholder = HojeoLigero;
                    float cdx = dx, ca = alpha;
                    if (columnas) cdx = slideFrom * CurvaColumnaFina(dtHojeo, OrdenColumna(i % Cols, izq, false), false, out ca);
                    buf[n].CoverRect = new RectangleF(ox + area.X + cdx, oy + area.Y, area.Width, area.Height);
                    buf[n].Slot = i;
                    buf[n].X = ox + (i % Cols) * cw + cdx; buf[n].Y = oy + area.Y; buf[n].W = cw; buf[n].H = side + gap + labelH;
                    buf[n].A = ca;
                    buf[n].Sel = (i == Selected) && dtHojeo < 0;   // la elegida (quieta): la portada enfocada no la apaga
                    n++;
                    if (CoverControl == null && budget < GridBakeBudget && budget > 0 &&
                        !CargaTrabajo.PuedeEmpezar(System.Diagnostics.Stopwatch.GetTimestamp() - bakeT0, gpuBakeCost, GpuBakeLimit, false))
                        budget = 0; // seguir consultando cache, aplazar solo composiciones nuevas
                }
                if (columnas && slideViejas != null) n = CeldasViejas(buf, n, w, h, cw, side, labelH, gap, dtHojeo, ox, oy);
                paginaVista = Page;
                return n;
            }
            public Func<Album, SKBitmap> PeekCoverGpu;   // EN LINUX: Image: la portada de la cache de portadas, un SKBitmap (lo pone Form1)
            public Func<Album, SKBitmap> PeekPreviewGpu;   // EN LINUX: Image: la previa, un SKBitmap (lo pone Form1)
            public bool PortadasLod = true;
            internal CoverGovernor CoverControl;
            // Solo referencias ya horneadas, en orden estable. Nunca lee disco ni compone imagenes.
            public int BuildGpuAhead(GpuGridCell[] cells)
            {
                if (Sliding || Ahead == null || cellCacheW != Width || cellCacheH != Height) return 0;
                int count = Math.Min(cells.Length, Ahead.Count);
                for (int i = 0; i < count; i++)
                {
                    string key = ClaveCasilla(Ahead[i]);
                    CeldaGpu sprite;
                    cells[i] = new GpuGridCell();
                    cells[i].Key = key;
                    if (key != null && gpuCellCache.TryGetValue(key, out sprite) && sprite.Cover != null)
                    { cells[i].Bmp = sprite.Bmp; cells[i].Layout = sprite.Layout; cells[i].HasCover = true;
                        cells[i].Direct = true; cells[i].FullCover = PeekCoverGpu != null ? PeekCoverGpu(Ahead[i]) as SKBitmap : null; }   // EN LINUX: as Bitmap: la portada de la cache de portadas, un SKBitmap
                }
                return count;
            }
            // Prepara solo imagenes YA disponibles, sin lecturas ni promocion a prioridad visible.
            // Volver a visitar el anillo permite recoger portadas que llegaron tras la primera pasada.
            public void BakeAheadGpu()
            {
                if (Ahead == null || Ahead.Count == 0 || PeekCoverGpu == null || Sliding) return;
                int w = Width, h = Height, cw = w / Cols, ch = h / Rows;
                if (cw < 4 || ch < 4 || cellCacheW != w || cellCacheH != h) return;
                int side, labelH, gap; CellMetrics(cw, ch, out side, out labelH, out gap);
                int allowance = CoverControl != null ? 32 : Math.Max(2, Math.Min(6, CargaTrabajo.Procesadores / 2));
                int budget = allowance, visited = 0;
                long begin = System.Diagnostics.Stopwatch.GetTimestamp();
                while (visited < Math.Min(24, Ahead.Count) && budget > 0 &&
                    (CoverControl != null || CargaTrabajo.PuedeEmpezar(System.Diagnostics.Stopwatch.GetTimestamp() - begin, aheadBakeTicks, GpuBakeLimit, budget == allowance)))
                {
                    if (aheadPos >= Ahead.Count) aheadPos = 0;
                    Album a = Ahead[aheadPos]; visited++;
                    if (a == null) { aheadPos++; continue; }
                    SKBitmap cover = PeekCoverGpu(a);   // EN LINUX: Image: la portada de la cache de portadas, un SKBitmap
                    if (cover == null) { aheadPos++; continue; } // no hornear un disco vacio
                    string key = ClaveCasilla(a); CeldaGpu prepared;
                    bool cached = key != null ? gpuCellCache.TryGetValue(key, out prepared) : gpuAnonymousCache.TryGetValue(a, out prepared);
                    if (cached && prepared.Cover == cover) { prepared.Uso = ++gpuCellUso; aheadPos++; continue; } // proteger vecinas al podar
                    int previous = budget;
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    GpuCellLayout layout;
                    if (CoverControl != null && !CoverControl.CanStart(aheadBakeTicks, false, false)) break;
                    PeekGpuCell(a, cw, side, labelH, gap, cover, false, ref budget, out layout);
                    if (budget < previous)
                    {
                        long cost = Math.Max(1L, System.Diagnostics.Stopwatch.GetTimestamp() - start);
                        aheadBakeTicks = Math.Max(cost, aheadBakeTicks - aheadBakeTicks / 4);
                    }
                    else break; // aplazado, no saltarlo: evitar inanicion por la cadencia del render
                    aheadPos++;
                }
                PodaGpuCells();
            }
#endif

            // PRE-HORNEADO de la pagina de al lado: mientras nadie toca nada, se van dejando listas las casillas
            // que se veran al pasar de pagina. Asi el hojeo se ve fluido LA PRIMERA VEZ y no solo al volver.
            // Vale para las DOS versiones: en la del bar (sin tarjeta grafica) es donde mas se agradece, porque
            // ahi re-escalar 18 caratulas justo al pulsar es lo que se siente como un tiron.
            public List<Album> Ahead;
            private int aheadPos;
            public void SetAhead(List<Album> lista) { Ahead = lista; aheadPos = 0; }
            private long aheadBakeTicks;
            private static readonly long AheadBudgetTicks = Math.Max(1L, System.Diagnostics.Stopwatch.Frequency / 250L);
            private static bool CanStartAhead(long elapsed, long estimate, int baked)
            {
                if (elapsed < 0 || elapsed >= AheadBudgetTicks) return false;
                // La primera casilla siempre puede intentarse: una estimacion cara no debe
                // impedir el progreso. Una tarea indivisible aun puede exceder los 4 ms.
                return baked == 0 || estimate <= AheadBudgetTicks - elapsed;
            }
            public void BakeAhead()
            {
                if (Ahead == null || aheadPos >= Ahead.Count) return;
                int w = Width, h = Height;
                if (w < 8 || h < 8) return;
                int cw = w / Cols, ch = h / Rows;
                if (cw < 4 || ch < 4 || cellCacheW != w || cellCacheH != h) return;
                int side, labelH, gap; CellMetrics(cw, ch, out side, out labelH, out gap);
                int budget = 2, visited = 0;
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                while (aheadPos < Ahead.Count && budget > 0 && visited < PageSize
                    && CanStartAhead(System.Diagnostics.Stopwatch.GetTimestamp() - t0, aheadBakeTicks, 2 - budget))
                {
                    Album a = Ahead[aheadPos++];
                    long begin = System.Diagnostics.Stopwatch.GetTimestamp();
                    int previous = budget;
                    PeekOrBakeCell(a, cw, side, labelH, gap, (GetCover != null) ? GetCover(a) : null, ref budget);
                    if (budget < previous)
                    {
                        long cost = Math.Max(1L, System.Diagnostics.Stopwatch.GetTimestamp() - begin);
                        aheadBakeTicks = Math.Max(cost, aheadBakeTicks - aheadBakeTicks / 4);
                    }
                    visited++;
                }
                if (cellCache.Count > capCeldas) PodaCeldas();
            }
            private SKPaint cPenGlow, cPenMid, cPenLine;   // contorno del disco seleccionado (cacheadas; antes se creaban por cuadro)   // EN LINUX: Pen: SKPaint con su color y su ancho
            private SKBitmap contourSprite; private Size contourSpriteSize = Size.Empty;   // contorno HORNEADO (3 trazos AA -> 1 copia); todas las casillas miden igual   // EN LINUX: Bitmap: un SKBitmap
            private Rocola.Nativo.Letra lblFont; private SKPaint lblBrush; private FormatoTexto lblSF;       // recursos de etiquetas (reusables)   // EN LINUX: Font, SolidBrush y StringFormat: Letra, SKPaint y FormatoTexto
            private SKBitmap blendBmp;   // para el fundido suave de portadas del cubo TOP   // EN LINUX: Bitmap: un SKBitmap
            public Starfield Sky;   // COMPARTIDO con el fondo del form: esta vista pinta su VENTANA del mismo campo (una sola pieza)
            public float PulseLo = 0f, PulseMid = 0f, PulseHi = 0f;   // pulso 0..1 por banda (graves/medios/agudos): cada estrella late con SU frecuencia

            private SKBitmap BlendInto(SKBitmap a, SKBitmap b, float f)   // EN LINUX: los puntos de las dos portadas y el mapa de la mezcla, SKBitmap
            {
                // 192 basta: el cubo se dibuja a ~154px; antes 320 gastaba el doble de relleno en cada fundido.
                if (blendBmp == null) blendBmp = Mapas.Nuevo(192, 192);   // EN LINUX: new Bitmap(192, 192, Format32bppPArgb) (Nativo/Mapas.cs)
                using (SKCanvas g = new SKCanvas(blendBmp))   // EN LINUX: Graphics.FromImage (lo demas del estado, de fabrica: sin copia ni medio pixel)
                {
                    g.Clear(Color.Black.Sk());   // EN LINUX: Graphics.Clear
                    Filtro filtro = Filtro.Bilineal;   // EN LINUX: g.InterpolationMode = Bilinear, en una variable
                    Rectangle dst = new Rectangle(0, 0, blendBmp.Width, blendBmp.Height);
                    if (a != null) Imagenes.Pinta(g, a, new SKRect(0, 0, a.Width, a.Height), new SKRect(dst.Left, dst.Top, dst.Right, dst.Bottom), filtro);   // EN LINUX: g.DrawImage(a, dst): la imagen entera en esa caja (Nativo/Imagenes.cs)
                    if (b != null && f > 0f)
                    {
                        float[][] cm = MatrizColor.Identidad(); cm[3][3] = f;   // EN LINUX: new ColorMatrix() con Matrix33 = f
                        // EN LINUX: using (ImageAttributes ia = new ImageAttributes()): la matriz de color va en la llamada (lo demas, de fabrica: la envoltura Clamp, transparente)
                        {
                            // EN LINUX: ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap): va en la llamada de abajo
                            Imagenes.Pinta(g, b, new SKRect(0, 0, 0 + b.Width, 0 + b.Height), new SKRect(dst.Left, dst.Top, dst.Right, dst.Bottom), filtro, false, false, Envoltura.Transparente, cm);   // EN LINUX: g.DrawImage(b, dst, 0, 0, b.Width, b.Height, Pixel, ia)
                        }
                    }
                }
                return blendBmp;
            }

            // Portadas a fundir del disco SELECCIONADO: para discos TOP cicla sus portadas (ca actual, cb
            // siguiente, f = mezcla 0..1); para discos normales devuelve la suya en ca (cb=null, f=0). La
            // usan OnPaint (CPU, que las funde a una imagen) y el modo GPU (que funde en dos pasadas).
            private void SelectedCovers(out SKBitmap ca, out SKBitmap cb, out float f)   // EN LINUX: Image: las portadas, SKBitmap de la cache (las da Form1 y van a la tarjeta tal cual)
            {
                ca = null; cb = null; f = 0f;
                if (Page == null || Selected < 0 || Selected >= Page.Count) return;
                Album selA = Page[Selected];
                if (selA != null && selA.CycleCovers != null && selA.CycleCovers.Count > 0 && GetCoverByPath != null)
                {
                    int m = selA.CycleCovers.Count;
                    double cyc = 3.8, hold = 2.6;
                    double total = cyc * m;
                    double pos = Phase - Math.Floor(Phase / total) * total;
                    int idx = (int)(pos / cyc) % m;
                    double within = pos - idx * cyc;
                    float ff = 0f;
                    if (within > hold) ff = (float)((within - hold) / (cyc - hold));
                    if (ff < 0f) ff = 0f; else if (ff > 1f) ff = 1f;
                    ca = GetCoverByPath(selA.CycleCovers[idx]);
                    cb = (m > 1) ? GetCoverByPath(selA.CycleCovers[(idx + 1) % m]) : null;
                    if (ca == null && GetCover != null) ca = GetCover(selA);   // mientras carga
                    f = ff;
                }
                else ca = (GetCover != null) ? GetCover(selA) : null;
            }

            // Caratula del disco SELECCIONADO ya FUNDIDA a una sola imagen (la usa el render por CPU).
            private SKBitmap SelectedCover()   // EN LINUX: los puntos de la portada (o de la mezcla) para pintarla: un SKBitmap
            {
                SKBitmap ca, cb; float f; SelectedCovers(out ca, out cb, out f);   // EN LINUX: Image: la portada de la cache de portadas, un SKBitmap
                return (cb == null || f <= 0.002f) ? ca : BlendInto(ca, cb, f);
            }

            private void EnsureLabelRes()
            {
                if (lblFont == null) lblFont = Rocola.Nativo.Letra.EnPuntos(this.Font.FontFamily.Name, 10f, true);   // EN LINUX: new Font(this.Font.FontFamily, 10, Bold): la familia de la letra del control (de WinForms, sin tocar), en puntos y en negrita (Nativo/Letras.cs)
                if (lblBrush == null) lblBrush = new SKPaint { Color = Color.White.Sk() };   // EN LINUX: new SolidBrush(Color.White)
                if (lblSF == null)
                {
                    lblSF = new FormatoTexto();   // EN LINUX: new StringFormat() (Nativo/TextoGdiPlus.cs)
                    lblSF.Horizontal = Alinea.Centro; lblSF.Vertical = Alinea.Centro;   // EN LINUX: Alignment y LineAlignment = Center
                    lblSF.Corte = CorteTexto.PuntosPorLetra;   // EN LINUX: Trimming = EllipsisCharacter
                    lblSF.Banderas = BanderasTexto.SoloEnteros;   // EN LINUX: FormatFlags = LineLimit (con =: quedan solo esas banderas)
                }
            }

            // Avanza el giro e invalida lo MINIMO: en cuadros intermedios solo la casilla del cubo que
            // gira (y la del relevo si lo hay); el cuadro completo (fondo+brillos) va 1 de cada 3.
            public void AnimTick(bool fullFrame)
            {
                Rectangle d = SpinRect();
                StepSpin();
                Rectangle d2 = SpinRect();
                if (fullFrame) { Invalidate(); return; }
                if (d2 != Rectangle.Empty) d = (d == Rectangle.Empty) ? d2 : Rectangle.Union(d, d2);
                if (d != Rectangle.Empty) Invalidate(d);
            }

            // QUE TROZO DE LA PARED CAMBIA AL MOVER LA SELECCION: la casilla que se deja y la que se elige,
            // CON SUS LETREROS.
            //
            // Es el hermano de SpinRect, y la diferencia importa: SpinRect es para el GIRO del cubo, que no
            // toca el letrero, asi que solo mete la portada. Al MOVERSE si cambia el letrero -el del disco
            // elegido se ve distinto-, y si se dejara fuera se quedaria el letrero viejo en pantalla.
            //
            // Antes, moverse repintaba la pared ENTERA -el cielo estrellado y las 18 casillas- cuando solo
            // cambian dos: 1.75 ms por pulsacion a 1280x1024, el 92% de la pantalla para nada, y con el freno
            // de flechas hasta 25 veces por segundo, justo mientras la maquina esta descomprimiendo el audio.
            public Rectangle RectSeleccion()
            {
                if (Page == null || Page.Count == 0) return Rectangle.Empty;
                int w = Width, h = Height; if (w < 8 || h < 8) return Rectangle.Empty;
                Rectangle r = UneCasilla(Rectangle.Empty, w, h, Selected);
                return UneCasilla(r, w, h, prevIdx);   // la que se acaba de dejar (sigue girando y apagandose)
            }

            private Rectangle UneCasilla(Rectangle r, int w, int h, int i)
            {
                if (i < 0 || i >= Page.Count) return r;
                Rectangle area, lbl;
                Geometry(w, h, i, out area, out lbl);
                area.Inflate(16, 16);                  // el contorno blanco brillante se sale de la portada
                Rectangle u = Rectangle.Union(area, lbl);
                return (r == Rectangle.Empty) ? u : Rectangle.Union(r, u);
            }

            // Rectangulo que abarca el cubo seleccionado (girando) y el cubo en relevo, con margen
            // suficiente para el contorno blanco brillante.
            private Rectangle SpinRect()
            {
                if (Page == null || Page.Count == 0) return Rectangle.Empty;
                int w = Width, h = Height; if (w < 8 || h < 8) return Rectangle.Empty;
                Rectangle r = Rectangle.Empty, area, lbl;
                if (Selected >= 0 && Selected < Page.Count)
                { Geometry(w, h, Selected, out area, out lbl); area.Inflate(16, 16); r = area; }
                if (prevIdx >= 0 && prevIdx < Page.Count)
                { Geometry(w, h, prevIdx, out area, out lbl); area.Inflate(16, 16); r = (r == Rectangle.Empty) ? area : Rectangle.Union(r, area); }
                return r;
            }

            // Relevo de giro: al cambiar de cubo, el ANTERIOR da un giro rapido que decae (se "pasa la energia"),
            // y el NUEVO arranca plano. Asi se ve cual se dejo y cual es el actual, aunque se mueva muy rapido.
            private int prevIdx = -1;        // cubo que se acaba de dejar (gira y se apaga)
            private double prevAngle = 0;    // su angulo actual
            private float prevSpin = 0f;     // su velocidad de giro (decae)
            private float spinDir = 1f;      // sentido del giro (segun la direccion del movimiento)

            // Mueve la seleccion DENTRO de la pagina con efecto de relevo (lo llama el movimiento izq/der).
            public void MoveSelectionTo(int newSel, int dir)
            {
                int cnt = (Page != null) ? Page.Count : 0;
                if (newSel != Selected && Selected >= 0 && Selected < cnt)
                {
                    prevIdx = Selected;            // el que dejamos
                    prevAngle = SelAngle;          // continua desde donde iba
                    prevSpin = 0.7f * (dir >= 0 ? 1f : -1f);   // golpe de giro en el sentido del movimiento
                }
                spinDir = (dir >= 0 ? 1f : -1f);
                Selected = newSel;
                SelAngle = 0;                      // el nuevo empieza plano (y arranca a girar)
            }

            // Cancela cualquier relevo en curso (al cambiar de pagina/letra, donde los cubos ya no son los mismos).
            public void CancelHandoff() { prevIdx = -1; prevSpin = 0f; }

            // Avanza el giro del cubo actual y el del cubo en relevo (lo llama el temporizador, 24fps).
            public void StepSpin()
            {
#if ROCOLA_GPU
                if (Sliding) return;   // pasando de pagina: no hay cubo a la vista, y asi el nuevo empieza plano
#endif
                SelAngle += 0.06 * spinDir;
                if (prevIdx >= 0)
                {
                    if (Math.Abs(prevSpin) > 0.02f) { prevAngle += prevSpin; prevSpin *= 0.85f; }
                    else
                    {
                        // ya casi parado: lo asentamos al multiplo de 2π mas cercano (queda plano, sin salto)
                        double target = Math.Round(prevAngle / (2 * Math.PI)) * (2 * Math.PI);
                        prevAngle += (target - prevAngle) * 0.25;
                        if (Math.Abs(target - prevAngle) < 0.03) prevIdx = -1;
                    }
                }
            }

            public AlbumView()
            {
                caja = new CajaVista(this); caja.RepintaConMedida = false;   // EN LINUX: this.SetStyle(AllPaintingInWmPaint | UserPaint | OptimizedDoubleBuffer, true): sin ventana propia no hay WM_ERASEBKGND ni doble bufer: la pinta su Pinta entera; SIN ResizeRedraw (no lo pone): al cambiar de medida no se pinta entera (Control.SetBoundsCore); Selectable, TabStop (esta en el ciclo del tabulador de XP: HelpBar, LetterBar, AlbumView, ProgressView, GpuPanel), StandardClick y StandardDoubleClick, los de fabrica de Control, que el hueco de la capa conserva; un Control mide 0x0 de fabrica, como la caja
            }

            private void OnFontChanged(EventArgs e)   // EN LINUX: protected override void OnFontChanged: lo llama el Font de esta vista (junto a Pinta) cuando cambia la que se ve, como Control.Font
            {
                caja.Invalidate();   // EN LINUX: base.OnFontChanged(e): Control.OnFontChanged la repinta (Invalidate) y avisa FontChanged (nadie lo escucha; no tiene hijas)
                SueltaDibujo(ref lblFont);
                if (cellCache != null) ResetCellCache();
                SueltaDibujo(ref cubeCache);
            }
            void IDisposable.Dispose() { Dispose(true); } private void Dispose(bool disposing)   // EN LINUX: protected override void Dispose(bool disposing) de Control: la llama su hueco al desecharse con la ventana (Control.Dispose desecha a los hijos) con disposing = true, como WinForms; sin finalizador no hay Dispose(false), que aqui no hacia nada
            {
                try
                {
                    if (disposing)
                    {
                        if (cellCache != null) ClearCellCache();
                        SueltaDibujo(ref cubeCache); SueltaDibujo(ref contourSprite); SueltaDibujo(ref wallLayer);
                        SueltaDibujo(ref blendBmp); SueltaDibujo(ref lblFont);
                        SueltaDibujo(ref lblBrush); lblSF = null;   // EN LINUX: SueltaDibujo(ref lblSF): un FormatoTexto no tiene nada que soltar; queda en null
                        SueltaDibujo(ref cPenGlow); SueltaDibujo(ref cPenMid); SueltaDibujo(ref cPenLine);
                    }
                }
                finally { }   // EN LINUX: base.Dispose(disposing) (sacarla de la ventana, su handle, el aviso Disposed): lo hace su hueco despues de llamar aqui, en el mismo orden
            }

            // Fuerza reconstruir la capa de cubos (cuando llega una portada en segundo plano)
            public void InvalidateCubeCache() { cacheSig = null; Invalidate(); }
            // Descarta las celdas horneadas (p.ej. tras re-escanear la biblioteca: mismo Folder pero etiqueta/portada
            // pueden haber cambiado). Fuerza rehornear en el proximo RebuildCubes.
            public void ResetCellCache() { ClearCellCache(); cellCacheW = cellCacheH = -1; cacheSig = null; Invalidate(); }

            public void Raton(AvisoRaton que, Raton e)   // EN LINUX: protected override void OnMouseClick(MouseEventArgs e): el raton de la vista (IVista.Raton), que el hueco de la capa o la pantalla le dan como WinForms al control; solo el clic (MouseClick)
            { if (que != AvisoRaton.Clic) return;   // EN LINUX: los demas avisos del raton, los de fabrica de Control (no hacen nada)
                int cw = Width / Cols, ch = Height / Rows;
                if (cw > 0 && ch > 0)
                {
                    int col = e.X / cw, row = e.Y / ch;
                    if (col >= 0 && col < Cols && row >= 0 && row < Rows)
                    {
                        int idx = row * Cols + col;
                        if (Page != null && idx < Page.Count) { Selected = idx; if (OnActivate != null) OnActivate(idx); }
                    }
                }
                // EN LINUX: base.OnMouseClick(e): el aviso MouseClick a los que escuchan lo da el hueco o la pantalla despues de Raton (PantallaRocola.AvisaOyentes), en el mismo orden
            }

            private string Sig()
            {
                if (Page == null || Page.Count == 0) return "0";
                return Page.Count.ToString() + "|" + (Page[0].Folder == null ? "" : Page[0].Folder) + "|" + (Page[Page.Count - 1].Folder == null ? "" : Page[Page.Count - 1].Folder);
            }

            private void Geometry(int w, int h, int i, out Rectangle cubeArea, out Rectangle labelRect)
            {
                int cw = w / Cols, ch = h / Rows;
                int col = i % Cols, row = i / Cols;
                Rectangle cell = new Rectangle(col * cw, row * ch, cw, ch);
                int labelH = 38, padX = 8, gap = 3, padY = 6;   // labelH = 2 lineas; etiqueta pegada al cubo
                int side = Math.Min(cell.Width - 2 * padX, cell.Height - labelH - gap - 2 * padY);
                if (side < 10) side = 10;
                int blockH = side + gap + labelH;
                int top = cell.Y + Math.Max(padY, (cell.Height - blockH) / 2);   // centra cubo+etiqueta en la celda
                cubeArea = new Rectangle(cell.X + (cell.Width - side) / 2, top, side, side);
                labelRect = new Rectangle(cell.X + 2, cubeArea.Bottom + gap, cell.Width - 4, labelH);
            }

            // Dimensiones de la celda (cuadro del disco) IGUAL que las calcula Geometry, para hornear/colocar.
            private static void CellMetrics(int cw, int ch, out int side, out int labelH, out int gap)
            {
                labelH = 38; gap = 3; int padX = 8, padY = 6;
                side = Math.Min(cw - 2 * padX, ch - labelH - gap - 2 * padY);
                if (side < 10) side = 10;
            }

            // Clave de cache de la celda: Folder (estable y unico) para discos normales. Para discos TOP ("Lo mas
            // sonado"), que reusan el mismo Folder sintetico pero cuya portada cambia al variar las reproducciones,
            // se le agrega la ruta de portada: asi se re-hornea cuando cambia (la etiqueta es fija). null = no cachea.
            // Se pide para CADA casilla en CADA cuadro (el panel la usa de clave de su cache de texturas). En los
            // discos normales es la carpeta tal cual, sin gasto; en los TOP habia que pegar carpeta+portada, y eso
            // era una cadena nueva por casilla y por cuadro: en la pagina de LO MAS SONADO, seis por cuadro a 60
            // cuadros por segundo. Ahora se arma UNA vez y se guarda en el propio disco.
            private static string CellKey(Album a)
            {
                if (a == null || a.Folder == null) return null;
                if (!a.IsTop) return a.Folder;
                string cp = a.CoverPath ?? "";
                if (a.KeyCache == null || a.KeyFrom != cp) { a.KeyFrom = cp; a.KeyCache = a.Folder + "|" + cp; }
                return a.KeyCache;
            }

            // LA CLAVE DE LA CASILLA, CON LA MARCA PUESTA. UN SOLO SITIO, y por eso existe.
            //
            // El palomito de "ya la pusiste" va HORNEADO DENTRO del mapa de bits de la casilla, asi que al
            // pedir una cancion hay que hornear una casilla NUEVA: por eso la clave lleva un "#q" pegado.
            // Eso estaba bien puesto en los dos sitios que hornean... y no en el tercero, el que le entrega
            // la rejilla a la tarjeta grafica, que ademas es el que manda: el panel cachea sus texturas por
            // esa clave. Resultado: la rocola horneaba la casilla con marca, se la daba al panel, y el panel
            // reusaba la textura vieja SIN marca. El adorno entero estaba invisible en las dos maquinas de
            // Windows, y tampoco desaparecia nunca al vaciarse la cola.
            //
            // Pegar el "#q" en un tercer sitio habria sido repetir el error. Ahora los tres piden aqui.
            //
            // Y NO CUESTA UNA CADENA NUEVA POR CUADRO: la variante con marca se arma UNA vez por disco y se
            // guarda en el propio disco, igual que la clave base. Esto se pide para cada casilla en cada
            // cuadro -dieciocho, sesenta veces por segundo-, y pegar cadenas ahi seria basura sin fin.
            private string ClaveCasilla(Album a)
            {
                string k = CellKey(a);
                if (k == null || EnCola == null || !EnCola(a)) return k;
                if (a.KeyQ == null || !object.ReferenceEquals(a.KeyQDe, k)) { a.KeyQDe = k; a.KeyQ = k + "#q"; }
                return a.KeyQ;
            }

            // Hornea la celda de un disco (portada estatica en angulo 0 + etiqueta a 2 lineas) a un bitmap propio.
            private SKBitmap BakeCell(Album a, int cw, int side, int labelH, int gap, SKBitmap cover)   // EN LINUX: el mapa y los puntos de la portada, SKBitmap
            { return BakeCell(a, cw, side, labelH, gap, cover, 1f); }

            // 'esc' encoge la casilla, para el hojeo (ver CeldaChica).
            //
            // SE ENCOGE EL DIBUJO, NO LAS MEDIDAS, y esto no es un capricho: la letra del rotulo tiene tamano
            // FIJO -no sale de 'labelH', ver EnsureLabelRes-, asi que pasando las medidas partidas por dos el
            // disco menguaria pero el nombre seguiria igual de grande: enorme para su hueco, y cortado.
            // Encogiendo el pincel sale la MISMA casilla, igual de proporcionada, solo que con menos pixeles.
            public Func<Album, bool> EnCola;   // ¿este disco tiene algo pedido? (lo contesta el form)

            private SKBitmap BakeCell(Album a, int cw, int side, int labelH, int gap, SKBitmap cover, float esc)   // EN LINUX: el mapa y los puntos de la portada, SKBitmap
            {
                int bw = cw, bh = side + gap + labelH;
                bool encoge = (esc > 0f && esc < 0.999f);
                if (encoge)
                {
                    bw = (int)(cw * esc); if (bw < 1) bw = 1;
                    bh = (int)((side + gap + labelH) * esc); if (bh < 1) bh = 1;
                }
                SKBitmap bmp = Mapas.Nuevo(bw, bh);   // EN LINUX: new Bitmap(bw, bh, Format32bppPArgb) (Nativo/Mapas.cs)
                using (SKCanvas g = new SKCanvas(bmp))   // EN LINUX: Graphics.FromImage (copia y medio pixel, de fabrica)
                {
                    g.Clear(Color.Transparent.Sk());   // EN LINUX: Graphics.Clear
                    if (encoge) g.Scale(esc, esc);   // EN LINUX: g.ScaleTransform
                    Filtro filtro = Filtro.Bilineal;   // mas rapido que bicubico (CPU vieja); las portadas ya son de buena resolucion   // EN LINUX: g.InterpolationMode = Bilinear, en una variable
                    bool suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias, en una variable
                    Rectangle rDisco = new Rectangle((cw - side) / 2, 0, side, side);
                    Cube.Draw(g, suave, false, filtro, false, rDisco, cover, 0.0);   // disco centrado (mismo x que Geometry)   // EN LINUX: el lienzo y su estado (copia y medio pixel, de fabrica)
                    if (EnCola != null && EnCola(a)) Palomito(g, suave, rDisco);   // EN LINUX: el lienzo y su suavizado
                    EnsureLabelRes();
                    TextoGdiPlus.Escribe(g, AlbumLabel(a), lblFont, AjusteLetra.DelSistema, lblBrush, new Rectangle(2, side + gap, cw - 4, labelH), lblSF);   // etiqueta pegada al disco   // EN LINUX: g.DrawString en esa caja con ese formato y el modo de letra de fabrica (SystemDefault) (Nativo/TextoGdiPlus.cs)
                }
                return bmp;
            }

            // EL PALOMITO DE "YA LA PUSISTE". Va en la esquina de arriba a la derecha del disco, que es la
            // unica de las cuatro que no pisa ni la etiqueta ni el recuadro de seleccion. Verde, el mismo
            // de la cola, para que se lea sin explicarlo: verde = tuya, ya va.
            private static void Palomito(SKCanvas g, bool suave, Rectangle disco)   // EN LINUX: el lienzo de Skia y el suavizado del que llama (los tres que lo llaman, hornos de casillas, lo traen puesto; la copia y el medio pixel, de fabrica en los tres)
            {
                float d = disco.Width * 0.26f; if (d < 13f) d = 13f;
                float x = disco.Right - d * 0.92f, y = disco.Top - d * 0.08f;
                using (SKPaint b = new SKPaint { Color = Color.FromArgb(236, 120, 230, 165).Sk() })   // EN LINUX: SolidBrush: el pincel de un color en un SKPaint
                    Pixel.RellenaOvalo(g, new RectangleF(x, y, d, d), b, suave);   // EN LINUX: g.FillEllipse(b, x, y, d, d) (Nativo/Pixel.cs)
                using (SKPaint aro = new SKPaint { Color = Color.FromArgb(150, 10, 40, 26).Sk(), StrokeWidth = Math.Max(1f, d * 0.07f) })   // EN LINUX: Pen(color, ancho): la pluma en un SKPaint
                    Pixel.TrazaOvalo(g, new RectangleF(x, y, d, d), aro, suave);   // EN LINUX: g.DrawEllipse(aro, x, y, d, d)
                using (SKPaint p = new SKPaint { Color = Color.FromArgb(255, 10, 44, 28).Sk(), StrokeWidth = Math.Max(1.6f, d * 0.15f) })   // EN LINUX: Pen(color, ancho)
                {
                    p.StrokeCap = SKStrokeCap.Round; p.StrokeJoin = SKStrokeJoin.Round;   // EN LINUX: StartCap y EndCap = Round (las dos puntas, iguales) y LineJoin = Round
                    Pixel.Lineas(g, new PointF[] {   // EN LINUX: g.DrawLines(p, puntos) (Nativo/Pixel.cs)
                        new PointF(x + d * 0.27f, y + d * 0.52f),
                        new PointF(x + d * 0.44f, y + d * 0.70f),
                        new PointF(x + d * 0.75f, y + d * 0.32f) }, p, suave);   // EN LINUX: la pluma y el suavizado van al final de la llamada
                }
            }

            // Devuelve el bitmap horneado de la celda del album; lo re-hornea si falta o si cambio el estado de
            // portada (placeholder -> cargada). Clave = CellKey(a) (ver arriba). Sin clave no se cachea.
            private SKBitmap GetOrBakeCell(Album a, int cw, int side, int labelH, int gap, SKBitmap cover)   // EN LINUX: el mapa de la casilla, un SKBitmap; la portada, el SKBitmap de la cache que da Form1
            {
                string key = ClaveCasilla(a);
                // LA MARCA ENTRA EN LA LLAVE, y por eso no hace falta avisar a nadie de nada: al pedir una
                // cancion, la llave de ese disco cambia, la casilla no esta en la caja y se hornea con su
                // palomito. Y la version sin marca se queda guardada para cuando la cancion ya sono.
                bool hasCover = (cover != null);
                CellSprite cs = null;
                if (key != null && cellCache.TryGetValue(key, out cs) && cs.Bmp != null && cs.HasCover == hasCover)
                { cs.Uso = ++cellUso; return cs.Bmp; }   // reutiliza (paginar = solo copiar)
                SKBitmap bmp = BakeCell(a, cw, side, labelH, gap, cover);   // EN LINUX: SKBitmap; la portada es el SKBitmap mismo
                if (key != null)
                {
                    if (cs != null && cs.Bmp != null && cs.Bmp != bmp) cs.Bmp.Dispose();   // reemplaza el placeholder por la version con portada
                    cellCache[key] = new CellSprite { Bmp = bmp, HasCover = hasCover, Uso = ++cellUso };
                }
                return bmp;
            }

            // Como GetOrBakeCell pero con PRESUPUESTO de horneado por cuadro (para la rejilla por GPU): si la celda
            // no esta cacheada y no queda presupuesto, devuelve la version vieja (placeholder) si la hay, o null (el
            // panel la salta y aparece en el siguiente cuadro). Asi un barrido de paginas NUEVAS no hornea 18 celdas
            // de golpe -> los fps no bajan. Comparte cellCache con GetOrBakeCell (mismas claves).
            // Cuando empezo el deslizamiento de pagina que hay en curso, o -1 si no hay ninguno. Sin tarjeta
            // grafica las paginas no se deslizan -no existe ese camino-, asi que ahi siempre es -1.
#if ROCOLA_GPU
            private int TickHojeo { get { return slideTick; } }
#else
            private int TickHojeo { get { return -1; } }
#endif

            private SKBitmap PeekOrBakeCell(Album a, int cw, int side, int labelH, int gap, SKBitmap cover, ref int budget)   // EN LINUX: el mapa de la casilla, un SKBitmap; la portada, el SKBitmap de la cache que da Form1
            { return PeekOrBakeCell(a, cw, side, labelH, gap, cover, ref budget, false); }

            // 'chica' = la pagina se esta deslizando, asi que la casilla va a media resolucion. Se guarda con
            // su propia clave para que la entera siga ahi: al parar hay que volver a ella.
            private SKBitmap PeekOrBakeCell(Album a, int cw, int side, int labelH, int gap, SKBitmap cover, ref int budget, bool chica)   // EN LINUX: el mapa de la casilla, un SKBitmap; la portada, el SKBitmap de la cache que da Form1
            {
                string key = ClaveCasilla(a);
                bool hasCover = (cover != null);
                string k = (key == null) ? null : (chica ? key + "#c" : key);
                CellSprite cs = null;
                if (k != null) cellCache.TryGetValue(k, out cs);
                // MIENTRAS LA PAGINA SE MUEVE NO SE REHACE UNA CASILLA PORQUE HAYA LLEGADO SU PORTADA.
                //
                // Normalmente, en cuanto la portada aparece, la casilla se vuelve a hornear Y a subir entera a
                // la tarjeta para cambiar el disco vacio por el de verdad. Pero pasando paginas rapido las
                // portadas no estan listas NUNCA, asi que el momento mas apretado era justo el que hacia el
                // doble de trabajo. Se aplaza a que la pagina pare: un cuarto de segundo despues, y sin nadie
                // mirando el detalle de una casilla que va volando.
                //
                // Solo se aplaza lo horneado EN ESTE deslizamiento, y para eso esta 'Nace': una casilla chica
                // que quedo de un hojeo de hace un rato SI se rehace, porque si no una pagina ya visitada se
                // veria con los discos vacios para siempre.
                bool recien = chica && HojeoLigero && cs != null && TickHojeo >= 0 && cs.Nace - TickHojeo >= 0;
                if (cs != null && cs.Bmp != null && (cs.HasCover == hasCover || recien))
                { cs.Uso = ++cellUso; return cs.Bmp; }   // cacheada -> gratis
                // sin presupuesto: usa la vieja (placeholder) si hay. Cuenta como uso: se esta viendo AHORA
                // MISMO, asi que la poda no puede llevarsela por delante.
                if (budget <= 0)
                {
                    if (cs != null && cs.Bmp != null) { cs.Uso = ++cellUso; return cs.Bmp; }
                    // Y ANTES QUE DEJAR UN HUECO, la de la OTRA resolucion: la tarjeta la estira y se ve un
                    // pelo blanda un cuadro, que es infinitamente mejor que un agujero en la pared.
                    CellSprite otra;
                    if (key != null && cellCache.TryGetValue(chica ? key : key + "#c", out otra) && otra.Bmp != null)
                    { otra.Uso = ++cellUso; return otra.Bmp; }
                    return null;
                }
                budget--;
                SKBitmap bmp = BakeCell(a, cw, side, labelH, gap, cover, chica ? CeldaChica : 1f);   // EN LINUX: SKBitmap; la portada es el SKBitmap mismo
                if (k != null)
                {
                    if (cs != null && cs.Bmp != null && cs.Bmp != bmp) cs.Bmp.Dispose();
                    cellCache[k] = new CellSprite { Bmp = bmp, HasCover = hasCover, Uso = ++cellUso, Nace = Environment.TickCount };
                }
                return bmp;
            }

            // AL LLENARSE EL CAJON SE TIRABAN TODAS LAS CASILLAS. El que estaba hojeando perdia de golpe hasta
            // las paginas que acababa de ver, y retroceder UNA sola pagina obligaba a re-escalar 18 caratulas y
            // re-maquetar 18 nombres: un tiron cada seis paginas. Ahora se tiran solo las MAS VIEJAS y se quedan
            // las ultimas vistas, que son justo a las que se vuelve.
            private void PodaCeldas()
            {
                int sobran = cellCache.Count - keepCeldas;
                if (sobran <= 0) return;
                // El corte: por debajo de ese valor de uso se tira. Son ~100 numeros y esto pasa una vez cada
                // varias paginas, asi que ordenarlos no cuesta nada al lado de hornear una sola casilla.
                int[] usos = new int[cellCache.Count];
                int k = 0;
                foreach (CellSprite cs in cellCache.Values) usos[k++] = cs.Uso;
                Array.Sort(usos);
                int corte = usos[sobran];
                List<string> fuera = new List<string>(sobran);
                foreach (KeyValuePair<string, CellSprite> kv in cellCache)
                    if (kv.Value.Uso < corte) fuera.Add(kv.Key);
                for (int i = 0; i < fuera.Count; i++)
                {
                    CellSprite cs = cellCache[fuera[i]];
                    if (cs.Bmp != null) cs.Bmp.Dispose();
                    cellCache.Remove(fuera[i]);
                }
            }

            private void ClearCellCache()
            {
#if ROCOLA_GPU
                ClearGpuCells();
#endif
                foreach (CellSprite cs in cellCache.Values) if (cs.Bmp != null) cs.Bmp.Dispose();
                cellCache.Clear();
                aheadBakeTicks = 0;
            }

            // Compone la rejilla en cubeCache (bitmap a pantalla completa) para el render por CPU. En modo GPU NO se
            // usa (el panel dibuja cada disco como su propio quad via BuildGpuGrid); OnPaint por CPU esta suprimido.
            private void RebuildCubes(int w, int h)
            {
                if (cubeCache == null || cubeCache.Width != w || cubeCache.Height != h)
                {
                    if (cubeCache != null) cubeCache.Dispose();
                    cubeCache = Mapas.Nuevo(w, h);   // EN LINUX: new Bitmap(w, h) (Nativo/Mapas.cs)
                }
                if (cellCacheW != w || cellCacheH != h) { ClearCellCache(); cellCacheW = w; cellCacheH = h; RecalculaCajon(w, h); }   // tamano nuevo: las celdas horneadas ya no encajan (y el cajon cambia de tope)
                else if (cellCache.Count > capCeldas) PodaCeldas();                                             // lleno: fuera las mas viejas (nunca la pagina que se ve)
                int budget = CpuBakeBudget;   // HORNEADO REPARTIDO (como en GPU): a lo mas CpuBakeBudget celdas nuevas por repintado; el
                cubesPending = false;         // resto queda para el proximo (OnPaint re-invalida). Evita el congelon de hornear 18 de golpe.
                using (SKCanvas g = new SKCanvas(cubeCache))   // EN LINUX: Graphics.FromImage (todo el estado, de fabrica)
                {
                    g.Clear(Color.Transparent.Sk());   // EN LINUX: Graphics.Clear
                    if (Page == null) return;
                    int cw = w / Cols, ch = h / Rows;
                    if (cw < 4 || ch < 4) return;
                    int side, labelH, gap; CellMetrics(cw, ch, out side, out labelH, out gap);
                    for (int i = 0; i < Page.Count && i < PageSize; i++)
                    {
                        // Todos los discos van estaticos (angulo 0). El seleccionado (y el de relevo) se dibujan
                        // girando ENCIMA en OnPaint; como girando nunca son mas angostos, lo tapan. Asi el cache
                        // NO depende de la seleccion -> moverse entre discos no lo reconstruye (fluido).
                        Rectangle area, lbl; Geometry(w, h, i, out area, out lbl);
                        SKBitmap cell = PeekOrBakeCell(Page[i], cw, side, labelH, gap, (GetCover != null) ? GetCover(Page[i]) : null, ref budget);   // EN LINUX: SKBitmap
                        if (cell != null) { int px = (i % Cols) * cw; Imagenes.Pinta(g, cell, new SKRect(0, 0, cell.Width, cell.Height), new SKRect(px, area.Y, px + cell.Width, area.Y + cell.Height), Filtro.Bilineal); }   // copia 1:1 (sin re-escalar portada ni re-maquetar texto)   // EN LINUX: g.DrawImageUnscaled(cell, x, y): la imagen entera a su tamano desde ese punto, con el filtro de fabrica (Nativo/Imagenes.cs)
                        else if (Page[i] != null && Page[i].Folder != null) cubesPending = true;   // solo celdas CACHEABLES se completan luego (convergen); las raras sin Folder no bloquean el bucle
                    }
                }
            }

            // Cuando llega UNA caratula, redibuja SOLO ese cubo en el cache (no los 18).
            // Asi pasar de pagina no dispara reconstrucciones completas repetidas (evita el tiron en XP).
            public void RefreshCover(string coverPath)
            {
                if (Page == null || coverPath == null) return;
                int w = Width, h = Height;
                if (cubeCache == null || cubeCache.Width != w || cubeCache.Height != h) { InvalidateCubeCache(); return; }
                int cw = w / Cols, ch = h / Rows;
                if (cw < 4 || ch < 4) return;
                int side, labelH, gap; CellMetrics(cw, ch, out side, out labelH, out gap);
                bool any = false;
                Rectangle dirtyR = Rectangle.Empty;
                using (SKCanvas g = new SKCanvas(cubeCache))   // EN LINUX: Graphics.FromImage (todo el estado, de fabrica)
                {
                    for (int i = 0; i < Page.Count && i < PageSize; i++)
                    {
                        if (Page[i] == null || Page[i].CoverPath != coverPath) continue;
                        int col = i % Cols, row = i / Cols;
                        Rectangle cell = new Rectangle(col * cw, row * ch, cw, ch);
                        Rectangle area, lbl; Geometry(w, h, i, out area, out lbl);
                        // rehornea la celda CON la portada (GetOrBakeCell detecta que HasCover cambio) y actualiza
                        // el cache: revisitar la pagina sigue siendo instantaneo. Luego la copia en cubeCache.
                        SKBitmap cell2 = GetOrBakeCell(Page[i], cw, side, labelH, gap, (GetCover != null) ? GetCover(Page[i]) : null);   // EN LINUX: SKBitmap
                        bool copia = true;   // deja la celda transparente antes de recomponer   // EN LINUX: g.CompositingMode = SourceCopy, en una variable
                        using (SKPaint tb = new SKPaint { Color = Color.Transparent.Sk() }) Pixel.Rellena(g, (RectangleF)cell, tb, false, false, copia);   // EN LINUX: SolidBrush y g.FillRectangle (sin suavizado ni medio pixel, de fabrica) (Nativo/Pixel.cs)
                        copia = false;   // EN LINUX: g.CompositingMode = SourceOver
                        Imagenes.Pinta(g, cell2, new SKRect(0, 0, cell2.Width, cell2.Height), new SKRect(col * cw, area.Y, col * cw + cell2.Width, area.Y + cell2.Height), Filtro.Bilineal, false, copia);   // EN LINUX: g.DrawImageUnscaled(cell2, x, y)
                        dirtyR = (dirtyR == Rectangle.Empty) ? cell : Rectangle.Union(dirtyR, cell);
                        any = true;
                    }
                }
                if (any && dirtyR != Rectangle.Empty) Invalidate(dirtyR);   // solo su celda: 18 caratulas ya NO son 18 repintados completos
                // En modo GPU (cubeCache es null) esta ruta no se ejecuta: la portada llega a la rejilla por GPU sola,
                // porque BuildGpuGrid consulta GetCover cada cuadro y re-hornea la celda cuando la portada aparece.
            }

            private const int ContourMargin = 8;   // margen del sprite del contorno (para el resplandor ancho de 11px)

            // Hornea el sprite del contorno de seleccion (3 trazos AA -> 1 copia). Es INDEPENDIENTE de cual celda
            // este seleccionada (todas miden igual): solo se re-hornea al cambiar de tamano. Lo llaman el OnPaint
            // (CPU) y el panel GPU (via ContourSpriteBmp), asi existe aunque el OnPaint por CPU este suprimido.
            private void EnsureContourSprite()
            {
                int w = Width, h = Height;
                if (w < 8 || h < 8) return;
                int cw = w / Cols, ch = h / Rows;
                if (cw < 4 || ch < 4) return;
                Rectangle area, lbl; Geometry(w, h, 0, out area, out lbl);
                Rectangle fr = area; fr.Inflate(4, 4);
                Size need = new Size(fr.Width + 2 * ContourMargin, fr.Height + 2 * ContourMargin);
                if (contourSprite != null && contourSpriteSize == need) return;
                if (cPenGlow == null)
                {
                    cPenGlow = new SKPaint { Color = Color.FromArgb(20, 255, 255, 255).Sk(), StrokeWidth = 11f };   // resplandor ancho y tenue   // EN LINUX: new Pen(color, 11): la pluma en un SKPaint (Pixel le pone las esquinas de GDI+)
                    cPenMid = new SKPaint { Color = Color.FromArgb(45, 255, 255, 255).Sk(), StrokeWidth = 6f };     // resplandor medio   // EN LINUX: new Pen(color, 6)
                    cPenLine = new SKPaint { Color = Color.FromArgb(235, 255, 255, 255).Sk(), StrokeWidth = 2.4f }; // linea nitida   // EN LINUX: new Pen(color, 2,4)
                }
                if (contourSprite != null) contourSprite.Dispose();
                contourSprite = Mapas.Nuevo(need.Width, need.Height);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb) (Nativo/Mapas.cs)
                using (SKCanvas cg = new SKCanvas(contourSprite))   // EN LINUX: Graphics.FromImage (copia y medio pixel, de fabrica)
                using (SKPath cp = RoundRect(new Rectangle(ContourMargin, ContourMargin, fr.Width, fr.Height), 10))   // EN LINUX: RoundRect (Form1.Ajustes.cs) da el SKPath (lo que era el GraphicsPath: par-impar y con la figura cerrada): se traza directo y se suelta como en XP
                {
                    bool suave = true;   // EN LINUX: cg.SmoothingMode = AntiAlias, en una variable
                    Pixel.Traza(cg, cp, cPenGlow, suave);   // EN LINUX: cg.DrawPath (Nativo/Pixel.cs) con ese SKPath, directo
                    Pixel.Traza(cg, cp, cPenMid, suave);   // EN LINUX: cg.DrawPath, directo
                    Pixel.Traza(cg, cp, cPenLine, suave);   // EN LINUX: cg.DrawPath, directo
                }
                contourSpriteSize = need;
            }

            public void Pinta(SKCanvas g) { Filtro f = Filtro.Bilineal; Pinta(g, false, false, ref f, false); } private readonly CajaVista caja; public Rectangle Bounds { get { return caja.Bounds; } set { caja.Bounds = value; } } public int Left { get { return caja.Left; } set { caja.Left = value; } } public int Top { get { return caja.Top; } set { caja.Top = value; } } public int Width { get { return caja.Width; } set { caja.Width = value; } } public int Height { get { return caja.Height; } set { caja.Height = value; } } public void SetBounds(int x, int y, int width, int height) { caja.SetBounds(x, y, width, height); } public bool Visible { get { return caja.Visible; } set { caja.Visible = value; } } public void Invalidate() { caja.Invalidate(); } public void Invalidate(Rectangle rc) { caja.Invalidate(); } private Rocola.Dibujo2D.Font font; public Rocola.Dibujo2D.Font Font { get { return font ?? Control.DefaultFont; } set { Rocola.Dibujo2D.Font antes = Font; if (value == null ? font != null : (font == null || !Control.IgualLetra(value, font))) font = value; if (value == null || !Control.IgualLetra(antes, value)) OnFontChanged(EventArgs.Empty); } } internal void Pinta(SKCanvas g, bool suave, bool copia, ref Filtro filtroDe, bool medio)   // EN LINUX: protected override void OnPaint(PaintEventArgs e): Pinta(SKCanvas) es la de la pantalla (IVista.Pinta): el lienzo y el estado del Graphics de un WM_PAINT (el de fabrica: sin suavizado, sin copia, filtro Bilinear, sin medio pixel); la de cinco argumentos es el OnPaint de XP con el estado del Graphics en argumentos (el filtro con ref: el que DrawWall deja puesto vuelve a quien llama, como quedaba en el Graphics de XP; en un WM_PAINT se iba con el). En la misma linea, lo que XP usa de este control por fuera y le daba Control: Bounds, Left, Top, Width, Height, SetBounds, Visible e Invalidate (con su CajaVista), Invalidate(Rectangle) (el giro de la casilla y el horneado repartido: como en la capa, la vista se pinta entera; el rectangulo solo acotaba la region del WM_PAINT en Windows) y la Font: la de la capa (Rocola.Dibujo2D.Font) hasta la oleada 7, con lo de Control.Font: sin la suya, la del dueno (propiedad ambiental), que es la de la ventana, que XP nunca cambia: Control.DefaultFont, el mismo objeto; al ponerla, la propia solo cambia si es otra letra (una igual deja el objeto de antes) y avisa si la que se ve cambia (resolved.Equals(value)); nadie se la pone (EnsureLabelRes lee su familia)
            {
                // EN LINUX: Graphics g = e.Graphics: el lienzo de Skia (con su matriz y su recorte) llega como g y el estado del Graphics como argumentos (suave, copia, filtroDe, medio)
                int w = Width, h = Height;
                if (w < 8 || h < 8) { g.Clear(Shade(BgAccent, 0.82).Sk()); return; }   // EN LINUX: Graphics.Clear
                // Cuando el fondo GPU (pantalla completa, opaco) esta encima, TODO esto queda oculto. Reconstruir
                // la rejilla + pintar estrellas + cubo por CPU en cada Invalidate (p.ej. al paginar, que llama
                // Invalidate por cambio de pagina) era trabajo invisible que ademas SALTABA la franja de la GPU
                // -> tirones al hojear. El panel GPU dibuja la rejilla por su cuenta (BuildGpuGrid, un quad/celda).
#if ROCOLA_GPU
                if (GpuCovered) return;
#endif

                bool sizeChanged = (cacheW != w || cacheH != h);
                string sig = Sig();
                if (sizeChanged || cubeCache == null || sig != cacheSig || cubesPending) RebuildCubes(w, h);   // ya NO se reconstruye por cambio de seleccion; cubesPending completa el horneado repartido
                cacheW = w; cacheH = h; cacheSig = sig;

                if (Sky != null)
                {
                    Sky.SkyOffsetX = this.Left; Sky.SkyOffsetY = this.Top;   // VENTANA del campo COMPARTIDO de pantalla completa (una sola pieza con las franjas)
                    Size cliente; bool conDueno = PantallaRocola.ClienteDelDueno(this, out cliente); Sky.SkyFullW = conDueno ? cliente.Width : w;   // EN LINUX: (this.Parent != null) ? this.Parent.ClientSize.Width : w: el cliente de su dueno (la ventana) lo da la pantalla; sin dueno, false
                    Sky.SkyFullH = conDueno ? cliente.Height : h;   // EN LINUX: (this.Parent != null) ? this.Parent.ClientSize.Height : h
                    Sky.SkyMid = SkyColorMid(BgAccent); Sky.Acento = BgAccent;
                    Sky.Dim = BgDim;   // atenuado mientras se navega con musica (vuelve solo tras 10s sin botones)
                    Sky.DrawReactive(g, suave, filtroDe, medio, copia, w, h, SkyColor(BgAccent), SkyColor2(BgAccent), PulseLo, PulseMid, PulseHi);   // fondo titilante + LATIDO por banda (grandes=graves, medianas=medios/agudos)   // EN LINUX: Starfield.DrawReactive traducida (Form1.Fondo.cs), directo, con el lienzo y el estado del Graphics (no se lo cambia); ya no pasa por la firma de XP del puente (Plataforma/Puente.fondo.cs)
                }
                if (WallOpacity < 0.999f)   // EN LINUX: el estado que trae el Graphics (suavizado, copia, filtro y medio pixel) llega en argumentos y el cielo no lo cambia: con el se pinta la pared
                {
                    if (wallLayer == null || wallLayer.Width != w || wallLayer.Height != h)
                    {
                        SueltaDibujo(ref wallLayer);
                        wallLayer = Mapas.Nuevo(w, h);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb) (Nativo/Mapas.cs)
                    }
                    using (SKCanvas layer = new SKCanvas(wallLayer))   // EN LINUX: Graphics.FromImage (todo el estado, de fabrica)
                    {
                        layer.Clear(Color.Transparent.Sk());   // EN LINUX: Graphics.Clear
                        Filtro deFabrica = Filtro.Bilineal; DrawWall(layer, false, false, ref deFabrica, false, w, h);   // EN LINUX: el lienzo de la capa y el estado de fabrica de un Graphics nuevo (sin suavizado, sin copia, Bilinear, sin medio pixel)
                    }
                    // EN LINUX: using (ImageAttributes ia = new ImageAttributes()): la matriz de color va en la llamada (lo demas, de fabrica: la envoltura Clamp, transparente)
                    {
                        float[][] cm = MatrizColor.Identidad();   // EN LINUX: new ColorMatrix() (Nativo/Imagenes.cs)
                        cm[3][3] = Math.Max(0f, Math.Min(1f, WallOpacity));   // EN LINUX: cm.Matrix33
                        // EN LINUX: ia.SetColorMatrix(cm): va en la llamada de abajo
                        Imagenes.Pinta(g, wallLayer, new SKRect(0, 0, 0 + w, 0 + h), new SKRect(0, 0, w, h), filtroDe, medio, copia, Envoltura.Transparente, cm);   // EN LINUX: g.DrawImage(wallLayer, caja, 0, 0, w, h, Pixel, ia): con el filtro, el medio pixel y la copia del Graphics
                    }
                }
                else
                {
                    SueltaDibujo(ref wallLayer);
                    Filtro filtro = filtroDe; DrawWall(g, suave, copia, ref filtro, medio, w, h); filtroDe = filtro;   // EN LINUX: el lienzo y su estado; el filtro que DrawWall deja puesto (Bilinear, si pinta el cubo) vuelve a quien llama (filtroDe), como quedaba en el Graphics de XP
                }
                if (cubesPending) Invalidate();
            }
            private void DrawWall(SKCanvas g, bool suave, bool copia, ref Filtro filtro, bool medio, int w, int h)   // EN LINUX: el lienzo de Skia y el estado del que llama; el filtro va con ref porque aqui se cambia (g.InterpolationMode = Bilinear) y en XP quedaba cambiado en el Graphics del que llama
            {
                Imagenes.Pinta(g, cubeCache, new SKRect(0, 0, cubeCache.Width, cubeCache.Height), new SKRect(0, 0, 0 + cubeCache.Width, 0 + cubeCache.Height), filtro, medio, copia);   // EN LINUX: g.DrawImageUnscaled(cubeCache, 0, 0): la imagen entera a su tamano (Nativo/Imagenes.cs)

                if (Page != null && Selected >= 0 && Selected < Page.Count)
                {
                    int cw = w / Cols, ch = h / Rows;
                    if (cw >= 4 && ch >= 4)
                    {
                        Rectangle area, lbl;
                        Geometry(w, h, Selected, out area, out lbl);
                        filtro = Filtro.Bilineal;   // EN LINUX: g.InterpolationMode = Bilinear
                        // Contorno blanco con resplandor del disco SELECCIONADO (ademas del giro), para ubicarlo facil.
                        Rectangle fr = area; fr.Inflate(4, 4);
                        EnsureContourSprite();   // sprite horneado (3 trazos AA -> 1 copia); solo se re-hornea al cambiar tamano
                        if (contourSprite != null) { int kx = fr.X - ContourMargin, ky = fr.Y - ContourMargin; Imagenes.Pinta(g, contourSprite, new SKRect(0, 0, contourSprite.Width, contourSprite.Height), new SKRect(kx, ky, kx + contourSprite.Width, ky + contourSprite.Height), filtro, medio, copia); }   // EN LINUX: g.DrawImageUnscaled(contourSprite, x, y)
                        Cube.Draw(g, suave, copia, filtro, medio, area, SelectedCover(), SelAngle);   // EN LINUX: el lienzo y su estado
                    }
                }

                // Cubo de RELEVO: el que acabamos de dejar sigue girando (rapido y decayendo) sobre su
                // posicion. Como el cubo girando nunca es mas angosto que el del cache, lo tapa sin fantasma.
                if (prevIdx >= 0 && prevIdx != Selected && Page != null && prevIdx < Page.Count)
                {
                    int cw = w / Cols, ch = h / Rows;
                    if (cw >= 4 && ch >= 4)
                    {
                        Rectangle area2, lbl2;
                        Geometry(w, h, prevIdx, out area2, out lbl2);
                        filtro = Filtro.Bilineal;   // EN LINUX: g.InterpolationMode = Bilinear
                        Cube.Draw(g, suave, copia, filtro, medio, area2, (GetCover != null) ? GetCover(Page[prevIdx]) : null, prevAngle);   // EN LINUX: el lienzo y su estado, y la portada (el SKBitmap de la cache que da Form1)
                    }
                }
                if (cubesPending) Invalidate();   // quedan celdas por hornear: pide otro repintado para completarlas (repartido, sin congelar)
            }
        }

        // ==========  CUBO DE LA CANCION (gira + se inclina)  ==========
        private partial class CubeBox : IVista   // EN LINUX: : Control -> sin herencia (port nativo, vistas-pared: W3b): una vista de la pantalla nativa (Nativo/IVista.cs) con su CajaVista; en la ventana de la capa la pone su hueco (Plataforma/Anfitrion.cs), que la pinta con Pinta y le da el raton como WinForms al control (Raton); partial como en XP (FluidezGpu.cs); no es IDisposable: XP no le sobrescribe Dispose
        {
            public SKBitmap Cover;   // EN LINUX: Image: la portada del cubo, un SKBitmap de la cache: la pone Form1.Biblioteca.cs y la lee Form1.Video.cs para la tarjeta
            public double Angle;
            public double Phase;

            // ---------- EL RELEVO ----------
            // Al entrar la siguiente cancion, la portada del cubito de abajo se cambiaba DE GOLPE: un
            // cuadro con una y el siguiente con otra. A dos metros, de noche y con los amigos hablando,
            // eso no es un cambio: es que de pronto hay otra cosa ahi y nadie sabe desde cuando.
            //
            // Ahora el cubo da MEDIA VUELTA y la portada nueva aparece en el camino. Y el momento del
            // cambio no es uno cualquiera: a la mitad el cubo lleva 90 grados girados, y como las cuatro
            // caras llevan la misma imagen, 90 grados dejan el cubo con EXACTAMENTE la misma silueta que
            // al empezar. Asi el cambio no puede dar un salto de forma -solo cambia la imagen-, y cambia
            // justo en el punto mas rapido del giro, que es cuando menos se le mira la cara.
            //
            // Y con tarjeta grafica sale gratis: el panel ya lee Angle y Cover en cada cuadro, asi que
            // no hay ni una linea de dibujo nueva.
            private const int RelMs = 420;
            private int relTick;          // 0 = no hay relevo en marcha
            private SKBitmap relNueva;       // la portada que entra   // EN LINUX: Image: la portada de la cache de portadas, un SKBitmap
            private double relGirado;     // cuanto lleva puesto de la media vuelta

            public void Relevo(SKBitmap nueva)   // EN LINUX: Image: la portada de la cache de portadas, un SKBitmap
            {
                if (relTick != 0 && object.ReferenceEquals(relNueva, nueva)) return;   // esa ya viene en camino
                relNueva = nueva;
                relGirado = 0;
                relTick = Environment.TickCount; if (relTick == 0) relTick = 1;
            }
            public bool Girando { get { return relTick != 0; } }
            public bool UsaPortada(SKBitmap im)   // EN LINUX: Image: la portada de la cache de portadas, un SKBitmap
            {
                return object.ReferenceEquals(Cover, im) || object.ReferenceEquals(relNueva, im);
            }

            // Avanza el giro. Sin relevo, el paso de siempre. Con relevo, ademas, la media vuelta.
            // El giro lo lleva el cubo y no el reloj de la pantalla: si no, habria que repetir la cuenta
            // de la media vuelta en los dos sitios desde donde se le da cuerda.
            public void Avanza(double paso)
            {
                Angle += paso;
                if (relTick == 0) return;
                int dt = Environment.TickCount - relTick;
                if (dt < 0) dt = 0;
                double u = (dt >= RelMs) ? 1.0 : (dt / (double)RelMs);
                double e = u * u * (3.0 - 2.0 * u);        // arranca y frena suave; a la mitad es donde mas corre
                double meta = Math.PI * e;
                Angle += meta - relGirado;                 // lo que falte para ir por donde toca
                relGirado = meta;
                if (relNueva != null && e >= 0.5) { Cover = relNueva; relNueva = null; }
                if (u >= 1.0) relTick = 0;
            }

            public CubeBox()
            {
                caja = new CajaVista(this); caja.RepintaConMedida = false;   // EN LINUX: this.SetStyle(AllPaintingInWmPaint | UserPaint | OptimizedDoubleBuffer | SupportsTransparentBackColor, true): sin ventana propia no hay WM_ERASEBKGND, doble bufer ni comprobacion del fondo transparente (BackColor admite alfa): la pinta su Pinta entera; SIN ResizeRedraw (no lo pone): al cambiar de medida no se pinta entera; Selectable, TabStop, StandardClick y StandardDoubleClick, los de fabrica de Control, que el hueco de la capa conserva; un Control mide 0x0 de fabrica, como la caja
                this.BackColor = Color.FromArgb(10, 15, 44);
            }

            public void Pinta(SKCanvas g) { Pinta(g, false, false); } private readonly CajaVista caja; public Rectangle Bounds { get { return caja.Bounds; } set { caja.Bounds = value; } } public int Left { get { return caja.Left; } set { caja.Left = value; } } public int Top { get { return caja.Top; } set { caja.Top = value; } } public int Width { get { return caja.Width; } set { caja.Width = value; } } public int Height { get { return caja.Height; } set { caja.Height = value; } } public void SetBounds(int x, int y, int width, int height) { caja.SetBounds(x, y, width, height); } public bool Visible { get { return caja.Visible; } set { caja.Visible = value; } } public void Invalidate() { caja.Invalidate(); } private Color backColor = CajaPieza.ColorDeFondo; public Color BackColor { get { return backColor; } set { if (backColor.Equals(value)) return; backColor = value; caja.Invalidate(); } } internal void Pinta(SKCanvas g, bool copia, bool medio)   // EN LINUX: protected override void OnPaint(PaintEventArgs e): Pinta(SKCanvas) es la de la pantalla (IVista.Pinta): el lienzo y el estado del Graphics de un WM_PAINT (el de fabrica: sin copia ni medio pixel); la de tres argumentos es el OnPaint de XP con lo que lee del estado del Graphics que llega (la copia y el medio pixel; el suavizado y el filtro los pone el mismo); lo que XP le dejaba al Graphics (Bilinear y AntiAlias) se iba con el. Su OnPaintBackground de fabrica no pintaba nada visible: Form1 lo pone transparente (Form1.cs, Form1.Video.cs) y el fondo de un control transparente es el de su dueno, que la capa y la pantalla pintan debajo; opaco (el de su constructor) lo tapa el Clear de abajo. En la misma linea, lo que XP usa de este control por fuera y le daba Control: Bounds, Left, Top, Width, Height, SetBounds, Visible e Invalidate (con su CajaVista) y el BackColor: un campo propio que repinta al cambiar (Control.OnBackColorChanged); con SupportsTransparentBackColor admite alfa; sin poner seria el de su dueno (ambiental): el constructor lo pone siempre
            {
                // EN LINUX: Graphics g = e.Graphics: el lienzo de Skia (con su matriz y su recorte) llega como g y lo que se lee del estado del Graphics como argumentos (copia, medio)
                if (this.BackColor.A == 255) g.Clear(this.BackColor.Sk());   // EN LINUX: Graphics.Clear
                int w = Width, h = Height;
                if (w < 12 || h < 12) return;
                Filtro filtro = Filtro.Bilineal;   // EN LINUX: g.InterpolationMode = Bilinear, en una variable
                bool suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias, en una variable; la copia y el medio pixel, los que traiga el Graphics (en argumentos)

                int side = (int)(Math.Min(w, h) * 0.78);
                Rectangle area = new Rectangle((w - side) / 2, (h - side) / 2, side, side);

                float cx = w / 2f, cy = h / 2f;
                float tilt = (float)(10.0 * Math.Sin(Phase * 0.9));
                int st = g.Save();   // EN LINUX: GraphicsState st = g.Save(): la matriz y el recorte los guarda el lienzo (el estado del dibujo va en variables y aqui dentro no cambia)
                g.Translate(cx, cy);   // EN LINUX: g.TranslateTransform
                g.RotateDegrees(tilt);   // EN LINUX: g.RotateTransform (en grados)
                g.Translate(-cx, -cy);   // EN LINUX: g.TranslateTransform
                Cube.Draw(g, suave, copia, filtro, medio, area, Cover, Angle);   // EN LINUX: el lienzo y su estado, y la portada (el SKBitmap mismo)
                g.RestoreToCount(st);   // EN LINUX: g.Restore(st)
            }
        }
    }
}
