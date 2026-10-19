# Configuración de BioRed con Azure SQL

Para la publicación final, siga también `REVISION_FINAL_SEGURIDAD_PRUEBAS_AZURE.md`.

La API fue adaptada para las tablas nuevas de autenticación, permisos y
geolocalización. Los datos sensibles no se guardan en `appsettings.json`.

Durante la integración del equipo, Scalar/OpenAPI está habilitado también en
Azure y CORS permite temporalmente cualquier origen. Antes de la publicación
definitiva, cambie CORS al dominio exacto de la Web.

## 1. Abrir la solución

Abra `BioRed/BioRed.sln` con Visual Studio 2022.

## 2. Configurar la conexión a Azure SQL

Antes de iniciar la API, ejecute `SQL/02_Preparar_Datos_API.sql` conectado
directamente a `BioRed`. El script no crea tablas: agrega permisos, configuración
inicial de entrega e índices para las consultas de ubicación.

Desde una consola ubicada en la carpeta `BioRed`, ejecute:

```powershell
dotnet user-secrets set "ConnectionStrings:BioRedConnection" "Server=tcp:SERVIDOR.database.windows.net,1433;Initial Catalog=BioRed;User ID=USUARIO;Password=CONTRASENA;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```

Reemplace `SERVIDOR`, `USUARIO` y `CONTRASENA` por los datos de Azure. No suba
esa cadena a GitHub.

Configure también una clave JWT de 32 bytes o más:

```powershell
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()
$jwtKey = [Convert]::ToBase64String($bytes)
dotnet user-secrets set "Jwt:Key" $jwtKey
```

## 3. Cuenta inicial

La autenticación consulta `Cuentas_Acceso`. Cada registro debe apuntar mediante
`referencia_id` a un código existente en `Usuarios`, `Clientes` o
`Repartidores`.

Para crear automáticamente la cuenta de un empleado existente durante el
desarrollo, configure:

```powershell
dotnet user-secrets set "AdminSeed:Enabled" "true"
dotnet user-secrets set "AdminSeed:ReferenceId" "CODIGO_DEL_EMPLEADO"
dotnet user-secrets set "AdminSeed:Email" "admin@correo.com"
dotnet user-secrets set "AdminSeed:Password" "ClaveSegura#2026"
```

Después del primer inicio correcto, desactive el sembrado:

```powershell
dotnet user-secrets set "AdminSeed:Enabled" "false"
```

## 4. Endpoints principales

