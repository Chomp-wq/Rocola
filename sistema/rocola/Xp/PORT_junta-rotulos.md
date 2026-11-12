# Port nativo: la junta de 7b (rotulos-y-letras) sobre 6a (gpupanel-sin-control)

Oleada junta de la tanda 4 (PLAN_TANDA4_A_N7.md, oleada 7): `git merge --no-ff port-rotulos-y-letras` (7b, que salio de 8460045) sobre
ed5040c (la rama principal con 6a gpupanel-sin-control ya juntada). Los dos grupos rediseñaron a la vez la pantalla nativa: 6a hizo del
GpuPanel una vista de la tarjeta (PORT_gpupanel-sin-control.md) y 7b hizo de los Label vistas nativas con forma y paso el video a huecos de
textura (PORT_rotulos-y-letras.md). Se juntan las dos cosas sin perder ninguna. La logica de XP no cambia: en Codigo/ no se toco nada a
mano (git junto solo los archivos de los dos grupos).

## Lo hecho

### Los choques y como se resolvieron (commit 6495c74)

- **Nativo/IVista.cs**: la interfaz lleva los dos miembros nuevos, `DeLaTarjeta` (6a: la pinta la tarjeta, el GpuPanel de XP) y `Forma`
  (7b: la Region de WinForms, solo el aviso sin tarjeta). La cabecera explica los dos (una linea por grupo; la de 7b dice tambien que los
  rotulos son vistas no seleccionables con su TabIndex en la coleccion).
- **Nativo/PantallaRocola.Vistas.cs** (cuatro choques; lo demas lo junto git y se reviso a mano):
  - la cabecera de la lista de la ventana une las dos: vistas pintadas por procesador o por la tarjeta, capas de textura (el video) y la
    lista de lo que va encima del panel (el panel es una vista de la lista; el video y los rotulos tambien), con su orden por los
    Controls.Add y BringToFront de XP (`AgregaVista`, `VistaAlFrente`), y la forma de una vista;
  - `ILaCapa`: lo de 6a (`Presenta` solo para la hoja F12, `PresentaEnVista`, `OlvidaEnVista`, `VentanaMinimizada`) y lo de 7b
    (`EsSuyaLaCapa`, `AgregaCapa`, `CapaAlFrente`, `QuitaCapa`, `CambioCapa`); `LugarDelVideo` y `AgregaRotulo` se van (7b);
  - `AgregaRotulo` se va (7b: lblAlbum entra al panel del disco con `AgregaVista`). Lo que 6a le habia anadido (el TabIndex del rotulo,
    `AnotaTab(rotulo, l)`) no se pierde: lblAlbum es una vista y `AgregaHija` le da su TabIndex por coleccion (`AnotaTab(v, l)`, de 6a);
  - `AgregaCapa(t)` con su TabIndex por coleccion (6a) y, junto a ella, `AgregaCapa(ventana, t)` y `CambioCapa` (7b).
  - Se juntaron solos y se comprobo que encajan: la vista de la tarjeta como corte de los tramos de la composicion (6a) y la forma al
    pintar, al recortar a las de debajo y en el raton (7b: `RecortaForma`, `EnLaForma`); una vista de la tarjeta encima recorta a las de
    debajo con su rectangulo (no tiene forma: XP solo se la pone al aviso sin tarjeta); `VistaAlFrente` y `QuitaVista` con la capa del
    video en su hueco (7b) junto al olvido de las vistas de la tarjeta al quitarlas (6a); `TraspasaHijas` sin la rama del rotulo de la capa.
- **Nativo/PantallaRocola.Foco.cs** (sin choque, una nota): `HijasTab` decia que «los rotulos no se eligen ni tienen hijas: saltarlas da lo
  mismo»; ahora los rotulos son vistas (RotuloVista) que no se eligen ni se paran en el tabulador (Seleccionable y ParaElTabulador false,
  como Label), y asi lo dice.
- **Plataforma/Anfitrion.cs**: los huecos de textura del video (7b: `EsSuyaLaCapa`, `AgregaCapa`, `CapaAlFrente`, `QuitaCapa`,
  `CambioCapa` y la clase `HuecoDelVideo`) y lo de 6a (`Presenta` solo para la hoja F12, `PresentaEnVista`, `OlvidaEnVista`,
  `VentanaMinimizada`, el hueco «Nativa» de la vista de la tarjeta y sus avisos de MouseMove y MouseWheel). La cabecera nombra los dos
  huecos (el «Nativa» del GpuPanel y el del video); las notas de 7b que decian «como el GpuPanel» dicen ahora «como el hueco del
  GpuPanel» (el GpuPanel ya no es un control), y la del hueco del video dice que su TabIndex lo pone el Controls.Add de la capa.
- **Pruebas/Ventanas/PantallaVistas/Program.cs**: las pruebas de los dos, una despues de otra (la vista de la tarjeta y su tabulador de 6a;
  el rotulo con forma y el video de 7b). La vista falsa ya tenia `Tarjeta`/`DeLaTarjeta` (6a) y el rotulo usa `RotuloVista.Forma` (7b).
