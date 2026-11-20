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
