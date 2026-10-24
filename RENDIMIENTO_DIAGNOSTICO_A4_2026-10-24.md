# El diagnóstico completo del A4 y lo que se arregló — 24 de octubre de 2026

Rama Modest (`claude/modest-carson-fa2v2n`). El dueño mandó tres archivos de la rocola (AMD A4-6300, Windows XP SP3, 1024x768):

- `rocola_diagnostico_2026-10-24_1644.txt`: el «Diagnóstico completo» de Ctrl+F3, 216 s, 16 escenas.
- `rocola_error.txt`: del 23 de octubre por la tarde al 24 por la tarde.
- `rocola_sonoridad.txt`: para ver si conviene otro nivelado.

Lo que sigue son las medidas del A4 y lo que se hizo con ellas. Los arreglos se probaron en Linux con Mono, con señales y
tarjetas de prueba. **No se midieron todavía en el A4**: eso lo dirá el próximo diagnóstico completo.

## 1. Lo que dicen los números

### La pantalla

Doce de las dieciséis escenas salen «bien»: cuadros a 60 por segundo, con algún cuadro suelto de 33 ms. Lo malo se junta en tres
sitios:

| Dónde | Qué | Lo que dice el diagnóstico |
|---|---|---|
| Entrar y salir de un disco | **23 de los 25 huecos de más de 50 ms** | todos en la fase «interfaz», con la bandera «interfaz_subida»: 32 a 43 ms rehaciendo y subiendo la capa de botones y barras, y 4 texturas subidas en ese cuadro |
| El peor caso navegando | un hueco de **284 ms** | 266 ms en la fase «interfaz», con una recolección de generación 2 en el mismo hueco |
| Entrar y salir de un disco | un hueco de **195 ms** | 134 ms «fuera del dibujo»: ocupado en algo sin marca |

Durante todo el diagnóstico el repaso de la biblioteca estaba activo (48.417 archivos por medir). Ocupó del 70 al 80 % de un
núcleo, porque las escenas del diagnóstico no cuentan como «alguien usándola». En el bar, el repaso se para a los 8 s de tocar un
botón y va en prioridad mínima: no es la causa de los tirones de navegar.

### El sonido

El `rocola_error.txt` no trae **ni un corte de sonido**: no hay ni una vez «corte del hilo de la música (silencio entregado)». Lo
que sí trae, la noche del 23 al 24:

- **Unas cien líneas «[Latencia de audio]»**: el margen bajaba de 1000 a 750 ms y a los pocos segundos volvía a subir, «atasco de
  15 a 20 ms». Esos atascos eran del hilo de la música, que lee trozos de 25 ms: pasarse del 60 % del trozo (15 ms) contaba como
  atasco, y en XP un solo tic del reloj del sistema son 15,6 ms. Pero ese hilo tiene 200 ms de colchón: a la música no le pasaba
  nada.
- **Diez «avisos tardíos de la salida» de 217 a 267 ms**. La salida de XP (WaveOut) la rellena el hilo de la pantalla. Con 750 o
  1000 ms de colchón, un retraso de 250 ms no se oye.
- **Paradas de la pantalla de 500 a 766 ms «en: publicar la biblioteca»**, al arrancar y una a las 21:35. Con un colchón de 500 ms,
  una de esas sí se oiría.

### La memoria

El montón gestionado de la rocola va de 130 a 220 MB, con las 118.000 canciones. Una recolección de generación 2 con eso para
todos los hilos un buen rato. En el diagnóstico hubo tres en 216 s.

## 2. Lo que se arregló

Todo es automático y sin interruptor: no cambia nada de lo que se ve ni de lo que suena.

### Entrar y salir de un disco: sin crear ni destruir texturas

La capa de interfaz cambia de medida al entrar en un disco: navegando es una franja de arriba (1024x118 en la prueba), y dentro de
un disco o en el buscador ocupa casi toda la pantalla (1024x654). Cada cambio:

- soltaba la imagen y reservaba otra (hasta 2,8 MB);
- destruía la textura de la tarjeta y creaba otra de hasta 1024x1024 (4 MB);
- y lo mismo con las piezas de cristal de la lista y con la textura que guarda el fundido de colores al cambiar de letra.

Ahora:

- **La imagen de la otra medida se guarda** (`uiOverlayOtro`, Form1.Video.cs) y se vuelve a usar al volver. Se repinta entera
  igual.
- **Las texturas que se sueltan van a un repuesto** de cuatro (`Repone` / `TomaRepuesto` / `SubeReponiendo`, RespuestaGpu.cs). La
  siguiente que necesita la misma medida en potencias de dos toma una de ahí y solo le sube la imagen encima, con su borde, como en
  una nueva.
