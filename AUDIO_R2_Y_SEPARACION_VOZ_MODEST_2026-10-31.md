# El pre-eco (R2), los huecos (R1) y la separación de voz con IA en Rocola OS — 31 de octubre de 2026

Petición del dueño: «Haz todo y revisa si en Rocola OS es lo suficientemente ligero para correr lo de separación de voz con IA en 2 o
máximo 4 núcleos». «Todo» se refiere a las propuestas de [AUDIO_P1_D1_D2_Y_TECNICAS_MODEST_2026-10-30.md](AUDIO_P1_D1_D2_Y_TECNICAS_MODEST_2026-10-30.md):
medir el pre-eco y los huecos en el repaso, R2 (quitar el pre-eco) y R1 (rellenar los huecos).

Todo lo medido es en Linux (Mono para la rocola y PyTorch en esta máquina para la IA). Nada se oyó en el billar ni se probó en el A4.

## 1. El pre-eco: medido y quitado (R2)

### La medida, en el repaso de la biblioteca

Sin el original no se puede restar nada, pero el pre-eco deja una huella clara: **el golpe tarda en subir**. `MedidorPreEco`
(`Rocola/PreEco.cs`) mira los agudos en bloques de 1 ms y mide cuánto tarda cada golpe en subir los últimos 12 dB. Lo hace de paso,
mientras el repaso mide la sonoridad: es el **algoritmo 7** de `rocola_sonoridad.txt`, con dos columnas nuevas (la mediana en ms y
cuántos golpes).

Medido con dos MP3 de verdad de los mismos golpes secos, descomprimidos con NLayer (`pruebas/preeco_lame.mp3` y
`pruebas/preeco_shine.mp3`):

| MP3 | lo que tardan en subir los golpes | ¿trae pre-eco? |
|---|---|---|
| LAME 128 kbps (el codificador bueno) | 2 ms | no |
| shine 128 kbps (sin bloques cortos, como muchos MP3 viejos) | 11 ms | sí |

Una canción «trae pre-eco» si se midieron al menos 8 golpes claros y tardan 7 ms o más. Sin golpes (un tono, una balada sin
batería) no opina.

**Ojo:** al subir el algoritmo a 7, la tanda larga **vuelve a medir toda la biblioteca**, como pasó con el 5 y el 6. Mientras tanto
las medidas del 6 siguen nivelando y quitando el zumbido igual. Un ejecutable anterior guarda las del 7 como ajenas, sin tocarlas.

### Quitarlo

- **Interruptor nuevo** en Ctrl+F3 → Sonido → Ajustes del sonido → Lo que suena ahora: **«Quitar el pre-eco de los MP3 viejos»**
  (`quita_preeco=`). Va **apagado de fábrica**.
- Solo actúa en las canciones medidas con pre-eco. En las demás ni se pone: salen bit a bit como antes.
- La etapa mira 64 ms por delante. En cada golpe busca dónde empieza de verdad, y en los 26 ms de antes baja los agudos (de 700 Hz
  hacia arriba) hasta el fondo que había. Lo que ya sonaba sigue sonando; solo se quita lo que sobra.
- Medido con la etapa de la rocola contra el original: con shine, el pre-eco que se oye (de 4 a 20 ms antes de cada golpe) pasa de
  **−9,0 a −18,5 dB**. Los golpes vuelven a subir en 4 ms en vez de 11, y el golpe en sí no cambia (−0,03 dB).
- «Escuchar sin mejoras» la deja pasar. Apagarla se oye al instante; **encenderla se nota desde la siguiente canción** (la etapa
  retrasa 64 ms y no se puede meter a media canción).
- Riesgo: un redoble con dos golpes a menos de 26 ms puede perder algo del primero. Por eso va solo en las canciones medidas con
  pre-eco y empieza apagada. Mi consejo: déjala apagada hasta que el repaso diga cuántas canciones lo traen (abajo).
- Para la ventana tuve que reacomodar la tarjeta de los arreglos de la grabación: las cinco filas caben sin cambiar nada más.

### Cuántas canciones lo traen

Cuando el repaso haya vuelto a medir la biblioteca, `rocola_sonoridad.txt` traerá la columna del pre-eco de cada canción.
`Recoger_sonido_XP.cmd` ya la copia. Mándamela y te digo cuántas lo traen y cuáles. Si son muchas, vale la pena encender el
interruptor (o hacerlo automático en el modo billar).

## 2. Los huecos (R1): no lo hice, y por qué

Para rellenar un hueco hay que saber **dónde** está. Lo intenté de tres formas sin el original:

- **Caídas por bandas de un tercio de octava:** no ve nada. Los huecos de un MP3 miden unos 80 Hz de ancho y uno o dos cuadros de
  largo, y se pierden en la media de la banda.
- **Celdas que «parpadean»** (están, faltan y vuelven): separa mal. Da 0,26 % en el original y 1,1 % en el codificador malo, y
  LAME a 128 kbps sale peor que a 96, al revés de la realidad.
- **Mirando la profundidad frente a lo de alrededor:** los huecos no son más hondos que los valles normales entre armónicos.

