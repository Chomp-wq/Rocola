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
        public string Nombre, Texto; public Rectangle R; public Font F; public float Pt; public bool Envuelve; public int Extra; public int Pag;
        public Caja(string n, int x, int y, int w, int h, string t, Font f, float pt, bool env, int extra, int pag)
        { Nombre = n; R = new Rectangle(x, y, w, h); Texto = t; F = f; Pt = pt; Envuelve = env; Extra = extra; Pag = pag; }
    }
    static List<Caja> cajas = new List<Caja>();
    // DE QUE PAGINA SE ESTA HABLANDO. La ventana tiene dos pestanas -Sonido e Imagen- y las dos ocupan el
    // MISMO sitio: solo se ve una cada vez. Sin esto, cada control de Imagen saldria "montado encima" de uno
    // de Sonido y la comprobacion de choques no valdria para nada. La pagina 2 es el marco (titulo, pestanas
    // y el boton de Listo), que se ve en las dos y por tanto choca con las dos.
    static int pagActual = 0;
    static void C(string n, int x, int y, int w, int h, string t, Font f, float pt, bool env) { cajas.Add(new Caja(n, x, y, w, h, t, f, pt, env, 0, pagActual)); }
    static void CSw(string n, int x, int y, int w, int h, string t, Font f, float pt) { cajas.Add(new Caja(n, x, y, w, h, t, f, pt, false, 50, pagActual)); }   // los interruptores llevan la pastilla delante
    static bool MismaPagina(Caja a, Caja b) { return a.Pag == b.Pag || a.Pag == 2 || b.Pag == 2; }

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
        if (ra != b.Substring(0, pb)) return false;
        return ra == "salida" || ra == "veredicto" || ra == "propuesta" || ra == "pierde";
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
        int W = 960, H = hd ? 1008 : 732;

        pagActual = 2;   // el marco: se ve en las dos pestanas
        // El titulo encogio de 700 a 600 para dejarle sitio a las pestanas. No mueve nada: el texto mas
        // largo que lleva es "Imagen y rendimiento", que ni de lejos llega a 600.
        C("titulo", 22, 10, 600, 28, "Imagen y rendimiento", fTit, 15f, false);
        C("pestana.sonido", 640, 8, 148, 30, "Sonido", fBot, 10f, false);
        C("pestana.imagen", 796, 8, 148, 30, "Imagen", fBot, 10f, false);
        C("boton.listo", 804, hd ? 956 : 680, 140, 38, "Listo", fBot, 10f, false);
        pagActual = 0;   // de aqui en adelante, la pagina de Sonido
        C("subtitulo", 24, 42, 590, 18, "Todo lo que cambies aqui se oye al instante en la cancion que esta sonando.", fSub, 9.5f, false);
        // EL MODO NOCHE va en el hueco que queda a la derecha del subtitulo: es el unico sitio de esta
        // ventana donde cabia sin mover nada (el subtitulo gasta 545 de sus 916 px). Se mide con las horas
        // mas largas que pueden salir -dos digitos cada una- porque el letrero las saca de la configuracion.
        CSw("noche", 624, 40, 316, 22, "Bajar el volumen de 23 a 7 h", fInt, 9.5f);
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
          + "Material para la IA: apagado (lo demas se sigue midiendo igual).   La cancion que suena entra en la etapa MEDIA.\n"
          + "El detalle, cancion por cancion, queda en rocola_calidad.txt junto al programa.", fDes, 8.5f, true);
        C("boton.repaso", bx + 16, by + 114, 300, 34, "Repasar la biblioteca a fondo", fBot, 10f, false);
        // En HD el renglon de arriba ya lo llenan el repaso a fondo, su avance y la limpieza de videos que no
        // suenan (que alli si existe: descomprimir su sonido pide Media Foundation, y en XP no la hay). Por
        // eso el interruptor de la IA va en un renglon propio y la tarjeta es un renglon mas alta.
        if (hd)
        {
            C("avance.texto", bx + 332, by + 112, 306, 18, "52,842 de 52,842 canciones   (100%)", fDes, 8.5f, false);
            C("avance.barra", bx + 332, by + 134, 306, 8, "", null, 0f, false);
            C("boton.mudos", bx + 652, by + 114, 260, 34, "Videos que no suenan: 52,842", fBot, 10f, false);
            CSw("ia.juntar", bx + 16, by + 156, 286, 26, "Juntar material para la IA", fNom, 10f);
        }
        else
        {
            CSw("ia.juntar", bx + 332, by + 118, 286, 26, "Juntar material para la IA", fNom, 10f);
            C("avance.texto", bx + 630, by + 112, 282, 18, "52,842 de 52,842 canciones   (100%)", fDes, 8.5f, false);
            C("avance.barra", bx + 630, by + 134, 282, 8, "", null, 0f, false);
        }

        // ---- pie ----
        C("boton.fabrica", 16, hd ? 956 : 680, 220, 38, "Dejar como de fabrica", fBot, 10f, false);

        // ============================ LA PAGINA DE IMAGEN Y RENDIMIENTO ============================
        // Vive en un Panel que empieza en y=96, asi que aqui todo va en coordenadas de la VENTANA: las del
        // panel mas 96. Es la misma comprobacion de siempre -que quepa con la letra del PEOR caso, no con la
        // de esta maquina-, y aqui hace mas falta que en ninguna parte porque es texto nuevo y largo.
        pagActual = 1;
        const int PY = 96;
        // ---- tarjeta ESTE EQUIPO (16,100,456,400) ----
        C("medir.boton", 32, PY + 44, 424, 36, "Medir este equipo", fBot, 10f, false);
        C("medir.tarda", 32, PY + 86, 424, 32,
          "Tarda medio minuto. Pone una cancion muda y usa la rocola sola:\npasa de pagina, cambia de letra, escribe y entra en discos.", fDes, 8.5f, true);
        C("medir.cabecera", 32, PY + 124, 424, 16, "PANTALLA                          TRABAJO   CUADROS/S", fDes, 8.5f, false);
        for (int i = 0; i < 5; i++)
            C("medir.fila" + i, 32, PY + 144 + i * 22, 424, 20,
              "Escribiendo en el buscador  999.9 ms      999   <-- la que manda", fDes, 8.5f, false);
        // El techo de la pantalla y el aviso de "sin musica" van en SU renglon, no dentro del veredicto:
        // metidos ahi lo hacian crecer a trece renglones y no cabia. Cada cosa en su sitio.
        C("medir.techo", 32, PY + 252, 424, 16, "El techo de esta pantalla son 999 cuadros por segundo.", fDes, 8.5f, false);
        C("medir.sinmusica", 32, PY + 272, 424, 32,
          "OJO: no se pudo poner ninguna cancion y se midio en\n"
          + "silencio. De verdad saldra peor de lo que dice aqui.", fDes, 8.5f, true);
        // El veredicto se arma sobre la marcha: se prueban las CUATRO frases que puede llegar a escribir, y
        // comparten sitio a proposito (solo se ve una cada vez).
        string peorTxt = "La que peor va es \"Escribiendo en el buscador\": 999 cuadros por segundo (999.9 ms de"
                       + " trabajo por cuadro).";
        C("veredicto.v1", 32, PY + 308, 424, 80,
          "Aun sin medir.\n\nAqui no hay ningun numero porque ninguno seria de verdad: los de este equipo salen"
          + " de medirlo.", fDes, 8.5f, true);
        C("veredicto.v2", 32, PY + 308, 424, 80,
          "Esta rocola NO esta dibujando con la tarjeta grafica.\n\nPor ese camino el techo son 24 cuadros por"
          + " segundo, puesto asi a proposito para gastar poco. La perilla que importa aqui es encender la"
          + " tarjeta.", fDes, 8.5f, true);
        C("veredicto.v3", 32, PY + 308, 424, 80,
          peorTxt + "\n\nPasa de los 30 de sobra. No hace falta bajar nada.", fDes, 8.5f, true);
        C("veredicto.v4", 32, PY + 308, 424, 80,
          peorTxt + "\n\nNo llega a los 30. Al lado de cada cosa, a la derecha, esta lo que se gana bajandola un"
          + " escalon en ESTE equipo.", fDes, 8.5f, true);

        // ---- tarjeta LA PROPUESTA (16,508,456,160) ----
        C("propuesta.v1", 32, PY + 448, 424, 68,
          "Sin medir todavia. La propuesta sale de lo que se mida en ESTE equipo, no de una lista de equipos"
          + " buenos y malos escrita a mano.", fDes, 8.5f, true);
        C("propuesta.v2", 32, PY + 448, 424, 68,
          "Bajar patron nuevo de estrellas a bajo, zoom al entrar a un disco a medio y 4 mas deja"
          + " \"Escribiendo en el buscador\" en 999.  AUN ASI NO LLEGA A 30: este equipo no da para mas ni"
          + " bajandolo todo.", fDes, 8.5f, true);
        C("propuesta.v3", 32, PY + 448, 424, 68,
          "No llega a los 30 y NO hay nada que bajar que lo arregle: ninguno de los efectos cuesta lo"
          + " suficiente como para notarse. El cuello esta en otro sitio, no en los adornos del cielo.",
          fDes, 8.5f, true);
        C("propuesta.aplicar", 32, PY + 524, 424, 34, "Aplicar lo recomendado", fBot, 10f, false);

        // ---- tarjeta EFECTOS: la sub-ventana de al lado (488,100,456,568) ----
        C("efectos.general", 504, PY + 42, 424, 16, "Todo de golpe:        (ahora esta a medida)", fDes, 8.5f, false);
        string[] nomNivel = { "Bajo", "Medio", "Alto" };
        for (int k = 0; k < 3; k++)
            C("general." + nomNivel[k], 504 + k * 144, PY + 62, 136, 28, nomNivel[k], fBot, 10f, false);
        string[] efeNom = { "Nieblas y nebulosas", "Cuantas estrellas", "Aurora del horizonte",
                            "Patron nuevo de estrellas", "Estrellas fugaces", "Zoom al entrar a un disco" };
        for (int i = 0; i < 6; i++)
        {
            int y = PY + 104 + i * 68;
            C("efecto" + i + ".nombre", 504, y, 424, 18, efeNom[i], fNom, 10f, false);
            for (int k = 0; k < 3; k++)
                C("efecto" + i + ".n" + k, 504 + k * 102, y + 22, 96, 26, nomNivel[k], fBot, 10f, false);
            C("efecto" + i + ".coste", 810, y + 24, 118, 18, "ya esta abajo", fDes, 8.5f, false);
        }
        // El renglon que explica que se pierde con el nivel que acabas de tocar: se prueban los seis textos
        // mas largos, que comparten sitio (solo se ve uno cada vez).
        string[] efePierde = {
            "Se queda la nebulosa de color y se va el velo de encima.",
            "Un tercio de las estrellas: el cielo se ve bastante mas vacio.",
            "A media altura: se sigue viendo latir, mas discreta.",
            "Cada minuto, con las dos constelaciones cruzandose.",
            "Una cada 15-30 segundos, en vez de dos cada 7-16.",
            "Sin animacion: el disco se abre de golpe." };
        for (int i = 0; i < 6; i++)
            C("pierde.v" + i, 504, PY + 496, 424, 64, efeNom[i] + " en medio:\n" + efePierde[i], fDes, 8.5f, true);

        C("imagen.fabrica", 16, PY + 584, 300, 38, "Graficos como de fabrica", fBot, 10f, false);
        pagActual = 0;

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
                if (!MismaPagina(cajas[i], cajas[j])) continue;   // dos pestanas: solo se ve una cada vez
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
