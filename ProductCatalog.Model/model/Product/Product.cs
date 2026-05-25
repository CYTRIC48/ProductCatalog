using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ProductCatalog.Model.model.Product
{
    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Название товара обязательно для заполнения")]
        [Display(Name = "Название")]
        public string Name { get; set; } = string.Empty;

        [Range(0, double.MaxValue, ErrorMessage = "Цена не может быть меньше 0")]
        [Display(Name = "Цена")]
        [DataType(DataType.Currency)]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Price { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Количество не может быть меньше 0")]
        [Display(Name = "Количество")]
        public int Quantity { get; set; }

        [StringLength(1000, ErrorMessage = "Описание не может превышать 1000 символов")]
        [Display(Name = "Описание")]
        public string Description { get; set; }

        [Display(Name = "Фото")]
        public string ImageUrl { get; set; }
        public int CategoryId { get; set; }
        public Category.Category Category { get; set; }
        public int PublisherId { get; set; }
        public Publisher.Publisher Publisher { get; set; }
        public int sellerId { get; set; }
        public User.User seller { get; set; }
    }
}
