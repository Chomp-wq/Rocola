# rocola-xp: los cambios de la rocola de XP del 17 y 18 de noviembre de 2026

Rama `xp-1718`, sobre `junta-spotify` (42cc031). Los nueve commits de XP (carpeta `Rocola/`, rama `claude/modest-carson-fa2v2n`,
`0af4f6b..937c4bd`) pasados a `Codigo/` con la traducción de siempre (decisión del 8-11-2026: `Codigo/` es `Rocola/` con SOLO las
diferencias de traducción). `Rocola/`, `pruebas/` y `AUDIO_NOCHE_Y_MEJORAS_XP_MODEST_2026-11-17.md` se trajeron tal cual de 937c4bd (los
mismos bytes que en la rama principal: al juntar, git no ve conflicto) para que `diff-xp.py` compare contra la XP nueva.

Lo de XP (AGENTS.md, entradas del 17 y 18-11-2026): el ecualizador del dueño con E1 y E3; el modo noche desde las 22 h con la bajada de una
hora pareja en dB, sin aviso, y N1 (graves de noche); «Probar» hasta otro toque; el modo billar sin las cinco etapas que mueven el volumen,
sin claridad ni eses y con el bajo como lo dejó el dueño; el volumen parejo de cero (parejo_v2); lo más que bajó el limitador en 10 s; fuera
«Presencia nueva o antes» e I3; los «MP3» que no lo son (NoEsMp3.cs); la entrada de 10 ms tras un corte (SalidaPropia.cs).

## Lo que se pasó, archivo por archivo

Con `git merge-file` (lo que ya estaba traducido en `Codigo/`, la XP de antes y la XP nueva) y a mano lo que chocó o no compilaba:

| Archivo | Cómo |
|---|---|
| `CompararCiegas.cs`, `Form1.Datos.cs`, `Form1.SinPausa.cs`, `GraveMono.cs`, `LoQueSuena.cs`, `LocalAuto.cs`, `SalaLocal.cs`, `VolumenParejo.cs` | tal cual (sin nada de Windows en lo nuevo; se juntaron solos) |
| `Form1.Sonido.cs` | tal cual, salvo `OpenAudio`: `NoEsMp3.Rechaza(path)` y su comentario van ANTES de la línea del gancho g13 (que no cambia): una ruta `spotify:track:` no acaba en `.mp3` y `Rechaza` no la mira |
| `Form1.Ajustes.cs` | once choques: lo nuevo de XP con `Label -> Rotulo`, `ContentAlignment -> AlineaTexto`, `Panel -> Grupo`, los `AccessibleName`/`AccessibleRole` comentados (MSAA), `TextRenderer.MeasureText -> TextoGdi.Mide`, `foreach (Control ...)` con `IPieza` y `.Caja.Visible` (más `pEq`), y en el `finally` `EqDuenoSinTermina()` delante de `PantallaRocola.EscondeFlecha()` (el `Cursor.Hide()` de XP). La hoja del ecualizador entera traducida igual |
| `EcualizadorDueno.cs`, `NoEsMp3.cs` (nuevos) | tal cual (la etapa hereda de `EtapaPorCuadros`, que ya da la lectura de NAudio 3) |
| `Form1.EcualizadorDueno.cs` (nuevo) | `CurvaEqView` deja de heredar de `Control`: pieza de la hoja (`IPieza`, como el deslizador) pintada con SkiaSharp y `Nativo/` (`PinturaXp`, `Pixel.Rellena/TrazaRect/Linea/Lineas`, `TextoGdiPlus.Mide/Escribe` con el formato de fábrica, `SetClip -> ClipRect` en un `Save`, `Font.Height -> AltoEntero`); lo de `Control` que usa, en `Nativo/PiezasAjustes.cs` |
| `SalidaPropia.cs` | ver abajo |

Los finales de línea de los tres nuevos, como en XP (LF): `Codigo/` sigue los de `Rocola/` archivo por archivo (en `Rocola/` y en
`Codigo/` 96 de los archivos van con LF), no CRLF.

`diff-xp.py --sin-ganchos` en los 112 archivos: 5 135 líneas quitadas, 4 868 puestas y **229 sin marca, las mismas de antes** (eran
5 095 / 4 828 / 229 en los 109): las 40 de más llevan todas su «EN LINUX». Por archivo: Form1.Ajustes.cs +6/+6, SalidaPropia.cs +4/+4,
Form1.EcualizadorDueno.cs 30/30 (todas con marca), los demás sin cambio en las cuentas.

## SalidaPropia: la entrada de 10 ms en la salida ALSA

