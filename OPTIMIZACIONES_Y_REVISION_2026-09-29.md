# Optimización y revisión — 29 de septiembre de 2026

## Alcance

Cambios comunes aplicados a `rocola-hd` y `claude/modest-carson-fa2v2n`.
HD conserva .NET 4.8; Modest conserva el destino .NET 3.5/Windows XP.
El renderizador por CPU sigue congelado: se comprueba que compila, pero no se modifica
su dibujo. Estas optimizaciones no añaden interruptores ni alteran los ajustes visuales.

Esta revisión cubre los caminos modificados y sus regresiones. No significa que se hayan
demostrado inexistentes todos los bugs o que no haya más optimizaciones posibles.

## Implementado en ambas ramas

### GPU: textos y tarjetas del buscador

- Las cintas deslizantes conservan su identidad, textura y tiempo de animación aunque
  otra cinta se inserte o retire delante de ellas.
- La GPU conserva las texturas por bitmap, no por posición global: insertar un título
  no vuelve a subir toda la cola. Las texturas retiradas se liberan y el reset las reconstruye.
- Una portada o marca de cola repinta y sube únicamente las tarjetas afectadas y su halo.
  Se unen recuadros solapados y se redibujan los vecinos dentro del recorte.
- Se corrigió un clip de la portada sin imagen que reemplazaba el recorte parcial: podía
  volver a mezclar píxeles de una tarjeta vecina.
- Consulta, color, geometría, página, tamaño, pérdida de textura, versiones no consecutivas
  y fallo de subida parcial conservan la reconstrucción completa de respaldo.
- Se mantienen sombras, letras, bordes, miniaturas, animaciones y textos deslizantes.

Medición de bytes en un dispositivo D3D9 **imitado**, no FPS:

| Caso | Subida anterior completa | Subida con caché/recorte |
| --- | ---: | ---: |
| Insertar una cinta entre 21 existentes | 2.529.600 | 72.000 |
| Llega una portada, 800 × 600 | 450.408 | 56.536 |
| Llega una portada, 1024 × 768 | 925.044 | 90.396 |
| Llega una portada, 1920 × 1080 | 2.543.488 | 299.376 |
| Llega una portada, 3440 × 1440 | 7.567.248 | 885.960 |

### Biblioteca, HDD y SSD

- La pantalla toma snapshots coherentes de listas/tablas. La construcción de índices,
  selección y ordenación de tandas se realiza en trabajadores de fondo.
- Las esperas iniciales de los análisis se despiertan al cancelar; no es necesario
  agotar los 4/15 segundos previstos.
- F5 cancela los planes e invalida sus publicaciones encoladas **antes** de vaciar la biblioteca.
- Los lectores de análisis completo/estimación y el calentador de medidas comparten
  turnos por unidad HDD. Las lecturas cortas de cabeceras no están todas serializadas.
- SSD no recibe esa restricción de cabezal; el presupuesto de carriles respeta la CPU efectiva
  y la prioridad de la interacción. Una biblioteca mixta puede aprovechar el SSD.
- Se permite perfilar unidades añadidas después del primer escaneo.
- El índice TOP encuentra las canciones pedidas sin recorrer todas las pistas en cada
  reconstrucción, conservando discos de origen duplicados y su orden original.

### Audio: cancelación, relevo e identidad

- Cancelar la bomba de sonido no espera en la pantalla a que termine un `Read` bloqueado.
  Los lectores permanecen vivos y se liberan una vez que acaba ese último `Read`.
- Cada reproducción tiene una generación de publicación. Los bloques tardíos no alteran
  sonoridad, EQ ni pulso de la siguiente.
- El cierre/instalación de medidas del relevo comparte el cerrojo breve de publicación.
  Lectores, cola y operaciones de UI quedan fuera de ese cerrojo.
- La identidad se captura antes de abrir/medir el archivo. Viaja con el lector durante
  apertura asíncrona, precarga y relevo; no se sustituye por la del archivo actual de la ruta.
  Apollo transporta la identidad del original. Una apertura que cambia queda no certificable.
- Un archivo sustituido invalida las medidas relacionadas, incluso si mantiene duración
  y tamaño; se permite repetir también la medida integrada de esa sesión.
- Los resultados de análisis de un archivo que cambió no se publican ni certifican el repaso.
- El recorte de silencios comprueba el original, el mapa y el sello del lector, no solo duración.

### Persistencia y colores

- Nuevo archivo auxiliar `rocola_fuentes.txt`, formato `ROCOLA-FUENTES v1`.
  Guarda tamaño, creación UTC, modificación UTC y huellas normalizadas de las medidas/mapa.
  No modifica la música ni sustituye las tablas existentes.
- El redondeo de las huellas coincide con el de las tablas persistidas.
  Un guardado parcial/inconsistente se vuelve a medir en vez de certificar valores mezclados.
