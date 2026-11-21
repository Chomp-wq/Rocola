# El cielo de la rocola, otra vez vivo: dia y noche, fiestas, clima y vidrio (plan)

Decision del dueno del 20-11-2026 («Me gustaron todas las ideas, haz todo!»), sobre la presentacion «El cielo de la rocola, otra vez
vivo» (artefacto DZTPpnJqUw6whDPUitEZZm, 36 diapositivas con maquetas hechas sobre capturas reales de rocola-xp). Se hacen las 23
ideas (DN1-DN6, FI1-FI6, CL1-CL5, VI1-VI6) y lo comun (la ciudad del bar, «Ver como se veria», calma al elegir, la hoja «Imagen» de
Ctrl+F4). Solo Rocola OS (rocola-xp), solo GPU (el dibujo por procesador esta congelado), encima del cielo de XP: lo del programa
anterior (H1-H7, F1-F3, O1-O3, G1-G11, sistema/rocola/Consola) NO se trasplanta; de el solo se reusa la logica que no es dibujo
(sistema/rocola/Motor/CieloClima.cs: el sol, la luna, las ciudades, Open-Meteo, el lugar por internet, el calendario).

> **En esta version, apagado de fabrica (junta 4, 21-11-2026).** Decision del dueno al juntar («Cuando termines esto sube a github, ya lo
> demas que entre en otra version»; el cielo vivo va en la siguiente): las cuatro funciones del cielo vivo (`cielo`, `fiestas`, `clima` y
> `vidrio`, `RocolaOs.Cielo.cs`) van con `DeFabrica = false`: sin `cielo=1` (o `fiestas=1`, `clima=1`, `vidrio=1`) en `rocola_os.txt` no
> arranca nada del cielo, no cambia nada a la vista y no se pide el lugar ni el clima por internet. Sus claves no cambian. **Vuelven a
> encenderse de fabrica (`DeFabrica = true`) en la version del cielo**, cuando entren los cuatro grupos y la hoja «Imagen» de Ctrl+F4. La
> bateria `Pruebas/RocolaOs/cielo` las enciende ella con `ROCOLA_OS=1` y comprueba lo de fabrica aparte (`CIELO_DE_FABRICA=1`).

## 1. Las seis reglas (de la presentacion; mandan sobre todo lo demas)

1. **La noche es la de XP.** Con el sol bajo -12 grados (noche cerrada), el cielo sale bit a bit como hoy (salvo lo que pinte una
   idea encendida que de noche si se ve: la luna B, las nubes, el clima, la fiesta).
2. **Nada cambia de lugar.** Portadas, ficha, barras, botones, letra y textos se quedan donde estan y como estan.
3. **Siempre se lee.** Hasta 0,848 del alto (la ultima fila de titulos de la pared acaba hacia 650/768) el cielo no pasa de
   luminancia relativa 0,17 (texto blanco a 4,5:1 o mas); lo claro va en la franja fina del horizonte (0,853-0,868) y debajo el
   «suelo» oscuro. Se mide en la bateria (seccion 7).
4. **Lo vistoso, donde se ve.** Lo grande (papel picado, globos, fuegos, pastilla del clima) va al escaparate (la atraccion) y, cuando
   se dice, al disco abierto y al reposo con musica. Nada encima de una portada ni de un texto.
5. **Se calma al elegir.** Mientras alguien usa la rocola (un boton hace menos de 15 s, o el buscador, la ayuda, el asistente,
   Ctrl+F3 o Ctrl+F4 abiertos) lo que se mueve en el cielo nuevo (lluvia, fuegos, nubes, relampagos, adornos) baja a 0,35 en 0,4 s y
   vuelve a 1 en 3 s. Lo de XP (aurora, estrellas, fugaz) no cambia por esto.
6. **Interruptor y probarlo.** Cada idea lleva su interruptor persistente; todas apagadas (o sin ROCOLA_OS) = XP identica, comprobado
   como el port (cintas byte a byte). «Ver como se veria» ensena cualquier hora, fecha o clima 30 s.

## 2. Funciones, claves y valores de fabrica (rocola_os.txt; RocolaOs.Ajustes.cs)

Cuatro funciones del andamio (RocolaOs.Registra), cada una con sus claves (RocolaOsClave.SiNo, EnVivo = true salvo donde se dice):

