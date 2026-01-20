using System.ComponentModel.DataAnnotations;

namespace PcStore.Web.Models
{
    public enum SaleStatus
    {
        // --- ПОЛЯ ---

        // Состояние для черновика
        [Display(Name = "Черновик")]
        Draft = 0, // Товары добавляются

        // Состояние для ожидания оплаты
        [Display(Name = "Ожидает оплаты")]
        WaitingForPayment = 1, // Кассир нажал "Оформить продажу", клиент ищет деньги

        // Состояние для оплаченной продажи
        [Display(Name = "Оплачен")]
        Paid = 2, // Успешный финал

        // Состояние для продажи, удалённой вручную
        [Display(Name = "Отменен продавцом")]
        CancelledBySeller = 3, // Продавец удалил чек

        // Состояние для продажи, удалённой системой
        [Display(Name = "Отменен системой")]
        CancelledBySystem = 4 // Тайм-аут оплаты
    }
}