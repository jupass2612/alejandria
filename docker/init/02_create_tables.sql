ALTER SESSION SET CURRENT_SCHEMA = IMES;

create table IMES.MODELS_MASTER
(
    ID                         NUMBER(38) default "IMES"."ISEQ$$_86854".nextval not null
        constraint PK_MASTER_MODELS
            primary key,
    MODELNAME                  VARCHAR2(50),
    MODELDESC                  VARCHAR2(50),
    PARENTMODELID              NUMBER,
    MODELPARTNUMBER            VARCHAR2(50),
    MODELQRPARTNUMBER          VARCHAR2(50),
    MODELPRODUCTID             VARCHAR2(50),
    PCBSPERPANEL               NUMBER,
    UICOLOR                    VARCHAR2(50),
    SIMULATEINTERLOCK          NUMBER,
    VALIDATEINTERLOCK          NUMBER,
    SERIALNUMBERBYMES          NUMBER,
    SERIALNUMBERCREATIONSP     VARCHAR2(50),
    ISACTIVE                   NUMBER,
    FORCESECONDARYSERIALNUMBER NUMBER,
    MODELPARTNUMBER2           VARCHAR2(50),
    FGPARTNUMBER               VARCHAR2(50),
    HANDLINGLOGIC              VARCHAR2(50)
)
/

create table IMES.MODELS_PARENTS
(
    ID                   NUMBER(38) default "IMES"."ISEQ$$_86854".nextval not null
        constraint PK_MODELS_PARENTS
            primary key,
    PARENTMODELNAME      VARCHAR2(50),
    PARTNUMBER           VARCHAR2(50),
    FAMILY               VARCHAR2(50),
    VALIDATEFLOW         NUMBER,
    VALIDATELASTOPRESULT NUMBER,
    FLOWFWDVALIDATION    NUMBER,
    ISACTIVE             NUMBER
)
/

create table IMES.MODELS_SERIALNUMBERSPECS
(
    ID                   NUMBER(38) default "IMES"."ISEQ$$_86854".nextval not null,
    MODELID              NUMBER,
    VALUEDESCRIPTION     VARCHAR2(50),
    STARTPOSITION        NUMBER,
    EXPECTEDCHARACTERLEN NUMBER,
    ISDYNAMICVALUE       NUMBER,
    FIXEDCHARACTERVALUE  VARCHAR2(50)
)
/
