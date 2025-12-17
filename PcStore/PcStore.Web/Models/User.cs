using System.ComponentModel.DataAnnotations;

namespace PcStore.Web.Models
{
    public class User
    {
        // --- ПОЛЯ ---

        // Идентификатор пользователя
        [Display(Name = "ИД")]
        public int Id { get; set; }

        //ФИО Пользователя
        [Display(Name = "ФИО")]
        [Required(ErrorMessage = "Введите ФИО")]
        public string FullName { get; set; }

        // Логин пользователя
        [Display(Name = "Логин")]
        [Required(ErrorMessage = "Введите логин")]
        public string Login { get; set; }

        // Пароль пользователя
        [Display(Name = "Пароль")]
        [Required(ErrorMessage = "Введите пароль")]
        public string Password { get; set; }

        // --- СВЯЗИ ---

        // Внешний ключ на роль
        [Display(Name = "Роль")]
        public int RoleId { get; set; }
        public virtual Role Role { get; set; }
    }
}