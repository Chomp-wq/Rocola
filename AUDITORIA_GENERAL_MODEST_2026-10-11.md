# Auditoria general de Modest — 11 de octubre de 2026

Solo `claude/modest-carson-fa2v2n`. HD permanece suspendida. Se conserva el
destino .NET 3.5/Windows XP y el renderizado CPU congelado; no se agrega un
rostro ni se modifica el cuerpo o lenguaje corporal del asistente. Estas correcciones son
automaticas: no agregan interruptores de optimizacion en Ctrl+F3.

## Defectos corregidos

1. **Estado de creditos/cola:** una escritura fallida ya no se declara guardada.
   Conserva la instantanea pendiente y reintenta con un solo temporizador:
   0,5 s, luego 2 s y finalmente 10 s entre intentos. Un intento viejo no puede
   sustituir una solicitud nueva. Borrar el estado o cerrar cancela los reintentos.
   No puede garantizar persistencia si el disco permanece inaccesible o se corta
   la corriente antes de una publicacion correcta.
2. **Instancia unica:** las rutas relativas, puntos, barras y variantes de
   mayusculas de la misma carpeta se normalizan antes de calcular el mutex.
   No se afirma resolver alias de enlaces/junctions ni nombres cortos de Windows.
3. **Carpeta de datos:** la comprobacion de escritura usa un archivo temporal
   exclusivo, no un nombre fijo que pueda estar ocupado por otra comprobacion.
   Si debe recurrir a la carpeta del programa, se avisa al iniciar y se registra
   el motivo; no se borran los datos de la carpeta solicitada.
4. **Buscador:** durante la espera de actualizacion, confirmar o hacer clic ya no
   elige/gasta credito en una tarjeta de la consulta anterior. Primero se publica
   el texto pendiente; la eleccion requiere una accion sobre resultados actuales.
5. **Salida:** mantener Ctrl+Alt+Q no cuenta como tres pulsaciones independientes.
   La repeticion automatica de Windows se consume sin adelantar el gesto de cierre.
6. **Tarjeta elegida con raton:** el buscador fija el indice y foco correctos antes
   de elegirla, evitando una transicion originada en otra tarjeta.
7. **Resumen y ficha del disco:** adoptan duraciones y metadatos posteriores; la
   ficha tambien adopta la caratula cuando llega y los cambios de resolucion.
   Una medida de otro disco no vuelve a rasterizar/subir el resumen o ficha
   visibles si su contenido sigue igual. Se consulta memoria, no se abre musica.
8. **Miniaturas persistentes:** pasan de JPEG a PNG versionado (`_256_v2.png`),
   conservando transparencia y evitando otra compresion con perdida. Al volver
   a premultiplicar alfa puede existir un nivel de redondeo de color visible.
   La version nueva no reutiliza las miniaturas JPEG antiguas; ambas participan
   en el limite y limpieza existentes de cache. No se cambia la caratula original.
9. **Bordes GPU:** se rellenan los texeles sobrantes de las texturas de potencia
   de dos con el borde valido. Las caratulas/paneles usan CLAMP; solo la malla de
   ruido que necesita repeticion solicita WRAP. Evita leer relleno indeterminado
   o el borde opuesto al filtrar una imagen.
10. **Reloj WinMM:** amplia el contador de bytes al superar 4 GiB, conservando
    continuidad en sesiones largas. Un retroceso pequeno se trata como reinicio,
    no como audio escuchado. Un dispositivo nuevo reinicia la ampliacion y las
    salidas con reloj nativo de 64 bits no se truncan.
11. **Entrenador DSEE directo:** rechaza lotes de extractores incompatibles con
    el contrato actual (2), tambien al permitir ejemplos sin origen. No basta
    con usar el CLI directo para eludir la validacion de herramientas auxiliares.
12. **Separacion de entrenamiento/validacion/prueba:** los lotes con cero ejemplos
    seleccionados no cuentan como grupos independientes. Reciben particion -1;
    no falsean los minimos ni cambian el reparto de los grupos realmente usados.
    Se rechazan pesos negativos y formas incompatibles.

## Recursos GPU, sin reducir calidad

Se consulta el limite real de textura del dispositivo. Un panel que lo supera
se divide en teselas con un texel vecino de margen para el filtrado bilineal:
no se reduce la resolucion ni se cambian posiciones, opacidad o colores.
Las actualizaciones parciales incluyen los vecinos afectados. La creacion
es completa o falla liberando lo creado; el limite es 256 teselas por imagen,
sin reintentos o reservas sin cota.

