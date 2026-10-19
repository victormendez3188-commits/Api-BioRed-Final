USE BioRed;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Repartidores', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Cuentas_Acceso', N'U') IS NULL
BEGIN
    THROW 51020, 'Falta una tabla requerida: Repartidores o Cuentas_Acceso.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.Repartidores
    GROUP BY DPI_documento
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51021, 'Existen documentos duplicados en Repartidores.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.Repartidores
    WHERE placa_vehiculo IS NOT NULL
      AND placa_vehiculo <> ''
    GROUP BY placa_vehiculo
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51022, 'Existen placas duplicadas en Repartidores.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.Cuentas_Acceso
    GROUP BY correo
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51023, 'Existen correos duplicados en Cuentas_Acceso.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.Cuentas_Acceso
    GROUP BY tipo_usuario, referencia_id
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51024, 'Existen referencias de cuenta duplicadas.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.Repartidores
    WHERE estado_disponibilidad IS NOT NULL
      AND estado_disponibilidad NOT IN
          ('Disponible', 'Ocupado', 'Desconectado')
)
BEGIN
    THROW 51025, 'Existen estados de disponibilidad no válidos.', 1;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes AS i
    INNER JOIN sys.index_columns AS ic
        ON ic.object_id = i.object_id
       AND ic.index_id = i.index_id
    INNER JOIN sys.columns AS c
        ON c.object_id = ic.object_id
       AND c.column_id = ic.column_id
    WHERE i.object_id = OBJECT_ID(N'dbo.Repartidores')
      AND i.is_unique = 1
      AND c.name = N'DPI_documento'
      AND ic.key_ordinal = 1
)
BEGIN
    CREATE UNIQUE INDEX UX_Repartidores_Documento
        ON dbo.Repartidores (DPI_documento);

    PRINT 'Índice único de documentos creado correctamente.';
END
ELSE
BEGIN
    PRINT 'El índice único de documentos ya existe.';
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Repartidores')
      AND name = N'UX_Repartidores_Placa'
)
BEGIN
    CREATE UNIQUE INDEX UX_Repartidores_Placa
        ON dbo.Repartidores (placa_vehiculo)
        WHERE placa_vehiculo IS NOT NULL
          AND placa_vehiculo <> '';

    PRINT 'Índice único de placas creado correctamente.';
END
ELSE
BEGIN
    PRINT 'El índice único de placas ya existe.';
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Repartidores')
      AND name = N'CK_Repartidores_EstadoDisponibilidad'
)
BEGIN
    ALTER TABLE dbo.Repartidores WITH CHECK
        ADD CONSTRAINT CK_Repartidores_EstadoDisponibilidad
        CHECK
        (
            estado_disponibilidad IS NULL OR
            estado_disponibilidad IN
                ('Disponible', 'Ocupado', 'Desconectado')
        );

    ALTER TABLE dbo.Repartidores
        CHECK CONSTRAINT CK_Repartidores_EstadoDisponibilidad;

    PRINT 'Regla de disponibilidad creada correctamente.';
END
ELSE
BEGIN
    PRINT 'La regla de disponibilidad ya existe.';
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Repartidores')
      AND name = N'IX_Repartidores_Disponibilidad'
)
BEGIN
    CREATE INDEX IX_Repartidores_Disponibilidad
        ON dbo.Repartidores (estado, estado_disponibilidad);

    PRINT 'Índice de disponibilidad creado correctamente.';
END
ELSE
BEGIN
    PRINT 'El índice de disponibilidad ya existe.';
END;
GO

SELECT
    (SELECT COUNT(*) FROM dbo.Repartidores) AS repartidores,
    (SELECT COUNT(*) FROM dbo.Cuentas_Acceso
        WHERE tipo_usuario = 'Repartidor') AS cuentas_repartidor,
    (SELECT COUNT(*) FROM dbo.Repartidores
        WHERE estado = 1
          AND estado_disponibilidad = 'Disponible') AS disponibles;
GO
