using System.ComponentModel.DataAnnotations;

namespace PcStore.Web.Models
{
    public class Supplier
    {
        // --- ПОЛЯ ---

        // Идентифицатор поставщика
        [Display(Name = "ИД")]
        public int Id { get; set; }

        // Название поставщика
        [Display(Name = "Название")]
        [Required(ErrorMessage = "Введите название компании-поставщика")]
        public string Name { get; set; }

        // Контакты поставщика
        [Display(Name = "Контактная информация")]
        public string ContactInfo { get; set; }

        // --- СВЯЗИ ---

        // Один поставщик может поставлять много товаров
        public virtual List<Product> Products { get; set; } = new List<Product>();
    }
}