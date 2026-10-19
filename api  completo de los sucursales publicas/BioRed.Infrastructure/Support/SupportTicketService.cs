using BioRed.Application.Notifications;
using BioRed.Application.Support;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Support;

/// <summary>
/// Atención al cliente por tickets. El cliente solo ve sus propios casos y el
/// administrador puede consultar, responder y actualizar cualquier ticket.
/// </summary>
public sealed class SupportTicketService : ISupportTicketService
{
    private static readonly HashSet<string> Categories = new(StringComparer.Ordinal)
    {
        "PEDIDO", "PAGO", "FACTURACION", "PRODUCTO", "CUENTA", "OTRO"
    };

    private static readonly HashSet<string> States = new(StringComparer.Ordinal)
    {
        "ABIERTO", "EN_PROCESO", "RESUELTO", "CERRADO"
    };

    private static readonly HashSet<string> Priorities = new(StringComparer.Ordinal)
    {
        "NORMAL", "ALTA", "URGENTE"
    };

    private readonly BioRedDbContext _dbContext;
    private readonly IPushNotificationSender _pushSender;
    private readonly ISupportBotService _supportBotService;

    public SupportTicketService(
        BioRedDbContext dbContext,
        IPushNotificationSender pushSender,
        ISupportBotService supportBotService)
    {
        _dbContext = dbContext;
        _pushSender = pushSender;
        _supportBotService = supportBotService;
    }

