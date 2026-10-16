# Estado extendido: Modest en el AMD A4 con XP

Objetivo confirmado: AMD A4-6300 APU, dos núcleos, 3,5 GB de RAM, SSD y 1024×768.
Solo `claude/modest-carson-fa2v2n`, destino .NET Framework 3.5. Se conserva el
renderizador CPU y no se modifica reproducción, DSP, créditos, cola ni controles.

## Qué permite concluir el registro

El adjunto contiene el fallo antiguo de GDI+ de la versión 11:11 y, en la versión
18:54, pausas de 516–2.063 ms durante cuadros, además de 1.766 ms al publicar la
biblioteca. El segundo conjunto no contiene una excepción. «Un cuadro de la
tarjeta» era una etiqueta demasiado amplia para atribuirlo a una operación.

No basta para concluir que la GPU, la RAM, el preanálisis o la aurora sean por sí
solos la causa de las pausas. Terminar el escaneo tampoco significa terminar el
repaso de canciones o las miniaturas. La nueva versión identifica fases más
concretas: preparar el estado, subirlo, preparar textos, dibujar fondo/estrella,
dibujar interfaz y presentar. Los avisos de parada incluyen pendientes de repaso,
miniaturas y si se está construyendo la biblioteca, sin crear cadenas por cuadro.

## Ahorros automáticos

1. **Mapa del estado reutilizado.** Al cambiar canciones, cola o color se limpia
   y repinta el mismo bitmap si conserva tamaño; no se crea otro de un megabyte.
   La firma de contenido y los avisos siguen decidiendo cuándo hay que repintar.
2. **Fondo del reloj reutilizado.** Sus 34 filas se copian al mapa reservado, sin
   clonar una nueva imagen por cada cambio de contenido.
3. **Copia exacta de píxeles.** Fondo y restauración usan filas PArgb, sin volver
   a componer el alfa. Cada hilo reutiliza un pequeño arreglo por fila; no hay
   un arreglo grande nuevo por actualización.
4. **Reloj por recuadro.** Se borra y sube únicamente la unión del texto anterior
   y nuevo, con margen de antialias. El progreso y sus dígitos siguen actualizándose.
   Una revisión saltada exige subir el mapa entero, para no perder otros cambios.
5. **Portadas pertinentes.** Una portada precargada para otra página no obliga a
   reconstruir la firma de las tres o doce canciones visibles del estado.
6. **Textos ocultos.** No se suben especulativamente cintas de una barra del todo
   oculta. Al comenzar su transición se prepara ese grupo; lo visible conserva su
   subida inmediata. No se suprimen títulos ni artistas largos.
7. **Gestión explícita de la textura mutable.** Se conserva la reutilización GPU
   que ya existía y se comprueban tamaño, referencia y revisión antes del parche.
   Un fallo regional vuelve a la subida completa; un recurso ausente se reintenta.
   La textura del estado no se trata como una portada inmutable compartida.
8. **Reflejo totalmente cubierto.** Con el estado opaco ya abierto y actualizado,
   no se calcula ni envía un reflejo que cae entero bajo él. Si sobresale por
   arriba se conserva. El mando para quitar el reflejo queda en gris cuando ya
   está cubierto; conserva su valor para volver al estado transparente.

La versión anterior **ya reutilizaba la textura GPU** de igual tamaño: no se
atribuye a este cambio un ahorro nuevo de creaciones de textura. Sí desaparecen
creaciones repetidas de los mapas GDI+ y se reduce el área de los parches.

## Seis interruptores nuevos

En **Ctrl+F3 → Imagen → Performance**, con guardado inmediato e independientes.
Vienen apagados al cargar una configuración que no los tenía. **Solo Performance**
los enciende conservando los niveles actuales de efectos; los presets Bajo, Medio
y Performance también los incluyen. Alto y el restablecimiento los apagan.

| Opción | Cambio visual y ahorro |
|---|---|
| Estado extendido opaco | Fondo sólido en vez de transparentar el cielo. Con el panel completamente abierto y su textura actualizada, se recortan las capas de fondo que ya soportaban huecos y el estirado del cielo intermedio. Durante la transición se conserva el fondo completo. |
| Aurora con menos columnas | Mitad de segmentos en relleno, contorno y horizonte. Sigue representando las ocho bandas y todo el ancho, con algo menos de detalle espacial; no reduce la cadencia del render ni el análisis de sonido. |
| Sin reflejo de aurora en reposo | Omite ese reflejo secundario con el estado abierto. Conserva la aurora principal. |
| Sin estela de estrella en reposo | Omite sus copias durante el reposo; conserva la estrella 2D/3D, giros, desplazamiento, gestos y ayuda. Las estelas vuelven al navegar. |
| Corona de estrella compacta | Reduce los dos halos grandes. Conserva cuerpo, polvo, animación y destello; también se aplica fuera del reposo. |
| Color directo del estado extendido | Aplica de una vez el color final de la canción al panel. Evita sus repintados intermedios; el cielo y los demás elementos mantienen sus transiciones. |

Los mandos sin utilidad en la configuración actual quedan en gris y explican su
requisito. El mando anterior de transparencia también queda en gris mientras
se usa el estado opaco, sin perder su valor guardado. La lista se desplaza dentro de un área propia, manteniendo visibles las
pestañas Efectos/Fluidez/Performance y accesibles los diez ahorros en 1024×768.

## Medir en la máquina real

El medidor de Imagen añade **Estado extendido** si hay una canción de audio.
Espera a que termine su entrada antes de contar y exige que realmente se haya
abierto. Si ya sonaba música conserva esa canción. No cambia ni guarda la
preferencia de estado extendido para medir; restaura el destino anterior incluso
si falla la medición. Puede tardar unos minutos al comparar todos los ahorros.

