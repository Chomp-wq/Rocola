// =====================================================================================
//  OPTIMIZACIONES PARA Form1.cs  (rocola)
// =====================================================================================
//  Tu Form1.cs son ~2700 lineas. En vez de reescribirlo entero (riesgo de un error de
//  transcripcion en codigo que YA funciona, y aqui no hay compilador para verificarlo),
//  aqui van SOLO los 3 bloques que cambian, COMPLETOS y listos para pegar sobre los
//  metodos del mismo nombre en tu Form1.cs. Cada uno reemplaza 1:1 a su version actual.
//
//  Este archivo NO se compila (esta excluido en Rocola.csproj). Es solo la referencia.
//  Despues de aplicar los 3 cambios, compila y prueba como siempre.
//
//  Resumen:
//    OPT #1  PlayTrack       -> evita recursion (posible StackOverflow) en rachas de fallos
//    OPT #2  Score + RecomputeResults -> deja de asignar cadenas por cada candidato del indice
//    OPT #4  Cube.Draw       -> reutiliza los 3 arreglos en vez de asignarlos por cuadro
//
//  (OPT #3 y #5: ver notas al final; no incluidos por riesgo / bajo impacto.)
// =====================================================================================


// -------------------------------------------------------------------------------------
//  OPT #1  ->  Reemplaza el metodo  PlayTrack(Track t)  COMPLETO.
//
//  Cambio: la unica linea distinta es el "PlayNext();" del catch, que ahora va por
//  BeginInvoke (igual que ya haces en StartVideoItem). Asi, si varias canciones
//  seguidas no abren, no se apilan llamadas PlayTrack->PlayNext->PlayTrack...
// -------------------------------------------------------------------------------------
private void PlayTrack(Track t)
{
    try
    {
        reader = new AudioFileReader(t.Path);
        reader.Volume = FixedVolume;
        WaveOut wo = new WaveOut();
        wo.DesiredLatency = 1000;
        wo.NumberOfBuffers = 5;
        output = wo;
        output.PlaybackStopped += Output_PlaybackStopped;
        output.Init(new SampleToWaveProvider16(new LevelTap(reader, this)));
        output.Play();
        progressTimer.Start();
    }
    catch (Exception ex)
    {
        lblStatus.Text = "No se pudo reproducir " + Path.GetFileName(t.Path) + " (" + ex.Message + ")";
        LogError("PlayTrack " + Path.GetFileName(t.Path), ex);
        DisposePlaybackNow();
        // === OPTIMIZACION 1 ===  antes: PlayNext();  (recursion sincrona en cadena de fallos)
        try { if (this.IsHandleCreated) this.BeginInvoke((MethodInvoker)delegate { PlayNext(); }); } catch { }
    }
}


// -------------------------------------------------------------------------------------
//  OPT #2  ->  Reemplaza  Score(...)  Y  RecomputeResults(...)  COMPLETOS.
//
//  Cambio: " " + q  y  q.Split(' ')  se calculaban DENTRO de Score, que corre por CADA
//  entrada de searchIndex (decenas de miles) en CADA tecla. Ahora se calculan una sola
//  vez por tecla en RecomputeResults y se pasan a Score. El puntaje es identico.
// -------------------------------------------------------------------------------------
private static int Score(string cand, string q, string spaceQ, string[] toks)
{
    if (q.Length == 0) return 0;
    if (cand == q) return 10000;
    if (cand.StartsWith(q, StringComparison.Ordinal)) return 6000 - Math.Min(cand.Length, 2000);
    int wi = cand.IndexOf(spaceQ, StringComparison.Ordinal);          // === OPTIMIZACION 2 === (antes: " " + q)
    if (wi >= 0) return 4000 - wi;
    int idx = cand.IndexOf(q, StringComparison.Ordinal);
    if (idx >= 0) return 2000 - Math.Min(idx, 1500);
    if (toks != null)                                                 // === OPTIMIZACION 2 === (antes: q.Split(' ') aqui)
    {
        int sc = 0, real = 0;
        for (int i = 0; i < toks.Length; i++)
        {
            string tk = toks[i];
            if (tk.Length == 0) continue;
            real++;
            int at = cand.IndexOf(tk, StringComparison.Ordinal);
            if (at < 0) return 0;
            bool wordStart = (at == 0) || (cand[at - 1] == ' ');
            sc += (wordStart ? 300 : 150) - Math.Min(at, 100);
        }
        if (real >= 2) return 800 + sc;
    }
    return 0;
}

private void RecomputeResults(string query)
{
    MarkSearchActivity();
    if (libBuilding)
    {
        searchOverlay.Results = new List<SearchItem>();
        searchOverlay.Invalidate();
        return;
    }
    string q = Normalize(query);
    List<SearchItem> res = new List<SearchItem>();
    if (q.Length > 0)
    {
        string spaceQ = " " + q;                                      // === OPTIMIZACION 2 === (una vez por tecla)
        string[] toks = (q.IndexOf(' ') >= 0) ? q.Split(' ') : null;  // === OPTIMIZACION 2 ===
        List<SearchItem> source =
            (lastQuery != null && lastQuery.Length > 0 && lastMatches != null && q.StartsWith(lastQuery, StringComparison.Ordinal))
            ? lastMatches : searchIndex;

        List<SearchItem> matches = new List<SearchItem>();
        for (int i = 0; i < source.Count; i++)
        {
            int sc = Score(source[i].Key, q, spaceQ, toks);           // === OPTIMIZACION 2 === (pasa spaceQ/toks)
            if (sc > 0) { source[i].Score = sc; matches.Add(source[i]); }
        }
        matches.Sort(delegate (SearchItem a, SearchItem b)
        {
            if (b.Score != a.Score) return b.Score - a.Score;
            if (a.Kind != b.Kind) return a.Kind - b.Kind;
            return string.Compare(a.Display, b.Display, StringComparison.OrdinalIgnoreCase);
        });
        lastMatches = matches; lastQuery = q;
        int take = Math.Min(40, matches.Count);
        for (int i = 0; i < take; i++) res.Add(matches[i]);
    }
    else { lastMatches = null; lastQuery = null; }
    searchOverlay.Results = res;
    searchOverlay.Invalidate();
}


