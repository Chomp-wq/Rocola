# Uso de recursos de Modest (10-10-2026)

Solo se trabaja en `claude/modest-carson-fa2v2n`. HD queda suspendida por
decision del usuario. Modest mantiene .NET 3.5 y el destino Windows XP; el
renderizador CPU permanece congelado. No se cambian efectos, calidad final,
musica, pesos de los modelos ni preferencias visuales.

## Cambios automaticos

| Apartado | Cambio | Proteccion conservada |
| --- | --- | --- |
| Corpus IA / RAM | Escritura por bloques con dos buffers reutilizables de 40 KiB; sin copiar el lote entero a otros dos arrays | Orden y bits de los ejemplos, CRC incremental, origen y confirmacion durable |
| Corpus IA / disco | Limite comprobado con el tamaño real dentro del mismo candado de escritura | Se acepta o rechaza el lote completo, sin escribir un lote parcial |
| Lectura MP3 flotante | Consumir y compactar sobrantes en su buffer; reservar solo si aumenta la capacidad necesaria | Mismo PCM, orden, reset, fin y fallback NLayer/ACM |
| Apertura MP3 para medir | Omitir el ID3 inicial validado mediante una vista seekable; no cargar dos veces la caratula incrustada | Mismo descompresor y ruta para su guardia, PCM, duracion y saltos; reproduccion/etiquetas intactas |
| Planes de biblioteca / RAM | Soltar la referencia del formulario al terminar, cancelar o fallar el lanzamiento | No borrar tablas compartidas ni retirar un plan nuevo desde un final antiguo |
| Guardados / CPU | Retirar el prefijo confirmado de la cola con un solo desplazamiento | Conservar llegadas durante la escritura y no retirar nada si el guardado falla |
| Respaldos / disco | Quitar WriteThrough por cada bloque del archivo privado `.pending` | FlushFileBuffers final obligatorio, hash leido del destino y publicacion posterior |
| Recuperacion de anexos | Buscar el ultimo salto de linea en bloques inversos de 64 KiB | Mismo punto de corte; deteccion de lecturas cortas/truncado |
| Ruido del cielo / GPU | Agrupar las teselas en un envio y reutilizar la malla de la resolucion vigente | Mismos vertices, UV, colores, orden y textura Legacy/Natural |
| Caratulas retiradas / GPU | Sacarlas por el extremo de la lista, sin copiarla entera; hasta 32 por tick y presupuesto aproximado de 1 ms | Proteger caratulas de cubos, relevos, transicion, cola y artistas del buscador |

El presupuesto de limpieza se comprueba **entre** imagenes: no puede interrumpir
un Dispose individual de GDI+. No es un limite absoluto de duracion de cuadro.
Una imagen aun usada mantiene la politica anterior de propiedad/GC; no se añade
otra cache creciente de imagenes protegidas.

La malla del ruido pasa de 8 a 1 envios a 3440x1440, de 12 a 1 en 4K y de 40 a 1
en 8K. Estos son recuentos de envios, no multiplicadores de FPS. Se redimensiona
al tamaño actual y se suelta al liberar la GPU, sin retener el maximo historico.

El limite del corpus es atomico **por lote**, no una transaccion de toda una
cancion estereo: sus canales siguen entrando por llamadas separadas. No se
cambia el formato que consumen los entrenadores ni se añade Python a Modest.

Estas mejoras eliminan trabajo redundante, no ponen un techo nuevo al equipo
potente ni aumentan los carriles de equipos pequeños. No se añaden interruptores
de optimizacion y no se fuerza GC desde produccion.

## Validacion reproducible

`pruebas/validar-recursos-modest.ps1` compila y ejecuta nueve baterias con el
compilador .NET 3.5, advertencias como errores, tanto AnyCPU como x86. Acepta una
ruta opcional a un ejecutable anterior real para la comparacion A/B; sin ella
declara explicitamente que solo comprueba contratos y reutilizacion.

| Bateria | Comprobaciones correctas por arquitectura |
| --- | ---: |
| corpusacotado | 65 |
| discoacotado | 98 |
| prefijosguardados | 7 (incluyen 3.000 colas aleatorias) |
| flotantesobrantes, A/B real | 23.453 |
| planbiblioteca | 31 |
| arranqueeficiente | 31 |
| ruidomalla | 114 |
| papeleraportadas | 29 |
| mp3medidaligera, A/B real | 798 |

