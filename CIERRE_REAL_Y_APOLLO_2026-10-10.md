# Cierre de instalacion y pruebas reales (10-10-2026)

Esta pasada termina la instalacion pendiente de Modest y el paquete coherente de
Apollo de HD, y ejecuta las comparaciones reales pendientes. **No declara resuelto
el tiron de Modest con repaso activo**: las mediciones lo reprodujeron.

CPU congelado; Modest sigue dirigido a .NET 3.5/XP y HD a .NET 4.8. No se cambio el
modelo, trayectoria, colores, calidad de caratulas ni cadena de reproduccion en
esta pasada. No se añadieron preferencias de rendimiento.

## Defectos de los instrumentos corregidos

- La prueba real de estrella confundia una ficha PlayItem con su Track y no
  preparaba la ficha de reproduccion. Fallaba antes de medir. Ahora abre la misma
  pista del disco real, aplica su paleta, conserva el DSP y comprueba la salida
  real Playing despues del calentamiento. El sonido se silencia al final de la
  cadena; no se cambia el volumen de Windows.
- Se agrega --con-repaso como modo EXPLICITO del instrumento, no como ajuste del
  programa. Exige pendientes y trabajadores vivos. Sin esa opcion sigue esperando
  pendientes cero y trabajadores terminados. Cambiar de estado invalida la escena.
  Los contadores sumados son obligaciones de varias tandas, no canciones unicas.
- En HD, 00_verificar.py enviaba silencio a Apollo: el atajo de silencio podia
  evitar la red neuronal. Ahora exige una señal estereo no silenciosa y salida
  real, finita, completa y no nula. Tres pruebas unitarias vigilan esa guardia.

Autoprueba de estrella: 43 controles correctos por rama, repetidos en proceso
nativo y x86, con su compilador correspondiente y advertencias como errores.
No son mediciones FPS. No se modifico el decodificador externo sin una comparacion
que demostrase que sus reservas son la causa.

## Comparacion GPU con musica real

Ryzen 7 5700X3D, RTX 4070 SUPER, Windows actual, 3440x1440, pantalla 180 Hz y tope
200. Ocho escenas por rama y escenario: Legacy/Natural x estrella 2D/3D x
navegacion quieta/estado extendido. Cinco segundos de calentamiento y 25 segundos
medidos por escena. Application.Idle y temporizador reales, sin cuadros forzados.
32 escenas terminadas, cero errores del instrumento y cero fallos de Present.

Datos aislados en TEMP; musica original de solo lectura, ia_datos=0 SOLO en las
copias. Modelos pequeños iguales a los instalados, sin copiar el corpus grande.
Ramas probadas por separado, sin otras Rocola, HAL, compilaciones, Torch o
entrenamiento concurrentes. Estas son pruebas de animacion, NO de pagina/disco
frio, latencia de teclado fisico, fotones ni prueba de XP.

Con la biblioteca pequeña de 17 pistas y repaso realmente terminado:

- Modest: 179,791–180,048 presentaciones/s; mayor intervalo 19,871 ms.
- HD: 179,876–180,051 presentaciones/s; mayor intervalo 13,931 ms.
- Ninguna escena registro episodios de 20 ms o mas.

Con la copia completa de biblioteca y repaso normal pendiente/vivo:

| Escena extendida | Rama | Presentaciones/s | Mayor p99 de ventana (ms) | Maximo de toda la fase (ms) | Episodios >=20 ms | Coincidencia GC |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Legacy 2D | Modest | 175,857 | 27,921 | 34,386 | 27 | 27 |
| Legacy 3D | Modest | 174,424 | 21,252 | 34,696 | 35 | 34 |
| Natural 2D | Modest | 173,780 | 23,489 | 35,331 | 32 | 32 |
| Natural 3D | Modest | 169,501 | 29,067 | 61,598 | 66 | 65 |
| Legacy 2D | HD | 179,909 | 7,001 | 11,338 | 0 | 0 |
| Legacy 3D | HD | 179,796 | 6,760 | 15,294 | 0 | 0 |
| Natural 2D | HD | 179,438 | 7,002 | 33,438 | 1 | 0 |
| Natural 3D | HD | 179,843 | 7,002 | 19,425 | 0 | 0 |

Los p95/p99 pertenecen a ventanas recientes de 512 cuadros, NO a toda la sesion;
el maximo y los episodios si cubren todos sus intervalos. No sumar maximos de
fases independientes para explicar un cuadro. El umbral adaptativo en estas
escenas fue 20 ms.

En navegacion quieta hubo cero episodios en ambas ramas. Se mantiene actividad
sin pasar pagina; el propio programa espera 8 s de reposo antes de analizar.
Los hilos permanecieron vivos con pendientes, pero estos no disminuyeron durante
la navegacion. En extendido si avanzaron: Modest de 59.633 a 59.350 y HD de 59.484
a 59.347 entre el inicio de la primera fase extendida y el final de la ultima.
No se pauso el repaso desde el instrumento.

Modest registro 160 episodios en sus cuatro fases extendidas; 158 coincidieron
con cambios de contadores GC. Esto **no mide la duracion del GC ni demuestra su
causa**. En el peor intervalo Natural 3D (61,598 ms), sus propias fases fueron
60,369/0,458/0,239 ms (preparar/dibujar/presentar) y 0,532 ms fuera del render,
con deltas GC 6/1/0. En HD, la pausa aislada de 33,438 ms no coincidió con GC.

