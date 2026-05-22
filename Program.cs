using ShoppingCart.Exceptions;
using ShoppingCart.Extensions;
using ShoppingCart.Middleware;
using ShoppingCart.Models;

var builder = WebApplication.CreateBuilder(args);

//логування
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

//сесії
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var products = new List<Product>
{
    new Product { Id = 1, Name = "Ноутбук", Price = 35000 },
    new Product { Id = 2, Name = "Смартфон", Price = 15000 },
    new Product { Id = 3, Name = "Навушники", Price = 2000 },
    new Product { Id = 4, Name = "Планшет", Price = 12000 },
    new Product { Id = 5, Name = "Смарт-годинник", Price = 5000 }
};

builder.Services.AddSingleton(products);

var app = builder.Build();

//middleware для обробки помилок
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSession();

//ідентифікація користувача через кукі
app.MapGet("/identify", (HttpContext context, ILogger<Program> logger) =>
{
    if (context.Request.Cookies.TryGetValue("UserId", out var existingId))
    {
        if (!Guid.TryParse(existingId, out var existingGuid))
        {
            logger.LogWarning("некоректний формат кукі UserId на /identify");
            throw new InvalidCookieException();
        }

        logger.LogInformation("існуючий користувач ідентифікований: {UserId}", existingGuid);
        return Results.Ok(new { userId = existingGuid, message = "існуючий користувач" });
    }

    //новий GUID
    var newUserId = Guid.NewGuid();
    context.Response.Cookies.Append("UserId", newUserId.ToString(), new CookieOptions
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddMinutes(30)
    });

    logger.LogInformation("новий користувач ідентифікований: {UserId}", newUserId);
    return Results.Ok(new { userId = newUserId, message = "новий користувач" });
});

//додавання товару до кошика
app.MapPost("/cart/add/{productId:int}", (
    int productId,
    int quantity,
    HttpContext context,
    List<Product> catalog,
    ILogger<Program> logger) =>
{
    if (!context.Request.Cookies.TryGetValue("UserId", out var rawId) ||
        !Guid.TryParse(rawId, out var userId))
    {
        logger.LogWarning("спроба додати товар без валідної кукі UserId");
        throw new InvalidCookieException();
    }

    //перевірка кількості
    if (quantity <= 0)
    {
        logger.LogWarning("некоректна кількість {Quantity} для товару {ProductId}, UserId: {UserId}", quantity, productId, userId);
        throw new CartException("кількість товару повинна бути більше 0");
    }

    //чи існує товар
    var product = catalog.FirstOrDefault(p => p.Id == productId);
    if (product is null)
    {
        logger.LogWarning("товар з ID {ProductId} не знайдено, UserId: {UserId}", productId, userId);
        throw new ProductNotFoundException(productId);
    }

    //отримуємо або створюємо кошик
    var cart = context.Session.GetObject<List<CartItem>>("Cart") ?? new List<CartItem>();

    var existing = cart.FirstOrDefault(c => c.ProductId == productId);
    if (existing is not null)
    {
        existing.Quantity += quantity;
    }
    else
    {
        cart.Add(new CartItem
        {
            ProductId = product.Id,
            Name = product.Name,
            Price = product.Price,
            Quantity = quantity
        });
    }

    context.Session.SetObject("Cart", cart);

    logger.LogInformation("товар {ProductName} x{Quantity} доданий до кошика, UserId: {UserId}", product.Name, quantity, userId);
    return Results.Ok(new { message = $"Товар {product.Name} x{quantity} додано до кошика" });
});

//перегляд кошика
app.MapGet("/cart", (HttpContext context, ILogger<Program> logger) =>
{
    if (!context.Request.Cookies.TryGetValue("UserId", out var rawId) ||
        !Guid.TryParse(rawId, out var userId))
    {
        logger.LogWarning("спроба переглянути кошик без валідної кукі UserId");
        throw new InvalidCookieException();
    }

    var cart = context.Session.GetObject<List<CartItem>>("Cart") ?? new List<CartItem>();

    logger.LogInformation("перегляд кошика, UserId: {UserId}, товарів: {Count}", userId, cart.Count);
    return Results.Ok(cart);
});

//очищення кошика
app.MapGet("/cart/clear", (HttpContext context, ILogger<Program> logger) =>
{
    if (!context.Request.Cookies.TryGetValue("UserId", out var rawId) ||
        !Guid.TryParse(rawId, out var userId))
    {
        logger.LogWarning("спроба очистити кошик без валідної кукі UserId");
        throw new InvalidCookieException();
    }

    context.Session.Remove("Cart");

    logger.LogInformation("кошик очищено, UserId: {UserId}", userId);
    return Results.Ok(new { message = "кошик очищено" });
});

app.Run();