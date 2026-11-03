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

## Hito 10 (1-11-2026): Rocola OS arranca en la rocola, con la música del disco de XP

- **La música se queda donde está.** Al arrancar, `rocola-discos` monta **en solo lectura** cada partición que no sea la del
  sistema (el disco de Windows XP del A4 en NTFS, memorias USB en FAT o exFAT, ext4) en `/rocola/discos/<nombre>` y busca carpetas
  `musica` hasta 3 niveles (como XP); una memoria USB sin `musica` se usa entera. La rocola las suma a las suyas (`/run/rocola-musica`).
  No se escribe ni un byte en el disco de XP. El núcleo lleva NTFS (ntfs3), FAT y exFAT.
- **El arranque:** la prueba de la etapa 1 (con la música encontrada en su resumen), 60 s para la foto y **la rocola gráfica**, que
  vuelve a empezar si se cierra. Sus datos en `/rocola/datos`; lo que dice, en `/root/rocola.txt`.
- **Pedir una canción ya no congela la pantalla:** la medida del repaso se hace en un hilo de fondo (como en XP) y se publica después;
  mientras, suena con la regla de las canciones sin medir.
- **Probado en QEMU** con un segundo disco NTFS que imita el de XP (`Rocola/musica` con 11 discos y una carpeta `WINDOWS`): la rocola
  encontró la música, el teclado hizo de botones (moneda: 4 créditos; letras; entrar a un disco; pedir) y en la grabación de la
  tarjeta emulada suenan las canciones pedidas.

Para probar en el A4 hace falta la imagen compilada para el A4 (`sh sistema/compila.sh`, con el SDK de .NET 10 en la máquina que la
arma) grabada en una memoria USB, y arrancar el A4 desde ella. **Nada de esto se ha probado aún en el A4.**

## Hito 11 (1-11-2026): lo que la rocola de XP ya aprendió, y el reinicio de las 7

- **Importar de XP** (`Motor/ImportaXP.cs`): `rocola-discos` busca en los otros discos la carpeta de datos de XP (la de su
  `rocola_sonoridad.txt` o `rocola_config.txt`, sin la de `respaldo`). La primera vez, Rocola OS copia a `/rocola/datos`: la sonoridad
  y las medidas de cada canción, el volumen, el EQ, el sello de cada archivo, las duraciones, las repasadas a fondo, el volumen parejo
  y la bitácora del billar, las estadísticas, **los botones** (`rocola_teclas.txt`), **la configuración** y los modelos de IA.
  - Las rutas de Windows (`D:\Rocola\musica\...`) se traducen a las de Rocola OS buscando el archivo en los discos montados (cada
    letra de unidad queda ligada a su disco); las canciones que ya no están se quedan fuera.
  - Una sola vez: deja `rocola_importado_xp.txt` con lo que hizo, y nunca pisa un archivo que Rocola OS ya tenga. No escribe en el
    disco de XP. Los créditos y la cola no se traen (son del día).
  - XP adopta las medidas de un archivo copiado a otro disco si el tamaño coincide (aunque cambie la fecha de creación), así que las
    medidas importadas nivelan desde la primera canción.
- **El reinicio de las 7 y el respaldo diario** (lo de XP tal cual: `CheckBusinessDayReset` y `BackupLearnedData`): pasadas las 7,
  con la rocola ociosa 10 minutos, los créditos que quedaron de anoche van a cero y lo aprendido se respalda en
  `/rocola/datos/respaldo` (los 3 últimos días). Lo aprendido y las estadísticas se guardan cada minuto.
- **Probado:** aquí, con tablas de XP de verdad en su formato (rutas `D:\...`, una canción borrada, una carpeta `dir=` que ya no
  existe): 30 medidas traducidas, lo que falta fuera, los botones, el modo billar y el modelo de IA traídos. En QEMU, con esos datos
  dentro del disco NTFS: el resumen del arranque muestra la música encontrada y la rocola usa los botones de XP (la moneda en «1»:
  «5» ya no hace nada).

## Hito 12 (1-11-2026): el arranque de uso diario, BIOS y UEFI

- **Arranque de uso diario:** la prueba de sonido y gráfica solo la primera vez (o desde el menú de GRUB: «prueba»); después, directo
  a la rocola (`/usr/bin/rocola`), que como la de XP pone el volumen general al máximo y sin silencio, y vuelve a empezar si se cierra.
- **BIOS y UEFI:** `disk.img` lleva GRUB de BIOS en el sector de arranque y GRUB de UEFI en una partición EFI (la 2); el núcleo, con
  su soporte EFI. Probado en QEMU de las dos formas (UEFI con OVMF): las dos llegan a la prueba.
- El sistema, en 1 GB (antes 512 MB): sitio para los datos, sus respaldos de 3 días y lo importado de XP.
- Cómo compilar y grabar la imagen del A4, y qué esperar al arrancarla: `LEEME.md`, «En el A4».

## Hito 13 (1-11-2026): el asistente de botones, lo más sonado, el buscador y la navegación de XP

- **El asistente de botones** de XP: sin `rocola_teclas.txt` (o con F8) pide los 8 botones por turno, cada uno distinto, y guarda el
  archivo en el formato de XP.
- **Lo más sonado** (`BuildTopDiscs` de XP): la letra TOP, a la izquierda de la A, con un disco «TOP 100» por género y por artista con lo
  más pedido. La rocola arranca en la primera letra real, como XP.
- **El buscador** (`RecomputeResults` y `BuildSearchIndex` de XP, sin la pantalla: la misma puntuación, la caché por letra y la
  errata): REGRESAR mantenido + ARRIBA lo abre y lo cierra; el teclado de XP en pantalla (1234567890 / QWERTYUIOP / ASDFGHJKLÑ /
  ZXCVBNM / ESPACIO y BORRAR); a la derecha del teclado, los resultados (artistas, discos y canciones); una canción se pide sin cerrar
  el buscador, un disco se abre, un artista lleva a su disco. REGRESAR borra una letra.
- **La navegación de XP:** en la pared, IZQ/DER de disco, ARRIBA/ABAJO de página, REGRESAR mantenido + IZQ/DER de letra; dentro de un
  disco, ARRIBA/ABAJO de canción, IZQ/DER de página. Para los combos, evdev dice qué botones siguen apretados.
- **La memoria de la biblioteca de XP no se trajo:** para saber si vale, recorre todas las carpetas mirando tamaños y fechas, que
  es más que el recorrido normal (los títulos salen del nombre del archivo, sin abrirlo).
- Probado aquí fuera de pantalla (asistente, TOP, buscador: escribir, pasar a los resultados, elegir).

## Hito 14 (1-11-2026): los vídeos

- **La sección de vídeos de XP** (`BuildVideoSection`): los vídeos de las carpetas de música (las mismas extensiones que XP) van en la
  letra VIDEOS, por carpeta; se piden, cuestan un crédito y entran en la cola y en el estado como en XP.
