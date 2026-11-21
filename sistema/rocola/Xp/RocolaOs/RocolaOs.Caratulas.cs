// LAS CARATULAS MAS RAPIDAS (Rocola OS, rapido-caratulas, 20-11-2026; peticion del dueno del 20-11-2026: «muchas mejoras [de rendimiento],
// priorizando [...] la carga de las caratulas en general para que sean instantaneas (por lo menos las que pertenecen a la musica local)»).
// No es de XP. Sin ROCOLA_OS (o con caratulas-rapidas=0 en rocola_os.txt) no hace nada: cada gancho devuelve lo de XP. g220 y g221 NO cambian la imagen:
// lo que se ve es punto por punto lo mismo (Pruebas/RapidoCaratulas: banco con huellas); por eso va encendida de fabrica y sin interruptor
// en ninguna ventana (decision del dueno del 15-9-2026: lo que no cambia el aspecto es automatico). Lo medido, en FASE11_rapido-caratulas.md.
//
// g220 (Codigo/Form1.Biblioteca.cs, SaveThumb): LA MINIATURA EN PNG CON MENOS COMPRESION. XP guarda cada miniatura nueva de 256x256 en PNG
//      (thumb.Save(tmp, ImageFormat.Png)); traducido, el codificador PNG de Skia con lo de fabrica (zlib 6 y probar los cinco filtros en
//      cada fila) tarda unos 38 ms por miniatura en esta maquina: el 70 % de lo que cuesta una portada nueva, y el lector de portadas (uno
//      en dos nucleos) no pasa a la siguiente hasta guardarla. Con zlib 1 y el filtro «Up» (de libpng) son 3 ms y el archivo sale un 20 %
//      mas grande (unos 110 KB en vez de 90); PNG no pierde nada: al leerla salen los mismos puntos (comprobado en cada miniatura del banco).
//      Las miniaturas que ya estan (de XP o de antes) se leen igual: no se rehace ninguna.
// g221 (Codigo/Form1.Portadas.cs, CoverSettle_Tick): LAS LETRAS DE AL LADO, ADELANTADAS. Cuando alguien se queda en una pagina (los 120 ms
//      de XP), XP encarga en la fila baja las portadas de las paginas de colchon (dos a cada lado) y la siguiente (con el salto a la letra
//      siguiente solo desde la ultima pagina). Saltar de letra con REGRESAR e izquierda o derecha lleva a la PRIMERA pagina de la letra de
//      antes o de despues, que XP no adelanta: llegaba sin portadas (en caliente, un cuadro o dos; sin miniatura guardada, uno o dos
//      segundos). Aqui se encargan tambien esas dos paginas (36 portadas como mucho), en la MISMA fila baja y despues de lo de XP: el
//      lector de XP (uno en dos nucleos) las atiende cuando no hay nada visible, con sus mismos topes; la cache de memoria de XP (220 o
//      1000) las guarda como a las demas. Las de Spotify (su letra) no: esas no son de la musica local.
//
// LAS DOS PROPUESTAS DEL DOCUMENTO, ELEGIDAS POR EL DUENO (21-11-2026), cada una con su interruptor en rocola_os.txt, ENCENDIDAS de fabrica
// (las dos solo valen la primera vez de cada portada: despues se lee la miniatura guardada, como siempre):
// g222 (Codigo/Form1.Portadas.cs, DecodeCoverCore) portadas_dct=: LA JPEG GRANDE, DECODIFICADA YA REDUCIDA. Para hacer la miniatura de 256,
//      XP descomprime la portada original ENTERA (una de 3 000 px son 9 millones de puntos: 60-90 ms aqui) y la achica con los mipmaps. Con
//      la escala de la DCT de libjpeg-turbo (M/8, M de 1 a 7) la JPEG sale ya reducida, a la escala mas chica que deje los DOS lados en al
//      menos RocolaOsDctVeces veces lo que se pinta (la miniatura: 256, y nunca menos); de ahi sigue el camino de XP sin cambiar nada (la
//      reduccion a 256 con el mismo filtro, el premultiplicado, la miniatura que se publica y la que se guarda). Solo JPEG que Skia da en BGRA o en gris
//      (PNG, GIF y lo demas: lo de siempre) y solo si sirve (una que mide menos de 8/7 de eso, entera como siempre). Los puntos NO salen
//      iguales (lo medido, en FASE11_rapido-caratulas.md: contra un achicado ideal se aparta lo mismo o menos que lo de XP): por eso es
//      eleccion del dueno y tiene interruptor.
// g223 (Codigo/CaratulasAdaptativas.cs, AdaptiveCoverReaders) y g224 (Codigo/Form1.Portadas.cs, CoverPump) portadas_dos_lectores=: DOS
//      LECTORES MIENTRAS LA PAGINA QUE SE VE ESTA SIN MINIATURAS. En dos nucleos XP pone un lector de portadas (CargaTrabajo.Lectores(2) = 1,
//      para no quitarle procesador a la pantalla). Cuando el lector va a hacer una portada sin miniatura guardada (de la original) y la fila
//      alta (lo que se ve) tiene mas portadas esperando, se permite UN lector mas que los de XP; ese de mas decodifica a nice 19 (por debajo
//      del repaso de XP: solo toma el procesador que la pantalla, el sonido y el lector de XP dejan libre) y en cuanto la fila alta se vacia
//      (la pagina ya se repartio) vuelve a valer lo de XP y el de mas acaba lo suyo y se va (las mismas reglas de XP para sobrar). Cada
//      portada es un trabajo de la cola de XP (coverLoading: una ruta, un trabajo): nunca dos lecturas del mismo archivo. En un disco con
//      cabezal (DiscoLento) no: alli XP deja un lector por unidad.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
        internal const string RocolaOsCaratulasNombre = "caratulas-rapidas";
        internal const string RocolaOsCaratulasDct = "portadas_dct", RocolaOsCaratulasDosLectores = "portadas_dos_lectores";

