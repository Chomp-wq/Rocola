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

## Extra (3-11-2026, quinta parte): vidrio y letra nueva (G1-G11 y L1)

Decisión del dueño sobre la presentación «Rocola OS: vidrio y letra»: todo, y la letra L1. Cada cosa con su interruptor en Ctrl+F3
(Imagen → «Vidrio y letra»).

- **L1, letra nueva**: Outfit en los títulos (lo que era Trebuchet, los números grandes, el título de lo que suena, de la ficha, del
  disco, del buscador y de los ajustes) y Plus Jakarta Sans en el resto. Libres (SIL OFL, sin nombre reservado), en grosores fijos
  sacados de sus versiones variables, en `sistema/board/rocola/rootfs_overlay/usr/share/rocola/letras` con sus licencias: ya no
  dependen del disco de XP. El espacio entre palabras es de al menos 0,27 del tamaño (el de fábrica es estrecho y a tamaños chicos
  las palabras se juntaban). Encendida de fábrica; se cambia en vivo. Si faltan los archivos, el interruptor sale en gris.
- **El vidrio** (`DibujoVidrio.cs`, `Vidrio.cs`): lo de detrás desenfocado de verdad, con un sombreador gaussiano de 9 toques (dos
  veces en horizontal y vertical) a 1/8 de la pantalla. Dos fuentes: el **cielo borroso** (la capa del cielo, una vez por cuadro y
  solo si se usó en el anterior: coste bajo) y **lo de detrás de verdad** (se copia el trozo de pantalla del panel con 32 puntos de
  margen: coste medio). Cada panel: sombra en aros, el fondo desenfocado con el velo y el tinte encima en **una sola pasada opaca**
  (sombreador propio), un reflejo inclinado que cruza cada 9 s, el filo y el canto de luz. Las teclas, botones y pastillas son vidrio
  sin fondo propio.
- **Dónde**: G1 cabecera, G2 barra de letras, G3 lo que suena (y la cola y los créditos), G5 hoja de canciones (con V7, vidrio de
  verdad), G6 reposo, G7 buscador (caja, placa del teclado con teclas de vidrio, placa de resultados), G8 atracción (placa a la
  medida del nombre y pastilla del mensaje; con la portada de cine, vidrio de verdad) — encendidos. G4 ficha rápida, G9 ajustes
  (dibuja la pared detrás y la desenfoca; no en Colores/Imagen/Fondo con V28), G10 avisos (sustituye a V24, que queda en gris),
  G11 globo de la ayuda — apagados hasta medirlos en el A4.
- **Coste** (llvmpipe, 1920x1080, pantallas pintadas por cuadro, de fábrica frente a todo apagado): pared 3,1 / 2,8; disco 3,6 /
  3,25; reposo 2,9 / 3,1 (menos: el vidrio reemplaza un panel ancho); buscador 3,0 / 2,6; atracción 4,1 / 3,7; ajustes 2,15 / 2,0.
  Con G9 encendido los ajustes suben a ~6 pantallas. **Nada medido en el A4.**

## Extra (3-11-2026, sexta parte): el vidrio y la letra nueva, más ligeros (sin cambiar el aspecto)

Automático y sin interruptor (no cambia ningún punto: comparado cuadro a cuadro, con reloj fijo, contra la versión anterior en pared,
disco, reposo, buscador, atracción, ajustes y ficha, con los vidrios de fábrica, con todos encendidos y con las letras de XP; las
únicas diferencias son las que cambian solas entre dos ejecuciones iguales, como el contador de canciones por medir).

- **Las letras se dibujan al usarlas** (`Letra`, `Dibujo.cs`): antes cada tamaño nuevo horneaba sus 192 letras de golpe (2,5 a
  15 ms aquí, varias veces eso en el A4), y la transición al reposo pide cinco tamaños por cuadro (las letras crecen con el panel):
  entrar al reposo daba cuadros de 20-30 ms y una docena de recolecciones. Ahora el tamaño nuevo solo se mide, y cada letra se dibuja
  con stb_truetype la primera vez que se escribe, en el mismo atlas, con la misma función y la misma escala (los mismos puntos). Si un
  atlas se llenara, se hornea entero como antes. `ROCOLA_LETRAS_HORNEADAS=1` vuelve a lo de antes para comparar. Cada archivo de
  letra se lee una vez (`Letras.Lee`).
- **Los tamaños de paso se sueltan** (`PodaLetras`): los de la transición se quedaban para siempre (decenas de atlas de 0,5 a 2 MB
  en la memoria de la tarjeta, que en el A4 es la del sistema). Cada 600 cuadros, si hay más de 24 de un tipo, se sueltan los que
  no se pidieron en ese tiempo; si vuelven a hacer falta, se hacen otra vez (ya sin coste).
- **El cielo borroso antes de pintar la pantalla** (`CieloXp.cs`): sus pasadas a las capas chicas iban después de poner el cielo, y
  la tarjeta dejaba la pantalla a medias para ir a ellas y volver; ahora van antes de pintar nada en la pantalla, leyendo la misma
  capa ya al día.
- **Las esquinas del vidrio sin tiras a todo lo ancho** (`RedondoVidrio`): cada renglón curvo era una tira de un punto de alto y
  todo el ancho del panel (en la cabecera y lo que suena, unas 50 tiras de 1900 puntos; la tarjeta pinta de dos en dos renglones y
  ahí trabaja el doble). Ahora el tramo recto de arriba y el de abajo son un rectángulo cada uno y solo lo curvo va por renglones.
  `ROCOLA_TIRAS_ENTERAS=1` (o `ROCOLA_SIN_LOTES`) vuelve a lo de antes.
- **Los ángulos de los contornos, una vez** (`Contorno`): el vidrio pone cinco contornos por panel (tres aros de sombra, el filo y
  el de las pastillas), y cada uno calculaba senos y cosenos en cada cuadro; se guardan por número de pasos, con la misma cuenta.
- **Medido aquí** (llvmpipe; solo procesador, mediana por cuadro a 1920x1080, versión anterior / nueva): pared 0,41 / 0,33 ms;
  buscador 0,36 / 0,29 ms; al entrar al reposo, el peor cuadro de 20-31 ms a 3,3-3,8 ms, y de 9-11 recolecciones a ninguna. En la
  tarjeta (`ROCOLA_PERFIL=gpu`): la cabecera de 2,45 a 1,85 ms y lo que suena de 2,8 a 2,4 ms. **Nada medido en el A4.**

## Extra (3-11-2026, séptima parte): el instalador y el actualizador, que no estaban; revisión del sistema instalado

Revisión pedida por el dueño: que el instalador ponga Rocola OS encima de la rocola de XP sin tocar la música ni lo que la rocola de XP
generó, y que todo esté en orden.

- **Faltaban `rocola-instalar` y `rocola-actualiza`.** Se escribieron el 1-11 en otra sesión, pero la regla `bin/` del `.gitignore`
  dejaba fuera `rootfs_overlay/usr/bin/` (ya había pasado con `rocola-prueba`): nunca llegaron al repositorio ni a las imágenes
  publicadas. Con la memoria de sistema-5, «Instalar» no hacía nada y la rocola instalada no se actualizaba. El `.gitignore` ya los
  incluye y los dos se escribieron de nuevo, a la medida de lo que ya esperaban el initramfs, GRUB, `S99rocola`, `/usr/bin/rocola` y
  Ctrl+F3 → Internet.
- **`rocola-instalar`**: busca el NTFS con `musica` (no la memoria), enseña disco, canciones, datos de XP y Windows XP; comprueba MBR,
  hueco para GRUB y sitio antes de preguntar; ENTER instala, otro botón cancela sin tocar nada. Guarda el arranque de XP (una vez),
  copia el sistema entero y lo compara byte a byte, crea lo aprendido (`datos.img`, ext4 con diario, hecho en `post-build.sh` con el
  mke2fs de la máquina que compila) con lo que la memoria ya tuviera, traduciendo `/rocola/discos/<particion>/` a `/rocola/disco/`, y
  la llave de GitHub de la carpeta de XP; pone GRUB (busca el disco por su UUID) y su menú oculto (ESC: la versión anterior, la prueba
  y Windows XP). Volver a instalar va a la otra copia y conserva lo aprendido. Si Windows no se apagó bien, no escribe. Al quitar la
  memoria se reinicia solo (el núcleo no trae SysRq: BusyBox y sus bibliotecas se copian antes a la RAM).
- **`rocola-desinstalar`**: devuelve el arranque de XP guardado (el código del primer sector y el hueco; no la tabla) y deja `\rocola-os`.
- **`rocola-actualiza`**: la llave (de una memoria conectada aunque sea después de prender, o de la carpeta de XP), la hora por NTP, el
  programa (a4 o genérico según AVX; suma y `--version`; el que no arranca queda como malo) y el sistema en la otra copia (sumas,
  escrito entero, `probando` y `arranque.cfg` al final). Dice qué pasa en Ctrl+F3 (llave que no vale, sin internet, sin sitio...).
- **Arreglos en lo que ya estaba:**
  - el initramfs busca el disco por el UUID que pone el instalador, mira los demás NTFS solo en lectura, monta con `noatime`, y si una
    copia del sistema no se puede montar arranca con la otra en vez de quedarse en una consola (en el bar no hay teclado);
  - elegir en el menú «la versión anterior del sistema» daba por buena la nueva que estaba en prueba: ahora la anterior queda como la
    de siempre y la nueva como mala;
  - «Buscar actualización ahora» borraba su aviso antes de que el actualizador lo viera (y con «Actualizarse sola» apagado no buscaba);
  - importar de XP: las rutas guardadas con otras mayúsculas que la carpeta real (`C:\MUSICA` frente a `musica`; Windows no las
    distingue y Linux sí) se perdían, con todas sus medidas; ahora se encuentran (`ImportaXP.cs`, `SinMayusculas`).
- **Probado en una máquina emulada** con el núcleo de sistema-5, un disco como el de XP (NTFS desde el sector 63, `ntldr`, música,
  `C:\MUSICA\...` en sus tablas) y una memoria: instala; la copia del arranque de XP es idéntica y la tabla de particiones no cambia; el
  disco prende solo en Rocola OS (GRUB lee el NTFS, initramfs, el sistema y lo aprendido montados); la vuelta atrás vuelve sola al
  anterior a la tercera; volver a instalar conserva lo aprendido; con ESC sale el menú (Rocola OS, la versión anterior, la prueba, Windows XP) y «Windows XP»
  pasa al arranque de la partición de XP (en la prueba no hay un Windows de verdad: su sector de arranque responde); `rocola-desinstalar` devuelve el arranque de XP tal cual; el
  actualizador contra un GitHub de mentira (llave que falta o no vale, descarga rota, programa que no arranca, apagado y pedido a mano).
  **Cortes de luz:** un ext4 dentro de un archivo NTFS **con huecos** se estropeó; escrito entero (como lo hacen el instalador y el
  actualizador) aguantó siete apagados de golpe, cuatro a media escritura, sin más que reproducir su diario. BusyBox y GRUB de la prueba
  son los de esta máquina (los de la imagen del A4 llevan instrucciones del Piledriver que QEMU no tiene). **Nada probado en el A4.**

## Extra (3-11-2026, octava parte): la red, la hora y el registro

- **Sin red en el A4.** El udev del sistema renombraba la tarjeta de red (`eth0` → `enp3s0`, por su ranura) y todo usaba `eth0`: el
  DHCP, el encendido remoto y Ctrl+F3 → Internet. La rocola se habría quedado sin internet y sin Wake on LAN. Ahora se queda como
  `eth0`: `net.ifnames=0` en los menús de arranque (la memoria y el instalado) y una regla vacía en `/etc/udev/rules.d` que sustituye a
  la que renombra (esta llega también con las actualizaciones del sistema, que no reescriben el menú instalado).
- **El DHCP se rendía.** El de Buildroot (`ifup`, `udhcpc -n`) probaba ~10 s al prender y no volvía a intentarlo: tras un apagón, con el
  router del bar aún prendiendo, la rocola quedaba sin red hasta reiniciar (y hacía esperar el arranque). Ahora `S41red` lo lleva en
  segundo plano y sin rendirse (cada 20 s), con el guion de Buildroot dado a mano. Probado en QEMU con el cable «conectado» a los 45 s:
  consigue dirección, ruta y DNS sola.
- **La hora.** No había zona horaria: la rocola contaba en UTC. El reloj de la placa del A4 está en hora local (lo deja así XP), y el
  núcleo lo leía como UTC: por casualidad salía la hora local, pero en cuanto el NTP de `rocola-actualiza` pusiera la buena, el
  reinicio de las 7, «lo más pedido esta noche» y las bitácoras se habrían corrido 6 horas; y `rocola-actualiza` escribía el reloj en
  UTC, con lo que Windows XP se habría quedado 6 horas adelantado. Ahora: zona `America/Mexico_City` (tzdata, sin cambio de horario
  desde 2022), `S02hora` lee el reloj como hora local al prender y lo escribe igual al apagar, el initramfs también (las horas de
  `sistema.txt`), y la hora por NTP se pone antes que nada, aunque falte la llave de GitHub. .NET (NativeAOT, globalización invariante)
  toma la zona de `/etc/localtime`: comprobado.
- **El registro de la rocola** (`rocola.txt`) iba dentro del sistema y se perdía en cada actualización del sistema: va con lo aprendido
  (`/rocola/datos/rocola.txt`); `/root/rocola.txt` lo sigue enseñando.

**Nada probado en el A4** (la Realtek del A4 y su reloj, en especial).

## Extra (3-11-2026, novena parte): la salida de sonido directa a la tarjeta del amplificador y los MP3 con basura

- **El remuestreo de ALSA.** La rocola abría `default`, que en una HDA es `plug → softvol → dmix` a 48 kHz, y alsa-lib (sin
  alsa-plugins) convierte con su remuestreo lineal: cada canción de 44,1 kHz (casi todas) se pasaba a 48 kHz con el peor conversor,
  después de todo lo que cuida la cadena. Medido con el mismo alsa-lib (un tono de 1 kHz y otro de 15 kHz a 44,1 kHz): el de 15 kHz
  baja 3,4 dB y aparece uno falso en 18,9 kHz a −15 dB, con restos a −27 dB entre 1 y 15 kHz. Ahora se abre la tarjeta directa
  (`hw`: la frecuencia de la canción tal cual, sin mezclador ni conversión; la música y los vídeos nunca suenan a la vez). Un archivo a
  una frecuencia que la tarjeta no admita (MP3 viejos de 22 o 32 kHz) se pasa a 48 kHz con el remuestreo de la propia rocola (el de los
  discos continuos, 192 toques), no con el lineal; si nada de eso abre, `plughw` y por último `default`, como antes.
- **La tarjeta del amplificador.** El A4 tiene dos HDA: la del HDMI de la Radeon y la analógica, que va al amplificador; `default` era
  la tarjeta 0, que puede ser la del HDMI (y el volumen se subía solo en esa). Ahora se elige la primera salida que no sea HDMI ni
  DisplayPort (`SalidaAlsa.Elige` en la rocola y `rocola-tarjeta` en el sistema, la misma regla, probadas con un `/proc/asound` como el
  del A4: HDMI en la 0 y la Realtek en la 1). La usan la música, la prueba de bocinas, los vídeos (mpv, `--audio-device`), la prueba de
  arranque (`speaker-test -D`) y el volumen al máximo de `/usr/bin/rocola` (`amixer -c`). `ROCOLA_ALSA` la fija a mano.
