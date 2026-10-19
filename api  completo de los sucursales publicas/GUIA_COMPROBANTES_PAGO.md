# Comprobantes de pago de BioRed

Esta ampliación conserva únicamente dos métodos de entrega:

1. `DOWNLOAD_PDF`: descarga inmediata del comprobante en PDF.
2. `EMAIL`: envío del mismo PDF al correo registrado del cliente.

El comprobante se crea cuando un pago queda en estado `COMPLETED`. La entrega se solicita después; por lo tanto, una falla de correo no modifica el pago, no repite el cobro y se puede intentar nuevamente.

## Instalación

1. Ejecute `SQL/12_Preparar_Comprobantes_Pago.sql` en la base `BioRed`.
2. Recompile la solución.
3. Ejecute `SQL/12B_Verificar_Comprobantes_Pago.sql`. Las últimas tres consultas deben regresar cero filas.

## Configuración segura del correo

El PDF funciona sin configurar correo. Para habilitar `EMAIL`, guarde la configuración SMTP en los secretos del proyecto `BioRed`; no escriba la contraseña en `appsettings.json`.

```powershell
dotnet user-secrets set "ReceiptEmail:Enabled" "true"
dotnet user-secrets set "ReceiptEmail:Host" "SERVIDOR_SMTP"
dotnet user-secrets set "ReceiptEmail:Port" "587"
dotnet user-secrets set "ReceiptEmail:Security" "StartTls"
dotnet user-secrets set "ReceiptEmail:UserName" "USUARIO_SMTP"
dotnet user-secrets set "ReceiptEmail:Password" "CONTRASEÑA_O_APP_PASSWORD"
dotnet user-secrets set "ReceiptEmail:FromAddress" "CORREO_REMITENTE"
dotnet user-secrets set "ReceiptEmail:FromName" "BioRed"
```

Valores admitidos para `ReceiptEmail:Security`: `Auto`, `StartTls`, `SslOnConnect` y `None`. En producción se debe usar una opción cifrada.

## Uso en Scalar

Primero consulte:

```text
GET /api/v1/payments/orders/{orderId}/receipt
```

La respuesta muestra el comprobante y las dos alternativas disponibles. Para elegir una, ejecute:

```text
POST /api/v1/payments/orders/{orderId}/receipt
```

Para descargar:

```json
{
  "deliveryMethod": "DOWNLOAD_PDF"
}
```

Para enviar al correo registrado:

```json
{
  "deliveryMethod": "EMAIL"
}
```

No se acepta una dirección en la petición: el servidor usa exclusivamente el correo registrado del cliente. Un envío ya confirmado al mismo correo es idempotente y no genera mensajes duplicados.
