USE BioRed;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/*
    Módulo 4: pagos, promociones y recetas.
    El script es idempotente y no elimina información existente.
*/

IF OBJECT_ID(N'dbo.Pedidos', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Productos', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Clientes', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Cuentas_Acceso', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Sucursal', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Empresa', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Promociones', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Tipo_Pago', N'U') IS NULL OR
   OBJECT_ID(N'dbo.CxCEncabezado', N'U') IS NULL OR
   OBJECT_ID(N'dbo.CxCDetalle', N'U') IS NULL OR
   OBJECT_ID(N'dbo.DevolucionEncabezado', N'U') IS NULL OR
   OBJECT_ID(N'dbo.DevolucionDetalle', N'U') IS NULL
BEGIN
    THROW 51200, 'Faltan tablas base. Ejecute primero los scripts 01 a 10.', 1;
END;
GO

IF COL_LENGTH('dbo.Productos', 'requiere_receta') IS NULL
BEGIN
    ALTER TABLE dbo.Productos
        ADD requiere_receta bit NOT NULL
            CONSTRAINT DF_Productos_RequiereReceta DEFAULT (0);
END;
GO

IF COL_LENGTH('dbo.DevolucionDetalle', 'monto_linea') IS NULL
BEGIN
    ALTER TABLE dbo.DevolucionDetalle
        ADD monto_linea decimal(18,2) NULL;
END;
GO

/*
    Se ejecuta en un lote posterior para que SQL Server reconozca monto_linea
    también durante la primera instalación del módulo.
*/
UPDATE dbo.DevolucionDetalle
SET monto_linea = ROUND(precio_venta * cantidad, 2)
WHERE monto_linea IS NULL;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.Tipo_Pago
    WHERE cod_tipo_pago = 'PAGO-PAYPAL'
)
BEGIN
    INSERT INTO dbo.Tipo_Pago
        (cod_tipo_pago, nombre_tipo_pago, estado)
    VALUES
        ('PAGO-PAYPAL', 'PayPal Sandbox', 1);
END
ELSE
BEGIN
    UPDATE dbo.Tipo_Pago
    SET nombre_tipo_pago = 'PayPal Sandbox', estado = 1
    WHERE cod_tipo_pago = 'PAGO-PAYPAL';
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.Tipo_Pago
    WHERE cod_tipo_pago = 'PAGO-EFECTIVO'
)
BEGIN
    INSERT INTO dbo.Tipo_Pago
        (cod_tipo_pago, nombre_tipo_pago, estado)
    VALUES
        ('PAGO-EFECTIVO', 'Efectivo contra entrega', 1);
END;
ELSE
BEGIN
    UPDATE dbo.Tipo_Pago
    SET nombre_tipo_pago = 'Efectivo contra entrega', estado = 1
    WHERE cod_tipo_pago = 'PAGO-EFECTIVO';
END;
GO

IF COL_LENGTH('dbo.Promociones', 'cod_sucursal') IS NULL
    ALTER TABLE dbo.Promociones ADD cod_sucursal varchar(15) NULL;
GO

IF COL_LENGTH('dbo.Promociones', 'descripcion') IS NULL
    ALTER TABLE dbo.Promociones ADD descripcion varchar(300) NULL;
GO

IF COL_LENGTH('dbo.Promociones', 'monto_minimo_pedido') IS NULL
    ALTER TABLE dbo.Promociones ADD monto_minimo_pedido decimal(18,2) NOT NULL
        CONSTRAINT DF_Promociones_MontoMinimo DEFAULT (0);
GO

IF COL_LENGTH('dbo.Promociones', 'monto_maximo_descuento') IS NULL
    ALTER TABLE dbo.Promociones ADD monto_maximo_descuento decimal(18,2) NULL;
GO

IF COL_LENGTH('dbo.Promociones', 'limite_usos_total') IS NULL
    ALTER TABLE dbo.Promociones ADD limite_usos_total int NULL;
GO

IF COL_LENGTH('dbo.Promociones', 'limite_usos_cliente') IS NULL
    ALTER TABLE dbo.Promociones ADD limite_usos_cliente int NOT NULL
        CONSTRAINT DF_Promociones_LimiteCliente DEFAULT (1);
GO

IF COL_LENGTH('dbo.Promociones', 'creado_por') IS NULL
    ALTER TABLE dbo.Promociones ADD creado_por varchar(15) NULL;
GO

IF COL_LENGTH('dbo.Promociones', 'creado_el_utc') IS NULL
    ALTER TABLE dbo.Promociones ADD creado_el_utc datetime2 NOT NULL
        CONSTRAINT DF_Promociones_Creado DEFAULT (sysutcdatetime());
GO

IF COL_LENGTH('dbo.Promociones', 'actualizado_por') IS NULL
    ALTER TABLE dbo.Promociones ADD actualizado_por varchar(15) NULL;
