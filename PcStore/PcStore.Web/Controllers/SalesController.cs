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

        // Главная страница
        [HttpGet]
        public async Task<IActionResult> Index(int? activeSaleId, string searchString, int? categoryId, int? supplierId, string sortOrder)
        {
            // ID текущего пользователя
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // Поиск всех активных черновиков этого продавца
            var drafts = await _context.Sales
                .Where(s => s.UserId == userId && s.Status == SaleStatus.Draft)
                .OrderByDescending(s => s.DateTime)
                .ToListAsync();

            // Какой чек показывать
            Sale currentSale = null;

            if (activeSaleId.HasValue)
            {
                currentSale = drafts.FirstOrDefault(s => s.Id == activeSaleId);
            }

            // Если не найден - взять последний открытый
            if (currentSale == null)
            {
                currentSale = drafts.FirstOrDefault();
            }

            // Детали для текущего чека
            if (currentSale != null)
            {
                await _context.Entry(currentSale)
                    .Collection(s => s.SaleItems)
                    .Query()
                    .Include(si => si.Product)
                    .LoadAsync();

                CalculateTotals(currentSale);
            }

            // Товары для витрины с фильтрацией
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

            ViewBag.ActiveSale = currentSale;
            ViewBag.Drafts = drafts;

            ViewBag.HasActiveSale = currentSale != null;

            return View(await productsQuery.ToListAsync());
        }

        // Живой поиск AJAX с фильтрами
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

        // Добавить товар (сразу в БД)
        [HttpPost]
        public async Task<IActionResult> AddItem(int productId, int quantity, int? saleId)
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
                // Если чека нет или он уже оплачен - создать новый
                if (sale == null || sale.Status != SaleStatus.Draft)
                    return await CreateNewSaleAndAdd(productId, quantity);
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            // Поиск товара в БД
            var existingItem = await _context.SaleItems
                .FirstOrDefaultAsync(si => si.SaleId == sale.Id && si.ProductId == productId);

            // Логика проверки количества
            int currentQtyInCart = existingItem?.Quantity ?? 0;
            int newTotalQty = currentQtyInCart + quantity;

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
                    PriceAtSale = product.Price
                };
                _context.SaleItems.Add(newItem);
            }

            await _context.SaveChangesAsync();

            ViewBag.LastChangedId = productId;
            return await ReloadCartPartial(sale.Id);
        }

        private async Task<IActionResult> CreateNewSaleAndAdd(int productId, int quantity)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var sale = new Sale { UserId = userId, Status = SaleStatus.Draft, DateTime = DateTime.Now };
            _context.Sales.Add(sale);
            await _context.SaveChangesAsync();

            return await AddItem(productId, quantity, sale.Id);
        }

        // Удаление товара (из БД)
        [HttpPost]
        public async Task<IActionResult> RemoveItem(int productId, int saleId)
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

        // Применение скидки
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

        // Отмена продавцом
        [HttpPost]
        public async Task<IActionResult> CancelSale(int saleId)
        {
            var sale = await _context.Sales.FindAsync(saleId);

            if (sale != null && sale.Status == SaleStatus.Draft)
            {
                // Смена статуса на "Отменен продавцом" (цифра 3)
                sale.Status = SaleStatus.CancelledBySeller;

                sale.DateTime = DateTime.Now;

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        // Финализация (состояние "Ждёт оплаты", но процесс ожидания оплаты упрощён до моментальной продажи)
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

                // Списание остатков
                foreach (var item in sale.SaleItems)
                {
                    // Загрузка "свежего" состояния товара из БД
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

                    product.QuantityInStock -= item.Quantity;
                }

                // Фиксация суммы
                decimal subTotal = sale.SaleItems.Sum(x => x.PriceAtSale * x.Quantity);
                sale.TotalAmount = subTotal - (subTotal * sale.DiscountPercent);

                sale.Status = SaleStatus.Paid;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Произошла ошибка при оформлении. Попробуйте снова.";
                return RedirectToAction("Index");
            }

            return RedirectToAction("Index");
        }

        // История
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

        // Поиск по истории
        [Authorize(Roles = "Менеджер")] // К методу имеет доступ только менеджер
        [HttpGet]
        public async Task<IActionResult> SearchHistory(string searchString, int? statusId)
        {
            var query = _context.Sales
                .Include(s => s.User)
                .Include(s => s.SaleItems)
                .AsQueryable();

            // Поиск (по номеру чека или имени продавца)
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(s =>
                    s.Id.ToString().Contains(searchString) ||
                    s.User.FullName.Contains(searchString)
                );
            }

            // Фильтр по статусу
            if (statusId.HasValue)
            {
                if (statusId == -1)
                {
                    // Если выбрано "Отмененные" - искать отменённые и продавцом, и системой
                    query = query.Where(s => s.Status == SaleStatus.CancelledBySeller ||
                                             s.Status == SaleStatus.CancelledBySystem);
                }
                else
                {
                    query = query.Where(s => (int)s.Status == statusId);
                }
            }

            // Сортировка (новые сверху)
            query = query.OrderByDescending(s => s.DateTime);

            var sales = await query.ToListAsync();

            return PartialView("_SalesTablePartial", sales);
        }

        // Детализация чека
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

        // --- ВСПОМОГАТЕЛЬНЫЕ ---
        
        // Подсчёт суммы всех позиций (с ценой на момент добавления) и всей продажи с учётом скидки
        private void CalculateTotals(Sale sale)
        {
            decimal subTotal = sale.SaleItems.Sum(x => x.PriceAtSale * x.Quantity);
            decimal discountAmt = subTotal * sale.DiscountPercent;

            ViewBag.SubTotal = subTotal;
            ViewBag.DiscountAmount = discountAmt;
            ViewBag.DiscountPercent = (int)(sale.DiscountPercent * 100);
            ViewBag.GrandTotal = subTotal - discountAmt;
        }

        // Обновление представления корзины
        private async Task<IActionResult> ReloadCartPartial(int saleId)
        {
            var sale = await _context.Sales
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .FirstOrDefaultAsync(s => s.Id == saleId);

            if (sale != null) CalculateTotals(sale);

            return PartialView("_CartPartial", sale);
        }
    }
}