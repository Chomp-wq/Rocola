# La prueba del importador y de lo grande (grupo xp-datos)

`prueba.sh PROGRAMA [importa usa material]` prueba `rocola-xp --importa-xp` (`Nativo/ImportaXp.cs`) y la carpeta de lo grande
(`Nativo/DatosGrandes.cs`: el material de la IA y las miniaturas en `ROCOLA_IA_DATOS`), con datos **inventados**: unos discos falsos con
música generada por ffmpeg (tonos, silencio y ruido blanco) y la carpeta de datos de una rocola de XP falsa (`mundo.py`: lo que mide
`rocola-xp` de esa música, con las rutas pasadas a «C:\\» y «D:\\» como las tendría XP, más todo lo que XP deja en su carpeta). Los
modelos de IA son los del repositorio (`ia/rocola_ia.bin`, `pruebas/datos/rocola_eq_ia.bin`).

- `PROGRAMA`: la carpeta con `rocola-xp.dll` (JIT, `bin/Release/net10.0`) o `aot:CARPETA` (NativeAOT).
- `W`: carpeta de trabajo, por defecto `/root/rocola-trabajo/act/xp-datos/importa` (se rehace en cada corrida; unos 10 MB).
- Hace falta ser root (`unshare -m` y `mount`: los discos falsos se montan en **solo lectura** mientras se importa, como los monta
  `rocola-discos`; y un tmpfs de 200 MB para «poco sitio»), ffmpeg y python3.

Partes (sin nombrarlas, las tres; unos 3 a 4 minutos con la máquina libre):

- **importa**: tabla por tabla (rutas traducidas sin distinguir mayúsculas, también con acentos, la canción que ya no está fuera, la
  última línea a medias fuera, la letra de unidad al disco con su música aunque otro con una copia parcial vaya antes, el sello de
  `rocola_fuentes.txt` con la fecha de creación que verá Linux), la configuración (`dir=` traducidas o quitadas, `respaldo_dir=`
  quitada), los botones y los modelos tal cual, lo que no se trae, la marca; la segunda llamada no toca nada; la copia del lanzador de
  antes (`rocola_de_xp.txt`); otra carpeta de XP (`.antes-xp` y la configuración juntada); el disco que cambia de nombre (no se
  re-importa); los fallos (2, 3, 4, sin dejar nada); la carpeta de XP como destino; y la carpeta de XP sin tocar (contenido y fechas).
  Añadido en la revisión: un apagón a media importación y a media re-importación (con `strace`, si está: el programa muere en seco al ir a
  poner un archivo en su sitio; la siguiente llamada deja lo mismo que una sin cortes, también los `.antes-xp`) y `rocola-xp` abierta antes
  de la primera importación sin la música (deja `rocola_volumen.txt` vacío, que no debe tapar la tabla de XP; una tabla con alguna canción
  suya sí se queda).
- **usa**: `rocola-xp` fuera de pantalla con lo importado (como el lanzador: más el `dir=` de la música): lee las seis medidas, el
  repaso solo mide la canción nueva, los botones importados dan créditos sin abrir el asistente de los botones, y al sonar una importada
  su sello vale tal cual; con el sello de Windows (la prueba de control) `rocola-xp` la adopta y le pone el de hoy, que es lo que evita
  el importador.
- **material**: el material de la IA, las miniaturas y el respaldo sin `ROCOLA_IA_DATOS` (en la carpeta de datos, como XP), con ella
  (allí; de qué canciones se sacó, `rocola_ia_hechas.txt`, sigue en la de datos), con menos de 256 MB libres allí (la cosecha no junta
  nada; lo demás sí) y con ella en solo lectura (a la carpeta de datos, y lo dice por la salida de errores).

Cada comprobación sale en una línea `BIEN` o `MAL`; al final `TODO BIEN (N comprobaciones)` o cuántas `MAL` (y sale con 1). Ninguna
rocola de la prueba queda abierta (se comprueba, y un `trap` las cierra si la prueba se corta). El 14-11-2026: 136 de 136 en JIT y en
NativeAOT; tras la revisión (sus 9 casos nuevos), 145 de 145 en JIT y en NativeAOT. Hace falta `strace` para los dos apagones (sin él
esos casos no se corren y lo dice). La comprobación «suena «01 Pulso»» pulsa las teclas a tiempo fijo: con la máquina muy cargada puede
salir MAL; se repite una vez.
