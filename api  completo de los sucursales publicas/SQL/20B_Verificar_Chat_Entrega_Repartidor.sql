/* vamos a Verificar el chat del entrega del repartidor  */

SET NOCOUNT ON;

SELECT
    OBJECT_ID(N'dbo.Codigos_Entrega_Pedido', N'U') AS codigos_entrega_object_id,
    COL_LENGTH(N'dbo.Repartidores', N'numero_licencia') AS numero_licencia_existe;

SELECT
    i.name AS indice,
    i.is_unique AS es_unico
FROM sys.indexes AS i
WHERE i.object_id = OBJECT_ID(N'dbo.Codigos_Entrega_Pedido')
  AND i.name IN (N'UX_CodigosEntrega_Pedido', N'IX_CodigosEntrega_Pedido_Validado');

SELECT TOP (20)
    c.id_codigo_entrega,
    c.id_pedido,
    c.fecha_generacion_utc,
    c.fecha_expiracion_utc,
    c.intentos_fallidos,
    c.max_intentos,
    c.estado_envio,
    c.intentos_envio,
    c.validado,
    c.fecha_validacion_utc
FROM dbo.Codigos_Entrega_Pedido AS c
ORDER BY c.id_codigo_entrega DESC;
