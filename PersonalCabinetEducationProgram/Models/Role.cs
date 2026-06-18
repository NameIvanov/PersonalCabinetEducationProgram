using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("roles")]
    public class Role : IdentityRole<int>
    {
        [Column("Description")]
        public string Description { get; set; } = string.Empty;
    }
}
