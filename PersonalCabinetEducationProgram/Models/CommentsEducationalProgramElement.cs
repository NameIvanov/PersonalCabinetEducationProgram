namespace PersonalCabinetEducationProgram.Models
{
    public class CommentsEducationalProgramElement
    {
        public Guid Id { get; set; }
        public Guid LinkElement { get; set; }
        public Guid LinkUser { get; set; }
        public DateTime DateTimeComment { get; set; }
        public string CommentContent { get; set; }
        public string Status {  get; set; }
    }
}
