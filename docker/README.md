# Entorno Docker de pruebas — iMES (Oracle)

Esta carpeta levanta una base de datos Oracle local, con las 3 tablas
reales de iMES (`MODELS_MASTER`, `MODELS_PARENTS`,
`MODELS_SERIALNUMBERSPECS`), para poder desarrollar y probar la
integración directa de Alejandría con iMES **sin tocar la base de datos
real de producción ni la de Pruebas** mientras se construye esa parte.

## Requisitos

- Docker Desktop (o Docker Engine + Compose) instalado.
- ~2 GB libres de espacio en disco para la imagen + los datos.

## Levantarlo

```bash
cd docker
docker compose up -d
```

La primera vez tarda varios minutos — Oracle XE inicializa la base
desde cero y corre los scripts de `init/` en orden (crean el usuario
`IMES`, la secuencia que usan las 3 tablas, las tablas mismas, y unos
datos de ejemplo). Se puede seguir el progreso con:

```bash
docker compose logs -f oracle-imes-test
```

Ya está listo cuando el log dice `DATABASE IS READY TO USE!`.

## Conexión

| Dato | Valor |
|---|---|
| Host | `localhost` |
| Puerto | `1521` |
| Servicio (PDB) | `XEPDB1` |
| Usuario | `IMES` |
| Contraseña | `DevOnly_ChangeMe!` (ver `docker-compose.yml` — cámbiala ahí si quieres otra) |

Cadena de conexión típica (Oracle.ManagedDataAccess):
```
User Id=IMES;Password=DevOnly_ChangeMe!;Data Source=localhost:1521/XEPDB1;
```

Esto corresponde al entorno **"Docker"** en `appsettings.json` de
Alejandría (junto a **"Productivo"** y **"Pruebas"**, que apuntan a los
servidores Oracle reales de BCS) — ver `ImesOracle.Entornos` en ese
archivo y `EntornoActivo` para elegir cuál usa la app.

## Apagar

```bash
docker compose down       # apaga, conserva los datos en el volumen
docker compose down -v    # apaga y borra todo — la próxima vez vuelve a inicializar desde cero
```

## Re-ejecutar los scripts de `init/`

Los scripts de `init/` **solo se corren la primera vez** que se crea el
volumen de datos. Si cambias algo ahí y quieres que se vuelva a aplicar
desde cero:

```bash
docker compose down -v
docker compose up -d
```

## Siguiente paso

Este entorno es la base para que Alejandría pueda hacer la
configuración de modelos directamente en iMES (escribir en
`MODELS_MASTER`/`MODELS_PARENTS`/`MODELS_SERIALNUMBERSPECS`) en vez de
solo llevar el seguimiento de que alguien más ya lo hizo a mano — la
lógica de lectura/escritura real hacia estas tablas todavía no está
construida en Alejandría; este entorno es donde se prueba mientras se
construye.
