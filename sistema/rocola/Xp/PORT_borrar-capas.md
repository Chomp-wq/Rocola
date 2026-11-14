# Port nativo: borrar-capas (oleada 9 = N7: el borrado final de las capas)

Seccion «9a. borrar-capas» de `PLAN_TANDA4_A_N7.md` y «LA LISTA DEL BORRADO FINAL» de `PORT_n7-adelanto.md`, sobre `535be08` (8a
form1-sin-form y 8b arneses-sin-capa ya juntados). Rama `port-borrar-capas`. Es el final del port nativo: en `sistema/rocola/Xp` ya no
queda ninguna clase que imite a Windows. El programa compila sin `Dibujo2D.cs`, sin `Ventanas.*.cs`, sin puentes y sin el
`global using Rocola.Ventanas;`, en JIT y en NativeAOT. La referencia congelada (`/root/rocola-trabajo/referencia-xp`, aef2211, y su copia
con ganchos `ref-trazas`) no se toco: sigue compilando su propia capa y contra ella se comparo todo.

Nada de esto esta probado en el A4.

## Lo hecho

### 1. Borrado de Plataforma/ (3 840 lineas en 13 archivos)

| Archivo | Lineas | Que era |
|---|---|---|
| `Dibujo2D.cs` | 776 | GDI+ de imitacion sobre SkiaSharp: `Graphics`, `Bitmap`, `Image`, `Font`, `Brush`, `Pen`, `StringFormat`, `GraphicsPath`, `Region`... |
| `Ventanas.Control.cs` | 1 287 | `Control`, `Label`, `Panel`, `Button`, `ListBox`... de imitacion |
| `Ventanas.Escritorio.cs` | 830 | `Escritorio`: las ventanas de la capa, su foco, su raton, sus teclas y su pintura |
| `Ventanas.Form.cs` | 599 | `Form`, `Application`, `MessageBox` de imitacion |
| `Ventanas.Tipos.cs` | 108 | los tipos sueltos de WinForms (`PaintEventArgs`, `MouseEventArgs`, `Cursors`...) |
| `Traduccion.cs` | 5 | el `global using Rocola.Ventanas;` |
| `Puente.cs` y `Puente.{asistente,buscador,fondo,pared,video-form1,vistas-a}.cs` | 235 | `PuenteDibujo` y las firmas de XP con un `Graphics` que quedaban |

Antes de borrar se comprobo que nadie los usaba (los tres `grep` del paso 0 de la lista): de `Rocola.Dibujo2D` y `PuenteDibujo`, solo
`Anfitrion.cs` (lineas 19, 211 y 304); de `Rocola.Ventanas`, solo `Anfitrion.cs`, `Traduccion.cs` y la costura de `VolcadoEstado.cs`.
En `Codigo/` no hubo que cambiar ni una linea de codigo: con las capas borradas, el compilador no se quejo en ningun archivo de XP
(8a ya habia dejado `Form1.cs:1030-1062` y `Form1.Pared.cs:1502` en lo nativo). Se quedan en `Plataforma/` (no imitan nada): `Aot.cs`,
`Form1.Pantalla.cs`, `Mp3Xp.cs`, `NAudioXp.cs` (`Rocola.Puente`, el de NAudio 3: no es `PuenteDibujo`), `PruebaAot.cs`, `PruebasLinux.cs`,
`PruebasSonido.cs`, `TeclasLinux.cs`, `UnicodeXp.cs`, `TrazaVentanas.cs` y `VolcadoEstado.cs` (ganchos de prueba: se quedan ahi porque
`Pruebas/Ventanas/ganchos-referencia.patch` pone los mismos en la `Plataforma/` de la referencia).

### 2. `Plataforma/Anfitrion.cs` -> `Nativo/Anfitrion.cs` (de 348 a 110 lineas)

