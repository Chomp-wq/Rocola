# Medir el cuadro de la tarjeta y poner en orden la hoja Performance — Modest, 19 de octubre de 2026

Petición del usuario: buscar optimizaciones en todos los apartados; automáticas si no cambian el aspecto, con interruptor en
Performance si lo cambian un poco; y acomodar la hoja Performance para que quepan más ajustes. Después de empezar, se vio que
la rama ya traía una ronda propia del 16 al 19 de octubre (13 ahorros de Performance, perfiles de aurora, estado extendido opaco,
etc.). Esta ronda **no repite eso**: se quedó solo con lo que no estaba y con lo que resultó mejor.

Resumen corto:
- **Se midió** el cuadro de la tarjeta con una herramienta nueva (`pruebas/inventario.cs`).
- **La hoja Performance se reordenó por temas.** Con 13 ahorros iba en una lista con barra de desplazamiento y las últimas
  filas quedaban debajo del botón Listo. Ahora son cinco hojitas: Cielo, Grano, Aurora, Estrella y Estado.
- **La prueba de la ventana de ajustes** no miraba las hojas Fluidez y Performance de Imagen; ahora sí, y mira cada hojita.
- **No se añadió ningún ahorro nuevo.** Una «aurora más ligera» propia se descartó: la de la rama (`Aurora con menos columnas`)
  ya hace lo mismo y está comprobada bit a bit contra el release anterior.

## La hoja Performance

- **Antes:** una columna de 13 ahorros dentro de un panel con barra de desplazamiento, de 96 píxeles por fila. Sin tocar nada se
  veían unos 5; el resto, desplazando, y la parte de abajo caía bajo el botón Listo.
- **Ahora:** cinco pestañas por tema, hasta cuatro ahorros por hoja (la pantalla del bar es táctil y de botones: nada de
  barras de desplazamiento). Debajo, fijos: lo que va siempre activo y lo medido.

| Hoja | Ahorros |
| --- | --- |
| Cielo | Cielo a media resolución; Fondo en una sola capa; Cielo a un cuarto de resolución |
| Grano | Grano y viñeta en una pasada; Fondo sin grano |
| Aurora | Aurora con menos columnas; Sin reflejo de aurora en reposo; Aurora sin velo inferior; Halo de aurora compacto |
| Estrella | Sin estela de estrella en reposo; Corona de estrella compacta |
| Estado | Estado extendido opaco; Color directo del estado extendido |

- No cambia ningún ajuste, clave, valor de fábrica, dependencia ni texto de gris: solo dónde se ven. El preset Performance, el
  medidor y «Gráficos como de fábrica» siguen como estaban.
- La nota de abajo («De fábrica, apagados…») ya no llega hasta el botón Listo.
- Hay sitio para tres hojas más sin apretar nada.

## Una pantalla que nadie revisaba

`ventanaf3` recorría cada página de Ctrl+F3 y sus hojas, pero las pestañas Efectos, Fluidez y Performance de Imagen no son hijas
directas de la página y nunca se miraban. Por eso nadie vio que Performance se salía de la ventana. Ahora la prueba abre Fluidez
y Performance y cada una de sus hojitas, con la letra de Mono y con una más ancha (DejaVu Sans, parecida a la de Windows XP).
Pasó de 4.203 a 7.289 comprobaciones. Con la lista anterior de 13 ahorros, esa prueba nueva daba 48 problemas.

## Lo que se midió

Con la tarjeta imitada (no es una tarjeta de verdad: cuenta órdenes, triángulos y píxeles, y mide el tiempo de procesador de ESTE
equipo). Herramienta: `pruebas/inventario.cs`, que cualquiera puede repetir.

**Un cuadro del fondo a 1024×768** (400 cuadros, con las nebulosas moviéndose). Se midió antes y después de la ronda de la rama:

| Escena | Antes de la ronda | Con la rama de hoy |
| --- | --- | --- |
| Cielo y estrellas, de fábrica | 0,062 ms | 0,042 ms |
| + luna, de fábrica | 0,119 ms | 0,074 ms |
| + aurora, de fábrica | 0,120 ms | 0,066 ms |
| Memoria reservada por cuadro | 0 bytes | 0 bytes |
| Recolecciones del GC en 400 cuadros | 0 | 0 |

El procesador gasta una fracción de milisegundo por cuadro y no reserva memoria: la ronda de la rama lo bajó casi a la mitad y
no queda nada grande ahí. **El trabajo que queda es de la tarjeta.**

**Qué pinta la tarjeta en un cuadro con luna y aurora** (pantallas completas por cuadro, 1024×768):

| Pieza | De fábrica | Performance |
| --- | --- | --- |
| El cielo y sus capas (degradado, nebulosas, vía láctea, grano y viñeta) | 5,97 | 0,38 (en la imagen intermedia) |
| El estirado de esa imagen a la pantalla | — | 1,00 |
| La aurora (relleno 0,42, neón 0,09, reflejo 0,02) | 0,53 | 0,53 |
| Estrellas, luna y otros | 0,01 | 0,01 |
| **Total** | **6,51** | **1,92** |

En triángulos, la aurora son ≈ 5.800 de los ≈ 6.100 de un cuadro. En una tarjeta que hace las cuentas de vértices con el
procesador eso pesa más que los píxeles; no se pudo medir en una tarjeta de verdad.

**Una cosa que sí salió de medir por separado cada ahorro con aurora** (procesador por cuadro): «Fondo en una sola capa» sube de
0,05 a ≈ 0,2 ms, porque oscurece uno a uno los ≈ 18.000 vértices de la aurora (la viñeta ahora va dentro del cielo). Es ≈ 1 % de
un núcleo a 60 cuadros, y a cambio ahorra la viñeta y el grano a pantalla completa. No se tocó.

**El buscador, con 30.000 canciones:** índice en 109 ms; por tecla, mediana 0,29 ms, la peor 11 ms. Nada que ganar.

## Lo que queda como propuesta

| | Qué haría | Ganancia estimada | Por qué no se hizo |
| --- | --- | --- | --- |
| La aurora dentro de la imagen del cielo | El relleno se pinta dentro de la imagen intermedia, que ya se estira | ≈ −0,2 pantallas por cuadro (de ≈ 1,9), solo con el cielo a un cuarto | Obliga a repintar el cielo cada vez que cambia la aurora (hasta 60 por segundo). Laborioso y arriesgado |
| Estirar solo lo que cambió | Dejar de estirar el cielo entero en cada cuadro | Hasta −1 pantalla por cuadro | Exige que la pantalla conserve su contenido entre cuadros y rehacer por trozos todo lo de encima: un rediseño |
| Medir en el equipo del bar | «Medir este equipo» con la hoja Performance | — | Es lo único que dice cuánto gana cada ahorro allí |

## Cómo se comprobó

- **`ventanaf3`:** 7.289 comprobaciones (la ventana entera, con Fluidez, Performance y sus cinco hojitas, en dos letras).
- **`ajustesvivos` y `performance`:** pasan (97 y 112 comprobaciones).
- **Tanda completa (`pruebas/todo.sh`):** comparada con la misma tanda sobre la rama sin mis cambios. La rama sin tocar falla 35 baterías y con mis cambios 34, y las 34 son las mismas (la rama de hoy ya no deja la suite limpia: lo decía su propio documento). Ninguna falla es nueva; `indice` falló solo en la rama sin tocar.
- **Capturas** de dos hojitas de la ventana con la rama de hoy: se ven completas y sin tapar el botón Listo.

**No probado aquí:** una tarjeta de verdad, la pantalla del bar con las letras de Windows, y Windows XP.
