USE BioRed;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.FacturaEncabezado', N'U') IS NULL OR
   OBJECT_ID(N'dbo.FacturaDetalle', N'U') IS NULL OR
   OBJECT_ID(N'dbo.DevolucionEncabezado', N'U') IS NULL OR
   OBJECT_ID(N'dbo.DevolucionDetalle', N'U') IS NULL OR
   OBJECT_ID(N'dbo.CxCEncabezado', N'U') IS NULL OR
   OBJECT_ID(N'dbo.CxCDetalle', N'U') IS NULL OR
   OBJECT_ID(N'dbo.CxPEncabezado', N'U') IS NULL OR
   OBJECT_ID(N'dbo.CxPDetalle', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Pedidos', N'U') IS NULL OR
   OBJECT_ID(N'dbo.CompraEncabezado', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Estados', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Tipo_Pago', N'U') IS NULL
BEGIN
    THROW 51100, 'Falta una tabla requerida para facturación o contabilidad.', 1;
END;
GO

DECLARE @Estados TABLE
(
    id_estado int NOT NULL,
    nombre_estado varchar(50) NOT NULL,
    descripcion_estado varchar(200) NOT NULL
);

INSERT INTO @Estados (id_estado, nombre_estado, descripcion_estado)
VALUES
    (10, 'Factura emitida', 'Factura generada para un pedido.'),
    (20, 'Devolucion aplicada', 'Devolucion aplicada al inventario y a la cuenta por cobrar.'),
    (30, 'CxC pendiente', 'Cuenta por cobrar con saldo pendiente.'),
    (31, 'CxC liquidada', 'Cuenta por cobrar sin saldo pendiente.'),
    (32, 'CxC parcial', 'Cuenta por cobrar con pagos o creditos parciales.'),
    (40, 'CxP pendiente', 'Cuenta por pagar con saldo pendiente.'),
    (41, 'CxP liquidada', 'Cuenta por pagar sin saldo pendiente.'),
    (42, 'CxP parcial', 'Cuenta por pagar con pagos parciales.');

SET IDENTITY_INSERT dbo.Estados ON;

INSERT INTO dbo.Estados
    (id_estado, nombre_estado, descripcion_estado, activo)
SELECT
    source.id_estado,
    source.nombre_estado,
    source.descripcion_estado,
    1
FROM @Estados AS source
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.Estados AS target
    WHERE target.id_estado = source.id_estado
);

SET IDENTITY_INSERT dbo.Estados OFF;

UPDATE target
SET
    target.nombre_estado = source.nombre_estado,
    target.descripcion_estado = source.descripcion_estado,
    target.activo = 1
FROM dbo.Estados AS target
INNER JOIN @Estados AS source
    ON source.id_estado = target.id_estado;
GO

/* Factura vinculada de forma única con un pedido. */
IF COL_LENGTH('dbo.FacturaEncabezado', 'id_pedido') IS NULL
    ALTER TABLE dbo.FacturaEncabezado ADD id_pedido int NULL;
GO

IF COL_LENGTH('dbo.FacturaEncabezado', 'serie_documento') IS NULL
    ALTER TABLE dbo.FacturaEncabezado ADD serie_documento varchar(30) NULL;
GO

IF COL_LENGTH('dbo.FacturaEncabezado', 'numero_documento') IS NULL
    ALTER TABLE dbo.FacturaEncabezado ADD numero_documento varchar(50) NULL;
GO

IF COL_LENGTH('dbo.FacturaEncabezado', 'subtotal') IS NULL
    ALTER TABLE dbo.FacturaEncabezado ADD subtotal decimal(18,2) NULL;
GO

IF COL_LENGTH('dbo.FacturaEncabezado', 'costo_envio') IS NULL
    ALTER TABLE dbo.FacturaEncabezado ADD costo_envio decimal(18,2) NULL;
GO

