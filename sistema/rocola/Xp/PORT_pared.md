# Port nativo, tanda 3 del dibujo: el grupo «pared»

Rama `port-pared` (sale de `ae8b50b`). Carpeta de trabajo, fuera del repositorio: `/root/rocola-trabajo/port/pared`.
Todo lo de abajo está medido en la máquina de pruebas (llvmpipe, fuera de pantalla). **Nada está probado en el A4.**

## Qué se tradujo

Cinco archivos de `Codigo/`, línea por línea: cada línea cambiada lleva su `// EN LINUX:` y los cinco conservan su número de líneas y
sus finales (`Form1.Pared.cs`: 1 678 líneas, 1 470 con CRLF, antes y después; los otros cuatro, LF).

| Archivo | Qué dibuja | Cómo queda |
|---|---|---|
| `Form1.Pared.cs` | la atracción por procesador (`AttractView`), el cubo (`Cube.Draw`), el halo y las siete ayudas de los botones (`GlowAround`, `CtrlDot`, `CtrlTxt`, `CtrlTri`, `CtrlPad`, `CtrlDirLeft`, `CtrlDirRight`), la pared (`AlbumView`: las casillas por procesador y para la tarjeta, la etiqueta, el palomito, el contorno, el atlas, la pared entera por `OnPaint`) y el cubo de la canción (`CubeBox`) | `SKCanvas` con el estado en variables o en parámetros (suavizado, modo de letra, copia, filtro, medio píxel); pinceles y plumas en `SKPaint`; letras en `Rocola.Nativo.Letra`; formatos en `FormatoTexto`; mapas en `SKBitmap` (`Mapas.Nuevo`); `LockBits` son los puntos mismos (`GetPixels`, `RowBytes`); `ColorMatrix` es la matriz de 5×5 en `Imagenes.Pinta`; `PathGradientBrush` es `Degradado.DeTrazo` |
| `CaratulasSeparadasGpu.cs` | la casilla por separado: la marca, la etiqueta, las tiras del contorno (`PackEdge`, `PegaBordes`) | igual; las portadas enteras siguen siendo `Bitmap` de la capa (clave por identidad) |
| `PrecargaPaginaGpu.cs` | el precalentado | sus mapas son `SKBitmap`; `EnSeco` y `BakeMoneda` (de otros) siguen dando tipos de la capa, escritos enteros |
| `PaletaCaratulaGpu.cs` | la paleta de una portada | lee los puntos de un `SKBitmap` según el formato que tenía el `Bitmap` en XP (`FormatoMapa`) |
| `PreviewsIndexadosGpu.cs` | nada (solo entrega miniaturas) | ya no necesita `using Rocola.Dibujo2D`; el tipo va escrito entero |

El estado del `Graphics`, según `Pruebas/TrazaDibujo/estado_por_linea.tsv` (76 líneas de estos archivos: 42, 20, 12, 2 y 0) y, para lo
que no está en la tabla, por el código de quien llama:

- **Fijo** en los hornos (un `Graphics` nuevo sobre un mapa: sin copia ni medio píxel; el suavizado y el filtro que XP pone).
- **Parámetro** en `Cube.Draw` (suavizado, copia, filtro, medio píxel) y en el halo y las ayudas (suavizado, modo de letra, copia, medio
  píxel): los llaman con estados distintos.
- **Lo que un método deja cambiado**: `DrawWall` cambia el filtro (`Bilinear`) si pinta el cubo, y en XP quedaba así en el `Graphics` del
  que llama: el filtro va con `ref`, y los tres `OnPaint` le dejan al `Graphics` el filtro, el suavizado y el modo de letra que XP le dejaba.

Nativo nuevo: `Nativo/FormatoMapa.cs` (solo un `enum`: el formato que tenía un mapa en XP; sin cuentas, las de leer están en `Pixeles.cs`).
Añadido a la lista del banco (`Pruebas/BancoDibujo/Nuevo/Nuevo.csproj`).

Lo que se sube a la tarjeta y ahora es `SKBitmap` (Bgra8888 premultiplicado, de `Mapas.Nuevo`): `GpuGridCell.Bmp`, `CellTex.Bmp`,
`lastContour` y `GetContour` (`D3D9Star.cs`: solo esas líneas de lectura, sin `PuenteDibujo.Mapa`). Las comparaciones por identidad y
quién suelta los puntos son los de XP.

## Costuras que deja este grupo

Todas en `Plataforma/Puente.pared.cs` salvo donde se dice. Ninguna de las traducidas le cambia el estado al `Graphics`.

