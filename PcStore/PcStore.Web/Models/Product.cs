using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PcStore.Web.Models
{
    public class Product
    {
        // --- ПОЛЯ ---

        // Идентификатор товара
        [Display(Name = "ИД")]
        public int Id { get; set; }

        // Название товара
        [Display(Name = "Название")]
        [Required(ErrorMessage = "Введите название товара")]
        public string Name { get; set; }

        // Единица складского учёта (артикул)
        [Display(Name = "Артикул")]
        [Required(ErrorMessage = "Введите артикул товара")]
        public string Sku { get; set; }

        // Описание товара
        [Display(Name = "Описание")]
        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        // Цена товара
        [Display(Name = "Цена")]
        [Required(ErrorMessage = "Введите цену")]
        [Range(0.01, 999999999.99, ErrorMessage = "Недопустимая цена")]
        [Column(TypeName = "decimal(11,2)")]
        public decimal Price { get; set; }

        // Количество товара на складе
        [Display(Name = "Количество на складе")]
        [Required(ErrorMessage = "Введите количество товара на складе")]
        [Range(0, 10000, ErrorMessage = "Недопустимое количество")]
        public int QuantityInStock { get; set; }

        // --- СВЯЗИ ---

        // Внешний ключ на категорию
        public int CategoryId { get; set; }
        [Display(Name = "Категория")]
        public virtual Category Category { get; set; }

        // Внешний ключ на поставщика
        public int SupplierId { get; set; }
        [Display(Name = "Поставщик")]
        public virtual Supplier Supplier { get; set; }
    }
}