IF COL_LENGTH('dbo.FacturaEncabezado', 'descuento') IS NULL
    ALTER TABLE dbo.FacturaEncabezado ADD descuento decimal(18,2) NULL;
GO

IF COL_LENGTH('dbo.FacturaEncabezado', 'observaciones') IS NULL
    ALTER TABLE dbo.FacturaEncabezado ADD observaciones varchar(300) NULL;
GO

UPDATE dbo.FacturaEncabezado
SET
    numero_documento = COALESCE(NULLIF(LTRIM(RTRIM(numero_documento)), ''), CONCAT('MIGRADO-', id_factura)),
    subtotal = COALESCE(subtotal, total_factura),
    costo_envio = COALESCE(costo_envio, 0),
    descuento = COALESCE(descuento, 0)
WHERE numero_documento IS NULL OR
      LTRIM(RTRIM(numero_documento)) = '' OR
      subtotal IS NULL OR
      costo_envio IS NULL OR
      descuento IS NULL;
GO

IF EXISTS (SELECT 1 FROM dbo.FacturaEncabezado WHERE id_pedido IS NULL)
    THROW 51101, 'Existen facturas antiguas sin pedido; vinculelas antes de continuar.', 1;
GO

ALTER TABLE dbo.FacturaEncabezado ALTER COLUMN id_pedido int NOT NULL;
ALTER TABLE dbo.FacturaEncabezado ALTER COLUMN numero_documento varchar(50) NOT NULL;
ALTER TABLE dbo.FacturaEncabezado ALTER COLUMN subtotal decimal(18,2) NOT NULL;
ALTER TABLE dbo.FacturaEncabezado ALTER COLUMN costo_envio decimal(18,2) NOT NULL;
ALTER TABLE dbo.FacturaEncabezado ALTER COLUMN descuento decimal(18,2) NOT NULL;
GO

IF COL_LENGTH('dbo.FacturaEncabezado', 'serie_documento_normalizada') IS NULL
BEGIN
    ALTER TABLE dbo.FacturaEncabezado
        ADD serie_documento_normalizada AS (ISNULL([serie_documento], '')) PERSISTED;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.FacturaEncabezado')
      AND referenced_object_id = OBJECT_ID(N'dbo.Pedidos')
)
BEGIN
    ALTER TABLE dbo.FacturaEncabezado WITH CHECK
        ADD CONSTRAINT FK_FacturaEncabezado_Pedidos
        FOREIGN KEY (id_pedido) REFERENCES dbo.Pedidos(id_pedido);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.FacturaEncabezado')
      AND name = N'UX_Factura_Pedido'
)
    CREATE UNIQUE INDEX UX_Factura_Pedido ON dbo.FacturaEncabezado(id_pedido);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.FacturaEncabezado')
      AND name = N'UX_Factura_Sucursal_Documento'
)
BEGIN
    CREATE UNIQUE INDEX UX_Factura_Sucursal_Documento
        ON dbo.FacturaEncabezado
            (cod_sucursal, serie_documento_normalizada, numero_documento);
END;
GO

/* Devoluciones vinculadas con la factura original. */
IF COL_LENGTH('dbo.DevolucionEncabezado', 'id_factura') IS NULL
    ALTER TABLE dbo.DevolucionEncabezado ADD id_factura int NULL;
GO

IF COL_LENGTH('dbo.DevolucionEncabezado', 'numero_devolucion') IS NULL
    ALTER TABLE dbo.DevolucionEncabezado ADD numero_devolucion varchar(30) NULL;
GO

IF COL_LENGTH('dbo.DevolucionEncabezado', 'motivo') IS NULL
    ALTER TABLE dbo.DevolucionEncabezado ADD motivo varchar(300) NULL;
GO

IF COL_LENGTH('dbo.DevolucionEncabezado', 'credito_aplicado') IS NULL
    ALTER TABLE dbo.DevolucionEncabezado ADD credito_aplicado decimal(18,2) NULL;
GO

