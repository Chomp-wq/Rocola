# Port nativo de XP: la cache de portadas, la de previas y la de celdas en SKBitmap (grupo «cache-portadas»)

Oleada 4 (tanda 7), sección «4a. cache-portadas» de `PLAN_TANDA4_A_N7.md`, 12-11-2026. Base: `809389d`; rama `port-cache-portadas`.
Decisiones del dueño en `AGENTS.md` (8-11-2026: traducir el código para que trabaje nativo en Linux, sin capas que imiten a Windows; lo
idéntico se asegura por el código). Nada de esto se probó en el A4.

## Qué se hizo

Hasta aquí la caché de portadas (y la de previas de 32×32) seguía guardando imágenes de la capa que imita a GDI+ (`Rocola.Dibujo2D.Image`
/ `Bitmap`), con `PuenteDibujo.Entrega` al entrar y `PuenteDibujo.Mapa` en cada sitio que las pintaba o las subía (la costura 1 de
`PORT_portadas.md`). Ahora guardan `SKBitmap`, y todo el que las pide, las compara o las sube recibe ese mismo `SKBitmap`:

| Dónde | En XP | Ahora |
|---|---|---|
| `Form1.Portadas.cs` | `coverCache`, `coverTrash`, el trabajador (`Image thumb`), `DecodeCover*`, `CoverEnCache`: `Image`/`Bitmap` | `SKBitmap`; las `DecodeCover*` devuelven lo de `DecodeCoverCore` tal cual (sin `Entrega`: las cuatro líneas de return vuelven a ser las de XP); la previa entra en su caché tal cual (la línea vuelve a ser la de XP) |
| `Form1.Biblioteca.cs` | `PortadaEnMemoria`, `LoadCover`, `LoadCoverAsync`, `RequestCover`, `LoadCoverByPath`, `RequestCoverByPath`, `CachePut`, `DrainCoverTrash*`, `RetienePortada`, `StartDiscTrans`, la portada de la atracción | `SKBitmap` (25 líneas; las 27 notas «la leen archivos aun sin traducir» arregladas) |
| `CaratulasAdaptativas.cs` | `gpuPreviewCache`, `PutGpuPreview`, `PeekGpuPreview`, `GpuPreviewStore.Get`/`FromBytes`, `LodTex.Bmp`, `WarmGpuPreview` | `SKBitmap`; `FromBytes` ya no entrega (`return b;`), `CreateOverlay(cell.Preview…)` vuelve a ser la línea de XP; fuera `using Rocola.Dibujo2D` (línea 3) |
| `PreviewsIndexadosGpu.cs` | `GetBatch` da `Bitmap[]` | `SKBitmap[]` |
| `D3D9Star.cs` | `GpuCubeInfo.CoverA/B`, `GpuGridCell.Preview/FullCover`, `GpuTrans.Bmp`, `transLastBmp`, `selImgA…miniImg`, `EnsureCover`, `EnsureImmutableCover` | `SKBitmap`; la subida (`Upload`, `CreateCoverShared`) vuelve a ser la línea de XP |
| `RespuestaGpu.cs` | `CoverSlot.Bmp`, `EnsureRecentCover` | `SKBitmap` |
| `CaratulasSeparadasGpu.cs` | `nativeCovers` (clave por identidad), `InteriorOpaco(cell.FullCover)` | `SKBitmap`; `InteriorOpaco(SKBitmap, bool)` con el formato de XP de la portada (abajo) |
| `Form1.Pared.cs`, `Form1.Buscador.cs`, `Form1.Vistas.cs`, `Form1.cs`, `Form1.Video.cs`, `Form1.Sonido.cs`, `Form1.Datos.cs:503` (`transImg`), `Form1.Fondo.cs` (`ColoresDisco.Fuente`, `ColorDePortadaDe`), `FluidezGpu.cs`, `AtraccionGpu.cs`, `EstadoReposoGpu.cs`, `BuscadorPortadasGpu.cs`, `PantallaVivaGpu.cs`, `DiscoVivoGpu.cs` | las portadas y previas que pasan, se comparan o se pintan | `SKBitmap` y la llamada directa (sin `Mapa`); 9 líneas vuelven a ser idénticas a las de XP (`Form1.Pared.cs` 102-104 y 993, `AtraccionGpu.cs` 955, `EstadoReposoGpu.cs` 147, `PantallaVivaGpu.cs` 842, y las de arriba) |