En rocola-xp `SalidaPropia.cs` ya es la salida de ALSA (la `WaveOutPropio` de XP con la tarjeta de `Nativo/SalidaAlsa.cs`; lo que en XP
contesta Windows se calcula en `Nativo/SalidaPropiaAlsa.cs`), así que la entrada va ahí, línea por línea como en XP. Lo único distinto es
cómo se sabe que «ningún otro bloque sigue sonando»: en XP es la bandera WHDR_DONE de cada WAVEHDR; aquí, como el resto del archivo, que lo
sonado (`Sonado()`, mirado después de `Llena`) pasó de donde acaba cada bloque (`finDe`). La rampa se aplica en `lectura` y se copia al
arreglo del bloque (en XP, `Marshal.Copy` a su memoria).

Dos arreglos de la traducción, encontrados al probarlo con la tarjeta de prueba con reloj (`Pruebas/Windows/alsa/reloj.c`):
- `Nativo/SalidaAlsa.cs`, `Sonado()`: si la tarjeta está en XRUN también cuenta todo como sonado aunque `snd_pcm_delay` conteste 0 (los
  plugins de ioplug, como el de prueba, cambian el estado al ver el XRUN pero devuelven lo de antes; la tarjeta de verdad contesta -EPIPE).
  Sin esto el primer bloque tras el corte entraba sin rampa.
- `Codigo/SalidaPropia.cs`, el «aún sonando» de cada bloque se mira en ese momento (`finDe[i] > (ya = Sonado())`), como la bandera que pone
  Windows: con el `ya` de antes del atasco, tras un corte se escribía un solo bloque y se esperaba a que sonara, y la tarjeta se quedaba
  muda OTRA VEZ 50 ms después (dos XRUN por atasco; lo hacía también la rocola-xp de antes, `junta-spotify`). La pausa no cuenta como corte
  si la tarjeta sabe pausar (`snd_pcm_pause`); XP nunca pausa la música.

## Rocola OS

- Ganchos: g06 y g20 (Form1.Datos.cs) bajan 12 líneas, g13, g23 y g61 (Form1.Sonido.cs) 56, 46 y 46: `GANCHOS.tsv` al día. La BASE de
  `ganchos.py` y de `nivel0.sh` pasa de fc7b667 a **69e6547**: un commit con `Codigo/` (y las dos líneas de fuera, g21 y g22) SIN los
  ganchos y con los cambios de XP, lo que `ganchos.py` pide para «si cambia Rocola/ y se pasa a Codigo/, la nueva». Si la rama se rebasa,
  ese número cambia: `--base` / `BASE=`.
- `avisos-base.txt`: los mismos 13 avisos; los de Form1.Datos.cs y Form1.Sonido.cs, 12 y 56 líneas más abajo.
- El modo noche con Spotify (escena «noche» de `Pruebas/RocolaOs/spotify-xp/prueba.sh`): medía la rampa de 20 s al cruzar las 23 h y
  «Probar» de 15 s que volvía sola. Ahora, seis corridas: (1) cruzando las **22 h** (la zona de mentira a las 21:59:30), la pendiente del
  nivel antes (0) y después (**-0,0537 dB/min**, -3,22 dB en 60 min) y el mayor cambio entre tramos de 100 ms (sin escalón); (2) prendida
  **ya de noche** (23:30): desde el principio al 80 % de lo de día; (3) «Probar» tocado otra vez a los 15 s: baja en 0,93 s, se queda hasta
  el otro toque y vuelve. Siempre la de Spotify contra la de XP. `niveles.py` gana `--cruce EPOCA` (cruce, pend_antes, pend_despues,
  salto) y la clave `mediana`.
- Con el modo Spotify, el ecualizador del dueño también va en la cadena de las de Spotify (la misma `ArmaCadena`); su margen (E3), con la
  huella de tono de la canción: una de Spotify no la tiene y usa lo máximo de la curva, como una de XP sin medir.

## Pruebas (20-11-2026, en la máquina de 4 núcleos compartida)

| Qué | Resultado |
|---|---|
| Compilar JIT (`dotnet build -c Release`; y `nivel0.sh` con `PASOS="codigo ganchos compila"`: los cuatro BIEN) | 0 errores, los 13 avisos de `avisos-base.txt` (0 nuevos) |
| Publicar NativeAOT (`-p:PublishAot=true`) | sale con 0, los mismos 13 avisos, 0 de IL/AOT |
| `ganchos.py` | TODO BIEN (49 ganchos en 19 archivos; 112 archivos comparados con 69e6547, 17 con ganchos) |
| Baterías de XP sobre rocola-xp.dll (JIT) | noche 65/65, billar18 35/35, billarparejo 8/8, ecualizador 77/77, nomp3 23/23, localauto 63/63, cadenabillar 92/92 |
| compara-cadena (la rocola-xp de antes contra la nueva) | 10 iguales bit a bit, 6 distintas esperadas (las del modo billar), 0 inesperadas |
| La salida ALSA con un atasco de 1,2 s (tono de 997 Hz, 16 bits) | sin atasco: 0 entradas, lo sonado igual al tono bit a bit; con atasco: 1 XRUN (la de antes, 2), 1 entrada, ganancia 0,03 / 0,50 / 0,97 en 0-2 / 4-6 / 8-10 ms y después igual al tono (438 cuadros distintos, los de la rampa) |
| spotify-xp, escena «noche», JIT y NativeAOT | TODO BIEN (28 y 28): pendiente después de las 22 h -0,054 / -0,0536 dB/min (sp / xp), antes 0; salto 0,0001-0,0005 dB; ya de noche -3,24 / -3,22 dB; «Probar» 14,9 s abajo y vuelve en 0,9 s |

