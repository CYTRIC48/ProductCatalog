using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductCatalog.Model.model.Review
{
    public class Review
    {
        public int Id { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
        public DateTime DateCreated { get; set; }

        public int UserId { get; set; }
        public User.User User { get; set; }

        public int ProductId { get; set; }
        public Product.Product Product { get; set; }
    }
}
