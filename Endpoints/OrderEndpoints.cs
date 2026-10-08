using Cronus.Ordering.Contracts;
using Cronus.Ordering.Errors;
using Cronus.Ordering.Services;

namespace Cronus.Ordering.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders").WithTags("Orders");

        orders.MapPost("/", async (
            CreateOrderRequest request,
            OrderService orderService,
            CancellationToken cancellationToken) =>
        {
            RequestValidator.EnsureValid(request);

            var order = await orderService.PlaceOrderAsync(request, cancellationToken);
            return Results.Created($"/orders/{order.Id}", OrderResponse.FromEntity(order));
        })
        .WithName("CreateOrder")
        .WithSummary("Converts the open cart into an order and arranges its delivery.");

        orders.MapGet("/{orderId:guid}", async (
            Guid orderId,
            OrderService orderService,
            CancellationToken cancellationToken) =>
        {
            var order = await orderService.GetOrderAsync(orderId, cancellationToken);
            return Results.Ok(OrderResponse.FromEntity(order));
        })
        .WithName("GetOrder")
        .WithSummary("Returns one order together with the delivery it is linked to.");

        return app;
    }
}
