# Port nativo de XP: la segunda mitad de `Form1.Vistas.cs` (el encabezado, las cápsulas, el género, la barra y la lista del disco), de GDI+ a SkiaSharp directo

Grupo «vistas-b» de la tanda 3 del port nativo (el dibujo con GDI+ de lo que quedaba), 11-11-2026: `Codigo/Form1.Vistas.cs` desde la clase
`HeaderView` (línea 998) hasta el final: `HeaderView`, `PastillaView`, `GeneroView`, `ProgressView`, `SongListView` (su parte de este archivo,
con sus hornos) y `Track`. Las líneas 1-997 son del grupo vistas-a, en paralelo, y no se tocaron (salvo la línea 10, la de los `using`: ver
«Riesgos»). Decisiones del dueño en `AGENTS.md` (8-11-2026: «que tradujeras todo el código para que trabajara nativamente en Linux», «ningún
layer de traducción») y la receta en `PORT_DIBUJO.md` (D2-D4, `Pruebas/BancoDibujo/EjecutaNativo.cs`). Base: `ae8b50b`; rama `port-vistas-b`.
Aquí va lo que se tradujo, el estado del `Graphics` de cada función, cada costura con lo que todavía no está traducido (con su razón y cuándo
se quita), las costuras de otros grupos que se quitaron, las comprobaciones con sus números y lo que queda. Nada de esto se probó en el A4 (no
se puede desde aquí): todo es fuera de pantalla con llvmpipe.

## Qué se tradujo

De `HeaderView` al final, el archivo ya no usa los tipos que imitan a GDI+ para dibujar: todo va con SkiaSharp (`SKCanvas`, `SKPaint`,
`SKPath`, `SKBitmap`) y las funciones nativas (`Nativo/`: `Pixel`, `TextoGdiPlus`, `Imagenes`, `Mapas`, `Letra`, `FormatoTexto`), línea por
línea como en XP. Cada línea que cambia lleva su `// EN LINUX: ...` (287 líneas cambiadas, 287 puestas: `git diff --numstat`); el archivo
conserva sus 2 749 líneas y sus finales de línea (2 568 con CRLF y 181 con LF, como en la base). La lógica de XP no se tocó: mismos valores,
mismo orden, mismas ramas. Los tipos de la capa solo quedan, escritos enteros (`Rocola.Dibujo2D.…`), en las costuras de abajo. La línea 10
sigue llevando `using Rocola.Dibujo2D` porque el archivo lo comparten vistas-a y las líneas de WinForms (`OnPaint(PaintEventArgs e)`), que no
son de esta tanda.

