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
        public string TypeElement { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        [Column("upload_date")]
        public DateOnly? UploadDate { get; set; }

        public string Description { get; set; } = string.Empty;

        [Column("status_approvals")]
        public string StatusApprovals { get; set; } = string.Empty;

        [Column("file_path")]
        public string? FilePath { get; set; }

        [Column("file_name")]
        public string? FileName { get; set; }

        [Column("is_archived")]
        public bool IsArchived { get; set; }

        [Column("archived_at")]
        public DateTime? ArchivedAt { get; set; }

        [Column("archived_by_user_id")]
        public int? ArchivedByUserId { get; set; }

        [Column("version")]
        public int Version { get; set; } = 1;

        [Column("external_source")]
        [MaxLength(20)]
        public string? ExternalSource { get; set; }

        [Column("external_key")]
        [MaxLength(300)]
        public string? ExternalKey { get; set; }

        [Column("parent_external_key")]
        [MaxLength(300)]
        public string? ParentExternalKey { get; set; }

        [Column("last_imported_at")]
        public DateTime? LastImportedAt { get; set; }

        // Навигации
        public EducationalProgram EducationalProgram { get; set; } = null!;
        public ICollection<EducationalProgramElementComment> Comments { get; set; } = [];
        public ICollection<EducationalProgramElementFile> Files { get; set; } = [];
        public ICollection<Notification> Notifications { get; set; } = [];
    }
}
