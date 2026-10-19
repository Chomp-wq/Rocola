# Fluidez: lo que se midió, lo que se descartó, y paradas que ahora dicen su contexto — Modest, 19 de octubre de 2026

Destino: AMD A4-6300, dos núcleos, 3,5 GB de RAM, SSD, Windows XP / .NET Framework 3.5, 1024×768. Solo la rama
`claude/modest-carson-fa2v2n`. Punto de partida: `426c052` (más dos commits de la sesión anterior: `98782e9` y `bc847f9`).

**Resumen.** Se buscó el siguiente ahorro importante en todo el dibujo. Con el mismo escenario completo de las pruebas
(rejilla, estado extendido, estrella 2D/3D, aurora, textos largos, progreso) el cuadro cuesta ≈ 0,09 ms y casi no reserva
memoria; lo único que sobresale son unos pocos eventos de milisegundos. **No apareció un ahorro nuevo que valga su
complejidad**, y se dice por qué. Lo que sí entra: una hoja Performance que cabe (sin barra de desplazamiento ni filas
bajo el botón Listo), una prueba que antes no existía, y un apunte de contexto en cada parada de pantalla para que la
próxima medición en el A4 diga **qué** la causó y no solo qué hacía la pantalla.

## Lo que se midió

Medidor: `pruebas/medirestado.cs` (controlador artificial, sin audio ni monitor; Linux/Mono, no el A4 ni XP), con las
opciones de Performance encendidas, 120 cuadros de calentamiento y 360 intervalos, animado, textos largos y progreso.

| Escenario | p50 | p95 | p99 | Máximo | Memoria por cuadro | Recolecciones |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Rejilla, 1024×768 | 0,09 ms | 0,13 | 0,32 | 0,51 | 0,9 KB | 0 |
| Estado extendido, 1024×768 | 0,09 ms | 0,20 | 3,99 | 6,45 | 1,8 KB | 0 |
| Estado extendido, 1920×1080 | 0,13 ms | 0,29 | 5,05 | 8,17 | 2,4 KB | 0 |

- **El p99 del estado extendido son los repintados del panel**, no el dibujo. Cada vez que cambia lo que muestra
  (canción, cola, color), el panel se rehace con GDI+: ≈ 3,1 ms a 1024×768 y ≈ 3,7 ms a 1920×1080, y el cuadro siguiente sube
  su textura: ≈ 0,5 y ≈ 1,5 ms. Son tiempos de este equipo y de libgdiplus; el GDI+ de XP en un A4 será mucho más lento, pero no
  se midió.
- **El color del género hace muchos repintados seguidos.** El comentario del código mide 14 repintados en la transición de
  color de cada canción. A 3,1 ms cada uno son ≈ 43 ms de trabajo repartidos en un segundo en este equipo. Lo que ya existe y
  los evita es **«Color directo del estado extendido»** (Imagen → Performance → hojita Estado): un solo repintado, con el cambio de
  color de golpe. Si el A4 sigue dando tirones justo después de cambiar de canción, es lo primero que hay que probar.
- **Cuidado con el medidor:** sin el mapeo de `RtlMoveMemory` a `memmove` (un archivo `rocola.exe.config` junto al programa;
  no se distribuye), el controlador artificial lanza excepciones en cada cuadro y da cifras unas 150 veces peores (p50 de 15 a 28
  ms y 1 a 2 MB por cuadro). Esos números **no son del programa**. Las baterías `estadoeficiente`, `resolucionligera`,
  `trabajovisible`, `auroraeficiente` y `cintasligeras` también fallan sin él (fallan igual en la rama de partida).
- **Fondo, un cuadro completo con luna y aurora** (`pruebas/inventario.cs`): 0,04 a 0,07 ms de procesador, 0 bytes por cuadro y
  0 recolecciones. La tarjeta pinta, a 1024×768, 6,5 pantallas por cuadro de fábrica y 1,9 con Performance (1,0 es el estirado del
  cielo y 0,5 la aurora).
- **Primer cuadro:** el primer `RenderFrame` tarda 105 a 215 ms y los siguientes 0,1 (compilación y arranque de Mono; en .NET será
  distinto). Se estimó el coste de compilar con .NET: los tipos del dibujo son 147 KB de los 751 KB de código del programa.
- **Programa entero con 30.000 canciones** (perfil sin tarjeta, servidor virtual): ventana lista en 148 ms, biblioteca en 1,0 s, memoria
  160 a 185 MB estable, hilo de pantalla ≈ 9 % de un núcleo con la biblioteca quieta y sin paradas de más de 25 ms después del arranque.

## Lo que se descartó, con su motivo

