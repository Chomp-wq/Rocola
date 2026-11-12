# Port nativo de XP: `Form1.Vistas.cs` y `DiscoVivoGpu.cs` llaman directo a lo ya traducido (grupo «directas-vistas»)

Grupo «1b. directas-vistas» de la oleada 1 (tanda 4) de `PLAN_TANDA4_A_N7.md`, 12-11-2026. Rama `port-directas-vistas`, base `178722c`.
Decisiones del dueño en `AGENTS.md` (8-11-2026: traducir el código de XP para que llame directo a lo nativo de Linux, sin capas que imiten
a Windows). Las tandas 2 y 3 dejaron traducidos `Form1.Pared.cs` (las ayudas de los botones, `SueltaBitmaps`), `Form1.Ajustes.cs`
(`AnchoTexto`, `CreaSfDibuja`) y `Form1.Fondo.cs` (`ContornoRedondo`), pero `Form1.Vistas.cs` y `DiscoVivoGpu.cs` seguían llamándolos por
puentes con un `Graphics`, una `Font`, un `StringFormat` o un `GraphicsPath` de la capa (`Plataforma/Dibujo2D.cs`). Este grupo cambia esas
llamadas por las directas y borra las costuras que se quedan sin nadie que las llame. Nada se probó en el A4: todo es fuera de pantalla con
llvmpipe.

## Qué se hizo

La lógica de XP no cambia: solo la llamada al puente pasa a ser la llamada a la traducida, con los mismos datos (el lienzo y su estado, la
misma `Letra`, el mismo `FormatoTexto`, el mismo trazo). Cada línea cambiada lleva su `// EN LINUX: ...`; los dos archivos conservan su
número de líneas y sus finales de línea (`Form1.Vistas.cs`: 2 749 líneas, 2 568 con CRLF; `DiscoVivoGpu.cs`: 621, todas LF), como en la
base. `git diff --numstat`: 50 y 10 líneas cambiadas, las mismas puestas que quitadas.

`Codigo/Form1.Vistas.cs`:

| Línea(s) | Antes (por el puente) | Ahora (directo) |
|---|---|---|
| 10 | `using Rocola.Dibujo2D` | fuera: lo que queda de la capa (el `Graphics` de `OnPaint`, la `Font` del control, las portadas `Image` de Form1) ya va escrito entero (`Rocola.Dibujo2D.X`) |
| 38, 40, 44, 46 (`BarraAvance` de Ctrl+F3) | `GraphicsPath pa = RoundedPath(...)` y `PuenteDibujo.Ruta(pa)` | `SKPath pa = ContornoRedondo(...)` y `pa` |
| 162, 163, 171, 172 (`SongPagerBar`) | `CtrlDirLeftVistasA`, `CtrlTxtVistasA`, `CtrlDirRightVistasA` (un `Graphics` sobre el mismo lienzo y una `Font` hecha de la `Letra`) | `CtrlDirLeft`, `CtrlTxt`, `CtrlDirRight` de `Form1.Pared.cs` con el lienzo, el suavizado, el modo de letra, la copia, el medio píxel y la `Letra` |
| 1030, 1341, 1579, 2113 | `PuenteDibujo.Formato(CreaSfDibuja(StringAlignment.Near/Center))` | `CreaSfDibuja(Alinea.Cerca/Centro)` de `Form1.Ajustes.cs`: el mismo `FormatoTexto` (el puente devolvía su envoltorio y `Formato` sacaba el mismo objeto) |
| 1063, 1064, 1083, 1109, 1147, 1156, 1207, 1208, 2224, 2242 | `AnchoTextoVistasB(g, ajuste, t, f)` (un `Graphics` con ese modo de letra y una `Font` de la `Letra`) | `AnchoTexto(ajuste, t, f)` de `Form1.Ajustes.cs`: de un `Graphics`, `MeasureString` solo mira su modo de letra |
| 1325, 1326 (`PastillaView.Ancho`) | `AnchoTextoVistasB(t, f)` | `AnchoTexto(t, f)` |
| 1580-1583, 1651-1654, 1662-1675, 1766-1769, 1854-1857, 2088-2089, 2153 | 10 `RoundedPath` (un `GraphicsPath` dueño del `SKPath` de `ContornoRedondo`) y 17 `PuenteDibujo.Ruta` | 10 `ContornoRedondo` (el `SKPath`) y el `SKPath` directo; el `using` y el `p.Dispose()` de 2090 lo sueltan igual que XP suelta el `GraphicsPath` |
| 1913 | `SueltaMapasVistasB(ref ...)` | `SueltaBitmaps(ref SKBitmap[])` de `Form1.Pared.cs` (la misma cuenta) |
| 178, 1239, 1451 | notas viejas | 178: queda una sola firma de XP de `LetterBar.RenderContent`; 1239 y 1451: `Form1.Biblioteca.cs` ya está traducido, lo que sigue con `Image` de la capa es su caché de portadas (hasta la oleada cache-portadas) |

