using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

class Prueba2
{
    static Type TF; static object F;
    static object Fld(string n) { return TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).GetValue(F); }
    static void SetFld(string n, object v) { TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(F, v); }
    static object Call(string n, params object[] a)
    {
        MethodInfo mi = TF.GetMethod(n, BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Static);
        return mi.Invoke(mi.IsStatic ? null : F, a);
    }
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static void Nueva()   // simula "la rocola arranca de nuevo": instancia limpia, tablas vacias
    {
        F = FormatterServices.GetUninitializedObject(TF);
        SetFld("iaDone", new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase));
        SetFld("iaDoneLock", new object());
        SetFld("iaPend", new List<string>());
        SetFld("deepDone", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        SetFld("deepDoneLock", new object());
        SetFld("deepPend", new List<string>());
    }
    static Dictionary<string,int> Ia { get { return (Dictionary<string,int>)Fld("iaDone"); } }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        Nueva();
        string fIa = (string)Call("IaDoneFile"), fDeep = (string)Call("DeepDoneFile");
        if (File.Exists(fIa)) File.Delete(fIa);
        if (File.Exists(fDeep)) File.Delete(fDeep);

        string A = @"D:\musica\Banda\01 Uno.mp3", B = @"D:\musica\Banda\02 Dos.flac", C = @"D:\musica\Otro\Tres.wav";

        // --- 1) el modo AUTOMATICO cosecha A y B (nivel 1)
        Check((bool)Call("MarkIaDone", A, 1), "automatico cosecha A (nivel 1)");
        Check((bool)Call("MarkIaDone", B, 1), "automatico cosecha B (nivel 1)");
        Check(!(bool)Call("MarkIaDone", A, 1), "no la vuelve a cosechar en el MISMO nivel");
        Call("SaveIaDone"); System.Threading.Thread.Sleep(500);
        long tam1 = new FileInfo(fIa).Length;

        // --- 2) reinicio: recuerda el nivel 1
        Nueva(); Call("LoadIaDone");
        Check(Ia.Count == 2 && Ia[A] == 1 && Ia[B] == 1, "tras reiniciar, A y B siguen en nivel 1");

        // --- 3) EL ARREGLO: el repaso A FONDO SI puede volver a entrar en ellas (nivel 1 -> 2)
        Check((bool)Call("MarkIaDone", A, 2), "el repaso A FONDO acumula sobre A (1 -> 2)");
        Check((bool)Call("MarkIaDone", C, 2), "y cosecha C directo en nivel 2");
        Check(!(bool)Call("MarkIaDone", A, 2), "pero solo una vez por nivel");
        Check(!(bool)Call("MarkIaDone", A, 1), "y el automatico ya no la toca (nivel menor)");
        Call("SaveIaDone"); System.Threading.Thread.Sleep(500);

        // --- 4) se ANADE al final (no se reescribe): el archivo crecio
        long tam2 = new FileInfo(fIa).Length;
        Check(tam2 > tam1, "el archivo se ANADE (crecio de " + tam1 + " a " + tam2 + " bytes), no se reescribe");

        // --- 5) reinicio: manda el nivel MAS ALTO aunque A aparezca dos veces
        Nueva(); Call("LoadIaDone");
        Check(Ia.Count == 3, "tras reiniciar hay 3 canciones (leyo " + Ia.Count + ")");
        Check(Ia[A] == 2, "A quedo en nivel 2 (la linea mas alta manda)");
        Check(Ia[B] == 1, "B sigue en nivel 1: el repaso a fondo aun puede darle mas");
        Check(Ia[C] == 2, "C en nivel 2");

        // --- 6) COMPATIBILIDAD: un archivo VIEJO (solo rutas, sin nivel) se lee como nivel 1,
        //        asi que la rocola del local podra volver a cosechar a fondo todo lo ya juntado.
        File.WriteAllText(fIa, A + "\r\n" + B + "\r\n" + C + "\r\n");
        Nueva(); Call("LoadIaDone");
        Check(Ia.Count == 3 && Ia[A] == 1 && Ia[B] == 1 && Ia[C] == 1,
              "un rocola_ia_hechas.txt VIEJO se lee como nivel 1 (el repaso a fondo las recupera todas)");

        // --- 7) deepDone tambien por anadido
        Nueva();
        Call("MarkDeepDone", A); Call("MarkDeepDone", B);
        Call("SaveDeepDone"); System.Threading.Thread.Sleep(400);
        long d1 = new FileInfo(fDeep).Length;
        Call("MarkDeepDone", C);
        Call("SaveDeepDone"); System.Threading.Thread.Sleep(400);
        long d2 = new FileInfo(fDeep).Length;
        Check(d2 > d1, "la lista del repaso a fondo tambien se ANADE (" + d1 + " -> " + d2 + ")");
        Nueva(); Call("LoadDeepDone");
        HashSet<string> dd = (HashSet<string>)Fld("deepDone");
        Check(dd.Count == 3, "y al reiniciar estan las 3 (leyo " + dd.Count + ")");

        // --- 8) UNA sola copia de cada ruta en memoria
        string r1 = new string(A.ToCharArray()), r2 = new string(A.ToCharArray());
        Check(!ReferenceEquals(r1, r2), "(dos rutas iguales pero copias distintas)");
        object p1 = Call("Pool", r1), p2 = Call("Pool", r2);
        Check(ReferenceEquals(p1, p2), "Pool() las junta en UNA sola copia (menos memoria con +100k canciones)");
        Check((string)p1 == A, "y el texto no cambia");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
