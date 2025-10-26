using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExpenseTracker.Models
{
    public class Transaction
    {
        [Key]
        public int TransactionID { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Category is Required.")]
        public int CategoryID { get; set; }

        // Required: Navigation property for Entity Framework. Enables relationship mapping between Transaction and Category.
        
        public Category? Category { get; set; }

        [Range(1, int.MaxValue,ErrorMessage = "Amount Should be > 0")]
        public decimal Amount { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        [Column(TypeName = "nvarchar(500)")]
        public string? Note { get; set; }

        [NotMapped]
        public string? CategoryTitleWithIcon
        {
            get
            {
                if (Category == null)
                {
                    return "";
                }
                else
                    return Category.Icon + " " + Category.Title;
            }
        }

        [NotMapped]
        public string? FormattedAmount
        {
            get
            {
                if (Category == null || Category.Type.ToLower() == "expense")
                {
                    return "-" + Amount.ToString("C0");
                }
                else
                    return "+" + Amount.ToString("C0");
            }
        }

    }
}
