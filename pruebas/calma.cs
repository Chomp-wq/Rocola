using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

// LA AURORA SE CALMA MAS MIENTRAS ALGUIEN ESTA ELIGIENDO.
//
// La aurora es lo mas grande y lo que mas se mueve de toda la pantalla. Mientras alguien intenta LEER una
// lista de canciones es justo lo que mas estorba -y a las horas en que un cliente ya lleva unas cuantas
// encima, mas todavia-. Las estrellas ya bajaban al 45%; la aurora bajaba solo al 65%, o sea casi nada.
//
// Ahora baja al 35% Y ADEMAS se DIFUMINA: lo afilado -el filo de neon y la raya del horizonte, que es lo que
// tira de la vista- se va casi del todo y queda una mancha de color suave. Aqui se comprueba con los numeros
// que de verdad salen de la aurora, no con la intencion.
class Calma
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TP, TS, TAu;
    static object pan, au;
    static MethodInfo mAur;

    static float Cte(Type t, string n)
    {
        FieldInfo f = t.GetField(n, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (f == null) return float.NaN;
        object v = f.GetRawConstantValue();
        return (v is int) ? (int)v : (float)v;
    }
    static float[] Vec(string n) { return (float[])TP.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pan); }
    static float Media(float[] v) { if (v == null || v.Length == 0) return 0f; float s = 0; for (int i = 0; i < v.Length; i++) s += v[i]; return s / v.Length; }

    // Dibuja un cuadro de aurora con esa atenuacion y devuelve el brillo medio del RELLENO y el del FILO.
    static void Cuadro(float dim, float dimFilo, out float relleno, out float filo)
    {
        TAu.GetField("Dim").SetValue(au, dim);
        TAu.GetField("DimFilo").SetValue(au, dimFilo);
        mAur.Invoke(pan, new object[] { au });
        relleno = Media(Vec("aurA"));                       // opacidad del relleno, punto a punto
        filo = Media(Vec("aurNi")) + Media(Vec("aurLi"));   // brillo del neon + el de la raya del horizonte
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in asm.GetTypes())
        {
            if (t.Name == "GpuPanel") TP = t;
            if (t.Name == "GpuStarfield") TS = t;
            if (t.Name == "GpuAurora") TAu = t;
        }
        Check(TP != null && TS != null && TAu != null, "la aurora esta donde tiene que estar");
        if (TP == null || TAu == null) return 1;

        Console.WriteLine("\n  1) HAY UN MANDO APARTE PARA LO AFILADO\n");
        Check(TAu.GetField("DimFilo") != null, "la aurora se puede atenuar por partes: el relleno por un lado y el filo por otro");
        float nav = Cte(TF, "AurNavLevel"), navF = Cte(TF, "AurNavFilo"), estrellas = Cte(TF, "BgDimLevel");
        Console.WriteLine("     mientras se navega:  estrellas al " + (estrellas * 100).ToString("0") + "%,  relleno de la aurora al "
                          + (nav * 100).ToString("0") + "%,  su filo al " + (navF * 100).ToString("0") + "% de eso");
        Check(!float.IsNaN(nav) && !float.IsNaN(navF), "los dos niveles estan escritos donde se pueden ver y cambiar");
        Check(nav > 0.15f && nav < 0.55f, "el relleno baja de verdad, pero no desaparece   [" + (nav * 100).ToString("0") + "%]");
        Check(navF < nav, "y el filo baja MAS que el relleno: eso es lo que se lee como difuminado");
        // Como estaba antes: Dim = 0.36 + 0.64*bgDim, y el filo iba con el mismo numero.
        float antes = 0.36f + 0.64f * estrellas;
        Console.WriteLine("     antes el relleno se quedaba en el " + (antes * 100).ToString("0") + "% y el filo TAMBIEN");
        Check(nav < antes - 0.15f, "la aurora se calma bastante mas que antes   [" + (antes * 100).ToString("0") + "% -> " + (nav * 100).ToString("0") + "%]");

        Console.WriteLine("\n  2) Y ASI SALE DE VERDAD DE LA AURORA\n");
        object apagado = FormatterServices.GetUninitializedObject(TS);   // sin tarjeta: las llamadas de dibujo no hacen nada
        pan = Activator.CreateInstance(TP, true);
        ((Control)pan).SetBounds(0, 0, 1024, 768);
        TP.GetField("stars", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pan, apagado);
        mAur = TP.GetMethod("DrawAurora", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mAur != null, "se le puede pedir un cuadro de aurora");
        if (mAur == null) return 1;

        au = Activator.CreateInstance(TAu);
        float[] B = new float[64];
        for (int i = 0; i < B.Length; i++) B[i] = 0.35f + 0.5f * (float)Math.Abs(Math.Sin(i * 0.29));
        TAu.GetField("Has").SetValue(au, true); TAu.GetField("B").SetValue(au, B);
        TAu.GetField("BaseY").SetValue(au, 672f); TAu.GetField("Alto").SetValue(au, 300f);
        TAu.GetField("Calma").SetValue(au, 0f);
        TAu.GetField("Rgb").SetValue(au, (uint)0x7A3CC8); TAu.GetField("Rgb2").SetValue(au, (uint)0x3C6AC8);
        TAu.GetField("RgbA").SetValue(au, (uint)0xC85AA0); TAu.GetField("Rgb2A").SetValue(au, (uint)0x8A5AC8);

        float relQuieto, filQuieto, relNav, filNav, relAntes, filAntes;
        Cuadro(1f, 1f, out relQuieto, out filQuieto);                 // nadie toca nada
        Cuadro(nav, navF, out relNav, out filNav);                    // alguien esta eligiendo (AHORA)
        Cuadro(antes, 1f, out relAntes, out filAntes);                // alguien esta eligiendo (ANTES)

        Console.WriteLine("     con la rocola quieta:      relleno " + relQuieto.ToString("0.000") + "   filo " + filQuieto.ToString("0.000"));
        Console.WriteLine("     eligiendo, como estaba:    relleno " + relAntes.ToString("0.000") + "   filo " + filAntes.ToString("0.000")
                          + "   (" + (relAntes / relQuieto * 100).ToString("0") + "% y " + (filAntes / filQuieto * 100).ToString("0") + "%)");
        Console.WriteLine("     eligiendo, como esta AHORA: relleno " + relNav.ToString("0.000") + "   filo " + filNav.ToString("0.000")
                          + "   (" + (relNav / relQuieto * 100).ToString("0") + "% y " + (filNav / filQuieto * 100).ToString("0") + "%)");
        Check(relNav < relAntes * 0.75f, "el relleno se calma bastante mas que antes");
        Check(filNav < filAntes * 0.40f, "y el filo de neon se va casi del todo: se queda la mancha de color, sin lineas duras");
        Check(filNav / filQuieto < relNav / relQuieto * 0.5f, "el filo baja al menos el DOBLE que el relleno (por eso se ve difuminada, no solo mas oscura)");

        Console.WriteLine("\n  3) EN REPOSO VUELVE ENTERA\n");
        float rel2, fil2;
        Cuadro(1f, 1f, out rel2, out fil2);
        Check(Math.Abs(rel2 - relQuieto) < 0.0005f && Math.Abs(fil2 - filQuieto) < 0.0005f,
              "en cuanto se deja de tocar, la aurora vuelve EXACTAMENTE como estaba (no se queda apagada)");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
