# Fase 11: las juntas

*27-11-2026: el navegador de Ctrl+F4 (y el apartado Descargas de Archivos) se quitó; ver `FASE11_navegador-archivos.md`. Lo que abajo dice del navegador, de `navegador-archivos` o de la compilación completa por WebKit es la historia de esas juntas (la prueba se llama ahora `Pruebas/RocolaOs/archivos`).*

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
- `spotify-xp` NativeAOT 159/160 por un aviso de «Pantalla parada» con la máquina cargada (la revisión, abajo, ya no cuenta esos avisos; no se volvió a correr en NativeAOT).
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

| batería | JIT (`bd39d2b`; la prueba de `spotify-xp` con `34df01d`) |
|---|---|
| `pantalla` | 76/76 (con la escena nueva `navegador`: 5 de 5) |
| `navegador-archivos` | 51/51 |
| `pared-generos` | 69/69 (el contrato con el archivo nuevo, 187/673, y las 4 de `real` con el catálogo de verdad) |
| `catalogo` | 156/156 (carga 9 a 12) |
| `andamio` | 101/101 |
| `asistente` | 29/29 |
| `sustitucion` | 97/97 |
| `spotify-xp` | 160/160 (antes 158/160, abajo) |

- `spotify-xp` dio primero 158/160 (carga 3 a 7, después de un reinicio de la máquina): (1) «el vigilante abre como mucho un programa
  por segundo» contó 54 en 20 s porque contaba cada `execve` de strace, también los intentos fallidos (ENOENT) por cada carpeta del PATH
  y la envoltura de `rm` que la sesión de pruebas pone delante en el PATH (`/root/.claude/...`); el vigilante abrió lo de siempre (10
  `sleep`, 2 `mountpoint`, 3 `rm`). `34df01d`: solo cuenta los que se abrieron de verdad y sin esas envolturas (15, como antes). (2) «pulso:
  ni un hueco dentro de cada una» con un hueco en la de 440 Hz: la máquina cargada; otra vez con carga 11, BIEN (y las otras tres escenas
  que miran huecos, BIEN en las dos corridas). Repetida entera con `34df01d`: 160/160.
- Ninguna en NativeAOT en esta revisión (lo cambiado son las pruebas y, en `bd39d2b`, `RocolaOs.Navegador.cs`, `RocolaOs.Pantalla.cs` y la pantalla simulada, solo con `ROCOLA_OS`; el NativeAOT de la junta, arriba).

## Junta 3: lo de Ctrl+F4, el modo noche, los apagones, la letra y el cielo (21-11-2026)

Rama `junta-3` (en `.claude/worktrees/junta-3`), sobre la rama principal `claude/modest-carson-fa2v2n` en `26a2bed`. Merges con `--no-ff`
(sin rebase), en este orden:

| # | Rama | Hasta | Lo que trae | Documento |
|---|---|---|---|---|
| 1 | `junta-2` | `d9ab792` | La segunda compilación: Ctrl+F4 (Pantalla, Navegador y Archivos), la pared con el catálogo, el pre-mapeo de géneros | arriba, «Junta 2» |
| 2 | `archivos-papelera` | `134d08e` | Descargas y Papelera en «Archivos» de Ctrl+F4 (sale de `junta-2`) | `FASE11_navegador-archivos.md` |
| 3 | `xp-1718-linux` | `086854b` | La importación de XP con `parejo_v2` y la conversión con ffmpeg de los «MP3» que no lo son (g110-g113) | `PORT_xp-1718.md`, «Lo de Linux» |
| 4 | `noche-50` | `9478303` | El modo noche de 50 a 90 % (`Rocola/` y `Codigo/`) | `PORT_xp-1718.md`, «El modo noche de 50 a 90 %» |
| 5 | `noche-nv` | `c01578f` | NV3-NV5 (lo de los vecinos) y su revisión; g102 (el filtro de mpv de noche) | `PORT_noche-nv.md` |
| 6 | `apagones` | `a8b075f` | La auditoría contra los apagones y lo arreglado (dirsync en `/rocola/datos`) | `FASE11_apagones.md` |
| 7 | `letras` | `5126eb1` | Buscar por la letra (L3; Genius apagado) y la fuente de la letra (LRCLIB, la USLT del MP3) | `FASE11_letras.md` |
| 8 | `cielo-nucleo` | `1fb1a6e` | El núcleo del cielo vivo, DN1 y DN5 (g120-g139) | `FASE11_cielo-nucleo.md` |

