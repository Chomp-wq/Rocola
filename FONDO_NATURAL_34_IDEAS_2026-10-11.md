# Espacio natural: las 34 ideas del fondo — 11 de octubre de 2026

Petición del usuario: «Hazlas todas, y todo lo que hagas ahora es para el natural, legacy se queda
como está ahora». Rama Modest. Todo es de tarjeta gráfica y solo actúa con **Fondo → Espacio natural**.
Legacy y el renderizado por CPU no cambian (comprobado por huellas, ver «Cómo se comprobó»).

Presentación con los bocetos: artefacto privado «Ideas para el fondo de la Rocola».

## Interruptores nuevos (Ctrl+F3 → Fondo → hoja «Cielo y aurora», tarjeta «Detalle natural»)

Son efectos visuales nuevos, así que cada uno lleva su interruptor persistente. Los tres vienen
encendidos de fábrica y «Fondo como de fábrica» los vuelve a encender. No cambian Legacy.

| Interruptor | Clave en la configuración | Idea |
| --- | --- | --- |
| Aurora con colores reales (Natural) | `aurora_real=` | 20 |
| Luna con su fase real (Natural) | `luna_fase=` | 33 |
| Cielo según la hora (Natural) | `cielo_hora=` | 34 |

Las demás ideas conservan aspecto y función o son acabado propio de Natural: no llevan interruptor.

## Qué cambia, efecto por efecto

**Cielo** (`CieloNaturalMallaGpu.cs`)
1. Resplandor del aire: franja verdosa muy tenue sobre el horizonte.
2. Degradado sin quiebre: la bóveda Natural es una malla de 10×3 (60 triángulos, una llamada, sin textura)
   con una curva Hermite monótona entre las tres paradas; desaparece la raya del corte.
3. Luz lateral: el costado de la luna (derecha) se aclara un poco hacia abajo.
4. Grano parejo: teja propia de ruido de gradiente entrelazado; mismo alfa y misma mitad de puntos claros
   que la de siempre, sin grumos (desviación por bloques 8×8: 1,22 frente a 3,64). Como la de Legacy, se
   arma repitiendo una pieza (aquí de 256×256) con un solo relleno, no píxel a píxel.
34. Cielo según la hora (interruptor): más hondo de madrugada y un crepúsculo cálido abajo hacia las 19:30
    (y algo al amanecer). Se lee el reloj una vez cada 20 s.

**Nebulosas** (`NebulosaNaturalGpu.cs`, `CieloNaturalMallaGpu.cs`)
5. Color real del gas: el mapa trae polvo azul, oxígeno verde azulado y corazón rosado; la canción lo sigue
   tiñendo con un tinte aclarado (`TinteGasNatural`).
6. Glóbulos y vetas oscuras que recortan la nube por ausencia de luz.
7. Dos distancias: las dos pasadas derivan con recorrido y ritmo distintos.
8. Respiración lentísima del brillo, desfasada entre las dos pasadas.

**Vía Láctea**
9. Núcleo galáctico más ancho y cálido; brazos blanco azulados.
10. Gran grieta de polvo a lo largo de la banda.
11. Grano de estrellas sin resolver, dentro del mismo mapa (coste 0; no se añadió la pasada extra).

**Estrellas** (`EstrellasNaturalesGpu.cs`, `D3D9Star.cs`)
12. Color según brillo: las débiles casi blancas, las brillantes con su temperatura.
13. Centelleo irregular: dos ritmos mezclados (una lectura más de la tabla).
14. Destellos de color en las grandes de la mitad baja.
15. Puntas con un filo azul/naranja en el atlas Natural, y algo más largas en las más brillantes.

