# Historial de cambios

## 1.21.0
- Configurar modelo: el encabezado ahora es una sola franja con Área, Estado del documento, Plazo, Estatus del área y Estatus del modelo, más una línea con lo que falta. Antes eran dos tarjetas separadas.
- Se quitaron "Estatus BD Consolidado" y "Estatus Plataforma iMES" del encabezado; ya se ven en la barra de pasos.
- El aviso de "Mismo número de parte en otra área" ya no deja un hueco cuando no aplica.
- Así la barra de pasos queda a la vista al abrir la pantalla.

## 1.20.2
- Corregido: "Quién recibe el documento" en Añadir modelo y Actualizar tenía su propia lista escrita a mano, con "Juan Pablo Solis" en vez de "Juan Pablo Solis Sanchez" como en Configurar. Ahora las dos usan la misma lista, y los registros viejos con el nombre corto se muestran con el nombre completo.
- Corregido: en Histórico el encabezado de la última columna no medía lo mismo que en las filas.

## 1.20.1
- Paso 3 reacomodado en el orden de trabajo: primero se captura el serial de ejemplo, la plantilla y el MODELID; luego se ve la tabla de cómo debe quedar MODELS_SERIALNUMBERSPECS y el INSERT; después "Probar en iMES"; y al final quién lo dio de alta (SerialSpecs y Packing).

## 1.20.0
- Configurar modelo ahora va por pasos, uno a la vez: 1 MODELS_PARENTS, 2 MODELS_MASTER, 3 MODELS_SERIALNUMBERSPECS (con Packing, Probar un serial y la vista previa) y 4 Plataforma iMES.
- Arriba se ven los 4 pasos con su estatus; se puede saltar a cualquiera con un clic o avanzar con Anterior/Siguiente.
- Al abrir un modelo o cambiar de área, arranca en el primer paso que falte.

## 1.19.0
- Nueva "Vista previa de MODELS_SERIALNUMBERSPECS" en Configurar modelo: con el serial de "Probar un serial", la plantilla del cliente (Tesla o GM) y el MODELID, muestra cada segmento (descripción, posición, longitud, dinámico y valor) y arma el INSERT para copiarlo.
- Tesla corta el serial por posiciones fijas; GM lo parte con la misma expresión regular que se usa en Oracle.

## 1.18.0
- Se quitó todo lo relacionado a Oracle (Productivo y Pruebas): Alejandría ya no se conecta a la base de iMES, solo lleva el seguimiento.
- Se quitó el apartado "DataBase" del menú y la tarjeta "Entorno de iMES (Oracle)" de Ajustes.
- En Configurar modelo, el asistente de 3 pasos se cambió por 4 selectores (MODELS_PARENTS, MODELS_MASTER, MODELS_SERIALNUMBERSPECS y su Packing) para marcar quién dio de alta cada uno, o No Aplica.
- "Probar un serial" vuelve a tener su propio campo, con "Extraer del PDF" a un lado.
- "Buscar en iMES" y "Probar un serial" siguen igual: usan la API web de iMES, no Oracle.

## 1.17.0
- Se quitó el entorno Docker de Oracle. Ahora solo quedan Productivo y Pruebas en Ajustes → Entorno de iMES; si alguna computadora tenía Docker elegido, se cambia sola a Pruebas.
- Se quitó la carpeta local de pruebas que se usaba con Docker: Alejandría siempre trabaja contra la base compartida en red.
- Se quitó el proyecto Alejandría Cliente de la solución.

## 1.16.5
- Corregido: al "Extraer del PDF" en el Paso 3, el campo del serial se veía lleno pero "Empezar" no hacía nada hasta darle clic al campo primero. Causa: el enlace de ese campo solo sincroniza el valor real hasta que pierde el foco, no de inmediato cuando se llena por código (como hace la extracción). Ahora se actualiza directo desde el código en cuanto se extrae, sin depender de que alguien le dé clic al campo.

## 1.16.4
- Corregido error de compilación (CS0111) en la 1.16.3 — ExtraerCandidatosSerialDelPdfAsync quedó definido dos veces en ConfigurarEnImesViewModel. Se quitó la copia repetida.

