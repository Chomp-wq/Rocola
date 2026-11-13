# Port nativo: n7-adelanto (oleada 9 adelantada: lo de N7 que no depende de 8a)

Parte de la seccion «9a. borrar-capas» de PLAN_TANDA4_A_N7.md, sobre 7113804, a la vez que 8a (form1-sin-form) y 8b
(arneses-sin-capa). No se borra ninguna capa: se deja listo lo que se puede para que el borrado final sea corto. Codigo/ no cambia
(0 lineas).

## Lo hecho

1. **Plataforma/TrazaVentanas.cs** (la traza de las ventanas) y **Plataforma/VolcadoEstado.cs** (el estado de XP en puntos fijos) nombran
   la capa de ventanas en UNA sola funcion cada uno (`DeLaCapa`, marcada «SE QUITA EN N7»), con una sola llamada; lo demas pregunta a lo
   nativo (Hoja.Foco, Hoja.Activa, Hoja.Abiertas, las piezas de las hojas, IPieza, RotuloVista). El texto de la traza y del volcado no
   cambia en un caracter (ver «Por que sale lo mismo» y las escenas).
2. **Pruebas/BancoDibujo**: fuera el ejecutor «puente» y `PruebaPuente.cs` («puente-objetos»). Quedan `gdi` y `nativo`, iguales que antes.
3. **Codigo/Form1.Biblioteca.cs:2731-2757** (los bloques `#else` que no compilan): nada que hacer, ya estaba hecho (ver abajo).
4. **Plataforma/PruebasLinux.cs**: nada que cambiar (ver abajo).
5. La lista del borrado final (al final de este documento).

### TrazaVentanas.cs

`Pieza(object)` (el nombre de una pieza en la traza: su clase y su texto) hacia tres cosas de la capa mezcladas con lo nativo: cambiar el
hueco de una vista por su vista (`Anfitrion.VistaDeHueco`), y `Control c = o as Control` con `c.Text`. Ahora:

- `Pieza` empieza con `string deLaCapa; if (DeLaCapa(ref o, out deLaCapa)) return deLaCapa;` y lo demas es nativo: una pieza de una hoja
  (`IPieza`: `Caja.Text`), una hoja (`Hoja.Text`), un rotulo de la pantalla (`RotuloVista.Text`) o, si no, la clase a secas (una vista).
- `private static bool DeLaCapa(ref object o, out string pieza)`: la costura. Cambia el hueco por su vista (y sigue arriba); si lo que
  queda es un control de la capa (hoy, la ventana de la rocola: `Form1'Rocola'`; y los de las 41 pruebas), lo nombra con su clase y su
  Text, como antes. Conserva el orden de antes: una pieza, una hoja o un rotulo nativos se nombran arriba (el `as Control` iba el ultimo).

### VolcadoEstado.cs

- `foco=` y `activa=`: `Hoja.Foco ?? focoVentana` y `Hoja.Activa ?? activaVentana`, con
  `object focoVentana, activaVentana; DeLaCapa(out focoVentana, out activaVentana);`.
- `private static void DeLaCapa(out object foco, out object activa) { foco = Escritorio.Foco; activa = Escritorio.Activa; }`: la costura.
- Se QUITARON el bucle `foreach (Form h in Escritorio.Formas)` (las ventanas de la capa que no son la rocola, con sus controles) y
  `PiezasPrueba(Control padre, ...)`. No escribian nada: en el programa la unica ventana de la capa es la de la rocola, que ese bucle
  saltaba (`h == this`). Comprobado en 7113804: el unico `new` de una clase que hereda de `Form` es `new Form1()` (Codigo/Program.cs:106);
  `CajaAviso` (Ventanas.Form.cs:495) solo la crea `MessageBox.Muestra` de la capa (Ventanas.Form.cs:489), y a `MessageBox.Show` no lo
  llama nadie en Codigo/, Nativo/ ni Plataforma/ (todos los avisos son `AvisoXp.Muestra`); Ctrl+F3, Ctrl+F2, el historial de caratulas y
  F12 son `Hoja`. Las 41 pruebas (que si crean Form de la capa) no piden el volcado (su principal no es Form1: «sin rocola»).

