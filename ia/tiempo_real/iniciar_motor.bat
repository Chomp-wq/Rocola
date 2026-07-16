@echo off
setlocal
cd /d "%~dp0"
echo ============================================================
echo  MOTOR DE IA EN VIVO   -   para la rocola-hd
echo ------------------------------------------------------------
echo  Mantiene Apollo en la GPU y procesa las canciones que pones
echo  en la rocola. DEJA ESTA VENTANA ABIERTA mientras la uses.
echo ============================================================
echo.
if not exist ..\upscale\venv\Scripts\python.exe ( echo [X] Primero corre el 1_instalar.bat de la carpeta upscale & pause & exit /b 1 )
echo Arrastra aqui la carpeta DONDE ESTA Rocola.exe y presiona Enter.
echo La rocola y el motor deben compartir la carpeta ia_cache que esta ahi.
set /p R=Carpeta de Rocola.exe: 
set R=%R:"=%
if not exist "%R%" ( echo [X] Esa carpeta no existe. & pause & exit /b 1 )
echo.
echo Motor arrancando... la primera vez carga Apollo unos segundos.
..\upscale\venv\Scripts\python daemon_ia.py --cache "%R%\ia_cache"
pause
