# Más mejoras de audio (tercera ronda) — Modest, 29 de octubre de 2026

Petición del dueño: «Busca más mejoras de audio en la cadena de sonido de la rocola (revisa los últimos cambios para que sepas qué
se ha hecho y no repitas trabajo innecesario)».

Esta ronda es solo de buscar y medir: **no cambia nada del programa.** Todo lo medido es con canciones sintéticas en Linux/Mono, con
la cadena de verdad (`Rocola.exe` por dentro, como las baterías de `pruebas/`). Nada se oyó en el billar ni se midió en el A4.

## En pocas palabras

- La cadena ya está muy completa. Después de las ocho tandas del modo billar, **casi no quedan mejoras de procesamiento que se
  puedan decidir sin oír la rocola en el bar.**
- Lo único que encontré, medible y arreglable: **la presencia se deja llevar por el grave** (la voz baja cada vez que entra el bombo o
  una nota de bajo). Hay un arreglo pequeño (P1), con números. Es una mejora moderada, no un cambio de noche a día.
- Lo que más ayudaría de aquí en adelante son **datos del bar**. Propongo dos cosas que solo escriben y no tocan el sonido (D1 y D2)
  para que la próxima ronda se base en números de tu rocola y no en canciones de prueba.
- Busqué fallos con 750 pasadas de un banco aleatorio sobre la cadena entera: **no hay ninguno que se oiga.** Quedan dos detalles
  minúsculos que dejo anotados (más abajo): un escalón de 1,6 % al apretar «Escuchar sin mejoras» y una caída de 1 dB durante 3 ms
  al empalmar dos pistas de un disco continuo cuando el limitador baja mucho.

## Lo que ya hay, para no repetir

La cadena entera y lo hecho están en [REVISION_CADENA_AUDIO_MODEST_2026-10-24.md](REVISION_CADENA_AUDIO_MODEST_2026-10-24.md),
[CADENA_BILLAR_MODEST_2026-10-27.md](CADENA_BILLAR_MODEST_2026-10-27.md),
[VOZ_GOLPE_Y_RETUMBE_MODEST_2026-10-27.md](VOZ_GOLPE_Y_RETUMBE_MODEST_2026-10-27.md) y
[AUDIO_PROPUESTAS_MODEST_2026-10-25.md](AUDIO_PROPUESTAS_MODEST_2026-10-25.md), y las ocho tandas del billar en `AGENTS.md`.

En el modo billar, la música pasa por: arreglos de la grabación (canal mudo, continua) → zumbido → des-recorte → nivelado al límite
y EQ de la canción → grave en mono y subgrave (fase lineal) → el local (claridad, partes suaves, balance, graves y agudos) → retumbe
→ presencia → ataque → ásperos → eses → bajo psicoacústico → limitador de pico real que respira → remuestreo → dither → salida con
hilo propio.

Y ya existen: el volumen parejo aprendido y estimado, afinar con el celular, la bitácora y su resumen, «Comparar a ciegas» (billar o
fábrica, con o sin afinar), los MP3 sin huecos entre pistas (etiqueta LAME), los discos continuos sin escalón, los desnormales, y
`[Cortes de sonido]` en `rocola_error.txt`.

Lo que dijiste que **no** (no lo vuelvo a proponer): recorte suave, quitar el eco de los vivos, cortar silencios o finales, el
micrófono, el DSEE y quitar el siseo en el billar, y el bombo sin hundir en el billar.

## 1. P1 — La presencia se deja llevar por el grave

**Qué pasa.** La presencia (`PresenciaSala.cs`) es un compresor suave que sube lo flojo y baja lo fuerte. Mide el nivel con la señal
**entera**, así que una nota de bajo o un bombo cuentan como «fuerte» y el compresor aprieta a **toda** la mezcla, voz incluida. La
voz baja un poco con cada golpe de grave y vuelve después (la suelta es de 250 ms). Eso es el «bombeo» de los compresores, y la
solución de siempre es que el detector no escuche el grave.

**Cuánto.** Una voz fija con notas de bajo (una cada 0,5 s), solo con la presencia encendida, midiendo cuánto sube y baja el nivel de
la voz (percentil 95 menos percentil 5, en ventanas de 25 ms):