**Fugaces**
16. Cabeza verde azulada (dorada en los bólidos) y cola que se apaga en naranja; mismos triángulos.
17. Curva de vida: se enciende, arde y se apaga; el bólido da un fogonazo al final.
18. Rastro azulado de unos 1,6 s tras un bólido (dos triángulos más en la misma llamada).
19. Lluvias de meteoros en sus fechas reales (Cuadrántidas, Líridas, Eta Acuáridas, Perseidas, Oriónidas,
    Leónidas y Gemínidas): parte de las fugaces sale del radiante, más cerca del máximo. No cambia cuántas hay.
    Aparecen repartidas por el cielo, cada una en su recta desde el radiante y con la cola apuntándole.

**Aurora** (`AuroraNaturalGpu.cs`, `D3D9Star.cs`)
20. Colores reales (interruptor): filo rosado abajo, cuerpo verde, arriba rojo y violeta. La música sigue
    mandando en la forma y la intensidad.
21. Pliegues: más luz donde la silueta se inclina y curvatura interior; el pie y la cresta no se mueven, y en
    las seis columnas de cada borde la curva se abre poco a poco (la cortina no se despega del marco).
22. Sin música, parches que laten despacio.
23. Reflejo tenue del color de la cortina en el suelo, que se apaga antes del texto de la franja.

**Estrella protagonista** (`Estrella3dGpu.cs`, `LunaGpu.cs`)
24. Reflejo del entorno: caras de arriba con el azul del cielo, de abajo con el tono de la aurora.
25. Borde de luz (Fresnel) en las caras de canto.
26. Destello en cruz con la casilla de rayos del atlas (también en la estrella plana).
27. Se inclina hacia donde gira.

**Corona, polvo y ecos**
28. Polvo más brillante del lado de la luz y apagado en la sombra.
29. Ecos que envejecen: se abren y pierden color (3D y plana).
30. La corona sube un poco cuando pasa el destello del metal (estrella 3D), suavizado por tiempo: igual a
    24, 50 o 60 cuadros por segundo.

**Profundidad**
31. Al pasar de página, nebulosas (30 %) y Vía Láctea (14 %) se corren menos que las estrellas grandes
    y vuelven despacio a su sitio.
32. Viñeta con la caída de un objetivo (coseno a la cuarta): centro limpio, esquina igual que antes, sin anillo.

**Luna** (`LunaRealGpu.cs`)
33. Luna con su fase real (interruptor), arriba a la derecha: fase por la fecha (fase media, ±1 día),
    mares, cráteres del mapa que ya existía (`MapaLunar`, a 128 px), luz cenicienta tenue, halo según lo
    iluminada que esté; tapa y apaga las estrellas cercanas. Cerca de la luna nueva no se ve, como en el cielo.
    El 11-10-2026 es luna nueva: empezará a verse en unos días.

## Coste

- Sin pasadas nuevas de pantalla completa, sin memoria por cuadro y sin horneados al dibujar.
- Nuevo por cuadro: la malla del cielo (60 triángulos, sustituye a 4), un cuadrilátero y dos halos de la luna,
  hasta dos triángulos del rastro, un cuadrilátero del destello.
- Texturas nuevas: la luna (128×128, se hornea al cambiar de fase, cada 7,4 h) y la teja de grano Natural
  (sustituye a la de siempre mientras Natural está activo).
- Medidas aisladas en Mono (no son FPS de la aplicación ni de la tarjeta del bar):
  malla de aurora Natural 0,061 → 0,065–0,074 ms; campo de 1.400 estrellas Natural 0,091 → 0,12 ms;
  12 ecos 3D Natural 16 µs con caché.

## Cómo se comprobó

- **Pruebas automáticas (Linux/Mono, sin Direct3D):** compilación de los dos perfiles a cero avisos;
  batería nueva `naturalideas` (717 comprobaciones tras la revisión: cuentas puras de las 34 ideas y que cada
  pieza esté enchufada); baterías del fondo existentes. Se actualizaron tres que exigían el aspecto Natural anterior:
  `naturalestelar` (color por brillo), `nebulosanatural` (mapas con color) y `ecosgeometria` (ecos que envejecen).
