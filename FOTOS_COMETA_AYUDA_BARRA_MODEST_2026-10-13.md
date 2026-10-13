# Cuatro arreglos de las fotos — Modest, 13 de octubre de 2026

Peticiones del usuario, con fotos de la pantalla del bar:

1. «Quita esa estrella fugaz»: la de las dos colas, que es el cometa.
2. «Si alguien deja el asistente abierto, después de 15 segundos diga adiós y se cierre».
3. «La barra de estado al extenderse queda un poco despegada del espectro de sonido, y normal también».
4. «Las letras "En esta rocola" en el modo atracción aparecen cortadas».

## 1. El cometa, fuera

- **Ya no cruza el cielo.** Era el cometa de Fondo → Cielo vivo: una cabeza brillante con una cola ancha y blanca y otra
  fina y azul, que pasaba cada 35 a 70 minutos.
- **Su interruptor ya no está.** Cielo vivo queda con seis adornos.
- **La clave vieja se ignora.** Si la configuración trae `cometa=`, no pasa nada y ya no se vuelve a escribir.
- **Las fugaces de siempre siguen**, y también la dorada al poner una canción.

## 2. La ayuda se despide sola

- **A los 15 segundos** sin que nadie toque nada, la estrella hace su gesto de despedida y su globo dice:
  - «¡Adiós!»;
  - «Aquí estaré cuando me necesites: ROJO + ABAJO, o la casilla AYUDA».
- **A los 2,2 segundos** se cierra.
- **Si alguien toca algo** mientras se despide, se queda.
- **Vale también a media guía.** Antes se cerraba sola a los 90 segundos; ahora, a los 15, también si va guiando un paso
  (por ejemplo, si espera una moneda y nadie la mete).

## 3. La raya del horizonte, pegada a la barra

- **El brillo que la despegaba.** El que se ve bajo la raya del horizonte es el reflejo de la aurora. Nacía 15 píxeles
  por debajo de la raya. Entre el brillo de la raya y el reflejo quedaba una franja oscura, y la raya se veía suelta
  encima de la barra. Pasaba igual con la barra normal y con la ampliada.
- **El arreglo.** Ahora el reflejo nace justo bajo la raya.
- **Lo que no cambió.** La raya y la barra ya estaban en la misma fila; eso sigue igual.
- **Hay que mirarlo en el bar.** Lo deduje de la foto y del código; aquí no hay una tarjeta gráfica de verdad para verlo.

## 4. «EN ESTA ROCOLA» entero

- **Lo que pasaba.** El letrero se dibujaba pegado al borde de arriba de la ficha. Con la letra del bar (Trebuchet MS),
  la parte de arriba de las letras caía fuera de la imagen y se perdía.
- **El arreglo.** Ahora se dibuja con margen de sobra en una imagen aparte. Después se buscan sus píxeles de verdad y se
  pega entero, arriba de la ficha. Sale completo con cualquier letra y cualquier tamaño de pantalla.

## Cómo se comprobó

- **`fotos1013`** (nueva; 16 comprobaciones con tarjeta; sin ella solo mira que no haya nada que mirar):
  - la despedida: a los 15 s, la duración de la despedida, el cierre, que se queda si alguien toca algo, y con el reloj
    de Windows dando la vuelta;
  - el globo del adiós;
  - el letrero entero desde la primera fila de la ficha, a cuatro altos de pantalla;
  - el reflejo nace en la raya.
- **`cielovivo`:** el cometa ya no está, ni su interruptor, y su clave se ignora.
- **Siguen pasando:**
  - `asistentecombo`, `asistentevisual`, `guiarojo` y `ayudaguia`;
  - `sextatanda`, `septimatanda` y `reposounido`;
  - `ventanaf3` y `ajustesvivos`.

  `atraccion` sigue con el mismo fallo de siempre.
- **Comprobación visual** de la ficha y del globo del adiós horneados: el letrero sale entero.
- **Tanda completa (`pruebas/todo.sh`):** los mismos problemas de siempre, fallo por fallo. La prueba nueva fallaba en el
  perfil sin tarjeta, donde no existen la ayuda ni la atracción de tarjeta; se ajustó y pasa en los dos perfiles.
- **Dibujo por CPU:** los 14 métodos, idénticos.

**No probado aquí:** la pantalla del bar (la raya y el reflejo con la tarjeta de verdad, el letrero con Trebuchet MS) y
Windows XP.