`git mv`; la clase conserva su nombre (`PruebasLinux.cs` no cambia). Se fue todo lo que unia la pantalla con la capa: `VentanasDeLaCapa`
(la implementacion de `PantallaRocola.ILaCapa`), `HuecoDeVista`, `HuecoDelVideo`, `VistaDeHueco`, `SueltaLaCapa`, `IAnfitrion` y la mano de
`Escritorio.CursorActual`. Lo que queda es nativo:

- abrir la pantalla (KMS o fuera de pantalla), el compositor, la capa del video, las teclas de evdev y de prueba: igual que antes;
- **las capas de un cuadro** (`Capas`), que antes pedia a `Escritorio.Capas()`: ahora las hojas salen de `Hoja.Abiertas` con las MISMAS
  reglas que tenia `Escritorio.Capas` para ellas (se copiaron linea por linea): cada hoja visible, su superficie y la version que se pinto
  (`Hoja.Superficie`, UNA vez por cuadro y ANTES de pedir los tramos de la ventana, como siempre); si la tarjeta pinta en su cliente (F12),
  esa textura justo despues; las que quedaron detras de la ventana (`DetrasDeLaVentana`), antes que todo; el orden final es el de antes
  (detras, la ventana de la rocola, las hojas). La clave de cada capa (con la que el compositor guarda su textura) dura lo que la hoja se
  ve, como el diccionario que llevaba `Escritorio`;
- lo que ponia el constructor estatico de `Escritorio` y hace falta sin la ventana de la rocola (una hoja sola, en las pruebas): el
  pintado (`PantallaRocola.PonElPintado`), `Hoja.HayQueComponer` y la de delante (`Hoja.Activa`). Con la rocola, lo pone su ventana al
  abrirse (`PantallaRocola.Ventana.cs`, `AbreNativa`), como desde 8a.

### 3. Las ramas de la capa en Nativo/

- **`PantallaRocola.Vistas.cs`** (-158 +54): fuera la interfaz `ILaCapa` (28 miembros), el campo `LaCapa` y cada
  `ILaCapa c = LaCapa; if (c != null ...)` (29 lineas), `TraspasaHijas` y `PintaVistaDeLaCapa`. Con la rocola abierta `LaCapa` ya era
  `null` desde 8a, asi que el camino que corre es el mismo de antes. `PintadoSinLaCapa` se llama `PonElPintado`. `VentanaMinimizada(v)`
  da lo de la ventana de la rocola (`IVentanaDeLaRocola.Minimizada`, siempre false) y false para una vista suelta, como `FindForm() == null`.
- **`PantallaRocola.Rocola.cs`**: fuera `SueltaLaCapa`; `AlApagar = FinDeSesion` (antes miraba `LaCapa`).
- **`IVista.cs`**: `Pinta(SKCanvas)` ya no tiene cuerpo de fabrica (era «la pinta la capa»): todas las vistas de XP y `RotuloVista` ya lo
  implementaban (compila sin tocar ninguna).
- **`Hoja*.cs`**: los ganchos con nombre «de la capa» son ahora «la ventana de debajo» (la de la rocola): `ActivaDeLaCapa` ->
  `VentanaActiva`, `CedeLaCapa` -> `CedeLaVentana`, `QuitaFocoALaCapa` -> `QuitaFocoALaVentana`, `ActivaLaDeLaCapa` -> `ActivaLaVentana`,
  `VivaEnLaCapa` -> `VentanaViva`, `SitioEnLaCapa` -> `SitioDeLaVentana`, `ActivaLaDeArribaDeLaCapa` -> `ActivaLaVentanaDeDebajo`,
  `DesactivaPorLaCapa` -> `DesactivaPorLaVentana`, `DetrasDeLaCapa` -> `DetrasDeLaVentana`. Solo el nombre (y sus comentarios): siguen
  siendo ganchos y no llamadas directas a `PantallaRocola` porque `Pruebas/Ventanas/Hoja` compila `Hoja*.cs` sin la ventana de la rocola.
  Lo mismo `PantallaRocola.PintaUno`, `HaySucio`, `ProximoPintado`, `LaDeDelante`, `PonDelante`, `PonEncima` y `PonFoco` (los compila
  `Pruebas/Ventanas/Pantalla` sin las vistas).