### Los choques y cómo se resolvieron

- `RocolaOs/GANCHOS.tsv` (en xp-1718-linux, noche-nv, letras y cielo-nucleo): las filas de los dos lados.
- `Pruebas/Importa/mundo.py` y `prueba.sh` (en noche-50 y noche-nv, contra xp-1718-linux): la configuración de XP falsa lleva las claves
  de las tres ramas (lo del 17 y 18-11, `noche_pct=50` y las de los vecinos), y se comprueba todo lo de las tres. Un solo valor chocaba:
  `noche_graves=` (5 en xp-1718-linux, 10 con `noche_graves_8=1` en noche-nv): queda 10, el de noche-nv, también en la comprobación de
  rocola-xp abierta después de importar.
- `PORT_xp-1718.md` (xp-1718-linux y noche-50 añadían cada una su sección al final): las dos, una detrás de otra.
- Sin choque de git pero sí de números: los ganchos de `letras` (abajo).
- `AGENTS.md`: solo `apagones` lo cambió (una entrada arriba); sin choque. `Rocola/Form1.Sonido.cs` y `Codigo/Form1.Sonido.cs`: solo los
  cambiaron noche-50 y noche-nv (xp-1718-linux solo puso g110 y g111 en `Codigo/`), y git los juntó solo; `Codigo/` queda igual que en
  noche-nv salvo los ganchos.

### Los ganchos renumerados

Ninguno repetido entre ramas salvo **g110 y g111**: los tomaron `xp-1718-linux` (`Codigo/Form1.Sonido.cs`: la conversión en `OpenAudio` y
en `LectorMedida`) y `letras` (`Codigo/Form1.Biblioteca.cs`: la letra en la lista del buscador y la tarjeta «NO ESTÁ EN LA ROCOLA»).
Entró antes xp-1718-linux, así que los de letras pasan a **g114 y g115**: en sus dos marcas de `Codigo/Form1.Biblioteca.cs`,
`GANCHOS.tsv`, `RocolaOs.Letras.cs`, `RocolaOs.LetrasGenius.cs`, `RocolaOs.SpotifyCatalogo.cs` (dos comentarios) y `FASE11_letras.md`
(dicho allí). Los demás no se tocan: g85-g87, g90-g98, g100-g101 (junta-2), g102 (noche-nv), g110-g113 (xp-1718-linux), g120-g139
(cielo-nucleo). En total **90 ganchos en 28 archivos** (84 en 23 archivos de `Codigo/`). Las líneas de g110 y g111 se movieron con lo
que noche-nv añadió en `Form1.Sonido.cs` (1511 → 1638 y 621 → 742); las demás siguen en su sitio.

### La base nueva de los ganchos

**`d5bc2b0`**: el código de la junta con los 90 ganchos devueltos (su línea de antes, en `Codigo/` y fuera, en `Nativo/`); el commit
siguiente, `67eda45`, los vuelve a poner. Su `Codigo/` es byte a byte el de `a76d008` (la base de noche-nv): ninguna otra rama cambió
`Rocola/`. La nombran `ganchos.py`, `nivel0.sh` (`BASE=`), la cabecera de `GANCHOS.tsv`, `Pruebas/RocolaOs/LEEME.md` y la prueba del
andamio (que aún decía `69e6547`). Si la rama se junta con squash o rebase, ese número cambia: `--base` / `BASE=`.

### Lo que se arregló en la junta (commits propios)

- `1cb623a`: la prueba del cielo esperaba de `diff-xp.py --sin-ganchos` las cuentas de antes del modo noche (5 135 / 4 868 / 229);
  con noche-nv son 5 147 / 4 880 / 229 (las de `nivel0.sh`). Era el único MAL del cielo en JIT (59 de 60).
