# Port nativo de XP: la pantalla viva (`PantallaVivaGpu.cs` y `DiscoVivoGpu.cs`), de GDI+ a SkiaSharp directo

Grupo «pantalla-viva» de la tanda 2 del port nativo (el dibujo con GDI+ de los archivos de la capa de la tarjeta), 8-11-2026. Decisiones del
dueño en `AGENTS.md` (8-11-2026: «que tradujeras todo el código para que trabajara nativamente en Linux», «ningún layer de traducción») y la
receta en `PORT_DIBUJO.md` (D2-D4, `Pruebas/BancoDibujo/EjecutaNativo.cs`). Base: `f2f9dcb`; rama `port-pantalla-viva`. Aquí va lo que se
tradujo, el estado del `Graphics` de cada función, cada costura con lo que todavía no está traducido (con su razón y cuándo se quita), las
comprobaciones con sus números y lo que queda. Nada de esto se probó en el A4.

## Qué se tradujo

Los dos archivos ya no ven los tipos que imitan a GDI+ (`using Rocola.Dibujo2D` fuera: `using System.Drawing; using Rocola.Nativo; using
SkiaSharp;` en la misma línea de antes); los de la capa solo quedan escritos enteros en las costuras de abajo. Todo su dibujo va con SkiaSharp
(`SKCanvas`, `SKPaint`, `SKPath`, `SKBitmap`) y las funciones nativas (`Nativo/`), línea por línea como en XP: cada línea que cambia lleva su
`// EN LINUX: ...`, los archivos conservan su número de líneas (963 y 621: donde XP tenía un `using` de un `StringFormat`, la llave del bloque
sube una línea y la declaración del `FormatoTexto` ocupa la de la llave) y sus finales de línea (LF, sin BOM). La lógica de XP no se tocó:
mismos valores, mismo orden, mismas ramas. Las líneas de WinForms (la página de Ctrl+F3 de `PantallaVivaGpu.cs`, `PaginaPantalla` y
`MuestraHoja`; la letra de la lista en `AjustaDiscoVivo`) no se tradujeron: son de la pista de ventanas.

| Archivo | Función | Lo que dibuja | Cómo quedó |
|---|---|---|---|
| `PantallaVivaGpu.cs` | `PintaFoco`, `LetraQueQuepa`, `Pildora` | el letrero «12 canciones · 38 min» de la portada enfocada (la letra más grande que quepa, la píldora y el texto) | `(SKBitmap, ..., string familia)`; `Letra.EnPixeles`, `TextoGdiPlus.Mide`/`Escribe`, `Pixel.Rellena`/`Traza`, `FormatoTexto`; `LetraQueQuepa` mide con el modo de letra (`AjusteLetra`) y da una `Letra`; `Pildora` da un `SKPath` |
| | `HorneaFoco`, `BuildFocoState`, `GpuFoco` | el letrero que se hornea al cambiar de disco y se le pasa a la tarjeta | `SKBitmap` (`Mapas.Nuevo`); la familia, `FamiliaUi().Name` |
| | `PintaFicha`, `Etiqueta` | la ficha rápida del disco: fondo redondo, portadita recortada, artista, disco, resumen, las pastillas del género y de la cola, la raya, las más pedidas y el pie | `(SKBitmap, SKBitmap portada, ..., string familia)`; cinco `Letra`, un `SKPaint` de un color que se cambia como el `SolidBrush` de XP (`br.Color`), dos `FormatoTexto`; la portada con `Imagenes.Pinta` dentro de un `Save` con el recorte del trazo redondo (`ClipPath` sin suavizar, lo que hacía `SetClip`); `Etiqueta(SKCanvas, suave, ajuste, ..., Letra, ...)` |
| | `HorneaFicha`, `BuildFichaState`, `GpuFicha` | la ficha que se hornea y se le pasa a la tarjeta | `SKBitmap`; la portada de `PeekCoverGpu` por el puente |
| | `RutaRedonda` | el rectángulo redondo de XP (`Redondo`) | da un `SKPath` par-impar con los cuatro arcos como `AddArc` (el primero empieza la figura; los demás se unen al punto anterior) y `Close`; `Redondo`, con su nombre y su `GraphicsPath`, sigue en el puente (abajo) |
| | `ColorDePortada` (`GpuPanel`) | el color de la portada enfocada (36 puntos) | `SKBitmap.GetPixel` pasado a `Color` (`ColorSk.DeSk`): el color sin premultiplicar, como `Bitmap.GetPixel` de la capa |
| `DiscoVivoGpu.cs` | `PintaBoleto`, `TamanoBoleto` | el boleto que vuela de la fila a la cola | `(SKBitmap, SKBitmap portada, string, Letra)`: el trazo de `RutaRedonda`, la portadita recortada (`Imagenes.Pinta` en un `Save` con `ClipPath`), el título; `TamanoBoleto(..., Letra, ...)` |
| | `VuelaBoleto`, `GpuBoleto` | el boleto que se hornea al pedir | `SKBitmap` (`Mapas.Nuevo`); la portada del cubo y la letra de la lista por el puente |
| | `SongListView`: `AnchoDuracion`, `PlanFilaGpu`, `PlanPaginaGpu`, `AnchoSonando` | las medidas de la lista grande y de los minutos | `(SKCanvas, AjusteLetra, ...)`: solo miden, con `AnchoTexto` de `Form1.Ajustes.cs` (sin traducir: por el puente) |
| | `SongListView.PastillaSonandoEq` | la pastilla de SONANDO con el hueco de las barritas | `(SKCanvas, ajuste, filtro, copia, medio, ...)`: la pastilla (`Imagenes.Pinta`, entera y en su tamaño, lo que era `DrawImageUnscaled`) y «SONANDO» (`TextoGdiPlus.Escribe`) |

