# Menos trabajo repetido por cuadro en Modest

Continuación del release `0a3f7b0` para el AMD A4-6300, dos núcleos, 3,5 GB de
RAM, SSD y Windows XP. Solo la rama Modest y .NET Framework 3.5. Esta tanda
conserva el aspecto: sus tres ahorros son automáticos, sin nuevos interruptores.
Los doce ajustes anteriores de Performance mantienen sus valores y dependencias.

## Qué cambia

1. **Recortes de las capas del fondo.** La nebulosa de cada color, la vía láctea y
   la viñeta guardan su geometría alrededor de las portadas y del estado opaco.
   Mientras la capa y las zonas tapadas sean iguales, no se vuelven a ordenar
   cortes ni a construir todos los triángulos. La textura se sigue dibujando en
   cada cuadro. Se comprueban las coordenadas, escala, UV, color, teselas y todos
   los rectángulos tapados; no se usa un hash que pueda colisionar.
2. **Recorte del cielo intermedio.** Con cielo a media/cuarta resolución se guarda
   también la geometría de su estirado. Repintar el contenido del cielo no obliga
   a rehacer ese recorte. Cambiar resolución, divisor, dimensiones de textura o
   zonas tapadas sí lo rehace. Se usa siempre la textura actual del dispositivo.
3. **Estrellas completamente cubiertas.** Con el estado opaco ya abierto y su
   versión actual subida, se omiten el cálculo del titileo/color y los seis
   vértices de cada estrella pequeña totalmente detrás del panel. Se conserva
   entera cualquiera que roce el límite, con margen conservador. El patrón, la
   deriva y el reloj siguen avanzando; al destaparse aparecen en el lugar correcto.
   Se aplica también a los dos patrones durante su fundido periódico.

Durante la entrada/salida del estado, con transparencia o una versión pendiente,
el campo de estrellas se dibuja entero. La estrella principal 2D/3D, sus gestos,
la ayuda sin rostro, fugaces y aurora conservan sus animaciones. Los textos y las
portadas siguen a resolución nativa. No se cambia audio, reproducción, datos,
análisis musical ni frecuencia de cuadros. El renderizador CPU queda intacto.

No hay un nuevo ajuste que activar. El tercer ahorro aprovecha **Estado extendido
opaco**, ya existente en Ctrl+F3 → Imagen → Performance; los otros dos actúan
cuando sus capas tienen zonas tapadas. El segundo necesita el cielo intermedio.

## Memoria acotada

Son cuatro entradas para capas y una para el cielo intermedio. Cada una conserva
como máximo 8.192 vértices de 28 bytes: hasta **1,094 MiB** de buffers de vértices
entre las cinco, más metadatos pequeños. Las reservas crecen geométricamente para
no realojar por cada pequeño cambio de cantidad; los aciertos no reservan arrays.
Un recorte mayor conserva el camino anterior. La caché no guarda bitmaps ni
texturas GPU; guarda una copia propia de los vértices, pues el buffer de trabajo
se sobrescribe con la siguiente capa.

## Verificación

`pruebas/trabajovisible.cs`: **206 comprobaciones** sobre el ejecutable:

- Igualdad byte a byte de posiciones, UV, colores, orden de triángulos y estado
  de dibujo entre recorte nuevo y reutilizado, incluso sobrescribiendo el buffer
  de trabajo. Cuatro capas alternas conservan sus propias entradas.
- Invalidación por posición, tamaño, UV, color, teselas y zonas tapadas. Retirar
  todas las zonas restaura la capa; una capa completamente tapada admite resultado
  vacío. Cambiar mitad/cuarto reconstruye el estirado.
- Estrellas con/sin puntas finas y tres niveles de latido: solo se omiten sprites
  enteros, el resto coincide byte a byte, y desactivar la ocultación restaura todo.
  Se comprueban versión pendiente, transparencia, transición y ambos patrones.
- Geometría en 1024×768, 1280×1024 y 1920×1080. Dos comparaciones de raster de
  referencia a **320×240**: recorte reutilizado y estrellas con su atlas real
  detrás de un panel. No se afirma rasterización real a las tres resoluciones.

