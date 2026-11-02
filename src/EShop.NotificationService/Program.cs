using EShop.Data;
using EShop.Messaging;
using EShop.NotificationService.Email;
using EShop.NotificationService.Messaging;

var builder = Host.CreateApplicationBuilder(args);

// The inbox (processed message ids) lives in the shared database.
builder.Services.AddEShopDbContext(builder.Configuration);
builder.Services.AddEShopMessaging(builder.Configuration);
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.AddSingleton<IEmailSender, MailKitEmailSender>();

// Runs before the consumers (hosted services start in registration order).
builder.Services.AddHostedService<TopologyInitializer>();

builder.Services.AddHostedService<OrderEventsConsumer>();

builder.Build().Run();