### El estado del `Graphics`

| Dónde | Suavizado | Modo de letra | Copia | Filtro | Medio píxel | Matriz y recorte | De dónde sale |
|---|---|---|---|---|---|---|---|
| `PintaFoco` (y la medida de `LetraQueQuepa`) | sí | `SuaveAjustada` | no | (no pinta imágenes) | no | identidad, sin recorte | fijo: el `Graphics.FromImage` de XP y sus dos líneas (tabla, líneas 383-421: `s=AntiAlias t=AntiAliasGridFit c=SourceOver i=Bilinear p=Default m=I r=no` en las 67 + 263 llamadas; `varia` = «-») |
| `PintaFicha`, `Etiqueta` | sí | `SuaveAjustada` | no | `BilinealAlta` | no | identidad; la portadita, con su recorte en un `Save` | fijo (tabla, líneas 579-702: `i=HighQualityBilinear`, el recorte solo en el `DrawImage` de 613, `r=si`) |
| `PintaBoleto` | sí | `SuaveAjustada` | no | `BilinealAlta` | no | identidad; la portadita, con su recorte | fijo (tabla, líneas 250-282) |
| `AnchoDuracion`, `PlanFilaGpu`, `PlanPaginaGpu`, `AnchoSonando` | — | parámetro | — | — | — | — | solo miden: el modo de letra del `Graphics` de `PintaPagina` (`Form1.Vistas.cs`), que pasa el puente (no están en la tabla: no llaman a GDI+, llaman a `AnchoTexto`, que mide en `Form1.Ajustes.cs:296`) |
| `PastillaSonandoEq` | (no lo usa) | parámetro | parámetro | parámetro | parámetro | la del lienzo | tabla, líneas 409 y 412: `s=AntiAlias t=SystemDefault i=Bilinear` en las 44 llamadas; como la llama `PintaPagina` con su `Graphics`, va todo lo que pesa como parámetro |

`PintaPagina` no traducida pone el suavizado y deja su `Graphics` como estaba; ninguna de las traducidas le cambia el estado (miden o pintan con
lo que reciben), así que las firmas puente no tienen nada que devolverle.

## Costuras con lo que no está traducido (la regla: el puente va del lado de lo que no está traducido)

Todo lo del puente de este grupo está en `Plataforma/Puente.pantalla-viva.cs`, en una parte por archivo.

