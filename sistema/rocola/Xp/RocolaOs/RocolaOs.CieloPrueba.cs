// EL CIELO VIVO DE ROCOLA OS, NUCLEO: LA ORDEN DE PRUEBA DE LAS CUENTAS (Pruebas/RocolaOs/cielo/prueba.sh). Sin la rocola (solo las cuentas
// puras: el sol, la luna, la fase del dia, las paletas, el calendario, rocola_fechas.txt, el clima y el lugar), con la pantalla fuera
// (ROCOLA_PANTALLA=fuera:...: las ordenes de RocolaOs/ corren despues de abrirla). La zona de la hora es la del sistema (TZ=America/Mexico_City
// en la prueba, como /etc/localtime en Rocola OS). Escribe una linea por comprobacion o por dato (la prueba los compara con lo esperado y con
// el algoritmo de la NOAA de astral); sale con 0 si todo lo que comprueba ella misma salio bien.
//   rocola-xp --prueba-cielo
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Rocola
{
    internal static partial class RocolaOsCielo
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RegistraPrueba() { RocolaOsOrdenes.Si("--prueba-cielo", Prueba); }

        private static int malas, buenas;
        private static void Si(string que, bool bien) { Console.WriteLine((bien ? "BIEN  " : "MAL   ") + que); if (bien) buenas++; else malas++; }
        private static string H(DateTime? d) { return d.HasValue ? d.Value.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "-"; }
        private static string F(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }

        internal static int Prueba(string[] a)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            // ---- el sol: salidas y puestas (DATO: la prueba las compara con astral, +-3 min) ----
            string[] cds = { "cdmx", "monterrey" };
            double[] la = { 19.43, 25.69 }, lo = { -99.13, -100.32 };
            string[] fechas = { "2026-01-15", "2026-03-20", "2026-06-21", "2026-09-22", "2026-11-18", "2026-11-20", "2026-12-21" };
            for (int ic = 0; ic < cds.Length; ic++)
                foreach (string fs in fechas)
                {
                    DateTime d = DateTime.ParseExact(fs, "yyyy-MM-dd", ci);
                    Console.WriteLine("SOL " + cds[ic] + " " + fs + " " + la[ic].ToString(ci) + " " + lo[ic].ToString(ci) + " sale " + H(RocolaOsAstro.SalidaOPuesta(d, la[ic], lo[ic], true))
                        + " pone " + H(RocolaOsAstro.SalidaOPuesta(d, la[ic], lo[ic], false)));
                }
            // ---- la luna: la fase en lunas llenas y nuevas conocidas de 2026 (las de los eclipses) ----
            Fase("luna nueva 2026-02-17 12:01 UTC (eclipse anular)", new DateTime(2026, 2, 17, 12, 1, 0, DateTimeKind.Utc), 0.0);
            Fase("luna llena 2026-03-03 11:38 UTC (eclipse total)", new DateTime(2026, 3, 3, 11, 38, 0, DateTimeKind.Utc), 0.5);
            Fase("luna nueva 2026-08-12 17:37 UTC (eclipse total)", new DateTime(2026, 8, 12, 17, 37, 0, DateTimeKind.Utc), 0.0);
            Fase("luna llena 2026-08-28 04:18 UTC (eclipse parcial)", new DateTime(2026, 8, 28, 4, 18, 0, DateTimeKind.Utc), 0.5);
            {
                double alt, az, f; RocolaOsAstro.Luna(new DateTime(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc), 19.43, -99.13, out alt, out az, out f);
                double xp = Form1.FaseLunar(new DateTime(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc));
                Si("la fase de la luna del nucleo y la de XP (Form1.FaseLunar) se llevan menos de 0,03 (" + F(f) + " y " + F(xp) + ")", Dist(f, xp) < 0.03);
            }
            // ---- la fase del dia: los pesos suman 1, sin saltos, y donde manda cada paleta ----
            float[] p = new float[RocolaOsCieloDia.Fases], q = new float[RocolaOsCieloDia.Fases];
            double salto = 0, suma = 0;
            for (int s = 0; s < 2; s++)
            {
                bool sube = s == 0;
                RocolaOsCieloDia.Pesos(-30, sube, q);
                for (double alt = -30; alt <= 80; alt += 0.01)
                {
                    RocolaOsCieloDia.Pesos(alt, sube, p);
                    float t = 0; for (int i = 0; i < p.Length; i++) { t += p[i]; salto = Math.Max(salto, Math.Abs(p[i] - q[i])); }
                    suma = Math.Max(suma, Math.Abs(t - 1));
                    Array.Copy(p, q, p.Length);
                }
            }
            Si("los pesos de las paletas suman 1 (lo mas que se separan: " + suma.ToString("0.000000", ci) + ")", suma < 1e-5);
            Si("sin saltos: de una centesima de grado a otra, lo mas que cambia un peso es " + salto.ToString("0.0000", ci), salto < 0.005);
            Manda(-20, true, "noche"); Manda(-12.001, true, "noche"); Manda(-8, true, "alba"); Manda(-0.5, true, "amanecer"); Manda(20, true, "manana");
            Manda(50, true, "mediodia"); Manda(50, false, "mediodia"); Manda(20, false, "manana"); Manda(9, false, "dorada"); Manda(-0.5, false, "atardecer");
            Manda(-8, false, "anochecer"); Manda(-12.001, false, "noche");
            // ---- la regla 3: ninguna paleta pasa de 0,17 hasta 0,848 del alto (con el horizonte de las maquetas, 0,868, y con el de la pared, el
            //      escaparate y el disco de rocola-xp en 1024x768: la raya en 667) ----
            foreach (float hz in new float[] { 0.868f, 667f / 768f })
                for (int f = 1; f < RocolaOsCieloDia.Fases; f++)
                {
                    double l = RocolaOsCieloDia.LuzMaxima(f, hz, RocolaOsCieloDia.Regla3Hasta);
                    Si("regla 3: " + RocolaOsCieloDia.Nombres[f] + " con el horizonte en " + hz.ToString("0.000", ci) + ": luz maxima hasta 0,848 = " + F(l), l <= RocolaOsCieloDia.Regla3Luz);
                }
            // ---- y la mezcla de cada minuto de cuatro dias en las dos ciudades (pesos x paletas, sin la noche de XP: lo mas claro posible) ----
            double peor = 0; string donde = "";
            foreach (string fs in new[] { "2026-03-20", "2026-06-21", "2026-11-18", "2026-12-21" })
                for (int ic = 0; ic < cds.Length; ic++)
                    for (int m = 0; m < 24 * 60; m += 1)
                    {
                        DateTime local = DateTime.SpecifyKind(DateTime.ParseExact(fs, "yyyy-MM-dd", ci).AddMinutes(m), DateTimeKind.Local);
                        double alt, az, a2, z2; RocolaOsAstro.Sol(local.ToUniversalTime(), la[ic], lo[ic], out alt, out az);
                        RocolaOsAstro.Sol(local.ToUniversalTime().AddMinutes(10), la[ic], lo[ic], out a2, out z2);
                        RocolaOsCieloDia.Pesos(alt, a2 > alt, p);
                        double l = LuzMezcla(p, 667f / 768f, RocolaOsCieloDia.Regla3Hasta);
                        if (l > peor) { peor = l; donde = cds[ic] + " " + local.ToString("yyyy-MM-dd HH:mm", ci); }
                    }
            Si("regla 3 con las mezclas de cada minuto (cdmx y monterrey, 4 dias): lo mas claro hasta 0,848 = " + F(peor) + " (" + donde + ")", peor <= RocolaOsCieloDia.Regla3Luz);
            // en el reposo con musica el horizonte sube con el panel (la raya en 525 de 768): lo claro sube con el; ahi lo que se mide es lo de
            // encima de la franja clara, hasta 0,848/0,868 del horizonte (DATO y comprobacion aparte: la regla 3 habla de la pared)
            {
                float hr = 525f / 768f; double mxr = 0; string pr = "";
                for (int f = 1; f < RocolaOsCieloDia.Fases; f++)
                {
                    float[] uno = new float[RocolaOsCieloDia.Fases]; uno[f] = 1f;
                    double l = LuzMezcla(uno, hr, RocolaOsCieloDia.Regla3Hasta / RocolaOsCieloDia.HorizonteMaqueta * hr);
                    if (l > mxr) { mxr = l; pr = RocolaOsCieloDia.Nombres[f]; }
                }
                Si("reposo con musica (horizonte 525/768): hasta 0,848/0,868 del horizonte lo mas claro es " + F(mxr) + " (" + pr + ")", mxr <= RocolaOsCieloDia.Regla3Luz);
                double lit = 0;
                for (int f = 1; f < RocolaOsCieloDia.Fases; f++) lit = Math.Max(lit, RocolaOsCieloDia.LuzMaxima(f, hr, RocolaOsCieloDia.Regla3Hasta));
                Console.WriteLine("DATO reposo con musica: hasta 0,848 del alto (literal, ya bajo el horizonte, detras del panel) lo mas claro es " + F(lit));
            }
            // ---- la fase a las horas de las escenas (DATO) ----
            foreach (string hh in new[] { "06:50", "09:30", "13:00", "17:40", "18:35", "22:00", "23:30" })
            {
                DateTime local = DateTime.SpecifyKind(DateTime.ParseExact("2026-11-18 " + hh, "yyyy-MM-dd HH:mm", ci), DateTimeKind.Local);
                double alt, az, a2, z2; RocolaOsAstro.Sol(local.ToUniversalTime(), 19.43, -99.13, out alt, out az);
                RocolaOsAstro.Sol(local.ToUniversalTime().AddMinutes(10), 19.43, -99.13, out a2, out z2);
                RocolaOsCieloDia.Pesos(alt, a2 > alt, p);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < p.Length; i++) if (p[i] > 0) sb.Append(RocolaOsCieloDia.Nombres[i]).Append('=').Append(p[i].ToString("0.00", ci)).Append(' ');
                Console.WriteLine("FASE cdmx 2026-11-18 " + hh + " sol " + alt.ToString("0.0", ci) + " " + sb.ToString().Trim());
            }
            // ---- el calendario (DATO: la prueba lo compara con §6) ----
            DateTime d0 = new DateTime(2026, 1, 1);
            for (int i = 0; i < 365; i++)
            {
                DateTime d = d0.AddDays(i);
                RocolaOsCalendarioHoy h = RocolaOsCalendario.De(d.AddHours(20), RocolaOsFechasArchivo.Vacio);
                if (h.Fiestas.Length > 0) Console.WriteLine("FIESTA " + d.ToString("yyyy-MM-dd", ci) + " " + h.Nombres);
            }
            foreach (int anio in new[] { 2025, 2026, 2027, 2028 }) Console.WriteLine("PADRE " + anio + " " + RocolaOsCalendario.TercerDomingoDeJunio(anio).ToString("yyyy-MM-dd", ci));
            // los fuegos: el Grito y Ano Nuevo
            Fuegos("2026-09-15 20:00", "2026-09-15 23:00", 0); Fuegos("2026-09-15 23:03", "2026-09-15 23:00", 0); Fuegos("2026-09-15 23:05", "-", 0);
            Fuegos("2026-12-31 23:59", "2027-01-01 00:00", 10); Fuegos("2027-01-01 00:02", "2027-01-01 00:00", 10); Fuegos("2026-07-04 23:00", "-", 0);
            // ---- rocola_fechas.txt ----
            string texto = "# fechas del bar\n15-03;cumple;Juan\n02-08;aniversario;El Billar\r\n31-10;quitar;muertos\n12-10;fiesta;Mi fiesta|#ff0000,#ffffff\n"
                + "16-09;mensaje;¡Viva México, cabrones!\n15-09;fuegos;22:30\n01-01;fuegos;no\n30-02;cumple;Nadie\n12-13;cumple;Nadie\nhola\n10-05;cumple;\n20-11;quitar;\n";
            RocolaOsFechasArchivo fa = RocolaOsFechasArchivo.Lee(texto);
            Si("rocola_fechas.txt: 8 renglones buenos (" + fa.Renglones.Length + ") y 4 malos (" + fa.Malos.Length + ": " + string.Join(" | ", fa.Malos) + ")", fa.Renglones.Length == 8 && fa.Malos.Length == 4);
            RocolaOsCalendarioHoy x = RocolaOsCalendario.De(new DateTime(2026, 3, 15, 21, 0, 0), fa);
            Si("el 15-03, el cumpleanos de Juan: «" + (x.Propias.Length > 0 ? x.Propias[0].Mensaje : "") + "»", x.Propias.Length == 1 && x.Propias[0].Mensaje == "¡Feliz cumpleaños, Juan!");
            x = RocolaOsCalendario.De(new DateTime(2026, 8, 2, 21, 0, 0), fa);
            Si("el 02-08, el aniversario: «" + (x.Propias.Length > 0 ? x.Propias[0].Mensaje : "") + "»", x.Propias.Length == 1 && x.Propias[0].Mensaje == "¡Feliz aniversario, El Billar!");
            x = RocolaOsCalendario.De(new DateTime(2026, 10, 31, 21, 0, 0), fa);
            Si("el 31-10 sin Muertos (quitar): " + x.Nombres, x.Fiestas.Length == 0);
            x = RocolaOsCalendario.De(new DateTime(2026, 11, 1, 21, 0, 0), fa);
            Si("el 01-11 sigue Muertos: " + x.Nombres, x.Fiestas.Length == 1 && x.Fiestas[0].Clave == "muertos");
            x = RocolaOsCalendario.De(new DateTime(2026, 10, 12, 21, 0, 0), fa);
            Si("el 12-10, la fiesta propia con sus colores: " + x.Nombres + " " + (x.Fiestas.Length > 0 ? x.Fiestas[0].Colores.Length + " colores, «" + x.Mensajes[0] + "»" : ""),
                x.Fiestas.Length == 1 && x.Fiestas[0].Nombre == "Mi fiesta" && x.Fiestas[0].Colores.Length == 2 && x.Fiestas[0].Colores[0] == 0xFFFF0000u && x.Mensajes.Length == 1);
            x = RocolaOsCalendario.De(new DateTime(2026, 9, 16, 21, 0, 0), fa);
            Si("el 16-09, el mensaje del archivo en lugar del de fabrica: «" + (x.Mensajes.Length > 0 ? x.Mensajes[0] : "") + "»", x.Mensajes.Length == 1 && x.Mensajes[0] == "¡Viva México, cabrones!");
            x = RocolaOsCalendario.De(new DateTime(2026, 9, 15, 20, 0, 0), fa);
            Si("el 15-09, los fuegos a las 22:30 (el archivo manda)", x.ProximosFuegos.HasValue && x.ProximosFuegos.Value == new DateTime(2026, 9, 15, 22, 30, 0));
            x = RocolaOsCalendario.De(new DateTime(2026, 12, 31, 23, 59, 0), fa);
            Si("el 31-12 sin fuegos de Ano Nuevo (fuegos;no el 01-01)", !x.ProximosFuegos.HasValue);
            x = RocolaOsCalendario.De(new DateTime(2026, 11, 20, 21, 0, 0), fa);
            Si("el 20-11 sin la Revolucion (quitar sin texto: todas las de ese dia): " + x.Nombres, x.Fiestas.Length == 0);
            // ---- el clima ----
            string om = "{\"latitude\":19.4,\"longitude\":-99.1,\"current\":{\"time\":\"2026-11-20T13:00\",\"weather_code\":61,\"cloud_cover\":88,\"precipitation\":1.4,"
                + "\"rain\":1.2,\"showers\":0.2,\"snowfall\":0,\"wind_speed_10m\":14.8,\"wind_direction_10m\":250,\"temperature_2m\":17.6,\"visibility\":12000,\"is_day\":1}}";
            DateTime ahora = new DateTime(2026, 11, 20, 19, 0, 0, DateTimeKind.Utc);
            RocolaOsClima cl;
            using (JsonDocument d = JsonDocument.Parse(om)) cl = RocolaOsClima.DeOpenMeteo(d.RootElement, ahora);
            Si("Open-Meteo: lluvia (61), nubes 88 %, 1,4 mm/h, viento 14,8 de 250, 17,6 C, de dia", cl != null && cl.Tipo == RocolaOsClimaTipo.Lluvia && Math.Abs(cl.Nubes - 0.88f) < 1e-4
                && Math.Abs(cl.Lluvia - 1.4f) < 1e-4 && Math.Abs(cl.Viento - 14.8f) < 1e-4 && cl.Direccion == 250f && Math.Abs(cl.Temperatura - 17.6f) < 1e-4 && cl.DeDia);
            RocolaOsClima c2 = RocolaOsClima.DeTexto(cl.Texto(19.43, -99.13));
            Si("rocola_clima.txt de ida y vuelta (mismo tipo, codigo y hora)", c2 != null && c2.Tipo == cl.Tipo && c2.Codigo == cl.Codigo && c2.Medida == cl.Medida && Math.Abs(c2.Nubes - cl.Nubes) < 0.006f);
            Si("el clima vale a las 2 h 59 min", cl.Vigente(ahora.AddMinutes(179)));
            Si("y a las 3 h 01 min ya no (despejado)", !cl.Vigente(ahora.AddMinutes(181)));
            using (JsonDocument d = JsonDocument.Parse("{\"current\":{\"cloud_cover\":10}}")) Si("una respuesta sin weather_code no vale", RocolaOsClima.DeOpenMeteo(d.RootElement, ahora) == null);
            Si("un rocola_clima.txt roto no vale", RocolaOsClima.DeTexto("medida=ayer\ncodigo=x\n") == null);
            int[] cod = { 0, 1, 2, 3, 45, 48, 51, 55, 61, 63, 65, 66, 71, 80, 82, 95, 99 };
            RocolaOsClimaTipo[] esp = { RocolaOsClimaTipo.Despejado, RocolaOsClimaTipo.PocoNublado, RocolaOsClimaTipo.PocoNublado, RocolaOsClimaTipo.Nublado, RocolaOsClimaTipo.Neblina,
                RocolaOsClimaTipo.Neblina, RocolaOsClimaTipo.Llovizna, RocolaOsClimaTipo.Llovizna, RocolaOsClimaTipo.Lluvia, RocolaOsClimaTipo.Lluvia, RocolaOsClimaTipo.Aguacero,
                RocolaOsClimaTipo.Lluvia, RocolaOsClimaTipo.Nieve, RocolaOsClimaTipo.Lluvia, RocolaOsClimaTipo.Aguacero, RocolaOsClimaTipo.Tormenta, RocolaOsClimaTipo.Tormenta };
            bool todos = true; for (int i = 0; i < cod.Length; i++) if (RocolaOsClima.DeCodigo(cod[i], 0.1f) != esp[i]) todos = false;
            Si("los codigos de la OMM (DeCodigo, 17 codigos)", todos && RocolaOsClima.DeCodigo(0, 0.8f) == RocolaOsClimaTipo.Nublado && RocolaOsClima.DeCodigo(0, 0.4f) == RocolaOsClimaTipo.PocoNublado);
            RocolaOsClima fz = RocolaOsClima.Forzar("tormenta,90,12,30,180,15");
            Si("ROCOLA_CLIMA «tormenta,90,12,30,180,15»", fz != null && fz.Tipo == RocolaOsClimaTipo.Tormenta && Math.Abs(fz.Nubes - 0.9f) < 1e-4 && fz.Lluvia == 12f && fz.Viento == 30f
                && fz.Direccion == 180f && fz.Temperatura == 15f && fz.Vigente(ahora.AddDays(5)));
            Si("ROCOLA_CLIMA «llovizna» (con lo de fabrica) y «granizo» no vale", RocolaOsClima.Forzar("llovizna") != null && RocolaOsClima.Forzar("llovizna").Lluvia > 0 && RocolaOsClima.Forzar("granizo") == null);
            // ---- el lugar por internet ----
            using (JsonDocument d = JsonDocument.Parse("{\"ip\":\"x\",\"city\":\"Monterrey\",\"latitude\":25.6866,\"longitude\":-100.3161}"))
            {
                RocolaOsLugar g = RocolaOsLugar.DeJson(d.RootElement, ahora);
                Si("el lugar de ipapi.co (numeros)", g != null && g.Ciudad == "Monterrey" && Math.Abs(g.Lat - 25.6866) < 1e-6);
                RocolaOsLugar g2 = RocolaOsLugar.DeTexto(g.Texto());
                Si("rocola_clima_lugar.txt de ida y vuelta", g2 != null && g2.Ciudad == "Monterrey" && Math.Abs(g2.Lon + 100.3161) < 1e-4 && g2.Cuando == g.Cuando);
                Si("vale una semana: a los 6 dias no es viejo, a los 7 si", !g.Viejo(ahora.AddDays(6)) && g.Viejo(ahora.AddDays(7)));
            }
            using (JsonDocument d = JsonDocument.Parse("{\"city\":\"Puebla\",\"latitude\":\"19.04\",\"longitude\":\"-98.20\"}"))
            { RocolaOsLugar g = RocolaOsLugar.DeJson(d.RootElement, ahora); Si("el lugar de get.geojs.io (numeros en texto)", g != null && g.Ciudad == "Puebla" && Math.Abs(g.Lat - 19.04) < 1e-6); }
            using (JsonDocument d = JsonDocument.Parse("{\"success\":false}")) Si("una respuesta sin lugar no vale", RocolaOsLugar.DeJson(d.RootElement, ahora) == null);
            // ---- la calma (regla 5): a 60 cuadros por segundo, baja a 0,35 en 0,4 s y vuelve a 1 en 3 s, sin saltos ----
            {
                double paso = 1; float c = 1, antes = 1, mx = 0; int n = 0;
                while (c > 0.3501f && n < 1000) { c = PasoCalma(ref paso, true, 1 / 60.0); mx = Math.Max(mx, Math.Abs(c - antes)); antes = c; n++; }
                double baja = n / 60.0;
                int m = 0;
                while (c < 0.9999f && m < 1000) { c = PasoCalma(ref paso, false, 1 / 60.0); mx = Math.Max(mx, Math.Abs(c - antes)); antes = c; m++; }
                double sube = m / 60.0;
                Si("la calma baja a 0,35 en " + baja.ToString("0.00", ci) + " s y vuelve a 1 en " + sube.ToString("0.00", ci) + " s; lo mas que cambia de un cuadro a otro: "
                    + mx.ToString("0.000", ci), Math.Abs(baja - 0.4) < 0.05 && Math.Abs(sube - 3.0) < 0.05 && mx < 0.05f);
                double p2 = 1; float a1 = PasoCalma(ref p2, true, 0.2), a2 = PasoCalma(ref p2, false, 0.2);
                Si("si deja de elegir a medias, vuelve desde ahi (" + a1.ToString("0.000", ci) + " y luego " + a2.ToString("0.000", ci) + ")", a2 > a1 && a1 > 0.35f && a1 < 1f);
            }
            // ---- la ciudad elegida: las 41 de la lista ----
            Si("41 ciudades y la automatica (con las coordenadas de la Ciudad de Mexico)", RocolaOsCiudades.Nombres.Length == 42 && RocolaOsCiudades.Lat.Length == 42 && RocolaOsCiudades.Lon.Length == 42
                && RocolaOsCiudades.Lat[0] == 19.43 && RocolaOsCiudades.Nombres[8] == "Ciudad de México" && RocolaOsCiudades.Nombres[23] == "Monterrey");
            Console.WriteLine((malas == 0 ? "TODO BIEN" : malas + " MAL") + " (" + (buenas + malas) + " comprobaciones de la orden)");
            return malas == 0 ? 0 : 1;
        }
        private static double Dist(double a, double b) { double d = Math.Abs(a - b) % 1.0; return Math.Min(d, 1 - d); }
        private static void Fase(string que, DateTime utc, double esperada)
        {
            double alt, az, f; RocolaOsAstro.Luna(utc, 19.43, -99.13, out alt, out az, out f);
            double xp = Form1.FaseLunar(utc);
            Si(que + ": fase " + F(f) + " (XP: " + F(xp) + "), esperada " + F(esperada) + " +-0,03", Dist(f, esperada) < 0.03);
        }
        private static void Manda(double alt, bool sube, string fase)
        {
            float[] p = new float[RocolaOsCieloDia.Fases]; RocolaOsCieloDia.Pesos(alt, sube, p);
            int mx = 0; for (int i = 1; i < p.Length; i++) if (p[i] > p[mx]) mx = i;
            Si("con el sol a " + alt.ToString("0.0", CultureInfo.InvariantCulture) + " grados " + (sube ? "(manana)" : "(tarde)") + " manda " + fase + " (" + RocolaOsCieloDia.Nombres[mx] + ", "
                + p[mx].ToString("0.00", CultureInfo.InvariantCulture) + ")", RocolaOsCieloDia.Nombres[mx] == fase);
        }
        private static void Fuegos(string cuando, string esperado, int cuenta)
        {
            DateTime t = DateTime.ParseExact(cuando, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            int c; DateTime? f = RocolaOsCalendario.ProximosFuegos(t, RocolaOsFechasArchivo.Vacio, out c);
            string s = f.HasValue ? f.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "-";
            Si("fuegos a las " + cuando + ": " + s + (f.HasValue ? " (cuenta " + c + " s)" : ""), s == esperado && (!f.HasValue || c == cuenta));
        }
        // lo mas claro, hasta 0,848 del alto, de la mezcla de las paletas de dia con estos pesos (sin la noche: la de XP no cuenta aqui)
        private static double LuzMezcla(float[] p, float H, float hasta)
        {
            double dia = 0; for (int i = 1; i < p.Length; i++) dia += p[i];
            if (dia <= 0) return 0;
            double mx = 0;
            for (int k = 0; k <= 400; k++)
            {
                float yf = hasta * k / 400f; double r = 0, g = 0, b = 0;
                for (int i = 1; i < p.Length; i++)
                {
                    if (p[i] <= 0) continue;
                    float pr, pg, pb; RocolaOsCieloDia.ColorPaleta(i, yf, H, out pr, out pg, out pb);
                    r += p[i] * pr; g += p[i] * pg; b += p[i] * pb;
                }
                double l = RocolaOsCieloDia.Luminancia((float)(r / dia), (float)(g / dia), (float)(b / dia));
                if (l > mx) mx = l;
            }
            return mx;
        }
    }
}
