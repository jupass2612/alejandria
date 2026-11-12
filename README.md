# Alejandría — Seguimiento de Altas de Modelos

App de escritorio (WinUI 3, .NET 8) para llevar el seguimiento de las altas
de modelo en iMES: qué entró, en qué va cada área y quién hizo qué.
Corre contra una SQLite en la red, así que se usa desde varias PCs a la vez.

Alejandría **no escribe en Oracle**: el alta en iMES se hace a mano y aquí
solo se registra. Lo único que consulta es la API web de iMES.

## Cómo se usa

1. **Altas de Modelos**: llega la solicitud y se da de alta con nombre,
   números de parte (SMT / BE / TLA), quién la mandó, quién la recibe y el PDF.
   Las acciones de cada fila (Editar, Ver/Adjuntar PDF, Reemplazar PDF) están en el menú **⋯**.
2. **Modelos**: aparecen los pendientes, con los atrasados arriba.
   Se puede buscar por nombre o número de parte y filtrar por
   Pendientes · En proceso · Atrasados. La columna **Avance** tiene un punto por paso.
3. **Configurar** (clic en la fila), por área y en 4 pasos:
   1. **MODELS_PARENTS**: quién lo dio de alta (o Ya existente).
   2. **MODELS_MASTER**: el **MODELID** que quedó y quién lo dio de alta.
   3. **MODELS_SERIALNUMBERSPECS**: serial de ejemplo (a mano o **Extraer del PDF**),
      plantilla (GM SMT, GM o Tesla) y MODELID → tabla de cómo debe quedar y el **INSERT** para copiar.
      Luego **Probar en iMES** y quién dio de alta SerialSpecs.
   4. **Plataforma iMES**: Work Order, Ruta/Flujo y Línea con **Buscar en iMES**, y PartLineBomMaintenance.

   Arriba está el área, el estado del documento, el plazo (5 días hábiles) y lo que falta.
   Al completar un paso pasa solo al siguiente (se apaga en Ajustes). Se guarda solo a los 2 s.
4. **Histórico**: cuando todas las áreas quedan Terminadas **y hay PDF**, el modelo pasa aquí
   y ya no se edita. Si cambia un número de parte se usa **Actualizar** (crea un modelo nuevo
   copiando lo que no cambió). **Ver detalles** muestra la config guardada.
5. **Estadísticas**: KPIs, configuraciones por persona y reportes en **Excel** y **PDF**
   (todo Histórico o los modelos que elijas).

**Estado del documento**: Correcto o Pendiente a correcciones. No frena el avance;
si queda pendiente, en Histórico sale **Adjuntar PDF** para subir el corregido.

### Atajos

| Atajo | Qué hace |
| --- | --- |
| Ctrl+N | Añadir modelo (desde cualquier pantalla) |
| Ctrl+F | Buscar (Modelos e Histórico) |
| Ctrl+S / Enter | Guardar en Configurar (Enter no guarda si estás en una caja de texto) |
| Alt+← / Alt+→ | Paso anterior / siguiente |

## Requisitos

- Windows 10 1809+ o Windows 11
- .NET 8 SDK (o más nuevo)
- Rider o Visual Studio con Windows App SDK

## Compilar y correr

Abrir `Alejandria.sln`, plataforma **x64** (WinUI 3 no compila en AnyCPU) y correr.
No necesita MSIX.

```powershell
dotnet build Alejandria\Alejandria.csproj -c Debug -p:Platform=x64
```

Si sale que no puede copiar `Alejandria.exe`, la app está abierta: ciérrala y vuelve a compilar.

## Configuración

`appsettings.json` junto al `.exe`:

```json
{
  "DatabasePath": "\\\\sqromx-prfs01\\Departments\\IT\\MES\\Juan Pablo Solis\\DB\\altas_modelos.db",
  "ImesApi": {
    "BaseUrl": "https://sqromx-imesweb.ad.one-bcs.com/imes-api/",
    "BearerToken": ""
  },
  "Updates": {
    "Folder": "\\\\sqromx-prfs01\\Departments\\IT\\MES\\Juan Pablo Solis\\Alejandria\\Actualizaciones"
  }
}
```

