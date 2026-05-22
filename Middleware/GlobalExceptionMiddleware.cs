using Microsoft.AspNetCore.Mvc;
using ShoppingCart.Exceptions;

namespace ShoppingCart.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "помилка {Path}", context.Request.Path);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/problem+json";

            var problem = exception switch
            {
                ProductNotFoundException notFound => new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                    Title = "Товар не знайдено",
                    Status = StatusCodes.Status404NotFound,
                    Detail = notFound.Message,
                    Instance = context.Request.Path,
                    Extensions = { ["productId"] = notFound.ProductId }
                },

                InvalidCookieException invalidCookie => new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Некоректна кука",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = invalidCookie.Message,
                    Instance = context.Request.Path
                },

                CartException cartError => new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Помилка кошика",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = cartError.Message,
                    Instance = context.Request.Path
                },

                _ => new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                    Title = "Внутрішня помилка сервера",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = "Помилка. Спробуйте пізніше",
                    Instance = context.Request.Path
                }
            };

            context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
            await Results.Problem(problem).ExecuteAsync(context);
        }
    }
}
