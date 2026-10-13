using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// LA ESTRELLA EN 3D, SU ESTELA Y LOS SALTITOS (Rocola/Estrella3dGpu.cs; solo tarjeta grafica)
//
// Lo que vigila:
//  1) El solido: cerrado (cada arista entre dos caras), con las normales hacia afuera, y de frente es la estrella del
//     icono (las puntas y los valles en su sitio, la de arriba arriba). De frente se ven las diez caras de delante; de
//     espaldas, las de detras. Una cara de espaldas no se pinta, y las que se ven van de atras hacia adelante.
//  2) El giro: da la vuelta entera, la velocidad cambia sin tirones, no depende de los cuadros por segundo y al navegar
//     se calma. LA MUSICA YA NO LA MUEVE (24-09-2026, "da brincos"): con silencio, con golpes o con otro genero, el
//     mismo recorrido, el mismo giro y el mismo balanceo, y ninguna parte de su dibujo lee la musica. Apagada la
//     estrella viajera, queda de frente como el icono.
//  3) La luz: cada cara segun hacia donde mira; el brillo corre por las facetas al girar.
//  4) La estela: una copia por distancia recorrida (igual de separadas a 24, 60 o 144 cuadros por segundo, lenta o
//     rapida), ninguna si no se mueve, cada una nace transparente (sin copias que salten a la vista) y guarda el giro
//     de su momento. Sin estela, sin copias.
//  5) LOS SALTITOS: el reparto de cuadros de la rocola frente al compositor de Windows, con lo que tarda cada cuadro.
//     El bucle de siempre (66 por segundo en una pantalla de 60 Hz) da saltos dobles; colocar la estrella para el
//     refresco siguiente sin mas no basta si el cuadro tarda unos milisegundos; acompasado al refresco, cada refresco
//     la ensena avanzada lo mismo. La rejilla de Windows, sin compositor (XP) y el cableado.
//  6) El interruptor de Ctrl+F3 (se guarda y se aplica), que dibujar no reserve memoria, y lo que cuesta.
class Estrella3dPrueba
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type gpu, e3, reloj, form, vtx;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string Fm(double v, string f) { return v.ToString(f, System.Globalization.CultureInfo.InvariantCulture); }
    static object Get(object o, string n) { return o.GetType().GetField(n, F).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, F).SetValue(o, v); }
    static object Run(object o, string n, params object[] a) { return gpu.GetMethod(n, F).Invoke(o, a); }
    static T Est<T>(string n) { return (T)e3.GetField(n, F).GetValue(null); }

    static float[] px = new float[12], py = new float[12], vx = new float[12], vy = new float[12], vz = new float[12];
    static float[] nx = new float[20], ny = new float[20], nz = new float[20], zc = new float[20];
    static bool[] mira = new bool[20];
    static void Pose(float cx, float cy, float r, double yaw, double pitch, double roll)
    {
        e3.GetMethod("Proyecta", F).Invoke(null, new object[] { cx, cy, r, yaw, pitch, roll, px, py, vx, vy, vz, nx, ny, nz, zc, mira });
    }

    static void ElSolido()
    {
        Console.WriteLine("\n  1) EL SOLIDO\n");
        int[] CA = Est<int[]>("CA"), CB = Est<int[]>("CB"), CC = Est<int[]>("CC");
        int[] AV0 = Est<int[]>("AV0"), AV1 = Est<int[]>("AV1"), AF0 = Est<int[]>("AF0"), AF1 = Est<int[]>("AF1");
        // Cada arista de cada cara, contada: en un solido cerrado cada una sale exactamente dos veces, una en cada sentido.
        Dictionary<long, int> ida = new Dictionary<long, int>();
        bool cerrado = true;
        for (int c = 0; c < 20; c++)
        {
            int[] v = { CA[c], CB[c], CC[c] };
            for (int k = 0; k < 3; k++) { long key = v[k] * 100 + v[(k + 1) % 3]; if (ida.ContainsKey(key)) cerrado = false; ida[key] = c; }
        }
        foreach (long key in ida.Keys) if (!ida.ContainsKey((key % 100) * 100 + key / 100)) cerrado = false;
        Check(cerrado && ida.Count == 60, "cerrado: 20 caras y 30 aristas, cada una compartida por dos caras que la recorren al reves");
        bool aristas = true;
        for (int a = 0; a < 30; a++)
        {
            long k0 = AV0[a] * 100 + AV1[a], k1 = AV1[a] * 100 + AV0[a];
            int f0 = ida.ContainsKey(k0) ? ida[k0] : -1, f1 = ida.ContainsKey(k1) ? ida[k1] : -1;
            if (!((f0 == AF0[a] && f1 == AF1[a]) || (f0 == AF1[a] && f1 == AF0[a]))) aristas = false;
        }
        Check(aristas, "la tabla de aristas (la de la silueta) dice bien que dos caras comparte cada una");
        // Normales hacia afuera: en la pose de frente, la normal de cada cara se aleja del centro del solido.
        Pose(0, 0, 1, 0, 0, 0);
        bool afuera = true;
        for (int c = 0; c < 20; c++)
        {
            float mx = (vx[CA[c]] + vx[CB[c]] + vx[CC[c]]) / 3, my = (vy[CA[c]] + vy[CB[c]] + vy[CC[c]]) / 3, mz = (vz[CA[c]] + vz[CB[c]] + vz[CC[c]]) / 3;
            if (nx[c] * mx + ny[c] * my + nz[c] * mz <= 0) afuera = false;
        }
        Check(afuera, "las 20 normales apuntan hacia afuera");
        // De frente es el icono: 5 puntas a radio 1 (la primera arriba) y 5 valles a 0,445, cada 36 grados.
        Pose(500, 300, 100, 0, 0, 0);
        bool icono = Math.Abs(px[0] - 500) < .01 && Math.Abs(py[0] - 200) < .01;
        for (int i = 0; i < 10; i++)
        {
            double r = Math.Sqrt((px[i] - 500) * (px[i] - 500) + (py[i] - 300) * (py[i] - 300)) / 100, ang = Math.Atan2(px[i] - 500, 300 - py[i]);
            double esperado = (i & 1) == 0 ? 1 : .445; double angE = i * Math.PI / 5; if (angE > Math.PI) angE -= 2 * Math.PI;
            if (Math.Abs(r - esperado) > .002 || Math.Abs(ang - angE) > .002) icono = false;
        }
        Check(icono, "de frente es la estrella del icono: la punta de arriba arriba, las puntas a radio 1 y los valles a 0,445");
        int delante = 0, detras = 0; for (int c = 0; c < 20; c++) if (mira[c]) { if (c < 10) delante++; else detras++; }
        Check(delante == 10 && detras == 0, "de frente se ven las 10 caras de delante y ninguna de detras");
        Pose(500, 300, 100, Math.PI, 0, 0);
        delante = 0; detras = 0; for (int c = 0; c < 20; c++) if (mira[c]) { if (c < 10) delante++; else detras++; }
        Check(delante == 0 && detras == 10, "de espaldas (media vuelta) se ven las 10 de detras");
        // Orden de pintado: de atras hacia adelante.
        int[] orden = new int[20]; bool ordenado = true;
        foreach (double yaw in new double[] { .3, 1.1, 2.0, 2.9, 4.4 })
        {
            Pose(0, 0, 1, yaw, .22, .1);
            e3.GetMethod("Ordena", F).Invoke(null, new object[] { zc, orden });
            for (int i = 1; i < 20; i++) if (zc[orden[i]] < zc[orden[i - 1]]) ordenado = false;
        }
        Check(ordenado, "las caras se ordenan de atras hacia adelante");
        // Lo que se manda a la tarjeta: solo las caras que miran a la camara, y la franja solo en la silueta.
        using (Control p = (Control)Activator.CreateInstance(gpu, true))
        {
            Array caras = Array.CreateInstance(vtx, 60), filo = Array.CreateInstance(vtx, 180);
            MethodInfo pose = gpu.GetMethod("PoseEstrella", F);
            bool cuenta = true;
            foreach (double yaw in new double[] { 0, .3, 1.1, 2.0, 2.9, 4.4 })
            {
                object[] a = { caras, 0, filo, 0, 500f, 300f, 80f, yaw, .22, .1, 0xFF8899AAu, 255f, false, 1.1f, 0, 0, 0f };
                int n = (int)pose.Invoke(p, a);
                Pose(500, 300, 80, yaw, .22, .1);
                int ven = 0, silueta = 0;
                for (int c = 0; c < 20; c++) if (mira[c]) ven++;
                for (int e = 0; e < 30; e++) if (mira[AF0[e]] != mira[AF1[e]]) silueta++;
                if (n != 3 * ven || (int)a[14] == 0 || (int)a[14] > 6 * silueta) cuenta = false;
            }
            Check(cuenta, "a la tarjeta van solo las caras que miran a la camara, y la franja solo en la silueta");
        }
        // La perspectiva es suave: la punta mas cercana crece como mucho un 9 %.
        float cam = (float)e3.GetField("Camara", F).GetValue(null), relieve = (float)e3.GetField("Relieve", F).GetValue(null);
        Check(cam / (cam - relieve) < 1.09f, "perspectiva suave (lo mas cercano, " + Fm(100 * (cam / (cam - relieve) - 1), "0.0") + " % mas grande)");
    }

    static void ElGiro()
    {
        Console.WriteLine("\n  2) EL GIRO\n");
        double referencia = -1; bool pasos = true; double maxPaso = 0;
        foreach (int fps in new int[] { 24, 60, 144, 240 })
            using (Control p = (Control)Activator.CreateInstance(gpu, true))
            {
                Set(p, "idleBlend", 1f);
                double antes = 0; float velAntes = 0;
                for (int i = 0; i < fps * 20; i++)
                {
                    Run(p, "AdvanceStarMusic", 1f / fps, .4f, .3f, .2f);
                    double yaw = (double)Get(p, "starYaw"); float vel = (float)Get(p, "starSpin");
                    if (Math.Abs(vel - velAntes) > 6.3 * 5 * (1.0 / fps) + 1e-5) pasos = false;   // la velocidad cambia sin saltos
                    maxPaso = Math.Max(maxPaso, (yaw - antes) * fps);
                    antes = yaw; velAntes = vel;
                }
                double y = (double)Get(p, "starYaw");
                if (referencia < 0) referencia = y; else if (Math.Abs(y - referencia) > .05) pasos = false;
            }
        Check(pasos, "el giro no depende de los cuadros por segundo (24, 60, 144 y 240) y su velocidad cambia sin tirones");
        Check(referencia > 2 * Math.PI, "da la vuelta entera: en 20 s, " + Fm(referencia / (2 * Math.PI), "0.0") + " vueltas");
        double[] vueltas = new double[3];
        for (int modo = 0; modo < 3; modo++)
            using (Control p = (Control)Activator.CreateInstance(gpu, true))
            {
                Set(p, "idleBlend", modo == 2 ? 0f : 1f);
                for (int i = 0; i < 600; i++) Run(p, "AdvanceStarMusic", 1f / 60, modo == 0 ? 0f : .8f, modo == 0 ? 0f : .6f, modo == 0 ? 0f : .5f);
                vueltas[modo] = (double)Get(p, "starYaw") / (2 * Math.PI);
                if (modo == 1)
                {
                    double y0 = (double)Get(p, "starYaw"); Set(p, "LunaActiva", false);
                    Run(p, "AdvanceStarMusic", .1f, 1f, 1f, 1f);
                    Check((double)Get(p, "starYaw") == y0, "con la estrella viajera apagada no gira");
                }
            }
        Info("vueltas en 10 s: en silencio " + Fm(vueltas[0], "0.00") + ", con musica " + Fm(vueltas[1], "0.00") + ", navegando " + Fm(vueltas[2], "0.00"));
        Check(vueltas[1] == vueltas[0] && vueltas[0] > vueltas[2] * 3, "con musica gira igual que en silencio, y al navegar se calma");
        Check(vueltas[1] / 10 < 1.0 && vueltas[0] / 10 > .1, "ni frenetica (menos de una vuelta por segundo) ni parada (mas de una cada 10 s)");
        LaMusicaNoLaMueve();
    }

    // LO QUE PIDIO EL DUEÑO: que no brinque con la musica. Tres estrellas a la par durante 30 s -en silencio, con
    // golpes secos de graves, medios y agudos (lo que mas la hacia saltar) y con musica de otro genero- tienen que ir
    // exactamente por el mismo sitio, con el mismo giro y el mismo balanceo, cuadro a cuadro.
    static void LaMusicaNoLaMueve()
    {
        Control[] p = new Control[3];
        string[] genero = { "Rock", "Rock", "Balada" };
        for (int k = 0; k < 3; k++) { p[k] = (Control)Activator.CreateInstance(gpu, true); Set(p[k], "idleBlend", 1f); Run(p[k], "SetStarGenre", genero[k]); }
        string[] campos = { "starTravelX", "starTravelY", "starYaw", "moonMotionTime", "starEchoPhase" };
        bool igual = true, velIgual = true; double maxDif = 0;
        try
        {
            for (int i = 0; i < 60 * 30; i++)
            {
                float dt = (i % 3 == 0) ? 1f / 24 : (i % 3 == 1) ? 1f / 144 : 1f / 60;   // cuadros desparejos, como en el bar
                bool golpe = (i % 29) < 3;
                Run(p[0], "AdvanceStarMusic", dt, 0f, 0f, 0f);
                Run(p[1], "AdvanceStarMusic", dt, golpe ? 1f : 0f, (i % 11) < 2 ? 1f : 0f, (i % 7) == 0 ? 1f : 0f);
                Run(p[2], "AdvanceStarMusic", dt, .9f, .8f, .7f);
                foreach (string c in campos)
                {
                    double a = (double)Get(p[0], c), b1 = (double)Get(p[1], c), b2 = (double)Get(p[2], c);
                    maxDif = Math.Max(maxDif, Math.Max(Math.Abs(a - b1), Math.Abs(a - b2)));
                    if (a != b1 || a != b2) igual = false;
                }
                foreach (string c in new string[] { "starVelocityX", "starVelocityY", "starSpin" })
                    if ((float)Get(p[0], c) != (float)Get(p[1], c) || (float)Get(p[0], c) != (float)Get(p[2], c)) velIgual = false;
            }
            // La musica se sigue oyendo (para el flujo de color del espectro): no es que no le llegue.
            Check((float)Get(p[2], "starDrive") > .5f && (float)Get(p[0], "starDrive") < .001f,
                  "la musica le sigue llegando (el color del espectro la usa), pero...");
            Check(igual && velIgual, "...en silencio, a golpes secos o con otro genero, el mismo recorrido, el mismo giro, el mismo balanceo y la misma estela, cuadro a cuadro durante 30 s (diferencia maxima " + maxDif.ToString("0.###E0") + ")");
            double maxRoll = 0;
            for (int i = 0; i <= 2000; i++) maxRoll = Math.Max(maxRoll, Math.Abs((float)Run(null, "StarRoll", i * .05)));
            Check(maxRoll > .14 && maxRoll < .15, "su balanceo va siempre igual de ancho: hasta " + Fm(maxRoll * 180 / Math.PI, "0.0") + " grados a cada lado, sin crecer con los golpes");
        }
        finally { for (int k = 0; k < 3; k++) p[k].Dispose(); }
        // Y ninguna parte de su dibujo lee la musica: ni el tamaño, ni el balanceo, ni la corona, ni el polvo, ni la
        // estela, ni el color. (lunaBeat y los demas siguen vivos para las barritas de la que suena, DiscoVivoGpu.cs.)
        string[] dibujo = { "DrawDetailedMoon", "DrawLunarDust", "DrawStar3d", "UpdateStarEchoes3d", "DrawStarEchoes3d",
                            "UpdateStarEchoes", "DrawStarEchoes", "StarPitch", "StarSceneCenter", "StarColor", "StarEchoColor" };
        string[] musica = { "lunaBeat", "lunaMid", "lunaHigh", "starDrive", "starAttack", "starGenreDrive" };
        List<string> leen = new List<string>();
        foreach (string m in dibujo)
        {
            List<MethodBase> ms = Codigo.MetodosConAyudantes(gpu, m);
            if (ms.Count == 0) { leen.Add(m + " (no esta)"); continue; }
            foreach (string c in musica) if (Codigo.LeeCampo(ms, c)) leen.Add(m + " lee " + c);
        }
        Check(leen.Count == 0, "ninguna parte del dibujo de la estrella lee la musica" + (leen.Count > 0 ? ": " + string.Join(", ", leen.ToArray()) : ""));
        Check((float)Run(null, "MoonSceneScale", 1f, true) > 1.3f && (float)Run(null, "MoonSceneScale", 0f, true) == 1f,
              "su tamaño: un 32 % mas en reposo, entrando despacio, y ni un latido");
    }

    static void LaLuz()
    {
        Console.WriteLine("\n  3) LA LUZ\n");
        MethodInfo luz = e3.GetMethod("Luz", F);
        float min = 9, max = -9; HashSet<int> brillan = new HashSet<int>();
        for (int k = 0; k < 36; k++)
        {
            Pose(0, 0, 1, k * Math.PI / 18, .22, 0);
            float mejor = 0; int cual = -1;
            for (int c = 0; c < 20; c++)
            {
                if (!mira[c]) continue;
                object[] a = { nx[c], ny[c], nz[c], true, 0f };
                float l = (float)luz.Invoke(null, a); float refl = (float)a[4];
                min = Math.Min(min, l); max = Math.Max(max, l);
                if (refl > mejor) { mejor = refl; cual = c; }
            }
            if (cual >= 0 && mejor > .2f) brillan.Add(cual);
        }
        Check(min >= .19f && max <= 1.3f && max - min > .5f, "cada cara con su luz (de " + Fm(min, "0.00") + " a " + Fm(max, "0.00") + "): las facetas se distinguen");
        Check(brillan.Count >= 4, "al dar la vuelta, el brillo pasa por " + brillan.Count + " facetas distintas");
        uint c1 = (uint)e3.GetMethod("ColorCara", F).Invoke(null, new object[] { 0xFFFF0000u, 1f, 0f, 200u });
        Check((c1 >> 24) == 200 && ((c1 >> 16) & 255) > 150 && ((c1 >> 8) & 255) == 0, "el color de la cancion tine el metal y se conserva el alfa");
    }

    static void LaEstela()
    {
        Console.WriteLine("\n  4) LA ESTELA\n");
        float paso = (float)gpu.GetField("EcoPaso", F).GetValue(null);
        foreach (int fps in new int[] { 24, 60, 144 })
            foreach (float vel in new float[] { 150f, 600f })
                using (Control p = (Control)Activator.CreateInstance(gpu, true))
                {
                    p.Size = new Size(1920, 1080); Set(p, "idleBlend", 1f);
                    float r = 80; double t = 10;
                    for (int i = 0; i < fps * 3; i++)
                    {
                        t += 1.0 / fps;
                        Run(p, "UpdateStarEchoes3d", t, (float)(100 + vel * (t - 10)), 300f, r, t * 3, .22, 0f, 0xFFFFFFFFu);
                    }
                    Array ecos = (Array)Get(p, "starEchoes"); int head = (int)Get(p, "starEchoHead"), count = (int)Get(p, "starEchoCount");
                    List<float> xs = new List<float>(); List<double> yaws = new List<double>();
                    for (int i = 0; i < count; i++) { object e = ecos.GetValue((head - count + i + ecos.Length) % ecos.Length); xs.Add((float)Get(e, "X")); yaws.Add((double)Get(e, "Yaw")); }
                    float peor = 0; bool giran = true;
                    for (int i = 1; i < xs.Count; i++) { peor = Math.Max(peor, Math.Abs((xs[i] - xs[i - 1]) - paso * r)); if (!(yaws[i] > yaws[i - 1])) giran = false; }
                    float tolera = vel / fps + .01f;   // un cuadro de recorrido como mucho
                    Check(count >= 3 && peor <= tolera && giran, fps + " cuadros/s, " + vel + " px/s: " + count + " copias, separadas " + Fm(paso * r, "0") + " px (se apartan " + Fm(peor, "0.0") + " como mucho) y cada una con su giro");
                }
        using (Control p = (Control)Activator.CreateInstance(gpu, true))
        {
            p.Size = new Size(1920, 1080); Set(p, "idleBlend", 1f);
            double t = 10; for (int i = 0; i < 180; i++) { t += 1 / 60.0; Run(p, "UpdateStarEchoes3d", t, 500f + (float)Math.Sin(t) * .3f, 300f, 80f, t, .22, 0f, 0xFFFFFFFFu); }
            Check((int)Get(p, "starEchoCount") == 0, "si la estrella casi no se mueve, no deja copias (no se apilan en el mismo sitio)");
            Set(p, "EstelaEstelar", false); Set(p, "starEchoVisibility", 0f);
            for (int i = 0; i < 180; i++) { t += 1 / 60.0; Run(p, "UpdateStarEchoes3d", t, 500f + 400f * (float)(t - 13), 300f, 80f, t, .22, 0f, 0xFFFFFFFFu); }
            Check((int)Get(p, "starEchoCount") == 0, "con los ecos apagados en Ctrl+F3, ninguna copia");
        }
        // Nace transparente y aparece poco a poco; luego se desvanece.
        MethodInfo alfa = gpu.GetMethod("StarEchoAlpha3d", F);
        float a0 = (float)alfa.Invoke(null, new object[] { 0.0, 1f }), aMed = (float)alfa.Invoke(null, new object[] { .03, 1f }), aPico = 0;
        float peorSalto = 0, antes = 0;
        for (double age = 0; age < 1.3; age += 1 / 60.0)
        {
            float a = (float)alfa.Invoke(null, new object[] { age, 1f });
            if (a > aPico) aPico = a;
            peorSalto = Math.Max(peorSalto, Math.Abs(a - antes)); antes = a;
        }
        Check(a0 == 0f && aMed > 0f && aMed < aPico, "cada copia nace transparente (detras de la estrella) y aparece en " + Fm(1000 * (double)gpu.GetField("EcoEntra", F).GetValue(null), "0") + " ms: sin copias que salten a la vista");
        Check(peorSalto < .5f && (float)alfa.Invoke(null, new object[] { 1.2, 1f }) == 0f, "y se desvanece sin saltos (" + Fm(peorSalto, "0.00") + " de un cuadro a otro como mucho) hasta desaparecer a los 1,2 s");
    }

    // Un reparto de cuadros como el de la rocola, con las cuentas de la propia rocola (RelojPantalla): el bucle, lo que
    // tarda cada cuadro en dibujarse ('r', con 1 ms de vaiven), un despertar que llega hasta 0,6 ms antes o 0,5 ms tarde,
    // y el compositor de Windows, que en cada refresco (0,5 ms despues de el) ensena el ultimo cuadro TERMINADO. Modos:
    // 0 = el bucle de siempre (va por su cuenta a su tope) con el reloj de siempre; 1 = el mismo bucle, pero colocando
    // cada cuadro para el refresco siguiente (la primera idea); 2 = el bucle acompasado a la rejilla de refrescos.
    // Devuelve, refresco a refresco durante 10 s, cuanto avanzo lo que se ve (en refrescos: 1 es lo justo). Antes va un
    // segundo que no cuenta: el arranque, mientras el bucle cae en su sitio.
    static List<double> Reparto(double hz, double r0, int tope, int modo, out double fps)
    {
        MethodInfo siguiente = reloj.GetMethod("Siguiente", F), despues = reloj.GetMethod("Despues", F);
        Random rnd = new Random(7);
        double P = 1000.0 / hz, V0 = 3.3, eps = .5, t = 0, prox = 0;
        List<double> listo = new List<double>(), contenido = new List<double>();
        while (t < 11000)
        {
            double ahora = t;
            if (ahora + .6 < prox) { t = rnd.Next(3) == 0 ? prox - .6 * rnd.NextDouble() : prox + .5 * rnd.NextDouble(); continue; }
            double sig = modo == 2 ? (double)siguiente.Invoke(null, new object[] { ahora, tope, V0, P }) : double.NaN;
            if (!double.IsNaN(sig)) prox = sig;
            else { prox += tope; if (prox < ahora) prox = ahora + tope; }
            contenido.Add(modo == 0 ? ahora : (double)despues.Invoke(null, new object[] { ahora, V0, P }));
            double r = Math.Max(.2, r0 + 2 * rnd.NextDouble() - 1);
            listo.Add(ahora + r);
            t = ahora + r + .05;
        }
        List<double> pasos = new List<double>();
        int k = -1; double antes = double.NaN;
        for (int n = 0; V0 + n * P + eps <= 11000; n++)
        {
            while (k + 1 < listo.Count && listo[k + 1] <= V0 + n * P + eps) k++;
            if (k < 0) continue;
            if (!double.IsNaN(antes) && n * P > 1000) pasos.Add((contenido[k] - antes) / P);
            antes = contenido[k];
        }
        fps = listo.Count / 11.0;
        return pasos;
    }
    static void Cuenta(List<double> pasos, out int dobles, out int parones)
    {
        dobles = 0; parones = 0;
        foreach (double p in pasos) { if (p > 1.5) dobles++; else if (p < .5) parones++; }
    }
    static int TopeAuto(int hz) { return 1000 / (hz + hz / 10); }   // el de GpuPanel.TopeMs: la pantalla y un 10 %

    static void LosSaltitos()
    {
        Console.WriteLine("\n  5) LOS SALTITOS: UN CUADRO POR REFRESCO\n");
        double fps; int dobles, parones;
        Cuenta(Reparto(60, 2, TopeAuto(60), 0, out fps), out dobles, out parones);
        Info("el bucle de siempre en una pantalla de 60 Hz: " + Fm(fps, "0.0") + " cuadros por segundo, " + dobles + " saltos dobles en 10 s");
        Check(dobles >= 30, "con el bucle de siempre se ven los saltitos (" + dobles + " saltos dobles en 10 s): la prueba mira algo que existe");
        int d1, p1, d4, p4;
        Cuenta(Reparto(60, 1.5, TopeAuto(60), 1, out fps), out d1, out p1);
        Cuenta(Reparto(60, 4, TopeAuto(60), 1, out fps), out d4, out p4);
        Info("colocando cada cuadro para el refresco siguiente, sin acompasar el bucle: con cuadros de 1,5 ms, " + d1 + " dobles y " + p1 +
             " parones; con cuadros de 4 ms, " + d4 + " y " + p4);
        Check(d4 + p4 >= 30, "colocar la estrella para el refresco siguiente no basta si el bucle va por su cuenta: con cuadros de 4 ms da " + (d4 + p4) + " tirones en 10 s");
        // Acompasado: sin tirones tarde lo que tarde el cuadro (hasta casi un refresco) y a 60, 75 y 144 Hz.
        bool bien = true; string visto = ""; double desvio = 0;
        foreach (double r in new double[] { .5, 2, 4, 8, 12 })
        {
            List<double> pasos = Reparto(60, r, TopeAuto(60), 2, out fps);
            Cuenta(pasos, out dobles, out parones);
            foreach (double p in pasos) desvio = Math.Max(desvio, Math.Abs(p - 1));
            visto += (visto.Length > 0 ? ", " : "") + Fm(r, "0.#") + " ms: " + (dobles + parones);
            if (dobles + parones != 0 || Math.Abs(fps - 60) > .5) bien = false;
        }
        Check(bien, "acompasado al refresco, sin tirones tarde lo que tarde el cuadro (tirones en 10 s con cuadros de " + visto + ") y a 60 cuadros por segundo");
        Check(desvio < 1e-6, "y cada refresco la muestra avanzada EXACTAMENTE un refresco (lo que mas se aparta: " + desvio.ToString("0.0e0", System.Globalization.CultureInfo.InvariantCulture) + ")");
        bien = true; visto = "";
        foreach (int hz in new int[] { 75, 144 })
        {
            Cuenta(Reparto(hz, hz > 100 ? 2 : 4, TopeAuto(hz), 2, out fps), out dobles, out parones);
            visto += (visto.Length > 0 ? ", " : "") + hz + " Hz: " + (dobles + parones) + " tirones, " + Fm(fps, "0.0") + " cuadros/s";
            if (dobles + parones != 0 || Math.Abs(fps - hz) > .5) bien = false;
        }
        Check(bien, "y en otras pantallas, un cuadro por refresco (" + visto + ")");
        // Con video a la vista el panel va a 30 (33 ms): un cuadro cada DOS refrescos, siempre igual.
        List<double> v30 = Reparto(60, 4, 33, 2, out fps);
        bool alterna = v30.Count > 100;
        for (int i = 0; i < v30.Count; i++)
        {
            double p = v30[i];
            if (Math.Abs(p) > .02 && Math.Abs(p - 2) > .02) alterna = false;
            if (i > 0 && Math.Abs(p - v30[i - 1]) < .02) alterna = false;
        }
        Check(alterna && Math.Abs(fps - 30) < .5, "con el tope de 30 del video, un cuadro cada dos refrescos, parejo (" + Fm(fps, "0.0") + " cuadros/s)");
        MethodInfo sig = reloj.GetMethod("Siguiente", F);
        Check(double.IsNaN((double)sig.Invoke(null, new object[] { 100.0, 22, 3.3, 1000.0 / 60 })) &&
              !double.IsNaN((double)sig.Invoke(null, new object[] { 100.0, 50, 3.3, 1000.0 / 60 })),
              "un tope a mano que no es un numero entero de refrescos (45 en 60 Hz) se respeta como siempre; uno que si (20), acompasado");

        // La rejilla: lo que dice Windows, en ticks, pasado al reloj de la rocola; un periodo absurdo no vale.
        MethodInfo enMs = reloj.GetMethod("EnMs", F);
        object[] a = { 5000.0, 1000000000L, 10000000L / 60, 1000000000L - 30000L, 10000000L, 0.0, 0.0 };
        bool vale = (bool)enMs.Invoke(null, a);
        Check(vale && Math.Abs((double)a[5] - 4997.0) < 1e-6 && Math.Abs((double)a[6] - 16.6666) < .001,
              "la rejilla de Windows pasada al reloj de la rocola: el refresco de hace 3 ms y el periodo de 60 Hz");
        bool absurdos = true;
        foreach (long per in new long[] { 0, 10000000L / 10, 10000000L / 1000 })
            if ((bool)enMs.Invoke(null, new object[] { 5000.0, 1000000000L, per, 1000000000L, 10000000L, 0.0, 0.0 })) absurdos = false;
        Check(absurdos, "un periodo absurdo (0, 10 Hz, 1000 Hz) no se usa");
        MethodInfo rejilla = reloj.GetMethod("Rejilla", F), ahoraMs = gpu.GetMethod("AhoraMs", F);
        // El PC de pruebas puede tener compositor: simular su ausencia en vez de exigir Windows XP real.
        FieldInfo sinCompositor = reloj.GetField("sinCompositor", F);
        object sinOriginal = sinCompositor.GetValue(null);
        object[] b = { (double)ahoraMs.Invoke(null, null), 0.0, 0.0 };
        try
        {
            sinCompositor.SetValue(null, true);
            Check(!(bool)rejilla.Invoke(null, b), "sin compositor simulado (destino XP) no hay rejilla: el bucle y la estrella siguen como siempre");
        }
        finally { sinCompositor.SetValue(null, sinOriginal); }
        FieldInfo prueba = reloj.GetField("Prueba", F);
        long frec = System.Diagnostics.Stopwatch.Frequency;
        prueba.SetValue(null, (Func<long[]>)delegate { return new long[] { frec / 60, System.Diagnostics.Stopwatch.GetTimestamp() - 3 * frec / 1000 }; });
        b = new object[] { (double)ahoraMs.Invoke(null, null), 0.0, 0.0 };
        bool con = (bool)rejilla.Invoke(null, b);
        prueba.SetValue(null, null);
        Check(con && Math.Abs((double)b[2] - 1000.0 / 60) < .01 && Math.Abs((double)b[1] - ((double)b[0] - 3)) < 1,
              "con compositor, la rejilla es la que dice Windows (" + Fm((double)b[2], "0.000") + " ms, el ultimo refresco hace " + Fm((double)b[0] - (double)b[1], "0.0") + " ms)");

        // El cableado: el bucle agenda cada cuadro y, al terminarlo, olvida cuando se veria; la estrella se coloca para
        // ese momento. Y el agendado de verdad, con una rejilla de mentira de 60 Hz (el ultimo refresco, hace 3 ms).
        MethodInfo idle = gpu.GetMethod("OnIdle", F), agenda = gpu.GetMethod("AgendaCuadro", F), update = gpu.GetMethod("UpdateMoonMusic", F);
        int lee, escribe, lee2, escribe2;
        Codigo.UsosDeCampo(idle, "vistoMs", out lee, out escribe);
        Codigo.UsosDeCampo(update, "vistoMs", out lee2, out escribe2);
        Check(agenda != null && Codigo.Llama(idle, "GpuPanel.AgendaCuadro") && escribe >= 1,
              "el bucle de dibujo (OnIdle) agenda cada cuadro (AgendaCuadro) y, al terminarlo, olvida cuando se veria");
        Check(lee2 >= 1 && escribe2 == 0, "la estrella se mueve con el momento en que se vera su cuadro (UpdateMoonMusic)");
        using (Control p = (Control)Activator.CreateInstance(gpu, true))
        {
            double P = 1000.0 / 60;
            double ah = (double)ahoraMs.Invoke(null, null);
            // Una rejilla FIJA en QPC: la misma en ambas llamadas, sin ruido de JIT/reflexion dentro de la consulta.
            long refrescoPrueba = System.Diagnostics.Stopwatch.GetTimestamp() - 3 * frec / 1000;
            prueba.SetValue(null, (Func<long[]>)delegate { return new long[] { frec / 60, refrescoPrueba }; });
            Run(p, "AgendaCuadro", ah, 15);
            double prox1 = (double)Get(p, "proxCuadroMs"), visto1 = (double)Get(p, "vistoMs");
            Run(p, "AgendaCuadro", ah, 33);
            double prox2 = (double)Get(p, "proxCuadroMs");
            Set(p, "proxCuadroMs", 1000.0); Run(p, "AgendaCuadro", 1000.2, 22);
            double prox3 = (double)Get(p, "proxCuadroMs"), visto3 = (double)Get(p, "vistoMs");
            prueba.SetValue(null, null);
            try
            {
                sinCompositor.SetValue(null, true);
                Set(p, "proxCuadroMs", 1000.0); Run(p, "AgendaCuadro", 1000.2, 15);
            }
            finally { sinCompositor.SetValue(null, sinOriginal); }
            double prox4 = (double)Get(p, "proxCuadroMs"), visto4 = (double)Get(p, "vistoMs");
            // El refresco de hace 3 ms es 'ah - 3': este cuadro se vera en el siguiente, y el proximo empieza 2 ms despues.
            Check(Math.Abs(visto1 - (ah - 3 + P)) < .05 && Math.Abs(prox1 - (ah - 3 + P + 2)) < .05 && Math.Abs(prox2 - (ah - 3 + 2 * P + 2)) < .05,
                  "con la rejilla, el cuadro de ahora se vera en el refresco siguiente y el proximo empieza 2 ms despues de el (con el tope de 30 del video, dos refrescos despues)");
            Check(prox3 == 1022.0 && visto3 == -1 && prox4 == 1015.0 && visto4 == -1,
                  "con un tope raro, o sin compositor, se agenda como siempre ('th' despues del objetivo anterior) y sin saber cuando se vera");
        }
        using (Control p = (Control)Activator.CreateInstance(gpu, true))
        {
            double t0 = (double)ahoraMs.Invoke(null, null) + 12;
            Set(p, "vistoMs", t0); Run(p, "UpdateMoonMusic", 0f);
            double c1 = (double)Get(p, "cuadroMs");
            Set(p, "vistoMs", -1.0); Run(p, "UpdateMoonMusic", 0f);
            double c2 = (double)Get(p, "cuadroMs");
            System.Threading.Thread.Sleep(30);
            Run(p, "UpdateMoonMusic", 0f);
            double c3 = (double)Get(p, "cuadroMs");
            Check(c1 == t0 && c2 == t0 && c3 > t0, "un cuadro que no sabe cuando se vera usa el reloj de siempre, sin ir nunca hacia atras");
        }
    }

    static void ElInterruptor()
    {
        Console.WriteLine("\n  6) EL INTERRUPTOR, LA MEMORIA Y EL COSTE\n");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_estrella3d_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        form.GetField("carpetaDatos", F).SetValue(null, dir);
        object owner = Codigo.FormSinEncender(form);
        using (Control panel = (Control)Activator.CreateInstance(gpu, true))
        {
            form.GetField("gpuPanel", F).SetValue(owner, panel);
            using (Font f = new Font("Segoe UI", 9))
            using (Panel page = (Panel)form.GetMethod("PaginaFondo", F).Invoke(owner, new object[] { f, f, f, f, Color.White, Color.Gray, Color.Gray }))
            {
                bool dentro = true;
                Control t = Find(page, "Estrella en 3D girando");
                Check(t != null, "Ctrl+F3 -> Fondo tiene \"Estrella en 3D girando\"");
                if (t != null)
                {
                    Control card = t.Parent;
                    foreach (Control c in card.Controls)
                    {
                        if (c.Left < 0 || c.Top < 0 || c.Right > card.ClientSize.Width || c.Bottom > card.ClientSize.Height) dentro = false;
                        foreach (Control d in card.Controls) if (c != d && c.Bounds.IntersectsWith(d.Bounds)) dentro = false;
                    }
                    Check(dentro, "cabe en su tarjeta sin tapar nada");
                    // Primero se enciende: la rocola armada sin constructor lo tiene apagado, y marcar lo que ya esta
                    // marcado no avisa a nadie.
                    foreach (bool b in new bool[] { true, false, true })
                    {
                        t.GetType().GetProperty("Checked", F).SetValue(t, b, null);
                        form.GetMethod("SaveConfigPrefs", F).Invoke(owner, null);
                        object leido = Codigo.FormSinEncender(form); form.GetMethod("LoadConfigPrefs", F).Invoke(leido, null);
                        Check((bool)Get(leido, "estrella3d") == b && (bool)Get(panel, "Estrella3D") == b, "apagado/encendido se guarda (estrella_3d=" + (b ? 1 : 0) + ") y se aplica al panel");
                    }
                }
            }
        }
        try { Directory.Delete(dir, true); } catch { }
        foreach (string m in new string[] { "PoseEstrella", "DrawStar3d", "DrawStarEchoes3d", "PreparaEcos3d", "UpdateStarEchoes3d", "StarPitch" })
        {
            bool nada = true; foreach (Codigo.Paso s in Codigo.Pasos(gpu.GetMethod(m, F))) if (s.Op == 0x8D) nada = false;
            Check(nada, "dibujar no reserva arreglos: " + m);
        }
        foreach (string m in new string[] { "Proyecta", "Ordena", "Luz", "ColorCara" })
        {
            bool nada = true; foreach (Codigo.Paso s in Codigo.Pasos(e3.GetMethod(m, F))) if (s.Op == 0x8D || s.Op == 0x73) nada = false;
            Check(nada, "ni la geometria: " + m);
        }
        // El renderizador por procesador no sabe nada de esto.
        bool cpu = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(form))
            foreach (string l in Codigo.Llamadas(m)) if (l.StartsWith("Estrella3d.") || l.StartsWith("RelojPantalla.")) cpu = true;
        Check(!cpu, "el renderizador por procesador no usa nada de la estrella 3D");
        MethodInfo dibuja = gpu.GetMethod("DrawDetailedMoon", F);
        int lee3d, escribe3d; Codigo.UsosDeCampo(dibuja, "Estrella3D", out lee3d, out escribe3d);
        Check(lee3d >= 1 && Codigo.Llama(dibuja, "GpuPanel.DrawStar3d"), "el panel dibuja la estrella 3D solo si el interruptor lo pide (DrawDetailedMoon)");
        // Lo que cuesta: una pose (proyectar, ordenar, luz y triangulos) de la estrella y doce copias, en el procesador.
        using (Control p = (Control)Activator.CreateInstance(gpu, true))
        {
            Array caras = Array.CreateInstance(vtx, 12 * 60), filo = Array.CreateInstance(vtx, 12 * 180);
            MethodInfo pose = gpu.GetMethod("PoseEstrella", F);
            object[] a = { caras, 0, filo, 0, 500f, 300f, 80f, .5, .22, .1, 0xFF8899AAu, 255f, false, 1.1f, 0, 0, 0f };
            for (int i = 0; i < 200; i++) pose.Invoke(p, a);
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            int n = 13 * 400;
            for (int i = 0; i < n; i++) { a[7] = i * .01; pose.Invoke(p, a); }
            double us = sw.Elapsed.TotalMilliseconds * 1000 / n;
            Info("una pose cuesta " + Fm(us, "0.0") + " us (con la llamada por reflexion; Mono, esta maquina): la estrella y 12 copias, " + Fm(us * 13 / 1000, "0.00") + " ms por cuadro");
            Check(us * 13 < 1000, "la estrella y sus 12 copias caben de sobra en un cuadro (menos de 1 ms)");
        }
    }
    static Control Find(Control p, string name) { foreach (Control c in p.Controls) { if (c.Text == name) return c; Control r = Find(c, name); if (r != null) return r; } return null; }

    [STAThread]
    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        gpu = asm.GetType("Rocola.GpuPanel"); e3 = asm.GetType("Rocola.Estrella3d"); reloj = asm.GetType("Rocola.RelojPantalla");
        form = asm.GetType("Rocola.Form1"); vtx = asm.GetType("Rocola.D3D9+Vtx");
        if (gpu == null || e3 == null || reloj == null)
        {
            Console.WriteLine("  FALLO no esta la estrella 3D en este ejecutable (es el perfil de tarjeta grafica?)");
            return 1;
        }
        ElSolido(); ElGiro(); LaLuz(); LaEstela(); LosSaltitos(); ElInterruptor();
        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN (" + oks + " comprobaciones)" : fallos + " FALLO(S)"));
        return fallos;
    }
}