- **Lo que ya sonó**, para el reloj de la canción y el relevo, se le pregunta a la tarjeta (`snd_pcm_delay`) en vez de suponer el
  colchón pedido de 300 ms (la tarjeta puede dar otro).
- **MP3 con basura a media canción.** mpg123 buscaba el siguiente cuadro bueno solo en 1 KB y, si no lo hallaba, daba la canción por
  acabada: un MP3 de 60 s con 8 KB rotos a la mitad sonaba 30 s y se medía como de 30 s (ffmpeg lo toca entero). Sin límite
  (`MPG123_RESYNC_LIMIT = -1`) suena y se mide entero; los archivos sanos no cambian.

**No probado en el A4**: la tarjeta directa (`hw`) y la elección de la Realtek dependen de su placa; aquí se probó el camino por ALSA
hasta un archivo y la elección con tarjetas imitadas.

## Extra (3-11-2026, décima parte): la caja negra de los cortes, y la salida probada con una HDA emulada

- **Los cortes de sonido no quedaban apuntados.** En XP cada corte iba a `rocola_error.txt` («[Cortes de sonido]», la caja negra del
  22-10); en Rocola OS la salida ALSA los recuperaba y contaba, pero la rocola no los apuntaba en ningún sitio. Ahora, al cambiar de
  canción (en un disco continuo la salida sigue abierta) o al cerrarla, si hubo alguno: una línea con la canción, cuántos, la tarjeta,
  cómo iba el hilo del sonido y si el repaso de la biblioteca estaba en marcha (por la bitácora de fondo).
- **El registro dice qué se abrió**: «Sonido: abierta hw:0,0 a 44100 Hz» (o «a 48000 Hz (la canción es de 22050)») cada vez que cambia.
- **Probado en QEMU** con el núcleo de sistema-5, una HDA emulada (intel-hda + hda-output) grabada a un WAV y el programa de la rocola
  (la versión genérica): elige «Generic Analog» (`plughw:0,0`), abre la tarjeta directa (`hw:0,0`) a 44 100 Hz, el hilo del sonido va
  en tiempo real, y al congelar el programa 2 s la salida cuenta el corte y sigue. Esa tarjeta emulada admite 22 050 Hz; la del A4
  quizá no (entonces, 48 kHz con el remuestreo de la rocola). **No probado en el A4.**

## Extra (3-11-2026, undécima parte): la tarjeta ocupada al acabar un vídeo

Los vídeos (mpv) usan ahora la misma tarjeta, directa. Si al acabar un vídeo mpv aún no la ha soltado, la canción siguiente no podía
abrirla (EBUSY) y caía a `default`: la tarjeta 0, que en el A4 puede ser la del HDMI (la música habría salido por el monitor). Ahora,
ocupada, se reintenta cada 100 ms hasta 1,5 s (el registro lo dice: «'hw:0,0' estaba ocupada; se liberó a los 300 ms»), y si aun así
hay que caer, se cae a la mezcla de esa misma tarjeta (`default:N`) antes que a `default`. Probado en QEMU con la HDA emulada: una
segunda rocola que abre la tarjeta mientras otra suena espera 300 ms y la abre directa. **No probado en el A4.**

## Extra (3-11-2026, duodécima parte): el sonido medido en la tarjeta, y la tarjeta que llega tarde

- **Medido lo que sale por la tarjeta** (QEMU, HDA emulada grabada a un WAV a 44 100 Hz, tonos de 1 y 15 kHz por la cadena entera):
  el de 15 kHz sale entero (0,0 dB, antes −3,4 dB) y lo peor que no estaba en la canción queda a −71 dB (antes, con el remuestreo
  lineal, −15 dB).
- **El ahorro de energía de la HDA** (el «plop» al volver del silencio) viene apagado (`power_save=0`): no hace falta tocarlo.
- **La tarjeta que llega tarde.** La salida del chip arranca silenciada y la HDA se registra en segundo plano, a veces después de que
  udev acaba; si `/usr/bin/rocola` subía el volumen antes, la rocola sonaba muda todo el día. Ahora espera a la tarjeta (hasta 20 s) y
  vuelve a poner el volumen cada vez que se abre la rocola. Probado con un `/proc/asound` imitado en el que la tarjeta aparece a los 3 s.
  **No probado en el A4.**

## Extra (3-11-2026, decimotercera parte): la última etapa, en 24 bits y con silencio de verdad

La cadena trabaja en coma flotante y al final se pasaba a 16 bits con dither triangular (lo mismo que en XP, cuyo WaveOut no daba más).
Ahora que la salida va directa a la tarjeta:

- **24 bits si la tarjeta los admite** (`S32_LE`, el contenedor de la HDA en Linux; `Salida24en32`): el mismo redondeo con dither
  triangular, pero de 1/8 388 608. Medido con un tono a −60 dBFS: el ruido de la conversión pasa de −96,3 dBFS (16 bits) a −144,5 dBFS,
  por debajo del propio de la tarjeta. Solo con la tarjeta directa (`hw`); si no los admite (la HDA emulada de QEMU solo tiene 16 bits:
  probado, vuelve sola a 16 y suena), 16 como siempre. `ROCOLA_SALIDA_16=1` obliga a 16. El registro dice cuál: «…, 24 bits».
- **Silencio exacto donde la cadena da cero** (las dos salidas y el WAV): entre canciones, el cebado y los silencios saltados ya no
  llevan el soplido del dither (~−96 dBFS), que con el amplificador a tope era lo único que se oía en un silencio. Con música, igual.

**No probado en el A4** (la Realtek suele admitir 16, 20 y 24 bits).

## Extra (3-11-2026, decimocuarta parte): los MP3 de frecuencias mezcladas, también por mpg123

Los pocos MP3 que traen cuadros de otra frecuencia (pegados de dos grabaciones) se reconstruyen en memoria con solo los cuadros de la
frecuencia dominante, como en XP. Esa reconstrucción iba por NLayer, el descompresor que se come cuadros y que se había quitado para
todo lo demás. Ahora se escribe en `/tmp` (en la RAM), mpg123 la abre y se borra en el acto: Linux la deja leer mientras siga abierta,
y no queda nada aunque la rocola se caiga. Si mpg123 no puede, NLayer como antes.

Probado con un MP3 de 20 s a 44,1 kHz + 2 s a 48 kHz + 20 s a 44,1 kHz: con mpg123 suena y se mide entero (40,05 s: los dos tramos de
la dominante); con NLayer, sobre la misma reconstrucción, **se paraba a los 20 s** y la segunda mitad no sonaba. El descomprimir del
repaso baja de 628 a 52 ms. No quedó ningún temporal. **No probado en el A4.**

## Extra (3-11-2026, decimoquinta parte): los vídeos, al volumen de las canciones

En XP los vídeos los pone el reproductor de Windows y la rocola no puede abrir su sonido: nunca se miden, y como ese reproductor solo
sabe **bajar** (0 a 100), la perilla «Volumen de los videos» dejaba hueco (85) y un vídeo grabado flojo se quedaba flojo. En Rocola OS
los pone mpv, que sí puede subir, y el sistema trae ffmpeg. Lo que ya no hace falta y lo que se hace mejor:

- **El repaso mide el sonido de los vídeos** (`Motor/AudioDeVideo.cs`): ffmpeg saca solo la pista de sonido (sin descomprimir la
  imagen), en coma flotante a 48 kHz, por una tubería, y se mide como XP medía un vídeo (`MeasureFile`: cuánto suena). Van detrás de
  las canciones, con la prioridad baja del repaso (ffmpeg hereda el nice 19: comprobado) y en pausa mientras suena un vídeo. Se guarda
  como lo demás (`rocola_volumen.txt`) y no se vuelve a medir.
- **Cada vídeo medido suena al nivel de las canciones** (`VolumenVideo`, la cuenta de `VideoVolumeFor` de XP sin el tope de 100): con
  el nivelado encendido, al objetivo «Videos y canciones aún sin medir», de −16 a +12 dB. La perilla vale para los que aún no se
  midieron (o sin nivelado); su nota en Ctrl+F3 lo dice.
- **El volumen va exacto y con limitador**: antes se pasaba `--volume=85`, pero el volumen de mpv es cúbico (85 → −4,2 dB, no los
  −1,4 de XP: medido). Ahora la ganancia va en el limitador de ffmpeg dentro de mpv (`alimiter`, `level_in`), que para los picos en el
  techo de la cadena de la rocola (0,88, −1,1 dB) cuando un vídeo flojo se sube, con su retraso compensado (imagen y sonido juntos).

Probado aquí: dos vídeos de prueba que ffmpeg (EBU R128) da a 15,6 dB uno del otro, la rocola los mide a 15,6 dB; el recorrido entero
(biblioteca → repaso → guardado → ganancia) y sin volver a medir al arrancar otra vez; con mpv 0.37 la opción se acepta, la subida
es exacta (+12,04 dB), un clic sale en la misma muestra y los picos se quedan en 0,880; un vídeo sin sonido queda como mudo y suena
con la perilla. `BR2_PACKAGE_FFMPEG_FFMPEG=y` queda escrito en el defconfig. **No probado en el A4** (ni cuánto tarda allí medir un vídeo).

## Extra (3-11-2026, decimosexta parte): la IA del DSEE aprende en la propia rocola (IA1 e IA2)

Decisión del dueño sobre `IA_DATOS_EN_CRUDO_MODEST_2026-11-03.md` («IA1, IA2»).

- **IA2, cortar donde corta tu música** (`Motor/CortesIa.cs`): la cosecha simula además hasta cuatro cortes de los que de verdad
  tiene la biblioteca (los más comunes entre los MP3 dañados, a 250 Hz, con al menos 3 canciones, a 400 Hz de los fijos). La cuenta de
  cada ejemplo no cambia (`MakePair`, extractor 2): el entrenador de Python y lo ya juntado siguen valiendo. Probado: con 3 MP3 cortados
  a 14 kHz, la siguiente canción sana se cosecha también a 14 kHz (6 cortes; unos 20 % más ejemplos); 17,5 kHz, con solo 2, no entra.
- **IA1, que la rocola entrene sola** (`Motor/EntrenaIa.cs`, `Motor/IaNueva.cs`): el entrenador de `ia/entrenar_ia.py` pasado a C#,
  con sus reglas (índice con CRC, reparto por grupo 80/10/10 con la misma semilla, pesos por grupo y canción, Adam, la validación elige
  y la prueba decide contra la curva fija y la red puesta, en bruto y como suena). Entrena en reposo (nada sonando, cola vacía, 15 min
  sin botones), como mucho cada 20 h y con material nuevo, a nice 19, y se corta en cuanto alguien usa la rocola. **Nunca se pone
  sola**: queda como `IAS/rocola_ia.candidata.bin`; en Ctrl+F3, «Comparar a ciegas: la IA de ahora o la nueva» (Lo que suena ahora) y,
  si gana, «Quedarse con la IA nueva» (Mejoras con IA; la de antes queda como `.anterior-N`). También «Entrenar la IA ahora» y
  «Descartar la IA nueva»; `rocola-consola --entrena-ia` para medir cuánto tarda. **Con el modo billar encendido el DSEE no suena**
  (regla del modo), así que la comparación pide apagarlo.

Probado aquí con 33 185 ejemplos cosechados por la rocola de 12 piezas sintéticas: el mismo reparto que Python (15 870 / 8 660 / 8 655,
6/3/3 grupos), el mismo error de la curva fija (0,4192) y de la red puesta (1,8759), y la red nueva 0,0935 (Python, 0,0960); Python
carga la candidata; 17 s (24 s con el programa nativo, nice 19); un botón lo corta al momento y se apunta como cortado; la red nueva,
puesta en la cadena, rehace los agudos de una pieza que no vio a 3-4 dB del original (la puesta, a 8-26 dB), pero es del mismo tipo de
sonido sintético. Se aparta de Python en: muestra de hasta 400 000 ejemplos, azar de .NET y sin el informe por clases de señal.
**No probado en el A4**: falta medir cuánto tarda allí con el material de verdad y oír la comparación.

## Extra (3-11-2026, decimoséptima parte): el DSEE en el billar, solo en las muy cortadas, y medido otra vez

Decisión del dueño («1 y 2»): volver a medir el DSEE en el billar y dejarlo encender solo donde puede servir.

- **«DSEE en el billar, solo en las muy cortadas»** (`dsee_billar_cortes=`, APAGADO de fábrica, Ctrl+F3 -> Mejoras con IA;
  `DseePara` en `Motor/CadenaBillar.cs`): con el modo billar, el DSEE trabaja solo en las canciones con el corte medido por debajo de
  13 kHz. En las demás sigue apagado, y con el modo apagado todo es como antes. Necesita el DSEE guardado en SÍ; si no, va en gris
  diciendo por qué. La comparación a ciegas de la IA nueva ya vale en el billar con esas canciones.
- **La medida**: una pieza sintética tipo pop (que la IA nueva no vio) pasada por MP3 de verdad (LAME 192 kbps, `-cutoff` 12 y
  15 kHz), por la cadena del billar, contra la pieza completa por la misma cadena (tercios de octava, dB, referidos a 1 kHz):

| | 6,3 kHz | 8 kHz | 10 kHz | 12,5 kHz | 16 kHz | 20 kHz |
|---|---|---|---|---|---|---|
| Cortada a 11,9 kHz, sin DSEE | −1,5 | −1,3 | −0,3 | −7,5 | −70 | −72 |
| con el DSEE y la IA de ahora | −1,2 | −1,0 | −0,2 | −5,7 | −18 | −30 |
| con el DSEE y una IA entrenada por la rocola | +0,4 | +0,5 | +0,5 | −4,0 | −6,6 | −9,2 |

  - Con un MP3 de verdad cortado a 12 kHz, el DSEE devuelve bastante más que lo medido el 27-10 (+0,2 a +0,4 dB, con un corte
    ideal): la IA de ahora sube el tercio de 12,5 kHz 1,8 dB y rellena 16 kHz, que estaba vacío; la entrenada aquí se queda a 4-9 dB
    del original arriba de 12 kHz.
  - **Pero la entrenada aquí sube también de 6 a 10 kHz (+1,7 dB), por debajo del corte**: lo más probable es que el detector del
    corte lo vea más abajo en algunos cuadros y la red nueva rellene fuerte desde ahí. Pasó las reglas de números igual: es justo lo
    que la comparación a ciegas tiene que juzgar, y está entrenada con piezas sintéticas del mismo estilo (optimista).
  - La cortada a 15,4 kHz no se toca con el interruptor (sale como sin DSEE); la completa tampoco.
  - Coste: de 14 a 18 ms más por segundo de música en esta PC, **solo en las canciones de corte bajo**.
- Si tendrá sentido en el bar depende de cuántas canciones de corte bajo hay y de cuánto dan las cornetas por encima de 12 kHz:
  **no probado en el A4 ni oído en el billar**.

