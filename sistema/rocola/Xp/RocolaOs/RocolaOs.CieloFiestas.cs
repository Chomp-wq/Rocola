// EL CIELO VIVO DE ROCOLA OS, NUCLEO: EL CALENDARIO DE LAS FIESTAS (PLAN_CIELO_VIVO.md §6) Y LAS FECHAS PROPIAS (rocola_fechas.txt, §2).
// Solo datos y cuentas, sin dibujo ni disco: lo que se pinta en cada fecha lo hace el grupo de las fiestas (FI1-FI6) leyendo esto en la
// foto del cielo (RocolaOsCieloFoto.Hoy: Fiestas, Propias, Mensajes, ProximosFuegos, CuentaFuegos). Sin internet: las fechas las sabe la rocola.
//
// rocola_fechas.txt (en la carpeta de datos; lo lee el hilo del cielo cuando cambia, RocolaOs.CieloClima.cs; lo edita la hoja «Imagen» de
// Ctrl+F4 o Ctrl+F4 -> Archivos, nunca la orden «ajuste»): un renglon por fecha, «DD-MM;tipo;texto», UTF-8, finales LF o CRLF; lo que empieza
// por «#» es comentario y un renglon que no vale se salta (la bitacora lo dice una vez). Los tipos:
//   cumple        «15-03;cumple;Juan»               FI6: ese dia, «¡Feliz cumpleaños, Juan!» y globos en el escaparate
//   aniversario   «02-08;aniversario;El Billar»     FI6: «¡Feliz aniversario, El Billar!»
//   fiesta        «12-10;fiesta;Mi fiesta»          una fiesta mas del calendario (colores de fabrica: oro y azul); con «|#rrggbb,#rrggbb» al
//                                                   final del texto, sus colores («12-10;fiesta;Mi fiesta|#ff0000,#ffffff»)
//   quitar        «31-10;quitar;muertos»            quita esa fiesta del calendario en esa fecha (su clave: abajo); sin texto, todas las de ese dia
//   mensaje       «16-09;mensaje;¡Viva México!»     el mensaje del escaparate de ese dia (FI5) en lugar del de fabrica (varios: se turnan)
//   fuegos        «15-09;fuegos;23:00» o «;no»       la hora de los fuegos de ese dia (FI4), o sin fuegos
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Rocola
{
    // UNA FIESTA DEL CALENDARIO (inmutable)
    internal sealed class RocolaOsFiesta
    {
        internal readonly string Clave;     // «anio_nuevo», «reyes»... (la de «quitar»); las de rocola_fechas.txt, «propia»
        internal readonly string Nombre;    // «Año Nuevo»
        internal readonly uint[] Colores;   // ARGB (opacos), los de la fecha (FI1 la aurora, FI3 la estela, FI2 y FI5 los adornos)
        internal readonly string Adorno;    // lo que ademas lleva (§6): «fuegos», «estrella_guia», «farolitos», «corazones», «bandera», «flores_moradas»,
                                            // «globos», «flores», «papel_picado», «veladoras_flores», «notas», «rosas», «farolitos_luces», «luces», «» ninguno
        internal readonly string Donde;     // donde va el adorno: «horizonte» (FI2), «escaparate» (FI5), «aurora» (las notas de Santa Cecilia) o «»
        internal readonly string Mensaje;   // el mensaje de fabrica para el escaparate (FI5; el grupo de las fiestas lo puede afinar)
        internal RocolaOsFiesta(string clave, string nombre, uint[] colores, string adorno, string donde, string mensaje)
        { Clave = clave; Nombre = nombre; Colores = colores; Adorno = adorno; Donde = donde; Mensaje = mensaje; }
    }

    // UNA FECHA PROPIA DE HOY (FI6)
    internal sealed class RocolaOsFechaPropia
    {
        internal readonly string Tipo;      // «cumple» o «aniversario»
        internal readonly string Texto;     // el nombre que escribio el dueno
        internal RocolaOsFechaPropia(string tipo, string texto) { Tipo = tipo; Texto = texto; }
        internal string Mensaje { get { return Tipo == "cumple" ? "¡Feliz cumpleaños, " + Texto + "!" : "¡Feliz aniversario, " + Texto + "!"; } }
    }

    // LO QUE DICE rocola_fechas.txt, YA LEIDO (inmutable: el hilo del cielo pone uno nuevo entero cuando cambia el archivo)
    internal sealed class RocolaOsFechasArchivo
    {
        internal sealed class Renglon { internal int Dia, Mes; internal string Tipo, Texto; }
        internal readonly Renglon[] Renglones;
        internal readonly string[] Malos;   // los renglones que no valieron (para la bitacora)
        internal static readonly RocolaOsFechasArchivo Vacio = new RocolaOsFechasArchivo(new Renglon[0], new string[0]);
        private RocolaOsFechasArchivo(Renglon[] r, string[] m) { Renglones = r; Malos = m; }
        internal static readonly string[] Tipos = { "cumple", "aniversario", "fiesta", "quitar", "mensaje", "fuegos" };

        // LEE EL TEXTO DEL ARCHIVO (sin tocar el disco: el texto ya leido). Nunca lanza.
        internal static RocolaOsFechasArchivo Lee(string texto)
        {
            List<Renglon> r = new List<Renglon>(); List<string> malos = new List<string>();
            if (string.IsNullOrEmpty(texto)) return Vacio;
            string[] lineas = texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lineas.Length; i++)
            {
                string l = lineas[i].Trim();
                if (i == 0 && l.Length > 0 && l[0] == '﻿') l = l.Substring(1).Trim();   // (la marca de UTF-8, si un editor la puso)
                if (l.Length == 0 || l[0] == '#') continue;
                Renglon x = Renglon1(l);
                if (x == null) { if (malos.Count < 20) malos.Add((i + 1).ToString(CultureInfo.InvariantCulture) + ": " + (l.Length > 60 ? l.Substring(0, 60) + "..." : l)); continue; }
                r.Add(x);
            }
            return new RocolaOsFechasArchivo(r.ToArray(), malos.ToArray());
        }
        private static Renglon Renglon1(string l)
        {
            int p1 = l.IndexOf(';'); if (p1 < 0) return null;
            int p2 = l.IndexOf(';', p1 + 1);
            string fecha = l.Substring(0, p1).Trim(), tipo = (p2 < 0 ? l.Substring(p1 + 1) : l.Substring(p1 + 1, p2 - p1 - 1)).Trim().ToLowerInvariant();
            string texto = p2 < 0 ? "" : l.Substring(p2 + 1).Trim();
            int dia, mes;
            if (!Fecha(fecha, out dia, out mes)) return null;
            if (Array.IndexOf(Tipos, tipo) < 0) return null;
            if ((tipo == "cumple" || tipo == "aniversario" || tipo == "fiesta" || tipo == "mensaje") && texto.Length == 0) return null;
            if (tipo == "fuegos" && texto != "no") { TimeSpan h; if (!Hora(texto, out h)) return null; }
            if (texto.Length > 120) texto = texto.Substring(0, 120);
            return new Renglon { Dia = dia, Mes = mes, Tipo = tipo, Texto = texto };
        }
        // «DD-MM» (tambien «D-M» y con «/»)
        internal static bool Fecha(string s, out int dia, out int mes)
        {
            dia = mes = 0;
            string[] p = s.Split('-', '/');
            if (p.Length != 2) return false;
            if (!int.TryParse(p[0], NumberStyles.None, CultureInfo.InvariantCulture, out dia) || !int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out mes)) return false;
            if (mes < 1 || mes > 12 || dia < 1) return false;
            return dia <= DateTime.DaysInMonth(2024, mes);   // (bisiesto: el 29-02 vale)
        }
        // «HH:MM»
        internal static bool Hora(string s, out TimeSpan h)
        {
            h = TimeSpan.Zero;
            string[] p = s.Split(':');
            int hh, mm;
            if (p.Length != 2 || !int.TryParse(p[0], NumberStyles.None, CultureInfo.InvariantCulture, out hh) || !int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out mm)) return false;
            if (hh > 23 || mm > 59) return false;
            h = new TimeSpan(hh, mm, 0); return true;
        }
    }

    // LO DE HOY: las fiestas, las fechas propias, los mensajes del escaparate y los proximos fuegos (lo calcula el latido para la foto)
    internal sealed class RocolaOsCalendarioHoy
    {
        internal readonly RocolaOsFiesta[] Fiestas;          // las de hoy (casi siempre una; dos si rocola_fechas.txt anade otra), sin las quitadas
        internal readonly RocolaOsFechaPropia[] Propias;     // cumpleanos y aniversarios de hoy
        internal readonly string[] Mensajes;                 // los del escaparate de hoy (los de rocola_fechas.txt mandan sobre los de fabrica)
        internal readonly DateTime? ProximosFuegos;          // cuando empiezan los proximos fuegos (hora local), si es dentro de las proximas 24 h
        internal readonly int CuentaFuegos;                  // los segundos de la cuenta antes de los fuegos (10 en Ano Nuevo, 0 en el Grito)
        internal static readonly RocolaOsCalendarioHoy Nada = new RocolaOsCalendarioHoy(new RocolaOsFiesta[0], new RocolaOsFechaPropia[0], new string[0], null, 0);
        internal RocolaOsCalendarioHoy(RocolaOsFiesta[] f, RocolaOsFechaPropia[] p, string[] m, DateTime? fuegos, int cuenta)
        { Fiestas = f; Propias = p; Mensajes = m; ProximosFuegos = fuegos; CuentaFuegos = cuenta; }
        internal string Nombres
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (RocolaOsFiesta x in Fiestas) sb.Append(sb.Length > 0 ? ", " : "").Append(x.Clave);
                foreach (RocolaOsFechaPropia x in Propias) sb.Append(sb.Length > 0 ? ", " : "").Append(x.Tipo).Append(' ').Append(x.Texto);
                return sb.Length > 0 ? sb.ToString() : "ninguna";
            }
        }
    }

    internal static class RocolaOsCalendario
    {
        // los colores (§6)
        private const uint Oro = 0xFFFFC83Cu, Plata = 0xFFD8DEE8u, Azul = 0xFF3C78FFu, Rojo = 0xFFE03C3Cu, Blanco = 0xFFF4F4F4u, Rosa = 0xFFFF7AB8u,
            VerdeBandera = 0xFF00A05Au, RojoBandera = 0xFFD8202Eu, Morado = 0xFF8E44C8u, Cempasuchil = 0xFFFF9A1Au, Naranja = 0xFFFF8A2Au, Verde = 0xFF22A05Au;
        private static readonly uint[] Tricolor = { VerdeBandera, Blanco, RojoBandera };

        private static RocolaOsFiesta F(string clave, string nombre, uint[] c, string adorno, string donde, string msg) { return new RocolaOsFiesta(clave, nombre, c, adorno, donde, msg); }
        internal static readonly RocolaOsFiesta AnioNuevo = F("anio_nuevo", "Año Nuevo", new[] { Oro, Plata }, "fuegos", "", "¡Feliz Año Nuevo! Empieza el año con tu canción");
        internal static readonly RocolaOsFiesta Reyes = F("reyes", "Día de Reyes", new[] { Oro, Azul }, "estrella_guia", "horizonte", "Día de Reyes: ¡pon la canción que te traiga suerte!");
        internal static readonly RocolaOsFiesta Candelaria = F("candelaria", "Candelaria", new[] { Rojo, Blanco }, "farolitos", "horizonte", "Día de la Candelaria: ¿a quién le tocan los tamales?");
        internal static readonly RocolaOsFiesta Amor = F("amor", "Amor y amistad", new[] { Rosa, Rojo }, "corazones", "escaparate", "Amor y amistad: dedica una canción");
        internal static readonly RocolaOsFiesta Bandera = F("bandera", "Día de la Bandera", Tricolor, "bandera", "escaparate", "Día de la Bandera: ¡Viva México!");
        internal static readonly RocolaOsFiesta Mujer = F("mujer", "Día de la Mujer", new[] { Morado, 0xFFC890F0u }, "flores_moradas", "horizonte", "Día de la Mujer: una canción para ellas");
        internal static readonly RocolaOsFiesta Nino = F("nino", "Día del Niño", new[] { 0xFFFF4F6Eu, 0xFFFFC93Cu, 0xFF3CC8FFu, 0xFF7CD957u }, "globos", "escaparate", "Día del Niño: ¡que suene la alegría!");
        internal static readonly RocolaOsFiesta Madres = F("madres", "Día de las Madres", new[] { Rosa, Blanco }, "flores", "horizonte", "10 de mayo: una canción para mamá");
        internal static readonly RocolaOsFiesta Padre = F("padre", "Día del Padre", new[] { Azul, Oro }, "globos", "escaparate", "Día del Padre: una canción para papá");
        internal static readonly RocolaOsFiesta Independencia = F("independencia", "Independencia", Tricolor, "papel_picado", "escaparate", "¡Viva México! Pon tu ranchera favorita");
        internal static readonly RocolaOsFiesta Muertos = F("muertos", "Día de Muertos", new[] { Cempasuchil, Morado }, "veladoras_flores", "horizonte", "Día de Muertos: la canción que le gustaba a quien recuerdas");
        internal static readonly RocolaOsFiesta Revolucion = F("revolucion", "Revolución", Tricolor, "papel_picado", "escaparate", "Día de la Revolución: ¡pon un corrido!");
        internal static readonly RocolaOsFiesta SantaCecilia = F("santa_cecilia", "Santa Cecilia", new[] { Oro, Rojo }, "notas", "aurora", "Santa Cecilia, patrona de los músicos: ¡que suene la música!");
        internal static readonly RocolaOsFiesta Guadalupana = F("guadalupana", "Guadalupana", new[] { Azul, Rosa }, "rosas", "horizonte", "12 de diciembre: las mañanitas a la Virgen");
        internal static readonly RocolaOsFiesta Posadas = F("posadas", "Posadas", new[] { Naranja, Oro }, "farolitos_luces", "horizonte", "Posadas: ¡pon la canción de la fiesta!");
        internal static readonly RocolaOsFiesta Navidad = F("navidad", "Navidad", new[] { Rojo, Verde, Oro }, "luces", "horizonte", "Feliz Navidad: pon tu canción favorita");
        internal static readonly RocolaOsFiesta[] Todas = { AnioNuevo, Reyes, Candelaria, Amor, Bandera, Mujer, Nino, Madres, Padre, Independencia, Muertos,
            Revolucion, SantaCecilia, Guadalupana, Posadas, Navidad };
        internal static readonly TimeSpan GritoHora = new TimeSpan(23, 0, 0);   // los fuegos del Grito: el 15-9 a las 23:00
        internal const int CuentaAnioNuevo = 10;                                // y los de Ano Nuevo: el 1-1 a las 00:00, tras 10 s de cuenta
        internal const int FuegosMinutos = 4;                                   // los fuegos duran 4 minutos (§6)

        // EL TERCER DOMINGO DE JUNIO del ano (el Dia del Padre)
        internal static DateTime TercerDomingoDeJunio(int anio)
        {
            DateTime d = new DateTime(anio, 6, 1);
            while (d.DayOfWeek != DayOfWeek.Sunday) d = d.AddDays(1);
            return d.AddDays(14);
        }

        // LAS FIESTAS DE FABRICA DE UNA FECHA (sin rocola_fechas.txt), en el orden de la tabla
        internal static List<RocolaOsFiesta> DeFabrica(DateTime dia)
        {
            List<RocolaOsFiesta> l = new List<RocolaOsFiesta>(2);
            int m = dia.Month, d = dia.Day;
            if (m == 1 && d == 1) l.Add(AnioNuevo);   // (27-11-2026: el 31-12 de dia ya es Navidad; la cuenta y los fuegos desde las 23:59:50 no cambian)
            if (m == 1 && d == 6) l.Add(Reyes);
            if (m == 2 && d == 2) l.Add(Candelaria);
            if (m == 2 && d == 14) l.Add(Amor);
            if (m == 2 && d == 24) l.Add(Bandera);
            if (m == 3 && d == 8) l.Add(Mujer);
            if (m == 4 && d == 30) l.Add(Nino);
            if (m == 5 && d == 10) l.Add(Madres);
            if (m == 6 && dia.Date == TercerDomingoDeJunio(dia.Year)) l.Add(Padre);
            if (m == 9 && (d == 15 || d == 16)) l.Add(Independencia);
            if ((m == 10 && d == 31) || (m == 11 && d <= 2)) l.Add(Muertos);
            if (m == 11 && d == 20) l.Add(Revolucion);
            if (m == 11 && d == 22) l.Add(SantaCecilia);
            // (27-11-2026, decision del dueno: diciembre es de Navidad, salvo la Guadalupana, SOLO el 12, y las posadas, SOLO del 16 al 23: sus nueve noches
            // acaban en Nochebuena, y el 24 ya es Navidad sin alternar con las posadas)
            if (m == 12 && d == 12) l.Add(Guadalupana);
            if (m == 12 && d >= 16 && d <= 23) l.Add(Posadas);
            if (m == 12 && d != 12 && !(d >= 16 && d <= 23)) l.Add(Navidad);
            return l;
        }

        // LO DE HOY (la hora local del cielo, con rocola_fechas.txt ya leido)
        internal static RocolaOsCalendarioHoy De(DateTime local, RocolaOsFechasArchivo archivo)
        {
            if (archivo == null) archivo = RocolaOsFechasArchivo.Vacio;
            DateTime dia = local.Date;
            List<RocolaOsFiesta> fiestas = DeFabrica(dia);
            List<RocolaOsFechaPropia> propias = new List<RocolaOsFechaPropia>();
            List<string> mensajes = new List<string>();
            foreach (RocolaOsFechasArchivo.Renglon r in archivo.Renglones)
            {
                if (r.Dia != dia.Day || r.Mes != dia.Month) continue;
                if (r.Tipo == "quitar")
                {
                    string q = r.Texto.Trim().ToLowerInvariant();
                    fiestas.RemoveAll(x => q.Length == 0 || x.Clave == q || Sin(x.Nombre) == Sin(q));
                }
            }
            foreach (RocolaOsFechasArchivo.Renglon r in archivo.Renglones)
            {
                if (r.Dia != dia.Day || r.Mes != dia.Month) continue;
                if (r.Tipo == "cumple" || r.Tipo == "aniversario") propias.Add(new RocolaOsFechaPropia(r.Tipo, r.Texto));
                else if (r.Tipo == "fiesta") fiestas.Add(Propia(r.Texto));
                else if (r.Tipo == "mensaje") mensajes.Add(r.Texto);
            }
            if (mensajes.Count == 0) foreach (RocolaOsFiesta f in fiestas) if (!string.IsNullOrEmpty(f.Mensaje)) mensajes.Add(f.Mensaje);
            int cuenta;
            DateTime? fuegos = ProximosFuegos(local, archivo, out cuenta);
            return new RocolaOsCalendarioHoy(fiestas.ToArray(), propias.ToArray(), mensajes.ToArray(), fuegos, cuenta);
        }
        private static RocolaOsFiesta Propia(string texto)
        {
            string nombre = texto; uint[] colores = { Oro, Azul };
            int b = texto.IndexOf('|');
            if (b >= 0)
            {
                nombre = texto.Substring(0, b).Trim();
                List<uint> c = new List<uint>();
                foreach (string s in texto.Substring(b + 1).Split(','))
                {
                    string t = s.Trim().TrimStart('#'); uint v;
                    if (t.Length == 6 && uint.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) c.Add(0xFF000000u | v);
                }
                if (c.Count > 0) colores = c.ToArray();
            }
            return new RocolaOsFiesta("propia", nombre, colores, "", "", "¡Hoy es " + nombre + "!");
        }
        // sin acentos y en minusculas (para «quitar»; sin ICU: a mano, lo de las fiestas)
        private static string Sin(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char ch in s.ToLowerInvariant())
            {
                int i = "áéíóúüñ".IndexOf(ch);
                sb.Append(i >= 0 ? "aeiouun"[i] : ch == ' ' ? '_' : ch);
            }
            return sb.ToString();
        }

        // LOS PROXIMOS FUEGOS (FI4): su comienzo (hora local) si es en las proximas 24 h o empezaron hace menos de 4 min; si no, null. De fabrica:
        // el Grito (15-9, 23:00) y Ano Nuevo (1-1, 00:00, con 10 s de cuenta antes). rocola_fechas.txt manda en su fecha («fuegos;HH:MM» o «;no»).
        internal static DateTime? ProximosFuegos(DateTime local, RocolaOsFechasArchivo archivo, out int cuenta)
        {
            cuenta = 0;
            for (int k = -1; k <= 1; k++)
            {
                DateTime dia = local.Date.AddDays(k);
                TimeSpan? h = null; int c = 0;
                if (dia.Month == 9 && dia.Day == 15) h = GritoHora;
                if (dia.Month == 1 && dia.Day == 1) { h = TimeSpan.Zero; c = CuentaAnioNuevo; }
                if (archivo != null)
                    foreach (RocolaOsFechasArchivo.Renglon r in archivo.Renglones)
                        if (r.Tipo == "fuegos" && r.Dia == dia.Day && r.Mes == dia.Month)
                        {
                            TimeSpan x;
                            if (r.Texto == "no") h = null; else if (RocolaOsFechasArchivo.Hora(r.Texto, out x)) { h = x; c = (x == TimeSpan.Zero && dia.Month == 1 && dia.Day == 1) ? CuentaAnioNuevo : 0; }
                        }
                if (!h.HasValue) continue;
                DateTime inicio = dia + h.Value;
                if (local >= inicio.AddMinutes(FuegosMinutos)) continue;
                if (inicio - local > TimeSpan.FromHours(24)) continue;
                cuenta = c;
                return inicio;
            }
            return null;
        }
    }
}
