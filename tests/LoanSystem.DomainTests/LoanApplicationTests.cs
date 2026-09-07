using LoanSystem.Modules.LoanOrigination.Domain;
namespace LoanSystem.DomainTests;

public sealed class LoanApplicationTests
{
    static ProductSnapshot Product(string[]? types = null) => new(Guid.NewGuid(), Guid.NewGuid(), "Housing", 1, 100000m, "OMR", 10m, types ?? ["Build"], new("OM", 1, [new("A", 100000m)], 240, "Monthly"), new(2026, 1, 1), null, "Active", "Published", DateTimeOffset.UtcNow);
    static BorrowerSnapshot Borrower() => new("1", "E1", "Original", null, "OM", "MOD", "A", "Active employee", "Active");
    [Fact] public void New_application_is_draft_and_owns_snapshot_collections() { var types = new[] { "Build" }; var a = LoanApplication.Create(Guid.NewGuid(), Product(types), 100m, "Build", Borrower()); types[0] = "Other"; Assert.Equal(LoanApplicationStatus.Draft, a.Status); Assert.Equal("Build", a.ProductSnapshot.FinancingTypes.Single()); }
    [Theory][InlineData(0)][InlineData(-1)][InlineData(100001)] public void Invalid_amount_is_rejected(decimal amount) => Assert.Throws<LoanApplicationValidationException>(() => LoanApplication.Create(Guid.NewGuid(), Product(), amount, "Build", Borrower()));
    [Fact] public void Financing_type_must_belong_to_version() => Assert.Throws<LoanApplicationValidationException>(() => LoanApplication.Create(Guid.NewGuid(), Product(), 100m, "Purchase", Borrower()));
    [Fact] public void Draft_edit_changes_only_editable_values() { var a = LoanApplication.Create(Guid.NewGuid(), Product(), 100m, "Build", Borrower()); var snapshot = a.BorrowerSnapshot; a.EditDraft(200m, "Build"); Assert.Equal(200m, a.RequestedAmount); Assert.Same(snapshot, a.BorrowerSnapshot); Assert.Equal(LoanApplicationStatus.Draft, a.Status); }
}