| Clase | Función | Lo que dibuja | Cómo quedó |
|---|---|---|---|
| `HeaderView` | `AseguraRecursos`, `AnchoMinimo` | las letras del encabezado y lo que mide | `Letra.EnPuntos(familia del control, h × 0,446 / 0,303, negrita)`; el pincel, un `SKPaint`; el formato, el `FormatoTexto` de `CreaSfDibuja` (el mismo objeto, por el puente); el mapa de 1×1 con que XP mide es un `SKBitmap` con su lienzo (modo de letra de fábrica: `DelSistema`) |
| | `RenderContent` | la píldora del color, la sección (con su sombra y, si no cabe, con la letra apretada), la raya, los puntos de las páginas y «Página N de M» en sus formas | `(SKCanvas, AjusteLetra, copia, medio)`: el suavizado lo pone ella (variable `suave`); `Pixel.Rellena/Linea/RellenaOvalo/TrazaOvalo`, `TextoGdiPlus.Escribe` |
| | `Redondo` | la píldora | un `SKPath` hecho como el `GraphicsPath` de GDI+ (regla 5): relleno par-impar, el primer arco con `forceMoveTo` y los otros tres unidos al punto anterior, `Close` |
| `PastillaView` | `Ancho` | lo que mide la cápsula con un alto | la familia por su nombre (la firma con `FontFamily`, en el puente); `Letra.EnPuntos` |
| | `RenderContent` | la cápsula: fondo y borde, el brillo de la llegada, la etiqueta, el valor (quieto o rodando como un odómetro, con su recorte), las tres portaditas de la cola con su «+N», la moneda que cae, la sacudida y el salto | `(SKCanvas, AjusteLetra, copia, Filtro, medio)`: el suavizado lo pone ella; `g.Save()`/`RestoreToCount` por `GraphicsState`; `Translate`/`Scale` (se anteponen, como en GDI+); los recortes con `ClipRect`/`ClipPath` sin suavizar; el filtro de las portaditas (`BilinealAlta`) en una variable que se devuelve a mano, como en XP; las portadas llegan como `Image` de la capa y se pintan sus puntos (`PuenteDibujo.Mapa`) |
| | `RoundRectF` | el contorno de la cápsula y de las portaditas | un `SKPath` como el de arriba |
| `GeneroView` | `RenderContent` | la pastilla del género (fuera de la lista) | `(SKCanvas, AjusteLetra, copia, medio)`; la letra es la del control (`PuenteDibujo.Letra(this.Font)`) |
| `ProgressView` | `RenderContent` | la barra de avance: el riel, el tubo de neón (tres plumas de puntas redondas) o el relleno con su brillo | `(SKCanvas, copia, medio)`; las plumas con `StrokeCap`/`StrokeJoin` redondos donde XP los ponía; el degradado del relleno con `Degradado` (Nativo) |
| `SongListView` | `HorneaCirculo`, `HorneaPastilla`, `HorneaCifras`, `HorneaGenero`, `Pastilla` | los circulitos de los números, las pastillas de las marcas («EN COLA», «SONANDO»), las diez cifras en sus tres colores y la pastilla del género | `SKBitmap` (`Mapas.Nuevo`: Bgra8888 premultiplicado, lo que era `Format32bppPArgb`); su `Graphics.FromImage` es un `SKCanvas` con el estado en variables (suavizado y `Suave`/AntiAlias donde XP los ponía) |
| | `PintaNumero` | el número de la fila con sus cifras horneadas | `(SKCanvas, copia, Filtro, medio, …)`: copia los mapas con `Imagenes.Pinta` |
| | `AseguraHorneada`, `hornoPag`, `hornoAnt` | la página horneada y la que se va al hojear | `SKBitmap`; se sueltan en el mismo orden que en XP |
| | `RenderContent` | la página horneada, o las dos deslizándose | `(SKCanvas, copia, Filtro, medio)`: `DrawImageUnscaled` es `Imagenes.Pinta` entera y en su tamaño |
| | `RecortaAlCristal` | el recorte de la fila al cristal (solo con la tarjeta) | `(SKCanvas)`: `ClipPath` sin suavizar, que deshace el `RestoreToCount` de quien llama |
| | `PintaPagina` | la página entera: fondo, fila elegida, números, títulos (con el texto móvil), duraciones, marcas y géneros | `(SKCanvas, ref bool suave, AjusteLetra, copia, Filtro, medio, arranque)`: el suavizado va por referencia porque `PintaPagina` lo deja puesto y la segunda llamada sobre el mismo lienzo lo hereda (la fila elegida de `AcabadoGpu.cs`) |
| `Track` y lo demás | — | (no dibujan) | sin cambios |

## El estado del `Graphics`

| Dónde | Suavizado | Modo de letra | Copia | Filtro | Medio píxel | Matriz y recorte | De dónde sale |
|---|---|---|---|---|---|---|---|
| `HeaderView.RenderContent` | sí (lo pone ella) | parámetro | parámetro | — | parámetro | en el lienzo | tabla (`estado_por_linea.tsv`), 1138-1164: varían el modo de letra (`SystemDefault` desde `OnPaint`, `AntiAliasGridFit` desde `DrawCtrlOverlay`), la matriz, la del HDC y el recorte |
| `HeaderView.AnchoMinimo` | — | `DelSistema` | — | — | — | — | fijo: un `Graphics` nuevo sobre un mapa de 1×1 (tabla, 1058-1060) |
| `PastillaView.RenderContent` | sí (lo pone ella) | parámetro | parámetro | parámetro; `BilinealAlta` en las portaditas y vuelve | parámetro | en el lienzo; `Save`/`Restore` del rodillo, de cada portadita y de la sacudida | tabla, 1384-1462: varían lo mismo que arriba; el recorte del rodillo y de las portaditas, solo el recorte |
| `GeneroView.RenderContent` | sí (lo pone ella) | parámetro | parámetro | — | parámetro | en el lienzo | no la pasaron los recorridos: razonado desde `OnPaint` y `DrawCtrlOverlay` (los mismos que llaman a las otras) y pasada por el arnés |
| `ProgressView.RenderContent` | sí (lo pone ella, antes de mirar su medida) | — | parámetro | — | parámetro | en el lienzo | tabla, 1630-1642 (sin variación); las ramas del neón y del relleno, por el arnés |
| los hornos de `SongListView` | sí | `Suave` (AntiAlias) en cifras y género | no | — | no | identidad, sin recorte | fijo: `Graphics.FromImage` y sus líneas (tabla, 1746-1821, `varia` = «-») |
| `SongListView.RenderContent` | — | — | parámetro | parámetro | parámetro | en el lienzo | tabla, 2060: varían el suavizado y el modo de letra (que copiar un mapa no usa), la del HDC y el recorte |
| `PintaPagina` | `ref`: entra como esté y sale puesto | parámetro | parámetro | parámetro | parámetro | en el lienzo | tabla, 2120-2247: solo varía el suavizado (2124: sin suavizar la primera vez y suavizado en la segunda llamada sobre el mismo lienzo); sus tres llamadores le pasan el estado de fábrica de un `Graphics` nuevo (`DelSistema`, sin copia, `Bilineal`, sin medio píxel) |
| `PintaNumero`, `RecortaAlCristal` | — | — | parámetro | parámetro | parámetro | la del lienzo | las llama solo `PintaPagina` |

