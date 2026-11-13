# Port nativo: arneses sin la capa, segunda parte (grupo arneses-sin-capa-2, oleada 8b, 13-11-2026)

Segunda parte de «8b. arneses-sin-capa» de `PLAN_TANDA4_A_N7.md`. La primera (`PORT_arneses-sin-capa.md`, rama `port-arneses-sin-capa`, base
de esta: `d069fda`) hizo `Pruebas/Comun` y convirtio 8 arneses; quedaban 9, que son los de aqui. Lo pedido: que en EL LADO DEL PROGRAMA NUEVO
(el que compila contra la traduccion) no nombren ningun tipo de la capa de GDI+ (`Rocola.Dibujo2D`) ni de la de WinForms (`Rocola.Ventanas`),
porque N7 las borra; el lado de la referencia congelada sigue compilando su capa. Y que cada arnes de EXACTAMENTE lo mismo que antes.

Nada de esto toca `Codigo/`, `Plataforma/`, `Nativo/`, `AGENTS.md` ni la documentacion general: solo `Pruebas/` (los 9 arneses, dos de sus
guiones y ANADIDOS a `Pruebas/Comun`) y este documento. `git diff d069fda` fuera de `Pruebas/` y de este archivo esta vacio.

## Lo hecho

| Arnes | Lineas con simbolos de la capa (analizador) antes -> despues | Lineas de salida | Contra «antes» (referencia y nuevo, letras falsas y DejaVu) | Contra la referencia |
|---|---|---|---|---|
| `VistasAcabadoEstilo/Vistas.cs` | 109 -> 0 | 6 607 | 4 de 4 archivos iguales byte a byte | 0 distintas de 6 603 (sin las 4 `il-*`, de siempre), las dos letras |
| `VistasVideoForm1/VideoForm1.cs` | 90 -> 0 | 735 | 4 de 4 iguales | IGUALES, las dos letras |
| `VistasVistasA/VistasA.cs` | 70 -> 0 | 1 266 | 4 de 4 iguales | IGUALES, las dos letras |
| `VistasVistasB/VistasB.cs` | 83 -> 0 | 3 108 | 4 de 4 iguales | 0 distintas de 3 108, las dos letras |
| `VistasPared/Pared.cs` | 105 -> 0 | 1 719 | 4 de 4 iguales | IGUALES, las dos letras |
| `VistasAsistente/Asistente.cs` | 75 -> 0 | 2 043 | 4 de 4 iguales | IGUALES, las dos letras |
| `VistasPantallaViva/PantallaViva.cs` | 124 -> 0 | 768 | 4 de 4 iguales | IGUALES, las dos letras |
| `VistasReposo/Reposo.cs` | 156 -> 0 | 1 791 | 4 de 4 iguales | IGUALES, las dos letras |
| `VistasAtraccion/Atraccion.cs` | 78 -> 0 | 19 120 | 4 de 4 iguales | IGUALES, las dos letras |

En total 890 lineas con simbolos de la capa -> 0, y 36 de 36 archivos de salida iguales byte a byte a los de antes del cambio.

`TrazaDibujo` no se toca: no tiene lado nuevo. Es la instrumentacion de una copia de `Dibujo2D.cs` DE LA REFERENCIA (`prepara.sh` copia la
referencia congelada, le pone `[CallerFilePath]` a su capa y compila esa copia); nombra la capa por naturaleza y vale mientras exista la
referencia. No compila contra el programa nuevo ni lo hara.

### Como se convirtio cada uno (lo mismo que en la primera parte)

- El lienzo de prueba es `Lz` (un `SKCanvas` con el estado que el programa recibe en argumentos; en la referencia, su `Graphics`), los mapas
  son `object` (`Pru.NuevoMapa`: el `Bitmap` de la referencia o un `SKBitmap` con el nombre de su formato apuntado), las letras `Fte` (la
  `Font` o la `Letra`), los formatos `Fmt` (el `StringFormat` o el MISMO `FormatoTexto`) y los modos, los de `Comun/Modos.cs` (mismos nombres
  y numeros que GDI+: las lineas que los escriben salen iguales).
