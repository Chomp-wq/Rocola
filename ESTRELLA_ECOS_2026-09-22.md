# Estrella reactiva y ecos cromáticos — 22 de septiembre de 2026

Cambios compartidos por Modest y HD, exclusivamente en el renderizador GPU.
No se modifica el renderizador CPU, el audio ni los datos del usuario.

## Aspecto y movimiento

- La esfera texturizada se sustituye por una silueta luminosa: núcleo opaco,
  cuatro puntas principales y cuatro secundarias en el estilo detallado.
  Los bordes y el halo conservan transparencia para integrarse con el cielo.
- La órbita es elíptica y coherente, con una inclinación suave en lugar de
  una textura girando continuamente. Se eleva al entrar en reposo.
- La estrella conserva su reacción musical y el color de la paleta de portada.
- En reposo aparecen hasta ocho imágenes residuales de posiciones reales
  anteriores. Mezclan el color de portada con cian, violeta y rosa; cada una
  se desvanece en 1,6 segundos. No se acumulan sobre una estrella inmóvil.
- Al volver a usar la rocola desaparecen suavemente los ecos. Cambiar el tamaño,
  reiniciar la GPU o reanudar tras una pausa descarta las posiciones antiguas.

## Control

Ctrl+F3 → Fondo → **Ecos de color de la estrella (GPU)**.
Activado por defecto; se guarda como `estela_estelar` y el restablecimiento de
Fondo lo reactiva. Requiere la estrella visible y sus órbitas activadas.
Los controles existentes de detalle, halo, polvo y protagonismo se conservan.

## Coste acotado

Se reutiliza una textura neutra de 256 × 256, tintada al dibujar, sin generar
ni subir imágenes nuevas por cuadro. La estela usa un historial fijo de ocho
posiciones y un lote preasignado de 48 vértices: como máximo un envío adicional
de geometría texturizada. El núcleo blanco añade un pequeño brillo GPU.
No se incorporan trabajadores, lecturas de disco ni recursos externos.

## Verificación y límites

- `pruebas/estelaestelar.cs`: historial acotado, fundido a 24/60/144/240 Hz,
  pausas, cambio de tamaño, interruptores, caducidad y presentación Direct3D 9 HAL
  con geometría de ecos no vacía; reutilización y liberación de la textura.
- `pruebas/lunadetalle.cs`: 32 combinaciones de los cinco controles, guardado,
  restauración y límites de los controles, además de caché y liberación HAL.
- `pruebas/espacioprecarga.cs`: núcleo opaco, transparencia exterior y silueta.
- `pruebas/paletaservicio.cs`: la órbita de reposo permanece elevada.
- `pruebas/validar-capas.ps1`: compilación GPU/CPU y 30 baterías de regresión.

La imagen `estrella-ecos.png` generada por las pruebas es una composición del
componente, no una captura de la aplicación completa. Las pruebas HAL no son
una medición de FPS en equipos de dos núcleos ni una validación visual con música.
Modest conserva el destino .NET 3.5/XP, pero no se ha probado en un Windows XP real.
