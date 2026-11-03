# Publicar una versión nueva de Alejandría (Velopack)

Alejandría se instala y se actualiza con [Velopack](https://velopack.io).
Se instala por usuario en `%LocalAppData%\Alejandria`, sin admin ni UAC.
Las actualizaciones salen de una carpeta de red, sin servidor ni internet.

Todos los comandos se corren desde la raíz del repo (`C:\Users\solisj\alejandria`).

## Qué versión ponerle

`MAJOR.MINOR.PATCH`, por ejemplo `1.22.0`:

- **PATCH**: arreglos chicos (un bug, un texto, un detalle visual).
- **MINOR**: algo nuevo o un cambio que se nota.
- **MAJOR**: cambios grandes de fondo.

Lo que es solo de comentarios o README no sube versión.

## Una sola vez en la PC

```powershell
dotnet tool install -g vpk
```

De preferencia con la misma versión que el paquete `Velopack` del `.csproj`:

```powershell
dotnet tool install -g vpk --version 0.0.1053
```

## Pasos

### 1. Subir la versión

- En `Alejandria/Alejandria.csproj`, cambiar `<Version>`.
- En `Alejandria/CHANGELOG.md`, agregar arriba una sección `## X.Y.Z` con la lista de cambios.
  De ahí salen "Ver notas de versión" en Ajustes y el aviso de qué hay de nuevo.

### 2. Commit y tag

```powershell
git add -A
git commit -m "Alejandría X.Y.Z: <qué cambió>"
git tag -a vX.Y.Z -m "Alejandría X.Y.Z"
git push origin main --tags
```

### 3. Publicar

Cerrar Alejandría antes (si está abierta no deja copiar el `.exe`).

```powershell
dotnet publish Alejandria\Alejandria.csproj -c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -o Installer\publish
```

### 4. Empaquetar

```powershell
vpk pack -u Alejandria -v X.Y.Z -p Installer\publish -e Alejandria.exe -o Installer\Releases
```

- `-u Alejandria`: id de la app, **nunca cambiarlo** o Velopack la toma como otra app.
- `-v X.Y.Z`: igual que `<Version>` del `.csproj`.
- `-o Installer\Releases`: ahí queda todo. Conviene no borrar lo de versiones anteriores, Velopack lo usa para los paquetes delta.

Queda algo así:

- `Alejandria-win-Setup.exe`: instalador para PCs nuevas.
- `Alejandria-win-Portable.zip`: versión portable.
- `Alejandria-X.Y.Z-full.nupkg`, `releases.win.json`, `RELEASES`, `assets.win.json`: lo que leen las instalaciones para actualizarse.

### 5. Subir a la carpeta de red

Copiar **todo** lo de `Installer\Releases\` a la carpeta de `Updates:Folder` en `appsettings.json`:

```
\\sqromx-prfs01\Departments\IT\MES\Juan Pablo Solis\Alejandria\Actualizaciones
```

```powershell
robocopy Installer\Releases "\\sqromx-prfs01\Departments\IT\MES\Juan Pablo Solis\Alejandria\Actualizaciones" /E
```

### 6. Probar

En una PC que ya la tenga: Ajustes → **Buscar actualización**. Debe encontrar la versión nueva, instalarla y abrir sola.

## PC nueva

Pasarle `Alejandria-win-Setup.exe` (de la carpeta de red o por USB) y correrlo una vez. No pide admin y ya queda conectada a la carpeta de actualizaciones.

## Notas

- `Installer\publish` y `Installer\Releases` no se suben a git (están en el `.gitignore`).
- Sin firma de código, SmartScreen puede avisar la primera vez ("Windows protegió su PC"). Es normal en una app interna: **Más información → Ejecutar de todas formas**.
