using LoanSystem.BuildingBlocks.Domain;
namespace LoanSystem.DomainTests;

public sealed class BuildingBlockTests { [Fact] public void Entity_exposes_strong_identifier() { var entity = new Example { Id = Guid.NewGuid() }; Assert.NotEqual(Guid.Empty, entity.Id); } private sealed class Example : Entity<Guid>; }
