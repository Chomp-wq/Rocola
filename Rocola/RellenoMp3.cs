// SIN HUECOS ENTRE PISTAS DE MP3: el retraso y el relleno del codificador
//
// Un MP3 no empieza ni acaba donde empezaba y acababa la musica. El codificador (LAME, y el de ffmpeg que es LAME)
// mete al principio un RETRASO (576 muestras) y rellena el final hasta completar el ultimo cuadro (hasta 1.151
// muestras mas). El descompresor anade su propio retraso (529 muestras en los habituales). Cada MP3 lleva esas
// cuentas escritas en su primer cuadro, la etiqueta LAME ("Xing"/"Info" mas la extension LAME): el retraso, el
// relleno y cuantos cuadros hay. En un disco continuo (en vivo, mezclas, operas) eso deja entre pista y pista unos
// 40-50 ms de silencio que no estaban en el disco: medido con un tono continuo partido en tres MP3 (pruebas/relleno.cs
// y MEJORAS_AUDIO_Y_PENDIENTES_2026-09-22.md, 1.27), 1.635 muestras casi mudas en cada union.
//
// Lo que no se sabia era cuanto retrasa EL DESCOMPRESOR DE CADA EQUIPO (el de Windows no se puede probar aqui). Asi
// que se MIDE, una vez por sesion y en el propio equipo: la rocola lleva dentro un MP3 de medio segundo con un clic en
// una muestra conocida (ClicEn), lo descomprime por el mismo camino que las canciones (AbreMp3) y busca el clic. Lo
// que se desplazo, menos el retraso del codificador que dice su etiqueta, es el retraso del descompresor. Solo si la
// medida sale limpia (un clic claro, el resto en silencio y un retraso razonable) se recorta; si no, todo sigue como
// antes. Queda apuntado en rocola_error.txt ("[Sin huecos]"), aunque no sea un error.
//
// El recorte (FuenteSinRelleno): se quitan del principio el retraso del codificador mas el del descompresor, y se
// corta al final en la duracion real de la musica. Solo en MP3 de MPEG-1 (32, 44,1 y 48 kHz) con etiqueta LAME
// valida (con su CRC) y que cuadre con lo que hay en el archivo (un MP3 unido lleva la etiqueta de su primera parte: ver
// Cuadra). Los saltos usan coordenadas de la musica y conservan el limite final de la etiqueta.
// Lo recortado es silencio (el del codificador) y el relleno del final: en una cancion suelta no cambia nada que se
// oiga, y en un disco continuo desaparece el hueco.
using System;
using System.IO;

namespace Rocola
{
    // Lo que dice la etiqueta LAME del primer cuadro.
    internal struct EtiquetaLame
    {
        public int Retraso, Relleno, Tasa, PorCuadro;
        public long Cuadros;
        public long Muestras { get { return Cuadros * PorCuadro - Retraso - Relleno; } }   // las de la musica, por canal
    }

    internal static class RellenoMp3
    {
        internal const int ClicEn = 10000, ClicMuestras = 22050, ClicTasa = 44100;   // el MP3 de medir (ClipB64)
        internal const int ExtraMaximo = 4608;   // un descompresor que retrase mas de 4 cuadros no es de fiar