| Costura | Dónde | Qué se hace | Se quita cuando |
|---|---|---|---|
| las firmas de XP de `PintaFoco` y `PintaFicha` (con `Bitmap`, `Image` y `FontFamily`) | las llama `PrecargaPaginaGpu.cs` (case 4, para calentar) | sobrecargas delgadas: los mismos puntos (`PuenteDibujo.Mapa`), la familia por su nombre (`FamiliaPantallaViva`: la de una `FontFamily` nula es la genérica, como hace `new Font` con ella en la capa) | `PrecargaPaginaGpu.cs` se traduzca |
| `Redondo` con su `GraphicsPath` | lo piden así `SuenaVivoGpu.cs` (traducido: pinta el `SKPath` con `PuenteDibujo.Ruta`) y `AtraccionGpu.cs` (en su rama `port-atraccion`, también por `PuenteDibujo.Ruta`) | la traducida es `RutaRedonda` (un `SKPath`); `Redondo(RectangleF, float)` del puente da un `GraphicsPath` de la capa con ESE mismo `SKPath` (`TrazoPantallaViva`: los mismos puntos, el relleno par-impar y la figura cerrada, como tras `CloseFigure`; al soltar el `GraphicsPath` se suelta el `SKPath`). No puede ser una sobrecarga (solo cambia lo que devuelve): por eso la traducida lleva otro nombre, como `LetraTituloReposo` | los dos llamen a `RutaRedonda` (se cambia `Rocola.Dibujo2D.GraphicsPath p = Redondo(...)` por `SKPath p = RutaRedonda(...)` y se va el `PuenteDibujo.Ruta`) |
| la familia de la interfaz (`FamiliaUi`, `Form1.Ajustes.cs`, da un `FontFamily`) | `HorneaFoco`, `HorneaFicha` | `FamiliaUi().Name` (nunca es nula) | `Form1.Ajustes.cs` se traduzca |
| las portadas de la pared (`PeekCoverGpu`, `Form1.Pared.cs`, da una `Image`) | `HorneaFicha` (la que se pinta) y `BuildFichaState` (la que se compara) | la que se pinta, `PuenteDibujo.Mapa` (sus puntos, los mismos); la que se compara por identidad para rehacer la ficha (`fichaPortada`, `portada`) se queda con su tipo, `Rocola.Dibujo2D.Image`, escrito entero | `Form1.Pared.cs` dé `SKBitmap` |
| la portada del cubo enfocado (`cs.Sel.CoverA` de `GpuCubeInfo`, un `Bitmap` de la capa: lo da `Form1`, sin traducir) | `AplicaEnfoque` (`GpuPanel`) | `ColorDePortada(PuenteDibujo.Mapa(cs.Sel.CoverA), ...)`; `enfoqueDe`, que se compara por identidad para no volver a medir la misma, se queda con su tipo (`Rocola.Dibujo2D.Bitmap`), como los campos compartidos de `D3D9Star.cs` (`PORT_frontera.md`, costura 2) | `GpuCubeInfo.CoverA` sea `SKBitmap` |
| la portada del disco abierto (`cubeBox.Cover`, una `Image` de un control sin traducir) | `VuelaBoleto` | `PuenteDibujo.Mapa`; el `lock (tapa)` de XP queda sobre el `SKBitmap` (el mismo objeto para la misma `Image`; en XP nadie más toma ese candado) | el control se traduzca |
| la letra de la lista del disco (`songList.Font`, la `Font` de un control de WinForms) | `VuelaBoleto` (`TamanoBoleto`, `PintaBoleto`) | `PuenteDibujo.Letra(songList.Font)`: la `Letra` de esa `Font`, la misma (no se suelta) | las ventanas sean nativas |
| `AnchoTexto` (`Form1.Ajustes.cs`) mide con un `Graphics` y una `Font` | `AnchoDuracion`, `PlanFilaGpu`, `PlanPaginaGpu`, `AnchoSonando`, `PastillaSonandoEq`; y sin `Graphics`, `TamanoBoleto` | `AnchoTextoPantallaViva(SKCanvas, AjusteLetra, string, Font)`: un `Graphics` sobre el mismo lienzo con ese modo de letra (`GraphicsPantallaViva`; `AnchoTexto` solo hace `MeasureString`, que solo mira el modo de letra) y la `Font` tal cual (la del control o `fCola`); `AnchoTextoPantallaViva(string, Letra)`: la `Font` de esa `Letra` (los mismos datos), soltada al acabar | `Form1.Ajustes.cs` se traduzca |
| las firmas de XP de `AnchoDuracion`, `PlanFilaGpu`, `PlanPaginaGpu` y `PastillaSonandoEq` | las llama `PintaPagina` (`Form1.Vistas.cs`) con su `Graphics` | sobrecargas delgadas en la parte de `SongListView` del puente: el lienzo (`PuenteDibujo.Lienzo`) y su estado (`PuenteDibujo.Estado`) | `Form1.Vistas.cs` se traduzca |
| lo de `SongListView` que hace `Form1.Vistas.cs`: `fCola` (una `Font`), `colaB` (un `SolidBrush`), `cenFmt` (un `StringFormat`), `Pastilla(...)` y `HorneaGenero(...)` (dan `Bitmap`) | `PastillaSonandoEq`, `AnchoGenero` | `colaB.Color = SonAmbar` se queda como en XP (el pincel es de ellos); se pinta con su color (`PuenteDibujo.Pincel(colaB, medio)`), la `Letra` de `fCola`, el MISMO `FormatoTexto` de `cenFmt` y los puntos de la pastilla (`PuenteDibujo.Mapa`); de la del género solo se lee el ancho (`Rocola.Dibujo2D.Bitmap pg`, escrito entero) | `Form1.Vistas.cs` se traduzca |
| `PastillaView.RenderContentGpu(Graphics)` | la llama `DrawCtrlOverlay` (`Form1.Video.cs`) y ella llama a `RenderContent` de la cápsula (`Form1.Vistas.cs`) | se queda con su `Graphics` (`Rocola.Dibujo2D.Graphics`, escrito entero): solo pone una bandera y pasa el `Graphics` de un archivo sin traducir a otro; traducirla obligaría a ir y volver por el puente (y `RenderContent` le deja cambiado el suavizado al `Graphics` de quien llama) | `Form1.Video.cs` y `Form1.Vistas.cs` se traduzcan (entonces será `(SKCanvas, estado)` como ellos) |
| las letras de los controles de Ctrl+F3 (`PaginaPantalla(Font fNom, Font fDes, Font fBot, ...)`) y de la lista (`fuenteListaNormal`, `fuenteListaGrande`, `AjustaDiscoVivo`) | WinForms | se quedan con su tipo de la capa, escrito entero (`Rocola.Dibujo2D.Font`, `Rocola.Dibujo2D.FontStyle.Bold`) | las ventanas sean nativas (pista de ventanas) |
| `Upload` (`D3D9Star.cs`) | `PreparaPantallaViva`, `PreparaDiscoVivo` | nada: ya recibe `SKBitmap` (grupo frontera); el letrero, la ficha y el boleto se le pasan tal cual (las líneas de XP no cambian) | — |

