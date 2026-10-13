# Mejoras con IA en el sonido: I1, I2 e I3 — Modest, 13 de octubre de 2026

Petición del usuario: «Haz todas menos I4 e I5, y busca más mejoras de audio, con o sin IA». Las propuestas están en
[IA_EN_EL_SONIDO_MODEST_2026-10-13.md](IA_EN_EL_SONIDO_MODEST_2026-10-13.md).

Las tres cambian lo que suena. Por eso cada una lleva su interruptor en **Ctrl+F3 → Sonido → Mejoras con IA**, y van
**apagadas de fábrica**: hay que escucharlas en el local antes de dejarlas encendidas. Con las tres apagadas, la cadena
de sonido da exactamente los mismos bytes que antes: lo comparé con la versión publicada en canciones enteras, con y
sin silencios, en los tres modos (de fábrica, todo apagado, sin saltar silencios).

## La hoja nueva

En la tarjeta «Calidad del sonido», el renglón del botón «El local y sus bocinas…» se reparte con uno nuevo,
**«Mejoras con IA…»**. Abre una hoja con dos tarjetas:

- **Mejoras con IA**: los tres interruptores, cada uno con su explicación.
- **Los modelos**: si está cargada la IA del EQ, el modelo general del DSEE y cuántos modelos hay por género, y dónde se
  entrenan. Abajo, «Volver a las canciones».

## I1. Bajo y brillo a la medida de cada canción

**Antes**, el bajo psicoacústico y el realce comparaban cada canción con el **promedio** de toda la biblioteca.

**Ahora**, con el interruptor y el modelo del EQ cargado (`rocola_eq_ia.bin`), la comparan con **el objetivo de esa
canción**: cuánto grave (50 Hz) y cuánto aire (10 y 15,8 kHz) tendría una canción bien grabada con su mismo cuerpo. Es
el mismo objetivo que ya usaba el EQ automático.

- **Bajo:** la misma cuenta de siempre, con esa vara en lugar del promedio.
- **Brillo:** si le falta aire frente a su objetivo (en lo que su archivo conserva), más realce; si le sobra, menos.
  Como mucho un 25 % arriba o un 20 % abajo.
- **Seguro:** el objetivo de la red no se aleja más de 6 dB del promedio, como en el EQ.
- **Sin modelo del EQ**, o apagado: todo como antes.
- **En vivo:** al cambiar el interruptor, el bajo de la que suena y el de la siguiente se recalculan al instante.

Coste: ninguno que se note; se calcula una vez por canción.

## I2. Quitar el siseo de las grabaciones viejas (`Siseo.cs`)

Baja **hasta 6 dB** el ruido de fondo fijo de los agudos (de 3 kHz para arriba), solo en los momentos en que la música
está cerca de ese piso.

**Cómo reconoce el siseo:** por cada banda de agudos apunta lo más bajo que llegó en cada trozo de 0,75 s. Hay siseo si
se cumple todo esto:

- en los últimos cuatro trozos (3 s) ese mínimo fue casi el mismo, dentro de 3 dB: el siseo no sube y baja con la
  música;
- está entre −90 y −40 dB (más abajo es silencio limpio; más arriba, música);
- la banda pasó al menos la mitad del último trozo pegada a ese piso;
- pasa en casi todas las bandas a la vez, porque el siseo es de banda ancha.

**Cuidados:** la rebaja baja despacio y vuelve de golpe, así que un golpe de platillo pasa entero. Se calcula solo lo
que se quita y se resta de la señal original, así que **lo que no toca sale con los mismos bits**.

**Retraso:** 11,6 ms, que la cadena cuenta como el de las demás etapas. En el relevo sin pausa entre pistas de un
disco se descarta con el resto del cebado.

**Medido aquí, con señales sintéticas:**

| | Agudos al entrar | Al salir |
| --- | --- | --- |
| Música limpia | — | **sin tocar ni un bit** |
| Siseo a −55 dB, en la coda suave | −65,0 dB | −69,6 dB |
| Siseo a −65 dB, en la coda suave | −75,0 dB | −79,6 dB |
| Con la banda sonando fuerte | −41,5 dB | −41,5 dB |

**Coste:** unos 8 ms por segundo de canción aquí. Los dos canales van en una sola transformada de Fourier; al
principio eran 15 ms.

**Lo que hay que saber:**

- **No usa una red neuronal.** Para entrenar una que distinga siseo de platillos harían falta ejemplos marcados a
  mano, y no los hay. La hace la regla del piso fijo.
- **Aprende mientras la canción suena**, no en el repaso: así no hace falta guardar nada nuevo ni repasar la
  biblioteca otra vez. Los primeros 3 o 4 segundos de cada canción escucha y no toca nada.
