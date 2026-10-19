# Asociación dinámica de farmacias con BioRed

BioRed nunca se conecta directamente a la base de datos de una farmacia. Cada farmacia conserva su información y BioRed se comunica únicamente con su API mediante HTTPS.

La base de datos de BioRed guarda solamente la solicitud de asociación, la URL pública de la API y el estado de aprobación. El proyecto no incluye ninguna farmacia asociada de forma predeterminada.

## Preparación

Ejecute una sola vez en SQL Server:

`SQL/04_Integraciones_Farmacias.sql`

El script crea `dbo.Integraciones_Farmacia` sin insertar FarmaciaDemo ni otra asociación.

## Flujo para la demostración

### 1. La farmacia solicita asociarse

Este endpoint es público porque la farmacia todavía no posee acceso a BioRed:

`POST /api/v1/pharmacy-associations`

Ejemplo para la demostración:

```json
{
  "companyCode": "EMP-ADEPH",
  "pharmacyName": "FarmaciaDemo",
  "apiBaseUrl": "https://farmaciademo-api-gbhqecd5hvdaheha.westus3-01.azurewebsites.net/",
  "contactName": "Responsable de FarmaciaDemo",
  "contactEmail": "contacto@farmaciademo.com"
}
```

BioRed devuelve `201 Created` y deja la solicitud en estado `Pendiente`. Guarde el valor `associationId`.

### 2. El administrador consulta las solicitudes

Inicie sesión como administrador y coloque su Bearer Token:

`GET /api/v1/pharmacy-associations`

### 3. BioRed valida la API externa

Con el Bearer Token del administrador:

`POST /api/v1/pharmacy-associations/{associationId}/validate`

BioRed comprueba por HTTPS que la API responda correctamente a `GET /api/Productos` y `GET /api/Inventario`. No accede a la base de datos externa.

### 4. El administrador aprueba la asociación

`PATCH /api/v1/pharmacy-associations/{associationId}/approve`

Solo se puede aprobar una solicitud pendiente cuya conexión ya fue validada. El estado cambia a `Activa`.

### 5. BioRed utiliza la asociación

Después de aprobarla, los endpoints existentes resuelven la URL desde la base de datos de BioRed:

| Acción | Método | Endpoint de BioRed |
|---|---|---|
| Listar productos | GET | `/api/v1/integrations/pharmacies/{companyCode}/products` |
| Consultar producto | GET | `/api/v1/integrations/pharmacies/{companyCode}/products/{productCode}` |
| Listar inventario | GET | `/api/v1/integrations/pharmacies/{companyCode}/inventory` |
| Consultar existencias | GET | `/api/v1/integrations/pharmacies/{companyCode}/inventory/{productCode}` |
| Enviar pedido | POST | `/api/v1/integrations/pharmacies/{companyCode}/orders` |
| Consultar pedido | GET | `/api/v1/integrations/pharmacies/{companyCode}/orders/{orderCode}` |

### 6. Suspender la asociación

`PATCH /api/v1/pharmacy-associations/{associationId}/suspend`

Una integración suspendida deja de ser utilizada inmediatamente por BioRed.

## Datos que BioRed no almacena

- Cadena de conexión de la farmacia.
- Usuario o contraseña de su base de datos.
- Acceso a sus tablas.
- Copia directa de su base de datos.

En producción, la API de cada farmacia también deberá exigir una credencial propia, como API Key u OAuth. Esa credencial deberá manejarse mediante un almacén seguro y nunca escribirse directamente en el proyecto.
