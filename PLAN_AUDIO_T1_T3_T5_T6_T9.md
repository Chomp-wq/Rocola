# Plan de trabajo: T1, T3, T5, T6, T9 y el ruido del amplificador — 25 de octubre de 2026

Rama Modest (`claude/modest-carson-fa2v2n`). Decisión del dueño sobre [AUDIO_PROPUESTAS_MODEST_2026-10-25.md](AUDIO_PROPUESTAS_MODEST_2026-10-25.md):
«T1, T3, T5, T6, T9 y revisa si hay forma de que se escuche menos el ruido eléctrico del amplificador por medio de software, o si es
algo solamente de hardware… deja los datos de cómo vas a hacerlo por si se acaba mi límite pueda seguir con otra IA».

**Este archivo es el traspaso.** Quien siga (otra IA o una persona) debe leer primero `AGENTS.md` (las reglas permanentes) y luego
esto. Cada tarea dice qué hay que hacer, dónde, cómo se prueba y en qué estado quedó. Al terminar cada una se actualiza la tabla de
estado y se publica (commit + push a la rama Modest, comprobando el push).

## Estado

| Tarea | Estado | Commit |
|---|---|---|
| Plan (este archivo) | hecho | (este commit) |
| T1 nivelado con el tono del local | pendiente | |
| T3 inicio y fin útiles (algoritmo 6) | pendiente | |
| T6 quitar el zumbido | pendiente | |
| T9 suavizar las eses | pendiente | |
| T5 salida con hilo propio | pendiente | |
| Ruido del amplificador | pendiente | |

## Reglas que hay que respetar (de AGENTS.md y del proyecto)

- .NET 3.5 / C# 3 / Windows XP. **Nada de .NET 2.0 SP2**: el A4 tiene 2.0.50727.1433. Por ejemplo, `WaitHandle.WaitOne(int)` sin el
  `bool` no existe allí: usar `WaitOne(ms, false)`.
- El renderizado por CPU está congelado (no aplica aquí).
- Lo que cambia cómo suena lleva su interruptor persistente en Ctrl+F3, salvo lo que el dueño dijo que va sin él. Lo que no cambia el
  sonido es automático y sin interruptor.
- Con los mandos en su valor de fábrica, la cadena tiene que salir **bit a bit** como antes (`pruebas/compara-cadena.cs` contra el
  ejecutable anterior).
- Disco continuo: toda etapa con memoria sigue con la de la pista anterior (`Sigue`, desde `SigueA` en `Form1.SinPausa.cs`), sin
  escalón en el cambio (`pruebas/cadenaentera.cs`).
- «Escuchar sin mejoras» (S4, `AplicaSinMejoras` en `LoQueSuena.cs`) quita las mejoras al mismo volumen; «Qué le hace a la canción»
  (S5, `TextoLoQueSuena`) enseña lo que actúa.
- Archivos con finales de línea mezclados (CRLF y LF): editar sin cambiarlos. En este trabajo se usa un ayudante de Python que
  reemplaza texto exacto con `newline=''`.
- Pruebas:
  - `TMPDIR=/tmp sh pruebas/todo.sh <baterias>`, o la suite entera sin argumentos (necesita historial de git).
  - Fallan ya antes de este trabajo, por cosas de Linux y de la tarjeta imitada: `sala`, `discoacotado` y `caratulasadaptativas`.
  - Una batería nueva se añade a `pruebas/todo.sh` (en la lista CPU o GPU) y a `pruebas/LEEME.md`.
- Commits en español; terminan con las líneas `Co-Authored-By` y `Claude-Session` que pide el entorno. Sin nombres de modelo.
- No subir datos del dueño al repositorio.
- Documentar en `README.md` (entrada arriba), en `AGENTS.md` (la decisión) y en un documento de la tanda.

## T1. El nivelado deja margen al tono del local

**Por qué.** Medido el 25-10, en la cadena de verdad y con seis canciones sintéticas:

