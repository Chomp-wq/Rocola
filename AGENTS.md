# Reglas permanentes de la Rocola

- Decisión del usuario del 16 de septiembre de 2026: subir a GitHub los cambios terminados y verificados de ambas ramas. Comprobar el resultado del push; no afirmar publicación si falla.
- Priorizar respuesta y reproducción también en procesadores modestos de 2 núcleos/2 hilos. Las precargas deben tener presupuestos acotados y no saturar los núcleos disponibles.

- Aplicar las mejoras compartidas en Modest y HD. Modest conserva .NET 3.5 y compatibilidad de destino con Windows XP; HD utiliza .NET 4.8.
- Decisión del usuario del 8 de septiembre de 2026: el renderizado por CPU es obsoleto y queda congelado. No modificarlo para añadir efectos, corregir su apariencia ni optimizarlo. No retirar ni revertir retroactivamente su implementación sin petición expresa.
- Las nuevas mejoras de renderizado se implementan exclusivamente en GPU. Se permite configurar sus opciones desde código común, sin alterar la salida del renderizador CPU.
- Decisión del usuario del 15 de septiembre de 2026: solo las opciones visuales llevan interruptores independientes y persistentes en Ctrl+F3. Las optimizaciones que conservan aspecto y funcionalidad son automáticas y siempre activas; no añadir interruptores ni preferencias para desactivarlas. Esta regla sustituye la anterior que exigía interruptores de rendimiento.
- Decisión del usuario del 28 de septiembre de 2026: saltar los silencios largos de las canciones lleva su propio interruptor persistente en Ctrl+F3 (Sonido), encendido de fábrica.
- Conservar música, modelos de IA, configuración y datos del usuario al compilar o desplegar.
- Excepción solicitada por el usuario el 1 de octubre de 2026: ofrecer perfiles de carga de carátulas en Ctrl+F3, con Automático como predeterminado. Ajustan prioridades y presupuesto GPU sin desactivar el regulador, cambiar la calidad final ni superar los límites de HDD y equipos de dos hilos. No autoriza otros interruptores de optimización.
- Distinguir pruebas automatizadas, comprobación visual real y mediciones de rendimiento. No afirmar compatibilidad real con XP sin probar en XP.
- Decisión del usuario del 1 de octubre de 2026: conservar el estilo de fondo Legacy y ofrecer Espacio natural como alternativa persistente en Ctrl+F3 -> Fondo -> Estilo. Legacy es el predeterminado para configuraciones antiguas; cambiar de estilo no debe alterar los ajustes individuales. Solo GPU.
