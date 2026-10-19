# BioRed — Módulo 4: pagos, promociones y recetas

Este módulo completa el flujo comercial de los pedidos sin eliminar ni reemplazar
la información de los módulos anteriores.

## Correcciones verificadas en la versión v26

- PayPal Checkout incluye URL de retorno y cancelación para completar la
  aprobación desde Sandbox.
- Se aceptan los enlaces HATEOAS `approve` y `payer-action` devueltos por PayPal.
- La conciliación de reembolsos mantiene la igualdad contable entre el total de
  la devolución, el crédito aplicado y el reembolso pendiente.
- Los flujos de captura y reembolso fueron comprobados con idempotencia en
  PayPal Sandbox y la verificación SQL final no reportó inconsistencias.

## Orden de instalación

1. Ejecutar `SQL/11_Preparar_Pagos_Promociones_Recetas.sql` en SQL Server,
   seleccionando la base `BioRed`.
2. Configurar las credenciales de PayPal Sandbox con User Secrets.
3. Limpiar y recompilar la solución `BioRed.sln` en Visual Studio 2022.
4. Ejecutar la API y probar los endpoints desde Scalar.

## Configuración segura de PayPal Sandbox

Abrir una terminal dentro de la carpeta del proyecto `BioRed` y ejecutar:

```powershell
dotnet user-secrets set "PayPal:ClientId" "CLIENT_ID_DE_LA_APLICACION_SANDBOX"
dotnet user-secrets set "PayPal:ClientSecret" "CLIENT_SECRET_DE_LA_APLICACION_SANDBOX"
dotnet user-secrets set "PayPal:LocalCurrencyUnitsPerPayPalUnit" "7.70"
```

En desarrollo local, `appsettings.json` incluye las rutas de retorno y cancelación
para el perfil HTTPS de Visual Studio (`https://localhost:7248`). Si se publica la
API en Azure, configurar también `PayPal:ReturnUrl` y `PayPal:CancelUrl` con el
dominio HTTPS público de la aplicación.

No se deben escribir ni compartir las credenciales en `appsettings.json`, capturas,
repositorios o mensajes. La aplicación usa quetzales (`GTQ`) para la contabilidad
local y dólares (`USD`) para PayPal Sandbox. La tasa de conversión es configurable
y debe actualizarse antes de realizar una prueba.

## Funciones incluidas

### Pagos

- Crear una orden PayPal para un pedido del cliente.
- Capturar únicamente una orden aprobada por PayPal.
- Validar identificador, monto y moneda recibidos del proveedor.
- Evitar cobros duplicados mediante claves de idempotencia e índices únicos.
- Impedir que un pedido PayPal avance sin una captura completada.
- Confirmar efectivo contra entrega únicamente por un administrador o por el
  repartidor asignado, después de marcar el pedido como `Entregado`.
- Conciliar automáticamente el pago con la cuenta por cobrar.
- Procesar reembolsos PayPal o devoluciones de efectivo sin superar el pago original.

### Promociones

- Descuentos por porcentaje (`PERCENTAGE`) o monto fijo (`FIXED`).
- Vigencia en UTC, monto mínimo y descuento máximo opcional.
- Aplicación por empresa, sucursal o productos específicos.
- Límite total y límite de usos por cliente.
- Bloqueo de cambios en las reglas después del primer uso.
- Distribución exacta del descuento cuando se devuelve uno o varios productos.

### Recetas

- Carga de documentos PDF, JPEG o PNG de hasta 5 MB.
- Validación de extensión, firma del archivo y hash SHA-256.
- Flujo `PENDING`, `APPROVED`, `REJECTED`, `USED` y `EXPIRED`.
- Revisión exclusiva de administradores.
- Validación del cliente, productos, cantidades y fechas al crear el pedido.
- Una receta aprobada solamente puede utilizarse en un pedido.

## Endpoints agregados

| Método | Ruta | Rol |
| --- | --- | --- |
| GET | `/api/v1/payments/orders/{orderId}` | Propietario, repartidor asignado o administrador |
| POST | `/api/v1/payments/orders/{orderId}/paypal` | CLIENTE |
| POST | `/api/v1/payments/paypal/{providerOrderId}/capture` | CLIENTE |
| POST | `/api/v1/management/payments/orders/{orderId}/cash/confirm` | ADMIN o REPARTIDOR asignado |
| POST | `/api/v1/management/payments/refunds` | ADMIN |
| GET | `/api/v1/promotions/available` | CLIENTE |
| GET/POST/PUT/DELETE | `/api/v1/management/promotions` | ADMIN |
| GET/POST | `/api/v1/prescriptions` | CLIENTE |
| GET | `/api/v1/prescriptions/{prescriptionId}/document` | Propietario o ADMIN |
| GET/PATCH | `/api/v1/management/prescriptions` | ADMIN |

## Documento mínimo para una prueba controlada

El siguiente texto Base64 empieza con una firma PDF válida y puede utilizarse
únicamente para probar la validación técnica del endpoint:

```text
JVBERi0xLjQKJUVPRgo=
```

Usar `receta-prueba.pdf` como nombre y `application/pdf` como tipo de contenido.

## Reglas de integración importantes

- La factura se emite solamente cuando el pedido está `Entregado`.
- Si el pago se completó antes de emitir la factura, la cuenta por cobrar nace
  conciliada y con saldo cero.
- Los endpoints manuales de cuentas por cobrar no aceptan PayPal ni efectivo
  contra entrega; ambos deben pasar por el módulo formal de pagos.
- Una devolución de productos promocionados reembolsa lo realmente pagado, no
  el precio bruto anterior al descuento.
