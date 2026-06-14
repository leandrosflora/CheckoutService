using CheckoutService.Api;
using CheckoutService.Application;
using CheckoutService.Infrastructure;
using CheckoutService.Infrastructure.Repositories;
using CheckoutService.Infrastructure.Mocks;
using CheckoutService.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using CheckoutService.Application.Ports;

var builder = WebApplication.CreateBuilder(args);

var useMockData = builder.Configuration.GetValue("MockData:Enabled", false)
    || string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("CheckoutDb"));

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.AddScoped<CheckoutApplicationService>();

var healthChecks = builder.Services.AddHealthChecks();

if (useMockData)
{
    builder.Services.AddSingleton<ICheckoutRepository, MockCheckoutRepository>();
    if (builder.Configuration.GetSection(KafkaOptions.SectionName).Exists())
    {
        builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
        builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
    }
    else
    {
        builder.Services.AddSingleton<IEventPublisher, MockEventPublisher>();
    }
}
else
{
    builder.Services.AddDbContext<CheckoutDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("CheckoutDb")
            ?? throw new InvalidOperationException("CheckoutDb connection string not configured");

        options.UseNpgsql(connectionString);
    });

    builder.Services.AddScoped<ICheckoutRepository, CheckoutRepository>();
    builder.Services.AddScoped<IEventPublisher, OutboxEventPublisher>();
    builder.Services.AddScoped<IShippingPromiseProjectionRepository, ShippingPromiseProjectionRepository>();
    builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
    builder.Services.AddHostedService<OutboxKafkaDispatcher>();
    builder.Services.AddHostedService<ShippingPromiseCalculatedConsumer>();

    healthChecks.AddDbContextCheck<CheckoutDbContext>();
}

builder.Services.AddHttpClient<IShippingPromiseClient, ShippingPromiseClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:ShippingPromise"]
        ?? throw new InvalidOperationException("ShippingPromise URL not configured"));

    client.Timeout = TimeSpan.FromMilliseconds(800);
});

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapCheckoutEndpoints();

app.Run();
