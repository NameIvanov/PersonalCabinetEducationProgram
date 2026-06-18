using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("element_status_history", Schema = "personal_cabinet")]
    public class ElementStatusHistory
    {
        [Key]
        public int Id { get; set; }

        [Column("educational_program_element_id")]
        public int EducationalProgramElementId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("old_status")]
        public string OldStatus { get; set; }

        [Column("new_status")]
        public string NewStatus { get; set; }

        [Column("change_date")]
        public DateTime ChangeDate { get; set; }

        [Column("comment")]
        public string Comment { get; set; }

        public EducationalProgramElement Element { get; set; }
        public User User { get; set; }
    }
}