| Idea | Por qué no |
| --- | --- |
| Pintar la aurora dentro de la imagen del cielo | Ganaría ≈ 0,2 de 1,9 pantallas (≈ 11 %) y obliga a repintar el cielo cada vez que cambia la aurora. No compensa |
| Más reducción de relleno de la tarjeta | A 1920×1080 con Performance son ≈ 240 millones de píxeles por segundo, ≈ 2 GB/s de una memoria de ≈ 20 GB/s compartida: no es el límite |
| Compilar de antemano los métodos del dibujo (`PrepareMethod`) o NGEN | El coste total estimado de compilar esos tipos en el A4 es de unas décimas de segundo, y Mono no ejecuta `PrepareMethod`: no se puede medir aquí. Sin medida, sin complejidad |
| Modo de baja latencia del recolector | Evitaría recolecciones completas, pero con días de uso y 2 GB de direcciones en XP puede acabar en falta de memoria |
| Lista de «archivos que no se pueden medir» | Los rotos fallan en 0,3 a 7 ms: no ahorra nada medible |
| Repintar el estado en otro hilo | Es un rediseño grande (dos mapas, versiones, subidas parciales) para un coste de unos milisegundos por canción |

## Lo que sí se cambió

- **La parada de pantalla apunta su contexto** (`Rocola/MedidasBitacora.cs`). Cuando el vigía detecta una parada de 150 ms o más,
  la línea de `rocola_diagnostico.txt` (y la de `rocola_error.txt` desde 500 ms) añade: recolecciones (0, 1 y 2) desde el apunte
  anterior, memoria de trabajo y privada del programa, memoria gestionada, número de hilos, RAM física libre y de direcciones libre,
  y carga de memoria del sistema. Se calcula en el hilo del vigía y **solo cuando hay una parada que apuntar**: la pantalla no paga nada
  y no hay reservas por cuadro. Un contador del recolector coincidente no prueba la causa ni dice cuánto duró: sirve para sospechar
  o descartar. Batería: `contextoparada`.
- **La hoja Performance** (sesión anterior, ahora comprobada con esta versión): cinco hojitas por tema (Cielo, Grano, Aurora, Estrella,
  Estado), hasta cuatro ahorros cada una; antes eran 13 en una lista con desplazamiento y las últimas filas quedaban bajo el
  botón Listo. `ventanaf3` recorre ahora Fluidez, Performance y sus hojitas, y `estadoeficiente` comprueba cada interruptor en su
  hojita. No cambia ningún ajuste, clave ni valor.
- **`pruebas/inventario.cs`:** mide procesador, memoria y píxeles por capa y por orden de un cuadro del fondo.

No hay interruptores nuevos: nada de esto cambia el aspecto. El dibujo por procesador queda igual (14 métodos idénticos).

## Qué hacer en el A4 para encontrar la causa de verdad

1. Poner `diagnostico=1` en la configuración, usar la rocola con música y el estado extendido, y pasar `rocola_diagnostico.txt`.
2. En cada línea `pantalla parada`, mirar: `gc_desde_el_apunte_anterior` (¿hubo una completa?), `ram_libre` y `trabajo` (¿falta memoria o la
   están paginando?), `hilos`, y lo que decía `en:`.
3. Probar «Color directo del estado extendido» y comparar si las paradas de después de cambiar de canción desaparecen.
4. No entrenar IA, compilar ni ejecutar pruebas de tarjeta a la vez. Comprobar si el repaso de la biblioteca sigue activo.

## Cómo repetirlo

```sh
sh pruebas/todo.sh inventario contextoparada estadoeficiente ventanaf3 ajustesvivos performance
# el medidor completo (con el mapeo de RtlMoveMemory junto al ejecutable):
mcs -sdk:4 -out:me.exe -main:MedirEstado -r:System.Windows.Forms.dll -r:System.Drawing.dll \
    pruebas/medirestado.cs pruebas/franjareloj.cs pruebas/leercodigo.cs
xvfb-run -a mono me.exe Rocola.exe nuevos 1024 768 animado textos progreso
```

## Verificación

- GPU y CPU compilan con `-sdk:2 -langversion:3`, advertencias como errores (C# 3, referencias del perfil 2.0 de .NET; el ejecutable
  pide el CLR 2.0 y `System.Core` 3.5, o sea .NET Framework 3.5).
- **El mismo `Rocola.exe` que se entrega** pasó 39 baterías GPU y 7 CPU relacionadas (ver `VALIDACION.txt` del ZIP), con el mapeo
  de `RtlMoveMemory` de las pruebas. El dibujo por procesador: 14 métodos idénticos.
- No se ejecutó toda la suite sobre ese binario. La suite completa de las fuentes finales se comparó con la rama de partida (`426c052`): 34 baterías con problemas frente a 35, **las mismas** salvo `indice`, que solo falló en la rama de partida. Ninguna falla es nueva. Se corrió también antes de los dos últimos cambios de código, con el mismo resultado (34 frente a 35).
- **No probado:** Windows XP, una tarjeta real, la pantalla del bar, el AMD A4, ni escucha.
