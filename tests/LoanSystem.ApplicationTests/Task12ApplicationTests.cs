using LoanSystem.Contracts;
using LoanSystem.Modules.Disbursements.Application;
using LoanSystem.Modules.Disbursements.Domain;

namespace LoanSystem.ApplicationTests;

public sealed class Task12ApplicationTests
{
    [Fact]
    public async Task Create_uses_authoritative_currency_validates_documents_and_builds_safe_idempotency_data()
    {
        var loan = new Loan(Guid.NewGuid()); var document = Guid.NewGuid(); var store = new Store(); var service = new DisbursementService(store, loan, new Docs(document)); var actor = Guid.NewGuid();
        var result = await service.CreateAsync(new(loan.Value.LoanId, 10, new("Contractor", "Builder", "Holder", "Bank", "Account"), [document]), "secret-key", actor, "corr", default);
        Assert.Equal(IdempotentCreateOutcome.Created, result.Outcome); Assert.Equal("OMR", store.Created!.Currency); Assert.Equal("disbursements.create", store.Scope); Assert.DoesNotContain("secret-key", store.KeyHash); Assert.DoesNotContain("Account", store.RequestHash); Assert.Equal(actor, store.Actor); Assert.Single(store.Created.SupportingDocuments);
    }
    [Fact]
    public async Task Create_rejects_missing_key_loan_document_and_beneficiary()
    {
        var loan = new Loan(Guid.NewGuid()); var command = new CreateDisbursementCommand(loan.Value.LoanId, 10, new("Contractor", "A", "A", "B", "C"), []);
        await Assert.ThrowsAsync<MissingIdempotencyKeyException>(() => new DisbursementService(new Store(), loan, new Docs()).CreateAsync(command, "", Guid.NewGuid(), "c", default));
        await Assert.ThrowsAsync<LoanNotFoundException>(() => new DisbursementService(new Store(), new Loan(null), new Docs()).CreateAsync(command, "key", Guid.NewGuid(), "c", default));
        await Assert.ThrowsAsync<SupportingDocumentNotAccessibleException>(() => new DisbursementService(new Store(), loan, new Docs()).CreateAsync(command with { SupportingDocumentIds = [Guid.NewGuid()] }, "key", Guid.NewGuid(), "c", default));
        await Assert.ThrowsAsync<DisbursementValidationException>(() => new DisbursementService(new Store(), loan, new Docs()).CreateAsync(command with { Beneficiary = new("Other", "A", "A", "B", "C") }, "key", Guid.NewGuid(), "c", default));
    }
    private sealed class Loan : ILoanAccountsModule { public Loan(Guid? id) { Value = id.HasValue ? new(id.Value, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100, "OMR", "Build", 0, 0, 100, "Active", "v") : null!; } public LoanAccountContract Value { get; } public Task<LoanAccountContract?> GetAsync(Guid loanId, CancellationToken cancellationToken = default) => Task.FromResult<LoanAccountContract?>(Value); }
    private sealed class Docs(params Guid[] allowed) : IDocumentsModule { public Task<DocumentReferenceContract?> GetAccessibleAsync(Guid documentId, Guid actorUserId, CancellationToken cancellationToken = default) => Task.FromResult<DocumentReferenceContract?>(allowed.Contains(documentId) ? new(documentId, "x", "text/plain", 1, true) : null); }
    private sealed class Store : IDisbursementStore
    {
        public Disbursement? Created; public string Scope = ""; public Guid Actor; public string KeyHash = ""; public string RequestHash = "";
        public Task<IdempotentCreateResult> CreateAsync(Disbursement value, string scope, Guid actor, string keyHash, string requestHash, DisbursementCapacityRequestedV1 message, CancellationToken ct) { Created = value; Scope = scope; Actor = actor; KeyHash = keyHash; RequestHash = requestHash; return Task.FromResult(new IdempotentCreateResult(IdempotentCreateOutcome.Created, value)); }
        public Task<Disbursement?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult<Disbursement?>(Created); public Task<DisbursementPage> SearchAsync(DisbursementSearch search, CancellationToken ct) => Task.FromResult(new DisbursementPage([], 1, 25, 0)); public Task ApplyReservedAsync(DisbursementCapacityReservedV1 message, CancellationToken ct) => Task.CompletedTask; public Task ApplyRejectedAsync(DisbursementCapacityRejectedV1 message, CancellationToken ct) => Task.CompletedTask;
    }
}