La diferencia de runtime, las reservas del lector y otras tareas de repaso siguen
siendo hipotesis. No se hizo una traza causal de reservas/pausas. No se oculta este
resultado deteniendo el analisis, forzando GC, cambiando Modest a CLR 4 o reduciendo
la calidad del fondo. Tampoco se promete ausencia absoluta de pausas de Windows.

## Apollo real, solamente HD

- Paquete coherente: 108 pruebas Python correctas, incluyendo FFmpeg real para
  remuestreo y casos simulados de inferencia/fallos. Esas pruebas no son escucha.
- Verificador no silencioso: inferencia real CUDA correcta antes y despues de
  instalar, con el checkpoint oficial fijado, desde su cache existente.
- Revision 0b123a4606d5345d9082f6f633d8bfebe84c8e79.
- Checkpoint SHA-256:
  99d9af7f1ff20e63c393035513a655392818d66b4d7fc23d658175c1f15e8d76.
- Integracion opcional pruebas/apollosesionreal.py: fragmento real de 12 s
  degradado CONTROLADAMENTE a 22,05 kHz para obligar una inferencia, tres llamadas
  reales a la red, mezcla manual 70%, remuestreo soxr-p28.
- Publico cache WAV float32 estereo 44.100 Hz, 529.200 muestras por canal,
  longitud exacta, todas finitas, sin recompresion MP3 y entrada original con
  SHA-256 sin cambios. La fase de procesar/publicar tardo 2,300 s en este PC;
  NO incluye arranque del proceso/carga del modelo ni generaliza a otros equipos.
- Par X/Y a ciegas, misma RMS y margen comun sin recorte, WAV flotante. La clave
  queda separada. No se ha evaluado preferencia audible ni recuperacion fiel
  de informacion perdida.

No se cambiaron los pesos, venv, codigo externo Apollo, cache de usuario ni
modelo.txt. No se activó Apollo automaticamente en la configuracion. Modest no
recibe dependencias CUDA/Python ni este paquete exclusivo de HD.

## Instalacion recuperable

Modest se cerro normalmente con autorizacion explicita, sin matar el proceso.
Se sustituyeron SOLO EXE/PDB, con respaldo anterior/nuevo:

D:\Release1\respaldo_cierre_20261010_37b1b838d2e743cca5636d912cf40045

Instalado, Release y respaldo nuevo coinciden por SHA-256:

- Modest EXE: 7B1A192ED546683DAF39C7DCB1D85F6C9C24293ACE55EB93C288AFE4A2B5FB8E.
- Modest PDB: 3804BE8A8B72D914297C149339E5CC8B3D8313433BC4DF244DFCD9901C112CDF.
- HD EXE: 0F9FE70539C5BE28239CDB51ABABB469AEECD6C2F66C9FEFCE0048E6A78F86B9.
- HD PDB: D3DD3C3034C57CDABE40FCA657C31C8876C07E72E087959B57156173455F29F8.

Modest: configuracion/modelos/NAudio con sus hashes conservados; 35 archivos
restantes de raiz y 12 archivos IA con tamaño/fecha sin cambios al instalar.
Musica y corpus de entrenamiento no se reemplazaron ni copiaron a las pruebas.

HD conserva la instalacion EXE/PDB anterior y agrega el paquete coherente de
35 archivos Apollo (.py, .bat, LEEME y ejemplo), con copia de anterior/nuevo y
manifiesto SHA-256 en:

D:\ReleaseHD\respaldo_apollo_20261010_9a681e66b2654de59a9a3e332a42cacc

Siete archivos protegidos (EXE/PDB, configuracion, dos modelos, NAudio y
modelo.txt) conservaron SHA-256; 25.347 archivos IA fuera del paquete conservaron
tamaño/fecha. No usar una sincronizacion recursiva de Release para instalar.

## Estado abierto, no confundir con terminado

Modest se reabrio y, mediante Ctrl+F3 -> Sonido, se activo Repasar la biblioteca
a fondo. Juntar material para la IA ya estaba encendido y se conservo. Se verifico
el boton Detener el repaso a fondo y el mensaje Repasando la biblioteca a fondo;
el progreso paso de 47.769 a 48.068 de 62.872 archivos y de 71.013.675 a 71.267.675
ejemplos tras activarlo. El corpus conserva su nombre/indice y sigue creciendo.
Durante la reapertura (incluyendo el repaso normal previo al clic), el .bin paso
de 11.359.739.216 a 11.402.828.016 bytes y el .idx de 1.397.200 a 1.404.304 bytes.
No se confundio la recogida de ejemplos con ajustar ni instalar un modelo nuevo.
Se deja el programa abierto; no se mantiene un proceso de pruebas concurrente.

1. Corregir las pausas de Modest con repaso activo, usando un perfil causal y
   pruebas comparables antes/despues. La cache de estela no bastó para eliminarlas.
2. Escucha a ciegas Apollo por el usuario; la comprobacion numerica no la sustituye.
3. Prueba fisica en XP, HDD y CPU antiguo de 2 nucleos/2 hilos; no disponibles.
4. La recoleccion a fondo solicitada se deja trabajando en la instalacion Modest.
   Esto no entrena ni activa por si solo un modelo nuevo. No esperar a completar
   toda la biblioteca para devolver el control al usuario.
