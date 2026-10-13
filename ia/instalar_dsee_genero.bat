@echo off
rem ============================================================================================
rem  INSTALAR LOS MODELOS DEL DSEE POR GENERO (los que dejo entrenar_dsee.bat en ia\entrenado)
rem --------------------------------------------------------------------------------------------
rem  Doble clic. El programa busca solo los modelos por genero, propone la carpeta de la rocola,
rem  pregunta genero por genero si ya lo escuchaste y no cierra la ventana hasta que aprietes
rem  Enter. Lo que paso queda en instalar_dsee_genero_log.txt, al lado de este archivo.
rem ============================================================================================
setlocal
cd /d "%~dp0"
set "PYS=%~dp0instalar_dsee_genero.py"
if not exist "%PYS%" (
  echo   [X] Falta instalar_dsee_genero.py en esta misma carpeta:
  echo       %~dp0
  echo       Si abriste esto desde dentro de un ZIP, descomprimelo primero.
  goto espera
)
set "PY="
if exist "%~dp0upscale\venv\Scripts\python.exe" call :prueba "%~dp0upscale\venv\Scripts\python.exe"
if not defined PY call :prueba py -3
if not defined PY call :prueba python
if not defined PY call :prueba python3
if not defined PY (
  echo   [X] No hay un Python que funcione en esta PC. Es el mismo que usa entrenar_dsee.bat:
  echo       bajalo de https://www.python.org/downloads/ con "Add Python to PATH" y luego
  echo       pip install numpy
  goto espera
)
rem El programa se queda abierto solo (pregunta y espera Enter); con argumentos, va directo.
if "%~1"=="" (
  %PY% "%PYS%"
) else (
  %PY% "%PYS%" %*
  goto espera
)
endlocal & exit /b %ERRORLEVEL%

:espera
echo.
echo   Aprieta una tecla para cerrar esta ventana.
pause >nul
endlocal & exit /b 1

:prueba
if defined PY goto :eof
%* -c "import sys" >nul 2>nul
if not "%ERRORLEVEL%"=="0" goto :eof
set "PY=%*"
goto :eof
