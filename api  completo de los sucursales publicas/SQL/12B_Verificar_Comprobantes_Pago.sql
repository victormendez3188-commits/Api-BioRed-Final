USE BioRed;
GO

SET NOCOUNT ON;
GO

SELECT
    CASE WHEN OBJECT_ID(N'dbo.ComprobantesPago', N'U') IS NOT NULL
         THEN 1 ELSE 0 END AS tabla_comprobantes_pago;

SELECT
    cp.id_comprobante,
    cp.numero_comprobante,
    cp.id_pago,
    p.id_pedido,
    cp.cod_cliente,
    cp.correo_destino,
    cp.estado_envio,
    cp.intentos_envio,
    cp.ultimo_envio_utc,
    cp.detalle_fallo,
    cp.fecha_emision_utc
FROM dbo.ComprobantesPago AS cp
INNER JOIN dbo.PagosPedido AS p
    ON p.id_pago = cp.id_pago
ORDER BY cp.id_comprobante;

/* Debe regresar cero filas: pago completado sin comprobante. */
SELECT
    p.id_pago,
    p.id_pedido,
    p.estado
FROM dbo.PagosPedido AS p
LEFT JOIN dbo.ComprobantesPago AS cp
    ON cp.id_pago = p.id_pago
WHERE p.estado = 'COMPLETED'
  AND cp.id_comprobante IS NULL;

/* Debe regresar cero filas: comprobantes repetidos. */
SELECT id_pago, COUNT(*) AS repeticiones
FROM dbo.ComprobantesPago
GROUP BY id_pago
HAVING COUNT(*) > 1;

/* Debe regresar cero filas: estados o contadores inválidos. */
SELECT *
FROM dbo.ComprobantesPago
WHERE estado_envio NOT IN
    ('NO_SOLICITADO', 'PENDIENTE', 'ENVIADO', 'FALLIDO')
   OR intentos_envio < 0;
GO
