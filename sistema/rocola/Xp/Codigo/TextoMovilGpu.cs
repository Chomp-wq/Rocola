#if ROCOLA_GPU
using System;
using System.Collections.Generic;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: las cintas del texto que se desliza y el texto que se mueve dibujan con SkiaSharp directo y las funciones nativas (Xp/Nativo), no con GDI+

namespace Rocola
{
    internal sealed class CintaTexto : IDisposable
    {
        internal string Key, Identity;
        internal RectangleF Rect;
        internal SKBitmap[] Tiles;   // EN LINUX: los mapas de la cinta son SKBitmap (a la tarjeta le llegan prestados: GpuPanel.SubeCinta)
        internal int Width, Born;
        private bool desplazamientoListo;
        private int desplazamientoTick, desplazamientoBorn;
        private float desplazamientoExceso, desplazamientoValor;
        internal float Desplazamiento(int now, float anchoVisible)
        {
            float exceso = Math.Max(0, Width - anchoVisible);
            if (!desplazamientoListo || now != desplazamientoTick || Born != desplazamientoBorn || exceso != desplazamientoExceso)
            {
                desplazamientoValor = GpuPanel.CorreTitulo((uint)(now - Born), exceso);
                desplazamientoTick = now; desplazamientoBorn = Born; desplazamientoExceso = exceso;
                desplazamientoListo = true;
            }
            return desplazamientoValor;
        }
        public void Dispose() { if (Tiles != null) foreach (SKBitmap b in Tiles) b.Dispose(); }   // EN LINUX: SKBitmap
    }
    internal sealed class CintasTexto : IDisposable
    {
        internal sealed class Lista : System.Collections.ObjectModel.Collection<CintaTexto>
        {
            internal long Version;
            protected override void InsertItem(int index, CintaTexto item)
            { base.InsertItem(index, item); unchecked { Version++; } }
            protected override void RemoveItem(int index)
            { base.RemoveItem(index); unchecked { Version++; } }
            protected override void SetItem(int index, CintaTexto item)
            { base.SetItem(index, item); unchecked { Version++; } }
            protected override void ClearItems()
            { base.ClearItems(); unchecked { Version++; } }
        }
        internal readonly Lista Items = new Lista();
        // Solo cambia al sustituir, ordenar o retirar cintas. Su posicion y reloj se
        // leen del objeto vivo al dibujar; moverlo no requiere rehacer la lista GPU.
        internal long Version { get { return Items.Version; } }
        private int used, pixels;
        internal void Begin() { used = 0; pixels = 0; }
        internal void End()
        {
            while (Items.Count > used) { int i = Items.Count - 1; Items[i].Dispose(); Items.RemoveAt(i); }
        }
        internal bool Add(SKCanvas source, AjusteLetra ajuste, string text, Rocola.Nativo.Letra font, Color color, RectangleF rect, FormatoTexto format)   // EN LINUX: el lienzo de Skia y su modo de letra (lo unico del estado del Graphics que se usa aqui; el recorte y la matriz van en el lienzo), una Letra y un FormatoTexto
        {
            if (string.IsNullOrEmpty(text) || rect.Width < 12 || rect.Height < 8 || used >= 48
                || !rect.IntersectsWith(RecorteLienzo.Limites(source))) return false;   // EN LINUX: VisibleClipBounds: los limites exactos del recorte (Nativo/RecorteLienzo.cs)
            FormatoTexto sf = new FormatoTexto(format);   // EN LINUX: using (StringFormat sf = (StringFormat)format.Clone()): una copia (un FormatoTexto, que no se suelta: lo de abajo queda como bloque)
            {
                sf.Banderas |= BanderasTexto.SinPartir;   // EN LINUX: FormatFlags |= NoWrap
                sf.Corte = CorteTexto.Nada; sf.Horizontal = Alinea.Cerca;   // EN LINUX: Trimming = None; Alignment = Near
                float measured = TextoGdiPlus.Mide(text, font, ajuste, new SizeF(100000, rect.Height), sf).Width;   // EN LINUX: source.MeasureString (Nativo/TextoGdiPlus.cs) con el modo de letra del lienzo
                if (measured <= rect.Width - 1) return false;
                int width = (int)Math.Ceiling(measured) + 4, height = (int)Math.Ceiling(rect.Height);
                // Limite de memoria ante metadatos corruptos; las tiras caben incluso en texturas de 1024.
                if (width > 16384 || height > 256 || width * height > 2097152 - pixels) return false;
                pixels += width * height;
                PointF[] points = { new PointF(rect.X, rect.Y) };
                for (int k = 0; k < points.Length; k++) { SKPoint q = source.TotalMatrix.MapPoint(points[k].X, points[k].Y); points[k] = new PointF(q.X, q.Y); }   // EN LINUX: source.Transform y TransformPoints: la matriz del lienzo (la del HDC es la identidad: las cintas solo se apuntan al hornear mapas de bits)
                RectangleF target = new RectangleF(points[0].X, points[0].Y, rect.Width, rect.Height);
                string identity = text + "|" + font.Familia + "|" + font.Tamano + "|" + ((font.Negrita ? 1 : 0) | (font.Cursiva ? 2 : 0) | (font.Subrayada ? 4 : 0)) + "|" + (font.UnidadPixel ? 2 : 3)   // EN LINUX: Name, Size, (int)Style y (int)Unit de la Font: los de la Letra (Bold 1, Italic 2, Underline 4; Pixel 2, Point 3, las unidades que XP usa)
                    + "|" + rect.Width + "|" + height + "|" + (int)sf.Vertical;   // EN LINUX: (int)sf.LineAlignment
                string key = identity + "|" + color.ToArgb();
                CintaTexto run = used < Items.Count ? Items[used] : null;
                // El orden cambia si otro titulo empieza/deja de desbordarse. Conservar la cinta
                // por identidad evita hornear otra vez las que solo cambiaron de ranura.
                if (run == null || run.Key != key)
                    for (int i = used + 1; i < Items.Count; i++)
                        if (Items[i].Key == key)
                        {
                            run = Items[i]; Items.RemoveAt(i); Items.Insert(used, run); break;
                        }
                if (run == null || run.Key != key)
                {
                    CintaTexto prior = null;
                    for (int i = used; i < Items.Count; i++)
                        if (Items[i].Identity == identity) { prior = Items[i]; break; }
                    CintaTexto next = new CintaTexto { Key = key, Identity = identity, Width = width,
                        Born = prior != null ? prior.Born : Environment.TickCount,
                        Tiles = new SKBitmap[(width + 1023) / 1024] };   // EN LINUX: SKBitmap
                    try
                    {
                        for (int i = 0; i < next.Tiles.Length; i++)
                        {
                            SKBitmap b = Mapas.Nuevo(Math.Min(1024, width - i * 1024), height);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
                            next.Tiles[i] = b;
                            using (SKCanvas g = new SKCanvas(b))   // EN LINUX: Graphics.FromImage
                            using (SKPaint brush = new SKPaint { Color = color.Sk() })   // EN LINUX: SolidBrush
                            {
                                g.Clear(Color.Transparent.Sk());   // EN LINUX: Graphics.Clear
                                AjusteLetra ajusteTira = AjusteLetra.SuaveAjustada;   // EN LINUX: g.TextRenderingHint = AntiAliasGridFit, en una variable (la copia y el medio pixel, los de fabrica del Graphics nuevo)
                                g.Translate(-i * 1024, 0);   // EN LINUX: TranslateTransform
                                TextoGdiPlus.Escribe(g, text, font, ajusteTira, brush, new RectangleF(0, 0, width, height), sf, false, false);   // EN LINUX: g.DrawString (Nativo/TextoGdiPlus.cs)
                            }
                        }
                    }
                    catch { foreach (SKBitmap b in next.Tiles) if (b != null) b.Dispose(); throw; }   // EN LINUX: SKBitmap
                    if (prior != null) { Items.Remove(prior); prior.Dispose(); }
                    Items.Insert(used, next);
                    run = next;
                }
                run.Rect = target; used++;
                return true;
            }
        }
        public void Dispose()
        {
            foreach (CintaTexto r in Items) r.Dispose();
            if (Items.Count > 0) Items.Clear();
            used = 0;
        }
    }
    public partial class Form1
    {
        [ThreadStatic] private static CintasTexto capturandoCintas;
        private CintasTexto[] cintasTexto;
        private sealed class CapturaCintas : IDisposable
        {
            private CintasTexto before, current;
            internal CapturaCintas(CintasTexto group)
            { before = capturandoCintas; current = group; if (group != null) group.Begin(); capturandoCintas = group; }
            public void Dispose() { if (current != null) current.End(); capturandoCintas = before; }
        }
        private IDisposable CapturaTexto(int group)
        {
            if (cintasTexto == null) cintasTexto = new CintasTexto[5];
            if (cintasTexto[group] == null) cintasTexto[group] = new CintasTexto();
            if (!textosDeslizantes || !gpuEnabled) { cintasTexto[group].Dispose(); return new CapturaCintas(null); }
            return new CapturaCintas(cintasTexto[group]);
        }
        internal static bool CintaVistaPrevia(SKCanvas g, AjusteLetra ajuste, string text, Rocola.Nativo.Letra font, Color color, Rectangle rect)   // EN LINUX: el lienzo de Skia y su modo de letra, y una Letra; la llama directo DrawPreviewInfo (Form1.Buscador.cs, ya traducido)
        {
            FormatoTexto sf = new FormatoTexto();   // EN LINUX: using (StringFormat sf = new StringFormat()): un FormatoTexto, que no se suelta (lo de abajo queda como bloque)
            {
                sf.Banderas = BanderasTexto.SinPartir;   // EN LINUX: FormatFlags = NoWrap
                return TextoConCinta(g, ajuste, text, font, color, rect, sf);   // EN LINUX: con el lienzo y su modo de letra
            }
        }
        internal static void PintaTextoMovil(SKCanvas g, AjusteLetra ajuste, bool copia, bool medio, string text, Rocola.Nativo.Letra f, SKPaint brush, RectangleF rect, FormatoTexto sf)   // EN LINUX: el lienzo de Skia y el estado del texto del que llama (modo de letra, copia y medio pixel), una Letra, el pincel en un SKPaint y un FormatoTexto; quien aun pasa un Graphics llega por el puente (Plataforma/Puente.reposo.cs)
        {
            SKPaint solid = brush != null && brush.Shader == null ? brush : null;   // EN LINUX: brush as SolidBrush: un pincel de un color es un SKPaint sin Shader (los degradados y las texturas lo llevan)
            if (solid == null || !TextoConCinta(g, ajuste, text, f, ColorSk.DeSk(solid.Color), rect, sf)) TextoGdiPlus.Escribe(g, text, f, ajuste, brush, rect, sf, copia, medio);   // EN LINUX: solid.Color, el de .NET; g.DrawString (Nativo/TextoGdiPlus.cs) con el estado del que llama
        }
        private void PintaArtistaCompacto(SKCanvas g, AjusteLetra ajuste, bool copia, bool medio)   // EN LINUX: el lienzo de Skia y el estado del texto del que llama (modo de letra, copia y medio pixel); quien aun pasa un Graphics llega por el puente (Plataforma/Puente.reposo.cs)
        {
            if (lblNow == null) return;
            Rectangle r = lblNow.Bounds;
            int right = r.Right;
            if (pastGenero != null && pastGenero.Visible) right = Math.Min(right, pastGenero.Left - 8);
            if (lblMejorada != null && lblMejorada.Visible) right = Math.Min(right, lblMejorada.Left - 8);
            bool alta; Rectangle next = CajaSiguienteForma(out alta);
            if (alta) right = Math.Min(right, next.Left - 16);   // la tarjeta baja va por encima del renglon: no lo corta
            r.Width = Math.Max(0, right - r.Left);
            FormatoTexto sf = new FormatoTexto();   // EN LINUX: using (StringFormat sf = new StringFormat()): un FormatoTexto, que no se suelta (lo de abajo queda como bloque)
            {
                sf.Banderas = BanderasTexto.SinPartir;   // EN LINUX: FormatFlags = NoWrap
                ContentAlignment align = lblNow.TextAlign;
                sf.Vertical = (align & (ContentAlignment.MiddleLeft | ContentAlignment.MiddleCenter | ContentAlignment.MiddleRight)) != 0   // EN LINUX: sf.LineAlignment
                    ? Alinea.Centro : (align & (ContentAlignment.BottomLeft | ContentAlignment.BottomCenter | ContentAlignment.BottomRight)) != 0   // EN LINUX: StringAlignment.Center
                    ? Alinea.Lejos : Alinea.Cerca;   // EN LINUX: StringAlignment.Far y Near
                if (TextoConCinta(g, ajuste, lblNow.Text, PuenteDibujo.Letra(lblNow.Font), lblNow.ForeColor, r, sf)) return;   // EN LINUX: con el modo de letra del lienzo; la letra de la etiqueta (WinForms, aun sin traducir), su Letra por el puente
                // Si no cabe, con sus puntos suspensivos donde se acaba SU sitio (r), como el titulo. Antes se recortaba la
                // etiqueta entera con un SetClip y la ultima palabra quedaba partida a la mitad (barrido del 12-10-2026).
                if (TextoGdiPlus.Mide(lblNow.Text, PuenteDibujo.Letra(lblNow.Font), ajuste, SizeF.Empty, null).Width > r.Width)   // EN LINUX: g.MeasureString sin caja ni formato (Nativo/TextoGdiPlus.cs)
                    PintaRenglonLejosReposo(g, ajuste, copia, medio, lblNow.Text, lblNow.Font, lblNow.ForeColor, r, Alinea.Cerca);   // EN LINUX: PintaRenglonLejos (EstiloGpu.cs, aun sin traducir) pinta con un Graphics: por el puente (Plataforma/Puente.reposo.cs)
                else DrawLabelOverlay(g, ajuste, copia, medio, lblNow, lblNow.ForeColor, r);   // EN LINUX: DrawLabelOverlay (Form1.Video.cs, ya traducida) con el lienzo y lo que pesa del estado en el texto
            }
        }
        internal static bool TextoConCinta(SKCanvas g, AjusteLetra ajuste, string text, Rocola.Nativo.Letra f, Color color, RectangleF rect, FormatoTexto sf)   // EN LINUX: el lienzo de Skia y su modo de letra, una Letra y un FormatoTexto; quien aun pasa un Graphics llega por el puente (Plataforma/Puente.reposo.cs)
        { return capturandoCintas != null && capturandoCintas.Add(g, ajuste, text, f, color, rect, sf); }   // EN LINUX: con el modo de letra del lienzo
        private void SueltaCintasTexto()
        {
            if (cintasTexto != null) foreach (CintasTexto group in cintasTexto) if (group != null) group.Dispose();
            cintasTexto = null;
        }
    }
    internal sealed partial class GpuPanel
    {
        internal Func<CintasTexto[]> GetCintasTexto;
        private sealed class CintaTile
        {
            internal IntPtr Tex;
            internal SKBitmap Source;   // EN LINUX: SKBitmap (el de la cinta)
            internal CintaTexto Run;
            internal int W, H, Group, Part;
            internal float U, V;
            internal bool Seen;
        }
        private readonly List<CintaTile> cintaTiles = new List<CintaTile>();
        private readonly Dictionary<SKBitmap, CintaTile> cintaByBitmap = new Dictionary<SKBitmap, CintaTile>();   // EN LINUX: SKBitmap (por identidad, como el Bitmap de XP)
        private readonly List<SKBitmap> cintaOldSources = new List<SKBitmap>();   // EN LINUX: SKBitmap
        private readonly List<IntPtr> cintaRetiradas = new List<IntPtr>();
        private readonly List<CintaTile> cintaPendientes = new List<CintaTile>();
        private CintasTexto[] cintaGrupos;
        private long[] cintaVersiones;
        private bool cintaListaValida;
        internal int CintasListasCalculadas, CintasListasReusadas;
        private readonly Tarjeta.Vtx[] cintaQuad = new Tarjeta.Vtx[6];
        private bool MismaListaCintas(CintasTexto[] groups)
        {
            int n = groups != null ? groups.Length : 0;
            if (!cintaListaValida || cintaGrupos.Length != n) return false;
            for (int i = 0; i < n; i++)
                if (!object.ReferenceEquals(groups[i], cintaGrupos[i])
                    || (groups[i] != null && groups[i].Version != cintaVersiones[i])) return false;
            return true;
        }
        private void ActualizaListaCintas(CintasTexto[] groups)
        {
            // Publicar la firma al acabar: una excepcion no deja una lista parcial valida.
            cintaListaValida = false;
            cintaTiles.Clear(); cintaPendientes.Clear();
            foreach (CintaTile tile in cintaByBitmap.Values) tile.Seen = false;
            if (groups != null)
                for (int group = 0; group < groups.Length; group++)
                {
                    if (groups[group] == null) continue;
                    foreach (CintaTexto run in groups[group].Items)
                        for (int part = 0; part < run.Tiles.Length; part++)
                        {
                            CintaTile tile;
                            SKBitmap source = run.Tiles[part];   // EN LINUX: SKBitmap
                            if (!cintaByBitmap.TryGetValue(source, out tile))
                            {
                                tile = new CintaTile(); tile.Source = source; cintaByBitmap.Add(source, tile);
                            }
                            tile.Run = run; tile.Group = group; tile.Part = part; tile.Seen = true;
                            cintaTiles.Add(tile);
                            if (tile.Tex == IntPtr.Zero) cintaPendientes.Add(tile);
                        }
                }
            cintaOldSources.Clear();
            foreach (KeyValuePair<SKBitmap, CintaTile> kv in cintaByBitmap)   // EN LINUX: SKBitmap
                if (!kv.Value.Seen) cintaOldSources.Add(kv.Key);
            foreach (SKBitmap source in cintaOldSources)   // EN LINUX: SKBitmap
            {
                CintaTile tile = cintaByBitmap[source];
                if (tile.Tex != IntPtr.Zero) cintaRetiradas.Add(tile.Tex);
                cintaByBitmap.Remove(source);
            }
            cintaOldSources.Clear();
            int n = groups != null ? groups.Length : 0;
            if (cintaGrupos == null || cintaGrupos.Length != n)
            { cintaGrupos = new CintasTexto[n]; cintaVersiones = new long[n]; }
            for (int i = 0; i < n; i++)
            { cintaGrupos[i] = groups[i]; cintaVersiones[i] = groups[i] != null ? groups[i].Version : 0; }
            cintaListaValida = true; CintasListasCalculadas++;
        }
        private void PreparaCintasTexto()
        {
            CintasTexto[] groups = GetCintasTexto != null ? GetCintasTexto() : null;
            if (!MismaListaCintas(groups)) ActualizaListaCintas(groups);
            else CintasListasReusadas++;
            // En reposo, las texturas ya listas no necesitan consultas de visibilidad,
            // diccionario ni reloj de presupuesto. El dibujo sigue leyendo Rect/Born.
            if (cintaPendientes.Count == 0 && cintaRetiradas.Count == 0) return;
            // Lo legible nunca espera al presupuesto. Solo se aplazan las partes fuera de la
            // ventana del texto; se preparan con margen antes de que el desplazamiento las muestre.
            int now = Environment.TickCount;
            foreach (CintaTile tile in cintaPendientes)
                if (tile.Tex == IntPtr.Zero && CintaVisible(tile, now)) SubeCinta(tile);
            long inicio = System.Diagnostics.Stopwatch.GetTimestamp();
            long presupuesto = Math.Min(CargaTrabajo.PresupuestoGpu(CargaTrabajo.Procesadores, TopeFpsReal, lastFrameWork),
                (long)(System.Diagnostics.Stopwatch.Frequency * .00035));
            foreach (CintaTile tile in cintaPendientes)
            {
                if (tile.Tex != IntPtr.Zero) continue;
                if (System.Diagnostics.Stopwatch.GetTimestamp() - inicio >= presupuesto) break;
                if (CintaGrupoProximo(tile.Group)) SubeCinta(tile);
            }
            // Una subida fallida sigue pendiente y se reintenta al cuadro siguiente.
            for (int i = cintaPendientes.Count - 1; i >= 0; i--)
                if (cintaPendientes[i].Tex != IntPtr.Zero) cintaPendientes.RemoveAt(i);
            inicio = System.Diagnostics.Stopwatch.GetTimestamp();
            while (cintaRetiradas.Count > 0)
            {
                int i = cintaRetiradas.Count - 1;
                Tarjeta.Suelta(cintaRetiradas[i]); cintaRetiradas.RemoveAt(i);
                if (cintaRetiradas.Count <= 256 && System.Diagnostics.Stopwatch.GetTimestamp() - inicio >= presupuesto) break;
            }
        }
        // No subir partes de una barra completamente oculta. Al comenzar el fundido se
        // prepara su grupo; lo visible conserva siempre su subida inmediata y sin presupuesto.
        private bool CintaGrupoProximo(int group)
        {
            if (group == 0) return idleBlend < 1f || !IdleExpandTarget;
            if (group == 1) return idleBlend > 0f || IdleExpandTarget;
            return CintaDeLista(group);
        }
        private void SubeCinta(CintaTile tile)
        { tile.Tex = Upload(tile.Tex, ref tile.W, ref tile.H, PuenteDibujo.Bitmap(tile.Source), out tile.U, out tile.V); }   // EN LINUX: Upload (D3D9Star.cs, aun sin traducir) pide un Bitmap: el mismo prestado para el mismo mapa (Plataforma/Puente.cs; no suelta los puntos)