Las baterías de XP se compilaron fuera del repositorio con .NET 10 (`Assembly.LoadFrom` de rocola-xp.dll; la lectura con `Span` de NAudio 3
añadida a sus fuentes de prueba y `../Rocola` apuntando a `Codigo/` para las que leen el código); compara-cadena carga cada programa en su
propio `AssemblyLoadContext`. `salidapropia.cs` de XP (un winmm imitado) no aplica: en su lugar, la prueba de la tarjeta con reloj de
arriba. `ventanaf3.cs` es de WinForms: no se corrió.

No se corrió `nivel0.sh` entero: la copia congelada de la referencia (aef2211) es la XP de antes, y Ctrl+F3 -> Sonido (el modo noche con
sus graves, el botón del ecualizador, sin «Presencia nueva o antes» ni I3) y el sonido de fábrica a partir de las 22 h ya no son los suyos
a propósito. Nada probado en el A4.

## Lo de Linux (20-11-2026)

Rama `xp-1718-linux`, sobre `claude/modest-carson-fa2v2n` (424ade7) con d44473f. Lo que faltaba del paso: la importación de XP con lo
nuevo, los «MP3» que no lo son con lo que Linux tiene, las baterías de XP otra vez y la hoja del ecualizador con el arnés del port. **No**
se hizo lo de los videos (el ecualizador del dueño y los graves de noche como filtro de mpv): es una propuesta que decide el dueño.

### 1. La importación de XP (`Nativo/ImportaXp.cs`)

- d44473f (revisado): si la configuración de XP no trae `parejo_v2=1`, su `rocola_billar_nivel.txt` no se importa y la marca de la
  importación lo dice; la carpeta de XP no se escribe. Bien, pero al juntar las configuraciones quedaban dos huecos:
  - una **rocola-xp de antes del 18-11** (su propia tabla, aprendida con la presencia, sin la marca) y una XP **con** la marca: la tabla de
    rocola-xp se queda («no se tocan») y `JuntaConfig(ya, cfg)` le añadía el `parejo_v2=1` de XP: al abrir, el código de XP ya no la
    apartaba;
  - una re-importación de una XP con `parejo_v2=0` escrito (manda la de XP) le quitaba la marca a la tabla de rocola-xp, que se apartaba
    sin motivo.
- Arreglo: **la marca va con la tabla que quede** (no es una preferencia, es de `rocola_billar_nivel.txt`). Si la tabla de XP va a su
  sitio (no había, re-importación, copia del lanzador, o la de rocola-xp sin ninguna canción), `parejo_v2=1`; si se queda la de rocola-xp
  (o ninguna), la marca que tenía rocola-xp, y si no tenía, ninguna (rocola-xp aparta lo suyo al cargar, como XP). Una sola línea, al final;
  la marca de la importación dice cuál quedó (`parejo_v2=1 (con la tabla de XP)` o `(la de rocola-xp)`).
- `eq_dueno=`, `eq_dueno_billar=`, `noche_graves=`, `noche_desde=` y `noche_desde_22=` pasan tal cual (las lee el código de XP).
  `dsee_genero=` también se copia (es una línea más), rocola-xp la ignora (`LoadConfigPrefs`: `{ }`) y al guardar ya no la escribe.
- Prueba (`Pruebas/Importa`, parte `importa`, JIT): **147 de 147**. Lo nuevo: la configuración de XP del mundo falso con las seis claves
  (y `noche_desde=23` puesta a mano después del paso a las 22: se respeta), y cinco casos del volumen parejo con rocola-xp abierta
  después: (a) XP sin la marca a una rocola-xp nueva: no se importa la tabla, sin marca, rocola-xp la pone al abrir, sin nada que apartar,
  `dsee_genero=` fuera, el ecualizador, los graves de noche y las 23 h iguales, la carpeta de XP sin tocar; (b) rocola-xp vieja sin la
  marca y XP con ella: su tabla no se toca, sin marca, y al abrir se aparta en `rocola_billar_nivel.antes-2026-11-18.txt` (la misma) y
  pone la marca; (c) rocola-xp con la marca y XP sin ella: la de XP no entra, la suya y su marca se quedan, y al abrir no se aparta nada;
  (d) re-importación de una XP con `parejo_v2=0`: la tabla y la marca `1` de rocola-xp se quedan; (e) re-importación de la XP con la
  marca: su tabla entra (la de rocola-xp a `.antes-xp`) con `parejo_v2=1`. Las partes `usa` y `material` no cambian (no se volvieron a
  correr: ver lo pendiente).

