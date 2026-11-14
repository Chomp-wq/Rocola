# Fase 11: lo propio de Rocola OS, encima de la rocola de XP traducida — el plan

14-11-2026, sobre `fc7b667`. Junta lo que estudiaron tres agentes en solo lectura (el inventario de lo que falta, la arquitectura y las
propuestas visuales), revisado contra el código. **No se cambió ni se compiló nada.** Es una propuesta: **no se empieza nada hasta que el
dueño la apruebe** (decisión del 14-11-2026: «con mucho cuidado: primero un plan que aprueba el dueño»). **Nada de esto está probado en
el A4.**

> Si hay poco tiempo: el resumen (§1), las propuestas visuales para elegir (§5) y las decisiones (§6). Lo demás es para los grupos que
> harán el trabajo.

## 1. Resumen

**Qué es la fase 11.** Desde el 14-11-2026, la rocola de Rocola OS es la de Windows XP traducida a Linux (`rocola-xp`, publicada en
sistema-37), y es la de XP tal cual. Lo que el programa anterior de Rocola OS (`rocola-consola`) hacía de más hoy no está: la lista está
en `sistema/INTEGRACION_ROCOLA_XP.md` §8. La fase 11 lo vuelve a poner **encima** de la de XP, sin cambiarla (regla del dueño del
8-11-2026).

**Lo que se queda idéntico a XP.** Todo lo de XP:
- su código: `Codigo/` sigue siendo Rocola/ con solo las líneas de traducción;
- su aspecto y su sonido de fábrica;
- sus botones, Ctrl+F3 y Ctrl+F2;
- su apagado: el botón borra el estado; el apagón lo conserva.

Lo nuevo vive aparte y se enciende con una sola variable que pone el lanzador (`ROCOLA_OS`). **Sin ella, `rocola-xp` es exactamente la
de hoy**, y se sigue comprobando como en el port, contra la copia congelada de la referencia.

**Lo que se añade.**
- **Lo no visual**, encima de XP:
  - lo que el sistema le pide al programa: reiniciarse o apagarse «cuando esté libre», un sistema nuevo el mismo día, lo que suena
    para el buzón y el diagnóstico pedido desde casa;
  - mirar si hay internet;
  - el botón derecho y la rueda del ratón;
  - lo que no debe fallar en el bar: los botones aunque falle la pantalla gráfica, la hora que salta, las portadas gigantes y el
    vigilante del bucle;
  - el sonido propio de Rocola OS (24 bits, sin chasquidos), los «MP3» que no lo son y los videos medidos;
  - el buscador que entiende faltas y letras;
  - Spotify por dentro;
  - la IA que aprende en reposo.
- **Lo visual.** Lo del programa anterior **no se trasplanta**: las mejoras V, el vidrio y la letra, el cielo H/F/O, el color vivo, el
  movimiento y las vistas de Spotify. En su lugar van **24 propuestas nuevas (K1 a K24)**, que conservan el aspecto de XP y solo le
  añaden:
  - 12 hacen falta para usar lo no visual: la hoja de Rocola OS, la red, los avisos, la flecha del ratón y Spotify a la vista;
  - 3 ayudan, 8 solo embellecen y 1 es para el tablero de Rocola en casa.
  - El dueño elige cuáles.

**Cómo se hace.** En oleadas, como el port (`PLAN_TANDA4_A_N7.md`): grupos en ramas aparte, revisión independiente y la comprobación
completa al juntar.
1. Primero, lo que más falta hace en el bar y lo que desbloquea lo demás (oleadas 0 a 2). Nada de eso se ve ni se oye distinto.
2. Después, la hoja de Rocola OS, el sonido y el buscador (oleadas 3 y 4).
3. Spotify, la IA y lo que solo embellece, cuando el dueño elija (oleadas 5 a 9).
4. Al final, los documentos y la lista de lo que hay que probar en el A4 (oleada 10).

**Lo que tiene que hacer el dueño.** Leer las 13 decisiones (§6) y elegir las propuestas (§5). Con las decisiones 1, 3 y 10 ya se pueden
empezar las oleadas 0 y 1.

## 2. La arquitectura y las reglas de trabajo

### 2.1 La idea

Con lo de Rocola OS apagado, `rocola-xp` tiene que ser la rocola de XP traducida de hoy, igual byte a byte en todas las comparaciones.

Lo nuevo vive aparte y solo entra a XP por **ganchos de una línea**, listados y reversibles. Cada gancho devuelve lo de XP mientras lo suyo
está apagado.

### 2.2 Dónde va el código

- **Una carpeta nueva, `sistema/rocola/Xp/RocolaOs/`.** Se compila sola: `RocolaXp.csproj` solo deja fuera bin, obj, Referencia y Pruebas.
  - `Codigo/`: XP, la traducción y los ganchos.
  - `Nativo/`: lo de Windows hecho en Linux.
  - `Plataforma/`: lo de Linux que no es de XP, y los ganchos de prueba.
  - `RocolaOs/`: lo que XP nunca tuvo.
- **El interruptor general `ROCOLA_OS`** es una variable que `/usr/bin/rocola` pone y `RocolaOs.cs` lee una vez al arrancar:
  - `1`: lo de Rocola OS, según su archivo de ajustes;
  - una lista (`vida,raton`): solo eso, para las pruebas;
  - sin la variable, o `0`: nada de la fase 11.
  - Es el mismo modelo que `ROCOLA_IA_DATOS` en `Nativo/DatosGrandes.cs`: «sin la variable, como XP».
  - Hoy nadie la pone (comprobado: no hay ninguna `ROCOLA_OS` ni en el programa ni en los guiones), así que todas las pruebas del port
    siguen valiendo sin tocarlas.
- **Los ajustes propios van en `/rocola/datos/xp/rocola_os.txt`**, no en `rocola_config.txt`.
  - El motivo: XP reescribe `rocola_config.txt` ENTERO con solo sus claves (`Form1.Datos.cs`, `TextoConfig`, 727-888, desde
    `SaveConfigPrefs` y el guardado al momento de Ctrl+F3 y Ctrl+F2). Una clave de Rocola OS escrita allí se borraría en el primer
    guardado.
  - Se escribe con el `EscribeSeguro` de XP, por su `GuardaDetras`; así el cierre también espera a que acabe (`WaitDataWrites`).
  - Entra en el respaldo diario por su cuenta, sin tocar la lista de XP.
  - `rocola_config.txt`, el importador y las `dir=` del lanzador no cambian.
- **Lo demás va en partes de Form1** (`public partial class Form1`), como ya hacen `Nativo/Form1.Ventana.cs` y `Plataforma/VolcadoEstado.cs`.
  - Desde ahí se ve lo privado de XP sin tocar `Codigo/`:
    - los campos: `nowPlaying`, `queue`, `credits`, `lastInput`, `searchOpen`, `videoOn`, `maintMode`, `diagEnMarcha`...;
    - los métodos: `RutaDatos`, `EscribeSeguro`, `LogError`, `ShowToast`...;
    - los tipos: `Track`, `PlayItem`, `IAudioSource` y las piezas de Ctrl+F3.
  - La fuente de sonido de Spotify TIENE que ir en una de esas partes: `IAudioSource` es privada de Form1 (`Form1.Sonido.cs:1679`).
  - Reglas para esas partes:
    - todos los nombres empiezan por `RocolaOs`;
    - sin inicializadores de campo con efectos (correrían dentro del constructor de XP);
    - sin constructores estáticos que trabajen.
- **El arranque y el cierre no tocan `Codigo/`.**
  - Una línea en `Nativo/PantallaRocola.Rocola.cs`, en `Corre(Form1)` (63-72, antes de `rocola.Ventana.Ensena()`):
    `RocolaOs.Arranca(rocola)`, que sin `ROCOLA_OS` no hace nada.
  - En `Nativo/Form1.Ventana.cs`, en `AbreVentana` (35: `a.Cerrandose = OnFormClosing`), un envoltorio para que Rocola OS sepa la
    razón del cierre antes de que XP guarde.
  - Las órdenes de prueba de la línea de mandatos van con `[ModuleInitializer]`, como `VersionRocolaOs` e `ImportaXp`.
- **Del programa anterior se COPIA el algoritmo, citando de dónde sale. Nunca se enlaza `Motor/`**: 65 de sus 85 archivos declaran
  `partial class Form1` (la de aquel programa) y se fundirían con la de XP. Lo que se copia:
  - `Consola/Conexion.cs`, casi tal cual;
  - la lógica de `Consola/Actualizacion.cs` (las marcas de `/run` y `vivo.txt`), con «libre» hecho sobre los campos de XP;
  - de `Motor/Spotify.cs`, el enchufe (`ControlSpotify`, con un JSON que funciona en NativeAOT) y el búfer del sonido (`SpotifyEnVivo`);
  - los algoritmos de `SpotifyMas.cs` (SP4), `SustitucionSpotify.cs` (SP2: A, B y C) y `VolumenSpotify.cs`;
  - la fonética de `BuscadorFlexible.cs` y el índice de `BuscaLetras.cs`;
  - `Conversiones.cs`;
  - `EntrenaIa.cs`, `IaNueva.cs` y `CortesIa.cs`.
  - `sistema/rocola-spotify` (Rust) no cambia.
  - `Gl.cs`, `Kms.cs` y `Botones.cs` siguen enlazados como hoy: tocarlos cambia `rocola-xp`.
- **No se copia lo que duplica a XP**: `DeXP.cs`, la cadena, la cola, los créditos, la biblioteca, el buscador, el modo noche, los ajustes,
  la configuración, el importador, la salida, los lectores, `Prioridad.cs` y `Acentos.cs`.
- **Tampoco lo que era del dibujo del programa anterior**: `Consola/Pantalla`, `MejorasVisuales.cs`, `LetrasVista.cs`, `Raton.cs`,
  `VistaTexto.cs`, `Diagnostico.cs` y `Controlador.cs`. De `CieloClima.cs` y `SpotifyPortadas.cs` solo se aprovecha lo que no dibuja.

### 2.3 Los ganchos en `Codigo/`

- **Un gancho es UNA línea que ya existe, cambiada en su sitio.** Nunca se añade, se quita ni se mueve una línea de `Codigo/`.
  - El motivo: las trazas nombran cada reloj y cada encargo por archivo:línea (`Nativo/Reloj.cs` y `PantallaRocola.Encola`, con
    `CallerFilePath` y `CallerLineNumber`: `TrazaVentanas.Sitio`), y la referencia congelada tiene esas líneas.
  - Una línea de más cambia el nombre de todo lo de debajo, y las escenas salen DIFERENTE. Ya pasa con los cuatro archivos que no tienen
    las líneas de XP: `compara_trazas.py` tiene que traducir `D3D9Star.cs:1997` a `1867`.
- **Cómo es un gancho:**
  - envuelve una expresión, o pone una llamada al principio o al final de la línea;
  - llama a UN método `RocolaOsXxx`, que devuelve lo de XP si lo suyo está apagado;
  - lleva la marca `// ROCOLA OS gNN: (XP) <código de antes> — <para qué>`, como las de «EN LINUX: (XP)»;
  - conserva los finales de línea de antes (`Pruebas/Windows/finales.py`). Ojo: `Codigo/Form1.Video.cs` va todo en LF.
- **El manifiesto, `RocolaOs/GANCHOS.tsv`**: por cada gancho, el id, el archivo, la línea, la línea de antes y la de ahora. Una
  herramienta nueva, `Pruebas/RocolaOs/ganchos.py`, comprueba:
  - que cada línea del manifiesto está así en `Codigo/`;
  - que ninguna otra lleva «ROCOLA OS»;
  - que, devolviendo las de antes, `Codigo/` queda byte a byte como en la base `fc7b667` (los 109 archivos, con las mismas líneas y
    CRLF);
  - cuántos ganchos hay y en qué archivos.
- **`diff-xp.py --sin-ganchos`** aplica el manifiesto al revés antes de comparar, para que las cuentas de la traducción no se muevan, y
  cuenta los ganchos aparte. Hoy, en los 109 archivos: 5 095 líneas quitadas, 4 828 puestas y 229 sin marca (comprobado).
- Fuera de `Codigo/` (Nativo, Plataforma), los relojes y encargos llevan nombre fijo, así que no mueven trazas. Aun así, cada sitio que se
  toca lleva la marca y va en el manifiesto.

**Los ganchos que ya se saben para lo no visual**: 11 líneas en 7 archivos, más los 7 que ya existen. Pueden hacer falta unos pocos más,
que el grupo cuenta y anota antes de empezar: el DSEE en el billar (`CadenaBillar.cs`), pedir y pagar canciones de Spotify, y la espera de
20 s en la cola (`PlayNext`). Los de lo visual se asignan cuando el dueño elija.

