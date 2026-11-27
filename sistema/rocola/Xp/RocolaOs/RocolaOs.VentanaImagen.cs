// ROCOLA OS (fase 11): LA HOJITA «IMAGEN» DE LA VENTANA DE CTRL+F4 (PLAN_CIELO_VIVO.md, «La hoja Imagen»; FASE11_hoja-imagen.md): donde el dueno
// enciende, apaga y mira el cielo vivo, con el mismo aspecto de las hojas Pantalla, Sonido y Spotify (las tarjetas de Ctrl+F3). Cinco hojitas
// pegadas arriba:
//   DIA Y NOCHE  la funcion «cielo» y sus seis ideas (DN1-DN6), la ciudad del bar (ciudad_bar: automatica por internet o una de las 41) y la letra
//                en el cielo (letra_cielo, K6).
//   FIESTAS      la funcion «fiestas» y sus seis ideas (FI1-FI6) y TUS FECHAS: cumpleanos y aniversarios de rocola_fechas.txt (ver, agregar, quitar).
//   CLIMA        la funcion «clima» y sus cuatro ideas (CL2-CL5) y el clima de afuera de ahora.
//   VIDRIO       la funcion «vidrio» y sus seis ideas (VI1-VI6), con lo que cada una cuesta.
//   VER COMO     «Ver como se veria»: una hora, una fecha (o fiesta) y un clima, 30 s, sin guardar nada (RocolaOsCielo.VerComo): cierra la ventana
//                para que se vea la rocola de verdad; vuelve sola a los 30 s o al tocar un boton.
// Cada interruptor cambia su clave de rocola_os.txt al momento (RocolaOs.PonAjuste: se guarda para siempre). Las cuatro funciones (cielo, fiestas,
// clima, vidrio) se deciden una vez por arranque (RocolaOs.Encendida): su interruptor se guarda al momento pero vale desde el SIGUIENTE ARRANQUE, y
// la hoja lo dice; las ideas de dentro cambian en vivo. Si la funcion de una idea esta apagada en rocola_os.txt, la idea va en gris diciendo cual
// necesita (su valor guardado se conserva). Las cuatro funciones van encendidas de fabrica desde la junta final (27-11-2026).
// Para anadir un interruptor (por ejemplo el de la letra en el cielo si cambia de nombre) basta una linea en las tablas de abajo.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using Rocola.Nativo;

namespace Rocola
{
    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraHojitaImagen()
        {
            // (Orden 12: despues de Pantalla (10) y antes de Sonido (15), como pide el plan; las demas no se mueven de sitio salvo hacia la derecha)
            RocolaOsHojitas.Registra(new RocolaOsHojita { Nombre = "Imagen", Orden = 12, Arma = (f, x) => f.RocolaOsPaginaImagen(x) });
        }

        // UN INTERRUPTOR DE LA HOJA: su clave de rocola_os.txt, su nombre, lo que hace y la funcion de la que depende (null: ella misma es la funcion)
        internal sealed class ImagenFila
        {
            internal string Clave, Nombre, Que, Funcion;
            internal ImagenFila(string clave, string nombre, string que, string funcion) { Clave = clave; Nombre = nombre; Que = que; Funcion = funcion; }
        }
        // LAS TABLAS (el orden en que se ven). La primera de cada grupo es su funcion; las demas, sus ideas.
        internal static readonly ImagenFila[] ImagenDia = {
            new ImagenFila("cielo", "Cielo vivo (todo este grupo)", "Enciende o apaga las de abajo. Vale al siguiente arranque.", null),
            new ImagenFila("cielo_sol", "Colores del cielo según el sol", "Alba, mañana, tarde dorada y atardecer; la noche es la de siempre.", "cielo"),
            new ImagenFila("sol", "El sol cruza el cielo", "Siempre detrás de todo; en la pared solo asoma al salir y al ponerse.", "cielo"),
            new ImagenFila("luna_camino", "La luna en su camino real", "Apagada: la luna se queda en su sitio de siempre, con su fase real.", "cielo"),
            new ImagenFila("estrellas_anochecer", "Estrellas que se encienden al anochecer", "Una a una, según la hora; de noche, las de siempre.", "cielo"),
            new ImagenFila("horizonte_hora", "La raya del horizonte con la luz de la hora", "El color de la raya sigue al sol (la de la noche es la de siempre).", "cielo"),
            new ImagenFila("nubes", "Nubes con la luz del sol o de la luna", "Nubes pintadas, de día y de noche.", "cielo"),
        };
        internal static readonly ImagenFila[] ImagenFiestas = {
            new ImagenFila("fiestas", "Fiestas (todo este grupo)", "Sin internet. Vale al siguiente arranque.", null),
            new ImagenFila("aurora_fiesta", "La aurora con los colores de la fecha", "La aurora de siempre, con los colores de la fiesta.", "fiestas"),
            new ImagenFila("horizonte_fiesta", "Adornos sobre la raya del horizonte", "Una fila baja de farolitos, flores, luces...", "fiestas"),
            new ImagenFila("fugaz_fiesta", "La fugaz dorada con la estela de la fecha", "Sin fugaces nuevas: la de siempre, con otro color.", "fiestas"),
            new ImagenFila("fuegos", "Fuegos artificiales", "Solo el Grito (15-9, 23 h) y Año Nuevo (00 h, con cuenta de 10 s).", "fiestas"),
            new ImagenFila("escaparate_fiesta", "Papel picado y mensaje de la fecha", "Solo en el escaparate (la atracción).", "fiestas"),
            new ImagenFila("fechas_propias", "Tus fechas", "Cumpleaños y aniversarios de la lista de la derecha.", "fiestas"),
        };
        internal static readonly ImagenFila[] ImagenClima = {
            new ImagenFila("clima", "Clima de afuera (todo este grupo)", "Pide el clima cada 15 min (Open-Meteo). Vale al reabrir.", null),
            new ImagenFila("lluvia", "Lluvia", "Detrás de la música, con el viento de afuera.", "clima"),
            new ImagenFila("tormenta", "Relámpagos", "Dentro de las nubes, sin destellos de pantalla ni sonido.", "clima"),
            new ImagenFila("neblina", "Neblina", "Una franja sobre el horizonte.", "clima"),
            new ImagenFila("clima_escaparate", "El clima en el escaparate", "La pastilla «hora · °C · tiempo» y un mensaje.", "clima"),
        };
        internal static readonly ImagenFila[] ImagenVidrio = {
            new ImagenFila("vidrio", "Vidrio (todo este grupo)", "Las piezas de siempre, de vidrio. Vale al siguiente arranque.", null),
            new ImagenFila("vidrio_pastillas", "Pastillas de vidrio", "BUSCAR, AYUDA, letras, En cola y créditos. Costo bajo.", "vidrio"),
            new ImagenFila("vidrio_luz", "El filo de luz sigue al cielo", "Costo bajo.", "vidrio"),
            new ImagenFila("vidrio_ficha", "La ficha de vidrio", "Desenfoca lo de detrás: apagada hasta medirla en el A4.", "vidrio"),
            new ImagenFila("vidrio_franja", "La franja «AHORA SUENA» de vidrio", "Desenfoca lo de detrás: apagada hasta medirla en el A4.", "vidrio"),
            new ImagenFila("vidrio_lista", "La lista del disco de vidrio", "Tenida por la portada; desenfoca: apagada hasta medirla.", "vidrio"),
            new ImagenFila("vidrio_buscador", "El buscador, los avisos y las ventanas", "Desenfoca lo de detrás: apagada hasta medirla en el A4.", "vidrio"),
        };
        // la letra en el cielo (K6): su funcion es ella misma (pide la letra a LRCLIB o la del MP3); si el otro grupo le cambia el nombre, aqui
        internal static readonly ImagenFila ImagenLetra = new ImagenFila("letra_cielo", "La letra de lo que suena, en el cielo", "Pide la letra (LRCLIB o la del MP3). Vale al siguiente arranque.", null);

