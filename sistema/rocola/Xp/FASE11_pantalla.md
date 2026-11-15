# Fase 11, grupo pantalla-ctrlf4: la ventana de Rocola OS (Ctrl+F4) con la hoja «Pantalla»

15-11-2026. Rama `pantalla-ctrlf4`, sobre `junta-spotify` (a744bf4). Es la petición del dueño del 15-11-2026 (AGENTS.md: «en la ventana
de ctrl f3 (o ... ctrl f4) pon para configurar elementos mas de GPU, como la resolucion, los hertz del monitor, y los colores del
monitor, todas esas cosas quedandose guardadas para siempre») y el punto 9 de `SPOTIFY_JUNTA_PENDIENTES.md`. **No entra en la
compilación del cumpleaños: se junta después** (un modo de pantalla mal puesto deja la rocola a oscuras). **Nada de esto está probado en
el A4 ni con un monitor de verdad**: todo fuera de pantalla con un KMS simulado (llvmpipe), en la máquina de pruebas compartida.

## Qué se ve

- **Ctrl+F4** (solo con `ROCOLA_OS`) abre la ventana de Rocola OS: una hoja de XP de 960x732 como Ctrl+F3 (cabe en 1024x768), el mismo
  fondo, las mismas letras, el título grande a la izquierda, las pestañas a la derecha en su renglón (terminando donde las de Ctrl+F3),
  las tarjetas, deslizadores y botones planos de XP (las mismas clases: `Tarjeta`, `Deslizador`, `BotonPlano`), «Listo» verde abajo a la
  derecha, Escape cierra, la moneda cuenta con la ventana abierta (`MonedaTambienAqui`), y con ella abierta `maintMode` (como las modales
  de XP: la rocola no está «libre»). El foco, el tabulador y el ratón son los de la hoja nativa (`Nativo/Hoja*.cs`).
- **Una pestaña, «Pantalla»**, con dos tarjetas:
  - **RESOLUCIÓN Y HERCIOS**: lo de ahora (y si es lo automático o lo guardado), una barra con las resoluciones del monitor (de 1024x768
    para arriba: las ventanas de la rocola miden 966x764; y siempre la que está puesta) y otra con los hercios de esa resolución (el
    automático lleva «*»), «Probar ... (15 s)», «Volver a lo automático», lo que pasó (en ámbar o verde) y el controlador.
  - **COLORES DEL MONITOR**: brillo (−50..+50), contraste (50..150 %), gamma (0,50..2,00), temperatura de color (cálida −50..fría +50) y
    saturación (0..200 %), con «Restablecer los colores (de fábrica)». Se ve al instante. La saturación solo si la tarjeta tiene matriz
    de color (CTM); si no, en gris diciendo por qué (regla del 16-10-2026): «Necesita una tarjeta con matriz de color (CTM); la de esta
    rocola (radeon) no la tiene». Sin tabla de gamma (o sin la función), los cuatro en gris con su porqué.
- **«¿Se ve bien?»** al probar un modo: una hoja chica encima, «No (vuelve en N s)» con el foco (Intro, Escape o REGRESAR: vuelve) y
  «Sí, se ve bien»; cuenta atrás de 15 s y, sin respuesta, vuelve sola al modo de antes. Solo lo confirmado se guarda.
- Al cambiar de modo, la rocola se acomoda a la medida nueva como lo hace XP con otra resolución (la ventana maximizada toma la pantalla:
  OnResize → LayoutControls) y las hojas abiertas se centran otra vez. Capturas: `pantalla-capturas/` del scratchpad de la sesión.

Las capturas de las pruebas (en el scratchpad de la sesión, `pantalla-capturas/`): `ventana-1024.png` (la ventana), `prueba-15s.png` (la
rocola ya en 1920x1080 con «¿Se ve bien?»), `vuelta-sola.png` (de vuelta en 1024x768, «Nadie confirmó...»), `guardado-1920.png`,
`colores.png`, `cerrada.png`, `arranque-1920.png` (arranca ya en lo guardado), `no-se-pudo.png`, `lista.png` y `apagada.png` (la hojita en
gris), `sin-rocola-os.png`.

## Cómo está hecho

