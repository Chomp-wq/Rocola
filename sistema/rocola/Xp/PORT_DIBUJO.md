# Port nativo de XP: el dibujo (GDI+ → SkiaSharp directo)

Pista del dibujo de la primera tanda del port nativo (decisión y aclaración del dueño del 8-11-2026: «que tradujeras todo el código para
que trabajara nativamente en Linux», «ningún layer de traducción»). Diseño: `PLAN_TRADUCCION_XP_A_LINUX_MODEST_2026-11-07.md` («El port
nativo») y la entrada «Diseño del port nativo» de `AGENTS.md`. Aquí va lo que hizo esta pista, cómo se comprobó (con números), las
herramientas y lo que queda. No se probó nada en el A4 (no se puede desde aquí).

La referencia de todas las comparaciones es la copia congelada `/root/rocola-trabajo/referencia-xp` (commit aef2211, la versión con capas,
compilada; nunca se toca).

## D0. Herramientas (`Pruebas/`, fuera del programa)

`Pruebas/` no entra en el programa (`RocolaXp.csproj` la quita del `Compile`). La carpeta de trabajo, fuera del repositorio, es
`ROCOLA_TRABAJO` (de fábrica `/root/rocola-trabajo/port/dibujo`).

### 1. Banco por función (`Pruebas/BancoDibujo`)

Casos al azar como DATOS (`Casos.cs`, sin tipos de GDI+), hechos con la API que imita a GDI+ (`EjecutaGdi.cs`) y comparados por la huella
(SHA-256) de los bytes de la imagen y por lo que devuelve cada llamada (medidas, recortes, formatos, excepciones), caso por caso:

- `Congelado/` compila el banco con el `Dibujo2D.cs` congelado de la referencia (`-p:Referencia=...` para otra ruta);
- `Nuevo/` lo compila con el `Dibujo2D.cs` de esta copia y las funciones nativas (`Nativo/`), y con `ejecutor nativo` hace los mismos casos
  llamando DIRECTO a las funciones nativas, como lo hará el código de XP traducido (D2).

```
cd sistema/rocola/Xp/Pruebas/BancoDibujo
dotnet build -c Release Congelado ; dotnet build -c Release Nuevo
ROCOLA_LETRAS_XP=$ROCOLA_TRABAJO/letras-xp-falsas dotnet Congelado/bin/Release/net10.0/BancoCongelado.dll casos 3000 semilla 7 salida c.tsv
ROCOLA_LETRAS_XP=... dotnet Nuevo/bin/Release/net10.0/BancoNuevo.dll casos 3000 semilla 7 salida n.tsv [ejecutor nativo]
cmp c.tsv n.tsv          # (vuelca 17,230 carpeta DIR: esas imágenes en PNG; clase texto: solo esa clase)
```

Clases de casos (cada una con el estado del Graphics al azar: los seis modos de suavizado, los seis de letra, copia, los nueve filtros,
los cinco de medio píxel, la calidad, traslaciones —enteras, a medios, cuartos o cualquiera—, escalas, giros y de cero a tres recortes:
rectángulo, ruta, intersección, exclusión, `ResetClip`, reemplazo):

- `texto` y `mide`: `DrawString` y `MeasureString` en todas sus sobrecargas; cadenas con acentos, eñe, emoji (pares sustitutos),
  tabuladores, saltos `\n` y `\r\n`, `&`, `&&`, espacios al principio y al final, guiones `-`, `–`, `—`, espacio ideográfico y palabras
  que no caben; letras de XP y genéricas, una que no existe, tamaños 7,5, 8,25, 9,5, `kh*0,42`, 0,5 y al azar, en las siete unidades,
  con negrita, cursiva y subrayado; formatos `null`, nuevos, el tipográfico COMPARTIDO, el normal compartido, copias del tipográfico y con
  banderas, con las tres alineaciones en los dos sentidos, las seis maneras de recortar y las banderas (NoWrap, LineLimit, NoClip,
  MeasureTrailingSpaces, FitBlackBox) puestas con `=` y con `|=`; cajas en cero, negativas y con decimales. Un tercio de los de `texto` le
  cambian al tipográfico compartido sus alineaciones y banderas por su nombre y lo vuelven a pedir (lo que hacen `EstadoReposoGpu` y
  `Espaciado` en XP);
