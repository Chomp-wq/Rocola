# Recuperacion grafica en Modest y pantallazo de XP

## Lo que muestran las fotos

Windows XP SP3 informa `BCCode 100000ea` (`THREAD_STUCK_IN_DEVICE_DRIVER`): un hilo de controlador quedo bloqueado. Microsoft lo asocia frecuentemente al controlador de pantalla o al hardware grafico. No es una excepcion .NET de Rocola. El usuario confirma que la maquina funcionaba sola cuando ocurrio.

Sin el minidump no se puede identificar el modulo responsable, su version ni la pila del bloqueo. Estas correcciones resuelven defectos demostrables de la recuperacion Direct3D de Rocola; NO demuestran que se haya eliminado la causa de ese pantallazo. No se instala ni modifica ningun controlador.

Referencias de Microsoft:

- [Bug check 0xEA](https://learn.microsoft.com/windows-hardware/drivers/debugger/bug-check-0xea--thread-stuck-in-device-driver).
- [IDirect3DDevice9::Reset](https://learn.microsoft.com/windows/win32/api/d3d9/nf-d3d9-idirect3ddevice9-reset): parametros de entrada/salida, mensajes reentrantes y llamadas permitidas tras un fallo.

## Correcciones

1. Reset recibe una COPIA de D3DPRESENT_PARAMETERS. Direct3D pone BackBufferWidth/Height/Count a cero al regresar; antes modificaba la configuracion persistente, causando perdida del tamano solicitado y reinicios repetidos.
2. Durante Reset se rechazan otro Reset y BeginFrame. RenderFrame tiene una guardia con finally: un WM_PAINT reentrante no vuelve a preparar ni dibujar el cuadro dentro del controlador.
3. La recuperacion se comprueba ANTES de preparar/subir texturas. Si Reset falla, se bloquean creaciones, actualizaciones, precargas y consultas de memoria hasta recuperar el dispositivo. Los reintentos se espacian 250 ms; no hay un bucle de Reset por cada cuadro. Se conserva el tamano mas reciente solicitado.
4. El patron estrellado conserva sus dimensiones propias para reconstruirse despues de una recuperacion aplazada, incluso cuando los parametros pedidos ya tienen el tamano nuevo.

Automaticas, sin opciones nuevas ni cambios de apariencia en funcionamiento normal. Exclusivamente GPU; CPU congelado. Se conservan audio, preferencias y datos.

## Comprobaciones

Base real: `a91eb9f8e1d5f4fb3d9757a5be3fe591fe04f840`.

- `recuperagpu`: 31 comprobaciones mediante callbacks con las firmas del dispositivo: Reset que borra parametros, fallo y reintento, DEVICELOST/DEVICENOTRESET/S_OK, 100 cuadros durante la espera, reentrada de Reset/BeginScene/WM_PAINT, prohibicion de subidas tras fallo y reconstruccion aplazada del patron. Reproduce los defectos en el ejecutable anterior y pasa en el nuevo.
- CPU y GPU compilados sin avisos con referencias oficiales Microsoft .NET 3.5 y C# 3.
- Aprobadas performance, cielovivo, fluidezcuadros y ajustesfluidez, mas las 14 baterias Python del lanzador.
- estadoeficiente: 218 comprobaciones aprobadas usando una copia de prueba con el mapeo Mono de RtlMoveMemory a memmove. Sin ese mapeo Linux no puede copiar a las texturas de la tarjeta imitada. El mapeo NO se distribuye para Windows.
- Release completo del csproj, optimizado, con icono/recursos/propiedades y NLayer embebido. Sobre el ejecutable entregado pasan recuperagpu (31), estadoeficiente (218), salalocal (103) y presenciasala (51).
- Comparacion de 32 huellas entre COPIAS instrumentadas de a91eb9f y el codigo nuevo: escenas de 1024x768, preset Performance, rejilla/reposo, estrella 2D/3D, asistente/guia e interruptores por separado. Coinciden los pixeles, vertices, ordenes y cuadros presentados en las 32 escenas; los resultados quedan en la verificacion adjunta al paquete.

La bateria huellacuadros contra su archivo historico del 19-10 no pasa: informa 25 diferencias. No se reescribe esa referencia para ocultarlas. Por ejemplo, alto/rejilla/3D produce `pix df056f19785d944e` y `bruto 02b5347e90e0fdb6` tanto en a91eb9f como en el codigo nuevo, mientras el archivo historico espera otro valor. La comparacion directa con a91eb9f es la pertinente para detectar cambios introducidos aqui; no se declara toda la suite aprobada.

Medicion SINTETICA de preparacion de cuadros, no FPS ni latencia de presentacion de la APU real (Mono, dispositivo imitado, sin musica):

| 1024x768 Performance | p95 ms | p99 ms | max ms |
| --- | ---: | ---: | ---: |
| Rejilla, estrella 3D | 0.552 | 0.684 | 0.786 |
| Rejilla, estrella 2D | 0.514 | 0.657 | 0.723 |
| Reposo, estrella 3D | 0.494 | 0.575 | 0.651 |
| Reposo, estrella 2D | 0.520 | 0.618 | 0.843 |

Los demas resultados estan en el registro de coste. GC simultaneo no demuestra causalidad. No se ha reproducido el pantallazo en XP ni comprobado la tarjeta, su temperatura o su controlador; tampoco se afirma ausencia de pausas.

## Recoger el fallo real

El paquete incluye `Recoger_error_XP.cmd`. Ejecutarlo desde la carpeta de Rocola crea `Diagnostico_XP` y copia minidumps existentes de Windows y de sus temporales WER, registros disponibles de Rocola, la lista de controladores y un informe dxdiag sin comprobacion WHQL. No copia el volcado completo MEMORY.DMP ni cambia ajustes. Mantiene los originales. La carpeta se excluye de Git.

Adjuntar el .dmp del reinicio (el mas reciente si solo hubo ese) y dxdiag.txt. La foto menciona un `Mini102226-02.dmp` dentro de una carpeta WER temporal; puede haber sido borrada al cerrar el informe, por lo que tambien se revisa `%SystemRoot%\Minidump`.

El guion tiene ASCII/CRLF y usa comandos de XP, pero falta ejecutarlo en la maquina real. El diagnostico del controlador queda pendiente del volcado.
