# R1: rellenar los huecos de los MP3 viejos (22-11-2026)

Decisión del dueño del 22-11-2026: hacer «R1: rellenar huecos de los MP3 viejos» en Rocola OS. La propuesta es de octubre
([AUDIO_P1_D1_D2_Y_TECNICAS_MODEST_2026-10-30.md](../../../AUDIO_P1_D1_D2_Y_TECNICAS_MODEST_2026-10-30.md), «R1»). En XP no se hizo
([AUDIO_R2_Y_SEPARACION_VOZ_MODEST_2026-10-31.md](../../../AUDIO_R2_Y_SEPARACION_VOZ_MODEST_2026-10-31.md), §2): el ACM de Windows no dice
qué bandas vinieron vacías, y mirando el sonido ya descomprimido no se distinguen de los valles normales. Allí quedó escrito que en Rocola OS
sí tenía sentido, «con el dato exacto».

Solo Rocola OS (rocola-xp). Sin `ROCOLA_OS`, XP tal cual: no se mide ni se escribe nada. **Nada se oyó en el bar ni se probó en el A4.**

## 1. De dónde sale el dato exacto

rocola-xp descomprime los MP3 con **libmpg123** (`Nativo/DescompresorMp3.cs`; NLayer en coma flotante para las que llegan al máximo,
`Codigo/FlotanteNLayer.cs`). libmpg123 tampoco dice qué bandas vinieron a cero. Pero el descompresor recibe **cada cuadro del MP3 entero**,
así que se le puede preguntar al propio MP3: `RocolaOs/RocolaOs.HuecosMp3.cs` es un lector de la capa III de MPEG-1 (44,1, 48 y 32 kHz) que
lee la información lateral, el depósito de bits, los factores de escala y los valores de Huffman (los árboles de la norma, en
`RocolaOs.HuecosTablas.cs`, copiados de NLayer 3.0.0, MIT, por reflexión y una sola vez). Con eso sabe, en cada granulo (576 muestras, 13 ms)
y en cada línea del espectro, si el codificador la mandó **a cero** y con qué **paso de cuantización**.

El sonido lo sigue dando libmpg123: el lector no lo sustituye, solo calcula el relleno y lo **suma**. Para sumarlo en su sitio, el lector
también hace la síntesis del MP3 (alias, IMDCT con la ventana del granulo, inversión de frecuencia y banco polifásico), con vectores de 4
(SSE: lo que tiene el A4 sin AVX2).

**Comprobado con un doble que anula**: el descompresor envuelto, pero con el lector sintetizando la canción ENTERA con el signo cambiado. Si
el lector lee, sintetiza, escala y cae en el mismo momento que libmpg123, sale casi silencio. Sale a **−103 dB** de la canción en las 7
codificaciones (lo que queda es el redondeo a 16 bits), también con saltos, y a **−127 dB** con NLayer en coma flotante. Así se encontró
lo único raro de libmpg123: el primer cuadro (al abrir y tras un salto) no lo da en la primera llamada; con uno de LAME lo da en la segunda
junto con el siguiente, con uno de shine no lo da nunca. El envoltorio lleva el relleno en una cola y tira lo que libmpg123 ya no va a dar
(«lo que da es lo más nuevo»), y con eso los dos casos caen en su sitio.

## 2. Qué se rellena

En un granulo largo (los cortos, los mixtos y los de estéreo de intensidad no se tocan), **de 3 kHz para arriba y por debajo de la última
línea con algo de ese canal** (su corte en ese granulo), en el dominio del propio MP3 (L/R, o M/S si viene así: cada canal con lo suyo, y lo
que era casi mono sigue casi mono):

- **Líneas a cero en una banda que trae algo.** Salieron cero porque valían menos que medio paso de su banda. Van con un ruido de **0,1 pasos
  de amplitud eficaz** (20 dB bajo el paso; `LineaPaso`): por debajo del error que el propio codificador dio por tapado (el de un ruido parejo
  de ±medio paso es 0,29 pasos) y nunca a más de 6 dB bajo lo que traen las líneas con algo de esa banda.
- **Una banda entera a cero** con algo en la de debajo y en una de las dos de encima (un hueco de como mucho dos bandas), corta (hasta 4
  granulos, 52 ms, entera; se apaga hasta el 8: desde ahí ya es la música, no un hueco): la media geométrica de la energía por línea de sus
  vecinas, 6 dB por debajo, y como mucho la de un ruido parejo de ±medio paso.

