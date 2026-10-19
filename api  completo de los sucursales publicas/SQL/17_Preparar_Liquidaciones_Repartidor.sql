/*
    BioRed - Liquidaciones del repartidor

    Script aditivo e idempotente. No elimina ni recrea tablas existentes.
    Ejecútelo una sola vez en Azure SQL antes de habilitar DriverSettlements.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Cuentas_Pago_Repartidor', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Cuentas_Pago_Repartidor
        (
            cod_repartidor       varchar(15)  NOT NULL,
            proveedor            varchar(20)  NOT NULL
                CONSTRAINT DF_CuentasPagoRepartidor_Proveedor DEFAULT ('PAYPAL'),
            correo_paypal        varchar(254) NOT NULL,
            activa               bit          NOT NULL
                CONSTRAINT DF_CuentasPagoRepartidor_Activa DEFAULT (1),
            creado_el_utc        datetime2(7) NOT NULL
                CONSTRAINT DF_CuentasPagoRepartidor_Creado DEFAULT (SYSUTCDATETIME()),
            actualizado_el_utc   datetime2(7) NOT NULL
                CONSTRAINT DF_CuentasPagoRepartidor_Actualizado DEFAULT (SYSUTCDATETIME()),

            CONSTRAINT PK_Cuentas_Pago_Repartidor
                PRIMARY KEY (cod_repartidor),
            CONSTRAINT FK_CuentasPagoRepartidor_Repartidores
                FOREIGN KEY (cod_repartidor)
                REFERENCES dbo.Repartidores(cod_repartidor),
            CONSTRAINT CK_CuentasPagoRepartidor_Proveedor
                CHECK (proveedor = 'PAYPAL'),
            CONSTRAINT CK_CuentasPagoRepartidor_Correo
                CHECK (correo_paypal LIKE '%_@_%._%')
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Cuentas_Pago_Repartidor')
          AND name = N'UX_CuentasPagoRepartidor_Correo'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_CuentasPagoRepartidor_Correo
            ON dbo.Cuentas_Pago_Repartidor(correo_paypal);
    END;

    IF OBJECT_ID(N'dbo.Liquidaciones_Repartidor', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Liquidaciones_Repartidor
        (
            id_liquidacion                 bigint IDENTITY(1,1) NOT NULL,
            id_pedido                      int            NOT NULL,
            cod_repartidor                 varchar(15)    NOT NULL,
            id_pago                        bigint         NULL,
            metodo_origen                  varchar(20)    NOT NULL,
            estado                         varchar(30)    NOT NULL,
            monto_repartidor_local         decimal(18,2)  NOT NULL,
            moneda_local                   varchar(3)     NOT NULL,
            monto_proveedor                decimal(18,2)  NULL,
            moneda_proveedor               varchar(3)     NULL,
            tipo_cambio                    decimal(18,6)  NULL,
            monto_efectivo_cobrado         decimal(18,2)  NOT NULL
                CONSTRAINT DF_LiquidacionesRepartidor_EfectivoCobrado DEFAULT (0),
            monto_efectivo_a_remitir       decimal(18,2)  NOT NULL
                CONSTRAINT DF_LiquidacionesRepartidor_EfectivoRemitir DEFAULT (0),
            correo_paypal_destino          varchar(254)   NULL,
            lote_proveedor_id              varchar(100)   NULL,
            item_proveedor_id              varchar(100)   NULL,
            transaccion_proveedor_id       varchar(100)   NULL,
            clave_idempotencia             varchar(100)   NOT NULL,
            intentos                       int            NOT NULL
                CONSTRAINT DF_LiquidacionesRepartidor_Intentos DEFAULT (0),
            creado_el_utc                  datetime2(7)   NOT NULL
                CONSTRAINT DF_LiquidacionesRepartidor_Creado DEFAULT (SYSUTCDATETIME()),
            ultimo_intento_utc             datetime2(7)   NULL,
            completado_el_utc              datetime2(7)   NULL,
            efectivo_conciliado_el_utc     datetime2(7)   NULL,
            efectivo_conciliado_por        varchar(15)    NULL,
            referencia_conciliacion        varchar(100)   NULL,
            observaciones                  varchar(300)   NULL,
            detalle_fallo                  varchar(500)   NULL,

            CONSTRAINT PK_Liquidaciones_Repartidor
                PRIMARY KEY (id_liquidacion),
            CONSTRAINT FK_LiquidacionesRepartidor_Pedidos
                FOREIGN KEY (id_pedido)
                REFERENCES dbo.Pedidos(id_pedido),
            CONSTRAINT FK_LiquidacionesRepartidor_Repartidores
                FOREIGN KEY (cod_repartidor)
                REFERENCES dbo.Repartidores(cod_repartidor),
            CONSTRAINT FK_LiquidacionesRepartidor_PagosPedido
                FOREIGN KEY (id_pago)
                REFERENCES dbo.PagosPedido(id_pago),
            CONSTRAINT CK_LiquidacionesRepartidor_Metodo
                CHECK (metodo_origen IN ('PAYPAL', 'CASH')),
            CONSTRAINT CK_LiquidacionesRepartidor_Estado
                CHECK (estado IN
                (
                    'PENDIENTE_CONFIGURACION',
                    'PENDIENTE',
                    'PROCESANDO',
                    'COMPLETADO',
                    'ERROR',
                    'EFECTIVO_COBRADO',
                    'EFECTIVO_CONCILIADO'
                )),
            CONSTRAINT CK_LiquidacionesRepartidor_Montos
                CHECK
                (
                    monto_repartidor_local >= 0
                    AND (monto_proveedor IS NULL OR monto_proveedor >= 0)
                    AND monto_efectivo_cobrado >= 0
                    AND monto_efectivo_a_remitir >= 0
                ),
            CONSTRAINT CK_LiquidacionesRepartidor_Intentos
                CHECK (intentos >= 0)
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Liquidaciones_Repartidor')
          AND name = N'UX_LiquidacionesRepartidor_Pedido'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_LiquidacionesRepartidor_Pedido
            ON dbo.Liquidaciones_Repartidor(id_pedido);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Liquidaciones_Repartidor')
          AND name = N'UX_LiquidacionesRepartidor_Idempotencia'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_LiquidacionesRepartidor_Idempotencia
            ON dbo.Liquidaciones_Repartidor(clave_idempotencia);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Liquidaciones_Repartidor')
          AND name = N'IX_LiquidacionesRepartidor_Repartidor_Fecha'
    )
    BEGIN
        CREATE INDEX IX_LiquidacionesRepartidor_Repartidor_Fecha
            ON dbo.Liquidaciones_Repartidor(cod_repartidor, creado_el_utc DESC);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Liquidaciones_Repartidor')
          AND name = N'IX_LiquidacionesRepartidor_Estado_Fecha'
    )
    BEGIN
        CREATE INDEX IX_LiquidacionesRepartidor_Estado_Fecha
            ON dbo.Liquidaciones_Repartidor(estado, creado_el_utc DESC);
    END;

    COMMIT TRANSACTION;

    SELECT
        N'OK' AS resultado,
        OBJECT_ID(N'dbo.Cuentas_Pago_Repartidor') AS cuentas_pago_object_id,
        OBJECT_ID(N'dbo.Liquidaciones_Repartidor') AS liquidaciones_object_id;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
