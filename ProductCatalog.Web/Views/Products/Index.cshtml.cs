using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Data;
using ProductCatalog.Model.model.Product;

namespace ProductCatalog.Pages.Products
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Product> Products { get; set; } = new List<Product>();
        public string? SearchString { get; set; }
        public string? CurrentSort { get; set; }
        public string NameSort { get; set; } = string.Empty;
        public string PriceSort { get; set; } = string.Empty;
        public string QuantitySort { get; set; } = string.Empty;

        public async Task OnGetAsync(string? sortOrder, string? searchString)
        {
            SearchString = searchString;
            CurrentSort = sortOrder;

            NameSort = sortOrder == "name_asc" ? "name_desc" : "name_asc";
            PriceSort = sortOrder == "price_asc" ? "price_desc" : "price_asc";
            QuantitySort = sortOrder == "qty_asc" ? "qty_desc" : "qty_asc";

            IQueryable<Product> query = _context.Products;

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(p => p.Name.Contains(searchString));
            }

            query = sortOrder switch
            {
                "name_asc" => query.OrderBy(p => p.Name),
                "name_desc" => query.OrderByDescending(p => p.Name),
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "qty_asc" => query.OrderBy(p => p.Quantity),
                "qty_desc" => query.OrderByDescending(p => p.Quantity),
                _ => query.OrderBy(p => p.Id)
            };

            Products = await query.ToListAsync();
        }
    }
}