### 2. Los «MP3» que no lo son, convertidos (función «convierte»)

La decisión del 4-11-2026 (AGENTS.md) encima de lo de XP del 17-11-2026: `RocolaOs/RocolaOs.Convierte.cs`, encendida de fábrica
(`convierte=0` en `rocola_os.txt` la apaga), con `NoEsMp3.OtroFormato` de detector y la lógica de `Motor/Conversiones.cs` (no se tocó).
`Nativo/LectorAudio.cs` y `Nativo/MedidorVideo.cs` ya usan ffmpeg por tubería, pero para otra cosa (descomprimir un WAV comprimido,
medir un video): la conversión es un proceso aparte que escribe un archivo, como en el programa anterior.

- **Qué hace**: ffmpeg (`ROCOLA_FFMPEG` para las pruebas) pasa el archivo a WAV PCM de 16 bits con su frecuencia y sus canales, de uno en
  uno en un hilo de fondo (nice 19). **El original no se toca**: la copia va a `ROCOLA_CONVERTIDAS`, con un nombre sacado de la ruta, el
  tamaño y la fecha de escritura (si el archivo cambia, otra copia), escrita como `.haciendo.wav` y cambiada de nombre al acabar (las a
  medias de un apagón se borran al arrancar). Sin ffmpeg, con menos de 1 GB libre (más 16 veces el original) o si ffmpeg falla: lo de XP.
- **Cómo suena como la canción de la rocola**: la canción sigue siendo la ruta del `.mp3` original para todo (la cola, el estado, lo más
  sonado, las estadísticas, lo aprendido, lo que se ve); solo cambia **qué archivo se lee**, en los dos sitios donde XP abre el sonido:
  - **g110** (`Codigo/Form1.Sonido.cs:1638`, antes 1511: se movió en la junta 3 al juntar noche-nv, `OpenAudio`, antes del rechazo de XP): `RocolaOsConvertidaAbre` arma el lector como XP
    (`SinSilencios`, `SellaFuenteAudio` con el sello **del original**, `ComoEstereo`, `FileAudioSource`) sobre el WAV, sin `SinRelleno`
    (el hueco del codificador es de los MP3). Si aún no está, se pide y se espera hasta **20 s** (en el hilo de la apertura; el medidor de
    Ctrl+F3, que abre en la pantalla, no espera). Si tarda más, un `IOException` «se esta convirtiendo»: por `AudioStartFailed` el crédito
    se devuelve y, como es de E/S (`EsArchivoDanado`), **no queda dañada**: la próxima vez suena. Sin ffmpeg, sin sitio o fallida: null.
  - **g111** (`Codigo/Form1.Sonido.cs:742`, antes 621: se movió en la junta 3, `LectorMedida`: el repaso, la duración, la tanda larga): `RocolaOsConvertidaMide` da la ruta
    del WAV (el mismo sonido; la medida se guarda bajo la ruta del original, la de quien mide). Si no está, se pide y ese hilo de fondo
    espera hasta 60 s; si no llega, la ruta original y XP la rechaza. Así el repaso la convierte en cuanto la encuentra.
  - Sin `ROCOLA_OS` o con la función apagada, los dos ganchos devuelven lo de XP sin leer nada; un MP3 de verdad (y el MP3 dentro de un
    WAV, 0x55) tampoco cambia.
- **El lanzador** (`/usr/bin/rocola`, BusyBox, sin `stat`): `conv_xp`, como `ia_xp`: `ROCOLA_CONVERTIDAS=$DISCO/rocola-os/convertidas`
  solo con el disco de la rocola montado con escritura (comprobado con un tmpfs en escritura y en solo lectura; no con el BusyBox del
  sistema, que no está en esta máquina). Sin ella, `convertidas` en la carpeta de datos (datos.img: sin 1 GB libre, lo de XP).
- Ganchos: g110 y g111 en `GANCHOS.tsv`; `ganchos.py` TODO BIEN (51 en 19 archivos); `diff-xp.py --sin-ganchos` en los 112: las mismas
  5 135 / 4 868 / 229 de antes (los dos ganchos se devuelven).