Incluye concurrencia, reintentos, limite real, corrupcion, lecturas cortas,
preservacion de duplicados y referencias usadas. El test de planes verifica
con WeakReference y GC **solo de prueba** que las capturas pueden recogerse;
la ruta de fallo de Thread.Start se revisa estructuralmente, no se induce un
agotamiento real de hilos.

Pasaron tambien las 67 baterias del runner de capas, con HAL omitido
explicitamente en esa pasada, y la equivalencia de memoria de analisis anterior
(147 controles) e integrada (284), nativo y x86. El comparador CPU confirma 14
metodos de dibujo con instrucciones, operandos, locales y clausulas de excepcion
identicos a la compilacion previa. Compilacion Release con advertencias como
errores correcta.

Suite de audio completa: 38 baterias C#, 71 pruebas Python de entrenadores y
contratos, formatos reales con FFmpeg y controles repetidos con afinidad de dos
procesadores logicos, correctos. Los WAV/MP3/WMA/M4A de prueba se crean en TEMP;
no se entrenan ni sustituyen los modelos instalados. Audio suave verifica
2.793.667 casos y audio estereo 360; no son valoraciones de escucha.

Las pruebas de PCM y corpus son bit a bit. La prueba de ruido compara vertices y
envios en una tarjeta simulada; por si sola no certifica los pixeles de HAL ni
el rendimiento real. Los tiempos de micropruebas de RAM no se presentan como
ganancia de FPS ni como latencia de un HDD.

Comprobacion adicional HAL real, antes/despues: 28 PNG deterministas identicos
por SHA-256 (24 laminas de estrellas subpixel y 4 de ruido aislado Legacy/Natural
a 1024x768 y 3440x1440). La bateria actual paso 5.649 controles HAL. Las escenas
completas incluyen relojes y fugaces, y no se usan sus hashes para afirmar
igualdad entre ejecuciones independientes. Se corrige la comprobacion antigua
que inspeccionaba `nieblaQuad`, ahora auxiliar de otra capa, para inspeccionar la
malla de ruido realmente enviada; ademas exige un envio con todos sus triangulos.

La apertura ligera solo se utiliza al medir. Valida diez bytes de cabecera y,
si existe, diez de footer; no crea arrays del tamaño del ID3. Conserva el ID3v1
final y el fichero abierto hasta cerrar el lector. Versiones/flags desconocidos,
syncsafe invalido, truncado, footer incongruente y cabecera extendida/compresion
vuelven al lector historico. No se intenta corregir el salto peculiar de NAudio
con flag 0x40. El constructor y Dispose liberan tambien la vista en los fallos.
La fixture compara cinco MP3 reales de prueba (mono/estereo, VBR, MPEG-2,
44,1/48 kHz), etiquetas de 2 MiB, v2.2/v2.3/v2.4/footer, lecturas fragmentadas,
seeks, fallos y archivo cerrado. Esto elimina reservas innecesarias comprobadas;
por si solo no demuestra que fueran la causa de los tirones.

## Medicion real antes/despues

Se ejecutaron secuencialmente las dos compilaciones a 3440x1440, pantalla de
180 Hz y tope de 200, en este PC (5700X3D / RTX 4070 SUPER). Cada una cubrio
Legacy/Natural, estrella 2D/3D y navegacion quieta/estado extendido: ocho escenas,
cinco segundos de estabilizacion y 25 segundos medidos por escena. Se utilizo
el bucle Application.Idle real, sin forzar cuadros; la misma cancion desde cero,
DSP real y salida muda al final de la cadena, sin cambiar el volumen de Windows.
Las dos copias de datos arrancaron con los mismos 15 archivos verificados;
solo en ellas se desactivo la cosecha. El repaso de biblioteca siguio activo.
No se ejecutaron otras pruebas, compilaciones ni la Rocola de produccion a la vez.

Ambas terminaron las ocho escenas sin errores ni fallos de presentacion. En la
compilacion nueva, la navegacion quieta rondo 180 cuadros presentados/s, sin
intervalos de 20 ms o mas en sus cuatro escenas; maximos entre 7,347 y 7,651 ms.
Esto no mide cambios rapidos de pagina ni los fotones de la pantalla.

Con el estado extendido y repaso activo, los resultados fueron mixtos:

