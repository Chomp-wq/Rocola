# Degradados y reflejos reutilizados — Modest, 17-10-2026

Destino: AMD A4-6300, dos núcleos, 3,5 GB de RAM, Windows XP/.NET 3.5.
Solo `claude/modest-carson-fa2v2n`; referencia anterior `978ab3d`.

## Tres ahorros automáticos

1. **Degradados de los paneles.** El estado extendido, las tarjetas y la ayuda
   reutilizan los píxeles de su degradado cuando coinciden altura y ambos colores,
   incluido el alfa. Se evitan el horneado y las copias de filas a GDI+ repetidas.
   Cada llamada entrega un pincel nuevo con su origen propio, que el llamador
   puede transformar o liberar. Guardar los píxeles ARGB originales evita el
   redondeo adicional de transparencia que puede introducir clonar un pincel.
2. **Degradado del cielo suave.** Se reutiliza su malla mientras coincidan ancho,
   alto, posición del horizonte, tres colores y extensión inferior. Un cambio de
   resolución, paleta o geometría la reconstruye. Se conserva el buffer existente,
   sin otra reserva de vértices. Sirve tanto a pantalla completa como al cielo
   intermedio, con sus respectivas dimensiones.
3. **Reflejo de la aurora.** Se reutilizan sus 864 vértices entre muestras iguales.
   Cada actualización de las alturas, colores o brillos invalida la caché; también
   lo hacen los cambios de ancho, horizonte o espacio del reflejo. Se mantiene el
   buffer existente. Un reflejo apagado u oculto conserva los descartes previos,
   y al volver utiliza los datos actuales.

Los tres conservan el dibujo y no añaden interruptores, según las reglas de
Modest. Los trece ahorros de Performance y sus dependencias siguen disponibles.
No se cambia la frecuencia de animación ni se baja la resolución de textos o
portadas. Audio, reproducción, datos y renderizador CPU no se modifican.

La caché de degradados retiene **cuatro mapas de 4 × hasta 2048 píxeles**:
128 KiB como máximo para esos píxeles, aparte de objetos GDI+ y pinceles entregados.
Los mapas sustituidos se liberan; alturas superiores se hornean sin retenerse.
La creación del pincel y la liberación de mapas están protegidas si coinciden
varios hilos. Las dos cachés de geometría solo añaden firmas y contadores.

El ahorro del reflejo aparece cuando se presentan varios cuadros entre muestras
de la aurora, por ejemplo con ondas a 24/50 Hz o presentación por encima de 60 Hz.
No se reduce su coste de reconstrucción cuando corresponde una muestra nueva.

## Verificación

`cachesgraficas` pasa 230 comprobaciones: igualdad de píxeles, transparencias,
orígenes y anchos; independencia del pincel; sustitución de mapas con pinceles
vivos; límite de memoria; tamaños extraordinarios y cuatro hilos concurrentes.
Comprueba además los envíos del reflejo, su recuperación al reactivarlo y todas
las invalidaciones de ambas mallas a 1024×768, 1280×1024 y 1920×1080.

Con el ejecutable anterior como segundo argumento, pasa 255 comprobaciones:
compara los píxeles de degradados y los vértices del cielo con `978ab3d` cargado
en un ensamblado independiente. No se considera suficiente que una caché
coincida consigo misma. `cielovivo` pasa 71 y `fotos1013` pasa 18 comprobaciones,
incluida la posición del reflejo junto a la línea del horizonte.

El release se comprueba con estas **25 baterías relacionadas sobre el ejecutable
empaquetado**: `cachesgraficas`, `cielovivo`, `fotos1013`, `auroraeficiente`,
`trabajovisible`, `campoestelarbuffers`, `dibujoligero`, `resolucionligera`,
`estadoeficiente`, `performance`, `rendercache`, `franjareloj`, `panelreposo`,
`fluidezcuadros`, `estrellaflujo`, `estrella3d`, `estelaestelar`, `ajustesvivos`,
`fichaportada`, `caratulasjpeg`, `hojeofino`, `papeleraportadas`, `asistentecombo`,
`asistentevisual` y `ayudaguia`. El ZIP registra resultados y SHA256 en
`VALIDACION.txt`. Los guiones Windows/Linux incluyen la nueva prueba.