### Por que sale lo mismo

- Traza: para un hueco, `o` pasa a ser la vista y sigue por lo nativo, como antes; para un control de la capa, `clase + 'Text'` con el
  mismo `Limpio`; para lo nativo, las mismas ramas en el mismo orden. Ningun control de la capa es IPieza, Hoja ni RotuloVista (grep:
  ninguna clase de Plataforma/Ventanas.*.cs los implementa), asi que mirar eso antes o despues del `as Control` da lo mismo; aun asi la
  costura lo mira antes, como el codigo de antes.
- Volcado: las dos lineas son la misma expresion con los valores de la capa pasados por la costura; el bucle quitado no escribia.

### Form1.Biblioteca.cs 2731-2757 (el plan: «2732-2746: los bloques #else, sin compilar»)

Los numeros del plan son de f5a33ca, donde ese `#else` de `DrainCoverTrash` (sin ROCOLA_GPU: no se compila) nombraba
`Rocola.Dibujo2D.Image` en 5 lineas. El grupo cache-portadas (6a1d46c) ya las paso a `SKBitmap` con su nota «EN LINUX» (lineas 2732,
2736, 2740, 2742 y 2746 de hoy), a la vez que la cache (`coverTrash` es `List<SKBitmap>`, Form1.Portadas.cs:28; `UsaPortada(SKBitmap)`,
Form1.Pared.cs:1626; `pastCola.Portadas` es `SKBitmap[]`, Form1.Vistas.cs:1239). `grep -n "Dibujo2D\|PuenteDibujo" Codigo/Form1.Biblioteca.cs`:
0 lineas. No se toco el archivo (mismo numero de lineas y de CRLF: git diff vacio).

### PruebasLinux.cs

El plan dice «se adapta PruebasLinux.cs:54». Esa linea es `Anfitrion.Arranca();` y el archivo no nombra ningun tipo de la capa (inventario
con [Obsolete], /tmp/claude-0/inventario-t4/refs.tsv: 0 usos de Rocola.Ventanas ni de Rocola.Dibujo2D en PruebasLinux.cs, PruebaAot.cs ni
PruebasSonido.cs). Cuando Anfitrion.cs pase a Nativo/ con el mismo nombre de clase, la linea no cambia; solo hay que corregir el
comentario de la linea 25 («sin KMS (Anfitrion.cs)» -> «(Nativo/Anfitrion.cs)»). No se toco.

## Costuras

Anadidas (las dos de la transicion; se quitan en N7, cuando se borre Plataforma/Ventanas.*.cs):

| Costura | Donde | Que hace | Se quita |
|---|---|---|---|
| `TrazaVentanas.DeLaCapa(ref object, out string)` | Plataforma/TrazaVentanas.cs | el hueco de una vista por su vista; un control de la capa, con su clase y su Text | N7: se borran la funcion y su llamada (la primera linea de `Pieza` tras el `null`) |
| `Form1.DeLaCapa(out object foco, out object activa)` | Plataforma/VolcadoEstado.cs | `Escritorio.Foco` y `Escritorio.Activa` | N7: se borran la funcion y su llamada; `foco=` y `activa=` pasan a la pantalla (abajo) |

Quitadas:

- `EjecutaGdi.PorPuente` y el ejecutor «puente» del banco (EjecutaGdi.cs: 6 trozos `#if NATIVO`; Programa.cs), `PruebaPuente.cs` entero y
  `Plataforma/Puente.cs` del proyecto `Nuevo/Nuevo.csproj`.
- VolcadoEstado.cs ya no nombra `Form`, `Control`, `Escritorio.Formas` ni `Control.Controls`.

Quedan sin llamador (no se borran aqui: son de N7 o de quien las use aun):