## Lo que se supuso (y por qué es lo mismo)

- **`SetClip(trazo)` dentro de un `Save` es recortar.** En `PintaFicha` y `PintaBoleto` el `Graphics` es nuevo (sin recorte: `r=no` en la tabla,
  líneas 608 y 270), así que reemplazar el recorte (lo que hace `SetClip`) es lo mismo que cortar con el trazo; los recortes de GDI+ son de
  píxeles enteros (`ClipPath(..., antialias: false)`, como la capa) y el `Restore` de XP es `RestoreToCount`.
- **El pincel de la ficha** es un `SKPaint` de un color que se cambia antes de cada texto, como el `SolidBrush` de XP; `TextoGdiPlus.Escribe` solo
  le lee el color (y el shader, que no tiene).
- **`DrawImageUnscaled(img, x, y)`** es la imagen entera en la caja `(x, y, x + ancho, y + alto)` con el filtro, la copia y el medio píxel del
  lienzo (lo mismo que la capa); una imagen nula no pinta nada.
- **Los catch de la portadita de la ficha** (`ExternalException`, `ArgumentException`) se quedan como en XP aunque `Imagenes.Pinta` no los lance.

## Comprobaciones

Con datos y programas en `/root/rocola-trabajo/port/pantalla-viva` (fuera del repositorio), la máquina compartida con otros agentes (carga de
5 a 11 en 4 núcleos durante las corridas) y llvmpipe. Nada en el A4.

