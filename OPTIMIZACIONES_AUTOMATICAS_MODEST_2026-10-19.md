# Ahorros automáticos del dibujo, con huellas de cuadro — Modest, 19 de octubre de 2026

Destino: AMD A4-6300, dos núcleos, 3,5 GB de RAM, Windows XP / .NET Framework 3.5. Solo la rama `claude/modest-carson-fa2v2n`; punto de
partida `e1ae788`. Petición: que todo lo que ya existe en Ctrl+F3 (Imagen, Fondo → Cielo vivo y Pulido, las hojitas de Performance, estrella 2D/3D,
aurora, grano, cielo, estado extendido, portadas, textos, transiciones, estrella asistente) y los cuatro presets (De fábrica, Performance, Medio,
Bajo) se dibuje **con el mismo aspecto y más barato**, de forma automática (sin interruptores nuevos), y seguir buscando ahorros por todo el camino
gráfico.

**Resumen.** El camino gráfico ya estaba muy exprimido (el procesador gasta ≈ 0,1 ms por cuadro con la tarjeta imitada y no reserva memoria), así que
lo que queda es poco y se midió en lo que sí se puede medir aquí: **llamadas al controlador, pantallas pintadas por cuadro y vértices**. Se
encontraron y se aplicaron **cinco ahorros automáticos**, todos con los mismos píxeles en las 168 pantallas comparadas. No hay ahorros B (con
interruptor): ninguno cambiaba el aspecto lo bastante y ahorraba lo bastante a la vez.

| # | Ahorro | Dónde | Resultado medido (tarjeta imitada) |
| --- | --- | --- | --- |
| 1 | Estados de mezcla y de etapa solo cuando cambian | todos los presets | llamadas `SetRenderState` por cuadro 24 → 14,4 (−40 %), `SetTextureStageState` 40 → 32 (−20 %) en la rejilla; 24 → 15,6 y 40 → 27,2 (−35 % / −32 %) en el reposo de fábrica; 22 → 15 y 30 → 24 en Bajo |
| 2 | Cielo de fábrica guardado y estirado (G1 a tamaño completo) | De fábrica, sin portadas opacas encima (reposo, atracción, buscadores) | 6,45 → 5,30 pantallas por cuadro de media (−18 %) a 1024×768; 6,29 → 5,17 a 1920×1080. **El peor cuadro sube de 6,45 a 7,45 pantallas** |
| 3 | Franjas del cielo bajo el estado opaco no se mandan | Bajo (y cualquier combinación con estado opaco y cielo directo) | 1,98 → 1,69 pantallas por cuadro (−15 %) a 1024×768; 1,95 → 1,66 a 1920×1080 |
| 4 | Malla de cada portada de la rejilla guardada | navegación | 18 mallas armadas por cuadro → 0 (18 reusadas); 2,9 → 1,2 µs por portada con la llamada incluida, ≈ 0,03 ms por cuadro |
| 5 | Dos esquinas copiadas en el cuadrilátero de cada estrella | estrellas 2D de todos los presets | 26,5 → 20,6 ns por estrella (−22 %), ≈ 8 µs por cuadro |

Lo que **no** se movió en nada medible: el tiempo de procesador por cuadro completo (p50/p95/p99 dentro del ruido de ±10 % de este equipo, ver la
tabla de abajo). No se afirma que la rocola real vaya más fluida: falta medirlo en el A4.

## Cómo se comprobó que es la misma imagen