        // ---------------- LEER LA ETIQUETA ----------------
        internal static bool Lee(string ruta, out EtiquetaLame e)
        {
            e = new EtiquetaLame();
            try
            {
                using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    byte[] h = new byte[10];
                    long ini = 0;
                    if (LeeTodo(fs, h, 10) == 10 && h[0] == 'I' && h[1] == 'D' && h[2] == '3')
                    {
                        ini = 10 + ((h[6] & 0x7F) << 21 | (h[7] & 0x7F) << 14 | (h[8] & 0x7F) << 7 | (h[9] & 0x7F));
                        if ((h[5] & 0x10) != 0) ini += 10;   // con pie
                    }
                    if (ini >= fs.Length) return false;
                    fs.Position = ini;
                    byte[] d = new byte[4096];
                    int n = LeeTodo(fs, d, d.Length);
                    return Lee(d, n, out e);
                }
            }
            catch { e = new EtiquetaLame(); return false; }
        }
        // En un trozo que empieza donde deberia empezar el primer cuadro (se busca en los primeros bytes: hay archivos
        // con algo de basura delante). Falso si no hay etiqueta LAME valida.
        internal static bool Lee(byte[] d, int n, out EtiquetaLame e)
        {
            e = new EtiquetaLame();
            for (int i = 0; i + 4 <= n && i < 2048; i++)
            {
                if (d[i] != 0xFF || (d[i + 1] & 0xE0) != 0xE0) continue;
                return Cuadro(d, i, n, out e);   // solo el primer cuadro: la etiqueta va ahi o no va
            }
            return false;
        }
        private static bool Cuadro(byte[] d, int i, int n, out EtiquetaLame e)
        {
            e = new EtiquetaLame();
            int b1 = d[i + 1], b2 = d[i + 2], b3 = d[i + 3];
            if (((b1 >> 3) & 3) != 3 || ((b1 >> 1) & 3) != 1) return false;   // solo MPEG-1 capa III
            int tasaIdx = (b2 >> 2) & 3, brIdx = b2 >> 4;
            if (tasaIdx == 3 || brIdx == 0 || brIdx == 15) return false;
            bool crc = (b1 & 1) == 0, mono = ((b3 >> 6) & 3) == 3;
            int x = i + 4 + (crc ? 2 : 0) + (mono ? 17 : 32);
            if (x + 8 > n) return false;
            bool xing = d[x] == 'X' && d[x + 1] == 'i' && d[x + 2] == 'n' && d[x + 3] == 'g';
            bool info = d[x] == 'I' && d[x + 1] == 'n' && d[x + 2] == 'f' && d[x + 3] == 'o';
            if (!xing && !info) return false;
            int flags = Be32(d, x + 4), p = x + 8;
            if ((flags & 1) == 0) return false;   // sin la cuenta de cuadros no se sabe donde acaba
            if (p + 4 > n) return false;
            long cuadros = (uint)Be32(d, p); p += 4;
            if ((flags & 2) != 0) p += 4;
            if ((flags & 4) != 0) p += 100;
            if ((flags & 8) != 0) p += 4;
            if (p + 36 > n) return false;
            // La extension LAME: el nombre del codificador y, en los bytes 21 a 23, el retraso y el relleno (12 bits cada
            // uno). Se exige el nombre de LAME o de ffmpeg (que usa LAME) y que cuadre su CRC-16: el de los primeros 190
            // bytes del cuadro, con el propio campo del CRC en cero si cae dentro (en mono cae; en estereo va justo detras).
            bool nombre = (d[p] == 'L' && d[p + 1] == 'A' && d[p + 2] == 'M' && d[p + 3] == 'E')
                       || (d[p] == 'L' && d[p + 1] == 'a' && d[p + 2] == 'v' && (d[p + 3] == 'c' || d[p + 3] == 'f'));
            if (!nombre || i + 190 > n) return false;
            int guardado = (d[p + 34] << 8) | d[p + 35];
            byte[] cab = new byte[190];
            Array.Copy(d, i, cab, 0, 190);
            if (p + 34 - i < 190) cab[p + 34 - i] = 0;
            if (p + 35 - i < 190) cab[p + 35 - i] = 0;
            if (Crc16(cab, 0, 190) != guardado) return false;
            int retraso = (d[p + 21] << 4) | (d[p + 22] >> 4), relleno = ((d[p + 22] & 0x0F) << 8) | d[p + 23];
            int[] tasas = { 44100, 48000, 32000 };
            e.Retraso = retraso; e.Relleno = relleno; e.Tasa = tasas[tasaIdx]; e.PorCuadro = 1152; e.Cuadros = cuadros;
            if (cuadros <= 0 || e.Muestras <= 0) { e = new EtiquetaLame(); return false; }
            return true;
        }
        // LA ETIQUETA TIENE QUE CUADRAR CON EL ARCHIVO. Un MP3 UNIDO (copy /b a.mp3+b.mp3, o un programa que pega pistas
        // sin rehacerla) lleva la etiqueta de la PRIMERA parte; uno cortado (una descarga a medias), una que dice mas de lo
        // que hay. Creerla cortaba la cancion pagada donde acababa la primera parte, o ponia el fundido de salida despues
        // del final. Solo vale si lo que el lector cuenta en el archivo ('segundos') es lo que dice, con dos cuadros de
        // margen (hay lectores que cuentan tambien el cuadro de la etiqueta). Si no, suena como antes, sin recortar.
        internal static bool Cuadra(EtiquetaLame e, double segundos)
        {
            if (e.Tasa <= 0 || e.PorCuadro <= 0 || !(segundos > 0)) return false;
            double cuadros = segundos * e.Tasa / e.PorCuadro;
            return Math.Abs(cuadros - e.Cuadros) <= 2.0;
        }
        private static int Be32(byte[] d, int p) { return (d[p] << 24) | (d[p + 1] << 16) | (d[p + 2] << 8) | d[p + 3]; }
        // CRC-16 (polinomio 0x8005 reflejado, como el de la etiqueta LAME).
        internal static int Crc16(byte[] d, int desde, int cuantos)
        {
            int c = 0;
            for (int k = desde; k < desde + cuantos; k++)
            {
                c ^= d[k];
                for (int b = 0; b < 8; b++) c = ((c & 1) != 0) ? (c >> 1) ^ 0xA001 : c >> 1;
            }
            return c;
        }
        private static int LeeTodo(Stream s, byte[] b, int n)
        {
            int hecho = 0;
            while (hecho < n) { int r = s.Read(b, hecho, n - hecho); if (r <= 0) break; hecho += r; }
            return hecho;
        }

