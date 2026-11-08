// DIRECT3D 9 DE LA ROCOLA DE XP, EN OPENGL ES 2 (traduccion de XP, fase 1; decision del dueno del 7-11-2026).
//
// En XP, D3D9Star.cs y los demas archivos de la tarjeta llaman a Direct3D 9 por su tabla de funciones (D3D9.Fn<T>(objeto, hueco)). En la
// traduccion, cada una de esas llamadas se cambia por la funcion de aqui con el MISMO nombre que tiene en Direct3D y los mismos
// parametros (D3D9.EnGl.Present en vez de D3D9.Fn<D3D9.PresentDel>(dev, 17)), que hace lo mismo con OpenGL ES 2 (Mesa; en el A4, r600
// por KMS). Todo lo demas de XP queda igual.
//
// COMO SE HACE CADA COSA
//  - Los objetos (Direct3D, el dispositivo, texturas, superficies, vertices) son objetos de aqui con su cuenta de referencias, como en
//    COM. El IntPtr que ve el codigo de XP es su numero (no se repite nunca).
//  - Los colores van en el orden de Direct3D (A8R8G8B8: en memoria B, G, R, A) de punta a punta: las texturas se suben tal cual, el
//    color del vertice se lee tal cual y lo pintado queda igual en la textura del fondo. Las cuentas de las etapas y de la mezcla son las
//    mismas en cada canal de color, asi que no hace falta cambiarlos de orden; solo al ponerlo en la pantalla (el anfitrion) se leen en
//    el orden de OpenGL. Lo que va por canal (COLORWRITEENABLE, TEXTUREFACTOR, BLENDFACTOR, BORDERCOLOR) se pasa con el rojo y el azul
//    cambiados de sitio, que es el orden en que quedan en la memoria.
//  - Las coordenadas de Direct3D 9 son pixeles con el centro en el entero; en OpenGL el centro esta en el medio: x de OpenGL = x + 0.5.
//    Todo se pinta en texturas (el fondo del dispositivo y las imagenes intermedias) sin dar la vuelta: la fila 0 de Direct3D es la
//    fila 0 de la textura, como al subir una imagen. El anfitrion la pone derecha en la pantalla. Con un framebuffer de textura, Mesa
//    (llvmpipe y r600) pinta con la regla de arriba a la izquierda contando desde la fila 0 de la memoria: la misma de Direct3D 9 (los
//    pixeles cuyo centro cae justo en el borde de arriba o de la izquierda de un triangulo son suyos; los del de abajo o la derecha, no).
//  - Lo que Direct3D 9 hace sin programa (las etapas de textura, SetTextureStageState) se convierte en un programa de OpenGL por cada
//    combinacion que se usa (se hace la primera vez y se guarda). La mezcla (SetRenderState) es la de OpenGL.
//  - Bloquear una textura da una memoria aparte del tamano de lo bloqueado; al soltarla se sube a la tarjeta (glTexSubImage2D).
//  - Present pasa la textura del fondo al escritorio (Escritorio.PresentaNativa), que compone las ventanas con ella y la pone en pantalla.
//  - Los vertices ya transformados (XYZRHW) se interpolan como en Direct3D (con su RHW; XP siempre pone 1).
//
// LO DE DIRECT3D 9 QUE SE HACE IGUAL (la referencia del SDK; revisado el 8-11-2026 y probado pixel a pixel contra esa referencia con
// llvmpipe; no en el A4):
//  - SetRenderState guarda los 210 estados con sus valores de fabrica (D3DRENDERSTATETYPE), y Reset los devuelve a ellos. Se hacen: la
//    mezcla entera (ALPHABLENDENABLE, SRCBLEND, DESTBLEND con los 15 D3DBLEND, BOTHSRCALPHA y BOTHINVSRCALPHA, BLENDFACTOR, BLENDOP con
//    ADD, SUBTRACT, REVSUBTRACT, MIN y MAX, y la del alfa aparte: SEPARATEALPHABLENDENABLE, SRCBLENDALPHA, DESTBLENDALPHA, BLENDOPALPHA),
//    CULLMODE (CW y CCW como se ven en la pantalla), ALPHATESTENABLE / ALPHAREF / ALPHAFUNC, COLORWRITEENABLE, DITHERENABLE (apagado de
//    fabrica: OpenGL lo trae encendido), TEXTUREFACTOR y SPECULARENABLE. Lo de la profundidad, la niebla, el stencil, la luz y el recorte
//    se guarda y no hace nada: el fondo no tiene profundidad, XP no los usa y con XYZRHW no hay luz.
//  - Las etapas (D3DTSS_*): las operaciones de 1 a 26 menos PREMODULATE y las de relieve (BUMPENVMAP), cada resultado entre 0 y 1, los
//    argumentos DIFFUSE, CURRENT, TEXTURE, TFACTOR, SPECULAR y TEMP con COMPLEMENT y ALPHAREPLICATE, COLORARG0 / ALPHAARG0 y RESULTARG;
//    DOTPRODUCT3 de color va tambien al alfa. Una etapa sin textura que pide D3DTA_TEXTURE pasa lo que le llega (como Direct3D 9: con los
//    estados de fabrica y SetTexture(0, NULL) sale el color del vertice, no negro). Dos etapas (las que usa XP: 0 y 1).
//  - El muestreo: POINT y LINEAR (ANISOTROPIC como LINEAR), WRAP, MIRROR, CLAMP, y BORDER y MIRRORONCE si OpenGL los tiene. Las texturas
//    son de un nivel (XP siempre pide uno), asi que MIPFILTER, MIPMAPLODBIAS, MAXMIPLEVEL y MAXANISOTROPY no cambian nada.
//  - X8R8G8B8: el alfa se lee 1 siempre (al crearla, al soltarla bloqueada y al pintar en ella, que no lo toca).
//  - El FVF se lee entero (XYZRHW, NORMAL, PSIZE, DIFFUSE, SPECULAR y hasta 8 juegos de coordenadas con su medida); cada etapa toma el
//    suyo por TEXCOORDINDEX. Sin color, el vertice es blanco; sin especular, negro; un juego que no esta, (0, 0).
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Rocola.Pantalla;