- con «Graves del local» a +3, el limitador quita más de 1 dB el 96 % del tiempo en una masterizada;
- con +6, quita más de 3 dB el 84 % del tiempo.

Para volver al criterio del nivelado al límite (más de 1 dB, como mucho el 10 % del tiempo; más de 3 dB, el 1 %) hay que bajar:

| Tono | Bajar |
|---|---|
| graves +3 | 2,0 dB |
| graves +6 | 4,5 dB |
| agudos +3 | 0,5 dB |
| agudos +6 | 1,5 dB |

**Qué se hace.**

- Una función `CompensaTonoDb(int graves, int agudos)` en `SalaLocal.cs`: 0,75 dB por dB de graves positivos y 0,25 dB por dB de
  agudos positivos. Los negativos no dan nada.
- Se aplica en `GananciaDe(Track t)` (`NivelDisco.cs`), que es por donde pasa la ganancia de cada canción al abrir (`ArmaCadena`), en
  el relevo (`Form1.SinPausa.cs`) y en vivo (`ApplyNormNow`): `g *= 10^(-CompensaTonoDb(lo.Graves, lo.Agudos)/20)`, con
  `lo = LocalDe(t)`, que da lo que de verdad suena (el modo automático del local incluido).
  - Con «sin mejoras» (S4) el tono no suena, así que no se compensa.
- Hay que confirmar que al mover graves y agudos en Ctrl+F3, y al encender o apagar S4, se llama a `ApplyNormNow` (o que se cambia
  `Tap.Gain`). La rampa de 20 ms de `LevelTap` evita el chasquido.
- Con el tono en 0, `CompensaTonoDb` da 0 y no se multiplica nada: bit a bit igual.

**Pruebas.**

- `pruebas/allimite.cs`, sección nueva: la misma medida de la cadena entera (`EnLaCadena`) con graves +3/+6, agudos +3/+6 y los dos
  a +6. Tiene que cumplir el criterio, y con el tono en 0 la ganancia tiene que ser idéntica.
- La herramienta de la medida original se armó copiando `allimite.cs` y añadiendo campos (`Extra`, `Recorte`). La sección nueva hace
  lo mismo dentro de la batería.

## T3. Dónde empieza y acaba de verdad cada canción (algoritmo 6)

**Qué se mide.** En `MedidorBs1770` (`Sonoridad.cs`), el corto plazo de 3 s del LRA (una cada 100 ms) ya se calcula. Para cada
casilla del histograma del LRA (0,1 LU, de −70 a +10) se guardan, con memoria fija:

- el primer subbloque en que el corto plazo llegó a esa casilla o más;
- el último.

Al cerrar, con la sonoridad integrada `I`, el umbral es `I − 20 LU`:

- **fin útil** = el último subbloque con corto plazo ≥ umbral, más 1, por 0,1 s. Es el final de esa ventana de 3 s;
- **inicio útil** = el primero ≥ umbral, por 0,1 s, menos 3 s (el principio de esa ventana), y nunca menos de 0.

Sin datos (canción de menos de 3 s, o muda): `-`.

**Formato** (`rocola_sonoridad.txt`). Algoritmo 6 = el 5 más cuatro campos antes de la ruta:

`inicio útil | fin útil | zumbido Hz (50, 60 o «-») | cuánto sobresale el zumbido, en dB («-»)`.

Los dos de zumbido son de T6; van en la misma vuelta del repaso para no medir dos veces. Hay que tocar:

- `LeeIntegRegistros` (`campos = 18` para 6);
- `EscribeIntegRegistro`;
- el struct `SonoridadIntegrada` (campos `InicioUtil`, `FinUtil`, `ZumbidoHz`, `ZumbidoDb`, con un constructor nuevo);
- `CierraIntegrada`.

`MedidorBs1770.Algoritmo` pasa a 6. `ConP90` sigue valiendo para 5 y 6, porque es `>= 5`.

**La tanda larga** pide todo lo que no sea del algoritmo actual. Hay que mirar `allimite` («la tanda larga pide todo lo que no es del
algoritmo 5») y cambiar la prueba al 6.

