/* BioRed - Verificación no destructiva de reembolsos de envío. */

SET NOCOUNT ON;

SELECT
    CASE WHEN COL_LENGTH(N'dbo.ReembolsosPedido', N'monto_entrega_reembolsado') IS NOT NULL
         THEN 1 ELSE 0 END AS columna_monto_entrega_reembolsado,
    CASE WHEN OBJECT_ID(N'dbo.Ajustes_Liquidacion_Repartidor', N'U') IS NOT NULL
         THEN 1 ELSE 0 END AS tabla_ajustes_liquidacion;

SELECT
    cc.name AS restriccion_estado_liquidacion,
    cc.definition
FROM sys.check_constraints AS cc
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Liquidaciones_Repartidor')
  AND (cc.name = N'CK_LiquidacionesRepartidor_Estado'
       OR cc.definition LIKE N'%RETENIDO_POR_REEMBOLSO%');

SELECT
    i.name AS indice,
    i.is_unique AS es_unico,
    i.filter_definition AS filtro
FROM sys.indexes AS i
WHERE i.object_id IN
(
    OBJECT_ID(N'dbo.ReembolsosPedido'),
    OBJECT_ID(N'dbo.Ajustes_Liquidacion_Repartidor')
)
AND i.name IN
(
    N'UX_ReembolsosPedido_EntregaPorPago',
    N'UX_AjustesLiquidacionRepartidor_Reembolso',
    N'IX_AjustesLiquidacionRepartidor_Liquidacion_Estado'
)
ORDER BY i.name;

SELECT TOP (100)
    a.id_ajuste,
    a.id_liquidacion,
    l.id_pedido,
    p.cod_pedido,
    l.cod_repartidor,
    a.id_reembolso,
    a.estado,
    a.metodo_recuperacion,
    a.monto_local,
    a.moneda_local,
    a.creado_el_utc,
    a.confirmado_el_utc,
    a.confirmado_por,
    a.referencia_confirmacion
FROM dbo.Ajustes_Liquidacion_Repartidor AS a
INNER JOIN dbo.Liquidaciones_Repartidor AS l
    ON l.id_liquidacion = a.id_liquidacion
INNER JOIN dbo.Pedidos AS p
    ON p.id_pedido = l.id_pedido
ORDER BY a.id_ajuste DESC;
