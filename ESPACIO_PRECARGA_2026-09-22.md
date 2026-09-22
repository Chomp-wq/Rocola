# Estrella, reposo y precarga — 22 de septiembre de 2026

## Implementado en Modest y HD

- La GPU sustituye la luna por una estrella: superficie luminosa opaca, granulación, corona y polvo orbital. Conserva un tamaño comparable y la órbita alta. En reposo se mueve más y adquiere protagonismo; durante el uso mantiene el movimiento suave.
- El color sigue la paleta activa de disco/género y recorre sus tonos lentamente. Giro, color y pulsación se resuelven con los vértices. La textura neutra de 256 × 256 se calcula una vez por estilo, no por canción, cuadro ni resolución.
- En reposo con música, muro y controles alcanzan transparencia cero, conservando cielo, aurora y estado ampliado. Se recuperan con la animación existente al usar la rocola. Las capas antiguas de una transición de color también respetan la ocultación.
- Se reutilizan los controles visuales existentes de Ctrl+F3; las claves guardadas de luna continúan siendo compatibles. Desactivar superficie detallada selecciona una estrella sencilla; desactivar el astro lo oculta. No se agregan interruptores de optimización.
- El renderizador CPU queda congelado: conserva su luna y su apariencia anteriores. Estos cambios visuales son exclusivamente GPU.

## Preparación de carátulas

La siguiente página empieza a solicitarse inmediatamente después de la página visible cuando la navegación es pausada (al menos 400 ms desde el cambio previo, o primera página). La cola de baja prioridad respeta los lectores y la coordinación HDD/SSD existentes; no se agregan trabajadores. En ráfagas se mantiene el asentamiento de 120 ms.

Se incluye el salto de la última página a la siguiente letra/sección, también al ciclar. No se cambia la selección para precargar. TOP se revalida al entrar porque su contenido puede cambiar. La página anterior dentro de la sección también conserva su preparación existente.

Las imágenes ya leídas se componen y se suben anticipadamente a la GPU, con el mismo aspecto/resolución. Se permiten más preparaciones si queda tiempo libre; en dos procesadores disponibles la subida de fondo sigue siendo de una textura por turno. Los presupuestos de tiempo, la prioridad visible y las pausas durante interacción/deslizamiento se conservan.

La caché de texturas deja de estar fijada siempre en 32 MiB. Al crear el dispositivo usa una estimación conservadora de memoria: base de 32 MiB cuando falta información o hay poca RAM, hasta 96 MiB en 32 bits o 128 MiB en 64 bits, sin exceder un cuarto de la estimación del controlador salvo el mínimo histórico. El límite de 54 entradas se mantiene. Las texturas visibles se protegen.

La estimación de [Direct3D 9](https://learn.microsoft.com/en-us/windows/win32/api/d3d9/nf-d3d9-idirect3ddevice9-getavailabletexturemem) es aproximada; no garantiza que una reserva posterior tenga éxito. No se consulta por cuadro.

Esto aumenta la posibilidad de llegar a una página completamente preparada, pero no promete carga instantánea en frío, después de redimensionar o con un disco lento/ocupado y memoria insuficiente. No se bloquea la navegación esperando imágenes.

## Verificación

- Compilación GPU/CPU y Release en ambos destinos (.NET 3.5 y .NET 4.8), advertencias tratadas como errores.
- 27 baterías de validar-capas.ps1, incluida espacioprecarga: solicitudes reales sin lanzar lectores, orden/prioridad, ráfagas, desbordamiento del reloj, última página, cambio de letra, reescaneo, opacidad cero, recuperación, superficie y límites.
- GPU HAL real: 18 texturas visibles más 18 siguientes de 512 × 512; navegación reutiliza las siguientes sin subirlas otra vez. Prueba conservadora de 32 MiB, protección de visibles, liberación y recuperación del dispositivo.
- Política de carga probada también con afinidad del proceso restringida a dos procesadores lógicos. No equivale a medir un procesador antiguo de dos núcleos.
- Vista de la textura generada inspeccionada; no es una captura de la aplicación instalada ni una medición de FPS.
- Pruebas adicionales DSEE, limitador y formatos MP3 CBR/VBR, WMA, AAC, WAV PCM/float32.
- Los ejecutables de prueba se conservan en carpetas temporales; no se modifican releases instalados, música, créditos, configuración ni modelos.
- Sin pruebas físicas de Windows XP, HDD lento o escucha comparativa.

La revisión de audio y sus propuestas, todavía sin aplicar al DSP, están en REVISION_AUDIO_2026-09-22.md.
