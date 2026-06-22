using CheckoutService.Api;
using CheckoutService.Application;
using CheckoutService.Infrastructure;
using CheckoutService.Infrastructure.Repositories;
using CheckoutService.Infrastructure.Mocks;
using CheckoutService.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using CheckoutService.Application.Ports;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var serviceName = builder.Environment.ApplicationName;
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:5107";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

var useMockData = builder.Configuration.GetValue("MockData:Enabled", false)
    || string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("CheckoutDb"));
var kafkaSection = builder.Configuration.GetSection(KafkaOptions.SectionName);
var kafkaConfigured = kafkaSection.Exists()
    && !string.IsNullOrWhiteSpace(kafkaSection.GetValue<string>(nameof(KafkaOptions.BootstrapServers)));

builder.Services.Configure<KafkaOptions>(kafkaSection);
builder.Services.AddScoped<CheckoutApplicationService>();

var healthChecks = builder.Services.AddHealthChecks();

if (useMockData)
{
    builder.Services.AddSingleton<ICheckoutRepository, MockCheckoutRepository>();
    builder.Services.AddSingleton<IShippingPromiseProjectionRepository, InMemoryShippingPromiseProjectionRepository>();
    if (kafkaConfigured)
    {
        builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
        builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        builder.Services.AddHostedService<ShippingPromiseCalculatedConsumer>();
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
    if (kafkaConfigured)
    {
        builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
        builder.Services.AddHostedService<OutboxKafkaDispatcher>();
        builder.Services.AddHostedService<ShippingPromiseCalculatedConsumer>();
    }

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

app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapCheckoutEndpoints();

app.Run();