Herramienta nueva, `pruebas/huellacuadros.cs` (más `huella-compila.sh` y `huellareloj.cs`): una tarjeta imitada que además **pinta** en software lo que
le llega (vértices ajustados a 1/256 de píxel, funciones de borde en enteros, regla de arriba-izquierda, centros de píxel en las coordenadas
enteras de Direct3D 9, una o dos texturas con sus operaciones, mezcla normal o sumada, imagen intermedia). De cada cuadro saca tres huellas: los
**píxeles**, el **chorro bruto** de órdenes (estados y vértices) y el chorro canónico (sin triángulos que no pintan). Para que dos ejecutables
reciban exactamente lo mismo, `huella-compila.sh` compila una **copia** de las fuentes con los relojes (`Environment.TickCount`, `Stopwatch`,
`DateTime.Now`) y el azar sin semilla cambiados por los de `huellareloj.cs`; la rocola que se entrega no lleva nada de esto. Dos montajes iguales dan
huellas iguales (se comprueba en la batería).

- **168 pantallas** (3 resoluciones × 4 presets × rejilla/reposo × estrella 2D/3D = 48; cada interruptor visual apagado y cada ahorro de Performance
  encendido en De fábrica y apagado en Performance, en rejilla y reposo = 112; la estrella asistente y su guía = 8): **168 de 168 con los
  píxeles idénticos** a los de `e1ae788`.
- El **chorro bruto** (cada orden y cada vértice) es idéntico en 110 de las 168: todas las de rejilla, Performance y Medio. Cambia, a propósito, en las
  58 donde actúan los ahorros 2 y 3 (reposo de fábrica, y reposo de Bajo): 8 de fábrica, 6 de Bajo, 43 de los interruptores sueltos en reposo de
  fábrica y 1 de Performance con «Cielo a media resolución» apagado (que es el cielo de fábrica).
- Donde el ahorro 2 se probó con **portadas encima** (huecos), los píxeles no eran idénticos (255 de 786.432, un nivel de 255, por el redondeo de los
  rectángulos alrededor de cada portada): por eso el ahorro 2 **no actúa con huecos** y la rejilla queda como antes, orden por orden.
- El dibujo por procesador (congelado): `cpucongelado` compara 14 métodos con el ejecutable previo → idénticos.
- Es una imitación hecha aquí, con el GDI+ de libgdiplus de este equipo: no es una tarjeta ni el GDI+ de Windows XP.

La batería `huellacuadros` (en `todo.sh`) repite 16 de esas pantallas (≈ 2 min), exige los píxeles de `huellas_modest_2026-10-19.txt` y el chorro bruto
donde corresponde, y vigila que los ahorros sigan ahorrando.

## Inventario

Tarjeta imitada, 480 cuadros, nebulosas animadas 24 de cada 60 cuadros, rejilla con 18 portadas opacas «directas» o estado extendido con la cola de
doce y la barra de progreso en neón, estrella 3D, aurora, textos largos. Cada fila: **antes → después**.

| 1024×768 | Órdenes | `SetRS` | `SetTSS` | Pantallas (media / máx.) | Vértices (KB) |
| --- | ---: | ---: | ---: | ---: | ---: |
| De fábrica, rejilla | 55 | 24,7 → 14,4 | 40 → 32 | 4,99 / 5,00 (igual) | 676 |
| De fábrica, reposo | 19,9 → 19,2 | 24 → 15,6 | 40 → 27,2 | 6,44 / 6,45 → **5,30 / 7,45** | 598 → 586 |
| Performance, rejilla | 52,6 | 21,6 → 13,6 | 34 → 27,2 | 1,65 / 1,82 (igual) | 324 |
| Performance, reposo | 16,5 | 23,6 → 16,6 | 34 → 27,2 | 1,42 / 1,59 (igual) | 236 |
| Medio, rejilla | 52,1 | 21,6 → 13,6 | 34 → 27,2 | 1,61 / 1,76 (igual) | 287 |
| Medio, reposo | 16 | 23,6 → 16,6 | 34 → 27,2 | 1,38 / 1,53 (igual) | 209 |
| Bajo, rejilla | 48,6 | 18 → 11 | 30 → 24 | 2,25 (igual) | 94 |
| Bajo, reposo | 13,5 | 22 → 15 | 30 → 24 | 1,98 → **1,69** | 41 → 40 |

