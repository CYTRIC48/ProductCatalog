using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Data;
using ProductCatalog.Model.model.Product;

namespace ProductCatalog.Web.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? sortOrder, string? searchString)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            ViewData["NameSort"] = sortOrder == "name_asc" ? "name_desc" : "name_asc";
            ViewData["PriceSort"] = sortOrder == "price_asc" ? "price_desc" : "price_asc";
            ViewData["QuantitySort"] = sortOrder == "qty_asc" ? "qty_desc" : "qty_asc";
            ViewData["CurrentSort"] = sortOrder;
            ViewData["SearchString"] = searchString;

            var products = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .Where(p => p.sellerId == userId.Value)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
                products = products.Where(p => p.Name.Contains(searchString));

            products = sortOrder switch
            {
                "name_asc" => products.OrderBy(p => p.Name),
                "name_desc" => products.OrderByDescending(p => p.Name),
                "price_asc" => products.OrderBy(p => p.Price),
                "price_desc" => products.OrderByDescending(p => p.Price),
                "qty_asc" => products.OrderBy(p => p.Quantity),
                "qty_desc" => products.OrderByDescending(p => p.Price),
                _ => products.OrderBy(p => p.Name)
            };

            return View(await products.ToListAsync());
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name");
            ViewBag.Publishers = new SelectList(await _context.Publishers.ToListAsync(), "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product)
        {
            ModelState.Remove("Category");
            ModelState.Remove("Publisher");
            ModelState.Remove("Seller");

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            product.sellerId = userId.Value;

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name");
                ViewBag.Publishers = new SelectList(await _context.Publishers.ToListAsync(), "Id", "Name");
                return View(product);
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .Include(p => p.seller)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            var reviews = await _context.Reviews
                .Include(r => r.User)
                .Where(r => r.ProductId == id)
                .OrderByDescending(r => r.DateCreated)
                .ToListAsync();

            var recommended = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.Id != id)
                .Take(8)
                .ToListAsync();

            var rating = reviews.Any() ? (decimal)reviews.Average(r => r.Rating) : 0;

            var userId = HttpContext.Session.GetInt32("UserId");

            bool hasPurchased = false;
            bool alreadyReviewed = false;

            if (userId != null)
            {
                hasPurchased = await _context.Orders
                    .Include(o => o.Items)
                    .Where(o => o.UserId == userId)
                    .AnyAsync(o => o.Items.Any(i => i.ProductName == product.Name));

                alreadyReviewed = await _context.Reviews
                    .AnyAsync(r => r.UserId == userId && r.ProductId == id);
            }

            ViewBag.Reviews = reviews;
            ViewBag.Recommended = recommended;
            ViewBag.Rating = rating;
            ViewBag.ReviewsCount = reviews.Count;
            ViewBag.HasPurchased = hasPurchased;
            ViewBag.AlreadyReviewed = alreadyReviewed;

            return View(product);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", product.CategoryId);
            ViewBag.Publishers = new SelectList(await _context.Publishers.ToListAsync(), "Id", "Name", product.PublisherId);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product)
        {
            if (id != product.Id) return BadRequest();
            ModelState.Remove("Category");
            ModelState.Remove("Publisher");
            ModelState.Remove("seller");

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            product.sellerId = userId.Value;

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", product.CategoryId);
                ViewBag.Publishers = new SelectList(await _context.Publishers.ToListAsync(), "Id", "Name", product.PublisherId);
                return View(product);
            }

            _context.Products.Update(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound();
            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null) _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            var available = product.Quantity;
            if (available <= 0)
            {
                TempData["CartMessage"] = "Товар отсутствует в наличии";
                return RedirectToAction("Details", new { id = productId });
            }

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new ProductCatalog.Model.model.Cart.Cart { UserId = userId.Value };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var existing = cart.Items.FirstOrDefault(i => i.GameId == productId);

            if (existing != null)
            {
                var total = existing.Quantity + quantity;
                if (total > available)
                {
                    existing.Quantity = available;
                    TempData["CartMessage"] = $"В корзине не может быть больше {available} шт. этого товара.";
                }
                else
                {
                    existing.Quantity = total;
                }
            }
            else
            {
                var toAdd = Math.Min(quantity, available);
                cart.Items.Add(new ProductCatalog.Model.model.Cart.CartItem
                {
                    GameId = productId,
                    Quantity = toAdd,
                    CartId = cart.Id
                });
                if (toAdd < quantity) TempData["CartMessage"] = $"Добавлено только {toAdd} шт. — столько есть в наличии.";
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToAction("Basket", "Home");
        }
    }
}