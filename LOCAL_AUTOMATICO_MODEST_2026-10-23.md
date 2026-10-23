# El local en automático — Modest, 23 de octubre de 2026

Petición del dueño del local (23-10-2026): un modo automático de las mejoras del local, que se active y se configure solo para dar el mejor sonido.
Decisiones del dueño: decide **por el tipo de local y por cada canción**, y los mandos manuales **se ponen en gris** mientras está encendido.

**Lo medido es sintético (Linux/Mono). Nada se ha oído en el bar ni probado en Windows XP.** Qué tan bien suena cada tipo de local solo se sabe allí.

## Qué hace

Un interruptor, **Ctrl+F3 → Sonido → El local y sus bocinas → Tono y presencia del local → «Modo automático del local»**, apagado de fábrica
(`local_auto=`), y un botón que cambia el **tipo de local** (`tipo_local=`: 0 billar, 1 sala buena, 2 bocinas chicas).
Sin micrófono la rocola no oye el local, así que decide con dos cosas que sí sabe:

| Tipo de local | Fijo para todas las canciones | Por canción (según su rango) |
| --- | --- | --- |
| Bocinas repartidas del billar | sin subgrave, estéreo estrecho **solo donde hace falta** (S3), claridad | partes suaves desde 12 dB de rango; presencia desde 16 dB |
| Sala con bocinas buenas | nada: el tono no se toca | partes suaves solo desde 16 dB |
| Bocinas chicas | sin subgrave, claridad, +1 dB de agudos | partes suaves desde 12 dB |

El **rango** de una canción es el P99 de sus picos menos su sonoridad integrada (los dos de la medida del repaso). Un disco de pop actual ronda 6 a 10 dB;
una balada, clásica o grabación sin comprimir, 14 o más. Esas son las que pierden sus partes bajitas en el ruido del local. Una canción **sin medir o con una
medida vieja (sin picos)** solo recibe lo fijo de su tipo. Los graves y el balance de la hoja no se tocan: el balance depende de las perillas del amplificador.
Los umbrales (12 y 16 dB) son una elección de oído, **no salen de la biblioteca real** de este local: `RangoAltoDb` y `RangoMuyAltoDb` en `LocalAuto.cs`.

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
4. Mira la distribución real de rangos de tu biblioteca cuando el repaso la haya medido; si casi todo cae sobre 12 dB, los umbrales hay que subirlos.