- `gdi` y `midegdi`: `TextRenderer.DrawText` y `MeasureText` en todas sus sobrecargas, con las banderas al azar (las 17 que entiende
  `DrawTextGdi`), colores con alfa, `Transparent` y `FromArgb(0,255,255,255)`;
- `forma`: rellenos y trazos de rectángulos, elipses, rutas (el rectángulo redondo de XP, arcos, líneas, Bézier, polígonos, porciones,
  figuras cerradas y abiertas, los dos modos de relleno), polígonos con picos (para el límite de esquinas), líneas, arcos, porciones,
  Bézier y regiones (`Exclude`, `Intersect`, `Union`); pinceles de color, lineales (de dos puntos, de caja con modo y con ángulo, escalable
  o no, con `Blend`, `InterpolationColors` y las envolturas), de trazo (elipse, rectángulo o triángulo, con el centro corrido y `Blend`) y de
  textura (envolturas y traslación); plumas de 0 a 14 px, las cuatro uniones, puntas iguales y distintas, guiones (y a medida) y plumas con
  pincel;
- `imagen`: las 15 maneras de `DrawImage`/`DrawImageUnscaled` (punto, caja, trozo de origen entero o decimal, paralelogramo), imágenes de
  1×1 a 96×96 opacas, con alfa, transparentes con manchas o de un color, en los tres formatos, con matriz de color (`Matrix33` o al azar,
  con las dos `SetColorMatrix`), envoltura y clave de color (con un color que sí está en la imagen);
- `puntos`: `LockBits` en PArgb, Argb, 24 bits y Rgb32, de solo leer, solo escribir y los dos, con trozos al azar, lo leído y lo que queda
  al escribir; `GetPixel`, `SetPixel`, `Clone(rect, formato)` y `new Bitmap(imagen, w, h)`;
- `lee` y `guarda`: `Image.FromStream` de JPEG, PNG con y sin alfa, GIF con paleta (con y sin color transparente) y BMP, y basura (lo
  que diría GDI+: medida, `PixelFormat`, `Flags`), y `Save` en PNG (a archivo y a memoria) y en JPEG con su calidad;
- `recorte`: `ClipBounds` y `VisibleClipBounds` tras recortes y transformaciones, y lo que el recorte deja pintar;
- `letra`: `Height`, `GetHeight()`, `GetHeight(g)`, `Size`, `SizeInPoints`, `Unit`, `Style`, `Name` de cada letra, y `new FontFamily(nombre)`
  (que lanza si no está);
- `cadena`: `GraphicsPath.AddString` rellena y trazada;
- `estado`: `Save`/`Restore` anidados con cambios de estado dentro, `Transform` leída y puesta, `ResetTransform` y
  `Matrix.TransformPoints`.

`ROCOLA_LETRAS_XP=$ROCOLA_TRABAJO/letras-xp-falsas` pone en el lugar de las letras de XP unas libres con sus nombres de archivo (Liberation
para Arial, Times y Courier; FreeSans para Trebuchet; Carlito para Segoe UI; DejaVu para Tahoma; Liberation Sans para Microsoft Sans Serif)
para que el banco pase también por las familias instaladas, la negrita y la cursiva inventadas (Microsoft Sans Serif y Tahoma no traen
todas) y, con `tablas_gdi.py`, por las tablas `hdmx` y `VDMX` de la letra de GDI (`micross.ttf` lleva las dos y `tahoma.ttf` solo `hdmx`;
ninguna de las 119 letras de esta máquina las trae). Sin esa variable todo cae en DejaVu, como en la rocola sin el disco de XP.

Comprobado:

