# BUSCAR y AYUDA, centradas juntas — Modest, 14 de octubre de 2026

Petición del usuario: «Haz que la casilla de buscar y ayuda estén bien centradas».

**Lo que pasaba.** Se centraba en la pantalla solo la casilla de BUSCAR, y la de AYUDA iba pegada a su derecha. Las dos
juntas se veían corridas hacia la derecha; a 1920 de ancho, unos 130 píxeles.

**Ahora.** El centro de la pantalla cae en el medio del par: BUSCAR, 10 píxeles de aire y AYUDA.

- **Sin AYUDA** (sin tarjeta gráfica, o donde no cabe), BUSCAR sola queda en el centro, como antes.
- **En pantallas angostas** sigue valiendo lo de siempre: el par nunca pisa las instrucciones y deja al encabezado lo que
  pide. En 1024×768 el par va de 308 a 715, así que ya queda centrado (511,5 de 512).
- **Con video**, las dos casillas ya iban centradas juntas encima del video; eso no cambia.

**Cómo se comprobó.**

- **`filabaja`:**
  - a 1920 de ancho, el par queda centrado con 1 píxel de margen como mucho;
  - sin AYUDA, BUSCAR sola en el centro;
  - en 1024 y en 800, las reglas de siempre (las instrucciones y el encabezado);
  - 185 comprobaciones con tarjeta y 50 sin ella.
- **Siguen pasando:** `entrar`, `quintatanda`, `septimatanda`, `ayudaguia` y `asistentecombo`.
- **Tanda completa (`pruebas/todo.sh`):** los mismos problemas de siempre, fallo por fallo; ninguno nuevo.
- **Dibujo por CPU:** los 14 métodos, idénticos.

**No probado aquí:** cómo se ve en la pantalla del bar y Windows XP.
