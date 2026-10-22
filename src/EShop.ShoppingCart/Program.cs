using System.Text.Json;
using System.Text.Json.Serialization;
using EShop.Data;
using EShop.ShoppingCart.Repositories;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Scalar.AspNetCore;
using static System.Console;

const string ClientCorsPolicy = "Client";

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(
        ClientCorsPolicy,
        policy =>
            policy
                .WithOrigins(allowedOrigins)
                .WithMethods("GET", "POST", "PUT", "DELETE")
                .AllowAnyHeader()
    )
);

builder.Services.AddEShopDbContext(builder.Configuration);

builder
    .Services.AddControllers(options =>
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
                logger.LogInformation(
                    " {0}, Media types: {1}",
                    mediaFormatter.GetType().Name,
                    string.Join(", ", mediaFormatter.SupportedMediaTypes)
                );
            }
            else
            {
                logger.LogInformation($" {formatter.GetType().Name}");
            }
        }
    })
    .AddXmlDataContractSerializerFormatters()
    .AddXmlSerializerFormatters()
    .AddJsonOptions(x => x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer(
        (document, _, _) =>
        {
            document.Info.Title = "EShop Shopping Cart API";
            document.Info.Version = "v1";
            return Task.CompletedTask;
        }
    );
});

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields =
        HttpLoggingFields.RequestPropertiesAndHeaders
        | HttpLoggingFields.ResponsePropertiesAndHeaders
        | HttpLoggingFields.Duration;
});

builder.Services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();

var app = builder.Build();

app.UseHttpLogging();

app.UseCors(ClientCorsPolicy);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // OpenAPI document: /openapi/v1.json
    app.MapScalarApiReference(); // interactive UI: /scalar
}

app.UseAuthorization();

app.MapControllers();

app.Run();