| bajo (pico) y voz | detector actual (banda entera) | con un filtro de 150 Hz hacia arriba |
|---|---|---|
| 0,55 y voz ×1 | 3,25 dB | 0,69 dB |
| 0,40 y voz ×1 | 2,34 dB | 0,47 dB |
| 0,70 y voz ×0,8 | 3,89 dB | 0,22 dB |
| 0,25 y voz ×1,4 | 1,25 dB | 0,06 dB |

Con las constantes del modo billar (la presencia devuelve hasta 5 dB), lo mismo da 4,52 / 3,27 / 5,20 / 1,64 dB con el detector
actual y 2,14 / 1,90 / 1,23 / 0,97 dB con el filtro.

**Con música más parecida a la de verdad.** Tres programas de 36 s con voz con sílabas, bajo, bombo (la balada no lo lleva) y
charles, y una estrofa suave seguida de un coro fuerte («pop con bajo», «balada con bajo suave» y «reggaeton»). Modo billar. Lo que
se mide es cuánto sube y baja la voz **de punta a punta** (la que sale frente a la que entra), en dB. «Nivel» es el del programa
antes de la cadena. El nivelado al límite los subiría unos +4 dB (pop), +7 dB (balada) y +3 dB (reggaeton), así que las filas de
+3 y +6 dB son las que se parecen a lo que suena en el bar.

| nivel | solo el limitador (sin presencia) | presencia actual | filtro de 150 Hz en el detector (+3 dB) |
|---|---|---|---|
| +0 dB | 0,0 / 0,0 / 0,1 | 5,5 / 1,7 / 6,2 | 4,0 / 0,6 / 4,4 |
| +3 dB | 1,6 / 0,0 / 0,9 | 8,9 / 4,0 / 9,3 | 7,1 / 2,8 / 7,2 |
| +6 dB | 4,2 / 0,7 / 3,1 | 11,3 / 7,3 / 9,8 | 8,4 / 6,0 / 7,3 |

(Cada celda: pop / balada / reggaeton.) El filtro quita entre un quinto y un tercio del vaivén (dos tercios en la balada a nivel
normal). **No lo quita todo:** lo que queda viene de la propia voz (el compresor sí debe actuar sobre sus sílabas), de la
compensación que se frena cuando se acerca un pico al techo y del limitador cuando la canción va muy fuerte.

**Lo que cuesta.** Con el filtro el detector ve menos nivel y aprieta un poco menos, así que la presencia nivela algo menos las
secciones: a nivel normal, la diferencia entre el coro y la estrofa queda 0,9 / 0,8 / 0,5 dB más abierta (de 2,7 / 4,8 / 2,2 dB pasa
a 3,6 / 5,6 / 2,7 dB; sin presencia es 6,4 / 6,0 / 6,5). Por eso el detector lleva +3 dB: sin ellos quedaba 1,7 / 1,5 / 0,7 dB más
abierta. La voz promedio no cambia en los programas, y en el banco de la voz fija queda entre 1 y 1,8 dB más alta (ya no se la baja
con cada nota de bajo).

**Lo que probé y no sirvió** (para que nadie lo repita):

- *El mismo filtro también en el pico que frena la compensación:* mejora a +3 dB (6,0 / 2,3 / 4,8 frente a 7,0 / 2,7 / 6,5 con solo
  el detector) pero a +6 dB ya no (8,7 / 4,9 / 7,4 frente a 7,8 / 5,9 / 7,1): el bajo se amplifica sin que nadie lo cuente y el
  limitador tiene que bajarlo, y bombea él.
- *Presencia en dos bandas* (el grave pasa aparte y solo se comprime de 150 Hz hacia arriba): no mejora (7,2 / 2,4 / 6,1 a +3 dB
  frente a 7,0 / 2,7 / 6,5) y cambia el equilibrio de tonos, porque el grave ya no sube con las partes suaves. Descartada.
- *Un filtro más alto* (250 a 400 Hz): baja más el vaivén (a 400 Hz, 4,4 / 2,7 / 4,5 a +3 dB), pero la presencia nivela mucho menos
  las secciones (coro menos estrofa 3,7 / 6,1 / 3,5 dB; sin presencia 5,5 / 6,0 / 5,9; en la balada, nada). Es otro compresor, no
  una mejora.

**Qué se cambiaría.** Solo en el modo billar (apagado, la cadena sale bit a bit como ahora; no hace falta interruptor, como en las
tandas 7 y 8):

