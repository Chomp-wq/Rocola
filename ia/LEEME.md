# IA propia de la rocola (rama rocola-hd)

## LO CORTO: que archivos me llevo de la rocola y que hago con ellos

Junto a `Rocola.exe` hay dos archivos, y son los UNICOS que hacen falta para entrenar:

| Archivo | Que entrena | Tamano tipico |
|---|---|---|
| `rocola_ia_datos.bin` | el **reconstructor de agudos** del DSEE (`rocola_ia.bin`) | de cientos de MB a varios GB |
| `rocola_eq.txt` | el **objetivo del EQ automatico** (`rocola_eq_ia.bin`) | unos pocos MB |

No hace falta llevarse la musica, ni el disco de la rocola, ni ningun otro archivo.

Con los dos en una USB, en la PC donde entrenas: **arrastralos sobre `ia\entrenar_dsee.bat`**.
Hace todo solo y deja los modelos en `ia\entrenado\`. Esos dos son los que se copian de
vuelta junto a `Rocola.exe`. Se puede arrastrar uno solo (entrena esa mitad y avisa de lo
que falto) o la carpeta entera donde esten (los busca dentro).

Lo unico que necesita la PC es **Python + numpy** (`pip install numpy`).

> Esto es distinto de `entrenar_con_mi_musica.bat`, que esta mas abajo: aquel sale de la MUSICA
> (hay que tener la biblioteca a mano y tarda horas convirtiendo). Este sale de lo que la rocola
> ya midio sola mientras trabajaba, asi que solo se mueven dos archivos.

### Que NO se entrena, y por que

El resto de lo automatico no es una red: son medidas que la rocola ya lleva sola y reglas
sobre esas medidas. No hay nada que entrenar ni ningun archivo que traer.

- **Volumen parejo** (`rocola_volumen.txt`): se mide cancion por cancion en el pre-analisis.
- **Fuerza del EQ automatico** y **cantidad de graves**: salen de cuanto se puede fiar uno de
  lo medido (ver mas abajo). Suben solas segun avanza el pre-analisis.
- **Des-recorte, realce de agudos y fundido**: ajustes fijos, no aprendidos.
- **El acantilado** (donde el MP3 talo los agudos): lo detecta el propio DSEE al vuelo.

### De donde sale el material y como hacer que crezca mas rapido

`rocola_ia_datos.bin` lo va juntando la rocola sola mientras esta encendida y nadie la usa
(pre-analisis). Cada cancion se mira varias veces, y cada vez se toman cuadros de un sitio
distinto, asi que el archivo sigue creciendo aunque ya haya pasado por toda la biblioteca.

Si tienes prisa: en Ctrl+F3, **REPASO A FONDO**. Se dedica a esto sin esperar a que la rocola
este en reposo y saca de cada cancion unas ocho veces mas material. Dejalo una noche.


Una red neuronal CHICA (MLP 28 -> 48 -> 48 -> 12, ~4,300 pesos) **entrenada por nosotros** que
aprende a predecir la ENVOLVENTE de los agudos que el codec corto, viendo la forma del espectro
que sobrevivio. El reconstructor espectral de la rocola la usa en vez de su curva fija de
-9 dB/octava: platillos recuperan su brillo real, pasajes oscuros se quedan oscuros.

## Como se usa

1. **Entrenar con TU musica, facil**: doble clic a **`entrenar_con_mi_musica.bat`** (o arrastra
   tu carpeta de musica encima). Hace todo solo: recorre la biblioteca, junta 600 canciones de
   CALIDAD COMPLETA (las danadas no pueden ensenar: no tienen los agudos reales que la red debe
   aprender), las convierte y entrena. Tarda un buen rato la primera vez; es reanudable.
   A mano (requiere `python3` + `numpy`):
   - Prueba de concepto (audio sintetico):  `python3 entrenar_ia.py`
   - Con tus wavs:  `python3 entrenar_ia.py --musica /carpeta/con/wavs --clips 7200 --por_archivo 12`
     (WAV 44.1 kHz / 16 bits; `preparar_entrenamiento.py` los genera desde mp3/flac.
     Con unas 500-600 canciones variadas y BUENAS el modelo queda saturado: es chico.)
2. **Activar**: copia `rocola_ia.bin` JUNTO a `Rocola.exe` y reinicia. En Ctrl+F3, la nota del
   reconstructor dira "Con IA PROPIA". Sin el archivo, el reconstructor sigue con su curva
   clasica (cero cambio).
3. La IA solo actua cuando "Reconstruir agudos perdidos (DSEE espectral)" esta activado, y solo
   sobre archivos con acantilado (<19.3 kHz). Costo de inferencia: ~4,300 multiplicaciones por
   ventana de ~23 ms — despreciable.

## Que hay en esta carpeta

- `entrenar_ia.py` — generador de datos + extractor de pares + entrenamiento (backprop/Adam en
  numpy, sin dependencias pesadas) + exportador del formato `RIA1`.
- `rocola_ia.bin` — **prueba de concepto** entrenada con audio sintetico variado. Sirve para
  probar el mecanismo; para el mejor sonido entrena con tu propia biblioteca (`--musica`).
- `ia_vectores_prueba.txt` — vectores de paridad: la bateria de pruebas verifica que la red en
  C# calcule EXACTAMENTE lo mismo que la entrenada en numpy (peor diferencia medida: 2e-6).

## El contrato (RIA1) — no romperlo

La extraccion de caracteristicas de `entrenar_ia.py` y la de `Form1.cs` (SpectralTap.BuildIaFeatures)
deben coincidir EXACTO:
- entrada[0..23]: log10 de la energia media por bin (|X/N|^2) de 24 bandas log de 200 Hz al corte,
  restada su media; [24] = esa media; [25] = corte/20000; [26] = planitud (ultimas 8 bandas); [27] = 0.
- salida[0..11]: log10(energia objetivo / energia de la costura) de 12 sub-bandas log del corte a
  min(2*corte, 20 kHz). Ventana raiz-de-Hann, cuadro 2048/4096, solape 50%.

Resultados de la prueba de concepto (validacion): la red predice la envolvente real con
**mse 0.54** contra **1.53** de la curva fija (2.8x mejor).

## Upscaling offline (MP3 -> "FLAC"): carpeta `upscale/`

Para la meta de maxima calidad NO se procesa en la rocola: se mejora la biblioteca UNA VEZ,
offline, con Apollo (restauracion neuronal de musica) en un PC con GPU. El piloto A/B esta en
`upscale/` (ver su LEEME.md). La IA de envolvente de esta carpeta sigue siendo util para lo que
no se haya pre-procesado.
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
