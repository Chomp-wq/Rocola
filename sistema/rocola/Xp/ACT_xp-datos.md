# Activación de rocola-xp: sus datos (grupo xp-datos)

14-11-2026. Rama `act-xp-datos`, sobre f62d6cb (el port nativo terminado, con `--version` y el cierre con SIGUSR1). Parte de la
activación de la rocola traducida de XP como **único** programa de Rocola OS (decisión del dueño del 14-11-2026: «lo ideal es la que
acabamos de traducir [...]» y «traer lo aprendido de XP»). Aquí solo lo de los datos de `rocola-xp`; el lanzador, el paquete y la
compilación rápida los hace otro grupo a la vez (no se tocaron `sistema/board`, `sistema/package`, `sistema/rapida.sh` ni
`sistema/rocola/Pruebas/Lanzador`). **Nada se probó en el A4 ni con el sistema entero**, ni con datos reales de XP: todo con datos
inventados, fuera de pantalla.

## Qué se hizo

### 1. Lo grande, fuera de lo aprendido (`Nativo/DatosGrandes.cs`)

En Rocola OS la carpeta de datos de `rocola-xp` (`/rocola/datos/xp`) va dentro de lo aprendido: un ext4 de 512 MB guardado en un
archivo (`datos.img`), que además comparte con lo del programa anterior. Tres cosas de XP no caben ahí (ver «Tamaños»):

- el material para entrenar la IA (`rocola_ia_datos.bin`, `.idx`, `.generos.tsv`): la cosecha está encendida de fábrica
  (`Form1.Ia.cs:32`, `iaHarvestOn = true`) y su tope es de 24 GB (`Form1.Ia.cs:60`);
- las miniaturas de las portadas (`rocola_miniaturas/`, con `previews32-v1.bin`);
- el respaldo diario de lo aprendido (`respaldo/`).

Lo que el dueño decidió para Rocola OS el 3-11-2026 (AGENTS.md; `Motor/MaterialIa.cs` del programa anterior), aplicado a `rocola-xp`:

- Van a la carpeta que diga **`ROCOLA_IA_DATOS`** (el lanzador la pondrá en el disco de la rocola, `/rocola/disco/rocola-os/ia-xp`):
  `rocola_ia_datos.*`, `rocola_miniaturas/` y `respaldo/`. **Sin la variable, como XP** (en la carpeta de datos). Si está puesta pero no
  se puede escribir allí (se prueba como `Form1.CarpetaDatos`: crear y borrar un archivo), también a la carpeta de datos, y lo dice una
  línea por la salida de errores (el lanzador la apunta). Se decide una vez por sesión.
- **La cosecha se para si allí quedan menos de 256 MB libres** (se mira como mucho cada 30 s; `DriveInfo` del sitio del material). Lo
  demás sigue igual (las medidas, las miniaturas, el respaldo). También con `ia_datos_mb=0` («sin tope»): el sitio se mira siempre (el
  programa anterior no lo miraba con `ia_datos_mb=0`).
- `rocola_ia_hechas.txt` (de qué canciones ya se sacó material) sigue en la carpeta de datos, como en XP: es lo aprendido y es chico.
- No se muda nada: lo que hubiera en la carpeta de datos se queda (con `datos=xp` esa carpeta es la de la rocola de XP y su material es
  suyo; el programa anterior sí mudaba el suyo al arrancar).

En `Codigo/` solo **siete líneas**, cada una con su «// EN LINUX: (XP) ...»; la lógica de XP no cambia, solo dónde va cada cosa:

| Archivo | Línea | Era en XP | Ahora |
|---|---|---|---|
| `Form1.Ia.cs` | 61 | `IaBudgetFull() { return iaMaxBytes > 0 && pares*160+16 >= iaMaxBytes; }` | `(...) \|\| DatosGrandes.SinSitio()` |
| `Form1.Ia.cs` | 70 | `IaDataFile()`: `RutaDatos("rocola_ia_datos.bin")` | `DatosGrandes.Ruta(...)` |
| `Form1.Ia.cs` | 251 | `IaIndexFile()`: `RutaDatos("rocola_ia_datos.idx")` | `DatosGrandes.Ruta(...)` |
| `Form1.Ia.cs` | 322, 493 | `RutaDatos("rocola_ia_datos.generos.tsv")` | `DatosGrandes.Ruta(...)` |
| `Form1.Portadas.cs` | 718 | `CarpetaMiniaturas()`: `RutaDatos("rocola_miniaturas")` | `DatosGrandes.Ruta(...)` |
| `Form1.Datos.cs` | 2483 | `BackupBaseDir()`: `RutaDatos("respaldo")` | `DatosGrandes.Ruta(...)` |

`DatosGrandes.Ruta(n)` sin `ROCOLA_IA_DATOS` es `Path.Combine(Form1.CarpetaDatos, n)`, lo mismo que `RutaDatos(n)`: sin la variable (las
escenas, las pruebas, la referencia) todo sale igual que antes. Con «tope alcanzado» en Ctrl+F3 (`IaInfoText`) cuando falta sitio: la
misma frase de XP para «la cosecha se paró».