| Funcion (su clave) | Claves | De fabrica |
|---|---|---|
| `cielo` (dia y noche y lo comun) | `cielo_sol` DN1, `sol` DN2, `luna_camino` DN3 (0 = A, la de XP), `estrellas_anochecer` DN4, `horizonte_hora` DN5, `nubes` DN6, `ciudad_bar` (Entero 0-41: 0 automatica por internet, si no una de la lista de CieloClima.Ciudades) | todas 1 salvo `luna_camino`=0; `ciudad_bar`=0 |
| `fiestas` | `aurora_fiesta` FI1, `horizonte_fiesta` FI2, `fugaz_fiesta` FI3, `fuegos` FI4, `escaparate_fiesta` FI5, `fechas_propias` FI6 | todas 1 |
| `clima` (su clave es CL1: las nubes de afuera) | `lluvia` CL2, `tormenta` CL3, `neblina` CL4, `clima_escaparate` CL5 | todas 1 |
| `vidrio` | `vidrio_ficha` VI1, `vidrio_franja` VI2, `vidrio_pastillas` VI3, `vidrio_lista` VI4, `vidrio_luz` VI5, `vidrio_buscador` VI6 | VI3 y VI5 1; VI1, VI2, VI4 y VI6 **0** hasta medirlas en el A4 |

Las funciones apagadas en rocola_os.txt (`cielo=0`, `clima=0`...) apagan todo lo suyo. Dependencias (en gris en la hoja, diciendo por
que, como pide la regla del 16-10-2026): DN2-DN6 y VI5 «calido» necesitan la hora del sol (siempre la hay: se calcula en la rocola);
CL2-CL5 necesitan `clima`; las nubes del clima son las de DN6 (con `nubes=0` el clima solo pone lluvia, neblina y la pastilla); VI5
sin DN1 deja el filo en el plateado de la noche de XP.

Archivos de datos (en la carpeta de datos de rocola-xp, /rocola/datos/xp; nunca llaves; solo los escribe el programa):
- `rocola_clima.txt`: el ultimo clima (hora UTC de la medida, codigo WMO, nubes %, lluvia mm/h, viento km/h y direccion, temperatura,
  visibilidad); vale 3 h.
- `rocola_clima_lugar.txt`: el lugar por internet (lat, lon, ciudad, fecha), una vez por semana.
- `rocola_fechas.txt`: las fechas propias (FI6) y los cambios del calendario, un renglon por fecha: `DD-MM;tipo;texto` (tipo
  `cumple`, `aniversario`, `fiesta`, `quitar`); se edita en Ctrl+F4 -> Imagen o en Ctrl+F4 -> Archivos (no es una clave: la orden
  «ajuste» del buzon no lo toca).
- Mensajes y horas de las fiestas cambiados por el dueno: en `rocola_fechas.txt` (tipo `mensaje`, `fuegos`).

## 3. Lo que se calcula (el nucleo, sin dibujo)

`RocolaOs.Cielo.cs` (estado) y `RocolaOs.CieloAstro.cs`, `RocolaOs.CieloClima.cs`, `RocolaOs.CieloFiestas.cs`:
- **La hora del sol y de la luna** de la ciudad del bar: Astro.Sol y Astro.Luna de Motor/CieloClima.cs (traidos, sin JsonElement si
  NativeAOT se queja: la rocola ya tiene su lector JSON en RocolaOs.Spotify*). Altura, acimut y fase.
- **La fase del dia** por la altura del sol (interpolando sin saltos entre las paletas de la seccion 5): noche < -12; alba (manana) /
  anochecer (tarde) de -12 a -4; amanecer / atardecer de -4 a +3; manana / dorada de +3 a +15; manana de +15 a +35 por la manana;
  mediodia > +35 (si el sol no llega a 35, el mediodia es la manana). Se recalcula en el latido (1 s) y el cuadro interpola.
- **La ciudad**: `ciudad_bar` o la de internet (ipapi.co, ipwho.is, get.geojs.io; una vez por semana; sin internet, la ultima, y sin
  ninguna, Ciudad de Mexico).