## Extra (3-11-2026, decimoctava parte): revisión de la cadena del billar con señales difíciles

Petición del dueño: buscar mejoras en toda la cadena del modo billar para aprovechar al máximo sus bocinas.

**Arreglado: las eses se aflojaban con el DSEE** (`Motor/Eses.cs`, `Cadena.cs`). En el billar, «Suavizar las eses» compara la «s» con
lo que suena por encima de 1 kHz. Con el DSEE en el billar (las muy cortadas), lo recreado arriba del corte entraba en esa referencia
como si fuera voz y la «s» se suavizaba menos: de 5 a 9 kHz salía hasta 1,9 dB más fuerte. Ahora, **solo en las canciones en que el
DSEE trabaja con el modo billar**, la referencia llega hasta el corte medido de la canción. Medido: esa subida desaparece (todo baja de
1 a 2 dB parejo, por el limitador con lo añadido arriba). En las demás canciones sale igual que antes (mismo resultado que el programa
anterior, salvo los 2 pasos del ruido de redondeo que cambian en cada pasada). Se probó también poner el tope en 11 kHz para todas:
una canción completa se suavizaba hasta 3,8 dB más, y eso sería otro cambio de sonido, así que no se hizo.

**Revisado sin fallos**, modo billar y de fábrica, con siete señales sintéticas (pop completo, master aplastado, grabación vieja mono y
bajita con siseo, silencios al principio y a la mitad, todo cargado a un lado, grave con golpes y un disco continuo en tres MP3):
- ningún NaN; el pico real (medidor EBU de ffmpeg) se queda en el techo, −1,0 a −1,1 dBTP, también en el master aplastado;
- el disco continuo, dentro de la biblioteca, pasa de pista a pista sin hueco ni bajón (±1 dB por bloques de 100 ms) y dura lo mismo
  que la pieza entera (el relleno de los MP3 se quita bien). Fuera de la biblioteca hay fundido, como debe: no se sabe que es un disco;
- lo grabado casi todo a un lado (26 dB) sale con 4,6 dB entre canales: es el estéreo estrecho de fábrica (ancho 0,6), como estaba;
- la cadena del billar tarda un 25 % más que la de fábrica en esta PC (1,6 s frente a 1,3 s para 40 s de música, con la lectura).
**No probado en el A4 ni oído en el billar.**

## Extra (3-11-2026, decimonovena parte): el material de la IA ya no puede llenar lo aprendido, y la IA aprende también de lo de XP

- **El fallo:** lo aprendido de Rocola OS vive en `datos.img`, de 512 MB, y ahí iba también `rocola_ia_datos.bin`, que la cosecha deja
  crecer hasta 24 GB. Con unas 300 canciones sanas repasadas a fondo (≈1,5 MB cada una; más con IA2) se llenaba, y desde entonces la
  rocola ya no podía guardar nada: ni la configuración, ni las tablas del volumen, ni el EQ, ni las IA.
- **Ahora** (`Motor/MaterialIa.cs`, `/usr/bin/rocola`): instalada, el material va al disco de la rocola, junto a la música
  (`/rocola/disco/rocola-os/ia`, `ROCOLA_IA_DATOS`); desde la memoria USB sigue en la carpeta de datos. Lo que ya hubiera se muda solo al
  arrancar, entero (copia completa, nunca dispersa) y una vez. Y la cosecha se para si donde escribe quedan menos de 256 MB libres.
- **La IA aprende también de lo de XP:** Rocola OS importa de XP la marca de «repasada a fondo» y no vuelve a repasar esas canciones,
  así que en la biblioteca de siempre apenas juntaría material. El entrenador (IA1) usa ahora también el `rocola_ia_datos.bin` y `.idx`
  de la carpeta de datos de XP (mismo formato y extractor), **solo leyéndolos**. Una misma canción cosechada en los dos lados se
  reconoce por su huella y queda en el mismo grupo: nunca a la vez en entrenamiento y en prueba.
- Probado: el material (5,3 MB) se mudó entero y la carpeta de datos quedó sin él; con el de XP de prueba, el informe dice los dos
  materiales, 7 grupos de entrenamiento en vez de 6, y el error en la prueba baja de 0,0935 a 0,0878; el de XP no se tocó. `DriveInfo`
  mide bien el espacio en una carpeta cualquiera de Linux. **No probado en el A4** (ni con el material de XP de verdad).

## Extra (4-11-2026): saltar sin chasquido

Al pulsar SALTAR, la salida se cerraba con `snd_pcm_drop`: lo que la tarjeta tenía por sonar (hasta 300 ms) se cortaba de golpe a mitad
de onda, y a volumen de bar eso es un golpe seco en las bocinas. En XP no se pudo arreglar (A1: habría que tocar el volumen del
WaveOut). En Rocola OS la salida es de la rocola (`SalidaAlsa.Apaga`, `ApagaSuave`): lo que ya está en la tarjeta suena tal cual, lo que
sigue baja a cero en 60 ms (media curva de coseno) y la salida acaba sola (drain). El salto llega unos 0,35 s después del botón (en XP,
0,75 s). Mientras baja no se prepara ni se releva nada. No toca ningún volumen del sistema: es la música la que baja. Automático.

Lo mismo **al apagar la rocola** (el botón de encendido o la orden de apagar) y **al cortar una prueba de bocinas o de bandas** porque
entra la música (`CierraSuave` en el Controlador: espera como mucho medio segundo a que baje y acabe; si no, se cierra como antes).
**Los vídeos también** (`Controlador.ApagaVideo`): mpv lleva desde el principio un volumen `@fin` que no hace nada; al saltar, por su
canal de órdenes (`--input-ipc-server`), se le pregunta por dónde va el sonido y se le pide que baje de 1 a 0 en 0,3 s; a los 0,4 s se
cierra. Si el canal no responde, se cierra igual a los 0,4 s. Probado contra mpv 0.37 con el mismo código: el sonido baja parejo hasta
cero y queda en silencio antes de cerrar. **Arreglado de paso:** al saltar un vídeo salía el aviso «No se pudo poner el video» (el
cierre a propósito se tomaba por un fallo).

Probado con una tarjeta ALSA que graba a archivo y un tono de 1 kHz a 0,8: baja de 0,80 a 0,00 en 60 ms, la última muestra es 0 y ningún
paso entre muestras es mayor que el del propio tono (antes podía quedar un escalón de hasta 0,8). **No probado en el A4.**

## Extra (4-11-2026, segunda parte): sin tarjeta de sonido, la cola espera

- **El fallo:** si la tarjeta no abría (desconectada, ocupada, o el controlador aún sin cargar) o dejaba de responder a media canción,
  la rocola daba la canción por terminada y pasaba a la siguiente: en segundos se comía toda la cola pagada sin que sonara nada.
- **Ahora** (`Consola/Controlador.cs`, `Motor/Cola.cs: AcaboPorFallo`, `SalidaAlsa.Fallo`): la canción vuelve al principio de la cola
  (los vídeos no, porque los pone mpv) y se vuelve a probar a los 2, 4, 8, 16 y después cada 30 s. Sale el aviso «Sin sonido (...). La cola
  se conserva: vuelvo a probar en N s», y en `rocola_error.txt` se apuntan el primer intento y uno de cada 20 («[Sin sonido (intento N)]»).
  Si la tarjeta deja de responder a media canción (ALSA no se recupera), esa canción se vuelve a poner entera. Cuando vuelve a abrir,
  todo sigue como siempre. Saltar a mano no cuenta como fallo. Automático.

Probado con una tarjeta que no existe (`--dispositivo hw:99,0`), una moneda y tres canciones: las tres siguen en la cola, el crédito
también, y el intento queda apuntado. **No probado en el A4.**

## Extra (4-11-2026, tercera parte): volver de un corte sin chasquido

- **Antes:** si el equipo se atascaba más que el colchón de la tarjeta (300 ms), la tarjeta se quedaba sin música, ALSA la recuperaba
  y la canción seguía a media onda: además del hueco, un chasquido al volver.
- **Ahora** (`SalidaAlsa.EntraSuave`): lo que sigue tras el corte entra en 10 ms (media curva de coseno), en las muestras ya convertidas
  (16 o 24 bits), también si el corte cae a mitad de un bloque. Sin cortes no se toca nada (bit a bit). El hueco en sí no se puede
  evitar (ya sonó); la caja negra lo sigue apuntando. Automático.

Probado el cálculo con un tono de 1 kHz a 0,8 en 16 y 24 bits: tras el corte sale 0, a los 5 ms la mitad y a los 10 ms entero, sin
saltos entre bloques. Un corte de verdad no se pudo provocar aquí (la tarjeta de prueba graba a archivo y nunca se queda sin
música). **No probado en el A4.**

## Extra (4-11-2026, cuarta parte): acabar sin chasquido aunque la canción se corte de golpe

- **Antes:** una canción que se acababa por sorpresa (un MP3 dañado o a medias, una lectura que falla, el disco que se quita) o con
  «sin fundido» dejaba la tarjeta en silencio de golpe, a media onda: un chasquido. Las canciones que el lector sabe cuánto duran ya
  acababan con el fundido de la cadena (también los MP3 truncados que mpg123 mide bien).
- **Ahora** (`SalidaAlsa.AcabaSuave`): si lo último que sonó no es casi silencio (sobre −66 dBFS), se añade una cola de 10 ms que lleva
  ese valor a cero (media curva de coseno). Las que acaban en silencio, como siempre (bit a bit). Al saltar ya baja en 60 ms y no se
  añade nada. Automático.

Probado con un tono de 440 Hz en MP3 cortado a la mitad y `fundido=0`: acaba con la cola y ningún paso entre muestras es mayor que el
del propio tono (0,110). Con el fundido de fábrica no se añade nada. **No probado en el A4.**

## Extra (4-11-2026, quinta parte): lo pagado que no suena devuelve su crédito (decisión del dueño)

- **Antes (como en XP):** una canción pagada que no se podía abrir (archivo dañado o que ya no está) se apuntaba y se saltaba; el
  crédito se perdía, y se podía volver a pedir y volver a perder.
- **Ahora** (`Cola.cs: DevuelveCredito`, `EmpiezaSiguiente`): se devuelve el crédito con el aviso «No se pudo tocar «…»: se devuelve el
  crédito», y la canción queda como dañada (no se puede volver a pedir mientras el archivo no cambie). Un vídeo cuyo mpv no arranca o
  falla en los primeros 8 s también devuelve el suyo (`Controlador.VideoFalloMs`); si falla después, ya se vio: solo el aviso. Lo que la
  tarjeta no deja sonar no pasa por aquí: vuelve a la cola (segunda parte). Automático.

Probado con una biblioteca de tres canciones, una de ellas ilegible: una moneda (4 créditos), las tres pedidas, suenan las dos buenas y
quedan 2 créditos (antes, 1); el fallo queda en `rocola_error.txt`. **No probado en el A4.**

## Extra (4-11-2026, sexta parte): los «MP3» que no lo son

- **El fallo:** un M4A, WMA, OGG o FLAC renombrado a `.mp3` (pasa en las bibliotecas de bar). mpg123 busca cuadros MPEG en cualquier
  cosa: en un M4A encontraba unos falsos y sonaba un golpe de ruido de 24 ms casi al máximo (0,83); los demás fallaban sin decir por qué.
- **Ahora** (`LectorCancion.OtroFormato`): antes de abrir un `.mp3` se mira su cabecera (después de la etiqueta ID3 si la hay). Un WAV
  renombrado suena como WAV; un M4A/AAC, WMA, OGG o FLAC no se toca: el aviso dice qué es («no es un MP3: es M4A/AAC con otro nombre»),
  se devuelve el crédito y queda en `rocola_error.txt` para convertirlo. Los MP3 de verdad, como siempre. Automático.

Probado con un tono de 4 s: M4A, WMA y OGG renombrados ya no suenan (antes el M4A daba el golpe); el WAV renombrado suena entero, y un
MP3 con etiqueta ID3 sale igual. **No probado en el A4.**

**Después, decisión del dueño («que se convierta automáticamente para la próxima, o si no tarda mucho que lo convierta en ese mismo
instante y reproduzca la canción»)** (`Motor/Conversiones.cs`): esos archivos se convierten con ffmpeg a un WAV de 16 bits con su
frecuencia y sus canales, en una carpeta aparte (`ROCOLA_CONVERTIDAS`: en el disco de la música, `/rocola/disco/rocola-os/convertidas`;
sin él, en la carpeta de datos). **El original no se toca.** Desde la copia suena y se mide como cualquier otra; las etiquetas, la
portada y lo aprendido siguen siendo del original, y si el original cambia se convierte otra vez.
- Se convierte cuando el repaso la encuentra o cuando alguien la pide, de una en una, con la prioridad más baja.
- Pedida: espera al principio de la cola hasta 20 s («Preparando «…» (no es un MP3: se convierte)...») y suena. Si tarda más, se
  devuelve el crédito y la conversión sigue: la próxima vez suena al momento.
- Sin ffmpeg, con menos de 1 GB libre o si ffmpeg no puede: se devuelve el crédito y queda como dañada, con el motivo.

Probado con un M4A de 4 s renombrado: pedido, se convierte y suena entero gastando un crédito; con un ffmpeg frenado 25 s, a los 20 s se
devuelve el crédito y la copia queda lista; pedido otra vez, suena al momento aunque no haya ffmpeg; sin ffmpeg, devuelve el crédito y
apunta por qué. El original sale igual byte a byte. Aquí convertir 4 min tarda 0,9 s; **en el A4 no está medido.**

## Extra (4-11-2026, séptima parte): los huecos de un MP3 dañado, sin chasquido

- **El fallo:** en una zona rota del archivo (unos KB dañados a media canción), el descompresor da los cuadros que no puede leer como
  silencio exacto: la canción caía de golpe a cero (medido: un salto de 0,65 de la escala, un chasquido fuerte en las bocinas), unos
  20 ms de silencio, y volvía de golpe.
- **Ahora** (`Lector.cs: HuecosMp3`, en el lector de los MP3, para sonar y para medir): solo cuando la música cae **en seco** a cero
  exacto desde más de −26 dBFS (en un MP3 sano el silencio digital llega bajando, nunca así), el hueco se rellena con la última muestra
  bajando a cero en 3 ms y lo que vuelve entra en 3 ms (si vuelve antes, se cruzan). Automático.

Probado con un tono de 20 s en MP3 con 8 KB de basura a la mitad: el peor salto entre muestras pasa de 0,649 a 0,065 (el del propio tono
es 0,056). El MP3 sano sale igual que con el programa anterior (la única diferencia, ±2 de 32768, es la del dither, la misma que hay
entre dos pasadas del mismo programa). **No probado en el A4.**

## Extra (4-11-2026, octava parte): lo pagado no se pierde al cerrar ni si se desconecta el disco de la música

- **Al cerrar sin el botón de encendido** (el vigilante que la vuelve a abrir tras un fallo, `salir`): la rocola daba por acabada la
  canción que sonaba y DESPUÉS guardaba el estado, así que esa canción, ya pagada, se perdía (las de la cola sí quedaban). Ahora se
  guarda antes de cerrar la salida y lo guardado ya no cambia hasta salir (`CongelaEstado`); al volver, la que sonaba empieza desde el
  principio, como tras un apagón. El botón de encendido sigue borrando el estado del día (mañana arranca limpia).
