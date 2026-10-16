# Dependencias y conflictos de los gráficos de Modest

Petición del 16 de octubre de 2026: completar la regla A1 de
`AJUSTES_CTRL_F3_REVISION_MODEST_2026-10-13.md` y la de grises de
`J1_J2_Y_GRISES_PERFORMANCE_MODEST_2026-10-14.md` para los ahorros nuevos.
Solo `claude/modest-carson-fa2v2n`, destino .NET Framework 3.5.

Había comprobaciones parciales: los ahorros del estado miraban su preferencia,
pero no el requisito padre «Ocultar discos en reposo con musica». Si ese padre
estaba apagado, el estado nunca podía abrirse aunque su preferencia siguiera en
uno. Además, algunos motivos agrupaban varios interruptores sin precisar cuál
faltaba. Se completa esa cadena y se usan los mismos motivos en sus mandos de
Fondo. No se cambia el renderizado, el audio ni los valores de las preferencias.

## Reglas aplicadas en Ctrl+F3

| Ajuste | Requisitos o conflicto que explica cuando queda en gris |
|---|---|
| Estado extendido opaco | Estado ampliado y ocultar discos en reposo, ambos en Imagen. |
| Color directo del estado extendido | Los dos requisitos anteriores. |
| Sin reflejo de aurora en reposo | Ambos requisitos del estado; aurora en Medio/Alto; Reflejo de la aurora en Fondo / Cielo vivo. Con estado opaco se indica que ya tapa ese reflejo. |
| Sin estela de estrella en reposo | Ambos requisitos del estado; Astro principal en el cielo en Detalles; Estrella viajera con orbitas y Ecos de color de la estrella en Fondo. Indica el primero que falte. |
| Corona de estrella compacta | Astro principal en el cielo y Corona estelar. No necesita el estado ampliado ni el movimiento. |
| Aurora con menos columnas | Aurora del horizonte en Medio o Alto. No necesita el estado ampliado. |
| Estado ampliado transparente | Ambos requisitos del estado; con el ahorro opaco indica «Sin efecto: estado opaco (Performance)». |
| Cola ampliada y Ocultar controles en reposo | Ambos requisitos del estado, también cuando solo falta el padre indirecto. |
| Bandas de aurora, Ondas suaves, Reflejo de la aurora, Aurora más limpia | Aurora del horizonte en Medio o Alto. |
| Los cuatro ahorros previos de cielo/grano | Se conservan sus requisitos y conflictos; si el panel no dispone de la capacidad gráfica necesaria, indica que la tarjeta no admite ese ahorro. |

Sin GPU se conserva el aviso común de la página y los mandos exclusivos de GPU
quedan en gris. Los motivos leen las preferencias, sin depender de que la página
del requisito esté visible. Cambiar interruptores, niveles o presets refresca las
dependencias por los eventos normales de la ventana.

El gris **no desmarca ni modifica lo guardado**. Al restaurar el requisito, el
mando vuelve con su valor anterior. No se crean dependencias circulares: «Sin
estela en reposo» no deshabilita «Ecos de color», ni el ahorro de reflejo bloquea
su mando global, porque estos efectos siguen actuando al navegar. Tampoco se
anulan la corona compacta o la aurora ligera por tener apagado el estado ampliado.

## Comprobación

- GPU y CPU compilan con referencias oficiales de .NET 3.5, C# 3 y advertencias
  como errores; el renderizador CPU permanece intacto.
- `ajustesvivos`: 87 comprobaciones. Añade cambios entre páginas con controles
  reales, padre indirecto apagado con hijo encendido, conflicto opaco/reflejo,
  requisitos separados de estrella/órbitas/ecos/corona/aurora, vuelta automática,
  conservación de preferencias y ausencia de bloqueo de los controles globales.
  Conserva las comprobaciones de límites y texto con una fuente ancha de Linux.
- `estadoeficiente`: 138 comprobaciones. Incluye panel sin las capacidades para
  los ahorros de cielo/grano y comprueba que esto no anula la aurora ligera.
- `performance` conserva sus 110 comprobaciones. El paquete incluye el resultado
  de las 14 baterías relacionadas sobre el ejecutable distribuido en `VALIDACION.txt`.

Pruebas realizadas en Linux/Mono con controlador artificial y datos aislados;
no equivalen a ejecutar en Windows XP ni a una inspección visual de su GPU real.
Esta revisión cambia disponibilidad y explicaciones de los mandos: no aporta
una medición nueva de fluidez. La comparación sintética del documento anterior
corresponde a las optimizaciones de `1828bd2`.
