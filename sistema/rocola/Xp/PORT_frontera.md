# Port nativo de XP, tanda 2: la frontera con la tarjeta (grupo «frontera»)

Pista del grupo «frontera» de la segunda tanda del port nativo (decisión y aclaración del dueño del 8-11-2026: «que tradujeras todo el
código para que trabajara nativamente en Linux», «ningún layer de traducción»). La tanda traduce el dibujo con GDI+ de los archivos de la
capa de la tarjeta a SkiaSharp directo, por grupos; a este le tocó la frontera: lo que la rocola sube a la tarjeta y los hornos de
`D3D9Star.cs`. Receta, regla del puente y diferencias conocidas: `PORT_DIBUJO.md`; la tarjeta: `PORT_TARJETA.md`. No se probó nada en el
A4 (no se puede desde aquí): todo es fuera de pantalla con llvmpipe.

La referencia de las comparaciones es la copia congelada `/root/rocola-trabajo/referencia-xp` (commit aef2211, la versión con capas; nunca
se toca). Base de esta pista: b6093196.

## Qué se tradujo

`Codigo/D3D9Star.cs` y `Codigo/TexturasCompatiblesGpu.cs` ya no usan GDI+: fuera su `using Rocola.Dibujo2D`; lo que todavía les llega de
archivos sin traducir es `Rocola.Dibujo2D.Bitmap`, escrito entero (abajo, «Costuras»). Cada línea que cambia lleva su `// EN LINUX: ...` y
los archivos conservan su número de líneas (4 539 y 264) y sus finales de línea.

| Archivo | Qué | En XP | Ahora |
|---|---|---|---|
| `D3D9Star.cs` | `CreateTextureFromBitmap`, `SubeBitmap`, `SubeBitmapBorde`, `CreateTexturePow2`, `UpdateTextureRegion`, `UpdateTexturePow2` | reciben `Bitmap`; `LockBits(ReadOnly, Format32bppArgb)` y `CopyMemory` a lo bloqueado de la textura (en bloques de 64 KiB o fila a fila) | reciben `SKBitmap`; `Pixeles.CopiaSinPremultiplicar` lee los puntos sin premultiplicar (la tabla truncada de la referencia) directo a lo bloqueado, fila a fila con su paso: los mismos bytes, sin la copia aparte. La guarda de XP sobre `bd.Scan0` y `bd.Stride` mira los puntos y el paso del `SKBitmap`; las dos copias de XP quedan como comentario |
| `D3D9Star.cs` | `GpuStarfield`: el constructor y `CreateOverlay`, `CreateCoverShared`, `UpdateOverlay`, `UpdateOverlayRegion` | `Bitmap` | `SKBitmap` (y el puente con `Bitmap`) |
| `D3D9Star.cs` | los hornos `BakeViaLactea`, `BakeNebulosas`, `Mancha`, `BakeRadialGlow`, `BakeStarTexture` | `Graphics`, `GraphicsPath`, `PathGradientBrush`, `SolidBrush`, `FillEllipse`, `FillPolygon` | `SKCanvas`, `SKPath` (par-impar, el `Alternate` de fábrica), `Degradado.DeTrazo` en la caja del trazo (`GetBounds`), `Pixel.RellenaOvalo` y `Pixel.RellenaPoligono`, el suavizado en una variable (`bool suave = true`); `Mapas.Nuevo` por `new Bitmap(w, h, Format32bppArgb)` |
| `D3D9Star.cs` | `BakeVineta` y su mapa guardado (`vinetaMapa`, solo de este archivo) | `LockBits(WriteOnly)` + `Marshal.Copy` | `Pixeles.EscribeSinPremultiplicar`; `SKBitmap` |
| `D3D9Star.cs` | `EnsureFpsTex` (el contador de Ctrl+F1) | `Bitmap` PArgb, `Font(GenericMonospace, 9,5, Bold)`, `TextRenderingHint.AntiAlias`, `FillRectangle`, `DrawString` | `Mapas.Nuevo`, `Letra.EnPuntos(Letras.AnchoFijo, 9,5, negrita)`, `AjusteLetra.Suave` en una variable, `Pixel.Rellena` sin suavizar, `TextoGdiPlus.Escribe` desde el punto y sin formato |
| `D3D9Star.cs` | `Upload`, `EnsureNieblas`, `EnsureLuna`, `PrecalientaAnimaciones` | `Bitmap` | `SKBitmap` |
| `TexturasCompatiblesGpu.cs` | `PortadasCompartidasGpu` (`Toma`, sus diccionarios), `CreaPortadaCompartida`, `ActualizaRegionBitmap`, `CreaTexturaDividida`, `ActualizaDividida` | `Bitmap` (las portadas compartidas, por identidad del `Bitmap`); `LockBits` de la zona | `SKBitmap` (por identidad del `SKBitmap`: un mapa por portada); `Pixeles.CopiaSinPremultiplicar` de la zona |
| `EstrellaGpu.cs`, `CieloNaturalGpu.cs`, `Form1.Fondo.cs` | `BakeStar`, `BakeNatural`, `BakeRuidoGrande` | devolvían el `Bitmap` del puente (`PuenteDibujo.Entrega`, D4) | devuelven `SKBitmap` (fuera la `Entrega`: `return b;` como en XP) |
| `CieloVivoGpu.cs` | `EnsureLunaCielo` | `Entrega(HorneaLuna(...))` para `Upload` | `Upload` recibe el `SKBitmap` de `HorneaLuna` |

