# Port nativo: los rotulos y las letras (tanda 4, oleada 7: grupo rotulos-y-letras, W6 con «letras-de-los-controles»)

Seccion «7b. rotulos-y-letras» de PLAN_TANDA4_A_N7.md, sobre 8460045 (en paralelo con 6a gpupanel-sin-control). Los once Label de la
ventana de la rocola dejan de ser Label de la capa y son vistas nativas (RotuloVista), la Font de las vistas es la Letra nativa, y se van
los puentes de las letras y de la forma del aviso y el hueco del video de la capa (VideoEnVentanas.cs). La logica de XP no cambia.

## Lo hecho

### Nativo/

- **RotuloVista.cs (nuevo)**: el rotulo de la ventana de la rocola. Una `IVista` con su `CajaVista` y SOLO lo que XP les lee o les pone a
  sus once Label (el inventario): `Text`, `Visible`, `Font` (Letra), `ForeColor`, `BackColor`, `SetBounds`, `Bounds`, `Left`, `Top`,
  `Width`, `Height`, `Right`, `Bottom`, `Size`, `Location`, `TextAlign` (AlineaTexto), `AutoEllipsis`, `UseMnemonic`, `AutoSize` (XP solo
  lo pone en false, el de fabrica) y `Forma` (la `Region`). Reglas de Label.cs/Control.cs de WinForms: no seleccionable ni en el tabulador,
  100x23 de fabrica, ResizeRedraw, repinta solo si cambia (Text, colores, letra, TextAlign, UseMnemonic, AutoEllipsis), la letra con
  Font.Equals (poner una igual no cambia la guardada), el fondo por `Control.OnPaintBackground` (relleno si A != 0) y el texto como
  `Label.OnPaint` con el TextRenderer de GDI (`PinturaXp.Texto`: alineacion, WordBreak, TextBoxControl, EndEllipsis, NoPrefix; el «&»
  quitado como la capa), con la matriz del HDC que trae el lienzo. La `Forma` (Control.Region) suelta la anterior como el setter de
  WinForms y repinta (SetWindowRgn). No se llama `Rotulo` porque `Rotulo` (Nativo/Rotulo.cs) es la pieza de las hojas (otra caja: foco y
  tabulador de la hoja); las trazas lo nombran `RotuloVista` y `compara_trazas.py` lo toma por `Label`, como a `Rotulo`.
- **IVista.cs**: `SKPath Forma` (de fabrica null): la forma de la vista.
- **PantallaRocola.Vistas.cs**: la lista de la ventana es la de lo que va encima del panel (cabecera). La `Forma` en la composicion
  (`RecortaForma`: la vista solo dentro; a las de debajo las tapa solo con ella) y en el raton (`EnLaForma`: fuera de la forma el raton es
  del de debajo; el centro del pixel, como la capa), en la lista y en las hijas. Se van `AgregaRotulo` (lblAlbum entra al panel del disco
  con `AgregaVista`, como sus hermanas) y `ILaCapa.AgregaRotulo`/`LugarDelVideo`. Nuevo: `AgregaCapa(ventana, capa)` (la capa de textura del
  video a una ventana: en una de la capa, su hueco de textura; si no, la lista nativa), `CambioCapa`, y `VistaAlFrente`/`QuitaVista` con una
  capa de textura que esta en un hueco; `ILaCapa` gana `EsSuyaLaCapa`, `AgregaCapa`, `CapaAlFrente`, `QuitaCapa` y `CambioCapa`.
- **VideoEnPantalla.cs**: la capa del video es siempre la `CapaDeTextura` de la lista; ya no pide a la capa un lugar propio.
- **Letras.cs**: `Letras.Iguales(Letra, Letra)`, Font.Equals de GDI+ (familia sin mayusculas, tamano, unidad, negrita, cursiva, subrayado;
  la misma comparacion que `IgualLetra` de la capa).

### Codigo/ (cada linea con su «EN LINUX»; numero de lineas y de CRLF de cada archivo igual a 8460045)

