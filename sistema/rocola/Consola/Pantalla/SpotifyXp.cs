// SPOTIFY EN LAS PORTADAS (7-11-2026, Motor/SpotifyPortadas.cs): la marquita verde de los discos de Spotify en las letras (P1) y, dentro de
// un disco, la tira «Más de este artista en Spotify» debajo de la portada (P2).
using System;
using System.Collections.Generic;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class VistaGrafica
    {
        private static readonly Color4 VerdeSpotify = Color4.Hex(0x1DB954);

        // La marquita: un circulo verde con las tres ondas, con un filo oscuro para que se vea sobre cualquier portada.
        private void MarcaSpotify(float cx, float cy, float r)
        {
            Circulo(cx, cy, r + 1.5f * k, new Color4(0, 0, 0.04f, 0.55f));
            Circulo(cx, cy, r, VerdeSpotify);
            for (int j = 0; j < 3; j++)
            {
                float rr = r * (1.05f - 0.25f * j), oy = cy + r * (0.55f + 0.08f * j), g = r * (0.17f - 0.025f * j);
                const int n = 8; double a0 = Math.PI * 1.22, a1 = Math.PI * 1.78;
                for (int i = 0; i < n; i++)
                {
                    double t0 = a0 + (a1 - a0) * i / n, t1 = a0 + (a1 - a0) * (i + 1) / n;
                    d.Linea(cx + rr * (float)Math.Cos(t0), oy + rr * (float)Math.Sin(t0), cx + rr * (float)Math.Cos(t1), oy + rr * (float)Math.Sin(t1), g, new Color4(0.02f, 0.03f, 0.05f, 1f));
                }
            }
        }

        // La tira, en el rectangulo (x, y, w, h): el letrero, las portadas (la elegida, resaltada) y el nombre de la elegida (o como llegar).
        private void TiraMas(Controlador c, List<Form1.Disco> mas, float x, float y, float w, float h)
        {
            Form1 m = c.motor;
            float r0 = 7 * k;
            MarcaSpotify(x + r0, y + mini.Tam * 0.55f, r0);
            // (los textos se arman solo cuando cambia lo elegido o el ancho: no en cada cuadro)
            int sel = c.cursorMas, n = mas.Count;
            if (!object.ReferenceEquals(tiraArtista, c.MasArtista) || tiraSel != sel || tiraN != n || tiraW != w || !object.ReferenceEquals(tiraDisco, sel >= 0 && sel < n ? mas[sel] : null))
            {
                tiraArtista = c.MasArtista; tiraSel = sel; tiraN = n; tiraW = w; tiraDisco = sel >= 0 && sel < n ? mas[sel] : null;
                tiraTit = mini.Ajusta("MÁS DE " + (c.MasArtista ?? "ESTE ARTISTA").ToUpperInvariant() + " EN SPOTIFY", w - 2 * r0 - 8 * k);
                string anio = tiraDisco != null ? m.AnioDeDisco(tiraDisco) : "";
                tiraAbajo = mini.Ajusta(tiraDisco != null ? tiraDisco.Nombre + (string.IsNullOrEmpty(anio) ? "" : " · " + anio) + "   (PONER: abrir)" : "ABAJO al final de la lista para elegir uno", w);
            }
            mini.Escribe(d, tiraTit, x + 2 * r0 + 8 * k, y, Turquesa);
            float pie = mini.Tam * 1.5f, arriba = y + mini.Tam * 1.6f;
            float lado = Math.Min(h - (arriba - y) - pie - 4 * k, 104 * k), gap = 10 * k;
            if (lado < 30 * k) return;
            int caben = Math.Max(1, (int)((w + gap) / (lado + gap)));
            int desde = sel >= 0 ? Math.Max(0, Math.Min(sel - caben / 2, n - caben)) : 0;
            for (int i = 0; i < caben && desde + i < n; i++)
            {
                int j = desde + i; Form1.Disco dd = mas[j];
                m.QuierePortada(dd);
                float px = x + i * (lado + gap);
                bool es = j == sel;
                if (es) d.Brillo(px + lado / 2, arriba + lado / 2, lado * 0.8f, VerdeSpotify.ConAlfa(0.35f));
                Portada(dd.Portada, dd.Nombre, px, arriba, lado);
                d.Contorno(px - 2 * k, arriba - 2 * k, lado + 4 * k, lado + 4 * k, 5 * k, es ? 3f * k : 1f * k, es ? Color4.Hex(0xE8F5EC) : Color4.Hex(0x9AAAE0, 0.35f));
            }
            if (n > caben)
            {   // hay mas: unos puntitos
                float px = x + w - 30 * k, py = arriba + lado + 4 * k;
                for (int i = 0; i < 3; i++) Circulo(px + i * 8 * k, py, 2.2f * k, Lavanda.ConAlfa(0.7f));
            }
            mini.Escribe(d, tiraAbajo, x, arriba + lado + 8 * k, sel >= 0 ? Blanco : Color4.Hex(0xC4CCEC, 0.8f));
        }
        private string tiraArtista, tiraTit = "", tiraAbajo = "";
        private int tiraSel = -2, tiraN = -1;
        private float tiraW = -1;
        private Form1.Disco tiraDisco;
    }
}