A 1280×1024 y 1920×1080 salen las mismas llamadas y, en pantallas, De fábrica reposo 6,42 → 5,28 y 6,29 → 5,17, Bajo reposo 1,99 → 1,70 y 1,95 → 1,66; lo
demás igual. Quién pinta qué, De fábrica reposo 1024×768 (pantallas completas): cielo 1,00; dos capas de nebulosa 1,94; vía láctea 0,53; relleno de la
aurora 0,37 (+ 0,09 de neón y 0,01 de reflejo); grano 1,00; estrellas 0,04; luna y estrella 0,18; viñeta 0,78; panel de estado 0,32; progreso 0,02 = 6,45.
Bajo reposo: cielo 1,00 (0,32 de ellas debajo del panel opaco, que no se ven nunca), viñeta 0,52, panel 0,32, resto 0,14.

Tiempos de procesador de este equipo (Linux/Mono, controlador artificial con el conteo de área encendido, mediana de 5 rondas con orden alternado, un
proceso por medición), **p50 / p99 en ms, antes → después**:

| Escenario | p50 | p99 | máximo |
| --- | --- | --- | --- |
| 1024×768 de fábrica, rejilla | 0,375 → 0,346 | 0,840 → 0,721 | 1,31 → 1,12 |
| 1024×768 de fábrica, reposo | 0,260 → 0,266 | 0,709 → 0,824 | 1,35 → 1,05 |
| 1024×768 Performance, rejilla | 0,330 → 0,323 | 0,907 → 0,612 | 1,95 → 1,02 |
| 1024×768 Performance, reposo | 0,254 → 0,242 | 0,971 → 0,897 | 1,67 → 1,50 |
| 1024×768 Bajo, reposo | 0,089 → 0,094 | 0,422 → 0,449 | 0,87 → 1,12 |
| 1920×1080 de fábrica, rejilla | 0,476 → 0,510 | 1,163 → 1,175 | 1,59 → 2,20 |
| 1920×1080 de fábrica, reposo | 0,338 → 0,343 | 1,952 → 1,970 | 2,50 → 2,22 |
| 1920×1080 Performance, reposo | 0,344 → 0,331 | 1,974 → 1,806 | 2,47 → 2,68 |
| 1920×1080 Bajo, reposo | 0,106 → 0,103 | 1,248 → 1,208 | 1,66 → 1,58 |

Mezcla de mejoras y empeoramientos dentro del ruido (±10 %): **no se puede afirmar** que el procesador gaste menos por cuadro. Memoria por cuadro: 0 B y 0
recolecciones en 480 cuadros, antes y después (las 24 filas).

## Los cinco ahorros, uno por uno

1. **Estados que no cambian** (`D3D9Device.Blend` y `TssModulate`, `D3D9Star.cs`). Entre «aditiva» y «normal» el origen de la mezcla es el mismo (alfa): ya
   no se manda `SRCBLEND` otra vez; entre «color de vértice» y «textura por color» los segundos argumentos (`COLORARG2` y `ALPHAARG2` = el color del vértice)
   no cambian: ya no se mandan. Las dos banderas se olvidan donde se olvidaba el resto de lo apuntado (`InitStates`, `EstadoFallido`, la pasada de dos
   texturas). El chorro bruto (cada orden con su estado real y sus vértices) sigue idéntico en las rejillas.
