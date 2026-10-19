SET NOCOUNT ON;

SELECT
    CASE
        WHEN OBJECT_ID(N'dbo.Cuentas_Acceso', N'U') IS NOT NULL THEN 1
        ELSE 0
    END AS tabla_cuentas_existe,
    CASE
        WHEN EXISTS
        (
            SELECT 1
            FROM dbo.Cuentas_Acceso
            WHERE tipo_usuario = 'Empleado'
              AND referencia_id = 'BOT-SOPORTE'
              AND correo = 'bot-soporte@biored.invalid'
        ) THEN 1
        ELSE 0
    END AS cuenta_bot_existe;

SELECT
    id_cuenta AS bot_account_id,
    correo,
    tipo_usuario,
    referencia_id,
    estado AS login_habilitado,
    creado_el
FROM dbo.Cuentas_Acceso
WHERE tipo_usuario = 'Empleado'
  AND referencia_id = 'BOT-SOPORTE';

SELECT TOP (20)
    mensaje.id_mensaje,
    mensaje.id_ticket,
    mensaje.contenido,
    mensaje.fecha_envio_utc
FROM dbo.Mensajes_Ticket_Atencion AS mensaje
INNER JOIN dbo.Cuentas_Acceso AS cuenta
    ON cuenta.id_cuenta = mensaje.id_cuenta_remitente
WHERE cuenta.tipo_usuario = 'Empleado'
  AND cuenta.referencia_id = 'BOT-SOPORTE'
ORDER BY mensaje.id_mensaje DESC;
