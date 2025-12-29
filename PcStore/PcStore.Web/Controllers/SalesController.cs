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
        // Добавлен параметр поиска searchString
        [HttpGet]
        public async Task<IActionResult> Index(string searchString)
        {
            // Список товаров для выбора
            var query = _context.Products.Include(p => p.Category).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(p => p.Name.Contains(searchString) || p.Sku.Contains(searchString));
            }

            var products = await query.ToListAsync();

            // "Корзина" сессии (при отсутствии создать)
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();

            // Получить процент скидки из сессии (если есть)
            string discountStr = HttpContext.Session.GetString("DiscountPercent");
            decimal discountPercent = string.IsNullOrEmpty(discountStr) ? 0 : decimal.Parse(discountStr);

            // Расчёт сумм
            decimal subTotal = cart.Sum(x => x.Total); // Сумма без скидки
            decimal discountAmount = subTotal * discountPercent; // Размер скидки
            decimal grandTotal = subTotal - discountAmount; // Итого к оплате

            // Передача данных в представление
            ViewBag.Cart = cart;
            ViewBag.SubTotal = subTotal;
            ViewBag.DiscountAmount = discountAmount;
            ViewBag.DiscountPercent = discountPercent * 100;
            ViewBag.GrandTotal = grandTotal;

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
                    Price = product.Price, // Фиксация цены на момент добавления товара
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
        public async Task<IActionResult> FinalizeSale()
        {
            // "Корзина" сессии
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart");
            if (cart == null || !cart.Any()) return RedirectToAction("Index");

            // Расчёт суммы с учётом скидки
            string discountStr = HttpContext.Session.GetString("DiscountPercent");
            decimal discountPercent = string.IsNullOrEmpty(discountStr) ? 0 : decimal.Parse(discountStr);

            decimal subTotal = cart.Sum(x => x.Total);
            decimal finalAmount = subTotal - (subTotal * discountPercent);

            // Создание продажи (Sale)
            var sale = new Sale
            {
                DateTime = DateTime.Now,
                // Получение ID текущего пользователя из куки
                UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)),
                TotalAmount = finalAmount // Передаётся сумма с учётом скидки
            };

            _context.Sales.Add(sale);
            await _context.SaveChangesAsync(); // Сохранение чека

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

            // Сброс скидки
            HttpContext.Session.Remove("DiscountPercent");

            // Переход на список товаров
            return RedirectToAction("Index", "Products");
        }

        // Применение скидки
        [HttpPost]
        public IActionResult ApplyDiscount(string code)
        {
            decimal discountValue = 0;

            // Заглушка логики скидок
            switch (code?.ToUpper())
            {
                case "PROMO10":
                    discountValue = 0.10m; // 10%
                    break;
                case "STUDENT":
                    discountValue = 0.15m; // 15%
                    break;
                case "VIP":
                    discountValue = 0.20m; // 20%
                    break;
                default:
                    discountValue = 0;
                    break;
            }

            // Сохранение размера скидки в сессию (0.10, 0.20, ...)
            // SessionExtensions.SetString не умеет хранить decimal, нужно превратить значение в строку
            HttpContext.Session.SetString("DiscountPercent", discountValue.ToString());

            return RedirectToAction("Index");
        }

        // История продаж
        [HttpGet]
        public async Task<IActionResult> History()
        {
            var sales = await _context.Sales
                .Include(s => s.User)
                .Include(s => s.SaleItems) // Для скидок
                .OrderByDescending(s => s.DateTime)
                .ToListAsync();

            return View(sales);
        }

        // Просмотр деталей конкретной продажи
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sale = await _context.Sales
                .Include(s => s.User)
                .Include(s => s.SaleItems) // Загрузка продаж
                    .ThenInclude(si => si.Product) // Загрузка товаров внутри продаж
                .FirstOrDefaultAsync(m => m.Id == id);

            if (sale == null)
            {
                return NotFound();
            }

            return View(sale);
        }
    }
}