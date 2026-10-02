#if ROCOLA_GPU
using System;
using System.Collections.Generic;
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
        internal static bool CanProbeCoverMemory(double now, double changed, double checkedAt,
            bool initial, bool busy)
        { return !busy && now - changed >= 600 && (initial || now - checkedAt >= 10000); }
        private readonly HashSet<string> protectedAhead = new HashSet<string>();
        internal static long LiveCellBudget(uint available, int cacheMb, int pointerBytes, ulong freeRam)
        {
            long target = AdaptiveCellBudget(available, cacheMb, pointerBytes);
            if (freeRam > 0) target = Math.Min(target, (long)Math.Min((ulong)long.MaxValue, freeRam / 16));
            return Math.Max(16L * 1024 * 1024, target);
        }
        private void RefreshCellBudget()
        {
            double now = AhoraMs();
            string key = gridN > 0 ? gridBuf[0].Key : null;
            if (key != memoryPageKey || cellBudgetWidth != Width || cellBudgetHeight != Height) {
                memoryPageKey = key; memoryPageChanged = now;
                cellBudgetWidth = Width; cellBudgetHeight = Height;
            }
            // GetAvailableTextureMem es sincrono: no llamar al driver en pleno hojeo.
            // El presupuesto anterior sigue protegiendo memoria durante la navegacion.
            if (stars == null || !stars.Ok || !CanProbeCoverMemory(now, memoryPageChanged,
                cellBudgetChecked, cellBudgetChecked < 0, UiUrgent || (CoverControl != null && CoverControl.PendingUpload))) return;
            cellBudgetChecked = now; cellBudgetWidth = Width; cellBudgetHeight = Height;
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
