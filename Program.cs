using CheckoutService.Api;
using CheckoutService.Application;
using CheckoutService.Infrastructure;
using CheckoutService.Infrastructure.Repositories;
using CheckoutService.Infrastructure.Mocks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var useMockData = builder.Configuration.GetValue("MockData:Enabled", false)
    || string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("CheckoutDb"));

builder.Services.AddScoped<CheckoutApplicationService>();

var healthChecks = builder.Services.AddHealthChecks();

if (useMockData)
{
    builder.Services.AddSingleton<ICheckoutRepository, MockCheckoutRepository>();
    builder.Services.AddSingleton<IEventPublisher, MockEventPublisher>();
    builder.Services.AddSingleton<IShippingPromiseClient, MockShippingPromiseClient>();
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

    builder.Services.AddHttpClient<IShippingPromiseClient, ShippingPromiseClient>(client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["Services:ShippingPromise"]
            ?? throw new InvalidOperationException("ShippingPromise URL not configured"));

        client.Timeout = TimeSpan.FromMilliseconds(800);
    });

    healthChecks.AddDbContextCheck<CheckoutDbContext>();
}

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
