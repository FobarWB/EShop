using EShop.Messaging.Consumers;
using EShop.Messaging.Outbox;
using EShop.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace EShop.Tests.Common;

/// <summary>
/// Runs an API in memory (TestServer) with its connection string pointed at the test database.
/// </summary>
public class EShopApiFactory<TEntryPoint, TContext> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
    where TContext : DbContext, IServiceDbContext
{
    private readonly TestDatabase<TContext> _database;

    public EShopApiFactory(TestDatabase<TContext> database)
    {
        _database = database;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            $"ConnectionStrings:{ServiceDbContextExtensions.ConnectionStringName}",
            _database.ConnectionString
        );
        // Tests never reach Keycloak: the test scheme becomes the default one instead of JwtBearer.
        builder.ConfigureTestServices(services =>
        {
            // The outbox publisher and the message consumers would reach the developer's RabbitMQ (user-secrets hold
            // its password): publish test events there and take real messages from its queues. API tests check the
            // outbox rows and call handlers directly; publishing and consuming are tested against their own broker.
            var brokerServices = services
                .Where(d =>
                    d.ImplementationType is { IsGenericType: true } type
                    && (
                        type.GetGenericTypeDefinition() == typeof(OutboxPublisher<>)
                        || type.GetGenericTypeDefinition() == typeof(MessageConsumer<,>)
                    )
                )
                .ToList();
            foreach (var descriptor in brokerServices)
            {
                services.Remove(descriptor);
            }

            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    null
                );
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // The API must never write to the development database: check what it really uses.
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var database = new NpgsqlConnectionStringBuilder(
            db.Database.GetConnectionString()
        ).Database;
        if (database != _database.DatabaseName)
        {
            host.Dispose();
            throw new InvalidOperationException(
                $"The API uses database '{database}' instead of '{_database.DatabaseName}'."
            );
        }

        return host;
    }
}
