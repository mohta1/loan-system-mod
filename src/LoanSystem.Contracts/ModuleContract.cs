namespace LoanSystem.Contracts;
/// <summary>Marker for behavior-free, cross-module public contracts.</summary>
public interface IModuleContract;

/// <summary>A narrow composition contract for retaining an import source without exposing storage details.</summary>
public interface IImportSourceDocumentStore
{
    Task<Guid> StoreAsync(string fileName, string contentType, long length, Stream content, Guid uploadedBy, CancellationToken cancellationToken);
    Task DiscardAsync(Guid documentId, Guid uploadedBy, CancellationToken cancellationToken);
}

public enum LoanProductVersionLookupStatus { Available, NotFound, Draft, ProductInactive, OutsideEffectivePeriod }
public sealed record LoanProductRankGradeRule(string RankGrade, decimal MaximumAmount);
public sealed record LoanProductEligibility(string RequiredNationality, int MaximumApplicationCount, IReadOnlyList<LoanProductRankGradeRule> RankGradeAmountRules, int MaximumTermMonths, string DueDateRule);
public sealed record LoanProductVersionContract(Guid LoanProductId, Guid LoanProductVersionId, string ProductName, int VersionNumber, string ProductStatus, decimal MaximumAmount, string Currency, decimal DeductionPercentage, IReadOnlyList<string> FinancingTypes, LoanProductEligibility EligibilityConfiguration, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string VersionStatus, DateTimeOffset? PublishedAtUtc);
public sealed record LoanProductVersionLookup(LoanProductVersionLookupStatus Status, LoanProductVersionContract? Version);
public interface ILoanProductsModule : IModuleContract { Task<LoanProductVersionLookup> GetVersionAsync(Guid versionId, DateOnly businessDate, CancellationToken cancellationToken = default); }

public sealed record BorrowerContract(Guid BorrowerId, string CivilNumber, string? EmployeeNumber, string FullName, string? PhoneNumber, string Nationality, string Organization, string? RankGrade, string? EmploymentInformation, bool IsActive);
public interface IBorrowersModule : IModuleContract { Task<BorrowerContract?> GetAsync(Guid borrowerId, CancellationToken cancellationToken = default); }

public sealed record DocumentReferenceContract(Guid DocumentId, string FileName, string ContentType, long Size, bool IsActive);
public interface IDocumentsModule : IModuleContract
{
    Task<DocumentReferenceContract?> GetAccessibleAsync(Guid documentId, Guid actorUserId, CancellationToken cancellationToken = default);
}

public sealed record LoanApplicationApprovedV1(Guid EventId, DateTimeOffset OccurredAtUtc, string CorrelationId, Guid LoanApplicationId, Guid BorrowerId, Guid LoanProductId, Guid LoanProductVersionId, decimal ApprovedAmount, string Currency, string FinancingType, Guid ActorUserId, DateTimeOffset ApprovedAtUtc);
public interface ILoanApplicationApprovedConsumer : IModuleContract { Task ConsumeAsync(LoanApplicationApprovedV1 message, CancellationToken cancellationToken = default); }
