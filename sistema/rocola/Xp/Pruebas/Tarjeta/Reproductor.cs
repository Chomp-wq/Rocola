// EL REPRODUCTOR DE CINTAS DE LA TARJETA (pruebas del port nativo, 8-11-2026; no va en el programa).
//
// Hace las llamadas de una cinta (grabada con ROCOLA_CINTA, Nativo/CintaTarjeta.cs) en el dispositivo de la tarjeta de un rocola-xp.dll
// cargado por reflexion (JIT): el de la copia congelada con capas (D3D9Device sobre Direct3DaGL) o el de la traduccion nativa
// (DispositivoTarjeta sobre OpenGL ES 2 directo). Despues de cada cuadro lee el fondo del dispositivo (lo que se presenta) y escribe
// una linea con su SHA-256, los contadores del dispositivo (pedidos, envios, cambios, perdidas) y los errores de OpenGL; y una linea por
// cada llamada que devuelve algo (las texturas con un nombre propio de la corrida: t1, t2...). Dos corridas son iguales si sus salidas
// son iguales linea por linea (diff). Entre cuadro y cuadro ensucia OpenGL como el compositor (otro framebuffer, otro programa, otras
// texturas en las unidades 0 a 3, mezcla, recorte, ARRAY_BUFFER y atributos de otro bufer...) y avisa al dispositivo como el anfitrion
// (la referencia: D3D9.EnGl.EstadoTocado; la traduccion: PantallaTarjeta.Tocada).
//
//   reproductor-cintas --programa DIR --cinta CINTA --sale SALIDA.txt [opciones]
//     --limite-textura N      el limite de textura del dispositivo en N (las texturas mas grandes van en teselas)
//     --redimensiona K:AxB    antes del cuadro K (desde 1) se cambia el tamano del fondo (Redimensiona), como un cambio de resolucion
//     --sin-avisar            se ensucia OpenGL pero no se avisa al dispositivo (control: tiene que salir distinto)
//     --sin-ensuciar          ni se ensucia ni se avisa
//     --vuelca K1,K2,... DIR  guarda el fondo de esos cuadros (BGRA crudo, fila 0 arriba) en DIR/cuadro-K.bgra
//     --hasta K               solo los primeros K cuadros
//   reproductor-cintas --compara DIR1 DIR2   compara los volcados punto a punto (bytes distintos, el mayor, el primero)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SkiaSharp;

namespace PruebasTarjeta
{
    internal static unsafe class Reproductor
    {
        const BindingFlags Todo = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // ---------------- lo que se usa del programa (por reflexion) ----------------
        static Assembly asm;
        static Type tDisp, tVtx, tVtx2, tMapa, tFormato;
        static MethodInfo mSuelta, mLeeFondoRef, mLeeFondoNat, mTocado;
        static FieldInfo fDev, fPedidos, fEnvios, fCambios, fPerdidas, fLost, fVineta, fCubo, fLimW, fLimH;
        static ConstructorInfo cDisp, cMapa;
        static readonly Dictionary<string, MethodInfo> metodos = new Dictionary<string, MethodInfo>();
        static string variante;

        static MethodInfo M(string nombre, int parametros)
        {
            string k = nombre + "/" + parametros;
            MethodInfo m;
            if (metodos.TryGetValue(k, out m)) return m;
            foreach (MethodInfo x in tDisp.GetMethods(Todo))
                if (x.Name == nombre && x.GetParameters().Length == parametros && !x.IsStatic) { m = x; break; }
            if (m == null) throw new MissingMethodException(tDisp.Name + "." + nombre + "/" + parametros);
            metodos[k] = m;
            return m;
        }

