# Caratulas adaptativas y LOD provisional — 01-10-2026

## Uso

El regulador funciona automaticamente en GPU en las dos ramas. No modifica la
resolucion final de las caratulas ni baja los efectos elegidos por el usuario.
No limita un equipo potente a 30 FPS: ese valor es una referencia de proteccion,
no un nuevo tope de dibujo ni una garantia de que nunca habra un tiron.

Ctrl+F3 -> Imagen -> Efectos contiene la opcion visual:
**Miniaturas provisionales durante la carga**. Se guarda como `caratulas_lod`;
viene encendida y solo cambia el aspecto mientras falta una imagen final.
El regulador de lecturas, composicion y subidas NO se puede desactivar.
Por peticion posterior del usuario, ahora se puede elegir su prioridad en
**Ctrl+F3 -> Pantalla -> Reproduccion y caratulas -> Carga de caratulas**:

- **Automatico** (predeterminado): conserva exactamente los presupuestos del
  regulador anterior y aprende cuanto margen tiene el equipo.
- **Fluidez**: emplea el 55% del presupuesto automatico y, en SSD, limita los
  lectores simultaneos a dos como maximo. Espacia las operaciones indivisibles
  de precarga: puede tardar mas en completar una pagina.
- **Equilibrado**: emplea el 85% del presupuesto y hasta la mitad redondeada
  hacia arriba de los lectores permitidos por el hardware.
- **Carga rapida**: admite hasta el 125% del presupuesto cuando hay margen, sin
  superar 12 ms ni la reserva de seguridad. Libera lectores saludables cada
  45 cuadros en vez de 90, sin superar el limite del hardware.

Los porcentajes se acotan siempre por los presupuestos minimos y maximos.
En todos los perfiles un HDD conserva un lector por unidad y un equipo de
dos hilos conserva un lector y como maximo 3 ms de composicion de fondo.
La deteccion de saturacion reduce presupuesto y lectores incluso en Carga
rapida. Ningun perfil cambia el tope de FPS, la calidad final, la paleta,
las miniaturas provisionales, el tamano de las caches ni el audio.
El render CPU permanece intacto; solo guarda esta preferencia para GPU.

El ajuste se aplica sin reiniciar ni vaciar las caches, y se guarda al cerrar
Ctrl+F3 como `caratulas_carga=0..3`. Un valor desconocido vuelve a Automatico.
Las operaciones ya iniciadas terminan normalmente; el nuevo perfil gobierna
las siguientes, sin cancelar lecturas ni recalcular el fondo.
Pantalla como de fabrica restaura Automatico y actualiza la seleccion visible.
Esta es una excepcion solicitada a la regla de optimizaciones automaticas:
no es un interruptor para desactivarlas.

Ctrl+F3 -> Imagen -> Medir incorpora un recorrido adicional con efectos altos y
musica si el medidor consigue abrirla. Enseña P95/P99 de intervalos entre
presentaciones, intervalos mayores de 33 ms y paginas listas/solicitadas.
La latencia maxima solo corresponde a paginas que llegaron a completarse.
Las abandonadas al seguir pasando no se disfrazan de cargas de cero ms.
El recorrido no cambia permanentemente los niveles de efectos ni guarda una
clasificacion irreversible del PC.

Para comparar perfiles, mantener resolucion y efectos, escoger uno y repetir
Medir. Comparar P95/P99 y paginas listas, no solo FPS medios. Repetir varias
veces: una cache caliente y una primera visita al HDD no son equivalentes.
Carga rapida no garantiza 30 FPS en cualquier equipo; Fluidez tampoco elimina
un bloqueo externo de Windows o el coste de una operacion indivisible.

## Que cambia

- Aprende el coste de componer una celda GPU y subir su textura. Comparte el
  presupuesto del cuadro visible entre ambas tareas y el preview.
- Si hay subidas pendientes, les da prioridad frente a componer mas celdas.
  El cache anterior sigue disponible aunque se haya agotado el presupuesto.
- Precarga las paginas adyacentes con el temporizador existente. Una casilla
  aplazada no se salta: se reintenta al recuperar margen. Reutilizar una vecina
  refresca su antiguedad para protegerla durante la poda.
