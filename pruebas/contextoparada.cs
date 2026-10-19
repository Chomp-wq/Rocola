using System;
using System.Collections.Generic;
using System.Reflection;

// EL CONTEXTO DE UNA PARADA DE PANTALLA (19-10-2026; Rocola/MedidasBitacora.cs).
//
// Una parada de cientos de milisegundos en el equipo del bar pudo ser el recolector de basura, la memoria, un hilo de fondo o
// el dibujo. La linea del vigia solo decia que hacia la pantalla. Ahora agrega, SOLO cuando hay una parada que apuntar y desde
// el hilo del vigia, cuantas recolecciones hubo desde el apunte anterior, la memoria del programa y la RAM que queda.
//
// Lo que se vigila:
//   1) que el contexto salga y diga lo que promete (recolecciones, memoria del programa) sin romper aunque el sistema no
//      conteste lo de la RAM (este Linux no tiene GlobalMemoryStatusEx);
//   2) que las recolecciones sean la DIFERENCIA con el apunte anterior: tras forzar recolecciones, la siguiente linea las cuenta;
//   3) que la bitacora de diagnostico y el archivo de errores lleven el mismo contexto;
//   4) que el contexto NO se calcule en cada cuadro ni en cada toque del vigia: solo dentro de ApuntaParada, y solo con parada.
class ContextoParada
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos, oks;
    static void Exige(bool c, string q) { if (c) { oks++; Console.WriteLine("  OK    " + q); } else { fallos++; Console.WriteLine("  FALLO " + q); } }
    static int Entero(string texto, string antes)
    {
        int i = texto.IndexOf(antes); if (i < 0) return -1;
        int j = i + antes.Length, k = j; while (k < texto.Length && char.IsDigit(texto[k])) k++;
        return k > j ? int.Parse(texto.Substring(j, k - j)) : -1;
    }
    // La tercera cifra de "gc_desde_el_apunte_anterior=a/b/c": las recolecciones completas.
    static int Gen2(string texto)
    {
        int i = texto.IndexOf("gc_desde_el_apunte_anterior="); if (i < 0) return -1;
        string[] p = texto.Substring(i + "gc_desde_el_apunte_anterior=".Length).Split(';')[0].Split('/');
        return p.Length == 3 ? int.Parse(p[2]) : -1;
    }
    static int Main(string[] argv)
    {
        Assembly a = Assembly.LoadFrom((argv.Length > 0) ? argv[0] : "rocola.exe");
        Type tf = a.GetType("Rocola.Form1");
        MethodInfo contexto = tf.GetMethod("ContextoDeParada", TODO), apunta = tf.GetMethod("ApuntaParada", TODO);
        Exige(contexto != null && apunta != null, "existen ContextoDeParada y ApuntaParada");
        if (contexto == null || apunta == null) return 1;
        object f = Codigo.FormSinEncender(tf);
        string uno = (string)contexto.Invoke(f, null);
        Exige(uno.StartsWith("; gc_totales=") && uno.Contains("trabajo=") && uno.Contains("privada=") && uno.Contains("monton_gestionado="),
              "la primera vez dice los totales del recolector y la memoria del programa: " + uno);
        for (int i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
        string dos = (string)contexto.Invoke(f, null);
        Exige(dos.Contains("gc_desde_el_apunte_anterior="), "la siguiente cuenta desde el apunte anterior: " + dos);
        int d2 = Gen2(dos);
        Exige(d2 >= 3, "y las tres recolecciones completas forzadas entre las dos se cuentan (" + d2 + " de generacion 2)");
        string tres = (string)contexto.Invoke(f, null);
        Exige(Gen2(tres) < d2, "sin nada mas, la tercera ya no las arrastra (" + Gen2(tres) + ")");
        // Quien lo llama: solo ApuntaParada, y con la misma cadena en los dos archivos.
        int llamadas = 0; bool enApunta = Codigo.Llama(apunta, "ContextoDeParada");
        foreach (Type t in a.GetTypes())
            foreach (MethodBase m in Codigo.TodosLosMetodos(t))
                if (m != contexto && Codigo.Llama(m, "ContextoDeParada")) llamadas++;
        Exige(enApunta && llamadas == 1, "ContextoDeParada solo lo llama ApuntaParada (" + llamadas + " sitio)");
        Exige(Codigo.Textos(apunta).Contains("pantalla parada ") && Codigo.Textos(apunta).Contains("; miniaturas_pendientes="),
              "ApuntaParada sigue escribiendo la linea del diagnostico y el apunte de errores");
        // El vigia no lo pide: su toque cada 25 ms no calcula nada de esto.
        bool vigiaNoLoPide = true;
        Type tv = a.GetType("Rocola.VigiaPantalla");
        foreach (MethodBase m in Codigo.TodosLosMetodos(tv)) if (Codigo.Llama(m, "ContextoDeParada") || Codigo.Llama(m, "GlobalMemoryStatusEx")) vigiaNoLoPide = false;
        Exige(vigiaNoLoPide, "y el hilo del vigia no lo calcula en cada toque");
        Console.WriteLine(fallos == 0 ? "\n  PASS contextoparada (" + oks + " comprobaciones; Linux/Mono: la RAM del sistema no se consulta aqui)" : "\n  " + fallos + " fallos");
        return fallos;
    }
}
