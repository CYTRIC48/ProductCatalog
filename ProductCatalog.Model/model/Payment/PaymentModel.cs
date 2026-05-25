namespace ProductCatalog.Model.model.Payment
{
    public class Payment
    {
        public int Id { get; set; }
        public double Amount { get; set; }
        public string Status { get; set; }

        public int ProductId { get; set; }
        public Product.Product Product { get; set; }

        public int BasketId { get; set; }
        public Cart.Cart Cart { get; set; }
    }
}
