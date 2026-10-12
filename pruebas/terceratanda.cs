using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// TERCERA TANDA DE FALLOS (12-10-2026).
//  1) Una cancion con el ARCHIVO danado se podia pedir una y otra vez: entraba en la cola, fallaba al llegarle el
//     turno y devolvia el credito, cada vez. Ahora, tras el primer fallo, al pedirla se dice al momento que esta
//     danada y no se cobra ni entra en la cola. Solo si es el archivo (no el disco), y mientras el archivo sea el mismo.
//  2) El aviso de fallo de la linea de estado lleva el motivo PRIMERO: con un nombre largo, la orilla se comia el motivo.
//  3) La biblioteca vacia decia "No encontre MP3", y la rocola toca tambien WAV, WMA...
// Formulario aislado, sin ventana ni musica.
class TerceraTanda
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static Type form;
    static object Call(object o, string n, params object[] a)
    {
        foreach (MethodInfo m in form.GetMethods(B)) if (m.Name == n && m.GetParameters().Length == a.Length)
            try { return m.Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; }
        throw new Exception("Falta " + n);
    }
    static int Main(string[] args)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rocola_tercera_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try { return Prueba(args[0], dir); }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
    static int Prueba(string exe, string dir)
    {
        form = Assembly.LoadFrom(exe).GetType("Rocola.Form1", true);
        object f = Codigo.FormSinEncender(form);
        Codigo.PonColecciones(f);

        // 1) Las canciones danadas.
        Func<Exception, bool> danado = delegate (Exception e) { return (bool)form.GetMethod("EsArchivoDanado", B).Invoke(null, new object[] { e }); };
        Check(danado(new FormatException("Not a WAVE file")) && danado(new InvalidOperationException("Got a frame at sample rate"))
              && danado(new EndOfStreamException()) && danado(new InvalidDataException()), "formato malo, cortado o frecuencias mezcladas: es el archivo");
        Check(!danado(new FileNotFoundException()) && !danado(new DirectoryNotFoundException()) && !danado(new IOException("device not ready"))
              && !danado(new UnauthorizedAccessException()) && !danado(null), "no encontrado, disco que falla o sin permiso: es el disco, no se marca");
        string a = Path.Combine(dir, "01 - Rota.mp3");
        File.WriteAllBytes(a, new byte[5000]);
        Call(f, "MarcaDanada", a, new FormatException("x"));
        Check((bool)Call(f, "EstaDanada", a), "tras fallar por archivo danado, queda apuntada");
        Check(!(bool)Call(f, "EstaDanada", Path.Combine(dir, "otra.mp3")), "las demas no");
        string b = Path.Combine(dir, "02 - Lejos.mp3");
        File.WriteAllBytes(b, new byte[100]);
        Call(f, "MarcaDanada", b, new DirectoryNotFoundException("x"));
        Check(!(bool)Call(f, "EstaDanada", b), "si fallo por el disco, se puede volver a pedir");
        Call(f, "MarcaDanada", Path.Combine(dir, "no-existe.mp3"), new FormatException("x"));
        Check(!(bool)Call(f, "EstaDanada", Path.Combine(dir, "no-existe.mp3")), "si el archivo ni existe (el disco se fue), no se marca");
        // AddItem: no entra, no cobra, y lo dice.
        Type tTrack = form.GetNestedType("Track", B), tItem = form.GetNestedType("PlayItem", B);
        object t = Activator.CreateInstance(tTrack, true); tTrack.GetField("Path", B).SetValue(t, a);
        object it = Activator.CreateInstance(tItem, true); tItem.GetField("Song", B).SetValue(it, t);
        form.GetField("credits", B).SetValue(f, 3);
        object r = Call(f, "AddItem", it);
        IList cola = (IList)form.GetField("queue", B).GetValue(f);
        Check(r.ToString() == "Danada" && (int)form.GetField("credits", B).GetValue(f) == 3 && cola.Count == 0, "pedirla otra vez: no entra en la cola ni cobra (" + r + ")");
        form.GetField("credits", B).SetValue(f, 0);
        Check(Call(f, "AddItem", it).ToString() == "Danada", "y sin creditos dice que esta danada, no que metas moneda");
        // Si el operador la cambia (otro tamano), se puede volver a pedir.
        File.WriteAllBytes(a, new byte[7000]);
        Check(!(bool)Call(f, "EstaDanada", a), "si se cambia el archivo, se vuelve a poder pedir");
        Check(((IDictionary)form.GetField("danadas", B).GetValue(f)).Count == 0, "y se olvida la marca");
        // Cableado.
        Check(Codigo.Llama(form.GetMethod("AudioStartFailed", B), "MarcaDanada"), "el fallo al empezar marca la cancion");
        Check(Codigo.Llama(form.GetMethod("AddItem", B), "EstaDanada"), "pedir una cancion mira si esta danada");
        string aviso = (string)form.GetField("AvisoDanada", B).GetValue(null);
        Check(aviso.Contains("dañada") && aviso.Contains(": "), "el aviso: «" + aviso + "» (titulo y renglon)");
        foreach (string m in new string[] { "AddSong", "ChooseSearchResult" })
        {
            MethodInfo mi = form.GetMethod(m, B);
            if (mi == null) { foreach (MethodBase x in Codigo.TodosLosMetodos(form)) if (Codigo.Llama(x, "AddItem") && x.Name.Contains("Search")) { mi = x as MethodInfo; break; } }
            Check(mi != null && Codigo.IlConNombres(mi).Exists(delegate (string s) { return s.EndsWith(" AvisoDanada") || s.Contains("\"" + aviso + "\""); }),
                  (mi != null ? mi.Name : m) + " avisa que esta danada");
        }

        // 2) El motivo primero.
        List<string> textos = Codigo.Textos(form.GetMethod("AudioStartFailed", B));
        Check(textos.Contains("No se pudo reproducir: "), "el aviso empieza por el motivo: «No se pudo reproducir: <motivo> (archivo)»");

        // 3) La biblioteca vacia.
        bool vacia = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(form))
            foreach (string s in Codigo.Textos(m)) if (s.StartsWith("No encontré música")) vacia = true;
        Check(vacia, "biblioteca vacia: «No encontré música en la carpeta»");
        foreach (MethodBase m in Codigo.TodosLosMetodos(form))
            foreach (string s in Codigo.Textos(m)) if (s.StartsWith("No encontre MP3")) Check(false, "queda el texto viejo en " + m.Name);

        Console.WriteLine(mal == 0 ? "  OK terceratanda: " + ok + " comprobaciones (formulario aislado; sin ventana ni musica)"
                                   : "  FALLO terceratanda: " + mal + " de " + (ok + mal));
        return mal;
    }
}