**Compatibilidad** (como en el 4 y el 5):

- las medidas de 1 a 5 se siguen leyendo y nivelan igual;
- un ejecutable anterior guarda las de 6 como ajenas, intactas: probar con `ROCOLA_EXE_ANTERIOR` (ver `pruebas/lra.cs`);
- `pruebas/compatvieja.cs` y `pruebas/sonoridad.cs` siguen verdes.

**Uso.** Solo se mide y se guarda; T7 y T8 (que no se pidieron todavía) lo usarían. `rocola_calidad.txt` puede decir cuántas canciones
tienen un final largo (más de 8 s después del fin útil): es un dato útil para decidir T7.

## T6. Quitar el zumbido de la corriente

**La medida, en el repaso (algoritmo 6, junto con T3).**

- Con la suma de los canales y ventanas de 2 s con ventana de Hann (un paso de 0,5 Hz), Goertzel en f0 = 50 y 60 Hz, sus armónicos 2 y 3,
  y dos vecinos a ±4 Hz de cada uno.
- Por cada ventana se calcula cuánto sobresale cada línea sobre la media de sus vecinos (dB) y su nivel (dBFS). Se acumula por
  casillas de la sonoridad de esa ventana (memoria fija).
- Al cerrar, se mira solo el 20 % más callado de las ventanas con sonido (por encima de −70 LUFS). Hay zumbido de f0 si en ellas:
  - la fundamental o el 2.º armónico sobresalen de media 10 dB o más;
  - lo hacen al menos en el 70 % de esas ventanas;
  - el nivel es mayor que −80 dBFS.
- Se guarda f0 y cuánto sobresale.
- Coste: unas 18 cuentas de Goertzel por cuadro de la suma. Medir los ns por cuadro y compararlo con la decodificación.

**La etapa** (`QuitaZumbido`, archivo nuevo `Zumbido.cs`).

- Muescas bicuadradas (RBJ «notch») en f0, 2·f0, 3·f0 y 4·f0, con ancho de 2 Hz cada una (Q = f/2), en serie y por canal.
- Va antes del nivelado: después de los arreglos S1/S2 y del des-recorte, junto a `ArreglaGrabacion`.
- Solo se pone si la canción tiene zumbido medido **y** el interruptor está encendido. Si no, no se inserta: bit a bit igual.
- `Pasa` para S4 (sin mejoras).
- `Sigue` para el disco continuo: si las dos pistas lo tienen, se copia el estado de los filtros.
- Al cambiar el interruptor en vivo, mezcla de 25 ms entre lo filtrado y lo que entra, como el subgrave (P6).

**El interruptor.**

- `quita_zumbido=`, **encendido de fábrica**: como S1 y S2, solo actúa con el defecto. Lo propuse así y el dueño eligió T6 sin
  objetar.
- En Ctrl+F3 → Sonido → Lo que suena ahora → tarjeta «ARREGLOS DE LA GRABACION», entre S2 y S3.
- La nota general de abajo («Los dos primeros, encendidos de fábrica…») se quita y cada nota lo dice. Así cabe sin moverse del
  botón «Volver».
- Hay que añadirlo también en:
  - `Form1.Datos.cs` (leer y guardar);
  - `Form1.Ajustes.cs` (el `bk…` para Cancelar y lo de Aplicar);
  - «Dejar como de fábrica»;
  - `GraveMono.cs:121` (donde S1 y S2 se aplican en vivo a la que suena y a la siguiente);
  - S5 («zumbido de 60 Hz quitado»).

**Pruebas** (batería nueva `zumbido`):

- tono de 60 Hz a −50 dBFS más armónicos, sobre música sintética con partes calladas → se detecta 60 y se quita (−30 dB o más en la
  línea);
- lo mismo con 50 Hz;
- música con un bajo largo en 61,7 Hz (Si) → no se detecta;
- música sin zumbido → no se detecta y la cadena sale bit a bit igual;
- lecturas partidas;
- disco continuo sin escalón;
- el interruptor en vivo sin chasquido;
- el coste.

