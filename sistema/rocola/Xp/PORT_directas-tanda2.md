# Port nativo: el grupo «directas-tanda2» (oleada 2, tanda 5; 12-11-2026)

Sección «2a. directas-tanda2» de PLAN_TANDA4_A_N7.md. Rama `port-directas-tanda2`, desde `bae626e`. Seis archivos de `Codigo/` que ya
dibujaban con SkiaSharp pero todavía llamaban a lo ya traducido a través de los puentes de la tanda 2 pasan a llamarlo directo; los
puentes que se quedan sin llamadores se borran. La lógica de XP no cambia: solo cambia la llamada (y las notas «EN LINUX»).

## Lo que se hizo (Codigo/)

| Archivo | Líneas | Antes (puente) | Ahora (directo) |
|---|---|---|---|
| SuenaVivoGpu.cs | 230, 362, 368, 374, 600 (y 232-233, 364-365, 370-371, 375, 602-603) | `Redondo` (GraphicsPath de la capa sobre el SKPath de `RutaRedonda`) y `PuenteDibujo.Ruta(p)` | `using (SKPath p = RutaRedonda(...))` y `p` |
| SuenaVivoGpu.cs | 210, 448 | `AnchoTextoSuena` (Letra → Font → `AnchoTexto(string, Font)` → `AnchoTexto(string, Letra)`) | `AnchoTexto(texto, letra)` (Form1.Ajustes.cs) |
| SuenaVivoGpu.cs | 551 | `PuenteDibujo.Letra(FuenteTituloReposo(w, h))` | `LetraTituloReposo(w, h)` |
| SuenaVivoGpu.cs | 593, 632 | `PintaTextoMovilSuena` (Graphics, Font, SolidBrush y StringFormat de la capa, y de vuelta) | `PintaTextoMovil(g, ajuste, copia, medio, ...)` (TextoMovilGpu.cs) |
| SuenaVivoGpu.cs | 704, 707, 715, 718 | `Upload(..., PuenteDibujo.Bitmap(x), ...)` | `Upload(..., x, ...)` (la de SKBitmap) |
| EstadoReposoGpu.cs | 353-354 | `DegradadoV` de XP (Brush de la capa) y `PuenteDibujo.Pincel(b, false)` | `DegradadoV(r, c0, c1, false)` (el shader) en `new SKPaint { Shader = ... }` |
| EstadoReposoGpu.cs | 385, 431 | `PintaTituloGrandeReposo`, `PintaMinutosColaReposo` (Graphics de la capa) | `PintaTituloGrande(g, suave, ajuste, false, false, ...)`, `PintaMinutosCola(...)` |
| AcabadoGpu.cs | 347-349 | `DegradadoV` de XP y `PuenteDibujo.Pincel(cuerpo, false)` | `DegradadoV(r, c0, c1, false)` en un SKPaint |
| AtraccionGpu.cs | 789, 803, 1011, 1039 (y 791-792, 805-806, 1012, 1044, 1050) | `Redondo` y `PuenteDibujo.Ruta(p)` | `RutaRedonda` y `p` |
| EstiloGpu.cs | 98, 100 | `gg = PuenteDibujo.GraphicsAcabado(...)` y `PintaArtistaCompacto(gg)` | `PintaArtistaCompacto(g, ajuste, copia, medio)` (sin `gg`) |
| EstiloGpu.cs | 195 | `TextoConCintaAcabado` (Font y Graphics de la capa) | `TextoConCinta(g, ajuste, texto, f, c, r, sf)` |
| TextoMovilGpu.cs | 175 | `PintaRenglonLejosReposo` (Graphics de la capa) | `PintaRenglonLejos(g, true, ajuste, copia, medio, ..., PuenteDibujo.Letra(lblNow.Font), ...)` |
| TextoMovilGpu.cs | 301 | `Upload(..., PuenteDibujo.Bitmap(tile.Source), ...)` | `Upload(..., tile.Source, ...)` |

