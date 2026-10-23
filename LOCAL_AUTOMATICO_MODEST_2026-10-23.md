# El local en automático — Modest, 23 de octubre de 2026

Petición del dueño del local (23-10-2026): un modo automático de las mejoras del local, que se active y se configure solo para dar el mejor sonido.
Decisiones del dueño: decide **por el tipo de local y por cada canción**, y los mandos manuales **se ponen en gris** mientras está encendido.

**Lo medido es sintético (Linux/Mono). Nada se ha oído en el bar ni probado en Windows XP.** Qué tan bien suena cada tipo de local solo se sabe allí.

## Qué hace

Un interruptor, **Ctrl+F3 → Sonido → El local y sus bocinas → Tono y presencia del local → «Modo automático del local»**, apagado de fábrica
(`local_auto=`), y un botón que cambia el **tipo de local** (`tipo_local=`: 0 billar, 1 sala buena, 2 bocinas chicas).
Sin micrófono la rocola no oye el local, así que decide con dos cosas que sí sabe:

| Tipo de local | Fijo para todas las canciones | Por canción |
| --- | --- | --- |
| Bocinas repartidas del billar | sin subgrave, estéreo estrecho **solo donde hace falta** (S3), claridad, **partes suaves siempre** (P1, 24-10) | presencia desde 10 LU de rango de sonoridad (P2, 24-10) |
| Sala con bocinas buenas | nada: el tono no se toca | partes suaves solo desde 14 LU de rango de sonoridad |
| Bocinas chicas | sin subgrave, claridad, +1 dB de agudos, **partes suaves siempre** (P1) | nada (sin presencia) |

**Cambio del 24 de octubre de 2026** (el dueño eligió P1, P2 y P6 de `MODOS_AUTOMATICOS_SONIDO_MODEST_2026-10-23.md`). Antes decidía el «rango» de cada canción = P99 de sus picos menos su sonoridad, que mide picos contra promedio y no cuánto se calla la canción (un rock de golpes secos pasaba los cortes sin tener nada suave; un pop con intro bajita, no). Ahora:

- **Partes suaves, siempre** en el billar y en las bocinas chicas. La etapa ya se frena sola por nivel (solo sube lo que va 12 dB bajo el objetivo, hasta 4 dB): en una canción pareja mide 0,0 dB, en una con intro suave sube hasta +3,9 dB.
- **Presencia (billar) y partes suaves de la sala** según el **rango de sonoridad (LRA, EBU Tech 3342)**: cuánto varía el volumen de la canción en el tiempo (ventanas de 3 s con ponderación K, puertas de −70 LUFS y de −20 LU, percentiles 10 y 95), que el repaso guarda en `rocola_sonoridad.txt` (algoritmo 4). Cortes: **10 LU** la presencia del billar, **14 LU** las partes suaves de la sala (ayuda más parca, como antes con 16 dB de picos). Las bocinas chicas y la sala buena no llevan presencia (como antes). Una balada o una canción con intro suave ronda 11 a 18 LU; un tema parejo, menos de 3.
- **Medidas de antes (sin LRA)**: siguen sirviendo para nivelar igual y, hasta que el repaso las vuelva a medir en segundo plano (con su presupuesto acotado de siempre, sin saturar los dos núcleos), el modo usa la regla vieja de P99 − sonoridad: presencia del billar y partes suaves de la sala desde 16 dB. Una canción sin medir recibe solo lo fijo del tipo (y las partes suaves en billar y chicas).
- Con el modo **apagado** (de fábrica) la cadena sale bit a bit como antes: `LocalDe` devuelve los mandos tal cual.

## Con el automático encendido

Los siete mandos que decide (subgrave, estéreo, claridad, partes suaves, graves del local, agudos del local, presencia) quedan **en gris**, y su nota dice
«Necesita: el modo automático del local apagado (Tono y presencia)». Lo que habías guardado en ellos se conserva y vuelve al apagarlo. Se oye al instante en la canción
que suena y en la siguiente ya preparada; cada canción se decide al armarla. «Escuchar sin mejoras» sigue quitándolo todo un rato; «Dejar como de fábrica» lo apaga y vuelve al billar.

## Cómo está hecho

`Rocola/LocalAuto.cs`: `LocalAutomatico(tipo, rango)` (pura, sin estado) y `LocalDe(cancion)`, que devuelve los mandos tal cual (automático apagado) o lo que decide.
Los tres sitios que antes leían los mandos sueltos (armar la cadena en `Form1.Sonido.cs`, `AplicaBocinas` en `GraveMono.cs` y `AplicaSinMejoras` en `LoQueSuena.cs`) leen ahora `LocalDe`.
**Apagado, la cadena sale como antes**: `LocalDe` devuelve exactamente los mandos, y las etapas reciben lo mismo que recibían.

## Pruebas

`localauto` (27 comprobaciones): la tabla por tipo y rango (incluidos los umbrales justos y valores imposibles), apagado = mandos tal cual con cualquier valor, encendido con medidas integradas reales
(rango 18 y 8 dB, medida vieja, sin medir), que lo guardado no se pierde al apagarlo, que la cadena, los mandos en vivo y «Escuchar sin mejoras» usan `LocalDe`, y la configuración y la ventana.
`ventanaf3` (7.581 comprobaciones, todas las páginas y hojas) sigue limpia con los controles nuevos. No se hizo una captura de la hoja nueva.

## Qué escuchar en el bar

1. Elige el tipo de local y enciende el modo: debería sonar como con las mejoras que ya usabas a mano, sin que toques nada.
2. Una balada o una canción clásica seguida de un tema muy comprimido: la primera no debe perderse en el ruido, la segunda no debe sonar más fuerte ni aplastada.
3. Si una canción de mucho rango suena «bombeada» (presencia), pasa a «Sala con bocinas buenas» o apaga el modo y usa los mandos a mano.
4. Mira la distribución real de rangos de tu biblioteca cuando el repaso la haya medido; mira el reparto del rango de sonoridad (LRA) en rocola_calidad.txt: si casi todo cae sobre 10 LU, el corte de la presencia hay que subirlo.