- **`CompositorPantalla.cs`**: fuera `ConMano` (nadie lo pone: 8a ya lo dejaba en false al abrir la rocola); la mano es la de las piezas
  de una hoja (`Hoja.ManoBajoRaton`), como estaba.
- **`PantallaRocola.AgregaCapa(CapaDeTextura)`** (un aviso de las revisiones): Add de una capa de textura que ya esta en la ventana la
  manda al fondo, como `ControlCollection.Add` («`if (value._parent == Owner) { value.SendToBack(); return; }`», Control.ControlCollection.cs:67-71)
  y como ya hacian las vistas y el hueco del video de la capa. La rocola no llega ahi (cada video crea su capa, `VideoEnPantalla.Lugar`).

### 4. `TrazaVentanas.cs` y `VolcadoEstado.cs`

Se borraron las dos costuras `DeLaCapa` y sus llamadas (n7-adelanto las habia dejado en una funcion cada una). `foco=` y `activa=` quedan
como decia «La firma que hace falta»: `Hoja.Foco`/`Hoja.Activa` o, si no, `PantallaRocola.NombreDelFoco`/`NombreDeLaActiva` (el volcado
lo escribe `Form1`, que siempre tiene su ventana nativa: la rama `!nativa` era la de la capa). El texto de la traza y del volcado no
cambia en un caracter: lo dicen las escenas (abajo), que comparan la secuencia de la traza y cada volcado con los de la referencia.

### 5. Codigo/: solo notas (179 lineas de 34 archivos; ninguna linea de codigo)

Las notas «EN LINUX» y cabeceras que hablaban en presente de «la ventana de la capa», «su hueco (Plataforma/Anfitrion.cs)», los puentes
(`Plataforma/Puente*.cs`), `PuenteDibujo` o `Dibujo2D.cs` dicen ahora lo que hay (la pantalla y la lista de su ventana,
`Nativo/PantallaRocola.*.cs`) o, donde comparan con la capa, «la referencia». Hecho con sustituciones sobre los bytes de cada linea
(sin tocar finales de linea): los 109 archivos conservan su numero de lineas y de CRLF (`lineas.sh`: 109 archivos, 0 distintos frente a
`535be08`). `diff-xp.py` de los 34: las «sin marca» son las mismas 88 lineas de la base, identicas (`diff` vacio).

### 6. Pruebas/

- **`BancoDibujo`**: `Nuevo/Nuevo.csproj` ya no compila `Plataforma/Dibujo2D.cs` ni `EjecutaGdi.cs`; su ejecutor (y el de fabrica) es
  «nativo»; el «gdi» queda solo en `Congelado` (la capa de la referencia). Los tres `banco.sh` (VistasPared, VistasBuscadorPortadas,
  VistasReposo) piden solo «nativo». `mutantes.sh` no cambia (usa el `Dibujo2D.cs` de la referencia).
- **`Ventanas/Pruebas41/`** y **`guiones/pruebas41.sh`**: borrados (probaban `Form` y `Control` de la capa). En `compara_trazas.py` y
  `referencia.sh` no habia nada suyo que quitar (`grep` de «41» y «p41»: 0). `Ventanas/LEEME.md`, al dia.