- **Tanda completa (`pruebas/todo.sh`, Linux/Mono):** 230 baterías bien. Las 28 que fallan también fallan,
  con los mismos fallos, en el código anterior (1049d60) en esta máquina (Mono sin Windows/HAL: texto, GDI,
  tiempos y fixtures de Windows). Ninguna batería falla por estos cambios.
- **Legacy idéntico:** huellas de todos los horneados Legacy (Vía Láctea, nebulosas, viñeta, estrella 2D,
  mapa lunar, grano, atlas, luna CPU), de las funciones de color con Natural apagado y de las mallas de aurora
  Legacy: iguales bit a bit antes y después.
- **No comprobado:** aspecto real en la tarjeta (sin capturas HAL; la batería `espacionaturalhal` solo corre en
  Windows), fluidez real (intervalos p95/p99/máximo con la estrella animada, Legacy y Natural, 2D/3D),
  Windows XP y la tarjeta del bar. Hay que mirarlo en la pantalla real con música.

## Revisión del 11-10-2026 (tarde)

Petición del usuario: «Quiero que revises todo lo que hecho buscando mejoras». Lo corregido en Natural
(Legacy sigue sin cambios):

- **Fallo corregido: la textura de la luna real no se soltaba al apagar la tarjeta o recrear el
  dispositivo** (`StopGpu` soltaba todas las demás). Se habría seguido dibujando con la textura de un
  dispositivo que ya no existe (posible cierre) y lo retenía en memoria. Ahora se suelta con las demás y se
  vuelve a hornear al volver.

- **Bóveda de 10 franjas en vez de 12** (60 triángulos en vez de 72). La sonda que decide si conviene el
  anillo de vértices de la tarjeta (`D3D9Device.Draw`) mide el primer dibujo de 64 triángulos o más del
  cuadro. Con 72, ese primer dibujo era la bóveda (pocos vértices, donde el envío directo gana siempre) y la
  decisión podía quedar mal tomada para las estrellas. Con 10 franjas la curva sigue sin quiebre: frente a 12,
  el color se separa de la curva exacta lo mismo (alrededor de un nivel de 255 con los cortes de Ctrl+F3).
- **Grano**: la teja de 1024 se hacía con un millón de cuentas en el hilo de la interfaz (al arrancar, al
  cambiar a Natural o tras perder la tarjeta). Ahora se calcula una pieza de 256 (16 veces menos) y se
  repite con un relleno, como la de Legacy. Mismo alfa, mitad de claros y sin grumos.
- **Aurora**: con la curvatura de los pliegues, la primera y la última columna se corrían hacia dentro y
  dejaban una rendija sin aurora pegada al borde de la pantalla. Ya no.
- **Lluvias de meteoros**: antes nacían todas a entre 30 y 160 px del radiante (en una pantalla de 768 de
  alto); ahora se reparten por el cielo a lo largo de su recta, como en una lluvia de verdad.
- **Corona**: el suavizado del destello dependía de los cuadros por segundo; ahora va por tiempo.

Cómo se comprobó la revisión (Linux/Mono, sin Direct3D):
- Compilación de los dos perfiles a cero avisos; `naturalideas` 718 comprobaciones (nuevas: la bóveda por
  debajo de la sonda, la teja de grano repetida píxel a píxel, la aurora pegada a los bordes en 40 siluetas,
  la corona igual a 24/30/60 cuadros y la luna soltada en `StopGpu`).
- Huella de Legacy: los horneados, las funciones de color con Natural apagado y las mallas de aurora Legacy
  siguen iguales bit a bit que en 1049d60.
- Renderizado por CPU congelado: `cpucongelado` compara el IL de sus 14 métodos con el de 1049d60: idéntico.
- No comprobado: aspecto real en la tarjeta, fluidez real (p95/p99/máximo) y Windows XP.
