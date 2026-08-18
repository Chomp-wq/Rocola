using System;
using System.Collections.Generic;
using System.Reflection;

// EL LIMITADOR DE CUADROS con video en pantalla.
//
// Con un video puesto, los cuadros se quedaban en 22 por segundo. El limitador pedia "un cuadro cada 33 ms"
// (30 por segundo) y medía con Environment.TickCount, que en Windows NO avanza de milisegundo en milisegundo:
// avanza a saltos de 15.6 ms. Asi que "ya pasaron 33 ms" solo podia contestarse en el salto siguiente, el de
// 46.8 -> 21.4 cuadros por segundo. Exactamente los 22 que se veian.
//
// Dos arreglos: medir con un reloj fino (Stopwatch, que va en microsegundos) y agendar el proximo cuadro
// sobre el OBJETIVO anterior en vez de sobre el momento en que se dibujo -asi un cuadro que llega tarde no
// arrastra su retraso y el promedio sale el pedido, aunque Windows no despierte cuando se le pide-. Y ademas:
// donde hay nucleos de sobra no se limita nada, porque ahi el video no le esta quitando el turno a nadie.
class Fps
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    // Lo que sale de verdad del limitador: se pide un cuadro cada 'th' ms, se mide con un reloj que salta de
    // 'pasoReloj' en 'pasoReloj', cada espera dura 'pasoSueno', y 'agenda' dice si el proximo cuadro se apunta
    // sobre el OBJETIVO anterior (true, como ahora) o sobre el momento en que se dibujo (false, como antes).
    // Devuelve los cuadros por segundo.
    static double Fotogramas(double th, double pasoReloj, double pasoSueno, bool agenda)
    {
        double t = 0, ultimo = 0, prox = th; int cuadros = 0;
        for (int i = 0; i < 20000; i++)
        {
            double ahora = (pasoReloj <= 0) ? t : Math.Floor(t / pasoReloj) * pasoReloj;
            if (agenda)
            {
                if (ahora + 0.6 < prox) { t += pasoSueno; continue; }
                prox += th;
                if (prox < ahora) prox = ahora + th;
            }
            else
            {
                double medidoUlt = (pasoReloj <= 0) ? ultimo : Math.Floor(ultimo / pasoReloj) * pasoReloj;
                if (ahora - medidoUlt < th) { t += pasoSueno; continue; }
                ultimo = t;
            }
            cuadros++;
            if (t > 4000) break;
        }
        return cuadros / (t / 1000.0);
    }

    // Recorre el codigo compilado de un metodo y anota a que llama (igual que en presencia.cs).
    static int LargoOperando(int op)
    {
        if (op == 0x45) return -1;
        if (op >= 0x2B && op <= 0x37) return 1;
        if (op >= 0x38 && op <= 0x44) return 4;
        switch (op)
        {
            case 0x0E: case 0x10: case 0x11: case 0x12: case 0x13:
            case 0x1F: case 0xDE: return 1;
            case 0x20: case 0x22: case 0x27: case 0x28: case 0x6F:
            case 0x72: case 0x73: case 0x74: case 0x75: case 0x79:
            case 0x7B: case 0x7C: case 0x7D: case 0x7E: case 0x7F: case 0x80:
            case 0x81: case 0x8C: case 0x8D: case 0x8F: case 0xA3: case 0xA4:
            case 0xA5: case 0xC2: case 0xC6: case 0xD0: case 0x29: return 4;
            case 0x21: case 0x23: return 8;
            default: return 0;
        }
    }
    static List<string> Llamadas(MethodBase m)
    {
        List<string> r = new List<string>();
        MethodBody b = m.GetMethodBody(); if (b == null) return r;
        byte[] il = b.GetILAsByteArray(); if (il == null) return r;
        Module mod = m.Module; int i = 0;
        while (i < il.Length)
        {
            int op = il[i];
            if (op == 0xFE) { i += 2; continue; }
            if (op == 0x45) { int n = BitConverter.ToInt32(il, i + 1); i += 1 + 4 + 4 * n; continue; }
            int len = LargoOperando(op);
            if ((op == 0x28 || op == 0x6F) && i + 4 < il.Length)
            {
                int tok = BitConverter.ToInt32(il, i + 1);
                try { MethodBase mb = mod.ResolveMethod(tok); r.Add(mb.DeclaringType.Name + "." + mb.Name); } catch { }
            }
            i += 1 + len;
        }
        return r;
    }
    static bool Llama(List<string> l, string q) { foreach (string s in l) if (s == q) return true; return false; }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        Type TP = null, TPr = null;
        foreach (Type t in asm.GetTypes()) { if (t.Name == "GpuPanel") TP = t; if (t.Name == "Program") TPr = t; }
        if (TP == null) { Console.WriteLine("no hay panel GPU en este ejecutable"); return 0; }

        Console.WriteLine("\n  1) DE DONDE SALIAN LOS 22 CUADROS\n");
        Console.WriteLine("     pidiendo un cuadro cada 33 ms (30 por segundo):");
        double antes = Fotogramas(33, 15.6, 1.0, false);
        double fino = Fotogramas(33, 0, 1.0, true);
        double finoTorpe = Fotogramas(33, 0, 15.6, true);
        double gordoAgenda = Fotogramas(33, 15.6, 1.0, true);
        Console.WriteLine(string.Format("       antes: reloj de Environment.TickCount (saltos de 15.6 ms)   {0:0.0} cuadros/s", antes));
        Console.WriteLine(string.Format("       ahora: reloj fino                                           {0:0.0} cuadros/s", fino));
        Console.WriteLine(string.Format("       ahora, aunque Windows solo despierte cada 15.6 ms:          {0:0.0} cuadros/s", finoTorpe));
        Console.WriteLine(string.Format("       y aunque ADEMAS el reloj fuera el gordo:                    {0:0.0} cuadros/s", gordoAgenda));
        Check(antes > 20 && antes < 23, "asi salian los 22 que se veian: midiendo con TickCount no se puede contestar antes de 46.8 ms");
        Check(fino >= 29 && fino <= 31, "con el reloj fino salen los 30 que se pedian");
        Check(finoTorpe >= 28, "y agendando sobre el objetivo salen igual aunque Windows tarde en despertar");
        Check(gordoAgenda >= 28, "hasta con el reloj gordo: agendar sobre el objetivo recupera el ritmo pedido");

        Console.WriteLine("\n  2) EL PROGRAMA MIDE CON EL RELOJ FINO\n");
        FieldInfo lf = TP.GetField("proxCuadroMs", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(lf != null && lf.FieldType == typeof(double), "el momento del proximo cuadro se guarda con decimales, no en saltos enteros");
        FieldInfo rel = TP.GetField("reloj", BindingFlags.Static | BindingFlags.NonPublic);
        Check(rel != null && rel.FieldType == typeof(System.Diagnostics.Stopwatch), "y hay un reloj fino (Stopwatch) para medirlo");
        if (rel != null)
        {
            System.Diagnostics.Stopwatch sw = (System.Diagnostics.Stopwatch)rel.GetValue(null);
            Check(sw != null && sw.IsRunning, "que ya viene andando desde que arranca la rocola");
            Check(System.Diagnostics.Stopwatch.IsHighResolution, "y es de los de verdad (microsegundos), no una copia del reloj gordo");
        }
        MethodInfo idle = TP.GetMethod("OnIdle", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(idle != null, "el bucle de dibujado esta donde tiene que estar");
        if (idle != null)
        {
            List<string> l = Llamadas(idle);
            Check(Llama(l, "GpuPanel.AhoraMs"), "el limitador pregunta la hora al reloj fino");
            Check(!Llama(l, "Environment.get_TickCount"), "y ya NO al reloj gordo de Windows, que era el del fallo");
        }

        Console.WriteLine("\n  3) EL RELOJ DE 1 ms SE PIDE UNA SOLA VEZ, Y SE DEVUELVE\n");
        // La rocola ya lo pedia desde siempre, en el constructor de Form1. Lo que se comprueba aqui es que
        // siga habiendo UNA sola pareja pedir/devolver: dos parejas no romperian nada (Windows las cuenta),
        // pero es justo el tipo de duplicado que despues se desparea al tocar una sola de las dos.
        Type TFo = asm.GetType("Rocola.Form1", true);
        int nBegin = 0, nEnd = 0;
        foreach (Type t in new Type[]{ TFo, TPr })
        {
            if (t == null) continue;
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (m.Name == "timeBeginPeriod") nBegin++;
                if (m.Name == "timeEndPeriod") nEnd++;
            }
        }
        Console.WriteLine("     declaraciones encontradas: " + nBegin + " para pedirlo y " + nEnd + " para devolverlo");
        Check(nBegin == 1 && nEnd == 1, "una sola pareja, y en Form1, que es quien lo pide al arrancar y lo devuelve al cerrar");
        Check(TFo.GetField("timerResRaised", BindingFlags.Instance | BindingFlags.NonPublic) != null,
              "y se apunta si de verdad se consiguio, para no devolver lo que nunca se pidio");

        Console.WriteLine("\n  4) DONDE SOBRA MAQUINA, NO SE LIMITA NADA\n");
        // La regla vive en SyncGpuFullScreen: con 4 nucleos o mas el video tiene los suyos y limitar el
        // dibujado solo lo empeora sin ganar nada.
        Type TF = asm.GetType("Rocola.Form1", true);
        MethodInfo sync = TF.GetMethod("SyncGpuFullScreen", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(sync != null, "la regla del limitador esta en el sitio de siempre");
        if (sync != null)
        {
            List<string> l = Llamadas(sync);
            Check(Llama(l, "Environment.get_ProcessorCount"), "y mira cuantos nucleos tiene la maquina antes de limitar");
        }
        Console.WriteLine("     esta maquina tiene " + Environment.ProcessorCount + " nucleos: "
                          + (Environment.ProcessorCount < 4 ? "con video se limitaria a 30 cuadros/s" : "con video NO se limita"));

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