### 2. El importador (`Nativo/ImportaXp.cs`): `rocola-xp --importa-xp CARPETA_XP [DISCO...]`

Al cargarse el programa, antes de entrar al `Main` de XP (como `Nativo/VersionRocolaOs.cs`): sin la instancia única, sin pantalla ni
sonido, y en la carpeta de datos solo escribe lo suyo.

- **Destino**: `ROCOLA_DATOS` (obligatoria). **Origen**: la carpeta de datos de la rocola de XP, o un archivo cuya primera línea la dice
  (`/run/rocola-datos-xp`, de `rocola-discos`). **Discos**: los que se den; sin ellos, `/rocola/discos/*` (ordenados) y `/rocola/disco`,
  los mismos que el programa anterior (`Motor/Arranque.cs`, `rocola-discos`).
- **Rutas**: cada ruta de Windows («C:\\Rocola\\musica\\...») se busca tramo por tramo, tal cual y si no sin distinguir mayúsculas (lo que
  hay en cada carpeta se lee una vez: 20 000 canciones en 1,1 s en NativeAOT y 2,2 s en JIT, con la caché del disco caliente). **Cada letra de unidad va al
  disco donde están más de sus canciones** (una muestra de hasta 40 por letra probada en cada disco; a igualdad, el disco de la carpeta
  de XP y después el orden): con una copia de la música en otro disco que vaya antes en la lista, la letra no se confunde (el programa
  anterior se quedaba con el primer disco donde encontraba la primera canción). Una canción que no está en el disco de su letra se busca
  en los demás. Las de red («\\\\servidor») y las que no están: la línea entera fuera.
- **El sello de cada canción** (`rocola_fuentes.txt`: `largo|creada|escrita|...`). `rocola-xp` comprueba al sonar que la medida es del
  archivo de hoy (`FuentesMedidas.cs`, `CompruebaFuenteMedida`); si no cuadra, la **adopta** pero le pone el sello de hoy y la vuelve a
  medir (`porVerificar`). El largo y la escrita valen igual en Linux (NTFS guarda la fecha en UTC con 100 ns y ntfs3 la da tal cual),
  pero la **creada no**: .NET en Linux no lee la fecha de nacimiento (`statx`) y da la más vieja de escritura y cambio (comprobado aquí:
  un archivo de ext4 con nacimiento de hoy y escritura de 2020 da `CreationTimeUtc` = la de 2020). Sin arreglarlo, **cada canción
  importada se volvería a medir la primera vez que sonara**. Por eso, si el largo y la escrita son los de XP (el mismo archivo, sin tocar),
  la creada pasa a la que verá `rocola-xp` en Linux; si no, se deja (otro archivo: `rocola-xp` hará lo de XP). Comprobado con el programa
  de verdad (prueba `usa`: con el sello de Windows la adopta y le cambia el sello; con el puesto, nada).
- **Las tablas**: las que XP guarda añadiendo al final (sonoridad, volumen, EQ, fuentes) se leen como las lee XP (`LeeLineasSanas`: sin
  la última línea si quedó a medias, sin las que tienen ceros); las demás, tal cual. Sin el archivo, su copia `.bak` (como
  `RecuperaGuardadosCortados`). Texto en UTF-8; si no lo es, Windows-1252 (uno editado con el Bloc de notas). Se escriben en UTF-8 sin
  marca y con CRLF, como XP.
- **La configuración**: `dir=` traducidas (a una carpeta) o quitadas si no están; `respaldo_dir=` quitada (en Rocola OS los otros discos
  van en solo lectura: el respaldo fallaría cada día; y en el disco de XP `PruneBackups` dejaría solo los tres respaldos más nuevos de
  `respaldo_rocola`, borrando los de la rocola de XP); lo demás tal cual. Se junta con la que ya hubiera (abajo).
- **Nunca a medias**: todo se arma en `.importa-xp.tmp` (dentro del destino) y cada archivo se baja al disco (`Flush(true)`); después lo
  que se reemplaza se copia a `.antes-xp` (si se corta ahí, lo de antes sigue entero en su sitio), se apunta `.importa-xp.en-curso`, cada
  archivo nuevo cambia de nombre al suyo de un tirón y **la marca va la última**. Un fallo no deja nada (ni la marca: la siguiente vez se
  vuelve a intentar). Si se cortara entre los cambios de nombre, `.importa-xp.en-curso` hace que la vez siguiente no vuelva a copiar a
  `.antes-xp` lo que ya es de la importación nueva.
- **Si no se encuentra ni una canción** de las tablas en los discos (no están montados), no se importa nada y sale con 4: se intenta otra
  vez en el siguiente arranque (sin esto se apuntaría la marca con todo fuera y nunca se volvería a traer).
- **La primera vez** (sin marca): lo que ya tenga `rocola-xp` no se toca (si no es igual, sale en la marca como «ya había de rocola-xp, no
  se tocan»), salvo una tabla suya sin ninguna canción (vacía, o solo su cabecera: la que deja `rocola-xp` abierta antes sin la música;
  añadido en la revisión, ver «Revisión»), y salvo lo que el lanzador de antes copió tal cual de XP (`rocola_de_xp.txt`: «NOMBRE de
  CARPETA») y la configuración, que se junta: manda la de `rocola-xp` si es suya y la de XP si es la copia del lanzador. **De la otra** pasan las claves que la que manda no
  tiene y las carpetas `dir=` que falten (son una lista, no una clave); nunca una línea con una ruta de Windows. Lo reemplazado, en
  `.antes-xp`.
