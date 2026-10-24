# Los modos automáticos del sonido — revisión, Modest, 23 de octubre de 2026

Petición del dueño: «En la configuración de sonido, varias cosas tienen su modo automático, revisa todos esos modos automáticos a ver si no se les pueden hacer mejoras.»

**Límites.** Todo lo medido es sintético (Linux/Mono, señales generadas; `pruebas/modosauto.cs`). Nada se oyó en el billar ni se midió en Windows XP / el A4. Los tiempos de CPU de Mono no son los del A4.
Lo que cambia lo que se oye **no se implementó**: va en las propuestas de abajo para que elijas.

## 1. Los modos y cómo deciden

Orden de la cadena: arreglos (S1, S2) → des-recorte → siseo (I2) → DSEE → nivelado, realce y EQ (`LevelTap`) → modo noche → grave en mono, subgrave y estéreo (S3) → local (claridad, partes suaves, graves, agudos, balance) → presencia → bajo psicoacústico → limitador → fundido.

| Modo | Qué decide | Con qué datos | Dónde |
| --- | --- | --- | --- |
| Volumen de los videos – Auto (`video_automatico=`) | el volumen base de los videos | la sonoridad estimada de los videos (≥ 8 medidos) y el objetivo del nivelado; el 15 % más flojo se da por perdido, mínimo 40 | `Form1.Video.cs` (no pasan por la cadena: lo pone Windows) |
| EQ automático (`eq_automatico=`) | la fuerza del EQ (35 % con 10 canciones medidas → 85 % con 60; ×0,88 sin modelo) y su objetivo por canción | huella tonal de 8 bandas de cada canción, promedio de la biblioteca, red `rocola_eq_ia.bin` (si hay), «canción rara» baja la fuerza hasta 0,45× | `Form1.Datos.cs` `EqFuerzaAuto`, `EqCorrFor` → `LevelTap` |
| Graves – Auto (`bajo_automatico=`) | cuánto bajo psicoacústico: 30 % de partida, ×0,75 si viene recortada, ×1,2 si le cortaron agudos, y un factor por canción (0,4–1,7) según su grave frente a la biblioteca | huella tonal, recorte, corte de agudos y corte de graves | `BajoAuto`, `BassFactorFor` → `DeepBassProvider` |
| S3 estéreo estrecho solo si hace falta (`estereo_auto=`) | estrecha (al `ancho_estereo`) solo si el lado supera el 60 % del medio, vuelve bajo 35 % | energía del lado y del medio sobre 120 Hz en 3 s, al escuchar | `GraveMono.cs` |
| Modo automático del local (`local_auto=`, `tipo_local=`) | subgrave, estéreo+S3, claridad, partes suaves, presencia, +1 dB de agudos según el tipo | tipo de local y «rango» de la canción = P99 de picos − sonoridad integrada (cortes 12 y 16 dB) | `LocalAuto.cs` |
| Sin interruptor: nivelado, nivel por disco, DSEE, I1, S1/S2 | ganancia a un objetivo con topes por picos; un canal mudo / corriente continua solo en las canciones que lo traen | sonoridad BS.1770 y picos del repaso | `Sonoridad.cs`, `NivelDisco.cs`, `ArreglosGrabacion.cs` |

## 2. Defectos encontrados y corregidos

Cada uno con prueba en `pruebas/modosauto.cs` que **falló en el ejecutable anterior** (9 fallos) y pasa ahora.

1. **S3 en gris no decía la verdad con el modo del local** (`Form1.Ajustes.cs`, mando «Estéreo estrecho solo si hace falta»). Con el modo automático encendido, S3 a mano se ignora (el modo manda y lo fuerza en el billar), pero el mando seguía activo; y si el estéreo a mano estaba apagado decía «Necesita: Estéreo al 60 %», falso. Ahora va en gris con «Necesita: el modo automático del local apagado (Tono y presencia)» y lo guardado se conserva (regla del 23 de octubre).
2. **La barra de los videos en gris cuando mandaba ella** (`Form1.Ajustes.cs`, `Form1.Video.cs`). El Auto de fábrica dejaba la barra en gris aunque el programa no eligiera nada: sin nivelado, con menos de 8 videos medidos, o **en Windows XP, donde los videos no se miden nunca** (`videoSinMedir`), de modo que en la rocola real la barra del volumen de los videos era intocable y el texto decía «manda la barra». Ahora solo va en gris si el automático de verdad elige (`VideoAutoManda`); el volumen que suena es el mismo de siempre. Los textos también se corrigieron.
3. **«Escuchar sin mejoras» no valía para una canción armada mientras dura** (`Form1.Sonido.cs` `ArmaCadena`). La siguiente preparada (a 12 s del final) o una canción nueva nacía con sus mejoras puestas mientras el botón decía «sin mejoras». Ahora `ArmaCadena` termina con `AplicaSinMejoras(c)` (sin el modo, no cambia nada).
4. **«A la medida» (I1) sin modelo del EQ no decía que no podía actuar** (`Form1.Ajustes.cs`, `AjustesVivos.cs`). Sin `rocola_eq_ia.bin` o con menos de 10 canciones medidas no cambia nada (`MetaDeCancion` da null) pero estaba activo. Ahora en gris, con el motivo en su nota; lo guardado se conserva.