- **`Ventanas/PantallaVistas`** (de 900 a 573 lineas): fuera la mitad de la capa (el «gemelo», un `Control` de la capa en lugar de la vista;
  la vista en su hueco; `Form`, `Label`, `Panel`, `Escritorio`). Quedan las reglas ya fijadas, contra los MISMOS valores (`esperado...`
  no cambio), en la lista de la ventana: **78 comprobaciones** de las 194 (75 que ya se hacian «sin la capa», mas tres nuevas: `setbounds`
  y `quitada`, que solo se miraban con la capa, y el Add repetido de una capa de textura). Lo que se pierde: la comparacion vista-contra-control
  (ya no hay control con que comparar) y `desecha-dueno` (que desechar al dueno desecha a la vista: sin capa hace falta la ventana de la
  rocola, que esta prueba no abre; lo cubren las escenas de cierre). `esconde-la-del-foco` queda con el valor de WinForms («S»): con la
  capa se aceptaba «X» porque la capa no seguia a WinForms (ver avisos).
- **Controles negativos**: 16 casos de 8 guiones (`control.sh` de VistasAsistente, VistasBuscador, VistasBuscadorPortadas,
  VistasPantallaViva, VistasPared, VistasVideoForm1 y VistasVistasA, y `VistasVistasB/mutantes.sh`) metian su fallo en un `Puente*.cs` o
  en una linea con `PuenteDibujo`/`Rocola.Dibujo2D`: retirados, con una nota en cada guion. No se corrieron los guiones enteros (cada caso
  es una compilacion): los que mutan una linea de `Codigo/` que haya cambiado desde su tanda lo dicen solos («el sed no cambio nada», «NO SE
  PUDO METER»).
- **`Inventario`**: no compilaba el programa de hoy (no incluia `Nativo/`); ahora si: **179 archivos (109 de Codigo), 0 errores de
  compilacion; winforms=0, gdi=0** (gdiprim=5 071 son `Color`, `Point`, `Rectangle`... de System.Drawing.Primitives, que no imitan nada;
  d3d9=14, wmp=0, otras=8, info=6 647).
- `Pruebas/Comun/PruCapa.cs`, `BancoDibujo/EjecutaGdi.cs` y `Congelado/`, `TrazaDibujo/` y lo que `Tarjeta/Reproductor.cs` busca por nombre
  (`Rocola.Dibujo2D.Bitmap`) se quedan: solo compilan o corren contra la referencia.

### 7. Comentarios de Nativo/, Plataforma/ y `RocolaXp.csproj`

Las cabeceras que decian «TRANSICION», «mientras dure la transicion», «se va con la capa», «Dibujo2D los llama» o citaban
`Plataforma/Anfitrion.cs` estan al dia. Donde un comentario dice de donde se PORTO una regla («portado de la capa», «Escritorio.PonFoco
de la capa»), se dejo, apuntando a la referencia: es la procedencia, y esa capa sigue en la copia congelada. `RocolaXp.csproj` ya no
cita `Direct3DaGL.cs` y dice que son `Codigo/`, `Nativo/` y `Plataforma/`. (`LEEME.md`, `PORT_JUNTAS.md`, el plan y `AGENTS.md` son de 9b
y del coordinador: no se tocaron.)

## Costuras

Quitadas (todas las que quedaban):

- `PantallaRocola.ILaCapa`, `PantallaRocola.LaCapa` y sus 29 ramas; `PantallaRocola.SueltaLaCapa`; `PantallaRocola.PintaVistaDeLaCapa`;
  `Anfitrion.VentanasDeLaCapa`, `HuecoDeVista`, `HuecoDelVideo`, `VistaDeHueco`; `IAnfitrion`.
- `TrazaVentanas.DeLaCapa` y `Form1.DeLaCapa` (VolcadoEstado.cs).
- `PuenteDibujo` entero (Puente.cs) y los seis `Puente.<grupo>.cs`; `Starfield.Draw`/`DrawReactive`/`ColoresDePortada` con `Graphics` o
  `Bitmap` (Puente.fondo.cs), `ExtractCoverPalette(Bitmap)` y `FormatoMapaPared` (Puente.pared.cs).
- `CompositorPantalla.ConMano`.
- El `global using Rocola.Ventanas;` (Traduccion.cs).