- **Otra carpeta de XP** (la marca dice otra; regla del 7-11-2026): se importa **todo** otra vez de la buena; lo de antes en `.antes-xp`
  (solo lo que la nueva trae: lo que la nueva no tiene se queda) y la configuración junta, mandando la nueva, con las claves que solo
  tenía la de antes (`JuntaConfigDeAntes`). **La misma carpeta en un disco que cambió de nombre** (hoy `sdc1`, ayer `sdb1`: la de la marca
  ya no existe, y la ruta dentro del disco y el `Rocola.exe` —largo y fecha— son los mismos) no se re-importa: solo se apunta dónde está.
- **La marca**, `rocola_importado_xp.txt`: primera línea como la del programa anterior («Importado de la rocola de XP (CARPETA) el
  FECHA»), `carpeta=`, `relativa=` (dentro de su disco), `programa=` (el `Rocola.exe`), y por tabla: líneas, traducidas, fuera; los sellos
  puestos; lo de la configuración; los modelos; lo que no se trae; a qué disco fue cada letra (con su cuenta); lo igual, lo no tocado y
  lo que fue a `.antes-xp`.
- **Salida**: una línea por la salida normal y 0; o una línea por la de errores y 2 (mal llamado), 3 (no está la carpeta de XP) o 4 (no se
  pudo leer o escribir, o ninguna canción en los discos). Con `ROCOLA_DATOS` igual a la carpeta de XP (con `datos=xp`), 0 sin tocar nada.

## Qué se trae y qué no

| Archivo de la carpeta de XP | Quién lo lee (Codigo/) | Clave | ¿Se trae? | Por qué |
|---|---|---|---|---|
| `rocola_sonoridad.txt` | `Sonoridad.cs` `LeeIntegRegistros` | `alg\|...\|ruta` | sí, rutas traducidas | lo medido (integrada, picos, LRA, silencios, zumbido, pre-eco) |
| `rocola_volumen.txt` | `Form1.Sonido.cs` `FillLoudCacheRecords` | `rms\|dur\|ruta` | sí | el nivelado |
| `rocola_eq.txt` | `Form1.Datos.cs` `FillEqCacheRecords` | `curva\|ruta` | sí | la huella del EQ y el corte (calidad) |
| `rocola_fuentes.txt` | `FuentesMedidas.cs` `LeeFuentes` | `largo\|creada\|escrita\|huellas\|ruta` | sí, y la creada de Linux | sin el sello cada medida se volvería a medir |
| `rocola_duracion.txt` | `Form1.Biblioteca.cs` `FillDurFastRecords` | `dur\|ruta` | sí | las duraciones rápidas |
| `rocola_afondo.txt` | `FillDeepDoneRecords` | `ruta` | sí | sin él el repaso a fondo empezaría de cero (comentario de XP en `BackupLearnedData`) |
| `rocola_ia_hechas.txt` | `FillIaDoneRecords` | `N\|ruta` | sí | no volver a sacar material de lo que XP ya cosechó (la IA repetiría ejemplos) |
| `rocola_stats.txt` | `Form1.Datos.cs` `ReadStatsRecords` | `cuenta\|ruta`, `cuenta\|artista`, `genero\|ruta` | sí (artistas y totales tal cual) | lo más pedido, las monedas, los géneros aprendidos |
| `rocola_billar_nivel.txt` | `VolumenParejo.cs` `CargaParejo` | `db\|ruta` | sí | el volumen parejo del billar |
| `rocola_billar_bitacora.txt` | `BitacoraBillar.cs` | solo el nombre del archivo | sí, tal cual | el resumen de la tarjeta del billar |
| `rocola_teclas.txt` | `Form1.Ajustes.cs` `KeysFile` | códigos de tecla de Windows | sí, tal cual | los botones (los números son los mismos en `rocola-xp`) |
| `rocola_config.txt` | `Form1.Datos.cs` `LoadConfigPrefs`... | clave=valor | sí: `dir=` traducidas o fuera, `respaldo_dir=` fuera | los ajustes de XP |
| `IAS/rocola_*.bin` (y `.anterior-N`) | `Form1.Datos.cs` `RutaLectura`, `Form1.Ia.cs` | nombre | sí, tal cual (nombre en minúsculas) | los modelos |
| sueltos `rocola_ia.bin`, `rocola_eq_ia.bin`, `rocola_ia_genero_*.bin` | `OrdenaIas` | nombre | sí, a la carpeta (los pasa a `IAS/` `rocola-xp` al arrancar, como XP) | los modelos |
| `rocola_estado.txt` | `RestoreState` | — | **no** | créditos y cola: el día |
| `rocola_noche.txt` | `AtraccionGpu.cs` | `noche=FECHA` | **no** | lo pedido esta noche: a las 8 de la mañana empieza de cero |
| `rocola_error.txt`, `rocola_diagnostico*.txt`, `rocola_calidad.txt` | — | — | **no** | informes de XP; `rocola-xp` escribe los suyos |
| `rocola_incompatibles.txt` | `NoteIncompatible` | ruta | **no** | solo evita apuntar dos veces lo que el descompresor de XP (ACM) no abrió; en Linux el descompresor es otro |
| `rocola_biblioteca.txt` | `TryLoadLibraryCache` | firma de las carpetas (`RootsSig`) | **no** | caché que XP rehace; su firma es de las rutas de Windows: no valdría |
| `rocola_letras.txt` | `LeeTintes` | `sig=` de la biblioteca | **no** | ídem (los colores de la barra se recalculan en segundo plano) |
| `rocola_miniaturas/` | `ThumbCachePath` | huella de la ruta de la portada | **no** | caché que XP rehace; cada una se llama por su ruta de Windows |
| `respaldo/` | — | — | **no** | copias de lo de XP, con sus rutas de Windows |
| `rocola_ia_datos.*` | `Form1.Ia.cs` | — | **no** | el material: no es un modelo, puede pesar gigas y se queda en la carpeta de XP (sin rutas: solo huellas) |