- **El disco de la música desconectado a media noche** (`Cola.cs`: `RaizConfiguradaAusente`, la misma comprobación que al arrancar):
  cada canción de la cola habría fallado una tras otra (con lo de hoy, cada una devolviendo su crédito y quedando como dañada). Ahora la
  cola espera con el aviso «Esperando el disco de música: la cola se conserva» (y una línea en `rocola_error.txt`) y sigue sola cuando
  vuelve. Si no vuelve en 10 minutos, como antes: cada una devuelve su crédito.

Probado con la tarjeta de prueba (que no va a tiempo real: una canción de 30 min «suena» en unos 36 s):
- Cerrar a media canción con dos en cola: antes (con el programa de ayer y con el de esta mañana) quedaban solo las dos de la cola;
  ahora quedan también la que sonaba y el crédito.
- Quitar la carpeta de la música mientras suena la primera: al acabar, la cola espera con las otras dos (sin devolver créditos ni
  marcarlas); al volver la carpeta, suenan las dos.

**No probado en el A4.**

## Extra (4-11-2026, novena parte): revisión de lo de hoy

Tres fallos encontrados al revisar lo hecho hoy, arreglados:
- **Un MP3 dentro de un WAV** (RIFF/WAVE con datos MPEG, formato 0x55; lo hacen algunos programas viejos) se tomaba por un WAV
  renombrado y ya no sonaba. Ahora solo se abre como WAV si lleva sonido sin comprimir (PCM, coma flotante o extensible); lo demás va a
  mpg123, como siempre (`LectorCancion.WavSinComprimir`). Probado: el MP3 dentro de un WAV, el WAV renombrado y el M4A renombrado, cada uno
  por su camino.
- **Un ffmpeg colgado** al convertir dejaba la conversión parada para siempre (leer lo que dice esperaba a que acabara, y el tope de
  tiempo no llegaba a mirarse); esa canción se quedaba en «se está convirtiendo»: 20 s de espera y crédito devuelto cada vez. Ahora lo
  que dice se lee aparte y a los 10 minutos se corta (con sus hijos); la canción queda como no convertible. Probado con un ffmpeg que no
  acaba nunca: se corta, se devuelve el crédito y no queda ningún proceso.
- **Si la tarjeta fallaba justo después del relevo** (la siguiente ya había entrado sin pausa), se volvía a poner la que ya había acabado
  y se perdía la que sonaba. Ahora vuelve a la cola la que de verdad sonaba (`CierraLaSalida`).

También revisado sin cambios: las lecturas de números de archivos (protegidas o solo de pruebas), los avisos del compilador que delatan
fallos (no hay) y los guiones del sistema con shellcheck (solo avisa de `read -s`, que BusyBox sí entiende).

## Extra (4-11-2026, décima parte): la hora que salta y los botones a medio guardar

- **La hora que salta** (`Creditos.cs: MiraSaltoDeHora`). Al prender, la hora viene del reloj de la placa y poco después la pone la red
  (NTP). Si el reloj de la placa iba mal (la pila gastada puede hacerlo arrancar en otro año), la hora saltaba de golpe y la rocola creía
  que había cambiado el día del negocio: a los 15 min sin botones borraba los créditos y la cola que quedaron de un apagón. Ahora, una vez
  por minuto, se compara lo que avanzó la hora con lo que avanzó el reloj del equipo (que no salta); si difieren más de 2 min, el día del
  negocio se rehace con la hora nueva (un salto no es «llegaron las 7») y «hace cuánto que nadie toca» se corre lo mismo. El salto se
  apunta en `rocola_error.txt`. **No probado** (aquí no se puede mover la hora del equipo); revisado y compilado.
- **Los botones a medio guardar** (`Botones.Configura`, `Controlador.CapturaAsistente`). Era el único archivo importante que se escribía de
  golpe (`File.WriteAllText`): un apagón justo al guardar la configuración de los botones podía dejarlo a medias, y la rocola arrancaba
  pidiendo configurar los botones, sin que los clientes pudieran usarla. Ahora se guarda como lo demás (archivo de paso, `fsync`,
  cambio de un tirón y copia `.bak`), y si el archivo está dañado se usa la copia. Probado: dañado con copia, arranca normal; dañado sin
  copia, pide configurarlos (como antes).

Revisado sin cambios: el guardado seguro de lo demás sí baja al disco en Linux (`FlushFileBuffers` es `fsync` en Rocola OS); el reloj del
equipo da la vuelta cada 24,9 días y todas las cuentas con él la aguantan; los índices de los menús y del buscador están protegidos.

## Extra (4-11-2026, undécima parte): lo crítico — que no se caiga, que arranque con lo aprendido y que los botones sigan

- **Un fallo al leer ya no tumba la rocola** (`SalidaAlsa.Bucle`). La cadena de cada canción atrapa sus errores (`Encadenado`), pero lo
  que va después en el mismo hilo (el pulso de la pantalla, el apagado suave, el convertidor a 48 kHz) no; un error sin atrapar en el
  hilo del sonido cierra el programa entero en .NET: sin música ni pantalla hasta que el vigilante lo volviera a abrir (perdiendo lo que
  sonaba). Ahora la canción se da por acabada (con su cola de 10 ms), se apunta en `rocola_error.txt` y sigue la cola. Los demás hilos
  de fondo (repaso, portadas, botones, conversión, IA) ya atrapaban los suyos: revisados uno por uno.
- **Lo aprendido se repara al arrancar** (`initramfs/init`, `post-build.sh`, `BR2_PACKAGE_E2FSPROGS`). Si `datos.img` no montaba (un
  apagón que lo dejó con errores más allá de lo que arregla el diario), la rocola arrancaba sin créditos, sin ajustes y sin los botones.
  Ahora el arranque lo repara con `e2fsck -p` (y `-fy` si con eso no basta), lo apunta en `sistema.txt` y vuelve a montarlo. Probado aquí
  el mismo par de pasos con un ext4 dañado a propósito: queda limpio. El arranque de verdad se prueba con la próxima imagen (GitHub).
- **Los botones, también fuera de lo aprendido** (`Arranque.cs: CopiaBotones`, `ROCOLA_COPIAS` en `/usr/bin/rocola`: el disco de la
  música, `rocola-os/copias`). Si se pierde `datos.img`, la rocola ya no arranca pidiendo configurar los botones (los clientes no podrían
  usarla): toma la copia y la devuelve a su sitio. La copia se hace al configurar los botones y, si falta, al arrancar. Probado: con los
  datos borrados y la copia, arranca normal y el archivo vuelve.

Revisado sin cambios: el registro `rocola.txt` escribe unas pocas líneas por canción (no llena el disco en años) y los respaldos guardan
solo los 3 días más recientes. **No probado en el A4.**

## Extra (4-11-2026, duodécima parte): lo grave en los gráficos y en el bucle de la rocola

Dibujar, poner la siguiente canción y atender los botones van en el MISMO bucle: si el dibujo se traba, la canción que suena acaba (va
en su propio hilo) pero ya no entra otra ni responden los botones.
- **El cambio de cuadro, con límite** (`Kms.EsperaCambio`). Se esperaba el aviso de la tarjeta (`read` del DRM) sin tope: si no llegaba
  (la tarjeta colgada, el monitor o la tele que se desconectan y el controlador que deja de avisar), la rocola quedaba muerta. Ahora se
  espera con `poll` hasta 500 ms y, si no llega, se sigue (se apunta una vez en el registro).
- **El vigilante del bucle** (`Controlador.Vigila`). Si el bucle no da una vuelta en 60 s por lo que sea (la tarjeta colgada dentro de
  su controlador, un disco que no responde), se apunta en `rocola_error.txt` y el programa se cierra a propósito: `/usr/bin/rocola` lo
  abre otra vez a los 2 s con los créditos y la cola guardados. Probado con el bucle parado a propósito (orden `atasca N` del guion de
  pruebas, solo con `--guion`) y el límite en 2 s: se cerró, lo apuntó y el estado quedó guardado; con la rocola normal 15 s y el límite
  en 1 s, nunca saltó.
- **Sin monitor al prender** (`Kms`). Si el controlador no veía ninguna pantalla conectada (el monitor o la tele apagados, o en otra
  entrada), la rocola se quedaba en modo texto toda la noche aunque después prendieran el monitor. Ahora, como XP, saca imagen igual: la
  primera salida que exista, con su modo si lo dio o 1024x768 a 60 Hz (VESA); el registro dice «salida a ciegas». **No probado** (aquí no
  hay tarjeta de verdad).
- **Las portadas gigantes** (`Dibujo.Decodifica`). Una foto de cámara de 100 Mpx ocupa 400 MB descomprimida; en el A4 (3,5 GB) podía
  acabar con la memoria y Linux mataría a la rocola. Ahora se lee primero la cabecera: más de 64 Mpx (o un archivo de más de 64 MB), sin
  portada. Probado fuera de pantalla con una portada de 72 Mpx: dibuja normal, 183 MB de memoria en total.
- **Un vídeo que se traba** (`Controlador.MiraAvanceVideo`). La rocola esperaba a que mpv acabara; un mpv colgado la dejaba en ese vídeo
  para siempre. Ahora cada 10 s se le pregunta por dónde va (`time-pos`, en un hilo aparte); si en 60 s no avanzó o no contesta, se cierra
  como un salto, se apunta y sigue la cola. Probado con mpv que `time-pos` avanza reproduciendo (1,96 → 5,00) y se queda quieto trabado.

Revisado sin cambios: si un sombreador no compila en la Radeon, el vidrio se apaga solo y, si fallan los básicos, la rocola sigue en modo
texto (no se cae); las texturas tienen tope (96 portadas, 6 grandes, 8 borrosas). **Nada de esto probado en el A4.**

## Extra (6-11-2026): Spotify dentro de la rocola (reuniones familiares)

Decisión del dueño: «integrar toda la biblioteca de Spotify [...] se podrá apagar con un interruptor, solo lo usaré para reuniones
familiares». Se le explicó que la API gratuita (Web API) no da el sonido y que lo oficial para tocarlo es un reproductor dentro de un
navegador; eligió librespot (no oficial). Una primera versión dejaba a la rocola como bocina de Spotify Connect (manejada desde el
celular): el dueño la rechazó («quería una integración nativa en la rocola»). Y pidió que cueste créditos («a mi familia le gusta el tema
de rocolas clásicas como Rowe AMI y quitar la parte física le quitaría chiste»).

- **El programa** (`sistema/rocola-spotify`, Rust, paquete `package/rocola-spotify`): con las piezas de librespot 0.8.0 (como ncspot).
  Entra con la llave guardada (`/rocola/datos/spotify/credentials.json`); sin ella, se anuncia en la red como «Rocola» y espera a que
  alguien lo elija una vez desde la app (Spotify Connect, zeroconf; necesita IPv6 en el núcleo). Toca con su `Player` (320 kbps,
  normalización básica de Spotify, sin huecos no) y saca el sonido por stdout en float. Se maneja por un enchufe Unix
  (`/run/rocola-spotify/control.sock`, una orden JSON por línea): `estado`, `toca` (uri), `para`, `api` (una ruta de la API web, con la
  llave de la sesión: solo permisos de lectura) y `baja` (una portada a `/run`). Su `Cargo.lock` es el de librespot 0.8.0 (las mismas
  versiones que el Rust de Buildroot ya compiló) y Buildroot lo compila con red y `--locked` (un paquete local no pasa por la descarga).
- **La rocola** (`Motor/Spotify.cs`): `SpotifyEnVivo` abre el programa (con el interruptor), lee su sonido a un anillo de 1,5 s y le
  pregunta cómo va cada 400 ms (tres conexiones: estado, tocar/parar al momento, y la API, que puede tardar). La letra **SPOTIFY**
  (`LetraSpotify`) lleva «Tus favoritas» (`/v1/me/tracks`), las listas (`/v1/me/playlists`) y los discos guardados (`/v1/me/albums`), con
  las canciones cargadas por adelantado en segundo plano (hasta 500 por lista; `/v1/playlists/ID/tracks`, y si no, `/items`). El buscador
  pide `/v1/search` (canción, disco y lista, 10 de cada) cuando se deja de escribir 0,6 s y lo añade detrás de lo de la biblioteca.
  Cada canción es una `Track` con ruta `/spotify/ID/Artista - Título.spotify`.
- **La cola:** `PideSpotify` cobra un crédito y da el turno como `Pide`; `EmpiezaSiguiente` pide la canción al programa y la arma con
  `ArmaCadena` (sin aprender nada: ni medidas, ni EQ, ni volumen parejo; sin DSEE ni relevo). `FuenteSpotify` lee del anillo sin
  esperar nunca y se acaba cuando el programa dice que acabó y ya no queda nada; sin sonido en 20 s, no se pudo (se devuelve el
  crédito). Al acabar o saltar, `para` y lo que quede se tira. En el estado no se guardan: lo pagado que no sonó vuelve como créditos.
- **Enganches en lo de XP:** `GananciaDe` (al nivel de las no medidas), `DseePara` (nunca), `SaveState`, `PreparaSiguiente`.
- **Probado** con un programa simulado que habla el mismo enchufe (biblioteca, búsqueda, portadas y tonos como canciones): la letra
  SPOTIFY con listas y discos (captura), dentro de un disco, el buscador con resultados de Spotify y portadas (captura), las canciones
  pedidas con créditos (sin crédito no suena; una moneda, dos canciones, quedan 2), sonando por la cadena una detrás de otra y nada en
  lo aprendido. El programa real compila aquí y en Buildroot, y en QEMU se abre y espera la cuenta. **No probado con una cuenta de
  Spotify de verdad ni en el A4.**

## Extra (6-11-2026, segunda parte): lo que Spotify le añade a la rocola (SP1-SP6, SP8, SP9, SP11)

Decisión del dueño sobre `SPOTIFY_PROPUESTAS_MODEST_2026-11-06.md`: «todo menos SP7, SP10, SP12», y nada que haga sonar música sin
créditos. Las letras, «con el estilo de Apple Music»; la versión de Spotify de sus canciones dañadas, «que aparentemente luzca igual».
Todo en `Motor/SpotifyMas.cs` (y `Consola/Pantalla/LetrasVista.cs`), solo con el modo Spotify encendido.

- **SP4, las parejas:** un hilo de fondo busca cada canción de la biblioteca (`track:título artist:artista`, 5 resultados) y acepta la
  que tiene el mismo título y artista normalizados (sin acentos, sin lo de entre paréntesis, sin «remaster», «en vivo», «feat.») y la
  duración a 2 s o menos; sin título, artista o duración (aún sin medir) no se arriesga. Una cada 2,5 s y otra vuelta cada 2 minutos;
  las sin pareja se vuelven a mirar a los 30 días. En `rocola_spotify_pares.txt` (id, duración, disco, artista, portada, año, géneros).
