# Fase 11: las juntas

Cada tanda de la fase 11 se junta en una rama propia y se comprueba entera antes de llegar a la rama principal (`PLAN_FASE11.md` §0 y
§2.6). Aquí va un resumen corto de cada junta; el detalle de cada grupo, en su `FASE11_<grupo>.md`.

## Junta 1: la de Spotify para el cumpleaños (15 y 20-11-2026)

Rama `junta-spotify`, juntada en la rama principal en `4ee693f` y publicada en sistema-38. Su detalle (lo que entró, los choques y las
tablas de pruebas) está en `SPOTIFY_JUNTA_PENDIENTES.md` («La junta», «La junta final» y «La junta del 20-11-2026»).

## Junta 2: la segunda compilación (20-11-2026)

Rama `junta-2`, sobre la rama principal `claude/modest-carson-fa2v2n` en `424ade7`. Merges con `--no-ff` (sin rebase), en este orden:

| # | Rama | Hasta | Lo que trae | Documento |
|---|---|---|---|---|
| 1 | `catalogo-generos` | `8090912` | El pre-mapeo de los nombres de género de Wikidata y MusicBrainz antes de `GenreLabel` (`sistema/catalogo/arma_catalogo.py`; el catálogo de prueba rehecho: 187 artistas, 673 discos) | `sistema/catalogo/LEEME.md` |
| 2 | `pantalla-ctrlf4` | `2bcf38e` | La ventana de Ctrl+F4 con la hoja «Pantalla» (resolución, hercios, colores del monitor; `rocola-pantalla`) | `FASE11_pantalla.md` |
| 3 | `navegador-archivos` | `977c5bd` | «Navegador» (WPE WebKit con cog, paquetes del defconfig: pide la compilación COMPLETA) y «Archivos» en Ctrl+F4 | `FASE11_navegador-archivos.md` |
| 4 | `pared-generos` | `b6af360` | La pared con el catálogo: el orden de XP con los discos del catálogo intercalados, hasta 500 páginas por letra, y la pastilla del género en cada casilla (decisión del dueño del 20-11-2026) | `FASE11_pared-generos.md` |

### Los conflictos y cómo se resolvieron

- **`RocolaOs/GANCHOS.tsv`** (en las tres juntas de código): se quedan todos los renglones de los dos lados, ordenados por número. En la de
  `navegador-archivos`, sus g80-g82 son los mismos tres de `pantalla-ctrlf4` (comprobado renglón a renglón: iguales salvo el número) y
  solo entran g100 y g101.
- **`navegador-archivos` sobre una `pantalla-ctrlf4` vieja** (`2f24a8d`, antes de `c7b4a41`, `e5eef60` y su revisión `2bcf38e`):
  `navegador-archivos` no tocó después de `2f24a8d` ningún archivo de la hoja Pantalla, ni `Kms.cs`, ni `rocola-pantalla` (sus cambios
  propios en lo de la otra rama: dos `Tab` en `Pruebas/RocolaOs/pantalla/prueba.sh`, `Nativo/Anfitrion.cs` con g101 y
  `Nativo/PantallaRocola.Ventana.cs` con g100). Git juntó solo y quedó lo de `2bcf38e`: `git diff pantalla-ctrlf4` de esos archivos solo da
  los renumerados.
- **`Pruebas/RocolaOs/catalogo/ApiFalsa/Program.cs`** (`pared-generos` salió de `c7f73b7`, antes de la sustitución): en el mismo sitio de
  `/v1/search` iban la búsqueda de la sustitución (`isrc:` / `track:`) y la del disco del catálogo sin id (`album:"..."`), y al final las
  funciones nuevas de cada uno (`Campo` y `SinAcentos`; `DiscoDelCatalogo`): se quedan las dos de cada una, una detrás de otra.
