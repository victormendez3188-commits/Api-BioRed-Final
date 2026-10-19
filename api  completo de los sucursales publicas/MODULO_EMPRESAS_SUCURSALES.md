# Módulo de empresas y sucursales

## Preparación

Ejecute `SQL/08_Preparar_Empresas_Sucursales.sql` una sola vez en Azure SQL.
El script protege los correos, valida las coordenadas y agrega reglas para las
tarifas y la cobertura de entrega.

Todos los endpoints requieren una cuenta con rol `ADMIN`.

## Empresas

- `GET /api/v1/management/companies`
- `GET /api/v1/management/companies/{companyCode}`
- `POST /api/v1/management/companies`
- `PUT /api/v1/management/companies/{companyCode}`
- `DELETE /api/v1/management/companies/{companyCode}`

### Crear empresa de prueba

```json
{
  "companyCode": "EMP-TEST-01",
  "companyName": "Farmacia Empresa de Prueba",
  "address": "Zona 1, Ciudad de Guatemala",
  "phone": "22220001",
  "email": "empresa.prueba@biored.com"
}
```

## Sucursales

- `GET /api/v1/management/branches`
- `GET /api/v1/management/branches/{branchCode}`
- `POST /api/v1/management/branches`
- `PUT /api/v1/management/branches/{branchCode}`
- `PUT /api/v1/management/branches/{branchCode}/delivery-configuration`
- `DELETE /api/v1/management/branches/{branchCode}`

El listado admite los parámetros `companyCode`, `search`, `includeInactive`,
`page` y `pageSize`. La versión 16 enlaza cada parámetro de consulta de forma
explícita para asegurar que el filtro por empresa funcione correctamente en
Swagger y Scalar.

### Crear sucursal de prueba

```json
{
  "branchCode": "SUC-TEST-01",
  "companyCode": "EMP-TEST-01",
  "branchName": "Sucursal de Prueba",
  "address": "Zona 1, Ciudad de Guatemala",
  "phone": "22220002",
  "email": "sucursal.prueba@biored.com",
  "latitude": 14.634915,
  "longitude": -90.506882
}
```

### Configurar entregas

```json
{
  "maximumRadiusKm": 15,
  "baseRate": 15,
  "extraKilometerRate": 2.5,
  "estimatedMinutes": 35,
  "active": true
}
```

La eliminación es lógica. Al desactivar una empresa se desactivan sus
sucursales y configuraciones de entrega. Al desactivar una sucursal también se
desactiva su configuración de entrega.
