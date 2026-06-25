using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace CheckoutService.Infrastructure.Database;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public DatabaseHealthCheck(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("CheckoutDb")
            ?? throw new InvalidOperationException("CheckoutDb connection string not configured");
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Unable to connect to CheckoutDb", ex);
        }
    }
}
