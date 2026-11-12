#if ROCOLA_GPU
using System;
using System.Collections.Generic;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: la precarga hace sus mapas de usar y tirar y sus letras con SkiaSharp y lo nativo (Xp/Nativo), y llama directo a los hornos ya traducidos; lo que aun es de la capa (el Graphics que da EnSeco, Form1.Video.cs, y la moneda que entrega BakeMoneda) va escrito entero
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
            return EnSeco(searchOverlay, delegate (SKCanvas g, bool suave, AjusteLetra ajuste)   // EN LINUX: delegate (Graphics g): EnSeco (Form1.Video.cs) da el lienzo de Skia de su mapa y el estado que le pone (suavizado y modo de letra; la copia, el filtro y el medio pixel, los de fabrica de un Graphics nuevo)
            { Filtro filtro = Filtro.Bilineal; searchOverlay.RenderContent(g, ajuste, false, ref filtro, false, SKMatrix.Identity, new Rectangle(0, 0, searchOverlay.Width, searchOverlay.Height), true); });   // EN LINUX: la traducida (Form1.Buscador.cs), directo, con el lienzo y el estado de un Graphics.FromImage (la copia y el medio pixel apagados, el filtro Bilinear y la matriz del HDC de un mapa de bits, la identidad); lo que le deje se tira con el mapa, como en XP
        }

        // La lista de un disco, con la marca de la tarjeta (la fila elegida dentro del cristal): otro horneado, y otro camino de codigo.
        // La marca se deja como estaba.
        private bool PrecalientaListaGpu()
        {
            if (songList == null) return false;
            bool antes = songList.pintandoGpu;
            songList.pintandoGpu = true;
            try { return EnSeco(songList, delegate (SKCanvas g, bool suave, AjusteLetra ajuste) { songList.RenderContent(g, false, Filtro.Bilineal, false); }); }   // EN LINUX: delegate (Graphics g) { songList.RenderContent(g); }: el lienzo de EnSeco y la traducida (Form1.Vistas.cs), directo, con lo que pesa del estado de un Graphics.FromImage (sin copia, filtro Bilinear, sin medio pixel)
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
            string fam = FamiliaUi().Name;   // EN LINUX: FontFamily fam = FamiliaUi(): la familia de la interfaz (Form1.Ajustes.cs da un FontFamily de la capa) por su nombre, que es lo que piden la Letra y los hornos ya traducidos
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
                    using (SKBitmap panel = RenderIdlePanelEn(null, w, ph, "Titulo de relleno", "Artista de relleno", "", qt, qa, new SKBitmap[n], n,   // EN LINUX: el panel es un SKBitmap y las portadas (ninguna: new Image[n]) un arreglo de SKBitmap: RenderIdlePanelEn (EstadoReposoGpu.cs, ya traducido), directo
                        Color.FromArgb(120, 160, 255), transparente, insignia, doce, null, opaco, out comienzo))
                    using (SKBitmap debajo = Mapas.Nuevo(w, 34))   // EN LINUX: new Bitmap(w, 34, Format32bppPArgb): un SKBitmap BGRA premultiplicado (Nativo/Mapas.cs)
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
                    using (SKBitmap foco = Mapas.Nuevo(AnchoFoco(lado, lado + 24), AltoFoco(lado)))   // EN LINUX: new Bitmap(w, h, Format32bppPArgb); PintaFoco (PantallaVivaGpu.cs, ya traducido), directo
                        PintaFoco(foco, "12 canciones \u00B7 38 min", "12 \u00B7 38 min", fam);
                    Size tam = TamanoFicha(Math.Max(120, w / 6), Math.Max(100, (h - 200) / 3));
                    using (SKBitmap ficha = Mapas.Nuevo(tam.Width, tam.Height))   // EN LINUX: new Bitmap(w, h, Format32bppPArgb); PintaFicha (PantallaVivaGpu.cs, ya traducido), directo
                        PintaFicha(ficha, null, "Artista de relleno", "Disco de relleno", "12 canciones \u00B7 38 min", "Pop", Color.FromArgb(120, 160, 255), 1,
                            true, new string[] { "Cancion uno", "Cancion dos", "Cancion tres" }, new string[] { "3:41", "4:02", "2:58" }, new int[] { 5, 3, 1 }, 12, fam);
                    break;
                }
                case 5:   // los globos de la estrella: la pregunta y la despedida
                {
                    Size g = GpuPanel.AsistentePanelRect(w, h).Size;
                    if (g.Width < 8 || g.Height < 8) return;
                    using (SkiaSharp.SKBitmap panel = PintaPanelAsistente(g.Width, g.Height, 0, 0, 0)) { }   // EN LINUX: PintaPanelAsistente (AsistenteRocola.cs, ya traducido) da un SKBitmap: se suelta igual
                    using (SkiaSharp.SKBitmap adios = PintaAdiosAsistente(g.Width, g.Height)) { }   // EN LINUX: PintaAdiosAsistente (AsistenteRocola.cs, ya traducido) da un SKBitmap: se suelta igual
                    break;
                }
                case 6:   // el aviso, la invitacion a meter una moneda, la moneda y su "+4 creditos"
                {
                    using (Rocola.Nativo.Letra tit = Rocola.Nativo.Letra.EnPuntos(fam, 14f, true))   // EN LINUX: new Font(fam, 14, Bold): la Letra de esa familia, en puntos y en negrita (Nativo/Letras.cs; Form1 tiene un metodo Letra)
                    using (Rocola.Nativo.Letra det = Rocola.Nativo.Letra.EnPuntos(fam, 11f, true))   // EN LINUX: new Font(fam, 11, Bold)
                    {
                        Size a = TamanoAviso(w, "Aviso de relleno", "con su renglon", tit, det);
                        using (SKBitmap aviso = Mapas.Nuevo(a.Width, a.Height))   // EN LINUX: new Bitmap(w, h, Format32bppPArgb); TamanoAviso y PintaAviso (SuenaVivoGpu.cs, ya traducido), directo
                            PintaAviso(aviso, TipoAviso.Hecho, "Aviso de relleno", "con su renglon", tit, det);
                    }
                    using (Rocola.Nativo.Letra tit = Rocola.Nativo.Letra.EnPuntos(fam, 17f, true))   // EN LINUX: new Font(fam, 17, Bold)
                    using (Rocola.Nativo.Letra txt = Rocola.Nativo.Letra.EnPuntos(fam, 12.5f, true))   // EN LINUX: new Font(fam, 12,5, Bold)
                    using (Rocola.Nativo.Letra pie = Rocola.Nativo.Letra.EnPuntos(fam, 10.5f, true))   // EN LINUX: new Font(fam, 10,5, Bold)
                    {
                        Size t = TamanoInvita(w);
                        PointF ranura; float radio;
                        using (SKBitmap invita = Mapas.Nuevo(t.Width, t.Height))   // EN LINUX: new Bitmap(w, h, Format32bppPArgb); PintaInvita (SuenaVivoGpu.cs, ya traducido), directo
                            PintaInvita(invita, "Cancion de relleno", false, 4, tit, txt, pie, out ranura, out radio);
                    }
                    using (Rocola.Dibujo2D.Bitmap moneda = GpuPanel.BakeMoneda(96)) { }   // EN LINUX: BakeMoneda (SuenaVivoGpu.cs) todavia entrega un Bitmap de la capa (para Upload), escrito entero
                    using (Rocola.Nativo.Letra mas = Rocola.Nativo.Letra.EnPixeles(fam, 14f, true))   // EN LINUX: new Font(fam, 14, Bold, GraphicsUnit.Pixel): en pixeles
                    {
                        Size m = TamanoMas("+4 cr\u00E9ditos", mas);
                        using (SKBitmap bm = Mapas.Nuevo(m.Width, m.Height))   // EN LINUX: new Bitmap(w, h, Format32bppPArgb); TamanoMas y PintaMas (SuenaVivoGpu.cs, ya traducido), directo
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
