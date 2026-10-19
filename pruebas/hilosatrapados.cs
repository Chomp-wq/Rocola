using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
// HILOS DE FONDO QUE NO MATAN LA ROCOLA (19-10-2026): Form1.Portadas.cs (CoverPump) y Form1.Biblioteca.cs (ClavesDelIndice).
//
// En CLR 2 (.NET 3.5, Windows XP) una excepcion sin atrapar en cualquier hilo cierra el programa entero. Estos dos sitios tenian hilos de fondo sin un try
// de punta a punta:
//   - CoverPump (el lector de caratulas): lo que se escapara de sus try de dentro mataba el hilo y dejaba subido coverPumpCount, asi que no se armaba otro
//     lector; ahora se apunta, se baja el contador, la caratula en curso queda para reintentar en 30 s y el hilo termina sin cerrar nada.
//   - ClavesDelIndice (las claves del buscador repartidas entre los nucleos): un ayudante que fallara cerraba la rocola; ahora se atrapa y su tramo se rehace en el
//     hilo que reparte (que ya tiene su manejo de errores), con las demas claves hechas.
// Se provoca el fallo de verdad (un formulario sin armar al que le faltan sus colas, una entrada nula en el indice) y se mira que el proceso sigue vivo.
class HilosAtrapados
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }

    static int Main(string[] args)
    {
        TF = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe").GetType("Rocola.Form1", true);
        AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Console.WriteLine("  FALLO una excepcion sin atrapar en un hilo: " + e.ExceptionObject); Environment.Exit(3); };

        // ---------- CoverPump ----------
        MethodInfo pump = TF.GetMethod("CoverPump", T);
        Check(pump != null && pump.GetMethodBody().ExceptionHandlingClauses.Count >= 2, "CoverPump tiene un try/catch/finally de punta a punta");
        int lee, escribe;
        Codigo.UsosDeCampo(pump, "coverPumpCount", out lee, out escribe);
        Check(escribe >= 2, "y baja el contador de lectores por las dos salidas (la normal y el fallo)");
        object f = FormatterServices.GetUninitializedObject(TF);
        TF.GetField("coverLock", T).SetValue(f, new object());
        TF.GetField("coverPumpCount", T).SetValue(f, 1);
        // las colas sin armar: TakeCoverJob(null) lanza NullReferenceException dentro del hilo, fuera de los try de dentro
        TF.GetField("coverQHi", T).SetValue(f, null);
        System.Threading.Thread hilo = new System.Threading.Thread((System.Threading.ThreadStart)Delegate.CreateDelegate(typeof(System.Threading.ThreadStart), f, pump));
        hilo.IsBackground = true;
        hilo.Start();
        bool acabo = hilo.Join(10000);
        Check(acabo, "con un fallo inesperado dentro, el hilo termina por su cuenta (sin excepcion sin atrapar: el proceso sigue vivo)");
        Check((int)TF.GetField("coverPumpCount", T).GetValue(f) == 0, "y el contador de lectores baja de 1 a 0 (se podra armar otro)");

        // ---------- ClavesDelIndice ----------
        Type tCarga = TF.Assembly.GetType("Rocola.CargaTrabajo", true);
        FieldInfo fp = tCarga.GetField("Procesadores", T);
        object antes = fp.GetValue(null);
        try { fp.SetValue(null, 4); } catch { }
        bool conAyudantes = (int)fp.GetValue(null) >= 4;
        Type tItem = TF.GetNestedType("SearchItem", T);
        IList indice = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(tItem));
        for (int i = 0; i < 9000; i++)
        {
            object it = Activator.CreateInstance(tItem, true);
            tItem.GetField("Kind").SetValue(it, 0); tItem.GetField("Display").SetValue(it, "Artista Numero " + i);
            indice.Add(it);
        }
        indice[8500] = null;   // cae en un tramo de ayudante: lanza NullReferenceException
        object g = FormatterServices.GetUninitializedObject(TF);
        TF.GetField("searchIndex", T).SetValue(g, indice);
        string fallo = null;
        try { TF.GetMethod("ClavesDelIndice", T).Invoke(g, null); }
        catch (TargetInvocationException e) { fallo = e.InnerException.GetType().Name; }
        int hechas = 0;
        for (int i = 0; i < indice.Count; i++) if (indice[i] != null && tItem.GetField("Key").GetValue(indice[i]) != null) hechas++;
        if (conAyudantes)
        {
            Check(fallo == "NullReferenceException", "una entrada nula en el tramo de un ayudante: el fallo llega al hilo que reparte (no cierra el proceso) -> " + fallo);
            Check(hechas >= 6750, "y las claves de los tramos que no fallaron quedaron hechas (" + hechas + " de 9000; el tramo con la entrada nula se queda en ella, como siempre)");
        }
        else Console.WriteLine("  SE SALTA la prueba de ayudantes: aqui la rocola no reparte (procesadores = " + fp.GetValue(null) + ").");
        try { fp.SetValue(null, antes); } catch { }
        MethodInfo claves = TF.GetMethod("ClavesDelIndice", T);
        Check(claves != null, "ClavesDelIndice existe");
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}
