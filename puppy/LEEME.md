# La rocola en Puppy: se enciende y ya está

Esto deja la máquina del bar así: **se enciende y aparece la rocola**. Sin escritorio, sin iconos,
sin barra de tareas, sin que nadie toque nada.

> **Para instalar la rocola sin complicarse hay ahora un solo paso: `sh linux/instalar.sh`**
> (ver `linux/LEEME.md`). Sirve para Puppy y para Pop!_OS, instala solo lo que falte y deja el modo
> bar puesto si se le pide.
>
> **Esto de aquí sigue siendo la vía del equipo del bar**, y no sobra: el `.sfs` va de **sólo
> lectura**, así que nadie puede romper la instalación sin querer, y además deja el programa ya
> traducido a lenguaje de máquina (46 ms menos hasta la primera canción). Si la máquina es la del
> local, esta página es la buena.

Hace falta **BookwormPup64** ya instalado (una instalación "frugal" normal, la que hace Puppy sola).

---

## Lo que va en cada sitio, y por qué

| | qué es | dónde va |
|---|---|---|
| **`rocola.sfs`** | Mono, libgdiplus, **mpv y ffmpeg** (los vídeos) y la rocola | Puppy lo abre al arrancar. **Solo lectura**: nadie lo puede tocar ni romper. |
| **La carpeta de datos** | la música, los créditos, la cola, los volúmenes medidos, lo más sonado y **las miniaturas de las portadas** | Una carpeta normal del disco (`/mnt/home/rocola`). |

**Por qué separado.** Un `.sfs` no se puede escribir. Puppy deja escribir "por encima", pero lo
escrito se queda en la memoria y **no baja al disco hasta que Puppy guarda** — así que un apagón se
llevaría los créditos y la cola de la noche. Con la carpeta aparte, la rocola escribe en el disco de
verdad y un apagón no se lleva nada: al volver la luz, sigue donde iba.

**Y por eso están ahí las miniaturas de las portadas.** Cada disco de la pared tiene su portada, y
decodificar el JPG grande cuesta unas **once veces más** que leer una miniatura ya hecha (16 ms
contra 1.5 ms, medido). Antes se guardaban en la carpeta temporal del sistema, que en Puppy **está en
la memoria y se borra al apagar**: o sea que cada mañana había que rehacerlas todas. Ahora van al
disco con el resto, se hacen una vez y ya está para siempre.

---

## Los pasos

### 1. Preparar el `.sfs` (en la máquina de Puppy)

Primero, instala en Puppy **Mono**, **libgdiplus** y —si quieres vídeos— **mpv** y **ffmpeg**
(Menú → Setup → Puppy Package Manager). Después:

```
sh hacer_sfs.sh
```

Antes de empaquetar nada comprueba que a la máquina no le falte nada, y si falta algo **se planta y
lo dice** en vez de dejar un `.sfs` que no funciona.

De paso deja el programa **ya traducido** a lenguaje de máquina, para que no haya que traducirlo en
cada arranque: son unos 46 ms menos hasta que suena la primera canción (un 12%, medido). Si esa parte
no se pudiera hacer, lo dice y sigue: el `.sfs` sale igual y la rocola funciona lo mismo, arrancando
un pelín más lenta.

> Tiene que hacerse **en la máquina de Puppy**, no en otra. No es capricho: los programas de Linux se
> compilan contra las bibliotecas del sistema donde se hicieron, y uno traído de otra distribución no
> arranca. Copiando el Mono de la propia máquina, encaja seguro.

### 2. Que Puppy lo cargue al arrancar

1. Copia `rocola.sfs` junto a los archivos de arranque de Puppy (donde está `vmlinuz`).
2. Menú → Sistema → **BootManager** → *cargar archivos SFS al arrancar* → marca `rocola.sfs`.
3. Reinicia.

### 3. Dejar el arranque directo

```
sh instalar.sh
```

Guarda el arranque de Puppy tal como estaba, deja el directo puesto y pone un icono `rocola` en el
escritorio. Reinicia y la rocola tiene que aparecer sola.

---

## El día a día

**Salir de la rocola:** `Ctrl+Alt+Q` **tres veces seguidas**. Las dos primeras sólo avisan en pantalla
("Salir de la rocola: 1 de 3"), así que si se pulsa sin querer se ve venir y se para. A la tercera se
cierra y aparece el escritorio de Puppy.

> Tres veces y no una porque en un bar la rocola está al alcance de cualquiera. Tres seguidas dentro de
> unos segundos no le pasan a nadie por accidente.

**Volver a la rocola sin reiniciar:** el icono `rocola` del escritorio.

**Meter música:** se copia a `/mnt/home/rocola/musica`, un disco por carpeta. Para música que esté en
**otro disco**, se añade desde dentro de la rocola con **Ctrl+F2** (y ahí se queda, aunque se apague).

**Meter vídeos:** igual que la música, en una carpeta suya dentro de `musica`. La rocola les mide el
volumen sola y los deja al mismo nivel que las canciones, así que un vídeo no revienta después de una
canción ni se queda en un susurro. Si en la máquina faltan mpv o ffmpeg, **no hay sección de vídeos**
y la rocola funciona igual — nunca una sección que al pulsarla no hace nada.

**Deshacer el arranque directo:** `sh instalar.sh --quitar`. No borra ni la música ni nada de lo
aprendido.

---

## Si algo va mal

**La pantalla se queda en negro al encender.** Enciende y, en el menú de arranque de Puppy, entra sin
cargar el `rocola.sfs`. Ya dentro: `sh instalar.sh --quitar`.

**No suena, o suena con retraso.** Entra en la rocola, `Ctrl+F3`, y mira lo que dice arriba del todo.
Si avisa de que hay **PipeWire o PulseAudio metido en medio**, pon `salida=hw:0` en
`/mnt/home/rocola/rocola_config.txt` y reinicia la rocola: así le habla **directo** a la tarjeta, sin
nada que añada retraso.

**Quiero saber si esta máquina sirve, antes de nada.** Copia `pruebas/bibliotecas.exe` y corre
`mono bibliotecas.exe Rocola.exe`. En diez segundos dice si le falta algo y qué.

**Quiero saber cuánto tarda en sonar.** Dos mitades:
- La de Puppy (encender → llegar a la rocola) se apunta sola en `/mnt/home/rocola/rocola_arranques.txt`.
- La de la rocola (arrancar → que suene) la mide `mono pruebas/arranque.exe Rocola.exe`.

**La ventana de ajustes (Ctrl+F3) se porta raro.** En `arranca_rocola.sh`, quita el `#` del renglón
`# jwm &`. Eso enciende el gestor de ventanas que trae Puppy — no muestra escritorio ni barra de
tareas, sólo se encarga del foco de las ventanas.