- **Compila** (`dotnet build -c Release --no-incremental`): 0 errores y 15 avisos, la lista ordenada (`archivo(línea,columna): código`) idéntica a
  la de la base `f2f9dcb` (`SYSLIB0054` ×9, `CA2022` ×2, `SYSLIB0050`, `CA2014` y `CS1690` ×2; ninguno de este grupo), en cada uno de los dos
  commits del código (`dea1ab1`, solo `PantallaVivaGpu.cs` con su parte del puente, y `e04ecd2`, con `DiscoVivoGpu.cs`).
- **Diff contra XP** (`Pruebas/Tarjeta/guiones/diff-xp.py ... -v`): `PantallaVivaGpu.cs` 115 líneas quitadas y 112 puestas, `DiscoVivoGpu.cs` 56 y 53,
  **0 sin marca** en los dos (las 3 de diferencia son los `using` de XP en una línea, de D1). `git diff --numstat f2f9dcb`: 112/112 y 53/53 (las
  mismas quitadas que puestas); 963 y 621 líneas, LF y sin BOM, como en la base.
- **Arnés** (`Pruebas/VistasPantallaViva/arnes.sh`: el programa entero de esta copia y el de la referencia congelada con la misma entrada):
  **767 líneas iguales línea por línea** a la referencia, con las letras de XP falsas y con DejaVu: 147 letreros por la firma de XP y 147
  directo (siete medidas de portada × tres anchos de casilla × siete textos —que caben, el corto, el que no cabe ni con la letra más chica, vacío,
  con acentos, eñe, `&` y emoji— y cinco familias con las letras de XP falsas, la nula incluida; con DejaVu, dos), 48 fichas por la firma de XP y 48 directo (seis medidas, sin portada y
  con portadas opacas, con alfa, de 1600×1000 y de 1×1, género, cola, filas, duraciones y veces de todas clases), 60 boletos con su medida
  (cinco letras, seis títulos —vacío y nulo incluidos—, seis altos de fila, cuatro anchos, cinco portadas), 33 colores de portada (nula,
  diminutas, grises, negra, blanca, transparente, con alfa y 24 portadas de 6 a 600 puntos), 70 `Redondo` (10 cajas —con decimales, sin ancho,
  negativas— × 7 radios: los puntos del trazo bit a bit, pintado con ocho estados y lo que se le añade después) y 10 `Pildora`, y 144 de la
  lista del disco (48 páginas de `PintaPagina` con la lista grande, el ecualizador y los minutos en ocho combinaciones, seis medidas, cinco
  letras y los ocho estados del `Graphics` —suavizado, los seis modos de letra, copia, medio píxel, cinco filtros, traslación entera y con
  decimales, escala, giro y recortes rectangular y de trazo—, con lo que deja en `eqIdx`, `eqX`, `eqW`, `eqH`, `generosFuera` y `durAncho`, y 96
  pastillas de SONANDO solas por la firma de XP y directo). 834 huellas distintas; 0 excepciones; en la traducción, 243 llamadas directas a
  las traducidas.
- **Controles negativos del arnés** (`control.sh`: una copia de la traducción con un fallo metido a propósito, compilada con el arnés y
  comparada con la referencia): los diez salen: el segundo arco de `RutaRedonda` que empieza figura (226 líneas distintas: boletos, fichas,
  `Redondo`), lo mismo en `Pildora` (383: fichas, letreros, píldoras), la ficha con `AntiAlias` en vez de `AntiAliasGridFit` (96), el recorte
  de la portadita suavizado (80), `ColorDePortada` sin el alfa de los puntos (8) y con los puntos corridos (18), el texto de la pastilla de
  SONANDO con el modo de letra de fábrica (36) y la pastilla con el filtro de fábrica (24), el título del boleto recortado sin puntos (23) y
  el puente de `AnchoTexto` midiendo con el modo de letra de fábrica (28). Tres cambios no se ven porque no cambian nada: los 36 puntos de
  `ColorDePortada` traspuestos (son el mismo conjunto de puntos) y el medio píxel o la copia en el texto de la pastilla (el pincel es de un color
  opaco: `Pixel.ConPincel` solo usa el medio píxel con un shader, y con alfa 255 copiar y mezclar dan lo mismo).
