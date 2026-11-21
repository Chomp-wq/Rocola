# Pruebas de Rocola OS (fase 11): la batería del nivel 0

`nivel0.sh` es la **puerta de cada cambio de la fase 11** (`PLAN_FASE11.md` §2.6): `rocola-xp` **sin `ROCOLA_OS`** contra la copia
congelada de la referencia (aef2211), con lo mismo que la última junta del port (`verifica-junta17.sh` y `alterna.sh`, que vivían en el
scratchpad de la sesión que hizo el port; `PORT_JUNTAS.md`, la junta de N7) y lo que el plan le añade. Con `NIVEL=1` es el **nivel 1**: las
escenas y los recorridos con `ROCOLA_OS` puesto a una lista, comparando sin lo de Rocola OS. Nada de esto entra en el programa
(`Pruebas/**` está fuera de `RocolaXp.csproj`) ni dice nada del A4.

Los archivos:
- `nivel0.sh`: la batería (sus parámetros, en su cabecera).
- `alterna.sh`: cada escena con la referencia y en seguida con el programa, para que corran con la misma carga.
- `clasifica.py`: lo que la batería decide sola (lo igual, lo aceptado, lo que se repite, lo malo), para que quien la corre no tenga que
  interpretar nada.
- `avisos-base.txt`: los 13 avisos de compilar la base.
- `nivel0/prueba.sh`: la prueba de la batería misma (sin correr la rocola) y `nivel0/compara_trazas_ignora.py`, su doble de
  `compara_trazas.py --ignora`.
- (`ganchos.py` es del grupo andamio: lo corre el paso `ganchos`.)
- `apagones/prueba.sh`: la auditoría de apagones (`FASE11_apagones.md`): `kill -9`, caída, `SIGTERM`, `SIGUSR1`, `SIGHUP`, cortes justo
  al guardar (strace), `dirsync` y el importador; el corte de luz de verdad está en `sistema/rocola/Pruebas/Qemu/apagones.py`.
  Sus datos de partida (los tres tonos) los arma `apagones/datos.sh`; `prueba.sh` lo llama solo si faltan.

## Cómo se corre

```
bash sistema/rocola/Xp/Pruebas/RocolaOs/nivel0.sh ETIQUETA
```

- Comprueba la copia de trabajo donde está el guion (o `XP=.../sistema/rocola/Xp`): la compila desde cero en JIT (en `$TRABAJO/jit`) y la
  publica en NativeAOT (en `$TRABAJO/aot`). Con `PROG=carpeta` y `AOT=carpeta` usa un programa ya compilado y publicado.
- `TRABAJO` (de fábrica `/root/rocola-trabajo/fase11/nivel0-ETIQUETA`): ahí quedan `resumen.txt` (una línea por comprobación),
  `nivel0.log` (todo) y `registros/` (lo que dijo cada paso y cada comparación). Las salidas de las escenas quedan en `salidas/`, sin las
  capturas de las que salieron bien. Los programas compilados y publicados se borran al acabar (`GUARDA_BIN=1` los deja).
- `PASOS="..."` corre solo esos pasos (de fábrica, todos, en el orden de la tabla de abajo).
- Sale con **0** solo si todo es BIEN o ACEPTADO; con **1** si algo es MAL; con **2** si nada es MAL pero algo no se pudo comprobar (FALTA:
  falta algo de fuera o un programa; NO: una herramienta que aún no está, como `ganchos.py` antes de juntar el andamio, o el nivel 1 sin
  `--ignora`).
- Con la máquina libre tarda unos **TIEMPO_TOTAL min** (medido en la base: ver abajo). Con otros agentes trabajando, más: cada paso espera
  el candado.

Cada línea del resumen: `VEREDICTO  paso: los números  [segundos]`. Los veredictos:

| | |
|---|---|
| `BIEN` | igual a la referencia, o lo que se espera |
| `ACEPTADO` | distinto solo en lo aceptado (abajo), y dice qué |
| `MAL` | cualquier otra diferencia (con lo que se vio) |
| `FALTA` | no se pudo correr: falta algo de fuera, una herramienta o el programa |
| `NO` | no se comprobó: una herramienta de otro grupo que aún no está |

