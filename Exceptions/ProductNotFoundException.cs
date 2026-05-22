namespace ShoppingCart.Exceptions
{
    public class ProductNotFoundException : Exception
    {
        public int ProductId { get; }

        public ProductNotFoundException(int productId)
            : base($"Товар з ID {productId} не знайдено")
        {
            ProductId = productId;
        }
    }
}