| Id | Archivo:línea (hoy) | Para qué | Oleada |
|---|---|---|---|
| g01-g07 | `Form1.Ia.cs` 61, 70, 251, 322 y 493; `Form1.Datos.cs` 2483; `Form1.Portadas.cs` 718 | Ya existen (lo grande va en `ROCOLA_IA_DATOS`, desde el 14-11). Son de Rocola OS, no de traducción: solo se les cambia el comentario | 0 |
| g08 | `SalidaSonido.cs` 241 (el `return false;` de `FlotanteAdmitido`) | 24 bits: la salida de XP para «Vista o posterior» (coma flotante), que `Nativo/SalidaAlsa.cs` pasa a 24 bits con dither. Si conviene más, la línea 287 | 4 |
| g09 | `Form1.Sonido.cs` 1458 (`new FileAudioSource(path)`) | Abrir la copia convertida de un «MP3» que no lo es | 4 |
| g10 | `Form1.Sonido.cs` 664 (`rd = new LectorMedida(path);`) | Que el repaso de XP mida esa copia bajo la ruta del original | 4 |
| g11 | `Form1.Video.cs` 44 (`videoSinMedir = true`) | Medir los videos, como hacía XP en Vista o posterior | 4 |
| g12 | `Form1.Biblioteca.cs` 3516 (`searchOverlay.Results = res;`) | El buscador flexible y por letra: lo de más va DEBAJO de lo de XP | 4 |
| g13 | `Form1.Sonido.cs` 1456 (`string original = RutaOriginalDe(path);`) | Que una ruta «spotify:» se abra con la fuente de Spotify | 5 |
| g14 | `AperturaAudio.cs` 238 (`RutaQueSonaria`: `return path;`) | SP2: la precarga y el relevo abren la pareja de Spotify. XP dejó este sitio para eso: «el día que esta rocola tenga otra fuente, se cambia aquí y en un solo sitio» | 6 |
| g15 | `Form1.Datos.cs` 3385 (`PlayTrack(nowPlaying.Song);`) | SP2: suena la de Spotify, y todo lo demás sigue siendo de la tuya | 6 |
| g16 | `Form1.SinPausa.cs` 135 (`c = ArmaCadena(sig.Song, lector, iaMejorada, 0);`) | SP2 en el relevo sin pausa | 6 |
| g17-g18 | `Form1.Sonido.cs` 852-853 (el bucle sobre `IaCutsHz`) | IA2: también los cortes de tu biblioteca | 8 |

Los sitios fuera de `Codigo/` (marcados igual):
- `Nativo/PantallaRocola.Rocola.cs` (el arranque);
- `Nativo/Form1.Ventana.cs` (la razón del cierre);
- `Plataforma/TeclasLinux.cs` (el ratón);
- `Nativo/Anfitrion.cs` y `Nativo/Imagenes.cs`;
- `Nativo/SalidaAlsa.cs` y `Nativo/SalidaPropiaAlsa.cs`;
- `Nativo/LectorAudio.cs`, `Nativo/ReproductorVideo.cs` y `Nativo/DescompresorMp3.cs`;
- `Nativo/PantallaRocola.cs` (el vigilante);
- `Nativo/ImportaXp.cs`.

### 2.4 Hilos, bucle, datos y cierres

**El hilo de la pantalla.** Rocola OS solo entra en él de dos maneras:
- un reloj propio de 1 s con nombre fijo, el «latido» (`new Reloj("RocolaOs:latido", null)`, el constructor con nombre de
  `Nativo/Reloj.cs`), que solo se crea con `ROCOLA_OS`. Vence con la regla de WM_TIMER, y como mucho lee el estado de XP y mira
  archivos de `/run` (que está en memoria);
- encargos con nombre fijo (`PantallaRocola.EncolaDesde(..., "RocolaOs:...", false)`).

Lo que tiene que esperar (el disco, la red, ffmpeg, `rocola-spotify`, el entrenamiento) va en hilos propios. Además:
- nada en el reposo: una excepción ahí cierra el programa, como en WinForms;
- nada de `await` en el hilo de la pantalla;
- nada de OpenGL fuera de él;
- el estado de XP se lee solo en su hilo; a los demás hilos se les pasa una copia.

**Los hilos de Rocola OS:**
- son de fondo y lo atrapan todo: una excepción sin atrapar en otro hilo cierra el programa (`Program.cs` solo la apunta), y el lanzador
  lo cuenta como caída;
- su prioridad se pone con `Nativo/PrioridadHilo.cs`: en Linux un hilo hereda el nice de quien lo crea, y uno creado desde la pantalla
  correría a −6;
- lo de fondo va por debajo del repaso de XP;
- ningún cerrojo se comparte con el hilo del sonido.

**Los datos:**
- lo pequeño, en `/rocola/datos/xp`, con nombres `rocola_*.txt` (así `rocola-instalar` ya les traduce las rutas);
- lo grande (los WAV convertidos, el material de la IA), por `DatosGrandes.Ruta`;
- lo vivo, en `/run` (con `ROCOLA_RUN` para las pruebas, como en los guiones);
- las llaves (`rocola_github.txt`, `rocola_buzon.txt`, `rocola_genius.txt` y `/rocola/datos/spotify`) se leen donde están, y nunca se
  copian, se apuntan ni se suben;
- en `/rocola/datos` (lo de `rocola-consola`) no se escribe: el sistema anterior, con ESC al prender, abre `rocola-consola` con esos datos.
  La única excepción es la cuenta de Spotify, si la decisión 6 la comparte.

**Los cierres** siguen la regla del 8-11-2026 y no cambian:
- SIGTERM = apagado (`WindowsShutDown`): borra el estado;
- SIGUSR1 = reinicio propio (`TaskManagerClosing`): lo conserva;
- apagón: no corre nada.

Las salidas propias de Rocola OS van por esas dos vías (`CierreDeFuera` y `FinDeSesion`, en `Nativo/PantallaRocola.Ventana.cs`), y antes
ponen `Environment.ExitCode`, porque el `Main` de XP es `void`:
- 42, 43 y 46 conservan el estado;
- 44 lo borra, como el botón;
- 45 lo decide el dueño (decisión 3).

El lanzador ya atiende 42-46. Parar `rocola-spotify`, ffmpeg y el entrenamiento va en el envoltorio del cierre, y cabe en los 20 s que dan
el lanzador y el buzón.

### 2.5 El contrato con los guiones del sistema

- **Las marcas** que deja el sistema y que el programa atiende «cuando esté libre»:
  - `/run/rocola-reinicia-programa` → 42;
  - `/run/rocola-reinicia-equipo` → 46;
  - `/run/rocola-apaga-equipo` → 44;
  - `/run/rocola-reinicio-sistema` → 43;
  - `/run/rocola-pide-diagnostico` → el diagnóstico completo de XP;
  - `/run/rocola-hay-nueva` → 42, solo si vuelve el canal del programa (decisión 9).
- **«Libre»** quiere decir todo esto a la vez (se mira en el latido):
  - nada sonando ni en la cola, y sin video;
  - sin buscador, ayuda ni asistente;
  - sin Ctrl+F3, Ctrl+F2, avisos ni F12 abiertos (`Hoja.Abiertas`);
  - sin F10 ni el diagnóstico (`maintMode`, `diagEnMarcha`);
  - y 2 minutos sin botones (`lastInput`, como `CheckBusinessDayReset`, `Form1.Datos.cs:2466`).
- **`/run/rocola-xp-sabe`** dice lo que atiende el programa que corre, una palabra por renglón (`reinicia-programa`, `reinicia-equipo`,
  `apaga-equipo`, `reinicio-sistema`, `pide-diagnostico`, `vivo`...).
  - El buzón y `rocola-actualiza` solo vuelven a aceptar lo «cuando esté libre» si ese archivo lo dice.
  - Así, un sistema vuelto atrás, con un `rocola-xp` viejo, lo sigue rechazando como hoy.
- **`/run/rocola-vivo.txt`** se escribe cada 15 s de un tirón (`.n` y mover), con los mismos renglones que el programa anterior: el buzón
  ya lo sube tal cual en `estado.json`.

### 2.6 Cómo se comprueba

**Nivel 0, la puerta de cada cambio, SIN `ROCOLA_OS`.** Es la batería de la última junta del port (`verifica-junta17.sh`):
- compila desde cero (0 errores, los 13 avisos), y NativeAOT sin avisos IL/AOT;
- todos los arneses IGUALES: acabado-estilo 0 de 6 603, vistas-b 0 de 3 108, Ctrl+F3 0 de 545; el fondo, solo con su línea de marca;
- el banco (semillas 7, 11 y 5) y las 29 cintas;
- la hoja 86 de 86, la pantalla 32 de 32 y PantallaVistas 78 de 78;
- los recorridos, 9 de 9 con 0: capas 213 de 217 (las de siempre) y texturas dentro del ruido medido;
- las 24 escenas IGUALES salvo lo aceptado (sigterm, f3sigterm, los rótulos del repaso en tab y el reloj saturado de F12), y las 7 de
  NativeAOT;
- `cierre_rocola_os.sh` 43 de 43, en JIT y en AOT; y `Pruebas/Importa`;
- y además, lo nuevo: `ganchos.py`, y `diff-xp.py --sin-ganchos` con las mismas cuentas que hoy.

Como los ganchos devuelven lo de XP sin `ROCOLA_OS`, la referencia congelada (aef2211) sigue valiendo tal cual.

**Nivel 1, «Rocola OS encendido, pero sin nada».** Las 24 escenas y los 9 recorridos con `ROCOLA_OS=1`, contra la misma referencia:
- con una carpeta `/run` de prueba vacía, sin red (otro espacio de red), sin `rocola-spotify` y sin ratón;
- con apagado todo lo que cambia lo que se ve o se oye: el buscador flexible, los videos medidos, Spotify, las conversiones, el ratón y
  el sonido nuevo.

Tienen que salir IGUALES quitando solo los relojes y encargos «RocolaOs:» (para eso, `compara_trazas.py` necesita una opción `--ignora`).
Además:
- `sonido.sh` con y sin `ROCOLA_OS`: lo sonado, igual (`difsonido.py`);
- el coste: los intervalos de cuadro p95, p99 y máximo con NativeAOT, con y sin (regla del 9-10-2026), y cuánto tarda el latido.

**Nivel 2, cada función con lo suyo encendido**, en JIT y en NativeAOT, con pruebas y dobles DENTRO del repositorio (`Pruebas/RocolaOs/`).
- Hoy faltan, y hay que rehacerlos: el `rocola-spotify` simulado, el Genius simulado, el Open-Meteo simulado y el GitHub simulado de
  Rocola en casa. Antes se probó «con el simulado», pero no quedaron como guion.
- La tarjeta de prueba con reloj (`Pruebas/Windows/alsa/reloj.c`) sí está, pero en 16 bits: para los 24 bits hay que ampliarla.

**Nivel 3, el sistema:**
- `sistema/rocola/Pruebas/Lanzador/prueba.sh` con el `rocola-xp` de verdad (NativeAOT) y el BusyBox del sistema;
- el sistema entero en QEMU (`sistema/rocola/Pruebas/Qemu`).

Nada de esto dice nada del A4. Lo que se mide fuera de pantalla con llvmpipe sirve para comparar, no para afirmar fluidez.

### 2.7 Reglas de trabajo

**Cómo se reparte y se junta el trabajo:**
- Cada oleada son grupos en ramas aparte (`fase11-<grupo>`), y en una oleada ningún archivo de `Codigo/` está en dos grupos.
- Cada grupo tiene su revisión independiente, que hace siempre el modelo de la sesión.
- El modelo más ligero solo hace grupos chicos y mecánicos (decisión del 12-11-2026).
- Al juntar, la comprobación completa queda apuntada en `FASE11_JUNTAS.md`, y cada grupo deja su `FASE11_<grupo>.md`.

**Lo que no se puede hacer:**
- Nunca se añade, se quita ni se mueve una línea de `Codigo/`: solo ganchos de una línea, listados en el manifiesto.
- Nada nuevo se ve ni se oye sin `ROCOLA_OS`. Por eso las pruebas contra la referencia siguen valiendo.
- Lo visual solo se hace con el «sí» del dueño. Cada opción visual lleva su interruptor persistente en la hoja de Rocola OS; las
  optimizaciones que no cambian el aspecto son automáticas.

**Las reglas de siempre:**
- Nada suena sin créditos.
- Nunca se graba música: las portadas de Spotify van a `/run`.
- Las llaves nunca van en el código, en los registros ni en el buzón: el repositorio es público.
- Los guiones del sistema usan BusyBox sin `stat` (las fechas, con `date -r`) y se prueban con el BusyBox del sistema.
- No se añaden módulos a GRUB sin medir que el arranque sigue cabiendo en 31 744 bytes (la fase 11 no lo necesita).
- Las compilaciones de GitHub (la del sistema, la del programa y la de casa), solo cuando el dueño las pida.
- No se afirma nada del A4 sin probarlo allí. Se separan las pruebas automáticas, las comprobaciones a la vista y las medidas (p95,
  p99 y máximo, con NativeAOT).

**Lo que hay que mirar antes:**
- Si cambia Rocola/ (XP sigue en el bar), el cambio se pasa a `Codigo/` como siempre, y `ganchos.py` dice si algún gancho se movió.
- Antes de traer algo del programa anterior, mirar si XP ya lo hace (§3.7). Dos lógicas para lo mismo se pelean.
- Los nombres. XP y el programa anterior repiten etiquetas para cosas distintas (C1, G1, L1, M1, P1, S1, I1...), así que en este plan
  cada cosa se nombra por su clave de ajustes o por su nombre, y las propuestas nuevas son K. Ojo: `cielo_hora=` es una clave vieja que XP
  ignora.

## 3. El inventario

Lo que tenía `rocola-consola` y `rocola-xp` no, función por función. En las tablas:
- **Tam.** es el tamaño: chico, medio o grande.
- **Del A4** es lo mínimo que solo se puede saber allí.
- **Ol.** es la oleada (§4).
- Lo visual de cada función va en §5.

### 3.1 La vida en Rocola OS: lo que el sistema espera del programa