## Los pasos

Lo esperado es lo de la junta de N7 (`PORT_JUNTAS.md`, 14-11-2026); si un grupo amplía una prueba (más comprobaciones, más líneas), también
vale: lo que no puede es bajar. Los tiempos son de la corrida en la base (`b255f22`, que en el programa es `1afaade`), con la máquina
compartida con el grupo andamio.

| Paso | Qué comprueba | Lo esperado | Tiempo |
|---|---|---|---|
| `previo` | que estén lo de fuera del repositorio (abajo) y las herramientas (`dotnet`, `python3`, ImageMagick, `flock`, `unshare`, `ffmpeg`; `strace` y ser root, para `Pruebas/Importa`) | todo | 0 s |
| `codigo` | los 112 archivos de `Codigo/` (109 hasta `PORT_xp-1718.md`) con el mismo número de líneas, de CRLF y el mismo último byte que en `BASE` (8c8cba0, la de la junta 4, con Codigo/ byte a byte como d5bc2b0, la de la junta 3, y Nativo/ con lo de las ramas de rapidez: fc7b667 con los cambios de la rocola de XP del 17 y 18-11-2026, el modo noche de 50 a 90 % y el modo noche para los vecinos del 20-11-2026, PORT_noche-nv.md, con el arreglo de la subida de noche-50 juntado y la revisión de noche-nv, y los ganchos de todas las ramas devueltos también fuera de Codigo/; antes d5bc2b0, a76d008, 2c4b3c3, ca9802f, d6026b1 y ebdb272): un gancho cambia una línea en su sitio, nunca añade, quita ni mueve | 109 de 109 | 1 s |
| `ganchos` | `ganchos.py --base BASE` (del andamio: cada gancho del manifiesto en su sitio, con su marca, y `Codigo/` devolviéndolos byte a byte como la base) y `diff-xp.py --sin-ganchos` en los 112 archivos (si aún no tiene la opción, sin ella) | ganchos.py sale con 0; 5 147 líneas quitadas, 4 880 puestas y 229 sin marca (5 135 y 4 868 antes de `PORT_noche-nv.md`; 5 095 y 4 828 en los 109 de antes de `PORT_xp-1718.md`) | 1 s |
| `compila` | `dotnet build -c Release --no-incremental` de `RocolaXp.csproj` | 0 errores; 13 avisos, los de `avisos-base.txt` (archivo y línea; ninguno nuevo) | T_COMPILA |
| `arneses` | los doce `Vistas*/arnes.sh` (la copia y la referencia congelada compiladas con su entrada de prueba; las letras falsas y DejaVu), `VistasAcabadoEstilo/compara.sh`, `VistasVistasB/arnes.sh`, `VistasAjustes/compara.sh` y la frontera con la tarjeta (`VistasFrontera`: se compila con la copia y con la referencia y `compara.sh`) | IGUALES; VistasFondo solo su línea de marca; acabado-estilo 0 de 6 603, vistas-b 0 de 3 108, Ctrl+F3 0 de 545 (las dos letras, 0 con excepción); frontera 0 de 106 líneas y 0 de 153 texturas | T_ARNESES |
| `banco` | el banco del dibujo (`BancoDibujo`): el nativo contra el congelado de la referencia, 3 000 casos con las semillas 7 y 11 (letras falsas) y 5 (DejaVu) | iguales las tres | T_BANCO |
| `cintas` | las 29 cintas de la tarjeta (`VistasFrontera/cintas.sh`, con el reproductor compilado de `Pruebas/Tarjeta`) contra sus salidas de referencia | 29 iguales (6 616 cuadros) | T_CINTAS |
| `recorridos` | los nueve recorridos (`Recorridos/recorridos.sh` con `VOLCADO=1 CAPAS=1 HUELLAS=1`, uno por uno con los dos candados) contra las dos corridas de la referencia: lo QUIETO (lo que sale igual en las dos) | 9 de 9 salen con 0; capas quietas 213 de 217 (las cuatro de siempre); las texturas quietas que faltan, solo las de la hora | T_RECORRIDOS |
| `ventanas` | las reglas de la hoja (`pruebas_hoja.sh`, JIT), de la pantalla (`pruebas_pantalla.sh`, JIT y NativeAOT) y la lista de la ventana (`PantallaVistas`) | 86 de 86; 32 de 32 y 32 de 32; 78 de 78 | T_VENTANAS |
| `escenas` | las 24 escenas de la junta 17 (arranque, moneda, video, tab, buscador, cierreq, altf4, sigterm, f3sigterm, kill9, asistente, captura, salirq, f2, f2barras, f2sin, aviso, f3vistas, f3grises, f3teclas, f3raton, f3billar, f12, f12vigia), cada una alternada con la referencia con trazas y comparada con su ruido (`compara_escena.py`) | IGUALES salvo lo aceptado | T_ESCENAS |
| `aot` | `dotnet publish -p:PublishAot=true` y las 7 escenas de NativeAOT (arranque, cierreq, kill9, buscador, f3vistas, f3teclas, f12) alternadas con la referencia | salida 0, 0 avisos IL/AOT; las 7 IGUALES | T_AOT |
| `cierre` | `cierre_rocola_os.sh` (`--version` y SIGUSR1, con y sin hojas y con video) en JIT y en NativeAOT | 43 de 43 en los dos | T_CIERRE |
| `importa` | `Pruebas/Importa/prueba.sh` (el importador de lo aprendido en XP y lo grande fuera de lo aprendido) en JIT y en NativeAOT | 151 de 151 (145 antes de `PORT_noche-nv.md`) | T_IMPORTA |