- `GET /api/v1/health/database`: comprueba la conexión con Azure SQL.
- `POST /api/v1/auth/login`: inicia sesión por correo o usuario de empleado.
- `POST /api/v1/auth/refresh`: rota el refresh token.
- `GET /api/v1/auth/me`: devuelve la cuenta autenticada.
- `POST /api/v1/accounts`: crea acceso para un empleado, cliente o repartidor existente.
- `POST /api/v1/clients/register`: registra públicamente una cuenta de cliente.
- `GET /api/v1/clients/me`: consulta el perfil del cliente autenticado.
- `PUT /api/v1/clients/me`: actualiza nombre, apellido y teléfono del cliente.
- `POST /api/v1/drivers/register`: registra públicamente una cuenta de repartidor.
- `GET /api/v1/drivers/me`: consulta el perfil del repartidor autenticado.
- `PUT /api/v1/drivers/me`: actualiza los datos y el vehículo del repartidor.
- `PATCH /api/v1/drivers/me/availability`: cambia la disponibilidad del repartidor.
- `GET/POST /api/v1/management/companies`: lista y crea empresas.
- `GET/PUT/DELETE /api/v1/management/companies/{companyCode}`: consulta, actualiza o desactiva una empresa.
- `GET/POST /api/v1/management/branches`: lista y crea sucursales.
- `GET/PUT/DELETE /api/v1/management/branches/{branchCode}`: consulta, actualiza o desactiva una sucursal.
- `PUT /api/v1/management/branches/{branchCode}/delivery-configuration`: crea o actualiza cobertura y tarifas.
- `GET/POST /api/v1/management/suppliers`: lista y crea proveedores.
- `GET/PUT/DELETE /api/v1/management/suppliers/{supplierCode}`: consulta, actualiza o desactiva proveedores.
- `GET/POST /api/v1/management/purchases`: lista compras o registra una compra con actualización transaccional de inventario, lotes y Kardex.
- `GET /api/v1/management/purchases/{purchaseId}`: consulta el encabezado y los detalles de una compra.
- `POST /api/v1/management/purchases/{purchaseId}/cancel`: anula la compra y revierte las existencias cuando todavía es posible.
- `GET/POST /api/v1/management/invoices`: consulta facturas o emite una desde un pedido sin descontar inventario dos veces.
- `GET /api/v1/management/invoices/{invoiceId}`: consulta la factura, su detalle y el saldo por cobrar.
- `GET/POST /api/v1/management/returns`: consulta devoluciones o registra una devolución con reposición de inventario.
- `GET /api/v1/management/returns/{returnId}`: consulta el detalle de una devolución.
- `GET /api/v1/management/receivables`: consulta cuentas por cobrar y sus saldos.
- `GET /api/v1/management/receivables/{receivableId}`: consulta movimientos de una cuenta por cobrar.
- `POST /api/v1/management/receivables/{receivableId}/payments`: registra efectivo únicamente para pedidos entregados; PayPal requiere captura verificada.
- `GET/POST /api/v1/management/payables`: consulta cuentas por pagar o crea una desde una compra registrada.
- `GET /api/v1/management/payables/{payableId}`: consulta los pagos y el saldo de una cuenta por pagar.
- `POST /api/v1/management/payables/{payableId}/payments`: registra un abono al proveedor.
- `GET /api/v1/geolocation/branches/nearby`: busca sucursales cercanas.
- `POST /api/v1/geolocation/drivers/location`: actualiza la ubicación del repartidor.
- `GET /api/v1/geolocation/orders/{id}/tracking`: consulta el seguimiento autorizado.
- `POST /api/v1/orders`: crea un pedido para el cliente autenticado, valida cobertura y stock, calcula los importes, descuenta inventario y registra detalle, Kardex e historial.
- `GET /api/v1/orders`: devuelve una lista paginada de pedidos. Acepta `status`, `page` y `pageSize`; cada cuenta recibe únicamente los pedidos que puede consultar.
- `GET /api/v1/orders/{id}`: devuelve el detalle completo del pedido, productos, dirección, forma de pago, historial y última ubicación registrada.
- `PATCH /api/v1/orders/{id}/status`: aplica transiciones controladas del pedido; el repartidor asignado puede iniciar la ruta y entregar, y al entregar queda disponible automáticamente.
- `PUT /api/v1/orders/{id}/driver`: permite a una cuenta con `PEDIDO_VER` asignar un repartidor disponible y cambia el pedido a `Aceptado`.
- `GET /api/v1/catalog/branches/{branchCode}/products`: catálogo público paginado por sucursal, con búsqueda y filtro de disponibilidad.
- `GET /api/v1/catalog/branches/{branchCode}/products/{productCode}`: detalle público del producto, precio de venta y existencias de la sucursal.
- `GET /api/v1/clients/me/addresses`: lista las direcciones activas del cliente autenticado.
- `GET /api/v1/clients/me/addresses/{id}`: consulta una dirección propia.
- `POST /api/v1/clients/me/addresses`: registra una dirección; la primera se marca como predeterminada automáticamente.
- `PUT /api/v1/clients/me/addresses/{id}`: actualiza una dirección propia.
- `PATCH /api/v1/clients/me/addresses/{id}/default`: selecciona la dirección predeterminada.
- `DELETE /api/v1/clients/me/addresses/{id}`: desactiva la dirección sin borrar el historial.

Para probar la creación de pedidos, ejecute una sola vez
`SQL/03_Datos_Prueba_Crear_Pedido.sql`. Ese script agrega únicamente un producto
de prueba y su inventario; no crea ni elimina tablas.

## 5. Probar

Recompile la solución. Luego ejecute la API y abra:

```text
/scalar/v1
```

Primero pruebe `GET /api/v1/health/database`. Debe responder `200 OK` y
`status: Healthy`.
