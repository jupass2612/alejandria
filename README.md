# Alejandría — Seguimiento de Altas de Modelos

App de escritorio (WinUI 3, Windows 11) que uso para llevar el seguimiento
de las altas de modelo en iMES: qué modelos entraron, en qué van de su
configuración de base de datos y de Plataforma iMES, y quién hizo qué.
Todo corre contra una base SQLite compartida en red, para que se pueda
usar desde varias computadoras a la vez.

## Cómo funciona, de principio a fin

1. **Llega una solicitud de modelo nuevo** (el formato "BCS Unity System:
   New model & part number request"). Lo doy de alta en **Altas de
   Modelos**: nombre, números de parte por área (SMT/BE/TLA — el que
   corresponda), quién lo mandó, quién lo recibe, y adjunto el PDF de la
   solicitud ahí mismo.

2. En **Modelos** aparece automáticamente en cuanto tiene al menos un
   número de parte. Le doy **Configurar** y ahí capturo, por cada área
   que tenga:
   - **Base de datos iMES**: MODELS_PARENTS, MODELS_MASTER,
     MODELS_SERIALNUMBERSPECS y su Packing.
   - **Plataforma iMES**: Work Order, Ruta y Línea (estos ya no se
     escriben a mano — se traen directo de iMES, ver abajo), más Flujo y
     PartLineBomMaintenance.
   - **Estado del Documento** (junto al selector de área): Correcto o
     Pendiente a correcciones — es independiente del avance de la
     configuración, no bloquea que el modelo llegue a Terminado. Si
     queda Pendiente a correcciones, en Histórico se habilita un botón
     "Adjuntar PDF" para subir la versión corregida sin usar "Actualizar".

3. Para no capturar a ciegas, hay dos ayudas conectadas a la API real de
   iMES:
   - **"Buscar en iMES"**: trae el Work Order, la Ruta y la(s) Línea(s)
     activas para el número de parte del área que estoy viendo.
   - **"Probar un serial"**: capturo un serial real (o lo extraigo del
     PDF adjunto con un clic) y me dice a qué modelo pertenece según
     iMES — si lo reconoce, ya sé que MODELS_SERIALNUMBERSPECS y su
     Packing están bien. Quién dio de alta cada tabla se marca aparte,
     en su selector.
   - **Vista previa de MODELS_SERIALNUMBERSPECS**: con el mismo serial,
     eligiendo la plantilla del cliente (Tesla o GM) y el MODELID, muestra
     cómo queda cada segmento y arma el INSERT listo para copiar.

4. En cuanto **todas las áreas de un modelo** quedan Terminadas (y ya
   tiene su PDF adjunto — sin eso no cuenta como Terminado, aunque todo
   lo demás esté lleno), el modelo se mueve solo a **Histórico** y deja
   de poder editarse. Si después cambia un número de parte, no se edita
   el histórico: se usa el botón **"Actualizar"**, que crea un modelo
   nuevo copiando lo que no cambió. El botón **"Ver detalles"** muestra
   de solo lectura la configuración ya guardada de cada área (incluyendo
   quién la completó) — sin ningún control editable.

5. Cuando necesito mandar evidencia o revisar el avance general, uso
   **Estadísticas**: ahí están los KPIs, quién ha hecho cuántas
   configuraciones, y los botones para exportar **Excel** y **PDF**
   (general, de todo Histórico, o de los modelos que yo elija). En el
   PDF, los campos completos se ven como "Terminado" — no muestran el
   nombre de quién los hizo (eso se consulta desde "Ver detalles" en
   Histórico).

## Requisitos

- Windows 11 (o Windows 10 1809+)
- Visual Studio 2022 (17.9+) con los workloads **.NET Desktop
  Development** y **Windows App SDK / WinUI application development**
- .NET 8 SDK

## Compilar y correr

1. Abrir `Alejandria.sln` en Visual Studio.
2. Dejar que NuGet restaure los paquetes (Windows App SDK,
   CommunityToolkit.Mvvm, Microsoft.Data.Sqlite, ClosedXML, PDFsharp-GDI,
   PdfPig).
3. Seleccionar plataforma `x64` (WinUI 3 no compila como `AnyCPU`).
4. F5. La app corre sin empaquetar, no necesita instalación MSIX para
   desarrollar.

```powershell
dotnet build Alejandria.sln -c Debug -p:Platform=x64
```

Para el instalador y las actualizaciones se usa [Velopack](https://velopack.io),
ver [Publicar una versión nueva](#publicar-una-versión-nueva-velopack).

## Base de datos compartida

`appsettings.json` (junto al .exe) apunta a la ruta de red:

```json
{
  "DatabasePath": "\\\\sqromx-prfs01\\Departments\\IT\\MES\\Juan Pablo Solis\\DB\\altas_modelos.db",
  "ImesApi": {
    "BaseUrl": "https://sqromx-imesweb.ad.one-bcs.com/imes-api/",
    "BearerToken": ""
  }
}
```

Tiene que ser la misma ruta en todas las computadoras. La app crea la
tabla sola la primera vez que se conecta. Un par de cosas a tener en
cuenta con SQLite sobre red:

- Si dos personas guardan al mismo tiempo, una espera un poco a la otra
  (ya hay un `busy_timeout` configurado para eso) — con el volumen normal
  de altas no debería notarse.
- No abrir el `.db` con DB Browser u otro programa mientras alguien más
  lo está usando, para no pelearse el bloqueo del archivo.
- Si la ruta de red no está disponible (VPN apagada, servidor caído), la
  app avisa en unos segundos en vez de quedarse cargando para siempre.

## Una sola instancia por computadora

No se puede abrir Alejandría dos veces en la misma computadora — si lo
intentas, sale un aviso ("Alejandría ya está abierta en esta
computadora") y la segunda ventana ni se abre. Se controla con un Mutex
del sistema (`Program.cs`, antes de que arranque cualquier ventana). Si
alguna vez hace falta permitir varias instancias (por ejemplo, para
probar dos configuraciones de red en la misma computadora), avísame y lo
ajusto.

## Registro de errores

`LogService` guarda en esta computadora (`%LOCALAPPDATA%\Alejandria\logs\`,
un archivo `.log` por día, últimos 30 días) los errores que le pasen a la
app — desde fallas al guardar/consultar la base de datos hasta
excepciones que ni la propia app supo manejar (enganchado en
`App.xaml.cs` y, para hilos/tareas aparte, en `Program.cs`). En Ajustes
hay dos botones para revisarlo sin salir de la app: **"Ver registro"**
(los últimos 5 días, en una ventana) y **"Abrir carpeta"** (para verlo
directo en el Explorador, o mandarlo a alguien si hace falta ayuda para
diagnosticar algo).

## Actualizaciones

En Ajustes, **Buscar actualización** revisa la carpeta de red de
`Updates:Folder` (`appsettings.json`). Si hay versión nueva la descarga,
la instala y abre la app otra vez. Velopack instala por usuario, no pide admin.

**Ver notas de versión** muestra el `CHANGELOG.md`. Después de actualizar,
las notas de esa versión salen solas una vez (la última vista se guarda en
`preferences.json` de cada PC).

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

### 3. Publicar

Con Alejandría cerrada (si está abierta no deja copiar el `.exe`):

```powershell
dotnet publish Alejandria\Alejandria.csproj -c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -o Installer\publish
```

### 4. Empaquetar

```powershell
vpk pack -u Alejandria -v X.Y.Z -p Installer\publish -e Alejandria.exe -o Installer\Releases
```

- `-u Alejandria`: id de la app, **nunca cambiarlo**.
- `-v X.Y.Z`: igual que `<Version>`.
- No borrar lo de versiones anteriores en `Installer\Releases`, Velopack lo usa para los paquetes delta.

Queda `Alejandria-win-Setup.exe` (instalador para PCs nuevas),
`Alejandria-win-Portable.zip` y los archivos que leen las instalaciones
para actualizarse (`.nupkg`, `releases.win.json`, `RELEASES`, `assets.win.json`).

### 5. Subir a la carpeta de red

Copiar **todo** `Installer\Releases` a la carpeta de actualizaciones:

```powershell
robocopy Installer\Releases "\\sqromx-prfs01\Departments\IT\MES\Juan Pablo Solis\Alejandria\Actualizaciones" /E
```

### 6. Probar

En una PC que ya la tenga: Ajustes → **Buscar actualización**.

**PC nueva**: correr `Alejandria-win-Setup.exe` una vez, no pide admin.

`Installer\publish` e `Installer\Releases` no se suben a git. Sin firma de
código SmartScreen puede avisar la primera vez: **Más información →
Ejecutar de todas formas**.

## Integración con iMES

- **"Buscar en iMES"** llama a
  `GET /imes/getWorkOrderForNonLine?Ipn={número de parte}`. Un mismo
  número de parte puede tener varios work orders activos (uno por línea
  física), y el área a la que pertenece cada uno se decide por el campo
  `LINE` de la respuesta, no por el nombre del work order (porque ese
  estándar de nombres va cambiando). Cuando hay varias líneas de la misma
  área, se juntan en el campo Línea separadas por coma.
- Work Order, Ruta y Línea normalmente son de solo lectura (se llenan con
  "Buscar en iMES", para que no se desincronicen de lo que ya está
  registrado ahí). Si esa búsqueda no encuentra nada (por ejemplo, el
  Work Order todavía no está activo en iMES), sale un aviso con dos
  opciones: **"Volver a intentar"** (repite la búsqueda) o **"Ingresar
  manual"** — solo con esta última se destraban los 3 campos para
  capturarlos a mano como respaldo. Se vuelven a bloquear en cuanto una
  búsqueda sí encuentra algo, o al cambiar de área.
- **"Probar un serial"** llama a
  `GET /imes/SMT/QRO/GetModelDataFromSn?serialNumber={serial}`. El botón
  "Extraer del PDF adjunto" saca el serial directo del PDF de solicitud
  (sección "Laser Serial Number real example") en vez de tener que
  copiarlo a mano — ese campo a veces trae más de un serial (FG, PCBA,
  Clock spring...), así que si encuentra varios te deja elegir cuál
  probar.
- La autenticación es integrada de Windows (misma sesión con la que ya
  se entra a iMES sin pedir contraseña). Si algún día dan una cuenta de
  servicio con token fijo, se pone en `ImesApi:BearerToken`.
- Si `ImesApi:BaseUrl` está vacío, los botones que dependen de esto
  simplemente no aparecen.

## Estructura del proyecto

```
Alejandria/
├── Models/            ModelRecord, DetalleConfiguracion, y los DTO de iMES (ImesWorkOrder, ImesModeloPorSerial)
├── Services/
│   ├── ModelDatabaseService.cs      CRUD sobre SQLite
│   ├── ConfigStatusHelper.cs        Reglas de estatus (Pendiente/En Proceso/Terminado/Bloqueado)
│   ├── ImesApiService.cs            Cliente de la API de iMES
│   ├── PdfSerialExtractorService.cs Saca seriales de un PDF adjunto (PdfPig)
│   ├── DocumentAttachmentService.cs Copia/abre el PDF adjunto por modelo
│   ├── ExcelExportService.cs        Reporte Excel (ClosedXML)
│   ├── PdfReportService.cs          Reporte PDF (PDFsharp-GDI)
│   └── BusinessDayHelper.cs         Plazo de 5 días hábiles y días de retraso
├── ViewModels/         Uno por pantalla: AltasModelos, Modelos, Historico, ModelConfig, Estadisticas
├── Views/               Las páginas y diálogos (AddModelDialog, UpdateModelDialog)
├── Controls/             StatusBadge
├── Converters/           BoolToVisibility, StatusToBrush, KpiTonoToBrush, CountToVisibility
└── Styles/                Paleta, tipografía y estilos (ColorsAndBrushes, CardStyles, TitleBarStyles)
```

## Problemas conocidos

- **Ventana en negro al abrir**: no debería pasar — se quitó Mica/Acrylic
  por completo, el fondo es sólido. Si vuelve a salir negra, probablemente
  sea otra cosa.
- **Se queda cargando sin avanzar**: revisa la VPN/red de la empresa —
  si la ruta compartida no es alcanzable, la conexión tarda en fallar. Ya
  hay un límite de 8 segundos para que avise en vez de trabarse.
- **PdfPig no compila / versión no encontrada**: revisa en NuGet.org cuál
  es la versión más reciente y actualízala en el `.csproj` — la API que
  se usa (`PdfDocument.Open`, `.GetPages()...Text`) es estable entre
  versiones.
