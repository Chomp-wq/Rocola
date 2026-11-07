// PRUEBAS DE LA TRADUCCION EN LINUX (no son de XP): se piden por la linea de ordenes y salen antes de abrir la rocola.
//   rocola-xp --prueba-gpu ANCHO ALTO SALIDA.png [CUADROS]   el cielo y las estrellas de XP (GpuStarfield, la ventana de prueba F12 de
//                                                            XP) pintados con Direct3DaGL fuera de pantalla, y el ultimo cuadro en PNG.
// Y con la rocola entera (variables de entorno):
//   ROCOLA_PANTALLA=fuera:1024x768   sin KMS (Anfitrion.cs)
//   ROCOLA_CAPTURA=S:ruta.png,...    la pantalla a los S segundos; ROCOLA_CAPTURA_SALE=1 cierra despues de la ultima;
//                                    ROCOLA_CAPTURA_CAPAS=1 tambien cada capa; ROCOLA_CAPTURA_TEXTURAS=nombre:AxB,... texturas de OpenGL
//   ROCOLA_PULSA=S:VK,...            teclas de Windows a su hora (TeclasLinux.cs)
//   ROCOLA_TRAZA_DIBUJOS=S           cada dibujo del primer cuadro despues de S segundos (Direct3DaGL.cs)
//   ROCOLA_SALTA_DIBUJOS=k1,k2       no hace esos dibujos de cada cuadro (para buscar el que pinta mal)
//   ROCOLA_VOLCADO_TEXTURAS=carpeta  cada textura grande que se sube, en PNG
using System;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace Rocola
{
    internal sealed partial class GpuStarfield { internal D3D9Device Dispositivo { get { return gpu; } } }
    internal sealed partial class D3D9Device { internal IntPtr DispositivoGl { get { return dev; } } }

    internal static class PruebasLinux
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Antes()
        {
            string[] a = Environment.GetCommandLineArgs();
            if (a.Length >= 5 && a[1] == "--prueba-gpu") Environment.Exit(PruebaGpu(int.Parse(a[2]), int.Parse(a[3]), a[4], a.Length >= 6 ? int.Parse(a[5]) : 3));
            // la rocola: la pantalla (KMS, o fuera de pantalla) y OpenGL en este hilo, antes del Main de XP (Anfitrion.cs)
            Anfitrion.Arranca();
        }

        private static unsafe int PruebaGpu(int w, int h, string salida, int cuadros)
        {
            using (Rocola.Pantalla.FueraDePantalla fp = new Rocola.Pantalla.FueraDePantalla(w, h))
            {
                Console.WriteLine("OpenGL: " + fp.Descripcion);
                GpuStarfield s = new GpuStarfield(IntPtr.Zero, w, h, 1234);
                if (!s.Ok) { Console.WriteLine("No se pudo crear el dispositivo"); return 1; }
                System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < cuadros; i++) s.Render(w, h, 0f);
                Rocola.Pantalla.GL.Finish();
                double ms = reloj.Elapsed.TotalMilliseconds;
                int bw, bh;
                byte[] px = D3D9.EnGl.LeeFondo(s.Dispositivo.DispositivoGl, out bw, out bh);
                if (px == null) { Console.WriteLine("Sin fondo que leer"); return 1; }
                using (SKBitmap b = new SKBitmap(new SKImageInfo(bw, bh, SKColorType.Bgra8888, SKAlphaType.Opaque)))
                {
                    fixed (byte* p = px) Buffer.MemoryCopy(p, (void*)b.GetPixels(), px.Length, px.Length);
                    using (SKData d = b.Encode(SKEncodedImageFormat.Png, 100)) using (System.IO.FileStream f = System.IO.File.Create(salida)) d.SaveTo(f);
                }
                Console.WriteLine(cuadros + " cuadros en " + ms.ToString("0.0") + " ms (" + (ms / cuadros).ToString("0.00") + " ms por cuadro); objetos vivos: " + D3D9.ObjetosGlVivos);
                s.Dispose();
                Console.WriteLine("objetos vivos al soltar: " + D3D9.ObjetosGlVivos);
            }
            return 0;
        }
    }
}
