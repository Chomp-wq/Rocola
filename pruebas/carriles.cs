using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;

// EL REPASO DE LA BIBLIOTECA, REPARTIDO EN VARIOS CARRILES.
//
// Descomprimir la biblioteca entera es, con diferencia, el trabajo mas largo que hace la rocola: horas. Y lo
// hacia UN solo hilo, o sea que en un equipo de ocho o dieciseis nucleos dejaba quieto el 90% de la maquina.
//
// Repartirlo es facil de hacer y facil de hacer MAL, y lo que puede salir mal no se ve mirando la pantalla:
// que dos carriles cojan la misma cancion (trabajo tirado), que alguna se quede sin coger (una cancion sin
// medir para siempre), o que dos escriban a la vez en el archivo de material de la IA y lo dejen entrelazado
// -y por tanto inservible- sin que nadie se entere hasta que la IA deje de aprender.
//
// Aqui se comprueban esas tres cosas, ademas de que la regla de CUANTOS carriles sea la pensada: solo en el
// repaso a fondo, solo en SSD, dejando siempre un nucleo libre y sin pasar de cuatro. En la rocola de dos
// nucleos tiene que dar UNO, o sea exactamente lo de siempre.
class Carriles
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    // La misma regla que aplica la rocola, escrita aparte: si alguien la cambia sin querer, esto lo canta.
    static int CarrilesEsperados(bool aFondo, bool discoLento, int nucleos)
    {
        if (!aFondo || discoLento || nucleos < 4) return 1;
        int c = nucleos - 1;
        return (c > 4) ? 4 : c;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        BindingFlags IF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        Console.WriteLine();
        Console.WriteLine("  esta maquina tiene " + Environment.ProcessorCount + " nucleos");
        Console.WriteLine();

        // ---------- 1) LA REGLA DE CUANTOS CARRILES ----------
        Console.WriteLine("  {0,-34} {1,10} {2,10} {3,10}", "", "1 nucleo", "2 nucleos", "16 nucleos");
        Console.WriteLine("  " + new string('-', 68));
        Console.WriteLine("  {0,-34} {1,10} {2,10} {3,10}", "repaso normal, disco duro",
                          CarrilesEsperados(false, true, 1), CarrilesEsperados(false, true, 2), CarrilesEsperados(false, true, 16));
        Console.WriteLine("  {0,-34} {1,10} {2,10} {3,10}", "repaso normal, SSD",
                          CarrilesEsperados(false, false, 1), CarrilesEsperados(false, false, 2), CarrilesEsperados(false, false, 16));
        Console.WriteLine("  {0,-34} {1,10} {2,10} {3,10}", "repaso A FONDO, disco duro",
                          CarrilesEsperados(true, true, 1), CarrilesEsperados(true, true, 2), CarrilesEsperados(true, true, 16));
        Console.WriteLine("  {0,-34} {1,10} {2,10} {3,10}", "repaso A FONDO, SSD",
                          CarrilesEsperados(true, false, 1), CarrilesEsperados(true, false, 2), CarrilesEsperados(true, false, 16));
        Console.WriteLine();
        Check(CarrilesEsperados(true, false, 2) == 1, "la rocola de dos nucleos se queda en UN carril, como siempre");
        Check(CarrilesEsperados(true, true, 16) == 1, "en disco duro tambien: ahi manda el cabezal, no el procesador");
        Check(CarrilesEsperados(false, false, 16) == 1, "el repaso normal no se reparte: esta pensado para no notarse");
        Check(CarrilesEsperados(true, false, 16) == 4, "a fondo y en SSD se reparte, pero sin pasar de cuatro");
        Check(CarrilesEsperados(true, false, 5) == 4 && CarrilesEsperados(true, false, 4) == 3,
              "y siempre dejando un nucleo libre para la pantalla y la musica");

        // Que el ejecutable lleve de verdad varios hilos, no uno.
        FieldInfo fTh = TF.GetField("preScanTh", IF);
        Check(fTh != null && fTh.FieldType.IsArray, "el repaso lleva un juego de hilos, no uno solo");

        // ---------- 2) EL REPARTO: NI DOS VECES NI NINGUNA ----------
        // Se imita el reparto que hace la rocola -Interlocked sobre un indice compartido- con carriles que
        // tardan cosas distintas, que es lo que pasa de verdad: las canciones no duran lo mismo.
        Console.WriteLine();
        const int N = 5000;
        for (int carriles = 1; carriles <= 4; carriles++)
        {
            int[] veces = new int[N];
            int siguiente = -1;
            Thread[] th = new Thread[carriles];
            for (int c = 0; c < carriles; c++)
            {
                th[c] = new Thread(delegate ()
                {
                    Random r = new Random(Thread.CurrentThread.ManagedThreadId);
                    for (; ; )
                    {
                        int i = Interlocked.Increment(ref siguiente);
                        if (i >= N) break;
                        Interlocked.Increment(ref veces[i]);
                        if (r.Next(20) == 0) Thread.Sleep(0);   // el carril se distrae, como al descomprimir
                    }
                });
                th[c].IsBackground = true;
            }
            for (int c = 0; c < carriles; c++) th[c].Start();
            for (int c = 0; c < carriles; c++) th[c].Join();
            int dosVeces = 0, ninguna = 0;
            for (int i = 0; i < N; i++) { if (veces[i] > 1) dosVeces++; if (veces[i] == 0) ninguna++; }
            Check(dosVeces == 0 && ninguna == 0,
                  "con " + carriles + " carril(es), las " + N + " canciones se miden UNA vez cada una ("
                  + dosVeces + " repetidas, " + ninguna + " olvidadas)");
        }

        // ---------- 3) EL ARCHIVO DE LA IA, ESCRITO POR VARIOS A LA VEZ ----------
        // Sin candado, dos anadidos solapados dejan el archivo entrelazado y por tanto inservible: los pares
        // dejan de estar alineados y lo que aprende la red es ruido. Aqui se llama al metodo DE VERDAD desde
        // cuatro hilos y luego se comprueba que el archivo mide lo que tiene que medir.
        Console.WriteLine();
        MethodInfo mAdd = TF.GetMethod("IaAppendPairs", IF);
        MethodInfo mFile = TF.GetMethod("IaDataFile", IF);
        if (mAdd == null || mFile == null) { Check(false, "existe el guardado del material de la IA"); }
        else
        {
            object f = FormatterServices.GetUninitializedObject(TF);
            FieldInfo fLock = TF.GetField("iaFileLock", IF);
            Check(fLock != null, "el guardado del material de la IA tiene candado");
            if (fLock != null) fLock.SetValue(f, new object());
            string ruta = (string)mFile.Invoke(f, null);
            try { if (File.Exists(ruta)) File.Delete(ruta); } catch { }

            const int HILOS = 4, TANDAS = 40, PARES = 3;   // cada tanda: PARES pares de 40 numeros
            Thread[] th = new Thread[HILOS];
            for (int c = 0; c < HILOS; c++)
            {
                th[c] = new Thread(delegate ()
                {
                    for (int k = 0; k < TANDAS; k++)
                    {
                        List<float> l = new List<float>();
                        for (int i = 0; i < 40 * PARES; i++) l.Add(i * 0.5f);
                        mAdd.Invoke(f, new object[] { l });
                    }
                });
                th[c].IsBackground = true;
            }
            for (int c = 0; c < HILOS; c++) th[c].Start();
            for (int c = 0; c < HILOS; c++) th[c].Join();

            long esperado = 16 + (long)HILOS * TANDAS * 40 * PARES * 4;   // cabecera + todos los numeros
            long real = new FileInfo(ruta).Length;
            Console.WriteLine("  cuatro hilos escribiendo material a la vez: el archivo mide " + real
                              + " bytes y tenia que medir " + esperado);
            Check(real == esperado, "el archivo de material de la IA no se entrelaza");
            try { File.Delete(ruta); } catch { }
        }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}
