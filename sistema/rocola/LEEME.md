# La rocola de Rocola OS (etapa 2)

Código en .NET 10 de la rocola para Linux. Plan completo en [../ETAPA2.md](../ETAPA2.md).

- **`Motor/`** — la cadena de sonido, copiada de `Rocola/` (la rocola de XP) y llevada a .NET 10 y NAudio.Core 3. Por ahora
  conserva el código de cada etapa tal cual: cada una gana una lectura con `Span` que llama a la de siempre (`Puente.cs`). Hito 1
  (31-10-2026): las etapas de la cadena (limitador, presencia, el local, grave en mono, eses, retumbe, ataque, ásperos, bombo,
  zumbido, pre-eco, arreglos). `Provisional.cs` hace de nivelado (solo el volumen general) hasta traer el de verdad.
- **`pendiente/`** — lo que falta traer al motor (el nivelado y el EQ, el DSEE, el siseo, la tabla de la sonoridad): copiado y aún
  sin compilar. Se mueve a `Motor/` pieza a pieza.
- **`Pruebas/`** — `CompararConXP.cs`: el mismo programa compilado con Mono contra la rocola de XP y con .NET 10 contra el motor;
  pasa la misma música por cada etapa. `compara.py` compara las salidas muestra a muestra.

## Comprobar el motor contra la rocola de XP

```sh
sh pruebas/todo.sh --lista >/dev/null            # (o cualquier corrida: deja pruebas/rocola.exe compilado)
cd sistema/rocola/Pruebas && dotnet build
mcs -r:../../../pruebas/NAudio.dll -out:/tmp/xp/CompararConXP.exe CompararConXP.cs   # y copiar rocola.exe, NAudio.dll y NLayer.dll a /tmp/xp
(cd /tmp/xp && mono CompararConXP.exe rocola.exe /tmp/xp/salida)
dotnet bin/Debug/net10.0/CompararConXP.dll ../Motor/bin/Debug/net10.0/Motor.dll /tmp/linux
python3 compara.py /tmp/xp/salida /tmp/linux
```

Resultado del 31-10-2026: **las 15 variantes de las etapas, idénticas bit a bit** a las de XP.
