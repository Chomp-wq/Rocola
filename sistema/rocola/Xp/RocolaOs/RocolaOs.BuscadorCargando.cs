// EL ANILLO DE CARGA DEL BUSCADOR (Rocola OS, modo Spotify; peticion del dueno del 23-11-2026: «Con el delay artificial del buscador, en donde
// salen los resultados saldra cargando... Y un anillo de carga en lo que salen los resultados, disenalo bien para que encaje con el aspecto del
// resto del programa»).
//
// Mientras el buscador con portadas espera a Spotify (rocolaOsSpEspera, g40: RocolaOs.SpotifyCatalogo.cs) NO ensena resultados (ni los de
// antes): la lista es una vacia aparte, el sitio de las tarjetas no dice «Sin resultados» y la casilla de arriba dice «Buscando…». Pasados
// 150 ms (para que una respuesta rapida no destelle) sale, con un fundido de 150 ms, una TARJETA DE CARGA como las de los resultados (la misma
// imagen horneada: fondo, borde y sombra; un 30 % mas ancha, centrada, en la primera fila): en el sitio de la portada un anillo (pista tenue y un
// arco de 100 grados, extremos redondos, del color del genero aclarado, una vuelta cada 0,9 s) y a la derecha, con las letras y colores de las
// tarjetas, «BUSCANDO», «Cargando…» y «En tu música y en Spotify». Al llegar la respuesta la lista cambia y la tarjeta se va sola.
// La tarjeta es la PIEZA 3 del buscador con portadas (la del texto bajo el cubo, que ahi no existe): una imagen chica propia que se rehace a
// 30 cuadros por segundo como mucho (su firma lleva el cuadro) y se sube sola con la GPU; el resto del buscador no se repinta.
// Ganchos (Codigo/BuscadorPortadasGpu.cs): g266 PiezaVivaNueva (donde va), g267 DibujaPiezaVivaNueva (dibujarlo), g268 FirmaPieza (el cuadro),
// g269 HorneaCartas (sin el aviso de «Sin resultados» mientras se espera), g270 TextoCuenta («Buscando…»).
// Sin Rocola OS, sin el modo Spotify o sin espera, nada de esto se activa: el buscador es el de XP tal cual.
using System;
using System.Collections.Generic;
using System.Drawing;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
        private partial class SearchOverlay
        {
#if ROCOLA_GPU
            private List<SearchItem> rocolaOsEsperaLista;   // la lista vacia de la espera (la que esta en Results mientras se espera)
            private double rocolaOsEsperaDesde;             // desde cuando (GpuPanel.AhoraMs)
            private const double RocolaOsAnilloRetrasoMs = 150, RocolaOsAnilloFundeMs = 150, RocolaOsAnilloVueltaMs = 900, RocolaOsAnilloCuadroMs = 1000.0 / 30.0;

            // la lista de la espera: la misma si ya se esperaba (otra letra tecleada: el anillo sigue, no vuelve a salir), si no una nueva
            internal List<SearchItem> RocolaOsListaEspera()
            {
                if (!RocolaOsBuscando()) { rocolaOsEsperaLista = new List<SearchItem>(); rocolaOsEsperaDesde = GpuPanel.AhoraMs(); }
                return rocolaOsEsperaLista;
            }
            // se esta esperando a Spotify: lo que hay en los resultados es la lista vacia de la espera
            internal bool RocolaOsBuscando() { return rocolaOsEsperaLista != null && ReferenceEquals(Results, rocolaOsEsperaLista); }

            // cuanto lleva esperando (ms); menos de 0 si no se espera
            private double RocolaOsEsperaMs() { return RocolaOsBuscando() ? GpuPanel.AhoraMs() - rocolaOsEsperaDesde : -1.0; }
            // el cuadro del anillo (0 = todavia no sale): cambia 30 veces por segundo
            internal int RocolaOsAnilloCuadro()
            {
                double t = RocolaOsEsperaMs();
                return t < RocolaOsAnilloRetrasoMs ? 0 : 1 + (int)(t / RocolaOsAnilloCuadroMs);
            }
            // la tarjeta de carga (en coordenadas del buscador): la de la primera fila de resultados, centrada, un 30 % mas ancha que una tarjeta
            private Rectangle RocolaOsAnilloCarta()
            {
                RepartoBusca r = AseguraReparto();
                Rectangle q = r.Resultados;
                int w = Math.Min(q.Width, (int)Math.Round(r.CartaW * 1.3));
                return new Rectangle(q.X + (q.Width - w) / 2, q.Y, w, r.CartaH);
            }
            // el sitio de la imagen chica: la tarjeta con el sitio de su sombra
            internal bool RocolaOsAnilloPieza(out Rectangle donde)
            {
                donde = Rectangle.Empty;
                if (RocolaOsAnilloCuadro() == 0) return false;
                RepartoBusca r = AseguraReparto();
                if (r.Resultados.Width < 100 || r.CartaH < 20) return false;
                donde = Rectangle.Inflate(RocolaOsAnilloCarta(), Px(24, r.S), Px(24, r.S));
                return true;
            }
            internal void RocolaOsAnilloDibuja(SKCanvas g, AjusteLetra ajuste, bool copia, bool medio, int dx, int dy)
            {
                Rectangle p;
                if (!RocolaOsAnilloPieza(out p)) return;
                RepartoBusca r = AseguraReparto(); AseguraFuentes(r.S);
                float s = r.S;
                double t = RocolaOsEsperaMs();
                double tc = Math.Floor(t / RocolaOsAnilloCuadroMs) * RocolaOsAnilloCuadroMs;   // el tiempo del cuadro (la firma lleva el mismo)
                float a = (float)Math.Max(0.0, Math.Min(1.0, (tc - RocolaOsAnilloRetrasoMs) / RocolaOsAnilloFundeMs));
                if (a <= 0f) a = 1f / 255f;
                Rectangle cr = RocolaOsAnilloCarta(); cr.Offset(dx, dy);
                // la tarjeta: la misma imagen horneada que las de resultados (fondo, borde y sombra), con el fundido de entrada
                Color v = Vivo(Accent);
                int capa = g.SaveLayer(new SKPaint { Color = new SKColor(255, 255, 255, (byte)Math.Round(255 * a)) });
                PegaQuieto(g, true, copia, Filtro.Bilineal, medio, QuietoCarta, cr, v, v);
                g.RestoreToCount(capa);
                Rectangle cov = CajaPortadaCarta(cr, s);
                float grueso = Px(5, s), diam = Math.Min(Px(52, s), cov.Width);
                float cx = cov.X + cov.Width / 2f, cy = cov.Y + cov.Height / 2f;
                float rad = (diam - grueso) / 2f;
                SKRect ov = new SKRect(cx - rad, cy - rad, cx + rad, cy + rad);
                float giro = (float)((tc % RocolaOsAnilloVueltaMs) / RocolaOsAnilloVueltaMs * 360.0);
                Color claro = Lighten(v, .35);
                using (SKPaint pista = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = grueso, Color = Color.FromArgb((int)Math.Round(60 * a), 207, 224, 255).Sk() })
                using (SKPaint arco = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = grueso, StrokeCap = SKStrokeCap.Round, Color = Color.FromArgb((int)Math.Round(255 * a), claro).Sk() })
                {
                    g.DrawOval(ov, pista);
                    g.DrawArc(ov, -90f + giro, 100f, false, arco);
                }
                // los textos con la misma disposicion que una tarjeta: tipo, titulo y subtitulo
                int tx = cov.Right + Px(12, s), tw = cr.Right - Px(10, s) - tx;
                int gap = Math.Max(2, Px(4, s));
                int hTipo = Alto(fnTipo), hTit = Alto(fnTitulo), hSub = Alto(fnSub);
                int y = cr.Y + (cr.Height - (hTipo + gap + hTit + gap + hSub)) / 2;
                int ia = (int)Math.Round(255 * a);
                Renglon(g, ajuste, copia, medio, "BUSCANDO", fnTipo, Color.FromArgb(ia, TipoServicio), tx, y + hTipo / 2f, tw);
                y += hTipo + gap;
                Renglon(g, ajuste, copia, medio, "Cargando\u2026", fnTitulo, Color.FromArgb(ia, Color.White), tx, y + hTit / 2f, tw);
                y += hTit + gap;
                Renglon(g, ajuste, copia, medio, "En tu m\u00fasica y en Spotify", fnSub, Color.FromArgb(ia, TextoSub), tx, y + hSub / 2f, tw);
            }
#endif
        }
    }
}