- un filtro de segundo orden (Butterworth, 150 Hz) por canal **solo en la cuenta de la potencia del detector** (`sm += q` en
  `PresenciaSala.Proceso`); la señal que se oye no pasa por él y el pico que frena la compensación sigue siendo de banda entera;
- +3 dB (×2) a esa potencia para no abrir el nivelado de las secciones;
- el estado del filtro se copia en `Hereda` (discos continuos), con el recorte de desnormales de siempre (`1e-30`);
- coste: cinco multiplicaciones y sumas por muestra y canal. Despreciable frente al 2,8 % de un núcleo que gasta la cadena del billar.

**Pruebas.** Una batería nueva con la voz fija y las notas de bajo (el vaivén de la voz tiene que bajar a menos de dos tercios del de
ahora), `compara-cadena` contra el ejecutable de antes (idéntico con el modo apagado), `cadenabillar`, `cadenaentera` (el empalme de
un disco continuo), `desnormales` y la suite entera.

**Límite de lo medido.** Es un banco sintético: voz, bajo y bombo de mentira. No sé cuánto de esto se oye en tu local. Por eso, si lo
quieres, lo dejaría en «Comparar a ciegas» (una prueba más, como «Con o sin afinar»): tú decides con los oídos y el AGENTS.md lo
anota.

## 2. D1 y D2 — Datos del bar para la próxima ronda (no cambian el sonido)

Todo lo que se hizo desde el 27-10 se ajustó con canciones sintéticas. La bitácora del billar ya apunta una línea por canción, pero
no dice **cuánto trabajó cada etapa**, y el `Recoger_error_XP.cmd` solo junta cosas de fallos.

- **D1. Actividad de cada etapa en la bitácora.** Columnas nuevas al final de `rocola_billar_bitacora.txt` (las de antes no se mueven;
  el resumen lee solo hasta la 11): cuánto bajó la presencia y cuánto devolvió, lo que bajaron las eses, los ásperos y el retumbe, lo
  que subió el ataque, y cuánto subieron las partes suaves, en promedio y en el peor momento de la canción. Ya existen las lecturas
  (`ReduccionDb`, `CompensaDb`, `BajaDb`, `CortaDb`, `Toma`, `Subida`) que usa «Lo que suena ahora»; algunas se ponen a cero al
  leerlas, así que habría que repartirlas con un solo muestreador cada 0,5 s para que la ventana y la bitácora no se las quiten una
  a la otra. Solo escribe, en segundo plano.
- **D2. `Recoger_sonido_XP.cmd`.** Igual que `Recoger_error_XP.cmd`: copia a una carpeta `Sonido_XP` (y la abre) la bitácora del billar
  (y la `.1`), `rocola_billar_nivel.txt`, `rocola_sonoridad.txt`, `rocola_volumen.txt`, `rocola_eq.txt`, `rocola_config.txt` y
  `rocola_error.txt` (con `[Cortes de sonido]`, `[Ruido en la salida]` y `[Sin huecos]`). No cambia nada del equipo. Tú decides si me
  mandas el zip.

Con unas noches de bitácora podría decir, con números de tu rocola: qué etapas casi nunca actúan (y se pueden simplificar), cuáles
actúan demasiado, y si la presencia aprieta como en el banco. Y T2 (más abajo) necesita justo `rocola_sonoridad.txt` y
`rocola_volumen.txt`.

## 3. Lo de rondas anteriores que sigue pendiente, puesto al día

- **T2** (nivelado «híbrido», un objetivo común de sonoridad con el límite de tope): sigue esperando la tabla del bar (D2).
- **T4** (siseo de lo que el nivelado sube mucho): en el billar tu decisión es que no se quite el siseo. Sin cambios.
- **T7** (acortar finales larguísimos): lo descartaste.
- **T8** (mezcla entre discos distintos): no la recomiendo ahora; son dos cadenas a la vez en un procesador de dos núcleos y sin datos
  del A4.
- **T10** (saltar sin chasquido): **sigue pendiente.** Hoy SALTAR llama a `DisposePlaybackNow`, que para la salida de golpe
  (`WaveOutPropio.Stop` → `waveOutReset`): si la onda no estaba en cero, hay un escalón (un «plop»). Un fundido de 150 ms no se puede
  poner en la música que ya está en la cola de la tarjeta, y esa cola es de 750 a 1000 ms en el A4. Lo posible es bajar ese colchón
  a unos 300 ms ahora que el hilo es propio, **pero hay que mirar antes `[Cortes de sonido]` en el A4**. Hasta entonces, no.

