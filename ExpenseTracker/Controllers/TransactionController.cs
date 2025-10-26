using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Models;

namespace ExpenseTracker.Controllers
{
    public class TransactionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TransactionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Transaction
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Transactions.Include(t => t.Category);
            return View(await applicationDbContext.ToListAsync());
        }

        

        // GET: Transaction/Create
        public IActionResult AddOrEdit(int id=0)
        {
            //ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryID");
            PopulateCategories();
            if(id==0)
            {
                return View(new Transaction());
            }
            else
            {
                return View(_context.Transactions.Find(id));
            }
                
        }

        // POST: Transaction/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrEdit([Bind("TransactionID,CategoryID,Amount,Date,Note")] Transaction transaction)
        {
            if (ModelState.IsValid)
            {
                if (transaction.TransactionID == 0)
                    _context.Add(transaction);
                else
                    _context.Update(transaction);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            PopulateCategories();
            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryID", transaction.CategoryID);
            return View(transaction);
        }


        // GET: Transaction/Delete/5
        [HttpPost ,ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int? id)
        {

            if(_context.Transactions == null)
            {
                return Problem("DB is Null");
            }


            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction != null)
            {
                _context.Transactions.Remove(transaction);
            }
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [NonAction]
        public void PopulateCategories()
        {
            var CategoryCollection = _context.Categories.ToList();
            Category Default = new Category()
            {
                CategoryID = 0,
                Title = "Choose A Category"
            };
            CategoryCollection.Insert(0, Default);
            ViewBag.Categories = CategoryCollection;

        }

        

        private bool TransactionExists(int id)
        {
            return _context.Transactions.Any(e => e.TransactionID == id);
        }
    }
}
