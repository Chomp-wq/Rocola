using System;
using System.Collections.Generic;
using System.Reflection;

// EL DESLIZADOR DEL DETALLE NATURAL NO PUEDE REHORNEAR EL CIELO EN CADA PIXEL.
//
// El mando es continuo: arrastrarlo de punta a punta suelta un aviso POR CADA UNIDAD, hasta cien.
// Y hornear el mapa de la nebulosa cuesta 23 ms medidos en la pantalla del bar (256x192, que es un
// cuarto de 1024x768). Un cuadro a sesenta por segundo dura 16,7 ms: UN SOLO horneado ya se pasa.
// Cien seguidos son dos segundos y pico de tirones, mas cien veces destruir y crear la textura de la
// tarjeta, que es lo que peor le sienta al controlador.
//
// Aqui se comprueban las dos mitades del arreglo:
//   1. que los avisos se JUNTEN -pocos horneados por segundo, no uno por pixel-,
//   2. que al soltar el ULTIMO valor se hornee igual, que si no el mando mentiria,
//   3. y que el manejador no llame a RepintaFondo, que tira la cache del cielo de la CPU por un
//      mando que no puede tocarla: nebNaturalFuerza solo entra en BakeNatural, que es solo de
//      tarjeta grafica, y el cielo de la CPU usa NebFuerza, que es const.
class Mando
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos;
    static void Bien(string q) { Console.WriteLine("  OK    " + q); }
    static void Mal(string q) { Console.WriteLine("  FALLO " + q); fallos++; }
    static void Exige(bool c, string q) { if (c) Bien(q); else Mal(q); }

    static int Main(string[] argv)
    {
        Assembly a = Assembly.LoadFrom((argv.Length > 0) ? argv[0] : "rocola.exe");
        Type tp = a.GetType("Rocola.GpuPanel");
        Type tf = a.GetType("Rocola.Form1");
        if (tp == null)
        {
            // Perfil sin tarjeta grafica: el mando no existe alli y no hay nada que vigilar.
            Console.WriteLine("  OK    sin tarjeta grafica no hay detalle natural: nada que mirar");
            return 0;
        }

        MethodInfo toca = tp.GetMethod("TocaRehornear", TODO);
        FieldInfo espera = tp.GetField("NebEspera", TODO);
        if (toca == null || espera == null) { Mal("falta TocaRehornear o NebEspera en GpuPanel"); return 1; }
        int ms = (int)espera.GetValue(null);
        Exige(ms >= 60 && ms <= 400, "la espera entre horneados es razonable (" + ms + " ms)");

        // --- 1) UN ARRASTRE DE VERDAD: cien avisos repartidos en 1,6 segundos ---
        // Es la velocidad de un dedo cruzando el mando de lado a lado sin prisa.
        int reloj = 100000, ultimo = reloj - 10000, horneados = 0;
        for (int i = 0; i < 100; i++)
        {
            reloj += 16;   // un aviso por cuadro, que es lo mas rapido que puede llegar
            if ((bool)toca.Invoke(null, new object[] { false, true, reloj, ultimo })) { horneados++; ultimo = reloj; }
        }
        Console.WriteLine("  arrastre de 100 pasos en 1,6 s -> " + horneados + " horneados (" + (horneados * 23) + " ms de horno)");
        Exige(horneados <= 15, "un arrastre entero no pasa de 15 horneados (fueron " + horneados + ", antes eran 100)");
        Exige(horneados >= 4, "pero se sigue viendo al vuelo: mas de 4 en el arrastre (fueron " + horneados + ")");

        // --- 2) CIEN AVISOS EN EL MISMO INSTANTE: uno solo ---
        reloj = 200000; ultimo = reloj - 10000; horneados = 0;
        for (int i = 0; i < 100; i++)
            if ((bool)toca.Invoke(null, new object[] { false, true, reloj, ultimo })) { horneados++; ultimo = reloj; }
        Exige(horneados == 1, "cien avisos en el mismo milisegundo hornean UNA vez (fueron " + horneados + ")");

        // --- 3) AL SOLTAR, EL ULTIMO VALOR SE HORNEA IGUAL ---
        // Sin esto el mando mentiria: te quedarias con el penultimo valor puesto.
        Exige((bool)toca.Invoke(null, new object[] { false, true, ultimo + ms, ultimo }),
              "pasada la espera, la fuerza pendiente entra siempre (el ultimo valor no se pierde)");
        Exige(!(bool)toca.Invoke(null, new object[] { false, false, ultimo + 100000, ultimo }),
              "si no cambio nada no se hornea, por mucho que pase el tiempo");

        // --- 4) EL INTERRUPTOR NO ESPERA: es un clic ---
        Exige((bool)toca.Invoke(null, new object[] { true, false, ultimo + 1, ultimo }),
              "el interruptor rehornea al momento, sin esperar");

        // --- 5) EL RELOJ DA LA VUELTA CADA 24,9 DIAS Y NO PUEDE COLGAR EL MANDO ---
        // TickCount se pasa de golpe a negativo. Sin unchecked, la resta se sale y el mapa se quedaria
        // sin rehornear -o rehorneandose siempre- justo despues de la vuelta. Se mira a los dos lados:
        // 11 ms despues de la vuelta AUN no toca, y 206 ms despues SI.
        int justoAntes = int.MaxValue - 5;
        Exige(!(bool)toca.Invoke(null, new object[] { false, true, int.MinValue + 5, justoAntes }),
              "con el reloj dando la vuelta, 11 ms siguen siendo 11 ms y todavia no toca");
        Exige((bool)toca.Invoke(null, new object[] { false, true, int.MinValue + 200, justoAntes }),
              "y 206 ms siguen siendo 206: pasada la vuelta el mando responde igual");

        // --- 6) EL MANEJADOR NO LLAMA A RepintaFondo ---
        if (tf == null) { Mal("no encuentro Rocola.Form1"); return fallos; }
        List<MethodBase> pagina = Codigo.MetodosConAyudantes(tf, "PaginaFondo");
        Exige(pagina.Count > 0, "encuentro la pagina de Fondo y sus delegados (" + pagina.Count + ")");
        int mirados = 0;
        foreach (MethodBase m in pagina)
        {
            int lee, escribe; Codigo.UsosDeCampo(m, "nebNaturalFuerza", out lee, out escribe);
            if (escribe == 0) continue;
            // El boton de "Fondo como de fabrica" tambien escribe la fuerza, pero es UN clic y ademas
            // devuelve los once interruptores: se reconoce porque toca vinetaSuave, y ese si vale
            // RepintaFondo. El del deslizador no toca ningun interruptor.
            int l2, e2; Codigo.UsosDeCampo(m, "vinetaSuave", out l2, out e2);
            if (e2 > 0) continue;
            mirados++;
            Exige(!Codigo.Llama(m, "RepintaFondo"),
                  "el manejador del deslizador no llama a RepintaFondo (tira el cielo de la CPU por un mando que no lo toca)");
            Exige(Codigo.Llama(m, "AplicaEfectosGpu"),
                  "y si llama a AplicaEfectosGpu, como los demas mandos que son solo de tarjeta");
        }
        Exige(mirados == 1, "hay exactamente un manejador de deslizador que mirar (encontrados " + mirados + ")");

        Console.WriteLine(fallos == 0 ? "  todo bien" : ("  " + fallos + " fallos"));
        return fallos;
    }
}
