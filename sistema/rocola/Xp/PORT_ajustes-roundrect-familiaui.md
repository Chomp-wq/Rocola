# Port nativo de XP: `RoundRect` da un `SKPath` y `FamiliaUi` el nombre de la familia (grupo «ajustes-roundrect-familiaui»)

Grupo «2b. ajustes-roundrect-familiaui» de la oleada 2 (tanda 5) de `PLAN_TANDA4_A_N7.md`, 12-11-2026. Rama
`port-ajustes-roundrect-familiaui`, base `bae626e`. Decisiones del dueño en `AGENTS.md` (8-11-2026: traducir el código de XP para que llame
directo a lo nativo de Linux, sin capas que imiten a Windows). Nada se probó en el A4: todo es fuera de pantalla con llvmpipe.

## Qué se hizo

La lógica de XP no cambia: cambia el tipo que devuelven dos funciones de `Form1.Ajustes.cs` (el `GraphicsPath` y la `FontFamily` de la
capa, `Plataforma/Dibujo2D.cs`) por lo nativo (un `SKPath` y el nombre de la familia), y sus llamadores usan lo nativo directo. Cada línea
cambiada lleva su `// EN LINUX: ...`. Los cinco archivos de `Codigo/` conservan su número de líneas y de CRLF (comprobado antes del commit):

| Archivo | Líneas | Con CRLF | Cambiadas |
|---|---|---|---|
| Form1.Ajustes.cs | 2 044 | 2 011 | 31 |
| Form1.Pared.cs | 1 678 | 1 470 | 14 |
| Form1.cs | 2 785 | 2 741 | 1 (2270) |
| PantallaVivaGpu.cs | 963 | 0 | 2 (376, 569) |
| PrecargaPaginaGpu.cs | 231 | 0 | 1 (64) |

`Codigo/Form1.Ajustes.cs`:

| Línea(s) | Antes | Ahora |
|---|---|---|
| 10 | `using Rocola.Dibujo2D` | fuera (la línea queda con `System.Drawing`, `Rocola.Nativo` y `SkiaSharp`; la nota lo dice). Compila sin él: el archivo ya no nombra nada de la capa |
| 1932-1943 (`RoundRect`) | `GraphicsPath` con 4 `AddArc` y `CloseFigure` | `SKPath` con relleno par-impar (`Alternate`, el de fábrica de `GraphicsPath`), 4 `ArcTo` sobre la caja `(x, y, x + w, y + h)` en `float` (el primero con `forceMoveTo`, los otros unidos con línea al punto anterior, como `GraphicsPath.AddArc` con la figura abierta) y `Close`. Es la misma construcción que hacía la capa (`Dibujo2D.cs:280-284` y `:308`), con las mismas cuentas en `float`; las cotas de `d` (`> Width`, `> Height`, `< 1` → 1) quedan como en XP (no son las de `ContornoRedondo`, que pone 2) |
| 1880, 1882, 1883 (el aviso de siempre, `PintaToastHasta`) | `GraphicsPath gp = RoundRect(...)` y `PuenteDibujo.Ruta(gp)` | `SKPath gp = RoundRect(...)` y `gp`, soltado por el `using` como XP suelta el `GraphicsPath` |
| 363, 364, 369, 370 (`FamiliaUi`) | `FontFamily`; `new FontFamily(pref[i])` en un `try`; `FontFamily.GenericSansSerif` | `string`; `Letras.Familia(pref[i])` (lanza `ArgumentException` si la familia no está instalada, como `new FontFamily` de GDI+ y como lo hacía la capa: `Dibujo2D.cs:62` llamaba a esa misma función), y la de reserva `Letras.SinRemates` (Microsoft Sans Serif, lo que daba `GenericSansSerif` de la capa). Mismo orden, mismo valor guardado una vez |
| 669 | `string ff = FamiliaUi().Name` | `string ff = FamiliaUi()` |
| 396, 399, 403, 404, 486, 488, 490, 492, 549, 551, 554, 555, 611, 614 | 7 `RoundedPath` (el puente de `Puente.fondo.cs`, un `GraphicsPath` dueño del `SKPath` de `ContornoRedondo`) y 10 `PuenteDibujo.Ruta` | 7 `ContornoRedondo` (`Form1.Fondo.cs`, ya traducida) y el `SKPath` directo |
| 309 | nota vieja («la firma de XP [con `StringAlignment`] en `Puente.ventanas2.cs`») | corregida: esa firma ya no existe desde 1b; todos llaman con `Alinea` |

