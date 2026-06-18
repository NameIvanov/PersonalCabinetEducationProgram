using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("educational_program_elements", Schema = "personal_cabinet")]
    public class EducationalProgramElement
    {
        [Key]
        public int Id { get; set; }

        [Column("educational_program_id")]
        public int EducationalProgramId { get; set; }

        [Column("type_element")]
        public string TypeElement { get; set; }

        public string Name { get; set; }

        [Column("upload_date")]
        public DateOnly? UploadDate { get; set; }

        public string Description { get; set; }

        [Column("status_approvals")]
        public string StatusApprovals { get; set; }

        [Column("file_path")]
        public string? FilePath { get; set; }

        [Column("file_name")]
        public string? FileName { get; set; }

        // Навигации
        public EducationalProgram EducationalProgram { get; set; }
        public ICollection<EducationalProgramElementComment> Comments { get; set; }
    }
}