        static void CargaPrograma(string dir)
        {
            string dll = Path.Combine(dir, "rocola-xp.dll");
            AppDomain.CurrentDomain.AssemblyResolve += delegate (object s, ResolveEventArgs e)
            {
                string n = new AssemblyName(e.Name).Name;
                string p = Path.Combine(dir, n + ".dll");
                return File.Exists(p) ? Assembly.LoadFrom(p) : null;
            };
            asm = Assembly.LoadFrom(dll);
            tDisp = asm.GetType("Rocola.DispositivoTarjeta") ?? asm.GetType("Rocola.D3D9Device");
            tVtx = asm.GetType("Rocola.Tarjeta+Vtx") ?? asm.GetType("Rocola.D3D9+Vtx");
            tVtx2 = asm.GetType("Rocola.Tarjeta+Vtx2") ?? asm.GetType("Rocola.D3D9+Vtx2");
            Type tTarjeta = asm.GetType("Rocola.Tarjeta");
            mSuelta = tTarjeta != null ? tTarjeta.GetMethod("Suelta", Todo) : null;
            if (mSuelta == null) mSuelta = asm.GetType("Rocola.D3D9").GetMethod("Release", Todo);
            tMapa = asm.GetType("Rocola.Dibujo2D.Bitmap");
            tFormato = asm.GetType("Rocola.Dibujo2D.PixelFormat");
            foreach (ConstructorInfo c in tMapa.GetConstructors(Todo))
            {
                ParameterInfo[] p = c.GetParameters();
                if (p.Length == 2 && p[0].ParameterType == typeof(SKBitmap)) cMapa = c;
            }
            cDisp = tDisp.GetConstructor(Todo, null, new[] { typeof(IntPtr), typeof(int), typeof(int) }, null);
            mLeeFondoNat = tDisp.GetMethod("LeeFondo", Todo);
            Type tEnGl = asm.GetType("Rocola.D3D9+EnGl");
            if (mLeeFondoNat == null) mLeeFondoRef = tEnGl.GetMethod("LeeFondo", Todo);
            Type tPant = asm.GetType("Rocola.PantallaTarjeta");
            mTocado = tPant != null ? tPant.GetMethod("Tocada", Todo) : null;
            if (mTocado == null) mTocado = tEnGl.GetMethod("EstadoTocado", Todo);
            fDev = tDisp.GetField("dev", Todo);
            fPedidos = tDisp.GetField("Pedidos", Todo); fEnvios = tDisp.GetField("Envios", Todo); fCambios = tDisp.GetField("Cambios", Todo);
            fPerdidas = tDisp.GetField("Perdidas", Todo); fLost = tDisp.GetField("lost", Todo);
            fVineta = tDisp.GetField("VinetaPuntos", Todo); fCubo = tDisp.GetField("CubeBaseColor", Todo);
            fLimW = tDisp.GetField("limiteTexturaW", Todo); fLimH = tDisp.GetField("limiteTexturaH", Todo);
            variante = tDisp.Name + (mLeeFondoNat != null ? " (OpenGL directo)" : " (Direct3DaGL)") + ", soltar " + mSuelta.DeclaringType.Name + "." + mSuelta.Name +
                ", aviso " + mTocado.DeclaringType.Name + "." + mTocado.Name;
            // el inicializador del modulo de la rocola abre la pantalla fuera de pantalla y OpenGL en este hilo (PruebasLinux.cs)
            RuntimeHelpers.RunModuleConstructor(asm.ManifestModule.ModuleHandle);
            if (eglGetCurrentContext() == IntPtr.Zero)
            {
                Type fp = asm.GetType("Rocola.Pantalla.FueraDePantalla");
                Activator.CreateInstance(fp, Todo, null, new object[] { 1024, 768 }, null);
            }
            if (eglGetCurrentContext() == IntPtr.Zero) throw new Exception("sin contexto de OpenGL");
        }

        // ---------------- OpenGL (para leer, ensuciar y mirar errores) ----------------
        const string L = "libGLESv2.so.2";
        [DllImport("libEGL.so.1")] static extern IntPtr eglGetCurrentContext();
        [DllImport(L)] static extern uint glGetError();
        [DllImport(L)] static extern void glGenTextures(int n, uint* t);
        [DllImport(L)] static extern void glBindTexture(uint d, uint t);
        [DllImport(L)] static extern void glTexImage2D(uint d, int nivel, int fi, int w, int h, int borde, uint f, uint tipo, void* p);
        [DllImport(L)] static extern void glTexParameteri(uint d, uint p, int v);
        [DllImport(L)] static extern void glGenFramebuffers(int n, uint* f);
        [DllImport(L)] static extern void glBindFramebuffer(uint d, uint f);
        [DllImport(L)] static extern void glFramebufferTexture2D(uint d, uint a, uint t, uint tex, int nivel);
        [DllImport(L)] static extern void glViewport(int x, int y, int w, int h);
        [DllImport(L)] static extern void glScissor(int x, int y, int w, int h);
        [DllImport(L)] static extern void glColorMask(byte r, byte g, byte b, byte a);
        [DllImport(L)] static extern void glEnable(uint c);
        [DllImport(L)] static extern void glDisable(uint c);
        [DllImport(L)] static extern void glBlendFunc(uint s, uint d);
        [DllImport(L)] static extern void glBlendEquation(uint e);
        [DllImport(L)] static extern void glCullFace(uint m);
        [DllImport(L)] static extern void glActiveTexture(uint u);
        [DllImport(L)] static extern void glGenBuffers(int n, uint* b);
        [DllImport(L)] static extern void glBindBuffer(uint d, uint b);
        [DllImport(L)] static extern void glBufferData(uint d, IntPtr tam, void* p, uint uso);
        [DllImport(L)] static extern void glEnableVertexAttribArray(uint i);
        [DllImport(L)] static extern void glVertexAttribPointer(uint i, int n, uint tipo, byte norm, int paso, IntPtr p);
        [DllImport(L)] static extern void glPixelStorei(uint p, int v);
        [DllImport(L)] static extern void glClearColor(float r, float g, float b, float a);
        [DllImport(L)] static extern void glClear(uint m);
        [DllImport(L)] static extern uint glCreateShader(uint t);
        [DllImport(L)] static extern void glShaderSource(uint s, int n, byte** f, int* l);
        [DllImport(L)] static extern void glCompileShader(uint s);
        [DllImport(L)] static extern uint glCreateProgram();
        [DllImport(L)] static extern void glAttachShader(uint p, uint s);
        [DllImport(L)] static extern void glBindAttribLocation(uint p, uint i, string n);
        [DllImport(L)] static extern void glLinkProgram(uint p);
        [DllImport(L)] static extern void glUseProgram(uint p);
        [DllImport(L)] static extern void glDrawArrays(uint m, int f, int n);
        [DllImport(L)] static extern void glReadPixels(int x, int y, int w, int h, uint f, uint t, void* p);
        const uint TEXTURE_2D = 0x0DE1, RGBA = 0x1908, UNSIGNED_BYTE = 0x1401, FLOAT = 0x1406, FRAMEBUFFER = 0x8D40, COLOR_ATTACHMENT0 = 0x8CE0,
            BLEND = 0x0BE2, SCISSOR_TEST = 0x0C11, DITHER = 0x0BD0, CULL_FACE = 0x0B44, DEPTH_TEST = 0x0B71, STENCIL_TEST = 0x0B90,
            ONE = 1, FRONT = 0x0404, FUNC_REVERSE_SUBTRACT = 0x800B, TEXTURE0 = 0x84C0, ARRAY_BUFFER = 0x8892, STATIC_DRAW = 0x88E4,
            UNPACK_ALIGNMENT = 0x0CF5, PACK_ALIGNMENT = 0x0D05, COLOR_BUFFER_BIT = 0x4000, VERTEX_SHADER = 0x8B31, FRAGMENT_SHADER = 0x8B30,
            TRIANGLES = 4, TEXTURE_MIN_FILTER = 0x2801, TEXTURE_MAG_FILTER = 0x2800, NEAREST = 0x2600, TEXTURE_WRAP_S = 0x2802, MIRRORED_REPEAT = 0x8370;

