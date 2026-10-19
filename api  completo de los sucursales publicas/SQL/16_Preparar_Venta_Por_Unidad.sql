/*
    BioRed - Venta por presentación o por unidad

    Script aditivo e idempotente. No elimina tablas ni datos.
    Para un producto configurado en ProductosVentaUnidad,
    Inventario.cantidad_actual representa unidades base (tabletas, cápsulas, etc.).
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Productos', N'U') IS NULL OR
       OBJECT_ID(N'dbo.Inventario', N'U') IS NULL OR
       OBJECT_ID(N'dbo.Pedido_Detalle', N'U') IS NULL OR
       OBJECT_ID(N'dbo.FacturaDetalle', N'U') IS NULL OR
       OBJECT_ID(N'dbo.DevolucionDetalle', N'U') IS NULL
    BEGIN
        THROW 51600, 'Faltan tablas requeridas para configurar la venta por unidad.', 1;
    END;

    IF OBJECT_ID(N'dbo.ProductosVentaUnidad', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ProductosVentaUnidad
        (
            cod_producto varchar(15) NOT NULL,
            permite_venta_unidad bit NOT NULL
                CONSTRAINT DF_ProductosVentaUnidad_Permite DEFAULT (0),
            unidades_por_presentacion int NOT NULL,
            precio_por_unidad decimal(18,2) NOT NULL,
            unidad_base varchar(30) NOT NULL
                CONSTRAINT DF_ProductosVentaUnidad_Unidad DEFAULT ('unidad'),
            activo bit NOT NULL
                CONSTRAINT DF_ProductosVentaUnidad_Activo DEFAULT (1),
            CONSTRAINT PK_ProductosVentaUnidad PRIMARY KEY (cod_producto),
            CONSTRAINT FK_ProductosVentaUnidad_Producto
                FOREIGN KEY (cod_producto)
                REFERENCES dbo.Productos(cod_producto),
            CONSTRAINT CK_ProductosVentaUnidad_Cantidad
                CHECK (unidades_por_presentacion > 0),
            CONSTRAINT CK_ProductosVentaUnidad_Precio
                CHECK (precio_por_unidad > 0)
        );
    END;

    /* PEDIDO_DETALLE */

    IF COL_LENGTH(N'dbo.Pedido_Detalle', N'vendido_por_unidad') IS NULL
        ALTER TABLE dbo.Pedido_Detalle ADD vendido_por_unidad bit NOT NULL
            CONSTRAINT DF_PedidoDetalle_VendidoUnidad DEFAULT (0) WITH VALUES;

    IF COL_LENGTH(N'dbo.Pedido_Detalle', N'unidad_base') IS NULL
        ALTER TABLE dbo.Pedido_Detalle ADD unidad_base varchar(30) NULL;

    IF COL_LENGTH(N'dbo.Pedido_Detalle', N'unidades_por_presentacion') IS NULL
        ALTER TABLE dbo.Pedido_Detalle ADD unidades_por_presentacion int NULL;

    IF COL_LENGTH(N'dbo.Pedido_Detalle', N'unidades_inventario') IS NULL
        ALTER TABLE dbo.Pedido_Detalle ADD unidades_inventario int NULL;

    EXEC sys.sp_executesql N'
        UPDATE dbo.Pedido_Detalle
        SET unidades_inventario = cantidad
        WHERE unidades_inventario IS NULL;';

    IF EXISTS
    (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.Pedido_Detalle')
          AND name = N'unidades_inventario'
          AND is_nullable = 1
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.Pedido_Detalle
            ALTER COLUMN unidades_inventario int NOT NULL;';

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.default_constraints AS dc
        INNER JOIN sys.columns AS c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Pedido_Detalle')
          AND c.name = N'unidades_inventario'
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.Pedido_Detalle
            ADD CONSTRAINT DF_PedidoDetalle_UnidadesInventario
            DEFAULT (1) FOR unidades_inventario;';

    /* FACTURADETALLE */

    IF COL_LENGTH(N'dbo.FacturaDetalle', N'vendido_por_unidad') IS NULL
        ALTER TABLE dbo.FacturaDetalle ADD vendido_por_unidad bit NOT NULL
            CONSTRAINT DF_FacturaDetalle_VendidoUnidad DEFAULT (0) WITH VALUES;

    IF COL_LENGTH(N'dbo.FacturaDetalle', N'unidad_base') IS NULL
        ALTER TABLE dbo.FacturaDetalle ADD unidad_base varchar(30) NULL;

    IF COL_LENGTH(N'dbo.FacturaDetalle', N'unidades_por_presentacion') IS NULL
        ALTER TABLE dbo.FacturaDetalle ADD unidades_por_presentacion int NULL;

    IF COL_LENGTH(N'dbo.FacturaDetalle', N'unidades_inventario') IS NULL
        ALTER TABLE dbo.FacturaDetalle ADD unidades_inventario int NULL;

    EXEC sys.sp_executesql N'
        UPDATE dbo.FacturaDetalle
        SET unidades_inventario = cantidad
        WHERE unidades_inventario IS NULL;';

    IF EXISTS
    (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.FacturaDetalle')
          AND name = N'unidades_inventario'
          AND is_nullable = 1
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.FacturaDetalle
            ALTER COLUMN unidades_inventario int NOT NULL;';

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.default_constraints AS dc
        INNER JOIN sys.columns AS c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.FacturaDetalle')
          AND c.name = N'unidades_inventario'
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.FacturaDetalle
            ADD CONSTRAINT DF_FacturaDetalle_UnidadesInventario
            DEFAULT (1) FOR unidades_inventario;';

    /* DEVOLUCIONDETALLE */

    IF COL_LENGTH(N'dbo.DevolucionDetalle', N'vendido_por_unidad') IS NULL
        ALTER TABLE dbo.DevolucionDetalle ADD vendido_por_unidad bit NOT NULL
            CONSTRAINT DF_DevolucionDetalle_VendidoUnidad DEFAULT (0) WITH VALUES;

    IF COL_LENGTH(N'dbo.DevolucionDetalle', N'unidad_base') IS NULL
        ALTER TABLE dbo.DevolucionDetalle ADD unidad_base varchar(30) NULL;

    IF COL_LENGTH(N'dbo.DevolucionDetalle', N'unidades_por_presentacion') IS NULL
        ALTER TABLE dbo.DevolucionDetalle ADD unidades_por_presentacion int NULL;

    IF COL_LENGTH(N'dbo.DevolucionDetalle', N'unidades_inventario') IS NULL
        ALTER TABLE dbo.DevolucionDetalle ADD unidades_inventario int NULL;

    EXEC sys.sp_executesql N'
        UPDATE dbo.DevolucionDetalle
        SET unidades_inventario = cantidad
        WHERE unidades_inventario IS NULL;';

    IF EXISTS
    (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.DevolucionDetalle')
          AND name = N'unidades_inventario'
          AND is_nullable = 1
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.DevolucionDetalle
            ALTER COLUMN unidades_inventario int NOT NULL;';

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.default_constraints AS dc
        INNER JOIN sys.columns AS c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.DevolucionDetalle')
          AND c.name = N'unidades_inventario'
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.DevolucionDetalle
            ADD CONSTRAINT DF_DevolucionDetalle_UnidadesInventario
            DEFAULT (1) FOR unidades_inventario;';

    /* RESTRICCIONES */

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Pedido_Detalle')
          AND name = N'CK_PedidoDetalle_UnidadesInventario'
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.Pedido_Detalle
            ADD CONSTRAINT CK_PedidoDetalle_UnidadesInventario
            CHECK
            (
                unidades_inventario > 0
                AND (unidades_por_presentacion IS NULL OR unidades_por_presentacion > 0)
            );';

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.FacturaDetalle')
          AND name = N'CK_FacturaDetalle_UnidadesInventario'
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.FacturaDetalle
            ADD CONSTRAINT CK_FacturaDetalle_UnidadesInventario
            CHECK
            (
                unidades_inventario > 0
                AND (unidades_por_presentacion IS NULL OR unidades_por_presentacion > 0)
            );';

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.DevolucionDetalle')
          AND name = N'CK_DevolucionDetalle_UnidadesInventario'
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.DevolucionDetalle
            ADD CONSTRAINT CK_DevolucionDetalle_UnidadesInventario
            CHECK
            (
                unidades_inventario > 0
                AND (unidades_por_presentacion IS NULL OR unidades_por_presentacion > 0)
            );';

    COMMIT TRANSACTION;

    SELECT
        N'OK' AS resultado,
        OBJECT_ID(N'dbo.ProductosVentaUnidad') AS configuracion_object_id;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
