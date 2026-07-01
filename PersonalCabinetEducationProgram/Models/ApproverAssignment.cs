using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("approver_assignments", Schema = "personal_cabinet")]
    public class ApproverAssignment
    {
        [Key]
        public int Id { get; set; }

        [Column("approver_user_id")]
        public int ApproverUserId { get; set; }

        [Column("faculty_id")]
        public int? FacultyId { get; set; }

        [Column("department_id")]
        public int? DepartmentId { get; set; }

        [Column("assigned_by_user_id")]
        public int AssignedByUserId { get; set; }

        [Column("assigned_at")]
        public DateTime AssignedAt { get; set; }

        public User ApproverUser { get; set; }
        public User AssignedByUser { get; set; }
        public Facultys? Faculty { get; set; }
        public Departments? Department { get; set; }
    }
}