Sin defecto (comprobado): guardar y leer la configuración de los cinco modos da lo mismo y los valores fuera de rango (500, −3, NaN, infinito, texto, tipo 9) se acotan o ignoran; las lecturas de `integCache` desde `RangoDeCancion` son del hilo de pantalla, igual que las escrituras (no hay carrera; una prueba artificial con otro hilo sí fallaba, pero ese caso no existe). «Dejar como de fábrica» ya restaura todo.

## 3. Mejoras sin cambio audible hechas

- **Reparto de rangos en `rocola_calidad.txt`** (`LocalAuto.cs` `TextoRangos`/`RangosMedidos`, se escribe al abrir Ctrl+F3): cuántas canciones caen por franja (<6, 6–8 … ≥16 dB), mediana, P90, y cuántas pasarían los cortes de 12 y 16 dB. Es lo que hacía falta para calibrar el modo del local con la biblioteca real (el documento del 23 de octubre pedía mirarlo pero no había dónde).
- **Prueba de bit a bit**: `pruebas/compara-cadena.cs` carga el ejecutable anterior y el nuevo y pasa la misma canción por `OpenAudio + ArmaCadena` en 8 combinaciones de ajustes (fábrica, modo del local en sus tres tipos, local a mano, estéreo+S3+subgrave, DSEE+I1+siseo, videos/EQ/graves a mano) con canción medida y sin medir: **16 de 16 idénticas bit a bit** (contra el ejecutable de esa revisión, antes de P1, P2 y P6).

## 4. Lo medido y las propuestas para que elijas

Medidas con `mono modosauto.exe rocola.exe medidas` (4 canciones sintéticas de 24 s; «rango» = lo que usa el modo del local; «LRA aprox.» = rango de sonoridad en el tiempo, EBU 3342 sin ponderación K):

| canción | sonoridad | rango (P99−son.) | LRA | ¿pasa 12 / 16? | partes suaves: sube hasta | presencia: sube / baja hasta |
| --- | --- | --- | --- | --- | --- | --- |
| pop comprimido | −10,3 | 6,6 | 0,1 | no / no | 0,0 dB | −0,6 / −1,2 dB |
| pop con intro suave | −10,8 | 7,0 | 11,2 | no / no | **+3,9 dB** | +3,0 / −0,9 dB |
| rock con golpes | −17,4 | **16,7** | 0,2 | **sí / sí** | 0,0 dB | −0,8 / −1,2 dB |
| balada de oleadas | −20,4 | 10,1 | **18,3** | no / no | +1,8 dB | +3,0 / −2,4 dB |

**El «rango» del modo del local mide picos contra promedio, no cuánto se calla la canción.** El rock con golpes (nada suave) pasa los dos cortes; el pop con intro suave y la balada, que son para quienes existen las partes suaves, no pasan ninguno. Las partes suaves ya se frenan solas por nivel (en el rock no suben nada).

### Propuestas (audibles; no implementadas)

**P1 — Partes suaves siempre encendidas en «billar» y «bocinas chicas» (RECOMENDADA).**
- Qué haría: dejar de condicionarlas al rango; la etapa ya sube solo lo que está 12 dB bajo el objetivo (máx. 4 dB, 1,5 s).
- Por qué: con los datos de arriba, el corte actual las da a quien no las usa y se las quita a quien sí.
- Se oiría: intros y estrofas bajitas de canciones comprimidas suben hasta ~4 dB; el resto, igual (0,0 dB medido).
- Coste: +≈ 7 ns por muestra en Mono (22,5 → 29,4 ns de la etapa del local); en el A4 se mediría. Riesgo: una voz que baja en un tema fuerte se oirá más cerca de lo grabado; se apaga con «Sala con bocinas buenas» o el modo manual.
- Cómo se comprobaría: ampliar `localauto` y `modosauto`; escuchar un pop con intro suave y una balada.
- En el billar escuchar: intros de pop, estrofas calladas; que no «respire» lo fuerte.

