// ====================================================================================================
//  PRUEBA DE VIABILIDAD GPU (Direct3D 9) — SIN dependencias externas (P/Invoke directo a d3d9.dll)
// ----------------------------------------------------------------------------------------------------
//  Esto NO dibuja la rocola por GPU todavia. Solo comprueba si la maquina (la rocola, Windows XP) puede
//  crear un dispositivo Direct3D 9 por HARDWARE y reporta el resultado. Es el paso previo, barato y
//  seguro, antes de reescribir el render: si la GPU no responde, nos quedamos con el render por CPU.
//
//  NO necesita SlimDX ni NuGet: llama a d3d9.dll (presente en todo Windows XP) por interop. Esta todo
//  dentro de #if ROCOLA_GPU: con el simbolo APAGADO (lo normal) este archivo compila vacio.
//  Para activarlo: ver instrucciones al final del archivo.
// ====================================================================================================
#if ROCOLA_GPU
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Rocola
{
    internal static class GpuProbe
    {
        [DllImport("d3d9.dll")]
        private static extern IntPtr Direct3DCreate9(uint sdkVersion);

        // Parametros de presentacion de D3D9 (mismo orden/tipos que la struct nativa D3DPRESENT_PARAMETERS).
        [StructLayout(LayoutKind.Sequential)]
        private struct PresentParams
        {
            public uint BackBufferWidth, BackBufferHeight, BackBufferFormat, BackBufferCount;
            public uint MultiSampleType, MultiSampleQuality, SwapEffect;
            public IntPtr DeviceWindow;
            public int Windowed, EnableAutoDepthStencil;
            public uint AutoDepthStencilFormat, Flags, FullScreenRefreshRateInHz, PresentationInterval;
        }

        // Las funciones de la interfaz COM IDirect3D9 se llaman por su lugar en la vtable (this como 1er arg).
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint GetAdapterCountDel(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateDeviceDel(IntPtr self, uint adapter, uint devType, IntPtr hwnd, uint behaviorFlags, ref PresentParams pp, out IntPtr device);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ReleaseDel(IntPtr self);

        private static T VtblFn<T>(IntPtr punk, int slot) where T : class
        {
            IntPtr vtbl = Marshal.ReadIntPtr(punk);                       // puntero a la vtable
            IntPtr fn = Marshal.ReadIntPtr(vtbl, slot * IntPtr.Size);     // funcion en ese lugar
            return (T)(object)Marshal.GetDelegateForFunctionPointer(fn, typeof(T));
        }

        private static void ReleaseCom(IntPtr punk)
        {
            try { ReleaseDel rel = VtblFn<ReleaseDel>(punk, 2); rel(punk); } catch { }   // IUnknown::Release = lugar 2
        }

        public static void Run(IntPtr hwnd)
        {
            StringBuilder sb = new StringBuilder();
            IntPtr pD3D = IntPtr.Zero;
            try
            {
                pD3D = Direct3DCreate9(32);   // 32 = D3D_SDK_VERSION
                if (pD3D == IntPtr.Zero)
                {
                    sb.AppendLine(">>> SIN GPU: Direct3DCreate9 devolvio nulo (no hay Direct3D 9 utilizable).");
                }
                else
                {
                    GetAdapterCountDel getCount = VtblFn<GetAdapterCountDel>(pD3D, 4);   // IDirect3D9::GetAdapterCount = lugar 4
                    CreateDeviceDel createDevice = VtblFn<CreateDeviceDel>(pD3D, 16);    // IDirect3D9::CreateDevice    = lugar 16
                    uint adapters = 0;
                    try { adapters = getCount(pD3D); } catch { }
                    sb.AppendLine("Direct3D 9 disponible. Adaptadores: " + adapters);

                    PresentParams pp = new PresentParams();
                    pp.BackBufferWidth = 16; pp.BackBufferHeight = 16;
                    pp.BackBufferFormat = 0;     // D3DFMT_UNKNOWN (en ventana usa el formato del escritorio)
                    pp.BackBufferCount = 1;
                    pp.SwapEffect = 1;           // D3DSWAPEFFECT_DISCARD
                    pp.DeviceWindow = hwnd;
                    pp.Windowed = 1;

                    const uint HAL = 1;          // D3DDEVTYPE_HAL (hardware)
                    const uint HWVP = 0x40;      // D3DCREATE_HARDWARE_VERTEXPROCESSING
                    const uint SWVP = 0x20;      // D3DCREATE_SOFTWARE_VERTEXPROCESSING
                    IntPtr pDev;

                    int hr = createDevice(pD3D, 0, HAL, hwnd, HWVP, ref pp, out pDev);
                    if (hr >= 0)
                    {
                        sb.AppendLine().AppendLine(">>> GPU OK: dispositivo HARDWARE con vertices por HARDWARE (lo mejor).");
                        ReleaseCom(pDev);
                    }
                    else
                    {
                        sb.AppendLine("Vertices por hardware: NO (hr=0x" + hr.ToString("X8") + "). Probando vertices por software...");
                        hr = createDevice(pD3D, 0, HAL, hwnd, SWVP, ref pp, out pDev);
                        if (hr >= 0)
                        {
                            sb.AppendLine().AppendLine(">>> GPU OK: dispositivo HARDWARE con vertices por SOFTWARE (sirve para 2D).");
                            ReleaseCom(pDev);
                        }
                        else
                        {
                            sb.AppendLine().AppendLine(">>> SIN GPU utilizable: CreateDevice fallo (hr=0x" + hr.ToString("X8") + ").");
                        }
                    }

                    sb.AppendLine(sb.ToString().Contains("GPU OK")
                        ? "CONCLUSION: la rocola SI puede acelerar por GPU. Sigo con el render Direct3D 9."
                        : "CONCLUSION: conviene quedarse con el render por CPU (que ya quedo fluido).");
                }
            }
            catch (DllNotFoundException)
            {
                sb.AppendLine(">>> SIN GPU: no se encontro d3d9.dll (sin DirectX 9 en el equipo).");
            }
            catch (Exception ex)
            {
                sb.AppendLine(">>> Error en la prueba: " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (pD3D != IntPtr.Zero) ReleaseCom(pD3D);
            }

            string text = sb.ToString();
            try { System.IO.File.WriteAllText("gpu_probe.txt", text); } catch { }
            try { MessageBox.Show(text, "Rocola - Prueba de GPU (Direct3D 9)"); } catch { }
        }
    }
}
#endif

// ====================================================================================================
//  COMO ACTIVAR LA PRUEBA  (ya NO hace falta SlimDX ni NuGet)
// ----------------------------------------------------------------------------------------------------
//  1) Visual Studio: Proyecto > Propiedades > Compilacion (Build) > "Simbolos de compilacion condicional"
//     (Conditional compilation symbols): agregar   ROCOLA_GPU
//  2) Compilar en Release y correr en la rocola. Al abrir saldra un cuadro con el resultado (y se guarda
//     en gpu_probe.txt junto al .exe). Pasame ese texto:
//       - "GPU OK ..."   -> escribo el render Direct3D 9 del fondo estrellado (acelerado).
//       - "SIN GPU ..."  -> nos quedamos con el render por CPU (ya fluido); el mensaje dice por que.
//
//  Con ROCOLA_GPU sin definir, este archivo no hace nada y el programa compila/corre igual que hoy.
//  (Si llegaste a instalar el paquete SlimDX por NuGet, puedes quitarlo: esta prueba no lo usa.)
// ====================================================================================================
