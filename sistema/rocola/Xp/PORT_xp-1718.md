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
  - **g110** (`Codigo/Form1.Sonido.cs:1511`, `OpenAudio`, antes del rechazo de XP): `RocolaOsConvertidaAbre` arma el lector como XP
    (`SinSilencios`, `SellaFuenteAudio` con el sello **del original**, `ComoEstereo`, `FileAudioSource`) sobre el WAV, sin `SinRelleno`
    (el hueco del codificador es de los MP3). Si aún no está, se pide y se espera hasta **20 s** (en el hilo de la apertura; el medidor de
    Ctrl+F3, que abre en la pantalla, no espera). Si tarda más, un `IOException` «se esta convirtiendo»: por `AudioStartFailed` el crédito
    se devuelve y, como es de E/S (`EsArchivoDanado`), **no queda dañada**: la próxima vez suena. Sin ffmpeg, sin sitio o fallida: null.
  - **g111** (`Codigo/Form1.Sonido.cs:621`, `LectorMedida`: el repaso, la duración, la tanda larga): `RocolaOsConvertidaMide` da la ruta
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
- Lo que no es como quisiera, dicho: en el caso lento, `NoteIncompatible` de XP también apunta la canción en `rocola_incompatibles.txt`
  con «se esta convirtiendo» (es el camino de siempre de una que no se pudo abrir; no se tocó para no poner otro gancho). Las copias viejas
  (de un archivo que cambió o se borró) no se borran nunca. Mientras el repaso espera una conversión (hasta 60 s) no mide otra cosa.

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