- **Plataforma/Puente.cs**: con el banco sin «puente», de sus 14 funciones solo tienen llamador en el programa `PuenteDibujo.Lienzo` y
  `PuenteDibujo.Graphics(SKCanvas, SKMatrix)` (Plataforma/Anfitrion.cs:201 y 294: el hueco de una vista) y, dentro de los propios puentes,
  `Estado`, `Lienzo` y `Mapa` (Puente.fondo.cs, Puente.pared.cs). Sin NINGUN llamador en Codigo/, Nativo/, Plataforma/ ni Pruebas/BancoDibujo:
  `Graphics(SKCanvas, EstadoLienzo)`, `Entrega`, `Letra`, `Font`, `Formato`, `StringFormat`, `Pluma`, `Ruta` (las dos) y `Matriz`. Los
  arneses Pruebas/Vistas* que 8b esta cambiando los usan aun por reflexion o por su copia: por eso no se borran.
- Puente.asistente.cs, Puente.buscador.cs, Puente.video-form1.cs y Puente.vistas-a.cs ya estan vacios (solo comentarios). Puente.pared.cs
  (`ExtractCoverPalette(Bitmap)` y `FormatoMapaPared`) no tiene llamador en el programa. Puente.fondo.cs: `Starfield.DrawCached(Graphics...)`
  lo llama Form1.cs:1062 (OnPaintBackground, de 8a); `Draw`, `DrawReactive` y `ColoresDePortada(Bitmap...)`, nadie.

## La firma que hace falta en PantallaRocola (no la anadi: el archivo es de 8a)

Sin la capa, quien tiene el foco en la ventana de la rocola y si es la activa lo lleva la pantalla. Para que el volcado salga IGUAL
(«foco=Form1'Rocola'», «foco=HelpBar», «foco=nadie»; «activa=Form1'Rocola'», «activa=nadie») hacen falta, de solo lectura:

```csharp
// Nativo/PantallaRocola.*.cs
internal static bool HayVentanaNativa { get; }      // la ventana de la rocola es la pantalla (no una de la capa)
internal static string NombreDelFoco { get; }       // "nadie", el nombre de la ventana («Form1'Rocola'»: la clase de la rocola y su titulo) o TrazaVentanas.Pieza(vista)
internal static string NombreDeLaActiva { get; }    // el nombre de la ventana si es la activa (y no esta desechada), o "nadie"
```

Son las tres que 8a ya tiene escritas en su copia de trabajo (Nativo/PantallaRocola.Ventana.cs, sin commit a las 06:00 del 13-11-2026),
con esas firmas. Con ellas, en N7 las dos lineas del volcado quedan:

```csharp
sb.Append("foco=").Append(Hoja.Foco != null ? TrazaVentanas.Pieza(Hoja.Foco) : PantallaRocola.NombreDelFoco).Append('\n');
sb.Append("activa=").Append(Hoja.Activa != null ? TrazaVentanas.Pieza(Hoja.Activa) : PantallaRocola.NombreDeLaActiva).Append('\n');
```

## Al juntar con 8a (los dos tocamos TrazaVentanas.cs y VolcadoEstado.cs)

8a (60ace0b y su copia de trabajo) cambio una linea de cada uno, las mismas zonas que este grupo: va a haber conflicto. Como resolverlo:

- **TrazaVentanas.cs**: se toma esta version, y la linea de 8a (`string n = Anfitrion.NombreDeVentana(o) ?? o.GetType().Name;`: la ventana
  de la rocola en la capa se nombra por la rocola, «Form1») va DENTRO de la costura, en `DeLaCapa`, en lugar de
  `string n = o.GetType().Name, t = null;` (alli `o` es el control de la capa); la de `Pieza` se queda como esta (`o.GetType().Name`).
- **VolcadoEstado.cs**: se toma esta version. La linea de 8a `if (Anfitrion.RocolaDe(h) == this || ...) continue;` desaparece con el bucle
  (la ventana de la rocola en la capa, `Anfitrion.VentanaDeLaRocola`, sigue siendo la unica Form: el bucle sigue sin escribir nada). Lo
  de su copia de trabajo (`bool nativa = PantallaRocola.HayVentanaNativa; ...`) se junta asi mientras haya capa:
  ```csharp
  object focoVentana, activaVentana; DeLaCapa(out focoVentana, out activaVentana);   // COSTURA DE LA CAPA (se quita en N7)
  bool nativa = PantallaRocola.HayVentanaNativa;
  sb.Append("foco=").Append(Hoja.Foco != null || !nativa ? TrazaVentanas.Pieza(Hoja.Foco ?? focoVentana) : PantallaRocola.NombreDelFoco).Append('\n');
  sb.Append("activa=").Append(Hoja.Activa != null || !nativa ? TrazaVentanas.Pieza(Hoja.Activa ?? activaVentana) : PantallaRocola.NombreDeLaActiva).Append('\n');
  ```