## 4. Lo que revisé

**Un banco aleatorio sobre la cadena de verdad** (750 pasadas, semillas 13 a 262, tres comprobaciones por semilla):

- *Lectura:* la misma canción leída en trozos de 4096 o de tamaños al azar sale con los mismos bits; sin NaN ni infinitos; ningún pico
  pasa del techo del limitador (−1,1 dBFS); el largo es el de la entrada más el retardo de la cadena.
- *Cambios en vivo:* un mando de los trece que se mueven en caliente (claridad, partes suaves, presencia, subgrave, estéreo estrecho y
  automático, eses, bombo, retumbe, ataque, ásperos, modo billar y «Escuchar sin mejoras») cambia en un bloque al azar: sin NaN, sin
  pasar del techo y sin chasquido.
- *Disco continuo:* dos pistas empalmadas suenan como la misma pista entera, sin escalón.
- Las señales: quince clases (pop, ruido, barrido, cuadrada, impulsos, silencio con clics, continua, un solo canal, contrafase, muy
  bajita, voz, zumbido, master aplastado, bajo fuerte, platillos), de 22 a 88 kHz, mono y estéreo, de 0,3 a 12 s, con hasta +21 dB de
  ganancia forzada y la configuración (modo billar o no, EQ, volumen parejo, mandos) sorteada.

Resultado: **0 fallos en la lectura, 0 en el disco continuo y 2 avisos en los cambios en vivo, los dos al apretar «Escuchar sin
mejoras»:**

- *Semilla 33 (master aplastado, 88,2 kHz): falso aviso.* Quitar la presencia en esa canción la deja ~6 dB más fuerte y sus bordes
  se ven más empinados; el mayor salto entre muestras con el cambio (0,765) es casi el mismo que con «sin mejoras» desde el
  principio (0,752). La presencia se apaga con una rampa de 40 ms, sin escalón.
- *Semilla 108 (onda cuadrada): real pero minúsculo.* Con el des-recorte apagado desde el principio el aviso desaparece, así que es
  él: al apretar el botón se salta de golpe y la onda cuadrada (que para él es toda ella un recorte) pierde su parte reconstruida, un
  escalón de 0,016 (1,6 % del máximo), una vez. En música debería ser menor (solo repara picos recortados). Es una herramienta de
  diagnóstico: **no lo toco.**

**Buscando a propósito en los discos continuos** (unas 500 pasadas más, una clase de señal a la vez y con la subida forzada), sí
aparece un detalle real y pequeño: al empalmar dos pistas, **los últimos 3 ms de la primera pueden quedar hasta ~1 dB más bajos** de
lo que quedarían en la pista entera, y al empezar la segunda la ganancia vuelve de golpe (un escalón de hasta el 8 % del máximo,
0,056 a 0,08). Pasa porque, al acabar el archivo, el detector de pico real del limitador ve un corte seco (ceros) y «inventa» un pico
(el rebote de Gibbs) justo en los 3 ms de pre-escucha. Solo se ve si el limitador está bajando fuerte en ese instante: con el bajo
fuerte desde +6 dB de subida forzada (3 de 30 empalmes) y con barridos de tonos puros desde +12 dB (2 de 40). Con pop, voz, platillos
o master aplastado no sale ni a +18 dB, y tampoco en las 250 pasadas generales. En música un escalón de 1 dB lo tapa la propia
música: **no lo toco.** Si algún día molestara, se arregla en `PicoReal.cs` con unas diez líneas (al vaciar, dar al detector el
último cuadro en vez de ceros, solo cuando sigue otra pista del mismo disco).

**También miré, sin encontrar nada que arreglar:**

- *Las partes suaves* (`SalaLocal.cs`) miden también la banda entera, pero con una ventana de 0,4 s y una subida de 1,5 s: son lentas
  y no bombean con el bombo. No necesitan lo de P1.
- *Los MP3 sin huecos:* ya están (`RellenoMp3.cs`); no hay que recortar el retardo del codificador otra vez.
- *El fundido de salida* (lineal en amplitud, 0,5 s): se oye bien; uno logarítmico no se distinguiría en tan poco tiempo.

