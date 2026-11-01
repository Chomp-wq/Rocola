# La rocola de Rocola OS (etapa 2)

Código en .NET 10 de la rocola para Linux. Plan completo en [../ETAPA2.md](../ETAPA2.md).

- **`Motor/`** — la cadena de sonido, copiada de `Rocola/` (la rocola de XP) y llevada a .NET 10 y NAudio.Core 3. Conserva el código de
  XP tal cual: cada etapa gana una lectura con `Span` que llama a la de siempre (`Puente.cs`). Desde el hito 5 (31-10-2026) lleva la
  **cadena entera** de XP (`Cadena.cs`: `ArmaCadena` y `CadenaCancion`, sin cambiar una línea) con todo lo que necesita: el DSEE
  (`Dsee.cs`), el des-recorte, el bajo psicoacústico y el fundido (`Etapas2.cs`), el siseo, el modo billar (`CadenaBillar.cs`,
  `LocalAuto.cs`, `VolumenParejo.cs`, `VozGolpeYRetumbe.cs`), el relevo sin pausa (`SinPausa.cs`), los MP3 sin huecos
  (`RellenoMp3.cs`), los silencios largos, las tablas y medidas (`DeXP.cs`: lo que en XP vive en la ventana, sacado tal cual, cada
  trozo dice de qué archivo). Lo que en XP hace la ventana o Windows va en `Ventana.cs` en su forma de Linux (la carpeta de los datos,
  `fsync`, la pantalla que aún no hay); la apertura de canciones, en `Apertura.cs` (el lector de Rocola OS en lugar del de Windows).
  Lo público, en `Reproductor.cs`: `new Form1()` es un motor con los ajustes de fábrica de XP; `Arranca` (la configuración y las
  tablas: `Arranque.cs`, `Configuracion.cs`), `MideSiFalta`, `Arma` (y `Dispose` de la canción: guarda lo aprendido), `Cierra`.
- **`Consola/`** — `rocola-consola --rocola`: **la rocola en la pantalla** (KMS y OpenGL ES 2, `Pantalla/` y `VistaGrafica.cs`; letras y
  carátulas con StbTrueTypeSharp y StbImageSharp) o, con `--texto` o sin pantalla gráfica, **en modo texto** (`VistaTexto.cs`), con sus botones (evdev, `rocola_teclas.txt` de XP; en una
  terminal, también el teclado; Esc sale): discos por letra, pedir con créditos, monedas, saltar (`RocolaTexto.cs`). Y la rocola sin
  pantalla (`rocola-consola [--wav x.wav] [--biblioteca] canciones...`): lee `rocola_config.txt` y lo
  aprendido, toca la cola (o toda la biblioteca) con el relevo sin pausa de XP (`Cola.cs`, `Biblioteca.cs`), mide cada canción nueva (como
  el repaso de XP), le arma la **cadena entera** de XP con esos ajustes, guarda lo que aprende al oírla y la toca por ALSA (`SalidaAlsa.cs`, hilo propio, 16 bits con dither) o la escribe en un WAV
  (`--wav`). Los MP3 van todos por NLayer 3 (`Lector.cs`), sin el hueco del codificador; los mono salen como estéreo. Los datos
  (`rocola_error.txt`...) van a `ROCOLA_DATOS`, si no a `/rocola/datos`. Programa nativo (NativeAOT, 2,6 MB, solo necesita libc y
  libasound). Va en Rocola OS como paquete de Buildroot (`sistema/package/rocola-consola`).
- **`Pruebas/`** — `CompararConXP.cs`: el mismo programa compilado con Mono contra la rocola de XP y con .NET 10 contra el motor;
  pasa la misma música por cada etapa. `compara.py` compara las salidas muestra a muestra.

## Comprobar el motor contra la rocola de XP

```sh
sh pruebas/todo.sh --lista >/dev/null            # (o cualquier corrida: deja pruebas/rocola.exe compilado)
cd sistema/rocola/Pruebas && dotnet build
mcs -r:../../../pruebas/NAudio.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll -out:/tmp/xp/CompararConXP.exe CompararConXP.cs ../../../pruebas/leercodigo.cs   # y copiar rocola.exe, NAudio.dll y NLayer.dll a /tmp/xp
(cd /tmp/xp && mono CompararConXP.exe rocola.exe /tmp/xp/salida)
dotnet bin/Debug/net10.0/CompararConXP.dll ../Motor/bin/Debug/net10.0/Motor.dll /tmp/linux
python3 compara.py /tmp/xp/salida /tmp/linux
```

(Con `--solo-cadena` al final de las dos corridas, solo la cadena entera.)

Resultados:
- 31-10-2026, hito 5: **la cadena entera** (`ArmaCadena`) frente a la de XP, con una canción medida y otra sin medir, en seis
  combinaciones (de fábrica, modo billar, el local a mano, estéreo S3 con subgrave, DSEE con I1 y siseo, EQ y graves a mano): **las 12
  casi iguales**, con la mayor diferencia entre −135 y −150 dB (del 93 al 98 % de las muestras idénticas bit a bit). Lo que cambia es
  la cuenta del bajo psicoacústico (funciones matemáticas de Mono frente a las de .NET), muy por debajo de lo que se oye.
  - Con esta prueba salió un **fallo del puente** (`Puente.cs`): cuando una etapa leía de otra, las dos usaban el mismo arreglo de
    apoyo y se pisaban. Cada etapa sola salía bien (por eso no lo vieron los hitos 1 y 2), pero **la cadena del hito 4 sonaba mal en
    trozos**. Arreglado: un apoyo por nivel.
  - En QEMU, grabando lo que sale por la tarjeta emulada: la canción procesada suena tras el tono de prueba, con el mismo dibujo de
    volumen que el WAV (4,5 dB más baja por el mezclador al 80 %). QEMU no la entrega muestra a muestra (deriva y remuestrea), así que
    la comprobación exacta es la de `CompararConXP`.
- 31-10-2026, hito 4: en QEMU, **Rocola OS arranca y `rocola-consola` toca la canción de prueba por ALSA** (la HDA emulada). Aquí, el
  WAV de NativeAOT y el de .NET normal difieren solo en el dither (2 LSB como mucho).
- 31-10-2026, hito 1: **las 15 variantes de las etapas, idénticas bit a bit** a las de XP.
- 31-10-2026, hito 2: también **el nivelado** (ganancia, EQ y realce: `Nivelado.cs`, `Realce.cs`) **y la medida de la sonoridad**
  (LKFS, LRA, picos, inicio y fin, zumbido, pre-eco: `Sonoridad.cs`), idénticos. El nivelado avisa a la pantalla de cada bloque
  (`Ganchos.cs`, vacío hasta que haya pantalla).
