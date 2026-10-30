# Alejandría Cliente

App de escritorio (WinUI 3) para que la gente de BCS arme sus solicitudes
a MES con un formulario guiado en vez de una tabla de Word donde cada
quien lo llena distinto — así el PDF que sale siempre tiene el mismo
formato, sin importar quién lo llene, y Alejandría (la otra app de este
repositorio) lo puede leer sin tener que adivinar cómo está escrito.

No toca la base de datos compartida ni la API de iMES — es una app
totalmente independiente. Solo genera un PDF, que se sigue adjuntando a
mano en Altas de Modelos, igual que antes.

## Cómo se usa

Al abrir, pregunta qué tipo de solicitud necesitas:

- **Alta de modelo** — un modelo que todavía no existe en iMES/Unity. La
  solicitud completa: Información General, Modelo, PCB/SAP, Línea,
  Flujo, Interfaces MES, Packout, Aprobaciones. El "Laser Serial Number
  real example" es una lista donde agregas uno por cada componente con
  serial propio (FG, PCBA, T&C, Clock spring...).
- **Cambio de flujo** — cambió la línea, el flujo de operaciones o las
  interfaces MES de un modelo que ya existe. Trae Línea/Diagrama de
  flujo/Interfaces MES, con PCB y FG como campos separados en vez de una
  lista, y un Packout más simple (sin "Nueva estación" ni QR de
  producto).
- **Cambio de modelo** — modificar algo ya registrado de un modelo
  existente (nombre, números de parte, PCB/SAP, packout) sin tocar línea
  ni flujo. Cada campo se captura por partida doble: **Actual** y
  **Nuevo**.

En los tres casos: el llenado es **por secciones, una a la vez** (con
"Atrás"/"Siguiente" y un contador "Paso X de N"), no todo junto en una
sola pantalla larga. En el último paso, "Siguiente" cambia a **"Generar
PDF"**: eliges dónde guardarlo, se abre solo al terminar, y ese PDF es el
que se adjunta en Altas de Modelos, como siempre. El PDF generado calca
las tablas de las plantillas originales (mismos bordes, mismas
etiquetas) — la única diferencia a propósito es el diagrama de flujo: en
vez de dibujarlo a mano en la solicitud, adjuntas una imagen (foto o
captura de tu diagrama) y se pega en el PDF tal cual.

## Historial local

Desde la pantalla inicial, "Ver historial de solicitudes" muestra todo lo
generado en esta computadora, con dos botones por documento:

- **Ver PDF** — lo abre con el visor de PDF de Windows.
- **Editar** — vuelve a abrir el formulario correspondiente, ya lleno con
  los datos que quedaron guardados la primera vez. Al darle "Generar
  PDF" desde ahí, **reemplaza el mismo archivo** en vez de pedir una
  ubicación nueva.

Se guarda en `%LOCALAPPDATA%\AlejandriaCliente\historial\` — un
`historial.json` con los datos de cada solicitud, más una copia de
respaldo de cada PDF (por si el archivo original se mueve o se borra,
"Ver PDF" todavía tiene de dónde abrirlo).

## Qué falta para una v2

- Las aprobaciones son solo nombre y fecha, sin firma electrónica — se
  puede repensar según cómo se quiera que funcione ese flujo.
- Podría escribir, además del PDF, un archivo de datos a un lado (JSON)
  para que Alejandría no tenga ni que leer el PDF — si algún día se
  decide que la solicitud llegue directo a la base de datos compartida en
  vez de como PDF adjunto a mano.
- El PDF dibuja las tablas a mano (bordes, celdas) con PDFsharp-GDI en
  vez de calcar el .docx directamente — el layout es fiel a las
  plantillas originales, pero si algún día cambia el diseño de la
  plantilla oficial, hay que actualizar el código del generador a mano
  (no lee un archivo de plantilla). Cada fila mide su propio texto y
  calcula su altura antes de dibujarse (en vez de una altura fija), para
  que nada se encime ni se corte entre renglones — esto también ayuda a
  que Alejandría lea el texto limpio al procesarlo.

## Compilar y publicar

Mismo proceso que Alejandría (mismo `Program.cs`/Velopack/instalación por
usuario) — ver `Installer/LEEME-VELOPACK.md` en la raíz del repositorio,
sustituyendo `Alejandria` por `AlejandriaCliente` en las rutas y en
`-u` del comando `vpk pack` (el id de Velopack tiene que ser distinto al
de Alejandría, para que Windows los trate como dos apps separadas).
