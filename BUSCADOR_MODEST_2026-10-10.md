# Buscador Modest: relevancia, erratas y memoria

Solo `claude/modest-carson-fa2v2n`. HD sigue suspendida. No se modifica el
renderizado por CPU, el audio, los modelos, la musica, los creditos ni las
preferencias. Las mejoras del motor son automaticas; no llevan interruptores.

## Que cambia

- Canonicalizacion **propia del buscador**, tanto al indexar como al consultar.
  Se mantienen los nombres originales para mostrarlos y para reproducirlos.
  `Normalize` general y el mapeo historico del resaltado no se modifican.
- Se ignoran mayusculas, acentos, espacios sobrantes y variantes de separadores.
  Apostrofes rectos/curvos son equivalentes a omitir el apostrofe. Se conservan
  letras y numeros de otros alfabetos; una etiqueta con un surrogate roto no
  aborta la busqueda.
- El titulo completo de una cancion o un disco, y el artista completo, tienen
  prioridad sobre coincidencias parciales. Un prefijo del titulo gana prioridad
  a partir de tres caracteres; con una/dos letras no se entierran los artistas
  entre titulos genericos. Se conserva el desempate artista/disco/cancion y nombre.
- La puntuacion del titulo ignora el prefijo de pista reconocido (`03 - Dreamer`),
  pero el numero permanece en la clave y sigue siendo buscable. El titulo se
  delimita por dos enteros dentro de la clave, sin guardar otra copia normalizada
  de todos los titulos.
- **Solo si no hay ninguna coincidencia literal**, se admite una unica errata:
  letra omitida, insertada, sustituida o dos letras contiguas invertidas. La palabra
  corregida debe tener 4-64 unidades UTF-16; consulta de hasta 128 y 8 palabras.
  Las otras palabras deben coincidir literalmente. No se transforma la consulta
  visible ni se elige/reproduce nada automaticamente. El titulo completo corregido
  tiene prioridad sobre una palabra corregida dentro de un artista mas largo.
- No se agregan aproximaciones a una lista que ya tiene coincidencias literales.
  No es un modelo semantico, no inventa sinonimos ni corrige varias erratas.

Ejemplos comprobados con metadatos artificiales:

| Consulta | Coincidencia |
| --- | --- |
| `  JOSE  JOSE ` | Jose Jose con sus acentos originales |
| `ac dc` / `AC/DC` | AC/DC |
| `dont say` | Don't Say You Love Me |
| `precious depeche` | Depeche Mode - Precious (orden de palabras libre) |
| `preccious`, `precios`, `preciuos` | Precious, cuando no hay resultados literales |
| `depehce precious` | Depeche Mode - Precious |

No se promete que `acdc` equivalga a `ac dc`, ni tolerancia fonetica. Las
coincidencias recuperadas por signos/erratas pueden no llevar un resaltado
literal: no se dibuja una marca en una posicion que no existe en el nombre.

## Bugs corregidos

1. **Resultados que desaparecian al seguir escribiendo:** la consulta `love `
   descartaba nombres terminados en `love`; al pasar a `love i`, la coincidencia
   por palabras podia ser valida otra vez, pero la lista incremental ya la habia
   perdido. Ahora la clave de consulta no tiene espacios finales, y el subconjunto
   incremental cumple la misma regla de coincidencia que la busqueda completa.
2. Las listas aproximadas **no** se usan como subconjunto literal de la siguiente
   consulta. Tampoco se recortan a las 40 coincidencias visibles para acotar.
3. Mientras se reconstruye la biblioteca se vacian resultados, contador y estado
   incremental. No se conserva el contador de la consulta anterior ni se itera
   un indice que esta escribiendo el worker.
4. La cache de una letra ya no crece sin presupuesto al probar muchas letras o
   alfabetos. El desalojo no elimina musica ni limita los resultados buscables.
5. Un comienzo de palabra situado despues de 4000 caracteres podia puntuar cero
   o negativo y desaparecer. Una coincidencia literal siempre obtiene puntos
   positivos, tambien con metadatos anormalmente largos.

## Eficiencia y limites

- Se conservan el indice preparado, la mascara de letras, los postings selectivos,
  las consultas incrementales y la seleccion de los 40 mejores sin ordenar todo.
