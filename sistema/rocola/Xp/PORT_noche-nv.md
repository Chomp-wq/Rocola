# rocola-xp: el modo noche para los vecinos (NV3, NV4 y NV5; 20 de noviembre de 2026)

Rama `noche-nv`, sobre `noche-50` (el deslizador del modo noche de 50 a 90 %) con `noche-vecinos` juntada (el documento
`AUDIO_NOCHE_VECINOS_MODEST_2026-11-20.md` y su banco `pruebas/nochevecinos.*`). Decisión del dueño: «Del modo noche haz lo que me
recomiendes» = NV3 (NV1 + NV2), NV4 y NV5 (a y b) de ese documento. Lo de la rocola de XP está en su sección 7 y en la 4 quater de
`AUDIO_NOCHE_Y_MEJORAS_XP_MODEST_2026-11-17.md`; aquí, cómo se pasó a rocola-xp y lo que es solo de Rocola OS (NV5b).

## Lo de XP, pasado a `Codigo/`

Los commits de XP (`Rocola/`: `bcc65be` y `faf0163`) se pasaron con `git merge-file` (lo que ya estaba traducido en `Codigo/`, la XP de
antes —`0f40174`— y la XP nueva), como en `PORT_xp-1718.md`:

| Archivo | Cómo |
|---|---|
| `Form1.Sonido.cs`, `Form1.Datos.cs`, `GraveMono.cs`, `Form1.SinPausa.cs` | tal cual (sin nada de Windows en lo nuevo; se juntaron solos) |
| `Form1.Ajustes.cs` | tres choques: el renglón de «Graves de noche» (ahora el botón «Graves, corte y madrugada...»), la lista de las hojas que no se esconden (`IPieza`, con `pNoche`) y el `catch` del final (con `PantallaRocola.EscondeFlecha()`). Lo nuevo de XP con la traducción de siempre: `Label -> Rotulo`, `Panel -> Grupo`, `ContentAlignment -> AlineaTexto` y los `AccessibleName`/`AccessibleRole` comentados (MSAA): 16 líneas con su «EN LINUX» |
| `Form1.Video.cs` | un choque (todo el bloque del WMP, que en `Codigo/` es el reproductor de libmpv): queda lo de `Codigo/` con el comentario nuevo de XP (NV5a) |

Los finales de línea, como en XP archivo por archivo. `diff-xp.py --sin-ganchos` en los 112 archivos: **5 147 líneas quitadas, 4 880
puestas y 229 sin marca, las mismas 229 de antes** (eran 5 135 / 4 868 / 229): las 12 de más llevan todas su «EN LINUX» (las de la hoja nueva).

## Rocola OS

- **Ganchos**: g06 y g20 (`Form1.Datos.cs`) bajan 14 líneas; g61, g23 y g13 (`Form1.Sonido.cs`) 116, 116 y 122. La BASE de `ganchos.py`
  y de `nivel0.sh` pasa de ebdb272 a **ca9802f**: un commit con `Codigo/` (y las dos líneas de fuera, g21 y g22) SIN los ganchos y con el
  cambio de XP. Si la rama se rebasa, ese número cambia: `--base` / `BASE=`.
- **`avisos-base.txt`**: los mismos 13 avisos; los de `Form1.Datos.cs` 14 líneas y los de `Form1.Sonido.cs` 122 más abajo.
- **NV5b, la función `noche_video`** (`RocolaOs/RocolaOs.NocheVideo.cs`, encendida de fábrica; `noche_video=0` en `rocola_os.txt` la
  apaga) y su **gancho g102** (`Codigo/Form1.Video.cs`, `StartVideoItem`, justo antes de `video.Carga(path)`): al EMPEZAR cada video, el
  filtro de audio de mpv (`af`, por `ReproductorVideo.FiltroAudio`, nuevo en `Nativo/`): `lavfi=[lowshelf=f=120:t=q:w=0.7071:g=-G,
  highpass=f=F:t=q:w=0.5412,highpass=f=F:t=q:w=1.3066]` con los graves de noche de ese momento (G) y el corte equivalente (F: el
  Butterworth de cuarto orden que quita a 0,7 veces el corte lo mismo que la mezcla de la música, `CorteEquivalenteHz`); sin graves ni
  corte (de día), `af` vacío. Nunca a media reproducción (un chasquido); lo que cambie de noche lo toma el siguiente video, y su volumen
  sigue la rampa como en XP. Estos filtros son de fase mínima (mpv no tiene la fase lineal de la cadena): en un video no hay limitador
  detrás que pague los picos. Sin ROCOLA_OS, o con la función apagada, el filtro de mpv ni se toca. Rango de ganchos: g102 (libres g99,
  g103-g119; desde g120, el cielo vivo).
