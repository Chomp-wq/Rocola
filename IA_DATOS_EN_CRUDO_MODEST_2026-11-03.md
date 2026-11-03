# IA: aprovechar los datos de la rocola para entrenar mejor (propuesta, 3-11-2026)

**Elegidas: IA1 e IA2 (hechas, ver sistema/ETAPA2.md, decimosexta parte).**

Idea del dueño: que las IA aprovechen los datos en crudo que junta la rocola, junto con su propia red, para tener
más datos y mejores resultados. Esto es lo que hay hoy, lo que se midió y lo que se propone. **Nada hecho todavía:
falta que el dueño elija.**

## Lo que hay hoy

- **DSEE** (`rocola_ia.bin`, red 28 → 96 → 96 → 12): la rocola, mientras repasa la biblioteca, toma cuadros de las
  canciones que conservan sus agudos, les **simula** un corte en 5 sitios (11; 13; 15; 16,5 y 18 kHz) y guarda
  «lo que queda abajo / lo que había arriba» (`rocola_ia_datos.bin` + `.idx`). Ese archivo se lleva en USB a una PC,
  se entrena con `ia/entrenar_dsee.bat` y sale un **candidato** que no se activa sin escucharlo.
- **EQ automático** (`rocola_eq_ia.bin`): se entrena con `rocola_eq.txt` (la huella de cada canción sana).
- Guarda solo 28 + 12 números por cuadro, no el sonido: no se pueden sacar después pares nuevos de lo guardado.

## Lo que se midió (con una pieza sintética, no con música de verdad)

Una pieza de 60 s con notas armónicas hasta 20 kHz, platillos y bombo, pasada por MP3 de verdad (LAME 128 y 192 kbps),
y la red instalada aplicada a los mismos cuadros (contrato `contrato_dsee.py`, la cuenta de la rocola):

| | Error de la red con el corte simulado | con el MP3 de verdad |
|---|---|---|
| 128 kbps (corte ~16,7 kHz) | 2,109 | 2,073 (−2 %) |
| 192 kbps (corte ~18,8 kHz) | 2,074 | 2,090 (+1 %) |

- Lo que el MP3 cambia en lo que la red ve es pequeño (0,03 a 0,06 en log10, frente a 0,86 de variación normal
  entre cuadros): **simular el corte, como hace hoy, se parece bastante al MP3 de verdad**. Pasar la cosecha por
  un codificador real (LAME no está ni en el sistema) daría poco.
- El error grande (~2) es porque la red no conoce esa clase de sonido: **lo que más ayuda es más variedad de
  música de verdad**, no más copias del mismo cuadro.
- Con música real puede salir distinto (los huecos de LAME por encima de 16 kHz no salen en esta pieza): se puede
  repetir con canciones del bar.

## Lo que NO conviene: que la red se enseñe a sí misma

Usar lo que la propia red predice como si fueran datos nuevos (autoentrenamiento) no añade nada que no supiera: en
las canciones dañadas nadie sabe qué agudos había, así que la red copiaría sus propios errores y los reforzaría. Los
datos útiles son los de las canciones sanas, donde se sabe la respuesta.

## Propuestas

- **IA1 «Que la rocola entrene sola» (Rocola OS).** El entrenador, hoy en Python en otra PC, pasa al programa de la
  rocola (C#, .NET 10). Por la noche, con la prioridad más baja y solo si nadie la usa, entrena con **todo** lo
  cosechado. Usa las mismas reglas que hoy: separar por canción, ganarle a la curva fija y al modelo instalado, y
  dejar el resultado como candidato aparte. Se acaba el viaje en USB, y cada noche aprende de lo nuevo. **Activar**
  un candidato seguiría siendo cosa del dueño, después de escucharlo: por ejemplo, un botón «Probar la IA nueva»
  en Ctrl+F3 con la comparación a ciegas que ya existe, cuyos votos decidan. Coste: grande. Hay que medir en el A4
  cuánto tarda una noche de entrenamiento.
- **IA2 «Cortar donde corta tu música».** La rocola ya sabe dónde corta cada MP3 dañado de la biblioteca (la
  medida de calidad). Los 5 cortes fijos pasan a ser los que de verdad tiene la biblioteca (por ejemplo, 16 y
  19 kHz, que hoy no se simulan), y algunos más por cuadro. Más ejemplos justo donde se usan. Coste: pequeño;
  no cambia lo que suena; los datos viejos siguen valiendo.
- **IA3 «Los votos del dueño como datos».** «Comparar a ciegas» ya junta votos «me gusta» por tramo. Se guardan
  (canción, qué ganó) para que el entrenador del EQ sepa qué le gusta al dueño en ese local. Son pocos datos: sirve
  para afinar, no para entrenar desde cero. Coste: pequeño (solo guardar); usarlos, después.
- **IA4 «Probar con MP3 de verdad».** Repetir la medida de arriba con canciones reales del bar (las sanas, pasadas
  por MP3) antes de decidir si la cosecha debe usar un codificador real. Coste: pequeño; no cambia nada.

Recomendación: **IA2** (barata y segura) y **IA1** (la que de verdad aprovecha los datos, sin la USB). IA3 e IA4,
según se quiera.
