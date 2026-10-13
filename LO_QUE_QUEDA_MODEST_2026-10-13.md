# Lo que queda por mejorar, medido — Modest, 13 de octubre de 2026

Petición del usuario: «Busca más mejoras».

Esta ronda es de medir y buscar. No cambia nada del programa.

**Actualización:** el usuario eligió las dos, y están hechas; ver
[REPASO_Y_PARED_LIGEROS_MODEST_2026-10-13.md](REPASO_Y_PARED_LIGEROS_MODEST_2026-10-13.md). Las cifras son de esta máquina (Linux, Mono), no del
equipo del bar.

## Lo que ya va ligero (no merece más trabajo)

| Qué | Medido aquí |
| --- | --- |
| Toda la cadena de sonido, de fábrica | 19 ms por segundo de música (un 2 % de un núcleo) |
| Que empiece a sonar una canción (abrir, armar la cadena y los primeros 100 ms) | unos 5 ms |
| El repaso de una canción WAV, la memoria que pide | 64 KB (2 MB si cosecha para la IA, que es lo que guarda) |
| Leer un WAV entero de 3 min 50 s | 0,17 s de los 1,8 s del repaso a fondo |
| El fondo Legacy, el procesador por cuadro | 0,14 ms (ronda del 12 de octubre) |

Lo más caro de la cadena es el nivelado con el realce (7,5 ms por segundo), y ya está escrito a mano. Le sigue el
limitador de picos (3,4 ms).

## Probado y descartado

- **El limitador de picos con su filtro escrito a mano**, como el realce. Aquí ahorra un 10 % del limitador, y solo
  con música fuerte. En XP el procesador calcula con otra unidad, con pocos registros, y podría salir más lento. No se
  puede medir aquí, así que no lo dejé.

## Para que elijas

### P1. Leer los MP3 del repaso sin dejar basura — recomendada

El repaso de la biblioteca lee cada MP3 con la librería de audio (NAudio). Esa librería crea objetos nuevos por cada
cuadro del archivo (unos 38 por segundo de canción) y los tira enseguida. Medido aquí, con un descompresor de mentira,
porque en Linux no está el de Windows:

| Canción de prueba | Al abrir | Al leerla entera |
| --- | --- | --- |
| 200 s a 192 kbps | 1,3 MB | 5,8 MB |
| 215 s a 320 kbps | 1,4 MB | 9,6 MB |

Todo lo demás del repaso pide 64 KB por canción. O sea, **más del 99 % de la basura del repaso sale de ahí**. Esa
basura obliga al recolector de memoria a pasar a menudo mientras dura el repaso, y cada pasada detiene un momento
todos los hilos, también el de la pantalla.

**Lo que haría:**

- un lector propio de los cuadros MP3, solo para el repaso, con su memoria reservada una vez;
- le pasa al mismo descompresor de siempre los mismos cuadros, en el mismo orden;
- la reproducción no cambia;
- ante cualquier archivo raro que no entienda igual que NAudio, vuelve a NAudio.

**Cómo se comprobaría aquí:** con un descompresor que apunta todo lo que recibe, los mismos cuadros, byte a byte, que
con NAudio, en archivos normales, con y sin etiquetas, VBR, mono, 22 kHz y archivos dañados. Y los valores medidos,
idénticos.

**Lo que no se puede prometer:** que quite tirones. Las pausas largas medidas el 10 de octubre coincidían con
recolecciones de memoria, pero coincidir no prueba que las causen. Lo seguro es que el repaso deja casi 100 veces
menos basura. Si los tirones bajan, solo se sabrá en el equipo del bar.

**Riesgo:** medio. Es un lector nuevo para todos los MP3 de la biblioteca. Lo compensa la vuelta a NAudio y que se
compara cuadro a cuadro.

### P2. No pintar el cielo debajo de las portadas

Pendiente de la ronda del fondo del 12 de octubre. Es lo que más trabajo le quitaría a la tarjeta: en la pared, las
portadas tapan buena parte de la pantalla, y debajo se pintan antes casi seis capas del fondo. Hay que comprobarlo en
la tarjeta del bar, porque aquí no hay Direct3D.