- Despues de juntar: las escenas arranque, tab, f2, aviso, f3vistas, f3teclas y f12 (las que miran `foco=`, `activa=` y las hojas).

## Comprobaciones (13-11-2026; maquina compartida, carga 9-14 todo el rato; llvmpipe)

- a) `dotnet build -c Release` (sistema/rocola/Xp): **0 errores, 15 avisos**, los mismos de la base (las 17 lineas con «warning» de la
  base y de esta copia, sin la ruta: `diff` vacio).
- b) diff-xp.py: Codigo/ no cambia (`git diff 7113804 -- sistema/rocola/Xp/Codigo` vacio): nada que revisar; lineas y CRLF de Codigo/,
  los de la base.
- c) NativeAOT (`dotnet publish RocolaXp.csproj -c Release -r linux-x64 -p:PublishAot=true`): **0 avisos IL/AOT** (15 lineas con
  «warning», las de C# de siempre).
- El banco (Pruebas/BancoDibujo), ANTES del cambio (base): semillas 7 y 11 (letras falsas) y 5 (SIN_LETRAS), 3 000 casos, gdi, nativo y
  puente contra el congelado: 0 distintos en los 9; puente-objetos 402 bien, 0 mal. DESPUES: `Nuevo` compila con 0 avisos sin Puente.cs;
  **gdi y nativo, 6 de 6 con 0 distintos** del congelado (18 000 casos), y las seis salidas son **identicas byte a byte** (`cmp`) a las
  de antes del cambio. `ejecutor puente` ahora dice en cada caso «este banco no lleva el ejecutor puente» (antes, lo que no era
  «nativo» ni «puente» corria como «gdi» sin avisar).
- f) pruebas41.sh: **41 de 41**. pruebas_hoja.sh: **86 de 86**. AOT=1 pruebas_pantalla.sh: **32 de 32** en JIT y **32 de 32** en
  NativeAOT (2 lineas con avisos: los CS0649 del arnes, de siempre). PantallaVistas (pruebas_vistas.sh): **194 de 194**.
- Escenas alternadas con la referencia congelada (alterna.sh y compara.sh con RUIDO=/root/rocola-trabajo/port/ventanas2/ruido), JIT:
  - **arranque, buscador, f2, aviso, f3vistas, f3teclas y f12: IGUALES** (traza, volcados, capas a 0 puntos y archivos de datos).
  - **tab**: la secuencia de la traza (508 sucesos) IGUAL las tres veces. Primera y segunda corrida (carga 9 y 14): los volcados
    `estado-fondo.txt` y `estado-shifttab12.txt` distintos SOLO en los rotulos que cuentan lo que el repaso de la biblioteca lleva medido
    en ese momento («Calidad: 0 completas 10 aceptables» frente a «6 aceptables», «faltan 3 archivos» frente a «faltan 7», «33%» frente
    a «0%»: el repaso de fondo iba mas atrasado en una de las dos corridas) y la capa de la hoja por esos mismos rotulos (1 338 y 5 080
    puntos). Tercera (RN7C/N7C, carga 11): **los volcados y las capas IGUALES**; quedan fuera de la holgura solo las cuentas del reloj
    Form1.cs:724 (194 frente a 182) y del encargo MedidasBitacora.cs:129 (228 frente a 213), con signo cambiante de un segundo a otro
    (ruido de carga, lo aceptado en las juntas). Ninguna diferencia en `foco=`, `activa=`, las hojas ni sus piezas en ninguna corrida.
  - La primera pasada de f2 se hizo en la carpeta comun (/root/rocola-trabajo/port/ventanas) y choco con la escena f2 de otro grupo, que
    usa la misma carpeta de corrida (`corridas/v2h-f2`: salio sin rocola_teclas.txt y con la hoja distinta); repetida con
    `W=/root/rocola-trabajo/port/n7-adelanto/ventanas DATOS0=/root/rocola-trabajo/port/ventanas/datos0`: IGUAL. **Ojo para todos: las
    escenas f2* y f3* de dos grupos a la vez en la W comun se pisan** (CORRIDA es un nombre fijo); cada grupo, con su W.
  - NativeAOT (con la fecha de dotnet en rocola-xp) contra la referencia JIT: **f3teclas IGUAL**; f2 con traza (151 sucesos), volcados y
    capas iguales y solo las cuentas del reloj Form1.cs:724 (310 frente a 482) y del encargo MedidasBitacora.cs:129 (314 frente a 496):
    NativeAOT atiende mas ticks que la referencia JIT con la maquina cargada.
