# Port nativo de la tarjeta: Direct3D 9 de XP a OpenGL ES 2 directo

Pista «tarjeta» del port nativo (decisiones del dueño del 8-11-2026, AGENTS.md: «Diseño del port nativo»). El dispositivo de la tarjeta de
XP (`D3D9Device`, en `Codigo/D3D9Star.cs`, `Codigo/PerformanceGpu.cs` y `Codigo/TexturasCompatiblesGpu.cs`) deja de llamar a una capa
que imita Direct3D 9 (`Plataforma/Direct3DaGL.cs`) y llama a OpenGL ES 2 directo, con la lógica de XP sin tocar. Este archivo dice qué
se tradujo, cómo se comprobó (con números), dónde están las herramientas y qué queda.

## T0. Las herramientas

### La grabadora de cintas (en el programa)

`Nativo/CintaTarjeta.cs`. Con `ROCOLA_CINTA=ruta`, cada llamada que el código de XP le hace al dispositivo de la tarjeta se apunta en un
archivo (Brotli) con todo lo que el dispositivo lee: los vértices de cada dibujo (los que lee: `DrawSky` 12, `DrawTexQuad` y
`DrawOpaqueQuad` 6, los demás `tris * 3`; si el arreglo de XP es más corto, su largo), los puntos de cada mapa de bits que se sube (la
zona que se sube, tal como están guardados: BGRA premultiplicado), las zonas, la imagen intermedia, la pasada de dos texturas, los
cuadros, los cambios de tamaño, lo que se suelta (`D3D9.Release`) y los dos campos que XP escribe directo en el dispositivo
(`VinetaPuntos` y `CubeBaseColor`, antes de la llamada que los usa). Cada método del dispositivo empieza con una línea

    if (CintaTarjeta.Graba && CintaTarjeta.Fuera) ...   // (pruebas: ROCOLA_CINTA, Nativo/CintaTarjeta.cs)

y `Graba` es un `static readonly` falso sin la variable: apagada no cambia nada. Encendida, la llamada pasa por la grabadora, que hace la
llamada de verdad (con `dentro` puesto, para no apuntar dos veces lo que el dispositivo se llama a sí mismo) y la apunta con sus
resultados. `ROCOLA_CINTA_CUADROS=a-b,c-d` (segundos desde que arrancó el programa) apunta solo los cuadros que empiezan en esos tramos;
crear, subir y soltar texturas, cambiar de tamaño y el dispositivo se apuntan siempre (el reproductor necesita las mismas texturas).

Esas líneas son lo único que se añade a los tres archivos del dispositivo que no es traducción: se reconocen por su comentario
`(pruebas: ROCOLA_CINTA, ...)`.

### El reproductor (fuera del programa)

`Pruebas/Tarjeta` (`ReproductorCintas.csproj`, excluido del programa con el resto de `Pruebas/`). Carga por reflexión (JIT) el
`rocola-xp.dll` que se le diga —la copia congelada con capas (`/root/rocola-trabajo/referencia-xp`, commit aef2211) o la traducción— y
le hace las llamadas de la cinta. Después de cada cuadro lee el fondo del dispositivo (lo que se presenta) y escribe una línea con su
SHA-256, los contadores del dispositivo (pedidos, envíos, cambios, pérdidas) y los errores de OpenGL (`glGetError`); y una línea por cada
llamada que devuelve algo (las texturas con un nombre propio de la corrida: t1, t2...). Dos corridas son iguales si sus salidas son
iguales línea por línea. Entre cuadro y cuadro ensucia OpenGL como el compositor, mpv y la flecha (otro framebuffer y otra vista, otro
programa, otras texturas en las unidades 0 a 3 y la unidad activa en la 3, mezcla `ONE/ONE` con `FUNC_REVERSE_SUBTRACT`, recorte,
tramado, caras, `ARRAY_BUFFER` y los atributos 0 a 4 de otro búfer, `UNPACK_ALIGNMENT` 1) y avisa al dispositivo como el anfitrión.

    dotnet build -c Release -o /root/rocola-trabajo/port/tarjeta/reproductor        (en Pruebas/Tarjeta)
    dotnet reproductor-cintas.dll --programa DIR --cinta CINTA --sale SALIDA.txt [--limite-textura N] [--redimensiona K:AxB,...]
                                  [--sin-avisar] [--sin-ensuciar] [--vuelca K1,K2 DIR] [--hasta K]
    dotnet reproductor-cintas.dll --compara DIR1 DIR2          (los volcados, punto a punto)
    dotnet reproductor-cintas.dll --sintetica CINTA            (la cinta hecha a mano, CintaSintetica.cs)