- En la prueba de la tarjeta (`buscaligero`), seis idas y vueltas entre las dos medidas: **ninguna textura nueva** (antes, una por
  vuelta), seis tomadas del repuesto, y la imagen exacta cada vez.

Lo que no cambia: el dibujo de la capa (GDI+) sigue rehaciéndose entero en ese cuadro, para que no se vea la pared por los huecos.
Cuánto de los 32 a 43 ms era crear texturas y cuánto dibujar no se sabe: lo dirá el próximo diagnóstico.

### Mientras alguien elige, sin recolecciones grandes

.NET 3.5 deja pedir que no haga recolecciones de generación 2 durante un rato (`GCSettings.LatencyMode = LowLatency`). Las de
generación 0 y 1 siguen.

- Se pide mientras alguien elige: un botón hace menos de 10 s, o una escena del diagnóstico completo, para que el diagnóstico lo
  mida. Fuera de la atracción.
- Al dejar de elegir se vuelve al modo de antes, y la grande, si toca, llega cuando nadie mira.
- Con topes: si se sigue eligiendo más de 5 minutos, o el montón crece más de 128 MB, se vuelve al modo de antes hasta que se deje
  de elegir. Si Windows anda corto de memoria, .NET la hace igual.
- El modo existe desde .NET 2.0 SP1 (el del A4 es 2.0.50727.1433). Si no existiera, no se hace nada.
- Lo revisa el latido de la pantalla (`RevisaRecolector`, Optimizaciones.cs).

**Mono no aplica este modo**: la batería `recolector` mira la lógica y que se pida, no su efecto. El efecto solo se ve en el A4.

### La latencia del sonido, estable

- **El hilo de la música mide su atasco contra su colchón** (más de la mitad, 100 ms), no contra el trozo de 25 ms. Se acaba el
  subir y bajar cada 30 s, y su centenar de líneas por noche.
- **La salida clásica ya no baja de 750 ms** (antes podía llegar a 500). La rellena la pantalla, y las paradas de la pantalla
  llegan a 766 ms: con 500 ms de colchón, una de esas se oiría. Sin los falsos atascos el margen se habría quedado en 500, así que
  las dos cosas van juntas.

### Para el próximo diagnóstico

- **Publicar la biblioteca, paso a paso.** Cada paso marca al vigía de la pantalla con su nombre: «publicar la biblioteca: lo más
  sonado», «…: géneros de los artistas», «…: plan del repaso», etc. Y si entre todos pasan de medio segundo, una línea
  «[Publicar la biblioteca] 766 ms: …» en `rocola_error.txt` con lo de cada paso. Así se sabrá cuál de los once es el lento.
- **Cada tirón dice qué marcó la pantalla en el hueco** («en el hueco la pantalla marcó: «una tecla (…)» 12,3 ms antes del
  cuadro», o «nada»). Así el hueco de 134 ms «fuera del dibujo» dirá qué era.

## 3. El nivelado: lo que trae `rocola_sonoridad.txt`

No alcanza para decidir entre la regla de ahora y la híbrida:

- Trae **229 canciones**, todas del **algoritmo 3**: ninguna del 5, que es el que tiene el P90 que usa el nivelado al límite.
- **221 duran menos de un minuto** (intros, diálogos, comentarios de discos «track by track»). Solo 8 son canciones enteras.
- Las cortas se miden enteras ya en la primera tanda del repaso. Las largas esperan a la segunda tanda (la canción entera), que va
  después de la primera. Y la primera aún tenía 47.899 archivos por medir al acabar el diagnóstico.

Mientras tanto, casi todas las canciones se nivelan con su estimación (`rocola_volumen.txt`) y la regla de antes.

**Para decidirlo con números** hace falta la tabla con el repaso ya avanzado: que muchas canciones enteras tengan medida del
algoritmo 5. Entonces sirve el `rocola_sonoridad.txt`, comprimido en zip (con cien mil canciones son varios megas), y también el
`rocola_volumen.txt`.

## 4. Lo que queda por ver en el A4

- Repetir el diagnóstico completo. Lo mejor, con la biblioteca ya repasada, para separar el efecto del repaso.
- Si «Entrar y salir de discos» baja de sus 23 huecos de más de 50 ms, y cuánto.
- Si el peor caso sigue teniendo huecos de cientos de ms y qué marcó la pantalla en ellos.
- La primera línea «[Publicar la biblioteca]» de `rocola_error.txt`.
- Que no vuelvan las líneas «[Latencia de audio]» cada 30 s, y que no haya cortes.