## 1.16.3
- "Extraer del PDF" se movió al Paso 3 (junto al campo del serial de ejemplo) — ahí solo llena el campo con el serial que encuentre en el PDF adjunto, no prueba nada. Para probarlo, se usa el botón "Probar" aparte, en "Probar un serial".
- Ahora en el Paso 3 hay 3 formas de llenar el serial: escribirlo a mano, extraerlo del PDF, o (si ya se hizo antes) que aparezca solo.

## 1.16.2
- "Probar un serial" ya no tiene un campo para escribir el serial a mano — se quitó por completo. Ahora siempre usa el mismo serial de ejemplo que se capturó en el Paso 3 de arriba, y se muestra en pantalla cuál es (se actualiza solo conforme lo vas escribiendo ahí). "Extraer del PDF adjunto y probar" se queda como una segunda opción aparte, sin tocar el del Paso 3.

## 1.16.1
- Corregido crash real reportado ("The ItemsControl.ItemsPanelTemplate must have a derivative of Panel as the root element") — la tarjeta nueva del payload de "Probar un serial" (1.16.0) tenía un Border como raíz de la plantilla del panel, y WinUI3 no lo permite; tiene que ser un panel directo. Se movió el Border a envolver el ItemsControl por fuera en vez de ir dentro de la plantilla.

## 1.16.0
- "Probar un serial" ya no pide elegir "¿quién hizo esta configuración?" para marcar 4 campos a mano — eso quedó viejo desde que el estatus lo determinan los 3 pasos del asistente de Oracle.
- Si ya capturaste un serial de ejemplo en el Paso 3, "Probar" lo usa solo, sin tener que volver a escribirlo.
- En vez del resumen de una línea, ahora se ve el payload completo que regresó iMES, campo por campo, en una tarjeta.

## 1.15.0
- Rediseño: se quitaron las 3 pastillas separadas (MODELS_PARENTS/MASTER/SERIALNUMBERSPECS) de "Configuración Base de Datos — iMES" — dependían de que el seguimiento local en SQLite se mantuviera sincronizado con el asistente, y eso seguía fallando en casos distintos cada vez.
- El estatus general (En Proceso/Terminado, la pastilla de la esquina) ahora se calcula directo de los 3 pasos del asistente de abajo — Terminado solo cuando los 3 muestran "Paso correctamente realizado". Es la misma fuente de verdad que ya se ve funcionando bien en cada paso, sin una copia aparte que se pueda desincronizar.
- El seguimiento local en SQLite se sigue actualizando por dentro (para Histórico/Estadísticas/reportes), solo que ya no hay una pastilla que dependa de leerlo de vuelta.

## 1.14.2
- Corregido: MODELS_MASTER (y MODELS_SERIALNUMBERSPECS) no se marcaban en modelos con SMT y BE combinados ("SMT-BE"). Causa: el marcado estaba escribiendo a dos áreas separadas llamadas literalmente "SMT" y "BE", pero esas ya no existen como áreas propias — desde que SMT y BE se combinan en una sola área "SMT-BE", la pastilla de arriba lee esa área combinada, no las dos por separado. La marca se guardaba en un lugar que nadie leía. Ahora se marca directo la misma área que se está viendo ("SMT-BE" cuando aplica), sin traducir a nombres que ya no existen.

## 1.14.1
- Corregida la causa real de que MODELS_PARENTS a veces no se marcara aunque el Paso 1 mostrara "completado": la pregunta "¿Quién está haciendo esto?" era una sola, arriba de los 3 pasos — si se creaba el modelo padre antes de elegir a alguien ahí, se guardaba sin marcar el seguimiento local, y ya no había forma de repararlo después.
- Ahora la pregunta "¿Quién hizo este paso?" está en cada uno de los 3 pasos por separado (Parent, Master, Segmentos) — no se puede confirmar/guardar un paso sin haber dicho quién lo hizo, así nunca se guarda sin marcar. No hace falta que sea la misma persona en los 3.

## 1.14.0
- Cuando el entorno de Oracle activo (Ajustes → Entorno de iMES) es "Docker", Alejandría ya no usa la base de datos compartida en red — usa una carpeta local de pruebas (C:\Users\solisj\OneDrive - BCS AIS\Documentos\Alejandria Prueba), con su propia base de datos, sus propios Documentos (PDF adjuntos) y sus propios Respaldos. Al cambiar de vuelta a Productivo o Pruebas, regresa sola a la ruta de red real. Así las pruebas contra Docker nunca tocan datos reales.
- La carpeta de pruebas se crea sola la primera vez si no existe.

