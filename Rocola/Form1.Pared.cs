// LA PARED DE DISCOS: la rejilla, los cubos y la pantalla de atraccion
//
// Parte de Form1. La clase esta repartida en varios archivos por temas -son todos "partial class Form1"-
// para poder venir a tocar UNA cosa sin leerse el programa entero. Aqui no hay nada nuevo: es el mismo
// codigo que estaba en Form1.cs, movido tal cual.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rocola
{
    public partial class Form1 : Form
    {

        // Modo atraccion: pantalla negra con estrellas titilantes y un cubo gigante girando que cicla
        // portadas de la coleccion (con fundido). Aparece en reposo para llamar al cliente.
        private class AttractView : Control
        {
            public double Phase = 0;
            public double Angle = 0;
            public List<string> CycleCovers = new List<string>();
            public Func<string, Image> GetCoverByPath;
            public int SongCount { get; set; }   // propiedad (no campo): un campo de una clase por-referencia (Control) dispara la advertencia CS1690 al usarlo

            private SolidBrush sb;
            private Bitmap blendBmp;
            private Font fBig, fSmall; private StringFormat sfCen;

            private Starfield attractStars = new Starfield();

            public AttractView()
            {
                attractStars.Varied = true;   // estrellas multicolor en el modo atraccion
                // El cubo gigante gira a 24 por segundo, pero las estrellas no tienen por que: titilan al
                // mismo ritmo que en el resto del programa. Rehacer el cielo entero 24 veces por segundo era
                // el grueso del gasto de esta pantalla, y es la que mas horas pasa encendida.
                attractStars.RefrescoMs = 200;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }

            private void EnsureRes()
            {
                if (sb == null) sb = new SolidBrush(Color.White);
                if (fBig == null) fBig = new Font(FontFamily.GenericSansSerif, 24f, FontStyle.Bold);
                if (fSmall == null) fSmall = new Font(FontFamily.GenericSansSerif, 15f, FontStyle.Bold);
                if (sfCen == null) { sfCen = new StringFormat(); sfCen.Alignment = StringAlignment.Center; sfCen.LineAlignment = StringAlignment.Center; }
            }

            private Image BlendInto(Image a, Image b, float f)
            {
                if (blendBmp == null) blendBmp = new Bitmap(320, 320, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(blendBmp))
                {
                    g.Clear(Color.Black);
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    Rectangle dst = new Rectangle(0, 0, blendBmp.Width, blendBmp.Height);
                    if (a != null) g.DrawImage(a, dst);
                    if (b != null && f > 0f)
                    {
                        ColorMatrix cm = new ColorMatrix(); cm.Matrix33 = f;
                        using (ImageAttributes ia = new ImageAttributes())
                        {
                            ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                            g.DrawImage(b, dst, 0, 0, b.Width, b.Height, GraphicsUnit.Pixel, ia);
                        }
                    }
                }
                return blendBmp;
            }

            // Par de portadas a fundir + factor (0..1) segun la fase. Lo usa el CPU (BlendInto) y tambien la
            // GPU (que hace el fundido como en los discos TOP, sin generar el bitmap mezclado).
            public bool GetCoverPair(out Image a, out Image b, out float f)
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

            private Image CurrentCover()
            {
                Image a, b; float f;
                if (!GetCoverPair(out a, out b, out f)) return null;
                if (f <= 0.002f || b == null) return a;
                if (a == null) return b;
                return BlendInto(a, b, f);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                int w = Width, h = Height;
                if (w < 8 || h < 8) return;
                EnsureRes();
                attractStars.SkyOffsetY = 0; attractStars.SkyFullH = h;
                attractStars.Draw(g, w, h, Color.FromArgb(255, 6, 8, 26), Color.FromArgb(255, 18, 20, 54), Color.White, Phase, true, 0f);   // cielo nocturno en degradado (atraccion: sin musica)

                // cubo gigante central rotando + ciclando portadas (con fundido)
                int side = (int)(Math.Min(w, h) * 0.40);
                if (side < 80) side = 80;
                Rectangle area = new Rectangle(w / 2 - side / 2, (h - side) / 2 - 30, side, side);
                g.InterpolationMode = InterpolationMode.Bilinear;
                Cube.Draw(g, area, CurrentCover(), Angle);

                // textos
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                string line1 = (SongCount > 0) ? (SongCount.ToString("#,0") + " canciones") : "Tu rocola";
                sb.Color = Color.White;
                g.DrawString(line1, fBig, sb, new RectangleF(0, area.Bottom + 18, w, 36), sfCen);
                double pz = 0.5 + 0.5 * Math.Sin(Phase * 3.0);
                sb.Color = Color.FromArgb((int)(150 + 105 * pz), 255, 220, 120);
                g.DrawString("toca un boton para elegir", fSmall, sb, new RectangleF(0, area.Bottom + 58, w, 28), sfCen);
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
            [ThreadStatic] private static SolidBrush faceBrush;   // pincel reusable (cara/oscurecido): antes asignaba 1-2 por cara y cuadro
            [ThreadStatic] private static Pen edgePen;            // lapiz de contorno reusable (antes 1 por cuadro)

            public static void Draw(Graphics g, Rectangle bounds, Image cover, double angle)
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
                    SolidBrush fb = faceBrush ?? (faceBrush = new SolidBrush(Color.Black));
                    if (cover != null) g.DrawImage(cover, rect);
                    else { fb.Color = Color.FromArgb(60, 70, 110); g.FillRectangle(fb, rect); }
                    int darken = (int)((1.0 - depth[k]) * 130);
                    if (darken > 0) { fb.Color = Color.FromArgb(darken, 0, 0, 0); g.FillRectangle(fb, rect); }
                }

                double minx = ex[0], maxx = ex[0];
                for (int k = 1; k < 4; k++) { if (ex[k] < minx) minx = ex[k]; if (ex[k] > maxx) maxx = ex[k]; }
                Pen ep = edgePen ?? (edgePen = new Pen(Color.FromArgb(120, 0, 0, 0), 1));
                g.DrawRectangle(ep, (int)Math.Round(minx), y0, (int)Math.Round(maxx - minx), H - 1);
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
        private static void GlowAround(Graphics g, float leftX, float cy, float symW, float level, Color c)
        {
            if (level <= 0.02f) return;
            float cx = leftX + symW / 2f, r = symW * 0.9f + 6f;
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(cx - r, cy - r, 2 * r, 2 * r);
                using (PathGradientBrush pb = new PathGradientBrush(gp))
                {
                    pb.CenterPoint = new PointF(cx, cy);
                    pb.CenterColor = Color.FromArgb((int)(level * 215), c);
                    pb.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                    g.FillPath(pb, gp);
                }
            }
        }

        // Ayudantes para dibujar los controles fisicos como FORMAS (circulo rojo/verde, cruceta).
        // GDI reusables para las formas de instrucciones (se llaman muchas veces por cuadro): pincel al que se
        // le muta el color, contorno blanco fijo y un buffer de 3 puntos. Evita asignar objetos GDI por forma.
        [ThreadStatic] private static SolidBrush ctrlB;
        [ThreadStatic] private static Pen ctrlOutline;
        [ThreadStatic] private static PointF[] ctrlTri3;
        private static SolidBrush CB(Color c) { if (ctrlB == null) ctrlB = new SolidBrush(c); else ctrlB.Color = c; return ctrlB; }
        private static Pen COut() { return ctrlOutline ?? (ctrlOutline = new Pen(Color.FromArgb(130, 255, 255, 255), 1.4f)); }

        private static float CtrlDot(Graphics g, float x, int cy, int d, Color c)
        {
            g.FillEllipse(CB(c), x, cy - d / 2f, d, d);
            g.DrawEllipse(COut(), x, cy - d / 2f, d, d);
            return x + d;
        }
        private static float CtrlTxt(Graphics g, Font f, string s, float x, int cy, Color c)
        {
            SizeF sz = g.MeasureString(s, f);
            g.DrawString(s, f, CB(c), x, cy - sz.Height / 2f);
            return x + sz.Width;
        }
        private static void CtrlTri(Graphics g, Color c, PointF a, PointF b, PointF d)
        {
            PointF[] p = ctrlTri3 ?? (ctrlTri3 = new PointF[3]);
            p[0] = a; p[1] = b; p[2] = d;
            g.FillPolygon(CB(c), p);
        }
        private static float CtrlPad(Graphics g, float x, int cy, int sz)   // cruceta: arriba/abajo rojo, izq/der amarillo
        {
            Color red = Color.FromArgb(228, 72, 72), yellow = Color.FromArgb(242, 206, 72);
            float cx = x + sz / 2f, t = sz * 0.34f, w = t / 1.4f;
            CtrlTri(g, red, new PointF(cx, cy - sz / 2f), new PointF(cx - w, cy - sz / 2f + t), new PointF(cx + w, cy - sz / 2f + t));
            CtrlTri(g, red, new PointF(cx, cy + sz / 2f), new PointF(cx - w, cy + sz / 2f - t), new PointF(cx + w, cy + sz / 2f - t));
            CtrlTri(g, yellow, new PointF(x, cy), new PointF(x + t, cy - w), new PointF(x + t, cy + w));
            CtrlTri(g, yellow, new PointF(x + sz, cy), new PointF(x + sz - t, cy - w), new PointF(x + sz - t, cy + w));
            return x + sz;
        }
        // Icono del BOTON IZQUIERDA (flecha amarilla, mismo color que la cruceta horizontal)
        private static float CtrlDirLeft(Graphics g, float x, int cy, int sz, Color c)
        {
            PointF[] pts = ctrlTri3 ?? (ctrlTri3 = new PointF[3]);
            pts[0] = new PointF(x, cy); pts[1] = new PointF(x + sz, cy - sz * 0.5f); pts[2] = new PointF(x + sz, cy + sz * 0.5f);
            g.FillPolygon(CB(c), pts); g.DrawPolygon(COut(), pts);
            return x + sz;
        }
        // Icono del BOTON DERECHA (flecha amarilla, espejo de la izquierda)
        private static float CtrlDirRight(Graphics g, float x, int cy, int sz, Color c)
        {
            PointF[] pts = ctrlTri3 ?? (ctrlTri3 = new PointF[3]);
            pts[0] = new PointF(x + sz, cy); pts[1] = new PointF(x, cy - sz * 0.5f); pts[2] = new PointF(x, cy + sz * 0.5f);
            g.FillPolygon(CB(c), pts); g.DrawPolygon(COut(), pts);
            return x + sz;
        }

        // Solo recursos PROPIOS de las vistas, nunca Font/Cover/Sky prestados.
        private static void SueltaDibujo<T>(ref T resource) where T : class, IDisposable
        {
            T old = resource; resource = null;
            if (old != null) old.Dispose();
        }
        private static void SueltaBitmaps(ref Bitmap[] images)
        {
            Bitmap[] old = images; images = null;
            if (old != null) for (int i = 0; i < old.Length; i++) if (old[i] != null) old[i].Dispose();
        }

        private partial class AlbumView : Control
        {
            public List<Album> Page = new List<Album>();
            public int Selected = 0;
            public double SelAngle = 0;
            public double Phase = 0;
            public Color BgAccent = DefaultAccent;
            public Func<Album, Image> GetCover;
            public Func<string, Image> GetCoverByPath;   // discos TOP: caratula por ruta (ciclado)
            public Action<int> OnActivate;
#if ROCOLA_GPU
            public bool GpuCovered;   // el panel GPU (pantalla completa, opaco) tapa esta vista: no malgastes su OnPaint (rejilla+estrellas) por CPU
#endif
            public float BgDim = 1f;  // atenuacion del starfield mientras se navega con musica (el form la anima)

            private Bitmap cubeCache;
            public float WallOpacity = 1f;
            private Bitmap wallLayer;
            private int cacheW = -1, cacheH = -1;
            private string cacheSig = null;
            // CACHE DE CELDAS: cada disco (portada estatica + etiqueta) se HORNEA a un bitmap UNA vez y luego se
            // COPIA (blit 1:1) en cubeCache al paginar. Antes cada pagina re-ESCALABA 18 portadas y re-MAQUETABA
            // 18 etiquetas (ajuste de linea) -> carisimo al paginar rapido. Ahora paginar es casi solo copias.
            // Nace: cuando se horneo. Hace falta para saber si una casilla chica es de ESTE deslizamiento o
            // quedo de uno de hace rato (ver PeekOrBakeCell).
            private sealed class CellSprite { public Bitmap Bmp; public bool HasCover; public int Uso; public int Nace; }
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
            private sealed class CeldaGpu { internal Bitmap Bmp; internal GpuCellLayout Layout; internal Image Cover; internal long Uso; internal bool Queued, Lod; }
            private readonly Dictionary<string, CeldaGpu> gpuCellCache = new Dictionary<string, CeldaGpu>();
            private readonly Dictionary<Album, CeldaGpu> gpuAnonymousCache = new Dictionary<Album, CeldaGpu>();
            private long gpuCellUso;
            private void ClearGpuCells()
            {
                foreach (CeldaGpu c in gpuCellCache.Values) c.Bmp.Dispose(); gpuCellCache.Clear();
                foreach (CeldaGpu c in gpuAnonymousCache.Values) c.Bmp.Dispose(); gpuAnonymousCache.Clear();
                gpuCellUso = 0;
            }
            private void PodaGpuCells()
            {
                int count = gpuCellCache.Count + gpuAnonymousCache.Count;
                if (count <= capCeldas) return;
                long[] usos = new long[count]; int n = 0;
                foreach (CeldaGpu c in gpuCellCache.Values) usos[n++] = c.Uso;
                foreach (CeldaGpu c in gpuAnonymousCache.Values) usos[n++] = c.Uso;
                Array.Sort(usos); long corte = usos[Math.Max(0, count - keepCeldas)];
                List<string> fuera = new List<string>();
                foreach (KeyValuePair<string, CeldaGpu> c in gpuCellCache) if (c.Value.Uso < corte) fuera.Add(c.Key);
                foreach (string k in fuera) { gpuCellCache[k].Bmp.Dispose(); gpuCellCache.Remove(k); }
                List<Album> sinCarpeta = new List<Album>();
                foreach (KeyValuePair<Album, CeldaGpu> c in gpuAnonymousCache) if (c.Value.Uso < corte) sinCarpeta.Add(c.Key);
                foreach (Album a in sinCarpeta) { gpuAnonymousCache[a].Bmp.Dispose(); gpuAnonymousCache.Remove(a); }
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
            private Bitmap BakeGpuCell(Album a, int cw, int side, int labelH, int gap, Image cover, out GpuCellLayout layout)
            {
                bool q = EnCola != null && EnCola(a);
                layout = PlanAtlasGpu(cw, side, labelH, gap, q);
                if (layout.Width == 0)
                {
                    Bitmap whole = BakeCell(a, cw, side, labelH, gap, cover);
                    if (cover == null && PortadasLod)
                        using (Graphics g = Graphics.FromImage(whole))
                        { g.CompositingMode = CompositingMode.SourceCopy; g.FillRectangle(Brushes.Transparent, 0, 0, cw, side); }
                    return whole;
                }
                Bitmap bmp = new Bitmap(layout.Width, layout.Height, PixelFormat.Format32bppPArgb);
                try
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Transparent); g.InterpolationMode = InterpolationMode.Bilinear;
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.SetClip(layout.Face);
                        g.TranslateTransform(layout.Face.X - layout.FaceAt.X, layout.Face.Y);
                        Rectangle disco = new Rectangle((cw - side) / 2, 0, side, side);
                        if (cover != null || !PortadasLod) Cube.Draw(g, disco, cover, 0.0);
                        if (q) Palomito(g, disco);
                    }
                    // Texto una sola vez, con las mismas medidas/fuente. Copias PArgb sin recomponer alfa.
                    using (Bitmap label = new Bitmap(cw, labelH + 2, PixelFormat.Format32bppPArgb))
                    {
                        using (Graphics g = Graphics.FromImage(label))
                        {
                            g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                            EnsureLabelRes();
                            g.DrawString(AlbumLabel(a), lblFont, lblBrush, new Rectangle(2, 1, cw - 4, labelH), lblSF);
                        }
                        CopiaEtiquetaGpu(label, bmp, layout.Text1At, layout.Text1);
                        CopiaEtiquetaGpu(label, bmp, layout.Text2At, layout.Text2);
                    }
                    return bmp;
                }
                catch { bmp.Dispose(); throw; }
            }
            private static void CopiaEtiquetaGpu(Bitmap source, Bitmap dest, Rectangle at, Rectangle to)
            {
                // Un pixel vecino a cada lado evita costuras con filtrado bilineal durante el deslizamiento.
                int from = Math.Max(0, at.X - 1), end = Math.Min(source.Width, at.Right + 1);
                Rectangle src = new Rectangle(from, 0, end - from, source.Height);
                Rectangle dst = new Rectangle(to.X + from - at.X, to.Y - 1, src.Width, src.Height);
                BitmapData s = null, d = null;
                try
                {
                    s = source.LockBits(src, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                    d = dest.LockBits(dst, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                    for (int y = 0; y < src.Height; y++) D3D9.CopyMemory(
                        new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride),
                        new IntPtr(s.Scan0.ToInt64() + (long)y * s.Stride), (UIntPtr)(uint)(src.Width * 4));
                }
                finally { try { if (d != null) dest.UnlockBits(d); } finally { if (s != null) source.UnlockBits(s); } }
            }
            private Bitmap PeekGpuCell(Album a, int cw, int side, int labelH, int gap, Image cover, bool visible, ref int budget, out GpuCellLayout layout)
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
                budget--; Bitmap bmp = BakeSeparatedCell(a, cw, side, labelH, gap, out layout);
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
            public Bitmap ContourSpriteBmp { get { EnsureContourSprite(); return contourSprite; } }   // sprite del resalte (lo hornea si falta: el panel GPU lo lee aunque el OnPaint por CPU este suprimido)
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

            // Avanza el giro SIN repintar por CPU (el fondo y los cubos los pinta la GPU). Lo llama el
            // temporizador a 24fps cuando el fondo GPU esta activo, en vez de AnimTick (que invalida y repinta).
            public void StepSpinGpu() { StepSpin(); }

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
                    Image ca, cb; float f; SelectedCovers(out ca, out cb, out f);
                    st.Sel.Has = true; st.Sel.X = a.X; st.Sel.Y = a.Y; st.Sel.W = a.Width; st.Sel.H = a.Height;
                    st.Sel.Angle = SelAngle; st.Sel.CoverA = ca as Bitmap; st.Sel.CoverB = cb as Bitmap; st.Sel.Fade = f;
                }
                if (prevIdx >= 0 && prevIdx != Selected && prevIdx < Page.Count)   // RELEVO: el que dejamos, girando y decayendo
                {
                    Rectangle a2, l2; Geometry(w, h, prevIdx, out a2, out l2);
                    st.Relay.Has = true; st.Relay.X = a2.X; st.Relay.Y = a2.Y; st.Relay.W = a2.Width; st.Relay.H = a2.Height;
                    st.Relay.Angle = prevAngle; st.Relay.CoverA = ((GetCover != null) ? GetCover(Page[prevIdx]) : null) as Bitmap;
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
                int now = Environment.TickCount;
                gpuRapidSlide = RapidPageTurn(now, gpuLastSlideTick, gpuHasSlideTick);
                gpuLastSlideTick = now; gpuHasSlideTick = true;
                // PASAR DE PAGINA DESLIZANDO: la que se estaba viendo sale mientras entra la nueva. No si la pagina no
                // cambio (no hay de donde salir) ni si la anterior iba a medio entrar: habria que sacarla de donde
                // estaba, no de su sitio, y daria un salto. Ahi se va de golpe, como siempre.
                slideViejas = (PaginaDesliza && !enCurso && paginaVista != null && !ReferenceEquals(paginaVista, Page)) ? paginaVista : null;
                empujeHecho = PaginaDesliza ? 0f : 1f; empujeDir = dir; empujeDesde = now;
                slideFrom = dir * (float)ancho; slideTick = now;
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
            private float empujeHecho = 1f; private int empujeDir, empujeDesde;
            internal static float CurvaCielo(int dt)
            {
                if (dt <= 0) return 0f;
                if (dt >= SlideMs) return 1f;
                float u = dt / (float)SlideMs;
                return u * u * (3f - 2f * u);
            }
            // Lo que hay que correr el cielo DESDE LA ULTIMA VEZ que se pregunto (px; negativo = a la izquierda). Si
            // nadie lo pregunto mientras pasaba la pagina (la pared estaba tapada), lo que faltaba se tira: correr el
            // cielo de golpe mas tarde seria un salto.
            public float TomaEmpuje()
            {
                if (empujeHecho >= 1f) return 0f;
                int dt = Environment.TickCount - empujeDesde;
                if (!PaginaDesliza || dt < 0 || dt > SlideMs + 150) { empujeHecho = 1f; return 0f; }
                float p = CurvaCielo(dt);
                float d = p - empujeHecho; empujeHecho = p;
                return -empujeDir * EmpujeFr * Width * d;
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
            private int CeldasViejas(GpuGridCell[] buf, int n, int w, int h, int cw, int side, int labelH, int gap, int dt, int ox, int oy)
            {
                bool izq = slideFrom > 0f;
                int nada = 0;
                for (int i = 0; i < slideViejas.Count && i < PageSize && n < buf.Length; i++)
                {
                    Album a = slideViejas[i];
                    int col = i % Cols;
                    float al; float falta = CurvaColumna(dt, OrdenColumna(col, izq, true), true, out al);
                    float cdx = slideFrom * (falta - 1f);
                    Rectangle area, lbl; Geometry(w, h, i, out area, out lbl);
                    Image tapa = (PeekCoverGpu != null) ? PeekCoverGpu(a) : null;
                    buf[n] = new GpuGridCell();
                    buf[n].Key = ClaveCasilla(a);
                    buf[n].Bmp = PeekGpuCell(a, cw, side, labelH, gap, tapa, true, ref nada, out buf[n].Layout);
                    buf[n].Direct = true; buf[n].FullCover = tapa as Bitmap;
                    CeldaGpu cached;
                    buf[n].HasCover = buf[n].Key != null && gpuCellCache.TryGetValue(buf[n].Key, out cached) && cached.Cover != null;
                    buf[n].Preview = PortadasLod && PeekPreviewGpu != null ? PeekPreviewGpu(a) as Bitmap : null;
                    buf[n].Placeholder = HojeoLigero;
                    buf[n].CoverRect = new RectangleF(ox + area.X + cdx, oy + area.Y, area.Width, area.Height);
                    buf[n].Slot = PageSize + i;
                    buf[n].X = ox + col * cw + cdx; buf[n].Y = oy + area.Y; buf[n].W = cw; buf[n].H = side + gap + labelH;
                    buf[n].A = al;
                    n++;
                }
                return n;
            }
            public bool Sliding { get { return slideTick >= 0 && Environment.TickCount - slideTick < SlideMs; } }
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
            // Devuelve el desplazamiento de este cuadro y, de paso, el desvanecido de entrada.
            private float SlideNow(out float alpha)
            {
                alpha = 1f;
                if (slideTick < 0) return 0f;
                int dt = Environment.TickCount - slideTick;
                if (dt < 0 || dt >= SlideMs) { slideTick = -1; return 0f; }
                return slideFrom * CurvaHojeo(dt, out alpha);
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
                int dtHojeo = (slideTick >= 0) ? Environment.TickCount - slideTick : -1;
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
                    Image cover = chica ? (PeekCoverGpu != null ? PeekCoverGpu(a) : null) : (GetCover != null ? GetCover(a) : null);
                    buf[n].Bmp = chica && !PortadasLod ? null : PeekGpuCell(a, cw, side, labelH, gap, cover, true, ref useBudget, out buf[n].Layout);
                    buf[n].Direct = true; buf[n].FullCover = cover as Bitmap;
                    if (!chica) budget = useBudget;
                    CeldaGpu cached = null;
                    if (buf[n].Key != null) gpuCellCache.TryGetValue(buf[n].Key, out cached);
                    else gpuAnonymousCache.TryGetValue(a, out cached);
                    buf[n].HasCover = cached != null && cached.Bmp == buf[n].Bmp && cached.Cover != null;
                    buf[n].NeedsCover = !string.IsNullOrEmpty(a.CoverPath);
                    buf[n].Preview = PortadasLod && PeekPreviewGpu != null ? PeekPreviewGpu(a) as Bitmap : null;
                    if (budget < beforeBudget)
                        gpuBakeCost = CargaTrabajo.AprendeCoste(gpuBakeCost, System.Diagnostics.Stopwatch.GetTimestamp() - cellStart);
                    buf[n].Placeholder = HojeoLigero;
                    float cdx = dx, ca = alpha;
                    if (columnas) cdx = slideFrom * CurvaColumna(dtHojeo, OrdenColumna(i % Cols, izq, false), false, out ca);
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
            public Func<Album, Image> PeekCoverGpu;
            public Func<Album, Image> PeekPreviewGpu;
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
                        cells[i].Direct = true; cells[i].FullCover = PeekCoverGpu != null ? PeekCoverGpu(Ahead[i]) as Bitmap : null; }
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
                    Image cover = PeekCoverGpu(a);
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
            private Pen cPenGlow, cPenMid, cPenLine;   // contorno del disco seleccionado (cacheadas; antes se creaban por cuadro)
            private Bitmap contourSprite; private Size contourSpriteSize = Size.Empty;   // contorno HORNEADO (3 trazos AA -> 1 copia); todas las casillas miden igual
            private Font lblFont; private SolidBrush lblBrush; private StringFormat lblSF;       // recursos de etiquetas (reusables)
            private Bitmap blendBmp;   // para el fundido suave de portadas del cubo TOP
            public Starfield Sky;   // COMPARTIDO con el fondo del form: esta vista pinta su VENTANA del mismo campo (una sola pieza)
            public float PulseLo = 0f, PulseMid = 0f, PulseHi = 0f;   // pulso 0..1 por banda (graves/medios/agudos): cada estrella late con SU frecuencia

            private Image BlendInto(Image a, Image b, float f)
            {
                // 192 basta: el cubo se dibuja a ~154px; antes 320 gastaba el doble de relleno en cada fundido.
                if (blendBmp == null) blendBmp = new Bitmap(192, 192, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(blendBmp))
                {
                    g.Clear(Color.Black);
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    Rectangle dst = new Rectangle(0, 0, blendBmp.Width, blendBmp.Height);
                    if (a != null) g.DrawImage(a, dst);
                    if (b != null && f > 0f)
                    {
                        ColorMatrix cm = new ColorMatrix(); cm.Matrix33 = f;
                        using (ImageAttributes ia = new ImageAttributes())
                        {
                            ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                            g.DrawImage(b, dst, 0, 0, b.Width, b.Height, GraphicsUnit.Pixel, ia);
                        }
                    }
                }
                return blendBmp;
            }

            // Portadas a fundir del disco SELECCIONADO: para discos TOP cicla sus portadas (ca actual, cb
            // siguiente, f = mezcla 0..1); para discos normales devuelve la suya en ca (cb=null, f=0). La
            // usan OnPaint (CPU, que las funde a una imagen) y el modo GPU (que funde en dos pasadas).
            private void SelectedCovers(out Image ca, out Image cb, out float f)
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
            private Image SelectedCover()
            {
                Image ca, cb; float f; SelectedCovers(out ca, out cb, out f);
                return (cb == null || f <= 0.002f) ? ca : BlendInto(ca, cb, f);
            }

            private void EnsureLabelRes()
            {
                if (lblFont == null) lblFont = new Font(this.Font.FontFamily, 10f, FontStyle.Bold);
                if (lblBrush == null) lblBrush = new SolidBrush(Color.White);
                if (lblSF == null)
                {
                    lblSF = new StringFormat();
                    lblSF.Alignment = StringAlignment.Center; lblSF.LineAlignment = StringAlignment.Center;
                    lblSF.Trimming = StringTrimming.EllipsisCharacter;
                    lblSF.FormatFlags = StringFormatFlags.LineLimit;
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
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnFontChanged(EventArgs e)
            {
                base.OnFontChanged(e);
                SueltaDibujo(ref lblFont);
                if (cellCache != null) ResetCellCache();
                SueltaDibujo(ref cubeCache);
            }
            protected override void Dispose(bool disposing)
            {
                try
                {
                    if (disposing)
                    {
                        if (cellCache != null) ClearCellCache();
                        SueltaDibujo(ref cubeCache); SueltaDibujo(ref contourSprite); SueltaDibujo(ref wallLayer);
                        SueltaDibujo(ref blendBmp); SueltaDibujo(ref lblFont);
                        SueltaDibujo(ref lblBrush); SueltaDibujo(ref lblSF);
                        SueltaDibujo(ref cPenGlow); SueltaDibujo(ref cPenMid); SueltaDibujo(ref cPenLine);
                    }
                }
                finally { base.Dispose(disposing); }
            }

            // Fuerza reconstruir la capa de cubos (cuando llega una portada en segundo plano)
            public void InvalidateCubeCache() { cacheSig = null; Invalidate(); }
            // Descarta las celdas horneadas (p.ej. tras re-escanear la biblioteca: mismo Folder pero etiqueta/portada
            // pueden haber cambiado). Fuerza rehornear en el proximo RebuildCubes.
            public void ResetCellCache() { ClearCellCache(); cellCacheW = cellCacheH = -1; cacheSig = null; Invalidate(); }

            protected override void OnMouseClick(MouseEventArgs e)
            {
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
                base.OnMouseClick(e);
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
            private Bitmap BakeCell(Album a, int cw, int side, int labelH, int gap, Image cover)
            { return BakeCell(a, cw, side, labelH, gap, cover, 1f); }

            // 'esc' encoge la casilla, para el hojeo (ver CeldaChica).
            //
            // SE ENCOGE EL DIBUJO, NO LAS MEDIDAS, y esto no es un capricho: la letra del rotulo tiene tamano
            // FIJO -no sale de 'labelH', ver EnsureLabelRes-, asi que pasando las medidas partidas por dos el
            // disco menguaria pero el nombre seguiria igual de grande: enorme para su hueco, y cortado.
            // Encogiendo el pincel sale la MISMA casilla, igual de proporcionada, solo que con menos pixeles.
            public Func<Album, bool> EnCola;   // ¿este disco tiene algo pedido? (lo contesta el form)

            private Bitmap BakeCell(Album a, int cw, int side, int labelH, int gap, Image cover, float esc)
            {
                int bw = cw, bh = side + gap + labelH;
                bool encoge = (esc > 0f && esc < 0.999f);
                if (encoge)
                {
                    bw = (int)(cw * esc); if (bw < 1) bw = 1;
                    bh = (int)((side + gap + labelH) * esc); if (bh < 1) bh = 1;
                }
                Bitmap bmp = new Bitmap(bw, bh, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    if (encoge) g.ScaleTransform(esc, esc);
                    g.InterpolationMode = InterpolationMode.Bilinear;   // mas rapido que bicubico (CPU vieja); las portadas ya son de buena resolucion
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Rectangle rDisco = new Rectangle((cw - side) / 2, 0, side, side);
                    Cube.Draw(g, rDisco, cover, 0.0);   // disco centrado (mismo x que Geometry)
                    if (EnCola != null && EnCola(a)) Palomito(g, rDisco);
                    EnsureLabelRes();
                    g.DrawString(AlbumLabel(a), lblFont, lblBrush, new Rectangle(2, side + gap, cw - 4, labelH), lblSF);   // etiqueta pegada al disco
                }
                return bmp;
            }

            // EL PALOMITO DE "YA LA PUSISTE". Va en la esquina de arriba a la derecha del disco, que es la
            // unica de las cuatro que no pisa ni la etiqueta ni el recuadro de seleccion. Verde, el mismo
            // de la cola, para que se lea sin explicarlo: verde = tuya, ya va.
            private static void Palomito(Graphics g, Rectangle disco)
            {
                float d = disco.Width * 0.26f; if (d < 13f) d = 13f;
                float x = disco.Right - d * 0.92f, y = disco.Top - d * 0.08f;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(236, 120, 230, 165)))
                    g.FillEllipse(b, x, y, d, d);
                using (Pen aro = new Pen(Color.FromArgb(150, 10, 40, 26), Math.Max(1f, d * 0.07f)))
                    g.DrawEllipse(aro, x, y, d, d);
                using (Pen p = new Pen(Color.FromArgb(255, 10, 44, 28), Math.Max(1.6f, d * 0.15f)))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                    g.DrawLines(p, new PointF[] {
                        new PointF(x + d * 0.27f, y + d * 0.52f),
                        new PointF(x + d * 0.44f, y + d * 0.70f),
                        new PointF(x + d * 0.75f, y + d * 0.32f) });
                }
            }

            // Devuelve el bitmap horneado de la celda del album; lo re-hornea si falta o si cambio el estado de
            // portada (placeholder -> cargada). Clave = CellKey(a) (ver arriba). Sin clave no se cachea.
            private Bitmap GetOrBakeCell(Album a, int cw, int side, int labelH, int gap, Image cover)
            {
                string key = ClaveCasilla(a);
                // LA MARCA ENTRA EN LA LLAVE, y por eso no hace falta avisar a nadie de nada: al pedir una
                // cancion, la llave de ese disco cambia, la casilla no esta en la caja y se hornea con su
                // palomito. Y la version sin marca se queda guardada para cuando la cancion ya sono.
                bool hasCover = (cover != null);
                CellSprite cs = null;
                if (key != null && cellCache.TryGetValue(key, out cs) && cs.Bmp != null && cs.HasCover == hasCover)
                { cs.Uso = ++cellUso; return cs.Bmp; }   // reutiliza (paginar = solo copiar)
                Bitmap bmp = BakeCell(a, cw, side, labelH, gap, cover);
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

            private Bitmap PeekOrBakeCell(Album a, int cw, int side, int labelH, int gap, Image cover, ref int budget)
            { return PeekOrBakeCell(a, cw, side, labelH, gap, cover, ref budget, false); }

            // 'chica' = la pagina se esta deslizando, asi que la casilla va a media resolucion. Se guarda con
            // su propia clave para que la entera siga ahi: al parar hay que volver a ella.
            private Bitmap PeekOrBakeCell(Album a, int cw, int side, int labelH, int gap, Image cover, ref int budget, bool chica)
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
                Bitmap bmp = BakeCell(a, cw, side, labelH, gap, cover, chica ? CeldaChica : 1f);
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
                    cubeCache = new Bitmap(w, h);
                }
                if (cellCacheW != w || cellCacheH != h) { ClearCellCache(); cellCacheW = w; cellCacheH = h; RecalculaCajon(w, h); }   // tamano nuevo: las celdas horneadas ya no encajan (y el cajon cambia de tope)
                else if (cellCache.Count > capCeldas) PodaCeldas();                                             // lleno: fuera las mas viejas (nunca la pagina que se ve)
                int budget = CpuBakeBudget;   // HORNEADO REPARTIDO (como en GPU): a lo mas CpuBakeBudget celdas nuevas por repintado; el
                cubesPending = false;         // resto queda para el proximo (OnPaint re-invalida). Evita el congelon de hornear 18 de golpe.
                using (Graphics g = Graphics.FromImage(cubeCache))
                {
                    g.Clear(Color.Transparent);
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
                        Bitmap cell = PeekOrBakeCell(Page[i], cw, side, labelH, gap, (GetCover != null) ? GetCover(Page[i]) : null, ref budget);
                        if (cell != null) g.DrawImageUnscaled(cell, (i % Cols) * cw, area.Y);   // copia 1:1 (sin re-escalar portada ni re-maquetar texto)
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
                using (Graphics g = Graphics.FromImage(cubeCache))
                {
                    for (int i = 0; i < Page.Count && i < PageSize; i++)
                    {
                        if (Page[i] == null || Page[i].CoverPath != coverPath) continue;
                        int col = i % Cols, row = i / Cols;
                        Rectangle cell = new Rectangle(col * cw, row * ch, cw, ch);
                        Rectangle area, lbl; Geometry(w, h, i, out area, out lbl);
                        // rehornea la celda CON la portada (GetOrBakeCell detecta que HasCover cambio) y actualiza
                        // el cache: revisitar la pagina sigue siendo instantaneo. Luego la copia en cubeCache.
                        Bitmap cell2 = GetOrBakeCell(Page[i], cw, side, labelH, gap, (GetCover != null) ? GetCover(Page[i]) : null);
                        g.CompositingMode = CompositingMode.SourceCopy;   // deja la celda transparente antes de recomponer
                        using (SolidBrush tb = new SolidBrush(Color.Transparent)) g.FillRectangle(tb, cell);
                        g.CompositingMode = CompositingMode.SourceOver;
                        g.DrawImageUnscaled(cell2, col * cw, area.Y);
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
                    cPenGlow = new Pen(Color.FromArgb(20, 255, 255, 255), 11f);   // resplandor ancho y tenue
                    cPenMid = new Pen(Color.FromArgb(45, 255, 255, 255), 6f);     // resplandor medio
                    cPenLine = new Pen(Color.FromArgb(235, 255, 255, 255), 2.4f); // linea nitida
                }
                if (contourSprite != null) contourSprite.Dispose();
                contourSprite = new Bitmap(need.Width, need.Height, PixelFormat.Format32bppPArgb);
                using (Graphics cg = Graphics.FromImage(contourSprite))
                using (GraphicsPath cp = RoundRect(new Rectangle(ContourMargin, ContourMargin, fr.Width, fr.Height), 10))
                {
                    cg.SmoothingMode = SmoothingMode.AntiAlias;
                    cg.DrawPath(cPenGlow, cp);
                    cg.DrawPath(cPenMid, cp);
                    cg.DrawPath(cPenLine, cp);
                }
                contourSpriteSize = need;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                int w = Width, h = Height;
                if (w < 8 || h < 8) { g.Clear(Shade(BgAccent, 0.82)); return; }
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
                    Sky.SkyFullW = (this.Parent != null) ? this.Parent.ClientSize.Width : w;
                    Sky.SkyFullH = (this.Parent != null) ? this.Parent.ClientSize.Height : h;
                    Sky.SkyMid = SkyColorMid(BgAccent); Sky.Acento = BgAccent;
                    Sky.Dim = BgDim;   // atenuado mientras se navega con musica (vuelve solo tras 10s sin botones)
                    Sky.DrawReactive(g, w, h, SkyColor(BgAccent), SkyColor2(BgAccent), PulseLo, PulseMid, PulseHi);   // fondo titilante + LATIDO por banda (grandes=graves, medianas=medios/agudos)
                }
                if (WallOpacity < 0.999f)
                {
                    if (wallLayer == null || wallLayer.Width != w || wallLayer.Height != h)
                    {
                        SueltaDibujo(ref wallLayer);
                        wallLayer = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    }
                    using (Graphics layer = Graphics.FromImage(wallLayer))
                    {
                        layer.Clear(Color.Transparent);
                        DrawWall(layer, w, h);
                    }
                    using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
                    {
                        System.Drawing.Imaging.ColorMatrix cm = new System.Drawing.Imaging.ColorMatrix();
                        cm.Matrix33 = Math.Max(0f, Math.Min(1f, WallOpacity));
                        ia.SetColorMatrix(cm);
                        g.DrawImage(wallLayer, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, ia);
                    }
                }
                else
                {
                    SueltaDibujo(ref wallLayer);
                    DrawWall(g, w, h);
                }
                if (cubesPending) Invalidate();
            }
            private void DrawWall(Graphics g, int w, int h)
            {
                g.DrawImageUnscaled(cubeCache, 0, 0);

                if (Page != null && Selected >= 0 && Selected < Page.Count)
                {
                    int cw = w / Cols, ch = h / Rows;
                    if (cw >= 4 && ch >= 4)
                    {
                        Rectangle area, lbl;
                        Geometry(w, h, Selected, out area, out lbl);
                        g.InterpolationMode = InterpolationMode.Bilinear;
                        // Contorno blanco con resplandor del disco SELECCIONADO (ademas del giro), para ubicarlo facil.
                        Rectangle fr = area; fr.Inflate(4, 4);
                        EnsureContourSprite();   // sprite horneado (3 trazos AA -> 1 copia); solo se re-hornea al cambiar tamano
                        if (contourSprite != null) g.DrawImageUnscaled(contourSprite, fr.X - ContourMargin, fr.Y - ContourMargin);
                        Cube.Draw(g, area, SelectedCover(), SelAngle);
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
                        g.InterpolationMode = InterpolationMode.Bilinear;
                        Cube.Draw(g, area2, (GetCover != null) ? GetCover(Page[prevIdx]) : null, prevAngle);
                    }
                }
                if (cubesPending) Invalidate();   // quedan celdas por hornear: pide otro repintado para completarlas (repartido, sin congelar)
            }
        }

        // ==========  CUBO DE LA CANCION (gira + se inclina)  ==========
        private class CubeBox : Control
        {
            public Image Cover;
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
            private Image relNueva;       // la portada que entra
            private double relGirado;     // cuanto lleva puesto de la media vuelta

            public void Relevo(Image nueva)
            {
                if (relTick != 0 && object.ReferenceEquals(relNueva, nueva)) return;   // esa ya viene en camino
                relNueva = nueva;
                relGirado = 0;
                relTick = Environment.TickCount; if (relTick == 0) relTick = 1;
            }
            public bool Girando { get { return relTick != 0; } }
            public bool UsaPortada(Image im)
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
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                this.BackColor = Color.FromArgb(10, 15, 44);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                if (this.BackColor.A == 255) g.Clear(this.BackColor);
                int w = Width, h = Height;
                if (w < 12 || h < 12) return;
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int side = (int)(Math.Min(w, h) * 0.78);
                Rectangle area = new Rectangle((w - side) / 2, (h - side) / 2, side, side);

                float cx = w / 2f, cy = h / 2f;
                float tilt = (float)(10.0 * Math.Sin(Phase * 0.9));
                GraphicsState st = g.Save();
                g.TranslateTransform(cx, cy);
                g.RotateTransform(tilt);
                g.TranslateTransform(-cx, -cy);
                Cube.Draw(g, area, Cover, Angle);
                g.Restore(st);
            }
        }
    }
}
