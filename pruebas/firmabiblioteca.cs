using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
// LA FIRMA DE LA BIBLIOTECA CON UNA CARPETA QUE NO SE PUEDE LEER (Form1.Biblioteca.cs, RootSigCaptured/FirmaCarpeta).
//
// La biblioteca guardada (rocola_biblioteca.txt) solo vale si la firma del disco no cambio. Una sola carpeta sin
// permiso de lectura (o una ruta de mas de 260 letras) tiraba la firma ENTERA: sin firma no se guardaba la biblioteca
// y cada arranque recorria el disco completo (minutos en un disco duro). El recorrido de verdad ya se saltaba esa
// carpeta. Aqui se hace una carpeta cerrada DE VERDAD (en Linux, siendo root, se le da el arbol a 'nobody' y el proceso
// pasa a ser 'nobody', porque a root no le cierra nada un permiso) y se mira, con la rocola de verdad:
//   1) la firma sale (antes: excepcion), es estable, y la carpeta cerrada queda apuntada (distinta de la abierta);
//   2) la firma de antes y la de despues del recorrido coinciden: es la condicion para que la biblioteca se guarde;
//   3) lo de las carpetas que si se leen no cambia: las bibliotecas ya guardadas siguen valiendo;
//   4) el recorrido de verdad (ScanFolders) salta la carpeta cerrada y sigue con lo demas, y ahora LO DICE en
//      rocola_error.txt, con el motivo (antes se la tragaba sin decir nada).
class FirmaBiblioteca
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    [DllImport("libc", SetLastError = true)] static extern int chmod(string ruta, uint modo);
    [DllImport("libc", SetLastError = true)] static extern int chown(string ruta, int uid, int gid);
    [DllImport("libc", SetLastError = true)] static extern int setgid(int gid);
    [DllImport("libc", SetLastError = true)] static extern int setuid(int uid);
    [DllImport("libc")] static extern int geteuid();
    const int Nadie = 65534;

    static string Firma(string raiz)
    {
        try { return (string)TF.GetMethod("RootSig", T).Invoke(null, new object[] { raiz }); }
        catch (TargetInvocationException e) { return "EXCEPCION " + e.InnerException.GetType().Name; }
    }

    static int Main(string[] args)
    {
        TF = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe").GetType("Rocola.Form1", true);
        MethodInfo firma = TF.GetMethod("RootSigCaptured", T), carpeta = TF.GetMethod("FirmaCarpeta", T);
        Check(firma != null && carpeta != null && Codigo.Llama(firma, "FirmaCarpeta") && firma.GetMethodBody().ExceptionHandlingClauses.Count > 0,
              "cada carpeta se firma aparte (FirmaCarpeta) y un fallo en una no tira las demas");

        // Siendo root el proceso pasa a 'nobody' (abajo) y 'nobody' tiene que poder ATRAVESAR hasta el arbol de prueba. Un TMPDIR
        // privado (p. ej. el 0700 que crea 'mktemp -d') se lo impide y la prueba fallaba por eso, no por la rocola: siendo root
        // el arbol va a /tmp, que todos atraviesan.
        string baseTmp = (geteuid() == 0 && Directory.Exists("/tmp")) ? "/tmp" : Path.GetTempPath();
        string dir = Path.Combine(baseTmp, "rocola_firma_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        string raiz = Path.Combine(dir, "musica"), abierta = Path.Combine(raiz, "Disco A"), cerrada = Path.Combine(raiz, "Disco B");
        Directory.CreateDirectory(abierta); Directory.CreateDirectory(cerrada);
        File.WriteAllText(Path.Combine(abierta, "01 - Uno.mp3"), "x");
        string datos = Path.Combine(dir, "datos"); Directory.CreateDirectory(datos);   // donde la rocola apunta lo que pasa
        FieldInfo carpetaDatos = TF.GetField("carpetaDatos", T);
        object previa = carpetaDatos.GetValue(null);
        carpetaDatos.SetValue(null, datos);
        string sinCerrar = Firma(raiz);
        bool root = false, cerro = false;
        try
        {
            root = geteuid() == 0;
            foreach (string p in new string[] { dir, raiz, abierta, cerrada, datos, Path.Combine(abierta, "01 - Uno.mp3") })
                if (root) chown(p, Nadie, Nadie);
            cerro = chmod(cerrada, 0) == 0;
            if (cerro && root) cerro = setgid(Nadie) == 0 && setuid(Nadie) == 0;   // a root no le cierra nada un permiso
        }
        catch (Exception e) { Console.WriteLine("  SE SALTA la carpeta cerrada de verdad (aqui no hay permisos de Linux: " + e.GetType().Name + ")"); cerro = false; }
        try
        {
            if (cerro)
            {
                bool deVerdad = false;
                try { Directory.GetFiles(cerrada); } catch (UnauthorizedAccessException) { deVerdad = true; }
                Check(deVerdad, "la carpeta 'Disco B' de verdad no se puede leer" + (root ? " (el proceso ya es 'nobody')" : ""));
                string a = Firma(raiz), b = Firma(raiz);
                Check(!a.StartsWith("EXCEPCION"), "con una carpeta que no se puede leer, la firma sale (antes: " + (a.StartsWith("EXCEPCION") ? a : "excepcion") + ")");
                Check(a == b, "y es estable: dos veces la misma");
                Check(a != sinCerrar, "y la carpeta cerrada queda apuntada: si un dia se puede leer, la firma cambia y se vuelve a mirar");
                object f = Codigo.FormSinEncender(TF);
                TF.GetField("scanDirectoryMemo", T).SetValue(f, Activator.CreateInstance(TF.GetField("scanDirectoryMemo", T).FieldType, true));
                List<string> raices = new List<string> { raiz };
                string antes = null, despues = null;
                try { antes = (string)TF.GetMethod("RootsSigForScan", T).Invoke(f, new object[] { raices }); } catch (TargetInvocationException) { }
                try { despues = (string)TF.GetMethod("RootsSig", T).Invoke(null, new object[] { raices }); } catch (TargetInvocationException) { }
                Check(antes != null && antes == despues, "la de antes del recorrido y la de despues coinciden: la biblioteca se guarda y el proximo arranque no recorre el disco");
                // El recorrido de verdad: salta la carpeta cerrada, sigue con lo demas y lo dice.
                object g = Codigo.FormSinEncender(TF);
                List<string> audio = new List<string>(), video = new List<string>();
                Dictionary<string, string> portadas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                TF.GetMethod("ScanFolders", T).Invoke(g, new object[] { raiz, audio, video, portadas });
                MethodInfo avisa = TF.GetMethod("AvisaCarpetasSaltadas", T);
                if (avisa != null) avisa.Invoke(g, null);
                Codigo.EsperaBitacora();   // la bitacora escribe detras (bitacoradetras.cs)
                string errores = Path.Combine(datos, "rocola_error.txt");
                string te = File.Exists(errores) ? File.ReadAllText(errores) : "";
                Check(audio.Count == 1 && audio[0].EndsWith("01 - Uno.mp3"), "el recorrido de la biblioteca sigue con lo demas: la cancion del disco que si se lee esta");
                Check(te.Contains("1 carpeta no se pudo leer") && te.Contains("Disco B") && te.Contains("UnauthorizedAccessException"),
                      "y la carpeta que no se pudo leer queda dicha en rocola_error.txt, con el motivo (antes, nada)");
                Check(avisa != null && Codigo.Llama(TF.GetMethod("ScanLibraryCore", T), "AvisaCarpetasSaltadas"), "al acabar cada recorrido del disco");
                chmod(cerrada, Convert.ToUInt32("755", 8));
            }
            // Lo que si se lee no cambia: dos carpetas abiertas dan la firma de siempre (la misma cuenta, carpeta a carpeta).
            string otra = Firma(raiz);
            Check(!otra.StartsWith("EXCEPCION") && (!cerro || otra == sinCerrar), "con todo abierto, la firma es la de siempre: las bibliotecas ya guardadas siguen valiendo");
        }
        finally { carpetaDatos.SetValue(null, previa); try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}
