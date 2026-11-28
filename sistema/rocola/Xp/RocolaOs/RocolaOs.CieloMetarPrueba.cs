// EL CIELO VIVO DE ROCOLA OS: LA ORDEN DE PRUEBA DEL METAR Y DE LAS COORDENADAS (Pruebas/RocolaOs/cielo/prueba.sh, parte «metar»).
// Sin red ni la rocola entera: lee METAR escritos como los da aviationweather.gov (el JSON de verdad, con «10+», «VRB», wxString nulo...) y comprueba
// el codigo de la OMM, la nubosidad, la lluvia, el viento, la visibilidad, la estacion mas cercana, la regla «el METAR fresco manda y Open-Meteo
// rellena», las coordenadas de la hoja y los textos de «de donde sale». Una linea por comprobacion (BIEN o MAL); sale con 0 si todo bien.
//   rocola-xp --prueba-cielo-metar
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Rocola
{
    internal static class RocolaOsCieloMetarPrueba
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra() { RocolaOsOrdenes.Si("--prueba-cielo-metar", Prueba); }

        private static int malas, buenas;
        private static void Si(string que, bool bien) { Console.WriteLine((bien ? "BIEN  " : "MAL   ") + que); if (bien) buenas++; else malas++; }
        private static RocolaOsMetarObs Una(string json) { using (JsonDocument d = JsonDocument.Parse(json)) return RocolaOsMetar.Lee(d.RootElement); }
        private static long Epoca(DateTime utc) { return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds; }
        private static string Rep(string id, double lat, double lon, DateTime cuando, string resto)
        { return "{\"icaoId\":\"" + id + "\",\"obsTime\":" + Epoca(cuando) + ",\"lat\":" + lat.ToString(CultureInfo.InvariantCulture) + ",\"lon\":" + lon.ToString(CultureInfo.InvariantCulture) + (resto.Length > 0 ? "," + resto : "") + "}"; }

        internal static int Prueba(string[] a)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            DateTime ahora = new DateTime(2026, 11, 27, 20, 0, 0, DateTimeKind.Utc);
            // ---- el METAR de verdad de Guadalajara (MMGL, 27-11-2026 19:47Z, «-RA FEW008 BKN010 OVC080 12/12», 2 SM, 150 a 9 kt) ----
            RocolaOsMetarObs g = Una("{\"icaoId\":\"MMGL\",\"obsTime\":1795808820,\"temp\":12,\"dewp\":12,\"wdir\":150,\"wspd\":9,\"visib\":2,\"wxString\":\"-RA\",\"lat\":20.523,\"lon\":-103.312,\"cover\":\"OVC\",\"clouds\":[{\"cover\":\"FEW\",\"base\":800},{\"cover\":\"BKN\",\"base\":1000},{\"cover\":\"OVC\",\"base\":8000}]}");
            Si("MMGL real: -RA es lluvia ligera (codigo 61) y OVC es cielo cubierto", g != null && g.Id == "MMGL" && g.Codigo == 61 && g.Nubes == 1f && g.Lluvia > 0.3f && g.Lluvia < 1f);
            Si("MMGL real: 9 kt = 16,7 km/h, de 150 grados, 12 C, 2 SM = 3219 m", g != null && Math.Abs(g.VientoKmh.Value - 16.668) < 0.01 && g.Direccion == 150 && g.Temp == 12 && Math.Abs(g.VisibilidadM.Value - 3218.7) < 1);
            Si("MMGL real: la hora sale de obsTime (19:47 UTC)", g != null && g.Cuando.Hour == 19 && g.Cuando.Minute == 47 && g.Cuando.Day == 27);
            RocolaOsClima c = g.AClima(null, ahora, 11.8f);
            Si("el clima de MMGL: lluvia, fuente metar, estacion MMGL a 12 km, medida 19:47, cielo cubierto", c.Tipo == RocolaOsClimaTipo.Lluvia && c.Fuente == "metar" && c.Estacion == "MMGL" && Math.Abs(c.Km - 11.8f) < 0.01f && c.Medida == g.Cuando && c.Nubes == 1f);
            // ---- el tiempo presente ----
            Func<string, int> cod = wx => { float l; return RocolaOsMetar.CodigoDeTiempo(wx, out l); };
            Si("RA moderada 63, +RA fuerte 65 (aguacero), -RA 61", cod("RA") == 63 && cod("+RA") == 65 && cod("-RA") == 61 && RocolaOsClima.DeCodigo(65, 1f) == RocolaOsClimaTipo.Aguacero);
            Si("DZ llovizna 51-55, FZDZ 56", cod("-DZ") == 51 && cod("DZ") == 53 && cod("+DZ") == 55 && cod("FZDZ") == 56 && RocolaOsClima.DeCodigo(53, 1f) == RocolaOsClimaTipo.Llovizna);
            Si("SHRA chubasco 80, +SHRA 82, FZRA 66", cod("-SHRA") == 80 && cod("SHRA") == 81 && cod("+SHRA") == 82 && cod("FZRA") == 66);
            Si("TS y TSRA y +TSRA son tormenta (95 o mas); VCTS (cerca, no aqui) no", cod("TS") >= 95 && cod("TSRA") >= 95 && cod("+TSRA") >= 95 && cod("VCTS") == -1 && RocolaOsClima.DeCodigo(cod("TSRA"), 1f) == RocolaOsClimaTipo.Tormenta);
            Si("SN nieve 71-75, -SHSN 85", cod("-SN") == 71 && cod("SN") == 73 && cod("+SN") == 75 && cod("-SHSN") == 85 && RocolaOsClima.DeCodigo(73, 1f) == RocolaOsClimaTipo.Nieve);
            Si("FG y BR son neblina (45), FZFG 48, MIFG y VCFG no", cod("FG") == 45 && cod("BR") == 45 && cod("FZFG") == 48 && cod("MIFG") == -1 && cod("VCFG") == -1 && RocolaOsClima.DeCodigo(45, 0.2f) == RocolaOsClimaTipo.Neblina);
            Si("la lluvia manda sobre la neblina (-RA BR) y la tormenta sobre la lluvia (TSRA BR)", cod("-RA BR") == 61 && cod("TSRA BR") >= 95);
            Si("HZ, FU, DU y RERA no cuentan; null y vacio tampoco", cod("HZ") == -1 && cod("FU") == -1 && cod("RERA") == -1 && cod("") == -1 && cod(null) == -1);
            Si("UP (precipitacion desconocida de un sensor automatico) es lluvia ligera", cod("UP") == 61);
            // ---- las nubes ----
            Si("cobertura: FEW 0,19, SCT 0,44, BKN 0,75, OVC y VV 1, CLR/SKC/NSC/CAVOK 0, otra cosa -1", RocolaOsMetar.Cobertura("FEW") == 0.19f && RocolaOsMetar.Cobertura("SCT") == 0.44f && RocolaOsMetar.Cobertura("BKN") == 0.75f
                && RocolaOsMetar.Cobertura("OVC") == 1f && RocolaOsMetar.Cobertura("VV") == 1f && RocolaOsMetar.Cobertura("CLR") == 0f && RocolaOsMetar.Cobertura("CAVOK") == 0f && RocolaOsMetar.Cobertura("XYZ") == -1f);
            RocolaOsMetarObs b = Una(Rep("MMXX", 20, -103, ahora, "\"wxString\":\"RA\",\"wspd\":4,\"wdir\":270,\"temp\":14,\"visib\":6,\"clouds\":[{\"cover\":\"FEW\",\"base\":2000},{\"cover\":\"BKN\",\"base\":3500}]"));
            Si("RA con BKN: lluvia (63) y nublado (0,75 de nubes)", b.Codigo == 63 && b.Nubes == 0.75f && RocolaOsClima.DeCodigo(b.Codigo, b.Nubes) == RocolaOsClimaTipo.Lluvia && b.AClima(null, ahora, 5f).Nubes == 0.75f);
            RocolaOsMetarObs d1 = Una(Rep("MMXX", 20, -103, ahora, "\"wxString\":null,\"wspd\":0,\"wdir\":0,\"temp\":22,\"visib\":\"10+\",\"clouds\":[]"));
            Si("sin tiempo y sin nubes: despejado (codigo 0); visibilidad «10+» = 16,1 km; calma sin direccion", d1.Codigo == 0 && d1.Nubes == 0f && d1.VisibilidadM.Value > 16000 && d1.VientoKmh == 0 && !d1.Direccion.HasValue);
            RocolaOsMetarObs d2 = Una(Rep("MMXX", 20, -103, ahora, "\"wxString\":null,\"wspd\":6,\"wdir\":\"VRB\",\"temp\":22,\"visib\":\"10+\",\"clouds\":[{\"cover\":\"SCT\",\"base\":5000}]"));
            Si("viento variable (VRB): sin direccion; SCT: poco nublado (codigo 2)", !d2.Direccion.HasValue && d2.Codigo == 2 && RocolaOsClima.DeCodigo(d2.Codigo, d2.Nubes) == RocolaOsClimaTipo.PocoNublado);
            RocolaOsMetarObs d3 = Una(Rep("MMXX", 20, -103, ahora, "\"wxString\":\"FG\",\"visib\":0.25,\"temp\":3,\"clouds\":[{\"cover\":\"VV\",\"base\":100}]"));
            RocolaOsClima c3 = d3.AClima(null, ahora, 3f);
            Si("FG con VV y 0,25 SM: neblina (45) con 402 m de visibilidad; sin viento en el METAR ni Open-Meteo: 0 km/h", c3.Tipo == RocolaOsClimaTipo.Neblina && Math.Abs(c3.Visibilidad - 402.3f) < 1 && c3.Viento == 0f);
            Si("un reporte sin estacion o sin hora no vale", Una("{\"obsTime\":1795808820}") == null && Una("{\"icaoId\":\"MMGL\"}") == null);
            // ---- el relleno de Open-Meteo ----
            RocolaOsClima om = new RocolaOsClima(RocolaOsClimaTipo.Despejado, 0, 0.05f, 0f, 22f, 245f, 25f, 24000f, true, ahora.AddMinutes(-10), false);
            RocolaOsClima r = d2.AClima(om, ahora, 8f);
            Si("el relleno: viento variable toma la direccion de Open-Meteo (245), lo que el METAR trae (6 kt = 11,1 km/h) no se pisa", Math.Abs(r.Direccion - 245f) < 0.01f && Math.Abs(r.Viento - 11.112f) < 0.01f);
            RocolaOsClima rOld = d2.AClima(new RocolaOsClima(RocolaOsClimaTipo.Despejado, 0, 0f, 0f, 22f, 245f, 25f, 24000f, true, ahora.AddMinutes(-150), false), ahora, 8f);
            Si("un Open-Meteo de hace 150 min ya no rellena (direccion neutra 270)", Math.Abs(rOld.Direccion - 270f) < 0.01f);
            Si("lo que el METAR dice manda sobre Open-Meteo: lluvia del METAR con un Open-Meteo despejado", b.AClima(om, ahora, 5f).Tipo == RocolaOsClimaTipo.Lluvia && b.AClima(om, ahora, 5f).Temperatura == 14f);
            // ---- la frescura ----
            RocolaOsMetarObs v89 = Una(Rep("MMXX", 20, -103, ahora.AddMinutes(-89), "")), v95 = Una(Rep("MMXX", 20, -103, ahora.AddMinutes(-95), "")), vFut = Una(Rep("MMXX", 20, -103, ahora.AddMinutes(30), ""));
            Si("fresco: 89 min si, 95 min no, uno del futuro (reloj mal puesto) no", v89.Fresco(ahora) && !v95.Fresco(ahora) && !vFut.Fresco(ahora));
            // ---- la estacion mas cercana ----
            double bl = 20.60, bo = -103.30;
            Si("la distancia: 0,1 grado de latitud = 11,1 km; el mismo punto, 0", Math.Abs(RocolaOsMetar.Km(20, -103, 20.1, -103) - 11.12) < 0.05 && RocolaOsMetar.Km(bl, bo, bl, bo) == 0);
            List<RocolaOsMetarObs> l = new List<RocolaOsMetarObs>();
            l.Add(Una(Rep("MMA1", 20.70, -103.30, ahora.AddMinutes(-20), "\"wxString\":\"-RA\"")));            // a 11 km
            l.Add(Una(Rep("MMA1", 20.70, -103.30, ahora.AddMinutes(-80), "\"wxString\":null")));              // (uno viejo de la misma)
            l.Add(Una(Rep("MMB2", 20.62, -103.30, ahora.AddMinutes(-200), "")));                              // a 2 km pero sin nada fresco
            l.Add(Una(Rep("MMC3", 21.20, -103.30, ahora.AddMinutes(-5), "")));                                // a 67 km
            double km; RocolaOsMetarObs m = RocolaOsMetar.LaMasCercana(l, bl, bo, ahora, out km);
            Si("la estacion: MMA1 (11 km, con METAR fresco) gana a la de 2 km sin reporte fresco y a la de 67 km; su reporte es el mas nuevo (-RA)", m != null && m.Id == "MMA1" && Math.Abs(km - 11.1) < 0.2 && m.Codigo == 61);
            l.RemoveAt(0); l.RemoveAt(0);
            Si("ninguna estacion a 40 km o menos con METAR fresco: null", RocolaOsMetar.LaMasCercana(l, bl, bo, ahora, out km) == null);
            string cuadro = RocolaOsMetar.Cuadro(20.60, -103.30); string[] pc = cuadro.Split(',');
            Si("el cuadro de busqueda (" + cuadro + "): lat0,lon0,lat1,lon1, +-0,45 de latitud, y la longitud ensanchada por el coseno", pc.Length == 4 && Math.Abs(double.Parse(pc[0], ci) - 20.15) < 1e-3 && Math.Abs(double.Parse(pc[2], ci) - 21.05) < 1e-3 && double.Parse(pc[1], ci) < -103.7 && double.Parse(pc[3], ci) > -102.9);
            // ---- la estacion recordada ----
            RocolaOsMetar.Estacion e = new RocolaOsMetar.Estacion { Id = "MMGL", Lat = 20.523, Lon = -103.312, BarLat = 20.6, BarLon = -103.3, Buscada = ahora };
            RocolaOsMetar.Estacion e2 = RocolaOsMetar.Estacion.DeTexto(e.Texto());
            Si("rocola_clima_estacion.txt: lo escrito se lee igual", e2 != null && e2.Id == "MMGL" && Math.Abs(e2.Lat - 20.523) < 1e-4 && e2.Buscada == ahora);
            Si("se recuerda una semana: a los 6 dias no se busca otra, a los 8 si", !e.Vieja(ahora.AddDays(6), 20.6, -103.3) && e.Vieja(ahora.AddDays(8), 20.6, -103.3));
            Si("si el bar se mueve mas de ~1 km (0,02 grados) o la estacion queda a mas de 40 km, se busca otra", e.Vieja(ahora, 20.62, -103.3) && e.Vieja(ahora, 21.2, -103.3) && !e.Vieja(ahora, 20.605, -103.3));
            RocolaOsMetar.Estacion ninguna = new RocolaOsMetar.Estacion { Id = "", BarLat = 20.6, BarLon = -103.3, Buscada = ahora };
            Si("«ninguna cerca» se vuelve a mirar a las 6 h, no antes", !ninguna.Vieja(ahora.AddHours(5), 20.6, -103.3) && ninguna.Vieja(ahora.AddHours(7), 20.6, -103.3));
            // ---- rocola_clima.txt con la fuente ----
            RocolaOsClima c2 = RocolaOsClima.DeTexto(c.Texto(20.6, -103.3));
            Si("rocola_clima.txt guarda y lee la fuente (metar, MMGL, km) y lo de siempre", c2 != null && c2.Fuente == "metar" && c2.Estacion == "MMGL" && Math.Abs(c2.Km - 11.8f) < 0.05f && c2.Tipo == RocolaOsClimaTipo.Lluvia && c2.Medida == c.Medida);
            RocolaOsClima c4 = RocolaOsClima.DeTexto(om.Texto(20.6, -103.3));
            Si("el de Open-Meteo no lleva fuente (los archivos de antes se leen igual)", c4 != null && c4.Fuente == "" && !om.Texto(20.6, -103.3).Contains("fuente="));
            // ---- las coordenadas de la hoja ----
            double v1, v2;
            Si("coordenadas: 20.6597 y -103.3496 valen; la coma decimal (-103,3496) tambien", RocolaOsMetar.Coordenada("20.6597", 90, out v1) && RocolaOsMetar.Coordenada("-103,3496", 180, out v2) && Math.Abs(v2 + 103.3496) < 1e-9);
            Si("coordenadas: 91 de latitud, 181 de longitud, vacio, letras, NaN e infinito no valen", !RocolaOsMetar.Coordenada("91", 90, out v1) && !RocolaOsMetar.Coordenada("-181", 180, out v1) && !RocolaOsMetar.Coordenada("", 90, out v1)
                && !RocolaOsMetar.Coordenada("norte", 90, out v1) && !RocolaOsMetar.Coordenada("NaN", 90, out v1) && !RocolaOsMetar.Coordenada("Infinity", 90, out v1) && !RocolaOsMetar.Coordenada(null, 90, out v1));
            Si("coordenadas: los limites (90 y -180) valen", RocolaOsMetar.Coordenada("90", 90, out v1) && RocolaOsMetar.Coordenada("-180", 180, out v1));
            double pa, po;
            Si("pegar «20.6597, -103.3496» de un mapa da las dos; tres numeros, uno solo o fuera de rango no", RocolaOsMetar.Pareja("20.6597, -103.3496", out pa, out po) && Math.Abs(pa - 20.6597) < 1e-9 && Math.Abs(po + 103.3496) < 1e-9 && !RocolaOsMetar.Pareja("1 2 3", out pa, out po) && !RocolaOsMetar.Pareja("20.6", out pa, out po) && !RocolaOsMetar.Pareja("95, 10", out pa, out po));
            // ---- los textos de la hoja ----
            Si("hoja: observado en MMGL (12 km) hace 18 min", RocolaOsCielo.FuenteClima(c.AClimaConMedida(ahora.AddMinutes(-18)), ahora) == "Observado en el aeropuerto MMGL (12 km) hace 18 min.");
            Si("hoja: pronostico de Open-Meteo (de hace 10 min, sin METAR)", RocolaOsCielo.FuenteClima(om, ahora).StartsWith("Pronóstico de Open-Meteo para ", StringComparison.Ordinal));
            Si("hoja: sin internet, el de hace 1 h (Open-Meteo de hace 60 min)", RocolaOsCielo.FuenteClima(new RocolaOsClima(RocolaOsClimaTipo.Despejado, 0, 0f, 0f, 0f, 0f, 20f, float.NaN, true, ahora.AddMinutes(-60), false), ahora) == "Sin internet: el de hace 60 min.");
            Si("hoja: sin internet, un METAR de hace 2 h", RocolaOsCielo.FuenteClima(c.AClimaConMedida(ahora.AddMinutes(-120)), ahora).StartsWith("Sin internet: el último, observado en el aeropuerto MMGL", StringComparison.Ordinal));
            Console.WriteLine((malas == 0 ? "TODO BIEN" : "HAY FALLOS") + " (" + buenas + " bien, " + malas + " mal)");
            return malas == 0 ? 0 : 1;
        }
    }
}