Lo que las vistas le dejan puesto al `Graphics` de quien llama (y que XP dejaba): `SmoothingMode = AntiAlias`, con las mismas condiciones
de sus primeras líneas (si la vista no tiene medida para pintarse, no lo pone; `ProgressView` lo pone siempre; la lista no cambia nada). Lo
devuelve la firma de XP del puente; el arnés lo comprueba en cada caso (`Queda`: los cinco modos, la matriz, la cuenta de `Save` y el recorte).

## Costuras con lo que no está traducido (la regla: el puente va del lado de lo que no está traducido)

Todo lo del puente de este grupo está en `Plataforma/Puente.vistas-b.cs` (114 líneas).

| Costura | Dónde | Qué se hace | Se quita cuando |
|---|---|---|---|
| las firmas de XP de `RenderContent` (con un `Graphics`) de las cinco vistas y `PastillaView.RenderContentGpu` | las llaman sus `OnPaint` (WinForms, en el mismo archivo), `DrawCtrlOverlay` (`Form1.Video.cs`, 1786-1791) y `EnSeco` (`Form1.Portadas.cs`, `PrecargaPaginaGpu.cs`) | `PuenteDibujo.Lienzo(g)` (con su matriz y su recorte) y lo que pesa de `PuenteDibujo.Estado(g)`; después, el suavizado que XP le deja al `Graphics`. Las clases llevan `partial` con su nota | `Form1.Video.cs` (grupo video-form1) pase el lienzo y su estado, y `OnPaint` deje de ser de WinForms |
| `PastillaView.Ancho` con una `FontFamily` | `Form1.cs` 1485-1486 y `Form1.Datos.cs` 3337 (`this.Font.FontFamily`) | la familia por su nombre (la nula, la genérica sin remates, como `new Font(null, …)` de la capa) | la letra del Form sea una `Letra` |
| `AnchoTexto` (`Form1.Ajustes.cs`) mide con un `Graphics`, o sin él, y una `Font` | `HeaderView` (`AnchoMinimo` y `RenderContent`), `PastillaView.Ancho`, `PintaPagina` | `AnchoTextoVistasB`: un `Graphics` sobre el MISMO lienzo con el modo de letra del que llama (solo mide) y la `Font` de esa `Letra`, soltados al acabar | se junte ventanas2, que ya tiene `AnchoTexto(AjusteLetra, string, Letra)` y `AnchoTexto(string, Letra)`: llamar directo |
| `AnchoTextoPantallaViva(SKCanvas, AjusteLetra, string, Letra)` | `DiscoVivoGpu.cs` (grupo pantalla-viva) mide con `fCola`, que ahora es una `Letra` | una sobrecarga que va a `AnchoTextoVistasB`; sus cinco llamadas no cambian | lo mismo que la de arriba |
| `CreaSfDibuja` (`Form1.Ajustes.cs`) da un `StringFormat` | los formatos de las cinco vistas | `PuenteDibujo.Formato(…)`: su `FormatoTexto`, el mismo objeto | se junte ventanas2, que ya tiene `CreaSfDibuja(Alinea)` |
| `RoundedPath` (`Form1.Fondo.cs`) da un `GraphicsPath` | el género, la barra, las pastillas horneadas, la fila elegida y el recorte al cristal | se suelta como en XP; se pinta su `SKPath` (`PuenteDibujo.Ruta`, el mismo, sin copia) | `Form1.Fondo.cs` dé un `SKPath` |
| `SueltaBitmaps` (`Form1.Pared.cs`) pide `Bitmap[]` | las cifras horneadas | `SueltaMapasVistasB(ref SKBitmap[])`: la misma cuenta | `Form1.Pared.cs` se traduzca |
| `PastillaView.Portadas` es `Image[]` de la capa | las da `Form1` (`LoadCover`) y se comparan por identidad | se queda con su tipo, escrito entero; al pintar, `PuenteDibujo.Mapa(im)` | `Form1.Biblioteca.cs` dé `SKBitmap` |
| la letra del control (`this.Font`, WinForms) | el género y los títulos y duraciones de la lista; `HeaderView.pideFuente` (solo se compara por identidad) | `PuenteDibujo.Letra(Font)` al usarla; la familia, por su nombre | las ventanas dejen de ser de WinForms |

Ninguna parte del puente le cambia el estado al `Graphics` de quien llama, salvo el suavizado que `RenderContent` le deja en XP.

### Costuras de otros grupos que se quitaron (dependían de este archivo)