La primera versión solo rellenaba bandas enteras, como decía la propuesta. **No servía**: los huecos de un MP3 son casi todos de una o dos
líneas (los «80 Hz» de la medida de octubre), dentro de bandas que traen algo. Con bandas enteras, shine 128 pasaba del 18,9 al 18,0 % (abajo,
la tabla); con las líneas, al 5,1 %. El 0,1 de paso se eligió midiendo 0,07, 0,1, 0,13, 0,16, 0,2 y 0,29: con más, se rellenan más huecos
pero sobra más (ruido 6 dB o más por encima del original); 0,1 es donde los huecos más lo que sobra quedan más bajos en todas.

## 3. Las dos funciones

| función | de fábrica | qué hace |
|---|---|---|
| `huecos_mide` | **encendida** (solo mide) | un repaso de fondo (nice 19, 50 ms entre archivos) lee de cada MP3 de la biblioteca tres tramos (al 20, 50 y 80 %, 384 KB cada uno; el archivo entero si es chico) y apunta en `rocola_huecos.txt` (`ruta, tamaño, fecha, algoritmo, cuadros, candidatas, huecos, relleno en décimas de dB`: solo números). Una vez por archivo; otra si cambia su tamaño o su fecha |
| `relleno_huecos` | **apagada** | en las canciones que **traen huecos**, el descompresor de la que va a sonar va envuelto (`RocolaOsRellenoHuecos`) y se le suma el relleno |

**Una canción trae huecos** si el relleno le pondría **−20 dB o más** frente a lo que traen esas bandas (`UmbralDb`), con al menos 20 000
líneas candidatas. El veredicto se saca de los números al leer la tabla: cambiar el umbral no obliga a medir otra vez. Sin medida, o con
MPEG-2/2.5 (22 kHz y menos: este lector no los entiende), ni se pone.

Encender: `relleno_huecos=1` en `/rocola/datos/xp/rocola_os.txt` (la orden «ajuste» del buzón o a mano), y vale desde el siguiente arranque
(las funciones de Rocola OS se deciden al arrancar), para las canciones que se abran desde entonces, como R2. `huecos_mide=0` apaga el
repaso (y sin medida, el relleno no actúa).

- **Apagada** (o una canción sin huecos, sin medir o de MPEG-2): los ganchos devuelven **el mismo descompresor** de XP. Bit a bit como hoy.
- **«Escuchar sin mejoras»**: el envoltorio no suma nada (bit a bit como apagado; comprobado), desde el latido (un segundo como mucho).
- Si el lector falla a media canción, se calla el resto de esa canción y se apunta; el sonido de XP nunca deja de salir.
- Lo que mide XP (`LectorMedida`: sonoridad, EQ, pre-eco...) no se envuelve. Lo que aprende la cadena mientras suena (la sonoridad de una
  canción sin medir, la huella del EQ) sí oye el relleno: es 36 a 40 dB más bajo que la canción y solo de 3 kHz arriba.
- En el `/run/rocola-vivo.txt`: `huecos_mide=listo N medidas, M con huecos, ultima X ms` y `relleno_huecos=encendida abiertas=N rellenadas=M`.

Ganchos (una línea cada uno, en `RocolaOs/GANCHOS.tsv`): **g260** `Codigo/Form1.Sonido.cs` (`AbreMp3` desde el archivo) y **g261**
`Codigo/CancionEnMemoria.cs` (`AbreMp3` desde la memoria): `hecho = RocolaOsRellenoHuecos(DecodificacionFlotante.Envuelve(...), ruta)`. Son
los dos sitios donde se abre una canción para que suene. Archivos nuevos: `RocolaOs/RocolaOs.Huecos.cs` (las funciones, el repaso, la tabla,
el envoltorio y los ganchos), `RocolaOs.HuecosMp3.cs` (el lector y la síntesis), `RocolaOs.HuecosTablas.cs` y `RocolaOs.HuecosPrueba.cs`
(la orden `--prueba-huecos`).

## 4. Lo medido (`Pruebas/RocolaOs/huecos/prueba.sh`)

