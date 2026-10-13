# El sonido ya no se atasca en los silencios — Modest, 12 de octubre de 2026

Petición del usuario: «busca optimizaciones de rendimiento».

Es una optimización que no cambia ni el sonido ni lo que se ve. Por eso es automática y sin interruptor, como pide
AGENTS.md. Vale para las dos ramas del código (con y sin tarjeta); no toca el dibujo por CPU.

## Lo que se encontró

Medí cuánto gasta cada etapa de la cadena de sonido por cada segundo de canción, con la cadena de verdad y lo de
fábrica. Con música, la cadena entera gasta unos 15 a 19 ms por segundo (aquí), repartidos sin nada raro.

**En el silencio exacto gastaba siete veces más.** Cuando la música se calla del todo, cada filtro con memoria se va
apagando sin llegar nunca a cero. Acaba en números tan pequeños («desnormales») que el procesador los calcula decenas
de veces más despacio. Y lo que sale de ellos también es diminuto, así que el problema baja por toda la cadena.

Canción de prueba con un silencio largo, de fábrica pero sin saltar el silencio, en ms por segundo de canción:

| Etapa | Antes | Ahora |
| --- | --- | --- |
| Grave en mono | 22,6 | 0,8 |
| Medida del original (sonoridad y huella del EQ) | 22,7 | 1,2 |
| Nivelado, EQ y realce | 12,9 | 5,6 |
| Bajo psicoacústico | 12,1 | 0,7 |
| Modo noche | 12,0 | 0,8 |
| Limitador | 6,7 | 1,6 |
| Paso a 16 bits | 4,7 | 1,2 |
| **Toda la cadena** | **94,9** | **13,0** |

En un segundo de puro silencio, antes eran unos 140 ms. Ahora cuesta lo mismo o menos que la música.

### Cuándo pasaba en el bar

Con «Saltar silencios largos» encendido (lo de fábrica), los silencios largos ya se saltaban. Pero siguen sonando:

- el margen mudo del final de cada canción (medio segundo o lo que dure el fundido) y la cola que sueltan los
  retardos de la cadena al acabar;
- los silencios de menos de 2 s al principio o al final, y las pausas de menos de 5 s en medio;
- las canciones que el preanálisis aún no ha medido, que suenan enteras;
- todo, si el encargado apaga ese interruptor.

Justo al final de cada canción es cuando la pantalla prepara la siguiente. En el equipo de dos núcleos, la música
corre en su propio hilo, que se reparte el procesador con la pantalla y con el preanálisis.

## Lo que se cambió

Una pieza pequeña y común, `Desnormales.cs`. Cada 64 cuadros, lo que queda por debajo de un tope se pone a cero:

- 1e-15 en los filtros de precisión simple;
- 1e-30 en los de doble precisión.

Desde ahí, con ceros a la entrada, el filtro se queda en cero exacto y no cuesta nada. Se aplica en:

- el grave en mono (`GraveMono.cs`);
- el bajo psicoacústico y el modo noche (`Form1.Sonido.cs`, `ModoNoche.cs`);
- el EQ y el realce del nivelado (`AudioControles.cs`, `Realce.cs`). El paso-bajo del realce se apaga en pocas
  muestras, así que ese se limpia en cada una;
- la medida del original: sonoridad y huella del EQ (`AudioControles.cs`);
- el pulso que mueve la aurora y el brillo (`Form1.Video.cs`).

El subgrave, «El local» y la medida de sonoridad del preanálisis ya lo hacían.

Mientras suena música, se añade un contador por cuadro y, cada 64, unas pocas comparaciones.

## Cómo se comprobó

- **Lo que suena es lo mismo.** Comparé el ejecutable de antes y el de ahora sobre la salida entera de la cadena,
  en tres modos:
  - de fábrica;
  - todo apagado;
  - de fábrica sin saltar silencios.

  Con tres canciones de prueba de 3 a 4 minutos, otra con un hueco mudo de 4 s en medio y la sintética con
  silencio:
  - **con música, las mismas muestras**, bit a bit, también después del hueco mudo;
  - **en el silencio**, donde antes salían restos diminutos ahora sale 0, con diferencias de 2e-16 como mucho;
  - **tras el paso a 16 bits** (con el mismo ruido de redondeo), **los mismos bytes** en todos los casos.
- **Con música no gasta más:** tres medidas alternadas, de 16 a 19 ms por segundo antes y de 16 a 17 ahora. La
  diferencia está dentro del ruido de la medida. Al pulso visual le añade unos 0,03 ms por segundo.
- **Batería nueva `desnormales`** (CPU y GPU). Arma la cadena de verdad con lo de fábrica, el modo noche y la medida
  aprendiendo, sobre 3 s de música y 6 s de ceros. Mira que:
  - tras 2 s de silencio la salida sea cero exacto;
  - ningún estado quede en un resto diminuto;
  - el silencio no cueste más que la música (con margen de 2,5 veces).

  Con el ejecutable de antes falla las tres: 351.450 muestras con restos, 82 estados en restos, y 188 ms por segundo
  en silencio frente a 21 con música. Con el de ahora pasa (19,6 frente a 23,5).
- **Tanda completa (`pruebas/todo.sh`):** 26 problemas, los mismos 26 de siempre (baterías que ya fallaban en este
  Linux); ninguno nuevo. `desnormales` pasa en CPU y GPU.
- **Dibujo por CPU:** los 14 métodos congelados, idénticos.

## Segunda pasada (13 de octubre): el convertidor de frecuencia, un tercio más ligero

Petición del usuario: «busca más mejoras».

