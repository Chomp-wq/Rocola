using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

// EL RELEVO: QUE SE VEA QUE LA CANCION CAMBIO.
//
// El cubito de la barra de abajo lleva la portada de lo que suena. Al entrar la siguiente cancion, esa
// portada se cambiaba DE GOLPE: un cuadro con una y el siguiente con otra. A dos metros, de noche y con
// los amigos hablando, eso no es un cambio: es que de pronto hay otra cosa ahi y nadie sabe desde cuando.
//
// Ahora el cubo da MEDIA VUELTA y la portada nueva aparece en el camino. Y el momento del cambio no es
// uno cualquiera: a la mitad del giro el cubo lleva 90 grados, y como las cuatro caras llevan la misma
// imagen, 90 grados dejan el cubo con EXACTAMENTE la misma silueta que al empezar. Asi el cambio no
// puede dar un salto de forma -solo cambia la imagen-, y cambia justo cuando el cubo va mas rapido,
// que es cuando menos se le mira la cara.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE EL GIRO SEA DE MEDIA VUELTA Y SE ACABE. Un giro que no cierra deja el cubo torcido para
//      siempre respecto del de arriba, que sigue su propio ritmo.
//   2) QUE LA PORTADA CAMBIE UNA SOLA VEZ Y A LA MITAD. Ni al principio (seria el cambio de golpe de
//      antes, con un giro encima) ni al final (el giro no habria servido para nada).
//   3) QUE CAMBIE EN EL PUNTO MAS RAPIDO del giro.
//   4) QUE FUERA DEL RELEVO EL CUBO SIGA GIRANDO COMO SIEMPRE, ni mas ni menos.
//   5) QUE LA ROCOLA LO USE, y que no le quede ningun sitio donde siga cambiando la portada a mano.
class Vuelta
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TCubo;

    static MethodBase Met(Type t, string n)
    {
        if (t == null) return null;
        foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }
    static object Campo(object o, string n)
    {
        FieldInfo f = TCubo.GetField(n, TODO);
        return (f == null) ? null : f.GetValue(o);
    }
    static double Angulo(object c) { return Convert.ToDouble(Campo(c, "Angle")); }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes())
        {
            if (t.Name == "Form1") TF = t;
            if (t.Name == "CubeBox") TCubo = t;
        }
        if (TF == null || TCubo == null) { Console.WriteLine("  FALLO  no se encontro el cubo"); return 1; }

        Console.WriteLine("\n  1) LAS PIEZAS\n");
        MethodInfo mRel = (MethodInfo)Met(TCubo, "Relevo");
        MethodInfo mAv = (MethodInfo)Met(TCubo, "Avanza");
        PropertyInfo pGir = TCubo.GetProperty("Girando", TODO);
        Check(mRel != null, "el cubo sabe hacer el relevo de una portada a otra (Relevo)");
        Check(mAv != null, "y quien lleva su giro es el propio cubo (Avanza)");
        Check(pGir != null, "y se le puede preguntar si esta en mitad de un relevo (Girando)");
        if (mRel == null || mAv == null || pGir == null) { Console.WriteLine("\n  " + (fallos) + " FALLO(S)"); return Math.Max(fallos, 1); }

        object cubo = Activator.CreateInstance(TCubo, true);
        FieldInfo fCover = TCubo.GetField("Cover", TODO);
        Bitmap A = new Bitmap(8, 8), B = new Bitmap(8, 8);
        fCover.SetValue(cubo, A);

        Console.WriteLine("\n  2) FUERA DEL RELEVO, EL GIRO DE SIEMPRE\n");
        double a0 = Angulo(cubo);
        mAv.Invoke(cubo, new object[] { 0.06 });
        double d1 = Angulo(cubo) - a0;
        Check(Math.Abs(d1 - 0.06) < 1e-9, "sin relevo, un paso de 0.06 avanza 0.06 y nada mas");
        Check(!(bool)pGir.GetValue(cubo, null), "y no hay ningun relevo en marcha");

        Console.WriteLine("\n  3) LA MEDIA VUELTA\n");
        double ini = Angulo(cubo);
        mRel.Invoke(cubo, new object[] { B });
        Check((bool)pGir.GetValue(cubo, null), "al pedir el relevo, el cubo empieza a girar");
        // Se le da paso CERO a proposito: asi lo que avanza el angulo es SOLO la media vuelta y se
        // puede medir sin que el giro de siempre la ensucie.
        List<double> ts = new List<double>(), angs = new List<double>();
        int cambio = -1;
        int t0 = Environment.TickCount, tope = t0 + 3000;
        while (Environment.TickCount < tope)
        {
            mAv.Invoke(cubo, new object[] { 0.0 });
            ts.Add(Environment.TickCount - t0);
            angs.Add(Angulo(cubo) - ini);
            if (cambio < 0 && !object.ReferenceEquals(fCover.GetValue(cubo), A)) cambio = ts.Count - 1;
            if (!(bool)pGir.GetValue(cubo, null)) break;
            System.Threading.Thread.Sleep(3);
        }
        double total = angs[angs.Count - 1];
        double dur = ts[ts.Count - 1];
        Console.WriteLine("     el giro duro " + dur + " ms y avanzo " + total.ToString("0.000") + " radianes (media vuelta son " + Math.PI.ToString("0.000") + ")");
        Check(!(bool)pGir.GetValue(cubo, null), "el relevo se acaba solo");
        Check(Math.Abs(total - Math.PI) < 0.02, "y lo que gira es MEDIA VUELTA exacta, ni mas ni menos");
        Check(dur > 200 && dur < 900, "y dura lo que dura un gesto (" + dur + " ms)");
        Check(object.ReferenceEquals(fCover.GetValue(cubo), B), "al acabar, la portada puesta es la nueva");

        Console.WriteLine("\n  4) Y LA PORTADA CAMBIA A LA MITAD, NO AL PRINCIPIO NI AL FINAL\n");
        Check(cambio > 0, "la portada llega a cambiar durante el giro");
        if (cambio > 0)
        {
            double frac = ts[cambio] / dur;
            Console.WriteLine("     cambio al " + (frac * 100).ToString("0") + "% del giro, con " + angs[cambio].ToString("0.00") + " radianes girados");
            Check(frac > 0.40 && frac < 0.62, "cambia a la mitad del giro (" + (frac * 100).ToString("0") + "%)");
            // 90 grados: la silueta del cubo vuelve a ser la misma que al empezar, asi que el cambio
            // no puede dar un salto de forma.
            Check(Math.Abs(angs[cambio] - Math.PI / 2) < 0.25, "y con el cubo a 90 grados, donde su silueta es la misma que al empezar");
            // Y ES EL PUNTO MAS RAPIDO. Se mide la velocidad en cada tramo y se mira cual es la mayor.
            int rapido = 1; double vmax = -1;
            for (int i = 1; i < angs.Count; i++)
            {
                double dt = ts[i] - ts[i - 1];
                if (dt <= 0) continue;
                double v = (angs[i] - angs[i - 1]) / dt;
                if (v > vmax) { vmax = v; rapido = i; }
            }
            double fr = ts[rapido] / dur;
            Console.WriteLine("     lo mas rapido del giro va al " + (fr * 100).ToString("0") + "%");
            Check(Math.Abs(fr - frac) < 0.18, "y ese es justo el punto mas rapido del giro: es cuando menos se le mira la cara");
        }

        Console.WriteLine("\n  5) Y DESPUES, COMO SI NADA\n");
        double a2 = Angulo(cubo);
        mAv.Invoke(cubo, new object[] { 0.06 });
        Check(Math.Abs((Angulo(cubo) - a2) - 0.06) < 1e-9, "acabado el relevo, el cubo vuelve a girar como siempre");

        Console.WriteLine("\n  6) LA ROCOLA LO USA\n");
        MethodBase tick = Met(TF, "AnimTimer_Tick");
        Check(tick != null && Codigo.Llama(Codigo.ConAyudantes(TF, "AnimTimer_Tick"), "Avanza"),
              "el reloj de la pantalla ya no le mueve el angulo a mano: se lo pide al cubo");
        bool lopide = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            if (Codigo.Llama(m, "Relevo")) lopide = true;
        Check(lopide, "y al cambiar de cancion, alguien pide el relevo");

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
