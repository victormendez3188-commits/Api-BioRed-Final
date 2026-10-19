# BioRed: chat en tiempo real y confirmación de entrega

## Antes de ejecutar la API

1. En Azure SQL ejecute `SQL/20_Preparar_Chat_Entrega_Repartidor.sql` una vez.
2. Ejecute `SQL/20B_Verificar_Chat_Entrega_Repartidor.sql` para comprobar la instalación.
3. Configure el correo existente de la API (`ReceiptEmail`) con valores protegidos. Ese mismo correo entrega el PIN de cuatro dígitos al cliente.
4. Compile y despliegue la API.

No hay migración de Entity Framework ni `EnsureCreated`. El script SQL es aditivo y no elimina datos.

## Chat cliente–repartidor estilo Uber

La base de datos y los endpoints REST existentes siguen siendo la fuente oficial del chat:

- `GET /api/v1/orders/{orderId}/messages?afterMessageId=0&pageSize=50`
- `POST /api/v1/orders/{orderId}/messages`
- `PATCH /api/v1/orders/{orderId}/messages/read`

Se agregó SignalR para que las aplicaciones reciban cambios al instante:

- Hub: `/hubs/order-chat`
- El cliente debe conectarse con su JWT en `access_token`.
- Después de conectar debe invocar `JoinOrder(orderId)`.
- Eventos recibidos:
  - `orderMessageCreated`
  - `orderMessagesRead`
- Al salir de un pedido debe invocar `LeaveOrder(orderId)`.

El cliente **no envía mensajes por el Hub**. Debe enviarlos mediante el POST REST; así se mantienen validación, autorización, historial, bitácora y notificación push. SignalR solo actualiza la pantalla inmediatamente.

Ejemplo conceptual para la app móvil:

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl($"{apiBaseUrl}/hubs/order-chat", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult(jwt);
    })
    .WithAutomaticReconnect()
    .Build();

connection.On<OrderChatMessageRealtimeEvent>("orderMessageCreated", message =>
{
    // Agregar o actualizar el mensaje en la conversación actual.
});

connection.On<OrderChatMessagesReadRealtimeEvent>("orderMessagesRead", read =>
{
    // Marcar los mensajes enviados como leídos.
});