`--limite-textura N` pone el límite de textura del dispositivo (las texturas más grandes van en teselas, `TexturasDivididasGpu`);
`--redimensiona` cambia el tamaño del fondo antes de un cuadro (en Rocola OS la resolución no cambia con la rocola abierta: es lo que en
XP pasaba al cambiar la de Windows). La cinta sintética (`CintaSintetica.cs`, semilla fija) hace lo que las pantallas con los datos de
prueba no hacen o hacen poco: `DrawSky` de tres paradas, `DrawOpaqueQuad`, portadas compartidas, `PreloadTexture`, la misma textura con
`WRAP` y luego `CLAMP`, soltar texturas y crear otras en su sitio, presentar sin esperar (el Reset), dos tamaños, arreglos cortos o nulos,
dos dispositivos a la vez y un dibujo con una textura ya soltada.

### Las cintas

Grabadas con el programa de este árbol (antes de traducir; la grabadora vale igual después), datos de prueba nuevos en cada corrida,
fuera de pantalla con llvmpipe, en `/root/rocola-trabajo/port/tarjeta/cintas` (fuera del repositorio: 20 a 85 MB cada una).

Los guiones (`Pruebas/Tarjeta/guiones`; la carpeta de trabajo es `$TARJETA`, de fábrica `/root/rocola-trabajo/port/tarjeta`, con el
reproductor compilado en `reproductor/` y en `ref/` una copia de `bin/Release/net10.0` de la referencia congelada):
- `corre.sh PROGRAMA SALIDA [s]`: la rocola (JIT o NativeAOT) con una copia nueva de los datos de prueba de la referencia, fuera de
  pantalla y sin sonido; `CONFIG="clave=valor ..."` y las variables `ROCOLA_*` del entorno valen.
- `graba.sh NOMBRE FIN PULSA TRAMOS [PROGRAMA]`: graba una cinta (teclas con `ROCOLA_PULSA`, cuadros de esos tramos).
- `reproduce.sh CINTA PROGRAMA [opciones]`: la cinta en la referencia y en PROGRAMA, y si salen iguales.
- `control.sh NOMBRE ARCHIVO VIEJO NUEVO CINTA...`: un control negativo (el programa con un fallo puesto, compilado aparte).
- `pantallas.sh PROGRAMA NOMBRE [PANTALLA...]` y `estados.py REF1 REF2 OTRA...`: la traza de un cuadro de cada pantalla de la rocola
  entera y los estados de dibujo de cada pantalla frente a dos corridas de la referencia.
- `diff-xp.py ARCHIVO...`: el diff de `Rocola/ARCHIVO` contra `Codigo/ARCHIVO` sin los renombres de T1, con las líneas sin marca.