## Lo aceptado (lo decide `clasifica.py`)

Solo esto sale `ACEPTADO`; todo lo demás distinto es `MAL`:

- **`sigterm` y `f3sigterm`: el estado se borra.** SIGTERM es el apagado (regla del dueño del 8-11-2026): el programa cierra con
  `WindowsShutDown` y XP borra su estado; la referencia no atendía SIGTERM. Se acepta solo si lo que sobra en la secuencia es el cierre (el
  encargo `Apagado`, `closing`/`closed` con `WindowsShutDown` y, con una hoja abierta, `FormOwnerClosing`, el fin de su diálogo y el `pck`
  de la tecla que lo abrió, el foco que se va), nada falta, hay un `closed Form1'Rocola' WindowsShutDown` y `rocola_estado.txt` está en la
  referencia y falta en el programa.
- **`tab`: los rótulos del repaso.** Los rótulos de Ctrl+F3 que dicen lo que el repaso de la biblioteca lleva medido («Calidad: N
  aceptables», «faltan N archivos» / «ya está al día», «Los videos y las N canciones que faltan por medir...», «Empareja el tono... (van
  12)» con su porcentaje) dependen de cuánto avanzó el repaso con la carga de la máquina. Se acepta solo si cada rótulo distinto es el mismo
  rótulo (sitio, medida y banderas iguales) con uno de esos textos en los dos lados, y los puntos distintos de la capa de la hoja caen todos
  dentro de esos rótulos (se tapan y la capa tiene que salir igual). Las cuentas de los encargos del repaso (`Form1.Biblioteca.cs:*`) van con
  esa carrera. (Comprobado con una corrida de verdad: 8 rótulos y 3 789 puntos, todos dentro.)
- **`f12` y `f12vigia`: el reloj saturado de F12.** El reloj de la ventana de prueba (15 ms, un cuadro de la tarjeta por vuelta) va saturado
  fuera de pantalla y su cuenta es de la máquina (`PORT_arreglo-f12vigia.md`). `compara_trazas.py` ya compara su total con el 30 % cuando va
  saturado en las dos trazas; si aun así se sale, se acepta solo con su NOTA de saturado.
