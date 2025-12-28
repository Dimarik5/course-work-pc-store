namespace PcStore.Web.Models
{
    public class CartItem
    {
        // --- ПОЛЯ ---

        // Идентификатор товара в "корзине"
        public int ProductId { get; set; }

        // Название товара в "корзине"
        public string ProductName { get; set; }

        // Цена товара на момент добавления в "корзину"
        public decimal Price { get; set; }

        // Количество добавленного товара
        public int Quantity { get; set; }

        // Сумма = цена * количество (свойство только для чтения)
        public decimal Total => Price * Quantity;
    }
}