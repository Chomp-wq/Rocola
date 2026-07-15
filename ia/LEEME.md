# IA propia de la rocola (rama rocola-hd)

Una red neuronal CHICA (MLP 28 -> 48 -> 48 -> 12, ~4,300 pesos) **entrenada por nosotros** que
aprende a predecir la ENVOLVENTE de los agudos que el codec corto, viendo la forma del espectro
que sobrevivio. El reconstructor espectral de la rocola la usa en vez de su curva fija de
-9 dB/octava: platillos recuperan su brillo real, pasajes oscuros se quedan oscuros.

## Como se usa

1. **Entrenar** (requiere `python3` + `numpy`):
   - Prueba de concepto (audio sintetico):  `python3 entrenar_ia.py`
   - **Produccion (con TU musica)**:  `python3 entrenar_ia.py --musica /carpeta/con/wavs`
     (WAV 44.1 kHz / 16 bits; convierte MP3 con `ffmpeg -i cancion.mp3 cancion.wav`.
     Con unas 200-500 canciones variadas ya aprende bien.)
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
