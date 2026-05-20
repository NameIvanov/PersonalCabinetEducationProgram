namespace PersonalCabinetEducationProgram.Models
{
    public class EducationalProgramElements
    {
        public Guid Id { get; set; }
        public Guid LinkEducationProgram {  get; set; }
        public string TypeElement { get; set; }
        public string Name {  get; set; }
        public DateOnly UploadDate { get; set; }
        public string Description { get; set; }
        public string StatusApprovals   { get; set; }
    }
}