`Codigo/DiscoVivoGpu.cs`: los 9 `AnchoTextoPantallaViva` pasan a `AnchoTexto`: 235 `AnchoTexto(t, Letra)`; 336, 362, 383, 385 y 405
`AnchoTexto(ajuste, t, fCola)`; 313, 337 y 363, con la `Font` del control de la lista, `AnchoTexto(ajuste, t, PuenteDibujo.Letra(Font))`
(la misma `Letra`; se va con W6, cuando la letra del control sea una `Letra`). La nota de la línea 17 dice ahora que solo queda
`Plataforma/Puente.cs` (la `Font` del control y la `Image` del cubo).

Todas las notas «aún sin traducir» de estos dos archivos que hablaban de `Form1.Pared.cs`, `Form1.Ajustes.cs`, `Form1.Fondo.cs` o
`Form1.Biblioteca.cs` están corregidas. Las que dicen «WinForms, aún sin traducir» hablan de la `Font` o la `Image` que da un control o la
caché de portadas, y siguen siendo ciertas.

### Por qué es lo mismo

- `AnchoTextoVistasB(g, ajuste, t, f)` hacía un `Graphics` con `EstadoLienzo(false, ajuste, false, Bilineal, false, identidad)` y una
  `Font` con la familia, el tamaño, la unidad y el estilo de `f`, y llamaba a `AnchoTexto(Graphics, string, Font)` del puente, que devolvía
  `AnchoTexto(Estado(g).Letra, t, Letra(Font))`: el mismo modo de letra y una `Letra` con los mismos datos. Las comprobaciones de nulos son
  las mismas (sin letra o sin texto, 0). Hacer el `Graphics` sobre el lienzo no le cambia nada al lienzo (`Dibujo2D.cs:540`: solo lee su
  `SaveCount`).
- `CtrlDirLeftVistasA` y los otros dos llamaban a la firma de XP de `Puente.pared.cs`, que sacaba del `Graphics` el suavizado, el modo de
  letra, la copia y el medio píxel que acababa de ponerle, y la `Letra` de la `Font` que acababa de hacer: los mismos valores que ahora
  pasan directo.
- `RoundedPath` (`Puente.fondo.cs:20`) es un `GraphicsPath` cuyo `SKPath` es el de `ContornoRedondo`; `PuenteDibujo.Ruta` devuelve ese mismo
  `SKPath`, y soltar el `GraphicsPath` suelta el `SKPath`.
- `SueltaMapasVistasB` era, línea por línea, `SueltaBitmaps(ref SKBitmap[])` de `Form1.Pared.cs:304`.
- `CreaSfDibuja(StringAlignment)` del puente devolvía `PuenteDibujo.StringFormat(CreaSfDibuja((Alinea)(int)h))`, y `PuenteDibujo.Formato`
  sacaba de él el MISMO `FormatoTexto` (`Puente.cs:85`); `Near`/`Center` son 0/1, `Alinea.Cerca`/`Centro`.

## Costuras quitadas

| Archivo | Qué | Por qué se puede |
|---|---|---|
| `Plataforma/Puente.vistas-a.cs` | `LetterBar.RenderContent(Graphics)` (la de un argumento) | muerta: `OnPaint` (`Form1.Vistas.cs:558`) y `DrawCtrlOverlay` (`Form1.Video.cs:1787`) llaman a la de `(Graphics, bool)`, y el arnés vistas-a también (`VistasA.cs:284`) |
| | `CtrlDirLeftVistasA`, `CtrlDirRightVistasA`, `CtrlTxtVistasA` y `PuenteDibujo.GraphicsVistasA` | `Form1.Vistas.cs` llama a las de `Form1.Pared.cs`; nadie más las llamaba (grep en Codigo, Plataforma, Nativo y Pruebas) |
| `Plataforma/Puente.vistas-b.cs` | las dos `AnchoTextoVistasB`, `AnchoTextoPantallaViva(SKCanvas, AjusteLetra, string, Letra)` y `SueltaMapasVistasB` | `Form1.Vistas.cs` y `DiscoVivoGpu.cs` llaman directo; nadie más |
| `Plataforma/Puente.ventanas2.cs` | `CreaSfDibuja(StringAlignment)` | el único que la pedía era `Form1.Vistas.cs`; el arnés VistasAjustes (`Vistas.cs:93`) la buscaba por reflexión: ahora, si no está, hace lo que hacía el puente (la de `Alinea` y `PuenteDibujo.StringFormat`, por reflexión), y contra la referencia sigue llamando a la de XP |
| `Plataforma/Puente.pared.cs` | `ctrlBCapa` y `CB(Color)` | muertos (ya lo decía el inventario: solo salían en comentarios) |
| | `SueltaBitmaps(ref Bitmap[])` | `Form1.Vistas.cs` ya tiene `SKBitmap[]` y llama a la traducida |

