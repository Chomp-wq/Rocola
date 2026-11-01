# Rocola OS, etapa 2: la rocola en Linux — el plan

Inventario hecho el 31-10-2026 sobre `Rocola/` (la rocola de Windows XP, rama Modest), para saber qué se queda y qué se reescribe.
El código empieza en `sistema/rocola/` (ver «El orden»).

## Lo decidido (31-10-2026)

El dueño: «en Rocola OS vamos a usar lo más nuevo, nos centraremos en Rocola OS ahora».

- **.NET 10** (el de soporte largo más nuevo), compilado con **NativeAOT** a un solo programa nativo. Probado aquí: un programa de
  prueba sale de 1,3 MB y corre sin instalar .NET.
- **Las instrucciones del A4:** `<IlcInstructionSet>x86-64-v2,avx</IlcInstructionSet>`. En .NET 10 FMA solo viene en el grupo de
  AVX2, que el A4 (Piledriver) no tiene, así que la rocola usa SSE4.2 y AVX.
- **NAudio.Core 3.3.0** (el más nuevo en NuGet), solo su parte sin Windows. **Cambio importante:** en NAudio 3 `ISampleProvider`
  lee con `Span<float>` en vez de `(float[], offset, count)`. Hay que adaptar cada etapa; es más moderno y evita copias.
- **La rocola de XP no se toca** mientras tanto. La de Linux lleva **su propia copia** del código de la cadena, modernizada; se
  comprueba contra la de XP (la misma canción por las dos cadenas, muestra a muestra).

## Cuánto cuesta separar la cadena (medido)

Compilando con .NET 10 los 17 archivos de las etapas de sonido tal como están (sin la ventana):

- **54 errores en total.** Casi todos son el `Read(Span<float>)` de NAudio 3 (2 por etapa).
- Solo faltan 4 tipos que viven en otros archivos: `MapaSilencios`, `AudioFftPlan`, `PreparedEq` y `GuardadoReciente`.
- Una referencia a WinForms en dos archivos.

La cadena está bastante limpia: el trabajo grande de la etapa 2 sigue siendo la pantalla, no el sonido.

## Hito 1 (31-10-2026): las etapas de la cadena en .NET 10

`sistema/rocola/Motor` compila con .NET 10 y NAudio 3 las etapas de la cadena, y `sistema/rocola/Pruebas` las compara con la rocola
de XP: **las 15 variantes probadas salen idénticas bit a bit** (limitador con y sin respirar, presencia con y sin billar, el local,
grave en mono con subgrave, eses con y sin billar, retumbe, ataque, ásperos, bombo, zumbido, pre-eco y arreglos). Siguiente: el
nivelado y el EQ (`LevelTap`), la tabla de la sonoridad, el lector de MP3 con NLayer y la salida ALSA.

## Hitos 2 a 4 (31-10-2026)

- **Hito 2:** el nivelado, el EQ, el realce y la medida de la sonoridad, idénticos bit a bit a XP.
- **Hito 3:** el lector (todos los MP3 por NLayer 3, WAV por NAudio) y la salida ALSA con hilo propio.
- **Hito 4:** `rocola-consola`, la rocola sin pantalla, como paquete de Rocola OS: en QEMU arranca y toca la canción de prueba por ALSA.

## Hito 5 (31-10-2026): la cadena entera

- El motor lleva la **cadena entera de XP** (`ArmaCadena`, sin cambiar una línea) y todo lo que necesita: el DSEE, el des-recorte, el
  siseo, el bajo psicoacústico, el fundido, el modo billar, el relevo sin pausa, los MP3 sin huecos, las tablas y las medidas. Lo
  que en XP vive en la ventana se sacó tal cual (`Motor/DeXP.cs`); lo de Windows, en su forma de Linux (`Motor/Ventana.cs`).
- Comparada con la de XP en 12 casos (6 combinaciones de ajustes, con una canción medida y otra sin medir): **casi iguales**, como
  mucho −135 dB de diferencia (las funciones matemáticas del bajo psicoacústico).
- **Fallo encontrado y arreglado:** el puente a NAudio 3 hacía que dos etapas seguidas se pisaran el arreglo de apoyo; **el sonido de
  `rocola-consola` del hito 4 salía mal a trozos**. Cada etapa sola salía bien, por eso no se vio antes.
- `rocola-consola` ya toca con la cadena entera y los ajustes de fábrica; los MP3 sin el hueco del codificador (NLayer retrasa 529
  muestras, medido al arrancar como en XP). Probado en QEMU grabando la tarjeta de sonido emulada.
- Sin reflexión: NAudio 3 ya da la máscara de canales (`FormatoAudio`), sin avisos de NativeAOT.