- `3270072`: **spotify-xp, escena `fabrica`, «ni una petición a la red» con `ROCOLA_OS=1`: 3 conexiones**. Con cielo-nucleo juntado,
  rocola-xp sin ninguna llave pide el lugar (ipapi.co) y el clima (Open-Meteo) a los 20 s: es lo decidido por el dueño (CL, encendido de
  fábrica, sin llave), y la prueba de la junta 1 lo contaba como red de Spotify (salían por el proxy de la máquina, 127.0.0.1:33575). La
  escena le da ahora al clima, al lugar, a LRCLIB y a Genius un sumidero (`127.0.0.1:7`, nada escucha): esas no cuentan y se dicen
  aparte (3); lo de Spotify (`127.0.0.1:9`) y lo demás, igual. Ojo: **las demás baterías que abren con `ROCOLA_OS=1` sin darle al cielo
  un clima falso también salen a internet** a por el lugar y el clima (no lo comprueban); no se cambiaron.

### Las pruebas

Corridas el 21-11-2026 en la máquina de 4 núcleos compartida (carga 3 a 6). Programas de `67eda45` (lo de después solo toca pruebas y
documentos): JIT (`dotnet build -c Release`) y NativeAOT (`dotnet publish -r linux-x64 -p:PublishAot=true`), los dos con 0 errores y los
13 avisos de `avisos-base.txt` (`clasifica.py avisos`: BIEN, ninguno nuevo); NativeAOT con 0 avisos IL/AOT. Bitácora y salidas en
`/root/rocola-trabajo/fase11/junta-3/` (fuera del repositorio).

- XP con Mono (`pruebas/todo.sh noche cadenaentera billar18 billarparejo nomp3 ecualizador`): la rocola a 0 avisos en los dos perfiles,
  todas en verde; una por una: `noche` 145/145, `cadenaentera` 91/91, `billar18` 35/35, `billarparejo` 8/8, `nomp3` 23/23,
  `ecualizador` 77/77. `compara-cadena` (el `Rocola.exe` de la rama principal, `26a2bed`, contra el de la junta; los dos perfiles): 16
  iguales bit a bit, 0 distintas (fuerza el día). `Rocola/` y `pruebas/` son los de noche-nv.
- `ganchos.py`: TODO BIEN (90 ganchos en 28 archivos; 112 archivos de `Codigo/` iguales a `d5bc2b0`, 23 con ganchos).
- `diff-xp.py --sin-ganchos` (112 archivos): 5 147 quitadas, 4 880 puestas, 229 sin marca (las de noche-nv; 84 ganchos devueltos).
- Nivel 0 (`nivel0.sh`, pasos previo, código, ganchos y cintas, con el JIT de la junta): **5 BIEN**; las cintas de la tarjeta sin
  `ROCOLA_OS`, **29 iguales (6 616 cuadros), 0 distintas**: la pantalla sin Rocola OS sigue siendo la de XP. No se corrieron los demás
  pasos del nivel 0 (arneses, banco, recorridos, escenas, cierre: en la junta 2 lo que salía MAL salía igual en la base).

Las baterías de `Pruebas/RocolaOs` y `Pruebas/Importa` (pasadas/total):

| Batería | JIT | NativeAOT |
|---|---|---|
| `andamio` | 101/101 | 101/101 |
| `asistente` | 29/29 | 29/29 |
| `pantalla` | 76/76 | 76/76 |
| `navegador-archivos` | 62/62 | 62/62 |
| `pared-generos` | 69/69 (con el catálogo de verdad) | 69/69 |
| `convierte` | 57/57 | 57/57 |
| `letras` | 88/88 | 88/88 |
| `apagones` | 36/36 | 36/36 |
| `cielo` | 60/60 (tras `1cb623a`; antes 59/60) | 59/60, y `escenas` otra vez 29/29 |
| `noche-video` | 9/9 | 9/9 |
| `Importa` | 181/181 | 181/181 |
| `sustitucion` | 97/97 | 97/97 |
| `catalogo` | 156/156 | 156/156 |
| `spotify-xp` (con `PULSO`) | 185/187, y `celular` y `noche` otra vez 65/65 (antes de `3270072`: 186/187) | **187/187** |

