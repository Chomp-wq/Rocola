# Novena tanda de fallos — Modest, 12 de octubre de 2026

Petición del usuario: «Busca más fallos».

## Lo que se encontró y se arregló

### 1. La moneda se perdía con una ventana de ajustes abierta

La moneda solo se contaba en la ventana principal de la rocola. Si el encargado tenía abierta una ventana de ajustes
y un cliente metía una moneda en ese momento, el pulso del monedero le llegaba a esa ventana y **el dinero del
cliente se perdía**. Pasaba con tres ventanas:

- Ctrl+F3 (sonido, imagen, el local…);
- las carpetas de música;
- la comparación de carga de carátulas.

**Ahora** las tres cuentan la moneda igual que la pantalla principal:

- da sus créditos y se guarda al momento, por si se va la luz;
- un pulso es una moneda: si el pulso se queda apretado, la repetición del teclado no suma de más;
- la tecla de la moneda no le llega a los botones de la ventana. Esas ventanas no tienen dónde escribir, así que no
  le quita nada al encargado.

Quedan sin cubrir los avisos de Windows que salen dentro de la ventana de carpetas («Selecciona una carpeta de la
lista») y la ventana de prueba de F12. Son de servicio y se cierran en un momento.

### 2. Tocar la pantalla no despertaba la atracción (con tarjeta gráfica)

En el modo atracción, los botones la quitan, pero **un toque en la pantalla solo reiniciaba la cuenta del
reposo**: la atracción seguía. En una pantalla táctil no había forma de salir de ella sin los botones.

**Ahora** el toque la despierta, como cualquier botón. Ese toque solo despierta: no abre la ayuda ni nada de lo que
quede debajo del dedo. Sin tarjeta gráfica, tocar la atracción también la quita.

### 3. El menú del reproductor de video

Con el botón derecho del ratón, o dejando el dedo apretado en algunas pantallas táctiles, salía el menú de Windows
Media Player encima del video, con **Pausa, Detener y Pantalla completa** a mano del cliente. Ahora el reproductor va
sin ese menú. Si la versión de Windows Media Player no lo permite, todo sigue como antes.

**No comprobado:** aquí no hay Windows Media Player. La prueba solo confirma que la rocola lo pide.

## Lo que se revisó y estaba bien

- **Una prueba al azar de la rocola entera**, más larga, con vigilancia de estados imposibles: tres vueltas de 5
  minutos, con canciones de 32, 44,1 y 48 kHz, archivos rotos, discos que se borran, videos rotos, monedas, saltos,
  el buscador, la ayuda y la atracción.
  - 0 excepciones.
  - Las pastillas de la cola y de créditos dicen siempre lo mismo que hay de verdad.
  - «Sigue: -» con la cola vacía.
  - Nunca se quedó la cola con canciones y nada sonando.
  - Avisó de dos cosas que no son fallas:
    - la ayuda abierta encima del buscador, que está previsto: la guía sabe ayudar dentro del buscador;
    - la atracción con el buscador abierto, que solo pasaba porque la prueba los abría a la fuerza. En la rocola real
      la atracción solo entra con el buscador cerrado, y el botón que abre el buscador quita antes la atracción.
- **SALTAR con todo vacío borra los créditos.** Es a propósito: es como el encargado cierra el negocio.
- **El buscador** (leyendo el código): acentos, eñes, apóstrofos y letras raras se comparan bien.

## Cómo se comprobó

- **Prueba automática nueva `novenatanda`:** 14 comprobaciones con tarjeta, 12 sin ella.
  - La moneda con una ventana abierta:
    - da sus créditos;
    - un pulso apretado no suma de más;
    - soltar y volver a pulsar es otra moneda;
    - las demás teclas no dan créditos y siguen siendo de la ventana;
    - las tres ventanas la cuentan.
  - El toque que despierta la atracción y que ese toque no abre nada.
  - El reproductor de video pide ir sin menú.

  Contra el ejecutable anterior falla en las cuatro cosas.
- **Tanda completa (`pruebas/todo.sh`):** 26 problemas, los mismos 26 de siempre (baterías que ya fallaban en este
  Linux); ninguno nuevo. `novenatanda` y `octavatanda` pasan en los dos perfiles.
- **Dibujo por CPU:** los 14 métodos congelados, idénticos.
- **No comprobado:**
  - un monedero de verdad;
  - una pantalla táctil de verdad;
  - Windows Media Player.
