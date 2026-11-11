# Port nativo de XP: el cielo del dibujo por procesador, la luna, el grano, el contorno redondeado y el color de las portadas, de GDI+ a SkiaSharp directo

Grupo «fondo» de la tanda 3 del port nativo (el dibujo con GDI+ de lo que queda), 11-11-2026: `Form1.Fondo.cs` entero (lo que quedaba
despues de la tanda 2, que ya habia dejado `BakeRuidoGrande` devolviendo `SKBitmap`). Decisiones del dueno en `AGENTS.md` (8-11-2026: «que
tradujeras todo el codigo para que trabajara nativamente en Linux», «ningun layer de traduccion»; el dibujo por procesador «se traduce igual,
con el mismo aspecto») y la receta en `PORT_DIBUJO.md` (D2-D4, `Pruebas/BancoDibujo/EjecutaNativo.cs`). Base: `ae8b50b`; rama `port-fondo`.
Aqui va lo que se tradujo, el estado del `Graphics` de cada funcion, cada costura con lo que todavia no esta traducido (con su razon y cuando
se quita), las comprobaciones con sus numeros y lo que queda. Nada de esto se probo en el A4 (no se puede desde aqui): todo es fuera de
pantalla con llvmpipe.

## Que se tradujo

`Form1.Fondo.cs` ya no ve los tipos que imitan a GDI+ (`using Rocola.Dibujo2D` fuera; `using System.Drawing; using Rocola.Nativo; using
SkiaSharp;` en la misma linea de antes); los de la capa solo quedan escritos enteros (`Rocola.Dibujo2D.X`) en las costuras de abajo. Todo su
dibujo va con SkiaSharp (`SKCanvas`, `SKPaint`, `SKPath`, `SKShader`, `SKBitmap`) y las funciones nativas (`Pixel`, `Imagenes`, `Degradado`,
`Mapas`, `MatrizColor`, `RecorteLienzo`, `ColorSk`), linea por linea como en XP: cada linea que cambia lleva su `// EN LINUX: ...` (247), el
archivo conserva su numero de lineas (2 403 contando la ultima vacia, las mismas que en la base: los `using` de XP ya estaban juntados en una
linea desde la fase 1) y sus finales de linea mezclados (2 343 CRLF y 59 LF solos, linea por linea los de la base; BOM igual). La logica de XP
no se toco: mismos valores, mismo orden, mismas ramas.

