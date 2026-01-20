using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PcStore.Web.Data;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Аутентификация через Куки
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login"; // Страница входа, если пользователь не вошел
        options.AccessDeniedPath = "/Account/AccessDenied"; // Страница отказа в доступе, если нет прав
    });

// Указание лицензии для QuestPDF для генерации отчётов
QuestPDF.Settings.License = LicenseType.Community;

// Add services to the container.
builder.Services.AddControllersWithViews();

// Подключение к MySQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(9, 2, 0))
    ));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Products}/{action=Index}/{id?}"); // Страница по умолчанию

app.Run();