- 3 000 casos en 2,6 s; dos corridas dan el mismo archivo byte a byte (determinista);
- ve los errores: `mutantes.sh` compila ocho copias del `Dibujo2D.cs` congelado con un error metido a propósito y cuenta los casos
  distintos de 3 000: límite de esquinas 4 en vez de 10 → 18; sin el medio píxel del suavizado → 159; LockBits redondeando en vez de
  truncar → 70; margen derecho de TextRenderer 1,4 en vez de 1,5 → 64; el tipográfico copiado en vez de compartido → 51; `WrapMode.Clamp`
  que estira el borde en vez de transparente → 314; holgura del texto 0,05 en vez de 0,005 → 5; el alto de la letra de GDI redondeado en
  vez de hacia arriba → 128.

### 2. La traza por línea de XP (`Pruebas/TrazaDibujo`, `Pruebas/Recorridos`)

`TrazaDibujo/prepara.sh` copia la referencia congelada a `$ROCOLA_TRABAJO/ref-instrumentada`, le pone a los 120 métodos públicos de
`Graphics`, `TextRenderer`, `Bitmap` (también `LockBits`), `Image`, `GraphicsPath.AddString` y los constructores de `Font` (solo en su `Dibujo2D.cs`; el código
de XP no se toca) `[CallerFilePath]` y `[CallerLineNumber]` (`instrumenta.py`) y la traza (`TrazaDibujo.cs`), y la compila. Cada llamada
desde el código apunta archivo:línea de XP, la operación, los argumentos (letra, formato —y si es EL tipográfico compartido—, pincel,
pluma, imagen, cajas...) y el estado del Graphics: suavizado, modo de letra, copia, filtro, medio píxel, matriz (identidad, traslación
entera o con decimales, escala, giro), la matriz del HDC, recorte y destino. Se junta en memoria y se escribe en `ROCOLA_TRAZA_GDI`.

`Recorridos/recorridos.sh PROG SALIDA [nombres]` pasa la rocola de PROG por sus pantallas, cada recorrido con una copia nueva de los datos
(`corre.sh`, en una carpeta fija para que las rutas que enseña la rocola sean las mismas), el sonido a tiempo real sin tarjeta (el
complemento `file` de ALSA a una tubería que `ritmo.py` lee al ritmo de su cabecera WAV: con la salida nula una canción se acaba en un
instante y no se llega al estado extendido) y `TRAZA=1`, `CAPAS=1` o `VOLCADO=1`:

- `pared`: la pared, letras, páginas, el disco, pedir, lo que suena, el estado extendido (15 s sin tocar con música), SALTAR, otro disco;
- `pared2`: lo mismo con la estrella en 2D, la luna realista y la del cielo vivo, la interfaz tramada y el cielo profundo;
- `video`: la letra VIDEO, el video y su fin;
- `buscador`: abrirlo, escribir, los resultados, elegir, los atajos;
- `atraccion`: 40 s sin tocar y sin música;
- `ayuda`: el asistente (sus tres caminos) y la guía del rojo mantenido;
- `ctrlf3`: Ctrl+F3 por sus seis pestañas y sus hojitas (Imagen: Efectos, Fluidez, Performance; Fondo: cinco; Pantalla: tres),
  Tab y Espacio, Ctrl+F2 y F12;
- `hojas`: «Ajustes del sonido...» y sus hojas (el local y sus bocinas, voz golpe y retumbe, lo que suena ahora, mejoras con IA, tono del
  local);
- `botones`: el asistente de botones (sin `rocola_teclas.txt`).

Los datos de prueba son los de siempre más un disco «Larga Duracion» con dos canciones de 150 s (una con acentos, eñe y `&` en el nombre)
y portadas en tres carpetas: JPEG opaco de 600×600, PNG con alfa de 300×300 y JPEG de 1600×1000.

