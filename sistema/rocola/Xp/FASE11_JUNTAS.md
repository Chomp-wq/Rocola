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
  `pared-generos` hizo con la del catálogo). (Aquí se decía que la de la sustitución no lo necesitaba porque su `ROCOLA_OS` era «una
  lista sin `pared`»: **no es así**, su escena `apagado-cero` abre con `ROCOLA_OS=1` y la llave de la API; lo arregló la revisión, abajo.)
  La de `navegador-archivos` no lo necesita (`archivos,navegador`).
- `58f5824` — `Pruebas/RocolaOs/andamio/prueba.sh`: dos comprobaciones de la parte «ganchos» esperaban aún la base de antes de
  `PORT_xp-1718.md` («109 archivos comparados con fc7b667»); `ganchos.py` compara con `69e6547` y sus 112 archivos. **Fallaba igual en la
  rama principal** (la junta del 20-11 no corrió el andamio); no es de esta junta.
- `0c7584a` — `Pruebas/RocolaOs/asistente/prueba.sh`, escena `sinllave`: buscaba la palabra «asistente» en toda la bitácora de Rocola OS;
  con la pared juntada, la bitácora dice al arrancar dónde guardará el catálogo, y esa ruta (la carpeta de trabajo de la prueba) lleva la
  palabra. Ahora busca la marca de las líneas del asistente, `[asistente]`. Era la prueba, no el programa.
- Ningún cambio en el programa: `Codigo/`, `Nativo/`, `Plataforma/` y `RocolaOs/` quedan como salieron de los cuatro merges
  (`9621d34`); los programas probados (JIT y NativeAOT) se compilaron de ese código.

### Las pruebas

Corridas del 20 al 21-11-2026 en cuatro sesiones (dos reinicios de la máquina y un corte de la orden de fondo; lo cortado se volvió a correr
entero y no cuenta). La máquina, de 4 núcleos, compartida con otros agentes (carga de 0 a 12). Bitácora de las corridas:
`/root/rocola-trabajo/fase11/junta-segunda/bateria.log` (fuera del repositorio).