- Al reutilizar una consulta se restauran **como maximo 40 puntos**, no los de
  decenas de miles de coincidencias. Cada bolsa conserva los puntos del top y una
  copia de sus referencias; la vista recibe su propia lista. Se elimina el array
  de puntuaciones completo, que ya no se necesita.
- Letras: maximo 16 bolsas / 262144 coincidencias retenidas en conjunto; una bolsa
  de hasta 131072. Consultas recientes: maximo 8 / 65536 coincidencias; una bolsa
  de hasta 32768, claves de hasta 128. Son limites **de cache**, no de biblioteca
  ni resultados. No son una cota exacta de bytes: las listas tambien tienen
  capacidad sobrante/cabeceras. Se vacian al rearmar el indice.
- La aproximacion compara tramos de strings sin Substring, Split ni matrices por
  candidato. Los tokens se calculan una vez por consulta. Se aprovechan las letras
  que no pueden faltar aunque haya una errata y los postings existentes, sin
  construir otro indice de palabras que duplique la biblioteca en RAM.
- No hay nuevos lectores de disco, inferencia IA, trabajadores ni precarga masiva.
  La politica existente de indexado para dos hilos se conserva.
- El texto ASCII no pasa por descomposicion Unicode ni crea una copia intermedia
  en minusculas; una clave ASCII ya canonicalizada se devuelve sin reservar otra.

## Verificacion reproducible

`pruebas/validar-capas.ps1 -SinHal` compila estrictamente GPU/CPU con el compilador
nativo .NET 3.5 y ejecuta 68 baterias. El HAL se omite explicitamente: esto no es
una prueba visual ni una medicion de FPS.

`pruebas/validar-buscador.ps1 -Executable <Rocola.exe> -CpuExecutable <Rocola-cpu.exe> -DosHilos`
compila las pruebas AnyCPU y x86. Comprueba dentro de cada hijo que realmente
heredo dos procesadores logicos; restaura la afinidad y la variable de datos de
su proceso al acabar. Esa afinidad no equivale a un CPU antiguo.

`buscainteligente.cs` contiene unas 15000 comprobaciones por ejecucion:

- Todas las parejas de palabras `abc` de hasta cuatro letras se contrastan con
  una distancia de edicion independiente, incluyendo transposicion.
- El motor real se contrasta con una referencia que recorre **todo** el indice,
  sin cache, subconjuntos ni filtros de letras, y ordena todas las coincidencias.
  Incluye omisiones, inserciones, sustituciones e inversiones de letras.
- Escribir/borrar, espacios finales, cambio de orden, acentos/apostrofes,
  literal antes que aproximado, contaminacion de puntuaciones compartidas,
  limites de cache, reescaneo y reconstruccion del indice.
- Un indice artificial de 66001 entradas; todos sus resultados son artificiales.
  El contador incluye coincidencias fuera de las 40 visibles.

Tambien pasan `bolsa.cs` y `indice.cs`: 55447 entradas sin claves ni mascaras
erroneas; se conserva la prueba independiente del normalizador general. El
fixture historico deja de extrapolar tiempos a otro CPU con un multiplicador
inventado. Release compila con advertencias como errores; 14 metodos del dibujo
CPU tienen IL, operandos, locales y excepciones identicos al Release instalado
anterior (comparando builds del mismo compilador, no Roslyn contra csc 3.5).

En este PC, con afinidad de dos procesadores logicos, el fixture de 66001 entradas
dio aproximadamente 9 ms para una primera letra sin cache, 1.6 ms para `precious`,
5.2 ms para `depehce precious`, y alrededor de 0.1 ms al repetir consultas con
resultados. Incluye reflexion y la conversion de los 40 resultados a texto del
fixture. **No mide el repintado, FPS, teclado real ni un procesador XP**. La
Rocola de produccion seguia recolectando material durante las pruebas sinteticas;
no es una comparacion de fluidez antes/despues.

Evidencia local: `C:\Users\diego\AppData\Local\Temp\rocola-busqueda-1e78341f17164632830c791016654678`.

### Continuacion: listado real, sin abrir la musica

