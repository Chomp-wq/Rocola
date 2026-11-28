// ROCOLA OS (la cadena nueva del billar, B; CADENA_NUEVA.md §3, CADENA_NUEVA_B.md §5): LA HOJITA «BILLAR» DE CTRL+F4. Desde el 28-11-2026 (la
// cadena nueva es la unica) se ve SIEMPRE con ROCOLA_OS (antes, con cadena_nueva=1 o «billar_nuevo»). Dos tarjetas como las de Ctrl+F3 y las
// hojas de Ctrl+F4:
//   AFINAR CON EL CELULAR: la hoja de Ctrl+F3 (AfinarLocal.cs) para varios puntos del local: los botones 1 a 8 eligen el punto que se anota, «Tocar
//     las 8 bandas» (en MONO, como suena la B: RocolaOs.Billar2Bandas.cs), la banda que suena en grande, una barra por banda (lo que marca la app),
//     «Tu app dice: dBA / dBC», «Borrar este punto» y «Traer la de Ctrl+F3» (la de un punto de la cadena de hoy, al punto elegido). Con uno solo
//     basta (lo que el dueno pidio el 27-10-2026: «lo mas simple posible»); con mas, la rocola los promedia por su forma.
//   LA CURVA DEL BILLAR: el dibujo (lo que hace la B -paso alto y curva-, la curva del dueno de la memoria del billar y la suma: CurvaEqView de
//     XP), lo que la afinacion corrige banda por banda (y por que una no sube), y los mandos: el paso alto, la inclinacion de agudos, la claridad
//     de la voz (las dos campanas), usar la afinacion, el bajo virtual y su mezcla.
// Cada cambio se guarda al momento en rocola_os.txt (RocolaOs.PonAjuste) y vale DESDE LA SIGUIENTE CANCION (que se mide otra vez: la huella
// cambia); el diseno nuevo se hace de fondo (RocolaOsBillar2.Precalienta). Nada de esto toca la cancion que suena.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using Rocola.Nativo;

namespace Rocola
{
    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraHojitaBillar2()
        {
            // (Orden 18: despues de «Cadena» (17) y antes de «Archivos» (30); solo sale con la cadena nueva elegida: las demas no se mueven)
            RocolaOsHojitas.Registra(new RocolaOsHojita { Nombre = "Billar", Orden = 18, Arma = (f, x) => f.RocolaOsPaginaBillar2(x) });
        }
        internal const string RocolaOsClaveCadenaNueva = "cadena_nueva";   // (la del marco: RocolaOs.CadenaNueva.cs)
        // (28-11-2026: la cadena nueva es la unica, CADENA_NUEVA.md «La cadena vieja borrada») siempre a la vista con ROCOLA_OS
        internal static bool RocolaOsBillar2HojaVisible { get { return RocolaOs.Activo; } }

        private static int rocolaOsBillar2Punto;   // el punto que se anota (se recuerda mientras la rocola siga abierta)