- **VistasFondo: la línea de marca** (`inicio	traducida=True/False`).
- **Recorridos: lo de siempre.** Las tres capas de `ctrlf3` a los 46 s con F12 abierta (la referencia pone un tramo transparente por
  ventana) y la hoja de Sonido de Ctrl+F3 en `hojas` (la capa de 966×764 en 29,2, que sigue detrás de las hojitas: dice lo que lleva medido
  el repaso; en las juntas, la de los 18 s; con carga, también la de los 19,5 y los 21 s; esa página la comprueban punto por punto, con el
  repaso ya acabado, f3vistas, f3grises y VistasAjustes); y las texturas quietas que faltan si son todas rótulos de la hora y del ritmo
  (1024×128, 1024×64, 512×256 o 1024×256) y no pasan del 25 % de las quietas del recorrido (`--tope`). Las listas salen de las 43 corridas
  de recorridos que guardaron los grupos del port (en `/root/rocola-trabajo/port`).

**Lo que se repite una vez** (`REPITE=1`, de fábrica): una escena distinta solo en cuentas de relojes, encargos o repeticiones (la carga de
la máquina las saca de la holgura: `PORT_JUNTAS.md`) o en el sitio de un volcado del estado dentro de la secuencia (`ROCOLA_ESTADO_EN`, a su
segundo fijo, que cae al otro lado de una tecla: la escena `video` en una junta del port) se vuelve a correr, alternada otra vez con la
referencia; si la segunda sale BIEN o con lo aceptado, queda así («a la segunda», con lo que dio la primera), si no, MAL. Lo mismo un
recorrido con una capa o textura quieta que no es de las de siempre.

## El nivel 1

```
NIVEL=1 ROCOLA_OS_LISTA=vida,red bash nivel0.sh ETIQUETA
```

Las mismas escenas (JIT y NativeAOT) y los nueve recorridos, con el programa nuevo corriendo con `ROCOLA_OS=lista` (con `=1` no basta: de
fábrica lo automático de la decisión 4 va encendido), una carpeta `/run` de prueba vacía para cada corrida (`ROCOLA_RUN`), y sin red
(`unshare -n`: otro espacio de red con solo la interfaz local; comprobado en esta máquina, como root: conectar a 1.1.1.1:443 da «red
inalcanzable»). La referencia corre como siempre. Las escenas se comparan con `compara_trazas.py --ignora RocolaOs:` (sin los relojes y
encargos de Rocola OS, `IGNORA`) y lo demás (estados, capas, `rocola_estado.txt`) igual que en el nivel 0. Si `compara_trazas.py` aún no
tiene `--ignora`, las escenas no se corren y lo dice (`NO`). Los recorridos no miran trazas: se comparan igual que en el nivel 0. Pasos de
fábrica: `previo compila escenas aot recorridos`; `ESCENAS`, `ESCENAS_AOT` y `RECORRIDOS` eligen cuáles.

## El candado

Cada paso pesado (cada compilación, cada arnés, el banco, las cintas, cada recorrido, cada par de escenas, cada prueba de la hoja y de la
pantalla, el cierre y el importador) toma `PESADO` (`/root/rocola-trabajo/fase11/pesado.lock`) por su cuenta, y los recorridos, dentro,
`CANDADO_RECORRIDOS` (su carpeta de corrida es una sola: `/root/rocola-trabajo/port/dibujo/corrida`). Siempre con **`flock -o`**: el
candado lo tiene `flock` y lo que corre debajo no lo hereda.

Ojo, para quien use el candado: con `flock CANDADO dotnet build` (sin `-o`), el servidor de compilación de dotnet (`VBCSCompiler`) que
arranca esa compilación hereda el candado y se queda con él hasta que se apaga solo (10 minutos sin compilar). El 14-11-2026 pasó tres veces
seguidas (el candado tomado entre 7 y 10 minutos sin que nadie trabajara). Si pasa: `dotnet build-server shutdown --vbcscompiler` (con nadie
compilando) lo suelta.

## Lo que lee de fuera del repositorio, y cómo rehacerlo