El estado del `Graphics` en esta frontera es siempre el mismo (tabla de estado por línea: `varia` = «-» en todas las líneas de estos
archivos; las que no están en la tabla —`BakeNebulosas`, `EnsureFpsTex`— se razonan desde su código, que pone el estado él mismo): suavizado
en los hornos, el de fábrica en lo demás, medio píxel y copia de fábrica, la letra suave en el contador.

## Costuras (la regla del puente: va del lado de lo que no está traducido)

1. **Las sobrecargas con `Bitmap`** (`Plataforma/Puente.frontera.cs`): `GpuStarfield.CreateOverlay`, `CreateCoverShared`, `UpdateOverlay`,
   `UpdateOverlayRegion` y `GpuPanel.Upload` con la firma de XP toman los puntos con `PuenteDibujo.Mapa` (los mismos, sin copia y sin
   hacerse dueño) y llaman a la traducida. Las llaman archivos de otros grupos o sin traducir: `BuscadorLigeroGpu.cs`, `BuscadorPortadasGpu.cs`,
   `CaratulasAdaptativas.cs`, `CaratulasSeparadasGpu.cs`, `FranjaLigeraGpu.cs`, `RespuestaGpu.cs` (los envoltorios) y `AsistenteEstrellaGpu.cs`,
   `AtraccionGpu.cs`, `AyudaCasillaGpu.cs`, `DiscoVivoGpu.cs`, `FranjaLigeraGpu.cs`, `PantallaVivaGpu.cs`, `RespuestaGpu.cs`, `SuenaVivoGpu.cs`,
   `TextoMovilGpu.cs` (`Upload`). Se quitan cuando esos archivos pasen `SKBitmap`; todas, al final, con `Dibujo2D.cs`. El dispositivo
   (`DispositivoTarjeta`) no lleva puente: solo lo llamaban `D3D9Star.cs`, `TexturasCompatiblesGpu.cs` y la grabadora de cintas.
2. **Los campos compartidos con otros archivos se quedan con su tipo** (`Rocola.Dibujo2D.Bitmap`, escrito entero, con su nota): `GpuCubeInfo.CoverA`
   y `CoverB`, `GpuGridCell.Bmp`, `Preview` y `FullCover`, `GpuTrans.Bmp`, `GpuWin.Bmp`, `GpuToast.Bmp`, `CellTex.Bmp`, `transLastBmp`,
   `winLastBmp`, `lastContour`, `selImgA`, `selImgB`, `relImg`, `miniImg`, `idleLastBmp`, `GetUi`, `GetUiBot`, `GetIdlePanel`, `GetPieza`,
   `GetContour` y las firmas de `EnsureCover`, `EnsureImmutableCover` (la llama `RespuestaGpu.cs`) y `SubeEstadoExtendido`. Sus productores
   están en archivos de otros grupos. `D3D9Star.cs` compara por identidad como XP (no vuelve a subir la misma imagen) y convierte al subir,
   en nueve sitios, con `PuenteDibujo.Mapa(...)`. Cuando los productores den `SKBitmap`, cambia el tipo y se va el `Mapa`.