Cuentas: de `PuenteDibujo.Mapa` en `Codigo/` se quitan 32 y se añade 1 (de 48 a 17); de `PuenteDibujo.Entrega`, 6 llamadas (las de
`Form1.Portadas.cs` 644, 884, 888, 892 ×2 y `CaratulasAdaptativas.cs` 379); las 7 que quedan son de la interfaz (`uiOverlay`,
`uiBotOverlay`, `uiPieza`, `winSnap`, la moneda), de la oleada 5. `FluidezGpu.cs:3` es otra vez `using System.Drawing;` más `using
SkiaSharp;` (nota EN LINUX) y `CaratulasAdaptativas.cs:3` ya no ve la capa. Notas viejas arregladas en los archivos del grupo: las 4 de
`Form1.Portadas.cs` y las 27 de `Form1.Biblioteca.cs` («la leen archivos aun sin traducir»), `Form1.Pared.cs:134` y `:145`,
`Form1.Fondo.cs:771` y `:1021`, `CaratulasAdaptativas.cs:507`, `BuscadorPortadasGpu.cs:495` y `:765`, `PantallaVivaGpu.cs:772` y `:574`
y las cabeceras (línea de los `using`) de `Form1.Pared.cs`, `Form1.Buscador.cs`, `Form1.Vistas.cs`, `Form1.Video.cs`, `Form1.cs`,
`CaratulasSeparadasGpu.cs`, `AtraccionGpu.cs` y `PantallaVivaGpu.cs` que decían que las portadas eran de la capa.

### El formato de XP de cada caché (`Form1.FormatoPortada`)

Un `SKBitmap` es siempre BGRA premultiplicado; en XP cada `Bitmap` llevaba su `PixelFormat`, y dos lectores de portadas lo miran: la
paleta (`ExtractCoverPalette`, que lee según `FormatoMapa`) e `InteriorOpaco` (un mapa sin alfa ni paleta es opaco sin mirarlo: el riesgo
del JPEG de 24 bits). En XP **toda** portada de la caché sale de `DecodeCoverCore` como `Format32bppPArgb` (`new Bitmap(256, 256, PArgb)`,
`cb.Clone(…, PArgb)` de la guardada —aunque la guardada sea un JPEG de 24 bits— o `CopiaMiniaturaGpu`, que hace otra PArgb), y toda previa
como `new Bitmap(32, 32, PArgb)`. Así que el formato es un dato de la caché, no de cada mapa: `internal const FormatoMapa FormatoPortada =
FormatoMapa.PArgb32` (en la línea de `coverCache`, `Form1.Portadas.cs:23`, para no añadir líneas). Lo usan `ExtractCoverPalette(…,
FormatoPortada)` (trabajador de portadas y `ColorDePortadaDe`) e `InteriorOpaco(cell.FullCover, FormatoPortada == PArgb32 ||
FormatoPortada == Argb32)` (`Image.IsAlphaPixelFormat` de un formato de 32 bits con alfa: verdadero, se mira punto a punto, como en XP).
El arnés lo comprueba: la referencia da `PArgb32` en las 204 salidas de las cachés (13 portadas con «JPEG» encendido y apagado, guardadas
en PNG y JPEG, leídas, publicadas, del calentador y las previas), y la traducción escribe el declarado; las dos salidas son iguales.

### La portada soltada (`Nativo/MapaVivo.cs`, nuevo)