Todo vive en `/root/rocola-trabajo` (fuera del repositorio, y nunca se toca: solo se lee; los recorridos escriben en su carpeta de corrida
con su candado). Si se pierde, esto lo rehace; lo marcado «no idéntico» sale equivalente pero no byte a byte, y entonces lo que depende de él
(el ruido, las salidas de referencia) hay que rehacerlo también con la referencia, nunca con el programa que se comprueba.

| Qué | Dónde (de fábrica) | Cómo rehacerlo |
|---|---|---|
| La referencia congelada (el código) | `REFXP=/root/rocola-trabajo/referencia-xp` | `git -C /home/user/Rocola worktree add --detach /root/rocola-trabajo/referencia-xp aef2211` (el commit está en la historia de la rama). Nunca se toca: es lo único que conserva la capa |
| La referencia con trazas (compilada) | `REF=/root/rocola-trabajo/port/ventanas/ref-trazas/sistema/rocola/Xp/bin/Release/net10.0` | `W=/root/rocola-trabajo/port/ventanas bash Pruebas/Ventanas/guiones/referencia.sh`: copia el `sistema/rocola/Xp` de la referencia (más `Gl.cs`, `Kms.cs` y `Botones.cs`), le pone `Pruebas/Ventanas/ganchos-referencia.patch` (los ganchos de las trazas, solo en su Plataforma) y la compila en JIT |
| Los datos de las escenas | `DATOS0=/root/rocola-trabajo/port/ventanas/datos0` | `W=/root/rocola-trabajo/port/ventanas bash Pruebas/Ventanas/guiones/datos.sh ORIGEN`: la música y los `rocola_*.txt` (con `rocola_estado.txt`) de una carpeta de datos de prueba; la de siempre es `sistema/rocola/Xp/bin/Release/net10.0` de la copia principal (fuera de git). Si se pierde también: dos discos inventados («Los Ejemplos/Disco Uno» y «Otra Banda/Primer Disco», tres MP3 cada uno de 3,2 s, 128 kbps, 44,1 kHz estéreo) y un video («Zeta Videos/Clips/Video de prueba.mp4»), hechos con `ffmpeg`, y sus tablas medidas abriendo la rocola de la referencia con ellos; **no idéntico** |
| Los datos de las escenas de la hoja | `DH=/root/rocola-trabajo/port/ventanas2/datos` | `W=... DH=... bash Pruebas/Ventanas/guiones/datos_h.sh` (sale de `datos0`: con 21 carpetas de más, sin la música, el árbol del selector y `avisos_claros=0`). Lleva su propia ruta dentro (`dir=$DH/extra-vacia`): rehacerlo en el mismo sitio |
| El ruido medido | `RUIDO=/root/rocola-trabajo/port/ventanas2/ruido` y `/root/rocola-trabajo/port/ventanas/ruido` | `W=/root/rocola-trabajo/port/ventanas bash Pruebas/Ventanas/guiones/ruido.sh $REF 5` (las diez escenas de siempre, 5 corridas, en `$W/ruido`) y `RUIDO=/root/rocola-trabajo/port/ventanas2/ruido bash ruido.sh $REF 6 asistente captura salirq f2 f2barras f2sin aviso f3vistas f3grises f3teclas f3raton f3billar f12 f12vigia` (las de hoy son de 3 a 7 corridas: su `corridas` lo dice). Siempre con la referencia: es lo que ella se separa de sí misma |
| Las letras de XP falsas | `LETRAS=/root/rocola-trabajo/port/dibujo/letras-xp-falsas` | Libres con los nombres de Windows: `arial*` = Liberation Sans, `times*` = Liberation Serif, `cour*` = Liberation Mono, `trebuc*` = FreeSans, `segoeui*` = Carlito, `tahomabd` = DejaVu Sans Bold (las de `/usr/share/fonts/truetype`, copiadas tal cual); `micross.ttf` = `python3 Pruebas/BancoDibujo/tablas_gdi.py LiberationSans-Regular.ttf micross.ttf 1` y `tahoma.ttf` = `tablas_gdi.py DejaVuSans.ttf tahoma.ttf 0` (con fontTools: `pip install --target $ROCOLA_TRABAJO/py fonttools`). Comprobado el 14-11-2026: las 19 copiadas, iguales byte a byte; las dos hechas, iguales tabla por tabla salvo la fecha de `head` (y su suma), que fontTools pone al guardar |
| Las salidas de referencia de los recorridos | `CMPREF=/root/rocola-trabajo/port/dibujo/salidas/cmp-ref` y `CMPREF2=.../cmp-ref2` | Dos corridas de la referencia (la congelada compilada: `/root/rocola-trabajo/port/dibujo/ref-bin`, o `$REF`): `flock /root/rocola-trabajo/port/recorridos.lock env ROCOLA_TRABAJO=/root/rocola-trabajo/port/dibujo VOLCADO=1 CAPAS=1 HUELLAS=1 bash Pruebas/Recorridos/recorridos.sh REFBIN /root/rocola-trabajo/port/dibujo/salidas/cmp-ref` y otra vez en `cmp-ref2`, con `ROCOLA_TRABAJO` en ese mismo sitio (la rocola enseña la ruta de su corrida). Las de hoy son del 8-11-2026 (07:35 y 07:49), anteriores al arreglo de la junta 14, que movió una flecha del recorrido `buscador` (`PORT_arreglo-junta14.md`). **No idéntico**: unas corridas nuevas dan otro QUIETO (las texturas de la hora caen distinto) |
| Los datos de los recorridos | `DIBUJO=/root/rocola-trabajo/port/dibujo` (su `datos-base/`) | Los de las escenas más un disco «Larga Duración» con dos canciones de 150 s (una con acentos, eñe y `&` en el nombre) y portadas en tres carpetas (JPEG opaco de 600×600, PNG con alfa de 300×300 y JPEG de 1600×1000), `PORT_DIBUJO.md` §2; **no idéntico** (y entonces, otra vez `cmp-ref` y `cmp-ref2`) |
| Las cintas de la tarjeta y sus salidas de referencia | `TARJETA=/root/rocola-trabajo/port/tarjeta` (`cintas/` y `salidas/*.ref.txt`) | Grabarlas con `Pruebas/Tarjeta/guiones/graba.sh NOMBRE FIN PULSA TRAMOS PROGRAMA` (la grabadora va en el programa: `ROCOLA_CINTA`) y sacar su salida de referencia con `reproduce.sh CINTA PROGRAMA` (el reproductor contra `$TARJETA/ref`, una copia de la referencia congelada compilada), `PORT_TARJETA.md` T0; **no idéntico** (las de hoy, del 8-11-2026: 17 cintas y 12 variantes) |