## Tamaños: lo que escribe rocola-xp en su carpeta de datos

Peor caso razonable con **20 000 canciones en 2 000 discos** y rutas de unos 100 caracteres (crece en proporción a la biblioteca).

| Qué | Topes que trae XP | Peor caso | Dónde queda |
|---|---|---|---|
| `rocola_miniaturas/` | una PNG de 256x256 por portada (`Form1.Portadas.cs:43`); 142 KB cada una (lo medido por XP, `Form1.Portadas.cs:742`); el calentador las hace de toda la biblioteca (`Form1.cs:1786`); el barrido solo empieza pasados 200 MB y solo quita las de más de 365 días (`Form1.Portadas.cs:857-858`, `Form1.Biblioteca.cs:2619`) | **~280 MB** (2 000 × 142 KB; el comentario de `Form1.Portadas.cs:849` habla de 23 KB, pero era de las JPEG de antes), más `previews32-v1.bin`, que se compacta al pasar de su capacidad más 128 registros de 4 108 B (`PreviewsIndexadosGpu.cs:167`, `CaratulasAdaptativas.cs:319-320`; capacidad 1 024 con dos núcleos, como el A4, y 4 096 con más, `CaratulasAdaptativas.cs:408`): **~4,7 MB** en el A4 (~17 MB con más núcleos; XP no lo lee si pasa de 64 MB, `PreviewsIndexadosGpu.cs:31`) *(corregido en la revisión: antes decía «hasta 64 MB»)* | **`ROCOLA_IA_DATOS`** |
| `rocola_ia_datos.*` | 24 GB (`Form1.Ia.cs:60`); 160 B por ejemplo (`Form1.Ia.cs:61`) | **24 GB** | **`ROCOLA_IA_DATOS`**, y se para con < 256 MB libres |
| `respaldo/` | 3 copias diarias (`Form1.Datos.cs:2515`, `PruneBackups`) de 15 archivos (`Form1.Datos.cs:2487-2494`) | ~28 MB por copia lo normal, ~49 MB en el peor (tablas sin compactar y `rocola_ia_hechas.txt` con 9 líneas por canción): **84 a 147 MB** | **`ROCOLA_IA_DATOS`** (también porque una copia dentro de `datos.img` se pierde con él, y XP respalda porque «el disco puede morir») |
| las cuatro tablas que se guardan añadiendo (sonoridad, volumen, EQ, fuentes) | se compactan al pasar de 1,5 veces más 1 000 líneas (`DiarioTablas.cs:192`), con su `.bak` | 3,5 + 2,3 + 3,4 + 3,6 MB compactas; hasta ×2,5 con lo añadido y el `.bak`: **~32 MB** | carpeta de datos |
| `rocola_ia_hechas.txt` | se añade, nunca se compacta; hasta 9 líneas por canción (8 pasadas y a fondo, `Form1.Ia.cs:38-40`) | **~19 MB** (lo normal 2 a 6 MB) | carpeta de datos |
| `rocola_stats.txt` (+`.bak`), `rocola_biblioteca.txt` (+`.bak`), `rocola_afondo.txt`, `rocola_duracion.txt`, `rocola_billar_nivel.txt` (compacta al pasar de 2×+200, `VolumenParejo.cs:220`), `rocola_calidad.txt`, `rocola_letras.txt` | — | 8,4 + 4,8 + 2,0 + 2,2 + 4,2 + 2,3 + 0,4: **~24 MB** | carpeta de datos |
| `rocola_billar_bitacora.txt` | 4 MB y pasa a `.1` (`BitacoraBillar.cs:29,202`) | **8 MB** | carpeta de datos |
| `rocola_diagnostico.txt` | 2 MB, solo con `diagnostico=1` (`Form1.cs:967`; se borra al pasar, `MedidasBitacora.cs:318`) | 2 MB | carpeta de datos |
| `rocola_error.txt` | 256 KB (`Form1.cs:2320`, `Program.cs:139`...) | 0,3 MB | carpeta de datos |
| `rocola_diagnostico_FECHA.txt` | los 10 últimos (`DiagnosticoCompleto.cs:805,1139`) | decenas de KB cada uno | carpeta de datos |
| `rocola_noche.txt` | 400 canciones (`AtraccionGpu.cs:220`) | 45 KB | carpeta de datos |
| `rocola_incompatibles.txt` | sin tope, una línea por canción una sola vez (`Form1.Sonido.cs:1524-1527`) | KB (5 MB si fallaran todas) | carpeta de datos |
| `IAS/` | los modelos (53 KB el DSEE, 3 KB el EQ) | < 1 MB | carpeta de datos |

