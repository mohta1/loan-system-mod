using LoanSystem.Contracts;
using LoanSystem.Modules.LoanAccounts.Application;
using LoanSystem.Modules.LoanAccounts.Domain;

namespace LoanSystem.ApplicationTests;

public sealed class LoanAccountServiceTests
{
    [Fact]
    public async Task Consume_opens_account_from_approval_and_preserves_event_metadata()
    {
        var store = new Store();
        var service = new LoanAccountService(store);
        var eventId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);
        var approvedAt = occurredAt.AddMinutes(-1);
        var sourceApplicationId = Guid.NewGuid();
        var borrowerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var message = new LoanApplicationApprovedV1(eventId, occurredAt, "corr-1", sourceApplicationId, borrowerId, productId, versionId, 50000m, "omr", " Build ", actorId, approvedAt);

        await service.ConsumeAsync(message);

        Assert.Equal(actorId, message.ActorUserId);
        Assert.NotNull(store.Opened);
        Assert.Equal(sourceApplicationId, store.Opened.SourceApplicationId);
        Assert.Equal(borrowerId, store.Opened.BorrowerId);
        Assert.Equal(productId, store.Opened.LoanProductId);
        Assert.Equal(versionId, store.Opened.LoanProductVersionId);
        Assert.Equal(50000m, store.Opened.ApprovedAmount);
        Assert.Equal("OMR", store.Opened.Currency);
        Assert.Equal("Build", store.Opened.FinancingType);
        Assert.Equal(LoanAccountStatus.Active, store.Opened.Status);
        Assert.Equal(approvedAt, store.Opened.OpenedAtUtc);
        Assert.Equal(0m, store.Opened.ReservedDisbursementAmount);
        Assert.Equal(0m, store.Opened.TotalDisbursed);
        Assert.Equal(0m, store.Opened.TotalRepaid);
        Assert.Equal(50000m, store.Opened.AvailableToDisburse);
        Assert.Equal(0m, store.Opened.OutstandingBalance);
        Assert.Equal(eventId, store.EventId);
        Assert.Equal(occurredAt, store.OccurredAt);
    }

    [Fact]
    public async Task Get_maps_existing_account_and_returns_null_for_missing_account()
    {
        var account = LoanAccount.Open(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 25000m, "OMR", "Build", DateTimeOffset.UtcNow);
        var store = new Store { Found = account };
        var service = new LoanAccountService(store);

        var dto = await service.GetAsync(account.LoanId, default);

        Assert.NotNull(dto);
        Assert.Equal(account.LoanId, dto.LoanId);
        Assert.Equal(account.SourceApplicationId, dto.SourceApplicationId);
        Assert.Equal(25000m, dto.ApprovedAmount);
        Assert.Equal(25000m, dto.AvailableToDisburse);
        Assert.Equal(0m, dto.OutstandingBalance);
        Assert.Equal("Active", dto.Status);
        Assert.Equal(string.Empty, dto.ETag);

        store.Found = null;
        Assert.Null(await service.GetAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Search_normalizes_pagination_before_delegating()
    {
        var store = new Store();
        var service = new LoanAccountService(store);

        var page = await service.SearchAsync(new(null, null, null, null, 0, 500), default);

        Assert.NotNull(store.LastSearch);
        Assert.Equal(1, store.LastSearch.PageNumber);
        Assert.Equal(100, store.LastSearch.PageSize);
        Assert.Equal(1, page.PageNumber);
        Assert.Equal(100, page.PageSize);
    }

    [Fact]
    public async Task Find_by_source_delegates_to_store()
    {
        var sourceId = Guid.NewGuid();
        var account = LoanAccount.Open(sourceId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1000m, "OMR", "Build");
        var store = new Store { BySource = account };
        var service = new LoanAccountService(store);

        var found = await service.FindBySourceAsync(sourceId, default);

        Assert.Same(account, found);
        Assert.Equal(sourceId, store.SourceLookup);
    }

    private sealed class Store : ILoanAccountStore
    {
        public LoanAccount? Opened { get; private set; }
        public Guid EventId { get; private set; }
        public DateTimeOffset OccurredAt { get; private set; }
        public LoanAccount? Found { get; set; }
        public LoanAccount? BySource { get; set; }
        public Guid SourceLookup { get; private set; }
        public LoanAccountSearch? LastSearch { get; private set; }

        public Task<LoanAccount?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(Found);

        public Task<LoanAccount?> FindBySourceAsync(Guid sourceId, CancellationToken ct)
        {
            SourceLookup = sourceId;
            return Task.FromResult(BySource);
        }

        public Task<LoanAccountPage> SearchAsync(LoanAccountSearch search, CancellationToken ct)
        {
            LastSearch = search;
            return Task.FromResult(new LoanAccountPage([], search.PageNumber, search.PageSize, 0));
        }

        public Task OpenOnceAsync(LoanAccount account, Guid eventId, DateTimeOffset occurredAt, CancellationToken ct)
        {
            Opened = account;
            EventId = eventId;
            OccurredAt = occurredAt;
            return Task.CompletedTask;
        }
    }
}
