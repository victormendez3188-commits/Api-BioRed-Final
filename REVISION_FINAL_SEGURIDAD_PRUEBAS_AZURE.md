# Revisión final de seguridad, pruebas y despliegue en Azure

## Qué archivo utilizar

`BioRed-API-v31-AZURE-COLABORACION.zip` es el código fuente final para abrirlo en Visual Studio.

No se debe subir ese ZIP directamente a Azure App Service. Primero se debe generar `BioRed-Azure-Deploy-v31.zip`, que contiene únicamente la salida publicada de la API.

## Seguridad revisada

- JWT valida firma, emisor, audiencia y vencimiento sin tolerancia adicional.
- Los refresh tokens se guardan mediante hash y se rotan al usarlos.
- Las contraseñas se almacenan mediante ASP.NET Core Identity.
- Las contraseñas temporales son aleatorias, vencen en 24 horas y nunca se devuelven en JSON.
- El cambio temporal es obligatorio antes de emitir un JWT.
- El reenvío administrativo requiere rol `ADMIN` y revoca los refresh tokens activos.
- Login, refresh y cambio temporal permiten como máximo 10 solicitudes por minuto por IP.
- Los registros públicos permiten como máximo 5 solicitudes cada 10 minutos por IP.
- OpenAPI y Scalar se publican cuando `ApiDocumentation:Enabled` es `true`, incluso en Azure, para que el equipo pueda consultar y probar la API.
- Producción utiliza HSTS, HTTPS, respuesta segura para errores y encabezados defensivos.
- CORS permite temporalmente cualquier origen para facilitar la integración de la Web durante el desarrollo. Esto no elimina JWT, permisos ni rate limiting.
- `appsettings.json` no contiene contraseñas, conexión, clave JWT ni credenciales PayPal.

## Configuración obligatoria en Azure App Service

En **Environment variables / Application settings**, configure los valores secretos. Use doble guion bajo en lugar de dos puntos:

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
Jwt__Key=CLAVE_BASE64_DE_32_BYTES_O_MAS
Jwt__Issuer=BioRed.Api
Jwt__Audience=BioRed.Clients
Jwt__AccessTokenMinutes=15
Jwt__RefreshTokenDays=7
AdminSeed__Enabled=false
ApiDocumentation__Enabled=true
Cors__AllowedOrigins__0=*

ReceiptEmail__Enabled=true
ReceiptEmail__Host=smtp.gmail.com
ReceiptEmail__Port=587
ReceiptEmail__Security=StartTls
ReceiptEmail__UserName=CORREO_DE_BIORED
ReceiptEmail__Password=CONTRASENA_DE_APLICACION_DE_GMAIL
ReceiptEmail__FromAddress=CORREO_DE_BIORED
ReceiptEmail__FromName=BioRed

PayPal__BaseUrl=https://api-m.sandbox.paypal.com
PayPal__ClientId=CLIENT_ID_SANDBOX
PayPal__ClientSecret=CLIENT_SECRET_SANDBOX
PayPal__ReturnUrl=https://SU_APP.azurewebsites.net/api/v1/payments/paypal/return
PayPal__CancelUrl=https://SU_APP.azurewebsites.net/api/v1/payments/paypal/cancel
PayPal__LocalCurrency=GTQ
PayPal__PayPalCurrency=USD
PayPal__LocalCurrencyUnitsPerPayPalUnit=7.70
```

Configure la conexión de una de estas dos maneras:

- En **Connection strings**, nombre `BioRedConnection` y tipo `SQLAzure`.
- O en **Application settings**, clave `ConnectionStrings__BioRedConnection`.

Mientras el equipo integra la Web desde diferentes equipos o dominios, mantenga:

```text
Cors__AllowedOrigins__0=*
```

Cuando la dirección definitiva de la Web ya exista, reemplace el asterisco por su origen exacto:

```text
Cors__AllowedOrigins__0=https://SU_PAGINA_WEB
```

No agregue `/` al final del origen. La aplicación móvil nativa no depende de CORS, pero la Web ejecutada en un navegador sí.

Como esta API ya configura CORS en `Program.cs`, deje vacía la sección CORS del portal de Azure para evitar dos configuraciones diferentes.

## Crear el ZIP correcto para Azure

1. Extraiga `BioRed-API-v31-AZURE-COLABORACION.zip`.
2. Abra la carpeta extraída.
3. Haga clic derecho dentro de la carpeta y seleccione **Abrir en Terminal**.
4. Ejecute:

```powershell
powershell -ExecutionPolicy Bypass -File .\Crear-Paquete-Azure.ps1
```

El proceso debe terminar con el mensaje `Paquete creado correctamente` y generará:

```text
BioRed-Azure-Deploy-v31.zip
```

Ese es el archivo que se carga mediante ZIP Deploy en Azure App Service.

## Comprobaciones después del despliegue

1. `GET /api/v1/health/database` debe responder `200 OK` y `Healthy`.
2. `/openapi/v1.json` debe responder `200 OK` y `/scalar/v1` debe abrir la documentación en Azure.
3. El login del administrador debe responder `200 OK`.
4. `GET /api/v1/reports/dashboard` con Bearer ADMIN debe responder `200 OK`.
5. `GET /api/v1/geolocation/orders/{orderId}/position` debe responder `200 OK` para una cuenta autorizada.
6. Registre un cliente de prueba con un correo real y confirme la llegada de la contraseña temporal.
7. El primer login temporal debe responder `403 PASSWORD_CHANGE_REQUIRED`.
8. El cambio de contraseña temporal debe responder `200 OK`.
9. El login con la contraseña definitiva debe responder `200 OK`.
10. Pruebe un comprobante de pago y confirme que llegue el PDF por correo.

## Archivos antiguos

Conserve V30 como respaldo hasta que V31 compile y funcione en Azure. Después de comprobar el despliegue, puede eliminar V1 a V30 y conservar únicamente:

- `BioRed-API-v31-AZURE-COLABORACION.zip`
- `BioRed-Azure-Deploy-v31.zip`