| Cinta | Qué recorre | Cuadros |
|---|---|---|
| 00-sintetica | la cinta hecha a mano (arriba) | 16 |
| 01-pared-disco-buscador-ayuda | pared, disco, buscador, teclas del buscador, asistente | 335 |
| 02-atraccion | pared y modo atracción (a los 40 s) | 116 |
| 03-video | la letra VIDEO, el disco, moneda y el video sonando | 97 |
| 04-perf-media | `cielo_media=1` | 61 |
| 05-perf-cuarto | `cielo_media=1 cielo_cuarto=1` | 121 |
| 06-perf-grano | `grano_vineta=1` (dos texturas) | 109 |
| 07-perf-capa | `cielo_media=1 fondo_capa=1` (la viñeta por punto) | 134 |
| 08-perf-todo | los cuatro, con disco, buscador y asistente | 263 |
| 09-medir | Ctrl+F3 → Imagen → «Medir este equipo» (páginas, letras, buscador, disco, atracción, reposo con el estado extendido, presentar sin esperar) | 830 |
| 10-pared-1280 | pantalla de 1280x1024 | 97 |
| 11-ctrlf3 | Ctrl+F3 abierto (Imagen, Fondo, Pantalla) con la tarjeta pintando detrás | 95 |
| 12-f12-dos-dispositivos | F12: la ventana de prueba con su propio dispositivo, y se cierra | 193 |

Comprobación de las herramientas (antes de traducir): cada cinta da la misma salida en la referencia congelada y en este árbol (con las
líneas de la grabadora), también con `--limite-textura 256` y con `--redimensiona 40:1280x1024,200:800x600`; dos corridas de la
referencia dan lo mismo; y saltando un dibujo (`ROCOLA_SALTA_DIBUJOS=5`) salen distintos los 57 cuadros de una cinta de prueba.

## T1. Los renombres mecánicos

En los 22 archivos de `Codigo/` que los usan (y en `Plataforma/D3D9Device.Gl.cs`, `Plataforma/PruebasLinux.cs` y la grabadora):
`D3D9.Vtx` → `Tarjeta.Vtx`, `D3D9.VtxSize` → `Tarjeta.VtxSize`, `D3D9.Vtx2` → `Tarjeta.Vtx2`, `D3D9.Vtx2Size` → `Tarjeta.Vtx2Size`,
`D3D9.Release` → `Tarjeta.Suelta` y `D3D9Device` → `DispositivoTarjeta` (también en los comentarios, para que el diff contra `Rocola/`
los quite como mecánicos). `Tarjeta` es una clase nueva con el vértice (28 y 36 bytes, sin cambiar) y `Suelta`, que en este paso aún
llama al `D3D9.Release` de antes. El `CopyMemory` (RtlMoveMemory) de `Form1.Pared.cs:485` pasa a `Buffer.MemoryCopy` (que también vale
con origen y destino que se pisan). En los archivos que son parte de `Form1` (`CieloVivoGpu.cs`, dos líneas) va `Rocola.Tarjeta.Vtx`:
dentro de `Form1`, `Tarjeta` es la tarjeta de Ctrl+F3 (`Form1.Ajustes.cs:379`).

Comprobación: el diff contra el paso anterior, quitando esos seis cambios, son 39 líneas: las definiciones movidas a `Tarjeta`, el
`Buffer.MemoryCopy`, las dos de `Rocola.Tarjeta` y la línea de la grabadora que pasa de `Release` a `Suelta`. Las 14 cintas dan lo mismo
que la referencia congelada (también `--limite-textura 256` con la sintética y la 01), y una cinta grabada con este paso también.

## T2. Las piezas nativas (aún sin usar)

En `Nativo/`:

- `GlMas.cs`: lo de OpenGL ES 2 que no está en `Gl.cs` (que es del programa de Linux de ahora y no se toca). Antes estaba dentro de
  `Direct3DaGL.cs`; ahora en su archivo, con `glBlendEquation`, `glDeleteProgram`, `glIsEnabled` y `glGetBooleanv` además.