- **Se mira al abrir cada canción:** se nota desde la siguiente.
- Música electrónica con un colchón de ruido constante podría confundirse con siseo (−6 dB como mucho, solo en ese
  colchón). Por eso va apagado y hay que escucharlo.

## I3. Modelos del DSEE por género

**En la rocola:** con el interruptor encendido, cada canción usa el DSEE entrenado para su género
(`rocola_ia_genero_<genero>.bin` en la carpeta de datos), si existe; si no, el general. Detalles:

- El nombre de archivo sale de la etiqueta de género del archivo, con la misma cuenta que usa el entrenador
  (comprobado con los mismos ejemplos en los dos lenguajes).
- Los modelos se cargan al arrancar; un archivo roto o de otro contrato no se carga.
- Solo se nota con el DSEE encendido, desde la siguiente canción.

**En la PC donde se entrena (`ia/`):**

- `entrenar_ia.py --genero cumbia` entrena solo con el material de las canciones de ese género. Las canciones siguen en
  su misma partición, así que la prueba es de canciones que el entrenamiento no vio. Lo compara con el modelo general
  en la prueba de ese género: **solo sale aceptado si le gana ahí**, con las mismas guardas de siempre.
- `entrenar_dsee.bat` ahora, después del general, saca solo un candidato por cada género con 60 canciones o más (los
  6 con más material). Probado de punta a punta con material inventado.
- `instalar_dsee_genero.py` lo instala, solo si se cumple todo esto:
  - el informe dice «aceptado»;
  - es de ese género;
  - la huella coincide;
  - se confirma con `--escuchado`.

  Si ya había uno, lo guarda al lado como `.anterior-N`. El general no se toca.

**Para que suene, hace falta entrenar** con el material del bar. Aquí no hay ese material: lo que se probó es que el
camino funciona, no que un modelo por género suene mejor.

## Más mejoras de audio: lo revisado

- **El retraso del quitasiseo en el relevo sin pausa:** va dentro del cebado que se descarta; no queda hueco.
- **Canciones con un canal al revés:** ya estaban cubiertas. El grave en mono vigila el lado y devuelve su grave si los
  canales van al revés.
- **La transformada del DSEE:** ya usa tablas precalculadas; no hay trabajo repetido que quitar.
- **El entrenamiento por género** era la pieza que faltaba para que I3 sirva sin escribir comandos: añadida.
- **Idea para después (no hecha):** que el quitasiseo herede lo aprendido de la pista anterior del mismo disco, para
  no esperar 3 s en las intros de las siguientes.

Junto con lo del 12 y 13 de octubre ([RENDIMIENTO_SILENCIOS_MODEST_2026-10-12.md](RENDIMIENTO_SILENCIOS_MODEST_2026-10-12.md)):
la cadena ya no se atasca en los silencios, el convertidor de frecuencia gasta un tercio menos y el repaso a fondo ya
no se atasca en los silencios.

## Cómo se comprobó

- **Batería nueva `mejorasia`** (CPU y GPU), 32 comprobaciones:
  - **I1:** sin modelo no cambia nada; con un modelo de prueba, el bajo y el brillo dan la cuenta esperada; el tope
    de 6 dB; el EQ no cambia; el cambio en vivo.
  - **I2:** música limpia con los mismos bits; el siseo baja entre 3 y 6 dB en la coda y la banda fuerte queda igual;
    mono; 6 canales tal cual; en la cadena solo con el interruptor, y su retraso contado.
  - **I3:** los nombres como en Python; solo se cargan los modelos válidos; la cumbia usa el de cumbia, otro género el
    general, y apagado el general; en la cadena, el DSEE lleva el modelo de su género.
  - Las tres, apagadas de fábrica.
- **`ventanaf3` ampliada:** abre la hoja nueva en la ventana real y mira que nada quede fuera, tapado ni cortado (con
  la letra de Linux y con una más ancha). También la miré en una captura.
- **Python:**
  - `test_genero_dsee.py` (nuevo): nombres, filtro por género, rechazo sin material de la rocola, instalador;
  - las otras 13 pruebas de `ia/` siguen pasando;
  - el orquestador, probado de punta a punta con material inventado.
- **Tanda completa (`pruebas/todo.sh`):** los mismos 26 problemas de siempre más uno nuevo, `bloqueos`. Avisaba de que
  la carga de los modelos por género leía la carpeta de datos en el hilo de la pantalla al arrancar. Se pasó a segundo
  plano (mientras tanto, cada canción usa el modelo general) y `bloqueos` y `mejorasia` vuelven a pasar. No volví a
  correr la tanda entera tras ese último cambio, que solo mueve esa carga de hilo.
- **Dibujo por CPU:** sin tocar.

**No es oírlo en el bar.** Las tres se probaron con señales sintéticas y modelos de prueba. Hay que escucharlas allí.
