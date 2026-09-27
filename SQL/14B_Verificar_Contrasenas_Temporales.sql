/* Verificación del módulo de contraseñas temporales. */

SET NOCOUNT ON;
GO

SELECT
    CASE WHEN COL_LENGTH(N'dbo.Cuentas_Acceso', N'requiere_cambio_password') IS NOT NULL THEN 1 ELSE 0 END AS columna_cambio,
    CASE WHEN COL_LENGTH(N'dbo.Cuentas_Acceso', N'password_temporal_expira_utc') IS NOT NULL THEN 1 ELSE 0 END AS columna_vencimiento,
    CASE WHEN COL_LENGTH(N'dbo.Cuentas_Acceso', N'estado_envio_password_temporal') IS NOT NULL THEN 1 ELSE 0 END AS columna_estado_correo,
    CASE WHEN OBJECT_ID(N'dbo.Cuentas_Acceso', N'U') IS NOT NULL THEN 1 ELSE 0 END AS tabla_cuentas;
GO

SELECT
    id_cuenta,
    correo,
    tipo_usuario,
    referencia_id,
    estado,
    requiere_cambio_password,
    password_temporal_expira_utc,
    password_actualizada_utc,
    estado_envio_password_temporal,
    intentos_envio_password_temporal,
    ultimo_envio_password_temporal_utc,
    detalle_fallo_password_temporal
FROM dbo.Cuentas_Acceso
ORDER BY id_cuenta DESC;
GO
