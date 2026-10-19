# BioRed: guía de despliegue y pruebas

Esta copia incluye los cambios solicitados para la aplicación del cliente y del
repartidor. No contiene contraseñas, cadenas de conexión ni claves privadas.

## Funciones incluidas

- cancelación de pedidos en estado `Pendiente` o `Aceptado`;
- venta por presentación o por unidad, con inventario, factura, cancelación y
  devolución consistentes;
- referencia y coordenadas del cliente en el detalle del pedido;
- registro y desactivación de dispositivos FCM;
- chat privado entre el cliente propietario y el repartidor asignado;
- marcado de mensajes como leídos;
- notificaciones automáticas de asignación, cambios de estado y mensajes;
- ruta interna mediante Google Routes y `encodedPolyline`;
- límites de solicitudes para chat, rutas, dispositivos y liquidaciones;
- pago automático de la entrega al repartidor mediante PayPal Payouts;
- registro y conciliación del efectivo cobrado por el repartidor.

## 1. Base de datos

Antes de publicar esta versión ejecute, en este orden y sobre la base de datos
correcta:

1. `SQL/16_Preparar_Venta_Por_Unidad.sql`
2. `SQL/17_Preparar_Liquidaciones_Repartidor.sql`

No vuelva a ejecutar `SQL/15_Preparar_Chat_Notificaciones_Push.sql` si las tablas
`Dispositivos_Notificacion` y `Mensajes_Pedido` ya existen. Esta versión no usa
`EnsureCreated` ni una migración para volver a crear esas tablas.

Los scripts 16 y 17 son aditivos e idempotentes: no eliminan tablas ni registros.
Siempre haga una copia de seguridad antes de ejecutar cambios en producción.

### Activar un producto para venta por unidad

`Inventario.cantidad_actual`, `stock_minimo` y `stock_maximo` deben expresarse en
unidades base para un producto activado (por ejemplo, tabletas). Si antes se
guardaban 10 blísteres de 10 tabletas, la existencia debe convertirse una sola
vez a 100 unidades. No repita esa conversión.

Después registre la configuración del producto en `ProductosVentaUnidad` con:

- cantidad de unidades por presentación;
- precio por unidad;
- nombre de la unidad base;
- `permite_venta_unidad = 1` y `activo = 1`.

Finalmente active en Azure:

`UnitSales__Enabled=true`

## 2. Configuración protegida de Azure

Configure estos valores en App Service > Environment variables. No los escriba
en `appsettings.json` ni los suba a GitHub.

```text
ConnectionStrings__BioRedConnection=CADENA_AZURE_SQL
Jwt__Key=CLAVE_BASE64_DE_32_BYTES_O_MAS

Firebase__Enabled=true
Firebase__ProjectId=ID_PROYECTO_FIREBASE
Firebase__ServiceAccountJsonBase64=JSON_CUENTA_SERVICIO_EN_BASE64

GoogleRoutes__Enabled=true
GoogleRoutes__BaseUrl=https://routes.googleapis.com/
GoogleRoutes__ApiKey=CLAVE_PROTEGIDA

UnitSales__Enabled=true

DriverSettlements__Enabled=true
DriverSettlements__AutoProcessPayPalOnDelivery=true
DriverSettlements__ReconciliationIntervalSeconds=60
PayPal__ClientId=CLIENT_ID_PROTEGIDO
PayPal__ClientSecret=CLIENT_SECRET_PROTEGIDO
```

Para las pruebas use PayPal Sandbox. La cuenta empresarial que posee el
`ClientId` debe tener acceso a Payouts y saldo/capacidad para enviar pagos. Cada
repartidor configura el correo de su cuenta PayPal Sandbox personal.

## 3. Flujo de pago al repartidor

### Pedido pagado con PayPal

1. El cliente paga y la captura queda `COMPLETED`.
2. Se asigna un repartidor y se completa el flujo normal del pedido.
3. Al cambiar a `Entregado`, la API crea una liquidación única usando
   `Pedido.pago_repartidor`.
