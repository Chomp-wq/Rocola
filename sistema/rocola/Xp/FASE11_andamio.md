# Fase 11, grupo andamio (oleada 0, grupo 0a): la base de lo propio de Rocola OS

14-11-2026. Rama `fase11-andamio`, sobre `1afaade` (la rama principal con el plan aprobado). Es lo de §2.2, §2.3 y §2.4 de
`PLAN_FASE11.md`: la base sobre la que trabajarán todos los demás grupos de la fase 11, muchos a la vez. **No se ve ni se oye nada
nuevo**: sin `ROCOLA_OS`, `rocola-xp` es la de hoy (los ganchos devuelven lo de XP, no corre ningún latido ni se lee o escribe ningún
archivo de Rocola OS); con `ROCOLA_OS=1` todavía no hay ninguna función que encender (solo el latido, que no hace nada). Nada se probó
en el A4.

## Qué se hizo

**`RocolaOs/`, carpeta nueva** (se compila sola: `RocolaXp.csproj` solo deja fuera bin, obj, Referencia y Pruebas):

| Archivo | Qué lleva |
|---|---|
| `RocolaOs.cs` | La variable `ROCOLA_OS`, el registro de funciones (`RocolaOsFuncion`, `Registra`), la pregunta de cada función (`Encendida`, `Pedida`, `Activo`), el arranque (`Arranca`), el latido y los renglones de `vivo.txt` |
| `RocolaOs.Ajustes.cs` | `rocola_os.txt`: las claves que declara cada función (`RocolaOsClave`), leer, cambiar, guardar, el respaldo diario y la orden «ajuste» del buzón (`AjusteDeFuera`) |
| `RocolaOs.Cierre.cs` | El cierre: la razón, `AlCerrar` (en la pantalla) y `ParaAlCerrar` (en su hilo, con un plazo común), lo que tardó cada una |
| `RocolaOs.Hilos.cs` | Los hilos de fondo, los encargos con nombre fijo, las rutas (`Run`, `Datos`, `Grande`) y la bitácora |
| `RocolaOs.Form1.cs` | La parte de Form1: el envoltorio del cierre, leer y escribir con lo de XP (`LeeLineasSeguro`, `EscribeSeguro`, `GuardaDetras`) y el respaldo con sus funciones (`CapturaRespaldo`, `BackupToCapturado`, `PruneBackups`) |
| `RocolaOs.Ordenes.cs` | El patrón de las órdenes de prueba de la línea de mandatos (`RocolaOsOrdenes.Si`) y lo que hay que saber de ellas |
| `PruebaAndamio.cs` | Una función de mentira, «prueba-andamio», solo para las pruebas (solo con `ROCOLA_OS=prueba-andamio`), y la orden `--prueba-rocola-os` |
| `GANCHOS.tsv` | El manifiesto de los ganchos |

**Los ganchos** (abajo, uno por uno): g01-g07 en `Codigo/` (solo el comentario) y g21 y g22 en `Nativo/`.

**Las herramientas:** `Pruebas/RocolaOs/ganchos.py` (nueva), `Pruebas/Tarjeta/guiones/diff-xp.py --sin-ganchos` y
`Pruebas/Ventanas/guiones/compara_trazas.py --ignora`.

**Fuera de lo pedido, y por qué** (ver «Archivos de otros» abajo): una línea en cada uno de los 17 `.csproj` de los arneses, para que
compilen también `RocolaOs/`.

**La prueba propia:** `Pruebas/RocolaOs/andamio/prueba.sh`.

## Los ganchos

Todos en `RocolaOs/GANCHOS.tsv` (id, archivo desde `sistema/rocola/Xp`, línea, la de antes y la de ahora). La marca:
`// ROCOLA OS gNN: (XP) <código de antes> — <para qué>`. Fuera de `Codigo/` el paréntesis dice `(antes)` en vez de `(XP)`: lo de
`Nativo/` no es código de XP, y escribir «(XP)» sería falso (`ganchos.py` acepta los dos fuera de `Codigo/` y exige `(XP)` dentro).

