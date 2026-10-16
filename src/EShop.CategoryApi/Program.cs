using EShop.CategoryApi.Repositories;
using EShop.Data;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc.Formatters;
using Scalar.AspNetCore;
using static System.Console;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors();

builder.Services.AddEShopDbContext(builder.Configuration);

builder
    .Services.AddControllers(options =>
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
                WriteLine(
                    " {0}, Media types: {1}",
                    arg0: mediaFormatter.GetType().Name,
                    arg1: string.Join(", ", mediaFormatter.SupportedMediaTypes)
                );
            }
        }
    })
    .AddXmlDataContractSerializerFormatters()
    .AddXmlSerializerFormatters();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer(
        (document, _, _) =>
        {
            document.Info.Title = "EShop Category API";
            document.Info.Version = "v1";
            return Task.CompletedTask;
        }
    );
});

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.All;
    options.RequestBodyLogLimit = 4096; // default is 32k
    options.ResponseBodyLogLimit = 4096; // default is 32k
});

builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();

var app = builder.Build();

app.UseHttpLogging();

app.UseCors(configurePolicy: options =>
{
    options.WithMethods("GET", "POST", "PUT", "DELETE");
    options.WithOrigins(
        "https://localhost:5003", // allow requests from the MVC client
        "http://localhost:4200"
    );
    options.AllowAnyHeader();
});

app.UseCors("AllowSpecificOrigins");

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
