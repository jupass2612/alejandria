@echo off
rem Publica una versión nueva de Alejandría con Velopack.
rem Antes: subir Version en el .csproj y agregar "## X.Y.Z" en el CHANGELOG.
rem Uso (desde cualquier carpeta):  Installer\publicar-version.cmd
rem   /sincopiar  -> solo empaqueta, no sube a la carpeta de red
setlocal EnableExtensions
chcp 65001 >nul

rem Raíz del repo = carpeta de arriba de este script
pushd "%~dp0.."
set "RAIZ=%CD%"
set "CSPROJ=%RAIZ%\Alejandria\Alejandria.csproj"
set "CHANGELOG=%RAIZ%\Alejandria\CHANGELOG.md"
set "PUBLISH=%RAIZ%\Installer\publish"
set "RELEASES=%RAIZ%\Installer\Releases"
set "DESTINO=\\sqromx-prfs01\Departments\IT\MES\Juan Pablo Solis\Alejandria\Actualizaciones"

set "COPIAR=1"
if /i "%~1"=="/sincopiar" set "COPIAR=0"

echo.
echo ===== Publicar Alejandría =====
echo.

rem --- Revisiones antes de empezar ---

where dotnet >nul 2>&1 || (call :error "No encuentro dotnet. Instala el SDK de .NET 8." & goto :fin)
where vpk >nul 2>&1 || (call :error "No encuentro vpk. Instálalo con: dotnet tool install -g vpk --version 0.0.1053" & goto :fin)

rem Versión sacada del .csproj
set "VER="
for /f "tokens=3 delims=<>" %%v in ('findstr /r /c:"^ *.Version.[0-9]" "%CSPROJ%"') do set "VER=%%v"
if not defined VER (call :error "No encontré Version en el .csproj." & goto :fin)

echo %VER%| findstr /r "^[0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*$" >nul || (call :error "La versión '%VER%' no es X.Y.Z." & goto :fin)

rem El CHANGELOG tiene que traer la sección de esta versión
findstr /b /c:"## %VER%" "%CHANGELOG%" >nul || (call :error "Falta '## %VER%' en el CHANGELOG.md." & goto :fin)

rem Si la app está abierta no deja reemplazar el .exe
tasklist /fi "imagename eq Alejandria.exe" | find /i "Alejandria.exe" >nul && (call :error "Alejandría está abierta, ciérrala primero." & goto :fin)

if "%COPIAR%"=="1" if not exist "%DESTINO%\" (call :error "No llego a la carpeta de red. Revisa VPN/red: %DESTINO%" & goto :fin)

rem Traer lo que ya está publicado para que vpk arme los delta
if "%COPIAR%"=="1" (
    echo Trayendo las versiones publicadas de la carpeta de red...
    robocopy "%DESTINO%" "%RELEASES%" /E /XO /NFL /NDL /NJH /NJS /NP >nul
    if errorlevel 8 (call :error "Falló al copiar desde la carpeta de red." & goto :fin)
)

if exist "%RELEASES%\Alejandria-%VER%-full.nupkg" (call :error "La %VER% ya está publicada. Sube la versión en el .csproj." & goto :fin)

rem Aviso si hay cambios sin commit
git -C "%RAIZ%" status --porcelain 2>nul | findstr . >nul && echo [Aviso] Hay cambios sin commit, se van a publicar tal cual.

echo.
echo Versión:  %VER%
if "%COPIAR%"=="1" (echo Destino:  %DESTINO%) else (echo Destino:  solo local, sin copiar)
echo.
set "OK="
set /p "OK=¿Publicar? (S/N): "
if /i not "%OK%"=="S" (echo Cancelado. & goto :fin)

rem --- 1. Publicar ---

echo.
echo [1/3] dotnet publish...
if exist "%PUBLISH%" rmdir /s /q "%PUBLISH%"
dotnet publish "%CSPROJ%" -c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -o "%PUBLISH%"
if errorlevel 1 (call :error "Falló el dotnet publish." & goto :fin)

rem --- 2. Empaquetar ---

echo.
echo [2/3] vpk pack...
vpk pack -u Alejandria -v %VER% -p "%PUBLISH%" -e Alejandria.exe -o "%RELEASES%"
if errorlevel 1 (call :error "Falló el vpk pack." & goto :fin)

rem --- 3. Subir a la carpeta de red ---

if "%COPIAR%"=="0" (
    echo.
    echo [3/3] Sin copiar, quedó en %RELEASES%
    goto :listo
)

echo.
echo [3/3] Copiando a la carpeta de red...
robocopy "%RELEASES%" "%DESTINO%" /E /NFL /NDL /NJH /NP
if errorlevel 8 (call :error "Falló la copia a la carpeta de red." & goto :fin)

:listo
echo.
echo ===== Alejandría %VER% publicada =====
echo Probar en una PC: Ajustes ^> Buscar actualización.
echo.

rem Tag opcional
git -C "%RAIZ%" rev-parse -q --verify "refs/tags/v%VER%" >nul 2>&1 && goto :fin
set "TAG="
set /p "TAG=¿Crear el tag v%VER% y subirlo? (S/N): "
if /i not "%TAG%"=="S" goto :fin
git -C "%RAIZ%" tag -a v%VER% -m "Alejandría %VER%"
for /f %%r in ('git -C "%RAIZ%" remote') do git -C "%RAIZ%" push %%r v%VER%
goto :fin

:error
echo.
echo [Error] %~1
exit /b 1

:fin
popd
echo.
pause
endlocal
