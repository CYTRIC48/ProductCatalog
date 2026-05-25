using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Data;

namespace ProductCatalog.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? sortOrder, string? searchString)
        {
            ViewData["NameSort"] = sortOrder == "name_asc" ? "name_desc" : "name_asc";
            ViewData["CurrentSort"] = sortOrder;
            ViewData["SearchString"] = searchString;

            var products = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
                products = products.Where(p => p.Name.Contains(searchString));

            products = sortOrder switch
            {
                "name_asc" => products.OrderBy(p => p.Name),
                "name_desc" => products.OrderByDescending(p => p.Name),
                _ => products.OrderBy(p => p.Name)
            };

            return View(await products.ToListAsync());
        }

        public async Task<IActionResult> Basket()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var cart = await _context.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.Game)
                        .ThenInclude(g => g.Category)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            return View(cart);
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int itemId)
        {
            var item = await _context.CartItems.FindAsync(itemId);
            if (item != null)
            {
                _context.CartItems.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Basket");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int itemId, int quantity)
        {
            var item = await _context.CartItems
                .Include(i => i.Game)
                .FirstOrDefaultAsync(i => i.Id == itemId);

            if (item == null) return RedirectToAction("Basket");

            if (quantity <= 0)
            {
                _context.CartItems.Remove(item);
                await _context.SaveChangesAsync();
                TempData["CartMessage"] = "Товар удалён из корзины";
                return RedirectToAction("Basket");
            }

            var available = item.Game?.Quantity ?? 0;
            if (quantity > available)
            {
                item.Quantity = available;
                TempData["CartMessage"] = $"Доступно только {available} шт. данного товара. Количество в корзине установлено на {available}.";
            }
            else
            {
                item.Quantity = quantity;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Basket");
        }
        [HttpPost]
        public async Task<IActionResult> ClearCart()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart != null)
            {
                _context.CartItems.RemoveRange(cart.Items);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Basket");
        }

        public async Task<IActionResult> Checkout()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var cart = await _context.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.Game)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null || !cart.Items.Any())
                return RedirectToAction("Basket");

            return View(cart);
        }

        [HttpPost]
        public async Task<IActionResult> PlaceOrder(string cardNumber, string cardExpiry, string cardCvv)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var cart = await _context.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.Game)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null || !cart.Items.Any())
                return RedirectToAction("Basket");

            foreach (var item in cart.Items)
            {
                if (item.Game.Quantity < item.Quantity)
                {
                    TempData["Error"] = $"Товар «{item.Game.Name}» доступен только в количестве {item.Game.Quantity} шт.";
                    return RedirectToAction("Basket");
                }
            }

            foreach (var item in cart.Items)
            {
                item.Game.Quantity -= item.Quantity;
            }

            var order = new ProductCatalog.Model.model.Order.Order
            {
                UserId = userId.Value,
                TotalAmount = cart.TotalPrice,
                Status = "Paid",
                CreatedAt = DateTime.UtcNow,
                Items = cart.Items.Select(i => new ProductCatalog.Model.model.Order.OrderItem
                {
                    ProductName = i.Game.Name,
                    Price = i.Game.Price,
                    Quantity = i.Quantity
                }).ToList()
            };

            _context.Orders.Add(order);
            _context.CartItems.RemoveRange(cart.Items);
            await _context.SaveChangesAsync();

            return RedirectToAction("OrderSuccess", new { orderId = order.Id });
        }

        public async Task<IActionResult> OrderSuccess(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound();
            return View(order);
        }
        public async Task<IActionResult> OrderHistory()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var orders = await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        [HttpPost]
        public async Task<IActionResult> AddReview(int productId, int rating, string comment)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var hasPurchased = await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.UserId == userId)
                .AnyAsync(o => o.Items.Any(i => i.ProductName == _context.Products
                    .Where(p => p.Id == productId)
                    .Select(p => p.Name)
                    .FirstOrDefault()));

            if (!hasPurchased)
            {
                TempData["ReviewError"] = "Оставить отзыв можно только после покупки товара.";
                return RedirectToAction("Details", "Products", new { id = productId });
            }

            var alreadyReviewed = await _context.Reviews
                .AnyAsync(r => r.UserId == userId && r.ProductId == productId);

            if (alreadyReviewed)
            {
                TempData["ReviewError"] = "Вы уже оставляли отзыв на этот товар.";
                return RedirectToAction("Details", "Products", new { id = productId });
            }

            var review = new ProductCatalog.Model.model.Review.Review
            {
                UserId = userId.Value,
                ProductId = productId,
                Rating = Math.Clamp(rating, 1, 5),
                Comment = comment,
                DateCreated = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            TempData["ReviewSuccess"] = "Отзыв успешно добавлен!";
            return RedirectToAction("Details", "Products", new { id = productId });
        }
        public async Task<IActionResult> EditReview(int reviewId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var review = await _context.Reviews
                .Include(r => r.Product)
                .FirstOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId);

            if (review == null) return NotFound();

            return View(review);
        }

        [HttpPost]
        public async Task<IActionResult> EditReview(int reviewId, int rating, string comment)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId);

            if (review == null) return NotFound();

            review.Rating = Math.Clamp(rating, 1, 5);
            review.Comment = comment;
            review.DateCreated = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["ReviewSuccess"] = "Отзыв успешно обновлён!";
            return RedirectToAction("Details", "Products", new { id = review.ProductId });
        }
        public async Task<IActionResult> MyReviews()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var reviews = await _context.Reviews
                .Include(r => r.Product)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.DateCreated)
                .ToListAsync();

            return View(reviews);
        }
    }
}