        private static int imagenSub;   // la hojita que se ve (se recuerda mientras la rocola siga abierta)
        private static readonly string[] ImagenMeses = { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };
        private static readonly string[] ImagenClimas = { "despejado", "poco nublado", "nublado", "neblina", "llovizna", "lluvia", "aguacero", "tormenta", "nieve" };
        private static readonly string[] ImagenClimasClave = { "despejado", "poco", "nublado", "neblina", "llovizna", "lluvia", "aguacero", "tormenta", "nieve" };

        // lo que dice el estado de una funcion (en marcha, o desde cuando)
        internal static string RocolaOsImagenEstadoFuncion(string funcion, out int color)
        {
            bool va = RocolaOs.Encendida(funcion), pide = RocolaOs.AjusteSiNo(funcion);
            if (va && pide) { color = 0; return "En marcha."; }
            if (!va && pide) { color = 1; return "Encendida: empieza en el siguiente arranque."; }
            if (va) { color = 1; return "Apagada: se quita en el siguiente arranque."; }
            color = 2; return "Apagada (de fábrica aquí). Enciéndela y reabre la rocola.";
        }

        private RocolaOsPagina RocolaOsPaginaImagen(RocolaOsVentanaCtx x)
        {
            Color cBien = Color.FromArgb(120, 230, 165), cCielo = Color.FromArgb(120, 190, 255), cFiesta = Color.FromArgb(255, 196, 120), cClima = Color.FromArgb(150, 215, 255),
                cVidrio = Color.FromArgb(190, 160, 255), cVer = Color.FromArgb(255, 150, 190);
            Grupo pg = new Grupo(); pg.SetBounds(0, 96, 960, 580); pg.BackColor = x.CFondo;
            const int ancho = 424, altoTar = 530;
            List<Action> alMedioSegundo = new List<Action>();
            bool poniendo = false;   // (sincronizar un interruptor con lo guardado no vuelve a guardar)

            Func<Tarjeta, int, string, int, Rotulo> rot = delegate (Tarjeta t, int y, string texto, int alto)
            {
                Rotulo r = new Rotulo(); r.SetBounds(16, y, ancho, alto); r.BackColor = Color.Transparent; r.ForeColor = x.CTenue; r.Font = x.FDes; r.Text = texto; r.UseMnemonic = false;
                t.Controls.Add(r); return r;
            };
            Func<Tarjeta, int, string, Rotulo> sub = delegate (Tarjeta t, int y, string texto)
            {
                Rotulo r = new Rotulo(); r.SetBounds(16, y, ancho, 22); r.BackColor = Color.Transparent; r.ForeColor = x.CTexto; r.Font = x.FNom; r.Text = texto; r.UseMnemonic = false;
                t.Controls.Add(r); return r;
            };

            // ---------- UNA TARJETA DE INTERRUPTORES (una funcion y sus ideas) ----------
            Func<ImagenFila[], string, Color, Tarjeta> armaInterruptores = delegate (ImagenFila[] filas, string titulo, Color acento)
            {
                Tarjeta t = new Tarjeta(); t.SetBounds(16, 40, 456, altoTar); t.Titulo = titulo; t.Acento = acento;
                string funcion = filas[0].Clave;
                Rotulo estado = new Rotulo(); estado.SetBounds(16, 36, ancho, 18); estado.BackColor = Color.Transparent; estado.Font = x.FDes; estado.UseMnemonic = false; t.Controls.Add(estado);
                int y = 62;
                List<Tuple<ImagenFila, Interruptor>> ints = new List<Tuple<ImagenFila, Interruptor>>();
                foreach (ImagenFila f in filas)
                {
                    Interruptor s = new Interruptor(); s.SetBounds(16, y, ancho, 26); s.Font = x.FSub; s.ForeColor = x.CTexto; s.Acento = acento; s.Text = f.Nombre;
                    s.Name = f.Clave;
                    Rotulo d = rot(t, y + 27, f.Que, 18);
                    t.Controls.Add(s);
                    ImagenFila ff = f; Interruptor ss = s;
                    s.CheckedChanged += delegate
                    {
                        if (poniendo) return;
                        if (RocolaOs.PonAjuste(ff.Clave, ss.Checked ? "1" : "0")) RocolaOs.Apunta("ventana", "Imagen: " + ff.Clave + "=" + (ss.Checked ? "1" : "0"));
                    };
                    ints.Add(Tuple.Create(f, s));
                    y += 54;
                }
                Action pinta = delegate
                {
                    int color; string e = RocolaOsImagenEstadoFuncion(funcion, out color);
                    if (estado.Text != e) estado.Text = e;
                    Color c = color == 0 ? cBien : color == 1 ? x.CAviso : x.CApagado;
                    if (estado.ForeColor != c) estado.ForeColor = c;
                    bool madre = RocolaOs.AjusteSiNo(funcion);
                    poniendo = true;
                    foreach (Tuple<ImagenFila, Interruptor> p in ints)
                    {
                        bool v = RocolaOs.AjusteSiNo(p.Item1.Clave);
                        if (p.Item2.Checked != v) p.Item2.Checked = v;
                        bool idea = p.Item1.Funcion != null;
                        bool habil = !idea || madre;
                        if (p.Item2.Enabled != habil) p.Item2.Enabled = habil;
                        p.Item2.PonNecesita(idea && !madre ? "necesita «" + p.Item1.Funcion + "»" : null);
                    }
                    poniendo = false;
                };
                alMedioSegundo.Add(pinta);
                pinta();
                return t;
            };

            // ================= DIA Y NOCHE =================
            Tarjeta tDia = armaInterruptores(ImagenDia, "EL CIELO · DÍA Y NOCHE", cCielo);
            Tarjeta tCiu = new Tarjeta(); tCiu.SetBounds(488, 40, 456, 330); tCiu.Titulo = "LA CIUDAD DEL BAR"; tCiu.Acento = cCielo;
            Rotulo ciuNom = new Rotulo(); ciuNom.SetBounds(16, 40, ancho, 22); ciuNom.BackColor = Color.Transparent; ciuNom.ForeColor = cCielo; ciuNom.Font = x.FVal; ciuNom.UseMnemonic = false;
            Deslizador ciuBar = new Deslizador(); ciuBar.SetBounds(16, 68, ancho, 22); ciuBar.Minimum = 0; ciuBar.Maximum = RocolaOsCiudades.Maxima; ciuBar.Acento = cCielo;
            Rotulo ciuInfo = rot(tCiu, 98, "", 52);
            Rotulo ciuAhora = rot(tCiu, 158, "", 130);
            tCiu.Controls.Add(ciuNom); tCiu.Controls.Add(ciuBar);
            int ciu = RocolaOsCielo.CiudadElegida;
            ciuBar.Value = ciu;
            bool ciuSucia = false;
            Action pintaCiu = delegate
            {
                ciuNom.Text = ciuBar.Value == 0 ? "Automática (por internet)" : RocolaOsCiudades.Nombres[ciuBar.Value];
                ciuInfo.Text = ciuBar.Value == 0
                    ? "El lugar sale de internet una vez por semana; mientras no se sepa, vale la Ciudad de México. Mueve la barra para fijar una de las 41 ciudades."
                    : "Fija la ciudad: el sol, la luna y el clima son los de allí. Vuelve a la izquierda del todo para la automática.";
            };
            ciuBar.Scroll += delegate { ciuSucia = true; pintaCiu(); };
            pintaCiu();
            Action guardaCiu = delegate
            {
                if (!ciuSucia) return;
                ciuSucia = false;
                if (RocolaOs.PonAjuste("ciudad_bar", ciuBar.Value.ToString(CultureInfo.InvariantCulture))) RocolaOs.Apunta("ventana", "Imagen: ciudad_bar=" + ciuBar.Value);
            };
            alMedioSegundo.Add(delegate
            {
                RocolaOsCieloFoto f = RocolaOsCielo.Foto;
                string t;
                if (f == null) t = "El cielo vivo no va en esta sesión (sus cuatro funciones están apagadas): aquí no hay hora del sol que ver.";
                else t = (f.LugarSabido ? "" : "Lugar sin saber: elige la ciudad.\n") + "Ahora: " + f.Lugar.Replace('\n', ' ') + " (" + f.Lat.ToString("0.00", CultureInfo.InvariantCulture) + ", " + f.Lon.ToString("0.00", CultureInfo.InvariantCulture) + ")\n"
                    + "Hora del cielo " + f.Local.ToString("HH:mm", CultureInfo.InvariantCulture) + ", sol a " + f.SolAlt.ToString("0", CultureInfo.InvariantCulture) + "° ("
                    + (f.SolSube ? "sube" : "baja") + "), fase «" + f.Fase + "»." + (f.VerComo ? "\nSe está viendo «Ver cómo se vería»." : "");
                if (ciuAhora.Text != t) ciuAhora.Text = t;
            });
            Tarjeta tLet = armaInterruptoresUno(x, ImagenLetra, "LA LETRA EN EL CIELO", Color.FromArgb(255, 206, 120), alMedioSegundo, () => poniendo, v => poniendo = v, cBien);
            tLet.SetBounds(488, 380, 456, 190);

            // ================= FIESTAS =================
            Tarjeta tFie = armaInterruptores(ImagenFiestas, "LAS FIESTAS", cFiesta);
            Tarjeta tFec = armaFechas(x, cFiesta);

            // ================= CLIMA =================
            Tarjeta tCli = armaInterruptores(ImagenClima, "EL CLIMA", cClima);
            Tarjeta tCli2 = new Tarjeta(); tCli2.SetBounds(488, 40, 456, altoTar); tCli2.Titulo = "EL CLIMA DE AFUERA, AHORA"; tCli2.Acento = cClima;
            Rotulo cliAhora = rot(tCli2, 40, "", 150);
            rot(tCli2, 200, "Se pide a Open-Meteo cada 15 minutos, para la ciudad del bar (hoja «Día y noche»). Sin internet el último dato vale 3 horas y después el cielo "
                + "vuelve a lo normal. Nada se pide mientras la función «clima» está apagada.\n\nPara ver cómo quedaría un clima que hoy no hace, usa «Ver cómo se vería».", 170);
            alMedioSegundo.Add(delegate
            {
                RocolaOsCieloFoto f = RocolaOsCielo.Foto;
                string t;
                if (f == null) t = "El cielo vivo no va en esta sesión: no se sabe.";
                else if (!f.LugarSabido) t = "Lugar sin saber: elige la ciudad en la hoja «Día y noche». Sin el lugar no se pide ni se enseña el clima.";
                else if (!f.ClimaSabido) t = "Todavía no se sabe el clima de afuera (se ve despejado).";
                else
                {
                    RocolaOsClima c = f.Clima;
                    t = c.Nombre + (c.Forzado ? " (forzado: «Ver cómo se vería» o prueba)" : "") + "\nNubes " + (int)Math.Round(c.Nubes * 100) + " %, lluvia " + c.Lluvia.ToString("0.#", CultureInfo.InvariantCulture)
                        + " mm/h, viento " + c.Viento.ToString("0", CultureInfo.InvariantCulture) + " km/h"
                        + (float.IsNaN(c.Temperatura) ? "" : ", " + c.Temperatura.ToString("0", CultureInfo.InvariantCulture) + " °C")
                        + (c.Forzado ? "" : "\nMedido " + c.Medida.ToLocalTime().ToString("dd-MM HH:mm", CultureInfo.InvariantCulture));
                }
                if (cliAhora.Text != t) cliAhora.Text = t;
                cliAhora.ForeColor = x.CTexto;
            });

            // ================= VIDRIO =================
            Tarjeta tVid = armaInterruptores(ImagenVidrio, "EL VIDRIO", cVidrio);
            Tarjeta tVid2 = new Tarjeta(); tVid2.SetBounds(488, 40, 456, altoTar); tVid2.Titulo = "QUÉ HACE Y CUÁNTO CUESTA"; tVid2.Acento = cVidrio;
            rot(tVid2, 40, "Son las mismas piezas de siempre, en su sitio, con su tamaño, colores y letra: solo cambia el material.\n\n"
                + "Las pastillas y el filo de luz cuestan poco y van encendidas cuando enciendes «Vidrio».\n\n"
                + "La ficha, la franja «AHORA SUENA», la lista del disco y el buscador DESENFOCAN lo de detrás: cuestan más de dibujar y van apagadas hasta medirlas con el "
                + "«Diagnóstico completo» de Ctrl+F3 en la rocola de verdad (el A4). Enciéndelas solo para probar.\n\n"
                + "Un vidrio nunca tapa un texto: si algo no se lee, apágalo.", 360);

            // ================= VER COMO SE VERIA =================
            Tarjeta tVer = new Tarjeta(); tVer.SetBounds(16, 40, 456, altoTar); tVer.Titulo = "VER CÓMO SE VERÍA"; tVer.Acento = cVer;
            Tarjeta tVer2 = new Tarjeta(); tVer2.SetBounds(488, 40, 456, altoTar); tVer2.Titulo = "CÓMO FUNCIONA"; tVer2.Acento = cVer;
            int hoyDia = DateTime.Today.DayOfYear - 1;
            int anio = DateTime.Today.Year, diasAnio = DateTime.IsLeapYear(anio) ? 366 : 365;
            Interruptor swHora = new Interruptor(); swHora.SetBounds(16, 40, ancho, 26); swHora.Font = x.FSub; swHora.ForeColor = x.CTexto; swHora.Acento = cVer; swHora.Text = "Cambiar la hora";
            Rotulo valHora = new Rotulo(); valHora.SetBounds(16 + ancho - 170, 70, 170, 20); valHora.BackColor = Color.Transparent; valHora.ForeColor = cVer; valHora.Font = x.FVal; valHora.TextAlign = AlineaTexto.TopRight;
            Deslizador barHora = new Deslizador(); barHora.SetBounds(16, 70, ancho - 180, 22); barHora.Minimum = 0; barHora.Maximum = 47; barHora.Value = 40; barHora.Acento = cVer;
            Interruptor swFecha = new Interruptor(); swFecha.SetBounds(16, 112, ancho, 26); swFecha.Font = x.FSub; swFecha.ForeColor = x.CTexto; swFecha.Acento = cVer; swFecha.Text = "Cambiar la fecha";
            Deslizador barFecha = new Deslizador(); barFecha.SetBounds(16, 142, ancho, 22); barFecha.Minimum = 0; barFecha.Maximum = diasAnio - 1; barFecha.Value = hoyDia; barFecha.Acento = cVer;
            Rotulo valFecha = new Rotulo(); valFecha.SetBounds(16, 168, ancho, 40); valFecha.BackColor = Color.Transparent; valFecha.ForeColor = cVer; valFecha.Font = x.FNom; valFecha.UseMnemonic = false;
            BotonPlano bFAnt = new BotonPlano(); bFAnt.SetBounds(16, 212, 206, 30); bFAnt.Font = x.FBot; bFAnt.Text = "« Fiesta anterior";
            BotonPlano bFSig = new BotonPlano(); bFSig.SetBounds(234, 212, 206, 30); bFSig.Font = x.FBot; bFSig.Text = "Fiesta siguiente »";
            Interruptor swClima = new Interruptor(); swClima.SetBounds(16, 262, ancho, 26); swClima.Font = x.FSub; swClima.ForeColor = x.CTexto; swClima.Acento = cVer; swClima.Text = "Cambiar el clima";
            Deslizador barClima = new Deslizador(); barClima.SetBounds(16, 292, ancho - 180, 22); barClima.Minimum = 0; barClima.Maximum = ImagenClimas.Length - 1; barClima.Value = 0; barClima.Acento = cVer;
            Rotulo valClima = new Rotulo(); valClima.SetBounds(16 + ancho - 170, 292, 170, 20); valClima.BackColor = Color.Transparent; valClima.ForeColor = cVer; valClima.Font = x.FVal; valClima.TextAlign = AlineaTexto.TopRight;
            BotonPlano bVer = new BotonPlano(); bVer.SetBounds(16, 340, ancho, 40); bVer.Font = x.FBot; bVer.Relleno = Color.FromArgb(30, 96, 70); bVer.Borde = Color.FromArgb(72, 182, 132);
            BotonPlano bQuita = new BotonPlano(); bQuita.SetBounds(16, 388, ancho, 34); bQuita.Font = x.FBot; bQuita.Text = "Quitar lo que se ve y volver a lo de ahora";
            Rotulo verEstado = rot(tVer, 432, "", 90);
            tVer.Controls.Add(swHora); tVer.Controls.Add(barHora); tVer.Controls.Add(valHora); tVer.Controls.Add(swFecha); tVer.Controls.Add(barFecha); tVer.Controls.Add(valFecha);
            tVer.Controls.Add(bFAnt); tVer.Controls.Add(bFSig); tVer.Controls.Add(swClima); tVer.Controls.Add(barClima); tVer.Controls.Add(valClima); tVer.Controls.Add(bVer); tVer.Controls.Add(bQuita);
            rot(tVer2, 40, "Elige una hora, una fecha o una fiesta, y un clima; «Verlo» cierra esta ventana y muestra la rocola con eso durante "
                + RocolaOsCielo.VerComoSegundos + " segundos.\n\n"
                + "No se guarda nada: vuelve sola a los " + RocolaOsCielo.VerComoSegundos + " segundos, o en cuanto alguien toca un botón.\n\n"
                + "Lo que no cambies sigue siendo de ahora (la hora, la fecha o el clima de afuera). Con la fecha de una fiesta se ven sus colores y adornos; "
                + "los fuegos del Grito y de Año Nuevo piden además su hora (23:00 y 00:00).\n\n"
                + "Mientras alguien elige, el cielo se calma al 35 %: los efectos se ven a medio brillo en los primeros 15 segundos tras el último botón, "
                + "y completos después.\n\nHace falta el cielo vivo andando: alguna de sus cuatro funciones encendida desde el arranque.", 400);
            Rotulo verNota = rot(tVer2, 450, "", 70);
            // lo que dice cada deslizador
            Func<int, DateTime> diaDe = n => new DateTime(anio, 1, 1).AddDays(Math.Max(0, Math.Min(diasAnio - 1, n)));
            Action pintaVer = delegate
            {
                valHora.Text = (barHora.Value / 2).ToString("00", CultureInfo.InvariantCulture) + ":" + (barHora.Value % 2 == 0 ? "00" : "30");
                DateTime d = diaDe(barFecha.Value);
                List<RocolaOsFiesta> fs = RocolaOsCalendario.DeFabrica(d);
                string nom = "";
                foreach (RocolaOsFiesta f in fs) nom += (nom.Length > 0 ? " y " : "") + f.Nombre;
                valFecha.Text = d.Day + " de " + ImagenMeses[d.Month - 1] + (nom.Length > 0 ? "\n" + nom : "\n(ninguna fiesta de fábrica)");
                valClima.Text = ImagenClimas[barClima.Value];
                bool va = RocolaOsCielo.Va;
                bool algo = swHora.Checked || swFecha.Checked || swClima.Checked;
                barHora.Enabled = swHora.Checked; barFecha.Enabled = swFecha.Checked; bFAnt.Enabled = swFecha.Checked; bFSig.Enabled = swFecha.Checked; barClima.Enabled = swClima.Checked;
                swHora.Enabled = va; swFecha.Enabled = va; swClima.Enabled = va;
                swHora.PonNecesita(va ? null : "necesita el cielo vivo"); swFecha.PonNecesita(va ? null : "necesita el cielo vivo"); swClima.PonNecesita(va ? null : "necesita el cielo vivo");
                bVer.Enabled = va && algo;
                bVer.Text = va ? (algo ? "Verlo " + RocolaOsCielo.VerComoSegundos + " segundos (cierra esta ventana)" : "Elige algo que cambiar") : "Necesita el cielo vivo andando";
                RocolaOsCieloFoto ft = RocolaOsCielo.Foto;
                bool activo = ft != null && ft.VerComo;
                bQuita.Enabled = activo;
                verNota.Text = va ? "" : "El cielo vivo no va en esta sesión. Enciende «Cielo vivo» (u otra de las cuatro funciones) en su hojita y reabre la rocola: vale desde el siguiente arranque.";
                verNota.ForeColor = x.CAviso;
                verEstado.Text = activo ? "Ahora se está viendo un «Ver cómo se vería»." : "";
                verEstado.ForeColor = cBien;
                foreach (Interruptor s in new[] { swHora, swFecha, swClima }) s.Invalidate();
                barHora.Invalidate(); barFecha.Invalidate(); barClima.Invalidate();
            };
            barHora.Scroll += delegate { pintaVer(); }; barFecha.Scroll += delegate { pintaVer(); }; barClima.Scroll += delegate { pintaVer(); };
            swHora.CheckedChanged += delegate { pintaVer(); }; swFecha.CheckedChanged += delegate { pintaVer(); }; swClima.CheckedChanged += delegate { pintaVer(); };
            Func<int, int> fiestaDesde = delegate (int sentido)
            {
                int n = barFecha.Value;
                for (int k = 1; k <= diasAnio; k++)
                {
                    int m = ((n + sentido * k) % diasAnio + diasAnio) % diasAnio;
                    if (RocolaOsCalendario.DeFabrica(diaDe(m)).Count > 0) return m;
                }
                return n;
            };
            bFAnt.Click += delegate { barFecha.Value = fiestaDesde(-1); pintaVer(); };
            bFSig.Click += delegate { barFecha.Value = fiestaDesde(1); pintaVer(); };
            bVer.Click += delegate
            {
                DateTime? local = null;
                DateTime ahora = DateTime.Now;
                if (swHora.Checked || swFecha.Checked)
                {
                    DateTime d = swFecha.Checked ? diaDe(barFecha.Value) : ahora.Date;
                    TimeSpan h = swHora.Checked ? new TimeSpan(barHora.Value / 2, barHora.Value % 2 * 30, 0) : ahora.TimeOfDay;
                    local = d + h;
                }
                string clima = swClima.Checked ? ImagenClimasClave[barClima.Value] : null;
                string r = RocolaOsCielo.VerComo(local, clima);   // (no «desde la hoja»: un boton de la rocola lo quita)
                if (r.StartsWith("bien", StringComparison.Ordinal)) x.Dlg.Close();
                else { verNota.Text = r; pintaVer(); }
            };
            bQuita.Click += delegate { RocolaOsCielo.QuitaVerComo("la hoja Imagen"); pintaVer(); };
            pintaVer();
            alMedioSegundo.Add(pintaVer);

            // ================= LAS HOJITAS =================
            Tarjeta[][] subs = {
                new[] { tDia, tCiu, tLet }, new[] { tFie, tFec }, new[] { tCli, tCli2 }, new[] { tVid, tVid2 }, new[] { tVer, tVer2 } };
            string[] nombres = { "Día y noche", "Fiestas", "Clima", "Vidrio", "Ver cómo se vería" };
            int[] anchos = { 160, 130, 130, 130, 250 };
            List<BotonPlano> pes = new List<BotonPlano>();
            for (int i = 0; i < nombres.Length; i++)
            {
                BotonPlano b = new BotonPlano(); b.Font = x.FBot; b.Text = nombres[i];
                int xi = 16; for (int k = 0; k < i; k++) xi += anchos[k] + 8;
                b.SetBounds(xi, 2, anchos[i], 30);
                pes.Add(b); pg.Controls.Add(b);
            }
            foreach (Tarjeta[] g in subs) foreach (Tarjeta t in g) pg.Controls.Add(t);
            if (imagenSub < 0 || imagenSub >= subs.Length) imagenSub = 0;
            Action pintaSub = delegate
            {
                for (int i = 0; i < subs.Length; i++)
                {
                    bool act = i == imagenSub;
                    foreach (Tarjeta t in subs[i]) t.Visible = act;
                    pes[i].Relleno = act ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                    pes[i].Borde = act ? Color.FromArgb(150, 205, 255) : Color.FromArgb(60, 72, 118);
                    pes[i].ForeColor = act ? Color.FromArgb(226, 240, 255) : x.CApagado;
                    pes[i].Invalidate();
                }
            };
            for (int i = 0; i < pes.Count; i++) { int ii = i; pes[i].Click += delegate { imagenSub = ii; pintaSub(); }; }
            pintaSub();

            Interlocked.Increment(ref RocolaOsCielo.HojasAbiertas);   // (mientras la ventana esta abierta el cielo se calma: regla 5 del plan)
            RocolaOsPagina pag = new RocolaOsPagina();
            pag.Panel = pg; pag.Titulo = "Imagen";
            pag.CadaMedioSegundo = delegate
            {
                foreach (Action a in alMedioSegundo) a();
                guardaCiu();
            };
            pag.AlCerrar = delegate { guardaCiu(); Interlocked.Decrement(ref RocolaOsCielo.HojasAbiertas); };
            return pag;
        }

