using LoanSystem.Modules.Documents.Domain;
namespace LoanSystem.DomainTests;

public sealed class DocumentTests
{
    [Fact] public void Creates_metadata_without_binary_or_physical_path() { var d = Document.Create("proof.pdf", "application/pdf", 12, Guid.NewGuid().ToString("N"), Guid.NewGuid()); Assert.Equal("proof.pdf", d.FileName); Assert.Equal(12, d.Size); Assert.DoesNotContain(Path.DirectorySeparatorChar, d.StorageKey); Assert.Empty(typeof(Document).GetProperties().Where(x => x.PropertyType == typeof(byte[]))); }
    [Theory][InlineData("")][InlineData("bad\r\n.pdf")] public void Rejects_unsafe_metadata(string name) => Assert.Throws<ArgumentException>(() => Document.Create(name, "application/pdf", 1, Guid.NewGuid().ToString("N"), Guid.NewGuid()));
}
