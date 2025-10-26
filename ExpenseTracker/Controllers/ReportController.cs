using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
            decimal savings = Math.Max(0, balance); // simple "savings" computed from balance (non-negative)

            // Create chart image (pie chart for expense categories)
            byte[] chartBytes = CreateCategoryPieChart(selectedTransactions, 700, 300);

            // Build PDF using QuestPDF
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    // Page header
                    page.Header()
                        .AlignCenter()
                        .Text("Expense Report")
                        .FontSize(18).Bold().FontColor(Colors.Grey.Medium);

                    // Page-level border: wrap main content in a bordered container
                    page.Content()
                        .PaddingVertical(10)
                        .Column(col =>
                        {
                            // Period and brief explanation
                            col.Item().Text($"Period: {StartDate:yyyy-MM-dd} - {EndDate:yyyy-MM-dd}")
                                .SemiBold()
                                .FontSize(12).FontColor(Colors.Grey.Medium);

                            col.Item().PaddingTop(6).PaddingBottom(8)
                                .Text("Detailed overview of income, expenses, balance and savings. The left panel summarizes totals and highlights the current balance; the right panel contains a visual chart and the transaction list.")
                                .FontSize(10)
                                .FontColor(Colors.Grey.Medium);

                            // Main two-column layout: left = summary side-heading, right = chart + details
                            col.Item().Border(1).BorderColor(Colors.Grey.Medium).Padding(10).Row(row =>
                            {
                                // Left summary column (side heading)
                                row.ConstantColumn(170).PaddingRight(10).Column(left =>
                                {
                                    left.Item().Background(Colors.Blue.Medium).Padding(8)
                                        .Text("SUMMARY").FontColor(Colors.White).Bold().FontSize(12).AlignCenter();

                                    left.Item().PaddingTop(10).Column(summary =>
                                    {
                                        // Balance
                                        var balanceColor = balance >= 0 ? Colors.Green.Medium : Colors.Red.Medium;
                                        summary.Item().Text("Balance").FontSize(10).SemiBold();
                                        summary.Item().PaddingBottom(8).Text(balance.ToString("C2")).FontSize(14).Bold().FontColor(balanceColor);

                                        // Expense
                                        summary.Item().Text("Total Expense").FontSize(10).SemiBold();
                                        summary.Item().PaddingBottom(8).Text(totalExpense.ToString("C2")).FontSize(12).FontColor(Colors.Red.Medium);

                                        // Income
                                        summary.Item().Text("Total Income").FontSize(10).SemiBold();
                                        summary.Item().PaddingBottom(8).Text(totalIncome.ToString("C2")).FontSize(12).FontColor(Colors.Green.Medium);

                                        // Savings
                                        summary.Item().Text("Estimated Savings").FontSize(10).SemiBold();
                                        summary.Item().Text(savings.ToString("C2")).FontSize(12).FontColor(Colors.Purple.Medium);
                                    });

                                    // Small explanatory note
                                    left.Item().PaddingTop(12).Text("Explanation")
                                        .FontSize(10).SemiBold();
                                    left.Item().PaddingTop(4)
                                        .Text("Balance = Income - Expense. Savings shown as non-negative portion of balance.")
                                        .FontSize(9).FontColor(Colors.Grey.Medium);
                                });

                                // Right main column (chart + transactions)
                                row.RelativeColumn().Column(right =>
                                {
                                    // Chart area with heading
                                    right.Item().Text("Expense by Category").FontSize(12).Bold().FontColor(Colors.Grey.Medium);
                                    right.Item().PaddingTop(6).Image(chartBytes, ImageScaling.FitWidth);

                                    // Transactions table header
                                    right.Item().PaddingTop(10).Row(header =>
                                    {
                                        header.RelativeColumn(2).Text("Date").Bold();
                                        header.RelativeColumn(4).Text("Category").Bold();
                                        header.RelativeColumn(2).Text("Amount").Bold().AlignRight();
                                    });

                                    // Transactions list (wrap if long)
                                    foreach (var t in selectedTransactions)
                                    {
                                        right.Item().PaddingTop(4).Row(tx =>
                                        {
                                            tx.RelativeColumn(2).Text(t.Date.ToString("yyyy-MM-dd")).FontSize(10);
                                            tx.RelativeColumn(4).Text(t.Category?.Title ?? "(Uncategorized)").FontSize(10);
                                            // Color negative conceptually for expenses (amounts stored as positive)
                                            var isExpense = t.Category != null && string.Equals(t.Category.Type, "Expense", StringComparison.OrdinalIgnoreCase);
                                            var amtColor = isExpense ? Colors.Red.Medium : Colors.Green.Medium;
                                            tx.RelativeColumn(2).AlignRight().Text(t.Amount.ToString("C2")).FontColor(amtColor).FontSize(10);
                                        });
                                    }

                                    // Totals row
                                    right.Item().PaddingTop(12).Row(tot =>
                                    {
                                        tot.RelativeColumn().Text($"Total Income: {totalIncome.ToString("C2")}").SemiBold();
                                        tot.RelativeColumn().Text($"Total Expense: {totalExpense.ToString("C2")}").SemiBold().AlignCenter().FontColor(Colors.Red.Medium);
                                        tot.RelativeColumn().Text($"Balance: {balance.ToString("C2")}").SemiBold().AlignRight().FontColor(balance >= 0 ? Colors.Green.Medium : Colors.Red.Medium);
                                    });
                                });
                            });

                            // Footer note / space
                            col.Item().PaddingTop(8).Text("This report is auto-generated. Review category assignments for accuracy.")
                                .FontSize(9).FontColor(Colors.Grey.Medium);
                        });

                    // Page footer
                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("Generated: ").SemiBold();
                            text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).FontColor(Colors.Grey.Medium);
                        });
                });
            });

            byte[] pdfBytes = doc.GeneratePdf();

            return File(pdfBytes, "application/pdf", "ExpenseReport_Detailed.pdf");
        }

        // Creates a simple pie-chart PNG showing Expense totals per category.
        // Uses System.Drawing to render a clean pie + legend with consistent colors.
        private byte[] CreateCategoryPieChart(List<Transaction> transactions, int width = 700, int height = 300)
        {
            // Group only Expense-type transactions by category title
            var expenseGroups = transactions
                .Where(t => t.Category != null && string.Equals(t.Category.Type, "Expense", StringComparison.OrdinalIgnoreCase))
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Category.Title) ? "Uncategorized" : t.Category.Title)
                .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.Total)
                .ToList();

            using var bmp = new Bitmap(width, height);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.White);

            // Palette - pleasant contrasting colors (System.Drawing)
            var palette = new[]
            {
                System.Drawing.Color.FromArgb(0x4E, 0x8A, 0xF2), // blue
                System.Drawing.Color.FromArgb(0xF2, 0x6B, 0x6B), // red
                System.Drawing.Color.FromArgb(0xFF, 0xC1, 0x07), // yellow
                System.Drawing.Color.FromArgb(0x4C, 0xC7, 0xA1), // green
                System.Drawing.Color.FromArgb(0x9B, 0x59, 0xB6), // purple
                System.Drawing.Color.FromArgb(0xE6, 0x67, 0xB5), // pink
                System.Drawing.Color.FromArgb(0x7F, 0x8C, 0x8D), // gray
            };

            if (!expenseGroups.Any())
            {
                using var font = new Font("Arial", 14, FontStyle.Regular);
                var text = "No expense data for the selected period";
                var sz = g.MeasureString(text, font);
                g.DrawString(text, font, Brushes.Gray, (width - sz.Width) / 2, (height - sz.Height) / 2);
            }
            else
            {
                // define pie rectangle (square) on left side
                int padding = 16;
                int pieSize = Math.Min(height - padding * 2, (width / 2) - padding);
                var pieRect = new Rectangle(padding, padding, pieSize, pieSize);

                decimal total = expenseGroups.Sum(x => x.Total);
                float startAngle = 0f;

                // Draw pie slices
                for (int i = 0; i < expenseGroups.Count; i++)
                {
                    var item = expenseGroups[i];
                    float sweep = (float)((double)item.Total / (double)total * 360.0);
                    using var brush = new SolidBrush(palette[i % palette.Length]);
                    g.FillPie(brush, pieRect, startAngle, sweep);
                    g.DrawPie(Pens.White, pieRect, startAngle, sweep); // light separator
                    startAngle += sweep;
                }

                // Draw legend on right side
                int legendX = pieRect.Right + 20;
                int legendY = pieRect.Top;
                int boxSize = 12;
                using var font = new Font("Arial", 10, FontStyle.Regular);

                // Limit legend items to a reasonable number (if many categories, show top N and group rest)
                int maxLegendItems = 10;
                var legendItems = expenseGroups.Take(maxLegendItems).ToList();
                if (expenseGroups.Count > maxLegendItems)
                {
                    var othersTotal = expenseGroups.Skip(maxLegendItems).Sum(x => x.Total);
                    legendItems.Add(new { Category = "Other", Total = othersTotal });
                }

                for (int i = 0; i < legendItems.Count; i++)
                {
                    var item = legendItems[i];
                    var brush = new SolidBrush(palette[i % palette.Length]);
                    g.FillRectangle(brush, legendX, legendY + i * 20, boxSize, boxSize);
                    g.DrawRectangle(Pens.Black, legendX, legendY + i * 20, boxSize, boxSize);

                    string text = $"{item.Category} ({item.Total:C0})";
                    g.DrawString(text, font, Brushes.Black, legendX + boxSize + 8, legendY + i * 20 - 1);
                }
            }

            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }
    }
}