- Prueba nueva `Pruebas/RocolaOs/convierte/prueba.sh` (rocola-xp de verdad fuera de pantalla, la tarjeta con reloj para saber qué sonó, el
  doble «calidad-prueba» para pedir; música inventada con el ffmpeg de esta máquina): **41 de 41 en JIT** (la primera corrida, 4 MAL de
  la prueba misma: funciones de bash fuera de `bash -c` y la canción buena pedida otra vez al final). Escenas: `convierte` (M4A, OGG,
  FLAC y WAV con nombre .mp3: cada uno cobrado y oído entero, 6,00-6,05 s de 6, desde su WAV; el MP3 dentro de un WAV y el MP3 de verdad
  como siempre; ningún crédito devuelto; cuatro copias PCM de 16 bits, hechas en 0,3-0,8 s; originales iguales por sha256, tamaño y
  fecha; nada en `rocola_incompatibles.txt`; lo más sonado y `rocola_volumen.txt` por la ruta `.mp3`, nunca la del WAV), `xp` (sin la
  función: no suenan, créditos devueltos, dañada la segunda vez, `rocola_incompatibles.txt`, ninguna copia), `lenta` (un ffmpeg que tarda
  35 s: cobrada, a los 31,5 s ya no está y el crédito volvió, el motivo en `rocola_error.txt`, la copia llega a los 35,1 s, no quedó dañada
  y la segunda vez se cobra y suena entera), `sinffmpeg` y `sinsitio` (un tmpfs de 40 MB: lo de XP, con el motivo en la bitácora) y
  `sinrocolaos` (ni copias ni nada de Rocola OS; el repaso no la mide, como XP).
- ~~Lo que no es como quisiera~~ (arreglado en la revisión del 21-11, abajo): en el caso lento, `NoteIncompatible` apuntaba la canción
  con «se esta convirtiendo»; las copias viejas no se borraban; la espera del repaso no miraba la cancelación.

#### Revisión del 21-11-2026 (lo que encontró el revisor, comprobado uno por uno)

- **La carrera de los 20 s** (real, por lectura): `Espera` miraba `Lista` (null) y después `Convirtiendo` (false) y devolvía null si la
  conversión acababa justo entre las dos; `RocolaOsConvertidaAbre` pasaba entonces a lo de XP y la marcaba dañada con su WAV ya hecho.
  Ahora, si ya no se está convirtiendo, `Espera` vuelve a mirar `Lista` (el `File.Move` va antes de soltar la clave), y `Abre`, tras el
  plazo, también.
- **La copia al disco antes del cambio de nombre**: `Flush(true)` (fsync) del `.haciendo.wav` antes del `File.Move` y fsync de la carpeta
  después. Y si la copia no se deja abrir en g110 (un apagón a media escritura, o la borraron a medias), se borra y se pide otra (una vez
  por sesión; la segunda vez, fallida y lo de XP): el crédito se devuelve como «se está convirtiendo», sin marcarla dañada.
- **Ganchos nuevos g112 y g113** (`GANCHOS.tsv`; `ganchos.py` TODO BIEN, 53 en 19 archivos): la pedida que aún se está convirtiendo lanza
  `RocolaOsConvirtiendo` (un `IOException`, así que no queda dañada); g112 (`AudioStartFailed`, `AperturaAudio.cs:321`) no la anota en
  `rocola_incompatibles.txt` (XP la anotaba una vez para siempre: si después fallaba de verdad, el «no es un MP3» ya no se apuntaba) y g113
  (`MotivoFallo`, `Form1.cs:2222`) dice «se está preparando: sonará la próxima vez» en vez de «no se pudo leer el archivo del disco». Sin
  `ROCOLA_OS` ese error nunca se lanza: XP bit a bit.
- **La espera del repaso** (g111) se corta con la cancelación que ya trae ese hilo (`MeasurementCancelled`: el repaso que se para, el
  precalentador que cambia de canción). Sigue esperando hasta 60 s con el turno de esa unidad tomado mientras nadie la corta (no se
  cambió a «pedirla y medirla en otra vuelta»: con muchas así el repaso va de una en una, pero ffmpeg las hace en segundos).
- **Más de dos canales**: `-af aformat=channel_layouts=mono|stereo` (comprobado con el ffmpeg de esta máquina: un FLAC 5.1 sale con 2
  canales; mono sigue mono y un estéreo sale **idéntico byte a byte** con y sin el filtro).
- **Las copias viejas**: junto a cada copia, una nota `.txt` con la ruta del original; al arrancar (`LimpiaRestos`) se borran las de un
  original que cambió (otra clave) o que ya no está en su carpeta; si ni la carpeta está (un disco sin conectar), se queda; sin nota, no se
  toca.
- **`LeeMp3`** (`RocolaOs.CalidadReglas.cs`): de un «MP3» que es otro formato ya no se busca el cuadro (un sincronismo falso daba Hz y kbps
  al azar); `EsMp3` queda en false y para la regla A solo cuenta el corte medido (del WAV).