No se añadió ninguna costura.

### Costuras que quedan muertas en archivos de otros grupos (no se tocan: las borra la limpieza 3b o N7)

- `Plataforma/Puente.pantalla-viva.cs:60-82`: las dos `AnchoTextoPantallaViva` y `PuenteDibujo.GraphicsPantallaViva` ya no las llama nadie
  (`DiscoVivoGpu.cs` era el único). El archivo es de la oleada 3 (3b lo borra entero).
- `Plataforma/Puente.ventanas2.cs:18`, `AnchoTexto(Graphics, string, Font)`: ya solo la llaman `Puente.pantalla-viva.cs:65` (muerta) y el
  arnés VistasAjustes por reflexión (`Vistas.cs:69`); 3b la borra. `AnchoTexto(string, Font)` (:24) sigue viva para `Puente.suena.cs:60`.

### Notas viejas fuera de mis archivos (las corrige su dueño)

- `Codigo/Form1.Pared.cs:250` (`PincelCtrl` se llama así porque `CB` estaba en el puente: ya no está; 2b lo vuelve a llamar `CB`) y `:304`
  (`SueltaBitmaps`: «quien aún tiene Bitmap[] de la capa (Form1.Vistas.cs) llega por el puente»: ya nadie).
- `Codigo/Form1.Fondo.cs:771`: la firma de XP de `RoundedPath` ya no la pide `Form1.Vistas.cs` (siguen `Form1.Buscador.cs`, que la quita
  1a, y `Form1.Ajustes.cs`, que la quita 2b). Es de 4a.
- `Plataforma/Puente.pared.cs:7-12` (la cabecera: todavía nombra `CB` y `SueltaBitmaps` para `Form1.Vistas.cs`) y `Puente.fondo.cs:16`
  (nombra `Form1.Vistas.cs`): solo podía tocar las líneas 51-63 de `Puente.pared.cs`; las cabeceras las arregla quien borre esos archivos.

## Comprobaciones

Todo en esta máquina (4 núcleos compartidos con otros grupos, carga media de 5 a 6), fuera de pantalla con llvmpipe; carpeta de trabajo
`/root/rocola-trabajo/port/directas-vistas`.

- **Compilar (JIT)**: `dotnet build -c Release` desde cero: 0 errores y los mismos 15 avisos de la base (comparados línea a línea con los
  de `178722c`).
- **diff-xp.py** (`Form1.Vistas.cs DiscoVivoGpu.cs -v`): `Form1.Vistas.cs` 495 quitadas / 492 puestas (en la base, 493 / 490: las dos de
  más son las líneas 38 y 44 de `BarraAvance`, que antes eran iguales a XP y ahora llevan su marca), 2 sin marca, las mismas dos `}` que
  ya salían en la base; `DiscoVivoGpu.cs` 58 / 55, 0 sin marca (igual que en la base: sus líneas ya eran de traducción).
- **Líneas y finales de línea**: `Form1.Vistas.cs` 2 749 líneas, 2 568 CRLF; `DiscoVivoGpu.cs` 621, 0 CRLF; los dos como en `178722c`.
- **NativeAOT** (`dotnet publish -r linux-x64 -p:PublishAot=true`): 0 avisos IL/AOT, 0 errores (la publicación se borró después).
- **Arneses** (cada uno contra la referencia congelada, con las letras de XP falsas y con DejaVu):
  - vistas-a (`VistasVistasA/arnes.sh`): IGUALES las dos, 1 266 líneas, 722 llamadas directas a la traducida;
  - vistas-b (`VistasVistasB/arnes.sh`, con `BASE=178722c`): 0 distintas de 2 865 contra la referencia y 0 contra la base, con las dos
    letras (las 18 excepciones de DejaVu salen igual en la referencia);
  - Ctrl+F3 (`VistasAjustes/compara.sh`): 0 distintas de 545 con las dos letras, 0 excepciones; las 78 líneas de los formatos ya pasan por
    el camino nuevo (la de `Alinea` por reflexión), porque la firma con `StringAlignment` ya no está;
  - pantalla-viva (`VistasPantallaViva/arnes.sh`): IGUALES las dos, 768 líneas, 291 directas;
  - pared (`VistasPared/arnes.sh`): IGUALES las dos, 1 715 líneas, 673 directas.
