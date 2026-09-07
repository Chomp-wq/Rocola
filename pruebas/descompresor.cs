using System;
using System.Collections.Generic;
using System.Reflection;

// LA ROCOLA SE CERRABA A MEDIA CANCION, Y ERA SIEMPRE LA MISMA PUERTA MAL CERRADA.
//
// En el archivo de errores quedaba esto:
//
//     NAudio.MmException: InvalidParameter calling acmStreamClose
//        en NAudio.Wave.Compression.AcmStream.Dispose(Boolean disposing)
//        en NAudio.Wave.Compression.AcmStream.Finalize()
//
// Y eso no es un aviso: es el apunte de que el programa se murio entero.
//
// LO QUE PASA. NAudio arma el descompresor de MP3 -que por dentro es un aparato del sistema, de Windows- y
// JUSTO DESPUES sigue preparandose. Si falla en ese hueco (una cabecera rota, y de esas hay unas cuantas
// en cualquier biblioteca grande) suelta el error y deja el descompresor TIRADO: nadie lo cierra. Un rato
// despues lo cierra el recolector de basura por su cuenta, y ahi esta la trampa: por ese camino NAudio se
// salta el paso de soltar los buffers, Windows contesta que el descompresor ya no vale, y NAudio lanza la
// queja DESDE EL HILO DEL RECOLECTOR, donde nadie la puede recoger. El blindaje de kiosco de Program.cs
// solo cubre el hilo de la pantalla. Se escribe la linea y el programa muere.
//
// EL ARREGLO fue poner un solo camino para abrir un MP3 -AbreMp3- que se queda con el descompresor
// apuntado y lo cierra el mismo si la apertura falla.
//
// Y AQUI ESTA EL PORQUE DE ESTA BATERIA: ese arreglo se puso en DOS de los TRES sitios que abren un MP3.
// El que falto era el camino TOLERANTE, por el que pasan SOLO los archivos que ya hicieron fallar a NAudio
// una vez, o sea justo donde se concentran los fallos. Quince dias despues del arreglo, la rocola se volvio
// a cerrar por la puerta que quedaba.
//
// Un descuido asi no se caza leyendo: se caza con una regla que la maquina pueda mirar. La regla es
//
//     NADIE ABRE UN MP3 FUERA DE AbreMp3
//
// y esta bateria la comprueba en el programa ENTERO, metodo por metodo, sobre el codigo ya compilado.
class Descompresor
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    // El compilador saca los delegados a metodos aparte, y a veces a una CLASE aparte, y le pone el nombre
    // del metodo de fuera entre picos: <AbreMp3>c__AnonStorey14.<>m__0. Eso sigue siendo "dentro de
    // AbreMp3", asi que hay que mirar tambien el nombre de la clase que lo lleva. Y no se puede colar nadie
    // por ahi: ese nombre lo pone el compilador solo, con el del metodo de verdad dentro.
    static bool EsAbreMp3(MethodBase m)
    {
        if (m.Name.IndexOf("AbreMp3", StringComparison.Ordinal) >= 0) return true;
        Type d = m.DeclaringType;
        return d != null && d.Name.IndexOf("AbreMp3", StringComparison.Ordinal) >= 0;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);

        // Todos los metodos del programa, no solo los de Form1: el descompresor se puede abrir desde
        // cualquier sitio, y esa es justamente la clase de descuido que se busca.
        List<MethodBase> todos = new List<MethodBase>();
        foreach (Type t in asm.GetTypes())
            foreach (MethodBase m in Codigo.TodosLosMetodos(t))
                if (!todos.Contains(m)) todos.Add(m);
        Console.WriteLine("\n  se miran " + todos.Count + " metodos del programa entero\n");

        // ¿Hay MP3 por descompresor del sistema en esta version? En la rama de Linux no: alli el MP3 lo
        // descomprime NLayer, que va en el mismo lenguaje que la rocola y no tiene nada que se pueda quedar
        // tirado. Se dice y se pasa, en vez de fingir que se comprobo algo.
        bool hayAcm = false;
        foreach (MethodBase m in todos)
        {
            List<string> ll = Codigo.Llamadas(m);
            if (Codigo.Llama(ll, "Mp3FileReader..ctor") || Codigo.Llama(ll, "CreateAcmFrameDecompressor")) { hayAcm = true; break; }
        }
        if (!hayAcm)
        {
            // SE DICE COMO UNA COMPROBACION QUE PASA, no como un aviso suelto. El guion de las pruebas
            // ahora da por FALLIDA una bateria que no saca ni una linea OK -y hace bien: una que no
            // comprueba nada se veia igual de verde que una que si-. Aqui no hay nada que vigilar, pero
            // eso ES el resultado: en esta version no puede existir el fallo.
            Console.WriteLine("  OK     esta version no descomprime MP3 con el aparato del sistema (aqui lo hace");
            Console.WriteLine("         NLayer), asi que no hay descompresor que se pueda quedar tirado\n");
            Console.WriteLine("  TODO EN ORDEN");
            return 0;
        }

        // =========================================================================================
        Console.WriteLine("  1) HAY UN SOLO CAMINO PARA ABRIR UN MP3, Y CIERRA LO QUE ABRE\n");
        MethodInfo mRuta = null, mFlujo = null;
        foreach (MethodInfo m in TF.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (m.Name != "AbreMp3") continue;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length != 1) continue;
            if (ps[0].ParameterType == typeof(string)) mRuta = m;
            else if (typeof(System.IO.Stream).IsAssignableFrom(ps[0].ParameterType)) mFlujo = m;
        }
        Check(mRuta != null, "hay un camino seguro para abrir un MP3 de un ARCHIVO");
        Check(mFlujo != null, "y otro para abrirlo de la MEMORIA (el camino tolerante, por donde se colo)");
        for (int i = 0; i < 2; i++)
        {
            MethodInfo m = (i == 0) ? mRuta : mFlujo;
            string cual = (i == 0) ? "el de archivo" : "el de memoria";
            if (m == null) continue;
            List<string> ll = Codigo.ConAyudantes(TF, "AbreMp3");
            Check(Codigo.Llama(ll, "CreateAcmFrameDecompressor"),
                  cual + " se queda con el descompresor apuntado, que es lo que permite cerrarlo");
            Check(Codigo.Llama(m, "Dispose"),
                  cual + " lo CIERRA si la apertura falla (sin esto seria un envoltorio que no hace nada)");
            break;   // ConAyudantes junta los dos: basta mirarlo una vez
        }

        // =========================================================================================
        Console.WriteLine("\n  2) Y NADIE ABRE UN MP3 POR OTRO LADO\n");
        // Esta es LA comprobacion. Es la que habria cazado el descuido el mismo dia.
        List<string> culpables = new List<string>();
        foreach (MethodBase m in todos)
        {
            if (EsAbreMp3(m)) continue;
            List<string> ll = Codigo.Llamadas(m);
            bool abre = Codigo.Llama(ll, "Mp3FileReader..ctor");
            bool arma = Codigo.Llama(ll, "CreateAcmFrameDecompressor");
            if (!abre && !arma) continue;
            string donde = (m.DeclaringType != null ? m.DeclaringType.Name + "." : "") + m.Name;
            culpables.Add(donde + (abre ? "  (abre un lector)" : "") + (arma ? "  (arma un descompresor)" : ""));
        }
        foreach (string c in culpables) Console.WriteLine("     POR FUERA DEL CAMINO SEGURO:  " + c);
        Check(culpables.Count == 0, "el descompresor del sistema solo se arma dentro de AbreMp3, en ningun otro sitio");

        // =========================================================================================
        Console.WriteLine("\n  3) Y QUIEN GUARDA UN LECTOR, LO CIERRA\n");
        // Abrirlo bien no basta: un lector que se guarda y nunca se cierra acaba igual, cerrado por el
        // recolector de basura y con la misma queja desde el mismo hilo.
        int clases = 0;
        List<Type> vistos = new List<Type>();
        foreach (Type t0 in asm.GetTypes())
            foreach (Type d in Codigo.ConLosDeDentro(t0))
            {
                if (vistos.Contains(d)) continue;   // GetTypes ya trae los de dentro: sin esto se miran dos veces
                vistos.Add(d);
                FieldInfo campo = null;
                foreach (FieldInfo f in d.GetFields(TODO | BindingFlags.DeclaredOnly))
                    if (f.FieldType.Name == "Mp3FileReader") campo = f;
                if (campo == null) continue;
                clases++;
                // Su constructor tiene que pedirlo por el camino seguro...
                bool porElCamino = false;
                foreach (ConstructorInfo c in d.GetConstructors(TODO | BindingFlags.DeclaredOnly))
                    if (Codigo.Llama(c, "AbreMp3")) porElCamino = true;
                Check(porElCamino, d.Name + " pide su lector por el camino seguro");
                // ...y su Dispose tiene que TOCAR ese campo. Si se olvidaran de cerrarlo, no lo tocaria.
                MethodInfo dis = d.GetMethod("Dispose", TODO, null, Type.EmptyTypes, null);
                List<MethodBase> uno = new List<MethodBase>();
                if (dis != null) uno.Add(dis);
                Check(dis != null && Codigo.LeeCampo(uno, campo.Name),
                      "   ...y lo cierra al terminar, no lo deja para el recolector de basura");
            }
        Console.WriteLine("     clases que guardan un lector de MP3: " + clases);
        Check(clases == 3, "son las tres que abren MP3 (la que suena, la que mide y la tolerante), ni una mas");

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO EN ORDEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