IF COL_LENGTH('dbo.DevolucionEncabezado', 'reembolso_pendiente') IS NULL
    ALTER TABLE dbo.DevolucionEncabezado ADD reembolso_pendiente decimal(18,2) NULL;
GO

UPDATE dbo.DevolucionEncabezado
SET
    numero_devolucion = COALESCE(NULLIF(LTRIM(RTRIM(numero_devolucion)), ''), CONCAT('MIGRADO-', id_devolucion)),
    motivo = COALESCE(NULLIF(LTRIM(RTRIM(motivo)), ''), 'Registro migrado'),
    credito_aplicado = COALESCE(credito_aplicado, total_devolucion),
    reembolso_pendiente = COALESCE(reembolso_pendiente, 0)
WHERE numero_devolucion IS NULL OR
      LTRIM(RTRIM(numero_devolucion)) = '' OR
      motivo IS NULL OR
      LTRIM(RTRIM(motivo)) = '' OR
      credito_aplicado IS NULL OR
      reembolso_pendiente IS NULL;
GO

IF EXISTS (SELECT 1 FROM dbo.DevolucionEncabezado WHERE id_factura IS NULL)
    THROW 51102, 'Existen devoluciones antiguas sin factura; vinculelas antes de continuar.', 1;
GO

ALTER TABLE dbo.DevolucionEncabezado ALTER COLUMN id_factura int NOT NULL;
ALTER TABLE dbo.DevolucionEncabezado ALTER COLUMN numero_devolucion varchar(30) NOT NULL;
ALTER TABLE dbo.DevolucionEncabezado ALTER COLUMN motivo varchar(300) NOT NULL;
ALTER TABLE dbo.DevolucionEncabezado ALTER COLUMN credito_aplicado decimal(18,2) NOT NULL;
ALTER TABLE dbo.DevolucionEncabezado ALTER COLUMN reembolso_pendiente decimal(18,2) NOT NULL;
GO

IF COL_LENGTH('dbo.DevolucionDetalle', 'numero_lote') IS NULL
    ALTER TABLE dbo.DevolucionDetalle ADD numero_lote varchar(50) NULL;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.DevolucionEncabezado')
      AND referenced_object_id = OBJECT_ID(N'dbo.FacturaEncabezado')
)
BEGIN
    ALTER TABLE dbo.DevolucionEncabezado WITH CHECK
        ADD CONSTRAINT FK_DevolucionEncabezado_Factura
        FOREIGN KEY (id_factura) REFERENCES dbo.FacturaEncabezado(id_factura);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.DevolucionEncabezado')
      AND name = N'UX_Devolucion_Numero'
)
    CREATE UNIQUE INDEX UX_Devolucion_Numero ON dbo.DevolucionEncabezado(numero_devolucion);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.DevolucionEncabezado')
      AND name = N'IX_Devolucion_Factura_Fecha'
)
    CREATE INDEX IX_Devolucion_Factura_Fecha ON dbo.DevolucionEncabezado(id_factura, fecha_devolucion DESC);
GO

/* Cuenta por cobrar vinculada de forma única con la factura. */
IF COL_LENGTH('dbo.CxCEncabezado', 'id_factura') IS NULL
    ALTER TABLE dbo.CxCEncabezado ADD id_factura int NULL;
GO

IF COL_LENGTH('dbo.CxCEncabezado', 'saldo_cxc') IS NULL
    ALTER TABLE dbo.CxCEncabezado ADD saldo_cxc decimal(18,2) NULL;
GO

IF COL_LENGTH('dbo.CxCEncabezado', 'fecha_vencimiento') IS NULL
    ALTER TABLE dbo.CxCEncabezado ADD fecha_vencimiento date NULL;
GO

UPDATE dbo.CxCEncabezado
SET
    saldo_cxc = COALESCE(saldo_cxc, total_cxc),
    fecha_vencimiento = COALESCE(fecha_vencimiento, CONVERT(date, fecha_cxc))
