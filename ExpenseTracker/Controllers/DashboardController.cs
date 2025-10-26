using ExpenseTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ExpenseTracker.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Supports: range (week|month|3months|6months|year) OR custom startDate + endDate (preferred if provided)
        public async Task<ActionResult> Index(string range = "week", DateTime? startDate = null, DateTime? endDate = null)
        {
            DateTime EndDate;
            DateTime StartDate;

            if (startDate.HasValue && endDate.HasValue)
            {
                // Use provided custom dates (date part only)
                StartDate = startDate.Value.Date;
                EndDate = endDate.Value.Date;

                // Ensure StartDate <= EndDate
                if (StartDate > EndDate)
                {
                    var tmp = StartDate;
                    StartDate = EndDate;
                    EndDate = tmp;
                }

                ViewBag.SelectedRange = "custom";
            }
            else
            {
                // Map named ranges to days
                int days = range?.ToLowerInvariant() switch
                {
                    "month" => 30,
                    "3months" => 90,
                    "6months" => 180,
                    "year" => 365,
                    _ => 7 // "week" or any other fallback
                };

                EndDate = DateTime.Today;
                StartDate = EndDate.AddDays(-(days - 1));
                ViewBag.SelectedRange = range?.ToLowerInvariant() ?? "week";
            }

            // If EndDate wasn't set in the named-range branch, it was set above. Re-calc EndDate if custom branch set StartDate only.
            EndDate = endDate.HasValue ? endDate.Value.Date : EndDate;

            // Expose Start/End to view for pre-filling inputs (format yyyy-MM-dd for <input type="date">)
            ViewBag.StartDate = StartDate.ToString("yyyy-MM-dd");
            ViewBag.EndDate = EndDate.ToString("yyyy-MM-dd");

            // Fetch selected transactions within the inclusive date range
            List<Transaction> SelectedTransactions = await _context.Transactions
                .Include(x => x.Category)
                .Where(y => y.Date >= StartDate && y.Date < EndDate.AddDays(1))
                .ToListAsync();

            // Total Income (use decimal to preserve cents)
            decimal TotalIncome = SelectedTransactions
                .Where(x => x.Category != null && string.Equals(x.Category.Type, "Income", StringComparison.OrdinalIgnoreCase))
                .Sum(y => y.Amount);
            ViewBag.TotalIncome = TotalIncome.ToString("C0");

            // Total Expense
            decimal TotalExpense = SelectedTransactions
                .Where(x => x.Category != null && string.Equals(x.Category.Type, "Expense", StringComparison.OrdinalIgnoreCase))
                .Sum(y => y.Amount);
            ViewBag.TotalExpense = TotalExpense.ToString("C0");

            // Balance
            decimal Balance = TotalIncome - TotalExpense;
            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-US");
            culture.NumberFormat.CurrencyNegativePattern = 1;
            ViewBag.Balance = String.Format(culture, "{0:C0}", Balance);

            // Doughnut Chart - Expense By Category (only non-null categories)
            ViewBag.DoughnutChartData = SelectedTransactions
                .Where(i => i.Category != null && string.Equals(i.Category.Type, "Expense", StringComparison.OrdinalIgnoreCase))
                .GroupBy(j => j.Category.CategoryID)
                .Select(k => new
                {
                    categoryTitleWithIcon = k.First().Category.Icon + " " + k.First().Category.Title,
                    amount = k.Sum(j => j.Amount),
                    formattedAmount = k.Sum(j => j.Amount).ToString("C0"),
                })
                .OrderByDescending(l => l.amount)
                .ToList();

            // Spline Chart - Income vs Expense
            List<SplineChartData> IncomeSummary = SelectedTransactions
                .Where(i => i.Category != null && string.Equals(i.Category.Type, "Income", StringComparison.OrdinalIgnoreCase))
                .GroupBy(j => j.Date.Date)
                .Select(k => new SplineChartData()
                {
                    day = k.Key.ToString("dd-MMM"),
                    income = k.Sum(l => l.Amount)
                })
                .ToList();

            List<SplineChartData> ExpenseSummary = SelectedTransactions
                .Where(i => i.Category != null && string.Equals(i.Category.Type, "Expense", StringComparison.OrdinalIgnoreCase))
                .GroupBy(j => j.Date.Date)
                .Select(k => new SplineChartData()
                {
                    day = k.Key.ToString("dd-MMM"),
                    expense = k.Sum(l => l.Amount)
                })
                .ToList();

            // Build the x-axis labels for the selected range
            int totalDays = (int)(EndDate - StartDate).TotalDays + 1;
            string[] LastNDays = Enumerable.Range(0, totalDays)
                .Select(i => StartDate.AddDays(i).ToString("dd-MMM"))
                .ToArray();

            ViewBag.SplineChartData = from day in LastNDays
                                      join income in IncomeSummary on day equals income.day into dayIncomeJoined
                                      from income in dayIncomeJoined.DefaultIfEmpty()
                                      join expense in ExpenseSummary on day equals expense.day into expenseJoined
                                      from expense in expenseJoined.DefaultIfEmpty()
                                      select new
                                      {
                                          day = day,
                                          income = income == null ? 0 : income.income,
                                          expense = expense == null ? 0 : expense.expense,
                                      };

            // Recent Transactions (latest 5 overall). To limit these to the selected range, uncomment the Where.
            ViewBag.RecentTransactions = await _context.Transactions
                .Include(i => i.Category)
                //.Where(t => t.Date >= StartDate && t.Date < EndDate.AddDays(1))
                .OrderByDescending(j => j.Date)
                .Take(5)
                .ToListAsync();

            return View();
        }
    }

    public class SplineChartData
    {
        public string day { get; set; } = string.Empty;
        public decimal income { get; set; }
        public decimal expense { get; set; }
    }
}
