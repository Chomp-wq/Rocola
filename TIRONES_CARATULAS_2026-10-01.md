# Tirones al hojear caratulas — 1 de octubre de 2026

## Alcance

Correccion compartida exclusivamente para la ruta GPU en HD y Modest.
No cambia la resolucion de las caratulas, el diseño, la funcionalidad, los perfiles
existentes ni el renderizador CPU congelado. No añade interruptores.

El usuario informo de tirones ocasionales comunes a los tres perfiles.
La revision encontro mantenimiento sin presupuesto en esas mismas rutas.
La prueba sintetica reproduce las rafagas de limpieza, no demuestra que sean
la unica causa de los tirones observados en una biblioteca real.

## Cambios

- Liberacion de texturas nativas, etiquetas y LOD con un cupo compartido
  por cuadro: hasta cuatro liberaciones y un objetivo de 0,75 ms.
  Aprende su coste y lo carga al regulador de caratulas.
  Se permite una liberacion para garantizar progreso; una llamada individual
  al driver no se puede interrumpir, por lo que no es un limite duro de tiempo.
- La reserva para precargar vecinas utiliza el mismo cupo. Cuando no puede
  liberar mas sin gastar el cuadro, aplaza esa precarga; no barre todo el
  historial en una sola llamada ni expulsa las portadas visibles.
- Poda de bitmaps de etiquetas GPU en tandas de hasta cuatro y un objetivo
  de 0,5 ms por llamada. Mantiene el umbral inferior entre llamadas; ya no
  crea/ordena un array completo ni dispone decenas de bitmaps de golpe.
- Historial de previews de seis paginas (108 entradas, unos 432 KiB de
  texeles a 32x32). Durante la renovacion se permiten hasta 144 entradas.
  Antes se descartaban despues de cuatro cuadros, obligando a resubirlas
  al retroceder. Reset y cierre siguen liberando todas las entradas.
- Limite adicional de 36 entradas sobre el cupo de texturas nativas/etiquetas
  mientras progresa la poda: una rafaga no puede acumularlas indefinidamente.
  Una subida aplazada sigue pendiente y utiliza los previews cuando existen.
- El sondeo sincrono de memoria GPU se hace tras 600 ms sin cambio de pagina
  y como maximo cada diez segundos, sin cargas pendientes ni cambio urgente
  de pantalla. Durante el hojeo se conserva el presupuesto anterior.
  La estimacion inicial al crear el dispositivo permanece intacta.
  No se fija el presupuesto de los equipos potentes al de un equipo modesto.

## Verificacion

- Compilacion GPU y CPU con advertencias como errores en ambos proyectos.
- 34 baterias de validar-capas.ps1 aprobadas en HD y Modest, con HAL real.
- tironescaratulas.cs: 485 comprobaciones tambien contra ambas compilaciones
  Release, con afinidad a dos procesadores logicos. Incluye los tres perfiles,
  proteccion de visibles, convergencia de memoria, historial LOD acotado,
  limites de crecimiento y politica de sondeo.
- En el fixture anterior: una sola llamada libero 56 texturas; una poda de
  etiquetas elimino 108 imagenes. Con la correccion: hasta cuatro por tanda.
  Son conteos de trabajo, no una medicion de FPS de la aplicacion.
- partescaratulas.cs conserva sus 864 comprobaciones, incluidas comparaciones
  de imagen/legibilidad sobre superficies reales de GPU.
- cpucongelado.cs confirma instrucciones, operandos, locales y excepciones
  identicos en 14 metodos, frente a las compilaciones Release de referencia
  de la tanda anterior en ambas ramas.
- Compilacion Release HD (.NET 4.8) y Modest (.NET 3.5). Sigue existiendo el
  aviso previo de SDK 6.0A no registrado en Modest; compila correctamente.
  No se ha probado en Windows XP real.

Las pruebas de precarga anteriores se ajustan para avanzar el numero de cuadro
y volver a marcar la textura visible entre llamadas: invocar cincuenta veces
la precarga dentro del mismo cuadro ya no equivale a cincuenta cuadros.
Se conservan las comprobaciones de memoria, progreso, reutilizacion y no thrashing.

## Comprobacion pendiente en uso real

Comparar la misma secuencia rapida de paginas, resolucion y perfil con la
biblioteca del usuario. Estas pruebas no garantizan 30 FPS en cualquier GPU,
disco o procesador, ni eliminan pausas externas del driver/Windows.
No se borran caches para medir y no se alteran musica, creditos, modelos,
configuracion ni cola. No sobrescribir un ejecutable de produccion abierto
o modificado sin resolver primero ese estado.

Para repetir: pruebas/validar-capas.ps1. -SinHal omite expresamente pruebas
del dispositivo; no equivale a verificar el render GPU real.

