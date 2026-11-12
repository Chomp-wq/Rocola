# Port nativo: lo-suelto (W1, oleada 2 / tanda 5, 12-11-2026)

Seccion «2c. lo-suelto (W1)» de PLAN_TANDA4_A_N7.md. Rama `port-lo-suelto`, sobre bae626e. Lo que quedaba de WinForms fuera de las
ventanas y del dibujo: la carpeta del programa (Application.StartupPath) y el delegado MethodInvoker.

## Que se hizo

- **Nativo/CarpetaPrograma.cs** (nuevo): `CarpetaPrograma.Ruta`, la carpeta del programa sin la barra del final, como
  Application.StartupPath de WinForms (Path.GetDirectoryName(ExecutablePath)). Se calcula con `AppContext.BaseDirectory.TrimEnd('/', '\\')`,
  la misma cuenta que la capa (Plataforma/Ventanas.Form.cs:396 en bae626e; el plan decia :374, se ha movido). No se usa Assembly.Location:
  con NativeAOT sale vacio (comprobado en la prueba nueva).
- **Codigo/** (cada linea con su «EN LINUX»; mismo numero de lineas y de CRLF que en bae626e):
  - Program.cs:29 (el aviso de la carpeta de datos), Form1.Datos.cs:342 (la carpeta de datos cuando no se puede usar otra) y :362
    (donde se buscan las IA de fabrica): `Application.StartupPath` -> `CarpetaPrograma.Ruta`.
  - BombaSonido.cs:294 (`ArrancaCuandoEsteLista(MethodInvoker arranca)`) y Form1.Biblioteca.cs:1592 (`MethodInvoker finDeCarril`):
    `MethodInvoker` -> `Action`, el mismo delegado sin argumentos (en XP, System.Windows.Forms.MethodInvoker). Quien los llama no cambia
    (Form1.Sonido.cs:1265 pasa un `delegate { ... }`; finDeCarril se llama como antes).
- **Plataforma/Traduccion.cs**: se quita `global using MethodInvoker = Rocola.Ventanas.MethodInvoker;` (antes :8) y su comentario: ya no
  lo usa nadie de Codigo/. Queda `global using Rocola.Ventanas;` (lo usan todavia los grupos de las ventanas; se va en N7).
- **Prueba nueva**: Pruebas/Ventanas/Carpeta (Program.cs, PruebasCarpeta.csproj) y Pruebas/Ventanas/guiones/pruebas_carpeta.sh
  (`W=... pruebas_carpeta.sh ETIQUETA`). Compila el mismo Nativo/CarpetaPrograma.cs del programa y una copia de la cuenta de la capa,
  sacada por el guion de su linea de Plataforma/Ventanas.Form.cs (cuando se borre en N7, de `bae626e:`), y mira en cada caso: la nativa
  igual a la de la capa, igual a la regla de WinForms (GetDirectoryName del ejecutable: el .dll con JIT, el ejecutable con NativeAOT), sin
  barra final, que existe, que es la carpeta donde se puso el programa y, con NativeAOT, que Assembly.Location sale vacio (el motivo).
  Casos: JIT desde la carpeta de compilacion, desde `/` y copiado a una carpeta con espacios y acentos; NativeAOT igual y ademas por un
  enlace simbolico al ejecutable.

## Costuras

- Quitadas: `global using MethodInvoker` (Plataforma/Traduccion.cs). Codigo/ ya no usa Application.StartupPath ni MethodInvoker.
- Que quedan en la capa (no se borran aqui; las borra N7): `Application.StartupPath` (Plataforma/Ventanas.Form.cs:396; Codigo/ ya no la
  usa, pero la usa `Application.ExecutablePath` de :397, que sigue viva en Codigo/Form1.cs:956 y :1112, fuera de este grupo) y el
  delegado `MethodInvoker` (Plataforma/Ventanas.Tipos.cs:63; solo lo usa ya Ventanas.Escritorio.cs:98, dentro de la capa).
- Nuevas: ninguna.

## Comprobaciones (numeros)

- a) `dotnet build -c Release --no-incremental`: 0 errores, 15 avisos, los de siempre (ninguno en los archivos tocados).
- b) diff-xp.py: Program.cs 12 quitadas / 13 puestas (3 sin marca, las mismas de antes: los dos stubs de user32 y la linea de
  PrioridadHilo bajo su «EN LINUX»); Form1.Datos.cs 42/39 (2 sin marca, las de antes); BombaSonido.cs 7/8 (1 sin marca, antes 2: la que
  quedaba era la 294); Form1.Biblioteca.cs 93/90 (2 sin marca, las de antes). Cada linea cambiada lleva su «EN LINUX».
- c) NativeAOT (`dotnet publish ... -p:PublishAot=true`): 0 avisos IL/AOT.
- Inventario (Pruebas/Inventario, contra un `git archive` de bae626e): winforms 1804 -> 1799 (las 3 StartupPath y los 2 MethodInvoker),
  gdi 360 -> 360; Program.cs queda con 0 de winforms. (La herramienta da los mismos 21 errores de compilacion en las dos: no ve Nativo.)
- Prueba nueva (pruebas_carpeta.sh): 39 pasan, 0 fallan (JIT 3 casos x 5, NativeAOT 4 casos x 6); su NativeAOT, 0 avisos IL/AOT (el IL3000
  de leer Assembly.Location va a proposito y apagado en su .csproj).
- pruebas41.sh: 41 de 41. pruebas_hoja.sh: 62 de 62. AOT=1 pruebas_pantalla.sh: 32 de 32 en JIT y 32 de 32 en NativeAOT (su publicacion
  dice «2 lineas con avisos», como antes: el arnes no se toco).
- Escenas alternadas con la referencia (JIT, misma carga; W propio en /root/rocola-trabajo/port/lo-suelto, DATOS0 los de siempre;
  compara.sh con el ruido de ventanas2 y ventanas): arranque IGUAL, cierreq IGUAL, kill9 IGUAL, captura IGUAL, sigterm DIFERENTE la
  aceptada (la referencia no cierra con WindowsShutDown; aqui rocola_estado.txt se borra, como manda XP). Ademas **aviso** (el aviso de
  Program.cs con la carpeta de datos inutil: usa la carpeta del programa por Form1.Datos.cs:342 y la ensena por Program.cs:29): la primera
  vez el reloj Form1.cs:724 se paso de la holgura (92 frente a 69, carga 10); repetida, IGUAL. El texto apuntado en rocola_error.txt
  («Se usara la carpeta del programa: .../corridas/v2h-aviso/prog») es identico en las dos.
- NativeAOT: arranque IGUAL contra la referencia JIT. aviso en NativeAOT: la secuencia, los estados, las capas y la carpeta ensenada
  iguales; solo las cuentas del reloj Form1.cs:724 (116 frente a 77: NativeAOT da mas vueltas que la referencia con JIT; no es de este cambio).
- d) recorridos, e) arneses, banco y cintas: no corridos. Este grupo no toca dibujo, Nativo del dibujo, Puente*.cs ni lo que se sube a
  la tarjeta; los recorridos los corre el coordinador al juntar.

## Lo que queda

- Program.cs sigue con los dos stubs de user32 (SetForegroundWindow, ShowWindow: «otras» del inventario, ya anotados EN LINUX) y
  `p.MainWindowHandle` de System.Diagnostics: no son de la capa.
- Application.ExecutablePath de Form1.cs:956 y :1112 (de otro grupo) y, en N7, lo de la capa citado arriba.

## Riesgos

- Una diferencia que ya tenia la capa y se conserva: si el programa estuviera en la raiz (`/`), TrimEnd deja la cadena vacia, y WinForms
  daria la raiz con su barra (`C:\`). En Rocola OS el programa nunca esta en la raiz.
- Con JIT, la carpeta es la de rocola-xp.dll (no la de `dotnet`), igual que con la capa; con NativeAOT, la del ejecutable (por un enlace
  simbolico, la del ejecutable de verdad, como el ExecutablePath de Windows).
