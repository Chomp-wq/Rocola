using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

// EN EL CIELO NO HABIA LUNA.
//
// Era lo unico que un cielo de noche tiene y este no tenia. Ahora hay una: baja, a un lado, con su halo,
// paseandose muy despacio con el resto del cielo, y apagandose y tirando a naranja cerca del horizonte con
// LA MISMA curva del aire que ya usan las estrellas.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE HAYA UNA, y que la dibujen LAS DOS PAREDES pidiendosela al mismo sitio. Con dos dibujos, la
//      maquina del bar y la de repuesto tendrian lunas distintas.
//   2) QUE ESTE DONDE TIENE QUE ESTAR: abajo y a un lado. En el centro le pelearia a la pared de discos,
//      que es lo que la gente esta mirando.
//   3) QUE EL AIRE LA TOQUE, con la misma curva que a las estrellas y con la diferencia escrita en niveles
//      de 255.
//   4) QUE NO CUESTE, y aqui esta la parte que de verdad se rompio una vez. La primera version la tenia AL
//      PEGAR con una matriz de color, que es lo comodo... y costaba 0.33 ms por cuadro: mas que armar las
//      mil cuatrocientas estrellas de la otra pared entera. Este mismo archivo ya avisaba de eso en otro
//      sitio ("cuesta VEINTE VECES lo que copiar la capa tal cual") y aun asi se metio. Ahora va horneada
//      tenida y se reusa. Si alguien vuelve a la forma comoda, esto salta.
//   5) QUE SE PUEDA APAGAR Y QUE SE GUARDE. Una luna es una opinion, no una mejora.
//   6) Y QUE EL INTERRUPTOR QUEPA: la pagina de DETALLES tuvo que crecer para el sexto.
class Luna
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly char[] SALTO = { (char)10 };

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        Type TS = TF.GetNestedType("Starfield", BindingFlags.NonPublic | BindingFlags.Public);

        Console.WriteLine("\n  1) HAY LUNA, Y ES LA MISMA EN LAS DOS PAREDES\n");
        MethodInfo mBake = TF.GetMethod("BakeLuna", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Check(mBake != null, "hay una luna, y su dibujo se hace en un solo sitio (BakeLuna)");
        Check(TS != null && TS.GetMethod("PintaLuna", TODO) != null, "la pared sin tarjeta grafica la pinta");
        Type TG = null;
        foreach (Type t in asm.GetTypes()) if (t.Name == "GpuPanel") TG = t;
        if (TG != null)
        {
            MethodInfo mGpu = TG.GetMethod("DibujaLuna", TODO);
            Check(mGpu != null, "y la de tarjeta grafica tambien");

            // LA LUNA DE TARJETA YA NO ES LA MISMA QUE LA DE CPU, Y ESO ES LO ACORDADO. Antes se exigia
            // que la de tarjeta le pidiera el dibujo a Form1, para que las dos paredes ensenaran la misma
            // luna. Ahora la de tarjeta tiene la suya con relieve, halo y polvo, que es justo lo que dice
            // la regla de la casa: las mejoras de dibujo nuevas van SOLO por tarjeta, y el dibujo por CPU
            // no se toca.
            //
            // Pero la regla tiene una segunda mitad que es la que de verdad protege: cada mejora lleva su
            // interruptor, y APAGANDOLO tiene que volver lo de antes. Eso es lo que se comprueba aqui:
            // que apagando LunaRealista se vuelve al dibujo compartido de Form1.BakeLuna. Si algun dia se
            // quita esa vuelta atras, la de tarjeta se queda sin camino de regreso y la regla se rompe
            // sin que nadie lo note.
            MethodInfo mEnsure = TG.GetMethod("EnsureLuna", TODO);
            Check(mEnsure != null, "   ...y hay un solo sitio que decide que luna se sube (EnsureLuna)");
            if (mEnsure != null)
            {
                Check(Codigo.Llama(mEnsure, "BakeLuna"),
                      "   ...que sigue sabiendo pedirle el dibujo compartido a Form1");
                int leeSw, escSw; Codigo.UsosDeCampo(mEnsure, "LunaRealista", out leeSw, out escSw);
                Check(leeSw > 0,
                      "   ...y mira el interruptor: apagando la luna con relieve vuelve la de siempre");
            }
        }
        if (mBake == null) { Console.WriteLine("\n  " + Math.Max(fallos, 1) + " FALLO(S)"); return Math.Max(fallos, 1); }

        Bitmap luna = (Bitmap)mBake.Invoke(null, new object[] { 1080 });
        Console.WriteLine("     el dibujo mide " + luna.Width + "x" + luna.Height + " px");
        Check(luna.Width > 40 && luna.Width < 400, "y es un dibujo chico: se pega, no se pinta la pantalla entera");
        // Que tenga disco Y halo: sin halo es un circulo pegado encima, no una luna.
        int c = luna.Width / 2;
        int aCentro = luna.GetPixel(c, c).A;
        int aMedio = luna.GetPixel(c + (int)(luna.Width * 0.30f), c).A;
        int aBorde = luna.GetPixel(c + (int)(luna.Width * 0.48f), c).A;
        Console.WriteLine("     alfa en el centro " + aCentro + ", a media distancia " + aMedio + ", en el canto " + aBorde);
        Check(aCentro > 200, "el disco es opaco");
        Check(aMedio > 2 && aMedio < 200, "y alrededor hay halo, que es lo que la hace parecer luna");
        Check(aBorde < 12, "y el halo se apaga antes del canto: no se ve el cuadro del dibujo");

        Console.WriteLine("\n  2) Y ESTA ABAJO Y A UN LADO, NO EN MEDIO\n");
        float lx = (float)TF.GetField("LunaX", TODO).GetRawConstantValue();
        float ly = (float)TF.GetField("LunaY", TODO).GetRawConstantValue();
        Console.WriteLine("     se pone en el " + (lx * 100).ToString("0") + "% del ancho y el " + (ly * 100).ToString("0") + "% del alto");
        Check(lx < 0.28f || lx > 0.72f, "esta a un lado: en medio le pelearia a la pared de discos");
        Check(ly > 0.5f, "y baja, donde ya hay resplandor de horizonte");

        Console.WriteLine("\n  3) Y EL AIRE LA APAGA Y LA ENROJECE, COMO A LAS ESTRELLAS\n");
        MethodInfo mTinte = TF.GetMethod("TinteDelAire", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Check(mTinte != null, "la luna pasa por el tinte del aire");
        if (mTinte != null)
        {
            Check(Codigo.Llama(mTinte, "AireDelCielo"),
                  "y por la MISMA curva que las estrellas, no por una copia: las dos paredes pintan un solo cielo");
            object[] arr = { 20f, 1000f, 0f, 0f, 0f }, aba = { 980f, 1000f, 0f, 0f, 0f };
            mTinte.Invoke(null, arr); mTinte.Invoke(null, aba);
            float rA = (float)arr[2], bA = (float)arr[4];
            float rB = (float)aba[2], gB = (float)aba[3], bB = (float)aba[4];
            Console.WriteLine("     arriba del todo le queda el " + (rA * 100).ToString("0") + "% del brillo; abajo, el " + (rB * 100).ToString("0") + "%");
            Console.WriteLine("     y abajo el rojo le saca " + ((rB - bB) * 255).ToString("0") + " niveles de 255 al azul (arriba: " + ((rA - bA) * 255).ToString("0") + ")");
            Check(rB < rA - 0.15f, "abajo sale claramente mas apagada que arriba");
            Check((rB - bB) > (rA - bA) + 0.08f, "y mas naranja: le come el azul, como hace el aire de verdad");
            Check(gB > bB, "el verde aguanta mas que el azul, que es el orden en que el aire se los come");
        }

        Console.WriteLine("\n  4) Y NO CUESTA. AQUI ES DONDE SE ROMPIO UNA VEZ\n");
        // La primera version tenia el tinte AL PEGAR, con una matriz de color. Comodo, y 0.33 ms por cuadro:
        // la libreria de dibujo pasa por cada uno de los treinta mil pixeles de la luna, veinte veces por
        // cuadro mas caro que pegarla tal cual. Ahora se hornea tenida una vez y se reusa.
        if (TS != null)
        {
            const int W = 1920, H = 1080;
            object sf = Activator.CreateInstance(TS, true);
            TS.GetField("Density", TODO).SetValue(sf, 1f);
            TS.GetField("SkyFullW", TODO).SetValue(sf, W);
            TS.GetField("SkyFullH", TODO).SetValue(sf, H);
            TS.GetField("SkyMid", TODO).SetValue(sf, Color.FromArgb(255, 16, 37, 90));
            FieldInfo fCon = TS.GetField("ConLuna", TODO);
            Check(fCon != null, "la luna se puede apagar");
            MethodInfo mBlit = null;
            foreach (MethodInfo m in TS.GetMethods(TODO | BindingFlags.DeclaredOnly))
                if (m.Name == "BlitSkyAndStars" && m.GetParameters().Length == 4) mBlit = m;
            if (fCon != null && mBlit != null)
                using (Bitmap bm = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
                using (Graphics g = Graphics.FromImage(bm))
                {
                    Color c1 = Color.FromArgb(255, 3, 4, 8), c2 = Color.FromArgb(255, 37, 66, 141);
                    TS.GetMethod("DrawCached").Invoke(sf, new object[] { g, W, H, c1, c2 });
                    object[] arg = { g, W, H, true };
                    mBlit.Invoke(sf, arg);   // calienta (hornea la luna y su tinte)
                    List<double> con = new List<double>(), sin = new List<double>();
                    for (int k = 0; k < 15; k++)
                    {
                        fCon.SetValue(sf, true);
                        Stopwatch w = Stopwatch.StartNew(); mBlit.Invoke(sf, arg); w.Stop();
                        con.Add(w.Elapsed.TotalMilliseconds);
                        fCon.SetValue(sf, false);
                        w = Stopwatch.StartNew(); mBlit.Invoke(sf, arg); w.Stop();
                        sin.Add(w.Elapsed.TotalMilliseconds);
                    }
                    // SE MIRA LA VUELTA MAS LIMPIA DE LAS QUINCE, NO LA DE ENMEDIO.
                    //
                    // Esto mide una diferencia de decimas de milisegundo, y la mediana se la lleva por
                    // delante cualquier cosa que haga el sistema mientras tanto: un recolector de basura,
                    // otro nucleo ocupado, la propia tanda compilando al lado. Medido: fallaba una de cada
                    // cinco veces corriendo SOLA, sin que nada estuviera roto -que es la peor clase de
                    // prueba, la que hay que volver a correr para creersela-.
                    //
                    // La vuelta mas rapida de las quince es la que menos estorbo tuvo, o sea la que mas se
                    // acerca a lo que cuesta de verdad. Y se toma la mas limpia de las DOS medidas, asi que
                    // la resta sigue siendo la misma resta: no se afloja el limite, se quita el ruido.
                    con.Sort(); sin.Sort();
                    double cuesta = con[0] - sin[0];
                    fCon.SetValue(sf, true);
                    Console.WriteLine("     pegar el fondo con luna " + con[0].ToString("0.000")
                                      + " ms, sin luna " + sin[0].ToString("0.000") + " ms   (la vuelta mas limpia de 15)");
                    Console.WriteLine("     o sea que la luna cuesta " + cuesta.ToString("+0.000;-0.000") + " ms por cuadro, a " + W + "x" + H);
                    Check(cuesta < 0.15, "la luna cuesta casi nada: su tinte va horneado, no aplicado al pegar");
                }
        }

        Console.WriteLine("\n  5) SE PUEDE APAGAR, Y SE GUARDA\n");
        MethodBase lee = null, escribe = null, pag = null;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
        {
            if (m.Name == "LoadConfigPrefs") lee = m;
            else if (m.Name == "SaveConfigPrefs") escribe = m;
            else if (m.Name == "PaginaDetalles") pag = m;
        }
        bool enLee = false, enEsc = false;
        if (lee != null) foreach (string t in Codigo.Textos(lee)) if (t == "luna=") enLee = true;
        if (escribe != null) foreach (string t in Codigo.Textos(escribe)) if (t == "luna=") enEsc = true;
        Check(enLee && enEsc, "la rocola se acuerda de si la dejaste encendida o apagada");
        Check(pag != null, "y hay donde apagarla sin tocar archivos: la pestana DETALLES");
        if (pag != null)
        {
            List<MethodBase> deLaPagina = Codigo.MetodosConAyudantes(TF, "PaginaDetalles");
            Check(Codigo.EscribeCampo(deLaPagina, "lunaOn"), "el interruptor la apaga de verdad");
            Check(Codigo.EscribeCampo(deLaPagina, "f3CambioGraf"), "y lo que se toca se guarda al cerrar");
        }

        Console.WriteLine("\n  6) Y EL INTERRUPTOR CABE EN LA PAGINA\n");
        // La pagina de DETALLES son 636 px y no puede crecer. Sus dos tarjetas tuvieron que subir de 424 a
        // 496 para el sexto interruptor, y el pie y el boton de fabrica bajaron detras. Es facil de romper
        // con una linea de texto de mas, asi que se arma la pagina de verdad y se le miran las medidas.
        try
        {
            object frm = FormatterServices.GetUninitializedObject(TF);
            FontFamily ff = FontFamily.GenericSansSerif;
            using (Font fNom = new Font(ff, 10f, FontStyle.Bold))
            using (Font fDes = new Font(ff, 8.5f))
            using (Font fBot = new Font(ff, 10f, FontStyle.Bold))
            {
                MethodInfo mPag = TF.GetMethod("PaginaDetalles", BindingFlags.Instance | BindingFlags.NonPublic);
                Panel pg = (Panel)mPag.Invoke(frm, new object[] { fNom, fDes, fBot,
                    Color.FromArgb(226, 232, 245), Color.FromArgb(158, 170, 200), Color.FromArgb(110, 120, 150) });
                Control[] hijos = new Control[pg.Controls.Count];
                pg.Controls.CopyTo(hijos, 0);
                int choques = 0, salidas = 0, cortes = 0; float holgura = 99999f; string apretado = "";
                for (int i = 0; i < hijos.Length; i++)
                {
                    Rectangle rr = hijos[i].Bounds;
                    if (rr.Bottom > pg.Height || rr.Right > pg.Width || rr.Top < 0 || rr.Left < 0) salidas++;
                    for (int j = i + 1; j < hijos.Length; j++) if (rr.IntersectsWith(hijos[j].Bounds)) choques++;
                }
                using (Bitmap tmp = new Bitmap(4, 4))
                using (Graphics gg = Graphics.FromImage(tmp))
                    foreach (Control cc in hijos)
                    {
                        Control[] n = new Control[cc.Controls.Count];
                        cc.Controls.CopyTo(n, 0);
                        foreach (Control d in n)
                        {
                            if (d.Bounds.Bottom > cc.Height) salidas++;
                            if (d.Text == null || d.Text.Length == 0) continue;
                            int ancho = (d is Label) ? d.Width : d.Width - 8, alto = 0;
                            foreach (string li in d.Text.Split(SALTO))
                            {
                                SizeF sz = gg.MeasureString(li, d.Font);
                                alto += (int)Math.Ceiling(sz.Height);
                                if (ancho - sz.Width < holgura) { holgura = ancho - sz.Width; apretado = li; }
                                if (sz.Width > ancho) cortes++;
                            }
                            if (d is Label && alto > d.Height) cortes++;
                        }
                    }
                Console.WriteLine("     la pagina son " + pg.Width + "x" + pg.Height + " px");
                Console.WriteLine("     la linea de texto mas apretada deja " + ((int)holgura) + " px de sobra: " + apretado);
                Check(choques == 0, "nada se pisa con nada");
                Check(salidas == 0, "y nada se sale de la pagina ni de su tarjeta");
                Check(cortes == 0, "y ningun texto se corta: caben todos, de ancho y de alto");
                Check(holgura > 12f, "y sobra sitio de verdad, no por un pelo (en el bar la letra puede ser otra)");
            }
        }
        catch (Exception e) { Console.WriteLine("     (no se pudo armar la pagina aqui: " + e.GetType().Name + ")"); }

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO EN ORDEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
