// LO DE OPENGL ES 2 QUE NO ESTA EN Gl.cs (port nativo de XP). Gl.cs es del programa de Linux de ahora (sistema/rocola/Consola/Pantalla) y no
// se toca: lo que le falta a la tarjeta de XP (Nativo/SombreadoresTarjeta.cs, TexturasGl.cs, el dispositivo), al anfitrion y al video va
// aqui, por P/Invoke a la libGLESv2 de Mesa (en el A4, r600 por KMS; en las pruebas, llvmpipe).
using System;
using System.Runtime.InteropServices;

namespace Rocola
{
    internal static unsafe class GlMas
    {
        private const string L = "libGLESv2.so.2";
        internal const uint ZERO = 0, SRC_COLOR = 0x0300, ONE_MINUS_SRC_COLOR = 0x0301, DST_ALPHA = 0x0304, ONE_MINUS_DST_ALPHA = 0x0305,
            DST_COLOR = 0x0306, ONE_MINUS_DST_COLOR = 0x0307, SRC_ALPHA_SATURATE = 0x0308, CONSTANT_COLOR = 0x8001, ONE_MINUS_CONSTANT_COLOR = 0x8002,
            FUNC_ADD = 0x8006, MIN = 0x8007, MAX = 0x8008, FUNC_SUBTRACT = 0x800A, FUNC_REVERSE_SUBTRACT = 0x800B,
            NEAREST = 0x2600, REPEAT = 0x2901, MIRRORED_REPEAT = 0x8370, CLAMP_TO_BORDER = 0x812D, MIRROR_CLAMP_TO_EDGE = 0x8743,
            TEXTURE_BORDER_COLOR = 0x1004, CULL_FACE = 0x0B44, FRONT = 0x0404, BACK = 0x0405, CCW = 0x0901,
            DYNAMIC_DRAW = 0x88E8, MAX_TEXTURE_SIZE = 0x0D33, POINTS = 0, LINES = 1,
            LINE_STRIP = 3, TRIANGLE_STRIP = 5, TRIANGLE_FAN = 6, OUT_OF_MEMORY = 0x0505, INFO_LOG_LENGTH = 0x8B84, DEPTH_TEST = 0x0B71,
            DITHER = 0x0BD0, STENCIL_TEST = 0x0B90, POLYGON_OFFSET_FILL = 0x8037, SAMPLE_ALPHA_TO_COVERAGE = 0x809E, SAMPLE_COVERAGE = 0x80A0,
            EXTENSIONS = 0x1F03,
            // (lo que pregunta la traza de los dibujos, ROCOLA_TRAZA_DIBUJOS)
            BLEND_SRC_RGB = 0x80C9, BLEND_DST_RGB = 0x80C8, TEXTURE_BINDING_2D = 0x8069, CURRENT_PROGRAM = 0x8B8D, COLOR_WRITEMASK = 0x0C23;
        [DllImport(L, EntryPoint = "glDisableVertexAttribArray")] internal static extern void DisableVertexAttribArray(uint i);
        [DllImport(L, EntryPoint = "glVertexAttrib4f")] internal static extern void VertexAttrib4f(uint i, float x, float y, float z, float w);
        [DllImport(L, EntryPoint = "glBufferSubData")] internal static extern void BufferSubData(uint d, IntPtr desp, IntPtr tam, void* datos);
        [DllImport(L, EntryPoint = "glDeleteBuffers")] internal static extern void DeleteBuffers(int n, uint* b);
        [DllImport(L, EntryPoint = "glDeleteFramebuffers")] internal static extern void DeleteFramebuffers(int n, uint* f);
        [DllImport(L, EntryPoint = "glDeleteShader")] internal static extern void DeleteShader(uint s);
        [DllImport(L, EntryPoint = "glDeleteProgram")] internal static extern void DeleteProgram(uint p);
        [DllImport(L, EntryPoint = "glGetProgramInfoLog")] internal static extern void GetProgramInfoLog(uint p, int max, int* largo, byte* texto);
        [DllImport(L, EntryPoint = "glGetError")] internal static extern uint GetError();
        [DllImport(L, EntryPoint = "glColorMask")] internal static extern void ColorMask(byte r, byte g, byte b, byte a);
        [DllImport(L, EntryPoint = "glFlush")] internal static extern void Flush();
        [DllImport(L, EntryPoint = "glBlendFuncSeparate")] internal static extern void BlendFuncSeparate(uint s, uint d, uint sa, uint da);
        [DllImport(L, EntryPoint = "glBlendEquation")] internal static extern void BlendEquation(uint e);
        [DllImport(L, EntryPoint = "glBlendEquationSeparate")] internal static extern void BlendEquationSeparate(uint c, uint a);
        [DllImport(L, EntryPoint = "glBlendColor")] internal static extern void BlendColor(float r, float g, float b, float a);
        [DllImport(L, EntryPoint = "glCullFace")] internal static extern void CullFace(uint m);
        [DllImport(L, EntryPoint = "glFrontFace")] internal static extern void FrontFace(uint m);
        [DllImport(L, EntryPoint = "glUniform4f")] internal static extern void Uniform4f(int l, float a, float b, float c, float d);
        [DllImport(L, EntryPoint = "glTexParameterfv")] internal static extern void TexParameterfv(uint d, uint p, float* v);
        [DllImport(L, EntryPoint = "glIsEnabled")] internal static extern byte IsEnabled(uint c);
        [DllImport(L, EntryPoint = "glGetBooleanv")] internal static extern void GetBooleanv(uint p, byte* v);
    }
}
