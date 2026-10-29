using EShop.NotificationService.Email;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.AddSingleton<IEmailSender, MailKitEmailSender>();

// The consumers of the OrderApi events are registered here (step 6).

builder.Build().Run();