3. **`TeselasVisibles.De`** (`DibujoLigeroGpu.cs`, de otro grupo) lee un `Bitmap`: recibe el mapa horneado (vía láctea, nebulosas, viñeta)
   prestado por `PuenteDibujo.Bitmap(sk, Format32bppArgb)`, con el formato con que XP lo creaba (así lo lee por el mismo camino: `LockBits`
   Format32bppArgb). El prestado no suelta los puntos; el horno sí, al acabar. Se va cuando `DibujoLigeroGpu.cs` reciba `SKBitmap`.
4. **`PreparaAyudaIcono`** (`AyudaCasillaGpu.cs`, otro grupo) sigue entregando `PuenteDibujo.Entrega(BakeInterrogacion(...))` a `Upload`: entra
   por la sobrecarga con `Bitmap` y sube lo mismo. Ese grupo puede pasar ya el `SKBitmap`.
5. **Un `Bitmap` ya soltado** (`Dispose`) no tiene puntos: `PuenteDibujo.Mapa` da `null` y la traducida hace lo que hace con un `null` (ver
   «Riesgos»).

## Herramientas de pruebas que se tocaron

- **El volcado de las texturas que se suben vuelve** (`Nativo/VolcadoTexturas.cs`, nuevo). Con `ROCOLA_VOLCADO_TEXTURAS=carpeta`, la capa
  de la transición (`Direct3DaGL.cs`, `UnlockRect`) guardaba en PNG cada zona de 64×64 o más que se subía: es lo que comparan los
  recorridos (`VOLCADO=1`, `HUELLAS=1`). La traducción de la tarjeta quitó `Direct3DaGL.cs` y con ella el volcado: desde la base e78ff94
  la variable no hacía nada (`strings` del programa de la base: 0 veces `ROCOLA_VOLCADO_TEXTURAS`; de la referencia: 1) y los recorridos
  de la traducción salían sin texturas. Vuelve con el mismo nombre de archivo y los mismos puntos (los subidos, sin premultiplicar), al
  final de la línea del `glTexSubImage2D` de las dos subidas de XP (`SubeBitmapBorde` y `ActualizaRegionBitmap`), con la marca
  `(pruebas: ROCOLA_VOLCADO_TEXTURAS, Nativo/VolcadoTexturas.cs)` como la grabadora de cintas. Sin la variable no hace nada (`static
  readonly` falso). Comprobado: las huellas del atlas de estrellas y del halo que vuelca la traducción son las de la referencia en
  `cmp-ref` (`128x128 be9590f3a75eadd5`, `64x64 e75ea543761c0023`). **Los otros grupos de esta tanda no lo tienen en su copia**: sin él,
  sus recorridos no comparan texturas.
- **La grabadora de cintas** (`Nativo/CintaTarjeta.cs`, de la pista de la tarjeta): sus cinco métodos de subida reciben `SKBitmap` (el id
  es el del `SKBitmap`; el formato que apunta es siempre Format32bppPArgb, 925707: como guarda los puntos un `SKBitmap`; el dispositivo no
  lo mira). Sin esto el programa no compila (la grabadora llama al dispositivo); la otra salida, darle a la grabadora un `Bitmap` prestado,
  hacía que el préstamo cambiara de formato y de identidad mientras se graba.
- **El reproductor de cintas** (`Pruebas/Tarjeta/Reproductor.cs`): a la traducción le da el `SKBitmap` grabado y a la referencia, como
  antes, el `Bitmap` de la capa; de un método con las dos firmas toma la que no es de la capa.