| Funcion | Lo que dibuja | Como quedo |
|---|---|---|
| `BakeRuido`, `BakeRuidoGrande` | el grano del cielo (64x64 y el grande de la tarjeta) | `SKBitmap` (`Mapas.Nuevo`), `SetPixel` del `SKBitmap` con el color sin premultiplicar; el grande, el `TextureBrush` repetido como shader (`Degradado.Textura`, `Envoltura.Repite`) y `Pixel.Rellena` copiando |
| `BakeLuna` | la luna del cielo por procesador (el halo, el disco con sus manchas y el borrado de la sombra) | `SKBitmap`; cada `PathGradientBrush` es `Degradado.DeTrazo` en la caja del trazo (`GetBounds` = `SKPath.Bounds`), con su `Blend` (factores y posiciones) donde XP lo pone; `FillEllipse` = `Pixel.RellenaOvalo` con el suavizado y la copia en variables; `DrawImageUnscaled` = `Imagenes.Pinta` entera en su punto |
| `ContornoRedondo` (antes `RoundedPath`) | el contorno redondeado de casi todo (teclas, tarjetas, barras, globos) | un `SKPath` hecho como un `GraphicsPath` (regla 5): par-impar de fabrica, el primer arco con `forceMoveTo` y los otros tres unidos al anterior, `Close` = `CloseFigure`. Se llama `ContornoRedondo` y no `RutaRedonda` porque ese nombre ya lo tiene el `Redondo` de `PantallaVivaGpu.cs` (de `RectangleF` y `float`): con el mismo nombre, un `Redondo(Rectangle, int)` traducido mas tarde se iria a la de `RoundedPath` sin ningun aviso |
| `ColoresDePortada`, `ColorDePortada` | los dos tonos y el promedio de una portada | reciben un `SKBitmap`; `GetPixel` = `ColorSk.DeSk(SKBitmap.GetPixel)` (el color sin premultiplicar, el mismo que el `Bitmap` de la capa); el `try/catch` de XP sigue igual |
| `Starfield.Draw`, `RenderInto` | el cielo de la atraccion (cuadro horneado + fugaces en vivo) | `(SKCanvas, suave, filtro, medio, copia, ...)`: el estado del que llama como parametros; la copia del cuadro (`cache`) es un `SKBitmap` y su lienzo empieza con el estado de fabrica (`RenderInto` lleva el suyo en variables: el cielo sin suavizar, lo demas suavizado y bilineal) |
| `Starfield.DrawCached`, `BlitSkyAndStars`, `BlitEstrellas`, `Trozo` | el fondo de las franjas y del panel del disco | `(SKCanvas, filtro, medio, copia, ...)`; `BlitSkyAndStars` cambia la copia (cielo copiando, estrellas mezclando) en su variable y la deja como estaba, como XP le devuelve al `Graphics` su `CompositingMode` |
| `Starfield.DrawReactive`, `DrawBeatFlare`, `DrawShoots` | la pared que late con la musica y las fugaces | `(SKCanvas, suave, filtro, medio, copia, ...)`; `ClipBounds` = `RecorteLienzo.Limites` (exacto); `DrawLine`/`FillRectangle` de las fugaces = `Pixel.Linea`/`Pixel.Rellena` |
| `EnsureSkyBrush`, `EnsureSkyCache`, `PintaNebulosa`, `PintaViaLactea`, `EnsureNieblas`, `Mancha`, `PintaLuna`, `CapaAtenuada`, `EnsureDimIA`, `EnsureLayers`, `BakeStarFrame`, `BakeGlow`, `GlowFor`, `EnsureGlow`, `DrawRayos`, `DrawNucleo` | lo horneado del cielo (el degradado de tres paradas, las nebulosas, la via lactea, las nieblas, el grano, la luna tenida, la capa atenuada, las ocho capas del titileo, los resplandores, las rayitas y los nucleos) | `SKBitmap` y `SKShader`; el `LinearGradientBrush` vertical con `ColorBlend` = `Degradado.Lineal(caja, colores, posiciones, Degradado.Vertical, true)`; el `ImageAttributes` con su `ColorMatrix` = la matriz (`MatrizColor.Identidad` y sus celdas) y `Envoltura.Transparente` (la de fabrica) en `Imagenes.Pinta`; `FillPolygon` = `Pixel.RellenaPoligono` par-impar |
| `Bold` | la letra negrita de las etiquetas | sin cambios de tipo: da una `Rocola.Dibujo2D.Font` (costura con WinForms, abajo) |

## El estado del `Graphics`

Sale de `Pruebas/TrazaDibujo/estado_por_linea.tsv` (las lineas de este archivo que pasan los recorridos: 64) y, para las que no estan, del
codigo de quien llama; el arnes (abajo) lo pasa todo con ocho estados del que llama.

| Donde | Suavizado | Filtro | Copia | Medio pixel | Matriz y recorte | De donde sale |
|---|---|---|---|---|---|---|
| hornos (`BakeRuido*`, `BakeLuna`, `BakeGlow`, `BakeStarFrame`, nieblas, luna tenida, capa atenuada, cielo horneado, cuadro de `Draw`) | el que pone cada uno (en una variable) o el de fabrica | el que pone cada uno o el de fabrica (bilineal) | la que pone cada uno (`SourceCopy` donde XP la pone) | el de fabrica | identidad, sin recorte (mapas propios) | `Graphics.FromImage` y lo que el codigo de XP le pone despues; la tabla: siempre igual (`varia` = `-`) |
| `Draw`, `DrawReactive`, `DrawShoots` | parametro | parametro | parametro | parametro | en el lienzo | tabla, 2354 y 2357: `s,t,b,r` varian (`AntiAlias` desde la atraccion; `None` y `b=T r=hdc` desde un `OnPaint`) |
| `DrawCached`, `BlitSkyAndStars`, `BlitEstrellas`, `Trozo`, `PintaLuna`, `DrawBeatFlare` | no lo usan | parametro | parametro (y `BlitSkyAndStars` la cambia en su variable) | parametro | en el lienzo | tabla, 1861, 2065 y 2127: `b,r` varian (identidad o la base del HDC con su recorte) |

