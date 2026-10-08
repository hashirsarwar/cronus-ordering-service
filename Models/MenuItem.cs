using System.ComponentModel.DataAnnotations;

namespace Cronus.Ordering.Models;

public class MenuItem
{
    public Guid Id { get; set; }

    public Guid RestaurantId { get; set; }

    public Restaurant? Restaurant { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public decimal Price { get; set; }

    public bool IsAvailable { get; set; } = true;
}