- `SombreadoresTarjeta.cs`: los tres programas fijos de XP («color», «textura por color» y «dos texturas») con el texto EXACTO que
  generaba la traducción con capas, compilados una vez por dispositivo, con los atributos p, c, t0, t1 y e en 0 a 4 y el uniforme `m` por
  programa (solo se manda si cambia la medida del destino). `Triangulos` (28 bytes, de la memoria del programa: `DrawPrimitiveUP`),
  `TriangulosDeBufer` (del búfer de vértices: `DrawPrimitive`) y `Triangulos2` (36 bytes, el segundo juego en el atributo 3, que se apaga
  al acabar). Guarda, solo para la traza, lo que XP le pedía a Direct3D en las etapas 0 y 1 (los números de D3DTSS).
- `TexturasGl.cs`: `Nueva` (LINEAR y CLAMP al crearla; `GL_OUT_OF_MEMORY` da 0), `Framebuffer` (las que eran X8R8G8B8 empiezan en
  (0, 0, 0, 1); deja puestos el framebuffer y la máscara que había), `Sube` (por la unidad 2, desde una memoria aparte que se reusa;
  `false` si la zona no cabe, como `LockRect`), `Medida` y `Existe` (el registro por textura), `Envoltura` (CLAMP o REPEAT de la textura
  puesta, solo si cambia: en OpenGL ES 2 va con la textura y no con la etapa), `Borra` (con cola si llega de otro hilo, y el aviso
  `AlBorrar` para que el dispositivo olvide la textura puesta: OpenGL repite los nombres) y `Bytes`.
- `MemoriaTarjeta.cs`: VRAM + GTT por `DRM_IOCTL_RADEON_GEM_INFO` (512 MB si no es la radeon) menos `TexturasGl.Bytes`, en MB.
- `TrazaTarjeta.cs`: `ROCOLA_TRAZA_DIBUJOS` y `ROCOLA_SALTA_DIBUJOS` con el formato de antes; la mezcla, la textura y el destino los
  pregunta a OpenGL al trazar.
- `PantallaTarjeta.cs`: la única costura con el escritorio de hoy: `Presenta` (el fondo al control de la ventana y el anfitrión compone;
  sin ventana, `glFlush`), `Generacion`/`Tocada` (quien toca OpenGL sube la generación), `RefrescoHz` y `AhoraMs`.

Comprobación:
- Los textos: `reproductor-cintas --fuentes DIR SALIDA` escribe los de `SombreadoresTarjeta` (traducción) o los que genera
  `FuenteEtapas` de la referencia congelada para las tres combinaciones de XP, con los estados que pone XP. Los dos archivos son iguales
  byte a byte (1 815 bytes, MD5 `c88436cffffdd7b58f3204fa1bc7c238`).
- Las piezas solas: `reproductor-cintas --piezas DIR`: 22 de 22 (textura nueva con su medida, LINEAR y CLAMP, la unidad 0 activa al
  acabar; subir y leer lo mismo con 0 bytes distintos sin tocar la textura puesta en la unidad 0; zonas que no caben; REPEAT y CLAMP; el
  framebuffer de una X8 en (0, 0, 0, 1) dejando el framebuffer y la máscara de antes; la memoria que baja con las texturas; borrar avisa y
  la quita del registro; los tres programas compilan; sin errores de OpenGL).
- Con las piezas en el programa (sin usar), las cintas siguen dando lo mismo que la referencia.

## T3. El dispositivo, en un paso

`Codigo/D3D9Star.cs` (el dispositivo y el refresco del `GpuPanel`), `Codigo/PerformanceGpu.cs` y `Codigo/TexturasCompatiblesGpu.cs`
llaman a OpenGL ES 2 directo. Solo cambian las líneas que llamaban a Direct3D 9 y los bloques de interop (los delegados de la tabla de
COM y su caché, las constantes, `D3DPRESENT_PARAMETERS`, `D3DLOCKED_RECT`, `D3DCAPS9`); cada trozo cambiado lleva «EN LINUX» (60, 24 y
20 trozos; un guion lo mira: ninguno sin la marca). La lógica de XP no cambia: las cachés de estado (`stTss`, `stBlend`, `stTex`,
`stDireccion`, `stArg2Difuso`, `stOrigenAlfa`), juntar órdenes, los contadores, las potencias de 2 con su borde, la imagen intermedia,
las teselas, las portadas compartidas, la calibración UP o búfer, el reintento sin memoria y la pérdida del dispositivo. El diff contra
`Rocola/` quitando los renombres de T1: `D3D9Star.cs` 261 líneas fuera y 125 dentro, `PerformanceGpu.cs` 79 y 42,
`TexturasCompatiblesGpu.cs` 40 y 32; las de dentro llevan «EN LINUX» o van bajo su comentario «EN LINUX», salvo tres de antes de esta
pista (la espera del bucle y el texto de los FPS, de la pista de las ventanas y la del dibujo).

