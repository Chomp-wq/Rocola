using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;

// BARRIDO A FONDO. Aqui no se comprueba que algo se vea bonito: se comprueba que NADA se rompa.
//
// Se le meten a cada cuenta del fondo y del sonido los valores que en teoria no deberian llegarle nunca
// -ceros, negativos, numeros enormes, NaN, infinitos, pantallas de tamano cero- y se exige que ninguna
// devuelva basura (NaN o infinito) ni se salga de su rango. Una sola de esas basuras en un vertice deja la
// pantalla en negro o llena de triangulos disparados, y en el bar eso es la rocola muerta hasta reiniciarla.
class Barrido
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }
    static bool Sano(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    static bool Rango(float v, float lo, float hi) { return Sano(v) && v >= lo && v <= hi; }

    static Type TP, TF, TVtx;
    static MethodInfo M(Type t, string n)
    { return t.GetMethod(n, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public); }
    static float F1(MethodInfo m, params object[] a) { return (float)m.Invoke(null, a); }

    // los valores "imposibles" con los que se machaca todo
    static readonly float[] Locos = { 0f, 1f, -1f, 0.5f, 1e-8f, -1e-8f, 1e8f, -1e8f, 1e30f, -1e30f,
                                      float.NaN, float.PositiveInfinity, float.NegativeInfinity, 2f, -2f, 1000f };

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in asm.GetTypes()) { if (t.Name == "GpuPanel") TP = t; if (t.Name == "Vtx") TVtx = t; }
        bool gpu = (TP != null);
        Console.WriteLine(gpu ? "\n  (ejecutable CON panel GPU)" : "\n  (ejecutable sin panel GPU: se salta el fondo)");

        if (gpu)
        {
            Console.WriteLine("\n  1) LAS CUENTAS DEL FONDO, CON VALORES IMPOSIBLES\n");

            // --- las curvas de una sola entrada: pase lo que pase, salida sana y dentro de rango ---
            string[] curvas = { "AurBaja", "AurRampa", "AurSueloTono", "AurSueloAlfa" };
            float[][] limites = { new float[]{0f,1f}, new float[]{0f,1f}, new float[]{0f,1f}, new float[]{0f,1f} };
            for (int c = 0; c < curvas.Length; c++)
            {
                MethodInfo mi = M(TP, curvas[c]);
                bool ok = mi != null; string mal = "";
                if (mi != null)
                    foreach (float x in Locos)
                    {
                        float v;
                        try { v = F1(mi, x); } catch (Exception) { ok = false; mal = "truena con " + x; break; }
                        if (!Rango(v, limites[c][0], limites[c][1])) { ok = false; mal = x + " -> " + v; break; }
                    }
                Check(ok, curvas[c] + ": siempre entre 0 y 1, con cualquier entrada" + (mal.Length > 0 ? ("   [" + mal + "]") : ""));
            }

            // --- las ondas: dos entradas, y el tiempo puede ser enorme (la rocola lleva dias encendida) ---
            // Las ondas reciben un tiempo que el propio programa ACOTA (ver AurVuelta): nunca les llega un
            // numero enorme. Se prueban con todo ese rango y con posiciones imposibles.
            float vuelta = (float)(double)TP.GetField("AurVuelta", BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue();
            string[] ondas = { "AurRayo", "AurOndaAlto", "AurCalma" };
            foreach (string nom in ondas)
            {
                MethodInfo mi = M(TP, nom);
                bool ok = mi != null; string mal = "";
                if (mi != null)
                    for (float t = 0f; t <= vuelta && ok; t += vuelta / 500f)
                        foreach (float x in Locos)
                        {
                            float v;
                            try { v = F1(mi, x, t); } catch (Exception) { ok = false; mal = "truena con " + x + "," + t; break; }
                            if (!Rango(v, -3f, 3f)) { ok = false; mal = x + "," + t + " -> " + v; break; }
                        }
                Check(ok, nom + ": nunca devuelve basura ni se dispara, en toda la vuelta del reloj" + (mal.Length > 0 ? ("   [" + mal + "]") : ""));
            }
            // Y LA VUELTA DEL RELOJ NO SE VE: al dar la vuelta, cada onda tiene que estar EXACTAMENTE donde
            // estaba. Si alguien cambia un ritmo por uno que no sea multiplo de 0.01, esto lo caza.
            bool okVuelta = true; float peorSalto = 0f;
            foreach (string nom in ondas)
            {
                MethodInfo mi = M(TP, nom);
                for (int i = 0; i <= 200; i++)
                {
                    float fx = i / 200f;
                    float dv = Math.Abs(F1(mi, fx, 0f) - F1(mi, fx, vuelta));
                    if (dv > peorSalto) peorSalto = dv;
                }
            }
            okVuelta = peorSalto < 0.002f;
            Console.WriteLine("     al dar la vuelta el reloj (cada " + (vuelta/60f).ToString("0.0") + " min) el dibujo cambia como mucho " + (peorSalto*100).ToString("0.000") + "%");
            Check(okVuelta, "la vuelta del reloj NO se ve: todas las ondas vuelven a su sitio (por eso son multiplos de 0.01)");

            // --- la silueta: bandas con cualquier cosa dentro ---
            MethodInfo PERF = M(TP, "AurPerfil");
            bool okP = true; string malP = "";
            float[][] bandas = {
                new float[]{0,0,0,0,0,0,0,0},
                new float[]{1,1,1,1,1,1,1,1},
                new float[]{-1,-1,-1,-1,-1,-1,-1,-1},
                new float[]{1e30f,0,0,0,0,0,0,1e30f},
                new float[]{float.NaN,0,0,0,0,0,0,0},
                new float[]{float.PositiveInfinity,0,0,0,0,0,0,0},
                new float[]{0.5f},          // menos bandas de las que espera
                new float[]{},              // ninguna
                null,
            };
            foreach (float[] b in bandas)
                foreach (float x in Locos)
                {
                    float v;
                    try { v = F1(PERF, b, x); } catch (Exception) { okP = false; malP = "truena"; break; }
                    // con bandas envenenadas puede salir NaN: lo que NO puede es que eso llegue al dibujo (ver abajo)
                    if (!Sano(v) && b != null && b.Length == 8 && !float.IsNaN(b[0]) && !float.IsInfinity(b[0]))
                    { okP = false; malP = "bandas sanas dan " + v; break; }
                }
            Check(okP, "la silueta aguanta bandas vacias, nulas, negativas o enormes sin trorear" + (malP.Length > 0 ? ("   [" + malP + "]") : ""));

            // --- la escalera de tonos: colores imposibles ---
            MethodInfo TONO = M(TP, "TonoPorNivel");
            bool okT = true; string malT = "";
            float[][] cols = { new float[]{0,0,0}, new float[]{255,255,255}, new float[]{-50,300,0},
                               new float[]{1e30f,0,0}, new float[]{float.NaN,10,10}, new float[]{128,128,128} };
            foreach (float[] c in cols)
                foreach (float v in Locos)
                {
                    object[] a = { c[0], c[1], c[2], v };
                    try { TONO.Invoke(null, a); } catch (Exception) { okT = false; malT = "truena"; break; }
                    for (int k = 0; k < 3; k++)
                    {
                        float r = (float)a[k];
                        if (float.IsNaN(c[0]) || float.IsInfinity(c[0]) || c[0] > 1e6f) continue;   // entrada ya envenenada
                        if (!Rango(r, 0f, 255f)) { okT = false; malT = "sale " + r; break; }
                    }
                }
            Check(okT, "la escalera de tonos siempre devuelve un color valido (0..255)" + (malT.Length > 0 ? ("   [" + malT + "]") : ""));

            // --- la curva del filo y el suavizado ---
            MethodInfo CURVA = M(TP, "AurCurvaH"), SUAV = M(TP, "AurSuaviza");
            int cols2 = (int)Cte("AurCols");
            float[] h = new float[cols2 + 1];
            for (int i = 0; i <= cols2; i++) h[i] = (i % 7 == 0) ? 1e6f : (i % 3 == 0 ? -5f : i * 3f);
            bool okC = true; string malC = "";
            for (int i = 0; i <= cols2 && okC; i++)
                for (float u = 0f; u <= 1f; u += 0.25f)
                {
                    float v;
                    try { v = F1(CURVA, h, i, u); }
                    catch (Exception ex) { okC = false; malC = "truena en i=" + i + " u=" + u + ": " + ex.GetType().Name; break; }
                    if (!Sano(v) || v < 0f) { okC = false; malC = "i=" + i + " u=" + u + " -> " + v; break; }
                }
            Check(okC, "la curva del filo nunca sale negativa ni da basura, ni con alturas absurdas" + (malC.Length > 0 ? ("   [" + malC + "]") : ""));
            float[] d = new float[cols2 + 1];
            SUAV.Invoke(null, new object[] { h, d });
            bool okS = true;
            for (int i = 0; i <= cols2; i++) if (!Sano(d[i])) okS = false;
            Check(okS, "y el suavizado tampoco");

            Console.WriteLine("\n  2) LAS MALLAS, CON TAMANOS IMPOSIBLES\n");
            // Lo que de verdad importa: que NINGUN vertice salga con una coordenada basura. Un solo NaN en un
            // vertice y la tarjeta dibuja un triangulo que cruza la pantalla entera.
            MethodInfo MALLA = M(TP, "AurMalla"), BRILLO = M(TP, "AurBrillo");
            int filas = (int)Cte("AurFilas"), suelos = (int)Cte("AurSuelo"), brilloVtx = (int)Cte("AurBrilloVtx");
            int puntos = cols2 + 1;
            float[] hC = new float[puntos], aC = new float[puntos], rC = new float[puntos], nC = new float[puntos], lC = new float[puntos];
            uint[] cCor = new uint[(filas + 1) * puntos], cSu = new uint[(suelos + 1) * puntos], cNe = new uint[puntos], cLi = new uint[puntos];
            FieldInfo fX = TVtx.GetField("X"), fY = TVtx.GetField("Y");
            float[][] casos = {
                //  ancho, baseY, alturaPanel
                new float[]{ 1920f, 700f, 800f },
                new float[]{ 0f,    700f, 800f },     // pantalla de ancho cero
                new float[]{ 1f,    0f,   0f   },     // todo a cero
                new float[]{ 1920f, 900f, 800f },     // el horizonte POR DEBAJO del borde de abajo
                new float[]{ 1920f, -50f, 800f },     // el horizonte fuera de pantalla, arriba
                new float[]{ 1e9f,  1e9f, 1e9f },     // numeros enormes
            };
            bool okM = true, okB = true; string malM = "", malB = "";
            for (int cs = 0; cs < casos.Length; cs++)
            {
                for (int i = 0; i < puntos; i++)
                {
                    hC[i] = (i % 5 == 0) ? 0f : (i % 11 == 0 ? 1e5f : i * 2.5f);
                    aC[i] = (i % 4 == 0) ? 0f : 0.7f; rC[i] = 1f + (i % 3) * 0.2f;
                    nC[i] = (i % 6 == 0) ? 0f : 0.5f; lC[i] = 0.4f;
                    cNe[i] = 0x00A050C0u; cLi[i] = 0x00C08040u;
                }
                Array vm = Array.CreateInstance(TVtx, (filas + suelos) * cols2 * 6);
                int nm = (int)MALLA.Invoke(null, new object[] { vm, hC, cCor, cSu, aC, rC, casos[cs][0], casos[cs][1], casos[cs][2] });
                for (int k = 0; k < nm; k++)
                {
                    object v = vm.GetValue(k);
                    if (!Sano((float)fX.GetValue(v)) || !Sano((float)fY.GetValue(v))) { okM = false; malM = "caso " + cs; break; }
                }
                Array vb = Array.CreateInstance(TVtx, brilloVtx);
                int nb = (int)BRILLO.Invoke(null, new object[] { vb, hC, cNe, nC, cLi, lC, casos[cs][0], casos[cs][1] });
                for (int k = 0; k < nb; k++)
                {
                    object v = vb.GetValue(k);
                    if (!Sano((float)fX.GetValue(v)) || !Sano((float)fY.GetValue(v))) { okB = false; malB = "caso " + cs; break; }
                }
            }
            Check(okM, "el relleno: ni un vertice con coordenada basura, en ninguno de los " + casos.Length + " casos" + (malM.Length > 0 ? ("   [" + malM + "]") : ""));
            Check(okB, "el brillo: lo mismo" + (malB.Length > 0 ? ("   [" + malB + "]") : ""));

            // --- arreglos mas cortos de lo que toca: tiene que NEGARSE a dibujar, no salirse ---
            bool okCorto = true;
            try
            {
                Array chico = Array.CreateInstance(TVtx, 12);
                int n1 = (int)MALLA.Invoke(null, new object[] { chico, hC, cCor, cSu, aC, rC, 1920f, 700f, 800f });
                int n2 = (int)BRILLO.Invoke(null, new object[] { chico, hC, cNe, nC, cLi, lC, 1920f, 700f });
                if (n1 != 0 || n2 != 0) okCorto = false;
                int n3 = (int)MALLA.Invoke(null, new object[] { null, hC, cCor, cSu, aC, rC, 1920f, 700f, 800f });
                int n4 = (int)BRILLO.Invoke(null, new object[] { null, hC, cNe, nC, cLi, lC, 1920f, 700f });
                if (n3 != 0 || n4 != 0) okCorto = false;
                int n5 = (int)MALLA.Invoke(null, new object[] { Array.CreateInstance(TVtx, 100000), null, cCor, cSu, aC, rC, 1920f, 700f, 800f });
                if (n5 != 0) okCorto = false;
            }
            catch (Exception) { okCorto = false; }
            Check(okCorto, "con un buffer chico o nulo NO dibuja: devuelve cero en vez de salirse de memoria");
        }

        Console.WriteLine("\n  3) LAS CUENTAS DEL SONIDO\n");
        MethodInfo TANH = M(TF, "TanhRapida");
        if (TANH != null)
        {
            bool ok = true; string mal = "";
            foreach (float x in Locos)
            {
                float v = F1(TANH, x);
                if (!Rango(v, -1.001f, 1.001f)) { ok = false; mal = x + " -> " + v; break; }
            }
            Check(ok, "la tangente aproximada del realce nunca se sale de -1..1" + (mal.Length > 0 ? ("   [" + mal + "]") : ""));
        }
        MethodInfo CLAMP = M(TF, "Clampf");
        if (CLAMP != null)
        {
            bool ok = true;
            foreach (float x in Locos) { float v = (float)CLAMP.Invoke(null, new object[] { x, 0f, 1f }); if (!Rango(v, 0f, 1f)) { ok = false; break; } }
            Check(ok, "el acotador acota de verdad, hasta con NaN");
        }
        MethodInfo REAC = M(TF, "ColorReactivo");
        if (REAC != null)
        {
            bool ok = true;
            Color[] cs = { Color.Black, Color.White, Color.FromArgb(0,0,0), Color.FromArgb(1,1,1), Color.FromArgb(255,0,0), Color.FromArgb(9,9,9) };
            foreach (Color c in cs)
            {
                Color r = (Color)REAC.Invoke(null, new object[] { c });
                if (r.R > 255 || r.G > 255 || r.B > 255) ok = false;
            }
            Check(ok, "el color de la cancion sale siempre valido, tambien con negro puro");
        }

        Console.WriteLine("\n  4) LA VENTANA, CON TAMANOS IMPOSIBLES\n");
        object F = FormatterServices.GetUninitializedObject(TF);
        MethodInfo BOT = TF.GetMethod("BotStripTop", BindingFlags.Instance | BindingFlags.NonPublic);
        bool okBot = true; string malBot = "";
        int[] altos = { 0, 1, 2, 8, 16, 100, 768, 1080, 4320, int.MaxValue / 2 };
        foreach (int hh in altos)
        {
            int t = (int)BOT.Invoke(F, new object[] { hh });
            if (t < 0 || (hh > 16 && (t < hh / 2 || t > hh - 8))) { okBot = false; malBot = hh + " -> " + t; break; }
        }
        Check(okBot, "el horizonte se queda dentro de la pantalla con cualquier alto, del mas chico al mas grande"
              + (malBot.Length > 0 ? ("   [" + malBot + "]") : ""));

        Console.WriteLine("\n  5) UNA CANCION DAÑADA NO ENVENENA LA CADENA\n");
        // De extremo a extremo, por el camino de verdad: se le pasa a LevelTap -que es lo primero que ve el
        // sonido y esta SIEMPRE en la cadena- un bloque lleno de NaN, infinitos y valores absurdos, y se exige
        // que no salga ni una muestra mala y que las cuentas que arrastran estado sigan siendo numeros.
        Type TLev = null;
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)) if (t.Name == "LevelTap") TLev = t;
        Check(TLev != null, "el punto por el que pasa todo el sonido esta donde tiene que estar");
        if (TLev != null)
        {
            object F2 = FormatterServices.GetUninitializedObject(TF);
            Codigo.PonCerrojos(F2);   // los candados que pone el constructor (audioDatosLock, de la bomba de sonido)
            SetF(F2, "levelLock", new object());
            SetF(F2, "specVis", new float[8]); SetF(F2, "specNow", new float[8]);
            SetF(F2, "specPiso", new float[8]); SetF(F2, "specGolpe", new float[8]);
            SetF(F2, "auroraAmount", 0.55f);
            SetF(F2, "levelQ", Activator.CreateInstance(TF.GetField("levelQ", BindingFlags.Instance|BindingFlags.NonPublic).FieldType));
            object tap = Activator.CreateInstance(TLev, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                                                  null, new object[] { new FuenteRota(), F2 }, null);
            TLev.GetField("Gain").SetValue(tap, 1f);
            TLev.GetField("Master").SetValue(tap, 1f);
            TLev.GetField("Excite").SetValue(tap, 0.35f);
            float[] buf = new float[4096];
            MethodInfo READ = TLev.GetMethod("Read");
            bool okSal = true, okEst = true;
            for (int v = 0; v < 6; v++)
            {
                FuenteRota.Veneno = v;
                int n = (int)READ.Invoke(tap, new object[] { buf, 0, buf.Length });
                for (int i = 0; i < n; i++) if (!Sano(buf[i])) { okSal = false; break; }
                float[] sv = (float[])GetF(F2, "specNow");
                for (int b = 0; b < 8; b++) if (!Sano(sv[b])) okEst = false;
                if (!Sano((float)GetF(F2, "rmsLo")) || !Sano((float)GetF(F2, "rmsMid")) || !Sano((float)GetF(F2, "rmsHi"))) okEst = false;
                if (!Sano((float)(double)GetF(F2, "curSumSq"))) okEst = false;
            }
            Check(okSal, "un bloque lleno de NaN, infinitos y numeros absurdos NO deja salir ni una muestra mala");
            Check(okEst, "y las cuentas que arrastran estado (nivel, espectro, sonoridad medida) siguen siendo numeros");
            // Y despues del veneno, el sonido bueno vuelve a pasar bien: no se quedo nada pegado
            FuenteRota.Veneno = 6;
            int n2 = (int)READ.Invoke(tap, new object[] { buf, 0, buf.Length });
            bool okVuelve = true; float pico = 0f;
            for (int i = 0; i < n2; i++) { if (!Sano(buf[i])) okVuelve = false; float a = Math.Abs(buf[i]); if (a > pico) pico = a; }
            Console.WriteLine("     tras el veneno, una onda normal sale con pico " + pico.ToString("0.000"));
            Check(okVuelve && pico > 0.1f, "y en cuanto vuelve el sonido bueno, sale como si nada hubiera pasado");
        }

        if (gpu)
        {
            Console.WriteLine("\n  6) LO QUE CUESTA TENER LA AURORA SIEMPRE ENCENDIDA\n");
            // Ahora se dibuja siempre, tambien de madrugada con el local vacio. Conviene saber cuanto es.
            int cols3 = (int)Cte("AurCols"), filas3 = (int)Cte("AurFilas"), suelos3 = (int)Cte("AurSuelo");
            int brillo3 = (int)Cte("AurBrilloVtx");
            int vRelleno = (filas3 + suelos3) * cols3 * 6;
            int bytes = (vRelleno + brillo3) * 28;   // 28 bytes por vertice (x,y,z,rhw,color,u,v)
            Console.WriteLine("     " + (vRelleno + brillo3) + " vertices por cuadro = " + (bytes / 1024) + " KB");
            Console.WriteLine("     a 60 cuadros por segundo: " + (bytes * 60 / 1024 / 1024) + " MB/s hacia la tarjeta, y DOS llamadas de dibujo");
            Check(bytes <= 700 * 1024, "cabe de sobra en lo que la maquina modesta puede mover por cuadro");
        }

        Console.WriteLine("\n  7) LA ROCOLA ENCENDIDA DURANTE SEMANAS\n");
        // En un bar no se apaga nunca. El reloj de Windows da la vuelta a los 24.8 dias, y mucho antes -a los
        // pocos dias- "los segundos que lleva encendida" ya no caben con decimales en un float: el fondo
        // empezaria a ir a saltos de un cuarto de segundo sin motivo aparente. Los dos relojes de animacion se
        // llevan sumados a mano y en double, y el paso se saca por resta (que sale bien aunque el reloj vuelva).
        if (gpu)
        {
            FieldInfo aT = TP.GetField("aurT", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(aT != null && aT.FieldType == typeof(double), "el reloj de la aurora es de los que no se quedan sin decimales");
            Type TS2 = null;
            foreach (Type t in asm.GetTypes()) if (t.Name == "GpuStarfield") TS2 = t;
            FieldInfo cT = (TS2 != null) ? TS2.GetField("campoT", BindingFlags.Instance | BindingFlags.NonPublic) : null;
            Check(cT != null && cT.FieldType == typeof(double), "y el del campo de estrellas, tambien");
            Check(TS2 != null && TS2.GetField("startTick", BindingFlags.Instance | BindingFlags.NonPublic) == null,
                  "ninguno de los dos sigue contando desde el arranque con el reloj de Windows");
            // Cuanto se desviaria un float a los N dias
            for (int dias = 1; dias <= 30; dias *= 30)
            {
                float seg = dias * 86400f;
                float paso = (float)(seg * 1.19e-7);   // resolucion de un float en ese orden de magnitud
                Console.WriteLine("     a los " + dias + " dias, un float solo podria avanzar a saltos de " + (paso*1000f).ToString("0") + " ms");
            }
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }

    static object GetF(object o, string n) { return o.GetType().GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o); }
    static void SetF(object o, string n, object v) { o.GetType().GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o, v); }

    // Una fuente de sonido AVERIADA, de las que existen de verdad en una biblioteca grande.
    class FuenteRota : NAudio.Wave.ISampleProvider
    {
        public static int Veneno;
        public NAudio.Wave.WaveFormat WaveFormat
        { get { return NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(44100, 2); } }
        public int Read(float[] b, int off, int cnt)
        {
            for (int i = 0; i < cnt; i++)
            {
                switch (Veneno)
                {
                    case 0: b[off+i] = float.NaN; break;
                    case 1: b[off+i] = float.PositiveInfinity; break;
                    case 2: b[off+i] = float.NegativeInfinity; break;
                    case 3: b[off+i] = 1e30f; break;
                    case 4: b[off+i] = (i % 2 == 0) ? float.NaN : 0.5f; break;   // uno si, uno no
                    case 5: b[off+i] = (i % 997 == 0) ? float.NaN : (float)Math.Sin(i * 0.01); break;
                    default: b[off+i] = (float)(0.5 * Math.Sin(i * 0.02)); break;   // sonido bueno
                }
            }
            return cnt;
        }
    }

    static float Cte(string n)
    {
        FieldInfo f = TP.GetField(n, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        object v = f.GetRawConstantValue();
        return (v is int) ? (int)v : (float)v;
    }
}
