using System.ComponentModel.DataAnnotations;

namespace Cronus.Ordering.Models;

public class OrderItem
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Order? Order { get; set; }

    public Guid MenuItemId { get; set; }

    /// <summary>Name and price are snapshots taken when the order is placed.</summary>
    [MaxLength(200)]
    public required string Name { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}