WHERE saldo_cxc IS NULL OR fecha_vencimiento IS NULL;
GO

IF EXISTS (SELECT 1 FROM dbo.CxCEncabezado WHERE id_factura IS NULL)
    THROW 51103, 'Existen cuentas por cobrar sin factura; vinculelas antes de continuar.', 1;
GO

ALTER TABLE dbo.CxCEncabezado ALTER COLUMN id_factura int NOT NULL;
ALTER TABLE dbo.CxCEncabezado ALTER COLUMN saldo_cxc decimal(18,2) NOT NULL;
ALTER TABLE dbo.CxCEncabezado ALTER COLUMN fecha_vencimiento date NOT NULL;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.CxCEncabezado')
      AND referenced_object_id = OBJECT_ID(N'dbo.FacturaEncabezado')
)
BEGIN
    ALTER TABLE dbo.CxCEncabezado WITH CHECK
        ADD CONSTRAINT FK_CxCEncabezado_Factura
        FOREIGN KEY (id_factura) REFERENCES dbo.FacturaEncabezado(id_factura);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CxCEncabezado')
      AND name = N'UX_CxC_Factura'
)
    CREATE UNIQUE INDEX UX_CxC_Factura ON dbo.CxCEncabezado(id_factura);
GO

IF COL_LENGTH('dbo.CxCDetalle', 'tipo_movimiento') IS NULL
    ALTER TABLE dbo.CxCDetalle ADD tipo_movimiento varchar(20) NULL;
IF COL_LENGTH('dbo.CxCDetalle', 'saldo_anterior') IS NULL
    ALTER TABLE dbo.CxCDetalle ADD saldo_anterior decimal(18,2) NULL;
IF COL_LENGTH('dbo.CxCDetalle', 'saldo_nuevo') IS NULL
    ALTER TABLE dbo.CxCDetalle ADD saldo_nuevo decimal(18,2) NULL;
IF COL_LENGTH('dbo.CxCDetalle', 'cod_usuario') IS NULL
    ALTER TABLE dbo.CxCDetalle ADD cod_usuario varchar(15) NULL;
IF COL_LENGTH('dbo.CxCDetalle', 'cod_tipo_pago') IS NULL
    ALTER TABLE dbo.CxCDetalle ADD cod_tipo_pago varchar(15) NULL;
IF COL_LENGTH('dbo.CxCDetalle', 'observaciones') IS NULL
    ALTER TABLE dbo.CxCDetalle ADD observaciones varchar(300) NULL;
IF COL_LENGTH('dbo.CxCDetalle', 'referencia_externa') IS NULL
    ALTER TABLE dbo.CxCDetalle ADD referencia_externa varchar(100) NULL;
GO

UPDATE dbo.CxCDetalle
SET
    tipo_movimiento = COALESCE(tipo_movimiento, 'Pago'),
    saldo_anterior = COALESCE(saldo_anterior, monto_pagado),
    saldo_nuevo = COALESCE(saldo_nuevo, 0)
WHERE tipo_movimiento IS NULL OR saldo_anterior IS NULL OR saldo_nuevo IS NULL;
GO

IF EXISTS (SELECT 1 FROM dbo.CxCDetalle WHERE cod_usuario IS NULL)
    THROW 51104, 'Existen movimientos CxC antiguos sin usuario; completelos antes de continuar.', 1;
GO

ALTER TABLE dbo.CxCDetalle ALTER COLUMN tipo_movimiento varchar(20) NOT NULL;
ALTER TABLE dbo.CxCDetalle ALTER COLUMN saldo_anterior decimal(18,2) NOT NULL;
ALTER TABLE dbo.CxCDetalle ALTER COLUMN saldo_nuevo decimal(18,2) NOT NULL;
ALTER TABLE dbo.CxCDetalle ALTER COLUMN cod_usuario varchar(15) NOT NULL;
GO