`Codigo/Form1.Pared.cs`: `PincelCtrl` y `PlumaCtrl` vuelven a llamarse `CB` y `COut` como en XP (250, 251 y sus llamadas en 255, 256, 262,
269, 286 y 294): el `CB` de la capa (`Puente.pared.cs`) se borró en 1b y `COut` nunca tuvo puente; se renombró por coherencia con `CB`
(la nota de 251 decía «PlumaCtrl, como PincelCtrl»). 1492, 1495-1497: el contorno de la pared toma el `SKPath` de `RoundRect` directo.
Notas viejas corregidas: 10 (ya no llega `RoundRect` por el puente), 250 y 304 (`SueltaBitmaps`: `Form1.Vistas.cs` ya la llama directo).

`Codigo/Form1.cs:2270` (el aviso sin tarjeta): `RoundRect` da el `SKPath`; la forma de `lblToast` sigue siendo una `Region` de la capa
(`lblToast` es un `Control` de la capa hasta W6), así que pasa por un puente nuevo.

`Codigo/PantallaVivaGpu.cs:376 y 569` y `PrecargaPaginaGpu.cs:64`: `FamiliaUi().Name` → `FamiliaUi()` (ya es el nombre).

## Costuras

- **Añadida**: `RegionDeRuta(SKPath)` en `Plataforma/Puente.ventanas2.cs`. Hace lo que hacía `new Region(GraphicsPath)` de la capa
  (`Dibujo2D.cs:336`): una copia del trazo (`new SKPath(p)`, que copia también el relleno par-impar). Quien llama suelta su `SKPath` con el
  `using`, como XP soltaba el `GraphicsPath`. **Se quita en W6**, cuando el aviso sin tarjeta deje de ser un `Control` de la capa.
- **Quedan muertas** (en archivos compartidos o que no son de este grupo; las borra la limpieza o N7):
  - `RoundedPath(Rectangle, int)` de `Plataforma/Puente.fondo.cs:20`: ya nadie la llama (Form1.Buscador.cs y Form1.Vistas.cs dejaron de
    llamarla en la oleada 1; Form1.Ajustes.cs ahora).
  - `PuenteDibujo.FamiliaPantallaViva(FontFamily)` de `Plataforma/Puente.pantalla-viva.cs:46`: ya no tenía llamadores en la base
    (`Puente.pantalla-viva.cs` es de la oleada 3).
- **Siguen vivas** (comprobado con `[Obsolete(error)]` y una compilación): las tres firmas de XP de `Puente.ventanas2.cs`.
  `AnchoTexto(Graphics, string, Font)` la llama `Puente.pantalla-viva.cs:65`; `AnchoTexto(string, Font)`, `Form1.Vistas.cs:1565`,
  `Puente.pantalla-viva.cs:70` y `Puente.suena.cs:60`; `AnchoToast(string, Font)`, `Form1.cs:2268`. Las notas de `Form1.Ajustes.cs:290,
  320 y 1834` que las nombran siguen siendo ciertas.

## Comprobaciones (12-11-2026)