- `spotify-xp` JIT: la primera corrida (antes de `3270072`) dio 186/187, solo la de la red de arriba. La segunda, con el arreglo, 185/187:
  «celular: el estado: devueltas=2, no_pedidas>=2, sesion=0» y «probar xp: se queda hasta el otro toque, 15 s después (13,8 s abajo)»
  (la tolerancia de 13,9 a 16,2 s por el latido de 1 s, la de la junta 1). Las dos habían salido BIEN en la primera corrida y otra vez
  BIEN al repetir esas dos escenas (65/65); NativeAOT, 187/187. Es la máquina cargada, no la junta.
- `cielo` NativeAOT: «las fases de las capturas (bitácora de la pared vacía)» MAL una vez; la escena `escenas` repetida, 29/29 (BIEN
  también en las dos de JIT). La escena dura 52 s y la comprobación necesita las seis fases en la bitácora con la máquina cargada.

### Lo que queda

- Nada falla por la junta después de `1cb623a` y `3270072`. Lo que salió MAL una vez y BIEN al repetir (`spotify-xp` celular y «Probar»
  en JIT, `cielo` escenas en NativeAOT) es de la carga.
- ~~Las baterías que abren rocola-xp con `ROCOLA_OS=1` y sin clima falso salen a internet~~: arreglado en la revisión (abajo).
- No se corrió: el nivel 0 entero, el `cierre_rocola_os.sh`, la prueba de QEMU ni ninguna compilación del sistema (el navegador pide la
  COMPLETA). Lo pendiente de cada rama sigue en su documento. Nada probado en el A4.

### La revisión de la junta 3 (21-11-2026)

Un revisor no encontró nada grave. De lo que encontró, comprobado uno por uno:

- **La rama principal se movió después de la junta** (cierto: `e763255`, la entrada del 21-11 de AGENTS.md sobre las portadas reducidas,
  no estaba en junta-3 y se habría perdido al adelantar). Merge `--no-ff` de `claude/modest-carson-fa2v2n` en junta-3 (solo AGENTS.md, sin
  choque); `git merge-base --is-ancestor claude/modest-carson-fa2v2n junta-3` da 0 y AGENTS.md tiene las dos entradas del 20 y 21-11.
- **PORT_xp-1718.md con las líneas viejas de g110 y g111** (cierto: 1511 y 621). `8aeda81`: 1638 y 742, con la nota de que se movieron en
  la junta 3 (las de GANCHOS.tsv).
- **Las baterías salían a internet** (cierto: `ipapi.co` en letras y sustitución). Cada `prueba.sh` de `andamio`, `asistente`, `catalogo`,
  `letras`, `pantalla`, `pared-generos`, `spotify-xp` y `sustitucion` exporta `ROCOLA_CLIMA_API`, `ROCOLA_LUGAR_API`, `ROCOLA_LETRA_API` y
  `ROCOLA_GENIUS_API` al sumidero `127.0.0.1:7` si no vienen puestos (una escena con su falso lo pone delante y manda; `cielo` tiene los
  suyos y no se tocó). Con el arreglo, en `letras` la bitácora dice 6 veces «no se supo el lugar por internet (127.0.0.1 ...)» y ninguna
  `ipapi.co`.
- **spotify-xp `red()` descontaba cualquier puerto 7** (cierto). Ahora solo `htons(7), sin_addr=inet_addr("127.0.0.1")` (como lo escribe
  strace), en `red()` y en `sumidero()`.
- **Importa: las claves de los vecinos con una subcadena** (cierto). Ahora `grep -qxF` sin los `\r`, como `noche_desde`.
- Dudas que no son de la junta: la escena `fabrica` y el reposo con el cielo encendido sin llaves (las decisiones del 15-11 «sin llaves como
  XP» y del 20-11 «lo demás encendido de fábrica» pueden chocar en el reposo: lo aclara el dueño); Kms.cs de Consola viene de la junta 2.

