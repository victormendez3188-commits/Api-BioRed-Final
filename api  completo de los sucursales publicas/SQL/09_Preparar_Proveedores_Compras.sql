USE BioRed;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Proveedores', N'U') IS NULL OR
   OBJECT_ID(N'dbo.CompraEncabezado', N'U') IS NULL OR
   OBJECT_ID(N'dbo.CompraDetalle', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Estados', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Tipo_Pago', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Inventario', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Lotes', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Kardex', N'U') IS NULL
BEGIN
    THROW 51000, 'Falta una tabla requerida para proveedores, compras o inventario.', 1;
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Estados WHERE id_estado = 1)
BEGIN
    SET IDENTITY_INSERT dbo.Estados ON;
    INSERT INTO dbo.Estados
        (id_estado, nombre_estado, descripcion_estado, activo)
    VALUES
        (1, 'Registrada', 'Compra registrada y aplicada al inventario.', 1);
    SET IDENTITY_INSERT dbo.Estados OFF;

    PRINT 'Estado Registrada creado correctamente.';
END
ELSE
BEGIN
    UPDATE dbo.Estados SET activo = 1 WHERE id_estado = 1;
    PRINT 'El estado 1 ya existe y quedó activo.';
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Estados WHERE id_estado = 2)
BEGIN
    SET IDENTITY_INSERT dbo.Estados ON;
    INSERT INTO dbo.Estados
        (id_estado, nombre_estado, descripcion_estado, activo)
    VALUES
        (2, 'Anulada', 'Compra anulada y revertida del inventario.', 1);
    SET IDENTITY_INSERT dbo.Estados OFF;

    PRINT 'Estado Anulada creado correctamente.';
END
ELSE
BEGIN
    UPDATE dbo.Estados SET activo = 1 WHERE id_estado = 2;
    PRINT 'El estado 2 ya existe y quedó activo.';
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM dbo.Tipo_Pago WHERE cod_tipo_pago = 'PAGO-EFECTIVO'
)
BEGIN
    INSERT INTO dbo.Tipo_Pago
        (cod_tipo_pago, nombre_tipo_pago, estado)
    VALUES
        ('PAGO-EFECTIVO', 'Efectivo contra entrega', 1);

    PRINT 'Tipo de pago en efectivo creado correctamente.';
END;
GO

IF COL_LENGTH('dbo.CompraEncabezado', 'serie_documento') IS NULL
BEGIN
    ALTER TABLE dbo.CompraEncabezado
        ADD serie_documento varchar(30) NULL;
    PRINT 'Campo serie_documento creado correctamente.';
END;
GO

IF COL_LENGTH('dbo.CompraEncabezado', 'numero_documento') IS NULL
BEGIN
    ALTER TABLE dbo.CompraEncabezado
        ADD numero_documento varchar(50) NULL;
    PRINT 'Campo numero_documento creado correctamente.';
END;
GO

IF COL_LENGTH('dbo.CompraEncabezado', 'observaciones') IS NULL
BEGIN
    ALTER TABLE dbo.CompraEncabezado
        ADD observaciones varchar(300) NULL;
    PRINT 'Campo observaciones creado correctamente.';
END;
GO

UPDATE dbo.CompraEncabezado
SET numero_documento = CONCAT('MIGRADO-', id_compra)
WHERE numero_documento IS NULL
   OR LTRIM(RTRIM(numero_documento)) = '';
GO

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.CompraEncabezado')
      AND name = N'numero_documento'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.CompraEncabezado
        ALTER COLUMN numero_documento varchar(50) NOT NULL;
    PRINT 'numero_documento configurado como obligatorio.';
END;
GO

IF COL_LENGTH('dbo.CompraEncabezado', 'serie_documento_normalizada') IS NULL
BEGIN
    ALTER TABLE dbo.CompraEncabezado
        ADD serie_documento_normalizada AS
            (ISNULL([serie_documento], '')) PERSISTED;
    PRINT 'Serie normalizada creada correctamente.';
END;
GO

IF COL_LENGTH('dbo.CompraDetalle', 'numero_lote') IS NULL
BEGIN
    ALTER TABLE dbo.CompraDetalle
        ADD numero_lote varchar(50) NULL;
    PRINT 'Campo numero_lote creado correctamente.';
END;
GO

IF COL_LENGTH('dbo.CompraDetalle', 'fecha_vencimiento') IS NULL
BEGIN
    ALTER TABLE dbo.CompraDetalle
        ADD fecha_vencimiento date NULL;
    PRINT 'Campo fecha_vencimiento creado correctamente.';
END;
GO

IF EXISTS
(
    SELECT CUI_NIT
    FROM dbo.Proveedores
    GROUP BY CUI_NIT
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51001, 'Existen proveedores con CUI/NIT duplicado.', 1;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Proveedores')
      AND name = N'UX_Proveedores_CUI_NIT'
)
BEGIN
    CREATE UNIQUE INDEX UX_Proveedores_CUI_NIT
        ON dbo.Proveedores (CUI_NIT);
    PRINT 'Índice único de CUI/NIT creado correctamente.';