GO

IF COL_LENGTH('dbo.Promociones', 'actualizado_el_utc') IS NULL
    ALTER TABLE dbo.Promociones ADD actualizado_el_utc datetime2 NULL;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_Promociones_Sucursal'
)
    ALTER TABLE dbo.Promociones ADD CONSTRAINT FK_Promociones_Sucursal
        FOREIGN KEY (cod_sucursal) REFERENCES dbo.Sucursal(cod_sucursal);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_Promociones_UsuarioCreador'
)
    ALTER TABLE dbo.Promociones ADD CONSTRAINT FK_Promociones_UsuarioCreador
        FOREIGN KEY (creado_por) REFERENCES dbo.Usuarios(cod_usuario);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_Promociones_UsuarioActualiza'
)
    ALTER TABLE dbo.Promociones ADD CONSTRAINT FK_Promociones_UsuarioActualiza
        FOREIGN KEY (actualizado_por) REFERENCES dbo.Usuarios(cod_usuario);
GO

IF OBJECT_ID(N'dbo.PromocionProductos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PromocionProductos
    (
        cod_promocion varchar(15) NOT NULL,
        cod_producto varchar(15) NOT NULL,
        CONSTRAINT PK_PromocionProductos
            PRIMARY KEY (cod_promocion, cod_producto),
        CONSTRAINT FK_PromocionProductos_Promocion
            FOREIGN KEY (cod_promocion)
            REFERENCES dbo.Promociones(cod_promocion),
        CONSTRAINT FK_PromocionProductos_Producto
            FOREIGN KEY (cod_producto)
            REFERENCES dbo.Productos(cod_producto)
    );
END;
GO

IF OBJECT_ID(N'dbo.PromocionUsos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PromocionUsos
    (
        id_promocion_uso bigint IDENTITY(1,1) NOT NULL,
        cod_promocion varchar(15) NOT NULL,
        cod_cliente varchar(15) NOT NULL,
        id_pedido int NOT NULL,
        descuento_aplicado decimal(18,2) NOT NULL,
        usado_el_utc datetime2 NOT NULL
            CONSTRAINT DF_PromocionUsos_Fecha DEFAULT (sysutcdatetime()),
        CONSTRAINT PK_PromocionUsos PRIMARY KEY (id_promocion_uso),
        CONSTRAINT FK_PromocionUsos_Promocion
            FOREIGN KEY (cod_promocion)
            REFERENCES dbo.Promociones(cod_promocion),
        CONSTRAINT FK_PromocionUsos_Cliente
            FOREIGN KEY (cod_cliente)
            REFERENCES dbo.Clientes(cod_cliente),
        CONSTRAINT FK_PromocionUsos_Pedido
            FOREIGN KEY (id_pedido)
            REFERENCES dbo.Pedidos(id_pedido),
        CONSTRAINT UQ_PromocionUsos_Pedido UNIQUE (id_pedido),
        CONSTRAINT CK_PromocionUsos_Descuento CHECK (descuento_aplicado > 0)
    );

    CREATE INDEX IX_PromocionUsos_Promocion_Cliente
        ON dbo.PromocionUsos(cod_promocion, cod_cliente);
END;
GO

IF OBJECT_ID(N'dbo.RecetasCliente', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecetasCliente
    (
        id_receta bigint IDENTITY(1,1) NOT NULL,
        cod_cliente varchar(15) NOT NULL,
        numero_receta varchar(50) NULL,
        nombre_medico varchar(150) NOT NULL,
        numero_colegiado varchar(50) NOT NULL,
        fecha_emision date NOT NULL,
        fecha_vencimiento date NOT NULL,
        estado varchar(10) NOT NULL,
        observaciones varchar(300) NULL,
        nombre_archivo varchar(150) NOT NULL,
        tipo_contenido varchar(50) NOT NULL,
        tamano_archivo bigint NOT NULL,
        documento varbinary(max) NOT NULL,
        hash_documento_sha256 varchar(64) NOT NULL,
        revisado_por varchar(15) NULL,
        revisado_el_utc datetime2 NULL,
        observaciones_revision varchar(300) NULL,
        id_pedido int NULL,
        creado_el_utc datetime2 NOT NULL
            CONSTRAINT DF_RecetasCliente_Creado DEFAULT (sysutcdatetime()),
        usado_el_utc datetime2 NULL,
        CONSTRAINT PK_RecetasCliente PRIMARY KEY (id_receta),
        CONSTRAINT FK_RecetasCliente_Cliente
            FOREIGN KEY (cod_cliente) REFERENCES dbo.Clientes(cod_cliente),
        CONSTRAINT FK_RecetasCliente_UsuarioRevisor
            FOREIGN KEY (revisado_por) REFERENCES dbo.Usuarios(cod_usuario),
        CONSTRAINT FK_RecetasCliente_Pedido
            FOREIGN KEY (id_pedido) REFERENCES dbo.Pedidos(id_pedido),
        CONSTRAINT CK_RecetasCliente_Estado
            CHECK (estado IN ('PENDING', 'APPROVED', 'REJECTED', 'USED', 'EXPIRED')),
        CONSTRAINT CK_RecetasCliente_Fechas
            CHECK (fecha_vencimiento >= fecha_emision),
        CONSTRAINT CK_RecetasCliente_Archivo
            CHECK
            (
                tamano_archivo > 0 AND tamano_archivo <= 5242880 AND
                tipo_contenido IN ('application/pdf', 'image/jpeg', 'image/png')
            )
    );

    CREATE UNIQUE INDEX UX_RecetasCliente_Pedido
        ON dbo.RecetasCliente(id_pedido)
        WHERE id_pedido IS NOT NULL;

    CREATE INDEX IX_RecetasCliente_Cliente_Estado
        ON dbo.RecetasCliente(cod_cliente, estado, fecha_vencimiento);
END;
GO

IF OBJECT_ID(N'dbo.RecetaProductos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecetaProductos
    (
        id_receta_producto bigint IDENTITY(1,1) NOT NULL,
        id_receta bigint NOT NULL,
        cod_producto varchar(15) NOT NULL,
        cantidad_autorizada int NOT NULL,
        CONSTRAINT PK_RecetaProductos PRIMARY KEY (id_receta_producto),
        CONSTRAINT FK_RecetaProductos_Receta
            FOREIGN KEY (id_receta)
            REFERENCES dbo.RecetasCliente(id_receta),
        CONSTRAINT FK_RecetaProductos_Producto
            FOREIGN KEY (cod_producto)
            REFERENCES dbo.Productos(cod_producto),
        CONSTRAINT UQ_RecetaProductos_Receta_Producto
            UNIQUE (id_receta, cod_producto),
        CONSTRAINT CK_RecetaProductos_Cantidad CHECK (cantidad_autorizada > 0)
    );
END;
GO

IF OBJECT_ID(N'dbo.PagosPedido', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PagosPedido
    (
        id_pago bigint IDENTITY(1,1) NOT NULL,
        id_pedido int NOT NULL,
        id_cxc int NULL,
        cod_cliente varchar(15) NOT NULL,
        cod_tipo_pago varchar(15) NOT NULL,
        proveedor_pago varchar(20) NOT NULL,
        estado varchar(20) NOT NULL,
        monto_local decimal(18,2) NOT NULL,
        moneda_local char(3) NOT NULL,
        monto_proveedor decimal(18,2) NOT NULL,
        moneda_proveedor char(3) NOT NULL,
        tipo_cambio decimal(18,6) NOT NULL,
        orden_proveedor_id varchar(100) NULL,
        captura_proveedor_id varchar(100) NULL,
        url_aprobacion varchar(1000) NULL,
        clave_idempotencia varchar(100) NOT NULL,
        creado_por_cuenta int NOT NULL,
        creado_el_utc datetime2 NOT NULL
            CONSTRAINT DF_PagosPedido_Creado DEFAULT (sysutcdatetime()),
        completado_el_utc datetime2 NULL,
        referencia_externa varchar(100) NULL,
        observaciones varchar(300) NULL,
        detalle_fallo varchar(500) NULL,
        CONSTRAINT PK_PagosPedido PRIMARY KEY (id_pago),
        CONSTRAINT FK_PagosPedido_Pedido
            FOREIGN KEY (id_pedido) REFERENCES dbo.Pedidos(id_pedido),
        CONSTRAINT FK_PagosPedido_CxC
            FOREIGN KEY (id_cxc) REFERENCES dbo.CxCEncabezado(id_cxc),
        CONSTRAINT FK_PagosPedido_Cliente
            FOREIGN KEY (cod_cliente) REFERENCES dbo.Clientes(cod_cliente),
        CONSTRAINT FK_PagosPedido_CuentaCreadora
            FOREIGN KEY (creado_por_cuenta) REFERENCES dbo.Cuentas_Acceso(id_cuenta),
        CONSTRAINT FK_PagosPedido_TipoPago
            FOREIGN KEY (cod_tipo_pago) REFERENCES dbo.Tipo_Pago(cod_tipo_pago),
        CONSTRAINT CK_PagosPedido_Proveedor
            CHECK (proveedor_pago IN ('PAYPAL', 'CASH')),
        CONSTRAINT CK_PagosPedido_Estado
            CHECK
            (
                estado IN
                ('CREATED', 'APPROVED', 'COMPLETED', 'FAILED',
                 'PARTIALLY_REFUNDED', 'REFUNDED')
            ),
        CONSTRAINT CK_PagosPedido_Montos
            CHECK
            (
                monto_local > 0 AND monto_proveedor > 0 AND tipo_cambio > 0 AND
                LEN(moneda_local) = 3 AND LEN(moneda_proveedor) = 3
            )
    );

    CREATE UNIQUE INDEX UX_PagosPedido_Idempotencia
        ON dbo.PagosPedido(clave_idempotencia);

    CREATE UNIQUE INDEX UX_PagosPedido_OrdenProveedor
        ON dbo.PagosPedido(orden_proveedor_id)
        WHERE orden_proveedor_id IS NOT NULL;

    CREATE UNIQUE INDEX UX_PagosPedido_CapturaProveedor
        ON dbo.PagosPedido(captura_proveedor_id)
        WHERE captura_proveedor_id IS NOT NULL;

    CREATE UNIQUE INDEX UX_PagosPedido_Completado
        ON dbo.PagosPedido(id_pedido)
        WHERE estado = 'COMPLETED';

    CREATE INDEX IX_PagosPedido_Pedido_Fecha
        ON dbo.PagosPedido(id_pedido, creado_el_utc DESC);
END;
GO

IF OBJECT_ID(N'dbo.ReembolsosPedido', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReembolsosPedido
    (
        id_reembolso bigint IDENTITY(1,1) NOT NULL,
        id_devolucion int NOT NULL,
        id_pago bigint NULL,
        proveedor_pago varchar(20) NOT NULL,
        estado varchar(20) NOT NULL,
        monto_local decimal(18,2) NOT NULL,
        moneda_local char(3) NOT NULL,
        monto_proveedor decimal(18,2) NOT NULL,
        moneda_proveedor char(3) NOT NULL,
        tipo_cambio decimal(18,6) NOT NULL,
        reembolso_proveedor_id varchar(100) NULL,
        clave_idempotencia varchar(100) NOT NULL,
        procesado_por varchar(15) NOT NULL,
        creado_el_utc datetime2 NOT NULL
            CONSTRAINT DF_ReembolsosPedido_Creado DEFAULT (sysutcdatetime()),
        completado_el_utc datetime2 NULL,
        referencia_externa varchar(100) NULL,
        observaciones varchar(300) NULL,
        detalle_fallo varchar(500) NULL,
        CONSTRAINT PK_ReembolsosPedido PRIMARY KEY (id_reembolso),
        CONSTRAINT FK_ReembolsosPedido_Devolucion
            FOREIGN KEY (id_devolucion)
            REFERENCES dbo.DevolucionEncabezado(id_devolucion),
        CONSTRAINT FK_ReembolsosPedido_Pago
            FOREIGN KEY (id_pago) REFERENCES dbo.PagosPedido(id_pago),
        CONSTRAINT FK_ReembolsosPedido_Usuario
            FOREIGN KEY (procesado_por) REFERENCES dbo.Usuarios(cod_usuario),
        CONSTRAINT UQ_ReembolsosPedido_Devolucion UNIQUE (id_devolucion),
        CONSTRAINT UQ_ReembolsosPedido_Idempotencia UNIQUE (clave_idempotencia),
        CONSTRAINT CK_ReembolsosPedido_Proveedor
            CHECK (proveedor_pago IN ('PAYPAL', 'CASH')),
        CONSTRAINT CK_ReembolsosPedido_Estado
            CHECK (estado IN ('PENDING', 'COMPLETED', 'FAILED')),
        CONSTRAINT CK_ReembolsosPedido_Montos
            CHECK
            (
                monto_local > 0 AND monto_proveedor > 0 AND tipo_cambio > 0 AND
                LEN(moneda_local) = 3 AND LEN(moneda_proveedor) = 3
            )
    );

    CREATE UNIQUE INDEX UX_ReembolsosPedido_Proveedor
        ON dbo.ReembolsosPedido(reembolso_proveedor_id)
        WHERE reembolso_proveedor_id IS NOT NULL;
END;
GO

PRINT 'Preparación de pagos, promociones y recetas finalizada correctamente.';
GO

SELECT cod_tipo_pago, nombre_tipo_pago, estado
FROM dbo.Tipo_Pago
WHERE cod_tipo_pago IN ('PAGO-EFECTIVO', 'PAGO-PAYPAL')
ORDER BY cod_tipo_pago;
GO

SELECT
    (SELECT COUNT(*) FROM dbo.Promociones) AS promociones,
    (SELECT COUNT(*) FROM dbo.RecetasCliente) AS recetas,
    (SELECT COUNT(*) FROM dbo.PagosPedido) AS pagos,
    (SELECT COUNT(*) FROM dbo.ReembolsosPedido) AS reembolsos;
GO
