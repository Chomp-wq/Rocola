# Audio: trabajo redundante y activacion del compresor

Base: `89951a4caf363e5b7776a7dd0a3b7ea934b8f02b`, exclusivamente Modest.

- PresenciaSala evita calcular el logaritmo del margen cuando cabe incluso la compensacion maxima de 3 dB. La curva, ataque, suelta, puerta y limitador conservan su comportamiento. El umbral de la via rapida lleva un margen conservador de redondeo. A 44,1 kHz evita hasta unos 1378 logaritmos por segundo en esas condiciones; no supone ese ahorro en todas las canciones.
- SalaLocal calcula la ganancia objetivo del balance solo al cambiar su valor. La cache pertenece al hilo de audio; la interfaz sigue publicando un entero volatile. Conserva la rampa, los filtros y la herencia entre pistas.
- PresenciaSala sigue contando los canales tambien en bypass. Antes, activar despues de una lectura impar hacia que los pasos de ganancia cayeran entre izquierda y derecha del mismo cuadro. Ahora ambos canales conservan la misma ganancia. Cuatro pruebas reprodujeron el fallo antes de corregirlo, a 8/44,1/48/96 kHz.

Son cambios automaticos: no alteran el aspecto ni agregan decisiones de sonido, por lo que no necesitan nuevos interruptores. No modifican preferencias, volumen de Windows, biblioteca ni modelos de IA.

## Verificacion

Compilados CPU y GPU con cero avisos, C# 3 y referencias oficiales Microsoft .NET Framework 3.5 (paquete ReferenceAssemblies.net35 1.0.3). Release GPU completo desde la lista del csproj, optimizado, con icono, recursos, propiedades y NLayer embebido.

`pruebas/compara-local.cs` compara dos ensamblados distintos y comprueba 288 combinaciones de etapa, tasa, canales, amplitud y lectura (1, 777 y 4096 muestras), incluidos cambios de balance/claridad/partes suaves. Comparado contra la compilacion real anterior: muestras identicas bit a bit en los caminos optimizados. El caso corregido de activacion impar se verifica por separado en `presenciasala`.

Pruebas aprobadas: audioeficiencia, audioestereo, audiosuave, sonoridad, picoreal, picomuestras, realce, transiciones, sinpausa, salida, gravemono, salalocal, presenciasala y localauto. Tambien las 14 baterias Python que ejecuta todo.sh. Sobre el Release entregado: presenciasala (51 comprobaciones), salalocal (103), localauto (35), audioeficiencia y comparacion numerica.

Se corrigio el contador de resultados de todo.sh para reconocer `PASS:` ademas de `PASS `: audioeficiencia salia con codigo 0 y dos PASS, pero el resumen la marcaba como fallida.

Limitacion preexistente: ventanaf3 detecta 78 recortes de texto con las fuentes de Mono de este entorno. La compilacion anterior 89951a4 reproduce los mismos 78. No se presenta toda la suite como aprobada ni se modifica la interfaz para acomodar las fuentes de Linux.

Las mediciones de compara-local incluyen la fuente sintetica y varian entre ejecuciones; no se atribuye un porcentaje de aceleracion general a la rocola. No se midieron FPS, Direct3D, audicion real ni rendimiento en el AMD A4-6300/Windows XP. Este cambio se concentra en costo y correccion de audio.

## Repetir la comparacion

Desde pruebas, con Mono y NAudio.dll disponible:

```sh
mcs -warnaserror+ -r:NAudio.dll -out:compara-local.exe compara-local.cs
mono compara-local.exe /ruta/antes.exe /ruta/despues.exe
```

Usar dos compilaciones reales, conservar las mismas opciones de compilacion para comparar tiempos y no ejecutar otras baterias mientras se mide. La igualdad numerica no depende de los tiempos.
