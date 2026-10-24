# Lo que dicen los volcados del pantallazo de XP — 24 de octubre de 2026

El dueño corrió `Recoger_error_XP.cmd` en la rocola (AMD A4-6300, Windows XP SP3) y mandó `Diagnostico_XP`. Contiene:

- dos minivolcados del 22-10-2026, `Mini102226-01.dmp` y `Mini102226-02.dmp`;
- `sysdata.xml`, el informe de errores de Windows con los aparatos y las versiones de sus controladores;
- `controladores.csv`, la lista de controladores con su estado;
- `sistema.txt`.

No traía `dxdiag.txt` ni los registros de la rocola.

Los volcados se leyeron con un lector propio de los encabezados de XP: el encabezado PAGEDUMP y el bloque de triage, con la lista de
controladores, el hilo, el proceso y la pila en crudo. **No se usó WinDbg ni hay símbolos de los controladores de ATI**: se sabe en qué
controlador está cada dirección de la pila, no en qué función. Los archivos del dueño no se suben al repositorio.

## Los dos pantallazos

| | Primero (`-01`) | Segundo (`-02`) |
|---|---|---|
| Hora (UTC, reloj del equipo) | 22-10, 07:41:27 | 22-10, 07:42:36 |
| Error | `0x100000EA` THREAD_STUCK_IN_DEVICE_DRIVER | el mismo |
| Equipo encendido desde hacía | 2 h 46 min | 60 s (es el arranque tras el primero) |
| Proceso del hilo atascado | **`logonui.exe`** (la pantalla de inicio de sesión de Windows), creado 22 s antes | **`CCC.exe`** (Catalyst Control Center de ATI), creado 33 s antes |
| Dónde estaba el hilo | en `ati2cqag.dll` y `ati2dvag.dll` (el controlador de pantalla de ATI), esperando en `hal.dll` | lo mismo, y además `ati3duag.dll` (la parte Direct3D del controlador) |

En los dos casos el hilo del volcado **es** el que el error señala como atascado: coincide con el primer parámetro del error.

**El hilo atascado no era de la rocola en ninguno de los dos.** El controlador de pantalla de ATI se quedó esperando a la tarjeta
mientras dibujaban la pantalla de inicio de sesión de Windows y, en el arranque siguiente, el Catalyst Control Center. Es el caso
típico del 0xEA: el controlador espera a que la parte gráfica del APU termine, y la tarjeta no contesta.

Esto **no demuestra** que la rocola no tenga nada que ver, por dos motivos:

- El volcado no dice qué otros programas estaban abiertos.
- Un cambio de escritorio, como el de la pantalla de inicio de sesión, hace que la rocola pierda su dispositivo Direct3D y lo
  recupere. Las correcciones del 23-10 (`RECUPERACION_GRAFICA_XP_2026-10-23.md`) eran para esa recuperación.

Lo que sí dice es que el bloqueo ocurre dentro del controlador de ATI con programas que no son la rocola. Además, se repitió al
minuto, al arrancar y sin la rocola en ese hilo.

## Por qué apareció la pantalla de inicio de sesión a media noche

`logonui.exe` no se abre sola con el equipo encendido desde hace casi 3 horas. Windows XP la abre en uno de estos casos:

- al salir de un **protector de pantalla con «Al reanudar, proteger con contraseña»** (o «mostrar la pantalla de bienvenida»);
- al **volver de la inactividad** (standby) si pide contraseña;
- al bloquear el equipo (Windows+L).

Con la rocola sola y nadie tocándola, lo probable es uno de los dos primeros. **La rocola no hace nada para evitar el protector de
pantalla, el apagado del monitor ni la inactividad**: no hay ni `SetThreadExecutionState` ni respuesta a `SC_SCREENSAVE` ni a
`SC_MONITORPOWER`. Cada cambio de ese tipo hace pasar al controlador de ATI por un cambio de escritorio o de energía, que es justo
donde se trabó.

