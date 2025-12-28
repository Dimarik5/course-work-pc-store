using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcStore.Web.Data;
using PcStore.Web.Extensions;
using PcStore.Web.Models;
using System.Security.Claims;

namespace PcStore.Web.Controllers
{
    [Authorize] // К контроллеру имеют доступ только авторизованные пользователи
    public class SalesController : Controller
    {
        private readonly AppDbContext _context;

        public SalesController(AppDbContext context)
        {
            _context = context;
        }

        // Главная страница продажи
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Список товаров для выбора
            var products = await _context.Products.Include(p => p.Category).ToListAsync();

            // "Корзина" сессии (при отсутствии создать)
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();

            // Передача данных в представление
            ViewBag.Cart = cart;
            ViewBag.CartTotal = cart.Sum(x => x.Total);

            return View(products);
        }

        // Добавление товара в "корзину"
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            // "Корзина" сессии (при отсутствии создать)
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();

            // Проверка, есть ли уже такой товар в "корзине"
            var existingItem = cart.FirstOrDefault(x => x.ProductId == productId);

            // Если есть - увеличить количество
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                // Если нет - создать новый
                cart.Add(new CartItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Price = product.Price, // Фиксация цены на момент добавленяи товара
                    Quantity = quantity
                });
            }

            // Сохранение "корзины" обратно в сессию
            HttpContext.Session.Set("Cart", cart);

            return RedirectToAction("Index");
        }

        // Очистка "корзины" (отмена)
        [HttpPost]
        public IActionResult ClearCart()
        {
            HttpContext.Session.Remove("Cart");
            return RedirectToAction("Index");
        }

        // Оформить продажу (сохранение в БД)
        [HttpPost]
        public async Task<IActionResult> Checkout()
        {
            // "Корзина" сессии
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart");
            if (cart == null || !cart.Any()) return RedirectToAction("Index");

            // Создание продажи (Sale)
            var sale = new Sale
            {
                DateTime = DateTime.Now,
                // Получение ID текущего пользователя из куки
                UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)),
                TotalAmount = cart.Sum(x => x.Total)
            };

            _context.Sales.Add(sale);
            await _context.SaveChangesAsync(); // Сначала сохраняем чек, чтобы получить Sale.Id

            // Создание записей о позициях в продаже (SaleItems)
            foreach (var item in cart)
            {
                var saleItem = new SaleItem
                {
                    SaleId = sale.Id, // ID продажи
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    PriceAtSale = item.Price // Записывается та цена, которая была в "корзине"
                };

                // Уменьшение остатка товара на складе
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null) product.QuantityInStock -= item.Quantity;

                _context.SaleItems.Add(saleItem);
            }

            await _context.SaveChangesAsync(); // Сохранение позиций

            // Очистка "корзины"
            HttpContext.Session.Remove("Cart");

            // Переход на список товаров
            return RedirectToAction("Index", "Products");
        }
    }
}