Ninguna funcion del archivo deja el estado cambiado a quien la llama (XP devuelve la copia en `BlitSkyAndStars` y no toca nada mas del
`Graphics` del que llama): el puente no tiene que devolver nada.

## Costuras con lo que no esta traducido (la regla: el puente va del lado de lo que no esta traducido)

| Costura | Quien la pide | Como va | Se quita cuando |
|---|---|---|---|
| `RoundedPath(Rectangle, int)` da un `GraphicsPath` (`Plataforma/Puente.fondo.cs`) | `Form1.Buscador.cs`, `Form1.Ajustes.cs`, `Form1.Vistas.cs` | el `GraphicsPath` nuevo se queda con el `SKPath` de `ContornoRedondo` (el mismo trazo; al soltarlo se suelta) | esos tres archivos pidan `ContornoRedondo` |
| `ColoresDePortada(Bitmap, out, out)` (`Puente.fondo.cs`) | `Form1.Portadas.cs` (`thumb`) y `ColorDePortadaDe` de este archivo (la portada de `LoadCover`) | `PuenteDibujo.Mapa(b)` (los mismos puntos); antes lee `b.Width` para que una portada ya soltada lance ahi, como en XP (que lee el ancho antes de ningun punto): la traducida, con un `SKBitmap` soltado, cerraria la rocola | `Form1.Portadas.cs` y `Form1.Biblioteca.cs` (`LoadCover`, la cache de portadas) den `SKBitmap` |
| `Starfield.Draw`, `DrawCached`, `DrawReactive` con un `Graphics` (`Puente.fondo.cs`) | `Form1.Pared.cs` (la atraccion y la pared), `Form1.cs` (las franjas), `Form1.Buscador.cs` y el `OnPaint` de `BgPanel` (este archivo) | `PuenteDibujo.Lienzo(g)` y `PuenteDibujo.Estado(g)` (suavizado, filtro, medio pixel, copia); no devuelven estado (no lo cambian) | esos archivos y los `OnPaint` (WinForms) se traduzcan |
| `BgPanel.OnPaint`: `Rocola.Dibujo2D.Graphics g = e.Graphics` | WinForms (sin traducir en esta tanda) | el tipo, escrito entero; `DrawCached` va por la firma de XP del puente | se traduzcan las ventanas (`OnPaint` con el lienzo de la hoja propia) |
| `Bold(float)` da una `Rocola.Dibujo2D.Font` | `Form1.cs` y `TransLabel` (la `Font` de una `Label`) | el tipo, escrito entero | se traduzcan las etiquetas (WinForms) |
| `ColoresDisco.Fuente` es una `Rocola.Dibujo2D.Image`; `ColorDePortadaDe` recibe la portada de `LoadCover` como `Rocola.Dibujo2D.Bitmap` | `Form1.Portadas.cs` la compara por identidad con la imagen de la cache | el tipo, escrito entero | `Form1.Portadas.cs` y `Form1.Biblioteca.cs` den `SKBitmap` |

