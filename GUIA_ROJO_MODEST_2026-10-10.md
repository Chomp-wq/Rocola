# Guía del botón rojo — Modest

Al mantener el botón rojo en la pared de discos, se resaltan el buscador y el abecedario. Aparecen flechas amarillas en los extremos de las letras y una flecha hacia arriba junto al buscador. Izquierda/derecha iluminan su flecha mientras se mantienen pulsadas. Se conservan las combinaciones y las teclas configurables existentes; no se añade un modo de navegación nuevo.

La guía se oculta al soltar el rojo, perder el foco o entrar en búsqueda, disco, atracción, configuración, vídeo completo o mantenimiento. Solo GPU; renderizado por CPU congelado. Los halos usan la capa GPU existente, sin temporizadores, trabajadores ni regeneración de texturas por cada cuadro del destello. La firma de la interfaz incluye únicamente la visibilidad de la guía, no sus pulsaciones ni su fase.

Opción visual, activada inicialmente: **Ctrl+F3 → Pantalla → La pared y el disco → Guía al mantener el botón rojo**. Se guarda como `guia_rojo=1/0`; el botón de fábrica la restaura. El abecedario reserva carriles laterales para no dibujar encima de TOP/VIDEO; su selección por ratón utiliza su ancho real.

Verificación: compilación Release y compiladores nativos .NET 3.5 GPU/CPU con advertencias como errores; prueba aislada de 32 estados y 36 posiciones de flechas entre 640 y 3840 píxeles; comprobaciones de persistencia en ambos perfiles. Comparación de 14 métodos CPU con instrucciones y operandos resueltos idénticos al ejecutable anterior. Las pruebas aisladas no equivalen a una sesión real, FPS del programa ni compatibilidad física con XP.

La compilación está en `Rocola/bin/Release/Rocola.exe`. La sustitución de `D:\Release1` sigue pendiente: la instancia de producción permanece abierta; no se han reemplazado sus archivos ni alterado su música, modelos, configuración o recolección de datos. HD no se modifica.