| Id | Dónde | Qué cambia | Por qué |
|---|---|---|---|
| g01 | `Codigo/Form1.Ia.cs:61` | Solo el comentario: `// EN LINUX: (XP) sin «…SinSitio()»: …` → `// ROCOLA OS g01: (XP) private bool IaBudgetFull() { return iaMaxBytes > 0 && … >= iaMaxBytes; } — …` | La decisión 1 (aprobada): los 7 de lo grande son de Rocola OS, no de traducción |
| g02 | `Codigo/Form1.Ia.cs:70` | Ídem (`IaDataFile`, `RutaDatos(...)` de XP) | Ídem |
| g03 | `Codigo/Form1.Ia.cs:251` | Ídem (`IaIndexFile`) | Ídem |
| g04 | `Codigo/Form1.Ia.cs:322` | Ídem (`rocola_ia_datos.generos.tsv`) | Ídem |
| g05 | `Codigo/Form1.Ia.cs:493` | Ídem (el índice y los géneros) | Ídem |
| g06 | `Codigo/Form1.Datos.cs:2483` | Ídem (`BackupBaseDir`) | Ídem |
| g07 | `Codigo/Form1.Portadas.cs:718` | Ídem (`rocola_miniaturas`) | Ídem |
| g21 | `Nativo/PantallaRocola.Rocola.cs:72` | `rocola.Ventana.Ensena();` → `RocolaOs.Arranca(rocola); rocola.Ventana.Ensena();` | Lo de Rocola OS arranca en el hilo de la pantalla, con la rocola construida y antes de enseñarse la ventana (§2.2). Sin `ROCOLA_OS`, `Arranca` vuelve en la primera línea |
| g22 | `Nativo/Form1.Ventana.cs:35` | `a.Cerrandose = OnFormClosing;` → `a.Cerrandose = RocolaOsEnvuelveElCierre(OnFormClosing);` | Rocola OS sabe la razón del cierre y para lo suyo ANTES de que XP guarde (o borre el estado). Sin `ROCOLA_OS`, `RocolaOsEnvuelveElCierre` devuelve el MISMO delegado de XP (el mismo objetivo y el mismo método, el mismo orden) |

En g01-g07 los bytes del código no cambian, solo lo que va detrás de `   // `; el texto «(XP)» es el de `Rocola/` (`Form1.Ia.cs:64, 73,
254, 325 y 496`, `Form1.Datos.cs:2486`, `Form1.Portadas.cs:721`). Los cinco archivos tocados conservan sus líneas y sus finales (CRLF y LF
contados uno por uno contra `HEAD`: iguales). g21 y g22 son una línea cambiada en su sitio (fuera de `Codigo/` se podrían añadir líneas,
pero así el manifiesto es igual para todos y `ganchos.py` lo comprueba igual).

**g08 a g20** (los del plan) no se tocaron: son de sus grupos. Los números nuevos, desde g21 (el siguiente libre de la rama principal;
si dos grupos en paralelo toman el mismo, la junta renumera).

## Cómo se usa (las interfaces para los demás grupos)

### 1. Una función: se registra sola

Cada grupo pone lo suyo en **sus** archivos de `RocolaOs/` y lo registra con un `[ModuleInitializer]` propio. Nada que tocar en un
archivo central. El registro solo da el nombre, las claves y los delegados: **nada de trabajo** (corre siempre, también sin
`ROCOLA_OS`, antes del `Main` de XP: ni archivos, ni la variable, ni Form1). Un nombre o una clave repetidos o mal escritos no lanzan
(tumbaría el programa antes de abrir): se apuntan en la bitácora al arrancar y esa función (o esa clave) no se usa.

```csharp
// RocolaOs/Red.cs (ejemplo: el grupo 2a)
public partial class Form1   // (si necesita lo privado de XP; los nombres, todos con RocolaOs delante)
{
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void RocolaOsRegistraRed()
    {
        RocolaOs.Registra(new RocolaOsFuncion
        {
            Nombre = "red",                       // la palabra de ROCOLA_OS y su clave en rocola_os.txt
            Que = "mirar si hay internet",
            DeFabrica = true,                     // con ROCOLA_OS=1, encendida salvo «red=0» en rocola_os.txt
            Claves = new RocolaOsClave[] {
                RocolaOsClave.Entero("red_cada_s", 30, 5, 600, true, "cada cuantos segundos se mira"),
            },
            Arranca = f => f.RocolaOsArrancaRed(),          // en la pantalla, antes de ensenarse la ventana
            Latido = f => f.RocolaOsLatidoRed(),            // cada segundo, en la pantalla: rapido
            Vivo = (f, sb) => sb.Append("red=").Append(RocolaOsRedEstado).Append('\n'),
            ParaAlCerrar = razon => RocolaOsParaLaRed(),    // en su hilo, con el plazo comun
        });
    }
}
```

