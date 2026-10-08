using Cronus.Ordering.Contracts;
using Cronus.Ordering.Data;
using Cronus.Ordering.Errors;
using Cronus.Ordering.Models;
using Microsoft.EntityFrameworkCore;

namespace Cronus.Ordering.Services;

/// <summary>Turns an open cart into a persisted order and arranges its delivery.</summary>
public sealed class OrderService(
    OrderingDbContext db,
    CartService cartService,
    DeliveryClient deliveryClient,
    ILogger<OrderService> logger)
{
    public async Task<Order> PlaceOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var cart = await cartService.GetOpenCartAsync(cancellationToken);

        if (cart?.RestaurantId is null || cart.Items.Count == 0)
        {
            throw new RequestValidationException("cart", "The cart is empty. Add items before placing an order.");
        }

        var order = CreateOrder(cart, request);

        db.Orders.Add(order);
        cart.Status = CartStatus.Converted;
        cart.UpdatedAt = DateTimeOffset.UtcNow;

        // Persisted before calling out, so an unreachable delivery service can never lose a placed order.
        await db.SaveChangesAsync(cancellationToken);

        // Record placement before calling delivery so failures still leave a placement event.
        logger.LogInformation(
            "Placed order {OrderId} at restaurant {RestaurantId} with {ItemCount} line(s) totalling {TotalAmount}.",
            order.Id,
            order.RestaurantId,
            order.Items.Count,
            order.TotalAmount);

        await ArrangeDeliveryAsync(order, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return order;
    }

    public async Task<Order> GetOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        await db.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken)
        ?? throw new NotFoundException($"Order '{orderId}' was not found.");

    private static Order CreateOrder(Cart cart, CreateOrderRequest request) => new()
    {
        RestaurantId = cart.RestaurantId!.Value,
        RestaurantName = cart.Restaurant?.Name ?? "Unknown restaurant",
        CustomerName = request.CustomerName!.Trim(),
        AddressLine = request.AddressLine!.Trim(),
        City = Normalise(request.City),
        PostalCode = Normalise(request.PostalCode),
        TotalAmount = cart.Items.Sum(item => item.LineTotal),
        // Name and price are snapshotted, so a later menu edit cannot rewrite an existing order.
        Items =
        [
            .. cart.Items.Select(cartItem => new OrderItem
            {
                MenuItemId = cartItem.MenuItemId,
                Name = cartItem.MenuItem?.Name ?? "Unknown item",
                UnitPrice = cartItem.UnitPrice,
                Quantity = cartItem.Quantity,
            })
        ],
    };

    private async Task ArrangeDeliveryAsync(Order order, CancellationToken cancellationToken)
    {
        var request = new DeliveryRequest(
            order.Id,
            order.CustomerName,
            order.AddressLine,
            order.City,
            order.PostalCode);

        try
        {
            var delivery = await deliveryClient.CreateDeliveryAsync(request, cancellationToken);

            order.DeliveryId = delivery.Id;
            order.DeliveryStatus = delivery.Status;
            order.DeliveryFailureReason = null;
            order.Status = OrderStatus.Confirmed;

            logger.LogInformation(
                "Confirmed order {OrderId} with delivery {DeliveryId}, which is {Status}.",
                order.Id,
                delivery.Id,
                delivery.Status);
        }
        catch (DeliveryRequestException exception)
        {
            order.DeliveryFailureReason = exception.UserSafeReason;
            order.Status = OrderStatus.Placed;

            // Log at error because the persisted order needs delivery and no automatic retry will arrange it.
            logger.LogError(
                exception,
                "Order {OrderId} was placed but its delivery could not be arranged ({DeliveryFailure}).",
                order.Id,
                exception.UserSafeReason);
        }
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
