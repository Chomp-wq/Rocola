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
fuera de pantalla con llvmpipe, en `/root/rocola-trabajo/port/tarjeta/cintas` (fuera del repositorio: 20 a 85 MB cada una). Los guiones
`corre.sh`, `graba.sh` y `reproduce.sh` de esa carpeta corren la rocola con una copia nueva de los datos y graban o reproducen.

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
