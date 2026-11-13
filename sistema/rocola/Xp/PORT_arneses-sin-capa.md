# Port nativo: arneses sin la capa (grupo arneses-sin-capa, oleada 8b, 13-11-2026)

Seccion «8b. arneses-sin-capa» de `PLAN_TANDA4_A_N7.md`. Lo que se pidio: que los arneses de `Pruebas/` dejen de nombrar los tipos
de la capa de GDI+ (`Rocola.Dibujo2D`) y de la de WinForms (`Rocola.Ventanas`) EN EL LADO DEL PROGRAMA NUEVO (el que compila contra la
traduccion), porque N7 las borra; el lado de la referencia congelada sigue compilando su capa. Cada arnes tiene que dar EXACTAMENTE lo mismo
que antes con las dos letras.

Nada de esto toca `Codigo/`, `Plataforma/`, `Nativo/` ni `AGENTS.md`: solo `Pruebas/` y este documento.

## Como esta hecho: `Pruebas/Comun`

Un arnes es un solo archivo `.cs` que se compila dos veces (`-p:Fuente=` la referencia congelada y `-p:Fuente=` esta copia). Para que
el mismo archivo compile en los dos lados sin nombrar la capa, el dibujo que el arnes necesita va detras de una clase `Pru` (con `Lz`, `Fte`
y `Fmt`) que existe en DOS versiones con la misma forma:

| Archivo | Se compila cuando | Que usa |
|---|---|---|
| `Comun/Modos.cs` | siempre | los modos del estado (`ModoSuave`, `ModoLetra`, `ModoCopia`, `ModoPixel`, `ModoFiltro`, `ModoAlinea`, `ModoCorte`): nombres y valores de GDI+ a proposito, para que las lineas que escriben los arneses (`ToString`) salgan iguales |
| `Comun/PruCapa.cs` | la carpeta `Fuente` NO tiene `Nativo/` (la referencia congelada) | `Rocola.Dibujo2D` (Bitmap, Graphics, Font, StringFormat...) y `Rocola.Ventanas` (PaintEventArgs) |
| `Comun/PruNativo.cs` | la carpeta `Fuente` tiene `Nativo/` (el programa nuevo) | solo SkiaSharp y `Rocola.Nativo` (`Mapas`, `Pixeles`, `Pixel`, `Letra`, `FormatoTexto`, `TextoGdiPlus`, `RecorteLienzo`) |

Cada `.csproj` de arnes decide cual con `EsReferencia` (`!Exists('$(Fuente)/Nativo/Mapas.cs')`) y toma `Comun` de `$(ROCOLA_COMUN)` o de
`../Comun`; los guiones (`arnes.sh`, `compara.sh`, `control.sh`, `aot-corre.sh`, `mutantes.sh`...) exportan `ROCOLA_COMUN` porque
copian el proyecto a otra carpeta.

- `Lz` (lienzo de prueba): un `SKCanvas` con el estado que el programa recibe como parametros (suavizado, modo de letra, copia, filtro,
  medio pixel y la matriz del HDC) y solo lo que los arneses le hacen: `Limpia`, `Rellena` (un rectangulo liso), `Escribe` (DrawString
  liso), `RellenaRuta`/`TrazaRuta`, `Traslada`/`Escala`/`Gira`, `Recorta`/`Intersecta`/`Sella`, `Guarda`/`Restaura`, y como queda
  (`LimitesRecorte`, `Transformados`, `Matriz6`, `Estado()`). NO es una clase con la forma de `Graphics`: no tiene `FromImage`, ni
  propiedades de GDI+, ni `Dispose` de GDI+ (las cuentas de `Save`, `SetClip`, `SellaRecorte`, `Transform` y `ClipBounds` son las de
  `Dibujo2D.Graphics`, copiadas como los arneses las usan, y se comprueban contra la referencia en cada corrida).
- `Fte` (letra) y `Fmt` (formato de texto): una `Letra` y un `FormatoTexto` nativos en el programa nuevo; una `Font` y un `StringFormat` en la
  referencia. `Obj` da el objeto que se le pasa a lo que se llama por reflexion.
- `Pru`: mapas (`NuevoMapa`, `Aprecia`, `DeSk`, `Sk`, `Formato`, `Marcas`, `SinPremultiplicar` = `LockBits(Format32bppArgb)` de la capa o
  `Pixeles.Lee` en lo nuevo, `EscribePuntos` = `LockBits(WriteOnly)` o `Pixeles.Escribe`), `TMapa`/`TLienzo` (los tipos que pide cada lado),
  `Capa` (verdadero solo en la referencia), `Brocha`, `InfoRuta`, `OnPaint`.

Esta clase esta en `Pruebas/`, no en el programa: no imita a Windows para que otro la use, solo le da a los arneses una sola forma de pedir
lo mismo a los dos lados. Se borra con los arneses o cuando la referencia deje de compilarse.

