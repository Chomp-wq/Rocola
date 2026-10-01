# Paginacion GPU a resolucion alta (30-09-2026)

Aplicado en HD y Modest. Optimizacion automatica, sin interruptor nuevo.
Modest conserva .NET 3.5; el renderizado CPU queda congelado.

## Que cambia

Las caratulas originales siguen decodificandose a 256 x 256. La demora que crece
con la resolucion no demuestra por si sola lentitud del disco: antes cada casilla
se componia al tamano de pantalla y se subia como un rectangulo grande, con el
cubo, dos lineas de texto y las columnas transparentes a sus lados. Ademas, las
texturas se redondean a potencias de dos para conservar el respaldo D3D9 antiguo.

Ahora el camino GPU compone un atlas compacto: la cara visible del disco ocupa
su propio rectangulo y la etiqueta se empaqueta en dos bandas estrechas. La GPU
recoloca las tres regiones en sus posiciones originales. Tres quads comparten
textura y un unico envio; no son tres cambios de textura ni tres draw calls.
No se disminuye la resolucion ni se cambia el filtro Bilinear, la fuente,
el tamano del texto, las marcas de cola, el giro o las transiciones.

Se reservan pixeles vecinos en las bandas para evitar costuras con el filtrado
bilineal al deslizarse. El contorno de la marca de cola se incluye con su margen
de antialiasing; no se corta en resoluciones altas. Cuando empaquetar no reduce
tanto el area real como los bytes de textura redondeados, se conserva la casilla
anterior. Por ejemplo, la prueba de 2560 x 1440 usa ese respaldo.

El atlas permanece en cache al volver de pagina. La precarga de la siguiente
pagina prepara/sube tambien los atlas, con los presupuestos de trabajo y memoria
ya existentes. No se agregan trabajadores ni limites de FPS. La identidad de la
portada invalida una imagen reemplazada, no solo el estado tiene/no tiene caratula.
Las casillas sin carpeta tienen propiedad y liberacion de bitmap, con el mismo
tope de historial. Cambio de resolucion/fuente, F5 y cierre limpian la cache.

Si el presupuesto permite usar temporalmente una textura anterior, se usan sus
metadatos de atlas, no las medidas del bitmap nuevo. Asi un cambio de dimensiones
no interpreta una textura antigua como si ya se hubiera subido la nueva.

## Mediciones aisladas en este PC

Con pared de prueba de ancho 3440 y alto 1280 (pantalla 3440 x 1440):

- Textura por casilla: 2 MiB antes, 1 MiB despues. Una pagina de 18: 36 -> 18 MiB.
- En la prueba 3840 x 2160: 4 -> 2 MiB por casilla.
- En la prueba 1024 x 768: 256 -> 128 KiB por casilla.
- 36 preparaciones HAL (crear, copiar y liberar textura), alternando orden:
  HD aproximadamente 24-26 -> 14-16 ms; Modest 25-30 -> 16-17 ms en las primeras
  mediciones. Es alrededor de un 40 % menos en esta operacion aislada.
- Componer 36 casillas: sigue rondando 230-250 ms en esta prueba; las diferencias
  son pequenas y variables. No se afirma eliminar el escalado de GDI ni acelerar
  toda la pagina en ese porcentaje.

Son comparaciones sinteticas, no FPS de la biblioteca real ni una medicion de
transferencia GPU sincronizada. La memoria indicada es el presupuesto de texeles
ARGB redondeados, no una lectura fisica de VRAM. El ahorro exacto depende de las
medidas de casilla y de si lleva marca de cola.

Se probo tambien un prototipo que escalaba directamente la caratula de 256 en la
GPU: cambiaba mas el resultado del filtro. No se adopto. El cambio entregado
conserva el escalado existente; no afirma haber eliminado toda recomposicion.

## Verificacion

- PASS en ambas ramas: bateria completa validar-mejoras (31 fixtures y rendercache)
  y validar-capas (31 baterias), con compilacion GPU/CPU y advertencias como errores.
  Tras el ajuste final de desbloqueo se repiten atlasgpu, hojeoportadas, pantallaviva,
  espacioprecarga y cargaadaptativa.
- atlasgpu: 480 comprobaciones en ambas ramas. Siete resoluciones, portada opaca
  y transparente, ausencia de imagen, marcas de cola, cache, precarga, vuelta,
  resolucion nueva, casillas sin carpeta y metadatos de textura anterior.
- Texto: identico pixel a pixel al bitmap anterior. Caratulas: comparacion acotada
  a una diferencia maxima de 1/255 por canal, en menos de un 1 % de pixeles.
  La traslacion entera del dibujo puede cambiar unos pocos redondeos de GDI;
  no se describe toda la imagen como bit-identica.
- Comparacion de superficies HAL reales con posiciones fraccionarias y atenuacion:
  sin cortes ni costuras; mismo limite de redondeo de un nivel por canal.
- La nueva bateria falla frente al ejecutable previo porque falta el atlas.
- atlasgpu admite --dos-hilos despues de la ruta del ejecutable. Afinidad reducida
  no equivale a probar un procesador antiguo de dos nucleos.
- cpucongelado: 14 metodos del perfil CPU con IL identico al ejecutable anterior,
  incluyendo Cube.Draw, BakeCell, PeekOrBakeCell, OnPaint y la cache CPU.

Ejecutar desde cada rama:
powershell -File pruebas/validar-mejoras-20260929.ps1
powershell -File pruebas/validar-capas.ps1

La comparacion CPU es manual: compilar pruebas/cpucongelado.cs junto a
pruebas/leercodigo.cs (lee el IL por el mismo sitio que las baterias) y ejecutar
con las rutas del ejecutable CPU anterior y del nuevo. Los perfiles deben
compilarse con el mismo compilador/opciones para comparar su IL. Si los bytes
difieren solo por numeros de tabla corridos (un campo o un texto nuevo en otro
sitio), compara lo que nombra cada instruccion.

No se certifica XP real, FPS en la biblioteca del usuario ni HDD/SSD fisicos.
Las compilaciones/pruebas se realizan en temporales, sin sustituir releases,
musica, modelos, configuracion, creditos o cola.