- **Suenan con mpv** (en Rocola OS: por KMS, a pantalla completa, y por ALSA, con `video_vol=` de XP): la rocola cede la pantalla
  (`drmDropMaster`) mientras dura el vídeo y la recupera al acabar. MONEDA sigue contando y SALTAR lo corta. Si mpv falla, la rocola
  dice por qué en el aviso y en `/root/rocola.txt` (lo de mpv, en `/tmp/rocola-mpv.txt`).
- **Fallo encontrado y arreglado:** un combo rápido (REGRESAR + IZQ) no se reconocía si REGRESAR se soltaba antes de que la rocola
  atendiera la pulsación; ahora lo apretado se anota en el momento de la pulsación (`PulsadoConRegresar`).
- **Probado en QEMU** con un vídeo de 10 s en el disco NTFS: se ve a pantalla completa, se oye su sonido y al acabar vuelve la rocola.
  `--guion` (pruebas): una lista de botones y esperas para probar el bucle de verdad sin pantalla ni teclado.

## Hito 15 (1-11-2026): los ajustes del dueño y el botón de encendido

- **Los ajustes del dueño (Ctrl+F3 en un teclado, como en XP):** los interruptores del sonido de XP (el modo billar, el local, voz/golpe/
  retumbe, lo que suena ahora, las mejoras con IA, el fundido y el volumen de los vídeos), con lo que decide el modo billar en gris y
  el motivo, como en XP. Se manejan con los botones; REGRESAR guarda en `rocola_config.txt` (conservando las demás líneas) y se oye desde
  la siguiente canción. Desde ahí, también el asistente de botones.
- **El botón de encendido:** como en XP, apaga el equipo y es un **cierre** (no un apagón): la rocola guarda lo aprendido y borra el
  estado del día (créditos y cola), así al día siguiente arranca limpia. Va por `acpid` de busybox (`/etc/acpi/PWRF/00000080`), que
  avisa a la rocola (SIGTERM) y apaga. Un corte de luz no pasa por ahí: el estado sobrevive y la rocola reanuda.
- Probado: los ajustes se guardan y se releen; en QEMU, con el botón de encendido virtual, el equipo se apaga solo en 8 s.

## Hito 16 (1-11-2026): la pantalla, más cerca de la de XP

- **El cielo Legacy, en lo esencial:** la noche en degradado, 260 estrellas que titilan (siempre el mismo cielo) y la aurora: un velo de
  tiras finas que ondula despacio del verde al violeta sobre el horizonte. Unos 30 cuadros por segundo; las franjas de arriba y abajo,
  translúcidas.
- **La portada enfocada de XP** (`portada_enfocada=`): la elegida crece un 12 % en 170 ms desde el centro de su borde de abajo, con un
  halo del color de la propia portada (pesando los puntos con color, como en XP) y el marco dorado; las demás bajan un 20 % de brillo.
- **El letrero de la portada:** «5 canciones · 18 min» (o «3 videos») sobre su borde de abajo; los minutos solo si se sabe lo que dura
  cada canción (como en XP: no se inventan) y, si no cabe, sin los minutos.
- Probado aquí fuera de pantalla. Aún no están los cubos, la estrella, las ondas ni el estado extendido de XP.

## Hito 17 (1-11-2026): el pulso de la música y el repaso

- **El cielo late con la música** (como el pulso de XP: va con lo que se oye): el motor mide a la salida, sin tocar el sonido, la
  fuerza de los graves, los medios y los agudos por bloques de 1024 cuadros, con el cuadro en que suenan (`Motor/Pulso.cs`); la
  pantalla pide el del cuadro que se está oyendo (lo entregado a la tarjeta menos su colchón). La aurora sube con los medios y brilla
  con los graves; algunas estrellas, con los agudos. Comprobado: con el pulso, el sonido sale igual (solo cambia el dither, que es
  aleatorio).
- **El repaso de la biblioteca** (la tanda larga de XP, en lo esencial): al arrancar, las canciones aún sin medir con el algoritmo de hoy
  van a una fila de fondo; un hilo de prioridad mínima las mide poco a poco (lo pedido, antes que nada) y se guarda cada 30 s. Se para
  mientras suena un vídeo. Así cada canción nivela bien desde la primera vez. Probado: 30 canciones medidas solas en 15 s.
- **El relevo sin pausa entre frecuencias distintas** (48000 tras 44100), como en XP: la siguiente se convierte a la de la salida con el
  convertidor de XP (`RemuestreoContinuo`, con su limitador detrás) y entra sin cerrar la salida. Las tablas de 44100↔48000 se preparan
  al arrancar, en segundo plano. Probado: un disco de tres pistas a 44100, 48000 y 44100 Hz sale seguido (45 s justos) y la de 48000
  suena a su tono (440 Hz; sin convertir habría salido a 404).

## Extra (1-11-2026): la atracción

- **Cuando nadie la usa** (no suena nada, la fila está vacía, no hay vídeo ni hoja abierta y pasan 40 s sin botones), la rocola enseña
  sola sus discos, como el escaparate de XP en lo esencial: uno al azar cada 8 s (los de vídeos no), con su portada en grande, el nombre,
  el artista, cuántas veces se ha pedido y cuántas canciones trae, y aparece y se va en 0,6 s. Debajo, un aviso que cambia cada 5 s
  («Toca un botón y elige entre N canciones», «una moneda = 4 canciones», el buscador con el botón rojo).
- Cualquier botón la quita y no hace nada más (la moneda sí cuenta). Probado en captura (`--captura x.ppm --boton atraccion`).

## Extra (1-11-2026): los MP3 de frecuencias mezcladas

- Unos pocos MP3 traen cuadros de otra frecuencia (o mono entre estéreo). En XP el ACM falla con ellos y se reconstruyen
  (`BuildCleanMp3`); NLayer no falla, pero tocaba esos cuadros a la frecuencia del primero. Probado con un MP3 de 20 s a 44100 + 5 s a
  48000 (880 Hz) + 20 s a 44100: ese tramo salía a 808 Hz y más largo.
- Ahora el lector (`Lector.cs`: `Mp3DeUnaFrecuencia`) mira siempre las cabeceras de los cuadros y, si hay más de una clave (frecuencia
  y canales), copia a memoria solo los de la dominante, como XP; los de la minoría se descartan (a lo sumo un salto). Sin quitar el
  relleno del codificador, como XP. La medida del repaso usa el mismo lector. El mismo archivo sale ahora 40 s seguidos a 440 Hz.
- Mirar las cabeceras de un MP3 de 16 MB tarda 30 ms aquí; en uno normal no se lee nada más.

## Extra (1-11-2026): «Lo que suena ahora» en los ajustes

- Lo de XP (`LoQueSuena.cs`, copiado tal cual al motor): **«Escuchar sin mejoras»** (S4) va en la hoja de ajustes, en su grupo; dura
  15 s o hasta volver a pulsarlo o salir de la hoja, y no se guarda (es una herramienta). Abajo de la hoja, un recuadro con **lo que la
  cadena le hace a la que suena** (S5): el nivel que sale, lo que baja el limitador y las etapas que de verdad actúan. Se lee cada medio
  segundo y solo con la hoja abierta (sus lecturas se ponen a cero al leerlas: un solo lector, como en XP).
