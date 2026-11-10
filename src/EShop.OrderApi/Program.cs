using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using EShop.Data;
using EShop.Messaging;
using EShop.OrderApi;
using EShop.OrderApi.Accounts;
using EShop.OrderApi.GuestAccounts;
using EShop.OrderApi.Repositories;
using EShop.ServiceDefaults;
using EShop.ServiceDefaults.Keycloak;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;

const string ClientCorsPolicy = "Client";

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(
        ClientCorsPolicy,
        policy =>
            policy.WithOrigins(allowedOrigins).WithMethods("GET", "POST", "PUT").AllowAnyHeader()
    )
);

builder.Services.AddEShopDbContext(builder.Configuration);

// The checkout and the cancellation write events to the outbox; the publisher sends them to RabbitMQ.
builder.Services.AddEShopMessaging(builder.Configuration);
builder.Services.AddOutboxPublisher(OrderApiOutbox.Producer);

// Guest orders (#45): attaches them to the account CustomerApi created.
builder.Services.AddMessageConsumer<GuestAccountReadyHandler>(GuestAccountsQueue.Definition);

builder.Services.AddHealthChecks().AddDbContextCheck<EShopDbContext>();
builder.Services.AddEShopAuthentication(builder.Configuration);
builder.Services.AddEShopProblemDetails();

// Enums as names in JSON ("status": "Cancelled"), in responses and in request bodies.
builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())
    );

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer(
        (document, _, _) =>
        {
            document.Info.Title = "EShop Order API";
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

builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// The guest checkout asks Keycloak whether an email already has an account (service account eshop-orderapi).
builder.Services.AddKeycloakAdminClient<IAccountDirectory, KeycloakAccountDirectory>(
    builder.Configuration,
    KeycloakAccountDirectory.SettingsSection
);

// Behind nginx every request comes from the proxy: the client's address is in X-Forwarded-For. The APIs are reached
// only through nginx (their ports are not published in compose), so any proxy is trusted; with the default
// ForwardLimit of 1 the address nginx appended is taken, so a client cannot choose its own by sending the header.
// A public server revisits this (#59).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// The guest checkout is anonymous and makes Keycloak look up an email: a limit per address against flooding and
// against guessing which emails have accounts.
var guestPermits = builder.Configuration.GetValue(
    GuestCheckoutRateLimit.PermitsSetting,
    GuestCheckoutRateLimit.DefaultPermits
);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(
        GuestCheckoutRateLimit.PolicyName,
        context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = guestPermits,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }
            )
    );
});

var app = builder.Build();

// Before everything else: what follows sees the client's address, not the proxy's.
app.UseForwardedHeaders();

// First of the rest: catches exceptions thrown by everything after it.
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
app.UseRateLimiter();

app.MapControllers();

// Liveness and database connectivity for Docker and the proxy; health probes are not worth an HTTP log entry.
app.MapHealthChecks("/health").AllowAnonymous().WithHttpLogging(HttpLoggingFields.None);

app.Run();
