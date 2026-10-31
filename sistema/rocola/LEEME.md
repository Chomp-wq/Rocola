# La rocola de Rocola OS (etapa 2)

Código en .NET 10 de la rocola para Linux. Plan completo en [../ETAPA2.md](../ETAPA2.md).

- **`Motor/`** — la cadena de sonido, copiada de `Rocola/` (la rocola de XP) y llevada a .NET 10 y NAudio.Core 3. Por ahora
  conserva el código de cada etapa tal cual: cada una gana una lectura con `Span` que llama a la de siempre (`Puente.cs`). Hito 1
  (31-10-2026): las etapas de la cadena (limitador, presencia, el local, grave en mono, eses, retumbe, ataque, ásperos, bombo,
  zumbido, pre-eco, arreglos). Hito 2: el nivelado, el EQ, el realce y la medida de la sonoridad.
- **`pendiente/`** — lo que falta traer al motor (el DSEE y su red, el siseo, el lector de XP): copiado y aún sin compilar. Se mueve a `Motor/` pieza a pieza.
- **`Consola/`** — la rocola sin pantalla (`rocola-consola`): mide cada canción, la sube «al límite» como en XP, la pasa por la
  cadena (arreglos → nivelado → grave en mono → limitador) y la toca por ALSA (`SalidaAlsa.cs`, hilo propio, 16 bits con dither) o la
  escribe en un WAV (`--wav`). Los MP3 van todos por NLayer 3 (`Lector.cs`); los mono salen como estéreo. Programa nativo
  (NativeAOT, 2,4 MB, solo necesita libc y libasound). Va en Rocola OS como paquete de Buildroot (`sistema/package/rocola-consola`).
- **`Pruebas/`** — `CompararConXP.cs`: el mismo programa compilado con Mono contra la rocola de XP y con .NET 10 contra el motor;
  pasa la misma música por cada etapa. `compara.py` compara las salidas muestra a muestra.

## Comprobar el motor contra la rocola de XP

```sh
sh pruebas/todo.sh --lista >/dev/null            # (o cualquier corrida: deja pruebas/rocola.exe compilado)
cd sistema/rocola/Pruebas && dotnet build
mcs -r:../../../pruebas/NAudio.dll -out:/tmp/xp/CompararConXP.exe CompararConXP.cs   # y copiar rocola.exe, NAudio.dll y NLayer.dll a /tmp/xp
(cd /tmp/xp && mono CompararConXP.exe rocola.exe /tmp/xp/salida)
dotnet bin/Debug/net10.0/CompararConXP.dll ../Motor/bin/Debug/net10.0/Motor.dll /tmp/linux
python3 compara.py /tmp/xp/salida /tmp/linux
```

Resultados:
- 31-10-2026, hito 4: en QEMU, **Rocola OS arranca y `rocola-consola` toca la canción de prueba por ALSA** (la HDA emulada). Aquí, el
  WAV de NativeAOT y el de .NET normal difieren solo en el dither (2 LSB como mucho).
- 31-10-2026, hito 1: **las 15 variantes de las etapas, idénticas bit a bit** a las de XP.
- 31-10-2026, hito 2: también **el nivelado** (ganancia, EQ y realce: `Nivelado.cs`, `Realce.cs`) **y la medida de la sonoridad**
  (LKFS, LRA, picos, inicio y fin, zumbido, pre-eco: `Sonoridad.cs`), idénticos. El nivelado avisa a la pantalla de cada bloque
  (`Ganchos.cs`, vacío hasta que haya pantalla).