- **El arnés de la frontera** (`Pruebas/VistasFrontera`, nuevo; patrón de `Pruebas/BancoVistas`): `Frontera.cs` se compila con el programa
  entero de esta copia o de la referencia (`-p:Fuente=...`, el proyecto copiado fuera de la referencia) y llama con datos fijos a lo que
  los recorridos no pasan: (1) los hornos de `D3D9Star.cs` con todas las medidas que usa XP —la cuarta parte de las pantallas de 1024×768,
  1280×1024, 800×600, 1366×768, 1600×900, 640×480, 1920×1080, 1366×1024 y las mínimas (`Math.Max(8, Width / 4)`), el atlas de 64 y el halo
  de 64, la viñeta de 128— y otras, y los tres cuya firma cambió; (2) la subida con mapas de prueba al azar con semilla fija (opacos, con
  alfa, con los alfas de las orillas 0, 1, 2, 32, 128, 254 y 255, de 1×1, finos, grandes, con zonas que tocan el borde de la potencia de 2
  y una que se sale, portadas compartidas, y con el límite de textura en 256: en teselas); (3) el contador de cuadros en un `GpuPanel` sin
  construir, con números fijos y tres anchos. `compara.sh` lo corre en la referencia y en la traducción —por `SKBitmap` y por el puente con
  `Bitmap`— con las letras de XP falsas y con DejaVu, y compara lo que escribe y las texturas volcadas. `cintas.sh` pasa las cintas y sus
  variantes contra las salidas de referencia.

## Comprobaciones (con números)

- **Compila** (`dotnet build -c Release`): 0 errores y los mismos 15 avisos de la base, los mismos archivos y líneas (`Thread.VolatileRead`,
  `FormatterServices`, `Control.bounds`, dos `Stream.Read`, un `stackalloc`), en código que esta pista no toca: la lista completa de
  avisos, compilando desde cero (`--no-incremental`), sale idéntica antes y después.
- **Banco por función** (`Pruebas/BancoDibujo`, porque se tocaron `Nativo/` y el puente): semillas 7 y 11 con las letras de XP falsas y la 5
  sin ellas, 3 000 casos cada una, ejecutores `gdi`, `nativo` y `puente` contra el congelado: **0 distintos** en los 27 000 casos;
  `puente-objetos`: **407 comprobaciones bien, 0 mal**.
- **Cintas de la tarjeta** (`Pruebas/VistasFrontera/cintas.sh`, el reproductor dándole `SKBitmap` a la traducción): las 17 cintas de
  `/root/rocola-trabajo/port/tarjeta/cintas` (2 887 cuadros) y sus 12 variantes (`--limite-textura 256` en 00, 01, 08, 09, 10, n1 y n2, 64
  en 00, `--redimensiona` en 05, 06 y dos en 09): **29 de 29 iguales** a las salidas de la referencia, línea por línea (6 616 cuadros: el
  SHA-256 del fondo, los contadores y `glGetError` 0, y lo que devuelve cada llamada). La salida guardada de
  `09-medir --redimensiona 40:1280x1024,200:800x600` era de una versión vieja del reproductor (sin la línea «llamadas por método»): rehecha
  en la referencia con este reproductor, igual (y la vieja, igual sin esa línea). El reproductor nuevo da con la referencia la misma salida
  guardada (00). Controles negativos (el programa con un fallo puesto, compilado aparte: `control.sh` y, para la zona en teselas, lo mismo
  a mano con el límite de textura): sin el +0,5 del vértice, 16 de 16,
  335 de 335 y 109 de 109 cuadros distintos (como en la pista de la tarjeta); subir sin el alfa (`Rgb32` en `SubeBitmapBorde`), 13 de 16,
  335 de 335 y 109 de 109; la zona sin su última fila (`ActualizaRegionBitmap`), 7 de 16 (00 con límite 64), 112 de 335 (01 con límite 256)
  y 68 de 97 (03) (quitando solo el alfa de la zona salen 0 de 16 y 0 de 335: lo que suben por zonas esas cintas es opaco; por eso el
  control de la zona es el de la última fila).
