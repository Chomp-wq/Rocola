// LA HOJITA «CADENA» DE LA VENTANA DE CTRL+F4 (la cadena de sonido nueva, CADENA_NUEVA.md; RocolaOs.CadenaNueva.cs): cual cadena suena y lo de
// la nueva. Dos tarjetas como las de Ctrl+F3 y las hojas Pantalla, Sonido y Spotify:
//   CUAL SUENA: «La de hoy» o «La nueva» (con el modo billar guardado la nueva es la B, si no la A): se guarda en rocola_os.txt (cadena_nueva=)
//     y vale DESDE LA SIGUIENTE CANCION, sin reiniciar nada. Debajo, lo que suena ahora: que cadena, su ganancia y de donde salio, lo medido,
//     los arreglos y como se abrio (coma flotante, sin huecos).
//   LA NUEVA: el nivel (nivel_lufs, T) y el techo del pico real (techo_dbtp, C), de uno en uno; «Agudos del MP3» (agudos_mp3) y «Tono parejo»
//     (tono_parejo), apagados de fabrica (decide la prueba ciega); como va la medida (cuantas medidas, la cola, el repaso de fondo) y la
//     dispersion de la sonoridad de las de Spotify medidas (si es muy chica, Soloist ya las entrega normalizadas).
// Se refresca cada medio segundo con la ventana abierta. Nada de esto toca la cancion que suena (la ganancia es fija toda la cancion).
using System;
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
        internal static void RocolaOsRegistraHojitaCadena()
        {
            // (Orden 17: entre Spotify y Archivos)
            RocolaOsHojitas.Registra(new RocolaOsHojita { Nombre = "Cadena", Orden = 17, Arma = (f, x) => f.RocolaOsPaginaCadena(x) });
        }

        private RocolaOsPagina RocolaOsPaginaCadena(RocolaOsVentanaCtx x)
        {
            Color cNueva = Color.FromArgb(150, 230, 180), cHoy = Color.FromArgb(150, 190, 255);
            Grupo pg = new Grupo(); pg.SetBounds(0, 96, 960, 580); pg.BackColor = x.CFondo;
            Tarjeta tCual = new Tarjeta(); tCual.SetBounds(16, 4, 456, 568); tCual.Titulo = "CUAL SUENA"; tCual.Acento = cHoy;
            Tarjeta tNueva = new Tarjeta(); tNueva.SetBounds(488, 4, 456, 568); tNueva.Titulo = "LA CADENA NUEVA"; tNueva.Acento = cNueva;
            pg.Controls.Add(tCual); pg.Controls.Add(tNueva);
            const int ancho = 424;
            Func<int, int, int, Rotulo> rot = delegate (int y, int alto, int tipo)
            {
                Rotulo r = new Rotulo(); r.SetBounds(16, y, ancho, alto); r.BackColor = Color.Transparent;
                r.ForeColor = tipo == 0 ? x.CTexto : tipo == 1 ? x.CTenue : x.CAviso; r.Font = tipo == 0 ? x.FNom : x.FDes;
                return r;
            };
            Func<int, BotonPlano> boton = delegate (int y)
            {
                BotonPlano b = new BotonPlano(); b.SetBounds(16, y, ancho, 36); b.Font = x.FBot; return b;
            };
            // ================= CUAL SUENA =================
            BotonPlano bHoy = boton(40), bNueva = boton(84);
            Rotulo lCual = rot(128, 72, 1);
            Rotulo lAhoraT = rot(210, 20, 0); lAhoraT.Text = "Lo que suena ahora";
            Rotulo lAhora = rot(234, 300, 1);
            tCual.Controls.Add(bHoy); tCual.Controls.Add(bNueva); tCual.Controls.Add(lCual); tCual.Controls.Add(lAhoraT); tCual.Controls.Add(lAhora);
            // ================= LA NUEVA =================
            Rotulo lNivel = rot(40, 20, 0);
            BotonPlano bNivMenos = new BotonPlano(); bNivMenos.SetBounds(16, 64, 206, 34); bNivMenos.Font = x.FBot; bNivMenos.Text = "Nivel −1";
            BotonPlano bNivMas = new BotonPlano(); bNivMas.SetBounds(234, 64, 206, 34); bNivMas.Font = x.FBot; bNivMas.Text = "Nivel +1";
            Rotulo lTecho = rot(106, 20, 0);
            BotonPlano bTecMenos = new BotonPlano(); bTecMenos.SetBounds(16, 130, 206, 34); bTecMenos.Font = x.FBot; bTecMenos.Text = "Techo −1";
            BotonPlano bTecMas = new BotonPlano(); bTecMas.SetBounds(234, 130, 206, 34); bTecMas.Font = x.FBot; bTecMas.Text = "Techo +1";
            BotonPlano bAgudos = boton(176), bTono = boton(218);
            Rotulo lMedT = rot(266, 20, 0); lMedT.Text = "La medida";
            Rotulo lMed = rot(290, 150, 1);
            Rotulo lAviso = rot(446, 110, 2);
            foreach (IPieza p in new IPieza[] { lNivel, bNivMenos, bNivMas, lTecho, bTecMenos, bTecMas, bAgudos, bTono, lMedT, lMed, lAviso }) tNueva.Controls.Add(p);

            string aviso = null;
            Action pinta = delegate
            {
                bool va = RocolaOs.Encendida(RocolaOsCadenasNuevas.Medida) || RocolaOs.Activo;
                bool nueva = RocolaOsCadenasNuevas.Suena;
                char cad = LocalAutoAGuardar ? 'B' : 'A';
                bHoy.Text = (nueva ? "" : "✓ ") + "La de hoy";
                bNueva.Text = (nueva ? "✓ " : "") + "La nueva (" + (cad == 'B' ? "B, la del billar: el modo billar esta guardado" : "A, la normal") + ")";
                bHoy.Relleno = !nueva ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                bNueva.Relleno = nueva ? Color.FromArgb(30, 96, 70) : Color.FromArgb(28, 34, 62);
                bHoy.Borde = !nueva ? cHoy : Color.FromArgb(60, 72, 118); bNueva.Borde = nueva ? cNueva : Color.FromArgb(60, 72, 118);
                bHoy.Enabled = va && !RocolaOs.ConLista; bNueva.Enabled = va && !RocolaOs.ConLista;
                lCual.Text = "Vale desde la siguiente cancion (la que suena acaba con la suya). La nueva: una sola ganancia fija por cancion (o por disco), "
                    + "sin limitador ni nada que mueva el volumen; el MP3 en coma flotante y sin huecos." + (cad == 'B' && RocolaOsCadenaB.Arma == null ? " (Las piezas del billar aun no estan: la B va de paso.)" : "");
                RocolaOsReceta r = RocolaOsRecetaSonando;
                PlayItem np = nowPlaying;
                if (r == null) lAhora.Text = np == null ? "No suena nada." : videoOn ? "Un video (no pasa por la cadena)." : "Suena con la cadena de hoy.";
                else
                {
                    lAhora.Text = "Cadena " + r.Cadena + ": ganancia " + r.GananciaDb.ToString("+0.0;−0.0;0.0", CultureInfo.InvariantCulture) + " dB, fija.\n"
                        + r.Como + "\n"
                        + (r.Medida != null ? r.Medida.Resumen() : "Sin medir: se mide mientras suena (la siguiente vez, con su medida).") + "\n"
                        + (r.CanalMudo >= 0 || r.Invertido || r.ContinuaL != 0f || r.ContinuaR != 0f || r.ZumbidoHz > 0
                            ? "Arreglos: " + (r.CanalMudo >= 0 ? "canal mudo copiado; " : "") + (r.Invertido ? "canal invertido volteado; " : "") + (r.ContinuaL != 0f || r.ContinuaR != 0f ? "continua quitada; " : "") + (r.ZumbidoHz > 0 ? "zumbido de " + r.ZumbidoHz + " Hz; " : "") + "\n"
                            : "Sin arreglos.\n")
                        + (r.Agudos ? "Agudos del MP3. " : "") + (r.TonoDb != null ? "Tono parejo. " : "") + "\n"
                        + (RocolaOsFuenteSonando ?? "");
                }
                lNivel.Text = "Nivel de cada cancion: " + RocolaOsCadenasNuevas.T.ToString("0", CultureInfo.InvariantCulture) + " LUFS (de fabrica −18)";
                lTecho.Text = "Techo del pico real: " + RocolaOsCadenasNuevas.C.ToString("0", CultureInfo.InvariantCulture) + " dBTP (de fabrica −1)";
                bool ag = RocolaOsCadenasNuevas.Agudos, to = RocolaOsCadenasNuevas.Tono;
                bAgudos.Text = "Agudos y huecos del MP3 (DSEE y R1): " + (ag ? "encendido" : "apagado");
                bTono.Text = "Tono parejo por cancion (±3 dB): " + (to ? "encendido" : "apagado");
                bAgudos.Relleno = ag ? Color.FromArgb(30, 96, 70) : Color.FromArgb(28, 34, 62);
                bTono.Relleno = to ? Color.FromArgb(30, 96, 70) : Color.FromArgb(28, 34, 62);
                int n; double media;
                double disp = RocolaOsDispersionSpotify(cad, out n, out media);
                lMed.Text = RocolaOsMedidaTabla.Cuantas.ToString(CultureInfo.InvariantCulture) + " medidas en rocola_medida_nueva.txt; esta sesion "
                    + Interlocked.Read(ref RocolaOsMedidaTrabajo.Hechas).ToString(CultureInfo.InvariantCulture) + " (y " + Interlocked.Read(ref RocolaOsMedidaTrabajo.EnVivo).ToString(CultureInfo.InvariantCulture) + " en vivo).\n"
                    + "En la cola: " + RocolaOsMedidaTrabajo.EnCola + ". El repaso de fondo: " + RocolaOsMedidaTrabajo.Pendientes + " por medir"
                    + (RocolaOsMedidaTrabajo.Trabaja ? "." : " (solo trabaja con la nueva puesta).") + "\n"
                    + "La ultima tardo " + RocolaOsMedidaTrabajo.UltimaMs.ToString("0", CultureInfo.InvariantCulture) + " ms. El descompresor: " + RocolaOsDescompresorFlotante.Motor + ".\n"
                    + "Spotify: " + (n >= 2 ? n + " medidas, " + media.ToString("0.0", CultureInfo.InvariantCulture) + " LUFS de media, dispersion " + disp.ToString("0.0", CultureInfo.InvariantCulture) + " LU"
                        + (disp < 1.0 ? " (muy chica: Soloist las entrega ya normalizadas)" : "") : n + " medidas (hacen falta 2 para la dispersion)") + ".";
                lAviso.Text = aviso ?? (RocolaOs.ConLista ? "Con ROCOLA_OS de lista (pruebas) la eleccion la manda la lista." : "");
                foreach (BotonPlano b in new BotonPlano[] { bHoy, bNueva, bNivMenos, bNivMas, bTecMenos, bTecMas, bAgudos, bTono }) b.Invalidate();
            };
            Action<string, string, string> pon = delegate (string clave, string valor, string dice)
            {
                aviso = RocolaOs.PonAjuste(clave, valor) ? dice + " (desde la siguiente cancion)." : "No se pudo guardar " + clave + ".";
                RocolaOsCadenasNuevas.Apunta("Ctrl+F4: " + clave + "=" + valor);
                pinta();
            };
            bHoy.Click += delegate { pon(RocolaOsCadenasNuevas.Nombre, "0", "Guardado: la de hoy"); };
            bNueva.Click += delegate { pon(RocolaOsCadenasNuevas.Nombre, "1", "Guardado: la nueva"); RocolaOsMedidaTrabajo.Despierta(); };
            bNivMenos.Click += delegate { int v = (int)RocolaOsCadenasNuevas.T - 1; if (v >= -30) pon(RocolaOsCadenasNuevas.ClaveNivel, v.ToString(CultureInfo.InvariantCulture), "Nivel " + v + " LUFS"); };
            bNivMas.Click += delegate { int v = (int)RocolaOsCadenasNuevas.T + 1; if (v <= -6) pon(RocolaOsCadenasNuevas.ClaveNivel, v.ToString(CultureInfo.InvariantCulture), "Nivel " + v + " LUFS"); };
            bTecMenos.Click += delegate { int v = (int)RocolaOsCadenasNuevas.C - 1; if (v >= -20) pon(RocolaOsCadenasNuevas.ClaveTecho, v.ToString(CultureInfo.InvariantCulture), "Techo " + v + " dBTP"); };
            bTecMas.Click += delegate { int v = (int)RocolaOsCadenasNuevas.C + 1; if (v <= 0) pon(RocolaOsCadenasNuevas.ClaveTecho, v.ToString(CultureInfo.InvariantCulture), "Techo " + v + " dBTP"); };
            bAgudos.Click += delegate { pon(RocolaOsCadenasNuevas.ClaveAgudos, RocolaOsCadenasNuevas.Agudos ? "0" : "1", "Agudos del MP3 " + (RocolaOsCadenasNuevas.Agudos ? "apagados" : "encendidos")); };
            bTono.Click += delegate { pon(RocolaOsCadenasNuevas.ClaveTono, RocolaOsCadenasNuevas.Tono ? "0" : "1", "Tono parejo " + (RocolaOsCadenasNuevas.Tono ? "apagado" : "encendido")); };
            pinta();
            RocolaOsPagina pag = new RocolaOsPagina();
            pag.Panel = pg; pag.Titulo = "Cadena de sonido";
            pag.CadaMedioSegundo = delegate { pinta(); };
            return pag;
        }
    }
}