- Probado con el motor y una canción (sin pantalla): el texto cambia al encender «sin mejoras» y vuelve al apagarlo; y en captura.

## Extra (1-11-2026): comparar a ciegas

- `CompararCiegas.cs` de XP, copiado tal cual al motor (antes había solo piezas sueltas en `DeXP.cs`). En la hoja de ajustes, grupo
  «Lo que suena ahora»: **billar o fábrica**, **con o sin afinar** y **presencia nueva o de antes** (EMPEZAR / PARAR) y **«Me gusta
  esta»** (un voto por tramo). 8 tramos de 15 s al azar; el resultado sale en el recuadro de abajo.
- Como en XP: el modo cambia de verdad en vivo, pero lo guardado es el de antes; tocar otro ajuste o salir de la hoja la corta y vuelve
  lo de antes; si la canción se acaba, se corta antes de armar la siguiente; el volumen parejo no aprende de esa canción.
- En Rocola OS, además, la hoja no delata el tramo: «Modo billar» y los grises muestran lo de antes mientras se compara.
- Probado con el motor y una canción a paso real (34 s): el modo cambió entre tramos, la configuración guardaba «1» todo el rato, el
  voto contó y al parar volvió el modo billar. «Con o sin afinar» pide antes las bandas del celular (aún sin la hoja de afinar).

## Extra (1-11-2026): afinar el local con el celular y la prueba de bocinas

- **Afinar con el celular** (AfinarLocal.cs de XP, en su forma más simple de la sexta tanda), grupo nuevo de la hoja de ajustes:
  «Tocar las 8 bandas (de grave a agudo)» (EMPEZAR/PARAR; el recuadro de abajo dice la banda que se oye: «250 Hz 3 de 8»), «Tu app
  dice» dBA o dBC (la Z guardada se sigue leyendo), lo que marca cada banda (IZQ/DER de 1 dB; desde «sin medir» empieza en 70 dB) y
  «Usar la afinación» (en gris sin el modo billar o sin las bandas de la voz). El recuadro de abajo dice la corrección por banda. Mismas
  claves que XP (`afinado_local=`, `afinado_ponderacion=`, `afinado_usar=`); lo que cambia se aplica al instante en la que suena, y
  tocar algo aquí corta la comparación a ciegas, como en XP.
- **La prueba de bocinas** (SalaLocal.cs de XP), en «El local»: IZQ/DER pasan por callada, izquierda, derecha, las dos y la polaridad.
- Las clases del ruido (`PruebaBocinas`, `PruebaBanda`) son las de XP tal cual (`Motor/PruebasLocal.cs`). En XP cada prueba abre su
  propia salida; aquí el motor dice cuál debe sonar y el programa la toca por su propia salida ALSA, sin la cadena, solo con nada
  sonando; si alguien pone una canción, la prueba se calla; se para al salir de la hoja.
- Probado con el motor: las lecturas se guardan y se vuelven a leer al arrancar; el ruido sale a −20 dB por banda y el letrero sigue la
  banda; y en captura. **Sin oírlo:** falta tocarlo por la tarjeta (QEMU o el A4).

## Extra (1-11-2026): el estado extendido

- Como en XP (`muro_reposo=`, `estado_expandido=`, `cola_doce=`, los tres encendidos de fábrica y ahora en la hoja de ajustes, grupo
  «Pantalla»): con algo sonando, 15 s sin botones, en la pared y sin hojas abiertas, la pared se aparta (el cielo vuelve encima en
  0,6 s) y la franja de lo que suena crece a un tercio de la pantalla: la portada grande con su halo (late con el grave), «SUENA AHORA»,
  el título, el artista, el género, el reloj («1:23 / 3:45») con su barra, y a la derecha la cola con sus portadas y en cuánto suena
  cada una («EN COLA 2 · en ~4 min», `TextoCola` y `MinutosEspera` de XP): hasta tres, o hasta doce en dos columnas.
- El primer botón solo la despierta (la moneda, además, cuenta), como la atracción. Los avisos salen encima del panel.
- Probado en captura (`--captura x.ppm --boton reposo`: créditos, cinco pedidas y 20 s sin botones).

## Extra (1-11-2026): la estrella de la ayuda

- Como en XP (AsistenteRocola.cs y AsistenteGuiaRocola.cs): **ROJO + ABAJO** abre y cierra la ayuda. La estrella vive en su casilla de
  la barra de arriba («AYUDA», con sus dos botones dibujados, al lado de «BUSCAR» con los suyos) y vuela a su globo al abrirla.
- **Sin rostro** (decisión del 10-10-2026): conserva el cuerpo del icono; sus cinco puntas son la cabeza, los brazos (con su mano) y las
  piernas (con su pie). Se expresa solo con el cuerpo: saluda con la mano, señala con el brazo hacia lo que toca, abre los brazos al
  explicar, salta con el brazo arriba al acertar, busca con la mano en la frente (la moneda), festeja con los dos brazos y un salto, y
  se despide con una reverencia. Respira y se mece.
- Primero pregunta «¿Qué quieres hacer?» (poner una canción, buscarla por su nombre, aprender a moverme); después **lleva de la mano**:
  mira lo que de verdad pasa (dónde estás, si hay crédito, si abriste el buscador, si ya pediste), ilumina lo que toca, lo señala con
  una flecha dorada y dibuja los botones; los botones hacen lo de siempre. «Paso 2 de 3», «¡Muy bien!», «¡Lo lograste!» y vuelve sola.
  Las reglas puras (`PasoParaCancion`, `OrdenPaso`, `NumeroPaso`, `PasoDespedida`) y los textos son los de XP; 45 s sin tocar nada: se
  despide («¡Adiós!») y a los 2,2 s se va; si alguien toca algo mientras, se queda. Con la ayuda abierta no hay reposo ni atracción.
- En 2D (la 3D de XP no se hizo). Probado en captura: el menú, «Elige un disco» con la flecha, «Aprender a moverme» avanzando al paso 2,
  y la misión de poner una canción hasta «¡Lo lograste!». En el modo texto va en una línea.

## Extra (1-11-2026): los cubos

- **El cubo que gira** de XP (Cube.Draw y `PreparaCubo` de D3D9Star.cs): la portada elegida en la pared gira como un prisma de cuatro
  caras con la portada, visto de frente; cada cara visible se oscurece con la profundidad (hasta 130 de 255), a 1,44 rad/s (0,06 por
  paso de 24 por segundo) en el sentido en que se movió la selección, y con la portada enfocada (crece un 12 %, halo y letrero).
- **El relevo de giro** (AlbumView.MoveSelectionTo de XP): al cambiar de disco en la misma página, el que se deja da un giro rápido
  (0,7 por paso) que se apaga (×0,85 por paso) y se asienta plano en la vuelta entera más cercana; el nuevo arranca plano.
- **El cubo chico** de la barra de lo que suena también gira. Sin portada: el color del nombre y su inicial cuando está casi de frente.
- Probado en captura (`--boton derecha`).

## Extra (1-11-2026): la pantalla viva de XP (primera tanda: la pared)

