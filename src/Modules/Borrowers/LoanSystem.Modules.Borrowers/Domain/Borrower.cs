namespace LoanSystem.Modules.Borrowers.Domain;
public enum BorrowerStatus { Active, Inactive }
public sealed class Borrower
{
    private Borrower() { }
    private Borrower(Guid id,string civil,string? employee,string name,string? phone,string nationality,string organization,string? rank,string? employment,DateTimeOffset now)
    { Id=id; CivilNumber=civil; EmployeeNumber=employee; FullName=name; PhoneNumber=phone; Nationality=nationality; Organization=organization; RankGrade=rank; EmploymentInformation=employment; Status=BorrowerStatus.Active; CreatedAt=UpdatedAt=now; }
    public Guid Id {get;private set;} public string CivilNumber {get;private set;}=""; public string? EmployeeNumber {get;private set;} public string FullName {get;private set;}=""; public string? PhoneNumber {get;private set;} public string Nationality {get;private set;}=""; public string Organization {get;private set;}=""; public string? RankGrade {get;private set;} public string? EmploymentInformation {get;private set;} public BorrowerStatus Status {get;private set;} public DateTimeOffset CreatedAt {get;private set;} public DateTimeOffset UpdatedAt {get;private set;} public byte[] RowVersion {get;private set;}=[];
    public static Borrower Register(string civil,string? employee,string name,string? phone,string nationality,string organization,string? rank,string? employment,DateTimeOffset? now=null) => new(Guid.NewGuid(),Required(civil,nameof(CivilNumber),100),Optional(employee,100),Required(name,nameof(FullName),200),Optional(phone,50),Required(nationality,nameof(Nationality),100),Required(organization,nameof(Organization),200),Optional(rank,100),Optional(employment,1000),now??DateTimeOffset.UtcNow);
    public void Update(string civil,string? employee,string name,string? phone,string nationality,string organization,string? rank,string? employment,DateTimeOffset? now=null) { CivilNumber=Required(civil,nameof(CivilNumber),100); EmployeeNumber=Optional(employee,100); FullName=Required(name,nameof(FullName),200); PhoneNumber=Optional(phone,50); Nationality=Required(nationality,nameof(Nationality),100); Organization=Required(organization,nameof(Organization),200); RankGrade=Optional(rank,100); EmploymentInformation=Optional(employment,1000); UpdatedAt=now??DateTimeOffset.UtcNow; }
    public void Activate(DateTimeOffset? now=null) { if(Status==BorrowerStatus.Active)return; Status=BorrowerStatus.Active; UpdatedAt=now??DateTimeOffset.UtcNow; }
    public void Deactivate(DateTimeOffset? now=null) { if(Status==BorrowerStatus.Inactive)return; Status=BorrowerStatus.Inactive; UpdatedAt=now??DateTimeOffset.UtcNow; }
    static string Required(string value,string field,int max) { var v=value?.Trim(); if(string.IsNullOrWhiteSpace(v)||v.Length>max)throw new BorrowerValidationException(field); return v; }
    static string? Optional(string? value,int max) { var v=value?.Trim(); if(string.IsNullOrEmpty(v))return null; if(v.Length>max)throw new BorrowerValidationException("length"); return v; }
}
public sealed class BorrowerValidationException(string field):Exception(field) { public string Field {get;}=field; }
