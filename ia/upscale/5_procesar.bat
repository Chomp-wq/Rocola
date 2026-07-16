@echo off
setlocal
cd /d "%~dp0"
echo ============================================================
echo  PROCESADOR MASIVO   -   toda la biblioteca en calidad HD
echo ------------------------------------------------------------
echo  Mejora TODAS las canciones danadas y copia las buenas tal
echo  cual, en una carpeta NUEVA. Se puede cerrar y retomar.
echo  La carpeta nueva tambien te sirve de RESPALDO.
echo  Puede tardar horas: dejalo corriendo, hasta de noche.
echo ============================================================
echo.
if not exist venv\Scripts\python.exe ( echo [X] Primero corre 1_instalar.bat & pause & exit /b 1 )
if not exist calidad_biblioteca.csv ( echo [X] Primero corre 3_escanear.bat & pause & exit /b 1 )
set /p M=ARRASTRA aqui la carpeta de TU musica y Enter: 
set M=%M:"=%
if not exist "%M%" ( echo [X] Esa carpeta no existe. & pause & exit /b 1 )
set /p S=ARRASTRA aqui una carpeta VACIA en OTRO disco para la copia HD y Enter: 
set S=%S:"=%
echo.
echo Formato de salida:  mp3 = compatible con todo, 320k.  flac = maxima calidad, mas pesado.
set /p F=Escribe mp3 o flac y Enter (Enter = mp3): 
if "%F%"=="" set F=mp3
venv\Scripts\python 05_procesar_todo.py --musica "%M%" --salida "%S%" --formato %F%
echo.
pause