    public async Task<SupportTicketResult> CreateAsync(
        int accountId,
        string actorType,
        string actorReferenceId,
        CreateSupportTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = await ValidateActorAsync(
            accountId,
            actorType,
            actorReferenceId,
            false,
            cancellationToken);

        if (actor.Status != SupportTicketOperationStatus.Success)
        {
            return new(actor.Status, Detail: actor.Detail);
        }

        var subject = request.Subject?.Trim() ?? string.Empty;
        var category = Normalize(request.Category);
        var content = request.Content?.Trim() ?? string.Empty;

        if (subject.Length is < 3 or > 150 ||
            content.Length is < 1 or > 1000 ||
            !Categories.Contains(category))
        {
            return new(
                SupportTicketOperationStatus.InvalidRequest,
                Detail: "Asunto, categoría o mensaje de atención al cliente inválidos.");
        }

        string? orderCode = null;
        if (request.OrderId.HasValue)
        {
            var order = await _dbContext.Pedidos
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.IdPedido == request.OrderId.Value,
                    cancellationToken);

            if (order is null)
            {
                return new(
                    SupportTicketOperationStatus.NotFound,
                    Detail: "No se encontró el pedido indicado.");
            }

            if (!string.Equals(
                    order.CodCliente,
                    actorReferenceId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new(
                    SupportTicketOperationStatus.Forbidden,
                    Detail: "Solo puede relacionar un ticket con sus propios pedidos.");
            }

            orderCode = order.CodPedido;
        }

        var nowUtc = DateTime.UtcNow;
        var ticket = new TicketAtencion
        {
            IdCuentaCliente = accountId,
            IdPedido = request.OrderId,
            Asunto = subject,
            Categoria = category,
            Estado = "ABIERTO",
            Prioridad = "NORMAL",
            FechaCreacionUtc = nowUtc,
            FechaActualizacionUtc = nowUtc
        };

        ticket.Mensajes.Add(new MensajeTicketAtencion
        {
            IdCuentaRemitente = accountId,
            Contenido = content,
            FechaEnvioUtc = nowUtc
        });

        // Un único SaveChanges crea una transacción interna y permite que la
        // estrategia de reintentos de SQL Server administre toda la operación.
        _dbContext.TicketsAtencion.Add(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var botReplied = await _supportBotService.TryReplyAsync(
            ticket.IdTicket,
            accountId,
            content,
            cancellationToken);

        return new(
            SupportTicketOperationStatus.Success,
            MapTicket(ticket, actorReferenceId, orderCode, messageCount: botReplied ? 2 : 1));
    }

    public async Task<SupportTicketsResult> GetAsync(
        int accountId,
        string actorType,
        string actorReferenceId,
        bool canManageAllTickets,
        GetSupportTicketsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = await ValidateActorAsync(
            accountId,
            actorType,
            actorReferenceId,
            canManageAllTickets,
            cancellationToken);

        if (actor.Status != SupportTicketOperationStatus.Success)
        {
            return new(actor.Status, Detail: actor.Detail);
        }

        var state = string.IsNullOrWhiteSpace(request.Status)
            ? null
            : Normalize(request.Status);

        if (state is not null && !States.Contains(state))
        {
            return new(
                SupportTicketOperationStatus.InvalidRequest,
                Detail: "El estado del ticket no es válido.");
        }

        IQueryable<TicketAtencion> query = _dbContext.TicketsAtencion.AsNoTracking();

        if (!canManageAllTickets)
        {
            query = query.Where(item => item.IdCuentaCliente == accountId);
        }

        if (state is not null)
        {
            query = query.Where(item => item.Estado == state);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)request.PageSize);

        var tickets = await query
            .Include(item => item.CuentaCliente)
            .Include(item => item.Pedido)
            .OrderByDescending(item => item.FechaActualizacionUtc)
            .ThenByDescending(item => item.IdTicket)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var ticketIds = tickets.Select(item => item.IdTicket).ToArray();
        var messageCounts = ticketIds.Length == 0
            ? new Dictionary<long, int>()
            : await _dbContext.MensajesTicketAtencion
                .AsNoTracking()
                .Where(item => ticketIds.Contains(item.IdTicket))
                .GroupBy(item => item.IdTicket)
                .Select(group => new { TicketId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.TicketId, item => item.Count, cancellationToken);

        var items = tickets
            .Select(ticket => MapTicket(
                ticket,
                ticket.CuentaCliente.ReferenciaId,
                ticket.Pedido?.CodPedido,
                messageCounts.GetValueOrDefault(ticket.IdTicket)))
            .ToArray();

        return new(
            SupportTicketOperationStatus.Success,
            new SupportTicketsResponse(
                request.Page,
                request.PageSize,
                totalItems,
                totalPages,
                items));
    }

    public async Task<SupportTicketResult> GetByIdAsync(
        long ticketId,
        int accountId,
        string actorType,
        string actorReferenceId,
        bool canManageAllTickets,
        CancellationToken cancellationToken = default)
    {
        var ticket = await ResolveTicketAsync(
            ticketId,
            accountId,
            actorType,
            actorReferenceId,
            canManageAllTickets,
            cancellationToken);

        if (ticket.Status != SupportTicketOperationStatus.Success || ticket.Context is null)
        {
            return new(ticket.Status, Detail: ticket.Detail);
        }

        var messageCount = await GetMessageCountAsync(ticket.Context.Ticket.IdTicket, cancellationToken);

        return new(
            SupportTicketOperationStatus.Success,
            MapTicket(
                ticket.Context.Ticket,
                ticket.Context.Ticket.CuentaCliente.ReferenciaId,
                ticket.Context.Ticket.Pedido?.CodPedido,
                messageCount));
    }

    public async Task<SupportTicketMessagesResult> GetMessagesAsync(
        long ticketId,
        int accountId,
        string actorType,
        string actorReferenceId,
        bool canManageAllTickets,
        long afterMessageId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (afterMessageId < 0 || pageSize is < 1 or > 100)
        {
            return new(
                SupportTicketOperationStatus.InvalidRequest,
                Detail: "afterMessageId debe ser mayor o igual a cero y pageSize debe estar entre 1 y 100.");
        }

        var ticket = await ResolveTicketAsync(
            ticketId,
            accountId,
            actorType,
            actorReferenceId,
            canManageAllTickets,
            cancellationToken);

        if (ticket.Status != SupportTicketOperationStatus.Success || ticket.Context is null)
        {
            return new(ticket.Status, Detail: ticket.Detail);
        }

        var rows = await _dbContext.MensajesTicketAtencion
            .AsNoTracking()
            .Where(item => item.IdTicket == ticketId && item.IdMensaje > afterMessageId)
            .OrderBy(item => item.IdMensaje)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var items = rows
            .Take(pageSize)
            .Select(item => MapMessage(item, accountId))
            .ToArray();

        return new(
            SupportTicketOperationStatus.Success,
            new SupportTicketMessagesResponse(
                ticketId,
                afterMessageId,
                pageSize,
                hasMore,
                items));
    }

    public async Task<SupportTicketMessageResult> SendMessageAsync(
        long ticketId,
        int accountId,
        string actorType,
        string actorReferenceId,
        bool canManageAllTickets,
        SendSupportTicketMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var content = request.Content?.Trim() ?? string.Empty;

        if (content.Length is < 1 or > 1000)
        {
            return new(
                SupportTicketOperationStatus.InvalidRequest,
                Detail: "El mensaje debe tener entre 1 y 1000 caracteres.");
        }

        var ticket = await ResolveTicketAsync(
            ticketId,
            accountId,
            actorType,
            actorReferenceId,
            canManageAllTickets,
            cancellationToken);

        if (ticket.Status != SupportTicketOperationStatus.Success || ticket.Context is null)
        {
            return new(ticket.Status, Detail: ticket.Detail);
        }

        if (ticket.Context.Ticket.Estado == "CERRADO")
        {
            return new(
                SupportTicketOperationStatus.InvalidState,
                Detail: "El ticket está cerrado y ya no acepta mensajes.");
        }

        var nowUtc = DateTime.UtcNow;
        var message = new MensajeTicketAtencion
        {
            IdTicket = ticketId,
            IdCuentaRemitente = accountId,
            Contenido = content,
            FechaEnvioUtc = nowUtc
        };

        ticket.Context.Ticket.FechaActualizacionUtc = nowUtc;
        if (ticket.Context.Ticket.Estado == "RESUELTO")
        {
            ticket.Context.Ticket.Estado = "EN_PROCESO";
            ticket.Context.Ticket.FechaCierreUtc = null;
            ticket.Context.Ticket.CerradoPorCuentaId = null;
        }

        _dbContext.MensajesTicketAtencion.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (!canManageAllTickets)
        {
            await _supportBotService.TryReplyAsync(
                ticketId,
                accountId,
                content,
                cancellationToken);
        }

        if (canManageAllTickets)
        {
            await _pushSender.SendToAccountAsync(
                ticket.Context.Ticket.IdCuentaCliente,
                "support_ticket_message",
                "Respuesta de atención al cliente",
                "Tienes una nueva respuesta en tu ticket de soporte.",
                new Dictionary<string, string>
                {
                    ["ticketId"] = ticketId.ToString(),
                    ["messageId"] = message.IdMensaje.ToString()
                },
                cancellationToken);
        }

        return new(
            SupportTicketOperationStatus.Success,
            MapMessage(message, accountId));
    }

    public async Task<SupportTicketResult> UpdateAsync(
        long ticketId,
        int accountId,
        string actorType,
        string actorReferenceId,
        bool canManageAllTickets,
        UpdateSupportTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!canManageAllTickets)
        {
            return new(
                SupportTicketOperationStatus.Forbidden,
                Detail: "Solo el administrador puede actualizar el estado de un ticket.");
        }

        var state = Normalize(request.Status);
        var priority = string.IsNullOrWhiteSpace(request.Priority)
            ? null
            : Normalize(request.Priority);

        if (!States.Contains(state) || (priority is not null && !Priorities.Contains(priority)))
        {
            return new(
                SupportTicketOperationStatus.InvalidRequest,
                Detail: "El estado o la prioridad del ticket no son válidos.");
        }

        var ticket = await ResolveTicketAsync(
            ticketId,
            accountId,
            actorType,
            actorReferenceId,
            canManageAllTickets,
            cancellationToken);

        if (ticket.Status != SupportTicketOperationStatus.Success || ticket.Context is null)
        {
            return new(ticket.Status, Detail: ticket.Detail);
        }

        var nowUtc = DateTime.UtcNow;
        ticket.Context.Ticket.Estado = state;
        if (priority is not null)
        {
            ticket.Context.Ticket.Prioridad = priority;
        }

        ticket.Context.Ticket.FechaActualizacionUtc = nowUtc;
        ticket.Context.Ticket.FechaCierreUtc = state == "CERRADO" ? nowUtc : null;
        ticket.Context.Ticket.CerradoPorCuentaId = state == "CERRADO" ? accountId : null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var messageCount = await GetMessageCountAsync(ticketId, cancellationToken);
        return new(
            SupportTicketOperationStatus.Success,
            MapTicket(
                ticket.Context.Ticket,
                ticket.Context.Ticket.CuentaCliente.ReferenciaId,
                ticket.Context.Ticket.Pedido?.CodPedido,
                messageCount));
    }