| Función | Qué hace | Viene de | Dónde engancha en rocola-xp | Tam. | Riesgo | Del A4 | Ol. |
|---|---|---|---|---|---|---|---|
| Las salidas 42 a 46 | Cerrarse a propósito con un código que el lanzador ya atiende: 42 otra vez, 43 sistema nuevo, 44 apagar, 45 volver a XP, 46 reiniciar el equipo | 1-11 y 7-11 · `Consola/Controlador.cs`, `Actualizacion.cs` | `Environment.ExitCode` y las dos vías de cierre de `Nativo/PantallaRocola.Ventana.cs`; sin ganchos | chico | medio: con qué cierre va cada uno (decisión 3) | `poweroff` y `reboot` de verdad | 1 |
| `/run/rocola-vivo.txt` | Cada 15 s, para el buzón: lo que suena, la cola, los créditos, el último botón, el modo noche, Spotify y la pantalla de delante | 7-11 · `Consola/Actualizacion.cs` | el latido; sin ganchos | chico | bajo | — | 1 |
| Reiniciar el programa, el equipo o apagar «cuando esté libre» | El buzón deja la marca y la rocola, libre, sale con 42, 46 o 44 | 7-11 · `Consola/Actualizacion.cs`, `rocola-buzon` | el latido; el buzón las vuelve a aceptar con `/run/rocola-xp-sabe` | chico | medio | el reinicio y el apagado de verdad | 1 |
| Un sistema nuevo el mismo día | Con `/run/rocola-reinicio-sistema`, libre, sale con 43 (hoy entra al siguiente encendido) | 1-11 · `Consola/Actualizacion.cs` | el latido | chico | medio: un reinicio a deshoras si «libre» se equivoca | el reinicio con las dos copias del sistema | 1 |
| El diagnóstico pedido desde casa | Libre y con la marca, corre el diagnóstico completo de XP; el buzón sube el informe | 7-11 · `Consola/Actualizacion.cs` | `DiagnosticoCompletoF3` de XP desde el latido; el buzón toma el `rocola_diagnostico_*.txt` más nuevo de `/rocola/datos/xp` (hoy espera otro nombre, en otra carpeta) | chico | medio: enseña sus escenas en la pantalla del bar (por eso, solo libre) | lo que mide solo vale allí | 1 |
| El buzón | Ya sube la versión, los errores, los registros y los archivos de `rocola-xp`; faltan lo vivo, el diagnóstico y la bitácora de Spotify | 7-11 · `rocola-buzon` | el guion | chico | bajo | el buzón de verdad | 1 |
| Rocola en casa | Hoy compara con `programa-N` (de `rocola-consola`), dice que apagar conserva los créditos (contra la regla del 8-11) y tiene un botón que compila `rocola-consola` | 7-11 · `sistema/casa` | `sistema/casa` (su compilación, solo a pedido) | chico | bajo, pero confunde | — | 1 |
| «Actualizarse sola» y «Buscar actualización ahora» | `actualizar_solo=`; el botón deja `/run/rocola-buscar-ya` | 1-11 · `Motor/Ajustes.cs` | la clave en `rocola_os.txt` (`rocola-actualiza` la lee ahí y, si no está, en el archivo de antes); los mandos, en la hoja de Rocola OS (K11) | chico | bajo | — | 1 y 3 |
| El canal del programa | Bajar y poner `rocola-xp` sin el sistema entero | 1-11; apagado el 14-11 | solo salir con 42 al ver `/run/rocola-hay-nueva`; lo demás es de GitHub y los guiones | medio | alto: ya no hay `rocola-consola` de respaldo | que el bajado corra | decisión 9 |
| «La rocola»: reiniciar el programa o el equipo, apagar, volver a Windows XP | Botones de doble toque en 5 s | 7-11 «2 y 4» · `Motor/Ajustes.cs` | la hoja de Rocola OS (K11) y las salidas | medio | medio-alto: el 45 toca el arranque (`rocola-desinstalar` ya lo comprueba todo) | `rocola-desinstalar` en el disco de verdad | 3 |
| «Traer otra vez lo aprendido en XP» | Las tablas pasan a `.antes-xp` y se vuelven a traer, sin la configuración ni los botones | 7-11 «2 y 4» · `Motor/Arranque.cs` | `Nativo/ImportaXp.cs` (la marca `rocola_reimportar_xp`); el botón, en la hoja de Rocola OS | chico | medio: lo medido desde la importación pasa a `.antes-xp` | la carpeta de XP de verdad | 3 |

### 3.2 Internet, el ratón y lo que no debe fallar en el bar

| Función | Qué hace | Viene de | Dónde engancha en rocola-xp | Tam. | Riesgo | Del A4 | Ol. |
|---|---|---|---|---|---|---|---|
| Mirar si hay internet | Un hilo de fondo mira la tarjeta, el cable, la dirección, el router, internet (TCP a 1.1.1.1:443 o 8.8.8.8:53) y los nombres, cada 30 s (cada 5 s con la hoja de Rocola OS o Ctrl+F3 abiertos) | 7-11 · `Consola/Conexion.cs` | `RocolaOs/Red.cs`; su estado, en `vivo.txt` | chico | bajo | la Realtek, el cable y el router del bar | 2 |
| Dónde se ve la red | La hojita Internet y la marca en la barra de Ctrl+F3 | 1-11 y 7-11 | K11 y K12 (§5) | medio | bajo | la red de verdad | 3 |
| Botón derecho = REGRESAR, rueda = ARRIBA/ABAJO | En la pantalla de la rocola; en las hojas sigue el ratón de XP de siempre | 7-11 · `Consola/Raton.cs` | `Plataforma/TeclasLinux.cs` (196-227): la tecla configurada, por `LlegaTecla`; sin ganchos | chico | medio: que no se cruce con las combinaciones del rojo | un ratón | 2 |
| Clic en portadas, letras y canciones, y la flecha | En modo tarjeta (el del A4), XP solo atiende el clic del buscador, la ayuda y el asistente, y esconde la flecha | 7-11 · `Consola/Raton.cs` | reenviar el clic a las vistas de XP que ya lo atienden en modo procesador (`Form1.Pared.cs:1142`, `Form1.Vistas.cs:250`), como XP ya hace con el buscador (`Form1.Video.cs:866-883`); la flecha, con K13 | grande | medio: que no cobre sin querer | ratón y, si la hay, pantalla táctil | 3 |
| Sin pantalla gráfica, los botones siguen | Si KMS falla, hoy `rocola-xp` sigue «fuera de pantalla» y NO lee los botones (`Nativo/Anfitrion.cs`) | 1-11 · `Consola/VistaTexto.cs` | `Nativo/Anfitrion.cs`: arrancar las teclas también si KMS falló | chico | bajo de hacer; alto si no se hace | que la Radeon falle (no se sabe si pasa) | 2 |
| La hora que salta | Si la hora salta más de 2 min (el reloj de la placa, NTP), el día del negocio y la inactividad se corren lo mismo, y se apunta | 4-11 · `Motor/Creditos.cs` | el latido corrige `stateDay` y `lastInput` | chico | medio: toca el día del negocio | la pila del reloj de la placa | 2 |
| Portadas gigantes | Más de 64 Mpx o de 64 MB: sin portada (en el A4, Linux mataría la rocola por falta de memoria) | 4-11 · `Consola/Pantalla/Dibujo.cs` | `Nativo/Imagenes.cs`: mirar la cabecera antes de descomprimir | chico | bajo | la memoria del A4 | 2 |
| El vigilante del bucle | Si la pantalla no da una vuelta en 60 s, se apunta y el programa sale; el lanzador la vuelve a abrir (como tras un apagón) | 4-11 · `Consola/Controlador.cs` | `Nativo/PantallaRocola.cs` (un latido por vuelta, también en los bucles de las hojas) y un hilo | chico | medio: un falso aviso corta la música | la vuelta más larga de verdad | 2 (decisión 11) |
| La copia de los botones | Si falta `rocola_teclas.txt`, se toma del respaldo diario de XP (que ya lo copia al disco de la rocola) | 4-11 · `Motor/Arranque.cs` | el lanzador, antes de abrirla | chico | bajo | — | 2 |

### 3.3 El sonido y los archivos

| Función | Qué hace | Viene de | Dónde engancha en rocola-xp | Tam. | Riesgo | Del A4 | Ol. |
|---|---|---|---|---|---|---|---|
| 24 bits y silencio exacto | La última etapa en 24 bits, con su dither, si la tarjeta los admite; donde la cadena da cero exacto, silencio exacto | 3-11 · `Motor/SalidaAlsa.cs`, `Dither.cs` | g08 y `Nativo/SalidaAlsa.cs` | medio | medio: deja de salir bit a bit como XP | qué admite la Realtek; oírlo | 4 |
| La frecuencia que la tarjeta no tiene | Si no admite la de la canción (22 o 32 kHz), el convertidor de la rocola en vez del lineal de ALSA | 3-11 · `Motor/SalidaAlsa.cs` | `Nativo/SalidaAlsa.cs` y el convertidor de XP (`frecuencia_salida=`) | medio | medio | qué frecuencias admite | 4 |
| Saltar sin chasquido | Al SALTAR, lo que sigue baja a cero en 60 ms; igual al apagar y al cortar la prueba de bocinas | 4-11 · `Motor/SalidaAlsa.cs` | `Nativo/SalidaPropiaAlsa.cs` y `Nativo/SalidaAlsa.cs`; sin ganchos | medio | medio: el salto llega un poco después | oírlo con el amplificador | 4 |
| Volver de un corte y acabar suave | Tras un corte (xrun), lo que sigue entra en 10 ms; una canción que acaba de golpe, con 10 ms hasta cero | 4-11 · `Motor/SalidaAlsa.cs` | `Nativo/SalidaAlsa.cs` | chico | bajo | un corte de verdad solo se da allí | 4 |
| El hilo del sonido en tiempo real | SCHED_FIFO 20 para el hilo que escribe en la tarjeta | 1-11 · `Motor/Prioridad.cs` | `Nativo/PrioridadHilo.cs` | chico | medio: un hilo así que no suelta congela la pantalla | ver si quita cortes allí | solo si hace falta |
| Los huecos de un MP3 dañado | Donde la música cae a cero en seco, la última muestra baja en 3 ms y lo que vuelve entra en 3 ms | 4-11 · `Motor/Lector.cs` | `Nativo/DescompresorMp3.cs` | chico | bajo-medio: las medidas de esos MP3 cambian frente a las de XP | — | 4 |
| Los «MP3» que no lo son | Un M4A/AAC, WMA, OGG o FLAC con nombre .mp3 se convierte con ffmpeg a WAV en una carpeta aparte; el original no se toca | 4-11 · `Motor/Conversiones.cs` | g09 y g10; la espera de 20 s en la cola y su aviso, con K8 y K10 | medio | medio: escribe en el disco de la música | cuánto tarda allí | 4 (la espera, 7) |
| Los videos medidos | Medir el sonido de cada video con ffmpeg para que suene al nivel de las canciones, como hacía XP en Vista o posterior | 3-11 · `Motor/AudioDeVideo.cs` | g11, `Nativo/LectorAudio.cs` y `Nativo/ReproductorVideo.cs` | medio | medio: ese camino de XP nunca corrió en el bar | cuánto tarda medir; oírlo | 4 (decisión 5) |
| Saltar un video sin golpe | Su sonido baja a cero en 0,3 s | 4-11 · `Consola/Controlador.cs` | `Nativo/ReproductorVideo.cs` | chico | bajo | oírlo | 4 |
| El DSEE en el billar, solo en las muy cortadas | `dsee_billar_cortes=`, apagado de fábrica: con el modo billar, el DSEE solo en las de corte bajo 13 kHz | 3-11 · `Motor/CadenaBillar.cs` | un gancho en `CadenaBillar.cs`; su interruptor, en la hoja de Rocola OS | chico | medio: toca la cadena de XP (apagado, bit a bit) | oírlo en el billar | 8 |
| La cola que espera al disco de la música | Si el disco se desconecta, la cola espera en vez de devolver cada crédito | 4-11 · `Motor/Cola.cs` | tocaría `PlayNext` | chico | bajo | un disco que se desconecta | no por ahora: XP ya devuelve el crédito de cada una |

### 3.4 El buscador

| Función | Qué hace | Viene de | Dónde engancha en rocola-xp | Tam. | Riesgo | Del A4 | Ol. |
|---|---|---|---|---|---|---|---|
| El buscador flexible | Si lo de XP da menos de 40, una segunda pasada rellena DEBAJO con lo que suena igual, con faltas y sin palabras vacías; lo de XP manda | 7-11 · `Motor/BuscadorFlexible.cs` | g12; la segunda pasada, en su hilo | medio | medio: cambian las búsquedas que hoy dan menos de 40 | el tiempo por tecla | 4 |
| Buscar por la letra de tus MP3 | Un repaso de fondo guarda las palabras de la letra de cada MP3 (no la letra), y una frase de 4 palabras o más la encuentra | 7-11 · `Motor/BuscaLetras.cs` | g12 y un repaso nativo a la prioridad mínima | medio | bajo-medio: otro repaso en dos núcleos | el repaso con la biblioteca real | 4 |
| Buscar por la letra en internet (Genius) | Al dejar de escribir 0,8 s, se pregunta a Genius y sus tres primeras van delante | 7-11 · `Motor/BuscaLetras.cs` | la misma pasada, con la tarjeta «NO ESTÁ» (K7) | medio | medio: sus condiciones son de uso no comercial (decisión 13) | la llave y la red | 6 |

### 3.5 Spotify