- **SP2, la sustitución invisible:** en `EmpiezaSiguiente`, una tuya con el corte de agudos medido por debajo de 16 kHz y con pareja se
  arma con `EmpiezaSpotify(item, id, pista, true)`: lo que se ve (`nowPlaying`) es tu canción; la cadena se arma con su pista de Spotify
  (su nivelado y lo que se aprende son los de esa versión). Sin relevo (`PreparaSiguiente` no la prepara). Si en 6 s no llega sonido,
  vuelve al principio de la cola y suena tu MP3 (`sinSpotify`), sin aviso ni devolución.
- **SP3:** lo de Spotify ya no se arma «sin aprender»: se mide como lo tuyo bajo su ruta `/spotify/ID/...` y `GananciaSpotify` usa esa
  medida (al límite) cuando la hay; la primera vez, el nivel de las no medidas.
- **SP1, las letras:** `rocola-spotify` gana la orden `letra` (`spclient.get_lyrics`: la letra de Spotify, por líneas con su tiempo, y
  sus colores); la rocola la pide al empezar la canción (la de Spotify, la sustituida o la tuya con pareja) y la tiene solo en la memoria.
  `LetrasVista.cs` la dibuja sobre el cielo del estado extendido: Outfit en negrita, a la izquierda; la línea que se canta encendida y las
  demás tenues (cada una cambia de brillo poco a poco), deslizándose con un resorte amortiguado hasta dejar la que se canta a un tercio
  del alto; los tres puntos que respiran en los tramos sin voz; una franja del color que Spotify da a esa letra; y el crédito del
  proveedor. El momento es lo leído menos lo que la tarjeta aún tiene por sonar (`SalidaAlsa.PorDelante`).
- **SP5:** la portada del disco de Spotify para tus discos sin carátula (en `portadas_spotify/`, en los datos) y el género del artista
  para tus canciones sin género (mueve el color del género y lo más sonado por género).
- **SP6:** al entrar en un disco tuyo, el disco de Spotify de sus parejas (la mayoría; con dos, o con una en discos de una o dos
  canciones) y las que no tienes, al final con «· Spotify».
- **SP8 y SP9:** «Top 50 México» y «Viral 50 México» (solo las de Spotify) en la letra SPOTIFY; «Tus más escuchadas» y «Escuchado hace
  poco» como atajos del buscador.
- **SP11:** la foto del artista (la más chica de más de 150 px: estirada se ve difuminada) de fondo en el panel, y el año del disco.
- **Probado** con el `rocola-spotify` simulado y una canción de prueba con los agudos cortados a 11 kHz: el repaso midió su corte
  (11 300 Hz), el emparejador encontró su pareja (aunque en Spotify se llamaba «(Remasterizado)»), y al pedirla sonó desde Spotify con tu
  nombre, tu disco y tu crédito; la portada, el año y el género vinieron de Spotify; al entrar en el disco apareció la que le faltaba y
  sonó con su crédito; y las letras (una letra inventada para la prueba) en capturas, con la línea encendida y los puntos. **No probado
  con una cuenta real ni en el A4** (en particular, si Spotify deja ver las letras, los éxitos y los géneros a este programa).

## Extra (6-11-2026, tercera parte): revisión de todo lo de Spotify

Pedido del dueño: «revisa todo lo de Spotify en busca de fallos, errores y mejoras». Lo encontrado y arreglado:

- **Saltar podía callar la siguiente.** La orden de parar iba en segundo plano y podía llegar al programa de Spotify después de la de
  tocar la siguiente: esa no sonaba y, a los 20 s, se devolvía su crédito. Ahora se para al momento (`TerminaSpotify`).
- **La misma canción dos veces seguidas podía acabarse al empezar.** La rocola veía aún el «acabó» de la vez anterior. Ahora `toca`
  devuelve cuántas canciones han acabado hasta ese momento, y solo cuenta un «acabó» posterior (`FuenteSpotify`, `seq`).
- **Una canción de Spotify no disponible** esperaba 20 s en silencio; ahora se acaba en cuanto Spotify lo dice (y se devuelve el crédito,
  o suena tu MP3 si era una sustituida).
- **Si se caía la red a media canción,** la rocola se quedaba en silencio sin fin con la cola esperando. Ahora, 15 s sin nada y se acaba;
  si no había llegado a la mitad, como si no hubiera sonado (crédito, o tu MP3).
- **Lo que quedaba de una canción saltada** se tiraba al ritmo del reloj y podía colarse al principio de la siguiente; ahora se tira
  enseguida.
- **Un apagón con una de Spotify sonando** perdía su crédito (solo volvían las de la cola); ahora vuelve también.
- **Apagar el interruptor con una tuya sonando desde Spotify** (SP2) la cortaba a media canción; ahora acaba entera y después se cierra.
- **Una orden que tardaba se repetía** (la API, hasta 40 s esperando); ahora solo se repite si la conexión era vieja.
- **Los hilos de fondo** (el emparejador, la búsqueda, las portadas, las letras) van protegidos: un error en ellos habría cerrado la rocola.
- **Si Spotify no da la llave con todos los permisos de lectura** (no todos se permiten a cualquier programa), se pide con los básicos
  (buscar, tus listas, tus discos) en lugar de fallar todo.
- **El emparejador** espera un minuto si Spotify no contesta (límite de consultas), en vez de seguir preguntando cada 2,5 s.
- **Comparar títulos:** «- Remastered 2011», «- En vivo», «- Radio Edit» y similares (lo que Spotify pone tras un guion) se quitan; ya no
  se corta «live» o «version» en medio de un título.
- **Las letras:** si no hubo respuesta (la red), se vuelven a pedir al minuto (antes, esa canción se quedaba sin letra toda la noche); la
  posición ya no cuenta los huecos de silencio (si la red tarda, la letra no se adelanta); y el tamaño de letra ya no cambia mientras el
  panel sube (antes pedía una letra nueva en cada cuadro).
- **Tus listas de Spotify** se ponen al día cada 30 minutos (antes, una canción añadida a una lista no salía hasta reiniciar).
- **Las portadas en la memoria** de más de un día se borran al abrir el programa de Spotify.
- Probado otra vez con el `rocola-spotify` simulado: la misma canción dos veces suena entera las dos; la sustitución invisible, el disco
  completado y los créditos, como antes. Sin cuenta real ni A4.

## Extra (6-11-2026, cuarta parte): revisión de todo Rocola OS

Pedido del dueño: «revisa todo Rocola OS». Se repasaron el arranque instalado (initramfs: el disco por UUID, la copia en prueba, la vuelta
atrás, la reparación de datos.img), el inicio (S99rocola, S41red, S02hora), el lanzador de la rocola, la actualización del programa y del
sistema (rocola-actualiza), el instalador y el desinstalador, los discos de música, la tarjeta de sonido, el botón de encendido y la
salida para actualizarse (guarda el estado: los créditos se conservan). Lo que estaba bien se deja; se arregla:

- **Una versión del programa que se cae al arrancar una y otra vez** (aunque ya estuviera confirmada; por ejemplo, por algo en los datos)
  hacía que el lanzador la reabriera cada 2 s para siempre: solo se volvía atrás en los primeros 2 minutos de una versión nueva. Ahora, a
  la tercera caída seguida en menos de 30 s, se aparta (como mala: no se vuelve a bajar) y se usa la que trae el sistema; si esa también se
  cae, espera 30 s entre intentos. Probado con un programa que se cae siempre.
- **Las portadas de Spotify de tus discos** (SP5) se guardaban en lo aprendido (datos.img, 512 MB); con una biblioteca grande podían
  llenarlo. Instalada, ahora van al disco de la rocola (`\rocola-os\portadas_spotify`), como el material de la IA.
- Comprobado sin cambios: el espacio que pide la actualización del sistema (1,4 GB: bajar el `.gz` y descomprimir 1 GB) alcanza; un apagón
  a media actualización no cambia el arranque (se cambia lo último); el instalador copia y comprueba byte a byte y guarda el arranque de XP
  antes de tocarlo; el reinicio tras quitar la memoria funciona sin ella; la salida para actualizarse no borra créditos ni cola.

## Extra (7-11-2026): vuelve el modo noche

Pedido del dueño: «nos aventaron piedras por tener música a las 12 A.M, así que volveremos a poner el modo noche, con un slider desde el
70 al 90 % del audio, y con un botón para probar cómo se escucharía cuando se active». Igual en la rocola de XP (`Form1.Sonido.cs`) y aquí
(`Motor/ModoNoche.cs`), con las mismas claves:

- **De 11 de la noche a 7 de la mañana** (`noche_desde=`, `noche_hasta=`) la rocola suena al porcentaje del deslizador (`noche_pct=`, de 70
  a 90 %, 80 de fábrica). Es el porcentaje de **lo que se oye**, no de la señal: al 80 % son 3,2 dB menos (al 70 %, 5,1; al 90 %, 1,5).
  Interruptor «Modo noche» (`noche=`), **encendido de fábrica**.
- **Baja poco a poco** (20 s), con un aviso al empezar y al acabar; la canción que suena, la siguiente ya preparada, Spotify (pasa por la
  misma cadena) y los videos (aquí, un filtro de volumen de mpv que la rocola mueve en vivo; en XP, la perilla del reproductor).
- **«Probar el modo noche»** (Ctrl+F3 → Sonido, debajo del volumen de los videos): 15 segundos al nivel de la noche, al momento, para oír
  cómo quedaría; luego vuelve solo. No se guarda.
- Al escribir a un archivo (`--wav`) no hay modo noche: sale igual a cualquier hora.
- Probado: en XP, la batería `noche` (el cruce de la medianoche, los porcentajes, la rampa por reloj, la cadena y los videos, la
  configuración) y las del sonido; aquí, la ventana de ajustes en captura (los tres renglones y «PROBANDO 15 s»). No oído en el bar ni en
  el A4.

## Extra (7-11-2026, segunda parte): Spotify en las portadas, para quien no usa el buscador

Pedido del dueño: «los resultados de Spotify aparecen en el buscador, pero ¿cómo podríamos mostrarlos también en las portadas de discos
como la música normal? No muchos en mi familia saben usar el buscador». Se propusieron cuatro y eligió todas («Haz todo»). Cada una con
su interruptor en Ctrl+F3 → Spotify, encendidas (y solo con el modo Spotify encendido). `Motor/SpotifyPortadas.cs` y
`Consola/Pantalla/SpotifyXp.cs`.

- **P1. Spotify en las letras** (`spotify_letras=`): en la letra de cada artista de tu biblioteca, justo detrás de sus discos, hasta tres
  de sus discos más conocidos en Spotify que no tienes (un remasterizado o una edición de lujo cuentan como el mismo disco), con una
  marquita verde de Spotify en la esquina; y tus discos guardados en Spotify, en la letra de su artista (si es una letra que no tenías,
  aparece). Se eligen y se piden como cualquier disco, con crédito. Mientras llegan discos, la pared se reacomoda como mucho cada 15 s y
  lo que estás mirando se queda elegido.
- **P2. Más de este artista** (`mas_del_artista=`): dentro de un disco (tuyo o de Spotify), debajo de la portada, la tira «Más de … en
  Spotify» con hasta 8 discos (sin los que ya tienes). ABAJO desde la última canción entra a la tira; IZQ/DER pasan de disco; PONER lo
  abre; REGRESAR vuelve al disco de antes (y otra vez, a la pared).
- **P3. Tus artistas con foto** (`spotify_artistas=`): en la letra SPOTIFY, hasta 24 artistas (los que más escuchas y los que sigues; si
  la cuenta no los da, los de tu biblioteca con más canciones), con su foto. Al entrar: lo más conocido del artista y, en la tira, sus
  discos.
- **P4. Por género y época** (`spotify_generos=`): en la letra SPOTIFY, Cumbias, Rock en español, Baladas en español, Banda, Norteñas,
  Salsa, Rancheras, Pop en español, Éxitos de los 80 y Éxitos de los 90 (una lista de Spotify de cada una; la de Spotify si la hay).
- **Cómo se trae**: un hilo de fondo pide los discos de cada artista de tu biblioteca (y de tus artistas), uno cada 2,5 s, y los apunta
  en `rocola_spotify_artistas.txt` (solo nombres y números; se renuevan a los 14 días). Las portadas se bajan **solo cuando se ven**, a la
  memoria (`/run`), para no llenarla con las de toda la biblioteca.
- Probado con el `rocola-spotify` simulado: los discos en la letra A con su marca y la letra B nueva con el disco guardado; la tira dentro
  del disco, entrar a un disco de la tira y volver; un artista con foto en SPOTIFY, sus canciones y sus discos; pedir desde ahí cobra el
  crédito; con los interruptores apagados la pared queda como antes. Sin cuenta real ni A4: si Spotify no deja ver algo (por ejemplo,
  las listas de género o lo más conocido de un artista), simplemente no sale.

## Extra (7-11-2026, tercera parte): trabajar en la rocola desde casa

Pedido del dueño: «hazme un programa/herramienta para poder usar en mi laptop con POP_OS o desde mi computadora WINDOWS, en la que me
diga varias cosas esenciales de la rocola, como sus errores, datos de diagnósticos... también desde dicho programa poder despertar la
rocola para forzar actualizaciones, y que me dé estados de todo». Ver `sistema/casa/LEEME.md`.

- **El buzón** (`/usr/bin/rocola-buzon`, lo arranca S99rocola a los 90 s): la rocola no se puede alcanzar desde fuera, así que habla con
  casa por un repositorio privado de GitHub (`rocola-buzon`) con su propia llave (`rocola_buzon.txt` en una memoria USB, como la de las
  actualizaciones; solo ese repositorio). Cada minuto lee las órdenes; sube su estado cuando algo cambia (como mucho cada 2 minutos y al
  menos cada hora: versiones, actualización, red, equipo, lo que suena, los últimos errores y bitácoras), y a pedido los registros, un
  archivo entero de los datos (nunca las llaves ni la cuenta de Spotify) y el diagnóstico completo. Ctrl+F3 → Internet dice cómo va.
- **El programa** atiende lo que se pide desde casa (`Actualizacion.cs`): reiniciar el programa o el equipo y el diagnóstico completo
  cuando está libre (o el programa «ya»: sale con 42 y se pone la versión bajada, si la hay), y cada 15 s deja lo que pasa ahora en
  `/run/rocola-vivo.txt`.
- **Rocola en casa** (`sistema/casa`, .NET 10, un solo archivo para Pop!_OS y otro para Windows): un tablero en el navegador (solo en
  esta computadora) con Resumen (en línea, ¿la actualización ya está?, el programa corre, errores, compilaciones), Errores (agrupados),
  Registros, Archivos (y guardarlos en `~/RocolaCasa`), Actualizaciones (compilar en GitHub, buscar ahora, poner la nueva ya), Órdenes
  con sus respuestas, Despertar (Wake on LAN al router) y Ajustes; y en la terminal `--estado` y `--orden`.
- **Apagarla desde casa** (pedido del mismo día: «para cuando termine de trabajar con ella apagarla»): «apagar» cuando esté libre (el
  programa guarda el estado y sale con 44: `/usr/bin/rocola` apaga) o «ya» (el buzón apaga al momento); «cancelar» quita lo pendiente.
  La tarjeta de red sigue escuchando el encendido remoto.
