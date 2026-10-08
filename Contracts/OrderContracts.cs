using System.ComponentModel.DataAnnotations;
using Cronus.Ordering.Errors;
using Cronus.Ordering.Models;

namespace Cronus.Ordering.Contracts;

public sealed record CreateOrderRequest(
    [property: NotEmpty(ErrorMessage = "customerName is required.")]
    [property: MaxLength(200, ErrorMessage = "customerName cannot exceed 200 characters.")]
    string? CustomerName,
    [property: NotEmpty(ErrorMessage = "addressLine is required.")]
    [property: MaxLength(300, ErrorMessage = "addressLine cannot exceed 300 characters.")]
    string? AddressLine,
    [property: MaxLength(100, ErrorMessage = "city cannot exceed 100 characters.")]
    string? City,
    [property: MaxLength(20, ErrorMessage = "postalCode cannot exceed 20 characters.")]
    string? PostalCode);

public sealed record OrderItemResponse(
    Guid MenuItemId,
    string Name,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal)
{
    public static OrderItemResponse FromEntity(OrderItem orderItem) => new(
        orderItem.MenuItemId,
        orderItem.Name,
        orderItem.UnitPrice,
        orderItem.Quantity,
        orderItem.LineTotal);
}

/// <summary>The delivery the ordering service arranged, as far as it knows at read time.</summary>
public sealed record DeliveryInfoResponse(Guid Id, string? Status);

public sealed record OrderResponse(
    Guid Id,
    Guid RestaurantId,
    string RestaurantName,
    string CustomerName,
    string AddressLine,
    string? City,
    string? PostalCode,
    OrderStatus Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderItemResponse> Items,
    DeliveryInfoResponse? Delivery,
    string? DeliveryFailureReason)
{
    public static OrderResponse FromEntity(Order order) => new(
        order.Id,
        order.RestaurantId,
        order.RestaurantName,
        order.CustomerName,
        order.AddressLine,
        order.City,
        order.PostalCode,
        order.Status,
        order.TotalAmount,
        order.CreatedAt,
        [.. order.Items.Select(OrderItemResponse.FromEntity)],
        order.DeliveryId is null ? null : new DeliveryInfoResponse(order.DeliveryId.Value, order.DeliveryStatus),
        order.DeliveryFailureReason);
}
