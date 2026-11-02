// LA BIBLIOTECA (en XP, Form1.Biblioteca.cs: ScanLibraryCore sin la pantalla). Las mismas reglas de XP, con sus funciones tal cual
// (DeXP.cs): la carpeta "musica" junto a los datos (o hasta tres carpetas mas arriba: /rocola/datos -> /rocola/musica) mas las de
// "dir=" de la configuracion; cada carpeta con canciones es un disco ("Artista - Disco"), las canciones por titulo, los discos por
// artista. Por ahora se recorre el disco cada vez (la cache de la biblioteca de XP, despues).
using System;
using System.Collections.Generic;
using System.IO;

namespace Rocola
{
    public partial class Form1
    {
        private readonly List<Track> tracks = new List<Track>();

        // Un disco, para quien use el motor: artista, nombre y sus canciones (rutas y titulos, en el orden de XP).
        public sealed class Disco
        {
            public string Artista, Nombre, Carpeta, Portada;
            public char Letra;
            public bool Videos;   // un disco de la seccion de videos (sus Rutas son videos)
            public int Minutos = -1;
            public int Pedidas;          // cuantas veces se pidieron sus canciones (rocola_stats.txt), para la atraccion   // lo que dura, si se sabe lo de todas sus canciones (-1: no se sabe; como el letrero de XP, no se inventa)
            public List<string> Rutas = new List<string>();
            public List<string> Titulos = new List<string>();
            public override string ToString() { return string.IsNullOrEmpty(Nombre) ? Artista : Artista + " - " + Nombre; }
        }

        // Recorre la musica y arma los discos. Devuelve los discos en el orden de la pared de XP.
        public List<Disco> LeeBiblioteca()
        {
            List<string> roots = AllMusicRoots();
            tracks.Clear();
            List<string> audio = new List<string>(), vids = new List<string>();
            Dictionary<string, string> covs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            carpetasSaltadas = null;
            for (int r = 0; r < roots.Count; r++) ScanFolders(roots[r], audio, vids, covs);
            AvisaCarpetasSaltadas();
            for (int i = 0; i < audio.Count; i++) tracks.Add(Track.FromFile(audio[i]));
            cachedCovers = covs; cachedVideoPaths = vids;
            BuildAlbums(roots);
            DuracionesQueFaltan(tracks);   // las que aun no tienen duracion, de su cabecera (como el paseo de fondo de XP)
            BuildVideoSection(roots);   // la seccion de los videos (la de XP: Form1.Video.cs)
            cachedCovers = null; cachedVideoPaths = null;
            IndexaBiblioteca();
            BuildSearchIndex();   // el indice del buscador (Buscador.cs)
            List<Disco> r2 = new List<Disco>(albums.Count);
            foreach (Album a in albums)
            {
                Disco d = new Disco(); d.Artista = a.Artist; d.Nombre = a.Name; d.Carpeta = a.Folder; d.Portada = a.CoverPath; d.Letra = a.Letter;
                double seg = 0; bool todas = true;
                foreach (Track t in a.Tracks)
                {
                    d.Rutas.Add(t.Path); d.Titulos.Add(t.ToString());
                    float s1 = SegundosDe(t.Path); if (s1 > 0) seg += s1; else todas = false;
                    int veces; if (songCounts != null && songCounts.TryGetValue(t.Path, out veces)) d.Pedidas += veces;
                }
                if (todas && a.Tracks.Count > 0) d.Minutos = (int)Math.Round(seg / 60.0);
                r2.Add(d);
            }
            foreach (Album a in videoAlbums)
            {
                Disco d = new Disco(); d.Artista = a.Artist; d.Nombre = a.Name; d.Carpeta = a.Folder; d.Portada = a.CoverPath; d.Letra = VideoMark; d.Videos = true;
                foreach (string v in a.VideoFiles) { d.Rutas.Add(v); d.Titulos.Add(Path.GetFileNameWithoutExtension(v)); }
                r2.Add(d);
            }
            return r2;
        }
        public const char LetraVideos = VideoMark;
        public string CarpetaMusica { get { return MusicFolder; } }

        // LO MAS SONADO (BuildTopDiscs de XP): un disco virtual "TOP 100" por genero con lo mas pedido (rocola_stats.txt), en la "letra"
        // especial de XP (la estrella). Se rehace cuando cambian las estadisticas.
        public const char LetraTop = TopMark;
        public List<Disco> LoMasSonado()
        {
            BuildTopDiscs();
            List<Disco> r = new List<Disco>();
            if (topDiscs == null) return r;
            foreach (Album a in topDiscs)
            {
                Disco d = new Disco(); d.Artista = a.Artist; d.Nombre = a.Name; d.Carpeta = a.Folder; d.Letra = a.Letter;
                d.Portada = (a.CycleCovers != null && a.CycleCovers.Count > 0) ? a.CycleCovers[0] : null;
                for (int i = 0; i < a.Tracks.Count; i++)
                {
                    d.Rutas.Add(a.Tracks[i].Path);
                    string info = (a.TrackInfo != null && i < a.TrackInfo.Count) ? a.TrackInfo[i] : null;
                    d.Titulos.Add(a.Tracks[i].ToString() + (string.IsNullOrEmpty(info) ? "" : "   (" + info + ")"));
                }
                r.Add(d);
            }
            return r;
        }
    }
}
