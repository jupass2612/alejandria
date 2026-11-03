# Empaquetar y publicar Alejandría con Velopack

Alejandría usa [Velopack](https://velopack.io) para instalarse y
actualizarse sola — reemplaza el instalador de Inno Setup que se usaba
antes. Velopack instala **por usuario** (en `%LocalAppData%\Alejandria`),
así que ya no hace falta ser administrador ni aceptar un cuadro de UAC
para instalar o actualizar.

## Qué versión ponerle

`MAJOR.MINOR.PATCH` (ej. `1.1.1`):

- **PATCH** (último dígito): cambios chicos — arreglar un bug, ajustar un
  texto, un detalle visual.
- **MINOR** (segundo dígito): cambios medianos — una función nueva, un
  cambio de comportamiento notorio.
- **MAJOR** (primer dígito): cambios enormes — algo que cambia cómo
  funciona la app de fondo (ej. la migración a Velopack).

**Importante**: cada vez que subas la versión, agrega también una
sección nueva arriba de `Alejandria/CHANGELOG.md` (formato
`## X.Y.Z` seguido de una lista de los cambios) — de ahí sale tanto el
botón "Ver notas de versión" en Ajustes como la ventana de "qué hay de
nuevo" que aparece sola la primera vez que se abre una versión recién
actualizada.

## Requisitos (en la máquina donde compilas)

1. Visual Studio 2022 con el proyecto compilando en Release.
2. La herramienta de línea de comandos de Velopack:

   ```powershell
   dotnet tool install -g vpk
   ```

## Paso 1: Publicar la app

Igual que antes:

```powershell
dotnet publish Alejandria\Alejandria.csproj -c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -o Installer\publish
```

## Paso 2: Empaquetar con vpk

Desde la carpeta raíz del repositorio:

```powershell
vpk pack -u Alejandria -v 1.1.0 -p Installer\publish -e Alejandria.exe -o Installer\Releases
```

- `-u Alejandria`: el id único de la app — **no lo cambies entre
  versiones**, es lo que permite que Velopack reconozca una actualización
  en vez de tratarla como una app distinta.
- `-v 1.1.0`: el número de versión — tiene que coincidir con `<Version>`
  en `Alejandria.csproj` (súbelo ahí antes de este paso).
- `-e Alejandria.exe`: el ejecutable principal.
- `-o Installer\Releases`: carpeta donde queda todo lo generado.

Esto deja en `Installer\Releases\`:

- **`AlejandriaSetup.exe`**: el instalador para quien todavía no tiene la
  app — se lo mandas a alguien nuevo, lo corre y ya.
- Los archivos del "feed" de actualizaciones (`.nupkg`, metadatos) que
  las instalaciones ya existentes van a leer solas con "Buscar
  actualización".

## Paso 3: Publicar la actualización

Copia **todo el contenido** de `Installer\Releases\` a la carpeta de red
configurada en `appsettings.json` (`Updates:Folder` — la misma unidad
donde ya vive la base de datos), reemplazando lo que hubiera ahí antes.

Con eso, cualquier instalación existente que le dé "Buscar actualización"
en Ajustes va a encontrar la versión nueva, descargarla, y (si el usuario
confirma) instalarla y reiniciar sola.

## Primera instalación en una computadora nueva

Ahí sí se reparte `AlejandriaSetup.exe` directo (por USB, por la red, como
sea) — se corre una vez, sin pedir permisos de administrador, y deja la
app lista más el mecanismo de "Buscar actualización" ya conectado a la
carpeta configurada en `appsettings.json`.

## Firma de código (opcional)

Igual que antes: sin firmar, Windows SmartScreen puede avisar la primera
vez ("Windows protegió su PC") — es normal para software interno. Si la
empresa consigue un certificado de firma de código, `vpk pack` tiene
opciones para firmar automáticamente; dímelo y lo agrego a este mismo
paso a paso.