Nuevas: ninguna. Lo que queda con forma de gancho (delegados que pone quien abre la pantalla) NO es una costura de transicion: es para
que las pruebas de la hoja y de la pantalla compilen esos archivos solos (`Hoja.VentanaActiva`, `CedeLaVentana`, `QuitaFocoALaVentana`,
`ActivaLaVentana`, `VentanaViva`, `SitioDeLaVentana`, `HayQueComponer`, `ActivaLaVentanaDeDebajo`; `PantallaRocola.PintaUno`, `HaySucio`,
`ProximoPintado`, `LaDeDelante`, `PonDelante`, `PonEncima`, `PonFoco`, `Componer`).

Un nombre que se queda a proposito: las capturas por capa llaman «dibujo2d» al tramo por procesador de la ventana de la rocola
(`PantallaRocola.Vistas.cs`, `tipo`), para que `Pruebas/Recorridos/compara.py` lo empareje con el de la referencia. Es una etiqueta de
prueba, no una clase.

## Los avisos de las revisiones anteriores que quedaban para N7

1. **El panel del disco pinta bajo sus hijas (WS_CLIPCHILDREN no se reproduce).** No se cambio: la pantalla pinta el panel y despues sus
   hijas encima (`PintaHijas`), que da lo mismo que WS_CLIPCHILDREN mientras el `Pinta` del dueno no dependa del recorte; `BgPanel` no
   depende (pinta el cielo entero, opaco). Hacerlo bien pide recortar el lienzo del dueno por la forma de cada hija visible antes de su
   `Pinta` (y repintarlo cuando una hija se mueve): no es simple y no cambia ningun pixel de la rocola. Queda como riesgo.
2. **`AgregaCapa(t)` repetido no mandaba al fondo como `Controls.Add`.** Hecho (arriba, 3) y comprobado en PantallaVistas.
3. **Con `gpu=0`, al cerrar el buscador el foco va a `HelpBar` y no a `Form1'Rocola'` como en la referencia.** Es correcto y se queda:
   en WinForms `Form.IsContainerControl` es false (Control.cs:2900 y 5825-5840) y `SelectNextIfFocused` elige la siguiente vista; la capa
   de la referencia se quedaba en la escondida porque su `ContenedorDelFoco` saltaba la ventana. La regla del dueno (8-11-2026) es seguir
   la semantica de WinForms, no la de la capa. La traza de esa escena (ninguna de las 24 corre con `gpu=0`) dice `foco HelpBar` donde la
   referencia dice `foco Form1'Rocola'`. Queda documentado tambien en la cabecera de `PantallaRocola.Foco.cs`.

## Comprobaciones

Todo con el arbol final (`cea6eaf`), en la maquina de pruebas (llvmpipe, fuera de pantalla), contra la referencia congelada. La
primera pasada corrio con la verificacion del coordinador a la vez (maquina cargada); lo que hubo que repetir se dice.