- `AcabadoGpu.cs` (13 líneas): `hornoPag` y `hornoAnt` ya son `SKBitmap` (fuera `PuenteDibujo.Mapa(…)` en sus siete usos) y `PintaPagina`
  recibe el lienzo y el estado de fábrica directo (fuera `PuenteDibujo.Graphics(lienzo, …)`); las dos `PintaPagina` de la fila elegida
  comparten el suavizado en una variable (`suaveFila`), como compartían el `Graphics` en XP.
- `DiscoVivoGpu.cs` (6 líneas): `HorneaGenero` y `Pastilla` dan `SKBitmap`; `fCola`, `colaB` y `cenFmt` son `Letra`, `SKPaint` y `FormatoTexto`
  (fuera `PuenteDibujo.Letra/Pincel/Formato`); `RenderContentGpu` recibe el lienzo y su estado (la firma de XP, para `DrawCtrlOverlay`, en
  el puente de aquí).
- `SuenaVivoGpu.cs` (2 líneas): `fEtq` y `fVal` de la cápsula son `Letra` (fuera `PuenteDibujo.Letra`).
- `Plataforma/Puente.pantalla-viva.cs`: fuera las cuatro firmas de XP de `AnchoDuracion`, `PlanFilaGpu`, `PlanPaginaGpu` y `PastillaSonandoEq`
  para `PintaPagina`, que ya las llama directo (12 líneas menos).
- `Pruebas/VistasPantallaViva/PantallaViva.cs` (el arnés de ese grupo): llamaba por reflexión a `PintaPagina(Graphics, int)` y a la firma de
  XP de `PastillaSonandoEq`; ahora, si la primera es la traducida, le pasa el lienzo y el estado del `Graphics` (los ocho estados de antes), y
  sin la firma de XP llama a la pastilla directo las dos veces. Sigue saliendo igual a la referencia (abajo).

## Lo que se supuso (y por qué es lo mismo)

- **El color de un pincel que se reutiliza** (`brocha.Color = …` de XP) es `SKPaint.Color = ….Sk()`: los `SolidBrush` de XP son `SKPaint`
  de un color sin nada más; `Pixel` y `TextoGdiPlus` reciben el suavizado, la copia y el medio píxel aparte, como en D4.
- **Un `FormatoTexto` no tiene nada que soltar**: donde XP hacía `SueltaDibujo(ref sf)`, el campo queda en `null` (lo mismo que deja
  `SueltaDibujo`), y el objeto, que es el de `CreaSfDibuja`, no se comparte con nadie más.
- **`g.Save()` de GDI+ guarda todo el estado; el del lienzo, la matriz y el recorte.** Entre cada `Save` y su `Restore` de estas vistas solo
  cambian la matriz y el recorte (tabla: en 1423-1462 solo varía `r`), y el filtro de las portaditas XP lo devuelve a mano
  (`g.InterpolationMode = antes`), así que es lo mismo; el arnés lo comprueba con la cuenta de `Save` y el estado en que queda el `Graphics`.
- **`AnchoTexto` con un `Graphics` nuevo sobre el mismo lienzo** mide lo mismo que con el de XP: `MeasureString` solo mira el modo de letra.
- **`PintaPagina` siempre recibe el estado de fábrica** de un `Graphics` nuevo: sus tres llamadores (`AseguraHorneada`, y la capa de la
  lista y la fila elegida de `AcabadoGpu.cs`) pintan en un mapa recién hecho. El arnés de pantalla-viva la sigue llamando con ocho estados
  distintos (ahora como parámetros) y sale igual.
- Todo esto corre en el hilo de la pantalla.

## Comprobaciones

Con datos y programas en `/root/rocola-trabajo/port/vistas-b` (fuera del repositorio), la máquina compartida con otros agentes (carga de 6 a
11 en 4 núcleos) y llvmpipe. Nada en el A4. La traducción se hizo antes de un corte de la sesión (commit `3b4f3aa`); todas las comprobaciones
de abajo se repitieron después, con el código de ese commit.

- **Compila** (`dotnet build -c Release`): 0 errores y 15 avisos, los mismos de la base `ae8b50b` en los mismos sitios (`SYSLIB0054` ×9,
  `CA2022` ×2, `SYSLIB0050`, `CA2014` y `CS1690` ×2; ninguno de este grupo).
- **Diff contra XP** (`Pruebas/Tarjeta/guiones/diff-xp.py … -v`): `Form1.Vistas.cs` 291 líneas quitadas y 288 puestas, **1 sin marca**, que no
  es de este grupo (línea 131, `g.TextRenderingHint = …ClearTypeGridFit;` de la primera mitad: XP la escribía con el nombre entero y ya estaba
  así en la base); `AcabadoGpu.cs` 99/97, `DiscoVivoGpu.cs` 58/55 y `SuenaVivoGpu.cs` 172/169, **0 sin marca**. `git diff --numstat ae8b50b`:
  287/287, 13/13, 6/6 y 2/2; mismos números de líneas y de CRLF que en la base (2 749 con 2 568 CRLF; 411, 621 y 987 con LF). De la línea 998
  en adelante, lo único anterior que cambia es la línea 10.