- **Ficha rápida** (`ficha_disco=`): si el cursor se queda 1,1 s en un disco, al lado sale una tarjeta (entra en 0,22 s, se queda 9 s y
  se va en 0,4 s; `CurvaFicha` de XP) con su portadita, el artista, el disco, cuántas y cuánto duran, «N en la cola» si van suyas, y
  sus tres canciones más pedidas (`MasPedidas` de XP, tal cual; si nadie las ha pedido, las tres primeras) con su duración y sus veces.
  Nunca en el TOP ni en el reposo. Va a la derecha de la casilla si cabe; si no, a la izquierda (`LugarFicha` de XP).
- **Barra de letras con lupa** (`letras_lupa=`): la elegida y sus vecinas más grandes, y una rayita debajo de cada letra dice cuánta
  música tiene.
- **Pasar de página deslizando** (`pagina_desliza=`): la vieja sale por un lado mientras la nueva entra por el otro, columna a columna
  (0,38 s cada una, 45 ms entre columnas). Dentro de la misma letra.
- Los tres con su interruptor en la hoja de ajustes (grupo «Pantalla»), encendidos de fábrica, con las claves de XP. Probado en
  captura la ficha, la lupa y el deslizamiento a mitad (con una biblioteca de 12 discos; `--cuadros N` elige cuántos cuadros de
  50 ms se dibujan antes de la captura). El sentido es el de la flecha apretada (con dos páginas, ir y volver llevan a la misma).

## Extra (1-11-2026): la pantalla viva de XP (tandas 2 y 3: dentro del disco, lo que suena y los créditos)

- **Lista grande** (`lista_grande=`): filas más altas y letra más grande dentro del disco.
- **Ecualizador en la que suena** (`eq_sonando=`): su pastilla SONANDO lleva cuatro barritas que se mueven con la música (el pulso
  que mide el motor a la salida).
- **En cola, con los minutos** (`cola_minutos=`): «EN COLA 3 · en ~9 min» en las filas (`TextoCola`/`MinutosEspera` de XP).
- **Ver adónde va** (`vuelo_cola=`): al pedir una canción (en el disco o en el buscador), su boleto sale de la fila y vuela en arco
  hasta la cápsula de la cola (o hasta lo que suena, si no sonaba nada); al llegar, la cápsula da un saltito.
- **La moneda que cae** (`moneda_cae=`): cae una moneda dorada girando hasta la cápsula de Créditos, que da un destello verde, y sube
  «+4 créditos».
- **Sin créditos, invitar** (`sin_creditos_invita=`): en vez del letrero, una tarjeta amable con la ranura y una moneda entrando: «Te
  falta un crédito. Inserta una moneda para poner «…»». Se va sola a los 4,2 s (o al meter la moneda).
- **Avisos que se entienden** (`avisos_claros=`): entran por la derecha con su color y su icono (hecho, cuidado, error, novedad), en
  dos renglones (`PartirAviso` de XP, tal cual) y con una línea que enseña cuánto les queda. En XP el tipo sale del color con que avisa
  cada sitio; en Rocola OS, de lo que dice el aviso.
- Los siete con su interruptor (grupo «Pantalla»), encendidos de fábrica, con las claves de XP. Probados en captura (`--cuadros`).

## Extra (1-11-2026): la pantalla viva de XP (tanda 4: la atracción)

- **El escaparate** (`escaparate=`): en la atracción el cubo de la portada, en vez de girar, se mece (se ve de frente) y a su lado va
  la ficha del disco; sin él, el cubo gira como siempre y solo quedan los mensajes.
- **Lo más pedido esta noche** (`top_noche=`): la cuenta de XP tal cual (`Motor/Noche.cs`: se cuenta al entrar una canción a la cola,
  desde las 8 de la mañana, en `rocola_noche.txt`, cada minuto si cambió y al cerrar; solo canciones). En la atracción, cada 30 s y
  durante 15, el cubo se hace chico en la esquina y salen las cinco más pedidas, una a una desde la izquierda, con su portada y una
  barra que crece con sus veces; hacen falta al menos tres distintas. Y entre los mensajes, «Lo más pedido esta noche: «…»».
- Probado en captura (`--boton noche`, con una cuenta de prueba).

## Extra (1-11-2026): la pantalla viva de XP (tanda 5: el estilo)

- **Letra para leer de lejos** (`letra_lejos=`): en la franja de lo que suena, el título crece (si no cabe, el de siempre con puntos
  suspensivos) y lo que sigue va en una tarjeta lateral «A CONTINUACIÓN» (la siguiente y «y N más en la cola»).
- **El color del género en todo** (`color_genero=`): los colores de XP tal cual (`ColorDeEtiqueta`, en `Motor/Estilo.cs`; un solo
  sitio: etiqueta → color). Un resplandor de ese color que respira despacio (su propio reloj de 5 s, no la música) detrás de la portada
  de lo que suena (la chica y la grande del reposo) y de la grande dentro de un disco (el género que manda en él, leído una vez por
  disco en un hilo de fondo, sobre copias de sus canciones -`LeeEtiquetaAparte` de XP-: la pantalla no espera); la pastilla del género (late con los graves) y la barra de avance de su color.
- Arreglo: al empezar una canción se leen sus etiquetas (como XP); las que venían del estado guardado no tenían género.
- Probado en captura con un MP3 etiquetado «Cumbia» (sale «Regional», naranja tierra).

## Extra (1-11-2026): las pistas del rojo y los textos que se deslizan

- **Pistas del rojo mantenido** (`guia_rojo=`, GuiaRojoGpu.cs de XP): con el ROJO mantenido en la pared se encienden la barra de letras
  (con una flecha dorada a cada lado: ROJO + IZQ/DER cambia de letra) y las casillas BUSCAR y AYUDA, con su propia flecha llena de rojo
  vivo y un brillo que late (sin flechitas sueltas, como pidió el dueño el 12-10-2026). El rojo mantenido lo dicen los botones (evdev).
- **Los textos que se deslizan** (`textos_deslizantes=`): un título que no cabe (en lo que suena y en el estado extendido) se corre
  despacio hasta el final, espera y vuelve, recortado a su caja (scissor de GL); apagado, se corta con puntos suspensivos como antes.
- Probados en captura (`--boton rojo`; el deslizamiento a mitad de recorrido con `--cuadros 50`).

## Extra (1-11-2026): el buscador con portadas

- Como en XP (`buscador_portadas=`, BuscadorPortadasGpu.cs; encendido de fábrica, en la hoja de ajustes): arriba la casilla de lo
  escrito, grande, con su lupa y cuántos resultados hay; debajo, **los atajos** (solo los que tienen algo detrás: «Lo más pedido hoy»
  —la cuenta de la noche como resultados—, «Videos» —a su sección de la pared— y «Buscaste: …», las dos últimas búsquedas con las que
  se eligió algo, solo en memoria y nunca la clave de servicio); a la izquierda **el teclado compacto** (teclas de tamaño fijo) y a la
  derecha **las tarjetas** en dos columnas con su portada (o la inicial), qué es, el nombre con lo tecleado resaltado (sin distinguir
  mayúsculas ni acentos), un renglón que ayuda a elegir (el artista y el disco, o cuántas canciones) y SONANDO o EN COLA si toca; si hay
  más de las que caben, baja fila a fila y una barrita lo dice.
