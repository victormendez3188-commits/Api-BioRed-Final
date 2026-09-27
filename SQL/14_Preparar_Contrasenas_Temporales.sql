/*
   Módulo de contraseñas temporales para cuentas nuevas.
   Ejecutar conectado directamente a la base BioRed.
   Es idempotente: puede ejecutarse más de una vez.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Cuentas_Acceso', N'U') IS NULL
    THROW 50001, 'No existe dbo.Cuentas_Acceso en la base seleccionada.', 1;
GO

IF COL_LENGTH(N'dbo.Cuentas_Acceso', N'requiere_cambio_password') IS NULL
    EXEC(N'ALTER TABLE dbo.Cuentas_Acceso ADD requiere_cambio_password bit NOT NULL CONSTRAINT DF_CuentasAcceso_RequiereCambioPassword DEFAULT (0) WITH VALUES;');
GO

IF COL_LENGTH(N'dbo.Cuentas_Acceso', N'password_temporal_expira_utc') IS NULL
    EXEC(N'ALTER TABLE dbo.Cuentas_Acceso ADD password_temporal_expira_utc datetime2(7) NULL;');
GO

IF COL_LENGTH(N'dbo.Cuentas_Acceso', N'password_actualizada_utc') IS NULL
    EXEC(N'ALTER TABLE dbo.Cuentas_Acceso ADD password_actualizada_utc datetime2(7) NULL;');
GO

IF COL_LENGTH(N'dbo.Cuentas_Acceso', N'estado_envio_password_temporal') IS NULL
    EXEC(N'ALTER TABLE dbo.Cuentas_Acceso ADD estado_envio_password_temporal varchar(20) NOT NULL CONSTRAINT DF_CuentasAcceso_EstadoEnvioPasswordTemporal DEFAULT (''NO_APLICA'') WITH VALUES;');
GO

IF COL_LENGTH(N'dbo.Cuentas_Acceso', N'intentos_envio_password_temporal') IS NULL
    EXEC(N'ALTER TABLE dbo.Cuentas_Acceso ADD intentos_envio_password_temporal int NOT NULL CONSTRAINT DF_CuentasAcceso_IntentosEnvioPasswordTemporal DEFAULT (0) WITH VALUES;');
GO

IF COL_LENGTH(N'dbo.Cuentas_Acceso', N'ultimo_envio_password_temporal_utc') IS NULL
    EXEC(N'ALTER TABLE dbo.Cuentas_Acceso ADD ultimo_envio_password_temporal_utc datetime2(7) NULL;');
GO

IF COL_LENGTH(N'dbo.Cuentas_Acceso', N'detalle_fallo_password_temporal') IS NULL
    EXEC(N'ALTER TABLE dbo.Cuentas_Acceso ADD detalle_fallo_password_temporal varchar(500) NULL;');
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Cuentas_Acceso')
      AND name = N'CK_CuentasAcceso_EstadoEnvioPasswordTemporal'
)
    ALTER TABLE dbo.Cuentas_Acceso WITH CHECK
    ADD CONSTRAINT CK_CuentasAcceso_EstadoEnvioPasswordTemporal
        CHECK (estado_envio_password_temporal IN
            ('NO_APLICA', 'PENDIENTE', 'ENVIADO', 'FALLIDO', 'UTILIZADO'));
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Cuentas_Acceso')
      AND name = N'CK_CuentasAcceso_IntentosEnvioPasswordTemporal'
)
    ALTER TABLE dbo.Cuentas_Acceso WITH CHECK
    ADD CONSTRAINT CK_CuentasAcceso_IntentosEnvioPasswordTemporal
        CHECK (intentos_envio_password_temporal >= 0);
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Cuentas_Acceso')
      AND name = N'IX_CuentasAcceso_PasswordTemporalPendiente'
)
    CREATE INDEX IX_CuentasAcceso_PasswordTemporalPendiente
        ON dbo.Cuentas_Acceso
        (
            requiere_cambio_password,
            password_temporal_expira_utc
        )
        INCLUDE
        (
            correo,
            estado_envio_password_temporal
        );
GO

SELECT
    DB_NAME() AS base_datos,
    COL_LENGTH(N'dbo.Cuentas_Acceso', N'requiere_cambio_password') AS columna_cambio,
    COL_LENGTH(N'dbo.Cuentas_Acceso', N'password_temporal_expira_utc') AS columna_vencimiento,
    COL_LENGTH(N'dbo.Cuentas_Acceso', N'estado_envio_password_temporal') AS columna_correo;
GO
