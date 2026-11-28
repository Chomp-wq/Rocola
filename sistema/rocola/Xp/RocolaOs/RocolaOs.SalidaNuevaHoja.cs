// ROCOLA OS (cadena de audio nueva; CADENA_NUEVA.md §4; CADENA_NUEVA_salida.md): LA SALIDA NUEVA EN LA HOJA «SONIDO» DE CTRL+F4.
// Debajo de «LA QUE SE USA» (RocolaOs.VentanaSalida.cs), un bloque «SALIDA NUEVA»: lo que se abrio (la tarjeta por su nombre, el formato y
// la frecuencia de verdad, si tiene control de volumen por hardware), el volumen del dueno en dB, si actuo la proteccion, si los videos van
// por la rocola y los avisos (la tarjeta guardada que no esta); «Encender la salida nueva (prueba de 15 s)» / «Apagar la salida nueva» (por
// la misma prueba de «¿Se oye bien?» que elegir una salida: si nadie contesta, vuelve sola a lo de antes) y «Más y medir el DAC...»,
// una hoja con todo eso en grande, el volumen a mano y LA MEDIDA DEL CONVERTIDOR (RocolaOs.SalidaNuevaMide.cs) con sus instrucciones.
using System;
using System.Drawing;
using System.Globalization;
using System.Text;
using Rocola.Nativo;

namespace Rocola
{
    public partial class Form1
    {
        // EL BLOQUE de la tarjeta «LA QUE SE USA» (desde y): devuelve lo que se repinta cada medio segundo
        private Action RocolaOsSalidaNuevaBloque(RocolaOsVentanaCtx x, Tarjeta t, int y, Action<string, bool> estado)
        {
            const int ancho = 424;
            Color cTit = Color.FromArgb(150, 190, 255);
            Rotulo lTit = new Rotulo(); lTit.SetBounds(16, y, ancho, 18); lTit.BackColor = Color.Transparent; lTit.ForeColor = cTit; lTit.Font = x.FNom;
            lTit.Text = "SALIDA NUEVA · 24 bits, 44,1 kHz, siempre abierta";
            Rotulo lDiag = new Rotulo(); lDiag.SetBounds(16, y + 20, ancho, 102); lDiag.BackColor = Color.Transparent; lDiag.ForeColor = x.CTenue; lDiag.Font = x.FDes;
            BotonPlano bNueva = new BotonPlano(); bNueva.SetBounds(16, y + 126, 206, 34); bNueva.Font = x.FBot;
            BotonPlano bMas = new BotonPlano(); bMas.SetBounds(234, y + 126, 206, 34); bMas.Font = x.FBot; bMas.Text = "Más y medir el DAC...";
            t.Controls.Add(lTit); t.Controls.Add(lDiag); t.Controls.Add(bNueva); t.Controls.Add(bMas);
            bool va = RocolaOs.Encendida(RocolaOs.NombreSalida), encendida = RocolaOs.Encendida(SalidaNueva.NombreFuncion);
            Action pinta = delegate
            {
                lDiag.Text = RocolaOsSalidaNuevaTexto(false);
                // (en ambar si algo no va: encendida y rendida o sin tarjeta, con un aviso o si actuo la proteccion)
                bool mal = encendida && (SalidaNueva.Rendida || !SalidaNueva.Abierta || SalidaNueva.Aviso.Length > 0 || SalidaNueva.ProteccionVeces > 0);
                lDiag.ForeColor = mal ? x.CAviso : x.CTenue;
                bNueva.Text = encendida ? "Apagar la salida nueva" : "Encender (prueba de 15 s)";
                bNueva.Relleno = encendida ? Color.FromArgb(40, 52, 98) : Color.FromArgb(30, 96, 70);
                bNueva.Borde = encendida ? Color.FromArgb(72, 90, 150) : Color.FromArgb(72, 182, 132);
                bNueva.Enabled = va;
                bMas.Enabled = va;
                bNueva.Invalidate(); bMas.Invalidate();
            };
            bNueva.Click += delegate
            {
                string err;
                if (!RocolaOs.EligeSalida(RocolaOs.PruebaSalidaNueva + (encendida ? "0" : "1"), out err)) { estado("No se pudo guardar: " + err, false); return; }
                estado(encendida ? "Se apaga la salida nueva: la rocola se vuelve a abrir..." : "Se enciende la salida nueva: la rocola se vuelve a abrir y pregunta «¿Se oye bien?»...", true);
                x.Dlg.Close();
                RocolaOs.ReabreConservando(encendida ? "se apaga la salida nueva (Ctrl+F4)" : "se enciende la salida nueva (Ctrl+F4)");
            };
            bMas.Click += delegate { try { RocolaOsSalidaNuevaHoja(x); } catch (Exception ex) { RocolaOs.Falla(SalidaNueva.NombreFuncion, "la hoja de la salida nueva", ex); } pinta(); };
            pinta();
            return pinta;
        }