- **Form1.cs**: los once campos `Label` -> `RotuloVista`; `new Label()` -> `new RotuloVista()`; `ContentAlignment` -> `AlineaTexto`;
  `Controls.Add(lbl...)` -> `PantallaRocola.AgregaVista(this, lbl...)` en el mismo orden; `lbl.BringToFront()` ->
  `PantallaRocola.VistaAlFrente(lbl)`; `AgregaRotulo(panelSongs, lblAlbum)` -> `AgregaVista(panelSongs, lblAlbum)`; las letras de lblAttract
  y lblConfig y la de pastGenero, Letra directo (fuera 3 `PuenteDibujo.Font`); `Font.Height` de tres rotulos con su Letra (fuera 3
  `PuenteDibujo.Letra`); `PastillaView.Ancho(..., this.Font.FontFamily.Name, ...)` (la familia por su nombre: la firma con FontFamily era
  Puente.vistas-b.cs); `lblToast.Region = RegionDeRuta(gp)` -> `lblToast.Forma = new SKPath(gp)` (new Region(gp) es una copia del trazo).
- **Form1.Video.cs** (no se tocan ~851-882, 981, 990, 1167): lblGpu (`new`, `AutoSize`/`Size`, Letra directo, AlineaTexto, AgregaVista),
  `lblAttract.BringToFront()` (102) y `lblGpu.BringToFront()` (996, la misma linea que el `gpuPanel.Visible = false` de 6a: posible choque
  de una linea al juntar), `DrawLabelOverlay` con `RotuloVista` y AlineaTexto, las medidas con la Letra del rotulo (2 `PuenteDibujo.Letra`).
- **EstiloGpu.cs, TextoMovilGpu.cs, SuenaVivoGpu.cs, Form1.Ajustes.cs**: la Letra del rotulo directo (fuera 4, 3, 6 y 1 `PuenteDibujo.Letra`);
  `PintaTituloEnCaja(RotuloVista)`; AlineaTexto en TextoMovilGpu; `lblConfig.BringToFront()` -> `VistaAlFrente` (Form1.Ajustes.cs:1785 y
  1799); las notas de `AnchoTexto(string, Letra)` y `AnchoToast(string, Letra)` ya no dicen que su firma de XP esta en el puente.
- **Form1.Fondo.cs**: `Bold(size)` da la Letra nativa (`new Font(this.Font.FontFamily, size, Bold)`: la misma familia en puntos y negrita);
  `TransLabel(RotuloVista)`.
- **Form1.Vistas.cs**: la `Font` de LetterBar, HeaderView, PastillaView, GeneroView y SongListView es `Letra` con lo de Control.Font: sin la
  suya, `CajaPieza.LetraDeFabrica` (Control.DefaultFont, un solo objeto), y con `Letras.Iguales` donde estaba `Control.IgualLetra`; las
  identidades de XP se conservan (HeaderView:1057/1067 `ReferenceEquals(pideFuente, this.Font)`, con `pideFuente` Letra); `FontFamily.Name`
  -> `Familia`, `SizeInPoints` -> `Puntos`, `Height` -> `AltoEntero`; fuera 5 `PuenteDibujo.Letra`; `AnchoTexto(texto, this.Font)` (1565) no
  cambia de texto: ahora llega directo a la de la Letra.
- **DiscoVivoGpu.cs**: `fuenteListaNormal/Grande` son Letra (la grande, `Letra.EnPuntos(familia, cuerpo, true)`), las identidades de
  77-81 (`ReferenceEquals(songList.Font, f)`) igual; `SizeInPoints` -> `Puntos`; fuera 5 `PuenteDibujo.Letra`.
- **AcabadoGpu.cs**: la firma de la pagina con `Font.Familia` y el numero de FontStyle de la Letra (Bold 1, Italic 2, Underline 4).
- **Form1.Datos.cs**: `PastillaView.Ancho(..., this.Font.FontFamily.Name, ...)`.
- Form1.Biblioteca.cs, FranjaLigeraGpu.cs y EstadoReposoGpu.cs: sin cambios (solo leen `Text`, `Bounds`... de los rotulos, que tienen los
  mismos nombres).

En todo Codigo/ quedan 0 `PuenteDibujo.Letra(` y 0 `PuenteDibujo.Font(` (en 8460045: 31 y 4 usos -el plan contaba 27 y 4 en
f5a33ca-: Form1.cs 4+3, Form1.Video.cs 3+1, EstiloGpu.cs 4, TextoMovilGpu.cs 3, SuenaVivoGpu.cs 6, Form1.Ajustes.cs 1, DiscoVivoGpu.cs 5,
Form1.Vistas.cs 5). Lo que queda de la Font de la capa en ellos es la del formulario (`this.Font` de Form1: Form1.cs:1266, 1485-1486,
Form1.Fondo.cs:1267, Form1.Datos.cs:3337), que es de la oleada 8 (form1-sin-form).

### Plataforma/

