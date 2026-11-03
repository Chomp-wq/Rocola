// IA1: CUANDO ENTRENA LA ROCOLA Y COMO SE QUEDA EL DUENO CON LA IA NUEVA (Rocola OS, 3-11-2026; el entrenador, EntrenaIa.cs).
//
// CUANDO: solo en reposo de verdad (nada sonando, la cola vacia, nadie ha tocado un boton en 15 minutos, sin video ni comparacion a
// ciegas), como mucho una vez cada 20 horas, y solo si el material crecio (un 10 % y al menos 5.000 ejemplos desde el ultimo intento;
// la primera vez, con 2.000). Va en su propio hilo con la prioridad mas baja (nice 19) y se corta EN CUANTO la rocola se usa (una
// moneda, un boton, una cancion): ese intento no cuenta y se repite en la siguiente pausa (pasada una hora). Lo hecho queda en
// rocola_ia_entreno.txt (cuando, cuanto material y el resultado, una linea) y el informe entero en IAS/rocola_ia_entreno_informe.txt.
// «Entrenar ahora» (Ctrl+F3 -> Sonido -> Mejoras con IA) lo pide sin esperar al reposo; tambien se corta si empieza a sonar algo.
//
// LA CANDIDATA: si la red nueva pasa las reglas queda como IAS/rocola_ia.candidata.bin (con su informe .txt). No se pone sola:
//  - «Comparar a ciegas: la IA de ahora o la nueva» (Lo que suena ahora) cambia cada 15 s, sin decir cual, entre la red puesta y la
//    candidata en la cancion que suena (CompararCiegas.cs, CiegoIaNueva). Solo se oye en canciones con los agudos cortados, con el
//    DSEE encendido (y por tanto con el modo billar APAGADO: con el modo, el DSEE no suena) y con el modelo general (no el de su genero);
//  - si la nueva gana los votos, «Quedarse con la IA nueva» la pone: la de antes se guarda al lado como .anterior-N (nunca se borra un
//    modelo) y la nueva suena desde ese momento;
//  - «Descartar la IA nueva» la aparta (.descartada-N).
using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace Rocola
{
    public partial class Form1
    {
        internal const string CandidataIa = "rocola_ia.candidata.bin";
        private const int EntrenoReposoMin = 15, EntrenoCadaHoras = 20, EntrenoMinPrimera = 2000, EntrenoMinMas = 5000;
        private volatile bool entrenando, entrenoPedido, entrenoForzado;
        private volatile string entrenoAvance = "";
        private bool iaNuevaGano;
        private string iaMensaje = "";   // lo ultimo que dijo un boton de la IA nueva (Ctrl+F3)
        private int ultimoEntrenoMira;

        private string EntrenoEstadoArchivo { get { return RutaDatos("rocola_ia_entreno.txt"); } }
        private string RutaCandidata { get { return Path.Combine(CarpetaIas, CandidataIa); } }
        public bool HayIaNueva { get { try { return File.Exists(RutaCandidata); } catch { return false; } } }

        // Reposo de verdad: nada suena ni espera, y nadie toca la rocola desde hace un rato.
        private bool EnReposoParaEntrenar(int minutos)
        {
            PlayItem p = nowPlaying;
            return p == null && queue.Count == 0 && !ciegoActivo && bandaSonando == null && (DateTime.Now - lastInput).TotalMinutes >= minutos;
        }

        // Una vez por minuto (Periodico).
        private void MiraEntreno()
        {
            if (entrenando) return;
            if (!entrenoPedido && !EnReposoParaEntrenar(EntrenoReposoMin)) return;
            string datos = IaDataFile(), idx = IaIndexFile();
            long filas;
            try { if (!File.Exists(datos) || !File.Exists(idx)) { entrenoPedido = false; return; } filas = (new FileInfo(datos).Length - 16) / 160; } catch { return; }
            DateTime ultimo; long filasAntes; bool cortado; LeeEstadoEntreno(out ultimo, out filasAntes, out cortado);
            if (!entrenoPedido)
            {
                double horas = (DateTime.Now - ultimo).TotalHours;
                if (cortado) { if (horas < 1) return; }
                else
                {
                    if (horas < EntrenoCadaHoras) return;
                    if (filasAntes <= 0 ? filas < EntrenoMinPrimera : (filas < filasAntes * 1.1 || filas - filasAntes < EntrenoMinMas)) return;
                }
            }
            entrenoForzado = entrenoPedido; entrenoPedido = false; entrenando = true; entrenoAvance = "empezando";
            string instalada = RutaLectura("rocola_ia.bin");
            Thread t = new Thread(delegate () { EntrenaEnFondo(datos, idx, instalada, filas); }) { IsBackground = true, Name = "entrenar la IA" };
            t.Start();
        }

        private void EntrenaEnFondo(string datos, string idx, string instalada, long filas)
        {
            Prioridad.Fondo();
            EntrenoIa e = new EntrenoIa();
            try
            {
                string mx = Environment.GetEnvironmentVariable("ROCOLA_IA_FILAS"), ep = Environment.GetEnvironmentVariable("ROCOLA_IA_EPOCAS");   // pruebas
                int v; if (mx != null && int.TryParse(mx, out v) && v >= 1000) e.MaxFilas = v; if (ep != null && int.TryParse(ep, out v) && v >= 1 && v <= 200) e.Epocas = v;
                bool forzado = entrenoForzado;
                DateTime inicio = DateTime.Now;   // un boton DESPUES de empezar corta (en reposo); pedido a mano, solo la musica o una comparacion
                e.Parar = delegate { return cerrandoEntreno || nowPlaying != null || queue.Count > 0 || ciegoActivo || bandaSonando != null || (!forzado && lastInput > inicio); };
                e.Avance = delegate (string s) { entrenoAvance = s; };
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                e.Corre(datos, idx, instalada);
                string tiempo = " (" + Math.Round(sw.Elapsed.TotalMinutes, 1).ToString(CultureInfo.InvariantCulture) + " min)";
                Directory.CreateDirectory(CarpetaIas);
                if (!e.Cortado)
                {
                    File.WriteAllText(Path.Combine(CarpetaIas, "rocola_ia_entreno_informe.txt"), e.Informe + "Tardo" + tiempo + ".\n");
                    if (e.Aceptada && e.Modelo != null)
                    {
                        string tmp = RutaCandidata + ".nueva";
                        File.WriteAllBytes(tmp, e.Modelo);
                        File.Move(tmp, RutaCandidata, true);
                        File.WriteAllText(Path.ChangeExtension(RutaCandidata, ".txt"), e.Informe);
                        iaNuevaGano = false;   // es otra: hay que volver a compararla
                    }
                }
                EscribeEstadoEntreno(filas, e.Cortado, e.Resumen + tiempo);
                entrenoAvance = e.Resumen + tiempo;
            }
            catch (Exception ex) { entrenoAvance = "no se pudo entrenar: " + ex.Message; try { EscribeEstadoEntreno(filas, false, entrenoAvance); } catch { } }
            finally { entrenando = false; }
        }
        private volatile bool cerrandoEntreno;

        private void LeeEstadoEntreno(out DateTime ultimo, out long filas, out bool cortado)
        {
            ultimo = DateTime.MinValue; filas = 0; cortado = false;
            try
            {
                string[] l = File.ReadAllText(EntrenoEstadoArchivo).Split('\t');
                ultimo = new DateTime(long.Parse(l[0], CultureInfo.InvariantCulture), DateTimeKind.Local);
                filas = long.Parse(l[1], CultureInfo.InvariantCulture); cortado = l[2] == "1";
            }
            catch { }
        }
        private void EscribeEstadoEntreno(long filas, bool cortado, string resumen)
        {
            string t = DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture) + "\t" + filas.ToString(CultureInfo.InvariantCulture) + "\t" + (cortado ? "1" : "0") + "\t"
                     + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\t" + resumen.Replace('\t', ' ').Replace('\n', ' ');
            File.WriteAllText(EntrenoEstadoArchivo + ".nuevo", t); File.Move(EntrenoEstadoArchivo + ".nuevo", EntrenoEstadoArchivo, true);
        }

        // Lo que dice Ctrl+F3.
        public string TextoEntreno()
        {
            if (entrenando) return "Entrenando la IA nueva (" + entrenoAvance + ").";
            string ultimo = "";
            try { string[] l = File.ReadAllText(EntrenoEstadoArchivo).Split('\t'); if (l.Length >= 5) ultimo = l[3] + ": " + l[4]; } catch { }
            if (entrenoAvance.Length > 0 && ultimo.Length == 0) ultimo = entrenoAvance;
            return (ultimo.Length > 0 ? "Ultimo entrenamiento, " + ultimo + "." : "Aun no ha entrenado: lo hace sola en reposo, con material nuevo.")
                 + (HayIaNueva ? (iaNuevaGano ? " La IA nueva gano a ciegas: puedes quedarte con ella." : " Hay una IA nueva: comparala a ciegas.") : "");
        }
        public string EntrenaAhora()
        {
            if (entrenando) return "Ya esta entrenando.";
            if (!File.Exists(IaDataFile()) || !File.Exists(IaIndexFile())) return "Aun no hay material (lo junta el repaso de la biblioteca).";
            entrenoPedido = true; ultimoEntrenoMira = 0; MiraEntreno();
            return entrenando ? "Entrenando: se para si empieza a sonar algo." : "No se pudo empezar.";
        }

        public string QuedateConIaNueva()
        {
            if (!HayIaNueva) return "No hay IA nueva.";
            if (!iaNuevaGano) return "Comparala antes a ciegas (y que gane).";
            try
            {
                if (ciegoActivo) TerminaCiego("");
                Directory.CreateDirectory(CarpetaIas);
                string puesta = Path.Combine(CarpetaIas, "rocola_ia.bin");
                if (File.Exists(puesta)) File.Move(puesta, LibreAnterior(puesta));
                File.Move(RutaCandidata, puesta);
                try { string txt = Path.ChangeExtension(RutaCandidata, ".txt"); if (File.Exists(txt)) File.Move(txt, LibreAnterior(Path.Combine(CarpetaIas, "rocola_ia.puesta.txt"))); } catch { }
                NeuralEnv.TryLoad(RutaLectura("rocola_ia.bin"));
                iaNuevaGano = false;
                return "Listo: suena la IA nueva (la de antes quedo guardada al lado).";
            }
            catch (Exception ex) { return "No se pudo: " + ex.Message; }
        }
        public string DescartaIaNueva()
        {
            if (!HayIaNueva) return "No hay IA nueva.";
            try
            {
                if (ciegoActivo && ciegoQue == CiegoIaNueva) TerminaCiego("");
                string d = RutaCandidata.Replace(".candidata.bin", ".descartada.bin");
                File.Move(RutaCandidata, LibreAnterior(d));
                iaNuevaGano = false;
                return "IA nueva descartada (guardada aparte).";
            }
            catch (Exception ex) { return "No se pudo: " + ex.Message; }
        }

        // La comparacion a ciegas: true = la candidata.
        private void PonIaComparada(bool nueva)
        {
            if (nueva) NeuralEnv.TryLoad(RutaCandidata);
            else { string p = RutaLectura("rocola_ia.bin"); if (File.Exists(p)) NeuralEnv.TryLoad(p); else NeuralEnv.Descarga(); }
        }
    }
}