Corridas otra vez con el JIT de la junta (el código no cambió, solo pruebas y documentos): `letras` 88/88, `sustitucion` 97/97, `Importa`
181/181, `spotify-xp` 167/167 (sin `PULSO`, por eso no las 187; la escena `fabrica`: 0 conexiones a internet con y sin `ROCOLA_OS`, 3 al sumidero). Salidas en `/root/rocola-trabajo/fase11/junta-3/rev/`. Nada probado en el A4.


## Junta 4: la rapidez (carátulas, Spotify y lo general) y el cielo apagado de fábrica (21-11-2026)

Rama `junta-3` (en `.claude/worktrees/junta-3`), sobre `44dc14c` (la junta 3 con su revisión). Merges con `--no-ff` (sin rebase), en este
orden:

| # | Rama | Hasta | Lo que trae | Documento |
|---|---|---|---|---|
| 1 | `rapido-caratulas` | `6cbd650` | Las carátulas locales más rápidas: `Nativo/` con los mismos bytes (`Imagenes`, `Mapas`, `Pixeles`), la función `caratulas-rapidas` con g220-g224 (`portadas_dct=` y `portadas_dos_lectores=`, encendidas de fábrica) | `FASE11_rapido-caratulas.md` |
| 2 | `rapido-spotify` | `9473eb7` | Spotify más rápido (portadas del catálogo y de Spotify a la vez y por adelantado, el «play» adelantado, conectar antes); solo `RocolaOs/` y pruebas, sin ganchos (sale de `junta-2`) | `FASE11_rapido-spotify.md` |
| 3 | `rapido-general` | `80f358b` | Lo general: el compositor no mezcla capas vacías, el texto de GDI+ sin objetos por llamada, los polígonos en un `SKPath` del hilo; solo `Nativo/` | `FASE11_rapido-general.md` |

Y la **decisión de esta versión** (el dueño: «Cuando termines esto sube a github, ya lo demás que entre en otra versión»; el cielo vivo va
en la siguiente): las cuatro funciones del cielo vivo (`cielo`, `fiestas`, `clima`, `vidrio`; `RocolaOs.Cielo.cs`) quedan **apagadas de
fábrica** (`DeFabrica = false`, commit `40a88fc`): sin encenderlas en `rocola_os.txt` no hay cambio visible ni peticiones al lugar ni a
Open-Meteo. Sus claves no cambian. Vuelven a encenderse de fábrica en la versión del cielo (anotado en `PLAN_CIELO_VIVO.md` y
`FASE11_cielo-nucleo.md`). La batería del cielo las enciende ella con `ROCOLA_OS=1` (en `corre()`, si `rocola_os.txt` no dice ya
`cielo=`, `fiestas=`, `clima=` o `vidrio=`) y comprueba aparte lo de fábrica (`CIELO_DE_FABRICA=1`): la cinta de la pared igual a la de la
rama principal, el cielo no arranca, no pide el lugar ni el clima y no deja sus archivos (dos comprobaciones nuevas: 62 en vez de 60).

### Los choques y cómo se resolvieron

- `RocolaOs/GANCHOS.tsv` (rapido-caratulas): las filas de los dos lados; g220-g224 no estaban tomados.
- rapido-spotify: sin choque; git juntó solo `spotify-xp/prueba.sh` y `pared-generos/prueba.sh` (con el sumidero `127.0.0.1:7` de la
  revisión de la junta 3) y `RocolaOs.SpotifyCatalogo.cs` / `RocolaOs.SpotifyCatalogoApi.cs` (con los g114/g115 renumerados de letras).
- rapido-general: sin choque de git, pero **g87** (`Nativo/CompositorPantalla.cs`) se movió de la línea 46 a la 47 (rapido-general le
  añadió un campo, `Vacia`, a `CapaTextura` antes): arreglado en `GANCHOS.tsv` en el mismo merge. Los demás ganchos de `Nativo/`
  (g86, g101 en `Anfitrion.cs`) siguen en su sitio.
- Ningún gancho repetido: **95 ganchos en 29 archivos** (89 en 24 archivos de `Codigo/`).

### La base nueva de los ganchos