- Tablas antiguas sin sello se invalidan **al utilizarlas** y se reaprenden. La primera
  reproducción de esas canciones puede no disponer todavía de su nivelado/EQ aprendido
  ni de su recorte de silencios. No se purga toda la biblioteca al arrancar.
- Un sidecar ilegible o de versión posterior no se sobrescribe.
- Los colores de letras/discos se guardan como `ROCOLA-LETRAS v3` con identidad completa
  de biblioteca, no solo cantidad de canciones. Las versiones anteriores se recalculan.
- El diario reconoce restauraciones/ediciones externas de igual longitud cuando cambia
  creación o modificación, conservando sus reglas de guardado incremental.

**Límite de identidad:** tamaño y fechas son una firma barata de metadatos, no un hash del
contenido musical. Una herramienta que cambie los bytes y conserve exactamente los tres
metadatos puede eludir esta comprobación. No se leen canciones completas en UI para resolverlo.

## HD: trabajos externos y Apollo

- Presupuesto según afinidad disponible: 1 hilo para 2C/2T, 2 para 3, 3 para 4,
  la mitad para máquinas mayores (8 disponibles → 4; 16 → 8). Ya no hay un techo fijo de dos.
- Torch usa un único nivel de paralelismo entre operaciones y un presupuesto escalable
  dentro de cada operación; FFmpeg limita sus hilos de decodificación/filtros.
- Los subprocesos del motor se ejecutan por debajo de prioridad normal y sin ventana de consola.
- No se cambia el modelo, checkpoint, precisión, mezcla, remuestreo ni exportación WAV flotante.
  No se ha realizado inferencia con un checkpoint real como parte de esta revisión.

## Código muerto y herramientas de prueba

Se retiraron el helper de longitud sustituido por observación de metadatos, el timeout de
codificación de Apollo que ya no se utiliza, un campo sin lecturas del plan y una excepción
obsoleta del auditor de bloqueos. No se eliminan renderizadores congelados, respaldos
ni hooks de pruebas solo por parecer poco utilizados.

Se corrigieron fixtures que confundían delegados de Mono con los de C#, fuentes de Windows
con métricas de otra plataforma y bitmap premultiplicado con el formato de subida D3D9.
Las comparaciones de imagen completa contra parcial siguen siendo pixel a pixel.
`test_util.py` ahora admite `unittest discover` sin ejecutar `sys.exit` al importarlo
y utiliza un temporal exclusivo en vez de compartir `ut.wav`.

## Verificación reproducible

En cada rama, desde su raíz:

```powershell
.\pruebas\validar-mejoras-20260929.ps1
```

Compila GPU/CPU con advertencias como errores, usa la versión de .NET del proyecto
y ejecuta 20 baterías más `rendercache`. Acepta `-Tests` y `-OutputDirectory`.
No sustituye releases ni escribe datos de producción. `todo.sh` también registra
las nuevas baterías; su sintaxis se comprueba, pero Mono/Linux no se ejecutó en esta pasada.

Comprobaciones específicas aprobadas:

- `fuentes`: 19 HD / 17 Modest, incluyendo lector precargado y original de Apollo.
- `planbiblioteca`: 17 por rama.
- `cierrebomba`: 17 por rama, incluido `Read` que tarda más de un segundo.
- `bomba`: 26 por rama, misma cadena DSP bit a bit.
- `sinpausa`: 78 HD / 77 Modest, incluyendo publicación concurrente del primer bloque.
- `silencios`: 47 HD / 45 Modest.
- `rendercache`: 71 por rama; resoluciones 800 × 600 a 3440 × 1440.
- Cintas/reloj/tarjetas comparadas nuevamente en varias ejecuciones independientes.
- HD: `python -m unittest discover -s ia/upscale -p 'test_*.py' -v`: 99 pruebas.

También se ejecutan las regresiones de diario, archivo sustituido, picos, no finitos,
tablas/historial de arranque, etiquetas por adelantado, audio en memoria, arranque de salida,
panel de reposo, textos GPU y auditor de bloqueos. Apertura asíncrona y precarga se comprueban
adicionalmente con `-Tests aperturaaudio,precarga`.

## Pendiente / no afirmado

- Separar tinte y textura del panel de canciones para evitar hornearlo al cambiar color:
  **no implementado**. El tintado fijo de D3D9 no reproduce todavía exactamente degradados,
  aclarados, textos y placeholders actuales. Se mantiene su acabado existente.
- No se midieron FPS reales, latencia del dispositivo, navegación sobre la biblioteca del
  usuario ni velocidad de un HDD/SSD físico. Los números de fixtures no representan esas medidas.
- Modest compila contra .NET 3.5 en este Windows; falta la comprobación física en Windows XP.
- Los ejecutables de prueba quedan en una carpeta temporal. No se desplegó ni lanzó una Rocola
  instalada ni se tocaron modelos, música, créditos, cola o configuración del usuario.