## 1.13.6
- Corregida la causa real de que las pastillas de estatus se quedaran grises: el control StatusBadge nunca estaba diseñado para cambiar de color después de creado — le faltaba avisarle a sus propios elementos internos que se había actualizado. El arreglo de la 1.13.5 (el aviso del asistente) era necesario pero no suficiente; con este ya se refleja de verdad.
- Quitada la pastilla "MODELS_SERIALNUMBERSPECS Packing" de "Configuración Base de Datos — iMES" — se quedan solo las 3 que corresponden a los 3 pasos reales del asistente (MODELS_PARENTS, MODELS_MASTER, MODELS_SERIALNUMBERSPECS).

## 1.13.5
- Corregido: las 4 pastillas de estatus arriba de "Configuración Base de Datos — iMES" se quedaban grises aunque el asistente de abajo ya mostrara los 3 pasos como "correctamente realizado". Causa: el asistente escribe el seguimiento local directo a la base de datos, pero nadie le avisaba a la pantalla que lo tenía cargado en memoria que había cambiado algo. Ahora el asistente avisa en cuanto marca algo, y las pastillas se refrescan solas sin reiniciar el asistente ni perder en qué paso estabas.

## 1.13.4
- El "ya está hecho" que descubre el asistente (consultando Oracle directo) ahora sí afecta los KPI de Estadísticas — antes solo cambiaba la vista del asistente, pero si todavía no habías elegido "quién está haciendo esto", el seguimiento local (y por lo tanto Estadísticas/Histórico) no se enteraba. Ahora se marca solo, sin depender de eso.
- Si ya estaba marcado con el nombre de una persona específica de antes, no se pisa con un genérico "Terminado" solo porque esta vez no se volvió a elegir responsable — se respeta lo que ya había.

## 1.13.3
- Cuando un paso ya existe (Parent recordado o elegido, Master encontrado en Oracle, o segmentos de serial ya configurados), el formulario de ese paso se reemplaza por una vista de "Paso correctamente realizado" con palomita verde — ya no se puede volver a crear por accidente. Aplica a los 3 pasos: MODELS_PARENTS, MODELS_MASTER, y MODELS_SERIALNUMBERSPECS (este último se revisa automáticamente en cuanto se sabe qué Master aplica).

## 1.13.2
- En cuanto se sabe el modelo padre (elegido, creado, o recordado de otra área), Paso 2 consulta directo en Oracle si ya existe un MODELS_MASTER con ese mismo número de parte y ese mismo modelo padre — en vez de confiar solo en la marca local, que podría estar desactualizada si el modelo se dio de alta desde otra computadora o directo en Oracle. Si lo encuentra, aparece de una vez como "Ya existe en iMES", sin tener que buscarlo ni crearlo de nuevo.

## 1.13.1
- Paso 2 (MODELS_MASTER) detecta cuando un modelo tiene número de parte de SMT y de BE a la vez, y avisa "Configurando SMT-BE": precarga el número de SMT como principal, el de BE en "Número de parte 2", y agrega "(SMT-BE)" al nombre del modelo — siguiendo la misma convención que ya se ve en iMES. Al guardar, marca el estatus en ambas áreas (SMT y BE) de un jalón, igual que el modelo padre. TLA sigue siendo siempre aparte.

## 1.13.0
- Los 3 pasos del asistente de Oracle (MODELS_PARENTS/MASTER/SERIALNUMBERSPECS) ya no están encadenados — los 3 se ven y se pueden trabajar al mismo tiempo, sin botones "Siguiente" ni tener que terminar uno para ver el próximo.
- El modelo padre (MODELS_PARENTS) ahora tiene memoria: como es del modelo completo, no de un área, en cuanto se elige o crea desde cualquier área (SMT/BE/TLA), se recuerda y se marca como hecho en las 3 áreas del modelo a la vez — ya no hay que repetirlo por cada área.
- Al volver a entrar a cualquier área de un modelo que ya tiene su modelo padre asignado, aparece directo como "Se usará: [nombre]" en vez de tener que buscarlo o crearlo de nuevo.

