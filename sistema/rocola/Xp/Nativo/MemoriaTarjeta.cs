// LA MEMORIA DE LA TARJETA PARA TEXTURAS (port nativo de XP). Lo que en XP contestaba IDirect3DDevice9::GetAvailableTextureMem (la
// usan el presupuesto de portadas, PrecargaPaginaGpu.cs AdaptiveCellBudget, y el diagnostico): lo que el controlador radeon le da a las
// texturas (VRAM + GTT, por DRM_IOCTL_RADEON_GEM_INFO; si no es la radeon -llvmpipe en las pruebas- 512 MB) menos lo que ya ocupan las
// texturas de la rocola (TexturasGl.Bytes; Direct3D tambien lo descontaba), en MB enteros. Es la misma cuenta que hacia la traduccion
// con capas (Direct3DaGL.cs), salvo los 4 bytes de su textura negra, que aqui no hace falta.
#if ROCOLA_GPU
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Rocola
{
    internal static unsafe class MemoriaTarjeta
    {
        internal static uint Libre()
        {
            long total = Total(), libre = total - TexturasGl.Bytes;
            if (libre < 0) libre = 0;
            libre &= ~0xFFFFFL;
            return (uint)Math.Min(libre, 0xFFF00000L);
        }
        private static long total = -1;
        private static long Total()
        {
            if (total >= 0) return total;
            long m = Radeon();
            if (m <= 0) m = 512L << 20;
            total = m;
            return m;
        }
        [DllImport("libc", SetLastError = true)] private static extern int open(string ruta, int banderas);
        [DllImport("libc")] private static extern int close(int fd);
        [DllImport("libc", SetLastError = true)] private static extern int ioctl(int fd, ulong peticion, void* datos);
        // DRM_IOCTL_RADEON_GEM_INFO: gart_size, vram_size, vram_visible. Solo con el controlador radeon (el del A4): el mismo numero de
        // llamada es otra cosa en otros controladores.
        private static long Radeon()
        {
            try
            {
                FileSystemInfo c = Directory.ResolveLinkTarget("/sys/class/drm/card0/device/driver", true);
                if (c == null || c.Name != "radeon") return 0;
            }
            catch { return 0; }
            ulong* info = stackalloc ulong[3];
            foreach (string r in new[] { "/dev/dri/renderD128", "/dev/dri/card0" })
            {
                int fd = -1;
                try
                {
                    fd = open(r, 2 | 0x80000);   // O_RDWR | O_CLOEXEC
                    if (fd < 0) continue;
                    info[0] = info[1] = info[2] = 0;
                    if (ioctl(fd, 0xC018645CUL, info) == 0 && info[1] > 0) return (long)(info[0] + info[1]);
                }
                catch { }
                finally { if (fd >= 0) close(fd); }
            }
            return 0;
        }
    }
}
#endif