- `DatabasePath`: la misma en todas las PCs. Las tablas y columnas nuevas se crean solas.
- `ImesApi:BaseUrl`: vacío oculta Buscar en iMES y Probar.
- `Updates:Folder`: carpeta de Velopack.

Junto a la base viven `Documentos` (PDFs adjuntos) y `Respaldos` (copia diaria, 30 días).

**SQLite en red**:

- Si dos guardan al mismo tiempo, uno espera (busy_timeout de 5 s).
- No abrir el `.db` con DB Browser mientras alguien lo usa.
- Sin red o VPN avisa a los 8 s en vez de quedarse cargando.

## Ajustes

- **Tema**: claro, oscuro o el de Windows.
- **Notificaciones**: avisos al guardar.
- **Buscar en iMES automáticamente**: al entrar a un área sin Work Order/Ruta/Línea.
- **Avanzar de paso automáticamente**: en Configurar.
- **Actualizaciones**: Buscar actualización y Ver notas de versión.
- **Registro de errores**: Ver registro (últimos 5 días) y Abrir carpeta.

Las preferencias son de cada PC: `%LOCALAPPDATA%\Alejandria\preferences.json`.

## Integración con iMES

Con la sesión de Windows (no pide contraseña). Si algún día dan un token, va en `ImesApi:BearerToken`.

- **Buscar en iMES**: `GET /imes/getWorkOrderForNonLine?Ipn={número de parte}`.
  Trae todos los work orders del número de parte sin importar la línea. Si hay varios
  se elige uno en la lista; si hay uno se llena solo. En SMT-BE se busca el de SMT y el de BE.
  Work Order, Ruta y Línea son de solo lectura; si no encuentra nada se puede
  **Volver a intentar** o **Ingresar manual**.
- **Probar en iMES**: `GET /imes/SMT/QRO/GetModelDataFromSn?serialNumber={serial}`.
  Muestra el payload completo.
- **Extraer del PDF**: busca "Laser Serial Number real example" en el PDF adjunto;
  si trae varios (FG, PCBA, Clock spring...) deja elegir.

## Plantillas de MODELS_SERIALNUMBERSPECS