## 1.12.0
- El asistente de 3 pasos (MODELS_PARENTS → MODELS_MASTER → MODELS_SERIALNUMBERSPECS) ya no es una pantalla aparte a la que hay que navegar — ahora vive directamente dentro de "Configurar modelo", en el mismo lugar donde antes estaban los 4 desplegables de estatus. Ya no hay que salir de la pantalla para dar de alta el modelo en Oracle.
- Al cambiar de área (SMT/BE/TLA) dentro de "Configurar modelo", el asistente se reinicia solo en el Paso 1 para la nueva área.
- Para TLA, aparece un aviso: como el número de parte puede repetirse entre áreas, en el Paso 2 hay que fijarse en cuál resultado trae "TLA" en el nombre del modelo.
- "DataBase → Configurar un modelo en iMES" se queda como una herramienta aparte, para trabajar en Oracle sin un modelo específico de por medio (usa el mismo asistente, reorganizado como un control reutilizable).

## 1.11.0
- Cambio de fondo en "Configurar modelo": los 4 desplegables de estatus de BD iMES (MODELS_PARENTS/MASTER/SERIALNUMBERSPECS/Packing, donde se elegía a mano Pendiente/En Proceso/Terminado) desaparecieron. En su lugar hay un resumen de solo lectura (con los mismos colores de siempre) y un botón "Dar de alta en iMES (Oracle)".
- Ese botón abre "Configurar en iMES" ya con el modelo y número de parte precargados — al elegir "¿Quién está haciendo esto?" y completar cada paso (Parent, Master, SerialNumberSpecs) contra el entorno de Oracle activo, el estatus local se marca solo. Ya no hay que elegir un responsable aparte para "fingir" que se hizo — el insert real en Oracle ES la prueba.
- Histórico, Estadísticas, y los reportes PDF/Excel no cambiaron nada — siguen leyendo el mismo seguimiento local de siempre, que ahora se llena solo en vez de a mano.
- "Probar un serial", Plataforma iMES (Work Order/Ruta/Flujo/Línea/PartLineBomMaintenance), Estado del Documento, y el PDF adjunto siguen exactamente igual, sin tocarse.

## 1.10.1
- Corregido error de compilación (CS0176) en el Paso 3 de "Configurar en iMES" — la lista de etiquetas sugeridas (ClientPartNumber/Separator/etc.) estaba declarada como "static", pero el XAML la usa a través del ViewModel, y C# no permite mezclar eso. Se corrigió quitando "static".

## 1.10.0
- "Configurar en iMES", Paso 3: rediseñado el armado de MODELS_SERIALNUMBERSPECS para que sea visual — escribes un serial real de ejemplo, y cada carácter aparece como una casilla clickeable. Le das clic al último carácter del pedazo que quieres cerrar (el sistema ya sabe dónde empieza: justo después del segmento anterior), eliges qué es (con sugerencias como ClientPartNumber/Separator/Revision/BuildDate, pero puedes escribir cualquier otra) y si es dinámico o no. Posición y longitud se calculan solas, y el valor fijo se toma directo del serial que escribiste. Se puede deshacer el último segmento si algo quedó mal, y la tabla de abajo muestra lo confirmado para revisar antes de guardar. Sigue existiendo "Copiar los segmentos del modelo base" si el Master se basó en uno existente.

## 1.9.2
- Corregido: al hacer clic en "DataBase" en el menú, se quedaba mostrando "Altas de Modelos" en vez de navegar. Causa: había una lista aparte (Tag → página) en el código de navegación que había que mantener sincronizada a mano con la lista real del menú, y se me olvidó agregar "DataBase" ahí. Se corrigió de raíz quitando esa lista duplicada — ahora la navegación usa directamente la página que ya trae registrada cada sección del menú, así no puede volver a pasar con futuras secciones nuevas.

## 1.9.1
- "DataBase" ahora tiene 3 secciones directas para ver cada tabla por separado (MODELS_PARENTS, MODELS_MASTER, MODELS_SERIALNUMBERSPECS), en vez de una sola pantalla con pestañas para cambiar entre ellas.

## 1.9.0
- Nuevo apartado en el menú, "DataBase" (debajo de Estadísticas), con dos secciones:
  - "Configurar un modelo en iMES" — se movió aquí desde Ajustes (era el botón experimental).
  - "Historial de Modelos" — nuevo, de solo lectura: ver las 3 tablas de iMES (MODELS_PARENTS, MODELS_MASTER, MODELS_SERIALNUMBERSPECS) tal como están hoy, sin poder editar nada desde ahí. Hasta 500 filas por tabla.
