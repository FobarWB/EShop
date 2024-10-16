using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Northwind.ShoppingCart
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Request.EnableBuffering(); // Позволяет читать тело запроса несколько раз
            var bodyAsText = await new StreamReader(context.Request.Body).ReadToEndAsync();
            context.Request.Body.Position = 0; // Сбросить позицию потока

            _logger.LogInformation($"Request Body: {bodyAsText}");

            await _next(context);
        }
    }
}