| Costura | Para quién | Se quita cuando |
|---|---|---|
| `Cube.Draw(Graphics, Rectangle, Image, double)` | `Form1.Buscador.cs` (1 llamada) | se traduzca `Form1.Buscador.cs` |
| `GlowAround`, `CtrlDot`, `CtrlTxt`, `CtrlTri`, `CtrlPad`, `CtrlDirLeft`, `CtrlDirRight` con `Graphics` (y `Font`) | `Form1.cs` (`HelpBar`), `Form1.Buscador.cs`, `Form1.Vistas.cs` | se traduzcan esos tres |
| `CB(Color)` que da un `SolidBrush`; en `Form1.Pared.cs` el pincel se llama `PincelCtrl` (y la pluma `PlumaCtrl`) | `Form1.Vistas.cs` (`SongPagerBar`, 3 usos) | se traduzca `Form1.Vistas.cs`; entonces `PincelCtrl` puede volver a llamarse `CB` |
| `SueltaBitmaps(ref Bitmap[])` | `Form1.Vistas.cs` (las cifras horneadas) | se traduzca `Form1.Vistas.cs` |
| `ExtractCoverPalette(Bitmap)` y `PuenteDibujo.FormatoMapaPared` | `Form1.Fondo.cs`, `Form1.Portadas.cs` | esos dos entreguen `SKBitmap` con su formato |
| las portadas son `Rocola.Dibujo2D.Image`/`Bitmap` (escrito entero): `GetCover`, `GetCoverByPath`, `PeekCoverGpu`, `PeekPreviewGpu`, `CubeBox.Cover`, `Relevo`, `UsaPortada`, `GpuGridCell.Preview`/`FullCover`, `GpuCubeInfo.CoverA`/`CoverB`, `nativeCovers` | las da Form1 (`Form1.Biblioteca.cs`, `Form1.Portadas.cs`, `CaratulasAdaptativas.cs`) y las leen `Form1.Video.cs`, `AtraccionGpu.cs`, `PantallaVivaGpu.cs`, `DiscoVivoGpu.cs`; al pintarlas, `PuenteDibujo.Mapa` (los mismos puntos) | la caché de portadas dé `SKBitmap` (con ella se quita también la costura de pantalla-viva sobre `PeekCoverGpu`) |
| `e.Graphics` en los tres `OnPaint` (`PuenteDibujo.Lienzo` y `Estado`) y `Starfield.Draw`/`DrawReactive` con el `Graphics` | WinForms (no es de esta tanda) y `Form1.Fondo.cs` | la pista de ventanas dé el lienzo y se traduzca `Form1.Fondo.cs` |
| `RoundRect` devuelve un `GraphicsPath` de la capa (`PuenteDibujo.Ruta`, 3 usos en el contorno) | `Form1.Ajustes.cs` (rama de ventanas) | `RoundRect` dé un `SKPath` |
| `EnSeco(…, delegate (Rocola.Dibujo2D.Graphics g))` y `BakeMoneda` (un `Bitmap` de la capa) en `PrecargaPaginaGpu.cs` | `Form1.Video.cs`, `SuenaVivoGpu.cs` | se traduzcan |

## Costuras de otros grupos que se quitaron

Con `Form1.Pared.cs` traducido, 27 llamadas pasan a ir directas (lienzo y estado) en `AsistenteRocola.cs`, `AsistenteGuiaRocola.cs`,
`AyudaCasillaGpu.cs`, `BuscadorPortadasGpu.cs` y `AcabadoGpu.cs`, y se borran `CtrlDotAsistente`, `CtrlTxtAsistente`, `CtrlTriAsistente`
y `GraphicsAsistente` (`Puente.asistente.cs`) y `CtrlPadPortadas`, `CtrlDotPortadas` y `CtrlTxtPortadas`
(`Puente.buscador-portadas.cs`). `GraphicsAcabado` se queda: lo siguen usando `EstiloGpu.cs` y la cinta de texto.

## Comprobaciones (11-11-2026, con el código de los commits)

- **(a) Compila**: `dotnet build -c Release`, 0 errores y 15 avisos, los mismos de la base.
- **(b) Banco** (`Pruebas/VistasPared/banco.sh`): semillas 7 y 11 con las letras falsas y 5 con DejaVu, 3 000 casos cada una, ejecutores
  gdi, nativo y puente contra el Congelado: **0 distintos en las nueve**; puente-objetos, 407 bien y 0 mal.
