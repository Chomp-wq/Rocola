# IA propia de la rocola (modelo entrenado por el dueno con su biblioteca)

- `rocola_ia.bin` es la red de envolvente espectral ENTRENADA CON LA MUSICA DEL LOCAL
  (misma red y formato RIA1 que en la rama rocola-hd; paridad C#<->numpy verificada: 2e-6).
- Para activarla: copia `rocola_ia.bin` JUNTO a `Rocola.exe` y reinicia. En Ctrl+F3, la nota
  del reconstructor dira "con IA PROPIA". Sin el archivo, el DSEE usa su curva clasica (cero cambio).
- Solo actua con el DSEE espectral encendido y sobre canciones con agudos cortados; la
  inferencia son ~4,300 multiplicaciones por ventana: corre sobrada hasta en el CPU de esta rocola.
- `ia_vectores_prueba.txt` son los vectores de paridad de ESTE modelo (los usa la bateria de pruebas).
- Para RE-entrenar con mas musica: rama rocola-hd, `ia\entrenar_con_mi_musica.bat`.

## El EQ y el bajo van SOLOS (no hay perilla que ajustar)

De fabrica, en Ctrl+F3, el "EQ automatico" y los "Graves (bajo profundo)" llevan puesta la casilla
**Automatico**: las perillas quedan apagadas y lo decide el programa cancion por cancion, con lo que
el analisis del DSEE ya midio de cada archivo.

- **Fuerza del EQ**: sale de cuanto se puede FIAR uno del objetivo. Con menos de 10 canciones sanas
  medidas no corrige nada; segun el pre-analisis avanza sube del 31% al 75% (85% con el modelo
  `rocola_eq_ia.bin` cargado, porque entonces el objetivo es por cancion y no uno para todas). Nunca
  llega al 100%: siempre deja un resto sin corregir para no aplanar el caracter de cada disco.
- **Cancion rara**: si una se aleja muchisimo del resto de la biblioteca, casi seguro queria sonar
  asi (un disco muy oscuro, una grabacion en vivo). A esas se les toca menos, hasta la mitad.
- **Bajo profundo**: parte del 30% y lo ajusta el analisis. A una cancion con los picos recortados se
  le baja al 23% (mas grave solo haria trabajar al limitador y emborrona); a una copia pobre, con los
  agudos cortados, se le sube al 36% (esas suelen ir tambien flojas de abajo). Encima sigue actuando
  el ajuste por huella tonal de siempre, asi que el margen real va de ~12% a ~55% segun la cancion.

Se ve trabajando en la propia ventana: junto a cada casilla aparece el porcentaje que eligio el
programa y, en los graves, tambien lo que le toca a la cancion que esta sonando.

Quien prefiera mandar a mano solo tiene que quitar la casilla; la perilla vuelve a valer como siempre.
En el archivo de configuracion son `eq_automatico=` y `bajo_automatico=` (1 = automatico).

## Modelo del EQ AUTOMATICO (`rocola_eq_ia.bin`)

Es OTRA red, aparte de la del DSEE, y sirve para otra cosa.

- El EQ automatico corrige cada cancion hacia un objetivo. Ese objetivo era UNO para toda la
  biblioteca: el promedio. Con esta red el objetivo se calcula POR CANCION: mirando el cuerpo
  de su espectro (158 Hz - 5.6 kHz, lo que define de que musica se trata y lo que los codecs
  no destrozan) predice cuanto grave y cuanto aire tendria si estuviera bien grabada.
- Se entrena SOLO con las canciones sanas de tu biblioteca. Aprende de tus mejores grabaciones.
- Los datos ya existen: `rocola_eq.txt`, que la rocola escribe sola al pre-analizar. No hay que
  cosechar nada aparte.

Para entrenarlo:

    python3 entrenar_eq.py --datos rocola_eq.txt --salida rocola_eq_ia.bin

y se copia `rocola_eq_ia.bin` junto a `Rocola.exe`. Sin el archivo, la rocola usa el promedio
(exactamente como antes).

**El entrenador NO guarda el modelo si no le gana al promedio.** Antes de escribir nada compara
los dos sobre canciones que la red nunca vio y exige una mejora minima del 5%. Si tu biblioteca
no tiene bastante variedad para que la red aprenda algo, te lo dice y no guarda nada: es la unica
forma honesta de saber si aporta o solo esta memorizando.

Hacen falta al menos 200 canciones sanas analizadas para intentarlo.