/* Cuenta por pagar vinculada con CompraEncabezado, no con FacturaEncabezado. */
IF COL_LENGTH('dbo.CxPEncabezado', 'id_compra') IS NULL
    ALTER TABLE dbo.CxPEncabezado ADD id_compra int NULL;
GO

IF COL_LENGTH('dbo.CxPEncabezado', 'saldo_cxp') IS NULL
    ALTER TABLE dbo.CxPEncabezado ADD saldo_cxp decimal(18,2) NULL;
GO

IF COL_LENGTH('dbo.CxPEncabezado', 'fecha_vencimiento') IS NULL
    ALTER TABLE dbo.CxPEncabezado ADD fecha_vencimiento date NULL;
GO

UPDATE dbo.CxPEncabezado
SET
    saldo_cxp = COALESCE(saldo_cxp, total_cxp),
    fecha_vencimiento = COALESCE(fecha_vencimiento, CONVERT(date, fecha_cxp))
WHERE saldo_cxp IS NULL OR fecha_vencimiento IS NULL;
GO

IF EXISTS (SELECT 1 FROM dbo.CxPEncabezado WHERE id_compra IS NULL)
    THROW 51105, 'Existen cuentas por pagar sin compra; vinculelas antes de continuar.', 1;
GO

ALTER TABLE dbo.CxPEncabezado ALTER COLUMN id_compra int NOT NULL;
ALTER TABLE dbo.CxPEncabezado ALTER COLUMN saldo_cxp decimal(18,2) NOT NULL;
ALTER TABLE dbo.CxPEncabezado ALTER COLUMN fecha_vencimiento date NOT NULL;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.CxPEncabezado')
      AND referenced_object_id = OBJECT_ID(N'dbo.CompraEncabezado')
)
BEGIN
    ALTER TABLE dbo.CxPEncabezado WITH CHECK
        ADD CONSTRAINT FK_CxPEncabezado_Compra
        FOREIGN KEY (id_compra) REFERENCES dbo.CompraEncabezado(id_compra);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CxPEncabezado')
      AND name = N'UX_CxP_Compra'
)
    CREATE UNIQUE INDEX UX_CxP_Compra ON dbo.CxPEncabezado(id_compra);
GO

DECLARE @FkCxPFactura sysname;

SELECT TOP (1) @FkCxPFactura = fk.name
FROM sys.foreign_keys AS fk
INNER JOIN sys.foreign_key_columns AS fkc
    ON fkc.constraint_object_id = fk.object_id
INNER JOIN sys.columns AS column_parent
    ON column_parent.object_id = fk.parent_object_id
   AND column_parent.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.CxPDetalle')
  AND column_parent.name = N'id_factura';

IF @FkCxPFactura IS NOT NULL
BEGIN
    DECLARE @DropFkSql nvarchar(max);

    SET @DropFkSql =
        N'ALTER TABLE dbo.CxPDetalle DROP CONSTRAINT ' +
        QUOTENAME(@FkCxPFactura) +
        N';';

    EXEC sys.sp_executesql @DropFkSql;
END;
GO

IF COL_LENGTH('dbo.CxPDetalle', 'id_factura') IS NOT NULL
    ALTER TABLE dbo.CxPDetalle DROP COLUMN id_factura;
GO

IF COL_LENGTH('dbo.CxPDetalle', 'tipo_movimiento') IS NULL
    ALTER TABLE dbo.CxPDetalle ADD tipo_movimiento varchar(20) NULL;
IF COL_LENGTH('dbo.CxPDetalle', 'saldo_anterior') IS NULL
    ALTER TABLE dbo.CxPDetalle ADD saldo_anterior decimal(18,2) NULL;
IF COL_LENGTH('dbo.CxPDetalle', 'saldo_nuevo') IS NULL
    ALTER TABLE dbo.CxPDetalle ADD saldo_nuevo decimal(18,2) NULL;
