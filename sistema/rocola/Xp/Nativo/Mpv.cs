// LIBMPV, EL REPRODUCTOR DE VIDEO DE ROCOLA OS (port nativo de XP, 8-11-2026). Las llamadas a libmpv.so.2 que usa el reproductor de los
// videos (ReproductorVideo.cs): crear, opciones, propiedades, ordenes, avisos y su API de render con OpenGL (libmpv pinta cada cuadro en una
// textura del mismo contexto de OpenGL que la tarjeta). Rocola OS trae libmpv (BR2_PACKAGE_MPV); sin ella, el video no
// abre (ReproductorVideo.Disponible = false), como un XP sin el reproductor de Windows Media.
using System;
using System.Runtime.InteropServices;

namespace Rocola
{
    internal static unsafe class Mpv
    {
        private const string L = "libmpv.so.2";
        [DllImport(L)] internal static extern IntPtr mpv_create();
        [DllImport(L)] internal static extern int mpv_initialize(IntPtr ctx);
        [DllImport(L)] internal static extern void mpv_terminate_destroy(IntPtr ctx);
        [DllImport(L)] internal static extern int mpv_set_option_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string nombre, [MarshalAs(UnmanagedType.LPUTF8Str)] string valor);
        [DllImport(L)] internal static extern int mpv_set_property_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string nombre, [MarshalAs(UnmanagedType.LPUTF8Str)] string valor);
        [DllImport(L)] internal static extern int mpv_get_property(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string nombre, int formato, void* dato);
        [DllImport(L)] internal static extern int mpv_command(IntPtr ctx, IntPtr* args);
        [DllImport(L)] internal static extern IntPtr mpv_wait_event(IntPtr ctx, double espera);
        [DllImport(L)] internal static extern void mpv_wakeup(IntPtr ctx);
        [DllImport(L)] internal static extern int mpv_render_context_create(IntPtr* res, IntPtr mpv, Param* p);
        [DllImport(L)] internal static extern void mpv_render_context_free(IntPtr ctx);
        [DllImport(L)] internal static extern void mpv_render_context_set_update_callback(IntPtr ctx, delegate* unmanaged<IntPtr, void> cb, IntPtr datos);
        [DllImport(L)] internal static extern ulong mpv_render_context_update(IntPtr ctx);
        [DllImport(L)] internal static extern int mpv_render_context_render(IntPtr ctx, Param* p);
        [DllImport(L)] internal static extern void mpv_render_context_report_swap(IntPtr ctx);
        [DllImport("libEGL.so.1")] internal static extern IntPtr eglGetProcAddress(sbyte* nombre);
        internal const int FORMATO_FLAG = 3, FORMATO_DOUBLE = 5;
        internal const int PARAM_API = 1, PARAM_INIT_GL = 2, PARAM_FBO = 3, PARAM_GIRA_Y = 4, PARAM_SIN_BLOQUEO = 12;   // (BLOCK_FOR_TARGET_TIME)
        // los avisos (mpv_event_id) que se miran
        internal const int AVISO_NADA = 0, AVISO_CIERRE = 1, AVISO_EMPIEZA = 6, AVISO_ACABA = 7, AVISO_CARGADO = 8, AVISO_SIGUE = 21;
        [StructLayout(LayoutKind.Sequential)] internal struct Param { public int Tipo; public void* Dato; }
        [StructLayout(LayoutKind.Sequential)] internal struct InicioGl { public delegate* unmanaged<IntPtr, sbyte*, IntPtr> Buscar; public IntPtr Ctx; }
        [StructLayout(LayoutKind.Sequential)] internal struct Fbo { public int Nombre, W, H, Formato; }
        [StructLayout(LayoutKind.Sequential)] internal struct Evento { public int Id, Error; public ulong Usuario; public void* Dato; }
        [UnmanagedCallersOnly] internal static IntPtr BuscaFuncion(IntPtr ctx, sbyte* nombre) { return eglGetProcAddress(nombre); }

        // ¿Esta libmpv en el sistema? (se pregunta una vez)
        private static int hay;   // 0 sin probar, 1 si, -1 no
        internal static bool Hay()
        {
            if (hay == 0)
            {
                IntPtr h;
                hay = NativeLibrary.TryLoad(L, typeof(Mpv).Assembly, null, out h) ? 1 : -1;
            }
            return hay > 0;
        }

        internal static int Orden(IntPtr ctx, params string[] partes)
        {
            IntPtr* a = stackalloc IntPtr[partes.Length + 1];
            for (int i = 0; i < partes.Length; i++) a[i] = Marshal.StringToCoTaskMemUTF8(partes[i]);
            a[partes.Length] = IntPtr.Zero;
            try { return mpv_command(ctx, a); }
            finally { for (int i = 0; i < partes.Length; i++) Marshal.FreeCoTaskMem(a[i]); }
        }
        internal static double Numero(IntPtr ctx, string nombre) { double d = 0; return mpv_get_property(ctx, nombre, FORMATO_DOUBLE, &d) >= 0 ? d : -1; }
        internal static bool Bandera(IntPtr ctx, string nombre) { int f = 0; return mpv_get_property(ctx, nombre, FORMATO_FLAG, &f) >= 0 && f != 0; }
    }
}
