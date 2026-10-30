# P1, D1 y D2 hechos, y otras técnicas para reconstruir el sonido — Modest, 30 de octubre de 2026

Petición del dueño, sobre [AUDIO_PROPUESTAS_MODEST_2026-10-29.md](AUDIO_PROPUESTAS_MODEST_2026-10-29.md): «Todo menos T10, y revisa si no
hay más técnicas de procesamiento de audio para mejorarlo, como un DSEE que reconstruya otras frecuencias y no solo los agudos».

Todo lo medido es con señales sintéticas en Linux/Mono, con la cadena de verdad. Nada se oyó en el billar ni se probó en el A4.

## 1. Lo hecho

### P1. La presencia del billar ya no se deja llevar por el grave

- `PresenciaSala.cs`: con el modo billar, el detector del compresor mide de **150 Hz hacia arriba** (Butterworth de segundo orden por
  canal) con +3 dB. La música que se oye no pasa por ese filtro, y el pico que frena la compensación sigue siendo de banda entera.
- Medido con la etapa sola (`pruebas/presenciagrave.cs`), una voz fija con notas de bajo: sube y baja **3,28 → 1,18**, **2,34 → 0,54**,
  **4,14 → 1,23** y **1,25 → 0,12 dB** (percentil 95 menos 5). En la cadena del billar entera, el banco de la ronda anterior daba
  de 1,6–5,2 a 1,0–2,1 dB.
- Sin el billar el filtro ni se calcula: `compara-cadena` contra el ejecutable anterior da **16 de 16 combinaciones iguales bit a
  bit**.
- En un disco continuo la siguiente hereda el filtro; el filtro corta los desnormales en el silencio.
- Automático con el modo billar y sin interruptor, como las tandas 7 y 8.

**Para decidirlo con el oído:** en «Lo que suena ahora» hay un botón nuevo, **«Presencia nueva o antes»**. Es la misma comparación a
ciegas que «Billar o fábrica»: 8 tramos de 15 s, la mitad con la presencia nueva y la mitad con la de antes, y votas con «Me gusta
esta». Solo cambia la presencia de la canción que suena (sin chasquido: medido) y no se guarda nada. Necesita el modo billar y una
canción en la que el modo haya puesto la presencia; si no, lo dice. Si te gusta más la de antes, dímelo y la quito.

### D1. Lo que hizo cada etapa, en la bitácora del billar

- `rocola_billar_bitacora.txt` lleva **16 columnas nuevas al final**: el promedio y el peor momento de cada canción para lo que bajó y
  lo que devolvió la presencia, lo que subieron las partes suaves, el retumbe, lo que bajó y subió el ataque, los ásperos y las eses
  (dB).
- Las 28 columnas de antes no se mueven, y el resumen de la tarjeta sigue igual.
- El reloj de la canción toma la muestra cada medio segundo (`MuestreaEtapas`), y «Lo que suena ahora» lee esa misma muestra. Antes,
  algunas lecturas de las etapas se ponían a cero al leerlas y la ventana era su único lector; ahora hay un solo muestreador para los
  dos.
- Solo escribe: el sonido no cambia.

### D2. `Recoger_sonido_XP.cmd`

- Junto a `Rocola.exe`, como `Recoger_error_XP.cmd`. Copia a la carpeta `Sonido_XP` la bitácora del billar (y su `.1`),
  `rocola_billar_nivel.txt`, `rocola_sonoridad.txt`, `rocola_volumen.txt`, `rocola_eq.txt`, `rocola_config.txt` y
  `rocola_error.txt`, y abre la carpeta.
- Si se usa `ROCOLA_DATOS`, los lee de ahí.
- No borra, no mueve y no cambia nada. Es ASCII con CRLF para XP, pero **no se ha ejecutado en el A4**.
- `Sonido_XP/` no se sube a Git.

### T2: espera tus datos

T2 (el nivelado «híbrido») no se puede decidir sin la tabla del bar: el objetivo común sale de cómo suenan **tus** canciones. La
`rocola_sonoridad.txt` del 24-10 traía 229 canciones medidas con una versión anterior. Cuando lleve unas noches con esta versión,
ejecuta `Recoger_sonido_XP.cmd`, comprime la carpeta `Sonido_XP` y mándamela. Con eso hago T2, y con las columnas de D1 reviso las
etapas con números del bar.

### Pruebas

- Batería nueva `presenciagrave` (P1, la comparación a ciegas, D1 y D2): 23 comprobaciones.
- `cadenabillar` actualizada a la línea nueva de la bitácora.
- `compara-cadena` contra el ejecutable anterior: 16 de 16 combinaciones iguales bit a bit.
- La suite entera (`pruebas/todo.sh`): 379 baterías, todas en verde.

## 2. Otras técnicas para reconstruir el sonido

### Qué pierde de verdad una canción en la rocola

Un MP3 **no quita graves ni medios**. Lo que pierde es:

1. los agudos por encima de su corte (eso ya lo recrea el DSEE);
2. **huecos** por debajo del corte: bandas que el codificador deja vacías en algunos cuadros;
3. **pre-eco**: un siseo justo antes de cada golpe, que deja la batería blanda.

El grave que falta no viene del MP3: viene de la grabación o de las bocinas del billar. Para las bocinas ya está el bajo
psicoacústico, que crea los armónicos que sí pueden dar.

