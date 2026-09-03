using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;

// EL CIELO TIENE QUE TENER FONDO, NO SOLO SUPERFICIE.
//
// El fondo de la rocola se leia PLANO, y habia dos razones medibles:
//
//   1) Las mil cuatrocientas estrellas se deslizaban TODAS con el mismo desplazamiento. Un cielo en el que
//      todo se mueve a la vez y a la misma velocidad es un papel pintado que alguien arrastra, no un
//      espacio. Lo que el ojo lee como distancia -sin que nadie se lo explique- es que lo lejano se mueva
//      MENOS que lo cercano.
//
//   2) Todas brillaban y titilaban IGUAL arriba que abajo. En un cielo de verdad pasa justo lo contrario:
//      mirando al horizonte se atraviesa muchisimo mas aire que mirando hacia arriba, y ese aire apaga las
//      estrellas, las tira a naranja y las hace centellear mucho mas.
//
// Aqui se comprueban las dos cosas contra el ejecutable de verdad, con los numeros escritos en niveles de
// 255, que es la unidad en la que acaba todo lo que se ve en la pantalla.
//
// Y una tercera: que no cueste. Esto se hace mil cuatrocientas veces por cuadro.
class CieloHondo
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static Type TF, TG;
    static MethodInfo mVerts, mAire;
    // Antes el desplazamiento era UNO para las mil cuatrocientas estrellas; ahora es uno POR TAMAÑO. Esto
    // se apaña con las dos formas a proposito: asi, corrida contra el ejecutable de ANTES, esta bateria
    // dice lo que falta en vez de reventar con una pila de errores que no explica nada.
    static bool porTamano;
    static FieldInfo fX, fY, fC;
    static Type TVtx;

    // Arma un puñado de estrellas a mano y devuelve el color con el que saldrian pintadas.
    // Todas iguales salvo en lo que se quiera mirar: asi la diferencia solo puede venir de ahi.
    static uint[] Pinta(float[] xs, float[] ys, int[] kd, double reloj, float[] dx, float[] dy,
                        float w, float h, out float[] px, out float[] py)
    {
        int n = xs.Length;
        Array buf = Array.CreateInstance(TVtx, n * 6);
        float[] ph = new float[n], spd = new float[n], sz = new float[n];
        int[] fm = new int[n]; uint[] col = new uint[n];
        for (int i = 0; i < n; i++) { ph[i] = 0f; spd[i] = 1f; sz[i] = 2f; fm[i] = 0; col[i] = 0xFFFFFFFF; }
        object qdx = porTamano ? (object)dx : (object)dx[2], qdy = porTamano ? (object)dy : (object)dy[2];
        mVerts.Invoke(null, new object[] { buf, xs, ys, ph, spd, sz, kd, fm, col, n, reloj, 1f, 0f, qdx, qdy, w, h });
        uint[] salida = new uint[n];
        px = new float[n]; py = new float[n];
        for (int i = 0; i < n; i++)
        {
            object v0 = buf.GetValue(i * 6);
            salida[i] = (uint)fC.GetValue(v0);
            px[i] = (float)fX.GetValue(v0) + sz[i];   // el vertice es la esquina; el centro esta a un tamaño
            py[i] = (float)fY.GetValue(v0) + sz[i];
        }
        return salida;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in asm.GetTypes()) if (t.Name == "GpuStarfield") TG = t;
        if (TG == null) { Console.WriteLine("  (esta version no dibuja por tarjeta grafica: no hay cielo que mirar)"); return 0; }
        mVerts = TG.GetMethod("BuildStarVerts", BindingFlags.Static | BindingFlags.NonPublic);
        mAire = TF.GetMethod("AireDelCielo", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (mVerts == null) { Console.WriteLine("  FALLO  no esta BuildStarVerts"); return 1; }
        TVtx = mVerts.GetParameters()[0].ParameterType.GetElementType();
        porTamano = mVerts.GetParameters()[13].ParameterType.IsArray;
        fX = TVtx.GetField("X"); fY = TVtx.GetField("Y"); fC = TVtx.GetField("Color");

        // =========================================================================================
        Console.WriteLine("\n  1) LA CURVA DEL AIRE VIVE EN UN SOLO SITIO\n");
        // Las dos paredes -la de tarjeta grafica y la horneada- tienen que usar la MISMA. Con dos copias,
        // la maquina del bar y la de repuesto pintarian cielos distintos y no se sabria hasta verlas juntas.
        // Es la misma regla que ya siguen el corte del cielo y la fuerza de las nebulosas.
        Check(mAire != null, "la curva del aire esta en Form1, fuera del trozo de tarjeta grafica");

        bool copia = false;
        foreach (MethodInfo m in TG.GetMethods(TODO | BindingFlags.DeclaredOnly))
            if (m.Name.IndexOf("Aire", StringComparison.OrdinalIgnoreCase) >= 0 && m.Name != "HazAire") copia = true;
        Check(!copia, "y el trozo de tarjeta grafica no se ha hecho su propia copia de la curva");

        MethodInfo mTabla = TG.GetMethod("HazAire", BindingFlags.Static | BindingFlags.NonPublic);
        Check(mTabla != null && Codigo.Llama(mTabla, "AireDelCielo"),
              "la tabla del cielo se rellena PIDIENDOSELA a Form1, no rehaciendo la cuenta");

        if (mAire != null)
        {
        float a0 = (float)mAire.Invoke(null, new object[] { 0f, 100f });
        float aMed = (float)mAire.Invoke(null, new object[] { 50f, 100f });
        float a1 = (float)mAire.Invoke(null, new object[] { 100f, 100f });
        Console.WriteLine("     aire arriba del todo " + a0.ToString("0.00") + ", a media altura " + aMed.ToString("0.00") + ", abajo del todo " + a1.ToString("0.00"));
        Check(a0 < 0.01f && a1 > 0.99f, "arriba no hay aire delante y abajo hay todo el aire");
        Check(aMed < 0.35f, "y la curva carga el efecto ABAJO, no lo reparte por igual (si no, el cielo entero se apagaria)");
        bool sube = true; float ant = -1f;
        for (int i = 0; i <= 100; i++)
        { float v = (float)mAire.Invoke(null, new object[] { (float)i, 100f }); if (v < ant) sube = false; ant = v; }
        Check(sube, "y no da marcha atras en ningun punto: cuanto mas abajo, mas aire");
        // Un disparate no puede apagar la pantalla de un bar a las dos de la mañana.
        float aMal = (float)mAire.Invoke(null, new object[] { -50f, 100f });
        float aMal2 = (float)mAire.Invoke(null, new object[] { 900f, 100f });
        float aMal3 = (float)mAire.Invoke(null, new object[] { 10f, 0f });
        Check(aMal >= 0f && aMal <= 1f && aMal2 >= 0f && aMal2 <= 1f && aMal3 >= 0f && aMal3 <= 1f,
              "y fuera de la pantalla sigue dando un numero con sentido, no un disparate");
        }

        // =========================================================================================
        Console.WriteLine("\n  2) PROFUNDIDAD: EL POLVO SE MUEVE MENOS QUE LAS GRANDES\n");
        const float W = 1920f, H = 1080f;
        FieldInfo fDk = TG.GetField("DerivaKind", BindingFlags.Static | BindingFlags.NonPublic);
        Check(fDk != null, "cada tamaño de estrella tiene apuntado cuanto deriva");
        if (fDk != null)
        {
            float[] dk = (float[])fDk.GetValue(null);
            Console.WriteLine("     polvo " + dk[0].ToString("0.00") + "x, medianas " + dk[1].ToString("0.00") + "x, grandes " + dk[2].ToString("0.00") + "x");
            Check(dk.Length == 3 && dk[0] < dk[1] && dk[1] < dk[2], "y van de menos a mas: polvo < mediana < grande");
            Check(dk[2] >= 0.999f && dk[2] <= 1.001f, "las grandes van a la velocidad de siempre (el fondo no se acelera)");
            Check(dk[0] <= 0.45f, "y el polvo va a menos de la mitad: es lo que se lee como distancia");

            // Y que el avance de cada tamaño se acumule POR SEPARADO. Multiplicar despues el desplazamiento
            // ya envuelto moveria el polvo de golpe un tercio de pantalla cada vuelta: un tiron cada dos
            // minutos de musica.
            List<MethodBase> dibuja = Codigo.MetodosConAyudantes(TG, "Begin");
            Check(Codigo.LeeCampo(dibuja, "DerivaKind"), "y el dibujado avanza cada tamaño por su lado, no uno multiplicado por tres");
        }

        // Tres estrellas en el MISMO sitio, una de cada tamaño: cada una tiene que acabar donde le toca.
        float[] xs = { 500f, 500f, 500f }, ys = { 400f, 400f, 400f };
        int[] kd = { 0, 1, 2 };
        float[] px, py;
        Pinta(xs, ys, kd, 0.0, new float[] { 30f, 60f, 90f }, new float[] { 15f, 30f, 45f }, W, H, out px, out py);
        Console.WriteLine("     con la deriva puesta, el polvo cae en x=" + px[0].ToString("0") + ", la mediana en " + px[1].ToString("0") + " y la grande en " + px[2].ToString("0"));
        Check(Math.Abs(px[0] - 530f) < 0.5f && Math.Abs(px[1] - 560f) < 0.5f && Math.Abs(px[2] - 590f) < 0.5f,
              "cada estrella se corre con la deriva de SU tamaño, no con una sola para todas");

        // =========================================================================================
        Console.WriteLine("\n  3) Y NINGUNA SE SALE DE LA PANTALLA AL DAR LA VUELTA\n");
        // La vuelta al otro lado se hace con UNA SOLA RESTA, y eso solo vale mientras el desplazamiento no
        // se pase de ancho. Aqui se le da el peor caso: estrella pegada al borde y deriva casi de una
        // pantalla entera, que es el maximo que puede tener acumulado.
        {
            Random r = new Random(4);
            int n = 900;
            float[] xx = new float[n], yy = new float[n]; int[] kk = new int[n];
            for (int i = 0; i < n; i++)
            { xx[i] = (float)(r.NextDouble() * W); yy[i] = (float)(r.NextDouble() * H); kk[i] = i % 3; }
            xx[0] = W - 0.001f; yy[0] = H - 0.001f;
            float[] qx, qy;
            Pinta(xx, yy, kk, 1.0, new float[] { W - 0.001f, W - 0.001f, W - 0.001f },
                  new float[] { H - 0.001f, H - 0.001f, H - 0.001f }, W, H, out qx, out qy);
            int fuera = 0;
            for (int i = 0; i < n; i++) if (qx[i] < 0f || qx[i] >= W || qy[i] < 0f || qy[i] >= H) fuera++;
            Console.WriteLine("     con la deriva al maximo, " + fuera + " de " + n + " estrellas se salen de la pantalla");
            Check(fuera == 0, "con la deriva al maximo ninguna se sale: la vuelta de una resta sigue bastando");
        }

        // =========================================================================================
        Console.WriteLine("\n  4) EL AIRE: LA MISMA ESTRELLA, ARRIBA Y ABAJO\n");
        // La MISMA estrella (misma fase, misma velocidad, mismo color) puesta arriba del todo y abajo del
        // todo. Se barre una vuelta entera de titileo y se miran dos cosas: cuanto brilla DE MEDIA -eso es
        // apagarse- y cuanto sube y baja -eso es centellear-.
        {
            float[] xx = { 100f, 100f }, yy = { H * 0.03f, H * 0.97f };
            int[] kk = { 2, 2 };
            float[] cero = { 0f, 0f, 0f };
            int PASOS = 240;
            double arrSum = 0, abaSum = 0; int arrMin = 999, arrMax = -1, abaMin = 999, abaMax = -1;
            double difRB = 0;
            for (int s = 0; s < PASOS; s++)
            {
                float[] qx, qy;
                uint[] c = Pinta(xx, yy, kk, s * (6.2831853 / PASOS), cero, cero, W, H, out qx, out qy);
                int aArr = (int)(c[0] >> 24), aAba = (int)(c[1] >> 24);
                arrSum += aArr; abaSum += aAba;
                if (aArr < arrMin) arrMin = aArr; if (aArr > arrMax) arrMax = aArr;
                if (aAba < abaMin) abaMin = aAba; if (aAba > abaMax) abaMax = aAba;
                int rA = (int)((c[0] >> 16) & 0xFF), bA = (int)(c[0] & 0xFF);
                int rB = (int)((c[1] >> 16) & 0xFF), bB = (int)(c[1] & 0xFF);
                difRB += (rB - bB) - (rA - bA);
            }
            double arrMed = arrSum / PASOS, abaMed = abaSum / PASOS;
            Console.WriteLine("     brillo medio: arriba " + arrMed.ToString("0.0") + " niveles de 255, abajo " + abaMed.ToString("0.0"));
            Console.WriteLine("     o sea que el aire le come " + (arrMed - abaMed).ToString("0.0") + " niveles a la estrella de abajo");
            Check(abaMed < arrMed - 20.0, "la estrella de abajo sale claramente mas apagada que la MISMA de arriba");

            double calor = difRB / PASOS;
            Console.WriteLine("     y le quita azul: abajo el rojo le saca " + calor.ToString("0.0") + " niveles mas al azul que arriba");
            Check(calor > 8.0, "y mas calida: abajo tira a naranja, como hace el aire de verdad");

            // EL CENTELLEO SE MIDE EN PROPORCION, NO EN NIVELES SUELTOS, y aqui esta el porque.
            //
            // La estrella de abajo esta apagada a menos de la mitad, asi que aunque titile MUCHO mas, en
            // niveles de 255 sube y baja parecido que la de arriba: no puede pasar de lo que brilla. Lo que
            // el ojo lee como centelleo es cuanto cambia RESPECTO A LO QUE BRILLA -una estrella tenue que se
            // apaga del todo y vuelve centellea muchisimo, aunque el salto en niveles sea chico-, y eso es
            // lo que se comprueba. Los dos numeros se escriben para que se vea que no se esconde nada.
            int arrAmp = arrMax - arrMin, abaAmp = abaMax - abaMin;
            double arrRel = arrAmp / Math.Max(1.0, arrMed), abaRel = abaAmp / Math.Max(1.0, abaMed);
            Console.WriteLine("     titileo: arriba sube y baja " + arrAmp + " niveles sobre " + arrMed.ToString("0") + " (" + (arrRel * 100).ToString("0") + "% de lo que brilla)");
            Console.WriteLine("              abajo  sube y baja " + abaAmp + " niveles sobre " + abaMed.ToString("0") + " (" + (abaRel * 100).ToString("0") + "% de lo que brilla)");
            Check(abaRel > arrRel * 1.25, "y centellea bastante mas abajo que arriba, que es lo que hace el aire");
        }

        // =========================================================================================
        Console.WriteLine("\n  5) Y NO CUESTA: ESTO SE HACE 1400 VECES POR CUADRO\n");
        // Primero lo estructural, que no depende de la maquina: la cuenta del aire va POR TABLA. Si alguien
        // mete aqui la curva de verdad -una potencia por estrella- esto lo caza aunque la maquina vaya
        // sobrada ese dia.
        Check(!Codigo.Llama(mVerts, "AireDelCielo") && !Codigo.Llama(mVerts, "Pow"),
              "el aire de cada estrella sale de una tabla, no de rehacer la curva mil cuatrocientas veces");
        {
            const int NE = 1400;
            FieldInfo fA = TG.GetField("AtlasU0", TODO);
            int formas = (fA != null) ? ((Array)fA.GetValue(null)).Length : 1;
            Array buf = Array.CreateInstance(TVtx, NE * 6);
            float[] xx = new float[NE], yy = new float[NE], ph = new float[NE], spd = new float[NE], sz = new float[NE];
            int[] kk = new int[NE], fm = new int[NE]; uint[] col = new uint[NE];
            Random r = new Random(7);
            for (int i = 0; i < NE; i++)
            {
                xx[i] = (float)(r.NextDouble() * W); yy[i] = (float)(r.NextDouble() * H);
                ph[i] = (float)(r.NextDouble() * 6.28); spd[i] = 0.5f + (float)r.NextDouble();
                sz[i] = 1f + (float)r.NextDouble() * 2f; kk[i] = i % 3; fm[i] = i % formas; col[i] = 0xFFFFFFFF;
            }
            object[] arg = { buf, xx, yy, ph, spd, sz, kk, fm, col, NE, 0.0, 1f, 0.3f, new float[3], new float[3], W, H };
            mVerts.Invoke(null, arg);
            List<double> v = new List<double>();
            for (int k = 0; k < 21; k++)
            {
                arg[10] = (double)k * 0.04;
                Stopwatch w = Stopwatch.StartNew();
                for (int i = 0; i < 10; i++) mVerts.Invoke(null, arg);
                w.Stop(); v.Add(w.Elapsed.TotalMilliseconds / 10.0);
            }
            v.Sort();
            double ms = v[v.Count / 2];
            Console.WriteLine("     armar " + NE + " estrellas con aire y profundidad: " + ms.ToString("0.000") + " ms por cuadro");
            Console.WriteLine("     a 180 cuadros por segundo son " + (ms * 180).ToString("0") + " ms de cada mil");
            Check(ms < 0.35, "y sigue costando lo de siempre (con esto medido en 0.06, un techo de 0.35 solo salta si alguien mete algo caro)");
        }

        // =========================================================================================
        Console.WriteLine("\n  6) SIN TARJETA GRAFICA NO SE PONE, Y AQUI ESTA EL PORQUE\n");
        // Sin tarjeta grafica las estrellas van horneadas en capas que se deslizan, asi que no se les puede
        // atar nada a la altura de la pantalla. La otra forma es un VELO -un degradado del color del cielo
        // pegado encima- y esto mide lo que costaria. La medida se queda aqui aunque el velo no este: si
        // algun dia la maquina del bar cambia, el numero esta a mano y no hay que volver a averiguarlo.
        {
            Type TS = TF.GetNestedType("Starfield", BindingFlags.NonPublic | BindingFlags.Public);
            bool hayVelo = TS != null && TS.GetField("velo", TODO) != null;
            const int VW = 1024, VH = 768;
            using (Bitmap fondo = new Bitmap(VW, VH, PixelFormat.Format32bppPArgb))
            using (Bitmap velo = new Bitmap(VW, VH, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(fondo))
            {
                using (Graphics vg = Graphics.FromImage(velo))
                {
                    vg.CompositingMode = CompositingMode.SourceCopy;
                    vg.Clear(Color.FromArgb(0, 0, 0, 0));
                    using (SolidBrush sb = new SolidBrush(Color.Black))
                        for (int y = 0; y < VH; y++)
                        {
                            float u = y / (float)VH;
                            int a = (int)(255f * 0.55f * (float)Math.Pow(u, 2.2));
                            if (a <= 0) continue;
                            sb.Color = Color.FromArgb(a, 40, 30, 60);
                            vg.FillRectangle(sb, 0, y, VW, 1);
                        }
                }
                g.CompositingMode = CompositingMode.SourceOver;
                g.DrawImageUnscaled(velo, 0, 0);   // la primera calienta
                List<double> v = new List<double>();
                for (int k = 0; k < 15; k++)
                {
                    Stopwatch w = Stopwatch.StartNew();
                    g.DrawImageUnscaled(velo, 0, 0);
                    w.Stop(); v.Add(w.Elapsed.TotalMilliseconds);
                }
                v.Sort();
                double ms = v[v.Count / 2];
                Console.WriteLine("     pegar el velo a " + VW + "x" + VH + " costaria " + ms.ToString("0.00") + " ms por cuadro");
                Console.WriteLine("     o sea " + (ms * 24).ToString("0") + " ms de cada mil a 24 cuadros por segundo, y x4 o x5 en la maquina del bar");
                Console.WriteLine("     (pegar el fondo entero -cielo mas una capa de estrellas- cuesta 0.55 ms ahi mismo: el velo lo DUPLICA)");
                Check(!hayVelo, "el velo no esta puesto: duplicaria lo que cuesta el fondo justo en la maquina que no tiene tarjeta grafica");
            }
        }

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO EN ORDEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
