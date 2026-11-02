// OPENGL ES 2 Y EGL DE ROCOLA OS (la pantalla, etapa 2). Solo las llamadas que usa la rocola, por P/Invoke a las de Mesa (libGLESv2 y
// libEGL); en el A4, Mesa r600 por KMS. El dibujo por tarjeta de XP (Direct3D 9) se reescribe con esto.
using System;
using System.Runtime.InteropServices;

namespace Rocola.Pantalla
{
    internal static unsafe class GL
    {
        private const string L = "libGLESv2.so.2";
        internal const uint COLOR_BUFFER_BIT = 0x4000, TRIANGLES = 4, TEXTURE_2D = 0x0DE1, RGBA = 0x1908, UNSIGNED_BYTE = 0x1401, FLOAT = 0x1406,
            TEXTURE_MIN_FILTER = 0x2801, TEXTURE_MAG_FILTER = 0x2800, LINEAR = 0x2601, TEXTURE_WRAP_S = 0x2802, TEXTURE_WRAP_T = 0x2803,
            CLAMP_TO_EDGE = 0x812F, BLEND = 0x0BE2, SRC_ALPHA = 0x0302, ONE_MINUS_SRC_ALPHA = 0x0303, ARRAY_BUFFER = 0x8892,
            STREAM_DRAW = 0x88E0, VERTEX_SHADER = 0x8B31, FRAGMENT_SHADER = 0x8B30, COMPILE_STATUS = 0x8B81, LINK_STATUS = 0x8B82,
            FRAMEBUFFER = 0x8D40, COLOR_ATTACHMENT0 = 0x8CE0, UNPACK_ALIGNMENT = 0x0CF5, PACK_ALIGNMENT = 0x0D05, TEXTURE0 = 0x84C0,
            SCISSOR_TEST = 0x0C11, RENDERER = 0x1F01, VERSION = 0x1F02, LINEAR_MIPMAP_LINEAR = 0x2703;
        [DllImport(L, EntryPoint = "glViewport")] internal static extern void Viewport(int x, int y, int w, int h);
        [DllImport(L, EntryPoint = "glScissor")] internal static extern void Scissor(int x, int y, int w, int h);
        [DllImport(L, EntryPoint = "glClearColor")] internal static extern void ClearColor(float r, float g, float b, float a);
        [DllImport(L, EntryPoint = "glClear")] internal static extern void Clear(uint mask);
        [DllImport(L, EntryPoint = "glEnable")] internal static extern void Enable(uint cap);
        [DllImport(L, EntryPoint = "glDisable")] internal static extern void Disable(uint cap);
        [DllImport(L, EntryPoint = "glBlendFunc")] internal static extern void BlendFunc(uint s, uint d);
        [DllImport(L, EntryPoint = "glCreateShader")] internal static extern uint CreateShader(uint tipo);
        [DllImport(L, EntryPoint = "glShaderSource")] internal static extern void ShaderSource(uint s, int n, byte** fuentes, int* largos);
        [DllImport(L, EntryPoint = "glCompileShader")] internal static extern void CompileShader(uint s);
        [DllImport(L, EntryPoint = "glGetShaderiv")] internal static extern void GetShaderiv(uint s, uint p, int* v);
        [DllImport(L, EntryPoint = "glGetShaderInfoLog")] internal static extern void GetShaderInfoLog(uint s, int max, int* largo, byte* texto);
        [DllImport(L, EntryPoint = "glCreateProgram")] internal static extern uint CreateProgram();
        [DllImport(L, EntryPoint = "glAttachShader")] internal static extern void AttachShader(uint p, uint s);
        [DllImport(L, EntryPoint = "glBindAttribLocation")] internal static extern void BindAttribLocation(uint p, uint i, string nombre);
        [DllImport(L, EntryPoint = "glLinkProgram")] internal static extern void LinkProgram(uint p);
        [DllImport(L, EntryPoint = "glGetProgramiv")] internal static extern void GetProgramiv(uint p, uint q, int* v);
        [DllImport(L, EntryPoint = "glUseProgram")] internal static extern void UseProgram(uint p);
        [DllImport(L, EntryPoint = "glGetUniformLocation")] internal static extern int GetUniformLocation(uint p, string nombre);
        [DllImport(L, EntryPoint = "glUniform2f")] internal static extern void Uniform2f(int l, float a, float b);
        [DllImport(L, EntryPoint = "glUniform1i")] internal static extern void Uniform1i(int l, int a);
        [DllImport(L, EntryPoint = "glGenTextures")] internal static extern void GenTextures(int n, uint* t);
        [DllImport(L, EntryPoint = "glDeleteTextures")] internal static extern void DeleteTextures(int n, uint* t);
        [DllImport(L, EntryPoint = "glBindTexture")] internal static extern void BindTexture(uint destino, uint t);
        [DllImport(L, EntryPoint = "glActiveTexture")] internal static extern void ActiveTexture(uint t);
        [DllImport(L, EntryPoint = "glTexParameteri")] internal static extern void TexParameteri(uint d, uint p, int v);
        [DllImport(L, EntryPoint = "glTexImage2D")] internal static extern void TexImage2D(uint d, int nivel, int interno, int w, int h, int borde, uint formato, uint tipo, void* datos);
        [DllImport(L, EntryPoint = "glGenerateMipmap")] internal static extern void GenerateMipmap(uint destino);
        [DllImport(L, EntryPoint = "glPixelStorei")] internal static extern void PixelStorei(uint p, int v);
        [DllImport(L, EntryPoint = "glGenBuffers")] internal static extern void GenBuffers(int n, uint* b);
        [DllImport(L, EntryPoint = "glBindBuffer")] internal static extern void BindBuffer(uint d, uint b);
        [DllImport(L, EntryPoint = "glBufferData")] internal static extern void BufferData(uint d, IntPtr tam, void* datos, uint uso);
        [DllImport(L, EntryPoint = "glVertexAttribPointer")] internal static extern void VertexAttribPointer(uint i, int n, uint tipo, byte normal, int paso, IntPtr desp);
        [DllImport(L, EntryPoint = "glEnableVertexAttribArray")] internal static extern void EnableVertexAttribArray(uint i);
        [DllImport(L, EntryPoint = "glDrawArrays")] internal static extern void DrawArrays(uint modo, int primero, int n);
        [DllImport(L, EntryPoint = "glReadPixels")] internal static extern void ReadPixels(int x, int y, int w, int h, uint formato, uint tipo, void* datos);
        [DllImport(L, EntryPoint = "glGenFramebuffers")] internal static extern void GenFramebuffers(int n, uint* f);
        [DllImport(L, EntryPoint = "glBindFramebuffer")] internal static extern void BindFramebuffer(uint d, uint f);
        [DllImport(L, EntryPoint = "glFramebufferTexture2D")] internal static extern void FramebufferTexture2D(uint d, uint adjunto, uint textura, uint t, int nivel);
        [DllImport(L, EntryPoint = "glGetString")] internal static extern IntPtr GetString(uint n);
        [DllImport(L, EntryPoint = "glFinish")] internal static extern void Finish();
        internal static string Texto(uint n) { IntPtr p = GetString(n); return p == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(p); }
    }

