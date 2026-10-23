@echo off
setlocal EnableExtensions
set "ROCOLA_DIAG=%~dp0Diagnostico_XP"
if not exist "%ROCOLA_DIAG%" mkdir "%ROCOLA_DIAG%"
if not exist "%ROCOLA_DIAG%" (
  echo No se pudo crear la carpeta de diagnostico junto a este archivo.
  pause
  exit /b 1
)
echo Recogiendo informes existentes. No se cambia ningun controlador ni ajuste.
ver > "%ROCOLA_DIAG%\sistema.txt"
echo Fecha y hora: %DATE% %TIME% >> "%ROCOLA_DIAG%\sistema.txt"
if exist "%SystemRoot%\Minidump\*.dmp" copy /y "%SystemRoot%\Minidump\*.dmp" "%ROCOLA_DIAG%" >nul
for /d %%D in ("%TEMP%\WER*") do (
  if exist "%%~fD\Mini*.dmp" copy /y "%%~fD\Mini*.dmp" "%ROCOLA_DIAG%" >nul
  if exist "%%~fD\sysdata.xml" copy /y "%%~fD\sysdata.xml" "%ROCOLA_DIAG%" >nul
)
if exist "%~dp0rocola_error.txt" copy /y "%~dp0rocola_error.txt" "%ROCOLA_DIAG%" >nul
if exist "%~dp0rocola_diagnostico.txt" copy /y "%~dp0rocola_diagnostico.txt" "%ROCOLA_DIAG%" >nul
if defined ROCOLA_DATOS (
  if exist "%ROCOLA_DATOS%\rocola_error.txt" copy /y "%ROCOLA_DATOS%\rocola_error.txt" "%ROCOLA_DIAG%" >nul
  if exist "%ROCOLA_DATOS%\rocola_diagnostico.txt" copy /y "%ROCOLA_DATOS%\rocola_diagnostico.txt" "%ROCOLA_DIAG%" >nul
)
if exist "%SystemRoot%\System32\driverquery.exe" "%SystemRoot%\System32\driverquery.exe" /v /fo csv > "%ROCOLA_DIAG%\controladores.csv" 2>&1
if exist "%SystemRoot%\System32\dxdiag.exe" (
  echo Preparando dxdiag. Puede tardar unos segundos.
  start "" /wait "%SystemRoot%\System32\dxdiag.exe" /whql:off /t "%ROCOLA_DIAG%\dxdiag.txt"
)
echo.
if not exist "%ROCOLA_DIAG%\*.dmp" echo No se encontro un minidump. Conserva los archivos de error que muestra Windows.
echo Informes guardados en:
echo %ROCOLA_DIAG%
echo Envia el archivo .dmp mas reciente y dxdiag.txt para identificar el bloqueo.
start "" explorer.exe "%ROCOLA_DIAG%"
pause
endlocal