        // ---------------- MEDIR EL DESCOMPRESOR ----------------
        // El MP3 de medir: medio segundo en silencio, estereo a 44,1 kHz, con un clic en la muestra ClicEn (hecho con
        // ffmpeg y LAME a 64 kbit/s: ver pruebas/relleno.cs, que comprueba lo que dice su etiqueta).
        internal static byte[] Clip() { return Convert.FromBase64String(ClipB64); }
        // Donde quedo el clic en lo que dio el descompresor ('x' entrelazado, 'n' muestras en total). Devuelve el retraso
        // del descompresor (lo que se desplazo, menos el retraso del codificador del clip) o -1 si la medida no es limpia.
        internal static int ExtraDelClic(float[] x, int n, int canales, int retrasoClip, out string porque)
        {
            porque = "";
            if (x == null || canales < 1 || n < canales) { porque = "no salio nada"; return -1; }
            int cuadros = n / canales, donde = -1;
            float mayor = 0f;
            for (int k = 0; k < cuadros; k++) { float v = Math.Abs(x[k * canales]); if (v > mayor) { mayor = v; donde = k; } }
            if (!(mayor >= 0.2f)) { porque = "no se encontro el clic (lo mas alto: " + mayor.ToString("0.000") + ")"; return -1; }
            for (int k = 0; k < cuadros; k++)
            {
                if (k > donde - 1500 && k < donde + 1500) continue;
                for (int c = 0; c < canales; c++)
                    if (!(Math.Abs(x[k * canales + c]) < 0.02f)) { porque = "hay sonido lejos del clic (en la muestra " + k + ")"; return -1; }
            }
            if (canales > 1 && !(Math.Abs(x[donde * canales + 1] - x[donde * canales]) < 0.05f)) { porque = "los dos canales no coinciden"; return -1; }
            int extra = donde - ClicEn - retrasoClip;
            if (extra < 0 || extra > ExtraMaximo) { porque = "un retraso fuera de lo razonable (" + extra + " muestras)"; return -1; }
            if (cuadros < ClicEn + retrasoClip + extra + 2000) { porque = "el descompresor se come el final"; return -1; }
            return extra;
        }