    internal static unsafe class EGL
    {
        private const string L = "libEGL.so.1";
        internal const int NONE = 0x3038, RED_SIZE = 0x3024, GREEN_SIZE = 0x3023, BLUE_SIZE = 0x3022, ALPHA_SIZE = 0x3021, SURFACE_TYPE = 0x3033,
            WINDOW_BIT = 0x0004, PBUFFER_BIT = 0x0001, RENDERABLE_TYPE = 0x3040, OPENGL_ES2_BIT = 0x0004, CONTEXT_CLIENT_VERSION = 0x3098,
            NATIVE_VISUAL_ID = 0x302E, OPENGL_ES_API = 0x30A0, PLATFORM_GBM_KHR = 0x31D7, PLATFORM_SURFACELESS_MESA = 0x31DD;
        [DllImport(L, EntryPoint = "eglGetPlatformDisplay")] internal static extern IntPtr GetPlatformDisplay(int plataforma, IntPtr nativo, IntPtr atributos);
        [DllImport(L, EntryPoint = "eglInitialize")] internal static extern int Initialize(IntPtr d, int* mayor, int* menor);
        [DllImport(L, EntryPoint = "eglBindAPI")] internal static extern int BindAPI(int api);
        [DllImport(L, EntryPoint = "eglChooseConfig")] internal static extern int ChooseConfig(IntPtr d, int* atributos, IntPtr* configs, int max, int* n);
        [DllImport(L, EntryPoint = "eglGetConfigAttrib")] internal static extern int GetConfigAttrib(IntPtr d, IntPtr c, int atributo, int* valor);
        [DllImport(L, EntryPoint = "eglCreateContext")] internal static extern IntPtr CreateContext(IntPtr d, IntPtr c, IntPtr compartido, int* atributos);
        [DllImport(L, EntryPoint = "eglCreateWindowSurface")] internal static extern IntPtr CreateWindowSurface(IntPtr d, IntPtr c, IntPtr ventana, int* atributos);
        [DllImport(L, EntryPoint = "eglMakeCurrent")] internal static extern int MakeCurrent(IntPtr d, IntPtr dibujo, IntPtr lectura, IntPtr ctx);
        [DllImport(L, EntryPoint = "eglSwapBuffers")] internal static extern int SwapBuffers(IntPtr d, IntPtr s);
        [DllImport(L, EntryPoint = "eglDestroySurface")] internal static extern int DestroySurface(IntPtr d, IntPtr s);
        [DllImport(L, EntryPoint = "eglDestroyContext")] internal static extern int DestroyContext(IntPtr d, IntPtr c);
        [DllImport(L, EntryPoint = "eglTerminate")] internal static extern int Terminate(IntPtr d);
        [DllImport(L, EntryPoint = "eglGetError")] internal static extern int GetError();