**P2 — Presencia por rango de sonoridad real (LRA) en vez de P99−sonoridad.**
- Qué haría: guardar en la medida del repaso un LRA (algoritmo 4) y encender la presencia con LRA ≥ 10 LU.
- Por qué: la presencia a una canción ya fuerte y sin partes suaves no suma nada y la **baja 1,2 dB** (no hay margen bajo el techo para devolverlo): el rock de arriba saldría ~1 dB más bajo que sus vecinas.
- Se oiría: baladas y pops con estrofa suave suben ~3 dB en lo bajito; rock/pop plano sin cambio.
- Coste: ≈ 40 bytes más por canción y volver a medir la biblioteca (el repaso, en segundo plano); riesgo medio (cambia el formato de la medida; los ejecutables anteriores guardan las nuevas como ajenas).
- Escuchar: baladas y temas con silencios; la sonoridad entre canciones seguidas.

**P3 — Mover los cortes 12/16 dB** solo después de ver el reparto real en `rocola_calidad.txt` (ya se escribe). Sin cambio hasta que mires la sección «RANGO DE LAS CANCIONES». Si se hace P1/P2, los cortes dejan de usarse.

**P4 — Los videos siguen «Fuerza del nivelado».** Hoy un video medido se nivela al 100 % aunque la fuerza esté en 50 % o 0 % (a 6 dB bajo el objetivo: video +6 dB; canción igual +3 dB a 50 % y 0 dB a 0 %). Propuesta: elevar el factor a la fuerza como en las canciones. Cambia el volumen relativo video/canción solo si cambias la fuerza (de fábrica es 100 %: sin cambio). Riesgo bajo; en XP no hay videos medidos, así que ahí no tendría efecto. Recomendada solo si usas la fuerza distinta de 100 %.

**P5 — Compensar la sonoridad que mueve el local.** El nivelado se calcula sobre la canción original y el local la mueve (LU frente a fábrica, pop comprimido / balada): modo billar o claridad −0,7 / −0,5; presencia −0,7 / −1,7; graves del local +6 dB +2,0 / +1,0; agudos +6 dB +0,3 / +0,1; subgrave fuera 0,0 / −0,1; bajo a 60 % −0,1 / −0,1. Propuesta: devolver la claridad (≈ +0,6 dB, constante y sin riesgo de picos más allá del limitador). Se oiría 0,7 dB más fuerte con claridad, igual que sin ella. Baja prioridad; el techo del limitador manda.

**P6 — Rampas al encender/apagar estéreo estrecho o subgrave en vivo** (también al cambiar el tipo de local con una canción sonando). Medido: el salto entre muestras seguidas sube hasta +6 / +8 dB (estéreo encender/apagar) y +16 dB (apagar el subgrave) sobre lo normal, un «tic» de unas centésimas. Solo ocurre al tocar el mando, nunca solo. Arreglo: rampa de 20–30 ms (el estado estable queda bit a bit igual). Riesgo: toca el bucle caliente de `GraveMono`. Baja prioridad.

### Medido y descartado (sin propuesta)

- **EQ automático + I1 ¿doble corrección?** Medido por la cadena con ruido rosa: el EQ solo (+3,4 dB en 10 y 15,8 kHz) da +3,0 dB sobre 8 kHz; el realce +25 % de I1 suma solo +0,2 dB más. Existe el solapamiento, pero es inaudible: no vale la pena tocarlo.
- **Graves automáticos frente al subgrave quitado:** el bajo psicoacústico añade −40,4 dB sin subgrave y −42,0 dB con él (1,6 dB menos). No se pelean.
- **S3 arranca ancho en cada canción:** empieza a estrechar a los 0,1 s y llega al 60 % al segundo: demasiado rápido para notarse.
- **I1 + EQ en el bajo:** el EQ recorta y el bajo psicoacústico repone; es el diseño, no un choque.

## 5. Pruebas