#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraCaratulas()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsCaratulasNombre,
                Que = "las caratulas mas rapidas (miniaturas PNG con menos compresion; las letras de al lado, adelantadas; con sus claves, la JPEG grande ya reducida y dos lectores con la pagina sin miniaturas)",
                DeFabrica = true,
                Claves = new RocolaOsClave[]
                {
                    RocolaOsClave.SiNo(RocolaOsCaratulasDct, true, true, "la primera vez de cada portada JPEG grande, decodificarla ya reducida (escala DCT): mucho mas rapido, los puntos casi iguales"),
                    RocolaOsClave.SiNo(RocolaOsCaratulasDosLectores, true, true, "un segundo lector de portadas, en el fondo, mientras la pagina que se ve esta sin miniaturas"),
                },
            });
        }

        // g220: la miniatura nueva en PNG (SaveThumb, Codigo/Form1.Biblioteca.cs). Sin la funcion, lo de siempre (Imagenes.GuardaPng).
        internal static void RocolaOsGuardaMiniaturaPng(SKBitmap thumb, string tmp)
        {
            // (con ROCOLA_IA_DATOS sin poder usarse, las miniaturas van a datos.img, de 512 MB: ahi no se gasta el 20 % de mas)
            if (!RocolaOs.Encendida(RocolaOsCaratulasNombre) || DatosGrandes.CayoEnDatos) { Imagenes.GuardaPng(thumb, tmp); return; }
            using (FileStream fs = File.Create(tmp)) RocolaOsGuardaPngRapida(thumb, fs);
        }
        // g221: la primera pagina de la letra de antes y de la de despues, en la fila baja (CoverSettle_Tick, despues de lo de XP). La misma
        // rueda que GotoLetter, la de REGRESAR+izq/der (las letras con discos; la estrella y los videos solo si su lista ya esta hecha: aqui no
        // se arma nada; la de Spotify, si es la de al lado, no se adelanta ni se salta).
        private void RocolaOsAdelantaLetras()
        {
            if (!RocolaOs.Encendida(RocolaOsCaratulasNombre)) return;
#if ROCOLA_GPU
            if (!gpuEnabled || libBuilding) return;
            // el MISMO orden y la misma rueda que GotoLetter (g46: con Spotify, su letra despues de VIDEO; en una pared del buscador, ninguna)
            string order = RocolaOsSpotifyCatalogoOrden(LetrasOrden);
            int n = order.Length;
            if (n == 0) return;
            int idx = order.IndexOf(currentLetter);
            if (idx < 0) idx = 0;
            for (int lado = -1; lado <= 1; lado += 2)
                for (int k = 1; k <= n; k++)
                {
                    char c = order[((idx + lado * k) % n + n) % n];
                    if (c == currentLetter) break;
                    if (!letterAvailable.Contains(c)) continue;
                    List<Album> lista = null;   // la letra de Spotify no es de XP: no se adelanta (pero si es la de al lado, tampoco se salta)
                    if (c == VideoMark) lista = videoAlbums;
                    else if (c == TopMark) lista = topDiscs;
                    else if (c != RocolaOsSpotifyMarca) byLetter.TryGetValue(c, out lista);
                    if (lista != null) PrefetchPage(lista, 0);
                    break;
                }
#endif
        }
        // zlib 1 y el filtro Up (Pruebas/RapidoCaratulas: 3 ms en vez de 38 por miniatura, un 20 % mas de disco, los mismos puntos)
        internal static void RocolaOsGuardaPngRapida(SKBitmap b, Stream s)
        {
            using (SKPixmap p = b.PeekPixels())
            using (SKData d = p == null ? null : p.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.Up, 1)))
            {
                if (d == null) throw new IOException("no se pudo guardar la miniatura");
                d.SaveTo(s);
            }
        }
        // ---------- g222: la JPEG grande, ya reducida (portadas_dct=) ----------
        // Cuantas veces lo que se pinta deben medir los DOS lados de la imagen reducida (lo que salio de medir: FASE11_rapido-caratulas.md).
        internal const int RocolaOsDctVeces = 2;
        // g222 (DecodeCoverCore): la portada original para hacer la miniatura de 'pinta' px. Sin la funcion o sin portadas_dct, o si no es una
        // JPEG que sirva, Imagenes.Lee de siempre (con el mismo Stream: no se ha tocado).
        internal static SKBitmap RocolaOsLeeOriginal(MemoryStream ms, byte[] data, int largo, int pinta, out FormatoImagen formato)
        {
            if (RocolaOs.Encendida(RocolaOsCaratulasNombre) && RocolaOs.AjusteSiNo(RocolaOsCaratulasDct))
            {
                SKBitmap r = null; FormatoImagen fr = FormatoImagen.Argb32;
                try { r = RocolaOsLeeJpegReducida(data, largo, pinta * RocolaOsDctVeces, out fr); } catch { r = null; }
                if (r != null) { formato = fr; return r; }
            }
            return Imagenes.Lee(ms, out formato);
        }
        // Una JPEG en color o en gris decodificada a la escala M/8 mas chica (M de 1 a 7) con los dos lados de al menos 'minimo'; null si no
        // es eso o si no hace falta reducir (entonces, lo de siempre). Los puntos en BGRA premultiplicado (opaca; una gris, con sus tres
        // canales iguales, lo que da pintarla en BGRA) y el formato de una JPEG (Rgb24), como Imagenes.Lee (LeeDirecto: el mismo tipo de alfa
        // que pide Decode, Success o IncompleteInput).
        internal static unsafe SKBitmap RocolaOsLeeJpegReducida(byte[] d, int n, int minimo, out FormatoImagen formato)
        {
            formato = FormatoImagen.Argb32;
            if (d == null || n < 4 || n > d.Length || d[0] != 0xFF || d[1] != 0xD8 || minimo <= 0) return null;
            int cw, ch;
            if (!MedidaDeCabecera(d, n, out cw, out ch) || (long)Math.Min(cw, ch) * 7 < (long)minimo * 8) return null;   // ni 7/8 cabe: entera
            SKData sd;
            fixed (byte* p = d) sd = SKData.CreateCopy((IntPtr)p, n);
            using (sd)
            using (SKCodec c = SKCodec.Create(sd))
            {
                if (c == null || c.EncodedFormat != SKEncodedImageFormat.Jpeg) return null;
                SKImageInfo info = c.Info;
                if ((info.ColorType != SKColorType.Bgra8888 && info.ColorType != SKColorType.Gray8) || info.AlphaType != SKAlphaType.Opaque || info.Width <= 0 || info.Height <= 0) return null;
                SKSizeI t = default(SKSizeI); bool hay = false;
                for (int m = 1; m < 8 && !hay; m++)
                {
                    t = c.GetScaledDimensions(m / 8f);
                    hay = t.Width >= minimo && t.Height >= minimo && t.Width < info.Width && t.Height < info.Height;
                }
                if (!hay) return null;
                SKBitmap b = new SKBitmap(new SKImageInfo(t.Width, t.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
                try
                {
                    if (b.GetPixels() == IntPtr.Zero) { b.Dispose(); return null; }
                    SKCodecResult r = c.GetPixels(new SKImageInfo(t.Width, t.Height, SKColorType.Bgra8888, SKAlphaType.Opaque), b.GetPixels());
                    if (r != SKCodecResult.Success && r != SKCodecResult.IncompleteInput) { b.Dispose(); return null; }
                    b.NotifyPixelsChanged();
                    formato = FormatoImagen.Rgb24;
                    return b;
                }
                catch { b.Dispose(); throw; }
            }
        }

        // ---------- g223 y g224: dos lectores mientras la pagina que se ve esta sin miniaturas (portadas_dos_lectores=) ----------
        private bool rosFaltanMiniaturas;   // (bajo coverLock) el lector encontro una portada sin miniatura y lo que se ve tenia mas esperando
        private int rosLectoresXp = 1;      // (bajo coverLock) los lectores que XP deja ahora (lo ultimo que dijo AdaptiveCoverReaders)
        private int rosDecodificanXp;       // (bajo coverLock) los que estan decodificando con la prioridad de XP (como mucho rosLectoresXp)
        [ThreadStatic] private static bool rosLectorAlFondo;   // este lector esta a nice 19
        [DllImport("libc", SetLastError = true, EntryPoint = "setpriority")] private static extern int RosSetpriority(int cual, uint quien, int nice);
        // g223 (AdaptiveCoverReaders, siempre bajo coverLock): los lectores de XP, o uno mas mientras la fila alta tenga portadas sin miniatura
        private int RocolaOsLectoresPortadas(int xp)
        {
            rosLectoresXp = xp;
            if (!rosFaltanMiniaturas) return xp;
            if (coverQHi.Count == 0 || !RocolaOs.Encendida(RocolaOsCaratulasNombre) || !RocolaOs.AjusteSiNo(RocolaOsCaratulasDosLectores))
            { rosFaltanMiniaturas = false; return xp; }   // la pagina que se ve ya se repartio: vuelve lo de XP (el de mas acaba lo suyo y se va)
            return Math.Max(xp, Math.Min(xp + 1, CargaTrabajo.Procesadores));
        }
        // g224 (CoverPump): la portada del trabajo, por DecodeCoverForPublication de XP. Antes, si es de la original y lo que se ve tiene mas
        // esperando, se abre el segundo lector; y el lector que decodifica cuando ya estan los de XP decodificando, a nice 19.
        private SKBitmap RocolaOsDecodificaPortada(string key, string identidad, out SKBitmap pendiente)
        {
            if (!RocolaOs.Encendida(RocolaOsCaratulasNombre) || !RocolaOs.AjusteSiNo(RocolaOsCaratulasDosLectores))
            {
                if (rosLectorAlFondo) { PrioridadHilo.AlArrancar(); rosLectorAlFondo = false; }   // (la clave se apago con uno al fondo)
                return DecodeCoverForPublication(key, identidad, out pendiente);
            }
            bool fria = false;
            try { fria = identidad != null && (MiniaturaGuardada(identidad) == null || (JpegActivo && MiniaturaPorRehacer(identidad))) && !DiscoLento(key); } catch { }
            bool abre = false, deXp;
            lock (coverLock)
            {
                if (fria && !rosFaltanMiniaturas && coverQHi.Count > 0) { rosFaltanMiniaturas = abre = true; }
                deXp = rosDecodificanXp < rosLectoresXp;
                if (deXp) rosDecodificanXp++;
            }
            try
            {
                if (deXp && rosLectorAlFondo) { PrioridadHilo.AlArrancar(); rosLectorAlFondo = false; }   // la de XP (BelowNormal)
                else if (!deXp && !rosLectorAlFondo) { try { RosSetpriority(0, PrioridadHilo.Este(), RocolaOs.NiceDeFondo); } catch { } rosLectorAlFondo = true; }
                if (abre) EnsureCoverReaders();
                return DecodeCoverForPublication(key, identidad, out pendiente);
            }
            finally { if (deXp) lock (coverLock) rosDecodificanXp--; }
        }
    }
}
