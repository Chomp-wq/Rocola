# Luna, paleta de caratulas y creditos de servicio — 2026-09-21

Aplicado en HD y Modest. CPU permanece congelado; los cambios de apariencia solo se
dibujan en GPU. No se sustituyeron releases, musica, modelos ni datos instalados.

## Luna

La orbita activa sube progresivamente otro 20% de la altura del panel al expandirse
el estado; su oscilacion vertical en reposo es menor para que no baje tanto hacia
la aurora. Conserva el movimiento horizontal, el retorno suave y los limites de
pantalla. Se respeta el interruptor existente de luna activa, sin crear otro mando.

## Paleta completa de caratula

Ctrl+F3 > Fondo > Paleta completa de caratula (GPU), activada por defecto y
persistente en `paleta_completa=`. El restablecimiento de Fondo la activa.

- Hasta ocho tonos representativos, incluyendo blancos, grises y negros.
- Hasta 1024 muestras de la imagen ya cargada; descarta transparencia y grupos
  de menos del 1% de las muestras opacas. No significa reproducir literalmente
  cada color de cada pixel: conserva una seleccion diversa y acotada.
- Cache junto al promedio y los dos tonos anteriores; comparte invalidacion y no
  hace lecturas de disco ni decodificacion de imagen por cuadro.
- Las columnas de aurora y sus bordes usan la paleta; las nebulosas reciben un
  degradado multicolor mediante los vertices de sus dos cuadrilateros existentes.
- Se respetan los pesos Disco 20%, Mitad y mitad y Disco 100%. Sin genero reconocido
  manda la caratula si su peso es positivo. La excepcion existente BTS/Bangtan con
  genero conserva su morado fijo. El acento de textos y los controles no cambia.
- Al 100% la paleta sustituye los tintes decorativos fijos de aurora y nebulosas;
  se mantiene el modelado de brillo, la mezcla con el cielo y la reaccion musical.
- Transicion temporal suavizada. Arreglos reutilizados: no crea texturas nuevas,
  geometria adicional ni nuevos envios de dibujo al cambiar de portada.
- Sin portada, en video, en atraccion o al desactivar la opcion se retira
  suavemente la contribucion de la paleta.

## Acceso local de servicio

Escribir exactamente `CHOMPS21` en el buscador (tambien acepta minusculas).
No muestra canciones para ese texto. Tras mantenerlo cinco segundos aparece una
tarjeta **+4 creditos**, seleccionada para activarla con el boton de poner/Enter
o raton. Cada pulsacion suma cuatro y mantiene la tarjeta disponible.

Editar el codigo o cerrar el buscador revoca la tarjeta y reinicia la espera.
Mantener el boton no repite creditos por la repeticion automatica del teclado.
Los creditos se guardan por el mismo camino inmediato que los normales, pero no
se cuentan como monedas ni ingresos. Se impide otorgarlos antes de restaurar el
estado, durante carga/cierre y si desbordarian el contador.

Es un acceso oculto local, **no autenticacion**: cualquiera que conozca el codigo
(o lea este repositorio) puede usarlo.

## Verificacion

`pruebas/validar-capas.ps1` compila GPU y CPU con avisos como errores y ejecuta
26 baterias. La nueva `paletaservicio.cs` incluye 434 comprobaciones: espera exacta
y vuelta del reloj, invalidacion de cache, tarjeta renderizada, concesion repetida,
persistencia y proteccion de estado/estadisticas, paleta sintetica de ocho colores,
transparencia, neutros, presets/sin genero, desactivacion, transiciones,
reutilizacion de memoria/texturas/geometria en D3D9 HAL y altura lunar en seis
resoluciones. Los archivos de prueba se crean en carpetas temporales aisladas.

Estas comprobaciones no certifican FPS con la biblioteca real, hardware HDD/SSD,
latencia audible ni ejecucion en Windows XP. Falta la apreciacion visual del
usuario con sus caratulas y su pantalla.
