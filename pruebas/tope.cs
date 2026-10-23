using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

// EL TOPE DE CUADROS DEL FONDO.
//
// El fondo con tarjeta grafica se dibujaba en un bucle SIN TOPE: tan rapido como pudiera el procesador. En un
// equipo con tarjeta decente eso son mil y pico cuadros por segundo, de los que la pantalla solo ENSEÑA
// sesenta; los otros mil se calculaban y se tiraban, con un nucleo entero clavado al 100%. Eso explicaba lo
// que se veia y no se entendia: al encender mas efectos BAJABAN los cuadros por segundo y la GRAFICA no subia
// su uso, porque la grafica nunca fue el cuello.
//
// Aqui se comprueba: 1) que hay tope y es el de la pantalla, 2) que el bucle lo consulta SIEMPRE, 3) que con
// el tope puesto el procesador queda casi libre, y 4) que las animaciones NO se vuelven a tirones por ello
// -que es el riesgo de verdad de poner tope, y por eso su reloj tuvo que pasar a ser el fino-.
class Tope
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TP, TS;
    static object Priv(object o, string n)
    { return o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o); }


    // Los cuadros por segundo que da el limitador tal como esta escrito, con un reloj de paso 'pasoReloj', un
    // sueño que dura 'pasoSueno' y cuadros que cuestan 'costeCuadro' ms. Devuelve tambien que parte del tiempo
    // se le fue al procesador DIBUJANDO (el resto lo paso durmiendo, o sea libre para el sonido y el buscador).
    static void Simula(double th, double pasoReloj, double pasoSueno, double costeCuadro, out double fps, out double ocupado)
    {
        double t = 0, prox = 0; int cuadros = 0; double dibujando = 0;
        for (int i = 0; i < 200000 && t < 4000; i++)
        {
            double ahora = (pasoReloj <= 0) ? t : Math.Floor(t / pasoReloj) * pasoReloj;
            if (ahora + 0.6 < prox) { t += pasoSueno; continue; }
            prox += th;
            if (prox < ahora) prox = ahora + th;
            t += costeCuadro; dibujando += costeCuadro; cuadros++;
        }
        fps = cuadros * 1000.0 / t;
        ocupado = dibujando / t;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes())
        {
            if (t.Name == "GpuPanel") TP = t;
            if (t.Name == "GpuStarfield") TS = t;
        }
        Check(TP != null && TS != null, "el panel de la tarjeta grafica esta donde tiene que estar");
        if (TP == null || TS == null) return 1;

        Console.WriteLine("\n  1) HAY TOPE, Y SALE DEL REFRESCO DE LA PANTALLA\n");
        MethodInfo mTope = TP.GetMethod("TopeMs", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mTope != null, "el panel sabe decir cada cuantos ms le toca un cuadro");
        if (mTope == null) return 1;
        FieldInfo fTopeFps = TP.GetField("TopeFps", BindingFlags.Instance | BindingFlags.Public);
        PropertyInfo pReal = TP.GetProperty("TopeFpsReal", BindingFlags.Instance | BindingFlags.Public);
        Check(fTopeFps != null && pReal != null, "y se le puede fijar a mano desde el config (tope_fps=)");

        // AUTOMATICO: aqui no hay pantalla de verdad, asi que tiene que caer en el respaldo de 60 Hz + 10%.
        Control auto0 = (Control)Activator.CreateInstance(TP, true);
        int msAuto = (int)mTope.Invoke(auto0, null);
        int fpsAuto = (int)pReal.GetValue(auto0, null);
        Console.WriteLine("     sin pantalla que preguntar -> " + msAuto + " ms por cuadro (" + fpsAuto + " cuadros/s)");
        Check(msAuto >= 1, "nunca contesta 0: si contestara 0 el bucle volveria a dibujar sin freno");
        Check(fpsAuto >= 60 && fpsAuto <= 90, "cae en el respaldo de 60 Hz con su margen (60-90 cuadros/s)");

        // A MANO, incluidos valores absurdos: no debe dejar pasar ni el 0 ni un numero disparatado.
        int[] pide = { 1, 24, 30, 60, 66, 100, 144, 240, 5000, -7 };
        bool bien = true;
        foreach (int q in pide)
        {
            Control p = (Control)Activator.CreateInstance(TP, true);
            fTopeFps.SetValue(p, q);
            int ms = (int)mTope.Invoke(p, null);
            int real = (int)pReal.GetValue(p, null);
            if (ms < 1 || real < 24 || real > 1000) { bien = false; Console.WriteLine("     pedir " + q + " dio " + ms + " ms / " + real + " cuadros/s"); }
        }
        Check(bien, "pidiendo 1, 5000 o un numero negativo sigue saliendo un tope con sentido (24-1000 cuadros/s)");

        Console.WriteLine("\n  2) EL BUCLE CONSULTA EL TOPE EN CADA VUELTA\n");
        MethodInfo idle = TP.GetMethod("OnIdle", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(idle != null, "el bucle de dibujado esta donde tiene que estar");
        if (idle != null)
        {
            List<string> l = Codigo.Llamadas(idle);
            Check(Codigo.Llama(l, "GpuPanel.TopeMs"), "el bucle le pregunta el tope antes de cada cuadro");
            Check(Codigo.Llama(l, "GpuPanel.AhoraMs"), "y sigue midiendo con el reloj fino, no con el gordo de Windows");
            // ESPERA, NO NECESARIAMENTE Thread.Sleep. Lo que importa es que el bucle SUELTE el nucleo
            // cuando va adelantado; con que llamada lo haga es cosa suya. Antes dormia a secas; ahora
            // espera por el conducto de la pantalla (Sistema.EsperaMensajes), que suelta el nucleo IGUAL
            // pero ademas se despierta si llega una tecla, en vez de tenerla esperando a que acabe la
            // siesta. Preguntar por Thread.Sleep a secas daba por roto justamente el cambio que lo mejora.
            Check(Codigo.Llama(l, "Thread.Sleep") || Codigo.Llama(l, "EsperaMensajes"),
                  "y cuando no le toca cuadro, ESPERA (deja el nucleo libre)");
        }

        Console.WriteLine("\n  3) CON EL TOPE, EL PROCESADOR QUEDA CASI LIBRE\n");
        // Un cuadro de fondo cuesta ~0.23 ms de procesador (medido aparte, ver la prueba 'cuadro').
        const double COSTE = 0.23;
        double fpsSin, ocupSin, fpsCon, ocupCon;
        Simula(0.0001, 0, 1.0, COSTE, out fpsSin, out ocupSin);        // como estaba: sin tope
        Simula(msAuto, 0, 1.0, COSTE, out fpsCon, out ocupCon);        // como esta ahora
        Console.WriteLine(string.Format("       antes (sin tope): {0,6:0} cuadros/s   y el nucleo {1,5:0.0}% ocupado dibujando", fpsSin, ocupSin * 100));
        Console.WriteLine(string.Format("       ahora (con tope): {0,6:0} cuadros/s   y el nucleo {1,5:0.0}% ocupado dibujando", fpsCon, ocupCon * 100));
        Check(ocupSin > 0.9, "sin tope el nucleo se iba entero en dibujar cuadros que nadie ve");
        Check(ocupCon < 0.10, "con tope el dibujado se lleva menos del 10% de un nucleo: el resto es para el sonido y el buscador");
        Check(fpsCon >= 60, "y siguen saliendo 60 cuadros por segundo o mas, que es lo que la pantalla puede enseñar");

        Console.WriteLine("\n  4) CON EL TOPE PUESTO LAS ANIMACIONES NO SE VAN A TIRONES\n");
        // ESTE es el riesgo de verdad de poner tope. El paso de cada cuadro se medía con el reloj de Windows,
        // que contesta de 15.6 en 15.6 ms. Dibujando mil cuadros por segundo eso se disimulaba; con el tope,
        // cada cuadro dura ~15 ms -o sea justo un salto del reloj- y el avance saldria alternando 15.6 y 0.
        double[] pasos = new double[40];
        double reloj = 0, ant = 0; int n = 0;
        for (int i = 0; i < 40; i++)   // reloj GORDO (como estaba): pasos desparejos
        {
            reloj += msAuto;
            double leido = Math.Floor(reloj / 15.6) * 15.6;
            pasos[n++] = leido - ant; ant = leido;
        }
        double peorGordo = 0, mediaGorda = 0;
        for (int i = 5; i < n; i++) mediaGorda += pasos[i];
        mediaGorda /= (n - 5);
        for (int i = 5; i < n; i++) peorGordo = Math.Max(peorGordo, Math.Abs(pasos[i] - mediaGorda));
        Console.WriteLine(string.Format("       con el reloj de Windows: el paso se desvia hasta {0:0.0} ms de los {1:0.0} que deberia", peorGordo, mediaGorda));
        Check(peorGordo > 5.0, "confirmado: con el reloj gordo el avance saldria a tirones (por eso hubo que cambiarlo)");

        // Y ahora EL PROGRAMA: se le pide un cuadro de aurora, se espera de verdad, y se mira si el reloj de
        // la aurora avanzo LO QUE PASO. Con el reloj gordo habria avanzado 0 (o de golpe 15.6).
        object apagado = FormatterServices.GetUninitializedObject(TS);   // campo de estrellas sin tarjeta: no dibuja nada
        Control pan = (Control)Activator.CreateInstance(TP, true);
        pan.SetBounds(0, 0, 1024, 768);
        TP.GetField("stars", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pan, apagado);
        MethodInfo mAur = TP.GetMethod("DrawAurora", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mAur != null, "la aurora esta donde tiene que estar");
        if (mAur != null)
        {
            Type tAu = mAur.GetParameters()[0].ParameterType;
            object au = Activator.CreateInstance(tAu);
            float[] B = new float[64];
            for (int i = 0; i < B.Length; i++) B[i] = 0.4f;
            tAu.GetField("Has").SetValue(au, true); tAu.GetField("B").SetValue(au, B);
            tAu.GetField("BaseY").SetValue(au, 672f); tAu.GetField("Alto").SetValue(au, 300f);
            tAu.GetField("Dim").SetValue(au, 1f); tAu.GetField("Calma").SetValue(au, 0f);
            tAu.GetField("Rgb").SetValue(au, (uint)0x7A3CC8); tAu.GetField("Rgb2").SetValue(au, (uint)0x3C6AC8);
            tAu.GetField("RgbA").SetValue(au, (uint)0xC85AA0); tAu.GetField("Rgb2A").SetValue(au, (uint)0x8A5AC8);
            object[] arg = new object[] { au };
            FieldInfo fT = TP.GetField("aurT", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(TP.GetField("aurTickAnt", BindingFlags.Instance | BindingFlags.NonPublic) == null,
                  "el reloj de la aurora ya NO es el gordo de Windows (aurTickAnt no existe)");
            Check(TP.GetField("aurFinoAnt", BindingFlags.Instance | BindingFlags.NonPublic) != null,
                  "y si el fino (aurFinoAnt)");
            // DESDE LA CADENCIA DE ONDAS (1-10-2026, cf28d99) la aurora no sigue la hora fina cuadro a cuadro: avanza a
            // las muestras de su rejilla (OndasFps, 60 de fabrica, ~16.7 ms), asi que con cuadros cada 5 ms solo se mueve
            // en uno de cada tres. Lo que se exige ahora, con la misma intencion (reloj fino y no el gordo de ~15.6 ms):
            //   1) cada muestra de la rejilla cae sobre la hora fina: nunca por delante y como mucho un periodo por detras;
            //   2) en cada cuadro la aurora avanza EXACTAMENTE lo que avanzo la rejilla (sin redondear al reloj gordo);
            //   3) en una ventana larga, lo que avanza la aurora coincide con lo que paso de verdad salvo un periodo.
            MethodInfo mAhora = TP.GetMethod("AhoraMs", BindingFlags.Static | BindingFlags.NonPublic);
            Check(mAhora != null, "el reloj fino se puede consultar");
            object cad = TP.GetField("waveCadence", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pan);
            FieldInfo fMs = cad.GetType().GetField("SampleMs", BindingFlags.Instance | BindingFlags.NonPublic);
            double periodo = 1000.0 / 60;
            mAur.Invoke(pan, arg);                       // 1er cuadro: solo pone el reloj en hora
            double relojIni = (double)mAhora.Invoke(null, null), aurIni = (double)fT.GetValue(pan), aurAnt = aurIni;
            double msAnt = (double)fMs.GetValue(cad), peorExacto = 0, peorRetraso = 0, menorRetraso = 1e9;
            for (int k = 0; k < 40; k++)
            {
                System.Threading.Thread.Sleep(5);
                mAur.Invoke(pan, arg);
                double relojYa = (double)mAhora.Invoke(null, null);
                double aurYa = (double)fT.GetValue(pan), ms = (double)fMs.GetValue(cad);
                peorExacto = Math.Max(peorExacto, Math.Abs((aurYa - aurAnt) * 1000.0 - (ms - msAnt)));
                // (el retraso se mide con la hora fina de DESPUES del cuadro: sobra el tiempo de la propia llamada)
                peorRetraso = Math.Max(peorRetraso, relojYa - ms); menorRetraso = Math.Min(menorRetraso, relojYa - ms);
                aurAnt = aurYa; msAnt = ms;
            }
            double relojFin = (double)mAhora.Invoke(null, null);
            double deriva = Math.Abs((aurAnt - aurIni) * 1000.0 - (relojFin - relojIni));
            Console.WriteLine(string.Format("       la aurora contra la rejilla: {0:0.000} ms de diferencia como mucho; la rejilla va {1:0.0}..{2:0.0} ms por detras de la hora fina (periodo {3:0.0}); en la ventana larga {4:0.0} ms de diferencia", peorExacto, menorRetraso, peorRetraso, periodo, deriva));
            Check(peorExacto < 0.05, "la aurora avanza lo que avanza su rejilla, cuadro a cuadro (sin redondear al reloj gordo)");
            Check(menorRetraso > -0.05 && peorRetraso < periodo + 3.0, "y cada muestra de la rejilla cae sobre la hora fina (como mucho un periodo por detras)");
            Check(deriva < periodo + 3.0, "y en una ventana larga la aurora avanza lo que pasa de verdad (con el reloj gordo se iria ~7.8 ms por cuadro)");
        }

        MethodInfo mBeg = TS.GetMethod("Begin", BindingFlags.Instance | BindingFlags.Public);
        if (mBeg != null)
        {
            List<string> l = Codigo.Llamadas(mBeg);
            Check(Codigo.Llama(l, "GpuPanel.AhoraMs"), "el campo de estrellas tambien mide su paso con el reloj fino");
        }
        MethodInfo mSh = TS.GetMethod("UpdateAndDrawShoots", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mSh != null && mSh.GetParameters().Length == 4, "y las estrellas fugaces reciben tambien la hora fina");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