Los doce `Vistas*/arnes.sh` leen siempre la referencia de `/root/rocola-trabajo/referencia-xp` y las letras de
`/root/rocola-trabajo/port/dibujo/letras-xp-falsas` (fijo en sus guiones): con otro `REFXP` u otras `LETRAS`, el paso `previo` lo avisa.

## La prueba de la batería

`bash nivel0/prueba.sh` (unos segundos, sin correr la rocola): lo que decide `clasifica.py` con salidas hechas a mano en el formato de las de
verdad (escenas iguales; sigterm y f3sigterm con su cierre, y sin él, con el estado sin borrar o con otra razón; cuentas; el volcado del
estado en otro sitio; el reloj de F12 saturado y sin saturar; estados y teclas distintos; los rótulos del repaso, con los puntos de la capa
dentro y fuera de los rótulos, con un botón cambiado y con el porcentaje solo; el nivel 1 con el doble de `--ignora`, sin la opción y con
diferencias de verdad; los recorridos con lo de siempre y sin ello, también la hoja de Sonido; VistasFondo; los avisos; `Codigo/`), la
sintaxis de los guiones, que `nivel0.sh` corre sin `ROCOLA_OS` y lo del candado (`pesado()` espera a quien lo tiene, apunta cuánto esperó, y
lo que corre dentro no lo hereda). Sale «TODO BIEN (54 comprobaciones)».
