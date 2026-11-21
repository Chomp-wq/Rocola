# rocola-xp: el modo noche para los vecinos (NV3, NV4 y NV5; 20 de noviembre de 2026)

Rama `noche-nv`, sobre `noche-50` (el deslizador del modo noche de 50 a 90 %; juntada otra vez el 21-11-2026 con su arreglo de la
revisión, abajo) con `noche-vecinos` juntada (el documento
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
  y de `nivel0.sh` pasó de ebdb272 a ca9802f (y al juntar noche-50 otra vez, a **2c4b3c3**, abajo): un commit con `Codigo/` (y las dos líneas de fuera, g21 y g22) SIN los ganchos y con el
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
| `compara-cadena` (la rocola-xp de antes, de noche-50, contra la nueva; de día) | 16 iguales bit a bit, 0 distintas (`compara-cadena` fuerza el día: la noche solo la prueban `noche.cs` y la escena noche de spotify-xp) |
| `Pruebas/RocolaOs/noche-video/prueba.sh` (NV5b), JIT y NativeAOT | 9 de 9 en los dos: sin Rocola OS el filtro ni se toca y el video sale como el archivo (50 Hz − 1 kHz = 9,54 dB); con `noche_video` de noche, `af` puesto y −14,61 dB (el 50 Hz baja 24,15 dB, lo mismo que mpv suelto con ese filtro) con el 1 kHz igual (64,04 / 64,04 dB); de día, `af` vacío y 9,54 dB. (El WAV del plugin `file` de ALSA dice s32 y lleva float32: se lee con `mide.py --float`.) |
| spotify-xp, escena «noche» (con la corrida nueva de la madrugada), JIT y NativeAOT | TODO BIEN (55) en los dos. La madrugada: antes de la 1, la noche entera y quieta (−3,27 / −3,23 dB frente al día, 0,0 dB/min); desde la 1, −0,0326 (Spotify) y −0,0321 / −0,0320 (XP) dB/min (esperado −0,0321), salto 0,0001-0,0004 dB, Spotify contra XP 0,0005-0,0006 dB/min |
| `Pruebas/Importa` (JIT) | TODO BIEN (151; antes 145: las seis claves nuevas tal cual) |
| Ctrl+F3 en rocola-xp (captura fuera de pantalla, `Pruebas/Windows/corre.sh`) | la página de Sonido con el botón «Graves, corte y madrugada...» y su hoja: todo cabe y se lee |

Con Mono, de la rocola de XP: los dos perfiles a 0 avisos; `noche` 136/136 (también con la hora local a las 2 y a las 23),
`compara-cadena` 16 iguales (también con el reloj a las 23; fuerza el día, así que eso solo prueba que la noche no se cuela), `cadenaentera` 91, `billar18` 35, `billarparejo` 8, `gravemono` 55, `subgrave` 68,
`todojunto` 13, `transiciones` 46, `ventanaf3` 9 405, `ajustes` 4, `videoaudio` 10.

No se corrió `nivel0.sh` entero (las escenas de Ctrl+F3 → Sonido contra la referencia congelada ya no son las suyas a propósito). Nada
oído ni probado en el A4 ni en el bar.

## La junta de noche-50 con su arreglo (21-11-2026)

Esta rama se hizo sobre una `noche-50` vieja (540b36c). `noche-50` siguió con el arreglo de su revisión (9478303: `32d1782` en XP,
`d6026b1` su base de ganchos y `5d81f3b` en `Codigo/`): la subida de las 7 en 20 s también al 50 % (la rampa se quedaba atrás de la
curva del avance y tardaba 23 s) y el deslizador movido de noche de 50 a 90 % y de vuelta en 20 s o menos. Se juntó con `git merge
noche-50` (a0c2cd5). Choques:

| Dónde | Cómo quedó |
|---|---|
| `AvanzaRampa` (`Rocola/Form1.Sonido.cs` y `Codigo/Form1.Sonido.cs`, la misma línea) | el paso de noche-50 (de 1 a lo más bajo de la noche, de donde está o de a donde va) con el piso de la madrugada de esta rama: `Math.Min(MadLin(), Math.Min(actual, meta))` (MadLin nunca es más alta que NocheLin; con la noche igual que la madrugada, lo de noche-50) |
| `AvanzaModoNoche` (los dos archivos) | lo de esta rama (los dos avances, el grave y el corte que siguen a su meta con `SigueMeta`) y lo de noche-50 (lo que ya va por la curva la sigue sin rampa), **cada uno por su lado**: antes de avanzar se mira si el volumen está en `FactorDeAvances(nocheAvance, madAvance)`, el grave en `GravesDeAvances` y el corte en `CorteDeAvances` (y que no se está probando ni en la rampa rápida); el que está, toma su meta; el que no (un mando movido, «Probar», un ajuste nuevo), su rampa. En noche-50 el grave salía del volumen (`GravesNocheDe(AvanceDeFactor(nocheFactor))`); aquí va aparte desde NV1/NV2, por eso la curva se mira en los tres. Cinco líneas más en los dos archivos (los comentarios y `libre`/`enCurva`/`gCurva`/`cCurva`) |
| `LEEME.md`, `ganchos.py`, `nivel0.sh`, `GANCHOS.tsv` | lo de esta rama, con la base nueva (abajo) |

Lo demás de noche-50 se juntó solo (`pruebas/noche.cs` con sus comprobaciones nuevas, `AUDIO_NOCHE_Y_MEJORAS_XP_MODEST_2026-11-17.md`,
`PORT_xp-1718.md`). `diff` de `Rocola/Form1.Sonido.cs` contra `Codigo/Form1.Sonido.cs`: las mismas 49 líneas de traducción y ganchos
que antes de la junta.

- **Base de los ganchos**: **2c4b3c3** (hija de la junta: `Codigo/` y g21/g22 de `Nativo/` sin los ganchos); 09c7d70 los pone otra vez.
  g61, g23 y g13 (`Form1.Sonido.cs`) bajan 5 líneas (1455, 1463, 1639). `ganchos.py`, `nivel0.sh` (`BASE`), `GANCHOS.tsv` y el LEEME
  apuntan a 2c4b3c3. `avisos-base.txt`: los dos de `Form1.Sonido.cs` a 3397 y 3415; los mismos 13.

### Pruebas de la junta (21-11-2026, la máquina de 4 núcleos compartida)

| Qué | Resultado |
|---|---|
| Mono, `pruebas/todo.sh noche cadenaentera billar18 billarparejo` | los dos perfiles 0 avisos; TODAS EN VERDE |
| Mono, las mismas sueltas contra el `Rocola.exe` de la junta | `noche` 142/142 (136 de esta rama + 6 de noche-50: la subida de la mañana al 50 % en 20,0 s, el deslizador de noche de 50 a 90 % en 20,0 s y de vuelta, el interruptor al 50 % en 20,0 s, y al 80 % la subida justo por la curva vuelta a vuelta en 20,0 s); también con la hora local a las 2 y a las 23 (142 y 142); `cadenaentera` 91/91, `billar18` 35/35, `billarparejo` 8/8. La de la madrugada: «a las 7 todo sube junto en 20,0 s» |
| Mono, `compara-cadena` (el `Rocola.exe` de 9d965c7 —esta rama antes de la junta— y el de noche-50 contra el de la junta), a las 7 y a las 23 h | 16 iguales bit a bit, 0 distintas, en las cuatro (`compara-cadena` fuerza el día —`nocheArrancada`, `nocheFactor = 1`— y no toca el grave ni la mezcla del corte: la noche solo la prueban `noche.cs` y la escena noche de spotify-xp; lo de las 23 h solo dice que la noche no se cuela) |
| `ganchos.py` | TODO BIEN (50 ganchos; 112 archivos comparados con 2c4b3c3, 18 con ganchos) |
| `diff-xp.py --sin-ganchos` (112 archivos) | 5 147 quitadas, 4 880 puestas, 229 sin marca (las mismas; 48 ganchos devueltos en 18 archivos) |
| rocola-xp JIT (`dotnet build -c Release --no-incremental`) | 0 errores, 13 avisos (`clasifica.py avisos`: BIEN, los de la base) |
| rocola-xp NativeAOT (`-p:PublishAot=true`) | sale con 0, los mismos 13 avisos (BIEN), 0 de IL/AOT |
| `noche-video/prueba.sh`, JIT | 9 de 9 (sin Rocola OS 9,54 dB y sin filtro; con `noche_video` de noche −14,61 dB igual que mpv suelto, 1 kHz 64,04 / 64,04 dB; de día 9,54 dB) |
| spotify-xp, escena «noche», JIT | TODO BIEN (55). Las 22 h: −0,0544 (Spotify) y −0,0537 (XP) dB/min, salto 0,0002 dB; ya de noche −3,22 dB (80 %) y −10 dB (50 %), quieta; «Probar» 0,9 s (80 %) y 1,5 s (50 %); la madrugada desde la 1 −0,0326 / −0,0321 dB/min (esperado −0,0321), salto 0,0002 / 0,0004 dB |
| `noche-video/prueba.sh`, NativeAOT | 9 de 9 (los mismos números que en JIT) |
| spotify-xp, escena «noche», NativeAOT | la primera corrida, 53 BIEN y 2 MAL: «probar50» sp y xp con 13,6 s abajo (se pide más de 13,9; la pulsación de «Probar» entró tarde con la máquina cargada, carga 4,6-5; la bajada, −10,05 / −10,0 dB, la rampa de 1,6 s y la vuelta en 1,4 / 1,5 s, bien). Repetida: TODO BIEN (55): las 22 h −0,0542 / −0,0535 dB/min, salto 0,0002 / 0,0005 dB; «Probar» 14,9 s abajo (80 %) y 15,5 / 14,6 s (50 %), rampa 1,0 / 0,8 s y 1,6 / 1,5 s; la madrugada −0,0325 / −0,032 dB/min, salto 0,0001 / 0,0004 dB |
No se corrió `nivel0.sh` entero. Nada oído ni probado en el A4 ni en el bar.

## La revisión de la rama (21-11-2026)

Un revisor no encontró nada grave; de lo menor se arregló lo claro y barato (`d045245` en `Rocola/` y `Codigo/`, mismas líneas y
finales; base de ganchos nueva **a76d008**, `577c0e2` los vuelve a poner):

| Qué | Arreglo | Prueba |
|---|---|---|
| `AvanzaRampa`: tras la junta el paso tomaba `MadLin()` aunque la madrugada estuviera apagada (noche 80 %, madrugada apagada al 50 %: paso 0,684 en vez de 0,5; la rampa de 20 s en unos 9 s y «Probar» un 27 % más rápido) | `Math.Min(nocheMadrugada ? MadLin() : NocheLin(), Math.Min(actual, meta))` (si la rampa va o viene de la madrugada, `min(actual, meta)` ya la cubre) | `noche.cs`: con la madrugada apagada al 50 % y la noche al 80 %, el paso de antes bit a bit; encendida, el paso mayor |
| NV2 en un disco continuo: `Hereda` solo sigue con la memoria si el corte es el mismo; con el corte movido a medio disco (o encendido con la que suena sin la etapa) la siguiente empezaba con la memoria en ceros (medio grave sin quitar unas decenas de ms en el empalme) | en el relevo de un disco continuo la siguiente se arma con el corte de la que suena (`corteNocheRelevo` en `Form1.SinPausa.cs`, `GraveMono.CorteNoche`; 0 si la que suena no tenía la etapa): el corte nuevo vale desde la siguiente que no siga | `noche.cs` (lectura del código: el relevo lo pone con `SiguienteDelDisco` y `CorteNoche`, `ArmaCadena` lo lee; `CorteNoche` de la etapa) |
| NV5b: el filtro iba en la propiedad `af` del reproductor ANTES del `loadfile`: de video a video rehacía los filtros del que aún suena | el filtro va como opción SOLO de ese archivo en su `loadfile` (`af=%N%...`, el largo delante por las comas de lavfi): con el índice de mpv 0.38+ (`replace -1 opciones`, el del sistema es 0.40) y, si mpv lo rechaza (0.37), sin él; lo que no entiende lo rechaza sin hacer nada (comprobado con mpv 0.37 por su IPC: la opción vale para ese archivo y el siguiente vuelve sin filtro) | `noche-video/prueba.sh` 9/9 en JIT y NativeAOT (con mpv 0.37 de la máquina: la forma con índice no se probó aquí) |
| La hoja de la madrugada: su deslizador puede pasar el de la noche y suena el de la noche (`MadDb` = el menor) | el rótulo dice «N % (noche)» cuando pasa | (solo el texto) |
| El documento decía de `compara-cadena` «a las 7 y a las 23 h» como si probara la noche | aclarado arriba: fuerza el día | — |

No se arregló (y por qué):

- **NV5a, los escalones del volumen de los videos**: la perilla del video es entera (0 a 100) en XP (el WMP solo acepta enteros) y en
  `Codigo/` (`VideoVolumeFor` devuelve un `int`, código de XP): con una perilla baja, de noche al 50 % cada paso cerca del final es de
  0,6 a 2,5 dB cada pocos minutos durante la bajada. En rocola-xp mpv aceptaría decimales, pero cambiarlo es cambiar el código de XP en
  `Codigo/` (o un gancho nuevo en `VideoVolumeFor` y `ReproductorVideo.Volumen`): para que lo decida el dueño. En XP queda así.
- **«Graves de noche» en 0 y el corte a la izquierda no evitan la madrugada** (12 dB y 80 Hz): es lo que dice NV4 («con graves 12 dB y
  el corte a 80») y la nota de la derecha de la hoja; si el que los puso en 0 quiere que sigan en 0 a la 1, lo decide el dueño.
- **El volumen parejo del billar no aprende de 22 a 7** (`AvanzaModoNoche` pone `Margen.Descarta` con la noche, el grave o el corte
  puestos): mide antes del limitador y con la noche aprendería a subir esas canciones de día; el billar trabaja sobre todo de noche, así
  que aprende la mitad o menos. Otra forma (medir compensando por el factor de noche) queda para el dueño.
- **La marca `noche_graves_8` al reimportar** (`ImportaXp.JuntaConfig`: manda lo de XP y se añade lo que solo tiene rocola-xp): una XP del
  17-11 con `noche_graves=4` junto a la marca de rocola-xp salta la subida 4→8; igual con `noche_desde_22`. Caso de borde, no reproducido.
- **El WMP del A4 y su perilla**: NV5a supone que es lineal en amplitud (como `VideoVolumeFor` ya suponía); no se puede comprobar sin XP.
- **El empalme del subgrave de `c.Mono`** con `subgrave=` movido a medio disco tiene el mismo patrón que tenía el corte de noche; es de
  antes y no es de esta rama.
- El teclado en la hoja nueva (el foco se queda en el botón escondido) es igual que en «Ajustes del sonido...», como en XP.

### Pruebas de la revisión (21-11-2026, la máquina de 4 núcleos compartida)

| Qué | Resultado |
|---|---|
| Mono, `pruebas/todo.sh noche cadenaentera billar18 billarparejo` | los dos perfiles 0 avisos; TODAS EN VERDE |
| Mono, sueltas | `noche` 145/145 (142 + 3 nuevas), `cadenaentera` 91/91, `billar18` 35/35, `billarparejo` 8/8 |
| Mono, `compara-cadena` (el `Rocola.exe` de 3f14906 contra el de la revisión; fuerza el día) | 16 iguales bit a bit, 0 distintas |
| `ganchos.py` | TODO BIEN (50 ganchos; 112 archivos comparados con a76d008, 18 con ganchos) |
| `diff-xp.py --sin-ganchos` (112 archivos) | 5 147 quitadas, 4 880 puestas, 229 sin marca (las mismas) |
| rocola-xp JIT y NativeAOT | 0 errores, los 13 avisos de la base (`clasifica.py avisos`: BIEN), 0 de IL/AOT |
| `noche-video/prueba.sh`, JIT y NativeAOT | 9 de 9 en los dos (de noche −14,61 dB como mpv suelto, 1 kHz 64,04 / 64,04 dB; de día y sin Rocola OS 9,54 dB) |

No se volvió a correr la escena noche de spotify-xp: la rampa solo cambia con la madrugada apagada y por debajo de la noche (la escena
la deja encendida al 70 %) y el corte del relevo solo en un disco continuo. Nada oído ni probado en el A4 ni en el bar.
