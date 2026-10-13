# ¿Qué partes del sonido pueden mejorar con IA? — Modest, 13 de octubre de 2026

Petición del usuario: «Revisa si algún apartado del sonido puede mejorarse con IA».

Esto es una revisión con propuestas. No se ha cambiado nada del programa.

**Actualización (13 de octubre):** el usuario eligió I1, I2 e I3, y están hechas; ver
[MEJORAS_IA_SONIDO_MODEST_2026-10-13.md](MEJORAS_IA_SONIDO_MODEST_2026-10-13.md). I4 e I5 no se hacen.

## El límite de partida

En XP con .NET 3.5 no hay ningún motor para ejecutar modelos de IA. Lo que se puede usar es lo que ya usa la rocola:

- **redes pequeñas escritas a mano** dentro del programa, que gastan muy poco;
- **entrenadas fuera**, en una PC, con el material que la propia rocola recoge de su biblioteca (`ia/`).

Nada de modelos grandes ni de internet.

## Lo que ya lleva IA

- **DSEE** (devolver los agudos a las copias pobres): una red entrenada con la biblioteca del dueño
  (`rocola_ia.bin`). Funciona en equipos de 2 núcleos o 2,2 GHz con 900 MB de memoria.
- **EQ automático**: si hay modelo entrenado (`rocola_eq_ia.bin`), una red decide cuánto grave (50 Hz) y cuánto aire
  (10 y 15,8 kHz) debería tener cada canción, mirando su cuerpo. Sin modelo, usa el promedio de la biblioteca.
- **Apollo** es solo de la rama HD, que está suspendida.

## Lo que se decide hoy con reglas fijas y podría aprenderse

Cada idea llevaría su interruptor en Ctrl+F3 si la apruebas, porque cambia lo que suena. Hay que escucharla en el bar
antes de dejarla encendida, y medir que el equipo de dos núcleos la aguante.

### I1. Bajo y brillo a la medida de cada canción — recomendada

**Hoy**, el bajo psicoacústico y el realce de agudos se reparten con reglas fijas:

- la canción se compara con el **promedio** de toda la biblioteca;
- «si viene recortada, ×0,75; si le faltan agudos, ×1,2».

Una balada de piano y un corrido se miden con la misma vara.

**Con IA**, la red del EQ automático ya sabe cuánto grave y cuánto aire tendría una canción **bien grabada que
sonara como esta**. Se usaría ese mismo objetivo para decidir cuánto bajo y cuánto realce le tocan:

- el EQ, el bajo y el realce empujarían hacia el mismo sitio;
- cuesta lo mismo que hoy, porque se calcula una vez por canción;
- no hay que entrenar nada nuevo, pero hace falta tener el modelo del EQ entrenado. Sin él, todo sigue como hoy.

**Riesgo:** bajo. El tope de 6 dB que ya protege el EQ serviría también aquí.

### I2. Quitar el siseo de las grabaciones viejas

Muchas cumbias, boleros y pasados de casete traen un siseo de fondo que, con el realce, se nota más.

**Con IA:**

- en el repaso a fondo, la rocola aprendería **el ruido de fondo de cada canción** en sus pasajes más callados;
- al sonar, lo bajaría con suavidad solo donde el siseo manda;
- una red pequeña distinguiría el siseo de los platillos, para no apagar la música.

**Coste:** del estilo del DSEE (aquí, unos 10 ms por segundo de canción). Hay que medirlo en el equipo del bar.

**Riesgo:** medio. Si se pasa, deja un sonido «acuoso». Iría apagado de fábrica.

### I3. Un DSEE más fino

La red del DSEE mira cada trozo de canción por separado. Entrenada con un poco de contexto (el trozo anterior) o
separando por género, devolvería agudos más naturales.

- Se entrena en la PC con el material que ya se recoge.
- El entrenador ya tiene sus guardas: no reemplaza el modelo instalado si sale peor en la prueba.

**Coste:** algo más que hoy en el equipo del bar.

**Riesgo:** bajo. Va detrás de las guardas del entrenador y de la escucha.

### I4. Finales con aplausos o charla

Hoy se salta lo que está **mudo de verdad** (por debajo de −60 dB). No se saltan los aplausos y el público del final de
una grabación en vivo, ni los finales que se apagan muy despacio (eso era la propuesta A5 de la décima tanda).

Una red pequeña reconocería «esto ya no es la canción» y empezaría el fundido antes.

**Riesgo:** alto. Cortaría música pagada si se equivoca. Además, para entrenarla hacen falta ejemplos marcados a mano
(«aquí acaba la canción»), y eso no se puede sacar solo de la biblioteca. **No la recomiendo de primeras.**

### I5. Claridad que sigue a la voz

«Claridad para el local» realza la voz siempre que está encendida. Con IA se aplicaría **solo cuando alguien canta**,
y no en los solos de acordeón o de trompeta, donde puede sonar áspera.

**Riesgo:** medio-alto. Igual que I4, hacen falta ejemplos con voz y sin voz marcados, y la rocola no los tiene.

## Lo que no necesita IA (o no la merece)

- **Nivelar el volumen:** ya usa la medida estándar de sonoridad (BS.1770). Una IA no la mejoraría.
- **Mezcla estilo DJ (A3):** encontrar los compases es tratamiento de señal clásico, no IA.
- **Subir el volumen cuando hay más gente:** necesita un micrófono en el bar. La mayor dificultad es que el micrófono
  oye a la propia rocola, y eso tampoco lo resuelve la IA.

## Recomendación

1. **I1** primero: ganancia clara, coste cero y riesgo bajo, sobre una IA que ya existe.
2. **I2** después, si en el bar hay muchas grabaciones viejas con siseo.
3. **I3** cuando haya material nuevo para reentrenar.

I4 e I5 solo si se pueden conseguir ejemplos marcados.
