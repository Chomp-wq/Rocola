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

- **Aún no compila.** Faltan la capa de ventanas (controles, el formulario, los temporizadores y el bucle), Direct3D 9 en OpenGL ES 2 y
  las llamadas de Windows. Ninguna compilación del sistema ni del programa lo usa: `sistema/rapida.sh` solo recompila el programa si
  cambian `Motor/` o `Consola/`.
- **Medido con Mono:** el alto de renglón y `Font.Height` salen iguales a GDI+. Los anchos de texto quedan a 1 o 2 píxeles de libgdiplus,
  que no pone el margen de 1/6 de GDI+; por eso la referencia buena son las capturas de la rocola de XP, no Mono.
- Nada probado en el A4.
