#if ROCOLA_GPU
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
namespace Rocola
{
    public partial class Form1
    {
        private uint adjacentPageTick;
        private bool adjacentPageKnown;
        internal static bool PrimePageImmediately(uint now, uint previous, bool known)
        { return !known || unchecked(now-previous)>=400u; }
        private void PrimeNextPage(List<Album> list, int current)
        {
            uint now=unchecked((uint)Environment.TickCount);
            bool prime=PrimePageImmediately(now,adjacentPageTick,adjacentPageKnown);
            adjacentPageTick=now;adjacentPageKnown=true;
            if(!prime) return;
            List<Album> next=NextGpuPage(list,current);
            for(int i=0;i<next.Count;i++) RequestCover(next[i],false,false);
            // BAJA prioridad; solo esta precarga confirmada puede saltarse coversOnHold.
        }
        // ---------------- EL PRECALENTADO DE LA TARJETA (20-10-2026) ----------------
        // PrecalientaUnPoco (Form1.Portadas.cs) calentaba solo los caminos del procesador: el buscador con forGpu=false (que con
        // tarjeta ni se pinta), la lista de un disco sin la marca de la tarjeta y la pantalla de reposo por el procesador. Lo que la
        // tarjeta pinta de verdad la primera vez -el buscador con forGpu=true, la lista dentro del cristal, el panel del estado
        // extendido, la pastilla y la ficha del disco, los globos de la estrella, los avisos- pagaba su primera vez DENTRO de un
        // cuadro: compilar el codigo (JIT), crear las familias de letra y los pinceles, y los horneados que se guardan. Aqui se
        // dibujan una vez, en mapas de bits de usar y tirar y con datos de relleno, y se tiran. No se toca nada de lo que se ve
        // (ni idlePanelBmp, ni el panel del asistente, ni los avisos en pantalla): solo se adelanta el trabajo. Cada paso va en
        // PrecalientaUnPoco, con sus mismas guardas (el turno del DSP, el margen de 28 ms del ultimo cuadro, la quietud).
        internal const int PrecalPrimeroGpu = 3, PrecalUltimoGpu = 6;   // 3 estado extendido, 4 pastilla y ficha, 5 estrella, 6 avisos y moneda

        // El buscador, por el camino de la tarjeta (forGpu = true): es lo que EnsureUiOverlay pinta al abrirlo.
        private bool PrecalientaBuscadorGpu()
        {
            if (searchOverlay == null) return false;
            return EnSeco(searchOverlay, delegate (Graphics g)
            { searchOverlay.RenderContent(g, new Rectangle(0, 0, searchOverlay.Width, searchOverlay.Height), true); });
        }

        // La lista de un disco, con la marca de la tarjeta (la fila elegida dentro del cristal): otro horneado, y otro camino de codigo.
        // La marca se deja como estaba.
        private bool PrecalientaListaGpu()
        {
            if (songList == null) return false;
            bool antes = songList.pintandoGpu;
            songList.pintandoGpu = true;
            try { return EnSeco(songList, delegate (Graphics g) { songList.RenderContent(g); }); }
            finally { songList.pintandoGpu = antes; }
        }

