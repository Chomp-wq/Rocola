# Cielo vivo, grupo nucleo: el estado del cielo, los enchufes, DN1 y DN5

20-11-2026. Rama `cielo-nucleo`, sobre `424ade7` (la rama principal con la decision del dueno y su plan). Es lo de las secciones 2, 3, 4 y 5
de `PLAN_CIELO_VIVO.md` (que manda): las cuatro funciones del andamio con todas sus claves, el estado del cielo (sol, luna, ciudad, clima,
calendario, calma, «Ver como se veria»), los **enchufes** en el codigo de XP para que los cuatro grupos de despues trabajen en paralelo sin
tocar las mismas lineas, y las dos primeras ideas: **DN1** (el cielo sigue al sol) y **DN5** (la raya del horizonte con la luz de la hora).
Solo Rocola OS (`ROCOLA_OS`), solo la tarjeta (el dibujo por procesador sigue congelado y no se toca). Sin `ROCOLA_OS`, o con las cuatro
funciones apagadas, o con todas sus claves en 0, la rocola pinta lo de XP (comprobado con las cintas de la tarjeta, abajo). **Nada probado
en el A4.**

## Que se hizo

| Archivo (todo en `RocolaOs/`) | Que lleva |
|---|---|
| `RocolaOs.Cielo.cs` | Las cuatro funciones (`cielo`, `fiestas`, `clima`, `vidrio`) con sus 23 claves; la foto (`RocolaOsCieloFoto`); el arranque, el latido; la escena y la calma de cada cuadro (`RocolaOsCieloEscena`); «Ver como se veria» y su orden del buzon; el reloj de los cuadros de las pruebas; lo que XP sabe de si alguien elige (`Form1.RocolaOsCieloEligiendo`) |
| `RocolaOs.CieloAstro.cs` | El sol y la luna (`RocolaOsAstro`, traidos de `Motor/CieloClima.cs`), la salida y la puesta del sol, las 41 ciudades (`RocolaOsCiudades`, el mismo orden que el programa anterior) |
| `RocolaOs.CieloClima.cs` | El clima (`RocolaOsClima`, Open-Meteo, `DeCodigo`, `rocola_clima.txt`), el lugar por internet (`RocolaOsLugar`, `rocola_clima_lugar.txt`) y el hilo del cielo (`RocolaOs.Hilo("cielo")`: red, disco y `rocola_fechas.txt`) |
| `RocolaOs.CieloFiestas.cs` | El calendario de §6 (`RocolaOsCalendario`, `RocolaOsFiesta`), las fechas propias (`RocolaOsFechasArchivo`: el formato de `rocola_fechas.txt`), los proximos fuegos |
| `RocolaOs.CieloDia.cs` | DN1 (las paletas de §5, los pesos por la altura del sol, el degradado `Arma`, las nebulosas de dia `Niebla`, el brillo de las estrellas) y DN5 (`Raya`) |
| `RocolaOs.CieloEnchufes.cs` | Los enchufes: lo que llaman los ganchos g120-g139 (partes de `GpuPanel`, `GpuStarfield` y `Form1`) y los registros de clientes de cada grupo (capas, tintes, estrellas, mensajes, aurora, fugaz) |
| `RocolaOs.CieloPrueba.cs` | La orden `rocola-xp --prueba-cielo` (las cuentas, sin la rocola) |
| `GANCHOS.tsv` | Los 20 ganchos nuevos, g120 a g139 |
| `Pruebas/RocolaOs/cielo/` | La bateria: `prueba.sh` y sus ayudantes (`cinta.py`, `clima_falso.py`, `contraste.py`, `medio.py`, `sol_y_calendario.py`) |

### Las funciones y sus claves (§2)

Registradas en `RocolaOsCielo.Registra` (un `[ModuleInitializer]`, sin trabajo). Los grupos de despues **solo leen** estas claves (ya estan
declaradas: nadie toca el registro de otro). Todas cambian en vivo (`enVivo`: el latido las vuelve a mirar cada segundo y las pone en la foto);
encender o apagar una de las cuatro funciones vale desde el siguiente arranque (como todo el andamio).

| Funcion (de fabrica 1) | Claves (de fabrica) |
|---|---|
| `cielo` | `cielo_sol` DN1 (1), `sol` DN2 (1), `luna_camino` DN3 (**0** = A, la de XP), `estrellas_anochecer` DN4 (1), `horizonte_hora` DN5 (1), `nubes` DN6 (1), `ciudad_bar` (**0**, Entero 0 a 41) |
| `fiestas` | `aurora_fiesta` FI1, `horizonte_fiesta` FI2, `fugaz_fiesta` FI3, `fuegos` FI4, `escaparate_fiesta` FI5, `fechas_propias` FI6 (todas 1) |
| `clima` (es CL1) | `lluvia` CL2, `tormenta` CL3, `neblina` CL4, `clima_escaparate` CL5 (todas 1) |
| `vidrio` | `vidrio_ficha` VI1 (**0**), `vidrio_franja` VI2 (**0**), `vidrio_pastillas` VI3 (1), `vidrio_lista` VI4 (**0**), `vidrio_luz` VI5 (1), `vidrio_buscador` VI6 (**0**) |

Cada grupo pregunta por su idea en la foto: `RocolaOsCielo.Foto.FI2`, `.CL3`, `.VI5`... (la funcion encendida **y** su clave), y DN3 B es
`Foto.DN3B` (`luna_camino=1`).

## El estado (§3)

