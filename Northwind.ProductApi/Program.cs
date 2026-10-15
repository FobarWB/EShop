using Microsoft.AspNetCore.Mvc.Formatters;
using EShop.Shared; 
using static System.Console;
using Northwind.ProductApi.Repositories;
using Microsoft.AspNetCore.HttpLogging;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("https://localhost:5002/");

// Add services to the container.
builder.Services.AddCors();

builder.Services.AddNorthwindContext();

builder.Services.AddControllers(options => 
{
    WriteLine("Default output formatters:");
    foreach (IOutputFormatter formatter in options.OutputFormatters)
    {
        OutputFormatter? mediaFormatter = formatter as OutputFormatter;
        if (mediaFormatter == null)
        {
            WriteLine($" {formatter.GetType().Name}");
        }
        else
        {
            WriteLine(" {0}, Media types: {1}", 
            arg0: mediaFormatter.GetType().Name,
            arg1: string.Join(", ", mediaFormatter.SupportedMediaTypes));
        }
    }
}).AddXmlDataContractSerializerFormatters().AddXmlSerializerFormatters();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Northwind Product API";
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

builder.Services.AddScoped<IProductRepository, ProductRepository>();


var app = builder.Build();

app.UseHttpLogging();

app.UseCors(configurePolicy: options =>
{
  options.WithMethods("GET", "POST", "PUT", "DELETE");
  options.WithOrigins(
    "https://localhost:5002", // allow requests from the MVC client
    "http://localhost:4200" // Angular client
  );
    options.AllowAnyHeader();
});

app.UseCors("AllowSpecificOrigins");
//app.UseCors(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // OpenAPI document: /openapi/v1.json
    app.MapScalarApiReference(); // interactive UI: /scalar
}
app.UseHttpLogging();

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseMiddleware<SecurityHeaders>();

app.MapControllers();

app.Run();
