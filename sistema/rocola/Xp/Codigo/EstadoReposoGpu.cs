#if ROCOLA_GPU
using System;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: el panel de reposo y su reloj dibujan con SkiaSharp directo y las funciones nativas (Xp/Nativo), no con GDI+
using System.Text;

namespace Rocola
{
    // El texto solo cambia al pasar de segundo, aunque la fuente se sigue leyendo en cada cuadro.
    // Se conserva TextoFaltan como unica autoridad del formato y del limite de fin de cancion.
    internal sealed class CacheRelojReposo
    {
        // El lector activo ya pertenece a Form1. La cache no debe retener un lector cerrado
        // (ni su MP3 en memoria) si el panel deja de pedir el reloj al terminar la musica.
        private WeakReference fuente;
        private string texto;
        private string signoNegativo;
        private long segundoActual, segundoTotal;
        private int minutoActual, minutoTotal;

        internal void Vaciar() { if (fuente != null) fuente.Target = null; texto = null; }

        internal string Texto(object origen, TimeSpan actual, TimeSpan total, string siNo)
        {
            if (total.TotalSeconds <= 0 || actual.TotalSeconds < 0)
            { Vaciar(); return siNo; }
            if (actual > total) actual = total;
            long sa = actual.Ticks / TimeSpan.TicksPerSecond, st = total.Ticks / TimeSpan.TicksPerSecond;
            // Fmt usa TotalMinutes: conservar tambien esa cuenta, incluso para duraciones extremas
            // donde el double puede redondear cerca del siguiente minuto antes que los ticks.
            int ma = (int)actual.TotalMinutes, mt = (int)total.TotalMinutes;
            string signo = ma < 0 || mt < 0 ? System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NegativeSign : null;
            if (texto != null && fuente != null && ReferenceEquals(fuente.Target, origen) && sa == segundoActual && st == segundoTotal &&
                ma == minutoActual && mt == minutoTotal && signo == signoNegativo) return texto;
            string nuevo = Form1.TextoFaltan(actual, total, siNo);
            if (fuente == null) fuente = new WeakReference(origen); else fuente.Target = origen;
            segundoActual = sa; segundoTotal = st; minutoActual = ma; minutoTotal = mt; signoNegativo = signo; texto = nuevo;
            return nuevo;
        }
    }

    public partial class Form1
    {
        private SKBitmap idlePanelBmp;   // EN LINUX: SKBitmap (a la tarjeta, que aun lo pide como Bitmap, le llega prestado: BuildIdlePanel)
        private string idlePanelSig;
        private SKBitmap idleClockUnderlay;   // EN LINUX: SKBitmap
        private string idlePanelTime;
        private int idlePanelRevision;
        private int idlePanelProgressStart;
        private CacheRelojReposo idleClockTexto;

        private string TextoRelojReposoCache(IAudioSource r)
        {
            string corto = (lblTime != null) ? lblTime.Text : "";
            if (!suenaGrande || r == null || nowPlaying == null || nowPlaying.IsVideo)
            { if (idleClockTexto != null) idleClockTexto.Vaciar(); return corto; }
            TimeSpan cur, tot;
            try { cur = r.CurrentTime; tot = r.TotalTime; }
            catch { if (idleClockTexto != null) idleClockTexto.Vaciar(); return corto; }
            if (idleClockTexto == null) idleClockTexto = new CacheRelojReposo();
            return idleClockTexto.Texto(r, cur, tot, corto);
        }

        // EL PANEL SE REHACE CUANDO ALGO CAMBIA, NO CADA 400 ms MIRANDO A VER.
        //
        // Antes se rearmaba la firma -titulo, artista, y para cada una de las doce de la cola su ruta, su
        // texto y su caratula- dos veces y media por segundo, para casi siempre descubrir que nada habia
        // cambiado. No costaba casi nada -0,005 ms, medido con doce en la cola-, pero tenia un precio
        // peor que el del procesador: un cambio tardaba hasta 400 ms en verse. Pones una cancion y el
        // panel va detras. Ese, y no el ahorro, es el motivo de este cambio.
        //
        // Ahora cada sitio que cambia algo del panel avisa, y la firma solo se rearma cuando hay aviso.
        // LA FIRMA SE QUEDA como segundo filtro, y eso es a proposito: un aviso de mas cuesta 0,005 ms y
        // no un redibujo de 1,70. O sea que avisar de sobra sale barato y lo unico caro es olvidarse, que
        // es justo al reves de como suele salir esto. Lo vigila la bateria 'panelreposo': cambia cada cosa
        // que el panel ensena, una por una, y exige que se entere.
        private int idlePanelDatos;
        private int idlePanelDatosDe = -1;
        private Color idlePanelAcento = Color.Empty;
        partial void CambioElPanelDeReposo() { unchecked { idlePanelDatos++; } }

