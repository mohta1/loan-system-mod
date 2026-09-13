namespace LoanSystem.Modules.LoanOrigination.Domain;

public enum PropertyInspectionStatus { Draft, Recorded, Approved, Rejected }
public interface IPropertyInspectionDomainEvent;
public sealed record PropertyInspectionRecorded(Guid PropertyInspectionId, Guid LoanApplicationId, Guid ActorUserId, DateTimeOffset Timestamp) : IPropertyInspectionDomainEvent;
public sealed record PropertyInspectionApproved(Guid PropertyInspectionId, Guid LoanApplicationId, Guid ActorUserId, DateTimeOffset Timestamp) : IPropertyInspectionDomainEvent;
public sealed record PropertyInspectionRejected(Guid PropertyInspectionId, Guid LoanApplicationId, Guid ActorUserId, DateTimeOffset Timestamp, string Reason) : IPropertyInspectionDomainEvent;

public sealed class PropertyInspection
{
    private readonly List<IPropertyInspectionDomainEvent> _domainEvents = [];
    private PropertyInspection() { }
    public Guid Id { get; private set; }
    public Guid LoanApplicationId { get; private set; }
    public Guid InspectorId { get; private set; }
    public string Governorate { get; private set; } = "";
    public string State { get; private set; } = "";
    public string Area { get; private set; } = "";
    public int NumberOfFloors { get; private set; }
    public int NumberOfRooms { get; private set; }
    public decimal PropertyArea { get; private set; }
    public string PropertyCondition { get; private set; } = "";
    public DateOnly? InspectionDate { get; private set; }
    public string Result { get; private set; } = "";
    public string? Notes { get; private set; }
    public PropertyInspectionStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? FinalizedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyList<IPropertyInspectionDomainEvent> DomainEvents => _domainEvents;
    public static PropertyInspection Create(Guid applicationId, Guid inspectorId, DateTimeOffset? now = null) { if (applicationId == Guid.Empty || inspectorId == Guid.Empty) throw new InspectionValidationException("inspectorId"); var at = now ?? DateTimeOffset.UtcNow; return new() { Id = Guid.NewGuid(), LoanApplicationId = applicationId, InspectorId = inspectorId, Status = PropertyInspectionStatus.Draft, CreatedAtUtc = at, UpdatedAtUtc = at }; }
    public void Edit(string governorate, string state, string area, int floors, int rooms, decimal propertyArea, string condition, DateOnly? date, string result, string? notes, DateTimeOffset? now = null) { EnsureEditable(); Governorate = governorate?.Trim() ?? ""; State = state?.Trim() ?? ""; Area = area?.Trim() ?? ""; NumberOfFloors = floors; NumberOfRooms = rooms; PropertyArea = propertyArea; PropertyCondition = condition?.Trim() ?? ""; InspectionDate = date; Result = result?.Trim() ?? ""; Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(); UpdatedAtUtc = now ?? DateTimeOffset.UtcNow; }
    public PropertyInspectionRecorded Complete(Guid actor, DateTimeOffset? now = null) { EnsureActor(actor); EnsureEditable(); if (Status != PropertyInspectionStatus.Draft) throw new InspectionStateException(); ValidateComplete(); var at = now ?? DateTimeOffset.UtcNow; Status = PropertyInspectionStatus.Recorded; UpdatedAtUtc = at; var e = new PropertyInspectionRecorded(Id, LoanApplicationId, actor, at); _domainEvents.Add(e); return e; }
    public PropertyInspectionApproved Approve(Guid actor, DateTimeOffset? now = null) { EnsureActor(actor); EnsureRecorded(); var at = now ?? DateTimeOffset.UtcNow; Status = PropertyInspectionStatus.Approved; UpdatedAtUtc = at; FinalizedAtUtc = at; var e = new PropertyInspectionApproved(Id, LoanApplicationId, actor, at); _domainEvents.Add(e); return e; }
    public PropertyInspectionRejected Reject(Guid actor, string? reason, DateTimeOffset? now = null) { EnsureActor(actor); EnsureRecorded(); if (string.IsNullOrWhiteSpace(reason)) throw new InspectionRejectionReasonRequiredException(); var at = now ?? DateTimeOffset.UtcNow; Status = PropertyInspectionStatus.Rejected; UpdatedAtUtc = at; FinalizedAtUtc = at; var e = new PropertyInspectionRejected(Id, LoanApplicationId, actor, at, reason.Trim()); _domainEvents.Add(e); return e; }
    private static void EnsureActor(Guid actor) { if (actor == Guid.Empty) throw new InvalidActorException(); }
    private void EnsureEditable() { if (Status is PropertyInspectionStatus.Approved or PropertyInspectionStatus.Rejected) throw new InspectionFinalizedException(); }
    private void EnsureRecorded() { if (Status != PropertyInspectionStatus.Recorded) throw new InspectionStateException(); }
    private void ValidateComplete() { if (InspectorId == Guid.Empty) throw new InspectionValidationException("inspectorId"); if (Governorate.Length == 0) throw new InspectionValidationException("governorate"); if (State.Length == 0) throw new InspectionValidationException("state"); if (Area.Length == 0) throw new InspectionValidationException("area"); if (NumberOfFloors <= 0) throw new InspectionValidationException("numberOfFloors"); if (NumberOfRooms < 0) throw new InspectionValidationException("numberOfRooms"); if (PropertyArea <= 0) throw new InspectionValidationException("propertyArea"); if (PropertyCondition.Length == 0) throw new InspectionValidationException("propertyCondition"); if (InspectionDate is null) throw new InspectionValidationException("inspectionDate"); if (Result.Length == 0) throw new InspectionValidationException("result"); }
}
public sealed class InspectionValidationException(string field) : Exception(field) { public string Field { get; } = field; }
public sealed class InspectionStateException : Exception;
public sealed class InspectionFinalizedException : Exception;
public sealed class InspectionRejectionReasonRequiredException : Exception;
