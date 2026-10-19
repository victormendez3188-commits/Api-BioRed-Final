/*
aqui agregamos  en nuestro preyecto BioRed  los Chat instantáneo, confirmación de entrega y registro móvil de repartidor
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Pedidos', N'U') IS NULL OR
       OBJECT_ID(N'dbo.Cuentas_Acceso', N'U') IS NULL OR
       OBJECT_ID(N'dbo.Repartidores', N'U') IS NULL
    BEGIN
        THROW 52000, 'Faltan las tablas Pedidos, Cuentas_Acceso o Repartidores.', 1;
    END;

    /* La licencia se usa únicamente para el registro móvil del repartidor. */
    IF COL_LENGTH(N'dbo.Repartidores', N'numero_licencia') IS NULL
    BEGIN
        ALTER TABLE dbo.Repartidores
            ADD numero_licencia varchar(30) NULL;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Repartidores')
          AND name = N'UX_Repartidores_NumeroLicencia'
    )
    BEGIN
        /*
           SQL Server compila el lote completo antes de reconocer una columna
           agregada con ALTER TABLE en este mismo lote. Por eso el índice se
           crea mediante SQL dinámico, cuando numero_licencia ya existe.
        */
        EXEC sys.sp_executesql N'
            CREATE UNIQUE INDEX UX_Repartidores_NumeroLicencia
                ON dbo.Repartidores(numero_licencia)
                WHERE numero_licencia IS NOT NULL AND numero_licencia <> '''';';
    END;

    IF OBJECT_ID(N'dbo.Codigos_Entrega_Pedido', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Codigos_Entrega_Pedido
        (
            id_codigo_entrega      bigint IDENTITY(1,1) NOT NULL,
            id_pedido              int NOT NULL,
            codigo_hash            varchar(255) NOT NULL,
            fecha_generacion_utc   datetime2(7) NOT NULL,
            fecha_expiracion_utc   datetime2(7) NOT NULL,
            intentos_fallidos      int NOT NULL
                CONSTRAINT DF_CodigosEntrega_IntentosFallidos DEFAULT (0),
            max_intentos           int NOT NULL
                CONSTRAINT DF_CodigosEntrega_MaxIntentos DEFAULT (5),
            estado_envio           varchar(30) NOT NULL
                CONSTRAINT DF_CodigosEntrega_EstadoEnvio DEFAULT ('PENDIENTE'),
            intentos_envio         int NOT NULL
                CONSTRAINT DF_CodigosEntrega_IntentosEnvio DEFAULT (0),
            ultimo_envio_utc       datetime2(7) NULL,
            detalle_fallo_envio    varchar(500) NULL,
            validado               bit NOT NULL
                CONSTRAINT DF_CodigosEntrega_Validado DEFAULT (0),
            id_cuenta_validacion   int NULL,
            fecha_validacion_utc   datetime2(7) NULL,

            CONSTRAINT PK_Codigos_Entrega_Pedido
                PRIMARY KEY (id_codigo_entrega),
            CONSTRAINT FK_CodigosEntrega_Pedido
                FOREIGN KEY (id_pedido)
                REFERENCES dbo.Pedidos(id_pedido),
            CONSTRAINT FK_CodigosEntrega_CuentaValidacion
                FOREIGN KEY (id_cuenta_validacion)
                REFERENCES dbo.Cuentas_Acceso(id_cuenta),
            CONSTRAINT CK_CodigosEntrega_Fechas
                CHECK (fecha_expiracion_utc > fecha_generacion_utc),
            CONSTRAINT CK_CodigosEntrega_Intentos
                CHECK (intentos_fallidos >= 0 AND max_intentos BETWEEN 3 AND 10 AND intentos_envio >= 0),
            CONSTRAINT CK_CodigosEntrega_EstadoEnvio
                CHECK (estado_envio IN ('PENDIENTE', 'ENVIADO', 'FALLIDO')),
            CONSTRAINT CK_CodigosEntrega_Validacion
                CHECK
                (
                    (validado = 0 AND id_cuenta_validacion IS NULL AND fecha_validacion_utc IS NULL)
                    OR
                    (validado = 1 AND id_cuenta_validacion IS NOT NULL AND fecha_validacion_utc IS NOT NULL)
                )
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Codigos_Entrega_Pedido')
          AND name = N'UX_CodigosEntrega_Pedido'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_CodigosEntrega_Pedido
            ON dbo.Codigos_Entrega_Pedido(id_pedido);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Codigos_Entrega_Pedido')
          AND name = N'IX_CodigosEntrega_Pedido_Validado'
    )
    BEGIN
        CREATE INDEX IX_CodigosEntrega_Pedido_Validado
            ON dbo.Codigos_Entrega_Pedido(id_pedido, validado);
    END;

    COMMIT TRANSACTION;

    SELECT
        N'OK' AS resultado,
        OBJECT_ID(N'dbo.Codigos_Entrega_Pedido') AS codigos_entrega_object_id,
        COL_LENGTH(N'dbo.Repartidores', N'numero_licencia') AS numero_licencia_existe;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