- **(c) Recorridos** (los nueve, con el candado): 9 de 9 salen con 0, sin excepciones. Capas de Dibujo2D quietas **215 de 217**: las dos
  son las que ya apunta `PORT_JUNTAS.md` y no son de este grupo (la hoja de Ctrl+F3 de `hojas` a los 18 s y la de Ctrl+F2 de `ctrlf3` a
  los 40 s, que solo cambió de nombre). Texturas quietas, contando las veces: 465 de 487 en la primera corrida (`cmp`), hecha con la
  máquina cargada (carga 5; «Pantalla parada 1140 ms» en `rocola_error.txt`): en `pared` faltaron cuatro de 512×256 de los primeros 9 s
  y una franja. Segunda corrida de siete recorridos (`cmp2`): `pared` **83 de 83**, `video` 50 de 50. Juntando las dos, **376 de las 381
  texturas quietas distintas salen idénticas**; las cinco que no (`2a697d30…` y `2f591363…` de `pared2`, `8cd9108c…` y `e843916c…` del
  buscador, `a9dc2d55…` de `ayuda`) son de la hora y tampoco salen en 26 a 43 de las más de 50 corridas guardadas de la referencia y de
  los grupos ya juntados (`/root/rocola-trabajo/port/pared/union.py`). `atraccion`: 32 de 32 y 10 de 10 capas.
- **(d) Arnés** (`Pruebas/VistasPared/arnes.sh`): **1 715 líneas iguales a la referencia congelada con las dos letras** (1 714 casos, 673
  llamadas directas a la traducida): el cubo con nueve estados del `Graphics`, las ayudas, las casillas (por procesador, para la tarjeta,
  por separado, chicas), el atlas, el contorno, la paleta en cada formato, el precalentado y los tres `OnPaint` con el estado que le dejan
  al `Graphics`. Controles negativos (`control.sh`, fallos metidos a propósito): de los 15 primeros, 14 se ven (de 8 a 465 líneas distintas). El
  que no se veía, «las tiras del contorno sin suavizar», no puede verse: un rectángulo de un punto en coordenadas enteras sale igual con
  suavizado y sin él; se cambió por «un punto más anchas» (270 líneas; el mismo camino con otro color da también 270). Se añadió uno más,
  «la mezcla de portadas con la mitad del fundido» (56 líneas). Quedan 16, y los 16 se ven; los dos nuevos se corrieron sueltos con el
  mismo mecanismo, no con `control.sh` entero otra vez. Los arneses de los grupos
  cuyas costuras se quitaron (`otros.sh`): asistente 1 990 líneas, buscador-portadas 3 128 y acabado-estilo 6 607, 0 distintas.
- **(e) Cintas** (`Pruebas/VistasFrontera/cintas.sh`): **29 de 29 iguales (6 616 cuadros)**.
- **(f) NativeAOT**: `dotnet publish -c Release -r linux-x64`, 0 avisos IL/AOT. El binario por `pared` y `atraccion`
  (`Pruebas/VistasPared/aot-corre.sh`): sale con 0, 12 de 12 y 5 de 5 capturas, 0 excepciones en lo que escribe y en `rocola_error.txt`.
  Las capturas se miraron (la pared con su contorno y la atracción con su cubo) pero no se compararon punto por punto con las de JIT.
- **(g) `diff-xp.py`**: `Form1.Pared.cs` 231 quitadas y 228 puestas (1 sin marca: una llave `{` sola que el diff desplaza),
  `CaratulasSeparadasGpu.cs` 37 y 35, `PrecargaPaginaGpu.cs` 21 y 20, `PaletaCaratulaGpu.cs` 14 y 14, `PreviewsIndexadosGpu.cs` 3 y 3
  (0 sin marca en los cuatro).

## Otros archivos tocados

`Codigo/D3D9Star.cs` (las líneas de lectura de `GpuGridCell.Bmp`, `CellTex.Bmp`, `lastContour` y `GetContour`); `AcabadoGpu.cs`,
`AsistenteRocola.cs`, `AsistenteGuiaRocola.cs`, `AyudaCasillaGpu.cs`, `BuscadorPortadasGpu.cs` (las llamadas que ya van directas);
`Plataforma/Puente.asistente.cs` y `Puente.buscador-portadas.cs` (costuras borradas); `Pruebas/VistasAsistente/control.sh` (un control
que metía el fallo en el puente borrado lo mete ahora en la traducida); `Pruebas/BancoDibujo/Nuevo/Nuevo.csproj` (`FormatoMapa.cs`).

## Lo que queda y los riesgos

- Las portadas siguen siendo imágenes de la capa (arriba): es la costura grande de este grupo.
- La paleta depende de que quien llama diga el formato que el `Bitmap` tenía en XP; hoy lo lee el puente del `Bitmap` de la capa. Cuando
  la caché de portadas dé `SKBitmap`, tendrá que guardar ese formato (un JPEG leído es de 24 bits; si se pierde, la paleta de las portadas
  con transparencia cambia).
- `lblFont` toma la familia de `this.Font` (la letra del control, de WinForms): cuando la vista deje de ser un `Control`, hay que darle
  ese nombre.
- El suavizado de `PackEdge` no se puede comprobar con puntos (arriba): está razonado por el código de XP.
- Las líneas que los recorridos no pasan (el dibujo por procesador de los `OnPaint`, la casilla chica, el fundido del cubo TOP) están
  comprobadas solo por el arnés, con datos fijos.