        // LO QUE DICE (corto en el bloque; largo en la hoja)
        internal static string RocolaOsSalidaNuevaTexto(bool largo)
        {
            StringBuilder sb = new StringBuilder();
            if (!RocolaOs.Encendida(SalidaNueva.NombreFuncion))
            {
                sb.Append("Apagada (lo de siempre: la tarjeta por su número, plughw, 16 bits y a la frecuencia de cada canción). ");
                sb.Append("Encendida: la tarjeta por su nombre (la USB primero), siempre abierta, 24 bits y 44,1 kHz fijos, los botones de volumen en dB y los videos por la rocola.");
                return sb.ToString();
            }
            if (SalidaNueva.Rendida) { sb.Append("Encendida, pero NO ABRIÓ: ").Append(SalidaNueva.Motivo).Append(". Esta sesión suena por el camino de siempre."); return sb.ToString(); }
            if (!SalidaNueva.Abierta) sb.Append("Sin tarjeta ahora: ").Append(SalidaNueva.Motivo.Length > 0 ? SalidaNueva.Motivo : "abriendo").Append(" (se vuelve a buscar cada segundo).\n");
            else
            {
                sb.Append("Tarjeta: ").Append(SalidaNueva.TarjetaNombre.Length > 0 ? SalidaNueva.TarjetaNombre : SalidaNueva.PcmNombre).Append(SalidaNueva.TarjetaUsb ? " (USB)" : "")
                  .Append(" · ").Append(SalidaNueva.PcmNombre).Append('\n');
                sb.Append("Abierta: ").Append(SalidaNueva.TextoFormato()).Append(" · búfer ").Append(SalidaNueva.RetrasoTarjetaMs).Append(" ms");
                if (largo) sb.Append(" · periodo ").Append(SalidaNueva.PeriodoAbierto * 1000 / SalidaNueva.Tasa).Append(" ms");
                sb.Append('\n');
            }
            sb.Append("Volumen por hardware: ").Append(SalidaNueva.ControlHw.Length > 0 ? SalidaNueva.ControlHw : "—").Append('\n');
            sb.Append("Volumen del dueño: ").Append(SalidaNueva.VolumenDb.ToString(CultureInfo.InvariantCulture)).Append(" dB (digital)").Append(SalidaNueva.Mudo ? " · EN SILENCIO" : "").Append('\n');
            sb.Append("Protección 0 dBFS: ").Append(SalidaNueva.ProteccionVeces == 0 ? "no ha actuado" : "ACTUÓ " + SalidaNueva.ProteccionVeces + " veces (" + SalidaNueva.ProteccionMuestras + " muestras)").Append('\n');
            sb.Append("Videos: ").Append(SalidaNueva.VideosPorLaRocola ? "por la rocola (" + RocolaOsVideosMedidos + " medidos)" : "mpv directo (la tarjeta se suelta en cada video)");
            if (largo)
            {
                sb.Append('\n').Append("Aperturas de la tarjeta: ").Append(SalidaNueva.Aperturas).Append(" · caídas: ").Append(SalidaNueva.Caidas).Append(" · xruns: ").Append(SalidaNueva.Xruns)
                  .Append(" · fuentes: ").Append(SalidaNueva.FuentesVivas);
                sb.Append('\n').Append("Frecuencias: cada canción sale de su cadena a la suya y la salida nueva la lleva a 44,1 kHz con el sinc de XP, sin limitador (la frecuencia de salida de Ctrl+F3 no se usa).");
                sb.Append('\n').Append("Pico más alto antes de la protección: ").Append(SalidaNueva.PicoMax > 0 ? SalidaNuevaMide.D(20 * Math.Log10(SalidaNueva.PicoMax)) + " dBFS" : "— (aún no sonó nada)");
                if (SalidaNueva.VideoFlujos > 0) sb.Append('\n').Append("Sonido de los videos: ").Append(SalidaNueva.VideoFlujos).Append(" (el último: ").Append(SalidaNueva.VideoFormato).Append(")");
            }
            if (SalidaNueva.Aviso.Length > 0) sb.Append('\n').Append("AVISO: ").Append(SalidaNueva.Aviso);
            return sb.ToString();
        }

