// PRUEBA DE LAS PIEZAS NATIVAS DE LA TARJETA (pruebas del port nativo): TexturasGl, MemoriaTarjeta y SombreadoresTarjeta solas, sin el
// dispositivo, en el rocola-xp.dll traducido (por reflexion; OpenGL fuera de pantalla con llvmpipe).
//   reproductor-cintas --piezas DIR
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PruebasTarjeta
{
    internal static unsafe class Piezas
    {
        const BindingFlags Todo = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        static int fallos;
        static void Mira(bool ok, string que) { Console.WriteLine((ok ? "  bien  " : "  MAL   ") + que); if (!ok) fallos++; }
        const string L = "libGLESv2.so.2";
        [DllImport(L)] static extern void glGenFramebuffers(int n, uint* f);
        [DllImport(L)] static extern void glBindFramebuffer(uint d, uint f);
        [DllImport(L)] static extern void glFramebufferTexture2D(uint d, uint a, uint t, uint tex, int nivel);
        [DllImport(L)] static extern void glReadPixels(int x, int y, int w, int h, uint f, uint t, void* p);
        [DllImport(L)] static extern void glGetIntegerv(uint p, int* v);
        [DllImport(L)] static extern void glGetBooleanv(uint p, byte* v);
        [DllImport(L)] static extern void glColorMask(byte r, byte g, byte b, byte a);
        [DllImport(L)] static extern void glActiveTexture(uint u);
        [DllImport(L)] static extern void glGetTexParameteriv(uint d, uint p, int* v);
        [DllImport(L)] static extern void glBindTexture(uint d, uint t);
        [DllImport(L)] static extern uint glGetError();
        const uint FRAMEBUFFER = 0x8D40, COLOR_ATTACHMENT0 = 0x8CE0, TEXTURE_2D = 0x0DE1, RGBA = 0x1908, UNSIGNED_BYTE = 0x1401, FRAMEBUFFER_BINDING = 0x8CA6,
            COLOR_WRITEMASK = 0x0C23, ACTIVE_TEXTURE = 0x84E0, TEXTURE0 = 0x84C0, TEXTURE_WRAP_S = 0x2802, TEXTURE_MIN_FILTER = 0x2801, REPEAT = 0x2901, CLAMP_TO_EDGE = 0x812F, LINEAR = 0x2601;

        static byte[] Lee(uint tex, int w, int h)
        {
            int antes; glGetIntegerv(FRAMEBUFFER_BINDING, &antes);
            uint f; glGenFramebuffers(1, &f); glBindFramebuffer(FRAMEBUFFER, f);
            glFramebufferTexture2D(FRAMEBUFFER, COLOR_ATTACHMENT0, TEXTURE_2D, tex, 0);
            byte[] px = new byte[w * h * 4];
            fixed (byte* p = px) glReadPixels(0, 0, w, h, RGBA, UNSIGNED_BYTE, p);
            glBindFramebuffer(FRAMEBUFFER, (uint)antes);
            return px;
        }

        internal static int Prueba(string dir)
        {
            Environment.SetEnvironmentVariable("ROCOLA_PANTALLA", "fuera:1024x768");
            AppDomain.CurrentDomain.AssemblyResolve += delegate (object s, ResolveEventArgs e)
            {
                string p = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(p) ? Assembly.LoadFrom(p) : null;
            };
            Assembly asm = Assembly.LoadFrom(Path.Combine(dir, "rocola-xp.dll"));
            RuntimeHelpers.RunModuleConstructor(asm.ManifestModule.ModuleHandle);
            Type tt = asm.GetType("Rocola.TexturasGl"), tm = asm.GetType("Rocola.MemoriaTarjeta"), ts = asm.GetType("Rocola.SombreadoresTarjeta");
            Func<string, object[], object> T = (n, a) => { foreach (MethodInfo m in tt.GetMethods(Todo)) if (m.Name == n && m.GetParameters().Length == a.Length) return m.Invoke(null, a); throw new MissingMethodException(n); };
            Console.WriteLine("Piezas de " + asm.Location);
            uint libre0 = (uint)tm.GetMethod("Libre", Todo).Invoke(null, null);
            long bytes0 = (long)tt.GetProperty("Bytes", Todo).GetValue(null);
            // una textura nueva: LINEAR, CLAMP, su medida; la unidad activa queda en la 0
            glActiveTexture(TEXTURE0);
            uint t = (uint)T("Nueva", new object[] { 300, 200 });
            int u; glGetIntegerv(ACTIVE_TEXTURE, &u);
            Mira(t != 0, "Nueva(300, 200) da una textura (" + t + ")");
            Mira(u == (int)TEXTURE0, "la unidad activa vuelve a la 0");
            object[] med = { t, 0, 0 };
            Mira((bool)T("Medida", med) && (int)med[1] == 300 && (int)med[2] == 200, "Medida 300x200");
            Mira((long)tt.GetProperty("Bytes", Todo).GetValue(null) - bytes0 == 300 * 200 * 4, "Bytes sube 240000");
            glBindTexture(TEXTURE_2D, t);
            int fmin, ws; glGetTexParameteriv(TEXTURE_2D, TEXTURE_MIN_FILTER, &fmin); glGetTexParameteriv(TEXTURE_2D, TEXTURE_WRAP_S, &ws);
            Mira(fmin == (int)LINEAR && ws == (int)CLAMP_TO_EDGE, "LINEAR y CLAMP al crearla");
            // subir por la unidad 2 y leer lo mismo (B, G, R, A tal cual)
            Random z = new Random(7);
            byte[] datos = new byte[300 * 200 * 4]; z.NextBytes(datos);
            bool propia; IntPtr m = (IntPtr)tt.GetMethod("Memoria", Todo).Invoke(null, new object[] { (long)datos.Length, null });
            Marshal.Copy(datos, 0, m, datos.Length);
            glBindTexture(TEXTURE_2D, 0);
            Mira((bool)T("Sube", new object[] { t, 0, 0, 300, 200, m }), "Sube 300x200");
            int b0; glGetIntegerv(0x8069, &b0);
            Mira(b0 == 0, "la textura puesta en la unidad 0 no cambia al subir");
            tt.GetMethod("SueltaMemoria", Todo).Invoke(null, new object[] { m, false });
            byte[] leido = Lee(t, 300, 200);
            int dist = 0; for (int i = 0; i < datos.Length; i++) if (datos[i] != leido[i]) dist++;
            Mira(dist == 0, "lo leido es lo subido (" + dist + " bytes distintos)");
            Mira(!(bool)T("Sube", new object[] { t, 250, 150, 60, 60, IntPtr.Zero }), "Sube fuera de la textura: no (como LockRect)");
            Mira(!(bool)T("Sube", new object[] { 999999u, 0, 0, 1, 1, IntPtr.Zero }), "Sube a una que no es de la tarjeta: no");
            // la envoltura, solo si cambia
            glBindTexture(TEXTURE_2D, t);
            T("Envoltura", new object[] { t, true });
            glGetTexParameteriv(TEXTURE_2D, TEXTURE_WRAP_S, &ws);
            Mira(ws == (int)REPEAT, "Envoltura REPEAT");
            T("Envoltura", new object[] { t, false });
            glGetTexParameteriv(TEXTURE_2D, TEXTURE_WRAP_S, &ws);
            Mira(ws == (int)CLAMP_TO_EDGE, "Envoltura CLAMP");
            // el framebuffer de una X8: (0, 0, 0, 1) y deja el framebuffer y la mascara como estaban
            uint x8 = (uint)T("Nueva", new object[] { 64, 32 });
            int fAntes; glGetIntegerv(FRAMEBUFFER_BINDING, &fAntes);
            glColorMask(1, 1, 1, 0);
            uint fb = (uint)T("Framebuffer", new object[] { x8, true });
            int fDespues; glGetIntegerv(FRAMEBUFFER_BINDING, &fDespues);
            byte* mk = stackalloc byte[4]; glGetBooleanv(COLOR_WRITEMASK, mk);
            Mira(fb != 0, "Framebuffer de la X8");
            Mira(fDespues == fAntes, "deja puesto el framebuffer de antes");
            Mira(mk[0] == 1 && mk[1] == 1 && mk[2] == 1 && mk[3] == 0, "deja la mascara de antes (1,1,1,0)");
            byte[] negro = Lee(x8, 64, 32);
            bool todoNegro = true; for (int i = 0; i < negro.Length; i += 4) if (negro[i] != 0 || negro[i + 1] != 0 || negro[i + 2] != 0 || negro[i + 3] != 255) todoNegro = false;
            Mira(todoNegro, "la X8 empieza en (0, 0, 0, 1)");
            // la memoria de la tarjeta descuenta las nuestras
            uint libre1 = (uint)tm.GetMethod("Libre", Todo).Invoke(null, null);
            Mira(libre0 >= libre1 && libre0 - libre1 <= (300 * 200 * 4 + 64 * 32 * 4 + (1 << 20)), "MemoriaTarjeta.Libre baja con las texturas (" + (libre0 >> 20) + " -> " + (libre1 >> 20) + " MB)");
            // borrar avisa a los dispositivos y la quita del registro
            uint avisada = 0;
            EventInfo ev = tt.GetEvent("AlBorrar", Todo);
            Action<uint> a = n => avisada = n;
            ev.GetAddMethod(true).Invoke(null, new object[] { a });
            T("Borra", new object[] { t });
            ev.GetRemoveMethod(true).Invoke(null, new object[] { a });
            Mira(avisada == t, "Borra avisa (AlBorrar)");
            Mira(!(bool)T("Existe", new object[] { t }), "y ya no existe");
            T("BorraFramebuffer", new object[] { fb }); T("Borra", new object[] { x8 });
            Mira((long)tt.GetProperty("Bytes", Todo).GetValue(null) == bytes0, "Bytes vuelve a lo de antes");
            // los programas compilan y enlazan
            object s = ts.GetMethod("Compila", Todo).Invoke(null, null);
            Mira(s != null, "SombreadoresTarjeta.Compila: los tres programas");
            if (s != null) ts.GetMethod("Suelta", Todo).Invoke(s, null);
            uint err = glGetError();
            Mira(err == 0, "sin errores de OpenGL (" + err + ")");
            Console.WriteLine(fallos == 0 ? "TODO BIEN" : fallos + " MAL");
            return fallos;
        }
    }
}