- Ajustes ya no tiene el botón de configurar — se quedó solo con elegir el entorno y "Probar conexión".

## 1.8.1
- "Configurar en iMES", Paso 2 (MODELS_MASTER): agregados los campos que faltaban — Model Part Number y Model Part Number 2 (editables, antes solo se tomaba el número buscado sin poder ajustarlo), Modelo padre como desplegable (elige entre los parents ya registrados, en vez de solo heredar el del Paso 1 sin poder cambiarlo), y PCBs por panel (dejarlo vacío = NULL, no todos los modelos tienen un número fijo).

## 1.8.0
- Nueva pantalla experimental "Configurar en iMES" (Ajustes → Entorno de iMES): da de alta un modelo directo en las 3 tablas de Oracle, en 3 pasos.
  1. Modelo padre (MODELS_PARENTS) — busca nombres parecidos a lo que escribas (por si ya existe con otra ortografía) antes de forzar a crear uno nuevo.
  2. Modelo (MODELS_MASTER) — busca por los primeros 8 caracteres del número de parte; si coincide con uno existente, puedes basarte en él (copia sus datos como punto de partida editable) en vez de capturar todo desde cero.
  3. Segmentos del serial (MODELS_SERIALNUMBERSPECS) — si el paso 2 se basó en un modelo existente, ofrece copiar sus mismos segmentos; si no, se capturan a mano.
- Usa el entorno de Oracle activo que se eligió en Ajustes (Productivo/Pruebas/Docker).

## 1.7.0
- Primer paso hacia que Alejandría configure directamente en iMES (en vez de solo llevar el seguimiento de que ya se hizo a mano): entorno Docker con Oracle XE y las 3 tablas reales de iMES (MODELS_MASTER, MODELS_PARENTS, MODELS_SERIALNUMBERSPECS) — ver `docker/README.md`.
- appsettings.json ahora parametriza 3 entornos de Oracle (Productivo, Pruebas, Docker), seleccionables desde Ajustes con un botón "Probar conexión" que confirma que la conexión y las 3 tablas están accesibles. Todavía no hace nada más — es la base para la siguiente fase (lectura/escritura real).

## 1.6.0
- Respaldo automático de la base de datos compartida: al abrir Alejandría, si no hay un respaldo de hoy, se crea uno con fecha en una carpeta "Respaldos" junto a la base — se quedan los últimos 30 días. Corre en segundo plano, sin detener el arranque si la red falla.
- Configurar modelo ahora abre directo en la primera área que todavía tenga algo pendiente, en vez de siempre la principal — si todas las áreas ya están completas, abre en la principal como antes.
- Auditoría de diálogos: se revisaron todos los flujos que muestran un ContentDialog (Histórico, Ver detalles, Ajustes, Configurar modelo) para confirmar que ninguno más intenta abrir uno anidado — no se encontró ningún otro caso.

## 1.5.1
- Corregido: al guardar un modelo con número de parte duplicado, la app crasheaba ("Only a single ContentDialog can be open at any time") — WinUI3 no permite abrir un ContentDialog dentro de otro, y el aviso de duplicado intentaba abrirse desde dentro del propio formulario (que también es un ContentDialog). Se corrigió con un aviso InfoBar dentro del mismo formulario en vez de un diálogo aparte: si detecta un duplicado, avisa y pide un segundo clic en "Guardar" para confirmar.

