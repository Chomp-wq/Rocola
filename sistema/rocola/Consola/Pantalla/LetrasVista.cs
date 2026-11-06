// LAS LETRAS EN EL ESTADO EXTENDIDO, AL ESTILO DE APPLE MUSIC (SP1, decision del dueno del 6-11-2026: «las letras de la musica quiero
// que tengan el estilo de Apple Music»). La letra la da Spotify en el momento (Form1.LetraSonando, SpotifyMas.cs; no se guarda) y se
// dibuja sobre el cielo, encima del panel de lo que suena:
//  - grandes, en negrita y alineadas a la izquierda, envueltas al ancho;
//  - la linea que se canta, encendida (blanca, o el color de resalte que da Spotify); las de antes, apagadas; las que vienen, tenues; y
//    cada una cambia de brillo poco a poco, no de golpe;
//  - el texto se desliza suave hacia arriba (un resorte amortiguado) para dejar la linea que se canta a un tercio del alto;
//  - en los tramos sin voz (antes de la primera linea o en las lineas vacias), los tres puntos que respiran y se van llenando;
//  - de fondo, una franja del color que Spotify da a esa letra; y abajo, de quien es la letra (el proveedor).
// Sin letra sincronizada (solo el texto), se desliza con lo que lleva la cancion. Sin letra, no se dibuja nada.
using System;
using System.Collections.Generic;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class VistaGrafica
    {
        private Form1.Letra letraVista;
        private float letraScroll, letraAncho, letraTam;
        private double letraT;
        private float[] letraBrillo;
        private List<List<string>> letraRenglones;
        private float[] letraY;   // donde empieza cada linea (desde arriba del texto)
        private float letraAlto;

        private void LetrasApple(Controlador c, float W, float y0, float y1, float alfa)
        {
            Form1 m = c.motor;
            Form1.Letra l = m.LetraSonando();
            if (l == null || l.Lineas.Count == 0 || y1 - y0 < 80 * k || alfa <= 0.01f) { letraVista = null; return; }
            double t = reloj.Elapsed.TotalSeconds, dt = Math.Max(0, Math.Min(0.1, t - letraT)); letraT = t;
            float yCredito = y1 - nChica.Tam * 1.2f;
            if (!string.IsNullOrEmpty(l.Proveedor)) y1 = yCredito - 10 * k;   // la letra acaba antes del credito (de quien es la letra)
            Letra f = Titulo(Math.Max(24 * k, Math.Min(46 * k, (y1 - y0) / 6.5f)));
            float x0 = 48 * k, ancho = Math.Min(W * 0.62f, W - x0 - 260 * k);
            // el texto, envuelto (una vez por letra y tamano)
            if (!object.ReferenceEquals(l, letraVista) || letraAncho != ancho || letraTam != f.Tam)
            {
                bool nueva = !object.ReferenceEquals(l, letraVista);
                letraVista = l; letraAncho = ancho; letraTam = f.Tam;
                letraRenglones = new List<List<string>>(l.Lineas.Count);
                letraY = new float[l.Lineas.Count];
                float y = 0, renglon = f.Tam * 1.14f, entre = f.Tam * 0.62f;
                for (int i = 0; i < l.Lineas.Count; i++)
                {
                    List<string> r = l.Lineas[i].Texto.Length == 0 ? new List<string>() : Envuelve(l.Lineas[i].Texto, f, ancho);
                    letraRenglones.Add(r); letraY[i] = y;
                    y += Math.Max(1, r.Count) * renglon + entre;
                }
                letraAlto = y;
                if (nueva) { letraBrillo = new float[l.Lineas.Count]; letraScroll = float.NaN; }
            }
            // la que se canta
            int ms = PruebaLetraMs >= 0 ? PruebaLetraMs : m.MsSonando(c.PorDelante);
            int activa;
            if (l.Sincronizada)
            {
                activa = -1;
                for (int i = 0; i < l.Lineas.Count; i++) { if (l.Lineas[i].Ms <= ms + 120) activa = i; else break; }
            }
            else activa = (int)Math.Min(l.Lineas.Count - 1, Math.Max(0, c.Progreso * l.Lineas.Count));
            // a donde va el texto: la que se canta, a un tercio del alto; antes de la primera, la primera ahi mismo
            float ancla = y0 + (y1 - y0) * 0.32f;
            float objetivo = ancla - (activa >= 0 ? letraY[activa] : 0);
            if (float.IsNaN(letraScroll)) letraScroll = objetivo;
            letraScroll += (objetivo - letraScroll) * (float)(1 - Math.Exp(-dt * 6.5));
            // el fondo: una franja del color de la letra que da Spotify, de la izquierda hacia el cielo
            if (l.Fondo != 0)
            {
                Color4 fo = Color4.Hex(l.Fondo & 0xFFFFFF);
                d.DegradadoH(0, y0 - 20 * k, W * 0.78f, y1 - y0 + 40 * k, fo.ConAlfa(0.50f * alfa), fo.ConAlfa(0f));
            }
            Color4 resalte = l.Resalte != 0 ? Dibujo.Mezcla(Color4.Hex(l.Resalte & 0xFFFFFF), Blanco, 0.65f) : Blanco;
            d.Recorta(0, y0, W, y1 - y0);
            float renglonAlto = f.Tam * 1.14f;
            for (int i = 0; i < l.Lineas.Count; i++)
            {
                float yi = letraScroll + letraY[i];
                List<string> r = letraRenglones[i];
                float hi = Math.Max(1, r.Count) * renglonAlto;
                // el brillo, poco a poco: la que se canta 1; las que vienen, mas tenues cuanto mas lejos; las de antes, apagadas
                float meta = i == activa ? 1f : i > activa ? Math.Max(0.20f, 0.42f - 0.07f * (i - activa - 1)) : 0.22f;
                if (letraBrillo[i] == 0f) letraBrillo[i] = meta;
                letraBrillo[i] += (meta - letraBrillo[i]) * (float)(1 - Math.Exp(-dt * 9));
                if (yi + hi < y0 - 4 * k || yi > y1 + 4 * k) continue;
                // se desvanecen en los bordes de arriba y de abajo
                float borde = Math.Min(SuaveL((yi + hi - y0) / (60 * k)), SuaveL((y1 - yi) / (90 * k)));
                float a = letraBrillo[i] * borde * alfa;
                if (a <= 0.01f) continue;
                if (r.Count == 0)
                {
                    // un tramo sin voz: los tres puntos (solo en la que se canta), que se llenan con el tramo y respiran
                    if (i == activa && l.Sincronizada) PuntosSinVoz(x0, yi + renglonAlto * 0.5f, f.Tam, Avance(l, i, ms), a, resalte, t);
                    continue;
                }
                Color4 col = i == activa ? resalte : Blanco;
                for (int j = 0; j < r.Count; j++) f.Escribe(d, r[j], x0, yi + j * renglonAlto, col.ConAlfa(a));
            }
            // antes de la primera linea (la entrada de la cancion), los puntos tambien
            if (l.Sincronizada && activa < 0 && l.Lineas[0].Ms > 2500)
                PuntosSinVoz(x0, letraScroll - f.Tam * 1.2f, f.Tam, Math.Max(0f, Math.Min(1f, ms / (float)l.Lineas[0].Ms)), alfa, resalte, t);
            d.SinRecorte();
            if (!string.IsNullOrEmpty(l.Proveedor))
                nChica.Escribe(d, "Letra: " + l.Proveedor, x0, yCredito, Blanco.ConAlfa(0.45f * alfa));
        }
        // (pruebas: ROCOLA_PRUEBA_LETRA_MS fija el momento de la cancion, para las capturas)
        private static readonly int PruebaLetraMs = int.TryParse(Environment.GetEnvironmentVariable("ROCOLA_PRUEBA_LETRA_MS"), out int v) ? v : -1;
        private static float SuaveL(float x) { x = Math.Max(0f, Math.Min(1f, x)); return x * x * (3 - 2 * x); }
        // Cuanto va de un tramo sin voz (0 a 1): de su inicio a la linea siguiente.
        private static float Avance(Form1.Letra l, int i, int ms)
        {
            int ini = l.Lineas[i].Ms, fin = i + 1 < l.Lineas.Count ? l.Lineas[i + 1].Ms : ini + 4000;
            return fin > ini ? Math.Max(0f, Math.Min(1f, (ms - ini) / (float)(fin - ini))) : 1f;
        }
        private void PuntosSinVoz(float x, float y, float tam, float avance, float alfa, Color4 col, double t)
        {
            float r = tam * 0.16f, paso = tam * 0.55f;
            float respira = 1f + 0.12f * (float)Math.Sin(t * 3.2);
            for (int i = 0; i < 3; i++)
            {
                float lleno = Math.Max(0f, Math.Min(1f, avance * 3 - i));   // el primero se llena en el primer tercio, y asi
                float a = (0.30f + 0.70f * lleno) * alfa, rr = r * respira * (0.85f + 0.15f * lleno);
                d.Redondo(x + i * paso - rr + r, y - rr, rr * 2, rr * 2, rr, col.ConAlfa(a));
            }
        }
    }
}