- d) Un recorrido («hojas», con el candado, VOLCADO=1 CAPAS=1 HUELLAS=1): ver «Recorrido» abajo.
- Las 29 cintas y los arneses Vistas*: no se tocaron Nativo/, la tarjeta ni ningun Puente*.cs; no hacen falta (los corre la junta).

### Recorrido «hojas» (d; con el candado, VOLCADO=1 CAPAS=1 HUELLAS=1, JIT, con la fecha de dotnet en rocola-xp)

Cuatro corridas de esta copia y, de control, tres de la base 7113804 compilada aparte (misma maquina, carga 7-16), contra cmp-ref y cmp-ref2:

| | texturas quietas | capas quietas (de 37) | las distintas |
|---|---|---|---|
| esta copia, 1.a (rocola-xp con la fecha de hoy) | 22 de 24 | 27 | la hoja de Ctrl+F3 en 10 capturas: la fecha de compilacion del titulo |
| esta copia, 2.a | 24 de 24 | 34 | c18, c19.5, c21 (capa4, la hoja) |
| esta copia, 3.a | 24 de 24 | 36 | c18 |
| esta copia, 4.a | 24 de 24 | 35 | c18, c19.5 |
| base 7113804, 1.a | 24 de 24 | 31 | c18, c19.5, c21, c22.5, c24, c26 |
| base 7113804, 2.a | 23 de 24 | 34 | c18, c19.5, c21 |
| base 7113804, 3.a | 24 de 24 | 36 | c18 |

Las capas de la hoja de Ctrl+F3 que salen distintas (de 1 a 6 por corrida) cambian de una corrida a otra y salen IGUAL DE VARIABLES con
la base sin este cambio: son los rotulos de las hojas de Sonido que cuentan lo que el repaso de la biblioteca lleva medido en ese
momento (lo mismo que en la escena tab), con la maquina cargada; no llegan a las 37 de 37 de la junta ni la base ni esta copia. Este
grupo no toca nada que se pinte (solo el texto de la traza y del volcado). La textura «de B ni en A ni en A2» (128x128) sale igual en
la base. Ojo: sin `touch -r /usr/lib/dotnet/dotnet` en rocola-xp, tambien en JIT, la fecha del titulo cambia 10 capas.

## Lo que queda (de este grupo)

- Nada pendiente de lo pedido. Lo de `foco=`/`activa=` sin la capa depende de las tres propiedades de 8a (arriba).

## Riesgos

- El conflicto con 8a en TrazaVentanas.cs y VolcadoEstado.cs (arriba esta como resolverlo). Si al juntar se deja la linea de 8a en `Pieza`
  en vez de en la costura, tambien sale bien (`NombreDeVentana` devuelve null para lo que no es la ventana de la rocola), pero quedarian
  dos sitios que nombran la capa.
- El bucle quitado del volcado: si alguien volviera a crear una Form de la capa en el programa (no debe: regla 4), sus piezas ya no
  saldrian en el volcado.
- Los tres `banco.sh` (VistasPared, VistasBuscadorPortadas, VistasReposo) ya no piden «puente» ni puente-objetos; los informes viejos
  (PORT_*.md) hablan de «los 9» y «402/407 comprobaciones»: desde ahora son 6 y no hay puente-objetos.
- `BancoNuevo.dll puente-objetos` (un solo argumento) ya no es una orden: corre el banco por defecto (2 000 casos, gdi) a la salida.