| Función | Qué hace | Viene de | Dónde engancha en rocola-xp | Tam. | Riesgo | Del A4 | Ol. |
|---|---|---|---|---|---|---|---|
| El núcleo (`spotify=`, apagado de fábrica) | Abre `rocola-spotify` (ya va en el sistema), le habla por su enchufe, y su sonido pasa por la cadena de la rocola | 6-11 · `Motor/Spotify.cs`; `sistema/rocola-spotify` | g13 y una fuente de sonido dentro de Form1 | grande | alto: librespot no es oficial y nunca se probó con una cuenta real | todo, con la cuenta real | 5 |
| SP4: emparejar tu biblioteca | Un hilo busca cada canción tuya en Spotify (una cada 2,5 s) y apunta su pareja si es la misma grabación | 6-11 y 7-11 · `SpotifyMas.cs`, `SustitucionSpotify.cs` | un hilo de fondo sobre la biblioteca de XP | medio | medio: 20 000 canciones son unas 14 h de consultas | los límites de Spotify | 6 |
| SP2: las tuyas cortadas suenan desde Spotify sin que se note | Con la pareja estricta y la comprobación por la forma del sonido (A, B y C); si Spotify no puede, tu MP3 | 6-11 y 7-11 · `SustitucionSpotify.cs` | g14, g15 y g16 | grande | alto: cambia la música sin avisar | la cuenta real; oír que es la misma grabación | 6 |
| SP3 y el volumen de Spotify (V1-V3) | Lo de Spotify se mide y se nivela como lo tuyo | 6-11 y 7-11 · `VolumenSpotify.cs` | `ArmaCadena` de XP por la ruta de Spotify; revisar cada tabla de XP que vea rutas «spotify:» | medio | bajo-medio | la cuenta real | 6 |
| Pedir y pagar canciones de Spotify | Cada una cuesta un crédito y lleva su turno; si no suena, el crédito vuelve; tras un apagón, lo pagado vuelve como crédito | 6-11 · `Spotify.cs`, `Cola.cs` | una canción de Spotify como una de XP con ruta «spotify:», para que XP la cobre, le dé turno y la guarde; los ganchos que hagan falta (cobrar, el estado), contados | grande | alto: toca la cola, los créditos y el estado de XP | la cuenta real | 6-7 |
| Lo que se ve de Spotify | La letra SPOTIFY, el buscador, las letras sincronizadas (SP1), completar discos (SP6), éxitos (SP8), portadas y géneros que faltan (SP5), los artistas | 6-11 y 7-11 | K1-K8, K14 y K23 (§5) | grande | medio | la cuenta real | 6-7 |

### 3.6 La IA y la base

| Función | Qué hace | Viene de | Dónde engancha en rocola-xp | Tam. | Riesgo | Del A4 | Ol. |
|---|---|---|---|---|---|---|---|
| IA1: entrenar en reposo | En reposo (nada sonando, 15 min sin botones, cada 20 h y con material nuevo), una IA del DSEE nueva; nunca se pone sola: el dueño la compara a ciegas y decide | 3-11 · `Motor/EntrenaIa.cs`, `IaNueva.cs` | un hilo nativo; el cambio de IA, con lo de XP; sus botones, en la hoja de Rocola OS | grande | medio: carga en dos núcleos | cuánto tarda; oír la comparación | 8 |
| IA2: los cortes de tu biblioteca | La cosecha simula también hasta cuatro cortes de los que tiene tu biblioteca | 3-11 · `Motor/CortesIa.cs` | g17-g18 | chico | bajo | — | 8 |
| Entrenar también con lo de XP | El entrenador lee en solo lectura el material de la carpeta de XP (y el del programa anterior) | 3-11 · `Motor/MaterialIa.cs` | el entrenador | chico | bajo | el material de verdad | 8 |
| Los ajustes propios | `rocola_os.txt` (§2.2) | 8-11 | `RocolaOs/` | chico | alto si se olvida: interruptores que se apagan solos | — | 0 |
| Lo que dejó `rocola-consola` | Parejas de Spotify, índice de letras, claves de su configuración, la cuenta de Spotify enlazada | 14-11 | según la decisión 6 | chico | bajo | ver qué hay de verdad en el A4 | decisión 6 |

### 3.7 Ya está en rocola-xp: nada que hacer

- **Lo del sistema:**
  - `--version`;
  - los cierres: SIGTERM y SIGUSR1, con SIGHUP ignorada;
  - el importador de lo aprendido en XP, y lo grande en el disco de la rocola;
  - la consola de mantenimiento;
  - lo del lanzador: las caídas, el sistema nuevo que se confirma, la tarjeta del amplificador, `rocola-sonido` y `rocola-prueba`.
- **Devolver el crédito de lo que no suena y no cobrar una dañada.** Esto XP ya lo hace:
  - `AperturaAudio.cs`: `MarcaDanada`, y `AudioStartFailed` → `RefundCredit`;
  - `Form1.Video.cs`, para los videos;
  - `AddItem`, que no cobra una dañada.
  - `INTEGRACION_ROCOLA_XP.md` §8 lo da por perdido: se corrige en la oleada 10.
- **Lo que XP ya resuelve a su manera:**
  - sin tarjeta de sonido, la cola espera (`SalidaSonido.cs`: «el aparato, no la canción»);
  - el video que se traba (el vigía de XP);
  - el modo noche (canciones y videos; Spotify pasará por la misma cadena).
- **Lo demás:**
  - los acentos sin ICU (`UnicodeXp.FormaD`);
  - los teclados y ratones conectados después;
  - la salida a ciegas y el cambio de cuadro con tope;
  - la tarjeta sin HDMI y la tarjeta ocupada;
  - los MP3 con basura (mpg123 busca el siguiente cuadro);
  - el material de la IA fuera de `datos.img`;
  - el odómetro, la sacudida y «Página N de M» (son de XP);
  - la caja negra y el volumen al máximo.

### 3.8 No se trae, y por qué

- **La búsqueda en su propio hilo.** XP ya va rápido con su caché: 3,2 ms con una letra en 20 000 canciones y 0,07 con tres (en la máquina de pruebas). La segunda
  pasada flexible sí va en su hilo.
- **Las optimizaciones del dibujo del programa anterior.** `rocola-xp` dibuja con lo de XP. Las prioridades de los hilos ya las pone
  `Nativo/PrioridadHilo.cs`, con las de XP.
- **RePag/AvPag o Tab para cambiar de pestaña en Ctrl+F3.** En el de XP las pestañas se tocan con el ratón, y el Tab recorre los
  controles, como en WinForms.
- **Los interruptores retirados del programa anterior** (`letras_lupa=`, `letra_lejos=`, `portadas_nitidas=`, `eq_real=`,
  `atraccion_fundidos=`, `avisos_vidrio=`) **y `medio_encendido=1`.** Eran de su dibujo.
- **Una vista de texto si falla la pantalla.** Sería un aspecto nuevo. Lo que importa (los botones y la música) va en la oleada 2; si el
  dueño quiere además la vista, es una propuesta más.
- **La resolución del monitor (V30).** En el bar la pantalla es de 1024×768, la de XP. Si cambia el monitor, se propone.
- **R1 (rellenar los huecos del MP3 bajo el corte).** Nunca se hizo, tampoco en el programa anterior; sigue como idea.
- **La separación de voz.** No se hace (7-11-2026).
- **El video en ventana grande.** `rocola-xp` ya tiene el de XP: el recuadro, y la pantalla completa tras la inactividad.

## 4. Las oleadas

**Cómo se trabaja en cada oleada.** Igual que en el port:
- grupos en ramas aparte, sin compartir archivos de `Codigo/`;
- cada grupo con su revisión independiente;
- la comprobación completa al juntar.

**Lo que se da por hecho en cada grupo** (no se repite en cada uno):
- el nivel 0 y el nivel 1 (§2.6), en JIT y en NativeAOT;
- su documento y su apunte en `FASE11_JUNTAS.md`.

**Antes y después de cada oleada:**
- Ninguna oleada empieza sin las decisiones que necesita (están anotadas en cada una).
- Al acabar, cada oleada puede ir al A4 con la compilación rápida, que lanza el dueño.
- En el A4 se puede apagar todo lo nuevo sin compilar nada (decisión 10).

Al lado de cada grupo va su tamaño y quién lo hace: **sesión** = el modelo de la sesión; **ligero** = el modelo más ligero, con
revisión de la sesión.

### OLEADA 0: el andamio (no se ve ni se oye nada)

**0a. andamio** (medio; sesión)
- Toca:
  - `RocolaOs/`, nuevo: `RocolaOs.cs` (lee `ROCOLA_OS`), los ajustes en `rocola_os.txt` y `GANCHOS.tsv`;
  - `Nativo/PantallaRocola.Rocola.cs` (la línea del arranque) y `Nativo/Form1.Ventana.cs` (el envoltorio del cierre);
  - en `Codigo/`, solo el comentario de g01-g07 (si el dueño lo aprueba);
  - las herramientas: `Pruebas/RocolaOs/ganchos.py`, `Pruebas/Tarjeta/guiones/diff-xp.py` (con `--sin-ganchos`) y
    `Pruebas/Ventanas/guiones/compara_trazas.py` (con `--ignora`).
- Hace: lo de §2.2 y §2.3.
- Depende de: la decisión 1.
- Comprobar:
  - el nivel 0 entero;
  - `ganchos.py`: los 109 archivos, byte a byte, contra la base;
  - `diff-xp.py --sin-ganchos`: las cuentas de hoy;
  - el nivel 1 sin nada que encender: IGUAL.
- En el A4: nada.

### OLEADA 1: lo que el sistema espera del programa

1a y 1b van en paralelo: se entienden por el contrato de §2.5.

**1a. vida** (medio; sesión)
- Toca: `RocolaOs/` (una parte de Form1). Sin ganchos en `Codigo/`.
- Hace:
  - el latido de 1 s y «libre»;
  - las marcas de §2.5, y las salidas 42, 43, 44 y 46, cada una por la vía de cierre que diga la decisión 3;
  - el diagnóstico de XP pedido desde casa (la misma entrada que su botón, `DiagnosticoCompletoF3`);
  - `/run/rocola-vivo.txt`: los renglones de antes y, al final, unos `clave=valor` para el tablero de casa (título, artista, posición,
    duración, cola y créditos);
  - `/run/rocola-xp-sabe`.
- Depende de: 0a y la decisión 3.
- Comprobar `cierre_rocola_os.sh`, ampliado:
  - con cada marca, la rocola sale con su código SOLO estando libre;
  - no sale con música, cola, buscador, Ctrl+F3, F10, asistente o diagnóstico, ni antes de 2 minutos sin botones;
  - el estado se conserva o se borra según la vía;
  - `Environment.ExitCode` llega al lanzador, también en NativeAOT (al acabar el `Main` de XP, con su bitácora y su instancia única);
  - `vivo.txt` dice lo que suena, la cola y los créditos;
  - el diagnóstico deja su informe.
- En el A4:
  - el reinicio y el apagado pedidos desde casa;
  - que «libre» espere de verdad con gente usándola.

**1b. guiones** (medio; sesión)
- Toca: `sistema/board/rocola/rootfs_overlay/usr/bin/rocola`, `rocola-buzon` y `rocola-actualiza`.
- Hace:
  - El lanzador pone `ROCOLA_OS=1`, salvo con `todo=0` en `rocola_os.txt` (el interruptor de emergencia, decisión 10).
  - El buzón:
    - vuelve a aceptar lo «cuando esté libre» y el diagnóstico SOLO si `/run/rocola-xp-sabe` lo dice;
    - toma el informe de XP más nuevo de `/rocola/datos/xp` (con `date -r`, sin `stat`);
    - busca `rocola_spotify.txt` primero en `/rocola/datos/xp`;
    - atiende la orden de emergencia (decisión 10).
  - `rocola-actualiza`:
    - lee `actualizar_solo=` de `rocola_os.txt` y, si no está, del de antes;
    - si el programa atiende el sistema nuevo, dice que entra «cuando esté libre».
- Depende de: el contrato de §2.5 y la decisión 10.
- Comprobar:
  - `sistema/rocola/Pruebas/Lanzador/prueba.sh` con el BusyBox del sistema, con casos nuevos: con y sin `rocola-xp-sabe` (un sistema
    vuelto atrás sigue rechazando), el diagnóstico con el nombre de XP y `todo=0`;
  - `sh -n` con el BusyBox del sistema.
- En el A4: el buzón de verdad.

**1c. casa** (chico; ligero)
- Toca: `sistema/casa`.
- Hace:
  - la versión del programa contra `sistema-N`, mientras el canal del programa esté apagado;
  - quitar o esconder, mientras tanto, «Compilar el programa» y «Poner la versión nueva ya»;
  - el texto de apagar: borra, por la regla del 8-11, salvo que el dueño decida otra cosa;
  - las órdenes «cuando esté libre»;
  - su LEEME: «Ctrl+F3 → Internet» ya no existe así.
- Comprobar: que compila, con un GitHub simulado que quede como guion dentro del repositorio (hoy no está).
- Su compilación (`rocola-casa.yml`), solo cuando el dueño la pida.

### OLEADA 2: internet, el ratón y lo que no debe fallar en el bar

Los grupos que añaden algo al latido o a `vivo.txt` (2a, 2c y 2d) lo ponen en archivos propios de `RocolaOs/`, a los que llama el latido
de 1a: así no chocan entre ellos.

**2a. red** (chico; ligero)
- Toca: `RocolaOs/Red.cs` (una copia de `Consola/Conexion.cs` con `Nativo/PrioridadHilo.cs`) y un renglón de `vivo.txt`.
- Comprobar:
  - en un espacio de red aparte (`unshare -n`): sin tarjeta, sin cable, sin dirección, sin router y con un DNS que no contesta;
  - el nivel 1.
