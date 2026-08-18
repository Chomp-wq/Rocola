using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

// EL COLOR DE CADA GENERO.
//
// La rocola le pone color a cada genero musical: en la pastilla que sale dentro del disco, en la del
// buscador, en la de la franja de abajo y en el tinte de cada letra del abecedario. Un genero sin color se
// queda con el azul de por defecto, que es el de "no se de que es esto".
//
// Aqui se comprueban cuatro cosas:
//
//   1) Que NINGUN genero de verdad se quede sin color. Se prueban los que trae un ID3 y los que se leen del
//      nombre de la carpeta, en español y con acentos, que es como estan escritos en una rocola de bar.
//   2) Que las palabras con las que la rocola adivina el genero por el nombre de la carpeta (NombrePal)
//      sirvan TODAS. Una que no lleve a ningun genero es una palabra que no hace nada.
//   3) Que el color sea el MISMO se pregunte por donde se pregunte. Antes habia dos listas de palabras,
//      copia una de la otra, y ya se habian desparejado: "Balada" salia con el color del bolero en la lista
//      del disco y con el azul de por defecto en la barra de letras.
//   4) Que dos generos no compartan un color que no se pueda distinguir. Se mide con la distancia "redmean",
//      que se parece a lo que ve el ojo bastante mas que restar los tres numeros del RGB.
class Colores
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static MethodInfo mLabel, mColor, mCrudo;
    static Color porDefecto;
    static string Etiqueta(string g) { return (string)mLabel.Invoke(null, new object[] { g }); }
    static Color Color1(string et) { return (Color)mColor.Invoke(null, new object[] { et }); }
    static Color ColorCrudo(string g) { return (Color)mCrudo.Invoke(null, new object[] { g }); }

    // Distancia entre dos colores parecida a la que percibe el ojo (redmean). Restar el RGB a secas dice que
    // dos naranjas casi iguales estan "lejos" solo porque cambia mucho el rojo.
    static double Distancia(Color a, Color b)
    {
        double rm = (a.R + b.R) / 2.0;
        double dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
        return Math.Sqrt((2 + rm / 256.0) * dr * dr + 4.0 * dg * dg + (2 + (255 - rm) / 256.0) * db * db);
    }

    // Los generos que de verdad aparecen en una rocola de bar: lo que traen los ID3 y lo que dicen los
    // nombres de las carpetas. Ninguno de estos puede quedarse sin color.
    static readonly string[] DeVerdad = {
        "Cumbia", "Cumbias", "Banda", "Banda Sinaloense", "Norteño", "Nortena", "Mariachi", "Ranchera",
        "Corridos", "Corridos Tumbados", "Duranguense", "Sonidero", "Grupero", "Grupera", "Tejano", "Tejanas",
        "Quebradita", "Sierreño", "Huapango", "Huasteco", "Zapateado", "Regional Mexicano",
        "Salsa", "Merengue", "Bachata", "Vallenato", "Guaracha", "Mambo", "Cha Cha Cha", "Danzon", "Danzón",
        "Marimba", "Son Cubano", "Tropical", "Tropicales", "Timba",
        "Balada", "Baladas", "Balada Romantica", "Romantica", "Romántica", "Románticas", "Bolero", "Boleros",
        "Tango", "Vals", "Serenata", "Flamenco", "Rumba", "Copla",
        "Reggaeton", "Reguetón", "Regueton", "Perreo", "Dembow", "Rap", "Trap", "Hip Hop",
        "Rock", "Rock en Español", "Pop", "Pop Latino", "Metal", "Punk", "Indie", "Alternative", "Grunge",
        "Electronica", "Electrónica", "Tecno", "Techno", "House", "Trance", "Dance", "Dubstep", "Tribal",
        "Reggae", "Ska", "Jazz", "Blues", "Soul", "Funk", "Bossa Nova", "Samba", "Sertanejo",
        "Clasica", "Clásica", "Classical", "Opera", "Ópera", "Instrumental", "Piano", "Orquesta",
        "Gospel", "Cristiana", "Cristiano", "Alabanza", "Navidad", "Navideñas", "Villancicos", "Christmas",
        "Karaoke", "Karaokes", "Infantil", "Infantiles", "Country", "Folk", "Disco", "Oldies", "Soundtrack",
        "Banda Sonora", "K-Pop", "Anime", "Latino", "Latin", "Chill", "Lo-Fi", "Ambient", "Gotico", "Gothic",
        "DnB", "Drum and Bass", "Hablado", "Audiolibro", "Industrial", "World", "Vocal", "Reggaeton",
    };

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        BindingFlags F = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        mLabel = TF.GetMethod("GenreLabel", F);
        mColor = TF.GetMethod("ColorDeEtiqueta", F);
        mCrudo = TF.GetMethod("GenreToColor", F);
        porDefecto = (Color)TF.GetField("DefaultAccent", F).GetValue(null);
        string[] pal = (string[])TF.GetField("NombrePal", F).GetValue(null);

        Console.WriteLine();

        // ---------- 1) ningun genero de verdad sin color ----------
        List<string> sinColor = new List<string>();
        Dictionary<string, bool> etiquetas = new Dictionary<string, bool>();
        foreach (string g in DeVerdad)
        {
            string et = Etiqueta(g);
            if (et != "Otros") etiquetas[et] = true;
            if (et == "Otros" || Color1(et).ToArgb() == porDefecto.ToArgb()) sinColor.Add(g + " -> " + et);
        }
        Console.WriteLine("  " + DeVerdad.Length + " generos de una rocola de bar, en " + etiquetas.Count + " familias de color");
        foreach (string x in sinColor) Console.WriteLine("     sin color:  " + x);
        Check(sinColor.Count == 0, "ninguno de los " + DeVerdad.Length + " generos de verdad se queda sin color ("
              + sinColor.Count + " sin el)");

        // Y cada familia tiene que reconocerse a SI MISMA: si la rocola llama "Gotico" a un genero, escribir
        // "Gotico" tiene que devolver "Gotico". Cuando no pasa es siempre por lo mismo -la lista de palabras
        // esta en ingles y el nombre de la familia en español-, y entonces una carpeta llamada como la propia
        // familia se queda sin color. Asi salieron Gotico, DnB y Hablado.
        List<string> noSeReconocen = new List<string>();
        foreach (string e in new List<string>(etiquetas.Keys)) if (Etiqueta(e) != e) noSeReconocen.Add(e + " -> " + Etiqueta(e));
        foreach (string x in noSeReconocen) Console.WriteLine("     no se reconoce a si misma:  " + x);
        Check(noSeReconocen.Count == 0, "cada familia de genero se reconoce a si misma por su nombre ("
              + noSeReconocen.Count + " no)");

        // ---------- 2) las palabras del nombre de la carpeta sirven todas ----------
        List<string> muertas = new List<string>();
        foreach (string w in pal) if (Etiqueta(w) == "Otros") muertas.Add(w);
        foreach (string x in muertas) Console.WriteLine("     palabra que no lleva a ningun genero:  \"" + x + "\"");
        Check(muertas.Count == 0, "las " + pal.Length + " palabras con las que se adivina el genero por el nombre de la carpeta sirven ("
              + muertas.Count + " no llevan a nada)");

        // ---------- 3) el mismo color se pregunte por donde se pregunte ----------
        int desparejados = 0;
        foreach (string g in DeVerdad)
        {
            Color a = Color1(Etiqueta(g)), b = ColorCrudo(g);
            if (a.ToArgb() != b.ToArgb())
            {
                if (desparejados < 5) Console.WriteLine("     \"" + g + "\": " + a.R + "," + a.G + "," + a.B + " por un lado y "
                                                        + b.R + "," + b.G + "," + b.B + " por el otro");
                desparejados++;
            }
        }
        Check(desparejados == 0, "el color de un genero es el mismo se pregunte por donde se pregunte ("
              + desparejados + " desparejados)");

        // ---------- 4) que no haya dos que no se distingan ----------
        List<string> ets = new List<string>(etiquetas.Keys);
        ets.Sort();
        double peor = double.MaxValue; string pa = "", pb = "";
        List<string> juntas = new List<string>();
        for (int i = 0; i < ets.Count; i++)
            for (int j = i + 1; j < ets.Count; j++)
            {
                double d = Distancia(Color1(ets[i]), Color1(ets[j]));
                if (d < peor) { peor = d; pa = ets[i]; pb = ets[j]; }
                if (d < 45) juntas.Add(ets[i] + " y " + ets[j] + " (" + d.ToString("0") + ")");
            }
        Console.WriteLine();
        Console.WriteLine("  la pareja de colores mas parecida de las " + ets.Count + " familias: "
                          + pa + " y " + pb + ", a " + peor.ToString("0") + " de distancia");
        foreach (string x in juntas) Console.WriteLine("     demasiado juntas:  " + x);
        Check(juntas.Count == 0, "no hay dos generos con colores que no se puedan distinguir (los que no llegan a 45: "
              + juntas.Count + ")");

        // Y el azul de por defecto se queda SOLO para "no se de que es esto": ningun genero puede usarlo.
        int usanDefecto = 0;
        foreach (string e in ets) if (Color1(e).ToArgb() == porDefecto.ToArgb()) usanDefecto++;
        Check(usanDefecto == 0, "el azul de \"no se de que es\" no se lo queda ningun genero");

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}
