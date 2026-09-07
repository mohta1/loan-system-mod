using LoanSystem.Contracts;
using LoanSystem.Modules.LoanOrigination.Application;
using LoanSystem.Modules.LoanOrigination.Domain;
namespace LoanSystem.ApplicationTests;

public sealed class LoanApplicationServiceTests
{
    static BorrowerContract Borrower(bool active = true) => new(Guid.NewGuid(), "1", "E1", "Original", null, "OM", "MOD", "A", "Employment", active);
    static LoanProductVersionContract Product() => new(Guid.NewGuid(), Guid.NewGuid(), "Housing", 1, "Active", 1000m, "OMR", 10m, ["Build"], new("OM", 1, [new("A", 1000m)], 240, "Monthly"), DateOnly.FromDateTime(DateTime.UtcNow), null, "Published", DateTimeOffset.UtcNow);
    [Fact] public async Task Create_captures_contract_values() { var b = Borrower(); var p = Product(); var store = new Store(); var service = new LoanApplicationService(store, new Borrowers(b), new Products(new(LoanProductVersionLookupStatus.Available, p))); var result = await service.CreateAsync(new(b.BorrowerId, p.LoanProductVersionId, 500m, "Build"), default); Assert.Equal("Original", result.BorrowerSnapshot.FullName); Assert.Equal("Housing", result.ProductSnapshot.ProductName); Assert.Single(store.Values); }
    [Fact] public async Task Missing_and_inactive_borrowers_are_rejected() { var p = Product(); await Assert.ThrowsAsync<BorrowerUnavailableException>(() => new LoanApplicationService(new Store(), new Borrowers(null), new Products(new(LoanProductVersionLookupStatus.Available, p))).CreateAsync(new(Guid.NewGuid(), p.LoanProductVersionId, 1, "Build"), default)); var b = Borrower(false); await Assert.ThrowsAsync<BorrowerUnavailableException>(() => new LoanApplicationService(new Store(), new Borrowers(b), new Products(new(LoanProductVersionLookupStatus.Available, p))).CreateAsync(new(b.BorrowerId, p.LoanProductVersionId, 1, "Build"), default)); }
    [Theory][InlineData(LoanProductVersionLookupStatus.NotFound)][InlineData(LoanProductVersionLookupStatus.Draft)][InlineData(LoanProductVersionLookupStatus.ProductInactive)][InlineData(LoanProductVersionLookupStatus.OutsideEffectivePeriod)] public async Task Unavailable_product_is_rejected(LoanProductVersionLookupStatus status) { var b = Borrower(); await Assert.ThrowsAsync<ProductUnavailableException>(() => new LoanApplicationService(new Store(), new Borrowers(b), new Products(new(status, null))).CreateAsync(new(b.BorrowerId, Guid.NewGuid(), 1, "Build"), default)); }
    [Fact]
    public async Task Get_maps_existing_application_and_returns_null_for_unknown()
    {
        var store = new Store();
        var application = Application();
        store.Values.Add(application);
        var service = Service(store);

        var found = await service.GetAsync(application.Id, default);

        Assert.NotNull(found);
        Assert.Equal(application.Id, found.LoanApplicationId);
        Assert.Equal("Original", found.BorrowerSnapshot.FullName);
        Assert.Null(await service.GetAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Search_normalizes_pagination_before_delegating()
    {
        var store = new Store();
        var service = Service(store);

        await service.SearchAsync(new(null, null, null, null, 0, 101), default);

        Assert.NotNull(store.LastSearch);
        Assert.Equal(1, store.LastSearch.PageNumber);
        Assert.Equal(100, store.LastSearch.PageSize);
    }

    [Fact]
    public async Task Edit_updates_draft_preserves_snapshots_and_enforces_expected_version()
    {
        var store = new Store();
        var application = Application();
        store.Values.Add(application);
        var borrowerSnapshot = application.BorrowerSnapshot;
        var productSnapshot = application.ProductSnapshot;
        byte[] expected = [1, 2, 3];

        var result = await Service(store).EditAsync(application.Id, new(750m, "Renovate"), expected, default);

        Assert.NotNull(result);
        Assert.Equal(750m, result.RequestedAmount);
        Assert.Equal("Renovate", result.FinancingType);
        Assert.Same(borrowerSnapshot, application.BorrowerSnapshot);
        Assert.Same(productSnapshot, application.ProductSnapshot);
        Assert.Same(expected, store.ExpectedVersion);
        Assert.Same(application, store.ExpectedApplication);
        Assert.True(store.Saved);
    }

    [Fact]
    public async Task Edit_returns_null_without_saving_for_unknown_application()
    {
        var store = new Store();

        Assert.Null(await Service(store).EditAsync(Guid.NewGuid(), new(1m, "Build"), [1], default));
        Assert.False(store.Saved);
        Assert.Null(store.ExpectedApplication);
    }

    [Fact] public async Task Unit_decision_sets_expected_version_invokes_domain_and_maps_metadata() { var store = new Store(); var application = Application(); application.EvaluateEligibility(0); application.Submit(); store.Values.Add(application); var actor = Guid.NewGuid(); byte[] expected = [4, 5]; var result = await Service(store).DecideByUnitAsync(new(application.Id, actor, UnitDecision.Approved, " ok ", expected), default); Assert.NotNull(result); Assert.Equal("UnitApproved", result.Status); Assert.Equal(actor, result.UnitApproval!.ActorUserId); Assert.Equal("ok", result.UnitApproval.Comment); Assert.Same(expected, store.ExpectedVersion); Assert.True(store.Saved); }
    [Fact] public async Task Unit_decision_returns_null_without_saving_when_missing() { var store = new Store(); Assert.Null(await Service(store).DecideByUnitAsync(new(Guid.NewGuid(), Guid.NewGuid(), UnitDecision.Rejected, "reason", [1]), default)); Assert.False(store.Saved); }
    [Fact]
    public async Task Committee_decision_sets_expected_version_invokes_domain_and_maps_metadata()
    {
        var store = new Store();
        var application = Application();
        application.EvaluateEligibility(0);
        application.Submit();
        application.ApproveByUnit(Guid.NewGuid(), "unit");
        store.Values.Add(application);
        var actor = Guid.NewGuid();
        byte[] expected = [7, 8];

        var result = await Service(store).DecideByCommitteeAsync(new(application.Id, actor, CommitteeDecision.Approved, " committee ", expected), default);

        Assert.NotNull(result);
        Assert.Equal("CommitteeApproved", result.Status);
        Assert.Equal(actor, result.CommitteeApproval!.ActorUserId);
        Assert.Equal("committee", result.CommitteeApproval.Comment);
        Assert.Equal(UnitDecision.Approved, result.UnitApproval!.Decision);
        Assert.Same(expected, store.ExpectedVersion);
        Assert.Same(application, store.ExpectedApplication);
        Assert.True(store.Saved);
    }

    [Fact]
    public async Task Committee_rejection_maps_reason_and_missing_application_does_not_save()
    {
        var store = new Store();
        var application = Application();
        application.EvaluateEligibility(0);
        application.Submit();
        application.ApproveByUnit(Guid.NewGuid());
        store.Values.Add(application);

        var rejected = await Service(store).DecideByCommitteeAsync(new(application.Id, Guid.NewGuid(), CommitteeDecision.Rejected, " incomplete ", [9]), default);

        Assert.NotNull(rejected);
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("incomplete", rejected.CommitteeApproval!.RejectionReason);
        Assert.NotNull(rejected.RejectedAtUtc);

        var missingStore = new Store();
        Assert.Null(await Service(missingStore).DecideByCommitteeAsync(new(Guid.NewGuid(), Guid.NewGuid(), CommitteeDecision.Approved, null, [1]), default));
        Assert.False(missingStore.Saved);
        Assert.Null(missingStore.ExpectedApplication);
    }

    static LoanApplication Application() => LoanApplication.Create(Borrower().BorrowerId, Snapshot(), 500m, "Build", new("1", "E1", "Original", null, "OM", "MOD", "A", "Employment", "Active"));
    static ProductSnapshot Snapshot() { var product = Product(); return new(product.LoanProductId, product.LoanProductVersionId, product.ProductName, product.VersionNumber, product.MaximumAmount, product.Currency, product.DeductionPercentage, ["Build", "Renovate"], new("OM", 1, [new("A", 1000m)], 240, "Monthly"), product.EffectiveFrom, null, "Active", "Published", product.PublishedAtUtc); }
    static LoanApplicationService Service(Store store) => new(store, new Borrowers(Borrower()), new Products(new(LoanProductVersionLookupStatus.Available, Product())));
    sealed class Borrowers(BorrowerContract? value) : IBorrowersModule { public Task<BorrowerContract?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(value); }
    sealed class Products(LoanProductVersionLookup value) : ILoanProductsModule { public Task<LoanProductVersionLookup> GetVersionAsync(Guid id, DateOnly date, CancellationToken ct = default) => Task.FromResult(value); }
    sealed class Store : ILoanApplicationStore
    {
        public List<LoanApplication> Values { get; } = [];
        public LoanApplicationSearch? LastSearch { get; private set; }
        public LoanApplication? ExpectedApplication { get; private set; }
        public byte[]? ExpectedVersion { get; private set; }
        public bool Saved { get; private set; }
        public Task AddAsync(LoanApplication x, CancellationToken c) { Values.Add(x); return Task.CompletedTask; }
        public Task<LoanApplication?> FindAsync(Guid id, CancellationToken c) => Task.FromResult(Values.SingleOrDefault(x => x.Id == id));
        public Task<LoanApplicationPage> SearchAsync(LoanApplicationSearch s, CancellationToken c) { LastSearch = s; return Task.FromResult(new LoanApplicationPage([], s.PageNumber, s.PageSize, 0)); }
        public Task<int> CountConflictingApplicationsAsync(Guid borrowerId, Guid excludeApplicationId, CancellationToken c) => Task.FromResult(Values.Count(x => x.BorrowerId == borrowerId && x.Id != excludeApplicationId));
        public void Expect(LoanApplication x, byte[] v) { ExpectedApplication = x; ExpectedVersion = v; }
        public Task SaveAsync(CancellationToken c) { Saved = true; return Task.CompletedTask; }
    }
}