Están en `Services/SerialSpecPlantillas.cs`. Cada una tiene la vista previa (en C#) y el INSERT.

- **GM SMT**: posiciones fijas con `SUBSTR` (mínimo 28 caracteres).
- **Tesla**: posiciones fijas con `SUBSTR` (mínimo 29 caracteres).
- **GM**: código 2D, se parte con la misma regex que en Oracle.

El INSERT toma `MAX(ID)` y suma 1, 2, 3… por fila. **Siempre revisarlo antes de correrlo.**

## Otros

- **Una sola instancia**: no deja abrirla dos veces en la misma PC (Mutex en `Program.cs`).
- **Log de errores**: `%LOCALAPPDATA%\Alejandria\logs\`, un archivo por día, 30 días.
  Las rutas de red se tapan antes de guardar.

## Actualizaciones

En Ajustes, **Buscar actualización** revisa `Updates:Folder`. Si hay versión nueva la descarga,
la instala y abre la app otra vez. Velopack instala por usuario, no pide admin.

**Ver notas de versión** muestra el `CHANGELOG.md`. Después de actualizar, las notas
de esa versión salen solas una vez.

## Publicar una versión nueva (Velopack)

Todo desde la raíz del repo (`C:\Users\solisj\alejandria`).

**Qué versión ponerle** (`MAJOR.MINOR.PATCH`):

- **PATCH**: arreglos chicos (un bug, un texto, un detalle visual).
- **MINOR**: algo nuevo o un cambio que se nota.
- **MAJOR**: cambios grandes de fondo.

Lo que es solo de comentarios o README no sube versión.

**Una sola vez en la PC**, instalar `vpk` (de preferencia la misma versión
que el paquete `Velopack` del `.csproj`):

```powershell
dotnet tool install -g vpk --version 0.0.1053
```

### 1. Subir la versión

- `Alejandria/Alejandria.csproj` → cambiar `<Version>`.
- `Alejandria/CHANGELOG.md` → agregar arriba `## X.Y.Z` con la lista de cambios.

### 2. Commit y tag

```powershell
git add -A
git commit -m "Alejandría X.Y.Z: <qué cambió>"
git tag -a vX.Y.Z -m "Alejandría X.Y.Z"
git push origin main --tags
```

### 3. Publicar con el script

Doble clic a `Installer\publicar-version.cmd` (o correrlo desde cmd). Hace los pasos 4 a 6 solo:

- Toma la versión del `.csproj` y revisa que el `CHANGELOG.md` traiga `## X.Y.Z`.
- Revisa que `dotnet` y `vpk` estén instalados, que Alejandría esté cerrada y que haya red.
- Baja lo que ya está publicado en la carpeta de red (para los delta).
- `dotnet publish`, `vpk pack` y copia todo a la carpeta de actualizaciones.
- Al final pregunta si crea el tag `vX.Y.Z` (si no existe) y lo sube a los remotos.

Con `publicar-version.cmd /sincopiar` solo empaqueta y no toca la carpeta de red.

Si algo falla se puede hacer a mano:

### 4. Publicar a mano

Con Alejandría cerrada (si está abierta no deja copiar el `.exe`):

```powershell
dotnet publish Alejandria\Alejandria.csproj -c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -o Installer\publish
```

### 5. Empaquetar

```powershell
vpk pack -u Alejandria -v X.Y.Z -p Installer\publish -e Alejandria.exe -o Installer\Releases
```

- `-u Alejandria`: id de la app, **nunca cambiarlo**.
- `-v X.Y.Z`: igual que `<Version>`.
- No borrar lo de versiones anteriores en `Installer\Releases`, Velopack lo usa para los paquetes delta.

Queda `Alejandria-win-Setup.exe` (instalador para PCs nuevas),
`Alejandria-win-Portable.zip` y los archivos que leen las instalaciones
para actualizarse (`.nupkg`, `releases.win.json`, `RELEASES`, `assets.win.json`).

### 6. Subir a la carpeta de red

Copiar **todo** `Installer\Releases` a la carpeta de actualizaciones:

```powershell
robocopy Installer\Releases "\\sqromx-prfs01\Departments\IT\MES\Juan Pablo Solis\Alejandria\Actualizaciones" /E
```

### 7. Probar

En una PC que ya la tenga: Ajustes → **Buscar actualización**.

**PC nueva**: correr `Alejandria-win-Setup.exe` una vez, no pide admin.

`Installer\publish` e `Installer\Releases` no se suben a git. Sin firma de
código SmartScreen puede avisar la primera vez: **Más información →
Ejecutar de todas formas**.

## Estructura

```
Alejandria/
├── Models/        ModelRecord, DetalleConfiguracion, SpecPreviewFila y los DTO de iMES
├── Services/
│   ├── ModelDatabaseService.cs       SQLite (CRUD y migraciones)
│   ├── ConfigStatusHelper.cs         Reglas de estatus y de los 4 pasos
│   ├── SerialSpecPlantillas.cs       Plantillas GM SMT / GM / Tesla
│   ├── ImesApiService.cs             API de iMES
│   ├── PdfSerialExtractorService.cs  Seriales del PDF adjunto (PdfPig)
│   ├── DocumentAttachmentService.cs  PDFs adjuntos
│   ├── ExcelExportService.cs         Reporte Excel (ClosedXML)
│   ├── PdfReportService.cs           Reporte PDF (PDFsharp)
│   ├── DatabaseBackupService.cs      Respaldo diario
│   ├── UpdateService.cs              Velopack
│   ├── ToastService.cs               Avisos abajo de la ventana
│   └── LogService.cs                 Log de errores
├── ViewModels/    Uno por pantalla
├── Views/         Páginas y diálogos
├── Controls/      StatusBadge, EmptyState
├── Converters/
└── Styles/        Paleta (Fluent + Atlassian), tarjetas y barra de título
```

## Problemas conocidos

- **Se queda cargando**: revisar VPN/red, la ruta compartida no responde.