Notas «EN LINUX» corregidas (decían «aún sin traducir» o nombraban los puentes borrados): SuenaVivoGpu.cs 17, 78, 205, 219, 346, 449,
572, 624; EstadoReposoGpu.cs 220, 247, 268, 335, 346; EstiloGpu.cs 25, 93, 99, 101, 102, 185; TextoMovilGpu.cs 149, 154, 179;
AtraccionGpu.cs 26; AcabadoGpu.cs 3, 212 y 260 (las dos últimas estaban en la lista de 3b, limpieza-puentes; nombraban
Puente.acabado-estilo.cs, que aquí se borra, así que se corrigen aquí, como EstiloGpu.cs:93; de la lista de 3b quedan GuiaRojoGpu.cs:140
y AcabadoGpu.cs:122).

No se tocan (como dice el plan): la moneda (SuenaVivoGpu.cs 695, 712, 724, 755: va a 5a), PrecargaPaginaGpu.cs, las letras de las
etiquetas de WinForms (`PuenteDibujo.Letra(lblX.Font)`, hasta W6), `GetIdlePanel` con Bitmap (EstadoReposoGpu.cs 118-181, D3D9Star.cs),
las piezas `uiPieza` (AcabadoGpu.cs 122 y 337) y las portadas que llegan como Image (AtraccionGpu.cs 547 y 955, EstadoReposoGpu.cs 147).

Los seis archivos conservan su número de líneas y sus finales (todos LF): SuenaVivoGpu 987, EstadoReposoGpu 446, EstiloGpu 259,
TextoMovilGpu 390, AtraccionGpu 1217, AcabadoGpu 411, como en `bae626e`.

### Por qué cada cambio es lo mismo

- **RutaRedonda.** `Redondo` del puente era `TrazoPantallaViva(RutaRedonda(r, radio))`: un GraphicsPath de la capa que lleva ESE SKPath
  (no una copia), y `PuenteDibujo.Ruta(p)` lo devolvía. Directo es el mismo objeto; soltarlo suelta lo mismo.
- **DegradadoV.** La firma de XP del puente seguía el mismo camino que la traducida (el mismo guardado de cuatro mapas, el mismo cerrojo y
  las mismas cuentas) pero con pinceles de la capa: `LinearGradientBrush(r, c0, c1, 90)` o un `TextureBrush` con su traslación, y
  `PuenteDibujo.Pincel(b, false)` los pasaba a un SKPaint con `Pon(p, centros: true)`: el mismo shader que da la traducida con
  `medio = false` (`Degradado.Lineal(r, c0, c1, 90)` o `Degradado.Textura(mapa, Repite, traslación, false)`) y el color negro opaco, que
  es el de fábrica de un SKPaint nuevo. Única diferencia: los de más de 2 048 de alto (abajo, «Riesgos»).
- **El medio píxel de los pinceles de color (el «cuidado» del plan).** Antes: SKPaint del que llama → `SolidBrush(ColorSk.DeSk(color))` →
  `PuenteDibujo.Pincel(brush, e.Medio)` → `SolidBrush.Pon`: `Shader = null; Color = color`. El color vuelve con los mismos 8 bits por
  canal y el medio píxel no se usa sin shader (solo corre la textura). `TextoGdiPlus.Escribe` copia el color y el shader del pincel a un
  SKPaint suyo (`Pixel.ConPincel`) y no le cambia nada al que recibe, y `PintaTextoMovil` solo mira si tiene shader (para la cinta): con
  el SKPaint directo pinta los mismos puntos. `EstadoReposoGpu.cs` ya le pasaba esos mismos SKPaint directo a `PintaTextoMovil` en las
  líneas 383, 427 y 430.
- **Las letras.** `PuenteDibujo.Font(l)` hace `new Font(familia, Tamano, estilo, unidad)`, que construye otra Letra con
  `Letra.EnPuntos/EnPixeles(familia, Tamano, ...)`: los mismos datos (familia ya resuelta, tamaño, unidad y estilo). Medir y escribir solo
  dependen de esos datos; la clave de la cinta (`CintasTexto.Add`) usa Familia, Tamano, estilo y unidad: la misma. La cinta no se queda la
  letra.