await connection.StartAsync();
await connection.InvokeAsync("JoinOrder", orderId);
```

Para .NET MAUI, la aplicación debe agregar `Microsoft.AspNetCore.SignalR.Client`. Si la aplicación usa Flutter, use el cliente compatible de SignalR y los mismos nombres de hub, método y eventos.

## Confirmación de entrega con PIN

Al crear un pedido, la API genera un PIN aleatorio de cuatro dígitos, guarda únicamente su hash y lo manda al correo registrado del cliente. Nunca devuelve ni registra el PIN en texto.

Endpoints nuevos:

- `POST /api/v1/orders/{orderId}/delivery-code/resend`
  - Cliente propietario o administrador.
  - No necesita body.
  - Invalida el código anterior y manda uno nuevo por correo.
- `POST /api/v1/orders/{orderId}/delivery-code/verify`
  - Solo el repartidor asignado, con el pedido en `En Camino`.
  - Body:

```json
{ "code": "1234" }
```

La respuesta correcta incluye productos, precios, total, método de pago, `isPaid` y `amountToCollect`. Para PayPal completado el monto por cobrar es `0`; para efectivo pendiente equivale al total del pedido.

Para pedidos nuevos que sí tienen código emitido, el repartidor no puede cambiar el estado a `Entregado` hasta verificarlo. Los pedidos antiguos sin registro de código conservan su flujo anterior, para no bloquear datos históricos.

El código vence según `DeliveryConfirmation:CodeLifetimeMinutes` (por defecto 24 horas) y se bloquea tras `DeliveryConfirmation:MaxAttempts` intentos (por defecto 5). El reenvío genera otro código y reinicia los intentos.

SMS no se implementó porque requiere un proveedor de mensajería, cuenta y costos propios (por ejemplo Azure Communication Services o Twilio). La confirmación por correo ya está lista con la configuración SMTP que BioRed usa para comprobantes.

## Registro móvil de repartidor

Además del flujo existente con contraseña temporal (`POST /api/v1/drivers/register`), se agregó:

`POST /api/v1/auth/register-driver`

```json
{
  "firstName": "Ana",
  "lastName": "López",
  "email": "ana@gmail.com",
  "phone": "55555555",
  "documentNumber": "1234567890123",
  "licenseNumber": "LIC-123456",
  "vehicleType": "Motocicleta",
  "plate": "P123ABC",
  "password": "ClaveSegura#2026",
  "confirmPassword": "ClaveSegura#2026"
}
```

La API valida correo Gmail, teléfono, DPI de 13 a 20 dígitos, licencia, placa y contraseña fuerte (12 caracteres, mayúscula, minúscula, número y símbolo). La aplicación móvil debe pedir el permiso de ubicación; la API valida las coordenadas cuando el repartidor reporta su posición.

## Catálogo para la aplicación cliente

Se agregó el endpoint público:

- `GET /api/v1/catalog/payment-types`

El catálogo ya tenía búsqueda por texto y filtro de disponibilidad:

- `GET /api/v1/catalog/branches/{branchCode}/products?search=vitamina&onlyAvailable=true`

No se agregó un filtro por categoría porque la tabla de productos usada por esta API no tiene una categoría persistida. No se debe simular esa clasificación con datos inventados.

## Funcionalidades ya existentes y que no se duplicaron

- Promociones al crear pedidos y cálculo del descuento del lado del servidor.
- Recetas y productos que requieren receta.
- Perfil, direcciones e historial de pedidos del cliente.
- Cancelación de pedidos permitidos y restauración de inventario.
- Notificaciones push para asignación, cambios de estado y mensajes.
- Bot de atención al cliente con escalamiento a administración por tickets.
- Pagos PayPal/efectivo, comprobantes, facturación, cuentas por cobrar y liquidaciones al repartidor.
- Geolocalización, tracking y ruta gratuita basada en OpenStreetMap/OSRM.
- Reportes de ganancias, bitácora, proveedores, compras, productos e inventario.

## Decisiones para no duplicar movimientos contables o inventario

En la implementación actual, **registrar una compra ya recibe la compra y aumenta inventario**. Por eso no se añadió un endpoint `receive` que pudiera sumar existencias por segunda vez.

De igual manera, **crear una devolución administrativa ya aplica el crédito y devuelve existencias**. Agregar "aprobar/rechazar" sin convertir primero las devoluciones en solicitudes pendientes ocasionaría movimientos duplicados. Si se desea un flujo de aprobación, debe diseñarse como una fase posterior con nuevos estados y reglas contables, no como un endpoint superficial.

## Configuración de Azure relevante

Mantenga secretos en Application Settings o Key Vault, nunca en `appsettings.json` ni GitHub:

- `ConnectionStrings__BioRedConnection`
- `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`
- `ReceiptEmail__Enabled=true`
- `ReceiptEmail__Host`, `ReceiptEmail__Port`, `ReceiptEmail__Security`
- `ReceiptEmail__UserName`, `ReceiptEmail__Password`
- `ReceiptEmail__FromAddress`, `ReceiptEmail__FromName`
- `DeliveryConfirmation__Enabled=true`
- `DeliveryConfirmation__CodeLifetimeMinutes=1440`
- `DeliveryConfirmation__MaxAttempts=5`
- PayPal, Firebase y demás claves existentes.

Para SignalR desde una web, reemplace `Cors:AllowedOrigins` por las URLs reales de las aplicaciones; no use `*` cuando la web use credenciales. Las aplicaciones móviles se conectan con el token JWT en `access_token`.

## Prueba final recomendada

1. Cree un pedido con una cuenta de cliente y confirme que llega el correo con el PIN.
2. Inicie sesión como administrador y asigne un repartidor.
3. Como repartidor, cambie el pedido a `En Preparación` y luego `En Camino`.
4. Abra la conversación desde ambas aplicaciones, conéctelas al hub y envíe un mensaje por el POST REST. La otra pantalla debe recibir `orderMessageCreated` sin actualizar manualmente.
5. Ingrese un PIN incorrecto para comprobar que se descuenta un intento.
6. Ingrese el PIN recibido desde el endpoint `verify`; la respuesta debe indicar productos, total, `isPaid` y `amountToCollect`.
7. Intente marcar el pedido como `Entregado` sin validar el PIN en otro pedido nuevo: debe devolver 409.
8. Después de validar el PIN, marque el pedido como `Entregado`: debe responder 200.
9. Pruebe el registro móvil de repartidor con una contraseña débil y luego con una fuerte; la primera debe devolver 400 y la segunda 201.
10. Consulte `GET /api/v1/catalog/payment-types` sin token.
