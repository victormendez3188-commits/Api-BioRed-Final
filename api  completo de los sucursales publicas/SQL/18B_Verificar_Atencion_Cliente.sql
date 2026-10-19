/* Verificación no destructiva del módulo Atención al cliente. */

SELECT
    t.id_ticket,
    t.id_cuenta_cliente,
    t.id_pedido,
    t.asunto,
    t.categoria,
    t.estado,
    t.prioridad,
    t.fecha_creacion_utc,
    t.fecha_actualizacion_utc,
    t.fecha_cierre_utc,
    COUNT(m.id_mensaje) AS cantidad_mensajes
FROM dbo.Tickets_Atencion AS t
LEFT JOIN dbo.Mensajes_Ticket_Atencion AS m
    ON m.id_ticket = t.id_ticket
GROUP BY
    t.id_ticket,
    t.id_cuenta_cliente,
    t.id_pedido,
    t.asunto,
    t.categoria,
    t.estado,
    t.prioridad,
    t.fecha_creacion_utc,
    t.fecha_actualizacion_utc,
    t.fecha_cierre_utc
ORDER BY t.fecha_actualizacion_utc DESC, t.id_ticket DESC;