- Probado con un GitHub simulado: la rocola sube su estado, atiende ping, registros, un archivo, buscar actualización y rechaza las
  llaves; el tablero muestra todo y los veredictos. Sin el buzón real ni el A4: la rocola necesita el siguiente sistema.

## Extra (7-11-2026, cuarta parte): optimizaciones de lo nuevo

Pedido del dueño: «busca optimizaciones en todo lo que acabamos de hacer, me imagino que lo de Spotify es pesado». Nada cambia de aspecto
ni de comportamiento (automáticas, sin interruptor):

- **Spotify en las letras (P1):** lo que va en cada letra se calcula **en otro hilo** y en la pantalla solo se mezcla, en una pasada por
  letra. Antes se insertaba disco por disco normalizando la letra entera cada vez (cuadrático, en el hilo que dibuja): en una letra con
  cientos de discos, un tirón.
- **Normalizar nombres** (comparar artistas y discos sin acentos ni mayúsculas): se recuerda lo ya normalizado (las mismas cadenas se
  comparan miles de veces) y ya no crea listas nuevas en cada llamada. Cada disco guarda la clave de su artista.
- **Un disco de artista nuevo ya no despierta a toda la pantalla:** antes cada uno (uno cada pocos segundos al principio) hacía que la
  pantalla repasara toda la biblioteca (portadas que faltan y canciones que le faltan a cada disco); ahora ese repaso va como mucho cada
  5 s, y solo cuando de verdad cambió algo de lo tuyo.
- **Las portadas de Spotify en la pared:** la pantalla ya no toma un cerrojo en cada cuadro por cada disco sin portada (el disco recuerda
  que ya la pidió).
- **La tira «Más de este artista»:** sus textos se arman solo cuando cambia lo elegido (antes, en cada cuadro), y la tira se pregunta cada
  1,5 s (antes, 0,7).
- **El hilo de los discos de artistas**, en reposo, mira lo pendiente cada 15 s (lo que pide la tira, cada segundo).
- **El modo noche** (Rocola OS y XP) mira la hora una vez por segundo, no en cada vuelta del bucle.
- **El buzón:** busca su llave en las memorias USB solo si falta o cada 15 minutos (antes montaba y desmontaba las memorias cada minuto);
  pregunta por las órdenes con ETag (si no cambiaron, GitHub contesta 304: sin cuerpo y sin gastar consultas), sin una consulta aparte
  para ver si el buzón existe; arma su estado (unos 30 programas pequeños) como mucho cada 2 minutos, no cada minuto; y corre con
  prioridad baja (nice 15). **Rocola en casa** no pregunta a GitHub con la pestaña del navegador escondida.
- Probado otra vez: la pared con Spotify, la tira, la batería `noche` de XP y el buzón con el GitHub simulado (la segunda vuelta: 304 y
  sin rehacer el estado). No medido en el A4.

## Extra (7-11-2026, quinta parte): más optimizaciones

Pedido del dueño: «busca más optimizaciones». Automáticas, sin cambio de aspecto ni de comportamiento:

- **Los círculos y anillos de la pantalla** (el número de cada canción dentro de un disco, la marca de Spotify, los puntos de página) usan
  tablas de senos y cosenos calculadas una vez: antes eran 2 senos y 2 cosenos por triángulo en cada cuadro (unos 1 500 por cuadro
  dentro de un disco). Mismos puntos.
- **Los nombres de los discos de Spotify** se comparan con la normalización que se recuerda (antes cada comparación armaba la cadena otra
  vez).
- **El modo noche, durante su rampa** (Rocola OS y XP): a la canción siguiente ya preparada solo se le cambia el volumen; antes se le
  recalculaba todo (nivelado, EQ, realce, bajo, DSEE) en cada vuelta durante 20 s. Y el factor de la noche se calcula una vez por
  porcentaje.
- **El buzón:** recuerda el «sha» de lo último que subió a cada archivo y ya no lo pregunta antes de cada subida (si otro lo cambió,
  GitHub dice 409 y entonces sí pregunta); las respuestas se leen una sola vez; y las órdenes se leen con un solo `jq` (antes, tres por
  orden).
- **Rocola en casa** recuerda lo que bajó de GitHub con su ETag y pregunta «¿cambió?»: si no, GitHub contesta 304, sin cuerpo y sin
  gastar consultas (el tablero pregunta cada 30 s).
- Probado: las baterías `noche`, `videoaudio` y `todojunto` de XP, la pantalla dentro de un disco con la tira, y el buzón y el tablero
  con el GitHub simulado (304 en la segunda lectura, subidas sin preguntar el sha). No medido en el A4.

## Extra (7-11-2026, sexta parte): revisión de fallas de todo lo de Spotify

Pedido del dueño: «revisa que todo lo relacionado con Spotify no tenga fallas». Se arregla:

- **La llave que deja de valer con la rocola prendida** (`rocola-spotify`): si cambias la contraseña de Spotify o cierras sesión en
  todos los dispositivos, antes reintentaba con la llave vieja para siempre (Spotify sin funcionar hasta reiniciar). Ahora la borra y
  vuelve a esperar a que la elijas desde el celular, como la primera vez.
- **El emparejador (SP4) y el hilo de los discos de artistas** se acababan si `rocola-spotify` se caía y se volvía a abrir solo: ya no
  emparejaban ni traían discos hasta reiniciar la rocola. Ahora siguen con el nuevo.
- **Un corte de internet** apartaba un día a cada artista que tocaba en ese momento (sin discos en las letras ni en la tira). Ahora se
  reintenta; solo a la tercera falla seguida (un artista que ya no está) se aparta un día.
- **Una portada que no se pudo bajar** porque `rocola-spotify` se reabrió ya no se queda sin portada hasta reiniciar: se vuelve a pedir.
- **La tira «Más de este artista»** ya no sale en las listas (tus favoritas, tus listas, las de género): son de varios artistas.
- **Un disco de Spotify en la pared aún sin abrir** decía «0 canciones»: ahora dice «Disco de Spotify» y, al quedarse en él, sus
  canciones se traen solas (la ficha dice cuántas son y cuáles).
- **El archivo de los discos de artistas** ya no crece para siempre: si pasa de 2 MB se reescribe con lo vigente.
- Si se apaga Spotify mientras la pared con sus discos se está calculando, lo calculado ya no se pone.
- Revisado sin cambios: créditos y su devolución, la cola tras un apagón, la versión de Spotify de tus canciones dañadas, las letras,
  el buscador, el modo noche sobre lo de Spotify, apagar el interruptor a media canción.
- Probado con el `rocola-spotify` simulado (la pared, la tira, un artista que carga sus canciones al verlo); `rocola-spotify` compila con
  el cambio. Sin cuenta real ni A4.

## Extra (7-11-2026, séptima parte): del buscador a un disco de Spotify, y de vuelta al buscador

Pedido del dueño: «cuando en el buscador se busque un disco que solo está en Spotify, se lleva directamente al disco, y al salir del
disco se regresará al buscador, ya que en este caso el disco no está en la pared de discos, solo en el buscador».

- Un disco o una lista de Spotify del buscador **que no está en la pared** se abre directo (la pared no se mueve) y **REGRESAR vuelve al
  buscador**, con lo que se había escrito y el mismo resultado elegido. Si desde ahí se abre otro de la tira «Más de…», REGRESAR vuelve
  primero a ese disco y luego al buscador.
- Si **sí está en la pared** (en la letra SPOTIFY, o en su letra con «Discos de Spotify en las letras»), la pared se queda en él y
  REGRESAR vuelve a la pared, como con tus discos.
- Probado con el `rocola-spotify` simulado: «Disco solo en Spotify» → dentro → REGRESAR → el buscador con «solo» y el disco elegido;
  «Disco guardado» → REGRESAR → la pared, en él.

## Extra (7-11-2026, octava parte): el buscador, para que cualquiera encuentre su canción

Pedido del dueño: «necesito que mejoremos el algoritmo de búsqueda al máximo, para que cualquiera que intente encontrar una canción pueda
hacerlo». `Motor/BuscadorFlexible.cs`, automático, sin interruptor.

**Lo de antes se queda y manda:** la búsqueda de XP (igual, empieza igual, empieza palabra, en medio, por palabras, una errata) sigue
saliendo **primero** y en su orden. Cuando da menos de 40, una segunda pasada **rellena debajo** con:

- **Como suena** (español): b/v, s/c/z, ll/y, h muda, qu/k, ge/je, ch/sh, ph/f, n antes de b/p, letras dobles. «bicente fernandes» →
  Vicente Fernández; «kerida» → Querida; «chakira» → Shakira; «abito de ti» → Hábito de ti; «estos selos» → Estos celos.
- **Faltas**: una letra mal en palabras de 4 a 6 letras y dos en las de 7 o más (cambiada, de más, de menos o dos al revés), también a
  medio escribir: «juan grabiel», «luis migel», «bohemian rapsody», «yesterdey», «contrabando y traision».
- **Palabras de sobra o que faltan**: «el», «la», «de», «the»… no se exigen; con tres o más palabras bastan dos de cada tres: «como la
  flor de selena», «tigres jaula oro».
- **Pegadas o separadas**: «lacucaracha» → La cucaracha.
- **El disco y el género** de cada canción cuentan (menos que el título y el artista): «selena amor prohibido» trae también las demás
  del disco.
- **Precisión**: con varias palabras, una tiene que casar bien o todas con poca falta; si lo exacto ya encontró 5 o más, de lo flexible
  solo lo que suena igual.
- **Lo más pedido primero**, a igualdad, en toda la búsqueda (hasta 150 puntos por lo que ha sonado; los escalones van de miles en miles,
  así que solo desempata).
- **Rápido**: un índice con cada palabra distinta de la biblioteca (como suena) y las entradas que la tienen; se compara contra esas
  palabras (no contra cada canción) y lo de cada palabra buscada se recuerda mientras se escribe la siguiente. Medido en la máquina de
  pruebas con 20 000 canciones: de 4 a 12 ms por búsqueda sin nada recordado (antes de este índice, 14 a 25 ms); sin la segunda pasada,
  0 a 2 ms. No medido en el A4.
- Prueba: `rocola-consola --busca "texto" ...` (con `ROCOLA_MUSICA_LISTA`) dice los 8 primeros y lo que tardó.

## Extra (7-11-2026, novena parte): buscar una canción por su letra (L1 y L3)

Pedido del dueño: «¿también se podría buscar una canción por su letra?» → eligió L1 y L3. `Motor/BuscaLetras.cs`. Se busca con una
**frase de 4 palabras o más** en el buscador de siempre.

- **L3. La letra que traen tus MP3** (automático): un repaso de fondo, con prioridad mínima (20 ms entre archivos), lee una vez cada MP3
  (el marco USLT de su etiqueta; otra vez si el archivo cambia) y guarda **solo sus palabras sueltas**, como suenan, en
  `rocola_letras_mp3.txt` (no la letra). Una frase con 3 de cada 4 de sus palabras en la letra encuentra la canción, aunque la última
  vaya a medio escribir. Sale debajo de lo exacto (650 puntos).
- **L1. La letra en internet** (Ctrl+F3 → Internet → «Buscar canciones por su letra (Genius)», encendido; necesita la llave de Genius):
  al dejar de escribir una frase se pregunta a Genius de qué canciones es (por `curl`, como las actualizaciones; nada se guarda). Sus tres
  primeras van **delante** de los demás resultados: **tu canción** si la tienes (el mismo título como suena, con poca falta, y el
  artista); si no, **la de Spotify** (con el modo encendido y el mismo título); si no, una tarjeta **«NO ESTÁ»** que dice cuál es (al
  elegirla, «no está en la rocola»). «Querida» y «Querida (En Vivo)» cuentan como una.
- **La llave de Genius**: gratuita (genius.com/api-clients → Client Access Token; pasos en `sistema/casa/LEEME.md`). Llega a la rocola
  desde Rocola en casa (Ajustes → «Mandar a la rocola», orden `llave_genius`) o en `rocola_genius.txt` en una memoria USB (la copia el
  buzón). Nunca se puede pedir de vuelta como archivo. Las condiciones de uso de Genius son para uso no comercial.
- Probado: MP3 de prueba con una letra **inventada** (frases enteras, en desorden, a medio escribir); un Genius simulado (tu canción,
  una que no tienes, la llave equivocada no truena); la llave mandada desde casa por el buzón simulado. Sin la API real de Genius ni
  el A4.

## Extra (7-11-2026, décima parte): color vivo, que se calma mientras se usa

El dueño comparó con la rocola de XP («los textos en XP son más coloridos») y, tras ver la maqueta de las ideas C1-C12, eligió la
opción **Vivo** «pero con sus medidas de baja contaminación visual»: mientras alguien usa la rocola, los efectos bajan, como siempre
se hizo. Solo en Rocola OS (`Consola/Pantalla/ColorVivo.cs`), opciones visuales de coste bajo, cada una con su interruptor
persistente en Ctrl+F3 → Imagen → **Color vivo**, encendidas de fábrica (claves en `Motor/MejorasVisuales.cs`). No se hicieron C7
(cielo con los colores de la portada) ni C8 (luces de antro).

- **La calma mientras se usa** («viveza», de 0,35 a 1): baja en 0,4 s al primer botón (también con los ajustes, el buscador, la
  ayuda o el asistente abiertos) y vuelve en 3 s cuando pasan 15 s sin botones, en el reposo y en la atracción. Todo lo que brilla
  o se mueve se multiplica por ella, y lo que se mueve va más despacio (el degradado y el neón avanzan a su paso).
- **C1** `textos_color=`: los nombres de la pared, el título de lo que suena, el disco en el reposo y los números de las canciones
  con el color del disco.
- **C2** `titulos_degradado=`: «AHORA SUENA», el nombre del disco abierto y «LETRA X» en un degradado cian-rosa-oro que corre (una
  vuelta cada 6 s en calma); en uso, más pálido y casi quieto.
- **C3** `letras_color=`: cada letra de la barra con el color del género que más se repite entre sus discos ya conocidos (no se lee
  ningún archivo para esto); sin ninguno, un color por su sitio en el abecedario. La elegida brilla con el suyo.
- **C4** `vidrio_color=`: los vidrios (cabecera, barra de letras, lo que suena, reposo) se tiñen con la canción que suena, más vivo,
  y pasan de una a otra en 1,5 s; el filo, del mismo color.
- **C5** `neon_borde=`: una luz del color de la canción da vueltas por el borde de lo que suena, del disco abierto y del reposo.
- **C6** `vidrio_late=`: el brillo de esos vidrios sube con cada golpe del bajo.
- **C9** `portadas_brillan=`: un resplandor del color de cada portada debajo de ella (pared, disco abierto, reposo).
- **C10** `destello_pedir=`: un aro de luz del color del disco donde se pidió la canción (0,7 s, al 70 %).
- **C11** `barra_neon=`: la barra de avance en degradado del color de la canción, que pulsa con la música (la barra con forma, V10,
  toma el mismo color).
- **El color de un disco**: el de su portada si tiene color (así combina con ella); si es gris, el de su género; si no, el azul de la
  rocola. Siempre llevado a 55 % de saturación y 85 % de brillo como mínimo.