4. La API envía el Payout al correo PayPal configurado por el repartidor.
5. Un proceso interno consulta los Payouts en estado `PROCESANDO` hasta obtener
   el resultado definitivo.

Si falta el correo del repartidor o Payouts está deshabilitado, el pedido sigue
entregado y la liquidación queda `PENDIENTE_CONFIGURACION`; no se pierde.

### Pedido con efectivo contra entrega

1. El cliente entrega al repartidor el total del pedido.
2. El repartidor o el administrador confirma el cobro con el endpoint existente
   de pago en efectivo.
3. La API registra `EFECTIVO_COBRADO`.
4. El repartidor conserva `pago_repartidor` y entrega a la farmacia/plataforma
   `total_cobrado - pago_repartidor`.
5. Un administrador confirma esa entrega y la liquidación cambia a
   `EFECTIVO_CONCILIADO`.

No se envía un Payout PayPal en un pedido de efectivo, porque el repartidor ya
recibió físicamente su pago de entrega.

## 4. Endpoints nuevos de liquidación

### Repartidor autenticado

```text
GET  /api/v1/drivers/me/payout-account
PUT  /api/v1/drivers/me/payout-account
GET  /api/v1/drivers/me/settlements
GET  /api/v1/drivers/me/settlements/{settlementId}
POST /api/v1/drivers/me/settlements/{settlementId}/refresh
```

Cuerpo para configurar PayPal:

```json
{
  "payPalEmail": "repartidor-sandbox@example.com"
}
```

La API obtiene el repartidor de la claim `reference_id`; la aplicación no envía
el código del repartidor.

### Administrador

```text
GET  /api/v1/management/driver-settlements
GET  /api/v1/management/driver-settlements/{settlementId}
POST /api/v1/management/driver-settlements/{settlementId}/retry
POST /api/v1/management/driver-settlements/{settlementId}/refresh
POST /api/v1/management/driver-settlements/{settlementId}/cash-remittance/confirm
```

Cuerpo de conciliación de efectivo:

```json
{
  "reference": "CAJA-2026-0001",
  "observations": "Efectivo recibido y conciliado."
}
```

## 5. Cancelación solicitada por el cliente

```text
POST /api/v1/orders/{orderId}/cancel
```

```json
{
  "reason": "Ya no necesito el pedido."
}
```

El cliente solo puede cancelar su propio pedido mientras esté `Pendiente` o
`Aceptado`. Si el pago ya fue completado, primero debe seguirse el flujo de
reembolso. La cancelación restaura inventario y libera al repartidor cuando ya no
tiene otro pedido activo.

## 6. Comprar una unidad

El catálogo indica `canSellByUnit`, `unitPrice`, `baseUnit`,
`unitsPerPresentation`, `availableUnits` y `availablePresentations`.

Ejemplo de pedido de tres tabletas:

```json
{
  "productCode": "MED-001",
  "quantity": 3,
  "sellByUnit": true
}
```

Para comprar dos blísteres del mismo producto:

```json
{
  "productCode": "MED-001",
  "quantity": 2,
  "sellByUnit": false
}
```

No se permite enviar el mismo producto simultáneamente en ambos modos dentro de
un pedido.

## 7. Chat, dispositivos y mapa

```text
POST  /api/v1/notification-devices
POST  /api/v1/notification-devices/unregister

GET   /api/v1/orders/{orderId}/messages
POST  /api/v1/orders/{orderId}/messages
PATCH /api/v1/orders/{orderId}/messages/read

GET   /api/v1/orders/{orderId}/route
```

El destino de la ruta sale siempre de `Direcciones_Cliente`; la aplicación solo
envía la ubicación actual del repartidor. El chat valida por JWT al cliente
propietario y al repartidor asignado.

## 8. Compilar y probar

Desde la carpeta que contiene `BioRed.sln`:

```powershell
dotnet restore BioRed.sln
dotnet build BioRed.sln --configuration Release
```

Después pruebe primero en Sandbox y revise Log Stream. No active Payouts en
producción hasta confirmar importes, tipo de cambio, cuenta emisora y correos de
los repartidores.