- **El clima** (CL1): Open-Meteo `current=` (weather_code, cloud_cover, precipitation, rain, showers, snowfall, wind_speed_10m,
  wind_direction_10m, temperature_2m, visibility, is_day) cada 15 min en un RocolaOs.Hilo (nunca en el hilo de la pantalla); al
  cambiar de ciudad, al momento. Tipos: despejado, poco nublado, nublado, neblina, llovizna, lluvia, aguacero, tormenta, nieve
  (DeCodigo). El ultimo vale 3 h; despues, despejado.
- **La fiesta de hoy** (calendario de la seccion 6) y las fechas propias.
- **La calma** (regla 5): un numero de 0,35 a 1 que lee todo lo que se mueve.
- **«Ver como se veria»**: fecha, hora y clima forzados 30 s (no se guarda; se quita al tocar cualquier boton que no sea de la hoja,
  al cerrar Ctrl+F4 o a los 30 s). Para las pruebas, como en el programa anterior: `ROCOLA_CIELO_FECHA` (fecha y hora local fijas,
  `AAAA-MM-DD HH:MM`), `ROCOLA_CLIMA` (un tipo, o `tipo,nubes,lluvia,viento,direccion,temperatura`), `ROCOLA_CLIMA_API` y
  `ROCOLA_LUGAR_API` (servidores falsos), `ROCOLA_CIUDAD` (lat,lon).

Todo el estado que lee el dibujo es una foto inmutable que el latido cambia entera (sin cerrojos en el cuadro, sin reservar memoria
por cuadro).

## 4. Los enchufes en el codigo de XP (ganchos)

Cada cambio en Codigo/ es un gancho de una linea `// ROCOLA OS gNNN: (XP) ...` en GANCHOS.tsv (Pruebas/RocolaOs/ganchos.py), que sin
ROCOLA_OS o con la idea apagada hace exactamente lo de XP. Rangos para que los grupos no choquen (los de la segunda compilacion usan
hasta g101; los de pantalla-ctrlf4, g80-g82, chocaban con los del asistente de la rama principal y en la junta 2 pasaron a g85-g87):

| Rango | Grupo |
|---|---|
| g120-g139 | nucleo: los enchufes comunes, DN1 y DN5 |
| g140-g149 | sol, luna y estrellas (DN2, DN3, DN4) |
| g150-g169 | nubes y clima (DN6, CL1-CL5) |
| g170-g189 | fiestas (FI1-FI6) |
| g190-g209 | vidrio (VI1-VI6) |
| g210-g219 | la hoja Imagen de Ctrl+F4 |

Puntos ya vistos (el nucleo los confirma y pone los enchufes; cada grupo pone despues sus ganchos en su rango o usa el enchufe):
- Codigo/D3D9Star.cs, GpuStarfield.Begin (hacia la linea 1188): los tres colores del cielo (skyTop/skyMid/skyBot, CorteDelCielo, el
  tinte CieloHora con HoraHondo/HoraCalor, ColoresCielo de PulidoGpu), CieloBase/CieloDirecto (cielo a media resolucion de
  PerformanceGpu y las 12 franjas de CieloSuave: los dos caminos deben dar el mismo cielo), TrasCielo (la aurora), el brillo de las
  estrellas (`dim`), TrasEstrellas (la luna), UpdateAndDrawShoots (las fugaces y Festeja).
- Codigo/CieloVivoGpu.cs: SitioLuna (DN3 A/B), CieloHora (DN1 lo sustituye mientras esta encendido), LluviaDe (lluvias de estrellas de
  XP, se quedan), Festeja (FI3).
- La aurora (GpuAurora: Rgb/Rgb2/RgbA/Rgb2A, el color del genero): FI1 cambia solo sus colores.
- La raya del horizonte y su reflejo (DN5, FI2, CL4): donde XP los pinta.
- Codigo/AtraccionGpu.cs: el escaparate y sus mensajes que turna (FI5, FI6, CL5; FI4 los fuegos detras).
- Codigo/PantallaVivaGpu.cs: la ficha (GpuFicha, VI1), el estado extendido y la franja «AHORA SUENA» (VI2).
- Las pastillas (BUSCAR, AYUDA, barra de letras, En cola, creditos: VI3), la lista del disco (VI4), el buscador, los avisos y las
  hojas del dueno (VI6).
