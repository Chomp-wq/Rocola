# Textos con menos trabajo por cuadro — Modest, 18-10-2026

Destino: AMD A4-6300, dos núcleos, 3,5 GB de RAM, Windows XP/.NET 3.5.
Solo `claude/modest-carson-fa2v2n`; referencia anterior `a9ded3e`.

## Dos ahorros automáticos

1. **Reutilizar la lista de textos GPU.** Antes, cada cuadro recorría todos los
   grupos y partes de texto, consultaba su diccionario de bitmaps y buscaba las
   texturas retiradas. Ahora compara la identidad y versión de los cinco grupos.
   La propia colección registra inserciones, retiradas, sustituciones y vaciado;
   también detecta cambios que conservan el mismo número de elementos. Solo se
   reconstruye la lista si cambia su contenido, orden o propietario.
2. **Revisar únicamente cargas pendientes.** Las partes que ya tienen textura
   salen de la cola de carga. Cuando todas están preparadas y no hay retiradas,
   se evitan los recorridos de carga y consultas al reloj de presupuesto. Lo
   visible sigue subiendo inmediatamente; el resto conserva la precarga con
   presupuesto y el descarte de barras ocultas. Una subida fallida queda pendiente
   para reintentarse. Retirar un título también retira sus cargas pendientes.

Mover una cinta o avanzar su reloj no reconstruye la lista: el dibujo lee la
posición, antigüedad y tamaño del objeto actual. Los cambios de título/color y
los reinicios gráficos actualizan lo necesario. El reinicio libera la lista
guardada y todas sus referencias, además de las texturas activas y retiradas.

Ambos ahorros conservan el aspecto y son automáticos, sin nuevos interruptores.
Se mantienen los trece ajustes Performance y sus dependencias. No se reduce la
resolución de textos o portadas, ni la frecuencia de animación. Audio,
reproducción, datos y renderizador CPU no se modifican.

La memoria adicional contiene referencias a las partes existentes y las
versiones de los grupos; no duplica píxeles ni texturas. Se conservan los límites
por grupo de 48 cintas y 2.097.152 píxeles, con partes de hasta 1024 de ancho.

## Verificación

`cintasligeras` pasa **447 comprobaciones**: carga visible sin demora, barra
oculta, reaparición, cambios de posición/orden/color/títulos, sustitución con el
mismo número de elementos, errores de subida y reintentos, lista de canciones,
vista previa, retirada antes de cargar y recuperación tras reinicio gráfico.
Se ejecuta a 1024×768, 1280×1024 y 1920×1080 con un controlador artificial.

`textosgpu` conserva sus 631 comprobaciones y `rendercache` sus 98. La prueba
existente de inserción directa en la colección se conserva sin modificar: la
notificación de cambios pertenece a la colección, no a sus llamadores.

El modo `huella` de la nueva prueba funciona también con el ejecutable anterior.
Compara vértices, estados de dibujo y todos los píxeles de las texturas enviadas
en procesos independientes. Los relojes se colocan en la pausa inicial para que
el tiempo transcurrido entre procesos no altere la comparación.
Las **27 huellas coinciden** con `a9ded3e`, incluidas sustituciones, cambios de
color, lista/vista previa y recuperación tras reinicio.

El release se verifica con **27 baterías relacionadas sobre el ejecutable del
ZIP**: las 25 del release anterior, más `cintasligeras` y `textosgpu`. Los guiones
Windows y Linux incluyen la nueva prueba. `VALIDACION.txt` identifica el
ejecutable comprobado y su SHA256.

GPU y CPU se compilan contra las referencias oficiales de .NET 3.5, sin
advertencias. Se conserva documentado el fallo previo de maquetación de
`cielohondo` bajo Mono, reproducido anteriormente también en `0a3f7b0`; no se
afirma que todas las pruebas del repositorio pasen.
También pasan `estadoeficiente`, `ajustesvivos` y `fichaportada` contra CPU.

## Medición

El modo `medir` de `cintasligeras` mide únicamente la preparación con las texturas
ya cargadas. Son 31 lotes de 1.000 operaciones, tras calentar, con 0, 12 y 48
títulos. Cada muestra es un promedio por lote; no representa la latencia de un
cuadro ni FPS. Se comparan dos rondas, invirtiendo el orden de los ejecutables.

Mediana de los promedios por lote, en microsegundos por preparación:

| Títulos / ronda | Anterior | Actual |
|---|---:|---:|
| 0 / 1 | 0.187 | 0.019 |
| 0 / 2 | 0.177 | 0.019 |
| 12 / 1 | 1.208 | 0.019 |
| 12 / 2 | 1.216 | 0.019 |
| 48 / 1 | 4.182 | 0.019 |
| 48 / 2 | 4.100 | 0.019 |

Cada caso nuevo registra 31.000 reusos y cero reconstrucciones. Esto mide el
camino con las texturas listas, no el coste de cambiar de página ni dibujar los
textos. La magnitud absoluta ahorrada es pequeña; no equivale a multiplicar los
FPS por la proporción entre esas cifras.

`medirestado` incorpora un modo `textos` que captura títulos largos de verdad
en la barra extendida. Compara cuadros completos en las tres resoluciones,
estrella 2D/3D y rejilla/estado extendido, con los perfiles `original` y `halo`
iguales en ambos ejecutables. Registra p50/p95/p99/máximo de intervalos
Start→Start, cuadros aceptados/rechazados y reconstrucciones de la lista.

Se completan 96 casos: **34.656 cuadros aceptados y cero rechazados**. En los
intervalos medidos de la versión nueva se evitan 17.328 reconstrucciones de la
lista. Los contadores inexistentes en la anterior aparecen como cero en este
medidor, o como −1 en el microbenchmark; no significan ausencia de trabajo.

La mediana de los p50 de los casos pasa de 0.1295 a 0.128 ms. No hay mejora
uniforme en p99/máximo: el mayor intervalo observado pasa de 5.061 a 5.570 ms.
Por tanto, la comparación confirma menos trabajo repetido, sin demostrar una
reducción de todos los tirones. Los registros completos se incluyen en
`MEDICION_SINTETICA.txt` del ZIP.

Las mediciones se ejecutan sin otras compilaciones ni pruebas simultáneas.
No incluyen trabajo de GPU real, espera de monitor, reproducción musical ni
Windows XP. La fluidez y una resolución mayor deben comprobarse en el A4 del
local; estos resultados por sí solos no permiten prometerlas.
