using CheckoutService.Application;
using CheckoutService.Contracts;

namespace CheckoutService.Api;

public static class CheckoutEndpoints
{
    public static IEndpointRouteBuilder MapCheckoutEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/checkouts")
            .WithTags("Checkouts");

        group.MapPost("/", async Task<IResult> (
            CreateCheckoutRequest request,
            HttpContext httpContext,
            CheckoutApplicationService service,
            CancellationToken cancellationToken) =>
        {
            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();

            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return Results.BadRequest(new
                {
                    error = "Idempotency-Key header is required"
                });
            }

            var correlationId = httpContext.Request.Headers["X-Correlation-Id"].ToString();
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = httpContext.TraceIdentifier;
            }

            var response = await service.CreateCheckoutAsync(
                request,
                idempotencyKey,
                correlationId,
                cancellationToken);

            return Results.Created($"/checkouts/{response.CheckoutId}", response);
        });

        group.MapGet("/{checkoutId:guid}", async Task<IResult> (
            Guid checkoutId,
            CheckoutApplicationService service,
            CancellationToken cancellationToken) =>
        {
            var response = await service.GetCheckoutAsync(checkoutId, cancellationToken);

            return response is null
                ? Results.NotFound()
                : Results.Ok(response);
        });

        group.MapPost("/{checkoutId:guid}/confirm", async Task<IResult> (
            Guid checkoutId,
            ConfirmCheckoutRequest request,
            HttpContext httpContext,
            CheckoutApplicationService service,
            CancellationToken cancellationToken) =>
        {
            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();

            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return Results.BadRequest(new
                {
                    error = "Idempotency-Key header is required"
                });
            }

            var response = await service.ConfirmCheckoutAsync(
                checkoutId,
                request,
                idempotencyKey,
                cancellationToken);

            return Results.Ok(response);
        });

        return app;
    }
}