        // Un contexto de OpenGL ES 2 en la pantalla dada (nativo: el aparato de GBM o nada). 'visual': el formato de la superficie (0: cualquiera).
        internal static void Contexto(int plataforma, IntPtr nativo, int visual, bool ventana, out IntPtr pantalla, out IntPtr config, out IntPtr ctx)
        {
            pantalla = GetPlatformDisplay(plataforma, nativo, IntPtr.Zero);
            if (pantalla == IntPtr.Zero) throw new InvalidOperationException("EGL: sin pantalla (" + GetError().ToString("x") + ")");
            int ma, me;
            if (Initialize(pantalla, &ma, &me) == 0) throw new InvalidOperationException("EGL: no inicia (" + GetError().ToString("x") + ")");
            BindAPI(OPENGL_ES_API);
            int* at = stackalloc int[] { RED_SIZE, 8, GREEN_SIZE, 8, BLUE_SIZE, 8, SURFACE_TYPE, ventana ? WINDOW_BIT : PBUFFER_BIT, RENDERABLE_TYPE, OPENGL_ES2_BIT, NONE };
            if (!ventana) at[6] = NONE;   // sin superficie (surfaceless): cualquier configuracion de ES2
            IntPtr* cs = stackalloc IntPtr[64]; int n;
            if (ChooseConfig(pantalla, at, cs, 64, &n) == 0 || n == 0)
            {
                if (ventana) throw new InvalidOperationException("EGL: sin configuracion de OpenGL ES 2");
                n = 0; cs[0] = IntPtr.Zero;   // sin superficie: un contexto sin configuracion (EGL_KHR_no_config_context, lo tiene Mesa)
            }
            config = cs[0];
            if (visual != 0)
                for (int i = 0; i < n; i++) { int v; if (GetConfigAttrib(pantalla, cs[i], NATIVE_VISUAL_ID, &v) != 0 && v == visual) { config = cs[i]; break; } }
            int* ca = stackalloc int[] { CONTEXT_CLIENT_VERSION, 2, NONE };
            ctx = CreateContext(pantalla, config, IntPtr.Zero, ca);
            if (ctx == IntPtr.Zero) throw new InvalidOperationException("EGL: sin contexto (" + GetError().ToString("x") + ")");
        }
    }
}
