using LoanSystem.Contracts;
using LoanSystem.Modules.LoanOrigination.Application;
using LoanSystem.Modules.LoanOrigination.Domain;

namespace LoanSystem.ApplicationTests;

public sealed class Task11ApplicationServiceTests
{
    [Fact]
    public async Task Final_approve_enforces_expected_version_persists_and_adds_correlated_outbox_message()
    {
        var application = ReadyForFinalApproval();
        var store = new Store(application);
        var service = new LoanApplicationService(store, new Borrowers(), new Products());
        var actor = Guid.NewGuid();
        byte[] expected = [7, 8, 9];

        var result = await service.FinalApproveAsync(application.Id, actor, "corr-task11", expected, default);

        Assert.NotNull(result);
        Assert.Equal("Approved", result.Status);
        Assert.Same(expected, store.ExpectedVersion);
        Assert.Same(application, store.ExpectedApplication);
        Assert.True(store.Saved);
        Assert.NotNull(store.Approval);
        Assert.Equal(application.Id, store.Approval.LoanApplicationId);
        Assert.Equal(application.BorrowerId, store.Approval.BorrowerId);
        Assert.Equal(application.LoanProductId, store.Approval.LoanProductId);
        Assert.Equal(application.LoanProductVersionId, store.Approval.LoanProductVersionId);
        Assert.Equal(application.RequestedAmount, store.Approval.ApprovedAmount);
        Assert.Equal(actor, store.Approval.ActorUserId);
        Assert.Equal("corr-task11", store.CorrelationId);
    }

    [Fact]
    public async Task Final_approve_returns_null_without_version_or_save_when_application_is_missing()
    {
        var store = new Store();
        var service = new LoanApplicationService(store, new Borrowers(), new Products());

        var result = await service.FinalApproveAsync(Guid.NewGuid(), Guid.NewGuid(), "corr-missing", [1], default);

        Assert.Null(result);
        Assert.Null(store.ExpectedApplication);
        Assert.Null(store.Approval);
        Assert.False(store.Saved);
    }

    private static LoanApplication ReadyForFinalApproval()
    {
        var product = new ProductSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Housing",
            1,
            100000m,
            "OMR",
            10m,
            ["Build"],
            new("OM", 1, [new("A", 100000m)], 240, "Monthly"),
            DateOnly.MinValue,
            null,
            "Active",
            "Published",
            DateTimeOffset.UtcNow);
        var application = LoanApplication.Create(
            Guid.NewGuid(),
            product,
            50000m,
            "Build",
            new("C1", "E1", "TASK-11", null, "OM", "MOD", "A", "Active", "Active"));
        application.EvaluateEligibility(0);
        application.Submit();
        application.ApproveByUnit(Guid.NewGuid());
        application.ApproveByCommittee(Guid.NewGuid());
        application.BeginPrerequisites();
        application.MarkInspectionApproved();
        application.AttachDocument(Guid.NewGuid(), LoanApplicationDocumentType.Ownership);
        application.AttachDocument(Guid.NewGuid(), LoanApplicationDocumentType.Survey);
        application.AttachDocument(Guid.NewGuid(), LoanApplicationDocumentType.EngineeringDrawing);
        application.CompleteMortgage(Guid.NewGuid());
        Assert.Equal(LoanApplicationStatus.ReadyForFinalApproval, application.Status);
        return application;
    }

    private sealed class Borrowers : IBorrowersModule
    {
        public Task<BorrowerContract?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult<BorrowerContract?>(null);
    }

    private sealed class Products : ILoanProductsModule
    {
        public Task<LoanProductVersionLookup> GetVersionAsync(Guid id, DateOnly date, CancellationToken ct = default) => Task.FromResult(new LoanProductVersionLookup(LoanProductVersionLookupStatus.NotFound, null));
    }

    private sealed class Store(params LoanApplication[] values) : ILoanApplicationStore
    {
        private readonly List<LoanApplication> _values = [.. values];
        public LoanApplication? ExpectedApplication { get; private set; }
        public byte[]? ExpectedVersion { get; private set; }
        public LoanApplicationApproved? Approval { get; private set; }
        public string? CorrelationId { get; private set; }
        public bool Saved { get; private set; }

        public Task AddAsync(LoanApplication value, CancellationToken ct)
        {
            _values.Add(value);
            return Task.CompletedTask;
        }

        public Task<LoanApplication?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(_values.SingleOrDefault(x => x.Id == id));
        public Task<LoanApplicationPage> SearchAsync(LoanApplicationSearch search, CancellationToken ct) => Task.FromResult(new LoanApplicationPage([], search.PageNumber, search.PageSize, 0));
        public Task<int> CountConflictingApplicationsAsync(Guid borrowerId, Guid excludeApplicationId, CancellationToken ct) => Task.FromResult(0);

        public void Expect(LoanApplication value, byte[] expected)
        {
            ExpectedApplication = value;
            ExpectedVersion = expected;
        }

        public void AddApprovalOutbox(LoanApplicationApproved approval, string correlationId)
        {
            Approval = approval;
            CorrelationId = correlationId;
        }

        public Task SaveAsync(CancellationToken ct)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }
}