// -------------------------------------------------------------------------------------
//  OPT #4  ->  Reemplaza la clase anidada  private static class Cube { ... }  COMPLETA.
//
//  Cambio: ex[4], depth[4] y order[4] se asignaban en CADA llamada a Draw (varias por
//  cuadro: cubo seleccionado, relevo, cubeBox, miniCube, buscador, atraccion). Ahora se
//  reutilizan via [ThreadStatic]. El dibujo es identico.
// -------------------------------------------------------------------------------------
private static class Cube
{
    [ThreadStatic] private static double[] exBuf;     // === OPTIMIZACION 4 ===
    [ThreadStatic] private static double[] depthBuf;  // === OPTIMIZACION 4 ===
    [ThreadStatic] private static int[] orderBuf;     // === OPTIMIZACION 4 ===

    public static void Draw(Graphics g, Rectangle bounds, Image cover, double angle)
    {
        double cx = bounds.X + bounds.Width / 2.0;
        int y0 = bounds.Y;
        int H = bounds.Height;
        double R = bounds.Width / 2.0;
        double DEG = Math.PI / 180.0;

        double[] ex = exBuf ?? (exBuf = new double[4]);          // === OPTIMIZACION 4 === (antes: new double[4])
        double[] depth = depthBuf ?? (depthBuf = new double[4]); // === OPTIMIZACION 4 ===
        int[] order = orderBuf ?? (orderBuf = new int[4]);       // === OPTIMIZACION 4 ===
        for (int k = 0; k < 4; k++) ex[k] = cx + R * Math.Cos(angle + 45 * DEG + k * 90 * DEG);
        for (int k = 0; k < 4; k++) depth[k] = Math.Sin(angle + 90 * DEG + k * 90 * DEG);
        order[0] = 0; order[1] = 1; order[2] = 2; order[3] = 3;  // === OPTIMIZACION 4 === (se reinicia cada llamada)

        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 3 - i; j++)
                if (depth[order[j]] > depth[order[j + 1]]) { int tmp = order[j]; order[j] = order[j + 1]; order[j + 1] = tmp; }

        for (int oi = 0; oi < 4; oi++)
        {
            int k = order[oi];
            if (depth[k] <= 0.0) continue;
            int left = (int)Math.Round(Math.Min(ex[k], ex[(k + 1) % 4]));
            int right = (int)Math.Round(Math.Max(ex[k], ex[(k + 1) % 4]));
            int w = right - left;
            if (w < 1) continue;
            Rectangle rect = new Rectangle(left, y0, w, H);
            if (cover != null) g.DrawImage(cover, rect);
            else using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 70, 110))) g.FillRectangle(b, rect);
            int darken = (int)((1.0 - depth[k]) * 130);
            if (darken > 0) using (SolidBrush d = new SolidBrush(Color.FromArgb(darken, 0, 0, 0))) g.FillRectangle(d, rect);
        }

        double minx = ex[0], maxx = ex[0];
        for (int k = 1; k < 4; k++) { if (ex[k] < minx) minx = ex[k]; if (ex[k] > maxx) maxx = ex[k]; }
        using (Pen edge = new Pen(Color.FromArgb(120, 0, 0, 0), 1))
            g.DrawRectangle(edge, (int)Math.Round(minx), y0, (int)Math.Round(maxx - minx), H - 1);
    }
}


// =====================================================================================
//  NOTAS (no aplicadas aqui, para que decidas):
//
//  OPT #3  Escaneo fresco recorre el disco 2-3 veces.
//    SafeEnumerateAudio camina todo el arbol; SafeEnumerateVideo lo vuelve a caminar
//    completo; y FindCover hace un Directory.GetFiles extra POR carpeta. En HDD con
//    biblioteca grande eso ~triplica el primer escaneo (y cada F5). Lo ideal es UN solo
//    recorrido que, por carpeta, liste los archivos una vez y reparta audio/video/portada.
//    NO lo incluyo porque toca Scanner_DoWork + BuildAlbums + BuildVideoSection +
//    FindCover + el formato del cache de biblioteca, y un error ahi cambiaria que portada
//    o que video se elige. Conviene hacerlo y PROBARLO contra tu biblioteca real.
//
//  OPT #5  Menores:
//    - MusicFolder hace Directory.Exists x4 en CADA acceso a la propiedad: cachear la ruta.
//    - DrainCoverTrash toma el lock cada tick aunque no haya nada (impacto minimo).
//    - Has(...) asigna un params[] por llamada (no esta en bucle caliente).
//    - BuildTopDiscs recorre albums x tracks en cada navegacion dentro de TOP: si tu
//      biblioteca es enorme, conviene cachearlo y solo reconstruir cuando cambien stats.
// =====================================================================================