`pruebas/modosauto.cs` (nueva, en `todo.sh` y `LEEME.md`): configuración, grises de la ventana real, «sin mejoras» en la cadena armada, reparto de rangos y las medidas (con el argumento `medidas`). Corridas con `TMPDIR=/tmp sh todo.sh modosauto localauto salalocal bajoauto gravemono presenciasala audioestereo sonoridad nivelpicos discoparejo ventanaf3 ajustesvivos ajustes calidad todojunto sinpausa arreglos`: todo en verde, los dos perfiles con 0 avisos. `pruebas/compara-cadena.cs` (herramienta, no entra en `todo.sh`, como `compara-local.cs`).

## 6. Hechas por decisión del dueño (23 de octubre de 2026): P1, P2 y P6

Base: la rama con este documento ya integrado (5a5a32b). Con `local_auto=` apagado (de fábrica) la cadena sale **bit a bit** como antes: `pruebas/compara-cadena.cs` contra el ejecutable de 5a5a32b salen **14 iguales y 2 distintas a propósito** (billar y bocinas chicas «sin medir», que ahora llevan partes suaves por P1), y 0 distintas inesperadas; las 6 combinaciones con el modo automático apagado (fábrica, local a mano, estéreo+S3+subgrave, DSEE+I1+siseo, videos/EQ/graves a mano), con canción medida y sin medir, son idénticas bit a bit. Lo que se sostiene es eso: con `local_auto=` apagado, todo sale igual. Nada de esto lleva interruptor propio (P1 y P2 solo tocan lo que decide el modo del local; P6 es automática).

### P1 — partes suaves siempre en «billar» y «bocinas chicas»
- `LocalAuto.cs`: `Suaves = true` en los dos tipos. La sala con bocinas buenas queda con las partes suaves solo desde 14 LU de rango de sonoridad (ver P2); antes, desde 16 dB de picos. Nota de la interfaz (`Tono y presencia`) y tabla de `LOCAL_AUTOMATICO_MODEST_2026-10-23.md` actualizadas.
- Medido (4 canciones sintéticas): pop comprimido 0,0 dB, rock con golpes 0,0 dB, balada +1,8 dB, pop con intro suave **+3,9 dB** (la subida máxima en una ventana de 1 s). Coste ≈ +7 ns por muestra en Mono sobre los 22,5 ns de la etapa del local.
- Pruebas: `localauto` (billar y chicas llevan partes suaves con poco rango, mucho, sin medir y NaN); fallaba en el ejecutable anterior (3 fallos en la tabla).

