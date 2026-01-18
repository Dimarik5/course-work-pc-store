using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PcStore.Web.Data;
using PcStore.Web.Models;
using System.Security.Claims;

namespace PcStore.Web.Controllers
{
    [Authorize]
    public class SalesController : Controller
    {
        private readonly AppDbContext _context;

        public SalesController(AppDbContext context)
        {
            _context = context;
        }

        // --- ГЛАВНАЯ СТРАНИЦА (ТЕРМИНАЛ) ---
        [HttpGet]
        public async Task<IActionResult> Index(int? activeSaleId, string searchString, int? categoryId, int? supplierId, string sortOrder)
        {
            // 1. Получаем ID текущего пользователя
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // 2. Ищем все активные черновики этого продавца (для переключателя в будущем)
            var drafts = await _context.Sales
                .Where(s => s.UserId == userId && s.Status == SaleStatus.Draft)
                .OrderByDescending(s => s.DateTime)
                .ToListAsync();

            // 3. Определяем, какой чек показывать
            Sale currentSale = null;

            if (activeSaleId.HasValue)
            {
                currentSale = drafts.FirstOrDefault(s => s.Id == activeSaleId);
            }

            // Если не нашли или не передали - берем последний открытый
            if (currentSale == null)
            {
                currentSale = drafts.FirstOrDefault();
            }

            // 4. Загружаем детали для текущего чека (товары)
            if (currentSale != null)
            {
                // Явная загрузка товаров внутри чека
                await _context.Entry(currentSale)
                    .Collection(s => s.SaleItems)
                    .Query()
                    .Include(si => si.Product)
                    .LoadAsync();

                // Расчет итогов для View
                CalculateTotals(currentSale);
            }

            // 5. Загружаем товары для витрины (ТВОЯ ЛОГИКА ФИЛЬТРАЦИИ)
            var productsQuery = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => !p.IsArchived)
                .AsQueryable();

            // Поиск
            if (!string.IsNullOrEmpty(searchString))
            {
                productsQuery = productsQuery.Where(p => p.Name.Contains(searchString) || p.Sku.Contains(searchString));
            }

            // Фильтры
            if (categoryId.HasValue && categoryId > 0)
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId);

            if (supplierId.HasValue && supplierId > 0)
                productsQuery = productsQuery.Where(p => p.SupplierId == supplierId);

            // Сортировка
            switch (sortOrder)
            {
                case "price_desc": productsQuery = productsQuery.OrderByDescending(p => p.Price); break;
                case "price_asc": productsQuery = productsQuery.OrderBy(p => p.Price); break;
                case "qty_asc": productsQuery = productsQuery.OrderBy(p => p.QuantityInStock); break;
                case "qty_desc": productsQuery = productsQuery.OrderByDescending(p => p.QuantityInStock); break;
                default: productsQuery = productsQuery.OrderByDescending(p => p.Id); break;
            }

            // Данные для выпадающих списков
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", categoryId);
            ViewData["SupplierId"] = new SelectList(_context.Suppliers, "Id", "Name", supplierId);

            // 6. Упаковываем всё во ViewBag
            ViewBag.ActiveSale = currentSale; // Это пойдет в PartialView чека
            ViewBag.Drafts = drafts;

            ViewBag.HasActiveSale = currentSale != null;

