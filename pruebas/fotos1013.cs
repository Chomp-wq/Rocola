using System;
using System.Drawing;
using System.Reflection;

// LAS FOTOS DEL 13-10-2026 (tanda de cuatro): el cometa fuera (lo mira 'cielovivo'), la ayuda que se despide sola a los
// 15 s, el reflejo de la aurora pegado a la raya del horizonte y «EN ESTA ROCOLA» entero en el modo atraccion.
// Con formulario aislado y sin tarjeta grafica: las cuentas y los dibujos que se hornean, no la pantalla del bar.
class Fotos1013
{
    static int ok, mal;
    static void Check(bool c, string s) { Console.WriteLine((c ? "  OK    " : "  FALLO ") + s); if (c) ok++; else mal++; }
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        Type TF = asm.GetType("Rocola.Form1", true);

        if (TF.GetMethod("PasoDespedida", T) == null)
        {   // La ayuda, la atraccion y la aurora solo existen con tarjeta grafica (el dibujo por CPU esta congelado).
            Check(TF.GetMethod("AbreAsistente", T) == null && TF.GetMethod("HorneaFichaVitrina", T) == null,
                  "sin tarjeta grafica no hay ayuda ni modo atraccion de tarjeta: no hay nada que mirar aqui");
            Console.WriteLine(mal == 0 ? "PASS fotos1013 (" + ok + " comprobaciones; sin tarjeta grafica)" : "FAIL fotos1013: " + mal + " fallos");
            return mal;
        }
        Console.WriteLine("\n  1) LA AYUDA SE DESPIDE SOLA\n");
        MethodInfo paso = TF.GetMethod("PasoDespedida", T);
        int tras = (int)TF.GetField("AdiosTrasMs", T).GetValue(null), dura = (int)TF.GetField("AdiosMs", T).GetValue(null);
        Check(tras == 15000, "a los 15 s sin tocar nada");
        Check(dura >= 1500 && dura <= 3000, "se despide un par de segundos (" + dura + " ms) y se cierra");
        Func<int, int, int, int> P = delegate (int ahora, int act, int adios) { return (int)paso.Invoke(null, new object[] { ahora, act, adios }); };
        Check(P(1000 + 14999, 1000, 0) == 0, "a los 14,9 s todavia no");
        Check(P(1000 + 15000, 1000, 0) == 1, "a los 15 s empieza a despedirse");
        Check(P(16000 + dura - 1, 1000, 16000) == 0 && P(16000 + dura, 1000, 16000) == 2, "y se cierra al acabar la despedida");
        Check(P(16500, 16200, 16000) == 3, "si alguien toca algo mientras se despide, se queda");
        Check(P(int.MinValue + 20000, int.MaxValue - 5000, 0) == 1, "aunque el reloj de Windows de la vuelta");
        Check(Codigo.Llama(Codigo.ConAyudantes(TF, "TickAsistenteGpu"), "PasoDespedida"), "el reloj de la ayuda lo usa");
        Bitmap globo = (Bitmap)TF.GetMethod("PintaAdiosAsistente", T).Invoke(null, new object[] { 640, 300 });
        bool tinta = (bool)TF.GetMethod("Tinta", T).Invoke(null, new object[] { globo, 0, 0, 0, 0 });
        Check(globo.Width == 640 && globo.Height == 300 && tinta, "el globo del adios se dibuja");
        globo.Dispose();
        Check(Codigo.Llama(Codigo.ConAyudantes(TF, "EnsureAsistentePanel"), "PintaAdiosAsistente"), "y es el que se ensena al despedirse");

        Console.WriteLine("\n  2) «EN ESTA ROCOLA» ENTERO\n");
        Type TFicha = asm.GetType("Rocola.FichaVitrina", true);
        MethodInfo hornea = TF.GetMethod("HorneaFichaVitrina", T), tintaM = TF.GetMethod("Tinta", T);
        foreach (int h in new int[] { 600, 768, 1080, 1440 })
        {
            object d = Activator.CreateInstance(TFicha);
            TFicha.GetField("Nombre").SetValue(d, "Monarcas"); TFicha.GetField("Artista").SetValue(d, "Bebu Silvetti");
            TFicha.GetField("Genero").SetValue(d, "Jazz"); TFicha.GetField("Canciones").SetValue(d, 12);
            using (Bitmap b = (Bitmap)hornea.Invoke(null, new object[] { d, 520, h }))
            using (Bitmap fila = b.Clone(new Rectangle(0, 0, b.Width, Math.Max(1, (int)(h * .019f * 1.4f))), b.PixelFormat))
            {
                object[] a = { fila, 0, 0, 0, 0 };
                bool hay = (bool)tintaM.Invoke(null, a);
                int ya = (int)a[2], xa = (int)a[1], yb = (int)a[4];
                Check(hay && ya == 0 && xa >= 0 && yb - ya >= (int)(h * .019f * .5f),
                      "a " + h + " de alto: el letrero empieza en la fila 0 de la ficha, con su altura entera (" + (yb - ya + 1) + " px)");
            }
        }
        Check(Codigo.Llama(Codigo.ConAyudantes(TF, "HorneaFichaVitrina"), "EspaciadoEntero"), "la ficha lo dibuja con margen y lo pega entero");

        Console.WriteLine("\n  3) EL REFLEJO, PEGADO A LA RAYA\n");
        Type TPanel = asm.GetType("Rocola.GpuPanel", false);
        if (TPanel == null) Console.WriteLine("     (sin tarjeta grafica no hay aurora ni reflejo)");
        else
        {
            MethodInfo dr = TPanel.GetMethod("DibujaReflejoAurora", T);
            System.Collections.Generic.List<string> il = Codigo.IlConNombres(dr);
            bool resta = false; foreach (string s in il) if (s.StartsWith("59")) resta = true;   // sub: base - lo que se alza la raya
            Check(resta && Codigo.Llama(dr, "AurReflejo"), "el reflejo nace en la raya del horizonte, no en el pie del relleno");
        }

        Console.WriteLine(mal == 0 ? "PASS fotos1013 (" + ok + " comprobaciones; formulario aislado, no la pantalla del bar)" : "FAIL fotos1013: " + mal + " fallos");
        return mal;
    }
}
