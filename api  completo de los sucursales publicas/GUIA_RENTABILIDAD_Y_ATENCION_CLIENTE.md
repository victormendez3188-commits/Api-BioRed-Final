# Rentabilidad y atención al cliente

Esta actualización agrega dos módulos nuevos sin modificar los pedidos, pagos,
liquidaciones, chat de pedido, mapa ni notificaciones ya existentes.

## 1. Preparar la base de datos

Antes de iniciar la API, ejecute una sola vez en Azure SQL:

```text
SQL/18_Preparar_Atencion_Cliente.sql
```

El script es aditivo e idempotente: crea únicamente las tablas
`Tickets_Atencion` y `Mensajes_Ticket_Atencion`, sus relaciones e índices. No
elimina pedidos, cuentas, pagos ni ningún dato existente.

Para revisar los tickets posteriormente puede ejecutar:

```text
SQL/18B_Verificar_Atencion_Cliente.sql
```

## 2. Reporte de rentabilidad

Estos endpoints son para el administrador que tenga el permiso `REPORTE_VER`.

| Método | Endpoint | Uso |
| --- | --- | --- |
| GET | `/api/v1/reports/profitability` | Resumen y tarjetas de pedidos pagados en un período. |
| GET | `/api/v1/reports/profitability/orders/{orderId}` | Rentabilidad de un pedido pagado específico. |

Ejemplo:

```http
GET /api/v1/reports/profitability?fromUtc=2026-10-01T00:00:00Z&toUtc=2026-10-31T23:59:59Z&branchCode=SUC-ADE-01&page=1&pageSize=20
Authorization: Bearer {token-admin}
```

Los campos importantes por pedido son:

- `platformCommission`: comisión de la plataforma guardada al crear el pedido.
- `deliveryRevenue`: costo de envío cobrado al cliente.
- `driverPayout`: monto que le corresponde al repartidor.
- `pharmacySettlement`: monto que se liquida a la farmacia.
- `grossPlatformEarnings`: `platformCommission + deliveryRevenue - driverPayout`.
- `completedRefunds`: reembolsos completados del pedido.
- `netPlatformEarnings`: `grossPlatformEarnings - completedRefunds`.

El reporte no inventa una comisión de PayPal: las tarifas reales del proveedor
no se guardan actualmente como un campo contable. Por eso `netPlatformEarnings`
significa ganancia neta de la plataforma **antes de tarifas variables del
proveedor de pago y otros gastos operativos**. La aplicación web debe mostrarlo
con esa etiqueta.

El precio de venta de los productos y su costo no se convierten aquí en una
ganancia histórica de la plataforma, porque el pedido no conserva un costo de
producto inmutable. La distribución oficial almacenada por la API es la
comisión de plataforma y la liquidación a farmacia, que sí se usa en el reporte.

## 3. Atención al cliente: aplicación cliente

Los endpoints de cliente requieren un JWT con rol `CLIENTE`. El cliente nunca
envía su cuenta, código de cliente ni un identificador de remitente: la API los
obtiene del JWT.

| Método | Endpoint | Uso |
| --- | --- | --- |
| POST | `/api/v1/support/tickets` | Crear un ticket y su primer mensaje. |
| GET | `/api/v1/support/tickets` | Listar solo los tickets propios. |
| GET | `/api/v1/support/tickets/{ticketId}` | Ver un ticket propio. |
| GET | `/api/v1/support/tickets/{ticketId}/messages` | Ver el historial paginado. |
| POST | `/api/v1/support/tickets/{ticketId}/messages` | Enviar un mensaje. |

Crear ticket:

```json
{
  "subject": "Necesito ayuda con mi pedido",
  "category": "PEDIDO",
  "orderId": 36,
  "content": "Quiero confirmar el horario de entrega."
}
```

Categorías permitidas: `PEDIDO`, `PAGO`, `FACTURACION`, `PRODUCTO`, `CUENTA`,
`OTRO`.

Para mensajes posteriores:

```json
{
  "content": "¿Podrían actualizarme, por favor?"
}
```

Los mensajes tienen de 1 a 1000 caracteres. Un ticket cerrado responde `409
Conflict` si se intenta enviar un mensaje. Cuando el cliente responde a un
ticket `RESUELTO`, la API lo devuelve a `EN_PROCESO`.

## 4. Atención al cliente: panel administrador

Los siguientes endpoints requieren JWT con rol `ADMIN`:

| Método | Endpoint | Uso |
| --- | --- | --- |
| GET | `/api/v1/management/support/tickets` | Ver todos los tickets. |
| GET | `/api/v1/management/support/tickets/{ticketId}` | Ver detalle. |
| GET | `/api/v1/management/support/tickets/{ticketId}/messages` | Ver conversación. |
| POST | `/api/v1/management/support/tickets/{ticketId}/messages` | Responder al cliente. |
| PATCH | `/api/v1/management/support/tickets/{ticketId}/status` | Cambiar estado o prioridad. |

Actualizar un ticket:

```json
{
  "status": "RESUELTO",
  "priority": "ALTA"
}
```

Estados permitidos: `ABIERTO`, `EN_PROCESO`, `RESUELTO`, `CERRADO`.

Prioridades permitidas: `NORMAL`, `ALTA`, `URGENTE`.

Cuando un administrador responde, la API intenta enviar una notificación push
al cliente con el tipo `support_ticket_message`. Si Firebase no está configurado
o falla, la respuesta sigue guardándose correctamente.

## 5. Prueba mínima recomendada

1. Ejecute `18_Preparar_Atencion_Cliente.sql`.
2. Compile y publique la API.
3. Inicie sesión como cliente y cree un ticket.
4. Inicie sesión como administrador, liste el ticket y responda.
5. Vuelva a entrar como cliente y consulte sus mensajes.
6. Cambie el ticket a `CERRADO` como administrador.
7. Intente responder como cliente: debe recibir `409`.
8. Consulte `GET /api/v1/reports/profitability/orders/{orderId}` con un pedido
   que tenga pago `COMPLETED`.