Todos los delegados son opcionales. Lo que corre en la pantalla (`Arranca`, `Latido`, `Vivo`, `AlCerrar`, `AlCambiar`) va protegido:
lo que lance se apunta y la rocola sigue.

### 2. ¿Va? La pregunta única

- `RocolaOs.Encendida("red")`: si la función va en esta sesión. Sin `ROCOLA_OS` o con «0», no (sin leer nada). Con «1», su clave en
  `rocola_os.txt` (`red=0` la apaga) o su `DeFabrica`. Con una lista (`ROCOLA_OS=vida,red`), si está en ella (el archivo no la enciende
  ni la apaga). Se decide una vez (a más tardar en `Arranca`) y vale toda la sesión: cambiar su clave con la orden «ajuste» vale desde
  el siguiente arranque. Desde cualquier hilo y sin cerrojos (lo puede preguntar un gancho caliente).
- `RocolaOs.Pedida("x")`: lo mismo, pero solo con la variable, sin leer `rocola_os.txt`. Para un gancho que corre antes de `Arranca` y
  no puede leer archivos: **g11** (un inicializador de campo de Form1). Con una lista, si está en ella; con «1», su `DeFabrica` (el
  archivo aún no se leyó: lo que se decida con `Pedida` se corrige en el `Arranca` de la función con `Encendida`).
- `RocolaOs.Activo`: si `ROCOLA_OS` está puesta y no es «0». Lo primero que mira un gancho.
- `SoloEnLaLista = true`: una función de prueba, que solo va si la lista la nombra (con «1» nunca, diga lo que diga el archivo).
- Un valor raro de la variable (ni «1» ni «0», p. ej. «si») se toma como lista (sin nada encendido) y la bitácora lo dice.
- No se pregunta en un `[ModuleInitializer]` (el registro aún no está completo).

### 3. Los ajustes, en `rocola_os.txt`

En la carpeta de datos de `rocola-xp` (`/rocola/datos/xp`: la `RutaDatos` de XP, con `ROCOLA_DATOS`). **No** en `rocola_config.txt`
(XP lo reescribe entero con solo sus claves). `rocola_config.txt`, el importador y las `dir=` del lanzador no cambian.

- Cada función **declara** sus claves en `RocolaOsFuncion.Claves`: `RocolaOsClave.SiNo(nombre, deFabrica, enVivo, que)`,
  `Entero(nombre, deFabrica, minimo, maximo, enVivo, que)` o `Texto(nombre, deFabrica, vale, enVivo, que)`. `enVivo`: si cambia sin
  reiniciar el programa (la función la lee cada vez o atiende `AlCambiar`). La clave de encender o apagar la función (su nombre) la
  declara `Registra`.
- Leer, desde cualquier hilo y sin cerrojos (una foto que se cambia entera: el hilo del sonido nunca espera):
  `RocolaOs.Ajuste("clave")` (texto), `AjusteSiNo`, `AjusteEntero`, `AjusteCambiado` (si el dueño la cambió). Si no está o no vale,
  el de fábrica (y la bitácora lo dice una vez). Sin `ROCOLA_OS`, siempre el de fábrica y sin leer nada.
- Cambiar: `RocolaOs.PonAjuste("clave", "valor")` (vacío: vuelve al de fábrica, se quita su renglón). `false` si la clave no está
  declarada o el valor no vale. Se guarda detrás.
- El archivo: `clave=valor` por renglón, UTF-8 sin marca y LF (lo leen también los guiones con BusyBox). **Solo lleva lo que alguien
  cambió.** Se conserva todo lo que no conoce en su sitio y su orden (claves de otras versiones, comentarios, renglones raros); de una
  repetida vale la última y al cambiarla queda una (donde la primera). **Nunca llaves** (el buzón lo sube con los registros).
- Se escribe con `EscribeSeguro` de XP (el de paso, el `.bak`, el cambio de nombre) por su `GuardaDetras` (cuenta para
  `WaitDataWrites`: el cierre lo espera), con lo último y solo si cambió. Se lee con `LeeLineasSeguro` de XP (si falta el bueno, el
  `.bak`).
