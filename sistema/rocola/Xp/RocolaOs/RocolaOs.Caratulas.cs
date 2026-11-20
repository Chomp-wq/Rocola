// LAS CARATULAS MAS RAPIDAS (Rocola OS, rapido-caratulas, 20-11-2026; peticion del dueno del 20-11-2026: «muchas mejoras [de rendimiento],
// priorizando [...] la carga de las caratulas en general para que sean instantaneas (por lo menos las que pertenecen a la musica local)»).
// No es de XP. Sin ROCOLA_OS (o con caratulas-rapidas=0 en rocola_os.txt) no hace nada: cada gancho devuelve lo de XP. NO cambia la imagen:
// lo que se ve es punto por punto lo mismo (Pruebas/RapidoCaratulas: banco con huellas); por eso va encendida de fabrica y sin interruptor
// en ninguna ventana (decision del dueno del 15-9-2026: lo que no cambia el aspecto es automatico). Lo medido, en FASE11_rapido-caratulas.md.
//
// g220 (Codigo/Form1.Biblioteca.cs, SaveThumb): LA MINIATURA EN PNG CON MENOS COMPRESION. XP guarda cada miniatura nueva de 256x256 en PNG
//      (thumb.Save(tmp, ImageFormat.Png)); traducido, el codificador PNG de Skia con lo de fabrica (zlib 6 y probar los cinco filtros en
//      cada fila) tarda unos 38 ms por miniatura en esta maquina: el 70 % de lo que cuesta una portada nueva, y el lector de portadas (uno
//      en dos nucleos) no pasa a la siguiente hasta guardarla. Con zlib 1 y el filtro «Up» (de libpng) son 3 ms y el archivo sale un 20 %
//      mas grande (unos 110 KB en vez de 90); PNG no pierde nada: al leerla salen los mismos puntos (comprobado en cada miniatura del banco).
//      Las miniaturas que ya estan (de XP o de antes) se leen igual: no se rehace ninguna.
using System;
using System.IO;
using System.Runtime.CompilerServices;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
        internal const string RocolaOsCaratulasNombre = "caratulas-rapidas";

#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraCaratulas()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsCaratulasNombre,
                Que = "las caratulas mas rapidas, con la misma imagen (miniaturas PNG con menos compresion)",
                DeFabrica = true,
            });
        }

        // g220: la miniatura nueva en PNG (SaveThumb, Codigo/Form1.Biblioteca.cs). Sin la funcion, lo de siempre (Imagenes.GuardaPng).
        internal static void RocolaOsGuardaMiniaturaPng(SKBitmap thumb, string tmp)
        {
            if (!RocolaOs.Encendida(RocolaOsCaratulasNombre)) { Imagenes.GuardaPng(thumb, tmp); return; }
            using (FileStream fs = File.Create(tmp)) RocolaOsGuardaPngRapida(thumb, fs);
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
    }
}