| Que | Resultado |
|---|---|
| `dotnet build -c Release --no-incremental` | 0 errores, **13 avisos**: los 15 de la base menos los dos CS1690 de `Ventanas.Control.cs(104)`, que se fue. Quedan 9 SYSLIB0054 (`Thread.VolatileRead`, lineas de XP), 1 SYSLIB0050 (`FormatterServices`, Mp3Ligero.cs, de XP), 2 CA2022 (lecturas de XP: Form1.cs:375, Mp3Ligero.cs:199) y 1 CA2014 (PruebasLinux.cs:98). Ninguno nuevo; no se quitan porque son lineas de XP (quitarlos seria cambiar su logica) o de una prueba |
| NativeAOT (`dotnet publish -p:PublishAot=true`) | salida 0, **0 avisos IL/AOT** (las 13 lineas con «warning» son las mismas de arriba) |
| Los 109 archivos de `Codigo/` | 109 de 109 con su numero de lineas, de CRLF y su ultimo byte iguales que en `535be08` |
| `diff-xp.py` de los 34 archivos de `Codigo/` tocados | la salida es identica a la de la base (`diff` vacio): las 88 «sin marca» de siempre, ninguna nueva |
| `grep` de `Rocola.Dibujo2D`, `Rocola.Ventanas`, `PuenteDibujo` en `Codigo/`, `Nativo/`, `Plataforma/` y el `.csproj` | **0** (y 0 de `LaCapa`, `ILaCapa`, `global using`). En `Pruebas/` quedan: `Comun/PruCapa.cs`, `BancoDibujo/EjecutaGdi.cs`, `TrazaDibujo/TrazaDibujo.cs`, `Tarjeta/Reproductor.cs` (por nombre, por reflexion) y `Ventanas/ganchos-referencia.patch`, que solo compilan o corren contra la referencia; `Inventario/Program.cs` (los nombres que cuenta) y comentarios en `PruNativo.cs`, `VistasSuena/Suena.cs` y los `control.sh`/`mutantes.sh` |
| `Pruebas/Inventario` | compila el programa (179 archivos, 0 errores): **winforms=0, gdi=0** |
| Banco del dibujo (referencia «gdi» congelada contra «nativo») | semillas 7, 11 y 5: 3 000 casos cada una, **0 distintos** |
| Las 29 cintas de la tarjeta | **29 iguales** (6 616 cuadros), 0 distintas |
| Arneses con las dos letras (falsas y DejaVu) | VistasAsistente, VistasAtraccion, VistasBuscador, VistasBuscadorPortadas, VistasPantallaViva, VistasPared, VistasReposo, VistasSuena, VistasVideoForm1, VistasVistasA: **IGUALES** las dos; VistasPortadas IGUALES; VistasFondo solo la linea de marca (`traducida=True`/`False`); VistasVistasB 0 de 3 108; VistasAcabadoEstilo 0 de 6 603; VistasAjustes 0 de 545; VistasFrontera 0 de 106 lineas y 153 de 153 texturas (xp y dejavu) |
| `pruebas_hoja.sh` | pasan **86**, fallan 0 |
| `AOT=1 pruebas_pantalla.sh` | pasan **32**, fallan 0, en JIT y en NativeAOT |
| `pruebas_carpeta.sh` | 39 pasan, 0 fallan |
| PantallaVistas | pasan **78**, fallan 0 (lo que queda de las 194: ver arriba) |
| Las 24 escenas alternadas con la referencia (Rbcf/bcf, con el ruido de ventanas2 y de ventanas) | **20 IGUALES** a la primera: arranque, moneda, video, buscador, cierreq, altf4, kill9, asistente, salirq, f2, f2barras, f2sin, aviso, f3vistas, f3grises, f3teclas, f3raton, f3billar, f12, f12vigia. `captura`, solo las cuentas del reloj Form1.cs:724 y del encargo MedidasBitacora.cs:129 (337/325 y 437/418): repetida (Rbcg/bcg) **IGUAL**. `sigterm` y `f3sigterm`: lo aceptado (el programa nuevo traza el cierre WindowsShutDown y borra el estado; la referencia no). `tab`: la traza IGUAL; distintos solo los rotulos de Ctrl+F3 que cuentan lo que el repaso de la biblioteca lleva medido («33 %, van 12, ya esta al dia» frente a «0 %, faltan 2/4 archivos») y sus 3 853/5 082 puntos de esa hoja, las dos veces: lo aceptado en PORT_JUNTAS.md (las prioridades de hilo de XP puestas de verdad) |
| La regla del apagado | `rocola_estado.txt` tras `sigterm` y `f3sigterm`: NO esta en el programa nuevo (se borra, como XP al apagar; en la referencia si esta: lo aceptado); tras `cierreq` (Ctrl+Alt+Q), `altf4` y `salirq`: no esta en ninguno de los dos; tras `kill9`: **esta** en los dos, tambien en NativeAOT |
| El ciclo del tabulador (16 Tab) | el foco pasa por HelpBar, LetterBar, AlbumView, ProgressView, GpuPanel tres vueltas y un paso mas, igual que la referencia (17 de 17); `foco=` y `activa=` del volcado iguales; la secuencia solo difiere en el cierre por SIGTERM del final (3 lineas: closing/closed WindowsShutDown y «foco nadie»), lo aceptado |
| NativeAOT, escenas contra la referencia | arranque, cierreq, kill9, buscador, f3vistas y f12 **IGUALES**; f3teclas: la primera vez (recien reiniciada la maquina, la referencia en frio no llego a tiempo a sus teclas: su traza se corta en el suceso 94 y su reloj da 0 ticks) DIFERENTE; repetida (Rbcb/bcb) **IGUAL** |
| Los 9 recorridos (con el candado) | 9 de 9 salen con 0. **Capas quietas 213 de 217**: las de siempre (las tres de `ctrlf3` a los 46 s con F12 abierta y la capa 4 de `hojas` a los 18 s, la del repaso). **Texturas quietas 473 de 487** la primera vez (faltan: atraccion 1, ayuda 2, buscador 2, pared2 3, video 6) y, repetidos video, ayuda, pared2, atraccion y buscador con la maquina libre, **478 de 487** (video 50 de 50, atraccion 32 de 32, buscador 76 de 78, ayuda 63 de 66, pared2 94 de 98): las que faltan cambian de una corrida a otra (las de la hora y el ritmo: rotulos de 1024x128, 512x256...), como en las juntas anteriores (477-478) |

