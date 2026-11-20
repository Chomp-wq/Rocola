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

PRUEBAS_AQUI