Lo de OpenGL que necesita los campos del dispositivo va en `Nativo/DispositivoTarjetaGl.cs` (otra parte de la misma clase). Salen del
programa `Plataforma/Direct3DaGL.cs` (1 406 líneas) y `Plataforma/D3D9Device.Gl.cs`.

| Direct3D 9 en XP | En Linux |
|---|---|
| `Direct3DCreate9` + `CreateDevice` (HARDWARE_VP y, si falla, SOFTWARE_VP) | `CreaFondo`: el fondo (una textura X8 con su framebuffer) y los tres programas, en el contexto de OpenGL del hilo de la pantalla; sin segundo intento (los vértices de XP ya vienen transformados) |
| `Reset` | `RehaceFondo` (el fondo a la medida pedida) y el `InitStates` de XP |
| `TestCooperativeLevel` | nada: en Rocola OS no pasa lo que en XP perdía el dispositivo (Ctrl+Alt+Supr, otra resolución, otro programa a pantalla completa; cambiar de consola cierra la rocola), y la capa siempre contestaba `D3D_OK`; la lógica de pérdida de XP sigue (la usan un dibujo que falla y la falta de memoria) |
| `BeginScene` / `EndScene` / `Present` | `EmpiezaEscena` (lo soltado desde otros hilos, la traza y, si otro tocó OpenGL, el estado otra vez) / nada / `PresentaFondo` (el fondo al control de la ventana: `Nativo/PantallaTarjeta.cs`) |
| `SetRenderState` (ZENABLE, CULLMODE, LIGHTING, ALPHABLENDENABLE, SRCBLEND, DESTBLEND) y `SetFVF` | `glDisable`/`glEnable` y `glBlendFunc`; el FVF son los punteros de cada dibujo |
| `SetTextureStageState` (las combinaciones de XP) | `SombreadoresTarjeta.Usa` del programa fijo |
| `SetTexture(0, t)` | `glBindTexture` en la unidad 0 con la dirección de la etapa 0; una textura ya soltada da `INVALIDCALL`, como antes |
| `SetSamplerState` ADDRESSU/V y MIN/MAGFILTER | `TexturasGl.Envoltura` de la textura puesta (en OpenGL ES 2 va con la textura); LINEAR al crearla |
| `CreateTexture` (A8R8G8B8, MANAGED) | `TexturasGl.Nueva`; sin memoria, `D3DERR_OUTOFVIDEOMEMORY` |
| `LockRect` / `UnlockRect` | una memoria aparte de la medida de la textura (`TexturasGl.Memoria`), `RellenaBorde` sobre ella y `TexturasGl.Sube` (glTexSubImage2D por la unidad 2) |
| `GetLevelDesc` | `TexturasGl.Medida` |
| `PreLoad`, `EvictManagedResources`, `AddRef` | nada: OpenGL ya subió la textura y no guarda copia manejada; la portada compartida lleva sus dueños (`AlSoltar` dice si le quedan) |
| `GetAvailableTextureMem` | `MemoriaTarjeta.Libre` |
| `GetDeviceCaps` (límite de textura, presentar sin esperar, dos texturas) | `GL_MAX_TEXTURE_SIZE`; las otras dos, sí (lo que contestaba la traducción con capas) |
| `DrawPrimitiveUP` | `Triangulos` y `Triangulos2` (glDrawArrays desde la memoria del programa) |
| `CreateVertexBuffer`, `Lock` (DISCARD o NOOVERWRITE), `Unlock`, `SetStreamSource`, `DrawPrimitive` | `glBufferData`, `glBufferData` sin datos (DISCARD) y `glBufferSubData`, `glDrawArrays` del búfer |
| `CreateTexture` RENDERTARGET, `GetSurfaceLevel`, `GetRenderTarget` y `SetRenderTarget` | `TexturasGl.Nueva`, `TexturasGl.Framebuffer` (X8: el alfa en 1) y `PonDestino` |
| la etapa 1 (la viñeta) y `ValidateDevice` | `DosTexturas` / `FinDosTexturas` (el programa «dos texturas», la viñeta en la unidad 1); una pasada |
| `GetDC` + `GetDeviceCaps(VREFRESH)` + `ReleaseDC` | `PantallaTarjeta.RefrescoHz` |
| `CopyMemory` | `Buffer.MemoryCopy` |
| `Release` de una textura | `Tarjeta.Suelta`: las teselas, las portadas compartidas o `TexturasGl.Borra` |