- **Arnés** (`Pruebas/VistasVistasB/arnes.sh`: el programa entero de la referencia congelada, de la base y de esta copia con la misma
  entrada, `VistasB.cs`): **2 864 casos, 0 líneas distintas** frente a la referencia y frente a la base, con las letras de XP falsas y con
  DejaVu (y la base, 0 frente a la referencia). 594 del encabezado (99 encabezados × 5 estados y lo que piden de ancho: ocho secciones —una que no cabe, la
  vacía y la nula incluidas—, once páginas —hasta 120, y fuera de rango—, nueve medidas, cuatro familias), 291 de las cápsulas (créditos y cola, con y sin portaditas —opacas, con alfa, nulas—,
  «+N», lo que miden, y 75 con el reloj quieto en quince instantes: la moneda que cae y el número que rueda, la sacudida del aviso, la
  llegada a la cola, moneda y aviso a la vez y la moneda grande), 144 del género, 294
  de la barra (el riel, el neón y el relleno), 1 524 de la lista (42 listas × 5 estados por `RenderContent`, la página
  horneada, los circulitos, las cifras, 216 pastillas de marcas y 180 de género, 82 capas de la tarjeta de `AcabadoGpu.cs` —la de la fila
  elegida, que llama dos veces a `PintaPagina`—, 95 hojeos con el reloj quieto, con las ocho combinaciones de lo vivo y con y sin tarjeta) y 17
  hornos sueltos. Cada vista se llama como la llama lo que no está traducido (la firma de XP con un `Graphics`) con el estado de la imagen de
  la interfaz, con el de un `OnPaint` (el dibujo por procesador) y con estados al azar (suavizado, los seis modos de letra, copia, medio píxel,
  cinco filtros, traslación entera y con decimales, escala, giro, recortes que reemplazan, que se suman y de un trazo); cada línea lleva los
  puntos tal cual y como los sube la tarjeta, lo que devuelve y el estado en que queda el `Graphics`. 1 787 huellas distintas; 0 sin reloj
  quieto; 0 excepciones con las letras falsas y 18 con DejaVu (`capsula-N-mide`: el arnés pide `new FontFamily("Times New Roman")`, que no existe
  sin esas letras; las mismas 18 en la referencia y en la base).
- **Controles negativos del arnés** (`mutantes.sh`: una copia de la traducción con un fallo metido a propósito cada vez): **los 17 se ven**
  (líneas distintas de 2 865): la raya de 2 puntos (215), un arco que empieza figura (327), el recorte del rodillo (10), el filtro de las
  portaditas (104), el género sin formato (89), el degradado de la barra horizontal (46), el título sin puntos suspensivos (297), la página
  sin suavizar (262), las cifras con otro modo de letra (801), el puente sin devolver el suavizado (238), la sacudida sin `Restore` (21), la
  copia olvidada en el texto del encabezado (58), el medio píxel olvidado en el relleno de la barra (8), la pluma del riel con puntas
  redondas (53), el filtro del hojeo (16), el sitio de la página que se va (75) y el modo de letra olvidado en la marca de la cola (240).
- **Los arneses de los grupos cuyas costuras se quitaron**, contra la referencia: pantalla-viva (`VistasPantallaViva/arnes.sh`, con su
  `PantallaViva.cs` adaptado) **767 líneas IGUALES** con las dos letras (291 llamadas directas; antes 243); suena **487 IGUALES** con las dos
  letras (72 directas); acabado-estilo (`compara.sh`) **0 distintas de 6 603** frente a la referencia y de 6 607 frente a su base, con las dos
  letras.
- **Banco por función** (`Pruebas/VistasReposo/banco.sh`; este grupo no toca `Nativo/`, `Dibujo2D.cs` ni `Puente.cs`, y `Puente.vistas-b.cs` no
  entra en el banco; se corrió igual): semillas 7 y 11 con las letras de XP falsas y la 5 con DejaVu, 3 000 casos, ejecutores `gdi`, `nativo` y
  `puente` contra la capa congelada: **0 distintos en los 9** (27 000 casos); `puente-objetos`: 407 bien, 0 mal.
- **Cintas de la tarjeta** (`Pruebas/VistasFrontera/cintas.sh`, porque la página, las pastillas y las cifras que se suben ya son `SKBitmap` de
  punta a punta): **29 de 29 IGUALES** (6 616 cuadros), con las variantes de límite de textura y de cambio de medida.
