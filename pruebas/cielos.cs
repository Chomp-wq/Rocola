using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// NUEVE CIELOS PARA ELEGIR MIRANDO.
//
// No es una bateria ni forma parte de la rocola: es un cuaderno de bocetos. Dibuja el cielo de hoy y, encima
// de el, cada idea POR SEPARADO, para poder ver que aporta cada una sin que se mezclen. La ultima junta las
// que valen la pena.
//
// Todo lo que se dibuja aqui iria en el HORNEADO del cielo de la rocola, que se hace una vez y luego solo se
// copia: por eso ninguna de estas ideas cuesta un milisegundo mas por cuadro.
//
//    mono cielos.exe            deja los PNG en pruebas/cielos/
class Cielos
{
    const int W = 1000, H = 620;
    static readonly Color CieloAlto = Color.FromArgb(255, 6, 8, 26);
    static readonly Color CieloMedio = Color.FromArgb(255, 13, 20, 52);
    static readonly Color CieloBajo = Color.FromArgb(255, 26, 44, 90);
    static readonly Color Genero = Color.FromArgb(255, 130, 40);   // el naranja de "Tropical", para las nebulosas

    // ---------------------------------------------------------------- el lienzo
    static Bitmap NuevoCielo()
    {
        Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(0, 0, W, H + 1), CieloAlto, CieloBajo, LinearGradientMode.Vertical))
        {
            ColorBlend cb = new ColorBlend(3);
            cb.Colors = new Color[] { CieloAlto, CieloMedio, CieloBajo };
            cb.Positions = new float[] { 0f, 0.42f, 1f };
            lgb.InterpolationColors = cb;
            g.FillRectangle(lgb, 0, 0, W, H);
        }
        return b;
    }

    // Una mancha de luz redonda que se desvanece: la base de casi todo lo de aqui.
    static void Mancha(Graphics g, float cx, float cy, float r, Color c, int alfaCentro)
    {
        if (r < 1f) r = 1f;
        using (GraphicsPath gp = new GraphicsPath())
        {
            gp.AddEllipse(cx - r, cy - r, r * 2, r * 2);
            using (PathGradientBrush pgb = new PathGradientBrush(gp))
            {
                pgb.CenterPoint = new PointF(cx, cy);
                pgb.CenterColor = Color.FromArgb(alfaCentro, c);
                pgb.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                g.FillEllipse(pgb, cx - r, cy - r, r * 2, r * 2);
            }
        }
    }

    // ---------------------------------------------------------------- las estrellas
    class Estrella { public float X, Y, Tam, Brillo; public Color Col; public int Capa; public bool Rayos; }

    // LA DE HOY: cinco puntas con la punta arriba.
    static readonly float[] Ux = new float[10], Uy = new float[10];
    static Cielos()
    {
        for (int k = 0; k < 10; k++)
        {
            double ang = -Math.PI / 2.0 + k * Math.PI / 5.0;
            float rad = ((k & 1) == 0) ? 1f : 0.45f;
            Ux[k] = (float)Math.Cos(ang) * rad; Uy[k] = (float)Math.Sin(ang) * rad;
        }
    }
    static void CincoPuntas(Graphics g, Estrella e)
    {
        float R = e.Tam;
        Mancha(g, e.X, e.Y, R * 2.6f, e.Col, (int)(70 * e.Brillo));
        PointF[] p = new PointF[10];
        for (int k = 0; k < 10; k++) p[k] = new PointF(e.X + R * Ux[k], e.Y + R * Uy[k]);
        using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * e.Brillo), e.Col))) g.FillPolygon(b, p);
    }

    // EL PUNTO DE LUZ: halo suave, nucleo diminuto casi blanco y -solo en las mas brillantes- cuatro rayitas
    // cruzadas. Las rayitas no las hace la estrella: las hace el diafragma de la camara, y son justo lo que
    // hace que una foto del cielo parezca una foto.
    static void PuntoDeLuz(Graphics g, Estrella e)
    {
        float R = e.Tam;
        // El halo: chico y suave. Aqui estaba el primer exceso -uno de 4 radios al 120 de alfa- y con el
        // todas las estrellas, hasta las de un pixel, salian con una bola de luz alrededor.
        Mancha(g, e.X, e.Y, R * (e.Rayos ? 5.2f : 2.9f), e.Col, (int)((e.Rayos ? 110 : 62) * e.Brillo));
        if (e.Rayos)
        {
            // Las cuatro rayitas cruzadas. Las lleva un punado de estrellas, las mas brillantes y nada mas:
            // en una foto del cielo tampoco las tienen mas de diez o doce. Con el umbral de antes las
            // llevaban ciento y pico y el cielo parecia purpurina.
            float largo = R * 4.2f, ancho = R * 0.22f;
            for (int paso = 0; paso < 3; paso++)
            {
                float Lg = largo * (1f - paso * 0.30f), A = ancho * (1f + paso * 1.1f);
                int alfa = Math.Min(120, 34 + paso * 26);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(alfa, e.Col)))
                {
                    g.FillPolygon(b, new PointF[] { new PointF(e.X - Lg, e.Y), new PointF(e.X, e.Y - A), new PointF(e.X + Lg, e.Y), new PointF(e.X, e.Y + A) });
                    g.FillPolygon(b, new PointF[] { new PointF(e.X, e.Y - Lg), new PointF(e.X + A, e.Y), new PointF(e.X, e.Y + Lg), new PointF(e.X - A, e.Y) });
                }
            }
        }
        // El nucleo: chiquito y tirando a blanco, que es lo que hace que "queme".
        Color nuc = Color.FromArgb((int)(255 * e.Brillo),
            Math.Min(255, e.Col.R + 70), Math.Min(255, e.Col.G + 70), Math.Min(255, e.Col.B + 70));
        using (SolidBrush b = new SolidBrush(nuc)) g.FillEllipse(b, e.X - R * 0.55f, e.Y - R * 0.55f, R * 1.1f, R * 1.1f);
    }

    // A QUIEN LE TOCAN LAS RAYITAS: a las N mas grandes de la lista, y a nadie mas.
    static void LasMasBrillantes(List<Estrella> l, int cuantas)
    {
        List<Estrella> orden = new List<Estrella>(l);
        orden.Sort(delegate (Estrella a, Estrella b) { return b.Tam.CompareTo(a.Tam); });
        for (int i = 0; i < orden.Count && i < cuantas; i++) orden[i].Rayos = true;
    }

    // ---------------------------------------------------------------- de que color es una estrella
    // Las de verdad van de blanco-azulado a blanco, amarillo, naranja y rojo: no hay estrellas verdes ni
    // violetas. Y la mayoria de las que se ven son casi blancas; el color fuerte es la excepcion.
    static readonly Color[] Temperatura = {
        Color.FromArgb(155, 176, 255), Color.FromArgb(170, 191, 255),   // azul-blanco (las mas calientes)
        Color.FromArgb(202, 215, 255), Color.FromArgb(228, 235, 255),   // blanco-azul
        Color.FromArgb(248, 247, 255), Color.FromArgb(255, 255, 250),   // blancas
        Color.FromArgb(255, 244, 234), Color.FromArgb(255, 237, 205),   // blanco-amarillo (como el Sol)
        Color.FromArgb(255, 218, 165), Color.FromArgb(255, 199, 142),   // naranjas
        Color.FromArgb(255, 178, 128),                                   // naranja hondo
    };
    static Color PorTemperatura(Random r)
    {
        // Curva a proposito: casi todas blancas, unas cuantas azuladas y doradas, muy pocas naranjas.
        int q = r.Next(100);
        if (q < 14) return Temperatura[r.Next(2)];
        if (q < 34) return Temperatura[2 + r.Next(2)];
        if (q < 68) return Temperatura[4 + r.Next(2)];
        if (q < 88) return Temperatura[6 + r.Next(2)];
        if (q < 97) return Temperatura[8 + r.Next(2)];
        return Temperatura[10];
    }

    // La paleta de HOY: colores de rotulador, sin nada que ver con una estrella de verdad.
    static readonly Color[] PalHoy = {
        Color.FromArgb(255, 200, 20), Color.FromArgb(255, 160, 50), Color.FromArgb(100, 160, 240),
        Color.FromArgb(70, 120, 210), Color.FromArgb(255, 100, 160), Color.FromArgb(255, 160, 200),
        Color.FromArgb(150, 120, 240), Color.FromArgb(200, 180, 255), Color.FromArgb(120, 240, 210), Color.White
    };

    // ---------------------------------------------------------------- donde se ponen
    // COMO HOY: con separacion minima obligatoria. Es justo lo que hace que el cielo se vea espolvoreado
    // con regularidad, como un papel pintado, en vez de como un cielo.
    static List<Estrella> Repartidas(Random r, int n, bool separadas)
    {
        List<Estrella> l = new List<Estrella>();
        for (int i = 0; i < n; i++)
        {
            float x = 0, y = 0;
            for (int intento = 0; intento < 12; intento++)
            {
                x = 22 + (float)r.NextDouble() * (W - 44);
                y = 22 + (float)r.NextDouble() * (H - 44);
                if (!separadas) break;
                bool libre = true;
                for (int j = 0; j < l.Count; j++)
                {
                    float dx = x - l[j].X, dy = y - l[j].Y;
                    if (dx * dx + dy * dy < 20 * 20) { libre = false; break; }
                }
                if (libre) break;
            }
            l.Add(new Estrella { X = x, Y = y });
        }
        return l;
    }

    // AGRUPADAS: unos cuantos centros y la mayoria de las estrellas cayendo cerca de uno, mas algunas
    // sueltas. Asi salen grumos, parejas y zonas vacias, que es como es un cielo de verdad.
    static float Gauss(Random r)
    {
        double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }
    static List<Estrella> Agrupadas(Random r, int n)
    {
        int nc = 9;
        float[] cx = new float[nc], cy = new float[nc], cr = new float[nc];
        for (int i = 0; i < nc; i++)
        {
            cx[i] = (float)r.NextDouble() * W; cy[i] = (float)r.NextDouble() * H;
            cr[i] = 60f + (float)r.NextDouble() * 130f;
        }
        List<Estrella> l = new List<Estrella>();
        for (int i = 0; i < n; i++)
        {
            float x, y;
            if (r.Next(100) < 68)   // la mayoria, en grumo
            {
                int c = r.Next(nc);
                x = cx[c] + Gauss(r) * cr[c]; y = cy[c] + Gauss(r) * cr[c] * 0.7f;
            }
            else { x = (float)r.NextDouble() * W; y = (float)r.NextDouble() * H; }
            if (x < 6) x = 6; if (x > W - 6) x = W - 6;
            if (y < 6) y = 6; if (y > H - 6) y = H - 6;
            l.Add(new Estrella { X = x, Y = y });
        }
        return l;
    }

    // Tamanos y brillos "de hoy": tres tallas bien marcadas.
    static void TallasDeHoy(Random r, List<Estrella> l, Color[] pal)
    {
        foreach (Estrella e in l)
        {
            int k = r.Next(100);
            e.Tam = (k < 12) ? (7 + r.Next(5)) : (k < 34) ? (4 + r.Next(3)) : (2 + r.Next(2));
            e.Brillo = (k < 12) ? 1f : (k < 34) ? 0.85f : 0.6f;
            e.Col = (k < 34) ? pal[r.Next(pal.Length)] : Color.FromArgb(225, 232, 250);
        }
    }

    // Tamanos y brillos DE CIELO: un punado de brillantes y muchisimas que casi no se ven. Es lo que da
    // sensacion de fondo, de que hay mas cielo detras del cielo.
    static void TallasDeCielo(Random r, List<Estrella> l)
    {
        foreach (Estrella e in l)
        {
            double q = r.NextDouble();
            double m = Math.Pow(q, 4.6);            // curva bien inclinada: casi todas debiles, unas pocas fuertes
            e.Tam = (float)(0.75 + m * 5.0);
            e.Brillo = (float)(0.16 + m * 0.84);
            e.Col = PorTemperatura(r);
            e.Capa = (m > 0.55) ? 2 : (m > 0.18) ? 1 : 0;
        }
    }

    // ---------------------------------------------------------------- las ideas grandes
    // LA VIA LACTEA: una banda de neblina en diagonal, hecha de muchisimas manchas muy tenues, con vetas
    // oscuras cruzandola (que en el cielo de verdad son nubes de polvo tapando las estrellas de detras).
    static void ViaLactea(Graphics g, Random r, out float bx0, out float by0, out float bx1, out float by1)
    {
        bx0 = -80; by0 = H * 0.86f; bx1 = W + 80; by1 = H * 0.06f;
        for (int i = 0; i < 260; i++)
        {
            float t = (float)r.NextDouble();
            float x = bx0 + (bx1 - bx0) * t, y = by0 + (by1 - by0) * t;
            float ancho = 78f + 42f * (float)Math.Sin(t * 3.1);
            x += Gauss(r) * ancho * 0.55f; y += Gauss(r) * ancho;
            Mancha(g, x, y, 40f + (float)r.NextDouble() * 90f, Color.FromArgb(214, 226, 255), 12);
        }
        for (int i = 0; i < 26; i++)   // las vetas de polvo, del color del cielo, tapando
        {
            float t = (float)r.NextDouble();
            float x = bx0 + (bx1 - bx0) * t + Gauss(r) * 46f, y = by0 + (by1 - by0) * t + Gauss(r) * 52f;
            Mancha(g, x, y, 34f + (float)r.NextDouble() * 74f, Color.FromArgb(10, 14, 38), 66);
        }
    }

    // NEBULOSAS del color de lo que suena: nubes muy suaves que tinen el cielo con el color del genero.
    static void Nebulosas(Graphics g, Random r, Color acento)
    {
        Color frio = Color.FromArgb(90, 150, 255);
        for (int i = 0; i < 3; i++)
        {
            float x = 120 + (float)r.NextDouble() * (W - 240), y = 90 + (float)r.NextDouble() * (H - 220);
            Color c = (i == 1) ? frio : acento;
            for (int k = 0; k < 26; k++)
                Mancha(g, x + Gauss(r) * 110f, y + Gauss(r) * 72f, 70f + (float)r.NextDouble() * 130f, c, 9);
            Mancha(g, x, y, 60f, c, 16);   // el corazon, un pelin mas encendido
        }
    }

    // ---------------------------------------------------------------- pintar una lista
    static void Pinta(Graphics g, List<Estrella> l, bool comoPunto, int capaSolo)
    {
        foreach (Estrella e in l)
        {
            if (capaSolo >= 0 && e.Capa != capaSolo) continue;
            if (comoPunto) PuntoDeLuz(g, e);
            else CincoPuntas(g, e);
        }
    }

    static void Guarda(Bitmap b, string nombre, string titulo)
    {
        using (Graphics g = Graphics.FromImage(b))
        using (Font f = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Bold))
        using (SolidBrush s = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
            g.DrawString(titulo, f, s, 14, H - 30);
        b.Save("cielos/" + nombre + ".png", ImageFormat.Png);
        b.Dispose();
        Console.WriteLine("     cielos/" + nombre + ".png   " + titulo);
    }

    static int Main()
    {
        System.IO.Directory.CreateDirectory("cielos");
        Console.WriteLine("\n  NUEVE CIELOS\n");

        // 1) EL DE HOY
        {
            Random r = new Random(7);
            Bitmap b = NuevoCielo();
            List<Estrella> l = Repartidas(r, 157, true);
            TallasDeHoy(r, l, PalHoy);
            using (Graphics g = Graphics.FromImage(b)) { g.SmoothingMode = SmoothingMode.AntiAlias; Pinta(g, l, false, -1); }
            Guarda(b, "1_hoy", "1. EL DE HOY  -  cinco puntas, colores de rotulador, separacion obligatoria");
        }

        // 2) SOLO DEJARLAS AGRUPARSE
        {
            Random r = new Random(7);
            Bitmap b = NuevoCielo();
            List<Estrella> l = Agrupadas(r, 157);
            TallasDeHoy(r, l, PalHoy);
            using (Graphics g = Graphics.FromImage(b)) { g.SmoothingMode = SmoothingMode.AntiAlias; Pinta(g, l, false, -1); }
            Guarda(b, "2_agrupadas", "2. QUE SE AGRUPEN  -  lo mismo de hoy, quitando la separacion obligatoria");
        }

        // 3) PUNTO DE LUZ CON RAYITAS
        {
            Random r = new Random(7);
            Bitmap b = NuevoCielo();
            List<Estrella> l = Repartidas(r, 157, true);
            TallasDeHoy(r, l, PalHoy);
            LasMasBrillantes(l, 10);
            using (Graphics g = Graphics.FromImage(b)) { g.SmoothingMode = SmoothingMode.AntiAlias; Pinta(g, l, true, -1); }
            Guarda(b, "3_puntos", "3. PUNTO DE LUZ  -  nucleo y halo; rayitas solo en las brillantes");
        }

        // 4) COLOR POR TEMPERATURA
        {
            Random r = new Random(7);
            Bitmap b = NuevoCielo();
            List<Estrella> l = Repartidas(r, 157, true);
            TallasDeHoy(r, l, PalHoy);
            foreach (Estrella e in l) e.Col = PorTemperatura(r);
            LasMasBrillantes(l, 10);
            using (Graphics g = Graphics.FromImage(b)) { g.SmoothingMode = SmoothingMode.AntiAlias; Pinta(g, l, true, -1); }
            Guarda(b, "4_temperatura", "4. COLOR POR TEMPERATURA  -  de azul-blanco a naranja; casi todas blancas");
        }

        // 5) MUCHAS MAS Y MAS TENUES
        {
            Random r = new Random(7);
            Bitmap b = NuevoCielo();
            List<Estrella> l = Repartidas(r, 620, false);
            TallasDeCielo(r, l);
            LasMasBrillantes(l, 10);
            using (Graphics g = Graphics.FromImage(b)) { g.SmoothingMode = SmoothingMode.AntiAlias; Pinta(g, l, true, -1); }
            Guarda(b, "5_muchas", "5. MUCHAS MAS Y MAS TENUES  -  620 en vez de 157, casi todas apenas visibles");
        }

        // 6) LA VIA LACTEA
        {
            Random r = new Random(7);
            Bitmap b = NuevoCielo();
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float x0, y0, x1, y1;
                ViaLactea(g, r, out x0, out y0, out x1, out y1);
                // y MAS estrellas dentro de la banda, que es de donde viene la neblina
                List<Estrella> l = Repartidas(r, 380, false);
                TallasDeCielo(r, l);
                LasMasBrillantes(l, 10);
            LasMasBrillantes(l, 10);
                List<Estrella> banda = new List<Estrella>();
                for (int i = 0; i < 300; i++)
                {
                    float t = (float)r.NextDouble();
                    Estrella e = new Estrella { X = x0 + (x1 - x0) * t + Gauss(r) * 60f, Y = y0 + (y1 - y0) * t + Gauss(r) * 78f };
                    banda.Add(e);
                }
                TallasDeCielo(r, banda);
                LasMasBrillantes(banda, 4);
                foreach (Estrella e in banda) { e.Tam *= 0.72f; e.Brillo *= 0.8f; }
                Pinta(g, l, true, -1); Pinta(g, banda, true, -1);
            }
            Guarda(b, "6_vialactea", "6. LA VIA LACTEA  -  banda de neblina, vetas de polvo y mas estrellas dentro");
        }

        // 7) NEBULOSAS DEL COLOR DEL GENERO
        {
            Random r = new Random(7);
            Bitmap b = NuevoCielo();
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Nebulosas(g, r, Genero);
                List<Estrella> l = Repartidas(r, 380, false);
                TallasDeCielo(r, l);
                LasMasBrillantes(l, 10);
            LasMasBrillantes(l, 10);
                Pinta(g, l, true, -1);
            }
            Guarda(b, "7_nebulosas", "7. NEBULOSAS DEL COLOR DEL GENERO  -  aqui el naranja de \"Tropical\"");
        }

        // 8) PROFUNDIDAD POR CAPAS
        {
            Random r = new Random(7);
            Bitmap b = NuevoCielo();
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                List<Estrella> l = Agrupadas(r, 620);
                TallasDeCielo(r, l);
                LasMasBrillantes(l, 10);
            LasMasBrillantes(l, 10);
                foreach (Estrella e in l)
                {
                    if (e.Capa == 0) { e.Tam *= 0.62f; e.Brillo *= 0.55f; }        // lejanas: diminutas y apagadas
                    else if (e.Capa == 2) { e.Tam *= 1.25f; e.Brillo = 1f; }        // cercanas: mas grandes y encendidas
                }
                Pinta(g, l, true, 0); Pinta(g, l, true, 1); Pinta(g, l, true, 2);
            }
            Guarda(b, "8_capas", "8. PROFUNDIDAD POR CAPAS  -  tres planos: lejanas apagadas, cercanas encendidas");
        }

        // 9) TODO JUNTO
        {
            Random r = new Random(7);
            Bitmap b = NuevoCielo();
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Nebulosas(g, r, Genero);
                float x0, y0, x1, y1;
                ViaLactea(g, r, out x0, out y0, out x1, out y1);
                List<Estrella> l = Agrupadas(r, 560);
                TallasDeCielo(r, l);
                LasMasBrillantes(l, 10);
            LasMasBrillantes(l, 10);
                List<Estrella> banda = new List<Estrella>();
                for (int i = 0; i < 260; i++)
                {
                    float t = (float)r.NextDouble();
                    banda.Add(new Estrella { X = x0 + (x1 - x0) * t + Gauss(r) * 58f, Y = y0 + (y1 - y0) * t + Gauss(r) * 74f });
                }
                TallasDeCielo(r, banda);
                LasMasBrillantes(banda, 4);
                foreach (Estrella e in banda) { e.Tam *= 0.7f; e.Brillo *= 0.78f; }
                foreach (Estrella e in l)
                {
                    if (e.Capa == 0) { e.Tam *= 0.62f; e.Brillo *= 0.55f; }
                    else if (e.Capa == 2) { e.Tam *= 1.2f; e.Brillo = 1f; }
                }
                Pinta(g, banda, true, -1);
                Pinta(g, l, true, 0); Pinta(g, l, true, 1); Pinta(g, l, true, 2);
                // una PROTAGONISTA, con halo grande y rayos largos, como Sirio en una foto de exposicion larga
                Estrella sirio = new Estrella { X = W * 0.74f, Y = H * 0.30f, Tam = 6.4f, Brillo = 1f, Rayos = true, Col = Color.FromArgb(214, 228, 255) };
                Mancha(g, sirio.X, sirio.Y, 62f, sirio.Col, 42);
                PuntoDeLuz(g, sirio);
            }
            Guarda(b, "9_todo", "9. TODO JUNTO  -  agrupadas + punto de luz + temperatura + muchas + via lactea + nebulosas + capas");
        }

        Console.WriteLine();
        return 0;
    }
}
