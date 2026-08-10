# IA propia de la rocola (rama rocola-hd)

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
