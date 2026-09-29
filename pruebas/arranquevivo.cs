using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// EL ARRANQUE NO DEJA LA VENTANA PARADA ESPERANDO LAS TABLAS (Form1.cs: EsperaLasTablas, SigueArranque; Form1.Datos.cs:
// CreateStartupLoad, CompleteStartupDataLoad).
//
// Las tablas del arranque (volumen, EQ, lo mas sonado...) las lee un hilo aparte. Antes la ventana, al mostrarse, se quedaba
// PARADA esperandolas: con 40.000 canciones en las tablas, de 0,8 a 1,1 s sin pintar ni atender nada en este servidor. Ahora
// sigue viva y lo que necesita las tablas sigue cuando el lector avisa, en el mismo orden; las teclas de mientras esperan su
// turno para que una moneda no se cuente antes de leer las monedas guardadas. Aqui, con la rocola de verdad:
//   1) con el lector aun leyendo, mostrarse no espera: deja apuntado que seguir y vuelve;
//   2) publicar las tablas ya no espera nunca (con la rocola de antes se queda colgada esperando);
//   3) el lector avisa al terminar, una vez, y fuera de su candado;
//   4) las teclas de mientras esperan y se atienden despues, en orden; salir sigue funcionando;
//   5) el orden de siempre: tablas, lo mas sonado, la biblioteca; y cerrar mientras tanto no sigue con nada.
class ArranqueVivo
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static object Get(object o, string n) { for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo fi = t.GetField(n, T | BindingFlags.DeclaredOnly); if (fi != null) return fi.GetValue(o); } throw new MissingFieldException(n); }
    static void Set(object o, string n, object v) { for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo fi = t.GetField(n, T | BindingFlags.DeclaredOnly); if (fi != null) { fi.SetValue(o, v); return; } } throw new MissingFieldException(n); }
    static object Call(object o, string n, params object[] a) { MethodInfo m = TF.GetMethod(n, T); if (m == null) throw new MissingMethodException(n); try { return m.Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
    [DllImport("libc", SetLastError = true)] static extern int mkfifo(string ruta, uint modo);

    // Correr algo en otro hilo con tiempo tope: la rocola de antes se queda esperando y no debe colgar la bateria.
    static bool ConTope(int ms, ThreadStart q, out double tardo)
    {
        Stopwatch sw = Stopwatch.StartNew();
        Thread t = new Thread(q); t.IsBackground = true; t.Start();
        bool acabo = t.Join(ms); tardo = sw.Elapsed.TotalMilliseconds; return acabo;
    }

    static int Main(string[] args)
    {
        TF = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe").GetType("Rocola.Form1", true);
        Type TL = TF.GetNestedType("StartupLoad", T);
        string dir = Path.Combine(Path.GetTempPath(), "rocola_arranquevivo_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        try
        {
            Console.WriteLine("\n  1) CON EL LECTOR AUN LEYENDO, MOSTRARSE NO ESPERA\n");
            object f = Codigo.FormSinEncender(TF);
            Set(f, "startupReadPending", true);
            object carga = Activator.CreateInstance(TL, true);   // un lector que todavia no termino
            Set(f, "startupLoad", carga);
            double ms; bool volvio;
            bool fallo1 = false;
            volvio = ConTope(3000, delegate { try { Call(f, "EsperaLasTablas"); } catch (Exception e) { fallo1 = true; Console.WriteLine("        " + e.GetType().Name + ": " + e.Message); } }, out ms);
            Check(volvio && !fallo1 && ms < 500, "con las tablas sin leer, la ventana sigue (" + (volvio ? ms.ToString("0") + " ms" : "se quedo esperando") + ")");
            bool enEspera = false; object aviso = null;
            try { enEspera = (bool)Get(f, "arranqueEnEspera"); aviso = Get(carga, "AlTerminar"); } catch { }
            Check(enEspera && aviso != null, "y deja apuntado que seguir cuando el lector avise");
            Check((bool)Get(f, "startupReadPending"), "sin haber publicado nada todavia (no se guarda nada con tablas vacias)");

            Console.WriteLine("\n  2) PUBLICAR LAS TABLAS NO ESPERA NUNCA\n");
            object g = Codigo.FormSinEncender(TF);
            Set(g, "startupReadPending", true);
            Set(g, "startupLoad", Activator.CreateInstance(TL, true));
            object prep = null;
            volvio = ConTope(3000, delegate { try { prep = Call(g, "CompleteStartupDataLoad"); } catch { } }, out ms);
            Check(volvio && prep is bool && !(bool)prep, "si el lector aun no termino, no se le espera: se lee por el camino de siempre (" + (volvio ? ms.ToString("0") + " ms" : "se quedo esperando") + ")");
            Check(!Codigo.Llama(TF.GetMethod("CompleteStartupDataLoad", T), "Wait") && !Codigo.Llama(TF.GetMethod("OnShown", T), "CompleteStartupDataLoad"),
                  "ni publicar espera al lector, ni mostrarse publica por su cuenta");

            Console.WriteLine("\n  3) EL LECTOR AVISA AL TERMINAR\n");
            string fifo = Path.Combine(dir, "tabla_lenta");
            bool hayFifo = false;
            try { hayFifo = mkfifo(fifo, Convert.ToUInt32("600", 8)) == 0; } catch { }
            if (!hayFifo) Console.WriteLine("        (sin mkfifo en este sistema: se salta la parte del lector lento)");
            else
            {
                // La primera tabla es una tuberia: el lector se queda leyendo hasta que se le escriba y se cierre.
                string[] rutas = { fifo, Path.Combine(dir, "no1"), Path.Combine(dir, "no2"), Path.Combine(dir, "no3"),
                                   Path.Combine(dir, "no4"), Path.Combine(dir, "no5"), Path.Combine(dir, "no6"), Path.Combine(dir, "no7") };
                object lento = TF.GetMethod("CreateStartupLoad", T).Invoke(null, new object[] { rutas });
                int avisos = 0; bool dentroDelCandado = true; int hilo = -1;
                FieldInfo fa = TL.GetField("AlTerminar", T);
                Thread.Sleep(100);
                lock (lento)
                {
                    Check(!(bool)Get(lento, "Done"), "el lector sigue leyendo (la tabla lenta aun no llego)");
                    if (fa != null)
                        fa.SetValue(lento, (Action)delegate
                        {
                            avisos++; hilo = Thread.CurrentThread.ManagedThreadId;
                            // Si el aviso llegara con el candado tomado, otro hilo no podria entrar: se prueba desde fuera.
                            Thread otro = new Thread(delegate () { lock (lento) { } }); otro.Start(); dentroDelCandado = !otro.Join(1000);
                        });
                }
                // Abrir la tuberia para escribir suelta al lector. Si el lector ya la cerro (no admite ese tipo de archivo y lo
                // da por tabla ausente), escribir o cerrar puede fallar: da igual, lo que se mira es que termine y avise.
                try
                {
                    using (FileStream w = new FileStream(fifo, FileMode.Open, FileAccess.Write))
                    { byte[] b = System.Text.Encoding.UTF8.GetBytes("0.12|200|D:\\m\\a.mp3\r\n"); w.Write(b, 0, b.Length); }
                }
                catch (IOException) { }
                for (int i = 0; i < 300 && !(bool)Get(lento, "Done"); i++) Thread.Sleep(10);
                Thread.Sleep(100);
                Check((bool)Get(lento, "Done") && avisos == 1, "al terminar, el lector avisa una vez (" + avisos + ")");
                Check(avisos == 1 && !dentroDelCandado && hilo != Thread.CurrentThread.ManagedThreadId, "desde su hilo y ya fuera de su candado");
                Check(fa != null && fa.GetValue(lento) == null, "y el aviso no se queda colgado del lector");
            }

            Console.WriteLine("\n  4) LAS TECLAS DE MIENTRAS ESPERAN SU TURNO\n");
            object k = Codigo.FormSinEncender(TF);
            Set(k, "arranqueEnEspera", true);
            DateTime antes = new DateTime(2001, 1, 1);
            Set(k, "lastInput", antes);
            MethodInfo pck = TF.GetMethod("ProcessCmdKey", T);
            Message m = Message.Create(IntPtr.Zero, 0x100, (IntPtr)(int)Keys.F24, IntPtr.Zero);
            object[] pa = { m, Keys.F24 };
            bool atendida = false;
            try { atendida = (bool)pck.Invoke(k, pa); } catch (Exception e) { Console.WriteLine("        " + (e.InnerException ?? e).GetType().Name); }
            IList cola = null; try { cola = Get(k, "teclasEnEspera") as IList; } catch { }
            Check(atendida && (DateTime)Get(k, "lastInput") == antes && cola != null && cola.Count == 1,
                  "una tecla mientras se leen las tablas se guarda, sin hacer nada todavia");
            Check(Codigo.Llama(pck, "PidenSalir") && Codigo.Nombres(pck).IndexOf("PidenSalir") < Codigo.Nombres(pck).IndexOf("AddCoin"),
                  "salir (Ctrl+Alt+Q) se mira antes: funciona siempre, tambien mientras tanto");
            Set(k, "arranqueEnEspera", false);
            try { Call(k, "AtiendeTeclasEnEspera"); } catch (Exception e) { Console.WriteLine("        (al atender: " + e.GetType().Name + ")"); }
            Check((DateTime)Get(k, "lastInput") != antes && Get(k, "teclasEnEspera") == null, "al terminar se atiende, y la espera queda vacia");
            List<string> sigue = Codigo.Nombres(TF.GetMethod("SigueArranque", T));
            int iAt = sigue.LastIndexOf("AtiendeTeclasEnEspera");
            Check(iAt >= 0 && iAt > sigue.IndexOf("LoadMusicFolder") && iAt > sigue.IndexOf("StartConfig"),
                  "y se atienden al final del arranque, con todo ya en su sitio");
            object tic = Codigo.FormSinEncender(TF);
            Set(tic, "arranqueEnEspera", true);
            double fase = (double)Get(tic, "phase");
            try { Call(tic, "AnimTimer_Tick", null, EventArgs.Empty); } catch (Exception e) { Console.WriteLine("        (latido: " + e.GetType().Name + ")"); }
            Check((double)Get(tic, "phase") == fase, "mientras tanto el latido no anima nada: ese procesador es del lector");

            Console.WriteLine("\n  5) EL ORDEN DE SIEMPRE, Y CERRAR MIENTRAS TANTO\n");
            int iT = sigue.IndexOf("CompleteStartupDataLoad"), iS = sigue.IndexOf("LoadStats"), iB = sigue.IndexOf("LoadMusicFolder");
            Check(iT >= 0 && iT < iS && iS < iB, "tablas, despues lo mas sonado (si hace falta) y despues la biblioteca, que es la que restaura creditos y cola");
            List<string> muestra = Codigo.Nombres(TF.GetMethod("OnShown", T));
            Check(muestra.Contains("EsperaLasTablas") && muestra.Contains("LoadConfigPrefs") && muestra.IndexOf("LoadConfigPrefs") < muestra.IndexOf("EsperaLasTablas"),
                  "al mostrarse: la configuracion y el vigia del foco, y despues se espera a las tablas sin parar");
            object c = Codigo.FormSinEncender(TF);
            Set(c, "startupReadPending", true); Set(c, "startupClosing", true);
            try { Call(c, "SigueArranque"); } catch (Exception e) { Console.WriteLine("        (" + e.GetType().Name + ")"); }
            Check((bool)Get(c, "startupReadPending") && (bool)Get(c, "arranqueSeguido"), "si se cerro mientras se leian, no sigue con nada (ni publica ni guarda)");
            object d = Codigo.FormSinEncender(TF);
            object cargaD = Activator.CreateInstance(TL, true);
            Set(d, "arranqueSeguido", true); Set(d, "startupLoad", cargaD);
            try { Call(d, "SigueArranque"); } catch { }
            Check(ReferenceEquals(Get(d, "startupLoad"), cargaD), "y el arranque sigue una sola vez: la segunda no hace nada");
        }
        catch (Exception e) { Check(false, "sin excepciones: " + (e.InnerException ?? e)); }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}