| Archivo | Qué lleva |
|---|---|
| `RocolaOs/RocolaOs.Ventana.cs` | La ventana de Ctrl+F4 (g80), las hojitas (`RocolaOsHojitas.Registra`: cada grupo que venga añade la suya con un nombre, un orden y el método que arma su página, un `Grupo` de 960x580 en y=96; y si quiere, lo de cada medio segundo y al cerrar) |
| `RocolaOs/RocolaOs.VentanaPantalla.cs` | La hojita «Pantalla» y «¿Se ve bien?» |
| `RocolaOs/RocolaOs.Pantalla.cs` | La función «pantalla» (de fábrica encendida con `ROCOLA_OS=1`; `pantalla=0` en `rocola_os.txt` la apaga), `rocola_pantalla.txt`, abrir la pantalla con lo guardado (g81), cambiar de modo, la tabla de gamma y la CTM, la cuenta de arranques, la orden `--prueba-pantalla` |
| `RocolaOs/RocolaOs.PantallaRocola.cs` | La parte de `PantallaRocola` que acomoda la rocola a otra medida (`RocolaOsCambioDeMedida`) |
| `RocolaOs/RocolaOs.PantallaSimulada.cs` | El KMS simulado de las pruebas (`ROCOLA_KMS_SIMULADO`, `ROCOLA_KMS_SIMULADO_TRAZA`) |
| `Consola/Pantalla/Kms.cs` (enlazado) | Los modos (`ModoPantalla`, `IModosPantalla`), lo automático sacado a `ModoAutomatico` sin cambiarlo, `EligeModo`, `CambiaModo`, `PonGamma`, `PonCtm`, `QuitaColores`, el nombre del controlador. Sin `EligeModo` ni llamadas nuevas, la pantalla es la de siempre (un `drmModeSetCrtc` que falla ahora se apunta en `UltimoFallo`; antes no se miraba) |
| `board/rocola/rootfs_overlay/usr/bin/rocola-pantalla` | La orden de la consola de mantenimiento |
| `Pruebas/RocolaOs/pantalla/prueba.sh` | La prueba |

### Los ganchos (g80-g82)

| Id | Dónde | Qué | Sin `ROCOLA_OS` |
|---|---|---|---|
| g80 | `Codigo/Form1.cs:2077` (ProcessCmdKey, justo delante de Ctrl+F2) | `if (RocolaOsCtrlF4(keyData)) return true;` delante | `false`: Ctrl+F4 sigue su camino de XP (que no la usa). Al ir en ese sitio, Ctrl+F4 se abre **con las mismas reglas que Ctrl+F2 y Ctrl+F3**: no mientras se leen las tablas del arranque, en la atracción la tecla solo despierta, ni con el asistente de botones, el video a pantalla completa o el asistente estrella que se quedan la tecla |
| g81 | `Nativo/Anfitrion.cs:30` | `new Kms()` → `RocolaOs.AbreLaPantalla()` | `new Kms()` (sin la función tampoco se lee nada). Solo se llega con la pantalla de verdad (con `ROCOLA_PANTALLA=fuera...` no) |
| g82 | `Nativo/CompositorPantalla.cs:46` | `ancho`/`alto` de solo lectura → la medida de la superficie en cada cuadro | la misma medida (nunca cambia) |

`ganchos.py`: TODO BIEN (29 ganchos). `diff-xp.py --sin-ganchos`: 5 095 / 4 828 / 229, las de la base.

### Lo guardado: `/rocola/datos/xp/rocola_pantalla.txt`

`clave=valor`, UTF-8 sin marca y LF, escrito de un tirón con `EscribeSeguro` de XP (el de paso, el `.bak`, el cambio de nombre; con la
rocola, por su `GuardaDetras`). Solo lleva lo que no es de fábrica (y siempre `modo=`); lo que no conoce se queda en su sitio.
`modo=1920x1080@60` (o `auto`; `i` al final si es entrelazado), `brillo=`, `contraste=`, `gamma=` (100 = 1,00), `temperatura=`,
`saturacion=`, `sin_confirmar=N`, `modo_malo=...`. Los colores se guardan cuando se dejan quietos medio segundo y al cerrar la ventana (como
lo de Ctrl+F3); el modo, solo al confirmarlo.

### Al arrancar, antes de pintar