        private bool CintaVisible(CintaTile tile, int now)
        {
            bool lista = tile.Group >= 2;
            if (lista && tile.Group != 4 && GetPiezaRect == null) return false;
            if (lista && !CintaDeLista(tile.Group)) return false;
            float alpha = lista ? SceneAlpha() * ControlsOpacity : tile.Group == 0 ? 1 - idleBlend : idleBlend;
            if (alpha <= .003f) return false;
            CintaTexto run = tile.Run; RectangleF r = run.Rect;
            float left = 0, right = r.Width;
            if (lista)
            {
                Rectangle origin = tile.Group == 4 ? OrigenVistaPrevia() : GetPiezaRect(tile.Group == 2 ? 1 : 2);
                if (origin.IsEmpty) return false;
                if (tile.Group == 2)
                {
                    Rectangle selected = GetPiezaRect(2);
                    float rowY = origin.Y + r.Y + r.Height * .5f;
                    if (!selected.IsEmpty && rowY >= selected.Top && rowY < selected.Bottom) return false;
                }
                float x = origin.X + r.X + (tile.Group == 4 || songFrameVertical ? 0 : songFrameSlide);
                float y = origin.Y + r.Y + (tile.Group != 4 && songFrameVertical ? songFrameSlide : 0);
                RectangleF clip = RectangleF.Intersect(new RectangleF(x, y, r.Width, r.Height), tile.Group == 4 ? origin : GetPiezaRect(0));
                if (clip.IsEmpty || (tile.Group == 4 && AlfaVistaPrevia(origin) <= .003f)) return false;
                left = clip.Left - x; right = clip.Right - x;
            }
            float off = run.Width <= 1024
                ? CorreTitulo((uint)(now - run.Born), Math.Max(0, run.Width - r.Width))
                : run.Desplazamiento(now, r.Width);
            float start = tile.Part * 1024 - off;
            return start < right + 32 && start + tile.Source.Width > left - 32;
        }
        private void DibujaCintasTexto(bool lista)
        {
            int now = Environment.TickCount;
            foreach (CintaTile tile in cintaTiles)
            {
                if ((tile.Group >= 2) != lista) continue;
                if (lista && !CintaDeLista(tile.Group)) continue;   // (BuscadorPortadasGpu.cs: lo de siempre, y con la ventana del buscador)
                float alpha = lista ? SceneAlpha() * ControlsOpacity : tile.Group == 0 ? 1 - idleBlend : idleBlend;
                if (alpha <= .003f || tile.Tex == IntPtr.Zero) continue;
                CintaTexto run = tile.Run; RectangleF r = run.Rect;
                float x = r.X, y = r.Y + (tile.Group == 0 ? Height - uiBotH : StatusTopY);
                RectangleF clip = new RectangleF(x, y, r.Width, r.Height);
                if (lista)
                {
                    Rectangle origin = tile.Group == 4 ? OrigenVistaPrevia() : GetPiezaRect(tile.Group == 2 ? 1 : 2);
                    if (origin.IsEmpty) continue;
                    if (tile.Group == 2)
                    {
                        Rectangle selected = GetPiezaRect(2);
                        float rowY = origin.Y + r.Y + r.Height * .5f;
                        if (!selected.IsEmpty && rowY >= selected.Top && rowY < selected.Bottom) continue;
                    }
                    x = origin.X + r.X + (tile.Group == 4 || songFrameVertical ? 0 : songFrameSlide);
                    y = origin.Y + r.Y + (tile.Group != 4 && songFrameVertical ? songFrameSlide : 0);
                    clip = RectangleF.Intersect(new RectangleF(x, y, r.Width, r.Height), tile.Group == 4 ? origin : GetPiezaRect(0));
                    if (tile.Group == 4) alpha = AlfaVistaPrevia(origin);
                    if (clip.IsEmpty) continue;
                }
                float off = run.Width <= 1024
                ? CorreTitulo((uint)(now - run.Born), Math.Max(0, run.Width - r.Width))
                : run.Desplazamiento(now, r.Width);
                float start = tile.Part * 1024 - off;
                float left = Math.Max(clip.Left - x, start), right = Math.Min(clip.Right - x, start + tile.Source.Width);
                if (right <= left) continue;
                float u0 = (left - start) / tile.Source.Width * tile.U;
                float u1 = (right - start) / tile.Source.Width * tile.U;
                float v0 = (clip.Top - y) / r.Height * tile.V, v1 = (clip.Bottom - y) / r.Height * tile.V;
                SetTexQuadA(cintaQuad, x + left, clip.Top, x + right, clip.Bottom, tile.U, tile.V, ((uint)(alpha * 255) << 24) | 0xFFFFFFu);
                for (int k = 0; k < cintaQuad.Length; k++)
                {
                    cintaQuad[k].U = cintaQuad[k].U == 0 ? u0 : u1;
                    cintaQuad[k].V = cintaQuad[k].V == 0 ? v0 : v1;
                }
                stars.DrawQuad(tile.Tex, cintaQuad);
            }
        }
        private void SueltaCintasGpu()
        {
            foreach (CintaTile t in cintaByBitmap.Values) if (t.Tex != IntPtr.Zero) Tarjeta.Suelta(t.Tex);
            foreach (IntPtr tex in cintaRetiradas) Tarjeta.Suelta(tex);
            cintaRetiradas.Clear();
            cintaByBitmap.Clear(); cintaTiles.Clear(); cintaOldSources.Clear();
            cintaPendientes.Clear(); cintaGrupos = null; cintaVersiones = null; cintaListaValida = false;
        }
    }
}
#endif
