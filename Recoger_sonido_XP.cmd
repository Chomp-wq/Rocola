@echo off
setlocal EnableExtensions
rem Recoger los datos del SONIDO de la rocola (D2, 30-10-2026; AUDIO_PROPUESTAS_MODEST_2026-10-29.md).
rem Solo copia: no cambia nada de la rocola ni del equipo. Ejecutar desde la carpeta de Rocola.exe.
set "ROCOLA_SON=%~dp0Sonido_XP"
if not exist "%ROCOLA_SON%" mkdir "%ROCOLA_SON%"
if not exist "%ROCOLA_SON%" (
  echo No se pudo crear la carpeta Sonido_XP junto a este archivo.
  pause
  exit /b 1
)
echo Recogiendo los datos del sonido. No se cambia ningun ajuste.
ver > "%ROCOLA_SON%\sistema.txt"
echo Fecha y hora: %DATE% %TIME% >> "%ROCOLA_SON%\sistema.txt"
set "ROCOLA_ORIGEN=%~dp0"
if defined ROCOLA_DATOS if exist "%ROCOLA_DATOS%\rocola_config.txt" set "ROCOLA_ORIGEN=%ROCOLA_DATOS%\"
echo Datos leidos de: %ROCOLA_ORIGEN% >> "%ROCOLA_SON%\sistema.txt"
for %%F in (rocola_billar_bitacora.txt rocola_billar_bitacora.txt.1 rocola_billar_nivel.txt rocola_sonoridad.txt rocola_volumen.txt rocola_eq.txt rocola_config.txt rocola_error.txt) do (
  if exist "%ROCOLA_ORIGEN%%%F" (
    copy /y "%ROCOLA_ORIGEN%%%F" "%ROCOLA_SON%" >nul
    echo   copiado %%F
  ) else (
    echo   no hay %%F
  )
)
echo.
echo Datos del sonido guardados en:
echo %ROCOLA_SON%
echo Comprime esa carpeta (clic derecho, Enviar a, Carpeta comprimida) y mandala.
start "" explorer.exe "%ROCOLA_SON%"
pause
endlocal
