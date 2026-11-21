# Guiones de la medida de las carátulas (rapido-caratulas)

Copias de los guiones con que se midió (`FASE11_rapido-caratulas.md`); trabajan en `/root/rocola-trabajo/fase11/rapido-caratulas` (la
variable `W` de cada uno) y toman el candado de lo pesado (`/root/rocola-trabajo/fase11/pesado.lock`). Nada de esto va en el programa.

- `genera.py RAIZ N MP3`: la biblioteca inventada (N discos con una portada generada —JPEG y PNG de 250 a 3 000 px, con alfa, en gris,
  progresivas, con otro nombre— y dos MP3 cortos enlazados).
- `compila.sh ETIQUETA FUENTE`: el programa de una carpeta `Xp` con `Banco.cs` y `Mide.cs` (`RapidoCaratulas.csproj`), en JIT y NativeAOT.
- `banco.sh` / `bancos.sh`: el banco de las portadas (`--banco-caratulas`), frío y con la miniatura guardada, base contra esta rama.
- `e2e2.sh PROG SALIDA TECLAS TIEMPO`: la rocola entera fuera de pantalla con la biblioteca, en los núcleos 0 y 1 y llvmpipe en el 2 y el 3,
  con la función de prueba `medir-caratulas`; `serie.sh` alterna configuraciones; `analiza2.py` resume; `escena1-3.txt`, las teclas.
- `desaloja.py`: saca archivos de la caché del sistema (`posix_fadvise`), para medir en frío sin `drop_caches`.