- **Recorridos** (`Pruebas/Recorridos`, `VOLCADO=1 CAPAS=1 HUELLAS=1`, con el candado y la carpeta de siempre, contra `cmp-ref` y `cmp-ref2`):
  - los nueve (`cmp`): 9 de 9 salen con 0; **capas de Dibujo2D quietas 213 de 217** y texturas quietas 477 de 487 (contando las veces);
  - pared, pared2, ayuda y buscador otra vez (`cmp-b`): capas quietas 84 de 84; texturas quietas 317 de 325;
  - las 4 capas: la hoja de Ctrl+F2 de ctrlf3 a los 40 s solo cambió de nombre con la hoja nativa (`capa4-hoja-189,182-646x404`; su huella,
    `31c56564160785fd`, es la de la referencia), y la hoja de Ctrl+F3 de hojas a los 18, 19,5 y 21 s es el repaso de la biblioteca, que va más
    despacio con la máquina cargada (`PORT_JUNTAS.md`): las tres huellas de esta copia son las de corridas de la rama sin este grupo
    (`pantalla-viva/rev/cmp-base`, `cmp` y `cmp-r2`);
  - las texturas que faltan son todas de 1 024 de ancho (la franja de lo que suena, el panel del estado extendido y la tanda de SALTAR: no
    son de este grupo) y son veces de menos de un instante: `c9e4fc67…` sale 2 veces en ayuda en la referencia y 1 aquí, igual que en
    `junta/cmp` y `asistente/cmp` (la rama sin este grupo); `8cd9108c…` (buscador) falta también en 25 de 52 corridas guardadas;
    `8c560fb6…` (pared, a los 16 s) falta en las dos corridas de aquí y en tres de otros grupos, y esta copia la saca en pared2 las dos veces;
    las de pared2 (`584659ad…`, `1c9507bb…`, `1f1712f1…` y las cuatro ya apuntadas en `PORT_pantalla-viva.md`: `2a697d30…`, `21d8fe91…`,
    `906bfb35…`, `2f591363…`) faltan también en entre 3 y 41 de las 53 corridas guardadas, de la referencia incluidas (`frontera/cmp-ref3`,
    `reposo/ref-hoy`). Las texturas de esta copia que no están en `cmp-ref` ni `cmp-ref2` son todas de 1 024×128 (68 distintas en los nueve
    que no salen en ninguna otra corrida guardada; con la rama sin este grupo, `junta/cmp`, son 83: el reloj de lo que suena);
  - de lo de aquí, el volcado ve la lista y la interfaz entre las quietas (todas idénticas); las cápsulas, el encabezado, el género y la barra
    van dentro de la imagen de la interfaz, y sueltos los compara el arnés;
  - ninguna excepción en lo que escribió la rocola ni en `rocola_error.txt` en ninguna corrida (solo «Pantalla parada» de la máquina
    cargada, el volumen de Windows que no hay, «Sin huecos» y la latencia de audio).
- **NativeAOT** (`dotnet publish -c Release -r linux-x64 -o /root/rocola-trabajo/port/vistas-b/aot`): los mismos 15 avisos del compilador y
  **ningún aviso IL ni de AOT**. El binario nativo (`Pruebas/VistasVistasB/aot-corre.sh`, fuera de pantalla, con las teclas y capturas de los
  recorridos): pared 12 de 12 capturas, pared2 6 de 6, video 7 de 7 y buscador 11 de 11, salida 0 y **0 excepciones** en lo que escribió y en
  `rocola_error.txt`; mirada la captura de los 20 s de pared (la lista del disco, las dos cápsulas y la barra, en su sitio).
- **Junto con vistas-a** (sin tocar ninguna rama: `git merge-tree` de esta rama y `port-vistas-a`, `be48ade`): un solo choque, la línea 10 (los
  mismos `using`, con otro comentario); resuelto fuera del repositorio, el árbol junto compila (0 errores, 15 avisos) y el arnés de aquí sale
  igual a la referencia (0 distintas de 2 865). Con `port-ventanas2` (`1cc6ccb`), el mismo único choque de la línea 10 (no se compiló junto).

## Herramientas nuevas (`Pruebas/VistasVistasB`, fuera del programa)

- `VistasB.cs`, `VistasVistasB.csproj`: el arnés (el programa entero de una carpeta `Xp`, `-p:Fuente=…`, con otra entrada).
- `arnes.sh`: lo compila con la referencia, la base y esta copia, lo corre con las dos letras y compara.
- `mutantes.sh`: los 17 controles negativos.
- `aot-corre.sh`: el binario NativeAOT por las pantallas del grupo.

## Riesgos y diferencias que quedan

- **La línea 10 de `Form1.Vistas.cs`** (los `using`) la cambian también vistas-a y ventanas2: al juntar choca (el código es el mismo:
  `using System.Drawing; using Rocola.Dibujo2D; using Rocola.Nativo; using SkiaSharp;`; solo cambia el comentario).
- **ventanas2 ya tradujo `AnchoTexto` y `CreaSfDibuja`** en su rama y deja las firmas de XP en su puente: al juntar, lo de aquí sigue
  compilando por esas firmas, pero `AnchoTextoVistasB` y los `PuenteDibujo.Formato(CreaSfDibuja(…))` pueden pasar a la llamada directa.
