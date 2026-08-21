namespace LoanSystem.BuildingBlocks.Domain;

public abstract class Entity<TId> where TId : notnull { public required TId Id { get; init; } }
