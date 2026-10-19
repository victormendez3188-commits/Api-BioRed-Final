/*
    BioRed - Atención al cliente

    Script aditivo e idempotente. No elimina ni modifica datos existentes.
    Ejecútelo una sola vez en Azure SQL antes de usar los endpoints de tickets.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Cuentas_Acceso', N'U') IS NULL OR
       OBJECT_ID(N'dbo.Pedidos', N'U') IS NULL
    BEGIN
        THROW 51800, 'Faltan las tablas Cuentas_Acceso o Pedidos para configurar atención al cliente.', 1;
    END;

    IF OBJECT_ID(N'dbo.Tickets_Atencion', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Tickets_Atencion
        (
            id_ticket                  bigint IDENTITY(1,1) NOT NULL,
            id_cuenta_cliente          int NOT NULL,
            id_pedido                  int NULL,
            asunto                     nvarchar(150) NOT NULL,
            categoria                  varchar(30) NOT NULL
                CONSTRAINT DF_TicketsAtencion_Categoria DEFAULT ('OTRO'),
            estado                     varchar(20) NOT NULL
                CONSTRAINT DF_TicketsAtencion_Estado DEFAULT ('ABIERTO'),
            prioridad                  varchar(20) NOT NULL
                CONSTRAINT DF_TicketsAtencion_Prioridad DEFAULT ('NORMAL'),
            fecha_creacion_utc         datetime2(7) NOT NULL
                CONSTRAINT DF_TicketsAtencion_Creado DEFAULT (SYSUTCDATETIME()),
            fecha_actualizacion_utc    datetime2(7) NOT NULL
                CONSTRAINT DF_TicketsAtencion_Actualizado DEFAULT (SYSUTCDATETIME()),
            fecha_cierre_utc           datetime2(7) NULL,
            cerrado_por_cuenta         int NULL,

            CONSTRAINT PK_Tickets_Atencion
                PRIMARY KEY (id_ticket),
            CONSTRAINT FK_TicketsAtencion_CuentaCliente
                FOREIGN KEY (id_cuenta_cliente)
                REFERENCES dbo.Cuentas_Acceso(id_cuenta),
            CONSTRAINT FK_TicketsAtencion_Pedido
                FOREIGN KEY (id_pedido)
                REFERENCES dbo.Pedidos(id_pedido),
            CONSTRAINT FK_TicketsAtencion_CerradoPor
                FOREIGN KEY (cerrado_por_cuenta)
                REFERENCES dbo.Cuentas_Acceso(id_cuenta),
            CONSTRAINT CK_TicketsAtencion_Categoria
                CHECK (categoria IN
                (
                    'PEDIDO',
                    'PAGO',
                    'FACTURACION',
                    'PRODUCTO',
                    'CUENTA',
                    'OTRO'
                )),
            CONSTRAINT CK_TicketsAtencion_Estado
                CHECK (estado IN ('ABIERTO', 'EN_PROCESO', 'RESUELTO', 'CERRADO')),
            CONSTRAINT CK_TicketsAtencion_Prioridad
                CHECK (prioridad IN ('NORMAL', 'ALTA', 'URGENTE'))
        );
    END;

    IF OBJECT_ID(N'dbo.Mensajes_Ticket_Atencion', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Mensajes_Ticket_Atencion
        (
            id_mensaje                 bigint IDENTITY(1,1) NOT NULL,
            id_ticket                  bigint NOT NULL,
            id_cuenta_remitente        int NOT NULL,
            contenido                  nvarchar(1000) NOT NULL,
            fecha_envio_utc            datetime2(7) NOT NULL
                CONSTRAINT DF_MensajesTicketAtencion_Enviado DEFAULT (SYSUTCDATETIME()),

            CONSTRAINT PK_Mensajes_Ticket_Atencion
                PRIMARY KEY (id_mensaje),
            CONSTRAINT FK_MensajesTicketAtencion_Ticket
                FOREIGN KEY (id_ticket)
                REFERENCES dbo.Tickets_Atencion(id_ticket),
            CONSTRAINT FK_MensajesTicketAtencion_Remitente
                FOREIGN KEY (id_cuenta_remitente)
                REFERENCES dbo.Cuentas_Acceso(id_cuenta)
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Tickets_Atencion')
          AND name = N'IX_TicketsAtencion_Cliente_Estado_Actualizacion'
    )
    BEGIN
        CREATE INDEX IX_TicketsAtencion_Cliente_Estado_Actualizacion
            ON dbo.Tickets_Atencion
            (id_cuenta_cliente, estado, fecha_actualizacion_utc DESC);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Tickets_Atencion')
          AND name = N'IX_TicketsAtencion_Pedido'
    )
    BEGIN
        CREATE INDEX IX_TicketsAtencion_Pedido
            ON dbo.Tickets_Atencion(id_pedido);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Mensajes_Ticket_Atencion')
          AND name = N'IX_MensajesTicketAtencion_Ticket_Mensaje'
    )
    BEGIN
        CREATE INDEX IX_MensajesTicketAtencion_Ticket_Mensaje
            ON dbo.Mensajes_Ticket_Atencion(id_ticket, id_mensaje);
    END;

    COMMIT TRANSACTION;

    SELECT
        N'OK' AS resultado,
        OBJECT_ID(N'dbo.Tickets_Atencion') AS tickets_object_id,
        OBJECT_ID(N'dbo.Mensajes_Ticket_Atencion') AS mensajes_object_id;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
