using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.ViewModels
{
    public class ElementListPageViewModel
    {
        public List<EducationalProgramElement> Elements { get; set; } = [];
        public List<string> Statuses { get; set; } = [];
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public string Sort { get; set; } = "name";
        public string Direction { get; set; } = "asc";
        public ElementListFiltersViewModel Filters { get; set; } = new();
    }
}