**Costuras de otros grupos que se quitaron** (dependian de que este archivo diera un `SKPath`): `RoundedPath` en `AcabadoGpu.cs` (2),
`AsistenteGuiaRocola.cs` (1), `AsistenteRocola.cs` (1), `AyudaCasillaGpu.cs` (2), `BuscadorPortadasGpu.cs` (14), `EstadoReposoGpu.cs` (2),
`EstiloGpu.cs` (1) y `GuiaRojoGpu.cs` (5): antes `using (Rocola.Dibujo2D.GraphicsPath p = RoundedPath(...))` y se pintaba
`PuenteDibujo.Ruta(p)` (el mismo `SKPath`, sin copia); ahora `using (SKPath p = ContornoRedondo(...))` y se pinta `p`. Se cambiaron solo
esas lineas, sus comentarios y la nota de la cabecera de seis de ellos (que nombraba `RoundedPath` entre lo que llegaba por el puente).
`PORT_acabado-estilo.md`, `PORT_asistente.md`, `PORT_buscador-portadas.md` y `PORT_reposo.md` siguen nombrando esa costura: queda para quien junte.

## Comprobaciones

- **Compilacion** (`dotnet build -c Release` en `sistema/rocola/Xp`): 0 errores y los mismos 15 avisos de la base (`avisos-base.txt` en la
  carpeta de trabajo, la misma lista linea por linea; ninguno nuevo).
- **El banco** (`Pruebas/BancoDibujo`): no hace falta y no se corrio. No se toco ningun archivo de `Nativo/` ni `Plataforma/Puente.cs`; el
  banco compila esos y `Dibujo2D.cs` (lista escrita en `Nuevo/Nuevo.csproj`), no `Puente.fondo.cs`, asi que sus casos salen por construccion
  como en la base. Lo que este grupo anade al puente lo pasa el arnes.
- **El arnes** (`Pruebas/VistasFondo/arnes.sh`, salida en `/root/rocola-trabajo/port/fondo`): el programa entero de esta copia y el de la
  referencia congelada con otra entrada (`Fondo.cs`), las letras de XP falsas (el fondo no dibuja letras: una pasada). **2 761 lineas iguales**
  a la referencia (la unica distinta es la primera, que dice si es la traduccion); 1 354 de las llamadas, en la traduccion, van DIRECTO a la
  traducida (el lienzo y su estado, el `SKPath`, el `SKBitmap`), como la llamaran los archivos que la usan cuando se traduzcan:
  - el cielo del dibujo por procesador: `Draw`, `DrawCached` y `DrawReactive` en seis cielos (64x48 sin luna, la atraccion de 320x240 con
    colores variados y mas estrellas, 400x300 con las franjas despejadas, el color intermedio y el acento, una ventana de 300x180 de una
    pantalla de 512x384 corrida a (100, 120) con las dos nebulosas y la atenuacion de 0,45, 256x200 sin luna y con otro corte del negro, y
    1024x768), por ocho estados del `Graphics` del que llama (el de fabrica; suavizado; la base y el recorte del HDC como en un `OnPaint`;
    copia, medio pixel y vecino mas cercano; recorte parcial -la casilla del cubo-; escala con bicubico; giro; traslacion con decimales y
    copia) y seis variantes de cada uno (rehacer o no el cuadro, fugaces que salen y avanzan, latido por banda, deriva): 1 728 mapas, con
    las fugaces, la deriva y, una vez por cielo, la huella de cada cache interna (el cuadro, el cielo horneado, las nieblas, la luna y la luna
    tenida, la capa atenuada y las ocho capas del titileo). Lo que depende del reloj (`Environment.TickCount`: el azar de las fugaces,
    rehacer el cuadro, el paso de las fugaces y de la deriva, la capa del titileo) se fija por reflexion antes de cada llamada, igual en los
    dos programas;
  - el `OnPaint` de `BgPanel` (por la firma de XP del puente), en tres medidas y sitios, dos atenuaciones, dos acentos, con y sin recorte: 24;
  - los hornos: el grano (64x64 y el grande en 64, 100, 257 y 512), la luna en once alturas (16 a 1440), 45 resplandores y la paleta de 8;
  - el contorno redondeado: 7 rectangulos (tambien de 1x1, de ancho 0 y fuera del mapa) por 8 radios (-2 a 40) por los ocho estados,
    rellenos y trazados, por la firma de XP y directo (`ContornoRedondo` con `Pixel.Rellena`/`Pixel.Traza`): 896, con sus puntos, su
    relleno y su caja;
  - el color de las portadas: seis clases (degradado, dos colores, ruido con alfa, gris, negro, azul con huecos transparentes) en siete medidas
    (1x1 a 640x64), por la firma de XP, directo con el `SKBitmap` y por `ColorDePortada`; sin portada y una ya soltada (`NullReferenceException`
    en los dos, al leer el ancho).