- Los atajos van en una fila encima del teclado (ARRIBA desde la primera fila); las tarjetas se recorren con las cuatro flechas.
- El atajo «Karaoke» de XP no está: en Rocola OS el género de cada disco no se sabe sin leer sus canciones.
- Probado en captura (escribiendo «C» con los botones).

## Extra (1-11-2026): el diagnóstico completo

- Como el de XP (Ctrl+F3 → Imagen → «Diagnóstico completo»; una herramienta, sin preferencia guardada): en la hoja de ajustes, grupo
  «Diagnóstico», EMPEZAR. Recorre ocho escenas, 6 s cada una, moviendo lo que se movería con los botones (pared quieta, pared
  moviéndose, pasando de página, dentro de un disco, buscador escribiendo, estado extendido si suena algo, atracción y la ayuda de la
  estrella guiando) y mide **los intervalos entre cuadros presentados**: cuadros, por segundo, mediana, p95, p99 y el peor, y las
  recolecciones de memoria de cada escena (regla del 9-10-2026: no solo los cuadros por segundo). No pide canciones ni toca créditos;
  al acabar deja la pantalla como estaba y añade el informe a `rocola_diagnostico.txt` (carpeta de datos). Cualquier botón lo corta.
- Probado aquí fuera de pantalla con llvmpipe (sin tarjeta gráfica): unos 25 cuadros/s, p99 de 44 a 55 ms. **No son números del A4**:
  hay que correrlo allí.

## Extra (1-11-2026): la estrella del cielo, en 3D

- El «astro principal» de XP (`luna=`): una estrella viaja despacio por el cielo, detrás de todo, y en el reposo crece un 32 %, va más
  deprisa y se ve entera. **En 3D** (`estrella_3d=`): la clase `Estrella3d` de XP tal cual (el sólido de cinco puntas con el centro
  levantado por delante y por detrás, 12 vértices y 20 caras, de atrás hacia adelante, con la luz principal arriba a la izquierda, la
  fría a la derecha y el brillo que corre por las facetas al girar); gira una vuelta cada ~19 s navegando y cada ~3,4 s en reposo, con
  su vaivén. El recorrido (`StarTravelCenter`), el tamaño y la suavidad de las velocidades son los de XP.
- **Su corona** (`halo_lunar=`), **su polvo** en tres órbitas con cola (`polvo_lunar=`, por delante y por detrás) y **su estela de
  copias** (`estela_estelar=`, tipo Sandevistan: una copia congelada cada radio recorrido, con su giro de ese momento, de un color de la
  paleta, que nace transparente y se apaga en 1,2 s). Teñida con el color del género de lo que suena.
- Durante la ayuda no sale (la estrella es la de la ayuda), como en XP. Los cinco interruptores, en la hoja (grupo «Pantalla»),
  encendidos de fábrica. Probado en captura (`--boton reposo --cuadros 140`).

## Extra (1-11-2026): probado entero en QEMU; la prueba del arranque y la prioridad de los hilos

- **Imagen de QEMU recompilada con todo lo de hoy** y arrancada (BIOS, con el disco NTFS de XP de prueba): la prueba del primer
  arranque, después la rocola por KMS (softpipe, 1280×800): la pared con su cubo y la ficha rápida, la estrella 3D viajando por el
  cielo, la moneda, el disco, pedir (suena por ALSA: grabado de la tarjeta emulada) y la ayuda de la estrella con su menú. Los registros
  de dentro (`/root/rocola.txt`, `rocola_error.txt`) sin fallos. (QEMU aquí emula la CPU sin KVM: va muy lento, no mide fluidez.)
- **Fallo encontrado y arreglado:** la prueba del arranque decía «Pantalla de la rocola: FALLO 0». Era falso: al acabar sus 20 s,
  `timeout` le manda SIGTERM y la rocola ahora se cierra ordenadamente (como con el botón de encendido) con código 0; la prueba solo
  aceptaba 124/143. Ahora mide que aguantó los 20 s: «se dibujó 20 s (y se cerró bien)».
- **La prioridad de los hilos, de verdad** (`Motor/Prioridad.cs`): en Linux, `Thread.Priority` de .NET no hace nada, así que el sonido
  competía de igual a igual con el dibujo y el repaso. Ahora el hilo de la salida ALSA va en tiempo real (SCHED_FIFO 20; si no se
  puede, nice −15) y el repaso de la biblioteca con nice 19, como las prioridades de XP. `/root/rocola.txt` dice cómo quedó («Sonido:
  el hilo de la salida va en tiempo real», comprobado en QEMU).

## Extra (1-11-2026): la imagen del A4 y un fallo de `compila.sh`

- **Fallo encontrado y arreglado:** `compila.sh` aplicaba la configuración del A4 solo la primera vez. Al compilar la imagen del A4
  aquí se usó una configuración de los primeros días: sin la rocola, sin mpv, sin el arranque UEFI, y con el `genimage-bios.cfg` ya
  renombrado (por eso falló al final). Al dueño le pasaría lo mismo al actualizar. Ahora, si `configs/rocola_a4_defconfig` es más
  nueva que la configuración de la compilación, se vuelve a aplicar (en los dos perfiles). Ojo: si cambian las opciones de un paquete
  ya compilado (como GRUB al añadir UEFI), ese paquete hay que rehacerlo (`sh sistema/compila.sh grub2-dirclean`, y luego compilar).

## Extra (1-11-2026): la imagen del A4 compilada, el núcleo genérico y las instrucciones de la rocola

- **La imagen del A4 se compila entera** (con la configuración ya buena): lleva la rocola, mpv, DejaVu y el firmware de la Radeon.
- **Fallo encontrado:** arrancada en QEMU se reiniciaba en GRUB sin decir nada, con cualquier procesador. Buildroot compila su GCC con
  `--with-arch=bdver2`, así que el núcleo y su descompresor salían con instrucciones BMI y TBM del Piledriver (miles de `bextr`, `blcs`,
  `tzmsk`). En el A4 existen, pero un núcleo así no arranca en ningún otro procesador ni en ninguna emulación. Ahora el núcleo se compila
  con `-march=x86-64` explícito (`sistema/external.mk`); la rocola y las bibliotecas siguen con las instrucciones del A4.
- **Las instrucciones de la rocola:** el binario lleva instrucciones AVX2 (el A4 no tiene AVX2). Probado con QEMU de usuario emulando un
  Piledriver sin AVX2 (`qemu-x86_64 -cpu Opteron_G5`, que sí rechaza una instrucción AVX2 suelta): la rocola mide, descomprime, pasa
  la cadena entera y escribe el WAV sin fallar. Ese AVX2 son caminos que .NET solo usa si el procesador lo tiene.