| Escena extendida | Presentaciones/s antes -> despues | Maximo de sesion, ms antes -> despues | Intervalos >=20 ms antes -> despues |
| --- | ---: | ---: | ---: |
| Legacy 2D | 175,402 -> 167,293 | 41,695 -> 33,270 | 28 -> 85 |
| Legacy 3D | 171,128 -> 170,474 | 34,582 -> 33,166 | 59 -> 57 |
| Natural 2D | 173,015 -> 173,107 | 46,976 -> 31,937 | 43 -> 44 |
| Natural 3D | 174,178 -> 173,796 | 30,861 -> 33,093 | 38 -> 40 |

Los maximos del p99 de las ventanas recientes de 512 cuadros fueron,
respectivamente: 27,849 -> 32,378; 28,446 -> 29,062; 23,092 -> 24,843 y
25,576 -> 22,619 ms. No son percentiles de toda la sesion. El total de
intervalos >=20 ms en estado extendido fue 168 -> 226: **los tirones no estan
resueltos ni se demuestra una mejora global de fluidez**. El avance del repaso
no es identico entre ejecuciones: Legacy 2D midio 59501 -> 59337 pendientes en
la referencia y 59349 -> 59305 en la nueva; no se procesaban necesariamente los
mismos archivos en cada ventana. No se atribuyen las diferencias a una sola
optimizacion ni se oculta la fase desfavorable.

Todos esos 226 intervalos coincidieron con incrementos de GC. En los peores
intervalos de las tres primeras escenas nuevas, el tiempo queda principalmente
fuera de preparar/dibujar/presentar; en Natural 3D coincidio durante preparar.
No demuestra que la geometria o el movimiento de la estrella causen la pausa.
Los beneficios de buffers, referencias liberadas, menor IO y menos envios se
validan por sus contratos independientes, no mediante esta tabla de FPS.

## Limites y continuidad

Queda pendiente un perfil causal de las pausas durante el repaso de biblioteca,
incluida la apertura/lectura por cuadros MP3 y las reservas del analisis. La
inspeccion de NAudio muestra nuevas reservas por Mp3Frame.ReadNextFrame(true),
pero no se ha probado que causen estos intervalos; no se parchea la dependencia
ni se introduce un pool sin demostrar la propiedad y vida de sus buffers.
Una diferencia en los contadores GC no mide la duracion de una pausa ni demuestra
su causa.
El intento de traza CLR de esta sesion fue rechazado por Windows por falta de
elevacion: no se obtuvo una traza, no se modificaron permisos del sistema ni se
instalo un perfilador. No se resuelve esto cambiando Modest a CLR 4, deteniendo
el repaso para ocultarlo o reduciendo el aspecto del fondo.

No hay un equipo XP ni un CPU antiguo de 2 nucleos/2 hilos disponible. Una prueba
con afinidad o archivos artificiales no sustituye esos equipos ni una comparacion
controlada entre SSD y HDD.
La escucha a ciegas de Apollo de la pasada anterior queda en manos del usuario;
no se hace trabajo adicional en HD mientras este suspendida.

## Instalacion y recoleccion

Se respaldaron el EXE/PDB anteriores y se instalaron solo esos dos archivos en
`D:\Release1`. Respaldo recuperable:
`D:\Release1\respaldo_recursos_20261010_fa6073ad5a13474a947e493596d7514a`.
El manifiesto local conserva los hashes y la copia anterior/nueva. EXE probado
e instalado: SHA-256
`987DE6E1F087566248C8967CA1B0353895B8E3C2A8367262B1C4D71D923EF503`.

Antes de reabrir, se verificaron sin cambios los tamaños/fechas de 48 archivos
de datos, dependencias y entrenadores, y los hashes de ambos modelos, configuracion
y NAudio. No se sustituyeron musica, corpus, preferencias, pesos ni entrenadores.
El nuevo arranque registro una pausa de 1250 ms sin excepcion; la referencia ya
habia registrado 1328 ms. Son avisos aislados del guardian, no una comparacion
controlada de latencia ni un arranque instantaneo certificado.

Se reabrio la produccion, se activo el repaso a fondo desde Sonido y se comprobo
`ia_datos=1`, el estado "Repasando..." y crecimiento real del corpus y su indice.
Se deja la Rocola activa recolectando; no se inicia un ajuste de pesos de IA ni
un entrenador Python paralelo. La ventana Sonido permanece abierta: el control
de Windows pierde el foco al intentar cerrarla. El usuario puede pulsar Listo
sin detener el repaso; no se cierra la Rocola ni se fuerza su terminacion.
