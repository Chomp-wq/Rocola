#if ROCOLA_GPU
using System;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: el acabado de la lista del disco, su vidrio y su barra de paginas dibujan con SkiaSharp directo y las funciones nativas (Xp/Nativo), no con GDI+; lo que les llega de archivos sin traducir (las paginas horneadas de la lista, PintaPagina, RoundedPath, DegradadoV, CtrlDirLeft y CtrlDirRight, las piezas de la tarjeta, el buscador) pasa por el puente (Plataforma/Puente.cs y Puente.acabado-estilo.cs)
namespace Rocola
{
    public partial class Form1
    {
        // Solo presentacion: nunca cambia rutas, etiquetas, orden ni claves de busqueda.
        internal static string TituloSinPista(string title)
        {
            if (string.IsNullOrEmpty(title)) return title;
            int i = 0;
            while (i < title.Length && char.IsWhiteSpace(title[i])) i++;
            if (i < title.Length && title[i] == '-') i++;
            int start = i;
            while (i < title.Length && title[i] >= '0' && title[i] <= '9') i++;
            int digits = i - start;
            if (digits < 1 || digits > 3) return title;
            while (i < title.Length && char.IsWhiteSpace(title[i])) i++;
            if (i >= title.Length) return title;
            char separator = title[i++];
            if (separator != '-' && separator != '.' && separator != '–' && separator != '—') return title;
            // Un numero decimal no es un numero de pista.
            if (separator == '.' && i < title.Length && char.IsDigit(title[i])) return title;
            while (i < title.Length && char.IsWhiteSpace(title[i])) i++;
            return i < title.Length ? title.Substring(i) : title;
        }
        private bool songMoveVertical;
        private int songMoveDirection;
        private InstrFlash songPagerFlash;
        private void MoveSongInput(int delta, bool vertical)
        {
            if (!gpuEnabled) { MoveSong(delta); return; }
            songMoveVertical = vertical; songMoveDirection = Math.Sign(delta);
            if (!vertical)
            {
                if (songPagerFlash == null) songPagerFlash = new InstrFlash();
                songPagerFlash.Fire(delta < 0 ? 0 : 1, Color.FromArgb(242, 206, 72));
            }
            try
            {
                // Con una sola fila por pagina, izquierda/derecha tampoco deben ciclar.
                if (!vertical) delta = Math.Max(0, Math.Min(songItems.Count - 1, songSel + delta)) - songSel;
                MoveSong(delta);
            }
            finally { songMoveVertical = false; songMoveDirection = 0; }
        }
        private partial class SongListView
        {
            public bool GpuSlideVertical;
            public void StartGpuSlide(int direction, bool vertical)
            {
                StartSlide(direction); GpuSlideVertical = vertical;
                if (vertical) slideDesde = direction * (float)Height;
            }
            // El velo y la clave de color se hacian NUEVOS en cada pintada. Los dos son objetos de GDI+
            // con su manija nativa detras, y esta lista se repinta al desplazarse y al pasar de pagina.
            // Se guardan como hace el resto de la casa (ver EnsurePaintRes en el buscador) y se sueltan
            // en el Dispose de la lista, por SueltaAcabadoGpu.
            private SKPaint veloGpu;   // EN LINUX: SolidBrush: un SKPaint de su color
            private Color? claveGpu;   // EN LINUX: ImageAttributes con SetColorKey: la clave de color es un color (Imagenes.Pinta la recibe asi); null: aun sin hacer
            private Color claveGpuDe;
            private SKBitmap cristalGpu;   // EN LINUX: Bitmap: un SKBitmap (Nativo/Mapas.cs)
            private bool cristalNeutral;
            private float[][] tinteCristalGpu;   // EN LINUX: ImageAttributes con SetColorMatrix: la matriz de color de GDI+, 5x5 (MatrizColor, Nativo/Imagenes.cs)
            private Color tinteCristalColor;
            private int gpuSnapshotSel = -1;
            private SKBitmap gpuSnapshotRow, gpuSelectedRaw;   // EN LINUX: Bitmap: SKBitmap
            private Color? GpuColorKey()   // EN LINUX: la clave de color: un color (en XP, el ImageAttributes con SetColorKey)
            {
                if (claveGpu == null || claveGpuDe != BackColor)
                {
                    // EN LINUX: if (claveGpu == null) claveGpu = new ImageAttributes(): un color no se crea
                    claveGpu = BackColor;   // EN LINUX: claveGpu.SetColorKey(BackColor, BackColor, ColorAdjustType.Bitmap): la clave baja y la alta, el mismo color
                    claveGpuDe = BackColor;
                }
                return claveGpu;
            }
            private SKBitmap GpuRowBuffer(ref SKBitmap buffer, int w, int h)   // EN LINUX: SKBitmap
            {
                if (buffer == null || buffer.Width != w || buffer.Height != h)
                {
                    SueltaDibujo(ref buffer);
                    buffer = Mapas.Nuevo(w, h);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
                }
                return buffer;
            }
            // Restaurar una fila no debe volver a medir/dibujar todas las canciones de la pagina.
            // Guardamos sus pixeles originales: mismos bordes y suavizado, sin otro mapa de pagina.
            // La copia exacta (SourceCopy) va recortada a su renglon y el modo de mezcla se repone a mano. En Windows
            // da lo mismo; en libgdiplus (Mono, donde corren las baterias) Restore no devuelve el modo al dibujo y
            // SourceCopy sin recorte vaciaba el resto de la pagina: la fila elegida quedaba sola en la instantanea.
            private void UpdateGpuSnapshot(SKCanvas snapshot, bool copia, Filtro filtro, bool medio, SKBitmap selected, int index, int w)   // EN LINUX: el lienzo de Skia y lo que pesa aqui del estado de su Graphics (copia, filtro y medio pixel), como parametros; la fila elegida, un SKBitmap
            {
                bool modo = copia;   // EN LINUX: CompositingMode modo = snapshot.CompositingMode (la copia es SourceCopy)
                int state = snapshot.Save();   // EN LINUX: GraphicsState state = snapshot.Save(): el lienzo guarda su matriz y su recorte (la copia, en su variable)
                try
                {
                    copia = true;   // EN LINUX: snapshot.CompositingMode = SourceCopy
                    if (gpuSnapshotSel >= PageStart && gpuSnapshotSel < PageStart + RowsPerPage &&
                        gpuSnapshotRow != null && gpuSnapshotRow.Width == w && gpuSnapshotRow.Height == RowH)
                    {
                        int antes = (gpuSnapshotSel - PageStart) * RowH;
                        snapshot.ClipRect(new SKRect(0, antes, w, antes + RowH), SKClipOperation.Intersect, false);   // EN LINUX: SetClip(rect, Intersect): se suma al recorte que haya, en pixeles enteros (sin suavizar, como GDI+); lo quita el Restore de abajo
                        Imagenes.Pinta(snapshot, gpuSnapshotRow, new SKRect(0, 0, gpuSnapshotRow.Width, gpuSnapshotRow.Height), new SKRect(0, antes, gpuSnapshotRow.Width, antes + gpuSnapshotRow.Height), filtro, medio, copia);   // EN LINUX: snapshot.DrawImageUnscaled(fila, 0, antes): entera en ese punto, con el filtro, el medio pixel y la copia del lienzo (Nativo/Imagenes.cs)
                    }
                }
                finally { snapshot.RestoreToCount(state); copia = modo; }   // EN LINUX: snapshot.Restore(state) (el lienzo vuelve a su matriz y su recorte; la copia que devolvia Restore es la de Save, que es modo) y CompositingMode = modo
                int y = (index - PageStart) * RowH;
                SKBitmap original = GpuRowBuffer(ref gpuSnapshotRow, w, RowH);   // EN LINUX: SKBitmap
                using (SKCanvas copy = new SKCanvas(original))   // EN LINUX: Graphics.FromImage(original): un lienzo nuevo; su estado va en variables (el de fabrica: Bilinear, sin medio pixel)
                {
                    bool copiaCopy = true;   // EN LINUX: copy.CompositingMode = SourceCopy
                    copy.Clear(Color.Transparent.Sk());   // EN LINUX: copy.Clear(Color.Transparent)
                    Imagenes.Pinta(copy, hornoPag, new SKRect(0, y, w, y + RowH), new SKRect(0, 0, w, RowH), Filtro.Bilineal, false, copiaCopy);   // EN LINUX: copy.DrawImage(hornoPag, caja, 0, y, w, RowH, Pixel): ese trozo de la pagina horneada (hornoPag, de Form1.Vistas.cs, ya es un SKBitmap: directo)
                }
                Imagenes.Pinta(snapshot, selected, new SKRect(0, 0, selected.Width, selected.Height), new SKRect(0, y, selected.Width, y + selected.Height), filtro, medio, copia);   // EN LINUX: snapshot.DrawImageUnscaled(selected, 0, y)
                gpuSnapshotSel = index;
            }
            // Capas inmutables: pagina sin seleccion y una sola fila elegida.
            // Se conserva el horneado anterior para el hojeo ya existente.
            public SKBitmap BakeGpuListLayer(bool selected)   // EN LINUX: un SKBitmap; EnsureSongPiece se lo entrega como Bitmap a las piezas de la tarjeta (uiPieza, de Form1.Video.cs, aun sin traducir)
            {
                int w = ClientSize.Width, h = selected ? RowH : ClientSize.Height;
                pintandoGpu = true;   // con la tarjeta, la fila elegida queda dentro del cristal (DentroDelCristal)
                SKBitmap result = Mapas.Nuevo(w, h);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb): un SKBitmap premultiplicado y transparente
                int saved = Sel;
                try
                {
                    if (!selected)
                    {
                        Sel = -1; hornoSig = long.MinValue;
                        using (new CapturaCintas(null)) AseguraHorneada(w, h);
                        using (SKCanvas output = new SKCanvas(result))   // EN LINUX: Graphics.FromImage(result): un lienzo nuevo (estado de fabrica: Bilinear, sin medio pixel ni copia)
                        {
                            Color? key = GpuColorKey();   // EN LINUX: la clave de color (un color)
                            if (capturandoCintas == null)
                                Imagenes.Pinta(output, hornoPag, new SKRect(0, 0, w, h), new SKRect(0, 0, w, h), Filtro.Bilineal, false, false, Envoltura.Transparente, null, key, key);   // EN LINUX: output.DrawImage(hornoPag, caja, 0, 0, w, h, Pixel, key): con la clave de color y la envoltura de fabrica de ImageAttributes (Clamp: transparente); hornoPag (Form1.Vistas.cs) ya es un SKBitmap: directo
                            else using (SKBitmap pageRaw = Mapas.Nuevo(w, h))   // EN LINUX: new Bitmap(w, h, Format32bppPArgb)
                            {
                                using (SKCanvas lienzo = new SKCanvas(pageRaw)) { bool suavePag = false; PintaPagina(lienzo, ref suavePag, AjusteLetra.DelSistema, false, Filtro.Bilineal, false, PageStart); }   // EN LINUX: Graphics.FromImage(pageRaw): un lienzo nuevo; PintaPagina (Form1.Vistas.cs, ya traducido) recibe el lienzo y el estado de fabrica de ese Graphics (sin suavizar -ella lo cambia-, DelSistema, sin copia, Bilinear, sin medio pixel): directo
                                Imagenes.Pinta(output, pageRaw, new SKRect(0, 0, w, h), new SKRect(0, 0, w, h), Filtro.Bilineal, false, false, Envoltura.Transparente, null, key, key);   // EN LINUX: output.DrawImage(pageRaw, caja, 0, 0, w, h, Pixel, key)
                            }
                        }
                        gpuSnapshotSel = -1;
                        return result;
                    }
                    SKBitmap raw = GpuRowBuffer(ref gpuSelectedRaw, w, h); bool suaveFila = false;   // EN LINUX: SKBitmap; y el suavizado del Graphics de abajo (el de fabrica: ninguno), en una variable que comparten las dos PintaPagina de la fila elegida
                    using (SKCanvas g = new SKCanvas(raw))   // EN LINUX: Graphics.FromImage(raw): el lienzo nuevo; PintaPagina (Form1.Vistas.cs, ya traducido) recibe ese lienzo y su estado de fabrica: directo; es el MISMO en las dos PintaPagina de abajo, con el suavizado en suaveFila (la segunda hereda el que deja la primera, como en XP)
                    using (SKCanvas output = new SKCanvas(result))   // EN LINUX: Graphics.FromImage(result): un lienzo nuevo (estado de fabrica)
                    {
                        // La fila elegida se pinta sola, como si fuera la primera: se le dice donde esta de verdad, para
                        // que quede dentro de las esquinas del cristal (DentroDelCristal).
                        gpuFilaY = (saved - PageStart) * RowH;
                        // La instantanea anterior conserva el titulo completo estatico.
                        using (new CapturaCintas(null))
                        {
                            PintaPagina(g, ref suaveFila, AjusteLetra.DelSistema, false, Filtro.Bilineal, false, saved);   // EN LINUX: PintaPagina (Form1.Vistas.cs, ya traducido): el lienzo y el estado de fabrica de su Graphics, con el suavizado que comparten las dos llamadas
                            if (hornoPag != null)
                            using (SKCanvas snapshot = new SKCanvas(hornoPag))   // EN LINUX: Graphics.FromImage(hornoPag): un lienzo sobre la pagina horneada (Form1.Vistas.cs: ya es un SKBitmap)
                                UpdateGpuSnapshot(snapshot, false, Filtro.Bilineal, false, raw, saved, w);   // EN LINUX: con el estado de fabrica de ese Graphics nuevo (SourceOver, Bilinear, sin medio pixel)
                        }
                        if (capturandoCintas != null) PintaPagina(g, ref suaveFila, AjusteLetra.DelSistema, false, Filtro.Bilineal, false, saved);   // EN LINUX: PintaPagina: hereda el suavizado que dejo la primera (suaveFila), como en XP
                        Color? clave = GpuColorKey(); Imagenes.Pinta(output, raw, new SKRect(0, 0, w, h), new SKRect(0, 0, w, h), Filtro.Bilineal, false, false, Envoltura.Transparente, null, clave, clave);   // EN LINUX: output.DrawImage(raw, caja, 0, 0, w, h, Pixel, GpuColorKey()): con la clave de color
                    }
                    return result;
                }
                catch { result.Dispose(); throw; }
                finally { Sel = saved; gpuFilaY = 0; }
            }

