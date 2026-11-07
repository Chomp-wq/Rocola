@echo off
rem DESINSTALAR ROCOLA OS DESDE WINDOWS XP (doble clic). Devuelve el arranque de Windows XP que guardo el instalador de Rocola OS
rem y despues ofrece borrar la carpeta \rocola-os. Lo hace desinstalar-rocola-os.cs (junto a este archivo): se compila con el
rem compilador de C# que trae .NET en Windows (no se baja nada) y se abre. Antes de escribir, comprueba todo y pide confirmar.
rem NO borres \rocola-os antes: el arranque de Rocola OS lee su menu de esa carpeta; sin ella la computadora no prende.
setlocal
set "CS=%~dp0desinstalar-rocola-os.cs"
if not exist "%CS%" goto falta
set "CSC=%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v2.0.50727\csc.exe"
if not exist "%CSC%" goto sincsc
set "EXE=%TEMP%\desinstalar-rocola-os.exe"
set "REG=%TEMP%\desinstalar-rocola-os.txt"
if exist "%EXE%" del /f /q "%EXE%"
"%CSC%" /nologo /target:winexe /optimize+ /r:System.Windows.Forms.dll /out:"%EXE%" "%CS%" > "%REG%" 2>&1
if errorlevel 1 goto nocompila
if not exist "%EXE%" goto nocompila
start "" "%EXE%" "%~dp0."
exit /b 0

:falta
echo Falta desinstalar-rocola-os.cs junto a este archivo: los dos tienen que estar en la misma carpeta.
echo No se toco nada.
pause
exit /b 1

:sincsc
echo No encuentro el compilador de C# de .NET en Windows:
echo   %WINDIR%\Microsoft.NET\Framework\v2.0.50727\csc.exe
echo Sin el, no se puede preparar el desinstalador. No se toco nada.
pause
exit /b 1

:nocompila
type "%REG%"
echo.
echo No se pudo preparar el desinstalador. No se toco nada.
pause
exit /b 1