Con `diagnostico=1`, `rocola_diagnostico.txt` contiene por escenario p95, p99 y
máximo de intervalos entre inicios de cuadro, con pendientes de repaso/miniaturas.
Ctrl+F1 muestra el contador y habilita el diagnóstico detallado de fluidez del
render; el archivo requiere además tener encendido el diagnóstico. Los fallos de
presentación invalidan la medida; no se cuentan como cuadros aceptados. Contadores
GC coincidentes no prueban la causa ni la duración de una pausa.

Para comparar con música, usar una copia con datos aislados mediante
`ROCOLA_DATOS`, conservar la misma canción, resolución y opciones, y comprobar si
siguen activos repaso o miniaturas en ambos casos. No entrenar IA, compilar ni
ejecutar pruebas HAL durante esa comparación. Probar navegación y estado extendido
con estrella 2D y 3D, incluyendo entrada/salida y la ayuda sin rostro.

## Verificación reproducible

`pruebas/validar-estado-extendido.ps1` compila GPU/CPU con el compilador .NET 3.5 y
advertencias como errores; usa datos temporales y ejecuta las regresiones sin
instalar el programa. `todo.sh estadoeficiente` registra también la batería nueva.

- `estadoeficiente`: reutilización sin residuos, píxeles/alfa, cambio de tamaño,
  reloj largo→corto, subidas completas/regionales y revisión saltada, oclusión solo
  tras subir la textura opaca, persistencia e independencia de los seis mandos,
  número/ancho de vértices y acceso a los diez interruptores desplazándose.
- `performance`, `rendercache`, `franjareloj`, `panelreposo`, `fluidezcuadros`,
  `estrellaflujo`, `estrella3d`, `estelaestelar`, `ajustesvivos`, `fichaportada`,
  `caratulasjpeg`, `hojeofino` y `papeleraportadas` cubren las rutas relacionadas.
- Se actualizan las pruebas que esperaban el parche de toda la tira, una página
  sin desplazamiento o desconocían los requisitos de los mandos nuevos. Conservan
  sus verificaciones positivas, negativas, de límites y de valores guardados.

`medirestado.cs`, junto a `franjareloj.cs` y `leercodigo.cs`, permite comparar con
el ejecutable exacto anterior en procesos separados. La rejilla de navegación
se mantiene estática; las pruebas de hojeo verifican los cambios de página aparte.
Imita el controlador y presentación; usa datos gráficos sintéticos, ninguna canción ni dispositivo de
audio. Informa cuadros aceptados/fallidos, mapas/texturas creados e intervalos
p50/p95/p99/máximo de esas llamadas. Son medidas del trabajo sintético en el host,
**no FPS, latencia del monitor ni resultados de un AMD A4 o Windows XP**.

En la comparación con `e38f583`, las trece actualizaciones de contenido de cada
caso extendido crean trece mapas antes y cero nuevos después; las texturas ya se
reutilizaban en ambos. El modo de aurora ligera emite exactamente la mitad de
vértices de relleno y brillo. A 1024×768, el estado ocupa 245 filas: el recorte
opaco puede evitar 243 filas de cada capa recortable (31,6 % de la pantalla), no
del coste total del cuadro. La corona pasa de radios 1,9/1,2 a 1,05/0,78: sus dos
quads juntos cubren alrededor del 34 % del área anterior.

Resultados de una pasada secuencial, sin compilaciones ni otras pruebas en paralelo,
para los casos extendidos (360 intervalos; todas las presentaciones artificiales
aceptadas):

| Versión | Estrella | p95 ms | p99 ms | Máximo ms |
|---|---|---:|---:|---:|
| `e38f583` | 2D | 0,103 | 1,912 | 2,622 |
| Automáticos nuevos | 2D | 0,107 | 1,751 | 2,520 |
| Automáticos y opciones | 2D | 0,125 | 1,761 | 2,519 |
| `e38f583` | 3D | 0,118 | 1,873 | 2,264 |
| Automáticos nuevos | 3D | 0,118 | 1,782 | 2,172 |
| Automáticos y opciones | 3D | 0,142 | 1,990 | 3,778 |

Estos tiempos **no demuestran una mejora uniforme de fluidez**: con opciones el
máximo sintético 3D fue mayor. El controlador no ejecuta el trabajo gráfico real,
y cada pasada es corta. Lo verificado es la reducción de mapas y geometría y la
conservación de las rutas probadas; el ahorro real requiere el equipo de destino.
Las salidas completas de la comparación van en `MEDICION_SINTETICA.txt` del ZIP.

La compilación GPU y CPU terminó sin advertencias. Pasaron las catorce baterías
GPU relacionadas de arriba y tres comprobaciones CPU (`estadoeficiente`,
`ajustesvivos` y `fichaportada`). La validación del ejecutable distribuido queda
registrada en `VALIDACION.txt` del ZIP. El script PowerShell se proporciona para
repetirla en Windows; no se ejecutó en esta sesión.

La ejecución de esta sesión es Linux/Mono con referencias oficiales de .NET 3.5;
el runtime disponible es CLR 4. Las pruebas de controlador artificial usan un
mapeo local de `RtlMoveMemory` a `memcpy`, que no se distribuye con el programa.
No hay prueba de GPU real, escucha ni ejecución en XP en esta sesión. Las pausas
de segundos del equipo real quedan pendientes de comprobar con el compilado y
su registro más preciso; estas pruebas no permiten prometer su desaparición.
