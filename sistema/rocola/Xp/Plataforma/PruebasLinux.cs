// PRUEBAS DE LA TRADUCCION EN LINUX (no son de XP): se piden por la linea de ordenes y salen antes de abrir la rocola.
//   rocola-xp --prueba-gpu ANCHO ALTO SALIDA.png [CUADROS]   el cielo y las estrellas de XP (GpuStarfield, la ventana de prueba F12 de
//                                                            XP) pintados con OpenGL ES 2 fuera de pantalla, y el ultimo cuadro en PNG.
//   rocola-xp --prueba-video RUTA [SEGUNDOS]                 el video solo (Nativo/ReproductorVideo.cs: libmpv en su textura de 498x606, sin la
//                                                            rocola): el punto del centro cada 0,25 s y, al final, en cuantas lecturas
//                                                            salio negro (RUTA absoluta; con el video de prueba el centro es amarillo).
//       ROCOLA_VIDEO_VOLCADO=prefijo     la textura entera en PNG en cada lectura (prefijo-NNN.png)
//       ROCOLA_VIDEO_TEXTURAS=S          a los S segundos, todas las texturas de OpenGL (las de mpv: planos, pasadas intermedias y tablas
//                                        de pesos de los escaladores) con su medida, formato y contenido (NaN, minimo, maximo, centro)
//   rocola-xp --prueba-aot / --prueba-mp3 ...                lo que XP hace con reflexion y estructuras de Windows, y el lector de MP3
//                                                            de XP (Mp3Xp.cs), para comparar JIT con NativeAOT (ver PruebaAot.cs)
//   rocola-xp --prueba-lector ARCHIVO...                      cada archivo con el lector de lo que no es MP3 ni WAV de siempre (lo que en
//                                                            XP era AudioFileReader: Nativo/LectorAudio.cs): formato, largo, duracion y
//                                                            una huella de las muestras; o la excepcion y como la toma XP (MotivoFallo,
//                                                            EsArchivoDanado). Un video (.mp4, .avi, .mpg, .wmv...): su duracion con el
//                                                            medidor (Nativo/MedidorVideo.cs), como Form1.MideDuracionVideo.
//   rocola-xp --prueba-salida SEGUNDOS [PARA]                la salida de XP (WaveOutPropio, SalidaPropia.cs) sobre ALSA (ROCOLA_ALSA): un
//                                                            tono de 440 Hz de SEGUNDOS, la posicion (GetPosition) frente al reloj cada
//                                                            100 ms y el aviso de parada; con PARA, Stop a esos segundos.
// El video, aqui y en la rocola entera (Nativo/ReproductorVideo.cs):
//   ROCOLA_VIDEO_TRAZA=1             los avisos de mpv, cada volumen que se le pone y cada cuadro pintado, con el punto del centro
//   ROCOLA_VIDEO_REGISTRO=ruta       lo que dice mpv (su registro, con msg-level=all=v)
//   ROCOLA_VIDEO_OPCIONES="k=v;..."  mas opciones de mpv, despues de las de la rocola (para probar otras: escaladores, gpu-dumb-mode...)
// Y con la rocola entera (variables de entorno):
//   ROCOLA_PANTALLA=fuera:1024x768   sin KMS (Anfitrion.cs)
//   ROCOLA_CAPTURA=S:ruta.png,...    la pantalla a los S segundos; ROCOLA_CAPTURA_SALE=1 cierra despues de la ultima;
//                                    ROCOLA_CAPTURA_CAPAS=1 tambien cada capa; ROCOLA_CAPTURA_TEXTURAS=nombre:AxB,... texturas de OpenGL
//   ROCOLA_PULSA=S:VK,...            teclas de Windows a su hora (TeclasLinux.cs)
//   ROCOLA_TRAZA_DIBUJOS=S           cada dibujo del primer cuadro despues de S segundos (Nativo/TrazaTarjeta.cs)
//   ROCOLA_SALTA_DIBUJOS=k1,k2       no hace esos dibujos de cada cuadro (para buscar el que pinta mal)
//   ROCOLA_VOLCADO_TEXTURAS=carpeta  cada textura grande que se sube, en PNG
using System;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace Rocola
{
    internal sealed partial class GpuStarfield { internal DispositivoTarjeta Dispositivo { get { return gpu; } } }

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
            if (a.Length >= 3 && (a[1] == "--prueba-lector" || a[1] == "--prueba-salida")) { videoPendiente = a; return; }   // (tambien con hilos: ffmpeg, la salida)
            // la rocola: la pantalla (KMS, o fuera de pantalla) y OpenGL en este hilo, antes del Main de XP (Anfitrion.cs)
            Anfitrion.Arranca();
        }

        private static string[] videoPendiente;
        internal static void DespuesDelInicio()
        {
            string[] a = videoPendiente;
            if (a == null) return;
            if (a[1] == "--prueba-lector") { string[] r = new string[a.Length - 2]; Array.Copy(a, 2, r, 0, r.Length); Environment.Exit(PruebasSonido.Lector(r)); }
            if (a[1] == "--prueba-salida") Environment.Exit(PruebasSonido.Salida(double.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture), a.Length >= 4 ? double.Parse(a[3], System.Globalization.CultureInfo.InvariantCulture) : -1));
            Environment.Exit(PruebaVideo(a[2], a.Length >= 4 ? double.Parse(a[3], System.Globalization.CultureInfo.InvariantCulture) : 8));
        }

        // EL VIDEO SOLO (Nativo/ReproductorVideo.cs), sin la rocola: libmpv pinta en su textura (el hueco del video de XP, 498x606) como con el anfitrion
        // (Pinta, y TrasPresentar despues de cada vuelta) y cada cuarto de segundo se lee el punto del centro. Al final, en cuantas de las
        // lecturas con el video ya empezado salio negro (0,0,0): lo que pasaba con los escaladores de fabrica de mpv (ReproductorVideo.Inicia).
        private const int VideoW = 498, VideoH = 606;
        private static unsafe int PruebaVideo(string ruta, double segundos)
        {
            string volcado = Environment.GetEnvironmentVariable("ROCOLA_VIDEO_VOLCADO");
            double texturasEn;
            if (!double.TryParse(Environment.GetEnvironmentVariable("ROCOLA_VIDEO_TEXTURAS"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out texturasEn)) texturasEn = -1;
            using (Rocola.Pantalla.FueraDePantalla fp = new Rocola.Pantalla.FueraDePantalla(1024, 768))
            {
                Console.WriteLine("OpenGL: " + fp.Descripcion);
                ReproductorVideo r = new ReproductorVideo();
                r.PonSitio(0, 0, VideoW, VideoH);
                r.Inicia();
                r.Carga(ruta);
                int pantalla; Rocola.Pantalla.GL.GetIntegerv(Rocola.Pantalla.GL.FRAMEBUFFER_BINDING, &pantalla);
                uint f; Rocola.Pantalla.GL.GenFramebuffers(1, &f);
                System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();
                double siguiente = 0;
                int lecturas = 0, negras = 0, n = 0;
                while (reloj.Elapsed.TotalSeconds < segundos)
                {
                    r.Pinta();
                    double t = reloj.Elapsed.TotalSeconds;
                    if (t >= siguiente && r.TexturaGl != 0)
                    {
                        siguiente += 0.25; n++;
                        Rocola.Pantalla.GL.BindFramebuffer(Rocola.Pantalla.GL.FRAMEBUFFER, f);
                        Rocola.Pantalla.GL.FramebufferTexture2D(Rocola.Pantalla.GL.FRAMEBUFFER, Rocola.Pantalla.GL.COLOR_ATTACHMENT0, Rocola.Pantalla.GL.TEXTURE_2D, r.TexturaGl, 0);
                        Rocola.Pantalla.GL.PixelStorei(Rocola.Pantalla.GL.PACK_ALIGNMENT, 4);
                        byte* px = stackalloc byte[4];
                        Rocola.Pantalla.GL.ReadPixels(VideoW / 2, VideoH / 2, 1, 1, Rocola.Pantalla.GL.RGBA, Rocola.Pantalla.GL.UNSIGNED_BYTE, px);
                        double pos = r.NumeroMpv("time-pos");
                        if (pos > 0.3) { lecturas++; if (px[0] == 0 && px[1] == 0 && px[2] == 0) negras++; }
                        Console.WriteLine(t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s estado " + (int)r.Estado + " centro " + px[0] + "," + px[1] + "," + px[2] + "," + px[3]
                            + " pos " + pos.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " tirados " + r.NumeroMpv("frame-drop-count") + " cuadro " + r.NumeroMpv("estimated-frame-number"));
                        if (!string.IsNullOrEmpty(volcado))   // ROCOLA_VIDEO_VOLCADO=prefijo: la textura entera (la fila 0 es la de arriba)
                            using (SKBitmap b = new SKBitmap(new SKImageInfo(VideoW, VideoH, SKColorType.Rgba8888, SKAlphaType.Unpremul)))
                            {
                                Rocola.Pantalla.GL.ReadPixels(0, 0, VideoW, VideoH, Rocola.Pantalla.GL.RGBA, Rocola.Pantalla.GL.UNSIGNED_BYTE, (void*)b.GetPixels());
                                using (SKData d = b.Encode(SKEncodedImageFormat.Png, 100)) using (System.IO.FileStream fs = System.IO.File.Create(volcado + "-" + n.ToString("000") + ".png")) d.SaveTo(fs);
                            }
                        Rocola.Pantalla.GL.BindFramebuffer(Rocola.Pantalla.GL.FRAMEBUFFER, (uint)pantalla);
                        if (texturasEn >= 0 && t >= texturasEn) { texturasEn = -1; VuelcaTexturas(); }
                    }
                    r.TrasPresentar();
                    System.Threading.Thread.Sleep(16);
                }
                GlMas.DeleteFramebuffers(1, &f);
                r.Dispose();
                Console.WriteLine("Resumen: centro negro en " + negras + " de " + lecturas + " lecturas con el video empezado");
            }
            return 0;
        }

        // ROCOLA_VIDEO_TEXTURAS=S: todas las texturas de OpenGL (las de mpv: los planos del video, sus pasadas intermedias y las tablas de
        // pesos de sus escaladores) con su medida, su formato interno, sus filtros y lo que tienen (NaN, infinitos, minimo, maximo, centro);
        // de las tablas de pesos (256 filas), las filas con NaN. Asi se encontro la tabla con basura de mpv (ReproductorVideo.Inicia).
        [System.Runtime.InteropServices.DllImport("libGLESv2.so.2", EntryPoint = "glIsTexture")] private static extern byte IsTexture(uint t);
        [System.Runtime.InteropServices.DllImport("libGLESv2.so.2", EntryPoint = "glGetTexLevelParameteriv")] private static unsafe extern void GetTexLevelParameteriv(uint d, int nivel, uint q, int* v);
        [System.Runtime.InteropServices.DllImport("libGLESv2.so.2", EntryPoint = "glGetTexParameteriv")] private static unsafe extern void GetTexParameteriv(uint d, uint q, int* v);
        private const uint TEXTURE_BINDING_2D = 0x8069, ACTIVE_TEXTURE = 0x84E0, TEXTURE_WIDTH = 0x1000, TEXTURE_HEIGHT = 0x1001,
            TEXTURE_INTERNAL_FORMAT = 0x1003, TEXTURE_MIN_FILTER = 0x2801, TEXTURE_MAG_FILTER = 0x2800;
        private static unsafe void VuelcaTexturas()
        {
            int antes; Rocola.Pantalla.GL.GetIntegerv(Rocola.Pantalla.GL.FRAMEBUFFER_BINDING, &antes);
            int unidad; Rocola.Pantalla.GL.GetIntegerv(ACTIVE_TEXTURE, &unidad);
            Rocola.Pantalla.GL.ActiveTexture(Rocola.Pantalla.GL.TEXTURE0);
            int ligada; Rocola.Pantalla.GL.GetIntegerv(TEXTURE_BINDING_2D, &ligada);
            uint f; Rocola.Pantalla.GL.GenFramebuffers(1, &f);
            Rocola.Pantalla.GL.BindFramebuffer(Rocola.Pantalla.GL.FRAMEBUFFER, f);
            Rocola.Pantalla.GL.PixelStorei(Rocola.Pantalla.GL.PACK_ALIGNMENT, 4);
            for (uint t = 1; t < 1000; t++)
            {
                if (IsTexture(t) == 0) continue;
                GlMas.GetError();
                Rocola.Pantalla.GL.BindTexture(Rocola.Pantalla.GL.TEXTURE_2D, t);
                if (GlMas.GetError() != 0) { Console.WriteLine("textura " + t + ": no es 2D"); continue; }
                int w = 0, h = 0, fi = 0, fmin = 0, fmag = 0;
                GetTexLevelParameteriv(Rocola.Pantalla.GL.TEXTURE_2D, 0, TEXTURE_WIDTH, &w); GetTexLevelParameteriv(Rocola.Pantalla.GL.TEXTURE_2D, 0, TEXTURE_HEIGHT, &h);
                GetTexLevelParameteriv(Rocola.Pantalla.GL.TEXTURE_2D, 0, TEXTURE_INTERNAL_FORMAT, &fi);
                GetTexParameteriv(Rocola.Pantalla.GL.TEXTURE_2D, TEXTURE_MIN_FILTER, &fmin); GetTexParameteriv(Rocola.Pantalla.GL.TEXTURE_2D, TEXTURE_MAG_FILTER, &fmag);
                System.Text.StringBuilder sb = new System.Text.StringBuilder("textura " + t + ": " + w + "x" + h + " formato " + fi.ToString("x") + " filtros " + fmin.ToString("x") + "/" + fmag.ToString("x"));
                Rocola.Pantalla.GL.FramebufferTexture2D(Rocola.Pantalla.GL.FRAMEBUFFER, Rocola.Pantalla.GL.COLOR_ATTACHMENT0, Rocola.Pantalla.GL.TEXTURE_2D, t, 0);
                if (w > 0 && h > 0 && (long)w * h <= 4096 * 4096 && Rocola.Pantalla.GL.CheckFramebufferStatus(Rocola.Pantalla.GL.FRAMEBUFFER) == Rocola.Pantalla.GL.FRAMEBUFFER_COMPLETE)
                {
                    int c = ((h / 2) * w + w / 2) * 4;
                    float[] pf = new float[w * h * 4];
                    fixed (float* p = pf) Rocola.Pantalla.GL.ReadPixels(0, 0, w, h, Rocola.Pantalla.GL.RGBA, Rocola.Pantalla.GL.FLOAT, p);
                    if (GlMas.GetError() == 0)   // las de coma flotante
                    {
                        int nan = 0, inf = 0; float mn = float.MaxValue, mx = float.MinValue;
                        foreach (float v in pf) { if (float.IsNaN(v)) nan++; else if (float.IsInfinity(v)) inf++; else { if (v < mn) mn = v; if (v > mx) mx = v; } }
                        sb.Append(" NaN ").Append(nan).Append(" infinitos ").Append(inf).Append(" minimo ").Append(mn).Append(" maximo ").Append(mx)
                          .Append(" centro ").Append(pf[c]).Append(',').Append(pf[c + 1]).Append(',').Append(pf[c + 2]).Append(',').Append(pf[c + 3]);
                        if (h == 256 && w <= 16)   // la tabla de pesos de un escalador: una fila por posicion, 4 pesos por punto de la textura
                            for (int y = 0; y < h; y++)
                            {
                                bool conNan = false;
                                for (int k = 0; k < w * 4; k++) if (float.IsNaN(pf[y * w * 4 + k])) conNan = true;
                                if (!conNan) continue;
                                sb.Append("\n   fila ").Append(y).Append(':');
                                for (int k = 0; k < w * 4; k++) sb.Append(' ').Append(pf[y * w * 4 + k].ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
                            }
                    }
                    else
                    {
                        byte[] pb = new byte[w * h * 4];
                        fixed (byte* p = pb) Rocola.Pantalla.GL.ReadPixels(0, 0, w, h, Rocola.Pantalla.GL.RGBA, Rocola.Pantalla.GL.UNSIGNED_BYTE, p);
                        if (GlMas.GetError() == 0) sb.Append(" centro ").Append(pb[c]).Append(',').Append(pb[c + 1]).Append(',').Append(pb[c + 2]).Append(',').Append(pb[c + 3]);
                    }
                }
                Rocola.Pantalla.GL.FramebufferTexture2D(Rocola.Pantalla.GL.FRAMEBUFFER, Rocola.Pantalla.GL.COLOR_ATTACHMENT0, Rocola.Pantalla.GL.TEXTURE_2D, 0, 0);
                Console.WriteLine(sb.ToString());
            }
            GlMas.DeleteFramebuffers(1, &f);
            Rocola.Pantalla.GL.BindTexture(Rocola.Pantalla.GL.TEXTURE_2D, (uint)ligada);
            Rocola.Pantalla.GL.ActiveTexture((uint)unidad);
            Rocola.Pantalla.GL.BindFramebuffer(Rocola.Pantalla.GL.FRAMEBUFFER, (uint)antes);
            GlMas.GetError();
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
                byte[] px = s.Dispositivo.LeeFondo(out bw, out bh);   // (Nativo/DispositivoTarjetaGl.cs)
                if (px == null) { Console.WriteLine("Sin fondo que leer"); return 1; }
                using (SKBitmap b = new SKBitmap(new SKImageInfo(bw, bh, SKColorType.Bgra8888, SKAlphaType.Opaque)))
                {
                    fixed (byte* p = px) Buffer.MemoryCopy(p, (void*)b.GetPixels(), px.Length, px.Length);
                    using (SKData d = b.Encode(SKEncodedImageFormat.Png, 100)) using (System.IO.FileStream f = System.IO.File.Create(salida)) d.SaveTo(f);
                }
                Console.WriteLine(cuadros + " cuadros en " + ms.ToString("0.0") + " ms (" + (ms / cuadros).ToString("0.00") + " ms por cuadro); texturas vivas: " + TexturasGl.Vivas);
                s.Dispose();
                Console.WriteLine("texturas vivas al soltar: " + TexturasGl.Vivas);
            }
            return 0;
        }
    }
}
