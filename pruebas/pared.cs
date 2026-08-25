using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.Serialization;

// HOJEAR LA PARED DE DISCOS SIN TOCAR EL DISCO.
//
// Cada portada se decodifica una vez y se queda en un cajon en memoria. Mientras siga ahi, verla otra vez
// es gratis; en cuanto sale, hay que ir al disco a leer su miniatura. El cajon era de 220 portadas, un
// numero elegido para la rocola de Windows XP, que tiene 1 GB.
//
// La del bar tiene 4 GB y es de 64 bits. Ahi caben mil -266 MB, medido de verdad- y con mil dentro LA PARED
// ENTERA de un bar normal vive en memoria: se puede hojear de punta a punta y volver sin que el disco se
// entere. Que es exactamente lo que se comprueba aqui: no "cuanta memoria usa", sino CUANTAS VECES HAY QUE
// IR AL DISCO, que es lo que se ve y lo que se oye.
class Pared
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const int PorPagina = 18;     // la pared son 6 x 3 discos
    const int Paginas = 40;       // 720 discos: una biblioteca de bar de las normales

    static Type TF;
    static object F;
    static IDictionary cache;
    static MethodInfo mPut, mDrain;

    // Hojea de la pagina 'desde' a la 'hasta' y devuelve cuantas veces hubo que ir al disco.
    static int Hojea(int desde, int hasta)
    {
        int alDisco = 0;
        int paso = (hasta >= desde) ? 1 : -1;
        for (int p = desde; p != hasta + paso; p += paso)
            for (int k = 0; k < PorPagina; k++)
            {
                string ruta = "/musica/Disco " + (p * PorPagina + k) + "/cover.jpg";
                if (cache.Contains(ruta)) continue;         // ya esta en memoria: gratis
                alDisco++;                                  // no esta: hay que leer su miniatura
                mPut.Invoke(F, new object[] { ruta, new Bitmap(256, 256, PixelFormat.Format32bppPArgb) });
            }
        mDrain.Invoke(F, null);
        return alDisco;
    }

    // Cuanta memoria ve LA ROCOLA. No es lo mismo que la que tiene la maquina: la version de Windows
    // la pide con una funcion de Windows, asi que corriendo las pruebas aqui no la ve y contesta cero.
    // Eso NO es un fallo -es justo el caso "no lo se, se prudente"-, y por eso la comprobacion de abajo
    // esta escrita para valer en las tres versiones.
    static ulong RamQueVeLaRocola(Assembly asm)
    {
        try
        {
            Type TS = asm.GetType("Rocola.Sistema");
            if (TS != null)
            {
                MethodInfo m = TS.GetMethod("MemoriaTotalMb", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (m != null) return (ulong)m.Invoke(null, null);
            }
        }
        catch { }
        return 0;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);

        MethodInfo mTope = TF.GetMethod("TopeDePortadas", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        FieldInfo fTope = TF.GetField("topePortadas", BindingFlags.Static | BindingFlags.NonPublic);
        Check(mTope != null && fTope != null, "la rocola decide cuantas portadas se guarda segun la maquina");
        if (mTope == null || fTope == null) { Console.WriteLine("\n  " + Math.Max(1, fallos) + " FALLO(S)\n"); return Math.Max(1, fallos); }

        Console.WriteLine("\n  1) EL CAJON SE AJUSTA A LA MAQUINA, NO ES UN NUMERO PUESTO A MANO\n");
        // Que de verdad MIRE la maquina. Un numero fijo disfrazado de funcion pasaria la prueba de abajo
        // en esta maquina y dejaria a la de XP tomando 266 MB de su unico giga.
        List<string> loQueHace = Codigo.Llamadas(mTope);
        Check(Codigo.Llama(loQueHace, "MemoriaTotalMb"), "pregunta cuanta memoria tiene la maquina");
        Check(Codigo.Llama(loQueHace, "get_Size"), "y si el programa es de 32 o de 64 bits (en 32 no se pasa de ~2 GB por mucha RAM que haya)");

        ulong ram = RamQueVeLaRocola(asm);
        int tope = (int)mTope.Invoke(null, null);
        Console.WriteLine("     la rocola ve " + (ram > 0 ? ram + " MB de RAM" : "una maquina cuya RAM no puede medir aqui")
                          + ", y el programa es de " + (IntPtr.Size * 8) + " bits");
        Console.WriteLine("     le da " + tope + " portadas  (" + ((long)tope * 273 / 1024) + " MB medidos)");

        // Las dos unicas respuestas sanas, y la regla que las separa. Escrito asi vale en las tres
        // versiones: la de XP -32 bits, 1 GB- tiene que quedarse en 220 pase lo que pase.
        Check(tope == 220 || tope == 1000, "el numero es uno de los dos previstos, no cualquier cosa");
        Check(IntPtr.Size >= 8 || tope == 220, "en 32 bits se queda en 220 (ahi el programa no pasa de ~2 GB por mucha RAM que haya)");
        Check(tope != 1000 || (IntPtr.Size >= 8 && ram >= 3500), "solo da las mil cuando de verdad hay 64 bits y 3.5 GB o mas");
        if (IntPtr.Size >= 8 && ram >= 3500)
            Check(tope == 1000, "y en una maquina asi, se las da");
        else
            Console.WriteLine("     (aqui no se puede comprobar el caso de la maquina grande: la rocola no ve esa RAM)");

        Console.WriteLine("\n  2) HOJEAR LA PARED ENTERA Y VOLVER\n");
        FieldInfo fCache = TF.GetField("coverCache", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fOrder = TF.GetField("coverOrder", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fTrash = TF.GetField("coverTrash", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fLock = TF.GetField("coverLock", BindingFlags.Instance | BindingFlags.NonPublic);
        mPut = TF.GetMethod("CachePut", BindingFlags.Instance | BindingFlags.NonPublic);
        mDrain = TF.GetMethod("DrainCoverTrash", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fCache == null || mPut == null || mDrain == null)
        { Console.WriteLine("\n  FALLO  no encuentro el cajon de portadas\n"); return fallos + 1; }

        Console.WriteLine("     " + (Paginas * PorPagina) + " discos, " + Paginas + " paginas de " + PorPagina);
        Console.WriteLine();
        Console.WriteLine("     cajon    ida (paginas 1->" + Paginas + ")   vuelta (" + Paginas + "->1)");

        int idaChico = 0, vueltaChico = 0, idaGrande = 0, vueltaGrande = 0;
        int[] topes = { 220, tope };
        for (int t = 0; t < topes.Length; t++)
        {
            F = FormatterServices.GetUninitializedObject(TF);
            fCache.SetValue(F, Activator.CreateInstance(fCache.FieldType));
            fOrder.SetValue(F, Activator.CreateInstance(fOrder.FieldType));
            fTrash.SetValue(F, Activator.CreateInstance(fTrash.FieldType));
            if (fLock != null) fLock.SetValue(F, new object());
            cache = (IDictionary)fCache.GetValue(F);
            fTope.SetValue(null, topes[t]);

            int ida = Hojea(0, Paginas - 1);
            int vuelta = Hojea(Paginas - 1, 0);
            Console.WriteLine("     " + topes[t].ToString().PadLeft(4) + "     " + ida.ToString().PadLeft(6)
                              + "            " + vuelta.ToString().PadLeft(6));
            if (t == 0) { idaChico = ida; vueltaChico = vuelta; } else { idaGrande = ida; vueltaGrande = vuelta; }
        }
        fTope.SetValue(null, -1);   // devolver el programa a como estaba

        Console.WriteLine();
        Check(idaChico == Paginas * PorPagina && idaGrande == Paginas * PorPagina,
              "la ida cuesta lo mismo en los dos: la primera vez hay que leerlas todas, no hay magia");
        if (tope > 220)
        {
            Check(vueltaChico > 0, "con el cajon de 220 (la rocola de XP), volver sobre lo andado vuelve al disco " + vueltaChico + " veces");
            Check(vueltaGrande == 0, "con el cajon de " + tope + " (la del bar), volver NO toca el disco ni una vez");
        }
        else
        {
            Console.WriteLine("     (esta maquina es de las chicas: le toca el cajon de 220 y no hay dos casos que comparar)");
            Check(vueltaChico == vueltaGrande, "el cajon chico se comporta igual pidiendolo de las dos formas");
        }

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }
}