        static uint suciaTex0, suciaTex1, suciaTex2, suciaFbo, suciaVbo, suciaProg;
        static uint Sombreador(uint tipo, string f)
        {
            uint s = glCreateShader(tipo);
            byte[] b = Encoding.ASCII.GetBytes(f);
            fixed (byte* pb = b) { byte* q = pb; int n = b.Length; glShaderSource(s, 1, &q, &n); }
            glCompileShader(s);
            return s;
        }
        // Lo que hacen el compositor (Anfitrion.Compone), mpv y la flecha entre un cuadro y otro, y algo mas: todo lo que el dispositivo
        // tiene que volver a poner (o no tocar) antes de dibujar.
        static void Ensucia()
        {
            if (suciaFbo == 0)
            {
                uint t; glGenTextures(1, &t); suciaTex0 = t; glGenTextures(1, &t); suciaTex1 = t; glGenTextures(1, &t); suciaTex2 = t;
                uint[] ts = { suciaTex0, suciaTex1, suciaTex2 };
                uint* px = stackalloc uint[64];
                for (int i = 0; i < 64; i++) px[i] = 0xFF00FFFFu ^ (uint)(i * 0x01030507);
                foreach (uint x in ts)
                {
                    glBindTexture(TEXTURE_2D, x);
                    glTexImage2D(TEXTURE_2D, 0, (int)RGBA, 8, 8, 0, RGBA, UNSIGNED_BYTE, px);
                    glTexParameteri(TEXTURE_2D, TEXTURE_MIN_FILTER, (int)NEAREST); glTexParameteri(TEXTURE_2D, TEXTURE_MAG_FILTER, (int)NEAREST);
                    glTexParameteri(TEXTURE_2D, TEXTURE_WRAP_S, (int)MIRRORED_REPEAT);
                }
                uint f; glGenFramebuffers(1, &f); suciaFbo = f;
                glBindFramebuffer(FRAMEBUFFER, f); glFramebufferTexture2D(FRAMEBUFFER, COLOR_ATTACHMENT0, TEXTURE_2D, suciaTex2, 0);
                uint b; glGenBuffers(1, &b); suciaVbo = b;
                glBindBuffer(ARRAY_BUFFER, b);
                float[] v = new float[65536 * 4];
                for (int i = 0; i < v.Length; i++) v[i] = (i % 7) * 0.31f - 0.9f;
                fixed (float* pv = v) glBufferData(ARRAY_BUFFER, (IntPtr)(v.Length * 4), pv, STATIC_DRAW);
                uint vs = Sombreador(VERTEX_SHADER, "attribute vec4 a; attribute vec4 b; varying vec4 c; void main() { gl_Position = vec4(a.xy, 0.0, 1.0); c = b; }\n");
                uint fs = Sombreador(FRAGMENT_SHADER, "precision mediump float; varying vec4 c; uniform sampler2D s; void main() { gl_FragColor = c + texture2D(s, c.xy); }\n");
                uint p = glCreateProgram(); glAttachShader(p, vs); glAttachShader(p, fs);
                glBindAttribLocation(p, 0, "a"); glBindAttribLocation(p, 1, "b");
                glLinkProgram(p); suciaProg = p;
            }
            glBindFramebuffer(FRAMEBUFFER, suciaFbo);
            glViewport(0, 0, 7, 5);
            glColorMask(1, 1, 1, 1);
            glEnable(SCISSOR_TEST); glScissor(1, 1, 3, 3);
            glClearColor(1, 0, 1, 0.5f); glClear(COLOR_BUFFER_BIT);
            glEnable(BLEND); glBlendFunc(ONE, ONE); glBlendEquation(FUNC_REVERSE_SUBTRACT);
            glEnable(DITHER); glEnable(CULL_FACE); glCullFace(FRONT);
            glUseProgram(suciaProg);
            glActiveTexture(TEXTURE0 + 1); glBindTexture(TEXTURE_2D, suciaTex1);
            glActiveTexture(TEXTURE0 + 2); glBindTexture(TEXTURE_2D, suciaTex0);
            glActiveTexture(TEXTURE0); glBindTexture(TEXTURE_2D, suciaTex0);
            glBindBuffer(ARRAY_BUFFER, suciaVbo);
            for (uint i = 0; i < 5; i++) { glVertexAttribPointer(i, 4, FLOAT, 0, 16, IntPtr.Zero); glEnableVertexAttribArray(i); }
            glDrawArrays(TRIANGLES, 0, 3);
            glActiveTexture(TEXTURE0 + 3); glBindTexture(TEXTURE_2D, suciaTex1);   // y la unidad activa queda en la 3
            glPixelStorei(UNPACK_ALIGNMENT, 1); glPixelStorei(PACK_ALIGNMENT, 1);
        }