- **Arnés de la frontera** (`compara.sh`): 61 mapas de los hornos, 46 operaciones de subida y 5 del contador; en la traducción por `SKBitmap`
  y por el puente, con las letras de XP falsas y con DejaVu (4 corridas): las 106 líneas **iguales** a la referencia y las **153 texturas
  volcadas iguales** (orden, medida, zona y huella).
- **Recorridos del programa entero** (los nueve, `VOLCADO=1 CAPAS=1 HUELLAS=1`, con el candado y la carpeta de siempre, contra las dos
  corridas de la referencia `cmp-ref` y `cmp-ref2` con `compara.py`; salida en `/root/rocola-trabajo/port/frontera/cmp`): **las 217 capas
  de Dibujo2D quietas, idénticas** (218 iguales y 3 distintas, las tres de las que ya cambian entre las dos de la referencia: `hojas` c16.5
  y `video` c21). Texturas (con el volcado que vuelve, arriba): A 603, B 585, idénticas 488; **quietas 487, en B 476**. Las 11 que faltan
  (`pared2` 5, `ayuda` 4, `buscador` 2) son de la hora, como en D2 y D4, y la máquina estaba muy cargada (carga media de 6 a 9 en 4
  núcleos: otros agentes). Pruebas:
  - otra corrida de la traducción de esos tres recorridos (B2): 9 de las 11 salen;
  - la referencia otra vez, ahora y con la misma carga (REF3, en mi carpeta; las de `cmp-ref` no se tocaron): a ella también le faltan 9
    de las 242 quietas de esos tres recorridos (`pared2` 6, `ayuda` 2, `buscador` 1);
  - de las 4 que faltan en las dos corridas de la traducción, tres le faltan también a REF3: la de 1024×256 de `pared2` a los 41,9 s
    (en ese instante B sube otro contenido, a los 42,1 s), y dos de 1024×128 que A y A2 suben más veces (`ayuda` c9e4fc67: 2, 2, y B, B2 y
    REF3: 1; `buscador` 584659ad: 4, 4, y 3, 3, 3); la cuarta (`buscador` e843916c, la caja del buscador entre dos letras, a los 8,6 s) sale
    en un tercer par corrido seguido con los PNG (referencia y traducción): las 9 subidas de los 7 a los 9 s, iguales en huella y orden en
    los dos, con e843916c y 584659ad;
  - con las dos corridas de la traducción como pareja (lo quieto de la traducción, 238 texturas en esos tres recorridos), REF3 tiene 236;
    las 2 que no, por la hora del lado de la referencia (una que A2 y REF3 suben dos veces y las demás tres; otra que solo REF3 no sube);
  - texturas de B que no salen ni en A ni en A2: 84 (estados intermedios de la franja de 1024×128 en las animaciones); en esos tres
    recorridos, B 43, B2 51 y REF3 42.
- **NativeAOT** (`dotnet publish -c Release -r linux-x64`): **ningún aviso IL ni AOT** (los 15 del compilador, los de siempre). El
  binario nativo corrió fuera de pantalla (`Pruebas/Tarjeta/guiones/corre.sh`, 36 s) por las pantallas de esta pista: la estrella en 2D
  (`BakeStar`, la de la luna) y las nebulosas sin el cielo profundo (`CONFIG` `estrella_3d=0 luna_realista=1 luna_activa=1 luna_protagonista=1 cielo_profundo=0`),
  el contador de Ctrl+F1, la pared, el disco, las páginas y el buscador escribiendo (`ROCOLA_PULSA`), cuatro capturas
  (`ROCOLA_CAPTURA=...`, `ROCOLA_CAPTURA_SALE=1`): **sin excepciones** en lo que escribe; en `rocola_error.txt` solo «Volumen de Windows al
  arrancar: no se pudo (no hay mezclador de altavoces)», sin excepción, como en los datos de prueba de la referencia. La captura de los
  12 s enseña el contador («GPU D3D9: 31 FPS (tope 66)»), la estrella 2D, el atlas y las nebulosas. En esa corrida se grabó una cinta
  (`f1-frontera-aot`, con la grabadora en `SKBitmap`: 7 031 registros, 2 `CreateTextureFromBitmap`, 26 `CreateTexturePow2` y 150
  `UpdateTexturePow2`): **igual** en la referencia y en la traducción, 182 cuadros, también con `--limite-textura 256`.
