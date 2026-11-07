// PRUEBAS DE LA TRADUCCION EN LINUX (no son de XP): se piden por la linea de ordenes y salen antes de abrir la rocola.
//   rocola-xp --prueba-gpu ANCHO ALTO SALIDA.png [CUADROS]   el cielo y las estrellas de XP (GpuStarfield, la ventana de prueba F12 de
//                                                            XP) pintados con Direct3DaGL fuera de pantalla, y el ultimo cuadro en PNG.
//   rocola-xp --prueba-video RUTA [SEGUNDOS]                 el video solo (libmpv en su textura, sin la rocola): el punto del centro.
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
            // (el video necesita hilos: mientras corre el inicializador del modulo, cualquier otro hilo que entre al modulo -el de los avisos
            // de mpv y su aviso de cuadro nuevo- se queda esperando; por eso esa prueba corre despues, al empezar el Main de XP)
            if (a.Length >= 3 && a[1] == "--prueba-video") { videoPendiente = a; return; }
            // la rocola: la pantalla (KMS, o fuera de pantalla) y OpenGL en este hilo, antes del Main de XP (Anfitrion.cs)
            Anfitrion.Arranca();
        }

        private static string[] videoPendiente;
        private static uint fboVolcado;
        private static unsafe uint f2(uint tex)
        {
            if (fboVolcado == 0) { uint f; Rocola.Pantalla.GL.GenFramebuffers(1, &f); fboVolcado = f; }
            Rocola.Pantalla.GL.BindFramebuffer(Rocola.Pantalla.GL.FRAMEBUFFER, fboVolcado);
            Rocola.Pantalla.GL.FramebufferTexture2D(Rocola.Pantalla.GL.FRAMEBUFFER, Rocola.Pantalla.GL.COLOR_ATTACHMENT0, Rocola.Pantalla.GL.TEXTURE_2D, tex, 0);
            return fboVolcado;
        }
        internal static void DespuesDelInicio()
        {
            string[] a = videoPendiente;
            if (a == null) return;
            Environment.Exit(PruebaVideo(a[2], a.Length >= 4 ? double.Parse(a[3], System.Globalization.CultureInfo.InvariantCulture) : 8));
        }

        // El video solo (VideoMpv.cs), sin la rocola: libmpv pinta en su textura y se lee el punto del centro cada cuarto de segundo
        private static unsafe int PruebaVideo(string ruta, double segundos)
        {
            using (Rocola.Pantalla.FueraDePantalla fp = new Rocola.Pantalla.FueraDePantalla(1024, 768))
            {
                Console.WriteLine("OpenGL: " + fp.Descripcion);
                Control host = new Control();
                host.Size = new System.Drawing.Size(498, 606);
                ReproductorWmp r = new ReproductorWmp(host);
                r.URL = ruta;
                System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();
                double siguiente = 0;
                while (reloj.Elapsed.TotalSeconds < segundos)
                {
                    r.Pinta();
                    if (reloj.Elapsed.TotalSeconds >= siguiente && host.TexturaGl != 0)
                    {
                        siguiente += 0.25;
                        int antes; Rocola.Pantalla.GL.GetIntegerv(0x8CA6, &antes);   // (FRAMEBUFFER_BINDING: el de la pantalla fuera de pantalla)
                        uint f; Rocola.Pantalla.GL.GenFramebuffers(1, &f);
                        Rocola.Pantalla.GL.BindFramebuffer(Rocola.Pantalla.GL.FRAMEBUFFER, f);
                        Rocola.Pantalla.GL.FramebufferTexture2D(Rocola.Pantalla.GL.FRAMEBUFFER, Rocola.Pantalla.GL.COLOR_ATTACHMENT0, Rocola.Pantalla.GL.TEXTURE_2D, host.TexturaGl, 0);
                        byte* px = stackalloc byte[4];
                        Rocola.Pantalla.GL.ReadPixels(249, 303, 1, 1, Rocola.Pantalla.GL.RGBA, Rocola.Pantalla.GL.UNSIGNED_BYTE, px);
                        Rocola.Pantalla.GL.BindFramebuffer(Rocola.Pantalla.GL.FRAMEBUFFER, (uint)antes);
                        GlMas.DeleteFramebuffers(1, &f);
                        Console.WriteLine(reloj.Elapsed.TotalSeconds.ToString("0.00") + " s estado " + r.playState + " centro " + px[0] + "," + px[1] + "," + px[2] + "," + px[3]
                            + " pos " + r.NumeroMpv("time-pos").ToString("0.00") + " tirados " + r.NumeroMpv("frame-drop-count") + "/" + r.NumeroMpv("decoder-frame-drop-count") + " atrasados " + r.NumeroMpv("vo-delayed-frame-count") + " cuadro " + r.NumeroMpv("estimated-frame-number"));
                        string volcado = Environment.GetEnvironmentVariable("ROCOLA_VIDEO_VOLCADO");   // (pruebas) la textura entera en PNG
                        if (!string.IsNullOrEmpty(volcado))
                        {
                            Rocola.Pantalla.GL.BindFramebuffer(Rocola.Pantalla.GL.FRAMEBUFFER, f2(host.TexturaGl));
                            using (SKBitmap b = new SKBitmap(new SKImageInfo(498, 606, SKColorType.Rgba8888, SKAlphaType.Unpremul)))
                            {
                                Rocola.Pantalla.GL.ReadPixels(0, 0, 498, 606, Rocola.Pantalla.GL.RGBA, Rocola.Pantalla.GL.UNSIGNED_BYTE, (void*)b.GetPixels());
                                using (SKData d = b.Encode(SKEncodedImageFormat.Png, 100)) using (System.IO.FileStream fs = System.IO.File.Create(volcado + "-" + ((int)(siguiente * 4)).ToString("000") + ".png")) d.SaveTo(fs);
                            }
                            Rocola.Pantalla.GL.BindFramebuffer(Rocola.Pantalla.GL.FRAMEBUFFER, (uint)antes);
                        }
                    }
                    r.TrasPresentar();
                    System.Threading.Thread.Sleep(16);
                }
                r.Dispose();
            }
            return 0;
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
