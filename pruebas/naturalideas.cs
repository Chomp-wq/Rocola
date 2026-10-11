using System;
using System.Drawing;
using System.Reflection;

// ESPACIO NATURAL, TANDA DEL 11-10-2026: las 34 ideas de la presentacion "Un cielo mas real y mas bello".
// Comprueba las cuentas puras (boveda, hora, grano, vineta, gas, Via Lactea, estrellas, fugaces, lluvias,
// aurora, metal, polvo, ecos y luna) y que cada pieza este enchufada donde se dibuja. No abre Direct3D,
// no suena musica y no mide FPS: el aspecto real y la fluidez se comprueban aparte, en la tarjeta.
class NaturalIdeas
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Assembly asm; static Type panel, stars, form;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) { ok++; } else { mal++; Console.WriteLine("  FALLO " + s); } }
    static object Run(Type t, string name, params object[] a)
    {
        foreach (MethodInfo m in t.GetMethods(B))
            if (m.Name == name && m.GetParameters().Length == a.Length)
                try { return m.Invoke(null, a); } catch (TargetInvocationException e) { throw e.InnerException; }
        throw new Exception("falta " + t.Name + "." + name);
    }
    static int R(uint c) { return (int)((c >> 16) & 255); }
    static int G(uint c) { return (int)((c >> 8) & 255); }
    static int Bl(uint c) { return (int)(c & 255); }
    static int A(uint c) { return (int)(c >> 24); }

    static int Main(string[] args)
    {
        asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        panel = asm.GetType("Rocola.GpuPanel", true); stars = asm.GetType("Rocola.GpuStarfield", true); form = asm.GetType("Rocola.Form1", true);
        Boveda(); Grano(); Gas(); Estrellas(); Fugaces(); Aurora(); Estrella(); Luna(); Enchufes();
        Console.WriteLine(mal == 0 ? "  OK naturalideas: " + ok + " comprobaciones (cuentas y enchufes; sin HAL ni FPS)" : "  FALLO naturalideas: " + mal + " de " + (ok + mal));
        return mal;
    }

    static uint Cielo(float v, float lado, float hondo, float crep)
    { return (uint)Run(stars, "ColorCieloNatural", 0xFF030610u, 0xFF0B1936u, 0xFF10264Bu, .45f, v, lado, hondo, crep); }
    static void Boveda()
    {
        // 2) La curva pasa por las tres paradas y no tiene quiebre en la del medio.
        foreach (float corte in new float[] { .2f, .45f, .7f })
        {
            float c0 = 6, c1 = 40, c2 = 75;
            Check(Math.Abs((float)Run(stars, "Curva3", 0f, corte, c0, c1, c2) - c0) < .01f, "la curva empieza en la primera parada");
            Check(Math.Abs((float)Run(stars, "Curva3", corte, corte, c0, c1, c2) - c1) < .01f, "la curva pasa por la parada del medio");
            Check(Math.Abs((float)Run(stars, "Curva3", 1f, corte, c0, c1, c2) - c2) < .01f, "la curva acaba en la ultima parada");
            float e = .002f;
            float izq = ((float)Run(stars, "Curva3", corte, corte, c0, c1, c2) - (float)Run(stars, "Curva3", corte - e, corte, c0, c1, c2)) / e;
            float der = ((float)Run(stars, "Curva3", corte + e, corte, c0, c1, c2) - (float)Run(stars, "Curva3", corte, corte, c0, c1, c2)) / e;
            float linIzq = (c1 - c0) / corte, linDer = (c2 - c1) / (1 - corte);
            Check(Math.Abs(izq - der) < Math.Abs(linIzq - linDer) * .25f + .5f, "sin quiebre en la parada del medio (corte " + corte + ")");
            float prev = c0;
            for (int i = 1; i <= 200; i++)
            {
                float v = (float)Run(stars, "Curva3", i / 200f, corte, c0, c1, c2);
                Check(v >= prev - .001f, "la curva no da vuelta atras"); prev = v;
            }
        }
        uint arriba = Cielo(0f, 0, 0, 0), medio = Cielo(.45f, 0, 0, 0);
        Check(arriba == 0xFF030610u, "arriba del todo, el color de arriba");
        Check(Math.Abs(Bl(medio) - 0x36) <= 1, "en el corte, el color del medio");
        // 1) El resplandor del aire: algo de verde sobre el horizonte, nada arriba.
        int verde = G(Cielo(.80f, 0, 0, 0)) - (int)Math.Round((float)Run(stars, "Curva3", .80f, .45f, (float)0x06, (float)0x19, (float)0x26));
        Check(verde >= 5 && verde <= 8, "resplandor del aire tenue y verdoso (+" + verde + ")");
        // 3) La luz del costado: abajo y del lado de la luna, mas clara; arriba nada.
        Check(Bl(Cielo(.95f, 1, 0, 0)) > Bl(Cielo(.95f, 0, 0, 0)) + 6, "luz lateral abajo");
        Check(Cielo(0f, 1, 0, 0) == Cielo(0f, 0, 0, 0), "luz lateral nula arriba");
        // 34) La hora: mas hondo de madrugada; crepusculo calido abajo.
        Check(Bl(Cielo(.2f, 0, 1, 0)) < Bl(Cielo(.2f, 0, 0, 0)), "madrugada mas honda");
        Check(R(Cielo(.98f, 0, 0, 1)) > R(Cielo(.98f, 0, 0, 0)) + 10 && Bl(Cielo(.98f, 0, 0, 1)) < Bl(Cielo(.98f, 0, 0, 0)), "crepusculo calido abajo");
        Check(R(Cielo(.05f, 0, 0, 1)) == R(Cielo(.05f, 0, 0, 0)), "el crepusculo no tiñe lo alto");
        object[] h = { 3f, 0f, 0f }; Run(panel, "HoraDelCielo", h);
        Check(Math.Abs((float)h[1] - 1f) < .01f, "a las 3 el cielo esta en lo mas hondo");
        h = new object[] { 22f, 0f, 0f }; Run(panel, "HoraDelCielo", h);
        Check((float)h[1] < .01f && (float)h[2] < .4f, "a las 22 ni hondo ni crepusculo fuerte");
        h = new object[] { 19.5f, 0f, 0f }; Run(panel, "HoraDelCielo", h);
        Check((float)h[2] > .99f, "a las 19:30, crepusculo");
        foreach (float rara in new float[] { float.NaN, -5f, 49f, float.PositiveInfinity })
        {
            h = new object[] { rara, 0f, 0f }; Run(panel, "HoraDelCielo", h);
            Check(!float.IsNaN((float)h[1]) && !float.IsNaN((float)h[2]), "hora rara no rompe el cielo");
        }
        Check(Cielo(float.NaN, float.NaN, float.NaN, float.NaN) >> 24 == 255, "sin NaN en la boveda");
    }

    static double Desviacion(Bitmap b, int bloque)
    {
        double suma = 0, suma2 = 0; int n = 0;
        for (int by = 0; by + bloque <= b.Height; by += bloque)
            for (int bx = 0; bx + bloque <= b.Width; bx += bloque)
            {
                int claros = 0;
                for (int y = 0; y < bloque; y++) for (int x = 0; x < bloque; x++) if (b.GetPixel(bx + x, by + y).R > 128) claros++;
                suma += claros; suma2 += claros * (double)claros; n++;
            }
        double m = suma / n; return Math.Sqrt(Math.Max(0, suma2 / n - m * m));
    }
    static void Grano()
    {
        // 4) Mismo alfa y misma mitad de claros que el grano de siempre, pero sin grumos.
        using (Bitmap nat = (Bitmap)Run(panel, "BakeRuidoNatural", 128))
        using (Bitmap leg = (Bitmap)Run(form, "BakeRuidoGrande", 128))
        {
            int claros = 0, alfa = (int)form.GetField("RuidoAlfa", B).GetValue(null); bool alfaBien = true;
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++) { Color c = nat.GetPixel(x, y); if (c.R > 128) claros++; if (c.A != alfa) alfaBien = false; }
            Check(alfaBien, "grano Natural con el mismo alfa");
            Check(claros > 128 * 128 * .47 && claros < 128 * 128 * .53, "grano Natural: mitad de claros (" + claros + ")");
            double dn = Desviacion(nat, 8), dl = Desviacion(leg, 8);
            Console.WriteLine("  grano por bloques de 8x8: natural " + dn.ToString("0.00") + ", legacy " + dl.ToString("0.00"));
            Check(dn < dl * .6, "grano Natural mas parejo que el de siempre");
        }
        // La teja grande se arma repitiendo la pieza (sin cuenta por pixel): igual alfa y mitad de claros en toda ella.
        int pieza = (int)panel.GetField("RuidoNaturalPieza", B).GetRawConstantValue();
        using (Bitmap grande = (Bitmap)Run(panel, "BakeRuidoNatural", 1024))
        using (Bitmap chica = (Bitmap)Run(panel, "BakeRuidoNatural", pieza))
        {
            bool repite = grande.Width == 1024 && grande.Height == 1024, alfaGrande = true; int clarosGrande = 0;
            for (int y = 0; y < 1024; y += 3)
                for (int x = 0; x < 1024; x += 5)
                {
                    Color c = grande.GetPixel(x, y);
                    if (c.ToArgb() != chica.GetPixel(x % pieza, y % pieza).ToArgb()) repite = false;
                    if (c.A != (int)form.GetField("RuidoAlfa", B).GetValue(null)) alfaGrande = false;
                    if (c.R > 128) clarosGrande++;
                }
            int vistos = ((1024 + 2) / 3) * ((1024 + 4) / 5);
            Check(repite, "teja grande de grano = la pieza de " + pieza + " repetida, pixel a pixel");
            Check(alfaGrande && clarosGrande > vistos * .47 && clarosGrande < vistos * .53, "teja grande: mismo alfa y mitad de claros");
        }
        bool rechaza = false; try { Run(panel, "BakeRuidoNatural", 0); } catch (ArgumentOutOfRangeException) { rechaza = true; }
        Check(rechaza, "teja de grano invalida rechazada");
        // 32) Vineta de objetivo: centro limpio, esquina igual que antes, y sube sin escalones.
        using (Bitmap v = (Bitmap)Run(panel, "BakeVinetaNatural", 128))
        {
            Check(v.GetPixel(64, 64).A == 0 && v.GetPixel(0, 0).A == 76, "vineta: centro limpio y esquina de siempre");
            int prev = 0; bool sube = true, suave = true;
            for (int i = 64; i < 128; i++) { int a = v.GetPixel(i, i).A; if (a < prev) sube = false; if (a - prev > 6) suave = false; prev = a; }
            Check(sube && suave, "vineta sube poco a poco, sin anillo");
            Check(v.GetPixel(127, 64).A > 0 && v.GetPixel(127, 64).A < 60, "borde lateral apenas oscurecido");
        }
    }

    static void Gas()
    {
        // 5) El color del gas: polvo azul, oxigeno verde azulado y corazon rosado.
        uint polvo = (uint)Run(panel, "ColorGasNatural", .1f, 0f), oxigeno = (uint)Run(panel, "ColorGasNatural", .5f, .3f), corazon = (uint)Run(panel, "ColorGasNatural", 1f, 1f);
        Check(Bl(polvo) == 255 && R(polvo) < 140, "bordes de polvo azul");
        Check(G(oxigeno) >= R(oxigeno) + 60, "zona de oxigeno verde azulada");
        Check(R(corazon) == 255 && G(corazon) < 140, "corazon rosado");
        // 6) Globulos.
        Check((float)Run(panel, "GlobulosNatural", .21f, .27f) > .99f && (float)Run(panel, "GlobulosNatural", .4f, .9f) < .001f, "globulos donde toca y solo ahi");
        // El tinte de la cancion se aclara sin perder su orden de canales.
        foreach (uint t in new uint[] { 0x315480u, 0x645594u, 0x000000u, 0xFFFFFFu })
        {
            uint a = (uint)Run(panel, "TinteGasNatural", t);
            Check(R(a) >= R(t) && G(a) >= G(t) && Bl(a) >= Bl(t) && (R(t) <= Bl(t)) == (R(a) <= Bl(a)), "tinte del gas aclarado conservando el matiz");
        }
        // 9) Nucleo calido, brazos blanco azulados.
        uint nucleo = (uint)Run(panel, "ColorViaNatural", 1f, 1f), brazo = (uint)Run(panel, "ColorViaNatural", 0f, .5f);
        Check(R(nucleo) == 255 && Bl(nucleo) < 200, "nucleo galactico calido");
        Check(Bl(brazo) == 255 && R(brazo) < 220, "brazos blanco azulados");
        using (Bitmap via = (Bitmap)Run(panel, "BakeViaNatural", 256, 192))
        {
            int calidos = 0, frios = 0;
            for (int y = 0; y < 192; y++) for (int x = 0; x < 256; x++) { Color c = via.GetPixel(x, y); if (c.A < 4) continue; if (c.R > c.B) calidos++; else frios++; }
            Check(calidos > 50 && frios > 50, "la banda tiene nucleo calido y brazos frios (" + calidos + "/" + frios + ")");
        }
    }

    static void Estrellas()
    {
        // 12) La debil se ve casi blanca; la brillante conserva su color.
        uint azul = 0x009BB0FFu;
        uint debil = (uint)Run(stars, "ColorEstrellaNatural", azul | (70u << 24), 0, .3f, 1.0, .5f);
        uint fuerte = (uint)Run(stars, "ColorEstrellaNatural", azul | (255u << 24), 1, .3f, 1.0, .5f);
        Check(R(debil) >= 225 && G(debil) >= 230, "estrella debil casi blanca");
        Check(Bl(fuerte) - R(fuerte) >= 90, "estrella brillante con su color");
        Check(A(debil) == 70 && A(fuerte) == 255, "el brillo no cambia");
        // 13) Dos ritmos: sigue dentro de la tabla.
        float min = 9, max = -9;
        for (int i = 0; i < 400; i++) { float v = (float)Run(stars, "CentelleoNatural", (float)Run(stars, "Titileo", i * .05), i * .137); min = Math.Min(min, v); max = Math.Max(max, v); }
        Check(min >= .449f && max <= 1.001f && max - min > .3f, "centelleo irregular dentro de su rango");
        // 14) Las grandes bajas cambian de color; las altas no.
        uint g0 = 0xFFF8F7FFu; bool cambia = false;
        for (int i = 0; i < 50; i++) if ((uint)Run(stars, "ColorEstrellaNatural", g0, 2, .9f, i * .21, 1f) != (uint)Run(stars, "ColorEstrellaNatural", g0, 2, .9f, 0.0, 1f)) cambia = true;
        Check(cambia, "las grandes bajas destellan de color");
        Check((uint)Run(stars, "ColorEstrellaNatural", g0, 2, .2f, 3.3, 1f) == (uint)Run(stars, "ColorEstrellaNatural", g0, 2, .2f, 7.7, 1f), "las altas no destellan");
        // 15) Puntas con color en el atlas Natural; el nucleo blanco.
        using (Bitmap atlas = (Bitmap)Run(stars, "BakeNaturalStarTexture", 64))
        {
            Color nucleo = atlas.GetPixel(64 + 31, 31), arriba = atlas.GetPixel(64 + 12, 30), abajo = atlas.GetPixel(64 + 12, 33);
            Check(nucleo.R > 240 && nucleo.G > 240 && nucleo.B > 240, "nucleo de la estrella blanco");
            Check((arriba.B - arriba.R) * (abajo.B - abajo.R) <= 0 && Math.Abs(arriba.B - abajo.B) + Math.Abs(arriba.R - abajo.R) > 10, "el filo de los rayos se separa en color");
        }
    }

    static void Fugaces()
    {
        // 17) Se enciende, arde y se apaga.
        Check((float)Run(stars, "BrilloFugazNatural", 1f, false) == 0f, "recien nacida, apagada");
        Check((float)Run(stars, "BrilloFugazNatural", .5f, false) == 1f, "a media vida, encendida");
        Check((float)Run(stars, "BrilloFugazNatural", 0f, true) == 0f && (float)Run(stars, "BrilloFugazNatural", float.NaN, true) == 0f, "al final, apagada");
        Check((float)Run(stars, "BrilloFugazNatural", .24f, true) > (float)Run(stars, "BrilloFugazNatural", .24f, false), "el bolido da su fogonazo");
        // 16) Cabeza verdosa y cola naranja (con los mismos 12 vertices).
        Type vt = asm.GetType("Rocola.D3D9+Vtx", true); Array v = Array.CreateInstance(vt, 12);
        Run(stars, "NaturalShootVertices", v, 0, 100f, 100f, 300f, 150f, 1.4f, .2f, .5f, 1f, 0xFFFFFFu);
        uint cabeza = (uint)vt.GetField("Color").GetValue(v.GetValue(0)), cola = (uint)vt.GetField("Color").GetValue(v.GetValue(2));
        Check(G(cabeza) > R(cabeza) && A(cabeza) > 150, "cabeza verde azulada");
        Check(R(cola) > G(cola) && G(cola) > Bl(cola) && A(cola) == 0, "cola que se apaga en naranja");
        // 19) Lluvias de meteoros en sus fechas.
        object[] p = { new DateTime(2026, 8, 12), 0f, 0f };
        Check((float)Run(stars, "LluviaDeMeteoros", p) > .7f, "Perseidas en su maximo");
        p = new object[] { new DateTime(2026, 6, 15), 0f, 0f };
        Check((float)Run(stars, "LluviaDeMeteoros", p) == 0f, "a mediados de junio no hay lluvia");
        p = new object[] { new DateTime(2027, 1, 3), 0f, 0f };
        Check((float)Run(stars, "LluviaDeMeteoros", p) > .7f, "Cuadrantidas, cruzando el fin de ano");
        p = new object[] { new DateTime(2026, 10, 11), 0f, 0f };
        float hoy = (float)Run(stars, "LluviaDeMeteoros", p);
        Check(hoy >= .25f && hoy < .4f && (float)p[1] > 0 && (float)p[2] > 0, "Oriónidas activas, lejos del maximo");
        p = new object[] { new DateTime(2028, 12, 31), 0f, 0f };
        Run(stars, "LluviaDeMeteoros", p); Check(true, "bisiesto sin error");
        // 30) La corona sigue al destello igual a cualquier ritmo de cuadros (a 60, el 25 % de siempre).
        float k60 = (float)Run(panel, "SuavizaCorona", 1 / 60.0), k30 = (float)Run(panel, "SuavizaCorona", 1 / 30.0), k24 = (float)Run(panel, "SuavizaCorona", 1 / 24.0);
        Check(Math.Abs(k60 - .25f) < .005f, "corona: a 60 cuadros, el 25 % por cuadro");
        Check(Math.Abs((1 - k30) - (1 - k60) * (1 - k60)) < .002f && Math.Abs(Math.Pow(1 - k24, 24) - Math.Pow(1 - k60, 60)) < .002, "corona: el mismo segundo a 24, 30 o 60 cuadros");
        Check((float)Run(panel, "SuavizaCorona", 0.0) == 0f && (float)Run(panel, "SuavizaCorona", double.NaN) == 0f && (float)Run(panel, "SuavizaCorona", -1.0) == 0f && (float)Run(panel, "SuavizaCorona", 5.0) == 1f, "corona: tiempos raros acotados");
    }

    static void Aurora()
    {
        uint verde = (uint)Run(panel, "AuroraRealBase", 1, .3f), alto = (uint)Run(panel, "AuroraRealBase", 5, .3f), pie = (uint)Run(panel, "AuroraRealBase", 0, .3f);
        Check(G(verde) > R(verde) + 100 && G(verde) > Bl(verde), "aurora real: cuerpo verde");
        Check(R(alto) > G(alto) && Bl(alto) > G(alto), "aurora real: arriba rojo y violeta");
        Check(R(pie) > G(pie), "aurora real: filo rosado abajo");
        Check((float)Run(panel, "ReflejoFila", 0f) == 0f && (float)Run(panel, "ReflejoFila", 1f) == 0f && (float)Run(panel, "ReflejoFila", .25f) > 0f, "reflejo solo bajo el horizonte, nunca en el pie ni en el borde");
        Check((uint)Run(panel, "SueloReflejo", 0x203040u, 0x40FF80u, .6f, 1f, 0f, 0f) == ((uint)(int)(.6f * 255f + .5f) << 24 | 0x203040u), "sin reflejo, el suelo de siempre");
        Check((float)Run(panel, "PulsoReposoAurora", .3f, 12f, 0f) == 1f, "con musica no hay pulso de reposo");
        float pmin = 9, pmax = 0;
        for (int i = 0; i < 300; i++) { float q = (float)Run(panel, "PulsoReposoAurora", i / 300f, i * .7f, 1f); pmin = Math.Min(pmin, q); pmax = Math.Max(pmax, q); }
        Check(pmin >= .6f && pmax <= 1.4f && pmax - pmin > .2f, "en reposo laten los parches, sin apagarse");
        Check((float)Run(panel, "ArcoAurora", 0f) == 0f && (float)Run(panel, "ArcoAurora", 1f) == 0f && (float)Run(panel, "ArcoAurora", .5f) > .99f, "la curva no mueve el pie ni la cresta");
        // 21) La curva se abre lejos del marco: la primera y la ultima columna no se despegan del borde de la
        // pantalla (sin rendija), ninguna columna adelanta a la de al lado y la curva no pasa de 1.8 columnas.
        int cols = (int)panel.GetField("AurCols", B).GetRawConstantValue();
        float[] alturas = new float[cols + 1];
        float paso = 1920f / cols; bool bordes = true, sinCruce = true, acotada = true, curva = false;
        for (int prueba = 0; prueba < 40; prueba++)
        {
            for (int i = 0; i <= cols; i++) alturas[i] = 120f + 90f * (float)Math.Sin(i * .21 + prueba * .7) + prueba * 9f;
            Run(panel, "PreparaPliegues", alturas, paso, 1080f);
            float[] c = (float[])panel.GetField("aurCurva", B).GetValue(null);
            if (c[0] != 0f || c[cols] != 0f) bordes = false;
            for (int i = 0; i < cols; i++) if (paso + c[i + 1] - c[i] <= paso * .05f) sinCruce = false;
            for (int i = 0; i <= cols; i++) { if (Math.Abs(c[i]) > paso * 1.8f + .01f) acotada = false; if (Math.Abs(c[i]) > paso * .5f) curva = true; }
        }
        Check(bordes, "la cortina no se despega de los bordes de la pantalla");
        Check(sinCruce && acotada && curva, "la cortina se curva por dentro sin cruzar columnas");
    }

    static void Estrella()
    {
        // 24 y 25) Arriba azul de noche, abajo la aurora, y el canto se enciende.
        uint arriba = (uint)Run(asm.GetType("Rocola.Estrella3d", true), "ColorCaraNaturalEntorno", 0xD0D8E8u, .6f, 0f, 255u, 0f, 1f, .5f, 0x5AE69Au);
        uint abajo = (uint)Run(asm.GetType("Rocola.Estrella3d", true), "ColorCaraNaturalEntorno", 0xD0D8E8u, .6f, 0f, 255u, 0f, -1f, .5f, 0x5AE69Au);
        Check(Bl(arriba) - R(arriba) > Bl(abajo) - R(abajo), "las caras de arriba toman el azul del cielo");
        Check(G(abajo) > G(arriba) - 5, "las de abajo toman la aurora");
        uint frente = (uint)Run(asm.GetType("Rocola.Estrella3d", true), "ColorCaraNaturalEntorno", 0xD0D8E8u, .5f, 0f, 255u, 0f, 0f, 1f, 0x5AE69Au);
        uint canto = (uint)Run(asm.GetType("Rocola.Estrella3d", true), "ColorCaraNaturalEntorno", 0xD0D8E8u, .5f, 0f, 255u, 0f, 0f, .1f, 0x5AE69Au);
        Check(R(canto) > R(frente) + 40, "borde de luz en las caras de canto (Fresnel)");
        // 28) Polvo con luz y sombra, sin saltos.
        float luz = (float)Run(panel, "PolvoAlLado", -1f, -1f), sombra = (float)Run(panel, "PolvoAlLado", 1f, 1f);
        Check(luz > .95f && sombra < .45f, "polvo: lado de la luz y lado de la sombra");
        Check(Math.Abs((float)Run(panel, "PolvoAlLado", .75f, -.66f) - (float)Run(panel, "PolvoAlLado", .7501f, -.6599f)) < .01f, "polvo sin saltos al cruzar la luz");
        // 29) Ecos que envejecen.
        Type vt = asm.GetType("Rocola.D3D9+Vtx", true); object v = Activator.CreateInstance(vt);
        vt.GetField("X").SetValue(v, 110f); vt.GetField("Y").SetValue(v, 100f); vt.GetField("Color").SetValue(v, 0x80FF2040u);
        object[] a = { v, 100f, 100f, 1.35f, .70f }; Run(panel, "EnvejeceVertice", a);
        uint c = (uint)vt.GetField("Color").GetValue(a[0]);
        Check(Math.Abs((float)vt.GetField("X").GetValue(a[0]) - 113.5f) < .01f, "la copia vieja se abre");
        Check(A(c) == 0x80 && R(c) - Bl(c) < 0xFF - 0x40, "la copia vieja pierde color sin cambiar su alfa");
        Check((float)Run(panel, "EdadEco", -1.0, 1.2) == 0f && (float)Run(panel, "EdadEco", 9.0, 1.2) == 1f && (float)Run(panel, "EdadEco", double.NaN, 1.2) == 0f, "edad de eco acotada");
    }

    static void Luna()
    {
        double nueva = (double)Run(panel, "FaseLunar", new DateTime(2026, 10, 10, 15, 50, 0, DateTimeKind.Utc));
        double llena = (double)Run(panel, "FaseLunar", new DateTime(2026, 10, 26, 4, 12, 0, DateTimeKind.Utc));
        // Fase media (sin las perturbaciones de la orbita): puede adelantarse o atrasarse hasta un dia.
        Check(nueva < .04 || nueva > .96, "luna nueva del 10-10-2026, con un dia de margen (" + nueva.ToString("0.000") + ")");
        Check(Math.Abs(llena - .5) < .04, "luna llena del 26-10-2026, con un dia de margen (" + llena.ToString("0.000") + ")");
        Check(Math.Abs((float)Run(panel, "IluminadaLunar", .5) - 1f) < .001f && (float)Run(panel, "IluminadaLunar", 0.0) < .001f, "lo iluminado va de nada a todo");
        using (Bitmap llenaB = (Bitmap)Run(panel, "BakeLunaReal", 128, .5))
        using (Bitmap otra = (Bitmap)Run(panel, "BakeLunaReal", 128, .5))
        using (Bitmap creciente = (Bitmap)Run(panel, "BakeLunaReal", 128, .25))
        using (Bitmap menguante = (Bitmap)Run(panel, "BakeLunaReal", 128, .75))
        {
            Check(llenaB.GetPixel(64, 64).A == 255 && llenaB.GetPixel(64, 64).R > 120 && llenaB.GetPixel(0, 0).A == 0, "llena: disco opaco y claro, cantos transparentes");
            bool igual = true; for (int y = 0; y < 128; y += 3) for (int x = 0; x < 128; x += 3) if (llenaB.GetPixel(x, y) != otra.GetPixel(x, y)) igual = false;
            Check(igual, "luna determinista");
            Check(creciente.GetPixel(100, 64).R > creciente.GetPixel(28, 64).R + 60, "creciente: iluminada por la derecha");
            Check(menguante.GetPixel(28, 64).R > menguante.GetPixel(100, 64).R + 60, "menguante: iluminada por la izquierda");
            Check(creciente.GetPixel(28, 64).A == 255 && creciente.GetPixel(28, 64).R < 40, "la cara en sombra tapa y apenas se ve");
        }
        bool rechaza = false; try { Run(panel, "BakeLunaReal", 4, .5); } catch (ArgumentOutOfRangeException) { rechaza = true; }
        Check(rechaza, "luna de tamano invalido rechazada");
        // El mapa de siempre sigue saliendo igual (se reaprovecha, no se cambia).
        using (Bitmap m1 = (Bitmap)Run(panel, "BakeDetailedMoon")) using (Bitmap m2 = (Bitmap)Run(panel, "BakeDetailedMoon"))
            Check(m1.Width == 256 && m1.GetPixel(128, 128) == m2.GetPixel(128, 128), "mapa lunar de siempre intacto y determinista");
    }

    static bool Llama(Type t, string metodo, string que)
    {
        foreach (MethodInfo m in t.GetMethods(B)) if (m.Name == metodo && Codigo.Llama(m, que)) return true;
        return false;
    }
    static void Enchufes()
    {
        Check(Llama(stars, "Begin", "GpuStarfield.DibujaCieloNatural"), "boveda Natural enchufada al cuadro");
        // La sonda del anillo de vertices (D3D9Device.Draw) toma el primer dibujo de 64 triangulos o mas: la boveda,
        // que va antes que las estrellas, tiene que quedarse por debajo para no ser ella la que se mida.
        int filas = (int)stars.GetField("CieloFilasNatural", B).GetRawConstantValue(), cols = (int)stars.GetField("CieloColsNatural", B).GetRawConstantValue();
        Check(filas >= 8 && filas * cols * 2 < 64, "la boveda Natural (" + filas * cols * 2 + " triangulos) no se cuela en la sonda del anillo");
        Check(Llama(stars, "BuildStarVerts", "GpuStarfield.ColorEstrellaNatural") && Llama(stars, "BuildStarVerts", "GpuStarfield.CentelleoNatural"), "color y centelleo Natural en el campo");
        Check(Llama(stars, "BuildStarVerts", "GpuStarfield.LuzJuntoALuna"), "la luna apaga las estrellas cercanas");
        Check(Llama(stars, "UpdateAndDrawShoots", "GpuStarfield.ApuntaDesdeRadiante") && Llama(stars, "UpdateAndDrawShoots", "GpuStarfield.DibujaRastro"), "lluvias y rastro enchufados");
        Check(Llama(panel, "RenderFrame", "GpuPanel.EnsureLunaReal") && Llama(panel, "RenderFrame", "GpuPanel.ActualizaHoraCielo"), "luna y hora preparadas antes del cuadro");
        // Al apagar la tarjeta o recrear el dispositivo se sueltan TODAS las texturas: la de la luna tambien (si
        // no, se seguiria dibujando con la de un dispositivo que ya no existe, y lo retendria).
        Check(Llama(panel, "StopGpu", "GpuPanel.SueltaLunaReal") && Llama(panel, "SueltaLunaReal", "D3D9.Release"), "la luna se suelta con las demas texturas");
        Check(Llama(panel, "DibujaNieblas", "GpuPanel.DibujaNieblasNatural"), "nebulosas Natural con dos distancias");
        Check(Llama(panel, "EnsureNieblasPreparadas", "GpuPanel.BakeRuidoNatural"), "grano Natural propio");
        Check(Llama(panel, "DrawAurora", "GpuPanel.AuroraRealBase") && Llama(panel, "DrawAurora", "GpuPanel.PulsoReposoAurora"), "aurora real y pulso en reposo");
        Check(Llama(panel, "AurMallaEstilo", "GpuPanel.PreparaPliegues") && Llama(panel, "AurMallaEstilo", "GpuPanel.SueloReflejo"), "pliegues y reflejo en la malla");
        Check(Llama(panel, "DrawStar3d", "GpuStarfield.DrawDestello") && Llama(panel, "DrawDetailedMoon", "GpuPanel.BancoNatural"), "destello en cruz e inclinacion");
        Check(Llama(panel, "PreparaEcos3d", "GpuPanel.EnvejeceVertice") && Llama(panel, "DrawLunarDust", "GpuPanel.PolvoAlLado"), "ecos que envejecen y polvo con luz");
        Check(!Llama(stars, "BuildStarVerts", "GpuStarfield.BakeNaturalStarTexture") && !Llama(panel, "DibujaLunaReal", "GpuPanel.BakeLunaReal"), "nada se hornea al dibujar");
        foreach (string f in new string[] { "AuroraReal", "LunaReal", "CieloHora" })
        {
            FieldInfo fi = panel.GetField(f, B); Check(fi != null, "interruptor " + f + " en el panel");
        }
        foreach (string f in new string[] { "auroraReal", "lunaFase", "cieloHora" })
            Check(form.GetField(f, B) != null && form.GetField(f, B).FieldType == typeof(bool), "preferencia " + f + " guardable");
    }
}