- Donde el arnes llamaba a la firma de XP con un `Graphics` (la referencia) o a la traducida con «el lienzo y el estado del Graphics» sacados
  por `PuenteDibujo` (el programa nuevo), ahora pregunta `Pru.Capa`: en la referencia busca el metodo por `Pru.TLienzo`/`Pru.TMapa` y le da
  `g.Objeto`; en el programa nuevo le da `g.Estado()` (lienzo, suave, letra, copia, filtro, medio). Ya no se llama a `PuenteDibujo.Estado`,
  `Lienzo`, `Letra`, `Formato`, `Mapa`, `Graphics`, `Entrega`, `Font` ni `FormatoMapaPared`, ni a `Brush.Pon`, `Font.L`, `Image.Sk`,
  `GraphicsPath.P` o `Graphics.C` por reflexion: 0 usos de `PuenteDibujo` en los 9.
- Lo que era de WinForms: los `Label` de la referencia salen de `Pru.Rotulo()` (el programa nuevo usa su `RotuloVista`, por reflexion, como
  ya hacia); `ContentAlignment` y `TextFormatFlags` van por su numero (los mismos de `AlineaTexto` y `BanderasGdi`); el `OnPaint(PaintEventArgs)`
  de la referencia, por `Pru.OnPaint`; `Control.Font` se pone por la propiedad `Font` si la vista la tiene (en la referencia todas son
  `Control`); `EnSeco` recibe en la referencia un `Action<Graphics>` hecho por `Pru.AccionLienzo` y en el programa nuevo su
  `Action<SKCanvas, bool, AjusteLetra>`, y lo que dibuja recibe un lienzo de prueba sobre ese lienzo (`Pru.LienzoSobre`).
- Las portadas de prueba: las que se escribian con `LockBits` van por `Pru.EscribePuntos` (sin premultiplicar) o `Pru.EscribeCrudos`
  (premultiplicadas); las leidas de un JPEG o un PNG, por `Pru.Lee`; a la paleta de una portada se le da el formato nativo
  (`Pru.FormatoNativo`), como hacia el puente que queda en `Puente.pared.cs`.
- Los casos «directo» (la traducida llamada sin la firma de XP) y los de «la firma de XP» son ya la misma llamada en el programa nuevo (no hay
  otra): se conservan las dos lineas de salida, con sus nombres, y la cuenta de «directas» que va a la salida de errores.

## Anadidos a `Pruebas/Comun` (solo lineas nuevas: `git diff --numstat d069fda -- Pruebas/Comun` da 48+0 y 162+0)

Todo va al final de cada clase, marcado «(segunda parte de la oleada 8b)», con la misma forma en `PruCapa.cs` (la capa de la referencia) y en
`PruNativo.cs` (SkiaSharp y `Rocola.Nativo`). No se cambio ni se movio nada de lo que habia.