OpenGL es uno para todos (el compositor, el video de libmpv, la flecha, cada dispositivo). Quien lo toca sube
`PantallaTarjeta.Generacion` (el anfitrión al presentar y el video: una línea en `Plataforma/Anfitrion.cs` y cuatro en
`Plataforma/VideoMpv.cs`) y `EmpiezaEscena` vuelve a poner en OpenGL lo que el dispositivo tenía puesto (`RestauraGl`: el estado de
fábrica, el programa de `stTss` y la mezcla de `stBlend`) sin tocar las cachés de XP. En Direct3D 9 nadie más tocaba el estado del
dispositivo, así que la cuenta de cambios de XP sale como en la referencia. Las cintas vieron dos fallos de las primeras versiones: con
el `InitStates` de XP en lugar de `RestauraGl` los cuadros salían iguales pero `cambios` sumaba 1, y sin apagar la profundidad y las
caras al restaurar, el ensuciado con `CULL_FACE FRONT` cambiaba un cuadro.

Fuera de los tres archivos: `Plataforma/PruebasLinux.cs` (`--prueba-gpu` lee el fondo con `LeeFondo` y las texturas vivas con
`TexturasGl.Vivas`) y `Plataforma/LlamadasWindows.cs` (fuera `CapacidadPantalla`). `ROCOLA_TRAZA_DIBUJOS` y `ROCOLA_SALTA_DIBUJOS`
siguen con su formato (`Nativo/TrazaTarjeta.cs`); con `ROCOLA_ERRORES_GL=1` el dispositivo mira `glGetError` al presentar cada cuadro y
lo escribe.

Comprobación (todo fuera de pantalla con llvmpipe, en la máquina de pruebas):
- Las 14 cintas en la referencia congelada y en la traducción salen iguales línea por línea (por cuadro: el SHA-256 del fondo, los
  contadores y `glGetError` 0; y lo que devuelve cada llamada): 00-sintetica 16 cuadros, 01 335, 02 116, 03 97, 04 61, 05 121, 06 109,
  07 134, 08 263, 09 830, 10 97, 11 95, 12 193 y la de T1 107: 2 574 cuadros, 0 bytes distintos. También con `--limite-textura 256`
  (00, 01, 08, 09 y 10) y 64 (00), con `--redimensiona 40:1280x1024,200:800x600,500:1024x768` (09), `5:640x480,20:1366x768` (06) y
  `10:1600x900` con `--limite-textura 512` (05).