En XP leer la medida de una `Image` soltada lanza `ArgumentException` (GDI+), y en cuatro sitios XP lee la medida de una portada que puede
venir de la caché **y atrapa** lo que lance: `CreateTexturePow2` y `UpdateTexturePow2` de `D3D9Star.cs` (devuelven `IntPtr.Zero`/false),
`ColoresDePortada` (`Form1.Fondo.cs`; el trabajador de portadas la atrapa y se queda sin tonos) y `Escala` (`BuscadorPortadasGpu.cs`;
`Miniatura` la atrapa: «la portada se soltó mientras tanto»). Con un `SKBitmap` soltado leer `Width` es una violación de segmento que cierra
la rocola, y antes de este grupo el puente lo disimulaba (`PuenteDibujo.Mapa` de una soltada daba `null`). En esas cuatro líneas la
medida se lee ahora con `MapaVivo.Ancho/Alto`, que comprueba el `Handle` y lanza `ArgumentException("Parameter is not valid.")` como GDI+
antes de tocar Skia (una sola condición más por lectura; con un mapa vivo da lo mismo). Donde XP no atrapaba (p. ej. `texture.Bw =
cell.FullCover.Width` en `CaratulasSeparadasGpu.cs`, `ReserveAheadSpace`), la línea es la de XP: allí XP también fallaba, y a esos sitios
la caché nunca da una soltada (las suelta `DrainCoverTrash` en el hilo de la pantalla y solo las que nadie retiene).

## Costuras

| Costura | Qué | Se quita cuando |
|---|---|---|
| Quitada: `SaveThumb(Bitmap)` (`Puente.portadas.cs:21`) | el calentador (`Form1.cs`) ya guarda la copia como `SKBitmap` | — |
| Quitada: `InteriorOpaco(Bitmap)` (`Puente.portadas.cs:34`) | `CaratulasSeparadasGpu.cs` llama a la traducida con `FormatoPortada` | — |
| Quitada: la costura 1 de `PORT_portadas.md` (la caché de la capa, `Entrega`/`Mapa`) y la 7 (`DecodeCoverParaCalentar` con `Bitmap`) | — | — |
| **Nueva**: `PuenteDibujo.Mapa(gw.Bmp)` en `D3D9Star.cs:2603` | `EnsureCover` recibe ahora `SKBitmap` y lo comparten la portada que vuela y la instantánea del buscador (`winSnap`, `GpuWin.Bmp`), que sigue siendo un `Bitmap` de la capa (oleada 5: `Form1.Biblioteca.cs:3002-3020`, `Form1.Datos.cs:508`). `Mapa` da el mismo `SKBitmap` para el mismo `Bitmap`: la identidad que compara `EnsureCover` es la de XP; `winSnap` se suelta y se pone a `null` en la misma línea, así que no llega una soltada | 5a haga `winSnap`/`GpuWin.Bmp` `SKBitmap` |
| Muertas, se quedan (archivos de la limpieza 3b o de N7): `ColoresDePortada(Bitmap)` (`Puente.fondo.cs:32`), `ExtractCoverPalette(Bitmap)` y `FormatoMapaPared` (`Puente.pared.cs:54` y `:65`) | ya no las llama `Codigo/` (los arneses `VistasFondo` y `VistasPared` todavía las llaman directo para comparar) | N7 / la limpieza |
| Quedan (oleada 5): `TeselasEnPasos(Bitmap)` y `SubeReponiendo(Bitmap)` de `Puente.portadas.cs` | la interfaz y las piezas siguen siendo `Bitmap` | 5a (borra `Puente.portadas.cs` entero) |
| `Form1.Pared.cs:134` sigue `partial` | la parte de `Cube` en `Puente.pared.cs` la quita 3b; si se quitara aquí y 3b no entrara a la vez, no compilaría | cuando 3b esté junta |

Otro archivo tocado fuera de los rangos del plan, mínimo: `D3D9Star.cs:744` y `:790` (`MapaVivo` en `CreateTexturePow2` y
`UpdateTexturePow2`, arriba) y `:2001`/`:2603` (la instantánea). `D3D9Star.cs` no está en otro grupo de esta oleada.

## Arneses

