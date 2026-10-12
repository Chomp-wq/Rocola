using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

// SEXTA TANDA (12-10-2026): LOS TEXTOS DEL MODO ATRACCION. Aviso del usuario: "ayer salian cortados unos textos".
//  1) Los mensajes que se turnan ("Toca un boton y elige entre N canciones", "Busca cualquier cancion con el boton
//     rojo y la flecha"...) que no cabian se achicaban a la medida JUSTA; a ese tamano las letras se ajustan a la
//     rejilla de pixeles, el renglon se pasaba un poco y se cortaba el final. En 1024x768 les pasaba a los cuatro.
//  2) "Lo mas pedido esta noche: «titulo largo»" se cortaba por cualquier lado: ahora se acorta la cita, con los
//     puntos dentro de las comillas.
//  3) El nombre largo de un disco se cortaba a los dos renglones: antes se prueba con la letra algo mas chica (y un
//     tercer renglon si hay sitio).
//  4) En la lista de la noche el titulo tenia el 40 % del ancho y la barra (adorno) el resto: ahora la mitad.
// Se hornea lo mismo que la tarjeta en nueve resoluciones y se mira que ningun texto toque la orilla de su caja.
// Sin tarjeta grafica no hay atraccion nueva (el dibujo por CPU no cambia).
class SextaTanda
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static Type F, TFi, TFn;
    static int Main(string[] args)
    {
        Assembly a = Assembly.LoadFrom(args[0]);
        F = a.GetType("Rocola.Form1", true); TFi = a.GetType("Rocola.FichaVitrina", false); TFn = a.GetType("Rocola.FilaNoche", false);
        if (F.GetMethod("HorneaMensajes", B) == null)
        {
            Check(TFi == null, "sin tarjeta grafica no hay escaparate ni lista de la noche (el dibujo por CPU no cambia)");
        }
        else Atraccion();
        Console.WriteLine(mal == 0 ? "  OK sextatanda: " + ok + " comprobaciones (texturas de la atraccion horneadas aparte; sin tarjeta)"
                                   : "  FALLO sextatanda: " + mal + " de " + (ok + mal));
        return mal;
    }

    // Lo pintado (alfa > 40) en las ultimas 'n' columnas de una franja: si el texto llega ahi, se corto.
    static bool TocaOrilla(Bitmap b, int y0, int y1, int n)
    {
        for (int y = Math.Max(0, y0); y < Math.Min(b.Height, y1); y++)
            for (int x = b.Width - n; x < b.Width; x++)
                if (b.GetPixel(x, y).A > 40) return true;
        return false;
    }

    static Bitmap Recorta(Bitmap b, int ancho, int y0, int y1)
    { return b.Clone(new Rectangle(0, y0, Math.Min(ancho, b.Width), Math.Min(y1, b.Height) - y0), b.PixelFormat); }

    static void Atraccion()
    {
        int[][] res = { new[] { 640, 480 }, new[] { 800, 600 }, new[] { 1024, 768 }, new[] { 1280, 720 }, new[] { 1280, 1024 },
                        new[] { 1366, 768 }, new[] { 1440, 900 }, new[] { 1600, 900 }, new[] { 1920, 1080 } };
        List<string> msgs = new List<string> { "Toca un botón y elige entre 12,345 canciones", "Pon tu canción: una moneda = 4 canciones",
            "Lo más pedido esta noche: «Amor Eterno (En Vivo Desde el Palacio de Bellas Artes)»", "Busca cualquier canción con el botón rojo y la flecha" };
        MethodInfo hm = F.GetMethod("HorneaMensajes", B), col = F.GetMethod("ColumnaEscaparate", B), altoMsg = F.GetMethod("AltoMensaje", B);
        MethodInfo ficha = F.GetMethod("HorneaFichaVitrina", B), geo = F.GetMethod("GeometriaTop", B), top = F.GetMethod("HorneaTop", B);
        MethodInfo acorta = F.GetMethod("AcortaCita", B), palabras = F.GetMethod("AcortaPorPalabras", B);
        MethodInfo cabe = F.GetMethod("EscalaQueCabe", B, null, new Type[] { typeof(Graphics), typeof(string), typeof(Font), typeof(float), typeof(StringFormat) }, null);
        Check(acorta != null && cabe != null, "estan las cuentas que hacen caber los renglones");
        if (acorta == null || cabe == null) return;
        foreach (int[] r in res)
        {
            int w = r[0], h = r[1]; string c = " (" + w + "x" + h + ")";
            RectangleF cl = (RectangleF)col.Invoke(null, new object[] { w, h });
            int mh = (int)altoMsg.Invoke(null, new object[] { h });
            // 1) y 2) Los mensajes: ninguno llega a la orilla derecha de su renglon.
            object[] p = { msgs, (int)cl.Width, h, null };
            using (Bitmap mb = (Bitmap)hm.Invoke(null, p))
            {
                float[] anchos = (float[])p[3];
                for (int i = 0; i < msgs.Count; i++)
                {
                    Check(!TocaOrilla(mb, i * mh, (i + 1) * mh, 2), "el mensaje " + (i + 1) + " cabe entero" + c);
                    Check(anchos[i] > 0f && anchos[i] <= mb.Width, "y su ancho (para el brillo de detras) esta dentro" + c);
                }
            }
            // 3) La ficha de un disco de nombre y artista largos.
            object fi = Activator.CreateInstance(TFi, true);
            TFi.GetField("Nombre").SetValue(fi, "Los Tigres del Norte - Jefe de Jefes (Edición Especial)");
            TFi.GetField("Artista").SetValue(fi, "Los Tigres del Norte con la Banda Sinaloense");
            TFi.GetField("Genero").SetValue(fi, "Norteño"); TFi.GetField("GeneroColor").SetValue(fi, Color.Peru);
            TFi.GetField("Canciones").SetValue(fi, 24); TFi.GetField("Pedidas").SetValue(fi, 137);
            // (el artista puede caber JUSTO hasta la orilla: aqui se mira que la ficha quepa encima del mensaje)
            using (Bitmap fb = (Bitmap)ficha.Invoke(null, new object[] { fi, (int)cl.Width, h }))
                Check(fb.Height <= cl.Height - mh + 2, "la ficha cabe encima del mensaje (" + fb.Height + " de " + (cl.Height - mh) + ")" + c);
            // 4) La lista de la noche: la barra no se mete debajo de "N veces" y los titulos tienen la mitad del ancho.
            IList filas = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TFn));
            string[] tit = { "Amor Eterno (En Vivo Desde Bellas Artes)", "El Rey", "La Chona", "Como la Flor", "Hasta la Raíz" };
            int[] veces = { 128, 37, 9, 2, 1 };
            for (int i = 0; i < 5; i++)
            {
                object fn = Activator.CreateInstance(TFn, true);
                TFn.GetField("Titulo").SetValue(fn, tit[i]); TFn.GetField("Artista").SetValue(fn, "Juan Gabriel");
                TFn.GetField("Veces").SetValue(fn, veces[i]); TFn.GetField("Color").SetValue(fn, Color.SteelBlue); filas.Add(fn);
            }
            object[] g6 = { w, h, null, null, null, null, null };
            geo.Invoke(null, g6);
            RectangleF pag = (RectangleF)g6[2]; float filaH = (float)g6[6];
            object[] ht = { filas, new Image[5], 214, (int)pag.Width, (int)pag.Height, g6[3], g6[4], g6[5], filaH, null, null, null, null };
            using (Bitmap tb = (Bitmap)top.Invoke(null, ht))
            {
                float bx = (float)ht[9], bw = (float)ht[10];
                float vecesX = pag.Width - filaH * 1.1f;
                Check(bx + bw <= vecesX + 1f, "la barra acaba antes de \"N veces\"" + c);
                Check(bx > pag.Width * .5f, "el titulo tiene la mitad del ancho" + c);
                // "N veces" va pegado a la derecha a proposito: lo que no puede es cortarse por la izquierda de su caja.
                float filasY = (float)g6[5];
                bool cortado = false;
                for (int i = 0; i < 5; i++)
                    for (int y = (int)(filasY + i * filaH); y < (int)(filasY + (i + 1) * filaH) && y < tb.Height; y++)
                        for (int x = (int)vecesX; x < (int)vecesX + 2 && x < tb.Width; x++) if (tb.GetPixel(x, y).A > 40) cortado = true;
                Check(!cortado, "\"128 veces\" cabe en su caja" + c);
                // El titulo largo del primer puesto no llega a la orilla de su caja (que acaba antes de la barra).
                int titDer = (int)(bx - filaH * .30f);
                Check(!TocaOrilla(Recorta(tb, titDer, (int)filasY, (int)(filasY + filaH)), 0, (int)filaH, 2), "el titulo largo del primer puesto cabe" + c);
            }
        }
        // La cita: con sitio, igual; sin sitio, los puntos DENTRO de las comillas y el principio entero.
        using (Bitmap b1 = new Bitmap(1, 1)) using (Graphics g = Graphics.FromImage(b1))
        using (Font f = new Font("Trebuchet MS", 22f, FontStyle.Bold, GraphicsUnit.Pixel)) using (StringFormat sf = new StringFormat())
        {
            string t = "Lo más pedido esta noche: «Amor Eterno (En Vivo Desde el Palacio de Bellas Artes)»";
            Check((string)acorta.Invoke(null, new object[] { g, t, f, 5000f, sf }) == t, "con sitio, la cita queda igual");
            string s = (string)acorta.Invoke(null, new object[] { g, t, f, 300f, sf });
            Check(s.StartsWith("Lo más pedido esta noche: «Amor") && s.EndsWith("…»") && s.Length < t.Length, "sin sitio: «Amor Eterno…» (" + s + ")");
            Check((string)acorta.Invoke(null, new object[] { g, "Sin comillas, nada que acortar aunque sea muy largo", f, 50f, sf }) == "Sin comillas, nada que acortar aunque sea muy largo", "sin cita no se toca");
            float e = (float)cabe.Invoke(null, new object[] { g, "Toca un botón y elige entre 12,345 canciones", f, 380f, sf });
            float wc; using (Font chica = new Font(f.FontFamily, f.Size * e, f.Style, f.Unit)) wc = g.MeasureString("Toca un botón y elige entre 12,345 canciones", chica, PointF.Empty, sf).Width;
            Check(e >= .75f && e < 1f && (wc <= 380f || e <= .75f), "la letra se achica hasta caber DE VERDAD, medida con la letra chica (" + e.ToString("0.00") + ", " + wc.ToString("0") + " de 380)");
            if (palabras != null)
            {
                string q = (string)palabras.Invoke(null, new object[] { g, "Amor Eterno (En Vivo Desde Bellas Artes)", f, 220f, sf });
                Check(q.EndsWith("\u2026") && !q.EndsWith(" \u2026") && "Amor Eterno (En Vivo Desde Bellas Artes)".StartsWith(q.Substring(0, q.Length - 1))
                      && g.MeasureString(q, f, PointF.Empty, sf).Width <= 220f, "lo que no cabe se acorta por palabras con puntos (" + q + ")");
                Check((string)palabras.Invoke(null, new object[] { g, "El Rey", f, 220f, sf }) == "El Rey", "lo que cabe queda igual");
            }
            else Check(false, "falta el acortado por palabras");
        }
    }
}