        // LA HOJA «SALIDA NUEVA» (modal, encima de Ctrl+F4): todo lo de la salida nueva, el volumen y la medida del convertidor
        private void RocolaOsSalidaNuevaHoja(RocolaOsVentanaCtx x)
        {
            using (Hoja q = new Hoja(MarcoXp.Dialogo))
            {
                q.Text = "Rocola OS - Salida nueva";
                q.ClientSize = new Size(940, 660);
                q.BackColor = x.CFondo;
                q.Font = x.FSub;
                Rotulo lT = new Rotulo(); lT.SetBounds(22, 10, 600, 28); lT.BackColor = Color.Transparent; lT.ForeColor = Color.FromArgb(232, 238, 255); lT.Font = x.FTit; lT.Text = "La salida nueva";
                Tarjeta tA = new Tarjeta(); tA.SetBounds(16, 48, 440, 548); tA.Titulo = "LO QUE ESTÁ ABIERTO"; tA.Acento = Color.FromArgb(150, 190, 255);
                Tarjeta tM = new Tarjeta(); tM.SetBounds(472, 48, 452, 548); tM.Titulo = "MEDIR EL CONVERTIDOR"; tM.Acento = Color.FromArgb(255, 196, 120);
                q.Controls.Add(lT); q.Controls.Add(tA); q.Controls.Add(tM);
                // ---- lo abierto y el volumen
                Rotulo lD = new Rotulo(); lD.SetBounds(16, 40, 408, 300); lD.BackColor = Color.Transparent; lD.ForeColor = x.CTexto; lD.Font = x.FDes;
                Rotulo lV = new Rotulo(); lV.SetBounds(16, 350, 408, 20); lV.BackColor = Color.Transparent; lV.ForeColor = x.CTexto; lV.Font = x.FNom;
                BotonPlano bMenos = new BotonPlano(); bMenos.SetBounds(16, 376, 128, 34); bMenos.Font = x.FBot; bMenos.Text = "- 1 dB";
                BotonPlano bMas = new BotonPlano(); bMas.SetBounds(152, 376, 128, 34); bMas.Font = x.FBot; bMas.Text = "+ 1 dB";
                BotonPlano bMudo = new BotonPlano(); bMudo.SetBounds(288, 376, 136, 34); bMudo.Font = x.FBot;
                Rotulo lVi = new Rotulo(); lVi.SetBounds(16, 418, 408, 120); lVi.BackColor = Color.Transparent; lVi.ForeColor = x.CTenue; lVi.Font = x.FDes;
                lVi.Text = "El volumen del dueño es el de los botones de atrás de la rocola: 1 dB por toque (apretado, unos 10 por segundo); el silencio con su tecla. "
                    + "Es digital, en coma flotante, antes de la única cuantización a 24 bits; se guarda y vale al volver a arrancar. El control de la tarjeta se "
                    + "puso una vez al 100 % y no se toca. La protección de 0 dBFS no debería actuar nunca: si actúa, se apunta (bitácora de Rocola OS).";
                tA.Controls.Add(lD); tA.Controls.Add(lV); tA.Controls.Add(bMenos); tA.Controls.Add(bMas); tA.Controls.Add(bMudo); tA.Controls.Add(lVi);
                // ---- la medida
                Rotulo lI = new Rotulo(); lI.SetBounds(16, 40, 420, 170); lI.BackColor = Color.Transparent; lI.ForeColor = x.CTenue; lI.Font = x.FDes;
                lI.Text = "1. Conecta un cable RCA de la SALIDA del convertidor (PROZOR) a la entrada de línea AZUL de la placa.\n"
                    + "2. Apaga el amplificador o bájalo al mínimo: suenan tonos de 1 kHz de 0 a -20 dBFS (unos 12 s).\n"
                    + "3. Mejor sin música: mientras mide, lo que suena sigue su curso en silencio.\n"
                    + "4. «Medir»: graba la entrada, mide cuánto distorsiona cada nivel (THD+N) y dice desde dónde deja de recortar. "
                    + "Propone el techo de la cadena nueva (techo_dbtp=): el nivel más alto limpio menos 1 dB.\n"
                    + "(Desde la consola de mantenimiento: rocola-mide-dac.)";
                BotonPlano bMide = new BotonPlano(); bMide.SetBounds(16, 216, 420, 38); bMide.Font = x.FBot; bMide.Relleno = Color.FromArgb(96, 70, 30); bMide.Borde = Color.FromArgb(200, 150, 72);
                Rotulo lR = new Rotulo(); lR.SetBounds(16, 262, 420, 230); lR.BackColor = Color.Transparent; lR.ForeColor = x.CTexto; lR.Font = x.FDes;
                BotonPlano bTecho = new BotonPlano(); bTecho.SetBounds(16, 498, 420, 36); bTecho.Font = x.FBot;
                tM.Controls.Add(lI); tM.Controls.Add(bMide); tM.Controls.Add(lR); tM.Controls.Add(bTecho);
                BotonPlano bOk = new BotonPlano(); bOk.SetBounds(784, 608, 140, 38); bOk.Font = x.FBot; bOk.Text = "Listo";
                bOk.Relleno = Color.FromArgb(30, 96, 70); bOk.Borde = Color.FromArgb(72, 182, 132);
                bOk.Click += delegate { q.Close(); };
                q.Controls.Add(bOk);
                string guardado = null;
                Action pinta = delegate
                {
                    lD.Text = RocolaOsSalidaNuevaTexto(true);
                    lV.Text = "Volumen del dueño: " + SalidaNueva.VolumenDb.ToString(CultureInfo.InvariantCulture) + " dB" + (SalidaNueva.Mudo ? " (en silencio)" : "");
                    bool va = SalidaNueva.Va;
                    bMenos.Enabled = va && SalidaNueva.VolumenDb > SalidaNueva.VolumenMinDb; bMas.Enabled = va && SalidaNueva.VolumenDb < SalidaNueva.VolumenMaxDb;
                    bMudo.Text = SalidaNueva.Mudo ? "Quitar silencio" : "Silencio"; bMudo.Enabled = va;
                    bool mide = SalidaNuevaMide.Midiendo;
                    bMide.Text = mide ? "Midiendo... " + (int)(SalidaNuevaMide.Avance * 100) + " %" : "Medir el convertidor (unos 15 s)";
                    bMide.Enabled = va && SalidaNueva.Abierta && !mide;
                    SalidaNuevaMide.Informe inf = SalidaNuevaMide.Ultimo;
                    string tr = !va ? "Necesita la salida nueva encendida y abierta (o «rocola-mide-dac» desde la consola de mantenimiento)."
                        : mide ? SalidaNuevaMide.Estado : inf != null ? inf.Texto() : SalidaNuevaMide.Estado;
                    lR.Text = tr;
                    bool hayTecho = inf != null && !double.IsNaN(inf.Techo);
                    bool clave = RocolaOs.ClaveDe("techo_dbtp") != null;
                    bTecho.Text = guardado ?? (hayTecho ? (clave ? "Guardar techo_dbtp=" + inf.Techo.ToString("0", CultureInfo.InvariantCulture) : "techo_dbtp=" + inf.Techo.ToString("0", CultureInfo.InvariantCulture) + " (la cadena nueva aún no lo lee)") : "Guardar el techo (después de medir)");
                    bTecho.Enabled = hayTecho && clave && guardado == null;
                    bMenos.Invalidate(); bMas.Invalidate(); bMudo.Invalidate(); bMide.Invalidate(); bTecho.Invalidate();
                };
                bMenos.Click += delegate { SalidaNueva.CambiaVolumen(-1, false); pinta(); };
                bMas.Click += delegate { SalidaNueva.CambiaVolumen(1, false); pinta(); };
                bMudo.Click += delegate { SalidaNueva.CambiaVolumen(0, true); pinta(); };
                bMide.Click += delegate { guardado = null; SalidaNuevaMide.EmpiezaEnLaRocola(); pinta(); };
                bTecho.Click += delegate
                {
                    SalidaNuevaMide.Informe inf = SalidaNuevaMide.Ultimo;
                    if (inf == null || double.IsNaN(inf.Techo)) return;
                    string v = inf.Techo.ToString("0", CultureInfo.InvariantCulture);
                    guardado = RocolaOs.PonAjuste("techo_dbtp", v) ? "Guardado: techo_dbtp=" + v : "No se pudo guardar techo_dbtp=" + v;
                    RocolaOs.Apunta(SalidaNueva.NombreFuncion, guardado + " (de la medida del convertidor)");
                    pinta();
                };
                q.TeclaAntes += delegate (PulsoTecla ke) { if (ke.KeyCode == Tecla.Escape) q.Close(); };
                MonedaTambienAqui(q);
                Reloj r = new Reloj("RocolaOs:salida-nueva-hoja", null); r.Interval = 500;
                r.Tick += delegate { try { pinta(); } catch { } };
                r.Start();
                pinta();
                q.MuestraModal(this);
                r.Stop(); r.Dispose();
            }
        }
    }
}
