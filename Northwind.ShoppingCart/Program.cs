using static System.Console;
using EShop.Shared;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.HttpLogging;
using Swashbuckle.AspNetCore.SwaggerUI;
using Northwind.ShoppingCart.Repositories;
using Microsoft.AspNetCore.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Northwind.ShoppingCart;
using Microsoft.AspNetCore.Mvc;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors();

builder.Services.AddNorthwindContext();

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
x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles)
.ConfigureApiBehaviorOptions(options =>
{
    // Customize API behavior options here if needed
    options.SuppressModelStateInvalidFilter = false; // Example: suppress automatic 400 response
});
builder.Services.AddAntiforgery(options => options.SuppressXFrameOptionsHeader = true);
builder.Services.AddControllersWithViews(options =>
{
   // options.Filters.Add(new ValidateAntiForgeryTokenAttribute());
});
builder.Services.AddSwaggerGen(c =>
{
c.SwaggerDoc("v1", new()
{ Title = "Northwind Shopping cart API", Version = "v1" });
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
    options.AllowAnyHeader();
});

app.UseCors("AllowSpecificOrigins");


if (builder.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json",
         "Northwind Service API Version 1");
        c.SupportedSubmitMethods(new[] {
         SubmitMethod.Get, SubmitMethod.Post,
         SubmitMethod.Put, SubmitMethod.Delete });
    });
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

//app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();
