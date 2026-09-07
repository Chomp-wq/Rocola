@echo off
rem ============================================================================================
rem  ENTRENAR EL DSEE DE LA ROCOLA
rem --------------------------------------------------------------------------------------------
rem  OJO con "setlocal enabledelayedexpansion": NO se usa a proposito. Con la expansion retardada
rem  encendida, cmd se come los signos de admiracion de cualquier ruta arrastrada, y una carpeta
rem  llamada "Musica!" o "Rocola (2)!" llegaba aqui con el nombre cambiado. Se buscaba el archivo,
rem  no estaba, y la ventana se cerraba sin explicar nada.
rem
rem  Y todo lo importante queda apuntado en entrenar_dsee_log.txt, al lado de este archivo: si la
rem  ventana se cierra sola, ahi esta escrito hasta donde llego.
rem ============================================================================================
setlocal
cd /d "%~dp0"
if not "%ERRORLEVEL%"=="0" exit /b 1
set "LOG=%~dp0entrenar_dsee_log.txt"
set "PYS=%~dp0entrenar_dsee.py"
set "COD=1"

> "%LOG%" echo === ENTRENAR EL DSEE ===  %DATE% %TIME%
>>"%LOG%" echo carpeta: %~dp0

echo ==========================================================================
echo   ENTRENAR EL DSEE DE LA ROCOLA
echo ==========================================================================
echo.

rem ---------- 1) QUE LLEGO ARRASTRADO ----------
rem Se ensena ANTES de nada. Si aqui no aparece nada es que el arrastre no funciono, y eso ya es
rem la respuesta: no hay que adivinar si el fallo vino despues.
echo   LO QUE ME ARRASTRASTE
set "N=0"
:otroarg
if "%~1"=="" goto finargs
set /a N+=1
if %N% GTR 8 goto demasiados
echo      %N%.  "%~1"
>>"%LOG%" echo ARG %N%: "%~1"
rem Cada ruta va en una variable propia. No se conserva %%* ni se usa CALL: ambos hacen que cmd
rem vuelva a interpretar &, |, ^ y %% de una ruta como comandos.
set "A%N%=%~1"
shift
goto otroarg
:finargs
if "%N%"=="0" (
  echo      (nada^)
  >>"%LOG%" echo ARG: ninguno
)
echo.

rem ---------- 2) EL MOTOR TIENE QUE ESTAR AQUI AL LADO ----------
rem Pasa mas de lo que parece: se abre el .bat DENTRO del zip sin descomprimir, y entonces Windows
rem lo copia solo a el a una carpeta temporal. El .py se queda dentro del zip y no hay nada que correr.
if not exist "%PYS%" (
  echo   [X] Falta entrenar_dsee.py, que es el motor de verdad.
  echo       Tiene que estar en esta misma carpeta:
  echo       %~dp0
  echo.
  echo       Si abriste esto desde dentro de un ZIP, DESCOMPRIMELO primero
  echo       (clic derecho sobre el zip -^> Extraer todo^) y prueba desde la carpeta ya extraida.
  >>"%LOG%" echo [X] no existe %PYS%
  goto fin
)

rem ---------- 3) PYTHON QUE DE VERDAD ARRANQUE ----------
rem No basta con encontrarlo: Windows 10/11 traen un "python.exe" de mentira que lo unico que hace
rem es abrir la Tienda y cerrarse. Se prueba a ejecutarlo, que es la unica forma de saberlo.
set "PY="
if exist "%~dp0upscale\venv\Scripts\python.exe" call :prueba "%~dp0upscale\venv\Scripts\python.exe"
if not defined PY call :prueba py -3
if not defined PY call :prueba python
if not defined PY call :prueba python3
if not defined PY (
  echo   [X] No hay un Python que funcione en esta PC.
  echo.
  echo       Bajalo de https://www.python.org/downloads/  y al instalarlo MARCA
  echo       la casilla "Add Python to PATH". Despues abre una ventana de consola y escribe:
  echo.
  echo           pip install numpy
  echo.
  echo       Si ya lo tienes instalado pero sale este mensaje, casi siempre es el Python
  echo       de la Tienda de Windows, que no sirve: desinstalalo y pon el de python.org.
  >>"%LOG%" echo [X] sin python utilizable
  goto fin
)
>>"%LOG%" echo python: %PY%
set "VER=?"
set "VERFILE=%TEMP%\rocola_pyver_%RANDOM%_%RANDOM%.txt"
%PY% -c "import sys;print(sys.version.split()[0])" > "%VERFILE%" 2>nul
if exist "%VERFILE%" for /f "usebackq tokens=*" %%v in ("%VERFILE%") do set "VER=%%v"
del "%VERFILE%" >nul 2>nul
echo   Python %VER%
>>"%LOG%" echo version: %VER%