### P2 — presencia (y partes suaves de la sala) por rango de sonoridad real, LRA
- **Qué se midió**: `MedidorBs1770.Lra` (`Sonoridad.cs`): corto plazo de 3 s con ponderación K cada 100 ms (anillo de 30 subbloques, solape de 2,9 s), puerta absoluta −70 LUFS, relativa −20 LU, percentiles 10 y 95 de un histograma fijo de 0,1 LU (800 casillas: la memoria no crece con la canción). Menos de 3 s o muda: sin LRA.
- **Contraste**: los 4 casos de EBU Tech 3342 dan **10,00 / 5,00 / 20,00 / 15,00 LU** (piden 10, 5, 20 y 15 ±1). Contra `pruebas/ref_lra.py` (Python, ponderación K de la tabla de la norma, sin histograma, percentiles interpolados): estéreo 48 y 44,1 kHz, mono, 5.0 a 32 kHz, estéreo 96 kHz y casi parejo, diferencias de 0,00 a 0,09 LU (tolerancia 0,25 a 0,30). Da lo mismo se lea como se lea (de una muestra, de 777, de golpe).
- **Decisión**: billar, presencia con LRA ≥ **10 LU**. Sala con bocinas buenas, partes suaves con LRA ≥ **14 LU** (más parca, como antes con 16 dB). Bocinas chicas y sala sin presencia (como antes: la presencia aprieta lo fuerte y las bocinas chicas ya van justas; la sala buena no la necesita). Con los datos sintéticos: pop con intro suave 11,2 LU → sí; balada 17,7 → sí; rock con golpes 0,2 → no (antes sí: lo bajaba 0,8 a 1,2 dB); pop comprimido 0,1 → no.
- **Formato** (`rocola_sonoridad.txt`): **algoritmo 4** = el 3 más un campo `LRA` (a la décima, `-` si no hay) antes de la ruta. Se leen todas las de antes (1, 2 y 3) igual que siempre y siguen **sirviendo para nivelar con los mismos números** (probado: misma raíz de potencia y mismos picos). Un registro de un algoritmo posterior (5…) se conserva intacto; un LRA negativo, no numérico o absurdo se salta. **Un ejecutable anterior (5a5a32b) lee el archivo nuevo sin romperse**: usa las medidas de 3 y guarda las de 4 intactas como ajenas (probado con `ROCOLA_EXE_ANTERIOR`); eso sí, mientras corra, las canciones ya re-medidas no le sirven para nivelar (cae a la estimación), como pasó del 2 al 3.
- **Re-medición**: las medidas de 1 a 3 no se tiran. Lo que se mide de nuevo (canciones nuevas, o las que el repaso hace por su cuenta) guarda el LRA siempre. Las completas de antes (algoritmo 3) **solo se piden de nuevo mientras `local_auto=` está encendido** (`TandaIntegrada`, probado: apagado, 0 pedidas por el LRA y el repaso se comporta como en 5a5a32b; encendido, sí), en el siguiente repaso (al arrancar o con «Repasar la biblioteca a fondo»), en segundo plano y con el presupuesto acotado de siempre. Mientras no estén re-medidas, el modo usa la regla de antes (P99 − sonoridad, 16 dB). Con el modo encendido cuesta decodificar otra vez cada canción una vez; el LRA en sí suma entre −0,02 y +0,38 ns por muestra en Mono según quién lo mida (13,9 → 14,3 ns en mi corrida; −0,02 en la de la revisión): está dentro del ruido de la medición, y como mucho unos 8 ms por una canción de 4 minutos en estéreo, nada frente a decodificarla. En el A4 no se midió.
- `rocola_calidad.txt` trae ahora el reparto del LRA (franjas, mediana, cuántas llevarían presencia en el billar y partes suaves en la sala, y cuántas medidas faltan por volver a medir).
- Pruebas: batería nueva `lra` (sus comprobaciones; sin `ROCOLA_EXE_ANTERIOR` o sin numpy imprime un AVISO que no cuenta como comprobación), `localauto` y `modosauto` ampliadas, `sonoridad` y `compatvieja` siguen verdes.

### P6 — sin chasquido al tocar estéreo estrecho o subgrave en vivo
- `GraveMono.cs`: el ancho que se aplica sigue al pedido en una rampa de 25 ms; el subgrave entra y sale mezclando lo filtrado con lo que entra en esos 25 ms (al apagarlo, el filtro sigue hasta acabar la mezcla). Cubre también el cambio de tipo de local con la canción sonando. Sin interruptor.
- Salto máximo entre muestras seguidas sobre el normal (60 instantes de cambio distintos, `gravemono` sección 6b):

| mando | antes | ahora |
| --- | --- | --- |
| estéreo estrecho, encender | +6,1 dB | −0,3 dB |
| estéreo estrecho, apagar | +8,2 dB | −0,3 dB |
| subgrave, encender | +0,2 dB | −0,3 dB |
| subgrave, apagar | **+15,9 dB** | +0,0 dB |

- Estado estable: tras 100 ms el estéreo sale **bit a bit** como con el mando puesto desde el principio, el subgrave apagado deja exactamente lo que entra, y con los mandos quietos el camino es el de siempre (sin reservas de memoria; costó ≈ 1,1 ns por cuadro más en Mono, 22,5 → 23,9 ns, unos 0,05 ms por segundo de música).

### Qué escuchar en el billar
1. Modo automático del local encendido, tipo billar: una canción con intro o estrofa bajita (balada, pop con intro suave) debe quedar más cerca del ruido de la sala; un tema parejo no debe sonar distinto. Un rock fuerte y parejo ya no debe sonar ~1 dB más bajo que sus vecinas.
2. Mientras el repaso re-mide la biblioteca, algunas canciones decidirán por la regla vieja y otras por el LRA; el cambio es gradual y queda registrado en `rocola_calidad.txt` («faltan N»).
3. Con una canción sonando, enciende y apaga «Estéreo» y «Quitar el subgrave» y cambia el tipo de local: no debe haber «tic».
4. Si la presencia se oye de más o de menos, el corte es `LraPresenciaLu` en `LocalAuto.cs`; el reparto de tu biblioteca está en `rocola_calidad.txt`.

Límites: todo sintético en Mono/Linux; no se oyó en el bar ni se midió en XP/A4.
