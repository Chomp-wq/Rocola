# Banco de las ventanas (port nativo)

Herramientas para comprobar que lo que le llega al codigo de XP desde las ventanas (teclas, caracteres, raton, foco, relojes,
encargos, hojas, cierres) es lo mismo en la traduccion que en la referencia congelada (`/root/rocola-trabajo/referencia-xp`, aef2211).
Nada de esto entra en el programa: `Pruebas/**` esta fuera de `RocolaXp.csproj`. El detalle de que se comprobo y con que numeros
esta en `../../PORT_VENTANAS.md`.

- `Pruebas41/`: las 41 pruebas de la capa de ventanas contra lo que hace WinForms (cada una dice lo que daria WinForms si falla).
  `guiones/pruebas41.sh PROGRAMA ETIQUETA` las compila contra el `rocola-xp.dll` de PROGRAMA y las corre fuera de pantalla.
- `Pantalla/`: las reglas de la pantalla nativa una por una (orden de una vuelta, Libre, DoEvents, relojes, excepciones, encargos,
  espera, modal, SIGTERM), compiladas con los mismos `Nativo/PantallaRocola.cs` y `Reloj.cs` del programa.
  `guiones/pruebas_pantalla.sh [ETIQUETA]` (con `AOT=1`, tambien en NativeAOT).
- Ganchos en el programa (en `Plataforma/`, para que sigan cuando se retire la imitacion):
  - `ROCOLA_TRAZA_VENTANAS=ruta`: una linea por suceso (`Plataforma/TrazaVentanas.cs` explica el formato).
  - `ROCOLA_ESTADO_EN=S:ruta,...`: el estado de XP y el de cada pieza de las hojas abiertas a los S segundos (`Plataforma/VolcadoEstado.cs`).
- `ganchos-referencia.patch`: los mismos ganchos para la copia de trabajo de la referencia (solo su Plataforma).
- `guiones/` (variables: `W` carpeta de trabajo fuera del repositorio, por defecto `/root/rocola-trabajo/port/ventanas`):
  - `datos.sh`: los datos de prueba de partida (`$W/datos0`).
  - `referencia.sh`: la referencia con los ganchos en `$W/ref-trazas` (copia, parche y compilacion JIT).
  - `escenas.sh PROGRAMA ETIQUETA [ESCENA...]`: las escenas (arranque, moneda, video, tab, buscador, cierreq, altf4, sigterm,
    f3sigterm, kill9) con un programa (carpeta JIT, o `aot:CARPETA`); cada una con `corre.sh` (copia nueva de los datos, teclas
    `ROCOLA_PULSA`, capturas con sus capas, traza, estados y los archivos que quedan).
  - `ruido.sh REF N`: la referencia N veces (R1..RN) y su ruido (`$W/ruido/ESCENA.json`); `SOLO_MIDE=1` solo mide.
  - `compara.sh A B`: cada escena de B contra la de A: la secuencia igual, las cuentas (relojes, encargos de otros hilos, repeticion
    de una tecla mantenida) dentro del ruido, los estados iguales, las capas de las hojas igual pixel a pixel y `rocola_estado.txt`
    presente o no en las dos (`compara_trazas.py`, `compara_escena.py`).
- `../Inventario/`: la herramienta de Roslyn del inventario (lo que el codigo de XP usa de Windows, por linea), base del diff contra
  `Rocola/` (`dotnet run -- RAIZ_XP SALIDA`; usa el SDK y los paquetes de esta maquina: rutas al principio de `Program.cs`).

Orden de uso: `datos.sh`, `referencia.sh`, `ruido.sh $W/ref-trazas/sistema/rocola/Xp/bin/Release/net10.0 3`, compilar la traduccion,
`escenas.sh CARPETA N1` y `compara.sh R1 N1`; y `pruebas41.sh` con las dos. La maquina de pruebas esta compartida: con carga las
teclas de prueba llegan tarde y las cuentas por segundo se mueven (de ahi la holgura medida); la secuencia no.