- **Los formatos.** `PuenteDibujo.StringFormat(f)` envolvía el MISMO FormatoTexto y `Formato(sf)` lo devolvía: directo es el mismo objeto.
- **El lienzo y su estado.** El Graphics del puente era uno sobre el mismo lienzo (`Lienzo(g)` es el lienzo) con el estado del que llama, y
  `PuenteDibujo.Estado` lo devolvía tal cual. `PintaTextoMovil`, `TextoConCinta` y `PintaArtistaCompacto` no reciben el suavizado (no lo
  usan; DrawString no lo mira). `PintaRenglonLejos` sí lo recibe pero no lo usa (lo pasaba a `TextoConCintaAcabado`, que lo dejaba en el
  Graphics sin mirarlo): desde TextoMovilGpu.cs:175 va `true`, el que le ponía `GraphicsReposo` (y el AntiAlias de quien llama en XP).
- **EstiloGpu.cs:98-100.** `gg` solo servía para `PintaArtistaCompacto(gg)`; el puente sacaba de él el mismo lienzo y el estado de la
  franja (ajuste, copia, medio). La traslación de la línea 99 la ven igual (va en el lienzo).
- **Upload.** `Upload(Bitmap)` de Puente.frontera.cs es `Upload(PuenteDibujo.Mapa(b))` y `PuenteDibujo.Bitmap(x).Sk` es `x` (y null da
  null): lo mismo que `Upload(x)`.

### Quién suelta la Letra de LetraTituloReposo (SuenaVivoGpu.cs:551)

Decidido: el campo `fTituloReposo` es su dueño, como en XP lo era de la Font de `FuenteTituloReposo`. Se suelta donde XP soltaba la Font
(la línea 550, al cambiar la medida de la pantalla). Antes, el puente hacía una Letra, una Font con sus datos, soltaba la Letra y daba la
Letra de esa Font (la Font nunca se soltaba: su `Dispose` es el de su Letra, que es lo que suelta 550). Soltar una Letra solo suelta sus
SKFont guardadas (`Letra.Dispose`); si se volviera a usar, las rehace.

## Puentes (Plataforma/)

Quitados, comprobado antes con grep en Codigo/, Plataforma/, Nativo/ y Pruebas/ y con el compilador (0 errores):

- **Puente.suena.cs, entero**: las firmas de XP de TamanoAviso, PintaAviso, PintaInvita, TamanoMas y PintaMas (PrecargaPaginaGpu.cs ya
  llama a las traducidas con SKBitmap y Letra), PintaTituloGrande y PintaMinutosCola con un Graphics (solo los llamaba Puente.reposo.cs),
  AnchoTextoSuena, PintaTextoMovilSuena, BrochaSuena y SueltaPincelSuena.
- **Puente.reposo.cs, entero**: las firmas de XP de TextoConCinta, PintaTextoMovil y PintaArtistaCompacto con un Graphics (sus llamadores
  ya estaban traducidos o eran puentes que se van), GraphicsReposo, PintaRenglonLejosReposo, SueltaPincelReposo, RenderIdlePanelEn,
  CopiaRegionReposo, UpdateIdleClock y UpdateIdleClockRegion con Bitmap (PrecargaPaginaGpu.cs ya va con SKBitmap), FuenteTituloReposo,
  PintaTituloGrandeReposo, PintaMinutosColaReposo y BrochaReposo.
- **Puente.acabado-estilo.cs, entero**: la firma de XP de PintaRenglonLejos con un Graphics, TextoConCintaAcabado y GraphicsAcabado.
- **Puente.asistente.cs:17-61**: la firma de XP de DegradadoV (Brush) y CopiaDegradadoAsistente; queda una nota de dos líneas. El resto
  del archivo (RenderContent de AyudaView) es de 4b.