## Recomendaciones (en la rocola, sin tocar código)

1. **Protector de pantalla y energía** (Panel de control → Pantalla → Protector de pantalla, y Opciones de energía):
   - protector «(Ninguno)» o, al menos, sin «proteger con contraseña»;
   - «Apagar el monitor», «Pasar a inactividad» e «Hibernar» en «Nunca».

   Así Windows deja de sacar la pantalla de inicio de sesión de noche. Ese fue el primer pantallazo.
2. **Quitar el Catalyst Control Center del arranque** (msconfig → Inicio: «CCC», «Catalyst Control Center» / «MOM»). Es un programa
   .NET que no hace falta para la rocola. Al arrancar pide memoria y procesador en un equipo de dos núcleos, y es el proceso del segundo
   pantallazo. El controlador de pantalla sigue funcionando igual sin él.
3. **PerfectDisk.** Está instalado, y sus dos filtros del sistema de archivos (`DefragFS`, `PDFSFilter`) arrancan solos y están
   corriendo. Desfragmentar un SSD no lo acelera y lo desgasta. Si tiene activada la desfragmentación automática o en segundo plano
   («StealthPatrol» o una programada), puede leer y escribir el disco mientras la rocola carga carátulas y música. Conviene apagar
   esa automática para el SSD, o desinstalarlo si no se usa.
4. **Si el 0xEA vuelve a salir con todo eso:** mirar la temperatura y el polvo del disipador del procesador, la fuente y la memoria.
   En un APU la parte gráfica usa la RAM del sistema, así que una memoria inestable también traba la tarjeta. El controlador es el
   `ati2mtag.sys` 6.14.10.7280 del 06-09-2014. No se recomienda cambiarlo sin tener otro probado para XP.

## Datos del equipo que sirven para la rocola

- **XP administra 2509 MB de memoria física, no 3,5 GB**: así lo dice el descriptor de memoria de los dos volcados. Lo que falta lo
  aparta el APU para su memoria de vídeo (lo que diga la BIOS en «UMA Frame Buffer») más el hueco de direcciones de 32 bits. Para los
  presupuestos de la rocola hay que contar con unos 2,5 GB para todo el sistema, y con 2 GB de direcciones para el proceso de la
  rocola.
- **Dos núcleos**, como se suponía.
- **Dos tarjetas de sonido:** la Realtek ALC887 (salida analógica) y la de HDMI del APU. Al arrancar, la rocola pone al máximo el
  volumen maestro de las dos. Los filtros de efectos de Creative del Realtek (`Ambfilt`, `Monfilt`) están detenidos: no tocan el
  sonido.
- **El SSD es un HS-SSD-SILK de 1 TB en modo IDE de compatibilidad** (`pciide`/`atapi`). Funciona. **No** pasarlo a AHCI en la BIOS
  sin instalar antes el controlador AHCI para XP: XP no arrancaría (pantallazo 0x7B).
- Windows está en `D:\WINDOWS`.

## Qué falta para optimizar el rendimiento

Estos archivos sirven para la estabilidad, pero no traen ninguna medida de rendimiento. Para decidir qué optimizar en el A4 hace falta:

- `rocola_diagnostico_AAAA-MM-DD_HHMM.txt`: el «Diagnóstico completo» de Ctrl+F3 → Imagen, unos 4 minutos con la rocola sin
  clientes;
- y, si se puede, `rocola_error.txt` y el `dxdiag.txt` del mismo equipo.

## Posible cambio en la rocola (pendiente de decisión del dueño)

Que la rocola, mientras está abierta, pida a Windows mantener el monitor encendido y no entrar en inactividad
(`SetThreadExecutionState`), y que no deje arrancar el protector de pantalla mientras su ventana está delante (en XP se puede). No cambia
nada de la pantalla de la rocola. Pero cambia lo que hace Windows, y quizá alguien quiere que el monitor se apague de noche. Por eso
**no se ha hecho sin preguntar**.