## 5. En el equipo del bar (sin tocar código)

Una cosa nueva y las que ya te dije, por si las quieres a mano:

1. **Sonidos de Windows en «Sin sonidos»** (Panel de control → Sonidos y dispositivos de audio → Sonidos → Combinación de sonidos).
   Desde el 24-10 la rocola pone el volumen general de Windows al máximo en cada arranque; si un aviso del sistema (un error, un USB
   que se conecta, un globo de notificación) suena, sale **al volumen del bar**. Y desmarcar «Reproducir sonido de inicio de Windows».
2. Lo del 25-10: calidad de conversión «Óptima» y aceleración «Completa» en las propiedades avanzadas de los altavoces, el panel de
   Realtek sin efectos de ambiente ni ecualizador, y la salida analógica como dispositivo predeterminado.
3. Lo de las fotos del 28-10: las bocinas en paralelo en un mismo borne cargan mucho al amplificador, el cable delgado a las cajas de
   fuera pierde grave, y las perillas del micrófono subidas meten ruido. Es de instalación; la cadena no lo arregla.

## 6. Mi orden

1. **D2** (el script de recoger): diez minutos, sin riesgo, y cambia lo que podré hacer la próxima vez.
2. **D1** (las columnas de actividad): una hora, solo escribe.
3. **P1** (la presencia sin el grave): si lo quieres oír; solo billar, a ciegas contra lo de ahora.
4. **T10**, cuando mires `[Cortes de sonido]` del bar y me los mandes.
5. **T2**, cuando llegue la tabla.

Mi opinión sincera: el procesamiento está casi saturado. Cada pieza nueva que se añade ahora sin oírla en el bar es adivinar. Lo
mejor que puede pasar es que unas noches de bitácora nos digan qué etapa sobra o falta.

Dime cuáles hago.

## Apéndice: cómo se midió (por si otra IA lo repite)

- **Banco de la presencia:** programa suelto que carga `Rocola.exe` por reflexión (como las baterías), arma la cadena con
  `ArmaCadena` con **solo** la presencia encendida (`normalizeOn` falso, ganancia 1, todos los demás mandos apagados, y con
  `PresenciaSala` forzada a las constantes del billar para las tablas del billar), pasa un WAV de coma flotante a 44,1 kHz, y mide con
  Goertzel la amplitud de un tono de la voz (440, 660 u 880 Hz según el banco) en ventanas de 25 ms cada 12,5 ms, de 2 s en adelante.
  «Sube y baja» es el percentil 95 menos el 5 de esa razón en dB. Los programas son: voz con sílabas a 2,6 Hz (330, 660, 990 y 1650
  Hz), bajo de 55 y 110 Hz cada 0,5 s, bombo de 48 a 103 Hz, charles de ruido filtrado, y una estrofa de 6 s al 45 % seguida de un
  coro de 6 s al 100 %.
- **El prototipo de P1** (el único cambio, ~35 líneas en `PresenciaSala.cs`): un filtro RBJ de segundo orden con Q = 0,7071 en
  150 Hz; en `Proceso`, en vez de `sm += q;` se calcula `yh` con el filtro (estado por canal: `x1, x2, y1, y2`) y se hace
  `sm += 1,995 * yh * yh;` (3 dB), dejando `if (q > mx) mx = q;` como estaba; en `Hereda`, copiar el estado del filtro de la anterior.
- **El banco aleatorio:** `Form(...)` arma el `Form1` sin encender (`Codigo.FormSinEncender`), sortea la configuración desde la
  semilla, mide la sonoridad (`MideIntegrada`), abre con `OpenAudio` y arma con `ArmaCadena`. Para los cambios en vivo se llama a
  `AplicaBocinas` (o a `AplicaSinMejoras`) en un bloque al azar y se compara con la misma lectura sin tocar (salto entre muestras
  frente a 2,5 veces el salto local de la lectura sin tocar). Para el disco continuo se arman dos pistas y se llama a `SigueA` y a
  `HeredaLocal` como el relevo de verdad. Si se quiere dejar como batería, hay que quitar de los cambios en vivo la comprobación del
  salto con «Escuchar sin mejoras» en ondas con bordes (los dos avisos de arriba).
