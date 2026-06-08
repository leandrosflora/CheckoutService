using CheckoutService.Api;
using CheckoutService.Application;
using CheckoutService.Infrastructure;
using CheckoutService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CheckoutDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("CheckoutDb")
        ?? throw new InvalidOperationException("CheckoutDb connection string not configured");

    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<CheckoutApplicationService>();
builder.Services.AddScoped<ICheckoutRepository, CheckoutRepository>();
builder.Services.AddScoped<IEventPublisher, OutboxEventPublisher>();

builder.Services.AddHttpClient<IShippingPromiseClient, ShippingPromiseClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:ShippingPromise"]
        ?? throw new InvalidOperationException("ShippingPromise URL not configured"));

    client.Timeout = TimeSpan.FromMilliseconds(800);
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CheckoutDbContext>();

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