- **Comprobado tras el arreglo:** el núcleo nuevo ya no lleva BMI/TBM (quedan `tzcnt`, que Linux usa a propósito porque en los
  procesadores viejos vale como `bsf`, y `popcnt`, que Linux cambia en el arranque según el procesador) y arranca entero en QEMU con
  el procesador por defecto. (Con el modelo Opteron_G5 la emulación se queda en las protecciones contra Spectre de esa familia: límite
  de QEMU.) Lo que sigue del sistema va con las instrucciones del Piledriver (XOP, FMA4, TBM) y QEMU no las emula: el primer programa
  se cae ahí, como es de esperar; eso solo se prueba en el A4.
- **La pantalla de la rocola del A4** (el binario de la imagen) también, con QEMU de usuario como Piledriver sin AVX2 y Mesa softpipe:
  dibuja el estado extendido con la estrella 3D y el género. (Con llvmpipe de esta máquina no vale: genera código para la CPU real.)
- Si la imagen del A4 no arrancara allí, la de QEMU (x86-64 genérico) también sirve para el A4 (`LEEME.md`, paso 7).

## Extra (1-11-2026): internet, el programa que se actualiza solo y el encendido remoto

- **Internet por cable** (decisión del dueño): DHCP en `eth0` (la Realtek del A4), la hora por NTP, `curl` con los certificados, `jq`
  y `ethtool`.
- **GitHub compila el programa** (`.github/workflows/rocola-programa.yml`): en cada cambio de `sistema/rocola/` en esta rama compila
  `rocola-consola` con .NET 10 (NativeAOT) en dos versiones (la del A4, con AVX, y la genérica), comprueba que arranca y que toca la
  canción de prueba, y lo publica como la versión «programa-N» del repositorio, con sus sumas SHA-256. **Probado:** la primera
  (`programa-1`) salió bien.
- **La rocola se actualiza sola** (`rocola-actualiza`, en el sistema, aparte del programa para que una versión mala no pueda romper las
  actualizaciones): al minuto de prender y cada hora (o «Buscar actualización ahora» en Ctrl+F3 → Internet) pregunta a GitHub con la
  llave de solo lectura (`rocola_github.txt`, que copia de una memoria USB), baja la versión de su procesador, comprueba la suma y que
  arranca (`--version`), y la deja lista. El programa, en cuanto está libre (nada sonando ni en la cola, ninguna hoja abierta y 2 min
  sin botones), guarda su estado y sale con 42; `/usr/bin/rocola` pone la nueva. Si la nueva se cae dos veces en sus primeros 2 min,
  vuelve a la anterior y la apunta como mala. Lo que pasa, en `rocola_actualizaciones.txt`. **Probado** contra GitHub: bajó, comprobó
  y dejó lista la versión 1; y la vuelta atrás con una versión que se cae (volvió a la 1 y apuntó la 2 como mala).
- **Ctrl+F3 → Internet:** «Actualizarse sola» (`actualizar_solo=`), «Buscar actualización ahora», y abajo la versión, la red con la
  MAC y cómo va la actualización.
- **Encendido remoto (Wake on LAN):** la tarjeta queda escuchando el paquete mágico al prender y al apagar (`ethtool -s eth0 wol g`). Lo
  manda el router del bar (decisión del dueño); falta activarlo en el BIOS y configurarlo en el router (`LEEME.md`).
- Nada de esto probado aún en el A4 ni con el router del bar.

## Extra (1-11-2026): el sistema operativo también se actualiza (soporte continuo)