| Anadido | En la referencia (`PruCapa.cs`) | En el programa nuevo (`PruNativo.cs`) | Lo piden |
|---|---|---|---|
| `Lz.RecortaElipse` | `SetClip` de un `GraphicsPath` con `AddEllipse` | `ClipPath` de un ovalo, sin suavizar, que cambia el recorte de antes | AcabadoEstilo, VistasB, PantallaViva |
| `Lz(SKCanvas, SKMatrix)` / `Lz(Graphics)`, `Pru.LienzoSobre`, `Pru.AccionLienzo` | un `Lz` sobre el `Graphics` que da `EnSeco`; el `Action<Graphics>` | un `Lz` sobre el lienzo del programa con su estado y la matriz del HDC (no se suelta) | VideoForm1 |
| `Lz.Mide`, `LimitesVisibles`, `RellenaOvalo`, `EscribeEn`, `EscribeGdi` | `MeasureString`, `VisibleClipBounds`, `FillEllipse`, `DrawString` en un punto, `TextRenderer.DrawText` | `TextoGdiPlus.Mide`, `RecorteLienzo.Limites`, `Pixel.RellenaOvalo`, `TextoGdiPlus.Escribe`, `TextoGdi.Escribe` con la matriz del HDC | VideoForm1, VistasB, Pared, Atraccion |
| `Lz.MatrizCruda`, `Cuenta` | `Graphics.C.TotalMatrix` y `SaveCount` | los del `SKCanvas` | VistasB |
| `Lz.RellenaDegradado`, `Linea` | `LinearGradientBrush(caja, c1, c2, angulo)` y `DrawLine` | `Degradado.Extremos` + `LinealCon` (repetido) y `Pixel.Linea` | Pared |
| `Lz.RellenaCon` (trazo o rectangulo), `RellenaTextura`, `SinRecorte` | `FillPath`/`FillRectangle` con el `Brush` de XP, `TextureBrush` trasladado, `ResetClip` | `Pixel.Rellena` con el `SKPaint`, `Degradado.Textura`, quitar el recorte | Asistente, Reposo |
| `Fte.Generica` | `new Font(FontFamily.GenericSansSerif, ...)` | `Letra` con `Letras.SinRemates`, sin buscar la familia | AcabadoEstilo, VideoForm1, Pared, PantallaViva |
| `Pru.Rotulo` | `new Label()` | lanza (el programa nuevo tiene `RotuloVista`) | AcabadoEstilo, VideoForm1, Reposo |
| `Pru.FamiliaDeFuente`, `Familia`, `FamiliaGenerica`, `NombreDeFamilia`, `NombreFamilia` | la `FontFamily` | su nombre (`Letras.Familia` lanza `ArgumentException` si no esta, como `new FontFamily`) | VistasB, PantallaViva |
| `Pru.Lee`, `FormatoNativo` | `Image.FromStream`; no hay | `Imagenes.Lee` con el formato que GDI+ le daria; `FormatoMapa` por el nombre del formato | Pared |
| `Pru.Ruta`, `RutaElipse`, `AnadeLinea` | el `SKPath` de un `GraphicsPath`; uno con `AddEllipse`; `AddLine` | el `SKPath`; un ovalo de relleno alterno; `MoveTo` + `LineTo` (figura aparte) | Asistente, PantallaViva |
| `Pru.BrochaDegradado`, `BrochaTextura`, `SueltaBrocha` | `LinearGradientBrush`, `TextureBrush` | un `SKPaint` con el shader de `Degradado.LinealCon` o `Degradado.Textura` (con el medio pixel del lienzo en que se pinta) | Reposo |
| `Pru.EscribeCrudos` | `LockBits(WriteOnly, Format32bppPArgb)` | los puntos tal cual a los del `SKBitmap` | Atraccion |
| `Pru.MedidaDeForm1` y `VentanaDePrueba` | el campo `bounds` de `Control` | `bounds` si Form1 hereda de Form; si no, una ventana de prueba en su campo `ventana` (ver abajo) | AcabadoEstilo |

Las cuentas de lo nativo son las de `Dibujo2D.cs` de esta rama (que es ya una capa fina sobre `Xp/Nativo`), copiadas como cada arnes las usa;
que son las mismas lo dice cada corrida: el programa nuevo sale byte a byte como cuando pasaba por la capa.

## Con y sin la herencia de Form (oleada 8a, a la vez)

El plan avisaba: 8a le quita a `Form1` la herencia de `Form`. Cuatro de estos arneses hacen una `Form1` sin construir
(`GetUninitializedObject`): VistasAcabadoEstilo, VistasVideoForm1, VistasPared y VistasReposo. Solo VistasAcabadoEstilo le leia algo de `Form`
(le ponia el campo `bounds` de `Control`, para `ClientSize`): ahora llama a `Pru.MedidaDeForm1`, que pone `bounds` si existe y, si `Form1` ya
no hereda de nada, le pone en su campo `ventana` una ventana de prueba (`DispatchProxy` sobre la interfaz que tenga ese campo: da esa medida y,
de lo demas, el valor de fabrica de cada tipo). Todo por reflexion; no nombra nada de 8a.

Comprobado contra la rama de 8a tal como estaba (`port-form1-sin-form`, `ec834bb`, exportada a una carpeta de trabajo, solo lectura): los 9
arneses compilan contra ella (0 errores) y su salida del lado nuevo, con las dos letras, es IGUAL byte a byte a la de «antes» (18 de 18
archivos, 0 lineas distintas), VistasAcabadoEstilo incluido (alli `Form1` no tiene `bounds` y entra la ventana de prueba).

## Los guiones