- El respaldo diario: en su propia carpeta del de XP (`respaldo/rocola-os/<fecha>`, en `ROCOLA_IA_DATOS` si está, y en `respaldo_dir=`
  si hay), con las funciones de XP (captura en el hilo de la pantalla, la copia comprobada detrás; las 3 más nuevas). Al arrancar y al
  cambiar el día del negocio (7 AM). La lista de XP (`BackupLearnedData`) no se toca, y su `PruneBackups` no ve esta carpeta (no es una
  fecha).
- **La orden «ajuste» del buzón (para 1a):** `string RocolaOs.AjusteDeFuera("clave=valor")`, en el hilo de la pantalla (1a la llama
  desde el latido al ver `/run/rocola-ajuste`). Solo claves declaradas; valida el valor; lo guarda; le avisa a su función (`AlCambiar`)
  si está encendida y la clave cambia en vivo. Devuelve la respuesta para el buzón, una línea: `bien: clave=valor[ (el de fabrica)][
  (vale desde el siguiente arranque del programa)]` o `mal: ...` (con las claves que hay, o los valores que valen). Ejemplo:

```csharp
// en el latido de 1a
string pedido = RocolaOs.Run("rocola-ajuste");
if (File.Exists(pedido)) { string r = RocolaOs.AjusteDeFuera(File.ReadAllText(pedido).Trim()); File.Delete(pedido); /* r, al buzon */ }
```

### 4. El latido

Un reloj de la pantalla de 1 s con nombre fijo, `new Reloj("RocolaOs:latido", null)` (el constructor con nombre de `Nativo/Reloj.cs`),
que **solo se crea con `ROCOLA_OS`** (en `Arranca`). Vence con la regla de WM_TIMER, como los de XP. Llama al `Latido` de cada función
encendida, en el orden en que se registraron. Reglas: rápido (mirar el estado de XP y archivos de `/run`, que está en memoria); nada
que espere (eso, en un hilo); nada de `await`; ningún reloj propio (todo por el latido o por encargos). Un latido que bombea la pantalla
(como el diagnóstico de XP) no vuelve a entrar en sí mismo (se cuenta como «saltado»). Se mide: `RocolaOs.Latidos`,
`LatidoUltimoMs`, `LatidoMaxMs`; uno de 50 ms o más se apunta (como mucho uno por minuto, con la función más lenta). Para al cerrar.

### 5. `/run/rocola-vivo.txt`

El archivo lo escribe **1a** (cada 15 s, desde el latido, de un tirón). Cada función aporta sus renglones con su `Vivo`: renglones
enteros con su `\n` (si falta, se pone). 1a los junta con `RocolaOs.RenglonesVivos(sb)`, después de los suyos. Si un `Vivo` lanza, lo
que había añadido se quita y se apunta.

### 6. Al cerrarse

El envoltorio de g22 llama a `RocolaOs.SeCierra(rocola, razon)` antes de `OnFormClosing` de XP, **una vez** (el apagado avisa dos
veces). Las razones no cambian (regla del 8-11-2026): SIGTERM → `WindowsShutDown` (XP borra el estado), Alt+F4 → `UserClosing` (lo
borra), SIGUSR1 → `TaskManagerClosing` (lo conserva); un apagón no corre nada.

1. Apunta la razón (`RocolaOs.RazonDelCierre`, `RocolaOs.Cerrandose`) y para el latido.
2. `AlCerrar(rocola, razon)` de cada función, **en el hilo de la pantalla**, una detrás de otra: rápido; puede leer y cambiar el estado
   de XP, que aún no se guardó.
3. `ParaAlCerrar(razon)` de cada una, **cada una en su hilo y todas a la vez**, para parar procesos e hilos (`rocola-spotify`, ffmpeg,
   el entrenamiento), sin tocar el estado de XP. Se esperan como mucho **4 s entre todas** (`PlazoAlCerrarMs`); lo que no acabó sigue de
   fondo mientras XP cierra y muere con el programa.
4. La bitácora dice cuánto tardó cada una (`cierre (WindowsShutDown): red (su hilo) 3 ms; ...; en total N ms`).

Por qué 4 s: el lanzador y el buzón dan 20 s, y XP, con lo pendiente por escribir, puede esperar hasta 15 s (`WaitDataWrites(15000)`
en `OnFormClosing` con «cierre por otra causa») más 3 de la bitácora; lo normal aquí son 30 a 90 ms. Lo de cada grupo se **mide** (5a,
4b, 8a), no se supone.