Medido con el dibujo por software de Mesa (no es el A4), 300 cuadros a 1024x768: reposo 12,8 ms de mediana con todo encendido frente
a 11,9 ms con todo apagado; la pared con algo sonando 13,6-14,4 frente a 13,2-13,5; unas 0,1 pantallas más de relleno. No medido ni
visto en el A4. Para las capturas, `--boton calma` deja la pared 20 s sin botones (el color entero).

## Extra (7-11-2026, onceava parte): el cielo según la hora, la fecha y el clima

El dueño vio una maqueta con un cielo realista y prefirió el de hoy («no busco el realismo extremo»), pero le gustó que cambie con la
hora. Con la segunda maqueta («Cielo según la hora») pidió: «Implementa todo, e incrementa la cantidad de decoración por fechas, que
se vea más bonito, y que también tome los datos del clima del internet». Solo Rocola OS. El cielo Legacy se queda con su estilo
dibujado; cambian sus colores y se le pone encima lo de cada momento. Cada cosa lleva su interruptor en Ctrl+F3 → Fondo (grupos
«Según la hora», «Según la fecha» y «Clima y más»), encendidos de fábrica.

- **Según la hora** (por la altura real del sol en la ciudad del bar, `Astro` en `Motor/CieloClima.cs`):
  - H1 `cielo_hora=`: las paradas del degradado, las nebulosas y el resplandor del horizonte pasan por día, hora dorada,
    atardecer o amanecer, noche (las paradas de siempre, tal cual) y madrugada (más índigo, de 2 a 4:30).
  - H2 `sol_hora=`: un sol dibujado en su rumbo y altura (el este a la izquierda), que se esconde detrás del horizonte.
  - H3 `estrellas_anochecer=`: primero las brillantes, luego las demás.
  - H4 `luna_arco=`: la luna sale, cruza y se pone a sus horas (si no está en el cielo, no se ve; apagado, siempre arriba a la derecha).
  - H7 `luna_sin_disco=`: su lado oscuro deja ver el cielo (antes era un disco negro).
  - H5 `aurora_hora=`: la aurora toma el color de la hora y casi no se ve de día.
  - H6 `medianoche=`: a las 12, fugaces y un destello en la onda.
- **Según la fecha** (`CalendarioCielo`):
  - F1 `fiestas_cielo=`:
    - Año Nuevo: confeti dorado; el 31 a las 23:59:50 una cuenta en el cielo y fuegos; luego «¡Feliz 2027!» cinco minutos.
    - Reyes: la estrella de Belén y tres estrellitas viajeras.
    - 14 de febrero: tonos rosas y corazones que suben.
    - Jacarandas (del 15 de marzo al 15 de abril): pétalos lilas.
    - Día del Niño: globos.
    - 10 de mayo: pétalos de rosa y corazones.
    - Fiestas patrias: papel picado tricolor y aurora verde y roja. La noche del Grito, fuegos verdes, blancos y rojos; las otras
      noches, alguno de vez en cuando.
    - Halloween: murciélagos, la luna naranja y tonos morados.
    - Día de Muertos (28 de octubre a 2 de noviembre): dos tiras de papel picado, pétalos de cempasúchil, veladoras en el horizonte,
      lucecitas que suben y la aurora naranja y rosa.
    - La Guadalupana: pétalos de rosa.
    - Navidad (13 de diciembre a 4 de enero): nieve y una serie de luces que se prende en cadena. En Nochebuena, además, la estrella
      de Belén y la aurora verde y roja.
  - F2 `lluvias_estrellas=`: Cuadrántidas, Líridas, Eta Acuáridas, Perseidas, Dracónidas, Oriónidas, Leónidas, Gemínidas y Úrsidas,
    un día antes y uno después de su máximo, con fugaces que salen de su radiante.
  - F3 `luna_llena=`: la luna llena más grande, con dos anillos de halo y el cielo un poco más claro.
- **Clima y más**:
  - O1 `constelaciones=`: Orión, la Osa Mayor y Casiopea se dibujan cada 40 s con su nombre.
  - O2 `nubes_cielo=`: unas nubes dibujadas.
  - O3 `clima_cielo=`: el clima de afuera.
- **El clima de afuera (O3)**:
  - Se pide a Open-Meteo por curl cada 15 minutos, en un hilo de fondo; es gratuito y sin llave.
  - Lo que se dibuja según el código: algo nublado, nublado (el cielo más gris y menos estrellas), neblina (bruma de abajo hacia
    arriba), llovizna, lluvia, lluvia fuerte, tormenta (rayos con destello suave) y nieve.
  - El último clima vale 3 horas tras un reinicio (`rocola_clima.txt`).
  - La hoja dice el clima, la temperatura, el lugar y hace cuánto se pidió.
- **Ciudad del bar** (`ciudad_bar=`):
  - «Automática» la saca de la conexión a internet, una vez por semana (`rocola_clima_lugar.txt`). Prueba ipapi.co, luego ipwho.is y
    luego get.geojs.io.
  - También se puede elegir una de 41 ciudades.
  - Mientras no se sabe, se usa la Ciudad de México.
- **Mientras se usa la rocola** (la viveza del color vivo): menos pétalos, nieve y globos; sin fugaces ni constelaciones; uno de cada
  tres fuegos; los rayos, sin rayo y con destello tenue.
- **Para probar**:
  - `ROCOLA_CIELO_FECHA=2026-11-01T21:00` fija la hora del cielo. Ojo: es hora local, y en la rocola la zona es la de México.
  - `ROCOLA_CLIMA=lluvia` (o despejado, poco, nublado, neblina, llovizna, fuerte, tormenta, nieve) fija el clima.
  - `ROCOLA_CLIMA_API` y `ROCOLA_UBICA_API` cambian el servidor; `ROCOLA_CLIMA_YA` pide sin esperar 20 s.
  - `--boton ajuste:CLAVE` abre Ctrl+F3 en la hoja de ese ajuste.
- **Medido** con el dibujo por software de Mesa (no es el A4), 300 cuadros a 1024x768:
  - Noche: 10,5 ms de mediana frente a 9,1 con todo apagado.
  - Día de Muertos: 12,0 ms; tormenta: 12,2; Grito: 11,5; Navidad: 11,3.
  - Sin memoria nueva por cuadro.
- **No probado** en el A4 ni con el clima del bar. Desde esta máquina Open-Meteo contestó una vez y luego dio el límite diario (la IP es
  compartida); la lectura se probó con un Open-Meteo simulado.

### Más festividades (7-11-2026, «Pon mas festividades»)

Se suman a F1 (`fiestas_cielo=`), con el mismo interruptor:
- **Candelaria (2 de febrero):** farolitos de papel que suben.
- **La bandera ondeando** arriba a la izquierda: el Día de la Bandera (24 de febrero), el 15 y 16 de septiembre y el 20 de noviembre.
- **Día de la Mujer (8 de marzo):** tonos y pétalos morados, con mariposas.
- **San Patricio (17 de marzo):** tréboles verdes.
- **Primavera (19 a 23 de marzo):** mariposas de colores y flores.
- **5 de mayo y Revolución (20 de noviembre):** papel picado tricolor y, de noche, fuegos de vez en cuando.
- **Día del Padre (tercer domingo de junio):** globos azules y dorados y destellos.
- **El resto de septiembre:** una tira de papel picado tricolor (el 13 al 16 sigue como antes, con el Grito).
- **Santa Cecilia (22 de noviembre), día de los músicos y del mariachi:** notas musicales que flotan.
- **Las posadas (16 a 24 de diciembre):** tres piñatas de estrella que se mecen y farolitos, además de lo de Navidad.
- **Día de Muertos:** ahora también llegan las mariposas monarca.

Todas se revisaron con capturas con la fecha forzada; nada en el A4.

## Extra (7-11-2026, doceava parte): artistas de Spotify en el buscador

Pedido del dueño: «cuando salgan artistas que no hay en la rocola, debería salir el artista en Spotify [...] se hará zoom hacia la
casilla escogida mostrando los resultados de dicho artista, y al volver estarás de nuevo en el buscador». Solo con el modo Spotify.

- **Qué artistas salen:**
  - La búsqueda de Spotify pide también artistas (`type=...,artist`).
  - Solo los que no están en la rocola: ni por su pareja de SP4 ni por el nombre de algún disco tuyo (`ArtistaEnLaRocola`).
  - Hasta tres. El primero que da Spotify va si su nombre se parece a lo escrito, a dos letras, también a medio escribir (`Cerca`);
    los demás, si lo contienen o están contenidos.
- **Cómo se ven:** la tarjeta dice ARTISTA y «Spotify · artista · 1,3 M de seguidores», con su foto. Van justo después de los
  artistas tuyos que salgan primero; si no hay ninguno, hasta arriba.
- **Al elegir uno:**
  - Se abre su página de Spotify: lo más conocido y, debajo de la portada, la tira con sus discos (P2).
  - La página crece desde la casilla elegida hasta la pantalla en 0,42 s, con un destello verde (`ZoomArtista`). Para eso, `Dibujo`
    tiene ahora un zoom: lo que se dibuja con él puesto se escala, se corre y se aclara.
  - REGRESAR vuelve siempre al buscador, con lo escrito y la misma elección. Los discos que se abran desde la tira se apilan como
    siempre.
- **Lo que no cambia:** un artista que sí está en la rocola sigue llevando a sus discos de la pared.
- **Probado** con el rocola-spotify simulado (búsqueda, zoom a medio camino, página con su tira y regreso); no con una cuenta real
  ni en el A4. Arreglo de paso: el número de seguidores se escribe sin cultura (el programa corre sin las tablas de idiomas).

### Revisión de fallas del cielo (7-11-2026, «Busca bugs fallos errores y mejoras y cosas que no tienen sentido»)

- **El sol y la luna saltaban de orilla.** En el centro de México, de mayo a julio el sol pasa a mediodía por el norte, y la luna a
  veces también. El lado de la pantalla salía del rumbo de 60 a 300 grados, recortado: a mediodía se iba de la orilla izquierda a la
  derecha de golpe. Ahora sale del seno del rumbo (este a la izquierda, oeste a la derecha, norte o sur en medio), que es continuo.
- **El clima viejo se quedaba para siempre.** Sin internet varios días, el cielo seguía lloviendo con el último clima. Ahora uno de
  más de 3 horas ya no vale y la hoja dice «El último clima ya es viejo».
- **Al cambiar la ciudad del bar** el clima tardaba hasta 15 minutos en ser el de allí. Ahora el hilo espera en tramos de 30 s y, si
  cambió el lugar, lo pide al momento.
- **Navidad se apagaba el 31 de diciembre** y volvía el 1 de enero. Ahora sigue todo el 31.
- **Orión salía todo el año**, y en las noches de verano no se ve. Ahora de mayo a octubre va el Escorpión en su lugar.
- **La estrella de Belén caía en medio de Orión.** Ahora va arriba al centro.
- **Los fuegos de las fiestas usaban una hora fija** (desde las 19:00), que en verano todavía es de día. Ahora salen cuando ya oscureció
  según el sol.
- **La hora del clima se guarda en UTC**, para no convertir la zona horaria en cada cuadro al ver si caducó.
- **El archivo de las letras de los MP3 solo crecía.** `rocola_letras_mp3.txt` solo recibe líneas nuevas, así que cada MP3 que
  cambiaba dejaba su línea vieja. Ahora, al leerlo, si sobra más de un tercio (y al menos 200 líneas), se reescribe con la última línea
  de cada archivo: aparte y cambiado de un golpe. No se mira si los archivos existen, porque con el disco de la música aún sin montar se
  perdería todo. Probado: 300 líneas de 50 archivos quedaron en 50.

### Más pulido (7-11-2026, «Sigue buscando más de lo mismo, tenemos que pulir el programa al máximo»)

- **Los acentos no se quitaban en ningún lado** (lo más grave). Rocola OS corre sin las tablas de idiomas (`InvariantGlobalization`,
  por memoria y arranque en el A4), y en ese modo `string.Normalize(FormD)` no separa los acentos (comprobado: «Canción Ñandú» sigue
  igual). Tres lugares dependían de eso:
  - el buscador: «cancion» no encontraba «Canción» y «nandu perez» no encontraba «Ñandú Pérez»;
  - la letra de la pared: los discos que empiezan con «Á», «É» o «Ñ» se iban a «#»;
  - el emparejado con Spotify: «Corazón» no casaba con «Corazon».

  Ahora `Motor/Acentos.cs` pasa cada letra de Latin-1 y del latin extendido A a su base, como hacía la normalización en la rocola de XP.
  Lo que cambia con eso:
  - el orden de los discos dentro de una letra tampoco mira los acentos («Ángel» entre «Ana» y «Aqua», no después de «Azul»);
  - las canciones que no casaron con Spotify antes de este día y llevan acentos se vuelven a buscar ya, sin esperar el mes;
  - las palabras de las letras de los MP3 guardadas con acentos se vuelven a leer.

  Probado: «nandu perez», «osz», «cancion osz» y «angel rios» encuentran lo suyo, y «Ángel Ríos» queda en la A.
- **Letras que salían como «?».** Las letras de la rocola van del código 32 al 255. El «−» de «−3 dB» en los ajustes del sonido, las
  comillas y apóstrofes tipográficos (’ “ ”), los guiones largos y letras como ł, ő o ş (muchos títulos de Spotify y de MP3) salían
  como «?». Ahora se dibujan con su igual más cercano.
- **Dos trabajos de fondo sin protección**: la foto del artista de Spotify y el género de un disco. Un error ahí, en un hilo del grupo,
  cerraba la rocola entera. Ahora se atrapa y se apunta.
- **Los fallos del bucle se juntaban desde el arranque.** Al fallo 201 la rocola se cerraba (para que se vuelva a abrir), pero la cuenta
  nunca volvía a cero: un error raro, uno por hora, la cerraba a los ocho días a media canción. Ahora cuentan solo los seguidos: con
  un minuto sin fallos, la cuenta vuelve a cero.
- **El zoom del artista podía quedarse puesto** si un cuadro fallaba a medio dibujar el disco: todo lo siguiente saldría chiquito.
  Cada cuadro empieza ahora sin zoom.
- **Revisado sin fallos:**
  - todos los hilos de fondo (botones, salida de sonido, Spotify, emparejado, discos de artistas, conversiones, IA, portadas, el
    vigilante): cada uno atrapa sus errores o va por la cola del bucle;
  - los cálculos en segundo plano de la pared;
  - las cachés de Spotify y del buscador, que tienen tope.
- **Medido:** el buscador de 20 000 canciones sigue entre 3 y 8 ms por búsqueda tras el arreglo de los acentos, y «cancion de amor»
  ahora encuentra «Triste canción de amor».

## El movimiento: transiciones y selecciones suaves (7 de noviembre de 2026)

Peticion del dueño: «Quiero rediseñar todas las animaciones y transiciones para hacerlas mas esteticas, fluidas y menos bruscas» y
«revisa absolutamente todas las animaciones, al entrar y salir de discos, al abrir buscador, al aparecer el modo atraccion».
Un solo sistema (`Consola/Pantalla/Movimiento.cs`) con las mismas curvas (salen rapido y llegan suave, sin rebote), medido con el
reloj (no por cuadro: con menos cuadros dura lo mismo). Tres opciones visuales en Ctrl+F3 → Imagen → **Movimiento**, encendidas de
fabrica (coste bajo):