- `VistasAcabadoEstilo/compara.sh` y `VistasVistasB/arnes.sh` compilaban tambien «la base de su tanda» (`b609319` y `ae8b50b`). Esas bases no
  tienen lo nativo que llama `Pruebas/Comun` (probado: 5 errores CS0103, `FormatoMapa`), y sin sus puentes el arnes tampoco sabria llamarlas.
  De fabrica ya solo compilan la referencia y esta copia (`CUALES="ref nuevo"`); `CUALES="ref base nuevo" BASE=<commit>` sigue pudiendose pedir
  con una base que lo tenga, y las lineas «contra la base» solo salen si hay base.
- Los demas guiones de los 9 (`arnes.sh`, `aot-corre.sh`, `control.sh`, `mutantes.sh`, `otros.sh`) ya exportaban `ROCOLA_COMUN` desde la
  primera parte; no se tocaron.

## Comprobaciones (medidas, con esta copia en `468c19f`)

- **Cada arnes antes y despues, byte a byte.** «Antes»: una copia de solo lectura de `d069fda` (`git archive`), los 9 arneses compilados con
  la referencia congelada (`aef2211`) y con esa copia, las dos letras (`ROCOLA_LETRAS_XP` falsas y DejaVu): 36 archivos. «Despues»: lo mismo
  con esta copia. Los 36 iguales (`cmp`). Se repitio con los guiones del repositorio tal cual (`compara.sh` y `arnes.sh` de cada uno, en
  `24ee374`): otra vez 36 de 36 iguales, y cada guion dice IGUALES (o 0 distintas) contra la referencia con las dos letras. Despues de
  `Pru.MedidaDeForm1` se repitio VistasAcabadoEstilo: 4 de 4 iguales.
- **La salida de errores** (no se compara; para saber): las cuentas de «llamadas directas» y «por Pinta» salen como antes (VideoForm1 140,
  Pared 1 612 y 51, Asistente 777, PantallaViva 291, Reposo 768, Atraccion 68 432); en VistasA y VistasB cambian en lo que cambian de una
  corrida a otra, por los reintentos del reloj (VistasA 1 469 -> 1 569 con 1 -> 35 reintentos; VistasB 1 905 -> 1 904).
- **El analizador** de `/root/rocola-trabajo/port/arneses-sin-capa/esc` (Roslyn: compila programa + arnes y cuenta por linea los simbolos de
  `Rocola.Dibujo2D` y `Rocola.Ventanas`), con `Comun/Modos.cs` y `Comun/PruNativo.cs` dentro: antes 109, 90, 70, 83, 105, 75, 124, 156 y 78
  lineas; despues 0 en los 9 y 0 en `PruNativo.cs`. Y `grep -n "Dibujo2D\|Ventanas\|PuenteDibujo"` en los 9 `.cs`: 0 lineas (tampoco en
  comentarios).
- **a) `dotnet build -c Release` en `Xp`**: 0 errores y los 15 avisos de la base (no se toco nada del programa).
- **b) `diff-xp.py`**: no hay archivos de `Codigo/` en este grupo (`git diff --name-only d069fda | grep Codigo`: nada).
- **c) NativeAOT** (`dotnet publish RocolaXp.csproj -c Release -r linux-x64 -p:PublishAot=true`): 0 avisos IL/AOT.
- **e)** los arneses de arriba. No corridos, porque no se toco lo que los pide: el banco por funcion (`BancoDibujo`: ni `Nativo/` ni
  `Puente.cs` cambian), las cintas, los recorridos, las pruebas de ventanas y las escenas.

## Costuras

Ninguna nueva en el programa y ninguna quitada del programa (este grupo no toca `Codigo/`, `Plataforma/` ni `Nativo/`). En `Pruebas/`:

- Queda `Comun/PruCapa.cs`, que nombra la capa A PROPOSITO: solo se compila para la referencia congelada (la carpeta `Fuente` sin
  `Nativo/`). Se borra cuando la referencia deje de compilarse, con los arneses.
- Quedan en los arneses las ramas `Pru.Capa` (la firma de XP de la referencia) y la clase `Pru` entera: se van con la referencia.
- `Pru.MedidaDeForm1` mira primero el campo `bounds` (la herencia de Form): cuando 8a este junto, esa mitad queda muerta en el programa nuevo
  (sigue haciendo falta en `PruCapa.cs`).

## Lo que queda (no era de este grupo, o depende de otros)

