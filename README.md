# Rocola

Rocola digital para Windows (WinForms + NAudio): pantalla completa, manejo por
botonera/teclado físico (sin depender de un teclado de PC), monedero con créditos,
búsqueda con teclado en pantalla, sección de videos (Windows Media Player), portadas
en cubos 3D, fondo estrellado por género, estadísticas ("Lo más sonado" + TOP por
artista) y modo atracción.

**Versión 2.5.** Esta rama es la **edición clásica (XP)**: la rocola física original.

## Ramas

- **Clásica (XP)** — *esta rama*, la de referencia: .NET Framework **3.5 sin SP1**,
  Windows XP 32-bit, CPU modesto. Todo lo nuevo nace aquí y se prueba en el fierro real.
- **`rocola-hd`** — para PC **potente** (Windows 7+/10, 4+ núcleos, 4+ GB): retargeteada
  a **.NET Framework 4.8**, con salida WASAPI flotante, reconstructor espectral fino,
  render por GPU (Direct3D 9) y **DSEE ULTRA** (IA Apollo con selector de GPU). Mismo
  programa y misma lógica de audio; solo cambia lo que el fierro potente puede aprovechar.

## Qué hace (v2.5)

**Operación (local):**
- Se maneja con la botonera/teclado físico configurable; nada de "lenguaje de teclado de PC".
- Monedero con créditos y cola de canciones pagadas.
- Buscador con teclado en pantalla; sección de videos; portadas en cubos 3D girando.
- "Lo más sonado" y TOP por artista; modo atracción cuando nadie la usa.
- **Blindaje de kiosco**: una excepción no controlada se anota en `rocola_error.txt`
  y la rocola **sigue corriendo** (ya no muere con el diálogo de error de .NET).

**Sonido:**
- **Nivelado perceptual** (tipo LUFS / BS.1770, con *K-weighting*): todas suenan parejo;
  sube las bajitas y **no** baja las que ya suenan fuerte. Se auto-aprende por canción.
- **Subir TODO el volumen**: dB extra generales; el **limitador con pre-escucha** baja los picos
  suave y ANTES de que lleguen (fuerte sin distorsión).
- **EQ automático**: corrige el tono de cada canción hacia el promedio de la biblioteca.
- **DSEE** (realce/reconstrucción de agudos): recrea el "aire" que el MP3 recorta.
  Insignia **MEJORADA** (dorada, que brilla) cuando de verdad está mejorando la canción.
- **Des-recorte (declipper)**: repara los picos aplastados "de fábrica" de archivos mal
  masterizados (reconstruye el pico redondo perdido; `declip=` en config).
- **Fundido** suave de entrada/salida entre canciones (`fundido=` en config, 0-3 s).
- **Dither TPDF** al bajar a 16 bits (salida WaveOut de XP): sin ruido de truncado.
- **Respaldo automático** de lo aprendido (volúmenes, EQ, estadísticas, blacklist, config)
  a otra carpeta/disco; conserva los 3 respaldos más recientes.

**Función de desarrollador (dueño):**
- **Blacklist**: mantener **ROJO + ARRIBA + ABAJO** por 5 s veta la canción que **suena**.
  Desaparece de las listas y del buscador al instante (pero **sigue sonando** hasta terminar) y
  **respeta las copias ya pagadas en la cola**. Al reiniciar, esos archivos se **borran del disco**.

## Compilar

Proyecto clásico de **.NET Framework 3.5** (Visual Studio). Abre `Rocola.slnx` y compila en
Release. El resultado (`Rocola.exe` + `NAudio.dll`) se copia junto a la carpeta `musica`, como siempre.

- Código: `Rocola/Form1.cs` (monolítico) + `Form1.Designer.cs` + `Program.cs` + `D3D9Star.cs`.
- Dependencia: **NAudio 1.9.0** (net35): la DLL viene **incluida** en `packages/` y el proyecto
  la referencia directo — compila **sin NuGet y sin internet** (ya no hay `packages.config`).

> `bin/`, `obj/`, `.vs/` y el resto del paquete NuGet no se versionan (ver `.gitignore`);
> solo se incluye la DLL de NAudio que el `HintPath` necesita.

## Optimizaciones aplicadas (sin cambiar aspecto ni comportamiento)

1. **Estabilidad — `PlayTrack`**: si varias canciones seguidas no abren (archivos
   movidos/corruptos), antes se apilaba `PlayTrack → PlayNext → PlayTrack…` (posible
   *StackOverflow*). Ahora el `PlayNext` del `catch` va por `BeginInvoke`, que desenreda la pila.
2. **Búsqueda — `Score` / `RecomputeResults`**: `" " + q` y `q.Split(' ')` se calculaban
   dentro de `Score` (corre por **cada** entrada del índice en **cada** tecla). Ahora se calculan
   una sola vez por pulsación (puntaje idéntico), evitando decenas de miles de asignaciones.
3. **Render — `Cube.Draw`**: los tres arreglos de la proyección 3D (`ex`, `depth`, `order`)
   se reutilizan vía `[ThreadStatic]` en vez de asignarse en cada llamada.