        private RocolaOsPagina RocolaOsPaginaBillar2(RocolaOsVentanaCtx x)
        {
            if (!RocolaOsBillar2HojaVisible) return null;
            Color cAfi = Color.FromArgb(120, 200, 255), cCur = Color.FromArgb(150, 230, 180), cBajo = Color.FromArgb(255, 196, 120), cBien = Color.FromArgb(120, 230, 165);
            Grupo pg = new Grupo(); pg.SetBounds(0, 96, 960, 580); pg.BackColor = x.CFondo;
            Tarjeta tAfi = new Tarjeta(); tAfi.SetBounds(16, 4, 456, 568); tAfi.Titulo = "AFINAR CON EL CELULAR"; tAfi.Acento = cAfi;
            Tarjeta tCur = new Tarjeta(); tCur.SetBounds(488, 4, 456, 568); tCur.Titulo = "LA CURVA DEL BILLAR"; tCur.Acento = cCur;
            pg.Controls.Add(tAfi); pg.Controls.Add(tCur);
            const int ancho = 424;
            bool poniendo = false;
            string aviso = "";
            Func<Tarjeta, int, int, Color, Letra, Rotulo> rot = delegate (Tarjeta t, int y, int alto, Color c, Letra f)
            {
                Rotulo r = new Rotulo(); r.SetBounds(16, y, ancho, alto); r.BackColor = Color.Transparent; r.ForeColor = c; r.Font = f; r.UseMnemonic = false;
                t.Controls.Add(r); return r;
            };
            Action guardado = delegate { RocolaOsBillar2.Precalienta(44100); };

            // ================= AFINAR CON EL CELULAR =================
            RocolaOsBillar2Afinacion af = RocolaOsBillar2.AfinacionDeAjustes();
            if (rocolaOsBillar2Punto < 0 || rocolaOsBillar2Punto >= RocolaOsBillar2Afinacion.Puntos) rocolaOsBillar2Punto = 0;
            BotonPlano[] bPunto = new BotonPlano[RocolaOsBillar2Afinacion.Puntos];
            for (int i = 0; i < bPunto.Length; i++)
            {
                BotonPlano b = new BotonPlano(); b.SetBounds(16 + i * 53, 38, 48, 30); b.Font = x.FBot; b.Text = (i + 1).ToString(CultureInfo.InvariantCulture);
                b.Name = "Billar2Punto" + (i + 1);
                int k = i; b.Click += delegate { rocolaOsBillar2Punto = k; };
                tAfi.Controls.Add(b); bPunto[i] = b;
            }
            BotonPlano bTocar = new BotonPlano(); bTocar.SetBounds(16, 76, ancho, 32); bTocar.Font = x.FBot; bTocar.Text = "Tocar las 8 bandas (de grave a agudo, en mono)"; bTocar.Name = "Billar2Tocar";
            tAfi.Controls.Add(bTocar);
            Rotulo lGrande = rot(tAfi, 112, 30, Color.FromArgb(255, 214, 120), x.FTit); lGrande.Name = "Billar2Grande";
            Rotulo[] nB = new Rotulo[8]; Deslizador[] barB = new Deslizador[8]; Rotulo[] vB = new Rotulo[8];
            for (int k = 0; k < 8; k++)
            {
                int y = 146 + 31 * k;
                nB[k] = new Rotulo(); nB[k].SetBounds(16, y + 4, 70, 20); nB[k].BackColor = Color.Transparent; nB[k].ForeColor = x.CTexto; nB[k].Font = x.FNom; nB[k].Text = NombreBanda(k);
                tAfi.Controls.Add(nB[k]);
                barB[k] = new Deslizador(); barB[k].SetBounds(90, y + 3, 250, 22); barB[k].Name = "Billar2Db" + k;
                barB[k].Minimum = AfinaSinMedir; barB[k].Maximum = AfinaMaxDb; barB[k].Acento = cAfi;
                tAfi.Controls.Add(barB[k]);
                vB[k] = new Rotulo(); vB[k].SetBounds(344, y + 4, 96, 20); vB[k].BackColor = Color.Transparent; vB[k].ForeColor = cAfi; vB[k].Font = x.FNom; vB[k].TextAlign = AlineaTexto.TopRight;
                tAfi.Controls.Add(vB[k]);
            }
            Rotulo lPond = new Rotulo(); lPond.SetBounds(16, 404, 140, 20); lPond.BackColor = Color.Transparent; lPond.ForeColor = x.CTexto; lPond.Font = x.FNom; lPond.Text = "Tu app dice:";
            tAfi.Controls.Add(lPond);
            BotonPlano[] bPond = new BotonPlano[2];
            for (int k = 0; k < 2; k++)
            {
                bPond[k] = new BotonPlano(); bPond[k].SetBounds(160 + 94 * k, 398, 86, 30); bPond[k].Font = x.FBot; bPond[k].Text = k == 0 ? "dBA" : "dBC";
                bPond[k].Name = "Billar2Pond" + (k == 0 ? "A" : "C"); tAfi.Controls.Add(bPond[k]);
            }
            BotonPlano bBorra = new BotonPlano(); bBorra.SetBounds(16, 436, 208, 30); bBorra.Font = x.FBot; bBorra.Text = "Borrar este punto"; bBorra.Name = "Billar2Borra";
            BotonPlano bTrae = new BotonPlano(); bTrae.SetBounds(232, 436, 208, 30); bTrae.Font = x.FBot; bTrae.Text = "Traer la de Ctrl+F3"; bTrae.Name = "Billar2Trae";
            tAfi.Controls.Add(bBorra); tAfi.Controls.Add(bTrae);
            Rotulo lPuntos = rot(tAfi, 472, 32, x.CTexto, x.FDes); lPuntos.Name = "Billar2Puntos";
            rot(tAfi, 506, 56, x.CTenue, x.FDes).Text = "Rocola callada, app en lento: toca las bandas y anota cada una donde está la gente. Con un punto basta; "
                + "con más (hasta 8) se promedia la forma de cada uno. Corrige el 70 % (80 % con 3 o más): baja hasta 6 dB, sube hasta 3 (63 Hz nunca, ni un hueco).";

            // ================= LA CURVA DEL BILLAR =================
            CurvaEqView vista = new CurvaEqView(); vista.SetBounds(16, 36, ancho, 150); vista.Font = x.FDes; vista.Acento = cCur; vista.Name = "Billar2Curva";
            tCur.Controls.Add(vista);
            Rotulo lCorr = rot(tCur, 190, 34, x.CTexto, x.FDes); lCorr.Name = "Billar2Correccion";
            // un mando: nombre, barra y valor
            Func<int, string, int, int, Color, Tuple<Deslizador, Rotulo>> fila = delegate (int y, string nombre, int min, int max, Color ac)
            {
                Rotulo n = new Rotulo(); n.SetBounds(16, y + 4, 156, 20); n.BackColor = Color.Transparent; n.ForeColor = x.CTexto; n.Font = x.FNom; n.Text = nombre; tCur.Controls.Add(n);
                Deslizador d = new Deslizador(); d.SetBounds(172, y + 3, 176, 22); d.Minimum = min; d.Maximum = max; d.Acento = ac; tCur.Controls.Add(d);
                Rotulo v = new Rotulo(); v.SetBounds(350, y + 4, 90, 20); v.BackColor = Color.Transparent; v.ForeColor = ac; v.Font = x.FNom; v.TextAlign = AlineaTexto.TopRight; tCur.Controls.Add(v);
                return Tuple.Create(d, v);
            };
            var fPaso = fila(230, "Paso alto", (int)(RocolaOsBillar2Curva.PasoAltoMin / 5), (int)(RocolaOsBillar2Curva.PasoAltoMax / 5), cCur); fPaso.Item1.Name = "Billar2PasoAlto";
            var fIncl = fila(262, "Agudos (por oct.)", 0, 6, cCur); fIncl.Item1.Name = "Billar2Inclinacion";
            var fMenos = fila(294, "Claridad 250-400", 0, (int)(RocolaOsBillar2Curva.ClaridadMenosMax * 2), cCur); fMenos.Item1.Name = "Billar2ClaridadGraves";
            var fMas = fila(326, "Claridad 2-4 kHz", 0, (int)(RocolaOsBillar2Curva.ClaridadMasMax * 2), cCur); fMas.Item1.Name = "Billar2ClaridadAgudos";
            Interruptor swAfi = new Interruptor(); swAfi.SetBounds(16, 362, ancho, 26); swAfi.Font = x.FSub; swAfi.ForeColor = x.CTexto; swAfi.Acento = cAfi;
            swAfi.Text = "Usar la afinación del celular"; swAfi.Name = "Billar2UsarAfinacion"; tCur.Controls.Add(swAfi);
            Interruptor swBajo = new Interruptor(); swBajo.SetBounds(16, 394, ancho, 26); swBajo.Font = x.FSub; swBajo.ForeColor = x.CTexto; swBajo.Acento = cBajo;
            swBajo.Text = "Bajo virtual (decide la prueba a ciegas)"; swBajo.Name = "Billar2BajoVirtual"; tCur.Controls.Add(swBajo);
            var fBajo = fila(426, "Mezcla del bajo", (int)RocolaOsBillar2Bajo.MezclaMinDb, (int)RocolaOsBillar2Bajo.MezclaMaxDb, cBajo); fBajo.Item1.Name = "Billar2BajoDb";
            Rotulo lNota = rot(tCur, 460, 100, x.CTenue, x.FDes); lNota.Name = "Billar2Nota";

            // ---------- lo guardado a los mandos ----------
            Func<double[]> lecturasDelPunto = () => af.Lecturas[rocolaOsBillar2Punto];
            Action ponMandos = delegate
            {
                poniendo = true;
                try
                {
                    RocolaOsBillar2Params p = RocolaOsBillar2.ParamsDeAjustes();
                    fPaso.Item1.Value = (int)Math.Round(p.PasoAltoHz / 5.0);
                    fIncl.Item1.Value = (int)Math.Round(-p.Inclinacion / 0.25);
                    fMenos.Item1.Value = (int)Math.Round(p.ClaridadMenos * 2);
                    fMas.Item1.Value = (int)Math.Round(p.ClaridadMas * 2);
                    fBajo.Item1.Value = (int)Math.Round(p.BajoVirtualDb);
                    swAfi.Checked = RocolaOs.AjusteSiNo(RocolaOsBillar2.KAfinacion);
                    swBajo.Checked = RocolaOs.AjusteSiNo(RocolaOsBillar2.KBajo);
                    double[] l = lecturasDelPunto();
                    for (int k = 0; k < 8; k++) barB[k].Value = double.IsNaN(l[k]) ? AfinaSinMedir : (int)Math.Round(l[k]);
                }
                finally { poniendo = false; }
            };
            // ---------- lo que se ve ----------
            Action pinta = delegate
            {
                RocolaOsBillar2Params p = RocolaOsBillar2.ParamsDeAjustes();
                fPaso.Item2.Text = RocolaOsBillar2.Decimal(p.PasoAltoHz) + " Hz";
                fIncl.Item2.Text = (p.Inclinacion == 0 ? "0" : p.Inclinacion.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',')) + " dB";
                fMenos.Item2.Text = (p.ClaridadMenos == 0 ? "0" : "-" + p.ClaridadMenos.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',')) + " dB";
                fMas.Item2.Text = (p.ClaridadMas == 0 ? "0" : "+" + p.ClaridadMas.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',')) + " dB";
                fBajo.Item2.Text = RocolaOsBillar2.Decimal(p.BajoVirtualDb) + " dB";
                fBajo.Item1.Enabled = p.BajoVirtual;
                for (int i = 0; i < bPunto.Length; i++)
                {
                    bool act = i == rocolaOsBillar2Punto, vale = af.Vale(i), tiene = af.Tiene(i);
                    bPunto[i].Relleno = act ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                    bPunto[i].Borde = act ? Color.FromArgb(150, 205, 255) : vale ? cBien : Color.FromArgb(60, 72, 118);
                    bPunto[i].ForeColor = vale ? cBien : tiene ? x.CAviso : (act ? Color.FromArgb(226, 240, 255) : x.CApagado);
                    bPunto[i].Invalidate();
                }
                double[] l = lecturasDelPunto();
                for (int k = 0; k < 8; k++) vB[k].Text = double.IsNaN(l[k]) ? "sin anotar" : l[k].ToString("0", CultureInfo.InvariantCulture) + " dB";
                for (int k = 0; k < 2; k++)
                {
                    bool act = k == 0 ? af.Pond == 0 : af.Pond != 0;
                    bPond[k].Relleno = act ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                    bPond[k].Borde = act ? Color.FromArgb(150, 205, 255) : Color.FromArgb(60, 72, 118);
                    bPond[k].ForeColor = act ? Color.FromArgb(226, 240, 255) : x.CApagado;
                    bPond[k].Invalidate();
                }
                int validos = af.Validos;
                lPuntos.Text = "Punto " + (rocolaOsBillar2Punto + 1) + (af.Vale(rocolaOsBillar2Punto) ? ": vale." : af.Tiene(rocolaOsBillar2Punto) ? ": le faltan bandas de la voz (250 Hz a 2 kHz)." : ": vacío.")
                    + "\nPuntos que valen: " + validos + (validos > 0 ? " (corrige el " + (af.Fuerza * 100).ToString("0", CultureInfo.InvariantCulture) + " %)." : ".");
                // lo que corrige la afinacion, banda por banda
                string[] mot; double[] corr = af.Correccion(out mot);
                if (corr == null) lCorr.Text = "Sin afinación medida: la curva es el objetivo y la claridad (lo que se supone de unas bocinas planas).";
                else
                {
                    string t = (swAfi.Checked ? "Corrige" : "Corregiría (apagado)") + ":";
                    string por = "";
                    for (int b = 0; b < 8; b++)
                    {
                        t += " " + NombreBanda(b).Replace(" Hz", "").Replace(" kHz", "k") + " " + (mot[b] == "sin medir" ? "—" : corr[b].ToString("+0.0;-0.0;0", CultureInfo.InvariantCulture).Replace('.', ','));
                        if (mot[b] != null && mot[b] != "sin medir" && por.Length == 0) por = NombreBanda(b) + ": " + mot[b];
                    }
                    lCorr.Text = t + (por.Length > 0 ? "\n" + por + "." : "");
                }
                // el dibujo: lo que hace la B (paso alto y curva), la curva del dueno (memoria del billar) y la suma
                double[] hz = EqCurvaHz, fijo = new double[hz.Length], suma = new double[hz.Length];
                double[] dueno = null;
                try { dueno = EqDuenoRespuesta(true, hz); } catch { }
                for (int i = 0; i < hz.Length; i++)
                {
                    fijo[i] = RocolaOsBillar2Curva.TotalDb(hz[i], p, false);
                    suma[i] = fijo[i] + (dueno != null ? dueno[i] : 0.0);
                }
                vista.Pon(fijo, dueno, suma);
                bool b2 = RocolaOs.AjusteSiNo(RocolaOsClaveCadenaNueva);
                lNota.Text = "Vale desde la siguiente canción (se vuelve a medir). El dibujo: la B (paso alto de 24 dB/oct y curva, en fase lineal), tu curva "
                    + "del billar (Ctrl+F3 → Ecualizador) y la suma. El bajo virtual hace armónicos del grave que las bocinas no dan, sin seguir a la música. "
                    + (b2 ? (LocalAutoAGuardar ? "Suena la B (cadena nueva y modo billar)." : "La cadena nueva está elegida; sin el modo billar suena la A.")
                          : "Ahora suena la cadena de hoy: la B suena con la cadena nueva (hoja «Cadena») y el modo billar.");
                string g = RocolaOsBillar2BandaGrande();
                lGrande.Font = g.Length > 0 || aviso.Length == 0 ? x.FTit : x.FDes;   // (un aviso largo, en chico)
                lGrande.Text = g.Length > 0 ? "Suena: " + g : aviso;
            };
            // ---------- los cambios ----------
            Action<string, string> pon = delegate (string clave, string valor)
            {
                if (poniendo) return;
                if (RocolaOs.PonAjuste(clave, valor)) { RocolaOs.Apunta("ventana", "Billar: " + clave + "=" + valor); guardado(); }
                pinta();
            };
            fPaso.Item1.Scroll += delegate { pon(RocolaOsBillar2.KPasoAlto, (fPaso.Item1.Value * 5).ToString(CultureInfo.InvariantCulture)); };
            fIncl.Item1.Scroll += delegate { pon(RocolaOsBillar2.KInclinacion, RocolaOsBillar2.Decimal(-0.25 * fIncl.Item1.Value)); };
            fMenos.Item1.Scroll += delegate { pon(RocolaOsBillar2.KClaridadGraves, RocolaOsBillar2.Decimal(0.5 * fMenos.Item1.Value)); };
            fMas.Item1.Scroll += delegate { pon(RocolaOsBillar2.KClaridadAgudos, RocolaOsBillar2.Decimal(0.5 * fMas.Item1.Value)); };
            fBajo.Item1.Scroll += delegate { pon(RocolaOsBillar2.KBajoDb, fBajo.Item1.Value.ToString(CultureInfo.InvariantCulture)); };
            swAfi.CheckedChanged += delegate { pon(RocolaOsBillar2.KAfinacion, swAfi.Checked ? "1" : "0"); };
            swBajo.CheckedChanged += delegate { pon(RocolaOsBillar2.KBajo, swBajo.Checked ? "1" : "0"); };
            Action guardaPunto = delegate
            {
                int k = rocolaOsBillar2Punto;
                string clave = RocolaOsBillar2.KPunto + (k + 1).ToString(CultureInfo.InvariantCulture);
                string t = af.Tiene(k) ? RocolaOsBillar2Afinacion.Texto(af.Lecturas[k]) : "";
                if (RocolaOs.PonAjuste(clave, t)) { RocolaOs.Apunta("ventana", "Billar: " + clave + "=" + t); guardado(); }
            };
            for (int k = 0; k < 8; k++)
            {
                int kk = k;
                barB[k].Scroll += delegate
                {
                    if (poniendo) return;
                    af.Lecturas[rocolaOsBillar2Punto][kk] = barB[kk].Value <= AfinaSinMedir ? double.NaN : barB[kk].Value;
                    guardaPunto(); pinta();
                };
            }
            for (int i = 0; i < bPunto.Length; i++) bPunto[i].Click += delegate { aviso = ""; ponMandos(); pinta(); };
            for (int k = 0; k < 2; k++)
            {
                int kk = k;
                bPond[k].Click += delegate { af.Pond = kk; pon(RocolaOsBillar2.KPond, kk == 0 ? "A" : "C"); };
            }
            bBorra.Click += delegate
            {
                for (int b = 0; b < 8; b++) af.Lecturas[rocolaOsBillar2Punto][b] = double.NaN;
                guardaPunto(); ponMandos(); pinta();
            };
            bTrae.Click += delegate
            {
                float[] de = AfinadoPromedio();
                bool hay = false;
                for (int b = 0; b < 8; b++) { af.Lecturas[rocolaOsBillar2Punto][b] = float.IsNaN(de[b]) ? double.NaN : de[b]; if (!float.IsNaN(de[b])) hay = true; }
                if (hay && afinadoPonderacion == 0 && af.Pond != 0) { af.Pond = 0; RocolaOs.PonAjuste(RocolaOsBillar2.KPond, "A"); }   // (la de Ctrl+F3 en dBA)
                aviso = hay ? "" : "Ctrl+F3 no tiene nada anotado.";
                guardaPunto(); ponMandos(); pinta();
            };
            bTocar.Click += delegate { aviso = RocolaOsBillar2ProbarBandas(); pinta(); };
            ponMandos();
            pinta();
            RocolaOsBillar2.Precalienta(44100);

            RocolaOsPagina pag = new RocolaOsPagina();
            pag.Panel = pg; pag.Titulo = "Billar";
            string antes = null;
            pag.CadaMedioSegundo = delegate
            {
                string g = RocolaOsBillar2BandaGrande();
                if (g != antes) { antes = g; lGrande.Font = g.Length > 0 || aviso.Length == 0 ? x.FTit : x.FDes; lGrande.Text = g.Length > 0 ? "Suena: " + g : aviso; }
            };
            pag.AlCerrar = delegate { RocolaOsBillar2ParaBandas(); };
            return pag;
        }
    }
}