- Los guiones de control negativo (`VistasVideoForm1/control.sh`, `VistasVistasA/control.sh`, `VistasVistasB/mutantes.sh`,
  `VistasPared/control.sh`, `VistasAsistente/control.sh`, `VistasPantallaViva/control.sh`) meten fallos con `sed` en `Codigo/` y en
  `Plataforma/Puente.*.cs`; varias de esas lineas ya no existen (se borraron con sus puentes en las tandas 4 a 7) y N7 borra el resto. No se
  tocaron: hay que rehacer sus mutaciones sobre el codigo traducido o retirarlos al borrar las capas.
- `VistasPared/banco.sh` y `VistasReposo/banco.sh` llaman al banco con el ejecutor «puente», que cambia otro grupo (`Pruebas/BancoDibujo`).
- Los comentarios de los `.csproj` de los 9 arneses todavia hablan de «el puente» de su tanda (texto, no codigo).
- `Tarjeta/Reproductor.cs`, `BancoVistas/Hornos.cs` y los 8 arneses de la primera parte usan una `Form1` sin construir en algun caso
  (VistasSuena): si 8a les rompe algo, `Pru.MedidaDeForm1` esta en `Comun` para ellos.

## Riesgos

- `Lz` y los pinceles de `PruNativo.cs` repiten las cuentas de `Dibujo2D.cs` (`SetClip` de un trazo, `LinearGradientBrush`, `TextureBrush`,
  `Pen` de fabrica, `TextRenderer.DrawText`, `MeasureString`). Mientras exista la capa, si alguien cambia una regla en un solo sitio, la
  corrida del arnes lo muestra contra la referencia; despues de N7 solo queda lo nativo y la referencia.
- Los casos «por la firma de XP» y «directo» de la traduccion son ya la misma llamada: el arnes no distingue un puente (no los hay). Si se
  quisiera probar otra via de llamada, hay que escribirla.
- `Pru.MedidaDeForm1` usa `DispatchProxy` (Reflection.Emit): sirve en JIT, que es como corren los arneses, no en NativeAOT. La ventana de
  prueba contesta «falso» y «cero» a todo lo que no es una medida: vale para lo que hoy leen los arneses (`ClientSize`); si un caso nuevo
  necesita otra cosa de la ventana, hay que darsela.
- VistasVistasA y VistasVistasB tienen casos que dependen del reloj (se repiten hasta dar con el instante): con la maquina muy cargada
  tardan mas o dan `SIN-TIEMPO`/`RELOJ:`; en estas corridas, 0 de los dos.
- La comprobacion contra la rama de 8a es contra su estado de ese momento (`ec834bb`, sin terminar): hay que repetirla al juntar.
- Nada de esto se ha visto en el A4 (son pruebas fuera de pantalla).

## Nota de la reanudacion

Encima de `468c19f` esta en esta rama `dc01a8e` («arneses-sin-capa, revision»: `VistasAjustes/Vistas.cs` sin el campo `tPuente`), que no es
de este grupo (lo trajo la revision de la primera parte; 1 linea mas y 2 menos en un arnes de la primera parte). No toca los 9 arneses ni
`Pruebas/Comun`; las comprobaciones de arriba se midieron en `468c19f` y no se repitieron encima. El grep final (0 lineas con `Dibujo2D`,
`Ventanas` o `PuenteDibujo` en los 9 `.cs`) y `git diff --numstat d069fda -- Pruebas/Comun` (48+0 y 162+0) si se repitieron con ella puesta.

## Revision (revisor independiente, 13-11-2026, sobre `69d2071`)

Leido `git diff d069fda..69d2071` (los 9 arneses, sus `.csproj`, los dos guiones y los anadidos a `Pruebas/Comun`) y repetido lo que sigue.
No se encontro ningun fallo en la conversion; no se cambio codigo en esta revision (solo esta seccion).

Lo repetido, en `69d2071` (con `dc01a8e` puesta), carpeta `/root/rocola-trabajo/port/arneses-sin-capa-2/rev`:

