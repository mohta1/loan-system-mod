namespace LoanSystem.BuildingBlocks.Application;

public readonly record struct Result(bool IsSuccess, string? ErrorCode = null);