**Las salidas propias (42 a 46) son de 1a**: ponen `Environment.ExitCode` (el `Main` de XP es `void`) y van por las dos vías que ya hay:
`PantallaRocola.CierreDeFuera()` (conserva el estado: 42, 43, 45, 46) o `PantallaRocola.FinDeSesion()` (lo borra, como el botón: 44).

### 7. Los encargos

`RocolaOs.Encarga("nombre", () => ...)`, desde cualquier hilo: un encargo al hilo de la pantalla con nombre fijo
`RocolaOs:<nombre>` (`PantallaRocola.EncolaDesde(rocola, ..., "RocolaOs:<nombre>", false)`). Las trazas lo nombran así (no mueve los
nombres de XP, que van por archivo:línea; `--ignora RocolaOs:` lo quita). Viaja con la vida de la ventana de la rocola: antes de
enseñarse o después de cerrarse no se encarga (devuelve `false`) y uno que llega con la ventana cerrada no corre. Lo que lance se apunta.
`RocolaOs.EnLaPantalla` dice si quien llama es el hilo de la pantalla.

### 8. Los hilos

- `RocolaOs.Hilo("nombre", () => ...)`: **lo de fondo**, por debajo del repaso de XP (que va en la mínima de la rocola, nice 4): nice
  19, el «Prioridad.Fondo» del programa anterior. Para emparejar, indexar, convertir, entrenar, mirar la red...
- `RocolaOs.Hilo("nombre", ThreadPriority.X, () => ...)`: un nivel de XP (`PrioridadHilo.Pon`), para lo poco que no puede ir al fondo
  (lo que alimenta al sonido). Nunca por delante de la salida de sonido de XP.
- Los dos: de fondo (no mantienen vivo el programa), con nombre `RocolaOs:<nombre>`, la prioridad puesta al empezar en el propio hilo
  (en Linux cada hilo nace con el nice del que lo crea: uno creado desde la pantalla correría a −6), y lo atrapan TODO (una excepción
  sin atrapar en otro hilo cierra el programa; el lanzador lo cuenta como caída): se apunta. `ThreadInterruptedException` se toma como
  «párate».
- Un hilo no toca el estado de XP (a los hilos, una copia), ni OpenGL, ni la pantalla; el hilo del sonido nunca espera a otro.

### 9. Las rutas

- `RocolaOs.Run("rocola-vivo.txt")`: lo vivo, en memoria: `/run`, o `ROCOLA_RUN` en las pruebas (como los guiones del sistema).
- `RocolaOs.Datos("rocola_algo.txt")`: lo pequeño, en la carpeta de datos (nombres `rocola_*.txt`: así `rocola-instalar` les traduce
  las rutas y el buzón los encuentra).
- `RocolaOs.Grande("convertidas")`: lo grande, en el disco de la rocola (`DatosGrandes.Ruta`: `ROCOLA_IA_DATOS`).
- Las llaves (`rocola_github.txt`, `rocola_buzon.txt`, `rocola_genius.txt`, `/rocola/datos/spotify`) se leen donde están: nunca se
  copian, se apuntan ni se suben. En `/rocola/datos` (lo de `rocola-consola`) no se escribe.

### 10. La bitácora (la decisión)

Dos sitios, porque son dos cosas:
- **Lo que pasa** (qué arrancó, qué se encendió, un ajuste de fuera, cuánto tardó el cierre, un latido lento): `RocolaOs.Apunta(quien,
  que)` → `rocola_os_bitacora.txt`, **propia**, en la carpeta de datos (`fecha  [quien] que`, LF). Así no llena `rocola_error.txt` de
  XP, que **se borra entera** al pasar de 256 KB (`Bitacora.Pega`) y se llevaría los errores de XP. El buzón ya la sube con los
  registros (`rocola*.txt` de `/rocola/datos/xp`, como `xp_rocola_os_bitacora.txt`).
- **Los fallos** (una excepción atrapada en un hilo, un latido, un encargo, un arranque, un cierre): `RocolaOs.Falla(quien, que, ex)` →
  un renglón en la bitácora propia y **además** la pila en `rocola_error.txt` con el formato de `LogError` de XP
  (`[Rocola OS: quien: que]` y el sello de la versión), porque ahí miran el dueño y el buzón (`estado.json` lleva sus últimos 40
  renglones) y junto a los de XP se ve qué pasó antes de qué. Para no tapar los de XP, el mismo fallo (quién, qué, tipo y mensaje) se
  apunta como mucho una vez cada 10 minutos, y el siguiente dice cuántas veces se calló.
