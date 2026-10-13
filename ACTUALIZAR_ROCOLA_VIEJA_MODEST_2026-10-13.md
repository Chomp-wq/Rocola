# Poner esta versión en una rocola con una versión vieja — Modest, 13 de octubre de 2026

Petición del usuario: «Voy a meter esta actualización a la rocola, la rocola tiene una versión vieja. Revisa que esta
versión sea compatible con versiones viejas y que, de ser necesario, convierta las cosas viejas a las nuevas».

## Resumen

**Es compatible.** Revisé cada archivo que guardan las versiones anteriores, desde la primera importada (24 de junio)
hasta hoy.

- **Lo que importa se lee tal cual:**
  - lo más sonado;
  - las monedas y los créditos cargados;
  - los botones del mueble;
  - los créditos y la cola pendientes;
  - las medidas de volumen y del ecualizador;
  - el material para la IA.
- **La configuración se convierte sola** al formato nuevo.
- **Lo que no sirve se descarta sin romper nada**: cachés viejas y claves de funciones que ya no existen. Las cachés se
  vuelven a hacer solas.

Arreglo de esta ronda: durante un día (10 de agosto) la fuerza de la aurora se guardó como `fondo=`, y no se leía. Ahora
se lee si no está la clave de hoy, `espectro=`.

## Cómo actualizar

1. **Cierra la rocola vieja** con su forma normal de cerrar, para que guarde los créditos, la cola y lo más sonado.
2. **Haz una copia de la carpeta** del programa entera, por si acaso. Lo valioso son los `rocola_*.txt`, los
   `rocola_*.bin` y la carpeta `ia`.
3. **Copia los archivos nuevos encima**, en la misma carpeta: `Rocola.exe` y `NAudio.dll`. **No borres** ningún
   `rocola_*.txt` ni `rocola_*.bin`. La versión nueva los busca en la carpeta del programa, igual que la vieja, salvo que
   esté puesta la variable `ROCOLA_DATOS`. La música se sigue buscando igual: una carpeta `musica` junto al programa o
   hasta cuatro carpetas más arriba, más las que se hayan añadido con Ctrl+F2.
4. **Ábrela.** La primera vez:
   - **reescanea la música** si la caché de la biblioteca es de una versión anterior al 2 de julio, o si las carpetas
     cambiaron. Tarda lo que tarde en recorrer los discos, una sola vez. Las cachés desde julio tienen la misma forma y se
     aprovechan;
   - **rehace las miniaturas de las portadas.** Las de las versiones anteriores al 25 de agosto estaban en la carpeta
     temporal de Windows; ahora van en `rocola_miniaturas`. Las primeras páginas tardan un poco más en tener portada;
   - **sigue midiendo las canciones** que falten, como siempre. Lo ya medido se aprovecha.
5. **La tarjeta gráfica viene encendida.** Una configuración vieja sin `gpu=` arranca con el dibujo por tarjeta. Si la
   tarjeta no puede, la rocola vuelve sola al dibujo de siempre. Para forzarlo, `gpu=0` en `rocola_config.txt`.

## Lo que se revisó, archivo por archivo

| Archivo | Qué guarda | Qué pasa con el de una versión vieja |
| --- | --- | --- |
| `rocola_config.txt` | los ajustes | Se lee y al guardar sale en el formato nuevo. Nieblas, fugaces, patrón y animación pasan de `0/1` a `bajo/alto`. La aurora `fondo=` del 10 de agosto se lee (arreglado hoy). Las claves de cosas retiradas se ignoran y no se vuelven a escribir: sonidos de la rocola, recorte del silencio, vinilos, Espacio natural, cachés sin interruptor. |
| `rocola_stats.txt` | lo más sonado, monedas, créditos cargados | Mismo formato (`ROCOLA-STATS v1`) desde el primer día. Se lee y se guarda igual. |
| `rocola_teclas.txt` | los botones del mueble | Mismo formato. La versión vieja solo guardaba teclas sueltas y distintas, que es lo que la nueva pide: **no hay que reconfigurarlos.** |
| `rocola_estado.txt` | créditos, canción y cola pendientes | Mismo formato. Si el archivo viejo no trae la hora, se usa la fecha del archivo. La posición vieja (`pos=`) se ignora y la canción vuelve a empezar. |
| `rocola_volumen.txt` | volumen medido de cada canción | Se lee también el formato de julio, sin duración. |
| `rocola_eq.txt` | huella de tono de cada canción | Se leen las huellas de julio (8 bandas). |
| `rocola_duracion.txt`, `rocola_afondo.txt`, `rocola_ia_hechas.txt` | listas de lo ya hecho | Listas de rutas: se leen igual. |
| `rocola_ia_datos.bin` | material para entrenar la IA | Misma cabecera desde agosto. Si viene sin su índice (`.idx`, de antes del 22 de septiembre), se cuenta y se le sigue añadiendo **sin tocar lo de antes**. |
| `rocola_biblioteca.txt`, `rocola_letras.txt` | cachés | La de la biblioteca de junio (v1) no se usa y se reescanea; la de julio en adelante (v2) se aprovecha si las carpetas no cambiaron. Las letras de formato viejo se rehacen solas. |
| miniaturas | cachés | Las viejas (carpeta temporal) no se usan: se rehacen. |

## Cómo se comprobó

- **`compatvieja`** (nueva, 18 comprobaciones; pasa con y sin tarjeta). Escribe los archivos tal como los dejaban las
  versiones anteriores (copiados de su código en el historial) y la versión de hoy los lee:
  - la configuración de agosto, con los valores convertidos, guardada en el formato nuevo y releída;
  - lo más sonado y las monedas de la primera versión, guardados y releídos;
  - los botones;
  - el volumen y el ecualizador de julio;
  - la caché de biblioteca de junio, que no se usa, y la de julio, que se aprovecha;
  - el material de la IA de agosto sin índice, al que se añade sin tocar lo de antes.
- **`apagon`** (créditos y cola tras un corte), **`ajustes`**, **`ajustesfluidez`**, **`noche`**, **`colores`** y
  **`detalles`** siguen pasando.

**No probado aquí:**

- **La rocola del bar de verdad, con sus archivos.** Por eso conviene el paso 2, la copia de la carpeta.
- **Windows XP.**
