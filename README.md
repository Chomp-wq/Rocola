# Rocola

Rocola digital para Windows (WinForms + NAudio). Pantalla completa, manejo por
botonera/teclado, monedero con creditos, busqueda con teclado en pantalla,
seccion de videos (Windows Media Player) y portadas animadas en cubos 3D.

## Compilar

Requiere el SDK de .NET (8.0 o superior). El proyecto compila para
.NET Framework 3.5, igual que el ejecutable original, asi que corre en el
mismo equipo donde corria la version anterior.

```
dotnet build -c Release
```

El resultado queda en `bin/Release/` (`Rocola.exe` + `NAudio.dll`). Copia esos
archivos junto a la carpeta `musica` como siempre.

## Optimizaciones aplicadas (sin cambios de aspecto ni funcionalidad)

1. **Recursos GDI+ cacheados** (`src/Gdi.cs`): las fuentes, pinceles y lapices
   que antes se creaban y destruian en cada repintado (a 25 cuadros por
   segundo, en todos los controles animados) ahora se crean una sola vez y se
   reutilizan. Esto elimina cientos de asignaciones de objetos GDI por segundo
   y reduce drasticamente la presion sobre el recolector de basura y los
   handles GDI.

2. **Armado de la biblioteca en segundo plano** (`src/Form1.Library.cs`): el
   armado de discos (que busca portadas en el disco duro carpeta por carpeta),
   el escaneo completo de videos y la construccion del indice de busqueda se
   ejecutaban en el hilo de la interfaz al terminar el escaneo, congelando la
   rocola con bibliotecas grandes. Ahora todo ocurre en el hilo de fondo y la
   interfaz solo recibe el resultado ya listo.

3. **Busqueda mas ligera** (`src/Form1.Search.cs`): las cadenas derivadas de
   la consulta se calculan una vez por tecla en lugar de por cada cancion del
   indice (antes, con 10,000 canciones, cada tecla generaba 10,000 cadenas
   temporales).

4. **Gradientes de fondo cacheados**: el fondo del formulario y del buscador
   solo se reconstruyen cuando cambia el tamano o el color de acento, no en
   cada cuadro de animacion.

5. **Menos repintados**: la barra de progreso solo se redibuja cuando avanza
   al menos un pixel, y la limpieza de portadas viejas ya no toma candados
   cuando no hay nada que liberar.

6. **Codigo de dibujo del cubo sin asignaciones**: los arreglos temporales de
   la proyeccion 3D se reutilizan entre cuadros.

## Archivos de configuracion (junto al .exe)

- `rocola_teclas.txt` — mapeo de botones (se genera con la configuracion
  guiada; busca `0000000000` en el buscador para reconfigurar).
- `rocola_error.txt` — registro de errores de reproduccion.
- Carpeta `musica` — biblioteca (se busca junto al .exe y hasta 4 niveles
  arriba).