Con eso, en `/rocola/datos/xp` quedan como mucho **unos 90 MB** (lo normal, unos 40) en lugar de unos 520 MB (que no caben en un
`datos.img` de 512 MB compartido). En el disco de la rocola (`ROCOLA_IA_DATOS`): las miniaturas (~280 MB más unos 5 MB de previas), el
respaldo (84 a 147 MB) y el material (hasta 24 GB, parado con < 256 MB libres). Nada se llevó a `/run`: las miniaturas son una caché que
XP rehace, pero XP las sacó de la carpeta temporal a propósito (`Form1.Portadas.cs:705-711`: en la memoria se borran al apagar y se
rehacen cada vez, 16 ms frente a 1,5 ms por portada), y en el A4 serían 280 MB de memoria.

## El contrato con el lanzador (lo que el otro grupo usa de aquí)

- `ROCOLA_DATOS=/rocola/datos/xp /usr/lib/rocola-xp/rocola-xp --importa-xp /run/rocola-datos-xp` en cada arranque, antes de abrir el
  programa; sin discos (los busca: `/rocola/discos/*` y `/rocola/disco`). Sale con 0 enseguida si `rocola_importado_xp.txt` ya dice esa
  carpeta (sin tocar nada). Con otro código, una línea por la salida de errores; un fallo no impide abrir la rocola.
- `ROCOLA_IA_DATOS` (para `rocola-xp`, `/rocola/disco/rocola-os/ia-xp`): ahí van el material de la IA, `rocola_miniaturas/` y
  `respaldo/`. Sin ella, todo en la carpeta de datos, como XP.

## Comprobaciones (máquina de pruebas, fuera de pantalla con llvmpipe; nada en el A4)

- **Compilación JIT** (`dotnet build -c Release`): 0 errores, **13 avisos, los mismos de la base** (archivo, línea y código: SYSLIB0054 ×9,
  CA2022 ×2, SYSLIB0050, CA2014; las líneas no se movieron).
- **NativeAOT** (`dotnet publish -c Release -r linux-x64 -p:PublishAot=true`): 0 errores, **0 avisos IL/AOT**, los mismos 13.
- **`Codigo/`**: tres archivos, siete líneas, todas con «EN LINUX». Los 109 archivos con el **mismo número de líneas y de CRLF** que en
  f62d6cb (contados uno por uno). `diff-xp.py`: `Form1.Ia.cs` 9/6 → 14/11, `Form1.Portadas.cs` 75/72 → 76/73, `Form1.Datos.cs` 46/43 →
  47/44 (líneas quitadas/puestas); sin marca: 0, 0 y las 2 de antes en `Form1.Datos.cs`.
- **`Pruebas/Importa/prueba.sh`** (datos inventados; ver su LEEME): **136 de 136 en JIT y 136 de 136 en NativeAOT**. Entre ellas: cada tabla
  sin rutas de Windows, la canción que ya no está fuera, la línea a medias fuera, la de otras mayúsculas y la de acentos en mayúsculas
  («03 CANCIÓN ÑANDÚ.mp3») con su nombre del disco, D: al disco con su música aunque una copia parcial vaya antes, los seis sellos con la
  creada de Linux, la configuración, los botones y los
  modelos byte a byte, lo que no se trae, la marca; la segunda llamada sin tocar nada (ni las fechas); la copia del lanzador de antes; otra
  carpeta (`.antes-xp`, la configuración juntada sin repetir claves ni carpetas, lo que la nueva no trae intacto); el disco que cambia de
  nombre; los fallos 2, 3 y 4 sin dejar nada (también con el destino inmutable); la carpeta de XP como destino; **la carpeta de XP y su
  música sin tocar** (contenido y fechas; y los discos falsos montados en solo lectura mientras se importa). Con `rocola-xp` abierto
  (parte `usa`): lee las seis medidas importadas, el repaso solo tiene la canción nueva («repaso: 1 archivos»), las importadas no se
  vuelven a medir, la moneda de los botones importados (la 77) da créditos sin abrir el asistente de los botones, nada de «Otro archivo»
  ni «Guardado cortado»; suena una importada y su sello vale tal cual, y en la de control (con la creada de Windows) `rocola-xp` la adopta
  y le cambia el sello. Lo grande (parte `material`, tres WAV de ruido blanco): sin `ROCOLA_IA_DATOS` todo en la carpeta de datos; con ella
  el material, las miniaturas y el respaldo allí (y `rocola_ia_hechas.txt` en la de datos); con 200 MB libres allí (un tmpfs) la cosecha
  no junta nada y lo demás sí; con ella en solo lectura, a la carpeta de datos y lo dice. Ninguna rocola de la prueba queda abierta.
