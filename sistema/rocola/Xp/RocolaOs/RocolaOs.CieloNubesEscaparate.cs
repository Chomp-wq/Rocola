// EL CIELO VIVO DE ROCOLA OS, GRUPO «NUBES Y CLIMA»: CL5 (LA PASTILLA DEL CLIMA Y SU MENSAJE EN EL ESCAPARATE). PLAN_CIELO_VIVO.md §1 (regla 4: lo
// vistoso va al escaparate) y §3; FASE11_cielo-clima.md. Solo la tarjeta. Clave clima_escaparate= (funcion clima); necesita el clima sabido.
//
// LA PASTILLA «hora · °C · tiempo» («7:45 p. m. · 24 °C · Lluvia», con un iconito del tiempo delante): una capsula oscura translucida con la letra
// de los mensajes del escaparate (Trebuchet MS en negrita) arriba a la derecha (a 0,05 del ancho del borde y 0,06 del alto), donde el escaparate
// del disco no pone nada (el cubo y la ficha empiezan hacia el 0,22 del alto); con la lista de lo mas pedido (el cubo se va a esa esquina) se apaga
// con ella (AtraccionGpu: UiAlfa). Se hornea en Prepara (fuera del cuadro) solo cuando cambia el texto (cada minuto, o cuando llega otro clima) y solo
// con el escaparate a la vista; aparece y se va con un fundido de 0,6 s. Se pinta tras el escaparate (capa Escaparate: g135), encima de todo lo
// del cielo y debajo de la interfaz. La hora es la del cielo de la foto (la del bar, o la de «Ver como se veria»).
// EL MENSAJE: «Afuera: 24 °C y lluvia. ¡Pon música y que llueva!» (uno por tipo de clima), entre los que XP turna cada 5 s bajo la ficha (el
// mensajero g136: tercero, tras «Una moneda = ...»), con la misma letra y la misma forma. Se arma al entrar la atraccion: el clima que haya en
// ese momento.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    internal static class RocolaOsCieloClimaTextos
    {
        // el nombre del tiempo, con mayuscula
        internal static string Tiempo(RocolaOsClimaTipo t)
        {
            switch (t)
            {
                case RocolaOsClimaTipo.Despejado: return "Despejado"; case RocolaOsClimaTipo.PocoNublado: return "Poco nublado"; case RocolaOsClimaTipo.Nublado: return "Nublado";
                case RocolaOsClimaTipo.Neblina: return "Neblina"; case RocolaOsClimaTipo.Llovizna: return "Llovizna"; case RocolaOsClimaTipo.Lluvia: return "Lluvia";
                case RocolaOsClimaTipo.Aguacero: return "Aguacero"; case RocolaOsClimaTipo.Tormenta: return "Tormenta"; case RocolaOsClimaTipo.Nieve: return "Nieve";
                default: return "";
            }
        }
        // «7:45 p. m.» (12 horas, como se dice en Mexico)
        internal static string Hora(DateTime t)
        {
            int h = t.Hour % 12; if (h == 0) h = 12;
            return h.ToString(CultureInfo.InvariantCulture) + ":" + t.Minute.ToString("00", CultureInfo.InvariantCulture) + (t.Hour >= 12 ? " p. m." : " a. m.");
        }
        // «24 °C» (entero; negativos con el signo menos), o "" si no se sabe
        internal static string Grados(float c)
        {
            if (float.IsNaN(c) || float.IsInfinity(c)) return "";
            int g = (int)Math.Round(c, MidpointRounding.AwayFromZero);
            return g.ToString(CultureInfo.InvariantCulture) + " °C";
        }
        // el texto de la pastilla: «7:45 p. m. · 24 °C · Lluvia» (sin lo que no se sepa)
        internal static string Pastilla(DateTime hora, RocolaOsClima c)
        {
            string g = Grados(c.Temperatura);
            return Hora(hora) + (g.Length > 0 ? " · " + g : "") + " · " + Tiempo(c.Tipo);
        }
        // el mensaje del escaparate (corto: cabe en la columna junto al cubo; XP lo acorta por palabras si no)
        internal static string Mensaje(RocolaOsClima c) { return Mensaje(c, c.DeDia); }
        // (deDia: el despejado de noche no pide «sol»)
        internal static string Mensaje(RocolaOsClima c, bool deDia)
        {
            string g = Grados(c.Temperatura), de = g.Length > 0 ? " (" + g + ")" : "";
            switch (c.Tipo)
            {
                case RocolaOsClimaTipo.Despejado:
                    return deDia ? "Afuera: " + (g.Length > 0 ? g + " y " : "") + "despejado. ¡Pon algo para el sol!"
                                 : "Afuera: " + (g.Length > 0 ? g + " y " : "") + "noche despejada. ¡Pon tu favorita!";
                case RocolaOsClimaTipo.PocoNublado: return "Afuera" + de + ", poco nublado. ¡Pon una canción!";
                case RocolaOsClimaTipo.Nublado: return "Afuera está nublado" + de + ". ¡Ponle música!";
                case RocolaOsClimaTipo.Neblina: return "Afuera hay neblina" + de + ". Aquí suena mejor";
                case RocolaOsClimaTipo.Llovizna: return "Afuera llovizna" + de + ". ¡Una canción para el rato!";
                case RocolaOsClimaTipo.Lluvia: return "Afuera llueve" + de + ". ¡Pon música y que llueva!";
                case RocolaOsClimaTipo.Aguacero: return "¡Aguacero afuera! Aquí adentro hay música";
                case RocolaOsClimaTipo.Tormenta: return "Hay tormenta afuera. ¡Aquí suena mejor!";
                case RocolaOsClimaTipo.Nieve: return "Afuera nieva" + de + ". ¡Una para el frío!";
                default: return null;
            }
        }
    }

    // CL5: EL MENSAJE (g136)
    internal sealed class RocolaOsCieloClimaMensajero : RocolaOsCieloMensajero
    {
        internal RocolaOsCieloClimaMensajero() { Quien = "CL5 el mensaje del clima"; Orden = RocolaOsCieloOrden.Clima; }
        internal override void Pon(List<string> mensajes, RocolaOsCieloFoto f)
        {
            if (!f.CL5 || !f.ClimaSabido) return;
            string m = RocolaOsCieloClimaTextos.Mensaje(f.Clima, f.SolAlt > 0);   // (de dia o de noche por el sol del lugar)
            if (string.IsNullOrEmpty(m)) return;
            mensajes.Insert(Math.Min(2, mensajes.Count), m);   // tras «Elige tu cancion» y «Una moneda = ...»
        }
    }

    // CL5: LA PASTILLA
    internal sealed class RocolaOsCieloClimaPastilla : RocolaOsCieloPintor
    {
        internal RocolaOsCieloClimaPastilla() { Quien = "CL5 la pastilla del clima"; Orden = RocolaOsCieloOrden.Clima; }
        private IntPtr tex; private float u, v; private int bw, bh, alto; private string texto = ""; private GpuStarfield de;
        private float alfa;   // el fundido propio (0 a 1)
        private readonly Tarjeta.Vtx[] quad = new Tarjeta.Vtx[6];

        internal override void Prepara(GpuPanel p)
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            GpuStarfield st = p.RocolaOsEstrellas;
            bool quiere = f != null && f.CL5 && f.ClimaSabido && st != null && p.RocolaOsEscaparateConFicha;
            RocolaOsCieloEscena e = RocolaOsCielo.Escena;
            float meta = quiere ? 1f : 0f;
            alfa += (meta - alfa) * (float)(1.0 - Math.Exp(-e.Dt / 0.2));   // ~0,6 s
            if (Math.Abs(alfa - meta) < 0.004f) alfa = meta;
            if (!quiere) return;
            string t = RocolaOsCieloClimaTextos.Pastilla(f.Local, f.Clima);
            int h = e.Alto;
            if (tex != IntPtr.Zero && ReferenceEquals(de, st) && t == texto && h == alto) return;
            Hornea(st, t, f.Clima, f.SolAlt > -1.0, h);
        }
        private void Hornea(GpuStarfield st, string t, RocolaOsClima c, bool dia, int h)
        {
            SKBitmap bmp = Dibuja(t, c, dia, h);
            int w = bmp.Width, hh = bmp.Height;
            if (tex != IntPtr.Zero && ReferenceEquals(de, st) && bw == w && bh == hh)
            { if (!st.UpdateOverlay(tex, bmp, out u, out v)) { Tarjeta.Suelta(tex); tex = IntPtr.Zero; } }
            else if (tex != IntPtr.Zero) { if (ReferenceEquals(de, st)) Tarjeta.Suelta(tex); tex = IntPtr.Zero; }
            if (tex == IntPtr.Zero) tex = st.CreateOverlay(bmp, out u, out v);
            bmp.Dispose();
            de = st; bw = w; bh = hh; texto = t; alto = h;
        }
        // LA IMAGEN de la pastilla (transparente fuera de la capsula) para una pantalla de alto h; la prueba tambien la pide (--prueba-cielo-clima --pastilla=RUTA)
        internal static SKBitmap Dibuja(string t, RocolaOsClima c, bool dia, int h)
        {
            float px = Math.Max(12f, h * 0.0225f);
            using (Rocola.Nativo.Letra fn = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), px, true))
            using (SKPaint pincel = new SKPaint { Color = new SKColor(255, 255, 255, 255) })
            {
                AjusteLetra ajuste = AjusteLetra.SuaveAjustada;
                FormatoTexto sf = new FormatoTexto(); sf.Banderas = BanderasTexto.SinPartir; sf.Vertical = Alinea.Centro; sf.Corte = CorteTexto.PuntosPorLetra;
                float tw = (float)Math.Ceiling(TextoGdiPlus.Mide(t, fn, ajuste, SizeF.Empty, sf).Width);
                float ico = px * 1.35f, pad = px * 0.85f, gap = px * 0.55f, alt = px * 2.05f;
                int w = (int)Math.Ceiling(pad + ico + gap + tw + pad), hh = (int)Math.Ceiling(alt);
                SKBitmap bmp = Mapas.Nuevo(w, hh);
                using (SKCanvas g = new SKCanvas(bmp))
                {
                    g.Clear(SKColors.Transparent);
                    float r = hh / 2f;
                    SKRect rc = new SKRect(0.75f, 0.75f, w - 0.75f, hh - 0.75f);
                    using (SKPaint fondo = new SKPaint { IsAntialias = true, Color = new SKColor(10, 16, 34, 176) }) g.DrawRoundRect(rc, r, r, fondo);
                    using (SKPaint borde = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, Color = new SKColor(150, 185, 235, 110) }) g.DrawRoundRect(rc, r, r, borde);
                    Icono(g, c, dia, pad + ico / 2f, hh / 2f, ico);
                    TextoGdiPlus.Escribe(g, t, fn, ajuste, pincel, new RectangleF(pad + ico + gap, 0, tw + 4f, hh), sf);
                }
                return bmp;
            }
        }
        // EL ICONITO del tiempo (de dia o de noche), de unos 'tam' px, centrado en (cx, cy)
        internal static void Icono(SKCanvas g, RocolaOsClima c, bool dia, float cx, float cy, float tam)
        {
            float s = tam * 0.5f;
            SKColor nube = new SKColor(206, 216, 232), oscura = new SKColor(150, 160, 182), azul = new SKColor(140, 190, 255), oro = new SKColor(255, 214, 102);
            using (SKPaint rel = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill })
            using (SKPaint raya = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1.4f, tam * 0.09f), StrokeCap = SKStrokeCap.Round })
            {
                switch (c.Tipo)
                {
                    case RocolaOsClimaTipo.Despejado:
                        if (dia)
                        {
                            rel.Color = oro; g.DrawCircle(cx, cy, s * 0.46f, rel); raya.Color = oro;
                            for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; g.DrawLine(cx + (float)Math.Cos(a) * s * 0.68f, cy + (float)Math.Sin(a) * s * 0.68f, cx + (float)Math.Cos(a) * s * 0.95f, cy + (float)Math.Sin(a) * s * 0.95f, raya); }
                        }
                        else
                        {
                            rel.Color = new SKColor(232, 236, 255); g.DrawCircle(cx, cy, s * 0.62f, rel);
                            rel.Color = new SKColor(10, 16, 34); g.DrawCircle(cx + s * 0.34f, cy - s * 0.18f, s * 0.54f, rel);
                        }
                        break;
                    case RocolaOsClimaTipo.PocoNublado:
                        if (dia) { rel.Color = oro; g.DrawCircle(cx - s * 0.28f, cy - s * 0.3f, s * 0.42f, rel); }
                        else { rel.Color = new SKColor(232, 236, 255); g.DrawCircle(cx - s * 0.28f, cy - s * 0.3f, s * 0.36f, rel); }
                        Nube(g, rel, nube, cx + s * 0.1f, cy + s * 0.18f, s * 0.9f); break;
                    case RocolaOsClimaTipo.Nublado: Nube(g, rel, nube, cx, cy, s); break;
                    case RocolaOsClimaTipo.Neblina:
                        raya.Color = nube; for (int i = -1; i <= 1; i++) g.DrawLine(cx - s * 0.8f + (i == 0 ? s * 0.2f : 0f), cy + i * s * 0.45f, cx + s * 0.8f - (i == 0 ? 0f : s * 0.2f), cy + i * s * 0.45f, raya); break;
                    case RocolaOsClimaTipo.Nieve:
                        Nube(g, rel, nube, cx, cy - s * 0.2f, s * 0.9f); rel.Color = SKColors.White;
                        g.DrawCircle(cx - s * 0.4f, cy + s * 0.7f, s * 0.12f, rel); g.DrawCircle(cx, cy + s * 0.85f, s * 0.12f, rel); g.DrawCircle(cx + s * 0.4f, cy + s * 0.7f, s * 0.12f, rel); break;
                    case RocolaOsClimaTipo.Tormenta:
                        Nube(g, rel, oscura, cx, cy - s * 0.2f, s * 0.9f);
                        using (SKPath rayo = new SKPath())
                        {
                            rayo.MoveTo(cx + s * 0.1f, cy + s * 0.15f); rayo.LineTo(cx - s * 0.22f, cy + s * 0.62f); rayo.LineTo(cx - s * 0.02f, cy + s * 0.62f);
                            rayo.LineTo(cx - s * 0.18f, cy + s * 1.0f); rayo.LineTo(cx + s * 0.28f, cy + s * 0.46f); rayo.LineTo(cx + s * 0.06f, cy + s * 0.46f); rayo.Close();
                            rel.Color = oro; g.DrawPath(rayo, rel);
                        }
                        break;
                    default:   // llovizna, lluvia, aguacero
                        Nube(g, rel, oscura, cx, cy - s * 0.25f, s * 0.9f); raya.Color = azul;
                        for (int i = -1; i <= 1; i++) g.DrawLine(cx + i * s * 0.42f, cy + s * 0.45f, cx + i * s * 0.42f - s * 0.12f, cy + s * 0.85f, raya);
                        break;
                }
            }
        }
        private static void Nube(SKCanvas g, SKPaint rel, SKColor color, float cx, float cy, float s)
        {
            rel.Color = color;
            g.DrawCircle(cx - s * 0.38f, cy + s * 0.12f, s * 0.36f, rel); g.DrawCircle(cx + s * 0.04f, cy - s * 0.12f, s * 0.46f, rel); g.DrawCircle(cx + s * 0.44f, cy + s * 0.14f, s * 0.34f, rel);
            g.DrawRect(new SKRect(cx - s * 0.38f, cy + s * 0.1f, cx + s * 0.44f, cy + s * 0.48f), rel);
        }
        internal override void Pinta(GpuPanel p, int capa)
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (f == null || alfa <= 0.004f || tex == IntPtr.Zero) return;
            float a = p.RocolaOsEscaparateAlfa(true);
            if (a <= 0.004f) return;
            GpuStarfield st = p.RocolaOsEstrellas; if (st == null || !ReferenceEquals(st, de)) return;
            RocolaOsCieloEscena e = RocolaOsCielo.Escena;
            float x = e.Ancho * 0.0225f, y = e.Alto - e.Alto * 0.026f - bh;   // abajo a la izquierda, como en la presentacion (el escaparate no pone nada ahi)
            uint al = (uint)Math.Max(0, Math.Min(255, (int)(255f * alfa * a + 0.5f)));
            p.RocolaOsPastillaQuad(quad, x, y, x + bw, y + bh, u, v, (al << 24) | 0x00FFFFFFu);
            st.DrawQuad(tex, quad);
        }
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra()
        {
            RocolaOsCielo.PonPintor(RocolaOsCieloCapa.Escaparate, new RocolaOsCieloClimaPastilla());
            RocolaOsCielo.PonMensajero(new RocolaOsCieloClimaMensajero());
        }
    }

    internal sealed partial class GpuPanel
    {
        // el escaparate del disco a la vista (con su ficha): la pastilla solo sale con el (no en la atraccion de siempre)
        internal bool RocolaOsEscaparateConFicha { get { return atrCuadro.Has && atrCuadro.Vitrina; } }
        // lo visible que esta el escaparate (como los textos de XP: la escena, los controles, la lista de lo mas pedido que lo quita)
        internal float RocolaOsEscaparateAlfa() { return RocolaOsEscaparateAlfa(false); }
        // siempre: la pastilla del clima no se apaga cuando sale la lista de lo mas pedido (UiAlfa): abajo a la izquierda no tapa nada
        internal float RocolaOsEscaparateAlfa(bool siempre)
        {
            if (!atrCuadro.Has || !atrCuadro.Vitrina) return 0f;
            return Math.Max(0f, Math.Min(1f, atrCuadro.VitrinaA)) * (siempre ? 1f : Math.Max(0f, Math.Min(1f, atrCuadro.UiAlfa))) * SceneAlpha() * ControlsOpacity;
        }
        internal void RocolaOsPastillaQuad(Tarjeta.Vtx[] q, float x0, float y0, float x1, float y1, float u, float v, uint argb) { SetTexQuadA(q, x0, y0, x1, y1, u, v, argb); }
    }
}