- Las dos con la `Bitacora` de XP (un hilo de fondo: quien apunta no espera al disco; tope de 256 KB; el `Main` de XP la vacía al
  salir). Sin `ROCOLA_OS` no se apunta nada.

### 11. Las órdenes de prueba de la línea de mandatos

`rocola-xp --prueba-algo ARGUMENTOS`, como `VersionRocolaOs` e `ImportaXp`: en el `[ModuleInitializer]` del grupo,
`RocolaOsOrdenes.Si("--prueba-algo", argumentos => { ...; return 0; })`. Si la línea es esa orden, la corre, atrapa lo que lance (sale
con 70), vacía la salida y acaba el programa con lo que devolvió; si no, no hace nada. **Lo que hay que saber** (comprobado):
- Los inicializadores corren en el orden de los archivos (`dotnet msbuild -getItem:Compile`: alfabético sin distinguir mayúsculas;
  `Codigo/`, `Nativo/`, `Plataforma/`, `RocolaOs/`). Los de `RocolaOs/` van **después** del de `Plataforma/PruebasLinux.cs`, que abre la
  pantalla (`Anfitrion.Arranca`: KMS, o fuera de pantalla con `ROCOLA_PANTALLA=fuera`). Una orden de `RocolaOs/` corre con la pantalla
  ya abierta: en las pruebas y desde la consola de mantenimiento, con `ROCOLA_PANTALLA=fuera`. Correr **antes** de la pantalla
  (`--prueba-spotify` de 2e, si no quiere quedarse con ella) pide un gancho en `Plataforma/PruebasLinux.cs`: se le pide al coordinador.
- Dentro de un inicializador **no hay otros hilos**: uno que entre al programa espera a que acaben los inicializadores (medido con JIT:
  un hilo que llama a un método del programa no acabó en 3 s; lo mismo dice `PruebasLinux.cs` del video). Una orden que necesite hilos
  (la salida de sonido de XP, procesos con lectura de fondo) lo hace todo en este hilo o pide el gancho de después del inicio
  (`PruebasLinux.DespuesDelInicio`).
- Al correr una orden solo están registradas las funciones de los archivos que van antes: la orden registra primero lo que necesita
  (`PruebaAndamio.cs` lo hace así).
- Sin la rocola no hay `GuardaDetras`: una orden que cambie ajustes los escribe con `RocolaOs.GuardaYa()`.

### 12. Un gancho nuevo en `Codigo/`

1. Una línea que ya existe, cambiada en su sitio: envuelve una expresión o pone una llamada al principio o al final, y llama a UN método
   `RocolaOsXxx` (en una parte de Form1 de `RocolaOs/`) que devuelve lo de XP si lo suyo está apagado.
2. La marca: `// ROCOLA OS gNN: (XP) <código de antes> — <para qué>`.
3. Los mismos finales de línea (`Codigo/Form1.Video.cs` va todo en LF; muchos archivos mezclan CRLF y LF; `Form1.Datos.cs` 671 y 858
   llevan CR CR LF). Editar con algo que respete los bytes.
4. Su renglón en `RocolaOs/GANCHOS.tsv` (sin tabuladores ni CR dentro: esas dos líneas de `Form1.Datos.cs` no caben en el formato).
5. `python3 Pruebas/RocolaOs/ganchos.py` → `TODO BIEN`; `python3 Pruebas/Tarjeta/guiones/diff-xp.py --sin-ganchos $(ls Codigo)` → las
   cuentas de la traducción de siempre.
6. Los arneses (`Pruebas/Vistas*`) compilan `RocolaOs/` (abajo): un gancho que llama a `RocolaOs` compila también allí.

### 13. Las pruebas de cada grupo

- Nivel 1 (con `ROCOLA_OS` puesto, contra la referencia): las escenas con `ROCOLA_OS=...` en el entorno (pasa por `corre.sh` hasta el
  programa) y `TRAZAS_IGNORA=RocolaOs: ./compara.sh R N` (o `compara_trazas.py ... --ignora RocolaOs:`).
- Una función de ejemplo que usa cada enganche: `RocolaOs/PruebaAndamio.cs`, y su prueba, `Pruebas/RocolaOs/andamio/prueba.sh`.

## Las herramientas

