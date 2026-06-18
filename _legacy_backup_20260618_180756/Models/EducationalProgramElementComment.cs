using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("comments_educational_program_element", Schema = "personal_cabinet")]
    public class EducationalProgramElementComment
    {
        [Key]
        public int Id { get; set; }

        [Column("educational_program_element_id")]
        public int EducationalProgramElementId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("date_time_comment")]
        public DateTime DateTimeComment { get; set; }

        [Column("comment_content")]
        public string CommentContent { get; set; }

        public string Status { get; set; }

        // Навигации
        public EducationalProgramElement Element { get; set; }
        public User User { get; set; }
    }
}