- **Importa**: `Nativo/ImportaXp.cs` no cambia (las claves que no son carpetas pasan tal cual); su prueba lo comprueba con
  `noche_graves=10`, `noche_graves_8=1`, `noche_corte=100`, `noche_madrugada=0`, `noche_madrugada_desde=2` y `noche_madrugada_pct=60`.
- Con el modo Spotify, lo de Spotify pasa por la misma `ArmaCadena`: los graves de noche, el corte y la madrugada le tocan igual.

## Pruebas (20-11-2026, en la máquina de 4 núcleos compartida)

| Qué | Resultado |
|---|---|
| Compilar JIT (`dotnet build -c Release --no-incremental`) | 0 errores, los 13 avisos de `avisos-base.txt` (0 nuevos; `clasifica.py avisos`: BIEN) |
| Publicar NativeAOT (`-p:PublishAot=true`) | sale con 0, los mismos 13 avisos, 0 de IL/AOT |
| `ganchos.py` | TODO BIEN (50 ganchos con g102; 112 archivos comparados con ca9802f, 18 con ganchos) |
| `diff-xp.py --sin-ganchos` (112 archivos) | 5 147 quitadas, 4 880 puestas, 229 sin marca (las mismas 229) |
| Baterías de XP sobre rocola-xp.dll (JIT, .NET 10; las fuentes de prueba con la lectura de NAudio 3, `LoadFrom`, cada rocola-xp de `compara-cadena` en su `AssemblyLoadContext`) | `noche` 136/136, `cadenaentera` 91/91, `billar18` 35/35, `billarparejo` 8/8, `gravemono` 54/55 (el que falla, «leer no reserva memoria», falla igual con la rocola-xp de antes: lo reserva el adaptador de la lectura de NAudio 3 de la prueba) |
| `compara-cadena` (la rocola-xp de antes, de noche-50, contra la nueva; de día) | 16 iguales bit a bit, 0 distintas |
| `Pruebas/RocolaOs/noche-video/prueba.sh` (NV5b), JIT y NativeAOT | 9 de 9 en los dos: sin Rocola OS el filtro ni se toca y el video sale como el archivo (50 Hz − 1 kHz = 9,54 dB); con `noche_video` de noche, `af` puesto y −14,61 dB (el 50 Hz baja 24,15 dB, lo mismo que mpv suelto con ese filtro) con el 1 kHz igual (64,04 / 64,04 dB); de día, `af` vacío y 9,54 dB. (El WAV del plugin `file` de ALSA dice s32 y lleva float32: se lee con `mide.py --float`.) |
| spotify-xp, escena «noche» (con la corrida nueva de la madrugada), JIT y NativeAOT | TODO BIEN (55) en los dos. La madrugada: antes de la 1, la noche entera y quieta (−3,27 / −3,23 dB frente al día, 0,0 dB/min); desde la 1, −0,0326 (Spotify) y −0,0321 / −0,0320 (XP) dB/min (esperado −0,0321), salto 0,0001-0,0004 dB, Spotify contra XP 0,0005-0,0006 dB/min |
| `Pruebas/Importa` (JIT) | TODO BIEN (151; antes 145: las seis claves nuevas tal cual) |
| Ctrl+F3 en rocola-xp (captura fuera de pantalla, `Pruebas/Windows/corre.sh`) | la página de Sonido con el botón «Graves, corte y madrugada...» y su hoja: todo cabe y se lee |

Con Mono, de la rocola de XP: los dos perfiles a 0 avisos; `noche` 136/136 (también con la hora local a las 2 y a las 23),
`compara-cadena` 16 iguales (también a las 23), `cadenaentera` 91, `billar18` 35, `billarparejo` 8, `gravemono` 55, `subgrave` 68,
`todojunto` 13, `transiciones` 46, `ventanaf3` 9 405, `ajustes` 4, `videoaudio` 10.

No se corrió `nivel0.sh` entero (las escenas de Ctrl+F3 → Sonido contra la referencia congelada ya no son las suyas a propósito). Nada
oído ni probado en el A4 ni en el bar.
