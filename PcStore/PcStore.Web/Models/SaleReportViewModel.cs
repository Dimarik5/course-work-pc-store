using System.ComponentModel.DataAnnotations;

namespace PcStore.Web.Models
{
    public class SaleReportViewModel
    {
        // Входные данные (фильтр)
        [DataType(DataType.Date)]
        [Display(Name = "С даты")]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "По дату")]
        public DateTime EndDate { get; set; }

        // Результаты (выходные данные)
        public decimal TotalRevenue { get; set; } // Общая выручка
        public int SalesCount { get; set; } // Количество чеков
        public List<Sale> Sales { get; set; } = new List<Sale>(); // Список чеков
    }
}