- La pared horneada usa el mismo cielo que el directo (lo dice el comentario de CorteDelCielo): el cielo nuevo tambien.

## 5. Las paletas (de las maquetas; y, y en fraccion del alto, para el horizonte de XP en 0,868)

| Fase | Paradas (y, color) | Raya del horizonte | Estrellas | Nebulosa | Velo |
|---|---|---|---|---|---|
| noche | la de XP exacta (0,06081A / 0,42 101C3A / 0,868 color del genero) | la de XP | 1 | 1 | 0 |
| madrugada | la de XP | la de XP | 1,1 | 0,9 | 0 |
| alba | 0 #0b1233, 0,42 #1d2a63, 0,80 #34367c, 0,853 #4a4088, 0,862 #7a5aa6, 0,868 #a07ac0, 0,876 #3a2d5a, 1 #221a38 | #a888d8 | 0,45 | 0,55 | 0,08 |
| amanecer | 0 #142a66, 0,40 #2c4690, 0,80 #4a4a8e, 0,853 #6a5490, 0,862 #d08c88, 0,868 #f8b490, 0,876 #6a4e6c, 1 #30263c | #ffc8a0 | 0,08 | 0,35 | 0,2 |
| manana | 0 #0e3778, 0,42 #21529a, 0,80 #2b5d9c, 0,853 #3165a4, 0,862 #7eb0e0, 0,868 #acd4f4, 0,876 #3a6290, 1 #223c5c | #e6f4ff | 0 | 0,30 | 0,34 |
| mediodia | 0 #0b306f, 0,42 #1f4e98, 0,80 #295a9c, 0,853 #3064a4, 0,862 #7cb0e2, 0,868 #acd6f6, 0,876 #355e8e, 1 #203a5a | #f0f8ff | 0 | 0,28 | 0,36 |
| dorada | 0 #17336e, 0,42 #33569a, 0,80 #4a5290, 0,853 #6a5a86, 0,862 #d49a68, 0,868 #f6c274, 0,876 #6a5050, 1 #322a3a | #ffd590 | 0 | 0,32 | 0,24 |
| atardecer | 0 #131f52, 0,40 #33306e, 0,78 #5e3672, 0,853 #7a3c6a, 0,862 #e06a50, 0,868 #ffa054, 0,876 #5a3046, 1 #2a1c34 | #ff9a58 | 0,12 | 0,35 | 0,16 |
| anochecer | 0 #080c28, 0,42 #19225c, 0,80 #352a6a, 0,853 #4a2e6a, 0,862 #9a4874, 0,868 #c0547c, 0,876 #2f1d3c, 1 #181226 | #e07098 | 0,55 | 0,7 | 0 |

- Con otro horizonte (otra resolucion, otro CorteDelCielo), las paradas se corren: lo de arriba se encoge o se estira hasta el
  horizonte y el suelo va de ahi hasta abajo. El color del genero de XP abajo se conserva (de dia, mezclado al 35 % en el suelo).