        // ---------------- la cinta ----------------
        sealed class Estado
        {
            internal readonly Dictionary<int, object> disps = new Dictionary<int, object>();
            internal readonly Dictionary<long, IntPtr> texturas = new Dictionary<long, IntPtr>();   // la textura de la cinta -> la de esta corrida
            internal readonly Dictionary<IntPtr, int> nombres = new Dictionary<IntPtr, int>();       // las vivas de esta corrida -> su nombre
            internal int siguienteNombre;
            internal readonly Dictionary<int, object> mapas = new Dictionary<int, object>();
            internal readonly Dictionary<int, SKBitmap> mapasSk = new Dictionary<int, SKBitmap>();
            internal int cuadros, llamadas, dibujos, sueltas, desviaciones, erroresGl, sinTextura;
            internal readonly HashSet<string> distintos = new HashSet<string>();
            internal readonly SortedDictionary<string, int> porMetodo = new SortedDictionary<string, int>(StringComparer.Ordinal);
        }
        static readonly IntPtr Desconocida = new IntPtr(0x7FFFFFF0);
        static IntPtr Tex(Estado e, long t)
        {
            if (t == 0) return IntPtr.Zero;
            IntPtr r;
            if (e.texturas.TryGetValue(t, out r)) return r;
            e.sinTextura++;
            return Desconocida;
        }
        static string Nombre(Estado e, IntPtr h)
        {
            if (h == IntPtr.Zero) return "0";
            int n;
            if (!e.nombres.TryGetValue(h, out n)) { n = ++e.siguienteNombre; e.nombres[h] = n; }
            return "t" + n;
        }
        static string NombreNuevo(Estado e, long tCinta, IntPtr h)
        {
            if (tCinta != 0 && h != IntPtr.Zero) e.texturas[tCinta] = h;
            return Nombre(e, h);
        }
        static object Vertices(BinaryReader r, Type tipo, int tam)
        {
            int largo = r.ReadInt32(), n = r.ReadInt32();
            if (largo < 0) return null;
            Array a = Array.CreateInstance(tipo, n);
            if (n > 0)
            {
                byte[] b = r.ReadBytes(n * tam);
                GCHandle g = GCHandle.Alloc(a, GCHandleType.Pinned);
                try { Marshal.Copy(b, 0, g.AddrOfPinnedObject(), b.Length); } finally { g.Free(); }
            }
            return a;
        }
        static object Mapa(Estado e, BinaryReader r)
        {
            int id = r.ReadInt32();
            if (id < 0) return null;
            int w = r.ReadInt32(), h = r.ReadInt32(), formato = r.ReadInt32();
            int x = r.ReadInt32(), y = r.ReadInt32(), zw = r.ReadInt32(), zh = r.ReadInt32();
            object m; SKBitmap sk;
            if (!e.mapas.TryGetValue(id, out m) || e.mapasSk[id].Width != w || e.mapasSk[id].Height != h)
            {
                sk = new SKBitmap(new SKImageInfo(Math.Max(1, w), Math.Max(1, h), SKColorType.Bgra8888, SKAlphaType.Premul));
                sk.Erase(SKColors.Transparent);
                m = cMapa.Invoke(new object[] { sk, Enum.ToObject(tFormato, formato) });
                e.mapas[id] = m; e.mapasSk[id] = sk;
            }
            sk = e.mapasSk[id];
            if (zw > 0 && zh > 0)
            {
                byte* p = (byte*)sk.GetPixels(); int rb = sk.RowBytes;
                for (int k = 0; k < zh; k++)
                {
                    byte[] fila = r.ReadBytes(zw * 4);
                    Marshal.Copy(fila, 0, (IntPtr)(p + (long)(y + k) * rb + (long)x * 4), fila.Length);
                }
                sk.NotifyPixelsChanged();
            }
            return m;
        }
        static string Medida(Estado e, object m)
        {
            if (m == null) return "null";
            foreach (KeyValuePair<int, object> k in e.mapas) if (ReferenceEquals(k.Value, m)) return e.mapasSk[k.Key].Width + "x" + e.mapasSk[k.Key].Height;
            return "?";
        }
        static Estado estado;
        static object Invoca(object d, string nombre, params object[] a)
        {
            int c; estado.porMetodo.TryGetValue(nombre, out c); estado.porMetodo[nombre] = c + 1;
            try { return M(nombre, a.Length).Invoke(d, a); }
            catch (TargetInvocationException ex) { throw new Exception(nombre + ": " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message, ex.InnerException); }
        }

