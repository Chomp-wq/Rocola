// EL DIBUJO DEL ASISTENTE CON SPOTIFY (fase 11, grupo asistente-spotify; RocolaOs.Asistente.cs). Nada nuevo: el globo de la pregunta es
// PintaPanelAsistente de XP (Codigo/AsistenteRocola.cs) copiado tal cual, con sus mismas letras, colores, vidrio, opciones, botones dibujados
// y leyenda; solo cambian los textos y cuantas opciones lleva (cuatro, o las tres de Spotify), y sus opciones van donde las pone
// AsistenteOpcionRect (con cuatro, un poco mas juntas: g76). El globo de la guia es PintaGuiaAsistente de XP, llamada directo, con los
// textos de las guias de Spotify. Sin Spotify, estas funciones devuelven null y dibuja XP.
using System;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
        // g80 (EnsureAsistentePanel: «: PintaPanelAsistente(tam.Width, tam.Height, 0, 0, asistenteSeleccion);»): EL GLOBO DE LA PREGUNTA con
        // Spotify. null = el de XP.
        private SKBitmap RocolaOsAsistenteMenuPinta(int w, int h, int seleccion)
        {
            if (!rocolaOsAsSpotify) return null;
            if (rocolaOsAsMenu == 0)
                return RocolaOsPintaPregunta(w, h, seleccion, "¡HOLA! TE ACOMPAÑO PASO A PASO", "¿Qué quieres hacer?",
                    "Elige y yo te voy señalando qué botón tocar. Tú lo haces y la música sigue sonando.",
                    new[] { "Poner una canción", "Buscar una canción por su nombre", "Aprender a moverme", "Canciones de Spotify" }, "salir");
            return RocolaOsPintaPregunta(w, h, seleccion, "CANCIONES DE SPOTIFY", "¿Qué quieres saber?",
                "Se piden con los mismos botones y cuestan 1 crédito, como las tuyas. Sin créditos no se pide.",
                new[] { "Pedir una canción de Spotify", "La letra ♫ de Spotify", "Los discos de un artista" }, "volver");
        }

        // PintaPanelAsistente de XP con los textos y las opciones como parametros (con cuatro opciones, la caja del texto de arriba es un
        // poco mas baja: las opciones empiezan antes). Linea por linea lo mismo que XP.
        internal static SKBitmap RocolaOsPintaPregunta(int w, int h, int seleccion, string arriba, string pregunta, string explica, string[] nombres, string salir)
        {
            w = Math.Max(1, w); h = Math.Max(1, h);
            int opciones = nombres.Length;
            SKBitmap bmp = Mapas.Nuevo(w, h);
            using (SKCanvas g = new SKCanvas(bmp))
            using (Rocola.Nativo.Letra pequeno = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), Math.Max(10, Math.Min(18, h * .034f)), true))
            using (Rocola.Nativo.Letra titulo = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), Math.Max(20, Math.Min(42, h * .085f)), true))
            using (Rocola.Nativo.Letra cuerpo = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), Math.Max(12, Math.Min(21, h * .043f))))
            using (Rocola.Nativo.Letra opcion = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), Math.Max(14, Math.Min(27, h * .052f)), true))
            using (SKPaint blanco = new SKPaint { Color = Color.FromArgb(246, 248, 255).Sk() })
            using (SKPaint tenue = new SKPaint { Color = Color.FromArgb(185, 206, 234).Sk() })
            using (SKPaint azul = new SKPaint { Color = Color.FromArgb(150, 215, 255).Sk() })
            {
                FormatoTexto formato = new FormatoTexto();
                g.Clear(Color.Transparent.Sk()); bool suave = true;
                AjusteLetra ajuste = AjusteLetra.Suave; bool copia = false, medio = false;
                using (SKPath borde = AsistenteGloboCamino(w, h))
                using (SKShader degradado = DegradadoV(new Rectangle(0, 0, w, h), Color.FromArgb(252, 24, 42, 74), Color.FromArgb(248, 10, 18, 37), medio)) using (SKPaint vidrio = new SKPaint { Shader = degradado })
                using (SKPaint halo = new SKPaint { Color = Color.FromArgb(35, 101, 206, 255).Sk(), StrokeWidth = 5f })
                using (SKPaint brillo = new SKPaint { Color = Color.FromArgb(220, 142, 215, 255).Sk(), StrokeWidth = 1.8f })
                { Pixel.Traza(g, borde, halo, suave, medio, copia); Pixel.Rellena(g, borde, vidrio, suave, medio, copia); Pixel.Traza(g, borde, brillo, suave, medio, copia); }
                int x = AsistenteGloboMargen(w), tw = Math.Max(1, w - x - Math.Max(12, w / 20));
                TextoGdiPlus.Escribe(g, arriba, pequeno, ajuste, azul, new RectangleF(x, h * .045f, tw, h * .06f), null, copia, medio);
                TextoGdiPlus.Escribe(g, pregunta, titulo, ajuste, blanco, new RectangleF(x, h * .12f, tw, h * .17f), null, copia, medio);
                TextoGdiPlus.Escribe(g, explica, cuerpo, ajuste, tenue, new RectangleF(x, h * .30f, tw, opciones > AsistenteOpciones ? h * .16f : h * .19f), null, copia, medio);
                formato.Vertical = Alinea.Centro; formato.Banderas = BanderasTexto.SinPartir;
                Color rojo = Color.FromArgb(228, 72, 72), verdeC = Color.FromArgb(70, 200, 105), apagado = Color.FromArgb(104, 112, 134);
                Rectangle r0 = AsistenteOpcionRect(w, h, 0);
                float libreTxt = Math.Max(1, r0.Width - 24 - AsistenteAnchoControles(r0));
                Rocola.Nativo.Letra fOpc = opcion, propia = null;
                for (int paso = 0; paso < 8; paso++)
                {
                    float mas = 0; for (int i = 0; i < nombres.Length; i++) mas = Math.Max(mas, TextoGdiPlus.Mide(nombres[i], fOpc, ajuste, new SizeF(0, 0), formato).Width);
                    if (mas <= libreTxt || fOpc.Tamano <= opcion.Tamano * .70f) break;
                    if (propia != null) propia.Dispose();
                    propia = Rocola.Nativo.Letra.EnPixeles(opcion.Familia, Math.Max(opcion.Tamano * .70f, fOpc.Tamano * Math.Max(.70f, libreTxt / mas * .98f)), opcion.Negrita, opcion.Cursiva, opcion.Subrayada);
                    fOpc = propia;
                }
                try
                {
                    for (int i = 0; i < opciones; i++)
                    {
                        Rectangle r = AsistenteOpcionRect(w, h, i);
                        bool elegida = i == seleccion;
                        using (SKPath camino = ContornoRedondo(r, Math.Min(14, r.Height / 3)))
                        using (SKPaint relleno = new SKPaint { Color = (elegida ? Color.FromArgb(235, 42, 87, 130) : Color.FromArgb(150, 28, 42, 68)).Sk() })
                        using (SKPaint contorno = new SKPaint { Color = (elegida ? Color.FromArgb(150, 235, 188) : Color.FromArgb(90, 113, 150)).Sk(), StrokeWidth = elegida ? 2f : 1f })
                        { Pixel.Rellena(g, camino, relleno, suave, medio, copia); Pixel.Traza(g, camino, contorno, suave, medio, copia); }
                        TextoGdiPlus.Escribe(g, nombres[i], fOpc, ajuste, blanco, new RectangleF(r.Left + 12, r.Top, Math.Max(1, r.Width - 24 - (elegida ? AsistenteAnchoControles(r) : 0)), r.Height), formato, copia, medio);
                        if (!elegida) continue;
                        Rectangle ka, kb, kv; AsistenteControlesOpcion(r, out ka, out kb, out kv);
                        TeclaFlecha(g, suave, ajuste, copia, medio, ka.X, ka.Y + ka.Height / 2, ka.Width, i > 0 ? rojo : apagado, 0);
                        TeclaFlecha(g, suave, ajuste, copia, medio, kb.X, kb.Y + kb.Height / 2, kb.Width, i < opciones - 1 ? rojo : apagado, 1);
                        CtrlDot(g, suave, ajuste, copia, medio, kv.X, kv.Y + kv.Height / 2, kv.Width, verdeC);
                    }
                }
                finally { if (propia != null) propia.Dispose(); }
                int ld = Math.Max(12, Math.Min(26, (int)(h * .046f))), lcy = (int)(h * .955f);
                float lx = x;
                Color azulC = Color.FromArgb(150, 215, 255);
                TeclaFlecha(g, suave, ajuste, copia, medio, lx, lcy, ld, rojo, 0); lx += ld + 3;
                TeclaFlecha(g, suave, ajuste, copia, medio, lx, lcy, ld, rojo, 1); lx += ld + 6;
                lx = CtrlTxt(g, suave, ajuste, copia, medio, pequeno, "mover", lx, lcy, azulC) + ld;
                lx = CtrlDot(g, suave, ajuste, copia, medio, lx, lcy, ld, verdeC) + 6;
                lx = CtrlTxt(g, suave, ajuste, copia, medio, pequeno, "elegir", lx, lcy, azulC) + ld;
                lx = CtrlDot(g, suave, ajuste, copia, medio, lx, lcy, ld, rojo) + 6;
                CtrlTxt(g, suave, ajuste, copia, medio, pequeno, salir, lx, lcy, azulC);
            }
            return bmp;
        }

        // g83 (PintaGuiaActual: «string arriba, titulo, texto, teclas;»): EL GLOBO DE UNA GUIA DE SPOTIFY, con PintaGuiaAsistente de XP. null =
        // una guia de XP (la dibuja XP).
        private SKBitmap RocolaOsAsistenteGuiaPinta(int w, int h)
        {
            int m = asistenteMision;
            if (m < RocolaOsMisionPide || m > RocolaOsMisionArtista) return null;
            string arriba, titulo, texto, teclas;
            if (asistenteLogroTick != 0)
            {
                arriba = "¡LO LOGRASTE!";
                if (m == RocolaOsMisionArtista) { titulo = "¡Ya viste sus discos!"; texto = "El ROJO siempre te regresa. Si me necesitas otra vez: ROJO + ABAJO."; }
                else if (RocolaOsAsPidioSpotify) { titulo = "¡Tu canción está en la cola!"; texto = "Es de Spotify y suena como las tuyas. Si me necesitas otra vez: ROJO + ABAJO."; }
                else { titulo = "¡Tu canción está en la cola!"; texto = "Ya va en camino. Si me necesitas otra vez: ROJO + ABAJO."; }
                teclas = "";
            }
            else
            {
                RocolaOsAsistenteTextos(asistentePaso, out titulo, out texto, out teclas);
                int total, numero = RocolaOsAsistenteNumero(m, asistentePaso, credits > 0, out total);
                arriba = GuiaBienAhora ? "¡MUY BIEN! SIGUIENTE PASO" : numero > 0 ? "PASO " + numero + " DE " + total : "SIGAMOS";
            }
            return PintaGuiaAsistente(w, h, arriba, titulo, texto, teclas, asistenteLogroTick != 0);
        }
    }
}