## LA LISTA DEL BORRADO FINAL (N7, despues de juntar 8a y 8b)

En este orden; despues de cada paso, `dotnet build -c Release` (0 errores). Los numeros de linea son de 7113804 (8a los mueve: buscar
con el grep que se da). Estado medido en una copia de 7113804 con Dibujo2D.cs y los ocho Puente*.cs borrados: el compilador solo se queja
(en las declaraciones) en Ventanas.Control.cs (19), Ventanas.Escritorio.cs (8), Ventanas.Tipos.cs (3), Ventanas.Form.cs (1),
Anfitrion.cs (1) y Form1.Pared.cs (2: la linea 1502); mas, en cuerpos, Form1.cs:1032 y 1062. En la copia de 8a (06:00 del 13-11) ya solo
nombran la capa Anfitrion.cs, Traduccion.cs y VolcadoEstado.cs (y la capa misma).

**0. Antes de borrar, comprobar que no queda nadie:**

```
cd sistema/rocola/Xp
grep -n "Rocola\.Dibujo2D\|PuenteDibujo\." Codigo/*.cs Nativo/*.cs Plataforma/*.cs | grep -v "^Plataforma/\(Dibujo2D\|Puente\|Ventanas\)" | grep -v "^[^:]*:[0-9]*:\s*//"
grep -n "Escritorio\.\|Rocola\.Ventanas\|: Control\b\|: Form\b\|PaintEventArgs\|KeyEventArgs\|MouseEventArgs\|Cursors\.\|Application\." Codigo/*.cs Nativo/*.cs Plataforma/*.cs | grep -v "^Plataforma/\(Dibujo2D\|Puente\|Ventanas\)" | grep -v "^[^:]*:[0-9]*:\s*//"
grep -rn "Plataforma/\(Dibujo2D\|Puente\|Ventanas\|Anfitrion\|Traduccion\)" --include=*.csproj --include=*.sh --include=*.py Pruebas
```

El primero debe dar solo Anfitrion.cs (y, si 8a no lo quito, Form1.cs:1032/1062 y Form1.Pared.cs:1502); el segundo, solo Anfitrion.cs,
Traduccion.cs:5 y VolcadoEstado.cs (la costura); el tercero, BancoDibujo (Congelado y Nuevo), Ventanas/Carpeta y los control.sh/mutantes.sh
de los arneses (abajo).

**1. Codigo/ (lo que 8a deje; cada linea con su «EN LINUX», mismo numero de lineas y CRLF):**

- Form1.cs: `OnPaintBackground(PaintEventArgs e)` con `Rocola.Dibujo2D.Graphics g = e.Graphics;` (1032) y `formStars.DrawCached(g, w, h, ...)`
  (1062): al lienzo de Skia con el estado del Graphics recien creado (Filtro.Bilineal, medio false, copia false: como
  Form1.Fondo.cs:2384-2397, BgPanel) y la firma nativa `Starfield.DrawCached(SKCanvas, Filtro, bool, bool, int, int, Color, Color)`
  (Form1.Fondo.cs:1752). 8a lo tiene como `AvisosDeLaVentana.PintaFondo` (Action<SKCanvas>).
- Form1.Pared.cs:1502 (AttractView): `Rocola.Dibujo2D.Font font` y su propiedad `Font` (con `Control.DefaultFont`), la letra de la capa
  que la vista guardaba «hasta la oleada 7» (columnas 835 y 869 en 7113804); en la copia de 8a ya no estan.
- Form1.Biblioteca.cs:2731-2757: nada (ya hecho).
- Las notas «EN LINUX» que aun digan «la capa», «el hueco (Plataforma/Anfitrion.cs)», «aun sin traducir» o «Dibujo2D.cs»: es 9b
  (`grep -n "aun sin traducir\|Plataforma/Anfitrion\|Dibujo2D.cs\|de la capa" Codigo/*.cs`).

**2. Plataforma/Anfitrion.cs -> Nativo/Anfitrion.cs** (`git mv`; la clase conserva el nombre: PruebasLinux.cs:54 no cambia), sin la capa:

