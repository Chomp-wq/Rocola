# Más resolución con menos trabajo en Modest

Objetivo: AMD A4-6300 APU, dos núcleos, 3,5 GB de RAM, SSD y Windows XP.
Se parte de 1024×768 y del commit `69cee8c`. Solo Modest, destino .NET Framework
3.5; el renderizador CPU permanece intacto. No se cambia reproducción, análisis
musical, frecuencia de cuadros, navegación, créditos, biblioteca ni ayuda.

Subir a 1280×1024 multiplica los píxeles por 1,67; pasar a 1920×1080 los multiplica
por 2,64. Por eso esta revisión reduce trabajo enviado y capas que leen/escriben
píxeles, conservando textos y portadas a la resolución de salida.

## Automático, sin diferencia visual

1. **Suelo de aurora completamente tapado.** Con el estado opaco ya abierto y su
   textura actualizada, no se calculan sus colores inferiores ni se envían las
   cuatro filas de suelo que quedan enteras detrás. Se conserva la cortina byte
   a byte. Durante las transiciones, con transparencia o ante una versión de
   textura pendiente se mantiene el dibujo completo. Al volver a mostrar el suelo
   se reconstruyen también sus colores, aunque no haya llegado otra muestra.
2. **Panel opaco sin mezcla.** Ese mismo estado, solo completamente abierto y
   actualizado, se dibuja con mezcla desactivada. Evita pedir la lectura del color
   del destino para una transparencia que vale uno. El dibujo siguiente restaura
   su mezcla habitual; también funciona con texturas divididas por límite del
   controlador. No se aplica a paneles transparentes ni durante fundidos.
3. **Superficie 2D solo cuando hace falta.** La estrella 3D y su ayuda usan
   geometría; ya no necesitan hornear y subir la textura 2D de 256×256 al arrancar.
   Cambiar a 2D la prepara antes de dibujar; volver a ella reutiliza la que ya se
   preparó. Conserva cuerpo, gestos y estrella sin rostro.

El relleno de la aurora pasa de **8.640 a 5.184 vértices** cuando el suelo está
cubierto: 40 % menos en esa malla, no 40 % menos de coste total ni 40 % más FPS.
Con «Aurora con menos columnas», pasa de 4.320 a 2.592. Su filo, horizonte y
respuesta musical conservan su camino anterior.

| Salida | Alto del estado | Píxeles del panel que pueden dibujarse sin mezcla |
|---|---:|---:|
| 1024×768 | 245 | 250.880 |
| 1280×1024 | 327 | 418.560 |
| 1920×1080 | 340 | 652.800 |

La desactivación de mezcla ofrece al controlador un camino más barato; no mide
por sí sola cuánto ancho de banda ahorra una tarjeta concreta.

## Dos interruptores nuevos en Ctrl+F3 → Imagen → Performance

| Ajuste | Qué cambia | Dependencias y conflictos |
|---|---|---|
| Fondo sin grano | Quita los puntos finos del fondo. No borra nebulosas, degradado ni viñeta. Omite su pasada normal, la combinación con viñeta y el grano dentro del cielo guardado. | Solo GPU. «Grano y viñeta en una pasada» queda en gris: «Sin efecto: Fondo sin grano (Performance)». El fondo en una capa sigue sirviendo. |
| Aurora sin velo inferior | Quita la luz bajo el horizonte; conserva cortina, filo, horizonte y las ocho bandas. Sirve también durante la navegación. | Solo GPU y Aurora del horizonte en Medio o Alto. Sin aurora, queda en gris e indica ese requisito. No se bloquea por usar el estado opaco, porque también actúa fuera del reposo. |

Ambos vienen apagados en una configuración anterior; se guardan individualmente
como `sin_grano_perf=` y `aurora_sin_suelo=`. Los presets Bajo, Medio y Performance
y «Solo Performance» los incluyen; Alto y restablecer los apagan. La lista ahora
contiene doce ahorros y conserva su desplazamiento y cabecera fija en 1024×768.

Si el grano todavía no se había preparado, no se crea su textura de 1024×1024.
Si ya estaba, se conserva para poder restaurarlo sin hornearlo otra vez. Al
cambiar el ajuste se invalida la imagen del cielo cuando corresponde; no quedan
puntos antiguos pegados dentro de la caché.

El medidor trata quitar el grano y unirlo a la viñeta como alternativas: no suma
sus ahorros como si fueran independientes. Sigue comprobando la propuesta sobre
un cuadro real de la aplicación. Los controles en gris conservan sus valores y
se reactivan al desaparecer la causa, siguiendo la regla A1.