2. **Cielo de fábrica a tamaño completo** (`GpuPanel.CieloMedioBase`, `PerformanceGpu.cs`). G1 (14-10-2026) no repetía el cielo de la imagen intermedia si no
   había cambiado, pero solo con «Cielo a media resolución». Ahora, sin ese interruptor y con nebulosas, el degradado, las dos capas de nebulosa y la vía
   láctea se pintan en una imagen del tamaño de la pantalla **solo cuando cambia su firma** (fase de las nebulosas a 24 Hz, colores, tamaño, textura…) y
   los demás cuadros la copian (un cuadrilátero opaco, pixel a pixel). A tamaño completo la imagen no lleva el borde de sobra (`MediaLado` con 1) ni se
   reescalan los vértices. **Costes que hay que decir**: (a) el cuadro en que el cielo cambia pinta una pantalla más (la copia): el peor cuadro pasa de 6,45 a
   7,45 pantallas, y ese caso es el 40 % de los cuadros; la media baja un 18 %; (b) memoria de la tarjeta: 4 MB a 1024×768, 8 MB a 1280×1024, 16 MB a
   1920×1080 (potencias de dos); (c) no actúa con portadas opacas encima (ver arriba). Si en el A4 el peor cuadro importa más que la media, este ahorro
   se revierte solo: va en un commit aparte.
3. **Franjas del cielo bajo el estado opaco** (`GpuStarfield.CieloDirecto` y `CieloSuaveVisible`, `D3D9Star.cs`/`PulidoGpu.cs`). Con el estado extendido opaco
   asentado, de `EstrellasTapadasDesde` hacia abajo lo tapa el panel; las franjas del cielo suave que empiezan ahí no se mandan. Una franja tapada en
   parte se pinta entera, así que no cambia ni el degradado de lo que se ve. Antes, en Bajo, 0,32 pantallas se pintaban para taparlas después.
4. **Mallas de portada guardadas** (`DrawNativeCover`, `CaratulasSeparadasGpu.cs`): 54 vértices por puesto (≤ 36 puestos, ≈ 54 KiB), rehechos solo si
   cambian el sitio, el tamaño, las coordenadas de textura, el tamaño del bitmap o el color/opacidad. Contadores `CubiertasCalculadas/Reusadas`.
5. **`SetQuadUV`**: cuatro esquinas y dos copias en vez de seis vértices calculados (como ya hacían halos, cuadriláteros y cubos).

## Lo que se miró y no se cambió

| Idea | Por qué no |
| --- | --- |
| Quitar los triángulos con alfa 0 de la aurora (≈ 204 de 2.880 por cuadro, 7 %) | No tienen área de píxeles que cambie nada; filtrarlos cuesta más procesador del que ahorra la tarjeta. Quedan en el chorro canónico como «invisibles» |
| Estrellas con alfa 0 | 0 de ≈ 1.400 con la atenuación normal; el cruce de patrones ya omite las de brillo < 0,002 |
| Suelo y reflejo de la aurora bajo el panel opaco | Ya se omiten (`EstadoOcultaDesde`): con el horizonte en 538 y el panel desde 525, todo el suelo queda tapado |
| Grano | Todos los texeles tienen alfa; no hay nada exacto que saltar |
| Viñeta punto por punto (G2, `D3D9Device.Vinetea`) | Es el mayor gasto gestionado de Performance (≈ 12,9 ns por vértice, ≈ 10.000 vértices por cuadro: ≈ 0,13 ms aquí, 15 % de las muestras). Un cambio exacto (repetidos por cuadrilátero) gana ≈ 25–33 % (≈ 0,03–0,04 ms) y guardar la malla por generación no sirve con las ondas a 60 por defecto (se rehace cada cuadro). Los demás cambios (truncar a entero con trucos de bits) no son exactos. Sin ganancia que valga su riesgo |
| Mallas de etiquetas de la rejilla (`AtlasCellVertices`) y recortes de capas (`Trozos`, 1,4 por cuadro) | ≈ 0,3 µs por celda y ≈ 4 % del tiempo gestionado: no medible |
| Cachear fuentes de GDI+ en el repintado del panel (6 por repintado) | `new Font` cuesta 4,8 µs con libgdiplus y el repintado 3,1 ms (98 % pango/cairo): no se puede medir aquí lo que cambiaría en el GDI+ de XP |
| `COLOROP = SELECTARG2` para «color de vértice» (ahorra 6 de cada 10 llamadas de etapa) | Es igual en la especificación de Direct3D 9, pero deja `TEXTURE` como argumento sin textura puesta; no se puede probar en un controlador de verdad aquí |
| Cielo con nebulosas a media resolución en Bajo | Cambia el aspecto (sería un ahorro B) para 0,26 pantallas que el ahorro 3 ya casi iguala |
| Cielo de fábrica guardado también **con** portadas encima | Los píxeles cambian un nivel de 255 en el borde de los huecos (255 píxeles medidos) |
| Un Direct3D 9 de mentira nativo para correr la rocola entera | Se construyó (`libd3dfalso.so`), el dispositivo arrancó, pero el bucle de dibujo no llegó a correr bajo xvfb/Mono; se descartó y no se entrega |