- **Las 29 cintas** (`VistasFrontera/cintas.sh`): 29 iguales (6 616 cuadros), 0 distintas.
- **Recorridos** (los nueve, con el cerrojo, contra `cmp-ref` y `cmp-ref2`): 9 de 9 salen con 0; capas quietas 216 de 217, texturas
  quietas 480 de 487. Lo que falta no es de este cambio, y se comprobó con el programa de la base (`178722c`, compilado aparte) corrido en
  seguida con el cerrojo, con los mismos recorridos (pared2, hojas, ayuda y buscador):
  - la capa que falta es la hoja de Ctrl+F3 de «hojas» a los 18 s (`c18.png.capa4`, la del repaso de la biblioteca, que va al ritmo de la
    máquina; ya salió así en juntas anteriores): la base también la da distinta (96 de 97 capas quietas en esos cuatro, igual que este
    cambio), y la segunda corrida de este cambio también;
  - las texturas: buscador 76 de 78 y ayuda 65 de 66 (dependen de la hora, como en la última junta), y pared2 94 de 98 (cuatro de
    1 024×128 y 1 024×256). La base, en la misma tanda, pierde 3 de pared2, 2 del buscador y 4 de la ayuda; y este cambio, en su segunda
    corrida, 4, 3 y 12: lo que falta cambia de corrida a corrida con el programa de antes y con el de ahora.
  - Este cambio contra la base, en esos cuatro recorridos: 97 capas iguales y 1 distinta (`c16.5` de «hojas», que tampoco es quieta: la
    referencia contra sí misma ya la da distinta).
- **Escenas alternadas** con la referencia (`alterna.sh`, etiquetas `Rdirectas-vistas1`/`directas-vistas1`, `compara.sh` con
  `RUIDO=.../ventanas2/ruido`): arranque, moneda, video y f3vistas IGUALES. «tab» sale DIFERENTE, repetida una vez también (etiquetas
  `...2`): la hoja de Sonido y la de la calidad dicen que el repaso de la biblioteca aún no acabó («faltan 5 archivos», «8 aceptables»,
  «0 %») cuando en la referencia ya está al día (12 medidas, «33 %»), y por eso la captura de esa hoja tiene 5 080 puntos distintos. Es el
  ritmo del repaso con la máquina cargada, no el dibujo: el programa de la base (`178722c`, sin este cambio) alternado con la referencia en
  la misma escena da la misma diferencia («faltan 2 archivos», 3 853 puntos; etiquetas `...3`). Este grupo no toca ni el repaso ni la hoja.
- **Inventario** (`Pruebas/Inventario`, la misma herramienta sobre la base y sobre este cambio): gdi baja de 390 a 375 referencias,
  winforms queda en 1 804; en `Form1.Vistas.cs`, gdi de 43 a 28 referencias (de 32 a 17 líneas: lo que queda es el `Graphics` de los
  `OnPaint`, la `Font` del control y las portadas `Image`), y en `DiscoVivoGpu.cs` gdi sigue en 8 (la `Font` del control, hasta W6).
- **No hacía falta**: el banco (no se tocó `Nativo/` ni `Puente.cs`) ni las pruebas de ventanas (no se tocaron ventanas, pantalla, teclas,
  ratón ni cierre).

## Lo que queda

- `Form1.Vistas.cs`: la herencia de `Control` de las 7 vistas y sus firmas de XP de `RenderContent` con un `Graphics` (lo que queda de
  `Puente.vistas-a.cs` y `Puente.vistas-b.cs`): 5b (vistas-disco); la `Font` del control y `PastillaView.Ancho` con `FontFamily` (W6); las
  portadas `Image` de Form1 (4a, cache-portadas).
- `DiscoVivoGpu.cs`: `PuenteDibujo.Letra(Font)` en 206, 213, 313, 337 y 363 y la `Font` de la capa en 42-80 (W6); `PuenteDibujo.Mapa` de
  la portada del cubo en 212 (4a).

## Riesgos

- Ninguno de lógica: cada llamada recibe los mismos valores que le daba el puente. Lo único que cambia por dentro es que ya no se hace una
  `Font` nueva con los datos de la `Letra` para volver a sacar de ella una `Letra` nueva: se usa la misma `Letra`. Si una `Letra` llevara algo
  que `PuenteDibujo.Font` no copia (solo copia familia, tamaño, unidad, negrita, cursiva y subrayado), antes se perdía en el viaje y ahora
  no; en estas medidas no hay nada así (los arneses dan lo mismo que la referencia con las letras de XP falsas y con DejaVu).
- Antes, un lienzo nulo habría lanzado dentro del puente al hacer el `Graphics`; ahora `AnchoTexto` no mira el lienzo. En XP un `Graphics`
  nulo daba 0 en `AnchoTexto`; en el código traducido el lienzo nunca es nulo en estas llamadas (es el de `RenderContent` o el de 1×1 de
  `AnchoPedido`), así que no cambia nada que se pueda ver.