**La foto** (`RocolaOsCieloFoto`, inmutable): la hace el latido (1 s, en la pantalla) y la cambia entera (`Volatile`); el que la lee no
necesita cerrojo. Lleva: la hora del cielo (`Local`, `Utc`, `VerComo`), el lugar (`Lat`, `Lon`, `Lugar`, `Ciudad`), el sol (`SolAlt`,
`SolAz`, `SolSube`), la luna (`LunaAlt`, `LunaAz`, `LunaFase` 0 nueva / 0,5 llena, `LunaLuz`), los pesos de las paletas (`Pesos[8]`, `Fase`
la de mas peso, `Noche` = peso de la noche de XP), lo de §5 ya mezclado (`Estrellas`, `Nebulosa`, `Velo`, `Raya`), el clima (`Clima`,
`ClimaSabido`), lo de hoy (`Hoy`: `Fiestas`, `Propias`, `Mensajes`, `ProximosFuegos`, `CuentaFuegos`) y las 23 ideas ya miradas. Una foto por
segundo (unos cientos de bytes); **nada de memoria nueva por cuadro**.

**El cuadro** (gancho g120, en la pantalla, al empezar cada cuadro de la tarjeta y antes de abrirlo): interpola lo de la foto anterior a la
nueva en 1 s (1,5 s si salta: «Ver como se veria»), sin escalon aunque llegue una foto a media mezcla (parte de lo que se ve:
`LoQueSeVe`); deja la escena (`RocolaOsCielo.Escena`: ancho, alto, **horizonte** = la raya de XP, `auCuadro.BaseY - AurLineaAlza`, 667 de
768 en la pared, el escaparate y el disco, mas arriba en el reposo; la franja de abajo; el modo de XP 0-4; la pared; el reposo; el reloj
fino y su paso) y **la calma**.

- **La hora del sol y de la luna**: `RocolaOsAstro` (las cuentas de bolsillo del programa anterior). La salida y la puesta del sol, a
  **0,3 min** de la NOAA (astral) en CDMX y Monterrey (28 de 28). La fase de la luna, a menos de 0,03 de las lunas de los eclipses de 2026.
  La zona de la hora es la del sistema (`/etc/localtime`, Mexico en Rocola OS; `TZ=America/Mexico_City` en las pruebas).
- **La fase del dia**: anclas al centro de cada tramo de §3 y mezcla en linea recta entre dos (`RocolaOsCieloDia.Pesos`). Manana:
  noche (-12 o menos) / alba (-8) / amanecer (-0,5) / manana (+9 a +30) / mediodia (+40). Tarde: noche (-12) / anochecer (-8) / atardecer
  (-0,5) / dorada (+9) / manana (+15 a +30) / mediodia (+40). Los pesos suman 1 y de una centesima de grado a otra un peso cambia como mucho
  0,0025 (sin saltos). Por la manana o por la tarde: si la altura del sol crece en 10 minutos. **A las horas de las escenas, el 18-11 en
  CDMX**: 6:50 amanecer 0,95 + manana 0,05; 9:30 manana 0,72 + mediodia 0,28; 13:00 mediodia; **17:40 dorada 0,36 + atardecer 0,64**;
  **18:35 noche 0,33 + anochecer 0,67**; 22:00 noche. (Las maquetas decian «17:40 dorada» y «18:35 atardecer»; con los tramos de §3, que
  mandan, el sol de esas horas de noviembre da otra cosa. Ver «Pendiente».)
- **La ciudad**: `ROCOLA_CIUDAD` (pruebas) > `ciudad_bar` (1 a 41) > la de internet (`rocola_clima_lugar.txt`, una vez por semana, como
  mucho un intento por hora; ipapi.co, ipwho.is, get.geojs.io, o `ROCOLA_LUGAR_API`) > la Ciudad de Mexico.
- **El clima**: el hilo del cielo pide Open-Meteo (`current=weather_code,cloud_cover,precipitation,rain,showers,snowfall,wind_speed_10m,
  wind_direction_10m,temperature_2m,visibility,is_day`) cada 15 min (al fallar, a los 2 min si no hay ninguno o a los 5), y al momento si
  cambia el lugar; espera 20 s al arrancar (`ROCOLA_CLIMA_YA` en las pruebas: sin espera). Guarda `rocola_clima.txt` (clave=valor: `medida`
  UTC, `codigo`, `nubes` %, `lluvia` mm/h, `viento`, `direccion`, `temperatura`, `visibilidad`, `de_dia`, `lat`, `lon`) con la escritura
  segura de XP. Vale 3 h (`RocolaOsClima.Vigente`); despues, despejado (`Despejado0`, `ClimaSabido` = false). Tipos: despejado, poco nublado,
  nublado, neblina, llovizna, lluvia, aguacero, tormenta, nieve (`DeCodigo`). Por HTTP con `HttpClient` (sin el proxy para 127.0.0.1),
  `JsonDocument` (NativeAOT no protesta: lo usa ya `RocolaOs.Spotify*`), sin llaves.
