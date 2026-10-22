using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;
// MP3 SIN HUECOS ENTRE PISTAS (RellenoMp3.cs).
//
// Cada MP3 lleva al principio el retraso del codificador y al final un relleno; el descompresor anade el suyo. La
// rocola los quita leyendo la etiqueta LAME del primer cuadro y midiendo el retraso del descompresor de cada equipo
// con un MP3 de prueba que lleva dentro (un clic en una muestra conocida). Aqui no hay descompresor de Windows: la
// etiqueta se lee de MP3 de verdad (hechos con ffmpeg/LAME, en esta carpeta) y el descompresor se imita (silencio,
// la musica y el relleno, con el retraso que se le diga). En Windows, la linea "[Sin huecos]" de rocola_error.txt
// dice lo que midio en ese equipo. Y la etiqueta tiene que cuadrar con el archivo: un MP3 unido (copy /b) lleva la de su
// primera parte y se cortaba a la mitad; ahora suena entero, como antes.
class Relleno
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF, TR, TE, TC;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }

    static bool Lee(string ruta, out object e)
    {
        object[] a = { ruta, null };
        bool ok = (bool)TR.GetMethod("Lee", T, null, new Type[] { typeof(string), TE.MakeByRefType() }, null).Invoke(null, a);
        e = a[1]; return ok;
    }
    static bool LeeBytes(byte[] d, out object e)
    {
        object[] a = { d, d.Length, null };
        bool ok = (bool)TR.GetMethod("Lee", T, null, new Type[] { typeof(byte[]), typeof(int), TE.MakeByRefType() }, null).Invoke(null, a);
        e = a[2]; return ok;
    }
    static long Campo(object e, string n) { return Convert.ToInt64(TE.GetField(n, T).GetValue(e)); }
    static long Musica(object e) { return (long)TE.GetProperty("Muestras", T).GetValue(e, null); }

    static void LaEtiqueta()
    {
        Console.WriteLine("\n  1) LA ETIQUETA LAME (MP3 de verdad, hechos con ffmpeg/LAME)\n");
        object e;
        byte[] clip = (byte[])TR.GetMethod("Clip", T).Invoke(null, null);
        Check(LeeBytes(clip, out e) && Campo(e, "Retraso") == 576 && Campo(e, "Relleno") == 1566 && Campo(e, "Cuadros") == 21 && Musica(e) == 22050
              && Campo(e, "Tasa") == 44100, "el MP3 de medir que lleva la rocola: retraso 576, relleno 1566, 21 cuadros, 22.050 muestras de musica");
        // Las tres partes de un disco continuo: la etiqueta dice exactamente lo que duraba cada trozo.
        long[] largo = { 57331, 60676, 58393 };
        bool partes = true;
        for (int k = 0; k < 3; k++) if (!Lee("relleno_p" + k + ".mp3", out e) || Musica(e) != largo[k]) partes = false;
        Check(partes, "las tres pistas del disco continuo de prueba: 57.331, 60.676 y 58.393 muestras, las de verdad");
        Check(Lee("relleno_48k.mp3", out e) && Campo(e, "Tasa") == 48000 && Musica(e) == 24000, "a 48 kHz: 24.000 muestras");
        Check(Lee("relleno_mono.mp3", out e) && Musica(e) == 22050, "en mono (otro tamano de la informacion lateral)");
        Check(Lee("relleno_vbr.mp3", out e) && Musica(e) == 22050, "de tasa variable (etiqueta 'Xing' con su tabla)");
        Check(!Lee("relleno_sinetiqueta.mp3", out e), "sin etiqueta: no se recorta");
        Check(!Lee("relleno_mpeg2.mp3", out e), "MPEG-2 (22,05 kHz): no se recorta (solo MPEG-1)");
        // Con un ID3v2 grande delante (una caratula), se encuentra igual.
        string tmp = Path.Combine(Path.GetTempPath(), "rocola_relleno_" + Guid.NewGuid().ToString("N") + ".mp3");
        try
        {
            byte[] mp3 = File.ReadAllBytes("relleno_mono.mp3");
            int tam = 60000;
            byte[] id3 = new byte[10 + tam];
            id3[0] = (byte)'I'; id3[1] = (byte)'D'; id3[2] = (byte)'3'; id3[3] = 3;
            id3[6] = (byte)((tam >> 21) & 0x7F); id3[7] = (byte)((tam >> 14) & 0x7F); id3[8] = (byte)((tam >> 7) & 0x7F); id3[9] = (byte)(tam & 0x7F);
            for (int i = 10; i < id3.Length; i++) id3[i] = (byte)((i * 7) & 0xFF);   // con 0xFF dentro: no debe confundirlo
            using (FileStream fs = File.Create(tmp)) { fs.Write(id3, 0, id3.Length); fs.Write(mp3, 0, mp3.Length); }
            Check(Lee(tmp, out e) && Musica(e) == 22050, "con 60 KB de ID3v2 delante (una caratula) se encuentra igual");
            // Un byte cambiado en la etiqueta: el CRC no cuadra.
            byte[] roto = (byte[])clip.Clone();
            int donde = IndexOf(roto, new byte[] { (byte)'L', (byte)'a', (byte)'v', (byte)'c' }) + 21;
            roto[donde] ^= 0x10;
            Check(!LeeBytes(roto, out e), "con el retraso cambiado a mano (un bit): el CRC no cuadra y no se usa");
            Check(!Lee(Path.Combine(Path.GetTempPath(), "no_existe_" + Guid.NewGuid().ToString("N") + ".mp3"), out e), "un archivo que no existe: falso, sin excepcion");
        }
        finally { try { File.Delete(tmp); } catch { } }
    }
    static int IndexOf(byte[] d, byte[] q)
    {
        for (int i = 0; i + q.Length <= d.Length; i++) { int k = 0; while (k < q.Length && d[i + k] == q[k]) k++; if (k == q.Length) return i; }
        return -1;
    }

    // ---------------- el descompresor imitado ----------------
    // Lo que da un descompresor que retrasa 'extra' muestras: el retraso del codificador en silencio, la musica, y
    // el resto de los cuadros en silencio (los cuadros completos que dice la etiqueta).
    static float[] Descomprime(float[] musica, int ch, object e, int extra)
    {
        long total = Campo(e, "Cuadros") * 1152 + extra;
        float[] x = new float[total * ch];
        long ini = Campo(e, "Retraso") + extra;
        Array.Copy(musica, 0, x, ini * ch, musica.Length);
        return x;
    }
    sealed class Imitado : ISampleProvider
    {
        readonly float[] d; int p; readonly WaveFormat wf;
        public Imitado(float[] x, int ch) { d = x; wf = WaveFormat.CreateIeeeFloatWaveFormat(44100, ch); }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] b, int o, int c) { int n = Math.Min(c, d.Length - p); if (n <= 0) return 0; Array.Copy(d, p, b, o, n); p += n; return n; }
        // Para el recorte: entrega en trozos raros (partiendo cuadros).
        public int ReadRaro(float[] b, int o, int c) { return Read(b, o, Math.Min(c, 777)); }
    }
    static float[] Clic(int ch)
    {
        float[] m = new float[22050 * ch];
        for (int c = 0; c < ch; c++) m[10000 * ch + c] = 0.6f;
        return m;
    }
    static int Extra(float[] x, int ch, out string porque)
    {
        object[] a = { x, x.Length, ch, 576, null };
        int r = (int)TR.GetMethod("ExtraDelClic", T).Invoke(null, a);
        porque = (string)a[4]; return r;
    }
    static void LaMedida()
    {
        Console.WriteLine("\n  2) MEDIR EL DESCOMPRESOR CON EL CLIC\n");
        object e; byte[] clip = (byte[])TR.GetMethod("Clip", T).Invoke(null, null); LeeBytes(clip, out e);
        string porque;
        bool bien = true;
        foreach (int ex in new int[] { 0, 529, 1152 + 529, 4608 })
            if (Extra(Descomprime(Clic(2), 2, e, ex), 2, out porque) != ex) bien = false;
        Check(bien, "encuentra el retraso del descompresor: 0, 529, 1.681 y 4.608 muestras");
        Check(Extra(Descomprime(Clic(1), 1, e, 529), 1, out porque) == 529, "tambien si da un solo canal");
        float[] x = Descomprime(Clic(2), 2, e, 529); x[2 * 20000] = 0.1f;
        Check(Extra(x, 2, out porque) == -1 && porque.Length > 0, "con sonido lejos del clic no se fia (" + porque + ")");
        x = Descomprime(new float[22050 * 2], 2, e, 529);
        Check(Extra(x, 2, out porque) == -1, "sin clic no se fia (" + porque + ")");
        Check(Extra(Descomprime(Clic(2), 2, e, 6000), 2, out porque) == -1, "un retraso de mas de 4 cuadros no se cree (" + porque + ")");
        x = Descomprime(Clic(2), 2, e, 529); float[] corto = new float[(10000 + 576 + 529 + 1000) * 2]; Array.Copy(x, corto, corto.Length);
        Check(Extra(corto, 2, out porque) == -1, "si el descompresor se come el final no se fia (" + porque + ")");
        x = Descomprime(Clic(2), 2, e, 529); x[2 * (10000 + 576 + 529) + 1] = 0f;
        Check(Extra(x, 2, out porque) == -1, "si los dos canales no coinciden no se fia");

        // La medida de la rocola, con el descompresor imitado: apunta lo que decidio.
        List<string> apuntes = new List<string>();
        TR.GetField("Apunte", T).SetValue(null, (Action<string>)delegate(string s) { apuntes.Add(s); });
        FieldInfo cache = TF.GetField("rellenoExtra", T), abre = TF.GetField("AbreClip", T);
        try
        {
            foreach (int ex in new int[] { 529, -1 })
            {
                cache.SetValue(null, int.MinValue); apuntes.Clear();
                int esperado = ex;
                Func<byte[], ISampleProvider> f = delegate(byte[] b)
                {
                    object et; LeeBytes(b, out et);
                    float[] y = Descomprime(Clic(2), 2, et, 529);
                    if (esperado < 0) y[2 * 20000] = 0.3f;   // uno que no se deja medir
                    return new Imitado(y, 2);
                };
                abre.SetValue(null, f);
                int r = (int)TF.GetMethod("RellenoExtra", T).Invoke(null, null);
                int r2 = (int)TF.GetMethod("RellenoExtra", T).Invoke(null, null);
                bool apunto = apuntes.Count == 1 && apuntes[0].Contains(ex >= 0 ? "en uso" : "NO se usa");
                Check(r == ex && r2 == ex && apunto, ex >= 0 ? "la rocola mide 529 una sola vez por sesion y apunta \"en uso\"" : "si no se deja medir, apunta \"NO se usa\" y no recorta (-1)");
            }
        }
        finally { cache.SetValue(null, int.MinValue); abre.SetValue(null, null); TR.GetField("Apunte", T).SetValue(null, null); }
        // Un fallo pasajero no es para toda la sesion (22-10-2026): pasados unos minutos se mide UNA vez mas; si vuelve a fallar, no mas.
        {
            FieldInfo reint = TF.GetField("rellenoReintentado", T), espera = TF.GetField("RellenoReintentoMs", T);
            MethodInfo rel = TF.GetMethod("RellenoExtra", T);
            int intentos = 0; bool falla = true;
            Func<byte[], ISampleProvider> g = delegate(byte[] b)
            {
                intentos++;
                object et; LeeBytes(b, out et);
                float[] y = Descomprime(Clic(2), 2, et, 529);
                if (falla) y[2 * 20000] = 0.3f;
                return new Imitado(y, 2);
            };
            int guardada = (int)espera.GetValue(null);
            try
            {
                cache.SetValue(null, int.MinValue); reint.SetValue(null, false); abre.SetValue(null, g);
                espera.SetValue(null, 60000);
                int a = (int)rel.Invoke(null, null), b2 = (int)rel.Invoke(null, null);
                Check(a == -1 && b2 == -1 && intentos == 1, "un fallo al medir no se repite de inmediato (dentro de los minutos de espera sigue en -1 sin volver a medir)");
                espera.SetValue(null, 0); falla = false;
                int c = (int)rel.Invoke(null, null), d = (int)rel.Invoke(null, null);
                Check(c == 529 && d == 529 && intentos == 2, "pasada la espera lo intenta una vez mas y, si ahora si, se queda con 529 (" + c + ", " + intentos + " intentos)");
                // Y si el reintento tambien falla, se acabo: ni un tercer intento en toda la sesion.
                cache.SetValue(null, int.MinValue); reint.SetValue(null, false); intentos = 0; falla = true; espera.SetValue(null, 0);
                int e1 = (int)rel.Invoke(null, null), e2 = (int)rel.Invoke(null, null), e3 = (int)rel.Invoke(null, null), e4 = (int)rel.Invoke(null, null);
                Check(e1 == -1 && e2 == -1 && e3 == -1 && e4 == -1 && intentos == 2, "si el reintento tambien falla, no hay un tercero (" + intentos + " intentos en cuatro llamadas)");
            }
            finally { cache.SetValue(null, int.MinValue); reint.SetValue(null, false); abre.SetValue(null, null); espera.SetValue(null, guardada); }
        }
    }

    // ---------------- el recorte ----------------
    static float[] Pieza(int desde, int hasta)
    {
        float[] x = new float[(hasta - desde) * 2];
        for (int i = desde; i < hasta; i++)
        {
            float v = (float)(Math.Floor((0.5 * Math.Sin(2 * Math.PI * 220 * i / 44100.0) + 0.2 * Math.Sin(2 * Math.PI * 1375 * i / 44100.0)) * 32767) / 32768.0);
            x[2 * (i - desde)] = v; x[2 * (i - desde) + 1] = v;
        }
        return x;
    }
    static List<float> Une(bool recorta, int extra, bool raro)
    {
        int[] cortes = { 0, 57331, 118007, 176400 };
        List<float> todo = new List<float>();
        for (int k = 0; k < 3; k++)
        {
            object e; Lee("relleno_p" + k + ".mp3", out e);
            Imitado dec = new Imitado(Descomprime(Pieza(cortes[k], cortes[k + 1]), 2, e, extra), 2);
            Func<float[], int, int, int> lee = raro ? (Func<float[], int, int, int>)dec.ReadRaro : dec.Read;
            object rec = recorta ? Activator.CreateInstance(TC, T, null, new object[] { lee, 2, Campo(e, "Retraso") + extra, Musica(e) }, null) : null;
            MethodInfo mlee = TC.GetMethod("Lee", T);
            float[] b = new float[1000];
            while (true)
            {
                int n = recorta ? (int)mlee.Invoke(rec, new object[] { b, 0, b.Length }) : lee(b, 0, b.Length);
                if (n <= 0) break;
                for (int i = 0; i < n; i++) todo.Add(b[i]);
            }
        }
        return todo;
    }
    static void ElRecorte()
    {
        Console.WriteLine("\n  3) EL RECORTE: UN DISCO CONTINUO PARTIDO EN TRES MP3\n");
        float[] orig = Pieza(0, 176400);
        List<float> sin = Une(false, 529, false);
        int hueco = (sin.Count - orig.Length) / 2;
        Check(hueco > 3000, "sin recorte, el disco sale " + hueco + " muestras mas largo (" + (hueco / 44.1).ToString("0") + " ms de silencio repartidos en el principio y las uniones)");
        List<float> con = Une(true, 529, false);
        bool igual = con.Count == orig.Length; for (int i = 0; igual && i < orig.Length; i++) if (con[i] != orig[i]) igual = false;
        Check(igual, "con recorte, el disco sale IDENTICO al original, muestra a muestra: sin huecos en las uniones");
        List<float> raro = Une(true, 529, true);
        igual = raro.Count == orig.Length; for (int i = 0; igual && i < orig.Length; i++) if (raro[i] != orig[i]) igual = false;
        Check(igual, "igual si el descompresor entrega trozos que parten cuadros (777 muestras)");
        List<float> otro = Une(true, 1152 + 529, false);
        igual = otro.Count == orig.Length; for (int i = 0; igual && i < orig.Length; i++) if (otro[i] != orig[i]) igual = false;
        Check(igual, "igual con un descompresor que retrasa un cuadro mas (lo que mida el equipo es lo que se quita)");
        // Tras un salto se conserva el limite final y se reconstruye cuanto falta.
        object e; Lee("relleno_p0.mp3", out e);
        Imitado d2 = new Imitado(Descomprime(Pieza(0, 57331), 2, e, 529), 2);
        object rec = Activator.CreateInstance(TC, T, null, new object[] { (Func<float[], int, int, int>)d2.Read, 2, Campo(e, "Retraso") + 529, Musica(e) }, null);
        TC.GetMethod("Movido", T).Invoke(rec, new object[] { 0L });
        float[] b = new float[4]; int n = (int)TC.GetMethod("Lee", T).Invoke(rec, new object[] { b, 0, 4 });
        Check(n == 4 && b[0] == Pieza(0, 2)[0] && b[3] == Pieza(0, 2)[3], "volver al principio conserva el recorte inicial");
    }

    static void Conectado()
    {
        Console.WriteLine("\n  4) CONECTADO\n");
        Check(Codigo.Llama(TF.GetMethod("OpenAudio", T), "SinRelleno"), "OpenAudio pone el recorte a los MP3 que suenan");
        MethodInfo sr = TF.GetMethod("SinRelleno", T);
        Check(Codigo.Llama(sr, "Lee") && Codigo.Llama(sr, "RellenoExtra") && Codigo.Llama(sr, "get_SampleRate"),
              "solo con etiqueta valida, a la misma frecuencia que el lector, y con el descompresor medido");
        Check(Codigo.Llama(TF.GetMethod("MideRelleno", T), "AbreMp3") && Codigo.Llama(TF.GetMethod("MideRelleno", T), "ExtraDelClic"),
              "la medida descomprime el clip por el mismo camino que las canciones (AbreMp3)");
        int lee, escribe; Codigo.UsosDeCampo(TF.GetMethod("ArmaCadena", T), "CuadrosMusica", out lee, out escribe);
        Check(lee > 0, "el fundido de salida cuenta con la duracion recortada");
    }

    // ---------------- la etiqueta tiene que cuadrar con el archivo ----------------
    // Un MP3 UNIDO (copy /b a.mp3+b.mp3) lleva la etiqueta de su PRIMERA parte. Aqui el lector es un WAV con la duracion
    // que tendria cada archivo (en Linux no hay descompresor de MP3 de Windows); la etiqueta se lee del MP3 de verdad.
    static void Cuadra()
    {
        Console.WriteLine("\n  5) LA ETIQUETA TIENE QUE CUADRAR CON EL ARCHIVO (un MP3 unido, uno cortado)\n");
        object e; Lee("relleno_p0.mp3", out e);
        object e1; Lee("relleno_p1.mp3", out e1);
        long c0 = Campo(e, "Cuadros"), c1 = Campo(e1, "Cuadros");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_unido_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        FieldInfo cache = TF.GetField("rellenoExtra", T);
        object previo = cache.GetValue(null);
        try
        {
            cache.SetValue(null, 529);   // el descompresor, medido (como en un equipo donde se pudo)
            string unido = Path.Combine(dir, "unido.mp3");
            byte[] a = File.ReadAllBytes("relleno_p0.mp3"), b = File.ReadAllBytes("relleno_p1.mp3");
            byte[] u = new byte[a.Length + b.Length]; Array.Copy(a, u, a.Length); Array.Copy(b, 0, u, a.Length, b.Length);
            File.WriteAllBytes(unido, u);
            object eu; Lee(unido, out eu);
            Check(Campo(eu, "Cuadros") == c0, "el MP3 unido lleva la etiqueta de la primera parte (" + c0 + " cuadros, y tiene " + (c0 + c1 + 1) + ")");
            object f = Codigo.FormSinEncender(TF);
            string sinRelleno = "FuenteSinRelleno";
            long[] cuadros = { c0, c0 + 1, c0 + c1 + 1, c0 / 2 };
            string[] que = { "el MP3 de verdad (lo que dice la etiqueta)", "con un cuadro mas (el lector cuenta el de la etiqueta)",
                             "el MP3 unido: NO se recorta y suena ENTERO", "uno cortado (una descarga a medias): NO se recorta" };
            bool[] recorta = { true, true, false, false };
            for (int k = 0; k < cuadros.Length; k++)
            {
                string wav = Path.Combine(dir, "lector" + k + ".wav");
                int n = (int)(cuadros[k] * 1152);
                using (WaveFileWriter w = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)))
                {
                    float[] x = new float[2 * 4096];
                    for (int i = 0; i < x.Length; i++) x[i] = 0.25f;
                    for (int hecho = 0; hecho < n; hecho += 4096) w.WriteSamples(x, 0, 2 * Math.Min(4096, n - hecho));
                }
                object lector = TF.GetMethod("OpenAudio", T).Invoke(f, new object[] { wav });
                object r = TF.GetMethod("SinRelleno", T).Invoke(f, new object[] { k == 2 ? unido : "relleno_p0.mp3", lector });
                bool esRecorte = r != null && r.GetType().Name == sinRelleno;
                long salen = 0;
                ISampleProvider sp = (ISampleProvider)r; float[] buf = new float[8192]; int m;
                while ((m = sp.Read(buf, 0, buf.Length)) > 0) salen += m;
                ((IDisposable)r).Dispose();
                bool bien = esRecorte == recorta[k] && (recorta[k] || salen == 2L * n);
                Check(bien, que[k] + " (" + (esRecorte ? "recorta" : "no recorta") + ", salen " + salen / 2 + " de " + n + " muestras)");
            }
            object ee = e;
            MethodInfo cu = TR.GetMethod("Cuadra", T);
            Check(!(bool)cu.Invoke(null, new object[] { ee, 0.0 }) && !(bool)cu.Invoke(null, new object[] { ee, double.NaN }),
                  "sin duracion (o una que no es un numero), no cuadra: no se recorta");
            Check(Codigo.Llama(TF.GetMethod("SinRelleno", T), "Cuadra") && Codigo.Llama(TF.GetMethod("SinRelleno", T), "get_TotalTime"),
                  "SinRelleno lo mira con lo que el lector cuenta en el archivo");
        }
        finally { cache.SetValue(null, previo); try { Directory.Delete(dir, true); } catch { } }
    }

    static int Main(string[] args)
    {
        Assembly a = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = a.GetType("Rocola.Form1", true);
        TR = a.GetType("Rocola.RellenoMp3", false); TE = a.GetType("Rocola.EtiquetaLame", false); TC = a.GetType("Rocola.RecorteRelleno", false);
        if (TR == null || TE == null || TC == null) { Check(false, "no esta RellenoMp3"); return 1; }
        try { LaEtiqueta(); } catch (Exception e) { Check(false, "la etiqueta: " + e); }
        try { LaMedida(); } catch (Exception e) { Check(false, "la medida: " + e); }
        try { ElRecorte(); } catch (Exception e) { Check(false, "el recorte: " + e); }
        try { Conectado(); } catch (Exception e) { Check(false, "conectado: " + e); }
        try { Cuadra(); } catch (Exception e) { Check(false, "cuadra: " + (e.InnerException ?? e)); }
        Console.WriteLine(fallos == 0 ? "\n  todo bien (" + oks + " comprobaciones)" : "\n  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}