    private async Task<TicketContextResult> ResolveTicketAsync(
        long ticketId,
        int accountId,
        string actorType,
        string actorReferenceId,
        bool canManageAllTickets,
        CancellationToken cancellationToken)
    {
        if (ticketId <= 0)
        {
            return new(
                SupportTicketOperationStatus.InvalidRequest,
                null,
                "El identificador del ticket no es válido.");
        }

        var actor = await ValidateActorAsync(
            accountId,
            actorType,
            actorReferenceId,
            canManageAllTickets,
            cancellationToken);

        if (actor.Status != SupportTicketOperationStatus.Success)
        {
            return new(actor.Status, null, actor.Detail);
        }

        var ticket = await _dbContext.TicketsAtencion
            .Include(item => item.CuentaCliente)
            .Include(item => item.Pedido)
            .SingleOrDefaultAsync(item => item.IdTicket == ticketId, cancellationToken);

        if (ticket is null)
        {
            return new(
                SupportTicketOperationStatus.NotFound,
                null,
                "No se encontró el ticket de atención al cliente.");
        }

        if (!canManageAllTickets && ticket.IdCuentaCliente != accountId)
        {
            return new(
                SupportTicketOperationStatus.Forbidden,
                null,
                "No tiene acceso a este ticket.");
        }

        return new(SupportTicketOperationStatus.Success, new TicketContext(ticket));
    }