- **Los arneses de los grupos de la costura de `RoundedPath`**, otra vez con esta copia (salidas en `/root/rocola-trabajo/port/fondo/otros`):
  `VistasBuscadorPortadas` 3 127 lineas iguales a la referencia (250 directas), con las letras falsas y con DejaVu; `VistasAsistente` 1 989
  iguales (520 directas), las dos; `VistasReposo` 1 790 iguales (640 directas), las dos; `VistasAcabadoEstilo/compara.sh`, 0 distintas de
  6 603 contra la referencia y 0 de 6 607 contra su base, las dos. (ese arnes pasa
  `AcabadoGpu.cs`, `EstiloGpu.cs` -`PintaTarjetaSiguiente`- y `GuiaRojoGpu.cs`).
- **Los recorridos** (`Pruebas/Recorridos`, los nueve, con el candado y la carpeta de siempre, `VOLCADO=1 CAPAS=1 HUELLAS=1`, contra las dos
  corridas de la referencia de D2; salida en `cmp`): 0 excepciones en lo que escribio la rocola y en `rocola_error.txt` en los nueve; todas
  las capturas. **Capas de Dibujo2D quietas: 215 de 217** (las franjas del fondo y el panel del disco por procesador de `botones` y `video`,
  9 y 16, todas). Texturas quietas: 447 de 487 en esa corrida, con la maquina a carga 6-7 (otros agentes compilando). Lo que falta:
  - **una textura de 128x128 que no tenia ninguna corrida anterior** (`ededef11...`, a los 0,5 s, en los nueve recorridos): es la luna del
    cielo vivo (`CieloVivoGpu.cs`), que no se hornea cuando esta iluminada menos del 4 % (`LunaMinima`). Las referencias se corrieron el 8-11
    (iluminada el 2 %) y hoy a las 19:30 UTC lo esta el 4,02 %: es la primera corrida con la luna. La referencia congelada corrida hoy
    (`cmp-ref-hoy`: `botones`, `video` y `buscador`) la sube igual, la misma huella; en `botones` sale identica a esta traduccion en las 29
    texturas;
  - `buscador` subio solo 69 texturas en 36 s (452 composiciones: iba lento) y `video` 56. Otra vez, con la misma carga que `cmp-ref-hoy`
    (`cmp2`): `video` 50 de 50 quietas (la referencia de hoy, 50) y `buscador` 75 de 78 (la de hoy, 76). Juntando mis dos corridas, faltan
    solo las de la hora ya apuntadas por otros grupos (`PORT_asistente.md`, `PORT_frontera.md`, `PORT_JUNTAS.md`): `584659ad` (en `pared`,
    `pared2` y `buscador`), `e843916c` (`buscador`), `c9e4fc67` y `1f1e16c5` (`ayuda`), `2a697d30`, `21d8fe91`, `2f591363` (`pared2`) y dos
    franjas de `pared2` (`906bfb35` de 1024x64 y `1f1712f1` de 1024x128) que salen en 39 y 28 corridas de otros grupos y de la referencia; la
    referencia de hoy tampoco tiene dos de las 1024x128 quietas de `buscador`;
  - las dos capas: la hoja de Ctrl+F3 de `hojas` a los 18 s (la que explica `PORT_JUNTAS.md`: el repaso de la biblioteca va mas despacio con
    la maquina cargada) y la de Ctrl+F2 de `ctrlf3` a los 40 s, que en esta corrida no estaba abierta todavia (la captura no la tiene; tampoco
    `asistente/cmp-r3`, una corrida de la referencia, ni `reposo/cmp3`): la hora a la que llega la tecla.