- fuera `using Rocola.Dibujo2D;` (19) y todo lo que nombra Rocola.Ventanas (26 lineas en 7113804: `grep -n "Escritorio\.\|Control\b\|Form\b\|Cursors\." Plataforma/Anfitrion.cs`):
  `VentanasDeLaCapa` (la implementacion de `PantallaRocola.ILaCapa`), `HuecoDeVista` (282), `HuecoDelVideo` (325), `VistaDeHueco` (125),
  las capas de `Escritorio.Capas` en `Capas()` (76-120: quedan `PantallaRocola.CapasDeLaVentana(r)` y las hojas de `Hoja.Abiertas`, una
  vez por cuadro, con las de detras primero), `compositor.ConMano = Escritorio.CursorActual == Cursors.Hand` (la mano: de la hoja,
  Hoja.Entrada.cs «LA MANO»), `PantallaRocola.LaCapa = new VentanasDeLaCapa()` y, de 8a, `VentanaDeLaRocola`, `NombreDeVentana`,
  `RocolaDe` y `PantallaRocola.AbreEnLaCapa` (pasa a `PantallaRocola.AbreNativa`).
- Lo que ponia el constructor de Ventanas.Escritorio.cs (46-66) lo tiene que poner lo nativo: `PantallaRocola.PintaUno`, `HaySucio` y
  `ProximoPintado` (-> `PantallaRocola.PintadoSinLaCapa()`), `LaDeDelante`, `PonDelante`, `PonEncima`, `PonFoco`, y los ocho ganchos de
  Hoja (`ActivaDeLaCapa`, `CedeLaCapa`, `QuitaFocoALaCapa`, `ActivaLaDeLaCapa`, `VivaEnLaCapa`, `SitioEnLaCapa`, `HayQueComponer`,
  `ActivaLaDeArribaDeLaCapa`): 8a los pone en `PantallaRocola.AbreNativa` (PantallaRocola.Ventana.cs). Comprobar con
  `grep -n "Hoja\.[A-Za-z]*Capa\s*=\|Hoja\.HayQueComponer\s*=\|PantallaRocola\.\(PintaUno\|HaySucio\|ProximoPintado\|LaDeDelante\|PonDelante\|PonEncima\|PonFoco\)\s*=" Nativo/*.cs`
  (deben salir los 15).
- Corregir el comentario de PruebasLinux.cs:25 («(Anfitrion.cs)»).