            return View(await productsQuery.ToListAsync());
        }

        // --- AJAX: ЖИВОЙ ПОИСК (Тот же метод, только с фильтрами) ---
        [HttpGet]
        public async Task<IActionResult> SearchProducts(string searchString, int? categoryId, int? supplierId, string sortOrder)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => !p.IsArchived)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(p => p.Name.Contains(searchString) || p.Sku.Contains(searchString));
            }

            if (categoryId.HasValue && categoryId > 0) query = query.Where(p => p.CategoryId == categoryId);
            if (supplierId.HasValue && supplierId > 0) query = query.Where(p => p.SupplierId == supplierId);

            switch (sortOrder)
            {
                case "price_desc": query = query.OrderByDescending(p => p.Price); break;
                case "price_asc": query = query.OrderBy(p => p.Price); break;
                case "qty_asc": query = query.OrderBy(p => p.QuantityInStock); break;
                case "qty_desc": query = query.OrderByDescending(p => p.QuantityInStock); break;
                default: query = query.OrderByDescending(p => p.Id); break;
            }

            // Есть ли активный черновик у пользователя
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            bool hasDrafts = await _context.Sales.AnyAsync(s => s.UserId == userId && s.Status == SaleStatus.Draft);

            ViewBag.HasActiveSale = hasDrafts;

            return PartialView("_ProductListPartial", await query.ToListAsync());
        }

        // --- ДЕЙСТВИЯ С ЧЕКАМИ ---

        // Создать новый пустой чек
        [HttpPost]
        public async Task<IActionResult> CreateNewSale()
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var sale = new Sale
            {
                UserId = userId,
                DateTime = DateTime.Now,
                Status = SaleStatus.Draft,
                DiscountPercent = 0,
                TotalAmount = 0
            };

            _context.Sales.Add(sale);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", new { activeSaleId = sale.Id });
        }

        // Добавить товар (Сразу в БД!)
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity, int? saleId)
        {
            Sale sale;

            // Если чека нет - создаем новый и добавляем в него
            if (saleId == null)
            {
                return await CreateNewSaleAndAdd(productId, quantity);
            }
            else
            {
                sale = await _context.Sales.FindAsync(saleId);
                // Если чека нет или он уже оплачен - создаем новый
                if (sale == null || sale.Status != SaleStatus.Draft)
                    return await CreateNewSaleAndAdd(productId, quantity);
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            // Ищем товар в БД
            var existingItem = await _context.SaleItems
                .FirstOrDefaultAsync(si => si.SaleId == sale.Id && si.ProductId == productId);

            // Логика проверка количества
            int currentQtyInCart = existingItem?.Quantity ?? 0; // Сколько уже лежит
            int newTotalQty = currentQtyInCart + quantity;      // Сколько хочет положить

            if (newTotalQty > product.QuantityInStock)
            {
                return await ReloadCartPartial(sale.Id);
            }

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                var newItem = new SaleItem
                {
                    SaleId = sale.Id,
                    ProductId = product.Id,
                    Quantity = quantity,
                    PriceAtSale = product.Price // Фиксируем цену
                };
                _context.SaleItems.Add(newItem);
            }

            await _context.SaveChangesAsync();

            ViewBag.LastChangedId = productId; // Для анимации
            return await ReloadCartPartial(sale.Id);
        }

        private async Task<IActionResult> CreateNewSaleAndAdd(int productId, int quantity)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var sale = new Sale { UserId = userId, Status = SaleStatus.Draft, DateTime = DateTime.Now };
            _context.Sales.Add(sale);
            await _context.SaveChangesAsync();

            return await AddToCart(productId, quantity, sale.Id);
        }

        // Удаление товара (из БД)
        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int productId, int saleId)
        {
            var item = await _context.SaleItems
                .FirstOrDefaultAsync(si => si.SaleId == saleId && si.ProductId == productId);

            if (item != null)
            {
                _context.SaleItems.Remove(item);
                await _context.SaveChangesAsync();
            }

            return await ReloadCartPartial(saleId);
        }

        [HttpPost]
        public async Task<IActionResult> ApplyDiscount(string code, int saleId)
        {
            var sale = await _context.Sales.FindAsync(saleId);
            if (sale == null) return NotFound();

            decimal discountValue = 0;
            switch (code?.ToUpper())
            {
                case "PROMO10": discountValue = 0.10m; break;
                case "STUDENT": discountValue = 0.15m; break;
                case "VIP": discountValue = 0.20m; break;
                default: discountValue = 0; break;
            }

            sale.DiscountPercent = discountValue;
            await _context.SaveChangesAsync();

            return await ReloadCartPartial(saleId);
        }

        // Отмена (Удаление черновика из БД)
        [HttpPost]
        public async Task<IActionResult> CancelSale(int saleId)
        {
            var sale = await _context.Sales.FindAsync(saleId);

            // Отменяем только если это Черновик
            if (sale != null && sale.Status == SaleStatus.Draft)
            {
                // 1. Меняем статус на "Отменен продавцом" (это цифра 3)
                sale.Status = SaleStatus.CancelledBySeller;

                // 2. Фиксируем время отмены (опционально, но полезно)
                sale.DateTime = DateTime.Now;

                // Товары (SaleItems) удалять НЕ НАДО. 
                // Пусть останутся в истории, чтобы мы знали, ЧТО именно хотели купить, но передумали.

                await _context.SaveChangesAsync();
            }

            // Редирект обновит вкладки, и этот чек пропадет из списка "Активных", 
            // потому что там фильтр Where(Status == Draft)
            return RedirectToAction("Index");
        }

        // Финализация (Оплата)
        [HttpPost]
        public async Task<IActionResult> FinalizeSale(int saleId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var sale = await _context.Sales
                .Include(s => s.SaleItems)
                .FirstOrDefaultAsync(s => s.Id == saleId);

                if (sale == null || !sale.SaleItems.Any()) return RedirectToAction("Index");

                // Списываем остатки
                foreach (var item in sale.SaleItems)
                {
                    // Загружаем "свежее" состояние товара прямо из базы
                    var product = await _context.Products.FindAsync(item.ProductId);

                    if (product == null)
                    {
                        // Товара вообще нет
                        TempData["ErrorMessage"] = $"Товар с ID {item.ProductId} не найден.";
                        return RedirectToAction("Index");
                    }

                    if (product.QuantityInStock < item.Quantity)
                    {
                        // Другой продавец уже продал этот товар в другом окне
                        TempData["ErrorMessage"] = $"Товар \"{product.Name}\" закончился! (Остаток: {product.QuantityInStock}, в чеке: {item.Quantity})";
                        return RedirectToAction("Index");
                    }

                    // Если всё ок - списываем
                    product.QuantityInStock -= item.Quantity;
                }

                // Фиксируем сумму
                decimal subTotal = sale.SaleItems.Sum(x => x.PriceAtSale * x.Quantity);
                sale.TotalAmount = subTotal - (subTotal * sale.DiscountPercent);

                sale.Status = SaleStatus.Paid;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync(); // Подтверждаем транзакцию
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(); // Если что-то упало - отменяем всё
                TempData["ErrorMessage"] = "Произошла ошибка при оформлении. Попробуйте снова.";
                return RedirectToAction("Index");
            }

            return RedirectToAction("Index");
        }

        // --- ВСПОМОГАТЕЛЬНЫЕ ---

        private void CalculateTotals(Sale sale)
        {
            decimal subTotal = sale.SaleItems.Sum(x => x.PriceAtSale * x.Quantity);
            decimal discountAmt = subTotal * sale.DiscountPercent;

            ViewBag.SubTotal = subTotal;
            ViewBag.DiscountAmount = discountAmt;
            ViewBag.DiscountPercent = (int)(sale.DiscountPercent * 100);
            ViewBag.GrandTotal = subTotal - discountAmt;
        }

        private async Task<IActionResult> ReloadCartPartial(int saleId)
        {
            var sale = await _context.Sales
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .FirstOrDefaultAsync(s => s.Id == saleId);

            if (sale != null) CalculateTotals(sale);

            // ВАЖНО: Модель теперь - Sale, а не List<CartItem>!
            return PartialView("_CartPartial", sale);
        }

        // ... History и Details остаются без изменений ...
        [Authorize(Roles = "Менеджер")]
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

        [Authorize(Roles = "Менеджер")]
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