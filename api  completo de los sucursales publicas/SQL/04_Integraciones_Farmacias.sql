USE BioRed;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Integraciones_Farmacia', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Integraciones_Farmacia
    (
        id_integracion INT IDENTITY(1,1) NOT NULL,
        cod_empresa VARCHAR(15) NOT NULL,
        nombre_farmacia VARCHAR(100) NOT NULL,
        url_base_api VARCHAR(500) NOT NULL,
        nombre_contacto VARCHAR(100) NOT NULL,
        correo_contacto VARCHAR(150) NOT NULL,
        estado_integracion VARCHAR(20) NOT NULL
            CONSTRAINT DF_Integraciones_Farmacia_Estado DEFAULT ('Pendiente'),
        conexion_validada BIT NOT NULL
            CONSTRAINT DF_Integraciones_Farmacia_Validada DEFAULT (0),
        fecha_solicitud DATETIME2 NOT NULL
            CONSTRAINT DF_Integraciones_Farmacia_Fecha DEFAULT (SYSUTCDATETIME()),
        fecha_ultima_validacion DATETIME2 NULL,
        fecha_aprobacion DATETIME2 NULL,
        actualizado_por VARCHAR(15) NULL,

        CONSTRAINT PK_Integraciones_Farmacia
            PRIMARY KEY (id_integracion),

        CONSTRAINT UQ_Integraciones_Farmacia_Empresa
            UNIQUE (cod_empresa),

        CONSTRAINT CK_Integraciones_Farmacia_Estado
            CHECK (estado_integracion IN ('Pendiente', 'Activa', 'Suspendida')),

        CONSTRAINT CK_Integraciones_Farmacia_UrlHttps
            CHECK (url_base_api LIKE 'https://%')
    );

    CREATE INDEX IX_Integraciones_Farmacia_Estado
        ON dbo.Integraciones_Farmacia (estado_integracion, conexion_validada);

    PRINT 'Tabla Integraciones_Farmacia creada correctamente.';
END
ELSE
BEGIN
    PRINT 'La tabla Integraciones_Farmacia ya existe. No se realizaron cambios.';
END;
GO

SELECT
    id_integracion,
    cod_empresa,
    nombre_farmacia,
    url_base_api,
    estado_integracion,
    conexion_validada,
    fecha_solicitud,
    fecha_ultima_validacion,
    fecha_aprobacion
FROM dbo.Integraciones_Farmacia
ORDER BY id_integracion DESC;
GO