Pasan las 18 baterías de la revisión anterior y se añaden `trabajovisible`,
`campoestelarbuffers` (9.700 comprobaciones del patrón) y `dibujoligero` (recortes,
opacidad y raster). El ZIP registra **21 baterías aprobadas sobre su Rocola.exe**.
GPU y CPU compilan con referencias oficiales de .NET 3.5 y advertencias como
error; pasan además `estadoeficiente`, `ajustesvivos` y `fichaportada` contra CPU.
El runner de Windows `validar-estado-extendido.ps1` y `todo.sh` incluyen la nueva
batería. El runner de Windows no se ejecutó aquí.

Se ejecutó también `cielohondo`: falla una aserción de maquetación («ningún texto
se corta») en Mono. Se reprodujo el mismo fallo en **0a3f7b0** con la misma prueba;
esta revisión no modifica esa interfaz. No se presenta como aprobada toda la
batería del repositorio. Las pruebas HAL omitidas tampoco cuentan como verificadas.

## Comparación sintética

`medirestado.cs` admite un quinto argumento `animado`: cambia las fases de
nebulosas 24 de cada 60 cuadros simulados. Se comparan antes (`0a3f7b0`) y ahora,
con los mismos siete ahorros anteriores activos, tres resoluciones, rejilla
estática/estado extendido y estrella 2D/3D. Dos rondas, invirtiendo el orden de los
ejecutables en la segunda. Cada caso prepara 120 cuadros y mide 360 intervalos
entre inicios, con 13 repintados de contenido del estado. Procesos secuenciales,
sin compilar ni ejecutar otras pruebas durante la comparación.

Las **48 ejecuciones de escenario** aceptan sus 361 cuadros y no fallan
presentaciones artificiales. No se crean bitmaps ni texturas adicionales durante
la medición en ninguna versión: esa reutilización ya existía.

En estado extendido, por cada caso nuevo:

- **936 de 1.444 recortes reutilizados** (64,8 %); los otros 508 se recalculan.
- Aproximadamente 147 estrellas cubiertas por cuadro en 1024×768, 247 en
  1280×1024 y 384 en 1920×1080. La cantidad depende del patrón, latido y posición;
  en estos casos evita preparar aproximadamente un tercio del campo pequeño.

Los contadores no existían en la versión anterior: sus ceros impresos significan
«sin contador», no «sin trabajo». El medidor no activa el cielo intermedio;
su recorte se comprueba por separado en `trabajovisible` y `performance`.

No hay una mejora uniforme del p99 o del máximo. Ejemplo de la primera ronda,
estado extendido y estrella 3D (milisegundos del host):

| Resolución | p95 antes / ahora | p99 antes / ahora | Máximo antes / ahora |
|---|---:|---:|---:|
| 1024×768 | 0,176 / 0,129 | 1,848 / 1,726 | 2,374 / 2,174 |
| 1280×1024 | 0,159 / 0,281 | 2,195 / 2,272 | 3,241 / 3,057 |
| 1920×1080 | 0,327 / 0,172 | 2,753 / 2,871 | 3,949 / 4,152 |

Las salidas de ambas rondas van en `MEDICION_SINTETICA.txt`. Esta prueba es
Linux/Mono CLR 4, con controlador artificial: no realiza el trabajo gráfico de
la GPU, no reproduce música y no espera al monitor. No prueba FPS reales ni
compatibilidad de ejecución en XP; el destino de compilación sigue siendo .NET
3.5. La copia de memoria específica de las pruebas Linux no se incluye en el ZIP.

## Comprobar en la rocola

Actualizar solo Rocola.exe, NAudio.dll y avisos, conservando la carpeta de datos.
Comparar la misma canción y opciones a 1024×768 primero, y después una resolución
mayor admitida por la pantalla. Usar estrella 2D/3D, navegación, apertura/cierre
del estado y ayuda, manteniendo comparable el repaso de biblioteca. Recoger
p95/p99/máximo con el medidor y diagnóstico. La mejora en el A4/XP y la fluidez a
mayor resolución siguen pendientes de medir en esa máquina.
