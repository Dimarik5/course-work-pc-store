using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PcStore.Web.Models
{
    public class Sale
    {
        // --- ПОЛЯ ---

        // Идентификатотр продажи
        [Display(Name = "ИД")]
        public int Id { get; set; }

        // Дата и время продажи
        [Display(Name = "Дата и время")]
        public DateTime DateTime { get; set; } = DateTime.Now;

        // Сумма продажи
        [Display(Name = "Сумма")]
        [Column(TypeName = "decimal(11,2)")]
        public decimal TotalAmount { get; set; }

        // Продавец
        [Display(Name = "Продавец")]
        public int UserId { get; set; }

        // --- СВЯЗИ ---

        // Внешний ключ на пользователя
        [Display(Name = "Пользователь")]
        public virtual User User { get; set; }

        // В одной продаже может быть много позиций
        public virtual List<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }
}