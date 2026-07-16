using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.ViewModels
{
    public class ElementListFiltersViewModel
    {
        public const string NotUploadedFilterValue = "__not_uploaded";

        public bool ShowFilters { get; set; }

        public string? MainName { get; set; }
        public string? MainStatus { get; set; }
        public DateOnly? MainDateFrom { get; set; }
        public DateOnly? MainDateTo { get; set; }

        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
        public DateOnly? DateFrom { get; set; }
        public DateOnly? DateTo { get; set; }

        public ElementColumnFilter Main => new()
        {
            SearchText = MainName,
            Status = MainStatus,
            DateFrom = MainDateFrom,
            DateTo = MainDateTo
        };

        public ElementColumnFilter Tab => new()
        {
            Description = Code,
            Name = Name,
            TypeElement = Type,
            Status = Status,
            DateFrom = DateFrom,
            DateTo = DateTo
        };

        public bool HasAnyFilter => Main.HasAnyFilter || Tab.HasAnyFilter;

        public Dictionary<string, string> ToRouteData(bool includeMain = true, bool includeTab = true)
        {
            var values = new Dictionary<string, string>();
            if (ShowFilters || HasAnyFilter)
                values[nameof(ShowFilters)] = bool.TrueString.ToLowerInvariant();

            if (includeMain)
            {
                Add(values, nameof(MainName), MainName);
                Add(values, nameof(MainStatus), MainStatus);
                Add(values, nameof(MainDateFrom), MainDateFrom?.ToString("yyyy-MM-dd"));
                Add(values, nameof(MainDateTo), MainDateTo?.ToString("yyyy-MM-dd"));
            }

            if (includeTab)
            {
                Add(values, nameof(Code), Code);
                Add(values, nameof(Name), Name);
                Add(values, nameof(Type), Type);
                Add(values, nameof(Status), Status);
                Add(values, nameof(DateFrom), DateFrom?.ToString("yyyy-MM-dd"));
                Add(values, nameof(DateTo), DateTo?.ToString("yyyy-MM-dd"));
            }

            return values;
        }

        private static void Add(Dictionary<string, string> values, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                values[key] = value;
        }

        public static readonly string[] Statuses =
        [
            NotUploadedFilterValue,
            ElementApprovalStatus.Uploaded,
            ElementApprovalStatus.OnApproval,
            ElementApprovalStatus.Approved,
            ElementApprovalStatus.RevisionRequired,
            ElementApprovalStatus.Published
        ];

        public static string GetStatusLabel(string status) =>
            status == NotUploadedFilterValue ? "Не загружено" : status;
    }

    public class ElementFilterUiViewModel
    {
        public required ElementListFiltersViewModel Filters { get; set; }
        public required string FormId { get; set; }
        public required string ResetUrl { get; set; }
        public required Dictionary<string, string> PreservedRouteValues { get; set; }
        public bool IsMain { get; set; }
    }

    public class ElementColumnFilter
    {
        public string? SearchText { get; set; }
        public string? TypeElement { get; set; }
        public string? Description { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public DateOnly? DateFrom { get; set; }
        public DateOnly? DateTo { get; set; }

        public bool HasTextFilter =>
            !string.IsNullOrWhiteSpace(SearchText) ||
            !string.IsNullOrWhiteSpace(Description) ||
            !string.IsNullOrWhiteSpace(Name);

        public bool HasAnyFilter =>
            HasTextFilter || !string.IsNullOrWhiteSpace(TypeElement) ||
            !string.IsNullOrWhiteSpace(Status) || DateFrom.HasValue || DateTo.HasValue;
    }
}
