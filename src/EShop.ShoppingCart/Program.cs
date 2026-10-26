using System.Text.Json;
using System.Text.Json.Serialization;
using EShop.Data;
using EShop.ServiceDefaults;
using EShop.ShoppingCart.Repositories;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

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
builder.Services.AddHealthChecks().AddDbContextCheck<EShopDbContext>();
builder.Services.AddEShopAuthentication(builder.Configuration);
builder.Services.AddEShopProblemDetails();

builder
    .Services.AddControllers()
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

app.MapControllers();

// Liveness and database connectivity for Docker and the proxy; health probes are not worth an HTTP log entry.
app.MapHealthChecks("/health").AllowAnonymous().WithHttpLogging(HttpLoggingFields.None);

app.Run();
