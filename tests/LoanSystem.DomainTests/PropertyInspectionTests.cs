using LoanSystem.Modules.LoanOrigination.Domain;
namespace LoanSystem.DomainTests;

public sealed class PropertyInspectionTests
{
    [Fact] public void Complete_and_approve_finalize_and_raise_events() { var x = Complete(); var at = DateTimeOffset.UnixEpoch.AddDays(2); x.Approve(Guid.NewGuid(), at); Assert.Equal(PropertyInspectionStatus.Approved, x.Status); Assert.Equal(at, x.FinalizedAtUtc); Assert.IsType<PropertyInspectionApproved>(x.DomainEvents[^1]); Assert.Throws<InspectionFinalizedException>(() => x.Edit("x", "x", "x", 1, 0, 1, "x", DateOnly.MinValue, "x", null)); }
    [Fact] public void Reject_requires_reason_and_finalizes_without_application_behavior() { var x = Complete(); Assert.Throws<InspectionRejectionReasonRequiredException>(() => x.Reject(Guid.NewGuid(), " ")); x.Reject(Guid.NewGuid(), " unsafe "); Assert.Equal(PropertyInspectionStatus.Rejected, x.Status); Assert.IsType<PropertyInspectionRejected>(x.DomainEvents[^1]); Assert.Throws<InspectionStateException>(() => x.Reject(Guid.NewGuid(), "again")); }
    [Fact] public void Draft_requires_complete_property_data() { var x = PropertyInspection.Create(Guid.NewGuid(), Guid.NewGuid()); Assert.Throws<InspectionValidationException>(() => x.Complete(Guid.NewGuid())); Assert.Throws<InspectionStateException>(() => x.Approve(Guid.NewGuid())); }
    [Fact] public void Recorded_remains_editable() { var x = Complete(); x.Edit("Dhofar", "Salalah", "Central", 3, 5, 300, "Excellent", new(2026, 9, 8), "Suitable", null); Assert.Equal("Dhofar", x.Governorate); }
    [Fact] public void Actor_attributed_actions_reject_empty_actor() { var draft = ValidDraft(); Assert.Throws<InvalidActorException>(() => draft.Complete(Guid.Empty)); var recorded = Complete(); Assert.Throws<InvalidActorException>(() => recorded.Approve(Guid.Empty)); Assert.Throws<InvalidActorException>(() => recorded.Reject(Guid.Empty, "reason")); }
    private static PropertyInspection ValidDraft() { var x = PropertyInspection.Create(Guid.NewGuid(), Guid.NewGuid()); x.Edit("Muscat", "Bawshar", "Khuwair", 2, 4, 250, "Good", new(2026, 9, 7), "Suitable", "notes"); return x; }
    private static PropertyInspection Complete() { var x = PropertyInspection.Create(Guid.NewGuid(), Guid.NewGuid()); x.Edit("Muscat", "Bawshar", "Khuwair", 2, 4, 250, "Good", new(2026, 9, 7), "Suitable", "notes"); x.Complete(Guid.NewGuid()); return x; }
}
