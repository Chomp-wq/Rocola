// ====================================================================================================
//  PRUEBA DE VIABILIDAD GPU (Direct3D 9 / SlimDX)
// ----------------------------------------------------------------------------------------------------
//  Esto NO dibuja la rocola por GPU todavia. Solo comprueba si la maquina (la rocola, Windows XP) puede
//  crear un dispositivo Direct3D 9 por HARDWARE y reporta el resultado. Es el paso previo, barato y
//  seguro, antes de reescribir el render: si la GPU no responde, nos quedamos con el render por CPU.
//
//  Esta todo dentro de #if ROCOLA_GPU: con el simbolo APAGADO (lo normal) este archivo compila vacio y
//  el programa no necesita SlimDX para nada. Para activarlo, ver instrucciones al final del archivo.
// ====================================================================================================
#if ROCOLA_GPU
using System;
using System.Text;
using System.Windows.Forms;
using SlimDX.Direct3D9;

namespace Rocola
{
    internal static class GpuProbe
    {
        // Intenta inicializar Direct3D 9 y crear un dispositivo HARDWARE diminuto (16x16, invisible).
        // Reporta a un MessageBox y a gpu_probe.txt (junto al .exe). No deja nada creado.
        public static void Run(IntPtr hwnd)
        {
            StringBuilder sb = new StringBuilder();
            Direct3D d3d = null;
            Device dev = null;
            try
            {
                d3d = new Direct3D();
                sb.AppendLine("Direct3D 9 inicializado. Adaptadores: " + d3d.AdapterCount);
                try { sb.AppendLine("GPU: " + d3d.Adapters[0].Details.Description); }
                catch (Exception en) { sb.AppendLine("(no se pudo leer el nombre de la GPU: " + en.Message + ")"); }

                PresentParameters pp = new PresentParameters();
                pp.Windowed = true;
                pp.SwapEffect = SwapEffect.Discard;
                pp.BackBufferWidth = 16;
                pp.BackBufferHeight = 16;
                pp.BackBufferFormat = Format.X8R8G8B8;
                pp.DeviceWindowHandle = hwnd;

                try
                {
                    dev = new Device(d3d, 0, DeviceType.Hardware, hwnd, CreateFlags.HardwareVertexProcessing, pp);
                    sb.AppendLine();
                    sb.AppendLine(">>> GPU OK: dispositivo HARDWARE con vertices por HARDWARE (lo mejor).");
                }
                catch (Exception eh)
                {
                    sb.AppendLine("Vertices por hardware: NO (" + eh.Message + "). Probando vertices por software...");
                    dev = new Device(d3d, 0, DeviceType.Hardware, hwnd, CreateFlags.SoftwareVertexProcessing, pp);
                    sb.AppendLine();
                    sb.AppendLine(">>> GPU OK: dispositivo HARDWARE con vertices por SOFTWARE (sirve igual para 2D).");
                }
                sb.AppendLine("CONCLUSION: la rocola SI puede acelerar el dibujo por GPU. Seguimos con el render D3D9.");
            }
            catch (Exception ex)
            {
                sb.AppendLine();
                sb.AppendLine(">>> SIN GPU utilizable: " + ex.GetType().Name + ": " + ex.Message);
                sb.AppendLine("CONCLUSION: conviene quedarse con el render por CPU (que ya quedo fluido).");
            }
            finally
            {
                if (dev != null) dev.Dispose();
                if (d3d != null) d3d.Dispose();
            }

            string text = sb.ToString();
            try { System.IO.File.WriteAllText("gpu_probe.txt", text); } catch { }
            try { MessageBox.Show(text, "Rocola - Prueba de GPU (Direct3D 9)"); } catch { }
        }
    }
}
#endif

// ====================================================================================================
//  COMO ACTIVAR LA PRUEBA (en la rocola / en tu Visual Studio)
// ----------------------------------------------------------------------------------------------------
//  1) Instalar SlimDX:
//       - SlimDX End User Runtime (.NET 2.0, enero 2012)  -> https://slimdx.org  (sirve en XP)
//       - SlimDX SDK (para tener SlimDX.dll para referenciar)  -> de la misma pagina
//     (Tambien hace falta el runtime de DirectX 9, que XP ya suele traer.)
//
//  2) En el proyecto Rocola (Visual Studio):
//       - Proyecto > Agregar referencia > SlimDX  (aparece en .NET si instalaste el SDK; si no,
//         Examinar y apuntar a SlimDX.dll, la version para .NET 2.0).
//       - Proyecto > Propiedades > Compilacion (Build) > "Simbolos de compilacion condicional"
//         (Conditional compilation symbols): agregar   ROCOLA_GPU
//
//  3) Compilar en Release y correr en la rocola. Al abrir aparecera un cuadro con el resultado
//     (y se guarda en gpu_probe.txt junto al .exe). Pasame ese texto y seguimos:
//       - Si dice "GPU OK"      -> escribo el render Direct3D 9 del fondo estrellado.
//       - Si dice "SIN GPU"     -> nos quedamos con el render por CPU (ya fluido) y ese mensaje me
//                                  dice por que fallo, por si hay arreglo (driver, etc.).
//
//  Con ROCOLA_GPU sin definir, este archivo no hace nada y el programa compila/corre igual que hoy.
// ====================================================================================================