`AbreLaPantalla` (g81, desde el `[ModuleInitializer]` de `Plataforma/PruebasLinux.cs`, antes del `Main` de XP: aún no hay registro de
funciones, así que mira lo mismo que decidirá `Encendida`: la variable, la lista y `pantalla=` de `rocola_os.txt`):
1. Con `sin_confirmar` ≥ 2 y un modo guardado: lo automático, `modo=auto` y `modo_malo=<modo> (2 arranques seguidos sin llegar a 60 s en
   pie)`.
2. Si el monitor da el modo guardado (y no es el automático): **se cuenta el arranque (`sin_confirmar+1`, escrito ya)** y `Kms` lo pone
   en el constructor con un cuadro negro (`drmModeSetCrtc`) antes de que la rocola pinte nada. Si el controlador no lo acepta, lo
   automático al momento (`modo_malo=<modo>: el controlador no lo acepta...`).
3. Si el monitor no lo da (otro monitor): lo automático, sin contar; lo guardado se queda por si vuelve.
4. Los colores guardados se ponen en la tarjeta.
5. A los **60 s en pie** (el latido de la función), `sin_confirmar` vuelve a 0. Así, un modo con el que el programa se cae dos veces
   seguidas antes del minuto se abandona solo al tercer arranque (el lanzador lo vuelve a abrir con sus esperas: 2 s, 10 s...).
6. Al cerrarse la rocola (`AlCerrar`, en el hilo de la pantalla), los colores de fábrica para la consola (nadie desecha la pantalla al
   salir; el núcleo devuelve el modo de la consola pero no la tabla de gamma). Un apagón o una caída no lo corren: el siguiente arranque
   los vuelve a poner.

Lo automático es lo de siempre, sin cambiar (`Kms.ModoAutomatico`, sacado del constructor tal cual): el preferido del monitor, a esa
resolución el de más hercios y, con V30 apagado (lo de hoy), 1024x768 con el de más hercios si el monitor lo tiene.

### Los colores: la tabla de gamma y la CTM

Entrada i de n (x = i/(n−1)): `y = x^(1/gamma)`, luego `(y−0,5)·contraste+0,5`, luego `+brillo/200`, y por canal la temperatura
(cálida: azul ×(1−0,40·t) y verde ×(1−0,10·t); fría: rojo ×(1−0,25·t) y verde ×(1−0,10·t); multiplica, nunca pasa del blanco); al borde lo
que se sale; 16 bits. Es aproximada, como el menú de un monitor. Con todo de fábrica no se pone tabla: `QuitaColores` (sin `GAMMA_LUT`, o
con la llamada de siempre la tabla con que arranca el núcleo, `i<<8` con 256: `drm_mode_crtc_set_gamma_size`). La saturación es una matriz
3x3 con los pesos de Rec. 709 (cada fila suma 1), en S31.32 con signo y magnitud (`drm_color_ctm`).
**Sin coste por cuadro**: la tabla la aplica la salida de la tarjeta; solo cuesta al moverla (como mucho una cada 50 ms).

**La vía**: si el CRTC tiene la propiedad `GAMMA_LUT` (con `GAMMA_LUT_SIZE`), por ella (un blob de `drm_color_lut` y
`drmModeObjectSetProperty`, que en un controlador atómico va por el camino atómico); si no, la llamada de siempre `drmModeCrtcSetGamma`
con el `gamma_size` del CRTC. La CTM, solo si el CRTC tiene la propiedad `CTM`.

**El controlador del A4 (radeon, núcleo 6.12.47, mirado en el código de `salida-qemu/build/linux-6.12.47/drivers/gpu/drm/radeon`)**: el
A4-6300 (Richland, gráfica HD 8370D, familia ARUBA: DCE 6.1) va con `radeon`, que **no es atómico**: no tiene `GAMMA_LUT` ni `CTM`. Tiene la
gamma de siempre: `drm_mode_crtc_set_gamma_size(..., 256)` (`radeon_display.c:698`), `.gamma_set = radeon_crtc_gamma_set` (que carga la
tabla: `radeon_crtc_load_lut`), y para ARUBA (`ASIC_IS_DCE5`) `dce5_crtc_load_lut`, que escribe las 256 entradas a 10 bits por canal
(`& 0xffc0`) y antes espera 10 ms (`msleep(10)`). La tabla se vuelve a cargar al encender el CRTC (`atombios_crtc.c:282`), así que
sobrevive a un cambio de modo (igual se vuelve a poner después de cada cambio). `DRM_IOCTL_MODE_SETGAMMA` y `SETCRTC` piden ser el amo
(`DRM_MASTER`), que rocola-xp es. **En el A4, pues: brillo, contraste, gamma y temperatura sí; la saturación, en gris.** (En QEMU, bochs
tampoco tiene gamma: los cuatro en gris.)

