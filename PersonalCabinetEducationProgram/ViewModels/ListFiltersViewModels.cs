namespace PersonalCabinetEducationProgram.ViewModels
{
    public abstract class ListFiltersViewModel
    {
        public bool ShowFilters { get; set; }

        public abstract bool HasAnyFilter { get; }

        public Dictionary<string, string> ToRouteData()
        {
            var values = new Dictionary<string, string>();
            if (ShowFilters || HasAnyFilter)
                values[nameof(ShowFilters)] = bool.TrueString.ToLowerInvariant();

            AddRouteValues(values);
            return values;
        }

        protected abstract void AddRouteValues(Dictionary<string, string> values);

        protected static void Add(Dictionary<string, string> values, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                values[key] = value;
        }

        protected static void Add(Dictionary<string, string> values, string key, int? value)
        {
            if (value.HasValue)
                values[key] = value.Value.ToString();
        }

        protected static void Add(Dictionary<string, string> values, string key, DateOnly? value)
        {
            if (value.HasValue)
                values[key] = value.Value.ToString("yyyy-MM-dd");
        }
    }

    public sealed class UserListFiltersViewModel : ListFiltersViewModel
    {
        public int? Id { get; set; }
        public string? Login { get; set; }
        public string? FullName { get; set; }
        public string? Post { get; set; }
        public string? Role { get; set; }
        public string? ApprovalStatus { get; set; }

        public override bool HasAnyFilter =>
            Id.HasValue || !string.IsNullOrWhiteSpace(Login) || !string.IsNullOrWhiteSpace(FullName) ||
            !string.IsNullOrWhiteSpace(Post) || !string.IsNullOrWhiteSpace(Role) ||
            !string.IsNullOrWhiteSpace(ApprovalStatus);

        protected override void AddRouteValues(Dictionary<string, string> values)
        {
            Add(values, nameof(Id), Id);
            Add(values, nameof(Login), Login);
            Add(values, nameof(FullName), FullName);
            Add(values, nameof(Post), Post);
            Add(values, nameof(Role), Role);
            Add(values, nameof(ApprovalStatus), ApprovalStatus);
        }
    }

    public sealed class ProgramListFiltersViewModel : ListFiltersViewModel
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Level { get; set; }
        public int? Year { get; set; }
        public string? Department { get; set; }
        public string? Faculty { get; set; }
        public string? Status { get; set; }
        public string? Manager { get; set; }

        public override bool HasAnyFilter =>
            !string.IsNullOrWhiteSpace(Code) || !string.IsNullOrWhiteSpace(Name) ||
            !string.IsNullOrWhiteSpace(Level) || Year.HasValue || !string.IsNullOrWhiteSpace(Department) ||
            !string.IsNullOrWhiteSpace(Faculty) || !string.IsNullOrWhiteSpace(Status) ||
            !string.IsNullOrWhiteSpace(Manager);

        protected override void AddRouteValues(Dictionary<string, string> values)
        {
            Add(values, nameof(Code), Code);
            Add(values, nameof(Name), Name);
            Add(values, nameof(Level), Level);
            Add(values, nameof(Year), Year);
            Add(values, nameof(Department), Department);
            Add(values, nameof(Faculty), Faculty);
            Add(values, nameof(Status), Status);
            Add(values, nameof(Manager), Manager);
        }
    }

    public sealed class DepartmentListFiltersViewModel : ListFiltersViewModel
    {
        public int? Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }

        public override bool HasAnyFilter => Id.HasValue || !string.IsNullOrWhiteSpace(Code) || !string.IsNullOrWhiteSpace(Name);

        protected override void AddRouteValues(Dictionary<string, string> values)
        {
            Add(values, nameof(Id), Id);
            Add(values, nameof(Code), Code);
            Add(values, nameof(Name), Name);
        }
    }

    public sealed class FacultyListFiltersViewModel : ListFiltersViewModel
    {
        public int? Id { get; set; }
        public string? Name { get; set; }

        public override bool HasAnyFilter => Id.HasValue || !string.IsNullOrWhiteSpace(Name);

        protected override void AddRouteValues(Dictionary<string, string> values)
        {
            Add(values, nameof(Id), Id);
            Add(values, nameof(Name), Name);
        }
    }

    public sealed class AssignmentListFiltersViewModel : ListFiltersViewModel
    {
        public DateOnly? DateFrom { get; set; }
        public DateOnly? DateTo { get; set; }
        public string? User { get; set; }
        public string? AssignmentType { get; set; }
        public string? Target { get; set; }
        public string? Author { get; set; }

        public override bool HasAnyFilter => DateFrom.HasValue || DateTo.HasValue ||
            !string.IsNullOrWhiteSpace(User) || !string.IsNullOrWhiteSpace(AssignmentType) ||
            !string.IsNullOrWhiteSpace(Target) || !string.IsNullOrWhiteSpace(Author);

        protected override void AddRouteValues(Dictionary<string, string> values)
        {
            Add(values, nameof(DateFrom), DateFrom);
            Add(values, nameof(DateTo), DateTo);
            Add(values, nameof(User), User);
            Add(values, nameof(AssignmentType), AssignmentType);
            Add(values, nameof(Target), Target);
            Add(values, nameof(Author), Author);
        }
    }

    public sealed class AuditListFiltersViewModel : ListFiltersViewModel
    {
        public DateOnly? DateFrom { get; set; }
        public DateOnly? DateTo { get; set; }
        public string? User { get; set; }
        public string? Entity { get; set; }
        public string? Action { get; set; }
        public string? Details { get; set; }

        public override bool HasAnyFilter => DateFrom.HasValue || DateTo.HasValue ||
            !string.IsNullOrWhiteSpace(User) || !string.IsNullOrWhiteSpace(Entity) ||
            !string.IsNullOrWhiteSpace(Action) || !string.IsNullOrWhiteSpace(Details);

        protected override void AddRouteValues(Dictionary<string, string> values)
        {
            Add(values, nameof(DateFrom), DateFrom);
            Add(values, nameof(DateTo), DateTo);
            Add(values, nameof(User), User);
            Add(values, nameof(Entity), Entity);
            Add(values, nameof(Action), Action);
            Add(values, nameof(Details), Details);
        }
    }

    public sealed class OrganizationDocumentFiltersViewModel : ListFiltersViewModel
    {
        public string? Program { get; set; }
        public string? Type { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public DateOnly? DateFrom { get; set; }
        public DateOnly? DateTo { get; set; }

        public bool HasElementFilter => !string.IsNullOrWhiteSpace(Type) || !string.IsNullOrWhiteSpace(Name) ||
            !string.IsNullOrWhiteSpace(Description) || !string.IsNullOrWhiteSpace(Status) ||
            DateFrom.HasValue || DateTo.HasValue;

        public override bool HasAnyFilter => !string.IsNullOrWhiteSpace(Program) || HasElementFilter;

        protected override void AddRouteValues(Dictionary<string, string> values)
        {
            Add(values, nameof(Program), Program);
            Add(values, nameof(Type), Type);
            Add(values, nameof(Name), Name);
            Add(values, nameof(Description), Description);
            Add(values, nameof(Status), Status);
            Add(values, nameof(DateFrom), DateFrom);
            Add(values, nameof(DateTo), DateTo);
        }
    }

    public sealed class NotificationListFiltersViewModel : ListFiltersViewModel
    {
        public const string Read = "read";
        public const string Unread = "unread";

        public string? Title { get; set; }
        public string? Program { get; set; }
        public string? Element { get; set; }
        public string? Actor { get; set; }
        public string? ReadStatus { get; set; }
        public DateOnly? DateFrom { get; set; }
        public DateOnly? DateTo { get; set; }

        public override bool HasAnyFilter => !string.IsNullOrWhiteSpace(Title) ||
            !string.IsNullOrWhiteSpace(Program) || !string.IsNullOrWhiteSpace(Element) ||
            !string.IsNullOrWhiteSpace(Actor) || !string.IsNullOrWhiteSpace(ReadStatus) ||
            DateFrom.HasValue || DateTo.HasValue;

        protected override void AddRouteValues(Dictionary<string, string> values)
        {
            Add(values, nameof(Title), Title);
            Add(values, nameof(Program), Program);
            Add(values, nameof(Element), Element);
            Add(values, nameof(Actor), Actor);
            Add(values, nameof(ReadStatus), ReadStatus);
            Add(values, nameof(DateFrom), DateFrom);
            Add(values, nameof(DateTo), DateTo);
        }
    }

    public sealed class CurriculumImportListFiltersViewModel : ListFiltersViewModel
    {
        public string? FileName { get; set; }
        public string? PlanCode { get; set; }
        public string? Author { get; set; }
        public DateOnly? DateFrom { get; set; }
        public DateOnly? DateTo { get; set; }

        public override bool HasAnyFilter => !string.IsNullOrWhiteSpace(FileName) ||
            !string.IsNullOrWhiteSpace(PlanCode) || !string.IsNullOrWhiteSpace(Author) ||
            DateFrom.HasValue || DateTo.HasValue;

        protected override void AddRouteValues(Dictionary<string, string> values)
        {
            Add(values, nameof(FileName), FileName);
            Add(values, nameof(PlanCode), PlanCode);
            Add(values, nameof(Author), Author);
            Add(values, nameof(DateFrom), DateFrom);
            Add(values, nameof(DateTo), DateTo);
        }
    }

    public sealed class LiveFilterFormViewModel
    {
        public required string FormId { get; set; }
        public required Dictionary<string, string> PreservedRouteValues { get; set; }
    }
}
