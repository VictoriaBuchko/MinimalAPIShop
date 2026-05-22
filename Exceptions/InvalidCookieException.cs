namespace ShoppingCart.Exceptions
{
    public class InvalidCookieException : Exception
    {
        public InvalidCookieException()
            : base("Некоректний формат ідентифікатора користувача в куці")
        {
        }
    }
}
