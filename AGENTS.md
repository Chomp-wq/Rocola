# Reglas permanentes de la Rocola

- Aplicar las mejoras compartidas en Modest y HD. Modest conserva .NET 3.5 y compatibilidad de destino con Windows XP; HD utiliza .NET 4.8.
- Decisión del usuario del 8 de septiembre de 2026: el renderizado por CPU es obsoleto y queda congelado. No modificarlo para añadir efectos, corregir su apariencia ni optimizarlo. No retirar ni revertir retroactivamente su implementación sin petición expresa.
- Las nuevas mejoras de renderizado se implementan exclusivamente en GPU. Se permite configurar sus opciones desde código común, sin alterar la salida del renderizador CPU.
- Toda nueva mejora visual o de rendimiento debe tener un interruptor independiente y persistente en la ventana de control Ctrl+F3. Desactivarlo debe recuperar el comportamiento anterior del efecto.
- Conservar música, modelos de IA, configuración y datos del usuario al compilar o desplegar.
- Distinguir pruebas automatizadas, comprobación visual real y mediciones de rendimiento. No afirmar compatibilidad real con XP sin probar en XP.