## Qué falta medir en el A4

1. El peor cuadro del ahorro 2 (7,45 contra 6,45 pantallas) y la memoria de la tarjeta compartida: «Medir este equipo» con De fábrica y con `diagnostico=1`.
2. Cuánto pesa de verdad una llamada `SetRenderState`/`SetTextureStageState` en el controlador de XP (aquí solo se cuentan).
3. Los repintados del panel de estado en el GDI+ de XP (este equipo usa libgdiplus).

Este informe es de pruebas sintéticas en Linux/Mono con una tarjeta imitada; no prueba Windows XP, una tarjeta real, el monitor ni el audio.

## Verificación

- Compila con `-sdk:2 -langversion:3`, advertencias como errores, perfiles con y sin tarjeta (C# 3, .NET 3.5).
- `huellacuadros`: 16 pantallas y la comparación de 168 hecha a mano con el mismo reloj: 168 de 168 con los píxeles de `e1ae788`; chorro bruto idéntico en 110.
- Dibujo por procesador congelado: `cpucongelado` → 14 métodos idénticos al ejecutable previo.
- Baterías que miraban el código del cuadro y se ajustaron a la nueva estructura (sin quitar nada que vigilaran): `performance` (el cuadro de fábrica sin nebulosas
  sigue siendo «todo a la pantalla»; con nebulosas se pinta una vez y se estira; G2 sin el cielo a media resolución sigue sin entrar en la imagen),
  `dibujoligero` y `presencia` (el cielo directo ahora vive en `CieloDirecto`).
- Tanda completa (`pruebas/todo.sh`, con el `rocola.exe.config` de las pruebas de Mono junto al ejecutable): los mismos nombres con problemas que la tanda
  de referencia salvo seis que ahí fallaban solo por falta de ese archivo (`auroraeficiente`, `cintasligeras`, `estadoeficiente`, `hojeoportadas`,
  `resolucionligera`, `trabajovisible`: ahora pasan) y salvo `resol`, una prueba de tiempos del dibujo por procesador (sin tocar) que pasa 8 de 9 veces y
  falló una vez con 1,38 > 1,35; `presencia` falló en la primera tanda por la reestructuración y se corrigió. Ninguna falla es nueva.
- Pruebas de rendimiento, de mezcla de estados y de píxeles con una tarjeta imitada en Linux/Mono: no prueban Windows XP, una tarjeta de verdad ni la pantalla del bar.

## Cómo repetirlo

```sh
sh pruebas/todo.sh huellacuadros performance dibujoligero presencia
sh pruebas/huella-compila.sh . /tmp/rocola_huella.exe
mcs -sdk:4 -out:huella.exe -r:System.Windows.Forms.dll -r:System.Drawing.dll pruebas/huellacuadros.cs pruebas/leercodigo.cs
xvfb-run -a mono huella.exe /tmp/rocola_huella.exe huella "1024x768 bajo reposo"     # huellas
xvfb-run -a mono huella.exe rocola_gpu.exe coste "1024x768 alto reposo"              # procesador, llamadas, pantallas
xvfb-run -a mono huella.exe rocola_gpu.exe detalle "1024x768 alto reposo est3D"      # orden por orden
```
