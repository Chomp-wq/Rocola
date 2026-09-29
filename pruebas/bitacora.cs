using System;
using System.Collections;
using System.IO;
using System.Reflection;
// LO QUE TARDA, EN LA BITACORA (MedidasBitacora.cs).
//
// Tres esperas que solo se pueden medir en el equipo del bar: las tablas del arranque, la biblioteca (y dentro, recorrer
// las carpetas para saber si la guardada vale) y cada cancion hasta que suena. Con "diagnostico=1" quedan en
// rocola_diagnostico.txt; sin eso no se escribe nada. Aqui se mira, con la rocola de verdad:
//   1) apagado: no se escribe nada;
//   2) encendido: una linea por cosa, con los milisegundos que se le dan;
//   3) enganchado donde toca: al publicar las tablas, al acabar la biblioteca y cuando la cancion ya suena.
class Bitacora
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Set(object o, string n, object v) { TF.GetField(n, T).SetValue(o, v); }
    static object Call(object o, string n, params object[] a) { try { return TF.GetMethod(n, T).Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }

    static int Main(string[] args)
    {
        TF = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe").GetType("Rocola.Form1", true);
        string dir = Path.Combine(Path.GetTempPath(), "rocola_bitacora_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        FieldInfo carpeta = TF.GetField("carpetaDatos", T);
        object previa = carpeta.GetValue(null);
        try
        {
            carpeta.SetValue(null, dir);
            string diag = Path.Combine(dir, "rocola_diagnostico.txt");
            object f = Codigo.FormSinEncender(TF);
            Set(f, "tracks", Activator.CreateInstance(TF.GetField("tracks", T).FieldType));
            Set(f, "albums", Activator.CreateInstance(TF.GetField("albums", T).FieldType));

            Console.WriteLine("\n  1) APAGADO: NADA\n");
            Set(f, "diagOn", false);
            Call(f, "ApuntaArranque", 12, 3); Call(f, "ApuntaBiblioteca", true); Call(f, "ApuntaCancion", 40);
            Codigo.EsperaBitacora();   // la bitacora escribe detras (bitacoradetras.cs)
            Check(!File.Exists(diag), "sin \"diagnostico=1\" no se escribe nada");

            Console.WriteLine("\n  2) ENCENDIDO: UNA LINEA POR COSA\n");
            Set(f, "diagOn", true);
            Set(f, "bitacoraFirmaMs", 57);
            Call(f, "ApuntaArranque", 12, 3); Call(f, "ApuntaBiblioteca", true); Call(f, "ApuntaCancion", 40);
            Codigo.EsperaBitacora();
            string t = File.Exists(diag) ? File.ReadAllText(diag) : "";
            Check(t.Contains("arranque: tablas leidas en 12 ms") && t.Contains("la ventana espero 3 ms"), "el arranque: lo que tardaron las tablas y lo que las espero la ventana");
            Check(t.Contains("biblioteca: lista en") && t.Contains("la guardada") && t.Contains("(recorrer las carpetas) 57 ms"), "la biblioteca: de donde salio y lo que costo comprobar la guardada");
            Check(t.Contains("cancion: abierta en 40 ms, sonando en"), "la cancion: abierta y sonando");

            Console.WriteLine("\n  3) ENGANCHADO DONDE TOCA\n");
            Check(Codigo.Llama(TF.GetMethod("CompleteStartupDataLoad", T), "ApuntaArranque"), "al publicar las tablas del arranque");
            Check(Codigo.Llama(TF.GetMethod("Scanner_RunWorkerCompleted", T), "ApuntaBiblioteca"), "al acabar la biblioteca");
            Check(Codigo.Llama(TF.GetMethod("StartOpenedTrack", T), "ApuntaCancion"), "cuando la cancion ya suena");
        }
        catch (Exception e) { Check(false, "sin excepciones: " + (e.InnerException ?? e)); }
        finally { carpeta.SetValue(null, previa); try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}