## T9. Suavizar las «eses»

**La etapa** (`SuavizaEses`, archivo nuevo `Eses.cs`). Va después del local y la presencia y antes del bajo psicoacústico y del
limitador.

- Un paso-banda bicuadrado centrado en 6,5 kHz (Q 0,9) da la banda de las eses.
- Dos envolventes RMS (5 ms): la de la banda y la de todo.
- Si la banda pasa del 45 % de la energía de todo, y la banda está por encima de −40 dBFS, se calcula la ganancia de la banda
  `g = max(−6 dB, (0,45/r)^1)` en 1 ms de ataque y 60 ms de suelta.
- La salida es `y = x − (1 − g)·banda`. Cuando `g` es exactamente 1, sale `y = x`: **bit a bit lo que entra**.
- Lecturas partidas (cuenta de canal), `Pasa` (S4) y `Sigue` (disco continuo).

**El interruptor.**

- `suaviza_eses=`, **apagado de fábrica**: así lo propuse.
- En Ctrl+F3 → Sonido → El local y sus bocinas → «Tono y presencia del local», si cabe. Si no, en Lo que suena ahora.
- Se aplica al instante: su propia ganancia hace de rampa.

**Pruebas** (batería nueva `eses`):

- ráfagas de ruido de 4 a 9 kHz sobre un tono de voz → bajan de 3 a 6 dB;
- música pareja (ruido rosa, mezcla) → baja menos de 0,5 dB y casi nunca actúa;
- apagado = bit a bit igual;
- lecturas partidas;
- disco continuo;
- el coste.

## T5. La salida de sonido con su propio hilo

**Por qué.** Hoy NAudio `WaveOut` avisa por ventana. Cada bloque lo rellena **la pantalla** (`SalidaSonido.cs`, `CreaWaveOut`), copiando
del colchón del hilo de la música (`BombaSonido`, `ConBomba`). Si la pantalla se para más que la latencia (750 a 1000 ms), la música se
corta. En el A4 se paró 2015 ms (23-10, Present) y 3172 ms (24-10, al arrancar).

`WaveOutEvent` de NAudio no sirve: usa `WaitOne(int)`, que el A4 no tiene.

**Qué se hace** (archivo nuevo `SalidaPropia.cs`): una clase `WaveOutPropio : IWavePlayer, IWavePosition`.

- **El trabajo:** `waveOutOpen` con `CALLBACK_EVENT` (0x50000) y un `AutoResetEvent`. Los `WAVEHDR` y los datos van en memoria no
  administrada (`Marshal.AllocHGlobal`), sin pines.
- **El hilo:** propio, en `ThreadPriority.Highest`, con nombre «audio (salida)» (`HilosRocola.Nombra`). Su bucle es
  `WaitOne(espera, false)`; luego, por cada cabecera con `WHDR_DONE`, la rellena desde el proveedor y la vuelve a escribir.
- **Windows, detrás de una interfaz:** `ISistemaWaveOut`, con la real (P/Invoke a `winmm.dll`) y otra imitada para las pruebas en
  Linux, que «consume» las cabeceras con un reloj.
- **Lo que tiene que hacer como el `WaveOut` de ahora:**
  - `Init`, `Play`, `Pause`, `Stop` y `Dispose`;
  - `PlaybackState`;
  - `PlaybackStopped`, que se pasa al hilo de la pantalla con `BeginInvoke`: el manejador de ahora (`Output_PlaybackStopped`) toca la
    interfaz;
  - `GetPosition()`, con `waveOutGetPosition` en bytes, por el reloj de la canción;
  - `OutputWaveFormat`;
  - `DesiredLatency`, `NumberOfBuffers`: los mismos números de ahora.
- **Respaldo automático:**
  - si `waveOutOpen` falla o algo truena al armarla, se usa el `WaveOut` de siempre, en esa sesión;
  - si truena a media canción, `PlaybackStopped` con la excepción, como ahora (el código de ahora ya reabre la salida).