Medí también las opciones de sonido que no son de fábrica. **El convertidor de frecuencia** era, con diferencia, lo
que más gastaba: unos 40 ms por segundo, el triple que todo el resto de la cadena. Lo usan:

- «Tarjeta a: 44,1 / 48 kHz» (Ctrl+F3 → Sonido → El local y sus bocinas), cuando la canción viene a la otra
  frecuencia;
- el relevo sin pausa entre dos canciones de frecuencias distintas.

**Por qué gastaba tanto.** Cada muestra de salida se calcula con 192 coeficientes, y cada uno se interpolaba en el
momento entre dos fases de una tabla. Pero de 44,1 a 48 kHz solo hay 160 posiciones distintas, que se repiten sin
parar.

**Qué se cambió** (`SinPausa.cs`, `RemuestreoContinuo`):

- esas posiciones se interpolan una sola vez, con la misma cuenta y en el mismo orden, y cada muestra solo las lee.
  Si en alguna pareja de frecuencias rara hubiera demasiadas (más de 2 MB), se sigue como siempre;
- el anillo de entrada guarda cada muestra dos veces, ya en doble precisión: la ventana nunca da la vuelta y no hay
  que comprobarlo en cada coeficiente, ni convertir cada muestra 192 veces.

**Resultado (aquí, mejor de cinco medidas):** de 44,1 a 48 kHz en estéreo, de 43 a 29 ms por segundo; de 48 a
44,1, de 42 a 29. Con 6 canales casi no cambia (es raro en una rocola).

**Los mismos bits.** El ejecutable de antes y el de ahora dan exactamente la misma salida, en 10 parejas de frecuencias
(de 11 a 96 kHz), en mono, estéreo y 5.1. La batería nueva **`remuestreoligero`** (CPU y GPU) lo vigila: compara en
la misma rocola el camino nuevo con el de siempre, en 48 conversiones, y mira que 44,1 ↔ 48 usen el nuevo. Con el
ejecutable de antes falla 13 de sus 62 comprobaciones.

También pasan `sinpausa`, `octavatanda`, `eficiencia` y `desnormales`. `salidacontinua` falla igual que antes: en este
Linux no hay `Msacm32.dll` para el MP3. Tanda completa: los mismos 26 problemas de siempre, ninguno nuevo; el dibujo
por CPU, idéntico.

## Tercera pasada (13 de octubre): el repaso a fondo de la biblioteca, sin atascarse en los silencios

Petición del usuario: «busca más mejoras».

En la primera pasada medí el preanálisis en su modo rápido, que solo recorre trozos de cada canción, y no se
encarecía con silencios. Pero **el repaso a fondo** (el que va midiendo la biblioteca entera en segundo plano, durante
horas, en el equipo de dos núcleos) recorre cada canción de principio a fin. Ahí sí pasaba lo mismo que en la cadena:
la huella del EQ y la sonoridad que mide se apagaban en restos diminutos.

Medido aquí (repaso a fondo, mejor de varias medidas):

| Canción | Antes | Ahora |
| --- | --- | --- |
| 60 s de música | 0,65 s | 0,64 s |
| 60 s de música y 60 s de silencio | 6,43 s | 1,24 s |
| 4 minutos de música | 2,37 s | 2,35 s |
| Esa misma con 7 s de silencio | 3,10 s | 2,55 s |

Cada segundo de silencio costaba unas nueve veces lo que uno de música; ahora, lo mismo. Pesa en las canciones con
finales mudos largos, pistas ocultas o pausas, que son justo las que el repaso lee enteras.

**Qué se cambió:** en `MeasureFile` (`Form1.Sonido.cs`), cada 64 cuadros los estados de la huella del EQ y de la
sonoridad que bajan de 1e-15 se ponen a cero, con la misma pieza `Desnormales.cs`.

**Los mismos valores, bit a bit.** Con el ejecutable de antes y el de ahora, en seis archivos (con y sin silencios) y
en los dos modos del preanálisis, sale exactamente lo mismo:

- el volumen;
- la duración;
- la huella del EQ (con su corte de agudos y su recorte);
- la sonoridad integrada, con su mapa de silencios.

**Batería `desnormales`, ampliada.** Mide en el repaso a fondo una canción de 3 s de música y 20 de silencio frente a
otra de 23 s de música. Mira que ningún estado quede en un resto diminuto y que el silencio no cueste más del doble.
Con el ejecutable de antes falla las dos (1,9 s frente a 0,2 s); con el de ahora pasa (0,23 frente a 0,23).

Tanda completa: los mismos 26 problemas de siempre, ninguno nuevo; el dibujo por CPU, idéntico.

## Lo que no se pudo medir

- **Los tiempos son de esta máquina**, no del equipo del bar. La proporción (unas siete veces) es la de aquí.
- **XP calcula con otra unidad del procesador (x87)**, que también va lenta con estos números, pero no lo pude medir.
  No prometo la misma proporción allí.

## Lo que se revisó y no tenía nada que ganar

- **El fondo no reserva memoria por cuadro:** 0 bytes en cada capa (nebulosas, grano, luna, cielo vivo, estrellas),
  medido con la tarjeta simulada de la ronda del fondo. Sin basura, no hay pausas del recolector por su culpa.
- **El preanálisis de la biblioteca** ya mide la sonoridad en la misma pasada y no se encarecía con silencios: una
  canción de 4 minutos con 7 s de silencio tarda lo mismo que sin ellos.
- **Con música**, la cadena no tiene una etapa desproporcionada. Lo que más gasta es el realce sobremuestreado
  (unos 5 ms por segundo aquí), que ya se optimizó en rondas anteriores.
