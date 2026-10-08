using System.ComponentModel.DataAnnotations;
using Cronus.Ordering.Errors;
using Cronus.Ordering.Models;

namespace Cronus.Ordering.Contracts;

/// <summary>The quantity cap is shared with <see cref="CartLimits"/> so the message stays accurate.</summary>
public sealed record AddCartItemRequest(
    [property: NotEmpty(ErrorMessage = "menuItemId is required.")]
    Guid MenuItemId,
    [property: Range(1, CartLimits.MaxQuantityPerItem, ErrorMessage = "quantity must be between 1 and 20.")]
    int Quantity);

public sealed record CartItemResponse(
    Guid Id,
    Guid MenuItemId,
    string Name,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal)
{
    public static CartItemResponse FromEntity(CartItem cartItem) => new(
        cartItem.Id,
        cartItem.MenuItemId,
        cartItem.MenuItem?.Name ?? string.Empty,
        cartItem.UnitPrice,
        cartItem.Quantity,
        cartItem.LineTotal);
}

public sealed record CartResponse(
    Guid Id,
    Guid? RestaurantId,
    string? RestaurantName,
    CartStatus Status,
    IReadOnlyList<CartItemResponse> Items,
    decimal Total)
{
    public static CartResponse FromEntity(Cart cart) => new(
        cart.Id,
        cart.RestaurantId,
        cart.Restaurant?.Name,
        cart.Status,
        [.. cart.Items.Select(CartItemResponse.FromEntity)],
        cart.Total);
}