- **Tiempo**: 20 000 canciones en 2 000 discos (tablas sintéticas de 22 MB, 30 MB importados): 1,0 a 1,1 s en NativeAOT y 2,2 s en JIT,
  con la caché caliente; la segunda llamada (ya importado), 8 ms en NativeAOT. No medido en el A4 (con el disco frío tardará más; es una
  vez).
- **`Pruebas/Ventanas/guiones/cierre_rocola_os.sh`**: **43 de 43 en JIT y 43 de 43 en NativeAOT** (TODO BIEN; el guion tiene hoy 43
  comprobaciones, con las de `selsigusr1` que le añadió su revisión), con el programa del primer paso y otra vez con el del respaldo (el
  último cambio, el texto de un error del importador, no pasa por ahí).
- **Las escenas contra la referencia congelada**, alternadas (`alterna.sh`, `compara.sh` con el ruido de `ventanas2`), dos veces (con el
  programa del primer paso y con el del respaldo): `arranque`, `cierreq`, `altf4`, `kill9` y `video` **IGUAL**; `sigterm` DIFERENTE solo en lo aceptado (la referencia no atiende el apagado: aquí
  `closing`/`closed` con `WindowsShutDown` y `rocola_estado.txt` borrado).
- **No corridos**: los recorridos, los arneses de vistas, el banco y las cintas (nada de lo tocado pinta ni sube nada a la tarjeta; sin
  `ROCOLA_IA_DATOS` todo va donde iba). La prueba del lanzador (es del otro grupo).

## Lo que queda

- El lanzador (otro grupo): llamar al importador según el contrato y poner `ROCOLA_IA_DATOS` para `rocola-xp`.
- Probarlo con el sistema entero (QEMU) y en el A4, con la carpeta de XP de verdad (sus tablas, su NTFS montado por ntfs3, sus fechas).
- «Traer otra vez lo aprendido en XP» (Ctrl+F3 → La rocola del programa anterior, `rocola_reimportar_xp`): es de la fase 11.
- Si se quiere, que `rocola-xp` use el material de la IA de la rocola de XP para entrenar (el programa anterior lo hacía): `rocola-xp` no
  entrena (como XP); es de la fase 11.

## Riesgos

- **Sin `ROCOLA_IA_DATOS`** (Rocola OS desde la memoria USB, sin el disco de la rocola), todo va a la carpeta de datos, como en XP: con
  2 000 discos, unos 280 MB de miniaturas más el respaldo. Si esa carpeta es `datos.img` de 512 MB, se llenaría (la cosecha sí se para).
- **La fecha de creación**: el arreglo del sello cuenta con que .NET en Linux no lee la de nacimiento (comprobado con .NET 10.0.114). Si
  una versión futura la leyera (`statx`), los sellos apuntados (los importados y los que haya puesto `rocola-xp`) dejarían de cuadrar:
  cada canción se adoptaría y se volvería a medir una vez al sonar (nada se pierde).
- **Discos FAT32 o exFAT** (memorias USB): guardan la hora local, y Windows y Linux pueden verla distinta; entonces los sellos no cuadran y
  esas canciones se vuelven a medir al sonar (lo de XP con un archivo cambiado: nada se pierde). En NTFS (el disco de XP) cuadran.
- **El sello y ntfs3**: que la escrita llegue igual (100 ns, UTC) es por cómo ntfs3 lee la fecha de NTFS; no visto con un NTFS de verdad
  (aquí no hay ninguno). Si no cuadrara, lo mismo de arriba: se vuelven a medir.
- **Dos discos con la misma música**: el voto elige bien la letra, pero el lanzador apunta como `dir=` todas las carpetas «musica» de
  todos los discos: `rocola-xp` vería la música dos veces (no es de este grupo).
- **Una vez por carpeta**: lo que la rocola de XP aprenda después de la importación (si se sigue usando en el bar, arrancando Windows) no
  se trae; `rocola-xp` lo mide por su cuenta. Como el programa anterior.
- **La re-importación** pasa a `.antes-xp` lo que `rocola-xp` aprendió desde la primera (la regla del 7-11-2026). La identidad por
  `Rocola.exe` evita re-importar cuando solo cambia el nombre del disco; si a la vez se cambiara el `Rocola.exe` de XP, se re-importaría.
- **Una carpeta de XP sin ninguna canción en los discos** se reintenta en cada arranque (sale con 4 y el lanzador lo apunta cada vez).
- Las miniaturas, el respaldo y el material en el NTFS de la rocola: cada archivo nuevo marca el NTFS «sucio» hasta el siguiente `sync`
  (lo atiende `S99rocola`, como con el material del programa anterior).
- La importación corre antes de abrir la rocola: la primera vez, con una biblioteca grande y el disco frío, la rocola tarda unos segundos
  más en aparecer.

## Revisión (revisor independiente, 14-11-2026)