- **Banco por función** (`Pruebas/VistasReposo/banco.sh`; este grupo no toca `Nativo/`, `Dibujo2D.cs` ni `Puente.cs`, y `Puente.pantalla-viva.cs`
  no entra en el banco; se corrió igual): semillas 7 y 11 con las letras de XP falsas y la 5 con DejaVu, 3 000 casos, ejecutores `gdi`,
  `nativo` y `puente` contra la capa congelada: **0 distintos en los 9** (27 000 casos); `puente-objetos`: 407 bien, 0 mal.
- **El arnés del grupo «suena»** (`Pruebas/VistasSuena/arnes.sh`, porque `SuenaVivoGpu.cs` toma ahora su `Redondo` del puente de este grupo, sobre
  `RutaRedonda`): **487 líneas iguales** a la referencia con las letras de XP falsas y con DejaVu (72 llamadas directas).
- **Recorridos** (`Pruebas/Recorridos`, `VOLCADO=1 CAPAS=1 HUELLAS=1`, con el candado y la carpeta de siempre, contra `cmp-ref` y `cmp-ref2` con
  `compara.py`; el volcado de texturas ya está en el programa desde el grupo frontera):
  - primera corrida, las cuatro pantallas del grupo (pared, pared2, video, buscador; `cmp`): **capas de Dibujo2D quietas 74 de 74 idénticas**;
    texturas quietas 303 de 309 (contando las veces), 219 de 225 distintas;
  - los nueve al final (`cmp2`, con el código de los commits): **capas de Dibujo2D quietas 217 de 217 idénticas** (218 de 221 en total: las 3
    distintas tampoco son iguales entre las dos de la referencia: `video` c21 y `ctrlf3` c46); texturas quietas 447 de 487 (contando las veces),
    360 de 381 distintas (el buscador, 45 de 60 en esta corrida y 58 de 60 en la primera: sus texturas dependen de la hora, `PORT_JUNTAS.md`);
  - juntando las dos corridas: **377 de las 381 texturas quietas distintas salen idénticas** (pared 58/58, video 40/40, buscador 60/60, atracción
    27/27, ayuda 56/56, botones 27/27, ctrlf3 23/23, hojas 23/23, pared2 63/67). Las 4 de pared2 son variantes de un instante, de otros
    archivos y que salen también en otras corridas: el panel del estado extendido de los 41,8 s (`2a697d30…`: el reloj en otro segundo; esta
    copia sacó el otro, `8fc23430…`, que es el de `cmp-ref` en pared), y la tanda de SALTAR de los 62 s (`21d8fe91…` y `906bfb35…`, que esta copia
    sacó en pared; `2f591363…`, la franja de ese instante, que sale en corridas de los grupos frontera, suena y reposo, mientras esta copia
    sacó `da4f642c…`, que sale también en una corrida de la referencia, `frontera/cmp-ref3`, y en las de la rama principal sin este grupo,
    `junta5`). En pared y pared2 esta copia sale del estado extendido a los 60,6 s (la tecla de los 60 s) y la referencia a los 62,7 s o más tarde:
    lo mismo hacen las corridas de la rama principal sin este grupo (`junta`, `junta5`, `frontera`, `suena`), así que no es de aquí.
  - Ninguna de las que faltan es de este grupo (las cuatro son de 1024 de ancho: el panel del estado extendido y las franjas). De lo de aquí, el
    volcado solo ve la ficha (312×251 en 1024×768, en una textura de 512×256), entre las quietas de pared y pared2; el letrero (158×26) y el
    boleto (344×46) son más bajos que el mínimo del volcado (64): los compara el arnés.
  - Ninguna excepción en lo que escribió la rocola ni en `rocola_error.txt` en ninguna corrida (solo «Pantalla parada» de la máquina cargada, el
    volumen de Windows que no hay, «Sin huecos» y la latencia de audio).
