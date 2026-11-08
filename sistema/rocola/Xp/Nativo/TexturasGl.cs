// LAS TEXTURAS DE LA TARJETA EN OPENGL ES 2 (port nativo de XP, 8-11-2026). Lo que el dispositivo de XP (DispositivoTarjeta) le pedia a
// Direct3D 9 de sus texturas, hecho con OpenGL:
//   - Nueva: glGenTextures + glTexImage2D (RGBA, sin datos), con el filtro LINEAR y la envoltura CLAMP desde el principio (XP pone LINEAR a
//     la etapa 0 en InitStates y a la 1 en la pasada de dos texturas; sin esto OpenGL deja NEAREST_MIPMAP_LINEAR y una textura de un
//     nivel sale negra). Sin memoria (GL_OUT_OF_MEMORY) devuelve 0: lo que en Direct3D era D3DERR_OUTOFVIDEOMEMORY.
//   - Framebuffer: para pintar en una textura (el fondo, la imagen intermedia). Las de XP que eran X8R8G8B8 se ponen a (0, 0, 0, 1): el
//     dispositivo nunca escribe el alfa (glColorMask), asi que el alfa se lee 1 siempre, como en Direct3D.
//   - Sube: glTexSubImage2D por la unidad 2 (no toca las texturas puestas en las unidades 0 y 1 para dibujar), desde una memoria aparte
//     que se reusa (lo que en Direct3D era LockRect y UnlockRect: XP escribe los puntos en la memoria y se suben al soltarla).
//   - Medida: la medida guardada al crearla (GetLevelDesc). Existe: si es una textura de la tarjeta (las otras, D3DERR_INVALIDCALL).
//   - Envoltura: CLAMP o REPEAT de la textura puesta en la unidad activa. En Direct3D 9 la direccion es de la etapa (SetSamplerState); en
//     OpenGL ES 2 es de la textura: el dispositivo se la pone a cada textura al ponerla, y se apunta para no repetir la llamada.
//   - Borra: glDeleteTextures. Si llega de otro hilo (OpenGL es del hilo de la pantalla), se borra en el siguiente cuadro
//     (BorraPendientes). Y se avisa a los dispositivos (AlBorrar): OpenGL repite los nombres, y uno que la tuviera apuntada como puesta
//     tiene que olvidarla.
//   - Bytes: lo que ocupan en la tarjeta las texturas de la rocola (MemoriaTarjeta lo descuenta, como GetAvailableTextureMem).
#if ROCOLA_GPU
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Rocola.Pantalla;

namespace Rocola
{
    internal static unsafe class TexturasGl
    {
        private sealed class Info { internal int Ancho, Alto; internal bool Repite; internal long Bytes; }
        private static readonly object candado = new object();
        private static readonly Dictionary<uint, Info> registro = new Dictionary<uint, Info>();
        private static readonly List<uint> porBorrar = new List<uint>(), fbosPorBorrar = new List<uint>();
        private static int hiloGl = -1;
        private static long bytes;
        internal static long Bytes { get { return Interlocked.Read(ref bytes); } }
        internal static int Vivas { get { lock (candado) return registro.Count; } }
        // los dispositivos que apuntan la textura puesta (DispositivoTarjeta.OlvidaTextura)
        internal static event Action<uint> AlBorrar;
        private static bool EnHiloGl { get { return hiloGl < 0 || Thread.CurrentThread.ManagedThreadId == hiloGl; } }

        // GL_MAX_TEXTURE_SIZE (lo que en Direct3D 9 son MaxTextureWidth y MaxTextureHeight)
        private static int maxima;
        internal static int Maxima()
        {
            if (maxima > 0) return maxima;
            int v = 0; GL.GetIntegerv(GlMas.MAX_TEXTURE_SIZE, &v);
            maxima = v > 0 ? v : 2048;
            return maxima;
        }