No corrido: los `control.sh`/`mutantes.sh` enteros (ver «Lo que queda»); nada en el A4.

## Lo que queda

- De este grupo, nada de lo pedido. Lo de 9b (docs-y-notas: `LEEME.md`, `PORT_JUNTAS.md`, el plan, `AGENTS.md`) es de otro grupo y del
  coordinador.
- Los `control.sh`/`mutantes.sh` de los arneses no se corrieron enteros: conviene pasarlos una vez con la maquina libre y retirar los
  casos que digan que su linea ya no esta.
- La ventana nativa de la rocola no tiene bateria propia (lo decia PORT_form1-sin-form.md): las 41 pruebas se fueron con la capa. Sus
  reglas las comprueban las 24 escenas contra la referencia, `Pruebas/Ventanas/Hoja` (86) y PantallaVistas (78).

## Riesgos

- WS_CLIPCHILDREN (aviso 1).
- La referencia congelada es ahora lo UNICO que conserva la capa: si se pierde `/root/rocola-trabajo/referencia-xp` (esta fuera del
  repositorio; es `aef2211`, recuperable con `git worktree add ... aef2211`) no hay contra que comparar el banco, los arneses, las cintas
  ni las escenas.
- `Anfitrion.Capas` es codigo nuevo en el camino de cada cuadro (las hojas ya no pasan por `Escritorio.Capas`): comprobado por las escenas
  con hojas (f2*, aviso, f3*, f12*: capas iguales pixel a pixel) y los recorridos `ctrlf3` y `hojas`, no por una prueba propia.
- Los ganchos de `Hoja` cambiaron de nombre: una rama abierta antes de esta junta que los nombre (`Hoja.ActivaDeLaCapa`...) no compilara.
- Nada de esto se ha visto en el A4.

## Revision

Revision independiente del diff `535be08..eddeda5` (14-11-2026), leyendo el codigo contra la base y repitiendo solo lo que la lectura ponia
en duda. No se encontro ningun fallo que arreglar; no cambia ninguna linea de codigo.

Lo comprobado:

