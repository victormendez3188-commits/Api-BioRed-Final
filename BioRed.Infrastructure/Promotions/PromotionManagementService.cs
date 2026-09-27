using BioRed.Application.Promotions;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Promotions;

public sealed class PromotionManagementService : IPromotionManagementService
{
    private const decimal MaximumAmount = 9_999_999_999_999_999.99m;
    private readonly BioRedDbContext _dbContext;

    public PromotionManagementService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetPromotionsResponse> GetAsync(
        GetManagedPromotionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Promociones.AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(item => item.Activa);
        }

        if (!string.IsNullOrWhiteSpace(request.CompanyCode))
        {
            var companyCode = NormalizeCode(request.CompanyCode);
            query = query.Where(item => item.CodEmpresa == companyCode);
        }

        if (!string.IsNullOrWhiteSpace(request.BranchCode))
        {
            var branchCode = NormalizeCode(request.BranchCode);
            query = query.Where(item => item.CodSucursal == branchCode);
        }

        return await BuildPageAsync(
            query.OrderByDescending(item => item.InicioUtc)
                .ThenBy(item => item.CodPromocion),
            request.Page,
            request.PageSize,
            cancellationToken);
    }

    public async Task<GetPromotionsResponse> GetAvailableAsync(
        string clientCode,
        GetAvailablePromotionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedClient = NormalizeCode(clientCode);
        var branchCode = NormalizeCode(request.BranchCode);
        var nowUtc = DateTime.UtcNow;

        var companyCode = await _dbContext.Sucursales.AsNoTracking()
            .Where(item => item.CodSucursal == branchCode && item.Estado)
            .Select(item => item.CodEmpresa)
            .SingleOrDefaultAsync(cancellationToken);

        if (companyCode is null)
        {
            return new(1, 100, 0, 0, Array.Empty<PromotionResponse>());
        }

        var query = _dbContext.Promociones.AsNoTracking()
            .Where(item =>
                item.Activa &&
                (item.TipoDescuento == "PERCENTAGE" ||
                 item.TipoDescuento == "FIXED") &&
                item.CodEmpresa == companyCode &&
                (item.CodSucursal == null || item.CodSucursal == branchCode) &&
                item.InicioUtc <= nowUtc &&
                item.FinUtc >= nowUtc &&
                item.MontoMinimoPedido <= request.OrderSubtotal &&
                (!item.LimiteUsosTotal.HasValue ||
                 _dbContext.PromocionesUsos.Count(usage =>
                     usage.CodPromocion == item.CodPromocion) < item.LimiteUsosTotal.Value) &&
                _dbContext.PromocionesUsos.Count(usage =>
                    usage.CodPromocion == item.CodPromocion &&
                    usage.CodCliente == normalizedClient) < item.LimiteUsosCliente)
            .OrderBy(item => item.FinUtc)
            .ThenBy(item => item.CodPromocion);

        return await BuildPageAsync(query, 1, 100, cancellationToken);
    }

    public async Task<PromotionManagementResult> GetByCodeAsync(
        string promotionCode,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(promotionCode);
        var promotion = await _dbContext.Promociones.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CodPromocion == code,
                cancellationToken);

        if (promotion is null)
        {
            return Failure(
                PromotionManagementStatus.NotFound,
                $"No existe la promoción {code}.");
        }

        return new(
            PromotionManagementStatus.Success,
            await BuildResponseAsync(promotion, cancellationToken));
    }

    public async Task<PromotionManagementResult> CreateAsync(
        string actorReferenceId,
        SavePromotionRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(request.PromotionCode);

        if (await _dbContext.Promociones.AsNoTracking().AnyAsync(
                item => item.CodPromocion == code,
                cancellationToken))
        {
            return Failure(
                PromotionManagementStatus.Duplicate,
                $"Ya existe la promoción {code}.");
        }

        var validation = await ValidateConfigurationAsync(
            actorReferenceId,
            request.CompanyCode,
            request.BranchCode,
            request.DiscountType,
            request.DiscountValue,
            request.MinimumOrderAmount,
            request.MaximumDiscountAmount,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.TotalUsageLimit,
            request.PerClientUsageLimit,
            request.ProductCodes,
            cancellationToken);

        if (validation.Error is not null)
        {
            return validation.Error;
        }

        var nowUtc = DateTime.UtcNow;
        var promotion = new Promocion
        {
            CodPromocion = code,
            CodEmpresa = validation.CompanyCode!,
            CodSucursal = validation.BranchCode,
            Nombre = request.Name.Trim(),
            Descripcion = CleanOptional(request.Description),
            TipoDescuento = NormalizeCode(request.DiscountType),
            ValorDescuento = request.DiscountValue,
            MontoMinimoPedido = request.MinimumOrderAmount,
            MontoMaximoDescuento = NormalizeMaximumDiscount(request.MaximumDiscountAmount),
            InicioUtc = NormalizeUtc(request.StartsAtUtc),
            FinUtc = NormalizeUtc(request.EndsAtUtc),
            LimiteUsosTotal = request.TotalUsageLimit,
            LimiteUsosCliente = request.PerClientUsageLimit,
            Activa = true,
            CreadoPor = NormalizeCode(actorReferenceId),
            CreadoElUtc = nowUtc
        };

        

        try
        {
            _dbContext.Promociones.Add(promotion);

            foreach (var productCode in validation.ProductCodes)
            {
                _dbContext.PromocionesProductos.Add(
                    new PromocionProducto
                    {
                        CodPromocion = code,
                        CodProducto = productCode
                    });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new PromotionManagementResult(
                PromotionManagementStatus.Success,
                await BuildResponseAsync(
                    promotion,
                    cancellationToken));
        }
        catch (DbUpdateException exception)
             when (IsUniqueViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();

            return Failure(
                PromotionManagementStatus.Duplicate,
                $"Ya existe la promoción {code}.");
        }
    }

    public async Task<PromotionManagementResult> UpdateAsync(
        string promotionCode,
        string actorReferenceId,
        UpdatePromotionRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(promotionCode);
        var promotion = await _dbContext.Promociones.SingleOrDefaultAsync(
            item => item.CodPromocion == code,
            cancellationToken);

        if (promotion is null)
        {
            return Failure(
                PromotionManagementStatus.NotFound,
                $"No existe la promoción {code}.");
        }

        if (await _dbContext.PromocionesUsos.AsNoTracking().AnyAsync(
                item => item.CodPromocion == code,
                cancellationToken))
        {
            return Failure(
                PromotionManagementStatus.AlreadyUsed,
                "Una promoción utilizada no puede cambiar sus reglas; solamente puede desactivarse.");
        }

        var validation = await ValidateConfigurationAsync(
            actorReferenceId,
            request.CompanyCode,
            request.BranchCode,
            request.DiscountType,
            request.DiscountValue,
            request.MinimumOrderAmount,
            request.MaximumDiscountAmount,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.TotalUsageLimit,
            request.PerClientUsageLimit,
            request.ProductCodes,
            cancellationToken);

        if (validation.Error is not null)
        {
            return validation.Error;
        }

        promotion.CodEmpresa = validation.CompanyCode!;
        promotion.CodSucursal = validation.BranchCode;
        promotion.Nombre = request.Name.Trim();
        promotion.Descripcion = CleanOptional(request.Description);
        promotion.TipoDescuento = NormalizeCode(request.DiscountType);
        promotion.ValorDescuento = request.DiscountValue;
        promotion.MontoMinimoPedido = request.MinimumOrderAmount;
        promotion.MontoMaximoDescuento = NormalizeMaximumDiscount(request.MaximumDiscountAmount);
        promotion.InicioUtc = NormalizeUtc(request.StartsAtUtc);
        promotion.FinUtc = NormalizeUtc(request.EndsAtUtc);
        promotion.LimiteUsosTotal = request.TotalUsageLimit;
        promotion.LimiteUsosCliente = request.PerClientUsageLimit;
        promotion.Activa = request.Active;
        promotion.ActualizadoPor = NormalizeCode(actorReferenceId);
        promotion.ActualizadoElUtc = DateTime.UtcNow;

        var oldProducts = await _dbContext.PromocionesProductos
            .Where(item => item.CodPromocion == code)
            .ToArrayAsync(cancellationToken);
        _dbContext.PromocionesProductos.RemoveRange(oldProducts);

        foreach (var productCode in validation.ProductCodes)
        {
            _dbContext.PromocionesProductos.Add(new PromocionProducto
            {
                CodPromocion = code,
                CodProducto = productCode
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new(
            PromotionManagementStatus.Success,
            await BuildResponseAsync(promotion, cancellationToken));
    }

    public async Task<PromotionManagementResult> DeactivateAsync(
        string promotionCode,
        string actorReferenceId,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(promotionCode);
        var promotion = await _dbContext.Promociones.SingleOrDefaultAsync(
            item => item.CodPromocion == code,
            cancellationToken);

        if (promotion is null)
        {
            return Failure(
                PromotionManagementStatus.NotFound,
                $"No existe la promoción {code}.");
        }

        promotion.Activa = false;
        promotion.ActualizadoPor = NormalizeCode(actorReferenceId);
        promotion.ActualizadoElUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(
            PromotionManagementStatus.Success,
            await BuildResponseAsync(promotion, cancellationToken));
    }

    private async Task<GetPromotionsResponse> BuildPageAsync(
        IQueryable<Promocion> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalItems = await query.CountAsync(cancellationToken);
        var promotions = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        var responses = new List<PromotionResponse>(promotions.Length);
        foreach (var promotion in promotions)
        {
            responses.Add(await BuildResponseAsync(promotion, cancellationToken));
        }

        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)pageSize);

        return new(page, pageSize, totalItems, totalPages, responses);
    }

    private async Task<PromotionResponse> BuildResponseAsync(
        Promocion promotion,
        CancellationToken cancellationToken)
    {
        var productCodes = await _dbContext.PromocionesProductos.AsNoTracking()
            .Where(item => item.CodPromocion == promotion.CodPromocion)
            .OrderBy(item => item.CodProducto)
            .Select(item => item.CodProducto)
            .ToArrayAsync(cancellationToken);
        var usedCount = await _dbContext.PromocionesUsos.AsNoTracking()
            .CountAsync(
                item => item.CodPromocion == promotion.CodPromocion,
                cancellationToken);

        return new(
            promotion.CodPromocion,
            promotion.CodEmpresa,
            promotion.CodSucursal,
            promotion.Nombre,
            promotion.Descripcion,
            promotion.TipoDescuento,
            promotion.ValorDescuento,
            promotion.MontoMinimoPedido,
            promotion.MontoMaximoDescuento,
            promotion.InicioUtc,
            promotion.FinUtc,
            promotion.LimiteUsosTotal,
            promotion.LimiteUsosCliente,
            usedCount,
            promotion.Activa,
            productCodes);
    }

    private async Task<ConfigurationValidation> ValidateConfigurationAsync(
        string actorReferenceId,
        string companyCode,
        string? branchCode,
        string discountType,
        decimal discountValue,
        decimal minimumOrderAmount,
        decimal? maximumDiscountAmount,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        int? totalUsageLimit,
        int perClientUsageLimit,
        IReadOnlyCollection<string> productCodes,
        CancellationToken cancellationToken)
    {
        var actorCode = NormalizeCode(actorReferenceId);
        if (!await _dbContext.Usuarios.AsNoTracking().AnyAsync(
                item => item.CodUsuario == actorCode && item.Estado,
                cancellationToken))
        {
            return Invalid(PromotionManagementStatus.NotFound,
                "La cuenta no está vinculada con un usuario administrativo activo.");
        }

        var normalizedCompany = NormalizeCode(companyCode);
        if (!await _dbContext.Empresas.AsNoTracking().AnyAsync(
                item => item.CodEmpresa == normalizedCompany && item.Estado,
                cancellationToken))
        {
            return Invalid(PromotionManagementStatus.CompanyNotFound,
                $"No existe la empresa activa {normalizedCompany}.");
        }

        var normalizedBranch = CleanOptional(branchCode)?.ToUpperInvariant();
        if (normalizedBranch is not null &&
            !await _dbContext.Sucursales.AsNoTracking().AnyAsync(
                item => item.CodSucursal == normalizedBranch &&
                        item.CodEmpresa == normalizedCompany &&
                        item.Estado,
                cancellationToken))
        {
            return Invalid(PromotionManagementStatus.BranchNotFound,
                "La sucursal no existe, está inactiva o no pertenece a la empresa.");
        }

        var type = NormalizeCode(discountType);
        var normalizedStart = NormalizeUtc(startsAtUtc);
        var normalizedEnd = NormalizeUtc(endsAtUtc);
        var normalizedMaximum = NormalizeMaximumDiscount(maximumDiscountAmount);

        if (type is not ("PERCENTAGE" or "FIXED") ||
            discountValue <= 0m ||
            discountValue > MaximumAmount ||
            (type == "PERCENTAGE" && discountValue > 100m) ||
            minimumOrderAmount < 0m || minimumOrderAmount > MaximumAmount ||
            (normalizedMaximum.HasValue &&
             (normalizedMaximum <= 0m || normalizedMaximum > MaximumAmount)) ||
            normalizedStart == default ||
            normalizedEnd <= normalizedStart ||
            (totalUsageLimit.HasValue && totalUsageLimit <= 0) ||
            perClientUsageLimit <= 0)
        {
            return Invalid(PromotionManagementStatus.InvalidConfiguration,
                "Revise el tipo, montos, vigencia y límites de uso de la promoción.");
        }

        var normalizedProducts = productCodes
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(NormalizeCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedProducts.Length != productCodes.Count)
        {
            return Invalid(PromotionManagementStatus.InvalidConfiguration,
                "Los códigos de producto no pueden estar vacíos ni repetidos.");
        }

        if (normalizedProducts.Length > 0)
        {
            var foundProducts = await _dbContext.Productos.AsNoTracking()
                .Where(item =>
                    normalizedProducts.Contains(item.CodProducto) &&
                    item.CodEmpresa == normalizedCompany &&
                    item.Estado)
                .Select(item => item.CodProducto)
                .ToArrayAsync(cancellationToken);

            if (foundProducts.Length != normalizedProducts.Length)
            {
                return Invalid(PromotionManagementStatus.ProductNotFound,
                    "Uno o más productos no existen, están inactivos o pertenecen a otra empresa.");
            }
        }

        return new(null, normalizedCompany, normalizedBranch, normalizedProducts);
    }

    private static ConfigurationValidation Invalid(
        PromotionManagementStatus status,
        string detail) =>
        new(Failure(status, detail), null, null, Array.Empty<string>());

    private static PromotionManagementResult Failure(
        PromotionManagementStatus status,
        string detail) =>
        new(status, Detail: detail);

    private static string NormalizeCode(string value) =>
        value.Trim().ToUpperInvariant();

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static decimal? NormalizeMaximumDiscount(decimal? value) =>
        value is null or 0m ? null : value;

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException &&
        sqlException.Number is 2601 or 2627;

    private sealed record ConfigurationValidation(
        PromotionManagementResult? Error,
        string? CompanyCode,
        string? BranchCode,
        IReadOnlyCollection<string> ProductCodes);
}
