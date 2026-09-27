using System.Security.Cryptography;
using BioRed.Application.Prescriptions;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Prescriptions;

public sealed class PrescriptionService : IPrescriptionService
{
    private const int MaximumDocumentBytes = 5 * 1024 * 1024;
    private readonly BioRedDbContext _dbContext;

    public PrescriptionService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetPrescriptionsResponse> GetForClientAsync(
        string clientCode,
        GetPrescriptionsRequest request,
        CancellationToken cancellationToken = default)
    {
        await ExpireOldPrescriptionsAsync(cancellationToken);
        var normalizedClient = NormalizeCode(clientCode);
        var query = _dbContext.RecetasClientes.AsNoTracking()
            .Where(item => item.CodCliente == normalizedClient);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = NormalizeCode(request.Status);
            query = query.Where(item => item.Estado == status);
        }

        return await BuildPageAsync(
            query,
            request.Page,
            request.PageSize,
            cancellationToken);
    }

    public async Task<GetPrescriptionsResponse> GetForManagementAsync(
        GetPrescriptionsRequest request,
        CancellationToken cancellationToken = default)
    {
        await ExpireOldPrescriptionsAsync(cancellationToken);
        var query = _dbContext.RecetasClientes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = NormalizeCode(request.Status);
            query = query.Where(item => item.Estado == status);
        }

        if (!string.IsNullOrWhiteSpace(request.ClientCode))
        {
            var clientCode = NormalizeCode(request.ClientCode);
            query = query.Where(item => item.CodCliente == clientCode);
        }

        return await BuildPageAsync(
            query,
            request.Page,
            request.PageSize,
            cancellationToken);
    }

    public async Task<PrescriptionManagementResult> GetByIdAsync(
        long prescriptionId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        await ExpireOldPrescriptionsAsync(cancellationToken);
        var prescription = await _dbContext.RecetasClientes.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IdReceta == prescriptionId,
                cancellationToken);

        if (prescription is null)
        {
            return NotFound(prescriptionId);
        }

        if (!CanAccess(
                prescription,
                actorType,
                actorReferenceId,
                isAdministrator))
        {
            return Failure(
                PrescriptionManagementStatus.Forbidden,
                "La receta pertenece a otro cliente.");
        }

        return new(
            PrescriptionManagementStatus.Success,
            await BuildResponseAsync(prescription, cancellationToken));
    }

    public async Task<PrescriptionManagementResult> GetDocumentAsync(
        long prescriptionId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var prescription = await _dbContext.RecetasClientes.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IdReceta == prescriptionId,
                cancellationToken);

        if (prescription is null)
        {
            return NotFound(prescriptionId);
        }

        if (!CanAccess(
                prescription,
                actorType,
                actorReferenceId,
                isAdministrator))
        {
            return Failure(
                PrescriptionManagementStatus.Forbidden,
                "La receta pertenece a otro cliente.");
        }

        return new(
            PrescriptionManagementStatus.Success,
            Document: new PrescriptionDocumentResponse(
                prescription.Documento,
                prescription.TipoContenido,
                prescription.NombreArchivo));
    }

    public async Task<PrescriptionManagementResult> CreateAsync(
        string clientCode,
        CreatePrescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedClient = NormalizeCode(clientCode);
        if (!await _dbContext.Clientes.AsNoTracking().AnyAsync(
                item => item.CodCliente == normalizedClient && item.Estado,
                cancellationToken))
        {
            return Failure(
                PrescriptionManagementStatus.ClientNotFound,
                "No existe un cliente activo asociado a la cuenta.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.IssuedDate == default ||
            request.ExpirationDate == default ||
            request.IssuedDate > today ||
            request.ExpirationDate < request.IssuedDate ||
            request.ExpirationDate < today)
        {
            return Failure(
                PrescriptionManagementStatus.InvalidDates,
                "La fecha de emisión no puede ser futura y la receta debe estar vigente.");
        }

        var normalizedItems = request.Items
            .Where(item => item is not null)
            .Select(item => new
            {
                ProductCode = NormalizeCode(item.ProductCode),
                item.AuthorizedQuantity
            })
            .ToArray();

        if (normalizedItems.Length != request.Items.Count ||
            normalizedItems.Any(item =>
                string.IsNullOrWhiteSpace(item.ProductCode) ||
                item.AuthorizedQuantity <= 0) ||
            normalizedItems.Select(item => item.ProductCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != normalizedItems.Length)
        {
            return Failure(
                PrescriptionManagementStatus.DuplicateProduct,
                "Los productos de la receta no pueden estar vacíos ni repetidos.");
        }

        var productCodes = normalizedItems
            .Select(item => item.ProductCode)
            .ToArray();
        var foundProducts = await _dbContext.Productos.AsNoTracking()
            .Where(item =>
                productCodes.Contains(item.CodProducto) &&
                item.Estado &&
                item.RequiereReceta)
            .Select(item => item.CodProducto)
            .ToArrayAsync(cancellationToken);

        if (foundProducts.Length != productCodes.Length)
        {
            return Failure(
                PrescriptionManagementStatus.ProductNotFound,
                "Uno o más productos no existen, están inactivos o no requieren receta.");
        }

        byte[] document;
        try
        {
            document = Convert.FromBase64String(request.DocumentBase64.Trim());
        }
        catch (FormatException)
        {
            return Failure(
                PrescriptionManagementStatus.InvalidDocument,
                "El documento no contiene Base64 válido.");
        }

        var contentType = request.ContentType.Trim().ToLowerInvariant();
        var fileName = Path.GetFileName(request.FileName.Trim());
        if (document.Length is 0 or > MaximumDocumentBytes ||
            fileName.Length == 0 || fileName.Length > 150 ||
            !HasExpectedSignature(document, contentType) ||
            !HasValidExtension(fileName, contentType))
        {
            return Failure(
                PrescriptionManagementStatus.InvalidDocument,
                "El archivo debe ser PDF, JPEG o PNG válido y no superar 5 MB.");
        }

        var prescription = new RecetaCliente
        {
            CodCliente = normalizedClient,
            NumeroReceta = CleanOptional(request.PrescriptionNumber),
            NombreMedico = request.DoctorName.Trim(),
            NumeroColegiado = request.DoctorLicense.Trim(),
            FechaEmision = request.IssuedDate,
            FechaVencimiento = request.ExpirationDate,
            Estado = "PENDING",
            Observaciones = CleanOptional(request.Notes),
            NombreArchivo = fileName,
            TipoContenido = contentType,
            TamanoArchivo = document.LongLength,
            Documento = document,
            HashDocumentoSha256 = Convert.ToHexString(SHA256.HashData(document)),
            CreadoElUtc = DateTime.UtcNow
        };

        _dbContext.RecetasClientes.Add(prescription);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var item in normalizedItems)
        {
            _dbContext.RecetasProductos.Add(new RecetaProducto
            {
                IdReceta = prescription.IdReceta,
                CodProducto = item.ProductCode,
                CantidadAutorizada = item.AuthorizedQuantity
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(
            PrescriptionManagementStatus.Success,
            await BuildResponseAsync(prescription, cancellationToken));
    }

    public async Task<PrescriptionManagementResult> ReviewAsync(
        long prescriptionId,
        string actorReferenceId,
        ReviewPrescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorCode = NormalizeCode(actorReferenceId);
        if (!await _dbContext.Usuarios.AsNoTracking().AnyAsync(
                item => item.CodUsuario == actorCode && item.Estado,
                cancellationToken))
        {
            return Failure(
                PrescriptionManagementStatus.UserNotFound,
                "La cuenta no está vinculada con un usuario administrativo activo.");
        }

        var prescription = await _dbContext.RecetasClientes.SingleOrDefaultAsync(
            item => item.IdReceta == prescriptionId,
            cancellationToken);

        if (prescription is null)
        {
            return NotFound(prescriptionId);
        }

        if (prescription.Estado != "PENDING")
        {
            return Failure(
                PrescriptionManagementStatus.InvalidTransition,
                $"Una receta en estado {prescription.Estado} ya no puede revisarse.");
        }

        var status = NormalizeCode(request.Status);
        var reviewNotes = CleanOptional(request.ReviewNotes);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (status == "APPROVED" && prescription.FechaVencimiento < today)
        {
            return Failure(
                PrescriptionManagementStatus.InvalidDates,
                "No se puede aprobar una receta vencida.");
        }

        if (status == "REJECTED" &&
            (reviewNotes is null || reviewNotes.Length < 5))
        {
            return Failure(
                PrescriptionManagementStatus.InvalidTransition,
                "Debe indicar el motivo del rechazo con al menos 5 caracteres.");
        }

        prescription.Estado = status;
        prescription.RevisadoPor = actorCode;
        prescription.RevisadoElUtc = DateTime.UtcNow;
        prescription.ObservacionesRevision = reviewNotes;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(
            PrescriptionManagementStatus.Success,
            await BuildResponseAsync(prescription, cancellationToken));
    }

    private async Task<GetPrescriptionsResponse> BuildPageAsync(
        IQueryable<RecetaCliente> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalItems = await query.CountAsync(cancellationToken);
        var prescriptions = await query
            .OrderByDescending(item => item.CreadoElUtc)
            .ThenByDescending(item => item.IdReceta)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        var responses = new List<PrescriptionResponse>(prescriptions.Length);

        foreach (var prescription in prescriptions)
        {
            responses.Add(await BuildResponseAsync(prescription, cancellationToken));
        }

        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)pageSize);

        return new(page, pageSize, totalItems, totalPages, responses);
    }

    private async Task<PrescriptionResponse> BuildResponseAsync(
        RecetaCliente prescription,
        CancellationToken cancellationToken)
    {
        var clientName = await _dbContext.Clientes.AsNoTracking()
            .Where(item => item.CodCliente == prescription.CodCliente)
            .Select(item => item.NombreCliente + " " + item.ApellidoCliente)
            .SingleAsync(cancellationToken);
        var items = await
        (
            from detail in _dbContext.RecetasProductos.AsNoTracking()
            join product in _dbContext.Productos.AsNoTracking()
                on detail.CodProducto equals product.CodProducto
            where detail.IdReceta == prescription.IdReceta
            orderby detail.IdRecetaProducto
            select new PrescriptionItemResponse(
                detail.CodProducto,
                product.NombreProducto,
                detail.CantidadAutorizada)
        ).ToArrayAsync(cancellationToken);

        return new(
            prescription.IdReceta,
            prescription.CodCliente,
            clientName,
            prescription.NumeroReceta,
            prescription.NombreMedico,
            prescription.NumeroColegiado,
            prescription.FechaEmision,
            prescription.FechaVencimiento,
            prescription.Estado,
            prescription.Observaciones,
            prescription.NombreArchivo,
            prescription.TipoContenido,
            prescription.TamanoArchivo,
            prescription.HashDocumentoSha256,
            prescription.RevisadoPor,
            prescription.RevisadoElUtc,
            prescription.ObservacionesRevision,
            prescription.IdPedido,
            prescription.CreadoElUtc,
            prescription.UsadoElUtc,
            items);
    }

    private async Task ExpireOldPrescriptionsAsync(
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expired = await _dbContext.RecetasClientes
            .Where(item =>
                item.Estado == "APPROVED" &&
                item.FechaVencimiento < today)
            .ToArrayAsync(cancellationToken);

        if (expired.Length == 0)
        {
            return;
        }

        foreach (var prescription in expired)
        {
            prescription.Estado = "EXPIRED";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool CanAccess(
        RecetaCliente prescription,
        string actorType,
        string actorReferenceId,
        bool isAdministrator) =>
        isAdministrator ||
        (actorType.Equals("Cliente", StringComparison.OrdinalIgnoreCase) &&
         prescription.CodCliente.Equals(
             actorReferenceId,
             StringComparison.OrdinalIgnoreCase));

    private static bool HasExpectedSignature(byte[] document, string contentType) =>
        contentType switch
        {
            "application/pdf" => document.Length >= 5 &&
                document[0] == 0x25 && document[1] == 0x50 &&
                document[2] == 0x44 && document[3] == 0x46 &&
                document[4] == 0x2D,
            "image/jpeg" => document.Length >= 3 &&
                document[0] == 0xFF && document[1] == 0xD8 && document[2] == 0xFF,
            "image/png" => document.Length >= 8 &&
                document[0] == 0x89 && document[1] == 0x50 &&
                document[2] == 0x4E && document[3] == 0x47 &&
                document[4] == 0x0D && document[5] == 0x0A &&
                document[6] == 0x1A && document[7] == 0x0A,
            _ => false
        };

    private static bool HasValidExtension(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return contentType switch
        {
            "application/pdf" => extension == ".pdf",
            "image/jpeg" => extension is ".jpg" or ".jpeg",
            "image/png" => extension == ".png",
            _ => false
        };
    }

    private static PrescriptionManagementResult NotFound(long prescriptionId) =>
        Failure(
            PrescriptionManagementStatus.NotFound,
            $"No existe la receta {prescriptionId}.");

    private static PrescriptionManagementResult Failure(
        PrescriptionManagementStatus status,
        string detail) =>
        new(status, Detail: detail);

    private static string NormalizeCode(string value) =>
        value.Trim().ToUpperInvariant();

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