- `sistema/INTEGRACION_ROCOLA_XP.md` dice que `ROCOLA_CONVERTIDAS` volvió para `rocola-xp`.
- Prueba: escenas nuevas `atiempo` (un ffmpeg de 12 s, pedida mientras se convierte: se espera, suena entera, cobrada una vez, nada
  devuelto ni anotado, la nota junto a la copia) y `rota` (la copia es basura: se tira y se rehace, el crédito vuelve sin marcarla dañada
  y a la segunda suena; al arrancar se borran la de un original que cambió y la de uno que ya no está, y se quedan la de un disco que no
  está y la que no tiene nota); `lenta` comprueba además que no queda en `rocola_incompatibles.txt`. Resultado: **57 de 57 en JIT y 57
  de 57 en NativeAOT** (atiempo: WAV listo en 12,5 s, cobrada una vez, 6,00 s oídos de 6; rota: «Not a WAVE file», otra copia en 1,9 s,
  crédito devuelto y a la segunda 6,00 s). La sustitución (`Pruebas/RocolaOs/sustitucion/prueba.sh`, por `LeeMp3`), JIT: **97 de 97**.
  Compila JIT y NativeAOT con los mismos 13 avisos (0 de IL/AOT); `ganchos.py` TODO BIEN (53 en 19 archivos); `diff-xp.py --sin-ganchos`
  las mismas 5 135 / 4 868 / 229.
- **Lo que no se cambió**, y por qué: (1) las medidas viejas hechas sobre la basura de un archivo leído como MP3 antes del 17-11: si
  existen se importan y se aplican a la convertida (solo se tiran si la duración difiere más de 1 s); no comprobado si alguna casa, y
  la XP de antes casi siempre fallaba al abrirlas (`BuildCleanMp3` sin cuadros útiles devuelve null), así que no se tocó. (2)
  `audioOpeningForMeasurement` leído desde otros hilos: el medidor de Ctrl+F3 para lo que suena; no se ve cómo coincidiría. (3) La
  re-importación de una XP sin `parejo_v2=1` sobre una tabla de rocola-xp con la marca: se quedan la tabla y la marca (ya se aprendió sin
  la presencia): **lo decide el dueño**. (4) Mientras se espera hasta 20 s en el hilo de la apertura no se abre otra: es lo de la decisión
  del 4-11; no medido cómo se ve. (5) De E1 la batería sigue probando solo «otro toque».

### 3. Las baterías de XP y la hoja del ecualizador

- Las cinco baterías de XP del paso, compiladas fuera del repositorio con .NET 10 (`/root/rocola-trabajo/fase11/xp-1718-linux/baterias`:
  `NAudio.Core` 3.3.0, la lectura con `Span` de NAudio 3 añadida a sus fuentes de prueba y una de arreglos para las que leen, `leercodigo.cs`
  al lado, `../Rocola` apuntando a `Codigo/`; `billar18.cs` y `billarparejo.cs` cargaban con `Assembly.Load(bytes)`, sin sitio para sus
  dependencias: `LoadFrom`), contra el rocola-xp.dll de esta rama (JIT): **ecualizador 77/77, noche 65/65, nomp3 23/23, billarparejo
  8/8, billar18 35/35**.
- La hoja «Ecualizador...» con el arnés del port de Ctrl+F3 (`ventanaf3.cs` es de WinForms y no se puede correr): escena nueva
  `f3ecualizador` (`Pruebas/Ventanas/guiones/escenas.sh`, sus pasos en `vistas_f3.py`: el ratón por Ajustes del sonido, la hoja, la banda
  de 31 Hz al máximo, la de 16 kHz al mínimo, «Sin ecualizador» dos veces, «Todo en 0», la de 500 Hz a +6 y volver) y su comprobación
  `bateria_eq.py` sobre los volcados de cada pieza y las capturas de la hoja (la referencia aef2211 no tiene esa hoja: no se compara punto
  por punto). JIT: **23 de 23**: el botón en Ajustes del sonido; la hoja con sus 10 deslizadores, nombres y «0 dB», «Todo en 0», «Sin
  ecualizador», la memoria en uso, el margen y la curva; cada banda movida con su valor y su letrero, el margen y el dibujo de la curva
  cambiado (2 359 y 371 puntos de su caja); «Ponerlo (15 s)» y vuelta; «Todo en 0» deja el dibujo como al abrir (0 puntos); lo guardado
  (`eq_dueno=0.0;0.0;0.0;0.0;6.0;...`, `eq_dueno_billar=` en 0); y las tres de sitio de `ventanaf3.cs` (`bateria_f3.py`): 1 278
  comprobaciones en 9 vistas.
- Compila JIT: 0 errores, los 13 avisos de `avisos-base.txt`. NativeAOT (`dotnet publish -r linux-x64 -p:PublishAot=true`): sale con 0,
  los mismos 13, 0 de IL/AOT.

## El modo noche de 50 a 90 % (20-11-2026)

Rama `noche-50`, sobre la rama principal (4d03d6c). El cambio de XP (`687af49`; AGENTS.md, la entrada del 20-11-2026;
`AUDIO_NOCHE_Y_MEJORAS_XP_MODEST_2026-11-17.md`, sección 4 ter): `NochePctMin` 50 (50 % = −10 dB con la misma cuenta), un `noche_pct=`
guardado entre 50 y 90 se respeta y fuera se acota, y la rampa (`AvanzaRampa`) con el paso mayor entre medio volumen y lo que baja la
noche (de 66 a 90 % el de antes, bit a bit; de 50 a 65 % la noche entera sigue en 20 s y en 1,5 s).