        // EL COLOR DEL GENERO ES OTRA COSA: no cambia, SE DESLIZA. En cada latido -24 por segundo- se
        // acerca un 16% al color de la cancion nueva, y tarda unos 24 latidos en llegar. Avisar en cuanto
        // se mueve un punto seria rehacer el panel en los 24 seguidos: 41 ms, medido.
        //
        // Asi que avisa cuando se ha movido lo bastante para VERSE. Seis de 255 es un 2%: por debajo de
        // eso, dos paneles seguidos son el mismo panel para cualquiera que lo mire. Con el umbral la
        // transicion baja a 14 redibujos, 24 ms.
        //
        // Y AUN ASI ESTO SALE MAS CARO QUE ANTES, que hay que decirlo: con el reloj de los 400 ms la
        // misma transicion costaba unos 3 redibujos -5 ms-, porque la mayoria de los latidos ni se
        // miraban. Son 19 ms de mas UNA VEZ POR CANCION -cada tres o cuatro minutos- a cambio de que se
        // vea al momento en vez de hasta 400 ms tarde. El numero lo vuelve a sacar la bateria
        // 'panelreposo' cada vez que corre, para que no se pierda.
        private const int AcentoPaso = 6;
        partial void CambioElAcentoDelPanel(Color nuevo)
        {
            if (estadoColorDirecto) nuevo = bgAccentTarget;
            if (idlePanelAcento != Color.Empty
                && Math.Abs(nuevo.R - idlePanelAcento.R) < AcentoPaso
                && Math.Abs(nuevo.G - idlePanelAcento.G) < AcentoPaso
                && Math.Abs(nuevo.B - idlePanelAcento.B) < AcentoPaso) return;
            idlePanelAcento = nuevo;
            CambioElPanelDeReposo();
        }
        // Las precargas de otras paginas no cambian las portaditas del estado ni de su cola.
        private bool PortadasVisiblesEnCola(string[] keys, int maximum)
        {
            int count = Math.Min(maximum, queue.Count);
            for (int i = 0; i < count; i++)
            {
                Album disc = queue[i] != null ? queue[i].Disc : null;
                if (disc == null || string.IsNullOrEmpty(disc.CoverPath)) continue;
                for (int k = 0; k < keys.Length; k++)
                    if (string.Equals(keys[k], disc.CoverPath, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
        private Rocola.Dibujo2D.Bitmap BuildIdlePanel()   // EN LINUX: lo pide GetIdlePanel (D3D9Star.cs, aun con Bitmap): el mapa le llega prestado (PuenteDibujo.Bitmap, el MISMO Bitmap para el mismo mapa: la tarjeta compara por identidad para no volver a subirlo)
        {
            if (nowPlaying == null || videoOn) return null;
            if (gpuPanel != null && idlePanelBmp != null) gpuPanel.IdleProgressStart = idlePanelProgressStart;
            int w = ClientSize.Width, h = GpuPanel.IdlePanelHeight(ClientSize.Height);
            if (w < 480 || h < 140) return null;
            if (idlePanelBmp != null && idlePanelBmp.Width == w && idlePanelBmp.Height == h &&
                idlePanelDatos == idlePanelDatosDe)
            {
                // Nada de lo que el panel ensena ha cambiado. Lo unico que corre solo es el reloj, y ese
                // se parchea aparte sin rehacer nada mas.
                ParcheaElReloj(w, h);
                return PuenteDibujo.Bitmap(idlePanelBmp);   // EN LINUX: prestado (Plataforma/Puente.cs; no suelta los puntos)
            }
            idlePanelDatosDe = idlePanelDatos;
            Color acento = estadoColorDirecto ? bgAccentTarget : bgAccent;
            string title, artist; nowPlaying.TituloArtista(out title, out artist);
            title = TituloSinPista(title);
            string[] qt = new string[Math.Min(colaDoce ? 12 : 3, queue.Count)], qa = new string[qt.Length];
            SKBitmap[] covers = new SKBitmap[qt.Length];   // EN LINUX: las portadas, SKBitmap
            StringBuilder sig = new StringBuilder();
            sig.Append(w).Append('|').Append(h).Append('|').Append(nowPlaying.PathKey()).Append('|')
                .Append(title).Append('|').Append(artist).Append('|').Append(queue.Count).Append('|').Append(acento.ToArgb());
            string time = TextoRelojReposoCache(reader);   // mismo formato de SuenaVivoGpu.cs, sin volver a formar cadenas por cuadro
            sig.Append('|').Append(estadoTransparente).Append('|').Append(EstadoOpacoActivo).Append('|').Append(iaBadgeKind);
            for (int i = 0; i < qt.Length; i++)
            {
                queue[i].TituloArtista(out qt[i], out qa[i]);
                qt[i] = TituloSinPista(qt[i]);
                covers[i] = queue[i].Disc != null ? LoadCoverAsync(queue[i].Disc) : null;
                sig.Append('|').Append(queue[i].PathKey()).Append('|').Append(qt[i]).Append('|').Append(qa[i])
                    .Append('|').Append(covers[i] != null ? covers[i].GetHashCode() : 0);
            }
            sig.Append('|').Append(colaDoce).Append('|').Append(textosDeslizantes).Append('|').Append(InterfazTramada);
            // Lo que suena, en grande (SuenaVivoGpu.cs): el genero y lo que falta para cada una de la cola.
            ReposoGrande extra = ExtraReposo(w, h, qt.Length);
            if (extra != null)
            {
                sig.Append("|G|").Append(extra.Genero).Append('|').Append(extra.GeneroColor.ToArgb());
                for (int i = 0; i < extra.Minutos.Length; i++) sig.Append('|').Append(extra.Minutos[i]);
            }
            string key = sig.ToString();
            if (idlePanelBmp != null && key == idlePanelSig) { ParcheaElReloj(w, h); return PuenteDibujo.Bitmap(idlePanelBmp); }   // EN LINUX: prestado
            int progressStart;
            SKBitmap next;   // EN LINUX: SKBitmap
            using (CapturaTexto(1))
                next = RenderIdlePanelEn(idlePanelBmp, w, h, title, artist, "", qt, qa, covers, queue.Count, acento,
                    estadoTransparente && !EstadoOpacoActivo, iaBadgeKind, colaDoce, extra, EstadoOpacoActivo, out progressStart);
            reposoUltimo = extra;
            if (extra != null) { extra.Titulo = title; extra.W = w; extra.H = h; }   // el titulo que se desliza lo hornea la tarjeta al pedirlo
            if (idleClockUnderlay == null || idleClockUnderlay.Width != w)
            {
                if (idleClockUnderlay != null) idleClockUnderlay.Dispose();
                idleClockUnderlay = Mapas.Nuevo(w, 34);   // EN LINUX: new Bitmap(w, 34, Format32bppPArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
            }
            CopiaRegionReposo(next, new Rectangle(0, h - 34, w, 34), idleClockUnderlay, Point.Empty);
            progressStart = UpdateIdleClock(next, idleClockUnderlay, time);
            idlePanelTime = time; idlePanelRevision++;
            if (gpuPanel != null) { gpuPanel.IdlePanelVersion = idlePanelRevision; gpuPanel.IdlePanelDirty = Rectangle.Empty; }
            idlePanelProgressStart = progressStart;
            if (gpuPanel != null) gpuPanel.IdleProgressStart = progressStart;
            SKBitmap old = idlePanelBmp; idlePanelBmp = next; idlePanelSig = key;   // EN LINUX: SKBitmap
            if (old != null && !ReferenceEquals(old, next)) old.Dispose();
            return PuenteDibujo.Bitmap(idlePanelBmp);   // EN LINUX: prestado
        }

        internal static int DrawIdleClock(SKCanvas g, AjusteLetra ajuste, int w, int h, string time)   // EN LINUX: el lienzo de Skia y su modo de letra (la copia y el medio pixel son los de fabrica: ninguno de los que la llaman los cambia)
        {
            float scale = Math.Max(.8f, Math.Min(1.3f, h / 260f));
            using (Rocola.Nativo.Letra clock = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Tahoma"), 17 * scale, true))   // EN LINUX: new Font("Tahoma", px, Bold, Pixel): la familia como la resuelve GDI+ (Nativo/Letras.cs)
            using (SKPaint white = new SKPaint { Color = Color.FromArgb(240, 245, 255).Sk() })   // EN LINUX: SolidBrush
            {   // EN LINUX: using (StringFormat format = new StringFormat()): el formato es un FormatoTexto, que no se suelta (va dentro)
                FormatoTexto format = new FormatoTexto();   // EN LINUX: new StringFormat() (Nativo/TextoGdiPlus.cs)
                format.Corte = CorteTexto.PuntosPorLetra;   // EN LINUX: Trimming = EllipsisCharacter
                format.Banderas = BanderasTexto.SinPartir;   // EN LINUX: FormatFlags = NoWrap
                format.Horizontal = Alinea.Cerca; format.Vertical = Alinea.Centro;   // EN LINUX: Alignment = Near; LineAlignment = Center
                int x = GpuPanel.IdleCoverRect(w, h).Right + 16;
                int width = (int)Math.Ceiling(TextoGdiPlus.Mide(time ?? "", clock, ajuste, new SizeF(int.MaxValue, 999999f), format).Width) + 2;   // EN LINUX: g.MeasureString(texto, letra, ancho, formato): en una caja de ese ancho y 999999 de alto (Nativo/TextoGdiPlus.cs)
                width = Math.Max(1, Math.Min(width, w - x - 112));
                TextoGdiPlus.Escribe(g, time ?? "", clock, ajuste, white, new RectangleF(x, h - 32, width, 28), format, false, false);   // EN LINUX: g.DrawString (Nativo/TextoGdiPlus.cs)
                return x + width + 8;
            }
        }
        // SOLO EL RELOJ. Es lo unico del panel que cambia sin que nadie lo toque, asi que no entra en el
        // sistema de avisos: se mira su texto -una comparacion de cadenas- y si cambio se repinta la franja
        // de abajo y nada mas. Rehacer el panel entero por un digito costaria 1,70 ms cada segundo.
        private void ParcheaElReloj(int w, int h)
        {
            string time = TextoRelojReposoCache(reader);
            if (idleClockUnderlay == null || idlePanelBmp == null || idlePanelTime == time) return;
            Rectangle zona;
            idlePanelProgressStart = UpdateIdleClockRegion(idlePanelBmp, idleClockUnderlay, time, idlePanelProgressStart, out zona);
            idlePanelTime = time; idlePanelRevision++;
            if (gpuPanel != null)
            {
                gpuPanel.IdlePanelVersion = idlePanelRevision;
                gpuPanel.IdlePanelDirty = zona;
                gpuPanel.IdleProgressStart = idlePanelProgressStart;
            }
        }

        // Solo los digitos, con margen para el antialias y para borrar un texto anterior mas largo.
        internal static int UpdateIdleClockRegion(SKBitmap panel, SKBitmap underlay, string time, int previousRight, out Rectangle dirty)   // EN LINUX: SKBitmap; PrecargaPaginaGpu.cs, ya traducido, la llama directo
        {
            float scale = Math.Max(.8f, Math.Min(1.3f, panel.Height / 260f));
            int x = GpuPanel.IdleCoverRect(panel.Width, panel.Height).Right + 16;
            // EN LINUX: using (Graphics g = Graphics.FromImage(panel)): ese Graphics solo mide, y medir no pide lienzo (TextoGdiPlus.Mide); su modo de letra va en una variable
            using (Rocola.Nativo.Letra font = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Tahoma"), 17 * scale, true))   // EN LINUX: new Font("Tahoma", px, Bold, Pixel) (Nativo/Letras.cs)
            {   // EN LINUX: using (StringFormat sf = new StringFormat()): el formato es un FormatoTexto, que no se suelta (va dentro)
                FormatoTexto sf = new FormatoTexto();   // EN LINUX: new StringFormat()
                sf.Banderas = BanderasTexto.SinPartir;   // EN LINUX: FormatFlags = NoWrap
                AjusteLetra ajuste = AjusteLetra.SuaveAjustada;   // EN LINUX: g.TextRenderingHint = AntiAliasGridFit, en una variable
                int width = (int)Math.Ceiling(TextoGdiPlus.Mide(time ?? "", font, ajuste, new SizeF(int.MaxValue, 999999f), sf).Width) + 2;   // EN LINUX: g.MeasureString(texto, letra, ancho, formato) (Nativo/TextoGdiPlus.cs)
                width = Math.Max(1, Math.Min(width, panel.Width - x - 112));
                int right = Math.Max(previousRight, x + width + 8) + 3;
                dirty = Rectangle.Intersect(new Rectangle(0, 0, panel.Width, panel.Height),
                    new Rectangle(Math.Max(0, x - 3), panel.Height - 34, Math.Max(1, right - x + 3), 34));
            }
            CopiaRegionReposo(underlay, new Rectangle(dirty.X, dirty.Y - panel.Height + underlay.Height, dirty.Width, dirty.Height), panel, dirty.Location);
            using (SKCanvas g = new SKCanvas(panel))   // EN LINUX: Graphics.FromImage
            {
                g.Save(); g.ClipRect(new SKRect(dirty.Left, dirty.Top, dirty.Right, dirty.Bottom), SKClipOperation.Intersect, false);   // EN LINUX: g.SetClip(dirty): un recorte de pixeles enteros, sin suavizar, en un Save del lienzo como lo pone la capa (el Graphics nuevo no traia recorte que reemplazar)
                // EN LINUX: g.SmoothingMode = AntiAlias: el reloj solo escribe, y al texto no le toca el suavizado (TextoGdiPlus.Escribe no lo lleva)
                AjusteLetra ajuste = AjusteLetra.SuaveAjustada;   // EN LINUX: g.TextRenderingHint = AntiAliasGridFit, en una variable
                return DrawIdleClock(g, ajuste, panel.Width, panel.Height, time);   // EN LINUX: con el lienzo y su modo de letra
            }
        }

        [ThreadStatic] private static byte[] filaReposo;
        internal static void CopiaRegionReposo(SKBitmap source, Rectangle area, SKBitmap target, Point position)   // EN LINUX: SKBitmap; PrecargaPaginaGpu.cs, ya traducido, la llama directo
        {
            int bytes = checked(area.Width * 4);
            if (filaReposo == null || filaReposo.Length < bytes) filaReposo = new byte[bytes];
            IntPtr srcScan0 = new IntPtr(source.GetPixels().ToInt64() + (long)area.Y * source.RowBytes + (long)area.X * 4); int srcStride = source.RowBytes;   // EN LINUX: source.LockBits(area, ReadOnly, Format32bppPArgb): un SKBitmap guarda los puntos asi (premultiplicados): los mismos, desde la esquina del trozo y con su paso
            // EN LINUX: try: lo leido son los puntos mismos, no hay copia que soltar (abajo, sin finally)
            {
                IntPtr dstScan0 = new IntPtr(target.GetPixels().ToInt64() + (long)position.Y * target.RowBytes + (long)position.X * 4); int dstStride = target.RowBytes;   // EN LINUX: target.LockBits(zona, WriteOnly, Format32bppPArgb): los puntos mismos del mapa
                try
                {
                    for (int y = 0; y < area.Height; y++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(new IntPtr(srcScan0.ToInt64() + (long)y * srcStride), filaReposo, 0, bytes);   // EN LINUX: src.Scan0 y src.Stride
                        System.Runtime.InteropServices.Marshal.Copy(filaReposo, 0, new IntPtr(dstScan0.ToInt64() + (long)y * dstStride), bytes);   // EN LINUX: dst.Scan0 y dst.Stride
                    }
                }
                finally { target.NotifyPixelsChanged(); }   // EN LINUX: target.UnlockBits(dst): Skia se entera de que cambiaron los puntos
            }
            // EN LINUX: finally { source.UnlockBits(src); }: lo leido eran los puntos mismos, nada que devolver
        }

        internal static int UpdateIdleClock(SKBitmap panel, SKBitmap underlay, string time)   // EN LINUX: SKBitmap; PrecargaPaginaGpu.cs, ya traducido, la llama directo
        {
            CopiaRegionReposo(underlay, new Rectangle(0, 0, underlay.Width, underlay.Height), panel, new Point(0, panel.Height - underlay.Height));
            using (SKCanvas g = new SKCanvas(panel))   // EN LINUX: Graphics.FromImage
            {
                // EN LINUX: g.SmoothingMode = AntiAlias: el reloj solo escribe, y al texto no le toca el suavizado (TextoGdiPlus.Escribe no lo lleva)
                AjusteLetra ajuste = AjusteLetra.SuaveAjustada;   // EN LINUX: g.TextRenderingHint = AntiAliasGridFit, en una variable
                return DrawIdleClock(g, ajuste, panel.Width, panel.Height, time);   // EN LINUX: con el lienzo y su modo de letra
            }
        }

        // Textura exclusiva GPU: sus textos no se escalan durante la animacion.
        // El cubo actual se dibuja aparte como geometria y crece con el panel.

        internal static Rectangle IdleQueueCell(int w, int h, int index, bool dozen)
        { return IdleQueueCellCount(w, h, index, dozen ? 12 : 3, dozen); }

        internal static Rectangle IdleQueueCellCount(int w, int h, int index, int count, bool dozen)
        {
            int split = (int)(w * .52f), visible = Math.Max(1, Math.Min(count, 12));
            int cols = !dozen ? 1 : w >= 1500 ? Math.Min(3, visible) :
                visible <= 3 ? 1 : visible <= 8 && h >= 260 ? 2 : visible <= 6 ? 2 : h >= 280 ? 2 : 3;
            int rows = dozen ? (visible + cols - 1) / cols : 3, gap = 8;
            int cw = (w - split - 24 - (cols - 1) * gap) / cols;
            int rh = (h - 78) / rows;
            // Una cola corta no debe estirar las tarjetas hasta llenar la pantalla.
            // El limite vale tambien para una o dos canciones en pantallas pequenas:
            // estirar esa unica fila separaba la miniatura (centrada) del texto (arriba).
            // Solo se limita; nunca se agrandan las filas de una cola larga.
            if (dozen) { cw = Math.Min(cw, 520); rh = Math.Min(rh, 84); }
            return new Rectangle(split + (index % cols) * (cw + gap), 42 + (index / cols) * rh, cw, rh - 5);
        }

        internal static Rectangle IdleQueueThumb(Rectangle cell)
        {
            int side = Math.Max(12, Math.Min(Math.Min(cell.Height - 6, cell.Width / 3), 64));
            return new Rectangle(cell.X + 5, cell.Y + (cell.Height - side) / 2, side, side);
        }

        internal static RectangleF IdleQueueTextBlock(Rectangle cell, float titleHeight, float detailHeight, int numberWidth)
        {
            Rectangle thumb = IdleQueueThumb(cell);
            int x = thumb.Right + 5;
            int width = Math.Max(12, cell.Right - x - (cell.Width < 180 ? 5 : numberWidth + 8));
            float height = titleHeight + detailHeight;
            return new RectangleF(x, cell.Y + Math.Max(2f, (cell.Height - height) * .5f), width, height);
        }

        internal static RectangleF IdleQueueNumberRect(Rectangle cell, float numberHeight)
        {
            Rectangle thumb = IdleQueueThumb(cell);
            float height = Math.Min(thumb.Height, (float)Math.Ceiling(numberHeight) + 2f);
            return new RectangleF(thumb.Left, thumb.Bottom - height, thumb.Width, height);
        }

        internal static SKBitmap RenderIdlePanelQueue(int w, int h, string title, string artist, string time,   // EN LINUX: un SKBitmap
            string[] qt, string[] qa, SKBitmap[] covers, int count, Color accent, bool transparent, int badgeKind, bool dozen)   // EN LINUX: las portadas, SKBitmap
        {
            int progressStart;
            return RenderIdlePanelLayout(w, h, title, artist, time, qt, qa, covers, count, accent, transparent, badgeKind, dozen, out progressStart);
        }

        internal static SKBitmap RenderIdlePanelLayout(int w, int h, string title, string artist, string time,   // EN LINUX: un SKBitmap
            string[] qt, string[] qa, SKBitmap[] covers, int count, Color accent, bool transparent, int badgeKind, bool dozen, out int progressStart)   // EN LINUX: las portadas, SKBitmap
        { return RenderIdlePanelGrande(w, h, title, artist, time, qt, qa, covers, count, accent, transparent, badgeKind, dozen, null, out progressStart); }

        // La letra del titulo del panel (tambien la del titulo que se desliza, SuenaVivoGpu.cs).
        internal static Rocola.Nativo.Letra LetraTituloReposo(int w, int h)   // EN LINUX: FuenteTituloReposo, con una Letra (Nativo/Letras.cs) en lugar de la Font; la llaman directo este archivo y SuenaVivoGpu.cs (ya traducido), que se hace dueno de la que pide
        {
            float scale = Math.Max(.8f, Math.Min(1.3f, h / 260f));
            return Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), Math.Max(22, Math.Min(36 * scale, w / 30f)), true);   // EN LINUX: new Font("Trebuchet MS", px, Bold, Pixel)
        }

        // 'extra' es lo de "Lo que suena, en grande" (SuenaVivoGpu.cs); con null, el panel de siempre.
        internal static SKBitmap RenderIdlePanelGrande(int w, int h, string title, string artist, string time,   // EN LINUX: un SKBitmap
            string[] qt, string[] qa, SKBitmap[] covers, int count, Color accent, bool transparent, int badgeKind, bool dozen, ReposoGrande extra, out int progressStart)   // EN LINUX: las portadas, SKBitmap
        { return RenderIdlePanelEn(null, w, h, title, artist, time, qt, qa, covers, count, accent, transparent, badgeKind, dozen, extra, false, out progressStart); }

        internal static SKBitmap RenderIdlePanelEn(SKBitmap bitmap, int w, int h, string title, string artist, string time,   // EN LINUX: un SKBitmap; PrecargaPaginaGpu.cs, ya traducido, la llama directo
            string[] qt, string[] qa, SKBitmap[] covers, int count, Color accent, bool transparent, int badgeKind, bool dozen,   // EN LINUX: las portadas, SKBitmap
            ReposoGrande extra, bool opaque, out int progressStart)
        {
            float scale = Math.Max(.8f, Math.Min(1.3f, h / 260f));
            if (bitmap == null || bitmap.Width != w || bitmap.Height != h) bitmap = Mapas.Nuevo(w, h);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
            using (SKCanvas g = new SKCanvas(bitmap))   // EN LINUX: Graphics.FromImage
            using (SKShader degradado = DegradadoV(new Rectangle(0, 0, w, h),   // EN LINUX: using (Brush bg = DegradadoV(...)): DegradadoV (PulidoGpu.cs, ya traducido) da el shader del pincel, con el medio pixel de este lienzo (el de fabrica: false), y se suelta como el Brush en XP...
                Color.FromArgb(opaque ? 255 : transparent ? 0 : 232, 12, 19, 42), Color.FromArgb(opaque ? 255 : transparent ? 0 : 248, 7, 12, 29), false)) using (SKPaint bg = new SKPaint { Shader = degradado })   // C4 (PulidoGpu.cs)   // EN LINUX: ...en un SKPaint (el color de fabrica, negro opaco, como el que le ponia el pincel de GDI+: el shader manda)
            using (SKPaint top = new SKPaint { Color = Color.FromArgb(155, Lighten(accent, .45)).Sk(), StrokeWidth = 2 })   // EN LINUX: Pen(color, 2)
            using (Rocola.Nativo.Letra small = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), 15 * scale, true))   // EN LINUX: new Font("Trebuchet MS", px, Bold, Pixel) (Nativo/Letras.cs)
            using (Rocola.Nativo.Letra normal = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), 20 * scale, true))   // EN LINUX: new Font("Trebuchet MS", px, Bold, Pixel)
            using (Rocola.Nativo.Letra big = LetraTituloReposo(w, h))   // EN LINUX: FuenteTituloReposo, con una Letra
            using (SKPaint white = new SKPaint { Color = Color.FromArgb(240, 245, 255).Sk() })   // EN LINUX: SolidBrush
            using (SKPaint muted = new SKPaint { Color = Color.FromArgb(157, 179, 209).Sk() })   // EN LINUX: SolidBrush
            using (SKPaint color = new SKPaint { Color = Lighten(accent, .5).Sk() })   // EN LINUX: SolidBrush
            using (SKPaint card = new SKPaint { Color = Color.FromArgb(transparent ? 65 : 160, 27, 40, 67).Sk() })   // EN LINUX: SolidBrush
            // EN LINUX: using (StringFormat line = new StringFormat()): un FormatoTexto, que no se suelta (va dentro)
            // EN LINUX: using (StringFormat wrap = new StringFormat()): igual
            // EN LINUX: using (StringFormat numberFormat = StringFormat.GenericTypographic): el tipografico COMPARTIDO (no se suelta: tampoco en GDI+)
            {   FormatoTexto line = new FormatoTexto(), wrap = new FormatoTexto(), numberFormat = FormatoTexto.TipograficoCompartido;   // EN LINUX: los tres formatos; numberFormat es el MISMO objeto que StringFormat.GenericTypographic del codigo sin traducir (Nativo/TextoGdiPlus.cs)
                g.Clear(Color.Transparent.Sk()); // el mapa se reutiliza: no acumular texto/alfa de la cancion anterior   // EN LINUX: Graphics.Clear
                bool suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias, en una variable (la copia y el medio pixel son los de fabrica: abajo van como false)
                AjusteLetra ajuste = AjusteLetra.SuaveAjustada;   // EN LINUX: g.TextRenderingHint = AntiAliasGridFit, en una variable
                line.Corte = CorteTexto.PuntosPorLetra; line.Banderas = BanderasTexto.SinPartir;   // EN LINUX: Trimming = EllipsisCharacter; FormatFlags = NoWrap
                wrap.Corte = CorteTexto.PuntosPorPalabra; wrap.Banderas = BanderasTexto.SoloEnteros;   // EN LINUX: Trimming = EllipsisWord; FormatFlags = LineLimit
                Pixel.Rellena(g, new RectangleF(0, 0, w, h), bg, suave, false, false);   // EN LINUX: g.FillRectangle (Nativo/Pixel.cs)
                Pixel.Linea(g, 0, GpuPanel.StatusEdgeOffset, w, GpuPanel.StatusEdgeOffset, top, suave, false, false);   // EN LINUX: g.DrawLine (Nativo/Pixel.cs)
                int split = (int)(w * .52f);
                Rectangle cover = GpuPanel.IdleCoverRect(w, h);
                // Sin tarjeta bajo el cubo: se ve directamente el fondo del estado.
                int tx = cover.Right + 20, tw = Math.Max(20, split - tx - 24);
                TextoGdiPlus.Escribe(g, "A H O R A   S U E N A", small, ajuste, color, new RectangleF(24, 12, split - 40, 26 * scale), line, false, false);   // EN LINUX: g.DrawString (Nativo/TextoGdiPlus.cs)
                float titleH = Math.Min(90, h * .28f), artistY = 56 + titleH + 6;
                if (extra == null)
                {
                    TextoGdiPlus.Escribe(g, title ?? "", big, ajuste, white, new RectangleF(tx, 56, tw, titleH), wrap, false, false);   // EN LINUX: g.DrawString
                    PintaTextoMovil(g, ajuste, false, false, artist ?? "", normal, color, new RectangleF(tx, artistY, tw, Math.Max(22, h - 82 - artistY)), line);   // EN LINUX: con el lienzo y su estado
                }
                else PintaTituloGrande(g, suave, ajuste, false, false, extra, title, artist, big, normal, small, white, color, line, tx, tw);   // en una linea   // EN LINUX: PintaTituloGrande (SuenaVivoGpu.cs, ya traducido), directo, con el lienzo y su estado, las mismas Letra y los mismos pinceles (de un color: SolidBrush) y formato
                if (badgeKind != 0)
                {
                    Rectangle badge = GpuPanel.IdleBadgeRect(w, h);
                    Color metal = badgeKind == 2 ? Color.FromArgb(255, 221, 132) : Color.FromArgb(218, 237, 255);
                    using (SKPaint tint = new SKPaint { Color = Color.FromArgb(42, metal).Sk() })   // EN LINUX: SolidBrush
                    using (SKPaint ink = new SKPaint { Color = metal.Sk() })   // EN LINUX: SolidBrush
                    using (SKPaint rim = new SKPaint { Color = Color.FromArgb(145, metal).Sk(), StrokeWidth = 1f })   // EN LINUX: Pen(color), de 1
                    using (SKPath shape = ContornoRedondo(badge, 8))   // EN LINUX: RoundedPath da un GraphicsPath: aqui su SKPath (ContornoRedondo, Form1.Fondo.cs: el mismo trazo) y se suelta como en XP
                    {
                        Pixel.Rellena(g, shape, tint, suave, false, false); Pixel.Traza(g, shape, rim, suave, false, false);   // EN LINUX: g.FillPath y g.DrawPath (Nativo/Pixel.cs)
                        FormatoTexto centered = new FormatoTexto(line);   // EN LINUX: using (StringFormat centered = new StringFormat(line)): una copia (un FormatoTexto, que no se suelta: lo de abajo queda como bloque)
                        {
                            centered.Horizontal = Alinea.Centro; centered.Vertical = Alinea.Centro;   // EN LINUX: Alignment = Center; LineAlignment = Center
                            TextoGdiPlus.Escribe(g, "MEJORADA", small, ajuste, ink, badge, centered, false, false);   // EN LINUX: g.DrawString
                        }
                    }
                }
                progressStart = DrawIdleClock(g, ajuste, w, h, time);   // EN LINUX: con el lienzo y su modo de letra
                string heading = "A CONTINUACION  /  " + count + (count == 1 ? " CANCION" : " CANCIONES");
                TextoGdiPlus.Escribe(g, heading, small, ajuste, color, new RectangleF(split, 12, w - split - 24, 26 * scale), line, false, false);   // EN LINUX: g.DrawString
                if (count == 0)
                    TextoGdiPlus.Escribe(g, "La siguiente cancion la eliges tu", normal, ajuste, muted, new RectangleF(split + 14, 62, w - split - 45, 64), null, false, false);   // EN LINUX: g.DrawString sin formato (null: el de fabrica)
                Rectangle sample = IdleQueueCellCount(w, h, 0, qt.Length, dozen);
                using (Rocola.Nativo.Letra queueTitle = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), dozen ? Math.Max(9, Math.Min(21 * scale, (sample.Height - 8) * .52f)) : normal.Tamano, true))   // EN LINUX: new Font("Trebuchet MS", px, Bold, Pixel); normal.Size es su Tamano
                using (Rocola.Nativo.Letra queueArtist = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), dozen ? Math.Max(8, Math.Min(16 * scale, (sample.Height - 8) * .40f)) : small.Tamano, true))   // EN LINUX: igual; small.Size es su Tamano
                using (Rocola.Nativo.Letra queueIndex = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), Math.Min(queueArtist.Tamano, IdleQueueThumb(sample).Width / 1.6f), true))   // EN LINUX: igual; queueArtist.Size es su Tamano
                {
                float titleHeight = queueTitle.AltoRenglon, detailHeight = queueArtist.AltoRenglon + 2;   // EN LINUX: GetHeight(g): el alto del renglon (Nativo/Letras.cs, a 96 puntos por pulgada como el Graphics)
                int numberWidth = (int)Math.Ceiling(TextoGdiPlus.Mide("12", queueArtist, ajuste, SizeF.Empty, null).Width) + 4;   // EN LINUX: g.MeasureString sin caja ni formato (Nativo/TextoGdiPlus.cs)
                numberFormat.Horizontal = Alinea.Centro; numberFormat.Vertical = Alinea.Centro;   // EN LINUX: Alignment = Center; LineAlignment = Center, en el tipografico COMPARTIDO: lo heredan los demas, esten traducidos o no, como en XP
                numberFormat.Banderas |= BanderasTexto.SinPartir;   // EN LINUX: FormatFlags |= NoWrap (tambien en el compartido)
                for (int i = 0; i < qt.Length && i < (dozen ? 12 : 3); i++)
                {
                    Rectangle r = IdleQueueCellCount(w, h, i, qt.Length, dozen);
                    using (SKPath shape = ContornoRedondo(r, Math.Min(10, r.Height / 3))) Pixel.Rellena(g, shape, card, suave, false, false);   // EN LINUX: RoundedPath da un GraphicsPath: aqui su SKPath (ContornoRedondo, Form1.Fondo.cs: el mismo trazo) y g.FillPath (Nativo/Pixel.cs)
                    Rectangle thumb = IdleQueueThumb(r);
                    if (i < covers.Length && covers[i] != null) Imagenes.Pinta(g, covers[i], new SKRect(0, 0, covers[i].Width, covers[i].Height), new SKRect(thumb.Left, thumb.Top, thumb.Right, thumb.Bottom), Filtro.Bilineal, false, false);   // EN LINUX: g.DrawImage(imagen, caja) (Nativo/Imagenes.cs) con el filtro de fabrica del Graphics (Bilinear)
                    else Pixel.Rellena(g, thumb, color, suave, false, false);   // EN LINUX: g.FillRectangle
                    bool compact = r.Width < 180;
                    RectangleF text = IdleQueueTextBlock(r, titleHeight, detailHeight, numberWidth);
                    float textX = text.X, textW = text.Width, textY = text.Y;
                    PintaTextoMovil(g, ajuste, false, false, qt[i] ?? "", queueTitle, white, new RectangleF(textX, textY, textW, titleHeight), line);   // EN LINUX: con el lienzo y su estado
                    string enMin = (extra != null) ? TextoMinutosCola(extra, i) : "";   // "en ~6 min" (SuenaVivoGpu.cs)
                    if (enMin.Length == 0)
                        PintaTextoMovil(g, ajuste, false, false, i < qa.Length ? qa[i] ?? "" : "", queueArtist, muted, new RectangleF(textX, textY + titleHeight, textW, detailHeight), line);   // EN LINUX: con el lienzo y su estado
                    else PintaMinutosCola(g, suave, ajuste, false, false, enMin, i < qa.Length ? qa[i] ?? "" : "", queueArtist, muted, compact, textX, textY + titleHeight, textW, detailHeight, line);   // EN LINUX: PintaMinutosCola (SuenaVivoGpu.cs, ya traducido), directo, con el lienzo y su estado, la misma Letra, el mismo pincel y el mismo formato
                    if (compact)
                    {
                        RectangleF label = IdleQueueNumberRect(r, queueIndex.AltoRenglon);   // EN LINUX: GetHeight(g)
                        using (SKPaint shade = new SKPaint { Color = Color.FromArgb(210, 8, 12, 24).Sk() }) Pixel.Rellena(g, label, shade, suave, false, false);   // EN LINUX: SolidBrush y g.FillRectangle
                        TextoGdiPlus.Escribe(g, (i + 1).ToString("00"), queueIndex, ajuste, white, label, numberFormat, false, false);   // EN LINUX: g.DrawString con el tipografico compartido
                    }
                    else TextoGdiPlus.Escribe(g, (i + 1).ToString("00"), queueArtist, ajuste, color, new RectangleF(r.Right - numberWidth - 3, textY, numberWidth, r.Bottom - textY - 2), line, false, false);   // EN LINUX: g.DrawString
                }
                }
            }
            return bitmap;
        }
    }
}
#endif