rem ---------- 4) NUMPY ----------
%PY% -c "import numpy" >nul 2>nul
if not "%ERRORLEVEL%"=="0" (
  echo.
  echo   [X] Falta numpy, que es lo unico que hace falta ademas de Python.
  echo       Abre una ventana de consola y escribe:    pip install numpy
  >>"%LOG%" echo [X] falta numpy
  goto fin
)
echo.

rem ---------- 5) A ENTRENAR ----------
rem Sin nada arrastrado, se mira si el material ya esta copiado en esta misma carpeta.
if not "%N%"=="0" goto conargs
set "ENC="
if exist "rocola_ia_datos.bin" set ENC=%ENC% "rocola_ia_datos.bin"
if exist "rocola_eq.txt"       set ENC=%ENC% "rocola_eq.txt"
if not defined ENC goto sinargs
echo   No arrastraste nada, pero el material esta en esta misma carpeta. Lo uso.
echo.
>>"%LOG%" echo usando material de la propia carpeta
%PY% "%PYS%" %ENC%
goto listo

:sinargs
%PY% "%PYS%"
goto listo

:conargs
if "%N%"=="1" %PY% "%PYS%" "%A1%"
if "%N%"=="2" %PY% "%PYS%" "%A1%" "%A2%"
if "%N%"=="3" %PY% "%PYS%" "%A1%" "%A2%" "%A3%"
if "%N%"=="4" %PY% "%PYS%" "%A1%" "%A2%" "%A3%" "%A4%"
if "%N%"=="5" %PY% "%PYS%" "%A1%" "%A2%" "%A3%" "%A4%" "%A5%"
if "%N%"=="6" %PY% "%PYS%" "%A1%" "%A2%" "%A3%" "%A4%" "%A5%" "%A6%"
if "%N%"=="7" %PY% "%PYS%" "%A1%" "%A2%" "%A3%" "%A4%" "%A5%" "%A6%" "%A7%"
if "%N%"=="8" %PY% "%PYS%" "%A1%" "%A2%" "%A3%" "%A4%" "%A5%" "%A6%" "%A7%" "%A8%"

:listo
set "COD=%ERRORLEVEL%"
>>"%LOG%" echo salida del entrenador: %COD%
if not "%COD%"=="0" (
  echo.
  echo   -- el entrenador termino con el codigo %COD% --
  echo      Lo de arriba explica que paso. Queda apuntado en:
  echo      %LOG%
)

goto fin

:demasiados
set "COD=1"
echo   [X] Arrastraste mas de 8 rutas. Arrastra solo los archivos de entrenamiento
echo       rocola_ia_datos.bin y rocola_eq.txt (o la carpeta que los contiene^).
>>"%LOG%" echo [X] demasiados argumentos: mas de 8

:fin
echo.
echo ==========================================================================
echo   Esta ventana NO se cierra sola. Lee lo de arriba y aprieta una tecla.
echo   Si algo salio mal, lo apuntado esta en: entrenar_dsee_log.txt
echo ==========================================================================
pause >nul
endlocal & exit /b %COD%

rem --- Prueba a arrancar un Python. Solo se queda con el que de verdad responde. ---
:prueba
if defined PY goto :eof
%* -c "import sys" >nul 2>nul
if not "%ERRORLEVEL%"=="0" goto :eof
set "PY=%*"
goto :eof