- En el A4: la Realtek, el cable y el router del bar.

**2b. raton** (chico; sesión)
- Toca: `Plataforma/TeclasLinux.cs` (196-227).
- Hace:
  - el botón derecho da la tecla REGRESAR de `rocola_teclas.txt`, y la rueda, ARRIBA/ABAJO;
  - solo en la pantalla de la rocola: con una hoja delante, sigue el ratón de XP de siempre;
  - esas teclas cuentan como apretadas para `GetAsyncKeyState` (`TeclasLinux.Fisica`).
- Depende de: la decisión 4.
- Comprobar:
  - una escena nueva con el clic derecho y la rueda (el protocolo de `ROCOLA_PULSA` gana `clicderecho` y `rueda`): la traza da la tecla y
    lo que XP hace con ella;
  - `f3raton` IGUAL;
  - el nivel 1.
- En el A4: un ratón de verdad.

**2c. robustez** (medio; sesión)
- Toca:
  - `Nativo/Anfitrion.cs`: las teclas también si KMS falló (con `ROCOLA_PANTALLA=fuera`, el de las pruebas, sigue como hoy);
  - `Nativo/Imagenes.cs`: las portadas gigantes;
  - `RocolaOs/`: la hora que salta, en el latido;
  - el lanzador: la copia de los botones.
- Comprobar:
  - con KMS fallando a propósito (como `gl_falla.sh`) y teclas por `ROCOLA_PULSA`: suena y se puede pedir;
  - una portada de 72 Mpx;
  - la hora, con relojes de mentira (la cuenta separada de la hora del equipo);
  - `rocola_teclas.txt` borrado, con un respaldo de XP.
- En el A4: la pila del reloj de la placa.

**2d. vigilante** (chico; sesión), solo si el dueño lo aprueba (decisión 11)
- Toca: `Nativo/PantallaRocola.cs` (el latido de cada vuelta, también en los bucles de las hojas) y un hilo vigilante.
- Hace, en dos pasos:
  1. solo medir y apuntar la vuelta más larga (en `vivo.txt` y en `rocola_error.txt`), para verla en el A4;
  2. después, salir si pasan 60 s sin una vuelta.
- Comprobar:
  - con Ctrl+F3 abierto un buen rato, con el diagnóstico completo y con F12: no salta;
  - con un bucle parado a propósito (una orden de prueba): sale, lo apunta y el lanzador la abre con su estado.
- En el A4: la vuelta más larga de verdad, con el disco de la música frío.

**2e. prueba-spotify** (chico; sesión), si el dueño elige la opción recomendada de la decisión 7
- Toca: `RocolaOs/` (con `[ModuleInitializer]`, como `VersionRocolaOs`).
- Hace: `rocola-xp --prueba-spotify`, desde la consola de mantenimiento y con la rocola cerrada, como `rocola-prueba`:
  - abre `rocola-spotify` y espera la cuenta (la primera vez, desde el celular);
  - busca una canción y la toca 20 s por la tarjeta.
  - No usa nada de XP.
- Comprobar: con el `rocola-spotify` simulado, rehecho en `Pruebas/RocolaOs/`.
- En el A4: con la cuenta real. Es lo más incierto de toda la fase, por eso va tan pronto.

### OLEADA 3: la hoja de Rocola OS y lo visual de base

Necesita la decisión 2 y las propuestas K10-K13 elegidas. Las oleadas 3 y 4 no dependen una de la otra: si el dueño aún no eligió, la 4 va
antes.

**3a. hoja-rocola-os** (grande; sesión): K11 y K12
- Toca, con Ctrl+F4 (la recomendada):
  - `Nativo/PantallaRocola.Teclado.cs` o `.Vistas.cs`: Ctrl+F4 se atiende antes de XP, que no lo usa;
  - `Nativo/Hoja.Pinta.cs`: la marca de la red y el recordatorio en la barra de Ctrl+F3;
  - `RocolaOs/`: la hoja y la cadena de la red;
  - `Nativo/ImportaXp.cs`: la marca `rocola_reimportar_xp`;
  - el lanzador: el 45, por la vía que se decida.
  - Con la séptima pestaña, además, uno o pocos ganchos en `Codigo/Form1.Ajustes.cs`.
- Hace:
  - las hojitas Internet, Spotify, Al día, La rocola, En casa, Sonido e IA, e Imagen;
  - cada interruptor nuevo, persistente en `rocola_os.txt`;
  - lo que no puede actuar va en gris, diciendo por qué (regla del 16-10-2026).
- Comprobar:
  - con `ROCOLA_OS` apagado, IGUALES: VistasAjustes 0 de 545, `bateria_f3`, `vistas_f3`, la batería de las pestañas y las escenas `f3*`;
  - con él: escenas nuevas por hojita, el doble toque en 5 s y el 45 con un `rocola-desinstalar` de mentira;
  - `Pruebas/Importa/prueba.sh` con la marca.
- En el A4:
  - verla en la pantalla del bar;
  - el 45 de verdad, al final y con el dueño delante.

**3b. avisos** (chico; ligero): K10
- Toca: `Codigo/SuenaVivoGpu.cs` (un gancho para los dos iconos nuevos) y `RocolaOs/`.
- Comprobar:
  - el arnés `VistasSuena` IGUAL sin `ROCOLA_OS`;
  - con él, los avisos nuevos horneados.

**3c. raton-vista** (medio; sesión): K13 y el clic en la pantalla principal
- Toca:
  - `Nativo/PantallaRocola.Vistas.cs`: enseñar y esconder la flecha;
  - `Nativo/CompositorPantalla.cs`: el contorno y el aro;
  - `RocolaOs/`;
  - el reenvío del clic a las vistas de XP: un segundo oyente del clic del panel, como el de XP; si hace falta, un gancho en
    `Form1.Video.cs`.
- Depende de: 2b.
- Comprobar con escenas nuevas:
  - un clic en una portada abre su disco;
  - uno en una letra la elige;
  - uno en una canción la elige, y el segundo la pide y la cobra;
  - sin ratón, o con el ratón quieto, IGUAL; y `f3raton` IGUAL.
- En el A4: el ratón y, si la hay, la pantalla táctil.

### OLEADA 4: el sonido, los archivos y el buscador

Cada cosa, según las decisiones 4 y 5. Los cuatro grupos no comparten archivos.

**4a. salida** (grande; sesión)
- Toca: `Codigo/SalidaSonido.cs` (g08), `Nativo/SalidaAlsa.cs` y `Nativo/SalidaPropiaAlsa.cs`; `Nativo/PrioridadHilo.cs`, solo si el A4 lo
  pide.
- Hace:
  - 24 bits con dither y silencio exacto;
  - la frecuencia que la tarjeta no tiene, con el convertidor de la rocola;
  - saltar sin chasquido, también al apagar o al cortar una prueba;
  - volver de un corte y acabar suave.
  - El grupo comprueba que pasar por la vía de coma flotante de XP no cambia nada más de la cadena.
- Comprobar:
  - con lo nuevo apagado: `sonido.sh` y `difsonido.py`, bit a bit;
  - con lo nuevo, la tarjeta de prueba con reloj ampliada a 24 bits en 32: tonos de 1 y 15 kHz, saltos y un corte forzado;
  - `pruebas/salida.cs`, `salidapropia.cs` y `salidacontinua.cs` de XP;
  - QEMU con la HDA (`sistema/rocola/Pruebas/Qemu/sonido.py`).
- En el A4:
  - qué formatos y frecuencias admite la Realtek;
  - oírlo en el billar;
  - la caja negra, sin cortes nuevos.

**4b. archivos** (medio; sesión)
- Toca: `Codigo/Form1.Sonido.cs` (g09 y g10), `Nativo/DescompresorMp3.cs` y `RocolaOs/`.
- Hace:
  - convertir en el repaso con ffmpeg: a la prioridad mínima, de uno en uno, con 1 GB libre y un tope de 10 min;
  - la copia, en el disco de la música (`rocola-os/convertidas`) o por `DatosGrandes`;
  - quitar la marca de dañada de XP cuando la copia ya está;
  - los huecos de un MP3 dañado.
  - La espera de 20 s en la cola llega con K8 (oleada 7).
- Comprobar:
  - con un M4A con nombre .mp3: el repaso lo convierte, se mide y suena bajo la ruta del original, y el original queda igual byte a byte
    (md5);
  - sin ffmpeg, sin 1 GB libre o con un ffmpeg colgado: como XP;
  - un MP3 con 8 KB de basura.
- En el A4: cuánto tarda convertir, y el sitio en el disco.

**4c. videos** (medio; sesión)
- Toca: `Codigo/Form1.Video.cs` (g11), `Nativo/LectorAudio.cs` (el sonido de un video, por ffmpeg) y `Nativo/ReproductorVideo.cs`.
- Hace:
  - medir los videos en el repaso (decisión 5);
  - su volumen, como el de las canciones, con la ganancia exacta en mpv y un limitador al techo;
  - saltar un video sin golpe.
- Comprobar:
  - la escena `video` IGUAL sin `ROCOLA_OS`;
  - dos videos a 15,6 dB uno del otro quedan parejos;
  - `--prueba-video` y `pruebas/videoaudio.cs`.
- En el A4: cuánto tarda medir un video, y oírlo.

**4d. buscador** (medio; sesión)
- Toca: `Codigo/Form1.Biblioteca.cs` (g12) y `RocolaOs/`.
- Hace:
  - la segunda pasada flexible, en su hilo, añadida DEBAJO por encargo, con el índice de XP;
  - el repaso de las letras de los MP3 (USLT), a la prioridad mínima (`rocola_letras_mp3.txt`).
- Comprobar:
  - sin `ROCOLA_OS`, el recorrido y la escena del buscador IGUALES;
  - con él: una búsqueda con una falta trae resultados debajo; lo de XP sale primero y en su orden; con 40 o más no se añade nada; y el
    tiempo por tecla en la pantalla no sube;
  - las mismas búsquedas en el programa anterior (`rocola-consola --busca`), como referencia de lo flexible.
- En el A4: el repaso de las letras con la biblioteca real.

### OLEADA 5: Spotify por dentro, el núcleo

Necesita las decisiones 6 y 7.

**5a. spotify-nucleo** (grande; sesión)
- Toca: `Codigo/Form1.Sonido.cs` (g13), `RocolaOs/` (el proceso, el enchufe y la fuente de sonido dentro de Form1) y `Pruebas/RocolaOs/`
  (el `rocola-spotify` simulado).
- Hace:
  - abrir y cerrar `rocola-spotify` con `spotify=`;
  - la cuenta, en `/rocola/datos/spotify`;
  - la bitácora `rocola_spotify.txt`, en `/rocola/datos/xp`;
  - lo que llega pasa por la cadena de XP: el nivelado, el local, el billar y el limitador; sin DSEE ni relevo.
- Comprobar:
  - con `spotify=0`, lo sonado bit a bit;
  - con el simulado: que suene, que pare, que se caiga a media canción (sale el aviso de XP) y que el cierre quepa en los 20 s.
- En el A4: la cuenta real (Premium), con el celular en la misma red.

### OLEADAS 6 y 7: Spotify entero, y lo que se ve (con las K elegidas)

El reparto exacto en grupos se hace cuando el dueño elija, con la regla de siempre (la tabla de abajo dice qué archivo toca cada K). El
reparto propuesto:

**Oleada 6:**
- **6a. parejas**: SP4.
- **6b. sustitución y pago**:
  - SP2 y SP3 (g14, g15 y g16);
  - pedir y pagar canciones de Spotify;
  - lo pagado que no sonó vuelve como crédito.
  - Toca `AperturaAudio.cs`, `Form1.Datos.cs` y `Form1.SinPausa.cs`.
  - Va a la vez que 6a, con un archivo de parejas de prueba en el formato del programa anterior.
- **6c. pared**: K1 y K2, con SP8.
- **6d. buscador**: K3 y K7, con Genius (decisión 13) y la ida y vuelta entre el buscador y un disco de Spotify.
- **6e. letra**: K6, con SP1.
- **6f. estrella**: K14.

**Oleada 7:**
- **7a. disco y cola**:
  - K4 (y la fila «MÁS DE ARTISTA EN SPOTIFY», lo que era P2);
  - K5, K8 y K9;
  - SP6.
- **7b. la espera de 20 s en la cola**: la de las conversiones y la de Spotify, con su gancho en `PlayNext`.
- **7c. SP5**: las portadas y los géneros que faltan, si el dueño lo quiere (si choca de archivo con 7b, va después).
- **7d. K23.**

Comprobar en todas:
- con `spotify=0`, o sin `ROCOLA_OS`, IGUAL;
- el simulado de punta a punta;
- `--prueba-sp2`, con los mismos casos del programa anterior.

En el A4: la cuenta real, y oír que lo sustituido es la misma grabación.

### OLEADA 8: la IA en reposo (decisión 8)

**8a. entrenar** (grande; sesión)
- Toca: `RocolaOs/` (el entrenador, la IA nueva y su hojita); en `Codigo/`, solo si hace falta un gancho para la comparación a ciegas.
- Hace:
  - IA1, con el material de XP y el del programa anterior, en solo lectura;
  - sus botones en la hoja de Rocola OS (Sonido e IA): entrenar ahora, comparar a ciegas «la de ahora o la nueva», quedarse con la nueva
    y descartarla.
  - La comparación a ciegas, sin añadir líneas a `CompararCiegas.cs`: si no cabe en ganchos de una línea, se discute antes con el
    coordinador.