- En equipos de dos hilos conserva un lector y menor presupuesto de fondo.
  En equipos mayores libera gradualmente mas lectores cuando hay margen, hasta
  el limite existente de cada unidad. Un HDD conserva un solo lector por unidad;
  no se convierte en SSD por disponer de mas nucleos.
- Distingue trabajo caro de simples intervalos externos lentos: un mensaje
  aislado de Windows no debe estrangular la precarga para siempre.
- Conserva el ritmo de dibujo configurado en reposo. Para una operacion GDI
  indivisible admite una fraccion de un cuadro de 60 Hz cuando existe margen,
  evitando que un tope de 200 FPS impida precargar una casilla de 5 ms.
- En rafagas reutiliza las caratulas completas ya preparadas. Para las que faltan,
  usa previews 32x32 disponibles, en lugar de componer grandes celdas de baja
  calidad o leer los originales desde el hilo de la interfaz.
- El preview desaparece al llegar la textura final; el fundido ordinario dura
  140 ms. Los discos con marca de cola dibujan el preview debajo de su marca y
  entregan directamente la imagen final para no ocultar el palomito. El
  seleccionado usa el preview en su cubo giratorio, sin duplicarlo debajo.

## Cache, limites y primera visita

`previews32-v1.bin` se crea en la carpeta de miniaturas existente. Es un archivo
agrupado: no se abren 18 JPEG originales desde la interfaz al cambiar de pagina.
Se lee en trabajadores de caratulas, dentro de los mismos turnos de disco.
Las nuevas previews se guardan despues de publicar la caratula final y avisar a
la interfaz, y tambien se generan al precalentar miniaturas en reposo.

Cada entrada tiene identidad derivada de ruta, longitud y fecha de la fuente,
mas una comprobacion de integridad. Una entrada corrupta se omite; una cola
incompleta se recupera mediante archivo temporal y sustitucion de la propia
cache. Nunca se sustituye la musica ni se utiliza esta cache para extraer la
paleta de colores: la paleta sigue viniendo de la imagen completa.

La memoria de entradas crudas esta acotada a 1024 previews con dos hilos (4 MiB)
y 4096 en otros equipos (16 MiB), mas estructuras. El archivo se compacta tras
128 entradas adicionales; no crece indefinidamente. Los bitmaps preparados
para la interfaz conservan hasta 256 previews (aproximadamente 1 MiB).
Las texturas LOD son pequeñas, se reutilizan y se liberan al abandonar la pagina
o reiniciar GPU. Durante una transicion hay como maximo 36 dibujos LOD adicionales;
una portada completa que nunca necesito preview no añade esos dibujos.

No puede mostrarse una silueta de una caratula que nunca se ha leido y para la que
aun no existe preview. En esa primera visita se conserva la sustitucion neutra
acorde al cielo hasta disponer de imagen. Una biblioteca mayor que la cache
tambien puede expulsar previews antiguas. El LOD reduce el vacio visual, pero no
es una promesa de que todas las portadas de cualquier biblioteca esten siempre
preparadas.

## Verificacion y limites

- Compilacion GPU y CPU con advertencias del compilador como errores y las
  32 baterias de `pruebas/validar-capas.ps1` en HD y Modest.
- `pruebas/caratulasadaptativas.cs`: presupuestos compartidos, crecimiento de
  lectores, presion, progreso sin inanicion, LRU, P95/P99, cache persistente,
  truncados/corrupcion, rafagas, opcion visual y texturas Direct3D HAL reales.
- `pruebas/atlasgpu.cs`: 480 comprobaciones por rama, incluyendo calidad final,
  etiquetas, cola, resoluciones de 1024x768 a 3840x2160 y reutilizacion.
  Repetido con afinidad a dos procesadores logicos.
- `pruebas/cpucongelado.cs` compara instrucciones, operandos resueltos, locales y
  excepciones de los metodos CPU. Los numeros de tokens de metadatos pueden
  cambiar al agregar tipos GPU; no se ignoran sus significados.
  Pasaron 14 metodos por rama. En Modest se recompilo el commit de referencia
  con el mismo compilador 3.5: compararlo con otro compilador puede cambiar
  asignacion de locales aun con fuente identica.
