using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Integrations;

public sealed class CreatePharmacyAssociationRequest
{
    [Required]
    [StringLength(15)]
    public string CompanyCode { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string PharmacyName { get; init; } = string.Empty;

    [Required]
    [Url]
    [StringLength(500)]
    public string ApiBaseUrl { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ContactName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string ContactEmail { get; init; } = string.Empty;
}

public sealed record PharmacyAssociationResponse(
    int AssociationId,
    string CompanyCode,
    string PharmacyName,
    string ApiBaseUrl,
    string ContactName,
    string ContactEmail,
    string Status,
    bool ConnectionValidated,
    DateTime RequestedAtUtc,
    DateTime? LastValidatedAtUtc,
    DateTime? ApprovedAtUtc);

public enum PharmacyAssociationStatus
{
    Success,
    Duplicate,
    NotFound,
    InvalidUrl,
    InvalidState,
    ConnectionFailed
}

public sealed record PharmacyAssociationResult(
    PharmacyAssociationStatus Status,
    PharmacyAssociationResponse? Association = null,
    string? Detail = null);

public interface IPharmacyAssociationService
{
    Task<PharmacyAssociationResult> CreateAsync(
        CreatePharmacyAssociationRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PharmacyAssociationResponse>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<PharmacyAssociationResult> GetByIdAsync(
        int associationId,
        CancellationToken cancellationToken = default);

    Task<PharmacyAssociationResult> ValidateConnectionAsync(
        int associationId,
        string actorReferenceId,
        CancellationToken cancellationToken = default);

    Task<PharmacyAssociationResult> ApproveAsync(
        int associationId,
        string actorReferenceId,
        CancellationToken cancellationToken = default);

    Task<PharmacyAssociationResult> SuspendAsync(
        int associationId,
        string actorReferenceId,
        CancellationToken cancellationToken = default);
}
