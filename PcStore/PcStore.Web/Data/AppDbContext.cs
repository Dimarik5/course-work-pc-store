using Microsoft.EntityFrameworkCore;
using PcStore.Web.Models;

namespace PcStore.Web.Data
{
    public class AppDbContext : DbContext
    {
        // Конструктор, принимающий настройки и передающий их в базовый класс
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // --- ТАБЛИЦЫ БАЗЫ ДАННЫХ ---

        // Категории
        public DbSet<Category> Categories { get; set; }

        // Товары
        public DbSet<Product> Products { get; set; }

        // Роли
        public DbSet<Role> Roles { get; set; }

        // Продажи
        public DbSet<Sale> Sales { get; set; }

        // Позиции продаж
        public DbSet<SaleItem> SaleItems { get; set; }

        // Поставщики
        public DbSet<Supplier> Suppliers { get; set; }

        // Пользователи
        public DbSet<User> Users { get; set; }

        // --- ПАРАМЕТРЫ СОЗДАНИЯ БД ---

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Начальные данные в таблице Категрии
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Процессоры" },
                new Category { Id = 2, Name = "Видеокарты" },
                new Category { Id = 3, Name = "Мониторы" }
            );

            // Начальные данные в таблице Товары
            modelBuilder.Entity<Product>().HasData(
                new Product
                {
                    Id = 1,
                    Name = "AMD Ryzen 9 9950X3D AM5 OEM",
                    Sku = "Ryzen9_9950X3D_AM5_OEM_16x4.3GHz_TDP170",
                    Description = "Процессор AMD Ryzen 9 9950X3D представляет собой мощное решение для высокопроизводительных систем. Он работает на базовой частоте 4,3 ГГц с возможностью разгона до 5,7 ГГц в режиме boost. Этот чип обладает 16 ядрами и поддерживает 32 потока, что обеспечивает отличную производительность в многозадачных и ресурсоемких приложениях. Процессор оснащен внушительным объемом кэш-памяти L3 в 128 МБ, что способствует быстрому доступу к данным. Он поддерживает оперативную память DDR5 с частотой до 5600 МГц, обеспечивая высокую пропускную способность. Встроенная графика Radeon Graphics позволяет использовать процессор без отдельной видеокарты. С TDP в 170 Вт, этот процессор требует эффективного охлаждения.",
                    Price = 73499.00m,
                    QuantityInStock = 54,
                    CategoryId = 1,
                    SupplierId = 1
                },
                new Product
                {
                    Id = 2,
                    Name = "AMD Ryzen 7 5700X AM4 OEM",
                    Sku = "Ryzen7_5700X_AM4_OEM_8x3.4GHz_TDP65",
                    Description = "Процессор AMD Ryzen 7 5700X OEM является одним из флагманских устройств от AMD. Он основан на архитектуре Zen 3. Конфигурация модели представляет собой 8-ядерное и 16-потоковое устройство. Ее рабочая частота составляет 3.4 ГГц, а максимальная – 4.6 ГГц в режиме Turbo.\nAMD Ryzen 7 5700X OEM поддерживает сокет AM4, а также использует технологии Precision Boost 2 и Precision Boost Overdrive для ускорения частоты работы в зависимости от требований задач. Процессор поддерживает память типа DDR4.",
                    Price = 15399.00m,
                    QuantityInStock = 141,
                    CategoryId = 1,
                    SupplierId = 1
                }
            );

            // Начальные данные в таблице Роли
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Менеджер" },
                new Role { Id = 2, Name = "Продавец" }
            );

            // Начальные данные в таблице Поставщики
            modelBuilder.Entity<Supplier>().HasData(
                new Supplier { Id = 1, Name = "AMDiamond", ContactInfo = "amdiamond.official@gmail.com" }
            );

            // Начальные данные в таблице Пользователи
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    FullName = "Петров Петр Петрович",
                    Login = "user_manager",
                    Password = "user_manager_password",
                    RoleId = 1
                },
                new User
                {
                    Id = 2,
                    FullName = "Иванов Иван Иванович",
                    Login = "user_seller",
                    Password = "user_seller_password",
                    RoleId = 2
                }
            );
        }
    }
}