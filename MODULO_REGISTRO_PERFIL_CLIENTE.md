# Módulo de registro y perfil del cliente

Este módulo permite que un cliente se registre directamente desde la aplicación, inicie sesión y administre sus datos personales básicos.

## Preparación

Ejecute una sola vez en SQL Server:

`SQL/06_Preparar_Perfil_Cliente.sql`

El script no inserta clientes de prueba. Valida que no existan correos, documentos o cuentas duplicadas y crea los índices únicos necesarios.

## Endpoints

| Acción | Método | Endpoint | Autenticación |
|---|---|---|---|
| Registrar cliente | POST | `/api/v1/clients/register` | Pública |
| Consultar perfil | GET | `/api/v1/clients/me` | Bearer de CLIENTE |
| Actualizar perfil | PUT | `/api/v1/clients/me` | Bearer de CLIENTE |

## Registro

Ejemplo:

```json
{
  "branchCode": "SUC-ADE-01",
  "firstName": "Cliente",
  "lastName": "Prueba",
  "document": "DPI-PRUEBA-001",
  "phone": "55550001",
  "email": "cliente.perfil.prueba@biored.com"
}
```

La sucursal es obligatoria porque la tabla actual `Clientes` requiere `cod_sucursal`. La aplicación puede enviar la sucursal seleccionada o la más cercana al cliente.

La API crea el registro de `Clientes` y su `Cuentas_Acceso` dentro de una sola transacción. Si alguna parte falla, no guarda registros incompletos.

La API genera una contraseña temporal, guarda únicamente su hash de ASP.NET Core Identity y envía el texto temporal al correo. Vence en 24 horas y debe cambiarse antes de obtener un token.

## Inicio de sesión

Después del registro, copie la contraseña recibida por correo y cámbiela mediante `POST /api/v1/auth/change-temporary-password`. Después utilice:

`POST /api/v1/auth/login`

```json
{
  "userNameOrEmail": "cliente.perfil.prueba@biored.com",
  "password": "NuevaClaveSegura#2026"
}
```

Copie el `accessToken` obtenido y utilícelo como Bearer Token para consultar o actualizar el perfil.

## Actualización del perfil

```json
{
  "firstName": "Cliente Actualizado",
  "lastName": "Prueba",
  "phone": "55550002"
}
```

El cliente solamente puede cambiar su nombre, apellido y teléfono. El correo, documento, código, sucursal, estado y contraseña no se aceptan en este endpoint, lo que evita cambios sensibles involuntarios.

## Validaciones principales

- El correo debe tener formato válido y no puede repetirse.
- El documento no puede repetirse.
- La sucursal debe existir y estar activa.
- La contraseña definitiva debe tener por lo menos 12 caracteres, mayúscula, minúscula, número y carácter especial.
- `GET /me` y `PUT /me` solo permiten un Bearer Token con rol `CLIENTE`.
- Las respuestas nunca incluyen `password` ni `passwordHash`.
