using EShop.Data;
using EShop.ProductApi.Repositories;
using EShop.ServiceDefaults;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc.Formatters;
using Scalar.AspNetCore;
using static System.Console;

const string ClientCorsPolicy = "Client";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
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
builder.Services.AddHealthChecks().AddDbContextCheck<EShopDbContext>();
builder.Services.AddEShopAuthentication(builder.Configuration);
builder.Services.AddEShopProblemDetails();

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
            document.Info.Title = "EShop Product API";
            document.Info.Version = "v1";
            return Task.CompletedTask;
        }
    );
});

builder.Services.AddHttpLogging(options =>
{
    // Metadata only: request and response bodies may contain passwords and tokens.
    options.LoggingFields =
        HttpLoggingFields.RequestPropertiesAndHeaders
        | HttpLoggingFields.ResponsePropertiesAndHeaders
        | HttpLoggingFields.Duration;
});

builder.Services.AddScoped<IProductRepository, ProductRepository>();

var app = builder.Build();

// First in the pipeline: catches exceptions thrown by everything after it.
app.UseExceptionHandler();

app.UseHttpLogging();

app.UseCors(ClientCorsPolicy);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous(); // OpenAPI document: /openapi/v1.json
    app.MapScalarApiReference().AllowAnonymous(); // interactive UI: /scalar
}

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<SecurityHeaders>();

app.MapControllers();

// Liveness and database connectivity for Docker and the proxy; health probes are not worth an HTTP log entry.
app.MapHealthChecks("/health").AllowAnonymous().WithHttpLogging(HttpLoggingFields.None);

app.Run();