## Hito 6 (1-11-2026): la configuración y lo aprendido

- **La configuración:** `rocola-consola` lee `rocola_config.txt` de la carpeta de los datos, con las mismas claves del sonido que XP
  (45: el modo billar, cada interruptor de la cadena, el fundido, el nivelado, el EQ, el bajo, el DSEE...). Las líneas de cada clave
  son las de XP tal cual (`Motor/Configuracion.cs`); las de la pantalla se ignoran por ahora. Solo lee: no escribe la configuración.
- **El arranque**, en el orden de XP (`Motor/Arranque.cs`): los requisitos del DSEE (de `/proc`), la configuración, el volumen
  parejo del billar, las IA (`IAS/`) y las tablas de lo aprendido (volumen, sonoridad, EQ, repasadas a fondo, duraciones).
- **Lo aprendido se guarda** como en XP: la medida del repaso de cada canción nueva (`rocola_sonoridad.txt`) y, al acabar cada
  canción, su volumen, la huella del EQ (`rocola_volumen.txt`, `rocola_eq.txt`), el volumen parejo y la bitácora del billar (con las
  16 columnas de lo que hizo cada etapa: la consola hace de reloj de la canción, cada medio segundo). Mismos archivos y mismo formato
  que XP (con finales CRLF).
- Probado aquí (WAV, .NET y NativeAOT): la segunda vez que suena una canción ya trae su volumen y su EQ; con `local_auto=1` suena con
  el modo billar, aprende su volumen parejo (y lo aplica la vez siguiente) y escribe la bitácora. La cadena sigue casi igual a XP.

## Hito 7 (1-11-2026): la biblioteca y la cola sin pausa

- **La biblioteca** con las reglas de XP (`Motor/Biblioteca.cs`, funciones tal cual): la carpeta `musica` junto a los datos (o hasta
  tres más arriba: con los datos en `/rocola/datos`, la música en `/rocola/musica`) y las de `dir=`; cada carpeta es un disco
  («Artista - Disco»), sin entrar en atajos ni en carpetas del sistema; las canciones por título y los discos por artista. Por ahora
  recorre las carpetas cada vez (la caché de XP, más adelante).
- **La cola con el relevo sin pausa de XP** (`Motor/Cola.cs`): la siguiente se prepara 12 s antes y entra en la misma salida sin
  hueco; la pista siguiente del mismo disco, sin fundido y con la memoria de los filtros de la anterior. Lo aprendido de cada
  canción se cierra en el relevo, como en XP. Sin conversión de frecuencia: si la siguiente tiene otra, se cierra la salida y se
  abre otra.
- `rocola-consola --biblioteca` toca la biblioteca entera disco por disco (o la lista que se le dé).
- Probado aquí (WAV): un disco continuo partido en dos pistas sale **sin hueco ni salto** en la unión (el paso entre muestras igual
  que en el resto); las dos pistas aprenden su volumen y su EQ. La salida ALSA con cola no se ha probado aún (aquí no hay tarjeta).

## Hito 8 (1-11-2026): los botones, las monedas y la rocola en modo texto

- **Los botones** (`Motor/Botones.cs`): se leen de `/dev/input/event*` (evdev: teclados y placas de botones USB) en su hilo, se
  traducen al código de tecla de Windows y se configuran con el **mismo `rocola_teclas.txt` de XP** (arriba, abajo, izquierda,
  derecha, poner, regresar, moneda, saltar). Cuenta al bajar, una vez por pulso (ni al repetirse ni al soltar), como en XP. Sin el
  archivo (o con un botón repetido, como en XP), los de un teclado: flechas, Enter, Retroceso, 5 (moneda) y S (saltar). Aún no está
  el asistente para configurarlos.
- **Monedas, créditos y estado** (`Motor/Creditos.cs`, lo demás tal cual de XP): una moneda son 4 créditos y cada canción cuesta uno;
  no se pone la misma dos veces seguidas ni una dañada; `rocola_estado.txt` con cada cambio y la reanudación tras un apagón dentro
  del mismo día del negocio (desde las 7). Las estadísticas de lo más pedido (`rocola_stats.txt`).
- **La rocola en modo texto** (`rocola-consola --rocola`, `Consola/RocolaTexto.cs`): mientras llega la pantalla gráfica, la rocola
  de verdad en la consola: los discos por letra, entrar, pedir, monedas, saltar, la cola con relevo sin pausa y todo lo aprendido.
- Probado aquí: en una terminal simulada (moneda, entrar, pedir, repetida, saltar, salir; el estado y las estadísticas quedan como
  en XP; la reanudación devuelve créditos, lo que sonaba y la cola), y los botones con un aparato evdev simulado. El sonido con el
  dispositivo `null` de ALSA (sin tarjeta aquí). **No probado con botones ni sonido de verdad.**