        // UNA TARJETA CON UN SOLO INTERRUPTOR DE FUNCION (la letra en el cielo): lo mismo que armaInterruptores, con una fila
        private Tarjeta armaInterruptoresUno(RocolaOsVentanaCtx x, ImagenFila f, string titulo, Color acento, List<Action> alMedioSegundo,
            Func<bool> poniendo, Action<bool> ponPoniendo, Color cBien)
        {
            Tarjeta t = new Tarjeta(); t.SetBounds(488, 380, 456, 190); t.Titulo = titulo; t.Acento = acento;
            Rotulo estado = new Rotulo(); estado.SetBounds(16, 36, 424, 18); estado.BackColor = Color.Transparent; estado.Font = x.FDes; estado.UseMnemonic = false; t.Controls.Add(estado);
            Interruptor s = new Interruptor(); s.SetBounds(16, 62, 424, 26); s.Font = x.FSub; s.ForeColor = x.CTexto; s.Acento = acento; s.Text = f.Nombre; s.Name = f.Clave;
            Rotulo d = new Rotulo(); d.SetBounds(16, 89, 424, 56); d.BackColor = Color.Transparent; d.ForeColor = x.CTenue; d.Font = x.FDes; d.Text = f.Que
                + " La letra se canta en el reposo con música (la maqueta 3 que aprobaste); sin letra sincronizada va quieta.";
            d.UseMnemonic = false;
            t.Controls.Add(s); t.Controls.Add(d);
            s.CheckedChanged += delegate
            {
                if (poniendo()) return;
                if (RocolaOs.PonAjuste(f.Clave, s.Checked ? "1" : "0")) RocolaOs.Apunta("ventana", "Imagen: " + f.Clave + "=" + (s.Checked ? "1" : "0"));
            };
            Action pinta = delegate
            {
                bool registrada = RocolaOs.ClaveDe(f.Clave) != null;
                int color = 2; string e = registrada ? RocolaOsImagenEstadoFuncion(f.Clave, out color) : "Esta versión no trae la letra en el cielo.";
                if (estado.Text != e) estado.Text = e;
                Color c = color == 0 ? cBien : color == 1 ? x.CAviso : x.CApagado;
                if (estado.ForeColor != c) estado.ForeColor = c;
                s.Enabled = registrada;
                if (registrada)
                {
                    bool v = RocolaOs.AjusteSiNo(f.Clave);
                    ponPoniendo(true);
                    if (s.Checked != v) s.Checked = v;
                    ponPoniendo(false);
                }
            };
            alMedioSegundo.Add(pinta);
            pinta();
            return t;
        }

