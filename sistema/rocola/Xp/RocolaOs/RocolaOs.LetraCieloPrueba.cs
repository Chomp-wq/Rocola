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
            Func<double, double, bool> cerca = (x, y) => Math.Abs(x - y) < 0.001;
            // una letra inventada: 0 a 8 s, 8 a 16 s (largo: 16 a 40 s, mas de 10), vacio de 40 a 50, 50 a 56, y el ultimo de 56 (sin mas)
            RocolaOsLetra l = new RocolaOsLetra("x", "LRCLIB", true, new double[] { 0, 8, 16, 40, 50, 56 }, new string[] { "uno", "dos", "tres largo", "", "cinco", "seis" });
            si("Span: 8 s para el verso 0; el ultimo, 6 s sin la duracion y hasta el final con ella", cerca(RocolaOsLetraModelo.Span(l, 0, 100), 8) && cerca(RocolaOsLetraModelo.Span(l, 5, 0), 6) && cerca(RocolaOsLetraModelo.Span(l, 5, 70), 14));
            si("Dura: nunca mas de 10 s (el verso 2 dura 24)", cerca(RocolaOsLetraModelo.Dura(l, 2, 100), 10) && cerca(RocolaOsLetraModelo.Dura(l, 1, 100), 8));
            si("Llena: 0 antes, 0,5 a la mitad del compas (12 s del verso 1: 4 s de 8), 1 al final", RocolaOsLetraModelo.Llena(l, 1, 7, 100) == 0f && cerca(RocolaOsLetraModelo.Llena(l, 1, 12, 100), 0.5) && RocolaOsLetraModelo.Llena(l, 1, 30, 100) == 1f);
            si("Llena: el verso largo se llena en 10 s, no en 24 (a los 21 s: 0,5)", cerca(RocolaOsLetraModelo.Llena(l, 2, 21, 100), 0.5) && RocolaOsLetraModelo.Llena(l, 2, 26, 100) == 1f);
            si("HayVoz: un renglon vacio no", !RocolaOsLetraModelo.HayVoz(l, 3, 45, 100) && !RocolaOsLetraModelo.HayVoz(l, -1, 1, 100));
            si("HayVoz: el verso largo (24 s) con voz hasta llenarse y 4 s mas (14 s), y se apaga despues", RocolaOsLetraModelo.HayVoz(l, 2, 29, 100) && !RocolaOsLetraModelo.HayVoz(l, 2, 31, 100));
            si("HayVoz: un verso corto con voz todo el tramo", RocolaOsLetraModelo.HayVoz(l, 1, 15.9, 100));
            si("Siguiente: salta el vacio (del 2 al 4) y no hay despues del ultimo", RocolaOsLetraModelo.Siguiente(l, 2) == 4 && RocolaOsLetraModelo.Siguiente(l, 5) == -1 && RocolaOsLetraModelo.Siguiente(l, -1) == 0);
            si("Suave: 0, 0,5 y 1", RocolaOsLetraModelo.Suave(-1) == 0f && cerca(RocolaOsLetraModelo.Suave(0.5), 0.5) && RocolaOsLetraModelo.Suave(2) == 1f);
            RocolaOsLetraPieza[] p = new RocolaOsLetraPieza[4];
            int n = RocolaOsLetraModelo.Piezas(p, -1, 1, 2, 1f, 0.3f, 1f, false);
            si("Piezas quieto: el que se canta (lleno 0,3, alfa 1, sitio 0) y el que sigue (chico, alfa 1, sitio 1)", n == 2 && p[0].Verso == 1 && p[0].Grande && p[0].Hasta == 0 && p[0].Alfa == 1f && cerca(p[0].Llena, 0.3) && p[1].Verso == 2 && !p[1].Grande && p[1].Hasta == 1);
            n = RocolaOsLetraModelo.Piezas(p, 0, 1, 2, 0.5f, 0f, 1f, true);
            si("Piezas en el cambio (u 0,5): la escalera: sale el de antes (0 a -1, alfa 0,5, lleno), el que seguia se hace grande (1 a 0) y entra otro (2 a 1)",
               n == 4 && p[0].Verso == 0 && p[0].Desde == 0 && p[0].Hasta == -1 && cerca(p[0].Alfa, 0.5) && p[0].Llena == 1f && p[1].Verso == 1 && p[1].Grande && p[1].Desde == 1 && p[1].Hasta == 0
               && p[2].Verso == 1 && !p[2].Grande && p[3].Verso == 2 && !p[3].Grande && p[3].Desde == 2 && p[3].Hasta == 1);
            si("Piezas en el cambio: el chico de antes ya se apago a la mitad y el nuevo chico apenas entra (sin escribirse uno sobre otro)", cerca(p[2].Alfa, 0) && p[3].Alfa < 0.2f);
            n = RocolaOsLetraModelo.Piezas(p, 0, 1, 2, 1f, 0f, 1f, true);
            si("Piezas con u 1: ya quieto (dos piezas)", n == 2);
            n = RocolaOsLetraModelo.Piezas(p, 0, 4, -1, 0.5f, 0f, 1f, false);
            si("Piezas tras un salto (el nuevo no era el siguiente): sale el de antes y entra el nuevo en su sitio, sin escalera", n == 2 && p[1].Desde == 0 && p[1].Hasta == 0);
            // sin tiempos
            RocolaOsLetra m = new RocolaOsLetra("y", "el MP3", false, null, new string[] { "a", "", "b", "c", "", "d" });
            int[] ne = RocolaOsLetraModelo.ConTexto(m);
            si("ConTexto: los renglones con texto, en orden (0, 2, 3, 5)", ne.Length == 4 && ne[0] == 0 && ne[1] == 2 && ne[2] == 3 && ne[3] == 5);
            si("Reparto: 0 al principio, a la mitad de una de 100 s con 4 renglones 2, y no pasa del ultimo", cerca(RocolaOsLetraModelo.Reparto(4, 0, 100), 0) && cerca(RocolaOsLetraModelo.Reparto(4, 50, 100), 2) && cerca(RocolaOsLetraModelo.Reparto(4, 100, 100), 3) && RocolaOsLetraModelo.Reparto(4, 5, 0) == 0);
            // el compas de punta a punta: lo llenado de un verso va a su tiempo, cuadro a cuadro
            double dif = 0; for (double t = 8; t <= 16; t += 0.01) dif = Math.Max(dif, Math.Abs(RocolaOsLetraModelo.Llena(l, 1, t, 100) - (t - 8) / 8));
            si("el llenado va a su tiempo (verso de 8 s, cada 10 ms): error maximo " + dif.ToString("0.0000", CultureInfo.InvariantCulture), dif < 1e-6);
            Console.WriteLine("== " + bien + " BIEN, " + mal + " MAL");
            return mal == 0 ? 0 : 1;
        }
    }
}
