using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

// LA VENTANA DE SONIDO (Ctrl+F3): que todo QUEPA.
//
// Esta bateria ya existia y aun asi se colo un texto cortado en el equipo del bar ("Cuanto se suben las
// canciones bajitas. Las que ya suenan fuerte NUNCA se" y ahi se acababa). El fallo estaba AQUI: medía con la
// letra que hay en esta maquina, que es mas estrecha y de renglon mas bajo que la de Windows, asi que dos
// renglones le cabian en 28 pixeles cuando en el bar necesitaban 30.
//
// Ahora no se fia de la letra de esta maquina:
//   - cuenta RENGLONES (los cortes escritos a mano mas los que salgan por ancho) y les exige el alto de
//     renglon del PEOR caso razonable, no el que mida aqui;
//   - a lo ancho le suma un 8% de margen, por si la letra del bar sale un poco mas ancha.
class Ajustes
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    // Alto de renglon del PEOR caso. Segoe UI, que es la letra del bar, gasta 1.33 veces el cuerpo por
    // renglon (subida 2210 + bajada 514 sobre 2048). Se pide 1.34 -un 1% de margen- y se redondea arriba.
    // Con esta cuenta, la explicacion de dos renglones a 8.5 puntos necesita 32 px: por eso el hueco de 28
    // que habia antes cortaba la segunda linea, y por eso esta bateria no lo veia midiendo con la letra de
    // esta maquina, que es mas baja.
    static int AltoRenglon(float pt) { return (int)Math.Ceiling(pt * (96f / 72f) * 1.34f); }
    const float Margen = 1.08f;   // margen de ancho, por si la letra del bar sale mas ancha que la de aqui

    class Caja
    {
        public string Nombre, Texto; public Rectangle R; public Font F; public float Pt; public bool Envuelve; public int Extra;
        public Caja(string n, int x, int y, int w, int h, string t, Font f, float pt, bool env, int extra)
        { Nombre = n; R = new Rectangle(x, y, w, h); Texto = t; F = f; Pt = pt; Envuelve = env; Extra = extra; }
    }
    static List<Caja> cajas = new List<Caja>();
    static void C(string n, int x, int y, int w, int h, string t, Font f, float pt, bool env) { cajas.Add(new Caja(n, x, y, w, h, t, f, pt, env, 0)); }
    static void CSw(string n, int x, int y, int w, int h, string t, Font f, float pt) { cajas.Add(new Caja(n, x, y, w, h, t, f, pt, false, 50)); }   // los interruptores llevan la pastilla delante

    static Font ff(float t, FontStyle s)
    {
        string[] pref = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string p in pref) try { return new Font(new FontFamily(p), t, s); } catch { }
        return new Font(FontFamily.GenericSansSerif, t, s);
    }

    // Cuantos renglones ocupa un texto en un hueco de 'ancho' pixeles: los cortes escritos a mano, mas los que
    // salgan por no caber a lo ancho.
    static int Renglones(string t, Font f, int ancho, int extra)
    {
        int n = 0;
        string[] partes = t.Split('\n');
        for (int i = 0; i < partes.Length; i++)
        {
            int w = (int)(TextRenderer.MeasureText(partes[i], f).Width * Margen) + (i == 0 ? extra : 0);
            int l = (w <= ancho) ? 1 : (int)Math.Ceiling(w / (double)ancho);
            if (partes[i].Length == 0) l = 1;
            n += l;
        }
        return n;
    }

    // Dos cajas son variantes del mismo control si se llaman igual hasta el primer punto y ese nombre es
    // uno de los que el programa escribe de varias maneras.
    static bool Variantes(string a, string b)
    {
        int pa = a.IndexOf('.'), pb = b.IndexOf('.');
        if (pa <= 0 || pb <= 0) return false;
        string ra = a.Substring(0, pa);
        return ra == b.Substring(0, pb) && ra == "salida";
    }

    const int anchoFila = 424;
    static Font fNom, fVal, fDes, fInt, fBot, fSub, fTit;
    static void Fila(string id, int ox, int oy, int y, string titulo, string valor, string desc, bool conAuto)
    {
        int wNom = conAuto ? (anchoFila - 210) : (anchoFila - 100);
        C(id + ".nombre", ox + 16, oy + y, wNom, 18, titulo, fNom, 10f, false);
        if (conAuto) CSw(id + ".auto", ox + 16 + wNom + 6, oy + y - 2, 104, 22, "Auto", fInt, 9.5f);
        C(id + ".valor", ox + 16 + anchoFila - 94, oy + y - 2, 94, 20, valor, fVal, 11f, false);
        C(id + ".explica", ox + 16, oy + y + 21, anchoFila, 32, desc, fDes, 8.5f, true);
        C(id + ".barra", ox + 16, oy + y + 55, anchoFila, 22, "", null, 0f, false);
    }

    static int Main()
    {
        fTit = ff(15f, FontStyle.Bold); fSub = ff(9.5f, FontStyle.Regular); fNom = ff(10f, FontStyle.Bold);
        fVal = ff(11f, FontStyle.Bold); fDes = ff(8.5f, FontStyle.Regular); fBot = ff(10f, FontStyle.Bold);
        fInt = ff(9.5f, FontStyle.Bold);

        bool hd = false;
        try
        {
            Assembly asm = Assembly.LoadFrom("rocola.exe");
            Type tf = asm.GetType("Rocola.Form1", true);
            hd = tf.GetField("iaVivo", BindingFlags.Instance | BindingFlags.NonPublic) != null;
        }
        catch { }
        Console.WriteLine(hd ? "\n  (ejecutable de la rama HD: lleva ademas la tarjeta de la IA en vivo)" : "\n  (ejecutable de la rama modesta)");
        int W = 960, H = hd ? 974 : 732;

        C("titulo", 22, 10, 700, 28, "Sonido", fTit, 15f, false);
        C("subtitulo", 24, 42, 916, 18, "Todo lo que cambies aqui se oye al instante en la cancion que esta sonando.", fSub, 9.5f, false);
        // La linea que dice que hay DESPUES del cable. Son DOS renglones y el texto se arma sobre la marcha,
        // asi que no vale con probar UNA frase: se prueban las CUATRO que el programa puede llegar a escribir,
        // cada una con el primer renglon en su peor caso (nombre de equipo del largo maximo que deja el corte,
        // la frecuencia mas larga y el nombre de bocinas mas largo, "cuadrafonico (con subwoofer)").
        string peorPrimera = "Tu equipo: Altavoces (2- High Definition A..  -  Windows mezcla a 192 kHz"
                           + ", bocinas en cuadrafonico (con subwoofer), sin graves propios.";
        string[] segundas = {
            "Windows tiene sus MEJORAS ENCENDIDAS: ecualizan encima de la rocola. Apagalas en Panel de sonido -> tu salida -> Mejoras.",
            "Windows reparte el estereo en 8 canales. Si tus bocinas son dos, ponlo en estereo: el reparto ensucia el bajo.",
            "Sin mejoras de Windows encima: la rocola tiene el sonido para ella sola, que es como debe ser.",
            "Windows no dice si tiene mejoras puestas. Si algo suena raro, miralo en Panel de sonido -> tu salida -> Mejoras."
        };
        for (int v = 0; v < segundas.Length; v++)
            C("salida.v" + (v + 1), 24, 62, 916, 34, peorPrimera + "\n" + segundas[v], fDes, 8.5f, true);
        // Y la que sale cuando este Windows no guarda nada legible.
        C("salida.sinDatos", 24, 62, 916, 34,
          "Tu equipo: este Windows no guarda la configuracion de las bocinas donde se pueda leer.\n"
          + "Revisa el panel de tu tarjeta de sonido (Realtek y compania) y deja sus mejoras APAGADAS.",
          fDes, 8.5f, true);

        // ---- tarjeta VOLUMEN (16,70,456,400) ----
        int vx = 16, vy = 100;
        CSw("nivelar", vx + 16, vy + 40, 424, 26, "Nivelar el volumen de todas las canciones", fNom, 10f);
        Fila("objetivo", vx, vy, 76, "Volumen objetivo", "100%",
             "Que tan fuerte queda el conjunto.\nMas alto = todas las canciones suenan mas fuerte.", false);
        Fila("fuerza", vx, vy, 156, "Fuerza del nivelado", "100%",
             "Cuanto se suben las canciones bajitas.\nLas que ya suenan fuerte NUNCA se bajan.", false);
        Fila("subir", vx, vy, 236, "Subir TODO el volumen", "+12 dB",
             "De mas para TODAS, ademas del nivelado.\nPara cuando el local se queda corto de volumen.", false);
        Fila("videos", vx, vy, 316, "Volumen de los videos", "100%",
             "Los videos no pasan por la cadena de sonido.\nSu volumen se empareja aqui a mano.", false);

        // ---- tarjeta CALIDAD (488,70,456,400) ----
        int cx = 488, cy = 100;
        Fila("eq", cx, cy, 44, "EQ automatico", "100%",
             "Empareja el tono de cada cancion con el de tu biblioteca.\nSolo: mas fuerza cuantas mas canciones medidas (van 52,842).", true);
        Fila("graves", cx, cy, 124, "Graves (bajo profundo)", "100%",
             "Devuelve el golpe de bajo que el archivo no trae.\nSolo: a cada cancion se le da lo que le falta.   Esta: 170%.", true);
        Fila("brillo", cx, cy, 204, "Realce de brillo", "100%",
             "Devuelve el 'aire' que el MP3 apaga.\nSe le da mas a las canciones peor grabadas.", false);
        CSw("dsee", cx + 16, cy + 292, 424, 26, "DSEE", fNom, 10f);
        C("dsee.explica", cx + 16, cy + 322, 424, 68,
          "Oye cada cancion y ve donde el MP3 le corto los agudos.\n"
          + "Eso lo mide SIEMPRE: de ahi sacan el EQ automatico y los\n"
          + "graves automaticos cuanto le falta a cada una.\n"
          + (hd ? "Encendido, ademas los recrea. Se apaga con DSEE ULTRA."      // en HD manda el DSEE ULTRA
                : "EXPERIMENTAL: encendido, ademas los recrea con la IA."), fDes, 8.5f, true);

        // ---- tarjeta IA EN VIVO, solo en HD (16,480,928,232) ----
        if (hd)
        {
            int ix = 16, iy = 508;
            CSw("ultra", ix + 16, iy + 40, 480, 26, "DSEE ULTRA  -  restaurar con IA (maxima calidad)", fNom, 10f);
            C("ultra.explica", ix + 16, iy + 68, 896, 18, "Restaura la cancion entera con IA usando la tarjeta grafica. Queda lista despues de oirla una vez.", fDes, 8.5f, false);
            CSw("ia.auto", ix + 16, iy + 96, 480, 26, "Decidir sola cuanta IA segun el dano", fNom, 10f);
            C("ia.mezcla.nombre", ix + 16, iy + 132, 330, 18, "Cuanta IA", fNom, 10f, false);
            C("ia.mezcla.valor", ix + 346, iy + 130, 94, 20, "sola", fVal, 11f, false);
            C("ia.mezcla.explica", ix + 16, iy + 152, 424, 32, "Cuanto de lo restaurado se mezcla con el original.\nMenos IA = mas natural; mas IA = mas limpio.", fDes, 8.5f, true);
            C("ia.mezcla.barra", ix + 16, iy + 186, 424, 22, "", null, 0f, false);
            C("gpu.nombre", ix + 488, iy + 132, 424, 18, "Tarjeta grafica que hace los calculos", fNom, 10f, false);
            C("gpu.lista", ix + 488, iy + 156, 424, 26, "GPU 0 - NVIDIA GeForce RTX 4070 SUPER", fSub, 9.5f, false);
        }

        // ---- tarjeta BIBLIOTECA ----
        int bx = 16, by = hd ? 750 : 508;
        C("calidad", bx + 16, by + 40, 896, 20,
          "Calidad:   52,842 completas     52,842 aceptables     52,842 pobres     52,842 con los picos recortados", fNom, 10f, false);
        C("estado", bx + 16, by + 62, 896, 48,
          "La biblioteca se mide sola mientras la rocola trabaja: faltan 52,842 canciones.   Duraciones: faltan 52,842.\n"
          + "Material para la IA: 3,171,340 ejemplos (483 MB).   La cancion que suena entra en la etapa MEDIA.\n"
          + "El detalle, cancion por cancion, queda en rocola_calidad.txt junto al programa.", fDes, 8.5f, true);
        C("boton.repaso", bx + 16, by + 114, 300, 34, "Repasar la biblioteca a fondo", fBot, 10f, false);
        C("avance.texto", bx + 332, by + 112, 580, 18, "52,842 de 52,842 canciones   (100%)", fDes, 8.5f, false);
        C("avance.barra", bx + 332, by + 134, 580, 8, "", null, 0f, false);

        // ---- pie ----
        C("boton.fabrica", 16, hd ? 922 : 680, 220, 38, "Dejar como de fabrica", fBot, 10f, false);
        C("boton.listo", 804, hd ? 922 : 680, 140, 38, "Listo", fBot, 10f, false);

        // ---------- que todo quepa, contando RENGLONES ----------
        Console.WriteLine("\n    hueco                      renglones   caben   ancho pedido / hay");
        bool cabe = true;
        foreach (Caja c in cajas)
        {
            if (c.F == null || c.Texto.Length == 0) continue;
            int alto = AltoRenglon(c.Pt);
            int rengl = Renglones(c.Texto, c.F, c.R.Width, c.Extra);
            int caben = c.R.Height / alto;
            int pideW = 0;
            foreach (string parte in c.Texto.Split('\n'))
            { int w = (int)(TextRenderer.MeasureText(parte, c.F).Width * Margen); if (w > pideW) pideW = w; }
            pideW += c.Extra;
            Console.WriteLine(string.Format("    {0,-26} {1,6}   {2,6}    {3,6} / {4,5}", c.Nombre, rengl, caben, pideW, c.R.Width));
            bool ok = (rengl <= caben) && (c.Envuelve || pideW <= c.R.Width);
            if (!ok) { cabe = false; Console.WriteLine("        ^^^ NO CABE" + (rengl > caben ? " (le falta alto)" : " (le falta ancho)")); }
        }
        Check(cabe, "todos los textos caben ENTEROS, contando renglones con el alto del peor caso");

        // ---------- que nada se monte encima de nada ----------
        // Ojo: las "salida.*" son las frases ALTERNATIVAS de un mismo renglon (solo se ensena una cada vez),
        // asi que comparten sitio a proposito y no cuentan como choque.
        string choque = null;
        for (int i = 0; i < cajas.Count && choque == null; i++)
            for (int j = i + 1; j < cajas.Count && choque == null; j++)
            {
                if (Variantes(cajas[i].Nombre, cajas[j].Nombre)) continue;
                if (cajas[i].R.IntersectsWith(cajas[j].R)) choque = cajas[i].Nombre + " se monta con " + cajas[j].Nombre;
            }
        if (choque != null) Console.WriteLine("\n    " + choque);
        Check(choque == null, "ningun control se monta encima de otro");

        // ---------- que la ventana entera quepa ----------
        int abajo = 0, derecha = 0;
        foreach (Caja c in cajas) { if (c.R.Bottom > abajo) abajo = c.R.Bottom; if (c.R.Right > derecha) derecha = c.R.Right; }
        Console.WriteLine(string.Format("\n    la ventana mide {0}x{1} y su contenido llega a {2}x{3}", W, H, derecha, abajo));
        Check(derecha <= W - 8 && abajo <= H - 8, "todo el contenido queda dentro de la ventana, con margen");
        // 1024x768 es la pantalla mas chica en la que puede correr; la barra de titulo de un dialogo se lleva ~30 px
        Check(W <= 1024 && (hd || H + 30 <= 768), hd
              ? "y la ventana cabe de sobra en la pantalla de la maquina HD"
              : "y la ventana entera, con su barra de titulo, cabe en 1024x768");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
