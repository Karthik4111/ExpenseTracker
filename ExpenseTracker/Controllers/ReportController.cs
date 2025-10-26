using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ExpenseTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ExpenseTracker.Controllers
{
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Generates PDF immediately. Supports same ranges/custom dates as DashboardController.
        public async Task<IActionResult> GeneratePdf(string range = "week", DateTime? startDate = null, DateTime? endDate = null)
        {
            DateTime EndDate;
            DateTime StartDate;

            if (startDate.HasValue && endDate.HasValue)
            {
                StartDate = startDate.Value.Date;
                EndDate = endDate.Value.Date;
                if (StartDate > EndDate)
                {
                    var tmp = StartDate;
                    StartDate = EndDate;
                    EndDate = tmp;
                }
            }
            else
            {
                int days = range?.ToLowerInvariant() switch
                {
                    "month" => 30,
                    "3months" => 90,
                    "6months" => 180,
                    "year" => 365,
                    _ => 7
                };

                EndDate = DateTime.Today;
                StartDate = EndDate.AddDays(-(days - 1));
            }

            EndDate = endDate.HasValue ? endDate.Value.Date : EndDate;

            var selectedTransactions = await _context.Transactions
                .Include(x => x.Category)
                .Where(y => y.Date >= StartDate && y.Date < EndDate.AddDays(1))
                .OrderBy(t => t.Date)
                .ToListAsync();

            decimal totalIncome = selectedTransactions
                .Where(x => x.Category != null && string.Equals(x.Category.Type, "Income", StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Amount);

            decimal totalExpense = selectedTransactions
                .Where(x => x.Category != null && string.Equals(x.Category.Type, "Expense", StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Amount);

            decimal balance = totalIncome - totalExpense;

            // Build PDF using QuestPDF
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header()
                        .AlignCenter()
                        .Text($"Expense Report")
                        .FontSize(16).Bold();

                    page.Content()
                        .PaddingVertical(10)
                        .Column(column =>
                        {
                            column.Item().Text($"Period: {StartDate:yyyy-MM-dd} — {EndDate:yyyy-MM-dd}").SemiBold();

                            // Table header
                            column.Item().PaddingTop(8).Row(row =>
                            {
                                row.RelativeColumn().Text("Date").Bold();
                                row.RelativeColumn().Text("Category").Bold();
                                row.RelativeColumn().Text("Amount").Bold().AlignRight();
                            });

                            // Transactions
                            foreach (var t in selectedTransactions)
                            {
                                column.Item().PaddingTop(4).Row(row =>
                                {
                                    row.RelativeColumn().Text(t.Date.ToString("yyyy-MM-dd"));
                                    row.RelativeColumn().Text(t.Category?.Title ?? string.Empty);
                                    row.RelativeColumn().Text(t.Amount.ToString("C2")).AlignRight();
                                });
                            }

                            // Totals
                            column.Item().PaddingTop(12).Row(row =>
                            {
                                row.RelativeColumn().Text($"Total Income: {totalIncome.ToString("C2")}").SemiBold();
                                row.RelativeColumn().Text($"Total Expense: {totalExpense.ToString("C2")}").SemiBold().AlignCenter();
                                row.RelativeColumn().Text($"Balance: {balance.ToString("C2")}").SemiBold().AlignRight();
                            });
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("Generated: ").SemiBold();
                            text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                        });
                });
            });

            byte[] pdfBytes = doc.GeneratePdf();

            return File(pdfBytes, "application/pdf", "ExpenseReport.pdf");
        }
    }
}