Costuras que quedan muertas en archivos compartidos (no se borran aquí, regla 3): `Redondo` y `PuenteDibujo.TrazoPantallaViva` en
Puente.pantalla-viva.cs:40 y :49 (el archivo entero lo borra 3b, limpieza-puentes); `PuenteDibujo.Pincel` de Puente.cs:96 ya no lo llama
ningún archivo del programa (lo usan los arneses de reposo y suena por reflexión; 3b lo tiene en su lista); y
`AnchoTexto(string, Font)` de Puente.ventanas2.cs:24 sigue con un llamador (Puente.pantalla-viva.cs:70). Costuras nuevas: ninguna.

## Los arneses (Pruebas/)

Las firmas de XP que se borran ya no compilan en la traducción, y cada arnés se compila con la referencia congelada y con esta copia.
Pasan a reflexión, como ya hacían con «Nativa»/«Traducida»: si el programa tiene la de XP (la referencia), se llama esa; si no, la
traducida con lo mismo que le pasaba el puente borrado (los puntos del Bitmap por `PuenteDibujo.Mapa`, la Letra de la Font, el pincel por
`PuenteDibujo.Pincel` con el medio píxel del Graphics y soltado al volver, el mismo FormatoTexto, el lienzo y su estado; del panel de
reposo, el mismo Bitmap si vuelve el mapa dado o `PuenteDibujo.Entrega(..., Format32bppPArgb)` si no). Una excepción de dentro sale sin
envolver, como antes.

- VistasSuena/Suena.cs: `Xp`, `XpFuenteTituloReposo` y `XpConGraphics` para TamanoAviso, PintaAviso, PintaInvita (sus out), TamanoMas,
  PintaMas, FuenteTituloReposo, PintaTituloGrande y PintaMinutosCola.
- VistasReposo/Reposo.cs: la clase `Xp` con PintaTextoMovil, TextoConCinta, PintaArtistaCompacto, FuenteTituloReposo, RenderIdlePanelEn,
  CopiaRegionReposo, UpdateIdleClock y UpdateIdleClockRegion (18 llamadas y la de PintaArtistaCompacto).
- VistasAsistente/Asistente.cs: la firma de XP de DegradadoV por reflexión (sin ella, la traducida con el medio píxel del Graphics, como la
  línea directa). Se quitan las tres líneas `degradado-grande-xp-*` de las dos salidas: medían que la firma de XP del puente lanzara
  ArgumentNullException al pintar (como la capa de la referencia); esa firma ya no existe y la traducida la comparan las líneas
  `degradado-grande-*` contra GDI+ con el mapa vivo.
- VistasAcabadoEstilo/Vistas.cs:224: PintaRenglonLejos por `Llama` (la de XP o la traducida con el estado del Graphics; la Font a su Letra
  y StringAlignment a Alinea).

VistasAtraccion y VistasPantallaViva no llamaban a nada de lo borrado (PantallaViva busca `Redondo` por reflexión y sigue estando).

## Comprobaciones (con números)

- a) `dotnet build -c Release`: 0 errores y los mismos 15 avisos que `bae626e` (comparados línea por línea).
- b) `diff-xp.py` de los seis archivos `-v`: SuenaVivoGpu 169 puestas, EstadoReposoGpu 113, EstiloGpu 52, TextoMovilGpu 47, AtraccionGpu
  161, AcabadoGpu 99, todas con su marca salvo dos `{` de AtraccionGpu.cs que ya estaban así en `bae626e` (mismas cuentas antes y
  después: cambian líneas, no se añaden).
- c) NativeAOT (`dotnet publish -p:PublishAot=true`): 0 avisos IL/AOT (solo los 15 del compilador).
- Arneses (cada uno con la referencia congelada, letras de XP falsas y DejaVu):
  - suena: IGUALES las dos (487 líneas);
  - reposo: IGUALES las dos (1 790 líneas);
  - asistente: IGUALES las dos (1 986 líneas, sin las tres `degradado-grande-xp-*`);
  - acabado-estilo (`BASE=bae626e`): 0 distintas de 6 603 contra la referencia y 0 de 6 607 contra la base, con las dos letras;
  - atraccion: IGUALES las dos (19 119 líneas);
  - pantalla-viva: IGUALES las dos (767 líneas).
