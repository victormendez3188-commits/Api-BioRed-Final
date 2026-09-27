# Módulo de facturación, devoluciones y cuentas

Todos los endpoints requieren una cuenta con rol `ADMIN` y un Bearer Token válido.

## Preparación

1. Compile primero la solución.
2. Ejecute `SQL/10_Preparar_Facturacion_Contabilidad.sql`. El script es
   idempotente y puede volver a ejecutarse si necesita comprobar la instalación.
3. Inicie la API y autorice Scalar con el token del administrador.

El script corrige la relación de cuentas por pagar para usar `CompraEncabezado`,
vincula las facturas con pedidos y agrega saldos, vencimientos, documentos e
información de auditoría.

## Endpoints

| Área | Método | Endpoint |
| --- | --- | --- |
| Facturas | GET | `/api/v1/management/invoices` |
| Facturas | GET | `/api/v1/management/invoices/{invoiceId}` |
| Facturas | POST | `/api/v1/management/invoices` |
| Devoluciones | GET | `/api/v1/management/returns` |
| Devoluciones | GET | `/api/v1/management/returns/{returnId}` |
| Devoluciones | POST | `/api/v1/management/returns` |
| Cuentas por cobrar | GET | `/api/v1/management/receivables` |
| Cuentas por cobrar | GET | `/api/v1/management/receivables/{receivableId}` |
| Cuentas por cobrar | POST | `/api/v1/management/receivables/{receivableId}/payments` |
| Cuentas por pagar | GET | `/api/v1/management/payables` |
| Cuentas por pagar | GET | `/api/v1/management/payables/{payableId}` |
| Cuentas por pagar | POST | `/api/v1/management/payables` |
| Cuentas por pagar | POST | `/api/v1/management/payables/{payableId}/payments` |

## Reglas de negocio

- Cada pedido puede generar una sola factura.
- La factura copia los productos y precios del pedido; no descuenta inventario
  nuevamente porque el pedido ya hizo esa salida.
- Al emitir una factura se crea automáticamente su cuenta por cobrar.
- Un cobro en efectivo solo puede registrarse cuando el pedido está `Entregado`.
- Un cobro marcado como PayPal no puede agregarse manualmente. El módulo de pagos
  lo registrará únicamente después de validar la captura con PayPal Sandbox.
- Una devolución solo se admite para un pedido entregado, no puede exceder la
  cantidad facturada y repone inventario y Kardex.
- La devolución reduce primero el saldo por cobrar; cualquier exceso queda como
  reembolso pendiente para que el módulo de pagos lo procese.
- Cada compra registrada puede generar una sola cuenta por pagar.
- Los pagos parciales conservan el saldo anterior, el nuevo saldo, el usuario y
  la forma de pago.
- Los importes de cobros y pagos admiten como máximo dos decimales.

## Ejemplos

### Emitir factura desde un pedido

Use un `orderId` existente. Para las pruebas actuales se puede comenzar con el
pedido `2`, pero primero debe comprobarse con `GET /api/v1/orders/2`.

```json
{
  "orderId": 2,
  "documentSeries": "FAC",
  "documentNumber": "VENTA-TEST-001",
  "dueDate": null,
  "observations": "Factura de prueba del módulo contable."
}
```

### Registrar efectivo contra entrega

```json
{
  "amount": 1.00,
  "paymentTypeCode": "PAGO-EFECTIVO",
  "externalReference": "RECIBO-TEST-001",
  "observations": "Abono de prueba recibido al entregar el pedido."
}
```

El importe debe ajustarse al saldo real. La API impide cobrar más que el saldo.

### Registrar devolución

Use un producto que aparezca en la factura. `lotNumber` es opcional; cuando se
envía debe ser un lote existente del producto.

```json
{
  "invoiceId": 1,
  "reason": "Producto devuelto durante la prueba funcional.",
  "items": [
    {
      "productCode": "PROD-TEST-01",
      "quantity": 1,
      "lotNumber": null
    }
  ]
}
```

### Crear cuenta por pagar desde una compra registrada

```json
{
  "purchaseId": 2,
  "dueDate": null
}
```

### Registrar pago al proveedor

```json
{
  "amount": 10.00,
  "paymentTypeCode": "PAGO-EFECTIVO",
  "externalReference": "EGRESO-TEST-001",
  "observations": "Abono de prueba al proveedor."
}
```

## Orden de pruebas

1. Consultar un pedido existente y emitir su factura.
2. Consultar la factura y la cuenta por cobrar creada automáticamente.
3. Verificar que el efectivo sea rechazado si el pedido todavía no está entregado.
4. Con el pedido entregado, registrar un abono y revisar el nuevo saldo.
5. Crear una devolución pequeña y verificar inventario, Kardex y saldo.
6. Crear una nueva compra registrada.
7. Generar su cuenta por pagar y registrar un abono.
8. Comprobar consultas, filtros, duplicados y pagos mayores al saldo.
