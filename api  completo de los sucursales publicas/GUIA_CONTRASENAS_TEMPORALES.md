# Contraseñas temporales para usuarios nuevos

Este módulo aplica a cuentas nuevas de **Cliente**, **Repartidor** y **Empleado**.
La API genera una contraseña aleatoria de 16 caracteres, guarda únicamente su hash y envía la contraseña temporal al correo registrado. La contraseña vence después de 24 horas.

## 1. Preparar SQL Server

1. Abra `SQL/14_Preparar_Contrasenas_Temporales.sql` en SSMS.
2. Confirme que la base seleccionada sea `BioRed`.
3. Ejecute todo el script.
4. Debe devolver valores mayores que cero en las tres columnas de comprobación.
5. Ejecute `SQL/14B_Verificar_Contrasenas_Temporales.sql`.
6. La primera consulta debe devolver `1` en sus cuatro columnas.

Las cuentas existentes se conservan con `requiere_cambio_password = 0`; no se obliga a cambiar sus contraseñas.

## 2. Compilar y ejecutar

1. En Visual Studio 2022, seleccione **Compilar > Recompilar solución**.
2. El resultado esperado es `4 correctos, 0 con errores`.
3. Ejecute BioRed y abra Scalar.

Se reutiliza la configuración `ReceiptEmail` guardada en User Secrets. No agregue la contraseña de aplicación de Gmail a `appsettings.json` ni al ZIP.

## 3. Registrar un cliente nuevo

Use `POST /api/v1/clients/register`. El cuerpo ya no recibe `password`:

```json
{
  "branchCode": "SUC-ADE-01",
  "firstName": "Prueba",
  "lastName": "Temporal",
  "document": "DOC-TEMP-001",
  "phone": "55550001",
  "email": "correo.real@gmail.com"
}
```

El resultado esperado es `201 Created`, `passwordChangeRequired: true`, `temporaryPasswordEmailStatus: "ENVIADO"` y la llegada del correo con la contraseña temporal.

Para repartidores use `POST /api/v1/drivers/register`; tampoco envíe el campo `password`.

Para crear una cuenta asociada a un empleado, cliente o repartidor existente, el administrador usa `POST /api/v1/accounts`:

```json
{
  "email": "correo.real@gmail.com",
  "userType": "Empleado",
  "referenceId": "USR-PRUEBA"
}
```

## 4. Comprobar el bloqueo inicial

Pruebe `POST /api/v1/auth/login` con el correo y la contraseña recibida. Debe responder `403 Forbidden` con el código:

```json
{
  "code": "PASSWORD_CHANGE_REQUIRED"
}
```

No se entrega JWT mientras la contraseña temporal siga pendiente.

## 5. Cambiar la contraseña temporal

Use `POST /api/v1/auth/change-temporary-password` sin Bearer token:

```json
{
  "email": "correo.real@gmail.com",
  "temporaryPassword": "LA-CLAVE-RECIBIDA",
  "newPassword": "NuevaClaveSegura@2026"
}
```

La nueva contraseña debe tener al menos 12 caracteres e incluir mayúscula, minúscula, número y carácter especial. Después del `200 OK`, inicie sesión con la nueva contraseña; ahora sí debe recibir los tokens.

## 6. Reemitir una contraseña vencida

Un administrador autenticado usa:

`POST /api/v1/accounts/{accountId}/temporary-password`

No lleva cuerpo. La operación invalida la contraseña anterior, revoca los refresh tokens activos, crea otra contraseña con 24 horas de vigencia y la envía al mismo correo.

## Resultados de seguridad

- La contraseña temporal en texto solo existe durante la generación y el envío SMTP.
- La base guarda exclusivamente el hash de Identity.
- La contraseña temporal vence en 24 horas.
- No se emite JWT hasta completar el cambio.
- El reenvío administrativo invalida credenciales y refresh tokens anteriores.
- El servidor nunca devuelve la contraseña temporal en JSON ni la escribe en logs.
