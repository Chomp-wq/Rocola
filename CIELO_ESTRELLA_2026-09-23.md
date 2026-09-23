# Cielo nocturno, movimiento suave y estela musical

Corrección compartida por HD y Modest, del 23 de septiembre de 2026.
Solo cambia el dibujo GPU. El renderizador CPU continúa congelado.

> **Actualización del mismo día:** la estrella pasó a ser sólida en 3D y gira; su estela
> deja una copia por cada radio recorrido, y se quitaron los saltitos al moverse. Lo que
> aquí se dice de los ecos por tiempo vale solo con "Estrella en 3D girando (GPU)"
> apagado. Ver [La estrella en 3D](ESTRELLA_3D_2026-09-23.md).

## Aspecto

- El degradado original vuelve a dibujarse con sus tres paradas: cenit oscuro,
  zona azul y horizonte. Se respetan las perillas existentes del cielo.
- La paleta móvil deja de recolorear ese degradado, las nebulosas, el velo
  galáctico y las estrellas pequeñas/fugaces. También se retira el tinte completo
  de portada en las nebulosas; conservan su tratamiento anterior.
- La cortina, contorno, horizonte y suelo de la aurora mantienen el flujo de
  portada/género. Su fase avanza un 46 % más rápido que en la versión anterior
  (0,038–0,076 vueltas/s según la energía). No mueve ni modifica las frecuencias.
- La estrella principal, su halo y polvo orbital mantienen sus colores musicales.
  La interfaz y las carátulas no se tiñen.

## Trayectoria y ecos

Los cambios de frecuencia antes entraban directamente en parte de la velocidad.
Ahora cada eje tiene una velocidad suavizada, con constante de tiempo de 200 ms,
y la posición integra esa curva de forma analítica. La reacción a los ataques
sigue presente, pero no hay saltos instantáneos de velocidad.

La trayectoria reserva el tamaño máximo de la estrella: crecer o pulsar ya no
mueve los márgenes de la órbita a cada golpe. Se mantiene el recorrido amplio
por la parte superior, la influencia del género y la calma al navegar.

Los ecos ya no alternan tres acentos cian/violeta/rosa. Muestrean la paleta actual
de portada/género con una fase musical continua, independiente del índice del
buffer. Cada captura guarda su color, posición y tamaño hasta desvanecerse.
Una paleta monocromática conserva su familia de color; no se inventa un arcoíris.
Se añade luz neutra para conservar las facetas en portadas oscuras.

Se mantienen el límite de 12 ecos, su caducidad de 1,8 s y un único envío agrupado.
El color de los ecos funciona con su interruptor propio aunque se apague el
movimiento de color del espectro.

## Controles y coste

Se reutiliza `colores_fluidos`, sin cambiar las preferencias guardadas. El rótulo
de Ctrl+F3 → Fondo pasa a **Colores del espectro en movimiento (GPU)**.
No se añaden interruptores ni opciones de rendimiento.

Se eliminan las mallas del tinte global: cielo de 96 a 12 vértices, cada capa de
nebulosa de 144 a 6. También desaparece el remuestreo de paleta por estrella de
fondo. Permanecen la tabla reutilizable de 64 colores y la textura de estrella;
no se añaden lecturas de disco, subidas de texturas ni arreglos por cuadro.

## Verificaciones

- Release en HD (.NET 4.8) y Modest (.NET 3.5), sin advertencias.
- Compilación de perfiles GPU y CPU en ambas ramas; no se cambia su renderer CPU.
- Pruebas de trayectoria, impulsos y aceleración a 24/60/144/240 Hz, pasos de
  tiempo variables, límites de pantalla y estabilidad de posición al pulsar.
- Paletas monocromáticas y multicolor, persistencia de Ctrl+F3, conservación del
  alfa, buffers/texturas reutilizados y presentación real con Direct3D 9 HAL.
- HD: 31/31 baterías de `validar-capas.ps1`, más `salida` y `exclusivo`.
- Modest: 30/31 baterías, más `salida`. Continúa la discrepancia preexistente
  de comparación PCM exacta en `eqpreparado` (22050 Hz, mono, muestra 54).
  Se reproduce también compilando una copia limpia del commit `b2d5911`,
  sin ninguna de estas modificaciones. No se relaja la prueba ni se modifica EQ.

Los nuevos ganchos de pruebas de salida de audio se inicializan explícitamente
a null (mismo valor que ya tenían), para que los compiladores antiguos acepten
la compilación estricta. No se cambia el procesamiento ni la salida de audio.

No se certifican FPS con la biblioteca real, percepción musical en el equipo
del usuario ni ejecución en Windows XP. Los ejecutables de validación quedan
en carpetas temporales; no se sustituyen releases, música, modelos ni ajustes.