`TrazaDibujo/tabla_estado.py` junta las trazas en **`estado_por_linea.tsv`** (copia en `Pruebas/TrazaDibujo/` y en `$ROCOLA_TRABAJO`):
por cada archivo:línea de XP que llama a GDI+, sus operaciones y llamadas, en qué recorridos salió, los estados vistos con sus veces y qué
parte del estado cambia entre llamadas. Resultado: **729 líneas en 36 archivos** (34 de `Codigo/` y 2 de `Plataforma/`; 18 son `LockBits`); en **94** el
estado cambia entre llamadas (son las que la traducción tiene que llevar con un parámetro y no con un valor fijo), por ejemplo
`Form1.Ajustes.cs:296` (`MeasureString` con suavizado, letra, matriz y recorte distintos según quién lo llame), `Form1.Buscador.cs:252-302`,
`AyudaCasillaGpu.cs:80-131`, `EstiloGpu.cs:77-214` y `Form1.Pared.cs:174-177`. Los archivos con más líneas: `Form1.Vistas.cs` 108,
`Form1.Fondo.cs` 64, `SuenaVivoGpu.cs` 46, `PantallaVivaGpu.cs` 43, `Form1.Pared.cs` 42, `BuscadorPortadasGpu.cs` 37.

Lo que NO pasó por la tarjeta en los recorridos (y por eso no está en la tabla): el dibujo por procesador (los `OnPaint` de las vistas, el
`DrawOneKey` del buscador clásico: con tarjeta el teclado lo pinta `BuscadorPortadasGpu`) y unos pocos hornos (estrella 2D de
`EstrellaGpu.cs` sale con `pared2`; `LunaGpu`, `HorneaLuna` y `BakeInterrogacion` dependen de opciones u horas). Los hornos se comprueban
con el banco de vistas (D4).

### 3. Las diferencias conocidas de la referencia frente a GDI+, medidas (`TrazaDibujo/diferencias.py`, `diferencias.txt`)

- **(a) Los mapas Format32bppArgb se guardan premultiplicados.** Con la cuenta de LockBits de la referencia (escribir `c*a/255`, leer
  `min(255, p*255/a)`, las dos truncando), de las 65 536 parejas (alfa, canal) 63 795 no vuelven igual (con alfa 255 sí; con alfa 0 se
  pierde el color). Alfa 1: B0CFFF se lee 000000; alfa 32: B0CFFF se lee AFC7FF. En el halo de `EstrellaGpu.cs` (BakeStar): 37 036 puntos
  con alfa 1-32 y 74 072 canales distintos al leerlos, que con su alfa pesan como mucho 1,02 niveles en la pantalla (en general, como mucho
  1,98). En GDI+ un mapa Format32bppArgb guarda el color tal cual y vuelve exacto. Afecta a los mapas que XP escribe sin premultiplicar
  (`EstrellaGpu`, `LunaGpu`, `CieloVivoGpu`, `PulidoGpu`; no a `CieloNaturalGpu`, la viñeta ni el grano, que son blanco o negro). Sin
  comprobar: cómo redondea GDI+ al pasar un PArgb a Argb en LockBits (la referencia trunca).
- **(b) ClearTypeGridFit sale en gris.** 7 líneas y 4 920 llamadas en los recorridos: `Form1.Ajustes.cs:407, 561, 566, 617` (Ctrl+F3) y
  `Form1.Pared.cs:127, 130, 262`. **(b2)** El texto de GDI con el modo de letra del sistema (`SystemDefault`: en XP, el suavizado que tenga
  Windows en el A4; la referencia supone gris): 5 líneas de `Plataforma` (las etiquetas de Ctrl+F3, 5 272 llamadas); del código de XP, solo
  medidas (`EstiloGpu.cs:66`, `Form1.Video.cs:710`).
- **(c) Las dos puntas de una pluma se juntan en una.** 0 líneas en los recorridos (XP pone las dos puntas iguales).
- **(d) El mínimo de 1 px de la pluma no mira la escala.** 0 llamadas con plumas de menos de 1 px y escala o giro.
- **(e) Sin comprobar si GDI+ corre también medio píxel el texto y las imágenes escaladas.** (e1) `DrawImage` con el medio píxel de
  fábrica: 20 líneas y 3 223 llamadas (las nieblas de `Form1.Fondo.cs`, las portadas de `Form1.Pared.cs:174`, las miniaturas con
  `HighQualityBilinear`...); (e2) `DrawString` con traslación con decimales, escala o giro: 42 líneas y 6 218 llamadas.

La traducción copia la referencia tal cual (para que las comparaciones sigan siendo exactas); cada diferencia se corregirá después en su
función nativa, en un solo sitio, con la lista a la vista del dueño.