- `Pruebas/VistasPortadas` (del grupo «portadas»): el formato de lo que publican las cachés se escribe como `FormatoMapa` (en la referencia,
  del `PixelFormat` del `Bitmap`; en la traducción, el declarado); `SaveThumb` e `InteriorOpaco` sin los puentes quitados (la traducción
  recibe en el arnés lo que XP miraba del formato). **Casos nuevos** (`Cache`): un `Form1` sin construir con la caché de verdad —
  identidad (`CachePut`, `CoverEnCache`, `PortadaEnMemoria`, `LoadCover`, `LoadCoverByPath`, `LoadCoverAsync` dan el MISMO objeto; al
  reemplazar, la de antes va a la papelera tal cual; la misma no; la expulsión por el tope; `PutGpuPreview`/`PeekGpuPreview`; 
  `DrainCoverTrashAcotado` suelta las que nadie usa y deja la que vuela, `transImg`) y **portada soltada** (`ColoresDePortada`, el
  trabajador y `Escala` lanzan y se atrapan como en XP; `Escala` con una viva sí da la miniatura; la última línea dice que el programa
  sigue). La referencia lanza `NullReferenceException` (la capa) y la traducción `ArgumentException` (la de GDI+): la salida dice
  «lanza» y el tipo va a la salida de errores. `control.sh`: «InteriorOpaco sin el formato» va ahora contra `DibujoLigeroGpu.cs`, y tres
  controles nuevos.
- `VistasPared`, `VistasBuscador`, `VistasBuscadorPortadas` y `VistasVistasB`: `Pon` (y `Delegado`) dan a la traducción las portadas de
  prueba como `SKBitmap` (`Image.Sk`, el mismo para la misma `Image`), también dentro de los delegados (`GetCover`, `PeekCoverGpu`,
  `GetCoverByPath`, `GetCoverYa`) y los arreglos (`Portadas`); a la referencia, tal cual.

## Comprobaciones (con números)

Salidas en `/root/rocola-trabajo/port/cache-portadas` (fuera del repositorio; sin binarios ni PNG).

- **(a) Compila** (`dotnet build -c Release --no-incremental`): 0 errores, 15 avisos, la misma lista archivo:línea:aviso que la base.
- **(b) diff-xp.py -v** de los 21 archivos: 0 líneas sin marca nuevas (las «sin marca» por archivo son las de la base: Form1.Biblioteca 2,
  Form1.Pared 1, Form1.Vistas 2, Form1.cs 2, Form1.Fondo 2, Form1.Video 4, Form1.Sonido 10, Form1.Datos 2, D3D9Star 12, AtraccionGpu 2).
  Quitadas/puestas: Form1.Portadas.cs 75/72 → 70/67; Form1.Pared.cs 234/231 → 230/227; CaratulasAdaptativas.cs 50/47 → 49/46;
  AtraccionGpu.cs, EstadoReposoGpu.cs y PantallaVivaGpu.cs una menos; Form1.Fondo.cs +2 (las dos de `MapaVivo` y la paleta), D3D9Star.cs
  +1, FluidezGpu.cs +1, Form1.Datos.cs +1 (`transImg`), BuscadorPortadasGpu.cs +1 (`Escala`). Los 109 archivos de `Codigo/` con el mismo
  número de líneas y de CRLF que `809389d`.
- **(c) NativeAOT** (`dotnet publish … -p:PublishAot=true`): 0 avisos IL/AOT; los 15 del compilador, la misma lista.
- **(e) Arneses**, todos contra la referencia congelada:
  - portadas: **478 líneas IGUALES** (466 de antes + 12 nuevas), 0 excepciones, 0 «DIRECTO DISTINTO», 104 no nativos comparados.
    Controles nuevos: la caché que da una copia → 10 líneas distintas; reemplazar sin mandar la de antes a la papelera → 14;
    `MapaVivo` sin comprobar → **violación de segmento** (salida 139, faltan las 5 últimas líneas); InteriorOpaco sin el formato → 14.
  - pared: 1 715 líneas IGUALES con las letras falsas y con DejaVu (673 llamadas directas).
  - buscador: 4 709 IGUALES (falsas y DejaVu); buscador-portadas: 3 128 IGUALES (falsas y DejaVu; 5 excepciones en las dos, las de
    siempre); vistas-a: 1 266 IGUALES (las dos); vistas-b: 0 distintas de 2 865 (las dos); atracción: 19 120 IGUALES (las dos);
    pantalla-viva: 768 IGUALES (las dos); reposo: 1 791 IGUALES (las dos); fondo: 2 763 con solo la primera distinta (`inicio
    traducida=True/False`, la marca, como en las juntas).
  - **Cintas** (`Pruebas/VistasFrontera/cintas.sh`): 29 de 29 iguales (6 616 cuadros).
  - Banco: no aplica (no se tocó `Puente.cs` ni `Nativo/` salvo el archivo nuevo `MapaVivo.cs`, que el banco no compila).