- **M1** `transiciones=`: escenas pared, disco, buscador, atraccion, ajustes y asistente. Entrar a un disco: crece desde su portada
  (0,36 s) o desde su casilla del buscador (0,38 s; el artista de Spotify con su destello verde, sustituye a `ZoomArtista`); volver
  a la pared o pasar de un disco a otro: la pared se asienta desde un 4 % mas cerca (0,28 s; desde la atraccion 0,34 s); el
  buscador baja (0,28 s); otra letra: la pared entra 70 px desde el lado hacia el que se movio (0,26 s); los ajustes crecen desde
  el 96,5 % (0,24 s); la atraccion aparece con un fundido de 0,9 s; el asistente, 0,3 s. La cabecera se va y vuelve con un fundido
  de 0,25 s (no esta en el buscador ni en la atraccion). Mientras dura la transicion de los ajustes el cielo se pinta entero.
- **M2** `seleccion_desliza=`: la fila elegida de un disco y la pastilla de la letra viajan a su sitio (seguidor exponencial de
  50 ms, por debajo de los textos con la meta del cuadro anterior); en el buscador, un aro de luz va de tecla en tecla, de casilla
  en casilla y del teclado a los resultados, y se desvanece al llegar.
- **M3** `listas_suaves=`: la lista de canciones de un disco se corre suave (la barrita tambien); las filas de los bordes se
  desvanecen y quedan recortadas al panel.

Nada hace esperar: los botones actuan al momento. Ya estaban animados y no cambian: el reposo (0,6/0,35 s), la ficha de la pared,
la cascada de resultados (V17), el titulo que cambia (V12), los avisos, la moneda, el vuelo de la cancion y los fundidos de la
atraccion. El renglon de los ajustes sigue saltando (su desplazamiento ya era suave). Pruebas: `ROCOLA_PELICULA=N` (con
`ROCOLA_RELOJ_FIJO` y `ROCOLA_RELOJ_PASO`) guarda N cuadros tras cada boton de `--captura`; revisado cuadro a cuadro. Banco del disco
abierto en llvmpipe: 14 ms de mediana, 1,2 KB por cuadro, sin recolecciones. No medido ni visto en el A4.

## Sin tirones y carga rapida (7 de noviembre de 2026)

Peticion del dueño: «optimizar la utilizacion de recursos al maximo, no puede haber tirones ni bajones de rendimiento, y todo tiene que
cargar extremadamente rapido». Todo automatico y sin interruptor (no cambia el aspecto ni el sonido). Medido con el banco
(`ROCOLA_BANCO`, `ROCOLA_PERFIL`; ahora apunta cada cuadro de mas de 4 ms y en que se fue: «LENTO») en la version NATIVA (NativeAOT,
como corre en el A4; la de pruebas con JIT da picos que en la rocola no existen):

- **Portadas a la tarjeta sin tirones**: subir varias portadas en el mismo cuadro, cada una calculando sus tamanos reducidos
  (mipmaps) en la tarjeta, se comia 15-20 ms de un cuadro. Ahora los tamanos reducidos se calculan en el hilo de las portadas
  (promedio de 2x2) y cada cuadro sube como mucho ~300 000 puntos (una grande o cuatro normales); lo demas, en los siguientes.
- **La busqueda en su hilo**: buscar en 20 000 canciones cuesta 2-8 ms aqui (mas en el A4) y se hacia en el hilo de la pantalla en
  cada tecla. Ahora un hilo propio busca lo ultimo escrito (si se teclea mas rapido, las intermedias se saltan) y la pantalla lo recoge
  en su vuelta; Spotify se mezcla solo con lo local de lo escrito.
- **Spotify: la siguiente ya bajada**: si la primera de la cola va a sonar desde Spotify (de Spotify o tuya sustituida, SP2), se le pide
  a rocola-spotify que la vaya bajando (orden nueva «precarga», `Player::preload` de librespot); al tocarla, librespot usa lo ya bajado
  y empieza al momento. Probado con el rocola-spotify simulado (llega la orden); no con una cuenta real.
- **Hilos de fondo de verdad abajo**: en Linux `Thread.Priority` no hace nada; el repaso de las letras, los discos de los artistas y las
  parejas de Spotify y el clima ahora piden nice 19 (como ya lo hacian el repaso, las portadas, la IA y las conversiones).
- **Menos basura por cuadro**: la cola para la pantalla se recuerda (se rehace si cambia la cola o pasa 1 s) y el reloj de lo que suena
  es un texto nuevo por segundo, no por cuadro.

Medido aqui (version nativa, sin la tarjeta): el programa gasta de 0,16 a 0,34 ms de CPU por cuadro de mediana en pared, disco,
buscador, reposo y atraccion, y el peor cuadro de 600 no pasa de ~2 ms (antes de este cambio la subida de portadas daba 18-20 ms);
de 1 a 9 KB de memoria por cuadro, sin recolecciones en el banco. Empezar una cancion local en el hilo principal: ~1 ms (la primera,
~6). Arrancar con 19 000 canciones (con la cache): 0,56 s hasta la primera captura. No medido en el A4.

### Segunda tanda («Sigue buscando mejoras», 7 de noviembre de 2026)

- **Portadas con libjpeg-turbo**: el sistema lleva `jpeg-turbo` (con SIMD; `BR2_PACKAGE_JPEG_TURBO`, dependencia de rocola-consola) y
  la rocola descomprime los JPEG con su libturbojpeg YA REDUCIDOS (1/8 a 8/8 de su tamano: lo justo para el lado que se va a ensenar).
  Medido aqui (version nativa): portada de 500 px 5,7 -> 2,1 ms; de 1000 px 16 -> 3,4 ms; de 1400 px 28,5 -> 4,3 ms (con todo lo
  demas: colores, mipmaps). Sin la biblioteca (un sistema viejo con el programa nuevo) o con un archivo que no puede, el lector de
  siempre (StbImageSharp). Una portada ya del tamano pedido no se copia otra vez.
- **El hilo de las portadas con nice 5** (antes 19, al parejo del repaso de la biblioteca): detras de la pantalla, delante del repaso.
- **Spotify sin colas**: rocola-spotify atiende cada conexion por separado, pero la rocola mandaba toda la API por una sola. Ahora lo
  que alguien esta esperando (buscar, abrir un disco o una lista, la pagina de un artista, letras, la foto del artista, lo que le falta
  a un disco, los atajos, la busqueda por letra) va por su conexion «prisa», y las portadas que se ven por tres conexiones (tres a la
  vez); lo de fondo (biblioteca, parejas, discos de artistas, generos) sigue en la suya. Las portadas de los resultados de Spotify se
  bajan de tres en tres y cada una sale al llegar. La busqueda en Spotify sale a los 300 ms de la ultima letra (antes 600: con los
  botones, entre letra y letra pasa mas de un segundo). Probado con el rocola-spotify simulado.
- Revisado y sin cambio: el cielo borroso de los vidrios (cinco pasadas en capas de 1/8 de la pantalla: casi nada; lo que contaba el
  banco era la capa del cielo que ya esperaba) y los brillos de la portada del disco (ya recortados a lo que se ve).

## SP2 mas preciso: cuando suena peor, la pareja estricta y comprobada con el sonido (7 de noviembre de 2026)

Pregunta del dueño: «¿que tan preciso es esto? ¿Hay forma de mejorarlo?»; eligio «A, B y C». Solo con el modo Spotify encendido
(`Motor/SustitucionSpotify.cs`). Calidad de Spotify: rocola-spotify pide 320 kbps (Ogg Vorbis, «Muy alta»); el Lossless de Spotify
no lo da librespot 0.8 (solo pide Ogg/MP3).

- **A. Suena peor, con mas datos** (`SuenaPeor`): antes, solo agudos cortados bajo 16 kHz. Ahora tambien lo que dice el MP3 (su
  cabecera: CBR, o Xing/Info/VBRI) y el pre-eco medido: menos de 128 kbps; grabada a menos de 44,1 kHz; 128 kbps con los agudos
  bajo 17,5 kHz (el corte de LAME); pre-eco con 192 kbps o menos y agudos bajo 19 kHz. En los VBR la tasa media no cuenta (sale de la
  musica, no de la calidad: un V0 tranquilo da ~110 kbps): solo el corte.
- **B. La pareja, estricta** (regla 2 en `rocola_spotify_pares.txt`, columnas 11 y 12: regla y comprobacion): con el ISRC del MP3
  (marco TSRC) se busca `isrc:` y solo vale la que lo tenga igual (duracion a 3 s); si no, el titulo base igual (antes bastaba que
  empezara igual), las marcas de version iguales en las dos (en vivo, remix/mezcla, acustica, karaoke, instrumental, demo, regrabada,
  extendida, a capella...; remasterizada o «radio edit» no cuentan), el artista principal igual palabra por palabra (antes «contiene»:
  «Los Angeles» casaba con «Los Angeles Azules») o etiquetada con uno de los artistas de la grabacion, la duracion a 2 s y, a
  igualdad, el disco que se llama como el tuyo y no un recopilatorio. Solo se sustituye con parejas de la regla 2: las de antes se
  vuelven a buscar, primero las que se sustituirian.
- **C. Comprobada con el sonido** (la primera vez que suena desde Spotify): en el hilo del sonido se apunta el volumen de lo que entra a
  la cadena por tramos contados en muestras (240 por cancion, como la forma de tu MP3) y al acabar se compara con la forma de tu MP3
  (correlacion en dB, corrimiento de hasta 3 tramos, al menos 40 tramos y 1,5 dB de vaiven): >= 0,80 la misma (se apunta), < 0,55 otra
  (esa cancion ya no se sustituye: suena tu MP3), en medio o sin datos, se vuelve a mirar la proxima vez. Solo numeros de volumen en
  memoria; nada se graba. La bitacora de Spotify dice cada veredicto.

Probado: `--prueba-sp2` (marcas, artistas, ISRC, la comparacion con formas inventadas y lo leido de MP3 de 64, 128, 320 kbps y VBR) y
de punta a punta con el rocola-spotify simulado (re-emparejado con la regla 2; la misma grabacion: «misma», r 1,00; otra: «otra»,
r 0,01, y la vez siguiente suena el archivo propio). No con una cuenta real ni en el A4. Las parejas que no pasan las reglas nuevas
dejan de dar tambien letras, portadas y canciones que faltan (SP1, SP5, SP6).

### Tercera tanda («Busca mas mejoras», 7 de noviembre de 2026)

- **Spotify al momento con la rocola callada**: si no suena nada ni hay cola y el cursor se queda medio segundo en una cancion de
  Spotify (dentro de un disco o lista), se precarga (`PrecargaElegida`); al pedirla, empieza sin esperar a que llegue. La que acaba
  de sonar no se vuelve a precargar.
- **Sin silencio entre canciones de Spotify**: al acabar una, la rocola esperaba a que rocola-spotify lo dijera (se le pregunta cada
  400 ms) rellenando con silencio. Con su duracion sabida, si ya llego toda menos 250 ms y no queda nada, se da por acabada al
  momento y empieza la siguiente (que ya estaba precargada).
- **La busqueda se reintenta una vez** si fallo porque la biblioteca se estaba poniendo al dia justo entonces (antes se quedaba en
  «Nada con eso» hasta la siguiente letra).
- Revisado y sin cambio: el guardado seguro ya baja los archivos al disco en Linux (`FlushFileBuffers` es `fsync` en `Ventana.cs`) y
  el estado se guarda en un hilo de fondo; arrancar en frio con 19 000 canciones tarda 0,33 s aqui; el resaltado de lo escrito en el
  buscador esta bien.

Probado con el rocola-spotify simulado (guiones: precarga de la elegida, dos de Spotify seguidas sin devolver creditos).

## Interruptores innecesarios retirados de Ctrl+F3 (7 de noviembre de 2026)

Peticion del dueño: «borra interruptores que sean innecesarios en la ventana de ctrl f3». Se revisaron los 165 mandos (para cada uno,
si su valor se lee en algun sitio fuera de los ajustes y la configuracion) y se retiraron seis; sus claves se ignoran:

- **No hacian nada en Rocola OS**: «Barra de letras con lupa» (`letras_lupa=`) y «Letra para leer de lejos» (`letra_lejos=`): se
  guardaban pero ningun dibujo los leia.
- **Ya no hay motivo para apagarlos** (quedan siempre encendidos): «Portadas siempre nitidas» (V4, `portadas_nitidas=`: sus reducciones
  ya se hacen en el hilo de las portadas, sin coste en la pantalla), «El ecualizador, de verdad» (V9, `eq_real=`: el espectro real ya se
  calcula de todas formas; apagado eran barritas de mentira) y «Pasos sin cortes» en la atraccion (V21, `atraccion_fundidos=`: con el
  movimiento nuevo, nada cambia de golpe).
- **Repetido**: «Avisos de vidrio» (V24, `avisos_vidrio=`): lo hace «Avisos de vidrio nuevos» (G10, `vidrio_avisos=`), que va en todos
  los avisos.

Tambien se quito el dibujo viejo del buscador (`Buscador()`), que ya nadie llamaba.

### Cuarta tanda («Busca mas mejoras», 7 de noviembre de 2026)

- **La portada grande, adelantada**: con el cursor 0,4 s quieto en un disco de la pared se lee su copia grande (`PrecargaGrande`,
  detras de lo que se ve); al entrar, la portada del cubo ya sale nitida en vez de verse estirada un momento.
- **Las letras de al lado, adelantadas**: con la pared quieta 0,6 s se va leyendo la primera pagina de la letra anterior y la
  siguiente (como ya se hacia con las paginas de al lado): al cambiar de letra, sus portadas ya estan.
- **Las portadas en el orden en que se ven**: cada portada pedida iba al principio de la cola, asi que en un mismo cuadro la ultima
  (abajo a la derecha del buscador) cargaba primero. Ahora lo pedido en un cuadro va en su orden y lo de un cuadro mas nuevo delante.
- «Rocola en casa» compilada (casa-1): su compilacion no estaba registrada en GitHub (nunca habia corrido); ahora corre tambien al
  cambiar su propio archivo de compilacion (solo eso) y desde Actions.

### Quinta tanda («Busca mas mejoras», 7 de noviembre de 2026)

- **`rocola_spotify_pares.txt` ya no crece sin fin**: cada pareja nueva o corregida (regla 2, comprobada con el sonido, rebuscada al
  mes) era una linea mas y nunca se compactaba (varios MB por vuelta con 20 000 canciones, en los 512 MB de los datos). Al arrancar
  Spotify, si tiene mas del doble de lineas que parejas, se reescribe con la ultima de cada una (como ya hacia el volumen parejo).
  Probado: 405 lineas de la misma pareja quedan en 1, con su ultimo estado.
- Revisado y sin cambio: el paso entre canciones locales ya va sin cortes (la salida no se cierra: `Encadenado`); las demas bitacoras
  ya tienen tope o se compactan; compilar con `OptimizationPreference=Speed` no gana nada medible (cadena entera de 70 s en 0,8 s,
  busquedas, portadas y cuadros iguales dentro del ruido) y el programa crece 400 KB: no se cambio.

