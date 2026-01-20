using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcStore.Web.Controllers;
using PcStore.Web.Data;
using PcStore.Web.Models;

namespace PcStore.Tests
{
    [TestFixture]
    public class ProductsControllerTests
    {
        private AppDbContext _context;

        [SetUp]
        public void Setup()
        {
            // Создание БД в памяти
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);

            // Добавление тестовых данных

            // Товар №1 с количеством 10
            _context.Products.Add(new Product
            {
                Id = 1,
                Name = "Тестовый товар 1",
                Sku = "123",
                Price = 10000,
                QuantityInStock = 10,
                IsArchived = false,
                CategoryId = 1,
                SupplierId = 1
            });

            // Товар №2 с количеством 0
            _context.Products.Add(new Product
            {
                Id = 2,
                Name = "Тестовый товар 2",
                Sku = "456",
                Price = 10000,
                QuantityInStock = 0,
                IsArchived = false,
                CategoryId = 1,
                SupplierId = 1
            });

            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            _context.Dispose();
        }

        // Тест работы софт-делит в архивации (происходит архивация, а не удаление)
        [Test]
        public async Task DeleteConfirmed_Should_Archive_Product_Instead_Of_Deleting()
        {
            // Подготовка - создание контроллера и передача ему временной БД
            var controller = new ProductsController(_context);
            
            // Действие - попытка удаления товара с ID=2
            await controller.DeleteConfirmed(2);

            // Проверка

            var product = await _context.Products.FindAsync(2);

            // Товар не должен быть null (он остался в БД)
            Assert.IsNotNull(product);
            // Поле IsArchived должно стать true (1)
            Assert.IsTrue(product.IsArchived, "Товар должен быть помечен как архивный");
        }

        // Тест отказа архивации при ненулевом количестве
        [Test]
        public async Task DeleteConfirmed_Was_Cancelled_Due_To_Non_Zero_Quantity()
        {
            // Подготовка
            var controller = new ProductsController(_context);

            // Действие
            var result = await controller.DeleteConfirmed(1);

            // Проверка

            var product = await _context.Products.FindAsync(1);

            // Поле IsArchived должно стать true (1)
            Assert.IsFalse(product.IsArchived, "Товар не должен быть архивирован, пока есть остаток");

            // Контроллер перекинул нас обратно на страницу Delete
            Assert.IsInstanceOf<RedirectToActionResult>(result);
            var redirect = result as RedirectToActionResult;
            Assert.AreEqual("Delete", redirect.ActionName, "Должен быть редирект обратно на страницу удаления");
        }

        // Тест списания товара на введённое число
        [Test]
        public async Task WriteOff_Should_Decrease_Quantity()
        {
            // Подготовка
            var controller = new ProductsController(_context);
            int writeOffAmount = 3;
            string reason = "Брак";

            // Действие
            await controller.WriteOff(1, writeOffAmount, reason);

            // Проверка

            var product = await _context.Products.FindAsync(1);

            // 10 - 3 = 7
            Assert.AreEqual(7, product.QuantityInStock, "Количество должно уменьшиться на списанную величину");
        }

        // Тест списания товара в большем количестве, чем есть на складе
        [Test]
        public async Task WriteOff_Should_Not_Decrease_If_Quantity_Is_Too_Big()
        {
            // Подготовка
            var controller = new ProductsController(_context);
            int writeOffAmount = 100; // Попытка ссписать 100, хотя создан был товар с количеством 10
            string reason = "Давайте уйдём в минус";

            // Действие
            var result = await controller.WriteOff(1, writeOffAmount, reason);

            // Проверка

            var product = await _context.Products.FindAsync(1);

            // 10 - 100 = 10
            Assert.AreEqual(10, product.QuantityInStock, "Остаток не должен меняться при ошибочном списании");

            // Проверка, что вернулась страница с ошибкой, а не редирект на страницу списка
            Assert.IsInstanceOf<ViewResult>(result);
        }
    }
}