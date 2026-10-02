# Caratulas: separacion, trabajadores e indice (1 de octubre de 2026)

Implementacion compartida entre `rocola-hd` y `claude/modest-carson-fa2v2n`. Renderizado CPU congelado; no se han cambiado sonido, preferencias, creditos, cola ni modelos.

## Las cuatro mejoras

1. **Portada, geometria y etiqueta separadas.** La rejilla GPU sube la miniatura final de 256 px sin ampliarla previamente a la resolucion de la casilla. GPU dibuja su cara frontal con el mismo redondeo y proporciones. Texto, contorno y palomito se conservan como un atlas transparente independiente. Los tramos verticales rectos del contorno se almacenan en una sola fila; la GPU los estira sin mezclar con el relleno transparente. El borde de la portada lleva suavizado geometrico de medio texel para evitar un recorte duro al deslizar. No se cambia la resolucion final de las miniaturas ni la paleta extraida.
   - Cambiar o recibir la portada no recompone la etiqueta.
   - La misma imagen comparte textura aunque cambie su marca de cola.
   - El presupuesto adaptativo comprende ambas subidas y el LOD. Las texturas cacheadas siguen disponibles aunque no quede presupuesto.
   - `BakeGpuCell` y su plan anterior se mantienen exclusivamente como referencia de regresion: la rejilla de produccion utiliza `BakeSeparatedCell`.
2. **Reutilizacion de trabajadores.** Los lectores GPU esperan con una condicion, sin bucle activo, hasta dos segundos sin trabajo; otra peticion los despierta y reutiliza. Al caducar ese plazo, invalidar F5 o cerrar, se liberan. Se conservan el cupo global, un lector por HDD, las prioridades visibles y los limites del regulador en equipos de dos hilos. No se reserva permanentemente un grupo de hilos en reposo.
3. **Indice ligero y lectura agrupada.** El pack existente de previews de 32 px sigue compatible. Un sidecar `.idx` guarda claves, offsets y CRC, no imagenes. Al arrancar se leen metadatos; si falta el indice o esta dañado, se recorren cabeceras saltando los pixeles. Cada preview se valida al utilizarse. Los pedidos del lote se ordenan por offset y comparten una apertura; un archivo inaccesible no provoca decenas de reaperturas. El indice se actualiza cada 128 registros y al compactar, no en cada lectura. La compactacion conserva tambien las entradas que aun no se han leido y repara colas incompletas. Todas estas escrituras se hacen en los trabajadores, no durante el dibujo.
   - Los contadores de lectura describen bytes solicitados por la aplicacion; no son medidas de lecturas fisicas del SSD/HDD ni del read-ahead de Windows.
   - Pack, metadatos y cache de pixeles siguen acotados. La cola FIFO de pixeles tambien se depura para evitar acumular nodos obsoletos.
   - Una preview ausente/corrupta no bloquea la caratula final.
4. **Memoria GPU adaptable.** Se mantiene la estimacion inicial existente y se revisa aproximadamente cada dos segundos o al cambiar la resolucion. Considera la estimacion de texturas del driver, la memoria RAM libre y el coste POT de las texturas propias para no entrar en una espiral de poda.
   - Presupuesto desconocido: 32 MiB; bajo presion RAM puede bajar a 16 MiB. Limites superiores conservadores: 96 MiB en 32 bits, 128 MiB en 64 bits, sujetos al perfil de memoria.
   - Historial de texturas: 54 a 216 entradas, calculado por coste; no hay un tope fijo de 54 para equipos potentes.
   - Se contabilizan conjuntamente etiquetas y portadas nativas. Visibles nunca se expulsan durante ese cuadro. Vecinas tienen preferencia; pueden ceder bajo presion real. La precarga puede liberar portadas historicas aunque su antigua etiqueta ya haya sido expulsada.
   - Es una estimacion, no una garantia de VRAM libre ni de 30 FPS. Una pagina visible que por si sola exceda el presupuesto se conserva.

No se han añadido interruptores: son optimizaciones automaticas. Los perfiles de carga existentes en Ctrl+F3 siguen funcionando.

## Verificacion

- Compilacion GPU y CPU con advertencias como errores para ambos destinos.
- 33 baterias de `pruebas/validar-capas.ps1`, con HAL real disponible y utilizado en esta sesion.
- `partescaratulas.cs`: geometria, contornos, marcas y texto de 1024x768 hasta 3840x2160; cambios de portada sin recomponer etiqueta; orden de lotes; indice corrupto, truncados y compactacion; reutilizacion/cierre de lectores; memoria GPU y proteccion de vecinas.
- 864 comprobaciones en esa bateria con HAL, tambien contra ambas compilaciones Release limitando el proceso a dos procesadores logicos.
- Lectura real de la superficie GPU para portadas pequenas, ultrawide y 4K durante desplazamientos fraccionarios y fundidos. Texto GPU con diferencia maxima de 1 nivel (en la matriz realizada, 0); comparacion software del contorno/marca con diferencia maxima de 1.
- No se exige igualdad pixel a pixel a la portada GPU: antes se interpolaba en GDI y despues en D3D; ahora se interpola directamente en D3D. Se comprueba un error global acotado (PSNR > 36 dB en el fixture) y se revisaron las imagenes capturadas. No se sacrifica la resolucion final ni se cambia el diseño.
- `cpucongelado.cs`: 14 metodos del renderer CPU con instrucciones, operandos, locales y excepciones identicos a los ejecutables anteriores, en ambas ramas.
- Compilacion Release HD (.NET 4.8) y Modest (.NET 3.5). En Modest persiste el aviso previo sobre el SDK 6.0A no registrado; la compilacion finaliza correctamente. **No es una prueba real en Windows XP.**

### Medicion aislada, no FPS del programa

En esta sesion, con afinidad a dos procesadores logicos, 18 composiciones de casillas correspondientes a 3440 px de ancho:

- HD: atlas anterior 122.65-124.03 ms; etiquetas separadas 3.58-3.91 ms.
- Modest: atlas anterior 122.13-126.53 ms; etiquetas separadas 3.99-4.21 ms.

Esto mide solamente la composicion CPU necesaria para la ruta GPU, sin lectura de biblioteca ni tiempo de subida/dibujo. No significa que el programa entero sea 30 veces mas rapido ni garantiza la carga instantanea desde cualquier disco. La afinidad tampoco representa el rendimiento de un procesador antiguo real.

Para reproducir las baterias en Windows: `pruebas/validar-capas.ps1`. Se usan datos de prueba aislados y no se sustituyen ejecutables de produccion. `-SinHal` permite omitir expresamente las pruebas de dispositivo; esa ejecucion no verifica el dibujo GPU real.