- **Se tocaron archivos de otros grupos ya juntados** para quitar sus costuras (`AcabadoGpu.cs`, `DiscoVivoGpu.cs`, `SuenaVivoGpu.cs`,
  `Puente.pantalla-viva.cs`) y el arnés `VistasPantallaViva/PantallaViva.cs`; sus tres arneses salen iguales a la referencia.
- **`PintaPagina` ya no tiene firma con un `Graphics`**: nadie sin traducir la llama (solo este archivo y `AcabadoGpu.cs`).
- **Un `SKBitmap` soltado no lanza excepción, cierra la rocola** (`PORT_JUNTAS.md`): los hornos (`hornoPag`, `hornoAnt`, las cifras, las
  pastillas, los géneros) se sueltan en los mismos sitios y en el mismo orden que en XP; el arnés pasa por soltar y volver a hornear (cambio de
  medida, de letra y `Dispose`), pero no hay prueba que busque a propósito un uso después de soltar.
- **Las portaditas de la cola** siguen llegando como `Image` de la capa (`Portadas`): se pintan sus puntos sin copia; quien las suelta sigue
  siendo `Form1`.
- Las texturas de 1 024 de ancho de los recorridos dependen del instante (arriba): ninguna es de este grupo, pero en una máquina cargada no
  se puede pedir que salgan todas en una sola corrida.
- No se vio ningún fallo de XP en estas vistas que hubiera que conservar aparte.
- Nada probado en el A4.

## Lo que queda

- En este archivo, de `HeaderView` al final: las líneas de WinForms (`Control`, `OnPaint(PaintEventArgs)`, `this.Font`, `Invalidate`), que
  son de la pista de ventanas, y las costuras de la tabla.
- Quitar `Puente.vistas-b.cs` por partes: las firmas con un `Graphics` cuando se traduzcan `Form1.Video.cs`, `Form1.Portadas.cs` y
  `PrecargaPaginaGpu.cs`; `AnchoTextoVistasB` al juntar ventanas2; `SueltaMapasVistasB` con `Form1.Pared.cs`; `Ancho` con `FontFamily` con
  `Form1.cs` y `Form1.Datos.cs`.
- `RoundedPath` (`Form1.Fondo.cs`) y las portadas (`Form1.Biblioteca.cs`), cuando se traduzcan sus archivos.

## Revisión (11-11-2026, revisor independiente)

Revisado el diff `ae8b50b..port-vistas-b` contra `Rocola/Form1.Vistas.cs` (XP), `Plataforma/Dibujo2D.cs` (lo que hacía la capa en cada
llamada) y `Pruebas/TrazaDibujo/estado_por_linea.tsv`, y repetidas las comprobaciones. Sin fallos de dibujo ni de lógica.

**Lo mirado, línea por línea**

- Lo que queda de la capa de `HeaderView` al final son solo las costuras de la tabla (los `OnPaint(PaintEventArgs)`, `this.Font` y
  `AnchoTexto(texto, this.Font)` de `GeneroView`, que son de WinForms; `RoundedPath`, `CreaSfDibuja`, `Portadas`, `pideFuente`).
- Cada llamada frente a la de la capa: el pincel y la pluma (`new SKPaint()` con su color, ancho, unión y punta: lo mismo que ponen
  `SolidBrush.Pon` y `Pen.Pon`; el mínimo de un píxel y el límite de esquinas los pone `Pixel`), `MeasureString(s, f)` = caja (0, 0) sin
  formato, `MeasureString(s, f, 999, sf)` = caja de 999 × 999999, `DrawImageUnscaled` = entera en su tamaño (una nula no se pinta, como en
  la capa), `SetClip(…, Intersect)` = `ClipRect`/`ClipPath` sin suavizar, `Save`/`Restore` (entre cada par solo cambian la matriz y el
  recorte; el filtro de las portaditas se devuelve a mano, como en XP), `new Font(familia, pt, Bold)` = `Letra.EnPuntos(nombre, pt, true)`,
  `Font.Height` = `AltoEntero`, `LinearGradientBrush(caja, a, b, Vertical)` = `Degradado.Lineal(…, Degradado.Vertical, true)`,
  `FormatFlags = NoWrap` reemplaza, el tipográfico compartido es el mismo objeto, y los dos trazos redondos unen sus arcos como GDI+.
- El estado de cada línea coincide con la tabla (1138-1164, 1384-1462, 1630-1642, 1746-1821, 2060, 2124-2247); `PintaPagina` solo
  cambia el suavizado (por referencia) y sus tres llamadores le dan el estado de fábrica.
- Los puentes devuelven al `Graphics` el suavizado con las mismas condiciones de XP (encabezado y cápsula: si miden 8 o más; género: 6 o
  más y con texto; barra: siempre; lista: nada).
- Los hornos se sueltan y se ponen a `null` (o se vacía su diccionario) en los mismos sitios que en XP; no hay uso después de soltar en el
  orden de XP. Todo en el hilo de la pantalla.
