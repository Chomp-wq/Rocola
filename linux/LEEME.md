# La rocola en Linux: un solo paso

Antes esto eran **cinco pasos**, y dos de ellos había que saber lo que se estaba haciendo: instalar
Mono, libgdiplus, mpv y ffmpeg a mano *adivinando los nombres de los paquetes*, compilar, armar un
`.sfs`, entrar al BootManager de Puppy a marcar un archivo, reiniciar, y recién ahí correr otro guión.

Ahora es **uno**:

```
sh linux/instalar.sh
```

Sirve igual en **BookwormPup64 (Puppy)** y en **Pop!_OS** — y de paso en cualquier Debian o Ubuntu,
que es lo que Pop!_OS es por debajo.

---

## Antes de nada: mirar sin tocar

```
sh linux/instalar.sh --probar
```

Escribe **lo que haría** —dónde pondría el programa, dónde la música, qué le falta a la máquina y qué
paquetes instalaría— y **no toca ni un archivo**. Sirve para decidir con la información delante en vez
de a ciegas.

## Los tres modos

| | qué hace |
|---|---|
| `sh linux/instalar.sh` | instala |
| `sh linux/instalar.sh --probar` | dice lo que haría, sin tocar nada |
| `sh linux/instalar.sh --quitar` | lo deshace, **sin tocar la música ni lo aprendido** |

Al final pregunta si además quiere el **modo bar** (la máquina enciende y aparece la rocola). Para no
tener que contestar: `--modo-bar` lo pone, `--normal` no lo pone.

---

## Lo que hace, por dentro

1. **Reconoce la máquina**: Puppy, familia Debian (Pop!_OS, Ubuntu, Mint…) u otra.
2. **Mira qué le falta**, lo enseña en una lista y pide permiso **una vez** antes de instalarlo.
   - Si la máquina no trae `apt` —algún Puppy—, **se planta y escribe los nombres exactos** que hay
     que buscar en el Package Manager, en vez de dejar algo a medias.
   - **mpv y ffmpeg no bloquean**: sin ellos la rocola funciona igual, sólo que **sin sección de
     vídeos**. Lo avisa, para que sea una decisión y no una sorpresa.
3. **Compila la rocola**, con las mismas fuentes que usa la tanda de pruebas — no hay dos listas que
   puedan discrepar: la saca de `pruebas/todo.sh`.
4. **Comprueba contra el programa recién hecho** que de verdad no falte ninguna biblioteca, **antes**
   de instalar nada. Esto es lo que evita dejar una máquina que enciende en negro.
5. **Lo deja puesto**: el programa, sus bibliotecas, el modelo de la IA, un mandato `rocola` y un
   icono en el menú.
6. **Crea la carpeta de datos y la de música**, para que no haya que adivinar dónde va nada.

---

## Dónde va cada cosa, y por qué cambia

|  | Puppy | Pop!_OS y compañía |
|---|---|---|
| **El programa** | `/opt/rocola` | `/opt/rocola` con root, `~/.local/opt/rocola` sin él |
| **La música y los datos** | `/mnt/home/rocola` | `~/Rocola` |

**En Puppy los datos van a un disco de verdad.** En una instalación frugal la carpeta del usuario vive
*dentro* del archivo de guardado, y lo que se escribe ahí se queda en la memoria hasta que Puppy
guarda: **un apagón se llevaría los créditos y la cola de la noche**. Con `/mnt/home` se escribe en el
disco de verdad y al volver la luz la rocola sigue donde iba.

**Y en Pop!_OS al revés**: `/mnt/home` no existe, así que mandar ahí la música sería mandarla a ninguna
parte. Va a `~/Rocola`, a la vista, que es donde uno espera dejar sus canciones.

**Sin root no se mete en `/opt`.** Nadie tiene que dar su contraseña sólo para probar la rocola en su
portátil: se instala en su propia carpeta y funciona igual.

Si se quiere otro sitio: `ROCOLA_DATOS=/donde/sea sh linux/instalar.sh`.

---

## El modo bar

Es lo que convierte un ordenador en **la máquina del local**: se enciende y aparece la rocola.

