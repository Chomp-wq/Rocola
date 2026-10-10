# Fluidez: no perder las pausas anteriores de la sesion

Continuacion posterior: ver `CIERRE_REAL_Y_APOLLO_2026-10-10.md` para la instalacion
de Modest, las comparaciones reales ya ejecutadas y Apollo actualizado en HD.
El estado pendiente que figura al final de ESTE informe corresponde al momento
anterior de la misma fecha; no sustituye las nuevas mediciones ni sus limites.

Cambios compartidos en Modest (.NET 3.5) y HD (.NET 4.8), solo en el diagnostico
GPU y el instrumento opcional. No se cambia la estrella, su trayectoria, la estela,
los colores, el audio, la calidad de caratulas ni los controles. CPU congelado.

## Defecto de observabilidad corregido

El diagnostico conserva 512 intervalos recientes y un anillo de 32 episodios.
Cuando se sobrescribian, ya no era posible reconstruir desde ese historial todos
los tirones o el maximo de una fase. No significa que esos cuadros no se dibujasen:
se perdia informacion de la medicion, no funcionalidad de la animacion.

Ahora mantiene, hasta el siguiente reinicio, el total de intervalos y episodios,
cuantos episodios coincidieron con un incremento de algun contador GC y el maximo
de toda la sesion. Guarda una sola muestra del peor intervalo con SUS fases
anteriores, hueco fuera del render, agenda, periodo y deltas GC. No se mezclan los
maximos independientes de varios cuadros para explicar uno concreto. En empates
conserva la primera muestra. Los cuadros normales a topes lentos tambien pueden
ser el maximo, sin clasificarse incorrectamente como tiron.

Solo agrega escalares y una estructura fija; no arreglos, colas, objetos/boxing ni
escritura de archivos por cuadro. Conserva el umbral adaptado a refresco/tope/video,
la salida acotada y su drenaje de texto una vez por segundo. Los buffers y las
estadisticas por ventana mantienen su comportamiento. No hay interruptor nuevo.
Una coincidencia GC NO es la duracion de una recoleccion ni prueba de causalidad;
si no se capturan contadores, cero coincidencias no certifica ausencia de GC.

## Instrumento real mas estricto

`estrellasesionreal.cs` distingue los maximos de informes recientes del maximo de
toda la fase, e informa los totales y las fases correlacionadas del peor intervalo.
Los p95/p99 siguen perteneciendo a ventanas recientes, no a toda la sesion.

Comprueba que haya exactamente un intervalo menos que cuadros presentados y la
misma generacion de medida desde el inicio: un reset, perdida de continuidad o
minimizacion invalida la escena. Detecta reinicios incluso si se reinician ambos
contadores. Rechaza tambien otra sesion con sufijos HD/Modest; antes solo buscaba
el mismo nombre exacto de instrumento. No cierra procesos. Captura la duracion de
la fase antes de la guardia final, sin incluir su enumeracion en presentaciones/s.

Se prepararon dos copias independientes del mismo album original de 17 pistas,
configuracion, tablas y modelos pequenos en TEMP, con hashes identicos de los 15
archivos copiados por rama e ia_datos=0. Se excluyeron respaldos, corpus IA, cola
persistida y ejecutables antiguos. La musica original solo se leera. Las tablas
copiadas NO certifican por si solas que el repaso este terminado: el instrumento
debe esperar pendientes cero y trabajadores terminados antes de cada medicion.

## Verificado en esta pasada

- Release de ambas ramas con advertencias como errores: correcto.
- `validar-capas.ps1 -SinHal`: 59 baterias correctas por rama; las comparaciones
  HAL opcionales se omiten explicitamente. Otras baterias conservan sus fixtures
  graficos sinteticos; esto NO es una comparacion de FPS con musica real.
- `fluidezcuadros`: 1.976 comprobaciones por rama, repetidas contra Release final
  en proceso nativo y x86. Incluye pausas que salen del historial, episodios
  sobrescritos/drenados, maximo con fases propias, empates, periodos lentos,
  GC desconocido, resets y fallos/entradas invalidas. La nueva prueba falla contra
  la compilacion anterior por ausencia de los contadores de sesion.
- Audita el codigo de registro por cuadro: sin newarr/newobj/boxing.
- Instrumento: 35 controles sinteticos por rama, nativo y x86. Guardia real:
  rechazo la Rocola abierta antes de leer datos o crear la ventana de prueba.
- CPU: 14 metodos con instrucciones, operandos resueltos, locales y excepciones
  identicos a la compilacion anterior en cada rama.

No se altero el decodificador MP3 externo para evitar asignaciones sin evidencia
comparativa. La hipotesis de sus reservas durante el repaso sigue siendo una
hipotesis; esta pasada no agrega una medicion de memoria/GC del lector real.

## Instalacion y pendientes

HD actualizado solo EXE/PDB en D:\ReleaseHD, cerrado, con respaldo recuperable
`respaldo_contadores_20261010_aeed299e5b2d49a69e15ad3d5f2bff25` (anterior/nuevo).
Instalado, Release y respaldo nuevo coinciden por SHA-256:

- EXE: `0F9FE70539C5BE28239CDB51ABABB469AEECD6C2F66C9FEFCE0048E6A78F86B9`.
- PDB: `D3DD3C3034C57CDABE40FCA657C31C8876C07E72E087959B57156173455F29F8`.

Configuracion, modelos y NAudio conservan sus hashes; otros 26 archivos de raiz
y 25.367 archivos de IA conservan tamano/fecha. No se sustituyo musica ni datos.

Modest sigue abierto en D:\Release1; no se cerro ni se sustituyo. Release pendiente:

- EXE: `7B1A192ED546683DAF39C7DCB1D85F6C9C24293ACE55EB93C288AFE4A2B5FB8E`.
- PDB: `3804BE8A8B72D914297C149339E5CC8B3D8313433BC4DF244DFCD9901C112CDF`.

La comparacion real de ocho escenas (Legacy/Natural, 2D/3D, navegacion quieta/
estado extendido) sigue pendiente mientras la Rocola de produccion este abierta.
No se ejecutaron esas escenas en paralelo con ella. No se certifica que el tiron
haya desaparecido, ni se promete animacion libre de pausas de Windows.
Despues faltara repetir con biblioteca completa y repaso activo. Apollo instalado
conserva su paquete anterior; actualizacion coherente, inferencia real y A/B audible
siguen pendientes por separado. No hay prueba fisica XP/HDD/CPU antiguo de dos hilos.