- a) `dotnet build -c Release`: 0 errores, 15 avisos (los mismos de la base: SYSLIB0054, CA2022, SYSLIB0050, CA2014, CS1690).
- b) `diff-xp.py -v` de los cinco archivos: las mismas líneas sin marca que en la base (Form1.Ajustes.cs 9, Form1.Pared.cs 1, Form1.cs 2,
  las otras 0: comentarios de varias líneas y llaves, ninguna de este grupo). Form1.Ajustes.cs pasa de 385/382 a 403/400 líneas
  quitadas/puestas (las nuevas son las de `RoundRect`, `FamiliaUi` y la 10); los otros cuatro no cambian de cuenta (sus líneas ya eran de
  traducción).
- c) NativeAOT (`dotnet publish -p:PublishAot=true`): 0 avisos IL/AOT (52 s).
- e) Arneses:
  - VistasAjustes (`compara.sh`, incluye el aviso `PintaToast`/`PintaToastHasta`): letras falsas 0 distintas de 545 y DejaVu 0 de 545, sin
    excepciones.
  - VistasPared (`arnes.sh`, incluye el contorno en 5 medidas): IGUALES con letras falsas y con DejaVu (1 715 líneas cada una).
  - El banco no hace falta: no se tocó `Nativo/` ni `Puente.cs`. Las cintas tampoco: no cambia nada del dispositivo ni lo que se le sube.
- f) Escenas alternadas con la referencia (`Rajustesrr`/`ajustesrr`): f3vistas, f3grises, f3teclas, f3raton y f3billar IGUALES. aviso
  salió DIFERENTE la primera vez solo en cuentas de reloj y encargo (Form1.cs:724 en el segundo 7: 21 frente a 13; en total 124 frente a
  103; MedidasBitacora.cs:129: 130 frente a 105) con la máquina cargada (carga media 6,9 en 4 núcleos); repetida una vez
  (`Rajustesrr2`/`ajustesrr2`): IGUAL. `bateria_f3.py` sobre f3vistas: OK, 3 017 comprobaciones en 26 vistas.
- d) Recorridos (los nueve, con el candado, `cmp`, 7 min): 9 de 9 salen con 0. Contra `cmp-ref`/`cmp-ref2`: capas quietas 214 de 217
  y texturas quietas 442 de 487. Las tres capas son de la hoja de Ctrl+F3 del recorrido «hojas» (`c18`, `c19.5` y `c21`,
  `capa4-dibujo2d-29,2-966x764`), capturas tomadas 1 s después de un clic. No son del cambio: el mismo programa da cada vez otra cosa
  (carga media 5 a 7 en 4 núcleos). Repetido «hojas» y «ctrlf3» (`cmp2`): ctrlf3 con todo lo quieto, y en hojas solo `c18` distinta
  (`c19.5` y `c21` con la huella de la referencia); «hojas» otra vez (`cmp3`): `c18` y `c19.5` distintas con huellas nuevas y `c21`
  igual. Las huellas de `c18` en las tres corridas son tres distintas (35270006…, 8f22a8d7…, 46a3875c…), y la corrida de
  directas-buscador-video (`cmp2`, otra rama) también sacó `c18` distinta (c45e76a2…). Las texturas que faltan son las bandas de
  1024x128, 1024x256 y 512x256 que cambian con la hora; una corrida de la base de la oleada 1 a una hora parecida
  (`pantalla-nativa/cmp`, 02:45) dio 448 de 487. Lo que de verdad cambia este grupo en la tarjeta (el contorno de la pared y el aviso)
  sale igual en los arneses.

## Lo que queda y riesgos

- `Form1.Ajustes.cs` sigue usando `PuenteDibujo.Letra(lblToast.Font)` (1863) y `PuenteDibujo.Bitmap(toastBmp)` (1916): son del aviso con
  `lblToast` (W6) y de `GpuToast` (`D3D9Star.cs`), fuera de este grupo.
- Riesgo bajo: `RegionDeRuta` toca el campo interno `Region.P` de la capa; si la capa cambiara su `Region` antes de W6 habría que
  ajustarlo.
- `FamiliaUi` deja de devolver un objeto: si algún archivo aún sin traducir pidiera `FamiliaUi().Name` o una `FontFamily`, no compilaría
  (no hay ninguno: compila).
