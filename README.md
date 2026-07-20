# Rocola

Rocola digital para Windows (WinForms + NAudio): pantalla completa, manejo por
botonera/teclado físico (sin depender de un teclado de PC), monedero con créditos,
búsqueda con teclado en pantalla, sección de videos (Windows Media Player), portadas
en cubos 3D, fondo estrellado por género, estadísticas ("Lo más sonado" + TOP por
artista) y modo atracción.

**Versión 2.5.** Esta rama es la **edición HD** (`rocola-hd`), para PC potente.

## Ramas

- **Clásica (XP)** — la rocola física original y rama de referencia: .NET Framework
  **3.5 sin SP1**, Windows XP 32-bit, CPU modesto. Todo lo nuevo nace ahí y se prueba
  en el fierro real.
- **`rocola-hd`** — *esta rama*, para PC **potente** (Windows 7+/10, 4+ núcleos, 4+ GB):
  retargeteada a **.NET Framework 4.8**. Mismo programa y **misma lógica de audio** que la
  clásica, más el **NIVEL HD** que la máquina activa sola al arrancar (`ProbeAudioTier`).

## Qué hace (v2.5)

**Operación (local):**
- Se maneja con la botonera/teclado físico configurable; nada de "lenguaje de teclado de PC".
- Monedero con créditos y cola de canciones pagadas.
- Buscador con teclado en pantalla; sección de videos; portadas en cubos 3D girando.
- "Lo más sonado" y TOP por artista; modo atracción cuando nadie la usa.
- **Blindaje de kiosco**: una excepción no controlada se anota en `rocola_error.txt`
  y la rocola **sigue corriendo** (ya no muere con el diálogo de error de .NET).

**Sonido (idéntico en las dos ramas):**
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
- **Dither TPDF** al bajar a 16 bits (en la salida WaveOut de respaldo).
- **Respaldo automático** de lo aprendido (volúmenes, EQ, estadísticas, config)
  a otra carpeta/disco; conserva los 3 respaldos más recientes.

## Nivel HD (solo esta rama)

- **Salida WASAPI flotante** (`salida=auto|wasapi|waveout` en `rocola_config.txt`): muestras
  *float* de punta a punta hasta el motor de audio moderno, sin bajar a 16 bits ni pasar por
  el mezclador clásico, y con menos latencia (el pulso visual se re-alinea solo). Si algo falla,
  cae automáticamente al **WaveOut** clásico.
- **Reconstructor espectral fino**: el DSEE espectral usa **STFT de 4096** (bins más finos =
  detección del corte y costura más limpias) en vez de 2048.
- **DSEE ULTRA — IA propia (Apollo)**: por demanda, con **selector de GPU** en Ctrl+F3. La red
  incluida (`rocola_ia.bin`) predice la **envolvente** de los agudos perdidos y el reconstructor
  la usa en vez de su curva fija. Reentrenable con tu propia música (`--musica`; ver `ia/LEEME.md`),
  con paridad python↔C# verificada. Regla de la casa: **no se embarca nada llamado "IA" que no lo sea.**
- **Render por GPU (Direct3D 9)**: estrellas, cubos 3D y buscador acelerados; con caída a GDI si no hay GPU.

## Compilar

Proyecto de **.NET Framework 4.8** (Visual Studio). Abre `Rocola.slnx` y compila en Release.
El resultado (`Rocola.exe` + `NAudio.dll`) se copia junto a la carpeta `musica`, como siempre.

- Código: `Rocola/Form1.cs` (monolítico) + `Form1.Designer.cs` + `Program.cs` + `D3D9Star.cs`.
- Dependencia: **NAudio 1.9.0**: la DLL viene **incluida** en `packages/` y el proyecto la
  referencia directo — compila **sin NuGet y sin internet** (ya no hay `packages.config`).
- Herramientas de IA (opcionales, Python): `ia/` — entrenamiento (`entrenar_ia.py`,
  `entrenar_con_mi_musica.bat`), motor en tiempo real (`ia/tiempo_real/`) y upscale por lotes (`ia/upscale/`).

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