- **En Puppy** se cambia el arranque de las ventanas, y queda **sin escritorio**: no hay iconos ni
  barra de tareas que nadie pueda tocar. Es el mismo cambio que hace `puppy/instalar.sh`, que lleva
  tiempo funcionando en la máquina del bar. El arranque de Puppy se guarda tal cual para poder
  devolverlo.
- **En Pop!_OS** se abre sola al entrar al escritorio, pero **el escritorio sigue debajo**. Para un
  arranque de verdad sin escritorio hace falta Puppy.

---

## El día a día

**Abrir la rocola:** el icono *Rocola* del menú, o escribir `rocola` en una terminal.

> Si dice que no encuentra `rocola` justo después de instalar: cierra la sesión y vuelve a entrar. Es
> que `~/.local/bin` se mete en el PATH **al entrar**, y la carpeta acaba de nacer.

**Meter música:** se copia a la carpeta `musica` que dejó el instalador, **un disco por carpeta**.
Para música que esté en **otro disco**, se añade desde dentro de la rocola con **Ctrl+F2** (y ahí se
queda, aunque se apague).

**Meter vídeos:** igual que la música, en una carpeta suya dentro de `musica`. Si faltan mpv o ffmpeg
no hay sección de vídeos y la rocola funciona lo mismo.

**Salir de la rocola:** `Ctrl+Alt+Q` **tres veces seguidas**. Las dos primeras sólo avisan.

**Quitarla:** `sh linux/instalar.sh --quitar`. Borra el programa, el mandato, el icono y el arranque
directo. **No toca la carpeta de datos**: la música y lo que la rocola aprendió en años —los volúmenes
medidos, el EQ, lo más sonado— es lo único de esa máquina que no se recupera; el programa se vuelve a
instalar en un minuto.

---

## Si algo va mal

**No suena, o suena con retraso.** `Ctrl+F3` dentro de la rocola, y mira lo de arriba del todo. Si
avisa de que hay **PipeWire o PulseAudio metido en medio**, pon `salida=hw:0` en el
`rocola_config.txt` de la carpeta de datos y reinicia la rocola: así le habla directo a la tarjeta.

**Quiero saber si esta máquina sirve, antes de nada.** `sh linux/instalar.sh --probar` lo dice sin
instalar nada.

**Va lenta.** `Ctrl+F3` → pestaña **Imagen** → **Medir esta máquina**. Mide de verdad, con una canción
sonando y pasando páginas a lo bestia, y propone los ajustes con los que esta máquina pasa de 30
imágenes por segundo. También dice cuánto cuesta cada efecto por separado, por si se quiere ajustar a
mano.

**La pantalla se queda en negro al encender (modo bar en Puppy).** Enciende y entra sin cargar la
rocola, y ya dentro: `sh linux/instalar.sh --quitar`.

---

## ¿Y el `.sfs` de Puppy?

**Sigue ahí, y no lo sustituye esto** (`puppy/LEEME.md`). Un `.sfs` va de **sólo lectura**: nadie lo
puede romper sin querer, ni siquiera queriendo. Para el equipo del bar eso es mejor. Esto de aquí es
para poner la rocola en una máquina **sin pelearse**, que es otra cosa.

---

## Qué está probado y qué no

**Probado de verdad** sobre Ubuntu 24.04 (que es lo que Pop!_OS es por debajo): el instalador compila
la rocola, comprueba las bibliotecas contra el programa recién hecho, la instala, y el mandato `rocola`
la **arranca** y escribe en la carpeta de datos correcta. `--quitar` deja el programa fuera y la música
y lo aprendido intactos. Y `--probar` no crea ni un archivo.

**No probado en una máquina de verdad:** el camino de **Puppy**. Se prueba simulado
(`pruebas/instala.exe` le finge una BookwormPup y comprueba que elige `/mnt/home` y no la carpeta del
usuario), y su modo bar es el mismo cambio de arranque que ya usa `puppy/instalar.sh` en la máquina del
bar. Pero la instalación entera en Puppy no la ha corrido nadie todavía.
