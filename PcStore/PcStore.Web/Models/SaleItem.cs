using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PcStore.Web.Models
{
    public class SaleItem
    {
        // --- ПОЛЯ ---

        // Идентификатор позиции в продаже
        [Display(Name = "ИД")]
        public int Id { get; set; }

        // Количество товара этой позиции в продаже
        [Display(Name = "Количество")]
        [Required(ErrorMessage = "Введите количество")]
        [Range(1, 10000, ErrorMessage = "Количество не может быть меньше 1")]
        public int Quantity { get; set; }

        // Цена товара на момент продажи
        [Display(Name = "Цена")]
        [Column(TypeName = "decimal(11,2)")]
        public decimal PriceAtSale { get; set; }

        // --- СВЯЗИ ---

        // Внешний ключ на продажу
        [Display(Name = "Продажа")]
        public int SaleId { get; set; }
        public virtual Sale Sale { get; set; }

        // Внешний ключ на товар
        [Display(Name = "Товар")]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }
    }
}