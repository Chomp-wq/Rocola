# Estela 3D: geometria reutilizada y prueba de fluidez preparada

Continuacion de las pasadas de fluidez y audio del 10-10-2026. Cambios compartidos
en Modest y HD, exclusivamente en GPU. El renderizador CPU permanece congelado.

## Mejora implementada

Las copias de la estela guardan una pose congelada, pero antes se proyectaban,
ordenaban e iluminaban de nuevo en cada cuadro. Ahora cada uno de los doce slots
conserva sus vertices. Solo se recompone una copia si cambia su posicion, radio,
orientacion o color, o al cambiar Legacy/Natural. Tiempo y fuerza siguen actualizando
el alfa sin repetir la proyeccion. No cambia la estrella principal, su trayectoria,
velocidad, cadencia, separacion de ecos, paleta, duracion ni material.

Los lotes enviados a Direct3D conservan orden, numero de triangulos, geometria,
RGB, alfa y redondeo exactos, incluido el filo aditivo y su extremo transparente.
La cache usa unos 80 KiB de vertices adicionales por panel, en arreglos fijos;
no reserva memoria por cuadro, no guarda portadas ni agrega trabajadores.
Es automatica, sin interruptor nuevo. El modelo 2D conserva su dibujo anterior.

El ahorro aislado es pequeno en este PC: se evita trabajo repetido de geometria,
no una pausa de varias decenas de milisegundos. **No se presenta como solucion
demostrada de todos los tirones ni como ganancia medida de FPS del programa.**

## Verificaciones terminadas

- Release de ambas ramas con advertencias como errores: correcto.
- `validar-capas.ps1 -SinHal`: 59 baterias correctas por rama. Las comparaciones
  HAL opcionales se omiten explicitamente; otras baterias mantienen pruebas graficas
  sinteticas. No es una sesion comparativa con musica real.
- `ecosgeometria`: 24.337 comprobaciones y 24.074.148 bytes de vertices identicos
  por rama, frente a la receta sin cache. Repetido con el ejecutable anterior como
  referencia independiente, en proceso nativo y x86. Incluye estilos, materiales,
  transparencias, edades, los campos de la clave y reutilizacion/reinicio del anillo.
  La bateria falla con el binario anterior, que carece del preparador cacheado.
- `estrella3d`: 66 comprobaciones; ahora tambien verifica que el preparador no
  reserve arreglos. Las pruebas de reloj, Natural, aurora y diagnostico siguen verdes.
- CPU congelado: 14 metodos de dibujo con instrucciones, operandos resueltos,
  variables locales y excepciones identicos a la compilacion anterior por rama.
- Instrumento real: 21 controles sinteticos de preparacion/captura/matriz/aislamiento
  en ambas ramas, tambien x86. La guardia rechazo la Rocola de produccion abierta,
  sin cerrarla, abrir otra ventana ni acceder a sus datos para una prueba.

El microbenchmark de ecos compara cache caliente con reconstruccion **mas copia**
de la misma receta; no es un antes/despues del binario completo. Sus tiempos no se
convierten en FPS, ni la ejecucion x86 en certificacion de Windows XP.

## Medicion real aun pendiente

`pruebas/estrellasesionreal.cs` recorre ocho escenas: Legacy/Natural, estrella
2D/3D y navegacion quieta/estado extendido. Usa el bucle de render y temporizador
reales, no cuadros forzados. Abre la misma pista desde cero, con DSP real y salida
muda; conserva la prioridad del programa. Calienta y espera la barra antes de medir.

Espera que terminen escaneo, duraciones, repaso e integrada; comprueba pendientes
y trabajadores vivos, no solo ia_datos=0. No pausa el repaso para mejorar las cifras.
Rechaza otras Rocola/pruebas abiertas y datos originales o enlazados. Las ocho
escenas, sus intervalos reales y el camino de reproduccion de este instrumento
**no se han ejecutado en esta pasada**: solo se compilo, se probo su autoprueba y
se verifico la guardia contra otra Rocola abierta. Uso/requisitos en pruebas/LEEME.md.

Informa cuadros presentados, fallos, rangos p95/p99 de ventanas recientes de 512
cuadros y maximos de intervalo, hueco fuera del render, agenda y fases. No son
percentiles agregados de toda la sesion, fotones ni medidas de pausas GC. Los
maximos independientes no se suman. La navegacion quieta mantiene la pared activa
sin pasar paginas; los cambios de pagina se miden aparte con sesionreal.

Sigue pendiente comparar las ocho escenas con una copia de un album completamente
medido, despues repetir con biblioteca completa y repaso activo, sin compilacion,
HAL ni entrenamiento concurrentes. No hay certificacion fisica XP/HDD/CPU antiguo.
Apollo instalado conserva su paquete anterior; su actualizacion coherente e
inferencia/A-B audible siguen siendo verificaciones separadas pendientes.

## Instalacion

HD actualizado en `D:\ReleaseHD`, solo EXE/PDB, con respaldo recuperable
`respaldo_estela_20261010_e49893510624497f9e301e4b388e95d0` (anterior/nuevo).
El instalado coincide por SHA-256 con Release y el respaldo nuevo:

- EXE: `032DBD3A23985DFF352295677353BA4056679347C92DDF83338DC45F8720F4D4`.
- PDB: `941498EF471478B9B970805BB6B127F68F7DEA2318E652465D06D6B42E0166D2`.

Configuracion, modelos y NAudio conservan sus hashes; los otros 26 archivos de
raiz y 25.367 archivos del directorio IA conservan tamano y fecha. No se sustituyen
musica, corpus, creditos, cola ni tablas.

Modest preparado en Release pero no instalado: `D:\Release1\Rocola.exe` sigue
abierto. No se cerro por cuenta del agente. La compilacion pendiente incluye
tambien las mejoras anteriores de memoria y lectura WAV:

- EXE: `1DDF43B4A8E3A59FFBA747F4B77774FB118AA7E020C8E97F57D4276F3F7E630A`.
- PDB: `744F56FC7369D20B24987A8EE5B0D3ADAF3A7C195D07308D9CE65F13203C4BA0`.

No se ha creado un monitor de instalacion en segundo plano: la comprobacion de
cierre se hace durante el trabajo activo y nunca reemplaza un ejecutable en uso.