Las portadas inmutables comparten una textura por bitmap/dispositivo entre
pared y cubos. Cada consumidor tiene su referencia real; el ultimo libera la
textura y retira las referencias al bitmap. No hay una referencia permanente
extra ni otra cache residente. Las capas de interfaz mutables NO se comparten.
Se mantienen los presupuestos y prioridades del regulador de caratulas.

El limite y los modos de direccionamiento proceden de las interfaces oficiales
[D3DCAPS9](https://learn.microsoft.com/en-us/windows/win32/api/d3d9caps/ns-d3d9caps-d3dcaps9),
[GetDeviceCaps](https://learn.microsoft.com/en-us/windows/win32/api/d3d9/nf-d3d9-idirect3ddevice9-getdevicecaps)
y [direccionamiento de texturas](https://learn.microsoft.com/en-us/windows/win32/direct3d9/texture-addressing-modes).
Las teselas, su margen y la gestion de referencias son decisiones de esta implementacion.

## Verificacion reproducible

`pruebas/validar-auditoria-general.ps1 -OutputDirectory <carpeta-temporal>`
compila las fuentes actuales GPU y CPU con el compilador nativo .NET 3.5,
advertencias como errores. No abre la Rocola de produccion ni un dispositivo de audio.

- Auditoria general: 65.611 comprobaciones GPU y 65.596 comunes/CPU. Incluye
  escritura bloqueada, recuperacion, solicitudes viejas, cancelacion, consultas
  pendientes, clic correcto, duraciones tardias, cache de ficha y 65.536 pixeles
  de alfa por variante. Son comprobaciones dentro de baterias, no ese numero de
  escenarios independientes.
- Texturas: 26.672 comprobaciones con driver artificial. Incluye memoria inicial
  sucia, bordes, actualizacion parcial, uniones bilineales en geometria animada,
  referencias compartidas entre consumidores reales, dispositivos distintos,
  liberacion del panel y fallos de creacion. No mide FPS ni representa una GPU vieja.
- `validar-capas.ps1 -SinHal`: 71 baterias generales del programa sobre la revision
  final. El flag omite la comparacion visual HAL opcional; otras baterias conservan
  sus fixtures graficos sinteticos. No es una medicion real con biblioteca/musica.
- `validar-audio-mejoras.ps1 -SkipLayers`: controles, DSEE, limitador, cosecha,
  entrenadores, formatos y PCM artificiales correctos.
- `validar-audio-20260929.ps1`: continuidad, reloj/latencia, NLayer integrado y
  104 pruebas Python correctas, incluyendo el renderer PCM real sin dispositivo
  ni UI. No se instala ni se entrena sobre el corpus del usuario.
- `validar-recursos-modest.ps1`: nueve baterias, repetidas en AnyCPU y x86,
  correctas. La lectura de fixtures MP3 conserva su PCM.
- Renderizado CPU: 14 metodos con instrucciones, operandos resueltos, locales
  y excepciones identicos a `1049d604226523740642451a1e3d7789fea5d8d2`, compilando
  ambos con el mismo compilador/opciones/nombre de ensamblado.

El lanzador Mono `todo.sh` incluye la auditoria comun; no se ejecuto en esta
maquina Windows ni se presenta como validado aqui.

La prueba antigua del guardado ahora inspecciona tambien su nuevo ayudante de
encolado; los drivers artificiales anteriores inicializan el estado de direccion
de textura y admiten WRAP explicito para ruido. No se eliminaron sus controles
de seguridad, geometria o PCM para obtener un resultado favorable.

## Instalacion y limite pendiente

`D:\Release1` no se cierra, sustituye ni configura en esta pasada: el usuario
pospuso la actualizacion y la instalacion estaba abierta durante la revision.
No se modifica musica, configuracion, creditos, modelos, corpus ni repaso a fondo.
Compilar en el repositorio no significa haber actualizado el programa instalado.

Queda la atribucion causal de las pausas ocasionales de la estrella en una sesion
aislada/controlada, Legacy/Natural y 2D/3D, con musica real y preanalisis terminado.
Estos arreglos no certifican que esas pausas desaparecieran. No se ha medido en
Windows XP, un CPU antiguo de dos nucleos/dos hilos ni el DAC del local.

Los contadores GC solo indican coincidencia; para atribuir su duracion harian
falta eventos correlacionados de una traza, como explica la documentacion de
[eventos GC de ETW](https://learn.microsoft.com/en-us/dotnet/framework/performance/garbage-collection-etw-events).
No se recogio ni se atribuyo una traza nueva de la sesion de produccion.
