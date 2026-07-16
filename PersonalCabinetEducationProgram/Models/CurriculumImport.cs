using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("curriculum_imports", Schema = "personal_cabinet")]
    public class CurriculumImport
    {
        [Key]
        public int Id { get; set; }

        [Column("educational_program_id")]
        public int EducationalProgramId { get; set; }

        [Column("imported_by_user_id")]
        public int ImportedByUserId { get; set; }

        [Column("original_file_name")]
        [MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Column("stored_file_path")]
        [MaxLength(500)]
        public string StoredFilePath { get; set; } = string.Empty;

        [Column("imported_at")]
        public DateTime ImportedAt { get; set; }

        [Column("plan_code")]
        [MaxLength(100)]
        public string PlanCode { get; set; } = string.Empty;

        [Column("plan_name")]
        [MaxLength(1000)]
        public string PlanName { get; set; } = string.Empty;

        [Column("source_app_version")]
        [MaxLength(50)]
        public string SourceAppVersion { get; set; } = string.Empty;

        [Column("created_count")]
        public int CreatedCount { get; set; }

        [Column("updated_count")]
        public int UpdatedCount { get; set; }

        [Column("archived_count")]
        public int ArchivedCount { get; set; }

        [Column("skipped_count")]
        public int SkippedCount { get; set; }

        [Column("warnings_json", TypeName = "longtext")]
        public string WarningsJson { get; set; } = "[]";

        [NotMapped]
        public IReadOnlyList<string> Warnings
        {
            get
            {
                try
                {
                    return JsonSerializer.Deserialize<List<string>>(WarningsJson) ?? [];
                }
                catch (JsonException)
                {
                    return [];
                }
            }
        }

        public EducationalProgram EducationalProgram { get; set; } = null!;
        public User ImportedByUser { get; set; } = null!;
    }
}
