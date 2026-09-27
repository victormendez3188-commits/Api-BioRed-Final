# Decisiones confirmadas para métodos de pago

Estas reglas se implementarán en el módulo de pagos del sistema BioRed.

## PayPal

- Se integrará la API oficial de PayPal en el entorno **Sandbox**.
- Se utilizarán credenciales de prueba configuradas de forma segura.
- La orden y la captura se procesarán realmente dentro de PayPal Sandbox.
- El cliente utilizará saldo o crédito de prueba; no se cobrará una tarjeta
  bancaria real.
- El sistema guardará los identificadores de PayPal y validará el resultado
  contra PayPal antes de marcar un pedido como pagado.
- No se simulará una respuesta exitosa dentro del código de BioRed.

## Pago en efectivo

- Será pago contra entrega.
- El pedido se crea con el pago pendiente.
- El cobro se registra cuando el repartidor confirma la entrega.
- Se guardarán fecha, monto, pedido y usuario o repartidor que confirmó el
  cobro para mantener trazabilidad.

## Seguridad

- Los secretos de PayPal no se guardarán en el repositorio ni en archivos
  públicos.
- Desarrollo utilizará Sandbox y producción utilizará credenciales reales
  independientes.
- Los importes confirmados se comprobarán en el servidor; no se confiará en
  valores enviados por la aplicación móvil o la página web.