- **diff contra XP** (`Pruebas/Tarjeta/guiones/diff-xp.py -v`): `D3D9Star.cs` 401 líneas quitadas y 265 puestas, 12 sin marca: 11 de
  la pista de la tarjeta (de antes de esta) y una llave de XP que no cambió y el diff mete en el trozo de `EnsureFpsTex`;
  `TexturasCompatiblesGpu.cs` 54 y 46, 5 sin marca (de la pista de la tarjeta); `EstrellaGpu.cs` 23 y 21, 1 (de D4: la continuación de
  una línea marcada); `CieloNaturalGpu.cs` 7 y 6, 0; `CieloVivoGpu.cs` 11 y 10, 0; `Form1.Fondo.cs` (finales de línea mezclados: el guion
  no los quita; con `diff --strip-trailing-cr`) 17 y 14, 0. De esta pista (`git diff` desde la base): todas las líneas puestas en `Codigo/`
  llevan «EN LINUX» salvo tres `return b;`, que vuelven al texto de XP.

## Riesgos y diferencias que quedan

- **Un `Bitmap` ya soltado que llega por el puente**: `PuenteDibujo.Mapa` da `null`. `UpdateOverlayRegion` devuelve entonces `false` donde la
  referencia lanzaba `NullReferenceException` (y GDI+ en XP, `ArgumentException`); `CreateCoverShared` con una portada soltada que todavía
  es llave de las compartidas devuelve 0 donde XP devolvía la textura compartida (buscaba por identidad sin tocar el mapa). Lo demás sale
  igual (las demás subidas ya devolvían 0 o `false` al fallar el `Width`). No pasa en las cintas, el arnés ni los recorridos.
- **Un `SKBitmap` soltado por su dueño traducido mientras otro sin traducir conserva su `Bitmap` prestado**: `PuenteDibujo.Mapa` da un
  `SKBitmap` sin puntos y leerlo es un fallo nativo (Skia), no la excepción de .NET que daba GDI+. Es del puente en general; esta pista no
  suelta ningún mapa que otro conserve.
- Las cintas grabadas desde ahora apuntan el formato 925707 (PArgb) en todos los mapas; el dispositivo no lo mira.
- La diferencia (a) de `PORT_DIBUJO.md` (los mapas Format32bppArgb de XP se guardan premultiplicados) sigue en los hornos de aquí, como en la
  referencia (la viñeta es negra con alfa y no pierde nada; el atlas, el halo, la vía láctea y las nebulosas sí, como allí).
- No se vio ningún fallo de XP en estas funciones.
- Nada de esto se probó en el A4.

## Lo que queda

- Que los demás grupos pasen `SKBitmap` a `Upload` y a los envoltorios de `GpuStarfield` (y se vayan las sobrecargas de
  `Puente.frontera.cs`) y que los campos compartidos de la lista de arriba cambien de tipo cuando sus productores estén traducidos.
- `TeselasVisibles.De` y `TrabajoTeselas` (`DibujoLigeroGpu.cs`) con `SKBitmap`: se va el préstamo de los tres hornos.
- Juntar `Nativo/VolcadoTexturas.cs` antes que las comparaciones de los demás grupos (sin él, sus recorridos no tienen texturas).
- Al final, borrar `Plataforma/Puente.frontera.cs` con `Dibujo2D.cs` y `Puente.cs`. Las líneas de pruebas de la grabadora y del volcado
  (`(pruebas: ROCOLA_CINTA, ...)` y `(pruebas: ROCOLA_VOLCADO_TEXTURAS, ...)`) se quedan mientras haga falta comparar.
