using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
// LA ROCOLA, POR DELANTE DE LAS TAREAS DE WINDOWS (Program.cs: PrioridadDeLaRocola).
//
// El equipo del bar es de la rocola, pero Windows sigue a lo suyo (actualizaciones, antivirus, indexador) y con la
// prioridad normal le quita procesador justo cuando lo necesita. La rocola arranca con prioridad "por encima de lo
// normal". Aqui se mira:
//   1) que la pone solo la rocola que se queda (despues de mirar si ya habia otra) y antes de abrir la ventana;
//   2) que es "por encima de lo normal": nunca "alta" ni "tiempo real", que pueden estorbar al propio Windows;
//   3) que no hay otro sitio que la cambie;
//   4) que si el sistema no deja cambiarla, no pasa nada (se sigue como siempre).
class Prioridad
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }

    // Los enteros que carga el metodo, leidos con el lector comun (Codigo.Pasos): ldc.i4 lleva el numero entero detras y
    // ldc.i4.0 .. ldc.i4.8 lo llevan en la propia instruccion. Las prioridades que importan aqui (por encima de lo normal,
    // alta, tiempo real) no caben en un byte, asi que siempre van con ldc.i4.
    static List<int> Constantes(MethodBase m)
    {
        List<int> r = new List<int>();
        foreach (Codigo.Paso p in Codigo.Pasos(m))
        {
            if (p.Op == 0x20) r.Add(p.Numero);                          // ldc.i4
            else if (p.Op >= 0x16 && p.Op <= 0x1E) r.Add(p.Op - 0x16);  // ldc.i4.0 .. ldc.i4.8
        }
        return r;
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        Type TP = asm.GetType("Rocola.Program", true);
        MethodInfo pon = TP.GetMethod("PrioridadDeLaRocola", T), main = TP.GetMethod("Main", T);
        Console.WriteLine("\n  1) CUANDO SE PONE\n");
        Check(pon != null, "hay un sitio que pone la prioridad de la rocola (PrioridadDeLaRocola)");
        if (pon == null) { Console.WriteLine("\n  FALLOS: " + fallos); return 1; }
        List<string> pasos = Codigo.Nombres(main);
        int iMutex = pasos.IndexOf(".ctor"), iYa = pasos.IndexOf("AlFrenteLaQueYaCorre"), iPon = pasos.IndexOf("PrioridadDeLaRocola"), iRun = pasos.IndexOf("Run");
        Check(iPon > iYa && iYa >= 0 && iPon > iMutex, "solo la rocola que se queda: despues de mirar si ya habia otra corriendo (la segunda se cierra sin tocar nada)");
        Check(iRun > iPon, "y antes de abrir la ventana: desde el primer cuadro");

        Console.WriteLine("\n  2) CUAL\n");
        List<int> k = Constantes(pon);
        bool porEncima = k.Contains((int)ProcessPriorityClass.AboveNormal);
        bool alta = k.Contains((int)ProcessPriorityClass.High) || k.Contains((int)ProcessPriorityClass.RealTime);
        Check(Codigo.Llama(pon, "set_PriorityClass") && porEncima && !alta, "por encima de lo normal; nunca alta ni tiempo real (pueden estorbar al raton o al sonido del propio Windows)");

        Console.WriteLine("\n  3) EN UN SOLO SITIO\n");
        int sitios = 0; string cuales = "";
        Type[] tipos;
        try { tipos = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { tipos = e.Types; }   // sin alguna dll de al lado: los que si cargan
        foreach (Type t in tipos)
            if (t != null)
            foreach (MethodBase m in Codigo.TodosLosMetodos(t))
            {
                if (m.DeclaringType != t) continue;
                try { if (Codigo.Llama(m, "set_PriorityClass")) { sitios++; cuales += " " + t.Name + "." + m.Name; } } catch { }
            }
        Check(sitios == 1, "solo PrioridadDeLaRocola cambia la prioridad de la rocola (" + cuales.Trim() + ")");

        Console.WriteLine("\n  4) SI WINDOWS NO DEJA\n");
        Check(Codigo.Llama(pon, "set_PriorityClass") && pon.GetMethodBody().ExceptionHandlingClauses.Count > 0,
              "si el sistema no deja cambiarla, se sigue como siempre (va protegido)");
        bool revento = false;
        try { pon.Invoke(null, null); } catch { revento = true; }
        Check(!revento, "y llamarla de verdad no revienta nada");

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}
