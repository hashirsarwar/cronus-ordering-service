namespace Cronus.Ordering.Models;

/// <summary>
/// There is a single open cart per installation; Cronus has no user accounts yet, so the cart
/// is not scoped to a customer. It is locked to the restaurant of its first item.
/// </summary>
public class Cart
{
    public Guid Id { get; set; }

    public Guid? RestaurantId { get; set; }

    public Restaurant? Restaurant { get; set; }

    public CartStatus Status { get; set; } = CartStatus.Open;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<CartItem> Items { get; set; } = [];

    public decimal Total => Items.Sum(item => item.LineTotal);
}