IF COL_LENGTH('dbo.CxPDetalle', 'cod_usuario') IS NULL
    ALTER TABLE dbo.CxPDetalle ADD cod_usuario varchar(15) NULL;
IF COL_LENGTH('dbo.CxPDetalle', 'cod_tipo_pago') IS NULL
    ALTER TABLE dbo.CxPDetalle ADD cod_tipo_pago varchar(15) NULL;
IF COL_LENGTH('dbo.CxPDetalle', 'observaciones') IS NULL
    ALTER TABLE dbo.CxPDetalle ADD observaciones varchar(300) NULL;
IF COL_LENGTH('dbo.CxPDetalle', 'referencia_externa') IS NULL
    ALTER TABLE dbo.CxPDetalle ADD referencia_externa varchar(100) NULL;
GO

UPDATE dbo.CxPDetalle
SET
    tipo_movimiento = COALESCE(tipo_movimiento, 'Pago'),
    saldo_anterior = COALESCE(saldo_anterior, monto_pagado),
    saldo_nuevo = COALESCE(saldo_nuevo, 0)
WHERE tipo_movimiento IS NULL OR saldo_anterior IS NULL OR saldo_nuevo IS NULL;
GO

IF EXISTS (SELECT 1 FROM dbo.CxPDetalle WHERE cod_usuario IS NULL)
    THROW 51106, 'Existen movimientos CxP antiguos sin usuario; completelos antes de continuar.', 1;
GO

ALTER TABLE dbo.CxPDetalle ALTER COLUMN tipo_movimiento varchar(20) NOT NULL;
ALTER TABLE dbo.CxPDetalle ALTER COLUMN saldo_anterior decimal(18,2) NOT NULL;
ALTER TABLE dbo.CxPDetalle ALTER COLUMN saldo_nuevo decimal(18,2) NOT NULL;
ALTER TABLE dbo.CxPDetalle ALTER COLUMN cod_usuario varchar(15) NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CxCDetalle_Usuario')
    ALTER TABLE dbo.CxCDetalle WITH CHECK
        ADD CONSTRAINT FK_CxCDetalle_Usuario
        FOREIGN KEY (cod_usuario) REFERENCES dbo.Usuarios(cod_usuario);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CxCDetalle_TipoPago')
    ALTER TABLE dbo.CxCDetalle WITH CHECK
        ADD CONSTRAINT FK_CxCDetalle_TipoPago
        FOREIGN KEY (cod_tipo_pago) REFERENCES dbo.Tipo_Pago(cod_tipo_pago);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CxPDetalle_Usuario')
    ALTER TABLE dbo.CxPDetalle WITH CHECK
        ADD CONSTRAINT FK_CxPDetalle_Usuario
        FOREIGN KEY (cod_usuario) REFERENCES dbo.Usuarios(cod_usuario);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CxPDetalle_TipoPago')
    ALTER TABLE dbo.CxPDetalle WITH CHECK
        ADD CONSTRAINT FK_CxPDetalle_TipoPago
        FOREIGN KEY (cod_tipo_pago) REFERENCES dbo.Tipo_Pago(cod_tipo_pago);
GO

