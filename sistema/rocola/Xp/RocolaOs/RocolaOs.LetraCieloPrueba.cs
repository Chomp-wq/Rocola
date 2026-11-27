// LA ORDEN `rocola-xp --prueba-letra-cielo-modelo`: las cuentas puras de la letra en el cielo (RocolaOs.LetraCielo.cs), sin la rocola ni la tarjeta.
using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Rocola
{
    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraLetraCieloPrueba() { RocolaOsOrdenes.Si("--prueba-letra-cielo-modelo", RocolaOsLetraCieloModeloPrueba); }

        private static int RocolaOsLetraCieloModeloPrueba(string[] a)
        {
            int bien = 0, mal = 0;
            Action<string, bool> si = (d, ok) => { Console.WriteLine((ok ? "BIEN  " : "MAL   ") + d); if (ok) bien++; else mal++; };
            Func<double, double, bool> cerca = (x0, y0) => Math.Abs(x0 - y0) < 0.001;
            Func<Func<float, float>, float, float, float, bool> Continua = (f, a, b, tope) => { for (float t = a; t < b; t += 0.01f) if (Math.Abs(f(t + 0.01f) - f(t)) > tope) return false; return true; };
            Func<float, int, float, float> Paso = (meta, pasos, ww) => { float xx = 0, vv = 0; for (int i = 0; i < pasos; i++) RocolaOsLetraModelo.Resorte(ref xx, ref vv, meta, ww, 1f / pasos); return xx; };
            Func<bool> Parejo = () => { double ant0 = 0; for (double t = 0.1; t <= 100; t += 0.1) { double r = RocolaOsLetraModelo.Reparto(4, t, 100); if (t > 0.15 && Math.Abs((r - ant0) - 0.003) > 1e-6) return false; ant0 = r; } return true; };
            // una letra inventada: vacio de 0 a 8 (tramo sin voz de 8 s), uno 8, dos 16, largo 24, vacio 34 (10 s sin voz), cinco 44, seis 52, siete 60, vacio final 68
            RocolaOsLetra l = new RocolaOsLetra("x", "LRCLIB", true, new double[] { 0, 8, 16, 24, 34, 44, 52, 60, 68 }, new string[] { "", "uno", "dos", "tres largo", "", "cinco", "seis", "siete", "" });
            RocolaOsLetraItems it = RocolaOsLetraModelo.Arma(l, 100);
            si("Arma: los versos con texto y los tramos sin voz de mas de 5 s (ocho: puntos, uno, dos, largo, puntos, cinco, seis, siete); el vacio del final no cuenta",
               it.N == 8 && it.Verso[0] == -1 && it.Verso[1] == 1 && it.Verso[3] == 3 && it.Verso[4] == -1 && it.Verso[5] == 5 && it.Verso[7] == 7);
            si("Arma: cada uno acaba donde empieza el siguiente y el ultimo al final de la cancion (100 s)", cerca(it.Fin[0], 8) && cerca(it.Fin[4], 44) && cerca(it.Fin[7], 100));
            RocolaOsLetra corto = new RocolaOsLetra("x", "LRCLIB", true, new double[] { 0, 4, 7, 12 }, new string[] { "a", "", "b", "c" });
            si("Arma: un renglon vacio de menos de 5 s no lleva puntos", RocolaOsLetraModelo.Arma(corto, 60).N == 3);
            RocolaOsLetra intro = new RocolaOsLetra("x", "LRCLIB", true, new double[] { 9, 14 }, new string[] { "a", "b" });
            RocolaOsLetraItems ii = RocolaOsLetraModelo.Arma(intro, 60);
            si("Arma: antes del primer verso (a los 9 s, sin renglon vacio) van los puntos desde 0", ii.N == 3 && ii.Verso[0] == -1 && cerca(ii.Ini[0], 0) && cerca(ii.Fin[0], 9));
            RocolaOsLetra pronto = new RocolaOsLetra("x", "LRCLIB", true, new double[] { 2, 6 }, new string[] { "a", "b" });
            si("Arma: con el primer verso a los 2 s no hay puntos y antes de el la cosa de ahora es -1", RocolaOsLetraModelo.Arma(pronto, 60).N == 2 && RocolaOsLetraModelo.Actual(RocolaOsLetraModelo.Arma(pronto, 60), 1) == -1);
            si("Actual: el que ya empezo (8 s: el verso uno, 15,9 s: el mismo, 16 s: dos, 40 s: los puntos)", RocolaOsLetraModelo.Actual(it, 8) == 1 && RocolaOsLetraModelo.Actual(it, 15.9) == 1 && RocolaOsLetraModelo.Actual(it, 16) == 2 && RocolaOsLetraModelo.Actual(it, 40) == 4);
            si("PuntosVis: entran en 0,5 s (a los 34,25 s a medias), llenos a los 36 s, se van en los 0,45 s antes del siguiente verso y no hay en un verso",
               RocolaOsLetraModelo.PuntosVis(it, 4, 34) == 0f && cerca(RocolaOsLetraModelo.PuntosVis(it, 4, 34.25), 0.5) && cerca(RocolaOsLetraModelo.PuntosVis(it, 4, 36), 1) && cerca(RocolaOsLetraModelo.PuntosVis(it, 4, 43.775), 0.5) && RocolaOsLetraModelo.PuntosVis(it, 4, 44) == 0f && RocolaOsLetraModelo.PuntosVis(it, 2, 18) == 0f);
            si("PuntoBrillo: cada punto en su fase (el 0 al minimo en 0 s, el 1 llega despues) y siempre de 0 a 1", cerca(RocolaOsLetraModelo.PuntoBrillo(0, 0), 0) && RocolaOsLetraModelo.PuntoBrillo(0, 1) > 0.05f && cerca(RocolaOsLetraModelo.PuntoBrillo(1.3, 0), 1));
            // lo que se ve segun la distancia
            si("AlfaD: el que se canta opaco (1), el siguiente translucido (0,3 a 0,4), el de arriba se apaga, los lejanos menos",
               RocolaOsLetraModelo.AlfaD(0) == 1f && RocolaOsLetraModelo.AlfaD(1) > 0.3f && RocolaOsLetraModelo.AlfaD(1) < 0.4f && RocolaOsLetraModelo.AlfaD(-1) > 0.25f && RocolaOsLetraModelo.AlfaD(-1) < 0.4f && RocolaOsLetraModelo.AlfaD(-2.5f) < 0.16f && RocolaOsLetraModelo.AlfaD(4) < RocolaOsLetraModelo.AlfaD(1) && RocolaOsLetraModelo.AlfaD(4) > 0.25f);
            si("AlfaD: baja sin saltos de 0 a 1 (cada 0,01 no cambia mas de 0,03)", Continua(RocolaOsLetraModelo.AlfaD, -3, 5, 0.03f));
            si("BlurD: 0 en el que se canta, crece con la distancia (el siguiente a la mitad, el lejano del todo) y es continua", RocolaOsLetraModelo.BlurD(0) == 0f && RocolaOsLetraModelo.BlurD(1) > 0.4f && RocolaOsLetraModelo.BlurD(1) < 0.5f && RocolaOsLetraModelo.BlurD(3) == 1f && RocolaOsLetraModelo.BlurD(2) > RocolaOsLetraModelo.BlurD(1) && Continua(RocolaOsLetraModelo.BlurD, -4, 4, 0.03f));
            si("EscalaD: 1 el que se canta y 0,96 los demas, creciendo desde el borde izquierdo", RocolaOsLetraModelo.EscalaD(0) == 1f && cerca(RocolaOsLetraModelo.EscalaD(1), 0.96) && cerca(RocolaOsLetraModelo.EscalaD(-2), 0.96));
            si("Mascara: 0 en los bordes de la columna, 1 en el cuerpo, suave", RocolaOsLetraModelo.Mascara(100, 100, 400, 50) == 0f && RocolaOsLetraModelo.Mascara(400, 100, 400, 50) == 0f && RocolaOsLetraModelo.Mascara(250, 100, 400, 50) == 1f && RocolaOsLetraModelo.Mascara(125, 100, 400, 50) > 0.4f && RocolaOsLetraModelo.Mascara(125, 100, 400, 50) < 0.6f);
            // el resorte: sin rebote, y llega (0,55 s con w = 5,9 / 0,55)
            float x = 0, v = 0, w = 5.9f / 0.55f, pico = 0; bool mono = true; float ant = 0; float en55 = 0;
            for (int i = 1; i <= 240; i++)
            {
                RocolaOsLetraModelo.Resorte(ref x, ref v, 100f, w, 1f / 240f * 2f);   // 2 s en pasos de 1/120
                if (x < ant - 1e-3f) mono = false; ant = x; pico = Math.Max(pico, x);
                if (i == 66) en55 = x;
            }
            si("Resorte: sube sin rebote (nunca pasa de la meta, " + pico.ToString("0.00", CultureInfo.InvariantCulture) + ") y a los 0,55 s lleva " + en55.ToString("0.0", CultureInfo.InvariantCulture) + " de 100", mono && pico <= 100.001f && en55 > 96f && en55 < 100f);
            float x2 = 0, v2 = 0; RocolaOsLetraModelo.Resorte(ref x2, ref v2, 100f, w, 1f); RocolaOsLetraModelo.Resorte(ref x2, ref v2, 100f, w, 0.5f);
            si("Resorte: un paso largo de 1 s o de 0,016 s da lo mismo (analitico)", cerca(Paso(100, 16, w) / 100, Paso(100, 1, w) / 100) && x2 > 99.9f);
            // sin tiempos
            RocolaOsLetra m = new RocolaOsLetra("y", "el MP3", false, null, new string[] { "a", "", "b", "c", "", "d" });
            RocolaOsLetraItems im = RocolaOsLetraModelo.Arma(m, 100);
            si("Arma sin tiempos: solo los renglones con texto, en orden (0, 2, 3, 5), sin puntos", im.N == 4 && !im.Sinc && im.Verso[0] == 0 && im.Verso[1] == 2 && im.Verso[2] == 3 && im.Verso[3] == 5);
            si("Reparto: 0 al principio, a la mitad de 100 s con 4 renglones 1,5, el ultimo al final y sin duracion 0", cerca(RocolaOsLetraModelo.Reparto(4, 0, 100), 0) && cerca(RocolaOsLetraModelo.Reparto(4, 50, 100), 1.5) && cerca(RocolaOsLetraModelo.Reparto(4, 100, 100), 3) && cerca(RocolaOsLetraModelo.Reparto(4, 140, 100), 3) && RocolaOsLetraModelo.Reparto(4, 5, 0) == 0);
            si("Reparto: avanza parejo (cada 0,1 s la misma cantidad, sin saltos)", Parejo());
            // partir un verso largo (la letra de la maquina; la medida es la de XP)
            string[] una = RocolaOsLetraHornea.Parte("hola amigos", 40f, 700);
            string larga = "este es un verso inventado largo que no cabe en un solo renglon de la columna";
            string[] var = RocolaOsLetraHornea.Parte(larga, 40f, 400);
            si("Parte: un verso corto cabe en un renglon; uno largo se parte en varios (" + var.Length + ") sin perder palabras y sin cortar ninguna", una.Length == 1 && var.Length >= 2 && var.Length <= 4 && string.Join(" ", var) == larga);
            si("Parte: no pasa de cuatro renglones aunque el verso sea enorme", RocolaOsLetraHornea.Parte(larga + " " + larga + " " + larga, 40f, 300).Length == 4);
            Console.WriteLine("   (cambio de verso " + RocolaOsLetraModelo.CambioS.ToString("0.00", CultureInfo.InvariantCulture) + " s)");
            Console.WriteLine("== " + bien + " BIEN, " + mal + " MAL");
            return mal == 0 ? 0 : 1;
        }
    }
}
