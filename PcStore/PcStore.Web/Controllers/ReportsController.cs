using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcStore.Web.Data;
using PcStore.Web.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PcStore.Web.Controllers
{
    [Authorize(Roles = "Менеджер")] // К контроллеру имеет доступ только менеджер
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        // Формирование отчёта
        [HttpGet]
        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate)
        {
            // Если дата начала не выбрана - назначить начало текущего месяца
            var start = startDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            // Если дата конца не выбрана - назначить текущий момент
            // Если выбрана - назначить конец этого дня (23:59:59)
            var end = endDate.HasValue
                ? endDate.Value.Date.AddDays(1).AddTicks(-1)
                : DateTime.Now;

            // Запрос к БД
            var sales = await _context.Sales
                .Include(s => s.User)
                .Where(s => s.DateTime >= start && s.DateTime <= end)
                .OrderByDescending(s => s.DateTime)
                .ToListAsync();

            // Модель для отображения
            var model = new SaleReportViewModel
            {
                StartDate = start,
                EndDate = endDate ?? DateTime.Now.Date.AddHours(23).AddMinutes(59),
                Sales = sales,
                SalesCount = sales.Count,
                TotalRevenue = sales.Sum(s => s.TotalAmount)
            };

            return View(model);
        }

        // Генерация файла
        [HttpGet]
        public async Task<IActionResult> ExportPdf(DateTime? startDate, DateTime? endDate)
        {
            // Логика получения данных та же, что при формировании отчёта
            var start = startDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var end = endDate.HasValue ? endDate.Value.Date.AddDays(1).AddTicks(-1) : DateTime.Now;

            var sales = await _context.Sales
                .Include(s => s.User)
                .Where(s => s.DateTime >= start && s.DateTime <= end)
                .OrderByDescending(s => s.DateTime)
                .ToListAsync();

            decimal totalRevenue = sales.Sum(s => s.TotalAmount);

            // Генерация PDF
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    // Шапка
                    page.Header()
                        .Text($"Отчет по продажам ({start:dd.MM.yyyy} - {end:dd.MM.yyyy})")
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                    // Содержимое
                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(x =>
                    {
                        // Таблица
                        x.Item().Table(table =>
                        {
                            // Определение колонок
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(50); // ID
                                columns.RelativeColumn(); // Дата
                                columns.RelativeColumn(); // Продавец
                                columns.RelativeColumn(); // Сумма
                            });

                            // Заголовки
                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("№");
                                header.Cell().Element(CellStyle).Text("Дата");
                                header.Cell().Element(CellStyle).Text("Продавец");
                                header.Cell().Element(CellStyle).Text("Сумма");

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.DefaultTextStyle(x => x.SemiBold()).BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5);
                                }
                            });

                            // Строки данных
                            foreach (var sale in sales)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(sale.Id.ToString());
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(sale.DateTime.ToString("g"));
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(sale.User?.FullName ?? "Н/Д");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(sale.TotalAmount.ToString("C"));
                            }
                        });

                        // Итого
                        x.Item().PaddingTop(10).Text($"ИТОГО ВЫРУЧКА: {totalRevenue:C}").Bold().FontSize(16).AlignRight();
                        x.Item().Text($"Количество продаж: {sales.Count}").AlignRight();
                    });

                    // Футер (номер страницы)
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Страница ");
                            x.CurrentPageNumber();
                        });
                });
            });

            // Возврат файла
            byte[] pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"Report_{start:yyyyMMdd}_{end:yyyyMMdd}.pdf");
        }
    }
}