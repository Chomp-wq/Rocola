// Lo que el Direct3D de mentira (D3d9.cs) y la pantalla (Pantalla.cs) le piden a OpenGL ES 2 ademas de lo que ya tenia la etapa 2
// (sistema/rocola/Consola/Pantalla/Gl.cs).
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Rocola.Linux
{
    internal static unsafe class Gles
    {
        private const string L = "libGLESv2.so.2";
        internal const uint ZERO = 0, ONE = 1, SRC_COLOR = 0x0300, ONE_MINUS_SRC_COLOR = 0x0301, SRC_ALPHA = 0x0302, ONE_MINUS_SRC_ALPHA = 0x0303,
            DST_ALPHA = 0x0304, ONE_MINUS_DST_ALPHA = 0x0305, DST_COLOR = 0x0306, ONE_MINUS_DST_COLOR = 0x0307,
            NEAREST = 0x2600, LINEAR = 0x2601, REPEAT = 0x2901, MIRRORED_REPEAT = 0x8370, CLAMP_TO_EDGE = 0x812F,
            TEXTURE_2D = 0x0DE1, RGBA = 0x1908, UNSIGNED_BYTE = 0x1401, FLOAT = 0x1406, TRIANGLES = 4, TRIANGLE_STRIP = 5, TRIANGLE_FAN = 6,
            BLEND = 0x0BE2, CULL_FACE = 0x0B44, DEPTH_TEST = 0x0B71, SCISSOR_TEST = 0x0C11, DITHER = 0x0BD0,
            FRAMEBUFFER = 0x8D40, COLOR_ATTACHMENT0 = 0x8CE0, FRAMEBUFFER_COMPLETE = 0x8CD5, ARRAY_BUFFER = 0x8892, STREAM_DRAW = 0x88E0,
            TEXTURE0 = 0x84C0, TEXTURE_MIN_FILTER = 0x2801, TEXTURE_MAG_FILTER = 0x2800, TEXTURE_WRAP_S = 0x2802, TEXTURE_WRAP_T = 0x2803,
            UNPACK_ALIGNMENT = 0x0CF5, PACK_ALIGNMENT = 0x0D05, COLOR_BUFFER_BIT = 0x4000, VERTEX_SHADER = 0x8B31, FRAGMENT_SHADER = 0x8B30,
            COMPILE_STATUS = 0x8B81, LINK_STATUS = 0x8B82, MAX_TEXTURE_SIZE = 0x0D33, RENDERER = 0x1F01, VERSION = 0x1F02, EXTENSIONS = 0x1F03,
            FRAMEBUFFER_BINDING = 0x8CA6;
        [DllImport(L, EntryPoint = "glViewport")] internal static extern void Viewport(int x, int y, int w, int h);
        [DllImport(L, EntryPoint = "glScissor")] internal static extern void Scissor(int x, int y, int w, int h);
        [DllImport(L, EntryPoint = "glClearColor")] internal static extern void ClearColor(float r, float g, float b, float a);
        [DllImport(L, EntryPoint = "glClear")] internal static extern void Clear(uint mask);
        [DllImport(L, EntryPoint = "glEnable")] internal static extern void Enable(uint cap);
        [DllImport(L, EntryPoint = "glDisable")] internal static extern void Disable(uint cap);
        [DllImport(L, EntryPoint = "glBlendFunc")] internal static extern void BlendFunc(uint s, uint d);
        [DllImport(L, EntryPoint = "glBlendFuncSeparate")] internal static extern void BlendFuncSeparate(uint s, uint d, uint sa, uint da);
        [DllImport(L, EntryPoint = "glCreateShader")] internal static extern uint CreateShader(uint tipo);
        [DllImport(L, EntryPoint = "glShaderSource")] internal static extern void ShaderSource(uint s, int n, byte** fuentes, int* largos);
        [DllImport(L, EntryPoint = "glCompileShader")] internal static extern void CompileShader(uint s);
        [DllImport(L, EntryPoint = "glGetShaderiv")] internal static extern void GetShaderiv(uint s, uint p, int* v);
        [DllImport(L, EntryPoint = "glGetShaderInfoLog")] internal static extern void GetShaderInfoLog(uint s, int max, int* largo, byte* texto);
        [DllImport(L, EntryPoint = "glGetProgramInfoLog")] internal static extern void GetProgramInfoLog(uint s, int max, int* largo, byte* texto);
        [DllImport(L, EntryPoint = "glCreateProgram")] internal static extern uint CreateProgram();
        [DllImport(L, EntryPoint = "glAttachShader")] internal static extern void AttachShader(uint p, uint s);
        [DllImport(L, EntryPoint = "glBindAttribLocation")] internal static extern void BindAttribLocation(uint p, uint i, byte* nombre);
        [DllImport(L, EntryPoint = "glLinkProgram")] internal static extern void LinkProgram(uint p);
        [DllImport(L, EntryPoint = "glGetProgramiv")] internal static extern void GetProgramiv(uint p, uint q, int* v);
        [DllImport(L, EntryPoint = "glUseProgram")] internal static extern void UseProgram(uint p);
        [DllImport(L, EntryPoint = "glGetUniformLocation")] internal static extern int GetUniformLocation(uint p, byte* nombre);
        [DllImport(L, EntryPoint = "glUniform1i")] internal static extern void Uniform1i(int l, int a);
        [DllImport(L, EntryPoint = "glUniform1f")] internal static extern void Uniform1f(int l, float a);
        [DllImport(L, EntryPoint = "glUniform2f")] internal static extern void Uniform2f(int l, float a, float b);
        [DllImport(L, EntryPoint = "glUniform4f")] internal static extern void Uniform4f(int l, float a, float b, float c, float d);
        [DllImport(L, EntryPoint = "glGenTextures")] internal static extern void GenTextures(int n, uint* t);
        [DllImport(L, EntryPoint = "glDeleteTextures")] internal static extern void DeleteTextures(int n, uint* t);
        [DllImport(L, EntryPoint = "glBindTexture")] internal static extern void BindTexture(uint destino, uint t);
        [DllImport(L, EntryPoint = "glActiveTexture")] internal static extern void ActiveTexture(uint t);
        [DllImport(L, EntryPoint = "glTexParameteri")] internal static extern void TexParameteri(uint d, uint p, int v);
        [DllImport(L, EntryPoint = "glTexImage2D")] internal static extern void TexImage2D(uint d, int nivel, int interno, int w, int h, int borde, uint formato, uint tipo, void* datos);
        [DllImport(L, EntryPoint = "glTexSubImage2D")] internal static extern void TexSubImage2D(uint d, int nivel, int x, int y, int w, int h, uint formato, uint tipo, void* datos);
        [DllImport(L, EntryPoint = "glPixelStorei")] internal static extern void PixelStorei(uint p, int v);
        [DllImport(L, EntryPoint = "glGenBuffers")] internal static extern void GenBuffers(int n, uint* b);
        [DllImport(L, EntryPoint = "glBindBuffer")] internal static extern void BindBuffer(uint d, uint b);
        [DllImport(L, EntryPoint = "glBufferData")] internal static extern void BufferData(uint d, IntPtr tam, void* datos, uint uso);
        [DllImport(L, EntryPoint = "glVertexAttribPointer")] internal static extern void VertexAttribPointer(uint i, int n, uint tipo, byte normal, int paso, IntPtr desp);
        [DllImport(L, EntryPoint = "glEnableVertexAttribArray")] internal static extern void EnableVertexAttribArray(uint i);
        [DllImport(L, EntryPoint = "glDisableVertexAttribArray")] internal static extern void DisableVertexAttribArray(uint i);
        [DllImport(L, EntryPoint = "glVertexAttrib2f")] internal static extern void VertexAttrib2f(uint i, float x, float y);
        [DllImport(L, EntryPoint = "glDrawArrays")] internal static extern void DrawArrays(uint modo, int primero, int n);
        [DllImport(L, EntryPoint = "glReadPixels")] internal static extern void ReadPixels(int x, int y, int w, int h, uint formato, uint tipo, void* datos);
        [DllImport(L, EntryPoint = "glGenFramebuffers")] internal static extern void GenFramebuffers(int n, uint* f);
        [DllImport(L, EntryPoint = "glDeleteFramebuffers")] internal static extern void DeleteFramebuffers(int n, uint* f);
        [DllImport(L, EntryPoint = "glBindFramebuffer")] internal static extern void BindFramebuffer(uint d, uint f);
        [DllImport(L, EntryPoint = "glFramebufferTexture2D")] internal static extern void FramebufferTexture2D(uint d, uint adjunto, uint textura, uint t, int nivel);
        [DllImport(L, EntryPoint = "glCheckFramebufferStatus")] internal static extern uint CheckFramebufferStatus(uint d);
        [DllImport(L, EntryPoint = "glGetIntegerv")] internal static extern void GetIntegerv(uint p, int* v);
        [DllImport(L, EntryPoint = "glGetString")] internal static extern IntPtr GetString(uint n);
        [DllImport(L, EntryPoint = "glGetError")] internal static extern uint GetError();
        [DllImport(L, EntryPoint = "glFinish")] internal static extern void Finish();
        [DllImport(L, EntryPoint = "glFlush")] internal static extern void Flush();
        internal static string Texto(uint n) { IntPtr p = GetString(n); return p == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(p); }
        internal static int Uniforme(uint p, string nombre) { byte[] b = Encoding.ASCII.GetBytes(nombre + "\0"); fixed (byte* q = b) return GetUniformLocation(p, q); }
        internal static void Atributo(uint p, uint i, string nombre) { byte[] b = Encoding.ASCII.GetBytes(nombre + "\0"); fixed (byte* q = b) BindAttribLocation(p, i, q); }

        // Un programa de dos sombreadores; null si no compila (y lo dice).
        internal static uint Programa(string vs, string fs, params string[] atributos)
        {
            uint v = Sombreador(VERTEX_SHADER, vs), f = Sombreador(FRAGMENT_SHADER, fs);
            uint p = CreateProgram();
            AttachShader(p, v); AttachShader(p, f);
            for (int i = 0; i < atributos.Length; i++) Atributo(p, (uint)i, atributos[i]);
            LinkProgram(p);
            int ok; GetProgramiv(p, LINK_STATUS, &ok);
            if (ok == 0)
            {
                byte* t = stackalloc byte[2048]; int n; GetProgramInfoLog(p, 2048, &n, t);
                throw new InvalidOperationException("GLES: el programa no enlaza: " + Marshal.PtrToStringAnsi((IntPtr)t, n) + "\n" + fs);
            }
            return p;
        }
        private static uint Sombreador(uint tipo, string fuente)
        {
            uint s = CreateShader(tipo);
            byte[] b = Encoding.ASCII.GetBytes(fuente);
            fixed (byte* q = b) { byte* pq = q; int n = b.Length; ShaderSource(s, 1, &pq, &n); }
            CompileShader(s);
            int ok; GetShaderiv(s, COMPILE_STATUS, &ok);
            if (ok == 0)
            {
                byte* t = stackalloc byte[2048]; int n; GetShaderInfoLog(s, 2048, &n, t);
                throw new InvalidOperationException("GLES: el sombreador no compila: " + Marshal.PtrToStringAnsi((IntPtr)t, n) + "\n" + fuente);
            }
            return s;
        }
    }
}
