// ====================================================================================================
//  LOS VIDEOS EN LINUX
// ----------------------------------------------------------------------------------------------------
//  En Windows los videos los ponia Media Player, que viene con el sistema. En Linux no hay nada
//  equivalente que venga de serie, asi que la rocola se apoya en dos programas que van DENTRO del
//  .sfs, junto a ella:
//
//      mpv       los reproduce, dibujando DENTRO de la ventana de la rocola
//      ffmpeg    saca el sonido de un video para poder medirlo (el nivelador de volumen)
//
//  Y si no estan, no pasa nada malo: no hay seccion de videos y la rocola funciona igual que si no
//  hubiera ninguno. Nunca una seccion vacia ni un video mudo.
// ====================================================================================================
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using NAudio.Wave;

namespace Rocola
{
    internal static class Video
    {
        // ¿Estan los dos programas? Se busca UNA vez: preguntarlo por cada video seria abrir un proceso
        // por cada archivo de la biblioteca.
        private static int hayMpv = -1, hayFfmpeg = -1;

        private static bool Existe(string programa)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(programa, "--version");
                psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
                Process p = Process.Start(psi);
                p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
                p.WaitForExit(4000);
                return true;
            }
            catch { return false; }
        }

        public static bool HayReproductor { get { if (hayMpv < 0) hayMpv = Existe("mpv") ? 1 : 0; return hayMpv == 1; } }
        public static bool HayMedidor { get { if (hayFfmpeg < 0) hayFfmpeg = Existe("ffmpeg") ? 1 : 0; return hayFfmpeg == 1; } }

        // Cuanto dura un video, preguntandoselo a ffprobe. Devuelve 0 si no se pudo.
        public static double Duracion(string ruta)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("ffprobe",
                    "-v quiet -show_entries format=duration -of csv=p=0 \"" + ruta + "\"");
                psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
                Process p = Process.Start(psi);
                string s = p.StandardOutput.ReadToEnd().Trim();
                p.StandardError.ReadToEnd();
                p.WaitForExit(8000);
                double d;
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d) && d > 0) return d;
            }
            catch { }
            return 0;
        }
    }

    // EL REPRODUCTOR DE VIDEO: mpv, dibujando DENTRO de la ventana de la rocola.
    //
    // En Windows esto lo hacia el control de Media Player, que se metia dentro de la ventana como un
    // trozo mas del programa. mpv hace lo mismo por otra via: se le dice "dibuja dentro de ESTA ventana"
    // y se le mandan las ordenes -pon este video, sube el volumen, por donde vas- por un canal propio.
    //
    // DOS COSAS QUE HAY QUE HACER BIEN, Y LAS DOS COSTARON DE ENCONTRAR:
    //
    //   1. HAY QUE DECIRLE CON QUE DIBUJAR. mpv tiene una lista de formas de dibujar y va probando de
    //      arriba abajo. Si las buenas no estan -en una maquina sin tarjeta grafica- acaba cayendo en
    //      una de reserva que NO respeta "dibuja dentro de esta ventana": el video sale en una ventana
    //      suelta en una esquina de la pantalla, encima de todo. Se vio en las pruebas. Por eso aqui se
    //      le nombran solo las dos que si la respetan.
    //
    //   2. EL CANAL DE ORDENES es un socket de Unix, que el .NET de esta rocola no sabe abrir. Se abre
    //      a mano con las mismas llamadas del sistema que usa cualquier programa de Linux, igual que se
    //      hace con el sonido y con las ventanas en el resto de esta rama.
    internal sealed class ReproductorMpv : IDisposable
    {
        private const int AF_UNIX = 1, SOCK_STREAM = 1;
        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)] private static extern int socket(int dominio, int tipo, int protocolo);
        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)] private static extern int connect(int fd, byte[] dir, int largo);
        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)] private static extern IntPtr send(int fd, byte[] datos, IntPtr n, int banderas);
        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)] private static extern IntPtr recv(int fd, byte[] datos, IntPtr n, int banderas);
        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)] private static extern int close(int fd);

        private Process mpv;
        private int fd = -1;
        private readonly string socketRuta;
        private readonly byte[] entrada = new byte[8192];
        private string sobra = "";
        private int idPeticion = 1;
        private readonly object candado = new object();

        public bool Ok { get { return mpv != null && fd >= 0 && !Muerto; } }
        public bool Muerto { get { try { return mpv == null || mpv.HasExited; } catch { return true; } } }

        public ReproductorMpv(IntPtr ventanaX11)
        {
            socketRuta = "/tmp/rocola_mpv_" + Process.GetCurrentProcess().Id + ".sock";
            try { File.Delete(socketRuta); } catch { }
            string args = "--wid=" + ventanaX11.ToInt64()
                        + " --input-ipc-server=" + socketRuta
                        + " --vo=gpu,x11"                  // SOLO las que respetan --wid (ver arriba)
                        + " --idle=yes --force-window=yes --keep-open=no"
                        + " --no-terminal --no-osc --osd-level=0"
                        + " --no-input-default-bindings --input-vo-keyboard=no"   // la rocola manda: mpv no atiende teclas
                        + " --cursor-autohide=always --really-quiet";
            ProcessStartInfo psi = new ProcessStartInfo("mpv", args);
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            mpv = Process.Start(psi);
            // El socket no existe hasta que mpv arranca del todo: se espera un poco, sin bloquear la rocola
            // mas de lo justo.
            for (int i = 0; i < 60 && fd < 0; i++)
            {
                System.Threading.Thread.Sleep(50);
                if (Muerto) break;
                if (File.Exists(socketRuta)) Conecta();
            }
            if (fd >= 0) Manda("{\"command\":[\"disable_event\",\"all\"]}");   // sin avisos por su cuenta: asi cada respuesta es la de su pregunta
        }

        private void Conecta()
        {
            try
            {
                int s = socket(AF_UNIX, SOCK_STREAM, 0);
                if (s < 0) return;
                // sockaddr_un: dos bytes con la familia y detras la ruta, terminada en cero.
                byte[] dir = new byte[110];
                dir[0] = AF_UNIX; dir[1] = 0;
                byte[] r = System.Text.Encoding.UTF8.GetBytes(socketRuta);
                Array.Copy(r, 0, dir, 2, Math.Min(r.Length, 106));
                if (connect(s, dir, dir.Length) != 0) { close(s); return; }
                fd = s;
            }
            catch { }
        }

        private void Manda(string orden)
        {
            if (fd < 0) return;
            try
            {
                byte[] b = System.Text.Encoding.UTF8.GetBytes(orden + "\n");
                send(fd, b, (IntPtr)b.Length, 0);
            }
            catch { }
        }

        // Pregunta por una propiedad y devuelve el texto que venga detras de "data". No hace falta un
        // lector de JSON entero: las respuestas de mpv son de una linea y lo unico que se pide son
        // numeros y si/no. Un lector completo seria mas codigo del que resuelve.
        private string Pregunta(string propiedad)
        {
            if (fd < 0) return null;
            int id;
            lock (candado)
            {
                id = idPeticion++;
                Manda("{\"command\":[\"get_property\",\"" + propiedad + "\"],\"request_id\":" + id + "}");
                string marca = "\"request_id\":" + id;
                for (int intento = 0; intento < 20; intento++)
                {
                    string linea = Linea();
                    if (linea == null) break;
                    if (linea.IndexOf(marca, StringComparison.Ordinal) < 0) continue;   // no es la respuesta a ESTA pregunta
                    int i = linea.IndexOf("\"data\":", StringComparison.Ordinal);
                    if (i < 0) return null;
                    i += 7;
                    int j = i;
                    while (j < linea.Length && linea[j] != ',' && linea[j] != '}') j++;
                    return linea.Substring(i, j - i).Trim().Trim('"');
                }
            }
            return null;
        }

        private string Linea()
        {
            try
            {
                int corte = sobra.IndexOf('\n');
                while (corte < 0)
                {
                    long n = recv(fd, entrada, (IntPtr)entrada.Length, 0).ToInt64();
                    if (n <= 0) return null;
                    sobra += System.Text.Encoding.UTF8.GetString(entrada, 0, (int)n);
                    corte = sobra.IndexOf('\n');
                }
                string l = sobra.Substring(0, corte);
                sobra = sobra.Substring(corte + 1);
                return l;
            }
            catch { return null; }
        }

        private double Numero(string propiedad)
        {
            string s = Pregunta(propiedad);
            double d;
            if (s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            return -1;
        }

        public void Abre(string ruta)
        {
            lock (candado) Manda("{\"command\":[\"loadfile\",\"" + ruta.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"]}");
        }
        public void Para() { lock (candado) Manda("{\"command\":[\"stop\"]}"); }
        public int Volumen { set { lock (candado) Manda("{\"command\":[\"set_property\",\"volume\"," + value + "]}"); } }
        public double Posicion { get { return Numero("time-pos"); } }
        public double Duracion { get { return Numero("duration"); } }
        // ¿SE ACABO EL VIDEO? Dos formas de saberlo, y hacen falta las dos.
        //
        // "eof-reached" solo dice que si en el instante justo en que llega al final; en cuanto mpv
        // descarga el archivo vuelve a decir que no, y si el sondeo cae un pelo despues no se entera
        // nunca y el video se queda pegado en la pantalla. Se vio en la prueba: un video de 6 segundos
        // seguia diciendo que no habia terminado a los 8.
        //
        // "idle-active" dice que mpv esta sin nada que reproducir, y eso ya no se le pasa: en cuanto
        // suelta el archivo se queda asi hasta que se le da otro.
        public bool Termino
        {
            get
            {
                if (Pregunta("eof-reached") == "true") return true;
                return Pregunta("idle-active") == "true";
            }
        }

        public void Dispose()
        {
            try { if (fd >= 0) { Manda("{\"command\":[\"quit\"]}"); close(fd); } } catch { }
            fd = -1;
            try { if (mpv != null && !mpv.HasExited) { mpv.WaitForExit(600); if (!mpv.HasExited) mpv.Kill(); } } catch { }
            try { if (mpv != null) mpv.Dispose(); } catch { }
            mpv = null;
            try { File.Delete(socketRuta); } catch { }
        }
    }

    // EL SONIDO DE UN VIDEO, COMO SI FUERA UNA CANCION.
    //
    // Esta es la unica pieza que hacia falta para que el nivelador de volumen de los videos funcione en
    // Linux. Todo lo demas -medir la sonoridad, guardarla, emparejarla con la de las canciones, la
    // perilla de Ctrl+F3- ya estaba escrito y probado: lo unico que faltaba era alguien que supiera
    // sacar el sonido de dentro de un video.
    //
    // Lo hace ffmpeg, que lo entrega ya en el formato que la rocola usa por dentro (coma flotante,
    // 44100, dos canales) y por una tuberia, sin escribir nada al disco.
    internal sealed class SonidoDeVideo : ISampleProvider, IDisposable
    {
        private const int Frecuencia = 44100, Canales = 2;
        private readonly WaveFormat wf = WaveFormat.CreateIeeeFloatWaveFormat(Frecuencia, Canales);
        private Process ff;
        private Stream tuberia;
        private byte[] crudo = new byte[16384];
        private long muestrasLeidas;
        private readonly double duracion;

        public SonidoDeVideo(string ruta)
        {
            if (!Video.HayMedidor) throw new NotSupportedException("Falta ffmpeg: sin el no se puede medir el sonido de un video.");
            duracion = Video.Duracion(ruta);
            ProcessStartInfo psi = new ProcessStartInfo("ffmpeg",
                "-v quiet -nostdin -i \"" + ruta + "\" -vn -f f32le -acodec pcm_f32le -ar " + Frecuencia + " -ac " + Canales + " -");
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            ff = Process.Start(psi);
            tuberia = ff.StandardOutput.BaseStream;
        }

        public WaveFormat WaveFormat { get { return wf; } }

        public int Read(float[] buffer, int offset, int count)
        {
            if (tuberia == null) return 0;
            int quiero = count * 4;
            if (crudo.Length < quiero) crudo = new byte[quiero];
            int hechos = 0;
            // Una tuberia entrega lo que tiene, no lo que se le pide: hay que insistir hasta llenar o
            // hasta que se acabe. Sin esto, una lectura corta pareceria el final del video.
            while (hechos < quiero)
            {
                int n;
                try { n = tuberia.Read(crudo, hechos, quiero - hechos); }
                catch { n = 0; }
                if (n <= 0) break;
                hechos += n;
            }
            int muestras = hechos / 4;
            Buffer.BlockCopy(crudo, 0, buffer, offset * 4, muestras * 4);
            muestrasLeidas += muestras;
            return muestras;
        }

        public TimeSpan CurrentTime { get { return TimeSpan.FromSeconds((double)muestrasLeidas / (Frecuencia * Canales)); } }
        public TimeSpan TotalTime { get { return TimeSpan.FromSeconds(duracion); } }

        public void Dispose()
        {
            try { if (tuberia != null) tuberia.Close(); } catch { }
            tuberia = null;
            try { if (ff != null && !ff.HasExited) ff.Kill(); } catch { }
            try { if (ff != null) ff.Dispose(); } catch { }
            ff = null;
        }
    }
}
