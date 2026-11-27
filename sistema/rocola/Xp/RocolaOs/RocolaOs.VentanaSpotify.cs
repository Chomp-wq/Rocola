// ROCOLA OS (fase 11): LA HOJITA «SPOTIFY» DE LA VENTANA DE CTRL+F4 (mejora que el dueno eligio el 27-11-2026; FASE11_hoja-spotify.md): para ver
// de un vistazo como va Spotify en la rocola, con el mismo aspecto de las hojas Pantalla y Sonido (las tarjetas de Ctrl+F3). Solo se VE; el
// unico boton es «Vaciar la cache» (lo que la rocola recuerda de lo que contesto Spotify; se vuelve a pedir solo, no toca la cuenta ni nada mas).
//   SOLOIST Y LA CUENTA: el programa de Spotify (fase del vigilante rocola-soloist, su version, cuando caduca y cuantos dias le quedan), si la
//     rocola esta conectada a el, si esta emparejado (sesion), la bocina, lo que hace; y la API web (llave de la app, cuenta, bien o mal, el
//     ultimo codigo, 429, peticiones de la ultima hora, el ultimo exito).
//   LO PEDIDO: cobradas, devueltas, no pedidas y en cola de esta sesion, y las ultimas canciones de Spotify (acabadas o devueltas y por que;
//     salen de la bitacora de Spotify, rocola_spotify.txt, sus ultimas lineas).
//   LA CACHE: la memoria en disco de lo que contesto Spotify (RocolaOs.SpotifyCatalogoMemoria.cs): aciertos, archivos y peso.
// NUNCA se ensena una llave ni nada de la cuenta (la hoja no lee los archivos de las llaves). Se refresca cada segundo con la ventana abierta.
// Sin el modo Spotify encendido la hoja existe y lo dice.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Rocola.Nativo;

namespace Rocola
{
    // lo que la hoja sabe de Spotify en un momento (se arma en un lugar para poder probarlo y no dibujar con datos a medias)
    internal sealed class RocolaOsSpotifyVista
    {
        internal bool Encendido, Ws, Sesion, Activa;
        internal string Fase = "-", Version = "", Caduca = "", Dias = "", Dispositivo = "", Reproduce = "";
        internal string Api = "-"; internal bool Llave, Cuenta;
        internal string UltimoCodigo = "-", Peticiones60 = "-", Ultimo429 = "-", UltimoExito = "-", FrenaS = "0";
        internal long Cobradas, Devueltas, NoPedidas, EnCola;
        internal List<string[]> Ultimas = new List<string[]>();   // {hora, cuenta (acabo/DEVUELTA/NO PEDIDO), titulo, detalle}
        internal long CacheAciertos, CacheVencidas, CacheSinNada, CacheGuardadas, CacheBytes; internal int CacheArchivos; internal bool CacheDisco;
    }

    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraHojitaSpotify()
        {
            // (Orden 16: entre Sonido y Navegador. Las pestanas van pegadas a la derecha: las de despues no se mueven)
            RocolaOsHojitas.Registra(new RocolaOsHojita { Nombre = "Spotify", Orden = 16, Arma = (f, x) => f.RocolaOsPaginaSpotify(x) });
        }

        private const int SpotifyUltimasMax = 7;
        // (27-11-2026, peticion del dueno) las paginas por letra de la pared: de 10 en 10 hasta 100 y de 50 en 50 hasta 1000 (29 pasos); 100 de fabrica
        internal const string SpotifyClavePaginas = "pared_paginas";
        private static readonly int[] SpotifyPasosPaginas = ArmaPasosPaginas();
        private static int[] ArmaPasosPaginas()
        {
            List<int> l = new List<int>();
            for (int n = 10; n <= 100; n += 10) l.Add(n);
            for (int n = 150; n <= 1000; n += 50) l.Add(n);
            return l.ToArray();
        }
        // el paso mas cercano a un valor (la clave puede traer cualquiera de 10 a 1000)
        private static int SpotifyPasoDe(int paginas)
        {
            int mejor = 0;
            for (int i = 0; i < SpotifyPasosPaginas.Length; i++) if (Math.Abs(SpotifyPasosPaginas[i] - paginas) < Math.Abs(SpotifyPasosPaginas[mejor] - paginas)) mejor = i;
            return mejor;
        }
        // cuanto ocuparian las portadas como mucho: paginas x 18 discos x 27 letras x ~15 KB
        internal static string RocolaOsSpotifyPesoPaginas(int paginas)
        {
            long discos = (long)paginas * 18 * 27;
            return RocolaOsSpotifyPeso(discos * 15 * 1024) + " (" + discos.ToString(CultureInfo.InvariantCulture) + " discos)";
        }