- Pasado a `Codigo/` línea por línea: las mismas seis líneas (cinco en `Form1.Sonido.cs`, un comentario en `Form1.Ajustes.cs`), en su
  sitio y con su fin de línea (CRLF), sin nada que traducir. Ninguna línea se añade ni se quita: los ganchos y los avisos no se mueven
  (`GANCHOS.tsv` y `avisos-base.txt` sin cambio de líneas).
- La base de los ganchos pasa de 69e6547 a **ebdb272**: 69e6547 con esas seis líneas (`Codigo/` sin los ganchos; g21 y g22, de
  `Nativo/`, con su línea de antes). El commit siguiente (`effbc05`) vuelve a poner los 49 ganchos. La nombran `ganchos.py`, `nivel0.sh`,
  `GANCHOS.tsv` y `Pruebas/RocolaOs/LEEME.md`. Si la rama se rebasa, ese número cambia: `--base` / `BASE=`.
- Los videos en rocola-xp: no hay un filtro de mpv para la noche (el `@noche` era del programa retirado, `Consola`). Bajan por la cuenta de
  XP (`VideoVolumeFor` → `FactorVideoNoche`: la perilla al pct %), y `Nativo/ReproductorVideo.cs` lleva esa perilla, que es de amplitud,
  al volumen cúbico de mpv. Al 50 %: perilla 50, mpv 79,37, amplitud 0,5 (−6 dB; la música, −10 dB). Ya era así al 80 % (−1,9 contra
  −3,2 dB); no cambió.
- `Nativo/ImportaXp.cs` copia `noche_pct=` tal cual (solo traduce `dir=` y quita `respaldo_dir=`); rocola-xp lo acota al leerlo, a 50-90.
  `Pruebas/Importa` lleva ahora `noche_pct=50` en la XP falsa.
- La escena «noche» de `Pruebas/RocolaOs/spotify-xp/prueba.sh` medía solo el 80 % de fábrica; ahora, dos corridas más con
  `noche_pct=50` (en un `rocola_config.txt` de los datos), con la de Spotify y con la de XP: prendida ya de noche y «Probar».
- Ctrl+F3 → Sonido, el mismo 80 % con el rango nuevo: comparada la captura de rocola-xp de antes (la de este documento, arriba) con la
  nueva (`corre.sh`, Ctrl+F3 a los 26 s), dentro de la ventana solo cambian 917 puntos, los del deslizador del modo noche (el botón pasa
  de la mitad a tres cuartos del riel).

### Pruebas (20-11-2026, en la máquina de 4 núcleos compartida y muy cargada)

| Qué | Resultado |
|---|---|
| Baterías de XP con Mono (`pruebas/todo.sh`) | `noche` 86/86 (antes 65; contra la rocola de antes, 12 fallos); en verde también `ajustes`, `ajustesvivos`, `ventanaf3`, `pestanas`, `sala`, `salalocal`, `compatvieja`, `videos`, `detalles`, `moneda`, `arreglos` y `cadenaentera`; la rocola compila a 0 avisos en los dos perfiles |
| Compilar JIT | 0 errores, los 13 avisos de `avisos-base.txt` (0 nuevos) |
| `ganchos.py` | TODO BIEN (49 ganchos en 19 archivos; 112 archivos comparados con ebdb272, 17 con ganchos) |
| `clasifica.py codigo` (el paso `codigo` de `nivel0.sh`) | 112 archivos con las mismas líneas, CRLF y último byte que en ebdb272 |
| `diff-xp.py --sin-ganchos` | 5 135 quitadas, 4 868 puestas, 229 sin marca: las mismas de antes (las seis líneas son iguales en `Rocola/` y `Codigo/`) |
| Batería `noche` de XP sobre rocola-xp.dll (JIT) | 86/86 |
| Publicar NativeAOT (`-p:PublishAot=true`, el código de `effbc05`) | sale con 0, los 13 avisos de `avisos-base.txt` (0 nuevos), 0 de IL/AOT |
| `Pruebas/Importa` (JIT) | TODO BIEN (145) |
| spotify-xp, escena «noche» (JIT) | las 14 nuevas del 50 % BIEN: prendida ya de noche −10,02 dB (Spotify) y −10,00 (XP), quieta; «Probar» −10,02 y −10,00 dB, rampa 1,5 s, 14,5 y 14,4 s abajo y vuelve en 1,4 y 1,5 s; Spotify contra XP, 0,015 y 0,02 dB. Las de XP al 80 % BIEN (−0,0536 dB/min, salto 0,0005 dB, −3,22 dB, rampa 0,9 s). 8 MAL en las dos corridas de Spotify al 80 % y sus cruces: huecos de la fuente falsa con la máquina cargada (tramos a −17 y −24 dB a los 3-7 s y a los 32-36 s, sin XRUN en la tarjeta); en «Probar» la bajada sí estuvo (−3,24 dB de los 12 a los 26 s), pero el análisis tomó el hueco de los 3 s por su principio |
| spotify-xp, escena «noche» (NativeAOT) | 40 de 44: las del 50 % BIEN (prendida ya de noche −10,02 / −10,00 dB, quieta; «Probar» −10,02 / −10,00 dB, rampa 1,5 s, 14,5 y 14,6 s abajo, vuelve en 1,5 y 1,4 s; Spotify contra XP 0,015 y 0,02 dB); las de XP al 80 % BIEN (−0,0536 dB/min, salto 0,0005 dB, −3,22 dB, «Probar» 0,9 s); «yanoche» y «probar» de Spotify al 80 % BIEN esta vez (−3,24 dB, 0,9 s). 4 MAL, todas de la corrida de Spotify que cruza las 22 h: huecos de la fuente falsa (tramos a −19/−22 dB a los 7, 10, 27, 42-47 s), que dan 0,2586 dB/min antes de las 22 h, −0,0768 después y un salto de 7,1 dB |
| spotify-xp, escena «noche», otra vez (JIT, 20:30-20:45) | **TODO BIEN (44)**: «noche» de Spotify cruzando las 22 h −0,0539 dB/min, antes −0,0, salto 0,0002 dB; las del 50 % −10,02 / −10,00 dB, rampa 1,4 y 1,5 s. Justo antes, con la misma máquina, la rocola-xp de ANTES de este cambio (`xp-1718`, 70 a 90 %) en la misma escena: 11 MAL, 3 de ellas la MISMA corrida de Spotify al 80 % con el mismo hueco (0,0541 dB/min antes de las 22 h, salto 7,7 dB): ese fallo no viene de este cambio; las otras 8, las del 50 % que la de antes acota a 70 (−5,15 dB en lugar de −10) y una «Probar» al 50 % de Spotify sin bajada medida (0,0 dB; no se miró por qué) |
| `pruebas/noche.cs` con Mono, otra vez (`todo.sh noche`, la rocola recompilada a 0 avisos en los dos perfiles) | TODO BIEN (86 comprobaciones) |

