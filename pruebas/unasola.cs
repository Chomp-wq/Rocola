using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

// UNA SOLA ROCOLA A LA VEZ.
//
// No habia nada que impidiera abrir la rocola dos veces, y en un local pasa: tarda en arrancar -tiene que
// leer la biblioteca entera-, parece que no hizo nada, y se le da doble clic otra vez. O el atajo del
// arranque de Windows coincide con uno que alguien lanzo a mano.
//
// Con dos rocolas corriendo a la vez pasan cuatro cosas, y ninguna es pequena:
//   - suenan DOS canciones encima;
//   - las dos escriben los mismos archivos y se pisan: se pierden los creditos del cliente, el volumen
//     medido de la biblioteca -horas de trabajo- y lo mas sonado. El candado de los guardados protege
//     dentro de UN programa; entre dos programas distintos no puede hacer nada;
//   - los creditos van por separado: la moneda entra en una y el cliente esta mirando la otra;
//   - y el repaso de la biblioteca corre dos veces, con el disco moliendo el doble durante horas.
//
// Aqui se comprueba que la senal que lo impide existe, que su nombre es EL MISMO en dos arranques distintos
// (si cambiara, la segunda rocola no vería a la primera y arrancaria igual), que dos instalaciones distintas
// no se estorban entre si, y -lo que de verdad importa- que la senal hace su trabajo.
class UnaSola
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TP = asm.GetType("Rocola.Program", true);
        MethodInfo mClave = TP.GetMethod("ClaveInstancia", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        Console.WriteLine("\n  1) LA ROCOLA MARCA QUE YA ESTA CORRIENDO\n");
        Check(mClave != null, "la rocola tiene una senal para saber que ya hay otra corriendo");
        FieldInfo fMutex = TP.GetField("unica", BindingFlags.Static | BindingFlags.NonPublic);
        Check(fMutex != null && fMutex.FieldType == typeof(Mutex),
              "y la senal es de las del sistema (la ve otro programa, no solo este)");
        if (mClave == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        // ---------- 2) EL NOMBRE DE LA SENAL ----------
        // Tiene que salir SIEMPRE igual para la misma carpeta: si cambiara de un arranque a otro, la segunda
        // rocola no veria a la primera y arrancaria tan campante. Y tiene que ser DISTINTO para carpetas
        // distintas, para que una instalacion de pruebas no le estorbe a la del local.
        Console.WriteLine("\n  2) EL NOMBRE DE LA SENAL SALE DE DONDE ESTA INSTALADA\n");
        string a1 = (string)mClave.Invoke(null, new object[] { @"C:\Rocola" });
        string a2 = (string)mClave.Invoke(null, new object[] { @"C:\Rocola" });
        string a3 = (string)mClave.Invoke(null, new object[] { @"C:\Rocola\" });
        string a4 = (string)mClave.Invoke(null, new object[] { @"c:\rocola" });
        string b1 = (string)mClave.Invoke(null, new object[] { @"D:\Pruebas\Rocola" });
        Console.WriteLine("     C:\\Rocola -> " + a1 + "     D:\\Pruebas\\Rocola -> " + b1);
        Check(a1 == a2, "la misma carpeta da siempre el mismo nombre");
        Check(a1 == a3 && a1 == a4, "y da igual la barra del final o las mayusculas");
        Check(a1 != b1, "dos instalaciones distintas dan nombres distintos: no se estorban");
        Check(a1 != null && a1.IndexOf('\\') < 0 && a1.IndexOf('/') < 0,
              "el nombre no lleva barras (el sistema no las admite en una senal)");

        // ---------- 3) Y LA SENAL HACE SU TRABAJO ----------
        // Se coge la senal con el MISMO nombre que usaria la rocola y se comprueba que un segundo intento ya
        // no la consigue: eso es exactamente lo que le pasa a la segunda rocola cuando alguien la abre.
        Console.WriteLine("\n  3) CON UNA COGIDA, LA SEGUNDA NO ENTRA\n");
        string clave = (string)mClave.Invoke(null, new object[] { Path.GetFullPath(".") });
        bool primera, segunda;
        using (Mutex m1 = new Mutex(true, clave, out primera))
        {
            using (Mutex m2 = new Mutex(true, clave, out segunda))
            {
                Console.WriteLine("     la primera coge la senal: " + (primera ? "si" : "no")
                                  + "     la segunda la coge: " + (segunda ? "si" : "no"));
                Check(primera, "la primera rocola se queda con la senal");
                Check(!segunda, "y la segunda ve que ya esta cogida: se va sin hacer nada");
            }
            m1.ReleaseMutex();
        }
        // Y al soltarla, otra si puede arrancar (si no, tras cerrar la rocola ya no abriria nunca mas).
        bool tercera;
        using (Mutex m3 = new Mutex(true, clave, out tercera)) { if (tercera) m3.ReleaseMutex(); }
        Check(tercera, "y al cerrarse la primera, la siguiente si puede arrancar");

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}