## Arneses hechos (el lado nuevo ya no nombra la capa)

Comprobado con el analizador de Roslyn (`/root/rocola-trabajo/port/arneses-sin-capa/esc`: compila programa + arnes y cuenta, por linea del
arnes, los simbolos de `Rocola.Dibujo2D` y `Rocola.Ventanas`) y con `grep`: 0 usos.

| Arnes | Antes (lineas con la capa) | Despues | Resultado contra el de antes y contra la referencia |
|---|---|---|---|
| `BancoVistas/Hornos.cs` | 22 | 0 | 129 lineas: IGUALES a la referencia y a lo de antes, las dos letras |
| `VistasFrontera/Frontera.cs` | 11 | 0 | 106 lineas y 153 texturas, 0 distintas de la referencia; salida y texturas iguales a las de antes; se quita la puerta «puente» de `compara.sh` |
| `VistasPortadas/Portadas.cs` | 20 | 0 | 478 lineas IGUALES; sin «DIRECTO DISTINTO» ni «NO NATIVO DISTINTO» |
| `VistasAjustes/Vistas.cs` | 44 | 0 | 0 de 545 distintas de la referencia y de lo de antes, las dos letras |
| `VistasFondo/Fondo.cs` | 63 | 0 | 2 763 lineas; ver «Diferencias» |
| `VistasSuena/Suena.cs` | 72 | 0 | 488 lineas IGUALES a la referencia y byte a byte a las de antes, las dos letras (72 llamadas directas, como antes) |
| `VistasBuscador/Buscador.cs` | 79 | 0 | (ver el final de este documento) |
| `VistasBuscadorPortadas/BuscadorPortadas.cs` | 71 | 0 | (ver el final de este documento) |
| `Tarjeta/Reproductor.cs` | 0 (por nombre) | 0 | `tMapa`/`cMapa` quedan nulos en el programa nuevo (solo la referencia tiene el Bitmap de la capa) |

## Diferencias que quedan (todas a proposito)

- `VistasFondo`, linea `portada-soltada`: una portada ya soltada lanza `NullReferenceException` en la capa de la referencia y
  `ArgumentException` en el programa nuevo (`Nativo/MapaVivo.cs`, como GDI+ en XP). Antes el programa nuevo la recibia por el `Bitmap` del
  puente y salia igual que la referencia por casualidad de la capa. Ahora la salida dice solo «lanza» (como ya hace el arnes de portadas) y
  el tipo va a la salida de errores. Cambia la linea en las dos salidas; lo demas, igual.
- `VistasFondo`, linea `inicio` (`traducida=False/True`): la marca de siempre.
- `VistasBuscadorPortadas`: la portada soltada llega nula al programa nuevo (como antes por `Image.Sk` de una `Image` soltada).

## Lo que NO se hizo (queda para otra pasada; el mismo metodo)

Nueve arneses nombran todavia la capa en el lado nuevo (lineas con `Rocola.Dibujo2D`/`Rocola.Ventanas`, medidas con el analizador):
`VistasAcabadoEstilo` 109, `VistasVideoForm1` 90, `VistasVistasA` 70, `VistasVistasB` 83, `VistasPared` 105, `VistasAsistente` 75,
`VistasPantallaViva` 124, `VistasReposo` 156, `VistasAtraccion` 78. Piden, ademas de lo que ya trae `Comun`: pinceles con degradado y de textura
(`LinearGradientBrush`, `PathGradientBrush`, `TextureBrush`), `GraphicsPath` con `AddEllipse`/`AddLine`, `Region`, controles
(`Label`, `Panel`, `PaintEventArgs`, `ContentAlignment`, `Control.Font`), `DrawString` con medidas (`MeasureString`) y `FillEllipse`.
`TrazaDibujo` no tiene lado nuevo: es la instrumentacion de una copia de `Dibujo2D.cs` de la referencia (`prepara.sh`), asi que nombra la
capa por naturaleza y se va con ella.

## Riesgos

- `Lz` repite las cuentas de `Graphics` (`Save`/`Restore`, `SetClip`, `SellaRecorte`, `Transform`, `ClipBounds`) para el programa nuevo: si
  alguien cambia las de la capa, la referencia (congelada) y esta copia dejarian de coincidir; cada corrida de un arnes lo muestra.
- Los guiones que copian el proyecto de un arnes a otra carpeta necesitan `ROCOLA_COMUN` (lo exportan todos los que existian); un guion
  nuevo que compile un arnes tiene que hacer lo mismo.
- `compara.sh` de `VistasAcabadoEstilo` y `arnes.sh` de `VistasVistasB` compilan tambien «la base» de una tanda vieja (`git archive`): esa
  base no tiene `Pruebas/Comun` ni `Nativo/Mapas.cs` con el mismo contenido; con ellos, usar `CUALES="ref nuevo"`.