### Arreglo de la revisión (21-11-2026): la subida de las 7 en 20 s también al 50 %

La revisión encontró que al 50 % la subida de las 7 tardaba unos 23 s (la rampa, con su tope, se quedaba atrás de la curva del avance al
final) y el deslizador de noche de 50 a 90 %, 21 s. Arreglado en `Rocola/Form1.Sonido.cs` y pasado a `Codigo/` (las mismas tres líneas,
ninguna añadida): si el volumen ya va por la curva del avance (no «Probar» ni su vuelta), la sigue sin rampa; y el paso de la rampa
cuenta desde lo más bajo de la noche, de donde está o de a donde va (de 66 a 90 %, el de antes bit a bit). Detalle en
`AUDIO_NOCHE_Y_MEJORAS_XP_MODEST_2026-11-17.md`, sección 4 ter.

- La base de los ganchos pasa de ebdb272 a **d6026b1** (ebdb272 con esas tres líneas; el commit siguiente, `5d81f3b`, vuelve a poner los
  49 ganchos). Es un commit interno de la rama: si se junta con squash o rebase, se rehace una vez en la rama final (`--base` / `BASE=`).
- `nivel0.sh`, AJUSTES (545): `VistasAjustes` mide los anchos de texto, los formatos y los avisos de `Form1.Ajustes.cs` (no pinta la hoja
  Sonido ni el deslizador), así que el rango nuevo no le cambia nada; los 917 puntos del deslizador son de la captura de `corre.sh`. No se
  corrió el paso «arneses».

| Qué | Resultado |
|---|---|
| `pruebas/todo.sh` con Mono (la rocola a 0 avisos en los dos perfiles) | `noche` 92/92 (la subida al 50 % en 20,0 s; el deslizador de 50 a 90 % en 20,0 s y de vuelta en 15,5; apagar el interruptor 20,0 s); en verde `ajustes`, `ajustesvivos`, `ventanaf3`, `pestanas`, `sala`, `salalocal`, `compatvieja`, `videos`, `detalles`, `moneda`, `arreglos` y `cadenaentera` |
| Compilar JIT | 0 errores, 13 avisos (los de `avisos-base.txt`; ninguna línea se movió) |
| `ganchos.py` | TODO BIEN (112 archivos comparados con d6026b1, 17 con ganchos) |
| `clasifica.py codigo` | 112 archivos con las mismas líneas, CRLF y último byte que en d6026b1 |
| `diff-xp.py --sin-ganchos` | 5 135 quitadas, 4 868 puestas, 229 sin marca: las mismas de antes |
| Batería `noche` de XP sobre rocola-xp.dll (JIT) | 92/92 |
| NativeAOT y la escena «noche» de spotify-xp | no se volvieron a correr (disco casi lleno): el cambio es solo la cuenta de la rampa, que la batería mide en JIT |
