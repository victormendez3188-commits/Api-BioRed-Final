USE BioRed;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.ComprobantesPago', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ComprobantesPago
        (
            id_comprobante      BIGINT IDENTITY(1, 1) NOT NULL,
            id_pago             BIGINT NOT NULL,
            numero_comprobante  VARCHAR(40) NOT NULL,
            cod_cliente         VARCHAR(15) NOT NULL,
            correo_destino      VARCHAR(100) NULL,
            fecha_emision_utc   DATETIME2(7) NOT NULL
                CONSTRAINT DF_ComprobantesPago_FechaEmision
                DEFAULT SYSUTCDATETIME(),
            estado_envio        VARCHAR(20) NOT NULL
                CONSTRAINT DF_ComprobantesPago_EstadoEnvio
                DEFAULT 'NO_SOLICITADO',
            intentos_envio      INT NOT NULL
                CONSTRAINT DF_ComprobantesPago_IntentosEnvio
                DEFAULT 0,
            ultimo_envio_utc    DATETIME2(7) NULL,
            detalle_fallo       VARCHAR(500) NULL,

            CONSTRAINT PK_ComprobantesPago
                PRIMARY KEY (id_comprobante),
            CONSTRAINT UQ_ComprobantesPago_IdPago
                UNIQUE (id_pago),
            CONSTRAINT UQ_ComprobantesPago_Numero
                UNIQUE (numero_comprobante),
            CONSTRAINT FK_ComprobantesPago_PagosPedido
                FOREIGN KEY (id_pago)
                REFERENCES dbo.PagosPedido (id_pago),
            CONSTRAINT FK_ComprobantesPago_Clientes
                FOREIGN KEY (cod_cliente)
                REFERENCES dbo.Clientes (cod_cliente),
            CONSTRAINT CK_ComprobantesPago_EstadoEnvio
                CHECK (estado_envio IN
                    ('NO_SOLICITADO', 'PENDIENTE', 'ENVIADO', 'FALLIDO')),
            CONSTRAINT CK_ComprobantesPago_IntentosEnvio
                CHECK (intentos_envio >= 0)
        );
    END;

    /* Crea el comprobante de pagos completados antes de instalar este módulo. */
    INSERT INTO dbo.ComprobantesPago
    (
        id_pago,
        numero_comprobante,
        cod_cliente,
        correo_destino,
        fecha_emision_utc,
        estado_envio,
        intentos_envio
    )
    SELECT
        p.id_pago,
        CONCAT(
            'REC-',
            CONVERT(CHAR(8), COALESCE(p.completado_el_utc, p.creado_el_utc), 112),
            '-',
            RIGHT(REPLICATE('0', 8) + CONVERT(VARCHAR(20), p.id_pago), 8)
        ),
        p.cod_cliente,
        COALESCE(NULLIF(LTRIM(RTRIM(c.email_cliente)), ''), ca.correo),
        COALESCE(p.completado_el_utc, p.creado_el_utc),
        'NO_SOLICITADO',
        0
    FROM dbo.PagosPedido AS p
    INNER JOIN dbo.Clientes AS c
        ON c.cod_cliente = p.cod_cliente
    OUTER APPLY
    (
        SELECT TOP (1) NULLIF(LTRIM(RTRIM(a.correo)), '') AS correo
        FROM dbo.Cuentas_Acceso AS a
        WHERE a.referencia_id = p.cod_cliente
          AND UPPER(a.tipo_usuario) = 'CLIENTE'
          AND a.estado = 1
        ORDER BY a.id_cuenta
    ) AS ca
    WHERE p.estado = 'COMPLETED'
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.ComprobantesPago AS cp
          WHERE cp.id_pago = p.id_pago
      );

    COMMIT TRANSACTION;

    SELECT
        id_comprobante,
        numero_comprobante,
        id_pago,
        cod_cliente,
        correo_destino,
        estado_envio,
        intentos_envio,
        fecha_emision_utc
    FROM dbo.ComprobantesPago
    ORDER BY id_comprobante;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO
