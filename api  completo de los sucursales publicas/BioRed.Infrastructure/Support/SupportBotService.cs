using System.Globalization;
using System.Text;
using BioRed.Application.Support;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BioRed.Infrastructure.Support;

/// <summary>
/// Asistente gratuito basado en reglas. Responde preguntas frecuentes y usa
/// información del pedido solamente cuando el ticket ya pertenece al cliente.
/// No entrega diagnósticos, dosis ni recomendaciones médicas.
/// </summary>
public sealed class SupportBotService : ISupportBotService
{
    private readonly BioRedDbContext _dbContext;
    private readonly SupportBotOptions _options;
    private readonly ILogger<SupportBotService> _logger;

    public SupportBotService(
        BioRedDbContext dbContext,
        IOptions<SupportBotOptions> options,
        ILogger<SupportBotService> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> TryReplyAsync(
        long ticketId,
        int clientAccountId,
        string clientMessage,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled ||
            ticketId <= 0 ||
            clientAccountId <= 0 ||
            string.IsNullOrWhiteSpace(clientMessage))
        {
            return false;
        }

        try
        {
            var botAccount = await _dbContext.CuentasAcceso
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.TipoUsuario == "Empleado" &&
                            item.ReferenciaId == _options.BotReferenceId,
                    cancellationToken);

            if (botAccount is null)
            {
                _logger.LogWarning(
                    "El bot de atención está habilitado, pero no existe su cuenta interna. TicketId: {TicketId}.",
                    ticketId);
                return false;
            }

            var ticket = await _dbContext.TicketsAtencion
                .SingleOrDefaultAsync(
                    item => item.IdTicket == ticketId &&
                            item.IdCuentaCliente == clientAccountId,
                    cancellationToken);

            if (ticket is null || ticket.Estado == "CERRADO")
            {
                return false;
            }

            var answer = await BuildAnswerAsync(ticket, clientMessage, cancellationToken);
            if (string.IsNullOrWhiteSpace(answer))
            {
                return false;
            }

            var nowUtc = DateTime.UtcNow;
            _dbContext.MensajesTicketAtencion.Add(new MensajeTicketAtencion
            {
                IdTicket = ticket.IdTicket,
                IdCuentaRemitente = botAccount.IdCuenta,
                Contenido = $"{_options.DisplayName}: {answer}",
                FechaEnvioUtc = nowUtc
            });

            ticket.FechaActualizacionUtc = nowUtc;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // El mensaje del cliente ya fue guardado antes de llamar al bot.
            // No registramos el contenido del chat para preservar privacidad.
            _logger.LogWarning(
                exception,
                "No fue posible generar la respuesta automática del ticket {TicketId}.",
                ticketId);
            return false;
        }
    }

    private async Task<string> BuildAnswerAsync(
        TicketAtencion ticket,
        string clientMessage,
        CancellationToken cancellationToken)
    {
        var message = Normalize(clientMessage);

        if (ContainsAny(message,
                "DOSIS", "RECETA", "MEDICAMENTO", "EFECTO SECUNDARIO",
                "PUEDO TOMAR", "ENFERMEDAD", "SINTOMA"))
        {
            return "Por seguridad no puedo indicar dosis ni dar recomendaciones médicas. " +
                   "Consulta a un farmacéutico o a un profesional de salud.";
        }

        if (ContainsAny(message,
                "CONTRASENA", "PASSWORD", "INICIAR SESION", "NO PUEDO ENTRAR",
                "NO PUEDO ACCEDER", "OLVIDE MI CLAVE", "OLVIDE LA CLAVE"))
        {
            return "Si no puedes iniciar sesión, usa la opción ‘Olvidé mi contraseña’ " +
                   "en la pantalla de acceso e ingresa tu correo registrado. " +
                   "Recibirás una contraseña temporal para crear una nueva. " +
                   "Si todavía no tienes cuenta, debes registrarte antes de realizar pedidos.";
        }

        if (ContainsAny(message,
                "REGISTRAR", "REGISTRO", "CREAR CUENTA", "NO TENGO CUENTA"))
        {
            return "Para realizar pedidos necesitas crear una cuenta de cliente desde la aplicación. " +
                   "Después podrás agregar dirección, elegir productos y consultar tus pedidos.";
        }

        if (ContainsAny(message,
                "REEMBOLSO", "DEVOLUCION", "DEVOLVER", "DINERO DE VUELTA"))
        {
            return "Para solicitar un reembolso o devolución, selecciona el pedido correspondiente " +
                   "y explica el motivo. Un administrador revisará el caso antes de procesarlo.";
        }

        if (ContainsAny(message,
                "FACTURA", "RECIBO", "COMPROBANTE"))
        {
            return "Puedes consultar el comprobante del pago desde el detalle de tu pedido. " +
                   "Si no aparece, indícanos el número de pedido para revisarlo.";
        }

        if (ContainsAny(message,
                "CANCELAR", "CANCELACION", "YA NO QUIERO"))
        {
            return "Puedes solicitar la cancelación desde el detalle del pedido mientras el estado lo permita. " +
                   "Si ya está en preparación o en camino, el administrador revisará tu caso.";
        }

        if (ContainsAny(message,
                "PAGO", "PAYPAL", "EFECTIVO", "COBRARON", "COBRO"))
        {
            return await BuildPaymentAnswerAsync(ticket, cancellationToken);
        }

        if (ContainsAny(message,
                "PEDIDO", "ESTADO", "REPARTIDOR", "UBICACION", "UBICACIÓN",
                "EN CAMINO", "CUANDO LLEGA", "CUÁNDO LLEGA", "ENTREGA"))
        {
            return await BuildOrderAnswerAsync(ticket, cancellationToken);
        }

        if (ContainsAny(message, "HOLA", "BUENAS", "AYUDA"))
        {
            return "Hola. Puedo orientarte sobre pedidos, pagos, comprobantes, cancelaciones, " +
                   "reembolsos, registro y acceso a tu cuenta. Escribe tu consulta con más detalle.";
        }

        return "Recibimos tu consulta. Un integrante de Atención BioRed revisará el ticket. " +
               "También puedes escribir ‘pedido’, ‘pago’, ‘factura’, ‘cancelar’ o ‘contraseña’ " +
               "para recibir ayuda automática.";
    }

    private async Task<string> BuildOrderAnswerAsync(
        TicketAtencion ticket,
        CancellationToken cancellationToken)
    {
        if (!ticket.IdPedido.HasValue)
        {
            return "Para consultar el estado exacto, crea el ticket desde el detalle de tu pedido " +
                   "o indícanos su número para que Atención BioRed lo revise.";
        }

        var order = await _dbContext.Pedidos
            .AsNoTracking()
            .Where(item => item.IdPedido == ticket.IdPedido.Value)
            .Select(item => new { item.CodPedido, item.EstadoPedido })
            .SingleOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return "No fue posible localizar el pedido relacionado. Un administrador revisará tu caso.";
        }

        return $"El pedido {order.CodPedido} se encuentra en estado ‘{order.EstadoPedido}’. " +
               "Puedes ver el detalle y el seguimiento desde Mis pedidos.";
    }

    private async Task<string> BuildPaymentAnswerAsync(
        TicketAtencion ticket,
        CancellationToken cancellationToken)
    {
        if (!ticket.IdPedido.HasValue)
        {
            return "Para revisar un pago específico, crea el ticket desde el detalle del pedido correspondiente.";
        }

        var payment = await _dbContext.PagosPedidos
            .AsNoTracking()
            .Where(item => item.IdPedido == ticket.IdPedido.Value)
            .OrderByDescending(item => item.CreadoElUtc)
            .Select(item => new { item.Estado, item.ProveedorPago })
            .FirstOrDefaultAsync(cancellationToken);

        if (payment is null)
        {
            return "Aún no encontramos un pago registrado para el pedido relacionado. " +
                   "Un administrador revisará el caso.";
        }

        return $"El último pago registrado tiene estado ‘{payment.Estado}’ y proveedor ‘{payment.ProveedorPago}’. " +
               "No compartas contraseñas, códigos ni datos de tu tarjeta por este chat.";
    }

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(value => text.Contains(Normalize(value), StringComparison.Ordinal));

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}
