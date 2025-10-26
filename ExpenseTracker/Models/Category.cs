using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExpenseTracker.Models
{
    public class Category
    {
        [Key]
        public int CategoryID { get; set; }

        [Column (TypeName = "nvarchar(100)")]
        [Required(ErrorMessage = "Title is Required")]
        public string Title { get; set; }

        [Column (TypeName = "nvarchar(100)")]
        public string Icon { get; set; }

        [Column(TypeName = "nvarchar(50)")]
        public string Type { get; set; } = "Expense"; // "Income" or "Expense"

        [NotMapped]
        public string? TitleWithIcon
        {
            get
            {
                return this.Title + " " + this.Icon;
            }
        }
    }
}
