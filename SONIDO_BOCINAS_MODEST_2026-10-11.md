# Las bocinas del billar: subgrave fuera y estéreo más estrecho — Modest, 11 de octubre de 2026

Con las 7 fotos del sistema de sonido (9 y 10 de octubre) se cierran los dos pendientes del apartado 3.H de
`MEJORAS_AUDIO_Y_PENDIENTES_2026-09-22.md`, que esperaban saber qué bocinas son.

## Lo que se ve en las fotos

- **Gabinete de la rocola** (X5 Platinum):
  - 2 medios-graves de 6,5" GVT SPB2327, de 8 Ω;
  - 2 tweeters Mitzu TW-4700, de 4 Ω;
  - el monitor y el monedero;
  - un amplificador de dos canales (A y B) con entradas de línea (IN A, IN B) y dos de micrófono, cada una con su
    perilla LEVEL.
- **Fuera, 3 cajas**, cada una en una zona:
  - junto al arco de ladrillo: dos vías, cono de ~12", corneta y dos puertos;
  - en la sala del fondo, en la pared verde: una caja negra de ~10–12";
  - en el entrepiso, sobre un carrete: de dos o tres vías, ~12".
- **Ningún subwoofer.**
- **El local:** techo de lámina, ladrillo y piso de loseta. Mucho rebote.

## Lo que cambia en el programa (automático, en la etapa del grave en mono)

### 1. El subgrave fuera (30 Hz)

Por debajo de ~30 Hz ninguna de esas bocinas da nada que se oiga. En las cajas con puerto, por debajo de su sintonía
el cono se queda sin freno: ese grave solo gasta amplificador y mueve los conos de más. Ahora se quita con un
paso-alto de cuarto orden en los dos canales.

Medido con tonos por la etapa de verdad (exactamente un Butterworth de cuarto orden):

| 15 Hz | 20 Hz | 25 Hz | 30 Hz | 40 Hz | 50 Hz | 60 Hz y más |
| --- | --- | --- | --- | --- | --- | --- |
| −24,1 dB | −14,3 dB | −7,2 dB | −3,0 dB | −0,4 dB | −0,07 dB | 0 |

El bajo que se oye (de 50 Hz para arriba) no cambia. El grave sintético y el limitador vienen detrás, así que el
limitador deja de gastar margen en un grave que nadie oye.

### 2. El estéreo al 60 %

Cada zona oye sobre todo **su** bocina, o sea un solo canal. Un instrumento grabado todo a un lado (pasa en muchas
grabaciones viejas) desaparecía en la zona del otro canal. Ahora el lado (L − R), por encima del corte del grave en
mono, va al 60 %:

- lo que va todo a la izquierda sale por la derecha **12 dB más bajo**, en vez de nada, y por la izquierda a −1,9 dB;
- lo del centro (voz, bajo, bombo, casi todo) sale igual, con los dos canales idénticos.

### Configuración (`rocola_config.txt`)

| Clave | De fábrica | Qué hace |
| --- | --- | --- |
| `subgrave=` | `30` | Hz del corte; `0` lo quita (entre 15 y 60) |
| `ancho_estereo=` | `60` | % del estéreo; `100` lo deja como viene, `0` mono del todo |
| `grave_mono=` | `120` | Como antes: por debajo, el mismo grave por todas las bocinas |

Con `subgrave=0` y `ancho_estereo=100` la etapa sale bit a bit como antes.

Siguiendo a `grave_mono=`, no llevan interruptor en Ctrl+F3. Si lo quieres en la pestaña Sonido, se añade.

Coste: 1,7 ms por segundo de música en este equipo (0,17 % de un núcleo). Sin memoria nueva al leer. Las pistas
seguidas del mismo disco heredan los filtros, sin golpe en el cambio. En el silencio los filtros quedan en cero
exacto, sin números desnormales (que en un procesador viejo son muy lentos).

No se quitó el grave de 50–80 Hz: las cajas de 12" sí lo dan.

## Lo que el programa no puede arreglar: para revisar en el equipo

Visto en las fotos; conviene comprobarlo en persona:

1. **Las perillas de micrófono están subidas sin micrófono conectado.** Las dos LEVEL rojas junto a las entradas MIC
   están a media vuelta o más. Sin micrófono solo suman ruido o zumbido. Si no se usan, a mínimo.
2. **IN A e IN B no están a la misma altura.** Las perillas amarillas apuntan a sitios distintos, así que un canal
   suena más fuerte que el otro. Como cada zona oye un canal, una zona queda más baja. Para igualarlas, poner una
   canción en mono (o una voz sola) y ajustar hasta que las dos zonas suenen igual.
3. **Los tweeters del gabinete (4 Ω) necesitan su condensador.** Si van conectados directo al amplificador, reciben
   todo el grave: distorsionan y se queman. En una foto parece haber una pieza pequeña en el cable de uno, pero no se
   distingue bien.
4. **La carga del amplificador.** En cada canal pueden ir en paralelo un medio-grave de 8 Ω, un tweeter de 4 Ω y una o
   dos cajas de fuera. Eso puede bajar de lo que aguanta el amplificador. Si se calienta mucho, se corta o distorsiona
   a volumen alto, es por eso: revisar los ohmios por canal.
5. **La polaridad.** Ahora el grave sale igual por todas las bocinas. Si alguna está conectada al revés (rojo con
   negro), cerca de ella el bajo se cancela con la de al lado. Basta revisar que en todas vaya + con +.

## Cómo se comprobó

- **Pruebas automáticas (Linux/Mono):**
  - batería nueva `subgrave`, 37 comprobaciones: apagado bit a bit como antes, la curva del corte, el ancho a un lado
    y al centro, un canal, pistas seguidas, silencio sin desnormales, configuración, la cadena de cada canción y que
    leer no reserve memoria;
  - siguen pasando `gravemono` (41), `todojunto`, `silencios`, `sinpausa`, `transiciones` y `audioestereo` en los dos
    perfiles.
- **No comprobado:** escucharlo en el billar. Para notarlo, caminar por las tres zonas con una canción vieja de
  instrumentos muy abiertos a los lados; deberían oírse en todas. Si prefieres más o menos estéreo, cambia
  `ancho_estereo=`.