Leído el diff entero (f62d6cb..95b9266) contra la tarea y las reglas, y el lanzador del otro grupo (rama `act-sistema-activa`, 740f872)
para el contrato. Repetido aquí lo principal y lo que la lectura puso en duda (máquina de pruebas, fuera de pantalla con llvmpipe; nada en
el A4 ni en QEMU). Commits de la revisión: 9413080 (arreglo 1), c0fad22 (arreglo 2 y la prueba) y el de este documento.

### Lo encontrado y arreglado

1. **(grave) La cosecha podía no mirar nunca el sitio libre** (`Nativo/DatosGrandes.cs`, `SinSitio`). Con `Environment.TickCount` (32 bits:
   cuenta desde que se prendió el equipo y da la vuelta a los 24,9 días) y la primera vez en `int.MinValue / 2` (copiado de
   `Motor/MaterialIa.cs`, que tiene el mismo fallo y no se toca), la primera resta se desbordaba si `rocola-xp` arrancaba con el equipo
   encendido entre 12,4 y 37,3 días (cada 49,7): contestaba «hay sitio» sin mirar y no volvía a mirar hasta salir de ese tramo (hasta 24,9
   días seguidos), así que la cosecha podía llenar el disco de la rocola, justo lo que evita el tope de 256 MB. Ahora `TickCount64` (no da
   la vuelta) y la primera vez siempre se mira. En las pruebas no se veía: la máquina llevaba menos de 12 días prendida.
2. **(grave) Una tabla vacía de `rocola-xp` tapaba para siempre la de XP** (`Nativo/ImportaXp.cs`). Reproducido con el programa de verdad:
   `rocola-xp` abierta sin la música y sin tablas deja al cerrarse `rocola_volumen.txt` de 0 bytes (y `respaldo/`, `rocola_error.txt`). En
   Rocola OS eso pasa si la primera importación sale con 4 (no está el disco de la música) y el lanzador abre la rocola igual, como dice el
   contrato. La importación siguiente decía «ya había de rocola-xp, no se tocan: rocola_volumen.txt» y ponía la marca: el nivelado de XP no
   entraba nunca. Ahora, antes de la primera importación (sin marca), una tabla de `rocola-xp` **sin ninguna canción** (ninguna línea con
   una ruta de Linux: vacía, o solo su cabecera y sus totales; la bitácora del billar, que no lleva rutas, siempre) se reemplaza: la vacía
   sin `.antes-xp`, las otras con su `.antes-xp`; la marca lo dice («vacias (de rocola-xp, sin ninguna cancion), reemplazadas: ...»). Una
   tabla con alguna canción de `rocola-xp` sigue sin tocarse, como antes y como el programa anterior.
3. **(menor, documento)** `previews32-v1.bin` no llega a 64 MB: se compacta al pasar de su capacidad más 128 registros (1 024 con dos
   núcleos, como el A4: unos 4,7 MB; 4 096 con más: unos 17 MB); 64 MB es solo lo que XP ya no lee. Corregido en «Tamaños».

### Lo comprobado

- **Compilación**: JIT, 0 errores y los mismos 13 avisos de la base (archivo, línea y código); NativeAOT, 0 avisos IL/AOT y los mismos 13.
  Antes y después de los arreglos.
- **`Codigo/`**: la revisión no lo toca. Los 109 archivos con las mismas líneas y CRLF que f62d6cb (contados uno por uno); `diff-xp.py`:
  `Form1.Ia.cs` 14/11, `Form1.Portadas.cs` 76/73, `Form1.Datos.cs` 47/44 (sin marca: 0, 0 y las 2 de siempre), como dice el grupo.
- **`Pruebas/Importa/prueba.sh`**: con el programa del grupo, 136 de 136 en JIT (lo que dice el grupo). Con los arreglos y los 9 casos
  nuevos: **145 de 145 en JIT y 145 de 145 en NativeAOT** (la primera corrida en NativeAOT dio 144: «(linux) suena «01 Pulso»» con
  la máquina cargada por la prueba del lanzador del otro grupo; esa comprobación pulsa las teclas a tiempo fijo; repetida la parte `usa`,
  14 de 14, y la prueba entera, 145 de 145). Los nuevos: `rocola-xp` abierta antes de la primera importación sin la música (cerrada
  con SIGUSR1: deja `rocola_volumen.txt` vacío; la importación lo reemplaza sin `.antes-xp`, lo dice la marca y una tabla con una canción
  de `rocola-xp` se queda), y un **apagón a media importación y a media re-importación**: `strace` mata el programa en seco al ir a poner
  un archivo en su sitio (ese cambio de nombre no se hace); la marca no está o sigue diciendo la carpeta de antes, y la llamada siguiente
  deja exactamente lo mismo que una sin cortes, también los `.antes-xp` (`.importa-xp.en-curso` hace su papel). A mano, además: cortes en
  los archivos 1, 5, 12 y 16 de una primera importación y 1 y 3 de una re-importación (todos igual que sin cortes), y el destino casi lleno
  (un tmpfs de 160 KB): sale con 4, una línea, y no deja nada.
- **Las citas archivo:línea de «Tamaños»**: miradas una por una; bien (la de 705-711 empieza en la línea en blanco de antes del comentario).
- **El sello**: .NET 10 lee las fechas con `lstat` (strace del importador: ni un `statx`), así que `CreationTimeUtc` en Linux no es la de
  nacimiento; es lo que supone el arreglo del sello. Con ntfs3 de verdad no se vio (aquí no hay ntfs3; ver «Lo que queda»).