        // Una textura nueva de ancho x alto, sin datos, LINEAR y CLAMP (la unidad 2, y despues la 0 activa). 0 si no hay memoria (o si
        // OpenGL no la hace: mas grande que GL_MAX_TEXTURE_SIZE, que en Direct3D era D3DERR_INVALIDCALL).
        internal static uint Nueva(int ancho, int alto)
        {
            if (hiloGl < 0) hiloGl = Thread.CurrentThread.ManagedThreadId;
            if (ancho <= 0 || alto <= 0 || ancho > Maxima() || alto > Maxima()) return 0;
            while (GlMas.GetError() != 0) { }
            uint n; GL.GenTextures(1, &n);
            GL.ActiveTexture(GL.TEXTURE0 + 2);
            GL.BindTexture(GL.TEXTURE_2D, n);
            GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, ancho, alto, 0, GL.RGBA, GL.UNSIGNED_BYTE, null);
            if (GlMas.GetError() != 0) { GL.DeleteTextures(1, &n); GL.ActiveTexture(GL.TEXTURE0); return 0; }
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GL.LINEAR); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GL.LINEAR);
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)GL.CLAMP_TO_EDGE); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)GL.CLAMP_TO_EDGE);
            GL.ActiveTexture(GL.TEXTURE0);
            Info i = new Info { Ancho = ancho, Alto = alto, Bytes = (long)ancho * alto * 4 };
            lock (candado) registro[n] = i;
            Interlocked.Add(ref bytes, i.Bytes);
            return n;
        }

        // El framebuffer para pintar en la textura. sinAlfa (lo que era X8R8G8B8): se pone a (0, 0, 0, 1). Deja puestos el framebuffer y la
        // mascara de color que habia. 0 si OpenGL no lo deja pintar.
        internal static uint Framebuffer(uint textura, bool sinAlfa)
        {
            uint f; GL.GenFramebuffers(1, &f);
            if (!Adjunta(f, textura, sinAlfa)) { GlMas.DeleteFramebuffers(1, &f); return 0; }
            return f;
        }
        // El mismo framebuffer, a pintar en otra textura (el fondo de un dispositivo, al cambiar de tamano: su framebuffer no cambia).
        internal static bool Adjunta(uint f, uint textura, bool sinAlfa)
        {
            int antes = 0; GL.GetIntegerv(GL.FRAMEBUFFER_BINDING, &antes);
            byte* mascara = stackalloc byte[4]; GlMas.GetBooleanv(GlMas.COLOR_WRITEMASK, mascara);
            bool recorte = GlMas.IsEnabled(GL.SCISSOR_TEST) != 0;
            GL.BindFramebuffer(GL.FRAMEBUFFER, f);
            GL.FramebufferTexture2D(GL.FRAMEBUFFER, GL.COLOR_ATTACHMENT0, GL.TEXTURE_2D, textura, 0);
            bool ok = GL.CheckFramebufferStatus(GL.FRAMEBUFFER) == GL.FRAMEBUFFER_COMPLETE;
            if (ok && sinAlfa)
            {
                GlMas.ColorMask(1, 1, 1, 1); GL.Disable(GL.SCISSOR_TEST);
                GL.ClearColor(0, 0, 0, 1); GL.Clear(GL.COLOR_BUFFER_BIT);
                GlMas.ColorMask(mascara[0], mascara[1], mascara[2], mascara[3]);
                if (recorte) GL.Enable(GL.SCISSOR_TEST);
            }
            GL.BindFramebuffer(GL.FRAMEBUFFER, (uint)antes);
            return ok;
        }
        internal static void BorraFramebuffer(uint f)
        {
            if (f == 0) return;
            if (!EnHiloGl) { lock (candado) fbosPorBorrar.Add(f); return; }
            GlMas.DeleteFramebuffers(1, &f);
        }

        // ---- subir ----
        // La memoria aparte donde XP escribe los puntos antes de subirlos (B, G, R, A, filas seguidas): una que se reusa y, si esta en uso,
        // otra (propia = true: se suelta al acabar).
        private static IntPtr memoria; private static long memoriaBytes; private static bool memoriaOcupada;
        internal static IntPtr Memoria(long cuantos, out bool propia)
        {
            lock (candado)
            {
                if (!memoriaOcupada)
                {
                    if (memoriaBytes < cuantos)
                    {
                        if (memoria != IntPtr.Zero) NativeMemory.Free((void*)memoria);
                        memoria = (IntPtr)NativeMemory.Alloc((nuint)Math.Max(1L, cuantos)); memoriaBytes = cuantos;
                    }
                    memoriaOcupada = true; propia = false;
                    return memoria;
                }
            }
            propia = true;
            return (IntPtr)NativeMemory.Alloc((nuint)Math.Max(1L, cuantos));
        }
        internal static void SueltaMemoria(IntPtr m, bool propia)
        {
            if (propia) { if (m != IntPtr.Zero) NativeMemory.Free((void*)m); }
            else lock (candado) memoriaOcupada = false;
        }
        // glTexSubImage2D de la zona (x, y, ancho, alto) desde 'datos' (filas de ancho * 4 bytes), por la unidad 2. False si la zona no
        // cabe en la textura o la textura no es de la tarjeta (en Direct3D, LockRect daba D3DERR_INVALIDCALL).
        internal static bool Sube(uint textura, int x, int y, int ancho, int alto, IntPtr datos)
        {
            Info i;
            lock (candado) if (!registro.TryGetValue(textura, out i)) return false;
            if (x < 0 || y < 0 || ancho <= 0 || alto <= 0 || x + ancho > i.Ancho || y + alto > i.Alto) return false;
            GL.ActiveTexture(GL.TEXTURE0 + 2);
            GL.BindTexture(GL.TEXTURE_2D, textura);
            GL.PixelStorei(GL.UNPACK_ALIGNMENT, 4);
            GL.TexSubImage2D(GL.TEXTURE_2D, 0, x, y, ancho, alto, GL.RGBA, GL.UNSIGNED_BYTE, (void*)datos);
            GL.ActiveTexture(GL.TEXTURE0);
            return true;
        }

        internal static bool Medida(uint textura, out int ancho, out int alto)
        {
            Info i;
            lock (candado) if (registro.TryGetValue(textura, out i)) { ancho = i.Ancho; alto = i.Alto; return true; }
            ancho = alto = 0;
            return false;
        }
        internal static bool Existe(uint textura) { lock (candado) return registro.ContainsKey(textura); }

        // CLAMP (repetir = false) o REPEAT de la textura, que esta puesta en la unidad activa. Solo si cambia.
        internal static void Envoltura(uint textura, bool repetir)
        {
            Info i;
            lock (candado) if (!registro.TryGetValue(textura, out i)) return;
            if (i.Repite == repetir) return;
            int v = repetir ? (int)GlMas.REPEAT : (int)GL.CLAMP_TO_EDGE;
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, v); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, v);
            i.Repite = repetir;
        }

        // ---- borrar ----
        internal static void Borra(uint textura)
        {
            Info i;
            lock (candado) { if (!registro.TryGetValue(textura, out i)) return; registro.Remove(textura); }
            Interlocked.Add(ref bytes, -i.Bytes);
            if (!EnHiloGl) { lock (candado) porBorrar.Add(textura); return; }
            GL.DeleteTextures(1, &textura);
            Action<uint> a = AlBorrar;
            if (a != null) a(textura);
        }
        // lo que se solto desde otro hilo (al empezar cada cuadro y al presentar)
        internal static void BorraPendientes()
        {
            uint[] t = null, f = null;
            lock (candado)
            {
                if (porBorrar.Count > 0) { t = porBorrar.ToArray(); porBorrar.Clear(); }
                if (fbosPorBorrar.Count > 0) { f = fbosPorBorrar.ToArray(); fbosPorBorrar.Clear(); }
            }
            if (t != null) foreach (uint n0 in t) { uint n = n0; GL.DeleteTextures(1, &n); Action<uint> a = AlBorrar; if (a != null) a(n); }
            if (f != null) foreach (uint n0 in f) { uint n = n0; GlMas.DeleteFramebuffers(1, &n); }
        }
    }
}
#endif