- **`Pruebas/RocolaOs/ganchos.py [--base REV] [--manifiesto RUTA]`**: el manifiesto (5 columnas, ids gNN sin repetir); cada «ahora» en
  su sitio (en `Codigo/`, en su línea; fuera, una sola vez en el archivo, y si se movió lo dice) y con su marca y su raya; ninguna otra
  línea de `Codigo/` lleva «ROCOLA OS», ni de `Nativo/` y `Plataforma/` la marca «ROCOLA OS gNN»; devolviendo las de antes, los 109
  archivos de `Codigo/` byte a byte como en la base (`fc7b667`, o `--base` si `Codigo/` cambia por un cambio de XP), y la de antes de
  los de fuera está en ese archivo de la base; el resumen. Sale con 0 solo si todo está bien.
- **`diff-xp.py --sin-ganchos`**: devuelve los ganchos en los bytes antes de comparar (por líneas como las cuenta el manifiesto: la
  lectura de texto de Python cambia un CR suelto por un salto de línea), los cuenta aparte y da el total. Sin la opción, la salida de
  siempre.
- **`compara_trazas.py --ignora PREFIJO`** (o `TRAZAS_IGNORA=PREFIJO[,...]`, para `compara.sh` y `compara_escena.py`, que lo llaman sin
  la opción): quita los relojes y los encargos (también los perdidos) cuyo nombre empieza por el prefijo, con una `NOTA` de cuántos en
  cada traza. Sin la opción ni la variable, nada cambia. (Ojo: un reloj nuevo con pocos tics no sale como diferencia sin `--ignora` si
  la holgura del ruido es mayor que su cuenta; por eso la prueba propia comprueba además que `--ignora` quitó justo los del latido.)

## Archivos de otros (toca_otros_archivos)

- **Los 17 `.csproj` de los arneses** (`Pruebas/Vistas*/*.csproj` y `Pruebas/BancoVistas/BancoVistas.csproj`): su línea
  `<Compile Include="$(Fuente)/Codigo/**/*.cs;$(Fuente)/Plataforma/**/*.cs;$(Fuente)/Nativo/**/*.cs" .../>` lleva además
  `;$(Fuente)/RocolaOs/**/*.cs`. Una línea cambiada en su sitio en cada uno. **Hace falta**: los arneses compilan el programa entero de
  su carpeta `Fuente` y, con g21 y g22 (y con cada gancho que llegue a `Codigo/`), `Nativo/` y `Codigo/` llaman a `RocolaOs/`: sin la
  línea no compilarían. Con la referencia congelada como `Fuente` no existe la carpeta y no cambia nada (un comodín sin nada no es error).

## Comprobaciones

PENDIENTE

## Para lo visual

El andamio no dibuja nada. Lo que deja listo para la ventana de Ctrl+F4 (K11) y para quien enseñe algo:
- Las funciones registradas y las encendidas (`RocolaOs.Registradas`, `Encendidas`, con su `Que`), y si se encendieron por la lista o
  por el archivo (`ConLista`).
- Cada clave declarada con lo necesario para un mando: `RocolaOsClave.Nombre`, `Que`, `Tipo` (qué valores valen, dicho para el dueño),
  `DeFabrica`, `EnVivo` y su función (`Duena`); `NombresDeClaves()`, `Ajuste`, `AjusteCambiado` y `PonAjuste` (lo mismo que hace la orden
  «ajuste»). Lo que no puede actuar va en gris diciendo por qué (regla del 16-10-2026): hoy no hay un campo «necesita»; lo añade la
  ventana cuando haga falta.
- Que una función cambia en el siguiente arranque (su clave de encender, o `EnVivo = false`): la respuesta de `AjusteDeFuera` ya lo dice.
- El coste: `LatidoMaxMs`, `LatidoUltimoMs`, `Latidos`, `CierreMs`, y la bitácora propia.

## Lo que queda

- **1a (vida)**: escribir `/run/rocola-vivo.txt` (con `RenglonesVivos`), `/run/rocola-xp-sabe`, atender `/run/rocola-ajuste` con
  `AjusteDeFuera`, «libre» y las salidas 42 a 46 (`Environment.ExitCode` y `CierreDeFuera`/`FinDeSesion`).
- **1b (guiones)**: que el lanzador ponga `ROCOLA_OS=1` (salvo con `/rocola/datos/xp/rocola_os_apagado`); la orden «ajuste» del buzón
  (deja `/run/rocola-ajuste`); `rocola-actualiza` lee `actualizar_solo=` de `rocola_os.txt`.
