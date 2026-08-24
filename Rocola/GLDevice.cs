// ====================================================================================================
//  EL DISPOSITIVO DE LINUX: OpenGL de compatibilidad, colgado por GLX de la ventana de la rocola.
// ----------------------------------------------------------------------------------------------------
//  Es el gemelo de Rocola/D3D9Device.cs. Todo el dibujo de verdad -el cielo, las estrellas, la aurora,
//  los cubos, el buscador- esta en Rocola/D3D9Star.cs y no sabe de tarjetas: solo pide "pintame estos
//  triangulos con este color y esta textura". Aqui se traduce esa peticion a OpenGL.
//
//  POR QUE SE TRADUCE EN VEZ DE REESCRIBIRSE. El dibujado de la rocola no usa programas de sombreado:
//  son vertices ya puestos en pixeles de la pantalla, cada uno con su color y su textura. Esa es la
//  forma mas vieja de dibujar que hay, y existe igual en Direct3D 9 y en OpenGL, asi que casi todo sale
//  linea por linea:
//
//      Direct3D 9                                   OpenGL
//      ------------------------------------------   -------------------------------------------------
//      CreateDevice(ventana)                        glXCreateContext + glXMakeCurrent
//      Clear(color)                                 glClearColor + glClear
//      BeginScene / EndScene                        (no hacen falta)
//      Present()                                    glXSwapBuffers
//      SetRenderState(ZENABLE, 0)                   glDisable(GL_DEPTH_TEST)
//      SetRenderState(LIGHTING, 0)                  glDisable(GL_LIGHTING)
//      SetRenderState(CULLMODE, NONE)               glDisable(GL_CULL_FACE)
//      SetRenderState(ALPHABLENDENABLE, 1)          glEnable(GL_BLEND)
//      SRCBLEND=SRCALPHA, DESTBLEND=ONE             glBlendFunc(GL_SRC_ALPHA, GL_ONE)          (sumar luz)
//      SRCBLEND=SRCALPHA, DESTBLEND=INVSRCALPHA     glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA)
//      COLOROP = SELECTARG1(DIFFUSE)                glDisable(GL_TEXTURE_2D)
//      COLOROP = MODULATE(TEXTURE, DIFFUSE)         glEnable(GL_TEXTURE_2D) + GL_MODULATE
//      SetFVF(XYZRHW|DIFFUSE|TEX1) + DrawPrimitiveUP  glVertexPointer/glColorPointer/glTexCoordPointer
//                                                     + glDrawArrays(GL_TRIANGLES)
//      CreateTexture + LockRect + copiar             glGenTextures + glTexImage2D
//      dispositivo perdido (Alt+Tab, bloqueo)        no existe en OpenGL: nunca se pierde
//
//  Y una cosa que NO es traduccion directa, el medio pixel: en Direct3D 9 un vertice puesto en (x,y)
//  cae medio pixel corrido respecto a donde OpenGL lo pondria. Por eso el dibujado resta 0.5 a las
//  cosas que tienen que quedar clavadas al pixel (las caras de los cubos, la capa de la interfaz). Si
//  aqui se dibujara sin mas, esas cosas saldrian medio pixel arriba y a la izquierda, y la textura
//  quedaria medio texel corrida: se ve como un desenfoque leve. Se arregla de una vez y para siempre
//  corriendo la CAMARA medio pixel, en la proyeccion, en vez de tocar ni un vertice del dibujado.
//
//  Todo dentro de #if ROCOLA_GPU. Con el simbolo apagado, el archivo compila vacio.
// ====================================================================================================
#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Rocola
{
    // -------- Interop crudo con X11, GLX y OpenGL --------
    internal static partial class D3D9
    {
        [DllImport("libX11.so.6")] public static extern int XDefaultScreen(IntPtr dpy);
        [DllImport("libX11.so.6")] public static extern int XSync(IntPtr dpy, bool descartar);

        [DllImport("libGL.so.1")] public static extern IntPtr glXChooseVisual(IntPtr dpy, int pantalla, int[] att);
        [DllImport("libGL.so.1")] public static extern IntPtr glXCreateContext(IntPtr dpy, IntPtr vis, IntPtr comparte, bool directo);
        [DllImport("libGL.so.1")] public static extern void glXDestroyContext(IntPtr dpy, IntPtr ctx);
        [DllImport("libGL.so.1")] public static extern bool glXMakeCurrent(IntPtr dpy, IntPtr ventana, IntPtr ctx);
        [DllImport("libGL.so.1")] public static extern IntPtr glXGetCurrentContext();
        [DllImport("libGL.so.1")] public static extern void glXSwapBuffers(IntPtr dpy, IntPtr ventana);
        [DllImport("libGL.so.1")] public static extern IntPtr glGetString(uint nombre);

        [DllImport("libGL.so.1")] public static extern void glClearColor(float r, float g, float b, float a);
        [DllImport("libGL.so.1")] public static extern void glClear(uint mask);
        [DllImport("libGL.so.1")] public static extern void glViewport(int x, int y, int w, int h);
        [DllImport("libGL.so.1")] public static extern void glMatrixMode(uint modo);
        [DllImport("libGL.so.1")] public static extern void glLoadIdentity();
        [DllImport("libGL.so.1")] public static extern void glOrtho(double i, double d, double ab, double ar, double c, double l);
        [DllImport("libGL.so.1")] public static extern void glEnable(uint cap);
        [DllImport("libGL.so.1")] public static extern void glDisable(uint cap);
        [DllImport("libGL.so.1")] public static extern void glBlendFunc(uint src, uint dst);
        [DllImport("libGL.so.1")] public static extern void glTexEnvi(uint objetivo, uint nombre, int valor);
        [DllImport("libGL.so.1")] public static extern void glBindTexture(uint objetivo, uint tex);
        [DllImport("libGL.so.1")] public static extern void glGenTextures(int n, uint[] tex);
        [DllImport("libGL.so.1")] public static extern void glDeleteTextures(int n, uint[] tex);
        [DllImport("libGL.so.1")] public static extern void glTexParameteri(uint objetivo, uint nombre, int valor);
        [DllImport("libGL.so.1")] public static extern void glPixelStorei(uint nombre, int valor);
        [DllImport("libGL.so.1")] public static extern void glTexImage2D(uint objetivo, int nivel, int interno, int w, int h, int borde, uint fmt, uint tipo, IntPtr px);
        [DllImport("libGL.so.1")] public static extern void glTexSubImage2D(uint objetivo, int nivel, int x, int y, int w, int h, uint fmt, uint tipo, IntPtr px);
        [DllImport("libGL.so.1")] public static extern void glEnableClientState(uint arreglo);
        [DllImport("libGL.so.1")] public static extern void glDisableClientState(uint arreglo);
        [DllImport("libGL.so.1")] public static extern void glVertexPointer(int tam, uint tipo, int paso, IntPtr p);
        [DllImport("libGL.so.1")] public static extern void glColorPointer(int tam, uint tipo, int paso, IntPtr p);
        [DllImport("libGL.so.1")] public static extern void glTexCoordPointer(int tam, uint tipo, int paso, IntPtr p);
        [DllImport("libGL.so.1")] public static extern void glDrawArrays(uint modo, int primero, int cuantos);
        [DllImport("libGL.so.1")] public static extern uint glGetError();
        [DllImport("libGL.so.1")] public static extern void glFinish();

        public const int GLX_RGBA = 4, GLX_DOUBLEBUFFER = 5, GLX_RED_SIZE = 8, GLX_GREEN_SIZE = 9,
                         GLX_BLUE_SIZE = 10, GLX_ALPHA_SIZE = 11, GLX_DEPTH_SIZE = 12;

        public const uint GL_COLOR_BUFFER_BIT = 0x4000;
        public const uint GL_RENDERER = 0x1F01, GL_VERSION = 0x1F02, GL_VENDOR = 0x1F00;
        public const uint GL_PROJECTION = 0x1701, GL_MODELVIEW = 0x1700;
        public const uint GL_TRIANGLES = 0x0004;
        public const uint GL_BLEND = 0x0BE2, GL_DEPTH_TEST = 0x0B71, GL_LIGHTING = 0x0B50, GL_CULL_FACE = 0x0B44;
        public const uint GL_SRC_ALPHA = 0x0302, GL_ONE = 1, GL_ONE_MINUS_SRC_ALPHA = 0x0303;
        public const uint GL_TEXTURE_2D = 0x0DE1, GL_TEXTURE_ENV = 0x2300, GL_TEXTURE_ENV_MODE = 0x2200;
        public const uint GL_MODULATE = 0x2100, GL_REPLACE = 0x1E01;
        public const uint GL_TEXTURE_MAG_FILTER = 0x2800, GL_TEXTURE_MIN_FILTER = 0x2801;
        public const uint GL_TEXTURE_WRAP_S = 0x2802, GL_TEXTURE_WRAP_T = 0x2803;
        public const uint GL_LINEAR = 0x2601, GL_CLAMP_TO_EDGE = 0x812F;
        public const uint GL_RGBA = 0x1908, GL_BGRA = 0x80E1, GL_UNSIGNED_BYTE = 0x1401, GL_FLOAT = 0x1406;
        public const uint GL_UNPACK_ALIGNMENT = 0x0CF5, GL_UNPACK_ROW_LENGTH = 0x0CF2;
        public const uint GL_VERTEX_ARRAY = 0x8074, GL_COLOR_ARRAY = 0x8076, GL_TEXTURE_COORD_ARRAY = 0x8078;
        public const uint GL_NO_ERROR = 0;

        // SOLTAR UNA TEXTURA. En Direct3D las texturas son objetos COM y esto era soltar una referencia;
        // en OpenGL son numeros y esto es borrarlas. El archivo del dibujo llama a lo mismo en los dos.
        // (El numero 0 en OpenGL significa "ninguna textura" y nunca sale de glGenTextures, asi que
        // IntPtr.Zero sigue queriendo decir lo mismo que antes: aqui no hay textura.)
        private static readonly uint[] unaTex = new uint[1];
        public static void Release(IntPtr tex)
        {
            if (tex == IntPtr.Zero) return;
            try { unaTex[0] = (uint)tex.ToInt64(); glDeleteTextures(1, unaTex); } catch { }
        }

        public static string Texto(uint n) { try { IntPtr p = glGetString(n); return (p == IntPtr.Zero) ? "" : Marshal.PtrToStringAnsi(p); } catch { return ""; } }

        // LA VENTANA DE X11 QUE HAY DEBAJO DE UN CONTROL DE LA ROCOLA.
        //
        // OpenGL no se cuelga de un control, se cuelga de una ventana del sistema de ventanas. Mono no
        // cuenta por ninguna via oficial cual es la ventana de X11 de un control, pero por dentro guarda
        // esa correspondencia y se le puede preguntar. Mono ya no cambia -esta congelado-, asi que esto
        // no se va a mover bajo los pies. Es lo mismo que comprueba la bateria 'opengl'.
        public static bool VentanaX11(IntPtr handle, out IntPtr dpy, out IntPtr win)
        {
            dpy = IntPtr.Zero; win = IntPtr.Zero;
            try
            {
                Assembly swf = typeof(Control).Assembly;
                Type tx = swf.GetType("System.Windows.Forms.XplatUIX11");
                if (tx == null) return false;                  // no estamos en Linux
                BindingFlags est = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                PropertyInfo p = tx.GetProperty("DisplayHandle", est);
                if (p != null) { try { dpy = (IntPtr)p.GetValue(null, null); } catch { } }
                if (dpy == IntPtr.Zero)
                {
                    FieldInfo f = tx.GetField("DisplayHandle", est);
                    if (f != null) { try { dpy = (IntPtr)f.GetValue(null); } catch { } }
                }
                Type th = swf.GetType("System.Windows.Forms.Hwnd");
                if (th == null) return false;
                MethodInfo m = th.GetMethod("ObjectFromHandle", est);
                if (m == null) return false;
                object h = null;
                try { h = m.Invoke(null, new object[] { handle }); } catch { }
                if (h == null) return false;
                foreach (string n in new string[] { "client_window", "whole_window" })
                {
                    FieldInfo f = th.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f == null) continue;
                    try { IntPtr v = (IntPtr)f.GetValue(h); if (v != IntPtr.Zero) { win = v; break; } } catch { }
                }
            }
            catch { }
            return dpy != IntPtr.Zero && win != IntPtr.Zero;
        }
    }

    // -------- Dispositivo OpenGL sobre la ventana de la rocola --------
    internal sealed class D3D9Device
    {
        private IntPtr dpy, win, ctx;
        private int anchoBuf, altoBuf;
        private bool colorBgra = true;     // la tarjeta acepta el color tal cual viene (ver Draw)
        private byte[] colorAux;           // y si no lo acepta, aqui se le da la vuelta

        public bool Ok { get { return ctx != IntPtr.Zero; } }

        public D3D9Device(IntPtr hwnd, int w, int h)
        {
            if (!D3D9.VentanaX11(hwnd, out dpy, out win)) { dpy = IntPtr.Zero; win = IntPtr.Zero; return; }
            // Sin buffer de profundidad: el dibujado es plano, todo va en el orden en que se pide.
            int[] att = { D3D9.GLX_RGBA, D3D9.GLX_DOUBLEBUFFER,
                          D3D9.GLX_RED_SIZE, 8, D3D9.GLX_GREEN_SIZE, 8, D3D9.GLX_BLUE_SIZE, 8,
                          D3D9.GLX_DEPTH_SIZE, 0, 0 };
            IntPtr vi;
            try { vi = D3D9.glXChooseVisual(dpy, D3D9.XDefaultScreen(dpy), att); }
            catch { dpy = IntPtr.Zero; win = IntPtr.Zero; return; }   // ni siquiera hay OpenGL en esta maquina
            if (vi == IntPtr.Zero) { dpy = IntPtr.Zero; win = IntPtr.Zero; return; }
            IntPtr c = D3D9.glXCreateContext(dpy, vi, IntPtr.Zero, true);   // true = directo, sin pasar por el servidor
            if (c == IntPtr.Zero || !D3D9.glXMakeCurrent(dpy, win, c))
            {
                if (c != IntPtr.Zero) D3D9.glXDestroyContext(dpy, c);
                dpy = IntPtr.Zero; win = IntPtr.Zero; return;
            }
            ctx = c;
            anchoBuf = Math.Max(1, w); altoBuf = Math.Max(1, h);
            InitStates();
            Proyeccion();
        }

        public int BufAncho { get { return anchoBuf; } }
        public int BufAlto { get { return altoBuf; } }

        // La descripcion de la tarjeta, para el diagnostico y para las pruebas.
        public string Quien { get { return Actual() ? (D3D9.Texto(D3D9.GL_RENDERER) + "  |  " + D3D9.Texto(D3D9.GL_VERSION)) : ""; } }

        // REHACE EL BUFFER DE DIBUJO al tamano nuevo de la ventana.
        //
        // En Direct3D esto era rehacer el dispositivo entero y perder los estados. En OpenGL el buffer es
        // la ventana: basta con decirle a que trozo dibuja y volver a cuadrar la camara. No se pierde nada.
        public bool Redimensiona(int w, int h)
        {
            if (ctx == IntPtr.Zero || w < 8 || h < 8) return false;
            if (anchoBuf == w && altoBuf == h) return true;
            anchoBuf = w; altoBuf = h;
            if (!Actual()) return false;
            Proyeccion();
            return true;
        }

        // LA CAMARA. Se mira la ventana de frente y en pixeles: el (0,0) arriba a la izquierda y la Y
        // creciendo hacia abajo, que es como piensa el dibujado. El medio pixel de la cabecera se mete
        // AQUI -corriendo la camara, no los vertices-, para que lo que el dibujado clava al pixel quede
        // clavado de verdad y su textura caiga texel con pixel, igual que caia en Windows.
        private void Proyeccion()
        {
            D3D9.glViewport(0, 0, anchoBuf, altoBuf);
            D3D9.glMatrixMode(D3D9.GL_PROJECTION);
            D3D9.glLoadIdentity();
            D3D9.glOrtho(-0.5, anchoBuf - 0.5, altoBuf - 0.5, -0.5, -1, 1);
            D3D9.glMatrixMode(D3D9.GL_MODELVIEW);
            D3D9.glLoadIdentity();
        }

        // Pone este dispositivo al frente del hilo que dibuja. OpenGL trabaja siempre sobre "el contexto
        // actual", asi que antes de tocar nada hay que asegurarse de que el actual es el nuestro. Si ya
        // lo es -que es lo normal, cuadro tras cuadro- no cuesta nada.
        private bool Actual()
        {
            if (ctx == IntPtr.Zero) return false;
            try
            {
                if (D3D9.glXGetCurrentContext() == ctx) return true;
                return D3D9.glXMakeCurrent(dpy, win, ctx);
            }
            catch { return false; }
        }

        // ESTADO ACTUAL. Igual que en Windows y por lo mismo: una pantalla de la rocola son decenas de
        // dibujos SEGUIDOS con el mismo estado, y cambiar un estado es hablar con el controlador. Anotando
        // lo que ya esta puesto, solo se manda lo que de verdad cambia.
        private int stTss, stBlend;                  // 0 = sin fijar, 1 = color del vertice / 2 = textura*color ; 1 = sin mezcla, 2 = aditiva, 3 = normal
        private IntPtr stTex = new IntPtr(-1);       // -1 = sin fijar (IntPtr.Zero SI es un valor valido: sin textura)

        private void InitStates()
        {
            stTss = 0; stBlend = 0; stTex = new IntPtr(-1);
            D3D9.glDisable(D3D9.GL_DEPTH_TEST);
            D3D9.glDisable(D3D9.GL_LIGHTING);
            D3D9.glDisable(D3D9.GL_CULL_FACE);
            D3D9.glDisable(D3D9.GL_TEXTURE_2D);
            D3D9.glPixelStorei(D3D9.GL_UNPACK_ALIGNMENT, 4);   // las filas del bitmap vienen de 4 en 4 bytes
        }

        private void Tex(IntPtr t)
        {
            if (stTex == t) return;
            D3D9.glBindTexture(D3D9.GL_TEXTURE_2D, (t == IntPtr.Zero) ? 0u : (uint)t.ToInt64());
            stTex = t;
        }

        private void Blend(int modo)   // 1 = sin mezcla, 2 = aditiva (brillos), 3 = normal (encima)
        {
            if (stBlend == modo) return;
            if (modo == 1) D3D9.glDisable(D3D9.GL_BLEND);
            else
            {
                if (stBlend <= 1) D3D9.glEnable(D3D9.GL_BLEND);
                D3D9.glBlendFunc(D3D9.GL_SRC_ALPHA, (modo == 2) ? D3D9.GL_ONE : D3D9.GL_ONE_MINUS_SRC_ALPHA);
            }
            stBlend = modo;
        }

        private void TssDiffuse()   // usa el color del vertice (sin textura)
        {
            if (stTss == 1) return;
            D3D9.glDisable(D3D9.GL_TEXTURE_2D);
            stTss = 1;
        }
        private void TssModulate()  // textura * color del vertice
        {
            if (stTss == 2) return;
            D3D9.glEnable(D3D9.GL_TEXTURE_2D);
            D3D9.glTexEnvi(D3D9.GL_TEXTURE_ENV, D3D9.GL_TEXTURE_ENV_MODE, (int)D3D9.GL_MODULATE);
            stTss = 2;
        }

        // ---------------- TEXTURAS ----------------
        //
        // El bitmap de .NET en 32 bits guarda cada pixel como B,G,R,A en memoria, que es exactamente lo
        // que OpenGL llama GL_BGRA. O sea que se sube TAL CUAL, sin darle la vuelta a nada, igual que se
        // subia en Windows.
        private static int Pow2(int n) { int p = 1; while (p < n) p <<= 1; return p; }

        private uint NuevaTextura()
        {
            uint[] t = new uint[1];
            D3D9.glGenTextures(1, t);
            if (t[0] == 0) return 0;
            D3D9.glBindTexture(D3D9.GL_TEXTURE_2D, t[0]);
            stTex = new IntPtr(-1);                             // se acaba de tocar el enlace: el anotado ya no vale
            D3D9.glTexParameteri(D3D9.GL_TEXTURE_2D, D3D9.GL_TEXTURE_MAG_FILTER, (int)D3D9.GL_LINEAR);
            D3D9.glTexParameteri(D3D9.GL_TEXTURE_2D, D3D9.GL_TEXTURE_MIN_FILTER, (int)D3D9.GL_LINEAR);
            D3D9.glTexParameteri(D3D9.GL_TEXTURE_2D, D3D9.GL_TEXTURE_WRAP_S, (int)D3D9.GL_CLAMP_TO_EDGE);
            D3D9.glTexParameteri(D3D9.GL_TEXTURE_2D, D3D9.GL_TEXTURE_WRAP_T, (int)D3D9.GL_CLAMP_TO_EDGE);
            return t[0];
        }

        public IntPtr CreateTextureFromBitmap(Bitmap bmp)
        {
            if (ctx == IntPtr.Zero || bmp == null || !Actual()) return IntPtr.Zero;
            uint t = NuevaTextura();
            if (t == 0) return IntPtr.Zero;
            Sube(bmp, bmp.Width, bmp.Height, true);
            return new IntPtr(t);
        }

        // Crea una textura de tamano POTENCIA DE DOS (compatible con tarjeta vieja) y sube el bitmap en su
        // esquina superior izquierda. Devuelve las coordenadas de textura maximas usadas (uMax, vMax).
        public IntPtr CreateTexturePow2(Bitmap bmp, out float uMax, out float vMax)
        {
            uMax = 1f; vMax = 1f;
            if (ctx == IntPtr.Zero || bmp == null || !Actual()) return IntPtr.Zero;
            int w = bmp.Width, h = bmp.Height, tw = Pow2(w), th = Pow2(h);
            uint t = NuevaTextura();
            if (t == 0) return IntPtr.Zero;
            D3D9.glTexImage2D(D3D9.GL_TEXTURE_2D, 0, (int)D3D9.GL_RGBA, tw, th, 0, D3D9.GL_BGRA, D3D9.GL_UNSIGNED_BYTE, IntPtr.Zero);
            Sube(bmp, w, h, false);
            uMax = (float)w / tw; vMax = (float)h / th;
            return new IntPtr(t);
        }

        // Re-sube el contenido a una textura pow2 YA existente (mismo tamano de bitmap) en vez de crear
        // una nueva: evita crear y destruir una textura a pantalla completa en cada reconstruccion de UI.
        public bool UpdateTexturePow2(IntPtr tex, Bitmap bmp, out float uMax, out float vMax)
        {
            uMax = 1f; vMax = 1f;
            if (ctx == IntPtr.Zero || tex == IntPtr.Zero || bmp == null || !Actual()) return false;
            int w = bmp.Width, h = bmp.Height, tw = Pow2(w), th = Pow2(h);
            D3D9.glBindTexture(D3D9.GL_TEXTURE_2D, (uint)tex.ToInt64());
            stTex = new IntPtr(-1);
            Sube(bmp, w, h, false);
            uMax = (float)w / tw; vMax = (float)h / th;
            return true;
        }

        // Sube los pixeles del bitmap a la textura que este enlazada. 'entera' crea la imagen del tamano
        // del bitmap; si no, la mete en la esquina de una que ya existe.
        //
        // GL_UNPACK_ROW_LENGTH le dice a OpenGL cuantos pixeles hay DE VERDAD en cada fila del bitmap. Lo
        // necesita porque .NET redondea el ancho de fila hacia arriba, asi que una imagen de 130 pixeles
        // de ancho ocupa 132 en memoria; sin esto, cada fila entraria dos pixeles corrida respecto a la
        // anterior y la imagen saldria torcida.
        private void Sube(Bitmap bmp, int w, int h, bool entera)
        {
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                D3D9.glPixelStorei(D3D9.GL_UNPACK_ROW_LENGTH, bd.Stride / 4);
                if (entera) D3D9.glTexImage2D(D3D9.GL_TEXTURE_2D, 0, (int)D3D9.GL_RGBA, w, h, 0, D3D9.GL_BGRA, D3D9.GL_UNSIGNED_BYTE, bd.Scan0);
                else D3D9.glTexSubImage2D(D3D9.GL_TEXTURE_2D, 0, 0, 0, w, h, D3D9.GL_BGRA, D3D9.GL_UNSIGNED_BYTE, bd.Scan0);
                D3D9.glPixelStorei(D3D9.GL_UNPACK_ROW_LENGTH, 0);
            }
            finally { bmp.UnlockBits(bd); }
        }

        // ---------------- EL CUADRO ----------------
        //
        // En Direct3D habia que vigilar que el dispositivo no se "perdiera" (al bloquear la sesion, al
        // cambiar de resolucion) y rehacerlo. En OpenGL eso no existe: el contexto sigue ahi.
        public bool BeginFrame()
        {
            if (ctx == IntPtr.Zero || !Actual()) return false;
            // El mismo color de limpieza que en Windows (0xFF05060F), pasado a la escala 0..1 de OpenGL.
            D3D9.glClearColor(0x05 / 255f, 0x06 / 255f, 0x0F / 255f, 1f);
            D3D9.glClear(D3D9.GL_COLOR_BUFFER_BIT);
            return true;
        }

        public void EndFrame()
        {
            if (ctx == IntPtr.Zero) return;
            try { D3D9.glXSwapBuffers(dpy, win); } catch { }
        }

        // ---------------- LOS DIBUJADOS ----------------
        // Uno por uno, los mismos que en Windows y con la misma firma: el archivo del dibujo no se entera.
        public void DrawSky(D3D9.Vtx[] sky)
        {
            Tex(IntPtr.Zero); TssDiffuse(); Blend(1);
            Draw(sky, 4);
        }
        public void DrawStars(D3D9.Vtx[] stars, int starTris, IntPtr glowTex)
        {
            if (ctx == IntPtr.Zero || starTris <= 0 || glowTex == IntPtr.Zero) return;
            Tex(glowTex); TssModulate(); Blend(2);
            Draw(stars, starTris);
        }
        public void DrawShootStreaks(D3D9.Vtx[] v, int tris)
        {
            if (ctx == IntPtr.Zero || tris <= 0) return;
            Tex(IntPtr.Zero); TssDiffuse(); Blend(2);
            Draw(v, tris);
        }
        public void DrawMesh(D3D9.Vtx[] v, int tris)
        {
            if (ctx == IntPtr.Zero || tris <= 0) return;
            Tex(IntPtr.Zero); TssDiffuse(); Blend(3);
            Draw(v, tris);
        }
        public void DrawMeshAdd(D3D9.Vtx[] v, int tris)
        {
            if (ctx == IntPtr.Zero || tris <= 0) return;
            Tex(IntPtr.Zero); TssDiffuse(); Blend(2);
            Draw(v, tris);
        }
        public void DrawTexQuad(IntPtr tex, D3D9.Vtx[] quad)
        {
            if (tex == IntPtr.Zero) return;
            Tex(tex); TssModulate(); Blend(3);
            Draw(quad, 2);
        }
        public void DrawQuads(IntPtr tex, D3D9.Vtx[] v, int tris)
        {
            if (ctx == IntPtr.Zero || tris <= 0) return;
            if (tex != IntPtr.Zero) { Tex(tex); TssModulate(); } else { Tex(IntPtr.Zero); TssDiffuse(); }
            Blend(3);
            Draw(v, tris);
        }

        // EL DIBUJADO DE VERDAD: se le pasa el arreglo de vertices tal cual esta en memoria y se le dice
        // donde empieza cada cosa dentro de cada vertice. Es lo mismo que hacia DrawPrimitiveUP.
        //
        //   vertice = X,Y,Z,Rhw (4 floats, 16 bytes) | color (4 bytes) | U,V (2 floats, 8 bytes) = 28
        //
        // Z y Rhw no se usan (el dibujado es plano), asi que se le piden solo dos numeros de sitio.
        //
        // EL COLOR Y SUS CUATRO BYTES. El color viene guardado como 0xAARRGGBB, que en memoria queda
        // B,G,R,A; OpenGL, si se le piden 4 bytes, los lee como R,G,B,A. O sea que estarian cambiados el
        // rojo y el azul. Se le pide GL_BGRA, que es justamente "leelos al reves", y entonces no hay que
        // tocar ni un byte. Eso lo entienden todas las tarjetas de los ultimos quince anos, pero si
        // alguna no lo entendiera se nota AQUI MISMO -OpenGL avisa- y se pasa a darles la vuelta a mano,
        // que cuesta un poco pero se ve igual. Se comprueba una sola vez, en el primer dibujado.
        private void Draw(D3D9.Vtx[] verts, int tris)
        {
            if (ctx == IntPtr.Zero || verts == null || tris <= 0) return;
            int n = tris * 3;
            if (n > verts.Length) n = verts.Length;
            GCHandle gh = GCHandle.Alloc(verts, GCHandleType.Pinned);
            try
            {
                IntPtr b = gh.AddrOfPinnedObject();
                D3D9.glEnableClientState(D3D9.GL_VERTEX_ARRAY);
                D3D9.glEnableClientState(D3D9.GL_COLOR_ARRAY);
                D3D9.glEnableClientState(D3D9.GL_TEXTURE_COORD_ARRAY);
                D3D9.glVertexPointer(2, D3D9.GL_FLOAT, D3D9.VtxSize, b);
                D3D9.glTexCoordPointer(2, D3D9.GL_FLOAT, D3D9.VtxSize, new IntPtr(b.ToInt64() + 20));
                if (colorBgra)
                {
                    D3D9.glGetError();                                    // se limpia lo que hubiera de antes
                    D3D9.glColorPointer((int)D3D9.GL_BGRA, D3D9.GL_UNSIGNED_BYTE, D3D9.VtxSize, new IntPtr(b.ToInt64() + 16));
                    if (D3D9.glGetError() != D3D9.GL_NO_ERROR) colorBgra = false;   // esta tarjeta no: a mano
                }
                if (!colorBgra)
                {
                    if (colorAux == null || colorAux.Length < n * 4) colorAux = new byte[n * 4];
                    for (int i = 0; i < n; i++)
                    {
                        uint c = verts[i].Color;
                        colorAux[i * 4 + 0] = (byte)(c >> 16);            // rojo
                        colorAux[i * 4 + 1] = (byte)(c >> 8);             // verde
                        colorAux[i * 4 + 2] = (byte)c;                    // azul
                        colorAux[i * 4 + 3] = (byte)(c >> 24);            // opacidad
                    }
                    GCHandle gc = GCHandle.Alloc(colorAux, GCHandleType.Pinned);
                    try
                    {
                        D3D9.glColorPointer(4, D3D9.GL_UNSIGNED_BYTE, 0, gc.AddrOfPinnedObject());
                        D3D9.glDrawArrays(D3D9.GL_TRIANGLES, 0, n);
                    }
                    finally { gc.Free(); }
                    return;
                }
                D3D9.glDrawArrays(D3D9.GL_TRIANGLES, 0, n);
            }
            catch { }
            finally { gh.Free(); }
        }

        public void Dispose()
        {
            if (ctx == IntPtr.Zero) return;
            try { D3D9.glXMakeCurrent(dpy, IntPtr.Zero, IntPtr.Zero); } catch { }
            try { D3D9.glXDestroyContext(dpy, ctx); } catch { }
            ctx = IntPtr.Zero; dpy = IntPtr.Zero; win = IntPtr.Zero;
        }
    }
}
#endif
