# Cielo vivo, grupo del vidrio: VI1 a VI6

27-11-2026. Rama del grupo «vidrio», juntada con la rama principal (nubes y clima, 0461bf0, y las fiestas). Es `PLAN_CIELO_VIVO.md` §1 y §2: las MISMAS
piezas de XP, en su sitio, con su tamano, colores y letra; solo cambia el material. Solo Rocola OS, solo la tarjeta. **La funcion `vidrio` sigue APAGADA
de fabrica** (`DeFabrica = false`, no se toco: lo decide la junta final). Dentro de ella, VI3 y VI5 van encendidas y VI1, VI2, VI4 y VI6 apagadas hasta
medirlas en el A4. **Nada probado en el A4.**

| Idea (clave) | Que hace |
|---|---|
| VI1 la ficha (`vidrio_ficha`, apagada) | lo de detras de la ficha rapida del disco, desenfocado, y su relleno de XP a 0,82 de su alfa (232 -> 190) |
| VI2 la franja (`vidrio_franja`, apagada) | lo mismo bajo la franja «AHORA SUENA» (232/248 -> 190/203) |
| VI3 las pastillas (`vidrio_pastillas`, encendida) | BUSCAR, AYUDA, la barra de letras, En cola y creditos: brillo de luz arriba y filo; no desenfocan (la pastilla de XP ya es translucida) |
| VI4 la lista (`vidrio_lista`, apagada) | lo de detras del cristal de la lista del disco, desenfocado y con un 30 % del color de la portada (`GlassTint`); su cuerpo 205/218 -> 168/179 |
| VI5 el filo de luz (`vidrio_luz`, encendida) | anillo de 1,2 px sumado al borde de CADA pieza de vidrio, con el color de la raya del horizonte (DN1: el de la hora; de noche o sin DN1, el plateado de XP), mas fuerte arriba |
| VI6 avisos y buscador (`vidrio_buscador`, apagada) | el aviso claro y la pastilla del aviso de color: lo de detras desenfocado, relleno a 0,82 (244 -> 200) |

## Que se hizo

| Archivo | Que lleva |
|---|---|
| `RocolaOs/RocolaOs.CieloVidrioGl.cs` | LA PRIMITIVA: `DispositivoTarjeta.RocolaOsVidrioDesenfoca` (parte del dispositivo) y sus pasos en `GpuStarfield` |
| `RocolaOs/RocolaOs.CieloVidrio.cs` | `RocolaOsVidrio` (el alfa de los rellenos, la lista de piezas del cuadro, el contorno redondo, el color del filo, la cuenta de la regla 3), los tres pintores (capas `AntesDeLaFicha`, `AntesDelEstado`, `AntesDeLaInterfaz`) y lo suyo de `GpuPanel` y `Form1` |
| `RocolaOs/RocolaOs.CieloVidrioPrueba.cs` | `rocola-xp --prueba-vidrio`: el contorno, el alfa con la idea apagada y la regla 3 como cuenta (16/16) |
| `Pruebas/RocolaOs/cielo-vidrio/` | `comun.sh`, `rapida.sh` (una escena a mano) |

## La primitiva del desenfoque

El fondo de la tarjeta es una textura con su framebuffer, asi que no se copia: se muestrea mientras se pinta en OTRO framebuffer (nunca en el que se lee).
Por pieza: fondo -> mitad (estirado bilineal: promedia 2x2) -> cuarto (4x4 en total, sin aliasing) -> 9 tomas separables (horizontal y vertical, 5
lecturas bilineales cada una, pesos 0,2270 / 0,3162 / 0,0703, con los tres programas fijos de XP: la primera sin mezcla y las demas con alfa w/suma) y
se estira de vuelta sobre la pieza, por un abanico del contorno redondo, a 0,78 de brillo. Sigma de ~8 px de pantalla. Coste por pieza: 2 estirados + 10
lecturas + 1 dibujo, todos de menos de 1/4 de la pantalla (la ficha de 480x300 son 120x75 puntos). **Cache**: cada ranura (pieza) recuerda su zona y
el reloj; con la misma zona y menos de 66 ms se reusa sin dibujar nada (15 desenfoques por segundo como mucho). Sin memoria o fuera de la escena, falla en
silencio: la pieza queda como la de XP (`SinDesenfoque`, y el alfa vuelve al de XP en lo que se hornee despues).

## Ganchos (g360-g369)

g360 `DibujaBoleto` (D3D9Star.cs): brillo y filo de las piezas del cuadro, encima. g361 ficha (`PantallaVivaGpu.cs`), g362 franja (`EstadoReposoGpu.cs`),
g363 y g364 lista (`AcabadoGpu.cs`), g365 toast (`Form1.Ajustes.cs`), g366 aviso claro (`SuenaVivoGpu.cs`): el alfa del relleno. g367 y g368 (D3D9Star.cs):
el fondo desenfocado del toast y del aviso, y el filo despues. g369 (`Nativo/DispositivoTarjetaGl.cs`, SueltaFondo): suelta las texturas del vidrio.
Archivo comun tocado: solo esa linea de Nativo. Con la idea apagada (o sin ROCOLA_OS) todos dan lo de XP. Los rellenos se hornean con su pieza: encender o
apagar en vivo vale para lo siguiente que se hornee (la ficha de otro disco, el panel de otra cancion, la lista al abrir un disco).

## Regla 3 (siempre se lee)

`--prueba-vidrio`: sobre el fondo MAS claro posible (todo blanco) el vidrio de cada pieza da luminancia 0,03-0,11 y el blanco de la letra 6,7:1 a 12,8:1
(la regla pide 4,5); el gris azulado mas tenue de la ficha, 4,8:1 (XP: 6,9). Sobre un cielo claro, 0,018. La lista da 3,2:1 al gris tenue sobre blanco
entero (caso que no se da: detras de ella hay cielo y portadas oscurecidas).

## Lo que NO se hizo o no se comprobo

- **Las ventanas del dueno (Ctrl+F3, Ctrl+F4) no**: son hojas de Skia pintadas por procesador y compuestas por `CompositorPantalla`; su vidrio pide
  un relleno translucido en su dibujo (que se queda congelado) o desenfocar en el compositor: decision para el coordinador.
- **El buscador mismo (su ventana grande)**: no se hizo; solo avisos (VI6). Su fondo (8,12,36) opaco es de `SearchOverlay` y sus tarjetas de
  `BuscadorPortadasGpu`; queda para la junta.
- La regla 5 (calma) no tiene nada que calmar: nada del vidrio se mueve solo.
- Nada en el A4; el coste en tiempo se midio solo con llvmpipe (ver abajo).

> **27-11-2026, junta final:** las cuatro funciones del cielo vivo van ahora ENCENDIDAS de fabrica (`DeFabrica = true`); lo que arriba diga «apagada de fabrica» o «en esta version» era de la version anterior. Sus ideas que costaban (VI1, VI2, VI4, VI6) y `luna_camino` siguen apagadas.
