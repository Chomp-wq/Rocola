using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
// UNA MONEDA ANTES DE RESTAURAR EL ESTADO (Form1.Datos.cs, SaveState / EstadoPorRestaurar).
//
// Tras un apagon, los creditos y la cola se restauran cuando la biblioteca ya esta armada (o cuando vuelve el disco que
// faltaba), pero la moneda funciona desde el primer instante. Una moneda en ese rato guardaba el estado de AHORA encima
// del de antes, y al restaurar se perdian los creditos y la cola que los clientes ya habian pagado. Aqui, con la rocola
// de verdad y su archivo de estado:
//   1) antes de restaurar, guardar conserva lo de antes y le suma lo de ahora (creditos sumados, la cola de antes
//      delante y la de ahora detras, la hora de antes); dos guardados no cuentan dos veces lo de antes;
//   2) lo que se restaura de ese archivo: los creditos de antes mas los de ahora;
//   3) lo de otro dia del negocio no se conserva (se descartaria igual al restaurar);
//   4) ya restaurado, se guarda solo lo de ahora, como siempre; y la moneda sigue guardando al momento.
class EstadoPendiente
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static object Get(object o, string n) { return TF.GetField(n, T).GetValue(o); }
    static void Set(object o, string n, object v) { TF.GetField(n, T).SetValue(o, v); }
    static object Call(object o, string n, params object[] a) { try { return TF.GetMethod(n, T).Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static string Lee(string p) { return File.Exists(p) ? File.ReadAllText(p) : ""; }
    static string Linea(string texto, string clave) { foreach (string l in texto.Split('\n')) if (l.TrimEnd('\r').StartsWith(clave)) return l.TrimEnd('\r'); return null; }
    static int Cuenta(string texto, string clave) { int n = 0; foreach (string l in texto.Split('\n')) if (l.StartsWith(clave)) n++; return n; }

    static object Nueva()
    {
        object f = Codigo.FormSinEncender(TF);
        Set(f, "startupReadPending", false);
        return f;
    }
    static object Item(string ruta)
    {
        Type tt = TF.GetNestedType("Track", T); object track = Activator.CreateInstance(tt, true);
        tt.GetField("Path", T).SetValue(track, ruta);
        Type tp = TF.GetField("nowPlaying", T).FieldType; object it = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(tp);
        tp.GetField("Song", T).SetValue(it, track);
        return it;
    }

    static int Main(string[] args)
    {
        TF = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe").GetType("Rocola.Form1", true);
        string dir = Path.Combine(Path.GetTempPath(), "rocola_pendiente_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        FieldInfo carpeta = TF.GetField("carpetaDatos", T);
        object previa = carpeta.GetValue(null);
        try
        {
            carpeta.SetValue(null, dir);
            string est = Path.Combine(dir, "rocola_estado.txt");
            long ts = DateTime.Now.AddMinutes(-3).Ticks;
            string antes = "ROCOLA-ESTADO v1\r\nts=" + ts + "\r\ncreditos=7\r\nsonando=S|D:\\m\\a.mp3\r\ncola=S|D:\\m\\b.mp3\r\n";

            Console.WriteLine("\n  1) UNA MONEDA ANTES DE RESTAURAR (tras un apagon)\n");
            File.WriteAllText(est, antes);
            object f = Nueva();
            Check(!(bool)Get(f, "stateRestored"), "al arrancar, el estado aun no se restauro (espera a la biblioteca)");
            Set(f, "credits", 4);   // la moneda (AddCoin suma y guarda)
            Call(f, "SaveState");
            string t = Lee(est);
            Check(Linea(t, "creditos=") == "creditos=11", "los creditos de antes (7) mas los de la moneda (4): " + Linea(t, "creditos="));
            Check(t.Contains("sonando=S|D:\\m\\a.mp3") && t.Contains("cola=S|D:\\m\\b.mp3"), "la cancion y la cola de antes siguen ahi");
            Check(Linea(t, "ts=") == "ts=" + ts, "con la hora de antes (la regla de las 7 AM sigue mirando lo de antes)");
            // Se gasta uno en una cancion (la parte de la biblioteca que si esta) y se vuelve a guardar.
            Set(f, "credits", 3);
            IList cola = (IList)Get(f, "queue"); cola.Add(Item("E:\\otra\\c.mp3"));
            Call(f, "SaveState");
            t = Lee(est);
            Check(Linea(t, "creditos=") == "creditos=10", "otro guardado no cuenta dos veces lo de antes: 7 + 3 = " + Linea(t, "creditos="));
            Check(Cuenta(t, "sonando=") == 1 && t.IndexOf("D:\\m\\b.mp3") < t.IndexOf("E:\\otra\\c.mp3"), "la cola de ahora va detras de la de antes, y sigue habiendo una sola que sonaba");

            Console.WriteLine("\n  2) LO QUE SE RESTAURA DE ESE ARCHIVO\n");
            // RestoreState lee "creditos=" de este archivo: son los de antes mas los de ahora.
            int cr = int.Parse(Linea(t, "creditos=").Substring(9));
            Check(cr == 7 + 3, "al restaurar salen los 7 de antes mas los 3 que quedan de la moneda");

            Console.WriteLine("\n  3) LO DE OTRO DIA NO SE CONSERVA\n");
            File.WriteAllText(est, "ROCOLA-ESTADO v1\r\nts=" + DateTime.Now.AddDays(-2).Ticks + "\r\ncreditos=9\r\ncola=S|D:\\m\\z.mp3\r\n");
            object g = Nueva();
            Set(g, "credits", 4);
            Call(g, "SaveState");
            t = Lee(est);
            Check(Linea(t, "creditos=") == "creditos=4" && !t.Contains("z.mp3"), "un estado de hace dos dias se descartaria al restaurar: se guarda solo lo de ahora");

            Console.WriteLine("\n  4) YA RESTAURADO, COMO SIEMPRE\n");
            File.WriteAllText(est, antes);
            object h = Nueva();
            Set(h, "stateRestored", true);
            Set(h, "credits", 2);
            Call(h, "SaveState");
            t = Lee(est);
            Check(Linea(t, "creditos=") == "creditos=2" && !t.Contains("a.mp3"), "con el estado ya restaurado se guarda solo lo de ahora");
            File.Delete(est); File.Delete(est + ".bak");
            object k = Nueva();
            Set(k, "credits", 4);
            Call(k, "SaveState");
            t = Lee(est);
            Check(Linea(t, "creditos=") == "creditos=4", "sin nada guardado de antes, lo de ahora tal cual");
            Check(Codigo.Llama(TF.GetMethod("AddCoin", T), "SaveState") && Codigo.Llama(TF.GetMethod("SaveState", T), "EstadoPorRestaurar"),
                  "la moneda guarda al momento, y el guardado mira si hay algo de antes por restaurar");
        }
        catch (Exception e) { Check(false, "sin excepciones: " + (e.InnerException ?? e)); }
        finally { carpeta.SetValue(null, previa); try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}