- Comprobar:
  - el entrenador con material sintético: el mismo reparto y los mismos errores que el de Python;
  - que se corta al tocar un botón;
  - que nunca se pone sola.
- En el A4: cuánto tarda, y oír la comparación en el billar.

**8b. ia2-y-billar** (chico; sesión)
- Toca: `Codigo/Form1.Sonido.cs` (g17-g18), `Codigo/CadenaBillar.cs` (un gancho) y `RocolaOs/`.
- Hace: los cortes de tu biblioteca en la cosecha (IA2), y el DSEE en el billar con su interruptor (`dsee_billar_cortes=`, apagado).
- Comprobar:
  - `pruebas/compara-cadena.cs` y `cadenaentera.cs` de XP: apagado, bit a bit;
  - con él, MP3 cortados a 12 y 15 kHz por la cadena del billar.
- En el A4: oírlo en el billar.

### OLEADA 9: lo que solo embellece (las K15-K22 que elija el dueño)

- Solo por la tarjeta (regla del 8-9-2026), cada una con su interruptor en la hoja de Rocola OS → Imagen.
- Casi todas tocan `AtraccionGpu.cs` o `D3D9Star.cs`: se reparten por archivo (tabla de abajo).
- Comprobar:
  - sin `ROCOLA_OS`, o con la K apagada, IGUAL;
  - con ella, escenas y capturas nuevas;
  - el coste p95, p99 y máximo con NativeAOT, y en el A4 con el diagnóstico completo de XP.

### OLEADA 10: el cierre

- Los documentos:
  - `INTEGRACION_ROCOLA_XP.md` §8: lo que ya volvió, y la corrección del crédito;
  - `sistema/LEEME.md` y `sistema/casa/LEEME.md`: hoy mandan a «Ctrl+F3 → Internet / Spotify / La rocola», que ya no existen así;
  - `Xp/LEEME.md`;
  - una línea en `AGENTS.md`, sin decir que funciona en el A4.
- La comprobación completa (niveles 0 a 3), y la lista de lo que hay que probar en el A4 (§7).
- La compilación del sistema, cuando el dueño la pida.

**Qué archivo de `Codigo/` tocaría cada K** (con un gancho de una línea en cada uno; aproximado, lo confirma cada grupo):

| K | Archivos de `Codigo/` | Oleada |
|---|---|---|
| K1 | `Form1.Biblioteca.cs` (el orden de las letras, la cabecera), `Form1.Vistas.cs` (la barra de letras) | 6 |
| K2 | `PantallaVivaGpu.cs` (el letrero y la ficha) | 6 |
| K3, K7 | `BuscadorPortadasGpu.cs`, `Form1.Buscador.cs` | 6 |
| K4, K5, K9 | `Form1.Vistas.cs` (la lista, las pastillas), `DiscoVivoGpu.cs`, `EstadoReposoGpu.cs`; K9 también `BuscadorPortadasGpu.cs` y `PantallaVivaGpu.cs` | 7 |
| K6 | `D3D9Star.cs` (`RenderFrame`) | 6 |
| K8 | `DiscoVivoGpu.cs`, `D3D9Star.cs` (la barra de avance); la espera, `Form1.Datos.cs` | 7 |
| K10 | `SuenaVivoGpu.cs` | 3 |
| K11 | ninguno con Ctrl+F4; `Form1.Ajustes.cs` con la séptima pestaña | 3 |
| K12, K13 | ninguno (Nativo); K13, quizá `Form1.Video.cs` para el clic | 3 |
| K14 | `AsistenteRocola.cs`, `AsistenteGuiaRocola.cs` | 6 |
| K15, K17, K22 | `D3D9Star.cs`; K15 y K22, también `AtraccionGpu.cs`; K22, también `Form1.Vistas.cs` | 9 |
| K16, K18, K20, K21, K23 | `AtraccionGpu.cs` | 9 (K23: 7) |
| K19 | `AsistenteEstrellaGpu.cs` | 9 |
| K24 | ninguno (`sistema/casa`) | 1 o 10 |

## 5. Las propuestas visuales nuevas, para que el dueño elija

Son ideas nuevas: ninguna es de lo que se hizo para el programa anterior. Cada una:
- conserva el aspecto de XP y solo le añade, en huecos que XP deja libres, en estados nuevos (Spotify, preparando, dañada) o en páginas
  que se intercalan;
- tiene su interruptor persistente, salvo las que son parte de una función;
- con Spotify y los interruptores de Rocola OS apagados, deja la pantalla idéntica a la de XP (se comprueba como en el port).

Las reglas de siempre se respetan:
- lo que suena desde Spotify en lugar de un MP3 tuyo (SP2) no deja ningún indicio;
- la estrella no tiene cara (K14 y K19 usan solo los brazos y las manos);
- no hay fugaces ni cometas nuevos.

Los costes no están medidos en el A4: son estimados por cuánta pantalla rellenan y por lo que se hornea.

### 5.1 Hacen falta para usar algo no visual (12)

**K11. La hoja «Rocola OS»**: donde viven los interruptores y los botones del dueño
- Qué se ve:
  - una ventana como las de XP: barra azul, tarjetas con su título en versalitas y su rayita de color, interruptores morados, botones
    planos y el «Listo» verde;
  - con hojitas: Internet, Spotify, Al día, La rocola, En casa, Sonido e IA, e Imagen.
- Lo que va en cada hojita:
  - **Internet**: la cadena de la red (K12), y Genius (`busca_letra=`, decisión 13).
  - **Al día**: «sistema 37 · programa 37 · compilado el 14-11», si hay uno nuevo y cuándo entra, «Actualizarse sola» y «Buscar
    actualización ahora».
  - **La rocola**: reiniciar el programa o el equipo, apagar, volver a Windows XP y traer otra vez lo aprendido en XP.
    - Al primer toque, el botón se pone ámbar («Otra vez para apagar (5)») y hay 5 s para el segundo.
    - «Volver a Windows XP» dice claro que quita el arranque de Rocola OS y que la música y lo aprendido no se tocan.
    - También dice cómo entrar a la consola de mantenimiento y qué salida de sonido se usa.
  - **En casa**: si el buzón está, cuál fue la última orden y cuándo se subió el estado.
  - **Spotify**: el modo, la cuenta, cuántas consultas salen bien y cuántas mal, y el último código (429 = Spotify frena). La primera vez,
    los pasos del celular.
  - **Sonido e IA**: lo que el dueño quiera con interruptor (decisión 4), el DSEE en el billar y, cuando llegue, la IA en reposo
    (entrenar ahora, comparar a ciegas, quedarse con la nueva o descartarla).
  - **Imagen**: los interruptores de las propuestas elegidas, y «Como de fábrica».
- Dónde: con **Ctrl+F4**, como Ctrl+F2 (recomendada: la ventana de Ctrl+F3 no cambia en nada). La otra opción es una **séptima pestaña**
  en Ctrl+F3: las seis de XP pasarían de 100 a unos 86 px, como XP las fue angostando de 148 a 120 y a 100 cada vez que sumó una.
  Decisión 2.
- Para qué: Internet, Spotify, las actualizaciones, «La rocola», Rocola en casa y los interruptores de todo lo nuevo.
- Coste: nulo con la ventana cerrada; abierta, como una página de Ctrl+F3.

**K12. La cadena de la red**
- Qué se ve:
  - en la hojita Internet, seis eslabones en fila: Tarjeta, Cable, Dirección, Router, Nombres e Internet;
  - en verde hasta donde llega la conexión, cada uno con su dato (eth0, 192.168.1.40, «23 ms»);
  - el primero que falla, en rojo y con qué hacer («El cable no está conectado: revisa el conector de atrás»); los que siguen, en gris;
  - debajo, «Última vez con internet: hoy 21:04», y la MAC, la que pide el router para el encendido remoto.
  - En la barra azul de Ctrl+F3, junto a la X: un punto y una palabra («En línea», «Sin internet», «Sin cable», «Mirando…»).
- Para qué: el dueño no tenía forma de ver si la rocola está conectada.
- Coste: nulo. Lo mide el hilo de fondo, y se dibuja solo cuando cambia.

**K10. Los avisos de Rocola OS, con el aviso de XP**
- Qué se ve: el mismo aviso de XP (entra por la derecha, arriba, con su color, su icono y su línea que se acaba), con textos nuevos:
  - «Spotify no pudo poner «X» · tu crédito volvió»;
  - «X ya está lista · la próxima vez suena al momento»;
  - «Se fue el internet · lo de Spotify espera, tu música sigue» (solo con Spotify encendido);
  - «Rocola OS se puso al día · sistema 38», una vez.
  - Dos iconos nuevos con el trazo de los de XP: una nube y una flecha en círculo.
- Para qué: créditos devueltos, conversiones, Spotify, internet y actualizaciones.
- Coste: nulo.
- Interruptor:
  - los de créditos, sin interruptor: el cliente tiene que saber que su crédito volvió;
  - los de internet, `avisos_red=`, encendido.

**K13. La flecha del ratón y lo que va a tocar**
- Qué se ve:
  - XP esconde la flecha siempre. Con Rocola OS, al mover el ratón sale la flecha de Windows XP (y la mano sobre lo que se puede tocar);
    se esconde a los 6 s quieta o al apretar un botón de la rocola.
  - Lo que está debajo se rodea con un contorno fino lavanda.
  - Al hacer clic, sale un aro de la punta en 0,22 s: verde con el izquierdo y rojo con el derecho, los colores de los botones de la
    barra de ayuda de XP.
  - En la atracción, el primer clic solo la despierta.
- Para qué: el ratón (el dueño, el 7-11 en el A4: «no tengo soporte para mouse»).
- Coste: nulo.
- Interruptor:
  - la flecha, sin interruptor: sin ella el ratón no se puede usar;
  - el contorno y el aro, `raton_resalta=`, encendido.

**K1. «SPOTIFY» en la barra de letras**
- Qué se ve: con Spotify encendido, una casilla más entre la ★ y la #, igual al bloque dorado de la ★ pero en verde Spotify, con
  «SPOTIFY» en versalitas chicas (o una nota musical si no cabe, como VIDEO pasa a ►). El encabezado dice «SPOTIFY», o «SPOTIFY · sin
  conexión» en gris.
- Para qué: llegar a tu Spotify con los botones de siempre.
- Coste: nulo (una casilla más en la imagen de la barra, que XP ya rehace).
- Interruptor: va con `spotify=`.

**K2. Tu Spotify en la pared**
- Qué se ve:
  - dentro de la letra SPOTIFY, las casillas de XP de siempre;
  - primero «Tus favoritas», con una portada en los colores del cielo de XP y un corazón blanco;
  - después tus listas y tus discos guardados, más los éxitos si Spotify los deja ver;
  - el letrero de XP dice «Spotify · 32 canciones · 2 h 05 min», con un puntito verde, y la ficha rápida, «LAS PRIMERAS DE ESTA LISTA»;
  - sin conexión, las casillas quedan al 50 %.
  - Si el dueño quiere, también tus artistas con su foto y las diez listas por género y época (lo que eran P3 y P4), como casillas de la
    misma letra.
- Para qué: tu biblioteca de Spotify.
- Coste: como las portadas de XP (una textura por casilla). Las portadas se bajan solo al verse, a `/run`.
- Interruptor: va con `spotify=`.

**K3. Spotify en las tarjetas del buscador**
- Qué se ve:
  - lo de Spotify sale en las mismas tarjetas de XP, debajo de lo tuyo y con su portada;
  - la palabra de tipo de XP no cambia, pero lleva detrás una cápsula chiquita «SPOTIFY» en verde;
  - el renglón de abajo dice «de Spotify · Artista»;
  - sin conexión, no sale nada de Spotify, y junto a «6 resultados» se lee en gris «Spotify sin conexión».
- Para qué: buscar en Spotify.
- Coste: nulo (va dentro del horneado de las tarjetas).
- Interruptor: va con `spotify=`.

**K4. La página de un artista de Spotify, como un disco de XP**
- Qué se ve:
  - la vista de un disco de XP: arriba, el nombre del artista como título de disco, y «Spotify · artista · 5,2 M de seguidores»;
  - su foto en el cubo de la portada;
  - en la lista, «LO MÁS CONOCIDO» (sus 10 canciones, que se piden con PONER y cuestan su crédito);
  - después, «SUS DISCOS EN SPOTIFY», un renglón por disco con su portadita, que se abre con PONER;
  - se entra con el zoom de XP, y REGRESAR vuelve al buscador con lo escrito.
  - La misma fila de discos, «MÁS DE ARTISTA EN SPOTIFY», puede ir al final de un disco tuyo (lo que era P2, `mas_del_artista=`), con la
    navegación de siempre de la lista.
- Para qué: los artistas que no están en la rocola (lo que el dueño pidió el 7-11 para su familia).
- Coste: como entrar a un disco de XP.
- Interruptor: va con `spotify=` (y `mas_del_artista=`, para la fila en tus discos).

**K5. Lo que viene de Spotify, marcado**
- Qué se ve:
  - en un disco tuyo al que Spotify le completa canciones, después de las tuyas, una rayita con «TAMBIÉN EN SPOTIFY» en verde;
  - en esas canciones, una cápsula verde «SPOTIFY» donde XP pone «EN COLA» o «SONANDO» (manda la de XP);
  - en la cola, una rayita verde bajo la portadita;
  - en «A CONTINUACIÓN» y en lo que suena, una cápsula chiquita «SPOTIFY».
  - NUNCA sale en las tuyas que suenan desde Spotify por calidad (SP2).
