# Guiones de la medida de las carátulas (rapido-caratulas)

Copias de los guiones con que se midió (`FASE11_rapido-caratulas.md`); trabajan en `/root/rocola-trabajo/fase11/rapido-caratulas` (la
variable `W` de cada uno) y toman el candado de lo pesado (`/root/rocola-trabajo/fase11/pesado.lock`). Nada de esto va en el programa.

- `genera.py RAIZ N MP3`: la biblioteca inventada (N discos con una portada generada —JPEG y PNG de 250 a 3 000 px, con alfa, en gris,
  progresivas, con otro nombre— y dos MP3 cortos enlazados).
- `compila.sh ETIQUETA FUENTE`: el programa de una carpeta `Xp` con `Banco.cs` y `Mide.cs` (`RapidoCaratulas.csproj`), en JIT y NativeAOT.
- `banco.sh` / `bancos.sh`: el banco de las portadas (`--banco-caratulas`), frío y con la miniatura guardada, base contra esta rama.
- `e2e2.sh PROG SALIDA TECLAS TIEMPO`: la rocola entera fuera de pantalla con la biblioteca, en los núcleos 0 y 1 y llvmpipe en el 2 y el 3,
  con la función de prueba `medir-caratulas`; `serie.sh` alterna configuraciones; `analiza2.py` resume; `escena1-3.txt`, las teclas.
- `e2e3.sh` / `serie3.sh`: lo mismo que `e2e2.sh` / `serie.sh`, apuntando además la carga al empezar y, sin miniaturas, cuántas hizo la
  rocola (la repetición de la escena sin miniaturas, 21-11-2026).
- `desaloja.py`: saca archivos de la caché del sistema (`posix_fadvise`), para medir en frío sin `drop_caches`.
- (21-11-2026, las dos propuestas elegidas: `portadas_dct=` y `portadas_dos_lectores=`) `bancos2.sh`: el banco de las portadas con la rama
  sin esto (`nuevo4`, `7d5b02c`), sin `ROCOLA_OS`, con las dos claves en 0 y con la DCT, y las huellas comparadas; la DCT medida aparte con
  `rocola-xp --banco-dct LISTA 1.5,2,3,4 SALIDA` (`BancoDct.cs`: la diferencia con lo de XP y lo que cada una se aparta de un achicado ideal).
  `e2e4.sh` / `serie4.sh`: como `e2e3.sh` / `serie3.sh`, con `rocola_os.txt` por configuracion y, con `MUSICA=1`, la musica a tiempo real
  (el complemento «file» de ALSA a una tuberia que lee `ritmo.py`, el de `Pruebas/Recorridos`) y 20 creditos: `escena4.txt` pide una
  cancion larga (150 s; `largo.mp3` puesto en lugar de la primera cancion de los cinco primeros discos de la A) y despues hojea como
  `escena2.txt`; `analiza4.py` resume las paginas (con `analiza2.py`), el colchon del sonido y los lectores (`Mide.cs`).
