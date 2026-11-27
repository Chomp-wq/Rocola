// ROCOLA OS: LA CADENA NUEVA DEL BILLAR (B), SUS PIEZAS (peticion del dueno del 27-11-2026, plan aprobado el mismo dia: CADENA_NUEVA.md §3; lo que
// es cada pieza, por que cada numero y sus pruebas: CADENA_NUEVA_B.md). Solo Rocola OS; sin ganchos. EL CONTRATO DEL MARCO (CADENA_NUEVA.md §8.1,
// RocolaOs.CadenaNuevaPieza.cs): aqui se pone RocolaOsCadenaB.Arma y RocolaOsCadenaB.Huella; el marco (RocolaOs.CadenaNueva*.cs) los llama donde
// va el tramo B de su receta:
//   (arreglos fijos de la medida: el canal mudo copiado, el invertido ya volteado, la continua, el zumbido) -> [ESTE TRAMO, una pieza: mono ->
//   paso alto (y el corte de noche) -> curva del billar -> bajo virtual] -> ecualizador del dueno (memoria del billar) -> ganancia de la cancion
//   -> noche (su volumen y sus graves de 120 Hz; SU CORTE DE 80 HZ NO: ya va aqui, en la misma curva) -> fundido -> salida.
//
// LA FUNCION «billar_nuevo» (APAGADA de fabrica): la hojita «Billar» de Ctrl+F4 (RocolaOs.Billar2Hoja.cs) siempre a la vista; sin ella, la hojita
// sale cuando la cadena nueva esta elegida (cadena_nueva=1, la hojita «Cadena» del marco). El tramo B no depende de ella: lo usa el marco cuando
// suena la B (la cadena nueva con el modo billar). Sus ajustes (rocola_os.txt; se leen cada vez que se arma una cancion: un cambio vale desde la
// siguiente, que es la que se vuelve a medir: la huella cambia):
//   billar_paso_alto=80           Hz, de 50 a 150: donde las bocinas dejan de sonar (80 hasta medirlo; el paso alto de 24 dB por octava)
//   billar_inclinacion=-0.75      dB por octava sobre 1 kHz, de 0 a -1.5 (la curva objetivo)
//   billar_claridad_graves=2      dB que se quitan en 250-400 Hz, de 0 a 4
//   billar_claridad_agudos=2      dB que se ponen en 2-4 kHz, de 0 a 3
//   billar_afinacion=1            usar lo medido con el celular (si hay al menos un punto con las bandas de la voz)
//   billar_afina_pond=C           la app del celular mide en dBA (A) o dBC (C)
//   billar_afina_1= ... _8=       lo que marco la app en cada punto: «63;125;250;500;1k;2k;4k;8k» en dB, «x» = sin anotar
//   billar_bajo_virtual=0         el bajo virtual fijo (decide la prueba ciega)
//   billar_bajo_virtual_db=-8     su mezcla, de -14 a -4 dB
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Rocola
{
    internal static class RocolaOsBillar2
    {
        internal const string Nombre = "billar_nuevo";
        internal const string KPasoAlto = "billar_paso_alto", KInclinacion = "billar_inclinacion", KClaridadGraves = "billar_claridad_graves",
            KClaridadAgudos = "billar_claridad_agudos", KAfinacion = "billar_afinacion", KPond = "billar_afina_pond", KPunto = "billar_afina_",
            KBajo = "billar_bajo_virtual", KBajoDb = "billar_bajo_virtual_db";

        // un decimal de un rango (invariante: «-0.75»)
        internal static bool DecimalVale(string v, double min, double max)
        {
            double d;
            return v != null && v.Length > 0 && v.Length <= 12 && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d) && !double.IsNaN(d) && d >= min - 1e-9 && d <= max + 1e-9;
        }
        internal static string Decimal(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
        private static double LeeDecimal(string clave, double deFabrica, double min, double max)
        {
            double d;
            string v = RocolaOs.Ajuste(clave);
            if (v == null || !double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d) || double.IsNaN(d)) return deFabrica;
            return Math.Max(min, Math.Min(max, d));
        }

        internal static RocolaOsClave[] Claves()
        {
            RocolaOsClave[] c = new RocolaOsClave[9 + RocolaOsBillar2Afinacion.Puntos];
            int i = 0;
            c[i++] = RocolaOsClave.Entero(KPasoAlto, (int)RocolaOsBillar2Curva.PasoAltoDeFabrica, (int)RocolaOsBillar2Curva.PasoAltoMin, (int)RocolaOsBillar2Curva.PasoAltoMax, true,
                "cadena nueva del billar: el paso alto en Hz, donde las bocinas dejan de sonar (24 dB por octava, fase lineal; vale desde la siguiente cancion)");
            c[i++] = RocolaOsClave.Texto(KInclinacion, Decimal(RocolaOsBillar2Curva.InclinacionDeFabrica), v => DecimalVale(v, RocolaOsBillar2Curva.InclinacionMin, RocolaOsBillar2Curva.InclinacionMax), true,
                "cadena nueva del billar: la curva objetivo, dB por octava sobre 1 kHz (de 0 a -1.5)");
            c[i++] = RocolaOsClave.Texto(KClaridadGraves, Decimal(RocolaOsBillar2Curva.ClaridadMenosDeFabrica), v => DecimalVale(v, 0, RocolaOsBillar2Curva.ClaridadMenosMax), true,
                "cadena nueva del billar: dB que se quitan en 250-400 Hz (claridad de la voz; de 0 a 4)");
            c[i++] = RocolaOsClave.Texto(KClaridadAgudos, Decimal(RocolaOsBillar2Curva.ClaridadMasDeFabrica), v => DecimalVale(v, 0, RocolaOsBillar2Curva.ClaridadMasMax), true,
                "cadena nueva del billar: dB que se ponen en 2-4 kHz (claridad de la voz; de 0 a 3)");
            c[i++] = RocolaOsClave.SiNo(KAfinacion, true, true, "cadena nueva del billar: usar la afinacion medida con el celular (si hay un punto con las bandas de la voz)");
            c[i++] = RocolaOsClave.Texto(KPond, "C", v => v == "A" || v == "C", true, "cadena nueva del billar: la app del celular mide en dBA (A) o dBC (C)");
            for (int p = 1; p <= RocolaOsBillar2Afinacion.Puntos; p++)
                c[i++] = RocolaOsClave.Texto(KPunto + p.ToString(CultureInfo.InvariantCulture), "", RocolaOsBillar2Afinacion.TextoVale, true,
                    "cadena nueva del billar: lo que marco la app en el punto " + p + " (63;125;250;500;1k;2k;4k;8k en dB, x = sin anotar)");
            c[i++] = RocolaOsClave.SiNo(KBajo, false, true, "cadena nueva del billar: el bajo virtual fijo (|x| del grave que las bocinas no dan; decide la prueba ciega)");
            c[i++] = RocolaOsClave.Entero(KBajoDb, (int)RocolaOsBillar2Bajo.MezclaDeFabricaDb, (int)RocolaOsBillar2Bajo.MezclaMinDb, (int)RocolaOsBillar2Bajo.MezclaMaxDb, true,
                "cadena nueva del billar: la mezcla del bajo virtual en dB (de -14 a -4)");
            return c;
        }

        // LA AFINACION GUARDADA
        internal static RocolaOsBillar2Afinacion AfinacionDeAjustes()
        {
            RocolaOsBillar2Afinacion a = new RocolaOsBillar2Afinacion();
            a.Pond = RocolaOs.Ajuste(KPond) == "A" ? 0 : 1;
            for (int p = 0; p < RocolaOsBillar2Afinacion.Puntos; p++)
                a.Lecturas[p] = RocolaOsBillar2Afinacion.Lee(RocolaOs.Ajuste(KPunto + (p + 1).ToString(CultureInfo.InvariantCulture)));
            return a;
        }
        // LOS PARAMETROS DE AHORA (sin la noche: la pone quien arma)
        internal static RocolaOsBillar2Params ParamsDeAjustes()
        {
            RocolaOsBillar2Params p = new RocolaOsBillar2Params();
            int hz = RocolaOs.AjusteEntero(KPasoAlto);
            p.PasoAltoHz = hz >= RocolaOsBillar2Curva.PasoAltoMin && hz <= RocolaOsBillar2Curva.PasoAltoMax ? hz : RocolaOsBillar2Curva.PasoAltoDeFabrica;
            p.Inclinacion = LeeDecimal(KInclinacion, RocolaOsBillar2Curva.InclinacionDeFabrica, RocolaOsBillar2Curva.InclinacionMin, RocolaOsBillar2Curva.InclinacionMax);
            p.ClaridadMenos = LeeDecimal(KClaridadGraves, RocolaOsBillar2Curva.ClaridadMenosDeFabrica, 0, RocolaOsBillar2Curva.ClaridadMenosMax);
            p.ClaridadMas = LeeDecimal(KClaridadAgudos, RocolaOsBillar2Curva.ClaridadMasDeFabrica, 0, RocolaOsBillar2Curva.ClaridadMasMax);
            p.Afinacion = RocolaOs.AjusteSiNo(KAfinacion) ? AfinacionDeAjustes().Correccion() : null;
            p.BajoVirtual = RocolaOs.AjusteSiNo(KBajo);
            int db = RocolaOs.AjusteEntero(KBajoDb);
            p.BajoVirtualDb = db >= RocolaOsBillar2Bajo.MezclaMinDb && db <= RocolaOsBillar2Bajo.MezclaMaxDb ? db : RocolaOsBillar2Bajo.MezclaDeFabricaDb;
            return p;
        }
        // LA HUELLA DE AHORA (la de la medida: si cambia, la medida de la salida de una cancion ya no vale)
        internal static string Huella() { return ParamsDeAjustes().Huella(); }

        // EL DISENO DE FONDO (al arrancar y al cambiar un ajuste): asi quien arma la cadena casi siempre lo encuentra hecho
        private static int precalentando;
        internal static void Precalienta(int tasa)
        {
            if (Interlocked.Exchange(ref precalentando, 1) == 1) return;
            try
            {
                RocolaOsBillar2Params p = ParamsDeAjustes(), m = p.Copia();
                p.NocheHz = Form1.NocheCorteHz();   // (el de sonar; el de medir va sin noche: si el corte de noche no pasa del paso alto, es el mismo)
                RocolaOs.Hilo("billar2-diseno", delegate
                {
                    try
                    {
                        RocolaOsBillar2Diseno d = RocolaOsBillar2Diseno.Para(tasa, p);
                        RocolaOsBillar2Diseno.Para(tasa, m);
                        RocolaOs.Apunta(Nombre, "diseno " + tasa + " Hz hecho: K=" + d.K + " N=" + d.N + " (" + d.MsDiseno.ToString("0", CultureInfo.InvariantCulture) + " ms) " + d.Huella);
                    }
                    finally { Volatile.Write(ref precalentando, 0); }
                });
            }
            catch (Exception ex) { Volatile.Write(ref precalentando, 0); RocolaOs.Falla(Nombre, "precalentar el diseno", ex); }
        }
    }

    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraBillar2()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsBillar2.Nombre,
                Que = "la hojita «Billar» de Ctrl+F4 siempre a la vista (las piezas de la cadena nueva del billar, B: CADENA_NUEVA.md §3; sin ella, la hojita sale con la cadena nueva elegida)",
                DeFabrica = false,
                Claves = RocolaOsBillar2.Claves(),
                Arranca = f => RocolaOsBillar2.Precalienta(44100),
                AlCambiar = (f, clave) => RocolaOsBillar2.Precalienta(44100),
                Vivo = (f, sb) => sb.Append(RocolaOsBillar2.Nombre).Append("=encendida huella=").Append(RocolaOsBillar2.Huella()).Append('\n'),
            });
            // EL CONTRATO DEL MARCO (CADENA_NUEVA.md §8.1)
            RocolaOsCadenaB.Arma = RocolaOsBillar2ArmaB;
            RocolaOsCadenaB.Huella = RocolaOsBillar2.Huella;
            RocolaOsOrdenes.Si("--prueba-billar2", RocolaOsBillar2Prueba.Corre);
        }

        // RocolaOsCadenaB.Arma: el tramo B de una receta (una sola pieza, anotada). La entrada ya trae el invertido volteado (por los arreglos del
        // marco): el mono no voltea nada. Midiendo: de dia y sin nada de XP (los ajustes son de Rocola OS, una foto; la noche no se mide).
        private static NAudio.Wave.ISampleProvider RocolaOsBillar2ArmaB(RocolaOsReceta r, NAudio.Wave.ISampleProvider entrada)
        {
            if (r == null || entrada == null) return null;
            RocolaOsBillar2Params p = RocolaOsBillar2.ParamsDeAjustes();
            p.NocheHz = r.Midiendo ? 0 : NocheCorteHz();
            RocolaOsBillar2Diseno d = RocolaOsBillar2Diseno.Para(entrada.WaveFormat.SampleRate, p);
            return r.Anota(new RocolaOsBillar2Tramo(entrada, d, false, r.Midiendo ? null : (Func<double>)RocolaOsBillar2MezclaNoche));
        }

        // EL TRAMO B SIN RECETA (las pruebas y quien no use el marco): lo que dejaron los arreglos fijos (estereo o un canal, float, cualquier tasa
        // de 8 kHz para arriba) -> el tramo. voltearDerecho: un canal viene al reves y nadie lo volteo antes (el mono hace (L - R)/2). paraMedir: de
        // dia, sin la mezcla de noche. Nunca lanza: si algo falla, el origen tal cual (y se apunta). Disco continuo: tramo.Sigue(el de la anterior).
        internal static NAudio.Wave.ISampleProvider RocolaOsBillar2Arma(NAudio.Wave.ISampleProvider src, bool voltearDerecho, bool paraMedir)
        {
            if (src == null) return null;
            try
            {
                RocolaOsBillar2Params p = RocolaOsBillar2.ParamsDeAjustes();
                p.NocheHz = NocheCorteHz();
                RocolaOsBillar2Diseno d = RocolaOsBillar2Diseno.Para(src.WaveFormat.SampleRate, p);
                return new RocolaOsBillar2Tramo(src, d, voltearDerecho, paraMedir ? null : (Func<double>)RocolaOsBillar2MezclaNoche);
            }
            catch (Exception ex) { RocolaOs.Falla(RocolaOsBillar2.Nombre, "armar el tramo B", ex); return src; }
        }
        private static double RocolaOsBillar2MezclaNoche() { return GraveMono.NocheMezcla; }
    }
}
