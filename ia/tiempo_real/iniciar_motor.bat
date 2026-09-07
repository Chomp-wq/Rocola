@echo off
setlocal
cd /d "%~dp0"
if not "%ERRORLEVEL%"=="0" exit /b 1
echo ============================================================
echo  MOTOR DE IA EN VIVO   -   modo visible, para ver que pasa
echo ------------------------------------------------------------
echo  Deja esta ventana ABIERTA. Usa la MISMA cache que la rocola
echo  en ia\tiempo_real\cache, asi que no hay que configurar nada.
echo  Si prefieres que arranque solo y oculto, no uses esto: la
echo  rocola lo lanza al activar DSEE ULTRA.
echo ============================================================
echo.
set "PY=%~dp0..\upscale\venv\Scripts\python.exe"
if not exist "%PY%" ( echo [X] Primero corre el 1_instalar.bat de la carpeta upscale & pause & exit /b 1 )
if not exist "%~dp0daemon_ia.py" ( echo [X] Falta daemon_ia.py junto a este archivo & pause & exit /b 1 )
"%PY%" -c "import sys" >nul 2>nul
if not "%ERRORLEVEL%"=="0" ( echo [X] El Python del entorno no puede arrancar. Corre 1_instalar.bat otra vez. & pause & exit /b 1 )
"%PY%" "%~dp0daemon_ia.py"
set "COD=%ERRORLEVEL%"
echo.
echo El motor se detuvo. Revisa arriba o cache\motor_log.txt si hubo error.
pause
endlocal & exit /b %COD%