- Dos cintas grabadas con la traducción misma: n1 (JIT; pared, disco, buscador, video y Performance con el cielo a media y el grano,
  175 cuadros) y n2 (NativeAOT; Ctrl+F3 y la pared, 107 cuadros): iguales en la referencia y en la traducción, también con
  `--limite-textura 256`.
- Controles negativos (la traducción con un fallo puesto a propósito tiene que salir distinta de la referencia): sin avisar de que otro
  tocó OpenGL (la opción `--sin-avisar` del reproductor: el dispositivo no restaura su estado), 9 de 16 cuadros distintos en la
  sintética, 334 de 335 en la 01 y 108 de 109 en la 06; sin la envoltura por textura (compilada aparte con el guion `control.sh`), 3 de
  16 en la sintética (las cintas de la rocola no lo ven: el grano se lee en el centro de los texeles); sin el +0,5 del vértice (igual),
  16 de 16, 335 de 335 y 109 de 109.
- La rocola entera, JIT y NativeAOT, contra la referencia: la traza de un cuadro de cada pantalla (pared, disco, buscador, asistente,
  video, Ctrl+F3, atracción y Performance con media, grano y todo junto). Las corridas de la rocola entera no se repiten igual (azar sin
  semilla, la hora), ni siquiera dos de la referencia, así que se comparan los estados de dibujo de cada pantalla (mezcla, etapas,
  textura y destino; `estados.py`): ninguno fuera de los de la referencia y ninguno de los comunes que falte. Las capturas de esas
  pantallas, referencia y NativeAOT, iguales a la vista (el video incluido).
- El video: `--prueba-video` (centro negro en 0 de 23 cuadros) con JIT, con NativeAOT y con la referencia, y la rocola con el video
  sonando (cintas 03 y n1).
- `--prueba-gpu`: bien, «texturas vivas al soltar: 0». `ROCOLA_ERRORES_GL=1`: 0 errores en todas las pantallas (JIT y NativeAOT).
- NativeAOT (`dotnet publish -c Release -r linux-x64`): 0 avisos de IL o de AOT; los 15 avisos del compilador son los de antes (de
  otros archivos).
- No probado en el A4 (Mesa r600): no lo puedo probar desde aquí. Todo lo de arriba es con llvmpipe.

Diferencias que quedan:
- Con la referencia: la memoria libre no descuenta los 4 bytes de la textura negra de la capa (no hace falta).
- Con Direct3D 9 de XP (igual que la referencia): una textura soltada se borra al momento (en Direct3D seguía viva mientras estaba
  puesta en una etapa, por la cuenta de COM); el dispositivo la olvida (`OlvidaTextura`) y XP la vuelve a poner antes de cada dibujo,
  así que solo cambia si XP dibujara con una textura que ya soltó (la sintética lo hace: el dispositivo se da por perdido, como en la
  referencia). `TestCooperativeLevel` no da nunca pérdida, y las dos capacidades de Performance son siempre sí (como contestaba la capa).
- La presentación sigue pasando por el escritorio de hoy (`Nativo/PantallaTarjeta.cs`, la única costura: cuando la pista de las
  ventanas cambie el escritorio, cambia ese archivo) y `SubeBitmapBorde` y `ActualizaRegionBitmap` siguen leyendo el `Bitmap` (la pista
  del dibujo).

## T4. El reloj de la pantalla, sin compositor

`Codigo/RelojPantallaGpu.cs` le preguntaba al compositor de Windows (`DwmGetCompositionTimingInfo`, de `dwmapi.dll`, que existe de
Vista en adelante) cada cuánto refresca la pantalla y cuándo fue el último refresco, para empezar cada cuadro justo después de uno. En
el A4, con Windows XP, no hay `dwmapi.dll`: la primera llamada fallaba, `sinCompositor` quedaba puesto y `Rejilla` daba falso (el bucle
sigue con su tope de cuadros, «todo sigue como siempre»). En Rocola OS tampoco hay compositor que lo diga (el escritorio presenta por
KMS): fuera la llamada, su estructura y lo que se recordaba de ella; `Rejilla` da falso salvo con el compositor de mentira de las pruebas
(`RelojPantalla.Prueba`), como antes. Tres trozos, cada uno con «EN LINUX»; la cuenta (`EnMs`, `Tras`, `Despues`, `MomentoVista`,
`Siguiente`) no cambia.