- Borrados: **VideoEnVentanas.cs**, **Puente.ventanas2.cs** (AnchoTexto(string, Font), AnchoToast(string, Font), RegionDeRuta) y
  **Puente.vistas-b.cs** (PastillaView.Ancho con FontFamily; era lo unico que le quedaba).
- **Anfitrion.cs** (sin tocar la parte del GpuPanel: `Capas()`, `Presenta`, `OlvidaTarjeta`): fuera `AgregaRotulo` y `LugarDelVideo`; el
  hueco de una vista toma tambien su `Forma` como `Region` de la capa (una copia del trazo, como new Region(GraphicsPath); repinta); el
  hueco de textura de una capa de textura (`HuecoDelVideo`: lo que hacia VideoEnVentanas.cs, con el mismo nombre porque las trazas de
  capas lo nombran asi, Pruebas/Windows/video.sh).
- **TrazaVentanas.cs** (fuera de la lista del grupo, una linea): `Pieza` pone el texto de un `RotuloVista` como el de un Label de la capa.

### Pruebas/

- `Ventanas/guiones/compara_trazas.py`: `RotuloVista` equivale a `Label`.
- `Ventanas/PantallaVistas`: 26 casos nuevos: el rotulo con forma (dentro, fuera sobre otra vista, fuera, el raton, sin forma, quitado)
  con un Label de la capa (lo que fija WinForms), con el RotuloVista en su hueco y sin la capa; y el video (agregado al fondo, AlFrente, Pon,
  Quita) en una ventana de la capa y sin ella.
- Arneses adaptados por reflexion (siguen compilando contra la referencia): donde la traduccion pide Letra se le da la de la Font que da el
  arnes (`Font.L`, la misma que `PuenteDibujo.Letra`), AlineaTexto con el numero de ContentAlignment, el rotulo es `RotuloVista`, y las
  firmas borradas (AnchoTexto/AnchoToast con Font, PastillaView.Ancho con FontFamily) se llaman como las llamaba el puente:
  VistasAcabadoEstilo, VistasVistasA, VistasVistasB, VistasReposo, VistasVideoForm1, VistasAjustes y VistasPantallaViva. VistasAcabadoEstilo y
  VistasPantallaViva ademas dejan de hacer `(Control)` a las vistas que ya no lo son (SongPagerBar, SongListView, GeneroView, PastillaView,
  ProgressView, desde la oleada 5): con eso el de acabado-estilo, que en 8460045 tronaba en `Paginas`, vuelve a correr entero.

## Costuras

Quitadas: los 11 Label de la capa en la ventana; la Font de la capa en las vistas y en la lista del disco; `PuenteDibujo.Letra` (31 usos en 8460045) y
`PuenteDibujo.Font` (4) en Codigo/; `RegionDeRuta`, `AnchoTexto(string, Font)`, `AnchoToast(string, Font)`, `PastillaView.Ancho(...,
FontFamily, ...)`; `VideoEnVentanas.cs`; `PantallaRocola.AgregaRotulo` e `ILaCapa.AgregaRotulo`/`LugarDelVideo`.

Anadidas (de la transicion, se van con la capa en N7): `ILaCapa.EsSuyaLaCapa/AgregaCapa/CapaAlFrente/QuitaCapa/CambioCapa` y
`HuecoDelVideo` en Anfitrion.cs; la `Region` del hueco desde la `Forma` de la vista. `PuenteDibujo.Letra/Font` siguen en Puente.cs
(compartido: los usa el banco, ejecutor puente) hasta N7.

## Comprobaciones (12-11-2026, maquina compartida con carga de 5 a 7, llvmpipe)

- a) `dotnet build -c Release --no-incremental`: 0 errores y los mismos 15 avisos de la base (la lista guardada en la carpeta de trabajo).
- b) diff-xp.py -v de los 14 archivos del grupo: las lineas «sin marca» son las mismas que en 8460045 (Form1.cs 2, Form1.Video.cs 4,
  Form1.Biblioteca.cs 2, Form1.Ajustes.cs 9, Form1.Fondo.cs 2, Form1.Datos.cs 2, Form1.Vistas.cs 2, las demas 0: comentarios de varias
  lineas de XP); todas las lineas cambiadas llevan «EN LINUX». Lineas y CRLF de los 11 archivos cambiados iguales a 8460045.
