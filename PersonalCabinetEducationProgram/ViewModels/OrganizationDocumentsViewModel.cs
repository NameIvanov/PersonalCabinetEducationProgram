using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.ViewModels
{
    public class OrganizationDocumentsViewModel
    {
        public string PageTitle { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public List<EducationalProgram> Programs { get; set; } = new();
    }
}
