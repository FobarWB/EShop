using static System.Console;
using EShop.Data;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.HttpLogging;
using Scalar.AspNetCore;
using EShop.ShoppingCart.Repositories;
using Microsoft.AspNetCore.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EShop.ShoppingCart;
using Microsoft.AspNetCore.Mvc;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors();

builder.Services.AddEShopDbContext();

builder.Services.AddControllers(options =>
{
    // Use ILogger for logging
    var loggerFactory = LoggerFactory.Create(builder => 
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Information);
    });
    var logger = loggerFactory.CreateLogger("OutputFormatters");

    logger.LogInformation("Default output formatters:");
    foreach (IOutputFormatter formatter in options.OutputFormatters)
    {
        if (formatter is OutputFormatter mediaFormatter)
        {
            logger.LogInformation(" {0}, Media types: {1}", 
                mediaFormatter.GetType().Name,
                string.Join(", ", mediaFormatter.SupportedMediaTypes));
        }
        else
        {
            logger.LogInformation($" {formatter.GetType().Name}");
        }
    }
})
.AddXmlDataContractSerializerFormatters()
.AddXmlSerializerFormatters()
 .AddJsonOptions(x =>
 x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
// .ConfigureApiBehaviorOptions(options =>
// {
//     // Customize API behavior options here if needed
//     options.SuppressModelStateInvalidFilter = false; // Example: suppress automatic 400 response
// });
// builder.Services.AddAntiforgery(options => options.SuppressXFrameOptionsHeader = true);
// builder.Services.AddControllersWithViews(options =>
// {
//    // options.Filters.Add(new ValidateAntiForgeryTokenAttribute());
// });


builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "EShop Shopping Cart API";
        document.Info.Version = "v1";
        return Task.CompletedTask;
    });
});

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.All;
    options.RequestBodyLogLimit = 4096; // default is 32k
    options.ResponseBodyLogLimit = 4096; // default is 32k
});

builder.Services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();

var app = builder.Build();

app.UseHttpLogging();

app.UseCors(configurePolicy: options =>
{
  options.WithMethods("GET", "POST", "PUT", "DELETE");
  options.WithOrigins(
    "http://localhost:4200" // Angular client
  );
    options.AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader();
});

app.UseCors("AllowSpecificOrigins");


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // OpenAPI document: /openapi/v1.json
    app.MapScalarApiReference(); // interactive UI: /scalar
}
app.UseHttpLogging();

app.UseHttpsRedirection();

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();
