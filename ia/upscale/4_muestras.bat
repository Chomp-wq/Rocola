@echo off
setlocal
cd /d "%~dp0"
if not "%ERRORLEVEL%"=="0" exit /b 1
echo ============================================================
echo  PILOTO CON CANCIONES DANADAS   -   la prueba de verdad
echo ------------------------------------------------------------
echo  Toma las canciones MAS danadas de tu biblioteca, segun el
echo  CSV del escaner, y arma el A/B. Aqui se oye claro.
echo ============================================================
echo.
set "PY=%~dp0venv\Scripts\python.exe"
if not exist "%PY%" ( echo [X] Primero corre 1_instalar.bat & pause & exit /b 1 )
if not exist "%~dp0calidad_biblioteca.csv" ( echo [X] Primero corre 3_escanear.bat & pause & exit /b 1 )
"%PY%" -c "import sys" >nul 2>nul
if not "%ERRORLEVEL%"=="0" ( echo [X] El Python del entorno no puede arrancar. Corre 1_instalar.bat otra vez. & pause & exit /b 1 )
if not exist "%~dp0preparar_muestras.py" ( echo [X] Falta preparar_muestras.py & pause & exit /b 1 )
if not exist "%~dp001_piloto.py" ( echo [X] Falta 01_piloto.py & pause & exit /b 1 )
echo [1/2] Eligiendo un abanico de canciones danadas...
"%PY%" "%~dp0preparar_muestras.py"
set "COD=%ERRORLEVEL%"
if not "%COD%"=="0" ( echo [X] No se pudieron preparar las muestras. & pause & endlocal & exit /b %COD% )
echo.
echo [2/2] Restaurando y armando el A/B...
"%PY%" "%~dp001_piloto.py" --entrada "%~dp0muestras_danadas" --salida "%~dp0piloto_danadas"
set "COD=%ERRORLEVEL%"
if not "%COD%"=="0" (
  echo [X] No se pudo generar el piloto A/B.
  pause
  endlocal & exit /b %COD%
)
findstr /R "." "%~dp0piloto_danadas\ciego\clave.txt" >nul 2>nul
if not "%ERRORLEVEL%"=="0" ( echo [X] No se produjo ningun par A/B valido. & pause & endlocal & exit /b 1 )
echo.
echo ============================================================
echo  RESULTADO en:  piloto_danadas\ciego
echo   Escucha X / Y sin ver, anota tu favorita, y AL FINAL
echo   abre clave.txt.  ESTA es la prueba que decide todo.
echo ============================================================
pause
endlocal & exit /b %COD%
