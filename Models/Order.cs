using System.ComponentModel.DataAnnotations;

namespace Cronus.Ordering.Models;

public class Order
{
    public Guid Id { get; set; }

    public Guid RestaurantId { get; set; }

    /// <summary>Captured when the order is placed, so the order still reads correctly if the restaurant is renamed.</summary>
    [MaxLength(200)]
    public required string RestaurantName { get; set; }

    [MaxLength(200)]
    public required string CustomerName { get; set; }

    [MaxLength(300)]
    public required string AddressLine { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Placed;

    public decimal TotalAmount { get; set; }

    /// <summary>Identifier of the delivery in the delivery service, once one has been arranged.</summary>
    public Guid? DeliveryId { get; set; }

    /// <summary>
    /// The delivery service's own status token, stored verbatim. Deliberately a string rather than a
    /// mirrored enum: the ordering service does not own that vocabulary and must not fail when it grows.
    /// </summary>
    [MaxLength(32)]
    public string? DeliveryStatus { get; set; }

    /// <summary>
    /// Coarse, user-safe reason recorded when arranging a delivery fails. The underlying exception is
    /// logged but never persisted, so internal host names and stack details stay out of the database.
    /// </summary>
    [MaxLength(300)]
    public string? DeliveryFailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<OrderItem> Items { get; set; } = [];
}
