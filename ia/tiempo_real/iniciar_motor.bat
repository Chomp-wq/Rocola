@echo off
setlocal
cd /d "%~dp0"
echo ============================================================
echo  MOTOR DE IA EN VIVO   -   modo visible, para ver que pasa
echo ------------------------------------------------------------
echo  Deja esta ventana ABIERTA. Usa la MISMA cache que la rocola
echo  en ia\tiempo_real\cache, asi que no hay que configurar nada.
echo  Si prefieres que arranque solo y oculto, no uses esto: la
echo  rocola lo lanza al activar DSEE ULTRA.
echo ============================================================
echo.
if not exist ..\upscale\venv\Scripts\python.exe ( echo [X] Primero corre el 1_instalar.bat de la carpeta upscale & pause & exit /b 1 )
..\upscale\venv\Scripts\python daemon_ia.py
echo.
echo El motor se detuvo. Revisa arriba o cache\motor_log.txt si hubo error.
pause