- El «velo»: de dia la nebulosa de XP se aclara y pierde color, como cirros (nebulosa x (1 - velo) + su luminancia x 3,2 en
  #c7d6f2 x velo x 0,55). La aurora (la musica) se queda entera a cualquier hora.
- El sol (DN2): disco de radio 0,028 del alto, nucleo #fff6dc, halo #ffc070 (dos halos: r x 2,2 a 0,55 y r x 7 a 0,22). En la pared
  (y en todo lo que tenga portadas encima), al salir y al ponerse solo asoma sobre la raya del horizonte con el halo x 0,22, y la tarde
  dorada va detras de una portada de la derecha con el halo x 0,42; nunca detras de un titulo. En el escaparate, en su camino.
- La raya del horizonte (DN5): resplandor que sube 70 px (exp(-1,8 d)) mas un filo de 2,2 px, mas fuerte en el centro, con el color
  de la raya de la fase.

## 6. El calendario de las fiestas (FI1-FI5; sin internet)

| Fecha | Colores | Ademas |
|---|---|---|
| 31-12 y 1-1, Ano Nuevo | oro y plata | fuegos a las 00:00 tras una cuenta de 10 s |
| 6-1, Reyes | oro y azul | una estrella guia en el horizonte |
| 2-2, Candelaria | rojo y blanco | farolitos en el horizonte |
| 14-2, Amor y amistad | rosa y rojo | corazones en el escaparate |
| 24-2, Bandera | verde, blanco y rojo | la bandera en el escaparate |
| 8-3, Dia de la Mujer | morado | flores moradas en el horizonte |
| 30-4, Dia del Nino | colores de feria | globos en el escaparate |
| 10-5, Dia de las Madres | rosa y blanco | flores en el horizonte |
| tercer domingo de junio, Dia del Padre | azul y oro | globos en el escaparate |
| 15 y 16-9, Independencia | verde, blanco y rojo | papel picado; fuegos del Grito el 15 a las 23:00 |
| 31-10 a 2-11, Muertos | cempasuchil y morado | veladoras y flores en el horizonte |
| 20-11, Revolucion | verde, blanco y rojo | papel picado |
| 22-11, Santa Cecilia | oro y rojo | notas que suben con la aurora |
| 12-12, Guadalupana | azul y rosa | rosas en el horizonte |
| 16 a 24-12, posadas | naranja y oro | farolitos y luces en el horizonte |
| 24 y 25-12, Navidad | rojo, verde y oro | luces en el horizonte |

Fuera de las fechas, nada (la aurora con el color del genero de XP). Los fuegos (FI4) duran 4 min, detras de todo, nunca detras de un
texto, como mucho 3 destellos por segundo y al 35 % si alguien elige. Los mensajes van entre los que XP turna en el escaparate
(«¡Viva Mexico! Pon tu ranchera favorita»...), con la misma letra y la misma forma.

## 7. Pruebas (cada grupo, antes de su junta)

- **Apagado = XP**: sin ROCOLA_OS, y con ROCOLA_OS=1 y todas las claves de las cuatro funciones en 0, las cintas de la tarjeta, el
  banco y las escenas salen iguales que la rama principal (byte a byte, como el port: PORT_JUNTAS.md).
- **De noche = XP**: con `ROCOLA_CIELO_FECHA` a las 23:30 de un dia sin fiesta, sin clima y con todo encendido, el cielo de la pared
  igual que XP salvo lo que pinta la luna B (apagada de fabrica).
- **Escenas** (Pruebas/RocolaOs/cielo/, capturas fuera de pantalla con llvmpipe): escaparate, pared, reposo con musica, disco
  abierto y buscador a las horas de la presentacion (6:50, 9:30, 13:00, 17:40, 18:35, 22:00), las fechas del calendario, los climas
  (Open-Meteo falso) y cada idea sola; el contraste medido (regla 3) en cada una.
- **El reloj**: p95/p99/max de los cuadros con todo encendido frente a todo apagado (la regla del 9-10-2026), en NativeAOT; no
  afirmar fluidez en el A4.
- **Ganchos**: ganchos.py y `Pruebas/Tarjeta/guiones/diff-xp.py --sin-ganchos`; NativeAOT sin avisos nuevos.

## 8. Orden y grupos

1. **Nucleo** (rama `cielo-nucleo`, desde la principal): seccion 3 entera, los enchufes de la seccion 4, DN1, DN5 y la calma, con su
   bateria y su junta.
2. **Cuatro grupos en paralelo** sobre el nucleo, cada uno en su rama y en sus archivos (RocolaOs.Cielo<Grupo>*.cs) y su rango de
   ganchos: sol, luna y estrellas (DN2-DN4); nubes y clima (DN6, CL1-CL5); fiestas (FI1-FI6 y rocola_fechas.txt); vidrio (VI1-VI6).
   Cada uno con su revision independiente.
3. **La hoja Imagen de Ctrl+F4** (sobre la junta de la segunda compilacion: pantalla-ctrlf4 y navegador-archivos): los interruptores
   de las cuatro funciones en hojitas por familia (Dia y noche, Fiestas, Clima, Vidrio), en gris con el motivo cuando no pueden; la
   ciudad; las fechas propias; «Ver como se veria» (hora, fecha, clima; 30 s).
4. **Junta** de todo en la rama principal, baterias completas, y la compilacion COMPLETA del sistema (la pide el navegador de
   Ctrl+F4: WPE y cog).

No probado en el A4: ninguna de estas ideas se ha visto alli. Las de vidrio que desenfocan van apagadas hasta medirlas con el
diagnostico completo.