- Para qué: completar discos (SP6), y saber qué depende de internet.
- Coste: nulo.
- Interruptor: `marca_spotify=`, encendido. El separador va siempre.

**K6. La letra que se canta, como subtítulo del cielo**
- Qué se ve:
  - en el estado extendido, en el cielo libre entre la estrella y la aurora, dos renglones centrados;
  - el que se canta, en blanco, con la letra de los títulos de XP y una sombra suave; el que sigue, más chico y en lavanda;
  - al cambiar de verso, el de arriba sube y se apaga en 0,3 s;
  - en los tramos sin voz, el cielo de XP tal cual;
  - abajo a la derecha, chiquito: «Letra: <proveedor>».
  - No es la de antes: sin franjas de color, y sin alinear a la izquierda.
- Para qué: las letras sincronizadas de Spotify (SP1), de lo que suena (de Spotify, o tuya con pareja).
- Coste: bajo. Cada verso, dos texturas de un renglón (1-2 ms de horneado); cerca del 10 % de la pantalla con mezcla.
- Interruptor: `letra_cielo=`, encendido; en gris con Spotify apagado.

**K7. El buscador dice por qué salió cada una**
- Qué se ve:
  - lo de XP sale primero, igual;
  - antes de lo que añade el buscador flexible, una rayita «PARECIDAS»;
  - si una sale por su letra, el renglón de abajo dice «por la letra: «…y volver, volver…»», con lo que casa en el ámbar de XP;
  - si Genius dice que la frase es de una canción que no está, una tarjeta gris con «?»: «NO ESTÁ EN LA ROCOLA · Volver, volver · de
    Vicente Fernández». Con Spotify encendido, esa sale como tarjeta de Spotify y se puede pedir.
- Para qué: el buscador flexible y la búsqueda por la letra; sin la tarjeta, Genius no sirve.
- Coste: nulo.
- Interruptor: `buscador_explica=`, encendido. La tarjeta, con `busca_letra=`.

**K8. PREPARANDO**
- Qué se ve:
  - una cápsula lavanda «PREPARANDO», con un aro de ocho puntitos que gira, en el sitio y con el tamaño de SONANDO;
  - si es la que sigue y la de ahora ya acabó, la barra de avance de XP, vacía, con un brillo suave que la recorre;
  - nunca en las de SP2.
- Para qué: los «MP3» que se convierten al pedirlos (hasta 20 s) y la espera de Spotify; sin esto, parece que la rocola se trabó.
- Coste: nulo.
- Interruptor: no lleva; es parte de la función.

### 5.2 Ayudan, pero no son indispensables (3)

**K9. Las que no suenan, en gris**
- Qué se ve: una canción dañada o que ya no está, con el título en gris y una cápsula «NO SUENA» en lugar de la duración. Vuelve a verse
  normal cuando el archivo cambia.
- Para qué: XP ya avisa y no la cobra (`AddItem`); esto lo enseña antes de pedirla.
- Coste: nulo.
- Interruptor: `danadas_gris=`, encendido.

**K14. La estrella también enseña Spotify**
- Qué se ve:
  - con Spotify encendido, el menú de la estrella pasa de tres a cuatro opciones: «Poner una de Spotify», con una notita verde;
  - es una misión más de la guía de XP: abrir el buscador, escribir, elegir la de la cápsula verde, una moneda y PONER;
  - la estrella señala con la mano y celebra con los brazos, sin cara.
- Para qué: el dueño: «no muchos en mi familia saben usar el buscador».
- Coste: nulo.
- Interruptor: `guia_spotify=`, encendido.

**K23. Spotify en el escaparate**
- Qué se ve: con Spotify encendido, cada tres discos tuyos, la atracción enseña una lista o un disco de tu Spotify en el cubo que se
  mece, con una cápsula «de Spotify», y un mensaje: «En la reunión también hay Spotify: búscala con ROJO + ARRIBA».
- Para qué: que la familia lo descubra sin saber usar el buscador.
- Coste: una portada más.
- Interruptor: `escaparate_spotify=`, encendido.

### 5.3 Solo embellecen (8)

Solo con tarjeta gráfica; cada una con su interruptor en la hoja de Rocola OS → Imagen.

**K15. El disco que asoma**
- Qué se ve: dentro de un disco, cuando suena una de ese disco, un LP negro sale de detrás de la portada hasta asomar un tercio y gira a
  33⅓: surcos finos, un brillo quieto y la etiqueta del color de la portada. En el escaparate de la atracción asoma un cuarto.
- Para qué: la rocola clásica que le gusta a la familia; además dice de un vistazo qué disco suena.
- Coste: bajo (una textura de 256×256 por color; un cuadro de cerca del 12 % de la pantalla, casi todo tapado).
- Interruptor: `disco_asoma=`, encendido.

**K16. Las tiras de la rocola clásica**
- Qué se ve: cada tres discos del escaparate, 10 s de una página nueva, «LA ROCOLA CLÁSICA»: diez tiras de cartón crema con banda roja y
  azul, como las de las Rowe AMI, con dos canciones del disco (lado A y lado B), el artista y dónde encontrarlo («L·2»).
- Para qué: adorno (las Rowe AMI que le gustan a la familia).
- Coste: bajo-medio, y solo en la atracción (las tiras se hornean una vez por disco).
- Interruptor: `tiras_clasicas=`, encendido.

**K17. La constelación de la noche**
- Qué se ve:
  - cada canción que se pide esta noche enciende una estrellita, siempre en el mismo sitio para la misma canción; si la vuelven a pedir,
    crece;
  - cuando la atracción enseña «lo más pedido esta noche», sus estrellas se unen con líneas finísimas;
  - al empezar otra noche, se apagan poco a poco. Tope: 150.
- Para qué: que la noche del bar se vaya viendo en el cielo.
- Coste: nulo (puntos en el lote de estrellas que XP ya dibuja).
- Interruptor: `constelacion_noche=`, encendido.

**K18. Las fiestas en el escaparate**
- Qué se ve: en las fechas de México, un mensaje más entre los de XP y una cápsula de fiesta junto a la del disco. Por ejemplo:
  - el 15 y 16 de septiembre: «¡Viva México! Pon tu ranchera favorita», con borde verde, blanco y rojo;
  - en Día de Muertos: «pon la canción de alguien que extrañas», en cempasúchil;
  - el 10 de mayo, Santa Cecilia, la Guadalupana, las posadas, Navidad, Fin de Año, Reyes, el 14 de febrero, el Día del Niño, el Día del
    Padre, el 24 de febrero y el 20 de noviembre.
  - El cielo no cambia: no es la decoración de antes.
- Para qué: lo que al dueño le gustó de las fiestas, pero en los letreros.
- Coste: nulo.
- Interruptor: `fiestas_escaparate=`, encendido.

**K19. La estrella festeja con las manos**
- Qué se ve: en las fiestas, la estrella lleva algo en la mano y lo mueve con el brazo: una banderita, un cempasúchil, un corazón, una
  rosa, un farolito, una luz de bengala, un regalito o un sombrero de mariachi. Nada en la cabeza ni en la cara.
- Para qué: adorno.
- Coste: nulo.
- Interruptor: `estrella_festeja=`, encendido.

**K20. La hora y el clima en el escaparate**
- Qué se ve: en la atracción, arriba a la derecha, una cápsula con un iconito (luna o sol, nube, lluvia, rayo) y «11:42 p. m. · 18 °C»;
  y un mensaje: «Afuera llueve: quédate otra canción». El cielo no se nubla ni llueve: eso era lo de antes.
- Para qué: el clima de internet, que al dueño le gustó.
- Coste: nulo en la pantalla. En la red, Open-Meteo cada 15 min (gratis, sin llave). La ciudad se toma de la conexión una vez por
  semana, o se elige.
- Interruptor: `clima_escaparate=`, encendido; en gris sin internet.

**K21. Los tubos de burbujas**
- Qué se ve: en la atracción, dos tubos delgados en las orillas, con luz de ámbar a rojo y burbujitas que suben, como en las rocolas de
  los 40.
- Para qué: adorno.
- Coste: bajo (cerca del 3 % de la pantalla).
- Interruptor: `tubos_burbujas=`, **apagado** de fábrica: cambia el marco de la pantalla en la atracción, y conviene que el dueño lo vea
  primero.

**K22. Lo nuevo de la semana**
- Qué se ve: los discos que llegaron en los últimos 7 días, con una cápsula dorada «NUEVO» en una esquina de la portada; un puntito dorado
  bajo su letra; y un mensaje en la atracción: «Esta semana llegaron 6 discos nuevos». La primera vez, todo cuenta como viejo.
- Para qué: que los clientes de siempre vean qué música nueva hay.
- Coste: nulo (y una tabla nueva con la fecha en que se vio cada disco).
- Interruptor: `discos_nuevos=`, encendido.

### 5.4 Para el tablero de casa (1)

**K24. El tablero de casa con la cara de la rocola**
- Qué se ve: en Rocola en casa, la tarjeta «Lo que pasa ahora» como el estado extendido de XP: fondo azul noche, «A H O R A   S U E N A»
  en dorado, el título, el artista en verde, la barrita con el tiempo, las cápsulas (en cola, créditos, modo noche, Spotify) y «A
  CONTINUACIÓN». Sin portadas, para no subir imágenes al buzón.
- Para qué: Rocola en casa.
- Coste: nulo en el A4.
- Interruptor: no lleva; no es la pantalla de la rocola.

### 5.5 Lo que no se propone, y por qué

- **P1**, los discos de Spotify en las letras de tus artistas: mete discos ajenos entre los que el cliente conoce, y cambia la pared de XP.
- **SP9**, dos atajos más en el buscador: la fila de atajos de XP ya tiene cuatro (lo más pedido hoy, videos, karaoke, buscaste).
- **SP11**, la foto del artista de fondo; las letras al estilo de antes; el vidrio; el color vivo; las transiciones nuevas; la letra nueva;
  y el cielo que cambia con la hora, con las fiestas o con el clima: son de antes, o cambian lo de XP.
- **SP5**, las portadas y los géneros que faltan: no es un elemento nuevo, pero los discos sin portada ganarían una, y cambiarían colores en
  la pared. Va solo si el dueño lo pide (oleada 7).
- **Ideas descartadas**: un tema de GRUB (no cabe en 31 744 bytes); una pantalla con el cielo entre reinicios (toca el arranque); lluvia en
  el vidrio de los paneles (taparía texto); cambiar el cuadro liso de los discos sin portada (cambia lo de XP).

### 5.6 Para contestar

Basta con copiar esta lista y marcar:

- [ ] K11 La hoja «Rocola OS» (con Ctrl+F4 [ ] / séptima pestaña [ ])
- [ ] K12 La cadena de la red
- [ ] K10 Los avisos de Rocola OS
- [ ] K13 La flecha del ratón y lo que va a tocar
- [ ] K1 «SPOTIFY» en la barra de letras
- [ ] K2 Tu Spotify en la pared (con tus artistas y las listas por género [ ])
- [ ] K3 Spotify en las tarjetas del buscador
- [ ] K4 La página de un artista de Spotify (con la fila en tus discos [ ])
- [ ] K5 Lo que viene de Spotify, marcado
- [ ] K6 La letra que se canta, como subtítulo del cielo
- [ ] K7 El buscador dice por qué salió cada una
- [ ] K8 PREPARANDO
- [ ] K9 Las que no suenan, en gris
- [ ] K14 La estrella también enseña Spotify
- [ ] K23 Spotify en el escaparate
- [ ] K15 El disco que asoma
- [ ] K16 Las tiras de la rocola clásica
- [ ] K17 La constelación de la noche
- [ ] K18 Las fiestas en el escaparate
- [ ] K19 La estrella festeja con las manos
- [ ] K20 La hora y el clima en el escaparate
- [ ] K21 Los tubos de burbujas
- [ ] K22 Lo nuevo de la semana
- [ ] K24 El tablero de casa con la cara de la rocola
- [ ] SP5 Portadas y géneros de Spotify para tus discos que no los tienen

## 6. Las decisiones del dueño, antes de empezar

**1. La arquitectura y el orden.**
- (a) Aprobarlos como están en §2 y §4:
  - la carpeta `RocolaOs/` aparte;
  - el interruptor general `ROCOLA_OS`, que pone el lanzador;
  - los ganchos de una línea con su manifiesto;
  - el archivo propio `rocola_os.txt`;
  - volver a marcar como «ROCOLA OS» los 7 ganchos de lo grande, que hoy dicen «EN LINUX» (solo cambia el comentario).
- (b) Cambiar algo.
- **Recomendada: (a).** Es lo que deja la rocola de XP intacta y comprobable.

**2. Dónde se ve y se cambia lo de Rocola OS (K11).**
- (a) Una ventana propia con Ctrl+F4, como Ctrl+F2.
  - Ventajas: la ventana de Ctrl+F3 no cambia en nada; no necesita ganchos en `Form1.Ajustes.cs` (lo más delicado de Ctrl+F3); se hace
    y se prueba aparte.
  - En la barra de Ctrl+F3, un recordatorio: «Rocola OS: Ctrl+F4».
  - Es una excepción a la regla de que los interruptores van en Ctrl+F3: el dueño la aprueba con esta decisión.
- (b) Una séptima pestaña en Ctrl+F3.
  - Ventajas: todo lo del dueño en un solo sitio, como XP fue creciendo.
  - Inconvenientes: las seis pestañas de XP pasan de 100 a unos 86 px, y hacen falta ganchos en `Form1.Ajustes.cs`.
