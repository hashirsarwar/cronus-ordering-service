using System.ComponentModel.DataAnnotations;

namespace Cronus.Ordering.Models;

public class Restaurant
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(300)]
    public required string AddressLine { get; set; }

    [MaxLength(100)]
    public required string City { get; set; }

    public bool IsActive { get; set; } = true;

    public List<MenuItem> MenuItems { get; set; } = [];
}
