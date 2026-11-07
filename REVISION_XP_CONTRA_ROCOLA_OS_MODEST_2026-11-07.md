# Revisión: la rocola de XP contra Rocola OS (7 de noviembre de 2026)

Pedida por el dueño tras instalar Rocola OS en el A4: «en aspecto no es igual a la rocola XP, le faltan muchísimas cosas de la
versión de XP, está súper recortada, no se escucha la música». Comparé el código de las dos: los 109 archivos de XP (69 000 líneas)
contra los de Rocola OS, las 178 claves de `rocola_config.txt` de XP, las opciones de Ctrl+F3 y cada función que el código de XP
describe. No hay capturas de la de XP en el repositorio: lo visual hay que confirmarlo con fotos de la rocola.

## Lo que ya está igual o casi

- **El sonido (la cadena):** casi todos los módulos de audio de XP tienen su equivalente en Linux (nivelado, EQ, realce, DSEE, modo
  billar y sus etapas, modo noche, silencios, pre-eco, zumbido, eses...). Las 124 opciones con interruptor de Ctrl+F3 de Rocola OS
  están conectadas a algo (ninguna es un interruptor vacío).
- **Funciones:** la moneda que cae, el boleto que vuela a la cola, las barritas en la que suena, cuántas veces se pidió cada canción,
  la pastilla del género, el título que se desliza, la estrellita de TOP, lo más pedido esta noche, la atracción, el estado extendido,
  los videos (con mpv), el código de servicio, la contraseña de Ctrl+F3 y las cuentas.

## Lo grave (bloquea el bar)

1. **No se oye la música.** Arreglo probable ya subido: se apaga el «Auto-Mute Mode» de la tarjeta (calla la salida de atrás si cree
   que hay audífonos) y se suben todas las salidas. Sin confirmar: falta la foto de `cat /proc/asound/cards`, `rocola-tarjeta` y
   `tail -20 /rocola/datos/rocola_error.txt`.
2. **Spotify conectado y sin nada a la vista.** Ya tiene su pestaña y dice si Spotify se niega a dar la biblioteca y con qué código;
   falta la foto de ese recuadro.

## Lo que falta: capas visuales de XP que no existen en Rocola OS

La causa principal de que se vea «recortada». En XP estas capas están ENCENDIDAS de fábrica; Rocola OS no tiene su clave ni, hasta
donde se ve en el código, su efecto (algunas pueden estar hechas de otra forma; hay que confirmarlo con fotos de la de XP):

- **Cristal y transparencias:** lo que suena transparente (`estado_transparente`), la lista de canciones transparente
  (`canciones_transparentes`), el interior de los discos con cristal (`canciones_cristal`), las capas de vidrio (`glass_capas`).
- **Neón y selección:** neón intenso (`neon_intenso`), barra de progreso de neón (`progreso_neon`) y fluida (`progreso_fluido`), halo de
  la selección (`halo_seleccion`), portada enfocada (`portada_enfocada`), brillo de la portada (`portada_brillo`), profundidad al pasar
  de página (`profundidad_pagina`), lo que suena en grande (`suena_grande`), los controles en el reposo (`controles_reposo`).
- **Color:** paleta completa de la carátula (`paleta_completa`), colores del espectro en movimiento (`colores_fluidos`), colores
  profundos (`colores_profundos`), interfaz tramada (`interfaz_tramada`).
- **El cielo:** nebulosas de cine (`nebulosas_cine`), nebulosas con detalle natural (`cielo_profundo`), cielo suave (`cielo_suave`),
  aurora limpia y en bandas (`aurora_limpia`, `aurora_bandas`), reflejo de la aurora (`reflejo_aurora`), ondas suaves
  (`ondas_suaves`), estrellas finas y su densidad (`estrellas_finas`, `estrellas`), viñeta suave y grano (`vineta_suave`,
  `grano_vineta`), nieblas de color (`nieblas`), estrellas fugaces y la de festejo (`fugaces`, `fugaz_festejo`), el relevo del
  patrón cada minuto (`patron`).
- **La luna y la estrella:** luna realista (mares, halo, polvo), luna protagonista, la luna del cielo vivo y la estrella viajera con
  órbitas (`luna_realista`, `luna_protagonista`, `luna_del_cielo`, `luna_activa`); la estrella con degradado, reflejo, destello,
  borde y espejo (`estrella_*`).
- **El asistente:** su animación (`asistente_animado`).

Además, en Rocola OS hay opciones nuevas «de coste medio» APAGADAS hasta medirlas en el A4 (V1, V7, V13, V14, V15, V20, V24, G4,
G9-G11): varias se parecen a lo que XP traía encendido.

## Funciones pequeñas que faltan

- Los créditos que ruedan como un odómetro y la sacudida del aviso «sin créditos».
- «Página 3 de 18» en la cabecera (solo hay puntitos).
- El video en ventana (con la rocola a la vista).
- Apagar, reiniciar o volver a XP desde Ctrl+F3; volver a traer los datos de XP.

## Propuesta de orden

1. Que suene (con la foto del diagnóstico) y Spotify (con la foto de su pestaña).
2. Encender de fábrica las opciones que más se parecen a XP (barato y rápido).
3. Las capas visuales de XP, por pantallas: la pared y las letras; dentro del disco; lo que suena; el cielo; la luna y la estrella.
   Cada una comparada contra fotos de la de XP.
4. Las funciones pequeñas.
5. Antes de volver a instalar, probar en el A4 con la memoria USB en «Probar Rocola OS sin instalar» (no toca el disco).