- **Junto con la rama principal y el grupo atracción** (sin tocar ninguna rama: `git merge-tree` de `f997e61` —con acabado-estilo— y esta rama, y
  de eso con `port-atraccion`, `e62b789`): sin conflictos; el árbol junto compila (0 errores, los mismos 15 avisos) y el arnés sale **igual** a
  la referencia (767 líneas, con las dos letras). `AtraccionGpu.cs` de esa rama llama a `Redondo` y lo toma del puente de aquí.
- **Cintas de la tarjeta**: no aplica. Este grupo no toca el dispositivo, `D3D9Star.cs` ni `Nativo/`; lo que sube son los mismos puntos (los
  `SKBitmap` del letrero, la ficha y el boleto llegan a `Upload` directo en vez de por la sobrecarga con `Bitmap` del grupo frontera, que ya
  pasaba esos mismos `SKBitmap`), y lo que se sube lo comparan los recorridos y el arnés.
- **NativeAOT** (`dotnet publish -c Release -r linux-x64 -o /root/rocola-trabajo/port/pantalla-viva/aot`): los mismos 15 avisos del compilador de la
  base y **ningún aviso IL ni de AOT** (0 IL2xxx/IL3xxx). El binario nativo (`Pruebas/VistasPantallaViva/aot-corre.sh`, con
  `Pruebas/Tarjeta/guiones/corre.sh`, los datos de los recorridos, el sonido a tiempo real y las teclas y capturas de los recorridos) por pared
  (el letrero de la portada enfocada, la ficha, la lista del disco, el boleto, el aviso con su `Redondo` del puente), pared2 (con la estrella en
  2D, las lunas, la interfaz tramada y el cielo profundo), video y buscador: 12 de 12, 6 de 6, 7 de 7 y 11 de 11 capturas, salida 0 y **0
  excepciones** en lo que escribió y en `rocola_error.txt` (solo el volumen de Windows que no hay y «Sin huecos»). La captura de los 27,5 s
  enseña el letrero «2 canciones · 5 min» sobre la portada enfocada y el aviso «Pusiste «02 Ángel de amor…» · es la siguiente · en ~2 min».

## Herramientas nuevas (`Pruebas/VistasPantallaViva`, fuera del programa)

- `VistasPantallaViva.csproj` + `PantallaViva.cs`: el arnés. Compila el programa entero de una carpeta `Xp` (esta copia o, copiando el
  proyecto fuera, la referencia con `-p:Fuente=...`) con otra entrada que llama con datos fijos a `PintaFoco` y `PintaFicha` (por la firma de
  XP y, en la traducción, también directo), `PintaBoleto` y `TamanoBoleto` (por reflexión, con los tipos de cada lado), `ColorDePortada`,
  `Redondo` y `Pildora` (los puntos del trazo, bit a bit, y pintados) y la lista del disco (`PintaPagina` con la lista grande, el ecualizador y
  los minutos, con ocho estados del `Graphics`, y `PastillaSonandoEq` sola, por la firma de XP y directo). `arnes.sh` lo compila de las dos
  maneras, lo corre con las letras de XP falsas y con DejaVu y compara (carpeta de trabajo: `PANTALLAVIVA`, de fábrica
  `/root/rocola-trabajo/port/pantalla-viva`).
- `control.sh`: los controles negativos del arnés (diez fallos metidos a propósito, uno por copia; `SOLO=expresión` corre solo los que casen).
- `aot-corre.sh`: el binario NativeAOT por las pantallas del grupo (pared, pared2, video, buscador), con las teclas y capturas de los recorridos.

## Riesgos y diferencias que quedan

