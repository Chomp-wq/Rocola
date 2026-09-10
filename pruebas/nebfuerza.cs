using System;
using System.Drawing;
using System.Reflection;

// EL MANDO DE CUANTO DETALLE NATURAL, Y QUE HAGA LO QUE DICE.
//
// El mapa de nubes del detalle natural cubre casi toda la pantalla. A fuerza entera la bruma ancha tapa
// el cielo: el degradado -azul arriba, calido abajo- desaparece y queda una pared de color. Por eso hay
// un mando.
//
// Lo que se comprueba aqui no es solo que el numero baje. Es que baje LA BRUMA y no los picos: al
// bajarlo tiene que caer el alfa MEDIO y, sobre todo, tienen que caer los pixeles muy cargados. Si
// bajara todo por igual la nube saldria igual de tapada pero mas oscura, que no arregla nada. La
// diferencia esta en que la fuerza multiplica el valor y NO el tope; sin eso, esta prueba falla.
class NebFuerza
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static void Mide(Bitmap b, out double medio, out double cargados)
    {
        long suma = 0; int altos = 0;
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
            { int a = b.GetPixel(x, y).A; suma += a; if (a > 100) altos++; }
        int n = b.Width * b.Height;
        medio = suma / (double)n; cargados = 100.0 * altos / n;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = null, TP = null;
        foreach (Type t in asm.GetTypes()) { if (t.Name == "Form1") TF = t; if (t.Name == "GpuPanel") TP = t; }
        if (TF == null) { Console.WriteLine("  FALLO  no se encontro Form1"); return 1; }
        if (TP == null)
        {
            // El detalle natural es solo de tarjeta grafica: en el perfil sin ella no hay nada que medir,
            // y eso ES el resultado. Se dice en voz alta: una bateria que no saca ni una linea OK se da
            // por fallida, y hace bien.
            Console.WriteLine("  OK     este perfil no dibuja por tarjeta grafica: aqui no hay detalle natural que medir");
            return 0;
        }

        Console.WriteLine("\n  1) EL MANDO EXISTE Y SE PUEDE GUARDAR\n");
        FieldInfo fF = TF.GetField("nebNaturalFuerza", TODO);
        Check(fF != null, "hay un mando para la fuerza del detalle natural (nebNaturalFuerza)");
        MethodInfo mSan = TF.GetMethod("FuerzaNatural", TODO);
        Check(mSan != null, "y se lee del archivo por una funcion que lo acota (FuerzaNatural)");
        if (fF == null || mSan == null) { Console.WriteLine("\n  " + Math.Max(1, fallos) + " FALLO(S)\n"); return Math.Max(1, fallos); }

        // Un numero de un archivo puede venir de cualquier manera: se acota al leerlo, no donde se usa.
        Check((float)mSan.Invoke(null, new object[] { "50" }) == 0.5f, "un 50 del archivo es media fuerza");
        Check((float)mSan.Invoke(null, new object[] { "900" }) == 1f, "un disparate por arriba se queda en el tope");
        Check((float)mSan.Invoke(null, new object[] { "-9" }) == 0f, "y uno por abajo, en cero");
        float pordef = (float)mSan.Invoke(null, new object[] { "no es un numero" });
        Check(pordef > 0f && pordef < 1f, "y una linea rota vuelve al valor de fabrica, que NO es el tope ("
              + (int)Math.Round(pordef * 100) + " %)");

        Console.WriteLine("\n  2) BAJARLO QUITA LA BRUMA, NO SOLO OSCURECE\n");
        MethodInfo mNat = TP.GetMethod("BakeNatural", TODO);
        Check(mNat != null, "se puede hornear el mapa del detalle natural");
        if (mNat == null) { Console.WriteLine("\n  " + Math.Max(1, fallos) + " FALLO(S)\n"); return Math.Max(1, fallos); }

        float guardado = (float)fF.GetValue(null);
        double medioAlto, cargadosAlto, medioBajo, cargadosBajo;
        try
        {
            fF.SetValue(null, 1f);
            using (Bitmap b = (Bitmap)mNat.Invoke(null, new object[] { 200, 120 })) Mide(b, out medioAlto, out cargadosAlto);
            fF.SetValue(null, 0.5f);
            using (Bitmap b = (Bitmap)mNat.Invoke(null, new object[] { 200, 120 })) Mide(b, out medioBajo, out cargadosBajo);
        }
        finally { fF.SetValue(null, guardado); }

        Console.WriteLine(string.Format("     a fuerza entera: alfa medio {0:0.0}, pixeles muy cargados {1:0.0}%", medioAlto, cargadosAlto));
        Console.WriteLine(string.Format("     a media fuerza : alfa medio {0:0.0}, pixeles muy cargados {1:0.0}%", medioBajo, cargadosBajo));
        Check(medioBajo < medioAlto * 0.75, "a media fuerza el cielo se ve bastante mas (el alfa medio cae)");
        Check(cargadosAlto > 5.0, "a fuerza entera hay bastante pantalla tapada del todo (que es de lo que te quejabas)");
        Check(cargadosBajo < cargadosAlto / 4.0,
              "y bajarlo quita sobre todo LO TAPADO: los muy cargados caen mucho mas que el resto");

        Console.WriteLine("\n  3) Y EL MAPA SE REHACE AL MOVERLO\n");
        // El mapa lleva la fuerza cocida dentro: si nadie lo tira al mover el mando, el cambio no se veria
        // hasta reiniciar la rocola, y quien lo mueve creeria que no hace nada.
        bool rehace = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TP))
            if (Codigo.LeeCampo(new System.Collections.Generic.List<MethodBase>(new MethodBase[] { m }), "nebFuerzaDe"))
                rehace = true;
        Check(rehace, "el dibujo se acuerda de con que fuerza horneo, para rehacerlo cuando cambie");

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }
}