- **Sin interruptor:** no cambia el sonido (regla del 15-09).
- **Revisar antes de empezar:**
  - todo lo que hoy corre en el hilo de la pantalla porque lo llama la salida al pedir datos (`MideLatenciaSalida`,
    `PerfilLatenciaAudio.Observa`, la cuenta de cuadros entregados). Que pueda correr en otro hilo, o pasarlo a la pantalla;
  - el colchón de `BombaSonido` admite un lector que no es la pantalla (usa su `cerrojo`).
- **La latencia** se deja como está (`PerfilLatenciaAudio`, 750 o 1000 ms en XP) en esta primera versión. Bajarla (T10) es otro paso,
  después de probar en el A4.

**Pruebas** (batería nueva `salidapropia`, con el Windows imitado):

- abre, suena, pausa, sigue y para;
- la posición avanza con lo consumido;
- con la pantalla parada 3 s (un `Thread.Sleep` en el hilo de prueba que hace de pantalla), la salida imitada no se queda sin datos;
- si `waveOutOpen` falla, se usa la de siempre;
- `PlaybackStopped` llega al hilo de la pantalla;
- `Dispose` libera las cabeceras;
- una lectura del IL: ningún `WaitOne(int)` sin el `bool` (como en la revisión para XP).
- Las de la salida (`salida`, `latenciaadaptable`, `sinpausa`, `arranquevivo`, `videoaudio`) siguen verdes.

**Hay que probarla en el A4.** Es la parte que habla con el controlador de sonido de XP.

## El ruido eléctrico del amplificador

**Por investigar y documentar.** Lo que se sabe de antemano:

- El zumbido o soplido que mete el propio amplificador, o un lazo de tierra entre la PC y el amplificador, llega **después** de la
  tarjeta de sonido. El software no lo puede quitar; se quita con hardware:
  - un aislador de tierra (transformador) en el cable de audio;
  - la PC y el amplificador en el mismo enchufe;
  - cables blindados y cortos;
  - una caja DI o una tarjeta USB externa.
- **Lo que sí puede hacer el software:**
  1. **Dar la señal fuerte**, para que el amplificador pueda ir más bajo. Su ruido queda igual y la música más arriba. Ya se hace:
     volumen de Windows al máximo y el nivelado al límite. T1 evita que eso aplaste.
  2. **Silenciar las entradas que se oyen por la salida** en el mezclador de XP: micrófono, línea de entrada, CD, auxiliar. En los
     Realtek de XP vienen abiertas, y el micrófono con su refuerzo mete ruido aunque no haya nada conectado. La rocola puede mirarlas al
     arrancar (`VolumenWindows.cs` ya usa el mezclador de XP con `NAudio.Mixer`) y apuntar en `rocola_error.txt` cuáles están abiertas.
     Silenciarlas sería tocar el mezclador de Windows, que AGENTS.md solo permite para el volumen general. Así que:
     - lo de apuntar es automático;
     - silenciarlas, solo si el dueño lo pide (o lo hace a mano en el control de volumen).
  3. El ruido que la propia PC mete por la salida analógica (zumbido que cambia con el disco, la tarjeta gráfica o el ratón) es de la
     placa. Solo lo arregla una salida externa (USB o óptica).
- **Entregable:** una sección en el documento de la tanda y, si da tiempo, el apunte del mezclador al arrancar.

## Orden y forma de trabajo

1. T1, que es pequeña y está medida. Commit y push.
2. T3 junto con la medida de T6 (los dos tocan el formato). Commit y push.
3. La etapa de T6 y su interruptor. Commit y push.
4. T9. Commit y push.
5. T5. Commit y push.
6. El ruido del amplificador, en el documento final.
7. Documento final de la tanda (`AUDIO_T1_T3_T5_T6_T9_MODEST_2026-10-25.md`), README y AGENTS.md. Suite entera. Commit y push.

Después de cada paso se pone su estado en la tabla de arriba, con el commit.
