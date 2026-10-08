using Cronus.Ordering.Models;

namespace Cronus.Ordering.Contracts;

public sealed record RestaurantResponse(
    Guid Id,
    string Name,
    string? Description,
    string AddressLine,
    string City)
{
    public static RestaurantResponse FromEntity(Restaurant restaurant) => new(
        restaurant.Id,
        restaurant.Name,
        restaurant.Description,
        restaurant.AddressLine,
        restaurant.City);
}

public sealed record MenuItemResponse(
    Guid Id,
    Guid RestaurantId,
    string Name,
    string? Description,
    decimal Price,
    bool IsAvailable)
{
    public static MenuItemResponse FromEntity(MenuItem menuItem) => new(
        menuItem.Id,
        menuItem.RestaurantId,
        menuItem.Name,
        menuItem.Description,
        menuItem.Price,
        menuItem.IsAvailable);
}