- Finales de línea y número de líneas: `Form1.Vistas.cs` 2 749 líneas y 2 568 CRLF, como en la base; `AcabadoGpu.cs` 411, `DiscoVivoGpu.cs`
  621 y `SuenaVivoGpu.cs` 987, con LF. `git diff --numstat`: 287/287, 13/13, 6/6 (10/10 con lo de abajo) y 2/2.

**Lo encontrado y lo hecho**

- (menor) Cuatro notas `EN LINUX` de `DiscoVivoGpu.cs` (`AnchoDuracion`, `PlanFilaGpu`, `PlanPaginaGpu`, `PastillaSonandoEq`) seguían
  diciendo que `PintaPagina` estaba sin traducir y llegaba con su `Graphics` por el puente, cuando esas firmas ya se quitaron: corregidas
  (solo comentarios; commit `f2747bc`).
- (menor) El arnés tenía 17 controles negativos; se añadieron otros 10 a `mutantes.sh` en sitios que no tocaban (la duración a la
  izquierda, el fondo del «+N», el halo del punto de la página, el núcleo del neón, los puentes del género y de la barra sin devolver el
  suavizado, el circulito sin suavizar, el recorte de las portaditas suavizado, la pluma de la fila elegida y la copia olvidada en la
  lista): **los 10 se ven** (318, 41, 119, 42, 56, 411, 104, 262, 190 y 16 líneas distintas de 2 865).
- (para la junta, sin cambio) El comentario de `Plataforma/Puente.reposo.cs` (línea 25) aún nombra a `Form1.Vistas.cs` entre los que
  llaman a `PintaTextoMovil(Graphics…)`; ya no lo es (lo llama directo), pero esa firma sigue haciendo falta para `Puente.suena.cs`.
- (para la junta, sin cambio) `AnchoTextoVistasB` hace una `Font` (con su `Letra`) nueva en cada medida y la suelta; antes se medía con
  la letra guardada. Mide lo mismo (0 distintas en el arnés) y solo cuesta tiempo al rehacer el encabezado o la página; desaparece al
  juntar ventanas2 (`AnchoTexto(AjusteLetra, string, Letra)`).

**Comprobaciones repetidas** (en `/root/rocola-trabajo/port/vistas-b/rev`, carga de 3 a 5 en 4 núcleos)

- `dotnet build -c Release`: 0 errores, 15 avisos (`SYSLIB0054` ×9, `CA2022` ×2, `CS1690` ×2, `SYSLIB0050`, `CA2014`), antes y después
  del arreglo.
- `diff-xp.py -v`: `Form1.Vistas.cs` 291/288 con 1 sin marca (la línea 131, de la primera mitad, ya en la base); `AcabadoGpu.cs` 99/97,
  `DiscoVivoGpu.cs` 58/55 y `SuenaVivoGpu.cs` 172/169 con 0 sin marca.
- `arnes.sh` (referencia, base y esta copia, compilados de nuevo): 2 864 casos, **0 líneas distintas** contra la referencia y contra la
  base, con las letras falsas y con DejaVu (18 excepciones con DejaVu, las mismas en los tres); la salida es byte a byte la que guardó
  el traductor.
- Recorridos con el candado (pared, pared2, video y buscador; `VOLCADO`, `CAPAS`, `HUELLAS`) contra `cmp-ref` y `cmp-ref2`: los 4 salen
  con 0 y sin excepciones; capas quietas pared 24 de 24, pared2 12 de 12, buscador 22 de 22 y video 15 de 16 la primera vez (a los 24 s la
  captura cayó con tres capas en vez de cinco) y **16 de 16** al repetirlo; texturas quietas 82 de 83, 92 de 98, 75 de 78 y 50 de 50. Las
  10 que faltan son todas de 1 024 de ancho: 5 las saca esta misma copia en las corridas del traductor (`1c9507bb`, `584659ad`,
  `1f1712f1`, `e843916c`) y las otras 5 faltan también en corridas guardadas de la rama sin este grupo (`2a697d30` sale en 12 de 60,
  `21d8fe91` en 26, `2f591363` en 16 y `906bfb35` en 47 de 60 de pared2; `8cd9108c` en 28 de 59 de buscador). `906bfb35` (1 024×64, a
  los 62 s de pared2) no salió en ninguna de las tres corridas de esta copia: tampoco en las cuatro de pantalla-viva, las dos de la
  revisión de asistente ni en las de hoy de otros dos grupos sin este código; queda apuntada. `8c560fb6` de pared, que le faltó al
  traductor, aquí sí salió.
- Cintas (`VistasFrontera/cintas.sh`): **29 de 29 IGUALES** (6 616 cuadros).
- No se repitieron el banco por función (este grupo no toca `Nativo/` ni `Puente.cs`) ni la publicación NativeAOT (se miró su registro:
  0 avisos IL/AOT).