- **NativeAOT** (`dotnet publish -c Release -r linux-x64 -o /root/rocola-trabajo/port/fondo/aot`): los mismos 15 avisos del compilador y
  **ningun aviso IL ni de AOT** (0 IL2xxx/IL3xxx). El binario nativo (`Pruebas/VistasFondo/aot-corre.sh`, con `Pruebas/Tarjeta/guiones/corre.sh`,
  los datos de los recorridos, el sonido a tiempo real y las teclas y capturas de los recorridos) por `botones`, `video`, `atraccion` y `pared`:
  7 de 7, 7 de 7, 5 de 5 y 12 de 12 capturas, **0 excepciones** en lo que escribio y en `rocola_error.txt`.
- **diff contra XP** (`Pruebas/Tarjeta/guiones/diff-xp.py -v`): `Form1.Fondo.cs`, 252 lineas quitadas y 249 puestas (las 3 de diferencia
  son los `using` de XP, juntados desde la fase 1), 2 sin marca: dos `{` que el diff agrupa con la linea de arriba, que si la lleva. Los ocho
  archivos de la costura de `RoundedPath`: 0 sin marca (`AsistenteGuiaRocola.cs`, 1: otra `{` de la base). Finales de linea: los de la base
  linea por linea en los nueve archivos (`git diff --stat`: solo las lineas cambiadas).
- **Cintas de la tarjeta**: no hacen falta (este grupo no toca `D3D9Star.cs`, la tarjeta ni `Nativo/`; lo que sube la tarjeta de los
  archivos de la costura sale del mismo `SKPath`, y lo comparan los recorridos).

## Riesgos y diferencias que quedan

- **Un `SKBitmap` soltado cierra la rocola** (`PORT_JUNTAS.md`): las cachés del cielo (`cache`, `skyCache`, `layers`, `luna`...) son del
  `Starfield` y solo las suelta el mismo, en el orden de XP. `ColoresDePortada` con un `SKBitmap` soltado cerraria la rocola; por eso la
  firma de XP del puente lee antes el ancho del `Bitmap` (como XP, que lanza ahi). Cuando `Form1.Portadas.cs` y `LoadCover` den `SKBitmap`,
  quien la llame tiene que conservar ese orden.
- **El nombre**: la traducida de `RoundedPath` es `ContornoRedondo`, no `RutaRedonda` (ya era el `Redondo` de `PantallaVivaGpu.cs`, de
  `RectangleF` y `float`); quien traduzca un `Redondo(...)` o un `RoundedPath(...)` tiene que llamar a la que corresponde.
- **Fallos de XP que se conservan**: `ColorDePortada` (singular) no lo llama nadie, tampoco en XP; una portada soltada en
  `ColoresDePortada` lanza al leer el ancho (fuera del `try`), igual que en XP.
- El arnes cubre el cielo con estados del `Graphics` que los recorridos no ven (giro, escala, copia, medio pixel, vecino mas cercano): en
  la rocola solo le llegan la identidad o la base del HDC, sin suavizar o suavizado, mezclando.
- Nada de esto se probo en el A4.

## Lo que queda

- Traducir a quienes piden las firmas de XP del puente (`Form1.Buscador.cs`, `Form1.Ajustes.cs`, `Form1.Vistas.cs`, `Form1.Pared.cs`,
  `Form1.cs`, `Form1.Portadas.cs`, `Form1.Biblioteca.cs`) y los `OnPaint` y las etiquetas (WinForms); entonces `Puente.fondo.cs` se borra
  entero (y con el, los tipos escritos enteros de `Bold`, `ColoresDisco.Fuente`, `ColorDePortadaDe` y `BgPanel.OnPaint`).

## Revision (revisor independiente, 11-11-2026)

Revisado el diff `ae8b50b..port-fondo` contra `Rocola/Form1.Fondo.cs` (el de XP), las funciones nativas que llama (`Pixel`, `Imagenes`,
`Degradado`, `RecorteLienzo`, `MatrizColor`, `ColorSk`) y lo que hace con las mismas llamadas la capa congelada (`Dibujo2D.cs`), y repetidas
las comprobaciones. **No se encontro ningun fallo en el codigo**; solo una omision en este documento (arreglada).

