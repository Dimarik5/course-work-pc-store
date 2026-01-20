using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PcStore.Web.Data;
using PcStore.Web.Models;

namespace PcStore.Web.Controllers
{
    [Authorize] // К контроллеру имеют доступ только авторизованные пользователи
    public class ProductsController : Controller
    {
        private readonly AppDbContext _context;

        public ProductsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Products
        // Добавлен параметр поиска searchString
        public async Task<IActionResult> Index(string searchString, int? categoryId, int? supplierId)
        {
            // Получение товаров
            var products = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => !p.IsArchived) // Скрыть архивированные товары
                .AsQueryable();

            // Если строка поиска не пустая - отфильтровать строку
            if (!string.IsNullOrEmpty(searchString))
            {
                // Поиск по названию или по артикулу
                products = products.Where(p => p.Name.Contains(searchString) || p.Sku.Contains(searchString));
            }

            // Данные для фильтров
            ViewData["CategoryId"] = new SelectList(_context.Categories.Where(c => !c.IsArchived), "Id", "Name", categoryId);
            ViewData["SupplierId"] = new SelectList(_context.Suppliers.Where(c => !c.IsArchived), "Id", "Name", supplierId);

            // Изначальный запрос
            return View(new List<Product>());
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // GET: Products/Create
        [Authorize(Roles = "Продавец")] // К методу имеет доступ только продавец
        public IActionResult Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories.Where(c => !c.IsArchived), "Id", "Name");
            ViewData["SupplierId"] = new SelectList(_context.Suppliers.Where(c => !c.IsArchived), "Id", "Name");
            return View();
        }

        // POST: Products/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [Authorize(Roles = "Продавец")] // К методу имеет доступ только продавец
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Sku,Description,Price,QuantityInStock,CategoryId,SupplierId")] Product product)
        {
            if (ModelState.IsValid)
            {
                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories.Where(c => !c.IsArchived), "Id", "Name", product.CategoryId);
            ViewData["SupplierId"] = new SelectList(_context.Suppliers.Where(c => !c.IsArchived), "Id", "Name", product.SupplierId);
            return View(product);
        }

        // GET: Products/Edit/5
        [Authorize(Roles = "Продавец")] // К методу имеет доступ только продавец
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories.Where(c => !c.IsArchived), "Id", "Name", product.CategoryId);
            ViewData["SupplierId"] = new SelectList(_context.Suppliers.Where(c => !c.IsArchived), "Id", "Name", product.SupplierId);
            return View(product);
        }

        // POST: Products/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [Authorize(Roles = "Продавец")] // К методу имеет доступ только продавец
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Sku,Description,Price,QuantityInStock,CategoryId,SupplierId")] Product product)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories.Where(c => !c.IsArchived), "Id", "Name", product.CategoryId);
            ViewData["SupplierId"] = new SelectList(_context.Suppliers.Where(c => !c.IsArchived), "Id", "Name", product.SupplierId);
            return View(product);
        }

        // GET: Products/Delete/5
        [Authorize(Roles = "Продавец")] // К методу имеет доступ только продавец
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // Архивация
        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Продавец")] // К методу имеет доступ только продавец
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            // Проверка на количество
            if (product.QuantityInStock > 0)
            {
                return RedirectToAction(nameof(Delete), new { id = id });
            }

            product.IsArchived = true;
            _context.Products.Update(product);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Продавец")] // К методу имеет доступ только продавец
        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }

        // Показать страницу списания
        [HttpGet]
        [Authorize(Roles = "Менеджер")] // К методу имеет доступ только менеджер
        public async Task<IActionResult> WriteOff(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            return View(product);
        }

        // Выполнить списание
        [HttpPost]
        [Authorize(Roles = "Менеджер")] // К методу имеет доступ только менеджер
        public async Task<IActionResult> WriteOff(int id, int quantity, string reason)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            bool hasError = false;

            // Проверка количества 1
            if (quantity <= 0)
            {
                ModelState.AddModelError("", "Количество должно быть больше 0");
                ViewBag.Reason = reason;
                hasError = true;
            }

            // Проверка количества 2
            if (product.QuantityInStock < quantity)
            {
                ModelState.AddModelError("", $"Нельзя списать больше, чем есть на складе ({product.QuantityInStock})");
                ViewBag.Reason = reason;
                hasError = true;
            }

            // Проверка обязательного ввода причины списания
            if (string.IsNullOrWhiteSpace(reason))
            {
                ModelState.AddModelError("", "Укажите причину списания");
                if (hasError)
                {
                    ViewBag.Quantity = null;
                }
                else
                {
                    ViewBag.Quantity = quantity;
                }
                hasError = true;
            }

            // Итог ошибок
            if (hasError)
            {
                return View(product);
            }

            product.QuantityInStock -= quantity;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Показать страницу поставки
        [HttpGet]
        [Authorize(Roles = "Менеджер")] // К методу имеет доступ только менеджер
        public async Task<IActionResult> Supply(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            return View(product);
        }

        // Выполнить поставку
        [HttpPost]
        [Authorize(Roles = "Менеджер")] // К методу имеет доступ только менеджер
        public async Task<IActionResult> Supply(int id, int quantity, string source)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            bool hasError = false;

            // Проверка количества 1
            if (quantity <= 0)
            {
                ModelState.AddModelError("", "Количество должно быть больше 0");
                ViewBag.Source = source;
                hasError = true;
            }

            // Проверка количества 2
            if (quantity > 10000 - product.QuantityInStock)
            {
                ModelState.AddModelError("", $"Превышено доступное место под этот товар ({10000 - product.QuantityInStock})");
                ViewBag.Source = source;
                hasError = true;
            }

            // Проверка обязательного ввода накладной
            if (string.IsNullOrWhiteSpace(source))
            {
                ModelState.AddModelError("", "Укажите номер накладной");
                if (hasError)
                {
                    ViewBag.Quantity = null;
                }
                else
                {
                    ViewBag.Quantity = quantity;
                }
                hasError = true;
            }

            // Итог ошибок
            if (hasError)
            {
                return View(product);
            }

            product.QuantityInStock += quantity;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Поиск
        [HttpGet]
        public async Task<IActionResult> Search(string searchString, int? categoryId, int? supplierId, string sortOrder)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => !p.IsArchived) // Скрыть архивированные товары
                .AsQueryable();

            // ПОИСК
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(p => p.Name.Contains(searchString) || p.Sku.Contains(searchString));
            }

            // ФИЛЬТРАЦИЯ
            if (categoryId.HasValue && categoryId > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId);
            }

            if (supplierId.HasValue && supplierId > 0)
            {
                query = query.Where(p => p.SupplierId == supplierId);
            }

            // СОРТИРОВКА
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

            return PartialView("_ProductsTablePartial", products);
        }
    }
}