## La orden `rocola-pantalla` (consola de mantenimiento)

`rocola-pantalla auto` borra `rocola_pantalla.txt`, su `.bak` y su `.tmp` (si no, la rocola leería el `.bak`); si rocola-xp está abierta,
la cierra conservando el estado (SIGUSR1 con la marca `/run/rocola-xp-reinicio`, como `rocola-sonido`), la vuelve a borrar y el lanzador la
abre ya en lo automático; si no, vale al volver a ella (Ctrl+Alt+F1). `rocola-pantalla estado` lo dice en palabras (y lo de ahora, del
renglón `pantalla=` de `/run/rocola-vivo.txt`, cuando el grupo 1a escriba ese archivo). `rocola-pantalla modos`: las salidas conectadas y
sus modos de `/sys/class/drm/*/modes` (sin repetir; los hercios se ven en Ctrl+F4). BusyBox ash, sin `stat`.

## Las pruebas

15-11-2026, en la máquina de pruebas compartida (carga 5 a 11: otros agentes compilando). JIT = `dotnet build -c Release`; AOT =
`dotnet publish -p:PublishAot=true -r linux-x64`. **Todo fuera de pantalla con el KMS simulado; nada en el A4 ni con un monitor de verdad.**

| Prueba | Resultado |
|---|---|
| Compilar, JIT | 0 errores, los 13 avisos de siempre |
| Publicar NativeAOT | sale con 0, 0 avisos IL/AOT |
| `ganchos.py` | TODO BIEN (29 ganchos; `Codigo/` = `fc7b667` devolviéndolos) |
| `diff-xp.py --sin-ganchos` (109 archivos) | 5 095 / 4 828 / 229, las de la base; 25 ganchos de `Codigo/` devueltos en 10 archivos |
| `Pruebas/RocolaOs/pantalla/prueba.sh` | **JIT 65 de 65; AOT 65 de 65** (abajo, por partes) |
| nivel 0 (sin `ROCOLA_OS`, contra la referencia congelada) | NIVEL0_PENDIENTE |

`prueba.sh`, por partes (las mismas en JIT y en AOT):
- **numeros** (12): la tabla de fábrica es la recta exacta (i·257 con 256); la del núcleo para «Restablecer», `i<<8`; brillo +25 y
  temperatura −25 (r[0]=8192, en el medio b<g<r: 32 870 < 39 033 < 41 088, creciente y dentro de 0..65 535); gamma 2,00 y contraste 150 %
  contra la fórmula entrada por entrada; la CTM (identidad 0x0000000100000000 en la diagonal; 0 % = los pesos de Rec. 709; cada fila suma
  1); `rocola_pantalla.txt` leído y escrito (acota, lo que no vale es lo de fábrica, guarda lo desconocido).
- **guion** (10): `rocola-pantalla` con el BusyBox del sistema (`salida-qemu/target`, en chroot): `sh -n`, sin `stat`, `modos` (4 sin
  repetir de 6 renglones; sin monitor lo dice), `estado` (vacío, todo lo guardado y lo de ahora, del `.bak` si falta el bueno), `auto`
  (borra el archivo, su `.bak` y su `.tmp`; la segunda vez no hay nada), el uso.
- **ventana** (16): Ctrl+F4 en 1024x768@75 (lo automático); con el ratón, la resolución más grande y «Probar»: la rocola entera pasa a
  1920x1080 (capturas de 1920x1080) con «¿Se ve bien?»; **sin contestar vuelve sola a los 15,0 s (JIT) y 15,1 s (AOT)** y no se guardó
  nada; una moneda con la pregunta abierta cuenta; otra vez y «Sí»: `modo=1920x1080@60` guardado; brillo y temperatura con el ratón: la
  última tabla que llega a la «tarjeta» es exactamente la de brillo 25 y temperatura −25 (las 768 cifras) y se guarda; sin CTM no se pide
  la matriz; Escape cierra; al cerrarse la rocola, los colores de fábrica; la bitácora; el apagado de siempre; `rocola_error.txt` limpio.
