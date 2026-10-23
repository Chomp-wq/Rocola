// ================================================================================================
//  EL DIAGNOSTICO COMPLETO, LADO DE LA ROCOLA (23-10-2026)
//  (el motor, la toma de muestras y el informe viven en DiagnosticoCompleto.cs; esto monta las escenas con la rocola de verdad)
// ------------------------------------------------------------------------------------------------
//  REGLAS QUE ESTE ARCHIVO CUMPLE (pruebas/diagnostico.cs las vigila):
//   - NO guarda ni aplica nada: no llama a SaveConfigPrefs, no reinicia la tarjeta y no toca los volumenes (ni el de Windows ni el de la
//     salida). La cancion del diagnostico, si hace falta una, se pone por PlayTrackForMeasurement: sale MUDA desde dentro de la cadena
//     (CallaSiEsMedida, SalidaSonido.cs), igual que la del medidor rapido. La cancion de un cliente no se para ni se cambia.
//   - Deja todo como estaba: efectos, ahorros, la estrella 3D, la pantalla, la letra, la pagina, el disco abierto, el buscador, la
//     atraccion, el asistente, los colores del genero y la musica. Pase lo que pase (finally).
//   - Si alguien toca algo (un boton, una moneda, el raton), se cancela: lo deja todo y el informe lleva lo medido hasta entonces.
//     Los primeros 2 s no cuentan (el raton se asienta al cerrar la ventana de ajustes).
//   - El libro de los peores cuadros se enciende durante el diagnostico (aunque no este diagnostico=1) y se deja como estaba.
// ================================================================================================
#if ROCOLA_GPU
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace Rocola
{
    public partial class Form1
    {
        private bool diagEnMarcha;

        // Lo que hay que devolver al acabar.
        private sealed class DiagEstado
        {
            internal bool Buscador, Atraccion, Disco, Estrella3d, IdleTarget, LibroAntes, PuseCancion, Asistente;
            internal char Letra; internal int Pagina, Sel;
            internal int[] Niv; internal bool[] Ahorro;
            internal PlayItem NowPlaying;
            internal Color Acento, Genero, Port1, Port2, PanelBack, ListBack, ListAccent, PagerBack;
            internal bool SinGenero, ArtistaFijo;
        }

        // Lo que comparten las escenas.
        private sealed class DiagCtx
        {
            internal DiagEstado Bk;
            internal bool ConMusica, MusicaPropia;
            internal string MusicaTexto = "sin musica";
            internal PlayItem ItemPropio;
            internal int HojeoDir = 1, Cambio;
            internal PlayItem[] Items = new PlayItem[0];
            internal int BaseTick; internal DateTime BaseInput;
            internal int GraciaHasta, TransT0;
        }

        // ============================ LA ENTRADA ============================
        private void CorreDiagnosticoGpu()
        {
            diagResumen = "";
            if (diagEnMarcha) return;
            if (gpuPanel == null || !gpuEnabled || !gpuPanel.Visible)
            { diagResumen = "No se hizo el diagnostico: la rocola no dibuja con la tarjeta grafica ahora.\nSin ella no hay nada que medir por este camino."; return; }
            if (videoOn)
            { diagResumen = "No se hizo el diagnostico: hay un video en ventana.\nOcupa la tarjeta y el procesador y la medida no serviria."; return; }
            if (IsDisposed || Disposing || startupClosing) return;
            diagEnMarcha = true;
            DiagEstado bk = DiagGuardaEstado();
            DiagCtx c = new DiagCtx(); c.Bk = bk;
            DiagMeta meta = new DiagMeta();
            DiagnosticoCompleto motor = null;
            string error = null;
            string equipo = "", ajustes = "", repasoIni = "";
            try
            {
                meta.Inicio = DateTime.Now; meta.Version = DeCuandoEs();
                meta.Nucleos = Math.Max(1, Environment.ProcessorCount);
                equipo = DiagTextoEquipo();
                ajustes = DiagTextoAjustes();
                repasoIni = DiagFondo();
                DiagPoneMusica(c);
                DiagAnfitrion host = DiagCreaAnfitrion(c);
                motor = new DiagnosticoCompleto(host);
                motor.Nucleos = meta.Nucleos;
                List<DiagEscenaDef> escenas = DiagEscenas(c);
                for (int i = 0; i < escenas.Count; i++) motor.Escenas.Add(escenas[i]);
                // El libro de los peores cuadros, encendido mientras dura (y como estaba al acabar), y la toma enchufada.
                bk.LibroAntes = gpuPanel.LibroActivo;
                gpuPanel.LibroActivo = true;
                gpuPanel.DiagSink = motor.Captura;
                c.GraciaHasta = Environment.TickCount + 2000;
                DiagRebase(c);
                motor.Corre();
            }
            catch (Exception ex) { error = ex.Message; LogError("Diagnostico completo", ex); }
            finally
            {
                try { if (gpuPanel != null) { gpuPanel.DiagSink = null; gpuPanel.LibroActivo = bk.LibroAntes; } } catch { }
                string fondoFin = "";
                try { fondoFin = DiagFondo(); } catch { }
                DiagRestaura(bk);
                diagEnMarcha = false;
                try { ShowToast("Diagnostico terminado: restaurando todo", Color.FromArgb(40, 130, 90)); } catch { }
                if (motor != null)
                {
                    try { DiagTerminaInforme(motor, meta, c, equipo, ajustes, repasoIni, fondoFin, error); }
                    catch (Exception ex2) { diagResumen = "El diagnostico termino pero no se pudo armar el informe: " + ex2.Message; LogError("Informe del diagnostico", ex2); }
                }
                else diagResumen = "No se pudo empezar el diagnostico: " + (error ?? "motivo desconocido");
            }
        }

        private void DiagTerminaInforme(DiagnosticoCompleto motor, DiagMeta meta, DiagCtx c, string equipo, string ajustes, string repasoIni, string fondoFin, string error)
        {
            meta.DuracionMs = motor.FinMs - motor.InicioMs;
            meta.Hz = motor.Hz; meta.Equipo = equipo; meta.Ajustes = ajustes;
            meta.Cancelado = motor.Cancelado; meta.MotivoCancelacion = motor.MotivoCancelacion;
            StringBuilder cond = new StringBuilder();
            string nl = "\r\n";
            cond.Append("Musica: ").Append(c.MusicaTexto).Append(nl);
            cond.Append("Trabajo de fondo al empezar: ").Append(repasoIni.Length > 0 ? repasoIni : "nada (ni repaso ni miniaturas pendientes)").Append(nl);
            cond.Append("Trabajo de fondo al acabar: ").Append(fondoFin.Length > 0 ? fondoFin : "nada").Append(nl);
            cond.Append("Monitor: NO se solto; los cuadros se piden con el ritmo del bucle de verdad (AgendaCuadro + RenderFrame). El libro de los peores cuadros estuvo encendido durante las escenas.").Append(nl);
            cond.Append("Escenas recorridas: ").Append(motor.Resultados.Count).Append("; tiempo maximo del diagnostico ").Append((int)(motor.PresupuestoMs / 1000)).Append(" s.").Append(nl);
            if (!string.IsNullOrEmpty(error)) cond.Append("ERROR durante el diagnostico: ").Append(error).Append(nl);
            if (!c.ConMusica) cond.Append("SIN MUSICA: las escenas de estado extendido y de cambio de cancion necesitan una cancion; se saltaron.").Append(nl);
            meta.Condiciones = cond.ToString();
            string texto = DiagInforme.Construye(meta, motor.Resultados);
            string err;
            string ruta = DiagInforme.Escribe(CarpetaDatos, texto, meta.Inicio, DiagInforme.Conservar, out err);
            if (ruta == null) LogError("No se pudo escribir el informe del diagnostico: " + err, null);
            diagResumen = DiagInforme.Resumen(meta, motor.Resultados, ruta);
            Diag("diagnostico completo: " + diagResumen.Replace("\n", " | "));
        }

        // ============================ GUARDAR Y DEVOLVER ============================
        private DiagEstado DiagGuardaEstado()
        {
            DiagEstado b = new DiagEstado();
            b.Buscador = searchOpen; b.Atraccion = attractOn; b.Disco = (panelSongs != null && panelSongs.Visible);
            b.Letra = currentLetter; b.Pagina = page; b.Sel = selAlbumIndex;
            EfectoGrafico[] ef = Efectos();
            b.Niv = new int[ef.Length];
            for (int i = 0; i < ef.Length; i++) b.Niv[i] = LeeNivel(ef[i].Clave);
            b.Ahorro = new bool[NombreAhorro.Length];
            for (int i = 0; i < b.Ahorro.Length; i++) b.Ahorro[i] = LeeAhorro(i);
            b.Estrella3d = estrella3d; b.IdleTarget = gpuPanel != null && gpuPanel.IdleExpandTarget;
            b.NowPlaying = nowPlaying; b.Asistente = asistenteVisible;
            b.Acento = bgAccentTarget; b.Genero = bgGeneroTarget; b.Port1 = bgPort1; b.Port2 = bgPort2;
            b.SinGenero = bgSinGenero; b.ArtistaFijo = bgArtistaFijo;
            try { if (panelSongs != null) b.PanelBack = panelSongs.BackColor; if (songList != null) { b.ListBack = songList.BackColor; b.ListAccent = songList.Accent; } if (songPager != null) b.PagerBack = songPager.BackColor; } catch { }
            return b;
        }

        // Todo vuelve a su sitio, cada paso por su cuenta: si uno falla, los demas se hacen igual.
        private void DiagRestaura(DiagEstado bk)
        {
            try { if (asistenteVisible && !bk.Asistente) CierraAsistente(); } catch { }
            try { if (bancoReposoActivo) QuitaEscena(EscReposo); } catch { }
            try { EfectoGrafico[] ef = Efectos(); for (int i = 0; i < ef.Length && i < bk.Niv.Length; i++) if (LeeNivel(ef[i].Clave) != bk.Niv[i]) PonNivel(ef[i].Clave, bk.Niv[i]); } catch { }
            try { for (int i = 0; i < bk.Ahorro.Length; i++) if (LeeAhorro(i) != bk.Ahorro[i]) PonAhorro(i, bk.Ahorro[i]); } catch { }
            try { if (estrella3d != bk.Estrella3d) { estrella3d = bk.Estrella3d; AplicaEfectosGpu(); } } catch { }
            try
            {
                if (bk.PuseCancion)
                {
                    DisposePlaybackNow();
                    nowPlaying = bk.NowPlaying;   // null: no sonaba nada antes
                    RefrescaCola(); UpdateNowPlayingUI();
                }
                // Con la cancion de un cliente nunca se toco nowPlaying: si cambio mientras tanto, es que su cancion acabo de verdad, y asi se queda.
            }
            catch { }
            try
            {
                if (searchOpen && !bk.Buscador) CloseSearch();
                if (panelSongs != null && panelSongs.Visible && !bk.Disco) BackToAlbums();
                if (attractOn && !bk.Atraccion) ExitAttract();
                currentLetter = bk.Letra; page = bk.Pagina; selAlbumIndex = bk.Sel;
                RefreshAlbums();
                if (bk.Atraccion && !attractOn) EnterAttract();
                else if (bk.Buscador && !searchOpen) OpenSearch();
            }
            catch { }
            try
            {
                // Los colores del genero, como estaban (el cambio de cancion los movio).
                bgAccentTarget = bk.Acento; bgGeneroTarget = bk.Genero; bgPort1 = bk.Port1; bgPort2 = bk.Port2;
                bgSinGenero = bk.SinGenero; bgArtistaFijo = bk.ArtistaFijo;
                if (nowPlaying != null && !nowPlaying.IsVideo && nowPlaying.Song != null) ApplyGenreBackground(nowPlaying.Song, nowPlaying.Disc);
                else
                {
                    if (panelSongs != null) panelSongs.BackColor = bk.PanelBack;
                    if (songList != null) { songList.BackColor = bk.ListBack; songList.Accent = bk.ListAccent; }
                    if (songPager != null) songPager.BackColor = bk.PagerBack;
                    if (estadoColorDirecto) CambioElAcentoDelPanel(bk.Acento);
                }
            }
            catch { }
            try { if (gpuPanel != null) gpuPanel.IdleExpandTarget = bk.IdleTarget; } catch { }
        }

        // ============================ LA MUSICA ============================
        // Si ya suena algo se usa ESO (es de un cliente: ni se para ni se le toca el volumen). Si no, una cancion MUDA de la
        // biblioteca, como la del medidor rapido, presentada como «sonando» solo para que el estado extendido tenga algo que mostrar.
        private void DiagPoneMusica(DiagCtx c)
        {
            c.ConMusica = false; c.MusicaPropia = false;
            if (nowPlaying != null)
            {
                if (nowPlaying.IsVideo) { c.MusicaTexto = "sin musica (suena un video de la cola)"; return; }
                c.ConMusica = output != null;
                c.MusicaTexto = c.ConMusica ? "REAL: la cancion de un cliente (" + DiagTitulo(nowPlaying) + "); no se toco" : "sin musica (la cola dice que suena algo, pero no hay salida abierta)";
                return;
            }
            DiagArrancaPropia(c);
        }

        private string DiagTitulo(PlayItem it)
        {
            try { string t = it != null ? it.Display() : ""; return t != null && t.Length > 60 ? t.Substring(0, 60) + "..." : t; }
            catch { return ""; }
        }

        // Busca una cancion de la biblioteca (con su disco) y la pone muda. Devuelve false si no hay o no abrio.
        private bool DiagArrancaPropia(DiagCtx c)
        {
            if (tracks.Count == 0) { c.MusicaTexto = "sin musica (la biblioteca no tiene canciones)"; return false; }
            PlayItem it = DiagItemAleatorio(c.ItemPropio);
            if (it == null) { c.MusicaTexto = "sin musica (ninguna cancion con disco)"; return false; }
            try
            {
                if (c.MusicaPropia) DisposePlaybackNow();
                nowPlaying = it;
                PlayTrackForMeasurement(it.Song);
                if (output != null)
                {
                    c.MusicaPropia = true; c.ConMusica = true; c.ItemPropio = it; c.Bk.PuseCancion = true;
                    c.MusicaTexto = "MUDA del diagnostico (la cadena de sonido trabaja entera, sale callada desde dentro): " + DiagTitulo(it);
                    // La salida arranca cuando el hilo de la musica tiene su primer segundo: se le deja llegar (con los mensajes corriendo).
                    int t0 = Environment.TickCount;
                    while (unchecked(Environment.TickCount - t0) < 2000 && output != null && output.PlaybackState != NAudio.Wave.PlaybackState.Playing)
                    { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
                    return true;
                }
            }
            catch { }
            nowPlaying = c.Bk.NowPlaying;
            c.MusicaPropia = false; c.ConMusica = false; c.MusicaTexto = "sin musica (no se pudo abrir la cancion del diagnostico)";
            return false;
        }

        private PlayItem DiagItemAleatorio(PlayItem distintoDe)
        {
            Random az = new Random();
            for (int intento = 0; intento < 40; intento++)
            {
                Track t = tracks[az.Next(tracks.Count)];
                if (distintoDe != null && object.ReferenceEquals(distintoDe.Song, t) && tracks.Count > 1) continue;
                Album d = DiagDiscoDe(t);
                if (d == null) continue;
                PlayItem it = new PlayItem(); it.IsVideo = false; it.Song = t; it.Disc = d;
                return it;
            }
            return null;
        }

        private Album DiagDiscoDe(Track t)
        {
            for (int i = 0; i < albums.Count; i++)
            {
                Album a = albums[i];
                if (a.IsTop || a.Tracks == null) continue;
                if (a.Tracks.Contains(t)) return a;
            }
            return null;
        }

        // Que la cancion propia dure lo que queda de escena (si esta a punto de acabar, otra). Entre escenas, nunca dentro.
        private void DiagAseguraMusica(DiagCtx c, int minMs)
        {
            if (!c.MusicaPropia) return;
            try
            {
                bool viva = output != null && nowPlaying != null;
                double restan = double.MaxValue;
                if (reader != null && reader.TotalTime.TotalSeconds > 0) restan = (reader.TotalTime.TotalSeconds - reader.CurrentTime.TotalSeconds) * 1000.0;
                if (viva && restan > minMs + 15000) return;
                DiagArrancaPropia(c);
            }
            catch { }
        }

        // ============================ EL ANFITRION ============================
        private DiagAnfitrion DiagCreaAnfitrion(DiagCtx c)
        {
            DiagAnfitrion h = new DiagAnfitrion();
            GpuPanel p = gpuPanel;
            h.Cuadro = delegate { return p.DiagCuadroPaceado(); };
            h.Bombea = delegate { Application.DoEvents(); };
            h.Interrumpido = delegate { return DiagInterrumpido(c); };
            h.Ahora = delegate { return GpuPanel.AhoraMs(); };
            h.PeriodoRefMs = delegate { int hz = p.RefrescoHz; return 1000.0 / (hz > 0 ? hz : 60); };
            h.ReiniciaMedida = delegate { p.ReiniciaMedida(); };
            h.Pulso = delegate { if (!c.ConMusica) PulsoDeMentira(); };
            h.Latidos = delegate { return frameTick; };
            h.Latido = delegate { AnimTimer_Tick(null, EventArgs.Empty); };
            h.Aviso = delegate (string t) { ShowToast(t, Color.FromArgb(40, 130, 90)); };
            h.Cortes = delegate { BombaSonido b = bomba; return b != null ? b.Huecos : -1; };
            h.Tardios = delegate { FuenteLatenciaAudio m = medidorSalida; return m != null ? m.Atrasos : -1; };
            h.MaxTardioMs = delegate { FuenteLatenciaAudio m = medidorSalida; return m != null ? m.MaxAtrasoMs : -1; };
            h.TexMb = delegate { return p.DiagMemoriaTexturaMb; };
            h.Perdidas = delegate { return p.DiagPerdidas; };
            h.Fondo = delegate { return DiagFondo(); };
            h.Musica = delegate { return c.MusicaTexto; };
            h.Portadas = delegate { return DiagTextoPortadas(); };
            return h;
        }

        // Alguien toco algo: un boton (lastInputTick), el raton o la pantalla (lastInput), o la rocola se cierra.
        private bool DiagInterrumpido(DiagCtx c)
        {
            if (IsDisposed || Disposing || startupClosing) return true;
            if (unchecked(Environment.TickCount - c.GraciaHasta) < 0) { DiagRebase(c); return false; }
            if (lastInputTick != c.BaseTick) return true;
            // Mientras la estrella esta abierta ella misma renueva lastInput en cada latido (TickAsistenteGpu): ahi solo cuentan las teclas.
            if (!AsistenteVisible && lastInput != c.BaseInput) return true;
            return false;
        }
        private void DiagRebase(DiagCtx c) { c.BaseTick = lastInputTick; c.BaseInput = lastInput; }

        private string DiagFondo()
        {
            List<string> p = new List<string>();
            try
            {
                int falta = System.Threading.Thread.VolatileRead(ref preScanLeft);
                bool vivo = false;
                System.Threading.Thread[] th = preScanTh;
                if (th != null) for (int i = 0; i < th.Length; i++) if (th[i] != null && th[i].IsAlive) vivo = true;
                if (preparaRepasoTh != null && preparaRepasoTh.IsAlive) vivo = true;
                if (falta > 0 || vivo) p.Add("repaso/preanalisis de la biblioteca ACTIVO (" + Math.Max(0, falta) + " archivos por medir)");
                if (durScanTh != null && durScanTh.IsAlive) p.Add("lectura de duraciones en curso");
                int mp = minisPendientes; if (mp > 0) p.Add("miniaturas pendientes: " + mp);
                if (scanner != null && scanner.IsBusy) p.Add("escaneo de la biblioteca en curso");
            }
            catch { }
            return string.Join("; ", p.ToArray());
        }

        private string DiagTextoPortadas()
        {
            try
            {
                return "regulador: paginas listas " + coverGovernor.CompletedPages + "/" + coverGovernor.StartedPages + ", cuadros lentos (>33 ms) " + coverGovernor.SlowFrames
                    + ", peor pagina " + coverGovernor.WorstPageMs.ToString("0", CultureInfo.InvariantCulture) + " ms, portadas vistas " + coverGovernor.SeenCovers + " (ya listas al verlas: " + coverGovernor.ReadyAtFirstSeen + ")";
            }
            catch { return ""; }
        }

        // ============================ EL TEXTO DEL EQUIPO Y DE LOS AJUSTES ============================
        private string DiagTextoEquipo()
        {
            string nl = "\r\n";
            StringBuilder sb = new StringBuilder();
            long total, libre;
            sb.Append("Sistema operativo: ").Append(Environment.OSVersion.ToString()).Append(" (.NET ").Append(Environment.Version).Append(IntPtr.Size == 8 ? ", 64 bits" : ", 32 bits").Append(")").Append(nl);
            sb.Append("Procesador: ").Append(DiagEquipo.Cpu()).Append(nl);
            sb.Append("Memoria: ").Append(DiagEquipo.Memoria(out total, out libre)).Append(nl);
            sb.Append("Tarjeta grafica: ").Append(DiagEquipo.Tarjeta()).Append("; Direct3D 9").Append(gpuEnabled ? ", dispositivo creado" : "").Append(nl);
            if (gpuPanel != null)
            {
                int hz = gpuPanel.RefrescoHz;
                sb.Append("Pantalla: ").Append(gpuPanel.Width).Append("x").Append(gpuPanel.Height).Append(", refresco ").Append(hz).Append(" Hz (tope de cuadros de la rocola: ").Append(gpuPanel.TopeFpsReal).Append(" por segundo)").Append(nl);
                long tex = gpuPanel.DiagMemoriaTexturaMb;
                sb.Append("Memoria de texturas disponible al empezar: ").Append(tex > 0 ? tex + " MB" : "(la tarjeta no lo dijo)").Append(nl);
            }
            sb.Append("Biblioteca: ").Append(albums.Count).Append(" discos, ").Append(tracks.Count).Append(" canciones. Carpeta de datos: ").Append(CarpetaDatos).Append(nl);
            sb.Append("Perfil de disco: ").Append(perfilTexto.Length > 0 ? perfilTexto : "(sin medir)").Append(nl);
            return sb.ToString();
        }

        private string DiagTextoAjustes()
        {
            string nl = "\r\n";
            StringBuilder sb = new StringBuilder();
            EfectoGrafico[] ef = Efectos();
            string[] nom = { "bajo", "medio", "alto" };
            int comun = -2;
            for (int i = 0; i < ef.Length; i++) { int n = LeeNivel(ef[i].Clave); if (comun == -2) comun = n; else if (comun != n) comun = -1; }
            bool todos = TodosLosAhorros(true), ninguno = TodosLosAhorros(false);
            string preset = (comun == NivAlto && todos) ? "Performance" : (comun == NivAlto && ninguno) ? "Alto" : (comun == NivMedio || comun == NivBajo) ? nom[comun] + (todos ? " (con los ahorros)" : "") : "a medida";
            sb.Append("Preset de graficos: ").Append(preset).Append(nl);
            sb.Append("Efectos:");
            for (int i = 0; i < ef.Length; i++) sb.Append(' ').Append(ef[i].Clave).Append('=').Append(nom[LeeNivel(ef[i].Clave)]).Append(i + 1 < ef.Length ? "," : "");
            sb.Append(nl);
            sb.Append("Ahorros de Performance (hojas Cielo, Grano, Aurora, Estrella, Estado):").Append(nl);
            for (int i = 0; i < NombreAhorro.Length; i++)
            {
                string motivo = null; try { motivo = MotivoSinAhorro(i); } catch { }
                sb.Append("   ").Append(NombreAhorro[i]).Append(": ").Append(LeeAhorro(i) ? "encendido" : "apagado").Append(motivo != null ? "  [en gris: " + motivo + "]" : "").Append(nl);
            }
            sb.Append("Perfil de carga de caratulas: ").Append(NombreCargaCaratulas(caratulasCarga)).Append("; miniaturas provisionales: ").Append(caratulasLod ? "si" : "no").Append(nl);
            sb.Append("Cuadros: tope=").Append(fpsTope == 0 ? "automatico" : fpsTope.ToString()).Append(", ondas ").Append(animOndasFps).Append(" fps, cubos ").Append(animCubosFps).Append(" fps").Append(nl);
            sb.Append("Estrella 3D: ").Append(estrella3d ? "si" : "no").Append("; astro principal: ").Append(lunaOn ? "si" : "no").Append("; estrella viajera con orbitas: ").Append(lunaActiva ? "si" : "no")
              .Append("; ecos: ").Append(estelaEstelar ? "si" : "no").Append("; corona: ").Append(haloLunar ? "si" : "no").Append(nl);
            sb.Append("Estado ampliado en reposo: ").Append(estadoExpandido ? "si" : "no").Append("; ocultar discos en reposo: ").Append(muroReposo ? "si" : "no")
              .Append("; hojeo ligero: ").Append(hojeoLigero ? "si" : "no").Append("; lista transparente: ").Append(cancionesTransparentes ? "si" : "no").Append(nl);
            sb.Append("diagnostico=1 en la configuracion: ").Append(diagOn ? "si" : "no").Append(nl);
            return sb.ToString();
        }

        // ============================ LAS ESCENAS ============================
        private static DiagEscenaDef DiagDef(string id, string nombre, string que, int calienta, int medida, int paso)
        {
            DiagEscenaDef e = new DiagEscenaDef();
            e.Id = id; e.Nombre = nombre; e.Que = que; e.CalentaMs = calienta; e.MedidaMs = medida; e.PasoMs = paso;
            return e;
        }

        // Todo en su sitio de partida: sin buscador, atraccion, disco, estado extendido ni asistente; la pagina de la que se partio.
        private void DiagNeutral(DiagCtx c)
        {
            if (asistenteVisible) CierraAsistente();
            if (searchOpen) CloseSearch();
            if (attractOn) ExitAttract();
            if (panelSongs != null && panelSongs.Visible) BackToAlbums();
            if (bancoReposoActivo) QuitaEscena(EscReposo);
            currentLetter = c.Bk.Letra; page = c.Bk.Pagina; selAlbumIndex = c.Bk.Sel;
            RefreshAlbums();
            DiagRebase(c);
        }

        private string DiagSinDiscos()
        {
            List<Album> l = CurrentList();
            return (l == null || l.Count == 0) ? "no hay discos en esta letra de la biblioteca" : null;
        }

        private int DiagPaginas()
        {
            List<Album> l = CurrentList();
            return l == null ? 0 : Math.Max(1, (l.Count + PageSize - 1) / PageSize);
        }

        private string DiagNotaMusica(DiagCtx c) { return c.ConMusica ? "" : "OJO: sin musica; las barras del espectro son un latido de mentira."; }

        // Efectos a Alto, ahorros fuera, estrella 3D: el peor caso de aspecto. Lo que habia queda en c.Bk (se devuelve en Limpia y al final).
        private void DiagPeorCaso()
        {
            EfectoGrafico[] ef = Efectos();
            for (int i = 0; i < ef.Length; i++) PonNivel(ef[i].Clave, NivAlto);
            for (int i = 0; i < NombreAhorro.Length; i++) if (LeeAhorro(i)) PonAhorro(i, false);
            if (!estrella3d) { estrella3d = true; AplicaEfectosGpu(); }
        }
        private void DiagDevuelveEfectos(DiagCtx c)
        {
            EfectoGrafico[] ef = Efectos();
            for (int i = 0; i < ef.Length && i < c.Bk.Niv.Length; i++) if (LeeNivel(ef[i].Clave) != c.Bk.Niv[i]) PonNivel(ef[i].Clave, c.Bk.Niv[i]);
            for (int i = 0; i < c.Bk.Ahorro.Length; i++) if (LeeAhorro(i) != c.Bk.Ahorro[i]) PonAhorro(i, c.Bk.Ahorro[i]);
            if (estrella3d != c.Bk.Estrella3d) { estrella3d = c.Bk.Estrella3d; AplicaEfectosGpu(); }
        }

        // El estado extendido necesita algo «sonando». Con la cancion de un cliente es esa; si no, la del diagnostico (nowPlaying propio).
        private string DiagParaReposo(DiagCtx c, int minMs)
        {
            DiagAseguraMusica(c, minMs);
            if (!c.ConMusica || nowPlaying == null || nowPlaying.IsVideo) return "no hay cancion (ni la de un cliente ni una de la biblioteca) para que el estado extendido tenga que mostrar";
            return null;
        }

        private string DiagNotaReposo()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(auroraAmount > .004f ? "Aurora encendida." : "OJO: la aurora esta en Bajo en los ajustes (no hay aurora en esta escena).");
            sb.Append(lunaOn ? (estrella3d ? " Estrella 3D." : " Estrella 2D.") : " OJO: el astro principal esta apagado (Detalles): no se dibuja la estrella.");
            try
            {
                string t = DiagTitulo(nowPlaying);
                sb.Append(" Titulo de ").Append(t.Length).Append(" caracteres").Append(t.Length > 36 ? " (largo: se desliza)." : " (cabe sin deslizar; no se inventaron titulos largos).");
            }
            catch { }
            return sb.ToString();
        }

        private void DiagCambiaCancion(DiagCtx c, int n)
        {
            if (c.MusicaPropia && c.Items.Length > 1)
            {
                // Como un relevo de verdad en lo que ve la gente (RefrescaTrasRelevo): otra cancion «suena», el color del genero se
                // mueve y el panel de estado se rehace. El SONIDO no se toca (no hay reapertura de MP3 en el hilo de la pantalla).
                c.Cambio = (c.Cambio + 1) % c.Items.Length;
                sonoAntes = nowPlaying;
                nowPlaying = c.Items[c.Cambio];
                RefrescaTrasRelevo();
            }
            else if (c.Items.Length > 1)
            {
                // Con la cancion de un cliente solo se mueve el COLOR del genero (su cancion no se cambia).
                c.Cambio = (c.Cambio + 1) % c.Items.Length;
                PlayItem it = c.Items[c.Cambio];
                ApplyGenreBackground(it.Song, it.Disc);
            }
        }

        // Cuatro canciones de discos distintos, con generos distintos si los hay (lee su etiqueta ahora, fuera de la ventana).
        private void DiagPreparaItems(DiagCtx c)
        {
            List<PlayItem> l = new List<PlayItem>();
            List<string> generos = new List<string>();
            if (c.ItemPropio != null) { l.Add(c.ItemPropio); }
            else if (nowPlaying != null && !nowPlaying.IsVideo && nowPlaying.Song != null) l.Add(nowPlaying);
            for (int i = 0; i < l.Count; i++) { try { l[i].Song.EnsureTags(); } catch { } generos.Add(l[i].Song.Genre ?? ""); }
            Random az = new Random();
            for (int intento = 0; intento < 60 && l.Count < 4; intento++)
            {
                if (tracks.Count == 0) break;
                Track t = tracks[az.Next(tracks.Count)];
                Album d = DiagDiscoDe(t); if (d == null) continue;
                bool repetido = false;
                for (int i = 0; i < l.Count; i++) if (object.ReferenceEquals(l[i].Song, t) || object.ReferenceEquals(l[i].Disc, d)) repetido = true;
                if (repetido) continue;
                try { t.EnsureTags(); } catch { }
                string g = t.Genre ?? "";
                bool generoNuevo = g.Length > 0 && !generos.Contains(g);
                if (!generoNuevo && intento < 40 && l.Count > 1) continue;   // primero se buscan generos distintos
                PlayItem it = new PlayItem(); it.IsVideo = false; it.Song = t; it.Disc = d;
                l.Add(it); generos.Add(g);
            }
            c.Items = l.ToArray(); c.Cambio = 0;
        }

        private List<DiagEscenaDef> DiagEscenas(DiagCtx c)
        {
            List<DiagEscenaDef> l = new List<DiagEscenaDef>();
            DiagEscenaDef e;
            DiagVacio neutral = delegate { DiagNeutral(c); };

            e = DiagDef("rejilla", "Rejilla quieta", "La pared de discos con una pagina, sin tocar nada: fondo, aurora, reloj y la franja de abajo.", 1500, 8000, 0);
            e.Prepara = delegate { DiagNeutral(c); DiagAseguraMusica(c, 9500); return DiagSinDiscos(); };
            e.Notas = delegate { return DiagNotaMusica(c); };
            e.Limpia = neutral; l.Add(e);

            e = DiagDef("hojeo_vistas", "Hojeo rapido (portadas ya vistas)", "Pasar de pagina adelante y atras sin parar (cada 170 ms), por seis paginas: las portadas ya estan en la memoria.", 1000, 10000, 170);
            e.Prepara = delegate { DiagNeutral(c); DiagAseguraMusica(c, 11000); c.HojeoDir = 1; return DiagSinDiscos() ?? (DiagPaginas() < 2 ? "esta letra solo tiene una pagina de discos" : null); };
            e.Paso = delegate (int n)
            {
                int tope = Math.Min(DiagPaginas() - 1, c.Bk.Pagina + 5);
                if (c.HojeoDir > 0 && page >= tope) c.HojeoDir = -1;
                if (c.HojeoDir < 0 && page <= c.Bk.Pagina) c.HojeoDir = 1;
                if (c.HojeoDir > 0) NextPage(); else PrevPage();
            };
            e.Notas = delegate { return DiagNotaMusica(c); };
            e.Limpia = neutral; l.Add(e);

            e = DiagDef("hojeo_frio", "Hojeo rapido (portadas frias)", "Letra tras letra y pagina tras pagina cada 140 ms, sin vaciar ninguna cache: lo que no se haya visto aun se lee de verdad.", 500, 12000, 140);
            e.Prepara = delegate { DiagNeutral(c); DiagAseguraMusica(c, 12500); return DiagSinDiscos() ?? (letterAvailable.Count < 2 ? "solo hay una letra en la biblioteca" : null); };
            e.Paso = delegate (int n) { if ((n % 3) == 2) NextPage(); else ChangeLetter(1); };
            e.Notas = delegate { return DiagNotaMusica(c) + " Las portadas «frias» son las que no estaban ya en la memoria de esta sesion; no se borro ninguna cache. Mirar la linea Portadas."; };
            e.Limpia = neutral; l.Add(e);

            e = DiagDef("letras", "Cambio rapido de letra", "Letra siguiente y anterior cada 380 ms, como alguien con prisa.", 700, 8000, 380);
            e.Prepara = delegate { DiagNeutral(c); DiagAseguraMusica(c, 9000); return DiagSinDiscos() ?? (letterAvailable.Count < 2 ? "solo hay una letra en la biblioteca" : null); };
            e.Paso = delegate (int n) { ChangeLetter(((n & 7) < 4) ? 1 : -1); };
            e.Notas = delegate { return DiagNotaMusica(c); };
            e.Limpia = neutral; l.Add(e);

            e = DiagDef("buscador", "Buscador escribiendo", "Abre el buscador y escribe y borra palabras sin parar (cada 140 ms): cada letra rehace la lista y la textura de la pantalla.", 700, 10000, 140);
            e.Prepara = delegate
            {
                DiagNeutral(c); DiagAseguraMusica(c, 11000);
                if (searchOverlay == null) return "esta rocola no tiene buscador montado";
                OpenSearch(); DiagRebase(c); return null;
            };
            e.Paso = delegate (int n)
            {
                if (searchOverlay == null) return;
                string[] palabras = { "SABADO", "AMOR", "ROCK", "NOCHE" };
                string w = palabras[(n / 16) % palabras.Length];
                int k = n % 16;
                if (k < 8) searchOverlay.TypeChar(w[k % w.Length]); else searchOverlay.Backspace();
            };
            e.Notas = delegate { return DiagNotaMusica(c); };
            e.Limpia = neutral; l.Add(e);

            e = DiagDef("disco_entrar_salir", "Entrar y salir de discos", "Entra a un disco (zoom) y sale cada 380 ms: la animacion del disco y la lista de canciones.", 700, 10000, 380);
            e.Prepara = delegate { DiagNeutral(c); DiagAseguraMusica(c, 11000); return DiagSinDiscos(); };
            e.Paso = delegate (int n)
            {
                if (panelSongs != null && panelSongs.Visible) BackToAlbums();
                else
                {
                    List<Album> lista = CurrentList();
                    if (lista != null && lista.Count > 0)
                    {
                        int gi = selAlbumIndex; if (gi < 0 || gi >= lista.Count) gi = 0;
                        EnterAlbum(lista[gi]);
                    }
                }
            };
            e.Notas = delegate { return DiagNotaMusica(c); };
            e.Limpia = neutral; l.Add(e);

            e = DiagDef("disco_lista", "Dentro de un disco (lista quieta)", "Un disco abierto con su lista de canciones y el cubo girando, sin tocar nada.", 1200, 8000, 0);
            e.Prepara = delegate
            {
                DiagNeutral(c); DiagAseguraMusica(c, 9500);
                string sin = DiagSinDiscos(); if (sin != null) return sin;
                List<Album> lista = CurrentList();
                int gi = selAlbumIndex; if (gi < 0 || gi >= lista.Count) gi = 0;
                EnterAlbum(lista[gi]);
                return (panelSongs != null && panelSongs.Visible) ? null : "no se pudo abrir un disco";
            };
            e.Notas = delegate { return DiagNotaMusica(c); };
            e.Limpia = neutral; l.Add(e);

            e = DiagDef("atraccion", "Pantalla de atraccion", "El escaparate de la rocola cuando nadie la usa: cubo, fichas, mensajes y lo mas pedido.", 1500, 10000, 0);
            e.Prepara = delegate { DiagNeutral(c); DiagAseguraMusica(c, 11500); EnterAttract(); DiagRebase(c); return attractOn ? null : "no se pudo entrar a la atraccion"; };
            e.Notas = delegate { return DiagNotaMusica(c); };
            e.Limpia = neutral; l.Add(e);

            e = DiagDef("reposo_2d", "Estado extendido, estrella 2D", "El panel de estado en grande con la aurora, el reloj, la cola y textos; la estrella plana.", 1500, 12000, 0);
            e.AsentarMaxMs = 6000; e.MotivoNoAsienta = "no se pudo abrir el estado extendido";
            e.Prepara = delegate
            {
                DiagNeutral(c); string sin = DiagParaReposo(c, 20000); if (sin != null) return sin;
                if (estrella3d) { estrella3d = false; AplicaEfectosGpu(); }
                PonEscena(EscReposo); DiagRebase(c); return null;
            };
            e.PorIteracion = delegate { gpuPanel.IdleExpandTarget = true; };
            e.Asentada = delegate { return gpuPanel.EstadoExtendidoAsentado; };
            e.Notas = delegate { return DiagNotaReposo(); };
            e.Limpia = delegate { DiagNeutral(c); if (estrella3d != c.Bk.Estrella3d) { estrella3d = c.Bk.Estrella3d; AplicaEfectosGpu(); } }; l.Add(e);

            e = DiagDef("reposo_3d", "Estado extendido, estrella 3D", "Lo mismo con la estrella en 3D girando (la de fabrica).", 1500, 12000, 0);
            e.AsentarMaxMs = 6000; e.MotivoNoAsienta = "no se pudo abrir el estado extendido";
            e.Prepara = delegate
            {
                DiagNeutral(c); string sin = DiagParaReposo(c, 20000); if (sin != null) return sin;
                if (!estrella3d) { estrella3d = true; AplicaEfectosGpu(); }
                PonEscena(EscReposo); DiagRebase(c); return null;
            };
            e.PorIteracion = delegate { gpuPanel.IdleExpandTarget = true; };
            e.Asentada = delegate { return gpuPanel.EstadoExtendidoAsentado; };
            e.Notas = delegate { return DiagNotaReposo(); };
            e.Limpia = delegate { DiagNeutral(c); if (estrella3d != c.Bk.Estrella3d) { estrella3d = c.Bk.Estrella3d; AplicaEfectosGpu(); } }; l.Add(e);

            e = DiagDef("reposo_transicion", "Entrar y salir del estado extendido", "El panel de estado entra y sale (cada 2,6 s): la transicion entre la pared de discos y el estado extendido, cuatro veces.", 0, 20800, 2600);
            e.Prepara = delegate
            {
                DiagNeutral(c); string sin = DiagParaReposo(c, 22000); if (sin != null) return sin;
                PonEscena(EscReposo); gpuPanel.IdleExpandTarget = false; c.TransT0 = Environment.TickCount; DiagRebase(c); return null;
            };
            // El latido de la rocola (Form1.cs) pone IdleExpandTarget cada vez: el sube y baja se manda en cada vuelta, por reloj.
            e.PorIteracion = delegate { gpuPanel.IdleExpandTarget = ((unchecked(Environment.TickCount - c.TransT0) / 2600) % 2) == 0; };
            e.Notas = delegate { return DiagNotaReposo() + " Cada entrada y salida marca los cuadros con la bandera transicion_del_estado."; };
            e.Limpia = neutral; l.Add(e);

            e = DiagDef("cambio_cancion", "Cambio de cancion (color y panel)", "Otra cancion «suena» cada 3,5 s: la transicion de color del genero y el repintado del panel de estado.", 1000, 20000, 3500);
            e.AsentarMaxMs = 6000; e.MotivoNoAsienta = "no se pudo abrir el estado extendido";
            e.Prepara = delegate
            {
                DiagNeutral(c);
                if (c.MusicaPropia) { string sin = DiagParaReposo(c, 22000); if (sin != null) return sin; }
                else if (!c.ConMusica) return "sin cancion: no hay nada que cambiar (ni la de un cliente ni una de la biblioteca)";
                DiagPreparaItems(c);
                if (c.Items.Length < 2) return "la biblioteca no tiene dos canciones con disco para cambiar entre ellas";
                if (c.MusicaPropia) PonEscena(EscReposo);   // con la cancion propia se ve el panel de estado cambiando
                DiagRebase(c); return null;
            };
            e.Asentada = delegate { return !c.MusicaPropia || gpuPanel.EstadoExtendidoAsentado; };
            e.PorIteracion = delegate { if (c.MusicaPropia) gpuPanel.IdleExpandTarget = true; };
            e.Paso = delegate (int n) { DiagCambiaCancion(c, n); };
            e.Notas = delegate
            {
                return c.MusicaPropia ? "Con la cancion del diagnostico: cambia lo que «suena» como un relevo (RefrescaTrasRelevo: color del genero, panel de estado, etiquetas); el SONIDO no se reabre."
                                      : "Con musica de un cliente: solo se mueve el color del genero (su cancion no se cambia y el panel de estado no cambia de cancion).";
            };
            e.Limpia = delegate
            {
                try
                {
                    if (c.MusicaPropia && c.ItemPropio != null) { sonoAntes = nowPlaying; nowPlaying = c.ItemPropio; RefrescaTrasRelevo(); }
                    else if (nowPlaying != null && nowPlaying.Song != null) ApplyGenreBackground(nowPlaying.Song, nowPlaying.Disc);
                }
                catch { }
                DiagNeutral(c);
            };
            l.Add(e);

            e = DiagDef("asistente", "Asistente (estrella) y su guia", "La estrella saluda, explica y senala (cada 1,8 s) y arranca la guia paso a paso; se cierra sola al acabar.", 1000, 12000, 1800);
            e.Prepara = delegate
            {
                DiagNeutral(c);
                string motivo = MotivoSinAyuda(); if (motivo != null) return "la ayuda no esta disponible ahora: " + motivo;
                AbreAsistente(); DiagRebase(c);
                return asistenteVisible ? null : "no se pudo abrir el asistente";
            };
            e.Paso = delegate (int n)
            {
                if (!asistenteVisible) return;
                asistenteActividadTick = Environment.TickCount;   // que no se despida por inactividad en medio
                AsistenteGestoGpu[] gestos = { AsistenteGestoGpu.Saludar, AsistenteGestoGpu.Explicar, AsistenteGestoGpu.Buscar, AsistenteGestoGpu.Senalar, AsistenteGestoGpu.Confirmar };
                if (n == 1) EmpiezaMision(MisionCancion); else AnimaAsistente(gestos[n % gestos.Length]);
            };
            e.Notas = delegate { return DiagNotaMusica(c) + " La guia se cierra al acabar la escena; no queda nada abierto."; };
            e.Limpia = delegate { try { if (asistenteVisible) CierraAsistente(); asistenteMision = 0; } catch { } DiagNeutral(c); }; l.Add(e);

            e = DiagDef("avisos_ficha", "Avisos y ficha del disco", "Un aviso nuevo cada 0,7 s (cortos y largos) y la seleccion moviendose entre discos, que enseña la ficha rapida.", 800, 10000, 700);
            e.Prepara = delegate { DiagNeutral(c); DiagAseguraMusica(c, 11000); return DiagSinDiscos(); };
            e.Paso = delegate (int n)
            {
                string[] textos = { "Aviso corto", "Cancion agregada a la cola: puesto 3, faltan 8 minutos", "Inserta una moneda", "Esa ya va seguida: eligela de otro disco o espera tu turno" };
                ShowToast(textos[n % textos.Length], (n % 3) == 2 ? Color.FromArgb(200, 130, 35) : Color.FromArgb(40, 165, 90));
                MoveInPage(1);
            };
            e.Notas = delegate { return DiagNotaMusica(c) + (fichaDisco ? " Ficha rapida del disco encendida." : " OJO: la ficha del disco esta apagada en los ajustes (ficha_disco=0)."); };
            e.Limpia = neutral; l.Add(e);

            e = DiagDef("peor_navegando", "PEOR CASO navegando", "Efectos en Alto, sin ahorros, estrella 3D, musica con toda la cadena, y hojeo por letras y paginas frias con avisos mientras se leen portadas.", 1500, 14000, 150);
            e.Prepara = delegate { DiagNeutral(c); DiagAseguraMusica(c, 16000); DiagPeorCaso(); DiagRebase(c); return DiagSinDiscos(); };
            e.Paso = delegate (int n)
            {
                switch (n % 4)
                {
                    case 0: ChangeLetter(1); break;
                    case 1: NextPage(); break;
                    case 2: MoveInPage(1); break;
                    default: if ((n % 8) == 3) ShowToast("Peor caso: aviso", Color.FromArgb(40, 165, 90)); break;
                }
            };
            e.Notas = delegate { return "Ajustes puestos para esta escena: todos los efectos en Alto, ningun ahorro de Performance, estrella 3D (despues se devuelven los de la rocola). " + DiagNotaMusica(c); };
            e.Limpia = delegate { DiagDevuelveEfectos(c); DiagNeutral(c); }; l.Add(e);

            e = DiagDef("peor_reposo", "PEOR CASO en reposo", "Estado extendido con la estrella 3D, efectos en Alto sin ahorros, cambios de cancion y avisos mientras suena la musica.", 1500, 16000, 3500);
            e.AsentarMaxMs = 6000; e.MotivoNoAsienta = "no se pudo abrir el estado extendido";
            e.Prepara = delegate
            {
                DiagNeutral(c); string sin = DiagParaReposo(c, 24000); if (sin != null) return sin;
                DiagPreparaItems(c);
                DiagPeorCaso(); PonEscena(EscReposo); DiagRebase(c); return null;
            };
            e.PorIteracion = delegate { gpuPanel.IdleExpandTarget = true; };
            e.Asentada = delegate { return gpuPanel.EstadoExtendidoAsentado; };
            e.Paso = delegate (int n)
            {
                DiagCambiaCancion(c, n);
                ShowToast((n % 2) == 0 ? "Peor caso: aviso" : "Peor caso: otro aviso mas largo para repintar el cartel", Color.FromArgb(40, 165, 90));
            };
            e.Notas = delegate { return DiagNotaReposo() + " Efectos en Alto, sin ahorros (despues se devuelven los de la rocola)."; };
            e.Limpia = delegate
            {
                try
                {
                    if (c.MusicaPropia && c.ItemPropio != null) { sonoAntes = nowPlaying; nowPlaying = c.ItemPropio; RefrescaTrasRelevo(); }
                    else if (nowPlaying != null && nowPlaying.Song != null) ApplyGenreBackground(nowPlaying.Song, nowPlaying.Disc);
                }
                catch { }
                DiagNeutral(c); DiagDevuelveEfectos(c);
            };
            l.Add(e);
            return l;
        }
    }
}
#endif
