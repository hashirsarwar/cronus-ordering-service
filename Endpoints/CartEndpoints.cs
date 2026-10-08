using Cronus.Ordering.Contracts;
using Cronus.Ordering.Errors;
using Cronus.Ordering.Services;

namespace Cronus.Ordering.Endpoints;

public static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var cart = app.MapGroup("/cart").WithTags("Cart");

        cart.MapGet("/", async (CartService cartService, CancellationToken cancellationToken) =>
        {
            var openCart = await cartService.GetOpenCartAsync(cancellationToken);

            // 204 rather than a placeholder cart, so clients never have to deal with a fake identifier.
            return openCart is null
                ? Results.NoContent()
                : Results.Ok(CartResponse.FromEntity(openCart));
        })
        .WithName("GetCart")
        .WithSummary("Returns the open cart, or 204 when there is none.");

        cart.MapPost("/items", async (
            AddCartItemRequest request,
            CartService cartService,
            CancellationToken cancellationToken) =>
        {
            RequestValidator.EnsureValid(request);

            var updatedCart = await cartService.AddItemAsync(request.MenuItemId, request.Quantity, cancellationToken);
            return Results.Ok(CartResponse.FromEntity(updatedCart));
        })
        .WithName("AddCartItem")
        .WithSummary("Adds a menu item to the open cart, merging with an existing line.");

        cart.MapDelete("/items/{cartItemId:guid}", async (
            Guid cartItemId,
            CartService cartService,
            CancellationToken cancellationToken) =>
        {
            var remainingCart = await cartService.RemoveItemAsync(cartItemId, cancellationToken);

            return remainingCart is null
                ? Results.NoContent()
                : Results.Ok(CartResponse.FromEntity(remainingCart));
        })
        .WithName("RemoveCartItem")
        .WithSummary("Removes a cart line. Returns 204 when the cart becomes empty and is discarded.");

        return app;
    }
}