        static int Main(string[] args)
        {
            if (args.Length >= 3 && args[0] == "--compara") return Compara(args[1], args[2]);
            if (args.Length >= 2 && args[0] == "--sintetica") return CintaSintetica.Escribe(args[1]);
            if (args.Length >= 3 && args[0] == "--fuentes") return Fuentes.Escribe(args[1], args[2]);
            if (args.Length >= 2 && args[0] == "--piezas") return Piezas.Prueba(args[1]);
            string dir = null, cinta = null, sale = null, volcado = null;
            int limite = 0, hasta = int.MaxValue;
            bool sinAvisar = false, sinEnsuciar = false;
            HashSet<int> vuelca = new HashSet<int>();
            Dictionary<int, int[]> redim = new Dictionary<int, int[]>();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--programa": dir = args[++i]; break;
                    case "--cinta": cinta = args[++i]; break;
                    case "--sale": sale = args[++i]; break;
                    case "--limite-textura": limite = int.Parse(args[++i]); break;
                    case "--hasta": hasta = int.Parse(args[++i]); break;
                    case "--sin-avisar": sinAvisar = true; break;
                    case "--sin-ensuciar": sinEnsuciar = true; break;
                    case "--vuelca": foreach (string k in args[++i].Split(',')) vuelca.Add(int.Parse(k)); volcado = args[++i]; break;
                    case "--redimensiona":
                        foreach (string k in args[++i].Split(','))
                        {
                            string[] p = k.Split(':'); string[] m = p[1].Split('x');
                            redim[int.Parse(p[0])] = new[] { int.Parse(m[0]), int.Parse(m[1]) };
                        }
                        break;
                    default: Console.Error.WriteLine("no se que es " + args[i]); return 2;
                }
            }
            if (dir == null || cinta == null || sale == null) { Console.Error.WriteLine("falta --programa, --cinta o --sale"); return 2; }
            foreach (string v in new[] { "ROCOLA_PULSA", "ROCOLA_CAPTURA", "ROCOLA_CINTA", "ROCOLA_TECLAS" }) Environment.SetEnvironmentVariable(v, null);
            Environment.SetEnvironmentVariable("ROCOLA_PANTALLA", "fuera:1024x768");
            if (volcado != null) Directory.CreateDirectory(volcado);
            CargaPrograma(dir);
            Console.WriteLine("Programa: " + asm.Location + " -> " + variante);
            Estado e = new Estado(); estado = e;
            System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();
            using (StreamWriter o = new StreamWriter(sale, false, new UTF8Encoding(false)))
            using (FileStream f = File.OpenRead(cinta))
            using (BrotliStream bs = new BrotliStream(f, CompressionMode.Decompress))
            using (BufferedStream buf = new BufferedStream(bs, 1 << 20))
            using (BinaryReader r = new BinaryReader(buf))
            {
                string cab = Encoding.ASCII.GetString(r.ReadBytes(23));
                if (cab != "ROCOLA-CINTA-TARJETA 1\n") throw new Exception("no es una cinta de la tarjeta");
                o.WriteLine("# cinta " + Path.GetFileName(cinta) + (limite > 0 ? ", limite de textura " + limite : "") + (redim.Count > 0 ? ", redimensiona" : "") + (sinAvisar ? ", sin avisar" : "") + (sinEnsuciar ? ", sin ensuciar" : ""));
                byte[] cabecera = new byte[5];
                while (true)
                {
                    int leidos = buf.Read(cabecera, 0, 5);
                    if (leidos == 0) break;
                    while (leidos < 5) { int n = buf.Read(cabecera, leidos, 5 - leidos); if (n == 0) throw new EndOfStreamException(); leidos += n; }
                    byte tipo = cabecera[0];
                    int largo = BitConverter.ToInt32(cabecera, 1);
                    byte[] datos = r.ReadBytes(largo);
                    if (datos.Length != largo) { o.WriteLine("# la cinta se corta (registro a medias)"); break; }
                    using (BinaryReader q = new BinaryReader(new MemoryStream(datos)))
                    {
                        if (tipo == 1)   // NUEVO
                        {
                            int id = q.ReadInt32(); q.ReadInt64(); int w = q.ReadInt32(), h = q.ReadInt32();
                            object d = cDisp.Invoke(new object[] { IntPtr.Zero, w, h });
                            e.disps[id] = d;
                            if (limite > 0) { fLimW.SetValue(d, limite); fLimH.SetValue(d, limite); }
                            o.WriteLine("nuevo disp" + id + " " + w + "x" + h + " ok=" + Invoca(d, "get_Ok"));
                        }
                        else if (tipo == 3)   // SUELTA
                        {
                            long t = q.ReadInt64(); bool enPantalla = q.ReadBoolean();
                            IntPtr h = Tex(e, t);
                            if (h != Desconocida) { mSuelta.Invoke(null, new object[] { h }); e.nombres.Remove(h); }
                            e.sueltas++;
                            if (!enPantalla) o.WriteLine("suelta desde otro hilo " + Nombre(e, h));
                        }
                        else if (tipo == 4)   // CAMPO
                        {
                            int id = q.ReadInt32(); byte campo = q.ReadByte(); uint v = q.ReadUInt32();
                            object d = e.disps[id];
                            if (campo == 1) fVineta.SetValue(d, v != 0); else fCubo.SetValue(d, v);
                        }
                        else if (tipo == 2)   // LLAMADA
                        {
                            int id = q.ReadInt32(); byte metodo = q.ReadByte(); q.ReadDouble();
                            object d = e.disps[id];
                            e.llamadas++;
                            if (metodo == 5 && e.cuadros >= hasta) break;
                            if (metodo == 5 && redim.ContainsKey(e.cuadros + 1))
                            {
                                int[] m = redim[e.cuadros + 1];
                                o.WriteLine("redimensiona " + m[0] + "x" + m[1] + " -> " + Invoca(d, "Redimensiona", m[0], m[1]));
                            }
                            string res = Llamada(e, d, metodo, q);
                            if (res != null) o.WriteLine(res);
                            if (metodo == 6) Cuadro(e, d, o, vuelca, volcado, sinAvisar, sinEnsuciar);
                        }
                        else throw new Exception("registro desconocido " + tipo);
                    }
                }
                StringBuilder pm = new StringBuilder("# llamadas por metodo:");
                foreach (KeyValuePair<string, int> k in e.porMetodo) pm.Append(' ').Append(k.Key).Append('=').Append(k.Value);
                o.WriteLine(pm.ToString());
                o.WriteLine("# " + e.cuadros + " cuadros (" + e.distintos.Count + " distintos), " + e.llamadas + " llamadas, " + e.dibujos + " dibujos, " + e.sueltas + " texturas soltadas, "
                    + e.erroresGl + " errores de OpenGL, " + e.sinTextura + " texturas que la cinta no conocia");
            }
            Console.WriteLine(e.cuadros + " cuadros (" + e.distintos.Count + " distintos) en " + reloj.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s; " + e.llamadas + " llamadas, "
                + e.dibujos + " dibujos, " + e.erroresGl + " errores de OpenGL; desviaciones respecto de la grabacion: " + e.desviaciones);
            return 0;
        }

        static string Llamada(Estado e, object d, byte metodo, BinaryReader q)
        {
            switch (metodo)
            {
                case 1: { int w = q.ReadInt32(), h = q.ReadInt32(); bool g = q.ReadBoolean(); bool r = (bool)Invoca(d, "Redimensiona", w, h); Desvia(e, g, r); return "Redimensiona " + w + "x" + h + " -> " + r; }
                case 2: { bool g = q.ReadBoolean(); bool r = (bool)Invoca(d, "PreparaDispositivo"); Desvia(e, g, r); return "PreparaDispositivo -> " + r; }
                case 3: { long t = q.ReadInt64(); bool g = q.ReadBoolean(); bool r = (bool)Invoca(d, "PreloadTexture", Tex(e, t)); Desvia(e, g, r); return "PreloadTexture " + Nombre(e, Tex(e, t)) + " -> " + r; }
                case 4:
                    {
                        object m = Mapa(e, q); long g = q.ReadInt64();
                        IntPtr r = (IntPtr)Invoca(d, "CreateTextureFromBitmap", m);
                        Desvia(e, g != 0, r != IntPtr.Zero);
                        return "CreateTextureFromBitmap " + Medida(e, m) + " -> " + NombreNuevo(e, g, r);
                    }
                case 17:
                case 28:
                    {
                        object m = Mapa(e, q); long g = q.ReadInt64(); float gu = q.ReadSingle(), gv = q.ReadSingle();
                        object[] a = { m, 0f, 0f };
                        IntPtr r = (IntPtr)M(metodo == 17 ? "CreateTexturePow2" : "CreaPortadaCompartida", 3).Invoke(d, a);
                        Desvia(e, g != 0, r != IntPtr.Zero); Desvia(e, gu, (float)a[1]); Desvia(e, gv, (float)a[2]);
                        return (metodo == 17 ? "CreateTexturePow2 " : "CreaPortadaCompartida ") + Medida(e, m) + " -> " + NombreNuevo(e, g, r) + " u=" + F((float)a[1]) + " v=" + F((float)a[2]);
                    }
                case 18:
                    {
                        long t = q.ReadInt64(); object m = Mapa(e, q);
                        int x = q.ReadInt32(), y = q.ReadInt32(), w = q.ReadInt32(), h = q.ReadInt32(); bool g = q.ReadBoolean();
                        bool r = (bool)Invoca(d, "UpdateTextureRegion", Tex(e, t), m, new System.Drawing.Rectangle(x, y, w, h));
                        Desvia(e, g, r);
                        return "UpdateTextureRegion " + Nombre(e, Tex(e, t)) + " " + x + "," + y + "," + w + "x" + h + " -> " + r;
                    }
                case 19:
                    {
                        long t = q.ReadInt64(); object m = Mapa(e, q); bool g = q.ReadBoolean(); float gu = q.ReadSingle(), gv = q.ReadSingle();
                        object[] a = { Tex(e, t), m, 0f, 0f };
                        bool r = (bool)M("UpdateTexturePow2", 4).Invoke(d, a);
                        Desvia(e, g, r);
                        return "UpdateTexturePow2 " + Nombre(e, Tex(e, t)) + " " + Medida(e, m) + " -> " + r + " u=" + F((float)a[2]) + " v=" + F((float)a[3]);
                    }
                case 20: Invoca(d, "Dispose"); return "Dispose";
                case 21: Invoca(d, "EmpiezaCuenta", q.ReadBoolean()); return null;
                case 27: { bool s = q.ReadBoolean(), g = q.ReadBoolean(); bool r = (bool)Invoca(d, "PideSinEspera", s); Desvia(e, g, r); return "PideSinEspera " + s + " -> " + r; }
                case 5: { bool g = q.ReadBoolean(); bool r = (bool)Invoca(d, "BeginFrame"); Desvia(e, g, r); return r ? null : "BeginFrame -> False"; }
                case 6: { q.ReadBoolean(); return null; }   // (EndFrame: en Cuadro)
                case 22: { int w = q.ReadInt32(), h = q.ReadInt32(), div = q.ReadInt32(); bool g = q.ReadBoolean(); bool r = (bool)Invoca(d, "EmpiezaMedia", w, h, div); Desvia(e, g, r); return "EmpiezaMedia " + w + "x" + h + "/" + div + " -> " + r; }
                case 23: { bool g = q.ReadBoolean(); bool r = (bool)Invoca(d, "TerminaMedia"); Desvia(e, g, r); return "TerminaMedia -> " + r; }
                case 26:
                    {
                        long t0 = q.ReadInt64(), t1 = q.ReadInt64(); int tris = q.ReadInt32(); object v = Vertices(q, tVtx2, 36); bool g = q.ReadBoolean();
                        bool r = (bool)Invoca(d, "DibujaDosTexturas", Tex(e, t0), Tex(e, t1), v, tris);
                        Desvia(e, g, r); e.dibujos++;
                        return "DibujaDosTexturas " + Nombre(e, Tex(e, t0)) + " " + Nombre(e, Tex(e, t1)) + " " + tris + " -> " + r;
                    }
                case 7: { object v = Vertices(q, tVtx, 28); Invoca(d, "DrawSky", v); e.dibujos++; return null; }
                case 8: { long t = q.ReadInt64(); int tris = q.ReadInt32(); object v = Vertices(q, tVtx, 28); Invoca(d, "DrawStars", v, tris, Tex(e, t)); e.dibujos++; return null; }
                case 9: { int tris = q.ReadInt32(); object v = Vertices(q, tVtx, 28); Invoca(d, "DrawShootStreaks", v, tris); e.dibujos++; return null; }
                case 10: { int tris = q.ReadInt32(); object v = Vertices(q, tVtx, 28); Invoca(d, "DrawMesh", v, tris); e.dibujos++; return null; }
                case 11: { int tris = q.ReadInt32(); object v = Vertices(q, tVtx, 28); Invoca(d, "DrawMeshAdd", v, tris); e.dibujos++; return null; }
                case 12: { long t = q.ReadInt64(); object v = Vertices(q, tVtx, 28); Invoca(d, "DrawTexQuad", Tex(e, t), v); e.dibujos++; return null; }
                case 13: { long t = q.ReadInt64(); object v = Vertices(q, tVtx, 28); Invoca(d, "DrawOpaqueQuad", Tex(e, t), v); e.dibujos++; return null; }
                case 14: { long t = q.ReadInt64(); int tris = q.ReadInt32(); object v = Vertices(q, tVtx, 28); Invoca(d, "DrawQuads", Tex(e, t), v, tris); e.dibujos++; return null; }
                case 15: { long t = q.ReadInt64(); int tris = q.ReadInt32(); object v = Vertices(q, tVtx, 28); Invoca(d, "DrawQuadsWrap", Tex(e, t), v, tris); e.dibujos++; return null; }
                case 24: { int tris = q.ReadInt32(); object v = Vertices(q, tVtx, 28); Invoca(d, "DibujaMedia", v, tris); e.dibujos++; return null; }
                case 25: { int tris = q.ReadInt32(); object v = Vertices(q, tVtx, 28); Invoca(d, "DrawSkyN", v, tris); e.dibujos++; return null; }
                case 16:
                    {
                        long t = q.ReadInt64(); float um = q.ReadSingle(), vm = q.ReadSingle(), ax = q.ReadSingle(), ay = q.ReadSingle(), aw = q.ReadSingle(), ah = q.ReadSingle();
                        double ang = q.ReadDouble(); float alfa = q.ReadSingle(); double incl = q.ReadDouble();
                        Invoca(d, "DrawCube", Tex(e, t), um, vm, ax, ay, aw, ah, ang, alfa, incl); e.dibujos++;
                        return null;
                    }
                default: throw new Exception("metodo desconocido " + metodo);
            }
        }
        static string F(float f) { return f.ToString("R", CultureInfo.InvariantCulture); }
        static void Desvia(Estado e, bool grabado, bool ahora) { if (grabado != ahora) e.desviaciones++; }
        static void Desvia(Estado e, float grabado, float ahora) { if (grabado != ahora) e.desviaciones++; }

        static void Cuadro(Estado e, object d, StreamWriter o, HashSet<int> vuelca, string volcado, bool sinAvisar, bool sinEnsuciar)
        {
            bool fin = (bool)Invoca(d, "EndFrame");
            e.cuadros++;
            StringBuilder errores = new StringBuilder();
            for (int i = 0; i < 16; i++) { uint g = glGetError(); if (g == 0) break; errores.Append(g.ToString("x")).Append(' '); e.erroresGl++; }
            int w, h; byte[] px;
            if (mLeeFondoNat != null)
            {
                object[] a = { 0, 0 };
                px = (byte[])mLeeFondoNat.Invoke(d, a); w = (int)a[0]; h = (int)a[1];
            }
            else
            {
                object[] a = { fDev.GetValue(d), 0, 0 };
                px = (byte[])mLeeFondoRef.Invoke(null, a); w = (int)a[1]; h = (int)a[2];
            }
            string sha = px == null ? "-" : Convert.ToHexString(SHA256.HashData(px)).Substring(0, 32);
            e.distintos.Add(sha);
            o.WriteLine("cuadro " + e.cuadros + " fin=" + fin + " " + w + "x" + h + " sha=" + sha + " pedidos=" + fPedidos.GetValue(d) + " envios=" + fEnvios.GetValue(d)
                + " cambios=" + fCambios.GetValue(d) + " perdidas=" + fPerdidas.GetValue(d) + " perdido=" + fLost.GetValue(d) + (errores.Length > 0 ? " errores-gl=" + errores.ToString().Trim() : ""));
            if (px != null && vuelca.Contains(e.cuadros))
            {
                File.WriteAllBytes(Path.Combine(volcado, "cuadro-" + e.cuadros + ".bgra"), px);
                File.WriteAllText(Path.Combine(volcado, "cuadro-" + e.cuadros + ".medida"), w + "x" + h);
                using (SKBitmap b = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque)))
                {
                    Marshal.Copy(px, 0, b.GetPixels(), px.Length);
                    using (SKData png = b.Encode(SKEncodedImageFormat.Png, 100)) using (FileStream fs = File.Create(Path.Combine(volcado, "cuadro-" + e.cuadros + ".png"))) png.SaveTo(fs);
                }
            }
            if (!sinEnsuciar)
            {
                Ensucia();
                if (!sinAvisar) mTocado.Invoke(null, null);
            }
        }

        // ---------------- comparar volcados punto a punto ----------------
        static int Compara(string a, string b)
        {
            int distintos = 0;
            foreach (string fa in Directory.GetFiles(a, "cuadro-*.bgra"))
            {
                string fb = Path.Combine(b, Path.GetFileName(fa));
                if (!File.Exists(fb)) { Console.WriteLine(Path.GetFileName(fa) + ": falta en " + b); continue; }
                byte[] x = File.ReadAllBytes(fa), y = File.ReadAllBytes(fb);
                string[] m = File.ReadAllText(Path.ChangeExtension(fa, ".medida")).Split('x');
                int w = int.Parse(m[0]);
                if (x.Length != y.Length) { Console.WriteLine(Path.GetFileName(fa) + ": otra medida"); distintos++; continue; }
                int dif = 0, max = 0, primero = -1;
                for (int i = 0; i < x.Length; i++)
                {
                    int dd = Math.Abs(x[i] - y[i]);
                    if (dd != 0) { dif++; if (primero < 0) primero = i; if (dd > max) max = dd; }
                }
                Console.WriteLine(Path.GetFileName(fa) + ": " + (dif == 0 ? "0 bytes distintos (" + x.Length + " bytes)" : dif + " bytes distintos de " + x.Length + ", el mayor " + max + ", el primero en (" + (primero / 4 % w) + "," + (primero / 4 / w) + ") canal " + (primero % 4)));
                if (dif != 0) distintos++;
            }
            return distintos;
        }
    }
}
