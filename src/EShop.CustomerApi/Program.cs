using EShop.CustomerApi;
using EShop.CustomerApi.Accounts;
using EShop.CustomerApi.GuestOrders;
using EShop.CustomerApi.Repositories;
using EShop.Data;
using EShop.Messaging;
using EShop.ServiceDefaults;
using EShop.ServiceDefaults.Keycloak;
using Microsoft.AspNetCore.HttpLogging;
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

// Guest orders (#45): the consumer creates the account in Keycloak and reports it through the outbox.
builder.Services.AddEShopMessaging(builder.Configuration);
builder.Services.AddOutboxPublisher(CustomerApiOutbox.Producer);
builder.Services.AddMessageConsumer<GuestOrderPlacedHandler>(GuestOrdersQueue.Definition);
builder
    .Services.AddOptions<GuestAccountsOptions>()
    .Bind(builder.Configuration.GetSection(GuestAccountsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddKeycloakAdminClient<IAccountProvisioner, KeycloakAccountProvisioner>(
    builder.Configuration,
    KeycloakAccountProvisioner.SettingsSection
);

builder.Services.AddControllers();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer(
        (document, _, _) =>
        {
            document.Info.Title = "EShop Customer API";
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

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();

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