namespace Rocola
{
    // Lo de OpenGL ES 2 que hace falta aqui y no esta en Gl.cs (que es de la rocola de Linux de ahora y no se toca).
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
            EXTENSIONS = 0x1F03;
        [DllImport(L, EntryPoint = "glDisableVertexAttribArray")] internal static extern void DisableVertexAttribArray(uint i);
        [DllImport(L, EntryPoint = "glVertexAttrib4f")] internal static extern void VertexAttrib4f(uint i, float x, float y, float z, float w);
        [DllImport(L, EntryPoint = "glBufferSubData")] internal static extern void BufferSubData(uint d, IntPtr desp, IntPtr tam, void* datos);
        [DllImport(L, EntryPoint = "glDeleteBuffers")] internal static extern void DeleteBuffers(int n, uint* b);
        [DllImport(L, EntryPoint = "glDeleteFramebuffers")] internal static extern void DeleteFramebuffers(int n, uint* f);
        [DllImport(L, EntryPoint = "glDeleteShader")] internal static extern void DeleteShader(uint s);
        [DllImport(L, EntryPoint = "glGetProgramInfoLog")] internal static extern void GetProgramInfoLog(uint p, int max, int* largo, byte* texto);
        [DllImport(L, EntryPoint = "glGetError")] internal static extern uint GetError();
        [DllImport(L, EntryPoint = "glColorMask")] internal static extern void ColorMask(byte r, byte g, byte b, byte a);
        [DllImport(L, EntryPoint = "glFlush")] internal static extern void Flush();
        [DllImport(L, EntryPoint = "glBlendFuncSeparate")] internal static extern void BlendFuncSeparate(uint s, uint d, uint sa, uint da);
        [DllImport(L, EntryPoint = "glBlendEquationSeparate")] internal static extern void BlendEquationSeparate(uint c, uint a);
        [DllImport(L, EntryPoint = "glBlendColor")] internal static extern void BlendColor(float r, float g, float b, float a);
        [DllImport(L, EntryPoint = "glCullFace")] internal static extern void CullFace(uint m);
        [DllImport(L, EntryPoint = "glFrontFace")] internal static extern void FrontFace(uint m);
        [DllImport(L, EntryPoint = "glUniform4f")] internal static extern void Uniform4f(int l, float a, float b, float c, float d);
        [DllImport(L, EntryPoint = "glTexParameterfv")] internal static extern void TexParameterfv(uint d, uint p, float* v);
    }

    internal static unsafe partial class D3D9
    {
        // Lo que dice Direct3D 9 cuando algo no se puede (los mismos numeros)
        internal const int D3D_OK = 0, D3DERR_INVALIDCALL = unchecked((int)0x8876086C), D3DERR_OUTOFVIDEOMEMORY = unchecked((int)0x8876017C),
            E_OUTOFMEMORY = unchecked((int)0x8007000E), D3DERR_NOTAVAILABLE = unchecked((int)0x8876086A),
            // ValidateDevice (MAKE_D3DHRESULT(2073...2082))
            D3DERR_UNSUPPORTEDCOLOROPERATION = unchecked((int)0x88760819), D3DERR_UNSUPPORTEDCOLORARG = unchecked((int)0x8876081A),
            D3DERR_UNSUPPORTEDALPHAOPERATION = unchecked((int)0x8876081B), D3DERR_UNSUPPORTEDALPHAARG = unchecked((int)0x8876081C),
            D3DERR_TOOMANYOPERATIONS = unchecked((int)0x8876081D), D3DERR_UNSUPPORTEDFACTORVALUE = unchecked((int)0x8876081F),
            D3DERR_UNSUPPORTEDTEXTUREFILTER = unchecked((int)0x88760822);

        // ---------------- los objetos ----------------
        internal abstract class ObjetoGl { internal int Refs = 1; internal IntPtr Id; internal abstract void Libera(); }
        internal sealed class Direct3DGl : ObjetoGl { internal override void Libera() { } }

        internal sealed class TexturaGl : ObjetoGl
        {
            internal uint Nombre;              // la textura de OpenGL
            internal int W, H;
            internal uint Formato, Uso, Pool;
            internal bool SinAlfa;             // X8R8G8B8: el alfa vale 1 (al pintar en ella no se toca)
            internal bool Npot;                // no es potencia de 2 (REPEAT solo si OpenGL lo deja)
            internal SuperficieGl Nivel0;      // GetSurfaceLevel(0)
            internal IntPtr Bloqueo; internal int BX, BY, BW, BH; internal bool Bloqueada, BloqueoPropio, BloqueoSoloLeer;
            internal uint PuestoS, PuestoT, PuestoMin, PuestoMag;   // lo que tiene puesto en OpenGL (para no repetirlo)
            internal long PuestoBorde = -1;
            internal long Bytes { get { return (long)W * H * 4; } }
            internal override void Libera()
            {
                if (Bloqueada) { Bloqueada = false; SueltaMemoriaBloqueo(this); }
                if (Nivel0 != null) { Nivel0.Textura = null; Nivel0 = null; }
                uint n = Nombre; Nombre = 0;
                if (n != 0) EnGl.BorraTextura(n, Bytes);
            }
        }

        internal sealed class SuperficieGl : ObjetoGl
        {
            internal TexturaGl Textura;        // la textura donde se pinta (el fondo del dispositivo tambien es una)
            internal uint Fbo;
            internal bool DelFondo;            // el fondo del dispositivo (no se suelta con la textura)
            internal int W { get { return Textura != null ? Textura.W : 0; } }
            internal int H { get { return Textura != null ? Textura.H : 0; } }
            internal override void Libera()
            {
                uint f = Fbo; Fbo = 0;
                if (f != 0) EnGl.BorraFbo(f);
                TexturaGl t = Textura; Textura = null;
                if (t != null)
                {
                    if (t.Nivel0 == this) t.Nivel0 = null;
                    if (DelFondo) t.Libera();      // la textura del fondo es solo suya
                    else ReleaseObjeto(t);          // la superficie de una textura la tiene viva (como en Direct3D)
                }
            }
        }

        internal sealed class VerticesGl : ObjetoGl
        {
            internal uint Vbo; internal int Bytes;
            internal IntPtr Copia;             // lo bloqueado se escribe aqui y se sube al soltar
            internal int BDesde, BBytes; internal bool Bloqueado;
            internal override void Libera()
            {
                if (Copia != IntPtr.Zero) { NativeMemory.Free((void*)Copia); Copia = IntPtr.Zero; }
                uint b = Vbo; Vbo = 0;
                if (b != 0) EnGl.BorraVbo(b);
            }
        }

        // Una etapa de textura (D3DTSS_*) y su muestreo (D3DSAMP_*), con los valores de Direct3D 9 al empezar: la etapa 0 MODULATE
        // (TEXTURE, CURRENT) y SELECTARG1 (TEXTURE), las demas DISABLE; los argumentos 0 y el resultado, CURRENT; TEXCOORDINDEX = la etapa;
        // el muestreo WRAP, POINT, sin mipmaps (NONE), anisotropia 1, borde negro.
        internal struct Etapa
        {
            internal uint ColorOp, ColorArg0, ColorArg1, ColorArg2, AlphaOp, AlphaArg0, AlphaArg1, AlphaArg2, Resultado, IndiceCoordenadas;
            internal uint DireccionU, DireccionV, DireccionW, Borde, FiltroMag, FiltroMin, FiltroMip, SesgoMip, NivelMip, Anisotropia;
            internal IntPtr Textura;
            internal static Etapa DeFabrica(int i)
            {
                Etapa e = new Etapa();
                e.ColorOp = i == 0 ? TOP_MODULATE : TOP_DISABLE; e.ColorArg0 = TA_CURRENT; e.ColorArg1 = TA_TEXTURE; e.ColorArg2 = TA_CURRENT;
                e.AlphaOp = i == 0 ? TOP_SELECTARG1 : TOP_DISABLE; e.AlphaArg0 = TA_CURRENT; e.AlphaArg1 = TA_TEXTURE; e.AlphaArg2 = TA_CURRENT;
                e.Resultado = TA_CURRENT; e.IndiceCoordenadas = (uint)i;
                e.DireccionU = e.DireccionV = e.DireccionW = ADDRESS_WRAP; e.FiltroMag = 1; e.FiltroMin = 1;   // D3DTEXF_POINT
                e.Anisotropia = 1;
                return e;
            }
        }
        internal const int EtapasGl = 2;   // las que usa XP (la 0 y la 1, en la pasada de dos texturas)

        // Los estados de render de Direct3D 9 (D3DRENDERSTATETYPE: el numero es su sitio) y sus valores de fabrica (la referencia del SDK;
        // los decimales, como Direct3D, en los bits de un float).
        internal const int EstadosRs = 210;
        internal static void RsDeFabrica(uint[] rs, bool conProfundidad)
        {
            Array.Clear(rs, 0, rs.Length);
            const uint Uno = 0x3F800000;   // 1.0f
            rs[7] = conProfundidad ? 1u : 0u;              // ZENABLE: D3DZB_TRUE solo con profundidad automatica
            rs[8] = 3; rs[9] = 2;                          // FILLMODE SOLID, SHADEMODE GOURAUD
            rs[14] = 1; rs[16] = 1;                        // ZWRITEENABLE, LASTPIXEL
            rs[19] = 2; rs[20] = 1;                        // SRCBLEND ONE, DESTBLEND ZERO
            rs[22] = 3; rs[23] = 4; rs[25] = 8;            // CULLMODE CCW, ZFUNC LESSEQUAL, ALPHAFUNC ALWAYS
            rs[37] = Uno; rs[38] = Uno;                    // FOGEND, FOGDENSITY
            rs[53] = rs[54] = rs[55] = 1; rs[56] = 8;      // STENCILFAIL/ZFAIL/PASS KEEP, STENCILFUNC ALWAYS
            rs[58] = rs[59] = 0xFFFFFFFF;                  // STENCILMASK, STENCILWRITEMASK
            rs[60] = 0xFFFFFFFF;                           // TEXTUREFACTOR
            rs[136] = 1; rs[137] = 1; rs[141] = 1; rs[142] = 1;   // CLIPPING, LIGHTING, COLORVERTEX, LOCALVIEWER
            rs[145] = 1; rs[146] = 2;                      // DIFFUSEMATERIALSOURCE COLOR1, SPECULARMATERIALSOURCE COLOR2
            rs[154] = Uno; rs[155] = Uno; rs[158] = Uno;   // POINTSIZE, POINTSIZE_MIN, POINTSCALE_A
            rs[161] = 1; rs[162] = 0xFFFFFFFF;             // MULTISAMPLEANTIALIAS, MULTISAMPLEMASK
            rs[166] = 0x42800000;                          // POINTSIZE_MAX 64.0
            rs[168] = 0xF; rs[171] = 1;                    // COLORWRITEENABLE, BLENDOP ADD
            rs[172] = 3; rs[173] = 1;                      // POSITIONDEGREE CUBIC, NORMALDEGREE LINEAR
            rs[178] = Uno; rs[179] = Uno; rs[182] = Uno;   // MIN/MAXTESSELLATIONLEVEL, ADAPTIVETESS_Z
            rs[186] = rs[187] = rs[188] = 1; rs[189] = 8;  // CCW_STENCIL*
            rs[190] = rs[191] = rs[192] = 0xF;             // COLORWRITEENABLE1..3
            rs[193] = 0xFFFFFFFF;                          // BLENDFACTOR
            rs[207] = 2; rs[208] = 1; rs[209] = 1;         // SRCBLENDALPHA ONE, DESTBLENDALPHA ZERO, BLENDOPALPHA ADD
        }

        internal sealed class DispositivoGl : ObjetoGl
        {
            internal IntPtr Ventana;            // el control de la rocola donde se presenta (en XP, la ventana)
            internal PresentParams Pp;
            internal SuperficieGl Fondo;        // el fondo (back buffer): una textura de OpenGL con su FBO
            internal SuperficieGl Destino;      // donde se pinta ahora (SetRenderTarget)
            internal bool EnEscena;
            internal int Hilo;                  // el hilo de OpenGL (el de la pantalla)
            // el estado de Direct3D (lo que pidio el programa)
            internal readonly uint[] Rs = new uint[EstadosRs];
            internal uint Fvf;
            internal Etapa[] Etapas = new Etapa[8];
            internal IntPtr Flujo; internal uint FlujoDesp, FlujoPaso;   // SetStreamSource
            // lo que esta puesto en OpenGL (EstadoTocado lo olvida todo)
            internal bool EstadoSucio = true, ProgramaSucio = true;
            internal ProgramaGl Programa;       // el de la combinacion de ahora (ProgramaSucio: hay que buscarlo otra vez)
            internal ProgramaGl ProgramaPuesto; // el que tiene OpenGL
            internal uint FboPuesto = uint.MaxValue; internal int VistaW = -1, VistaH = -1;
            internal int MezclaPuesta = -1, RecortePuesto = -1, TramadoPuesto = -1, MascaraPuesta = -1;
            internal uint FuentePuesta, DestinoPuesto, FuenteAlfaPuesta, DestinoAlfaPuesto, EcuacionPuesta, EcuacionAlfaPuesta;
            internal long FactorPuesto = -1;
            internal bool EspecularPuesto;      // el atributo del color especular encendido (apunta a la memoria de un dibujo)
            internal uint[] TexturaPuesta = new uint[EtapasGl];
            internal uint TexturaNegra;         // la de una etapa sin textura: (0, 0, 0, 1)
            internal int Presentaciones;
            internal DispositivoGl() { RsDeFabrica(Rs, false); for (int i = 0; i < Etapas.Length; i++) Etapas[i] = Etapa.DeFabrica(i); }
            internal override void Libera()
            {
                SuperficieGl f = Fondo; Fondo = null;
                SuperficieGl d = Destino; Destino = null;
                if (d != null) ReleaseObjeto(d);   // la referencia del destino (aunque sea el fondo)
                if (f != null) ReleaseObjeto(f);
                for (int i = 0; i < Etapas.Length; i++) { IntPtr t = Etapas[i].Textura; Etapas[i].Textura = IntPtr.Zero; if (t != IntPtr.Zero) ReleaseId(t); }
                if (Flujo != IntPtr.Zero) { ReleaseId(Flujo); Flujo = IntPtr.Zero; }
                if (TexturaNegra != 0) { EnGl.BorraTextura(TexturaNegra, 4); TexturaNegra = 0; }
                Control c = Control.FromHandle(Ventana);
                if (c != null) c.TexturaGl = 0;
            }
        }

        internal sealed class ProgramaGl
        {
            internal uint Nombre; internal int LugarM, LugarTf, LugarAr; internal int W = -1, H = -1;
            internal long TfPuesto = -1, ArPuesto = -1;
            internal bool UsaEspecular;
            internal bool[] Muestrea = new bool[EtapasGl];
        }

        // ---------------- la tabla de objetos ----------------
        private static readonly object candadoGl = new object();
        private static readonly Dictionary<IntPtr, ObjetoGl> objetosGl = new Dictionary<IntPtr, ObjetoGl>();
        private static long siguienteIdGl = 0x10000;
        internal static int ObjetosGlVivos { get { lock (candadoGl) return objetosGl.Count; } }
        private static IntPtr RegistraGl(ObjetoGl o)
        {
            lock (candadoGl) { siguienteIdGl += 16; o.Id = new IntPtr(siguienteIdGl); objetosGl[o.Id] = o; }
            return o.Id;
        }
        internal static T ObjetoDe<T>(IntPtr id) where T : ObjetoGl
        {
            if (id == IntPtr.Zero) return null;
            lock (candadoGl) { ObjetoGl o; return objetosGl.TryGetValue(id, out o) ? o as T : null; }
        }
        private static uint ReleaseId(IntPtr id)
        {
            ObjetoGl o;
            lock (candadoGl) { if (!objetosGl.TryGetValue(id, out o)) return 0; }
            return ReleaseObjeto(o);
        }
        private static uint ReleaseObjeto(ObjetoGl o)
        {
            int r;
            lock (candadoGl)
            {
                r = --o.Refs;
                if (r > 0) return (uint)r;
                objetosGl.Remove(o.Id);
            }
            o.Libera();
            return 0;
        }
        private static void AddRefObjeto(ObjetoGl o) { lock (candadoGl) o.Refs++; }

        // La memoria de bloquear una textura: una que se reusa (las portadas se suben de una en una) y, si esta ocupada, otra.
        private static IntPtr memoriaBloqueo; private static long memoriaBloqueoBytes; private static bool memoriaBloqueoOcupada;
        private static IntPtr TomaMemoriaBloqueo(TexturaGl t, long bytes)
        {
            lock (candadoGl)
            {
                if (!memoriaBloqueoOcupada)
                {
                    if (memoriaBloqueoBytes < bytes)
                    {
                        if (memoriaBloqueo != IntPtr.Zero) NativeMemory.Free((void*)memoriaBloqueo);
                        memoriaBloqueo = (IntPtr)NativeMemory.Alloc((nuint)bytes); memoriaBloqueoBytes = bytes;
                    }
                    memoriaBloqueoOcupada = true; t.BloqueoPropio = false;
                    return memoriaBloqueo;
                }
            }
            t.BloqueoPropio = true;
            return (IntPtr)NativeMemory.Alloc((nuint)bytes);
        }
        private static void SueltaMemoriaBloqueo(TexturaGl t)
        {
            if (t.BloqueoPropio) { if (t.Bloqueo != IntPtr.Zero) NativeMemory.Free((void*)t.Bloqueo); }
            else lock (candadoGl) memoriaBloqueoOcupada = false;
            t.Bloqueo = IntPtr.Zero; t.BloqueoPropio = false;
        }

        // ======================================================================================================================
        // LAS FUNCIONES DE DIRECT3D 9, EN OPENGL ES 2 (el nombre de cada una es el de Direct3D; entre corchetes, su hueco en la tabla)
        // ======================================================================================================================
        internal static class EnGl
        {
            // los estados de render que se miran aqui (D3DRENDERSTATETYPE)
            private const uint RsAlfaPrueba = 15, RsFuente = 19, RsDestino = 20, RsRecorte = 22, RsAlfaRef = 24, RsAlfaFuncion = 25, RsTramado = 26,
                RsMezcla = 27, RsEspecular = 29, RsFactorTextura = 60, RsEscribe = 168, RsOperacion = 171, RsFactorMezcla = 193,
                RsAlfaAparte = 206, RsFuenteAlfa = 207, RsDestinoAlfa = 208, RsOperacionAlfa = 209;

            // ---- lo que se borra fuera del hilo de OpenGL: se borra en el siguiente cuadro ----
            private static int hiloGl = -1;
            private static readonly List<uint> texturasPorBorrar = new List<uint>(), fbosPorBorrar = new List<uint>(), vbosPorBorrar = new List<uint>();
            private static long bytesTexturas;   // lo que ocupan las texturas de la rocola en la tarjeta (GetAvailableTextureMem lo descuenta)
            internal static long BytesTexturas { get { return Interlocked.Read(ref bytesTexturas); } }
            private static bool EnHiloGl { get { return hiloGl < 0 || Thread.CurrentThread.ManagedThreadId == hiloGl; } }
            internal static void BorraTextura(uint n, long bytes)
            {
                Interlocked.Add(ref bytesTexturas, -bytes);
                if (!EnHiloGl) { lock (texturasPorBorrar) texturasPorBorrar.Add(n); return; }
                GL.DeleteTextures(1, &n);
                OlvidaTextura(n);
            }
            internal static void BorraFbo(uint f)
            {
                if (!EnHiloGl) { lock (fbosPorBorrar) fbosPorBorrar.Add(f); return; }
                GlMas.DeleteFramebuffers(1, &f);
                foreach (DispositivoGl d in dispositivos) if (d.FboPuesto == f) d.FboPuesto = uint.MaxValue;
            }
            internal static void BorraVbo(uint b)
            {
                if (!EnHiloGl) { lock (vbosPorBorrar) vbosPorBorrar.Add(b); return; }
                GlMas.DeleteBuffers(1, &b);
            }
            private static void BorraPendientes()
            {
                lock (texturasPorBorrar) { foreach (uint n0 in texturasPorBorrar) { uint n = n0; GL.DeleteTextures(1, &n); OlvidaTextura(n); } texturasPorBorrar.Clear(); }
                lock (fbosPorBorrar) { foreach (uint f0 in fbosPorBorrar) { uint f = f0; GlMas.DeleteFramebuffers(1, &f); } if (fbosPorBorrar.Count > 0) foreach (DispositivoGl d in dispositivos) d.FboPuesto = uint.MaxValue; fbosPorBorrar.Clear(); }
                lock (vbosPorBorrar) { foreach (uint b0 in vbosPorBorrar) { uint b = b0; GlMas.DeleteBuffers(1, &b); } vbosPorBorrar.Clear(); }
            }
            // una textura borrada que seguia puesta en una etapa: OpenGL puede dar su nombre a otra
            private static void OlvidaTextura(uint n)
            {
                foreach (DispositivoGl d in dispositivos)
                    for (int i = 0; i < EtapasGl; i++) if (d.TexturaPuesta[i] == n) d.TexturaPuesta[i] = uint.MaxValue;
            }
            // Algo de fuera (el anfitrion al componer, subir una textura) cambio lo puesto en OpenGL: se vuelve a poner todo en el siguiente dibujo.
            internal static void EstadoTocado()
            {
                foreach (DispositivoGl d in dispositivos)
                {
                    d.EstadoSucio = true; d.FboPuesto = uint.MaxValue; d.VistaW = d.VistaH = -1;
                    d.MezclaPuesta = d.RecortePuesto = d.TramadoPuesto = d.MascaraPuesta = -1;   // (el anfitrion compone con su propia mezcla)
                    d.FuentePuesta = d.DestinoPuesto = d.FuenteAlfaPuesta = d.DestinoAlfaPuesto = d.EcuacionPuesta = d.EcuacionAlfaPuesta = uint.MaxValue;
                    d.FactorPuesto = -1; d.EspecularPuesto = true;   // (se apaga en el siguiente dibujo que no lo use)
                    for (int i = 0; i < EtapasGl; i++) d.TexturaPuesta[i] = uint.MaxValue;
                    d.ProgramaPuesto = null;
                }
            }
            private static readonly List<DispositivoGl> dispositivos = new List<DispositivoGl>();

            // Lo que tiene esta OpenGL (se pregunta una vez): REPEAT con texturas que no son potencia de 2 (OpenGL ES 3, o ES 2 con
            // GL_OES_texture_npot; Mesa r600 y llvmpipe lo tienen), MIN y MAX en la mezcla, el borde y el espejo una vez.
            private static bool capacidadesVistas, npotCompleto, mezclaMinMax, conBorde, conEspejoUnaVez;
            private static void MiraCapacidades()
            {
                if (capacidadesVistas) return;
                capacidadesVistas = true;
                string v = GL.Texto(GL.VERSION), e = " " + GL.Texto(GlMas.EXTENSIONS) + " ";
                int mayor = 2;
                int i = v.IndexOf("OpenGL ES ", StringComparison.Ordinal);
                if (i >= 0 && v.Length > i + 10 && char.IsDigit(v[i + 10])) mayor = v[i + 10] - '0';
                bool es32 = mayor > 3 || (mayor == 3 && v.Length > i + 12 && v[i + 11] == '.' && v[i + 12] >= '2');
                npotCompleto = mayor >= 3 || e.Contains(" GL_OES_texture_npot ");
                mezclaMinMax = mayor >= 3 || e.Contains(" GL_EXT_blend_minmax ");
                conBorde = es32 || e.Contains(" GL_OES_texture_border_clamp ") || e.Contains(" GL_EXT_texture_border_clamp ");
                conEspejoUnaVez = e.Contains(" GL_EXT_texture_mirror_clamp_to_edge ");
            }

            // ---- Direct3D ----
            internal static IntPtr Direct3DCreate9(uint version)
            {
                if (version != 32) return IntPtr.Zero;   // D3D_SDK_VERSION de Direct3D 9.0c
                return RegistraGl(new Direct3DGl());
            }

            // IDirect3D9::CreateDevice [16]. Hace falta un contexto de OpenGL ES 2 en este hilo (lo abre el anfitrion al empezar).
            internal static int CreateDevice(IntPtr self, uint adaptador, uint tipo, IntPtr ventana, uint banderas, ref PresentParams pp, out IntPtr dispositivo)
            {
                dispositivo = IntPtr.Zero;
                if (ObjetoDe<Direct3DGl>(self) == null || adaptador != 0 || tipo != DEVTYPE_HAL) return D3DERR_INVALIDCALL;
                if (GL.GetString(GL.VERSION) == IntPtr.Zero) return D3DERR_NOTAVAILABLE;   // sin OpenGL en este hilo
                hiloGl = Thread.CurrentThread.ManagedThreadId;
                MiraCapacidades();
                DispositivoGl d = new DispositivoGl();
                d.Ventana = ventana; d.Hilo = hiloGl;
                PresentParams p = pp;
                if (p.BackBufferWidth == 0 || p.BackBufferHeight == 0) { p.BackBufferWidth = (uint)Escritorio.Pantalla.Width; p.BackBufferHeight = (uint)Escritorio.Pantalla.Height; }
                if (p.BackBufferFormat == FMT_UNKNOWN) p.BackBufferFormat = FMT_X8R8G8B8;
                d.Pp = p; pp = p;
                RsDeFabrica(d.Rs, p.EnableAutoDepthStencil != 0);
                if (!HazFondo(d)) return D3DERR_OUTOFVIDEOMEMORY;
                uint negra; GL.GenTextures(1, &negra); GL.BindTexture(GL.TEXTURE_2D, negra);
                uint px = 0xFF000000u;   // (0, 0, 0, 1) en cualquier orden
                GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, 1, 1, 0, GL.RGBA, GL.UNSIGNED_BYTE, &px);
                GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GlMas.NEAREST); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GlMas.NEAREST);
                d.TexturaNegra = negra;
                Interlocked.Add(ref bytesTexturas, 4);
                lock (dispositivos) dispositivos.Add(d);
                dispositivo = RegistraGl(d);
                EstadoTocado();
                return D3D_OK;
            }
            private static bool HazFondo(DispositivoGl d)
            {
                TexturaGl t = NuevaTextura((int)d.Pp.BackBufferWidth, (int)d.Pp.BackBufferHeight, FMT_X8R8G8B8, USAGE_RENDERTARGET, POOL_DEFAULT);
                if (t == null) return false;
                SuperficieGl s = new SuperficieGl(); s.Textura = t; s.DelFondo = true;
                t.Nivel0 = s;
                RegistraGl(s);
                d.Fondo = s;
                AddRefObjeto(s); d.Destino = s;   // el destino tiene su referencia (como SetRenderTarget)
                return true;
            }
            private static DispositivoGl Disp(IntPtr self) { return ObjetoDe<DispositivoGl>(self); }

            // IUnknown::AddRef [1] y Release [2]
            internal static uint AddRef(IntPtr self)
            {
                lock (candadoGl) { ObjetoGl o; if (!objetosGl.TryGetValue(self, out o)) return 0; return (uint)++o.Refs; }
            }
            internal static uint Release(IntPtr self)
            {
                ObjetoGl o;
                lock (candadoGl) { if (!objetosGl.TryGetValue(self, out o)) return 0; }
                DispositivoGl d = o as DispositivoGl;
                if (d != null && o.Refs == 1) lock (dispositivos) dispositivos.Remove(d);
                return ReleaseObjeto(o);
            }

            // IDirect3DDevice9::TestCooperativeLevel [3]: en Rocola OS la tarjeta no se pierde nunca. En XP se perdia (D3DERR_DEVICELOST y
            // luego D3DERR_DEVICENOTRESET) al cambiar de escritorio (Ctrl+Alt+Supr), de resolucion o con otro programa a pantalla completa;
            // aqui no hay nada de eso (el video va en una textura y cambiar de consola cierra la rocola), asi que siempre D3D_OK.
            internal static int TestCooperativeLevel(IntPtr self)
            {
                if (Disp(self) == null) return D3DERR_INVALIDCALL;
                return D3D_OK;
            }
            // [4] lo que queda para texturas: lo de la tarjeta (VRAM + GTT del controlador radeon; si no se sabe, 512 MB) menos lo que ya ocupan
            // las de la rocola (Direct3D tambien lo descuenta). Redondeado a MB como el de XP.
            internal static uint GetAvailableTextureMem(IntPtr self)
            {
                if (Disp(self) == null) return 0;
                long total = MemoriaTarjeta(), libre = total - BytesTexturas;
                if (libre < 0) libre = 0;
                libre &= ~0xFFFFFL;
                return (uint)Math.Min(libre, 0xFFF00000L);
            }
            private static long memoriaTarjeta = -1;
            private static long MemoriaTarjeta()
            {
                if (memoriaTarjeta >= 0) return memoriaTarjeta;
                long m = MemoriaRadeon();
                if (m <= 0) m = 512L << 20;
                memoriaTarjeta = m;
                return m;
            }
            // DRM_IOCTL_RADEON_GEM_INFO: gart_size, vram_size, vram_visible (lo que el controlador radeon le da a las texturas)
            [DllImport("libc", SetLastError = true)] private static extern int open(string ruta, int banderas);
            [DllImport("libc")] private static extern int close(int fd);
            [DllImport("libc", SetLastError = true)] private static extern int ioctl(int fd, ulong peticion, void* datos);
            private static long MemoriaRadeon()
            {
                // solo con el controlador radeon (el del A4): el mismo numero de llamada es otra cosa en otros controladores
                try
                {
                    FileSystemInfo c = Directory.ResolveLinkTarget("/sys/class/drm/card0/device/driver", true);
                    if (c == null || c.Name != "radeon") return 0;
                }
                catch { return 0; }
                ulong* info = stackalloc ulong[3];
                foreach (string r in new[] { "/dev/dri/renderD128", "/dev/dri/card0" })
                {
                    int fd = -1;
                    try
                    {
                        fd = open(r, 2 | 0x80000);
                        if (fd < 0) continue;
                        info[0] = info[1] = info[2] = 0;
                        if (ioctl(fd, 0xC018645CUL, info) == 0 && info[1] > 0) return (long)(info[0] + info[1]);
                    }
                    catch { }
                    finally { if (fd >= 0) close(fd); }
                }
                return 0;
            }
            // [5] EvictManagedResources: en OpenGL no hay copia de cada textura en la memoria del equipo que sacar; no hace nada.
            internal static int EvictManagedResources(IntPtr self) { return Disp(self) != null ? D3D_OK : D3DERR_INVALIDCALL; }

            // [16] Reset: el fondo a la medida nueva y todo el estado como al crear el dispositivo (como Direct3D 9: los estados de render,
            // de las etapas y del muestreo a los de fabrica, sin texturas ni vertices puestos, el FVF a 0 y el destino otra vez el fondo).
            // (Direct3D 9 ademas no deja hacer Reset mientras vivan recursos de POOL_DEFAULT: XP suelta los suyos antes -LiberaVertices y
            // LiberaMedia-, y aqui no se comprueba.)
            internal static int Reset(IntPtr self, ref PresentParams pp)
            {
                DispositivoGl d = Disp(self);
                if (d == null || d.EnEscena) return D3DERR_INVALIDCALL;
                PresentParams p = pp;
                if (p.BackBufferWidth == 0 || p.BackBufferHeight == 0) { p.BackBufferWidth = d.Pp.BackBufferWidth; p.BackBufferHeight = d.Pp.BackBufferHeight; }
                if (p.BackBufferFormat == FMT_UNKNOWN) p.BackBufferFormat = FMT_X8R8G8B8;
                bool otraMedida = p.BackBufferWidth != d.Pp.BackBufferWidth || p.BackBufferHeight != d.Pp.BackBufferHeight;
                d.Pp = p;
                pp = p;   // (Direct3D 9 pone a cero la medida al volver si falla; aqui no falla)
                if (d.Destino != null && d.Destino != d.Fondo) { ReleaseObjeto(d.Destino); d.Destino = d.Fondo; AddRefObjeto(d.Fondo); }
                if (otraMedida)
                {
                    SuperficieGl viejo = d.Fondo;
                    if (d.Destino == viejo) { d.Destino = null; ReleaseObjeto(viejo); }
                    d.Fondo = null;
                    ReleaseObjeto(viejo);
                    if (!HazFondo(d)) return D3DERR_OUTOFVIDEOMEMORY;
                }
                for (int i = 0; i < d.Etapas.Length; i++) { IntPtr t = d.Etapas[i].Textura; d.Etapas[i] = Etapa.DeFabrica(i); if (t != IntPtr.Zero) ReleaseId(t); }
                if (d.Flujo != IntPtr.Zero) { ReleaseId(d.Flujo); d.Flujo = IntPtr.Zero; }
                RsDeFabrica(d.Rs, p.EnableAutoDepthStencil != 0);
                d.Fvf = 0; d.ProgramaSucio = true;
                EstadoTocado();
                return D3D_OK;
            }

            // [17] Present: el fondo, al escritorio, que compone y lo pone en la pantalla.
            internal static int Present(IntPtr self, IntPtr a, IntPtr b, IntPtr c, IntPtr e)
            {
                DispositivoGl d = Disp(self);
                if (d == null || d.EnEscena || d.Fondo == null || d.Fondo.Textura == null) return D3DERR_INVALIDCALL;
                d.Presentaciones++;
                trazando = false;
                Control control = Control.FromHandle(d.Ventana);
                if (control != null)
                {
                    control.TexturaGl = d.Fondo.Textura.Nombre; control.AnchoGl = d.Fondo.W; control.AltoGl = d.Fondo.H;
                    Escritorio.SinEspera = d.Pp.PresentationInterval == PRESENT_INTERVAL_IMMEDIATE;
                    Escritorio.PresentaNativa(control);
                    EstadoTocado();
                }
                else GlMas.Flush();
                BorraPendientes();
                return D3D_OK;
            }

            // [23] CreateTexture: de un nivel (XP siempre pide 1; 0 seria la cadena entera de mipmaps, que aqui no se hace), A8R8G8B8 o
            // X8R8G8B8. Como Direct3D: un render target solo en POOL_DEFAULT.
            internal static int CreateTexture(IntPtr self, uint w, uint h, uint niveles, uint uso, uint formato, uint pool, out IntPtr textura, IntPtr compartida)
            {
                textura = IntPtr.Zero;
                if (Disp(self) == null || w == 0 || h == 0 || niveles != 1 || (formato != FMT_A8R8G8B8 && formato != FMT_X8R8G8B8)) return D3DERR_INVALIDCALL;
                if ((uso & USAGE_RENDERTARGET) != 0 && pool != POOL_DEFAULT) return D3DERR_INVALIDCALL;
                int max = MaxTextura();
                if (w > max || h > max) return D3DERR_INVALIDCALL;
                TexturaGl t = NuevaTextura((int)w, (int)h, formato, uso, pool);
                if (t == null) return D3DERR_OUTOFVIDEOMEMORY;
                textura = RegistraGl(t);
                return D3D_OK;
            }
            private static int maxTextura;
            internal static int MaxTextura()
            {
                if (maxTextura > 0) return maxTextura;
                int v = 0; GL.GetIntegerv(GlMas.MAX_TEXTURE_SIZE, &v);
                maxTextura = v > 0 ? v : 2048;
                return maxTextura;
            }
            private static TexturaGl NuevaTextura(int w, int h, uint formato, uint uso, uint pool)
            {
                while (GlMas.GetError() != 0) { }
                uint n; GL.GenTextures(1, &n);
                GL.BindTexture(GL.TEXTURE_2D, n);
                GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, w, h, 0, GL.RGBA, GL.UNSIGNED_BYTE, null);
                if (GlMas.GetError() == GlMas.OUT_OF_MEMORY) { GL.DeleteTextures(1, &n); EstadoTocado(); return null; }
                TexturaGl t = new TexturaGl();
                t.Nombre = n; t.W = w; t.H = h; t.Formato = formato; t.Uso = uso; t.Pool = pool; t.SinAlfa = formato == FMT_X8R8G8B8;
                t.Npot = (w & (w - 1)) != 0 || (h & (h - 1)) != 0;
                t.PuestoS = t.PuestoT = t.PuestoMin = t.PuestoMag = uint.MaxValue;
                Interlocked.Add(ref bytesTexturas, t.Bytes);
                // Lo de dentro de una textura nueva no esta definido en Direct3D (aqui, ceros); pero una X8R8G8B8 se lee SIEMPRE con alfa 1,
                // tambien antes de pintar en ella: se le pone (0, 0, 0, 1).
                if (t.SinAlfa)
                {
                    uint f; GL.GenFramebuffers(1, &f);
                    GL.BindFramebuffer(GL.FRAMEBUFFER, f);
                    GL.FramebufferTexture2D(GL.FRAMEBUFFER, GL.COLOR_ATTACHMENT0, GL.TEXTURE_2D, n, 0);
                    GlMas.ColorMask(1, 1, 1, 1); GL.Disable(GL.SCISSOR_TEST);
                    GL.ClearColor(0, 0, 0, 1); GL.Clear(GL.COLOR_BUFFER_BIT);
                    GlMas.DeleteFramebuffers(1, &f);
                }
                EstadoTocado();
                return t;
            }

            // IDirect3DResource9::PreLoad [9]: OpenGL ya la subio al soltarla; no hace nada
            internal static void PreLoad(IntPtr self) { }

            // IDirect3DTexture9::GetLevelDesc [17]: D3DSURFACE_DESC entero (Format, Type, Usage, Pool, MultiSampleType, MultiSampleQuality,
            // Width, Height)
            internal static int GetLevelDesc(IntPtr self, uint nivel, ref SurfaceDesc desc)
            {
                TexturaGl t = ObjetoDe<TexturaGl>(self);
                if (t == null || nivel != 0) return D3DERR_INVALIDCALL;
                fixed (SurfaceDesc* pd = &desc)
                {
                    uint* q = (uint*)pd;
                    q[0] = t.Formato; q[1] = 1; /* D3DRTYPE_SURFACE */ q[2] = t.Uso; q[3] = t.Pool; q[4] = 0; q[5] = 0;
                }
                desc.Width = (uint)t.W; desc.Height = (uint)t.H;
                return D3D_OK;
            }
            // IDirect3DTexture9::GetSurfaceLevel [18]: la superficie de la textura (para pintar en ella), con su referencia
            internal static int GetSurfaceLevel(IntPtr self, uint nivel, out IntPtr superficie)
            {
                superficie = IntPtr.Zero;
                TexturaGl t = ObjetoDe<TexturaGl>(self);
                if (t == null || nivel != 0) return D3DERR_INVALIDCALL;
                if (t.Nivel0 == null)
                {
                    SuperficieGl s = new SuperficieGl(); s.Textura = t;
                    AddRefObjeto(t);   // la superficie tiene viva su textura
                    t.Nivel0 = s;
                    superficie = RegistraGl(s);
                    return D3D_OK;
                }
                AddRefObjeto(t.Nivel0);
                superficie = t.Nivel0.Id;
                return D3D_OK;
            }
            // IDirect3DTexture9::LockRect [19] (toda la textura, o el rectangulo de 'rect'). Como Direct3D: no se bloquea una de POOL_DEFAULT
            // que no sea DYNAMIC (un render target), ni el nivel que no hay. Lo bloqueado se escribe entero antes de soltarlo: Direct3D daria lo
            // que ya tenia la textura y aqui la memoria trae lo que sea (todo lo que bloquea XP lo escribe entero: SubeBitmapBorde y
            // ActualizaRegionBitmap copian el mapa de bits y estiran el borde hasta el final de lo bloqueado).
            private const uint UsoDinamico = 0x200, BloqueoSoloLeer = 0x10;
            internal static int LockRect(IntPtr self, uint nivel, out LockedRect lr, IntPtr rect, uint banderas)
            {
                lr = new LockedRect();
                TexturaGl t = ObjetoDe<TexturaGl>(self);
                if (t == null) return D3DERR_INVALIDCALL;
                if (rect == IntPtr.Zero) return Bloquea(t, nivel, 0, 0, t.W, t.H, banderas, out lr);
                RectI r = *(RectI*)rect;
                return Bloquea(t, nivel, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, banderas, out lr);
            }
            internal static int LockRect(IntPtr self, uint nivel, out LockedRect lr, ref RectI rect, uint banderas)
            {
                lr = new LockedRect();
                TexturaGl t = ObjetoDe<TexturaGl>(self);
                if (t == null) return D3DERR_INVALIDCALL;
                return Bloquea(t, nivel, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, banderas, out lr);
            }
            private static int Bloquea(TexturaGl t, uint nivel, int x, int y, int w, int h, uint banderas, out LockedRect lr)
            {
                lr = new LockedRect();
                if (nivel != 0 || t.Bloqueada || (t.Pool == POOL_DEFAULT && (t.Uso & UsoDinamico) == 0) || x < 0 || y < 0 || w <= 0 || h <= 0 || x + w > t.W || y + h > t.H) return D3DERR_INVALIDCALL;
                long bytes = (long)w * h * 4;
                IntPtr m = TomaMemoriaBloqueo(t, bytes);
                t.Bloqueo = m; t.BX = x; t.BY = y; t.BW = w; t.BH = h; t.Bloqueada = true; t.BloqueoSoloLeer = (banderas & BloqueoSoloLeer) != 0;
                lr.Pitch = w * 4; lr.Bits = m;
                return D3D_OK;
            }
            // IDirect3DTexture9::UnlockRect [20]: lo escrito, a la tarjeta (tal cual: B, G, R, A; en una X8R8G8B8, con el alfa en 1)
            internal static int UnlockRect(IntPtr self, uint nivel)
            {
                TexturaGl t = ObjetoDe<TexturaGl>(self);
                if (t == null || nivel != 0 || !t.Bloqueada) return D3DERR_INVALIDCALL;
                if (!t.BloqueoSoloLeer)
                {
                    if (t.SinAlfa) { byte* p = (byte*)t.Bloqueo + 3; for (long i = 0, n = (long)t.BW * t.BH; i < n; i++, p += 4) *p = 0xFF; }
                    GL.BindTexture(GL.TEXTURE_2D, t.Nombre);
                    GL.PixelStorei(GL.UNPACK_ALIGNMENT, 4);
                    GL.TexSubImage2D(GL.TEXTURE_2D, 0, t.BX, t.BY, t.BW, t.BH, GL.RGBA, GL.UNSIGNED_BYTE, (void*)t.Bloqueo);
                    if (volcado != null && t.BW >= 64 && t.BH >= 64) Vuelca(t);
                    EstadoTocado();
                }
                t.Bloqueada = false;
                SueltaMemoriaBloqueo(t);
                return D3D_OK;
            }

            // (pruebas) ROCOLA_TRAZA_DIBUJOS=S: cada dibujo del cuadro que empieza a los S segundos (estado, textura y lo que ocupa)
            private static readonly double traza = Environment.GetEnvironmentVariable("ROCOLA_TRAZA_DIBUJOS") != null ? double.Parse(Environment.GetEnvironmentVariable("ROCOLA_TRAZA_DIBUJOS"), System.Globalization.CultureInfo.InvariantCulture) * 1000.0 : 0;
            private static bool trazando, trazado;
            // (pruebas) ROCOLA_SALTA_DIBUJOS=k1,k2: en cada cuadro no se hacen esos dibujos (para buscar cual pinta algo mal)
            private static int dibujoEnCuadro;
            private static readonly int[] salta = Environment.GetEnvironmentVariable("ROCOLA_SALTA_DIBUJOS") == null ? null : Array.ConvertAll(Environment.GetEnvironmentVariable("ROCOLA_SALTA_DIBUJOS").Split(','), int.Parse);
            private static void Traza(DispositivoGl d, byte* v, int paso, int n)
            {
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue; uint c0 = 0;
                for (int i = 0; i < n; i++) { float* f = (float*)(v + (long)i * paso); x0 = Math.Min(x0, f[0]); y0 = Math.Min(y0, f[1]); x1 = Math.Max(x1, f[0]); y1 = Math.Max(y1, f[1]); if (i == 0) c0 = *(uint*)(v + 16); }
                TexturaGl t = ObjetoDe<TexturaGl>(d.Etapas[0].Textura);
                StringBuilder etapas = new StringBuilder();
                for (int i = 0; i < EtapasGl; i++)
                {
                    Etapa e = d.Etapas[i];
                    etapas.Append(e.ColorOp).Append(',').Append(e.ColorArg1).Append(',').Append(e.ColorArg2).Append(',').Append(e.AlphaOp).Append(',').Append(e.AlphaArg1).Append(',').Append(e.AlphaArg2).Append(';');
                    if (e.ColorOp == TOP_DISABLE) break;
                }
                Console.WriteLine("dibujo n=" + n + " caja=(" + x0.ToString("0") + "," + y0.ToString("0") + ")-(" + x1.ToString("0") + "," + y1.ToString("0") + ") color=" + c0.ToString("X8") +
                    " mezcla=" + (d.Rs[RsMezcla] != 0 ? d.Rs[RsFuente] + "/" + d.Rs[RsDestino] : "no") + " etapas=" + etapas + " tex=" + (t != null ? t.W + "x" + t.H + "#" + t.Nombre : "-") + " filtro=" + d.Etapas[0].FiltroMin + "," + d.Etapas[0].FiltroMag +
                    " destino=" + (d.Destino == d.Fondo ? "fondo" : d.Destino.W + "x" + d.Destino.H));
            }

            // (pruebas) ROCOLA_VOLCADO_TEXTURAS=carpeta: cada textura grande que se sube, en PNG (como la ve Direct3D: sin premultiplicar)
            private static readonly string volcado = Environment.GetEnvironmentVariable("ROCOLA_VOLCADO_TEXTURAS");
            private static int volcadas;
            private static void Vuelca(TexturaGl t)
            {
                try
                {
                    using (SkiaSharp.SKBitmap b = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(t.BW, t.BH, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Unpremul)))
                    {
                        Buffer.MemoryCopy((void*)t.Bloqueo, (void*)b.GetPixels(), (long)t.BW * t.BH * 4, (long)t.BW * t.BH * 4);
                        using (SkiaSharp.SKData d = b.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
                        using (FileStream f = File.Create(Path.Combine(volcado, (++volcadas).ToString("0000") + "_" + (Escritorio.AhoraMs / 1000).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s_" + t.W + "x" + t.H + "_n" + t.Nombre + "_en" + t.BX + "," + t.BY + "_" + t.BW + "x" + t.BH + ".png")))
                            d.SaveTo(f);
                    }
                }
                catch { }
            }

            // [26] CreateVertexBuffer, IDirect3DVertexBuffer9::Lock [11] y Unlock [12]
            internal static int CreateVertexBuffer(IntPtr self, uint largo, uint uso, uint fvf, uint pool, out IntPtr vertices, IntPtr compartido)
            {
                vertices = IntPtr.Zero;
                if (Disp(self) == null || largo == 0) return D3DERR_INVALIDCALL;
                VerticesGl v = new VerticesGl(); v.Bytes = (int)largo;
                uint b; GlMas_GenBuffer(&b);
                GL.BindBuffer(GL.ARRAY_BUFFER, b);
                GL.BufferData(GL.ARRAY_BUFFER, (IntPtr)largo, null, GlMas.DYNAMIC_DRAW);
                GL.BindBuffer(GL.ARRAY_BUFFER, 0);
                v.Vbo = b;
                v.Copia = (IntPtr)NativeMemory.Alloc((nuint)largo);
                vertices = RegistraGl(v);
                return D3D_OK;
            }
            private static void GlMas_GenBuffer(uint* b) { GL.GenBuffers(1, b); }
            // (DISCARD y NOOVERWRITE dan lo mismo: lo bloqueado se sube entero al soltar, sin pisar lo que ya se dibujo. Con 0 bytes se
            // bloquea desde 'desp' hasta el final.)
            internal static int LockVB(IntPtr self, uint desp, uint tam, out IntPtr datos, uint banderas)
            {
                datos = IntPtr.Zero;
                VerticesGl v = ObjetoDe<VerticesGl>(self);
                if (v == null || v.Bloqueado || desp > (uint)v.Bytes) return D3DERR_INVALIDCALL;
                if (tam == 0) tam = (uint)v.Bytes - desp;
                if ((ulong)desp + tam > (uint)v.Bytes) return D3DERR_INVALIDCALL;
                v.BDesde = (int)desp; v.BBytes = (int)tam; v.Bloqueado = true;
                datos = new IntPtr(v.Copia.ToInt64() + desp);
                return D3D_OK;
            }
            internal static int UnlockVB(IntPtr self)
            {
                VerticesGl v = ObjetoDe<VerticesGl>(self);
                if (v == null || !v.Bloqueado) return D3DERR_INVALIDCALL;
                if (v.BBytes > 0)
                {
                    GL.BindBuffer(GL.ARRAY_BUFFER, v.Vbo);
                    GlMas.BufferSubData(GL.ARRAY_BUFFER, (IntPtr)v.BDesde, (IntPtr)v.BBytes, (void*)(v.Copia.ToInt64() + v.BDesde));
                    GL.BindBuffer(GL.ARRAY_BUFFER, 0);
                }
                v.Bloqueado = false;
                return D3D_OK;
            }
            // [100] SetStreamSource (con su referencia, como Direct3D)
            internal static int SetStreamSource(IntPtr self, uint flujo, IntPtr vertices, uint desp, uint paso)
            {
                DispositivoGl d = Disp(self);
                if (d == null || flujo != 0) return D3DERR_INVALIDCALL;
                if (vertices != IntPtr.Zero) { VerticesGl v = ObjetoDe<VerticesGl>(vertices); if (v == null) return D3DERR_INVALIDCALL; AddRefObjeto(v); }
                if (d.Flujo != IntPtr.Zero) ReleaseId(d.Flujo);
                d.Flujo = vertices; d.FlujoDesp = desp; d.FlujoPaso = paso;
                return D3D_OK;
            }

            // [37] SetRenderTarget y [38] GetRenderTarget (con sus referencias). Como Direct3D: solo una superficie de render target, y lo que
            // ya tenia pintado se queda.
            internal static int SetRenderTarget(IntPtr self, uint indice, IntPtr superficie)
            {
                DispositivoGl d = Disp(self);
                SuperficieGl s = ObjetoDe<SuperficieGl>(superficie);
                if (d == null || indice != 0 || s == null || s.Textura == null || (!s.DelFondo && (s.Textura.Uso & USAGE_RENDERTARGET) == 0)) return D3DERR_INVALIDCALL;
                if (s.Fbo == 0 && !HazFbo(s)) return D3DERR_INVALIDCALL;
                AddRefObjeto(s);
                if (d.Destino != null) ReleaseObjeto(d.Destino);
                d.Destino = s;
                d.FboPuesto = uint.MaxValue;   // y la vista a la medida del destino nuevo (como Direct3D)
                return D3D_OK;
            }
            private static bool HazFbo(SuperficieGl s)
            {
                uint f; GL.GenFramebuffers(1, &f);
                GL.BindFramebuffer(GL.FRAMEBUFFER, f);
                GL.FramebufferTexture2D(GL.FRAMEBUFFER, GL.COLOR_ATTACHMENT0, GL.TEXTURE_2D, s.Textura.Nombre, 0);
                uint estado = GL.CheckFramebufferStatus(GL.FRAMEBUFFER);
                EstadoTocado();
                if (estado != GL.FRAMEBUFFER_COMPLETE) { GlMas.DeleteFramebuffers(1, &f); return false; }
                s.Fbo = f;
                return true;
            }
            internal static int GetRenderTarget(IntPtr self, uint indice, out IntPtr superficie)
            {
                superficie = IntPtr.Zero;
                DispositivoGl d = Disp(self);
                if (d == null || indice != 0 || d.Destino == null) return D3DERR_INVALIDCALL;
                AddRefObjeto(d.Destino);
                superficie = d.Destino.Id;
                return D3D_OK;
            }

            // [41] BeginScene y [42] EndScene
            internal static int BeginScene(IntPtr self)
            {
                DispositivoGl d = Disp(self);
                if (d == null || d.EnEscena) return D3DERR_INVALIDCALL;
                BorraPendientes();
                d.EnEscena = true;
                dibujoEnCuadro = 0;
                if (traza > 0 && !trazado && Escritorio.AhoraMs >= traza) { trazando = true; trazado = true; Console.WriteLine("---- cuadro a los " + (Escritorio.AhoraMs / 1000).ToString("0.00") + " s"); }
                return D3D_OK;
            }
            internal static int EndScene(IntPtr self)
            {
                DispositivoGl d = Disp(self);
                if (d == null || !d.EnEscena) return D3DERR_INVALIDCALL;
                d.EnEscena = false;
                return D3D_OK;
            }

            // [57] SetRenderState: se guarda (y lo que toca el programa de las etapas lo marca); lo demas se pone en OpenGL en el dibujo.
            internal static int SetRenderState(IntPtr self, uint estado, uint valor)
            {
                DispositivoGl d = Disp(self);
                if (d == null || estado >= EstadosRs) return D3DERR_INVALIDCALL;
                if (d.Rs[estado] == valor) return D3D_OK;
                d.Rs[estado] = valor;
                if (estado == RsAlfaPrueba || estado == RsAlfaFuncion || estado == RsEspecular) d.ProgramaSucio = true;
                return D3D_OK;
            }
            // [65] SetTexture (con su referencia, como Direct3D: una textura puesta no se borra hasta quitarla)
            internal static int SetTexture(IntPtr self, uint etapa, IntPtr textura)
            {
                DispositivoGl d = Disp(self);
                if (d == null || etapa >= d.Etapas.Length) return D3DERR_INVALIDCALL;
                if (textura != IntPtr.Zero)
                {
                    TexturaGl t = ObjetoDe<TexturaGl>(textura);
                    if (t == null) return D3DERR_INVALIDCALL;
                    AddRefObjeto(t);
                }
                IntPtr antes = d.Etapas[etapa].Textura;
                d.Etapas[etapa].Textura = textura;
                if (antes != IntPtr.Zero) ReleaseId(antes);
                if (etapa < EtapasGl)
                {
                    d.TexturaPuesta[etapa] = uint.MaxValue;
                    if ((antes == IntPtr.Zero) != (textura == IntPtr.Zero)) d.ProgramaSucio = true;   // con o sin textura es otro programa
                }
                return D3D_OK;
            }
            // [67] SetTextureStageState
            internal static int SetTextureStageState(IntPtr self, uint etapa, uint tipo, uint valor)
            {
                DispositivoGl d = Disp(self);
                if (d == null || etapa >= d.Etapas.Length) return D3DERR_INVALIDCALL;
                uint antes;
                switch (tipo)
                {
                    case TSS_COLOROP: antes = d.Etapas[etapa].ColorOp; d.Etapas[etapa].ColorOp = valor; break;
                    case TSS_COLORARG1: antes = d.Etapas[etapa].ColorArg1; d.Etapas[etapa].ColorArg1 = valor; break;
                    case TSS_COLORARG2: antes = d.Etapas[etapa].ColorArg2; d.Etapas[etapa].ColorArg2 = valor; break;
                    case TSS_ALPHAOP: antes = d.Etapas[etapa].AlphaOp; d.Etapas[etapa].AlphaOp = valor; break;
                    case TSS_ALPHAARG1: antes = d.Etapas[etapa].AlphaArg1; d.Etapas[etapa].AlphaArg1 = valor; break;
                    case TSS_ALPHAARG2: antes = d.Etapas[etapa].AlphaArg2; d.Etapas[etapa].AlphaArg2 = valor; break;
                    case 26: antes = d.Etapas[etapa].ColorArg0; d.Etapas[etapa].ColorArg0 = valor; break;   // D3DTSS_COLORARG0
                    case 27: antes = d.Etapas[etapa].AlphaArg0; d.Etapas[etapa].AlphaArg0 = valor; break;   // D3DTSS_ALPHAARG0
                    case 28: antes = d.Etapas[etapa].Resultado; d.Etapas[etapa].Resultado = valor; break;   // D3DTSS_RESULTARG
                    case TSS_TEXCOORDINDEX: d.Etapas[etapa].IndiceCoordenadas = valor & 0xFFFF; return D3D_OK;   // (el juego se elige al dibujar)
                    default: return D3D_OK;   // relieve, transformar coordenadas, constante: XP no los usa
                }
                if (antes != valor) d.ProgramaSucio = true;
                return D3D_OK;
            }
            // [69] SetSamplerState
            internal static int SetSamplerState(IntPtr self, uint etapa, uint tipo, uint valor)
            {
                DispositivoGl d = Disp(self);
                if (d == null || etapa >= d.Etapas.Length) return D3DERR_INVALIDCALL;
                switch (tipo)
                {
                    case SAMP_ADDRESSU: d.Etapas[etapa].DireccionU = valor; break;
                    case SAMP_ADDRESSV: d.Etapas[etapa].DireccionV = valor; break;
                    case 3: d.Etapas[etapa].DireccionW = valor; break;       // ADDRESSW (texturas de volumen: no las hay)
                    case 4: d.Etapas[etapa].Borde = valor; break;            // BORDERCOLOR
                    case SAMP_MAGFILTER: d.Etapas[etapa].FiltroMag = valor; break;
                    case SAMP_MINFILTER: d.Etapas[etapa].FiltroMin = valor; break;
                    case 7: d.Etapas[etapa].FiltroMip = valor; break;        // MIPFILTER, MIPMAPLODBIAS, MAXMIPLEVEL, MAXANISOTROPY: con un nivel
                    case 8: d.Etapas[etapa].SesgoMip = valor; break;         // no cambian nada
                    case 9: d.Etapas[etapa].NivelMip = valor; break;
                    case 10: d.Etapas[etapa].Anisotropia = valor; break;
                    default: break;                                           // SRGBTEXTURE, ELEMENTINDEX, DMAPOFFSET: XP no los usa
                }
                return D3D_OK;
            }
            // [70] ValidateDevice: lo que pidan las etapas lo hace un programa, en una pasada; lo que no se hace aqui lo dice con el error de
            // Direct3D (como un controlador que no lo tiene).
            internal static int ValidateDevice(IntPtr self, out uint pasadas)
            {
                pasadas = 1;
                DispositivoGl d = Disp(self);
                if (d == null) return D3DERR_INVALIDCALL;
                for (int i = 0; i < d.Etapas.Length; i++)
                {
                    Etapa e = d.Etapas[i];
                    if (e.ColorOp == TOP_DISABLE) break;
                    if (i >= EtapasGl) return D3DERR_TOOMANYOPERATIONS;
                    if (!OpHecha(e.ColorOp, true)) return D3DERR_UNSUPPORTEDCOLOROPERATION;
                    if (e.AlphaOp != TOP_DISABLE && !OpHecha(e.AlphaOp, false)) return D3DERR_UNSUPPORTEDALPHAOPERATION;
                    if (!ArgHecho(e.ColorArg0) || !ArgHecho(e.ColorArg1) || !ArgHecho(e.ColorArg2) || (e.Resultado & 0xF) != TA_CURRENT && (e.Resultado & 0xF) != 5) return D3DERR_UNSUPPORTEDCOLORARG;
                    if (!ArgHecho(e.AlphaArg0) || !ArgHecho(e.AlphaArg1) || !ArgHecho(e.AlphaArg2)) return D3DERR_UNSUPPORTEDALPHAARG;
                    if (e.FiltroMag > 3 || e.FiltroMin > 3 || e.FiltroMip > 2) return D3DERR_UNSUPPORTEDTEXTUREFILTER;
                }
                return D3D_OK;   // (la mezcla no la mira: en Direct3D 9 la dicen las capacidades)
            }
            private static bool OpHecha(uint op, bool color)
            {
                if (op >= 2 && op <= 16) return true;                         // SELECTARG1 ... BLENDCURRENTALPHA
                if (op >= 18 && op <= 21) return color;                       // MODULATEALPHA_ADDCOLOR ... (solo de color)
                return op == 24 || op == 25 || op == 26;                      // DOTPRODUCT3, MULTIPLYADD, LERP
            }
            private static bool ArgHecho(uint a) { return (a & 0xF) <= 5 && (a & ~0x3Fu) == 0; }   // DIFFUSE ... TEMP (CONSTANT, no)
            // [89] SetFVF
            internal static int SetFVF(IntPtr self, uint fvf)
            {
                DispositivoGl d = Disp(self);
                if (d == null) return D3DERR_INVALIDCALL;
                d.Fvf = fvf;
                return D3D_OK;
            }

            // [83] DrawPrimitiveUP: los vertices del programa, tal cual (OpenGL los copia al dibujar). Como Direct3D 9, despues el flujo 0
            // queda en NULL (y suelta su referencia).
            internal static int DrawPrimitiveUP(IntPtr self, uint tipo, uint cuantos, IntPtr datos, uint paso)
            {
                DispositivoGl d = Disp(self);
                if (d == null || !d.EnEscena || datos == IntPtr.Zero || cuantos == 0) return D3DERR_INVALIDCALL;
                uint modo; int n;
                if (!Primitiva(tipo, cuantos, out modo, out n)) return D3DERR_INVALIDCALL;
                if (d.Flujo != IntPtr.Zero) { ReleaseId(d.Flujo); d.Flujo = IntPtr.Zero; d.FlujoDesp = d.FlujoPaso = 0; }
                if (!Prepara(d)) return D3DERR_INVALIDCALL;
                if (trazando) Traza(d, (byte*)datos, (int)paso, n);
                dibujoEnCuadro++;
                if (salta != null && Array.IndexOf(salta, dibujoEnCuadro) >= 0) return D3D_OK;
                GL.BindBuffer(GL.ARRAY_BUFFER, 0);
                if (!Atributos(d, (byte*)datos, (int)paso)) return D3DERR_INVALIDCALL;
                GL.DrawArrays(modo, 0, n);
                return D3D_OK;
            }
            // [81] DrawPrimitive: de los vertices puestos con SetStreamSource, desde el vertice 'desde'
            internal static int DrawPrimitive(IntPtr self, uint tipo, uint desde, uint cuantos)
            {
                DispositivoGl d = Disp(self);
                VerticesGl v = d != null ? ObjetoDe<VerticesGl>(d.Flujo) : null;
                if (d == null || v == null || !d.EnEscena || cuantos == 0 || d.FlujoPaso == 0) return D3DERR_INVALIDCALL;
                uint modo; int n;
                if (!Primitiva(tipo, cuantos, out modo, out n)) return D3DERR_INVALIDCALL;
                if (!Prepara(d)) return D3DERR_INVALIDCALL;
                GL.BindBuffer(GL.ARRAY_BUFFER, v.Vbo);
                bool ok = Atributos(d, (byte*)0 + d.FlujoDesp + (long)desde * d.FlujoPaso, (int)d.FlujoPaso);
                if (ok) GL.DrawArrays(modo, 0, n);
                GL.BindBuffer(GL.ARRAY_BUFFER, 0);
                return ok ? D3D_OK : D3DERR_INVALIDCALL;
            }
            private static bool Primitiva(uint tipo, uint cuantos, out uint modo, out int n)
            {
                modo = 0; n = 0;
                switch (tipo)
                {
                    case 1: modo = GlMas.POINTS; n = (int)cuantos; return true;
                    case 2: modo = GlMas.LINES; n = (int)cuantos * 2; return true;
                    case 3: modo = GlMas.LINE_STRIP; n = (int)cuantos + 1; return true;
                    case 4: modo = GL.TRIANGLES; n = (int)cuantos * 3; return true;
                    case 5: modo = GlMas.TRIANGLE_STRIP; n = (int)cuantos + 2; return true;
                    case 6: modo = GlMas.TRIANGLE_FAN; n = (int)cuantos + 2; return true;
                    default: return false;
                }
            }

            // ---- poner en OpenGL lo que pidio el programa, antes de cada dibujo (solo lo que cambio) ----
            private static bool Prepara(DispositivoGl d)
            {
                SuperficieGl s = d.Destino;
                if (s == null || s.Textura == null) return false;
                // el destino (y la vista: toda la superficie, como Direct3D al cambiar de destino)
                if (s.Fbo == 0 && !HazFbo(s)) return false;
                if (d.FboPuesto != s.Fbo) { GL.BindFramebuffer(GL.FRAMEBUFFER, s.Fbo); d.FboPuesto = s.Fbo; d.VistaW = d.VistaH = -1; }
                if (d.VistaW != s.W || d.VistaH != s.H) { GL.Viewport(0, 0, s.W, s.H); d.VistaW = s.W; d.VistaH = s.H; }
                if (d.EstadoSucio)
                {
                    // lo que Direct3D no tiene (o tiene apagado de fabrica y XP no enciende): fuera
                    GL.Disable(GL.SCISSOR_TEST); GL.Disable(GlMas.DEPTH_TEST); GL.Disable(GlMas.STENCIL_TEST); GL.Disable(GlMas.POLYGON_OFFSET_FILL);
                    GL.Disable(GlMas.SAMPLE_ALPHA_TO_COVERAGE); GL.Disable(GlMas.SAMPLE_COVERAGE);
                    GlMas.FrontFace(GlMas.CCW);
                    d.EstadoSucio = false;
                }
                uint[] rs = d.Rs;
                // COLORWRITEENABLE: el rojo de OpenGL es el azul de Direct3D (el orden de la memoria); el alfa de una X8 no se escribe nunca
                uint cw = rs[RsEscribe];
                int mascara = (int)(((cw >> 2) & 1) | (cw & 2) | ((cw & 1) << 2) | (s.Textura.SinAlfa ? 0 : (cw & 8)));
                if (d.MascaraPuesta != mascara)
                { GlMas.ColorMask((byte)(mascara & 1), (byte)((mascara >> 1) & 1), (byte)((mascara >> 2) & 1), (byte)((mascara >> 3) & 1)); d.MascaraPuesta = mascara; }
                // la mezcla
                bool mezcla = rs[RsMezcla] != 0;
                if (d.MezclaPuesta != (mezcla ? 1 : 0)) { if (mezcla) GL.Enable(GL.BLEND); else GL.Disable(GL.BLEND); d.MezclaPuesta = mezcla ? 1 : 0; }
                if (mezcla) PonMezcla(d, rs);
                // CULLMODE: el sentido como se ve en la pantalla (y hacia abajo). Sin dar la vuelta, OpenGL ve las mismas coordenadas con y hacia
                // arriba: lo que en la pantalla va con las agujas del reloj, para OpenGL va en contra (su cara de delante, GL_CCW).
                uint cm = rs[RsRecorte];
                int recorte = cm == 2 ? 1 : (cm == 3 ? 2 : 0);
                if (d.RecortePuesto != recorte)
                {
                    if (recorte == 0) GL.Disable(GlMas.CULL_FACE);
                    else { GL.Enable(GlMas.CULL_FACE); GlMas.CullFace(recorte == 1 ? GlMas.FRONT : GlMas.BACK); }
                    d.RecortePuesto = recorte;
                }
                // DITHERENABLE (apagado de fabrica en Direct3D; en OpenGL viene encendido)
                int tramado = rs[RsTramado] != 0 ? 1 : 0;
                if (d.TramadoPuesto != tramado) { if (tramado == 1) GL.Enable(GlMas.DITHER); else GL.Disable(GlMas.DITHER); d.TramadoPuesto = tramado; }
                // el programa de las etapas
                if (d.ProgramaSucio || d.Programa == null)
                {
                    ProgramaGl p = ProgramaDe(d);
                    if (p == null) return false;
                    d.Programa = p;
                    d.ProgramaSucio = false;
                }
                ProgramaGl pr = d.Programa;
                if (d.ProgramaPuesto != pr) { GL.UseProgram(pr.Nombre); d.ProgramaPuesto = pr; }
                if (pr.W != s.W || pr.H != s.H) { GL.Uniform2f(pr.LugarM, 2f / s.W, 2f / s.H); pr.W = s.W; pr.H = s.H; }
                if (pr.LugarTf >= 0 && pr.TfPuesto != rs[RsFactorTextura])
                {
                    uint c = rs[RsFactorTextura];   // D3DCOLOR, en el orden de la memoria: B, G, R, A
                    GlMas.Uniform4f(pr.LugarTf, (c & 255) / 255f, ((c >> 8) & 255) / 255f, ((c >> 16) & 255) / 255f, (c >> 24) / 255f);
                    pr.TfPuesto = c;
                }
                if (pr.LugarAr >= 0 && pr.ArPuesto != (rs[RsAlfaRef] & 0xFF)) { GL.Uniform1f(pr.LugarAr, (rs[RsAlfaRef] & 0xFF) / 255f); pr.ArPuesto = rs[RsAlfaRef] & 0xFF; }
                // las texturas de las etapas que muestrean, con el muestreo de su etapa
                for (int i = 0; i < EtapasGl; i++)
                {
                    if (!pr.Muestrea[i]) continue;
                    TexturaGl t = ObjetoDe<TexturaGl>(d.Etapas[i].Textura);
                    uint n = t != null ? t.Nombre : d.TexturaNegra;
                    GL.ActiveTexture(GL.TEXTURE0 + (uint)i);
                    if (d.TexturaPuesta[i] != n) { GL.BindTexture(GL.TEXTURE_2D, n); d.TexturaPuesta[i] = n; }
                    if (t != null) Muestreo(t, ref d.Etapas[i]);
                }
                GL.ActiveTexture(GL.TEXTURE0);
                return true;
            }
            // La mezcla de Direct3D 9: los factores de SRCBLEND y DESTBLEND (BOTHSRCALPHA y BOTHINVSRCALPHA ponen los dos), BLENDOP, y si
            // SEPARATEALPHABLENDENABLE, los del alfa aparte; si no, el alfa se mezcla con los mismos.
            private static void PonMezcla(DispositivoGl d, uint[] rs)
            {
                uint fs = rs[RsFuente], fd = rs[RsDestino];
                Ambos(ref fs, ref fd);
                uint fsa = fs, fda = fd, op = rs[RsOperacion], opa = op;
                if (rs[RsAlfaAparte] != 0) { fsa = rs[RsFuenteAlfa]; fda = rs[RsDestinoAlfa]; opa = rs[RsOperacionAlfa]; Ambos(ref fsa, ref fda); }
                uint gfs = Factor(fs, true), gfd = Factor(fd, false), gfsa = Factor(fsa, true), gfda = Factor(fda, false);
                if (d.FuentePuesta != gfs || d.DestinoPuesto != gfd || d.FuenteAlfaPuesta != gfsa || d.DestinoAlfaPuesto != gfda)
                {
                    GlMas.BlendFuncSeparate(gfs, gfd, gfsa, gfda);
                    d.FuentePuesta = gfs; d.DestinoPuesto = gfd; d.FuenteAlfaPuesta = gfsa; d.DestinoAlfaPuesto = gfda;
                }
                uint ge = Ecuacion(op), gea = Ecuacion(opa);
                if (d.EcuacionPuesta != ge || d.EcuacionAlfaPuesta != gea) { GlMas.BlendEquationSeparate(ge, gea); d.EcuacionPuesta = ge; d.EcuacionAlfaPuesta = gea; }
                bool constante = fs == 14 || fs == 15 || fd == 14 || fd == 15 || fsa == 14 || fsa == 15 || fda == 14 || fda == 15;
                if (constante && d.FactorPuesto != rs[RsFactorMezcla])
                {
                    uint c = rs[RsFactorMezcla];   // en el orden de la memoria
                    GlMas.BlendColor((c & 255) / 255f, ((c >> 8) & 255) / 255f, ((c >> 16) & 255) / 255f, (c >> 24) / 255f);
                    d.FactorPuesto = c;
                }
            }
            private static void Ambos(ref uint fuente, ref uint destino)
            {
                if (fuente == 12) { fuente = 5; destino = 6; }        // D3DBLEND_BOTHSRCALPHA: SRCALPHA y INVSRCALPHA
                else if (fuente == 13) { fuente = 6; destino = 5; }   // D3DBLEND_BOTHINVSRCALPHA: al reves
            }
            private static uint Factor(uint b, bool fuente)
            {
                switch (b)
                {
                    case 1: return GlMas.ZERO; case 2: return GL.ONE; case 3: return GlMas.SRC_COLOR; case 4: return GlMas.ONE_MINUS_SRC_COLOR;
                    case 5: return GL.SRC_ALPHA; case 6: return GL.ONE_MINUS_SRC_ALPHA; case 7: return GlMas.DST_ALPHA; case 8: return GlMas.ONE_MINUS_DST_ALPHA;
                    case 9: return GlMas.DST_COLOR; case 10: return GlMas.ONE_MINUS_DST_COLOR;
                    case 11: return fuente ? GlMas.SRC_ALPHA_SATURATE : GL.ONE;   // (de destino, OpenGL ES no lo tiene; ValidateDevice lo dice)
                    case 14: return GlMas.CONSTANT_COLOR; case 15: return GlMas.ONE_MINUS_CONSTANT_COLOR;   // BLENDFACTOR, INVBLENDFACTOR
                    default: return GL.ONE;
                }
            }
            private static uint Ecuacion(uint op)
            {
                switch (op)
                {
                    case 2: return GlMas.FUNC_SUBTRACT;            // D3DBLENDOP_SUBTRACT: origen - destino
                    case 3: return GlMas.FUNC_REVERSE_SUBTRACT;    // REVSUBTRACT: destino - origen
                    case 4: return mezclaMinMax ? GlMas.MIN : GlMas.FUNC_ADD;
                    case 5: return mezclaMinMax ? GlMas.MAX : GlMas.FUNC_ADD;
                    default: return GlMas.FUNC_ADD;
                }
            }
            private static void Muestreo(TexturaGl t, ref Etapa e)
            {
                uint s = Direccion(e.DireccionU, t), tt = Direccion(e.DireccionV, t);
                uint mn = e.FiltroMin <= 1 ? GlMas.NEAREST : GL.LINEAR, mg = e.FiltroMag <= 1 ? GlMas.NEAREST : GL.LINEAR;   // POINT; LINEAR (y ANISOTROPIC con un nivel)
                if (t.PuestoS != s) { GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)s); t.PuestoS = s; }
                if (t.PuestoT != tt) { GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)tt); t.PuestoT = tt; }
                if (t.PuestoMin != mn) { GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)mn); t.PuestoMin = mn; }
                if (t.PuestoMag != mg) { GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)mg); t.PuestoMag = mg; }
                if ((s == GlMas.CLAMP_TO_BORDER || tt == GlMas.CLAMP_TO_BORDER) && t.PuestoBorde != e.Borde)
                {
                    uint c = e.Borde;   // D3DCOLOR, en el orden de la memoria
                    float* v = stackalloc float[4];
                    v[0] = (c & 255) / 255f; v[1] = ((c >> 8) & 255) / 255f; v[2] = ((c >> 16) & 255) / 255f; v[3] = t.SinAlfa ? 1f : (c >> 24) / 255f;
                    GlMas.TexParameterfv(GL.TEXTURE_2D, GlMas.TEXTURE_BORDER_COLOR, v);
                    t.PuestoBorde = c;
                }
            }
            private static uint Direccion(uint d, TexturaGl t)
            {
                switch (d)
                {
                    case 1: return t.Npot && !npotCompleto ? GL.CLAMP_TO_EDGE : GlMas.REPEAT;             // WRAP
                    case 2: return t.Npot && !npotCompleto ? GL.CLAMP_TO_EDGE : GlMas.MIRRORED_REPEAT;    // MIRROR
                    case 4: return conBorde ? GlMas.CLAMP_TO_BORDER : GL.CLAMP_TO_EDGE;                   // BORDER
                    case 5: return conEspejoUnaVez ? GlMas.MIRROR_CLAMP_TO_EDGE : GL.CLAMP_TO_EDGE;       // MIRRORONCE
                    default: return GL.CLAMP_TO_EDGE;                                                      // CLAMP
                }
            }
            // Los vertices (el FVF entero, en su orden): la posicion (solo XYZRHW: lo demas pide las matrices de Direct3D, que XP no usa), la
            // normal y el tamano de punto (se saltan), el color difuso (sin el, blanco), el especular (sin el, negro) y los juegos de
            // coordenadas, cada uno con su medida (D3DFVF_TEXCOORDSIZEn: 2, 3, 4 o 1 numeros). Cada etapa toma el juego de su TEXCOORDINDEX
            // (si no esta en el FVF, (0, 0)).
            private static bool Atributos(DispositivoGl d, byte* base0, int paso)
            {
                uint fvf = d.Fvf;
                if ((fvf & 0x400E) != 0x004) return false;
                GL.VertexAttribPointer(0, 4, GL.FLOAT, 0, paso, (IntPtr)base0); GL.EnableVertexAttribArray(0);
                int o = 16;
                if ((fvf & 0x010) != 0) o += 12;   // D3DFVF_NORMAL
                if ((fvf & 0x020) != 0) o += 4;    // D3DFVF_PSIZE
                if ((fvf & 0x040) != 0) { GL.VertexAttribPointer(1, 4, GL.UNSIGNED_BYTE, 1, paso, (IntPtr)(base0 + o)); GL.EnableVertexAttribArray(1); o += 4; }
                else { GlMas.DisableVertexAttribArray(1); GlMas.VertexAttrib4f(1, 1, 1, 1, 1); }
                int especular = -1;
                if ((fvf & 0x080) != 0) { especular = o; o += 4; }
                if (d.Programa.UsaEspecular)
                {
                    if (especular >= 0) { GL.VertexAttribPointer(4, 4, GL.UNSIGNED_BYTE, 1, paso, (IntPtr)(base0 + especular)); GL.EnableVertexAttribArray(4); }
                    else { GlMas.DisableVertexAttribArray(4); GlMas.VertexAttrib4f(4, 0, 0, 0, 0); }
                    d.EspecularPuesto = especular >= 0;
                }
                else if (d.EspecularPuesto) { GlMas.DisableVertexAttribArray(4); d.EspecularPuesto = false; }
                int juegos = (int)((fvf >> 8) & 0xF);
                if (juegos > 8) juegos = 8;
                int* desp = stackalloc int[8], medida = stackalloc int[8];
                for (int k = 0; k < juegos; k++)
                {
                    uint f = (fvf >> (16 + 2 * k)) & 3;
                    medida[k] = f == 0 ? 2 : (f == 1 ? 3 : (f == 2 ? 4 : 1));
                    desp[k] = o; o += 4 * medida[k];
                }
                for (int i = 0; i < EtapasGl; i++)
                {
                    uint a = (uint)(2 + i);
                    int k = (int)(d.Etapas[i].IndiceCoordenadas & 0xFFFF);
                    if (k < juegos) { GL.VertexAttribPointer(a, Math.Min(medida[k], 2), GL.FLOAT, 0, paso, (IntPtr)(base0 + desp[k])); GL.EnableVertexAttribArray(a); }
                    else { GlMas.DisableVertexAttribArray(a); GlMas.VertexAttrib4f(a, 0, 0, 0, 1); }
                }
                return true;
            }

            // ---- los programas: uno por combinacion de etapas (y de la prueba de alfa y el especular) ----
            // La clave, sin texto (no deja basura en cada cambio de modo): por etapa, sus operaciones y argumentos y si tiene textura.
            private struct Clave : IEquatable<Clave>
            {
                internal ulong A, B, C;
                public bool Equals(Clave o) { return A == o.A && B == o.B && C == o.C; }
                public override bool Equals(object o) { return o is Clave && Equals((Clave)o); }
                public override int GetHashCode() { ulong h = A * 0x9E3779B97F4A7C15UL ^ B * 0xC2B2AE3D27D4EB4FUL ^ C * 0x165667B19E3779F9UL; return (int)(h ^ (h >> 32)); }
            }
            private static ulong ClaveEtapa(Etapa e, bool conTextura)
            {
                // operaciones en 6 bits, argumentos en 7 (el seleccionador y los dos modificadores)
                ulong k = (e.ColorOp & 63) | ((ulong)(e.ColorArg0 & 127) << 6) | ((ulong)(e.ColorArg1 & 127) << 13) | ((ulong)(e.ColorArg2 & 127) << 20)
                    | ((ulong)(e.AlphaOp & 63) << 27) | ((ulong)(e.AlphaArg0 & 127) << 33) | ((ulong)(e.AlphaArg1 & 127) << 40) | ((ulong)(e.AlphaArg2 & 127) << 47)
                    | ((ulong)((e.Resultado & 0xF) == 5 ? 1 : 0) << 54) | ((ulong)(conTextura ? 1 : 0) << 55) | (1UL << 56);
                return k;
            }
            private static Clave ClaveDe(DispositivoGl d)
            {
                Clave c = new Clave();
                for (int i = 0; i < EtapasGl; i++)
                {
                    Etapa e = d.Etapas[i];
                    if (e.ColorOp == TOP_DISABLE) break;
                    ulong k = ClaveEtapa(e, e.Textura != IntPtr.Zero);
                    if (i == 0) c.A = k; else c.B = k;
                }
                uint[] rs = d.Rs;
                c.C = (rs[RsAlfaPrueba] != 0 ? (rs[RsAlfaFuncion] & 0xF) : 8) | ((ulong)(rs[RsEspecular] != 0 ? 1 : 0) << 4);
                return c;
            }
            private static readonly Dictionary<Clave, ProgramaGl> programas = new Dictionary<Clave, ProgramaGl>();
            private const string FuenteVertices =
                "attribute vec4 p; attribute vec4 c; attribute vec4 e; attribute vec2 t0; attribute vec2 t1;\n" +
                "uniform vec2 m;\n" +
                "varying vec4 vc; varying vec4 ve; varying vec2 vt0; varying vec2 vt1;\n" +
                "void main() {\n" +
                "  float w = 1.0 / p.w;\n" +   // XYZRHW: rhw = 1/w; OpenGL interpola dividiendo por w, como Direct3D
                "  gl_Position = vec4(((p.xy + 0.5) * m - 1.0) * w, 0.0, w);\n" +
                "  vc = c; ve = e; vt0 = t0; vt1 = t1;\n" +
                "}\n";
            private static ProgramaGl ProgramaDe(DispositivoGl d)
            {
                Clave k = ClaveDe(d);
                ProgramaGl p;
                if (programas.TryGetValue(k, out p)) return p;
                bool[] muestrea = new bool[EtapasGl];
                bool usaTf, usaEsp, usaAr;
                string fuente = FuenteEtapas(d, muestrea, out usaTf, out usaEsp, out usaAr);
                p = Compila(fuente);
                if (p == null) return null;
                p.Muestrea = muestrea; p.UsaEspecular = usaEsp;
                if (!usaTf) p.LugarTf = -1;
                if (!usaAr) p.LugarAr = -1;
                programas[k] = p;
                return p;
            }
            // Una etapa que pide la textura (como argumento que su operacion de verdad mira; SELECTARG1 no mira el segundo ni SELECTARG2 el
            // primero, y el argumento 0 solo lo miran MULTIPLYADD y LERP).
            private static bool PideTextura(uint op, uint a0, uint a1, uint a2)
            {
                if (op == TOP_DISABLE) return false;
                if ((a1 & 0xF) == TA_TEXTURE && op != 3) return true;
                if ((a2 & 0xF) == TA_TEXTURE && op != TOP_SELECTARG1) return true;
                if ((a0 & 0xF) == TA_TEXTURE && (op == 25 || op == 26)) return true;
                return false;
            }
            // Las etapas de Direct3D 9 en GLSL: cada etapa toma sus argumentos (el color del vertice, el especular, lo de la etapa anterior,
            // el temporal, su textura o TFACTOR, con «1 - x» y «el alfa en todos» si se piden), hace su operacion con el color y con el alfa
            // por separado y lo deja entre 0 y 1, en CURRENT o en TEMP (RESULTARG). La primera etapa con COLOROP = DISABLE acaba (y las de
            // despues no cuentan); ALPHAOP = DISABLE con el color encendido deja pasar el alfa de antes. Una etapa SIN TEXTURA cuya operacion
            // pide D3DTA_TEXTURE pasa lo que le llega (SELECTARG1 de CURRENT), por separado en el color y en el alfa: lo que hace Direct3D 9
            // (con los estados de fabrica y sin textura sale el color del vertice). Despues, el especular si SPECULARENABLE y la prueba de alfa.
            private static string FuenteEtapas(DispositivoGl d, bool[] muestrea, out bool usaTf, out bool usaEsp, out bool usaAr)
            {
                usaTf = usaEsp = usaAr = false;
                StringBuilder sb = new StringBuilder(1024);
                sb.Append("#ifdef GL_FRAGMENT_PRECISION_HIGH\nprecision highp float;\n#else\nprecision mediump float;\n#endif\n");
                sb.Append("varying vec4 vc; varying vec4 ve; varying vec2 vt0; varying vec2 vt1;\nuniform sampler2D s0; uniform sampler2D s1; uniform vec4 tf; uniform float ar;\n");
                sb.Append("void main() {\n  vec4 cur = vc; vec4 tmp = vec4(0.0);\n");
                for (int i = 0; i < EtapasGl; i++)
                {
                    Etapa st = d.Etapas[i];
                    if (st.ColorOp == TOP_DISABLE) break;
                    bool conTextura = st.Textura != IntPtr.Zero;
                    uint cop = st.ColorOp, c0 = st.ColorArg0, c1 = st.ColorArg1, c2 = st.ColorArg2;
                    uint aop = st.AlphaOp, a0 = st.AlphaArg0, a1 = st.AlphaArg1, a2 = st.AlphaArg2;
                    if (!OpHecha(cop, true)) cop = TOP_SELECTARG1;                          // (lo que no se hace: ValidateDevice lo dice)
                    if (aop == TOP_DISABLE || !OpHecha(aop, false)) { aop = TOP_SELECTARG1; a1 = TA_CURRENT; }
                    if (!conTextura && PideTextura(cop, c0, c1, c2)) { cop = TOP_SELECTARG1; c1 = TA_CURRENT; }
                    if (!conTextura && PideTextura(aop, a0, a1, a2)) { aop = TOP_SELECTARG1; a1 = TA_CURRENT; }
                    string tex = "tx" + i;
                    if (PideTextura(cop, c0, c1, c2) || PideTextura(aop, a0, a1, a2) || cop == 13 || cop == 15 || aop == 13 || aop == 15)
                    {
                        muestrea[i] = true;
                        sb.Append("  vec4 ").Append(tex).Append(" = texture2D(s").Append(i).Append(", vt").Append(i).Append(");\n");
                    }
                    string x = Arg(c1, tex, ref usaTf, ref usaEsp), y = Arg(c2, tex, ref usaTf, ref usaEsp), z = Arg(c0, tex, ref usaTf, ref usaEsp);
                    string ax = Arg(a1, tex, ref usaTf, ref usaEsp), ay = Arg(a2, tex, ref usaTf, ref usaEsp), az = Arg(a0, tex, ref usaTf, ref usaEsp);
                    if (cop == 14 || aop == 14) usaTf = true;
                    string r = "r" + i;
                    if (cop == 24)   // DOTPRODUCT3: el mismo numero en los cuatro canales (tambien el alfa)
                        sb.Append("  float d").Append(i).Append(" = 4.0 * dot((").Append(x).Append(").rgb - 0.5, (").Append(y).Append(").rgb - 0.5);\n")
                          .Append("  vec4 ").Append(r).Append(" = clamp(vec4(d").Append(i).Append("), 0.0, 1.0);\n");
                    else
                        sb.Append("  vec4 ").Append(r).Append(" = clamp(vec4(").Append(Op(cop, z, x, y, ".rgb", tex)).Append(", ").Append(Op(aop, az, ax, ay, ".a", tex)).Append("), 0.0, 1.0);\n");
                    sb.Append("  ").Append((st.Resultado & 0xF) == 5 ? "tmp" : "cur").Append(" = ").Append(r).Append(";\n");
                }
                uint[] rs = d.Rs;
                if (rs[RsEspecular] != 0) { usaEsp = true; sb.Append("  cur.rgb = clamp(cur.rgb + ve.rgb, 0.0, 1.0);\n"); }
                uint fa = rs[RsAlfaPrueba] != 0 ? rs[RsAlfaFuncion] : 8;
                if (fa == 1) sb.Append("  discard;\n");                                   // NEVER
                else if (fa >= 2 && fa <= 7)                                               // la prueba de alfa: pasa si el alfa <op> ALPHAREF
                {
                    string[] cmp = { "", "", "<", "==", "<=", ">", "!=", ">=" };
                    sb.Append("  if (!(cur.a ").Append(cmp[fa]).Append(" ar)) discard;\n");
                    usaAr = true;
                }
                sb.Append("  gl_FragColor = cur;\n}\n");
                return sb.ToString();
            }
            private static string Arg(uint a, string tex, ref bool usaTf, ref bool usaEsp)
            {
                string b;
                switch (a & 0xF)
                {
                    case TA_DIFFUSE: b = "vc"; break;
                    case TA_CURRENT: b = "cur"; break;   // (en la etapa 0, «cur» es el color del vertice)
                    case TA_TEXTURE: b = tex; break;
                    case 3: b = "tf"; usaTf = true; break;     // D3DTA_TFACTOR (D3DRS_TEXTUREFACTOR)
                    case 4: b = "ve"; usaEsp = true; break;    // D3DTA_SPECULAR
                    case 5: b = "tmp"; break;                  // D3DTA_TEMP (0 hasta que una etapa con RESULTARG = TEMP lo escribe)
                    default: b = "vec4(0.0)"; break;           // D3DTA_CONSTANT: no (ValidateDevice lo dice)
                }
                if ((a & TA_COMPLEMENT) != 0) b = "(1.0 - " + b + ")";
                if ((a & TA_ALPHAREPLICATE) != 0) b = "vec4((" + b + ").a)";
                return b;
            }
            // una operacion en el color (sel = ".rgb") o en el alfa (".a"); x = argumento 1, y = argumento 2, z = argumento 0
            private static string Op(uint op, string z, string x, string y, string sel, string tex)
            {
                string X = "(" + x + ")" + sel, Y = "(" + y + ")" + sel, Z = "(" + z + ")" + sel;
                switch (op)
                {
                    case TOP_SELECTARG1: return X;
                    case 3: return Y;                                            // SELECTARG2
                    case TOP_MODULATE: return X + " * " + Y;
                    case 5: return "2.0 * " + X + " * " + Y;                     // MODULATE2X
                    case 6: return "4.0 * " + X + " * " + Y;                     // MODULATE4X
                    case 7: return X + " + " + Y;                                // ADD
                    case 8: return X + " + " + Y + " - 0.5";                     // ADDSIGNED
                    case 9: return "2.0 * (" + X + " + " + Y + " - 0.5)";        // ADDSIGNED2X
                    case 10: return X + " - " + Y;                               // SUBTRACT
                    case TOP_ADDSMOOTH: return X + " + " + Y + " - " + X + " * " + Y;
                    case 12: return "mix(" + Y + ", " + X + ", vc.a)";           // BLENDDIFFUSEALPHA
                    case 13: return "mix(" + Y + ", " + X + ", " + tex + ".a)";  // BLENDTEXTUREALPHA
                    case 14: return "mix(" + Y + ", " + X + ", tf.a)";           // BLENDFACTORALPHA
                    case 15: return X + " + " + Y + " * (1.0 - " + tex + ".a)";  // BLENDTEXTUREALPHAPM
                    case 16: return "mix(" + Y + ", " + X + ", cur.a)";          // BLENDCURRENTALPHA
                    case 18: return X + " + (" + x + ").a * " + Y;               // MODULATEALPHA_ADDCOLOR (solo de color)
                    case 19: return X + " * " + Y + " + (" + x + ").a";          // MODULATECOLOR_ADDALPHA
                    case 20: return "(1.0 - (" + x + ").a) * " + Y + " + " + X;  // MODULATEINVALPHA_ADDCOLOR
                    case 21: return "(1.0 - " + X + ") * " + Y + " + (" + x + ").a";   // MODULATEINVCOLOR_ADDALPHA
                    case 24: return "vec3(4.0 * dot((" + x + ").rgb - 0.5, (" + y + ").rgb - 0.5))" + (sel == ".a" ? ".x" : "");   // DOTPRODUCT3 (de alfa)
                    case 25: return Z + " + " + X + " * " + Y;                   // MULTIPLYADD
                    case 26: return "mix(" + Y + ", " + X + ", " + Z + ")";      // LERP
                    default: return X;
                }
            }
            private static uint sombreadorVertices;
            private static ProgramaGl Compila(string fuenteFragmentos)
            {
                if (sombreadorVertices == 0) sombreadorVertices = Sombreador(GL.VERTEX_SHADER, FuenteVertices);
                uint f = Sombreador(GL.FRAGMENT_SHADER, fuenteFragmentos);
                if (sombreadorVertices == 0 || f == 0) return null;
                uint p = GL.CreateProgram();
                GL.AttachShader(p, sombreadorVertices); GL.AttachShader(p, f);
                GL.BindAttribLocation(p, 0, "p"); GL.BindAttribLocation(p, 1, "c"); GL.BindAttribLocation(p, 2, "t0"); GL.BindAttribLocation(p, 3, "t1"); GL.BindAttribLocation(p, 4, "e");
                GL.LinkProgram(p);
                int ok; GL.GetProgramiv(p, GL.LINK_STATUS, &ok);
                GlMas.DeleteShader(f);
                if (ok == 0)
                {
                    byte* log = stackalloc byte[1024]; int largo;
                    GlMas.GetProgramInfoLog(p, 1024, &largo, log);
                    Console.Error.WriteLine("Direct3DaGL: el programa no enlaza: " + Marshal.PtrToStringAnsi((IntPtr)log, Math.Max(0, largo)));
                    return null;
                }
                ProgramaGl r = new ProgramaGl();
                r.Nombre = p;
                GL.UseProgram(p);
                r.LugarM = GL.GetUniformLocation(p, "m"); r.LugarTf = GL.GetUniformLocation(p, "tf"); r.LugarAr = GL.GetUniformLocation(p, "ar");
                GL.Uniform1i(GL.GetUniformLocation(p, "s0"), 0); GL.Uniform1i(GL.GetUniformLocation(p, "s1"), 1);
                foreach (DispositivoGl d in dispositivos) d.ProgramaPuesto = null;   // (el programa en uso cambio)
                return r;
            }
            private static uint Sombreador(uint tipo, string fuente)
            {
                uint s = GL.CreateShader(tipo);
                byte[] b = Encoding.ASCII.GetBytes(fuente);
                fixed (byte* pb = b)
                {
                    byte* f = pb; int largo = b.Length;
                    GL.ShaderSource(s, 1, &f, &largo);
                }
                GL.CompileShader(s);
                int ok; GL.GetShaderiv(s, GL.COMPILE_STATUS, &ok);
                if (ok == 0)
                {
                    byte* log = stackalloc byte[1024]; int largo;
                    GL.GetShaderInfoLog(s, 1024, &largo, log);
                    Console.Error.WriteLine("Direct3DaGL: el sombreador no compila: " + Marshal.PtrToStringAnsi((IntPtr)log, Math.Max(0, largo)) + "\n" + fuente);
                    return 0;
                }
                return s;
            }

            // Para las pruebas y el diagnostico: lo pintado en el fondo, en el orden de Direct3D (B, G, R, A por pixel, fila 0 arriba).
            internal static byte[] LeeFondo(IntPtr self, out int w, out int h)
            {
                w = h = 0;
                DispositivoGl d = Disp(self);
                if (d == null || d.Fondo == null || d.Fondo.Textura == null) return null;
                SuperficieGl s = d.Fondo;
                if (s.Fbo == 0) return null;
                w = s.W; h = s.H;
                byte[] px = new byte[w * h * 4];
                GL.BindFramebuffer(GL.FRAMEBUFFER, s.Fbo);
                GL.PixelStorei(GL.PACK_ALIGNMENT, 4);
                fixed (byte* p = px) GL.ReadPixels(0, 0, w, h, GL.RGBA, GL.UNSIGNED_BYTE, p);
                EstadoTocado();
                return px;
            }
        }
    }
}
