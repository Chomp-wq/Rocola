# Modo automático del local: revisión y propuestas (25-10-2026)

Petición: «El modo automático para el local, revisa si no se le pueden hacer mejoras».

Qué hace hoy (`LocalAuto.cs`, Ctrl+F3 → Sonido → El local y sus bocinas → Tono y presencia): con `local_auto=` encendido decide siete
mandos según el tipo de local (`tipo_local=`) y lo que el repaso midió de cada canción.

| Tipo | Subgrave | Estéreo | Claridad | Partes suaves | Agudos | Presencia |
|---|---|---|---|---|---|---|
| Bocinas repartidas del billar | se quita | estrecho solo si hace falta | sí | siempre | 0 | si LRA ≥ 10 LU |
| Sala con bocinas buenas | — | — | — | si LRA ≥ 14 LU | 0 | — |
| Bocinas chicas | se quita | — | sí | siempre | +1 dB | — |

Nada de esto cambia con el modo apagado (de fábrica): la cadena sale bit a bit como antes.

## Lo que se revisó y está bien

- **Las partes suaves y la presencia miden contra un nivel fijo** (el de «Videos y canciones aún sin medir», −11,7 dBFS de fábrica), no
  contra el de cada canción. Con el nivelado al límite las canciones ya no quedan todas en ese nivel (de −7 a −15 LKFS en las pruebas),
  pero está bien así: en un billar lo que importa es si lo bajito se oye por encima del ruido del local, y eso es un nivel absoluto.
- **T1** (el margen por graves y agudos) usa lo que decide el automático (los +1 dB de agudos de las bocinas chicas bajan 0,25 dB).
- **S5** («Lo que suena ahora») ya dice qué decidió para la canción que suena.

## Un fallo encontrado

**En un disco continuo, la presencia y las partes suaves pueden cambiar de una pista a la siguiente.** El automático decide por canción
(por su LRA). Si una pista lleva presencia y la siguiente no, la segunda hereda la ganancia de la primera y la suelta en unos 40 ms: en el
cambio de pista, justo donde no debe haber escalón (`pruebas/cadenaentera.cs`), el volumen salta hasta 3 dB (presencia) o 4 dB (partes suaves de la sala buena). Pasa solo con el modo
encendido, en el billar o la sala buena, en discos sin pausa entre pistas (en vivo, mezclados, conceptuales).

## Propuestas

| | Qué | Con qué interruptor | Cambia el sonido de fábrica |
|---|---|---|---|
| **L1** | **Arreglar el fallo:** en un disco continuo, el automático decide la presencia y las partes suaves por el disco (el LRA más alto de las pistas medidas de la racha, como ya hace el nivel por disco), así no cambian a media racha | ninguno (es un arreglo; solo actúa con el modo encendido) | no |
| **L2** | **Bocinas chicas: el bajo psicoacústico.** Las bocinas chicas no dan menos de 80–100 Hz; el bajo psicoacústico hace que el oído «oiga» ese grave por sus armónicos. Con este tipo, el automático le daría más (por ejemplo +50 % sobre lo que ya decide «Graves (bajo profundo)») | regla del modo automático, sin interruptor propio (como P1 y P2) | no |
| **L3** | **Bocinas chicas: quitar más subgrave.** Hoy se corta por debajo de 30 Hz en todos los tipos. En bocinas chicas, cortar en 60 Hz libera amplificador y evita que el cono se ahogue con lo que no puede dar (junto con L2, el grave se sigue «oyendo») | regla del modo automático | no |
| **L4** | **Suavizar las eses cuando el automático sube el brillo:** encender T9 en las bocinas chicas (+1 dB de agudos, tweeters duros) y en el billar con presencia (que acerca la voz) | regla del modo automático; T9 a mano sigue igual | no |
| **L5** | **Umbrales según tu biblioteca.** Los 10 y 14 LU se eligieron de oído. En su lugar: presencia para el tercio de canciones que más varía en ESTA biblioteca, y partes suaves de la sala buena para el 15 % que más varía. `rocola_calidad.txt` ya da el reparto | regla del modo automático | no |
| **L6** | **Un cuarto tipo: «Bocinas buenas con subwoofer».** Grave en mono más alto (el sub lo da todo), sin cortar subgrave, nada de claridad | se elige con el botón de tipo de local | no |

**Mi recomendación:** L1 sí o sí (es un fallo). L2, L3 y L4 si en el bar hay o habrá bocinas chicas; L4 también ayuda en el billar.
L5 y L6 son opcionales: L5 vale la pena cuando el repaso haya medido el LRA de casi toda la biblioteca (ver `rocola_calidad.txt`).

Ninguna se ha implementado aún: se espera la elección del dueño. Todo es con señales y reglas; nada se ha oído en el bar.

## Lo que eligió el dueño y lo que se hizo (25-10-2026)

«L1 y L5 si es lo que mejorará la calidad de sonido, hazlas, y asegúrate que el modo automático sea solo para mi configuración de bocinas,
las demás no me importan, que sea un modo automático especial para mi local, perfectamente configurado.»

- **Solo para el billar.** Se quitaron los tipos de local (sala con bocinas buenas, bocinas chicas) y el botón que los cambiaba; en su
  sitio, Ctrl+F3 dice «Hecho para tu billar: bocinas repartidas por zonas, sin subwoofer». `tipo_local=` se ignora y ya no se guarda.
  L2, L3, L4 y L6 eran para otros tipos de bocinas: no se hacen.
- **Lo que pone siempre:** sin subgrave (ninguna bocina del billar da menos de 30 Hz), estéreo estrecho solo donde hace falta (las
  bocinas por zonas), claridad, partes suaves, graves y agudos en 0.
- **L5, la presencia según tu biblioteca:** la llevan las canciones del tercio que más varía de volumen de TU música (el percentil
  66,7 del LRA), con el corte entre 6 y 14 LU para que nunca se ponga a todas ni a ninguna. Con menos de 200 canciones con LRA medido,
  10 LU como antes. Se recalcula como mucho cada 10 minutos mientras el repaso mide; `rocola_calidad.txt` dice el corte que salió y
  cuántas canciones lo pasan.
- **L1, sin escalón en los discos:** en racha del mismo disco decide el disco entero (su pista que más varía); lo decidido se fija al
  empezar cada canción (no cambia a media canción aunque cambie el corte), y en un disco sin pausas la pista siguiente hereda lo de la
  anterior.
- Con el modo apagado (de fábrica) la cadena sale bit a bit como antes. Pruebas: `localauto` (la regla, el corte con bibliotecas de
  mentira, los 10 minutos, lo fijado y lo heredado), `modosauto`, `cadenaentera`, `compara-cadena`.