GPU y CPU compilan contra referencias oficiales de .NET 3.5, sin advertencias.
También pasan `estadoeficiente`, `ajustesvivos` y `fichaportada` contra CPU.
Se mantiene documentado el fallo previo de maquetación de `cielohondo` bajo
Mono, reproducido anteriormente también en `0a3f7b0`; no se afirma que todas las
pruebas del repositorio pasen.

## Medición aislada de pinceles

`pruebas/medirdegradados.cs` prepara y libera el pincel sin pintar la pantalla.
25 lotes de 200 operaciones por altura, después de calentar, en cada ejecutable.
Se repiten dos rondas, invirtiendo el orden. Cada cifra es la mediana de los
promedios por lote, en microsegundos por operación; **no son FPS ni latencias
de cuadro**. El contador nuevo confirma 5.000 reusos y cero horneados durante
cada caso medido. Esto mide aciertos de caché, no el coste de una paleta nueva.

| Altura / ronda | Anterior | Actual |
|---|---:|---:|
| 245 / 1 | 81.494 | 4.147 |
| 245 / 2 | 78.301 | 2.547 |
| 327 / 1 | 107.364 | 3.246 |
| 327 / 2 | 103.844 | 3.373 |
| 346 / 1 | 112.809 | 3.344 |
| 346 / 2 | 110.144 | 3.404 |


## Cuadros completos con controlador artificial

Se comparan las dos versiones en tres resoluciones, con estrella 2D/3D y
rejilla estática/estado extendido. Hay dos perfiles idénticos entre versiones:
`original`, sin ahorros visuales, y `halo`, con los mismos ocho ahorros previos.
Las fases de nebulosas cambian 24 de cada 60 cuadros simulados. Dos rondas con
orden invertido, sin otras pruebas ni compilaciones simultáneas.

96 escenarios, 360 intervalos Start→Start por escenario: 34.656 cuadros
aceptados, ninguno rechazado. Las cachés nuevas evitan 17.328 reconstrucciones
del cielo durante los intervalos de esta escena con colores fijos. Los reusos
del reflejo son altos en el simulador porque presenta sin esperar al monitor;
ese porcentaje no representa lo que ocurrirá a 60 Hz en XP.

No hay mejora uniforme de p99/máximo. Para mostrar la variación, estado extendido
con estrella 3D, **p95 / p99 / máximo en milisegundos**:

| Perfil / resolución / ronda | Anterior | Actual |
|---|---:|---:|
| original / 1024x768 / 1 | 0.361 / 2.509 / 4.201 | 0.133 / 1.684 / 2.088 |
| original / 1024x768 / 2 | 0.136 / 1.750 / 2.172 | 0.136 / 1.721 / 2.504 |
| original / 1280x1024 / 1 | 0.312 / 2.866 / 3.366 | 0.148 / 2.204 / 3.031 |
| original / 1280x1024 / 2 | 0.246 / 2.450 / 3.140 | 0.173 / 2.359 / 3.257 |
| original / 1920x1080 / 1 | 0.207 / 2.540 / 3.212 | 0.180 / 2.553 / 3.127 |
| original / 1920x1080 / 2 | 0.276 / 2.587 / 3.247 | 0.396 / 3.633 / 4.879 |
| halo / 1024x768 / 1 | 0.120 / 1.822 / 2.248 | 0.182 / 1.902 / 2.149 |
| halo / 1024x768 / 2 | 0.163 / 1.934 / 2.162 | 0.154 / 1.783 / 2.007 |
| halo / 1280x1024 / 1 | 0.140 / 2.226 / 3.269 | 0.221 / 2.312 / 3.123 |
| halo / 1280x1024 / 2 | 0.147 / 2.244 / 3.179 | 0.188 / 2.366 / 3.160 |
| halo / 1920x1080 / 1 | 0.328 / 2.777 / 4.192 | 0.252 / 2.360 / 3.826 |
| halo / 1920x1080 / 2 | 0.166 / 2.553 / 4.222 | 0.260 / 2.473 / 3.869 |


Los resultados completos, incluidos rejilla y estrella 2D, van en
`MEDICION_SINTETICA.txt`. El medidor no reproduce toda la interfaz ni activa
el cielo intermedio; no ejecuta trabajo de GPU, audio ni entrada del usuario.
Son pruebas Linux/Mono CLR 4 con referencias de destino .NET 3.5. Siguen
pendientes la ejecución en Windows XP y la medición en la APU del local con
música real y la biblioteca estabilizada. No se promete fluidez a 1080p.
