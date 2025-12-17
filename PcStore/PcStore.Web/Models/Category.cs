using System.ComponentModel.DataAnnotations;

namespace PcStore.Web.Models
{
    public class Category
    {
        // --- ПОЛЯ ---

        // Идентификатор категории
        [Display(Name = "ИД")]
        public int Id { get; set; }

        // Название категории
        [Display(Name = "Название")]
        [Required(ErrorMessage = "Введите название категории товаров")]
        public string Name { get; set; }

        // Описание категории
        [Display(Name = "Описание")]
        public string Description { get; set; }

        // --- СВЯЗИ ---

        // В одной категории может быть много товаров
        public virtual List<Product> Products { get; set; } = new List<Product>();
    }
}