**3. Los ganchos de la transicion en Nativo/** (el plan: «Hoja.cs:137-145 con Hoja.Foco.cs:50»):

- Nativo/Hoja.cs:138-146 (los ocho `...DeLaCapa`) y sus usos (Hoja.cs 17 lineas, Hoja.Foco.cs:50, Hoja.Entrada.cs 2): sin la capa «la
  ventana de debajo» es la de la rocola en la pantalla; o se renombran (ya no son «de la capa») o se cambian por llamadas directas a
  PantallaRocola. `grep -n "LaCapa\b\|DeLaCapa\b\|ALaCapa\b\|EnLaCapa\b" Nativo/*.cs Codigo/*.cs`.
- `PantallaRocola.ILaCapa`, `LaCapa` y cada `ILaCapa c = LaCapa; if (c != null ...)` (PantallaRocola.Vistas.cs: 29 lineas;
  PantallaRocola.Rocola.cs: `AlApagar` -> `PantallaRocola.FinDeSesion()` de 8a; IVista.cs:1; Codigo/D3D9Star.cs:1 -`PantallaRocola.VentanaMinimizada`-).
- `PantallaRocola.VentanaDe(object)` (de 8a: «la rocola: su ventana, de la capa mientras dure») queda en la identidad.

**4. Plataforma/TrazaVentanas.cs y VolcadoEstado.cs:** borrar las dos `DeLaCapa` y sus llamadas; `foco=` y `activa=` como en «La firma
que hace falta». Si 8a puso `Anfitrion.NombreDeVentana` en la traza, se va con la costura (el nombre de la ventana lo da
`PantallaRocola.NombreDelFoco`/`NombreDeLaVentana`). Los dos archivos se pueden pasar a Nativo/ o quedarse (son ganchos de prueba, no
imitan nada); actualizar entonces Pruebas/Ventanas/LEEME.md:14-15.

**5. Borrar de Plataforma/** (ya sin referencias): `Ventanas.Control.cs` (1 287 lineas), `Ventanas.Escritorio.cs` (830),
`Ventanas.Form.cs` (599), `Ventanas.Tipos.cs` (108), `Traduccion.cs` (5: el `global using Rocola.Ventanas;`), `Dibujo2D.cs` (776),
`Puente.cs` (88), `Puente.asistente.cs` (26), `Puente.buscador.cs` (16), `Puente.fondo.cs` (49), `Puente.pared.cs` (41),
`Puente.video-form1.cs` (10) y `Puente.vistas-a.cs` (10). `VideoEnVentanas.cs` ya no existe. Las Ventanas.*.cs usan Dibujo2D (31 errores
si se borra antes Dibujo2D): se borran a la vez o primero las ventanas. Se quedan en Plataforma/ (no imitan a Windows): Aot.cs,
Form1.Pantalla.cs, Mp3Xp.cs, NAudioXp.cs (`Rocola.Puente`, el de NAudio: NO es PuenteDibujo), PruebaAot.cs, PruebasLinux.cs,
PruebasSonido.cs, TeclasLinux.cs, UnicodeXp.cs, TrazaVentanas.cs y VolcadoEstado.cs.

**6. Pruebas/:**

- `Pruebas/BancoDibujo/Nuevo/Nuevo.csproj`: compila `../../../Plataforma/Dibujo2D.cs` (linea 23) y `../EjecutaGdi.cs` para el ejecutor
  «gdi» de la copia de trabajo: al borrar Dibujo2D.cs, fuera esas dos y en Programa.cs el ejecutor por defecto de `NATIVO` pasa a
  «nativo» (el «gdi» queda solo en `Congelado`, contra la referencia). `mutantes.sh` no cambia (usa el Dibujo2D.cs de la referencia).
  Los tres `banco.sh` (VistasPared, VistasBuscadorPortadas, VistasReposo): `for e in gdi nativo` -> `for e in nativo`.
- `Pruebas/Ventanas/Pruebas41/` se borra con `guiones/pruebas41.sh` y lo suyo en `guiones/compara_trazas.py` y `guiones/referencia.sh`
  (el plan); `Pruebas/Ventanas/LEEME.md:8-9 y 31`.
- `guiones/pruebas_carpeta.sh` toma la cuenta de `Application.StartupPath` de `Plataforma/Ventanas.Form.cs` y, si ya no esta, la de
  bae626e (lineas 11-12): no hay que tocarlo; solo comprobar que sigue pasando sin el archivo.
- Los `control.sh`/`mutantes.sh` que meten un fallo a proposito en un puente (VistasAsistente: Puente.asistente.cs; VistasBuscador y
  VistasBuscadorPortadas: Puente.buscador.cs; VistasPared: Puente.pared.cs; VistasVideoForm1: Puente.video-form1.cs; VistasVistasA:
  Puente.vistas-a.cs; VistasPantallaViva y VistasVistasB: puentes que ya no existen): quitar esos casos (varios ya «no se aplican»).
- `Pruebas/Inventario`: debe dar 0 gdi y 0 winforms.

**7. RocolaXp.csproj, LEEME.md y la documentacion (9b):** RocolaXp.csproj:7 (Direct3DaGL.cs ya no existe), LEEME.md:162
(LlamadasWindows.cs), PruebaAot.cs:3, los comentarios de Nativo/ que dicen «Dibujo2D los llama mientras dure la traduccion»
(Letras.cs:5, TextoGdi.cs:3) y «la capa de la transicion» (PantallaRocola.Foco.cs, Hoja*.cs), y el InternalsVisibleTo de IVista.cs si
ya no hace falta.

**8. Comprobar (el plan):** compila sin el `global using` y con 0 avisos nuevos en JIT y NativeAOT; Inventario 0/0; banco (nativo, 3
semillas), todos los arneses, las 29 cintas, los 9 recorridos, las 21 escenas, hoja (86 hoy) y pantalla (32), PantallaVistas (194).
