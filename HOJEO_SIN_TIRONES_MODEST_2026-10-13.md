# Hojear rápido sin micro tirones, y los perfiles de carátulas con JPEG — Modest, 13 de octubre de 2026

Petición del usuario: «Al pasar rápido de página ahora las carátulas cargan instantáneamente y no bajan ni un poco
los fps, pero en el fondo se notan micro tirones, no es cien por ciento fluido. Revisa la causa y ajusta los 3 modos de
carga de carátulas para que aprovechen el nuevo formato de imágenes, por si el programa quiere usarse en dispositivos
aún más débiles».

Todo es solo con tarjeta gráfica. El dibujo por CPU no cambia: se comparó con la versión anterior, instrucción por
instrucción.

## Las causas de los tirones

Los FPS no bajaban porque los cuadros salían a tiempo. Lo que fallaba era **cuánto se movía el fondo en cada cuadro**:
unas veces nada, otras el doble. Se encontraron cuatro causas.

1. **El reloj grueso.** El deslizamiento de la pared y el empujón del cielo (estrellas y nebulosas) medían el tiempo
   con `Environment.TickCount`. En Windows ese reloj avanza a saltos de unos 15,6 ms. A 60 cuadros por segundo, un
   cuadro veía el tiempo quieto y el siguiente un salto doble. Las portadas lo disimulan porque llegan rápido a su
   sitio; el cielo se mueve despacio y se ve entero, así que ahí se notaba.
2. **Cada página nueva tiraba el empujón anterior.** Al pasar otra página antes de que el cielo terminara de correrse,
   el empujón nuevo empezaba de cero y lo que faltaba del anterior se perdía. Hojeando rápido, el cielo frenaba y
   arrancaba en cada página.
3. **Las nebulosas chocaban contra su tope.** Lo que se corrían se cortaba en seco al llegar al máximo. Hojeando
   rápido, se quedaban clavadas de golpe mientras las portadas seguían.
4. **Cada carátula leída pedía memoria nueva.** El lector pedía un arreglo nuevo por cada miniatura. En PNG casi
   siempre pasa de 85 KB, y esos solo los recoge la limpieza completa de memoria, que detiene un momento todos los
   hilos. **Es una causa posible, no demostrada:** que una limpieza coincida con un tirón no prueba que lo cause.

## Qué se cambió

- **Reloj fino** (`Form1.Pared.cs`). La pared y el empujón del cielo usan el cronómetro de alta resolución. Las curvas
  son las mismas, ahora con fracciones de milisegundo. `Sliding` sigue con el reloj de antes, porque también lo usa el
  dibujo por CPU, que no se toca.
- **Los empujones se suman.** Hay hasta cuatro a la vez, cada uno con su curva. Dos páginas seguidas corren el cielo
  exactamente dos empujones, en pasos siempre hacia el mismo lado.
- **Rodilla en las nebulosas** (`CieloVivoGpu.cs`). Hasta el 75 % del tope se corren igual que antes, así que una
  página suelta se ve idéntica. Desde ahí se acercan al tope poco a poco, sin chocar.
- **La lectura reutiliza su memoria** (`Form1.Portadas.cs`, `LeeEntero`). Cada hilo lector guarda un arreglo y lo
  vuelve a usar. Los archivos de más de 2 MB (portadas originales grandes) siguen pidiendo uno propio, que no se
  guarda. La imagen que sale no depende de ese arreglo.

Son automáticas, sin interruptor: no cambian el aspecto.

## Los perfiles de carga, con JPEG

Una miniatura JPEG pesa unas seis veces menos que la PNG y se lee en un tercio del tiempo. Por eso los perfiles ahora
deciden también cuántas páginas vecinas se preparan por adelantado (el «colchón») al detenerse en una:

| Perfil | SSD, PNG | SSD, JPEG | Disco duro, PNG | Disco duro, JPEG |
| --- | --- | --- | --- | --- |
| Fluidez | 1 | 1 | 1 | 1 |
| Automático y Equilibrado | 2 | 2 | 1 | 2 |
| Carga rápida | 2 | 3 (con más de 2 hilos) | 1 | 2 |

- **Fluidez** es el de los equipos más débiles. Se queda con una página a cada lado, tenga el disco que tenga (antes,
  con SSD, eran dos), para dejar el mínimo de trabajo de fondo. El JPEG ya le abarata cada lectura.
- **Con disco duro**, el JPEG permite dos páginas de colchón en vez de una, porque las lecturas son cortas.
- **Carga rápida** llega a tres páginas con SSD y JPEG, pero no en equipos de dos hilos.
- **No cambia:**
  - el número de lectores: uno por disco duro y uno en equipos de dos hilos;
  - el regulador;
  - la calidad final.

  Lo de más se pide con la prioridad baja de siempre, detrás de lo que se ve. El colchón más grande (144 portadas)
  cabe en la memoria de carátulas del equipo más chico (220).
- **Sin tarjeta gráfica**, todo sigue igual: dos páginas con SSD y una con disco duro.
- **Textos de Ctrl+F3.** Las explicaciones de Fluidez, Carga rápida y el interruptor JPEG ya lo dicen. El JPEG sigue
  apagado de fábrica, porque cambia algo la nitidez. Para un equipo muy débil, lo recomendado es **Fluidez con JPEG**.

## Cómo se comprobó

- **`hojeofino`** (nueva). Son 35 comprobaciones con tarjeta gráfica y 16 sin ella.
  - Lee el código compilado: la pared, el empujón y la rejilla ya no miden con `TickCount`.
  - Las curvas finas dan lo mismo que las de siempre con milisegundos enteros, y avanzan por en medio con fracciones.
  - Dos páginas seguidas corren el cielo 90 de 90 px, hacia los dos lados. Cuatro en ráfaga, 180 de 180 px.
  - Un empujón que nadie pidió a tiempo no se cobra de golpe después.
  - La rodilla:
    - es idéntica hasta el 75 %;
    - siempre crece;
    - nunca pasa del tope;
    - no tiene quiebre.
  - La lectura reutiliza el arreglo:
    - el contenido es el correcto;
    - los grandes van aparte;
    - cada hilo tiene el suyo;
    - una portada ya hecha no cambia al leer otra.
  - El colchón de cada perfil sale como en la tabla.
- **`anim`**, **`cielovivo`** y **`caratulasadaptativas`**: las explicaciones nuevas caben con las letras reales.
  `caratulasadaptativas` sigue cayendo en el mismo sitio que antes, ya conocido.
- **Dibujo por CPU:** idéntico al anterior, en los 14 métodos comparados.
- **Tanda completa (`pruebas/todo.sh`):** los mismos 26 problemas de siempre, ninguno nuevo. `hojeofino` pasa en los dos
  perfiles.

**No probado aquí:**

- **Que se vea fluido en el bar.** Aquí no hay Direct3D ni Windows. En Linux, `TickCount` ya va de milisegundo en
  milisegundo, así que el tirón del reloj no se reproduce: la causa sale de cómo funciona el reloj de Windows.
- **Si la lectura reutilizada quita pausas de verdad.** Solo se sabrá midiendo en el equipo del bar con Imagen →
  Medir.
- **Windows XP.**
