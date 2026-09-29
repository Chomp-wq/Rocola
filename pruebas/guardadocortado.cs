using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
// UN GUARDADO CORTADO A MEDIAS (Form1.Datos.cs, RecuperaGuardadosCortados).
//
// Donde File.Replace no se puede (una memoria FAT32, o el archivo bloqueado un instante por un antivirus), guardar va
// en dos pasos: el bueno pasa a .bak y el nuevo a su sitio. Si la luz se va entre los dos, en el disco queda la copia
// (.bak) y el nuevo a medio poner (.tmp), pero NO el archivo bueno. Casi ningun lector miraba la copia: la rocola
// arrancaba con esa tabla vacia, el primer guardado de la sesion la dejaba asi y el siguiente se llevaba la copia.
// Aqui se deja el disco exactamente asi y se mira, con la rocola de verdad:
//   1) al arrancar, cada archivo que falta vuelve de su copia (y la copia no se toca); lo que si esta, no se toca;
//   2) las tablas, los ajustes y las teclas se leen enteros, y guardar despues NO pierde lo de antes;
//   3) el estado (creditos y cola) no entra: ahi que el archivo no este quiere decir algo;
//   4) queda apuntado en rocola_error.txt, y se hace antes de leer nada.
class GuardadoCortado
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static object Get(object o, string n) { return TF.GetField(n, T).GetValue(o); }
    static void Set(object o, string n, object v) { TF.GetField(n, T).SetValue(o, v); }
    static object Call(object o, string n, params object[] a)
    {
        try { return TF.GetMethod(n, T).Invoke(o, a); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static object FormNueva()
    {
        object f = Codigo.FormSinEncender(TF);
        foreach (FieldInfo fi in TF.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            if (fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
        Set(f, "startupReadPending", false);
        return f;
    }
    static string Lee(string p) { return File.Exists(p) ? Encoding.UTF8.GetString(File.ReadAllBytes(p)) : null; }
    static void Escribe(string p, string s) { File.WriteAllBytes(p, new UTF8Encoding(false).GetBytes(s)); }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        string dir = Path.Combine(Path.GetTempPath(), "rocola_cortado_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        FieldInfo carpeta = TF.GetField("carpetaDatos", T);
        object previa = carpeta.GetValue(null);
        try
        {
            carpeta.SetValue(null, dir);
            string vol = Path.Combine(dir, "rocola_volumen.txt"), cfg = Path.Combine(dir, "rocola_config.txt");
            string eq = Path.Combine(dir, "rocola_eq.txt"), est = Path.Combine(dir, "rocola_estado.txt");
            string stats = Path.Combine(dir, "rocola_stats.txt");
            // El disco tal como lo deja un corte entre los dos pasos: la copia completa y el nuevo a medio poner.
            string volViejo = "0.2|200|D:\\m\\a.mp3\r\n0.25|180|D:\\m\\b.mp3\r\n0.3|210|D:\\m\\c.mp3\r\n";
            Escribe(vol + ".bak", volViejo);
            Escribe(vol + ".tmp", "0.2|200|D:\\m\\a.mp3\r\n0.25|1");
            Escribe(cfg + ".bak", "nivel_objetivo=0.31\r\ndir=E:\\MasMusica\r\nturno=0\r\n");
            Escribe(eq + ".bak", "1;2;0;-1;0;1;2;3;16000;0;40;1|D:\\m\\a.mp3\r\n");
            Escribe(est + ".bak", "ROCOLA-ESTADO v1\r\ncreditos=5\r\n");
            // Uno que SI esta, con una copia distinta: no se toca.
            Escribe(stats, "el bueno"); Escribe(stats + ".bak", "la copia");

            Console.WriteLine("\n  1) AL ARRANCAR, LO QUE FALTA VUELVE DE SU COPIA\n");
            object f = FormNueva();
            Call(f, "RecuperaGuardadosCortados");
            Check(Lee(vol) == volViejo && Lee(vol + ".bak") == volViejo, "el volumen vuelve de su copia, con los mismos bytes, y la copia se queda");
            Check(Lee(cfg) != null && Lee(eq) != null, "y los ajustes y el EQ tambien");
            Check(Lee(stats) == "el bueno" && Lee(stats + ".bak") == "la copia", "lo que si estaba no se toca");
            Check(!File.Exists(est), "el estado (creditos y cola) NO: ahi que falte quiere decir algo, y su lector ya mira la copia");
            Codigo.EsperaBitacora();   // la bitacora escribe detras (bitacoradetras.cs)
            string errores = Lee(Path.Combine(dir, "rocola_error.txt"));
            Check(errores != null && errores.Contains("rocola_volumen.txt recuperado de su copia") && errores.Contains("rocola_config.txt recuperado"),
                  "y queda apuntado en rocola_error.txt");

            Console.WriteLine("\n  2) SE LEE ENTERO Y GUARDAR DESPUES NO PIERDE NADA\n");
            Call(f, "LoadLoudCache");
            Dictionary<string, float> loud = (Dictionary<string, float>)Get(f, "loudCache");
            Check(loud.Count == 3, "las tres canciones medidas estan (" + loud.Count + ")");
            loud["D:\\m\\d.mp3"] = 0.22f;
            Call(f, "SaveLoudCache"); Codigo.EsperaGuardado(TF, f);
            string ahora = Lee(vol);
            Check(ahora != null && ahora.Contains("D:\\m\\a.mp3") && ahora.Contains("D:\\m\\c.mp3") && ahora.Contains("D:\\m\\d.mp3"),
                  "una medida nueva se guarda JUNTO a las de antes (antes: solo la nueva, y a la segunda se iba la copia)");
            Call(f, "LoadConfigPrefs");
            List<string> dirs = (List<string>)Get(f, "extraMusicDirs");
            Check(Math.Abs((float)Get(f, "normTarget") - 0.31f) < 1e-4f && dirs.Contains("E:\\MasMusica") && !(bool)Get(f, "turnoOn"),
                  "los ajustes se leen: nivel, carpetas de musica extra y los interruptores");

            Console.WriteLine("\n  3) SE HACE ANTES DE LEER NADA\n");
            MethodInfo empieza = TF.GetMethod("BeginStartupDataLoad", T);
            List<string> orden = Codigo.Nombres(empieza);
            int r = orden.IndexOf("RecuperaGuardadosCortados"), c = orden.IndexOf("CreateStartupLoad");
            Check(r >= 0 && c >= 0 && r < c, "BeginStartupDataLoad recupera antes de lanzar el lector de las tablas");
            List<string> ctor = Codigo.Nombres(TF.GetConstructor(Type.EmptyTypes));
            int b = ctor.IndexOf("BeginStartupDataLoad"), k = ctor.IndexOf("LoadKeys");
            Check(b >= 0 && k >= 0 && b < k, "y eso va antes que las teclas (y los ajustes, que se leen al mostrarse)");
        }
        catch (Exception e) { Check(false, "sin excepciones: " + (e.InnerException ?? e)); }
        finally
        {
            carpeta.SetValue(null, previa);
            try { Directory.Delete(dir, true); } catch { }
        }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}