- Las 29 cintas (`VistasFrontera/cintas.sh`): 29 iguales (6 616 cuadros), 0 distintas.
- El banco no se corrió: no se tocó Nativo/ ni Puente.cs.
- d) Recorridos: abajo.
- f) Escenas: no se tocan ventanas, pantalla, teclas, ratón ni cierre.

## Recorridos (d)

Los 9 recorridos con el candado, `VOLCADO=1 CAPAS=1 HUELLAS=1`, contra `cmp-ref` y `cmp-ref2`: los 9 salieron con 0. La máquina estaba
cargada (carga 8 a 11 con otros agentes), así que se corrió también la base `bae626e` (sin estos cambios) igual, para separar el ruido:

| | texturas quietas | capas quietas |
|---|---|---|
| base `bae626e`, una vuelta | 441 de 487 | 211 de 217 |
| esta rama, primera vuelta | 471 de 487 | 212 de 217 |
| esta rama, con ctrlf3, hojas y pared repetidos | 477 de 487 | 213 de 217 |

- Las texturas que faltan en la primera vuelta (dos 1024×128 en ctrlf3 y en hojas, una 512×256 en pared) son subidas que no llegaron a
  hacerse (el recorrido acabó o pasó de largo antes), no puntos distintos: repetidos, ctrlf3 y hojas sacan las mismas huellas en los
  mismos segundos que la referencia, y pared (tercera vuelta) las nueve 512×256 de la referencia, en su orden. Las 10 que faltan al final
  (ayuda 2, buscador 2, pared 1, pared2 5, todas 1024×128, 1024×256 o 1024×64) son las que dependen de la hora, como en la junta del
  11/12-11 (477 de 487); la base también las pierde.
- Las 4 capas quietas distintas son la `capa4` de la hoja de Ctrl+F3 de hojas a los 18-24 s (el repaso, que va según la carga); la base
  da 6 de esas mismas. Ninguno de los archivos de este grupo dibuja esa hoja.
- Las cintas del texto que se desliza, el panel de reposo, la franja de lejos y la atracción (lo que cambia aquí) salen con las mismas
  huellas que la referencia en atraccion (32 de 32), botones, video (50 de 50) y el resto.

Pruebas/Inventario (la herramienta de Roslyn, sobre la base y sobre esta rama): gdi 360 → 348, winforms 1 804 → 1 804 (solo bajan).

## Lo que queda

- 5a: la moneda (`BakeMoneda` con Bitmap, SuenaVivoGpu.cs 695-755) y PrecargaPaginaGpu.cs.
- 3b: Puente.pantalla-viva.cs entero (con `Redondo`, ya muerto), `PuenteDibujo.Pincel` (Puente.cs:96) cuando los arneses dejen de
  usarlo, y las notas que le quedan.
- W6: las letras de las etiquetas de WinForms (`PuenteDibujo.Letra(lblX.Font)` en SuenaVivoGpu.cs, EstiloGpu.cs y TextoMovilGpu.cs).
- 4a: las portadas que llegan como Image y el panel de reposo que sube `GetIdlePanel` con Bitmap.

## Riesgos

- **Los degradados de más de 2 048 de alto** (riesgo de PORT_asistente.md): por la firma de XP del puente lanzaban
  ArgumentNullException al pintarlos (la capa, como la referencia, soltaba el mapa antes de pintar); ahora EstadoReposoGpu.cs y
  AcabadoGpu.cs van a la traducida, que los pinta como GDI+ (Skia copia los puntos al hacer el shader). Es un cambio frente a la base,
  hacia lo que hace XP. Solo pasa con un vidrio de la lista del disco de más de 2 048 px de alto (no en 1024×768) y nunca en el panel de
  reposo (mide como mucho 340). Ninguna prueba lo ve fuera del arnés del asistente (`degradado-grande-*`).
- **La lógica de DegradadoV ya no está duplicada** (el otro riesgo de PORT_asistente.md): queda solo la traducida.
- Ninguno nuevo de la lógica de XP: nada de XP se reordenó ni se cambió.