            public long GpuLayerSignature()
            {
                pintandoGpu = true;
                int saved = Sel;
                try
                {
                    Sel = -1;
                    return Mix(Mix(FirmaPagina(Width, Height), Font.Name), (int)Font.Style);
                }
                finally { Sel = saved; }
            }
            public int GpuSlideToken { get { return slideTick; } }
            public float GpuSlideOffset { get { return DesplazaAhora(); } }
            public float GpuSlideTravel { get { return slideDesde; } }
            public bool GpuPreviousReady { get { return Sliding && hornoAnt != null && hornoAnt.Width == Width && hornoAnt.Height == Height; } }
            public SKBitmap BakeGpuPrevious()   // EN LINUX: un SKBitmap; EnsureSongPiece se lo entrega como Bitmap a las piezas de la tarjeta
            {
                if (!GpuPreviousReady) return null;
                SKBitmap result = Mapas.Nuevo(Width, Height);   // EN LINUX: new Bitmap(Width, Height, Format32bppPArgb)
                try
                {
                    using (SKCanvas g = new SKCanvas(result))   // EN LINUX: Graphics.FromImage(result): un lienzo nuevo (estado de fabrica)
                    // EN LINUX: using (ImageAttributes key = new ImageAttributes()): la clave de color es un color (abajo), no un objeto que soltar
                    {
                        Color key = BackColor;   // EN LINUX: key.SetColorKey(BackColor, BackColor, ColorAdjustType.Bitmap): la clave baja y la alta, el mismo color
                        Imagenes.Pinta(g, hornoAnt, new SKRect(0, 0, Width, Height), new SKRect(0, 0, Width, Height), Filtro.Bilineal, false, false, Envoltura.Transparente, null, key, key);   // EN LINUX: g.DrawImage(hornoAnt, caja, 0, 0, Width, Height, Pixel, key): con la clave y la envoltura de fabrica (Clamp: transparente); hornoAnt (Form1.Vistas.cs) ya es un SKBitmap: directo
                    }
                    return result;
                }
                catch { result.Dispose(); throw; }
            }
            partial void SueltaAcabadoGpu()
            {
                SueltaDibujo(ref veloGpu); claveGpu = null;   // EN LINUX: la clave es un color: nada que soltar (SueltaDibujo la soltaba y la dejaba en null)
                SueltaDibujo(ref cristalGpu);
                tinteCristalGpu = null;   // EN LINUX: la matriz de color es un arreglo: nada que soltar (SueltaDibujo la dejaba en null)
                SueltaDibujo(ref gpuSnapshotRow); SueltaDibujo(ref gpuSelectedRaw);
            }
            public void RenderTransparentGpuMode(SKCanvas g, bool suave, bool copia, Filtro filtro, bool medio, bool glass)   // EN LINUX: el lienzo de Skia y lo que pesa aqui del estado del Graphics (suavizado, copia, filtro y medio pixel)
            { RenderTransparentGpuTint(g, suave, copia, filtro, medio, glass, false, Color.White); }   // EN LINUX: con el lienzo y su estado
            public void RenderTransparentGpuTint(SKCanvas g, bool suave, bool copia, Filtro filtro, bool medio, bool glass, bool tinted, Color tint)   // EN LINUX: el lienzo de Skia y el estado del Graphics, como parametros; EnsureUiOverlay (Form1.Video.cs, aun sin traducir) la llama con su Graphics por el puente (Plataforma/Puente.acabado-estilo.cs)
            {
                pintandoGpu = true;   // la fila elegida, dentro del cristal (Form1.Vistas.cs, DentroDelCristal)
                int w = ClientSize.Width, h = ClientSize.Height;
                if (w < 4 || h < 4) return;
                AseguraHorneada(w, h);
                float dx = DesplazaAhora();
                // Velo mayormente opaco: deja ver el fondo sin competir con las letras.
                if (veloGpu == null) veloGpu = new SKPaint { Color = Color.FromArgb(210, 12, 16, 30).Sk() };   // EN LINUX: new SolidBrush(color)
                if (glass)
                {
                    if (cristalGpu == null || cristalGpu.Width != w || cristalGpu.Height != h || cristalNeutral != tinted)
                    {
                        SueltaDibujo(ref cristalGpu);
                        cristalGpu = BakeSongGlassTint(w, h, tinted); cristalNeutral = tinted;
                    }
                    if (tinted)
                    {
                        if (tinteCristalGpu == null || tinteCristalColor != tint)
                        {
                            tinteCristalGpu = null;   // EN LINUX: SueltaDibujo(ref tinteCristalGpu); tinteCristalGpu = new ImageAttributes(): de los atributos solo cuenta la matriz de color (abajo), un arreglo: nada que soltar
                            float[][] matrix = MatrizColor.Identidad();   // EN LINUX: new ColorMatrix(): la identidad de 5x5 (Nativo/Imagenes.cs)
                            matrix[0][0] = tint.R / 255f; matrix[1][1] = tint.G / 255f; matrix[2][2] = tint.B / 255f;   // EN LINUX: Matrix00, Matrix11 y Matrix22
                            tinteCristalGpu = matrix; tinteCristalColor = tint;   // EN LINUX: tinteCristalGpu.SetColorMatrix(matrix)
                        }
                        Imagenes.Pinta(g, cristalGpu, new SKRect(0, 0, w, h), new SKRect(0, 0, w, h), filtro, medio, copia, Envoltura.Transparente, tinteCristalGpu);   // EN LINUX: g.DrawImage(cristalGpu, caja, 0, 0, w, h, Pixel, tinte): con la matriz de color y la envoltura de fabrica de ImageAttributes (Clamp: transparente) (Nativo/Imagenes.cs)
                    }
                    else Imagenes.Pinta(g, cristalGpu, new SKRect(0, 0, cristalGpu.Width, cristalGpu.Height), new SKRect(0, 0, cristalGpu.Width, cristalGpu.Height), filtro, medio, copia);   // EN LINUX: g.DrawImageUnscaled(cristalGpu, 0, 0)
                }
                else Pixel.Rellena(g, new RectangleF(0, 0, w, h), veloGpu, suave, medio, copia);   // EN LINUX: g.FillRectangle(veloGpu, 0, 0, w, h) (Nativo/Pixel.cs)
                // Elimina el relleno exacto, no reduce el alfa del texto ni de
                // la seleccion. Conserva la cache, geometria y paginacion.
                // La clave se actualiza al cambiar el fondo, reutilizando su atributo nativo.
                GpuColorKey();
                if (dx != 0f && hornoAnt != null)
                    { Rectangle d = GpuSlideVertical ? new Rectangle(0, (int)(dx-slideDesde), w, h) : new Rectangle((int)(dx-slideDesde), 0, w, h); Imagenes.Pinta(g, hornoAnt, new SKRect(0, 0, w, h), new SKRect(d.Left, d.Top, d.Right, d.Bottom), filtro, medio, copia, Envoltura.Transparente, null, claveGpu, claveGpu); }   // EN LINUX: g.DrawImage(hornoAnt, caja, 0, 0, w, h, Pixel, claveGpu): con la clave de color; hornoAnt (Form1.Vistas.cs) ya es un SKBitmap: directo
                { Rectangle d = GpuSlideVertical ? new Rectangle(0, (int)dx, w, h) : new Rectangle((int)dx, 0, w, h); Imagenes.Pinta(g, hornoPag, new SKRect(0, 0, w, h), new SKRect(d.Left, d.Top, d.Right, d.Bottom), filtro, medio, copia, Envoltura.Transparente, null, claveGpu, claveGpu); }   // EN LINUX: g.DrawImage(hornoPag, caja, 0, 0, w, h, Pixel, claveGpu): con la clave de color; hornoPag, directo
            }
            public void RenderOpaqueGpu(SKCanvas g, bool copia, Filtro filtro, bool medio)   // EN LINUX: el lienzo de Skia y lo que pesa aqui del estado del Graphics (copia, filtro y medio pixel); EnsureUiOverlay (Form1.Video.cs) la llama por el puente
            {
                pintandoGpu = true;
                int w = Width, h = Height;
                AseguraHorneada(w, h);
                float d = DesplazaAhora();
                if (d != 0 && hornoAnt != null) { SKBitmap a = hornoAnt; int x = GpuSlideVertical ? 0 : (int)(d-slideDesde), y = GpuSlideVertical ? (int)(d-slideDesde) : 0; Imagenes.Pinta(g, a, new SKRect(0, 0, a.Width, a.Height), new SKRect(x, y, x + a.Width, y + a.Height), filtro, medio, copia); }   // EN LINUX: g.DrawImageUnscaled(hornoAnt, x, y): la pagina que se va, entera en ese punto (hornoAnt, de Form1.Vistas.cs, ya es un SKBitmap)
                { SKBitmap p = hornoPag; int x = GpuSlideVertical ? 0 : (int)d, y = GpuSlideVertical ? (int)d : 0; Imagenes.Pinta(g, p, new SKRect(0, 0, p.Width, p.Height), new SKRect(x, y, x + p.Width, y + p.Height), filtro, medio, copia); }   // EN LINUX: g.DrawImageUnscaled(hornoPag, x, y)
            }
        }
        private void DrawSongPagerGpu(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio)   // EN LINUX: el lienzo de Skia y el estado del Graphics (suavizado, modo de letra, copia y medio pixel), como parametros; EnsureUiOverlay (Form1.Video.cs, aun sin traducir) la llama con su Graphics por el puente (Plataforma/Puente.acabado-estilo.cs)
        {
            Rectangle r = songPager.Bounds;
            int pages = songPager.Pages, page = songPager.Page;
            if (pages <= 1 || r.Width < 60) return;
            float size = Math.Max(12f, Math.Min(20f, r.Height * .43f));
            using (Rocola.Nativo.Letra font = Rocola.Nativo.Letra.EnPixeles(Letras.SinRemates, size, true))   // EN LINUX: new Font(GenericSansSerif, size, Bold, Pixel): la generica sin remates de XP (Microsoft Sans Serif), en pixeles y en negrita (Nativo/Letras.cs)
            using (SKPaint text = new SKPaint { Color = Color.FromArgb(236, 242, 255).Sk() })   // EN LINUX: SolidBrush
            using (SKPaint dim = new SKPaint { Color = Color.FromArgb(180, 200, 230).Sk() })   // EN LINUX: SolidBrush
            using (SKPaint body = new SKPaint { Color = Color.FromArgb(125, 16, 24, 45).Sk() })   // EN LINUX: SolidBrush
            using (SKPaint rim = new SKPaint { Color = Color.FromArgb(110, Lighten(bgAccentTarget, .45)).Sk(), StrokeWidth = 1f })   // EN LINUX: Pen(color): la pluma de 1 de ancho (Pixel.Traza le pone lo de GDI+)
            // EN LINUX: using (StringFormat sf = new StringFormat()): el formato es un FormatoTexto, que no se suelta (abajo, dentro)
            {
                FormatoTexto sf = new FormatoTexto(); sf.Horizontal = Alinea.Centro; sf.Vertical = Alinea.Centro;   // EN LINUX: new StringFormat() (Nativo/TextoGdiPlus.cs); Alignment y LineAlignment = Center
                sf.Banderas = BanderasTexto.SinPartir; sf.Corte = CorteTexto.PuntosPorLetra;   // EN LINUX: FormatFlags = NoWrap (deja solo esa) y Trimming = EllipsisCharacter
                int cw = Math.Min(r.Width, Math.Max(170, (int)(size * 13)));
                Rectangle center = new Rectangle(r.X + (r.Width - cw) / 2, r.Y + 2, cw, Math.Max(12, r.Height - 4));
                using (Rocola.Dibujo2D.GraphicsPath p = RoundedPath(center, Math.Min(15, center.Height / 2)))   // EN LINUX: RoundedPath (Form1.Fondo.cs, aun sin traducir) devuelve un GraphicsPath: se pinta su SKPath (PuenteDibujo.Ruta: el mismo, sin copia)
                { Pixel.Rellena(g, PuenteDibujo.Ruta(p), body, suave, medio, copia); Pixel.Traza(g, PuenteDibujo.Ruta(p), rim, suave, medio, copia); }   // EN LINUX: g.FillPath(body, p); g.DrawPath(rim, p) (Nativo/Pixel.cs)
                TextoGdiPlus.Escribe(g, "PÁGINA  " + (page + 1) + " / " + pages, font, ajuste, text, center, sf, copia, medio);   // EN LINUX: g.DrawString(texto, letra, pincel, caja, formato) (Nativo/TextoGdiPlus.cs)
                int side = Math.Max(0, (r.Width - cw) / 2 - 12);
                if (side >= 90)
                {
                    int cy = r.Top + r.Height / 2, arrow = (int)size;
                    Color yellow = Color.FromArgb(242, 206, 72);
                    CtrlDirLeft(PuenteDibujo.GraphicsAcabado(g, suave, ajuste, copia, medio), r.Left + 8, cy, arrow, page > 0 ? yellow : Color.FromArgb(80, yellow));   // EN LINUX: CtrlDirLeft (Form1.Pared.cs, aun sin traducir) pinta con un Graphics: uno sobre el mismo lienzo con el estado de este, por el puente (Plataforma/Puente.acabado-estilo.cs)
                    CtrlDirRight(PuenteDibujo.GraphicsAcabado(g, suave, ajuste, copia, medio), r.Right - 8 - arrow, cy, arrow, page + 1 < pages ? yellow : Color.FromArgb(80, yellow));   // EN LINUX: CtrlDirRight (Form1.Pared.cs, aun sin traducir), igual
                    sf.Horizontal = Alinea.Cerca;   // EN LINUX: Alignment = Near
                    if (page > 0) TextoGdiPlus.Escribe(g, "ANTERIOR", font, ajuste, dim, new Rectangle(r.Left + 16 + arrow, r.Top, side - arrow - 8, r.Height), sf, copia, medio);   // EN LINUX: g.DrawString(...)
                    sf.Horizontal = Alinea.Lejos;   // EN LINUX: Alignment = Far
                    if (page + 1 < pages) TextoGdiPlus.Escribe(g, "SIGUIENTE", font, ajuste, dim, new Rectangle(r.Right - side - 8, r.Top, side - arrow - 8, r.Height), sf, copia, medio);   // EN LINUX: g.DrawString(...)
                }
            }
        }
        private bool FastSongGlass { get { return gpuPanel != null && gpuPanel.GlassPieces; } }
        private bool CanUseSongGlass
        {
            get { return glassCapas && cancionesCristal && cancionesTransparentes && !searchOpen &&
                panelSongs != null && panelSongs.Visible && songList != null &&
                songList.Width >= 4 && songList.Height >= 4; }
        }
        private int EnsureSongPiece(int index)
        {
            Rectangle rect = new Rectangle(panelSongs.Left + songList.Left, panelSongs.Top + songList.Top,
                songList.Width, songList.Height);
            bool empty = (index == 3 && !songList.GpuPreviousReady) || index >= 4;   // la 4 es del buscador con portadas
            if (index == 2)
            {
                empty = songList.Sel < songList.PageStart || songList.Sel >= songList.PageStart + songList.RowsPerPage ||
                    songList.Sel >= songItems.Count;
                rect.Y += (songList.Sel - songList.PageStart) * songList.RowH;
                rect.Height = songList.RowH;
            }
            if (empty)
            {
                if (uiPieza[index] != null) { uiPieza[index].Dispose(); uiPieza[index] = null; uiPiezaVer[index]++; }
                uiPiezaRect[index] = Rectangle.Empty; uiPiezaSig[index] = SigNada;
                return uiPiezaVer[index];
            }
            long sig = Mix(Mix(Mix(SigBase, 'G'), index), songList.Width);
            sig = Mix(sig, songList.Height); sig = Mix(sig, textosDeslizantes);
            if (index == 3)
            {
                sig = Mix(sig, songList.GpuSlideToken); sig = Mix(sig, songList.PageStart);
            }
            else if (index != 0)
            {
                sig = Mix(sig, songList.GpuLayerSignature()); sig = Mix(sig, colaVer); sig = Mix(sig, generoVer);
                if (index == 2) { Color accent = songList.Accent; sig = Mix(sig, songList.Sel); sig = Mix(sig, accent.ToArgb()); }
            }
            uiPiezaRect[index] = rect; // mover una capa no obliga a volver a subir sus pixeles
            if (uiPieza[index] != null && uiPiezaSig[index] == sig) return uiPiezaVer[index];
            SKBitmap next;   // EN LINUX: las tres capas se hacen como SKBitmap (abajo, BakeSongGlassTint, BakeGpuPrevious y BakeGpuListLayer)
            if (index == 0) next = BakeSongGlassTint(songList.Width, songList.Height, true);
            else if (index == 3) next = songList.BakeGpuPrevious();
            else using (CapturaTexto(index == 1 ? 2 : 3)) next = songList.BakeGpuListLayer(index == 2);
            if (uiPieza[index] != null) uiPieza[index].Dispose();
            uiPieza[index] = PuenteDibujo.Entrega(next, Rocola.Dibujo2D.PixelFormat.Format32bppPArgb); uiPiezaSig[index] = sig;   // EN LINUX: las piezas (uiPieza, de Form1.Video.cs) se las da como Bitmap a la tarjeta codigo aun sin traducir: el mapa se le entrega por el puente, con el formato con que XP lo hacia (Format32bppPArgb); el Bitmap se hace su dueno (al soltar la pieza se sueltan sus puntos)
            return ++uiPiezaVer[index];
        }
        internal static SKBitmap BakeSongGlass(int w, int h)   // EN LINUX: un SKBitmap
        { return BakeSongGlassTint(w, h, false); }
        internal static SKBitmap BakeSongGlassTint(int w, int h, bool neutral)   // EN LINUX: un SKBitmap (lo entregan EnsureSongPiece a la tarjeta y RenderTransparentGpuTint al dibujo)
        {
            SKBitmap b = Mapas.Nuevo(w, h);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
            using (SKCanvas g = new SKCanvas(b))   // EN LINUX: Graphics.FromImage(b): un lienzo nuevo (estado de fabrica; el suavizado, abajo)
            using (Rocola.Dibujo2D.GraphicsPath edge = RoundedPath(new Rectangle(1, 1, w - 3, h - 3), Math.Min(28, Math.Min(w, h) / 4)))   // EN LINUX: RoundedPath (Form1.Fondo.cs, aun sin traducir) devuelve un GraphicsPath: se pinta su SKPath (PuenteDibujo.Ruta: el mismo, sin copia)
            using (Rocola.Dibujo2D.Brush cuerpo = DegradadoV(new Rectangle(0, 0, w, h),   // EN LINUX: DegradadoV (PulidoGpu.cs, aun sin traducir) devuelve un Brush de GDI+ (un degradado o, con la interfaz tramada, una textura): su SKPaint, abajo
                neutral ? Color.FromArgb(205, 55, 55, 55) : Color.FromArgb(205, 26, 37, 59),
                neutral ? Color.FromArgb(218, 24, 24, 24) : Color.FromArgb(218, 9, 16, 31))) using (SKPaint body = PuenteDibujo.Pincel(cuerpo, false)) using (body.Shader)   // C4 (PulidoGpu.cs)   // EN LINUX: el pincel de GDI+ en un SKPaint (su shader, con el medio pixel de este lienzo: el de fabrica), por el puente; el shader se suelta con el
            using (SKShader brillo = Degradado.Lineal(new Rectangle(0, 0, w, h),   // EN LINUX: new LinearGradientBrush(caja, a, b, 115): su shader (Nativo/Degradado.cs) en un SKPaint (abajo)
                neutral ? Color.FromArgb(36, 255, 255, 255) : Color.FromArgb(36, 210, 230, 255), Color.FromArgb(0, 210, 230, 255), 115f)) using (SKPaint sheen = new SKPaint { Shader = brillo })   // EN LINUX: el pincel del degradado
            using (SKPaint rim = new SKPaint { Color = (neutral ? Color.FromArgb(120, 255, 255, 255) : Color.FromArgb(96, 193, 222, 255)).Sk(), StrokeWidth = 1.3f })   // EN LINUX: Pen(color, 1,3)
            {
                bool suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias: el estado del dibujo va en una variable (el medio pixel y la copia, los de fabrica)
                Pixel.Rellena(g, PuenteDibujo.Ruta(edge), body, suave); Pixel.Rellena(g, PuenteDibujo.Ruta(edge), sheen, suave); Pixel.Traza(g, PuenteDibujo.Ruta(edge), rim, suave);   // EN LINUX: g.FillPath(body, edge); g.FillPath(sheen, edge); g.DrawPath(rim, edge) (Nativo/Pixel.cs)
            }
            return b;
        }
        private partial class SearchOverlay
        {
            // LOS DOS NUMEROS SALEN DE UN SOLO SITIO. Antes esto rehacia a mano el reparto del
            // buscador -el 128 escrito tal cual en vez de M+70+28, y el margen otra vez como 30- en un
            // archivo distinto del que lo decide. Coincidian, pero nada los ataba: el dia que se toque
            // el margen o el alto de la cabecera se mueve todo menos este trozo, y no avisa nadie.
            private void ExtraPreviewGeometry(out Rectangle region, out Rectangle area)
            {
                region = RegionTeclado();
                area = CajaPortada(region);
            }
            public bool ExtraGpuRect(int kind, out Rectangle rect)
            {
                rect = Rectangle.Empty;
                if (!SeparateGpu || Results == null || Results.Count == 0) return false;
                int selected = FocusResults ? SelResult : 0;
                if (selected < 0 || selected >= Results.Count) return false;
                if (kind == 2)
                {
                    if (selected >= resultRects.Count) return false;
                    rect = Rectangle.Inflate(Rectangle.Round(resultRects[selected].Key), MargenTarj, MargenTarj);
                    return true;
                }
                if (kind == 3 && FocusResults && GetCover != null && Results[selected].Kind != 3)
                {
                    Rectangle region, area; ExtraPreviewGeometry(out region, out area);
                    int top = area.Bottom + 10;
                    rect = new Rectangle(region.X, top, region.Width, Math.Max(1, region.Bottom-top+4));
                    return region.Width > 0 && region.Height > 0;
                }
                return false;
            }
            public void DrawExtraGpu(Rocola.Dibujo2D.Graphics g, int kind, int dx, int dy)   // EN LINUX: no dibuja: pasa el Graphics de DibujaPiezaViva (Form1.Buscador.cs) a DrawOneResultCard y DrawPreviewInfo (Form1.Buscador.cs), los tres aun sin traducir; el tipo, escrito entero (sin el using de GDI+) hasta que se traduzca ese archivo
            {
                if (!SeparateGpu || Results == null || Results.Count == 0) return;
                if (kind == 2)
                {
                    int selected = FocusResults ? SelResult : 0;
                    if (selected < 0 || selected >= resultRects.Count) return;
                    Rectangle r = Rectangle.Round(resultRects[selected].Key); r.Offset(dx, dy);
                    DrawOneResultCard(g, selected, r, true, Normalize(Query ?? ""));
                }
                else if (kind == 3 && FocusResults)
                {
                    Rectangle region, area; ExtraPreviewGeometry(out region, out area);
                    region.Offset(dx, dy); area.Offset(dx, dy);
                    DrawPreviewInfo(g, region, area);
                }
            }
        }
    }
}
#endif