- **Dos copias del sistema** en la rocola instalada: `\rocola-os\a\` y `\rocola-os\b\` (núcleo, initramfs y `sistema.img`), y
  `\rocola-os\arranque.cfg` dice cuál prende (GRUB lo lee con `source`). **Lo aprendido va aparte**, en `\rocola-os\datos.img`
  (montado en `/rocola/datos`), y con él las versiones bajadas del programa (`/rocola/datos/programa`): cambiar el sistema no lo toca.
- **GitHub compila el sistema entero** (`.github/workflows/rocola-sistema.yml`: `sh sistema/compila.sh` con la imagen del A4, guardando
  descargas y ccache entre compilaciones), prueba que el núcleo arranca en QEMU y publica «sistema-N» con `sistema.img.gz`, `bzImage`,
  `initrd.gz` y sus sumas, y además `disk.img.gz` (la imagen de la memoria USB del instalador).
- **`rocola-actualiza`** (después del programa): en la rocola instalada baja el sistema nuevo a la copia que no está en uso, comprueba
  las sumas, lo apunta en `arranque.cfg`, deja `probando` y la rocola se reinicia sola cuando está libre (sale con 43).
- **La vuelta atrás:** el initramfs cuenta los arranques del sistema en prueba; la rocola lo confirma a los 3 minutos en pie
  (S99rocola borra `probando`); si a los 15 minutos no se confirmó, reinicia, y al tercer arranque sin confirmar el initramfs vuelve al
  anterior y lo apunta como malo. Con ESC al prender, «la versión anterior del sistema».
- El instalador ya instala así (2,3 GB libres). **Sin probar todavía:** la primera compilación del sistema en GitHub y la vuelta
  atrás en QEMU (siguiente paso).

## Extra (2-11-2026): los MP3 por libmpg123, y Ctrl+F3 con todo el sonido de XP

- **NLayer 3.0.0 se come un cuadro.** Comparado con ffmpeg, NLayer pierde un cuadro entero (1152 muestras, 26 ms) a media canción en
  cada uno de los tres MP3 de prueba (en el cuadro 815, 2048 y 5221), con un chasquido donde falta. No depende del tamaño de lectura
  ni de leer de archivo o de memoria: es del descompresor. libmpg123 da lo mismo que ffmpeg (a 3e-7); fuera de ese cuadro, lo mismo
  que NLayer (a 2e-8), y sin su gapless las muestras caen en el mismo sitio (SinRelleno y el clip del relleno siguen valiendo).
- **Ahora los MP3 van por libmpg123** (`rocola/Motor/Mpg123.cs`, `BR2_PACKAGE_MPG123=y`), para sonar y para medir; NLayer queda
  de respaldo (sin la biblioteca, si el archivo no abre, o en los de frecuencias mezcladas, que se reconstruyen como antes).
  La cadena entera a WAV sale igual (±2 LSB del dither) hasta el cuadro que NLayer perdía.
- **El repaso, mas rapido:** descomprimir 240 s pasa de 2,5 s a 0,3 s (320 kbit/s) y la medida completa de XP de 3,3 s a 1,3 s
  (en esta máquina; `rocola-consola --mide-repaso archivos...` lo mide). Las medidas salen iguales (sonoridad, picos, P90, LRA,
  corte, huella del EQ a 0,001). **No medido en el A4.** La compilación de Buildroot con mpg123 se hace en GitHub; aquí no se pudo
  (el proxy no deja bajar de sources.buildroot.net).
- **Ctrl+F3 → Sonido** lleva ya todos los ajustes de sonido de XP que valen en Linux: graves y agudos del local, balance, estéreo
  estrecho solo si hace falta (S3), DSEE por género (I3) y la tarjeta «Calidad del sonido» (EQ automático o a mano, el bajo profundo
  automático o a mano, el realce de brillo). Falta `frecuencia_salida=`: la salida ALSA aún no la admite.

## Extra (2-11-2026, segunda parte): el cielo con el color de lo que suena, y las hojas Detalles y Colores

- **El cielo sigue a la canción, como en XP** (`rocola/Consola/Pantalla/ColorFondo.cs`, cuentas de `Form1.Fondo.cs`): el degradado
  toma un dejo del género y las dos nebulosas se separan, una con el género y otra con el tono de la portada (de la portada solo el
  tono: el brillo y la viveza los pone el género). Los colores llegan deslizándose. Sin nada sonando, en un video o en la atracción,
  el cielo sale exactamente como antes. El tono de cada portada se mide una vez, al decodificarla (32x32 puntos, como XP).
- **Ctrl+F3 → Detalles** lleva ahora las dos tarjetas de XP: «Al pedir canciones» (`turno=`, `marca_cola=`: el palomito verde en
  los discos pedidos, `cola_portadas=`: las portaditas de la cola con su «+N») y «El fondo y la barra» (`fondo_portada=`, `relevo=`:
  el cubito de abajo da media vuelta en 420 ms al cambiar de canción). Todos encendidos de fábrica, con las claves de XP.
- **Ctrl+F3 → Colores** (nueva hoja): `portada_manda=` (20/50/100), `cielo_azul=` (35/70/100) y `cielo_negro=` (58/42/30), los
  mismos botones y valores de XP. `cielo_aire=` no se hizo: las estrellas de Linux no llevan esa atmósfera.
- Las hojas **Imagen** y **Fondo** de XP siguen en gris: son interruptores de los efectos de la tarjeta de XP (nebulosas de cine,
  bandas de la aurora, ahorros de Performance...), que el cielo de Linux dibuja a su manera; se irán pasando cuando el cielo de
  Linux tenga esas piezas.
- Sin referencia de XP con música sonando (la hermana no toca): el color se comprobó con capturas de Linux, no contra XP.

## Extra (2-11-2026, tercera parte): las mejoras visuales elegidas (V1-V5, V7-V24, V28-V30)

El dueño eligió 26 de las 30 ideas de la presentación «Rocola OS: mejoras visuales» (todas menos V6, V25, V26 y V27). Están todas,
cada una con su interruptor en Ctrl+F3 (hojas **Imagen** y **Fondo**, ya activas): las de coste bajo encendidas de fábrica; V1, V7, V13,
V14, V15, V20 y V24 (coste medio) apagadas hasta medirlas en el A4. Código: `rocola/Motor/MejorasVisuales.cs` (interruptores, el
espectro y la forma de la canción) y `rocola/Consola/Pantalla/Mejoras.cs` (el dibujo).

- **Lo nuevo debajo:** el espectro de 16 bandas medido en la salida (FFT de 1024 puntos por bloque, al compás de las bocinas: V9, V11),
  la forma de cada canción (240 tramos, en segundo plano con mpg123: V10), portadas con mipmaps y copia grande (V4), la portada
  borrosa de 24×24 (V7, V13), un cuadrilátero con textura para la perspectiva (V15, V20), copiar un trozo de pantalla (V24) y unas
  chispas (V18, V23).
- **V29:** la rocola pinta el cielo con «ROCOLA» en cuanto tiene la pantalla, antes de leer los datos y la biblioteca, y S99rocola ya
  no escribe en tty1 ni deja el cursor (sus mensajes van a `/run/rocola-arranque.txt`). GRUB y el núcleo siguen como estaban: el
  instalador que escribe el menú del disco no está en el repositorio.
- **V30:** KMS ya usaba la resolución del monitor y la interfaz ya se escalaba con ella (no se estiraba). El interruptor queda
  encendido (así) y apagado pide 1024×768 si el monitor la ofrece.
- Comprobado con capturas fuera de pantalla (Mesa por software). **Nada medido en el A4:** ni fluidez ni coste real de cada una.
  El espectro y las barritas reales necesitan la salida de sonido: en las capturas no se ven moverse.

## Extra (3-11-2026): rendimiento del dibujo para la resolución máxima a 75 Hz (sin cambiar el aspecto)

Petición del dueño: que en Linux la rocola corra a la resolución máxima del monitor y a 75 Hz, buscando ahorros en todo el dibujo sin
cambiar el aspecto. Son optimizaciones que conservan aspecto y funciones, así que son automáticas y sin interruptor (regla del 15-9-2026).

- **Ritmo de la pantalla**: ya no se dibuja como mucho cada 33 ms ni se salta el cuadro «si nada cambió»; se dibuja uno por refresco
  del monitor, esperando el cambio de página de KMS (vsync). El hilo de los botones no espera 50 ms cuando hay animación.
- **75 Hz**: de los modos del monitor se toma su resolución preferida y, a esa resolución, la frecuencia más alta que ofrezca (sin
  modos entrelazados). Si el monitor no da 75 Hz, se queda con lo que dé. V30 (1024x768) sigue igual.
- **El cielo lento en una capa**: el degradado y las nebulosas se pintan en una textura propia (FBO) y en pantalla se pone de una
  vez, opaca. Cada cuadro se rehace solo una cuarta parte (una franja), y entera cuando cambian los colores o un interruptor. Las
  estrellas, la aurora, la onda, el horizonte y la luna se pintan encima en cada cuadro, como antes.
- **Menos órdenes de dibujo**: las letras y el punto de luz llevan un cuadrito blanco de 4x4 dentro de su textura, así los
  rectángulos lisos van en el mismo lote que el texto. En la pared bajó de 172 a 106 órdenes por cuadro.
- **Portadas sin parar la pantalla**: leer y descomprimir cada portada va en un hilo de fondo de prioridad baja, de una en una; la
  pantalla sube como mucho 4 por cuadro. Las páginas de al lado se adelantan (hasta 48 en cola). Mientras llega, se ve el cuadro
  de color con la inicial, como al arrancar.
- **Sin basura por cuadro**: se recuerdan los textos cortados con puntos (Ajusta), los renglones repartidos (Envuelve), los textos
  sin acentos del buscador, las letras de cada tecla y lo que va en la cola. Las cinco letras de la ficha rápida se hornean con la
  primera pared, no al aparecer la ficha (eran unos MB de golpe). Ninguna escena del banco hace ya recolecciones.
- **Herramientas de medida** (solo con variables de entorno): `ROCOLA_BANCO=N` (mediana, p95, peor, KB por cuadro, recolecciones,
  órdenes y vértices), `ROCOLA_PERFIL=1` (ms y KB por tramo del dibujo), `ROCOLA_RELOJ_FIJO=s` (capturas comparables),
  `ROCOLA_SIN_CAPA_CIELO`, `ROCOLA_SIN_LOTES` (para comparar con y sin los ahorros), `ROCOLA_PORTADAS_EN_FONDO` (capturas con el
  hilo de las portadas) y `ROCOLA_BANCO_SIN_FIN`.

Comprobado aquí con Mesa por software (llvmpipe, 1920x1080; **no es el A4**): con reloj fijo, la imagen con los ahorros y sin
ellos sale igual (diferencia máxima 1 de 255, salvo los contadores que avanzan solos, como las canciones por medir). En la pared la
mediana pasó de 33,7 a ~29 ms con llvmpipe; 0 recolecciones en todas las escenas (antes: buscador 66 KB y ajustes 94 KB por
cuadro). **No está medido en el A4**: falta comprobar allí que llega a 75 Hz a la resolución nativa y si conviene apagar alguna
mejora de coste medio.

## Extra (3-11-2026, segunda parte): menos relleno por cuadro (lo que más cuesta en la tarjeta del A4)

En la tarjeta del A4 (memoria compartida con el procesador) lo caro es cuántas veces se pinta cada punto de la pantalla. El banco
ahora lo cuenta («pantallas pintadas» por cuadro; con `ROCOLA_PERFIL=1`, también por tramo, y con `ROCOLA_PERFIL=gpu` esperando a la
tarjeta en cada tramo). Todo automático y sin interruptor (no cambia el aspecto), salvo el arreglo de V28:

- **Ajustes (Ctrl+F3)**: el velo oscuro y el filo azul se pintan solo donde se ven (antes, dos pantallas enteras debajo de la
  ventana opaca), y bajo la ventana opaca el cielo no se copia: solo sus lados (la franja que se rehace, un lado por turno; al cerrar
  se rehace entera). De 6,2 a 2,4 pantallas.
- **Arreglo de V28** («Ver el cambio mientras se elige»): en Colores, Imagen y Fondo el filo azul se pintaba entero y opaco debajo
  del fondo translúcido de la ventana, así que lo de detrás no se veía. Ahora es solo el filo y se ve la pared detrás, como dice la
  opción. **Esto sí cambia lo que se ve en esas tres hojas** (es lo que la opción prometía).
- **Portada de cine (V19)**: la portada y su velo oscuro, en una sola pasada con un sombreador propio (antes dos pantallas enteras
  mezcladas una tras otra). Comparado en el mismo cuadro (`ROCOLA_CAPTURA_DOBLE`): diferencia máxima 2 de 255, por redondeo. La
  atracción, de 5,3 a 4,0 pantallas.
- **Cielo**: los cuatro degradados de la noche son opacos y cubren todo: ya no se pinta el fondo liso debajo y se pintan sin mezclar
  (la tarjeta no lee lo de debajo). La pantalla no se borra al empezar el cuadro si lo primero es la capa del cielo, que la cubre
  entera (se borra solo si se dibuja otra cosa antes).
- **Lo invisible no se manda**: lo que se dibuja con alfa 0 (lo que se desvanece y ya no se ve) no llega a la tarjeta.
- **Presentación por KMS**: el framebuffer de DRM de cada buffer se hace una vez (antes, uno nuevo en cada cuadro), y el programa ya
  no espera el cambio de página en el mismo cuadro: lo pide y prepara el siguiente mientras la tarjeta acaba; espera, si hace
  falta, al presentar el siguiente. **No probado en KMS de verdad** (aquí no hay tarjeta y la imagen de QEMU no estaba a mano): si en
  el A4 diera problemas, `ROCOLA_FLIP_ESPERA=1` vuelve a esperar como antes.
- Herramientas: `ROCOLA_RELOJ_PASO=ms` (el reloj fijo avanza eso por cuadro) y `ROCOLA_CAPTURA_DOBLE=VARIABLE` (el mismo cuadro otra
  vez con esa variable, en `ruta.b.ppm`).

Con reloj fijo, todas las escenas salen iguales a la versión anterior (salvo contadores que avanzan solos y la fecha del programa
en el título de los ajustes). Pantallas pintadas por cuadro a 1920x1080: pared 3,2 → 3,0; disco 3,8 → 3,6; reposo 3,8 → 3,5;
atracción 5,3 → 4,0; ajustes 6,2 → 2,4. **Nada medido en el A4.**

## Extra (3-11-2026, tercera parte): halos sin esquinas y lo opaco sin mezclar

Automático y sin interruptor (no cambia el aspecto):

- **Halos (brillos) grandes como polígono de 16 lados**: el punto suave se apaga como (1 − r)^2,2, así que las esquinas del cuadrado
  y el anillo de fuera no cambian ningún punto (menos de medio escalón de 255, calculado con el alfa de cada halo). Los de más de 24
  puntos de radio se pintan con un polígono que cubre solo lo que se ve: un 25-40 % menos de relleno por halo.
- **Lo opaco sin mezclar**: cada lote sabe si todo lo suyo es opaco (portadas sin transparencia —se mira al leerlas, en el hilo de
  fondo— o colores lisos con alfa 1); si lo es, se dibuja sin mezclar (sale idéntico y la tarjeta no lee lo de debajo: la mitad de
  memoria por punto). Una forma opaca grande (más de 1/16 de pantalla, como el fondo de la ventana de los ajustes) va en su propio
  lote. El banco ahora dice cuánto de lo pintado va mezclado.
- **Ajustes**: bajo la ventana opaca, las estrellas, la aurora, la onda, el horizonte y la luna se pintan solo en los dos lados que
  se ven.

Comparado con reloj fijo contra la versión anterior en 11 escenas y en el mismo cuadro con y sin los ahorros (diferencia máxima 2
de 255). Por cuadro a 1920x1080 (pintadas / mezcladas): pared 3,0 / 1,5; disco 3,4 / 2,0; reposo 3,3 / 2,0; atracción 3,9 / 2,5;
ajustes 2,0 / 0,95 (antes casi todo mezclado). **Nada medido en el A4.**

## Extra (3-11-2026, cuarta parte): lo más pesado, el cielo y las texturas

Automático y sin interruptor (no cambia el aspecto):

- **Nebulosas por tramos**: la textura de la nebulosa es transparente del todo en un 35 % de sus celdas de 8x8 texeles (contando un
  texel alrededor, lo que lee el filtrado). Se apuntan una vez los tramos de celdas con algo y solo esos se dibujan, y solo los que
  caen en la franja del cielo que se rehace en ese cuadro. Lo saltado no pintaba nada (alfa 0).
- **Blanco con alfa en dos bytes**: los atlas de letras, el punto suave de los halos y la nebulosa se suben en luminancia + alfa
  (la tarjeta lee lo mismo: 1, 1, 1, alfa) con la mitad de memoria y de lectura que en RGBA.
- Medido también lo del procesador (`ROCOLA_BANCO_SIN_GL=1`): preparar un cuadro cuesta aquí de 0,1 a 0,6 ms; lo que queda es de la
  tarjeta.

Con reloj fijo, igual que la versión anterior (salvo los contadores que avanzan solos). Por cuadro a 1920x1080 (pintadas /
mezcladas): pared 2,8 / 1,3; disco 3,25 / 1,8; reposo 3,1 / 1,8; atracción 3,7 / 2,35; ajustes 2,0 / 0,94. De ahí, el cielo es
1 pantalla (poner la capa, opaca) más unas 0,45 (la franja que se rehace). **Nada medido en el A4.**
