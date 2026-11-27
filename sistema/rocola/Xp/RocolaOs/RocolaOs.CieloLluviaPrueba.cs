// EL CIELO VIVO DE ROCOLA OS, GRUPO «NUBES Y CLIMA»: LA ORDEN DE PRUEBA DE LAS CUENTAS (Pruebas/RocolaOs/cielo/prueba-clima.sh, parte «orden»).
// Sin la rocola entera: las cuentas puras de DN6, CL1, CL2, CL3, CL4 y CL5 (lo que se quiere por clima, los colores de las nubes y su tope de luz
// -la regla 3-, el viento, la lluvia, el tope de un relampago cada 6 s, la niebla, los textos). Una linea por comprobacion (BIEN o MAL); sale con 0
// si todo bien.   rocola-xp --prueba-cielo-clima
using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Rocola
{
    internal static class RocolaOsCieloClimaPrueba
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra() { RocolaOsOrdenes.Si("--prueba-cielo-clima", Prueba); }

        private static int malas, buenas;
        private static void Si(string que, bool bien) { Console.WriteLine((bien ? "BIEN  " : "MAL   ") + que); if (bien) buenas++; else malas++; }
        private static RocolaOsCieloFoto Foto(RocolaOsClimaTipo t, float nubes, bool clima)
        {
            RocolaOsCieloFoto f = new RocolaOsCieloFoto();
            f.ClimaVa = clima; f.ClimaSabido = clima;
            f.Clima = new RocolaOsClima(t, -1, nubes, 2f, 18f, 270f, 21f, t == RocolaOsClimaTipo.Neblina ? 600f : float.NaN, true, DateTime.UtcNow, true);
            return f;
        }

        internal static int Prueba(string[] a)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            // ---- lo que se quiere por clima ----
            float n, g, ll, ni, nb, to;
            RocolaOsCieloClimaVista.Objetivo(Foto(RocolaOsClimaTipo.Despejado, 0f, false), out n, out g, out ll, out ni, out nb, out to);
            Si("sin clima: pocas nubes de adorno (" + n.ToString("0.00", ci) + "), nada gris, sin lluvia ni niebla", Math.Abs(n - 0.22f) < 1e-4 && g == 0f && ll == 0f && nb == 0f && to == 0f);
            RocolaOsCieloClimaVista.Objetivo(Foto(RocolaOsClimaTipo.Despejado, 0f, true), out n, out g, out ll, out ni, out nb, out to);
            Si("despejado: casi sin nubes (" + n.ToString("0.00", ci) + ") y nada gris", n >= 0.1f && n <= 0.2f && g == 0f && ll == 0f);
            RocolaOsCieloClimaVista.Objetivo(Foto(RocolaOsClimaTipo.Nublado, 0.5f, true), out n, out g, out ll, out ni, out nb, out to);
            Si("nublado: cielo cubierto (" + n.ToString("0.00", ci) + ") y gris, sin lluvia", n >= 0.85f && g >= 0.3f && ll == 0f);
            RocolaOsCieloClimaVista.Objetivo(Foto(RocolaOsClimaTipo.Lluvia, 0.9f, true), out n, out g, out ll, out ni, out nb, out to);
            Si("lluvia: nubes y gris y lluvia (" + ll.ToString("0.00", ci) + "), sin tormenta", n >= 0.9f && g > 0.5f && ll > 0.4f && to == 0f);
            RocolaOsCieloClimaVista.Objetivo(Foto(RocolaOsClimaTipo.Tormenta, 1f, true), out n, out g, out ll, out ni, out nb, out to);
            Si("tormenta: la mas oscura (gris " + g.ToString("0.00", ci) + ") y con relampagos", n == 1f && g >= 0.85f && to == 1f && ll > 0.8f);
            RocolaOsCieloClimaVista.Objetivo(Foto(RocolaOsClimaTipo.Neblina, 0.2f, true), out n, out g, out ll, out ni, out nb, out to);
            Si("neblina: niebla entera con visibilidad de 600 m (" + nb.ToString("0.00", ci) + "), sin lluvia", nb == 1f && ll == 0f);
            RocolaOsCieloClimaVista.Objetivo(Foto(RocolaOsClimaTipo.Nieve, 0.8f, true), out n, out g, out ll, out ni, out nb, out to);
            Si("nieve: copos (" + ni.ToString("0.00", ci) + ") y no lluvia", ni > 0.4f && ll == 0f);
            Si("neblina por la visibilidad: 800 m 1, 2000 m 0,8, 5000 m 0,6", RocolaOsCieloClimaVista.Niebla01(800f) == 1f && Math.Abs(RocolaOsCieloClimaVista.Niebla01(2000f) - 0.8f) < 1e-4f && Math.Abs(RocolaOsCieloClimaVista.Niebla01(5000f) - 0.6f) < 1e-4f && RocolaOsCieloClimaVista.Niebla01(float.NaN) == 1f);

            // ---- el viento (la rocola mira al sur: el este a la izquierda) ----
            Si("viento del oeste (270): va a la izquierda (" + RocolaOsCieloClimaVista.DireccionX(270f).ToString("0.00", ci) + ")", RocolaOsCieloClimaVista.DireccionX(270f) < -0.9f);
            Si("viento del este (90): va a la derecha", RocolaOsCieloClimaVista.DireccionX(90f) > 0.9f);
            Si("viento del norte (0): casi no, pero se mueve (|x| 0,3)", Math.Abs(Math.Abs(RocolaOsCieloClimaVista.DireccionX(0f)) - 0.3f) < 1e-5f);
            Si("velocidad de deriva: 18 km/h = 16,2 px/s, tope de 30, minimo de 3, sin clima 3,5", Math.Abs(RocolaOsCieloClimaVista.VelocidadPx(18f, true) - 16.2f) < 1e-3f && RocolaOsCieloClimaVista.VelocidadPx(100f, true) == 30f && RocolaOsCieloClimaVista.VelocidadPx(0f, true) == 3f && RocolaOsCieloClimaVista.VelocidadPx(50f, false) == 3.5f);
            Si("inclinacion de la lluvia: del oeste con 30 km/h negativa y 0,5 de tope", RocolaOsCieloLluvia.Inclina(-1f, 30f) < -0.55f * 0 && Math.Abs(RocolaOsCieloLluvia.Inclina(-1f, 30f) + 0.5f) < 1e-5f && RocolaOsCieloLluvia.Inclina(1f, 10f) > 0f && RocolaOsCieloLluvia.Inclina(1f, 0f) == 0f);

            // ---- la regla 3: ningun color de nube pasa de 0,15 de luminancia (de dia) y baja con la luz ----
            double peor = 0; bool sombraOk = true; int cuantos = 0;
            for (double alt = -20; alt <= 90; alt += 5)
                for (double lalt = -10; lalt <= 80; lalt += 15)
                    for (float gris = 0f; gris <= 1f; gris += 0.25f)
                    {
                        RocolaOsCieloFoto f = new RocolaOsCieloFoto(); f.SolAlt = alt; f.LunaAlt = lalt; f.Raya = alt < 12 ? 0xFF9A58u : 0xF0F8FFu;
                        float sol = RocolaOsCieloAstros.Paso(alt, -6.0, 10.0), luna = RocolaOsCieloAstros.Paso(lalt, -3.0, 8.0) * 0.8f * (1f - sol);
                        float lr, lg, lb, sr, sg, sb; RocolaOsCieloClimaVista.Colores(f, sol, luna, gris, out lr, out lg, out lb, out sr, out sg, out sb);
                        double l = RocolaOsCieloDia.Luminancia(lr, lg, lb), s = RocolaOsCieloDia.Luminancia(sr, sg, sb), tope = 0.15 * sol + 0.075 * luna + 0.012;
                        peor = Math.Max(peor, l - tope); if (s > l + 1e-6) sombraOk = false; cuantos++;
                    }
            Si("la luz de las nubes no pasa de su tope (0,15 sol + 0,075 luna + 0,012) en " + cuantos + " casos (peor exceso " + peor.ToString("0.0000", ci) + ")", peor < 0.004);
            Si("la sombra de una nube nunca es mas clara que su luz", sombraOk);
            double lmx = 0;
            for (float nivel = 0f; nivel <= 1f; nivel += 0.1f) for (int c = 0; c < 2; c++) { float r, gg, b; RocolaOsCieloLluvia.Color(c == 1, nivel, out r, out gg, out b); lmx = Math.Max(lmx, RocolaOsCieloDia.Luminancia(r, gg, b)); }
            Si("las rayas de lluvia no pasan de 0,17 de luminancia (" + lmx.ToString("0.000", ci) + ")", lmx <= 0.17);

            // ---- las nubes: cuantas, donde, cuanto tapan ----
            RocolaOsCieloClimaVista.Nubes = 0.22f; RocolaOsCieloClimaVista.AlfaNube = 0.8f; RocolaOsCieloClimaVista.Deriva = 0; RocolaOsCieloClimaVista.DirX = -1f; RocolaOsCieloClimaVista.Viento = 0;
            RocolaOsCieloNubes.Coloca(1024, 667f, 1f);
            int pocas = RocolaOsCieloNubes.Visibles;
            RocolaOsCieloClimaVista.Nubes = 1f; RocolaOsCieloNubes.Coloca(1024, 667f, 1f);
            int todas = RocolaOsCieloNubes.Visibles;
            Si("pocas nubes de adorno (" + pocas + ") y todas con cielo cubierto (" + todas + " de " + RocolaOsCieloNubes.Max + ")", pocas >= 2 && pocas <= 5 && todas == RocolaOsCieloNubes.Max);
            bool dentro = true; for (int k = 0; k < RocolaOsCieloNubes.Max; k++) if (RocolaOsCieloNubes.Cy[k] > 0.76f * 667f + 0.01f || RocolaOsCieloNubes.Cy[k] < 0f) dentro = false;
            Si("ninguna nube baja de 0,76 del horizonte", dentro);
            float c0 = RocolaOsCieloNubes.Cx[1];
            RocolaOsCieloClimaVista.Deriva = 10; RocolaOsCieloNubes.Coloca(1024, 667f, 1f);
            Si("con viento a la izquierda, a los 10 s la nube 1 esta " + (c0 - RocolaOsCieloNubes.Cx[1]).ToString("0.0", ci) + " px a la izquierda (o dio la vuelta)", RocolaOsCieloNubes.Cx[1] < c0 || RocolaOsCieloNubes.Cx[1] > c0 + 200f);
            float tapa = RocolaOsCieloNubes.Cobertura(RocolaOsCieloNubes.Cx[1], RocolaOsCieloNubes.Cy[1]), libre = RocolaOsCieloNubes.Cobertura(-500f, 5000f);
            Si("una estrella tras el centro de una nube brilla " + tapa.ToString("0.00", ci) + " y lejos de ellas " + libre.ToString("0.00", ci), tapa < 0.3f && libre == 1f);
            RocolaOsCieloClimaVista.Deriva = 0; RocolaOsCieloNubes.Coloca(1024, 667f, 0.35f);
            float tenue = RocolaOsCieloNubes.Al[1];
            RocolaOsCieloNubes.Coloca(1024, 667f, 1f);
            Si("con la calma (0,35) las nubes pierden alfa (" + tenue.ToString("0.00", ci) + " frente a " + RocolaOsCieloNubes.Al[1].ToString("0.00", ci) + ")", tenue < RocolaOsCieloNubes.Al[1] * 0.8f);
            RocolaOsCieloFoto fv = new RocolaOsCieloFoto(); fv.SolAlt = 40; fv.SolAz = 180; fv.DN6 = true;
            RocolaOsCieloEscena esc = new RocolaOsCieloEscena(); esc.Ancho = 1024; esc.Alto = 768; esc.Horizonte = 667f;
            RocolaOsCieloClimaVista.Sol = 1f; RocolaOsCieloClimaVista.Luna = 0f;
            RocolaOsCieloClimaVista.LitR = 120; RocolaOsCieloClimaVista.LitG = 126; RocolaOsCieloClimaVista.LitB = 134; RocolaOsCieloClimaVista.ShadeR = 50; RocolaOsCieloClimaVista.ShadeG = 55; RocolaOsCieloClimaVista.ShadeB = 80;
            int tris = RocolaOsCieloNubes.Arma(fv, esc, -1f, -1f, 0f);
            Si("la malla de 14 nubes: " + tris + " triangulos (el tope es " + (RocolaOsCieloNubes.Max * RocolaOsCieloNubes.Manchas * RocolaOsCieloNubes.Seg * 3) + ")", tris > 1000 && tris <= RocolaOsCieloNubes.Max * RocolaOsCieloNubes.Manchas * RocolaOsCieloNubes.Seg * 3);
            uint cmx = 0; Tarjeta.Vtx[] mall = RocolaOsCieloNubes.Malla; for (int i = 0; i < tris * 3; i++) { uint al = mall[i].Color >> 24; if (al > cmx) cmx = al; }
            Si("el alfa de la malla no pasa de 0,85 (" + (cmx / 255f).ToString("0.00", ci) + ")", cmx <= 217);

            // ---- la lluvia ----
            Si("rayas: 0 sin lluvia, 420 con lluvia total, menos con calma (" + RocolaOsCieloLluvia.Cuantas(1f, 0.35f) + ")", RocolaOsCieloLluvia.Cuantas(0f, 1f) == 0 && RocolaOsCieloLluvia.Cuantas(1f, 1f) == RocolaOsCieloLluvia.Max && RocolaOsCieloLluvia.Cuantas(1f, 0.35f) < RocolaOsCieloLluvia.Max * 0.6f);
            float hz = 667f; bool bien = true; float xmin = 1e9f, xmax = -1e9f, ymax = -1e9f; int trisLluvia = 0;
            for (int paso = 0; paso < 30; paso++)
            {
                trisLluvia = RocolaOsCieloLluvia.Arma(1024, hz, 768f, 1f, 0f, -1f, 30f, 1f, 1f, 0.1);
                Tarjeta.Vtx[] m = RocolaOsCieloLluvia.Malla;
                for (int i = 0; i < trisLluvia * 3; i++) { if (m[i].Y > ymax) ymax = m[i].Y; if (m[i].X < xmin) xmin = m[i].X; if (m[i].X > xmax) xmax = m[i].X; }
            }
            if (ymax > hz + 2f) bien = false;
            Si("la lluvia no pasa del horizonte (y max " + ymax.ToString("0.0", ci) + " de " + hz + ") y es de " + trisLluvia + " triangulos", bien && trisLluvia > 400);
            Si("la lluvia con viento del oeste a 30 km/h va inclinada a la izquierda (cabeza mas a la izquierda que la cola)", LluviaInclinada(-1f));
            Si("la lluvia con viento del este va inclinada a la derecha", LluviaInclinada(1f));
            int copos = RocolaOsCieloLluvia.Arma(1024, hz, 768f, 0f, 1f, -1f, 10f, 1f, 1f, 0.1);
            Si("nieve: copos (" + copos + " triangulos)", copos > 300);

            // ---- los relampagos ----
            Si("el tiempo hasta el siguiente relampago: de 6 a 16 s (" + RocolaOsCieloNubesRelampago.Intervalo(0f).ToString("0.0", ci) + " a " + RocolaOsCieloNubesRelampago.Intervalo(1f).ToString("0.0", ci) + ")", RocolaOsCieloNubesRelampago.Intervalo(0f) >= 6.0 && Math.Abs(RocolaOsCieloNubesRelampago.Intervalo(1f) - 16.0) < 1e-9 && RocolaOsCieloNubesRelampago.Intervalo(-5f) >= 6.0 && RocolaOsCieloNubesRelampago.Intervalo(5f) <= 16.0);
            bool sube = true, baja = true; double pico = 0, prev = 0; double tpico = 0;
            for (double u = 0; u < 1.0; u += 0.005)
            {
                double e = RocolaOsCieloNubesRelampago.Envolvente(u);
                if (e > pico) { pico = e; tpico = u; }
                if (u <= RocolaOsCieloNubesRelampago.SubeS && e + 1e-9 < prev) sube = false;
                if (u > RocolaOsCieloNubesRelampago.SubeS + 0.005 && e > prev + 1e-9) baja = false;
                prev = e;
            }
            Si("el relampago es UN pulso suave (sube hasta " + tpico.ToString("0.00", ci) + " s y solo baja despues; sin parpadeos), de 0 a 1 y se acaba a los " + RocolaOsCieloNubesRelampago.DuraS + " s", sube && baja && pico > 0.97 && RocolaOsCieloNubesRelampago.Envolvente(RocolaOsCieloNubesRelampago.DuraS) == 0f && RocolaOsCieloNubesRelampago.Envolvente(0) == 0f);
            Si("la luz de un relampago suma como mucho +" + (255 * RocolaOsCieloNubesRelampago.Suma * 180 / 255f).ToString("0", ci) + " a un canal (color 180 x alfa 0,26)", 180 * RocolaOsCieloNubesRelampago.Suma < 50f);

            // ---- la neblina ----
            Si("neblina: alfa 0 a 0,80 del horizonte, 0,55 en el 0,848 del alto y 0,7 en el horizonte", RocolaOsCieloNeblina.Alfa(0.8f * hz, hz, 1f) == 0f && Math.Abs(RocolaOsCieloNeblina.Alfa(hz * 0.848f / 0.868f, hz, 1f) - 0.55f) < 1e-3f && Math.Abs(RocolaOsCieloNeblina.Alfa(hz, hz, 1f) - 0.7f) < 1e-3f && RocolaOsCieloNeblina.Alfa(hz + 10f, hz, 1f) == 0f && RocolaOsCieloNeblina.Alfa(hz, hz, 0.5f) < 0.36f);
            RocolaOsCieloClimaVista.LitR = RocolaOsCieloClimaVista.LitG = RocolaOsCieloClimaVista.LitB = 100f;
            int trisNiebla = RocolaOsCieloNeblina.Arma(1024, hz, 1f, 12.0, 1f);
            uint amx = 0; float ymxN = 0; Tarjeta.Vtx[] mn = RocolaOsCieloNeblina.Malla; for (int i = 0; i < trisNiebla * 3; i++) { uint al = mn[i].Color >> 24; if (al > amx) amx = al; if (mn[i].Y > ymxN) ymxN = mn[i].Y; }
            Si("la malla de la neblina: " + trisNiebla + " triangulos, alfa max " + (amx / 255f).ToString("0.00", ci) + " y no baja del horizonte (" + ymxN.ToString("0.0", ci) + ")", trisNiebla == RocolaOsCieloNeblina.Columnas * 4 && amx <= 180 && ymxN <= hz + 0.01f);
            Si("sin niebla no se arma nada", RocolaOsCieloNeblina.Arma(1024, hz, 0f, 0.0, 1f) == 0);
            bool vaiven = true; for (int c = 0; c < 30; c++) for (double t = 0; t < 600; t += 7.3) { float vv = RocolaOsCieloNeblina.Vaiven(c, t); if (vv < 0.6f - 1e-4f || vv > 1f + 1e-4f) vaiven = false; }
            Si("el vaiven de la niebla queda entre 0,6 y 1", vaiven);

            // ---- los textos de la pastilla y el mensaje ----
            Si("hora: 0:05 = 12:05 a. m.; 13:30 = 1:30 p. m.; 12:00 = 12:00 p. m.", RocolaOsCieloClimaTextos.Hora(new DateTime(2026, 11, 18, 0, 5, 0)) == "12:05 a. m." && RocolaOsCieloClimaTextos.Hora(new DateTime(2026, 11, 18, 13, 30, 0)) == "1:30 p. m." && RocolaOsCieloClimaTextos.Hora(new DateTime(2026, 11, 18, 12, 0, 0)) == "12:00 p. m.");
            Si("grados: 23,6 = 24 °C; -2,4 = -2 °C; sin dato vacio", RocolaOsCieloClimaTextos.Grados(23.6f) == "24 °C" && RocolaOsCieloClimaTextos.Grados(-2.4f) == "-2 °C" && RocolaOsCieloClimaTextos.Grados(float.NaN) == "");
            RocolaOsClima cl = new RocolaOsClima(RocolaOsClimaTipo.Lluvia, 63, 0.9f, 2f, 18f, 240f, 16.4f, float.NaN, true, DateTime.UtcNow, true);
            string pastilla = RocolaOsCieloClimaTextos.Pastilla(new DateTime(2026, 11, 18, 19, 45, 0), cl);
            Si("la pastilla dice «" + pastilla + "»", pastilla == "7:45 p. m. · 16 °C · Lluvia");
            bool msgs = true; int largo = 0;
            foreach (RocolaOsClimaTipo t in new RocolaOsClimaTipo[] { RocolaOsClimaTipo.Despejado, RocolaOsClimaTipo.PocoNublado, RocolaOsClimaTipo.Nublado, RocolaOsClimaTipo.Neblina, RocolaOsClimaTipo.Llovizna, RocolaOsClimaTipo.Lluvia, RocolaOsClimaTipo.Aguacero, RocolaOsClimaTipo.Tormenta, RocolaOsClimaTipo.Nieve })
            {
                RocolaOsClima ct = new RocolaOsClima(t, -1, 0.5f, 1f, 10f, 270f, 18f, float.NaN, true, DateTime.UtcNow, true);
                foreach (bool dd in new[] { true, false })
                {
                    string m = RocolaOsCieloClimaTextos.Mensaje(ct, dd);
                    if (string.IsNullOrEmpty(m) || m.Length > 52) msgs = false; largo = Math.Max(largo, m == null ? 0 : m.Length);
                }
            }
            Si("un mensaje por cada tipo de clima, de 52 letras como mucho (el mas largo, " + largo + ")", msgs && RocolaOsCieloClimaTextos.Mensaje(RocolaOsClima.Despejado0).Contains("despejado"));
            RocolaOsClima desp = new RocolaOsClima(RocolaOsClimaTipo.Despejado, 0, 0.1f, 0f, 5f, 0f, 24f, float.NaN, true, DateTime.UtcNow, true);
            string mDia = RocolaOsCieloClimaTextos.Mensaje(desp, true), mNoche = RocolaOsCieloClimaTextos.Mensaje(desp, false);
            Si("el despejado de noche no pide «sol» («" + mNoche + "»); el de dia si («" + mDia + "»)", mDia.Contains("sol") && !mNoche.Contains("sol") && mNoche.Contains("noche"));
            System.Collections.Generic.List<string> lista = new System.Collections.Generic.List<string> { "a", "b", "c", "d" };
            RocolaOsCieloFoto fm = Foto(RocolaOsClimaTipo.Lluvia, 0.9f, true); fm.CL5 = true;
            new RocolaOsCieloClimaMensajero().Pon(lista, fm);
            Si("el mensaje del clima se mete tercero y con CL5 apagada o sin clima no se mete", lista.Count == 5 && lista[2].StartsWith("Afuera") && Pone(false, true) == 4 && Pone(true, false) == 4);

            // ---- (a pedido: --pastilla=RUTA) la pastilla de cada tiempo, de dia y de noche, en una hoja de 1024x768 con un cielo de fondo
            foreach (string arg in a) if (arg.StartsWith("--pastilla=")) HojaDePastillas(arg.Substring("--pastilla=".Length));
            Console.WriteLine((malas == 0 ? "TODO BIEN" : malas + " MAL") + " (" + buenas + " de " + (buenas + malas) + ")");
            return malas == 0 ? 0 : 1;
        }
        private static void HojaDePastillas(string ruta)
        {
            using (SkiaSharp.SKBitmap hoja = Rocola.Nativo.Mapas.Nuevo(1024, 768))
            using (SkiaSharp.SKCanvas g = new SkiaSharp.SKCanvas(hoja))
            {
                g.Clear(new SkiaSharp.SKColor(33, 82, 154));
                RocolaOsClimaTipo[] tipos = { RocolaOsClimaTipo.Despejado, RocolaOsClimaTipo.PocoNublado, RocolaOsClimaTipo.Nublado, RocolaOsClimaTipo.Neblina, RocolaOsClimaTipo.Llovizna, RocolaOsClimaTipo.Lluvia, RocolaOsClimaTipo.Tormenta, RocolaOsClimaTipo.Nieve };
                for (int i = 0; i < tipos.Length * 2; i++)
                {
                    bool dia = i < tipos.Length; RocolaOsClimaTipo t = tipos[i % tipos.Length];
                    RocolaOsClima c = new RocolaOsClima(t, -1, 0.5f, 1f, 10f, 270f, 21.4f, float.NaN, true, DateTime.UtcNow, true);
                    using (SkiaSharp.SKBitmap p = RocolaOsCieloClimaPastilla.Dibuja(RocolaOsCieloClimaTextos.Pastilla(new DateTime(2026, 11, 18, dia ? 13 : 22, 5, 0), c), c, dia, 768))
                        g.DrawBitmap(p, 40 + (i / tipos.Length) * 400, 40 + (i % tipos.Length) * 70);
                }
                using (SkiaSharp.SKImage im = SkiaSharp.SKImage.FromBitmap(hoja)) using (SkiaSharp.SKData d = im.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100)) using (System.IO.FileStream f = System.IO.File.Create(ruta)) d.SaveTo(f);
            }
            Console.WriteLine("la hoja de pastillas: " + ruta);
        }
        private static int Pone(bool cl5, bool clima)
        {
            System.Collections.Generic.List<string> lista = new System.Collections.Generic.List<string> { "a", "b", "c", "d" };
            RocolaOsCieloFoto f = Foto(RocolaOsClimaTipo.Lluvia, 0.9f, clima); f.CL5 = cl5;
            new RocolaOsCieloClimaMensajero().Pon(lista, f);
            return lista.Count;
        }
        // la lluvia inclinada hacia donde dice el viento: en el cuadro, la cabeza (abajo) de las rayas largas queda a ese lado de su cola
        private static bool LluviaInclinada(float dirX)
        {
            int tris = RocolaOsCieloLluvia.Arma(1024, 667f, 768f, 1f, 0f, dirX, 40f, 1f, 1f, 0.1);
            Tarjeta.Vtx[] m = RocolaOsCieloLluvia.Malla; int bien = 0, total = 0;
            for (int i = 0; i + 5 < tris * 3; i += 6)
            {
                // la cola son los dos primeros vertices (alfa 0); la cabeza, el tercero y el quinto (alfa mayor)
                float xt = (m[i].X + m[i + 1].X) / 2f, yt = (m[i].Y + m[i + 1].Y) / 2f, xc = (m[i + 2].X + m[i + 4].X) / 2f, yc = (m[i + 2].Y + m[i + 4].Y) / 2f;
                if (yc - yt < 4f) continue;
                total++; if ((xc - xt) * dirX > 0f) bien++;
            }
            return total > 50 && bien >= total * 0.95;
        }
    }
}