Lo mirado, linea por linea:
- **GDI+ que queda**: solo los tipos de la capa escritos enteros de las cuatro costuras de WinForms y de las portadas (`Bold`,
  `ColoresDisco.Fuente`, `ColorDePortadaDe`, `BgPanel.OnPaint`); sin `using Rocola.Dibujo2D`.
- **Equivalencia con XP y con lo que hacia la capa**: `PathGradientBrush` = `Degradado.DeTrazo` en la caja del trazo (`GetBounds` =
  `SKPath.Bounds`), centro de fabrica = centro de la caja, la orilla del primero de `SurroundColors` y el `Blend` donde XP lo pone (lo mismo
  que `PathGradientBrush.Pon`); el pincel con shader en un `SKPaint` de color negro opaco (el de fabrica de Skia, el mismo que pone la capa);
  `LinearGradientBrush(caja, a, b, Vertical)` + `InterpolationColors` = `Degradado.Lineal(caja, colores, posiciones, 90, escalable)`
  repetido (como `LinearGradientBrush.Pon` con `WrapMode.Tile`); `TextureBrush(teja, Tile)` = `Degradado.Textura(..., Repite, identidad,
  medio=false)` (la capa pasa `!Centros`, que con el medio pixel de fabrica es `false`); `DrawImage`/`DrawImageUnscaled` = `Imagenes.Pinta`
  con el `InterpolationMode` del lienzo como `Filtro` (bilineal de fabrica en `Graphics.FromImage`), su medio pixel y su copia, y la envoltura
  `Transparente` con o sin `ImageAttributes` (la de fabrica de los dos en la capa: `WrapMode.Clamp`); `FillPolygon` par-impar;
  `ClipBounds` = `RecorteLienzo.Limites` (lo mismo que devuelve la capa); `AddArc`/`AddEllipse` con la misma caja en `float` que
  `GraphicsPath.AddArc`/`AddEllipse` de la capa (el primer arco con `forceMoveTo`, los demas unidos) y `CloseFigure` = `Close`.
- **Estado del `Graphics`**: los hornos con el de fabrica de `Graphics.FromImage` mas lo que XP les pone (suavizado, bilineal, copia) en
  variables; `Draw`, `DrawReactive` y `DrawShoots` con el del que llama; `BlitSkyAndStars` cambia la copia solo en su variable (las
  estrellas y la luna mezclando, como XP antes de devolver `ocm`), y `DrawBeatFlare`/`DrawShoots` reciben la copia del que llama, la que XP
  les deja. El puente lee el estado con `PuenteDibujo.Estado` y el lienzo con `Lienzo` (el recorte de `SetClip` ya esta en el lienzo: la capa
  lo aplica al momento) y no tiene que devolver nada.
- **Costuras**: `RoundedPath` del puente le da al `GraphicsPath` el `SKPath` de `ContornoRedondo` (par-impar; `figuraNueva` queda en
  `true`, como despues del `CloseFigure` de XP); las 27 llamadas en `Form1.Ajustes.cs` (7), `Form1.Buscador.cs` (10) y `Form1.Vistas.cs` (10) siguen por
  el. `ColoresDePortada(Bitmap)` lee el ancho antes (una soltada lanza ahi como en la referencia) y lee los mismos puntos
  (`Bitmap.GetPixel` de la capa = `SKBitmap.GetPixel`, sin premultiplicar). Lo que se suelta: las caches del cielo solo las suelta el
  `Starfield`, en el orden de XP; los resplandores (`glowPal`) son compartidos y nunca se sueltan, como en XP.
- **Archivos**: los nueve de `Codigo/` con el mismo numero de lineas y la misma secuencia de finales de linea (CRLF/LF linea por linea)
  que en la base; nada tocado fuera de lo permitido (`Nativo/`, `Plataforma/Puente.cs`, `Dibujo2D.cs` y el banco, sin cambios).