## 1.5.0
- "Buscar en iMES" ahora se dispara solo al entrar a un área sin Work Order/Ruta/Línea capturados, en vez de tener que darle clic cada vez — ahorra un clic por área. Si no encuentra nada, no interrumpe con el diálogo de "reintentar/ingresar manual" (eso sigue reservado para cuando le das clic tú mismo), solo deja el aviso normal.
- Nuevo toggle en Ajustes, "Buscar en iMES automáticamente" (prendido por default) — para quien tenga una conexión lenta y prefiera decidir cuándo consultar.
- Configurar modelo: resumen de "qué falta" en la misma tarjeta del selector de área — dice de un vistazo qué campos siguen sin capturar (incluido el PDF adjunto), sin tener que recorrer toda la pantalla. Con ícono y color: palomita verde cuando ya está todo, aviso naranja cuando falta algo.
- Enter guarda la configuración, igual que el botón — no hace falta ir a buscarlo con el mouse.
- Corregido: "Ruta" y "Flujo" se mostraban como dos campos separados para todas las áreas, cuando en realidad son el mismo dato de iMES (ROUTE_NAME) — SMT le dice "Ruta" y BE/TLA le dicen "Flujo". Ahora solo se muestra (y se llena solo desde "Buscar en iMES") el que corresponde según el área, y "Flujo" dejó de ser un desplegable de estatus manual. Se corrigió también en los reportes (PDF/Excel), en "Ver detalles", y en "copiar configuración entre áreas" (traduce el valor si se copia entre un área SMT y una BE/TLA).
- Alta de modelo: si el número de parte ya existe en otro registro, avisa antes de guardar; si ya existe en otra área, autocompleta el Nombre de modelo.
- Configurar modelo: autoguardado 2 segundos después del último cambio (con un aviso discreto "Guardado"), sin dejar de tener el botón "Guardar configuración" para quien prefiera guardar de una vez.
- Al adjuntar un PDF nuevo en Altas de Modelos o en Histórico, si el modelo tenía el Estado del Documento en "Pendiente a correcciones", se ofrece (o en Histórico, se aplica directo) regresarlo a "Correcto".
- Caché en memoria de 5 minutos para las consultas a iMES por número de parte, para no repetir la misma llamada de red al entrar y salir de la misma área — el botón manual "Buscar en iMES" siempre limpia la caché primero, para nunca mostrar algo viejo si se pide una búsqueda explícita.
- Reducidas las asignaciones repetidas en el generador de reportes PDF (colores/fuente de la marca de agua reutilizados en vez de recrearse en cada llamada).

## 1.3.4
- El aviso de "documento pendiente de corrección" ahora sale en TODAS las páginas del modelo (no solo en la del área que quedó marcada así) y en la portada del reporte, si al menos una de sus áreas lo tiene.
- De paso se corrigió "Alcance del reporte"/"Propósito" en la portada, que tenían el mismo problema de fondo del aviso (texto largo que se desbordaba en vez de bajar de línea), aunque no se había notado porque esos textos hoy son cortos.

## 1.3.3
- Corregido: el aviso de "documento pendiente de corrección" en el reporte PDF salía cortado por ambos lados del recuadro. La causa: DrawString de PDFsharp con un rectángulo no ajusta el texto a varias líneas por sí solo como parecía sugerir la API — si el texto no cabe en una línea, lo deja centrado y se desborda en vez de bajar de renglón. Se corrigió partiendo el texto a mano, línea por línea, antes de dibujarlo.

## 1.3.2
- En el reporte PDF, la página de cualquier área marcada "Pendiente a correcciones" ahora lleva una marca de agua diagonal ("PENDIENTE DE CORRECCIÓN") y un aviso claro arriba: "Si no se recibe el documento corregido dentro de las próximas 24 horas, esta configuración se inhabilita."

## 1.3.1
- Se corrigió que el selector "Estado del Documento" se cortaba junto al de Área — estaban los dos en una sola fila horizontal sin poder encogerse, así que en ventanas angostas (o con el panel de navegación desplegado) el segundo se salía de la pantalla. Ahora cada uno tiene su propia fila, y el texto de la descripción se ajusta al espacio disponible en vez de competir por el ancho con el otro selector.

## 1.3.0
- Se quitó el apartado de configuración Base de Datos — Unity, en Configurar modelo, en los reportes (PDF y Excel), y en Estadísticas. Las columnas siguen en la base de datos por compatibilidad con lo ya guardado, solo que ya no se usan.
- En los reportes PDF, el nombre de quien completó una configuración ya no aparece — ahora se muestra como "Terminado" (mismo color verde de antes). El nombre se sigue guardando y se puede consultar desde Histórico.
- Histórico: nuevo botón "Ver detalles" — muestra de solo lectura la configuración ya guardada de cada área (Work Order/Ruta/Línea/Flujo/PartLineBomMaintenance y los 4 campos de BD iMES, incluyendo quién los completó), sin ningún control editable.
- Se quitó "Bloqueado por Documento Malo" de los desplegables de cada campo de BD. En su lugar, junto al selector de Área en Configurar modelo hay uno nuevo: "Estado del Documento" (Correcto / Pendiente a correcciones), independiente del avance de la configuración.
- Si un área queda "Pendiente a correcciones", no bloquea que el modelo llegue a Terminado — en Histórico se habilita ahí un botón "Adjuntar PDF" para subir la versión corregida, sin tener que pasar por "Actualizar".
- Estadísticas: el KPI de "Bloqueados" se renombró a "Pendientes a corrección de documento" y ahora cuenta según el nuevo Estado del Documento, no según el campo de BD que se quitó.