- **Codigo/**: sin choques. Form1.Video.cs:996 (la linea que tocaban los dos) lleva el `PantallaRocola.VistaAlFrente(lblGpu)` de 7b; el
  `gpuPanel.Visible` de esa linea es el de la CajaVista (6a) sin cambio de texto. Los 109 archivos con el numero de lineas y de CRLF de
  ed5040c.

### El TabIndex por coleccion para todo

Como pedia la junta, el TabIndex por coleccion de 6a (el mayor de los hermanos + 1, Control.ControlCollection.cs:82-95) vale tambien para
lo de 7b: un rotulo (RotuloVista) en la lista nativa lo recibe en `AgregaVista` (`AnotaTab(v, lista)`) y dentro de una vista en
`AgregaHija` (`AnotaTab(v, l)`); una capa de textura (el video) en la lista nativa en `AgregaCapa(t)`. En una ventana de la capa, el rotulo
en su hueco y el video en su hueco de textura reciben el suyo del Controls.Add de la capa, como el control de WinForms.

## Costuras

Ninguna quitada ni anadida por la junta: quedan las de 6a y 7b tal como las describen sus documentos (de la transicion, se van con la capa
en N7: `ILaCapa.PresentaEnVista/OlvidaEnVista/VentanaMinimizada`, `ILaCapa.EsSuyaLaCapa/AgregaCapa/CapaAlFrente/QuitaCapa/CambioCapa`, el
hueco «Nativa» de la vista de la tarjeta y sus avisos de MouseMove y MouseWheel, `HuecoDelVideo` y la Region del hueco desde la Forma).
Las dos que la junta podia haber dejado duplicadas no lo estan: `LugarDelVideo` (6a aun lo conservaba) y `AgregaRotulo` (6a le habia
anadido su TabIndex) se van con 7b.

## Comprobaciones (12-11-2026, maquina compartida con carga de 2 a 5, llvmpipe)

- a) `dotnet build -c Release --no-incremental`: **0 errores y los 15 avisos**, la misma lista archivo(linea): aviso que la base (la de
  gpupanel-sin-control/avisos-base.txt: SYSLIB0054 x9, SYSLIB0050, CA2022 x2, CA2014, CS1690 x2).
- b) diff-xp.py -v de los 13 archivos de Codigo/ que cambian los dos grupos: las lineas «sin marca» son las de los documentos de 6a y 7b,
  todas comentarios de XP de varias lineas (D3D9Star.cs 11, Form1.Ajustes.cs 9, Form1.Video.cs 4, Form1.Biblioteca.cs 2, Form1.Datos.cs 2,
  Form1.Fondo.cs 2, Form1.Vistas.cs 2, Form1.cs 2; AcabadoGpu, DiscoVivoGpu, EstiloGpu, SuenaVivoGpu y TextoMovilGpu 0). Los **109
  archivos de Codigo/ con su numero de lineas y de CRLF iguales a ed5040c**.
- c) NativeAOT (`dotnet publish RocolaXp.csproj -c Release -r linux-x64 -p:PublishAot=true`): sale con 0, **0 avisos IL/AOT** (solo los
  15 del compilador de siempre).
- PantallaVistas (pruebas_vistas.sh): **194 de 194** (las 168 de 6a con su revision y las 26 de 7b).
- f) pruebas41 **41 de 41**; pruebas_hoja **86 de 86**; `AOT=1 pruebas_pantalla` **32 de 32** en JIT y en NativeAOT (sus 2 lineas de
  avisos de siempre).
- El ciclo del tabulador (16 Tab desde los 7 s, cada 0,4 s, la traza del foco), referencia ref-trazas y esta junta: las dos
  `HelpBar LetterBar AlbumView ProgressView GpuPanel` x3 + `HelpBar LetterBar` (17 cambios de foco iguales); al final la junta apunta
  «foco nadie» del apagado (SIGTERM de timeout), lo aceptado.
- e) Las 29 cintas: **29 iguales (6 616 cuadros), 0 distintas**.
- e) Arneses contra la referencia congelada:
  - VistasAcabadoEstilo (ref y nuevo): **0 de 6 603** con las letras falsas y con DejaVu (6 607 lineas, 0 con excepcion: el GpuPanel sin
    construir de 6a y los rotulos de 7b juntos no tronaron);
  - VistasAjustes (Ctrl+F3): **0 de 545** con las dos letras;
  - VistasVistasA: **IGUALES** (1 266 lineas) con las dos letras;
  - VistasVistasB: **0 de 3 108** con las dos letras (las 18 excepciones con DejaVu estan tambien en la referencia);
  - VistasFondo: 2 763 lineas, distinta solo la primera, la marca `inicio traducida=True/False` (la de siempre, PORT_fondo.md);
  - VistasFrontera (compara.sh, sk y puente, letras falsas y DejaVu): **106 lineas, 0 distintas; 153 texturas, 0 distintas**.
- f) Escenas alternadas con la referencia congelada (JIT, jrR1/jrN1, RUIDO=ventanas2/ruido), las 23: **IGUALES** arranque, moneda, video,
  tab, buscador, cierreq, altf4, kill9, asistente, salirq, f3vistas, f3grises, f3teclas, f3raton, f3billar y f12 (16); sigterm y
  f3sigterm DIFERENTES por lo aceptado (rocola_estado.txt se borra al apagar: closing/closed WindowsShutDown); captura, f2, f2barras y f2sin
  solo por la cuenta del encargo MedidasBitacora.cs:129 y aviso solo por la del reloj Form1.cs:724 (118 frente a 127, holgura 6):
  repetidas una vez (jrR2/jrN2) las cinco **IGUALES**.
- NativeAOT (con la fecha de dotnet en rocola-xp) alternada con la referencia JIT (jrRA/jrNA): **arranque, cierreq, kill9, buscador,
  f3vistas, f3teclas y f12 IGUALES** (7 de 7).
- d) Recorridos (los nueve, con el candado y la carpeta de siempre): **9 de 9 salen con 0**. Capas quietas **213 de 217**: las cuatro de
  siempre de la junta 13 (ctrlf3 c46 capas 3, 4 y 6, con F12 abierta: PORT_f12-como-hoja.md; hojas c18 capa 4, la hoja de Sonido con lo
  que el repaso lleva medido). Texturas quietas **478 de 487** (las mismas que la junta 13): faltan en ayuda, buscador y pared2 las de la
  hora y del ritmo (1024x128, 512x256, 1024x256, 128x128). La c21 de video (capas 1 y 3) sale distinta pero no es quieta (como en 7b).
- No corrido: el banco por funcion (la junta no toca ninguna funcion de dibujo de Nativo/ ni Puente.cs; Letras.cs viene tal cual de 7b,
  que lo paso: 9 de 9). Los arneses suena, reposo, video-form1 y pantalla-viva no son de la lista de la junta (los primeros los toca a la
  vez arreglo-junta14).

## Lo que queda

- Lo que dejan 6a y 7b (sus documentos): la Font del formulario y `AlbumView.Font` (oleada 8 y la de Form1.Pared.cs), `Form1.Video.cs:879`
  con `searchOverlay.ClientRectangle`, `Escritorio.HayMensajes/EsperaMensajes` y la `Nativa` de Control (N7), y el Invalidate(true) del
  panel sin la capa (oleada 8).

## Riesgos

- Los de 6a y 7b siguen (las cintas nuevas con el identificador de la superficie; una vista de la tarjeta dentro de otra vista no se
  compone; el ToolTip de AutoEllipsis; la Region de la capa que no repinta).
- `AgregaCapa(t)` de una capa que ya esta en la lista nativa no hace nada; un Controls.Add otra vez en WinForms la manda al fondo (lo que 6a
  hizo para las vistas). XP agrega el control del video una sola vez (VideoEnPantalla.Lugar crea una capa nueva cada vez), asi que no se
  ve; se apunta para N7.

## Revision

Revisor independiente: se leyo el merge entero (`git show --cc 6495c74` y `git diff ed5040c..HEAD` de Nativo/, Plataforma/, Codigo/ y
PantallaVistas) contra los dos padres y contra Rocola/.

- Los choques estan bien resueltos: IVista lleva DeLaTarjeta y Forma; ILaCapa lleva lo de 6a y lo de 7b sin LugarDelVideo ni
  AgregaRotulo (ni una llamada viva a VideoEnVentanas, AgregaRotulo o LugarDelVideo fuera de comentarios); AgregaCapa(t) con su TabIndex
  por coleccion; la composicion (corte de tramos de la vista de la tarjeta y la forma al pintar, recortar y en el raton) encaja.
  Form1.Video.cs:981-996 sigue el orden de XP (Controls.Add del panel, BringToFront del panel, BringToFront de lblGpu si falla).
- Encontrado y arreglado (menor): la tabla del TabIndex de la lista nativa (PantallaRocola.Foco.cs) era un Dictionary que guardaba para
  siempre cada vista o capa anotada. Con la junta, cada video crea su capa (VideoEnPantalla.Lugar) y AgregaCapa le pone TabIndex: sin la
  capa (lo que queda tras N7), una capa por video se quedaba viva en la tabla. Ahora es una ConditionalWeakTable (por identidad, como el
  campo _tabIndex de Control: la vista lo conserva mientras vive, al quitarla y volverla a poner tambien). Ninguna cuenta cambia.
- Repetido: build Release 0 errores y 15 avisos (los de la base); NativeAOT 0 avisos IL/AOT (antes y despues del arreglo); los 109 archivos
  de Codigo/ con las lineas y los CRLF de ed5040c; diff-xp.py -v de los 11 archivos de Codigo/ cambiados: las mismas lineas sin marca que
  ya tenian (comentarios y llaves de XP); PantallaVistas 194 de 194 (antes y despues del arreglo); pruebas41 41 de 41; pruebas_hoja 86 de
  86. No repetidos (lo del informe, sin dudas de la revision): recorridos, escenas, cintas y arneses de vistas (la junta no cambia ninguna
  linea de Codigo/ a mano ni lo que se sube a la tarjeta; el arreglo solo toca la lista nativa, que esos no usan).
- Sigue el riesgo de AgregaCapa repetida (no va al fondo); XP no lo hace.
