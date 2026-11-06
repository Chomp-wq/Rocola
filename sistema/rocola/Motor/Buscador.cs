// EL BUSCADOR (en XP, Form1.Biblioteca.cs: BuildSearchIndex y RecomputeResults, sin la pantalla). La busqueda es la de XP tal cual: el
// indice de artistas, discos y canciones, la puntuacion (empieza igual, empieza palabra, en medio, por palabras), la cache por letra
// y, si nada casa, una errata. Devuelve los 40 mejores, en el orden de XP.
using System;
using System.Collections.Generic;

namespace Rocola
{
    public partial class Form1
    {
        // de Form1.Biblioteca.cs (RecomputeResults, sin la pantalla: devuelve los resultados)
        private List<SearchItem> BuscaComoXP(string query)
        {
            if (libBuilding)   // el worker esta llenando searchIndex: no iterarlo (carrera = crash)
            {
                lastQuery = null; lastMatches = null; lastMatchesApproximate = false;
                return new List<SearchItem>();
            }
            string q = MotorBusqueda.Normaliza(query);
            List<SearchItem> res = new List<SearchItem>();
            if (q.Length > 0)
            {
                // OPTIMIZACION (busqueda): partes derivadas de la consulta calculadas UNA vez por tecla,
                // no por cada candidato del indice.
                string[] toks = (q.IndexOf(' ') >= 0) ? q.Split(' ') : null;
                int qMask = MarcaLetras(q);
                // Incremental: si la nueva busqueda EMPIEZA con la anterior, las coincidencias nuevas
                // son un subconjunto de las anteriores -> re-evaluamos solo esas (mucho menos trabajo).
                List<SearchItem> source =
                    (!lastMatchesApproximate && lastQuery != null && lastQuery.Length > 0 && lastMatches != null && q.StartsWith(lastQuery, StringComparison.Ordinal))
                    ? lastMatches : searchIndex;

                // LA PRIMERA LETRA ES LA CARA. Con una sola letra tecleada, la marca de letras no descarta a
                // nadie -casi todas las canciones tienen una "a"- asi que hay que puntuar el indice ENTERO:
                // 3.2 ms de 20.000 entradas, contra 0.07 ms cuando ya hay tres letras. Y es la que mas se
                // nota, porque es la que abre cada busqueda.
                //
                // Como para una letra suelta el resultado es SIEMPRE el mismo mientras no cambie la musica, se
                // guarda: la primera vez que alguien teclea una "a" cuesta lo de siempre, y todas las demas
                // veces de la noche sale de memoria (medido: 10 veces menos, ver pruebas/bolsa.cs). Se guardan
                // tambien los puntos, porque el orden depende de ellos y cada busqueda los pisa.
                //
                // La cache de letras tiene presupuesto agregado y desalojo LRU; no depende
                // de que la biblioteca sea pequena ni de que se use solo el alfabeto latino.
                List<SearchItem> matches = null;
                BolsaLetra bolsa = null;
                if ((q.Length == 1 && (bolsa = FindLetterQuery(q[0])) != null) ||
                    (q.Length > 1 && (bolsa = FindRecentQuery(q)) != null))
                {
                    matches = bolsa.Lista;
                    // Solo 40 puntos son necesarios si el top ya existe. La lista COMPLETA
                    // se conserva para acotar, pero su Score anterior no participa en esa cuenta.
                    for (int i = 0; i < bolsa.Mejores.Count; i++) bolsa.Mejores[i].Score = bolsa.Puntos[i];
                    res.AddRange(bolsa.Mejores);
                    lastMatchesApproximate = bolsa.Approximate;
                }
                else
                {
                    matches = new List<SearchItem>();
                    int sello = ++selloBusqueda;
                    source = SearchCandidates(source, qMask);
                    int cuantos = source.Count;
                    for (int i = 0; i < cuantos; i++)
                    {
                        SearchItem si = source[i];
                        if ((si.Mask & qMask) != qMask) continue;   // le falta alguna letra: imposible que case
                        int sc = Score(si.Key, q, toks);
                        sc = MotorBusqueda.Relevancia(si.Key, si.TitleAt, si.TitleLength, q, sc);
                        if (sc > 0) { si.Score = sc + Popular(si); si.Marca = sello; matches.Add(si); }   // (a igualdad, lo mas pedido primero)
                    }
                    lastMatchesApproximate = false;
                    // Las coincidencias literales siempre tienen preferencia. Una unica errata
                    // solo se intenta si no hay ninguna, nunca al pulsar una letra corta.
                    if (matches.Count == 0 && MotorBusqueda.PermiteErrata(q, toks))
                    {
                        int required = MotorBusqueda.LetrasImprescindibles(q);
                        List<SearchItem> approximateSource = SearchCandidates(searchIndex, required);
                        for (int i = 0; i < approximateSource.Count; i++)
                        {
                            SearchItem si = approximateSource[i];
                            if ((si.Mask & required) != required) continue;
                            int missing = qMask & ~si.Mask;
                            if ((missing & (missing - 1)) != 0) continue;
                            int sc = MotorBusqueda.Aproximado(si.Key, q, toks);
                            sc = MotorBusqueda.RelevanciaAproximada(si.Key, si.TitleAt, si.TitleLength, q, toks, sc);
                            if (sc > 0) { si.Score = sc + Popular(si); si.Marca = sello; matches.Add(si); }
                        }
                        lastMatchesApproximate = matches.Count > 0;
                    }
                    // EL BUSCADOR FLEXIBLE (BuscadorFlexible.cs, 7-11-2026): si lo exacto no llena las 40, lo que casa de oido, con faltas o con
                    // palabras de sobra, debajo de lo exacto (de 100 a 700 puntos: lo exacto empieza en 800).
                    // LA LETRA DE TUS MP3 (L3, BuscaLetras.cs): con una frase, las canciones con casi todas sus palabras en la letra
                    if (!SinFlexible) BuscaEnLetrasMp3(q, matches);
                    if (matches.Count < 40 && !SinFlexible)
                    {
                        int antes = matches.Count;
                        BuscaFlexible(q, matches);
                        if (matches.Count > antes) lastMatchesApproximate = true;   // (la tecla siguiente busca otra vez en todo el indice)
                    }
                    TomaMejores(matches, res, 40);
                    if ((q.Length == 1 && matches.Count <= 131072) || (q.Length <= 128 && matches.Count <= 32768))
                    {
                        BolsaLetra nb = new BolsaLetra();
                        nb.Lista = matches; nb.Mejores = new List<SearchItem>(res); nb.Puntos = new int[res.Count];
                        nb.Approximate = lastMatchesApproximate;
                        for (int i = 0; i < res.Count; i++) nb.Puntos[i] = res[i].Score;
                        if (q.Length == 1) RememberLetterQuery(q[0], nb);
                        else RememberQuery(q, nb);
                    }
                }
                // De todo lo que casa solo se ENSENAN 40. Ordenar la lista entera para tirar el resto era el
                // gasto grande del buscador: con veinte mil canciones y una sola letra tecleada casan veinte
                // mil, y ordenarlas costaba 13 ms POR TECLA. Quedarse con los 40 mejores de una pasada cuesta
                // 0.19 ms. La lista sin ordenar se guarda igual, porque para acotar la busqueda de la tecla
                // siguiente lo unico que importa es QUIENES casan, no en que orden.
                lastMatches = matches; lastQuery = q;
                TotalUltimaBusqueda = matches != null ? matches.Count : res.Count;   // la casilla dice todas (CuentaResultados de XP)
                // La tecla siguiente sigue teniendo TODAS las coincidencias para acotar,
                // no solo las visibles; sus puntos se calculan con la nueva consulta.
            }
            else { lastMatches = null; lastQuery = null; lastMatchesApproximate = false; TotalUltimaBusqueda = 0; }
            return res;
        }