        // Al archivo de errores, una vez por texto en la sesion (como la decodificacion en coma flotante de HD).
        internal static Action<string> Apunte = null;   // las pruebas lo cambian
        private static readonly System.Collections.Generic.List<string> apuntados = new System.Collections.Generic.List<string>();
        internal static void Apunta(string texto)
        {
            Action<string> a = Apunte;
            if (a != null) { a(texto); return; }
            lock (apuntados) { if (apuntados.Contains(texto)) return; apuntados.Add(texto); }
            try
            {
                Bitacora.ApuntaConSello(Path.Combine(Form1.CarpetaDatos, "rocola_error.txt"), 262144,   // detras (MedidasBitacora.cs)
                                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [Sin huecos]", "\r\n" + texto + "\r\n\r\n");
            }
            catch { }
        }

        internal static readonly string ClipB64 =
            "//tQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAASW5mbwAAAA8AAAAVAAAR9AAXFxcXIiIiIiIuLi4uLjo6Ojo6RUVF" +
            "RVFRUVFRXV1dXV1oaGhoaHR0dHR/f39/f4uLi4uLl5eXl5eioqKirq6urq66urq6usXFxcXF0dHR0d3d3d3d6Ojo6Oj09PT09P//" +
            "//8AAAAATGF2YzYwLjMxAAAAAAAAAAAAAAAAJAYeAAAAAAAAEfTmfXaqAAAAAAAAAAAAAAAAAAAAAP/7UGQAD/AAAGkAAAAIAAAN" +
            "IAAAAQAAAaQAAAAgAAA0gAAABExBTUVVVVVMQU1FMy4xMDBVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVX/+1JkVg/wAABpAAAACAAADSAAAAEAAAGkAAAAIAAANIAA" +
            "AARVVVVVVVVVVUxBTUUzLjEwMFVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVf/7UmSpD/AAAGkAAAAIAAANIAAAAQAAAaQAAAAgAAA0gAAABFVVVVVVVVVVTEFNRTMu" +
            "MTAwVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVV//tSZKkP8AAAaQAAAAgAAA0gAAABAAABpAAAACAAADSAAAAEVVVVVVVVVVVMQU1FMy4xMDBVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVX/+1JkqQ/w" +
            "AABpAAAACAAADSAAAAEAAAGkAAAAIAAANIAAAARVVVVVVVVVVUxBTUUzLjEwMFVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVf/7UmSpD/AAAGkAAAAIAAANIAAAAQAA" +
            "AaQAAAAgAAA0gAAABFVVVVVVVVVVTEFNRTMuMTAwVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVUEFkG9hCJGKydIEw3JAgiRitvznOkAoYXBAkijbpAgyEIZaNG2oKyeSBiyMn1Gjn4IEDC4oJLRtpEBJFAghc0beqCtHSCC" +
            "4rJ5Csn2EIQtckYtHqgoJLFBJk25+CiOdQxcVk6QrJ9UYy5z//tSZKkP8AAAaQAAAAgAAA0gAAABAAABpAAAACAAADSAAAAEYyc9" +
            "UFBIxAVMQU1FMy4xMDBVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVWUB1CFCDBUBiDIVCoQiEAA6Vy3T7OV/q4NM1iV" +
            "X/OWEHT0ClX+B8Am2FW/qsQwhChyw/8mkInhJIMJ9v/8ANg3zzOcW/Fn0GL//0LEwLg4IYWzWavbW///QgXM65y5k7OPNoT6tcf/" +
            "/9DGToYhjIo49M1rj5xa3////Ov/+1JkqQ/wAABpAAAACAAADSAAAAEAAAGkAAAAIAAANIAAAARD2dk1H/FQVUxBTUUzLjEwMFVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVf/7UmTiA/AAAGkAAAAIAAANIAAAAQ4xIRcUlIAIAAA0goAABFVVVVVVVVVVTEFNRTMuMTAwVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV//tSZOqABBZC3f5l" +
            "4AQAAA0gwAAAAAABpBwAACAAADSDgAAEVVVVVVVVVVVMQU1FMy4xMDBVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVX/+1JkqQ/wAABpAAAACAAADSAAAAEAAAGkAAAA" +
            "IAAANIAAAARVVVVVVVVVVUxBTUUzLjEwMFVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVf/7UmSpD/AAAGkAAAAIAAANIAAAAQAAAaQAAAAgAAA0gAAABFVVVVVVVVVV" +
            "TEFNRTMuMTAwVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVV//tSZKkP8AAAaQAAAAgAAA0gAAABAAABpAAAACAAADSAAAAEVVVVVVVVVVVMQU1FMy4xMDBVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVX/" +
            "+1JkqQ/wAABpAAAACAAADSAAAAEAAAGkAAAAIAAANIAAAARVVVVVVVVVVUxBTUUzLjEwMFVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVf/7UmSpD/AAAGkAAAAIAAAN" +
            "IAAAAQAAAaQAAAAgAAA0gAAABFVVVVVVVVVVTEFNRTMuMTAwVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV//tSZKkP8AAAaQAAAAgAAA0gAAABAAABpAAAACAAADSA" +
            "AAAEVVVVVVVVVVVMQU1FMy4xMDBVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVX/+1JkqQ/wAABpAAAACAAADSAAAAEAAAGkAAAAIAAANIAAAARVVVVVVVVVVUxBTUUz" +
            "LjEwMFVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVf/7UmSpD/AAAGkAAAAIAAANIAAAAQAAAaQAAAAgAAA0gAAABFVVVVVVVVVVTEFNRTMuMTAwVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV//tSZKkP" +
            "8AAAaQAAAAgAAA0gAAABAAABpAAAACAAADSAAAAEVVVVVVVVVVVMQU1FMy4xMDBVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVX/+1JkqQ/wAABpAAAACAAADSAAAAEA" +
            "AAGkAAAAIAAANIAAAARVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVf/7UmSpD/AAAGkAAAAIAAANIAAAAQAAAaQAAAAgAAA0gAAABFVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" +
            "VVVVVVVVVVVVVVVVVVVVVVVVVVVV";
    }