- (c) Un botón dentro de una página de XP (Detalles).
- **Recomendada: (a).** Es la que más cuida el aspecto de XP; (b) también vale.

**3. Qué hace cada salida pedida.**
- Apagar, 44:
  - por la regla del 8-11, borra el estado, como el botón de encendido;
  - ¿también «apagar cuando esté libre» desde casa? Hoy Rocola en casa dice que conserva los créditos.
  - **Recomendada:** que borre, y corregir el texto de casa.
- Reiniciar el programa (42) o el equipo (46): **recomendado**, que conserven el estado, como los reinicios propios de Rocola OS de hoy
  (la regla del 8-11).
- Volver a Windows XP, 45:
  - (a) borra el estado: es apagar Rocola OS; la rocola de XP no lo leería de todos modos;
  - (b) lo conserva.
  - **Recomendada: (a).**
- ¿Vuelven los botones de «La rocola» (reiniciar, apagar, volver a XP y traer otra vez lo de XP), con su doble toque en 5 s?
  **Recomendada: sí**, en la hoja de Rocola OS.

**4. Lo que cambia lo que ve u oye el cliente frente a XP.** En el programa anterior estas cosas eran automáticas, sin interruptor.

| Qué | Lo que cambia frente a XP | Recomendada |
|---|---|---|
| El buscador flexible y por la letra de tus MP3 | Resultados de más, debajo de los de XP, solo cuando XP da menos de 40 | automático |
| Botón derecho = REGRESAR, rueda = ARRIBA/ABAJO | Hoy, en la pantalla de la rocola, el derecho no hace nada, y la rueda solo cuenta como actividad | automático |
| Convertir los «MP3» que no lo son | La segunda vez suenan; hoy XP devuelve el crédito siempre | automático |
| 24 bits y silencio exacto | Menos ruido de fondo; la salida deja de ser bit a bit la de XP | automático |
| La frecuencia que la tarjeta no tiene, con el convertidor de la rocola | Mejor calidad en las de 22 y 32 kHz | automático |
| Saltar sin chasquido | Sin golpe, y el salto llega un poco después | automático |
| Volver de un corte, acabar suave, los huecos de un MP3 dañado | Solo actúan cuando algo falla | automático |
| El hilo del sonido en tiempo real | Nada, si no hay cortes | solo si en el A4 la caja negra apunta cortes |
| La cola que espera al disco de la música | La cola espera en vez de devolver los créditos | no por ahora (XP ya devuelve cada crédito) |

- Las opciones:
  - (a) como dice la tabla;
  - (b) cada una con su interruptor en la hoja de Rocola OS;
  - (c) no traer alguna (decir cuál).
- **Recomendada: (a).**
  - Es lo que el dueño decidió en su día, y solo añade: lo exacto de XP no cambia, y lo del sonido solo actúa al saltar, al cortarse o con
    archivos dañados.
  - Cada cosa se puede apagar para compararla (con la lista de `ROCOLA_OS`).
  - Todas se quitan de golpe con el interruptor de emergencia (decisión 10).

**5. Medir los videos.**
- (a) Sí: suenan al nivel de las canciones, como hacía la rocola de XP en Vista o posterior.
- (b) No: como en el bar.
- **Recomendada: (a).** Ya estaba decidido el 3-11, y el código de XP tiene ese camino. Hay que oírlo en el A4.

**6. Lo que dejó `rocola-consola` en `/rocola/datos`.**
- (a) Empezar de cero, solo con lo de XP.
- (b) Traer una vez, sin tocar el original, lo que costó horas y no cambia la música:
  - `rocola_spotify_pares.txt` (unas 14 h de consultas) y `rocola_spotify_artistas.txt`;
  - `rocola_letras_mp3.txt`, si las rutas coinciden (se comprueba al traerlo);
  - las claves `spotify=`, `calidad_spotify=`, `actualizar_solo=` y `dsee_billar_cortes=` de su configuración (`busca_letra=` no:
    lo decide la 13);
  - y la cuenta de Spotify ya enlazada, que se usa donde está (`/rocola/datos/spotify`), compartida con el sistema anterior.
- (c) Traer todo lo posible.
- **Recomendada: (b).**

**7. Spotify.**
- (a) Empezar ya por lo invisible (el núcleo, SP4, SP3 y SP2), aunque la familia no pueda pedir canciones de Spotify hasta que haya
  propuestas visuales.
- (b) Hacer Spotify entero cuando el dueño elija sus K, en las oleadas 5 a 7. Antes, una prueba corta en el A4 (2e: `rocola-xp
  --prueba-spotify`, que entra con su cuenta y toca 20 s), para saber pronto si librespot sigue sirviendo con su cuenta.
- (c) No traer Spotify por ahora: no es para el bar.
- **Recomendada: (b).** Lo invisible solo no sirve de mucho, y lo más incierto (la cuenta real) se prueba primero.

**8. La IA en reposo** (IA1, IA2, el material de XP y del programa anterior, y el DSEE en el billar).
- (a) Sí, al final, con sus botones en la hoja de Rocola OS (Sonido e IA).
- (b) No por ahora.
- **Recomendada: (a).** Nunca se pone sola: sin la comparación a ciegas a la vista no serviría, por eso va con su hojita.

**9. El canal del programa** (`programa-N`).
- (a) Volver a abrirlo: compilar `rocola-xp` en `rocola-programa.yml`, bajarlo y ponerlo cuando esté libre, con su propia vuelta atrás.
- (b) Que `rocola-xp` siga llegando solo con el sistema: la rápida tarda unos minutos y ya trae la vuelta atrás del sistema entero.
- **Recomendada: (b).** Y en Rocola en casa, quitar mientras tanto el botón que compila el programa: hoy compilaría `rocola-consola`.

**10. Un interruptor de emergencia, «la de XP tal cual».**
- (a) Sí.
  - Una línea `todo=0` en `rocola_os.txt`, o la orden del buzón «rocola_os apagar», hace que el lanzador abra `rocola-xp` sin
    `ROCOLA_OS`: la de XP tal cual, sin compilar nada.
  - Se deshace con `todo=1`, o con «rocola_os encender».
- (b) No.
- **Recomendada: (a).** Es un seguro barato: todo lo nuevo ya cuelga de esa variable.

**11. El vigilante del bucle.**
- (a) Sí, en dos pasos: primero solo medir en el A4 la vuelta más larga; después, cortar a los 60 s (el lanzador la vuelve a abrir con el
  estado, como tras un apagón).
- (b) No: XP no lo tenía.
- **Recomendada: (a).** Una pantalla congelada no pone canciones ni atiende botones. Medir antes evita cortar la música por un falso
  aviso.

**12. Las propuestas visuales** (§5.6).
- Cuáles se hacen.
- Sus valores de fábrica. **Recomendado:** encendidas las de coste bajo, y K21 apagada.
- Si las que hacen falta para usar algo (la barra de letras, los renglones, las tarjetas, los avisos) salen también en el modo sin tarjeta
  gráfica. **Recomendado: sí**, porque son función y no adorno. Las que solo embellecen, solo con la tarjeta (regla del 8-9-2026).

**13. Genius** (buscar por la letra en internet). Sus condiciones son de uso no comercial, y la rocola está en un negocio.
- (a) Encendido de fábrica, como se decidió el 7-11.
- (b) Apagado de fábrica (`busca_letra=0`): el dueño lo enciende si lo acepta, por ejemplo para las reuniones familiares.
- (c) No traerlo.
- **Recomendada: (b).** La búsqueda por la letra de tus MP3 no tiene ese problema y va siempre.

## 7. Riesgos, y lo que no se sabe hasta probar en el A4

### 7.1 Riesgos del trabajo

- **Mover una línea de `Codigo/` rompe la comparación con la referencia.** Las trazas nombran por archivo:línea. Lo evitan el protocolo
  (siempre una línea en su sitio) y `ganchos.py`.
- **XP reescribe `rocola_config.txt` entero.** Por eso `rocola_os.txt`. Una clave nueva en el archivo de XP se borraría sola.
- **Las partes de Form1 pueden chocar con nombres de XP**, o correr código dentro de su constructor. Por eso el prefijo y las reglas de
  §2.2.
- **Un cambio de XP en una línea con gancho rompe el manifiesto.** Rocola/ sigue cambiando mientras el bar siga con XP. `ganchos.py` lo
  detecta, y el gancho se rehace a mano.
- **Los hilos:**
  - una excepción sin atrapar en un hilo de Rocola OS cierra el programa, y cuenta como caída;
  - un hilo creado desde la pantalla hereda su nice y le compite;
  - un `await` en la pantalla mete encargos entre los de XP.
- **El A4 tiene dos núcleos.** `rocola-spotify` (que descomprime Vorbis), ffmpeg, el emparejado, el índice de letras y el entrenamiento
  compiten con el repaso de XP y con la música, y nada de eso está medido allí. Entrenar, solo en reposo, cortándose al tocar un botón.
- **SP2 es lo más delicado:**
  - las rutas «spotify:» acaban en las tablas de XP (`rocola_volumen.txt`, `rocola_fuentes.txt`, los respaldos, el importador): hay que
    revisar cada lector;
  - si `rocola-spotify` muere a media canción, sale el aviso de XP;
  - librespot no es oficial: Spotify puede bloquear la cuenta o cambiar algo;
  - nunca se probó con una cuenta real;
  - Spotify no es para el público del bar.
- **Medir los videos enciende un camino de XP («Vista o posterior») que nunca corrió en el bar**, y cambia su volumen frente a XP.
- **Las conversiones:**
  - solo la apertura y la medida ven la copia; la duración, las etiquetas y la portada siguen leyendo el original;
  - XP marca la canción «dañada» toda la sesión, así que Rocola OS tiene que quitar esa marca cuando la copia esté lista;
  - escribe en el disco de la música (NTFS: `S99rocola` ya hace el `sync`).
- **El ratón:** las teclas que fabrica (el derecho, la rueda) tienen que verse apretadas para `GetAsyncKeyState`, y no cruzarse con las
  combinaciones del rojo ni con los botones de `rocola_teclas.txt`.
- **Las salidas:**
  - `Environment.ExitCode` tiene que sobrevivir al final del `Main` de XP, también en NativeAOT;
  - cerrar a propósito con 0 en menos de 10 s cuenta como caída: por eso siempre 42 a 46.
- **La hora:** NTP la corrige al minuto de prender, y XP decide las 7 y la inactividad con la hora del equipo; lo «cuando esté libre»
  (2 minutos sin botones) usa esa misma inactividad.
- **La referencia y su ruido viven fuera del repositorio** (`/root/rocola-trabajo/referencia-xp`, y `ventanas2/ruido` y `datos0`). Si se
  pierde esa máquina, se pierde con qué comparar: conviene respaldarlos.
- **Los finales de línea:** `Codigo/Form1.Video.cs` va todo en LF, aunque en Rocola/ va en CRLF. El manifiesto compara bytes, no supone
  CRLF.
- **Lo visual puede colarse:** una hoja, la flecha visible o una pestaña cambian lo que se ve. Por eso solo con el «sí» del dueño, y
  siempre detrás de `ROCOLA_OS`.
- **Duplicar a XP:** traer del programa anterior algo que XP ya hace (devolver el crédito, la cola sin tarjeta, el video que se traba)
  dejaría dos lógicas peleándose (§3.7).
- **El repositorio es público:** las llaves y la cuenta de Spotify nunca van en el código, en los registros ni en lo que sube el buzón.
- **Los archivos compartidos con el programa anterior** (`Gl.cs`, `Kms.cs`, `Botones.cs`): tocarlos cambia `rocola-xp`.
  `rocola-consola` sigue dentro de sistema-37, sin usarse, hasta la siguiente compilación completa.

### 7.2 Lo que no se sabe hasta probar en el A4

- **La tarjeta de sonido (Realtek):** si admite 24 bits y qué frecuencias; si saltar sin chasquido se oye bien con el amplificador del
  bar; si hacen falta el tiempo real y el colchón de hoy (la caja negra «[Cortes de sonido]»).
- **La Radeon (r600):** si KMS puede fallar y cómo; cuánto cuesta de verdad cada propuesta visual (p95, p99 y máximo); la vuelta más larga
  del bucle, para el vigilante.
- **La red:** la tarjeta, el cable, el router y el DHCP del bar; el buzón de verdad; el encendido remoto.
- **El ratón:** uno de verdad y, si la hay, la pantalla táctil.
- **El apagado y los reinicios:** `poweroff` y `reboot` pedidos desde casa; cuánto tarda `rocola-xp` en cerrarse con música por la tarjeta
  real (aquí, sin tarjeta, de 30 a 90 ms; se le dan 20 s); el 45 con `rocola-desinstalar` sobre el disco real.
- **Spotify:** todo, con la cuenta real (Premium), con el celular en la misma red para enlazarla la primera vez (Spotify Connect), los
  límites de consultas de Spotify y si deja ver las letras y los éxitos a este programa.
- **Los tiempos con la biblioteca real** (unas 117 000 canciones y videos en el disco del A4): convertir, medir los videos, el repaso de
  las letras, el emparejado con Spotify y el entrenamiento de la IA.
- **Lo aprendido:** la carpeta de XP de verdad para «traer otra vez lo aprendido en XP», y qué hay de verdad en `/rocola/datos` (Spotify
  estuvo conectado allí).
- **La pila del reloj de la placa** (la hora que salta), y la memoria con portadas gigantes.

Nada de lo de este plan está probado en el A4. Ninguna oleada se dará por buena allí sin probarla allí.
