# La rocola de XP traducida a Linux (en curso)

Decisión del dueño del 7 de noviembre de 2026: traducir **todo** el código de la rocola de XP para que funcione de forma nativa en Linux
e idéntica. El plan está en `PLAN_TRADUCCION_XP_A_LINUX_MODEST_2026-11-07.md` y la regla en `AGENTS.md`.

## Qué hay aquí

- `Codigo/`: los 109 archivos de `Rocola/`, copiados el 7-11-2026 con la parte mecánica de la traducción ya hecha:
  - se quitaron los `using` de WinForms y GDI+ (`System.Windows.Forms`, `System.Drawing.Drawing2D`, `Imaging` y `Text`) y sus nombres
    completos;
  - todo lo demás está igual que en XP.

  Lo que es de Windows se traduce a mano, archivo por archivo: las llamadas a `user32`, `kernel32` y `winmm`, Direct3D 9 y el reproductor
  de video. Si mientras tanto cambia `Rocola/`, el cambio se pasa también aquí.
- `Plataforma/`: lo nativo de Linux con que se traduce:
  - `Dibujo2D.cs`: GDI+ hecho con SkiaSharp. Usa las letras de XP de `/rocola/datos/letras` (si faltan, DejaVu Sans) con el alto de
    renglón sacado de las tablas de cada letra, el margen de 1/6 de letra de GDI+, el corte con «…» y el ajuste a los píxeles.
  - `Ventanas.Tipos.cs`: las teclas y los tipos sencillos de las ventanas, con los valores de Windows.
  - `Traduccion.cs`: los `global using`, para que cada archivo traducido quede línea por línea como el de XP.
- `RocolaXp.csproj`: el programa nuevo (.NET 10, NativeAOT, SkiaSharp 3.116.1, NAudio.Core 3, NLayer 3).

## Cómo va (7 de noviembre de 2026)

- **Compila entero**: los 109 archivos de XP (69 000 líneas) con la capa de Linux. Lo traducido a mano hasta ahora:
  - las ventanas (`Ventanas.*.cs`: controles, formulario, temporizadores, bucle, foco, teclas y capas);
  - el dibujo (`Dibujo2D.cs`, más los codificadores JPEG/PNG, los formatos con paleta y el degradado radial con `Blend`);
  - NAudio 3 (`NAudioXp.cs`: el puente con `Span`, y el MP3 con libmpg123 en lugar del ACM);
  - la salida de sonido de XP (`ISistemaWaveOut`) en ALSA (`SonidoAlsa.cs`, con los errores de winmm);
  - lo que no es MP3, WAV ni AIFF, por ffmpeg (`AudioXp.cs`);
  - el volumen de Windows en ALSA (`VolumenWindows.cs`, con amixer).

  En `Codigo/` solo cambian las 33 líneas `NAudio 3 (Puente.cs)` y lo que se tradujo a mano. Los finales de línea de XP se conservan.
- **Arranca** (`dotnet rocola-xp.dll`): lee la música, guarda su biblioteca y corre su bucle. Se para en la primera llamada a Windows que
  queda: `GetForegroundWindow` de user32.
- **Lo siguiente:**
  - traducir las llamadas a Windows que quedan (user32, kernel32, gdi32, dwmapi, advapi32) a lo de Linux;
  - Direct3D 9 (`D3D9Star.cs`) a OpenGL ES 2;
  - la pantalla por KMS (o fuera de pantalla para las capturas) y las teclas por evdev.
- **Medido con Mono:** el alto de renglón y `Font.Height` salen iguales a GDI+. Los anchos de texto quedan a 1 o 2 píxeles de libgdiplus,
  que no pone el margen de 1/6 de GDI+; por eso la referencia buena son las capturas de la rocola de XP, no Mono.
- Ninguna compilación del sistema ni del programa lo usa todavía (`sistema/rapida.sh` solo recompila el programa si cambian `Motor/` o
  `Consola/`). Nada probado en el A4.