- **Codigo/**: los 109 archivos con las mismas lineas, los mismos CRLF y el mismo ultimo byte que en 535be08. De las 179 lineas que cambian
  en 34 archivos, en las 179 la parte de codigo (lo de antes del primer `//`) es identica byte a byte: solo cambian notas.
  `diff-xp.py` de los 34 da la misma salida, linea por linea, que en la copia principal.
- **Lo que debia desaparecer**: 0 usos (fuera de comentarios) de `Rocola.Dibujo2D`, `Rocola.Ventanas`, `PuenteDibujo`, `LaCapa`, `ILaCapa`,
  `SueltaLaCapa`, `HuecoDe*`, `Escritorio.` y `global using` en Codigo/, Nativo/, Plataforma/ y el proyecto. Ningun `.csproj` ni guion de
  Pruebas/ del lado nuevo compila un archivo borrado (los que los nombran van contra la referencia o son comentarios). Los 81 guiones `.sh`
  de Pruebas/ pasan `bash -n` (el comentario metido dentro de la lista de `VistasBuscador/control.sh` es valido).
- **Compilacion desde cero** (`--no-incremental`): 0 errores, 13 avisos (los 15 de la base menos los dos CS1690 de `Ventanas.Control.cs`).
- **`Anfitrion.Capas`** contra `Escritorio.Capas` de 535be08, linea por linea: las mismas hojas (no desechadas y visibles), la superficie
  pedida una vez por cuadro y antes que la ventana, el mismo sitio y opacidad, la tarjeta de la hoja justo despues de la suya, las de detras
  al principio y las demas despues de la ventana; las claves duran lo mismo (se olvidan al esconderse o desecharse la hoja).
- **Las ramas de `LaCapa`**: en 535be08 `LaCapa` ya era `null` desde que se abria la ventana de la rocola, asi que con la rocola abierta
  cada funcion queda con la rama que ya corria. Lo unico que cambia de camino es lo de ANTES de abrir la ventana (el aviso de la carpeta de
  datos de `Program.cs:98`, que en 535be08 aun repartia la capa: `Escritorio.Tecla`, `RatonEn`, `PintaUno`): ahora lo reparte la pantalla,
  con los ganchos de la ventana de debajo sin poner (`Hoja` los mira antes de llamarlos). Por eso se repitio la escena **aviso** alternada
  con la referencia: IGUAL. Y **f12** (la tarjeta en el cliente de una hoja, la hoja detras de la ventana: el otro camino propio de
  `Anfitrion.Capas`): IGUAL.
- `VentanaMinimizada` pasa de «false sin la capa» a preguntar a la ventana, que contesta siempre false (`VentanaNativa.Minimizada`): lo mismo.
- `TrazaVentanas.Pieza` y el volcado: sin la capa quedan las mismas ramas que ya corrian con la ventana nativa; el texto no cambia.
- **PantallaVistas**: los valores esperados de las escenas que quedan (rotulo, video, panel, tabulador, tabulador con la tarjeta) son los de
  535be08 sin tocar, y las reglas de la escena de la vista junto al control (orden, raton, captura, doble clic, BringToFront, transparente,
  escondida, setbounds, quitada, quien escucha, foco) y de la tarjeta siguen como comprobaciones de la lista contra los mismos valores.

Apuntes menores (sin cambio):

- La marca `// NAudio 3 (Puente.cs)` de las lineas de sonido de Codigo/ nombra un archivo que no es el del puente de NAudio (esta en
  `Plataforma/NAudioXp.cs`; `Plataforma/Puente.cs` era el del dibujo y se borro). Es la marca fija de AGENTS.md y de `diff-xp.py`: no se toca.
- En `Pruebas/Ventanas/Hoja/Program.cs` tres titulos de prueba siguen diciendo «una ventana de la capa» (es la ventana de la rocola).

No repetido aqui (lo da el informe y la lectura no lo pone en duda): NativeAOT, banco, cintas, arneses, recorridos y el resto de las escenas;
los corre el coordinador al juntar.
