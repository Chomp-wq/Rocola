# IA propia de la rocola (modelo entrenado por el dueno con su biblioteca)

## Actualización de calidad — 22 de septiembre de 2026

El entrenador DSEE conserva el mejor checkpoint evaluado y solo publica si mejora
al menos 5% el MSE global frente a su curva fija y ninguna banda empeora más de 10%.
El código 2 significa rechazo por calidad; se conserva el modelo anterior.
Esto no sustituye validación por canciones independientes ni escucha a volumen igualado.

Copiar la carpeta de herramientas completa: `entrenar_ia.py` ahora necesita
`calidad_dsee.py` a su lado. Esta dependencia es del entrenador, no del reproductor.
Los modelos existentes no necesitan convertirse ni se reemplazan automáticamente.
AutoEQ acepta también las huellas de 12 campos y normaliza usando solo entrenamiento.

Ver [implementación, pruebas y pendientes detallados](../MEJORAS_AUDIO_Y_PENDIENTES_2026-09-22.md).

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


- `rocola_ia.bin` es la red de envolvente espectral ENTRENADA CON LA MUSICA DEL LOCAL
  (misma red y formato RIA1 que en la rama rocola-hd; paridad C#<->numpy verificada: 2e-6).
- Para activarla: copia `rocola_ia.bin` JUNTO a `Rocola.exe` y reinicia. En Ctrl+F3, la nota
  del reconstructor dira "con IA PROPIA". Sin el archivo, el DSEE usa su curva clasica (cero cambio).
- Solo actua con el DSEE espectral encendido y sobre canciones con agudos cortados; la
  inferencia son ~4,300 multiplicaciones por ventana: corre sobrada hasta en el CPU de esta rocola.
- `ia_vectores_prueba.txt` son los vectores de paridad de ESTE modelo (los usa la bateria de pruebas).
- Para RE-entrenar con mas musica: rama rocola-hd, `ia\entrenar_con_mi_musica.bat`.

## El contrato (RIA1) — no romperlo

Manda la ROCOLA: la red se aplica en `SpectralTap.RegenNeural` y la cosecha sale de `MakePair`.
`contrato_dsee.py` transcribe esas funciones y `BuildIaFeatures` paso a paso, con su mismo redondeo
float32, y es lo que usa `entrenar_ia.py` (version del extractor `EXTRACTOR = 2`).
- entrada[0..23]: log10 de la energia media por bin (|X/N|^2) de 24 bandas log de 200 Hz al corte,
  restada su media; [24] = esa media; [25] = corte/20000 **con el corte redondeado a bin**;
  [26] = planitud (ultimas 8 bandas); [27] = 0.
- salida[0..11]: log10(energia objetivo / energia de la costura) de 12 sub-bandas log del corte al
  `topBin` de la rocola: el menor entre 20,5 kHz, 95 % de Nyquist, 2·corte−2 bins y ny−4.
  Topes −4 y **+0,1**. Si una sub-banda queda vacia el par no vale (como en la rocola).
- Ventana raiz-de-Hann, cuadro 2048 (en esta rama es fijo: cosecha y reproduccion), solape 50 %.
  El contrato cubre tambien 4096 porque lo usa la rama HD; aqui esos casos se saltan diciendolo.

La version 1 (el extractor Python anterior) usaba 20 kHz de techo, corte sin redondear y tope +0,5:
un modelo entrenado con `--musica` o con audio sintetico aprendia bandas que no son las que la
rocola rellena. Lo que junta la propia rocola nunca tuvo ese problema: `MakePair` no ha cambiado
desde que existe (3 de agosto de 2026). Los modelos RIA1 instalados siguen funcionando igual: la
reproduccion no cambio.

`paridad_extractor.txt` son los vectores de paridad del extractor (28 entradas y 12 objetivos para
espectros y senales reproducibles bit a bit). `pruebas/iaparidad.cs` los compara con la rocola y
ademas recorre TODOS los cortes a seis frecuencias de muestreo contrastandolos con este contrato.
Si se cambia el contrato a proposito: `python3 contrato_dsee.py --generar-paridad paridad_extractor.txt`
y subir `EXTRACTOR`.

## Validacion de verdad: de que cancion viene cada ejemplo

La rocola escribe, junto a `rocola_ia_datos.bin`, el archivo `rocola_ia_datos.idx`: de que cancion
(y de que carpeta) viene cada ejemplo, solo como huellas. **Traete los dos.** Con el, el entrenador
separa por cancion/carpeta ANTES de mirar un cuadro: entrenamiento, validacion (elige el mejor
checkpoint) y prueba (decide si se publica). Ningun grupo esta en dos sitios.

Sin el `.idx` (todo lo cosechado antes de esta version) no se inventan grupos: se entrena para mirar,
pero sale el aviso VALIDACION NO INDEPENDIENTE, el codigo es 3 y no se reemplaza ningun modelo.
Opciones: `--agrupar artista|carpeta|cancion` (defecto artista) y `--incluir-sin-origen` (los suma
solo al entrenamiento; el resultado tampoco se publica). Con `--musica`, cada canal de cada WAV da
sus propios ejemplos (una mezcla mono anulaba lo que estuviera en contrafase) y los dos canales
van siempre juntos.

## Cuando se reemplaza un modelo (y cuando no)

El candidato se mide sobre la particion de PRUEBA contra la curva fija y contra el modelo que ya
tienes (arrastra tu `rocola_ia.bin` junto al material, o se usa el de esta carpeta). Solo se guarda si
gana a la curva (5 % global, ninguna banda 10 % peor) y al instalado (2 % global, ninguna banda 10 %
peor). Es una politica inicial para no publicar regresiones, no una prueba de que suene mejor: eso se
decide escuchando A/B a volumen igualado. Cada entrenamiento deja `informe_dsee.json` (sin rutas ni
fechas: la misma entrada da el mismo informe) y los vectores llevan la huella SHA-256 de su modelo.
Codigos: 0 guardado, 2 no supera la politica, 3 no verificable (sin validacion independiente).

## Publicar sin dejar nunca un par a medias

Cuando un modelo SI se guarda, modelo, vectores e informe se publican juntos como una version en
`modelos/dsee-<huella>/` con su manifiesto, y se activa cambiando un solo archivo (`modelos/ACTIVO`).
`rocola_ia.bin` y `ia_vectores_prueba.txt` de esta carpeta (y de `entrenado/`) son copias de la
version activa: si un corte de luz dejo una a medias, se rehacen solas al empezar el siguiente
entrenamiento. La primera vez, el modelo que ya tenias se archiva como version `previo-...`.
`python publicar_dsee.py --estado`, `--recuperar` o `--activar dsee-XXXX` para volver a una anterior.

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
