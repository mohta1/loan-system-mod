using LoanSystem.Modules.Borrowers.Domain;
namespace LoanSystem.DomainTests;

public sealed class BorrowerTests
{
    [Fact] public void Register_normalizes_and_starts_active() { var b = Borrower.Register(" 123 ", " E1 ", " Name ", null, " Omani ", " MOD ", " Grade ", null); Assert.Equal("123", b.CivilNumber); Assert.Equal("E1", b.EmployeeNumber); Assert.Equal(BorrowerStatus.Active, b.Status); }
    [Theory][InlineData("", "Name", "Omani", "MOD")][InlineData("1", "", "Omani", "MOD")][InlineData("1", "Name", "", "MOD")][InlineData("1", "Name", "Omani", "")] public void Required_fields_are_enforced(string civil, string name, string nationality, string organization) => Assert.Throws<BorrowerValidationException>(() => Borrower.Register(civil, null, name, null, nationality, organization, null, null));
    [Fact] public void Status_transitions_preserve_identity() { var b = Borrower.Register("1", null, "Name", null, "Omani", "MOD", null, null); b.Deactivate(); Assert.Equal(BorrowerStatus.Inactive, b.Status); b.Activate(); Assert.Equal(BorrowerStatus.Active, b.Status); }
    [Fact] public void Update_changes_master_data() { var b = Borrower.Register("1", null, "Name", null, "Omani", "MOD", null, null); b.Update("1", "2", "Changed", "3", "Omani", "Other", "G", "Info"); Assert.Equal("Changed", b.FullName); Assert.Equal("Other", b.Organization); }
    [Fact]
    public void Ef_constructor_and_validation_exception_are_usable()
    {
        Assert.NotNull(Activator.CreateInstance(typeof(Borrower), nonPublic: true));
        Assert.Equal("field", new BorrowerValidationException("field").Field);
    }
}