Pendiente para la rocola completa: el reinicio de las 7 (créditos a cero si quedó ociosa), el asistente de botones, los vídeos y la
caché de la biblioteca.

## Hito 9 (1-11-2026): la pantalla gráfica, primera versión

- **KMS y OpenGL ES 2** (`Consola/Pantalla/Kms.cs`, `Gl.cs`): sin escritorio; la tarjeta (`/dev/dri/card0`), la salida conectada con
  su modo preferido, una superficie de GBM y EGL, y cada cuadro con cambio de página sincronizado con el barrido (sin cortes). Al
  salir, la consola queda como estaba. Lo mismo que `kmscube` (que ya funcionó en la etapa 1).
- **El dibujo** (`Pantalla/Dibujo.cs`): rectángulos, carátulas y letras en lotes. Las letras se hornean con **StbTrueTypeSharp** y
  las carátulas se leen con **StbImageSharp** (C# puro, licencia MIT: JPEG, PNG, BMP, GIF), reducidas a 256 px (las últimas 96 se
  guardan). La letra es **DejaVu Sans** (va en Rocola OS); el dueño puede poner otra con `ROCOLA_LETRA`. (La de XP es Trebuchet
  MS, de Microsoft: no se puede repartir con el sistema.)
- **La vista** (`VistaGrafica.cs`): arriba los créditos y la cola; las letras; la pared de discos (8 carátulas por página, la
  elegida resaltada en dorado; sin carátula, un cuadro de color con la inicial); dentro de un disco, su carátula y sus canciones;
  abajo lo que suena con su carátula y su avance; los avisos en medio. Escalada a la pantalla (pensada para 1024x768). Es una
  primera versión funcional: el cielo, los cubos y lo demás de la pantalla de XP vendrán después, uno a uno.
- La lógica de la rocola quedó aparte (`Controlador.cs`) y la usan las dos vistas: `rocola-consola --rocola` sale en la pantalla
  gráfica y, si no hay (o con `--texto`), en modo texto. `--captura x.ppm [--boton ...]` saca un cuadro sin pantalla (pruebas).
- Probado aquí fuera de pantalla (Mesa por software): la pared, el disco abierto, los avisos y las carátulas. Se encontró y arregló
  un fallo (subir una carátula a media pantalla dibujaba lo anterior con otra textura).
- **Probado en QEMU por KMS de verdad** (1280x800, `bochs-drm`, Mesa por software): la prueba del arranque (`rocola-prueba`) abre
  ahora la rocola gráfica 20 s y termina con «Pantalla de la rocola: se dibujo 20 s»; la captura muestra la pared con el disco de
  prueba que lleva la imagen (`/rocola/musica/Rocola OS - Prueba`). **No probado en el A4** (Mesa r600).

**Ojo al pasar los datos del bar:** las tablas de XP van por ruta (`D:\musica\...`); en Rocola OS las rutas serán otras
(`/rocola/musica/...`). Habrá que traducirlas una vez al copiar los datos, o se vuelven a medir.

Pendientes conocidos: los MP3 de frecuencias mezcladas (en XP se reconstruyen: `BuildCleanMp3`) aún no; la comparación a ciegas y
«Lo que suena ahora» esperan a la pantalla; leer por delante la siguiente (`MiraPorDelante`) aún no.

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
| Compilar | .NET 10 con NativeAOT: un solo programa nativo, con SSE4.2 y AVX (`IlcInstructionSet`) |

## El orden

1. **El motor de sonido** como proyecto aparte de .NET 10 (`sistema/rocola/Motor`), con las etapas adaptadas a NAudio 3. Se compara
   con la cadena de XP: la misma canción por las dos, muestra a muestra.
2. **Una rocola sin pantalla:** el motor, la biblioteca y la salida ALSA. Toca la cola desde la consola. Se prueba en QEMU y, con
   la memoria USB, en el A4: ahí se ve si el sonido de verdad funciona.
3. **La pantalla:** OpenGL ES por KMS, las letras y las carátulas.
4. **Los botones y la moneda** (`evdev`), Ctrl+F3 y los vídeos.
5. **Rocola OS completo** como paquete de Buildroot (`sistema/package/rocola`), con la música y los datos en su partición.

## Lo que hace falta para empezar

- **El SDK de .NET 10:** instalado aquí desde los repositorios de Ubuntu (`dotnet-sdk-10.0` y `dotnet-sdk-aot-10.0`); NuGet
  funciona.
- **Para el sistema:** compilar la etapa 1, lo que necesita `sources.buildroot.net` (ver LEEME.md), y probarla en el A4.
