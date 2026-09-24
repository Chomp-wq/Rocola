# Estrella del icono y color fluido — 22 de septiembre de 2026

**Nota del 23 de septiembre:** este documento describe la implementación anterior.
La [corrección posterior](CIELO_ESTRELLA_2026-09-23.md) devuelve el cielo azul/negro,
limita el flujo al espectro y la estrella, suaviza su velocidad y elimina los
tres acentos fijos de la estela. Es la referencia vigente para esos apartados.

Actualiza el diseño de la estrella y de sus ecos anteriores. Compartido por HD y
Modest, exclusivamente en GPU. No modifica el renderizador CPU, la cadena de
audio, las carátulas originales, el icono de la aplicación ni los datos del usuario.

## Estrella de cinco puntas

Se reconstruye la forma de `rocola.ico` con diez caras triangulares, contorno,
caras plateadas/azul acero y dos destellos. El cuerpo es opaco; solo sus bordes,
halo y ecos llevan transparencia. La textura de 256 × 256 se prepara una vez por
estilo y se reutiliza; no se necesita un archivo externo durante la reproducción.

La estrella recorre los dos lados de la zona superior. Su trayectoria combina
dos fases continuas, con margen para el halo y sin entrar en la barra de estado.
No salta a una posición diferente al cambiar la canción o entrar/salir del reposo.

## Respuesta musical y ecos

Se reutilizan los niveles graves, medios y agudos que la rocola ya calcula.
La energía y los ataques aumentan la velocidad y la intensidad del rastro;
medios y agudos también matizan el recorrido vertical. Al navegar, el movimiento
baja hasta el 18 % del régimen de reposo y los ecos se desvanecen.

El género solo modera la respuesta: rock, metal y electrónica pueden acentuarla;
ambient, baladas, clásica y jazz la suavizan. Un género ausente usa un perfil
neutral. El sonido manda: incluso un género enérgico se calma durante el silencio.
No se separan instrumentos con IA ni se alteran muestras de audio.

Hasta **12 imágenes residuales**, con silueta completa y colores mezclados con
cian, violeta y rosa. El intervalo de captura baja de 140 a 70 ms con la energía;
su opacidad aumenta y cada eco caduca a los 1,8 segundos. Las posiciones y los
colores quedan guardados al capturarse, evitando estelas que cambien de lugar
o de color retrospectivamente. Se limpian ante pausas, cambio de tamaño o parada GPU.

La respuesta musical no depende de tener activada la aurora.

## Flujo de color compartido

Una paleta circular de 64 muestras desplaza suavemente los colores de portada,
género o su mezcla. Conserva los pesos existentes, la regla de disco completo
cuando no hay género y las excepciones ya existentes de artista/atracción.
El cierre de la paleta interpola el último color con el primero, sin cortes.

Comparten el flujo:

- El degradado del cielo, manteniendo su oscuridad.
- Las nebulosas y el velo de la Vía Láctea.
- La cortina, contorno, horizonte y suelo de la aurora.
- Las estrellas pequeñas, fugaces, estrella principal, corona y polvo orbital.

El flujo cambia colores, no las frecuencias ni la posición de los picos de audio.
La fase es continua al cambiar la intensidad; no se hacen rotaciones instantáneas
de la paleta. La interfaz, letras y carátulas no se recolorean por este efecto.

## Controles visuales

Ctrl+F3 → Fondo:

- **Estrella musical con órbitas (GPU)**: activa el nuevo recorrido musical. Desde el 24-09-2026 se llama **Estrella
  viajera con órbitas (GPU)** y ya no sigue la música: va a ritmo fijo (ver `ESTRELLA_3D_2026-09-23.md`).
- **Ecos de color de la estrella (GPU)**: activa el rastro reforzado.
- **Colores del fondo en movimiento (GPU)**: nuevo interruptor, activado por
  defecto y persistente mediante `colores_fluidos`. Al apagarlo, el color vuelve
  suavemente al reparto fijo anterior.
- Los controles existentes de detalle, corona, polvo y protagonismo se conservan.

Solo se añade el interruptor visual de color; no hay un interruptor de rendimiento.

## Presupuesto y verificaciones

Historial fijo de 12 elementos, un lote de 72 vértices para todos los ecos,
tabla cromática fija y mallas reutilizables. El cielo usa 96 vértices y cada
nebulosa/velo 144, manteniendo un envío por capa. No hay imágenes nuevas,
lecturas de disco, trabajadores ni arreglos nuevos por cuadro en estas rutas.

`pruebas/estrellaflujo.cs` comprueba la silueta, alcance y límites a diferentes
resoluciones, suavidad temporal a 24–240 Hz, silencio, género, regreso a navegación,
colores variables con y sin portada, alfa, guardado y límites de todos los controles
de Fondo. Presenta cuadros en Direct3D 9 HAL y verifica cambios reales de color en
los vértices del cielo, estrellas, nebulosas y aurora, conservando las texturas y
el espectro. Compara el tiempo de trabajo local con/sin flujo en un panel sintético
de 960 × 600: no es una medición del programa completo ni de un equipo de dos núcleos.

`pruebas/validar-capas.ps1` compila GPU/CPU y ejecuta **31 baterías**. Las
compilaciones y pruebas se hacen en carpetas temporales, sin reemplazar ejecutables
de producción. Modest mantiene .NET 3.5/XP; no se ha comprobado en un XP real.
Queda la valoración visual y de rendimiento con música en la pantalla del usuario.

### Resultado de esta pasada

- HD: compilaciones Release/GPU/CPU correctas y 31 baterías aprobadas.
- Modest: compilaciones Release/GPU/CPU correctas; las pruebas de esta mejora
  (`estrellaflujo`, `estelaestelar`, `lunadetalle` y `espacioprecarga`) pasan.
- Al integrar el cambio de audio `1ba6f56` de Modest aparece un fallo en
  `eqpreparado`: comparación exacta del PCM, 22050 Hz/mono, cambio 0, muestra 54.
  Se reproduce compilando una copia limpia de ese mismo commit, sin esta mejora
  visual, con el compilador .NET 3.5 de Windows. No se ha debilitado la comparación
  ni cambiado el procesamiento de audio para esconderlo. Queda pendiente de
  investigación en el trabajo de audio.

Medición orientativa local del panel sintético, excluyendo Present: HD
0,186–0,201 ms/cuadro sin flujo y 0,257–0,260 con flujo; Modest
0,277–0,286 sin flujo y 0,349–0,355 con flujo. No incluye biblioteca/reproducción,
ni garantiza esos tiempos en otro procesador o resolución.
