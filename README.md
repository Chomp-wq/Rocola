# Rocola

Rocola digital para Windows (WinForms + NAudio), pantalla completa, manejo por
botonera/teclado, monedero con créditos, búsqueda con teclado en pantalla, sección
de videos (Windows Media Player), portadas en cubos 3D, fondo estrellado por género,
estadísticas ("Lo más sonado") y modo atracción.

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