        private static Dictionary<string, string> RocolaOsSpotifyClaves(string texto)
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.Ordinal);
            if (texto == null) return d;
            foreach (string l in texto.Split('\n'))
            {
                int i = l.IndexOf('=');
                if (i > 0) d[l.Substring(0, i)] = l.Substring(i + 1).Trim();
            }
            return d;
        }
        private static string RocolaOsSpotifyLeeEstadoVigilante()
        {
            try { return File.ReadAllText(RocolaOs.Run("rocola-soloist/estado")); } catch { return null; }
        }
        // las ultimas lineas de la bitacora de Spotify que dicen como acabo una pedida: «FECHA HORA  acabo URI (TITULO): MOTIVO, sono S s ...»
        private static readonly Regex SpotifyLineaFin = new Regex(@"^\d{4}-\d\d-\d\d (\d\d:\d\d):\d\d  (acabo|DEVUELTA) spotify:\S+ \((.*?)\): (.*?), sono ([0-9.]+) s", RegexOptions.CultureInvariant);
        private static readonly Regex SpotifyLineaNoPedido = new Regex(@"^\d{4}-\d\d-\d\d (\d\d:\d\d):\d\d  NO PEDIDO: Soloist .*?«[^»]*» \((.*?)\)", RegexOptions.CultureInvariant);
        internal static List<string[]> RocolaOsSpotifyUltimasDeBitacora(string ruta, int max)
        {
            List<string[]> r = new List<string[]>();
            try
            {
                string[] ls;
                using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long ini = Math.Max(0, fs.Length - 160 * 1024);
                    fs.Seek(ini, SeekOrigin.Begin);
                    byte[] b = new byte[fs.Length - ini];
                    int n = 0, k;
                    while (n < b.Length && (k = fs.Read(b, n, b.Length - n)) > 0) n += k;
                    string t = Encoding.UTF8.GetString(b, 0, n);
                    ls = t.Split('\n');
                    if (ini > 0 && ls.Length > 0) ls[0] = "";   // (la primera puede venir cortada)
                }
                for (int i = ls.Length - 1; i >= 0 && r.Count < max; i--)
                {
                    string l = ls[i].TrimEnd('\r');
                    Match m = SpotifyLineaFin.Match(l);
                    if (m.Success) { r.Add(new[] { m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value + ", " + m.Groups[5].Value + " s" }); continue; }
                    m = SpotifyLineaNoPedido.Match(l);
                    if (m.Success) r.Add(new[] { m.Groups[1].Value, "NO PEDIDO", m.Groups[2].Value, "sin cobrar: Soloist toco algo que la rocola no pidio" });
                }
            }
            catch { }
            return r;
        }
        // todo junto, sin dibujar (lo lee la hoja cada segundo y la prueba)
        internal static RocolaOsSpotifyVista RocolaOsSpotifyArmaVista()
        {
            RocolaOsSpotifyVista v = new RocolaOsSpotifyVista();
            v.Encendido = RocolaOsSpotify.Encendido;
            RocolaOsSpotifyEstado e = RocolaOsSpotify.Estado;
            v.Ws = e.Ws; v.Sesion = e.Sesion; v.Activa = e.Activa; v.Dispositivo = e.Dispositivo ?? ""; v.Reproduce = e.Status ?? "";
            Dictionary<string, string> vg = RocolaOsSpotifyClaves(RocolaOsSpotifyLeeEstadoVigilante());
            string x;
            if (vg.TryGetValue("fase", out x) && x.Length > 0) v.Fase = x;
            if (vg.TryGetValue("version", out x)) v.Version = x;
            if (vg.TryGetValue("caduca", out x)) v.Caduca = x;
            if (vg.TryGetValue("dias", out x)) v.Dias = x;
            Dictionary<string, string> api = RocolaOsSpotifyClaves(RocolaOsSpotifyCatalogoApi.TextoEstado());
            if (api.TryGetValue("api", out x)) v.Api = x;
            v.Llave = api.TryGetValue("llave", out x) && x == "si"; v.Cuenta = api.TryGetValue("cuenta", out x) && x == "si";
            if (api.TryGetValue("ultimo_codigo", out x)) v.UltimoCodigo = x;
            if (api.TryGetValue("peticiones_60min", out x)) v.Peticiones60 = x;
            if (api.TryGetValue("ultimo_429", out x)) v.Ultimo429 = x;
            if (api.TryGetValue("ultimo_exito", out x)) v.UltimoExito = x;
            if (api.TryGetValue("frena_s", out x)) v.FrenaS = x;
            v.Cobradas = RocolaOsSpotify.Cobradas; v.Devueltas = RocolaOsSpotify.Devueltas; v.NoPedidas = RocolaOsSpotify.NoPedidas; v.EnCola = RocolaOsSpotify.EnCola;
            v.Ultimas = RocolaOsSpotifyUltimasDeBitacora(RocolaOs.Datos("rocola_spotify.txt"), SpotifyUltimasMax);
            v.CacheDisco = RocolaOsSpotifyMemoria.Activa;
            long a, ve, sn, g, b; int n;
            RocolaOsSpotifyMemoria.Numeros(out a, out ve, out sn, out g, out b, out n);
            v.CacheAciertos = a; v.CacheVencidas = ve; v.CacheSinNada = sn; v.CacheGuardadas = g; v.CacheBytes = b; v.CacheArchivos = n;
            return v;
        }
        private static string RocolaOsSpotifyFase(string f)
        {
            switch (f)
            {
                case "corriendo": return "en marcha";
                case "arrancando": return "arrancando";
                case "bajando": return "bajando uno nuevo";
                case "sin_llave": return "esperando su llave";
                case "sin_programa": return "sin programa (sin internet o sin bajarlo)";
                case "caducado": return "CADUCO (se baja otro)";
                case "esperando": return "esperando";
                case "parado": return "parado";
                case "-": return "sin vigilante";
                default: return f;
            }
        }
        private static string RocolaOsSpotifyApiDice(RocolaOsSpotifyVista v)
        {
            switch (v.Api)
            {
                case "bien": return "bien";
                case "frena": return "Spotify frena (429)" + (v.FrenaS != "0" && v.FrenaS.Length > 0 ? ", " + v.FrenaS : "");
                case "sin_red": return "sin red (se vuelve a probar sola)";
                case "falla": return "falla";
                case "sin_llave": return "sin su llave";
                default: return v.Api;
            }
        }
        private static string RocolaOsSpotifyCorta(string s, int max)
        {
            s = (s ?? "").Replace('\r', ' ').Replace('\n', ' ');
            return s.Length <= max ? s : s.Substring(0, Math.Max(1, max - 1)) + "…";
        }
        private static string RocolaOsSpotifyPeso(long bytes)
        {
            if (bytes < 1024) return bytes.ToString(CultureInfo.InvariantCulture) + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture) + " KB";
            return (bytes / 1048576.0).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
        }

        private RocolaOsPagina RocolaOsPaginaSpotify(RocolaOsVentanaCtx x)
        {
            Color cBien = Color.FromArgb(120, 230, 165), cMal = Color.FromArgb(255, 130, 120), cVerde = Color.FromArgb(29, 185, 84);
            Grupo pg = new Grupo(); pg.SetBounds(0, 96, 960, 580); pg.BackColor = x.CFondo;
            Tarjeta tSol = new Tarjeta(); tSol.SetBounds(16, 4, 456, 568); tSol.Titulo = "SOLOIST Y LA CUENTA"; tSol.Acento = cVerde;
            Tarjeta tPed = new Tarjeta(); tPed.SetBounds(488, 4, 456, 288); tPed.Titulo = "LO PEDIDO"; tPed.Acento = Color.FromArgb(150, 190, 255);
            Tarjeta tCac = new Tarjeta(); tCac.SetBounds(488, 300, 456, 140); tCac.Titulo = "LA CACHE"; tCac.Acento = Color.FromArgb(255, 196, 120);
            Tarjeta tPar = new Tarjeta(); tPar.SetBounds(488, 448, 456, 124); tPar.Titulo = "LA PARED"; tPar.Acento = Color.FromArgb(190, 160, 255);
            pg.Controls.Add(tSol); pg.Controls.Add(tPed); pg.Controls.Add(tCac); pg.Controls.Add(tPar);
            const int ancho = 424;

            // una fila «etiqueta: valor»: devuelve el rotulo del valor
            Func<Tarjeta, int, string, Rotulo> fila = delegate (Tarjeta t, int y, string etiqueta)
            {
                Rotulo l = new Rotulo(); l.SetBounds(16, y, 164, 20); l.BackColor = Color.Transparent; l.ForeColor = x.CTenue; l.Font = x.FDes; l.Text = etiqueta;
                Rotulo v = new Rotulo(); v.SetBounds(184, y - 1, ancho - 168, 22); v.BackColor = Color.Transparent; v.ForeColor = x.CTexto; v.Font = x.FSub;
                t.Controls.Add(l); t.Controls.Add(v);
                return v;
            };
            Func<Tarjeta, int, string, Rotulo> titulo = delegate (Tarjeta t, int y, string texto)
            {
                Rotulo l = new Rotulo(); l.SetBounds(16, y, ancho, 22); l.BackColor = Color.Transparent; l.ForeColor = x.CTexto; l.Font = x.FNom; l.Text = texto;
                t.Controls.Add(l); return l;
            };

            // ================= SOLOIST Y LA CUENTA =================
            titulo(tSol, 38, "El programa de Spotify (Soloist)");
            Rotulo vFase = fila(tSol, 66, "Vigilante");
            Rotulo vVer = fila(tSol, 90, "Version");
            Rotulo vDias = fila(tSol, 114, "Dias que le quedan");
            Rotulo vWs = fila(tSol, 138, "Conectado a la rocola");
            Rotulo vSes = fila(tSol, 162, "Emparejado (sesion)");
            Rotulo vBoc = fila(tSol, 186, "Bocina");
            Rotulo vRep = fila(tSol, 210, "Soloist esta");
            titulo(tSol, 252, "La cuenta de la API web");
            Rotulo vApi = fila(tSol, 280, "Estado");
            Rotulo vLla = fila(tSol, 304, "Llave y cuenta");
            Rotulo vCod = fila(tSol, 328, "Ultimo codigo");
            Rotulo vPet = fila(tSol, 352, "Peticiones, ultima hora");
            Rotulo vOk = fila(tSol, 376, "Ultimo exito (UTC)");
            Rotulo v429 = fila(tSol, 400, "Ultimo 429 (UTC)");
            Rotulo lNota = new Rotulo(); lNota.SetBounds(16, 436, ancho, 120); lNota.BackColor = Color.Transparent; lNota.ForeColor = x.CTenue; lNota.Font = x.FDes;
            lNota.Text = "Soloist es el programa oficial de Spotify que toca la musica; el vigilante lo baja y lo cambia solo cuando le quedan menos de 21 dias "
                + "(cada version caduca a los 90) y nunca con una pedida de Spotify sonando. La API web busca y abre discos; con «Spotify frena (429)» "
                + "espera sola. Aqui no se ve ninguna llave.";
            tSol.Controls.Add(lNota);

            // ================= LO PEDIDO =================
            Rotulo vCont = new Rotulo(); vCont.SetBounds(16, 38, ancho, 20); vCont.BackColor = Color.Transparent; vCont.ForeColor = x.CTexto; vCont.Font = x.FSub;
            tPed.Controls.Add(vCont);
            Rotulo lUlt = new Rotulo(); lUlt.SetBounds(16, 62, ancho, 18); lUlt.BackColor = Color.Transparent; lUlt.ForeColor = x.CTenue; lUlt.Font = x.FDes;
            lUlt.Text = "Las ultimas canciones de Spotify (la mas nueva arriba):"; tPed.Controls.Add(lUlt);
            List<Rotulo> filas = new List<Rotulo>();
            for (int i = 0; i < SpotifyUltimasMax; i++)
            {
                Rotulo r = new Rotulo(); r.SetBounds(16, 84 + i * 28, ancho, 28); r.BackColor = Color.Transparent; r.ForeColor = x.CTexto; r.Font = x.FDes;
                tPed.Controls.Add(r); filas.Add(r);
            }

            // ================= LA CACHE =================
            Rotulo vCac1 = new Rotulo(); vCac1.SetBounds(16, 38, ancho, 20); vCac1.BackColor = Color.Transparent; vCac1.ForeColor = x.CTexto; vCac1.Font = x.FSub;
            Rotulo vCac2 = new Rotulo(); vCac2.SetBounds(16, 62, ancho, 20); vCac2.BackColor = Color.Transparent; vCac2.ForeColor = x.CTexto; vCac2.Font = x.FSub;
            Rotulo vCac3 = new Rotulo(); vCac3.SetBounds(16, 112, ancho, 20); vCac3.BackColor = Color.Transparent; vCac3.ForeColor = x.CTenue; vCac3.Font = x.FDes;
            BotonPlano bVacia = new BotonPlano(); bVacia.SetBounds(16, 84, 190, 26); bVacia.Font = x.FBot; bVacia.Text = "Vaciar la cache";
            Rotulo vEstado = new Rotulo(); vEstado.SetBounds(214, 88, ancho - 198, 22); vEstado.BackColor = Color.Transparent; vEstado.ForeColor = cBien; vEstado.Font = x.FDes;
            tCac.Controls.Add(vCac1); tCac.Controls.Add(vCac2); tCac.Controls.Add(vCac3); tCac.Controls.Add(bVacia); tCac.Controls.Add(vEstado);

            // ================= LA PARED: paginas por letra (pared_paginas) =================
            bool hayClave = RocolaOs.ClaveDe(SpotifyClavePaginas) != null;
            Rotulo nomPar = new Rotulo(); nomPar.SetBounds(16, 38, ancho - 120, 18); nomPar.BackColor = Color.Transparent; nomPar.ForeColor = x.CTexto; nomPar.Font = x.FNom; nomPar.Text = "Páginas por letra en la pared";
            Rotulo valPar = new Rotulo(); valPar.SetBounds(16 + ancho - 160, 36, 160, 20); valPar.BackColor = Color.Transparent; valPar.ForeColor = cVerde; valPar.Font = x.FVal; valPar.TextAlign = AlineaTexto.TopRight;
            Deslizador barPar = new Deslizador(); barPar.SetBounds(16, 58, ancho, 22); barPar.Minimum = 0; barPar.Maximum = SpotifyPasosPaginas.Length - 1; barPar.Acento = Color.FromArgb(190, 160, 255);
            Rotulo infoPar = new Rotulo(); infoPar.SetBounds(16, 84, ancho, 36); infoPar.BackColor = Color.Transparent; infoPar.ForeColor = x.CTenue; infoPar.Font = x.FDes;
            tPar.Controls.Add(nomPar); tPar.Controls.Add(valPar); tPar.Controls.Add(barPar); tPar.Controls.Add(infoPar);
            int pasoPar = SpotifyPasoDe(hayClave ? RocolaOs.AjusteEntero(SpotifyClavePaginas) : 100);
            bool parSucio = false;
            Action pintaPar = delegate
            {
                int n = SpotifyPasosPaginas[pasoPar];
                valPar.Text = n + (n == 100 ? " (de fábrica)" : "");
                infoPar.Text = hayClave
                    ? "Las portadas ocuparían como mucho " + RocolaOsSpotifyPesoPaginas(n) + ": el paquete de portadas trae unas 60 000. Vale en un minuto o al reabrir la letra."
                    : "Este programa no declara «" + SpotifyClavePaginas + "» (se declara con la pared por género): aquí no se puede cambiar.";
            };
            barPar.Value = pasoPar; barPar.Enabled = hayClave;
            barPar.Scroll += delegate { int k = Math.Max(0, Math.Min(SpotifyPasosPaginas.Length - 1, barPar.Value)); if (k != pasoPar) { pasoPar = k; parSucio = true; pintaPar(); } };
            Action guardaPar = delegate
            {
                if (!parSucio) return;
                parSucio = false;
                int n = SpotifyPasosPaginas[pasoPar];
                if (RocolaOs.PonAjuste(SpotifyClavePaginas, n.ToString(CultureInfo.InvariantCulture))) RocolaOs.Apunta("ventana", "paginas por letra de la pared: " + n + " (" + SpotifyClavePaginas + "=)");
            };
            pintaPar();

            string aviso = "";
            Action pinta = delegate
            {
                RocolaOsSpotifyVista v = RocolaOsSpotifyArmaVista();
                Action<Rotulo, string, Color> pon = delegate (Rotulo r, string t, Color c) { if (r.Text != t) r.Text = t; if (r.ForeColor != c) r.ForeColor = c; };
                bool mal = v.Fase == "caducado" || v.Fase == "sin_programa";
                pon(vFase, v.Encendido ? RocolaOsSpotifyFase(v.Fase) : "el modo Spotify esta apagado (spotify=0)", !v.Encendido ? x.CApagado : mal ? cMal : v.Fase == "corriendo" ? cBien : x.CAviso);
                pon(vVer, v.Version.Length > 0 ? RocolaOsSpotifyCorta(v.Version, 34) + (v.Caduca.Length > 0 ? " · caduca el " + v.Caduca : "") : "-", x.CTexto);
                int dias; bool hayDias = int.TryParse(v.Dias, NumberStyles.Integer, CultureInfo.InvariantCulture, out dias);
                pon(vDias, !hayDias ? "-" : dias < 0 ? "ya caduco" : dias + (dias == 1 ? " dia" : " dias") + (dias < 21 ? " (se busca uno nuevo)" : ""), !hayDias ? x.CApagado : dias <= 7 ? cMal : dias < 21 ? x.CAviso : cBien);
                pon(vWs, v.Encendido ? (v.Ws ? "si" : "no") : "-", !v.Encendido ? x.CApagado : v.Ws ? cBien : cMal);
                pon(vSes, v.Encendido && v.Ws ? (v.Sesion ? "si" : "no: falta emparejarlo desde la app de Spotify") : "-", !v.Encendido || !v.Ws ? x.CApagado : v.Sesion ? cBien : x.CAviso);
                pon(vBoc, v.Dispositivo.Length > 0 ? RocolaOsSpotifyCorta(v.Dispositivo, 30) + (v.Activa ? " (activa)" : "") : "-", x.CTexto);
                pon(vRep, v.Reproduce.Length > 0 ? (v.Reproduce == "playing" ? "tocando" : v.Reproduce == "paused" ? "en pausa" : v.Reproduce == "buffering" ? "cargando" : v.Reproduce == "idle" ? "sin tocar" : v.Reproduce) : "-", x.CTexto);
                pon(vApi, RocolaOsSpotifyApiDice(v), v.Api == "bien" ? cBien : v.Api == "sin_llave" ? x.CApagado : v.Api == "-" ? x.CApagado : x.CAviso);
                pon(vLla, (v.Llave ? "llave de la app: si" : "llave de la app: no") + " · " + (v.Cuenta ? "tu cuenta: si" : "tu cuenta: no"), v.Llave ? x.CTexto : x.CApagado);
                pon(vCod, v.UltimoCodigo == "0" ? "-" : v.UltimoCodigo, x.CTexto);
                pon(vPet, v.Peticiones60, x.CTexto);
                pon(vOk, v.UltimoExito, x.CTexto);
                pon(v429, v.Ultimo429, v.Ultimo429 == "-" ? x.CTexto : x.CAviso);
                pon(vCont, "Cobradas " + v.Cobradas + " · devueltas " + v.Devueltas + " · no pedidas " + v.NoPedidas + " · en cola " + v.EnCola, x.CTexto);
                for (int i = 0; i < filas.Count; i++)
                {
                    if (i >= v.Ultimas.Count) { pon(filas[i], i == 0 ? "Todavia no ha sonado ninguna de Spotify." : "", x.CApagado); continue; }
                    string[] u = v.Ultimas[i];
                    bool malo = u[1] != "acabo";
                    string que = u[1] == "acabo" ? "acabo" : u[1] == "DEVUELTA" ? "DEVUELTA" : "NO PEDIDO";
                    pon(filas[i], u[0] + "  " + que + ": " + RocolaOsSpotifyCorta(u[2], 46) + "\n" + RocolaOsSpotifyCorta(u[3], 80), malo ? x.CAviso : x.CTexto);
                }
                pon(vCac1, v.CacheDisco ? v.CacheArchivos + " archivos, " + RocolaOsSpotifyPeso(v.CacheBytes) + " en el disco de la rocola (tope 48 MB)" : "sin disco aparte: no se guarda nada en disco", v.CacheDisco ? x.CTexto : x.CApagado);
                pon(vCac2, "Aciertos " + v.CacheAciertos + " · vencidas servidas " + v.CacheVencidas + " · guardadas " + v.CacheGuardadas, x.CTexto);
                pon(vCac3, "Busquedas 12 h, discos 30 dias, artistas 3 dias. Nunca llaves ni tu cuenta.", x.CTenue);
                bVacia.Enabled = v.CacheDisco && v.CacheArchivos > 0;
                pon(vEstado, aviso, cBien);
                bVacia.Invalidate();
            };
            bVacia.Click += delegate
            {
                int n = RocolaOsSpotifyMemoria.Vacia();
                aviso = n > 0 ? "Vaciada: " + n + " archivos. Se vuelve a pedir a Spotify cuando haga falta." : "No habia nada que borrar.";
                pinta();
            };
            pinta();

            RocolaOsPagina pag = new RocolaOsPagina();
            pag.Panel = pg; pag.Titulo = "Spotify";
            pag.CadaMedioSegundo = delegate { pinta(); guardaPar(); };
            pag.AlCerrar = delegate { guardaPar(); };
            return pag;
        }
    }
}
