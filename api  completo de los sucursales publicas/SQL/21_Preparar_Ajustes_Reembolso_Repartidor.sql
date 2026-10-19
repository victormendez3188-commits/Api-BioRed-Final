/*
    BioRed - Reembolso de envío y recuperación de liquidación del repartidor

*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.ReembolsosPedido', N'U') IS NULL OR
       OBJECT_ID(N'dbo.Liquidaciones_Repartidor', N'U') IS NULL
    BEGIN
        THROW 52100, 'Faltan ReembolsosPedido o Liquidaciones_Repartidor. Ejecute primero los scripts de pagos y liquidaciones.', 1;
    END;

    IF COL_LENGTH(N'dbo.ReembolsosPedido', N'monto_entrega_reembolsado') IS NULL
    BEGIN
        ALTER TABLE dbo.ReembolsosPedido
            ADD monto_entrega_reembolsado decimal(18,2) NOT NULL
                CONSTRAINT DF_ReembolsosPedido_MontoEntregaReembolsado
                DEFAULT (0) WITH VALUES;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.ReembolsosPedido')
          AND name = N'CK_ReembolsosPedido_MontoEntregaReembolsado'
    )
    BEGIN
        /*
           SQL Server compila el lote antes de ejecutar ALTER TABLE. Por ello,
           esta validación se ejecuta de forma dinámica: así puede referirse a
           la columna recién agregada incluso cuando el script corre por primera vez.
        */
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.ReembolsosPedido
                ADD CONSTRAINT CK_ReembolsosPedido_MontoEntregaReembolsado
                CHECK
                (
                    monto_entrega_reembolsado >= 0
                    AND monto_entrega_reembolsado <= monto_local
                );';
    END;

    /*
       Un pago puede tener varias devoluciones de productos, pero el costo de
       envío solo se puede reembolsar una vez. El índice evita duplicados por
       reintentos o solicitudes concurrentes.
    */
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.ReembolsosPedido')
          AND name = N'UX_ReembolsosPedido_EntregaPorPago'
    )
    BEGIN
        EXEC sys.sp_executesql N'
            CREATE UNIQUE INDEX UX_ReembolsosPedido_EntregaPorPago
                ON dbo.ReembolsosPedido(id_pago)
                WHERE monto_entrega_reembolsado > 0
                  AND id_pago IS NOT NULL;';
    END;

    /*
       Se agrega el estado de una liquidación PayPal que nunca llegó a enviarse
       porque el envío fue reembolsado. No se modifica ningún monto histórico.
    */
    DECLARE @constraint_estado_liquidacion sysname;
    DECLARE @sql_eliminar_restriccion nvarchar(max);
    SELECT TOP (1) @constraint_estado_liquidacion = cc.name
    FROM sys.check_constraints AS cc
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Liquidaciones_Repartidor')
      AND cc.name = N'CK_LiquidacionesRepartidor_Estado';

    IF @constraint_estado_liquidacion IS NOT NULL
    BEGIN
        SET @sql_eliminar_restriccion =
            N'ALTER TABLE dbo.Liquidaciones_Repartidor DROP CONSTRAINT ' +
            QUOTENAME(@constraint_estado_liquidacion) + N';';

        EXEC sys.sp_executesql @sql_eliminar_restriccion;
    END;

    ALTER TABLE dbo.Liquidaciones_Repartidor
        ADD CONSTRAINT CK_LiquidacionesRepartidor_Estado
        CHECK (estado IN
        (
            'PENDIENTE_CONFIGURACION',
            'PENDIENTE',
            'PROCESANDO',
            'COMPLETADO',
            'ERROR',
            'EFECTIVO_COBRADO',
            'EFECTIVO_CONCILIADO',
            'RETENIDO_POR_REEMBOLSO'
        ));

    IF OBJECT_ID(N'dbo.Ajustes_Liquidacion_Repartidor', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Ajustes_Liquidacion_Repartidor
        (
            id_ajuste                  bigint IDENTITY(1,1) NOT NULL,
            id_liquidacion             bigint NOT NULL,
            id_reembolso               bigint NOT NULL,
            tipo                       varchar(30) NOT NULL
                CONSTRAINT DF_AjustesLiquidacionRepartidor_Tipo
                DEFAULT ('REVERSO_REEMBOLSO'),
            estado                     varchar(30) NOT NULL
                CONSTRAINT DF_AjustesLiquidacionRepartidor_Estado
                DEFAULT ('PENDIENTE_RECUPERACION'),
            metodo_recuperacion        varchar(30) NOT NULL
                CONSTRAINT DF_AjustesLiquidacionRepartidor_Metodo
                DEFAULT ('RECUPERACION_FUTURA'),
            monto_local                decimal(18,2) NOT NULL,
            moneda_local               varchar(3) NOT NULL,
            creado_el_utc              datetime2(7) NOT NULL
                CONSTRAINT DF_AjustesLiquidacionRepartidor_Creado
                DEFAULT (SYSUTCDATETIME()),
            confirmado_el_utc          datetime2(7) NULL,
            confirmado_por             varchar(15) NULL,
            referencia_confirmacion    varchar(100) NULL,
            observaciones              varchar(300) NULL,

            CONSTRAINT PK_Ajustes_Liquidacion_Repartidor
                PRIMARY KEY (id_ajuste),
            CONSTRAINT FK_AjustesLiquidacionRepartidor_Liquidacion
                FOREIGN KEY (id_liquidacion)
                REFERENCES dbo.Liquidaciones_Repartidor(id_liquidacion),
            CONSTRAINT FK_AjustesLiquidacionRepartidor_Reembolso
                FOREIGN KEY (id_reembolso)
                REFERENCES dbo.ReembolsosPedido(id_reembolso),
            CONSTRAINT CK_AjustesLiquidacionRepartidor_Tipo
                CHECK (tipo = 'REVERSO_REEMBOLSO'),
            CONSTRAINT CK_AjustesLiquidacionRepartidor_Estado
                CHECK (estado IN
                (
                    'PENDIENTE_RECUPERACION',
                    'APLICADO_A_REMESA',
                    'RECUPERADO'
                )),
            CONSTRAINT CK_AjustesLiquidacionRepartidor_Metodo
                CHECK (metodo_recuperacion IN
                (
                    'RECUPERACION_FUTURA',
                    'RETENCION_EN_REMESA',
                    'RETENCION_ANTES_DE_PAGO'
                )),
            CONSTRAINT CK_AjustesLiquidacionRepartidor_Monto
                CHECK (monto_local > 0 AND LEN(moneda_local) = 3)
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Ajustes_Liquidacion_Repartidor')
          AND name = N'UX_AjustesLiquidacionRepartidor_Reembolso'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_AjustesLiquidacionRepartidor_Reembolso
            ON dbo.Ajustes_Liquidacion_Repartidor(id_reembolso);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Ajustes_Liquidacion_Repartidor')
          AND name = N'IX_AjustesLiquidacionRepartidor_Liquidacion_Estado'
    )
    BEGIN
        CREATE INDEX IX_AjustesLiquidacionRepartidor_Liquidacion_Estado
            ON dbo.Ajustes_Liquidacion_Repartidor
            (
                id_liquidacion,
                estado,
                creado_el_utc DESC
            );
    END;

    COMMIT TRANSACTION;

    SELECT
        N'OK' AS resultado,
        OBJECT_ID(N'dbo.Ajustes_Liquidacion_Repartidor') AS ajustes_object_id,
        COL_LENGTH(N'dbo.ReembolsosPedido', N'monto_entrega_reembolsado') AS columna_monto_entrega_reembolsado;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