        private bool PrecalientaPiezaGpu(int paso)
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w < 8 || h < 8) return false;   // aun sin tamano: se reintenta
            PrecalientaPiezaGpuEn(paso, w, h, colaDoce, estadoTransparente && !EstadoOpacoActivo, EstadoOpacoActivo, iaBadgeKind);
            return true;
        }

        // Sin estado del formulario, para poder probarlo solo. Todo lo que dibuja va a mapas de bits propios que se sueltan aqui mismo.
        internal static void PrecalientaPiezaGpuEn(int paso, int w, int h, bool doce, bool transparente, bool opaco, int insignia)
        {
            FontFamily fam = FamiliaUi();
            switch (paso)
            {
                case 3:   // el panel del estado extendido y su reloj
                {
                    int ph = GpuPanel.IdlePanelHeight(h);
                    if (w < 480 || ph < 140) return;
                    int n = doce ? 12 : 3;
                    string[] qt = new string[n], qa = new string[n];
                    for (int i = 0; i < n; i++) { qt[i] = "Cancion de relleno " + (i + 1); qa[i] = "Artista de relleno"; }
                    int comienzo, ultimo;
                    using (Bitmap panel = RenderIdlePanelEn(null, w, ph, "Titulo de relleno", "Artista de relleno", "", qt, qa, new Image[n], n,
                        Color.FromArgb(120, 160, 255), transparente, insignia, doce, null, opaco, out comienzo))
                    using (Bitmap debajo = new Bitmap(w, 34, PixelFormat.Format32bppPArgb))
                    {
                        CopiaRegionReposo(panel, new Rectangle(0, ph - 34, w, 34), debajo, Point.Empty);
                        ultimo = UpdateIdleClock(panel, debajo, "0:00 / 0:00");
                        Rectangle zona;
                        UpdateIdleClockRegion(panel, debajo, "0:01 / 3:00", ultimo, out zona);
                    }
                    break;
                }
                case 4:   // la pastilla de "12 canciones" y la ficha rapida del disco
                {
                    int lado = Math.Max(64, Math.Min(256, w / 8));
                    using (Bitmap foco = new Bitmap(AnchoFoco(lado, lado + 24), AltoFoco(lado), PixelFormat.Format32bppPArgb))
                        PintaFoco(foco, "12 canciones \u00B7 38 min", "12 \u00B7 38 min", fam);
                    Size tam = TamanoFicha(Math.Max(120, w / 6), Math.Max(100, (h - 200) / 3));
                    using (Bitmap ficha = new Bitmap(tam.Width, tam.Height, PixelFormat.Format32bppPArgb))
                        PintaFicha(ficha, null, "Artista de relleno", "Disco de relleno", "12 canciones \u00B7 38 min", "Pop", Color.FromArgb(120, 160, 255), 1,
                            true, new string[] { "Cancion uno", "Cancion dos", "Cancion tres" }, new string[] { "3:41", "4:02", "2:58" }, new int[] { 5, 3, 1 }, 12, fam);
                    break;
                }
                case 5:   // los globos de la estrella: la pregunta y la despedida
                {
                    Size g = GpuPanel.AsistentePanelRect(w, h).Size;
                    if (g.Width < 8 || g.Height < 8) return;
                    using (Bitmap panel = PintaPanelAsistente(g.Width, g.Height, 0, 0, 0)) { }
                    using (Bitmap adios = PintaAdiosAsistente(g.Width, g.Height)) { }
                    break;
                }
                case 6:   // el aviso, la invitacion a meter una moneda, la moneda y su "+4 creditos"
                {
                    using (Font tit = new Font(fam, 14f, FontStyle.Bold))
                    using (Font det = new Font(fam, 11f, FontStyle.Bold))
                    {
                        Size a = TamanoAviso(w, "Aviso de relleno", "con su renglon", tit, det);
                        using (Bitmap aviso = new Bitmap(a.Width, a.Height, PixelFormat.Format32bppPArgb))
                            PintaAviso(aviso, TipoAviso.Hecho, "Aviso de relleno", "con su renglon", tit, det);
                    }
                    using (Font tit = new Font(fam, 17f, FontStyle.Bold))
                    using (Font txt = new Font(fam, 12.5f, FontStyle.Bold))
                    using (Font pie = new Font(fam, 10.5f, FontStyle.Bold))
                    {
                        Size t = TamanoInvita(w);
                        PointF ranura; float radio;
                        using (Bitmap invita = new Bitmap(t.Width, t.Height, PixelFormat.Format32bppPArgb))
                            PintaInvita(invita, "Cancion de relleno", false, 4, tit, txt, pie, out ranura, out radio);
                    }
                    using (Bitmap moneda = GpuPanel.BakeMoneda(96)) { }
                    using (Font mas = new Font(fam, 14f, FontStyle.Bold, GraphicsUnit.Pixel))
                    {
                        Size m = TamanoMas("+4 cr\u00E9ditos", mas);
                        using (Bitmap bm = new Bitmap(m.Width, m.Height, PixelFormat.Format32bppPArgb))
                            PintaMas(bm, "+4 cr\u00E9ditos", mas);
                    }
                    break;
                }
            }
        }

        private List<Album> NextGpuPage(List<Album> list, int current)
        {
            List<Album> result=new List<Album>(PageSize);
            if(libBuilding || list==null || current<0 || (long)current*PageSize>=list.Count) return result;
            long start=((long)current+1)*PageSize;
            if(start>=list.Count)
            {
                // Misma rueda que NextPage/GotoLetter, sin cambiar seleccion ni construir TOP.
                // Al navegar se revalida TOP si cambio mientras se preparaban sus caratulas.
                int idx=LetrasOrden.IndexOf(currentLetter);if(idx<0)idx=0;
                list=null;start=0;
                for(int k=1;k<=LetrasOrden.Length;k++)
                {
                    char c=LetrasOrden[(idx+k)%LetrasOrden.Length];
                    if(!letterAvailable.Contains(c)) continue;
                    if(c==VideoMark) list=videoAlbums;
                    else if(c==TopMark) list=topDiscs;
                    else byLetter.TryGetValue(c,out list);
                    break;
                }
                if(list==null)return result;
            }
            int end=(int)Math.Min(list.Count,start+PageSize);
            for(long i=start;i<end;i++)result.Add(list[(int)i]);
            return result;
        }
    }
    internal sealed partial class GpuPanel
    {
        private long aheadAvailableTicks;
        private int cellTextureCapacity = 54;
        private double cellBudgetChecked = -2000;
        private int cellBudgetWidth, cellBudgetHeight;
        private string memoryPageKey;
        private double memoryPageChanged;
        // GetAvailableTextureMem es una llamada SINCRONA al controlador (en Windows XP puede tardar milisegundos, dentro de un cuadro). El
        // presupuesto de portadas que sale de ella solo cambia si cambia la memoria de la tarjeta, o sea casi nunca: se pregunta al
        // empezar, cada minuto, al cambiar la resolucion y cuando una textura no se pudo crear (20-10-2026; antes cada 10 s). No se
        // pregunta con el estado extendido abierto o entrando/saliendo: ahi no hay rejilla que presupuestar.
        internal const double SondeoMemoriaMs = 60000, SondeoForzadoMs = 2000;
        internal static bool CanProbeCoverMemory(double now, double changed, double checkedAt,
            bool initial, bool busy)
        { return !busy && now - changed >= 600 && (initial || now - checkedAt >= SondeoMemoriaMs); }
        // Un sondeo pedido por un suceso (resolucion, textura que no se pudo crear): ya, pero no mas de uno cada SondeoForzadoMs.
        internal static bool SondeoPedido(bool pedido, double now, double checkedAt)
        { return pedido && now - checkedAt >= SondeoForzadoMs; }
        private bool sondeoMemoriaPedido;
        private readonly HashSet<string> protectedAhead = new HashSet<string>();
        internal static long LiveCellBudget(uint available, int cacheMb, int pointerBytes, ulong freeRam)
        {
            long target = AdaptiveCellBudget(available, cacheMb, pointerBytes);
            if (freeRam > 0) target = Math.Min(target, (long)Math.Min((ulong)long.MaxValue, freeRam / 16));
            return Math.Max(16L * 1024 * 1024, target);
        }
        private void RefreshCellBudget() { RefreshCellBudget(AhoraMs()); }
        private void RefreshCellBudget(double now)
        {
            if (IdleExpandTarget || idleBlend > 0f) return;   // con el estado extendido (o entrando o saliendo) no hay rejilla que presupuestar
            string key = gridN > 0 ? gridBuf[0].Key : null;
            if (cellBudgetWidth != Width || cellBudgetHeight != Height) { if (cellBudgetWidth != 0 || cellBudgetHeight != 0) sondeoMemoriaPedido = true; }
            if (key != memoryPageKey || cellBudgetWidth != Width || cellBudgetHeight != Height) {
                memoryPageKey = key; memoryPageChanged = now;
                cellBudgetWidth = Width; cellBudgetHeight = Height;
            }
            // GetAvailableTextureMem es sincrono: no llamar al driver en pleno hojeo.
            // El presupuesto anterior sigue protegiendo memoria durante la navegacion.
            if (stars == null || !stars.Ok || !CanProbeCoverMemory(now, memoryPageChanged,
                cellBudgetChecked, cellBudgetChecked < 0 || SondeoPedido(sondeoMemoriaPedido, now, cellBudgetChecked),
                UiUrgent || (CoverControl != null && CoverControl.PendingUpload))) return;
            cellBudgetChecked = now; cellBudgetWidth = Width; cellBudgetHeight = Height; sondeoMemoriaPedido = false;
            uint available = stars.EstimatedTextureMemory;
            // El sondeo ya descuenta nuestras texturas: devolver su coste evita una espiral de poda.
            if (available != 0) available = (uint)Math.Min(uint.MaxValue, (ulong)available + (ulong)CachedCellBytes());
            cellTextureBudget = LiveCellBudget(available, Form1.CacheCeldasMb(), IntPtr.Size, Form1.FreeCoverMemory());
            long cell = 0; int count = 0;
            foreach (CellTex texture in cellTexCache.Values) { cell += CellBytes(texture); count++; }
            long average = count > 0 ? Math.Max(1, cell / count + 256 * 256 * 4) : 1024 * 1024;
            cellTextureCapacity = (int)Math.Max(54, Math.Min(216, cellTextureBudget / average));
        }
        // Estimacion gruesa al crear dispositivo, no sondeo por textura ni garantia de VRAM.
        // Reserva margen para fondo/video/driver y conserva 32 MiB si no hay informacion.
        internal static long AdaptiveCellBudget(uint available, int systemCacheMb, int pointerBytes)
        {
            const long mb=1024L*1024;
            if (available==0 || systemCacheMb<48) return 32*mb;
            long ceiling=Math.Min(pointerBytes>=8 ? 128 : 96, Math.Max(32,systemCacheMb)*2L)*mb;
            return Math.Max(32*mb,Math.Min(ceiling,available/4L));
        }
        internal static int AheadUploadAllowance(int processors)
        { return processors<=2 ? 1 : processors<=6 ? 2 : 4; }
    }
    public partial class Form1
    {
        internal static ulong FreeCoverMemory() { return MemoriaLibre(); }
    }
}
#endif
