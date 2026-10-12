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
