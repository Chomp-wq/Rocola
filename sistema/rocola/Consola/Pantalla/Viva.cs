// LA PANTALLA VIVA DE XP, TANDAS 2 Y 3 (DiscoVivoGpu.cs y SuenaVivoGpu.cs, peticion del 24-09-2026), en lo esencial: el ecualizador de la
// que suena, el boleto que vuela a la cola, la moneda que cae, la tarjeta que invita cuando no hay creditos y los avisos que se entienden.
// Cada una con su interruptor de XP (encendidos de fabrica); el controlador apunta la hora de cada suceso y aqui se anima.
using System;
using System.Collections.Generic;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class VistaGrafica
    {
        private Zona zCola, zSuena, zSelResultado;
        private static int Edad(int tick) { return tick == 0 ? int.MaxValue : unchecked(Environment.TickCount - tick); }
        private static float Suave(float u) { u = Math.Max(0f, Math.Min(1f, u)); return u * u * (3f - 2f * u); }

        // Cuatro barritas que se mueven con la musica (el pulso que mide el motor a la salida).
        private bool eqRealAhora, cuboLuzAhora;
        private void Barritas(float x, float y, float w, float h, Color4 c)
        {
            if (eqRealAhora && BarritasReales(x, y, w, h, c)) return;   // V9
            double t = reloj.Elapsed.TotalSeconds;
            float[] base_ = { pGraves, pMedios, (pMedios + pAgudos) / 2, pAgudos };
            float bw = w / 4 - 2 * k;
            for (int i = 0; i < 4; i++)
            {
                float v = Math.Max(0.15f, Math.Min(1f, 0.25f + 0.9f * base_[i] + 0.18f * (float)Math.Sin(t * (7 + 3 * i) + i)));
                d.Rect(x + i * (bw + 2 * k), y + h * (1 - v), bw, h * v, c);
            }
        }

        // El saltito de la capsula de la cola cuando llega el boleto (px hacia arriba).
        private float Salto(Controlador c)
        {
            int e = Edad(c.VueloTick) - VueloMs;
            if (e < 0 || e > 320) return 0f;
            return 6 * k * (float)Math.Sin(e / 320.0 * Math.PI);
        }

        // ---- LOS AVISOS QUE SE ENTIENDEN (avisos_claros=): entran por la derecha, con su color y su icono segun lo que dicen, en dos
        // renglones, y una linea que va ensenando cuanto les queda. El tipo, por lo que dice (en Rocola OS los avisos no traen color).
        internal enum TipoAviso { Hecho, Cuidado, Error, Novedad }
        internal static TipoAviso TipoDe(string t)
        {
            string s = (t ?? "").ToLowerInvariant();
            if (s.StartsWith("reanudando") || s.StartsWith("carpetas actualizadas") || s.Contains("guardados") || s.Contains("configurados")) return TipoAviso.Novedad;
            if (s.StartsWith("no se pudo") || s.Contains("danada") || s.Contains("dañada") || s.Contains("error") || s.Contains("fallo")) return TipoAviso.Error;
            if (s.StartsWith("inserta") || s.Contains("ya va seguida") || s.StartsWith("no se puede") || s.Contains("no esta disponible") || s.Contains("espera")) return TipoAviso.Cuidado;
            return TipoAviso.Hecho;
        }
        private static Color4 ColorAviso(TipoAviso t)
        {
            return t == TipoAviso.Cuidado ? Color4.Hex(0xF2CE48) : t == TipoAviso.Error ? Color4.Hex(0xF06460) : t == TipoAviso.Novedad ? Color4.Hex(0x96C8FF) : Color4.Hex(0x78E6A5);
        }
        private static Color4 FondoAviso(TipoAviso t)
        {
            return t == TipoAviso.Cuidado ? Color4.Hex(0x342808, 244 / 255f) : t == TipoAviso.Error ? Color4.Hex(0x3E1212, 244 / 255f) : t == TipoAviso.Novedad ? Color4.Hex(0x0E1C3C, 244 / 255f) : Color4.Hex(0x0E3422, 244 / 255f);
        }
        // "No se pudo reproducir: credito devuelto" -> titulo y renglon (PartirAviso de XP, tal cual).
        internal static void PartirAviso(string texto, out string titulo, out string detalle)
        {
            string t = (texto ?? "").Trim();
            titulo = t; detalle = "";
            int p = t.IndexOf(": ", StringComparison.Ordinal);
            if (p <= 0 || p >= t.Length - 2) p = -1;
            int q = t.IndexOf(" - ", StringComparison.Ordinal);
            if (p < 0 && q > 0 && q < t.Length - 3) { titulo = t.Substring(0, q); detalle = t.Substring(q + 3); return; }
            if (p > 0) { titulo = t.Substring(0, p); detalle = t.Substring(p + 2); }
        }
        // EL AVISO CLARO (HorneaAviso, TamanoAviso, PintaAviso y CurvaAviso de XP): a la derecha, a 80 de arriba; entra desde la derecha
        // frenando (220 ms), se queda y se va hacia la derecha desvaneciendose (320 ms); un aro con su icono, el titulo (14 pt) y el renglon
        // (11 pt), y abajo la linea de lo que le queda.
        private void AvisoClaro(Controlador c, string texto, float W)
        {
            TipoAviso tipo = TipoDe(texto);
            string tit, det; PartirAviso(texto, out tit, out det);
            int e = Math.Max(0, Edad(c.AvisoDesde)), dura = c.AvisoDuraMs;
            if (e >= dura) return;
            float alfa = 1f, dx = 0f, corre = 64 * k;
            if (e < 220) { float q = e / 220f, ee = 1f - (1f - q) * (1f - q) * (1f - q); alfa = ee; dx = corre * (1f - ee); }
            else if (e > dura - 320) { float q = (e - (dura - 320)) / 320f; alfa = 1f - q; dx = corre * q * q; }
            Letra fTit = Ui(14 * 4f / 3f * k), fDet = Ui(11 * 4f / 3f * k);
            float hTit = fTit.Tam * 1.15f, hDet = det.Length > 0 ? fDet.Tam * 1.15f : 0;
            float alto = Math.Max(56 * k, 12 * k + hTit + (hDet > 0 ? 2 * k + hDet : 0) + 14 * k), icono = Math.Min(44 * k, alto - 22 * k);
            float tw = Math.Max(fTit.Mide(tit), hDet > 0 ? fDet.Mide(det) : 0);
            float max = Math.Max(360 * k, Math.Min(560 * k, W * .40f)), w = Math.Max(300 * k, Math.Min(max, 16 * k + icono + 14 * k + tw + 22 * k));
            float x = Math.Max(8 * k, W - 18 * k - w) + dx, y = 80 * k, rad = Math.Min(16 * k, alto * .26f);
            Color4 col = ColorAviso(tipo);
            if (c.motor.VidrioAvisos) Vidrio(x, y, w, alto, rad, col, alfa, true, 0.42f, 1.3f);   // G10
            else if (c.motor.AvisosVidrio && VidrioDetras(x, y, w, alto, alfa)) d.Redondo(x, y, w, alto, rad, FondoAviso(tipo).ConAlfa(0.55f * alfa));   // V24
            else d.Redondo(x, y, w, alto, rad, FondoAviso(tipo).ConAlfa(alfa));
            d.Contorno(x, y, w, alto, rad, 1.5f * k, col.ConAlfa(alfa));
            // el aro con su icono (los trazos del boceto de XP, en una caja de 24)
            float cx = x + 16 * k + icono / 2, cy = y + alto / 2 - 1 * k;
            Anillo(cx, cy, icono / 2, 2f * k, col.ConAlfa(alfa));
            float u = icono / 24f * 0.52f * (44f / 24f) * 24f / 44f, ox = cx - 12 * u, oy = cy - 12 * u, g = Math.Max(1.6f * k, 2.6f * u);
            Color4 ci = col.ConAlfa(alfa);
            if (tipo == TipoAviso.Hecho) { d.Linea(ox + 4 * u, oy + 12.5f * u, ox + 9.5f * u, oy + 18 * u, g, ci); d.Linea(ox + 9.5f * u, oy + 18 * u, ox + 20 * u, oy + 6 * u, g, ci); }
            else if (tipo == TipoAviso.Error) { d.Linea(ox + 7 * u, oy + 7 * u, ox + 17 * u, oy + 17 * u, g, ci); d.Linea(ox + 17 * u, oy + 7 * u, ox + 7 * u, oy + 17 * u, g, ci); }
            else if (tipo == TipoAviso.Cuidado) { d.Linea(ox + 12 * u, oy + 6 * u, ox + 12 * u, oy + 13.5f * u, g, ci); Circulo(ox + 12 * u, oy + 18 * u, 1.7f * u + g / 2, ci); }
            else { d.Linea(ox + 9 * u, oy + 18 * u, ox + 9 * u, oy + 6 * u, g, ci); d.Linea(ox + 9 * u, oy + 6 * u, ox + 20 * u, oy + 4 * u, g, ci); d.Linea(ox + 20 * u, oy + 4 * u, ox + 20 * u, oy + 16 * u, g, ci); Circulo(ox + 6.5f * u, oy + 18 * u, 2.5f * u, ci); Circulo(ox + 17.5f * u, oy + 16 * u, 2.5f * u, ci); }
            float xt = cx + icono / 2 + 14 * k, wt = Math.Max(20 * k, x + w - xt - 16 * k);
            float yt = y + (alto - (hTit + (hDet > 0 ? 2 * k + hDet : 0))) * .5f - 2 * k;
            fTit.Escribe(d, fTit.Ajusta(tit, wt), xt, yt, Blanco.ConAlfa(alfa));
            if (hDet > 0) fDet.Escribe(d, fDet.Ajusta(det, wt), xt, yt + hTit + 2 * k, new Color4(214 / 255f, 222 / 255f, 245 / 255f, alfa));
            // la linea de lo que le queda, por dentro del borde de abajo
            float vida = Math.Max(0f, 1f - e / (float)dura), lw = (w - 2 * rad) * vida;
            if (lw > 1) d.Rect(x + rad, y + alto - 4.5f * k, lw, 3 * k, col.ConAlfa(alfa));
        }

        // ---- LOS TEXTOS QUE SE DESLIZAN (textos_deslizantes= de XP): si un titulo no cabe, en vez de cortarlo con puntos suspensivos se
        // corre despacio (40 px/s) hasta el final, espera y vuelve; con el interruptor apagado, como siempre.
        private void EscribeDesliza(Controlador c, Letra l, string s, float x, float y, float ancho, Color4 col)
        {
            float w = l.Mide(s);
            if (!c.motor.TextosDeslizantes || w <= ancho) { l.Escribe(d, l.Ajusta(s, ancho), x, y, col); return; }
            float sobra = w - ancho, vel = 40 * k, pausa = 1.6f, viaje = sobra / vel, ciclo = 2 * (pausa + viaje);
            float t = (float)(reloj.Elapsed.TotalSeconds % ciclo), dx;
            if (t < pausa) dx = 0; else if (t < pausa + viaje) dx = (t - pausa) * vel; else if (t < 2 * pausa + viaje) dx = sobra; else dx = sobra - (t - 2 * pausa - viaje) * vel;
            d.Recorta(x, y - l.Tam * 0.3f, ancho, l.Tam * 1.6f);
            l.Escribe(d, s, x - dx, y, col);
            d.SinRecorte();
        }

        // ---- EL BUSCADOR (como el de XP, BuscadorPortadasGpu.cs): arriba, de lado a lado, la casilla de lo escrito con su lupa, el cursor y
        // cuantos resultados hay; debajo, los atajos; a la izquierda el teclado (la elegida en azul, BORRAR en rojo) y a la derecha las
        // tarjetas en dos columnas, con la portada (si buscador_portadas= esta encendido), que es (ARTISTA, ALBUM, CANCION), el nombre
        // con lo tecleado resaltado y un renglon que ayuda a elegir; abajo a la izquierda, la leyenda de los botones.
        private void BuscadorPortadas(Controlador c, float W, float abajo)
        {
            Form1 m = c.motor;
            bool portadas = c.ConPortadas;
            float x0 = 24 * k, y0 = 22 * k, hb = 56 * k, wb = W - 48 * k;
            d.Brillo(x0 + wb / 2, y0 + hb / 2, wb * 0.45f, AzulBorde.ConAlfa(0.10f));
            bool vidrio = m.VidrioBuscador;   // G7
            if (vidrio) Vidrio(x0, y0, wb, hb, 14 * k, Color4.Hex(0x6FA8FF), 1f, false, 0.30f, 1.3f);
            else d.RedondoDegradado(x0, y0, wb, hb, 14 * k, Color4.Hex(0x172352, 0.88f), Color4.Hex(0x0E1638, 0.88f));
            d.Contorno(x0, y0, wb, hb, 14 * k, 1.6f * k, Color4.Hex(0x6FA8FF, 0.85f));
            // LAS LETRAS DEL BUSCADOR DE XP (BuscadorPortadasGpu.cs: px por la escala min(W/1280, H/720), con su minimo)
            float sx = Math.Min(W / 1280f, sup.Alto / 720f);
            Letra fCaja = Titulo(Math.Max(14, 34 * sx)), fCuenta = Ui(Math.Max(10, 16 * sx)), fAtajo = Ui(Math.Max(10, 14 * sx)), fTecla = Ui(Math.Max(12, 24 * sx)), fTeclaPal = Ui(Math.Max(10, 20 * sx));
            Letra fTipo = Ui(Math.Max(9, 11 * sx)), fTitulo = Titulo(Math.Max(12, 17 * sx)), fSub = UiFina(Math.Max(10, 12 * sx)), fMarca = Ui(Math.Max(9, 11 * sx)), fAyuda = Ui(Math.Max(11, 15 * sx));
            // la lupa
            float lx = x0 + 32 * k, ly = y0 + hb / 2 - 2 * k, lr = 9 * k;
            Color4 cLupa = Color4.Hex(0xB4C8F5);   // la lupa de XP: fina y azulada
            Anillo(lx, ly, lr, 2f * k, cLupa);
            d.Linea(lx + lr * 0.7f, ly + lr * 0.7f, lx + lr * 1.55f, ly + lr * 1.55f, 2.6f * k, cLupa);
            float tx0 = x0 + 56 * k;
            if (c.Escrito.Length > 0)
            {
                string e = fCaja.Ajusta(EscritoMin(c.Escrito), wb - 260 * k);
                float we = fCaja.Escribe(d, e, tx0, y0 + hb / 2 - fCaja.Tam * 0.62f, Blanco);
                if ((reloj.ElapsedMilliseconds / 530) % 2 == 0) d.Rect(tx0 + we + 4 * k, y0 + 14 * k, 2.5f * k, hb - 28 * k, Color4.Hex(0x6FA8FF));
            }
            else
            {
                if ((reloj.ElapsedMilliseconds / 530) % 2 == 0) d.Rect(tx0, y0 + 14 * k, 2.5f * k, hb - 28 * k, Color4.Hex(0x6FA8FF));
                fCuenta.Escribe(d, "Escribe un artista, un disco o una canción", tx0 + 12 * k, y0 + hb / 2 - fCuenta.Tam * 0.6f, Color4.Hex(0x8C98C8));
            }
            // TextoCuenta de XP: se ensenan las 40 mejores, pero la casilla dice todas las coincidencias; sin ninguna, «Sin resultados»
            bool servicio = c.Resultados.Count > 0 && c.Resultados[0].Tipo == 3;
            int total = c.Escrito.Length > 0 && !Form1.EsCodigoDeServicio(c.Escrito) ? Math.Max(m.TotalUltimaBusqueda, c.Resultados.Count) : c.Resultados.Count;
            if (!servicio && (total > 0 || c.Escrito.Length > 0))
            {
                string cu = total <= 0 ? "Sin resultados" : total == 1 ? "1 resultado" : total.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture) + " resultados";
                fCuenta.Escribe(d, cu, x0 + wb - 20 * k - fCuenta.Mide(cu), y0 + hb / 2 - fCuenta.Tam * 0.6f, Color4.Hex(0xC8D2F2));
            }
            // los atajos
            float ay = y0 + hb + 12 * k, ah = 30 * k, axx = x0;
            for (int i = 0; i < c.Atajos.Count; i++)
            {
                string t = c.Atajos[i].Texto; float w = fAtajo.Mide(t) + 28 * k;
                if (axx + w > W - 24 * k) break;
                bool sel = !c.EnResultados && c.FilaTecla < 0 && c.ColTecla == i;
                if (sel) selTecla.Meta(new Zona(axx, ay, w, ah));
                if (vidrio) PildoraVidrio(axx, ay, w, ah, ah / 2, sel ? Color4.Hex(0x3A6CFF) : Color4.Hex(0x7F90D0), 1f, sel);
                else
                {
                    d.Redondo(axx, ay, w, ah, ah / 2, sel ? Color4.Hex(0x3A6CFF) : Color4.Hex(0x16204A, 0.85f));
                    d.Contorno(axx, ay, w, ah, ah / 2, 1.2f * k, sel ? Color4.Hex(0xB8D0FF) : Color4.Hex(0x7F90D0, 0.4f));
                }
                fAtajo.Escribe(d, t, axx + 14 * k, ay + ah / 2 - fAtajo.Tam * 0.6f, Blanco);
                axx += w + 8 * k;
            }
            // el teclado: diez teclas por fila; la fila de la Z, centrada; ESPACIO y BORRAR abajo, anchas
            float ty0 = Math.Max(153 * k, ay + (c.Atajos.Count > 0 ? ah + 16 * k : 0)), sep = 6 * k, tecla = 40 * k, ht = 38 * k, ty = ty0;
            float anchoTeclado = 10 * tecla + 9 * sep;
            TeclaPulsada(c);   // V16
            if (vidrio) Vidrio(x0 - 10 * k, ty0 - 10 * k, anchoTeclado + 20 * k, Controlador.FilasTeclado.Length * (ht + 8 * k) + 12 * k, 18 * k, Color4.Hex(0x6F8CFF), 1f, false, 0.26f);
            for (int f = 0; f < Controlador.FilasTeclado.Length; f++)
            {
                string fila = Controlador.FilasTeclado[f];
                bool ultima = f == Controlador.FilasTeclado.Length - 1;
                float tx = x0 + (ultima ? 0 : (anchoTeclado - (fila.Length * tecla + (fila.Length - 1) * sep)) / 2);
                for (int i = 0; i < fila.Length; i++)
                {
                    string et = Controlador.Etiqueta(fila[i]);
                    bool borrar = fila[i] == '\u0002';
                    float w = ultima ? (borrar ? anchoTeclado * 0.4f - sep / 2 : anchoTeclado * 0.6f - sep / 2) : tecla;
                    bool sel = !c.EnResultados && f == c.FilaTecla && i == c.ColTecla;
                    if (sel) selTecla.Meta(new Zona(tx, ty, w, ht));
                    Color4 fondo = sel ? Color4.Hex(0x3A6CFF) : borrar ? Color4.Hex(0x3A1424, 0.88f) : Color4.Hex(0x141C40, 0.85f);
                    Color4 borde = sel ? Color4.Hex(0xB8D0FF) : borrar ? Color4.Hex(0xB04458, 0.8f) : Color4.Hex(0x7F90D0, 0.30f);
                    if (sel) d.Brillo(tx + w / 2, ty + ht / 2, w * 0.9f, Color4.Hex(0x3A6CFF, 0.35f));
                    float hu = Hundida(c, f, i), enc = 2.5f * k * hu;   // V16: se hunde y brilla
                    if (hu > 0) { d.Brillo(tx + w / 2, ty + ht / 2, w * 1.2f, Dorado.ConAlfa(0.45f * hu)); fondo = Dibujo.Mezcla(fondo, Color4.Hex(0xC98A22), 0.7f * hu); borde = Dibujo.Mezcla(borde, Dorado, hu); }
                    if (vidrio && hu <= 0) PildoraVidrio(tx, ty, w, ht, 7 * k, sel ? Color4.Hex(0x3A6CFF) : borrar ? Color4.Hex(0xB04458) : Color4.Hex(0x7F90D0), 1f, sel);
                    else
                    {
                        d.Redondo(tx + enc, ty + enc, w - 2 * enc, ht - 2 * enc, 7 * k, fondo);
                        d.Contorno(tx + enc, ty + enc, w - 2 * enc, ht - 2 * enc, 7 * k, 1.2f * k, borde);
                    }
                    Letra lt = et.Length > 1 ? fTeclaPal : fTecla;
                    lt.EscribeCentrado(d, et, tx + w / 2, ty + ht / 2 - lt.Tam * 0.6f + enc * 0.5f, borrar && !sel ? Color4.Hex(0xFFB0BC) : Blanco);
                    tx += w + sep;
                }
                ty += ht + 8 * k;
            }
            zTeclado = new Zona(x0, ty0, anchoTeclado, ty - ty0);
            // las tarjetas
            float rx = x0 + anchoTeclado + 46 * k, rw = W - rx - 24 * k, ry = ty0, gap = 11 * k, cw = (rw - gap) / 2, ch = 84 * k;
            int filas = Math.Max(1, (int)((abajo - ry - 40 * k + gap) / (ch + gap)));
            zResultados = new Zona(rx, ry, rw, filas * (ch + gap));
            if (vidrio && c.Resultados.Count > 0) Vidrio(rx - 10 * k, ry - 10 * k, rw + 20 * k, Math.Min(filas, (c.Resultados.Count + 1) / 2) * (ch + gap) - gap + 20 * k, 18 * k, Color4.Hex(0x6F8CFF), 1f, false, 0.26f);
            if (c.Resultados.Count == 0)
            {   // DibujaSinCartas de XP: que hacer, dicho en el sitio de las tarjetas
                Letra fVacio = UiFina(Math.Max(11, 16 * sx));
                string vacio = c.Escrito.Length > 0 ? "Sin resultados para «" + EscritoMin(c.Escrito) + "»" : c.Atajos.Count > 0 ? "Escribe para buscar, o elige un atajo de arriba" : "Escribe para buscar";
                fVacio.Escribe(d, fVacio.Ajusta(vacio, rw), rx, ry + 8 * k, Color4.Hex(0xA8B4DC));
            }
            int filaSel = c.SelResultado / 2, filasTot = (c.Resultados.Count + 1) / 2;
            int desde = Math.Max(0, Math.Min(filaSel - filas / 2, filasTot - filas));
            string suena = m.Sonando;
            if (enColaCuenta != m.EnCola || !object.ReferenceEquals(enColaSuena, suena))
            {   // lo que va en la cola, rehecho solo cuando la cola o lo que suena cambian (no en cada cuadro)
                enColaCuenta = m.EnCola; enColaSuena = suena; enCola.Clear();
                foreach (Form1.EnColaVista q in m.ColaParaPantalla(64)) if (q.Ruta != null) enCola.Add(q.Ruta);
            }
            for (int fi = 0; fi < filas && desde + fi < filasTot; fi++)
                for (int co = 0; co < 2; co++)
                {
                    int i = (desde + fi) * 2 + co; if (i >= c.Resultados.Count) break;
                    Form1.Resultado r = c.Resultados[i];
                    float corre, ac = Cascada(c, i, out corre);   // V17
                    if (ac <= 0.01f) continue;
                    float cx = rx + co * (cw + gap) + corre, cy = ry + fi * (ch + gap);
                    bool sel = c.EnResultados && i == c.SelResultado;
                    if (ac < 1f) { d.Redondo(cx, cy, cw, ch, 9 * k, Color4.Hex(0x161E44, 0.5f * ac)); d.Contorno(cx, cy, cw, ch, 9 * k, 1.1f * k, Color4.Hex(0x7F90D0, 0.3f * ac)); continue; }
                    if (sel) { d.Brillo(cx + cw / 2, cy + ch / 2, cw * 0.65f, Color4.Hex(0x6FA8FF, 0.22f)); zSelResultado = new Zona(cx, cy, cw, ch); selTecla.Meta(zSelResultado); }
                    if (vidrio) { PildoraVidrio(cx, cy, cw, ch, 9 * k, sel ? Color4.Hex(0x6FA8FF) : Color4.Hex(0x7F90D0), 1f, sel); if (sel) d.Contorno(cx, cy, cw, ch, 9 * k, 2f * k, Color4.Hex(0xB8D0FF)); }
                    else
                    {
                        d.RedondoDegradado(cx, cy, cw, ch, 9 * k, sel ? Color4.Hex(0x2C3C78, 0.92f) : Color4.Hex(0x161E44, 0.88f), sel ? Color4.Hex(0x223064, 0.92f) : Color4.Hex(0x10173A, 0.88f));
                        d.Contorno(cx, cy, cw, ch, 9 * k, sel ? 2f * k : 1.1f * k, sel ? Color4.Hex(0xB8D0FF) : Color4.Hex(0x7F90D0, 0.30f));
                    }
                    float pl = ch - 16 * k, tx = cx + 12 * k;
                    if (portadas)
                    {
                        if (r.Tipo == 3) Circulo(cx + 8 * k + pl / 2, cy + 8 * k + pl / 2, pl / 2, Dorado);
                        else Portada(r.Portada, r.Texto, cx + 8 * k, cy + 8 * k, pl);
                        tx = cx + pl + 18 * k;
                    }
                    float tw = cx + cw - 10 * k - tx;
                    string tipo = r.Tipo == 0 ? "ARTISTA" : r.Tipo == 1 ? "ÁLBUM" : r.Tipo == 3 ? "SERVICIO" : r.Tipo == 5 ? "NO ESTÁ" : "CANCIÓN";
                    Color4 ct = r.Tipo == 0 ? Color4.Hex(0x8FB8FF) : r.Tipo == 1 ? Color4.Hex(0xFFC24D) : r.Tipo == 2 ? VerdeTexto : Dorado;
                    fTipo.Escribe(d, tipo, tx, cy + 14 * k, ct);
                    string pas = r.Ruta != null && string.Equals(r.Ruta, suena, StringComparison.OrdinalIgnoreCase) ? "SONANDO" : r.Ruta != null && enCola.Contains(r.Ruta) ? "EN COLA" : null;
                    float twd = tw;
                    if (pas != null) { float wp = micro.Mide(pas) + 12 * k; d.Redondo(cx + cw - wp - 8 * k, cy + 10 * k, wp, 17 * k, 8.5f * k, (pas == "SONANDO" ? Verde : Dorado).ConAlfa(0.22f)); micro.Escribe(d, pas, cx + cw - wp - 2 * k, cy + 11.5f * k, pas == "SONANDO" ? Verde : Dorado); }
                    Resaltado(fTitulo, r.Texto, c.Escrito, tx, cy + 31 * k, tw, sel);
                    if (!string.IsNullOrEmpty(r.Detalle)) Resaltado(fSub, r.Detalle, c.Escrito, tx, cy + 54 * k, twd, false, Color4.Hex(0xA8B4DC));
                }
            AroQueViaja(c, selTecla, 8 * k);   // M2: de tecla en tecla, de casilla en casilla y del teclado a los resultados
            if (filasTot > filas)
            {   // la barrita de cuanto hay
                float bh = filas * (ch + gap) - gap, hh = Math.Max(20 * k, bh * filas / filasTot), by = ry + (bh - hh) * desde / Math.Max(1, filasTot - filas);
                d.Redondo(W - 17 * k, ry, 3 * k, bh, 1.5f * k, Color4.Hex(0x2A3462, 0.7f)); d.Redondo(W - 17 * k, by, 3 * k, hh, 1.5f * k, Color4.Hex(0x8FB8FF));
            }
            // la leyenda de abajo
            float ly2 = abajo - 8 * k, lxx = x0 + 2 * k;
            Rombo(lxx + 8 * k, ly2, 8 * k); lxx += 22 * k; lxx += fAyuda.Escribe(d, "Mover", lxx, ly2 - fAyuda.Tam * 0.62f, Leyenda) + 16 * k;
            Boton(lxx + 8 * k, ly2, 8 * k, 'V'); lxx += 22 * k; lxx += fAyuda.Escribe(d, "Poner / Elegir", lxx, ly2 - fAyuda.Tam * 0.62f, Leyenda) + 16 * k;
            float zx = lxx;
            Boton(lxx + 8 * k, ly2, 8 * k, 'B'); lxx += 20 * k; lxx += fAyuda.Escribe(d, "+", lxx, ly2 - fAyuda.Tam * 0.62f, Leyenda) + 4 * k;
            float q2 = 16 * k; d.Redondo(lxx, ly2 - q2 / 2, q2, q2, 3 * k, Color4.Hex(0x2A0F18, 0.9f)); d.Contorno(lxx, ly2 - q2 / 2, q2, q2, 3 * k, 1.4f * k, Rojo);
            d.Triangulo(lxx + q2 / 2, ly2 - q2 * 0.28f, lxx + q2 * 0.78f, ly2 + q2 * 0.2f, lxx + q2 * 0.22f, ly2 + q2 * 0.2f, Rojo); lxx += q2 + 8 * k;
            lxx += fAyuda.Escribe(d, "Cerrar", lxx, ly2 - fAyuda.Tam * 0.62f, Leyenda);
            zBuscar = new Zona(zx - 4 * k, ly2 - 14 * k, lxx - zx + 8 * k, 28 * k);   // lo que cierra el buscador (la guia de la ayuda lo senala)
        }
        // El nombre con lo tecleado resaltado (sin distinguir mayusculas ni acentos): lo que coincide, sobre un fondo dorado, como en XP.
        private void Resaltado(Letra l, string texto, string buscado, float x, float y, float ancho, bool sel, Color4? color = null)
        {
            Color4 col = color ?? (sel ? Blanco : Color4.Hex(0xE6EAFA));
            string t = l.Ajusta(texto ?? "", ancho), b = (buscado ?? "").Trim();
            int i = b.Length > 0 ? Plano(t).IndexOf(Plano(b), StringComparison.Ordinal) : -1;
            if (i < 0 || i + b.Length > t.Length) { l.Escribe(d, t, x, y, col); return; }
            float x1 = x + l.Escribe(d, t.Substring(0, i), x, y, col);
            float wm = l.Mide(t.Substring(i, b.Length));
            d.Redondo(x1 - 1 * k, y - 1 * k, wm + 2 * k, l.Tam * 1.15f, 3 * k, Color4.Hex(0x8A6A34, 0.85f));
            float x2 = x1 + l.Escribe(d, t.Substring(i, b.Length), x1, y, Color4.Hex(0xFFE2A8));
            l.Escribe(d, t.Substring(i + b.Length), x2, y, col);
        }
        private readonly HashSet<string> enCola = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string escritoDe, escritoMin;
        private string EscritoMin(string e) { if (!object.ReferenceEquals(e, escritoDe)) { escritoDe = e; escritoMin = e.ToLowerInvariant(); } return escritoMin; }
        private int enColaCuenta = -1; private string enColaSuena;
        // Lo pasado a mayusculas sin acentos se recuerda (los mismos titulos en cada cuadro); se olvida todo al pasar de 1024.
        private static readonly Dictionary<string, string> planos = new Dictionary<string, string>(StringComparer.Ordinal);
        private static string Plano(string s)
        {
            string r;
            if (planos.TryGetValue(s, out r)) return r;
            if (planos.Count >= 1024) planos.Clear();
            return planos[s] = HazPlano(s);
        }
        private static string HazPlano(string s)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
            foreach (char ch in s.ToUpperInvariant())
                sb.Append(ch == 'Á' ? 'A' : ch == 'É' ? 'E' : ch == 'Í' ? 'I' : ch == 'Ó' ? 'O' : ch == 'Ú' || ch == 'Ü' ? 'U' : ch);
            return sb.ToString();
        }

        // ---- LAS PISTAS DEL ROJO (guia_rojo= de XP, GuiaRojoGpu.cs): con el ROJO mantenido en la pared se encienden la barra de letras
        // (con una flecha a cada lado: ROJO + IZQ/DER cambia de letra) y las casillas BUSCAR y AYUDA, con su flecha llena de rojo vivo y un
        // brillo que late (peticion del 12-10-2026: que se resalten las que ya tienen en su contenedor, sin flechitas sueltas).
        private void GuiaRojo(Controlador c)
        {
            float late = 0.6f + 0.4f * (float)Math.Sin(reloj.Elapsed.TotalSeconds * 6);
            Color4 oro = Color4.Hex(0xFFD65E);
            if (!zLetras.Vacia && c.dentro == null && !c.Buscando)
            {
                Marco(zLetras, oro.ConAlfa(0.9f), 2 * k);
                float h = Math.Min(30 * k, zLetras.H * 0.66f), w = 18 * k, cy = zLetras.Cy;
                float xi = zLetras.X - 4 * k, xd = zLetras.X + zLetras.W + 4 * k;
                d.Triangulo(xi - w, cy, xi, cy - h / 2, xi, cy + h / 2, oro.ConAlfa(late));
                d.Triangulo(xd + w, cy, xd, cy - h / 2, xd, cy + h / 2, oro.ConAlfa(late));
            }
            foreach (Zona z in new[] { zBuscar, zAyuda })
            {
                if (z.Vacia) continue;
                d.Redondo(z.X - 2 * k, z.Y - 2 * k, z.W + 4 * k, z.H + 4 * k, z.H / 2 + 2 * k, oro.ConAlfa(0.35f * late));
                bool busca = z.X == zBuscar.X;
                float r = 9 * k, tx = z.X + z.W - 29 * k, cy = z.Cy;   // la tecla de flecha de la casilla (la misma cuenta que Casilla)
                d.Brillo(tx, cy, r * 2.6f, Rojo.ConAlfa(0.6f * late));
                Circulo(tx, cy, r + 1.5f * k, Color4.Hex(0xFFE6E6));
                Circulo(tx, cy, r, Rojo);
                float a = r * 0.62f;
                if (busca) d.Triangulo(tx, cy - a, tx + a, cy + a * 0.6f, tx - a, cy + a * 0.6f, Blanco);
                else d.Triangulo(tx - a, cy - a * 0.6f, tx + a, cy - a * 0.6f, tx, cy + a, Blanco);
            }
        }

        // ---- LO DE ENCIMA: la moneda que cae, la tarjeta que invita y el boleto que vuela ----
        internal const int MonedaCaeMs = 650, MonedaGolpeMs = 300, MonedaSumaMs = 1300, InvitaMs = 4200, VueloMs = 620;
        private int vueloVisto; private Zona vueloDesde; private string vueloTitulo = "";
        private void Efectos(Controlador c, float W, float H)
        {
            Form1 m = c.motor;
            Chispas();   // V18, V23
            DibujaDestello();   // C10
            if (m.GuiaRojo && c.RojoMantenido && !c.AyudaAbierta && c.AjusteSel < 0) GuiaRojo(c);
            // LA MONEDA QUE CAE: una moneda dorada girando hasta la capsula de Creditos; la capsula da un golpe con un destello verde y
            // sube "+4 creditos".
            int em = Edad(c.MonedaTick);
            if (m.MonedaCae && em < MonedaCaeMs + MonedaSumaMs && !zCreditos.Vacia)
            {
                if (em < MonedaCaeMs)
                {
                    float u = em / (float)MonedaCaeMs, cae = u * u;   // cae acelerando
                    if (m.MonedaMetal) cae = u < 0.8f ? (u / 0.8f) * (u / 0.8f) : 1f - 0.10f * (float)Math.Sin((u - 0.8f) / 0.2f * Math.PI);   // V22: y un rebote
                    float cx = zCreditos.Cx, cy = -30 * k + (zCreditos.Cy + 30 * k) * cae, r = 16 * k;
                    float giro = Math.Abs((float)Math.Cos(em / 1000.0 * 14));
                    if (m.MonedaMetal) { MonedaDeMetal(cx, cy, r, giro); goto monedaHecha; }
                    d.Brillo(cx, cy, r * 2.2f, Dorado.ConAlfa(0.35f));
                    float rx = Math.Max(2 * k, r * giro);
                    d.Redondo(cx - rx, cy - r, 2 * rx, 2 * r, rx, Color4.Hex(0xE0A93A));
                    d.Redondo(cx - rx * 0.75f, cy - r * 0.75f, 1.5f * rx, 1.5f * r, rx * 0.75f, Color4.Hex(0xFFD45A));
                monedaHecha:;
                }
                else
                {
                    int eg = em - MonedaCaeMs;
                    if (eg < MonedaGolpeMs) d.Brillo(zCreditos.Cx, zCreditos.Cy, zCreditos.W * 0.8f, Verde.ConAlfa(0.5f * (1f - eg / (float)MonedaGolpeMs)));
                    // "+4 creditos" (SueltaMoneda y DibujaMoneda de XP): sale encima de la capsula, a la derecha del numero, sube 46 px
                    // frenando y se desvanece; verde con un reborde oscuro fino
                    float u = Math.Min(1f, eg / 1100f);
                    string mas = "+" + c.MonedaSuma + (c.MonedaSuma == 1 ? " crédito" : " créditos");
                    Letra fm = Ui(Math.Max(11 * k, zCreditos.H * .40f));
                    float tw = fm.Mide(mas) + 24 * k, th = fm.Tam * 1.15f + 14 * k;
                    float mx = Math.Min(W - tw - 6 * k, zCreditos.X + zCreditos.W * 0.62f), myy = zCreditos.Y - th * .6f - 46 * k * (1f - (1f - u) * (1f - u));
                    float al = u < .08f ? u / .08f : u > .6f ? (1f - u) / .4f : 1f;
                    if (u < 1f)
                    {
                        float tx = mx + 12 * k, ty = myy + 7 * k;
                        for (int ox = -1; ox <= 1; ox++) for (int oy = -1; oy <= 1; oy++) if (ox != 0 || oy != 0) fm.Escribe(d, mas, tx + ox * 1.2f, ty + oy * 1.2f, new Color4(6 / 255f, 30 / 255f, 14 / 255f, 150 / 255f * al));
                        fm.Escribe(d, mas, tx, ty, new Color4(120 / 255f, 240 / 255f, 140 / 255f, al));
                    }
                }
            }
            // SIN CREDITOS, INVITAR: una tarjeta amable con la ranura y una moneda entrando; se va sola.
            int ei = Edad(c.InvitaTick);
            if (m.SinCreditosInvita && ei < InvitaMs)
            {
                float alfa = Math.Min(Suave(ei / 250f), 1f - Suave((ei - (InvitaMs - 400)) / 400f));
                float w = Math.Min(W - 80 * k, 560 * k), h = 150 * k, x = (W - w) / 2, y = H * 0.32f + 12 * k * (1f - alfa);
                d.Redondo(x - 2 * k, y - 2 * k, w + 4 * k, h + 4 * k, 20 * k, Dorado.ConAlfa(0.7f * alfa));
                d.Redondo(x, y, w, h, 18 * k, Color4.Hex(0x16224A, alfa));
                // la ranura y la moneda que entra (y vuelve a entrar)
                float rx = x + 34 * k, ry = y + 40 * k, rw = 70 * k;
                float ciclo = (ei % 1400) / 1400f, baja = Suave(ciclo / 0.7f);
                float my = ry - 40 * k + 46 * k * baja;
                float va = alfa * (1f - Suave((ciclo - 0.75f) / 0.25f));
                d.Brillo(rx + rw / 2, my + 16 * k, 26 * k, Dorado.ConAlfa(0.3f * va));
                d.Redondo(rx + rw / 2 - 16 * k, my, 32 * k, 32 * k, 16 * k, Color4.Hex(0xE0A93A, va));
                d.Redondo(rx + rw / 2 - 12 * k, my + 4 * k, 24 * k, 24 * k, 12 * k, Color4.Hex(0xFFD45A, va));
                d.Rect(rx - 4 * k, ry + 6 * k, rw + 8 * k, 44 * k, Color4.Hex(0x16224A, alfa));   // tapa lo que ya entro por la ranura
                d.Redondo(rx, ry, rw, 12 * k, 6 * k, Color4.Hex(0x05081A, alfa));
                float tx = rx + rw + 26 * k, tw = x + w - tx - 20 * k;
                grande.Escribe(d, "Te falta un crédito", tx, y + 26 * k, Dorado.ConAlfa(alfa));
                string q = string.IsNullOrEmpty(c.InvitaTitulo) ? "Inserta una moneda para poner tu canción" : "Inserta una moneda para poner «" + c.InvitaTitulo + "»";
                var lineas = Envuelve(q, media, tw);
                for (int i = 0; i < lineas.Count && i < 2; i++) media.Escribe(d, lineas[i], tx, y + 72 * k + i * 30 * k, Blanco.ConAlfa(alfa));
            }
            // VER ADONDE VA: el boleto sale de la fila y vuela hasta la capsula de la cola (o a lo que suena, si no sonaba nada).
            if (c.VueloTick != vueloVisto)
            {
                vueloVisto = c.VueloTick;
                vueloDesde = c.VueloDesdeBuscador ? zSelResultado : zCancion;
                vueloTitulo = c.VueloTitulo ?? "";
                Zona destino = c.VueloASuena ? zSuena : zCola;
                if (m.DestelloPedir && !vueloDesde.Vacia) Destello(vueloDesde.Cx, vueloDesde.Cy, c.dentro != null ? ColorDeDisco(m, c.dentro, false) : vivo1);   // C10
                if (m.ChispasPedir && !vueloDesde.Vacia && !destino.Vacia)   // V23: chispas del color del genero hasta la cola
                {
                    Color4 cc = m.ColorGenero && m.ColorSonando != 0 ? Color4.Hex(m.ColorSonando) : Dorado;
                    Suelta(vueloDesde.Cx, vueloDesde.Cy, 36, Dibujo.Mezcla(cc, Dorado, 0.4f), 220 * k, 0.9f, 3f * k, true, destino.Cx, destino.Cy);
                }
            }
            int ev = Edad(c.VueloTick);
            if (m.VueloCola && ev < VueloMs && !vueloDesde.Vacia)
            {
                Zona dest = c.VueloASuena ? zSuena : zCola;
                if (!dest.Vacia)
                {
                    float u = Suave(ev / (float)VueloMs);
                    float sx = vueloDesde.Cx, sy = vueloDesde.Cy, ex = dest.Cx, ey = dest.Cy;
                    float px = sx + (ex - sx) * u, py = sy + (ey - sy) * u - (float)Math.Sin(u * Math.PI) * 120 * k;   // en arco
                    float bw = (1f - 0.6f * u) * Math.Min(260 * k, vueloDesde.W), bh = (1f - 0.5f * u) * 34 * k;
                    d.Brillo(px, py, bw * 0.6f, Dorado.ConAlfa(0.3f));
                    d.Redondo(px - bw / 2, py - bh / 2, bw, bh, bh / 2, Color4.Hex(0xFFD45A));
                    if (u < 0.6f) chica.EscribeCentrado(d, chica.Ajusta(vueloTitulo, bw - 16 * k), px, py - chica.Tam * 0.55f, Tinta.ConAlfa(1f - u / 0.6f));
                }
            }
        }
    }
}