- Modest mantiene .NET 3.5; HD, .NET 4.8. El renderizado CPU no recibe LOD ni
  nuevas optimizaciones. Compilar para XP no demuestra funcionamiento en XP.
  No hubo pruebas en un XP real, un procesador antiguo ni un HDD real.

Se corrigio antes de publicar un intento demasiado restrictivo que aplazaba
caratulas entre 0.2 y 2.7 segundos. Se amplio el margen de operaciones
indivisibles y se evito reaccionar a pausas externas sin trabajo de render caro.

## Mediciones con biblioteca real en este PC

`pruebas/sesionreal.cs` abre Form1 completo y utiliza el medidor integrado. Las
dos sesiones completas se ejecutaron secuencialmente a 3440x1440 con 62776
canciones, tablas/modelos pequeños/miniaturas COPIADOS en TEMP y recogida IA
desactivada solo en esas copias. La musica original se lee sin modificarla.
La Rocola de produccion de Modest permanecio abierta: no se cerro ni se interrumpio
para medir, por lo que estas cifras incluyen competencia con otra aplicacion.

Una pasada HD de seis paginas con un segundo de precarga entre solicitudes dio
0.085, 23.602, 20.538, 13.811, 22.562 y 13.929 ms de espera hasta que las texturas
completas estuvieron listas. Una pasada de la version anterior en las mismas
condiciones dio aproximadamente 0.08–44.79 ms. No es un ensayo A/B controlado
ni una medida de disco frio.

Despues de medir con musica real muda, la espera en otras seis paginas fue
0.114–38.842 ms en HD y 0.434–72.115 ms en Modest. Los dos recorridos finalizaron
con cero errores de UI. Las llamadas internas de cambio de pagina y la espera
se miden por separado; esto no mide latencia fisica del teclado ni fotones.

En el recorrido lento/rapido con efectos altos se observaron:
HD: P95 12.09 ms, P99 101.74 ms y 12 intervalos superiores a 33 ms;
Modest: P95 17.55 ms, P99 145.62 ms y 16 intervalos superiores a 33 ms.
Estos resultados NO justifican afirmar 30 FPS garantizados sin caidas. Una
operacion indivisible, una lectura lenta, un mensaje de Windows o competencia
externa puede superar el margen. Las paginas abandonadas durante rafagas pueden
no llegar a estar completas. El benchmark no oculta esos picos tras la media.

Las sesiones anteriores al ultimo ajuste de contadores/orden del LOD documentan
el comportamiento del regulador; las baterias finales verifican esos ajustes.
La version final de Modest tambien recorrio seis paginas reales con afinidad a
dos procesadores logicos: esperas de 0.418, 15.786, 35.950, 25.293, 30.335 y
29.016 ms, un solo lector y cero errores de UI. El PC sigue siendo moderno:
restringir afinidad no simula un procesador antiguo ni la latencia de un HDD.
## Integracion con cambios nuevos de GitHub

Antes de publicar se incorporaron los cuatro commits nuevos de HD hasta
`bede28c` y los cuatro correspondientes de Modest hasta `73f6639`, conservando
sus arreglos de audio y de A CONTINUACION. Pasaron de nuevo las 32 baterias de HD.
El unico conflicto fue la prueba CPU: se conserva la
comparacion estricta por instrucciones, operandos, locales y excepciones.
Tambien se adapto el banco de estilo a los campos de tamaño de WinForms de
Microsoft, sin cambiar el programa ni relajar sus expectativas: pasaron 42
comprobaciones de estilo por rama, las de sinpausa, 49 de silencios en HD
(47 en Modest) y 71 de rendercache por rama.
Miniaturas y carriles tambien pasaron.

En una repeticion posterior, el dispositivo HAL dejo de estar disponible en
esta sesion. Una sonda independiente obtuvo HAL=False tanto con el ejecutable
anterior instalado como con el nuevo. La repeticion de atlas/LOD en ese momento
fallo por falta de dispositivo; no se contabiliza como una comprobacion HAL
correcta ni como una medicion de rendimiento. Las pruebas HAL anteriores ya
habian terminado correctamente. No se desbloqueo ni cambio la sesion del usuario.
La repeticion final de las 32 baterias de Modest tras integrar GitHub paso con
`-SinHal`: las partes sin dispositivo permanecieron activas y la bateria nueva
completo 31 comprobaciones logicas, omitiendo explicitamente sus siete checks
HAL. HD tambien paso esa bateria logica con afinidad a dos hilos. La opcion es
SOLO del runner de pruebas; sin ella, la comprobacion HAL de Windows sigue
siendo obligatoria. No confundir esa repeticion con nuevas pruebas de dibujo.