- **arranque** (7): con `modo=1920x1080@60` y colores guardados, **el primer modo puesto es el guardado** (ni un cuadro en 1024x768) y la
  tabla va justo después; `sin_confirmar=1` a los 10 s; **a los 60 s se confirma** (desaparece) y lo desconocido del archivo sigue.
- **caidas** (5): dos arranques matados con `kill -9` a los 8 s (`sin_confirmar` 1 y 2, en 1920x1080) y el tercero **arranca ya en lo
  automático**, con `modo=auto` y `modo_malo=... (2 arranques seguidos sin llegar a 60 s en pie)`.
- **malo** (4): el controlador no acepta el modo guardado → lo automático al abrir y `modo_malo` lo dice; probar uno que no acepta → ni
  cambia ni pregunta.
- **monitor** (2): un modo guardado que el monitor no da → lo automático, sin contar el arranque, lo guardado se queda.
- **lista** y **apagada** (6): con `ROCOLA_OS=vida,red` o `pantalla=0`, ni el KMS simulado ni nada de la pantalla; Ctrl+F4 abre la
  ventana con la hojita en gris diciendo por qué; `rocola_pantalla.txt` no se toca.
- **sin** (5): sin `ROCOLA_OS` (con el KMS simulado puesto y un `rocola_pantalla.txt` de 1920x1080): nada de Rocola OS, nada leído, la
  pantalla de siempre, Ctrl+F4 no abre nada; y **la misma escena contra la referencia congelada (Ctrl+F4, moneda y Ctrl+Alt+Q x3, como
  «cierreq»): IGUAL** con `compara_escena.py` y el ruido medido, en JIT y en AOT. (Con SIGTERM la referencia en JIT y este en AOT no
  cierran igual en el mismo segundo: por eso no se usó; el nivel 0 tampoco corre «sigterm» en AOT.)

**No se hizo la prueba en QEMU** (la de la junta de Spotify estaba corriendo: un `qemu-system` en marcha todo el tiempo). Ni en el A4.

## Lo que queda abierto y lo que hay que comprobar en el A4

- **Nada probado en el A4 ni con un monitor de verdad.** En el A4: que la lista de modos sea la del monitor del bar; que un cambio de modo
  (y la vuelta sola a los 15 s) se vea; que la radeon acepte la tabla de gamma (y cuánto tarda cada una: los 10 ms de `msleep`); que la
  consola de mantenimiento quede con los colores de fábrica; que con un modo guardado el arranque no parpadee de más; y medir que la
  rocola en una resolución mayor que 1024x768 vaya fluida (el A4 está medido en 1024x768; regla del 17-10-2026: no afirmar fluidez).
- **El cambio de modo en vivo rehace la superficie de GBM y EGL** (el contexto de OpenGL es el mismo): probado solo con el KMS simulado. La
  prueba en QEMU (bochs con su KMS de verdad) no se hizo: la de la junta de Spotify estaba corriendo.
- La pantalla simulada no aplica la tabla a la captura (en la tarjeta la aplica la salida, después del framebuffer): los colores se
  comprueban por los números de la tabla, no a la vista.
- Con un modo mayor la rocola de XP se reacomoda (LayoutControls), pero XP se diseñó para 1024x768: lo que se vea raro en otras medidas es
  de XP (la petición del 17-10-2026 ya exploraba resoluciones mayores).
- Al juntar: los números de ganchos (g80-g82) y `GANCHOS.tsv`; `Kms.cs` cambia también el código de `rocola-consola` (retirado: no se
  compila para el sistema; los cambios no cambian nada sin `EligeModo`). La ventana de Ctrl+F4 queda lista para las demás hojitas del plan
  (K11: Internet, Spotify, Al día, La rocola, En casa...), que son de lo visual del final.
- El recordatorio «Rocola OS: Ctrl+F4» en la barra de Ctrl+F3 (decisión 2 del plan) no se puso: es un gancho en `Nativo/Hoja.Pinta.cs`
  y cambia lo que se ve de Ctrl+F3; queda para la presentación de lo visual.