- **El calendario** (§6): `RocolaOsCalendario.De(hora local, rocola_fechas.txt)`. `rocola_fechas.txt`: «DD-MM;tipo;texto», tipos `cumple`,
  `aniversario`, `fiesta` (con «|#rrggbb,...» sus colores), `quitar` (una fiesta por su clave, o todas las de ese dia), `mensaje` y `fuegos`
  (`HH:MM` o `no`); comentarios con «#»; lo que no vale se salta y se apunta. Solo se lee (el hilo, cuando cambia); la hoja que lo edita es
  de despues. Los fuegos: el Grito (15-9, 23:00) y Ano Nuevo (1-1, 00:00 tras 10 s de cuenta), 4 minutos; `ProximosFuegos` da el proximo en
  las siguientes 24 h.
- **La calma** (regla 5): `Escena.Calma` baja a 0,35 en 0,4 s mientras alguien elige y vuelve a 1 en 3 s (con una curva suave).
  `Escena.Eligiendo` sale de lo que XP ya sabe: `lastInput` (teclas y raton) de hace menos de 15 s, `searchOpen` (el buscador),
  `ajustesAbiertos` (Ctrl+F3) o `AsistenteVisible` (la estrella: la ayuda); y `RocolaOsCielo.HojasAbiertas` > 0 (lo pone Ctrl+F4 mientras esta
  abierta). Lo de XP (aurora, estrellas, fugaz) no la usa.
- **«Ver como se veria»**: `RocolaOsCielo.VerComo(hora local?, clima?, desdeLaHoja)` (para la hoja «Imagen» de Ctrl+F4) pone la hora, la fecha
  y el clima 30 s, sin guardar nada; se quita a los 30 s, al tocar un boton (salvo `desdeLaHoja`) o con `QuitaVerComo` (al cerrar la hoja).
  Mientras, **por el buzon**: `/run/rocola-cielo-ver` con «AAAA-MM-DD HH:MM», «HH:MM», «...;clima» o «quitar» (lo lee el latido, lo borra y
  contesta en `/run/rocola-cielo-ver.respuesta`). La bateria lo usa para las capturas.
- **Para las pruebas**: `ROCOLA_CIELO_FECHA` («AAAA-MM-DD HH:MM», hora local; corre desde ahi con el reloj), `ROCOLA_CLIMA` (un tipo o
  «tipo,nubes,lluvia,viento,direccion,temperatura»: forzado, sin red, no caduca), `ROCOLA_CLIMA_API`, `ROCOLA_LUGAR_API`, `ROCOLA_CIUDAD`
  («lat,lon[,nombre]»), `ROCOLA_CLIMA_YA`, y `ROCOLA_CIELO_RELOJ=archivo` (p50/p95/p99/max del trabajo de cada cuadro desde los 8 s, al cerrar).
- **La bitacora** (`rocola_os_bitacora.txt`, `[cielo]`): el arranque (hora, lugar, sol, fase, luna, clima, fiestas), cada cambio de fase,
  de clima, de lugar y de lo de hoy, el horizonte de la escena (cuando cambia mas de 20 px), el clima guardado al arrancar (y si vale), cada
  fallo de la red (una vez hasta que cambie), «Ver como se veria». `vivo.txt` lleva un renglon `cielo=...` (cuando 1a lo escriba).

## DN1: el cielo sigue al sol

Las paletas de §5 (de las maquetas aprobadas), con el horizonte de la escena: el de XP en la pared, el escaparate y el disco (667/768: la raya
de la aurora; las maquetas lo suponian igual, 0,868) y el que sube con el reposo. **De noche (sol bajo -12) no se pinta nada: XP pinta su cielo
de siempre**, con su tinte por la hora (`CieloHora`) y todo: la regla 1 manda que la noche sea la de XP bit a bit. Entre la noche y el alba (y
el anochecer y la noche) se mezcla con las paradas de XP **en sus mismas franjas** (las 12 de C1, con `Luz.ColorCielo`, o las 3 de antes): a peso
1 de la noche sale lo mismo que XP. De dia las paletas sustituyen al degradado y al tinte por la hora de XP (el color del genero de abajo va al
35 % en el suelo, sin ese tinte: `skyBot` con C2). Lo pinta `RocolaOsCieloDia.Arma` (hasta 48 fronteras, `DrawSkyN` sin mezcla, como el de
XP); los dos caminos del cielo de XP (el directo y la imagen intermedia a media resolucion) llaman a lo mismo: el mismo degradado (medido:
abajo). La imagen intermedia se vuelve a pintar cuando cambia el cielo (`FirmaCielo`, g123).

- **Las nebulosas de dia** (g124 y g125): el alfa de cada pasada por la nebulosa de la fase (0,28 a 1) y el «velo» (el color hacia
  `#c7d6f2` con su luz media x 3,2 x 0,55, por el velo de la fase): cirros, como en §5.
- **Las estrellas** (g126): el brillo de todo el campo por la fase (0 de dia, 0,45 al alba, 0,55 al anochecer). Con DN4 lo hara el grupo del
  sol estrella por estrella (`RocolaOsCielo.EstrellasPorEstrella` = true y g127).
- **La «madrugada» de §5** (estrellas 1,1 y nebulosa 0,9 en las maquetas): no se aplica; la regla 1 manda.
- **La regla 3**: con el horizonte de la pared, ninguna paleta pasa de **0,125** hasta 0,848 del alto (manana; el limite es 0,17), ni la mezcla
  de cada minuto de cuatro dias en CDMX y Monterrey (0,124); medida en las capturas de la pared (abajo). En el **reposo con musica** el horizonte
  sube con el panel (la raya en 525 de 768) y con el la franja clara: hasta 0,848/0,868 del horizonte sigue bajo 0,17, pero «hasta 0,848 del alto»
  literal cae ya bajo el horizonte (detras del panel del reposo): ahi hay 0,63. Es lo de las maquetas aprobadas (la del reposo tenia el
  horizonte en 525); se dice para que el dueno lo sepa.

## DN5: la raya del horizonte con la luz de la hora

La raya de XP (la que lleva el pie de la aurora: `AurBrillo`, un nucleo de 1,4 px, 34 px de resplandor hacia arriba y 11 hacia abajo, sumada)
toma el color de la fase (§5: `#a888d8` alba ... `#ff9a58` atardecer), con 2,2 veces su luz media (como en las maquetas: la luz media de la
raya de XP por el color de la fase x 2,2), algo mas fuerte en el centro (0,6 a 1), mezclada con la de XP por el peso del dia. De noche, la de
XP. Va en el gancho de la columna de la aurora (g128) y la aurora se rehace cuando cambia la luz (g129, `FirmaAurora`).

**Diferencia con §5**: §5 describe un resplandor de 70 px con `exp(-1,8 d)` y un filo de 2,2 px (la funcion `luz_horizonte` de
`maquetas/lib.py`). Las maquetas que aprobo el dueno **no** la usaron (solo `prueba_lib.py`): recoloreaban la raya de XP (`escena.py`,
`cielo_hora`). Se hizo lo de las maquetas; un resplandor de 70 px subiria luz hasta la fila 597 y romperia la regla 3 en la ultima fila de
titulos (621 a 652). Si se quiere el de §5, va en la capa `TrasAurora` y se mide con la bateria.

## Los enchufes (§4)

Los 20 ganchos del nucleo, en `Codigo/` (una linea cambiada en su sitio, con su marca `// ROCOLA OS gNNN: (XP) ...`). Cada uno llama a un
metodo de `RocolaOs.CieloEnchufes.cs` que empieza por `if (!RocolaOs.Activo) return <lo de XP>`. Todos corren **en el hilo de la pantalla,
dentro de `RenderFrame`**; los de las capas, entre `BeginFrame` y `EndFrame` (dentro del cuadro: solo dibujar).

| Gancho | Archivo:linea | Que da | Quien lo usa |
|---|---|---|---|
| g120 | `D3D9Star.cs:2728` (antes de `stars.Begin`) | **El cuadro del cielo**: la escena, la calma, lo interpolado; despues, `Prepara` de cada pintor (aqui se hornea y se sube: fuera del cuadro) | todos |
| g121 | `D3D9Star.cs:1226` (`CieloDirecto`) | **Los colores del cielo**: el degradado de DN1 (o con tintes) en lugar del de XP | DN1; los tintes (`RocolaOsCieloTinte`: CL1 nublado) |
| g122 | `PerformanceGpu.cs:389` (`CieloAmplio`) | lo mismo en la imagen intermedia (cielo a media resolucion) | DN1, tintes |
| g123 | `PerformanceGpu.cs:505` (`CieloMedioBase`) | la imagen intermedia se repinta si cambio el cielo de Rocola OS (`FirmaCielo`, `FirmaCieloExtra`, `TintesVersion`) | DN1, tintes |
| g124 | `D3D9Star.cs:3855` (`NieblaDeriva`) | el color y el alfa de las dos pasadas de la nebulosa | DN1 (velo) |
| g125 | `D3D9Star.cs:3845` (la via lactea) | el color y el alfa de la via lactea | DN1 |
| g126 | `D3D9Star.cs:1336` (`BuildStarVerts`) | **El brillo de las estrellas** (todo el campo) | DN1 (por la fase) |
| g127 | `D3D9Star.cs:1364` (`BuildStarVerts`, por estrella) | el brillo de **cada** estrella: clientes `RocolaOsCieloEstrella` (`Brillo(actual, i, x, y, tamano)`, multiplican) | DN4 (de una en una), DN6/CL1 (tras las nubes) |
| g128 | `D3D9Star.cs:4469` (`DrawAurora`, al acabar cada columna) | **Los colores de la aurora**: clientes `RocolaOsCieloAurora` (`Columna`: `aurCortina`, `aurNc`, `aurLc`, `aurLi`, `aurSueloC`; para el suelo, `AurColoresSuelo`), y despues DN5 | FI1; DN5 |
| g129 | `D3D9Star.cs:4391` (`DrawAurora`) | la aurora se rehace si cambia `FirmaAurora` (DN5) o `FirmaAuroraExtra` (que sube quien cambia sus colores) | DN5, FI1 |
| g130 | `D3D9Star.cs:2248` (`trasCielo`) | capa **TrasNieblas**: entre el cielo (con sus nebulosas o la imagen intermedia) y la aurora | DN2 el sol; DN6/CL1 las nubes |
| g131 | `D3D9Star.cs:2249` (`trasCielo`) | capa **TrasAurora**: delante de la aurora, su raya y su reflejo; antes del grano y de las estrellas | FI2 adornos del horizonte; CL4 neblina |
| g132 | `D3D9Star.cs:2253` (`trasEstrellas`) | **La luna**: un pintor de la capa ConLaLuna puede tomarla (`TomaLaLuna`) y pintar la suya; si nadie, la de XP; despues, capa **ConLaLuna** (antes de la estrella y las fugaces) | DN3 (A de dia, B en su camino); CL1 (las nubes delante de la luna) |
| g133 | `D3D9Star.cs:2746` | capa **TrasFondo**: delante de todo el cielo (fugaces, vineta, grano), detras del halo de la eleccion, las portadas y la interfaz | CL2 lluvia; FI4 fuegos; DN2 el sol en la pared |
| g134 | `D3D9Star.cs:2817` | capa **AntesDeLaInterfaz**: encima de portadas, cubos y contorno; debajo de la textura de la interfaz (barras, pastillas, letras) | VI3 pastillas |
| g135 | `D3D9Star.cs:2824` | capa **Escaparate**: despues del escaparate de XP (`DibujaAtraccion`) | FI5 papel picado, FI6 globos, CL5 la pastilla del clima |
| g136 | `AtraccionGpu.cs:451` (`ArmaMensajes`) | **Los mensajes del escaparate**: clientes `RocolaOsCieloMensajero` (`Pon(lista, foto)`) sobre la lista que XP turna, recien armada | FI5, FI6, CL5 |
| g137 | `D3D9Star.cs:2910` | capa **AntesDeLaFicha**: antes de la ficha rapida del disco | VI1 |
| g138 | `D3D9Star.cs:2873` | capa **AntesDelEstado**: antes del panel del reposo con musica (AHORA SUENA) | VI2 |
| g139 | `D3D9Star.cs:1405` (`UpdateAndDrawShoots`) | **El color de la fugaz** de festejo: clientes `RocolaOsCieloFugaz` (`Color(deXp, foto)`) | FI3 la estela de la fecha; FI6 confeti |

**El orden de pintado** (de atras hacia delante): el degradado (g121/g122) > las nebulosas (g124/g125, o la imagen intermedia) > **TrasNieblas**
> la aurora con su raya (g128) y su reflejo > **TrasAurora** > el grano > las estrellas (g126/g127) > la luna (g132) > **ConLaLuna** > la estrella
de XP > las fugaces (g139) > la vineta > **TrasFondo** > el halo de la eleccion, las portadas, los cubos, el contorno > **AntesDeLaInterfaz** > la
interfaz de XP > el escaparate > **Escaparate** > las piezas vivas, el buscador > **AntesDelEstado** > el panel del reposo > ... > **AntesDeLaFicha**
> la ficha > el viaje de la portada, la moneda, los avisos, la estrella asistente.

**Los clientes** se registran en el `[ModuleInitializer]` de cada grupo (sin trabajo) con un orden fijo (`RocolaOsCieloOrden`: Sol 100, Luna 200,
Estrellas 300, Nubes 400, Clima 500, Fiestas 600, Vidrio 700; mas lo suyo): `RocolaOsCielo.PonPintor(capa, pintor)`, `PonTinte`, `PonEstrella`,
`PonMensajero`, `PonAurora`, `PonFugaz`. Son arreglos fijos que se cambian enteros al registrar: el cuadro solo los recorre (sin delegados ni
memoria por cuadro). Un cliente que lanza se apunta (`RocolaOs.Falla`) y ya no se llama en la sesion. Cada cliente mira su idea en la foto y
la calma en la escena (lo que se mueve en el cielo nuevo va por `Escena.Calma`; regla 5).

```csharp
// RocolaOs/RocolaOs.CieloLluvia.cs (ejemplo: el grupo del clima)
internal sealed class RocolaOsCieloLluvia : RocolaOsCieloPintor
{
    internal RocolaOsCieloLluvia() { Quien = "CL2 la lluvia"; Orden = RocolaOsCieloOrden.Clima + 10; }
    internal override void Prepara(GpuPanel p) { /* hornear y subir texturas, si hacen falta: aqui, fuera del cuadro */ }
    internal override void Pinta(GpuPanel p, int capa)
    {
        RocolaOsCieloFoto f = RocolaOsCielo.Foto;
        if (f == null || !f.CL2 || !f.ClimaSabido || f.Clima.Lluvia <= 0f) return;
        RocolaOsCieloEscena e = RocolaOsCielo.Escena;          // ancho, alto, horizonte, modo, calma...
        p.RocolaOsPintaLluvia(f, e);                            // (su parte de GpuPanel: ve lo privado de XP, stars incluido)
    }
#pragma warning disable CA2255
    [System.Runtime.CompilerServices.ModuleInitializer]
#pragma warning restore CA2255
    internal static void Registra() { RocolaOsCielo.PonPintor(RocolaOsCieloCapa.TrasFondo, new RocolaOsCieloLluvia()); }
}
```

### Lo que se puede dibujar (la tarjeta de XP)

Desde una parte de `GpuPanel` en `RocolaOs/` (ve lo privado de XP), con `stars` (`GpuStarfield`) y vertices `Tarjeta.Vtx` (X, Y en pixeles ya
transformados, Z = 0, Rhw = 1, `Color` ARGB, U, V). Los tres programas fijos de XP: el color del vertice, la textura por el color del vertice
y las dos texturas (grano y vineta). Lo que hay:

- **Sin textura**: `stars.DrawMesh(v, tris)` (mezcla normal, encima), `stars.DrawMeshAdd(v, tris)` (sumando luz: neon, raya), `stars.DrawColorQuad(quad)`;
  el degradado del cielo es `DrawSkyN` del dispositivo (sin mezcla: lo usa el nucleo).
- **Con textura**: `stars.DrawQuad(tex, quad)` (un cuadro, mezcla normal), `stars.DrawTexturedBatch(tex, v, tris)` (muchos en un envio),
  `...Wrap` (repetida).
- **Halos** (la textura lisa de XP, sumados): `stars.DrawGlow(cx, cy, r, argb)`; muchos de una vez con `GlowBatchBegin(n)`, `GlowBatchAdd(cx,
  cy, rx, ry, argb)` y `GlowBatchEnd()` (una llamada para todos: en Direct3D 9, y en el A4, lo caro es la llamada).
- **Hornear una textura una vez**: un `SKBitmap` (`Nativo/Mapas.Nuevo(w, h)` y un `SKCanvas` de Skia, como `HorneaLuna`) y
  `stars.CreateOverlay(bmp, out u, out v)` en **`Prepara`** (fuera del cuadro); `stars.UpdateOverlay(...)` para cambiarla; `Tarjeta.Suelta(tex)`
  al acabar (y al soltarse la tarjeta: `SueltaCieloVivo` de XP lo hace con las suyas).
- Lo que ya hay hecho: `lunaCieloTex` (la luna horneada de XP, por fase), `glowTex` / `uiGlowTex` (halos), las nebulosas `nebTex`, `hazTex`.
- **Saber que hay detras** (el vidrio): **no hay** todavia una primitiva para copiar lo ya pintado a una textura. Lo que el dispositivo ofrece:
  la imagen intermedia (`EmpiezaMedia/TerminaMedia/DibujaMedia`: pintar a una textura de la mitad o un cuarto y estirarla) y, en `Gl.cs`,
  `CopyTexImage2D` (`glCopyTexImage2D`). El grupo del vidrio la pone en una parte suya de `DispositivoTarjeta` (en `RocolaOs/`), con su
  prueba: copiar el rectangulo de la ficha del fondo del dispositivo (el framebuffer de la tarjeta esta atado dentro del cuadro; la Y de
  OpenGL va al reves), reducirlo con la imagen intermedia y pintarlo en `AntesDeLaFicha`. Ver «Rangos reservados».

## Como lo usa cada grupo

- **Sol, luna y estrellas** (DN2-DN4, `g140-g149`): el sol en `TrasNieblas` (por el cielo) y en `TrasFondo` (en la pared solo asoma sobre la
  raya, `p.RocolaOsHorizonteXp`; detras de una portada de la derecha con el halo x 0,42: las casillas estan en `gridBuf`); la luna con
  `TomaLaLuna` en `ConLaLuna` (A: la de XP, `lunaCieloTex`, en `Form1.SitioLuna`, con su alfa de dia por `Foto.LunaAlt`; B: en su camino, mas
  grande cerca del horizonte); las estrellas de una en una con `PonEstrella` (g127, `tamano` = 0 polvo, 1 mediana, 2 grande; el indice es el
  de su patron) y `EstrellasPorEstrella = true`.
- **Nubes y clima** (DN6, CL1-CL5, `g150-g169`): las nubes en `TrasNieblas` (detras de la aurora) y, delante de la luna, en `ConLaLuna`; las
  estrellas tras las nubes con `PonEstrella`; el cielo nublado con un tinte (`PonTinte`, y `TintesVersion++` cuando cambia); la lluvia y los
  relampagos en `TrasFondo`; la neblina en `TrasAurora`; la pastilla en `Escaparate` (horneada en `Prepara`) y su mensaje con `PonMensajero`.
  El clima: `Foto.Clima` (tipo, nubes, lluvia, viento y direccion, temperatura, visibilidad, de dia) y `Foto.ClimaSabido`.
- **Fiestas** (FI1-FI6, `g170-g189`): FI1 con `PonAurora` (los colores de `Foto.Hoy.Fiestas[i].Colores`; `FirmaAuroraExtra++` al cambiar de
  fecha); FI2 en `TrasAurora`; FI3 con `PonFugaz`; FI4 en `TrasFondo` (`Foto.Hoy.ProximosFuegos`, `CuentaFuegos`; nunca detras de un texto: los
  textos del escaparate estan en `atrCuadro`, los de la pared bajo las casillas); FI5 y FI6 en `Escaparate` y con `PonMensajero`
  (`Foto.Hoy.Mensajes`, `Foto.Hoy.Propias[i].Mensaje`); `rocola_fechas.txt` ya se lee (su hoja es de despues).
- **Vidrio** (VI1-VI6, `g190-g209`): VI1 en `AntesDeLaFicha`, VI2 en `AntesDelEstado`, VI3 en `AntesDeLaInterfaz`; el filo de luz (VI5) con
  `RocolaOsCielo.RayaR/G/B` y `NocheCuadro` (el plateado de XP de noche o sin DN1); lo de detras, con la primitiva de arriba.

### Rangos reservados (lo que un enchufe generico no da)

- **g140-g149 (sol, luna y estrellas)**: si DN3 necesita el sitio de la luna de XP en otro lado: `CieloVivoGpu.cs:303` (`EnsureLunaCielo`: el
  tamano horneado) y `:324` (`DibujaLunaCielo`), las dos `float cx, cy, r; Form1.SitioLuna(Width, Height, out cx, out cy, out r);`, y `:325`
  (`float atenua = ...`). Con `TomaLaLuna` no hacen falta. Si DN4 quiere apagar de dia las fugaces al azar de XP: `D3D9Star.cs:1445`
  (`uint la = (uint)(230 * k * dim); if (la > 255) la = 255;`; la de festejo, `s.Col` de g139, tiene que seguir viendose).
- **g150-g169 (nubes y clima)**: nada reservado: los enchufes bastan.
- **g170-g189 (fiestas)**: la estela de varios colores de la fugaz (tricolor): `D3D9Star.cs:1451` (`uint headC = (la << 24) | rgb, tailC = rgb;`)
  y `:1452-1453` (sus vertices); los colores de la aurora antes de `TonoPorNivel` (si `Columna` no basta): `D3D9Star.cs:4445` y `:4454` (las dos
  `FlowColor(...)`).
- **g190-g209 (vidrio)**: el relleno de cada pieza: la ficha `PantallaVivaGpu.cs:542` (`HorneaFicha`), el panel del reposo `EstadoReposoGpu.cs:118`
  (`BuildIdlePanel`), la textura de la interfaz `D3D9Star.cs:2821` (`DibujaTeselas(uiTex, ...)`), los avisos `D3D9Star.cs:2930` (`toastQuad`) y
  `:2932` (`DibujaAviso`) y las tarjetas del buscador (`BuscadorPortadasGpu.cs`). Las ventanas del dueno (Ctrl+F3, Ctrl+F4) **no son de la tarjeta**: son
  hojas de la pantalla (`Nativo/Hoja.cs`, pintadas con Skia y compuestas por `CompositorPantalla`); su vidrio pide otra cosa (decidirlo con el
  coordinador).
- **g210-g219**: la hoja Imagen de Ctrl+F4 (de §8).

## Las pruebas

`bash Pruebas/RocolaOs/cielo/prueba.sh PROGRAMA [partes]` (sus parametros en la cabecera). Necesita brotli y astral
(`pip install --target /root/rocola-trabajo/fase11/cielo-nucleo/py brotli astral==3.2`), los datos de las escenas de ventanas (`datos0`) y los
de las maquetas (`/root/rocola-trabajo/maquetas/datos-base` y su `musica`, solo se leen). Corre fuera de pantalla con llvmpipe, sin red de
verdad. Resultados (JIT salvo «reloj»):

| Parte | Que comprueba | Resultado |
|---|---|---|
| `ganchos` | `ganchos.py`; los 20 del cielo en el manifiesto; `diff-xp.py --sin-ganchos` | **TODO BIEN**: 112 archivos de `Codigo/` vuelven byte a byte a 69e6547 (20 con ganchos, 67 ganchos); `diff-xp.py`: 5135 lineas quitadas, 4868 puestas, 229 sin marca (las de la base) |
| `orden` | `rocola-xp --prueba-cielo` (68 comprobaciones) y lo de fuera | **TODO BIEN (68)**. Salidas y puestas del sol de CDMX y Monterrey en 7 fechas de 2026 contra la NOAA (astral 3.2): **lo mas lejos, 0,27 min** (28 de 28 a +-3 min). La luna en las cuatro lunas de los eclipses de 2026: 0,996/0,504/0,002/0,496 frente a 0/0,5/0/0,5 (+-0,03). Los pesos de las paletas suman 1 y cambian como mucho 0,0025 por centesima de grado. Regla 3 de cada paleta (horizonte 0,868 y 667/768): lo mas claro hasta 0,848, **0,125** (manana); de cada minuto de 4 dias en las dos ciudades, 0,124. El calendario de 2026 dia por dia: 28 dias con fiesta, **iguales a §6**; el tercer domingo de junio de 2025-2028 (15, 21, 20 y 18). `rocola_fechas.txt`: 8 renglones buenos y 4 malos de una muestra, cumple, aniversario, quitar (una y todas), fiesta propia con colores, mensaje, fuegos a otra hora y sin fuegos. El clima: Open-Meteo (una respuesta de muestra), `rocola_clima.txt` de ida y vuelta, vale a las 2 h 59 min y no a las 3 h 01, los 17 codigos de la OMM, `ROCOLA_CLIMA`. El lugar: ipapi.co (numeros) y get.geojs.io (texto), una semana. La calma: baja a 0,35 en 0,40 s y vuelve en 2,98 s, como mucho 0,041 de un cuadro a otro |
| `clima` | con el Open-Meteo y el lugar falsos, y sin red | **TODO BIEN (13)**. Con `ciudad_bar=0` se pide el lugar una vez (Monterrey, falso), el clima se pide para ese lugar con los campos de §3, se guardan `rocola_clima_lugar.txt` y `rocola_clima.txt`, y el sol pasa al lugar de internet. Con `ciudad_bar=23` no se pide el lugar y el clima va con Monterrey de la lista; si Open-Meteo contesta 500 se dice y el cielo queda sin saber (despejado). Sin red: un clima guardado de hace 2 h vale; uno de hace 4 h no (despejado). `ROCOLA_CLIMA` no pide nada |
| `apagado` | las cintas de la tarjeta (los cuadros de los 10 a los 11,5 s de la pared de `datos0`) contra la rama principal (424ade7) | **IGUALES** sin `ROCOLA_OS`, con `ROCOLA_OS=1` y `cielo=0 fiestas=0 clima=0 vidrio=0`, y con `ROCOLA_OS=1` y las 23 claves en 0: el mismo degradado del cielo byte a byte (1 de 1, `63b61f9e...`) y la misma forma del cuadro mas comun. El control (DN1 a las 13:00): **DISTINTAS** (el comparador si ve el cielo). Sin `ROCOLA_OS` no se escribe nada; con las cuatro en 0 no arranca nada |
| `noche` | `ROCOLA_CIELO_FECHA=2026-11-18 23:30`, todo encendido, sin clima | **IGUALES** a la rama principal (la cinta), fase noche |
| `calma` | la de verdad, con lo que sabe XP (`ROCOLA_CIELO_CALMA`) | una tecla a los 25 s: en 0,35 a los **0,35 s**; deja de elegir a los 15,1 s; en 1 a los **2,86 s**. (Al arrancar XP cuenta como si se acabara de tocar: 15 s calmado.) |
| `escenas` | 6 horas (6:50, 9:30, 13:00, 17:40, 18:35, 22:00) del 18-11 en CDMX | **TODO BIEN**. La pared casi vacia (`datos0`, mucho cielo libre), la regla 3 hasta 0,848: 0,104 / 0,110 / 0,112 / 0,092 / 0,066 / 0,058 (contraste del blanco de 6,5:1 a 9,7:1). El escaparate (x 560-1000, y 560-650): de 0,020 a 0,114; el disco (x 0-430, y 560-650): de 0,086 a 0,114. El reposo con musica, **dato** (no hay texto en ese cielo; el horizonte sube a 525 y la raya y su resplandor con el): hasta 0,848/0,868 de 525, 0,177 / 0,189 / 0,182 / 0,170 / 0,107 / 0,130, siempre en la fila 509-512, a 13-16 px de la raya (a las 22:00, la noche de XP, ya 0,130). **A las 6:50, 9:30 y 13:00 pasa de 0,17** (por 0,007 a 0,019): es el resplandor de la raya de XP mas claro de dia (DN5, x 2,2) y el cielo claro que baja hasta ella; la paleta sola da 0,125 a esa altura. No hay texto ahi; si la junta lo quiere dentro de 0,17 tambien en el reposo, se baja el factor de DN5 cuando `Escena.Reposo` (o el tope de la paleta cerca del horizonte). «Ver como se veria» por el buzon (6 veces en el escaparate, la pared llena y el reposo); el disco con `ROCOLA_CIELO_FECHA` (6 corridas: el disco se cierra solo si nadie toca, y las teclas quitarian «ver como se veria») |
| `medio` | el cielo a media resolucion (`cielo_media=1`: la imagen intermedia, 36 repintes en 50 cuadros) contra el directo, a las 13:00 | el mismo degradado: las medianas por fila se separan **1 nivel** como mucho |
| `reloj` | el trabajo de cada cuadro (`ROCOLA_CIELO_RELOJ`), NativeAOT, la pared llena con la ficha y luego el escaparate a las 17:40 (dos paletas mezclandose), 62 s, alternando | todo encendido (incluso VI1, VI2, VI4, VI6 y la luna B, que aun no pintan): p50 **2,11 / 2,03** ms, p95 **4,94 / 4,48**, p99 **8,49 / 7,93**, max **14,41 / 16,42**; las cuatro funciones apagadas: p50 **1,92 / 1,92**, p95 **4,99 / 4,84**, p99 **8,05 / 7,55**, max **19,22 / 16,68** (unos 1 800 cuadros cada una). Con llvmpipe (la tarjeta por procesador) y la maquina compartida (carga 7,3 en 4 nucleos): solo dice que el nucleo no cambia el orden de lo que cuesta un cuadro; **nada del A4** |

Ademas: **NativeAOT** publica sin avisos IL/AOT y con los mismos 13 avisos de compilar de `Pruebas/RocolaOs/avisos-base.txt` (archivo y linea);
un arnes (`VistasFondo`, que compila `Codigo/`, `Nativo/`, `Plataforma/` y `RocolaOs/`) compila sin avisos.

Las capturas (PNG, 1024x768; no van en git): `/root/rocola-trabajo/fase11/cielo-nucleo/capturas/` y una copia con mosaicos de las seis horas
(`mosaico-vacia.png`, `-escaparate`, `-pared`, `-disco`, `-reposo`) en el scratchpad de la sesion,
`/tmp/claude-0/-home-user-Rocola/b54ee812-4417-50ce-9f97-015b8aeef72d/scratchpad/cielo-nucleo-capturas/`. Vistas una por una: el dia azul hondo
con las nebulosas hechas velo, la raya clara, el amanecer y el atardecer violetas con la raya calida, la noche de XP; las portadas, la ficha,
la lista y los textos en su sitio y legibles.

**`nivel0.sh`** (un subconjunto, por el tiempo: `previo codigo ganchos escenas aot`, escenas `arranque moneda buscador captura asistente`,
en NativeAOT `arranque buscador`; registros en `/root/rocola-trabajo/fase11/cielo-nucleo/nivel0/` y `.../nivel1/`):

- nivel 0 (sin `ROCOLA_OS`): **11 BIEN, 1 MAL**. `codigo`, `ganchos.py` (69 ganchos en 22 archivos), `diff-xp.py --sin-ganchos` (5135 4868 229,
  los de la base), las escenas arranque, buscador, captura (a la segunda: la primera, un encargo de la bitacora fuera de la holgura) y
  asistente, y en NativeAOT arranque y buscador: **IGUAL**. **moneda: MAL**, y es de la base, no del cielo: la misma escena con la rama
  principal (424ade7, `$T/base-jit`) da el **mismo MAL** (`nivel0-base/`), y sus estados y la captura de la hoja de Ctrl+F3 son **iguales byte
  a byte** a los de esta rama: la referencia congelada (aef2211) es la XP de antes del ecualizador del dueno (el boton «Ecualizador...» en
  Sonido; `PORT_xp-1718.md`: «no se corrio `nivel0.sh` entero» por eso).
- nivel 1 (`ROCOLA_OS=cielo,fiestas,clima,vidrio`, sin red): **8 BIEN, 1 MAL** (moneda, lo mismo). Las trazas (sin los 8 relojes de Rocola OS,
  `--ignora RocolaOs:`), los estados y las capas de Skia, iguales; la capa de la tarjeta no se compara pixel a pixel en las escenas («el cielo se
  mueve»): el cielo lo comparan las cintas de arriba. Sin excepciones en `rocola_error.txt`.
- No se corrieron (tiempo y una sola maquina compartida): las otras 19 escenas, los recorridos, el banco, los arneses de la frontera y lo de la
  hoja. El nucleo no toca nada de eso fuera de la tarjeta (los ganchos solo estan en `D3D9Star.cs`, `PerformanceGpu.cs` y `AtraccionGpu.cs`,
  y sin `ROCOLA_OS` devuelven lo de XP), pero **no esta comprobado**: lo corre la junta.

## Lo no probado y lo pendiente

- **Nada en el A4**: ni como se ve, ni lo que cuesta en su tarjeta (el reloj de arriba es de llvmpipe, en una maquina compartida).
- **Sin la red de verdad**: Open-Meteo, ipapi.co, ipwho.is y get.geojs.io no se probaron (solo los falsos); el formato de sus respuestas es el
  documentado y el del programa anterior.
- **La hoja «Imagen» de Ctrl+F4 no existe** (es de despues, §8): `VerComo(..., desdeLaHoja)`, `QuitaVerComo` y `HojasAbiertas` estan listos
  para ella. «Ver como se veria» por el buzon: hecho el lado del programa (`/run/rocola-cielo-ver`); la orden en `rocola-buzon` (el guion del
  sistema) no se hizo.
- **Las fases de las 17:40 y las 18:35** (decision para el dueno o el coordinador): con los tramos de §3, en noviembre en CDMX las 17:40 son
  0,36 dorada + 0,64 atardecer y las 18:35, 0,67 anochecer + 0,33 noche; las maquetas las llamaban «tarde dorada» y «atardecer». Si se quiere
  lo de las maquetas, se mueven las anclas de la tarde (`RocolaOsCieloDia.AnclaTarde`), no la forma.
- **DN5 sin el resplandor de 70 px de §5** (se hizo lo de las maquetas; arriba el porque).
- **La regla 3 en el reposo con musica**: a 13-16 px encima de la raya (fila 509-512, horizonte en 525) la luz llega a 0,177-0,189 a las 6:50,
  9:30 y 13:00 (de 0,17). Alli no hay texto (el panel AHORA SUENA va mas arriba y tiene su fondo), por eso es un dato y no una falla de la
  bateria; decidirlo en la junta (arriba, como bajarlo).
- **El nivel 0 y el nivel 1 enteros no se corrieron** (solo el subconjunto de arriba); la escena moneda da MAL tambien en la rama principal.
- **La «madrugada» de §5** no se aplica (regla 1).
- **Las fugaces al azar de XP siguen saliendo de dia** (no se atenuan con el brillo de las estrellas: g126 solo toca las estrellas; la de
  festejo tiene que verse siempre). Si el grupo del sol (DN4) quiere apagarlas de dia: `D3D9Star.cs:1445` (`uint la = (uint)(230 * k * dim); ...`),
  en su rango; la estela de varios colores de FI3 es otra linea (`:1451`), en el de las fiestas.
- **Al arrancar, la calma esta en 0,35 los primeros 15 s** (XP pone `lastInput` al construir la rocola: cuenta como si alguien acabara de
  tocar). Es lo de XP; los grupos lo veran en las pruebas.
- **El dibujo por procesador** (sin tarjeta, `gpu=0`) no cambia: sin tarjeta no hay cielo vivo. La ventana de prueba F12 (su propio campo de
  estrellas) pinta el cielo de XP.
- **El vidrio no tiene aun como «ver lo de detras»**: la primitiva (copiar lo pintado a una textura y desenfocarla) es del grupo del vidrio
  (arriba). Las hojas del dueno (Ctrl+F3, Ctrl+F4) no son de la tarjeta.
- **`vivo.txt`**: el renglon `cielo=...` sale cuando 1a escriba el archivo (aqui no esta).
- **Las 20 casillas del nucleo estan llenas** (g120-g139): lo que falte de un enchufe comun va en el rango del grupo que lo pida (o en la
  junta).
- `PLAN_CIELO_VIVO.md` §7 pide tambien «las fechas del calendario, los climas y cada idea sola» en capturas: eso es de cada grupo (el nucleo no
  pinta fiestas ni clima).
