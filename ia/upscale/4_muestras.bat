@echo off
setlocal
cd /d "%~dp0"
echo ============================================================
echo  PILOTO CON CANCIONES DANADAS   -   la prueba de verdad
echo ------------------------------------------------------------
echo  Toma las canciones MAS danadas de tu biblioteca, segun el
echo  CSV del escaner, y arma el A/B. Aqui se oye claro.
echo ============================================================
echo.
if not exist venv\Scripts\python.exe ( echo [X] Primero corre 1_instalar.bat & pause & exit /b 1 )
if not exist calidad_biblioteca.csv ( echo [X] Primero corre 3_escanear.bat & pause & exit /b 1 )
echo [1/2] Eligiendo un abanico de canciones danadas...
venv\Scripts\python preparar_muestras.py
if errorlevel 1 ( pause & exit /b 1 )
echo.
echo [2/2] Restaurando y armando el A/B...
venv\Scripts\python 01_piloto.py --entrada muestras_danadas --salida piloto_danadas
echo.
echo ============================================================
echo  RESULTADO en:  piloto_danadas\ciego
echo   Escucha X / Y sin ver, anota tu favorita, y AL FINAL
echo   abre clave.txt.  ESTA es la prueba que decide todo.
echo ============================================================
pause
