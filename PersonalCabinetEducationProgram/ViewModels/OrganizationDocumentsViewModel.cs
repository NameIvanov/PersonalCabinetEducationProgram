using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.ViewModels
{
    public class OrganizationDocumentsViewModel
    {
        public string PageTitle { get; set; }
        public string EntityType { get; set; }
        public int EntityId { get; set; }
        public string EntityName { get; set; }
        public List<EducationalProgram> Programs { get; set; } = new();
    }
}
