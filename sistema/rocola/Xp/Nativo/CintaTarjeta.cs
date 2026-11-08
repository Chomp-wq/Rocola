// LA GRABADORA DE CINTAS DE LA TARJETA (pruebas del port nativo, 8-11-2026; no es de XP).
//
// Con ROCOLA_CINTA=ruta, cada llamada que el codigo de XP le hace al dispositivo de la tarjeta (D3D9Star.cs, PerformanceGpu.cs,
// TexturasCompatiblesGpu.cs) se apunta en un archivo, con todo lo que el dispositivo lee: los vertices de cada dibujo, los puntos de cada
// mapa de bits que se sube (la zona que se sube), las zonas, la imagen intermedia, la pasada de dos texturas, los cuadros, los cambios
// de tamano, lo que se suelta y los dos campos que XP escribe directo (VinetaPuntos y CubeBaseColor). Despues, el reproductor
// (sistema/rocola/Xp/Pruebas/Tarjeta) hace las mismas llamadas en el dispositivo de la copia congelada (la version con capas) y en el
// traducido, y compara el fondo de cada cuadro byte a byte: la cinta quita el reloj y el azar de en medio (las estrellas fugaces y el
// titileo salen de un Random sin semilla, GpuStarfield.srnd).
//
// APAGADA (sin ROCOLA_CINTA) NO HACE NADA: cada metodo del dispositivo empieza con
//     if (CintaTarjeta.Graba && CintaTarjeta.Fuera) ...   // (pruebas: ROCOLA_CINTA)
// y 'Graba' es un static readonly falso: el dispositivo sigue como si la linea no estuviera.
// ENCENDIDA, esa linea pasa la llamada por aqui: se hace la llamada de verdad (con 'dentro' puesto, para no apuntar dos veces lo que el
// dispositivo se llama a si mismo) y despues se apunta con sus resultados.
//
// Variables (pruebas):
//   ROCOLA_CINTA=ruta              donde se graba (Brotli). Se cierra al salir el programa.
//   ROCOLA_CINTA_CUADROS=a-b,c-d   (segundos desde que arranco el programa) solo se apuntan los cuadros que empiezan en esos tramos; lo
//                                  demas (crear, subir y soltar texturas, cambios de tamano, el dispositivo) siempre, para que el
//                                  reproductor tenga las mismas texturas. Sin ella, todos los cuadros.
//
// FORMATO (todo en little endian): "ROCOLA-CINTA-TARJETA 1\n" y registros [byte tipo][int bytes][datos]:
//   1 NUEVO    int disp, long ventana, int ancho, int alto, double ms
//   2 LLAMADA  int disp, byte metodo, double ms, argumentos y resultados (ver Escribe*, en el orden de cada metodo)
//   3 SUELTA   long textura, bool hiloDeLaPantalla, double ms
//   4 CAMPO    int disp, byte campo (1 VinetaPuntos, 2 CubeBaseColor), uint valor
// Vertices: int largo del arreglo (-1 = null), int cuantos se guardan, y sus bytes (28 o 36 por vertice). Mapa de bits: int id (-1 =
// null; el mismo objeto, el mismo id), int ancho, int alto, int formato, int x, int y, int w, int h (la zona que se guarda; w = 0:
// ninguna) y sus puntos tal como estan guardados (BGRA premultiplicado, fila a fila).
#if ROCOLA_GPU
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Rocola
{
    internal static unsafe class CintaTarjeta
    {
        internal static readonly bool Graba = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ROCOLA_CINTA"));
        [ThreadStatic] private static int dentro;
        internal static bool Fuera { get { return dentro == 0; } }

        // los metodos (el numero va en la cinta: no cambiarlos)
        internal const byte MNuevo = 0, MRedimensiona = 1, MPreparaDispositivo = 2, MPreloadTexture = 3, MCreateTextureFromBitmap = 4,
            MBeginFrame = 5, MEndFrame = 6, MDrawSky = 7, MDrawStars = 8, MDrawShootStreaks = 9, MDrawMesh = 10, MDrawMeshAdd = 11,
            MDrawTexQuad = 12, MDrawOpaqueQuad = 13, MDrawQuads = 14, MDrawQuadsWrap = 15, MDrawCube = 16, MCreateTexturePow2 = 17,
            MUpdateTextureRegion = 18, MUpdateTexturePow2 = 19, MDispose = 20, MEmpiezaCuenta = 21, MEmpiezaMedia = 22, MTerminaMedia = 23,
            MDibujaMedia = 24, MDrawSkyN = 25, MDibujaDosTexturas = 26, MPideSinEspera = 27, MCreaPortadaCompartida = 28;
        internal const byte TBloque = 0, TNuevo = 1, TLlamada = 2, TSuelta = 3, TCampo = 4;

        private static readonly object candado = new object();
        private static Stream salida;
        private static bool roto;
        private static readonly double[] tramos = LeeTramos(Environment.GetEnvironmentVariable("ROCOLA_CINTA_CUADROS"));
        private static readonly Stopwatch reloj = Stopwatch.StartNew();
        private static readonly double origenMs = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
        private static double AhoraMs { get { return origenMs + reloj.Elapsed.TotalMilliseconds; } }
        private static int hiloPantalla = -1;
        private static int siguienteDisp, siguienteMapa;
        private sealed class Disp { internal int Id; internal bool EnCuadro, CuadroGrabado; internal bool Vineta; internal uint Cubo; internal bool CamposVistos; }
        private static readonly ConditionalWeakTable<DispositivoTarjeta, Disp> disps = new ConditionalWeakTable<DispositivoTarjeta, Disp>();
        private sealed class IdMapa { internal int Id; }
        private static readonly ConditionalWeakTable<Bitmap, IdMapa> mapas = new ConditionalWeakTable<Bitmap, IdMapa>();
        [ThreadStatic] private static MemoryStream ms;
        [ThreadStatic] private static BinaryWriter bw;
        internal static long Registros, Bytes;

        private static double[] LeeTramos(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            List<double> l = new List<double>();
            foreach (string t in s.Split(','))
            {
                string[] p = t.Split('-');
                double a, b;
                if (p.Length == 2 && double.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out a)
                    && double.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out b)) { l.Add(a * 1000); l.Add(b * 1000); }
            }
            return l.Count > 0 ? l.ToArray() : null;
        }
        private static bool EnTramo(double ms)
        {
            if (tramos == null) return true;
            for (int i = 0; i + 1 < tramos.Length; i += 2) if (ms >= tramos[i] && ms <= tramos[i + 1]) return true;
            return false;
        }

        private static void Abre()
        {
            if (salida != null || roto) return;
            try
            {
                FileStream f = new FileStream(Environment.GetEnvironmentVariable("ROCOLA_CINTA"), FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 20);
                salida = new BufferedStream(new BrotliStream(f, CompressionLevel.Fastest), 1 << 20);
                byte[] cab = Encoding.ASCII.GetBytes("ROCOLA-CINTA-TARJETA 1\n");
                salida.Write(cab, 0, cab.Length);
                AppDomain.CurrentDomain.ProcessExit += delegate { Cierra(); };
                Console.WriteLine("Cinta de la tarjeta: " + Environment.GetEnvironmentVariable("ROCOLA_CINTA"));
            }
            catch (Exception ex) { roto = true; Console.Error.WriteLine("Cinta de la tarjeta: no se puede grabar (" + ex.Message + ")"); }
        }
        internal static void Cierra()
        {
            lock (candado)
            {
                if (salida == null) return;
                try { salida.Flush(); salida.Dispose(); } catch { }
                salida = null; roto = true;
                Console.WriteLine("Cinta de la tarjeta: " + Registros + " registros, " + Bytes + " bytes sin comprimir");
            }
        }

        // ---- armar un registro (en la memoria de este hilo) y echarlo a la cinta de una vez ----
        private static BinaryWriter Empieza(byte tipo)
        {
            if (ms == null) { ms = new MemoryStream(1 << 16); bw = new BinaryWriter(ms); }
            ms.SetLength(0);
            bw.Write(tipo); bw.Write(0);   // (los bytes se ponen al acabar)
            return bw;
        }
        private static void Termina()
        {
            bw.Flush();
            long n = ms.Length;
            byte[] b = ms.GetBuffer();
            int largo = (int)(n - 5);
            b[1] = (byte)largo; b[2] = (byte)(largo >> 8); b[3] = (byte)(largo >> 16); b[4] = (byte)(largo >> 24);
            lock (candado)
            {
                Abre();
                if (salida == null) return;
                try { salida.Write(b, 0, (int)n); Registros++; Bytes += n; }
                catch (Exception ex) { roto = true; Console.Error.WriteLine("Cinta de la tarjeta: fallo al escribir (" + ex.Message + ")"); try { salida.Dispose(); } catch { } salida = null; }
            }
        }
        private static Disp DeDisp(DispositivoTarjeta d)
        {
            Disp x;
            lock (candado)
            {
                if (!disps.TryGetValue(d, out x)) { x = new Disp(); x.Id = ++siguienteDisp; disps.Add(d, x); }
            }
            return x;
        }
        // Lo que XP escribe directo en el dispositivo (sin llamada): se apunta antes de la llamada que lo usa, si cambio.
        private static void Campos(DispositivoTarjeta d, Disp x)
        {
            if (x.CamposVistos && x.Vineta == d.VinetaPuntos && x.Cubo == d.CubeBaseColor) return;
            if (!x.CamposVistos || x.Vineta != d.VinetaPuntos) { BinaryWriter w = Empieza(TCampo); w.Write(x.Id); w.Write((byte)1); w.Write(d.VinetaPuntos ? 1u : 0u); Termina(); }
            if (!x.CamposVistos || x.Cubo != d.CubeBaseColor) { BinaryWriter w = Empieza(TCampo); w.Write(x.Id); w.Write((byte)2); w.Write(d.CubeBaseColor); Termina(); }
            x.Vineta = d.VinetaPuntos; x.Cubo = d.CubeBaseColor; x.CamposVistos = true;
        }
        // ¿Se apunta esta llamada? Lo de las texturas y el dispositivo, siempre; lo de dentro de un cuadro, si el cuadro se graba.
        private static BinaryWriter Llamada(Disp x, byte metodo, bool siempre)
        {
            if (!siempre && !(x.EnCuadro && x.CuadroGrabado)) return null;
            BinaryWriter w = Empieza(TLlamada);
            w.Write(x.Id); w.Write(metodo); w.Write(AhoraMs);
            return w;
        }
        private static void Vertices(BinaryWriter w, Tarjeta.Vtx[] v, int necesarios)
        {
            if (v == null) { w.Write(-1); w.Write(0); return; }
            int n = Math.Max(0, Math.Min(necesarios, v.Length));
            w.Write(v.Length); w.Write(n);
            if (n > 0) w.Write(MemoryMarshal.AsBytes(new ReadOnlySpan<Tarjeta.Vtx>(v, 0, n)));
        }
        private static void Vertices2(BinaryWriter w, Tarjeta.Vtx2[] v, int necesarios)
        {
            if (v == null) { w.Write(-1); w.Write(0); return; }
            int n = Math.Max(0, Math.Min(necesarios, v.Length));
            w.Write(v.Length); w.Write(n);
            if (n > 0) w.Write(MemoryMarshal.AsBytes(new ReadOnlySpan<Tarjeta.Vtx2>(v, 0, n)));
        }
        private static void Mapa(BinaryWriter w, Bitmap b, Rectangle zona)
        {
            if (b == null || b.Sk == null) { w.Write(-1); return; }
            IdMapa id;
            lock (candado) { if (!mapas.TryGetValue(b, out id)) { id = new IdMapa(); id.Id = ++siguienteMapa; mapas.Add(b, id); } }
            w.Write(id.Id); w.Write(b.Width); w.Write(b.Height); w.Write((int)b.PixelFormat);
            Rectangle r = Rectangle.Intersect(zona, new Rectangle(0, 0, b.Width, b.Height));
            if (r.Width <= 0 || r.Height <= 0 || b.Sk.ColorType != SkiaSharp.SKColorType.Bgra8888 || b.Sk.AlphaType != SkiaSharp.SKAlphaType.Premul)
            { w.Write(0); w.Write(0); w.Write(0); w.Write(0); return; }
            w.Write(r.X); w.Write(r.Y); w.Write(r.Width); w.Write(r.Height);
            byte* p = (byte*)b.Sk.GetPixels(); int rb = b.Sk.RowBytes;
            for (int y = 0; y < r.Height; y++) w.Write(new ReadOnlySpan<byte>(p + (long)(r.Y + y) * rb + (long)r.X * 4, r.Width * 4));
        }
        private static void Antes(DispositivoTarjeta d, out Disp x) { x = DeDisp(d); if (hiloPantalla < 0) hiloPantalla = Thread.CurrentThread.ManagedThreadId; dentro++; }
        private static void Despues() { dentro--; }
        private static void Falla(Exception ex) { if (!roto) Console.Error.WriteLine("Cinta de la tarjeta: " + ex.Message); }

        // ================================================================ las llamadas
        internal static void Nuevo(DispositivoTarjeta d, IntPtr ventana, int w, int h)
        {
            try
            {
                Disp x = DeDisp(d);
                if (hiloPantalla < 0) hiloPantalla = Thread.CurrentThread.ManagedThreadId;
                BinaryWriter b = Empieza(TNuevo); b.Write(x.Id); b.Write((long)ventana); b.Write(w); b.Write(h); b.Write(AhoraMs); Termina();
            }
            catch (Exception ex) { Falla(ex); }
        }
        internal static void Suelta(IntPtr t)
        {
            dentro++;
            try { Tarjeta.Suelta(t); }
            finally { dentro--; }
            try { BinaryWriter b = Empieza(TSuelta); b.Write((long)t); b.Write(Thread.CurrentThread.ManagedThreadId == hiloPantalla); b.Write(AhoraMs); Termina(); }
            catch (Exception ex) { Falla(ex); }
        }
        internal static bool Redimensiona(DispositivoTarjeta d, int w, int h)
        {
            Disp x; Antes(d, out x); bool r;
            try { r = d.Redimensiona(w, h); } finally { Despues(); }
            try { BinaryWriter b = Llamada(x, MRedimensiona, true); b.Write(w); b.Write(h); b.Write(r); Termina(); } catch (Exception ex) { Falla(ex); }
            return r;
        }
        internal static bool PreparaDispositivo(DispositivoTarjeta d)
        {
            Disp x; Antes(d, out x); bool r;
            try { r = d.PreparaDispositivo(); } finally { Despues(); }
            try { BinaryWriter b = Llamada(x, MPreparaDispositivo, true); b.Write(r); Termina(); } catch (Exception ex) { Falla(ex); }
            return r;
        }
        internal static bool PreloadTexture(DispositivoTarjeta d, IntPtr t)
        {
            Disp x; Antes(d, out x); bool r;
            try { r = d.PreloadTexture(t); } finally { Despues(); }
            try { BinaryWriter b = Llamada(x, MPreloadTexture, true); b.Write((long)t); b.Write(r); Termina(); } catch (Exception ex) { Falla(ex); }
            return r;
        }
        internal static IntPtr CreateTextureFromBitmap(DispositivoTarjeta d, Bitmap bmp)
        {
            Disp x; Antes(d, out x); IntPtr r;
            try { r = d.CreateTextureFromBitmap(bmp); } finally { Despues(); }
            try { BinaryWriter b = Llamada(x, MCreateTextureFromBitmap, true); Mapa(b, bmp, bmp != null ? new Rectangle(0, 0, bmp.Width, bmp.Height) : Rectangle.Empty); b.Write((long)r); Termina(); } catch (Exception ex) { Falla(ex); }
            return r;
        }
        internal static IntPtr CreateTexturePow2(DispositivoTarjeta d, Bitmap bmp, out float u, out float v)
        {
            Disp x; Antes(d, out x); IntPtr r;
            try { r = d.CreateTexturePow2(bmp, out u, out v); } finally { Despues(); }
            try { BinaryWriter b = Llamada(x, MCreateTexturePow2, true); Mapa(b, bmp, bmp != null ? new Rectangle(0, 0, bmp.Width, bmp.Height) : Rectangle.Empty); b.Write((long)r); b.Write(u); b.Write(v); Termina(); } catch (Exception ex) { Falla(ex); }
            return r;
        }
        internal static IntPtr CreaPortadaCompartida(DispositivoTarjeta d, Bitmap bmp, out float u, out float v)
        {
            Disp x; Antes(d, out x); IntPtr r;
            try { r = d.CreaPortadaCompartida(bmp, out u, out v); } finally { Despues(); }
            try { BinaryWriter b = Llamada(x, MCreaPortadaCompartida, true); Mapa(b, bmp, bmp != null ? new Rectangle(0, 0, bmp.Width, bmp.Height) : Rectangle.Empty); b.Write((long)r); b.Write(u); b.Write(v); Termina(); } catch (Exception ex) { Falla(ex); }
            return r;
        }
        internal static bool UpdateTextureRegion(DispositivoTarjeta d, IntPtr t, Bitmap bmp, Rectangle area)
        {
            Disp x; Antes(d, out x); bool r;
            try { r = d.UpdateTextureRegion(t, bmp, area); } finally { Despues(); }
            try { BinaryWriter b = Llamada(x, MUpdateTextureRegion, true); b.Write((long)t); Mapa(b, bmp, area); b.Write(area.X); b.Write(area.Y); b.Write(area.Width); b.Write(area.Height); b.Write(r); Termina(); } catch (Exception ex) { Falla(ex); }
            return r;
        }
        internal static bool UpdateTexturePow2(DispositivoTarjeta d, IntPtr t, Bitmap bmp, out float u, out float v)
        {
            Disp x; Antes(d, out x); bool r;
            try { r = d.UpdateTexturePow2(t, bmp, out u, out v); } finally { Despues(); }
            try { BinaryWriter b = Llamada(x, MUpdateTexturePow2, true); b.Write((long)t); Mapa(b, bmp, bmp != null ? new Rectangle(0, 0, bmp.Width, bmp.Height) : Rectangle.Empty); b.Write(r); b.Write(u); b.Write(v); Termina(); } catch (Exception ex) { Falla(ex); }
            return r;
        }
        internal static void Dispose(DispositivoTarjeta d)
        {
            Disp x; Antes(d, out x);
            try { d.Dispose(); } finally { Despues(); }
            try { BinaryWriter b = Llamada(x, MDispose, true); Termina(); x.EnCuadro = false; } catch (Exception ex) { Falla(ex); }
        }
        internal static bool PideSinEspera(DispositivoTarjeta d, bool sinEspera)
        {
            Disp x; Antes(d, out x); bool r;
            try { r = d.PideSinEspera(sinEspera); } finally { Despues(); }
            try { BinaryWriter b = Llamada(x, MPideSinEspera, true); b.Write(sinEspera); b.Write(r); Termina(); } catch (Exception ex) { Falla(ex); }
            return r;
        }
        internal static void EmpiezaCuenta(DispositivoTarjeta d, bool area)
        {
            Disp x; Antes(d, out x);
            try { d.EmpiezaCuenta(area); } finally { Despues(); }
            try { BinaryWriter b = Llamada(x, MEmpiezaCuenta, true); b.Write(area); Termina(); } catch (Exception ex) { Falla(ex); }
        }

        // ---- los cuadros ----
        internal static bool BeginFrame(DispositivoTarjeta d)
        {
            Disp x; Antes(d, out x); bool r;
            double t = AhoraMs;
            try { r = d.BeginFrame(); } finally { Despues(); }
            try
            {
                x.EnCuadro = r; x.CuadroGrabado = EnTramo(t);
                if (x.CuadroGrabado) { BinaryWriter b = Empieza(TLlamada); b.Write(x.Id); b.Write(MBeginFrame); b.Write(t); b.Write(r); Termina(); }
            }
            catch (Exception ex) { Falla(ex); }
            return r;
        }
        internal static bool EndFrame(DispositivoTarjeta d)
        {
            Disp x; Antes(d, out x); bool r;
            try { r = d.EndFrame(); } finally { Despues(); }
            try
            {
                BinaryWriter b = Llamada(x, MEndFrame, false);
                if (b != null) { b.Write(r); Termina(); }
                x.EnCuadro = false;
                if (b != null && r) lock (candado) { try { if (salida != null) salida.Flush(); } catch { } }
            }
            catch (Exception ex) { Falla(ex); }
            return r;
        }
        internal static bool EmpiezaMedia(DispositivoTarjeta d, int w, int h, int div)
        {
            Disp x; Antes(d, out x); bool r;
            try { r = d.EmpiezaMedia(w, h, div); } finally { Despues(); }
            try { BinaryWriter b = Llamada(x, MEmpiezaMedia, false); if (b != null) { b.Write(w); b.Write(h); b.Write(div); b.Write(r); Termina(); } } catch (Exception ex) { Falla(ex); }
            return r;
        }
        internal static bool TerminaMedia(DispositivoTarjeta d)
        {
            Disp x; Antes(d, out x); bool r;
            try { r = d.TerminaMedia(); } finally { Despues(); }
            try { BinaryWriter b = Llamada(x, MTerminaMedia, false); if (b != null) { b.Write(r); Termina(); } } catch (Exception ex) { Falla(ex); }
            return r;
        }
        internal static bool DibujaDosTexturas(DispositivoTarjeta d, IntPtr t0, IntPtr t1, Tarjeta.Vtx2[] v, int tris)
        {
            Disp x; Antes(d, out x); bool r;
            try { Campos2(d, x); r = d.DibujaDosTexturas(t0, t1, v, tris); } finally { Despues(); }
            try { BinaryWriter b = Llamada(x, MDibujaDosTexturas, false); if (b != null) { b.Write((long)t0); b.Write((long)t1); b.Write(tris); Vertices2(b, v, tris * 3); b.Write(r); Termina(); } } catch (Exception ex) { Falla(ex); }
            return r;
        }
        private static void Campos2(DispositivoTarjeta d, Disp x) { if (x.EnCuadro && x.CuadroGrabado) Campos(d, x); }

        // ---- los dibujos (los vertices que lee cada uno: DrawSky 12, DrawTexQuad y DrawOpaqueQuad 6, los demas tris * 3) ----
        private static void Dibujo(DispositivoTarjeta d, byte metodo, IntPtr tex, Tarjeta.Vtx[] v, int tris, int necesarios, bool conTex, bool conTris)
        {
            Disp x = DeDisp(d);
            if (!(x.EnCuadro && x.CuadroGrabado)) return;
            try
            {
                Campos(d, x);
                BinaryWriter b = Llamada(x, metodo, false);
                if (conTex) b.Write((long)tex);
                if (conTris) b.Write(tris);
                Vertices(b, v, necesarios);
                Termina();
            }
            catch (Exception ex) { Falla(ex); }
        }
        // (el dibujo se apunta ANTES de hacerlo: lo que se apunta es lo mismo, y asi los campos van antes del dibujo)
        internal static void DrawSky(DispositivoTarjeta d, Tarjeta.Vtx[] v) { Dibujo(d, MDrawSky, IntPtr.Zero, v, 4, 12, false, false); dentro++; try { d.DrawSky(v); } finally { dentro--; } }
        internal static void DrawStars(DispositivoTarjeta d, Tarjeta.Vtx[] v, int tris, IntPtr t) { Dibujo(d, MDrawStars, t, v, tris, tris * 3, true, true); dentro++; try { d.DrawStars(v, tris, t); } finally { dentro--; } }
        internal static void DrawShootStreaks(DispositivoTarjeta d, Tarjeta.Vtx[] v, int tris) { Dibujo(d, MDrawShootStreaks, IntPtr.Zero, v, tris, tris * 3, false, true); dentro++; try { d.DrawShootStreaks(v, tris); } finally { dentro--; } }
        internal static void DrawMesh(DispositivoTarjeta d, Tarjeta.Vtx[] v, int tris) { Dibujo(d, MDrawMesh, IntPtr.Zero, v, tris, tris * 3, false, true); dentro++; try { d.DrawMesh(v, tris); } finally { dentro--; } }
        internal static void DrawMeshAdd(DispositivoTarjeta d, Tarjeta.Vtx[] v, int tris) { Dibujo(d, MDrawMeshAdd, IntPtr.Zero, v, tris, tris * 3, false, true); dentro++; try { d.DrawMeshAdd(v, tris); } finally { dentro--; } }
        internal static void DrawTexQuad(DispositivoTarjeta d, IntPtr t, Tarjeta.Vtx[] v) { Dibujo(d, MDrawTexQuad, t, v, 2, 6, true, false); dentro++; try { d.DrawTexQuad(t, v); } finally { dentro--; } }
        internal static void DrawOpaqueQuad(DispositivoTarjeta d, IntPtr t, Tarjeta.Vtx[] v) { Dibujo(d, MDrawOpaqueQuad, t, v, 2, 6, true, false); dentro++; try { d.DrawOpaqueQuad(t, v); } finally { dentro--; } }
        internal static void DrawQuads(DispositivoTarjeta d, IntPtr t, Tarjeta.Vtx[] v, int tris) { Dibujo(d, MDrawQuads, t, v, tris, tris * 3, true, true); dentro++; try { d.DrawQuads(t, v, tris); } finally { dentro--; } }
        internal static void DrawQuadsWrap(DispositivoTarjeta d, IntPtr t, Tarjeta.Vtx[] v, int tris) { Dibujo(d, MDrawQuadsWrap, t, v, tris, tris * 3, true, true); dentro++; try { d.DrawQuadsWrap(t, v, tris); } finally { dentro--; } }
        internal static void DibujaMedia(DispositivoTarjeta d, Tarjeta.Vtx[] v, int tris) { Dibujo(d, MDibujaMedia, IntPtr.Zero, v, tris, tris * 3, false, true); dentro++; try { d.DibujaMedia(v, tris); } finally { dentro--; } }
        internal static void DrawSkyN(DispositivoTarjeta d, Tarjeta.Vtx[] v, int tris) { Dibujo(d, MDrawSkyN, IntPtr.Zero, v, tris, tris * 3, false, true); dentro++; try { d.DrawSkyN(v, tris); } finally { dentro--; } }
        internal static void DrawCube(DispositivoTarjeta d, IntPtr t, float uMax, float vMax, float ax, float ay, float aw, float ah, double angle, float alpha, double tilt)
        {
            Disp x = DeDisp(d);
            if (x.EnCuadro && x.CuadroGrabado)
                try
                {
                    Campos(d, x);
                    BinaryWriter b = Llamada(x, MDrawCube, false);
                    b.Write((long)t); b.Write(uMax); b.Write(vMax); b.Write(ax); b.Write(ay); b.Write(aw); b.Write(ah); b.Write(angle); b.Write(alpha); b.Write(tilt);
                    Termina();
                }
                catch (Exception ex) { Falla(ex); }
            dentro++; try { d.DrawCube(t, uMax, vMax, ax, ay, aw, ah, angle, alpha, tilt); } finally { dentro--; }
        }
    }
}
#endif
