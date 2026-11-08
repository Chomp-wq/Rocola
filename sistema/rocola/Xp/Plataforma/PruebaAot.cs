// PRUEBA DE NATIVEAOT Y DE LAS LLAMADAS A WINDOWS TRADUCIDAS (no es de XP): rocola-xp --prueba-aot [CARPETA_MUSICA [VIDEO]]
// Recorre una por una las partes de XP por las que el compilador de NativeAOT avisa (reflexion y estructuras de Windows: los IL2xxx/IL3xxx
// de Aot.cs) y las llamadas a Windows traducidas (LlamadasWindows.cs), y escribe lo que contesta cada una en lineas «clave = valor». Se corre
// con el programa de JIT (dotnet rocola-xp.dll --prueba-aot) y con el de NativeAOT (./rocola-xp --prueba-aot) y las dos salidas tienen que
// ser iguales, salvo las lineas que empiezan con «~» (cambian de una corrida a otra: memoria libre, tiempos, cuantos hilos).
// rocola-xp --prueba-mp3 ARCHIVO...: el lector de MP3 (Mp3Xp.cs) con un descompresor falso; su salida tiene que ser igual, linea por linea,
// a la del mismo texto (ComparaMp3, abajo) compilado con Mono contra NAudio 1.9, el lector de la rocola de XP (comprobado el 8-11-2026 con 14
// MP3: CBR, VBR, sin Xing, mono de 22 kHz, 48 kHz con ID3v1, roto a la mitad, con APE, con cambio de frecuencia, cortado, con basura al
// principio, con ID3v2 extendido, con un hueco, con enfasis reservado y solo la cabecera Xing).
// Corren dentro del inicializador del modulo, en un solo hilo (otro hilo que entrara al modulo esperaria a que acabe) y sin abrir la rocola.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Rocola
{
    internal static class PruebaAot
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Antes()
        {
            string[] a = Environment.GetCommandLineArgs();
            if (a.Length >= 2 && a[1] == "--prueba-aot") Environment.Exit(Corre(a.Length >= 3 ? a[2] : null, a.Length >= 4 ? a[3] : null));
            if (a.Length >= 3 && a[1] == "--prueba-mp3") { string[] r = new string[a.Length - 2]; Array.Copy(a, 2, r, 0, r.Length); ComparaMp3.Corre(r, Console.Out); Environment.Exit(0); }
        }

        [DllImport("libc", EntryPoint = "syscall")] private static extern long Llamada(long numero);
        [DllImport("libEGL.so.1")] private static extern IntPtr eglGetCurrentContext();

        private static void Di(string clave, object valor) { Console.WriteLine(clave + " = " + Convert.ToString(valor, CultureInfo.InvariantCulture)); }
        private static void Prueba(string clave, Func<object> f)
        {
            object v;
            try { v = f(); } catch (Exception ex) { v = "EXCEPCION " + ex.GetType().Name + ": " + ex.Message; }
            Di(clave, v);
        }

        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "La prueba mide justo eso: Marshal.SizeOf(typeof) como lo hace XP (Aot.cs)")]
        private static int Corre(string musica, string video)
        {
            // (RuntimeFeature.IsDynamicCodeSupported no sirve para distinguirlos: con PublishAot el de JIT tambien corre con ella apagada)
            Di("~ modo", Path.GetFileName(Environment.ProcessPath ?? "") == "dotnet" ? "JIT" : "NativeAOT");
            Di("~ sistema", Environment.OSVersion + ", .NET " + Environment.Version);

            // ---- 1) lo que XP decide con la memoria del equipo (en XP con GlobalMemoryStatusEx; aqui Nativo/Equipo.cs)
            Prueba("form1.tope_portadas (memoria)", () => Form1.TopeDePortadas());
            Prueba("form1.cache_celdas_mb (memoria)", () => Form1.CacheCeldasMb());
            Prueba("diag.cpu", () => DiagEquipo.Cpu());
            Prueba("diag.memoria.con_carga", () => { long t, l; return DiagEquipo.Memoria(out t, out l).Contains("(carga ") + " total>0=" + (t > 0) + " libre>0=" + (l > 0); });
            Prueba("~ diag.memoria", () => { long t, l; return DiagEquipo.Memoria(out t, out l); });

            // ---- 2) reflexion
            Prueba("audio.mascara 5.1 (dwChannelMask de NAudio)", () => FormatoAudio.Mascara(FormatoAudio.Pcm(48000, 16, 6, 0x3F)).ToString("X"));
            Prueba("audio.mascara estereo", () => FormatoAudio.Mascara(new WaveFormat(44100, 16, 2)));
            Prueba("audio.flotante 5.1", () => { WaveFormat f = FormatoAudio.Flotante(48000, 6, 0x3F); return f.GetType().Name + " " + f.Encoding + " mascara=" + FormatoAudio.Mascara(f).ToString("X"); });
            Prueba("mp3ligero.posible", () => LectorMp3Ligero.Posible + " indice=" + LectorMp3Ligero.IndicePosible);
            Prueba("nlayer", () => { NLayer.MpegFrameDecoder d = new NLayer.MpegFrameDecoder(); return typeof(NLayer.MpegFrameDecoder).Assembly.GetName().Name; });
            // (La duracion que mide no se puede ver aqui: ffmpeg contesta por otro hilo, y otro hilo que entre al modulo espera a que acabe
            // esta prueba. Se comprueba con la rocola entera: el video de la biblioteca sale medido en rocola_duracion.txt.)
            Prueba("video.medidor (como Form1.MideDuracionVideo: Nativo/MedidorVideo.cs)", () =>
            {
                MedidorVideo w = MedidorVideo.Crea();
                if (w == null) return "sin ffmpeg";
                string r = "duracion antes=" + w.Duracion.ToString("0.00", CultureInfo.InvariantCulture);
                if (video != null) { r += " pedida=" + w.PideDuracion(video); w.QuitaArchivo(); r += " al dejarlo=" + w.Duracion.ToString("0.00", CultureInfo.InvariantCulture); }
                w.Dispose();
                return r;
            });

            // ---- 3) lo que XP lee de Linux para el diagnostico completo (HilosRocola, LectorCpu: sus caminos de Unix)
            Prueba("diag.hilo_nativo", () => HilosRocola.IdNativoActual() == Llamada(186) ? "igual que gettid" : "distinto");
            Prueba("diag.cpu_sistema", () => { double o, t; return LectorCpu.SistemaMs(out o, out t) + " ocupado<=total " + (o <= t); });
            Prueba("diag.cpu_hilo", () => LectorCpu.HiloMs(HilosRocola.IdNativoActual()) >= 0);
            Prueba("diag.cpu_proceso", () => LectorCpu.ProcesoMs() >= 0);
            Prueba("~ procesadores", () => CargaTrabajo.Procesadores + " de " + Environment.ProcessorCount);

            // ---- 4) lo de Windows que contesta Linux directo (Nativo/Equipo.cs, Nativo/PrioridadHilo.cs)
            Prueba("win.direcciones", () => { ulong t, l; bool b = Equipo.Direcciones(out t, out l); return b + " total=" + t + " libres<total " + (l < t); });
            Prueba("~ win.direcciones_libres_mb", () => { ulong t, l; Equipo.Direcciones(out t, out l); return l >> 20; });
            Prueba("~ win.procesador_mhz", () => Equipo.ProcesadorMhz());
            Prueba("~ win.sistema", () => Equipo.SistemaOperativo());
            Prueba("win.drm (carpeta de prueba)", () => DrmDePrueba());
            Prueba("win.drm (/sys/class/drm)", () => { string c, p, k; return Equipo.TarjetaDrm("/sys/class/drm", out c, out p, out k) + " [" + c + "|" + p + "|" + k + "]"; });
            Rocola.Pantalla.FueraDePantalla fp = null;
            if (eglGetCurrentContext() == IntPtr.Zero) try { fp = new Rocola.Pantalla.FueraDePantalla(64, 64); } catch { }
            Prueba("win.adaptador", () => { string n, c, i; return Equipo.Adaptador(out n, out c, out i) + " [" + n + "]"; });
            Prueba("diag.tarjeta", () => DiagEquipo.Tarjeta());
            if (fp != null) fp.Dispose();
            string nices = "";
            Prueba("win.prioridad", () =>
            {
                Dictionary<uint, int> antes = new Dictionary<uint, int>();
                foreach (string h in Directory.GetDirectories("/proc/self/task")) { uint t = uint.Parse(Path.GetFileName(h)); antes[t] = PrioridadHilo.NiceDelHilo(t); }
                PrioridadHilo.DelProceso(System.Diagnostics.ProcessPriorityClass.AboveNormal);
                int movidos = 0, mal = 0;
                foreach (KeyValuePair<uint, int> k in antes)
                {
                    int d = PrioridadHilo.NiceDelHilo(k.Key);
                    if (d == int.MinValue) continue;
                    if (d == Math.Max(-20, Math.Min(19, k.Value - 6))) movidos++; else mal++;
                    nices += k.Value + "->" + d + " ";
                }
                return "principal " + PrioridadHilo.NiceDelHilo((uint)Environment.ProcessId) + (mal == 0 ? ", todos los hilos movidos 6 hacia arriba" : ", " + mal + " hilos mal");
            });
            // Thread.Priority de XP en el hilo que llama (PrioridadHilo.Pon), tras la clase de arriba. (La de un hilo nuevo, PrioridadHilo.Con, no
            // se puede ver aqui: el hilo nuevo esperaria a que acabe esta prueba; se ve con la rocola entera, en /proc/PID/task.)
            Prueba("win.prioridad_hilo (la correspondencia, en este hilo)", () =>
            {
                string r = "";
                foreach (System.Threading.ThreadPriority p in new[] { System.Threading.ThreadPriority.Highest, System.Threading.ThreadPriority.AboveNormal, System.Threading.ThreadPriority.Normal, System.Threading.ThreadPriority.BelowNormal, System.Threading.ThreadPriority.Lowest })
                {
                    PrioridadHilo.Pon(System.Threading.Thread.CurrentThread, p);
                    r += p + "=" + PrioridadHilo.NiceDelHilo(PrioridadHilo.Este()) + (PrioridadHilo.De(System.Threading.Thread.CurrentThread) == p ? "" : "(De dice otra)") + " ";
                }
                PrioridadHilo.Pon(System.Threading.Thread.CurrentThread, System.Threading.ThreadPriority.Normal);
                return r + "y de vuelta Normal=" + PrioridadHilo.NiceDelHilo(PrioridadHilo.Este());
            });
            // Lo que .NET devuelve de Thread.Priority de un hilo que ya corre (por eso XP lee la suya con PrioridadHilo.De): con JIT, la puesta;
            // con NativeAOT, siempre Normal
            Prueba("~ win.thread_priority_de_net (tras ponerle Lowest)", () =>
            {
                PrioridadHilo.Pon(System.Threading.Thread.CurrentThread, System.Threading.ThreadPriority.Lowest);
                string r = ".NET dice " + System.Threading.Thread.CurrentThread.Priority + ", De dice " + PrioridadHilo.De(System.Threading.Thread.CurrentThread);
                PrioridadHilo.Pon(System.Threading.Thread.CurrentThread, System.Threading.ThreadPriority.Normal);
                return r;
            });
            Di("~ win.prioridad_hilos", nices);
            Prueba("~ win.sleep1_ms", () =>
            {
                System.Diagnostics.Stopwatch r = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 200; i++) System.Threading.Thread.Sleep(1);
                return (r.Elapsed.TotalMilliseconds / 200).ToString("0.000", CultureInfo.InvariantCulture);
            });
            Prueba("win.version (Form1.Video: videoSinMedir = Major < 6)", () => Environment.OSVersion.Platform + " " + (Environment.OSVersion.Version.Major < 6 ? "como XP (sin medir videos)" : "como Vista o mas (mide videos)"));

            // ---- 5) el lector de MP3 (Plataforma/NAudioXp.cs) frente a lo que hace el de XP (NAudio 1.9)
            if (musica != null && Directory.Exists(musica))
            {
                List<string> mp3 = new List<string>(Directory.GetFiles(musica, "*.mp3", SearchOption.AllDirectories));
                mp3.Sort(StringComparer.Ordinal);
                foreach (string ruta in mp3) Prueba("mp3 " + Path.GetFileName(ruta), () => Mp3(ruta));
            }
            return 0;
        }

        // Una carpeta como /sys/class/drm con dos tarjetas: la primera sin nada enchufado, la segunda con su HDMI enchufado
        private static string DrmDePrueba()
        {
            string r = Path.Combine(Path.GetTempPath(), "rocola-prueba-drm-" + Environment.ProcessId);
            try
            {
                Directory.CreateDirectory(Path.Combine(r, "card0", "device")); Directory.CreateDirectory(Path.Combine(r, "card0-VGA-1"));
                Directory.CreateDirectory(Path.Combine(r, "card1", "device")); Directory.CreateDirectory(Path.Combine(r, "card1-HDMI-A-1")); Directory.CreateDirectory(Path.Combine(r, "card1-DVI-D-1"));
                Directory.CreateDirectory(Path.Combine(r, "renderD128"));
                File.WriteAllText(Path.Combine(r, "card0", "device", "uevent"), "DRIVER=simpledrm\n");
                File.WriteAllText(Path.Combine(r, "card0-VGA-1", "status"), "disconnected\n");
                File.WriteAllText(Path.Combine(r, "card1", "device", "uevent"), "DRIVER=radeon\nPCI_CLASS=30000\nPCI_ID=1002:990C\nPCI_SUBSYS_ID=1043:8526\n");
                File.WriteAllText(Path.Combine(r, "card1-HDMI-A-1", "status"), "connected\n");
                File.WriteAllText(Path.Combine(r, "card1-DVI-D-1", "status"), "disconnected\n");
                string c, p, k;
                bool b = Equipo.TarjetaDrm(r, out c, out p, out k);
                return b + " [" + c + "|" + p + "|" + k + "]";
            }
            finally { try { Directory.Delete(r, true); } catch { } }
        }

        // El lector de XP (NAudio 1.9 traducido, Mp3Xp.cs) frente al de NAudio 3 (Mp3FileReaderBase), con el mismo descompresor: lo que XP
        // espera de su lector es la duracion exacta al abrir, la misma cuenta al leerlo entero y, despues de dos saltos seguidos, lo mismo que
        // con uno solo (el de NAudio 3 calla 50 ms despues de dos saltos en menos de 30 ms: su «modo de arrastre»). Lo leido de corrido y
        // despues de un salto tiene que salir igual en los dos (la huella de los bytes).
        private static string Mp3(string ruta)
        {
            string r = "";
            string[] huellas = new string[2];
            for (int k = 0; k < 2; k++)
            {
                bool xp = k == 0;
                Func<WaveStream> abre = xp ? (Func<WaveStream>)(() => new Mp3FileReader(ruta)) : () => new Mp3FileReaderBase(ruta, DescompresorMp3.Crea);
                long alAbrir, alFinal, leidos = 0; ulong huella = 14695981039346656037UL; int alineado;
                using (WaveStream s = abre())
                {
                    alAbrir = s.Length; alineado = s.WaveFormat.BlockAlign;
                    byte[] b = new byte[65536]; int n;
                    while ((n = s.Read(b, 0, b.Length)) > 0) { leidos += n; for (int i = 0; i < n; i++) { huella ^= b[i]; huella *= 1099511628211UL; } }
                    alFinal = s.Length;
                }
                long destino = alFinal / 4 / alineado * alineado, antes = alFinal / 2 / alineado * alineado;
                byte[] uno = new byte[16384], dos = new byte[16384];
                using (WaveStream s = abre()) { s.Position = destino; Lee(s, uno); }
                using (WaveStream s = abre()) { s.Position = antes; s.Position = destino; Lee(s, dos); }
                int ceros = 0; for (int i = 0; i < dos.Length; i++) if (dos[i] == 0) ceros++;
                bool igual = true; for (int i = 0; i < uno.Length; i++) if (uno[i] != dos[i]) { igual = false; break; }
                ulong h1 = 14695981039346656037UL; for (int i = 0; i < uno.Length; i++) { h1 ^= uno[i]; h1 *= 1099511628211UL; }
                huellas[k] = huella.ToString("x16") + "/" + h1.ToString("x16");
                r += (xp ? "[1.9] " : " [NAudio 3] ") + "al_abrir=" + alAbrir + " leidos=" + leidos + " al_final=" + alFinal
                   + " dos_saltos=" + (igual ? "igual que uno" : "distinto (" + ceros + " de " + dos.Length + " bytes en cero)");
            }
            return r + " | huellas " + (huellas[0] == huellas[1] ? "iguales (de corrido y tras un salto)" : "DISTINTAS " + huellas[0] + " " + huellas[1]);
        }
        private static void Lee(WaveStream s, byte[] b) { int t = 0, n; while (t < b.Length && (n = s.Read(b, t, b.Length - t)) > 0) t += n; }
    }

    // LA COMPARACION DEL LECTOR DE MP3 (rocola-xp --prueba-mp3 ARCHIVO...): el mismo texto se compilo con Mono contra NAudio 1.9 (el lector
    // de la rocola de XP) y su salida tiene que ser igual, linea por linea, a la de aqui (la traduccion, Mp3Xp.cs). El descompresor es uno
    // falso y determinista: la salida depende solo de los cuadros que el lector le da y de cuantos lleva desde el ultimo Reset (como uno de
    // verdad: el primero tras vaciar no da nada y el segundo da doble).
    internal sealed class DescompresorFalso : IMp3FrameDecompressor
    {
        private readonly WaveFormat f; private int desdeReset;
        public DescompresorFalso(WaveFormat mp3) { f = new WaveFormat(mp3.SampleRate, 16, mp3.Channels); }
        public WaveFormat OutputFormat { get { return f; } }
        public int DecompressFrame(Mp3Frame frame, byte[] dest, int destOffset)
        {
            desdeReset++;
            if (desdeReset == 1) return 0;
            uint h = 2166136261; byte[] r = frame.RawData;
            for (int i = 0; i < frame.FrameLength; i++) { h ^= r[i]; h *= 16777619; }
            h ^= (uint)desdeReset;
            int n = frame.SampleCount * f.BlockAlign;
            if (desdeReset == 2 && frame.SampleCount == 1152) n *= 2;
            for (int i = 0; i < n; i++) dest[destOffset + i] = (byte)((h >> ((i & 3) * 8)) ^ (uint)i);
            return n;
        }
        public void Reset() { desdeReset = 0; }
        public void Dispose() { }
    }

    internal static class ComparaMp3
    {
        private static Mp3FileReader Abre(string ruta) { return new Mp3FileReader(ruta, delegate (WaveFormat w) { return new DescompresorFalso(w); }); }
        private static string Huella(byte[] b, int n) { ulong h = 14695981039346656037UL; for (int i = 0; i < n; i++) { h ^= b[i]; h *= 1099511628211UL; } return h.ToString("x16"); }
        private static int Lee(WaveStream s, byte[] b, int cuanto) { int t = 0, n; while (t < cuanto && (n = s.Read(b, t, Math.Min(7000, cuanto - t))) > 0) t += n; return t; }

        internal static void Corre(string[] archivos, TextWriter o)
        {
            foreach (string ruta in archivos)
            {
                string nom = Path.GetFileName(ruta);
                try
                {
                    long largo;
                    using (Mp3FileReader r = Abre(ruta))
                    {
                        largo = r.Length;
                        o.WriteLine(nom + " abrir: largo=" + r.Length + " formato=" + r.WaveFormat.SampleRate + "/" + r.WaveFormat.Channels + " mp3=" + r.Mp3WaveFormat.SampleRate + "/" + r.Mp3WaveFormat.Channels + "/" + r.Mp3WaveFormat.AverageBytesPerSecond + "/" + r.Mp3WaveFormat.BlockAlign + " xing=" + (r.XingHeader != null) + " id3v1=" + (r.Id3v1Tag != null) + " tiempo_ms=" + Math.Round(r.TotalTime.TotalMilliseconds));   // (en CLR 2, TimeSpan.FromSeconds redondea al ms; en .NET 10, no)
                        byte[] todo = new byte[(int)Math.Max(0, r.Length) + 100000];
                        int t = Lee(r, todo, todo.Length);
                        o.WriteLine(nom + " de corrido: " + t + " bytes, huella " + Huella(todo, t) + ", posicion " + r.Position + ", largo " + r.Length);
                    }
                    long[] dondes = new long[] { 0, largo / 4, largo / 2 + 3, largo - 1000, largo, largo + 5000, 4, 4608 * 3 + 2 };
                    byte[] b = new byte[30000];
                    using (Mp3FileReader r = Abre(ruta))
                        foreach (long p in dondes)
                        {
                            r.Position = p;
                            long antes = r.Position;
                            int n = Lee(r, b, 10000);
                            o.WriteLine(nom + " salto a " + p + ": posicion " + antes + ", leidos " + n + ", huella " + Huella(b, n) + ", despues " + r.Position);
                        }
                    using (Mp3FileReader r = Abre(ruta))
                    {
                        r.Position = largo / 2; r.Position = largo / 4;
                        int n = Lee(r, b, 10000);
                        o.WriteLine(nom + " dos saltos: leidos " + n + ", huella " + Huella(b, n));
                        r.CurrentTime = TimeSpan.FromTicks(12345000);   // (FromTicks: el mismo valor en CLR 2 y en .NET 10)
                        long p = r.Position;
                        n = Lee(r, b, 5000);
                        o.WriteLine(nom + " tiempo 1.2345 s: posicion " + p + ", leidos " + n + ", huella " + Huella(b, n) + ", tiempo_ms " + Math.Round(r.CurrentTime.TotalMilliseconds));
                    }
                    using (Mp3FileReader r = Abre(ruta))
                    {
                        int cuadros = 0; long muestras = 0; ulong h = 14695981039346656037UL;
                        Mp3Frame f;
                        while ((f = r.ReadNextFrame()) != null) { cuadros++; muestras += f.SampleCount; for (int i = 0; i < f.FrameLength; i++) { h ^= f.RawData[i]; h *= 1099511628211UL; } }
                        o.WriteLine(nom + " cuadros: " + cuadros + ", muestras " + muestras + ", huella " + h.ToString("x16") + ", posicion " + r.Position);
                    }
                }
                catch (Exception ex) { o.WriteLine(nom + " EXCEPCION " + ex.GetType().Name + ": " + ex.Message); }
            }
        }
    }
}
