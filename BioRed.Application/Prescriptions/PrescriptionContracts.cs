using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Prescriptions;

public sealed class CreatePrescriptionItemRequest
{
    [Required, StringLength(15)]
    public string ProductCode { get; init; } = string.Empty;

    [Range(1, 1000)]
    public int AuthorizedQuantity { get; init; }
}

public sealed class CreatePrescriptionRequest
{
    [StringLength(50)]
    public string? PrescriptionNumber { get; init; }

    [Required, StringLength(150)]
    public string DoctorName { get; init; } = string.Empty;

    [Required, StringLength(50)]
    public string DoctorLicense { get; init; } = string.Empty;

    public DateOnly IssuedDate { get; init; }
    public DateOnly ExpirationDate { get; init; }

    [StringLength(300)]
    public string? Notes { get; init; }

    [Required, StringLength(150)]
    public string FileName { get; init; } = string.Empty;

    [Required, RegularExpression("^(application/pdf|image/jpeg|image/png)$")]
    public string ContentType { get; init; } = string.Empty;

    [Required]
    public string DocumentBase64 { get; init; } = string.Empty;

    [Required, MinLength(1), MaxLength(100)]
    public IReadOnlyCollection<CreatePrescriptionItemRequest> Items { get; init; } =
        Array.Empty<CreatePrescriptionItemRequest>();
}

public sealed class ReviewPrescriptionRequest
{
    [Required, RegularExpression("^(APPROVED|REJECTED)$")]
    public string Status { get; init; } = string.Empty;

    [StringLength(300)]
    public string? ReviewNotes { get; init; }
}

public sealed class GetPrescriptionsRequest
{
    [RegularExpression("^(PENDING|APPROVED|REJECTED|USED|EXPIRED)$")]
    public string? Status { get; init; }

    [StringLength(15)]
    public string? ClientCode { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record PrescriptionItemResponse(
    string ProductCode,
    string ProductName,
    int AuthorizedQuantity);

public sealed record PrescriptionResponse(
    long PrescriptionId,
    string ClientCode,
    string ClientName,
    string? PrescriptionNumber,
    string DoctorName,
    string DoctorLicense,
    DateOnly IssuedDate,
    DateOnly ExpirationDate,
    string Status,
    string? Notes,
    string FileName,
    string ContentType,
    long FileSize,
    string DocumentSha256,
    string? ReviewedBy,
    DateTime? ReviewedAtUtc,
    string? ReviewNotes,
    int? OrderId,
    DateTime CreatedAtUtc,
    DateTime? UsedAtUtc,
    IReadOnlyCollection<PrescriptionItemResponse> Items);

public sealed record GetPrescriptionsResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<PrescriptionResponse> Items);

public sealed record PrescriptionDocumentResponse(
    byte[] Content,
    string ContentType,
    string FileName);

public enum PrescriptionManagementStatus
{
    Success,
    NotFound,
    Forbidden,
    ClientNotFound,
    UserNotFound,
    ProductNotFound,
    DuplicateProduct,
    InvalidDates,
    InvalidDocument,
    InvalidTransition
}

public sealed record PrescriptionManagementResult(
    PrescriptionManagementStatus Status,
    PrescriptionResponse? Prescription = null,
    PrescriptionDocumentResponse? Document = null,
    string? Detail = null);

public interface IPrescriptionService
{
    Task<GetPrescriptionsResponse> GetForClientAsync(
        string clientCode,
        GetPrescriptionsRequest request,
        CancellationToken cancellationToken = default);

    Task<GetPrescriptionsResponse> GetForManagementAsync(
        GetPrescriptionsRequest request,
        CancellationToken cancellationToken = default);

    Task<PrescriptionManagementResult> GetByIdAsync(
        long prescriptionId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<PrescriptionManagementResult> GetDocumentAsync(
        long prescriptionId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<PrescriptionManagementResult> CreateAsync(
        string clientCode,
        CreatePrescriptionRequest request,
        CancellationToken cancellationToken = default);

    Task<PrescriptionManagementResult> ReviewAsync(
        long prescriptionId,
        string actorReferenceId,
        ReviewPrescriptionRequest request,
        CancellationToken cancellationToken = default);
}