## Qué se comprobó

- `resolucionligera`: 83 comprobaciones con controlador artificial en 1024×768,
  1280×1024 y 1920×1080. Se prueban preparación 2D/3D, omisión y restauración del
  grano con/sin cielo guardado, viñeta conservada, subida y dibujo del estado,
  transiciones y restauración del suelo. El raster de referencia compara en
  320×240 los píxeles de suelo cubierto y mezcla opaca, con ambas densidades de
  aurora; la cortina se compara byte a byte.
- `estadoeficiente`: 175 comprobaciones, incluidos ocho ajustes persistentes,
  acceso a los doce mandos y envío opaco de una textura dividida con límite de
  512 texeles, regresando después a la mezcla normal.
- `ajustesvivos`: 92 comprobaciones de dependencias, conflictos, vuelta automática,
  valores conservados y límites de los mandos.
- `performance`: 112 comprobaciones, incluidas las alternativas de grano.
- Se conservan las regresiones de caché, reloj, panel, fluidez, estrella, portadas
  y navegación. También pasan `asistentecombo`, `asistentevisual` y `ayudaguia`.
  El ZIP registra las dieciocho baterías sobre su propio ejecutable.
- GPU y CPU compilan con referencias oficiales de .NET 3.5 y advertencias como
  errores. Pasan además `estadoeficiente` y `ajustesvivos` contra CPU.

`pruebas/validar-estado-extendido.ps1` incorpora la nueva batería con el compilador
.NET 3.5 de Windows; no se ejecutó ese script en esta sesión. `todo.sh` registra
`resolucionligera` con sus apoyos. Las pruebas usan datos aislados.

## Comparación de tiempos: límites de la evidencia

`medirestado.cs` admite `ejecutable perfil ancho alto`; perfiles `original`,
`performance` o `nuevos`. En esta comparación, «performance» activa cinco ahorros
anteriores: estado opaco, menos columnas, sin reflejo/estela en reposo y corona
compacta. «nuevos» añade los dos de esta revisión. No activa el cielo intermedio:
el controlador de este medidor no imita ese destino; su funcionamiento se cubre
con el controlador de `resolucionligera` y `performance`.

Se ejecutan procesos separados y secuenciales, sin compilar ni ejecutar otras
baterías a la vez: antes (`69cee8c`), cambios automáticos y ahorros nuevos, en las
tres resoluciones, con rejilla estática y estado, estrella 2D y 3D. Cada caso tiene
120 cuadros de preparación y 360 intervalos entre inicios de cuadro, con trece
repintados sintéticos de contenido en el estado. Se registran p50/p95/p99/máximo,
mapas/texturas nuevos y cuadros aceptados/fallidos. La rejilla estática no sustituye
las pruebas de navegación y ayuda de las demás baterías.

Las 36 combinaciones aceptaron sus 361 cuadros y no fallaron presentaciones
artificiales. En estado, ambas versiones crean cero bitmaps y cero texturas
adicionales: esa reutilización ya existía, no se atribuye a esta revisión.

**No hay una mejora uniforme de los tiempos del host.** Por ejemplo, en estado
3D a 1920×1080, el p99/máximo fue 2,534/3,129 ms antes, 2,522/3,070 ms con los
cambios automáticos y 2,897/4,226 ms con los dos ahorros nuevos. El simulador no
hace el trabajo gráfico de la GPU; sus cifras no prueban que los ahorros visuales
mejoren la fluidez real. Las salidas completas van en `MEDICION_SINTETICA.txt`.

Toda la ejecución de esta sesión es Linux/Mono CLR 4 con referencias de .NET 3.5.
El mapeo de copia de memoria usado por el controlador artificial no se distribuye.
No hubo escucha, GPU real ni Windows XP real. Lo comprobado es la reducción de
trabajo enviado y la conservación de las rutas indicadas; **1080p fluido en el
AMD A4 queda pendiente de medir en la rocola**.

## Probar en la máquina

Actualizar solo el ejecutable, NAudio y avisos, conservando los datos. Probar
primero los cambios a 1024×768 y después una resolución mayor que admita la
pantalla. Comparar la misma canción, opciones y estado del repaso de biblioteca,
con estrella 2D/3D, entrada/salida del reposo, navegación y ayuda. El medidor de
Imagen y `diagnostico=1` permiten recoger p95/p99/máximo y pendientes de trabajo.
No mezclar entrenamiento, compilación o pruebas de GPU con esa comparación.
