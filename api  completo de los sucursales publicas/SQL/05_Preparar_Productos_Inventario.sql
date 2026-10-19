USE BioRed;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Productos', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Inventario', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Lotes', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Kardex', N'U') IS NULL
BEGIN
    THROW 51000, 'Falta una de las tablas requeridas: Productos, Inventario, Lotes o Kardex.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.Inventario
    GROUP BY cod_sucursal, cod_producto
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51001, 'Inventario contiene productos duplicados por sucursal. Corrija los duplicados antes de crear el índice.', 1;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Inventario')
      AND name = N'UX_Inventario_Sucursal_Producto'
)
BEGIN
    CREATE UNIQUE INDEX UX_Inventario_Sucursal_Producto
        ON dbo.Inventario (cod_sucursal, cod_producto);

    PRINT 'Índice único de Inventario creado correctamente.';
END
ELSE
BEGIN
    PRINT 'El índice único de Inventario ya existe.';
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.Lotes
    GROUP BY cod_producto, numero_lote
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51002, 'Lotes contiene números de lote duplicados para un producto. Corrija los duplicados antes de crear el índice.', 1;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Lotes')
      AND name = N'UX_Lotes_Producto_Numero'
)
BEGIN
    CREATE UNIQUE INDEX UX_Lotes_Producto_Numero
        ON dbo.Lotes (cod_producto, numero_lote);

    PRINT 'Índice único de Lotes creado correctamente.';
END
ELSE
BEGIN
    PRINT 'El índice único de Lotes ya existe.';
END;
GO

SELECT
    'Productos' AS tabla,
    COUNT(*) AS registros
FROM dbo.Productos
UNION ALL
SELECT 'Inventario', COUNT(*) FROM dbo.Inventario
UNION ALL
SELECT 'Lotes', COUNT(*) FROM dbo.Lotes
UNION ALL
SELECT 'Kardex', COUNT(*) FROM dbo.Kardex;
GO
