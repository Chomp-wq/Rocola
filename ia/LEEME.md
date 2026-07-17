# IA propia de la rocola (modelo entrenado por el dueno con su biblioteca)

- `rocola_ia.bin` es la red de envolvente espectral ENTRENADA CON LA MUSICA DEL LOCAL
  (misma red y formato RIA1 que en la rama rocola-hd; paridad C#<->numpy verificada: 2e-6).
- Para activarla: copia `rocola_ia.bin` JUNTO a `Rocola.exe` y reinicia. En Ctrl+F3, la nota
  del reconstructor dira "con IA PROPIA". Sin el archivo, el DSEE usa su curva clasica (cero cambio).
- Solo actua con el DSEE espectral encendido y sobre canciones con agudos cortados; la
  inferencia son ~4,300 multiplicaciones por ventana: corre sobrada hasta en el CPU de esta rocola.
- `ia_vectores_prueba.txt` son los vectores de paridad de ESTE modelo (los usa la bateria de pruebas).
- Para RE-entrenar con mas musica: rama rocola-hd, `ia\entrenar_con_mi_musica.bat`.