El runner alternativo `pruebas/todo.sh` incluye ahora la nueva fuente y bateria;
la parte Direct3D de esa bateria se omite explicitamente fuera de Windows.
No se afirma una ejecucion de ese runner con Mono/Linux en esta pasada.

## Instalacion anterior al selector de perfiles

Se actualizaron `D:\Release1` y `D:\ReleaseHD` cuando ninguno de sus ejecutables
estaba abierto; no se cerro el programa para forzar la actualizacion. Solo se
copiaron Rocola.exe, Rocola.pdb y NAudio.dll, verificando el contenido por SHA-256.
No se copiaron ni sustituyeron configuracion, creditos, cola, musica o modelos.

Los archivos anteriores se conservan en la subcarpeta `respaldo` de cada
instalacion, dentro de:
`codex-caratulas-20261001-f424263559014ea2ad8dc1e3219845c7`.

SHA-256 de los ejecutables instalados:

- HD: `1BD25DD470DF2BDC1AF1F12AF746CC9E2C63DDD11E3BCBAEFE817D11B17F2F0F`.
- Modest: `337510A90DFB4F3E3562F33A45F49A9C1766AA543714069C215A6111E188BFDA`.

La compilacion HD incluye los cambios de produccion de `8e9ab5d`; Modest, los de
`cf36fb7`. La actualizacion adicional de Modest tras integrar GitHub conserva
tambien la compilacion intermedia en su carpeta `respaldo`, dentro de
`codex-caratulas-integrada-20261001-8f8e298f519f455398b7559ad5679340`.
Los cambios posteriores de documentacion/pruebas no alteran esos
ejecutables. No se relanzo la aplicacion con el dispositivo HAL no disponible.

## Verificacion e instalacion del selector de perfiles

Las dos ramas compilan en Release (.NET 4.8 HD / .NET 3.5 Modest). Cada rama
paso las 32 baterias de `validar-capas.ps1 -SinHal`, incluyendo configuracion,
distribucion de Ctrl+F3 y el regulador. La bateria ampliada verifica las cuatro
selecciones reales, guardado/lectura, reset, presupuesto distinto, reserva bajo
saturacion y limites de dos hilos. Se ejecuto tambien sobre el perfil CPU
(solo preferencias comunes) y Modest con afinidad a dos hilos.
Se inspecciono la imagen de la tarjeta y se midieron sus textos con las fuentes
de la ventana. El comparador CPU confirma 14 metodos con instrucciones,
operandos, locales y excepciones identicos a los ejecutables previos.

HAL estuvo disponible en una comprobacion nativa del LOD en ambas ramas:
subida real, reutilizacion de textura, dibujo, fundido y liberacion pasaron.
En otra ejecucion volvio a no estar disponible; las repeticiones finales omiten
esa parte explicitamente. No se midio el rendimiento real de cada perfil con
la biblioteca y musica, ni se probo XP/HDD/equipos antiguos.

La prueba adicional `pestanas.cs` confirma que las cinco paginas caben y la
tira de pestañas no se solapa, pero conserva un fallo anterior por exigir que
no exista el campo heredado `viniloDisco` (CPU congelado). Se reprodujo en el
ejecutable anterior; no se retiro ese campo ni se cambio el render CPU.

Se instalaron solo Rocola.exe, Rocola.pdb y NAudio.dll en D:\ReleaseHD y
D:\Release1, con los programas cerrados y copias verificadas. Datos, ajustes,
modelos, musica, creditos y cola permanecen intactos. Los anteriores estan en
`respaldo/codex-perfiles-caratulas-20261001-f7b5c2ed9b5b4e1bb2f513a898db7e54`
de cada instalacion.

SHA-256 actuales:

- HD: `2770E6747CCA95ADE18FC6FEAB5EFD597B46058A0BEBA25565724F20A9EFB548`.
- Modest: `790728A808AE120C5F8C51610C9FCF9EC31D9508C2DB5E8BA0EEDB86B75090B3`.

