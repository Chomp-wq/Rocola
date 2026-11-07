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

- **Compila entero, con la tarjeta gráfica**: los 109 archivos de XP (69 000 líneas) con la capa de Linux. Ojo: hasta ahora el proyecto no
  definía `ROCOLA_GPU` (el de XP sí), así que los 52 archivos de la tarjeta (`#if ROCOLA_GPU`) se compilaban vacíos; ya está definido y
  compilan. Lo traducido a mano hasta ahora:
  - las ventanas (`Ventanas.*.cs`: controles, formulario, temporizadores, bucle, foco, teclas y capas);
  - el dibujo (`Dibujo2D.cs`, más los codificadores JPEG/PNG, los formatos con paleta, el degradado radial con `Blend`, la clave de color
    de `ImageAttributes`, `Matrix.TransformPoints` y `MeasureString` con letras y renglones);
  - **Direct3D 9 en OpenGL ES 2** (`Direct3DaGL.cs`): cada llamada de XP a Direct3D por su tabla (`D3D9.Fn<T>(objeto, hueco)`, 36 sitios
    en `D3D9Star.cs`, `PerformanceGpu.cs` y `TexturasCompatiblesGpu.cs`) se cambió por la función del mismo nombre de `D3D9.EnGl`
    (`present = D3D9.EnGl.Present;`). Objetos con su cuenta de referencias como COM; colores en el orden de Direct3D de punta a punta (se
    suben tal cual); la regla de medio píxel de Direct3D 9; todo se pinta en texturas (el fondo y las imágenes intermedias); las etapas
    de textura (`SetTextureStageState`) se convierten en un programa de OpenGL por combinación; bloquear da una memoria aparte que se
    sube al soltar; `Present` pasa el fondo al escritorio (`Escritorio.PresentaNativa`). El resto de XP no cambia;
  - NAudio 3 (`NAudioXp.cs`: el puente con `Span`, y el MP3 con libmpg123 en lugar del ACM);
  - la salida de sonido de XP (`ISistemaWaveOut`) en ALSA (`SonidoAlsa.cs`, con los errores de winmm);
  - lo que no es MP3, WAV ni AIFF, por ffmpeg (`AudioXp.cs`);
  - el volumen de Windows en ALSA (`VolumenWindows.cs`, con amixer);
  - las llamadas a Windows del bucle de la tarjeta (`GetDeviceCaps` del refresco, `PeekMessage`, `MsgWaitForMultipleObjectsEx`), a la cola
    del escritorio.

  En `Codigo/` solo cambian las 33 líneas `NAudio 3 (Puente.cs)` y lo que se tradujo a mano. Los finales de línea de XP se conservan.
- **Probado**: `rocola-xp --prueba-gpu 1024 768 cielo.png 30` pinta el cielo y las estrellas de XP (`GpuStarfield`, lo de la ventana de
  prueba F12) con la traducción, fuera de pantalla: el degradado de tres paradas y las estrellas con su brillo y su destello salen como en
  XP; 30 cuadros a 5,9 ms cada uno con llvmpipe (sin tarjeta) y ningún objeto de OpenGL suelto al acabar. No probado en el A4.
- **Arranca** (`dotnet rocola-xp.dll`): lee la música, guarda su biblioteca y corre su bucle.
- **Lo siguiente:**
  - el anfitrión (KMS, o fuera de pantalla para las capturas) que compone las ventanas y lo de la tarjeta, y las teclas por evdev;
  - la primera pantalla entera de XP comparada con las capturas del A4;
  - lo de NativeAOT que avisa el compilador (`Marshal.SizeOf(Type)`, `Assembly.Load(byte[])`).
- **Medido con Mono:** el alto de renglón y `Font.Height` salen iguales a GDI+. Los anchos de texto quedan a 1 o 2 píxeles de libgdiplus,
  que no pone el margen de 1/6 de GDI+; por eso la referencia buena son las capturas de la rocola de XP, no Mono.
- Ninguna compilación del sistema ni del programa lo usa todavía (`sistema/rapida.sh` solo recompila el programa si cambian `Motor/` o
  `Consola/`). Nada probado en el A4.
