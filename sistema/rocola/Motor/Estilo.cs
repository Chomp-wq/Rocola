// EL COLOR DEL GENERO EN TODO (color_genero= de XP, EstiloGpu.cs) y la letra para leer de lejos (letra_lejos=). Los colores son los de XP
// (ColorDeEtiqueta de Form1.Fondo.cs, tal cual, como 0xRRGGBB): un solo sitio donde vive el color de cada genero (GenreLabel -> color).
using System;
using System.Collections.Generic;

namespace Rocola
{
    public partial class Form1
    {
        public bool ColorGenero = true;   // "color_genero="
        // LA ESTRELLA DEL CIELO (el astro principal de XP) y sus adornos, con las claves de XP (encendidos de fabrica).
        public bool EstrellaCielo = true;   // "luna="
        public bool Estrella3d = true;      // "estrella_3d="
        public bool HaloLunar = true;       // "halo_lunar="
        public bool PolvoLunar = true;      // "polvo_lunar="
        public bool EstelaEstelar = true;   // "estela_estelar="
        public const uint ColorPorDefecto = 0x2D5FCDu;   // el azul de siempre (sin genero)

        // El color del genero de la que suena (0 si no tiene genero).
        public uint ColorSonando { get { string g = SonandoGenero; return string.IsNullOrEmpty(g) ? 0u : ColorDeEtiqueta(GenreLabel(g)); } }
        public string EtiquetaSonando { get { string g = SonandoGenero; return string.IsNullOrEmpty(g) ? "" : GenreLabel(g); } }

        // El genero que manda en un disco (el que mas se repite entre las canciones que traen etiqueta). Se lee UNA vez por disco y en un
        // hilo de fondo, sobre copias de las canciones (LeeEtiquetaAparte de XP: no toca la biblioteca); mientras, "" (sin resplandor).
        private readonly Dictionary<string, string> generoDisco = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // El genero de un disco si ya se leyo (sin pedirlo): "" si no se sabe.
        public string GeneroSabido(Disco d)
        {
            if (d == null || d.Carpeta == null) return "";
            string g; lock (generoDisco) return generoDisco.TryGetValue(d.Carpeta, out g) ? g ?? "" : "";
        }
        public string GeneroDeDisco(Disco d)
        {
            if (d == null || d.Videos || d.Carpeta == null) return "";
            string g;
            lock (generoDisco)
            {
                if (generoDisco.TryGetValue(d.Carpeta, out g)) return g ?? "";
                generoDisco[d.Carpeta] = null;   // pedido: se lee una vez
            }
            List<Track> copias = new List<Track>();
            foreach (string r in d.Rutas) { Track t; if (pistaDeRuta != null && pistaDeRuta.TryGetValue(r, out t) && t != null) copias.Add(t.CopiaEtiqueta()); }
            string carpeta = d.Carpeta;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                Dictionary<string, int> cuenta = new Dictionary<string, int>();
                try   // (un error aqui, en un hilo del grupo, cerraria la rocola entera)
                {
                    foreach (Track c in copias)
                    {
                        Track l = Track.LeeEtiquetaAparte(c, null);
                        if (string.IsNullOrEmpty(l.Genre)) continue;
                        string e = GenreLabel(l.Genre); int n; cuenta.TryGetValue(e, out n); cuenta[e] = n + 1;
                    }
                }
                catch { }
                string mejor = ""; int m = 0;
                foreach (KeyValuePair<string, int> kv in cuenta) if (kv.Value > m) { m = kv.Value; mejor = kv.Key; }
                lock (generoDisco) generoDisco[carpeta] = mejor;
            });
            return "";
        }

        public static uint ColorDeEtiqueta(string etiqueta)
        {
            switch (etiqueta)
            {
                case "Karaoke": return 0x4696F5u; // azul de escenario
                case "Navidad": return 0x7DC846u; // verde pino claro
                case "K-Pop": return 0xAF64EBu; // lila claro
                case "Metal": return 0xD7282Du; // rojo sangre
                case "Punk": return 0xF5551Eu; // naranja encendido
                case "Gotico": return 0x643796u; // violeta oscuro
                case "Industrial": return 0x914150u; // granate apagado
                case "Reggaeton": return 0xC33CBEu; // magenta
                case "Hip-Hop": return 0x7D46D7u; // violeta azulado
                case "Tropical": return 0xFF8228u; // naranja mango
                case "Regional": return 0xD77819u; // naranja tierra
                case "Bolero": return 0xAF5055u; // vino rosado
                case "Flamenco": return 0xC88250u; // arena tostada
                case "Latino": return 0xBE552Du; // terracota
                case "Reggae": return 0x28AF55u; // verde bandera
                case "DnB": return 0x0F9196u; // turquesa oscuro
                case "Electronica": return 0x23C3D2u; // cian brillante
                case "Chill": return 0x5FAFA5u; // verde agua
                case "Jazz": return 0xE1A028u; // ambar
                case "Soul": return 0xB9822Du; // mostaza
                case "Cristiana": return 0xCDC364u; // oro palido
                case "Clasica": return 0x5A64D7u; // azul indigo
                case "Soundtrack": return 0x6E7DC8u; // azul acero
                case "Country": return 0x8C7855u; // caqui
                case "Folk": return 0x78965Au; // verde oliva
                case "Disco": return 0xE14BAFu; // rosa neon
                case "Oldies": return 0xAA7369u; // rosa palo
                case "World": return 0xA05F2Du; // marron
                case "Vocal": return 0xC8AF96u; // crema
                case "Hablado": return 0x878C8Cu; // gris
                case "Infantil": return 0xFFBE37u; // amarillo vivo
                case "Pop": return 0xEB376Eu; // rosa fuerte
                case "Rock": return 0xE15F4Bu; // coral
                default: return ColorPorDefecto;
            }
        }

    }
}