    private async Task<ActorValidationResult> ValidateActorAsync(
        int accountId,
        string actorType,
        string actorReferenceId,
        bool canManageAllTickets,
        CancellationToken cancellationToken)
    {
        var expectedType = canManageAllTickets ? "Empleado" : "Cliente";

        if (accountId <= 0 ||
            string.IsNullOrWhiteSpace(actorReferenceId) ||
            !string.Equals(actorType, expectedType, StringComparison.OrdinalIgnoreCase))
        {
            return new(
                SupportTicketOperationStatus.Forbidden,
                "La identidad autenticada no puede acceder a atención al cliente.");
        }

        var exists = await _dbContext.CuentasAcceso
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.IdCuenta == accountId &&
                    item.Estado &&
                    item.TipoUsuario == expectedType &&
                    item.ReferenciaId == actorReferenceId,
                cancellationToken);

        return exists
            ? new(SupportTicketOperationStatus.Success)
            : new(
                SupportTicketOperationStatus.Forbidden,
                "La cuenta autenticada no coincide con sus credenciales de acceso.");
    }

    private async Task<int> GetMessageCountAsync(
        long ticketId,
        CancellationToken cancellationToken) =>
        await _dbContext.MensajesTicketAtencion
            .AsNoTracking()
            .CountAsync(item => item.IdTicket == ticketId, cancellationToken);

    private static SupportTicketResponse MapTicket(
        TicketAtencion ticket,
        string? clientCode,
        string? orderCode,
        int messageCount) =>
        new(
            ticket.IdTicket,
            $"SUP-{ticket.IdTicket:D8}",
            ticket.IdCuentaCliente,
            clientCode,
            ticket.IdPedido,
            orderCode,
            ticket.Asunto,
            ticket.Categoria,
            ticket.Estado,
            ticket.Prioridad,
            messageCount,
            ticket.FechaCreacionUtc,
            ticket.FechaActualizacionUtc,
            ticket.FechaCierreUtc);

    private static SupportTicketMessageResponse MapMessage(
        MensajeTicketAtencion message,
        int accountId) =>
        new(
            message.IdMensaje,
            message.IdTicket,
            message.Contenido,
            message.FechaEnvioUtc,
            message.IdCuentaRemitente == accountId);

    private static string Normalize(string? value) =>
        value?.Trim().ToUpperInvariant() ?? string.Empty;

    private sealed record TicketContext(TicketAtencion Ticket);

    private sealed record TicketContextResult(
        SupportTicketOperationStatus Status,
        TicketContext? Context = null,
        string? Detail = null);

    private sealed record ActorValidationResult(
        SupportTicketOperationStatus Status,
        string? Detail = null);
}
