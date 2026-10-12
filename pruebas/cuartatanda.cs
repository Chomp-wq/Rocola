using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// CUARTA TANDA DE FALLOS (12-10-2026).
//  1) F5 (volver a leer TODA la biblioteca) y F12 (la ventana de prueba de la tarjeta) no miraban si un boton de la
//     rocola estaba en esa tecla, como si lo miran F8 y F10: con un boton en F5, cada vez que se apretaba se borraba
//     la biblioteca guardada y se volvia a recorrer el disco entero.
//  2) El archivo de teclas con dos funciones en el mismo boton (editado a mano o a medias) se aceptaba: la primera que
//     se mira se queda con el boton y la otra no responde nunca. Ahora se vuelve al asistente, que no lo deja hacer.
//     Y un numero que no es una tecla sola (0, negativo, con Ctrl) se ignora.
//  3) Una cancion cuyo ARCHIVO falla a media cancion tambien queda marcada como danada (como la que falla al empezar,
//     tercera tanda): si no, cada vez sonaba un trozo, se cortaba y devolvia el credito. Un lector ya cerrado
//     (ObjectDisposedException) es de la rocola, no del archivo: no marca.
// Formulario aislado, sin ventana ni musica.
class CuartaTanda
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static int Main(string[] args)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rocola_cuarta_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try { return Prueba(args[0], dir); }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
    static int Prueba(string exe, string dir)
    {
        Type form = Assembly.LoadFrom(exe).GetType("Rocola.Form1", true);
        object f = Codigo.FormSinEncender(form);
        Codigo.PonColecciones(f);

        // 1) Las teclas de servicio sueltas ceden ante un boton de la rocola.
        MethodInfo pck = form.GetMethod("ProcessCmdKey", B);
        List<string> il = Codigo.IlConNombres(pck);
        Func<string, bool> mira = delegate (string tecla)
        {
            for (int i = 1; i < il.Count; i++)
                if (il[i].Contains("IsConfiguredKey") && il[i - 1] == "1F " + tecla) return true;
            return false;
        };
        Check(mira("74"), "F5 (leer la biblioteca de cero) mira antes si es un boton de la rocola");
        Check(mira("77") && mira("79"), "F8 y F10, como siempre");
        bool hayF12 = Codigo.Llama(pck, "Toggle");
        if (hayF12) Check(mira("7B"), "F12 (ventana de prueba de la tarjeta) tambien");

        // 2) El archivo de teclas.
        MethodInfo load = form.GetMethod("LoadKeys", B);
        string ruta = Path.Combine(dir, "rocola_teclas.txt");
        Func<string, bool> carga = delegate (string txt) { File.WriteAllText(ruta, txt); return (bool)load.Invoke(f, null); };
        string bueno = "arriba=38\r\nabajo=40\r\nizquierda=37\r\nderecha=39\r\nponer=13\r\nregresar=27\r\nmoneda=53\r\nsaltar=83\r\n";
        Check(carga(bueno), "el archivo de siempre se lee");
        Check((Keys)form.GetField("keyCoin", B).GetValue(f) == Keys.D5 && (Keys)form.GetField("keyOK", B).GetValue(f) == Keys.Enter, "y deja cada boton en su funcion");
        Check(carga("# comentario\r\n\r\nbasura\r\n" + bueno + "otra=7\r\n"), "lineas de mas no estorban");
        Check(!carga(bueno.Replace("moneda=53", "moneda=13")), "moneda y PONER en el mismo boton: al asistente");
        Check((Keys)form.GetField("keyCoin", B).GetValue(f) == Keys.D5, "y no se toca lo que ya habia");
        Check(!carga(bueno.Replace("saltar=83", "saltar=38")), "saltar y arriba en el mismo boton: al asistente");
        Check(!carga(bueno.Replace("saltar=83", "saltar=0")), "un cero no es un boton");
        Check(!carga(bueno.Replace("saltar=83", "saltar=-5")), "un negativo tampoco");
        Check(!carga(bueno.Replace("saltar=83", "saltar=131155")), "ni una tecla con Ctrl (la rocola mira la tecla sola)");
        Check(carga(bueno.Replace("saltar=83", "saltar=-5\r\nsaltar=83")), "si despues viene uno bueno, vale el bueno");

        // 3) La cancion que se rompe a media cancion.
        Func<Exception, bool> danado = delegate (Exception e) { return (bool)form.GetMethod("EsArchivoDanado", B).Invoke(null, new object[] { e }); };
        Check(!danado(new ObjectDisposedException("lector")), "un lector ya cerrado no es un archivo danado");
        Check(danado(new InvalidOperationException("Got a frame at sample rate")) && danado(new FormatException()), "lo del archivo, si");
        MethodInfo parada = form.GetMethod("Output_PlaybackStopped", B);
        Check(Codigo.Llama(parada, "MarcaDanada"), "si se corta a media cancion por el archivo, se marca");
        Check(Codigo.Llama(parada, "SalidaCaida"), "la salida que se cae sigue yendo por su camino (se retoma la misma)");

        Console.WriteLine(mal == 0 ? "  OK cuartatanda: " + ok + " comprobaciones (formulario aislado; sin ventana ni musica)"
                                   : "  FALLO cuartatanda: " + mal + " de " + (ok + mal));
        return mal;
    }
}
