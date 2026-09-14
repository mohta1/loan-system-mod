using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoanSystem.Contracts;
using LoanSystem.Modules.Disbursements.Domain;

namespace LoanSystem.Modules.Disbursements.Application;

public static class DisbursementPermissions { public const string Read = "disbursements.read", Create = "disbursements.create"; }
public sealed record BeneficiaryInput(string? Type, string? DisplayName, string? AccountHolderName, string? BankName, string? BankAccountIdentifier);
public sealed record CreateDisbursementCommand(Guid LoanId, decimal Amount, BeneficiaryInput? Beneficiary, IReadOnlyList<Guid>? SupportingDocumentIds);
public sealed record DisbursementDto(Guid DisbursementId, Guid LoanId, decimal Amount, string Currency, BeneficiarySnapshot Beneficiary, IReadOnlyList<Guid> SupportingDocumentIds, string Status, string CapacityReservationStatus, string? CapacityRejectionReasonCode, string? CapacityRejectionReason, Guid ActorUserId, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string ETag);
public sealed record DisbursementListItem(Guid DisbursementId, Guid LoanId, decimal Amount, string Currency, string BeneficiaryName, string BeneficiaryType, string Status, string CapacityReservationStatus, DateTimeOffset CreatedAtUtc);
public sealed record DisbursementSearch(Guid? LoanId, DisbursementStatus? Status, int PageNumber = 1, int PageSize = 25);
public sealed record DisbursementPage(IReadOnlyList<DisbursementListItem> Items, int PageNumber, int PageSize, int TotalCount);
public enum IdempotentCreateOutcome { Created, Replayed, Conflict }
public sealed record IdempotentCreateResult(IdempotentCreateOutcome Outcome, Disbursement Disbursement);
public interface IDisbursementStore { Task<Disbursement?> FindAsync(Guid id, CancellationToken ct); Task<DisbursementPage> SearchAsync(DisbursementSearch search, CancellationToken ct); Task<IdempotentCreateResult> CreateAsync(Disbursement value, string scope, Guid actor, string keyHash, string requestHash, DisbursementCapacityRequestedV1 message, CancellationToken ct); Task ApplyReservedAsync(DisbursementCapacityReservedV1 message, CancellationToken ct); Task ApplyRejectedAsync(DisbursementCapacityRejectedV1 message, CancellationToken ct); }
public sealed class DisbursementService(IDisbursementStore store, ILoanAccountsModule loans, IDocumentsModule documents)
    : IDisbursementCapacityReservedConsumer, IDisbursementCapacityRejectedConsumer
{
    public async Task<IdempotentCreateResult> CreateAsync(CreateDisbursementCommand command, string idempotencyKey, Guid actor, string correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) throw new MissingIdempotencyKeyException();
        var loan = await loans.GetAsync(command.LoanId, ct) ?? throw new LoanNotFoundException();
        if (!Enum.TryParse<BeneficiaryType>(command.Beneficiary?.Type, true, out var type)) throw new DisbursementValidationException("beneficiaryType");
        var beneficiary = new BeneficiarySnapshot(type, command.Beneficiary?.DisplayName ?? "", command.Beneficiary?.AccountHolderName ?? "", command.Beneficiary?.BankName ?? "", command.Beneficiary?.BankAccountIdentifier ?? "");
        var ids = command.SupportingDocumentIds?.Distinct().Order().ToArray() ?? [];
        foreach (var id in ids) if (await documents.GetAccessibleAsync(id, actor, ct) is null) throw new SupportingDocumentNotAccessibleException();
        var requestHash = Hash(JsonSerializer.Serialize(new { command.LoanId, command.Amount, BeneficiaryType = type.ToString(), command.Beneficiary?.DisplayName, command.Beneficiary?.AccountHolderName, command.Beneficiary?.BankName, command.Beneficiary?.BankAccountIdentifier, Documents = ids }));
        var keyHash = Hash(idempotencyKey.Trim()); var creationHash = Hash($"disbursements.create:{actor:N}:{keyHash}"); var at = DateTimeOffset.UtcNow;
        var value = Disbursement.Create(Guid.NewGuid(), command.LoanId, command.Amount, loan.Currency, beneficiary, ids, actor, correlationId, creationHash, at);
        var message = new DisbursementCapacityRequestedV1(Guid.NewGuid(), at, correlationId, null, value.DisbursementId, value.LoanId, value.Amount, value.Currency, at);
        return await store.CreateAsync(value, "disbursements.create", actor, keyHash, requestHash, message, ct);
    }
    public async Task<DisbursementDto?> GetAsync(Guid id, CancellationToken ct) { var value = await store.FindAsync(id, ct); return value is null ? null : Map(value); }
    public Task<DisbursementPage> SearchAsync(DisbursementSearch search, CancellationToken ct) => store.SearchAsync(search with { PageNumber = Math.Max(1, search.PageNumber), PageSize = Math.Clamp(search.PageSize, 1, 100) }, ct);
    public Task ConsumeAsync(DisbursementCapacityReservedV1 message, CancellationToken cancellationToken = default) => store.ApplyReservedAsync(message, cancellationToken);
    public Task ConsumeAsync(DisbursementCapacityRejectedV1 message, CancellationToken cancellationToken = default) => store.ApplyRejectedAsync(message, cancellationToken);
    public static DisbursementDto Map(Disbursement x) => new(x.DisbursementId, x.LoanId, x.Amount, x.Currency, x.Beneficiary, x.SupportingDocuments.Select(d => d.DocumentId).ToArray(), x.Status.ToString(), x.CapacityReservationStatus.ToString(), x.CapacityRejectionReasonCode, x.CapacityRejectionReason, x.ActorUserId, x.CreatedAtUtc, x.UpdatedAtUtc, Convert.ToBase64String(x.RowVersion));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
public sealed class MissingIdempotencyKeyException : Exception;
public sealed class LoanNotFoundException : Exception;
public sealed class SupportingDocumentNotAccessibleException : Exception;
