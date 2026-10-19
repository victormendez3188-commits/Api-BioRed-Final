# Bot de Atención al Cliente

## Qué se agregó

El bot es gratuito y funciona con respuestas predefinidas dentro de los tickets de atención. No usa una cuenta de OpenAI, Firebase ni otro servicio externo.

Cuando un cliente crea un ticket o envía un mensaje nuevo, la API guarda primero el mensaje y luego intenta responder automáticamente. Si el bot tuviera un problema, el ticket y el mensaje del cliente seguirán guardados normalmente.

El bot puede orientar sobre:

- estado y seguimiento de pedidos;
- pagos, PayPal y efectivo;
- facturas y comprobantes;
- cancelaciones, devoluciones y reembolsos;
- registro de cuenta;
- recuperación de acceso;
- mensajes generales de ayuda.

Para evitar riesgos, no responde sobre dosis, recetas ni recomendaciones médicas. Esas consultas se dirigen a un farmacéutico o profesional de salud.

## Caso: no puedo iniciar sesión

Hay dos casos distintos:

1. **El cliente ya tiene una cuenta:** desde la pantalla de inicio de sesión debe elegir **“Olvidé mi contraseña”**. La aplicación llama a `POST /api/v1/auth/forgot-password` con el correo registrado. Luego, con la contraseña temporal enviada al correo, llama a `POST /api/v1/auth/change-temporary-password` para definir la nueva contraseña.
2. **El cliente no tiene una cuenta:** debe registrarse primero con `POST /api/v1/clients/register`. Sin una cuenta no puede iniciar sesión, crear pedidos ni abrir tickets personales. La pantalla de inicio debe mostrar claramente el botón **“Crear cuenta”** junto al botón de recuperación de contraseña.

No se revela desde la API si un correo existe o no: la aplicación debe mostrar un mensaje neutral, por ejemplo: **“Si el correo está registrado, recibirás las instrucciones para recuperar tu acceso.”**

## Instalación

1. En SQL Server, ejecuta `SQL/19_Preparar_Bot_Atencion_Cliente.sql` una sola vez.
2. Ejecuta `SQL/19B_Verificar_Bot_Atencion_Cliente.sql`. Debe indicar `cuenta_bot_existe = 1`.
3. Compila y publica la API.
4. En Azure App Service deja esta configuración:

```json
"SupportBot": {
  "Enabled": true,
  "BotReferenceId": "BOT-SOPORTE",
  "DisplayName": "Asistente BioRed"
}
```

Para desactivar las respuestas automáticas sin afectar los tickets, cambia `SupportBot__Enabled` a `false` en Azure y reinicia la aplicación.

La cuenta interna `BOT-SOPORTE` queda deshabilitada para inicio de sesión. Solo sirve para identificar los mensajes automáticos dentro de la base de datos.

## Prueba rápida

Inicia sesión como cliente y crea un ticket con:

```json
{
  "subject": "No puedo iniciar sesión",
  "category": "CUENTA",
  "message": "Olvidé mi contraseña y no puedo entrar."
}
```

Después consulta `GET /api/v1/support/tickets/{ticketId}/messages`. Deben aparecer el mensaje del cliente y una respuesta de **Asistente BioRed**.

También prueba mensajes como:

- `¿Cuál es el estado de mi pedido?` (creando el ticket desde un pedido propio);
- `¿Cómo cancelo un pedido?`;
- `Necesito mi factura o comprobante`;
- `¿Cómo solicito una devolución?`;
- `No tengo cuenta, ¿cómo compro?`.

El ticket siempre requiere que el cliente esté autenticado. La ayuda para alguien sin cuenta se muestra directamente en la pantalla de acceso y registro, no en un ticket.
