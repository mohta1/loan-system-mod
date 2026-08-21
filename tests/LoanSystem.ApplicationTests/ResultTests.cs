using LoanSystem.BuildingBlocks.Application;
namespace LoanSystem.ApplicationTests;

public sealed class ResultTests { [Fact] public void Result_represents_success_and_failure() { Assert.True(new Result(true).IsSuccess); Assert.Equal("invalid", new Result(false, "invalid").ErrorCode); } }