    // EL RECORTE: quita 'fuera' cuadros del principio y corta tras 'musica' cuadros. Cuenta en muestras (de todos los
    // canales), asi que un origen que entrega cuadros partidos no descoloca nada. Movido conserva el final tras seek.
    internal sealed class RecorteRelleno
    {
        private readonly Func<float[], int, int, int> lee;
        private long saltar, quedan;
        private readonly int canales;
        private readonly long inicio, musica;
        private readonly float[] basura = new float[4096];
        internal RecorteRelleno(Func<float[], int, int, int> origen, int canales, long fuera, long musica)
        {
            lee = origen;
            this.canales = Math.Max(1, canales);
            inicio = Math.Max(0, fuera); this.musica = Math.Max(0, musica);
            Movido(0);
        }
        internal void Movido(long cuadroCrudo)
        {
            long p = Math.Max(0, cuadroCrudo);
            saltar = Math.Max(0, inicio - p) * canales;
            quedan = Math.Max(0, musica - Math.Max(0, p - inicio)) * canales;
        }
        internal int Lee(float[] buffer, int offset, int count)
        {
            while (saltar > 0)
            {
                int r = lee(basura, 0, (int)Math.Min(saltar, basura.Length));
                if (r <= 0) return 0;
                saltar -= r;
            }
            if (quedan <= 0) return 0;
            int n = lee(buffer, offset, (int)Math.Min(count, quedan));
            if (n > 0) quedan -= n;
            return n;
        }
    }

    public partial class Form1
    {
        // El retraso del descompresor de este equipo: int.MinValue = aun no medido; -1 = no se pudo (no se recorta).
        private static int rellenoExtra = int.MinValue;
        private static readonly object rellenoCerrojo = new object();
        // Las pruebas cambian como se descomprime el clip (en Linux no hay descompresor de Windows).
        internal static Func<byte[], NAudio.Wave.ISampleProvider> AbreClip = null;

        // Si no se pudo medir (un fallo pasajero: poca memoria justo en ese momento, el descompresor ocupado), no se da por perdido
        // para toda la sesion (22-10-2026): pasados unos minutos se intenta UNA vez mas. Si vuelve a fallar, se queda en -1.
        internal static int RellenoReintentoMs = 5 * 60 * 1000;
        private static int rellenoFalloDesde;
        private static bool rellenoReintentado;

        private static int RellenoExtra()
        {
            lock (rellenoCerrojo)
            {
                if (rellenoExtra != int.MinValue)
                {
                    if (rellenoExtra >= 0 || rellenoReintentado) return rellenoExtra;
                    if (unchecked(Environment.TickCount - rellenoFalloDesde) < RellenoReintentoMs) return rellenoExtra;
                    rellenoReintentado = true;   // el reintento es unico
                }
                rellenoExtra = MideRelleno();
                if (rellenoExtra < 0) rellenoFalloDesde = Environment.TickCount;
                return rellenoExtra;
            }
        }
        // Descomprime el clip por el mismo camino que las canciones y busca el clic.
        private static int MideRelleno()
        {
            string porque;
            int extra = -1;
            try
            {
                byte[] clip = RellenoMp3.Clip();
                EtiquetaLame e;
                if (!RellenoMp3.Lee(clip, clip.Length, out e)) { porque = "el MP3 de medir no tiene etiqueta LAME"; }
                else
                {
                    Func<byte[], NAudio.Wave.ISampleProvider> abre = AbreClip;
                    NAudio.Wave.ISampleProvider sp = null; NAudio.Wave.Mp3FileReader r = null; MemoryStream ms = null;
                    try
                    {
                        if (abre != null) sp = abre(clip);
                        else { ms = new MemoryStream(clip); r = AbreMp3(ms); sp = new NAudio.Wave.SampleProviders.SampleChannel(r, false); }
                        int ch = sp.WaveFormat.Channels;
                        float[] x = new float[(RellenoMp3.ClicMuestras + 4 * 1152 + RellenoMp3.ExtraMaximo + 4096) * ch];
                        int n = 0, k;
                        while (n < x.Length && (k = sp.Read(x, n, x.Length - n)) > 0) n += k;
                        extra = RellenoMp3.ExtraDelClic(x, n, ch, e.Retraso, out porque);
                    }
                    finally
                    {
                        if (r != null) { try { r.Dispose(); } catch { } }
                        if (ms != null) { try { ms.Dispose(); } catch { } }
                    }
                }
            }
            catch (Exception ex) { porque = ex.GetType().Name + ": " + ex.Message; extra = -1; }
            if (extra >= 0)
                RellenoMp3.Apunta("MP3 sin huecos: el descompresor de este equipo retrasa " + extra + " muestras; en los MP3 con etiqueta LAME se quita el retraso y el relleno del codificador: en uso");
            else
                RellenoMp3.Apunta("MP3 sin huecos: NO se usa (" + porque + "); los MP3 suenan como antes");
            return extra;
        }