**`8c8cba0`**: el código de la junta con los 95 ganchos devueltos (`Codigo/` y fuera); el commit siguiente, `73a2de3`, los vuelve a poner
(el código queda byte a byte como el merge de rapido-general, `41faf5f`). Su `Codigo/` es **byte a byte el de `d5bc2b0`** (la base de la
junta 3): ninguna de las tres ramas cambió `Rocola/`; `Nativo/` lleva lo de rapido-caratulas y rapido-general. La nombran `ganchos.py`,
`nivel0.sh` (`BASE=`), la cabecera de `GANCHOS.tsv`, `Pruebas/RocolaOs/LEEME.md` y la prueba del andamio (`73a2de3`). Si la rama se
junta con squash o rebase, ese número cambia: `--base` / `BASE=`.

### Lo que se arregló en la junta (commits propios)

- Nada falló por la junta. Lo único propio es la decisión del cielo (arriba, `40a88fc`) y g87 (en el merge, `41faf5f`).
- Al correr `LeeIgual` hay que darle la lista con rutas absolutas (`todas.txt.abs` de rapido-caratulas): con `todas.txt` (relativa) se
  cae al leer la primera portada. Es de cómo se corre, no del programa.

### Las pruebas

Corridas el 21-11-2026 (17:10-22:00) en la máquina de 4 núcleos compartida (carga 2 a 5 en JIT; 4 a 11 en NativeAOT, con otro agente). Programas de `40a88fc` (lo de después solo
toca documentos): JIT (`dotnet build -c Release`) y NativeAOT (`dotnet publish -r linux-x64 -p:PublishAot=true`), los dos con 0 errores
y los 13 avisos de `avisos-base.txt` (`clasifica.py avisos`: BIEN, ninguno nuevo); NativeAOT con 0 avisos IL/AOT. Salidas en
`/root/rocola-trabajo/fase11/junta-4/` (fuera del repositorio; `baterias.log` con cada una).

- XP con Mono (`pruebas/todo.sh noche cadenaentera billar18 billarparejo nomp3 ecualizador`): TODAS EN VERDE; una por una: `noche`
  145/145, `cadenaentera` 91/91, `billar18` 35/35, `billarparejo` 8/8, `nomp3` 23/23, `ecualizador` 77/77. `compara-cadena` (el
  `Rocola.exe` de la rama principal, `e763255`, contra el de la junta; los dos perfiles): 16 iguales bit a bit, 0 distintas, en cpu y
  en gpu. `Rocola/` y `pruebas/` son los de la junta 3.
- `ganchos.py`: TODO BIEN (95 ganchos en 29 archivos; 112 archivos de `Codigo/` iguales a `8c8cba0`, 24 con ganchos).
- `diff-xp.py --sin-ganchos` (112 archivos): 5 147 quitadas, 4 880 puestas, 229 sin marca (las de la junta 3).
- Nivel 0 (`nivel0.sh`, pasos previo, código, ganchos, cintas y cierre, con el JIT y el NativeAOT de la junta): **7 BIEN**; las cintas
  de la tarjeta sin `ROCOLA_OS`, **29 iguales (6 616 cuadros), 0 distintas**; `cierre_rocola_os.sh` 43/43 en JIT y en NativeAOT.
- Las de las tres ramas: `RapidoCaratulas` **`LeeIgual` 21 663 comparaciones, 0 distintas** (con `todas.txt.abs`) y **`PixelesIgual`
  1 200, 0 distintas** (NativeAOT), las dos contra `85c931b` como en la rama; `RapidoGeneral` (su programa de medir, NativeAOT, con el
  código de la junta) la prueba de igualdad del compositor (`cmp5`: pared, buscador, Ctrl+F3, ayuda y botones con `d0` y `dcal`, cada 4
  cuadros): **3 361 cuadros comparados, 0 distintos**, `vuelven=0` en las diez; `rapido-spotify` su medida (`mide.sh`, NativeAOT; no
  comprueba nada, sus comprobaciones son las escenas `adelanto`, `adelantoxp` y `adelantoreloj` de `spotify-xp`): búsqueda 454 ms,
  página 1 de la A lista a 3,7 s del arranque, las páginas de la pared en pantalla a 54-77 ms, abrir un disco del buscador ya a la vista
  (adelantado; «-» como en la rama), lo de Soloist conectado a 248 ms (`rs-medidas.txt`).

