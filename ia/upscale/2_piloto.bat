@echo off
setlocal
cd /d "%~dp0"
if not "%ERRORLEVEL%"=="0" exit /b 1
echo ============================================================
echo  PILOTO DE UPSCALING   (paso 2 de 2)
echo ------------------------------------------------------------
echo  Necesitas una carpeta con 12-20 canciones MP3 de la rocola
echo  (variadas: cumbia, regional, K-pop, metal, 80s...).
echo ============================================================
echo.
set "PY=%~dp0venv\Scripts\python.exe"
if not exist "%PY%" (
  echo [X] Primero corre 1_instalar.bat
  pause
  exit /b 1
)
set "ENTRADA="
set /p "ENTRADA=ARRASTRA aqui la carpeta con tus canciones y presiona Enter: "
set "ENTRADA=%ENTRADA:"=%"
if not defined ENTRADA (
  echo [X] No escribiste ninguna carpeta.
  pause
  exit /b 1
)
"%PY%" -c "import sys" >nul 2>nul
if not "%ERRORLEVEL%"=="0" ( echo [X] El Python del entorno no puede arrancar. Corre 1_instalar.bat otra vez. & pause & exit /b 1 )
if not exist "%~dp001_piloto.py" ( echo [X] Falta 01_piloto.py & pause & exit /b 1 )
if not exist "%ENTRADA%\." (
  echo [X] Esa carpeta no existe: "%ENTRADA%"
  pause
  exit /b 1
)
set "SALIDA=%~dp0piloto_resultado"
echo.
echo Procesando... (unos minutos; veras el avance cancion por cancion)
"%PY%" "%~dp001_piloto.py" --entrada "%ENTRADA%" --salida "%SALIDA%"
set "COD=%ERRORLEVEL%"
if not "%COD%"=="0" (
  echo.
  echo [X] El piloto fallo. No se anuncia un resultado incompleto.
  pause
  endlocal & exit /b %COD%
)
findstr /R "." "%SALIDA%\ciego\clave.txt" >nul 2>nul
if not "%ERRORLEVEL%"=="0" (
  echo.
  echo [X] El proceso termino sin producir ningun par A/B valido.
  pause
  endlocal & exit /b 1
)
echo.
echo ============================================================
echo  RESULTADO en:  %SALIDA%
echo   1) Abre la carpeta "ciego" y escucha cada par X / Y
echo   2) Anota cual te gusto mas de cada numero
echo   3) Al FINAL abre clave.txt para ver cual era cual
echo ============================================================
pause
endlocal & exit /b %COD%
