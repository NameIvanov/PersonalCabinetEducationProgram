using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("notifications", Schema = "personal_cabinet")]
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("educational_program_element_id")]
        public int EducationalProgramElementId { get; set; }

        [Column("actor_name")]
        public string ActorName { get; set; } = string.Empty;

        [Column("type")]
        public string Type { get; set; } = NotificationType.StatusChanged;

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("message")]
        public string Message { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("is_read")]
        public bool IsRead { get; set; }

        [Column("read_at")]
        public DateTime? ReadAt { get; set; }

        public User User { get; set; } = null!;
        public EducationalProgramElement Element { get; set; } = null!;
    }
}
