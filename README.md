# Rocola

Rocola digital para Windows (WinForms + NAudio), pantalla completa, manejo por
botonera/teclado, monedero con créditos, búsqueda con teclado en pantalla, sección
de videos (Windows Media Player), portadas en cubos 3D, fondo estrellado por género,
estadísticas ("Lo más sonado") y modo atracción.

## Ramas

- **Clasica (XP)** — la rocola fisica original: .NET Framework **3.5 sin SP1**, Windows XP 32-bit,
  CPU modesto. Es la rama de referencia; todo lo nuevo nace ahi y se prueba en el fierro real.
- **`rocola-hd`** (esta rama) — para rocolas **POTENTES**: Windows 7+ (ideal 10), **4+ nucleos**,
  **4+ GB de RAM**. Proyecto retargeteado a **.NET Framework 4.8**. Todo lo de la clasica, mas el
  **NIVEL HD** (la maquina se mide sola al arrancar, `ProbeAudioTier`):
  - **Salida WASAPI flotante** (`salida=auto|wasapi|waveout` en `rocola_config.txt`): muestras float
    de punta a punta hasta el motor de audio moderno, sin bajar a 16 bits ni pasar por el mezclador
    clasico, y con menos latencia (el pulso visual se re-alinea solo). Si cualquier cosa falla,
    cae automaticamente al WaveOut clasico.
  - **Reconstructor espectral fino**: el DSEE espectral usa STFT de **4096** (bins mas finos =
    deteccion del corte y costura mas limpias) en vez de 2048.
  - **Hoja de ruta NIVEL 3 — IA real**: el punto de enchufe ya existe (otro *tap* en la cadena de
    `PlayTrack`, gateado por `tierHd`). Plan tecnico: `Microsoft.ML.OnnxRuntime` (x64) + un modelo de
    extension de ancho de banda / mejora musical en TIEMPO REAL. Hoy no existe un modelo libre digno
    de produccion para musica en tiempo real; cuando exista (o se entrene uno), se integra aqui.
    Regla de la casa: **no se embarca nada llamado "IA" que no lo sea.**

## Proyecto

Proyecto clásico de **.NET Framework 3.5** (Visual Studio). Abre `Rocola.slnx`
y compila en Release. El resultado (`Rocola.exe` + `NAudio.dll`) se copia junto a
la carpeta `musica` como siempre.

- Código: `Rocola/Form1.cs` (monolítico) + `Form1.Designer.cs` + `Program.cs`.
- Dependencia: NAudio 1.9.0 (net35), referenciada vía `packages/`.

> `bin/`, `obj/`, `.vs/` y el resto del paquete NuGet no se versionan
> (ver `.gitignore`); solo se incluye la DLL de NAudio que el `HintPath` necesita.

## Optimizaciones aplicadas (sin cambiar aspecto ni comportamiento)

1. **Estabilidad — `PlayTrack`**: si varias canciones seguidas no abren
   (archivos movidos/corruptos), antes se apilaba `PlayTrack → PlayNext →
   PlayTrack…` (posible *StackOverflow*). Ahora el `PlayNext` del `catch` va por
   `BeginInvoke`, que desenreda la pila (igual que ya hacía el video).

2. **Búsqueda — `Score` / `RecomputeResults`**: `" " + q` y `q.Split(' ')` se
   calculaban dentro de `Score`, que corre por **cada** entrada del índice en
   **cada** tecla. Ahora se calculan una sola vez por tecla y se pasan a `Score`
   (el puntaje es idéntico). Con bibliotecas grandes elimina decenas de miles de
   asignaciones de cadena por pulsación.

3. **Render — `Cube.Draw`**: los tres arreglos de la proyección 3D (`ex`,
   `depth`, `order`) se asignaban en cada llamada, y `Draw` se llama varias veces
   por cuadro. Ahora se reutilizan vía `[ThreadStatic]`.

### Pendiente (recomendado, no aplicado)

- **Escaneo fresco recorre el disco 2–3 veces**: `SafeEnumerateAudio` camina todo
  el árbol, `SafeEnumerateVideo` lo vuelve a caminar completo y `FindCover` hace un
  `Directory.GetFiles` extra por carpeta. Un solo recorrido que clasifique
  audio/video/portada por carpeta ~triplicaría la velocidad del primer escaneo (y
  de cada F5) en HDD. Requiere tocar el escaneo y el caché de biblioteca, y probar
  contra la biblioteca real para no cambiar qué portada/video se elige.
