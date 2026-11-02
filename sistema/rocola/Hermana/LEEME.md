# La rocola de XP en Rocola OS («Hermana»)

Decisión del dueño (2 de noviembre de 2026): la rocola de Rocola OS tiene que ser **idéntica** a la de XP. Por eso aquí no se
redibuja nada. Se compila **el mismo código de XP** (`Rocola/*.cs`, sin cambiarlo) con .NET 10 para Linux, y lo que en XP pone
Windows se pone aquí:

| Lo que en XP da Windows | En Rocola OS | Dónde |
|---|---|---|
| WinForms (ventanas, controles, teclas, temporizadores, el hilo de la ventana) | un WinForms propio, sobre GDI+ | `Formas/` (se llama `System.Windows.Forms`, para que el código de XP y NAudio 1.9 lo encuentren) |
| GDI+ (todo lo que la rocola pinta con `Graphics`) | `System.Drawing.Common` 6 con libgdiplus | el paquete de NuGet |
| Direct3D 9 (el cielo, la estrella, los cubos, las portadas) | un Direct3D 9 de mentira que dibuja con OpenGL ES 2 | `Windows/D3d9.cs`, `Windows/Gl.cs` |
| juntar las ventanas y presentar | capas de GDI+ y de la tarjeta, por KMS (o fuera de pantalla para probar) | `Windows/Pantalla.cs` y `../Consola/Pantalla/Kms.cs` |
| `user32`, `kernel32`, `gdi32`, `winmm` | las mismas funciones, exportadas por el propio programa | `Windows/Enlaces.cs` |
| la salida de sonido (`waveOut`) | ALSA, enchufada en `WaveOutPropio.Sistema` (el enchufe que XP ya trae) | `Windows/Sonido.cs` |
| el descompresor de MP3 de Windows (ACM) | NLayer en enteros de 16 bits, enchufado en `DescompresorMp3.Crea` | `Windows/Mp3.cs` |
| el teclado y los botones | evdev, con la misma tabla de teclas de `rocola_teclas.txt` | `Windows/Teclado.cs` |

Las llamadas a Windows que aquí no están dan `EntryPointNotFoundException`, igual que en Mono, donde la rocola de XP ya sabe
seguir sin ellas.

## Lo que se cambió en la rocola de XP (y por qué no cambia nada en XP)

- `AperturaAudio.cs`: `SetApartmentState(STA)` va en `try`, como ya iba en los otros dos sitios donde se usa. En Linux, .NET no
  lo admite y lanza una excepción; en Windows hace lo mismo que antes.
- `FlotanteNLayer.cs`: `DescompresorMp3.Crea`, el descompresor de MP3 que piden todos los lectores. En XP es el de Windows
  (`Mp3FileReader.CreateAcmFrameDecompressor`), igual que antes. Rocola OS pone el suyo.

## Compilar y probar

```sh
cd sistema/rocola/Hermana
dotnet publish -c Release -r linux-x64 -o ~/rocola-trabajo/hermana
cd ~/rocola-trabajo/hermana            # con una carpeta musica/ y rocola_teclas.txt al lado
ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_ALSA=null ROCOLA_SIN_TECLADO=1 \
  ROCOLA_GUION="8:C 9:Return 11:Return" ROCOLA_CAPTURA=/tmp/cuadro.ppm ROCOLA_CAPTURA_SEG=18 ./rocola
```

- `ROCOLA_PANTALLA=fuera:AxB`: sin monitor, en la memoria de la tarjeta. Sin la variable, por KMS (`/dev/dri/card0`).
- `ROCOLA_ALSA`: el aparato de ALSA (`default` si no se pone; `null` para probar sin tarjeta de sonido).
- `ROCOLA_GUION`: teclas a su hora (`segundos:tecla`; con `Control+F3` y similares).
- `ROCOLA_CAPTURA` y `ROCOLA_CAPTURA_SEG`: guarda la pantalla a esa hora y sale.

## Cómo va (2 de noviembre de 2026)

- Probado aquí (Linux en la nube, Mesa llvmpipe fuera de pantalla, ALSA `null`):
  - arranca;
  - el asistente de botones de la primera vez;
  - la pared con el cielo Legacy, la estrella 3D y la luna;
  - entrar a un disco (el cubo que gira y las canciones);
  - una moneda y poner una canción, que suena por ALSA;
  - Ctrl+F3 (la hoja de Sonido).
- **Nada probado en el A4 ni con monitor de verdad.**

Falta:

- comparar cuadro a cuadro contra la tarjeta imitada de las pruebas de XP (`pruebas/huellacuadros.cs`);
- las letras de XP (Segoe UI, Tahoma...), que vienen del disco de XP al instalar;
- el video (en XP es Windows Media Player por ActiveX; aquí, mpv detrás de `AxHost.Fabrica`);
- el idioma y el orden de las cadenas (en XP, español);
- meterla en la imagen de Buildroot (libgdiplus y sus dependencias) en lugar de `rocola-consola`;
- probarla en el A4.
