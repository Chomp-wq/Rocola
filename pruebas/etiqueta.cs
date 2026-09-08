using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

// DE QUE GENERO ES UNA CANCION QUE NO TRAE GENERO.
//
// EL FALLO QUE SE ARREGLA: en un disco de Black Eyed Peas todas las canciones salian marcadas "Pop", pero al
// ponerlas la pastilla cambiaba. La lista ensenaba el genero que MANDA en el disco (sacado de las pocas
// canciones que si traen etiqueta) y, en cuanto una sonaba, se apuntaba GenreLabel(null) = "Otros" encima y
// eso PISABA lo deducido. La lista decia una cosa y el reproductor otra.
//
// LO QUE SE COMPRUEBA AQUI:
//   1) que la respuesta es UNA sola, la misma en la lista, en el buscador y al sonar
//   2) que la cascada busca en el orden que dice: cancion -> disco -> artista -> nombre
//   3) que lo del NOMBRE no se pasa de listo (Metallica no es Metal, Discoteca no es Disco)
//   4) que si no hay NADA de donde tirar, no se inventa una etiqueta
class Etiqueta
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF, TAlb, TTrack;
    static object F;
    static MethodInfo mGen, mNombre, mArt;

    static object Campo(string n)
    { return TF.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(F); }
    static void PonCampo(string n, object v)
    { TF.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(F, v); }

    static object NuevoDisco(string carpeta, string nombre, string artista, int nTracks)
    {
        object a = FormatterServices.GetUninitializedObject(TAlb);
        TAlb.GetField("Folder").SetValue(a, carpeta);
        TAlb.GetField("Name").SetValue(a, nombre);
        TAlb.GetField("Artist").SetValue(a, artista);
        IList lt = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TTrack));
        for (int i = 0; i < nTracks; i++)
        {
            object t = FormatterServices.GetUninitializedObject(TTrack);
            TTrack.GetField("Path").SetValue(t, carpeta + System.IO.Path.DirectorySeparatorChar + "c" + i + ".mp3");
            lt.Add(t);
        }
        TAlb.GetField("Tracks").SetValue(a, lt);
        return a;
    }
    static object NuevaCancion(string ruta, string genero)
    {
        object t = FormatterServices.GetUninitializedObject(TTrack);
        TTrack.GetField("Path").SetValue(t, ruta);
        if (genero != null) TTrack.GetField("Genre").SetValue(t, genero);
        return t;
    }
    static string Gen(object tk, object alb) { return (string)mGen.Invoke(F, new object[] { tk, alb }); }

    static void PonDiscos(params object[] discos)
    {
        IList l = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TAlb));
        foreach (object a in discos) l.Add(a);
        PonCampo("albums", l);
    }
    static IDictionary DiscTag() { return (IDictionary)Campo("discTag"); }
    static IDictionary ArtTag() { return (IDictionary)Campo("artistTag"); }
    static IDictionary SongGen() { return (IDictionary)Campo("songGenre"); }

    static byte[] Une(params byte[][] partes)
    {
        int n = 0; for (int i = 0; i < partes.Length; i++) n += partes[i].Length;
        byte[] r = new byte[n]; int p = 0;
        for (int i = 0; i < partes.Length; i++) { Array.Copy(partes[i], 0, r, p, partes[i].Length); p += partes[i].Length; }
        return r;
    }
    static byte[] SyncSafe(int n)
    { return new byte[] { (byte)((n >> 21) & 127), (byte)((n >> 14) & 127), (byte)((n >> 7) & 127), (byte)(n & 127) }; }
    static byte[] Grande(int n)
    { return new byte[] { (byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n }; }
    static byte[] Unsync(byte[] b)
    {
        List<byte> r = new List<byte>(b.Length + 8);
        for (int i = 0; i < b.Length; i++)
        {
            r.Add(b[i]);
            if (b[i] == 0xff && i + 1 < b.Length && (b[i + 1] == 0x00 || b[i + 1] >= 0xe0)) r.Add(0x00);
        }
        return r.ToArray();
    }
    static byte[] Marco(int major, string id, byte[] cuerpo, byte formato)
    {
        byte[] h = new byte[10];
        Array.Copy(Encoding.ASCII.GetBytes(id), 0, h, 0, 4);
        byte[] tam = major >= 4 ? SyncSafe(cuerpo.Length) : Grande(cuerpo.Length);
        Array.Copy(tam, 0, h, 4, 4); h[9] = formato;
        return Une(h, cuerpo);
    }
    static void EscribeId3Complejo(string ruta, int major)
    {
        byte[] titulo = Une(new byte[] { 0 }, Encoding.GetEncoding(28591).GetBytes("Titulo \u00ff\u00e0 final"));
        byte[] genero = Une(new byte[] { 0 }, Encoding.ASCII.GetBytes("Rock"));
        byte[] extendida = major >= 4
            ? new byte[] { 0, 0, 0, 6, 1, 0 }       // v2.4: el tamano INCLUYE estos cuatro bytes
            : new byte[] { 0, 0, 0, 6, 0, 0, 0, 0, 0, 0 }; // v2.3: el tamano NO los incluye
        byte[] cuerpo;
        byte flags;
        if (major >= 4)
        {
            byte[] tituloGuardado = Unsync(titulo);
            cuerpo = Une(extendida, Marco(major, "TIT2", tituloGuardado, 0x02), Marco(major, "TCON", genero, 0));
            flags = 0x40;                            // cabecera extendida; el TIT2 lleva su propia unsync
        }
        else
        {
            byte[] limpio = Une(extendida, Marco(major, "TIT2", titulo, 0), Marco(major, "TCON", genero, 0));
            cuerpo = Unsync(limpio);                 // v2.3 des-sincroniza el cuerpo ENTERO
            flags = 0xc0;                            // unsync + cabecera extendida
        }
        byte[] cab = new byte[10]; cab[0] = (byte)'I'; cab[1] = (byte)'D'; cab[2] = (byte)'3';
        cab[3] = (byte)major; cab[5] = flags; Array.Copy(SyncSafe(cuerpo.Length), 0, cab, 6, 4);
        File.WriteAllBytes(ruta, Une(cab, cuerpo, new byte[160]));
    }

    static object LeeTrack(string ruta)
    {
        object t = FormatterServices.GetUninitializedObject(TTrack);
        TTrack.GetField("Path").SetValue(t, ruta);
        foreach (string n in new string[] { "Title", "Artist", "Album", "Genre" }) TTrack.GetField(n).SetValue(t, "");
        TTrack.GetMethod("EnsureTags").Invoke(t, null);
        return t;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "Album") TAlb = t;
            if (t.Name == "Track") TTrack = t;
        }
        Check(TAlb != null && TTrack != null, "el disco y la cancion estan donde tienen que estar");
        if (TAlb == null || TTrack == null) return 1;
        F = FormatterServices.GetUninitializedObject(TF);
        mGen = TF.GetMethod("GeneroDeCancion", BindingFlags.Instance | BindingFlags.NonPublic);
        mNombre = TF.GetMethod("GeneroPorNombre", BindingFlags.Instance | BindingFlags.NonPublic);
        mArt = TF.GetMethod("RecalculaGenerosArtista", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mGen != null && mNombre != null && mArt != null, "la cascada de genero esta donde tiene que estar");
        if (mGen == null) return 1;
        // Los diccionarios son 'readonly' y se crean en el constructor, que aqui no corre: se ponen a mano.
        foreach (string n in new string[] { "discTag", "artistTag", "nombreTag", "songGenre" })
            TF.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic)
              .SetValue(F, Activator.CreateInstance(typeof(Dictionary<string, string>), new object[] { StringComparer.OrdinalIgnoreCase }));

        Console.WriteLine("\n  0) ID3 2.3 Y 2.4: CABECERA EXTENDIDA Y DES-SINCRONIZACION\n");
        MethodInfo mSolo = TTrack.GetMethod("SoloGenero", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Check(mSolo != null && TTrack.GetMethod("EnsureTags") != null, "estan los dos lectores de etiquetas: completo y genero rapido");
        string dirId3 = Path.Combine(Path.GetTempPath(), "rocola_id3_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dirId3);
        try
        {
            foreach (int major in new int[] { 3, 4 })
            {
                string mp3 = Path.Combine(dirId3, "id3v2" + major + ".mp3");
                EscribeId3Complejo(mp3, major);
                string rapido = (string)mSolo.Invoke(null, new object[] { mp3 });
                object completo = LeeTrack(mp3);
                string tit = (string)TTrack.GetField("Title").GetValue(completo);
                string gen = (string)TTrack.GetField("Genre").GetValue(completo);
                Check(rapido == "Rock" && gen == "Rock", "ID3v2." + major + " encuentra TCON despues de la cabecera extendida");
                Check(tit == "Titulo \u00ff\u00e0 final", "ID3v2." + major + " revierte la des-sincronizacion sin correr los marcos");
            }
        }
        finally { try { Directory.Delete(dirId3, true); } catch { } }

        string raiz = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "etiqprueba");
        string carpBep = System.IO.Path.Combine(raiz, "Black Eyed Peas - The END");
        string carpBep2 = System.IO.Path.Combine(raiz, "Black Eyed Peas - Monkey Business");

        Console.WriteLine("\n  1) EL FALLO DE VERDAD: LO QUE DICE LA LISTA Y LO QUE DICE AL SONAR\n");
        object bep = NuevoDisco(carpBep, "The END", "Black Eyed Peas", 15);
        PonDiscos(bep);
        DiscTag()[carpBep] = "Pop";                      // el paseo de fondo saco "Pop" de las que si traen etiqueta
        object sinTag = NuevaCancion(carpBep + "\\c3.mp3", null);
        string enLista = Gen(sinTag, bep);
        Console.WriteLine("     en la lista del disco: \"" + enLista + "\"");
        Check(enLista == "Pop", "una cancion sin etiqueta hereda el genero de su disco");

        // ASI ERA ANTES: al sonar se apuntaba GenreLabel(null) = "Otros" y eso pisaba lo deducido.
        SongGen()[carpBep + "\\c3.mp3"] = "Otros";
        string comoEraAntes = Gen(sinTag, bep);
        SongGen().Remove(carpBep + "\\c3.mp3");
        Console.WriteLine("     como salia ANTES al ponerla: \"" + comoEraAntes + "\"   (ese era el fallo)");
        Check(comoEraAntes != enLista, "confirmado: apuntar \"Otros\" al sonar cambiaba la respuesta");

        // Y ASI ES AHORA: al sonar solo se apunta si el archivo trae etiqueta DE VERDAD.
        MethodInfo mPlay = TF.GetMethod("PlayNext", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mPlay != null, "el reproductor esta donde tiene que estar");
        string alSonar = Gen(sinTag, bep);
        Console.WriteLine("     al ponerla AHORA:      \"" + alSonar + "\"");
        Check(alSonar == enLista, "la lista y el reproductor dicen LO MISMO");

        // Y si la cancion SI trae etiqueta, manda la suya sobre la del disco.
        object conTag = NuevaCancion(carpBep + "\\c4.mp3", "Hip Hop");
        Console.WriteLine("     una que SI trae etiqueta: \"" + Gen(conTag, bep) + "\"");
        Check(Gen(conTag, bep) == "Hip-Hop", "la etiqueta de la propia cancion manda sobre la del disco");

        Console.WriteLine("\n  2) EL ARTISTA: UN DISCO SIN NINGUNA ETIQUETA\n");
        // Monkey Business no tiene NI UNA cancion etiquetada, asi que su disco no vota. Pero el artista si.
        object bep2 = NuevoDisco(carpBep2, "Monkey Business", "Black Eyed Peas", 12);
        PonDiscos(bep, bep2);
        mArt.Invoke(F, null);
        Console.WriteLine("     votos por artista: " + ArtTag().Count + "   ->  Black Eyed Peas = \"" + ArtTag()["Black Eyed Peas"] + "\"");
        Check(ArtTag().Count == 1 && (string)ArtTag()["Black Eyed Peas"] == "Pop",
              "el genero del artista sale de los discos suyos que SI se saben (sin abrir ni un archivo mas)");
        object sinNada = NuevaCancion(carpBep2 + "\\c1.mp3", null);
        Console.WriteLine("     una cancion de ese disco: \"" + Gen(sinNada, bep2) + "\"");
        Check(Gen(sinNada, bep2) == "Pop", "un disco sin ninguna etiqueta hereda la del artista (antes se quedaba sin nada)");

        // El disco pesa MAS que el artista: si el disco si vota, manda el disco.
        DiscTag()[carpBep2] = "Hip-Hop";
        Check(Gen(sinNada, bep2) == "Hip-Hop", "y si el disco si tiene genero propio, ese manda sobre el del artista");
        DiscTag().Remove(carpBep2);

        // Cada disco pesa lo que canciones tiene: uno suelto de 2 temas no le gana a uno de 20.
        string carpX = System.IO.Path.Combine(raiz, "Black Eyed Peas - Rarezas");
        object bepx = NuevoDisco(carpX, "Rarezas", "Black Eyed Peas", 2);
        DiscTag()[carpX] = "Metal";
        PonDiscos(bep, bep2, bepx);
        mArt.Invoke(F, null);
        Console.WriteLine("     con un disco raro de 2 temas marcado Metal: \"" + ArtTag()["Black Eyed Peas"] + "\"");
        Check((string)ArtTag()["Black Eyed Peas"] == "Pop", "un disco suelto de dos temas no le gana a uno de quince");
        DiscTag().Remove(carpX);

        Console.WriteLine("\n  3) EL NOMBRE DE LA CARPETA (media biblioteca de bar ya lo dice)\n");
        ((IDictionary)Campo("nombreTag")).Clear();
        string[,] casos = {
            { "Cumbias Mix 2020",        "Regional" },
            { "Salsa Clasica",           "Tropical" },
            { "Baladas Romanticas",      "Bolero"   },
            { "Banda 2019",              "Regional" },
            { "Corridos Perrones",       "Regional" },
            { "Reggaeton Viejito",       "Reggaeton"},
            { "Rock en Espanol",         "Rock"     },
            { "Los Angeles Azules",      ""         },   // un nombre de grupo no dice ningun genero
            { "Metallica - Black Album", ""         },   // NO es Metal: "metal" va dentro de "Metallica"
            { "La Discoteca de Papa",    ""         },   // NO es Disco: va dentro de "Discoteca"
            { "Poporopo",                ""         },   // NO es Pop
            { "Grandes Exitos",          ""         },
        };
        bool bienNom = true;
        for (int i = 0; i < casos.GetLength(0); i++)
        {
            string carp = System.IO.Path.Combine(raiz, casos[i, 0]);
            object a = NuevoDisco(carp, casos[i, 0], "Varios", 5);
            string got = (string)mNombre.Invoke(F, new object[] { a });
            bool ok = (got == casos[i, 1]);
            if (!ok) bienNom = false;
            Console.WriteLine(string.Format("     {0,-28} -> {1,-12} {2}", casos[i, 0], (got.Length > 0 ? "\"" + got + "\"" : "(nada)"), ok ? "" : "  <<< esperaba \"" + casos[i, 1] + "\""));
        }
        Check(bienNom, "el nombre solo cuenta cuando la palabra esta ENTERA: ni Metallica es Metal ni Discoteca es Disco");

        // La carpeta de ENCIMA tambien vale: "...\\Cumbias\\Los Angeles Azules"
        string padre = System.IO.Path.Combine(raiz, "Cumbias");
        object hijo = NuevoDisco(System.IO.Path.Combine(padre, "Los Angeles Azules"), "Los Angeles Azules", "Los Angeles Azules", 10);
        Check((string)mNombre.Invoke(F, new object[] { hijo }) == "Regional",
              "y si el genero esta en la carpeta de encima, tambien se lee");

        Console.WriteLine("\n  4) SI NO HAY DE DONDE TIRAR, NO SE INVENTA\n");
        ((IDictionary)Campo("nombreTag")).Clear();
        DiscTag().Clear(); ArtTag().Clear();
        object mudo = NuevoDisco(System.IO.Path.Combine(raiz, "Grandes Exitos"), "Grandes Exitos", "Desconocido", 8);
        PonDiscos(mudo);
        string nada = Gen(NuevaCancion(System.IO.Path.Combine(raiz, "Grandes Exitos") + "\\c1.mp3", null), mudo);
        Console.WriteLine("     sin etiqueta, sin disco, sin artista y sin nombre: " + (nada.Length == 0 ? "(sin pastilla)" : "\"" + nada + "\""));
        Check(nada.Length == 0, "sin ninguna pista no sale pastilla: mejor sin etiqueta que con una falsa");
        Check(Gen(null, null).Length == 0, "y preguntar por una cancion que no existe no revienta");

        Console.WriteLine("\n  5) LA MISMA RESPUESTA PARA EL BUSCADOR\n");
        // No hace falta armar la interfaz entera: basta con ver que hay MAS DE UN sitio preguntandole a la
        // misma funcion. Antes el interior del disco y el buscador tenian cada uno su propia respuesta, y de
        // ahi salia que dijeran cosas distintas.
        int usos = 0;
        foreach (Type t in asm.GetTypes())
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                foreach (string q in Codigo.Nombres(m))
                    if (q == "GeneroDeCancion") usos++;
        Console.WriteLine("     sitios que preguntan por la cascada: " + usos);
        Check(usos >= 2, "la lista del disco y el buscador preguntan a la MISMA funcion (por eso ya no se contradicen)");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
