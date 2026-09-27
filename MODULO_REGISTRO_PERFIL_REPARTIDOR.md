# Módulo de registro y perfil del repartidor

## Preparación

Ejecute `SQL/07_Preparar_Perfil_Repartidor.sql` una sola vez en Azure SQL.
El script valida duplicados, protege el DPI y la placa, y agrega la regla de
disponibilidad.

## Endpoints

- `POST /api/v1/drivers/register`: registro público del repartidor.
- `GET /api/v1/drivers/me`: consulta el perfil autenticado.
- `PUT /api/v1/drivers/me`: edita nombre, apellido, teléfono, vehículo y placa.
- `PATCH /api/v1/drivers/me/availability`: cambia entre `Disponible` y
  `Desconectado`.

El estado `Ocupado` es administrado por la API cuando el repartidor tiene un
pedido activo.

## Registro de prueba

```json
{
  "firstName": "Repartidor",
  "lastName": "Prueba",
  "document": "DPI-REP-PRUEBA-001",
  "phone": "55551001",
  "email": "repartidor.perfil.prueba@biored.com",
  "vehicleType": "Motocicleta",
  "licensePlate": "M-TEST-001"
}
```

La API envía una contraseña temporal al correo. Antes de iniciar sesión se debe reemplazar mediante `POST /api/v1/auth/change-temporary-password`.

## Inicio de sesión

```json
{
  "userNameOrEmail": "repartidor.perfil.prueba@biored.com",
  "password": "NuevaClaveSegura#2026"
}
```

## Actualización del perfil

```json
{
  "firstName": "Repartidor Actualizado",
  "lastName": "Prueba",
  "phone": "55551002",
  "vehicleType": "Motocicleta",
  "licensePlate": "M-TEST-001"
}
```

## Disponibilidad

```json
{
  "availability": "Disponible"
}
```
