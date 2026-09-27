USE BioRed;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Empresa', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Sucursal', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Configuracion_Entrega_Sucursal', N'U') IS NULL
BEGIN
    THROW 51030, 'Falta una tabla requerida: Empresa, Sucursal o Configuracion_Entrega_Sucursal.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.Empresa
    GROUP BY email_empresa
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51031, 'Existen correos duplicados en Empresa.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.Sucursal
    GROUP BY email_sucursal
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51032, 'Existen correos duplicados en Sucursal.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.Sucursal
    WHERE
        (latitud IS NULL AND longitud IS NOT NULL) OR
        (latitud IS NOT NULL AND longitud IS NULL) OR
        latitud NOT BETWEEN -90 AND 90 OR
        longitud NOT BETWEEN -180 AND 180
)
BEGIN
    THROW 51033, 'Existen coordenadas incompletas o fuera de rango en Sucursal.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.Configuracion_Entrega_Sucursal
    WHERE radio_maximo_km <= 0 OR
          radio_maximo_km > 100 OR
          tarifa_base < 0 OR
          tarifa_por_km_extra < 0 OR
          tiempo_estimado_min < 1 OR
          tiempo_estimado_min > 1440
)
BEGIN
    THROW 51034, 'Existen configuraciones de entrega fuera de los rangos permitidos.', 1;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Empresa')
      AND name = N'UX_Empresa_Email'
)
BEGIN
    CREATE UNIQUE INDEX UX_Empresa_Email
        ON dbo.Empresa (email_empresa);

    PRINT 'Índice único de correo de empresa creado correctamente.';
END
ELSE
BEGIN
    PRINT 'El índice único de correo de empresa ya existe.';
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Sucursal')
      AND name = N'UX_Sucursal_Email'
)
BEGIN
    CREATE UNIQUE INDEX UX_Sucursal_Email
        ON dbo.Sucursal (email_sucursal);

    PRINT 'Índice único de correo de sucursal creado correctamente.';
END
ELSE
BEGIN
    PRINT 'El índice único de correo de sucursal ya existe.';
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Sucursal')
      AND name = N'CK_Sucursal_Coordenadas'
)
BEGIN
    ALTER TABLE dbo.Sucursal WITH CHECK
        ADD CONSTRAINT CK_Sucursal_Coordenadas
        CHECK
        (
            (latitud IS NULL AND longitud IS NULL) OR
            (
                latitud IS NOT NULL AND
                longitud IS NOT NULL AND
                latitud BETWEEN -90 AND 90 AND
                longitud BETWEEN -180 AND 180
            )
        );

    ALTER TABLE dbo.Sucursal
        CHECK CONSTRAINT CK_Sucursal_Coordenadas;

    PRINT 'Regla de coordenadas creada correctamente.';
END
ELSE
BEGIN
    PRINT 'La regla de coordenadas ya existe.';
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Configuracion_Entrega_Sucursal')
      AND name = N'CK_ConfiguracionEntrega_Rangos'
)
BEGIN
    ALTER TABLE dbo.Configuracion_Entrega_Sucursal WITH CHECK
        ADD CONSTRAINT CK_ConfiguracionEntrega_Rangos
        CHECK
        (
            radio_maximo_km > 0 AND
            radio_maximo_km <= 100 AND
            tarifa_base >= 0 AND
            tarifa_por_km_extra >= 0 AND
            tiempo_estimado_min BETWEEN 1 AND 1440
        );

    ALTER TABLE dbo.Configuracion_Entrega_Sucursal
        CHECK CONSTRAINT CK_ConfiguracionEntrega_Rangos;

    PRINT 'Regla de configuración de entrega creada correctamente.';
END
ELSE
BEGIN
    PRINT 'La regla de configuración de entrega ya existe.';
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Empresa')
      AND name = N'IX_Empresa_Estado'
)
BEGIN
    CREATE INDEX IX_Empresa_Estado
        ON dbo.Empresa (estado, nombre_empresa);

    PRINT 'Índice de consulta de empresas creado correctamente.';
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Sucursal')
      AND name = N'IX_Sucursal_Empresa_Estado'
)
BEGIN
    CREATE INDEX IX_Sucursal_Empresa_Estado
        ON dbo.Sucursal (cod_empresa, estado, nombre_sucursal);

    PRINT 'Índice de consulta de sucursales creado correctamente.';
END;
GO

SELECT
    (SELECT COUNT(*) FROM dbo.Empresa) AS empresas,
    (SELECT COUNT(*) FROM dbo.Empresa WHERE estado = 1) AS empresas_activas,
    (SELECT COUNT(*) FROM dbo.Sucursal) AS sucursales,
    (SELECT COUNT(*) FROM dbo.Sucursal WHERE estado = 1) AS sucursales_activas,
    (SELECT COUNT(*) FROM dbo.Configuracion_Entrega_Sucursal) AS configuraciones_entrega;
GO