        // Cuantas coincidencias tuvo la ultima busqueda: se ensenan las 40 mejores, pero la casilla del buscador dice todas, como en XP.
        public int TotalUltimaBusqueda { get; private set; }
        internal static readonly bool SinFlexible = Environment.GetEnvironmentVariable("ROCOLA_BUSCA_SIN_FLEXIBLE") == "1";   // (pruebas: medir sin la segunda pasada)
        // Un resultado, para quien use el motor: Tipo 0 = artista (va a su primer disco), 1 = disco, 2 = cancion.
        public sealed class Resultado
        {
            public int Tipo; public string Texto; public string Carpeta; public string Ruta;
            public string Portada, Detalle;   // para el buscador con portadas (buscador_portadas= de XP): su portada y lo que ayuda a elegir
        }
        public bool BuscadorPortadas = true;   // "buscador_portadas=" (XP: BuscadorPortadasGpu.cs)
        // LO MAS PEDIDO HOY, como resultados del buscador (el atajo de XP): las canciones de la noche, de mas a menos.
        public List<Resultado> MasPedidoHoy()
        {
            List<Resultado> r = new List<Resultado>();
            CambiaDeNoche(DateTime.Now);
            foreach (string p in MasPedidasNoche(nocheCuenta, nocheOrden, 12))
            {
                Resultado x = new Resultado { Tipo = 2, Ruta = p, Texto = System.IO.Path.GetFileNameWithoutExtension(p), Detalle = nocheCuenta[p] == 1 ? "pedida 1 vez hoy" : "pedida " + nocheCuenta[p] + " veces hoy" };
                Track t; Album d;
                if (pistaDeRuta != null && pistaDeRuta.TryGetValue(p, out t) && t != null) x.Texto = t.ToString();
                if (discoDeRuta != null && discoDeRuta.TryGetValue(p, out d) && d != null) { x.Portada = d.CoverPath; x.Carpeta = d.Folder; }
                r.Add(x);
            }
            return r;
        }
        // (pruebas: la misma busqueda sin lo que se recuerda de las anteriores, para medir lo que cuesta de verdad)
        public List<Resultado> BuscaSinGuardar(string texto) { lock (valoresDePalabra) valoresDePalabra.Clear(); ClearRecentQueries(); if (bolsaLetra != null) bolsaLetra.Clear(); lastQuery = null; lastMatches = null; return Busca(texto); }
        public List<Resultado> Busca(string texto)
        {
            List<Resultado> r = new List<Resultado>();
            foreach (SearchItem it in BuscaComoXP(texto ?? ""))
            {
                Resultado x = new Resultado(); x.Tipo = it.Kind; x.Texto = it.Display;
                x.Carpeta = it.AlbumRef != null ? it.AlbumRef.Folder : null;
                x.Ruta = it.TrackRef != null ? it.TrackRef.Path : null;
                Album a = it.AlbumRef ?? (it.Albums != null && it.Albums.Count > 0 ? it.Albums[0] : null);
                if (a != null) x.Portada = a.CoverPath;
                // los textos de la tarjeta, como DatosCarta de XP (BuscadorPortadasGpu.cs)
                if (it.Kind == 0)
                {
                    int discos = 0, canciones = 0;
                    if (it.Albums != null) foreach (Album b in it.Albums) if (b != null) { discos++; canciones += b.Tracks.Count; }
                    if (discos == 0 && it.AlbumRef != null) { discos = 1; canciones = it.AlbumRef.Tracks.Count; }
                    if (discos > 0) x.Detalle = discos + (discos == 1 ? " disco" : " discos") + (canciones > 0 ? " · " + canciones + (canciones == 1 ? " canción" : " canciones") : "");
                }
                else if (it.Kind == 1 && a != null)
                {
                    if (!string.IsNullOrEmpty(a.Name)) x.Texto = a.Name;
                    int nc = a.Tracks.Count, nv = a.VideoFiles != null ? a.VideoFiles.Count : 0;
                    string cuenta = nc > 0 ? nc + (nc == 1 ? " canción" : " canciones") : (nv > 0 ? nv + (nv == 1 ? " video" : " videos") : "");
                    x.Detalle = string.IsNullOrEmpty(a.Artist) ? cuenta : (cuenta.Length > 0 ? a.Artist + " · " + cuenta : a.Artist);
                }
                else if (it.Kind == 2)
                {
                    Track t = it.TrackRef;
                    if (t != null && !string.IsNullOrEmpty(t.Title)) x.Texto = TituloSinPista(t.Title);
                    string artista = t != null && !string.IsNullOrEmpty(t.Artist) ? t.Artist : (it.AlbumRef != null ? (it.AlbumRef.Artist ?? "") : "");
                    string dur = t != null ? DurText(t.Path) : "";
                    string extra = dur.Length > 0 ? dur : (it.AlbumRef != null ? (it.AlbumRef.Name ?? "") : "");
                    x.Detalle = artista.Length > 0 ? (extra.Length > 0 ? artista + " · " + extra : artista) : extra;
                }
                if (it.Kind <= 2) r.Add(x);
            }
            return r;
        }
    }
}
