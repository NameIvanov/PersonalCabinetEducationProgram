namespace PersonalCabinetEducationProgram.Models
{
    public class EducationalPrograms
    {
        public Guid Id { get; set; }
        public string CodeReferral { get; set; }
        public string Name { get; set; }
        public string EducationLevel { get; set; }
        public DateOnly YearApprovals { get; set; }
        public string Status { get; set; }
        public Guid LinkDirector { get; set; }
    }
}