- **(d) Recorridos** (los nueve, `VOLCADO=1 CAPAS=1 HUELLAS=1`, con el candado y la carpeta de siempre, contra `cmp-ref` y `cmp-ref2`):
  los nueve salen con 0. **Capas de Dibujo2D quietas 215 de 217**: las dos que faltan son las de `hojas` a los 18 y 19,5 s (la hoja de
  Ctrl+F3 con lo que lleva medido el repaso de la biblioteca, que depende de la carga: `PORT_JUNTAS.md`), las mismas que en la corrida de
  la base de esta oleada (`armado-vistas`, 215 de 217). **Texturas quietas 475 de 487** en la primera corrida; otra de `ayuda`, `pared`
  y `pared2` (`cmp2`) recupera las de la primera que faltaban en esas tres (entre ellas una de 256×256 de `ayuda` a los 3 s,
  `b6b5d818…`, que sale en 51 de las 53 corridas de los grupos y en 8 de los 9 recorridos de esta: la hora). Contando las huellas
  distintas, de 381 quietas salen 377 en una u otra corrida; las 4 que faltan son las de la hora que ya explican `PORT_frontera.md` y
  `PORT_portadas.md` (`buscador` 1024×128 `e843916c` a los 8,6 s y `8cd9108c` a los 25,9 s; `pared2` 1024×256 `2a697d30` a los 41,9 s y
  1024×128 `2f591363` a los 62,4 s). Ninguna portada de 256×256 ni previa falta.
- **(f) Escenas alternadas con la referencia** (`alterna.sh`, etiquetas `cachepR`/`cachepN`, comparadas con el ruido de ventanas2):
  arranque, moneda, video, buscador, tab y captura **IGUALES** (6 de 6). **NativeAOT** (`cachepRA`/`cachepA`, `touch -r` de dotnet en
  `rocola-xp`): arranque y buscador **IGUALES**.

## Riesgos

- **Una portada soltada que llegue a un lector sin comprobar cierra la rocola** (violación de segmento) donde XP lanzaba una excepción
  y, fuera del reposo, «apuntaba y seguía». Solo se comprueba donde XP atrapaba (las cuatro de arriba). En los caminos de XP no llega
  ninguna: la caché saca la portada antes de soltarla y `DrainCoverTrash*` la suelta en el reloj de la pantalla, saltándose las que
  retienen el cubo, el mini-cubo, la cápsula de la cola, el buscador y la que vuela. Quien traduzca algo que guarde una portada entre
  cuadros tiene que conservar esa retención. `GpuPreviewStore.Small` y `Imagenes.Pinta` no comprueban (el trabajador le pasa una
  miniatura privada que nunca está soltada).
- **El formato ya no viaja con el mapa**: si alguna vez la caché guardara algo que en XP no fuera PArgb, `FormatoPortada` mentiría. Hoy
  todas salen de `DecodeCoverCore`/`FromBytes`, y el arnés lo comprueba en 204 salidas.
- `PutGpuPreview` manda a la papelera la previa NUEVA cuando ya había otra (`if (b != old) coverTrash.Add(b)`), y deja la vieja: así es en
  XP y se conserva.
- `MapaVivo` lanza el mensaje en inglés de .NET Framework («Parameter is not valid.»); nadie lo lee (los `catch` de XP no lo miran).
- Nada se probó en el A4.

## Lo que queda

- La interfaz y las piezas (`uiOverlay`, `uiBotOverlay`, `uiPieza`, `winSnap`, la moneda, el panel de reposo) como `SKBitmap` (5a): con
  ellas se van la costura nueva de `D3D9Star.cs:2603`, `TeselasEnPasos`/`SubeReponiendo` con `Bitmap` y `Puente.portadas.cs` entero.
- Las costuras muertas de `Puente.fondo.cs:32` y `Puente.pared.cs:54/65` (N7 o la limpieza), con los arneses que las llaman.
