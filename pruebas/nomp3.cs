using System;
using System.IO;
using System.Reflection;

// LOS «MP3» QUE NO LO SON (17-11-2026): un M4A/AAC, OGG, FLAC, WMA o WAV con el nombre cambiado a .mp3 sonaba a ruido (BuildCleanMp3 con
// cuadros falsos). NoEsMp3.OtroFormato mira la cabecera y dice que es; OpenAudio y LectorMedida lo rechazan antes de abrir.
// ESTA BATERIA VIGILA: cada formato se reconoce (con y sin etiqueta ID3v2, con pie), un MP3 de verdad y el MP3 dentro de un WAV (0x55)
// dan null, y lo vacio, corto o ilegible tambien (todo como siempre).
class NoMp3
{
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }

    static byte[] Mp3Real()
    {
        byte[] b = new byte[417 * 4];
        for (int i = 0; i < 4; i++) { b[i * 417] = 0xFF; b[i * 417 + 1] = 0xFB; b[i * 417 + 2] = 0x90; b[i * 417 + 3] = 0x64; }
        return b;
    }
    static byte[] Id3(int tam, bool pie)
    {
        byte[] e = new byte[10 + tam + (pie ? 10 : 0)];
        e[0] = (byte)'I'; e[1] = (byte)'D'; e[2] = (byte)'3'; e[3] = 3; e[5] = (byte)(pie ? 0x10 : 0);
        e[6] = (byte)((tam >> 21) & 0x7F); e[7] = (byte)((tam >> 14) & 0x7F); e[8] = (byte)((tam >> 7) & 0x7F); e[9] = (byte)(tam & 0x7F);
        return e;
    }
    static byte[] Une(byte[] a, byte[] b) { byte[] r = new byte[a.Length + b.Length]; Array.Copy(a, r, a.Length); Array.Copy(b, 0, r, a.Length, b.Length); return r; }
    static byte[] Con(int donde, string txt, int largo)
    {
        byte[] b = new byte[largo];
        for (int i = 0; i < txt.Length; i++) b[donde + i] = (byte)txt[i];
        return b;
    }
    static byte[] Wav(int fmt, bool bloqueAntes)
    {
        byte[] extra = bloqueAntes ? new byte[] { (byte)'L', (byte)'I', (byte)'S', (byte)'T', 3, 0, 0, 0, 1, 2, 3, 0 } : new byte[0];   // 3 bytes + relleno
        byte[] f = new byte[8 + 16];
        f[0] = (byte)'f'; f[1] = (byte)'m'; f[2] = (byte)'t'; f[3] = (byte)' '; f[4] = 16; f[8] = (byte)(fmt & 255); f[9] = (byte)(fmt >> 8);
        byte[] cab = Con(0, "RIFF", 12); cab[8] = (byte)'W'; cab[9] = (byte)'A'; cab[10] = (byte)'V'; cab[11] = (byte)'E';
        return Une(Une(Une(cab, extra), f), new byte[64]);
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        Type TN = asm.GetType("Rocola.NoEsMp3", true);
        MethodInfo of = TN.GetMethod("OtroFormato", T), rechaza = TN.GetMethod("Rechaza", T);
        string dir = Path.Combine(Path.GetTempPath(), "rocola_nomp3_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Func<string, byte[], string> mira = delegate(string nom, byte[] datos)
            {
                string r = Path.Combine(dir, nom + ".mp3"); File.WriteAllBytes(r, datos);
                return (string)of.Invoke(null, new object[] { r });
            };
            Console.WriteLine("\n  1) LO QUE NO ES MP3\n");
            Check(mira("m4a", Con(4, "ftypM4A ", 64)) == "M4A/AAC", "ftyp en 4..7 = M4A/AAC");
            Check(mira("ogg", Con(0, "OggS", 64)) == "OGG", "OggS = OGG");
            Check(mira("flac", Con(0, "fLaC", 64)) == "FLAC", "fLaC = FLAC");
            byte[] asf = new byte[64]; asf[0] = 0x30; asf[1] = 0x26; asf[2] = 0xB2; asf[3] = 0x75; asf[4] = 0x8E; asf[5] = 0x66;
            Check(mira("wma", asf) == "WMA", "el GUID de ASF = WMA");
            Check(mira("wav1", Wav(1, false)) == "WAV", "WAV PCM = WAV");
            Check(mira("wav3", Wav(3, false)) == "WAV", "WAV de coma flotante = WAV");
            Check(mira("wavx", Wav(0xFFFE, false)) == "WAV", "WAV extensible = WAV");
            Check(mira("wavl", Wav(1, true)) == "WAV", "WAV con un bloque LIST antes de fmt  = WAV");
            Console.WriteLine("\n  2) CON ETIQUETA ID3v2 DELANTE\n");
            Check(mira("id3m4a", Une(Id3(300, false), Con(4, "ftypM4A ", 64))) == "M4A/AAC", "ID3v2 y despues ftyp: M4A/AAC");
            Check(mira("id3ogg", Une(Id3(200, true), Con(0, "OggS", 64))) == "OGG", "ID3v2 con pie y despues OggS: OGG");
            Check(mira("id3flac", Une(Id3(20000, false), Con(0, "fLaC", 64))) == "FLAC", "ID3v2 de 20000 bytes (tamano synchsafe de dos bytes) y fLaC: FLAC");
            Console.WriteLine("\n  3) LO QUE SIGUE SIENDO COMO SIEMPRE (null)\n");
            Check(mira("mp3", Mp3Real()) == null, "un MP3 de verdad (cabecera FF FB 90 64) = null");
            Check(mira("id3mp3", Une(Id3(300, false), Mp3Real())) == null, "un MP3 con ID3v2 delante = null");
            Check(mira("id3pie", Une(Id3(100, true), Mp3Real())) == null, "un MP3 con ID3v2 con pie = null");
            Check(mira("wav55", Wav(0x55, false)) == null, "WAV con formato 0x55 (MP3 dentro) = null");
            Check(mira("vacio", new byte[0]) == null, "archivo vacio = null");
            Check(mira("tres", new byte[] { 1, 2, 3 }) == null, "archivo de 3 bytes = null");
            Check(mira("soloid3", Id3(500, false)) == null, "solo la etiqueta, sin nada despues = null");
            Check((string)of.Invoke(null, new object[] { Path.Combine(dir, "noexiste.mp3") }) == null, "un archivo que no existe = null (sin lanzar)");
            Check((string)of.Invoke(null, new object[] { null }) == null, "ruta nula = null");
            Console.WriteLine("\n  4) EL RECHAZO\n");
            string rm4a = Path.Combine(dir, "m4a.mp3"), rok = Path.Combine(dir, "mp3.mp3");
            string msg = null;
            try { rechaza.Invoke(null, new object[] { rm4a }); } catch (TargetInvocationException e) { msg = e.InnerException.GetType().Name + ": " + e.InnerException.Message; }
            Check(msg == "InvalidDataException: no es un MP3: es M4A/AAC con otro nombre", "un M4A con nombre .mp3 lanza InvalidDataException con el motivo (" + msg + ")");
            bool lanzo = false;
            try { rechaza.Invoke(null, new object[] { rok }); } catch (TargetInvocationException) { lanzo = true; }
            Check(!lanzo, "un MP3 de verdad no lanza");
            File.WriteAllBytes(Path.Combine(dir, "ogg.wav"), Con(0, "OggS", 64));
            lanzo = false;
            try { rechaza.Invoke(null, new object[] { Path.Combine(dir, "ogg.wav") }); } catch (TargetInvocationException) { lanzo = true; }
            Check(!lanzo, "solo se miran las rutas .mp3 (un .wav no se toca)");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN (" + oks + " comprobaciones)" : "  FALLOS: " + fallos);
        return fallos;
    }
}