- **nivel0 (0b)**: la batería; tiene que llevar `ganchos.py`, `diff-xp.py --sin-ganchos` y, para el nivel 1, `TRAZAS_IGNORA=RocolaOs:`.
- **2e** (`--prueba-spotify`): si necesita correr sin la pantalla o con hilos, el gancho en `Plataforma/PruebasLinux.cs` (§11).
- La prueba propia, en «uno» (`ROCOLA_OS=1`), supone que ninguna función va encendida de fábrica (lo dice dentro); cuando las haya, lo que
  vale ahí es la comparación con `--ignora`.

## Riesgos

- **El plazo del cierre (4 s) no es de XP.** Con `ROCOLA_OS`, lo de Rocola OS va antes de que XP guarde y le quita como mucho 4 s de
  los 20 que dan el lanzador y el buzón; XP en el peor caso espera 15 s a lo pendiente por escribir (más 3 s de la bitácora): si un día
  coincidieran los dos peores casos, a los 20 s llegaría el SIGKILL (como un apagón: el estado sobrevive). Cada grupo mide lo suyo; si
  algo de verdad necesita más, se discute antes de subir el plazo.
- **`AlCerrar` corre en la pantalla y no se corta**: una función que espere ahí detiene el cierre entero. Lo que espere va en
  `ParaAlCerrar` (en su hilo, con el plazo). Se revisa en cada grupo.
- **El latido y los encargos se cuentan en las trazas.** El latido mete un tic por segundo y, tras cada uno, el reposo de la pantalla
  (`Libre`) vuelve a salir una vez; medido con `ROCOLA_OS=1`: las escenas salen IGUALES contra la referencia quitando lo de `RocolaOs:`
  (abajo). Una función que haga mucho en su latido o encargue mucho movería las cuentas de los relojes de XP: el nivel 1 de cada grupo lo
  mira, y el latido lento se apunta.
- **El orden de los inicializadores** (`RocolaOs/` después de `Plataforma/`: la pantalla ya abierta) y **sin hilos dentro**: una orden
  de prueba que no lo sepa se queda con la pantalla o se cuelga esperando a un hilo (§11).
- **Un error de registro no avisa al compilar**: una clave mal escrita, o repetida entre dos grupos, se queda fuera y solo se ve en la
  bitácora al arrancar (`PonAjuste` devuelve `false`). La junta mira la bitácora del nivel 1.
- **Con una lista en `ROCOLA_OS`**, `rocola_os.txt` no enciende ni apaga funciones (solo sus demás ajustes): es para las pruebas; el
  lanzador pone «1».
- **Encender o apagar una función vale desde el siguiente arranque** (se decide una vez): la orden «ajuste» lo dice en su respuesta. Lo
  que el dueño quiera cambiar en vivo (p. ej. `spotify=`) va como clave de la función con `enVivo`, no como su clave de encender.
- **`Pedida` con «1» es una suposición** (el valor de fábrica, sin el archivo): quien la use corrige en su `Arranca` con `Encendida`.
- **La función de prueba va dentro del programa** (`RocolaOs/PruebaAndamio.cs`, como las órdenes de prueba de `Plataforma/`): solo corre
  con `ROCOLA_OS=prueba-andamio` (con «1» nunca, ni con su clave en el archivo); su orden, solo con `--prueba-rocola-os`.
- **El manifiesto no admite tabuladores ni CR dentro de una línea**: `Codigo/Form1.Datos.cs` 671 y 858 (CR CR LF) no se pueden enganchar
  sin ampliar el formato. Y si cambia `Rocola/` y se pasa a `Codigo/` una línea con gancho, `ganchos.py` falla (a propósito) y el gancho
  se rehace a mano, con `--base` nuevo.
- **Los números de los ganchos nuevos** los toma cada grupo en paralelo desde g23: dos pueden chocar; la junta renumera (el número va en
  la marca de la línea y en el manifiesto: los dos a la vez).
- **`rocola_error.txt` es de XP**: los fallos de Rocola OS van también ahí (el mismo, como mucho uno cada 10 min). Un fallo distinto cada
  vez (con un mensaje que cambia) no se calla: lo vería la bitácora y la junta.
- **Nada de esto está probado en el A4.** Fuera de pantalla con llvmpipe, en esta máquina compartida.