        // ---------- TUS FECHAS (rocola_fechas.txt) ----------
        private const int ImagenFechasFilas = 8;
        private Tarjeta armaFechas(RocolaOsVentanaCtx x, Color acento)
        {
            Tarjeta t = new Tarjeta(); t.SetBounds(488, 40, 456, 530); t.Titulo = "TUS FECHAS"; t.Acento = acento;
            const int ancho = 424;
            Rotulo nota = new Rotulo(); nota.SetBounds(16, 36, ancho, 32); nota.BackColor = Color.Transparent; nota.ForeColor = x.CTenue; nota.Font = x.FDes; nota.UseMnemonic = false;
            nota.Text = "rocola_fechas.txt: ese día el escaparate dice «¡Feliz cumpleaños, NOMBRE!» y suben globos y confeti. Toca un renglón para elegirlo.";
            t.Controls.Add(nota);
            List<BotonPlano> filas = new List<BotonPlano>();
            for (int i = 0; i < ImagenFechasFilas; i++)
            {
                BotonPlano b = new BotonPlano(); b.SetBounds(16, 76 + i * 34, ancho, 30); b.Font = x.FSub; b.Height = 30;
                t.Controls.Add(b); filas.Add(b);
            }
            BotonPlano bQuita = new BotonPlano(); bQuita.SetBounds(16, 76 + ImagenFechasFilas * 34 + 4, 208, 34); bQuita.Font = x.FBot; bQuita.Text = "Quitar la elegida";
            BotonPlano bArr = new BotonPlano(); bArr.SetBounds(232, 76 + ImagenFechasFilas * 34 + 4, 98, 34); bArr.Font = x.FBot; bArr.Text = "▲ Antes";
            BotonPlano bAba = new BotonPlano(); bAba.SetBounds(342, 76 + ImagenFechasFilas * 34 + 4, 98, 34); bAba.Font = x.FBot; bAba.Text = "Más ▼";
            BotonPlano bCum = new BotonPlano(); bCum.SetBounds(16, 76 + ImagenFechasFilas * 34 + 46, 206, 38); bCum.Font = x.FBot; bCum.Text = "Agregar un cumpleaños";
            BotonPlano bAni = new BotonPlano(); bAni.SetBounds(234, 76 + ImagenFechasFilas * 34 + 46, 206, 38); bAni.Font = x.FBot; bAni.Text = "Agregar un aniversario";
            Rotulo info = new Rotulo(); info.SetBounds(16, 76 + ImagenFechasFilas * 34 + 94, ancho, 64); info.BackColor = Color.Transparent; info.ForeColor = x.CTenue; info.Font = x.FDes; info.UseMnemonic = false;
            t.Controls.Add(bQuita); t.Controls.Add(bArr); t.Controls.Add(bAba); t.Controls.Add(bCum); t.Controls.Add(bAni); t.Controls.Add(info);
            List<RocolaOsImagenFechas.Entrada> lista = new List<RocolaOsImagenFechas.Entrada>();
            int sel = -1; int primera = 0; string aviso = "";
            Action pinta = null;
            Action recarga = delegate
            {
                lista = RocolaOsImagenFechas.Entradas(RocolaOsImagenFechas.Lee());
                if (sel >= lista.Count) sel = lista.Count - 1;
                if (primera > Math.Max(0, lista.Count - ImagenFechasFilas)) primera = Math.Max(0, lista.Count - ImagenFechasFilas);
                pinta();
            };
            pinta = delegate
            {
                for (int i = 0; i < ImagenFechasFilas; i++)
                {
                    int k = primera + i; BotonPlano b = filas[i];
                    bool hay = k < lista.Count;
                    b.Text = hay ? lista[k].Texto : (i == 0 && lista.Count == 0 ? "Todavía no hay ninguna fecha tuya." : "");
                    b.Enabled = hay;
                    bool act = hay && k == sel;
                    b.Relleno = act ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                    b.Borde = act ? Color.FromArgb(150, 205, 255) : Color.FromArgb(60, 72, 118);
                    b.ForeColor = hay ? (lista[k].Vale ? x.CTexto : x.CAviso) : x.CApagado;
                    b.Invalidate();
                }
                bQuita.Enabled = sel >= 0 && sel < lista.Count;
                bQuita.Invalidate();
                bArr.Enabled = primera > 0; bAba.Enabled = primera + ImagenFechasFilas < lista.Count; bArr.Invalidate(); bAba.Invalidate();
                string mas = lista.Count > ImagenFechasFilas ? "Hay " + lista.Count + " renglones: se ven " + ImagenFechasFilas + " a la vez. " : "";
                info.Text = (aviso.Length > 0 ? aviso + "\n" : "") + mas + "Fiestas propias, mensajes, fuegos y quitar una fiesta se escriben a mano en Ctrl+F4 → Archivos (rocola_fechas.txt, un renglón «DD-MM;tipo;texto»).";
                info.ForeColor = aviso.Length > 0 ? x.CAviso : x.CTenue;
            };
            for (int i = 0; i < ImagenFechasFilas; i++)
            {
                int ii = i;
                filas[i].Click += delegate { int k = primera + ii; if (k < lista.Count) { sel = k; pinta(); } };
            }
            bQuita.Click += delegate
            {
                if (sel < 0 || sel >= lista.Count) return;
                string nuevo = RocolaOsImagenFechas.Quita(RocolaOsImagenFechas.Lee(), lista[sel].Renglon);
                if (nuevo != null && RocolaOsImagenFechas.Guarda(nuevo)) { RocolaOs.Apunta("ventana", "Imagen: se quito «" + lista[sel].Texto + "» de rocola_fechas.txt"); aviso = "Quitada."; }
                else aviso = "No se pudo guardar rocola_fechas.txt.";
                sel = -1; recarga();
            };
            Action<string> agrega = delegate (string tipo)
            {
                string que = tipo == "cumple" ? "cumpleaños" : "aniversario";
                string nombre = RocolaOsImagenPideTexto(x, "Agregar un " + que, tipo == "cumple" ? "¿De quién es? (como saldrá: «¡Feliz cumpleaños, NOMBRE!»)" : "¿De qué es? (como saldrá: «¡Feliz aniversario, NOMBRE!»)", "", "Seguir",
                    s => RocolaOsImagenFechas.NombreNoVale(s));
                if (nombre == null) return;
                string fecha = RocolaOsImagenPideTexto(x, "Agregar un " + que, "¿Qué día? Día y mes, como 15-03.", "", "Agregar", delegate (string s) { string n; return RocolaOsImagenFechas.FechaNoVale(s, out n); });
                if (fecha == null) return;
                string nuevo = RocolaOsImagenFechas.Agrega(RocolaOsImagenFechas.Lee(), tipo, fecha, nombre);
                if (nuevo != null && RocolaOsImagenFechas.Guarda(nuevo)) { RocolaOs.Apunta("ventana", "Imagen: se agrego un " + que + " a rocola_fechas.txt"); aviso = "Agregado. Se ve ese día con las fiestas encendidas."; }
                else aviso = "No se pudo guardar rocola_fechas.txt.";
                recarga();
                sel = lista.Count - 1; primera = Math.Max(0, lista.Count - ImagenFechasFilas); pinta();
            };
            bArr.Click += delegate { primera = Math.Max(0, primera - ImagenFechasFilas); pinta(); };
            bAba.Click += delegate { primera = Math.Min(Math.Max(0, lista.Count - ImagenFechasFilas), primera + ImagenFechasFilas); pinta(); };
            bCum.Click += delegate { agrega("cumple"); };
            bAni.Click += delegate { agrega("aniversario"); };
            recarga();
            return t;
        }

