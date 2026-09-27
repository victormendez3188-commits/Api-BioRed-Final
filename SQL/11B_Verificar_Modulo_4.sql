USE BioRed;
GO

SET NOCOUNT ON;
GO

/* Verificación de solo lectura para el Módulo 4. */

SELECT
    CASE WHEN OBJECT_ID(N'dbo.Promociones', N'U') IS NOT NULL THEN 1 ELSE 0 END AS tabla_promociones,
    CASE WHEN OBJECT_ID(N'dbo.PromocionUsos', N'U') IS NOT NULL THEN 1 ELSE 0 END AS tabla_usos_promocion,
    CASE WHEN OBJECT_ID(N'dbo.RecetasCliente', N'U') IS NOT NULL THEN 1 ELSE 0 END AS tabla_recetas,
    CASE WHEN OBJECT_ID(N'dbo.PagosPedido', N'U') IS NOT NULL THEN 1 ELSE 0 END AS tabla_pagos,
    CASE WHEN OBJECT_ID(N'dbo.ReembolsosPedido', N'U') IS NOT NULL THEN 1 ELSE 0 END AS tabla_reembolsos;
GO

SELECT cod_tipo_pago, nombre_tipo_pago, estado
FROM dbo.Tipo_Pago
WHERE cod_tipo_pago IN ('PAGO-EFECTIVO', 'PAGO-PAYPAL')
ORDER BY cod_tipo_pago;
GO

SELECT
    (SELECT COUNT(*) FROM dbo.Promociones) AS promociones,
    (SELECT COUNT(*) FROM dbo.PromocionUsos) AS usos_promocion,
    (SELECT COUNT(*) FROM dbo.RecetasCliente) AS recetas,
    (SELECT COUNT(*) FROM dbo.PagosPedido) AS pagos,
    (SELECT COUNT(*) FROM dbo.ReembolsosPedido) AS reembolsos;
GO

SELECT id_pedido, COUNT(*) AS pagos_completados
FROM dbo.PagosPedido
WHERE estado = 'COMPLETED'
GROUP BY id_pedido
HAVING COUNT(*) > 1;
GO

SELECT
    r.id_pago,
    SUM(r.monto_local) AS total_reembolsado,
    MAX(p.monto_local) AS monto_pagado
FROM dbo.ReembolsosPedido AS r
INNER JOIN dbo.PagosPedido AS p ON p.id_pago = r.id_pago
WHERE r.estado <> 'FAILED'
GROUP BY r.id_pago
HAVING SUM(r.monto_local) > MAX(p.monto_local);
GO

SELECT
    d.id_devolucion,
    d.total_devolucion,
    SUM(ISNULL(dd.monto_linea, dd.precio_venta * dd.cantidad)) AS total_detalle
FROM dbo.DevolucionEncabezado AS d
INNER JOIN dbo.DevolucionDetalle AS dd
    ON dd.id_devolucion = d.id_devolucion
GROUP BY d.id_devolucion, d.total_devolucion
HAVING d.total_devolucion <>
       SUM(ISNULL(dd.monto_linea, dd.precio_venta * dd.cantidad));
GO
