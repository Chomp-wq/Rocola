using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

// LA BITACORA ESCRIBE DETRAS DE LA PANTALLA (MedidasBitacora.cs: Bitacora; Form1.Diag, Form1.LogError, Program.LogCrash).
//
// rocola_diagnostico.txt (con "diagnostico=1"), rocola_error.txt y rocola_incompatibles.txt se escribian en el hilo que
// apuntaba, muchas veces el de la pantalla: abrir el archivo, mirar cuanto mide y pegarle la linea. En un disco mecanico
// ocupado eso son decenas de milisegundos por linea, y con el diagnostico encendido hay varias por cancion. Ahora la
// linea -con su hora- va a una cola y la escribe un hilo de fondo. Aqui, con la rocola de verdad:
//   1) nadie que apunte escribe el archivo por su cuenta: todos pasan por la bitacora;
//   2) con el archivo ocupado un momento (un antivirus), apuntar vuelve al momento y las lineas NO se pierden: llegan
//      despues, todas y en orden (con la rocola de antes se perdian);
//   3) lo que dice cada archivo no cambia: la hora, el contexto, el sello de version y el tope de tamano;
//   4) un fallo grave (LogCrash) se escribe ya, en el mismo hilo, con lo que hubiera pendiente;
//   5) cuanto cuesta apuntar en la pantalla (medida, no criterio).
class BitacoraDetras
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Set(object o, string n, object v) { TF.GetField(n, T).SetValue(o, v); }
    static object Call(Type t, object o, string n, params object[] a) { try { return t.GetMethod(n, T).Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static string Lee(string p) { try { return File.Exists(p) ? File.ReadAllText(p) : ""; } catch { return ""; } }
    static int Cuenta(string t, string que) { int n = 0, i = 0; while ((i = t.IndexOf(que, i, StringComparison.Ordinal)) >= 0) { n++; i += que.Length; } return n; }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        Type TB = asm.GetType("Rocola.Bitacora", false);
        Type TP = asm.GetType("Rocola.Program", false);
        string dir = Path.Combine(Path.GetTempPath(), "rocola_bitacoradetras_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        FieldInfo carpeta = TF.GetField("carpetaDatos", T);
        object previa = carpeta.GetValue(null);
        try
        {
            carpeta.SetValue(null, dir);
            string diag = Path.Combine(dir, "rocola_diagnostico.txt"), err = Path.Combine(dir, "rocola_error.txt");
            object f = Codigo.FormSinEncender(TF);
            Set(f, "diagOn", true);

            Console.WriteLine("\n  1) NADIE ESCRIBE LA BITACORA POR SU CUENTA\n");
            Check(TB != null && TB.GetMethod("Vacia", T) != null, "hay una bitacora que escribe detras (Bitacora, con Vacia para escribir ya)");
            string[,] sitios = { { "Rocola.Form1", "Diag" }, { "Rocola.Form1", "LogError" }, { "Rocola.Form1", "NoteIncompatible" },
                                 { "Rocola.RellenoMp3", "Apunta" }, { "Rocola.Program", "LogCrash" } };
            for (int i = 0; i < sitios.GetLength(0); i++)
            {
                Type t = asm.GetType(sitios[i, 0], false);
                MethodInfo m = t == null ? null : t.GetMethod(sitios[i, 1], T);
                if (t != null && m == null) { foreach (Type n in t.GetNestedTypes(T)) { m = n.GetMethod(sitios[i, 1], T); if (m != null) break; } }
                List<string> ll = m == null ? new List<string>() : Codigo.Nombres(m);
                Check(m != null && !ll.Contains("AppendAllText") && (ll.Contains("Apunta") || ll.Contains("ApuntaConSello")),
                      sitios[i, 0].Substring(7) + "." + sitios[i, 1] + " deja el apunte a la bitacora y no abre el archivo");
            }
            // El decodificador en coma flotante: DecodificaFlotante.cs en HD, FlotanteNLayer.cs en Modest.
            bool algunDeco = false, decoBien = true;
            Type[] tipos;
            try { tipos = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { tipos = e.Types; }
            foreach (Type t in tipos)
            {
                if (t == null) continue;
                if (t.Namespace != "Rocola" || t == TF) continue;
                MethodInfo m = t.GetMethod("Apunta", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly, null, new Type[] { typeof(string) }, null);
                if (m == null || t.FullName == "Rocola.RellenoMp3" || t.FullName == "Rocola.Bitacora") continue;
                if (!Codigo.Textos(m).Exists(delegate (string s) { return s.Contains("rocola_error.txt"); })) continue;
                algunDeco = true;
                List<string> ll = Codigo.Nombres(m);
                if (ll.Contains("AppendAllText") || !ll.Contains("ApuntaConSello")) decoBien = false;
            }
            Check(algunDeco && decoBien, "y el apunte del decodificador en coma flotante tambien");
            Check(Codigo.Llama(TF.GetMethod("OnFormClosing", T), "Vacia") && TP != null && Codigo.Llama(TP.GetMethod("Main", T), "Vacia"),
                  "al cerrar (y al salir del programa) se escribe lo pendiente");

            Console.WriteLine("\n  2) CON EL ARCHIVO OCUPADO, APUNTAR NO ESPERA Y NO SE PIERDE NADA\n");
            File.WriteAllText(diag, "");
            double peor = 0;
            using (FileStream ocupado = new FileStream(diag, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                for (int i = 0; i < 200; i++)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    Call(TF, f, "Diag", "linea " + i.ToString("000"));
                    sw.Stop();
                    if (sw.Elapsed.TotalMilliseconds > peor) peor = sw.Elapsed.TotalMilliseconds;
                }
                Thread.Sleep(300);   // el antivirus tarda un poco en soltarlo
            }
            Console.WriteLine("        200 lineas con el archivo ocupado: la peor tardo " + peor.ToString("0.00") + " ms en volver");
            Check(peor < 100, "apuntar vuelve al momento aunque el archivo este ocupado");
            Codigo.EsperaBitacora();
            string t2 = Lee(diag);
            int n2 = Cuenta(t2, "  linea ");
            bool enOrden = true; int ultimo = -1;
            foreach (string l in t2.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                int k = l.IndexOf("  linea "); if (k < 0) continue;
                int v = int.Parse(l.Substring(k + 8, 3));
                if (v != ultimo + 1) enOrden = false;
                ultimo = v;
            }
            Console.WriteLine("        al soltarlo quedaron " + n2 + " de 200 lineas");
            Check(n2 == 200 && enOrden, "al soltarse el archivo llegan las 200, en el orden en que se apuntaron (antes se perdian)");

            Console.WriteLine("\n  3) LO QUE DICE CADA ARCHIVO NO CAMBIA\n");
            string primera = t2.Split('\n')[0].TrimEnd('\r');
            Check(primera.Length > 14 && primera[2] == ':' && primera[5] == ':' && primera[8] == '.' && primera.Substring(12) == "  linea 000",
                  "diagnostico: la hora con milesimas de cuando se apunto y el texto (" + primera + ")");
            Call(TF, f, "LogError", "Prueba de la bitacora", new InvalidOperationException("algo fallo"));
            Codigo.EsperaBitacora();
            string te = Lee(err);
            string[] le = te.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            Check(le.Length >= 3 && le[0].Length > 19 && le[0][4] == '-' && le[0].Substring(19).StartsWith("  [Prueba de la bitacora]  (rocola del ")
                  && le[1].StartsWith("System.InvalidOperationException: algo fallo") && te.EndsWith("\r\n\r\n"),
                  "errores: fecha, contexto, sello de version y la excepcion, como siempre");
            // El tope: un diagnostico de mas de 2 MB empieza de nuevo.
            using (FileStream g = new FileStream(diag, FileMode.Create)) g.SetLength(2097152 + 10);
            Call(TF, f, "Diag", "tras el tope");
            Codigo.EsperaBitacora();
            Check(new FileInfo(diag).Length < 100 && Lee(diag).Contains("tras el tope"), "el diagnostico por encima de 2 MB empieza de nuevo, como siempre");
            Call(TF, f, "NoteIncompatible", Path.Combine(dir, "rara.mp3"), new Exception("formato raro"));
            Codigo.EsperaBitacora();
            Check(Lee(Path.Combine(dir, "rocola_incompatibles.txt")).Contains("rara.mp3  |  formato raro\r\n"), "y la lista de incompatibles, igual");

            Console.WriteLine("\n  4) UN FALLO GRAVE SE ESCRIBE YA\n");
            Call(TF, f, "Diag", "pendiente antes del fallo");
            MethodInfo crash = TP == null ? null : TP.GetMethod("LogCrash", T);
            if (crash != null) crash.Invoke(null, new object[] { "UnhandledException", new ApplicationException("se cae") });
            // Sin esperar a nada: tiene que estar ya en el disco.
            Check(Lee(err).Contains("[UnhandledException]") && Lee(err).Contains("se cae"), "LogCrash deja el fallo en el disco antes de volver");
            Check(Lee(diag).Contains("pendiente antes del fallo"), "y con el lo que estaba pendiente de los demas archivos");

            Console.WriteLine("\n  5) LO QUE CUESTA APUNTAR EN LA PANTALLA (medida, no criterio)\n");
            List<double> v5 = new List<double>();
            for (int i = 0; i < 500; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                Call(TF, f, "Diag", "medida " + i);
                sw.Stop(); v5.Add(sw.Elapsed.TotalMilliseconds * 1000);
            }
            v5.Sort();
            Codigo.EsperaBitacora();
            Console.WriteLine("        500 lineas: mediana " + v5[250].ToString("0") + " us, la peor " + v5[499].ToString("0") + " us en el hilo que apunta");
            Check(Cuenta(Lee(diag), "  medida ") == 500, "y las 500 llegan al archivo");
        }
        catch (Exception e) { Check(false, "sin excepciones: " + (e.InnerException ?? e)); }
        finally
        {
            Codigo.EsperaBitacora();
            carpeta.SetValue(null, previa);
            try { Directory.Delete(dir, true); } catch { }
        }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}