- Los 9 arneses, compilados con la referencia congelada y con esta copia (0 errores), con las letras falsas y con DejaVu: los 36 archivos de
  salida son iguales byte a byte a los de «antes» (la base `d069fda`). Contra la referencia: VistasAcabadoEstilo 3 lineas distintas de
  6 607, las tres `il-*` (`il-SubeUi`, `il-RobaUiP`, `il-Ventana`: las de siempre, por la tanda 1), es decir 0 de 6 603; VistasVistasB
  IGUALES (3 108); VideoForm1 (735), VistasA (1 266), Pared (1 719), Asistente (2 043), PantallaViva (768), Reposo (1 791) y Atraccion
  (19 120) IGUALES, las dos letras. Los contadores de llamadas a lo traducido (la salida de errores) son los de antes (VistasB 1 904 y 243
  por Pinta; Pared 1 612 y 51; Reposo 768; Asistente 777; Atraccion 68 432; VideoForm1 140; PantallaViva 291): el lado nuevo sigue
  llamando al programa, no a una copia del arnes.
- `dotnet build -c Release` en `Xp`: 0 errores y los 15 avisos de la base (CA2014 1, CA2022 2, CS1690 2, SYSLIB0050 1, SYSLIB0054 9).
  El programa no cambia (`git diff --name-only d069fda` fuera de `Pruebas/` da solo este documento): diff-xp.py, NativeAOT, banco, cintas,
  recorridos y escenas no aplican y no se corrieron.
- grep de `Dibujo2D`, `Ventanas`, `Puente`, `Graphics`, `Bitmap`, `Font`, `StringFormat`, `GraphicsPath`, `Brush`, `Label`, `Control` en
  los 9 `.cs`: solo en comentarios y en nombres de propiedades o de tipos mirados por reflexion (`"Font"`, `.Name == "Control"`), ninguno
  como tipo. El analizador (`esc-final.txt`, de `468c19f`; los 9 arneses y `Comun` no cambian despues) da 0 lineas en los 9, `Modos.cs` y
  `PruNativo.cs`.
- `Pruebas/Comun`: solo anadidos (48+0 y 162+0). Lo que `PruNativo.cs` copia de la capa se miro contra `Plataforma/Dibujo2D.cs`
  (`TextRenderer.DrawText`: ni texto vacio, ni `Color.Transparent`, ni letra nula; sin suavizar con `SingleBitPerPixel` y
  `SingleBitPerPixelGridFit`; la matriz del HDC): igual. `Pru.MedidaDeForm1` casa con la rama de 8a como esta hoy (`ec834bb`:
  `private IVentanaDeLaRocola ventana` en `Nativo/Form1.Ventana.cs`, `ClientSize` = `ventana.Cliente`, de tipo `Size`).

LO QUE QUEDA PARA EL QUE JUNTA (no se puede arreglar desde esta rama):

- CHOQUE CON LA PRIMERA PARTE. La revision de la primera parte (rama `port-arneses-sin-capa`, hoy en `008c094`) convirtio POR SU CUENTA
  cuatro de los arneses de este grupo (VistasAtraccion `3cae1ba`, VistasAsistente `6d2c113`, VistasPantallaViva `4061f7e` y VistasVistasA
  `d4389d9`) y anadio a `Pruebas/Comun` cosas con los mismos nombres que las de aqui. `git merge-tree` de las dos ramas: conflicto de
  contenido en `Asistente.cs`, `Atraccion.cs`, `PantallaViva.cs` y `VistasA.cs`; y `PruCapa.cs` y `PruNativo.cs` se juntan SIN conflicto
  pero NO compilan (miembros dos veces, CS0111): `Lz.RellenaOvalo`, `Lz.RellenaCon` (las dos), `Pru.Ruta`, `Pru.AnadeLinea`, `Pru.Familia`
  y `Pru.FamiliaGenerica`. Receta: de esos cuatro arneses y de `Pruebas/Comun`, quedarse con una sola de las dos ramas entera. Con la de
  aqui (la que tiene los 9 y las 36 salidas comprobadas arriba) sobran de la otra rama sus anadidos a `Comun` (`RecortaRuta`,
  `Fte.DeFamilia`, `EscribePremultiplicados`, `RutaOvalo`, `RellenaMosaico`, `NombreFamilia(object)` y los repetidos): solo los usan sus
  versiones de esos cuatro arneses. `VistasAjustes/Vistas.cs` es identico en las dos ramas (`dc01a8e` aqui es `32cbcc7` alli): no choca.
  Tras juntar, repetir los cuatro arneses.
- Lo demas de «Lo que queda» y de «Riesgos» sigue igual (los guiones de control negativo sin rehacer, repetir contra 8a al juntar,
  `aot-corre.sh` de los arneses sin correr).