- **`SPOTIFY_JUNTA_PENDIENTES.md`**: la rama `pared-generos` traía la versión vieja (puntos 1 a 6); se queda la de la rama principal y su
  punto 6 entra como **punto 11**, marcado juntado en la junta 2; el punto 9 (Ctrl+F4) también se marca juntado.
- `AGENTS.md`: sin conflicto (ninguna de las cuatro ramas lo toca).

### Los ganchos renumerados

La rama principal ya usaba g80-g84 (el asistente con Spotify). Los de `pantalla-ctrlf4` pasan:

| Antes | Ahora | Dónde |
|---|---|---|
| g80 | **g85** | `Codigo/Form1.cs` 2077 (Ctrl+F4, delante de Ctrl+F2) |
| g81 | **g86** | `Nativo/Anfitrion.cs` 30 (`RocolaOs.AbreLaPantalla()`) |
| g82 | **g87** | `Nativo/CompositorPantalla.cs` 46 (la medida de la pantalla en cada cuadro) |

En la marca de cada línea, en `GANCHOS.tsv`, en los comentarios de `RocolaOs.Pantalla.cs`, `RocolaOs.PantallaSimulada.cs` y
`RocolaOs.Ventana.cs`, en `Pruebas/RocolaOs/pantalla/prueba.sh`, en `FASE11_pantalla.md` y en la nota de rangos de `PLAN_CIELO_VIVO.md`.
`navegador-archivos` (g100-g101) y `pared-generos` (g90-g98) no chocan con nada. Quedan **63 ganchos en 25 archivos** (g01-g07, g13,
g20-g24, g40-g57, g60-g62, g70-g87, g90-g98, g100-g101; 20 archivos de `Codigo/`); libres g99 y g102-g119; desde g120, del cielo vivo.

### Lo que se arregló en la junta (commits propios)

- `e718fe7` — `Pruebas/Qemu/pantalla.py`: dos `Tab` más para llegar a la barra de la resolución (con las pestañas Navegador y Archivos;
  lo mismo que `navegador-archivos` ya hizo en `Pruebas/RocolaOs/pantalla/prueba.sh`). **La prueba de QEMU no se corrió.**
- `39b6fc3` — `Pruebas/RocolaOs/asistente/prueba.sh` le da a la rocola `ROCOLA_CATALOGO_URL` y `ROCOLA_PORTADAS_CATALOGO` de la API falsa:
  con la pared juntada, `ROCOLA_OS=1` con la llave de la API arranca la descarga del catálogo, que a los 45 s miraría la versión
  «catalogo» de GitHub. Sus escenas duran 35 s como mucho (no llegaba), pero una prueba no debe poder salir a la red (lo mismo que
  `pared-generos` hizo con la del catálogo). La de la sustitución no lo necesita (`ROCOLA_OS` es una lista sin `pared`), ni la de
  `navegador-archivos` (`archivos,navegador`).
- `58f5824` — `Pruebas/RocolaOs/andamio/prueba.sh`: dos comprobaciones de la parte «ganchos» esperaban aún la base de antes de
  `PORT_xp-1718.md` («109 archivos comparados con fc7b667»); `ganchos.py` compara con `69e6547` y sus 112 archivos. **Fallaba igual en la
  rama principal** (la junta del 20-11 no corrió el andamio); no es de esta junta.
- `0c7584a` — `Pruebas/RocolaOs/asistente/prueba.sh`, escena `sinllave`: buscaba la palabra «asistente» en toda la bitácora de Rocola OS;
  con la pared juntada, la bitácora dice al arrancar dónde guardará el catálogo, y esa ruta (la carpeta de trabajo de la prueba) lleva la
  palabra. Ahora busca la marca de las líneas del asistente, `[asistente]`. Era la prueba, no el programa.
- Ningún cambio en el programa: `Codigo/`, `Nativo/`, `Plataforma/` y `RocolaOs/` quedan como salieron de los cuatro merges
  (`9621d34`); los programas probados (JIT y NativeAOT) se compilaron de ese código.

### Las pruebas

PENDIENTE