        // PEDIR UN TEXTO (como RocolaOsPideNombre de Archivos, con su propia regla): una hoja chica con un renglon; null si se cancela
        private string RocolaOsImagenPideTexto(RocolaOsVentanaCtx x, string titulo, string texto, string inicial, string si, Func<string, string> noVale)
        {
            string res = null;
            using (Hoja q = new Hoja(MarcoXp.Dialogo))
            using (Rocola.Nativo.Letra fm = Rocola.Nativo.Letra.EnPuntos(Letras.AnchoFijo, 11f))
            {
                q.Text = "Rocola OS - " + titulo;
                q.ClientSize = new Size(600, 230);
                q.BackColor = x.CFondo; q.Font = x.FSub;
                Rotulo lT = new Rotulo(); lT.SetBounds(22, 14, 556, 28); lT.BackColor = Color.Transparent; lT.ForeColor = Color.FromArgb(232, 238, 255); lT.Font = x.FTit; lT.Text = titulo;
                Rotulo lD = new Rotulo(); lD.SetBounds(24, 48, 556, 20); lD.BackColor = Color.Transparent; lD.ForeColor = x.CTenue; lD.Font = x.FSub; lD.Text = texto; lD.UseMnemonic = false;
                RocolaOsEditor ed = new RocolaOsEditor(); ed.UnRenglon = true; ed.Tope = 120; ed.Letra = fm; ed.SetBounds(22, 76, 556, 40); ed.TabIndex = 0;
                ed.Texto = inicial; ed.AlFinal();
                Rotulo lE = new Rotulo(); lE.SetBounds(24, 122, 556, 34); lE.BackColor = Color.Transparent; lE.ForeColor = x.CAviso; lE.Font = x.FDes; lE.UseMnemonic = false;
                BotonPlano bNo = new BotonPlano(); bNo.SetBounds(22, 168, 270, 40); bNo.Font = x.FBot; bNo.TabIndex = 2; bNo.Text = "Cancelar";
                BotonPlano bSi = new BotonPlano(); bSi.SetBounds(308, 168, 270, 40); bSi.Font = x.FBot; bSi.TabIndex = 1; bSi.Text = si;
                bSi.Relleno = Color.FromArgb(30, 96, 70); bSi.Borde = Color.FromArgb(72, 182, 132);
                Action acepta = delegate
                {
                    string n = ed.Texto; string e = noVale(n);
                    if (e != null) { lE.Text = e; ed.Focus(); return; }
                    res = n.Trim(); q.Close();
                };
                ed.Intro += acepta;
                bSi.Click += delegate { acepta(); };
                bNo.Click += delegate { res = null; q.Close(); };
                q.Controls.Add(ed); q.Controls.Add(bSi); q.Controls.Add(bNo); q.Controls.Add(lT); q.Controls.Add(lD); q.Controls.Add(lE);
                q.TeclaAntes += delegate (PulsoTecla ke) { if (ke.KeyCode == Tecla.Escape) { res = null; q.Close(); } };
                // (SIN MonedaTambienAqui, como RocolaOsPideNombre: aqui se escribe y una moneda es una tecla)
                q.MuestraModal(x.Dlg);
            }
            return res;
        }
    }
}