Música sintética (`musica.py`: acordes con armónicos y vibrato, voz con formantes, bombo, charles, platillo y fondo; 20 s, estéreo)
codificada con ffmpeg: LAME (el bueno) y shine (el simple, sin bloques cortos, como muchos MP3 viejos). Frente al ORIGINAL (`compara.py`, la
misma cuenta que la tabla de octubre), de 3 a 16 kHz: la parte de lo que se oye que queda **15 dB o más por debajo** (huecos) y la que queda
**6 dB o más por encima** (sobra: ruido donde no había):

| MP3 | relleno medido | ¿trae huecos? | huecos sin → con | sobra con |
|---|---|---|---|---|
| LAME 320 | −39,1 dB | no | 0,02 → 0,02 % | 0,01 % |
| LAME 192 | −25,4 dB | no | 0,40 → 0,38 % | 0,33 % |
| LAME 128 | −20,8 dB | no (por poco) | 4,75 → 2,70 % | 0,83 % |
| LAME 96 | −17,9 dB | **sí** | 18,5 → 13,4 % | 1,0 % |
| LAME 64 | −15,8 dB | **sí** | 54,3 → 48,8 % | 0,9 % |
| shine 128 | −17,5 dB | **sí** | 18,9 → **5,1 %** | 2,0 % |
| shine 96 | −14,4 dB | **sí** | 47,3 → **18,0 %** | 2,2 % |

(«con» en las de «no» es envolviéndolas a la fuerza, para ver qué haría.) Lo que más gana es lo que pedía la propuesta: los MP3 de un
codificador malo. En LAME 96 y 64 gana menos: ahí lo que falta es sobre todo el corte (de 14 kHz arriba en 64), que es del DSEE, no de R1.
El relleno sale 36 a 40 dB por debajo de la canción entera.

**Lo que cuesta** (NativeAOT, la mejor de 7 vueltas, en esta máquina: Xeon de 2,1 GHz; vectores de 4 como el A4): el relleno, **3,0 a 3,8 ms
por segundo de canción** en las de 96 y 128 kbps (0,3 a 0,4 % de un núcleo; libmpg123 gasta 0,8 a 1 ms); una de 320 kbps, 5,7 ms (leer el Huffman es lo
que más pesa). Sin medir en el A4: allí calculo dos o tres veces eso, alrededor del 1 % de un núcleo, en el hilo que ya descomprime. La medida
del repaso: unos 30 a 70 ms por canción aquí (los tres tramos), a nice 19.

Las comprobaciones (62 en NativeAOT, todas BIEN; `bash Pruebas/RocolaOs/huecos/prueba.sh PROGRAMA`, en JIT y `aot:CARPETA`): `mide` (las 4 con huecos sí, las 3 de LAME
de 128 o más no, la de MPEG-2 ni se mide), `decodifica` (envuelto sin sumar = sin envolver bit a bit; el doble anula a −103 dB con libmpg123
y −127 dB con NLayer; el relleno pone algo en las que traen huecos; en la de MPEG-2 nada), `oido` (la tabla de arriba), `salta`, `costo` y
`escena` (la rocola de verdad fuera de pantalla: el repaso mide la biblioteca, la vieja que se pide suena envuelta y con relleno; con
`relleno_huecos` apagada no se envuelve nada; sin `ROCOLA_OS` ni se mide).

## 5. Lo que no está comprobado

- **Nada oído**: ni en el bar ni en el A4 ni con unos audífonos. Las cuentas dicen que se rellena lo que el codificador borró sin pasarse de
  lo que él dio por tapado; si se oye mejor (o si el ruido de relleno se nota como siseo en alguna canción) lo dice el oído del dueño. Por
  eso va apagada.
- Solo con música sintética y dos codificadores de hoy. Los MP3 viejos de verdad (Xing, FhG de los 90, BladeEnc) pueden repartir los ceros
  de otra forma; el umbral de −20 dB puede necesitar ajuste cuando el repaso haya medido la biblioteca del bar (`rocola_huecos.txt` dirá
  cuántas traen huecos).
- MPEG-2 y 2.5 (22 kHz y menos, típicos de los de 32 a 64 kbps) no se miden ni se rellenan.
- El costo en el A4 es una estimación.
- Con el modo billar no cambia nada (el billar no decide R1); el dueño decide si va con él.
