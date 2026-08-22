using LoanSystem.Modules.Borrowers.Application;
using LoanSystem.Modules.Borrowers.Domain;

namespace LoanSystem.ApplicationTests;

public sealed class BorrowerApplicationTests
{
    private static readonly BorrowerInput Valid = new(" C1 ", " E1 ", " Ali ", " 9 ", " Omani ", " MOD ", " G7 ", " Staff ");

    [Fact]
    public async Task Registers_gets_updates_and_changes_status()
    {
        var store = new Store();
        var service = new BorrowerService(store);
        var created = await service.RegisterAsync(Valid, default);
        Assert.Equal("C1", created.CivilNumber);
        Assert.Equal(created, await service.GetAsync(created.BorrowerId, default));

        var updated = await service.UpdateAsync(created.BorrowerId, Valid with { FullName = "Changed" }, [1], default);
        Assert.Equal("Changed", updated!.FullName);
        Assert.Equal([1], store.Version);
        Assert.Equal("Inactive", (await service.StatusAsync(created.BorrowerId, false, [2], default))!.Status);
        Assert.Equal("Active", (await service.StatusAsync(created.BorrowerId, true, [3], default))!.Status);
    }

    [Fact]
    public async Task Missing_borrower_returns_null()
    {
        var service = new BorrowerService(new Store());
        Assert.Null(await service.GetAsync(Guid.NewGuid(), default));
        Assert.Null(await service.UpdateAsync(Guid.NewGuid(), Valid, [], default));
        Assert.Null(await service.StatusAsync(Guid.NewGuid(), true, [], default));
    }

    [Fact]
    public async Task Search_bounds_pagination()
    {
        var store = new Store();
        await new BorrowerService(store).SearchAsync(new(null, null, null, null, null, 0, 999), default);
        Assert.Equal(1, store.Search!.PageNumber);
        Assert.Equal(100, store.Search.PageSize);
    }

    [Fact]
    public async Task Friendly_uniqueness_checks_reject_conflicts()
    {
        var civil = new Store { CivilExists = true };
        var exception = await Assert.ThrowsAsync<BorrowerConflictException>(() => new BorrowerService(civil).RegisterAsync(Valid, default));
        Assert.Equal("borrowers.civilNumberConflict", exception.Code);

        var employee = new Store { EmployeeExists = true };
        exception = await Assert.ThrowsAsync<BorrowerConflictException>(() => new BorrowerService(employee).RegisterAsync(Valid, default));
        Assert.Equal("borrowers.employeeNumberConflict", exception.Code);
    }

    [Fact]
    public async Task Empty_employee_skips_employee_uniqueness_check()
    {
        var store = new Store { EmployeeExists = true };
        await new BorrowerService(store).RegisterAsync(Valid with { EmployeeNumber = " " }, default);
        Assert.False(store.EmployeeChecked);
    }

    [Fact]
    public async Task Provider_neutral_store_failures_are_preserved()
    {
        var error = new BorrowerConcurrencyException();
        var store = new Store { SaveError = error };
        Assert.Same(error, await Assert.ThrowsAsync<BorrowerConcurrencyException>(() => new BorrowerService(store).RegisterAsync(Valid, default)));
    }

    private sealed class Store : IBorrowerStore
    {
        public Borrower? Borrower { get; private set; }
        public bool CivilExists { get; init; }
        public bool EmployeeExists { get; init; }
        public bool EmployeeChecked { get; private set; }
        public byte[]? Version { get; private set; }
        public BorrowerSearch? Search { get; private set; }
        public Exception? SaveError { get; init; }
        public Task<bool> CivilExistsAsync(string value, Guid? except, CancellationToken ct) => Task.FromResult(CivilExists);
        public Task<bool> EmployeeExistsAsync(string value, Guid? except, CancellationToken ct) { EmployeeChecked = true; return Task.FromResult(EmployeeExists); }
        public Task AddAsync(Borrower borrower, CancellationToken ct) { Borrower = borrower; return Task.CompletedTask; }
        public Task<Borrower?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(Borrower?.Id == id ? Borrower : null);
        public Task<BorrowerPage> SearchAsync(BorrowerSearch search, CancellationToken ct) { Search = search; return Task.FromResult(new BorrowerPage([], search.PageNumber, search.PageSize, 0)); }
        public void ExpectVersion(Borrower borrower, byte[] version) => Version = version;
        public Task SaveAsync(CancellationToken ct) => SaveError is null ? Task.CompletedTask : Task.FromException(SaveError);
    }
}