- **Una imagen ya soltada que llega por el puente** (`PuenteDibujo.Mapa` da `null`): la portadita de la ficha y la del boleto salen como si no
  hubiera portada (el cuadrito del color del género; el verde del boleto), `ColorDePortada` da el color de la nebulosa y la pastilla de SONANDO
  no se pinta. En la referencia, leer su `Width` lanzaba `NullReferenceException`; en el GDI+ de XP, `ArgumentException`: en la ficha XP la
  atrapaba (`catch (ArgumentException)`) y pintaba el cuadrito (lo mismo que ahora); en el boleto, en `ColorDePortada` (su `b.Width` va fuera del
  `try`) y en la pastilla la excepción subía. En los caminos de XP no se llega: las portadas salen de la caché, que las suelta en
  `DrainCoverTrash` en el hilo de la pantalla y fuera del dibujo, nunca entre que se piden y se pintan; las pastillas de `Pastilla` se tiran solo
  al cambiar su alto, antes de devolver una nueva. Es lo mismo que apuntaron frontera y reposo.
- **Un `SKBitmap` soltado tumba la rocola** (revisión de reposo): los de este grupo (`focoBmp`, `fichaBmp`, `boletoBmp`) solo se sueltan al
  cambiar de medida, en el mismo paso en que se hace el nuevo (`HorneaFoco` y `HorneaFicha` dentro de `GetFoco`/`GetFicha`, que la tarjeta pide en
  `PreparaPantallaViva` antes de dibujar; `VuelaBoleto` al pedir, en el hilo de la pantalla, y `PreparaDiscoVivo` cambia la del cuadro antes de
  `DibujaBoleto`: `D3D9Star.cs` 2607-2608 frente a 2812, 2910 y 2921). Quien cambie ese orden tiene que conservarlo.
- **El candado del boleto** (`lock (tapa)`) es ahora el `SKBitmap` de la portada y no la `Image`: el mismo objeto para la misma `Image`
  (`Image.Sk`), y en XP nadie más toma ese candado (en `Rocola/` no hay otro `lock` sobre una portada), así que excluye lo mismo.
- **La línea 62 de `PantallaVivaGpu.cs`** (`PaginaPantalla`, la página de Ctrl+F3) solo cambió `Font` por `Rocola.Dibujo2D.Font`; la pista de
  ventanas (`port-ventanas2`) convierte esa página: si al juntar la cambió, manda la suya.
- Las diferencias (a)-(e) de `PORT_DIBUJO.md` siguen como en la referencia (la traducción llama a las mismas funciones nativas que la capa): la
  (e1) en las portaditas de la ficha y del boleto (`HighQualityBilinear`, escaladas con el medio píxel de fábrica); la (a) en lo que lee
  `ColorDePortada` de una portada con alfa (la capa guarda los mapas premultiplicados y `GetPixel` los devuelve sin premultiplicar, como
  ahora); la (b2) en lo que mide `AnchoTexto` para la lista (el `Graphics` de `PintaPagina` va con `SystemDefault`). Los mapas de este grupo son
  `Format32bppPArgb` en XP: la (a) no les toca.
- No se vio ningún fallo de XP en estas funciones.
- Nada de esto se probó en el A4.

## Lo que queda

- Quitar las costuras de arriba a medida que se traduzcan los otros lados: `PrecargaPaginaGpu.cs` (las firmas de XP de `PintaFoco` y
  `PintaFicha`), `SuenaVivoGpu.cs` y `AtraccionGpu.cs` (`Redondo`: su `Rocola.Dibujo2D.GraphicsPath p = Redondo(...)` pasa a `SKPath p =
  RutaRedonda(...)` sin `PuenteDibujo.Ruta`, y se borran `Redondo` y `TrazoPantallaViva`), `Form1.Ajustes.cs` (`FamiliaUi`, `AnchoTexto`),
  `Form1.Pared.cs` (`PeekCoverGpu`, `cubeBox.Cover`), `Form1.Vistas.cs` (`PintaPagina` y lo de `SongListView`: `fCola`, `colaB`, `cenFmt`,
  `Pastilla`, `HorneaGenero`; con `Form1.Video.cs`, `RenderContentGpu`), `GpuCubeInfo.CoverA` (`enfoqueDe`) y las ventanas (las letras de la
  página de Ctrl+F3 y de la lista). Al final, `Puente.pantalla-viva.cs` entero se borra con `Dibujo2D.cs` y `Puente.cs`.
- Al juntar con `port-atraccion`: compila tal cual (su `AtraccionGpu.cs` llama a `Redondo` del puente); después, el cambio de arriba.
- Nada se probó en el A4.
