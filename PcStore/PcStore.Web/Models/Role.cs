using System.ComponentModel.DataAnnotations;

namespace PcStore.Web.Models
{
    public class Role
    {
        // --- ПОЛЯ ---

        // Идентификатор роли
        [Display(Name = "ИД")]
        public int Id { get; set; }

        // Название роли
        [Display(Name = "Название")]
        [Required(ErrorMessage = "Введите название роли")]
        public string Name { get; set; }

        // --- СВЯЗИ ---

        // Одной и той же ролью могут обладать много пользователей
        public virtual List<User> Users { get; set; } = new List<User>();
    }
}