- c) NativeAOT (`dotnet publish ... -p:PublishAot=true`): 0 avisos IL/AOT.
- d) Recorridos (los nueve, con el candado y la carpeta de siempre): 9 de 9 salen con 0; capas quietas 211 de 217 y texturas quietas 450
  de 487. Las 6 capas: ctrlf3 a los 46 s (3, con F12 abierta: lo de PORT_f12-como-hoja.md) y hojas a los 18, 19,5 y 21 s (3, la hoja de
  Sonido con lo que el repaso lleva medido). Repetidas, igual. Para saber si son de este grupo: la BASE 8460045 compilada y los recorridos
  hojas y video corridos con ella y con esta rama, guardando las capturas y sus capas: 100 de 100 PNG iguales punto por punto (incluidas
  esas capas y la c21 del video, distinta de la referencia pero no quieta). Las texturas que faltan son las de la hora y del ritmo
  (1024x128, 512x256, 256x256, 1024x256, 1024x64), como en las juntas anteriores.
- e) Arneses contra la referencia congelada, con las letras de XP falsas y con DejaVu: acabado-estilo **0 de 6 603** (una linea,
  `lista-0-hojea100`, salio `TimeoutException` en la REFERENCIA por el reloj quieto con la maquina cargada; repetida sola, la misma huella
  que la nueva); vistas-b **0 de 3 108** (eran 2 865 en la junta 13: el arnes crecio con las oleadas 5 y 6); vistas-a IGUALES (1 266
  lineas); suena IGUALES (488); reposo IGUALES (1 791); video-form1 IGUALES (735); Ctrl+F3 (VistasAjustes) **0 de 545**; pantalla-viva
  IGUALES (768). Antes de los cambios de esta rama acabado-estilo y suena tronaban en 8460045 (casts a Control y Size de las vistas de la
  oleada 5): ahora corren enteros.
- Banco por funcion (se toco Nativo/Letras.cs): semillas 7 y 11 (letras falsas) y 5 (DejaVu), 3 000 casos, ejecutores gdi, nativo y
  puente contra el congelado: 9 de 9 IGUALES.
- Las 29 cintas: 29 iguales (6 616 cuadros), 0 distintas.
- f) pruebas41: **41 de 41**; pruebas_hoja: **86 de 86**; `AOT=1 pruebas_pantalla`: **32 de 32** en JIT y NativeAOT (sus 2 avisos de
  siempre); PantallaVistas: **143 de 143** (26 nuevas: el rotulo con forma en las tres maneras y el video con y sin la capa).
- Escenas alternadas con la referencia (ref-trazas; RUIDO=ventanas2/ruido), JIT, Rrotulos1/rotulos1: aviso, f3vistas, f3grises, f3teclas,
  f3raton, f3billar, arranque, moneda, video y buscador **IGUALES** (las trazas, el estado y los puntos); tab DIFERENTE solo por los rotulos
  de Ctrl+F3 con lo que el repaso lleva medido al abrirlo («33 %» frente a «0 %», «ya esta al dia» frente a «faltan 1 archivos») y los
  3 789 puntos de esos textos en la hoja: lo de PORT_armado-vistas.md y la junta 13 (depende de la carga; no es de este grupo). NativeAOT
  (con la fecha de dotnet en rocola-xp) contra la referencia JIT: arranque y f3vistas **IGUALES**.

## Lo que queda

- La Font del formulario (`this.Font` de Form1) sigue siendo la de la capa: oleada 8.
- `AlbumView.Font` (Form1.Pared.cs:1502, :998) sigue con la Font de la capa: Form1.Pared.cs no es de este grupo; nadie la pone (vale la de
  fabrica) y se lee por `FontFamily.Name`. Pasarla a Letra son tres lineas, en la oleada que tenga Form1.Pared.cs.

## Riesgos

- `Label.AutoEllipsis` en WinForms ensena un ToolTip con el texto entero al pasar el raton sobre un rotulo cortado; la capa (la referencia)
  no lo hacia y RotuloVista tampoco. En el bar no hay raton en XP.
- Los colores y la letra «ambientales» de WinForms (sin poner, los del dueno) no estan: XP pone fondo, color y letra a los once. Sin poner,
  el negro de fabrica y sin fondo.
- La Region de la capa (`Control.Region`, una propiedad sin mas) no repinta al cambiar; la del hueco y la Forma si (SetWindowRgn repinta):
  XP la pone justo antes de `Visible = true`, asi que no se ve la diferencia.
- Form1.Video.cs:996 lo tocan este grupo (lblGpu.BringToFront) y 6a (gpuPanel.Visible): choque de una linea al juntar.
