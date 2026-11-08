using System;
using System.IO;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    // ============================== LA CANCION, EN MEMORIA ==============================
    //
    // Mientras sonaba, la cancion se seguia leyendo del disco: NAudio pide el MP3 a trozos chicos (al abrir un MP3 de
    // 1,5 MB, 3.807 lecturas de 4 bytes y 3.804 saltos para armar su indice; al tocarlo, 7.602 lecturas mas; medido).
    // Windows junta esas lecturas, pero cada una es una llamada, y si el disco se atasca -un disco duro con el antivirus
    // o las actualizaciones encima, un disco que se habia dormido, una memoria USB lenta- la musica espera al disco.
    // Y la siguiente ya se leia ENTERA por adelantado (PrecalientaSiguiente), pero los bytes se tiraban y se confiaba
    // en que Windows los guardara hasta que tocara: con poca memoria y el repaso de la biblioteca leyendo canciones
    // enteras, eso dura minutos.
    //
    // Ahora un MP3 se abre desde la MEMORIA: la lectura por adelantado deja aqui los bytes de la siguiente y, si una
    // cancion llega sin ellos (la primera, o una elegida con la cola vacia), se lee entera de una vez al abrirla, en
    // trozos grandes. Mientras suena, el disco no se toca: queda libre para las portadas y el repaso, y un atasco del
    // disco no puede cortar la musica.
    //
    // LO QUE SE OYE ES IDENTICO: el mismo lector de NAudio con el mismo descompresor (AbreMp3 de dos argumentos es el de
    // siempre, letra por letra, leyendo de la memoria en vez del archivo), sobre los mismos bytes.
    //
    // Con tope: solo MP3 (lo demas lo abre Windows por el nombre), hasta 32 MB (una sesion de una hora sigue leyendose
    // del disco como siempre) y nunca mas de un octavo de la memoria libre del equipo. Como mucho hay dos a la vez: la
    // que suena y la siguiente.
    public partial class Form1
    {
        internal const long TopeEnMemoria = 32L * 1024 * 1024;
        private static readonly object enMemoriaCerrojo = new object();
        private static string adelantoRuta, adelantoFirma;   // la siguiente, tal como la dejo la lectura por adelantado
        private static byte[] adelantoDatos;
        internal static int EnMemoriaDelAdelanto, EnMemoriaLeidas;   // cuantas se abrieron asi (pruebas y diagnostico)

        // ¿Se puede tener entera en memoria?
        private static bool CabeEnMemoria(string ruta, long largo)
        {
            if (ruta == null || !ruta.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return false;
            if (largo <= 0 || largo > TopeEnMemoria) return false;
            ulong libre = MemoriaLibre();
            return libre == 0 || (ulong)largo * 8UL <= libre;
        }
        // EN LINUX: aqui XP declara EstadoMemoria (la MEMORYSTATUSEX de Windows) y PideEstadoMemoria (GlobalMemoryStatusEx de kernel32). La
        // memoria del equipo la da Linux (Nativo/Equipo.cs, de /proc/meminfo).
        private static ulong MemoriaLibre()
        {
            try
            {
                ulong fisica, fisicaLibre; uint carga;   // EN LINUX: (EstadoMemoria y su Largo) lo que se pregunta
                if (Equipo.Memoria(out fisica, out fisicaLibre, out carga)) return fisicaLibre;   // EN LINUX: (PideEstadoMemoria: GlobalMemoryStatusEx) /proc/meminfo
            }
            catch { }
            return 0;   // no se sabe (otro sistema): no se pone pega
        }

        // La lectura por adelantado pide donde leer: la cancion entera si cabe, o null (se lee a trozos y se tira, como
        // siempre).
        private static byte[] ReservaEnMemoria(string ruta, long largo)
        {
            if (!CabeEnMemoria(ruta, largo)) return null;
            try { return NuevoArregloDeCancion(largo, "lectura por adelantado"); }
            catch (OutOfMemoryException) { return null; }
        }
        // Cada cancion en memoria es un arreglo de 1 a 32 MB: va al monton de objetos grandes, y pedir uno puede disparar una recoleccion de
        // generacion 2 (que en .NET 3.5 para todos los hilos). Con diagnostico=1 se apunta cuantas hubo mientras se pedia el arreglo, para
        // saber en la rocola real si eso es lo que se nota al cambiar de cancion (si no, no hace falta reusar los arreglos). Una diferencia de
        // contadores es una COINCIDENCIA en el tiempo (otro hilo pudo provocarla), no su duracion ni su causa.
        internal static volatile bool DiagMemoria;
        private static long diagKB; private static int diagG2Antes, diagG2Despues, diagG0Antes, diagG0Despues, diagMs; private static string diagQuien;
        private static byte[] NuevoArregloDeCancion(long largo, string quien)
        {
            if (!DiagMemoria) return new byte[largo];
            int g2 = GC.CollectionCount(2), g0 = GC.CollectionCount(0), t = Environment.TickCount;
            byte[] d = new byte[largo];
            diagMs = unchecked(Environment.TickCount - t); diagKB = largo / 1024; diagQuien = quien;
            diagG2Antes = g2; diagG2Despues = GC.CollectionCount(2); diagG0Antes = g0; diagG0Despues = GC.CollectionCount(0);
            return d;
        }
        // Para la linea "cancion:" de la bitacora (solo con diagnostico=1): la ultima reserva y el total de recolecciones de generacion 2.
        internal static string DiagReservaTexto()
        {
            string q = diagQuien;
            if (q == null) return "; sin arreglo propio de cancion todavia (recolecciones gen2 en total: " + GC.CollectionCount(2) + ")";
            return "; ultimo arreglo de cancion (" + q + "): " + diagKB + " KB en " + diagMs + " ms, gen2 " + diagG2Antes + " -> " + diagG2Despues
                 + ", gen0 " + diagG0Antes + " -> " + diagG0Despues + " (coincidencia, no causa); gen2 en total: " + GC.CollectionCount(2);
        }
        // Y la deja aqui, entera, con la firma que tenia el archivo antes de leerlo.
        private static void DejaEnMemoria(string ruta, string firma, byte[] datos)
        {
            if (ruta == null || firma == null || datos == null) return;
            lock (enMemoriaCerrojo) { adelantoRuta = ruta; adelantoFirma = firma; adelantoDatos = datos; }
        }
        // La cola cambio: lo leido por adelantado ya no es la siguiente.
        private static void SueltaEnMemoria()
        {
            lock (enMemoriaCerrojo) { adelantoRuta = null; adelantoFirma = null; adelantoDatos = null; }
        }
        // Los bytes de 'ruta' si los dejo la lectura por adelantado y el archivo no cambio desde entonces. Se entregan una
        // sola vez.
        private static byte[] TomaDelAdelanto(string ruta)
        {
            byte[] d; string firma;
            lock (enMemoriaCerrojo)
            {
                if (adelantoDatos == null || !string.Equals(ruta, adelantoRuta, StringComparison.OrdinalIgnoreCase)) return null;
                d = adelantoDatos; firma = adelantoFirma;
                adelantoRuta = null; adelantoFirma = null; adelantoDatos = null;
            }
            return (firma == AudioFileSignature(ruta)) ? d : null;
        }
        // Entera, de una vez y en trozos grandes; null si no cabe o no se pudo (entonces se abre del disco, como siempre).
        private static byte[] LeeEnteraEnMemoria(string ruta)
        {
            try
            {
                using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
                {
                    long largo = fs.Length;
                    if (!CabeEnMemoria(ruta, largo)) return null;
                    byte[] d = NuevoArregloDeCancion(largo, "al abrir");
                    int hecho = 0;
                    while (hecho < d.Length)
                    {
                        int n = fs.Read(d, hecho, Math.Min(256 * 1024, d.Length - hecho));
                        if (n <= 0) return null;   // se acorto mientras se leia: del disco, como siempre
                        hecho += n;
                    }
                    return d;
                }
            }
            catch { return null; }   // (sin memoria incluido)
        }
        // El MP3 entero en memoria, listo para abrir; null si no toca.
        private static MemoryStream CancionEnMemoria(string ruta)
        {
            if (ruta == null || !ruta.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return null;
            byte[] d = TomaDelAdelanto(ruta);
            if (d != null) Interlocked.Increment(ref EnMemoriaDelAdelanto);
            else
            {
                d = LeeEnteraEnMemoria(ruta);
                if (d == null) return null;
                Interlocked.Increment(ref EnMemoriaLeidas);
            }
            return new MemoryStream(d, 0, d.Length, false, true);
        }

        // EL MISMO AbreMp3 DE SIEMPRE (el de un archivo), letra por letra, pero leyendo de la memoria: el mismo descompresor,
        // la misma comprobacion de la coma flotante con el nombre del archivo, y el mismo cierre si la apertura falla. El
        // lector no se queda con el flujo: lo guarda quien lo abrio (FileAudioSource).
        private static Mp3FileReader AbreMp3(string ruta, Stream enMemoria)
        {
            NAudio.Wave.IMp3FrameDecompressor hecho = null;
            Func<NAudio.Wave.WaveFormat, NAudio.Wave.IMp3FrameDecompressor> otroAcm = delegate(NAudio.Wave.WaveFormat f)
            { return DescompresorMp3.Crea(f); };   // EN LINUX: (Mp3FileReader.CreateAcmFrameDecompressor, el ACM de Windows) el descompresor nativo (Nativo/DescompresorMp3.cs: libmpg123)
            Mp3FileReader.FrameDecompressorBuilder armador = delegate(NAudio.Wave.WaveFormat wf)
            {
                hecho = DescompresorMp3.Crea(wf);   // EN LINUX: (Mp3FileReader.CreateAcmFrameDecompressor, el ACM de Windows) el descompresor nativo (Nativo/DescompresorMp3.cs: libmpg123)
                hecho = DecodificacionFlotante.Envuelve(wf, hecho, ruta, otroAcm);
                return hecho;
            };
            // Desde la memoria se abre el lector ligero (Mp3Ligero.cs): el MISMO Mp3FileReader de NAudio, con su cuenta de Read y de Position, pero
            // reutilizando la memoria de cada cuadro en lugar de crear 38 objetos y 76 arreglos por segundo de cancion (5,8 a 9,6 MB de basura por
            // cancion). El descompresor recibe los mismos bytes en el mismo orden y los mismos Reset: la bateria reproligera lo comprueba con
            // una cancion entera, saltos, reanudar y el relevo sin pausa. Si NAudio no es la 1.9 esperada, el de siempre.
            try { return LectorMp3Ligero.Posible ? LectorMp3Ligero.Abre(enMemoria, armador) : new Mp3FileReader(enMemoria, armador); }
            catch { if (hecho != null) { try { hecho.Dispose(); } catch { } } throw; }
        }
    }
}