Lo que se encontro y se hizo:
- (menor, documento) La lista de documentos que siguen nombrando la costura de `RoundedPath` olvidaba `PORT_reposo.md` (`EstadoReposoGpu.cs`,
  la insignia y las tarjetas de la cola, 2 sitios, tambien quitados): anadido arriba.

Comprobaciones repetidas (salidas en `/root/rocola-trabajo/port/fondo/revision`, `rev-cmp` y `rev-cmp3`):
- **Compilacion** (`dotnet build -c Release --no-incremental`): 0 errores, 15 avisos, la misma lista que `avisos-base.txt`.
- **diff contra XP** (`diff-xp.py -v`): `Form1.Fondo.cs` 252 quitadas y 249 puestas, 2 sin marca (las dos `{` sueltas de los
  `PathGradientBrush`); los ocho de la costura, 0 sin marca salvo la `{` de `AsistenteGuiaRocola.cs`.
- **El arnes** (`Pruebas/VistasFondo/arnes.sh`, otra vez las dos compilaciones): 2 763 lineas en las dos, solo la primera distinta
  (`traducida=True/False`); 1 354 llamadas directas; 1 728 mapas del cielo con 494 huellas distintas (no es un cielo repetido).
- **Recorridos** (con el candado, `VOLCADO=1 CAPAS=1 HUELLAS=1`, contra `cmp-ref`/`cmp-ref2`; carga de la maquina 6-7): `botones` 28/28
  texturas quietas y 9/9 capas; `video` 50/50 y 16/16 (las dos capas de 1024x768 de `c21` que difieren no son quietas: tampoco son iguales
  entre las dos de la referencia); `atraccion` 31/32 y 10/10; `pared` 82/83 y 24/24; `buscador` 77/78 y 22/22; `ayuda` 63/66 y 26/26
  (otra corrida). Lo que falta es de tiempos, no de la traduccion: en `atraccion` falta la ultima vuelta de la pagina de 512x256
  (`1da8fd19...`, a los 58-67 s en la referencia: con la carga la vuelta tardo mas y no llego; las mismas cuatro huellas y en el mismo
  orden); en `pared` un cuadro de una franja que se anima (`8c560fb6...`, a los 16 s) que falta tambien en otras 6 de las 55 corridas, una de
  ellas de la referencia (`traza-ref`); en `buscador` `e843916c...` (la de la hora, ya apuntada); en `ayuda` `584659ad...` y `577a5e4c...`, que
  faltan en 20 y 28 de las 71 corridas (esta incluida). La primera corrida de `ayuda` tomo otro camino a los 21-30 s (texturas de 1024x64 que salen tambien en
  corridas de ramas sin este grupo: `atraccion/cmp`, `vistas-a/cmp2`, `buscador/cmp`, `asistente/cmp-b2`): de tiempos de las teclas. La
  luna del cielo vivo (128x128 a los 0,5 s) sale con otra huella que en la corrida de las 19:30 (`085b52bd...` a las 21:19): su iluminacion
  cambia con la hora (`CieloVivoGpu.cs`, no es de este grupo). 0 excepciones en lo que escribio la rocola y en `rocola_error.txt` en todas.
- **NativeAOT**: no se volvio a publicar (el disco tiene 1,3 GB libres y el codigo no cambio desde el binario de las 19:38, posterior al
  ultimo cambio de codigo de las 19:15; su `aot.log`: 0 avisos IL/AOT). Ese binario otra vez por `video` y `botones`
  (`aot-corre.sh`): 7 de 7 capturas en cada uno, 0 excepciones en lo que escribio y en `rocola_error.txt`.
- **El banco**: no se corrio. Compila una lista escrita de archivos (`Dibujo2D.cs`, `Puente.cs` y los de `Nativo/`), ninguno cambiado en
  esta rama (`git diff --stat` vacio), y no compila `Puente.fondo.cs`: sale identico por construccion.
- **Cintas**: no hacen falta (no se toca la tarjeta ni `Nativo/`).