Lo medí con música sintética (acordes con armónicos, voz, bombo, charles y fondo), codificada con LAME (el codificador bueno) y con
**shine** (uno simple, sin bloques cortos, como muchos de los MP3 viejos de internet).

**Huecos: parte de lo que se oye que el MP3 deja 15 dB o más por debajo, por bandas.**

| codificador | 2–5 kHz | 5–10 kHz | 10–14 kHz | 14–16 kHz | 16–20 kHz |
|---|---|---|---|---|---|
| LAME 320 kbps | 0,0 % | 0,0 % | 0,0 % | 0,0 % | 0,1 % |
| LAME 128 kbps | 0,3 % | 0,7 % | 2,6 % | 4,8 % | 87 % (su corte) |
| LAME 96 kbps | 0,8 % | 3,3 % | 18 % | 60 % | 100 % |
| LAME 64 kbps | 2,9 % | 22 % | 83 % | 100 % | 100 % |
| shine 128 kbps | 0,7 % | 4,9 % | 21 % | 24 % | 23 % |
| shine 96 kbps | 1,9 % | 15 % | 45 % | 49 % | 48 % |

**Pre-eco: el error en los 11 ms antes de un golpe seco, frente al golpe.**

| codificador | mediana | peor |
|---|---|---|
| LAME 320 kbps | −41 dB | −31 dB |
| LAME 128 kbps | −34 dB | −25 dB |
| LAME 96 kbps | −34 dB | −28 dB |
| shine 128 kbps | **−5 dB** | **−2,5 dB** |

Lo que dicen las tablas:

- Un MP3 bueno a 128 kbps casi no tiene huecos bajo su corte ni pre-eco que se oiga. Ahí lo único que falta son los agudos, que es
  lo que hace el DSEE.
- Un MP3 de un codificador malo, o de menos de 128 kbps, **pierde a ratos una quinta parte o más de lo que hay entre 10 y 20 kHz, y
  hasta un 15 % entre 5 y 10 kHz**. Además, sus golpes llegan con un siseo casi tan fuerte como el golpe mismo. El DSEE de hoy no
  arregla ninguna de las dos cosas: solo trabaja por encima del corte, y en estos archivos no hay un corte limpio.

### R1. Rellenar los huecos del MP3 (el «DSEE por dentro»)

Es lo que hacen los codificadores modernos al descomprimir (el «noise filling» del AAC y el «gap filling» del MPEG-H), pero hecho
después, sobre la canción ya descomprimida.

- Con la FFT que ya usa el DSEE: en cada banda de un tercio de octava entre 4 y 16 kHz, si en un cuadro la banda cae 15 dB o más por
  debajo de sus vecinas en el tiempo y en la frecuencia, se rellena con ruido con la forma de lo que la rodea, unos 3 dB por debajo.
- Solo huecos cortos (menos de unos 100 ms) y estrechos: si se apaga todo, es un silencio de la música y no se toca.
- Solo en las canciones en las que el repaso de la biblioteca mida huecos, como el zumbido de T6. Las buenas salen bit a bit.
- Costaría lo que una pasada del DSEE (en el billar el DSEE está apagado, así que hay sitio).
- Cambia el sonido, así que llevaría su interruptor, apagado de fábrica. Tú decides si va en el modo billar.
- **Hace falta saber antes cuántas canciones lo necesitan.** Primero el repaso mediría los huecos y el pre-eco de cada canción y lo
  apuntaría en `rocola_calidad.txt`. Solo si salen muchas vale la pena el relleno.

### R2. Quitar el pre-eco (golpes más secos)

- Al encontrar un golpe, se baja de 2 kHz hacia arriba lo que hay en los 5 a 20 ms anteriores, hasta el nivel que había antes del
  siseo, y se vuelve en cuanto empieza el golpe.
- Mismo criterio que R1: solo en las canciones medidas con pre-eco, con interruptor y apagado de fábrica.
- Riesgo: un platillo que entra justo antes del golpe se recortaría un poco. Por eso solo en las canciones que lo traen.

### Lo que miré y no propongo

- **Reconstruir el grave «de verdad»** (crear subgrave, como los generadores de subarmónicos): ninguna bocina del billar da menos de
  ~40–50 Hz, y el modo billar corta a 40 Hz. Lo creado no se oiría y gastaría amplificador. Para el grave ya está el bajo
  psicoacústico.
- **Reconstruir los medios:** los MP3 no los pierden (menos del 1 % de huecos entre 0,5 y 2 kHz en todos los casos medidos). Las
  canciones flacas ya las corrige el EQ del billar.
- **Devolver el estéreo que quita el MP3 conjunto:** en el billar el estéreo se estrecha y las bocinas están por zonas; no se oiría.
- **Separar la voz con IA para subirla:** en el A4 con XP no da (los modelos de separación tardan varias veces lo que dura la
  canción). Se puede volver a mirar en Rocola OS, con el procesador de cuatro núcleos.
- **T10** (saltar sin chasquido): no lo pediste.

### Mi orden

1. **Medir antes de reconstruir:** que el repaso apunte los huecos y el pre-eco de cada canción. Solo mide y no cambia el sonido. Con
   eso, y con los datos de D2, sabremos si R1 y R2 valen la pena en tu biblioteca.
2. **R2** si sale pre-eco: es lo que más se oye de los dos (de −34 a −5 dB en un codificador malo).
3. **R1** si salen huecos.

Dime si hago la medida (1), que no cambia el sonido.