El runner acepta ademas `-LibraryCache <rocola_biblioteca.txt>`. Crea una copia
privada del listado y exige hashes iguales antes/despues de copiar; rechaza una
fuente que cambio durante la copia. El proceso de prueba lee esa instantanea con
`ReadLibraryRecords` y arma discos e indice con las funciones reales. No llama al
recorrido de firmas, no abre MP3/WAV/caratulas y no arranca otra Rocola. Los datos
de la biblioteca quedan solo en TEMP: no se incluyen en el repositorio ni se
imprimen rutas o nombres de los sondeos tomados del listado.

En la biblioteca instalada: **62.776 canciones, 4.468 discos y 68.132 entradas**.
Se comprueban los tramos de titulo y mascaras de todas las entradas. Se contrastan
27 consultas, incluidas muestras repartidas por el indice, contra un recorrido
completo sin mascaras/postings/cache: tanto las 40 visibles como el total. Las
consultas repetidas y escribiendo caracter por caracter deben coincidir, y las
entradas muestreadas deben recuperarse por su propio nombre aunque haya mas de
40 nombres iguales.

Pasaron 151.444 comprobaciones por arquitectura para GPU y 151.443 para la
compilacion CPU existente (sin modificar su dibujo), nativo .NET 3.5 AnyCPU/x86
con dos procesadores logicos. Incluyen las fixtures artificiales anteriores y
la validacion del listado. La referencia completa comparte las reglas de
relevancia con el motor: comprueba sus filtros y caches, no es una evaluacion
independiente de gustos/relevancia musical. La distancia de erratas mantiene
su referencia independiente. No son pruebas de teclado real, FPS ni XP fisico.

Evidencia local adicional:
`C:\Users\diego\AppData\Local\Temp\rocola-buscador-listado-20261010` y
`C:\Users\diego\AppData\Local\Temp\rocola-buscador-listado-cpu-20261010`.
La recoleccion de produccion siguio activa mientras se hicieron estas pruebas;
no se usa ese escenario compartido para medir ni prometer una mejora de FPS.

Release preparado: EXE SHA-256
`0AB4CD066517B73FCE063119B4A81271F9F6DE5589DDA16657142AEC1B622380`;
PDB `D8E0BE49E9BA7687E0DBDE5E15C88E722CB74191A513137F4DA7DAAC39170E97`.
Copias anterior/nueva preparadas (sin sustituir el EXE activo):
`D:\Release1\respaldo_buscador_20261010_171812_7ee2116e6a9c4223872c950a676bf737`.

## Lo pendiente de la pasada anterior

Continua pendiente el **perfil causal** de las pausas de la estrella durante el
repaso. Esta pasada no modifica la estrella ni atribuye las pausas a NAudio por
el mero hecho de encontrar reservas de memoria; tampoco apaga el repaso para
presentar una mejora falsa. El intento anterior de traza CLR fue denegado por
Windows; no se cambian permisos, dependencias ni CLR para sortearlo. Ver
`RECURSOS_MODEST_2026-10-10.md` para las mediciones y limitaciones completas.

La actualizacion de `D:\Release1` requiere cerrar normalmente su instancia.
La herramienta detecta el dialogo Sonido, pero sus controles quedan fuera de los
limites capturables del propietario; un intento de cerrar/traerlo al frente no
consiguio cerrarlo. Se solicito al usuario cerrar ajustes y Rocola. **No se fuerza
la terminacion ni se sustituye el ejecutable abierto.** La recoleccion seguia
activa y aumentando; al instalar se deben conservar datos/modelos/configuracion
y reabrir repaso+cosecha. No dar por instalada esta compilacion solo por publicarla.

En la continuacion se volvio a comprobar el problema del dialogo: solo se
expone como ventana seleccionable su propietario deshabilitado. Traer Sonido
al frente enfoca su control, pero la entrada de teclado vuelve al propietario
y el menu de cierre no aparece. Se pidio cerrar **solo Sonido** para poder
cerrar normalmente la Rocola despues. El EXE de produccion sigue siendo
`987DE6E1F087566248C8967CA1B0353895B8E3C2A8367262B1C4D71D923EF503`,
no el Release nuevo. Repaso+cosecha siguen activos; no se termina el proceso
a la fuerza mientras escribe ejemplos para intentar sortear la limitacion.