Los programas: JIT (`dotnet build -c Release`) y NativeAOT (`dotnet publish -p:PublishAot=true`) del código de `9621d34` (el de `junta-2`
desde entonces: los commits de después solo tocan pruebas y documentos). JIT: 0 errores y 13 avisos, los mismos 13 de `avisos-base.txt`
(archivo y línea; ninguno nuevo). NativeAOT: sale con 0, 0 avisos IL/AOT (13 avisos de C#, los mismos).

- `ganchos.py --base 69e6547`: TODO BIEN; 63 ganchos en 25 archivos; `Codigo/` devolviendo los ganchos: 112 archivos iguales a `69e6547`
  (20 con ganchos).
- `diff-xp.py --sin-ganchos` (112 archivos): 5 135 líneas quitadas, 4 868 puestas y 229 sin marca, las de la base.

Las baterías de `Pruebas/RocolaOs` (pasadas/total):

| Batería | JIT | NativeAOT |
|---|---|---|
| `pantalla` | 71/71 | 71/71 |
| `navegador-archivos` | 51/51 | 51/51 |
| `pared-generos` | 69/69 | 69/69 |
| `catalogo` | 156/156 | 156/156 |
| `andamio` | 101/101 (tras `58f5824`; antes 99/101, fallaba igual en la rama principal) | 101/101 |
| `asistente` | 29/29 (tras `0c7584a`; antes 28/29) | 29/29 |
| `sustitucion` | 97/97 | 97/97 |
| `spotify-xp` | 160/160 | **159/160** |

- `spotify-xp` tiene ahora 160 (140 en la junta 1): las partes `pulso` y `vigilante` (20) solo corren con `PULSO=` (el `libpulse.so.0` de
  `rocola-pulso`), y aquí se le dio. En NativeAOT: la corrida con carga 7 a 12 (`aot4`, sin `PULSO`) dio 136/140 (el XP de
  `apagado-sin` que no sonó, la traza y `rocola_error.txt` de `fabrica`, y la medida de «Probar» de XP, 13,9 s, la de la junta 1); `aot5`
  la cortó el reinicio; `aot6` (carga 3 a 4) dio **159/160**. El que falla es «fabrica: rocola_error.txt igual de largo que sin ROCOLA_OS»
  (15 y 9 renglones): la corrida con `ROCOLA_OS=1` tiene dos avisos de XP «Pantalla parada 512 ms / 532 ms, en: presentar cuadro de la
  tarjeta» (la tarjeta por software tardó en presentar), ningún aviso de Rocola OS; en `aot4` era al revés (la de XP tenía los renglones de
  más). Es la máquina cargada, no la junta; no se tocó la prueba.

El nivel 0 (`nivel0.sh` con los programas de arriba; la base, `424ade7`, con su JIT en `.../junta-segunda/base-jit`):

- BIEN: previo, código (112 archivos con las mismas líneas y CRLF que `69e6547`), `ganchos.py`, `diff-xp.py`, banco (semillas 7, 11 y 5
  IGUAL), cintas (29 iguales, 6 616 cuadros), ventanas (hoja 86, pantalla 32 JIT y 32 NativeAOT, PantallaVistas 78; 0 fallan),
  `cierre_rocola_os.sh` 43/43 JIT y 43/43 NativeAOT, `Pruebas/Importa` 145/145 JIT y NativeAOT.
- Arneses: 15 BIEN, VistasFondo ACEPTADO (solo la línea de marca) y **VistasVistasB MAL** por la regla de `nivel0.sh` («0 con excepción»):
  0 distintas de 3 108 con las dos letras, pero con DejaVu 18 casos `capsula-*-mide` dan `ArgumentException`, los mismos 18 en la
  referencia congelada (lo dice ya `PORT_vistas-disco.md`). No es de la junta.
- Recorridos: atracción, ayuda, botones, buscador y video BIEN; hojas, pared y pared2 ACEPTADO (lo de siempre); **ctrlf3 MAL** (capas
  quietas 56 de 61; sin explicar `c32.2` y `c8`, la capa de la página de Sonido, 966x764). **Con la base sale igual** (`base424c`: las
  mismas 5 capas distintas y las mismas 2 sin explicar): es el modo noche «de 22 a 7 h» de `PORT_xp-1718.md` contra la referencia
  congelada de antes, no la junta.
- Escenas JIT (24): 13 BIEN/IGUAL (arranque, video a la segunda, buscador, cierreq, altf4, kill9, asistente, captura, f2, f2sin, aviso,
  f12, f12vigia), sigterm ACEPTADO y 10 MAL: moneda, tab, f3sigterm, salirq, f2barras, f3vistas, f3grises, f3teclas, f3raton y f3billar.
  **Con la base salen las mismas 10 MAL** (`base424`), con las mismas diferencias (el interruptor «Modo noche (de 22 a 7 h)» en vez de
  «de 23», la página de Sonido con el botón nuevo «Voz, golpe y retumbe...» 6 px más abajo, los estados y `rocola_config.txt` con las
  claves nuevas del 17 y 18-11); salirq son solo cuentas de relojes (en la base, las del encargo `MedidasBitacora.cs:129`). Todo de
  `PORT_xp-1718.md`, que cambió a propósito la XP de la referencia congelada; ninguna escena distinta por la junta.
- Escenas NativeAOT (7): arranque, cierreq, kill9, buscador y f12 IGUAL; f3vistas y f3teclas MAL, las mismas diferencias que en JIT (y
  que en la base JIT).

No se corrió: la prueba de QEMU (`Pruebas/Qemu/pantalla.py`, `e718fe7`) ni ninguna compilación del sistema (el navegador pide la
COMPLETA). Nada probado en el A4.

### Lo que queda

- Nada falla por la junta. Lo que sale MAL en el nivel 0 sale igual en la base (`424ade7`) y viene de `PORT_xp-1718.md` (la XP del 17 y
  18-11 contra la referencia congelada) o del arnés VistasVistasB (las 18 excepciones de la referencia); habría que darle a `nivel0.sh` esas
  diferencias como aceptadas (no se hizo aquí: es de la oleada 0).
- `spotify-xp` NativeAOT 159/160 por un aviso de «Pantalla parada» con la máquina cargada; se puede volver a correr con la máquina libre.
- La prueba de QEMU de la hoja Pantalla con las pestañas nuevas, y la compilación COMPLETA (paquetes de WPE WebKit y cog), cuando el dueño
  la pida.
- **El catálogo publicado no tiene el pre-mapeo de géneros.** La versión «catalogo» de GitHub (20261114.3, 182 418 discos: la que lee la
  escena `real`) se armó antes de `8090912`; hasta volver a armarlo con `rocola-catalogo.yml`, la pastilla de cada casilla sigue con los
  géneros mal puestos que arregla `catalogo-generos` («A Flock of Seagulls» como Bolero, «classic rock» como Clásica...). Esa compilación
  es solo a pedido del dueño: él decide cuándo.
- Con la máquina muy cargada (carga 12) la escena `sinllave` de `catalogo` puede salir MAL («la traza igual a la de sin ROCOLA_OS») solo
  porque los relojes cuentan distinto (`Form1.cs:724`, `MedidasBitacora.cs:129`): lo de RJ9b/RJ12b de siempre; con la máquina libre sale
  BIEN. No es de la junta.

### La revisión de la junta (21-11-2026)

Un revisor no encontró nada grave. De lo que encontró, comprobado uno por uno:

- **El contrato I8 era el de antes** (cierto: `cmp` de `pared-generos/contrato/rocola_catalogo_prueba.tsv.gz` con el de
  `sistema/catalogo/prueba` daba distintos; 176/652 contra 187/673). `29c129c`: la copia del armador juntado, y las cuentas se leen de su
  cabecera «#rocola-catalogo».
- **La escena `real` se saltaba sin avisar** (cierto). `29c129c`: sin el catálogo de verdad sus 4 comprobaciones cuentan como FALTA y la
  batería sale con 2.
- **Pruebas que podían salir a la red** (cierto: `sustitucion` `apagado-cero` abre con `ROCOLA_OS=1` y la llave). `29c129c`: la de la
  sustitución da `ROCOLA_CATALOGO_URL` y `ROCOLA_PORTADAS_CATALOGO` de su API falsa; `spotify-xp`, `andamio` y `pantalla`, `127.0.0.1:9`
  (nada escucha). Corregido el texto de arriba.
- **`spotify-xp` fabrica contaba los «Pantalla parada»** (cierto). `29c129c`: `rocola_error.txt` se compara sin esos avisos de XP ni su
  renglón «(sin excepcion)».
- **Los programas falsos compartidos se rehacían por la fecha** (cierto). `29c129c`: en las cinco baterías, la API falsa y el Soloist
  falso se rehacen si su `Program.cs` no es igual (`cmp`) a la copia que queda junto al `.dll`.
- **La carga en `catalogo sinllave`**: anotado arriba; no se toca.
- **Duda: el navegador podía borrar el modo guardado** (cierto, por el código: `Kms.Recupera` no mira `drmSetMaster`, y un
  `drmModeSetCrtc` fallido al volver hacía `modo_malo`). `bd39d2b`: en los 15 s después de `Retoma`, el latido toma otra vez el mando y
  pone el mismo modo (hasta 10 veces); solo si sigue fallando, lo de siempre. El KMS simulado aprende `retoma=N` y `pantalla/prueba.sh`
  la escena `navegador` (corregida la frase de `FASE11_pantalla.md`). Con cog y una tarjeta de verdad no se probó; `Kms.cs` no se tocó.
- **Dudas sin cambio**: el nivel 0 ya se comparó con la base arriba (las mismas MAL); QEMU y la compilación COMPLETA siguen sin correr; el
  caso a ciegas de `2bcf38e` sigue sin prueba (ni el KMS simulado ni QEMU lo dan).

Las baterías afectadas, otra vez en JIT con el programa de `bd39d2b` (bitácora `.../junta-segunda/bateria-rev.log`):

RESULTADOS_REV
