# Más mejoras: el repaso a fondo un 25 % más rápido y el quitasiseo en discos seguidos — Modest, 13 de octubre de 2026

Petición del usuario: «Busca más mejoras».

## 1. El repaso a fondo de la biblioteca, un 25 % más rápido, con los mismos números

El repaso a fondo mide cada canción de principio a fin en segundo plano. En el bar, con miles de canciones y el
equipo de dos núcleos, son horas de trabajo. Medí qué se lleva el tiempo: **más de la mitad era la huella del EQ** (8
filtros por canal y muestra), y otra parte la sonoridad K.

**Qué se cambió** (`AudioControles.cs`, `MeasureFile` en `Form1.Sonido.cs`):

- **La huella del EQ** (`HuellaDelTrozo`) ya no se calcula cuadro a cuadro dentro del bucle de la canción, saltando
  entre las 8 bandas. Ahora cada trozo se pasa entero **banda por banda**, con sus coeficientes y su memoria en
  variables. Cada banda es un filtro aparte y su suma recibe exactamente lo mismo y en el mismo orden (cada cuadro, el
  canal izquierdo antes que el derecho).
- **La sonoridad K y los recortes** (`SonoridadDelTrozo`) salen también del bucle de cada cuadro. Van cuadro a cuadro
  y canal a canal como siempre, porque su suma es una sola para los dos canales, pero con los coeficientes en
  variables.
- La limpieza de restos diminutos cae en los mismos cuadros que antes.

**Medido aquí** (repaso a fondo de una canción de 3 min 50 s, mejor de tres): **de 2,39 a 1,79 s (−25 %)**. La huella
sola, de 5,8 a 3,7 ms por segundo de canción.

Probé también a llevar dos bandas a la vez: ganaba 0,3 ms más, dentro del ruido de la medida. No merecía el código
extra y lo quité.

**Los mismos números, bit a bit:**

- **Contra la versión publicada:** siete archivos (mono y estéreo, con y sin silencios, en los dos modos del
  preanálisis). Dan exactamente el mismo volumen, duración, huella del EQ, corte y recorte, sonoridad integrada y mapa
  de silencios.
- **Batería nueva `repasoligero`** (CPU y GPU): compara las dos funciones con la cuenta de antes, cuadro a cuadro.
  Usa 44,1 y 22,05 kHz; mono, estéreo y 6 canales; trozos de tamaños raros; empieza a mitad de la cuenta de 64 y mete
  silencios largos. Salen las mismas sumas y la misma memoria de los filtros, bit a bit. La versión anterior no tiene
  esas funciones y falla.

En XP el procesador calcula con otra unidad (x87). Ahí el último bit de una medida podría salir distinto, sin efecto
en nada; aquí son idénticos.

## 2. El quitasiseo en un disco seguido (I2)

Cuando suenan seguidas dos pistas del mismo disco (el relevo sin pausa), la segunda viene de la misma grabación y trae
el mismo siseo. Ahora **hereda lo que aprendió la anterior**, igual que ya lo hacían el grave en mono y el modo
noche. Así su intro sale sin siseo desde el principio, en vez de esperar unos 3 s a aprender.

Solo pasa cuando la anterior acabó entera y con el mismo formato, y solo con el interruptor del quitasiseo encendido.

Además, en los trozos de silencio digital el quitasiseo ya no hace la transformada, porque su resultado es cero
exacto. El resultado es el mismo.

**Comprobado en `mejorasia`** (ahora 36 comprobaciones):

- la intro de la segunda pista con herencia baja el siseo más de 3 dB desde el principio;
- sin herencia espera a aprender;
- la cadena guarda el quitasiseo para que la siguiente herede.

## Cómo se comprobó

- **Baterías:** `repasoligero` (nueva, en la tanda para CPU y GPU), `mejorasia`, `desnormales` y `sinpausa`.
- **`audioestereo`:** miraba en el código que el repaso llamara a la cuenta de la huella. Ahora la llama a través de
  `HuellaDelTrozo`, así que la prueba acepta los dos caminos. Lo que importa, que dé los mismos números, lo comprueba
  `repasoligero`.
- **Tanda completa (`pruebas/todo.sh`):** los mismos 26 problemas de siempre, ninguno nuevo. En una pasada anterior,
  con otras pruebas corriendo a la vez, `bolsa`, `busmov` y `pagina` se cayeron. Corridas solas pasan, y en la tanda
  final, sin nada más en marcha, también.
- **Dibujo por CPU:** sin tocar (los 14 métodos, idénticos instrucción por instrucción).
