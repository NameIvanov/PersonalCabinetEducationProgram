using System.ComponentModel.DataAnnotations;

namespace PersonalCabinetEducationProgram.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "ФИО")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Должность")]
        public string Post { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Логин")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [MinLength(6)]
        [Display(Name = "Пароль")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Пароли не совпадают")]
        [Display(Name = "Подтверждение пароля")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Роль")]
        public string Role { get; set; } = string.Empty;
    }
}
