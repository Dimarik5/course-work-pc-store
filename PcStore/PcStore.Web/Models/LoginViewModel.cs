using System.ComponentModel.DataAnnotations;

namespace PcStore.Web.Models
{
    public class LoginViewModel
    {
        // --- ПОЛЯ ---

        // Логин
        [Display(Name = "Логин")]
        [Required(ErrorMessage = "Введите логин")]
        public string Login { get; set; }

        // Пароль
        [Display(Name = "Пароль")]
        [Required(ErrorMessage = "Введите пароль")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}