        // Pone el recorte si el MP3 tiene etiqueta LAME valida y el descompresor se pudo medir. Si no, la fuente tal cual.
        private IAudioSource SinRelleno(string path, IAudioSource s)
        {
            if (s == null || path == null || !path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return s;
            try
            {
                EtiquetaLame e;
                if (!RellenoMp3.Lee(path, out e)) return s;
                if (s.WaveFormat.SampleRate != e.Tasa) return s;
                if (!RellenoMp3.Cuadra(e, s.TotalTime.TotalSeconds)) return s;   // un MP3 unido o cortado: como antes (ver Cuadra)
                int extra = RellenoExtra();
                if (extra < 0) return s;
                return new FuenteSinRelleno(s, e.Retraso + extra, e.Muestras);
            }
            catch { return s; }
        }

        // La cancion sin el retraso del principio ni el relleno del final (la cuenta, en RecorteRelleno).
        private sealed class FuenteSinRelleno : IAudioSource
        {
            private readonly IAudioSource s;
            internal IAudioSource Interior { get { return s; } }
            private readonly RecorteRelleno recorte;
            private readonly long cuadrosInicio;
            private readonly int tasa;
            private long muestrasMusica;
            internal readonly long CuadrosMusica;   // lo que dura la musica (para el fundido de salida)
            internal FuenteSinRelleno(IAudioSource origen, long cuadrosFuera, long cuadrosMusica)
            {
                s = origen; CuadrosMusica = cuadrosMusica; cuadrosInicio = cuadrosFuera;
                tasa = Math.Max(1, origen.WaveFormat.SampleRate);
                recorte = new RecorteRelleno(origen.Read, origen.WaveFormat.Channels, cuadrosFuera, cuadrosMusica);
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return s.WaveFormat; } }
            public int Read(float[] buffer, int offset, int count) { int n = recorte.Lee(buffer, offset, count); if (n > 0) System.Threading.Interlocked.Add(ref muestrasMusica, n); return n; }
            public float Volume { set { s.Volume = value; } }
            public TimeSpan CurrentTime { get { return TiempoCuadros(System.Threading.Interlocked.Read(ref muestrasMusica) / Math.Max(1, WaveFormat.Channels)); } }
            public TimeSpan TotalTime { get { return TiempoCuadros(CuadrosMusica); } }
            private TimeSpan TiempoCuadros(long cuadros) { return TimeSpan.FromTicks((long)Math.Round((double)cuadros * TimeSpan.TicksPerSecond / tasa)); }
            internal double SegundosCrudos { get { return s.TotalTime.TotalSeconds; } }
            internal MapaSilencios MapaMusica(MapaSilencios m)
            {
                float d = (float)((double)cuadrosInicio / tasa);
                if (m.Desde >= 0) m.Desde = Math.Max(0f, m.Desde - d);
                if (m.Hasta >= 0) m.Hasta = Math.Min((float)TotalTime.TotalSeconds, Math.Max(0f, m.Hasta - d));
                if (m.HuecoDesde >= 0) m.HuecoDesde = Math.Max(0f, m.HuecoDesde - d);
                if (m.HuecoHasta >= 0) m.HuecoHasta = Math.Max(0f, m.HuecoHasta - d);
                m.Dura = (float)TotalTime.TotalSeconds;
                return m;
            }
            public void Salta(TimeSpan donde)
            {
                long p = Math.Max(0, Math.Min(CuadrosMusica, (long)Math.Round(donde.TotalSeconds * tasa)));
                s.Salta(TiempoCuadros(p + cuadrosInicio));
                recorte.Movido(p + cuadrosInicio);
                System.Threading.Interlocked.Exchange(ref muestrasMusica, p * Math.Max(1, WaveFormat.Channels));
            }
            public void Dispose() { s.Dispose(); }
        }
    }
}
