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
