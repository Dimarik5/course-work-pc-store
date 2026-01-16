using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        // Расчет "корзины"
        private void CalculateCartTotals()
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            string discountStr = HttpContext.Session.GetString("DiscountPercent");
            decimal discountPercent = string.IsNullOrEmpty(discountStr) ? 0 : decimal.Parse(discountStr);

            decimal subTotal = cart.Sum(x => x.Total);
            decimal discountAmount = subTotal * discountPercent;
            decimal grandTotal = subTotal - discountAmount;

            ViewBag.Cart = cart;
            ViewBag.SubTotal = subTotal;
            ViewBag.DiscountAmount = discountAmount;
            ViewBag.DiscountPercent = (int)(discountPercent * 100);
            ViewBag.GrandTotal = grandTotal;
        }

        // Главная страница
        [HttpGet]
        public async Task<IActionResult> Index(string searchString)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => !p.IsArchived) // Скрыть архивированные товары
                .OrderByDescending(p => p.Id) // Сразу показать в порядке добавления
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(p => p.Name.Contains(searchString) || p.Sku.Contains(searchString));
            }

            var products = await query.ToListAsync();

            // Загрузка данных для выпадающих списков
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            ViewData["SupplierId"] = new SelectList(_context.Suppliers, "Id", "Name");

            // Подсчёт итогов для первоначальной загрузки
            CalculateCartTotals();

            return View(products);
        }

        // Поиск товара
        [HttpGet]
        public async Task<IActionResult> SearchProducts(string searchString, int? categoryId, int? supplierId, string sortOrder)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => !p.IsArchived) // Скрыть архивированные товары
                .AsQueryable();

            // Поиск
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(p => p.Name.Contains(searchString) || p.Sku.Contains(searchString));
            }

            // Фильтры
            if (categoryId.HasValue && categoryId > 0)
                query = query.Where(p => p.CategoryId == categoryId);

            if (supplierId.HasValue && supplierId > 0)
                query = query.Where(p => p.SupplierId == supplierId);

            // Сортировка
            switch (sortOrder)
            {
                case "price_desc":
                    query = query.OrderByDescending(p => p.Price);
                    break;
                case "price_asc":
                    query = query.OrderBy(p => p.Price);
                    break;
                case "qty_asc":
                    query = query.OrderBy(p => p.QuantityInStock);
                    break;
                case "qty_desc":
                    query = query.OrderByDescending(p => p.QuantityInStock);
                    break;
                default: // По умолчанию - сначала новые (по ID)
                    query = query.OrderByDescending(p => p.Id);
                    break;
            }

            var products = await query.ToListAsync();

            return PartialView("_ProductListPartial", products);
        }

        // Добавление в "корзину"
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            var existingItem = cart.FirstOrDefault(x => x.ProductId == productId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                cart.Add(new CartItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Price = product.Price,
                    Quantity = quantity
                });
            }

            HttpContext.Session.Set("Cart", cart);

            CalculateCartTotals();
            ViewBag.LastChangedId = productId;
            return PartialView("_CartPartial");
        }

        // Удаление из "орзины"
        [HttpPost]
        public IActionResult RemoveFromCart(int productId)
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart");
            if (cart != null)
            {
                var item = cart.FirstOrDefault(x => x.ProductId == productId);
                if (item != null)
                {
                    cart.Remove(item);
                    HttpContext.Session.Set("Cart", cart);
                }
            }

            CalculateCartTotals();
            return PartialView("_CartPartial");
        }

        // Применение скидки
        [HttpPost]
        public IActionResult ApplyDiscount(string code)
        {
            decimal discountValue = 0;
            switch (code?.ToUpper())
            {
                case "PROMO10": discountValue = 0.10m; break;
                case "STUDENT": discountValue = 0.15m; break;
                case "VIP": discountValue = 0.20m; break;
                default: discountValue = 0; break;
            }

            HttpContext.Session.SetString("DiscountPercent", discountValue.ToString());

            CalculateCartTotals();
            return PartialView("_CartPartial");
        }

        // Очистка "корзины"
        [HttpPost]
        public IActionResult ClearCart()
        {
            HttpContext.Session.Remove("Cart");
            HttpContext.Session.Remove("DiscountPercent");

            CalculateCartTotals();
            return PartialView("_CartPartial");
        }

        // Оформление продажи
        [HttpPost]
        public async Task<IActionResult> FinalizeSale()
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart");
            if (cart == null || !cart.Any()) return RedirectToAction("Index");

            CalculateCartTotals(); // Чтобы получить GrandTotal
            decimal finalAmount = (decimal)ViewBag.GrandTotal;

            var sale = new Sale
            {
                DateTime = DateTime.Now,
                UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)),
                TotalAmount = finalAmount
            };

            _context.Sales.Add(sale);
            await _context.SaveChangesAsync();

            foreach (var item in cart)
            {
                var saleItem = new SaleItem
                {
                    SaleId = sale.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    PriceAtSale = item.Price
                };

                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null) product.QuantityInStock -= item.Quantity;

                _context.SaleItems.Add(saleItem);
            }

            await _context.SaveChangesAsync();

            HttpContext.Session.Remove("Cart");
            HttpContext.Session.Remove("DiscountPercent");

            return RedirectToAction("Index", "Products");
        }

        // История продаж
        [Authorize(Roles = "Менеджер")] // К методу имеет доступ только менеджер
        [HttpGet]
        public async Task<IActionResult> History()
        {
            var sales = await _context.Sales
                .Include(s => s.User)
                .Include(s => s.SaleItems)
                .OrderByDescending(s => s.DateTime)
                .ToListAsync();
            return View(sales);
        }

        // Детали чека
        [Authorize(Roles = "Менеджер")] // К методу имеет доступ только менеджер
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var sale = await _context.Sales
                .Include(s => s.User)
                .Include(s => s.SaleItems).ThenInclude(si => si.Product)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (sale == null) return NotFound();
            return View(sale);
        }
    }
}