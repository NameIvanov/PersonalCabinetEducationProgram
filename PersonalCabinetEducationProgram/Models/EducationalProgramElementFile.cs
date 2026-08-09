using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("educational_program_element_files", Schema = "personal_cabinet")]
    public class EducationalProgramElementFile
    {
        [Key]
        public int Id { get; set; }

        [Column("educational_program_element_id")]
        public int EducationalProgramElementId { get; set; }

        [Column("stored_file_name")]
        public string StoredFileName { get; set; } = string.Empty;

        [Column("original_file_name")]
        public string OriginalFileName { get; set; } = string.Empty;

        [Column("revision_number")]
        public int RevisionNumber { get; set; }

        [Column("is_current")]
        public bool IsCurrent { get; set; }

        [Column("is_submitted")]
        public bool IsSubmitted { get; set; }

        [Column("is_removed")]
        public bool IsRemoved { get; set; }

        [Column("removed_at")]
        public DateTime? RemovedAt { get; set; }

        [Column("removed_by_user_id")]
        public int? RemovedByUserId { get; set; }

        [Column("removal_reason")]
        [MaxLength(100)]
        public string? RemovalReason { get; set; }

        [Column("uploaded_at")]
        public DateTime UploadedAt { get; set; }

        [Column("uploaded_by_user_id")]
        public int UploadedByUserId { get; set; }

        public EducationalProgramElement Element { get; set; } = null!;
        public User UploadedByUser { get; set; } = null!;
    }
}
