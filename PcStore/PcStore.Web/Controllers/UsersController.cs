using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PcStore.Web.Data;
using PcStore.Web.Models;

namespace PcStore.Web.Controllers
{
    [Authorize(Roles = "Менеджер")] // К контроллеру имеет доступ только менеджер
    public class UsersController : Controller
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
            // Загружаем список ролей для фильтра
            ViewData["RoleId"] = new SelectList(_context.Roles, "Id", "Name");

            // Возвращаем пустой список (AJAX подгрузит данные мгновенно)
            return View(new List<User>());
        }

        // Поиск
        [HttpGet]
        public async Task<IActionResult> Search(string searchString, int? roleId, string status, string sortOrder)
        {
            var query = _context.Users.Include(u => u.Role).AsQueryable();

            // Поиск
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(u => u.FullName.Contains(searchString) || u.Login.Contains(searchString));
            }

            // Фильтр по роли
            if (roleId.HasValue && roleId > 0)
            {
                query = query.Where(u => u.RoleId == roleId);
            }

            // Фильтр по статусу
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "active")
                {
                    query = query.Where(u => !u.IsBlocked);
                }
                else if (status == "blocked")
                {
                    query = query.Where(u => u.IsBlocked);
                }
            }

            // Сортировка
            switch (sortOrder)
            {
                case "name_desc":
                    query = query.OrderByDescending(u => u.FullName);
                    break;
                case "role_asc":
                    query = query.OrderBy(u => u.Role.Name);
                    break;
                default:
                    query = query.OrderBy(u => u.FullName);
                    break;
            }

            var users = await query.ToListAsync();

            return PartialView("_UsersTablePartial", users);
        }

        // GET: Users/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            ViewData["RoleId"] = new SelectList(_context.Roles, "Id", "Name");
            return View();
        }

        // POST: Users/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,FullName,Login,Password,RoleId")] User user)
        {
            if (ModelState.IsValid)
            {
                _context.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["RoleId"] = new SelectList(_context.Roles, "Id", "Name", user.RoleId);
            return View(user);
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            ViewData["RoleId"] = new SelectList(_context.Roles, "Id", "Name", user.RoleId);
            return View(user);
        }

        // POST: Users/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FullName,Login,Password,RoleId")] User user)
        {
            if (id != user.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(user);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.Id))
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
            ViewData["RoleId"] = new SelectList(_context.Roles, "Id", "Name", user.RoleId);
            return View(user);
        }

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.IsBlocked = true; // Блокировка пользователя

                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}