Las baterías de `Pruebas/RocolaOs` y `Pruebas/Importa` (pasadas/total):

| Batería | JIT | NativeAOT |
|---|---|---|
| `andamio` | 101/101 | 101/101 |
| `asistente` | 29/29 | — |
| `pantalla` | 76/76 | — |
| `navegador-archivos` | 62/62 | — |
| `pared-generos` | 70/70 (con el catálogo de verdad; una más que en la junta 3, de rapido-spotify) | 70/70 |
| `convierte` | 57/57 | — |
| `letras` | 88/88 | — |
| `apagones` | 36/36 | 36/36 |
| `cielo` | 62/62 (las dos nuevas de fábrica, BIEN) | 62/62 |
| `noche-video` | 9/9 | — |
| `Importa` | 181/181 | — |
| `sustitucion` | 97/97 | 97/97 |
| `catalogo` | 156/156 | 155/156; `sinllave` otra vez 4/4 |
| `spotify-xp` (con `PULSO`) | **216/216** (las escenas nuevas de rapido-spotify incluidas) | 215/216; `noche` otra vez 54/55 y 54/55 (abajo) |

- `catalogo` NativeAOT, `sinllave` MAL una vez: la traza con `ROCOLA_OS` contra la de sin él, quitando lo de Rocola OS, con el encargo
  `MedidasBitacora.cs:129` (el vigía de la pantalla, uno cada 25 ms) 833 veces frente a 704 (holgura 80). Es la cuenta de la que avisa
  `FASE11_rapido-general.md` («Para la junta: la escena salirq»: la pantalla contesta antes y caben más vueltas; siempre de más, la misma
  lógica). Repetida sola, 4/4. En JIT, BIEN.
- `spotify-xp` NativeAOT, «Probar» del modo noche (la tolerancia de 13,9 a 16,2 s por el latido de 1 s, la de las juntas 1 y 3): en la
  corrida entera, «probar sp» 13,9 s; repetida la escena `noche` dos veces con la máquina a carga 7-11 (otro agente compilando), una vez
  «probar50 sp» 13,5 s y otra «probar sp» 13,9 s, cada vez otra de las cuatro y las demás BIEN (14,0-15,0 s); cada una de las cuatro salió
  BIEN al menos una vez, y en JIT las cuatro BIEN. Es la carga contra una tolerancia justa, no la junta (ninguna de las tres ramas toca el
  modo noche ni el latido); queda como en la junta 3: ensanchar la tolerancia o medir contra el latido, si se quiere que no falle.
- `spotify-xp`, escena `fabrica`: 0 conexiones a internet con y sin `ROCOLA_OS`; al sumidero `127.0.0.1:7`, 1 con `ROCOLA_OS=1` (en la
  junta 3 eran 3: el lugar y el clima del cielo ya no se piden, ahora apagado).

### Lo que queda

- Nada falla por la junta (lo de NativeAOT de arriba sale BIEN al repetir o es la tolerancia de «Probar» con la máquina cargada).
- **Para la versión del cielo**: los cuatro grupos (sol/luna/estrellas, nubes y clima, fiestas, vidrio), la hoja «Imagen» de Ctrl+F4 y
  volver a poner `DeFabrica = true` en las cuatro funciones (y quitar `CIELO_DE_FABRICA` de la batería o dejarla comprobando lo apagado);
  la letra dibujada en el cielo (K6).
- La escena `salirq` del nivel 0 sale «solo cuentas» más a menudo con rapido-general (siempre de más, `FASE11_rapido-general.md`, «Para la
  junta»): no se corrieron las escenas aquí; quien corra el nivel 0 entero puede aceptarla así.
- No se corrió: el nivel 0 entero (arneses, banco, recorridos, escenas), la prueba de QEMU ni ninguna compilación del sistema (el navegador
  pide la COMPLETA). Lo pendiente de cada rama sigue en su documento. Nada probado en el A4.