## 1.1.8
- El registro de errores ya no guarda rutas de red completas — las excepciones de .NET a veces las meten tal cual en su mensaje (ej. al no encontrar la carpeta de actualizaciones), y como el botón "Abrir carpeta" del registro está pensado para mandárselo a alguien si hace falta ayuda, esa ruta quedaba expuesta. Ahora se tapa automáticamente por "[ruta de red]".
- Lo mismo aplicaba a las alertas que se muestran en pantalla (ej. "Base de datos no disponible") — cualquier mensaje de error que incluya el detalle técnico de una excepción ahora también tapa la ruta de red, no solo el registro.

## 1.1.7
- Encontrada la causa real de que no saliera la ventana de "qué hay de nuevo": justo tras Activated, XamlRoot todavía no estaba listo (más notorio en el primer arranque tras actualizar) — ahora se espera hasta 2 segundos en vez de rendirse al primer intento.
- La versión ya solo se marca como "vista" después de mostrar el diálogo con éxito, no antes — así un intento fallido (como este) ya no se queda sin poder volver a mostrarse.

## 1.1.6
- Se agregó registro de diagnóstico (LogInfo) en el chequeo de "qué hay de nuevo" al abrir — ahora queda en el log qué versión detecta, cuál tenía marcada como vista, y por qué decide mostrar o no el diálogo, para poder ver la causa real si sigue sin salir.

## 1.1.5
- Sin cambios de código — versión de prueba para confirmar, con una actualización real (no una corrida de depuración), que la ventana de "qué hay de nuevo" sí aparece después del arreglo de la 1.1.4.

## 1.1.4
- Arreglada la ventana de "qué hay de nuevo" al actualizar: no salía cuando el LastSeenVersion guardado estaba vacío (pasaba en el primer arranque tras actualizar desde una versión anterior a que existiera este seguimiento) — ahora también se muestra en ese caso.
- Los errores al leer preferences.json ahora quedan en el registro de errores en vez de quedar en silencio.

## 1.1.3
- Ajustes: sección "Registro de errores" con botones "Ver registro" (últimos 5 días) y "Abrir carpeta" — el registro en sí ya existía desde antes (LogService), esto era la parte que faltaba para poder verlo sin salir de la app.

## 1.1.2
- Confirmado el flujo de "Ver notas de versión": botón en Ajustes con el historial completo, y aviso automático al abrir Alejandría por primera vez tras una actualización.

## 1.1.1
- No se puede abrir Alejandría dos veces en la misma computadora.

## 1.1.0
- Empaquetado y actualizaciones migradas a Velopack: instala por usuario, sin pedir permisos de administrador.
- Ajustes: sección "Actualizaciones" con la versión instalada y botón "Buscar actualización".
- "Buscar en iMES": si no encuentra nada, ahora pregunta si reintentar o capturar el Work Order/Ruta/Línea a mano.
- "Probar un serial": botón "Extraer del PDF adjunto" para sacar el serial directo del PDF de solicitud, sin copiarlo a mano — soporta varios seriales en un mismo PDF (FG/PCBA/Clock spring) y deja elegir cuál probar.
- Al confirmar un serial con iMES, desplegable para marcar de un jalón MODELS_PARENTS/MASTER/SERIALNUMBERSPECS/Packing con quién hizo la configuración.
- Campo nuevo "MODELS_SERIALNUMBERSPECS Packing" en BD iMES y BD Unity.
- Arreglos de tabla: la columna de acciones de Altas de Modelos ya no se corta al desplegar el panel de navegación.
- Un modelo ya no llega a Terminado sin el PDF adjunto, aunque toda su configuración esté completa.

## 1.0.0
- Primera versión: Altas de Modelos, Modelos, Configurar modelo (BD iMES/Unity + Plataforma iMES), Histórico, Estadísticas y reportes (Excel y PDF).
- Integración con la API de iMES: "Buscar en iMES" para traer Work Order/Ruta/Línea, "Probar un serial" para corroborar la configuración de base de datos.
- Botón "Actualizar" en Histórico para dar de alta un cambio de número de parte sin perder la configuración de las áreas que no cambiaron.