- **El contrato**, contra el lanzador del otro grupo (740f872): llama `ROCOLA_DATOS=$XPDATOS timeout -k 10 600 "$XPBIN" --importa-xp
  "$RUN/rocola-datos-xp"` (`/rocola/datos/xp` y `/run`) solo si `/run/rocola-datos-xp` tiene algo, apunta un código distinto de 0 y abre la
  rocola igual; pone `ROCOLA_IA_DATOS=/rocola/disco/rocola-os/ia-xp` (no la `ia` de `rocola-consola`, que se queda como está) solo con ese
  disco montado con escritura; y no toma el importador por la rocola abierta (lo distingue por `--importa-xp`). Coincide con lo de aquí. Al
  tope de 10 min, `timeout` le manda SIGTERM al importador: no es la rocola (no hay estado que borrar) y un corte no deja nada a medias.
- **Las reglas**: nada de este grupo cierra `rocola-xp` (la prueba cierra sus rocolas de prueba; el caso nuevo, con SIGUSR1); el grupo no
  toca guiones del sistema (lo del BusyBox no aplica); la carpeta de XP solo se lee (en la prueba, montada en solo lectura y con la foto de
  contenido y fechas igual antes y después); el estado del día no se trae; lo de `rocola-consola` en `/rocola/datos` no se toca.
- **`cierre_rocola_os.sh`**: 43 de 43 en JIT y 43 de 43 en NativeAOT (TODO BIEN). **Las escenas** alternadas con la referencia congelada
  (`alterna.sh` y `compara.sh` con el ruido de `ventanas2`, etiquetas `Rxpdatosrev`/`xpdatosrev`): en JIT `arranque`, `cierreq`, `altf4`,
  `kill9` y `video` IGUAL; en NativeAOT (`RxpdatosrevA`/`xpdatosrevA`) `arranque` y `kill9` IGUAL; `sigterm` DIFERENTE en los dos solo en
  lo aceptado (`encargo Apagado`, `closing`/`closed` con `WindowsShutDown`, `foco nadie`, y `rocola_estado.txt` borrado).

### Lo que no se cambió (visto y aceptado)

- El respaldo en `ROCOLA_IA_DATOS` (el NTFS de la rocola): se hace en cada arranque de la rocola y al cerrar el día (`Form1.cs:843`,
  `Form1.Datos.cs:2474`; quedan los 3 últimos), unos 30 a 50 MB escritos cada vez con 20 000 canciones. Fuera de `datos.img` sobrevive si
  este se estropea; cada vez marca el NTFS «sucio» hasta el `sync` de `S99rocola` (a los 15 s). Razonable; si no se quiere, es una línea.
- La configuración juntada suma las carpetas `dir=` de las dos (una lista), mientras `JuntaConfigDeAntes` del programa anterior dejaba las
  de antes solo si la nueva no tenía ninguna: así no se pierden las que apunta el lanzador ni las que el dueño añada en `rocola-xp`; a
  cambio, una carpeta que solo tenía la configuración de antes (ya traducida, o apuntada por el lanzador) sigue en la lista.
- Los modelos se traen con el nombre en minúsculas: un modelo por género con mayúsculas en su nombre, que XP no usaba, `rocola-xp` sí lo
  usaría. El entrenador los escribe en minúsculas: no pasa.

### Lo que queda y riesgos (añadidos en la revisión)

- **Lo que se importa la primera vez es lo que está en los discos ese día, y ya no se vuelve a traer.** Si falta un disco con parte de la
  música (otra letra), sus líneas quedan fuera y la marca se pone; si falta toda, sale con 4 y se reintenta en cada arranque. Si antes de
  que salga bien `rocola-xp` llega a medir canciones de otro disco, esas tablas suyas (con canciones) no se reemplazan. Conviene activar
  con todos los discos de la música conectados; la marca dice la letra que no encontró disco («unidad E: -> (ninguno: ...)»).
- **Los nombres de los discos no son fijos**: `rocola-discos` monta cada partición con su nombre del núcleo (`/rocola/discos/sdb1`); si
  cambia (una memoria USB conectada al prender), las rutas de las tablas, las importadas y las de `rocola-xp`, dejan de casar con la
  música mientras dure y `rocola-xp` la vuelve a medir. Ya pasaba con `rocola-consola`; no afecta a lo del disco de la rocola
  (`/rocola/disco`). Lo arreglaría montar por UUID o etiqueta (`rocola-discos`, no es de este grupo).
- **La música de XP a más de 3 carpetas de la raíz de su disco**: XP la busca junto a sus datos y hasta 3 carpetas más arriba;
  `rocola-discos` solo busca «musica» hasta 3 niveles desde la raíz. Si la de XP está más honda y no está en Ctrl+F2 (`dir=`), `rocola-xp`
  no la ve (tampoco `rocola-consola`). No es de este grupo; el lanzador o el importador podrían apuntarla.
- El sello con un NTFS de verdad por ntfs3 (en QEMU o en el A4) y la carpeta de XP de verdad: sin probar.