/* Restricciones monetarias y de integridad. */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Factura_Total_Valido')
    ALTER TABLE dbo.FacturaEncabezado WITH CHECK
        ADD CONSTRAINT CK_Factura_Total_Valido
        CHECK
        (
            total_factura > 0 AND
            subtotal >= 0 AND
            costo_envio >= 0 AND
            descuento >= 0 AND
            total_factura = subtotal + costo_envio - descuento AND
            LEN(LTRIM(RTRIM(numero_documento))) > 0
        );
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_FacturaDetalle_Valido')
    ALTER TABLE dbo.FacturaDetalle WITH CHECK
        ADD CONSTRAINT CK_FacturaDetalle_Valido
        CHECK (cantidad > 0 AND precio_costo >= 0 AND precio_venta > 0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Devolucion_Montos_Validos')
    ALTER TABLE dbo.DevolucionEncabezado WITH CHECK
        ADD CONSTRAINT CK_Devolucion_Montos_Validos
        CHECK
        (
            total_devolucion > 0 AND
            credito_aplicado >= 0 AND
            reembolso_pendiente >= 0 AND
            credito_aplicado + reembolso_pendiente = total_devolucion
        );
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DevolucionDetalle_Valido')
    ALTER TABLE dbo.DevolucionDetalle WITH CHECK
        ADD CONSTRAINT CK_DevolucionDetalle_Valido
        CHECK (cantidad > 0 AND precio_costo >= 0 AND precio_venta > 0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CxC_Saldo_Valido')
    ALTER TABLE dbo.CxCEncabezado WITH CHECK
        ADD CONSTRAINT CK_CxC_Saldo_Valido
        CHECK (total_cxc > 0 AND saldo_cxc >= 0 AND saldo_cxc <= total_cxc);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CxP_Saldo_Valido')
    ALTER TABLE dbo.CxPEncabezado WITH CHECK
        ADD CONSTRAINT CK_CxP_Saldo_Valido
        CHECK (total_cxp > 0 AND saldo_cxp >= 0 AND saldo_cxp <= total_cxp);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CxCDetalle_Monto_Valido')
    ALTER TABLE dbo.CxCDetalle WITH CHECK
        ADD CONSTRAINT CK_CxCDetalle_Monto_Valido
        CHECK
        (
            monto_pagado > 0 AND
            saldo_anterior >= 0 AND
            saldo_nuevo >= 0 AND
            saldo_nuevo = saldo_anterior - monto_pagado AND
            tipo_movimiento IN ('Pago', 'NotaCredito')
        );
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CxPDetalle_Monto_Valido')
    ALTER TABLE dbo.CxPDetalle WITH CHECK
        ADD CONSTRAINT CK_CxPDetalle_Monto_Valido
        CHECK
        (
            monto_pagado > 0 AND
            saldo_anterior >= 0 AND
            saldo_nuevo >= 0 AND
            saldo_nuevo = saldo_anterior - monto_pagado AND
            tipo_movimiento = 'Pago'
        );
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.FacturaEncabezado')
      AND name = N'IX_Factura_Sucursal_Fecha'
)
    CREATE INDEX IX_Factura_Sucursal_Fecha
        ON dbo.FacturaEncabezado(cod_sucursal, fecha_factura DESC);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CxCEncabezado')
      AND name = N'IX_CxC_Sucursal_Estado_Vencimiento'
)
    CREATE INDEX IX_CxC_Sucursal_Estado_Vencimiento
        ON dbo.CxCEncabezado(cod_sucursal, id_estado, fecha_vencimiento);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CxPEncabezado')
      AND name = N'IX_CxP_Sucursal_Estado_Vencimiento'
)
    CREATE INDEX IX_CxP_Sucursal_Estado_Vencimiento
        ON dbo.CxPEncabezado(cod_sucursal, id_estado, fecha_vencimiento);
GO

IF COL_LENGTH('dbo.CxPDetalle', 'id_factura') IS NOT NULL
    THROW 51107, 'No fue posible retirar la relación incorrecta CxPDetalle.id_factura.', 1;

PRINT 'Preparación de facturación, devoluciones, CxC y CxP finalizada correctamente.';
GO

SELECT id_estado, nombre_estado, activo
FROM dbo.Estados
WHERE id_estado IN (10, 20, 30, 31, 32, 40, 41, 42)
ORDER BY id_estado;
GO

SELECT
    (SELECT COUNT(*) FROM dbo.FacturaEncabezado) AS facturas,
    (SELECT COUNT(*) FROM dbo.DevolucionEncabezado) AS devoluciones,
    (SELECT COUNT(*) FROM dbo.CxCEncabezado) AS cuentas_cobrar,
    (SELECT COUNT(*) FROM dbo.CxPEncabezado) AS cuentas_pagar;
GO