Comprobación:
- `reproductor-cintas --reloj DIR SALIDA` (`Pruebas/Tarjeta/Reloj.cs`) escribe lo que contesta `RelojPantalla`: `Rejilla` sin
  compositor (seis veces, a distintas horas), con uno de mentira (6 frecuencias por 4 fases, y otra vez sin él) y la cuenta sola en una
  rejilla de valores. La referencia congelada (con la llamada a `dwmapi.dll`, que en Linux falla como en XP) y la traducción escriben el
  mismo archivo: 405 líneas iguales.
- NativeAOT: el ejecutable ya no nombra `dwmapi` (`strings`; antes de T4, sí); 0 avisos de IL o de AOT.
- La rocola entera en NativeAOT por sus pantallas, con la traza de un cuadro de cada una y `ROCOLA_ERRORES_GL=1`: 0 errores de OpenGL y
  ningún estado de dibujo fuera de los de la referencia. Los que faltan en alguna corrida (el brillo que suma del asistente; la imagen
  intermedia, que no se repinta si el cielo no cambió) salen en otras: es cuestión del momento del cuadro trazado. Para comprobarlo, una
  cinta grabada en NativeAOT en el asistente con la traza en un cuadro de su tramo (n3-aot-asistente, 31 cuadros: la traza tiene los 13
  estados de la referencia) da lo mismo en la referencia y en la traducción.
- No probado en el A4: no lo puedo probar desde aquí.

## Lo que queda

- Probarlo en el A4 (Mesa r600 por KMS; `MemoriaTarjeta` con el ioctl de la radeon): `--prueba-gpu`, las pantallas con
  `ROCOLA_ERRORES_GL=1` y una cinta grabada allí reproducida aquí. Desde aquí no se puede: todo lo de arriba es con llvmpipe.
- La pista de las ventanas: la presentación, el refresco y el reloj pasan por `Nativo/PantallaTarjeta.cs` (hoy, el escritorio de
  `Plataforma`); cuando cambie el escritorio, cambia solo ese archivo.
- La pista del dibujo: `CreateTextureFromBitmap`, `SubeBitmapBorde` y `ActualizaRegionBitmap` leen el `Bitmap` (`LockBits`); cuando el
  dibujo deje `Bitmap`, cambia la lectura y la copia a la textura sigue igual.
- Al juntar las pistas: esta toca una línea de `Plataforma/Anfitrion.cs` (177), cuatro de `Plataforma/VideoMpv.cs`, `CapacidadPantalla`
  de `Plataforma/LlamadasWindows.cs` y `PruebaGpu` de `Plataforma/PruebasLinux.cs`. Quedan comentarios que nombran la capa que ya no está
  (`Plataforma/Anfitrion.cs:4`, la cabecera de `Plataforma/LlamadasWindows.cs` —GetDeviceCaps, GetDC, Direct3D 9 y
  DwmGetCompositionTimingInfo—, `Plataforma/Ventanas.Control.cs:30`, `Plataforma/Ventanas.Escritorio.cs:763`) y lo que `Xp/LEEME.md`
  cuenta de `D3D9.EnGl` y de su banco de 71 pruebas (que no está en el repositorio y no se volvió a correr: las cintas dan los mismos
  bytes que la capa en lo que usa XP, no en todo el modelo de Direct3D); no se tocaron para no chocar con las otras pistas.
- Las líneas de la grabadora (`CintaTarjeta`, una por método del dispositivo, apagadas sin `ROCOLA_CINTA`) se quedan para comparar
  después; si se quitan, la referencia para las cintas tendría que ser otra.
