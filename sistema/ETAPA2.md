# Rocola OS, etapa 2: la rocola en Linux — el plan

Inventario hecho el 31-10-2026 sobre `Rocola/` (la rocola de Windows XP, rama Modest), para saber qué se queda y qué se reescribe.
Nada de esto está empezado en código todavía.

## Lo que hay

- **109 archivos y unas 69.000 líneas de C#.** 76 de esos archivos son partes de la misma clase `Form1`: las etapas de la cadena
  de sonido (la presencia, el limitador, el grave en mono...) están *dentro* de la ventana.
- **Llamadas a Windows (`DllImport`):** user32 (15), kernel32 (14), winmm (11: la salida de sonido, el reloj y el mezclador),
  advapi32 (4), gdi32, dwmapi y d3d9 (1 cada una).
- **Direct3D 9:** 30 archivos (todo el dibujo por tarjeta gráfica).
- **WinForms:** 40 archivos (la ventana, Ctrl+F3, los botones).
- **System.Drawing:** 51 archivos (textos, carátulas, el dibujo por CPU, que está congelado).
- **NAudio:** 44 archivos, casi todo `ISampleProvider` y `WaveFormat`. Esa parte de NAudio es C# puro y funciona en Linux. Lo de
  Windows es solo la salida (`WaveOut`, ya cambiada por `WaveOutPropio` sobre winmm), el mezclador de XP y el descompresor ACM.
- **Los vídeos:** el reproductor de Windows Media como ActiveX (`VideoHost : AxHost`, `Form1.Video.cs`).

## Qué se queda y qué se reescribe

| parte | en Linux |
|---|---|
| La cadena de sonido entera (etapas, nivelado, sonoridad, pre-eco, limitador, remuestreo) | **se queda**: C# puro. Hay que sacarla de `Form1` a su propio proyecto («el motor»), sin cambiar un bit. Se comprueba con `compara-cadena` contra la de XP |
| Los MP3 | **NLayer para todos** (ya va dentro). Sin el ACM de Windows. Además, abre la puerta a rellenar los huecos (R1) con el dato del descompresor |
| La salida de sonido | **se reescribe**: ALSA (`libasound`, unas pocas llamadas: `snd_pcm_open`, `hw_params`, `writei`) en un hilo propio, como `WaveOutPropio` |
| El volumen general y el mezclador | ALSA (`amixer` por dentro: `snd_mixer_*`) |
| La biblioteca, las tablas, la configuración, las IA | **se quedan**; solo cambian las rutas (`/rocola/datos`) y los finales de línea |
| El dibujo | **se reescribe**: Direct3D 9 → OpenGL ES 2 por EGL/KMS (Mesa r600 en el A4). Es lo más grande: 30 archivos |
| Textos y carátulas | System.Drawing no existe en Linux con .NET moderno → FreeType para las letras y un decodificador de JPEG/PNG, o SkiaSharp |
| La ventana y los botones | sin ventanas: pantalla completa por KMS; las teclas y botones por `evdev` (`/dev/input`) |
| Ctrl+F3 (los ajustes) | se redibuja con lo mismo que la pantalla (ya no hay WinForms) |
| Los vídeos | `mpv` (libmpv) o GStreamer, los dos están en Buildroot; con libmpv la salida también va por KMS |
| Compilar | .NET 8 con NativeAOT: un solo programa nativo para x86-64 (`-march` no aplica: el JIT/AOT de .NET elige SSE/AVX al vuelo) |

## El orden

1. **El motor de sonido** como proyecto aparte, compartido por XP y Linux. Primero en XP, sin cambiar un bit (`compara-cadena`),
   para no tener dos cadenas distintas.
2. **Una rocola sin pantalla:** el motor, la biblioteca y la salida ALSA. Toca la cola desde la consola. Se prueba en QEMU y, con
   la memoria USB, en el A4: ahí se ve si el sonido de verdad funciona.
3. **La pantalla:** OpenGL ES por KMS, las letras y las carátulas.
4. **Los botones y la moneda** (`evdev`), Ctrl+F3 y los vídeos.
5. **Rocola OS completo** como paquete de Buildroot (`sistema/package/rocola`), con la música y los datos en su partición.

## Lo que hace falta para empezar

- **El SDK de .NET 8:** su sitio de descargas está bloqueado aquí, pero los repositorios de Ubuntu (que sí se alcanzan) lo traen
  (`dotnet-sdk-8.0`), igual que NuGet.
- **Para el sistema:** compilar la etapa 1, lo que necesita `sources.buildroot.net` (ver LEEME.md), y probarla en el A4.