END;
GO

IF EXISTS
(
    SELECT email_proveedor
    FROM dbo.Proveedores
    GROUP BY email_proveedor
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51002, 'Existen proveedores con correo duplicado.', 1;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Proveedores')
      AND name = N'UX_Proveedores_Email'
)
BEGIN
    CREATE UNIQUE INDEX UX_Proveedores_Email
        ON dbo.Proveedores (email_proveedor);
    PRINT 'Índice único de correo de proveedor creado correctamente.';
END;
GO

IF EXISTS
(
    SELECT
        cod_proveedor,
        ISNULL(serie_documento, ''),
        numero_documento
    FROM dbo.CompraEncabezado
    GROUP BY
        cod_proveedor,
        ISNULL(serie_documento, ''),
        numero_documento
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51003, 'Existen documentos de compra duplicados para un proveedor.', 1;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CompraEncabezado')
      AND name = N'UX_Compra_Proveedor_Documento'
)
BEGIN
    CREATE UNIQUE INDEX UX_Compra_Proveedor_Documento
        ON dbo.CompraEncabezado
            (cod_proveedor, serie_documento_normalizada, numero_documento);
    PRINT 'Índice único de documento de compra creado correctamente.';
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Proveedores')
      AND name = N'IX_Proveedores_Empresa_Estado'
)
BEGIN
    CREATE INDEX IX_Proveedores_Empresa_Estado
        ON dbo.Proveedores (cod_empresa, estado);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CompraEncabezado')
      AND name = N'IX_Compras_Sucursal_Fecha'
)
BEGIN
    CREATE INDEX IX_Compras_Sucursal_Fecha
        ON dbo.CompraEncabezado (cod_sucursal, fecha_compra DESC);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CompraEncabezado')
      AND name = N'IX_Compras_Proveedor_Fecha'
)
BEGIN
    CREATE INDEX IX_Compras_Proveedor_Fecha
        ON dbo.CompraEncabezado (cod_proveedor, fecha_compra DESC);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CompraDetalle')
      AND name = N'IX_CompraDetalle_Compra_Producto'
)
BEGIN
    CREATE INDEX IX_CompraDetalle_Compra_Producto
        ON dbo.CompraDetalle (id_compra, cod_producto);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Proveedores')
      AND name = N'CK_Proveedores_CUI_NIT_NoVacio'
)
BEGIN
    ALTER TABLE dbo.Proveedores WITH CHECK
        ADD CONSTRAINT CK_Proveedores_CUI_NIT_NoVacio
        CHECK (LEN(LTRIM(RTRIM(CUI_NIT))) > 0);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Proveedores')
      AND name = N'CK_Proveedores_Email_NoVacio'
)
BEGIN
    ALTER TABLE dbo.Proveedores WITH CHECK
        ADD CONSTRAINT CK_Proveedores_Email_NoVacio
        CHECK (LEN(LTRIM(RTRIM(email_proveedor))) > 0);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.CompraEncabezado')
      AND name = N'CK_Compra_Total_Positivo'
)
BEGIN
    ALTER TABLE dbo.CompraEncabezado WITH CHECK
        ADD CONSTRAINT CK_Compra_Total_Positivo
        CHECK (total_compra > 0);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.CompraDetalle')
      AND name = N'CK_CompraDetalle_Cantidad_Positiva'
)
BEGIN
    ALTER TABLE dbo.CompraDetalle WITH CHECK
        ADD CONSTRAINT CK_CompraDetalle_Cantidad_Positiva
        CHECK (cantidad > 0);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.CompraDetalle')
      AND name = N'CK_CompraDetalle_Precios_Validos'
)
BEGIN
    ALTER TABLE dbo.CompraDetalle WITH CHECK
        ADD CONSTRAINT CK_CompraDetalle_Precios_Validos
        CHECK (precio_costo > 0 AND precio_venta >= precio_costo);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.CompraDetalle')
      AND name = N'CK_CompraDetalle_Lote_Completo'
)
BEGIN
    ALTER TABLE dbo.CompraDetalle WITH CHECK
        ADD CONSTRAINT CK_CompraDetalle_Lote_Completo
        CHECK
        (
            (numero_lote IS NULL AND fecha_vencimiento IS NULL)
            OR
            (numero_lote IS NOT NULL AND fecha_vencimiento IS NOT NULL)
        );
END;
GO

PRINT 'Preparación de proveedores y compras finalizada correctamente.';
GO

SELECT id_estado, nombre_estado, activo
FROM dbo.Estados
WHERE id_estado IN (1, 2)
ORDER BY id_estado;
GO

SELECT
    (SELECT COUNT(*) FROM dbo.Proveedores) AS proveedores,
    (SELECT COUNT(*) FROM dbo.CompraEncabezado) AS compras,
    (SELECT COUNT(*) FROM dbo.CompraDetalle) AS detalles_compra;
GO
