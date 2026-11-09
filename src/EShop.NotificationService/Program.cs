using EShop.Data;
using EShop.Messaging;
using EShop.NotificationService.Email;
using EShop.NotificationService.OrderEvents;

var builder = Host.CreateApplicationBuilder(args);

// The inbox (processed message ids) lives in the shared database.
builder.Services.AddEShopDbContext(builder.Configuration);
builder.Services.AddEShopMessaging(builder.Configuration);
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.AddSingleton<IEmailSender, MailKitEmailSender>();

// Declares its queues itself and connects as soon as RabbitMQ is available.
builder.Services.AddMessageConsumer<OrderEmailHandler>(OrderEventsQueue.Definition);

builder.Build().Run();