La forma buena sería preguntárselo al descompresor, que sabe qué bandas vinieron vacías. Así lo hacen el AAC y el MPEG-H con su
«relleno de ruido». Pero en Windows XP los MP3 los descomprime el descompresor de Windows (ACM), que no lo dice. NLayer (el
descompresor en C# que ya lleva la rocola) sí lo sabría, pero hoy solo se usa en las canciones que llegan al máximo, y cambiarlo
para todas es un trabajo grande con riesgo (el A4 tiene que aguantarlo).

**En Rocola OS sí tiene sentido:** allí la rocola descomprimirá con su propio descompresor y el relleno se puede hacer dentro, con
el dato exacto. Lo dejo apuntado para la etapa 2. Rellenar a ciegas en XP sería adivinar, y un relleno equivocado mete ruido en
canciones buenas.

## 3. La separación de voz con IA en Rocola OS

### Para qué serviría en el bar

No para karaoke: para **subir la voz 2 o 3 dB** sobre la música cuando el bar está ruidoso. Para eso no hace falta una separación
perfecta. Se le suma a la mezcla un poco de la voz que encuentra el modelo (subir 3 dB es sumar un 41 %), así que sus errores entran
muy por debajo de la música. Un modelo ligero alcanza.

### Lo que cuesta, medido

En esta máquina (Xeon a 2,1 GHz con AVX-512, PyTorch en CPU), con la arquitectura real de cada modelo y pesos al azar (cuesta lo
mismo que con los pesos buenos), 20 s de música estéreo:

| modelo | qué saca | 1 hilo | 2 hilos | 4 hilos |
|---|---|---|---|---|
| Open-Unmix UMX-HQ (512) | la voz | 0,038 s por s de música | 0,021 | 0,014 |
| Open-Unmix UMX-L (1024) | la voz | 0,083 | 0,038 | 0,021 |
| HTDemucs (27 M de parámetros) | 4 fuentes | 0,98 | — | 0,37 |

### Lo que costaría en el A4

Esto es una estimación, no una medida. El A4-6300 son dos núcleos que comparten una sola unidad de coma flotante de 128 bits, sin
AVX2. Por hilo calculo entre 3 y 5 veces más lento que esta máquina en Open-Unmix (redes pequeñas que esperan más a la memoria que
al cálculo), y entre 5 y 8 veces en HTDemucs (cálculo puro, donde el AVX-512 de aquí pesa mucho).

| modelo | en el A4 (2 núcleos) | con 4 núcleos (A10-6800K o A8-6600K) |
|---|---|---|
| UMX-HQ | **25 a 40 s por canción** de 3,5 min, con un solo núcleo | igual de rápido; deja más libre |
| UMX-L | 50 a 85 s por canción, un núcleo | lo mismo |
| HTDemucs | **10 a 17 min por canción** con los dos núcleos | 5 a 9 min |

Lo que dice la tabla:

- **UMX-HQ cabe de sobra con 2 núcleos.** Mientras suena una canción, la siguiente de la cola se separa en un núcleo en menos de un
  minuto, y el otro núcleo queda para la pantalla y el sonido. Su red recorre la canción en los dos sentidos, así que no se hace
  sobre la marcha sino un poco antes, por trozos de unos 30 s. Una canción puesta sin cola empezaría a sonar normal y la voz al
  frente entraría a los pocos segundos.
- **HTDemucs no da para la cola** ni con 4 núcleos. Solo serviría separando de noche, con el bar cerrado: unas 40 a 70 canciones por
  noche con 2 núcleos y el doble con 4. Alcanzaría para las más pedidas en unas noches, no para toda la biblioteca.
- **4 núcleos no son necesarios** para la separación ligera. Ayudan con HTDemucs y dan holgura, pero la decisión de cambiar el
  procesador no depende de esto.
- Memoria: UMX-HQ pesa unos 9 MB y la voz de una canción en RAM unos 20 MB. Sin problema con 3,5 GB.

### Las licencias (importante en un negocio)

- **UMX-HQ:** figura con licencia MIT, que permite el uso comercial ([Open-Unmix](https://sigsep.github.io/open-unmix),
  [pesos en Hugging Face](https://huggingface.co/teamup-tech/Open-Unmix-weights)). Es el candidato.
- **UMX-L:** sus pesos son **solo para uso no comercial** (CC BY-NC-SA 4.0, según su [página de PyPI](https://pypi.org/project/openunmix/)).
  No sirve para el bar.
- **HTDemucs:** el código es MIT, pero sus pesos no están claros: el autor dijo que son para investigación, y nadie de Meta ha
  respondido sobre el uso comercial ([discusión](https://huggingface.co/adefossez/HTDemucs/discussions/1),
  [otra](https://huggingface.co/adefossez/HTDemucs-ft/discussions/1)). Sin una autorización por escrito, no lo usaría en el bar.

Esto no es un consejo legal.

### Mi propuesta para la etapa 2 de Rocola OS

**«Voz al frente»**, con su interruptor:

- UMX-HQ escrito en C# dentro de la rocola, como las IA que ya tiene: una FFT, tres capas de memoria a corto plazo (LSTM) y unas
  multiplicaciones de matrices.
- Separa la voz de la siguiente canción de la cola en un núcleo y la sube 2 o 3 dB al sonar.
- Antes de decidir nada: compararla a ciegas en el bar, como la presencia.

En Windows XP no lo propongo: el A4 con XP ya va justo, y esto es para cuando la rocola corra en Linux.

## 4. Pruebas

- Batería nueva `preeco` (19 comprobaciones), con los dos MP3 de verdad: la medida (shine sí, LAME no, sin golpes no opina, cualquier
  tamaño de lectura), la tabla del algoritmo 7 (y que las del 6 se siguen leyendo), la etapa (los golpes vuelven a subir de golpe, el
  golpe no cambia, bit a bit con Pasa o sin golpes, mono, lecturas de cualquier tamaño) y el interruptor.
- `silencios`, `allimite` y `zumbido` al día con el algoritmo 7.
- `compara-cadena` contra el ejecutable anterior: 16 de 16 combinaciones iguales bit a bit con todo de fábrica.
- La suite entera (`pruebas/todo.sh`): 380 baterías, todas en verde.
