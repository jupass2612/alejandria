-- Se corre conectado como SYSTEM (así es como gvenzl/oracle-xe corre los
-- scripts de esta carpeta la primera vez). El usuario IMES ya existe
-- para este punto (lo crea la imagen sola, vía la variable de entorno
-- APP_USER/APP_USER_PASSWORD del docker-compose.yml) — aquí solo se le
-- da lo que le falta: la secuencia que usan las 3 tablas para su ID.

ALTER SESSION SET CURRENT_SCHEMA = IMES;

CREATE SEQUENCE "IMES"."ISEQ$$_86854"
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;
